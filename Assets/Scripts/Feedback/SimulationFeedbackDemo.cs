using System;
using System.Globalization;
using Brick2Life.Application;
using Brick2Life.Core;
using Brick2Life.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Standalone task C demonstration. It submits hand-written target layouts as observations so the
    /// level/projection path can be exercised before task B is connected. This is NOT recognition output.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationFeedbackDemo : MonoBehaviour
    {
        [Tooltip("人工标注目标，仅用于任务 C 的独立演示（关卡与投影反馈）。接入任务 B 后请禁用本组件。")]
        [SerializeField] private bool keyboardDemo = true;
        [SerializeField] private Text statusLabel;
        [Tooltip("F5 下一个人工目标，F6 空布局，F7 投影开/关。")]
        [SerializeField] private bool logToConsole = true;

        // Layouts are described by gameplay role; the adapter maps roles back to the board's palette colors.
        private static readonly (int x, int y, GameRole role)[][] Samples =
        {
            new[]
            {
                (4, 4, GameRole.Core), (3, 4, GameRole.Wall), (5, 4, GameRole.Wall), (4, 3, GameRole.Wall),
                (4, 5, GameRole.Wall), (2, 2, GameRole.Tower), (7, 7, GameRole.Tower)
            },
            new[]
            {
                (4, 4, GameRole.Core), (3, 4, GameRole.Wall), (5, 4, GameRole.Wall), (4, 5, GameRole.Wall),
                (2, 2, GameRole.Tower), (7, 7, GameRole.Tower), (7, 2, GameRole.Tower)
            },
            new[]
            {
                (0, 0, GameRole.Core), (9, 9, GameRole.Tower), (9, 0, GameRole.Tower)
            }
        };

        private BoardSyncSession session;
        private SimulationLevelAdapter adapter;
        private long frameCounter;
        private int sampleIndex;
        private SimulationBattleController battle;

        public bool IsBound => session != null && adapter != null;
        public int SampleIndex => sampleIndex;

        public void Bind(BoardSyncSession syncSession, SimulationLevelAdapter levelAdapter)
        {
            session = syncSession ?? throw new ArgumentNullException(nameof(syncSession));
            adapter = levelAdapter ?? throw new ArgumentNullException(nameof(levelAdapter));
            if (logToConsole)
                Debug.Log("人工目标演示已绑定：用 F5 / F6 / F7 驱动关卡与投影；这些布局不是识别结果。", this);
        }

        public void NextSample()
        {
            if (!CanChangeLayout()) return;
            sampleIndex = (sampleIndex + 1) % Samples.Length;
            Submit(Samples[sampleIndex], "样例 " + (sampleIndex + 1) + "/" + Samples.Length);
        }

        public void SubmitEmpty()
        {
            if (!CanChangeLayout()) return;
            Submit(Array.Empty<(int x, int y, GameRole role)>(), "空布局");
        }

        public void SubmitCurrentSample()
        {
            if (!CanChangeLayout()) return;
            Submit(Samples[sampleIndex], "样例 " + (sampleIndex + 1) + "/" + Samples.Length);
        }

        public void ToggleProjection()
        {
            if (adapter == null) return;
            ProjectionSimulator projector = adapter.Projection;
            if (projector == null) return;
            projector.SetVisible(!projector.IsVisible);
            string message = projector.IsVisible ? "投影：开" : "投影：关（对照条件）";
            if (logToConsole) Debug.Log(message, this);
            if (statusLabel != null) statusLabel.text = message;
        }

        private bool CanChangeLayout()
        {
            if (!IsBound || !SimulationEditAccess.CanEdit) return false;
            if (battle == null) battle = FindFirstObjectByType<SimulationBattleController>();
            return battle == null || !battle.Running;
        }

        private void Update()
        {
            if (!keyboardDemo || !IsBound) return;
            if (Input.GetKeyDown(KeyCode.F5)) NextSample();
            else if (Input.GetKeyDown(KeyCode.F6)) SubmitEmpty();
            else if (Input.GetKeyDown(KeyCode.F7)) ToggleProjection();
        }

        private void Submit((int x, int y, GameRole role)[] layout, string label)
        {
            if (!CanChangeLayout()) return;
            BoardDefinition definition = session.Definition;
            var cells = new ObservedCell[definition.CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = new ObservedCell(BoardCell.Empty);
            int placed = 0;
            int skipped = 0;
            foreach (var item in layout)
            {
                if (!adapter.TryGetColorForRole(item.role, out string colorId))
                {
                    Debug.LogWarning("没有为角色 " + item.role + " 配置颜色，已跳过该个人工目标积木。", this);
                    skipped++;
                    continue;
                }
                cells[definition.Index(item.x, item.y)] = new ObservedCell(new BoardCell(Occupancy.Occupied, colorId));
                placed++;
            }
            string frameId = "manual-" + (++frameCounter).ToString("D4", CultureInfo.InvariantCulture);
            ObservationRequest request = session.BeginFrame(frameId, DateTimeOffset.UtcNow);
            var observation = new FrameObservation(definition, request.FrameId, request.SessionId, request.Sequence,
                request.CapturedAtUtc, request.CalibrationId, cells);
            BoardUpdate update = session.Confirm(request, observation);
            FeedbackFrame frame = adapter.Apply(update);
            string message = "人工目标 " + label + "（非识别结果）：" + adapter.Feedback.Describe() +
                (frame.Note == null ? "" : " · " + frame.Note) + (skipped == 0 ? "" : " · 跳过 " + skipped + " 块") +
                " · 放置 " + placed + " 块";
            if (logToConsole) Debug.Log(message, this);
            if (statusLabel != null) statusLabel.text = message;
        }
    }
}
