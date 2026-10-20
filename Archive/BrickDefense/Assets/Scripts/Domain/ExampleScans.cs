using System;
using System.Collections.Generic;

namespace BrickDefense.Core
{
    // These IDs stand for sample symbols, not measured physical brick colors.
    public static class ExampleScans
    {
        public const string CoreColor = "demo-core";
        public const string WallColor = "demo-wall";
        public const string TowerColor = "demo-tower";

        public static ScanResult CreateReference()
        {
            return Parse("reference", new[]
            {
                "..........",
                "..........",
                "...WWW.K..",
                "...W......",
                "...W......",
                "...W.T....",
                "..........",
                ".......K..",
                "..........",
                ".........."
            });
        }

        public static ScanResult CreateBlockedCore()
        {
            return Parse("blocked-core", new[]
            {
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                ".......W..",
                "......WKW.",
                ".......W..",
                ".........."
            });
        }

        // 文本中的 K/W/T 是示例角色符号；转换后仍走照片同一条关卡管线。
        public static ScanResult Parse(string scanId, string[] rows)
        {
            if (rows == null || rows.Length != BoardSize.Physical)
                throw new ArgumentException("Example layout must have ten rows.", nameof(rows));

            var cells = new PhysicalCellObservation[BoardSize.Physical * BoardSize.Physical];
            var bricks = new List<BrickObservation>();
            for (int r = 0; r < BoardSize.Physical; r++)
            {
                if (rows[r] == null || rows[r].Length != BoardSize.Physical)
                    throw new ArgumentException("Every example row must have ten cells.", nameof(rows));

                for (int c = 0; c < BoardSize.Physical; c++)
                {
                    string colorId;
                    switch (rows[r][c])
                    {
                        case '.':
                            cells[r * BoardSize.Physical + c] =
                                new PhysicalCellObservation(c, r, PhysicalCellState.Empty);
                            continue;
                        case 'K': colorId = CoreColor; break;
                        case 'W': colorId = WallColor; break;
                        case 'T': colorId = TowerColor; break;
                        default: throw new ArgumentException("Unknown example symbol.", nameof(rows));
                    }

                    string brickId = $"brick-{r}-{c}";
                    cells[r * BoardSize.Physical + c] =
                        new PhysicalCellObservation(c, r, PhysicalCellState.Occupied, brickId);
                    bricks.Add(new BrickObservation(brickId, c, r, colorId));
                }
            }

            return new ScanResult(scanId, ResultStatus.Success, cells, bricks.ToArray(), Array.Empty<Issue>());
        }

        public static LevelRules CreateDemoRules()
        {
            return new LevelRules(new Dictionary<string, BrickRole>
            {
                { CoreColor, BrickRole.Core },
                { WallColor, BrickRole.Wall },
                { TowerColor, BrickRole.Tower }
            });
        }
    }
}
