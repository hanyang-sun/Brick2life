using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Brick2Life.Simulation
{
    [DefaultExecutionOrder(100)]
    public sealed class SimulationGridPointer : MonoBehaviour
    {
        [SerializeField] private Camera workspaceCamera;
        [SerializeField] private Collider boardCollider;
        [SerializeField] private Transform bricksRoot;
        [SerializeField] private Text cellLabel;
        [SerializeField] private RectTransform outlineRoot;
        [SerializeField] private RectTransform[] outlineEdges = new RectTransform[4];
        [SerializeField, Min(0.5f)] private float outlineWidth = 2.5f;

        private readonly Vector2[] localCorners = new Vector2[4];
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private EventSystem raycastSystem;
        private PointerEventData pointerEvent;
        private bool dragPreview, dragValid;
        private Vector2Int dragCell;
        public Vector2Int HoveredCell { get; private set; } = new Vector2Int(-1, -1);

        private void Awake()
        {
            bool configured = workspaceCamera != null && boardCollider != null && bricksRoot != null &&
                cellLabel != null && outlineRoot != null && outlineEdges != null && outlineEdges.Length == 4;
            if (configured)
                for (int i = 0; i < 4; i++) configured &= outlineEdges[i] != null;
            if (!configured)
            {
                Debug.LogError("请配置操作相机、棋盘、积木根对象及格位提示 UI。", this);
                enabled = false;
                return;
            }
            ClearHover();
        }

        private void LateUpdate()
        {
            if (dragPreview)
            {
                if (Application.isFocused && SimulationLayout.Contains(dragCell.x, dragCell.y))
                    // 移动目标是棋盘格位，方框贴合棋盘而不是积木顶面。
                    ShowCell(dragCell, 0);
                else ClearHover();
                return;
            }
            if (!Application.isFocused || !Input.mousePresent ||
                !TryPickCell(Input.mousePosition, out Vector2Int cell, out float surfaceHeight))
            {
                ClearHover();
                return;
            }
            ShowCell(cell, surfaceHeight);
        }

        public bool TryPickCell(Vector2 screenPosition, out Vector2Int cell, out float surfaceHeight)
        {
            cell = new Vector2Int(-1, -1);
            surfaceHeight = 0;
            if (boardCollider == null || bricksRoot == null || !CanPick(screenPosition)) return false;

            Ray ray = workspaceCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, workspaceCamera.farClipPlane,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            bool isBrick = hit.collider.transform.IsChildOf(bricksRoot);
            if (hit.collider != boardCollider && !isBrick) return false;

            // 单位积木的侧面恰好位于整数边界，使用其中心确定所属格位。
            Vector3 position = isBrick ? hit.collider.bounds.center : hit.point;
            int gridX = Mathf.FloorToInt(position.x);
            int gridY = Mathf.FloorToInt(position.z);
            if (!SimulationLayout.Contains(gridX, gridY)) return false;
            cell = new Vector2Int(gridX, gridY);
            surfaceHeight = isBrick ? hit.collider.bounds.max.y : hit.point.y;
            return true;
        }

        // 拖拽目标投到固定棋盘平面，避免被积木自身或侧面干扰。
        public bool TryProjectToBoard(Vector2 screenPosition, out Vector3 position)
        {
            position = default;
            if (!CanPick(screenPosition)) return false;
            Ray ray = workspaceCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance) || distance > workspaceCamera.farClipPlane) return false;
            position = ray.GetPoint(distance);
            return true;
        }

        private bool CanPick(Vector2 position)
        {
            if (workspaceCamera == null || !workspaceCamera.pixelRect.Contains(position)) return false;
            EventSystem current = EventSystem.current;
            if (current == null) return true;
            if (raycastSystem != current)
            { raycastSystem = current; pointerEvent = new PointerEventData(current); }
            pointerEvent.Reset();
            pointerEvent.position = position;
            current.RaycastAll(pointerEvent, uiHits);
            return uiHits.Count == 0;
        }

        public void SetDragPreview(Vector2Int cell, bool valid)
        { dragPreview = true; dragCell = cell; dragValid = valid; }

        public void ClearDragPreview() => dragPreview = false;

        private void ShowCell(Vector2Int cell, float surfaceHeight)
        {
            cellLabel.text = dragPreview ? $"拖拽目标：({cell.x}, {cell.y})" : $"当前格位：({cell.x}, {cell.y})";
            HoveredCell = cell;
            outlineRoot.gameObject.SetActive(true);
            Color color = !dragPreview ? new Color(1, 0.75f, 0.15f) :
                dragValid ? new Color(0.3f, 1, 0.55f) : new Color(1, 0.3f, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                outlineEdges[i].GetComponent<Image>().color = color;
                float x = cell.x + (i == 1 || i == 2 ? 1 : 0);
                float z = cell.y + (i >= 2 ? 1 : 0);
                Vector3 screen = workspaceCamera.WorldToScreenPoint(new Vector3(x, surfaceHeight + 0.01f, z));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(outlineRoot, screen, null, out localCorners[i]);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector2 start = localCorners[i];
                Vector2 end = localCorners[(i + 1) % 4];
                Vector2 direction = end - start;
                outlineEdges[i].anchoredPosition = (start + end) * 0.5f;
                outlineEdges[i].sizeDelta = new Vector2(direction.magnitude, outlineWidth);
                outlineEdges[i].localRotation = Quaternion.Euler(0, 0,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
        }

        private void ClearHover()
        {
            HoveredCell = new Vector2Int(-1, -1);
            if (outlineRoot != null) outlineRoot.gameObject.SetActive(false);
            if (cellLabel != null) cellLabel.text = "将鼠标移到棋盘上";
        }

        private void OnDisable() => ClearHover();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ClearHover();
        }
    }
}
