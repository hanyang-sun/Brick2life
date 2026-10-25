using System;
using Brick2Life.Application;
using Brick2Life.Simulation;
using UnityEngine;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Composition entry for task C. It owns the BoardSyncSession that task B will confirm into and hands it to
    /// the level adapter, keeping the simulation assembly free of Application references.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class SimulationFeedbackBootstrap : MonoBehaviour
    {
        [Tooltip("仿真棋盘会话，仅用于读取公开的 BoardDefinition（棋盘定义），不读取积木状态。")]
        [SerializeField] private SimulationBoardSession board;
        [SerializeField] private SimulationLevelAdapter adapter;
        [SerializeField] private SimulationFeedbackDemo demo;
        [Tooltip("标定标识，随每次确认记录到 BoardSyncSession。")]
        [SerializeField] private string calibrationId = "brick2life-simulation";

        public BoardSyncSession Session { get; private set; }

        private void Awake()
        {
            if (board == null || !board.IsInitialized || adapter == null)
            {
                Debug.LogError("请配置仿真棋盘会话与任务 C 关卡适配器；可运行 Brick2Life/Simulation/Install feedback。", this);
                enabled = false;
                return;
            }
            string calibration = string.IsNullOrWhiteSpace(calibrationId) ? "brick2life-simulation" : calibrationId;
            Session = new BoardSyncSession(board.Definition, calibration);
            adapter.Bind(Session);
            if (demo != null) demo.Bind(Session, adapter);
            Debug.Log("任务 C 反馈会话已建立：board=" + board.Definition.BoardId + "，calibration=" + Session.CalibrationId +
                "；预测事件入口为 BoardSyncSession，投影与关卡不读取仿真真值。", this);
        }

        // Task B integration point: confirm an observation, then hand the update to the adapter.
        public FeedbackFrame Submit(BoardUpdate update)
        {
            if (adapter == null) throw new InvalidOperationException("任务 C 适配器未配置。");
            return adapter.Apply(update);
        }
    }
}
