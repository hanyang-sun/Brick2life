using System;
using System.Collections.Generic;

namespace Brick2Life.Battle
{
    public enum BattlePhase { Ready, Spawning, Clearing, AwaitingNextWave, Victory, Defeat }

    public static class BattleLimits
    {
        public const int MaxWaves = 20, MaxEnemiesPerWave = 50, MaxTotalEnemies = 400, MaxEnemyHealth = 10000,
            MaxCoreDamage = 1000, MaxInitialCoreHealth = 1000000, MaxTowerDamage = 1000000;
        public const double MinEnemySpeed = 0.1, MaxEnemySpeed = 20, MinSpawnInterval = 0.1, MaxSpawnInterval = 10,
            MinTowerRange = 0.1, MaxTowerRange = 40, MinTowerInterval = 0.05, MaxTowerInterval = 60;
    }

    /// <summary>Per-run wave parameters chosen in the battle panel.</summary>
    public sealed class WavePlan
    {
        public int WaveCount { get; }
        public int EnemiesPerWave { get; }
        public int EnemyHealth { get; }
        public double EnemySpeed { get; }
        public int CoreDamage { get; }
        public double SpawnInterval { get; }

        public WavePlan(int waveCount, int enemiesPerWave, int enemyHealth, double enemySpeed, int coreDamage, double spawnInterval)
        {
            WaveCount = waveCount; EnemiesPerWave = enemiesPerWave; EnemyHealth = enemyHealth;
            EnemySpeed = enemySpeed; CoreDamage = coreDamage; SpawnInterval = spawnInterval;
        }

        public static WavePlan Trial => new WavePlan(3, 8, 24, 1.4, 10, 1.1);
    }

    /// <summary>Core health and tower behaviour; scene-level defaults, adjustable in the panel.</summary>
    public sealed class TowerSettings
    {
        public int InitialCoreHealth { get; }
        public int Damage { get; }
        public double Range { get; }
        public double AttackInterval { get; }
        public TowerSettings(int initialCoreHealth, int damage, double range, double attackInterval)
        {
            InitialCoreHealth = initialCoreHealth; Damage = damage; Range = range; AttackInterval = attackInterval;
        }
        public static TowerSettings Trial => new TowerSettings(100, 12, 8, 0.45);
    }

    public sealed class EnemyState
    {
        public int Id { get; }
        public double X { get; }
        public double Y { get; }
        public double RouteProgress { get; }
        public int Health { get; }
        public int SpawnIndex { get; }
        public EnemyState(int id, double x, double y, double progress, int health, int spawnIndex)
        {
            Id = id; X = x; Y = y; RouteProgress = progress; Health = health; SpawnIndex = spawnIndex;
        }
    }

    public sealed class ShotEvent
    {
        public string TowerId { get; }
        public int EnemyId { get; }
        public double TowerX { get; }
        public double TowerY { get; }
        public double TargetX { get; }
        public double TargetY { get; }
        public bool Killed { get; }
        public ShotEvent(string towerId, int enemyId, double towerX, double towerY, double targetX, double targetY, bool killed)
        {
            TowerId = towerId; EnemyId = enemyId; TowerX = towerX; TowerY = towerY;
            TargetX = targetX; TargetY = targetY; Killed = killed;
        }
    }

    public sealed class BattleState
    {
        public BattlePhase Phase { get; }
        public int CoreHealth { get; }
        public int Wave { get; }
        public int SpawnedThisWave { get; }
        public int Killed { get; }
        public int Leaked { get; }
        public double Elapsed { get; }
        public bool Paused { get; }
        public IReadOnlyList<EnemyState> Enemies { get; }
        public bool Finished => Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat;
        public bool CanStartWave => Phase == BattlePhase.Ready || Phase == BattlePhase.AwaitingNextWave;

