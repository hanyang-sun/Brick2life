using System;
using System.Collections.Generic;
using Brick2Life.Application;
using UnityEngine;
using UnityEngine.UI;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Task C consumer: turns confirmed prediction updates into gameplay objects and projection content.
    /// It never reads the simulation layout or ground truth; the only input is BoardUpdate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationLevelAdapter : MonoBehaviour
    {
        [Serializable] public struct RoleRule
        {
            public string colorId;
            public GameRole role;
        }

        [SerializeField] private SimulationLevelView levelView;
        [SerializeField] private ProjectionSimulator projection;
        [Tooltip("颜色到游戏角色的映射；未配置的颜色会被报告为关卡问题。")]
        [SerializeField] private RoleRule[] rules = Array.Empty<RoleRule>();
        [SerializeField] private Text statusLabel;

        private LevelFeedbackSession feedback;
        private GeneratedRound round;
        private BoardSyncSession boundSession;
        public GeneratedRound Round => round;

        public void SetGeneratedRound(GeneratedRound value)
        {
            round = value;
            if (boundSession != null) Bind(boundSession);
            LastFrame = null;
            if (projection != null) projection.Clear();
            if (levelView != null) levelView.Clear();
        }

        public LevelFeedbackSession Feedback => feedback;
        public FeedbackFrame LastFrame { get; private set; }
        public bool IsBound => feedback != null;
        public SimulationLevelView LevelView => levelView;
        public ProjectionSimulator Projection => projection;
        public event Action<FeedbackFrame> FeedbackChanged;
        public long ConfirmedVersion => boundSession?.Confirmed?.Version ?? -1;

        public void Bind(BoardSyncSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            boundSession = session;
            // Task B may rebuild its session (new calibration); re-binding keeps the same rules and views.
            var pairs = new List<KeyValuePair<string, GameRole>>();
            foreach (RoleRule rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.colorId) || rule.role == GameRole.None) continue;
                pairs.Add(new KeyValuePair<string, GameRole>(rule.colorId,
                    round != null && rule.role == GameRole.Core ? GameRole.Wall : rule.role));
            }
            if (pairs.Count == 0)
            {
                Debug.LogError("关卡适配器没有颜色规则；请运行 Brick2Life/Simulation/Install feedback 或手工配置。", this);
                enabled = false;
                return;
            }
            feedback = new LevelFeedbackSession(session.Definition, new LevelRuleSet(pairs), round: round);
            if (levelView == null) Debug.LogWarning("关卡适配器缺少关卡视图，只更新投影与状态文字。", this);
            if (projection == null) Debug.LogWarning("关卡适配器缺少投影组件，只更新关卡与状态文字。", this);
            UpdateStatus(null);
        }

        public FeedbackFrame Apply(BoardUpdate update)
        {
            if (feedback == null) throw new InvalidOperationException("关卡适配器尚未绑定 BoardSyncSession。");
            FeedbackFrame frame = feedback.Apply(update);
            if (frame.Changed && frame.Level != null)
            {
                if (levelView != null) levelView.Render(frame.Level);
                if (projection != null && frame.Plan != null) projection.Render(frame.Plan);
            }
            LastFrame = frame;
            UpdateStatus(frame);
            if (frame.Changed) FeedbackChanged?.Invoke(frame);
            return frame;
        }

        public bool TryGetColorForRole(GameRole role, out string colorId)
        {
            colorId = null;
            if (round != null && role == GameRole.Core) return false;
            if (role == GameRole.None) return false;
            foreach (RoleRule rule in rules)
                if (rule.role == role && !string.IsNullOrWhiteSpace(rule.colorId)) { colorId = rule.colorId; return true; }
            return false;
        }

        public void SetStatusText(string text)
        {
            if (statusLabel != null) statusLabel.text = text;
        }

        // Task B's panel owns an extra line; the composition entry hands it to the adapter.
        public void SetStatusLabel(Text label)
        {
            statusLabel = label;
            UpdateStatus(LastFrame);
        }

        private void UpdateStatus(FeedbackFrame frame)
        {
            if (statusLabel == null) return;
            if (feedback == null) { statusLabel.text = "关卡：尚未建立"; return; }
            string line = "关卡：" + feedback.Describe();
            if (frame != null && !string.IsNullOrEmpty(frame.Note)) line += "\n" + frame.Note;
            statusLabel.text = line;
        }
    }
}
