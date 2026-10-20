using System;
using System.Collections.Generic;
using Roy_T.AStar.Collections;

namespace BrickDefense.Core
{
    // Heap-driven A* follows the researched Roy-T.AStar approach; multi-core rules are project adapters.
    // See docs/research/existing-milestone-github.md and THIRD_PARTY_NOTICES.md.
    internal static class GridPathfinder
    {
        private const int Width = BoardSize.Navigation;
        private const int CellCount = BoardSize.NavigationCellCount;

        public static NavigationPath Find(LevelCell[] cells, string[] orderedCoreIds, GridPoint spawn)
        {
            var goals = new List<GridPoint>();
            var coreRank = new Dictionary<string, int>();
            for (int i = 0; i < orderedCoreIds.Length; i++)
                coreRank.Add(orderedCoreIds[i], i);
            foreach (LevelCell cell in cells)
                if (cell.Role == BrickRole.Core)
                    goals.Add(cell.Position);

            // 四方向移动每步代价为 1，曼哈顿距离不会高估到最近核心的距离。
            var heuristic = new int[CellCount];
            var cost = new int[CellCount];
            var previous = new int[CellCount];
            var closed = new bool[CellCount];
            for (int i = 0; i < CellCount; i++)
            {
                heuristic[i] = DistanceToNearestGoal(cells[i].Position, goals);
                cost[i] = int.MaxValue;
                previous[i] = -1;
            }

            int start = spawn.Y * Width + spawn.X;
            var open = new MinHeap<FrontierEntry>();
            cost[start] = 0;
            open.Insert(new FrontierEntry(start, 0, heuristic[start]));

            int bestGoal = -1;
            int bestDistance = int.MaxValue;
            while (open.Count > 0)
            {
                FrontierEntry entry = open.Extract();
                if (entry.Score > bestDistance)
                    break;
                int current = entry.Index;
                // Entries are immutable. A later improvement leaves an older entry to skip here.
                if (closed[current] || entry.Cost != cost[current])
                    continue;
                closed[current] = true;

                if (cells[current].Role == BrickRole.Core)
                {
                    if (bestGoal < 0 || entry.Cost < bestDistance ||
                        (entry.Cost == bestDistance && EarlierGoal(current, bestGoal, cells, coreRank)))
                    {
                        bestGoal = current;
                        bestDistance = entry.Cost;
                    }
                    // Finish all candidates with f <= d so every equally close core can be compared.
                    continue;
                }

                int x = current % Width;
                int y = current / Width;
                if (y > 0) Visit(current - Width, current, cells, cost, heuristic, previous, closed, open);
                if (x > 0) Visit(current - 1, current, cells, cost, heuristic, previous, closed, open);
                if (x < Width - 1) Visit(current + 1, current, cells, cost, heuristic, previous, closed, open);
                if (y < Width - 1) Visit(current + Width, current, cells, cost, heuristic, previous, closed, open);
            }

            if (bestGoal < 0)
                return null;
            // previous 从目标回溯到出生点，翻转后得到用于绘制的前进路线。
            var route = new List<GridPoint>();
            for (int cursor = bestGoal; cursor >= 0; cursor = previous[cursor])
                route.Add(cells[cursor].Position);
            route.Reverse();
            return new NavigationPath(cells[bestGoal].SourceBrickId, route.ToArray());
        }

        private static bool EarlierGoal(int candidate, int selected, LevelCell[] cells,
            Dictionary<string, int> coreRank)
        {
            int candidateRank = coreRank[cells[candidate].SourceBrickId];
            int selectedRank = coreRank[cells[selected].SourceBrickId];
            return candidateRank < selectedRank || (candidateRank == selectedRank && candidate < selected);
        }

        private static void Visit(int next, int current, LevelCell[] cells, int[] cost,
            int[] heuristic, int[] previous, bool[] closed, MinHeap<FrontierEntry> open)
        {
            if (closed[next] || cells[next].Role == BrickRole.Wall || cells[next].Role == BrickRole.Tower)
                return;
            int nextCost = cost[current] + 1;
            if (nextCost >= cost[next])
                return;
            cost[next] = nextCost;
            previous[next] = current;
            open.Insert(new FrontierEntry(next, nextCost, nextCost + heuristic[next]));
        }

        private static int DistanceToNearestGoal(GridPoint point, List<GridPoint> goals)
        {
            int nearest = int.MaxValue;
            foreach (GridPoint goal in goals)
            {
                int distance = Math.Abs(point.X - goal.X) + Math.Abs(point.Y - goal.Y);
                if (distance < nearest)
                    nearest = distance;
            }
            return nearest;
        }

        private readonly struct FrontierEntry : IComparable<FrontierEntry>
        {
            public int Index { get; }
            public int Cost { get; }
            public int Score { get; }

            public FrontierEntry(int index, int cost, int score)
            {
                Index = index;
                Cost = cost;
                Score = score;
            }

            public int CompareTo(FrontierEntry other)
            {
                int byScore = Score.CompareTo(other.Score);
                if (byScore != 0) return byScore;
                int byCost = Cost.CompareTo(other.Cost);
                return byCost != 0 ? byCost : Index.CompareTo(other.Index);
            }
        }
    }

    internal sealed class NavigationPath
    {
        public string TargetBrickId { get; }
        public GridPoint[] Route { get; }

        public NavigationPath(string targetBrickId, GridPoint[] route)
        {
            TargetBrickId = targetBrickId;
            Route = route;
        }
    }
}
