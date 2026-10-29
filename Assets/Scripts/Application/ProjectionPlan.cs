using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Brick2Life.Core;

namespace Brick2Life.Application
{
    // Engine-neutral description of what the virtual projector draws. Adapters map rectangles to geometry.
    public enum ProjectionSurface { Table = 0, BrickTop = 1 }
    public enum ProjectionMarkKind { GridPoint = 0, RoleHighlight = 1, ChangeHighlight = 2 }
    public enum ProjectionChange { Added = 0, Removed = 1, Recolored = 2 }
    // Outlines mark secondary information without hiding the fill underneath.
    public enum ProjectionShape { Fill = 0, Ring = 1 }

    public readonly struct ProjectionColor
    {
        public float Red { get; }
        public float Green { get; }
        public float Blue { get; }
        public float Alpha { get; }
        public ProjectionColor(float red, float green, float blue, float alpha)
        {
            if (!Finite(red) || !Finite(green) || !Finite(blue) || !Finite(alpha) ||
                red < 0 || red > 1 || green < 0 || green > 1 || blue < 0 || blue > 1 || alpha < 0 || alpha > 1)
                throw new ArgumentOutOfRangeException(nameof(red), "Projection colors are normalized RGBA values.");
            Red = red; Green = green; Blue = blue; Alpha = alpha;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public ProjectionColor WithAlpha(float alpha) => new ProjectionColor(Red, Green, Blue, alpha);
        public override string ToString() => $"rgba({Red:0.00},{Green:0.00},{Blue:0.00},{Alpha:0.00})";
    }

    // Rectangle in board cell units: X/Y is the top-left corner, SizeX/SizeY the extent (1 = one cell).
    public sealed class ProjectionMark
    {
        public float X { get; }
        public float Y { get; }
        public float SizeX { get; }
        public float SizeY { get; }
        public ProjectionMarkKind Kind { get; }
        public ProjectionSurface Surface { get; }
        public GameRole Role { get; }
        public ProjectionChange? Change { get; }
        public ProjectionShape Shape { get; }
        public ProjectionColor Color { get; }
        public float CenterX => X + SizeX * 0.5f;
        public float CenterY => Y + SizeY * 0.5f;

        internal ProjectionMark(float x, float y, float sizeX, float sizeY, ProjectionMarkKind kind,
            ProjectionSurface surface, GameRole role, ProjectionChange? change, ProjectionColor color,
            ProjectionShape shape = ProjectionShape.Fill)
        {
            X = x; Y = y; SizeX = sizeX; SizeY = sizeY; Kind = kind; Surface = surface;
            Role = role; Change = change; Color = color; Shape = shape;
        }
        public override string ToString() =>
            $"{Kind}@({X:0.00},{Y:0.00}) size {SizeX:0.00}x{SizeY:0.00} {Surface} {Role} {Change} {Color}";
    }

    public sealed class ProjectionOptions
    {
        public bool IncludeGridPoints { get; set; } = true;
        public bool IncludeRoleHighlights { get; set; } = true;
        public bool IncludeChangeHighlights { get; set; } = true;
        // Hard cap keeps the projector workload bounded on dense boards and after large deltas.
        public int MaxMarks { get; set; } = 400;
        public float GridPointSize { get; set; } = 0.07f;
        public float HighlightSize { get; set; } = 0.8f;
        public float ChangeSize { get; set; } = 0.96f;
        // The lattice is context, not decoration: keep it faint so role patterns stay readable.
        public float GridAlpha { get; set; } = 0.16f;
        public float HighlightAlpha { get; set; } = 0.32f;
        public float ChangeAlpha { get; set; } = 0.75f;
        // Change marks fade out in the view after this many seconds; the plan only carries the duration.
        public float ChangeFadeSeconds { get; set; } = 1.6f;

        internal void Validate()
        {
            if (MaxMarks < 0) throw new ArgumentOutOfRangeException(nameof(MaxMarks));
            if (!Positive(GridPointSize) || !Positive(HighlightSize) || !Positive(ChangeSize))
                throw new ArgumentOutOfRangeException(nameof(GridPointSize), "Projection sizes must be positive.");
            if (!Unit(GridAlpha) || !Unit(HighlightAlpha) || !Unit(ChangeAlpha))
                throw new ArgumentOutOfRangeException(nameof(GridAlpha), "Projection alphas are normalized.");
            if (!Positive(ChangeFadeSeconds))
                throw new ArgumentOutOfRangeException(nameof(ChangeFadeSeconds), "Change fade duration must be positive.");
        }
        private static bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        private static bool Unit(float value) => !float.IsNaN(value) && value >= 0 && value <= 1;
    }

    public sealed class ProjectionPlan
    {
        public BoardDefinition Definition { get; }
        public long LevelVersion { get; }
        public IReadOnlyList<ProjectionMark> Marks { get; }
        public bool Truncated { get; }
        public string Summary { get; }

        internal ProjectionPlan(BoardDefinition definition, long levelVersion, List<ProjectionMark> marks,
            bool truncated, string summary)
        {
            Definition = definition; LevelVersion = levelVersion;
            Marks = new ReadOnlyCollection<ProjectionMark>(marks);
            Truncated = truncated; Summary = summary;
        }

        public int Count(ProjectionMarkKind kind)
        {
            int count = 0;
            foreach (ProjectionMark mark in Marks) if (mark.Kind == kind) count++;
            return count;
        }
    }

    public static class ProjectionPlanBuilder
    {
        // Fixed palette: role colors mirror the brick palette, change colors describe what the user just did.
        private static readonly ProjectionColor GridColor = new ProjectionColor(0.85f, 0.92f, 1f, 0.35f);
        private static readonly ProjectionColor CoreColor = new ProjectionColor(0.35f, 0.72f, 1f, 0.55f);
        private static readonly ProjectionColor WallColor = new ProjectionColor(0.78f, 0.82f, 0.88f, 0.55f);
        private static readonly ProjectionColor TowerColor = new ProjectionColor(1f, 0.64f, 0.22f, 0.55f);
        private static readonly ProjectionColor AddedColor = new ProjectionColor(0.30f, 1f, 0.55f, 0.8f);
        private static readonly ProjectionColor RemovedColor = new ProjectionColor(1f, 0.32f, 0.30f, 0.8f);
        private static readonly ProjectionColor RecoloredColor = new ProjectionColor(1f, 0.86f, 0.32f, 0.8f);

        public static ProjectionColor ColorOf(GameRole role) =>
            role == GameRole.Core ? CoreColor : role == GameRole.Wall ? WallColor : role == GameRole.Tower ? TowerColor : GridColor;

        public static ProjectionColor ColorOf(ProjectionChange change) =>
            change == ProjectionChange.Added ? AddedColor : change == ProjectionChange.Removed ? RemovedColor : RecoloredColor;

        // Delta must describe exactly the transition into level.SourceVersion, otherwise its cells are not trustworthy.
        public static ProjectionPlan Build(LevelState level, BoardDelta delta, ProjectionOptions options = null)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (delta != null && (delta.TargetVersion != level.SourceVersion ||
                delta.BoardId != level.Definition.BoardId))
                throw new ArgumentException("Changes must end at the projected level version.", nameof(delta));
            var settings = options ?? new ProjectionOptions();
            settings.Validate();
            var marks = new List<ProjectionMark>();
            bool truncated = false;
            void Add(ProjectionMark mark)
            {
                if (marks.Count >= settings.MaxMarks) { truncated = true; return; }
                marks.Add(mark);
            }

            if (settings.IncludeGridPoints)
                for (int y = 0; y <= level.Definition.Height; y++)
                    for (int x = 0; x <= level.Definition.Width; x++)
                        Add(new ProjectionMark(ClampGrid(x, settings.GridPointSize, level.Definition.Width),
                            ClampGrid(y, settings.GridPointSize, level.Definition.Height),
                            settings.GridPointSize, settings.GridPointSize, ProjectionMarkKind.GridPoint,
                            LatticeSurface(level, x, y), GameRole.None, null,
                            GridColor.WithAlpha(settings.GridAlpha)));

            if (settings.IncludeRoleHighlights)
                for (int y = 0; y < level.Definition.Height; y++)
                    for (int x = 0; x < level.Definition.Width; x++)
                    {
                        LevelCell cell = level.At(x, y);
                        if (!cell.Occupied || cell.Role == GameRole.None) continue;
                        Add(new ProjectionMark(Center(x, settings.HighlightSize), Center(y, settings.HighlightSize),
                            settings.HighlightSize, settings.HighlightSize, ProjectionMarkKind.RoleHighlight,
                            ProjectionSurface.BrickTop, cell.Role, null,
                            ColorOf(cell.Role).WithAlpha(settings.HighlightAlpha)));
                    }

            if (settings.IncludeChangeHighlights && delta != null)
                foreach (CellChange change in delta.Changes)
                {
                    ProjectionChange kind;
                    if (change.Before.Occupancy != Occupancy.Occupied) kind = ProjectionChange.Added;
                    else if (change.After.Occupancy != Occupancy.Occupied) kind = ProjectionChange.Removed;
                    else kind = ProjectionChange.Recolored;
                    // A removed brick no longer provides a top face, so its mark must land on the table.
                    bool onBrick = change.After.Occupancy == Occupancy.Occupied;
                    Add(new ProjectionMark(Center(change.Position.X, settings.ChangeSize), Center(change.Position.Y, settings.ChangeSize),
                        settings.ChangeSize, settings.ChangeSize, ProjectionMarkKind.ChangeHighlight,
                        onBrick ? ProjectionSurface.BrickTop : ProjectionSurface.Table, GameRole.None, kind,
                        ColorOf(kind).WithAlpha(settings.ChangeAlpha), ProjectionShape.Ring));
                }

            string summary = $"格点 {CountOf(marks, ProjectionMarkKind.GridPoint)} · 高亮 {CountOf(marks, ProjectionMarkKind.RoleHighlight)}" +
                $" · 变更 {CountOf(marks, ProjectionMarkKind.ChangeHighlight)}" + (truncated ? "（已截断）" : "");
            return new ProjectionPlan(level.Definition, level.SourceVersion, marks, truncated, summary);
        }

        private static float Center(int cell, float size) => cell + (1 - size) * 0.5f;

        // Lattice points stay inside the board: at the border they would otherwise hang half off the table.
        private static float ClampGrid(int coordinate, float size, int extent)
        {
            float half = size * 0.5f;
            float value = coordinate - half;
            float max = extent - size;
            if (value < 0) return 0;
            if (value > max) return max;
            return value;
        }

        // A lattice point is drawn on brick height when any adjacent cell is occupied, matching an overhead projector.
        private static ProjectionSurface LatticeSurface(LevelState level, int x, int y)
        {
            for (int dy = -1; dy <= 0; dy++)
                for (int dx = -1; dx <= 0; dx++)
                {
                    int cx = x + dx, cy = y + dy;
                    if (cx < 0 || cy < 0 || cx >= level.Definition.Width || cy >= level.Definition.Height) continue;
                    if (level.At(cx, cy).Occupied) return ProjectionSurface.BrickTop;
                }
            return ProjectionSurface.Table;
        }

        private static int CountOf(List<ProjectionMark> marks, ProjectionMarkKind kind)
        {
            int count = 0;
            foreach (ProjectionMark mark in marks) if (mark.Kind == kind) count++;
            return count;
        }
    }
}
