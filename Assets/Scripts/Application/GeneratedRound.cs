using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Brick2Life.Core;

namespace Brick2Life.Application
{
    /// <summary>Public system goals for one round, independent of the predicted player brick layout.</summary>
    public sealed class GeneratedRound
    {
        public const string GeneratorVersion = "generated-round-v1";
        public BoardDefinition Definition { get; }
        public int Seed { get; }
        public GridPoint Core { get; }
        public IReadOnlyList<GridPoint> Spawns { get; }
        private readonly HashSet<GridPoint> reserved;

        private GeneratedRound(BoardDefinition definition, int seed, GridPoint core, List<GridPoint> spawns)
        {
            Definition = definition; Seed = seed; Core = core;
            Spawns = new ReadOnlyCollection<GridPoint>(new List<GridPoint>(spawns));
            reserved = new HashSet<GridPoint>(spawns) { core };
        }

        public static GeneratedRound Generate(BoardDefinition definition, int seed, int spawnCount)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.Width < 3 || definition.Height < 3)
                throw new ArgumentException("A generated round needs an interior core cell.", nameof(definition));
            int perimeter = 2 * definition.Width + 2 * definition.Height - 4;
            if (spawnCount < 1 || spawnCount > perimeter)
                throw new ArgumentOutOfRangeException(nameof(spawnCount), "Use between one and the number of distinct edge cells.");

            // Reuses DatasetPlan's own xorshift32 sequence and partial Fisher-Yates selection.
            var random = new StableRandom(seed);
            var core = new GridPoint(1 + random.Next(definition.Width - 2), 1 + random.Next(definition.Height - 2));
            var edges = new List<GridPoint>(perimeter);
            for (int y = 0; y < definition.Height; y++)
                for (int x = 0; x < definition.Width; x++)
                    if (x == 0 || y == 0 || x == definition.Width - 1 || y == definition.Height - 1)
                        edges.Add(new GridPoint(x, y));
            var spawns = new List<GridPoint>(spawnCount);
            for (int i = 0; i < spawnCount; i++)
            {
                int selected = i + random.Next(edges.Count - i);
                GridPoint cell = edges[selected]; edges[selected] = edges[i]; edges[i] = cell;
                spawns.Add(cell);
            }
            return new GeneratedRound(definition, seed, core, spawns);
        }

        public bool IsReserved(int x, int y) => Definition.Contains(x, y) && reserved.Contains(new GridPoint(x, y));

        /// <summary>
        /// Empty-player reference for the system's initial routes. This is configuration, not a visual
        /// prediction: never submit it to B, BoardSyncSession, or recognition accuracy evaluation.
        /// </summary>
        public LevelState CreateReferenceLevel(LevelRuleSet rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            var empty = new BoardCell[Definition.CellCount];
            for (int i = 0; i < empty.Length; i++) empty[i] = BoardCell.Empty;
            return LevelState.FromSnapshot(new BoardSnapshot(Definition, 0, empty, SnapshotSource.Simulation), rules, this);
        }

        private sealed class StableRandom
        {
            private uint state;
            public StableRandom(int seed) { state = unchecked((uint)seed) ^ 0x9e3779b9u; if (state == 0) state = 1; }
            public int Next(int exclusiveMax)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (int)(state % (uint)exclusiveMax);
            }
        }
    }
}
