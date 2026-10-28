using System;
using Brick2Life.Application;
using Brick2Life.Simulation;
using UnityEngine;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Composition entry for task C. When task B's monitor is present it owns the single BoardSyncSession,
    /// and every confirmed prediction update is forwarded here; otherwise the entry keeps its own session so
    /// the manual-target demo can still drive the feedback path.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class SimulationFeedbackBootstrap : MonoBehaviour
    {
        [Tooltip("仿真棋盘会话，仅用于读取公开的 BoardDefinition（棋盘定义），不读取积木状态。")]
        [SerializeField] private SimulationBoardSession board;
        [SerializeField] private SimulationLevelAdapter adapter;
        [SerializeField] private SimulationFeedbackDemo demo;
        [Tooltip("任务 B 的识别面板。留空时运行时查找；找到则共享它的 BoardSyncSession。")]
        [SerializeField] private SimulationVisionMonitor vision;
        [Tooltip("接入任务 B 后自动停用人工目标演示，避免人工布局覆盖识别结果。")]
        [SerializeField] private bool disableDemoWhenVisionActive = true;
        [Tooltip("没有任务 B 时使用的自建会话标定标识。")]
        [SerializeField] private string calibrationId = "brick2life-simulation";

        public BoardSyncSession Session { get; private set; }
        public bool VisionConnected { get; private set; }

        private void Awake()
        {
            if (board == null || adapter == null)
            {
                Debug.LogError("请配置仿真棋盘会话与任务 C 关卡适配器；可运行 Brick2Life/Simulation/Install feedback。", this);
                enabled = false;
                return;
            }
            if (vision == null) vision = FindFirstObjectByType<SimulationVisionMonitor>();
            if (vision != null && vision.Session != null) ConnectVision(vision);
            else ConnectStandalone();
        }

        private void OnDestroy()
        {
            if (vision == null || !VisionConnected) return;
            vision.UpdateApplied -= OnVisionUpdate;
            vision.SessionRebuilt -= OnSessionRebuilt;
            vision.UiReady -= OnVisionUiReady;
        }

        private void ConnectVision(SimulationVisionMonitor monitor)
        {
            VisionConnected = true;
            Session = monitor.Session;
            monitor.UpdateApplied += OnVisionUpdate;
            monitor.SessionRebuilt += OnSessionRebuilt;
            // The monitor builds its panel in Start; bind the status line as soon as it exists.
            monitor.UiReady += OnVisionUiReady;
            if (monitor.FeedbackText != null) adapter.SetStatusLabel(monitor.FeedbackText);
            adapter.Bind(Session);
            if (disableDemoWhenVisionActive && demo != null)
                demo.enabled = false;
            adapter.SetStatusText(disableDemoWhenVisionActive
                ? "关卡：等待识别确认（任务 B 已接入，人工目标演示已停用）"
                : "关卡：等待识别确认（任务 B 已接入）");
            Debug.Log("任务 C 已接入任务 B：共享 BoardSyncSession（calibration=" + Session.CalibrationId +
                "），识别确认后自动更新关卡与投影；人工目标演示=" +
                (disableDemoWhenVisionActive ? "已停用" : "仍启用"), this);
        }

        private void ConnectStandalone()
        {
            string calibration = string.IsNullOrWhiteSpace(calibrationId) ? "brick2life-simulation" : calibrationId;
            Session = new BoardSyncSession(board.Definition, calibration);
            adapter.Bind(Session);
            if (demo != null) demo.Bind(Session, adapter);
            Debug.Log("未找到任务 B 识别面板：任务 C 使用自建会话与人工目标演示（calibration=" + calibration + "）。", this);
        }

        private void OnSessionRebuilt(BoardSyncSession session)
        {
            if (session == null || !VisionConnected) return;
            Session = session;
            try { adapter.Bind(session); }
            catch (Exception error) { Debug.LogError("任务 C 重新绑定识别会话失败：" + error.Message, this); }
        }

        private void OnVisionUiReady()
        {
            if (vision == null) return;
            adapter.SetStatusLabel(vision.FeedbackText);
        }

        private void OnVisionUpdate(BoardUpdate update)
        {
            try { adapter.Apply(update); }
            catch (Exception error) { Debug.LogError("任务 C 处理识别确认失败：" + error, this); }
        }

        // Task B integration point: confirm an observation, then hand the update to the adapter.
        public FeedbackFrame Submit(BoardUpdate update)
        {
            if (adapter == null) throw new InvalidOperationException("任务 C 适配器未配置。");
            return adapter.Apply(update);
        }
    }
}
