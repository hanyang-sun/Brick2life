using System;
using System.Collections.Generic;

namespace BrickDefense.Core
{
    public enum BattlePhase { Ready, Spawning, Clearing, AwaitingNextWave, Victory, Defeat }

    public sealed class WavePlan
    {
        public int WaveCount { get; }
        public int EnemiesPerWave { get; }
        public int EnemyHealth { get; }
        public double EnemySpeed { get; }
        public int CoreDamage { get; }
        public double SpawnInterval { get; }
        public WavePlan(int waveCount, int enemiesPerWave, int enemyHealth, double enemySpeed,
            int coreDamage, double spawnInterval)
        {
            WaveCount=waveCount; EnemiesPerWave=enemiesPerWave; EnemyHealth=enemyHealth;
            EnemySpeed=enemySpeed; CoreDamage=coreDamage; SpawnInterval=spawnInterval;
        }
        // Trial values, not measured Android performance or final game balance.
        public static WavePlan Trial => new WavePlan(3,8,24,1.4,10,1.1);
    }

    public sealed class CombatConfig
    {
        public int InitialCoreHealth { get; }
        public int TowerDamage { get; }
        public double TowerRange { get; }
        public double TowerAttackInterval { get; }
        public CombatConfig(int initialCoreHealth, int towerDamage, double towerRange, double towerAttackInterval)
        {
            InitialCoreHealth=initialCoreHealth; TowerDamage=towerDamage;
            TowerRange=towerRange; TowerAttackInterval=towerAttackInterval;
        }
        public static CombatConfig Trial => new CombatConfig(100,12,8,0.45);
    }

    public static class RuntimeLimits
    {
        // Conservative desktop trial caps. Android limits require device measurements.
        public const int MaxWaves=20, MaxEnemiesPerWave=50, MaxTotalEnemies=400, MaxEnemyHealth=10000,
            MaxCoreDamage=1000;
        public const double MinEnemySpeed=0.1, MaxEnemySpeed=20, MinSpawnInterval=0.1, MaxSpawnInterval=10;
    }

    public sealed class EnemyState
    {
        public int Id { get; }
        public double X { get; }
        public double Y { get; }
        public double RouteProgress { get; }
        public int Health { get; }
        public int SpawnIndex { get; }
        public EnemyState(int id,double x,double y,double progress,int health,int spawnIndex=0)
        { Id=id; X=x; Y=y; RouteProgress=progress; Health=health; SpawnIndex=spawnIndex; }
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
        public ShotEvent(string towerId,int enemyId,double towerX,double towerY,double targetX,double targetY,bool killed)
        {
            TowerId=towerId; EnemyId=enemyId; TowerX=towerX; TowerY=towerY;
            TargetX=targetX; TargetY=targetY; Killed=killed;
        }
    }

    public sealed class GameState
    {
        public BattlePhase Phase { get; }
        public int CoreHealth { get; }
        public int Wave { get; }
        public int SpawnedThisWave { get; }
        public int Killed { get; }
        public int Leaked { get; }
        public double Elapsed { get; }
        public IReadOnlyList<EnemyState> Enemies { get; }
        public bool Finished => Phase==BattlePhase.Victory || Phase==BattlePhase.Defeat;
        public bool CanStartWave => Phase==BattlePhase.Ready || Phase==BattlePhase.AwaitingNextWave;
        public GameState(BattlePhase phase,int coreHealth,int wave,int spawned,int killed,int leaked,
            double elapsed,EnemyState[] enemies)
        {
            Phase=phase; CoreHealth=coreHealth; Wave=wave; SpawnedThisWave=spawned;
            Killed=killed; Leaked=leaked; Elapsed=elapsed;
            Enemies=Array.AsReadOnly((EnemyState[])enemies.Clone());
        }
    }
}
