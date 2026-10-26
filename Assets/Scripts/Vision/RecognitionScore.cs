using System;
using System.Collections.Generic;
using Brick2Life.Core;

namespace Brick2Life.Vision
{
    // Evaluation only. Callers must not feed the score or the truth snapshot back into recognition.
    public sealed class RecognitionScore
    {
        public int Total { get; }
        public int ExactMatches { get; }
        public int OccupancyMatches { get; }
        public int MismatchCount { get; }
        public IReadOnlyList<string> Mismatches { get; }

        private RecognitionScore(int total, int exact, int occupancy, int mismatchCount, IReadOnlyList<string> mismatches)
        {
            Total = total; ExactMatches = exact; OccupancyMatches = occupancy; MismatchCount = mismatchCount; Mismatches = mismatches;
        }

        public static RecognitionScore Compare(FrameObservation observation, BoardSnapshot truth)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            if (truth == null) throw new ArgumentNullException(nameof(truth));
            if (!observation.Definition.IsCompatible(truth.Definition)) throw new ArgumentException("Board definitions differ.");
            if (!observation.Accepted)
                return new RecognitionScore(truth.Definition.CellCount, 0, 0, 1, new[] { "帧被拒绝：" + observation.RejectionReason });
            var predicted = new BoardCell[observation.Cells.Count];
            for (int i = 0; i < predicted.Length; i++) predicted[i] = observation.Cells[i].State;
            return CompareCells(observation.Definition, predicted, truth);
        }

        public static RecognitionScore CompareCells(BoardDefinition definition, IReadOnlyList<BoardCell> predicted, BoardSnapshot truth)
        {
            if (definition == null || predicted == null || truth == null) throw new ArgumentNullException(nameof(predicted));
            if (!definition.IsCompatible(truth.Definition) || predicted.Count != truth.Cells.Count)
                throw new ArgumentException("Prediction and truth are not the same board.");
            int exact = 0, occupancy = 0;
            var mismatches = new List<string>();
            for (int i = 0; i < predicted.Count; i++)
            {
                BoardCell actual = truth.Cells[i];
                BoardCell guess = predicted[i];
                bool sameOccupancy = guess.Occupancy == actual.Occupancy;
                if (sameOccupancy) occupancy++;
                if (guess.SameContent(actual)) exact++;
                else if (mismatches.Count < 12)
                    mismatches.Add("(" + (i % definition.Width + 1) + "," + (i / definition.Width + 1) + ") 真值=" + Name(actual) + " 预测=" + Name(guess));
            }
            return new RecognitionScore(predicted.Count, exact, occupancy, predicted.Count - exact, mismatches);
        }

        private static string Name(BoardCell cell)
        {
            if (cell.Occupancy == Occupancy.Empty) return "空";
            if (cell.Occupancy != Occupancy.Occupied || cell.ColorId == null) return "未知";
            switch (cell.ColorId)
            {
                case "red": return "红";
                case "green": return "绿";
                case "blue": return "蓝";
                case "yellow": return "黄";
                default: return cell.ColorId;
            }
        }
    }
}
