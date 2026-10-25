using Brick2Life.Core;
using Brick2Life.Application;

internal static class ProjectionPlanChecks
{
    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }

        var definition = new BoardDefinition(colorIds: new[] { "red", "green", "blue", "yellow" });
        var rules = LevelRuleSet.Create(
            new KeyValuePair<string, GameRole>("red", GameRole.Tower),
            new KeyValuePair<string, GameRole>("green", GameRole.Wall),
            new KeyValuePair<string, GameRole>("blue", GameRole.Core));
        BoardSnapshot Snapshot(long version, params (int x, int y, string color)[] bricks)
        {
            var cells = new BoardCell[definition.CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = BoardCell.Empty;
            foreach (var brick in bricks)
                cells[definition.Index(brick.x, brick.y)] = new BoardCell(Occupancy.Occupied, brick.color);
            return new BoardSnapshot(definition, version, cells, SnapshotSource.Vision);
        }

        LevelState Level(long version, params (int x, int y, string color)[] bricks) =>
            LevelState.FromSnapshot(Snapshot(version, bricks), rules);

        var level = Level(1, (0, 0, "blue"), (3, 4, "red"), (9, 9, "green"));
        var plan = ProjectionPlanBuilder.Build(level, null);
        Check(plan.LevelVersion == 1 && ReferenceEquals(plan.Definition, definition), "Plan identity is wrong.");
        Check(plan.Count(ProjectionMarkKind.GridPoint) == 121, "A 10x10 board has 11x11 lattice points.");
        Check(plan.Count(ProjectionMarkKind.RoleHighlight) == 3, "Role highlights must cover occupied cells only.");
        Check(plan.Count(ProjectionMarkKind.ChangeHighlight) == 0, "A baseline plan must not invent change marks.");
        Check(!plan.Truncated && plan.Summary.Contains("格点 121"), "Plan summary or truncation flag is wrong.");

        var grid = plan.Marks.First(mark => mark.Kind == ProjectionMarkKind.GridPoint);
        Check(Math.Abs(grid.SizeX - 0.12f) < 1e-4 && grid.Surface == ProjectionSurface.BrickTop, "Lattice point (0,0) must sit on a brick top.");
        var farGrid = plan.Marks.First(mark => mark.Kind == ProjectionMarkKind.GridPoint && Math.Abs(mark.CenterX - 5) < 1e-3 && Math.Abs(mark.CenterY - 5) < 1e-3);
        Check(farGrid.Surface == ProjectionSurface.Table, "A lattice point away from bricks must sit on the table.");

        var highlight = plan.Marks.First(mark => mark.Kind == ProjectionMarkKind.RoleHighlight && mark.Role == GameRole.Tower);
        Check(Math.Abs(highlight.CenterX - 3.5f) < 1e-4 && Math.Abs(highlight.CenterY - 4.5f) < 1e-4, "Role highlight is not centered on its cell.");
        Check(highlight.Surface == ProjectionSurface.BrickTop, "Role highlights must be projected on brick tops.");
        Check(Math.Abs(highlight.Color.Alpha - 0.55f) < 1e-4, "Role highlight alpha does not follow the options.");
        Check(ProjectionPlanBuilder.ColorOf(GameRole.Tower).Red > ProjectionPlanBuilder.ColorOf(GameRole.Wall).Red, "Role colors are not distinguishable.");

        var noGrid = ProjectionPlanBuilder.Build(level, null, new ProjectionOptions { IncludeGridPoints = false });
        Check(noGrid.Count(ProjectionMarkKind.GridPoint) == 0 && noGrid.Count(ProjectionMarkKind.RoleHighlight) == 3, "Disabling lattice points changed highlights.");

        // A move is reported by the core delta as a removal plus an addition.
        var before = Snapshot(1, (0, 0, "blue"), (3, 4, "red"));
        var after = Snapshot(2, (0, 0, "blue"), (3, 5, "red"));
        var delta = BoardDelta.Between(before, after);
        var moved = ProjectionPlanBuilder.Build(LevelState.FromSnapshot(after, rules), delta);
        Check(moved.Count(ProjectionMarkKind.ChangeHighlight) == 2, "A move must produce two change marks.");
        Check(moved.Marks.Count(mark => mark.Kind == ProjectionMarkKind.ChangeHighlight && mark.Change == ProjectionChange.Added) == 1, "Missing added mark.");
        Check(moved.Marks.Count(mark => mark.Kind == ProjectionMarkKind.ChangeHighlight && mark.Change == ProjectionChange.Removed) == 1, "Missing removed mark.");
        Check(moved.Marks.Where(mark => mark.Kind == ProjectionMarkKind.ChangeHighlight).All(mark => mark.Surface == ProjectionSurface.BrickTop), "Change marks must sit on a visible surface.");
        Check(moved.Marks.Where(mark => mark.Kind == ProjectionMarkKind.ChangeHighlight && mark.Change == ProjectionChange.Added)
            .All(mark => Math.Abs(mark.CenterX - 3.5f) < 1e-4 && Math.Abs(mark.CenterY - 5.5f) < 1e-4), "Added mark is not on the new cell.");

        var recoloredDelta = BoardDelta.Between(Snapshot(1, (2, 2, "red")), Snapshot(2, (2, 2, "green")));
        var recolored = ProjectionPlanBuilder.Build(LevelState.FromSnapshot(Snapshot(2, (2, 2, "green")), rules), recoloredDelta);
        Check(recolored.Count(ProjectionMarkKind.ChangeHighlight) == 1 &&
            recolored.Marks.Single(mark => mark.Kind == ProjectionMarkKind.ChangeHighlight).Change == ProjectionChange.Recolored, "A recolor must be one recolored mark.");

        var removedDelta = BoardDelta.Between(Snapshot(1, (2, 2, "red")), Snapshot(2));
        var removed = ProjectionPlanBuilder.Build(LevelState.FromSnapshot(Snapshot(2), rules), removedDelta);
        Check(removed.Marks.Single(mark => mark.Kind == ProjectionMarkKind.ChangeHighlight).Change == ProjectionChange.Removed, "A removal must be reported as removed.");

        int colourCount = removed.Marks.Count(mark => mark.Kind == ProjectionMarkKind.ChangeHighlight);
        Check(colourCount == 1 && removed.Marks.Single(mark => mark.Kind == ProjectionMarkKind.ChangeHighlight).Color.Red > 0.9f, "Removal color is not the removal tone.");

        var capped = ProjectionPlanBuilder.Build(level, null, new ProjectionOptions { MaxMarks = 5 });
        Check(capped.Truncated && capped.Marks.Count == 5, "The mark cap must truncate deterministically.");

        var emptyLevel = Level(1);
        var emptyPlan = ProjectionPlanBuilder.Build(emptyLevel, null);
        Check(emptyPlan.Count(ProjectionMarkKind.RoleHighlight) == 0 && emptyPlan.Count(ProjectionMarkKind.GridPoint) == 121, "An empty board must only project lattice points.");

        var unmappedLevel = Level(1, (0, 0, "blue"), (1, 1, "yellow"));
        var unmappedPlan = ProjectionPlanBuilder.Build(unmappedLevel, null);
        Check(unmappedPlan.Count(ProjectionMarkKind.RoleHighlight) == 1, "Cells without a role must not be highlighted.");

        var first = ProjectionPlanBuilder.Build(level, null);
        var second = ProjectionPlanBuilder.Build(level, null);
        Check(first.Marks.Select(mark => mark.ToString()).SequenceEqual(second.Marks.Select(mark => mark.ToString())), "Plan output must be deterministic.");

        int failures = 0;
        void ExpectFailure(Action action) { try { action(); } catch (ArgumentException) { failures++; } }
        ExpectFailure(() => ProjectionPlanBuilder.Build(null, null));
        ExpectFailure(() => ProjectionPlanBuilder.Build(LevelState.FromSnapshot(Snapshot(3, (0, 0, "blue")), rules), delta));
        ExpectFailure(() => ProjectionPlanBuilder.Build(level, null, new ProjectionOptions { MaxMarks = -1 }));
        ExpectFailure(() => ProjectionPlanBuilder.Build(level, null, new ProjectionOptions { GridAlpha = 2 }));
        ExpectFailure(() => new ProjectionColor(0, 0, 0, 1.5f));
        Check(failures == 5, "Invalid plan inputs must be rejected.");

        Console.WriteLine($"PROJECTION_PLAN_CHECKS_PASSED: {assertions} assertions.");
    }
}
