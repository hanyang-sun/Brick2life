using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Brick2Life.Application;
using Brick2Life.Battle;
using Brick2Life.Core;
using Brick2Life.Editor;
using Brick2Life.Feedback;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Automatic real-RGB updates, including a separate local-overlap fixture; never calls AnalyzeOnce or Restart.
public static class LiveProjectionRefreshChecks
{
    [Serializable] private sealed class Result
    {
        public bool passed;
        public int assertions, immediateConfirmations;
        public int continuousFrames;
        public List<string> failures = new List<string>();
        public List<string> boundaries = new List<string>();
    }
    private static Result result;
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static double started;
    private static int lastFrame = -1;
    private static SimulationVisionMonitor monitor;
    private static SimulationLevelAdapter adapter;
    private static SimulationBattleController controller;
    private static SimulationBoardSession board;
    private static Action<BoardUpdate> confirmedObserver;

    public static void Run()
    {
        result = new Result();
        EditorSceneManager.OpenScene("Assets/Scenes/Brick2LifeSimulation.unity");
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.playModeStateChanged += OnPlay;
        EditorApplication.isPlaying = true;
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= OnPlay;
        started = EditorApplication.timeSinceStartup; steps.Clear(); steps.Push(Verify());
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 50) throw new TimeoutException("Live projection checks timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (monitor != null && monitor.Automatic && monitor.CompensatingProjection)
            {
                result.continuousFrames++;
                Check(!ProjectionCaptureGate.IsSuppressed, "Continuous recognition blanked the projector.");
            }
            while (steps.Count > 0)
            {
                IEnumerator step = steps.Peek();
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
        if (monitor != null && confirmedObserver != null) monitor.UpdateApplied -= confirmedObserver;
        confirmedObserver = null;
        while (steps.Count > 0) (steps.Pop() as IDisposable)?.Dispose();
        SimulationDualWindowSession.Close(); SimulationEditAccess.ConfigureFocus(null);
        result.passed = result.failures.Count == 0;
        File.WriteAllText(Path.Combine(UnityEngine.Application.dataPath, "..", "verification.json"), JsonUtility.ToJson(result, true));
        EditorApplication.Exit(result.passed ? 0 : 1);
    }
    private static void Check(bool value, string message)
    { result.assertions++; if (!value) result.failures.Add(message + " | " + Boundary()); }
    private static string Boundary() => "frame=" + Time.frameCount + "; auto=" + monitor?.Automatic +
        "; confirmed=" + monitor?.Session?.Confirmed?.Version + "; level=" + adapter?.Feedback?.Level?.SourceVersion +
        "; projection=" + adapter?.Projection?.LastPlan?.LevelVersion + "; running=" + controller?.Running +
        "; raw=" + monitor?.LastRawScore?.ExactMatches + "; error=" + monitor?.LastError + "; mismatches=" + (monitor?.LastRawScore == null ? "" : string.Join("; ", monitor.LastRawScore.Mismatches.Take(4)));
    private static IEnumerator Wait(Func<bool> predicate, string message, double seconds = 3)
    {
        double deadline = EditorApplication.timeSinceStartup + seconds;
        while (!predicate() && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(predicate(), message);
    }
    private static bool Matches(GridPoint point, string color) => monitor.Session.Confirmed != null &&
        monitor.Session.Confirmed.At(point.X, point.Y).ColorId == color &&
        monitor.Session.Confirmed.At(point.X, point.Y).Occupancy == (color == null ? Occupancy.Empty : Occupancy.Occupied);
    private static IEnumerator Verify()
    {
        yield return Wait(() => Object.FindFirstObjectByType<SimulationVisionMonitor>()?.Session != null, "Scene did not initialize.");
        board = Object.FindFirstObjectByType<SimulationBoardSession>();
        monitor = Object.FindFirstObjectByType<SimulationVisionMonitor>();
        adapter = Object.FindFirstObjectByType<SimulationLevelAdapter>();
        controller = Object.FindFirstObjectByType<SimulationBattleController>();
        SimulationDualWindowSession.Open(); SimulationDualWindowSession.LayoutWindow.Focus();
        SimulationEditAccess.ConfigureFocus(() => true);
        Object.FindFirstObjectByType<SimulationCameraCapture>().CaptureWithoutProjection = false;
        monitor.Configure(3, true);
        Check(controller.GenerateNewRound(2, 3), "Could not create repeatable multi-entrance round.");
        controller.WavePlan = new WavePlan(2, 2, 9999, 0.1, 1, 1);
        controller.SetAutoStep(true);
        yield return Wait(() => adapter.Feedback.Level != null, "Automatic RGB did not establish a baseline.", 10);
        controller.StartBattle();
        yield return Wait(() => controller.Running, "Automatic fresh confirmation did not start battle.");
        BattleSimulation originalBattle = controller.Battle;
        confirmedObserver = update =>
        {
            if (update.Outcome != UpdateOutcome.Changed || !controller.Running || !ReferenceEquals(originalBattle, controller.Battle)) return;
            // Bootstrap was subscribed before this observer: no subsequent Unity Update may be needed.
            LevelState expected = adapter.Feedback.Level;
            bool matching = controller.Level != null;
            for (int y = 0; y < 10 && matching; y++)
                for (int x = 0; x < 10 && matching; x++)
                    for (int dy = 0; dy < 2 && matching; dy++)
                        for (int dx = 0; dx < 2 && matching; dx++)
                            matching &= controller.Level.CellAt(x * 2 + dx, y * 2 + dy).Role == expected.At(x, y).Role;
            Check(matching, "Confirmed RGB callback returned before the battle display map refreshed.");
            result.immediateConfirmations++;
        };
        monitor.UpdateApplied += confirmedObserver;
        GridPoint[] free = Enumerable.Range(0, 100).Select(i => new GridPoint(i % 10, i / 10))
            .Where(p => !controller.Round.IsReserved(p.X, p.Y)).Take(3).ToArray();
        Check(board.TryAdd(free[0].X, free[0].Y, "green", out BoardLayout.Brick wall), "Could not add wall during battle.");
        yield return Wait(() => Matches(free[0], "green"), "Wall addition was not automatically confirmed during battle.");
        yield return VerifyDisplay(originalBattle, free[0], GameRole.Wall, 1, "add wall");
        Check(board.TryAdd(free[1].X, free[1].Y, "red", out BoardLayout.Brick tower), "Could not add tower during battle.");
        yield return Wait(() => Matches(free[1], "red"), "Tower addition was not automatically confirmed during battle.");
        yield return VerifyDisplay(originalBattle, free[1], GameRole.Tower, 2, "add tower");
        // Export the next automatic recognition input after the confirmed tower projection is visible.
        var previousInput = monitor.LastRecognitionImage;
        yield return Wait(() => monitor.LastRecognitionImage != null &&
            !ReferenceEquals(previousInput, monitor.LastRecognitionImage), "No continuous RGB available for export.");
        SaveRecognitionInput();
        controller.SetPaused(true);
        Check(board.TryMove(tower.BrickId, free[2].X, free[2].Y), "Could not move tower during battle.");
        yield return Wait(() => Matches(free[1], null) && Matches(free[2], "red"), "Tower move was not automatically confirmed during battle.");
        yield return VerifyDisplay(originalBattle, free[2], GameRole.Tower, 2, "move tower");
        Check(controller.State.Paused, "Refreshing a moved tower lost the user pause.");
        controller.SetPaused(false);
        Check(board.TryDelete(tower.BrickId), "Could not delete tower during battle.");
        yield return Wait(() => Matches(free[2], null), "Tower deletion was not automatically confirmed during battle.");
        yield return VerifyDisplay(originalBattle, free[2], GameRole.None, 1, "delete tower");
        Check(board.TryDelete(wall.BrickId), "Could not delete wall during battle.");
        yield return Wait(() => Matches(free[0], null), "Wall deletion was not automatically confirmed during battle.");
        yield return VerifyDisplay(originalBattle, free[0], GameRole.None, 0, "delete wall");

        for (int i = 0; i < 250 && !controller.State.CanStartWave && !controller.State.Finished; i++) controller.StepManual(5);
        Check(controller.State.CanStartWave, "Could not reach the wave interval.");
        Check(board.TryAdd(free[1].X, free[1].Y, "red", out BoardLayout.Brick intervalTower), "Could not add a tower between waves.");
        yield return Wait(() => Matches(free[1], "red"), "Between-wave tower was not automatically confirmed.");
        yield return VerifyDisplay(originalBattle, free[1], GameRole.Tower, 1, "between-wave tower");
        Check(controller.State.CanStartWave, "Refreshing between waves started or reset the wave.");
        Check(controller.StartNextWave(), "Could not start the final wave.");
        for (int i = 0; i < 250 && !controller.State.Finished; i++) controller.StepManual(5);
        Check(controller.State.Phase == BattlePhase.Victory, "Could not reach a completed victory.");
        int wonHealth = controller.State.CoreHealth;
        Check(board.TryMove(intervalTower.BrickId, free[2].X, free[2].Y), "Could not move tower after victory.");
        yield return Wait(() => Matches(free[1], null) && Matches(free[2], "red"), "Post-victory tower move was not automatically confirmed.");
        yield return VerifyDisplay(originalBattle, free[2], GameRole.Tower, 1, "post-victory move", false);
        Check(controller.State.Phase == BattlePhase.Victory && controller.State.CoreHealth == wonHealth,
            "Post-victory display refresh changed the completed result.");
        Check(result.immediateConfirmations >= 6, "Running, paused and between-wave changes did not exercise immediate confirmation callbacks.");
        monitor.UpdateApplied -= confirmedObserver;
        confirmedObserver = null;
        yield return VerifyLocalOverlap();
        yield return VerifyProjectionInput();
    }

    private static void SaveRecognitionInput()
    {
        var rgb = monitor.LastRecognitionImage;
        if (rgb == null) return;
        Check(!ProjectionCaptureGate.IsSuppressed && monitor.CompensatingProjection,
            "Exported recognition input was captured outside continuous projection mode.");
        var texture = new Texture2D(rgb.Width, rgb.Height, TextureFormat.RGB24, false);
        try
        {
            var pixels = new Color32[rgb.Width * rgb.Height];
            for (int y = 0; y < rgb.Height; y++)
                for (int x = 0; x < rgb.Width; x++)
                {
                    rgb.TryRead(x, y, out byte r, out byte g, out byte b);
                    pixels[(rgb.Height - 1 - y) * rgb.Width + x] = new Color32(r, g, b, 255);
                }
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(Path.Combine(UnityEngine.Application.dataPath, "..", "continuous-recognition-input.png"),
                texture.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(texture); }
    }

    private static IEnumerator VerifyProjectionInput()
    {
        monitor.Automatic = false; controller.SetAutoStep(false);
        var capture = Object.FindFirstObjectByType<SimulationCameraCapture>();
        var light = adapter.Projection.GetComponent<SimulationProjectedLight>();
        var blue = new Texture2D(2, 2); blue.SetPixels(Enumerable.Repeat(Color.blue, 4).ToArray()); blue.Apply();
        var white = new Texture2D(2, 2); white.SetPixels(Enumerable.Repeat(Color.white, 4).ToArray()); white.Apply();
        float strength = light.LightStrength;
        try
        {
            light.LightStrength = 0.35f; light.CalibrationPattern = blue;
            SimulationObservationImage photograph = capture.CaptureObservationImage(true);
            monitor.AnalyzeOnce();
            bool same = true;
            for (int y = 0; y < photograph.Height && same; y++) for (int x = 0; x < photograph.Width && same; x++)
            {
                monitor.LastRecognitionImage.TryRead(x, y, out byte r, out byte g, out byte b);
                int i = (y * photograph.Width + x) * 3;
                same &= Math.Abs(r - photograph.Rgb[i]) <= 2 && Math.Abs(g - photograph.Rgb[i + 1]) <= 2 && Math.Abs(b - photograph.Rgb[i + 2]) <= 2;
            }
            Check(same, "Recognition input differs from the actual projected camera RGB.");
            Check(!ProjectionCaptureGate.IsSuppressed && capture.CaptureWithoutProjection == false, "Input verification used a hidden clean capture.");
            Check(monitor.LastRawScore.ExactMatches >= 95, "Photometric compensation failed on the actual blue projection: " + monitor.ReportText);
            long confirmed = monitor.Session.Confirmed.Version;
            light.LightStrength = 4; light.CalibrationPattern = white;
            monitor.AnalyzeOnce();
            Check(monitor.LastRawScore.ExactMatches < 100 && monitor.Session.Confirmed.Version == confirmed,
                "An actually overexposed RGB photograph guessed a fully known layout.");
            var modeButton = GameObject.Find("ProjectionSamplingMode").GetComponent<UnityEngine.UI.Button>();
            modeButton.onClick.Invoke();
            Check(capture.CaptureWithoutProjection && monitor.Session.Confirmed.Version == confirmed, "Mode switch discarded the confirmed layout.");
            monitor.AnalyzeOnce(); monitor.AnalyzeOnce();
            Check(monitor.Session.Confirmed.Version == confirmed, "Mode switch reused a previous mode's pending frames.");
            monitor.AnalyzeOnce();
            Check(monitor.LastRawScore.ExactMatches == 100, "Clean contrast mode no longer recognizes RGB.");
            monitor.ToggleSamplingMode();
            Check(!capture.CaptureWithoutProjection, "Mode switch did not restore continuous compensation.");
            yield return null;
            bool staleRejected = false;
            var calibration = (Brick2Life.Vision.VisionCalibration)typeof(SimulationVisionMonitor)
                .GetField("calibration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(monitor);
            try { light.GetObservationLighting(board.Definition, calibration, photograph, out _, out _); }
            catch (InvalidOperationException) { staleRejected = true; }
            Check(staleRejected, "A previous RGB frame was paired with the current projector output.");
        }
        finally { light.CalibrationPattern = null; light.LightStrength = strength; Object.DestroyImmediate(blue); Object.DestroyImmediate(white); }
        yield return null;
    }

    private static IEnumerator VerifyLocalOverlap()
    {
        controller.SetAutoStep(false);
        Check(controller.GenerateNewRound(2, 3), "Cannot prepare the local-overlap round.");
        controller.WavePlan = new WavePlan(1, 50, 9999, 1.4, 1, 0.2);
        yield return Wait(() => adapter.Feedback.Level != null && monitor.HasBaseline, "Local-overlap baseline did not confirm.");
        controller.StartBattle();
        yield return Wait(() => controller.Running, "Local-overlap battle did not start.");
        controller.StepManual(3);
        var target = controller.State.Enemies.First(enemy => !controller.Round.IsReserved(
            (int)Math.Floor(enemy.X / 2), (int)Math.Floor(enemy.Y / 2)));
        var wallCell = new GridPoint((int)Math.Floor(target.X / 2), (int)Math.Floor(target.Y / 2));
        BattleSimulation sameBattle = controller.Battle;
        var previousRoutes = controller.DisplayRoutes;
        Check(board.TryAdd(wallCell.X, wallCell.Y, "green", out _), "Cannot place a wall under the fixture enemy.");
        yield return Wait(() => Matches(wallCell, "green"), "Wall under enemy did not confirm from automatic RGB.");
        Check(controller.Level.IsValid && controller.State.PendingEnemyReroutes > 0 && !controller.State.UsingPreviousRoutes,
            "A locally overlapped enemy caused global route fallback in Unity.");
        Check(!ReferenceEquals(previousRoutes, controller.DisplayRoutes) && controller.DisplayRoutes.Count == 3,
            "New entrance routes were not immediately displayed.");
        var status = (UnityEngine.UI.Text)typeof(SimulationBattlePanel)
            .GetField("statusText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller.Panel);
        Check(status.text.Contains("脱离") && !status.text.Contains("布局堵路"), "UI mislabeled local escape as global blockage.");
        yield return VerifyDisplay(sameBattle, wallCell, GameRole.Wall, 1, "local-overlap wall");
        int oldSpawns = controller.State.SpawnedThisWave;
        controller.StepManual(3);
        yield return null;
        Check(controller.State.Phase == BattlePhase.Spawning && controller.State.SpawnedThisWave > oldSpawns &&
            controller.State.PendingEnemyReroutes == 0 && !controller.State.UsingPreviousRoutes,
            "Old enemies did not reconnect while new enemies kept spawning.");
        Check(!status.text.Contains("正脱离") && !status.text.Contains("布局堵路"), "UI kept an obsolete escape warning.");
        Check(controller.State.Enemies.Where(enemy => enemy.Id > oldSpawns).All(enemy =>
            !controller.Level.CellAt((int)Math.Floor(enemy.X), (int)Math.Floor(enemy.Y)).Blocked),
            "New enemies in Unity continued into the wall on the old route.");
    }
    private static IEnumerator VerifyDisplay(BattleSimulation originalBattle, GridPoint cell, GameRole role, int count, string stage, bool running = true)
    {
        yield return Wait(() => adapter.Feedback.Level != null && adapter.Feedback.Level.At(cell.X, cell.Y).Role == role,
            stage + ": confirmed level did not refresh without retry.");
        Check(controller.Running == running && ReferenceEquals(originalBattle, controller.Battle), stage + ": battle restarted or stopped unexpectedly.");
        yield return Wait(() => controller.Level != null && controller.Level.CellAt(cell.X * 2, cell.Y * 2).Role == role,
            stage + ": battle display map is stale despite a new confirmed layout.");
        ProjectionPlan plan = adapter.Projection.LastPlan;
        Check(plan != null && plan.LevelVersion == adapter.Feedback.Level.SourceVersion, stage + ": source projection version is stale.");
        bool hasMark = plan != null && plan.Marks.Any(mark => mark.Kind == ProjectionMarkKind.RoleHighlight &&
            mark.Role == role && Math.Abs(mark.CenterX - cell.X - 0.5f) < 0.001f && Math.Abs(mark.CenterY - cell.Y - 0.5f) < 0.001f);
        // Towers use octopus meshes; their optional role-highlight target is disabled by default.
        Check(role == GameRole.None || role == GameRole.Tower ? !plan.Marks.Any(mark => mark.Kind == ProjectionMarkKind.RoleHighlight &&
            Math.Abs(mark.CenterX - cell.X - 0.5f) < 0.001f && Math.Abs(mark.CenterY - cell.Y - 0.5f) < 0.001f) : hasMark,
            stage + ": role projection remains at an old position.");
        var field = typeof(SimulationProjectionWindow).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic);
        var windowPreview = (SimulationProjectionPreview)field.GetValue(SimulationDualWindowSession.DisplayWindow);
        yield return Wait(() => windowPreview.DisplayedVersion == adapter.Feedback.Level.SourceVersion,
            stage + ": actual display window did not refresh without retry.");
        Check(windowPreview.BrickCount == count, stage + ": actual window brick geometry is stale.");
        MeshRenderer[] windowBricks = windowPreview.Camera.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>()).ToArray();
        bool brickAtCell = windowBricks.Any(renderer => renderer.name == "ConfirmedBrick_" + cell.X + "_" + cell.Y &&
            Vector3.Distance(renderer.transform.position, new Vector3(cell.X + 0.5f, 0.5f, cell.Y + 0.5f)) < 0.001f);
        Check(brickAtCell == (role != GameRole.None), stage + ": actual window retained the old brick position.");
        var expectedTowers = Enumerable.Range(0, 100).Where(i => adapter.Feedback.Level.At(i % 10, i / 10).Role == GameRole.Tower)
            .Select(i => new Vector2(i % 10 + 0.5f, i / 10 + 0.5f)).ToArray();
        var overlay = controller.GetComponent<SimulationBattleOverlay>();
        var routeSegments = new List<Vector3>();
        bool centeredRoutes = true;
        foreach (BattleRoute route in controller.DisplayRoutes)
            for (int i = 1; i < route.Route.Count; i++)
            {
                GridPoint a = route.Route[i - 1], b = route.Route[i];
                centeredRoutes &= a.X != b.X ? a.Y % 2 == 1 && a.Y == b.Y : a.X % 2 == 1 && a.X == b.X;
                routeSegments.Add(new Vector3((a.X + b.X) * 0.25f, 0f, (a.Y + b.Y) * 0.25f));
            }
        Check(centeredRoutes, stage + ": entrance routes run along physical-cell edges.");
        yield return null;
        MeshRenderer[] lanes = overlay.MarksRoot.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "BattlePath").ToArray();
        Check(lanes.Length == 1 && lanes[0].forceRenderingOff && lanes[0].transform.position.y == 0f,
            stage + ": route content should use one shared floor projection, including at junctions.");
        var fieldTexture = lanes.FirstOrDefault()?.sharedMaterial.mainTexture as Texture2D;
        Check(fieldTexture != null && routeSegments.All(position =>
        {
            Color sample = fieldTexture.GetPixelBilinear(position.x / 10f, position.z / 10f);
            return sample.a > 0.9f && sample.b < 0.5f;
        }), stage + ": the current field does not follow the centered enemy routes.");
        foreach (string name in new[] { "BattleRange", "OctopusFace" })
        {
            MeshRenderer[] renderers = overlay.MarksRoot.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == name).ToArray();
            Check(renderers.Length == expectedTowers.Length && expectedTowers.All(position => renderers.Any(r =>
                Vector2.Distance(new Vector2(r.transform.position.x, r.transform.position.z), position) < 0.001f)),
                stage + ": actual " + name + " renderer count/positions did not follow confirmed towers.");
        }
        using (var pure = new SimulationProjectionPreview(board.Palette))
        {
            pure.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, true);
            Check(pure.DisplayedVersion == plan.LevelVersion && !pure.BricksVisible && pure.ProjectionCount > 0,
                stage + ": projection-only consumer did not refresh.");
        }
        result.boundaries.Add(stage + ": " + Boundary() + "; window=" + windowPreview.DisplayedVersion);
    }
}
