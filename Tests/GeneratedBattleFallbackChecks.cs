using Brick2Life.Application;
using Brick2Life.Battle;
using Brick2Life.Core;

internal static class GeneratedBattleFallbackChecks
{
    private static readonly GridPoint[] TwoEntrances = { new GridPoint(0, 0), new GridPoint(0, 9) };

    private static BattleLevel Map(GridPoint[] entrances, params (int x, int y, string color)[] bricks)
    {
        var definition = new BoardDefinition(colorIds: new[] { "blue", "red", "green" });
        var cells = new BoardCell[definition.CellCount];
        Array.Fill(cells, BoardCell.Empty);
        foreach (var brick in bricks) cells[definition.Index(brick.x, brick.y)] = new BoardCell(Occupancy.Occupied, brick.color);
        var rules = LevelRuleSet.Create(new KeyValuePair<string, GameRole>("blue", GameRole.Core),
            new KeyValuePair<string, GameRole>("red", GameRole.Tower),
            new KeyValuePair<string, GameRole>("green", GameRole.Wall));
        return BattleLevel.Build(LevelState.FromSnapshot(new BoardSnapshot(definition, 1, cells, SnapshotSource.Vision), rules), entrances);
    }

    public static void Run()
    {
        int assertions = 0;
        void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
        bool Near(double a, double b) => Math.Abs(a - b) < 1e-8;
        void UnchangedRun(BattleState before, BattleState after, string context)
        {
            Check(before.Phase == after.Phase && before.Wave == after.Wave && before.CoreHealth == after.CoreHealth &&
                before.SpawnedThisWave == after.SpawnedThisWave && before.Killed == after.Killed && before.Leaked == after.Leaked &&
                Near(before.Elapsed, after.Elapsed) && before.Paused == after.Paused, context + ": run counters changed.");
            Check(before.Enemies.Count == after.Enemies.Count, context + ": enemies were recreated.");
            for (int i = 0; i < before.Enemies.Count; i++)
            {
                EnemyState a = before.Enemies[i], b = after.Enemies[i];
                Check(a.Id == b.Id && a.Health == b.Health && a.SpawnIndex == b.SpawnIndex &&
                    Near(a.X, b.X) && Near(a.Y, b.Y) && Near(a.RouteProgress, b.RouteProgress), context + ": enemy jumped or reset.");
            }
        }
        BattleSimulation NewRun(BattleLevel level) => new BattleSimulation(level,
            new WavePlan(2, 4, 9999, 1, 10, 0.2), new TowerSettings(100, 1, 0.1, 0.5), keepLastValidRoutes: true);

        BattleLevel baseline = Map(TwoEntrances, (9, 4, "blue"));
        Check(baseline.IsValid && baseline.Entrances.Count == 2 && baseline.Routes.Count == 2,
            "Baseline requires two valid entrances.");
        var run = NewRun(baseline);
        Check(ReferenceEquals(run.CurrentLevel, baseline) && run.ActiveRoutes.Count == 2, "Initial map/routes are unavailable.");
        run.StartWave(); run.Step(0.1);
        BattleState before = run.State;
        var firstBlocked = Map(TwoEntrances, (9, 4, "blue"), (0, 0, "green"));
        Check(!firstBlocked.IsValid && firstBlocked.Routes.Count == 1 && firstBlocked.Entrances.Count == 2,
            "Blocked entrance must remain in the full entrance list.");
        Check(firstBlocked.Entrances[0].Equals(new GridPoint(0, 0)) && firstBlocked.Entrances[1].Equals(new GridPoint(0, 18)) &&
            firstBlocked.Routes[0].Spawn.Equals(new GridPoint(0, 18)), "Entrance fixture does not expose route compression.");
        Check(run.TryUpdateLevel(firstBlocked, out string warning), "Blocked route update was not accepted in fallback mode.");
        Check(warning.Length > 0 && run.State.UsingPreviousRoutes && run.State.RouteWarning.Length > 0 &&
            !run.State.WaitingForLayout, "Fallback did not report a non-blocking warning.");
        Check(ReferenceEquals(run.CurrentLevel, firstBlocked) && run.CurrentLevel.CellAt(0, 0).Blocked,
            "Fallback rolled back the newly confirmed wall.");
        Check(run.ActiveRoutes.Count == 2 && run.ActiveRoutes[0].Spawn.Equals(new GridPoint(0, 0)) &&
            run.ActiveRoutes[1].Spawn.Equals(new GridPoint(0, 18)), "Fallback compressed or swapped entrance routes.");
        UnchangedRun(before, run.State, "First entrance blocked");
        run.Step(0.75);
        Check(run.State.Elapsed > before.Elapsed && run.State.SpawnedThisWave == 4 && run.State.Enemies.Count == 4,
            "Fallback paused movement or spawning.");
        for (int i = 0; i < run.State.Enemies.Count; i++)
        {
            EnemyState enemy = run.State.Enemies[i];
            Check(enemy.Id == i + 1 && enemy.SpawnIndex == i % 2, "Fallback changed round-robin entrance identity.");
            Check(enemy.SpawnIndex == 0 ? enemy.Y < 2 : enemy.Y > 16,
                "New enemy followed the remaining candidate route of a different entrance.");
        }
        before = run.State;
        var restored = Map(TwoEntrances, (9, 4, "blue"));
        Check(run.TryUpdateLevel(restored, out warning) && warning.Length == 0 && !run.State.UsingPreviousRoutes &&
            !run.State.WaitingForLayout, "A reachable confirmed map did not clear the route warning.");
        UnchangedRun(before, run.State, "Recovered routes");
        Check(ReferenceEquals(run.CurrentLevel, restored) && run.ActiveRoutes.Count == 2, "Recovered map is not current.");

        var allBlocked = Map(TwoEntrances, (9, 4, "blue"), (0, 0, "green"), (0, 9, "green"));
        Check(allBlocked.Routes.Count == 0 && allBlocked.Entrances.Count == 2, "All-blocked fixture is malformed.");
        Check(run.TryUpdateLevel(allBlocked, out _) && run.State.UsingPreviousRoutes && run.ActiveRoutes.Count == 2,
            "Zero candidate routes lost the complete valid path group.");
        run.SetPaused(true);
        before = run.State;
        run.Step(1);
        UnchangedRun(before, run.State, "User-paused fallback");
        Check(run.TryUpdateLevel(restored, out _) && run.State.Paused && !run.State.UsingPreviousRoutes,
            "Path recovery overrode the user's pause.");
        run.SetPaused(false); run.Step(0.1);
        Check(run.State.Elapsed > before.Elapsed, "Recovered paths did not resume after user unpaused.");

        var sealedCore = Map(TwoEntrances, (9, 4, "blue"), (8, 4, "green"), (9, 3, "green"), (9, 5, "green"));
        bool onlyNoRoute = sealedCore.Issues.Count == 2;
        foreach (BattleIssue issue in sealedCore.Issues) onlyNoRoute &= issue.Code == "NO_ROUTE";
        Check(!sealedCore.IsValid && onlyNoRoute && sealedCore.Routes.Count == 0,
            "Sealed-core fixture must block routes without occupying any entrance.");
        before = run.State;
        Check(run.TryUpdateLevel(sealedCore, out _) && run.State.UsingPreviousRoutes && !run.State.WaitingForLayout,
            "Walls sealing the core stopped combat instead of retaining valid paths.");
        UnchangedRun(before, run.State, "Core enclosed by walls");
        run.Step(0.1);
        Check(run.State.Elapsed > before.Elapsed, "Sealed-core fallback stopped the combat clock.");

        var single = new[] { new GridPoint(0, 0) };
        var overlap = new BattleSimulation(Map(single, (9, 9, "blue")), new WavePlan(1, 1, 9999, 1, 10, 1),
            new TowerSettings(100, 1, 0.1, 1), keepLastValidRoutes: true);
        overlap.StartWave(); overlap.Step(2.4);
        before = overlap.State;
        EnemyState overlapped = before.Enemies[0];
        var wallUnderEnemy = Map(single, (9, 9, "blue"),
            ((int)Math.Floor(overlapped.X / 2), (int)Math.Floor(overlapped.Y / 2), "green"));
        Check(wallUnderEnemy.IsValid, "Overlap fixture should preserve a valid entrance route.");
        Check(overlap.TryUpdateLevel(wallUnderEnemy, out _) && overlap.State.UsingPreviousRoutes &&
            !overlap.State.WaitingForLayout, "An obstacle under a live enemy incorrectly stopped combat.");
        UnchangedRun(before, overlap.State, "Wall under enemy");
        overlap.Step(0.2);
        EnemyState moved = overlap.State.Enemies[0];
        Check(overlap.State.Elapsed > before.Elapsed &&
            Math.Abs(moved.X - overlapped.X) + Math.Abs(moved.Y - overlapped.Y) <= 0.200001,
            "Enemy did not continue its old route smoothly through the new obstacle.");

        // A route failure must not keep a deleted tower alive or prevent a new tower from firing.
        var shooting = new BattleSimulation(Map(single, (9, 9, "blue"), (1, 1, "red")),
            new WavePlan(1, 1, 9999, 1, 10, 1), new TowerSettings(100, 1, 40, 0.2), keepLastValidRoutes: true);
        shooting.StartWave();
        Check(shooting.Step(0.05).Count > 0, "Tower fixture did not fire.");
        Check(shooting.TryUpdateLevel(Map(single, (9, 9, "blue"), (0, 0, "green"), (1, 1, "red")), out _) &&
            shooting.Step(0.1).Count == 0, "Fallback reset an existing tower cooldown.");
        Check(shooting.TryUpdateLevel(Map(single, (9, 9, "blue"), (0, 0, "green")), out _), "Removing tower under fallback failed.");
        int health = shooting.State.Enemies[0].Health;
        Check(shooting.Step(0.4).Count == 0 && shooting.State.Enemies[0].Health == health,
            "Removed tower continued attacking on a blocked map.");
        var newTower = Map(single, (9, 9, "blue"), (0, 0, "green"), (3, 3, "red"));
        Check(shooting.TryUpdateLevel(newTower, out _) && ReferenceEquals(shooting.CurrentLevel, newTower),
            "New tower map was not accepted while using old paths.");
        Check(shooting.Step(0.1).Count == 0, "A newly placed tower ignored its attack interval.");
        Check(shooting.Step(0.2).Count > 0 && shooting.State.Enemies[0].Health < health,
            "New tower did not attack while route fallback was active.");

        var interval = new BattleSimulation(Map(single, (0, 1, "blue")), new WavePlan(2, 1, 9999, 20, 10, 1),
            new TowerSettings(100, 1, 0.1, 1), keepLastValidRoutes: true);
        interval.StartWave(); interval.Step(0.2);
        Check(interval.State.CanStartWave && interval.State.CoreHealth == 90, "Wave interval fixture failed.");
        Check(interval.TryUpdateLevel(Map(single, (0, 1, "blue"), (0, 0, "green")), out _) && interval.State.CanStartWave,
            "Fallback incorrectly prevented the next wave.");
        Check(interval.StartWave() && interval.State.Wave == 2 && interval.State.CoreHealth == 90,
            "Fallback next wave reset progress.");
        interval.Step(0.2);
        Check(interval.State.Finished && interval.State.Leaked == 2 && interval.State.CoreHealth == 80,
            "Enemies failed to finish an old path across waves.");

        var protectedRun = NewRun(baseline);
        protectedRun.StartWave(); protectedRun.Step(0.1);
        Check(!protectedRun.TryUpdateLevel(Map(new[] { TwoEntrances[1], TwoEntrances[0] }, (9, 4, "blue")), out _) &&
            protectedRun.State.WaitingForLayout && !protectedRun.State.UsingPreviousRoutes,
            "Fallback silently swapped fixed entrance indices.");
        Check(ReferenceEquals(protectedRun.CurrentLevel, baseline), "Rejected entrance change became current.");
        Check(protectedRun.TryUpdateLevel(baseline, out _), "Original entrance list did not recover.");
        Check(!protectedRun.TryUpdateLevel(Map(TwoEntrances, (8, 4, "blue")), out _) && protectedRun.State.WaitingForLayout,
            "Fallback silently moved the fixed core.");
        Check(protectedRun.TryUpdateLevel(baseline, out _), "Restoring the fixed core did not recover.");
        Check(!protectedRun.TryUpdateLevel(Map(TwoEntrances), out _) && protectedRun.State.WaitingForLayout,
            "Removing the fixed core was treated as an ordinary path failure.");
        Check(protectedRun.TryUpdateLevel(baseline, out _), "Missing-core rejection did not recover.");
        Check(!protectedRun.TryUpdateLevel(null, out _) && protectedRun.State.WaitingForLayout,
            "Missing confirmed input must not be silently accepted.");

        var legacy = new BattleSimulation(baseline, new WavePlan(1, 1, 9999, 1, 10, 1), new TowerSettings(100, 1, 0.1, 1));
        legacy.StartWave();
        Check(!legacy.TryUpdateLevel(firstBlocked, out _) && legacy.State.WaitingForLayout && !legacy.State.UsingPreviousRoutes,
            "Default legacy policy unexpectedly changed.");
        Console.WriteLine($"GENERATED_BATTLE_FALLBACK_CHECKS_PASSED: {assertions} assertions.");
    }
}
