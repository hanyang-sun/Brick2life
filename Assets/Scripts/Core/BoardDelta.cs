using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brick2Life.Core
{
    public sealed class CellChange
    {
        public GridPoint Position { get; }
        public BoardCell Before { get; }
        public BoardCell After { get; }
        internal CellChange(GridPoint position, BoardCell before, BoardCell after)
        { Position = position; Before = before; After = after; }
    }

    public sealed class BoardDelta
    {
        public string BoardId { get; }
        public long BaseVersion { get; }
        public long TargetVersion { get; }
        public IReadOnlyList<CellChange> Changes { get; }
        public bool IsEmpty => Changes.Count == 0;
        public bool HasAmbiguousMovement { get; }

        private BoardDelta(BoardSnapshot before, BoardSnapshot after, List<CellChange> changes)
        {
            BoardId = before.Definition.BoardId; BaseVersion = before.Version; TargetVersion = after.Version;
            Changes = new ReadOnlyCollection<CellChange>(changes);
            var removed = new Dictionary<string, int>(StringComparer.Ordinal);
            var added = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CellChange change in changes)
            {
                if (change.Before.Occupancy == Occupancy.Occupied) Increment(removed, change.Before.ColorId);
                if (change.After.Occupancy == Occupancy.Occupied) Increment(added, change.After.ColorId);
            }
            foreach (var item in removed)
                if (added.TryGetValue(item.Key, out int count) && (item.Value > 1 || count > 1)) HasAmbiguousMovement = true;
        }
        private static void Increment(Dictionary<string, int> counts, string color)
        { counts.TryGetValue(color, out int count); counts[color] = count + 1; }

        public static BoardDelta Between(BoardSnapshot before, BoardSnapshot after)
        {
            if (before == null || after == null) throw new ArgumentNullException(nameof(before));
            if (!before.Definition.IsCompatible(after.Definition)) throw new ArgumentException("Different board definitions are not comparable.");
            if (after.Version < before.Version) throw new ArgumentException("State versions must not go backwards.");
            var changes = new List<CellChange>();
            for (int i = 0; i < before.Cells.Count; i++)
                if (!before.Cells[i].SameContent(after.Cells[i]))
                    changes.Add(new CellChange(new GridPoint(i % before.Definition.Width, i / before.Definition.Width), before.Cells[i], after.Cells[i]));
            if (changes.Count > 0 && after.Version == before.Version) throw new ArgumentException("Changed state requires a new version.");
            return new BoardDelta(before, after, changes);
        }
    }
}
