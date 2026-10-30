using Brick2Life.Application;
using Brick2Life.Battle;
using Brick2Life.Core;

internal static class BattleChecks
{
    private static BoardDefinition Definition => new BoardDefinition(colorIds: new[] { "red", "green", "blue" });
    private static LevelRuleSet Rules => LevelRuleSet.Create(
        new KeyValuePair<string, GameRole>("red", GameRole.Tower),
        new KeyValuePair<string, GameRole>("green", GameRole.Wall),
        new KeyValuePair<string, GameRole>("blue", GameRole.Core));

    private static BoardSnapshot Snapshot(params (int x, int y, string color)[] bricks)
    {
        var definition = Definition;
        var cells = new BoardCell[definition.CellCount];
        for (int i = 0; i < cells.Length; i++) cells[i] = BoardCell.Empty;
        foreach (var brick in bricks) cells[definition.Index(brick.x, brick.y)] = new BoardCell(Occupancy.Occupied, brick.color);
        return new BoardSnapshot(definition, 1, cells, SnapshotSource.Vision);
    }

    private static LevelState Level(params (int x, int y, string color)[] bricks) =>
        LevelState.FromSnapshot(Snapshot(bricks), Rules);

    private static void ExpectIssue(BattleLevel level, string code, string why, ref int assertions)
    {
        assertions++;
        bool found = false;
        foreach (BattleIssue issue in level.Issues) if (issue.Code == code) { found = true; break; }
        if (!found) throw new Exception(why + "（实际：" + (level.Issues.Count == 0 ? "无问题" : level.Issues[0].ToString()) + "）");
    }

    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }

        // 一条从 (0,0) 到核心 (9,9) 的空棋盘通道，中间放一座塔。
        var level = Level((9, 9, "blue"), (4, 4, "red"));
        Check(level.IsPlayable, "Corridor level must be playable.");

        var built = BattleLevel.Build(level, new[] { new GridPoint(0, 0) });
        Check(built.IsValid, "A single empty entrance must produce a valid battle level. issues=" + string.Join(" | ", built.Issues) + " routes=" + built.Routes.Count + " cores=" + built.CoreBrickIds.Count);
        Check(built.Width == 20 && built.Height == 20, "Battle grid must be 20x20.");
        Check(built.CoreBrickIds.Count == 1, "One core brick expected.");
        Check(built.Routes.Count == 1 && built.Routes[0].Route.Count > 1, "A route is expected.");
        var route = built.Routes[0].Route;
        Check(route[0].Equals(new GridPoint(0, 0)), "Route must start at the entrance nav cell.");
        Check(route[route.Count - 1].Equals(new GridPoint(18, 18)), "Route must end on the core cell.");
        for (int i = 1; i < route.Count; i++)
        {
            int step = Math.Abs(route[i].X - route[i - 1].X) + Math.Abs(route[i].Y - route[i - 1].Y);
            Check(step == 1, "Route steps must be four-way adjacent.");
            BattleCell cell = built.CellAt(route[i].X, route[i].Y);
            Check(!cell.Blocked, "Route must not pass through a wall or tower.");
        }

        ExpectIssue(BattleLevel.Build(level, new[] { new GridPoint(9, 9) }), "SPAWN_OCCUPIED", "Occupied entrance must be rejected", ref assertions);
        ExpectIssue(BattleLevel.Build(level, new[] { new GridPoint(10, 0) }), "SPAWN_OUT_OF_BOUNDS", "Out-of-bounds entrance must be rejected", ref assertions);
        ExpectIssue(BattleLevel.Build(level, new[] { new GridPoint(0, 0), new GridPoint(0, 0) }), "SPAWN_DUPLICATE", "Duplicate entrance must be rejected", ref assertions);
        ExpectIssue(BattleLevel.Build(level, Array.Empty<GridPoint>()), "NO_SPAWNS", "Missing entrance must be rejected", ref assertions);

        var sealedCore = Level((5, 5, "blue"), (4, 5, "green"), (6, 5, "green"), (5, 4, "green"), (5, 6, "green"));
        ExpectIssue(BattleLevel.Build(sealedCore, new[] { new GridPoint(0, 0) }), "NO_ROUTE", "A walled-in core must have no route", ref assertions);

        // 等距双核心：rank 更靠前的核心必须稳定胜出（直接测试寻路的 tie-break）。
        var tieGrid = new BattleCell[20 * 20];
        for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
                tieGrid[y * 20 + x] = new BattleCell(new GridPoint(x, y), GameRole.None, null);
        tieGrid[0 * 20 + 10] = new BattleCell(new GridPoint(10, 0), GameRole.Core, "a");
        tieGrid[10 * 20 + 0] = new BattleCell(new GridPoint(0, 10), GameRole.Core, "b");
        var tieRoute = RouteSearch.Find(tieGrid, 20, 20, new[] { "a", "b" }, new GridPoint(10, 10));
        Check(tieRoute != null && tieGrid[tieRoute[tieRoute.Length - 1].Y * 20 + tieRoute[tieRoute.Length - 1].X].SourceBrickId == "a",
            "An equal-distance tie must pick the earlier core.");
        var reversedTie = RouteSearch.Find(tieGrid, 20, 20, new[] { "b", "a" }, new GridPoint(10, 10));
        Check(reversedTie != null && tieGrid[reversedTie[reversedTie.Length - 1].Y * 20 + reversedTie[reversedTie.Length - 1].X].SourceBrickId == "b",
            "Reordering the core priority must change the chosen core.");

        var plan = new WavePlan(2, 3, 5, 4, 10, 0.4);
        var towers = new TowerSettings(100, 6, 8, 0.5);
        Check(BattleValidation.Check(built, plan, towers).Count == 0, "Trial parameters must validate.");
        int invalid = 0;
        void Invalid(WavePlan p, TowerSettings t) { invalid++; if (BattleValidation.Check(built, p, t).Count == 0) throw new Exception("Invalid parameters were accepted."); }
        Invalid(new WavePlan(0, 3, 5, 4, 10, 0.4), towers);
        Invalid(new WavePlan(21, 3, 5, 4, 10, 0.4), towers);
        Invalid(new WavePlan(2, 0, 5, 4, 10, 0.4), towers);
        Invalid(new WavePlan(2, 51, 5, 4, 10, 0.4), towers);
        Invalid(new WavePlan(20, 50, 5, 4, 10, 0.4), towers);
        Invalid(new WavePlan(2, 3, 0, 4, 10, 0.4), towers);
        Invalid(new WavePlan(2, 3, 5, 0, 10, 0.4), towers);
        Invalid(new WavePlan(2, 3, 5, 4, 0, 0.4), towers);
        Invalid(new WavePlan(2, 3, 5, 4, 10, 0), towers);
        Invalid(plan, new TowerSettings(0, 6, 8, 0.5));
        Invalid(plan, new TowerSettings(100, 0, 8, 0.5));
        Invalid(plan, new TowerSettings(100, 6, 0, 0.5));
        Invalid(plan, new TowerSettings(100, 6, 8, 0));
        Check(invalid == 13, "Every out-of-range parameter must be rejected.");

        // 多入口轮转：两个入口，三只敌人 → 0,1,0。
        var twoSpawns = BattleLevel.Build(level, new[] { new GridPoint(0, 0), new GridPoint(0, 9) });
        Check(twoSpawns.IsValid && twoSpawns.Routes.Count == 2, "Two entrances must both route.");
        var rotation = new BattleSimulation(twoSpawns, new WavePlan(1, 3, 9999, 1, 10, 0.1), new TowerSettings(100, 1, 0.1, 1));
        Check(rotation.StartWave(), "Wave must start.");
        for (int i = 0; i < 60; i++) rotation.Step(0.1);
        Check(rotation.State.SpawnedThisWave == 3, "Three enemies must spawn.");
        Check(rotation.State.Enemies[0].SpawnIndex == 0 && rotation.State.Enemies[1].SpawnIndex == 1 &&
            rotation.State.Enemies[2].SpawnIndex == 0, "Entrances must rotate round-robin.");

        // 塔击杀：生命 1，塔在路径旁 → 第一枪击杀。
        var kill = new BattleSimulation(built, new WavePlan(1, 1, 1, 2, 10, 0.5), new TowerSettings(100, 5, 20, 0.5));
        Check(kill.StartWave(), "Kill scenario must start.");
        bool killed = false;
        for (int i = 0; i < 300 && !killed; i++)
            foreach (ShotEvent shot in kill.Step(0.05)) if (shot.Killed) killed = true;
        Check(killed && kill.State.Killed == 1, "A one-health enemy must be killed by the tower.");
        Check(kill.State.Phase == BattlePhase.Victory, "Killing the only enemy of the last wave must win.");

        // 泄漏与失败：伤害 10、核心生命 10 → 一只敌人泄漏即失败。
        var leak = new BattleSimulation(built, new WavePlan(1, 1, 9999, 20, 10, 0.2), new TowerSettings(10, 1, 0.1, 1));
        leak.StartWave();
        for (int i = 0; i < 400 && !leak.State.Finished; i++) leak.Step(0.1);
        Check(leak.State.Leaked == 1 && leak.State.CoreHealth == 0, "A leaked enemy must damage the core.");
        Check(leak.State.Phase == BattlePhase.Defeat, "Zero core health must defeat the run.");

        // 暂停：暂停期间时间与位置都不推进；恢复后继续。
        var pause = new BattleSimulation(built, new WavePlan(1, 2, 9999, 2, 10, 0.5), new TowerSettings(100, 1, 0.1, 1));
        pause.StartWave();
        pause.Step(0.5);
        double elapsed = pause.State.Elapsed;
        pause.SetPaused(true);
        pause.Step(2.0);
        Check(Math.Abs(pause.State.Elapsed - elapsed) < 1e-9 && pause.State.Paused, "Pausing must freeze the clock.");
        pause.SetPaused(false);
        pause.Step(0.5);
        Check(pause.State.Elapsed > elapsed, "Resuming must restart the clock.");

        // 重试：重置回 Ready、核心满血、波次归零。
        pause.StartWave();
        pause.Step(1);
        pause.Restart();
        Check(pause.State.Phase == BattlePhase.Ready && pause.State.Wave == 0 && pause.State.Enemies.Count == 0,
            "Restart must return to the ready phase.");
        Check(pause.State.CoreHealth == 100 && pause.State.Killed == 0 && pause.State.Leaked == 0,
            "Restart must restore health and counters.");

        // 波次推进：两波、每波一只、塔全杀 → 第一波后等待下一波，第二波后胜利。
        var waves = new BattleSimulation(built, new WavePlan(2, 1, 1, 3, 10, 0.3), new TowerSettings(100, 5, 20, 0.3));
        waves.StartWave();
        for (int i = 0; i < 400 && !waves.State.CanStartWave && !waves.State.Finished; i++) waves.Step(0.05);
        Check(waves.State.Phase == BattlePhase.AwaitingNextWave, "Clearing the first wave must await the next one.");
        Check(waves.StartWave(), "The next wave must be startable.");
        for (int i = 0; i < 300 && !waves.State.Finished; i++) waves.Step(0.05);
        Check(waves.State.Phase == BattlePhase.Victory && waves.State.Wave == 2, "Clearing the last wave must win.");

        Console.WriteLine($"BATTLE_CHECKS_PASSED: {assertions} assertions.");
    }
}
