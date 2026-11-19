using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Brick2Life.Simulation")]

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
            public int Layer { get; }
            internal Brick(string id, string color, int x, int y, int layer)
            { BrickId = id; ColorId = color; GridX = x; GridY = y; Layer = layer; }
        }
        private readonly List<Brick>[,] columns = new List<Brick>[Size, Size];
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
                    var brick = new Brick(state.BrickId ?? Guid.NewGuid().ToString("N"), state.ColorId, x, y, 0);
                    AppendToColumn(brick);
                    bricks.Add(brick);
                }
            Version = (int)snapshot.Version;
        }

        public static bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public Brick At(int x, int y) => AtLayer(x, y, 0);
        public bool CanPlace(int x, int y, string movingId = null) => CanPlaceAtLayer(x, y, 0, movingId);
        public bool TryAdd(int x, int y, string colorId, out Brick brick) => TryAddAtLayer(x, y, 0, colorId, out brick);

        internal int StackHeight(int x, int y) => Contains(x, y) ? columns[x, y]?.Count ?? 0 : 0;
        internal Brick AtLayer(int x, int y, int layer) => layer >= 0 && layer < StackHeight(x, y) ? columns[x, y][layer] : null;

        internal bool CanPlaceAtLayer(int x, int y, int layer, string movingId = null)
        {
            if (!Contains(x, y) || layer < 0) return false;
            int height = StackHeight(x, y);
            if (movingId != null && AtLayer(x, y, height - 1)?.BrickId == movingId) return layer == height - 1;
            return layer == height;
        }

        private void AppendToColumn(Brick brick)
        {
            var column = columns[brick.GridX, brick.GridY] ??= new List<Brick>();
            column.Add(brick);
        }

        internal bool TryAddAtLayer(int x, int y, int layer, string colorId, out Brick brick)
        {
            brick = null;
            if (!Definition.AllowsColor(colorId) || !CanPlaceAtLayer(x, y, layer)) return false;
            brick = new Brick(Guid.NewGuid().ToString("N"), colorId, x, y, layer);
            AppendToColumn(brick);
            bricks.Add(brick);
            Version++;
            return true;
        }
        public bool TryDelete(string id)
        {
            int index = bricks.FindIndex(brick => brick.BrickId == id);
            if (index < 0) return false;
            Brick current = bricks[index];
            if (current.Layer != StackHeight(current.GridX, current.GridY) - 1) return false;
            columns[current.GridX, current.GridY].RemoveAt(current.Layer);
            bricks.RemoveAt(index);
            Version++;
            return true;
        }
        public bool TryMove(string id, int x, int y) => TryMoveToLayer(id, x, y, 0);

        internal bool TryMoveToLayer(string id, int x, int y, int layer)
        {
            int index = bricks.FindIndex(brick => brick.BrickId == id);
            if (index < 0 || !CanPlaceAtLayer(x, y, layer, id)) return false;
            Brick current = bricks[index];
            if (current.Layer != StackHeight(current.GridX, current.GridY) - 1) return false;
            if (current.GridX == x && current.GridY == y) return current.Layer == layer;
            var moved = new Brick(current.BrickId, current.ColorId, x, y, layer);
            columns[current.GridX, current.GridY].RemoveAt(current.Layer);
            AppendToColumn(moved);
            if (layer == 0) bricks[index] = moved;
            else
            {
                // Reverse iteration must remove a brick before its support.
                bricks.RemoveAt(index);
                bricks.Add(moved);
            }
            Version++;
            return true;
        }
        public BoardSnapshot Snapshot()
        {
            var frozen = new BoardCell[Definition.CellCount];
            for (int i = 0; i < frozen.Length; i++) frozen[i] = BoardCell.Empty;
            foreach (Brick brick in bricks)
                if (brick.Layer == 0)
                    frozen[Definition.Index(brick.GridX, brick.GridY)] = new BoardCell(Occupancy.Occupied, brick.ColorId, brick.BrickId);
            return new BoardSnapshot(Definition, Version, frozen, SnapshotSource.Simulation);
        }
    }
}
