using Brick2Life.Application;
using Brick2Life.Core;

internal static class DatasetPlanChecks
{
    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
        void Reject(Action action, string message)
        { try { action(); } catch (ArgumentException) { assertions++; return; } throw new Exception(message); }
        var board = new BoardDefinition(colorIds: new[] { "red", "green", "blue", "yellow" });
        var plan = new DatasetPlan(board);
        Check(plan.ImageCount == 100 && plan.Layouts.Count == 20 && plan.LightingCount == 5, "Daily default is not 100 images.");
        Check(plan.Layouts.Select(item => item.GroupId).Distinct().Count() == 20, "Repeated content was counted as a distinct layout.");
        Check(plan.Layouts.Select(item => item.Family).Distinct().Count() == 10, "Daily default missed a layout family.");
        Check(plan.Layouts.Count(item => item.Family == DatasetLayoutFamilies.Empty) == 1, "Empty layout was duplicated.");
        Check(plan.Layouts.SelectMany(item => item.Snapshot.Cells).All(cell => cell.IsKnown && (cell.Occupancy == Occupancy.Empty || board.AllowsColor(cell.ColorId))), "Generated an unknown cell or invalid color.");
        Check(plan.Layouts.Where(item => item.Family == DatasetLayoutFamilies.Dense).All(item => item.Snapshot.Cells.Count(cell => cell.Occupancy == Occupancy.Occupied) >= 75), "Dense family has incorrect occupancy.");
        var replay = new DatasetPlan(board);
        Check(plan.Layouts.Select(item => item.GroupId).SequenceEqual(replay.Layouts.Select(item => item.GroupId)), "Seed did not reproduce content.");
        Check(plan.Layouts.SelectMany(item => item.Snapshot.Cells).Select(cell => cell.BrickId).SequenceEqual(replay.Layouts.SelectMany(item => item.Snapshot.Cells).Select(cell => cell.BrickId)), "Seed did not reproduce authoring IDs.");
        var otherSeed = new DatasetPlan(board, seed: 73);
        Check(!plan.Layouts.Select(item => item.GroupId).SequenceEqual(otherSeed.Layouts.Select(item => item.GroupId)), "Changing the seed did not change the plan.");
        var baseline = new DatasetPlan(board, lightingCount: 1);
        Check(baseline.ImageCount == 20 && plan.Layouts.Select(item => item.GroupId).SequenceEqual(baseline.Layouts.Select(item => item.GroupId)), "Lighting selection changed logical layout groups.");
        var isolated = new DatasetPlan(board, 3, families: DatasetLayoutFamilies.Line);
        Check(isolated.Layouts.All(item => item.Family == DatasetLayoutFamilies.Line), "Unselected families leaked into the plan.");
        var cloneCells = plan.Layouts[1].Snapshot.Cells.Select(cell => cell.Occupancy == Occupancy.Empty ? BoardCell.Empty : new BoardCell(Occupancy.Occupied, cell.ColorId)).ToArray();
        Check(DatasetPlan.ContentId(new BoardSnapshot(board, 99, cloneCells, SnapshotSource.Manual)) == plan.Layouts[1].GroupId, "Group hash included authoring IDs, version or source.");
        Reject(() => new DatasetPlan(board, 0), "Zero layouts accepted.");
        Reject(() => new DatasetPlan(board, 201), "Unbounded generation accepted.");
        Reject(() => new DatasetPlan(board, families: 0), "No families accepted.");
        Reject(() => new DatasetPlan(board, families: (DatasetLayoutFamilies)2048), "Unknown families accepted.");
        Reject(() => new DatasetPlan(board, lightingCount: 3), "Unsupported lighting accepted.");
        Reject(() => new DatasetPlan(board, 2, families: DatasetLayoutFamilies.Empty), "Duplicated empty layouts accepted.");
        Reject(() => new DatasetPlan(new BoardDefinition()), "Unspecified palette accepted.");
        var empty = new DatasetPlan(board, 1, families: DatasetLayoutFamilies.Empty);
        Check(empty.ImageCount == 5 && empty.Layouts[0].Snapshot.Cells.All(cell => cell.Occupancy == Occupancy.Empty), "Single empty plan incorrect.");
        // Finite single-cell family cannot satisfy arbitrarily many unique layouts.
        Reject(() => new DatasetPlan(board, 17, families: DatasetLayoutFamilies.Center), "Finite family silently duplicated layouts.");
        Console.WriteLine($"DATASET_PLAN_CHECKS_PASSED ({assertions} assertions)");
    }
}
