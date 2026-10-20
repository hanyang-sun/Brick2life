// Route-index following, nearest-in-range targeting, attack cooldown and death protection
// locally adapted from Brackeys/Tower-Defense-Tutorial (master, read 2026-10-11).
// Released under the Unlicense; see THIRD_PARTY_NOTICES.md and combat-a-github.md.
using System;
using System.Collections.Generic;

namespace BrickDefense.Core
{
    // One deterministic clock owns movement, damage and wave completion. Unity only renders snapshots.
    public sealed class BattleSimulation
    {
        public const double FixedStep=1.0/60;
        private sealed class Enemy
        {
            public int Id,Health,SpawnIndex;
            public double Progress,X,Y;
        }
        private sealed class Tower
        {
            public string Id;
            public double X,Y,Cooldown;
        }
        private readonly GridPoint[][] routes;
        private readonly WavePlan plan;
        private readonly CombatConfig config;
        private readonly List<Enemy> enemies=new List<Enemy>();
        private readonly List<Tower> towers=new List<Tower>();
        private BattlePhase phase=BattlePhase.Ready;
        private int coreHealth,wave,spawned,killed,leaked,nextId;
        private double accumulator,waveTime,nextSpawn,elapsed;
        public GameState State { get; private set; }

        public BattleSimulation(LevelResult level,WavePlan plan,CombatConfig config)
        {
            Issue[] issues=Validate(level,plan,config);
            if(issues.Length!=0) throw new ArgumentException(issues[0].Message);
            this.plan=plan; this.config=config; coreHealth=config.InitialCoreHealth;
            routes=new GridPoint[level.SpawnRoutes.Count][];
            for(int r=0;r<routes.Length;r++)
            {
                routes[r]=new GridPoint[level.SpawnRoutes[r].Route.Count];
                for(int p=0;p<routes[r].Length;p++) routes[r][p]=level.SpawnRoutes[r].Route[p];
            }
            foreach(LevelEntity entity in level.Entities)
                if(entity.Role==BrickRole.Tower)
                    towers.Add(new Tower { Id=entity.BrickId,X=entity.C*2+0.5,Y=entity.R*2+0.5 });
            towers.Sort((a,b)=>string.CompareOrdinal(a.Id,b.Id));
            Publish();
        }

        public static Issue[] Validate(LevelResult level,WavePlan plan,CombatConfig config)
        {
            var issues=new List<Issue>();
            void Error(string code,string message) => issues.Add(new Issue(code,message));
            if(level==null || level.Status!=ResultStatus.Success || level.CoreBrickIds==null ||
                level.CoreBrickIds.Length==0 || level.SpawnRoutes==null || level.SpawnRoutes.Count==0 ||
                level.Entities==null || level.Cells==null || level.Cells.Length!=BoardSize.NavigationCellCount)
                Error("BATTLE_LEVEL_INVALID","Choose at least one empty start; every start needs a route to a core.");
            else
            {
                var seen=new HashSet<GridPoint>();
                bool valid=true;
                foreach(SpawnRoute entrance in level.SpawnRoutes)
                {
                    if(entrance==null || !entrance.HasRoute || !seen.Add(entrance.Spawn)) { valid=false; continue; }
                    IReadOnlyList<GridPoint> route=entrance.Route;
                    if(!route[0].Equals(entrance.Spawn)) valid=false;
                    for(int i=0;i<route.Count;i++)
                    {
                        GridPoint p=route[i];
                        if(p.X<0 || p.Y<0 || p.X>=BoardSize.Navigation || p.Y>=BoardSize.Navigation) { valid=false; continue; }
                        LevelCell cell=level.Cells[p.Y*BoardSize.Navigation+p.X];
                        if(cell==null || cell.Role==BrickRole.Wall || cell.Role==BrickRole.Tower ||
                            (i==0 && cell.Role!=null) || (i<route.Count-1 && cell.Role==BrickRole.Core)) valid=false;
                        if(i>0 && Math.Abs(p.X-route[i-1].X)+Math.Abs(p.Y-route[i-1].Y)!=1) valid=false;
                        if(i==route.Count-1 && (cell?.Role!=BrickRole.Core ||
                            cell.SourceBrickId!=entrance.TargetCoreBrickId)) valid=false;
                    }
                }
                foreach(LevelEntity entity in level.Entities)
                    if(entity==null || entity.C<0 || entity.R<0 || entity.C>=BoardSize.Physical || entity.R>=BoardSize.Physical)
                        valid=false;
                if(!valid) Error("BATTLE_ROUTE_INVALID","The route must follow empty neighbouring cells to its core.");
            }
            if(plan==null) Error("WAVE_PLAN_MISSING","Enter the wave settings.");
            else
            {
                if(plan.WaveCount<1 || plan.WaveCount>RuntimeLimits.MaxWaves)
                    Error("WAVE_COUNT_INVALID","Waves must be from 1 to 20.");
                if(plan.EnemiesPerWave<1 || plan.EnemiesPerWave>RuntimeLimits.MaxEnemiesPerWave)
                    Error("WAVE_SIZE_INVALID","Enemies per wave must be from 1 to 50.");
                if((long)plan.WaveCount*plan.EnemiesPerWave>RuntimeLimits.MaxTotalEnemies)
                    Error("WAVE_TOTAL_INVALID","A run can contain at most 400 enemies.");
                if(plan.EnemyHealth<1 || plan.EnemyHealth>RuntimeLimits.MaxEnemyHealth)
                    Error("ENEMY_HEALTH_INVALID","Enemy health must be from 1 to 10000.");
                if(!Between(plan.EnemySpeed,RuntimeLimits.MinEnemySpeed,RuntimeLimits.MaxEnemySpeed))
                    Error("ENEMY_SPEED_INVALID","Enemy speed must be from 0.1 to 20 tiles per second.");
                if(plan.CoreDamage<1 || plan.CoreDamage>RuntimeLimits.MaxCoreDamage)
                    Error("CORE_DAMAGE_INVALID","Core damage must be from 1 to 1000.");
                if(!Between(plan.SpawnInterval,RuntimeLimits.MinSpawnInterval,RuntimeLimits.MaxSpawnInterval))
                    Error("SPAWN_INTERVAL_INVALID","Spawn interval must be from 0.1 to 10 seconds.");
            }
            if(config==null || config.InitialCoreHealth<1 || config.InitialCoreHealth>1000000 ||
                config.TowerDamage<1 || config.TowerDamage>1000000 || !Between(config.TowerRange,0.1,40) ||
                !Between(config.TowerAttackInterval,0.05,60))
                Error("COMBAT_CONFIG_INVALID","Check the core health and tower settings in the scene inspector.");
            return issues.ToArray();
        }

