using System;
using System.Collections.Generic;
using Brick2Life.Core;

namespace Brick2Life.Battle
{
    /// <summary>
    /// Deterministic battle clock: movement, tower damage, leaks and wave transitions. Unity only renders
    /// the published snapshots. Ported from the archived BrickDefense simulation (Brackeys-derived rules,
    /// Unlicense; see THIRD_PARTY_NOTICES.md) and extended with pause, restart and confirmed live layouts.
    /// </summary>
    public sealed class BattleSimulation
    {
        public const double FixedStep = 1.0 / 60;

        private sealed class Enemy
        {
            public int Id, Health, SpawnIndex;
            public double Progress, X, Y;
            public GridPoint[] Route;
            public int WaypointIndex;
            public bool PendingReroute;
            public int RetryCellX, RetryCellY;
        }

        private sealed class Tower
        {
            public string Id;
            public double X, Y, Cooldown;
        }

        public const double BubbleWindup = 0.2;
        public const double BubbleFlight = 0.45;

        public sealed class Bubble
        {
            public string TowerId { get; internal set; }
            public int EnemyId { get; internal set; }
            internal double OriginX, OriginY;
            public double X { get; internal set; }
            public double Y { get; internal set; }
            public double TimeRemaining { get; internal set; }
        }

        private readonly List<Bubble> bubbles = new List<Bubble>();
        public IReadOnlyList<Bubble> Bubbles { get; }
        private readonly bool useBubbles;

        private GridPoint[][] routes;
        private BattleLevel currentLevel;
        private readonly bool keepLastValidRoutes;
        private readonly IReadOnlyList<GridPoint> fixedEntrances;
        private readonly IReadOnlyList<string> fixedCoreBrickIds;
        private IReadOnlyList<BattleRoute> activeRoutes;
        private readonly WavePlan plan;
        private readonly TowerSettings tower;
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Tower> towers = new List<Tower>();
        private BattlePhase phase = BattlePhase.Ready;
        private int coreHealth, wave, spawned, killed, leaked, nextId;
        private double accumulator, waveTime, nextSpawn, elapsed;
        private bool paused;
        private bool waitingForLayout;
        private string layoutIssue = "";
        private bool usingPreviousRoutes;
        private string routeWarning = "";

        public BattleState State { get; private set; }
        public WavePlan Plan => plan;
        public TowerSettings Towers => tower;
        public bool Paused => paused;
        public BattleLevel CurrentLevel => currentLevel;
        public IReadOnlyList<BattleRoute> ActiveRoutes => activeRoutes;

        public BattleSimulation(BattleLevel level, WavePlan plan, TowerSettings tower, bool keepLastValidRoutes = false, bool useBubbles = false)
        {
            IReadOnlyList<BattleIssue> issues = BattleValidation.Check(level, plan, tower);
            if (issues.Count != 0) throw new ArgumentException(issues[0].ToString());
            this.plan = plan; this.tower = tower; coreHealth = tower.InitialCoreHealth;
            this.keepLastValidRoutes = keepLastValidRoutes;
            this.useBubbles = useBubbles;
            Bubbles = bubbles.AsReadOnly();
            currentLevel = level;
            fixedEntrances = level.Entrances;
            fixedCoreBrickIds = level.CoreBrickIds;
            activeRoutes = level.Routes;
            routes = CopyRoutes(level);
            UpdateTowers(level, true);
            Publish();
        }

        private static GridPoint[][] CopyRoutes(BattleLevel level)
        {
            var result = new GridPoint[level.Routes.Count][];
            for (int r = 0; r < result.Length; r++)
            {
                BattleRoute source = level.Routes[r];
                result[r] = new GridPoint[source.Route.Count];
                for (int p = 0; p < result[r].Length; p++) result[r][p] = source.Route[p];
            }
            return result;
        }

