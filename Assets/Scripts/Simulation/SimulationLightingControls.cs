using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Brick2Life.Simulation
{
    /// <summary>Sidebar popup reusing the existing UI font and button template.</summary>
    [DisallowMultipleComponent]
    public sealed class SimulationLightingControls : MonoBehaviour
    {
        [SerializeField] private SimulationLightingSettings settings;
        [SerializeField] private Button buttonTemplate;
        [SerializeField] private Text previewDescription;
        private Button opener, shadowButton;
        private RectTransform panel;
        private ScrollRect scroll;
        private Slider balanceSlider, ambientSlider;
        private Text balanceLabel, ambientLabel;
        private readonly List<Button> layoutButtons = new List<Button>();
        private readonly List<Button> brightnessButtons = new List<Button>();
        private bool originalDescriptionEnabled;

        private void Start()
        {
            if (settings == null || buttonTemplate == null || previewDescription == null) return;
            originalDescriptionEnabled = previewDescription.enabled;
            Transform canvas = previewDescription.transform.parent;
            opener = MakeButton("LightingMenu", canvas, "光照设置", TogglePanel);
            RectTransform source = previewDescription.rectTransform;
            RectTransform rect = (RectTransform)opener.transform;
            rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot; rect.anchoredPosition = source.anchoredPosition;
            rect.sizeDelta = source.sizeDelta;
            previewDescription.enabled = false;

            panel = new GameObject("LightingPanel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.gameObject.layer = buttonTemplate.gameObject.layer;
            panel.SetParent(canvas, false);
            panel.anchorMin = new Vector2(0.72f, 0); panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(18, 18); panel.offsetMax = new Vector2(-18, -138);
            panel.GetComponent<Image>().color = new Color(0.10f, 0.13f, 0.17f, 1);
            Label(panel, "光照实验", 12, 30, 20);
            RectTransform content = MakeScrollContent();
            Label(content, "白光方向", 0, 22, 14);
            string[] names = { "左侧", "右侧", "双侧" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                Button button = MakeButton("LightLayout_" + i, content, names[i], () => settings.SetLayout(index));
                Place((RectTransform)button.transform, 28, 32, i / 3f, (i + 1) / 3f);
                layoutButtons.Add(button);
            }
            Label(content, "亮度", 74, 22, 14);
            string[] levels = { "低", "标准", "高" };
            for (int i = 0; i < levels.Length; i++)
            {
                int index = i;
                Button button = MakeButton("LightBrightness_" + i, content, levels[i], () => settings.SetBrightness(index));
                Place((RectTransform)button.transform, 102, 32, i / 3f, (i + 1) / 3f);
                brightnessButtons.Add(button);
            }
            balanceLabel = Label(content, "左右比例", 148, 22, 14);
            balanceSlider = MakeSlider("LightBalance", content, 178, 100, true, value => settings.SetLeftLightShare(value / 100));
            Label(content, "比例仅在双侧模式生效", 208, 20, 12);
            ambientLabel = Label(content, "环境光", 244, 22, 14);
            ambientSlider = MakeSlider("AmbientMultiplier", content, 274, 3, false, settings.SetAmbientMultiplier);
            Label(content, "0 表示关闭环境填充光", 304, 20, 12);
            shadowButton = MakeButton("LightShadows", content, "阴影", settings.ToggleShadows);
            Place((RectTransform)shadowButton.transform, 344, 32);
            Button reference = MakeButton("LightReference", content, "方向光对照", () => settings.SetLayout(3));
            Place((RectTransform)reference.transform, 390, 32);
            layoutButtons.Add(reference);
            Button reset = MakeButton("ResetLightBalanceAmbient", content, "恢复比例和环境光", settings.ResetBalanceAndAmbient);
            Place((RectTransform)reset.transform, 436, 32);
            Label(content, "拖动立即生效。\n保存时会记录光照。", 482, 42, 12);
            Button close = MakeButton("CloseLighting", panel, "返回相机预览", TogglePanel);
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = Vector2.zero; closeRect.anchorMax = Vector2.right;
            closeRect.offsetMin = new Vector2(6, 10); closeRect.offsetMax = new Vector2(-6, 46);
            panel.gameObject.SetActive(false);
            Refresh();
        }

        private void OnEnable()
        {
            if (settings != null) settings.Changed += Refresh;
            if (opener != null) { opener.gameObject.SetActive(true); previewDescription.enabled = false; Refresh(); }
        }
        private void OnDisable()
        {
            if (settings != null) settings.Changed -= Refresh;
            if (opener != null) opener.gameObject.SetActive(false);
            if (panel != null) panel.gameObject.SetActive(false);
            if (opener != null && previewDescription != null) previewDescription.enabled = originalDescriptionEnabled;
        }
        private void OnDestroy()
        {
            if (opener != null) Destroy(opener.gameObject);
            if (panel != null) Destroy(panel.gameObject);
        }
        private void TogglePanel()
        {
            panel.gameObject.SetActive(!panel.gameObject.activeSelf);
            if (panel.gameObject.activeSelf) { panel.SetAsLastSibling(); scroll.verticalNormalizedPosition = 1; }
        }
        private void Refresh()
        {
            if (opener == null) return;
            string[] layouts = { "左侧", "右侧", "双侧", "方向光" };
            string[] levels = { "低", "标准", "高" };
            opener.GetComponentInChildren<Text>().text = "光照：" + layouts[(int)settings.CurrentLayout] + " · " + levels[(int)settings.CurrentBrightness];
            for (int i = 0; i < layoutButtons.Count; i++) Highlight(layoutButtons[i], i == (int)settings.CurrentLayout);
            for (int i = 0; i < brightnessButtons.Count; i++) Highlight(brightnessButtons[i], i == (int)settings.CurrentBrightness);
            shadowButton.GetComponentInChildren<Text>().text = settings.ShadowsEnabled ? "阴影：开启" : "阴影：关闭";
            Highlight(shadowButton, settings.ShadowsEnabled);
            balanceSlider.SetValueWithoutNotify(settings.LeftLightShare * 100);
            ambientSlider.SetValueWithoutNotify(settings.AmbientMultiplier);
            balanceSlider.interactable = settings.CurrentLayout == SimulationLightingSettings.Layout.Dual;
            int leftPercent = Mathf.RoundToInt(settings.LeftLightShare * 100);
            balanceLabel.text = "左右比例 " + leftPercent + " : " + (100 - leftPercent);
            balanceLabel.color = balanceSlider.interactable ? Color.white : new Color(0.6f, 0.65f, 0.7f);
            ambientLabel.text = "环境光 " + settings.AmbientMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + " 倍";
        }
        private static void Highlight(Button button, bool selected) =>
            button.image.color = selected ? new Color(0.3f, 0.52f, 0.65f) : new Color(0.2f, 0.24f, 0.29f);

        private Button MakeButton(string name, Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            Button button = Instantiate(buttonTemplate, parent, false);
            button.name = name;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
            Text label = button.GetComponentInChildren<Text>(true);
            label.text = text; label.color = Color.white; label.fontSize = 14;
            button.image.color = new Color(0.2f, 0.24f, 0.29f);
            button.gameObject.SetActive(true);
            return button;
        }
        private Text Label(Transform parent, string text, float top, float height, int size)
        {
            Text label = new GameObject("LightingLabel", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.gameObject.layer = buttonTemplate.gameObject.layer;
            label.transform.SetParent(parent, false);
            label.font = buttonTemplate.GetComponentInChildren<Text>(true).font;
            label.text = text; label.fontSize = size; label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 10; label.resizeTextMaxSize = size;
            Place(label.rectTransform, top, height);
            return label;
        }

        private RectTransform MakeScrollContent()
        {
            RectTransform body = Rect("LightingScroll", panel);
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(0, 58); body.offsetMax = new Vector2(0, -52);
            scroll = body.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Rect("LightingViewport", body);
            viewport.anchorMax = Vector2.one; viewport.offsetMax = new Vector2(-12, 0);
            viewport.gameObject.AddComponent<Image>().color = panel.GetComponent<Image>().color;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("LightingContent", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); content.sizeDelta = new Vector2(0, 534);
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            RectTransform track = Rect("LightingScrollbar", body);
            track.anchorMin = Vector2.right; track.anchorMax = Vector2.one;
            track.offsetMin = new Vector2(-7, 0); track.offsetMax = new Vector2(-1, 0);
            track.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.20f, 0.25f);
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            RectTransform handle = Rect("Handle", track);
            handle.anchorMax = Vector2.one;
            Image image = handle.gameObject.AddComponent<Image>(); image.color = new Color(0.4f, 0.56f, 0.65f);
            bar.handleRect = handle; bar.targetGraphic = image; bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        private Slider MakeSlider(string name, Transform parent, float top, float maximum, bool wholeNumbers,
            UnityEngine.Events.UnityAction<float> changed)
        {
            RectTransform root = Rect(name, parent);
            Place(root, top, 26);
            root.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.18f, 0.23f);
            Slider slider = root.gameObject.AddComponent<Slider>();
            RectTransform fill = Rect("Fill", root);
            fill.anchorMin = new Vector2(0, 0.4f); fill.anchorMax = new Vector2(1, 0.6f);
            fill.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.52f, 0.65f);
            RectTransform handleArea = Rect("HandleArea", root);
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(9, 0); handleArea.offsetMax = new Vector2(-9, 0);
            RectTransform handle = Rect("Handle", handleArea);
            handle.anchorMax = Vector2.up; handle.sizeDelta = new Vector2(18, -4);
            Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(0.75f, 0.85f, 0.9f);
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            slider.minValue = 0; slider.maxValue = maximum; slider.wholeNumbers = wholeNumbers;
            slider.onValueChanged.AddListener(changed);
            return slider;
        }

        private RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = buttonTemplate.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }
        private static void Place(RectTransform rect, float top, float height, float left = 0, float right = 1)
        {
            rect.anchorMin = new Vector2(left, 1); rect.anchorMax = new Vector2(right, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(6, -top - height); rect.offsetMax = new Vector2(-6, -top);
        }
    }
}
