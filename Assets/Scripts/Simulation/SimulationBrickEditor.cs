using System;
using System.Collections.Generic;
using Brick2Life.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityApplication = UnityEngine.Application;

namespace Brick2Life.Simulation
{
    [DisallowMultipleComponent]
    public sealed class SimulationBrickEditor : MonoBehaviour
    {
        private enum Tool { Add, Delete, Move }
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
        private bool locked;

        /// <summary>Battle and read-only display modes lock every edit path: mouse, tool buttons, colors and clearing.</summary>
        public bool Locked
        {
            get => locked;
            set
            {
                if (locked == value) return;
                locked = value;
                if (value) CancelDrag();
                RefreshInteractable();
                if (statusLabel != null)
                    statusLabel.text = value ? "已锁定：战斗或展示模式下不能修改积木。" : "已解锁，可以继续编辑积木。";
            }
        }

        private void RefreshInteractable()
        {
            if (toolButtons != null)
                foreach (Button button in toolButtons) if (button != null) button.interactable = !locked;
            if (colorButtons != null)
                foreach (Button button in colorButtons) if (button != null) button.interactable = !locked;
        }
        private Vector3 grabOffset;
        private Vector2Int dragTarget;
        private bool hasDragTarget;

        public void Bind(SimulationBoardSession session) => board = session ?? throw new ArgumentNullException(nameof(session));

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
                button.GetComponentInChildren<Text>(true).text = palette[i].label;
                button.gameObject.SetActive(true);
                colorButtons[i] = button;
            }
        }

        private void ConfigurationError()
        {
            Debug.LogError("请配置积木编辑器和按钮；每种颜色需有唯一 colorId、名称和独立材质；初始积木需位于合法且不重叠的单位格中心。", this);
            enabled = false;
        }

        public void SelectTool(int index)
        {
            if (index < 0 || index >= 3 || board == null || !board.IsInitialized) return;
            if (locked) { statusLabel.text = "战斗中不能切换积木编辑工具。"; return; }
            CancelDrag();
            tool = (Tool)index;
            RefreshButtons();
            statusLabel.text = tool == Tool.Add ? $"添加 {palette[colorIndex].label}：点击空格" :
                tool == Tool.Delete ? "删除：点击要移除的积木" : "移动：按住积木拖拽，松开放置；Esc / 右键取消";
        }

        public void SelectColor(int index)
        {
            if (board == null || !board.IsInitialized || index < 0 || index >= palette.Count) return;
            if (locked) { statusLabel.text = "战斗中不能更改积木颜色。"; return; }
            colorIndex = index;
            SelectTool((int)Tool.Add);
        }

        public void ClearBoard()
        {
            if (board == null || !board.IsInitialized) return;
            if (locked) { statusLabel.text = "战斗中不能清空积木。"; return; }
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
            if (locked)
            {
                if (draggedId != null) CancelDrag();
                return;
            }
            if (board == null || !board.IsInitialized) return;
            if (!UnityApplication.isFocused) { CancelDrag(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            { CancelDrag(); return; }
            Vector2 position = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) HandlePointerDown(position);
            if (draggedId == null) return;
            UpdateDrag(position);
            if (Input.GetMouseButtonUp(0)) EndDrag(position);
            else if (!Input.GetMouseButton(0)) CancelDrag();
        }

        private void HandlePointerDown(Vector2 position)
        {
            if (locked) return;
            if (!pointer.TryPickCell(position, out Vector2Int cell, out _)) return;
            BoardLayout.Brick brick = board.At(cell.x, cell.y);
            if (tool == Tool.Add)
            {
                if (!board.TryAdd(cell.x, cell.y, palette[colorIndex].colorId, out var added))
                { statusLabel.text = "目标格已有积木，请选择空格。"; return; }
                statusLabel.text = $"已添加 {palette[colorIndex].label}：({cell.x + 1}, {cell.y + 1})";
            }
            else if (tool == Tool.Delete)
            {
                if (brick == null) { statusLabel.text = "这里没有积木。"; return; }
                if (!DeleteBrick(brick.BrickId)) return;
                statusLabel.text = $"已删除：({cell.x + 1}, {cell.y + 1})";
            }
            else if (brick != null && pointer.TryProjectToBoard(position, out Vector3 ground))
            {
                draggedId = brick.BrickId;
                grabOffset = new Vector3(brick.GridX + 0.5f, 0, brick.GridY + 0.5f) - ground;
                UpdateDrag(position);
            }
        }

        private bool DeleteBrick(string id) => board.TryDelete(id);

        private void UpdateDrag(Vector2 position)
        {
            hasDragTarget = pointer.TryProjectToBoard(position, out Vector3 ground);
            if (hasDragTarget)
            {
                Vector3 center = ground + grabOffset;
                dragTarget = new Vector2Int(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z));
                hasDragTarget = BoardLayout.Contains(dragTarget.x, dragTarget.y);
            }
            bool valid = hasDragTarget && board.CanPlace(dragTarget.x, dragTarget.y, draggedId);
            pointer.SetDragPreview(hasDragTarget ? dragTarget : new Vector2Int(-1, -1), valid);
            statusLabel.text = !hasDragTarget ? "目标在棋盘外或界面上，松开将取消移动。" :
                valid ? $"拖拽到 ({dragTarget.x + 1}, {dragTarget.y + 1})，松开放置。" : "目标格已有积木，松开将取消移动。";
        }

        private void EndDrag(Vector2 position)
        {
            UpdateDrag(position);
            string id = draggedId;
            bool moved = hasDragTarget && board.TryMove(id, dragTarget.x, dragTarget.y);
            CancelDrag();
            statusLabel.text = moved ? $"已放置：({dragTarget.x + 1}, {dragTarget.y + 1})" : "移动已取消，积木保持原位。";
        }

        private void CancelDrag()
        {
            bool wasDragging = draggedId != null;
            draggedId = null;
            hasDragTarget = false;
            if (pointer != null) pointer.ClearDragPreview();
            if (wasDragging && statusLabel != null) statusLabel.text = "移动已取消，积木保持原位。";
        }

        private void OnDisable() => CancelDrag();
        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
    }
}
