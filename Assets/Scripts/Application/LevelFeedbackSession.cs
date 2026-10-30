using System;
using Brick2Life.Core;

namespace Brick2Life.Application
{
    // One presentation-ready result. Changed=false means the views may keep their current objects.
    public sealed class FeedbackFrame
    {
        public UpdateOutcome Outcome { get; }
        public LevelState Level { get; }
        public ProjectionPlan Plan { get; }
        public bool Changed { get; }
        public string Note { get; }
        internal FeedbackFrame(UpdateOutcome outcome, LevelState level, ProjectionPlan plan, bool changed, string note)
        { Outcome = outcome; Level = level; Plan = plan; Changed = changed; Note = note; }
    }

    // Task C entry point. It accepts confirmed prediction updates from BoardSyncSession only:
    // no simulation layout, no ground truth and no detector identity enter this class.
    public sealed class LevelFeedbackSession
    {
        public BoardDefinition Definition { get; }
        public LevelRuleSet Rules { get; }
        public ProjectionOptions Options { get; }
        public GeneratedRound Round { get; }
        public LevelState Level { get; private set; }
        public ProjectionPlan Plan { get; private set; }
        public long AppliedVersion => Level == null ? -1 : Level.SourceVersion;

        public LevelFeedbackSession(BoardDefinition definition, LevelRuleSet rules, ProjectionOptions options = null,
            GeneratedRound round = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Options = options ?? new ProjectionOptions();
            Options.Validate();
            Round = round;
        }

        public FeedbackFrame Apply(BoardUpdate update)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            // Rejections and stale frames keep the last confirmed level and do not re-publish projection marks.
            if (update.Outcome == UpdateOutcome.Rejected || update.Outcome == UpdateOutcome.Stale)
                return new FeedbackFrame(update.Outcome, Level, Plan, false, update.Reason);
            if (update.Snapshot == null) throw new InvalidOperationException("Accepted updates must carry a confirmed snapshot.");
            if (!Definition.IsCompatible(update.Snapshot.Definition))
                throw new ArgumentException("Feedback session received a snapshot for another board definition.", nameof(update));
            if (Level != null && update.Snapshot.Version < Level.SourceVersion)
                throw new ArgumentException("Confirmed state versions must not go backwards.", nameof(update));
            if (update.Outcome == UpdateOutcome.Unchanged && Level != null)
                return new FeedbackFrame(update.Outcome, Level, Plan, false, null);

            BoardDelta delta = update.Outcome == UpdateOutcome.Changed ? update.Delta : null;
            if (delta != null && Level != null && delta.BaseVersion != Level.SourceVersion)
                throw new ArgumentException("Changes must start at the previously applied version.", nameof(update));
            LevelState level = LevelState.FromSnapshot(update.Snapshot, Rules, Round);
            ProjectionPlan plan = ProjectionPlanBuilder.Build(level, delta, Options);
            Level = level; Plan = plan;
            string note = level.IsPlayable ? null : level.Issues[0].Message;
            return new FeedbackFrame(update.Outcome, level, plan, true, note);
        }

        public string Describe()
        {
            if (Level == null) return "尚未确认任何布局。";
            string state = Level.Describe();
            if (!Level.IsPlayable) return state + " · " + Level.Issues[0].Message;
            return Plan == null ? state : state + " · " + Plan.Summary;
        }
    }
}