        private void UpdateTowers(BattleLevel level, bool initial = false)
        {
            var existing = new Dictionary<string, Tower>();
            foreach (Tower item in towers) existing[item.Id] = item;
            towers.Clear();
            var seenBricks = new HashSet<string>();
            for (int y = 0; y < level.Height; y++)
                for (int x = 0; x < level.Width; x++)
                {
                    BattleCell cell = level.CellAt(x, y);
                    if (cell.Role != Application.GameRole.Tower) continue;
                    if (useBubbles && !seenBricks.Add(cell.SourceBrickId)) continue;
                    string id = useBubbles ? cell.SourceBrickId : cell.SourceBrickId + "-" + x + "-" + y;
                    double centerX = useBubbles ? x / BattleLevel.NavigationPerPhysical * BattleLevel.NavigationPerPhysical + BattleLevel.NavigationCenterOffset : x + 0.5;
                    double centerY = useBubbles ? y / BattleLevel.NavigationPerPhysical * BattleLevel.NavigationPerPhysical + BattleLevel.NavigationCenterOffset : y + 0.5;
                    if (!existing.TryGetValue(id, out Tower item) || item.X != centerX || item.Y != centerY)
                        item = new Tower { Id = id, X = centerX, Y = centerY,
                            Cooldown = initial ? 0 : tower.AttackInterval };
                    towers.Add(item);
                }
            towers.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        /// <summary>Updates entrance routes immediately; overlapped enemies keep their own path until they can reconnect.</summary>
        public bool TryUpdateLevel(BattleLevel candidate, out string reason)
        {
            if (State.Finished) { reason = "本局已结束。"; return false; }
            if (candidate == null) return WaitForLayout("等待有效的已确认布局。", out reason);
            if (candidate.Width != currentLevel.Width || candidate.Height != currentLevel.Height ||
                candidate.Entrances.Count != fixedEntrances.Count)
                return WaitForLayout("本局棋盘尺寸和入口数量不能改变。", out reason);
            for (int i = 0; i < fixedEntrances.Count; i++)
                if (!candidate.Entrances[i].Equals(fixedEntrances[i]))
                    return WaitForLayout("本局入口的位置和顺序不能改变。", out reason);
            if (keepLastValidRoutes)
            {
                if (candidate.CoreBrickIds.Count != fixedCoreBrickIds.Count)
                    return WaitForLayout("本局系统核心不能增删或移动。", out reason);
                for (int i = 0; i < fixedCoreBrickIds.Count; i++)
                    if (candidate.CoreBrickIds[i] != fixedCoreBrickIds[i])
                        return WaitForLayout("本局系统核心不能增删或移动。", out reason);
            }
            if (!candidate.IsValid)
            {
                string issue = candidate.Issues.Count > 0 ? candidate.Issues[0].Message : "布局不可用。";
                bool routeOnly = candidate.Issues.Count > 0;
                foreach (BattleIssue candidateIssue in candidate.Issues)
                    routeOnly &= candidateIssue.Code == "NO_ROUTE" || candidateIssue.Code == "SPAWN_OCCUPIED";
                if (keepLastValidRoutes && routeOnly) return KeepPreviousRoutes(candidate, issue, out reason);
                return WaitForLayout(issue, out reason);
            }
            var remaining = new GridPoint[enemies.Count][];
            for (int i = 0; i < enemies.Count; i++)
            {
                remaining[i] = RemainingRoute(candidate, enemies[i]);
                if (remaining[i] == null)
                {
                    string issue = "敌人 " + enemies[i].Id + " 无法从当前位置到达核心。";
                    if (!keepLastValidRoutes) return WaitForLayout(issue + "请移开障碍或恢复通路。", out reason);
                }
            }
            routes = CopyRoutes(candidate);
            activeRoutes = candidate.Routes;
            currentLevel = candidate;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                enemy.PendingReroute = remaining[i] == null;
                if (enemy.PendingReroute)
                {
                    enemy.RetryCellX = (int)Math.Floor(enemy.X);
                    enemy.RetryCellY = (int)Math.Floor(enemy.Y);
                }
                else
                {
                    enemy.Route = remaining[i];
                    enemy.WaypointIndex = 0;
                }
            }
            UpdateTowers(candidate);
            waitingForLayout = false; layoutIssue = "";
            usingPreviousRoutes = false; routeWarning = "";
            Publish();
            reason = State.RouteWarning;
            return true;
        }

