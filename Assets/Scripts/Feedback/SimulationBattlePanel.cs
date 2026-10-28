using System;
using Brick2Life.Battle;
using Brick2Life.Core;
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
        private int waves = 3, perWave = 8, health = 24, coreDamage = 10;
        private float speed = 1.4f, interval = 1.1f;
        private int coreHealth = 100, towerDamage = 12;
        private float towerInterval = 0.45f;
        private bool synced;

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
                      source.State.Killed + " · 泄漏 " + source.State.Leaked + " · 阶段 " + source.State.Phase
                    : "");
            if (spawnText != null) spawnText.text = "入口：" + source.Spawns.Count + " 个";
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
                if (label != null) label.text = source.SelectSpawns ? "点棋盘选入口（开）" : "点棋盘选入口";
                spawnButton.interactable = !source.Running;
            }
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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            canvasObject.AddComponent<GraphicRaycaster>();

            var panelObject = new GameObject("BattlePanel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            var panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0.08f, 0.09f, 0.11f, 0.86f);
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(360, 0);
            rect.anchoredPosition = new Vector2(-12, 0);
            var layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 4;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddText(panelObject.transform, "战斗参数", 22, FontStyle.Bold);
            AddSlider(panelObject.transform, "波数", 1, BattleLimits.MaxWaves, waves, true, value => { waves = (int)value; ApplyParameters(); });
            AddSlider(panelObject.transform, "每波数量", 1, BattleLimits.MaxEnemiesPerWave, perWave, true, value => { perWave = (int)value; ApplyParameters(); });
            AddSlider(panelObject.transform, "敌人生命", 1, 500, health, true, value => { health = (int)value; ApplyParameters(); });
            AddSlider(panelObject.transform, "敌人速度", (float)BattleLimits.MinEnemySpeed, 10, speed, false, value => { speed = value; ApplyParameters(); });
            AddSlider(panelObject.transform, "核心伤害", 1, 100, coreDamage, true, value => { coreDamage = (int)value; ApplyParameters(); });
            AddSlider(panelObject.transform, "出怪间隔", (float)BattleLimits.MinSpawnInterval, 5, interval, false, value => { interval = value; ApplyParameters(); });
            AddSlider(panelObject.transform, "核心血量", 1, 500, coreHealth, true, value => { coreHealth = (int)value; ApplyParameters(); });
            AddSlider(panelObject.transform, "塔伤害", 1, 100, towerDamage, true, value => { towerDamage = (int)value; ApplyParameters(); });
            AddSlider(panelObject.transform, "塔攻击间隔", 0.1f, 5, towerInterval, false, value => { towerInterval = value; ApplyParameters(); });

            spawnText = AddText(panelObject.transform, "入口：0 个", 16);
            var spawnRow = AddRow(panelObject.transform);
            spawnButton = AddButton(spawnRow, "点棋盘选入口", () => { if (controller != null) controller.SelectSpawns = !controller.SelectSpawns; });
            AddButton(spawnRow, "清空入口", () => { if (controller != null) { controller.ClearSpawns(); controller.SelectSpawns = false; } });

            var startRow = AddRow(panelObject.transform);
            AddButton(startRow, "开始", () => { if (controller != null) controller.StartBattle(); });
            nextButton = AddButton(startRow, "下一波", () => { if (controller != null) controller.StartNextWave(); });
            pauseButton = AddButton(startRow, "暂停", () =>
            {
                if (controller == null || controller.State == null) return;
                controller.SetPaused(!controller.State.Paused);
            });
            AddButton(startRow, "重试", () => { if (controller != null) controller.Restart(); });

            statusText = AddText(panelObject.transform, "选择入口后开始战斗", 15);
        }

        private Text AddText(Transform parent, string content, int size, FontStyle style = FontStyle.Normal)
        {
            var item = new GameObject("Label");
            item.transform.SetParent(parent, false);
            var text = item.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.fontStyle = style;
            text.text = content; text.color = Color.white; text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            var layout = item.AddComponent<LayoutElement>();
            layout.minHeight = size + 8;
            return text;
        }

        private Transform AddRow(Transform parent)
        {
            var row = new GameObject("Row");
            row.transform.SetParent(parent, false);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var element = row.AddComponent<LayoutElement>();
            element.minHeight = 34;
            return row.transform;
        }

        private Button AddButton(Transform parent, string label, Action onClick)
        {
            var item = new GameObject("Button_" + label);
            item.transform.SetParent(parent, false);
            var image = item.AddComponent<Image>();
            image.color = new Color(0.22f, 0.28f, 0.36f, 1f);
            var button = item.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => { onClick(); if (controller != null) Refresh(controller); });
            var text = AddText(item.transform, label, 15);
            text.alignment = TextAnchor.MiddleCenter;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
            return button;
        }

        private void AddSlider(Transform parent, string label, float min, float max, float value, bool whole, Action<float> onChanged)
        {
            var row = new GameObject("Slider_" + label);
            row.transform.SetParent(parent, false);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().minHeight = 26;

            var labelText = AddText(row.transform, label, 14);
            labelText.rectTransform.sizeDelta = new Vector2(88, 24);
            labelText.GetComponent<LayoutElement>().minWidth = 88;
            labelText.GetComponent<LayoutElement>().preferredWidth = 88;

            var sliderObject = new GameObject("Slider");
            sliderObject.transform.SetParent(row.transform, false);
            var slider = sliderObject.AddComponent<Slider>();
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = whole; slider.value = value;
            sliderObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var valueText = AddText(row.transform, Format(value, whole), 14);
            valueText.rectTransform.sizeDelta = new Vector2(64, 24);
            valueText.GetComponent<LayoutElement>().minWidth = 64;
            valueText.alignment = TextAnchor.MiddleRight;
            slider.onValueChanged.AddListener(current =>
            {
                valueText.text = Format(current, whole);
                onChanged(current);
            });
        }

        private static string Format(float value, bool whole) => whole ? ((int)value).ToString() : value.ToString("0.0#");
    }
}
