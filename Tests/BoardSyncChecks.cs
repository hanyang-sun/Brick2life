using Brick2Life.Core;
using Brick2Life.Application;

internal static class BoardSyncChecks
{
    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
        var definition = new BoardDefinition(colorIds: new[] { "red", "blue" });
        var session = new BoardSyncSession(definition, "calibration-v1");
        ObservationRequest Begin(string id) => session.BeginFrame(id, DateTimeOffset.UtcNow);
        ObservedCell[] Cells(int occupied = -1, string color = "red") => Enumerable.Range(0, 100)
            .Select(i => new ObservedCell(i == occupied ? new BoardCell(Occupancy.Occupied, color, "detector-id") : BoardCell.Empty)).ToArray();
        FrameObservation Observe(ObservationRequest request, ObservedCell[] cells, string calibration = null) => new FrameObservation(
            definition, request.FrameId, request.SessionId, request.Sequence, request.CapturedAtUtc, calibration ?? request.CalibrationId, cells);
        Check(session.Confirmed == null, "A fabricated empty baseline was published before recognition.");
        var first = Begin("first");
        var observations = Cells(32);
        FrameObservation observation = Observe(first, observations);
        observations[32] = new ObservedCell(BoardCell.Empty);
        var initial = session.Confirm(first, observation);
        Check(initial.Outcome == UpdateOutcome.Baseline && initial.Delta == null && initial.Snapshot.Version == 1, "First observation did not establish a baseline.");
        Check(initial.Snapshot.At(2, 3).ColorId == "red", "Caller mutation changed visual observation.");
        Check(initial.Snapshot.At(2, 3).BrickId == null && initial.Snapshot.Source == SnapshotSource.Vision, "Detector identity leaked into logical confirmed state.");
        var unchanged = Begin("same");
        var same = session.Confirm(unchanged, Observe(unchanged, Cells(32)));
        Check(same.Outcome == UpdateOutcome.Unchanged && same.Delta.IsEmpty && ReferenceEquals(same.Snapshot, initial.Snapshot), "Unchanged frame advanced state version.");
        var unknown = Begin("occluded");
        var hidden = Cells(32); hidden[32] = new ObservedCell(BoardCell.Unknown, reason: "occluded");
        Check(session.Confirm(unknown, Observe(unknown, hidden)).Outcome == UpdateOutcome.Rejected, "Unknown occupancy was confirmed.");
        Check(ReferenceEquals(session.Confirmed, initial.Snapshot), "Unknown occupancy erased prior state.");
        var unknownColor = Begin("unknown-color");
        hidden = Cells(32); hidden[32] = new ObservedCell(new BoardCell(Occupancy.Occupied));
        Check(session.Confirm(unknownColor, Observe(unknownColor, hidden)).Outcome == UpdateOutcome.Rejected, "Unknown color was confirmed.");
        var old = Begin("old");
        var latest = Begin("latest");
        Check(session.Confirm(old, Observe(old, Cells())).Outcome == UpdateOutcome.Stale, "Old frame overwrote a newer request.");
        Check(session.Confirmed.Version == 1, "Stale frame changed confirmed state.");
        var moved = session.Confirm(latest, Observe(latest, Cells(35)));
        Check(moved.Outcome == UpdateOutcome.Changed && moved.Snapshot.Version == 2 && moved.Delta.Changes.Count == 2, "Move was not confirmed once.");
        Check(session.Confirm(latest, Observe(latest, Cells())).Outcome == UpdateOutcome.Stale, "Completed request was replayed.");
        Check(initial.Snapshot.At(2, 3).ColorId == "red" && initial.Snapshot.At(5, 3).Occupancy == Occupancy.Empty, "New confirmation mutated historical state.");
        var pending = Begin("old-calibration");
        session.ChangeCalibration("calibration-v2");
        Check(session.Confirm(pending, Observe(pending, Cells())).Outcome == UpdateOutcome.Stale, "Calibration change left an old request active.");
        var mismatch = Begin("wrong-calibration");
        Check(session.Confirm(mismatch, Observe(mismatch, Cells(), "calibration-v1")).Outcome == UpdateOutcome.Rejected, "Mismatched calibration was accepted.");
        var cancelled = Begin("cancelled"); session.Cancel();
        Check(session.Confirm(cancelled, Observe(cancelled, Cells())).Outcome == UpdateOutcome.Stale, "Cancelled request was confirmed.");
        var failed = Begin("failure");
        Check(session.Fail(failed, "capture failed").Outcome == UpdateOutcome.Rejected && session.Confirmed.Version == 2, "Failure discarded the confirmed layout.");
        var denied = Begin("rejected");
        var rejected = new FrameObservation(definition, denied.FrameId, denied.SessionId, denied.Sequence, denied.CapturedAtUtc,
            denied.CalibrationId, null, false, "blurred image");
        Check(session.Confirm(denied, rejected).Reason == "blurred image" && session.Confirmed.Version == 2, "Recognizer rejection was lost.");
        var recolor = Begin("recolor");
        var colored = session.Confirm(recolor, Observe(recolor, Cells(35, "blue")));
        Check(colored.Delta.Changes.Count == 1 && colored.Snapshot.At(5, 3).ColorId == "blue", "Recolor was not a single cell change.");
        var cleared = Begin("empty");
        var empty = session.Confirm(cleared, Observe(cleared, Cells()));
        Check(empty.Outcome == UpdateOutcome.Changed && empty.Snapshot.Cells.All(cell => cell.Occupancy == Occupancy.Empty), "Confirmed empty board was rejected.");
        Console.WriteLine($"BOARD_SYNC_CHECKS_PASSED: {assertions} assertions.");
    }
}
