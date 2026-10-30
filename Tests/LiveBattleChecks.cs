using Brick2Life.Application;
using Brick2Life.Battle;
using Brick2Life.Core;

internal static class LiveBattleChecks
{
    private static BattleLevel Map(params (int x, int y, string color)[] bricks)
    {
        var definition = new BoardDefinition(colorIds: new[] { "blue", "red", "green" });
        var cells = new BoardCell[definition.CellCount];
        Array.Fill(cells, BoardCell.Empty);
        foreach (var b in bricks) cells[definition.Index(b.x, b.y)] = new BoardCell(Occupancy.Occupied, b.color);
        var rules = LevelRuleSet.Create(new KeyValuePair<string, GameRole>("blue", GameRole.Core),
            new KeyValuePair<string, GameRole>("red", GameRole.Tower), new KeyValuePair<string, GameRole>("green", GameRole.Wall));
        return BattleLevel.Build(LevelState.FromSnapshot(new BoardSnapshot(definition, 1, cells, SnapshotSource.Vision), rules),
            new[] { new GridPoint(0, 0) });
    }

    public static void Run()
    {
        int assertions = 0;
        void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
        bool Near(double a, double b) => Math.Abs(a - b) < 1e-8;
        BattleSimulation Run(BattleLevel map) => new BattleSimulation(map,
            new WavePlan(2, 1, 9999, 1, 10, 0.5), new TowerSettings(100, 1, 0.1, 0.5));

        var simulation = Run(Map((9, 9, "blue")));
        Check(simulation.StartWave(), "Live run did not start.");
        simulation.Step(0.37);
        BattleState before = simulation.State;
        EnemyState enemy = before.Enemies[0];
        var changed = Map((9, 9, "blue"), (2, 0, "green"), (5, 5, "red"));
        Check(simulation.TryUpdateLevel(changed, out _), "A reachable new wall was rejected.");
        EnemyState after = simulation.State.Enemies[0];
        Check(enemy.Id == after.Id && enemy.Health == after.Health && Near(enemy.X, after.X) && Near(enemy.Y, after.Y),
            "Rerouting teleported or recreated an enemy.");
        Check(before.Wave == simulation.State.Wave && before.CoreHealth == simulation.State.CoreHealth &&
            before.SpawnedThisWave == simulation.State.SpawnedThisWave && Near(before.Elapsed, simulation.State.Elapsed),
            "Rerouting reset wave, health, spawn count or clock.");
        for (int i = 0; i < 80; i++)
        {
            EnemyState previous = simulation.State.Enemies[0];
            simulation.Step(0.1);
            EnemyState current = simulation.State.Enemies[0];
            Check(!changed.CellAt((int)Math.Floor(current.X), (int)Math.Floor(current.Y)).Blocked, "Enemy crossed a new obstacle.");
            Check(Math.Abs(current.X - previous.X) + Math.Abs(current.Y - previous.Y) <= 0.100001,
                "Movement jumped when following a replacement route.");
        }
        Check(simulation.TryUpdateLevel(Map((2, 0, "blue")), out _), "Moving the core was rejected.");
        for (int i = 0; i < 400 && !simulation.State.CanStartWave; i++) simulation.Step(0.1);
        Check(simulation.State.Leaked == 1 && simulation.State.CoreHealth == 90 && simulation.State.Wave == 1,
            "Enemy did not reach the moved core without resetting the run.");
        Check(simulation.TryUpdateLevel(Map((9, 9, "blue")), out _) && simulation.State.CanStartWave,
            "Wave interval did not accept an updated layout.");
        Check(simulation.StartWave() && simulation.State.Wave == 2 && simulation.State.CoreHealth == 90,
            "Next wave reset the existing run after an edit.");

        var recovery = Run(Map((9, 9, "blue")));
        recovery.StartWave(); recovery.Step(2.4);
        BattleState frozen = recovery.State;
        EnemyState overlapped = frozen.Enemies[0];
        var underEnemy = Map((9, 9, "blue"), ((int)Math.Floor(overlapped.X / 2),
            (int)Math.Floor(overlapped.Y / 2), "green"));
        Check(underEnemy.IsValid, "Enemy-overlap fixture accidentally blocks the entrance.");
        Check(!recovery.TryUpdateLevel(underEnemy, out string overlapReason) && overlapReason.Length > 0,
            "Placing an obstacle under an enemy should wait for repair.");
        Check(recovery.State.WaitingForLayout && !recovery.State.Paused, "Layout wait was confused with user pause.");
        recovery.Step(5);
        Check(Near(frozen.Elapsed, recovery.State.Elapsed) && Near(frozen.Enemies[0].Y, recovery.State.Enemies[0].Y) &&
            frozen.CoreHealth == recovery.State.CoreHealth, "Invalid layout advanced combat.");
        recovery.SetPaused(true);
        Check(recovery.TryUpdateLevel(Map((8, 8, "blue")), out _) && !recovery.State.WaitingForLayout && recovery.State.Paused,
            "Repair lost the user's pause state.");
        recovery.Step(1);
        Check(Near(frozen.Elapsed, recovery.State.Elapsed), "Repair resumed an explicitly paused battle.");
        recovery.SetPaused(false); recovery.Step(0.1);
        Check(recovery.State.Elapsed > frozen.Elapsed, "Repaired run did not resume.");
        Check(!recovery.TryUpdateLevel(Map(), out _) && recovery.State.WaitingForLayout, "Removing the core did not suspend combat.");
        Check(!recovery.StartWave(), "A blocked layout started another wave.");
        Check(recovery.TryUpdateLevel(Map((8, 8, "blue")), out _), "Restoring the core did not recover.");
        Check(!recovery.TryUpdateLevel(Map((8, 8, "blue"), (0, 0, "red")), out _), "Occupied entrance was accepted.");
        Check(recovery.TryUpdateLevel(Map((8, 8, "blue")), out _), "Clearing the entrance did not recover.");
        Check(!recovery.TryUpdateLevel(Map((5, 5, "blue"), (4, 5, "green"), (6, 5, "green"),
            (5, 4, "green"), (5, 6, "green")), out _), "A sealed core did not suspend combat.");

        var shooting = new BattleSimulation(Map((9, 9, "blue"), (1, 1, "red")),
            new WavePlan(1, 1, 9999, 1, 10, 0.5), new TowerSettings(100, 1, 40, 0.5));
        shooting.StartWave();
        Check(shooting.Step(0.1).Count > 0, "Tower fixture did not shoot.");
        Check(shooting.TryUpdateLevel(Map((9, 9, "blue"), (1, 1, "red"), (8, 5, "green")), out _),
            "Unrelated edit was rejected.");
        Check(shooting.Step(0.2).Count == 0, "Unrelated edit reset an existing tower's cooldown.");
        Check(shooting.TryUpdateLevel(Map((9, 9, "blue")), out _), "Removing a tower was rejected.");
        int health = shooting.State.Enemies[0].Health;
        Check(shooting.Step(0.6).Count == 0 && shooting.State.Enemies[0].Health == health,
            "Removed tower kept damaging enemies.");
        Check(shooting.TryUpdateLevel(Map((9, 9, "blue"), (2, 2, "red")), out _), "Adding a tower was rejected.");
        Check(shooting.Step(0.6).Count > 0 && shooting.State.Enemies[0].Health < health, "New tower did not join combat.");
        Console.WriteLine($"LIVE_BATTLE_CHECKS_PASSED: {assertions} assertions.");
    }
}
