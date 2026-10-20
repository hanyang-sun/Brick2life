using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brick2Life.Core
{
    public sealed class ObservedCell
    {
        public BoardCell State { get; }
        // Optional algorithm score, not a calibrated probability of correctness.
        public double? Confidence { get; }
        public string Reason { get; }
        public ObservedCell(BoardCell state, double? confidence = null, string reason = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            if (confidence.HasValue && (double.IsNaN(confidence.Value) || confidence < 0 || confidence > 1))
                throw new ArgumentOutOfRangeException(nameof(confidence));
            Confidence = confidence; Reason = reason;
        }
    }

    public sealed class FrameObservation
    {
        public BoardDefinition Definition { get; }
        public string FrameId { get; }
        public string SessionId { get; }
        public long Sequence { get; }
        public DateTimeOffset CapturedAtUtc { get; }
        public string CalibrationId { get; }
        public bool Accepted { get; }
        public string RejectionReason { get; }
        public IReadOnlyList<ObservedCell> Cells { get; }

        public FrameObservation(BoardDefinition definition, string frameId, string sessionId, long sequence,
            DateTimeOffset capturedAtUtc, string calibrationId, IEnumerable<ObservedCell> cells,
            bool accepted = true, string rejectionReason = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(frameId) || string.IsNullOrWhiteSpace(sessionId) || sequence < 0)
                throw new ArgumentException("Frame/session identity and nonnegative sequence are required.");
            if (accepted && string.IsNullOrWhiteSpace(calibrationId)) throw new ArgumentException("Accepted observations require calibration.");
            if (!accepted && string.IsNullOrWhiteSpace(rejectionReason)) throw new ArgumentException("Rejected observations require a reason.");
            var owned = new List<ObservedCell>(cells ?? Array.Empty<ObservedCell>());
            if (accepted && owned.Count != definition.CellCount) throw new ArgumentException("Observations must cover every cell, including unknowns.");
            foreach (ObservedCell cell in owned)
                if (cell == null || (cell.State.ColorId != null && !definition.AllowsColor(cell.State.ColorId)))
                    throw new ArgumentException("Invalid observed cell or color.");
            FrameId = frameId; SessionId = sessionId; Sequence = sequence; CapturedAtUtc = capturedAtUtc.ToUniversalTime();
            CalibrationId = calibrationId; Accepted = accepted; RejectionReason = rejectionReason;
            Cells = new ReadOnlyCollection<ObservedCell>(owned);
        }
    }
}
