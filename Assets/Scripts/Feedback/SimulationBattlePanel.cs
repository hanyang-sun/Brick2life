using System;
using Brick2Life.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Battle parameters and controls: every wave/tower value is editable, entrances are picked on the
    /// board, and start/pause/next/retry drive the controller. Built at runtime so the scene stays small.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationBattlePanel : MonoBehaviour
    {
        [SerializeField] private SimulationBattleController controller;
        [SerializeField] private bool visible = true;

        private Font font;
        private Text statusText;
        private Text spawnText;
        private Button pauseButton;
        private Button nextButton;
        private Button spawnButton;
        private Button clearSpawnButton;
        private int waves = 3, perWave = 8, health = 24, coreDamage = 10;
        private float speed = 1.4f, interval = 1.1f;
        private int coreHealth = 100, towerDamage = 12;
        private float towerInterval = 0.45f;
        private bool synced;
        private GameObject body;
        private GameObject selectionBar;
        private bool bodyVisibleBeforeSelect = true;

        public bool IsVisible => visible;

        private void Awake()
        {
            if (controller == null) controller = FindFirstObjectByType<SimulationBattleController>();
            font = Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Start()
        {
            CreateUi();
            SyncFromController();
            SetVisible(visible);
            if (controller != null) Refresh(controller);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null) canvas.gameObject.SetActive(value);
        }

        public void Refresh(SimulationBattleController source)
        {
            if (source == null) return;
            if (statusText != null)
                statusText.text = source.LastMessage + (source.State != null
                    ? "\n核心 " + source.State.CoreHealth + " · 第 " + source.State.Wave + " 波 · 击杀 " +
                      source.State.Killed + " · 泄漏 " + source.State.Leaked + " · " + source.State.Phase
                    : "");
            if (spawnText != null)
                spawnText.text = source.SelectSpawns
                    ? "入口：" + source.Spawns.Count + " 个 · 点亮中，点棋盘空格"
                    : "入口：" + source.Spawns.Count + " 个";
            if (pauseButton != null)
            {
                var label = pauseButton.GetComponentInChildren<Text>();
                if (label != null) label.text = source.State != null && source.State.Paused ? "继续" : "暂停";
                pauseButton.interactable = source.Running && source.State != null && !source.State.Finished;
            }
            if (nextButton != null)
                nextButton.interactable = source.Running && source.State != null && source.State.CanStartWave;
            if (spawnButton != null)
            {
                var label = spawnButton.GetComponentInChildren<Text>();
                if (label != null) label.text = source.SelectSpawns ? "选入口（开）" : "点棋盘选入口";
                spawnButton.interactable = !source.Running;
            }
            if (clearSpawnButton != null) clearSpawnButton.interactable = !source.Running;
        }

        private void SyncFromController()
        {
            if (controller == null) return;
            WavePlan plan = controller.WavePlan;
            TowerSettings towers = controller.TowerSettings;
            waves = plan.WaveCount; perWave = plan.EnemiesPerWave; health = plan.EnemyHealth;
            speed = (float)plan.EnemySpeed; coreDamage = plan.CoreDamage; interval = (float)plan.SpawnInterval;
            coreHealth = towers.InitialCoreHealth; towerDamage = towers.Damage; towerInterval = (float)towers.AttackInterval;
            synced = true;
        }

        private void ApplyParameters()
        {
            if (controller == null || !synced) return;
            controller.WavePlan = new WavePlan(waves, perWave, health, speed, coreDamage, interval);
            controller.TowerSettings = new TowerSettings(coreHealth, towerDamage, 8, towerInterval);
        }

        private void CreateUi()
        {
            var canvasObject = new GameObject("BattleCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            // Fixed pixel scale keeps the panel legible in small game views; the scene stays visible beside it.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var panelObject = new GameObject("BattlePanel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            body = panelObject;
            var panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0.07f, 0.08f, 0.10f, 0.93f);
            var rect = panel.rectTransform;
            // Left side: the recognition panel and the level UI both live on the right.
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.sizeDelta = new Vector2(380, 0);
            rect.anchoredPosition = new Vector2(12, 0);
            var layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 3;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddText(panelObject.transform, "战斗参数", 20, FontStyle.Bold);
            var grid = new GameObject("Grid");
            grid.transform.SetParent(panelObject.transform, false);
            var gridLayout = grid.AddComponent<VerticalLayoutGroup>();
            gridLayout.spacing = 2;
            gridLayout.childControlWidth = true;
            gridLayout.childControlHeight = true;
            gridLayout.childForceExpandWidth = true;
            gridLayout.childForceExpandHeight = false;
            Transform row = AddRow(grid.transform);
            AddCell(row, "波数", 1, BattleLimits.MaxWaves, waves, true, value => { waves = (int)value; ApplyParameters(); });
            AddCell(row, "每波数量", 1, BattleLimits.MaxEnemiesPerWave, perWave, true, value => { perWave = (int)value; ApplyParameters(); });

            row = AddRow(grid.transform);
            AddCell(row, "敌人生命", 1, 500, health, true, value => { health = (int)value; ApplyParameters(); });
            AddCell(row, "敌人速度", (float)BattleLimits.MinEnemySpeed, 10, speed, false, value => { speed = value; ApplyParameters(); });

            row = AddRow(grid.transform);
            AddCell(row, "核心伤害", 1, 100, coreDamage, true, value => { coreDamage = (int)value; ApplyParameters(); });
            AddCell(row, "出怪间隔", (float)BattleLimits.MinSpawnInterval, 5, interval, false, value => { interval = value; ApplyParameters(); });

            row = AddRow(grid.transform);
            AddCell(row, "核心血量", 1, 500, coreHealth, true, value => { coreHealth = (int)value; ApplyParameters(); });
            AddCell(row, "塔伤害", 1, 100, towerDamage, true, value => { towerDamage = (int)value; ApplyParameters(); });

            row = AddRow(grid.transform);
            AddCell(row, "塔攻击间隔", 0.1f, 5, towerInterval, false, value => { towerInterval = value; ApplyParameters(); });

            spawnText = AddText(panelObject.transform, "入口：0 个", 15);
            var spawnRow = AddRow(panelObject.transform);
            spawnButton = AddButton(spawnRow, "点棋盘选入口", () => { if (controller != null) controller.SelectSpawns = !controller.SelectSpawns; });
            clearSpawnButton = AddButton(spawnRow, "清空入口", () => { if (controller != null) { controller.ClearSpawns(); controller.SelectSpawns = false; } });

            var startRow = AddRow(panelObject.transform);
            AddButton(startRow, "开始", () => { if (controller != null) controller.StartBattle(); });
            nextButton = AddButton(startRow, "下一波", () => { if (controller != null) controller.StartNextWave(); });
            pauseButton = AddButton(startRow, "暂停", () =>
            {
                if (controller == null || controller.State == null) return;
                controller.SetPaused(!controller.State.Paused);
            });
            AddButton(startRow, "重试", () => { if (controller != null) controller.Restart(); });

            statusText = AddText(panelObject.transform, "选择入口后开始战斗", 14);

            // Always-on top-left switch so the panel can be collapsed to see the whole board.
            var toggle = AddButton(canvasObject.transform, "战斗面板 ▾", () => { if (body != null) body.SetActive(!body.activeSelf); });
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = new Vector2(0, 1); toggleRect.anchorMax = new Vector2(0, 1);
            toggleRect.pivot = new Vector2(0, 1);
            toggleRect.sizeDelta = new Vector2(130, 28);
            toggleRect.anchoredPosition = new Vector2(12, -12);

            var barObject = new GameObject("BattleSelectionBar");
            barObject.transform.SetParent(canvasObject.transform, false);
            var barImage = barObject.AddComponent<Image>();
            barImage.color = new Color(0.10f, 0.12f, 0.16f, 0.92f);
            var barRect = barImage.rectTransform;
            barRect.anchorMin = new Vector2(0, 1); barRect.anchorMax = new Vector2(0, 1);
            barRect.pivot = new Vector2(0, 1);
            barRect.sizeDelta = new Vector2(430, 34);
            barRect.anchoredPosition = new Vector2(12, -48);
            var barRow = barObject.AddComponent<HorizontalLayoutGroup>();
            barRow.padding = new RectOffset(10, 6, 4, 4);
            barRow.spacing = 8;
            barRow.childControlWidth = true; barRow.childControlHeight = true;
            barRow.childForceExpandWidth = false; barRow.childForceExpandHeight = false;
            AddText(barObject.transform, "点亮中：点棋盘空格选入口", 14).GetComponent<LayoutElement>().flexibleWidth = 1;
            var endButton = AddButton(barObject.transform, "结束选择", () => { if (controller != null) controller.SelectSpawns = false; });
            var endElement = endButton.GetComponent<LayoutElement>();
            endElement.minWidth = 96; endElement.preferredWidth = 96; endElement.flexibleWidth = 0;
            selectionBar = barObject;
            selectionBar.SetActive(false);
        }

        /// <summary>Hides the parameter body while entrances are being picked, keeping a small hint bar.</summary>
        public void SetSelecting(bool selecting)
        {
            if (selecting)
            {
                bodyVisibleBeforeSelect = body != null && body.activeSelf;
                if (body != null) body.SetActive(false);
                if (selectionBar != null) selectionBar.SetActive(true);
            }
            else
            {
                if (body != null) body.SetActive(bodyVisibleBeforeSelect);
                if (selectionBar != null) selectionBar.SetActive(false);
            }
        }

        private Text AddText(Transform parent, string content, int size, FontStyle style = FontStyle.Normal)
        {
            var item = new GameObject("Label");
            item.transform.SetParent(parent, false);
            var text = item.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.fontStyle = style;
            text.text = content; text.color = Color.white; text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            var element = item.AddComponent<LayoutElement>();
            element.minHeight = size + 6;
            return text;
        }

        private Transform AddRow(Transform parent)
        {
            var row = new GameObject("Row");
            row.transform.SetParent(parent, false);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().minHeight = 26;
            return row.transform;
        }

        // One slider cell: label + slider + value, sized to share a row between two cells.
        private GameObject AddCell(Transform row, string label, float min, float max, float value, bool whole, Action<float> onChanged)
        {
            var cell = new GameObject("Cell_" + label);
            cell.transform.SetParent(row, false);
            var cellLayout = cell.AddComponent<HorizontalLayoutGroup>();
            cellLayout.spacing = 4;
            cellLayout.childControlWidth = true;
            cellLayout.childControlHeight = true;
            cellLayout.childForceExpandWidth = false;
            cellLayout.childForceExpandHeight = false;
            cell.AddComponent<LayoutElement>().flexibleWidth = 1;

            var labelText = AddText(cell.transform, label, 13);
            labelText.rectTransform.sizeDelta = new Vector2(70, 22);
            var labelElement = labelText.GetComponent<LayoutElement>();
            labelElement.minWidth = 70; labelElement.preferredWidth = 70;

            var slider = CreateSlider(cell.transform, min, max, value, whole, onChanged);
            var valueText = AddText(cell.transform, Format(value, whole), 13);
            valueText.rectTransform.sizeDelta = new Vector2(42, 22);
            var valueElement = valueText.GetComponent<LayoutElement>();
            valueElement.minWidth = 42; valueElement.preferredWidth = 42;
            valueText.alignment = TextAnchor.MiddleRight;
            slider.onValueChanged.AddListener(current => valueText.text = Format(current, whole));
            return cell;
        }

        private Slider CreateSlider(Transform parent, float min, float max, float value, bool whole, Action<float> onChanged)
        {
            var sliderObject = new GameObject("Slider");
            sliderObject.transform.SetParent(parent, false);
            var sliderElement = sliderObject.AddComponent<LayoutElement>();
            sliderElement.flexibleWidth = 1; sliderElement.minHeight = 22;
            var slider = sliderObject.AddComponent<Slider>();
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = whole; slider.value = value;
            slider.direction = Slider.Direction.LeftToRight;

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(sliderObject.transform, false);
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.38f, 0.43f, 0.5f, 1f);
            var backgroundRect = backgroundImage.rectTransform;
            backgroundRect.anchorMin = new Vector2(0, 0.32f);
            backgroundRect.anchorMax = new Vector2(1, 0.68f);
            backgroundRect.offsetMin = Vector2.zero; backgroundRect.offsetMax = Vector2.zero;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObject.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.32f);
            fillAreaRect.anchorMax = new Vector2(1, 0.68f);
            fillAreaRect.offsetMin = new Vector2(2, 0); fillAreaRect.offsetMax = new Vector2(-2, 0);
            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.35f, 0.72f, 0.95f, 1f);
            var fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;

            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero; handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = Vector2.zero; handleAreaRect.offsetMax = Vector2.zero;
            var handle = new GameObject("Handle", typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(0.92f, 0.95f, 1f, 1f);
            var handleRect = handleImage.rectTransform;
            handleRect.sizeDelta = new Vector2(10, 0);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.onValueChanged.AddListener(current => onChanged(current));
            return slider;
        }

        private Button AddButton(Transform parent, string label, Action onClick)
        {
            var item = new GameObject("Button_" + label);
            item.transform.SetParent(parent, false);
            var image = item.AddComponent<Image>();
            image.color = new Color(0.22f, 0.28f, 0.36f, 1f);
            var button = item.AddComponent<Button>();
            button.targetGraphic = image;
            item.AddComponent<LayoutElement>().minHeight = 26;
            button.onClick.AddListener(() => { onClick(); if (controller != null) Refresh(controller); });
            var text = AddText(item.transform, label, 14);
            text.alignment = TextAnchor.MiddleCenter;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
            text.GetComponent<LayoutElement>().minHeight = 24;
            return button;
        }

        private static string Format(float value, bool whole) => whole ? ((int)value).ToString() : value.ToString("0.0#");
    }
}
