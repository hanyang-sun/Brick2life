using BrickDefense.Core;
if (args.Contains("--simulation-layout-checks")) { SimulationLayoutChecks.Run(); return; }
if (args.Contains("--combat-checks")) { CombatChecks.Run(); return; }
if (args.Contains("--multiple-spawn-checks")) { MultipleSpawnChecks.Run(); return; }
if (args.Contains("--scan-rejection-checks")) { ScanRejectionChecks.Run(); return; }
if (args.Contains("--layout-review-checks")) { await LayoutReviewChecks.Run(); return; }
if (args.Length>=2 && args[0]=="--replay-scan")
{
    int backgroundFlag=Array.IndexOf(args,"--background-at");
    ScanReplay.Run(args[1],args.Contains("--expect-rejected"),backgroundFlag>=0?args[backgroundFlag+1]:null);
    return;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static bool HasIssue(LevelResult level, string code)
{
    return Array.Exists(level.Issues, issue => issue.Code == code);
}

var rules = ExampleScans.CreateDemoRules();
var reference = ExampleScans.CreateReference();
var grid = GridMapper.Map(reference);
Check(reference.PhysicalCells.Length == 100, "Reference scan must contain 100 physical cells.");
Check(reference.Bricks.Length == 9, "Reference scan must contain nine separate bricks.");
Check(grid.Status == ResultStatus.Success, "Reference grid mapping failed.");
Check(grid.Placements.Length == 9 && grid.NavCells.Length == 400, "Grid dimensions are wrong.");
Check(grid.NavCells.Count(cell => cell.SourceBrickId != null) == 36,
    "Nine bricks must occupy 36 navigation cells.");
Check(grid.NavCells.Where(cell => cell.SourceBrickId != null)
    .GroupBy(cell => cell.SourceBrickId).All(group => group.Count() == 4),
    "Every brick must map to exactly four navigation cells.");

var level = LevelBuilder.Build(grid, rules, new GridPoint(1, 18));
Check(level.Status == ResultStatus.Success, "Reference level should be playable.");
Check(level.Entities.Length == 9 && level.CoreBrickIds.Length == 2,
    "Reference entities or cores are wrong.");
Check(level.TargetCoreBrickId == "brick-7-7", "Wrong nearest core.");
Check(level.Route.Length - 1 == 16, "Reference shortest route must be 16 steps.");
Check(level.Route[0].Equals(new GridPoint(1, 18)), "Route starts at the wrong cell.");
Check(level.Route[^1].Equals(new GridPoint(14, 15)), "Route ends at the wrong core cell.");
for (int i = 1; i < level.Route.Length; i++)
{
    int distance = Math.Abs(level.Route[i].X - level.Route[i - 1].X) +
                   Math.Abs(level.Route[i].Y - level.Route[i - 1].Y);
    Check(distance == 1, "Route must move exactly one orthogonal cell per step.");
}

var blocked = LevelBuilder.Build(GridMapper.Map(ExampleScans.CreateBlockedCore()), rules,
    new GridPoint(1, 18));
Check(blocked.Status == ResultStatus.Rejected && HasIssue(blocked, "NO_ROUTE"),
    "Surrounded core must be rejected as unreachable.");

var occupiedSpawn = LevelBuilder.Build(grid, rules, new GridPoint(14, 14));
Check(occupiedSpawn.Status == ResultStatus.Rejected && HasIssue(occupiedSpawn, "SPAWN_OCCUPIED"),
    "Occupied spawn must be rejected.");

var tie = ExampleScans.Parse("equal-distance-cores", new[]
{
    "..........", "..........", "..........", "..........", "...K......",
    ".....K....", "..........", "..........", "..........", ".........."
});
var tiedLevel = LevelBuilder.Build(GridMapper.Map(tie), rules, new GridPoint(9, 9));
Check(tiedLevel.Status == ResultStatus.Success && tiedLevel.Route.Length - 1 == 2 &&
      tiedLevel.TargetCoreBrickId == "brick-4-3",
    "Equal-distance cores must select the first physical row and column.");

var adjacent = ExampleScans.Parse("adjacent", new[]
{
    "KWW.......", "..........", "..........", "..........", "..........",
    "..........", "..........", "..........", "..........", ".........."
});
var adjacentGrid = GridMapper.Map(adjacent);
Check(adjacentGrid.Status == ResultStatus.Success && adjacentGrid.Placements.Length == 3,
    "Adjacent same-color bricks must remain separate.");
Check(adjacentGrid.Placements[1].BrickId != adjacentGrid.Placements[2].BrickId,
    "Adjacent walls received the same brick ID.");

var invalidCells = (PhysicalCellObservation[])reference.PhysicalCells.Clone();
invalidCells[0] = new PhysicalCellObservation(0, 0, PhysicalCellState.Uncertain);
var invalidScan = new ScanResult("invalid", ResultStatus.Success, invalidCells,
    reference.Bricks, Array.Empty<Issue>());
Check(GridMapper.Map(invalidScan).Status == ResultStatus.Rejected,
    "An uncertain physical cell must reject the scan.");

var unmappedRules = new LevelRules(new Dictionary<string, BrickRole>
{
    { ExampleScans.CoreColor, BrickRole.Core },
    { ExampleScans.WallColor, BrickRole.Wall },
    { "another-color", BrickRole.Tower }
});
Check(HasIssue(LevelBuilder.Build(grid, unmappedRules, new GridPoint(1, 18)), "UNMAPPED_COLOR"),
    "An unmapped physical color must reject the level.");

var noCore = ExampleScans.Parse("no-core", new[]
{
    "W.........", "..........", "..........", "..........", "..........",
    "..........", "..........", "..........", "..........", ".........."
});
Check(HasIssue(LevelBuilder.Build(GridMapper.Map(noCore), rules, new GridPoint(1, 18)), "NO_CORE"),
    "A level without a core must be rejected.");

var alteredCells = (GridCell[])grid.NavCells.Clone();
alteredCells[14 * BoardSize.Navigation + 14] = new GridCell(new GridPoint(14, 14), null);
var alteredGrid = new GridResult(grid.SourceScanId, ResultStatus.Success, grid.Placements,
    alteredCells, Array.Empty<Issue>());
Check(HasIssue(LevelBuilder.Build(alteredGrid, rules, new GridPoint(1, 18)), "INVALID_GRID"),
    "A brick missing one of its 2x2 source cells must be rejected.");

foreach (var malformedBrick in new[]
{
    new BrickObservation("brick-2-3", 3, 2, null),
    new BrickObservation("brick-2-3", 10, 2, ExampleScans.WallColor)
})
{
    var alteredPlacements = (GridPlacement[])grid.Placements.Clone();
    alteredPlacements[0] = new GridPlacement(malformedBrick);
    var malformedGrid = new GridResult(grid.SourceScanId, ResultStatus.Success, alteredPlacements,
        grid.NavCells, Array.Empty<Issue>());
    Check(HasIssue(LevelBuilder.Build(malformedGrid, rules, new GridPoint(1, 18)), "INVALID_GRID"),
        "Missing color or out-of-bounds physical coordinates must reject without throwing.");
}

RouteOracleChecks.Run();
CombatChecks.Run();
MultipleSpawnChecks.Run();
await WorkflowChecks.Run();
await LayoutReviewChecks.Run();
await PlainBackgroundChecks.Run();
ScanRejectionChecks.Run();
Console.WriteLine("All level pipeline checks passed.");