        private static bool Between(double value,double min,double max) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value>=min && value<=max;

        public bool StartWave()
        {
            if(!State.CanStartWave) return false;
            wave++; spawned=0; waveTime=0; accumulator=0; nextSpawn=plan.SpawnInterval;
            phase=BattlePhase.Spawning;
            foreach(Tower tower in towers) tower.Cooldown=0;
            Spawn(); Publish(); return true;
        }

        public IReadOnlyList<ShotEvent> Step(double seconds)
        {
            if(double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds<0 || seconds>5)
                throw new ArgumentOutOfRangeException(nameof(seconds),"Advance by 0 to 5 seconds at a time.");
            var shots=new List<ShotEvent>();
            if(State.Finished || State.CanStartWave || seconds==0) return shots.AsReadOnly();
            accumulator+=seconds;
            while(accumulator+1e-10>=FixedStep && !StateFinished() &&
                (phase==BattlePhase.Spawning || phase==BattlePhase.Clearing))
            {
                accumulator=Math.Max(0,accumulator-FixedStep);
                Tick(shots);
            }
            Publish(); return shots.AsReadOnly();
        }

        private void Tick(List<ShotEvent> shots)
        {
            elapsed+=FixedStep; waveTime+=FixedStep;
            while(spawned<plan.EnemiesPerWave && waveTime+1e-10>=nextSpawn)
            { Spawn(); nextSpawn+=plan.SpawnInterval; }
            if(spawned==plan.EnemiesPerWave) phase=BattlePhase.Clearing;
            foreach(Enemy enemy in enemies)
            {
                GridPoint[] route=routes[enemy.SpawnIndex];
                enemy.Progress=Math.Min(route.Length-1,enemy.Progress+plan.EnemySpeed*FixedStep);
                int index=Math.Min((int)enemy.Progress,route.Length-2);
                double fraction=enemy.Progress-index;
                enemy.X=route[index].X+(route[index+1].X-route[index].X)*fraction;
                enemy.Y=route[index].Y+(route[index+1].Y-route[index].Y)*fraction;
            }
            foreach(Tower tower in towers)
            {
                tower.Cooldown=Math.Max(0,tower.Cooldown-FixedStep);
                if(tower.Cooldown>1e-10) continue;
                Enemy target=null; double best=config.TowerRange*config.TowerRange;
                foreach(Enemy enemy in enemies)
                {
                    if(enemy.Health<=0) continue;
                    double dx=enemy.X-tower.X,dy=enemy.Y-tower.Y,distance=dx*dx+dy*dy;
                    if(distance<=best && (target==null || distance<best || enemy.Id<target.Id))
                    { target=enemy; best=distance; }
                }
                if(target==null) continue;
                target.Health=Math.Max(0,target.Health-config.TowerDamage);
                tower.Cooldown=config.TowerAttackInterval;
                bool dead=target.Health==0;
                if(dead) killed++;
                shots.Add(new ShotEvent(tower.Id,target.Id,tower.X,tower.Y,target.X,target.Y,dead));
            }
            // Damage is settled before arrival in the same fixed tick; dead enemies cannot leak.
            for(int i=0;i<enemies.Count;)
            {
                Enemy enemy=enemies[i];
                if(enemy.Health<=0) { enemies.RemoveAt(i); continue; }
                if(enemy.Progress>=routes[enemy.SpawnIndex].Length-1-1e-10)
                {
                    leaked++; coreHealth=Math.Max(0,coreHealth-plan.CoreDamage); enemies.RemoveAt(i);
                    if(coreHealth==0) { phase=BattlePhase.Defeat; return; }
                    continue;
                }
                i++;
            }
            if(spawned==plan.EnemiesPerWave && enemies.Count==0)
                phase=wave==plan.WaveCount?BattlePhase.Victory:BattlePhase.AwaitingNextWave;
        }

        private bool StateFinished() => phase==BattlePhase.Victory || phase==BattlePhase.Defeat;
        private void Spawn()
        {
            // Round-robin entrances, continuing across waves (Boss Room mechanism reference;
            // no upstream code imported). The wave count remains the total across all entrances.
            int entrance=nextId%routes.Length;
            GridPoint start=routes[entrance][0];
            enemies.Add(new Enemy { Id=++nextId,SpawnIndex=entrance,Health=plan.EnemyHealth,X=start.X,Y=start.Y });
            spawned++;
        }
        private void Publish()
        {
            var snapshots=new EnemyState[enemies.Count];
            for(int i=0;i<enemies.Count;i++)
            {
                Enemy e=enemies[i]; snapshots[i]=new EnemyState(e.Id,e.X,e.Y,e.Progress,e.Health,e.SpawnIndex);
            }
            State=new GameState(phase,coreHealth,wave,spawned,killed,leaked,elapsed,snapshots);
        }
    }
}
