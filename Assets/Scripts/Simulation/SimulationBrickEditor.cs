using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityApplication = UnityEngine.Application;

namespace Brick2Life.Simulation
{
    [DisallowMultipleComponent]
    public sealed class SimulationBrickEditor : MonoBehaviour
    {
        private enum Tool { Add, Delete, Move }
        private const float OrbitDragThreshold = 5f;
        [SerializeField] private SimulationGridPointer pointer;
        [SerializeField] private Button[] toolButtons;
        [SerializeField] private Button colorButtonTemplate;
        [SerializeField] private RectTransform colorButtonContainer;
        [SerializeField] private Text statusLabel;

        private Button[] colorButtons;
        private SimulationBoardSession board;
        private IReadOnlyList<SimulationBoardSession.PaletteEntry> palette => board.Palette;
        private Tool tool;
        private int colorIndex;
        private string draggedId;
        private Vector2Int dragSource;
        private Vector2Int dragTarget;
        private int dragLayer;
        private bool hasDragTarget;
        private bool boardPressed, orbiting;
        private Vector2 pressPosition;
        private float lastOrbitX;
        private CanvasGroup editingControls;

        public SimulationBoardSession Board => board;
        private bool CanEdit => board != null && board.IsInitialized && !board.IsEditingLocked && SimulationEditAccess.CanEdit;

        public void Bind(SimulationBoardSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (board != null) board.EditingLockChanged -= OnEditingLockChanged;
            board = session;
            board.EditingLockChanged += OnEditingLockChanged;
            SimulationEditAccess.FocusChanged -= OnEditingFocusChanged;
            SimulationEditAccess.FocusChanged += OnEditingFocusChanged;
            OnEditingLockChanged(board.IsEditingLocked);
        }

        private void OnEditingLockChanged(bool locked)
        {
            RefreshEditingState();
        }

        private void OnEditingFocusChanged(bool focused) => RefreshEditingState();

        private void RefreshEditingState()
        {
            bool locked = board != null && board.IsEditingLocked;
            bool focused = SimulationEditAccess.CanEdit;
            CancelDrag();
            // All edit tools, palette buttons and ClearBoard share this existing panel.
            if (editingControls == null && toolButtons != null && toolButtons.Length > 0 && toolButtons[0] != null)
            {
                Transform controlsRoot = toolButtons[0].transform.parent;
                if (controlsRoot != null)
                {
                    editingControls = controlsRoot.GetComponent<CanvasGroup>();
                    if (editingControls == null) editingControls = controlsRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }
            if (editingControls != null) editingControls.interactable = !locked && focused;
            if (locked && statusLabel != null) statusLabel.text = "战斗中布局已锁定，结束本局或重试后可编辑。";
            else if (!focused && statusLabel != null) statusLabel.text = "点击原始布局窗口后可编辑。";
            else if (CanEdit && colorButtons != null && statusLabel != null) SelectTool((int)tool);
        }

        private void Start()
        {
            if (board == null || !board.IsInitialized || pointer == null || statusLabel == null ||
                palette.Count == 0 || toolButtons == null || toolButtons.Length != 3 ||
                colorButtonTemplate == null || colorButtonContainer == null ||
                colorButtonTemplate.GetComponentInChildren<Text>(true) == null)
            { ConfigurationError(); return; }
            foreach (Button button in toolButtons)
                if (button == null) { ConfigurationError(); return; }
            CreateColorButtons();
            OnEditingLockChanged(board.IsEditingLocked);
            SelectTool(0);
        }

        private void CreateColorButtons()
        {
            colorButtonTemplate.gameObject.SetActive(false);
            colorButtons = new Button[palette.Count];
            for (int i = 0; i < palette.Count; i++)
            {
                int index = i;
                Button button = Instantiate(colorButtonTemplate, colorButtonContainer, false);
                button.name = "SelectColor_" + i;
                // Replace the event so a cloned persistent listener cannot select another entry.
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => SelectColor(index));
                button.GetComponentInChildren<Text>(true).text = board.PlayerColorLabel(palette[i].colorId, palette[i].label);
                button.gameObject.SetActive(board.IsPlayerColorAvailable(palette[i].colorId));
                colorButtons[i] = button;
            }
            RefreshPlacementRules();
        }

