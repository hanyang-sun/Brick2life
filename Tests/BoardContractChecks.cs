using Brick2Life.Core;

internal static class BoardContractChecks
{
    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
        void Reject(Action action, string message)
        {
            try { action(); } catch (ArgumentException) { assertions++; return; }
            throw new Exception(message);
        }
        var palette = new[] { "red", "blue" };
        var definition = new BoardDefinition(colorIds: palette);
        palette[0] = "tampered";
        Check(definition.AllowsColor("red") && !definition.AllowsColor("tampered"), "Definition retained caller-owned colors.");
        Reject(() => new BoardDefinition(colorIds: new[] { "red", "red" }), "Duplicate palette was accepted.");
        Reject(() => definition.Index(-1, 0), "Out-of-board index was accepted.");
        var layout = new BoardLayout(definition);
        Check(layout.TryAdd(0, 9, "red", out var brick), "Could not prepare layout.");
        Check(!layout.TryAdd(1, 1, "green", out _), "Unknown authoring color was accepted.");
        BoardSnapshot before = layout.Snapshot();
        Check(layout.TryMove(brick.BrickId, 9, 0), "Could not move brick.");
        BoardSnapshot after = layout.Snapshot();
        Check(before.At(0, 9).ColorId == "red" && before.At(9, 0).Occupancy == Occupancy.Empty, "Snapshot changed after editing.");
        Check(before.Cells[90].ColorId == "red" && after.Cells[9].ColorId == "red", "Row-major coordinates were transposed.");
        var restored = new BoardLayout(before);
        Check(restored.Version == before.Version && restored.At(0, 9).BrickId == brick.BrickId, "Restoration lost version or authoring identity.");
        Check(restored.TryDelete(brick.BrickId) && before.At(0, 9).BrickId == brick.BrickId, "Restored edit mutated frozen source.");
        BoardDelta delta = BoardDelta.Between(before, after);
        Check(delta.Changes.Count == 2 && delta.Changes[0].Position.Equals(new GridPoint(9, 0)), "Move did not produce the expected cell changes.");
        Check(!delta.HasAmbiguousMovement && delta.BaseVersion == before.Version && delta.TargetVersion == after.Version, "Delta versions or ambiguity incorrect.");
        Reject(() => BoardDelta.Between(after, before), "Backwards comparison was accepted.");
        Reject(() => BoardDelta.Between(before, new BoardLayout(new BoardDefinition("another-board")).Snapshot()), "Different boards were compared.");
        var raw = Enumerable.Repeat(BoardCell.Empty, 100).ToArray();
        var empty = new BoardSnapshot(definition, 0, raw, SnapshotSource.Manual);
        raw[0] = new BoardCell(Occupancy.Occupied, "red");
        Check(empty.At(0, 0).Occupancy == Occupancy.Empty, "Snapshot retained caller-owned array.");
        Reject(() => new BoardSnapshot(definition, 0, raw.Take(99), SnapshotSource.Manual), "Incomplete snapshot was accepted.");
        raw[0] = BoardCell.Unknown;
        Reject(() => new BoardSnapshot(definition, 0, raw, SnapshotSource.Vision), "Unknown occupancy became confirmed state.");
        raw[0] = new BoardCell(Occupancy.Occupied);
        Reject(() => new BoardSnapshot(definition, 0, raw, SnapshotSource.Vision), "Unknown occupied color became confirmed state.");
        raw[0] = new BoardCell(Occupancy.Occupied, "green");
        Reject(() => new BoardSnapshot(definition, 0, raw, SnapshotSource.Vision), "Unregistered snapshot color was accepted.");
        raw[0] = new BoardCell(Occupancy.Occupied, "red", "same"); raw[1] = new BoardCell(Occupancy.Occupied, "blue", "same");
        Reject(() => new BoardSnapshot(definition, 1, raw, SnapshotSource.Simulation), "Duplicate authoring identity was accepted.");
        var identical = new BoardSnapshot(definition, before.Version, before.Cells.Select(cell => cell.Occupancy == Occupancy.Empty
            ? BoardCell.Empty : new BoardCell(Occupancy.Occupied, cell.ColorId, "different-detector-id")), SnapshotSource.Vision);
        Check(BoardDelta.Between(before, identical).IsEmpty, "Source identities were mistaken for layout changes.");
        var a = new BoardLayout(definition);
        a.TryAdd(0, 0, "red", out var one); a.TryAdd(1, 0, "red", out var two);
        var frozen = a.Snapshot();
        a.TryMove(one.BrickId, 5, 5); a.TryMove(two.BrickId, 6, 5);
        Check(BoardDelta.Between(frozen, a.Snapshot()).HasAmbiguousMovement, "Same-color correspondence ambiguity was hidden.");
        Reject(() => new BoardCell(Occupancy.Empty, "red"), "Empty cell carried a color.");
        Reject(() => new ObservedCell(BoardCell.Empty, double.NaN), "NaN observation score was accepted.");
        Console.WriteLine($"BOARD_CONTRACT_CHECKS_PASSED: {assertions} assertions.");
    }
}
