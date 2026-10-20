using System;
using System.Collections.Generic;
using Brick2Life.Core;

namespace Brick2Life.Application
{
    public enum UpdateOutcome { Baseline, Changed, Unchanged, Rejected, Stale }

    public sealed class ObservationRequest
    {
        public BoardDefinition Definition { get; }
        public string SessionId { get; }
        public string FrameId { get; }
        public long Sequence { get; }
        public string CalibrationId { get; }
        public DateTimeOffset CapturedAtUtc { get; }
        internal ObservationRequest(BoardDefinition definition, string sessionId, string frameId, long sequence,
            string calibrationId, DateTimeOffset capturedAtUtc)
        {
            Definition = definition; SessionId = sessionId; FrameId = frameId; Sequence = sequence;
            CalibrationId = calibrationId; CapturedAtUtc = capturedAtUtc.ToUniversalTime();
        }
    }

    public sealed class BoardUpdate
    {
        public UpdateOutcome Outcome { get; }
        public BoardSnapshot Snapshot { get; }
        // First accepted observation establishes a baseline: Delta is null, not an invented empty board.
        public BoardDelta Delta { get; }
        public string Reason { get; }
        internal BoardUpdate(UpdateOutcome outcome, BoardSnapshot snapshot, BoardDelta delta = null, string reason = null)
        { Outcome = outcome; Snapshot = snapshot; Delta = delta; Reason = reason; }
    }

    // Single-threaded owner of the confirmed visual state. Marshal callbacks to its owning thread.
    // It accepts observations only: no simulation layouts, Unity objects or GT input.
    public sealed class BoardSyncSession
    {
        private readonly HashSet<string> frameIds = new HashSet<string>(StringComparer.Ordinal);
        private ObservationRequest current;
        private long sequence;
        public BoardDefinition Definition { get; }
        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public string CalibrationId { get; private set; }
        public BoardSnapshot Confirmed { get; private set; }
        public FrameObservation LastConfirmedObservation { get; private set; }

        public BoardSyncSession(BoardDefinition definition, string calibrationId)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(calibrationId)) throw new ArgumentException("Calibration ID is required.");
            CalibrationId = calibrationId;
        }

        public ObservationRequest BeginFrame(string frameId, DateTimeOffset capturedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(frameId) || !frameIds.Add(frameId)) throw new ArgumentException("Frame ID must be unique within the session.");
            current = new ObservationRequest(Definition, SessionId, frameId, ++sequence, CalibrationId, capturedAtUtc);
            return current;
        }

        public void ChangeCalibration(string calibrationId)
        {
            if (string.IsNullOrWhiteSpace(calibrationId)) throw new ArgumentException("Calibration ID is required.");
            if (CalibrationId == calibrationId) return;
            CalibrationId = calibrationId;
            Cancel();
        }
        public void Cancel() { current = null; }

        public BoardUpdate Fail(ObservationRequest request, string reason)
        {
            if (request == null || !ReferenceEquals(current, request)) return Stale();
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Failure reason is required.");
            current = null;
            return new BoardUpdate(UpdateOutcome.Rejected, Confirmed, reason: reason);
        }

        public BoardUpdate Confirm(ObservationRequest request, FrameObservation observation)
        {
            if (request == null || !ReferenceEquals(current, request)) return Stale();
            if (observation == null) return Fail(request, "No visual observation was returned.");
            if (observation.FrameId != request.FrameId || observation.SessionId != request.SessionId ||
                observation.Sequence != request.Sequence || observation.CalibrationId != request.CalibrationId ||
                observation.CapturedAtUtc != request.CapturedAtUtc || !Definition.IsCompatible(observation.Definition))
                return Fail(request, "Observation does not match the frame, board, session or calibration.");
            if (!observation.Accepted) return Fail(request, observation.RejectionReason);
            var cells = new BoardCell[Definition.CellCount];
            bool changed = Confirmed == null;
            for (int i = 0; i < cells.Length; i++)
            {
                BoardCell state = observation.Cells[i].State;
                if (!state.IsKnown) return Fail(request, "Occupancy or color is unknown; confirmed state was retained.");
                // Logical confirmed state does not inherit any detector/source instance identity.
                cells[i] = state.Occupancy == Occupancy.Empty ? BoardCell.Empty : new BoardCell(Occupancy.Occupied, state.ColorId);
                if (Confirmed != null && !Confirmed.Cells[i].SameContent(cells[i])) changed = true;
            }
            BoardSnapshot previous = Confirmed;
            BoardSnapshot candidate = changed
                ? new BoardSnapshot(Definition, (previous?.Version ?? 0) + 1, cells, SnapshotSource.Vision) : previous;
            BoardDelta delta = previous == null ? null : BoardDelta.Between(previous, candidate);
            Confirmed = candidate;
            LastConfirmedObservation = observation;
            current = null;
            return new BoardUpdate(previous == null ? UpdateOutcome.Baseline : changed ? UpdateOutcome.Changed : UpdateOutcome.Unchanged, Confirmed, delta);
        }
        private BoardUpdate Stale() => new BoardUpdate(UpdateOutcome.Stale, Confirmed, reason: "The request was superseded, cancelled or already completed.");
    }
}