        public void RefreshPlacementRules()
        {
            if (board == null || palette == null || colorButtons == null) return;
            for (int i = 0; i < colorButtons.Length; i++)
            {
                colorButtons[i].gameObject.SetActive(board.IsPlayerColorAvailable(palette[i].colorId));
                colorButtons[i].GetComponentInChildren<Text>(true).text = board.PlayerColorLabel(palette[i].colorId, palette[i].label);
            }
            if (!board.IsPlayerColorAvailable(palette[colorIndex].colorId))
                for (int i = 0; i < palette.Count; i++)
                    if (board.IsPlayerColorAvailable(palette[i].colorId)) { colorIndex = i; break; }
            RefreshButtons();
        }

        private void ConfigurationError()
        {
            Debug.LogError("请配置积木编辑器和按钮；每种颜色需有唯一 colorId、名称和独立材质；初始积木需位于合法且不重叠的单位格中心。", this);
            enabled = false;
        }

        public void SelectTool(int index)
        {
            if (index < 0 || index >= 3 || !CanEdit) return;
            CancelDrag();
            tool = (Tool)index;
            RefreshButtons();
            statusLabel.text = tool == Tool.Add ? $"添加 {palette[colorIndex].label}：点击放置，拖动旋转" :
                tool == Tool.Delete ? "点击删除最上层，拖动旋转" : "拖拽最上层积木，拖动空处旋转";
        }

        public void SelectColor(int index)
        {
            if (!CanEdit || index < 0 || index >= palette.Count || !board.IsPlayerColorAvailable(palette[index].colorId)) return;
            colorIndex = index;
            SelectTool((int)Tool.Add);
        }

        public void ClearBoard()
        {
            if (!CanEdit) return;
            SelectTool((int)Tool.Add);
            int count = board.Bricks.Count;
            for (int i = count - 1; i >= 0; i--)
                DeleteBrick(board.Bricks[i].BrickId);
            statusLabel.text = count == 0 ? "棋盘已经为空，可以继续添加。" : $"已清空 {count} 块积木，可继续添加。";
        }

        private void RefreshButtons()
        {
            for (int i = 0; i < toolButtons.Length; i++)
                toolButtons[i].image.color = i == (int)tool ? new Color(0.3f, 0.52f, 0.65f) : new Color(0.2f, 0.24f, 0.29f);
            for (int i = 0; i < colorButtons.Length; i++)
            {
                Color color = palette[i].material.color;
                colorButtons[i].image.color = i == colorIndex ? color : Color.Lerp(color, Color.gray, 0.65f);
                Color background = colorButtons[i].image.color;
                colorButtons[i].GetComponentInChildren<Text>().color =
                    0.299f * background.r + 0.587f * background.g + 0.114f * background.b > 0.5f ? Color.black : Color.white;
            }
        }

