using BrickDefense.Core;
using BrickDefense.Workflow;

public static class MultipleSpawnChecks
{
    private static void Check(bool value,string message)
    { if(!value) throw new Exception("Multiple spawns: "+message); }
    public static void Run()
    {
        var levels=new LevelSession(); levels.LoadExample(false);
        GridPoint initial=new(1,18),a=new(9,9),b=new(11,9);
        Check(levels.ToggleSpawn(a) && levels.ToggleSpawn(b) && levels.Current.Result.SpawnRoutes.Count==3,"adding points replaced earlier choices");
        LevelContext retained=levels.Current;
        Check(!levels.ToggleSpawn(new GridPoint(6,8)) && ReferenceEquals(retained,levels.Current) &&
            levels.SpawnSelectionIssue.Code=="SPAWN_OCCUPIED","occupied tile changed selection or had no feedback");
        Check(!levels.ToggleSpawn(new GridPoint(-1,0)) && ReferenceEquals(retained,levels.Current),"out-of-bounds tile changed selection");
        Check(levels.ToggleSpawn(a) && levels.Current.Result.SpawnRoutes.Count==2 && levels.SpawnSelectionIssue==null,"toggle did not remove or clear feedback");
        Check(levels.Current.Result.SpawnRoutes[1].Spawn.Equals(b),"removing a point reordered remaining choices");
        levels.ToggleSpawn(initial); levels.ToggleSpawn(b);
        Check(levels.Current.Result.SpawnRoutes.Count==0 && levels.Current.Result.Entities.Length==9 &&
            levels.Current.Result.Issues[0].Code=="NO_SPAWNS","clearing starts removed geometry or stayed playable");
        using var session=new BattleSession(levels);
        Check(!session.TryStart(WavePlan.Trial,CombatConfig.Trial,out _) && !levels.IsLocked,"zero starts enabled battle");
        levels.ToggleSpawn(a); levels.ToggleSpawn(b);
        Check(session.TryStart(new WavePlan(1,4,100,20,1,0.1),CombatConfig.Trial,out _) && session.Level.SpawnRoutes.Count==2,"multi-start battle failed");
        retained=levels.Current;
        Check(!levels.ToggleSpawn(a) && ReferenceEquals(retained,levels.Current),"battle lock allowed removing an entrance");
        levels.Current.Result.Route[0]=new GridPoint(19,19);
        Check(session.Level.SpawnRoutes[0].Route[0].Equals(a) && session.State.Enemies[0].X==a.X,"snapshot leaked caller-owned route array");
        for(int i=0;i<1000 && !session.State.Finished;i++) session.Advance(0.1);
        Check(session.State.Finished && session.Retry(out _) && session.Level.SpawnRoutes.Count==2 &&
            session.State.Enemies[0].SpawnIndex==0 && session.State.Enemies[0].X==a.X,"retry lost starts or failed to reset rotation");
        session.ReturnToLayout();

        levels.LoadExample(true); retained=levels.Current;
        Check(!levels.ToggleSpawn(a) && ReferenceEquals(retained,levels.Current) && levels.SpawnSelectionIssue.Code=="NO_ROUTE","unreachable new start changed selection");
        Check(levels.ToggleSpawn(initial) && levels.Current.Result.SpawnRoutes.Count==0,"invalid default start cannot be removed");
        levels.LoadExample(false);
        Check(levels.ToggleSpawn(a) && levels.Current.Result.Status==ResultStatus.Success,"valid new start rejected after reloading layout");

        ScanResult scan=ExampleScans.Parse("two-route-check",new[] {"..........",".K........","..........","..........","..........",
            "..........","..........","..........","........K.",".........."});
        GridResult grid=GridMapper.Map(scan); LevelRules rules=ExampleScans.CreateDemoRules();
        GridPoint[] starts={new(0,2),new(19,16),new(0,0)};
        LevelResult level=LevelBuilder.Build(grid,rules,starts);
        Check(level.Status==ResultStatus.Success && level.SpawnRoutes.Count==3 &&
            level.SpawnRoutes[0].TargetCoreBrickId!=level.SpawnRoutes[1].TargetCoreBrickId,"entrances did not choose their own nearest core");
        var sim=new BattleSimulation(level,new WavePlan(2,5,10,1,2,0.1),new CombatConfig(100,1,0.1,1));
        sim.StartWave(); sim.Step(0.45);
        int[] counts=new int[3];
        foreach(EnemyState enemy in sim.State.Enemies)
        {
            counts[enemy.SpawnIndex]++;
            IReadOnlyList<GridPoint> route=level.SpawnRoutes[enemy.SpawnIndex].Route;
            int index=(int)enemy.RouteProgress; double fraction=enemy.RouteProgress-index;
            double x=route[index].X+(route[index+1].X-route[index].X)*fraction;
            double y=route[index].Y+(route[index+1].Y-route[index].Y)*fraction;
            Check(Math.Abs(x-enemy.X)<1e-8 && Math.Abs(y-enemy.Y)<1e-8,"enemy followed another entrance's route");
        }
        Check(sim.State.SpawnedThisWave==5 && counts.SequenceEqual(new[] {2,2,1}),"total count was multiplied or unevenly assigned");
        FinishWave(sim); Check(sim.State.Leaked==5 && sim.State.CoreHealth==90,"arrival on separate routes did not settle against shared health");
        sim.StartWave(); Check(sim.State.Enemies[0].SpawnIndex==2,"next wave restarted entrance rotation");
        FinishWave(sim); Check(sim.State.Phase==BattlePhase.Victory && sim.State.Leaked==10 && sim.State.CoreHealth==80,"multi-route run did not end correctly");

        LevelResult duplicate=LevelBuilder.Build(grid,rules,new[] {starts[0],starts[0]});
        Check(duplicate.Issues.Any(i=>i.Code=="SPAWN_DUPLICATE"),"duplicate entrance accepted");
        LevelResult mixed=LevelBuilder.Build(grid,rules,new[] {starts[0],new GridPoint(2,2)});
        Check(mixed.Status==ResultStatus.Rejected && mixed.SpawnRoutes[0].HasRoute && !mixed.SpawnRoutes[1].HasRoute &&
            BattleSimulation.Validate(mixed,WavePlan.Trial,CombatConfig.Trial).Length>0,"valid primary hid invalid secondary entrance");
        var forged=new LevelResult(ResultStatus.Success,level.Entities,level.Cells,level.CoreBrickIds,new[] {
            level.SpawnRoutes[0],new SpawnRoute(starts[1],level.SpawnRoutes[1].TargetCoreBrickId,new[] {starts[1],new GridPoint(16,16)})},Array.Empty<Issue>());
        Check(BattleSimulation.Validate(forged,WavePlan.Trial,CombatConfig.Trial).Any(i=>i.Code=="BATTLE_ROUTE_INVALID"),"secondary route skipped validation");
        GridPoint[] raw={new(0,2),new(1,2),new(2,2)};
        var frozen=new SpawnRoute(raw[0],"core",raw); raw[0]=new GridPoint(19,19);
        Check(frozen.Route[0].Equals(new GridPoint(0,2)),"route contract retained mutable input");
        Console.WriteLine("MULTIPLE_SPAWN_CHECKS_PASSED: toggle/zero/occupied/no-route, independent cores and routes, total round-robin counts across waves, shared health, lock/snapshot/retry and secondary route validation.");
    }
    private static void FinishWave(BattleSimulation sim)
    {
        for(int i=0;i<1000 && !sim.State.Finished && !sim.State.CanStartWave;i++) sim.Step(0.1);
        Check(sim.State.Finished || sim.State.CanStartWave,"wave did not finish");
    }
}
