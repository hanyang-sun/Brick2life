using System;
using System.Collections.Generic;
using Brick2Life.Core;

namespace Brick2Life.Battle
{
    /// <summary>
    /// Deterministic battle clock: movement, tower damage, leaks and wave transitions. Unity only renders
    /// the published snapshots. Ported from the archived BrickDefense simulation (Brackeys-derived rules,
    /// Unlicense; see THIRD_PARTY_NOTICES.md) and extended with pause and restart.
    /// </summary>
    public sealed class BattleSimulation
    {
        public const double FixedStep = 1.0 / 60;

        private sealed class Enemy
        {
            public int Id, Health, SpawnIndex;
            public double Progress, X, Y;
        }

        private sealed class Tower
        {
            public string Id;
            public double X, Y, Cooldown;
        }

        private readonly GridPoint[][] routes;
        private readonly WavePlan plan;
        private readonly TowerSettings tower;
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Tower> towers = new List<Tower>();
        private BattlePhase phase = BattlePhase.Ready;
        private int coreHealth, wave, spawned, killed, leaked, nextId;
        private double accumulator, waveTime, nextSpawn, elapsed;
        private bool paused;

        public BattleState State { get; private set; }
        public WavePlan Plan => plan;
        public TowerSettings Towers => tower;
        public bool Paused => paused;

        public BattleSimulation(BattleLevel level, WavePlan plan, TowerSettings tower)
        {
            IReadOnlyList<BattleIssue> issues = BattleValidation.Check(level, plan, tower);
            if (issues.Count != 0) throw new ArgumentException(issues[0].ToString());
            this.plan = plan; this.tower = tower; coreHealth = tower.InitialCoreHealth;
            routes = new GridPoint[level.Routes.Count][];
            for (int r = 0; r < routes.Length; r++)
            {
                BattleRoute source = level.Routes[r];
                routes[r] = new GridPoint[source.Route.Count];
                for (int p = 0; p < routes[r].Length; p++) routes[r][p] = source.Route[p];
            }
            for (int y = 0; y < level.Height; y++)
                for (int x = 0; x < level.Width; x++)
                {
                    BattleCell cell = level.CellAt(x, y);
                    if (cell.Role != Application.GameRole.Tower) continue;
                    towers.Add(new Tower { Id = cell.SourceBrickId + "-" + x + "-" + y, X = x + 0.5, Y = y + 0.5 });
                }
            towers.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            Publish();
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
            phase = BattlePhase.Ready;
            coreHealth = tower.InitialCoreHealth;
            wave = spawned = killed = leaked = nextId = 0;
            accumulator = waveTime = elapsed = 0;
            nextSpawn = 0;
            paused = false;
            foreach (Tower item in towers) item.Cooldown = 0;
            Publish();
        }

        public IReadOnlyList<ShotEvent> Step(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > 5)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Advance by 0 to 5 seconds at a time.");
            var shots = new List<ShotEvent>();
            if (paused || State.Finished || State.CanStartWave || seconds == 0) return shots.AsReadOnly();
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
                GridPoint[] route = routes[enemy.SpawnIndex];
                enemy.Progress = Math.Min(route.Length - 1, enemy.Progress + plan.EnemySpeed * FixedStep);
                int index = Math.Min((int)enemy.Progress, route.Length - 2);
                double fraction = enemy.Progress - index;
                enemy.X = route[index].X + (route[index + 1].X - route[index].X) * fraction;
                enemy.Y = route[index].Y + (route[index + 1].Y - route[index].Y) * fraction;
            }
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
                target.Health = Math.Max(0, target.Health - tower.Damage);
                item.Cooldown = tower.AttackInterval;
                bool dead = target.Health == 0;
                if (dead) killed++;
                shots.Add(new ShotEvent(item.Id, target.Id, item.X, item.Y, target.X, target.Y, dead));
            }
            // Damage settles before arrival in the same tick, so a killed enemy cannot leak.
            for (int i = 0; i < enemies.Count;)
            {
                Enemy enemy = enemies[i];
                if (enemy.Health <= 0) { enemies.RemoveAt(i); continue; }
                if (enemy.Progress >= routes[enemy.SpawnIndex].Length - 1 - 1e-10)
                {
                    leaked++;
                    coreHealth = Math.Max(0, coreHealth - plan.CoreDamage);
                    enemies.RemoveAt(i);
                    if (coreHealth == 0) { phase = BattlePhase.Defeat; return; }
                    continue;
                }
                i++;
            }
            if (spawned == plan.EnemiesPerWave && enemies.Count == 0)
                phase = wave == plan.WaveCount ? BattlePhase.Victory : BattlePhase.AwaitingNextWave;
        }

        private bool Finished() => phase == BattlePhase.Victory || phase == BattlePhase.Defeat;

        // Entrances are used round-robin and the rotation continues across waves.
        private void Spawn()
        {
            int entrance = nextId % routes.Length;
            GridPoint start = routes[entrance][0];
            enemies.Add(new Enemy { Id = ++nextId, SpawnIndex = entrance, Health = plan.EnemyHealth, X = start.X, Y = start.Y });
            spawned++;
        }

        private void Publish()
        {
            var snapshots = new EnemyState[enemies.Count];
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                snapshots[i] = new EnemyState(enemy.Id, enemy.X, enemy.Y, enemy.Progress, enemy.Health, enemy.SpawnIndex);
            }
            State = new BattleState(phase, coreHealth, wave, spawned, killed, leaked, elapsed, paused, snapshots);
        }
    }
}