        private bool KeepPreviousRoutes(BattleLevel candidate, string issue, out string reason)
        {
            // Only paths are retained: confirmed new walls and towers still become the current map.
            // Existing enemy paths/waypoint indices and the complete entrance array remain untouched.
            currentLevel = candidate;
            foreach (Enemy enemy in enemies) enemy.PendingReroute = false;
            UpdateTowers(candidate);
            waitingForLayout = false; layoutIssue = "";
            usingPreviousRoutes = true;
            routeWarning = reason = "布局堵路，沿用上次有效路线（可能穿过新放置的积木）。" + issue;
            Publish();
            return true;
        }

        private bool WaitForLayout(string message, out string reason)
        {
            waitingForLayout = true; layoutIssue = reason = message;
            usingPreviousRoutes = false; routeWarning = "";
            Publish();
            return false;
        }

        private static GridPoint[] RemainingRoute(BattleLevel candidate, Enemy enemy)
        {
            int x = (int)Math.Floor(enemy.X), y = (int)Math.Floor(enemy.Y);
            if (x < 0 || y < 0 || x >= candidate.Width || y >= candidate.Height || candidate.CellAt(x, y).Blocked)
                return null;
            GridPoint[] best = null;
            double bestCost = double.MaxValue;
            // Continuous positions remain on a four-way edge; either adjacent free endpoint may connect.
            for (int ny = y; ny <= (int)Math.Ceiling(enemy.Y); ny++)
                for (int nx = x; nx <= (int)Math.Ceiling(enemy.X); nx++)
                {
                    double dx = Math.Abs(enemy.X - nx), dy = Math.Abs(enemy.Y - ny);
                    if (dx > 1e-8 && dy > 1e-8) continue;
                    GridPoint[] path = candidate.FindRoute(new GridPoint(nx, ny), out int pathCost);
                    if (path == null) continue;
                    double cost = (dx + dy) * RouteSearch.StepCost + pathCost;
                    if (cost >= bestCost - 1e-8) continue;
                    best = path; bestCost = cost;
                }
            return best;
        }

        private void RetryPendingEnemyRoutes()
        {
            if (usingPreviousRoutes || !currentLevel.IsValid) return;
            foreach (Enemy enemy in enemies)
            {
                if (!enemy.PendingReroute) continue;
                int x = (int)Math.Floor(enemy.X), y = (int)Math.Floor(enemy.Y);
                if (x == enemy.RetryCellX && y == enemy.RetryCellY) continue;
                enemy.RetryCellX = x; enemy.RetryCellY = y;
                GridPoint[] route = RemainingRoute(currentLevel, enemy);
                if (route == null) continue;
                enemy.Route = route;
                enemy.WaypointIndex = 0;
                enemy.PendingReroute = false;
            }
        }

        public void SetPaused(bool value)
        {
            paused = value;
            Publish();
        }

        public bool StartWave()
        {
            if (State.Finished || !State.CanStartWave) return false;
            wave++; spawned = 0; waveTime = 0; accumulator = 0; nextSpawn = plan.SpawnInterval;
            phase = BattlePhase.Spawning;
            foreach (Tower item in towers) item.Cooldown = 0;
            Spawn();
            Publish();
            return true;
        }

        /// <summary>Restores the initial state so the same level and parameters can be replayed.</summary>
        public void Restart()
        {
            enemies.Clear();
            bubbles.Clear();
            phase = BattlePhase.Ready;
            coreHealth = tower.InitialCoreHealth;
            wave = spawned = killed = leaked = nextId = 0;
            accumulator = waveTime = elapsed = 0;
            nextSpawn = 0;
            paused = false;
            waitingForLayout = false; layoutIssue = "";
            foreach (Tower item in towers) item.Cooldown = 0;
            Publish();
        }

