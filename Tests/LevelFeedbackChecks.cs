using Brick2Life.Core;
using Brick2Life.Application;

internal static class LevelFeedbackChecks
{
    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }

        var definition = new BoardDefinition(colorIds: new[] { "red", "green", "blue" });
        var rules = LevelRuleSet.Create(
            new KeyValuePair<string, GameRole>("red", GameRole.Tower),
            new KeyValuePair<string, GameRole>("green", GameRole.Wall),
            new KeyValuePair<string, GameRole>("blue", GameRole.Core));
        var session = new BoardSyncSession(definition, "calibration-v1");
        var feedback = new LevelFeedbackSession(definition, rules);
        Check(feedback.AppliedVersion == -1 && feedback.Level == null, "A fresh session must not report a level.");

        ObservedCell[] Cells(params (int x, int y, string color)[] bricks)
        {
            var cells = new ObservedCell[definition.CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = new ObservedCell(BoardCell.Empty);
            foreach (var brick in bricks)
                cells[definition.Index(brick.x, brick.y)] = new ObservedCell(new BoardCell(Occupancy.Occupied, brick.color, "detector"));
            return cells;
        }
        FeedbackFrame Submit(ObservedCell[] cells, string frameId = null)
        {
            string id = frameId ?? Guid.NewGuid().ToString("N");
            ObservationRequest request = session.BeginFrame(id, DateTimeOffset.UtcNow);
            return feedback.Apply(session.Confirm(request, Observe(request, cells)));
        }
        FrameObservation Observe(ObservationRequest request, ObservedCell[] cells) => new FrameObservation(definition,
            request.FrameId, request.SessionId, request.Sequence, request.CapturedAtUtc, request.CalibrationId, cells);

        bool threw = false;
        try { feedback.Apply(null); } catch (ArgumentNullException) { threw = true; }
        Check(threw, "A missing update must be rejected.");

        var baseline = Submit(Cells((0, 0, "blue"), (3, 4, "red")));
        Check(baseline.Outcome == UpdateOutcome.Baseline && baseline.Changed, "A baseline must be applied and mark the views dirty.");
        Check(baseline.Plan.LevelVersion == 1 && baseline.Plan.Count(ProjectionMarkKind.ChangeHighlight) == 0, "A baseline must not project change marks.");
        Check(baseline.Level.Count(GameRole.Core) == 1 && baseline.Level.Count(GameRole.Tower) == 1, "The baseline level is wrong.");
        Check(baseline.Level.IsPlayable && baseline.Note == null, "A valid baseline must be playable without a note.");
        Check(feedback.AppliedVersion == session.Confirmed.Version, "Feedback did not follow the confirmed version.");
        Check(baseline.Level.Cells.All(cell => cell.ColorId != "detector"), "Detector identity leaked into the level.");

        LevelState firstLevel = baseline.Level;
        ProjectionPlan firstPlan = baseline.Plan;
        var unchanged = Submit(Cells((0, 0, "blue"), (3, 4, "red")));
        Check(unchanged.Outcome == UpdateOutcome.Unchanged && !unchanged.Changed, "An unchanged frame must not refresh the views.");
        Check(ReferenceEquals(unchanged.Level, firstLevel) && ReferenceEquals(unchanged.Plan, firstPlan), "An unchanged frame must keep the same level and plan.");
        Check(feedback.AppliedVersion == 1, "An unchanged frame advanced the applied version.");

        var changed = Submit(Cells((0, 0, "blue"), (3, 4, "red"), (5, 5, "green")));
        Check(changed.Outcome == UpdateOutcome.Changed && changed.Changed && changed.Plan.LevelVersion == 2, "An addition must produce a new level version.");
        Check(changed.Plan.Count(ProjectionMarkKind.ChangeHighlight) == 1, "An addition must project exactly one change mark.");
        Check(changed.Level.Count(GameRole.Wall) == 1 && changed.Level.IsPlayable, "The added wall did not reach the level.");
        Check(changed.Plan.Marks.Any(mark => mark.Kind == ProjectionMarkKind.RoleHighlight && mark.Role == GameRole.Wall), "The wall is not highlighted.");

        var occluded = Cells((0, 0, "blue"), (3, 4, "red"), (5, 5, "green"));
        occluded[definition.Index(5, 5)] = new ObservedCell(BoardCell.Unknown, reason: "occluded");
        var rejected = Submit(occluded);
        Check(rejected.Outcome == UpdateOutcome.Rejected && !rejected.Changed, "An unknown cell must not refresh the views.");
        Check(ReferenceEquals(rejected.Level, changed.Level), "A rejection discarded the confirmed level.");
        Check(!string.IsNullOrEmpty(rejected.Note), "A rejection must explain why the level was kept.");

        // A superseded frame must be reported as stale and must not touch the level.
        ObservationRequest superseded = session.BeginFrame("superseded", DateTimeOffset.UtcNow);
        session.BeginFrame("newer", DateTimeOffset.UtcNow);
        var stale = feedback.Apply(session.Confirm(superseded, Observe(superseded, Cells((0, 0, "blue")))));
        Check(stale.Outcome == UpdateOutcome.Stale && !stale.Changed && ReferenceEquals(stale.Level, changed.Level), "A stale frame must not change the level.");
        Check(feedback.AppliedVersion == 2, "A stale frame advanced the applied version.");

        var missingCore = Submit(Cells((3, 4, "red")), "missing-core");
        Check(missingCore.Outcome == UpdateOutcome.Changed && !missingCore.Level.IsPlayable, "A layout without a core must still be published as unplayable.");
        Check(missingCore.Note != null && missingCore.Note.Contains("核心"), "An unplayable layout must report the missing core.");
        Check(missingCore.Plan.Count(ProjectionMarkKind.RoleHighlight) == 1, "An unplayable layout must still describe its cells.");

        var recovered = Submit(Cells((0, 0, "blue")), "recovered");
        Check(recovered.Level.IsPlayable && recovered.Outcome == UpdateOutcome.Changed, "The level did not recover after a valid confirmation.");

        Check(feedback.Describe().Contains("核心 1"), "Describe does not summarise the current level.");

        int failures = 0;
        void ExpectFailure(Action action) { try { action(); } catch (ArgumentException) { failures++; } }
        var other = new BoardDefinition(boardId: "other-board", colorIds: new[] { "red" });
        var otherSession = new BoardSyncSession(other, "calibration-v1");
        ObservationRequest otherRequest = otherSession.BeginFrame("foreign", DateTimeOffset.UtcNow);
        var foreignCells = Enumerable.Range(0, other.CellCount).Select(i => new ObservedCell(BoardCell.Empty)).ToArray();
        var foreign = otherSession.Confirm(otherRequest, new FrameObservation(other, otherRequest.FrameId, otherRequest.SessionId,
            otherRequest.Sequence, otherRequest.CapturedAtUtc, otherRequest.CalibrationId, foreignCells));
        ExpectFailure(() => feedback.Apply(foreign));
        Check(failures == 1, "A snapshot from another board must be rejected.");

        Console.WriteLine($"LEVEL_FEEDBACK_CHECKS_PASSED: {assertions} assertions.");
    }
}
