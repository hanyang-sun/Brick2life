using System;
using Brick2Life.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Brick2Life.Feedback
{
    /// <summary>What the workspace camera shows. Only Original allows editing the bricks.</summary>
    public enum SimulationDisplayMode { Original = 0, BricksAndProjection = 1, ProjectionOnly = 2 }

    /// <summary>
    /// Splits the editable layout from the two read-only showcase views. Switching modes only touches
    /// the workspace camera, layers and projection visibility, so recognition (top camera) never changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationDisplayController : MonoBehaviour
    {
        [SerializeField] private ProjectionSimulator projection;
        [SerializeField] private SimulationLevelAdapter adapter;
        [SerializeField] private SimulationBrickEditor editor;
        [SerializeField] private Camera workspaceCamera;
        [SerializeField] private string stageObjectName = "Board";
        [SerializeField] private string stageLayerName = "Brick2LifeStage";
        [SerializeField, Range(0, 31)] private int gameLayer = 30;
        [SerializeField] private bool buildToolbar = true;

        private int stageLayer = -1;
        private int baseMask;
        private Color baseBackground;
        private CameraClearFlags baseFlags;
        private bool cameraCaptured;
        private GameObject toolbar;
        private readonly Button[] buttons = new Button[3];

        public SimulationDisplayMode Mode { get; private set; } = SimulationDisplayMode.Original;
        public bool IsReadOnly => Mode != SimulationDisplayMode.Original;
        public int StageLayer => stageLayer;

        private void Awake()
        {
            if (projection == null) projection = FindFirstObjectByType<ProjectionSimulator>();
            if (adapter == null) adapter = FindFirstObjectByType<SimulationLevelAdapter>();
            if (editor == null) editor = FindFirstObjectByType<SimulationBrickEditor>();
            if (workspaceCamera == null) workspaceCamera = FindCamera("WorkspaceCamera");
            stageLayer = LayerMask.NameToLayer(stageLayerName);
            CaptureCameraState();
            ApplyStageLayers();
        }

        private void Start()
        {
            if (buildToolbar) CreateToolbar();
            Apply(SimulationDisplayMode.Original);
        }

        public void SetMode(SimulationDisplayMode mode)
        {
            if (mode == Mode) return;
            Apply(mode);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) SetMode(SimulationDisplayMode.Original);
            else if (Input.GetKeyDown(KeyCode.F2)) SetMode(SimulationDisplayMode.BricksAndProjection);
            else if (Input.GetKeyDown(KeyCode.F3)) SetMode(SimulationDisplayMode.ProjectionOnly);
        }

        private static Camera FindCamera(string name)
        {
            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (camera.name == name) return camera;
            return Camera.main;
        }

        private void CaptureCameraState()
        {
            if (cameraCaptured || workspaceCamera == null) return;
            cameraCaptured = true;
            baseMask = workspaceCamera.cullingMask;
            baseBackground = workspaceCamera.backgroundColor;
            baseFlags = workspaceCamera.clearFlags;
        }

        private void Apply(SimulationDisplayMode mode)
        {
            Mode = mode;
            if (projection != null) projection.SetVisible(mode != SimulationDisplayMode.Original);
            ApplyStageLayers();
            if (workspaceCamera != null)
            {
                if (mode == SimulationDisplayMode.ProjectionOnly)
                {
                    int mask = baseMask;
                    if (stageLayer >= 0) mask &= ~(1 << stageLayer);
                    if (gameLayer >= 0) mask &= ~(1 << gameLayer);
                    workspaceCamera.cullingMask = mask;
                    workspaceCamera.clearFlags = CameraClearFlags.SolidColor;
                    workspaceCamera.backgroundColor = Color.black;
                }
                else
                {
                    workspaceCamera.cullingMask = baseMask;
                    workspaceCamera.clearFlags = baseFlags;
                    workspaceCamera.backgroundColor = baseBackground;
                }
            }
            BrickEditGate.SetDisplay(mode != SimulationDisplayMode.Original);
            RefreshButtons();
            if (adapter != null) adapter.SetStatusText(StatusLine());
        }

        private string StatusLine() => Mode switch
        {
            SimulationDisplayMode.Original => "原始布局：可编辑积木（战斗与展示模式下自动锁定）",
            SimulationDisplayMode.BricksAndProjection => "展示：积木 + 投影（只读）",
            _ => "展示：仅投影（只读，黑底）"
        };

        // Bricks and the board move to the stage layer so the workspace camera can hide them without
        // touching the top camera that recognition uses.
        private void ApplyStageLayers()
        {
            if (stageLayer < 0) return;
            GameObject board = GameObject.Find(stageObjectName);
            if (board != null) SetLayerRecursively(board);
            var session = FindFirstObjectByType<SimulationBoardSession>();
            if (session != null && session.BricksRoot != null) SetLayerRecursively(session.BricksRoot.gameObject);
        }

        private void SetLayerRecursively(GameObject root)
        {
            root.layer = stageLayer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject);
        }

        private void CreateToolbar()
        {
            var canvasObject = new GameObject("DisplayModeCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var bar = new GameObject("DisplayModeBar");
            bar.transform.SetParent(canvasObject.transform, false);
            var image = bar.AddComponent<Image>();
            image.color = new Color(0.07f, 0.08f, 0.10f, 0.92f);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(430, 38);
            rect.anchoredPosition = new Vector2(0, 12);
            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var font = Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string[] labels = { "原始布局", "积木+投影", "仅投影" };
            for (int i = 0; i < labels.Length; i++)
            {
                SimulationDisplayMode mode = (SimulationDisplayMode)i;
                var item = new GameObject("Mode_" + labels[i]);
                item.transform.SetParent(bar.transform, false);
                var itemImage = item.AddComponent<Image>();
                itemImage.color = new Color(0.22f, 0.28f, 0.36f, 1f);
                var button = item.AddComponent<Button>();
                button.targetGraphic = itemImage;
                button.onClick.AddListener(() => SetMode(mode));
                item.AddComponent<LayoutElement>().minHeight = 28;
                var label = new GameObject("Text");
                label.transform.SetParent(item.transform, false);
                var text = label.AddComponent<Text>();
                text.font = font; text.fontSize = 14; text.text = labels[i];
                text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
                var textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero; textRect.anchoredPosition = Vector2.zero;
                buttons[i] = button;
            }
            toolbar = bar;
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                var image = buttons[i].targetGraphic as Image;
                if (image != null)
                    image.color = (int)Mode == i ? new Color(0.30f, 0.58f, 0.72f, 1f) : new Color(0.22f, 0.28f, 0.36f, 1f);
            }
        }
    }
}