        public IReadOnlyList<ShotEvent> Step(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > 5)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Advance by 0 to 5 seconds at a time.");
            var shots = new List<ShotEvent>();
            if (paused || waitingForLayout || State.Finished || State.CanStartWave || seconds == 0) return shots.AsReadOnly();
            accumulator += seconds;
            while (accumulator + 1e-10 >= FixedStep && !Finished() &&
                (phase == BattlePhase.Spawning || phase == BattlePhase.Clearing))
            {
                accumulator = Math.Max(0, accumulator - FixedStep);
                Tick(shots);
            }
            Publish();
            return shots.AsReadOnly();
        }

        private void Tick(List<ShotEvent> shots)
        {
            elapsed += FixedStep; waveTime += FixedStep;
            while (spawned < plan.EnemiesPerWave && waveTime + 1e-10 >= nextSpawn)
            {
                Spawn();
                nextSpawn += plan.SpawnInterval;
            }
            if (spawned == plan.EnemiesPerWave) phase = BattlePhase.Clearing;
            foreach (Enemy enemy in enemies)
            {
                double remaining = plan.EnemySpeed * FixedStep;
                while (enemy.WaypointIndex < enemy.Route.Length)
                {
                    GridPoint target = enemy.Route[enemy.WaypointIndex];
                    double dx = target.X - enemy.X, dy = target.Y - enemy.Y;
                    double distance = Math.Abs(dx) + Math.Abs(dy);
                    double travel = Math.Min(distance, remaining);
                    if (distance > 1e-10)
                    {
                        enemy.X += dx * travel / distance;
                        enemy.Y += dy * travel / distance;
                    }
                    enemy.Progress += travel; remaining -= travel;
                    if (travel + 1e-10 < distance) break;
                    enemy.X = target.X; enemy.Y = target.Y;
                    enemy.WaypointIndex++;
                    if (remaining <= 1e-10) break;
                }
            }
            RetryPendingEnemyRoutes();
            AdvanceBubbles(shots);
            foreach (Tower item in towers)
            {
                item.Cooldown = Math.Max(0, item.Cooldown - FixedStep);
                if (item.Cooldown > 1e-10) continue;
                Enemy target = null;
                double best = tower.Range * tower.Range;
                foreach (Enemy enemy in enemies)
                {
                    if (enemy.Health <= 0) continue;
                    double dx = enemy.X - item.X, dy = enemy.Y - item.Y, distance = dx * dx + dy * dy;
                    if (distance <= best && (target == null || distance < best || enemy.Id < target.Id))
                    { target = enemy; best = distance; }
                }
                if (target == null) continue;
                item.Cooldown = useBubbles ? Math.Max(tower.AttackInterval, BubbleWindup + BubbleFlight) : tower.AttackInterval;
                if (useBubbles)
                {
                    bubbles.Add(new Bubble { TowerId = item.Id, EnemyId = target.Id,
                        OriginX = item.X, OriginY = item.Y, X = item.X, Y = item.Y,
                        TimeRemaining = BubbleWindup + BubbleFlight });
                }
                else
                {
                    Hit(item.Id, item.X, item.Y, target, shots);
                }
            }
            // Damage settles before arrival in the same tick, so a killed enemy cannot leak.
            for (int i = 0; i < enemies.Count;)
            {
                Enemy enemy = enemies[i];
                if (enemy.Health <= 0) { enemies.RemoveAt(i); continue; }
                if (enemy.WaypointIndex >= enemy.Route.Length)
                {
                    leaked++;
                    coreHealth = Math.Max(0, coreHealth - plan.CoreDamage);
                    enemies.RemoveAt(i);
                    if (coreHealth == 0) { bubbles.Clear(); phase = BattlePhase.Defeat; return; }
                    continue;
                }
                i++;
            }
            bubbles.RemoveAll(bubble => !enemies.Exists(enemy => enemy.Id == bubble.EnemyId));
            if (spawned == plan.EnemiesPerWave && enemies.Count == 0)
                phase = wave == plan.WaveCount ? BattlePhase.Victory : BattlePhase.AwaitingNextWave;
        }

