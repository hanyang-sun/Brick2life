using Brick2Life.Core;
using Brick2Life.Application;

internal static class LevelSemanticsChecks
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

        var empty = LevelState.FromSnapshot(Snapshot(1), rules);
        Check(empty.Cells.All(cell => !cell.Occupied && cell.Role == GameRole.None), "An empty board produced occupied cells.");
        Check(empty.Issues.Count == 1 && empty.Issues[0].Kind == LevelIssueKind.MissingCore, "An empty board must report a missing core.");
        Check(!empty.IsPlayable && empty.SourceVersion == 1, "An empty board must not be playable.");
        Check(empty.Describe().Contains("核心 0"), "Describe does not report the role counts.");

        var valid = LevelState.FromSnapshot(Snapshot(7, (0, 0, "blue"), (3, 4, "red"), (9, 9, "red"), (5, 5, "green")), rules);
        Check(valid.IsPlayable && valid.Issues.Count == 0, "A single-core layout must be playable.");
        Check(valid.Count(GameRole.Core) == 1 && valid.Count(GameRole.Tower) == 2 && valid.Count(GameRole.Wall) == 1, "Role counts are wrong.");
        Check(valid.SourceVersion == 7, "Level state lost the source version.");
        Check(valid.At(3, 4).Role == GameRole.Tower && valid.At(3, 4).ColorId == "red" && valid.At(3, 4).Occupied, "Cell roles are wrong.");
        Check(!valid.At(4, 4).Occupied && valid.At(4, 4).Role == GameRole.None, "An empty cell reported a role.");
        bool threw = false;
        try { valid.At(10, 0); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "Out-of-range grid access must throw.");

        var duplicated = LevelState.FromSnapshot(Snapshot(2, (0, 0, "blue"), (1, 0, "blue")), rules);
        Check(duplicated.Issues.Count == 1 && duplicated.Issues[0].Kind == LevelIssueKind.MultipleCores, "Two cores must be rejected.");
        Check(!duplicated.IsPlayable, "A two-core layout must not be playable.");

        var unmapped = LevelState.FromSnapshot(Snapshot(3, (0, 0, "blue"), (2, 2, "yellow")), rules);
        Check(unmapped.Issues.Count == 1 && unmapped.Issues[0].Kind == LevelIssueKind.UnmappedColor, "An unmapped color must be reported.");
        Check(unmapped.At(2, 2).Occupied && unmapped.At(2, 2).Role == GameRole.None, "Unmapped cells keep occupancy without a role.");
        Check(!unmapped.IsPlayable, "Unmapped colors must block the level.");
        Check(unmapped.Issues[0].Message.Contains("yellow"), "The unmapped issue does not name the color.");

        Check(rules.RoleOf("red") == GameRole.Tower && rules.RoleOf("missing") == GameRole.None, "Role lookup is wrong.");
        Check(!rules.TryGetRole("", out _) && !rules.TryGetRole(null, out _), "Blank colors must not resolve.");
        Check(rules.Rules.Count == 3, "Rule count is wrong.");

        int rejected = 0;
        void ExpectRuleFailure(Action action) { try { action(); } catch (ArgumentException) { rejected++; } }
        ExpectRuleFailure(() => LevelRuleSet.Create(new KeyValuePair<string, GameRole>("red", GameRole.None)));
        ExpectRuleFailure(() => LevelRuleSet.Create(
            new KeyValuePair<string, GameRole>("red", GameRole.Wall),
            new KeyValuePair<string, GameRole>("red", GameRole.Tower)));
        ExpectRuleFailure(() => LevelRuleSet.Create(new KeyValuePair<string, GameRole>(" ", GameRole.Wall)));
        Check(rejected == 3, "Invalid rule sets must be rejected.");

        threw = false;
        try { LevelState.FromSnapshot(null, rules); } catch (ArgumentNullException) { threw = true; }
        Check(threw, "A missing snapshot must be rejected.");
        threw = false;
        try { LevelState.FromSnapshot(Snapshot(1, (0, 0, "blue")), null); } catch (ArgumentNullException) { threw = true; }
        Check(threw, "A missing rule set must be rejected.");

        Console.WriteLine($"LEVEL_SEMANTICS_CHECKS_PASSED: {assertions} assertions.");
    }
}
