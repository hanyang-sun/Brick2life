using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Brick2Life.Battle;
using Brick2Life.Core;
using Brick2Life.Editor;
using Brick2Life.Feedback;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PhysicalProjectorChecks
{
    [Serializable] private sealed class Result
    {
        public bool passed;
        public int assertions;
        public float redResponse, greenResponse, heightResponse, shadowResponse, unshadowedResponse;
        public int shiftedPixels, reflectedPixels;
        public Color redDelta, greenDelta;
        public float beforeShadow;
        public float maxAlignmentError;
        public List<string> alignmentSamples = new List<string>();
        public List<string> failures = new List<string>();
    }
    private static Result result;
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static double started;
    private static int lastFrame = -1;
    private static SimulationVisionMonitor monitor;
    private static SimulationCameraCapture capture;
    private static SimulationProjectedLight light;
    private static Camera top, workspace;
    private static readonly List<Object> owned = new List<Object>();
    public static void Run()
    {
        result = new Result();
        EditorSceneManager.OpenScene("Assets/Scenes/Brick2LifeSimulation.unity");
        SimulationFeedbackSetup.Install();
        var feedbackRoot = GameObject.Find("SimulationFeedback");
        Check(feedbackRoot.GetComponents<MonoBehaviour>().All(component => component != null), "Cleanup left a missing feedback script.");
        Check(feedbackRoot.transform.Find("Level") == null, "Installer recreated the retired 3D role hierarchy.");
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.playModeStateChanged += OnPlay; EditorApplication.isPlaying = true;
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= OnPlay;
        started = EditorApplication.timeSinceStartup; steps.Clear(); steps.Push(Verify()); EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 90) throw new TimeoutException("Physical projector checks timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { (step as IDisposable)?.Dispose(); steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Finish();
        }
        catch (Exception error) { result.failures.Add(error.ToString()); Finish(); }
    }
    private static void Finish()
    {
        EditorApplication.update -= Tick;
        if (monitor != null) monitor.Automatic = false;
        if (light != null) light.CalibrationPattern = null;
        SimulationDualWindowSession.Close(); SimulationEditAccess.ConfigureFocus(null);
        foreach (Object item in owned) if (item != null) Object.DestroyImmediate(item);
        owned.Clear();
        result.passed = result.failures.Count == 0;
        File.WriteAllText(Path.Combine(UnityEngine.Application.dataPath, "..", "verification.json"), JsonUtility.ToJson(result, true));
        EditorApplication.Exit(result.passed ? 0 : 1);
    }
    private static void Check(bool value, string message) { result.assertions++; if (!value) result.failures.Add(message); }
    private static Texture2D Pattern(Func<int, Color> color)
    {
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) pixels[y * 64 + x] = color(x);
        texture.SetPixels(pixels); texture.Apply(); owned.Add(texture); return texture;
    }
    private static Color Sample(SimulationObservationImage image, Vector3 position)
    {
        Vector3 v = top.WorldToViewportPoint(position);
        int x = Mathf.RoundToInt(v.x * image.Width - 0.5f), y = Mathf.RoundToInt((1 - v.y) * image.Height - 0.5f);
        Color sum = Color.clear;
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
        {
            int i = (Mathf.Clamp(y + dy, 0, image.Height - 1) * image.Width + Mathf.Clamp(x + dx, 0, image.Width - 1)) * 3;
            sum += new Color(image.Rgb[i] / 255f, image.Rgb[i + 1] / 255f, image.Rgb[i + 2] / 255f);
        }
        return sum / (9 * image.ExposureScale);
    }
    private static int Difference(byte[] a, byte[] b, int threshold = 3) => a.Zip(b, (x, y) => Math.Abs(x - y) > threshold ? 1 : 0).Sum();
    // Measure the actual reflected footprint in RGB, not the source quad's constructed position.
    private static float FootprintError(SimulationObservationImage clean, SimulationObservationImage lit, Vector3 target)
    {
        Vector3 center = top.WorldToViewportPoint(target);
        int px = Mathf.RoundToInt(center.x * lit.Width), py = Mathf.RoundToInt((1 - center.y) * lit.Height);
        double total = 0, sumX = 0, sumZ = 0;
        for (int y = Mathf.Max(0, py - 24); y <= Mathf.Min(lit.Height - 1, py + 24); y++)
            for (int x = Mathf.Max(0, px - 24); x <= Mathf.Min(lit.Width - 1, px + 24); x++)
            {
                int index = (y * lit.Width + x) * 3;
                float weight = 0;
                for (int channel = 0; channel < 3; channel++) weight += Mathf.Max(0, lit.Rgb[index + channel] - clean.Rgb[index + channel]);
                if (weight < 12) continue;
                Ray ray = top.ViewportPointToRay(new Vector3((x + 0.5f) / lit.Width, 1 - (y + 0.5f) / lit.Height, 0));
                Vector3 point = ray.GetPoint((target.y - ray.origin.y) / ray.direction.y);
                total += weight; sumX += point.x * weight; sumZ += point.z * weight;
            }
        if (total == 0) return 100;
        return Vector2.Distance(new Vector2((float)(sumX / total), (float)(sumZ / total)), new Vector2(target.x, target.z));
    }

    private static IEnumerator VerifyBakedAlignment(ProjectionSimulator projection, SimulationBattleOverlay overlay)
    {
        var renderers = projection.MarksRoot.GetComponentsInChildren<Renderer>(true)
            .Concat(overlay.MarksRoot.GetComponentsInChildren<Renderer>(true)).ToArray();
        var enabled = renderers.Select(r => r.enabled).ToArray();
        // Capture suppression restores source GameObjects; disable rendering instead to isolate the test pattern.
        foreach (Renderer renderer in renderers) renderer.enabled = false;
        var patch = GameObject.CreatePrimitive(PrimitiveType.Quad); owned.Add(patch);
        patch.name = "AlignmentContent"; patch.layer = projection.ProjectionLayer;
        patch.transform.SetParent(projection.MarksRoot, false);
        patch.transform.rotation = Quaternion.Euler(90, 0, 0); patch.transform.localScale = Vector3.one * 0.32f;
        var ink = new Material(Shader.Find("Sprites/Default")) { color = Color.white }; owned.Add(ink);
        patch.GetComponent<Renderer>().sharedMaterial = ink; patch.GetComponent<Renderer>().forceRenderingOff = true;
        var receiver = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(receiver);
        var gray = new Material(Shader.Find("Standard")) { color = new Color(0.4f, 0.4f, 0.4f) }; owned.Add(gray);
        receiver.GetComponent<Renderer>().sharedMaterial = gray;
        Vector3 pose = light.ProjectorPosition, angles = light.ProjectorAngles;
        var fovField = typeof(SimulationProjectedLight).GetField("fieldOfView", BindingFlags.Instance | BindingFlags.NonPublic);
        float fov = (float)fovField.GetValue(light), strength = light.LightStrength, observerFov = top.fieldOfView;
        try
        {
            light.CalibrationPattern = null; light.LightStrength = 0.35f;
            // The production camera frames one layer; widen this optical fixture to observe elevated corner targets.
            top.fieldOfView = 60;
            for (int variant = 0; variant < 3; variant++)
            {
                light.ProjectorPosition = variant == 0 ? pose : new Vector3(5, 16, 4);
                light.ProjectorAngles = variant == 0 ? angles : Quaternion.LookRotation(new Vector3(5, 0, 5) - light.ProjectorPosition).eulerAngles;
                fovField.SetValue(light, variant == 2 ? 55f : fov);
                foreach (float height in new[] { 0f, 1f, 2f })
                    foreach (Vector2 point in new[] { new Vector2(5.5f, 5.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 9.5f), new Vector2(9.5f, 0.5f), new Vector2(9.5f, 9.5f) })
                    {
                        receiver.SetActive(height > 0);
                        receiver.transform.localScale = new Vector3(0.8f, Mathf.Max(0.01f, height), 0.8f);
                        receiver.transform.position = new Vector3(point.x, height / 2, point.y);
                        Vector3 target = new Vector3(point.x, height, point.y);
                        patch.transform.position = target;
                        yield return null;
                        var clean = capture.CaptureObservationImage(false);
                        var lit = capture.CaptureObservationImage(true);
                        float error = FootprintError(clean, lit, target);
                        result.maxAlignmentError = Mathf.Max(result.maxAlignmentError, error);
                        string label = $"pose={variant}, target={target}, error={error:F5}";
                        result.alignmentSamples.Add(label);
                        Check(error <= 0.02f, "Reflected footprint is misaligned: " + label);
                        if (error > 0.02f)
                        {
                            Save(top.targetTexture, $"alignment-fail-{variant}-{height}-{point.x}-{point.y}.png");
                            Save(light.OutputTexture, $"alignment-output-{variant}-{height}-{point.x}-{point.y}.png");
                        }
                    }
            }
            Save(top.targetTexture, "physical-projector-alignment.png");
        }
        finally
        {
            patch.SetActive(false); receiver.SetActive(false);
            light.ProjectorPosition = pose; light.ProjectorAngles = angles; light.LightStrength = strength; fovField.SetValue(light, fov);
            top.fieldOfView = observerFov;
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = enabled[i];
        }
    }
    private static byte[] ReadCamera(Camera camera)
    {
        var target = new RenderTexture(512, 512, 24); target.Create();
        var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
        RenderTexture old = camera.targetTexture, active = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply(); return image.GetRawTextureData<byte>().ToArray();
        }
        finally { camera.targetTexture = old; RenderTexture.active = active; target.Release(); Object.DestroyImmediate(image); Object.DestroyImmediate(target); }
    }
    private static void Save(RenderTexture texture, string filename)
    {
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        RenderTexture active = RenderTexture.active;
        try
        {
            RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(UnityEngine.Application.dataPath, "..", filename), image.EncodeToPNG());
        }
        finally { RenderTexture.active = active; Object.DestroyImmediate(image); }
    }
    private static IEnumerator Verify()
    {
        while (Object.FindFirstObjectByType<SimulationVisionMonitor>()?.Session == null) yield return null;
        monitor = Object.FindFirstObjectByType<SimulationVisionMonitor>(); monitor.Automatic = false;
        capture = Object.FindFirstObjectByType<SimulationCameraCapture>();
        capture.CaptureWithoutProjection = true;
        var adapter = Object.FindFirstObjectByType<SimulationLevelAdapter>();
        var battle = Object.FindFirstObjectByType<SimulationBattleController>();
        var board = Object.FindFirstObjectByType<SimulationBoardSession>();
        Check(battle.GenerateNewRound(12, 2), "Cannot generate physical projector fixture.");
        Check(board.TryAdd(2, 2, "red", out _) && board.TryAdd(6, 2, "green", out _), "Cannot create colored receiver bricks.");
        light = adapter.Projection.GetComponent<SimulationProjectedLight>();
        Check(light != null && light.enabled, "Physical projector was not enabled automatically.");
        top = GameObject.Find("TopCamera").GetComponent<Camera>();
        workspace = GameObject.Find("WorkspaceCamera").GetComponent<Camera>();
        for (int i = 0; i < 3; i++) monitor.AnalyzeOnce();
        SimulationDualWindowSession.Open(); SimulationEditAccess.ConfigureFocus(() => true);
        Texture2D white = Pattern(x => Color.white), black = Pattern(x => Color.black), blue = Pattern(x => Color.blue);
        Texture2D split = Pattern(x => x < 16 ? Color.blue : Color.black);

        var clean = capture.CaptureObservationImage(false);
        light.CalibrationPattern = black;
        var dark = capture.CaptureObservationImage(true);
        Check(Difference(clean.Rgb, dark.Rgb) == 0, "A black projector image paints the scene black or exposes source quads.");
        light.BlackLevel = 0.03f;
        Check(Difference(clean.Rgb, capture.CaptureObservationImage(true).Rgb, 0) > 1000,
            "Configurable black-level leakage did not add residual light.");
        light.BlackLevel = 0;
        // Use a non-clipping calibration exposure to measure reflectance rather than sensor saturation.
        float strength = light.LightStrength;
        light.LightStrength = 0.35f;
        light.CalibrationPattern = white;
        var bright = capture.CaptureObservationImage(true);
        result.reflectedPixels = Difference(clean.Rgb, bright.Rgb);
        Check(result.reflectedPixels > 1000, "Projector light does not reach the captured scene.");
        Color redDelta = Sample(bright, new Vector3(2.5f, 1, 2.5f)) - Sample(clean, new Vector3(2.5f, 1, 2.5f));
        Color greenDelta = Sample(bright, new Vector3(6.5f, 1, 2.5f)) - Sample(clean, new Vector3(6.5f, 1, 2.5f));
        result.redResponse = redDelta.r; result.greenResponse = greenDelta.g;
        result.redDelta = redDelta; result.greenDelta = greenDelta;
        Save(top.targetTexture, "physical-projector-white.png");
        Check(redDelta.r > 0.03f && redDelta.r > redDelta.b * 1.3f, "Red surface did not modulate reflected white projector light.");
        Check(greenDelta.g > 0.03f && greenDelta.g > greenDelta.r * 1.3f, "Green surface did not modulate reflected white projector light.");
        byte[] workspaceBright = ReadCamera(workspace);
        light.CalibrationPattern = black;
        Check(Difference(workspaceBright, ReadCamera(workspace)) == 0, "Clean building view changed with projector output.");
        light.LightStrength = strength;

        light.CalibrationPattern = split;
        var original = capture.CaptureObservationImage(true);
        light.ProjectorPosition = new Vector3(7, 14, 5);
        result.shiftedPixels = Difference(original.Rgb, capture.CaptureObservationImage(true).Rgb);
        Check(result.shiftedPixels > 1000, "Moving projector did not move the projected image.");
        light.ProjectorPosition = new Vector3(5, 14, 5);

        var receiver = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(receiver);
        receiver.name = "HeightReceiver"; receiver.transform.position = new Vector3(2.5f, 0.5f, 5);
        var gray = new Material(Shader.Find("Standard")) { color = new Color(0.4f, 0.4f, 0.4f) }; owned.Add(gray);
        receiver.GetComponent<Renderer>().sharedMaterial = gray;
        Vector3 lowPoint = new Vector3(2.3f, 1, 5);
        float low = Sample(capture.CaptureObservationImage(true), lowPoint).b - Sample(capture.CaptureObservationImage(false), lowPoint).b;
        receiver.transform.localScale = new Vector3(1, 2, 1); receiver.transform.position = new Vector3(2.5f, 1, 5);
        Vector3 highPoint = new Vector3(2.3f, 2, 5);
        float high = Sample(capture.CaptureObservationImage(true), highPoint).b - Sample(capture.CaptureObservationImage(false), highPoint).b;
        result.heightResponse = high - low;
        Check(result.heightResponse > 0.08f, "Changing height did not shift the projected pattern relative to the brick.");
        receiver.SetActive(false);

        light.ProjectorPosition = new Vector3(2, 8, 5);
        light.ProjectorAngles = Quaternion.LookRotation(new Vector3(5, 0, 5) - light.ProjectorPosition).eulerAngles;
        light.CalibrationPattern = blue;
        Vector3 shadowPoint = new Vector3(6, 0, 5), control = new Vector3(6, 0, 7);
        var beforeBlock = capture.CaptureObservationImage(true);
        var beforeClean = capture.CaptureObservationImage(false);
        float unblocked = Sample(beforeBlock, shadowPoint).b - Sample(beforeClean, shadowPoint).b;
        result.beforeShadow = unblocked;
        receiver.SetActive(true); receiver.transform.position = new Vector3(5, 1.5f, 5); receiver.transform.localScale = new Vector3(1, 3, 2);
        var blocked = capture.CaptureObservationImage(true); var blockedClean = capture.CaptureObservationImage(false);
        result.shadowResponse = Sample(blocked, shadowPoint).b - Sample(blockedClean, shadowPoint).b;
        result.unshadowedResponse = Sample(blocked, control).b - Sample(blockedClean, control).b;
        Check(unblocked > 0.05f && result.shadowResponse < unblocked * 0.25f, "Projector light passes through an intervening tall brick.");
        Check(result.unshadowedResponse > 0.05f, "Occlusion incorrectly removed the entire projector output.");
        capture.CaptureObservationImage(true);
        Save(top.targetTexture, "physical-projector-shadow.png");
        receiver.SetActive(false);
        var restored = capture.CaptureObservationImage(true);
        var restoredClean = capture.CaptureObservationImage(false);
        Check(Sample(restored, shadowPoint).b - Sample(restoredClean, shadowPoint).b > unblocked * 0.8f,
            "An inactive root renderer still casts a ghost projector shadow.");
        light.ProjectorPosition = new Vector3(5, 14, 5); light.ProjectorAngles = new Vector3(90, 0, 0);
        light.CalibrationPattern = null;
        yield return null;
        var overlay = battle.GetComponent<SimulationBattleOverlay>();
        yield return VerifyBakedAlignment(adapter.Projection, overlay);
        var natural = capture.CaptureObservationImage(true);
        Check(light.OutputTexture != null && Difference(clean.Rgb, natural.Rgb) > 20, "Existing role patterns were not baked into the projector output.");
        int originalMask = top.cullingMask;
        top.cullingMask &= ~(1 << adapter.Projection.ProjectionLayer);
        Check(Difference(clean.Rgb, capture.CaptureObservationImage(true).Rgb) > 20,
            "Camera layer exclusion unrealistically removed reflected projector light.");
        top.cullingMask = originalMask;
        Save(light.OutputTexture, "physical-projector-output.png");
        Save(top.targetTexture, "physical-projector-rgb.png");
        Check(adapter.Projection.MarksRoot.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff), "Source quads remained directly visible after output capture.");
        var preview = (SimulationProjectionPreview)typeof(SimulationProjectionWindow)
            .GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(SimulationDualWindowSession.DisplayWindow);
        preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, battle.View, false);
        Save(preview.Render(960, 720), "physical-projector-preview.png");
        Check(preview.BrickCount == 2 && preview.ProjectionCount > 0, "Preview did not receive the shared projector output.");
        Check(!preview.Camera.scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true))
            .Any(t => t.name == "Projection" || t.name == "Battle"), "Preview retained obsolete projection or battle-copy roots.");
        Vector3 pose = light.ProjectorPosition, angles = light.ProjectorAngles;
        preview.Orbit(new Vector2(40, 15)); preview.Render(512, 512);
        Check(light.ProjectorPosition == pose && light.ProjectorAngles == angles, "Orbiting the observer moved the projector.");
        using (ProjectionCaptureGate.Suppress())
        {
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, battle.View, false); preview.Render(512, 512);
            Check(!preview.ProjectionVisible, "Preview did not suppress projector contribution with capture gate.");
        }
        Check(adapter.Projection.IsVisible, "Capture gate did not restore projection.");
        light.enabled = false;
        Check(Difference(clean.Rgb, capture.CaptureObservationImage(true).Rgb) == 0, "Disabling physical projector exposes legacy source quads.");
        Check(!top.GetCommandBuffers(UnityEngine.Rendering.CameraEvent.BeforeForwardAlpha).Any(b => b.name == "Brick2Life projected light"),
            "Disabling projector leaked a camera command buffer.");
        light.enabled = true;
        battle.SetAutoStep(false);
        Check(battle.StartBattle(), "Cannot start battle with the physical projector.");
        battle.StepManual(2);
        yield return null;
        var face = overlay.MarksRoot.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "OctopusFace");
        Check(face != null && Mathf.Abs(face.transform.position.y - 1f) < 0.0001f, "Octopus face is not on the confirmed brick top.");
        Check(battle.Running && battle.GetComponent<SimulationBattleOverlay>().ActiveEnemyCount > 0,
            "Live battle animation did not enter the projector output.");
        var current = overlay.MarksRoot.Find("BattlePath")?.GetComponent<MeshRenderer>();
        Check(current != null && current.sharedMaterial.shader.isSupported && overlay.RouteProjectionVisible,
            "The current shader did not produce floor route content.");
        var routes = battle.DisplayRoutes;
        Check(routes.Count == 2 && routes[0].Route.Zip(routes[0].Route.Skip(1), (a, b) => (a, b))
            .Intersect(routes[1].Route.Zip(routes[1].Route.Skip(1), (a, b) => (a, b))).Count() >= 6,
            "The render fixture should contain two branches and a shared downstream lane.");
        monitor.AnalyzeOnce();
        Check(monitor.LastRawScore.ExactMatches == 100, "Clean sampling after reflected projection corrupted RGB recognition.");
        capture.CaptureObservationImage(true);
        Save(light.OutputTexture, "ocean-current-output.png");
        preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, battle.View, false);
        Save(preview.Render(960, 720), "physical-projector-battle.png");
        yield return VerifyCoralWall(board, adapter, preview, battle);
        yield return VerifyPufferfish(adapter, preview, battle, overlay);
    }

    private static IEnumerator VerifyPufferfish(SimulationLevelAdapter adapter, SimulationProjectionPreview preview,
        SimulationBattleController battle, SimulationBattleOverlay overlay)
    {
        Check(battle.Restart() && overlay.ActiveEnemyCount == 0, "Restart must hide the previous enemies.");
        battle.WavePlan = new WavePlan(1, 4, 24, 2.0, 10, 0.32);
        battle.TowerSettings = new TowerSettings(100, 12, 24, 0.35);
        capture.CaptureWithoutProjection = false;
        Check(battle.StartBattle(), "Cannot start the pufferfish fixture.");
        battle.StepManual(0.58f);
        yield return null;

        Renderer[] ActiveRenderers() => overlay.MarksRoot.GetComponentsInChildren<Renderer>();
        var fish = ActiveRenderers().FirstOrDefault(r => r.name == "BattleEnemy");
        Check(fish != null && battle.State.Enemies.Count >= 2 && overlay.ActiveEnemyCount == battle.State.Enemies.Count,
            "Spawned enemies did not appear as a school.");
        if (fish == null || battle.State.Enemies.Count == 0) yield break;
        var enemy = battle.State.Enemies[0];
        var heading = new Vector3((float)enemy.HeadingX, 0f, (float)enemy.HeadingY).normalized;
        Check(Vector3.Dot(fish.transform.up, heading) > 0.99f, "The pufferfish is not facing its next waypoint.");
        Check(!ActiveRenderers().Any(r => r.name == "BattleEnemyBar" || r.name == "BattleEnemyId"),
            "Healthy pufferfish must keep the lane clear of health bars and debug numbers.");
        capture.CaptureObservationImage(true);
        Check(fish.sharedMaterial.name == "Pufferfish" && !ShaderUtil.ShaderHasError(fish.sharedMaterial.shader),
            "Pufferfish shader did not compile.");
        Save(light.OutputTexture, "pufferfish-live-swim.png");
        preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, battle.View, false);
        Save(preview.Render(960, 720), "pufferfish-live-reflected.png");

        battle.StepManual(0.10f);
        yield return null;
        Check(battle.LastShots.Any(shot => !shot.Killed), "A real bubble must wound a pufferfish.");
        var properties = new MaterialPropertyBlock();
        fish.GetPropertyBlock(properties);
        float hit = properties.GetFloat("_Hit"), phase = properties.GetFloat("_SwimPhase");
        Check(hit > 0.6f && ActiveRenderers().Any(r => r.name == "BattleEnemyFill"),
            "A bubble hit must show the pufferfish reaction and health bar.");
        capture.CaptureObservationImage(true);
        Save(light.OutputTexture, "pufferfish-live-hit.png");
        monitor.AnalyzeOnce();
        Check(monitor.LastRawScore.ExactMatches == 100, "Pufferfish projection corrupted continuous layout recognition.");

        battle.SetPaused(true);
        yield return null;
        battle.StepManual(0.10f);
        yield return null;
        fish.GetPropertyBlock(properties);
        Check(Mathf.Approximately(properties.GetFloat("_Hit"), hit) &&
            Mathf.Approximately(properties.GetFloat("_SwimPhase"), phase), "The pufferfish pose advanced while paused.");
        battle.SetPaused(false);

        for (int i = 0; i < 30 && !battle.LastShots.Any(shot => shot.Killed); i++)
        {
            battle.StepManual(0.06f);
            yield return null;
        }
        var burst = ActiveRenderers().FirstOrDefault(r => r.sharedMaterial.name == "PufferfishBurst");
        Check(battle.LastShots.Any(shot => shot.Killed) && burst != null && burst.sharedMaterial.GetFloat("_Burst") == 1f,
            "A defeated pufferfish must produce the three-bubble burst.");
        capture.CaptureObservationImage(true);
        Save(light.OutputTexture, "pufferfish-live-burst.png");
        Check(battle.Restart() && overlay.ActiveEnemyCount == 0 && overlay.ActiveEffectCount == 0,
            "Restart must clear pufferfish and their defeat effects.");
    }

    private static IEnumerator VerifyCoralWall(SimulationBoardSession board, SimulationLevelAdapter adapter,
        SimulationProjectionPreview preview, SimulationBattleController battle)
    {
        Check(board.TryAdd(6, 1, "green", out var south), "Cannot place the south arm of the L-shaped wall.");
        Check(board.TryAdd(7, 2, "green", out _) && board.TryAdd(5, 3, "green", out _),
            "Cannot place the east arm and the diagonal wall.");
        for (int x = 3; x <= 7; x++)
            Check(board.TryAdd(x, 8, "green", out _), "Cannot place the straight wall for the crest fixture.");
        for (int i = 0; i < 3; i++) monitor.AnalyzeOnce();
        ProjectionSimulator projection = adapter.Projection;
        var properties = new MaterialPropertyBlock();
        Renderer WallAt(int x, int y) => projection.MarksRoot.GetComponentsInChildren<Renderer>()
            .FirstOrDefault(r => r.sharedMaterial.name == "ProjectionWall" &&
                r.transform.position == new Vector3(x + 0.5f, 1f, y + 0.5f));
        Vector4 Read(Renderer renderer, string property)
        {
            renderer.GetPropertyBlock(properties);
            return properties.GetVector(property);
        }
        Renderer elbow = WallAt(6, 2), diagonal = WallAt(5, 3);
        Check(elbow != null && elbow.sharedMaterial.shader.isSupported && diagonal != null,
            "Coral shader or confirmed wall renderers missing.");
        if (elbow == null || diagonal == null) yield break;
        Check(Read(elbow, "_Exposed") == new Vector4(1, 0, 0, 1) &&
            Read(elbow, "_InnerCorners") == new Vector4(0, 1, 0, 0),
            "L-shaped wall exposes a shared edge or misses its inside corner.");
        Check(Read(diagonal, "_Exposed") == Vector4.one,
            "A diagonal-only neighbor incorrectly closes a wall edge.");
        for (int x = 3; x <= 7; x++)
        {
            Renderer wall = WallAt(x, 8);
            if (wall != null) wall.GetPropertyBlock(properties);
            float expected = x % 2 == 1 ? 1f : 0f;
            Check(wall != null && properties.GetFloat("_ShowCrest") == expected,
                "The straight wall renderer did not receive its crest placement.");
        }
        float fadeEnds = Time.unscaledTime + projection.LastPlan.ChangeFadeSeconds;
        while (Time.unscaledTime <= fadeEnds) yield return null;
        var idle = capture.CaptureObservationImage(true);
        Check(!ShaderUtil.ShaderHasError(elbow.sharedMaterial.shader), "Coral shader failed to compile for the render fixture.");
        Save(light.OutputTexture, "coral-wall-output.png");
        Save(top.targetTexture, "coral-wall-rgb.png");
        preview.UpdateScene(adapter.Feedback.Level, projection, battle.View, false);
        Save(preview.Render(960, 720), "coral-wall-preview.png");

        var update = typeof(ProjectionSimulator).GetMethod("UpdateWallProximity", BindingFlags.Instance | BindingFlags.NonPublic);
        void Approach(double x, double z) =>
            update.Invoke(projection, new object[] { new[] { new EnemyState(1, x * 2, z * 2, 0, 24, 0) }, 0.25f });
        Approach(7.06, 2.5);
        Check(Read(elbow, "_Nearby") == Vector4.zero, "Enemy inside a shared seam lit up that seam.");
        Approach(5.94, 2.5);
        Check(Read(elbow, "_Nearby") == new Vector4(1, 0, 0, 0), "A passing enemy did not light only its facing side.");
        var nearby = capture.CaptureObservationImage(true);
        Save(light.OutputTexture, "coral-wall-nearby-output.png");
        Save(top.targetTexture, "coral-wall-nearby-rgb.png");
        Check(Difference(idle.Rgb, nearby.Rgb) > 10, "Changing the coral proximity did not affect reflected pixels.");
        update.Invoke(projection, new object[] { Array.Empty<EnemyState>(), 0.25f });
        Check(Read(elbow, "_Nearby") == Vector4.zero, "Coral tip glow did not fade after the enemy passed.");

        capture.CaptureWithoutProjection = false;
        monitor.AnalyzeOnce();
        Check(monitor.LastRawScore.ExactMatches == 100, "Coral wall layout misread with reflected projection and compensation.");
        capture.CaptureWithoutProjection = true;
        Check(board.TryAdd(7, 1, "green", out _), "Cannot fill the inside corner of the coral wall.");
        for (int i = 0; i < 3; i++) monitor.AnalyzeOnce();
        elbow = WallAt(6, 2);
        Check(elbow != null && Read(elbow, "_InnerCorners") == Vector4.zero,
            "Filling the diagonal did not close the inside corner.");
        Check(south != null && board.TryDelete(south.BrickId), "Cannot remove a wall from the fixture.");
        for (int i = 0; i < 3; i++) monitor.AnalyzeOnce();
        elbow = WallAt(6, 2);
        Check(elbow != null && Read(elbow, "_Exposed") == new Vector4(1, 0, 1, 1) &&
            Read(elbow, "_InnerCorners") == Vector4.zero, "Wall edge did not refresh after removing its neighbor.");
    }
}