        private void Update()
        {
            if (board == null || !board.IsInitialized) return;
            if (!CanEdit) { CancelDrag(); return; }
            if (!UnityApplication.isFocused) { CancelDrag(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            { CancelDrag(); return; }
            Vector2 position = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) BeginPointerGesture(position);
            if (Input.GetMouseButtonUp(0)) EndPointerGesture(position);
            else if (Input.GetMouseButton(0)) UpdatePointerGesture(position);
            else if (boardPressed || draggedId != null) CancelDrag();
        }

        private void BeginPointerGesture(Vector2 position)
        {
            if (tool == Tool.Move) HandlePointerDown(position);
            if (draggedId != null || !pointer.TryProjectToBoard(position, out _)) return;
            boardPressed = true;
            pressPosition = position;
            lastOrbitX = position.x;
        }

        private void UpdatePointerGesture(Vector2 position)
        {
            if (draggedId != null) { UpdateDrag(position); return; }
            if (!boardPressed || (!orbiting && (position - pressPosition).sqrMagnitude <
                OrbitDragThreshold * OrbitDragThreshold)) return;

            orbiting = true;
            pointer.OrbitWorkspace(position.x - lastOrbitX);
            lastOrbitX = position.x;
        }

        private void EndPointerGesture(Vector2 position)
        {
            if (draggedId != null) { EndDrag(position); return; }
            UpdatePointerGesture(position);
            bool clicked = boardPressed && !orbiting && tool != Tool.Move;
            boardPressed = orbiting = false;
            if (clicked) HandlePointerDown(position);
        }

        private void HandlePointerDown(Vector2 position)
        {
            if (!CanEdit) return;
            if (!pointer.TryPickCell(position, out Vector2Int cell, out float surfaceHeight)) return;
            if (board.IsReserved(cell.x, cell.y))
            { statusLabel.text = "核心和出生点由系统固定，不能放置、删除或移动。"; return; }
            int height = board.StackHeight(cell.x, cell.y);
            if (tool == Tool.Add)
            {
                if (!board.TryAddAtLayer(cell.x, cell.y, height, palette[colorIndex].colorId, out var added))
                { statusLabel.text = board.LastPlacementIssue ?? "目标格不可放置，请选择其他位置。"; return; }
                statusLabel.text = $"已添加 {palette[colorIndex].label}：({cell.x + 1}, {cell.y + 1}) 第 {added.Layer + 1} 层";
                return;
            }
            var brick = board.AtLayer(cell.x, cell.y, Mathf.RoundToInt(surfaceHeight) - 1);
            if (brick == null) { statusLabel.text = "这里没有积木。"; return; }
            if (brick.Layer != height - 1) { statusLabel.text = "请先移走上层积木。"; return; }
            if (tool == Tool.Delete)
            {
                if (!DeleteBrick(brick.BrickId)) return;
                statusLabel.text = $"已删除：({cell.x + 1}, {cell.y + 1}) 第 {brick.Layer + 1} 层";
            }
            else
            {
                draggedId = brick.BrickId;
                dragSource = cell;
                UpdateDrag(position);
            }
        }

        private bool DeleteBrick(string id) => CanEdit && board.TryDelete(id);

        private void UpdateDrag(Vector2 position)
        {
            if (!CanEdit) { CancelDrag(); return; }
            hasDragTarget = pointer.TryPickCell(position, out dragTarget, out _);
            dragLayer = board.StackHeight(dragTarget.x, dragTarget.y) - (dragTarget == dragSource ? 1 : 0);
            bool valid = hasDragTarget && board.CanPlaceAtLayer(dragTarget.x, dragTarget.y, dragLayer, draggedId);
            pointer.SetStackDragPreview(dragTarget, valid, dragLayer);
            statusLabel.text = !hasDragTarget ? "目标在棋盘外或界面上，松开将取消移动。" :
                valid ? $"拖拽到 ({dragTarget.x + 1}, {dragTarget.y + 1}) 第 {dragLayer + 1} 层，松开放置。" :
                (board.LastPlacementIssue ?? "目标格不可放置，松开将取消移动。");
        }

        private void EndDrag(Vector2 position)
        {
            if (!CanEdit) { CancelDrag(); return; }
            UpdateDrag(position);
            string id = draggedId;
            bool moved = hasDragTarget && board.TryMoveToLayer(id, dragTarget.x, dragTarget.y, dragLayer);
            CancelDrag();
            statusLabel.text = moved ? $"已放置：({dragTarget.x + 1}, {dragTarget.y + 1}) 第 {dragLayer + 1} 层" : "移动已取消，积木保持原位。";
        }

        private void CancelDrag()
        {
            bool wasDragging = draggedId != null;
            draggedId = null;
            hasDragTarget = false;
            boardPressed = orbiting = false;
            if (pointer != null) pointer.ClearDragPreview();
            if (wasDragging && statusLabel != null) statusLabel.text = "移动已取消，积木保持原位。";
        }

        private void OnDisable() => CancelDrag();
        private void OnDestroy()
        {
            if (board != null) board.EditingLockChanged -= OnEditingLockChanged;
            SimulationEditAccess.FocusChanged -= OnEditingFocusChanged;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
    }
}
