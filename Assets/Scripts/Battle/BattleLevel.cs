using System;
using System.Collections.Generic;
using Brick2Life.Application;
using Brick2Life.Core;

namespace Brick2Life.Battle
{
    /// <summary>Navigation cell of the 2×2-per-brick battle grid. Empty cells have no role.</summary>
    public readonly struct BattleCell
    {
        public GridPoint Position { get; }
        public GameRole Role { get; }
        public string SourceBrickId { get; }
        public bool Blocked => Role == GameRole.Wall || Role == GameRole.Tower;
        public bool IsCore => Role == GameRole.Core;

        public BattleCell(GridPoint position, GameRole role, string sourceBrickId)
        {
            Position = position; Role = role; SourceBrickId = sourceBrickId;
        }
    }

    public sealed class BattleRoute
    {
        public GridPoint Spawn { get; }
        public IReadOnlyList<GridPoint> Route { get; }
        public string TargetCoreId { get; }
        public BattleRoute(GridPoint spawn, IEnumerable<GridPoint> route, string targetCoreId)
        {
            Spawn = spawn; Route = new List<GridPoint>(route).AsReadOnly(); TargetCoreId = targetCoreId;
        }
    }

    public readonly struct BattleIssue
    {
        public string Code { get; }
        public string Message { get; }
        public BattleIssue(string code, string message) { Code = code; Message = message; }
        public override string ToString() => Code + ": " + Message;
    }

    /// <summary>
    /// Converts a confirmed level (10×10 gameplay cells) into a 20×20 battle grid, validates the
    /// chosen entrances and computes one route per entrance. Pure game logic, no Unity dependency.
    /// </summary>
    public sealed class BattleLevel
    {
        public const int NavigationPerPhysical = 2;
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<BattleCell> Cells { get; }
        public IReadOnlyList<BattleRoute> Routes { get; }
        public IReadOnlyList<string> CoreBrickIds { get; }
        public IReadOnlyList<BattleIssue> Issues { get; }
        public bool IsValid => Issues.Count == 0;
        public GridPoint At(int x, int y) => new GridPoint(x, y);
        public BattleCell CellAt(int x, int y) => Cells[y * Width + x];

        private BattleLevel(int width, int height, BattleCell[] cells, List<BattleRoute> routes,
            List<string> cores, List<BattleIssue> issues)
        {
            Width = width; Height = height;
            Cells = Array.AsReadOnly(cells);
            Routes = routes.AsReadOnly();
            CoreBrickIds = cores.AsReadOnly();
            Issues = issues.AsReadOnly();
        }

        // spawns are physical (10×10) coordinates chosen by the user; they map to the top-left nav cell.
        public static BattleLevel Build(LevelState level, IReadOnlyList<GridPoint> spawns)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            var issues = new List<BattleIssue>();
            if (!level.IsPlayable)
            {
                issues.Add(new BattleIssue("LEVEL_NOT_PLAYABLE",
                    level.Issues.Count > 0 ? level.Issues[0].Message : "关卡不可用。"));
            }
            int width = level.Definition.Width * NavigationPerPhysical;
            int height = level.Definition.Height * NavigationPerPhysical;
            var cells = new BattleCell[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    cells[y * width + x] = new BattleCell(new GridPoint(x, y), GameRole.None, null);

            var cores = new List<string>();
            // Row-major order gives the multi-core tie-break a stable, reproducible priority.
            for (int y = 0; y < level.Definition.Height; y++)
                for (int x = 0; x < level.Definition.Width; x++)
                {
                    LevelCell cell = level.At(x, y);
                    if (!cell.Occupied || cell.Role == GameRole.None) continue;
                    string id = "brick-" + x + "-" + y;
                    if (cell.Role == GameRole.Core) cores.Add(id);
                    for (int dy = 0; dy < NavigationPerPhysical; dy++)
                        for (int dx = 0; dx < NavigationPerPhysical; dx++)
                        {
                            int nx = x * NavigationPerPhysical + dx, ny = y * NavigationPerPhysical + dy;
                            cells[ny * width + nx] = new BattleCell(new GridPoint(nx, ny), cell.Role, id);
                        }
                }
            if (cores.Count == 0) issues.Add(new BattleIssue("NO_CORE", "关卡缺少核心。"));

            var routes = new List<BattleRoute>();
            if (spawns == null || spawns.Count == 0)
                issues.Add(new BattleIssue("NO_SPAWNS", "至少选择一个空格作为敌人入口。"));
            else
            {
                var seen = new HashSet<GridPoint>();
                foreach (GridPoint spawn in spawns)
                {
                    if (!seen.Add(spawn))
                    {
                        issues.Add(new BattleIssue("SPAWN_DUPLICATE", "入口 (" + spawn.X + "," + spawn.Y + ") 重复。"));
                        continue;
                    }
                    if (!level.Definition.Contains(spawn.X, spawn.Y))
                    {
                        issues.Add(new BattleIssue("SPAWN_OUT_OF_BOUNDS", "入口 (" + spawn.X + "," + spawn.Y + ") 不在棋盘内。"));
                        continue;
                    }
                    LevelCell physical = level.At(spawn.X, spawn.Y);
                    if (physical.Occupied)
                    {
                        issues.Add(new BattleIssue("SPAWN_OCCUPIED", "入口 (" + spawn.X + "," + spawn.Y + ") 必须为空格。"));
                        continue;
                    }
                    var start = new GridPoint(spawn.X * NavigationPerPhysical, spawn.Y * NavigationPerPhysical);
                    GridPoint[] route = RouteSearch.Find(cells, width, height, cores, start);
                    if (route == null)
                    {
                        issues.Add(new BattleIssue("NO_ROUTE", "入口 (" + spawn.X + "," + spawn.Y + ") 没有到核心的通路。"));
                        continue;
                    }
                    string target = cells[route[route.Length - 1].Y * width + route[route.Length - 1].X].SourceBrickId;
                    routes.Add(new BattleRoute(start, route, target));
                }
            }
            return new BattleLevel(width, height, cells, routes, cores, issues);
        }
    }
}
