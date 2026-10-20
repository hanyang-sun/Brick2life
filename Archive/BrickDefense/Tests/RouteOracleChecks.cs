using BrickDefense.Core;

internal static class RouteOracleChecks
{
    public static void Run()
    {
        var random = new Random(20261008);
        var rules = ExampleScans.CreateDemoRules();
        for (int sample = 0; sample < 200; sample++)
        {
            var rows = new string[BoardSize.Physical];
            for (int r = 0; r < BoardSize.Physical; r++)
            {
                var row = new char[BoardSize.Physical];
                for (int c = 0; c < row.Length; c++)
                {
                    double choice = random.NextDouble();
                    row[c] = choice < 0.05 ? 'K' : choice < 0.36 ? 'W' : choice < 0.43 ? 'T' : '.';
                }
                if (r == 4) row[4] = 'K';
                if (r == 9) row[0] = '.';
                rows[r] = new string(row);
            }

            var grid = GridMapper.Map(ExampleScans.Parse($"oracle-{sample}", rows));
            var emptyCells = grid.NavCells.Where(cell => cell.SourceBrickId == null).ToArray();
            GridPoint spawn = emptyCells[random.Next(emptyCells.Length)].Position;
            var level = LevelBuilder.Build(grid, rules, spawn);
            var expected = FindWithBreadthFirstSearch(level.Cells, level.CoreBrickIds, spawn);
            if (expected.Distance < 0)
            {
                if (level.Status != ResultStatus.Rejected ||
                    !level.Issues.Any(issue => issue.Code == "NO_ROUTE"))
                    throw new Exception($"Sample {sample}: unreachable level was accepted.");
                continue;
            }

            if (level.Status != ResultStatus.Success || level.Route.Length - 1 != expected.Distance ||
                level.TargetCoreBrickId != expected.Target)
                throw new Exception($"Sample {sample}: A* differs from independent BFS.");
            if (!level.Route[0].Equals(spawn))
                throw new Exception($"Sample {sample}: route does not start at spawn.");
            for (int i = 1; i < level.Route.Length; i++)
            {
                GridPoint previous = level.Route[i - 1];
                GridPoint current = level.Route[i];
                LevelCell cell = level.Cells[current.Y * BoardSize.Navigation + current.X];
                if (Math.Abs(current.X - previous.X) + Math.Abs(current.Y - previous.Y) != 1 ||
                    cell.Role == BrickRole.Wall || cell.Role == BrickRole.Tower ||
                    (cell.Role == BrickRole.Core && i != level.Route.Length - 1))
                    throw new Exception($"Sample {sample}: invalid route step.");
            }
            GridPoint last = level.Route[^1];
            if (level.Cells[last.Y * BoardSize.Navigation + last.X].SourceBrickId != expected.Target)
                throw new Exception($"Sample {sample}: route ends at wrong core.");
        }
        Console.WriteLine("200 layouts matched independent BFS distance, core selection and route checks.");
    }

    // Deliberately uses FIFO exploration without a heuristic or the production heap.
    private static (int Distance, string Target) FindWithBreadthFirstSearch(LevelCell[] cells,
        string[] orderedCoreIds, GridPoint spawn)
    {
        int width = BoardSize.Navigation;
        var distances = Enumerable.Repeat(-1, cells.Length).ToArray();
        var queue = new Queue<int>();
        int start = spawn.Y * width + spawn.X;
        distances[start] = 0;
        queue.Enqueue(start);
        int bestDistance = int.MaxValue;
        int bestRank = int.MaxValue;
        string target = null;
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (distances[current] > bestDistance) break;
            if (cells[current].Role == BrickRole.Core)
            {
                int rank = Array.IndexOf(orderedCoreIds, cells[current].SourceBrickId);
                if (rank < bestRank)
                {
                    bestRank = rank;
                    bestDistance = distances[current];
                    target = cells[current].SourceBrickId;
                }
                continue;
            }

            int x = current % width;
            int y = current / width;
            foreach (var neighbor in new[]
            {
                new GridPoint(x - 1, y), new GridPoint(x + 1, y),
                new GridPoint(x, y - 1), new GridPoint(x, y + 1)
            })
            {
                if (neighbor.X < 0 || neighbor.X >= width || neighbor.Y < 0 || neighbor.Y >= width)
                    continue;
                int index = neighbor.Y * width + neighbor.X;
                if (distances[index] >= 0 || cells[index].Role == BrickRole.Wall ||
                    cells[index].Role == BrickRole.Tower)
                    continue;
                distances[index] = distances[current] + 1;
                queue.Enqueue(index);
            }
        }
        return target == null ? (-1, null) : (bestDistance, target);
    }
}