        private void AdvanceBubbles(List<ShotEvent> shots)
        {
            for (int i = 0; i < bubbles.Count;)
            {
                Bubble bubble = bubbles[i];
                Enemy target = enemies.Find(enemy => enemy.Id == bubble.EnemyId);
                Tower source = towers.Find(item => item.Id == bubble.TowerId);
                if (target == null || target.Health == 0 ||
                    (bubble.TimeRemaining > BubbleFlight && (source == null || source.X != bubble.X || source.Y != bubble.Y)))
                {
                    bubbles.RemoveAt(i);
                    continue;
                }

                double previousTime = bubble.TimeRemaining;
                bubble.TimeRemaining -= FixedStep;
                if (bubble.TimeRemaining <= 1e-10)
                {
                    Hit(bubble.TowerId, bubble.OriginX, bubble.OriginY, target, shots);
                    bubbles.RemoveAt(i);
                    continue;
                }
                if (bubble.TimeRemaining <= BubbleFlight)
                {
                    if (previousTime > BubbleFlight)
                    {
                        double dx = target.X - bubble.X, dy = target.Y - bubble.Y;
                        double length = Math.Sqrt(dx * dx + dy * dy);
                        double reach = Math.Min(1.7, length * 0.5);
                        if (length > 1e-10)
                        {
                            bubble.X += dx * reach / length;
                            bubble.Y += dy * reach / length;
                        }
                    }
                    double progress = 1 - bubble.TimeRemaining / Math.Min(previousTime, BubbleFlight);
                    bubble.X += (target.X - bubble.X) * progress;
                    bubble.Y += (target.Y - bubble.Y) * progress;
                }
                i++;
            }
        }

        private void Hit(string towerId, double x, double y, Enemy target, List<ShotEvent> shots)
        {
            target.Health = Math.Max(0, target.Health - tower.Damage);
            bool dead = target.Health == 0;
            if (dead) killed++;
            shots.Add(new ShotEvent(towerId, target.Id, x, y, target.X, target.Y, dead));
        }

        private bool Finished() => phase == BattlePhase.Victory || phase == BattlePhase.Defeat;

        // Entrances are used round-robin and the rotation continues across waves.
        private void Spawn()
        {
            int entrance = nextId % routes.Length;
            GridPoint start = routes[entrance][0];
            enemies.Add(new Enemy { Id = ++nextId, SpawnIndex = entrance, Health = plan.EnemyHealth,
                X = start.X, Y = start.Y, Route = routes[entrance], WaypointIndex = 1 });
            spawned++;
        }

        private void Publish()
        {
            var snapshots = new EnemyState[enemies.Count];
            int pending = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy.PendingReroute) pending++;
                double headingX = 0, headingY = 0;
                for (int waypoint = enemy.WaypointIndex; waypoint < enemy.Route.Length; waypoint++)
                {
                    GridPoint target = enemy.Route[waypoint];
                    headingX = target.X - enemy.X;
                    headingY = target.Y - enemy.Y;
                    if (Math.Abs(headingX) + Math.Abs(headingY) > 1e-10) break;
                }
                snapshots[i] = new EnemyState(enemy.Id, enemy.X, enemy.Y, enemy.Progress, enemy.Health, enemy.SpawnIndex,
                    headingX, headingY);
            }
            if (!usingPreviousRoutes && !waitingForLayout)
                routeWarning = pending == 0 ? "" : "道路已更新，" + pending + " 个敌人正脱离新放置积木，脱离后重新寻路。";
            State = new BattleState(phase, coreHealth, wave, spawned, killed, leaked, elapsed, paused, snapshots,
                waitingForLayout, layoutIssue, usingPreviousRoutes, routeWarning, waitingForLayout ? 0 : pending);
        }
    }
}
