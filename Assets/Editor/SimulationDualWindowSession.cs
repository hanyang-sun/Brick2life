using System;
using Brick2Life.Feedback;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Brick2Life.Editor
{
    [InitializeOnLoad]
    public static class SimulationDualWindowSession
    {
        private static EditorWindow layoutWindow;
        private static SimulationProjectionWindow displayWindow;
        private static Camera workspaceCamera;
        private static int originalMask;
        public static EditorWindow LayoutWindow => layoutWindow;
        public static SimulationProjectionWindow DisplayWindow => displayWindow;

        static SimulationDualWindowSession()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            EditorApplication.delayCall += AutoOpen;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += AutoOpen;
            else if (state == PlayModeStateChange.ExitingPlayMode) Close();
        }

        private static void AutoOpen()
        {
            if (UnityEngine.Application.isBatchMode || !EditorApplication.isPlaying || displayWindow != null) return;
            if (Object.FindFirstObjectByType<SimulationBootstrap>() == null) return;
            Open();
        }

        [MenuItem("Brick2Life/Windows/Open layout and projection")]
        public static void Open()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("请先进入 Play，再打开原始布局与投影展示窗口。"); return; }
            if (layoutWindow != null && displayWindow != null) { layoutWindow.Focus(); displayWindow.Repaint(); return; }
            var board = Object.FindFirstObjectByType<SimulationBoardSession>();
            var adapter = Object.FindFirstObjectByType<SimulationLevelAdapter>();
            var controller = Object.FindFirstObjectByType<SimulationBattleController>();
            if (board == null || adapter == null) { Debug.LogWarning("请先打开仿真场景并安装任务 C 反馈。"); return; }
            Close();
            try
            {
                workspaceCamera = GameObject.Find("WorkspaceCamera")?.GetComponent<Camera>();
                if (workspaceCamera == null) throw new InvalidOperationException("没有找到原始布局相机 WorkspaceCamera。");
                originalMask = workspaceCamera.cullingMask;
                workspaceCamera.cullingMask &= ~((1 << 30) | (1 << 31));
                // Unity owns GameView's input/UI implementation; keep this type lookup editor-only.
                Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType == null) throw new InvalidOperationException("当前编辑器没有可用的 GameView 类型。");
                layoutWindow = (EditorWindow)ScriptableObject.CreateInstance(gameViewType);
                layoutWindow.titleContent = new GUIContent("原始积木布局");
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                float width = Mathf.Clamp((main.width - 60) * 0.5f, 560, 1000);
                float height = Mathf.Clamp(main.height - 120, 440, 780);
                layoutWindow.position = new Rect(main.x + 15, main.y + 75, width, height);
                layoutWindow.ShowUtility();
                displayWindow = ScriptableObject.CreateInstance<SimulationProjectionWindow>();
                displayWindow.Bind(adapter, board, controller);
                displayWindow.position = new Rect(main.x + width + 30, main.y + 75, width, height);
                displayWindow.ShowUtility();
                SimulationEditAccess.ConfigureFocus(() => layoutWindow != null && EditorWindow.focusedWindow == layoutWindow);
                layoutWindow.Focus();
                SimulationEditAccess.Refresh();
            }
            catch (Exception error) { Close(); Debug.LogException(error); }
        }

        private static void Tick()
        {
            if (layoutWindow == null && displayWindow == null) return;
            if (!EditorApplication.isPlaying || layoutWindow == null || displayWindow == null) { Close(); return; }
            SimulationEditAccess.Refresh();
        }

        [MenuItem("Brick2Life/Windows/Close layout and projection")]
        public static void Close()
        {
            EditorWindow oldLayout = layoutWindow;
            SimulationProjectionWindow oldDisplay = displayWindow;
            layoutWindow = null; displayWindow = null;
            SimulationEditAccess.ConfigureFocus(null);
            if (workspaceCamera != null) workspaceCamera.cullingMask = originalMask;
            workspaceCamera = null;
            if (oldDisplay != null) oldDisplay.Close();
            if (oldLayout != null) oldLayout.Close();
        }
    }
}
