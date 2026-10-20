using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brick2Life.Core
{
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public int X { get; }
        public int Y { get; }
        public GridPoint(int x, int y) { X = x; Y = y; }
        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }

    // Phase one: one fixed 10 x 10 board, one layer. Image/world transforms belong to adapters.
    public sealed class BoardDefinition
    {
        public const int Size = 10;
        private readonly HashSet<string> colors;
        public string BoardId { get; }
        public int DefinitionVersion { get; }
        public int Width => Size;
        public int Height => Size;
        public int CellCount => Width * Height;
        public double CellSize { get; }
        public string Unit { get; }
        public string Coordinates => "origin=min-X/min-Y; X/right; Y/forward; layer=0; cells[y*width+x]";
        // Empty palette explicitly means unrestricted nonblank color IDs (authoring/tests).
        public IReadOnlyList<string> ColorIds { get; }

        public BoardDefinition(string boardId = "brick2life-main", IEnumerable<string> colorIds = null,
            int definitionVersion = 1, double cellSize = 1, string unit = "simulation-unit")
        {
            if (string.IsNullOrWhiteSpace(boardId) || string.IsNullOrWhiteSpace(unit))
                throw new ArgumentException("Board ID and unit are required.");
            if (definitionVersion < 1 || double.IsNaN(cellSize) || double.IsInfinity(cellSize) || cellSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            BoardId = boardId; DefinitionVersion = definitionVersion; CellSize = cellSize; Unit = unit;
            colors = new HashSet<string>(StringComparer.Ordinal);
            var ordered = new List<string>();
            if (colorIds != null)
                foreach (string color in colorIds)
                {
                    if (string.IsNullOrWhiteSpace(color) || !colors.Add(color))
                        throw new ArgumentException("Color IDs must be nonblank and unique.");
                    ordered.Add(color);
                }
            ColorIds = new ReadOnlyCollection<string>(ordered);
        }

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public int Index(int x, int y)
        {
            if (!Contains(x, y)) throw new ArgumentOutOfRangeException(nameof(x));
            return y * Width + x;
        }
        public bool AllowsColor(string color) => !string.IsNullOrWhiteSpace(color) && (colors.Count == 0 || colors.Contains(color));
        public bool IsCompatible(BoardDefinition other) => other != null && BoardId == other.BoardId &&
            DefinitionVersion == other.DefinitionVersion && CellSize == other.CellSize && Unit == other.Unit && colors.SetEquals(other.colors);
    }
}
