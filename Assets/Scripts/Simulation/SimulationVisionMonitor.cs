using System;
using System.Globalization;
using System.Text;
using Brick2Life.Application;
using Brick2Life.Core;
using Brick2Life.Vision;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Brick2Life.Simulation
{
    // Screen overlay for the recognition matrix. It is not drawn into the overhead camera.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class SimulationVisionMonitor : MonoBehaviour
    {
        private const int CellSize = 36;
        [SerializeField] private float interval = 0.35f;
        [SerializeField] private int requiredFrames = 3;
        private SimulationBoardSession board;
        private SimulationCameraCapture capture;
        private VisionCalibration calibration;
        private LayoutChangeTracker tracker;
        private BoardSyncSession sync;
        private Font uiFont;
        private Font matrixFont;
        private GameObject panel;
        private Button toggleButton;
        private Text toggleLabel;
        private Text titleText;
        private Text accuracyText;
        private Text feedbackText;
        private Text eventText;
        private ScrollRect eventScroll;
        private bool stickToLatest = true;
        private bool applyingScroll;
        private bool logDrag;
        private Vector2 logDragPointer;
        private Text[,] matrixCells;
        private RectTransform matrixRoot;
        private string report = "等待识别";
        private float elapsed;
        private bool primed;
        private string loggedError;

        public bool Automatic { get; set; } = true;

        // Task C integration surface: one shared BoardSyncSession, confirmed updates, and the status line.
        public BoardSyncSession Session { get { return sync; } }
        public Text FeedbackText { get { return feedbackText; } }
        public event Action<BoardUpdate> UpdateApplied;
        public event Action<BoardSyncSession> SessionRebuilt;
        public event Action UiReady;

        public void SetFeedbackStatus(string text)
        {
            if (feedbackText != null) feedbackText.text = text ?? string.Empty;
        }

        public bool IsPanelVisible => panel != null && panel.activeSelf;
        public string ReportText => report;
        public string LastError { get; private set; }
        public RecognitionScore LastRawScore { get; private set; }
        public RecognitionScore LastStableScore { get; private set; }
        public UpdateOutcome? LastOutcome { get; private set; }
        public bool HasBaseline => tracker != null && tracker.HasBaseline;
        public System.Collections.Generic.IReadOnlyList<LayoutChangeEvent> Events => tracker == null ? null : tracker.Events;

        public float MatrixRowWidth
        {
            get { return matrixRoot == null || matrixRoot.childCount == 0 ? 0 : ((RectTransform)matrixRoot.GetChild(0)).rect.width; }
        }

        public float MatrixRowWidthDelta
        {
            get
            {
                if (matrixRoot == null || matrixRoot.childCount == 0) return -1;
                float min = float.MaxValue, max = 0;
                for (int i = 0; i < matrixRoot.childCount; i++)
                {
                    float width = ((RectTransform)matrixRoot.GetChild(i)).rect.width;
                    if (width < min) min = width;
                    if (width > max) max = width;
                }
                return max - min;
            }
        }

        public void Bind(SimulationBoardSession session, SimulationCameraCapture source)
        {
            board = session ?? throw new ArgumentNullException(nameof(session));
            capture = source ?? throw new ArgumentNullException(nameof(source));
            Rebuild();
        }

        public void Configure(int consistentFrames, bool automatic)
        {
            if (board == null || calibration == null) throw new InvalidOperationException("识别面板尚未绑定。");
            if (consistentFrames < 1) throw new ArgumentOutOfRangeException(nameof(consistentFrames));
            requiredFrames = consistentFrames;
            Automatic = automatic;
            Rebuild();
        }

        public void SetPanelVisible(bool visible)
        {
            if (panel == null) return;
            panel.SetActive(visible);
            if (toggleLabel != null) toggleLabel.text = visible ? "关闭识别" : "识别结果";
        }

        private void Rebuild()
        {
            calibration = CreateCalibration(board);
            tracker = new LayoutChangeTracker(requiredFrames);
            sync = new BoardSyncSession(board.Definition, calibration.CalibrationId);
            LastOutcome = null;
            LastRawScore = null;
            LastStableScore = null;
            LastError = null;
            // Task C binds to this session so recognition and feedback share one confirmed state.
            Action<BoardSyncSession> rebuilt = SessionRebuilt;
            if (rebuilt != null) rebuilt(sync);
        }

        private void Start()
        {
            if (board == null || capture == null) { enabled = false; Debug.LogError("识别面板未绑定棋盘或相机。", this); return; }
            CreateUi();
            Publish(null, null);
        }

        private void Update()
        {
            ScrollEventLogFromPointer();
            // F8 switches between interference-free sampling and sampling that includes task C's projection.
            if (Input.GetKeyDown(KeyCode.F8) && capture != null)
            {
                capture.CaptureWithoutProjection = !capture.CaptureWithoutProjection;
                Debug.Log("识别取样" + (capture.CaptureWithoutProjection ? "关闭" : "保留") + "虚拟投影（F8 切换）。", this);
            }
            if (!Automatic || board == null) return;
            elapsed += Time.unscaledDeltaTime;
            if (primed && elapsed < interval) return;
            primed = true;
            elapsed = 0;
            try { AnalyzeOnce(); loggedError = null; }
            catch (Exception error)
            {
                LastError = error.Message;
                report = "识别失败：" + error.Message;
                if (titleText != null) titleText.text = report;
                if (loggedError == error.Message) return;
                loggedError = error.Message;
                Debug.LogException(error, this);
            }
        }

        public void AnalyzeOnce()
        {
            if (board == null || !board.IsInitialized || capture == null || calibration == null)
                throw new InvalidOperationException("识别面板尚未绑定棋盘和相机。");
            SimulationObservationImage image = capture.CaptureObservationImage();
            // The snapshot is only the accuracy reference. Recognize does not receive it.
            BoardSnapshot truth = board.Snapshot();
            GridSample[][] samples = capture.BuildTopSamples(board.Definition, calibration, image.Width, image.Height);
            var rgb = new RgbImage(image.Width, image.Height, image.Rgb);
            FrameObservation observation = FixedGridRecognizer.Recognize(board.Definition, rgb, samples, calibration,
                "pending", "pending", 0, image.CapturedAtUtc);
            LastRawScore = RecognitionScore.Compare(observation, truth);
            LayoutTrackStep step = tracker.Observe(observation);
            LastStableScore = step.StableCells == null ? null : RecognitionScore.CompareCells(board.Definition, step.StableCells, truth);
            if (step.Confirmable)
            {
                string frameId = "vision_" + image.CapturedAtUtc.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
                ObservationRequest request = sync.BeginFrame(frameId, image.CapturedAtUtc);
                var stamped = new FrameObservation(observation.Definition, request.FrameId, request.SessionId, request.Sequence,
                    request.CapturedAtUtc, request.CalibrationId, observation.Cells);
                BoardUpdate update = sync.Confirm(request, stamped);
                LastOutcome = update.Outcome;
                // Task C consumes the confirmed prediction event; no ground truth is forwarded.
                Action<BoardUpdate> applied = UpdateApplied;
                if (applied != null) applied(update);
            }
            LastError = null;
            Publish(observation, step);
        }

        private void Publish(FrameObservation observation, LayoutTrackStep step)
        {
            var text = new StringBuilder();
            string title = "任务B  积木识别";
            text.AppendLine(title);
            if (observation == null || !observation.Accepted)
            {
                string reason = observation == null ? "等待第一帧相机图像。" : "本帧未输出矩阵：" + observation.RejectionReason;
                text.AppendLine(reason);
                ClearMatrix();
                ApplyText(titleText, reason);
                ApplyText(accuracyText, "");
            }
            else
            {
                FillMatrix(observation, text);
                ApplyText(titleText, title);
            }
            var accuracy = new StringBuilder();
            if (LastRawScore != null)
            {
                string line = Accuracy("单帧", LastRawScore);
                accuracy.Append(line);
                text.AppendLine(line);
            }
            if (tracker == null || !tracker.HasBaseline)
            {
                string line = "确认 " + (step == null ? 0 : step.Streak) + "/" + requiredFrames;
                if (accuracy.Length > 0) accuracy.Append("    ");
                accuracy.Append(line);
                text.AppendLine(line);
            }
            else
            {
                if (LastStableScore != null)
                {
                    string line = Accuracy("确认", LastStableScore);
                    if (accuracy.Length > 0) accuracy.Append("    ");
                    accuracy.Append(line);
                    text.AppendLine(line);
                }
                if (step != null && !step.Confirmable)
                {
                    string line = "待确认 " + step.Streak + "/" + requiredFrames;
                    if (accuracy.Length > 0) accuracy.Append("    ");
                    accuracy.Append(line);
                    text.AppendLine(line);
                }
            }
            if (observation != null && observation.Accepted) ApplyText(accuracyText, accuracy.ToString());
            var events = new StringBuilder();
            if (tracker == null || tracker.Events.Count == 0)
            {
                string line = tracker != null && tracker.HasBaseline ? "（尚无增删移动）" : "（连续帧确认后才记录）";
                events.Append(line);
                text.AppendLine(line);
            }
            else
            {
                for (int i = 0; i < tracker.Events.Count; i++)
                {
                    string line = tracker.Events[i].ToString();
                    events.AppendLine(line);
                    text.AppendLine(line);
                }
            }
            report = text.ToString();
            ApplyEventLog(events.ToString());
        }

        private void ApplyEventLog(string value)
        {
            bool follow = stickToLatest;
            applyingScroll = true;
            ApplyText(eventText, value);
            if (eventScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(eventScroll.content);
                if (follow) eventScroll.verticalNormalizedPosition = 0f;
            }
            applyingScroll = false;
        }

        // Wheel and middle-drag scroll the log without a raycast target, so left clicks still reach the board.
        private void ScrollEventLogFromPointer()
        {
            if (eventScroll == null || panel == null || !panel.activeInHierarchy) return;
            var area = (RectTransform)eventScroll.transform;
            Vector2 pointer = Input.mousePosition;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(area, pointer, null);
            if (over && Input.mouseScrollDelta.sqrMagnitude > 0.0001f && EventSystem.current != null)
            {
                var data = new PointerEventData(EventSystem.current);
                data.scrollDelta = Input.mouseScrollDelta;
                eventScroll.OnScroll(data);
                stickToLatest = eventScroll.verticalNormalizedPosition <= 0.02f;
            }
            if (Input.GetMouseButtonDown(2) && over)
            {
                logDrag = true;
                logDragPointer = pointer;
            }
            if (logDrag && Input.GetMouseButton(2))
            {
                float span = Mathf.Max(1f, eventScroll.content.rect.height - eventScroll.viewport.rect.height);
                float delta = (pointer.y - logDragPointer.y) / span;
                logDragPointer = pointer;
                applyingScroll = true;
                eventScroll.verticalNormalizedPosition = Mathf.Clamp01(eventScroll.verticalNormalizedPosition - delta);
                applyingScroll = false;
                stickToLatest = eventScroll.verticalNormalizedPosition <= 0.02f;
            }
            if (!Input.GetMouseButton(2)) logDrag = false;
        }

        private void FillMatrix(FrameObservation observation, StringBuilder text)
        {
            if (matrixCells == null) return;
            for (int y = 0; y < board.Definition.Height; y++)
            {
                text.Append(y + 1).Append(' ');
                for (int x = 0; x < board.Definition.Width; x++)
                {
                    string symbol = Symbol(observation.Cells[board.Definition.Index(x, y)].State);
                    Text cell = matrixCells[x, y];
                    if (cell != null)
                    {
                        cell.text = symbol;
                        cell.color = SymbolColor(symbol);
                    }
                    text.Append(symbol).Append(' ');
                }
                text.AppendLine();
            }
        }

        private void ClearMatrix()
        {
            if (matrixCells == null) return;
            foreach (Text cell in matrixCells)
                if (cell != null) { cell.text = ""; cell.color = SymbolColor("."); }
        }

        private static string Accuracy(string label, RecognitionScore score)
        {
            var text = new StringBuilder();
            text.Append(label).Append(' ').Append(score.ExactMatches).Append('/').Append(score.Total);
            if (score.OccupancyMatches != score.ExactMatches)
                text.Append("（占用 ").Append(score.OccupancyMatches).Append('/').Append(score.Total).Append('）');
            if (score.MismatchCount > 0 && score.Mismatches.Count > 0) text.Append("  ").Append(score.Mismatches[0]);
            return text.ToString();
        }

        private static string Symbol(BoardCell cell)
        {
            if (cell == null || cell.Occupancy == Occupancy.Unknown) return "?";
            if (cell.Occupancy == Occupancy.Empty) return "·";
            switch (cell.ColorId)
            {
                case "red": return "R";
                case "green": return "G";
                case "blue": return "B";
                case "yellow": return "Y";
                default: return "O";
            }
        }

        private static Color SymbolColor(string symbol)
        {
            switch (symbol)
            {
                case "R": return new Color(1f, 0.55f, 0.5f, 1f);
                case "G": return new Color(0.45f, 0.95f, 0.55f, 1f);
                case "B": return new Color(0.55f, 0.75f, 1f, 1f);
                case "Y": return new Color(1f, 0.88f, 0.35f, 1f);
                case "?": return new Color(1f, 1f, 1f, 0.95f);
                default: return new Color(0.82f, 0.85f, 0.88f, 0.9f);
            }
        }

        private static VisionCalibration CreateCalibration(SimulationBoardSession session)
        {
            var colors = new ColorSwatch[session.Palette.Count];
            var id = new StringBuilder("fixed-grid-v1");
            for (int i = 0; i < colors.Length; i++)
            {
                SimulationBoardSession.PaletteEntry entry = session.Palette[i];
                Color color = entry.material.color;
                var rgb = RgbColor.FromUnit(color.r, color.g, color.b);
                colors[i] = new ColorSwatch(entry.colorId, rgb);
                id.Append('|').Append(entry.colorId).Append('=').Append(rgb.R).Append(',').Append(rgb.G).Append(',').Append(rgb.B);
            }
            return new VisionCalibration(id.ToString(), new RgbColor(180, 180, 180), colors);
        }

        private void CreateUi()
        {
            uiFont = Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            matrixFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New" }, 20);
            if (matrixFont == null) matrixFont = uiFont;

            var canvasObject = new GameObject("VisionRecognitionCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            toggleButton = CreateButton(canvasObject.transform, "VisionToggle");
            var toggleRect = toggleButton.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0.5f, 1f);
            toggleRect.anchorMax = new Vector2(0.5f, 1f);
            toggleRect.pivot = new Vector2(0.5f, 1f);
            toggleRect.anchoredPosition = new Vector2(0, -12);
            toggleRect.sizeDelta = new Vector2(168, 36);
            toggleLabel = toggleButton.GetComponentInChildren<Text>();
            toggleLabel.text = "关闭识别";
            toggleButton.onClick.AddListener(() => SetPanelVisible(!IsPanelVisible));

            panel = new GameObject("VisionRecognitionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.30f, 0.12f);
            panelRect.anchorMax = new Vector2(0.70f, 0.88f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.06f, 0.08f, 0.1f, 0.58f);
            background.raycastTarget = false;
            var column = panel.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(20, 20, 16, 16);
            column.spacing = 8;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            titleText = CreateLine(panel.transform, "VisionTitle", 20, 30);
            accuracyText = CreateLine(panel.transform, "VisionAccuracy", 18, 28);
            // Task C feedback summary (level roles plus projection plan) is pushed in by the adapter.
            feedbackText = CreateLine(panel.transform, "VisionFeedback", 14, 26);
            matrixRoot = CreateMatrix(panel.transform);
            CreateBlock(panel.transform, "VisionEventTitle", uiFont, 18, TextAnchor.MiddleLeft, 28).text = "变化事件";
            eventText = CreateEventLog(panel.transform);
            SetPanelVisible(true);
            Action ready = UiReady;
            if (ready != null) ready();
        }

        private RectTransform CreateMatrix(Transform parent)
        {
            var rootObject = new GameObject("VisionMatrix", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            rootObject.transform.SetParent(parent, false);
            var root = rootObject.GetComponent<RectTransform>();
            var rows = rootObject.GetComponent<VerticalLayoutGroup>();
            rows.spacing = 2;
            rows.childAlignment = TextAnchor.MiddleCenter;
            rows.childControlWidth = true;
            rows.childControlHeight = true;
            rows.childForceExpandWidth = false;
            rows.childForceExpandHeight = false;
            var element = rootObject.GetComponent<LayoutElement>();
            element.preferredHeight = 11 * (CellSize + 2);
            element.minHeight = element.preferredHeight;
            element.flexibleHeight = 0;
            element.preferredWidth = 11 * CellSize;
            element.flexibleWidth = 0;

            matrixCells = new Text[BoardDefinition.Size, BoardDefinition.Size];
            CreateMatrixRow(root, -1);
            for (int y = BoardDefinition.Size - 1; y >= 0; y--) CreateMatrixRow(root, y);
            return root;
        }

        private void CreateMatrixRow(RectTransform root, int y)
        {
            var rowObject = new GameObject(y < 0 ? "VisionHeader" : "VisionRow" + y, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(root, false);
            var rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 0;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.preferredWidth = 11 * CellSize;
            rowElement.preferredHeight = CellSize;
            rowElement.minWidth = 11 * CellSize;

            CreateCell(rowObject.transform, y < 0 ? "" : (y + 1).ToString(), uiFont, false);
            for (int x = 0; x < BoardDefinition.Size; x++)
            {
                Text cell = CreateCell(rowObject.transform, y < 0 ? (x + 1).ToString() : "·", matrixFont, y >= 0);
                if (y >= 0) matrixCells[x, y] = cell;
            }
        }

        private Text CreateCell(Transform parent, string value, Font font, bool body)
        {
            var cellObject = new GameObject("Cell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            cellObject.transform.SetParent(parent, false);
            Image plate = cellObject.GetComponent<Image>();
            plate.color = body ? new Color(1f, 1f, 1f, 0.06f) : new Color(1f, 1f, 1f, 0f);
            plate.raycastTarget = false;
            var element = cellObject.GetComponent<LayoutElement>();
            element.preferredWidth = CellSize;
            element.preferredHeight = CellSize;
            element.minWidth = CellSize;
            element.minHeight = CellSize;
            var textObject = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(cellObject.transform, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = body ? 20 : 15;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.92f, 0.94f, 0.95f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.text = value;
            return text;
        }

        // Vertical list only. The matrix and status stay fixed; this viewport is the scroll surface.
        private Text CreateEventLog(Transform parent)
        {
            var host = new GameObject("VisionEventScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            host.transform.SetParent(parent, false);
            var hostElement = host.GetComponent<LayoutElement>();
            hostElement.preferredHeight = 140;
            hostElement.minHeight = 72;
            hostElement.flexibleHeight = 1;
            hostElement.flexibleWidth = 1;
            Image hostImage = host.GetComponent<Image>();
            hostImage.color = new Color(1f, 1f, 1f, 0f);
            hostImage.raycastTarget = false;
            eventScroll = host.GetComponent<ScrollRect>();
            eventScroll.horizontal = false;
            eventScroll.vertical = true;
            eventScroll.movementType = ScrollRect.MovementType.Clamped;
            eventScroll.scrollSensitivity = 30;
            eventScroll.inertia = true;
            eventScroll.onValueChanged.AddListener(position =>
            {
                if (!applyingScroll) stickToLatest = position.y <= 0.02f;
            });

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(host.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(-14, 0);
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textObject = new GameObject("VisionEvents", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(content.transform, false);
            Text text = textObject.GetComponent<Text>();
            text.font = uiFont;
            text.fontSize = 18;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            eventScroll.viewport = viewportRect;
            eventScroll.content = contentRect;

            var track = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            track.transform.SetParent(host.transform, false);
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = Vector2.right;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(-10, 0);
            trackRect.offsetMax = Vector2.zero;
            track.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            track.GetComponent<Image>().raycastTarget = false;
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(track.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            Image handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(0.75f, 0.86f, 0.92f, 0.45f);
            handleImage.raycastTarget = false;
            Scrollbar bar = track.GetComponent<Scrollbar>();
            bar.handleRect = handleRect;
            bar.targetGraphic = handleImage;
            bar.direction = Scrollbar.Direction.BottomToTop;
            eventScroll.verticalScrollbar = bar;
            eventScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return text;
        }

        private Text CreateLine(Transform parent, string name, int size, float height)
        {
            Text text = CreateBlock(parent, name, uiFont, size, TextAnchor.MiddleCenter, height);
            var element = text.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.flexibleHeight = 0;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Text CreateBlock(Transform parent, string name, Font font, int size, TextAnchor anchor, float height)
        {
            var block = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            block.transform.SetParent(parent, false);
            block.GetComponent<LayoutElement>().preferredHeight = height;
            block.GetComponent<LayoutElement>().minHeight = height;
            Text text = block.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            return text;
        }

        private Button CreateButton(Transform parent, string name)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.16f, 0.34f, 0.46f, 0.92f);
            image.raycastTarget = true;
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text label = labelObject.GetComponent<Text>();
            label.font = uiFont;
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            return buttonObject.GetComponent<Button>();
        }

        private static void ApplyText(Text text, string value)
        {
            if (text == null) return;
            if (text.font != null) text.font.RequestCharactersInTexture(value, text.fontSize);
            text.text = value;
        }
    }
}
