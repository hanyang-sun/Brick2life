using UnityEngine;

namespace Brick2Life.Simulation
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class SimulationBootstrap : MonoBehaviour
    {
        [SerializeField] private SimulationBoardSession board;
        [SerializeField] private SimulationBrickEditor editor;
        [SerializeField] private SimulationCameraCapture capture;
        [SerializeField] private SimulationVisionMonitor vision;
        private void Awake()
        {
            if (board == null || editor == null || capture == null || !board.IsInitialized)
            { Debug.LogError("请配置仿真入口的棋盘、编辑界面和采集组件。", this); enabled = false; return; }
            editor.Bind(board);
            capture.Bind(board);
            if (vision == null) vision = GetComponent<SimulationVisionMonitor>();
            if (vision == null) vision = gameObject.AddComponent<SimulationVisionMonitor>();
            vision.Bind(board, capture);
        }
    }
}
