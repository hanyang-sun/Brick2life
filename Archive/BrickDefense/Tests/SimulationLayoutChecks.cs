using Brick2Life.Simulation;

internal static class SimulationLayoutChecks
{
    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string message)
        { assertions++; if (!condition) throw new Exception(message); }

        var layout = new SimulationLayout();
        Check(layout.Bricks.Count == 0 && layout.Version == 0, "An empty board must be valid.");
        foreach (var cell in new[] { (-1, 0), (0, -1), (10, 0), (0, 10) })
        {
            Check(!layout.TryAdd(cell.Item1, cell.Item2, "red", out _), "Added outside the board.");
            Check(!layout.CanPlace(cell.Item1, cell.Item2), "An outside cell was available.");
        }
        Check(!layout.TryAdd(0, 0, " ", out _) && layout.Version == 0, "An invalid color changed the board.");
        Check(layout.TryAdd(0, 0, "red", out var original), "Could not add at the minimum corner.");
        Check(layout.TryAdd(9, 9, "blue", out var other), "Could not add at the maximum corner.");
        Check(!layout.TryAdd(0, 0, "green", out _) && layout.Version == 2, "An occupied add changed the board.");
        Check(!layout.TryMove(original.BrickId, 9, 9) && layout.Version == 2, "An occupied move changed the board.");
        Check(!layout.TryMove(original.BrickId, 10, 0) && layout.At(0, 0) == original, "An outside move lost its source.");
        Check(!layout.TryMove("missing", 5, 5) && !layout.TryDelete("missing"), "A missing ID was accepted.");
        Check(layout.TryMove(original.BrickId, 0, 0) && layout.Version == 2, "A same-cell drop changed the version.");
        Check(layout.TryMove(original.BrickId, 4, 6), "Could not move to an empty cell.");
        var moved = layout.At(4, 6);
        Check(moved.BrickId == original.BrickId && moved.ColorId == "red" && moved.Layer == 0,
            "A move changed identity, original color or layer.");
        Check(original.GridX == 0 && original.GridY == 0 && layout.At(0, 0) == null && layout.Version == 3,
            "A move mutated an old record or retained source occupancy.");
        Check(layout.TryDelete(moved.BrickId) && layout.At(4, 6) == null && layout.Version == 4, "Delete did not free the cell.");
        Check(layout.TryDelete(other.BrickId) && layout.Bricks.Count == 0, "Deleting the final brick must succeed.");
        Check(!layout.TryDelete(other.BrickId) && layout.Version == 5, "A repeated delete changed the version.");

        var ids = new HashSet<string>();
        for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
            {
                Check(layout.TryAdd(x, y, "free-color", out var brick), "Failed to fill a legal cell.");
                Check(ids.Add(brick.BrickId) && brick.GridX == x && brick.GridY == y && brick.Layer == 0,
                    "An ID or cell was incorrect.");
            }
        Check(layout.Bricks.Count == 100 && !layout.TryAdd(5, 5, "red", out _), "A full board accepted another brick.");
        foreach (var brick in layout.Bricks.ToArray()) Check(layout.TryDelete(brick.BrickId), "Could not empty a full board.");
        Check(layout.Bricks.Count == 0, "The board did not return to empty.");
        Console.WriteLine($"SIMULATION_LAYOUT_CHECKS_PASSED: {assertions} assertions; bounds, occupancy, stable IDs, immutable records, empty/full layouts and versions.");
    }
}
