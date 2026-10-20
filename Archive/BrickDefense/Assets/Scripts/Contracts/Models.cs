using System;
using System.Collections.Generic;

namespace BrickDefense.Core
{
    // 实体布局为 10×10；每个实体格展开为 2×2 导航格。所有模块共用这些尺寸。
    public static class BoardSize
    {
        public const int Physical = 10;
        public const int NavigationPerPhysical = 2;
        public const int Navigation = Physical * NavigationPerPhysical;
        public const int PhysicalCellCount = Physical * Physical;
        public const int NavigationCellCount = Navigation * Navigation;
    }

    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public int X { get; }
        public int Y { get; }

        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }

    public enum ResultStatus { Success, Rejected }
    public enum PhysicalCellState { Empty, Occupied, Uncertain }
    public enum BrickRole { Core, Wall, Tower }

    public sealed class Issue
    {
        public string Code { get; }
        public string Message { get; }
        public GridPoint? PhysicalCell { get; }
        public string FrameId { get; }
        public ImagePoint[] ImageRegion { get; }

        public Issue(string code, string message, GridPoint? physicalCell = null, string frameId = null,
            ImagePoint[] imageRegion = null)
        {
            Code = code;
            Message = message;
            PhysicalCell = physicalCell;
            FrameId = frameId;
            ImageRegion = imageRegion ?? Array.Empty<ImagePoint>();
        }
    }

    // 一个实体格的识别状态。Confidence 是颜色一致比例，不是经校准的正确概率。
    public sealed class PhysicalCellObservation
    {
        public int C { get; }
        public int R { get; }
        public PhysicalCellState State { get; }
        public string BrickId { get; }
        public double Confidence { get; }

        public PhysicalCellObservation(int c, int r, PhysicalCellState state, string brickId = null,
            double confidence = 1)
        {
            C = c;
            R = r;
            State = state;
            BrickId = brickId;
            Confidence = confidence;
        }
    }

    public sealed class BrickObservation
    {
        public string BrickId { get; }
        public int C { get; }
        public int R { get; }
        public string ColorId { get; }
        public double Confidence { get; }
        public ImageEvidence[] Evidence { get; }

        public BrickObservation(string brickId, int c, int r, string colorId,
            double confidence = 1, ImageEvidence[] evidence = null)
        {
            BrickId = brickId;
            C = c;
            R = r;
            ColorId = colorId;
            Confidence = confidence;
            Evidence = evidence ?? Array.Empty<ImageEvidence>();
        }
    }

    // 视觉输出：逐格状态与逐块记录必须相互对应，后续由 GridMapper 校验。
    public sealed class ScanResult
    {
        public string ScanId { get; }
        public ResultStatus Status { get; }
        public PhysicalCellObservation[] PhysicalCells { get; }
        public BrickObservation[] Bricks { get; }
        public Issue[] Issues { get; }
        public BoardObservation Board { get; }

        public ScanResult(string scanId, ResultStatus status, PhysicalCellObservation[] physicalCells,
            BrickObservation[] bricks, Issue[] issues, BoardObservation board = null)
        {
            ScanId = scanId;
            Status = status;
            PhysicalCells = physicalCells;
            Bricks = bricks;
            Issues = issues ?? Array.Empty<Issue>();
            Board = board;
        }
    }

    public sealed class GridPlacement
    {
        public string BrickId { get; }
        public int C { get; }
        public int R { get; }
        public string ColorId { get; }

        public GridPlacement(BrickObservation brick)
        {
            BrickId = brick.BrickId;
            C = brick.C;
            R = brick.R;
            ColorId = brick.ColorId;
        }
    }

    public sealed class GridCell
    {
        public GridPoint Position { get; }
        public string SourceBrickId { get; }

        public GridCell(GridPoint position, string sourceBrickId)
        {
            Position = position;
            SourceBrickId = sourceBrickId;
        }
    }

    // 中间结果同时保留实体积木和导航格，以便检查 1:4 的占用映射。
    public sealed class GridResult
    {
        public string SourceScanId { get; }
        public ResultStatus Status { get; }
        public GridPlacement[] Placements { get; }
        public GridCell[] NavCells { get; }
        public Issue[] Issues { get; }

        public GridResult(string sourceScanId, ResultStatus status, GridPlacement[] placements,
            GridCell[] navCells, Issue[] issues)
        {
            SourceScanId = sourceScanId;
            Status = status;
            Placements = placements;
            NavCells = navCells;
            Issues = issues ?? Array.Empty<Issue>();
        }
    }

    public sealed class LevelRules
    {
        public IReadOnlyDictionary<string, BrickRole> ColorRoleMap { get; }

        public LevelRules(IReadOnlyDictionary<string, BrickRole> colorRoleMap)
        {
            ColorRoleMap = colorRoleMap;
        }
    }

    public sealed class LevelEntity
    {
        public string BrickId { get; }
        public int C { get; }
        public int R { get; }
        public BrickRole Role { get; }

        public LevelEntity(GridPlacement placement, BrickRole role)
        {
            BrickId = placement.BrickId;
            C = placement.C;
            R = placement.R;
            Role = role;
        }
    }

    public sealed class LevelCell
    {
        public GridPoint Position { get; }
        public string SourceBrickId { get; }
        public BrickRole? Role { get; }

        public LevelCell(GridPoint position, string sourceBrickId, BrickRole? role)
        {
            Position = position;
            SourceBrickId = sourceBrickId;
            Role = role;
        }
    }

    // Each entrance owns a frozen route; an empty route marks a rejected entrance in the preview.
    public sealed class SpawnRoute
    {
        public GridPoint Spawn { get; }
        public string TargetCoreBrickId { get; }
        public IReadOnlyList<GridPoint> Route { get; }
        public bool HasRoute => Route.Count >= 2 && TargetCoreBrickId != null;

        public SpawnRoute(GridPoint spawn, string targetCoreBrickId, IReadOnlyList<GridPoint> route)
        {
            Spawn=spawn; TargetCoreBrickId=targetCoreBrickId;
            var points=new GridPoint[route?.Count ?? 0];
            for(int i=0;i<points.Length;i++) points[i]=route[i];
            Route=Array.AsReadOnly(points);
        }
    }

    // 关卡计算结果；即使路线被拒绝，也可能保留实体供界面预览。
    public sealed class LevelResult
    {
        public ResultStatus Status { get; }
        public LevelEntity[] Entities { get; }
        public LevelCell[] Cells { get; }
        public GridPoint Spawn { get; }
        public string[] CoreBrickIds { get; }
        public string TargetCoreBrickId { get; }
        public GridPoint[] Route { get; }
        public IReadOnlyList<SpawnRoute> SpawnRoutes { get; }
        public Issue[] Issues { get; }

        public LevelResult(ResultStatus status, LevelEntity[] entities, LevelCell[] cells, GridPoint spawn,
            string[] coreBrickIds, string targetCoreBrickId, GridPoint[] route, Issue[] issues)
            : this(status,entities,cells,coreBrickIds,
                new[] {new SpawnRoute(spawn,targetCoreBrickId,route)},issues)
        { }

        public LevelResult(ResultStatus status, LevelEntity[] entities, LevelCell[] cells,
            string[] coreBrickIds, SpawnRoute[] spawnRoutes, Issue[] issues)
        {
            Status = status;
            Entities = entities;
            Cells = cells;
            CoreBrickIds = coreBrickIds;
            SpawnRoutes=Array.AsReadOnly((SpawnRoute[])(spawnRoutes ?? Array.Empty<SpawnRoute>()).Clone());
            // Compatibility for single-entrance clients: these fields describe the first selected entrance.
            Spawn=SpawnRoutes.Count>0?SpawnRoutes[0].Spawn:new GridPoint(-1,-1);
            TargetCoreBrickId=SpawnRoutes.Count>0?SpawnRoutes[0].TargetCoreBrickId:null;
            Route=new GridPoint[SpawnRoutes.Count>0?SpawnRoutes[0].Route.Count:0];
            for(int i=0;i<Route.Length;i++) Route[i]=SpawnRoutes[0].Route[i];
            Issues = issues ?? Array.Empty<Issue>();
        }
    }
}
