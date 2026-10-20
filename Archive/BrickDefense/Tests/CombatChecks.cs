using BrickDefense.Core;
using BrickDefense.Workflow;

public static class CombatChecks
{
    private static void Check(bool condition,string message)
    { if(!condition) throw new Exception("Combat: "+message); }
    private static LevelResult Level(params string[] rows) => LevelBuilder.Build(
        GridMapper.Map(ExampleScans.Parse("combat-test",rows)),ExampleScans.CreateDemoRules(),new GridPoint(1,18));
    private static LevelResult Simple(bool tower=false) => Level(
        "..........","..........","..........","..........","..........",
        "..........","..........","..........",tower?"T.........":"..........",".K........");
    private static void FinishWave(BattleSimulation simulation)
    {
        for(int i=0;i<5000 && !simulation.State.Finished && !simulation.State.CanStartWave;i++) simulation.Step(0.1);
        Check(simulation.State.Finished || simulation.State.CanStartWave,"wave did not end");
    }
    public static void Run()
    {
        LevelResult simple=Simple();
        Check(simple.Status==ResultStatus.Success,"fixture must have a valid route");
        var noTower=new BattleSimulation(simple,new WavePlan(2,2,10,20,7,1),new CombatConfig(100,10,8,0.2));
        Check(noTower.State.Phase==BattlePhase.Ready && noTower.State.Wave==0,"must wait for first click");
        noTower.Step(2); Check(noTower.State.Enemies.Count==0,"ready state spawned automatically");
        Check(noTower.StartWave() && !noTower.StartWave(),"double start must be rejected");
        noTower.Step(0.25);
        Check(noTower.State.Leaked==1 && noTower.State.CoreHealth==93 && noTower.State.SpawnedThisWave==1 &&
            noTower.State.Phase==BattlePhase.Spawning,"gap between spawns prematurely ended wave");
        FinishWave(noTower); Check(noTower.State.Phase==BattlePhase.AwaitingNextWave,"next wave must wait");
        noTower.Step(5); Check(noTower.State.Wave==1 && noTower.State.SpawnedThisWave==2,"next wave auto-started");
        Check(noTower.StartWave(),"next click failed"); FinishWave(noTower);
        Check(noTower.State.Phase==BattlePhase.Victory && noTower.State.Leaked==4 && noTower.State.CoreHealth==72,
            "all leaking enemies should still allow victory if core survives");
        Check(!noTower.StartWave(),"victory restarted a wave");

        var defeat=new BattleSimulation(simple,new WavePlan(4,10,10,20,500,0.1),CombatConfig.Trial);
        defeat.StartWave(); defeat.Step(5);
        Check(defeat.State.Phase==BattlePhase.Defeat && defeat.State.CoreHealth==0 && defeat.State.Leaked==1,
            "defeat must happen once and immediately");
        int terminalSpawned=defeat.State.SpawnedThisWave;
        defeat.Step(5); Check(defeat.State.Leaked==1 && defeat.State.SpawnedThisWave==terminalSpawned,"defeat continued combat");

        LevelResult twoCores=Level("..........","..........","..........","..........","..........",
            "..........","..........","..........","..........",".K.....K..");
        var shared=new BattleSimulation(twoCores,new WavePlan(1,2,10,20,9,0.1),new CombatConfig(30,10,8,0.2));
        shared.StartWave(); FinishWave(shared);
        Check(twoCores.CoreBrickIds.Length==2 && shared.State.CoreHealth==12,"multiple cores must share health");

        LevelResult towers=Level("..........","..........","..........","..........","..........",
            "..........","..........","..........","T.T.......",".K........");
        var kill=new BattleSimulation(towers,new WavePlan(1,1,10,20,100,1),new CombatConfig(100,10,8,0.2));
        kill.StartWave(); IReadOnlyList<ShotEvent> shots=kill.Step(0.25);
        Check(kill.State.Phase==BattlePhase.Victory && kill.State.Killed==1 && kill.State.Leaked==0 &&
            kill.State.CoreHealth==100 && shots.Count==1 && shots[0].Killed,"multi-tower kill must settle once and never leak");

        var cooldown=new BattleSimulation(Simple(true),new WavePlan(1,4,1000,0.1,1,0.1),new CombatConfig(100,3,8,0.5));
        cooldown.StartWave(); IReadOnlyList<ShotEvent> cooldownShots=cooldown.Step(1);
        Check(cooldownShots.Count==2 && cooldownShots[0].EnemyId==1 && cooldownShots[1].EnemyId==4,
            "cooldown or nearest-target selection failed: newer enemies are closer to this tower");
        Check(cooldown.State.Enemies[0].Health==997 && cooldown.State.Enemies[3].Health==997,"shots damaged the wrong enemy");
        var outOfRange=new BattleSimulation(Simple(true),new WavePlan(1,1,100,0.1,1,1),new CombatConfig(100,10,0.1,0.05));
        outOfRange.StartWave(); Check(outOfRange.Step(1).Count==0,"out-of-range tower fired");

        LevelResult reference=LevelBuilder.Build(GridMapper.Map(ExampleScans.CreateReference()),ExampleScans.CreateDemoRules(),new GridPoint(1,18));
        var movement=new BattleSimulation(reference,new WavePlan(1,1,1000,20,1,1),new CombatConfig(100,1,0.1,1));
        movement.StartWave(); movement.Step(0.2);
        Check(Math.Abs(movement.State.Enemies[0].RouteProgress-4)<1e-8 &&
            Math.Abs(movement.State.Enemies[0].X-reference.Route[4].X)<1e-8 &&
            Math.Abs(movement.State.Enemies[0].Y-reference.Route[4].Y)<1e-8,"movement skipped/cut route corners");
        var framesA=new BattleSimulation(reference,WavePlan.Trial,CombatConfig.Trial);
        var framesB=new BattleSimulation(reference,WavePlan.Trial,CombatConfig.Trial);
        framesA.StartWave(); framesB.StartWave();
        for(int i=0;i<300;i++) framesA.Step(1.0/60);
        for(int i=0;i<50;i++) framesB.Step(0.1);
        Check(framesA.State.Killed==framesB.State.Killed && framesA.State.Leaked==framesB.State.Leaked &&
            framesA.State.CoreHealth==framesB.State.CoreHealth && framesA.State.Enemies.Count==framesB.State.Enemies.Count,
            "combat depends on render frame size");
        for(int i=0;i<framesA.State.Enemies.Count;i++)
            Check(Math.Abs(framesA.State.Enemies[i].RouteProgress-framesB.State.Enemies[i].RouteProgress)<1e-8 &&
                framesA.State.Enemies[i].Health==framesB.State.Enemies[i].Health,"enemy frame-size state differs");
        var trial=new BattleSimulation(reference,WavePlan.Trial,CombatConfig.Trial);
        for(int i=0;i<WavePlan.Trial.WaveCount;i++) { Check(trial.StartWave(),"trial next wave failed"); FinishWave(trial); }
        Check(trial.State.Phase==BattlePhase.Victory,"reference default trial should be winnable");

        WavePlan[] invalid={new WavePlan(0,1,1,1,1,1),new WavePlan(1,0,1,1,1,1),new WavePlan(1,1,0,1,1,1),
            new WavePlan(1,1,1,double.NaN,1,1),new WavePlan(1,1,1,double.PositiveInfinity,1,1),
            new WavePlan(1,1,1,0,1,1),new WavePlan(1,1,1,1,0,1),new WavePlan(1,1,1,1,1,-1),
            new WavePlan(1,1,1,1,1,double.NaN),new WavePlan(20,50,1,1,1,1),new WavePlan(int.MaxValue,int.MaxValue,1,1,1,1)};
        foreach(WavePlan plan in invalid) Check(BattleSimulation.Validate(simple,plan,CombatConfig.Trial).Length>0,"invalid plan was accepted");
        Check(BattleSimulation.Validate(simple,null,CombatConfig.Trial).Length>0 &&
            BattleSimulation.Validate(simple,WavePlan.Trial,new CombatConfig(1,1,double.NaN,1)).Length>0,"invalid config accepted");
        LevelResult blocked=LevelBuilder.Build(GridMapper.Map(ExampleScans.CreateBlockedCore()),ExampleScans.CreateDemoRules(),new GridPoint(1,18));
        Check(BattleSimulation.Validate(blocked,WavePlan.Trial,CombatConfig.Trial).Length>0,"unreachable level started");

        var levels=new LevelSession(); levels.LoadExample(false);
        using var session=new BattleSession(levels);
        var original=levels.Current;
        Check(session.TryStart(WavePlan.Trial,CombatConfig.Trial,out _) && levels.IsLocked,"start must lock layout");
        levels.LoadExample(true);
        Check(ReferenceEquals(original,levels.Current) && !levels.SelectSpawn(new GridPoint(9,9)) &&
            !levels.TryLoad(original.Definition),"map lock bypassed through workflow");
        Check(!session.TryStart(WavePlan.Trial,CombatConfig.Trial,out _),"started another run mid-wave");
        GameState snapshot=session.State; session.SetPaused(true); session.Advance(2);
        Check(ReferenceEquals(snapshot,session.State),"paused battle advanced");
        session.SetPaused(false);
        for(int i=0;i<1000 && !session.State.Finished;i++)
        { if(session.State.CanStartWave) session.StartNextWave(); session.Advance(0.1); }
        Check(session.State.Finished && !levels.IsLocked,"terminal run must unlock layout");
        int oldRun=session.RunId;
        Check(session.Retry(out _) && levels.IsLocked && session.RunId==oldRun+1 && session.State.Wave==1 &&
            session.State.CoreHealth==100 && session.State.Killed==0 && session.State.Leaked==0 &&
            session.State.Enemies.Count==1,"retry did not reset run");
        session.ReturnToLayout(); Check(session.State==null && !levels.IsLocked && levels.SelectSpawn(new GridPoint(9,9)),"return did not unlock map");
        Check(!session.TryStart(invalid[0],CombatConfig.Trial,out _) && !levels.IsLocked,"invalid start changed lock");
        Console.WriteLine($"COMBAT_CHECKS_PASSED: movement, damage, cooldown/range, shared core, manual waves, terminal state, frame sizes, limits, lock/pause/retry. Trial: cleared {trial.State.Killed}, leaked {trial.State.Leaked}, core {trial.State.CoreHealth}.");
    }
}
