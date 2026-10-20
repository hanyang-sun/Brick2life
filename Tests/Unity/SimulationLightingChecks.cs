using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Runs only in a fresh project; uses the actual scene, materials, camera and capture component.
public static class SimulationLightingChecks
{
    [Serializable] private sealed class Result
    {
        public bool passed; public int assertions, pairs; public string error;
        public List<Measurement> measurements = new List<Measurement>();
    }
    [Serializable] private sealed class Measurement { public string name; public float mean, clippedFraction; public float[] graySamples; }
    [Serializable] private sealed class Frame
    {
        public int schemaVersion, layoutVersion;
        public string cameraConfigurationId, lightingConfigurationId;
        public SimulationLightingConfiguration lightingConfiguration;
        public Cell[] cells;
    }
    [Serializable] private sealed class Cell { public int gridX, gridY; public bool occupied; public string brickId, colorId; }
    private static Result result;
    private static string output, cameraId, cells;
    private static int startedAt;
    private static SimulationLightingSettings settings;
    private static SimulationCameraCapture capture;
    private static Camera camera;
    private static float originalShadowDistance;
    private static int originalPixelLights;
    private static ShadowQuality originalShadows;
    private static ShadowResolution originalResolution;
    private static Color originalAmbient;

    public static void Run()
    {
        result = new Result();
        output = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Brick2LifeSimulation.unity");
            SimulationLightingSetup.Install();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            originalShadowDistance = QualitySettings.shadowDistance;
            originalPixelLights = QualitySettings.pixelLightCount;
            originalShadows = QualitySettings.shadows;
            originalResolution = QualitySettings.shadowResolution;
            originalAmbient = RenderSettings.ambientLight;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(error); }
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        startedAt = Time.frameCount;
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (Time.frameCount < startedAt + 5) return;
        EditorApplication.update -= Tick;
        try { Verify(); result.passed = true; Finish(null); }
        catch (Exception error) { Finish(error); }
    }
    private static void Verify()
    {
        settings = UnityEngine.Object.FindFirstObjectByType<SimulationLightingSettings>();
        capture = UnityEngine.Object.FindFirstObjectByType<SimulationCameraCapture>();
        camera = GameObject.Find("TopCamera").GetComponent<Camera>();
        Check(!camera.orthographic && Mathf.Abs(camera.fieldOfView - 50) < 0.001f, "Existing overhead perspective was changed.");
        Check(UnityEngine.Object.FindFirstObjectByType<SimulationBrickEditor>().enabled, "Brick editor failed to start.");
        Check(QualitySettings.shadows == ShadowQuality.All && QualitySettings.pixelLightCount >= 2, "Play-mode shadow quality missing.");
        Check(GameObject.Find("LightingMenu") != null, "Lighting UI did not start.");
        var images = new Dictionary<string, Color32[]>();
        var ids = new HashSet<string>();
        for (int side = 0; side < 2; side++)
        {
            settings.SetLayout(side == 0 ? 0 : 2);
            float previousMean = 0;
            for (int brightness = 0; brightness < 3; brightness++)
            {
                settings.SetBrightness(brightness);
                for (int shadows = 0; shadows < 2; shadows++)
                {
                    if (settings.ShadowsEnabled != (shadows == 1)) settings.ToggleShadows();
                    string name = (side == 0 ? "left" : "dual") + "-" + brightness + "-" + (shadows == 1 ? "shadows" : "clear");
                    Frame frame = Save(name, out Color32[] pixels);
                    images.Add(name, pixels);
                    Check(ids.Add(frame.lightingConfigurationId), "Different lighting presets reused an ID.");
                    var enabled = frame.lightingConfiguration.lights.Where(light => light.active && light.enabled).ToArray();
                    Check(enabled.Length == (side == 0 ? 1 : 2), "Wrong enabled light count.");
                    Check(enabled.All(light => light.type == "Spot" && light.color == Color.white), "Experiment does not use white Spot lights.");
                    Check(Mathf.Abs(enabled.Sum(light => light.intensity) - settings.IntensityBudget) < 0.0001f, "Intensity budget changed across light count.");
                    Check(enabled.All(light => light.shadows == (shadows == 1 ? "Soft" : "None")), "Shadow switch did not reach lights.");
                    float mean = result.measurements.Last().mean;
                    if (shadows == 0) { Check(mean > previousMean + 2, "Brightness levels did not brighten actual RGB."); previousMean = mean; }
                }
            }
        }
        foreach (string side in new[] { "left", "dual" })
            for (int brightness = 0; brightness < 3; brightness++)
            {
                Color32[] a = images[side + "-" + brightness + "-clear"], b = images[side + "-" + brightness + "-shadows"];
                int changes = a.Zip(b, (x, y) => Math.Abs(x.r - y.r) + Math.Abs(x.g - y.g) + Math.Abs(x.b - y.b)).Count(delta => delta > 12);
                Check(changes > 100, "Shadow switch had no visible effect: " + side + brightness + "; pixels=" + changes);
            }
        settings.SetLayout(2); settings.SetBrightness(1);
        Frame standard = Save("repeat-a", out _);
        Frame repeat = Save("repeat-b", out _);
        Check(standard.lightingConfigurationId == repeat.lightingConfigurationId, "Repeated light ID was unstable.");
        CultureInfo old = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); Check(Save("locale", out _).lightingConfigurationId == standard.lightingConfigurationId, "Locale changed light ID."); }
        finally { CultureInfo.CurrentCulture = old; }
        settings.SetLayout(1);
        Check(Save("right", out _).lightingConfiguration.lights.Single(light => light.enabled && light.active).objectPath.Contains("RightWhiteLight"), "Right selection failed.");
        settings.SetLayout(3);
        Check(Save("directional", out _).lightingConfiguration.lights.Single(light => light.enabled && light.active).type == "Directional", "Directional reference failed.");
        settings.SetLayout(2);

        Light extra = new GameObject("AdditionalLight").AddComponent<Light>();
        Check(Save("extra-light", out _).lightingConfiguration.lights.Any(light => light.objectPath.Contains("AdditionalLight")), "Additional scene light was omitted.");
        extra.gameObject.SetActive(false);
        Check(Save("inactive-light", out _).lightingConfiguration.lights.Any(light => !light.active && light.objectPath.Contains("AdditionalLight")), "Inactive light was omitted.");
        UnityEngine.Object.DestroyImmediate(extra.gameObject);
        Color ambient = RenderSettings.ambientLight;
        RenderSettings.ambientLight = Color.gray;
        Check(Save("ambient-change", out _).lightingConfigurationId != standard.lightingConfigurationId, "Ambient did not affect light ID.");
        RenderSettings.ambientLight = ambient;
        float distance = QualitySettings.shadowDistance;
        QualitySettings.shadowDistance = distance + 1;
        Check(Save("quality-change", out _).lightingConfigurationId != standard.lightingConfigurationId, "Shadow quality did not affect light ID.");
        QualitySettings.shadowDistance = distance;

        Reject(() => GameObject.Find("LeftWhiteLight").GetComponent<Light>().intensity += 1);
        settings.ApplyConfiguration();
        Reject(() => RenderSettings.ambientLight = Color.gray);
        settings.ApplyConfiguration();
        Reject(() => QualitySettings.shadowDistance += 1);
        settings.ApplyConfiguration();

        VerifyBalanceAndAmbient();

        // The settings popup must intercept sidebar clicks and keep editing controls intact.
        // Batch mode has no backbuffer Game view; give uGUI a real target for geometry/raycast generation.
        Canvas canvas = GameObject.Find("SimulationCanvas").GetComponent<Canvas>();
        var uiCamera = new GameObject("UiVerificationCamera").AddComponent<Camera>();
        uiCamera.enabled = false;
        uiCamera.cullingMask = 1 << 5;
        uiCamera.targetTexture = new RenderTexture(1024, 768, 24);
        uiCamera.targetTexture.Create();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCamera;
        canvas.planeDistance = 1;
        GameObject.Find("LightingMenu").GetComponent<Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        uiCamera.Render();
        var previousTarget = RenderTexture.active;
        RenderTexture.active = uiCamera.targetTexture;
        var uiImage = new Texture2D(1024, 768, TextureFormat.RGB24, false);
        uiImage.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0); uiImage.Apply();
        File.WriteAllBytes(Path.Combine(output, "lighting-ui.png"), uiImage.EncodeToPNG());
        RenderTexture.active = previousTarget;
        var panel = GameObject.Find("LightingPanel").GetComponent<RectTransform>();
        var hits = new List<RaycastResult>();
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(uiCamera, panel.TransformPoint(panel.rect.center)) };
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Any(hit => hit.gameObject == panel.gameObject || hit.gameObject.transform.IsChildOf(panel)), "Lighting panel did not block clicks; pointer=" + pointer.position + "; rect=" + panel.rect + "; depth=" + panel.GetComponent<Image>().depth);
        Slider balance = GameObject.Find("LightBalance").GetComponent<Slider>();
        Slider ambientControl = GameObject.Find("AmbientMultiplier").GetComponent<Slider>();
        DragSlider(balance, uiCamera, 0.8f, 0.2f);
        Check(Mathf.Abs(settings.LeftLightShare - 0.2f) < 0.011f, "Dragging the balance handle did not update scene settings.");
        DragSlider(ambientControl, uiCamera, 0.5f, 0.9f);
        Check(Mathf.Abs(settings.AmbientMultiplier - 2.7f) < 0.04f, "Dragging ambient control did not update scene settings.");
        GameObject.Find("LightLayout_0").GetComponent<Button>().onClick.Invoke();
        Check(settings.CurrentLayout == SimulationLightingSettings.Layout.Left, "UI button did not select left light.");
        Check(!balance.interactable, "Balance slider remained active in single-light mode.");
        GameObject.Find("LightLayout_2").GetComponent<Button>().onClick.Invoke();
        Check(balance.interactable && Mathf.Abs(balance.value - 20) < 1.1f, "Dual mode did not restore the saved ratio.");
        GameObject.Find("ResetLightBalanceAmbient").GetComponent<Button>().onClick.Invoke();
        Check(balance.value == 50 && ambientControl.value == 1, "Reset did not synchronize both sliders.");
        VerifyUiSizes(uiCamera, panel);
        GameObject.Find("CloseLighting").GetComponent<Button>().onClick.Invoke();
        Check(GameObject.Find("LightingPanel") == null && GameObject.Find("BrickTools") != null, "Popup did not return to editing.");

        // Disabling the experiment restores the scene/global values captured on entering Play.
        settings.SetAmbientMultiplier(3);
        settings.enabled = false;
        Check(QualitySettings.shadowDistance == originalShadowDistance && QualitySettings.pixelLightCount == originalPixelLights && QualitySettings.shadows == originalShadows && QualitySettings.shadowResolution == originalResolution, "Global quality settings were not restored.");
        Check(RenderSettings.ambientLight == originalAmbient, "Flat ambient color was not restored after disabling the controller.");
        settings.enabled = true;
        settings.SetLayout(2); settings.SetBrightness(1);
        settings.ResetBalanceAndAmbient();
        if (!settings.ShadowsEnabled) settings.ToggleShadows();
        Check(!Directory.GetFiles(Path.Combine(output, "SimulationCaptures"), "*.tmp").Any(), "Temporary files leaked.");
    }

    private static Frame Save(string name, out Color32[] pixels)
    {
        string before = capture.LastSavedPath;
        capture.SaveImage();
        string path = capture.LastSavedPath;
        Check(path != null && path != before && File.Exists(path), "Capture failed: " + name);
        string json = File.ReadAllText(Path.ChangeExtension(path, ".json"));
        Frame frame = JsonUtility.FromJson<Frame>(json);
        Check(frame.schemaVersion == 3 && frame.lightingConfiguration.schemaVersion == 2, "Lighting schema missing.");
        Check(frame.lightingConfigurationId.StartsWith("lighting-v2-") && frame.lightingConfigurationId.Length == 76, "Invalid lighting ID.");
        string currentCells = string.Join(";", frame.cells.Select(cell => cell.gridX + "," + cell.gridY + "," + cell.occupied + "," + cell.brickId + "," + cell.colorId));
        if (cameraId == null) { cameraId = frame.cameraConfigurationId; cells = currentCells; }
        Check(frame.cameraConfigurationId == cameraId && currentCells == cells, "Lighting changed camera config or GT.");
        var image = new Texture2D(2, 2);
        Check(image.LoadImage(File.ReadAllBytes(path)) && image.width == 512 && image.height == 512, "Invalid RGB PNG.");
        pixels = image.GetPixels32();
        float[] graySamples = new[] { new Vector3(1.5f, 0, 1.5f), new Vector3(8.5f, 0, 1.5f), new Vector3(1.5f, 0, 8.5f), new Vector3(8.5f, 0, 8.5f), new Vector3(5.5f, 0, 1.5f) }
            .Select(point => { Vector3 uv = camera.WorldToViewportPoint(point); return Luma(image.GetPixel((int)(uv.x * image.width), (int)(uv.y * image.height))); }).ToArray();
        result.measurements.Add(new Measurement { name = name, mean = pixels.Average(pixel => Luma(pixel)), clippedFraction = pixels.Count(pixel => pixel.r >= 254 && pixel.g >= 254 && pixel.b >= 254) / (float)pixels.Length, graySamples = graySamples });
        File.Copy(path, Path.Combine(output, name + ".png"));
        File.Copy(Path.ChangeExtension(path, ".json"), Path.Combine(output, name + ".json"));
        UnityEngine.Object.DestroyImmediate(image);
        result.pairs++;
        return frame;
    }
    private static float Luma(Color32 color) => 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;

    private static void VerifyBalanceAndAmbient()
    {
        settings.SetLayout(2); settings.SetBrightness(1); settings.ResetBalanceAndAmbient();
        Frame baseline = Save("balance-default", out Color32[] originalPixels);
        Check(settings.LeftLightShare == 0.5f && settings.AmbientMultiplier == 1, "Default extension values changed the old lighting baseline.");
        string[] ratioIds = new string[5];
        for (int i = 0; i < 5; i++)
        {
            float share = i / 4f;
            settings.SetLeftLightShare(share);
            Frame frame = Save("balance-" + i, out _);
            ratioIds[i] = frame.lightingConfigurationId;
            var preset = frame.lightingConfiguration.controllers.Single();
            var left = frame.lightingConfiguration.lights.Single(light => light.objectPath.Contains("LeftWhiteLight"));
            var right = frame.lightingConfiguration.lights.Single(light => light.objectPath.Contains("RightWhiteLight"));
            Check(Mathf.Abs(left.intensity - settings.IntensityBudget * share) < 0.0001f && Mathf.Abs(right.intensity - settings.IntensityBudget * (1 - share)) < 0.0001f, "Ratio did not reach actual lights.");
            Check(Mathf.Abs(left.intensity + right.intensity - settings.IntensityBudget) < 0.0001f && left.enabled && right.enabled, "Ratio changed total intensity or endpoint mode.");
            Check(preset.leftLightShare == share && preset.ambientMultiplier == 1 && preset.baseAmbientColor == settings.BaseAmbientColor, "New preset fields missing from capture.");
        }
        Check(ratioIds.Distinct().Count() == 5 && ratioIds[2] == baseline.lightingConfigurationId, "Ratio identity did not distinguish settings or restore the default.");
        float[] a = result.measurements.Single(value => value.name == "balance-1").graySamples;
        float[] b = result.measurements.Single(value => value.name == "balance-3").graySamples;
        Check(a[0] < a[1] - 15 && b[0] > b[1] + 15, "Actual board shading did not reverse with left/right balance.");
        settings.SetLayout(0);
        Frame single = Save("single-ratio-a", out Color32[] singleA);
        settings.SetLeftLightShare(0.1f);
        Frame singleChanged = Save("single-ratio-b", out Color32[] singleB);
        Check(singleA.SequenceEqual(singleB), "Retained ratio changed actual single-light RGB.");
        Check(single.lightingConfigurationId != singleChanged.lightingConfigurationId, "Retained request was omitted from configuration identity.");
        Check(singleChanged.lightingConfiguration.lights.Single(light => light.active && light.enabled).intensity == settings.IntensityBudget, "Single light lost its full budget.");
        settings.SetLayout(1); settings.SetLeftLightShare(0.9f);
        Frame rightOnly = Save("right-ratio", out _);
        Check(rightOnly.lightingConfiguration.lights.Single(light => light.active && light.enabled).intensity == settings.IntensityBudget, "Right-only mode used the dual-light fraction.");
        settings.SetLayout(2); settings.SetLeftLightShare(0.5f);
        float previousGray = -1;
        foreach (float multiplier in new[] { 0f, 1f, 3f })
        {
            settings.SetAmbientMultiplier(multiplier);
            Frame frame = Save("ambient-" + multiplier.ToString(CultureInfo.InvariantCulture), out _);
            Color actual = frame.lightingConfiguration.ambientSkyColor;
            Color expected = settings.BaseAmbientColor * multiplier;
            Check(frame.lightingConfiguration.ambientMode == "Flat" && Mathf.Abs(actual.r - expected.r) < 0.00001f && Mathf.Abs(actual.g - expected.g) < 0.00001f && Mathf.Abs(actual.b - expected.b) < 0.00001f && actual.a == 1, "Flat ambient did not use base RGB times multiplier.");
            Check(frame.lightingConfiguration.controllers.Single().ambientMultiplier == multiplier, "Ambient request was not recorded.");
            float gray = result.measurements.Last().graySamples.Average();
            Check(gray > previousGray + 2, "Changing ambient did not brighten actual board RGB.");
            previousGray = gray;
        }
        settings.ResetBalanceAndAmbient();
        Frame restored = Save("balance-ambient-restored", out Color32[] restoredPixels);
        Check(restored.lightingConfigurationId == baseline.lightingConfigurationId && restoredPixels.SequenceEqual(originalPixels), "Reset did not reproduce baseline configuration and RGB.");
        Reject(() => settings.SetLeftLightShare(0.75f));
        settings.ResetBalanceAndAmbient();
        Reject(() => settings.SetAmbientMultiplier(2));
        settings.ResetBalanceAndAmbient();
        settings.SetLeftLightShare(-1); Check(settings.LeftLightShare == 0, "Negative ratio was not clamped.");
        settings.SetLeftLightShare(2); Check(settings.LeftLightShare == 1, "Excess ratio was not clamped.");
        settings.SetAmbientMultiplier(-1); Check(settings.AmbientMultiplier == 0, "Negative ambient was not clamped.");
        settings.SetAmbientMultiplier(4); Check(settings.AmbientMultiplier == 3, "Excess ambient was not clamped.");
        settings.ResetBalanceAndAmbient();
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            settings.SetLeftLightShare(invalid); settings.SetAmbientMultiplier(invalid);
            Check(settings.LeftLightShare == 0.5f && settings.AmbientMultiplier == 1, "Non-finite input reached scene settings.");
        }
    }

    private static void DragSlider(Slider slider, Camera uiCamera, float down, float drag)
    {
        RectTransform area = (RectTransform)slider.handleRect.parent;
        Func<float, Vector2> point = fraction => RectTransformUtility.WorldToScreenPoint(uiCamera,
            area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin, area.rect.xMax, fraction), area.rect.center.y)));
        var data = new PointerEventData(EventSystem.current) { position = point(down), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Any(hit => hit.gameObject.transform.IsChildOf(slider.transform)), "Slider cannot receive pointer input: " + slider.name);
        data.pointerPressRaycast = hits.First(hit => hit.gameObject.transform.IsChildOf(slider.transform));
        data.pressPosition = data.position;
        ExecuteEvents.Execute(slider.gameObject, data, ExecuteEvents.pointerDownHandler);
        data.position = point(drag);
        ExecuteEvents.Execute(slider.gameObject, data, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(slider.gameObject, data, ExecuteEvents.pointerUpHandler);
    }

    private static void VerifyUiSizes(Camera uiCamera, RectTransform panel)
    {
        foreach (Vector2Int size in new[] { new Vector2Int(1024, 768), new Vector2Int(640, 480), new Vector2Int(571, 1000) })
        {
            uiCamera.targetTexture.Release();
            uiCamera.targetTexture = new RenderTexture(size.x, size.y, 24); uiCamera.targetTexture.Create();
            var scroll = GameObject.Find("LightingScroll").GetComponent<ScrollRect>();
            foreach (bool bottom in new[] { false, true })
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = bottom ? 0 : 1;
                Canvas.ForceUpdateCanvases(); uiCamera.Render();
                var previous = RenderTexture.active; RenderTexture.active = uiCamera.targetTexture;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, "lighting-ui-" + size.x + "x" + size.y + (bottom ? "-bottom" : "-top") + ".png"), image.EncodeToPNG());
                RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
                RectTransform close = GameObject.Find("CloseLighting").GetComponent<RectTransform>();
                Vector2 closePoint = RectTransformUtility.WorldToScreenPoint(uiCamera, close.TransformPoint(close.rect.center));
                Check(closePoint.x > 0 && closePoint.x < size.x && closePoint.y > 0 && closePoint.y < size.y, "Fixed return button fell outside the window.");
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = closePoint }, hits);
                Check(hits.Any(hit => hit.gameObject == close.gameObject || hit.gameObject.transform.IsChildOf(close)), "Return button was not clickable at " + size);
                RectTransform target = GameObject.Find(bottom ? "ResetLightBalanceAmbient" : "LightBalance").GetComponent<RectTransform>();
                Vector2 targetPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, target.TransformPoint(target.rect.center));
                hits.Clear(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = targetPoint }, hits);
                Check(hits.Any(hit => hit.gameObject == target.gameObject || hit.gameObject.transform.IsChildOf(target)), "Scrolled control was not reachable at " + size + "; bottom=" + bottom);
            }
        }
    }
    private static void Reject(Action change)
    {
        int before = Directory.GetFiles(Path.Combine(output, "SimulationCaptures")).Length;
        string previous = capture.LastSavedPath;
        bool invoked = false, warned = false;
        Camera.CameraCallback callback = current => { if (current == camera && !invoked) { invoked = true; change(); } };
        Application.LogCallback log = (message, trace, type) => { if (type == LogType.Warning && message.Contains("光照参数")) warned = true; };
        Camera.onPreCull += callback; Application.logMessageReceived += log;
        try { capture.SaveImage(); }
        finally { Camera.onPreCull -= callback; Application.logMessageReceived -= log; }
        Check(invoked && warned, "Lighting mutation did not reject the capture.");
        Check(capture.LastSavedPath == previous && Directory.GetFiles(Path.Combine(output, "SimulationCaptures")).Length == before, "Rejected capture published files.");
    }
    private static void Check(bool condition, string message) { result.assertions++; if (!condition) throw new Exception(message); }
    private static void Finish(Exception error)
    {
        if (error != null) { result.error = error.ToString(); Debug.LogException(error); }
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(result, true));
        Debug.Log("LIGHTING_CHECKS: " + JsonUtility.ToJson(result));
        EditorApplication.Exit(result.passed ? 0 : 1);
    }
}
