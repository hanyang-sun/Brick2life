using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brick2Life.Core
{
    // Mutable authoring model. Consumers crossing modules receive Snapshot(), never this live view.
    public sealed class BoardLayout
    {
        public const int Size = BoardDefinition.Size;
        public sealed class Brick
        {
            public string BrickId { get; }
            public string ColorId { get; }
            public int GridX { get; }
            public int GridY { get; }
            public int Layer => 0;
            internal Brick(string id, string color, int x, int y)
            { BrickId = id; ColorId = color; GridX = x; GridY = y; }
        }
        private readonly Brick[,] cells = new Brick[Size, Size];
        private readonly List<Brick> bricks = new List<Brick>();
        private readonly ReadOnlyCollection<Brick> readOnlyBricks;
        public BoardDefinition Definition { get; }
        public IReadOnlyList<Brick> Bricks => readOnlyBricks;
        public int Version { get; private set; }

        public BoardLayout(BoardDefinition definition = null)
        { Definition = definition ?? new BoardDefinition(); readOnlyBricks = bricks.AsReadOnly(); }

        public BoardLayout(BoardSnapshot snapshot) : this(snapshot?.Definition ?? throw new ArgumentNullException(nameof(snapshot)))
        {
            if (snapshot.Version > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(snapshot));
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    BoardCell state = snapshot.At(x, y);
                    if (state.Occupancy != Occupancy.Occupied) continue;
                    var brick = new Brick(state.BrickId ?? Guid.NewGuid().ToString("N"), state.ColorId, x, y);
                    cells[x, y] = brick; bricks.Add(brick);
                }
            Version = (int)snapshot.Version;
        }

        public static bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public Brick At(int x, int y) => Contains(x, y) ? cells[x, y] : null;
        public bool CanPlace(int x, int y, string movingId = null) => Contains(x, y) &&
            (cells[x, y] == null || cells[x, y].BrickId == movingId);
        public bool TryAdd(int x, int y, string colorId, out Brick brick)
        {
            brick = null;
            if (!Definition.AllowsColor(colorId) || !CanPlace(x, y)) return false;
            brick = new Brick(Guid.NewGuid().ToString("N"), colorId, x, y);
            cells[x, y] = brick; bricks.Add(brick); Version++;
            return true;
        }
        public bool TryDelete(string id)
        {
            int index = bricks.FindIndex(brick => brick.BrickId == id);
            if (index < 0) return false;
            Brick current = bricks[index];
            cells[current.GridX, current.GridY] = null; bricks.RemoveAt(index); Version++;
            return true;
        }
        public bool TryMove(string id, int x, int y)
        {
            int index = bricks.FindIndex(brick => brick.BrickId == id);
            if (index < 0 || !CanPlace(x, y, id)) return false;
            Brick current = bricks[index];
            if (current.GridX == x && current.GridY == y) return true;
            var moved = new Brick(current.BrickId, current.ColorId, x, y);
            cells[current.GridX, current.GridY] = null; cells[x, y] = moved; bricks[index] = moved; Version++;
            return true;
        }
        public BoardSnapshot Snapshot()
        {
            var frozen = new BoardCell[Definition.CellCount];
            for (int i = 0; i < frozen.Length; i++) frozen[i] = BoardCell.Empty;
            foreach (Brick brick in bricks)
                frozen[Definition.Index(brick.GridX, brick.GridY)] = new BoardCell(Occupancy.Occupied, brick.ColorId, brick.BrickId);
            return new BoardSnapshot(Definition, Version, frozen, SnapshotSource.Simulation);
        }
    }
}
