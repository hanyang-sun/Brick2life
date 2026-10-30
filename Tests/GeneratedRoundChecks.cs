using Brick2Life.Application;
using Brick2Life.Core;

internal static class GeneratedRoundChecks
{
    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
        void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, message);
        }

        var definition = new BoardDefinition(colorIds: new[] { "blue", "red", "green", "yellow" });
        var playerRules = LevelRuleSet.Create(
            new KeyValuePair<string, GameRole>("red", GameRole.Tower),
            new KeyValuePair<string, GameRole>("blue", GameRole.Wall),
            new KeyValuePair<string, GameRole>("green", GameRole.Wall),
            new KeyValuePair<string, GameRole>("yellow", GameRole.Wall));
        int perimeter = 2 * definition.Width + 2 * definition.Height - 4;
        foreach (int seed in new[] { 0, 1, -1, int.MinValue, int.MaxValue, 20261030, unchecked((int)0x9e3779b9u) })
            foreach (int count in new[] { 1, 2, 4, perimeter })
            {
                GeneratedRound round = GeneratedRound.Generate(definition, seed, count);
                GeneratedRound replay = GeneratedRound.Generate(definition, seed, count);
                Check(round.Seed == seed && ReferenceEquals(round.Definition, definition), "Round lost its seed or definition.");
                Check(round.Core.Equals(replay.Core) && round.Spawns.SequenceEqual(replay.Spawns), "The same seed did not reproduce a round.");
                Check(round.Core.X > 0 && round.Core.X < definition.Width - 1 && round.Core.Y > 0 &&
                    round.Core.Y < definition.Height - 1, "Generated core is not an interior cell.");
                Check(round.Spawns.Count == count && new HashSet<GridPoint>(round.Spawns).Count == count,
                    "Spawn count or uniqueness is wrong.");
                Check(round.Spawns.All(p => definition.Contains(p.X, p.Y) && !p.Equals(round.Core) &&
                    (p.X == 0 || p.Y == 0 || p.X == definition.Width - 1 || p.Y == definition.Height - 1)),
                    "A spawn is not a distinct, in-bounds edge cell.");
                Check(round.IsReserved(round.Core.X, round.Core.Y) && round.Spawns.All(p => round.IsReserved(p.X, p.Y)),
                    "System goal reservation is incomplete.");
                Check(!round.IsReserved(-1, 0) && !round.IsReserved(definition.Width, 0), "Out-of-bounds coordinates became reserved.");
            }

        var variants = Enumerable.Range(0, 32).Select(seed => GeneratedRound.Generate(definition, seed, 3)).ToArray();
        Check(variants.Select(r => r.Core).Distinct().Count() > 8 && variants.Select(r => string.Join(";", r.Spawns)).Distinct().Count() > 8,
            "Round generation does not vary across seeds.");
        GeneratedRound configured = variants[0];
        bool immutable = false;
        try { ((IList<GridPoint>)configured.Spawns)[0] = new GridPoint(4, 4); }
        catch (NotSupportedException) { immutable = true; }
        Check(immutable, "Callers can mutate fixed system spawns.");
        Reject(() => GeneratedRound.Generate(null, 1, 1), "Null board definition was accepted.");
        Reject(() => GeneratedRound.Generate(definition, 1, 0), "Zero spawns were accepted.");
        Reject(() => GeneratedRound.Generate(definition, 1, -1), "Negative spawn count was accepted.");
        Reject(() => GeneratedRound.Generate(definition, 1, perimeter + 1), "Too many unique spawns were accepted.");

        BoardSnapshot Snapshot(long version, params (int x, int y, string color)[] bricks)
        {
            var cells = Enumerable.Repeat(BoardCell.Empty, definition.CellCount).ToArray();
            foreach (var brick in bricks)
                cells[definition.Index(brick.x, brick.y)] = new BoardCell(Occupancy.Occupied, brick.color);
            return new BoardSnapshot(definition, version, cells, SnapshotSource.Vision);
        }
        GridPoint free = Enumerable.Range(0, definition.CellCount).Select(i => new GridPoint(i % definition.Width, i / definition.Width))
            .First(p => !configured.IsReserved(p.X, p.Y));
        BoardSnapshot emptyObservation = Snapshot(42);
        LevelState empty = LevelState.FromSnapshot(emptyObservation, playerRules, configured);
        Check(empty.IsPlayable && empty.Count(GameRole.Core) == 1 && empty.SourceVersion == 42,
            "Empty player observation did not produce the configured core.");
        Check(ReferenceEquals(empty.Round, configured), "Level does not expose its immutable system round.");
        Check(!empty.At(configured.Core.X, configured.Core.Y).Occupied && empty.At(configured.Core.X, configured.Core.Y).ColorId == "",
            "System core was fabricated as an occupied/color prediction.");
        Check(empty.Cells.Count(c => c.Occupied) == 0 && emptyObservation.Cells.All(c => c.Occupancy == Occupancy.Empty),
            "Applying system goals changed the player occupancy observation.");
        LevelState player = LevelState.FromSnapshot(Snapshot(43, (free.X, free.Y, "blue")), playerRules, configured);
        Check(player.IsPlayable && player.Count(GameRole.Wall) == 1 && player.Count(GameRole.Core) == 1 &&
            player.At(free.X, free.Y).ColorId == "blue", "Player wall color was changed or inferred as an extra core.");
        LevelState occupiedCore = LevelState.FromSnapshot(Snapshot(44, (configured.Core.X, configured.Core.Y, "green")), playerRules, configured);
        Check(!occupiedCore.IsPlayable && occupiedCore.Issues.Any(i => i.Kind == LevelIssueKind.ReservedCellOccupied),
            "Occupied system core cell was not reported as a rule violation.");
        Check(occupiedCore.At(configured.Core.X, configured.Core.Y).Occupied &&
            occupiedCore.At(configured.Core.X, configured.Core.Y).ColorId == "green" && occupiedCore.Count(GameRole.Core) == 1,
            "Rule validation silently erased the visual occupancy or moved the core.");
        GridPoint spawn = configured.Spawns[0];
        LevelState occupiedSpawn = LevelState.FromSnapshot(Snapshot(45, (spawn.X, spawn.Y, "red")), playerRules, configured);
        Check(!occupiedSpawn.IsPlayable && occupiedSpawn.Issues.Any(i => i.Kind == LevelIssueKind.ReservedCellOccupied) &&
            occupiedSpawn.At(spawn.X, spawn.Y).Occupied && occupiedSpawn.At(spawn.X, spawn.Y).ColorId == "red",
            "Occupied spawn cell was not preserved and reported.");

        var legacyRules = LevelRuleSet.Create(new KeyValuePair<string, GameRole>("blue", GameRole.Core),
            new KeyValuePair<string, GameRole>("red", GameRole.Tower));
        LevelState extraCore = LevelState.FromSnapshot(Snapshot(46, (free.X, free.Y, "blue")), legacyRules, configured);
        Check(!extraCore.IsPlayable && extraCore.Issues.Any(i => i.Kind == LevelIssueKind.PlayerCoreNotAllowed) &&
            extraCore.Count(GameRole.Core) == 1 && extraCore.At(free.X, free.Y).Role == GameRole.None,
            "Generated mode accepted a player-generated core.");
        LevelState legacy = LevelState.FromSnapshot(Snapshot(47, (free.X, free.Y, "blue")), legacyRules);
        Check(legacy.IsPlayable && legacy.Round == null && legacy.Count(GameRole.Core) == 1 && legacy.At(free.X, free.Y).Occupied,
            "Legacy two-argument semantics changed.");
        LevelState reference = configured.CreateReferenceLevel(playerRules);
        Check(reference.SourceVersion == 0 && reference.IsPlayable && reference.Count(GameRole.Core) == 1 &&
            reference.Cells.All(c => !c.Occupied) && ReferenceEquals(reference.Round, configured),
            "System reference is not an empty-player, version-zero level.");
        Reject(() => configured.CreateReferenceLevel(null), "Null reference rules were accepted.");
        var other = new BoardDefinition("another-board", definition.ColorIds);
        var incompatible = new BoardSnapshot(other, 1, Enumerable.Repeat(BoardCell.Empty, other.CellCount), SnapshotSource.Vision);
        Reject(() => LevelState.FromSnapshot(incompatible, playerRules, configured), "A different board accepted the round's system goals.");

        Console.WriteLine($"GENERATED_ROUND_CHECKS_PASSED: {assertions} assertions.");
    }
}
