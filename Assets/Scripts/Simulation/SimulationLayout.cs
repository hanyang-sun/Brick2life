using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brick2Life.Simulation
{
    // 仿真真实状态；不包含照片证据、识别置信度或游戏角色。
    public sealed class SimulationLayout
    {
        public const int Size = 10;

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
        public IReadOnlyList<Brick> Bricks => readOnlyBricks;
        public int Version { get; private set; }

        public SimulationLayout() => readOnlyBricks = bricks.AsReadOnly();

        public static bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public Brick At(int x, int y) => Contains(x, y) ? cells[x, y] : null;

        public bool CanPlace(int x, int y, string movingId = null) => Contains(x, y) &&
            (cells[x, y] == null || cells[x, y].BrickId == movingId);

        public bool TryAdd(int x, int y, string colorId, out Brick brick)
        {
            brick = null;
            if (string.IsNullOrWhiteSpace(colorId) || !CanPlace(x, y)) return false;
            brick = new Brick(Guid.NewGuid().ToString("N"), colorId, x, y);
            cells[x, y] = brick;
            bricks.Add(brick);
            Version++;
            return true;
        }

        public bool TryDelete(string id)
        {
            int index = bricks.FindIndex(brick => brick.BrickId == id);
            if (index < 0) return false;
            Brick current = bricks[index];
            cells[current.GridX, current.GridY] = null;
            bricks.RemoveAt(index);
            Version++;
            return true;
        }

        public bool TryMove(string id, int x, int y)
        {
            int index = bricks.FindIndex(brick => brick.BrickId == id);
            if (index < 0 || !CanPlace(x, y, id)) return false;
            Brick current = bricks[index];
            if (current.GridX == x && current.GridY == y) return true;
            var moved = new Brick(current.BrickId, current.ColorId, x, y);
            cells[current.GridX, current.GridY] = null;
            cells[x, y] = moved;
            bricks[index] = moved;
            Version++;
            return true;
        }
    }
}
