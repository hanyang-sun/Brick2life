using System;
using System.Collections.Generic;
using BrickDefense.Core;

namespace BrickDefense.Workflow
{
    public sealed class BattleSession : IDisposable
    {
        private readonly LevelSession levels;
        private BattleSimulation simulation;
        private static readonly IReadOnlyList<ShotEvent> NoShots=Array.AsReadOnly(Array.Empty<ShotEvent>());
        public GameState State => simulation?.State;
        public LevelResult Level { get; private set; }
        public WavePlan Plan { get; private set; }
        public CombatConfig Config { get; private set; }
        public int RunId { get; private set; }
        public bool Paused { get; private set; }
        public BattleSession(LevelSession levels) { this.levels=levels; levels.Changed+=LayoutChanged; }

        public bool TryStart(WavePlan plan,CombatConfig config,out Issue[] issues)
        {
            if(State!=null && !State.Finished)
            { issues=new[] {new Issue("BATTLE_ALREADY_RUNNING","Finish this run before starting another.")}; return false; }
            LevelResult source=levels.Current?.Result;
            issues=BattleSimulation.Validate(source,plan,config);
            if(issues.Length!=0) return false;
            // The simulation and renderer retain a separate map, even if caller-owned arrays change.
            Level=new LevelResult(source.Status,(LevelEntity[])source.Entities.Clone(),(LevelCell[])source.Cells.Clone(),
                (string[])source.CoreBrickIds.Clone(),CopyRoutes(source.SpawnRoutes),(Issue[])source.Issues.Clone());
            Plan=plan; Config=config; simulation=new BattleSimulation(Level,plan,config);
            Paused=false; RunId++; levels.SetBattleLock(true); simulation.StartWave(); return true;
        }
        public bool StartNextWave() => simulation!=null && !Paused && simulation.StartWave();
        public IReadOnlyList<ShotEvent> Advance(double seconds)
        {
            if(simulation==null || Paused) return NoShots;
            IReadOnlyList<ShotEvent> shots=simulation.Step(seconds);
            if(State.Finished) levels.SetBattleLock(false);
            return shots;
        }
        public void SetPaused(bool value) { if(State!=null && !State.Finished) Paused=value; }
        public bool Retry(out Issue[] issues)
        {
            if(State==null || !State.Finished)
            { issues=new[] {new Issue("BATTLE_NOT_FINISHED","Retry is available after the run ends.")}; return false; }
            return TryStart(Plan,Config,out issues);
        }
        public void ReturnToLayout()
        { levels.SetBattleLock(false); simulation=null; Level=null; Plan=null; Config=null; Paused=false; }
        private void LayoutChanged(LevelContext _) { if(State!=null && State.Finished) ReturnToLayout(); }
        private static SpawnRoute[] CopyRoutes(IReadOnlyList<SpawnRoute> source)
        {
            var routes=new SpawnRoute[source.Count];
            for(int i=0;i<routes.Length;i++)
                routes[i]=new SpawnRoute(source[i].Spawn,source[i].TargetCoreBrickId,source[i].Route);
            return routes;
        }
        public void Dispose() { ReturnToLayout(); levels.Changed-=LayoutChanged; }
    }
}