        public BattleState(BattlePhase phase, int coreHealth, int wave, int spawned, int killed, int leaked,
            double elapsed, bool paused, EnemyState[] enemies)
        {
            Phase = phase; CoreHealth = coreHealth; Wave = wave; SpawnedThisWave = spawned;
            Killed = killed; Leaked = leaked; Elapsed = elapsed; Paused = paused;
            Enemies = Array.AsReadOnly((EnemyState[])enemies.Clone());
        }
    }

    public static class BattleValidation
    {
        public static IReadOnlyList<BattleIssue> Check(BattleLevel level, WavePlan plan, TowerSettings tower)
        {
            var issues = new List<BattleIssue>();
            if (level == null || !level.IsValid || level.Routes.Count == 0)
                issues.Add(new BattleIssue("BATTLE_LEVEL_INVALID", level == null ? "缺少关卡。" : "请至少选择一个有通路的空格作为入口。"));
            if (plan == null) issues.Add(new BattleIssue("WAVE_PLAN_MISSING", "缺少波次参数。"));
            else
            {
                if (plan.WaveCount < 1 || plan.WaveCount > BattleLimits.MaxWaves)
                    issues.Add(new BattleIssue("WAVE_COUNT_INVALID", "波数必须在 1–20。"));
                if (plan.EnemiesPerWave < 1 || plan.EnemiesPerWave > BattleLimits.MaxEnemiesPerWave)
                    issues.Add(new BattleIssue("WAVE_SIZE_INVALID", "每波数量必须在 1–50。"));
                if ((long)plan.WaveCount * plan.EnemiesPerWave > BattleLimits.MaxTotalEnemies)
                    issues.Add(new BattleIssue("WAVE_TOTAL_INVALID", "一局最多 400 个敌人。"));
                if (plan.EnemyHealth < 1 || plan.EnemyHealth > BattleLimits.MaxEnemyHealth)
                    issues.Add(new BattleIssue("ENEMY_HEALTH_INVALID", "敌人生命必须在 1–10000。"));
                if (!Between(plan.EnemySpeed, BattleLimits.MinEnemySpeed, BattleLimits.MaxEnemySpeed))
                    issues.Add(new BattleIssue("ENEMY_SPEED_INVALID", "敌人速度必须在 0.1–20 格/秒。"));
                if (plan.CoreDamage < 1 || plan.CoreDamage > BattleLimits.MaxCoreDamage)
                    issues.Add(new BattleIssue("CORE_DAMAGE_INVALID", "核心伤害必须在 1–1000。"));
                if (!Between(plan.SpawnInterval, BattleLimits.MinSpawnInterval, BattleLimits.MaxSpawnInterval))
                    issues.Add(new BattleIssue("SPAWN_INTERVAL_INVALID", "出怪间隔必须在 0.1–10 秒。"));
            }
            if (tower == null) issues.Add(new BattleIssue("TOWER_SETTINGS_MISSING", "缺少塔与核心设置。"));
            else
            {
                if (tower.InitialCoreHealth < 1 || tower.InitialCoreHealth > BattleLimits.MaxInitialCoreHealth)
                    issues.Add(new BattleIssue("CORE_HEALTH_INVALID", "核心生命必须在 1–1000000。"));
                if (tower.Damage < 1 || tower.Damage > BattleLimits.MaxTowerDamage)
                    issues.Add(new BattleIssue("TOWER_DAMAGE_INVALID", "塔伤害必须在 1–1000000。"));
                if (!Between(tower.Range, BattleLimits.MinTowerRange, BattleLimits.MaxTowerRange))
                    issues.Add(new BattleIssue("TOWER_RANGE_INVALID", "塔射程必须在 0.1–40。"));
                if (!Between(tower.AttackInterval, BattleLimits.MinTowerInterval, BattleLimits.MaxTowerInterval))
                    issues.Add(new BattleIssue("TOWER_INTERVAL_INVALID", "攻击间隔必须在 0.05–60 秒。"));
            }
            return issues;
        }

        private static bool Between(double value, double min, double max) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max;
    }
}
