using Brick2Life.Feedback;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEngine;

namespace Brick2Life.Editor
{
    public sealed class SimulationProjectionWindow : EditorWindow
    {
        private SimulationProjectionPreview preview;
        private SimulationLevelAdapter adapter;
        private SimulationBattleController battle;
        private bool projectionOnly;
        private double nextRepaint;

        public bool ProjectionOnly => projectionOnly;

        public void Bind(SimulationLevelAdapter source, SimulationBoardSession board, SimulationBattleController controller)
        {
            preview?.Dispose();
            adapter = source; battle = controller;
            preview = new SimulationProjectionPreview(board.Palette);
            titleContent = new GUIContent("投影展示");
            minSize = new Vector2(440, 340);
        }

        private void OnFocus() => SimulationEditAccess.Refresh();

        private void Update()
        {
            if (EditorApplication.timeSinceStartup < nextRepaint) return;
            nextRepaint = EditorApplication.timeSinceStartup + 1.0 / 30;
            Repaint();
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying || preview == null || adapter == null)
            {
                EditorGUILayout.HelpBox("进入 Play 后，从 Brick2Life/Windows/Open layout and projection 打开双窗口。", MessageType.Info);
                return;
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                projectionOnly = GUILayout.Toolbar(projectionOnly ? 1 : 0, new[] { "积木＋投影", "仅投影" }, EditorStyles.toolbarButton) == 1;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("复位视角", EditorStyles.toolbarButton, GUILayout.Width(80))) preview.ResetView();
            }
            Rect viewport = new Rect(0, 23, position.width, Mathf.Max(64, position.height - 47));
            Event current = Event.current;
            int orbitControl = GUIUtility.GetControlID(FocusType.Passive);
            if (current.type == EventType.MouseDown && viewport.Contains(current.mousePosition) && (current.button == 0 || current.button == 1))
            { Focus(); GUIUtility.hotControl = orbitControl; current.Use(); }
            else if (current.type == EventType.MouseDrag && GUIUtility.hotControl == orbitControl)
            { preview.Orbit(current.delta); current.Use(); Repaint(); }
            else if (current.type == EventType.MouseUp && GUIUtility.hotControl == orbitControl)
            { GUIUtility.hotControl = 0; current.Use(); }
            else if (current.type == EventType.ScrollWheel && viewport.Contains(current.mousePosition))
            {
                preview.Zoom(current.delta.y); current.Use(); Repaint();
            }
            if (current.type == EventType.Repaint)
            {
                preview.UpdateScene(adapter.Feedback?.Level, adapter.Projection, battle != null ? battle.View : null, projectionOnly);
                RenderTexture texture = preview.Render(Mathf.RoundToInt(viewport.width), Mathf.RoundToInt(viewport.height));
                GUI.DrawTexture(viewport, texture, ScaleMode.StretchToFill, false);
                if (preview.DisplayedVersion < 0)
                    GUI.Label(new Rect(16, 38, position.width - 32, 28), battle != null && battle.Round != null
                        ? "系统目标已生成，正在确认玩家布局…" : "等待布局稳定确认…", EditorStyles.whiteLargeLabel);
            }
            GUILayout.BeginArea(new Rect(8, position.height - 22, position.width - 16, 20));
            GUILayout.Label("只读展示 · 拖动旋转 · 滚轮缩放 · 编辑和战斗操作请点回原始窗口", EditorStyles.miniLabel);
            GUILayout.EndArea();
        }

        private void OnDisable()
        {
            preview?.Dispose();
            preview = null;
        }
    }
}
