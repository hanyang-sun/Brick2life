using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brick2Life.Core
{
    public enum Occupancy { Unknown, Empty, Occupied }
    public enum SnapshotSource { Simulation, Vision, Manual }

    public sealed class BoardCell
    {
        public static readonly BoardCell Empty = new BoardCell(Occupancy.Empty);
        public static readonly BoardCell Unknown = new BoardCell(Occupancy.Unknown);
        public Occupancy Occupancy { get; }
        public string ColorId { get; }
        // Optional source identity; never used to infer visual correspondence or logical changes.
        public string BrickId { get; }

        public BoardCell(Occupancy occupancy, string colorId = null, string brickId = null)
        {
            if (!Enum.IsDefined(typeof(Occupancy), occupancy)) throw new ArgumentOutOfRangeException(nameof(occupancy));
            if (occupancy != Occupancy.Occupied && (colorId != null || brickId != null))
                throw new ArgumentException("Only occupied cells can carry color or identity.");
            if ((colorId != null && string.IsNullOrWhiteSpace(colorId)) || (brickId != null && string.IsNullOrWhiteSpace(brickId)))
                throw new ArgumentException("Optional IDs must be nonblank.");
            Occupancy = occupancy; ColorId = colorId; BrickId = brickId;
        }
        public bool SameContent(BoardCell other) => other != null && Occupancy == other.Occupancy && ColorId == other.ColorId;
        public bool IsKnown => Occupancy == Occupancy.Empty || (Occupancy == Occupancy.Occupied && ColorId != null);
    }

    public sealed class BoardSnapshot
    {
        public BoardDefinition Definition { get; }
        public long Version { get; }
        public SnapshotSource Source { get; }
        public IReadOnlyList<BoardCell> Cells { get; }

        public BoardSnapshot(BoardDefinition definition, long version, IEnumerable<BoardCell> cells, SnapshotSource source)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (!Enum.IsDefined(typeof(SnapshotSource), source)) throw new ArgumentOutOfRangeException(nameof(source));
            var owned = new List<BoardCell>(cells ?? throw new ArgumentNullException(nameof(cells)));
            if (owned.Count != definition.CellCount) throw new ArgumentException("A snapshot must contain every board cell.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BoardCell cell in owned)
            {
                if (cell == null || !cell.IsKnown || (cell.Occupancy == Occupancy.Occupied && !definition.AllowsColor(cell.ColorId)))
                    throw new ArgumentException("Snapshots require known occupancy and valid occupied colors.");
                if (cell.BrickId != null && !ids.Add(cell.BrickId)) throw new ArgumentException("Duplicate source identity.");
            }
            Version = version; Source = source; Cells = new ReadOnlyCollection<BoardCell>(owned);
        }
        public BoardCell At(int x, int y) => Cells[Definition.Index(x, y)];
    }
}
