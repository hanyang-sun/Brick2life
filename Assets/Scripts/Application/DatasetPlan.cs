using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Brick2Life.Core;

namespace Brick2Life.Application
{
    [Flags]
    public enum DatasetLayoutFamilies
    {
        Empty = 1, Center = 2, Edge = 4, Corner = 8, Line = 16,
        Cluster = 32, Checkerboard = 64, Sparse = 128, Medium = 256, Dense = 512,
        All = 1023
    }

    public sealed class DatasetLayout
    {
        public DatasetLayoutFamilies Family { get; }
        public string GroupId { get; }
        public BoardSnapshot Snapshot { get; }
        internal DatasetLayout(DatasetLayoutFamilies family, string groupId, BoardSnapshot snapshot)
        { Family = family; GroupId = groupId; Snapshot = snapshot; }
    }

    // Experimental policy lives outside the domain contracts and does not use Unity's global random state.
    public sealed class DatasetPlan
    {
        public const string GeneratorVersion = "daily-v1";
        public const int DefaultLayoutCount = 20;
        public const int DefaultSeed = 20261020;
        public int Seed { get; }
        public int LightingCount { get; }
        public int ImageCount => Layouts.Count * LightingCount;
        public IReadOnlyList<DatasetLayout> Layouts { get; }

        public static void Validate(int layoutCount, DatasetLayoutFamilies families, int lightingCount)
        {
            if (layoutCount < 1 || layoutCount > 200) throw new ArgumentOutOfRangeException(nameof(layoutCount), "Use 1–200 layouts (at most 1000 images).");
            if (families == 0 || (families & ~DatasetLayoutFamilies.All) != 0) throw new ArgumentException("Select at least one supported layout family.", nameof(families));
            if (lightingCount != 1 && lightingCount != 5) throw new ArgumentOutOfRangeException(nameof(lightingCount), "Select baseline only (1) or daily lighting (5).");
            if (families == DatasetLayoutFamilies.Empty && layoutCount != 1) throw new ArgumentException("Only one distinct empty board exists.", nameof(layoutCount));
        }

        public DatasetPlan(BoardDefinition definition, int layoutCount = DefaultLayoutCount, int seed = DefaultSeed,
            DatasetLayoutFamilies families = DatasetLayoutFamilies.All, int lightingCount = 5)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Validate(layoutCount, families, lightingCount);
            if (definition.ColorIds.Count == 0) throw new ArgumentException("Dataset generation requires an explicit palette.", nameof(definition));
            Seed = seed; LightingCount = lightingCount;
            var available = new List<DatasetLayoutFamilies>();
            foreach (DatasetLayoutFamilies family in Enum.GetValues(typeof(DatasetLayoutFamilies)))
                if (family != DatasetLayoutFamilies.All && (families & family) != 0) available.Add(family);
            var random = new StableRandom(seed);
            var layouts = new List<DatasetLayout>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int cursor = 0;
            // Bounded retries also handle palettes/families with too few distinct layouts.
            for (int attempt = 0; layouts.Count < layoutCount && attempt < layoutCount * 200; attempt++)
            {
                DatasetLayoutFamilies family = available[cursor++ % available.Count];
                if (family == DatasetLayoutFamilies.Empty && layouts.Exists(item => item.Family == family)) continue;
                BoardSnapshot snapshot = Generate(definition, family, random);
                string groupId = ContentId(snapshot);
                if (seen.Add(groupId)) layouts.Add(new DatasetLayout(family, groupId, snapshot));
            }
            if (layouts.Count != layoutCount)
                throw new ArgumentException("Selected families and palette cannot produce the requested number of distinct layouts. Reduce the count or select more families.");
            Layouts = layouts.AsReadOnly();
        }

        // IDs describe logical occupancy/color only; authoring IDs, versions, camera and lighting are excluded.
        public static string ContentId(BoardSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var canonical = new StringBuilder("board-content-v1;");
            foreach (BoardCell cell in snapshot.Cells)
            {
                if (!cell.IsKnown) throw new ArgumentException("Dataset layouts must be fully known.", nameof(snapshot));
                string color = cell.ColorId ?? "";
                canonical.Append((int)cell.Occupancy).Append(':').Append(color.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(color).Append(';');
            }
            using (SHA256 hash = SHA256.Create())
                return "layout-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        private static BoardSnapshot Generate(BoardDefinition definition, DatasetLayoutFamilies family, StableRandom random)
        {
            var cells = new BoardCell[definition.CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = BoardCell.Empty;
            string Color() => definition.ColorIds[random.Next(definition.ColorIds.Count)];
            void Put(int x, int y, string color = null)
            {
                int index = definition.Index(x, y);
                cells[index] = new BoardCell(Occupancy.Occupied, color ?? Color(), "cell-" + index.ToString(CultureInfo.InvariantCulture));
            }
            switch (family)
            {
                case DatasetLayoutFamilies.Empty: break;
                case DatasetLayoutFamilies.Center: Put(4 + random.Next(2), 4 + random.Next(2)); break;
                case DatasetLayoutFamilies.Edge:
                    int edge = random.Next(4), p = 1 + random.Next(8);
                    Put(edge < 2 ? (edge == 0 ? 0 : 9) : p, edge < 2 ? p : (edge == 2 ? 0 : 9)); break;
                case DatasetLayoutFamilies.Corner: Put(random.Next(2) * 9, random.Next(2) * 9); break;
                case DatasetLayoutFamilies.Line:
                    bool horizontal = random.Next(2) == 0; int axis = random.Next(10), length = 3 + random.Next(8), start = random.Next(11 - length);
                    for (int i = start; i < start + length; i++) Put(horizontal ? i : axis, horizontal ? axis : i);
                    break;
                case DatasetLayoutFamilies.Cluster:
                    int width = 2 + random.Next(3), height = 2 + random.Next(3);
                    int left = random.Next(11 - width), bottom = random.Next(11 - height); string clusterColor = Color();
                    for (int y = bottom; y < bottom + height; y++) for (int x = left; x < left + width; x++) Put(x, y, clusterColor);
                    break;
                case DatasetLayoutFamilies.Checkerboard:
                    int parity = random.Next(2);
                    for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++) if ((x + y) % 2 == parity) Put(x, y);
                    break;
                default:
                    int count = family == DatasetLayoutFamilies.Sparse ? 3 + random.Next(10)
                        : family == DatasetLayoutFamilies.Medium ? 25 + random.Next(26) : 75 + random.Next(26);
                    var positions = new int[100]; for (int i = 0; i < 100; i++) positions[i] = i;
                    for (int i = 0; i < count; i++)
                    {
                        int j = i + random.Next(100 - i), value = positions[j]; positions[j] = positions[i]; positions[i] = value;
                        Put(value % 10, value / 10);
                    }
                    break;
            }
            return new BoardSnapshot(definition, 0, cells, SnapshotSource.Simulation);
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
