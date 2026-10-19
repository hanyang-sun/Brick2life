using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Brick2Life.Simulation
{
    [DisallowMultipleComponent]
    public sealed class SimulationBrickEditor : MonoBehaviour
    {
        [Serializable] private struct PaletteEntry
        {
            public string colorId;
            public string label;
            public Material material;
        }

        private enum Tool { Add, Delete, Move }
        [SerializeField] private SimulationGridPointer pointer;
        [SerializeField] private Transform bricksRoot;
        [Tooltip("每种颜色填写唯一 colorId、显示名称和独立材质；按钮按列表自动生成。")]
        [SerializeField] private PaletteEntry[] palette;
        [SerializeField] private Button[] toolButtons;
        [SerializeField] private Button colorButtonTemplate;
        [SerializeField] private RectTransform colorButtonContainer;
        [SerializeField] private Text statusLabel;

        private readonly Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();
        private Button[] colorButtons;
        private SimulationLayout layout;
        private Tool tool;
        private int colorIndex;
        private string draggedId;
        private Vector3 grabOffset;
        private Vector2Int dragTarget;
        private bool hasDragTarget;

        public IReadOnlyList<SimulationLayout.Brick> Bricks => layout.Bricks;
        public int LayoutVersion => layout.Version;

        private void Start()
        {
            if (pointer == null || bricksRoot == null || statusLabel == null ||
                palette == null || palette.Length == 0 || toolButtons == null || toolButtons.Length != 3 ||
                colorButtonTemplate == null || colorButtonContainer == null ||
                colorButtonTemplate.GetComponentInChildren<Text>(true) == null)
            { ConfigurationError(); return; }
            var colors = new HashSet<string>();
            var materials = new HashSet<Material>();
            for (int i = 0; i < palette.Length; i++)
                if (palette[i].material == null || string.IsNullOrWhiteSpace(palette[i].colorId) ||
                    string.IsNullOrWhiteSpace(palette[i].label) || !colors.Add(palette[i].colorId) ||
                    !materials.Add(palette[i].material))
                { ConfigurationError(); return; }
            foreach (Button button in toolButtons)
                if (button == null) { ConfigurationError(); return; }

            layout = new SimulationLayout();
            foreach (Transform child in bricksRoot)
            {
                Renderer renderer = child.GetComponent<Renderer>();
                int index = Array.FindIndex(palette, entry => renderer != null && entry.material == renderer.sharedMaterial);
                Vector3 position = child.position;
                int x = Mathf.FloorToInt(position.x), y = Mathf.FloorToInt(position.z);
                if (index < 0 || child.GetComponent<Collider>() == null ||
                    (position - new Vector3(x + 0.5f, 0.5f, y + 0.5f)).sqrMagnitude > 0.0001f ||
                    !layout.TryAdd(x, y, palette[index].colorId, out SimulationLayout.Brick brick))
                { ConfigurationError(); return; }
                objects.Add(brick.BrickId, child.gameObject);
            }
            CreateColorButtons();
            SelectTool(0);
        }

        private void CreateColorButtons()
        {
            colorButtonTemplate.gameObject.SetActive(false);
            colorButtons = new Button[palette.Length];
            for (int i = 0; i < palette.Length; i++)
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
            if (index < 0 || index >= 3 || layout == null) return;
            CancelDrag();
            tool = (Tool)index;
            RefreshButtons();
            statusLabel.text = tool == Tool.Add ? $"添加 {palette[colorIndex].label}：点击空格" :
                tool == Tool.Delete ? "删除：点击要移除的积木" : "移动：按住积木拖拽，松开放置；Esc / 右键取消";
        }

        public void SelectColor(int index)
        {
            if (index < 0 || index >= palette.Length || layout == null) return;
            colorIndex = index;
            SelectTool((int)Tool.Add);
        }

        public void ClearBoard()
        {
            if (layout == null) return;
            SelectTool((int)Tool.Add);
            int count = layout.Bricks.Count;
            for (int i = count - 1; i >= 0; i--)
                DeleteBrick(layout.Bricks[i].BrickId);
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
            if (layout == null) return;
            if (!Application.isFocused) { CancelDrag(); return; }
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
            if (!pointer.TryPickCell(position, out Vector2Int cell, out _)) return;
            SimulationLayout.Brick brick = layout.At(cell.x, cell.y);
            if (tool == Tool.Add)
            {
                if (!layout.TryAdd(cell.x, cell.y, palette[colorIndex].colorId, out var added))
                { statusLabel.text = "目标格已有积木，请选择空格。"; return; }
                GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.name = "Brick_" + added.ColorId;
                instance.transform.SetParent(bricksRoot, false);
                instance.transform.position = Position(added);
                instance.GetComponent<Renderer>().sharedMaterial = palette[colorIndex].material;
                objects.Add(added.BrickId, instance);
                statusLabel.text = $"已添加 {palette[colorIndex].label}：({cell.x}, {cell.y})";
            }
            else if (tool == Tool.Delete)
            {
                if (brick == null) { statusLabel.text = "这里没有积木。"; return; }
                if (!DeleteBrick(brick.BrickId)) return;
                statusLabel.text = $"已删除：({cell.x}, {cell.y})";
            }
            else if (brick != null && pointer.TryProjectToBoard(position, out Vector3 ground))
            {
                draggedId = brick.BrickId;
                grabOffset = new Vector3(brick.GridX + 0.5f, 0, brick.GridY + 0.5f) - ground;
                UpdateDrag(position);
            }
        }

        private bool DeleteBrick(string id)
        {
            GameObject instance = objects[id];
            if (!layout.TryDelete(id)) return false;
            objects.Remove(id);
            // Destroy 延迟到帧末；立即停用，避免本帧射线再次命中已删除的积木。
            instance.SetActive(false);
            Destroy(instance);
            return true;
        }

        private void UpdateDrag(Vector2 position)
        {
            hasDragTarget = pointer.TryProjectToBoard(position, out Vector3 ground);
            if (hasDragTarget)
            {
                Vector3 center = ground + grabOffset;
                dragTarget = new Vector2Int(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z));
                hasDragTarget = SimulationLayout.Contains(dragTarget.x, dragTarget.y);
            }
            bool valid = hasDragTarget && layout.CanPlace(dragTarget.x, dragTarget.y, draggedId);
            pointer.SetDragPreview(hasDragTarget ? dragTarget : new Vector2Int(-1, -1), valid);
            statusLabel.text = !hasDragTarget ? "目标在棋盘外或界面上，松开将取消移动。" :
                valid ? $"拖拽到 ({dragTarget.x}, {dragTarget.y})，松开放置。" : "目标格已有积木，松开将取消移动。";
        }

        private void EndDrag(Vector2 position)
        {
            UpdateDrag(position);
            string id = draggedId;
            bool moved = hasDragTarget && layout.TryMove(id, dragTarget.x, dragTarget.y);
            if (moved) objects[id].transform.position = new Vector3(dragTarget.x + 0.5f, 0.5f, dragTarget.y + 0.5f);
            CancelDrag();
            statusLabel.text = moved ? $"已放置：({dragTarget.x}, {dragTarget.y})" : "移动已取消，积木保持原位。";
        }

        private static Vector3 Position(SimulationLayout.Brick brick) =>
            new Vector3(brick.GridX + 0.5f, brick.Layer + 0.5f, brick.GridY + 0.5f);

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
