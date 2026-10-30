using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

public static class SimulationDualWindowChecks
{
    [Serializable] private sealed class Result
    {
        public bool passed;
        public int assertions, captureChangedPixels, projectionChangedPixels, cleanCaptureMismatchPixels;
        public float automaticConfirmationSeconds;
        public List<string> failures = new List<string>();
    }
    private static Result result;
    private static IEnumerator sequence;
    private static int lastFrame = -1;
    private static double startedAt;

    public static void Run()
    {
        result = new Result();
        EditorSceneManager.OpenScene("Assets/Scenes/Brick2LifeSimulation.unity");
        Require(Object.FindObjectsByType<SimulationBattleOverlay>(FindObjectsSortMode.None).Length == 1,
            "The saved scene must contain exactly one battle projection overlay.");
        SimulationFeedbackSetup.InstallAndSave();
        SimulationFeedbackSetup.InstallAndSave();
        Require(Object.FindObjectsByType<SimulationBattleOverlay>(FindObjectsSortMode.None).Length == 1,
            "Reinstalling feedback duplicated the battle projection overlay.");
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.playModeStateChanged += OnPlay;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= OnPlay;
        sequence = Verify();
        startedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - startedAt > 90) throw new TimeoutException("Dual-window checks timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (!sequence.MoveNext()) Finish();
        }
        catch (Exception error) { result.failures.Add(error.ToString()); Finish(); }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        (sequence as IDisposable)?.Dispose();
        SimulationDualWindowSession.Close();
        SimulationEditAccess.ConfigureFocus(null);
        result.passed = result.failures.Count == 0;
        File.WriteAllText(Path.Combine(UnityEngine.Application.dataPath, "..", "verification.json"), JsonUtility.ToJson(result, true));
        Debug.Log("DUAL_WINDOW_CHECKS_" + (result.passed ? "PASSED" : "FAILED") + ": " + result.assertions + " assertions.");
        EditorApplication.Exit(result.passed ? 0 : 1);
    }

    private static void Check(bool condition, string message)
    {
        result.assertions++;
        if (!condition) result.failures.Add(message);
    }

    private static void Require(bool condition, string message)
    {
        result.assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static IEnumerator Verify()
    {
        for (int i = 0; i < 8; i++) yield return null;
        var board = Object.FindFirstObjectByType<SimulationBoardSession>();
        var editor = Object.FindFirstObjectByType<SimulationBrickEditor>();
        var adapter = Object.FindFirstObjectByType<SimulationLevelAdapter>();
        var capture = Object.FindFirstObjectByType<SimulationCameraCapture>();
        var monitor = Object.FindFirstObjectByType<SimulationVisionMonitor>();
        var pointer = Object.FindFirstObjectByType<SimulationGridPointer>();
        var controller = Object.FindFirstObjectByType<SimulationBattleController>();
        Camera workspace = GameObject.Find("WorkspaceCamera").GetComponent<Camera>();
        Camera overhead = GameObject.Find("TopCamera").GetComponent<Camera>();
        Require(board != null && editor != null && adapter != null && capture != null && monitor != null && controller != null,
            "Saved simulation scene is missing a required module.");
        monitor.Configure(1, false);
        // Explicit confirmed-observation fixtures test the consumer seam, not RGB recognition accuracy.
        var cells = new ObservedCell[board.Definition.CellCount];
        for (int i = 0; i < cells.Length; i++) cells[i] = new ObservedCell(BoardCell.Empty);
        cells[board.Definition.Index(0, 0)] = new ObservedCell(new BoardCell(Occupancy.Occupied, "blue"));
        cells[board.Definition.Index(9, 9)] = new ObservedCell(new BoardCell(Occupancy.Occupied, "red"));
        ObservationRequest request = monitor.Session.BeginFrame("dual-window-fixture", DateTimeOffset.UnixEpoch);
        BoardUpdate update = monitor.Session.Confirm(request, new FrameObservation(board.Definition, request.FrameId,
            request.SessionId, request.Sequence, request.CapturedAtUtc, request.CalibrationId, cells));
        adapter.Apply(update);
        for (int i = 0; i < 2; i++) yield return null;
        int boardVersion = board.Version;
        int sourceCount = board.Bricks.Count;
        Vector3 workspacePosition = workspace.transform.position;
        Quaternion workspaceRotation = workspace.transform.rotation;
        int mask = workspace.cullingMask;
        int captureMask = overhead.cullingMask;
        RenderTexture captureTexture = overhead.targetTexture;
        bool projectionVisible = adapter.Projection.IsVisible;
        SimulationObservationImage before = capture.CaptureObservationImage(true);
        using (var preview = new SimulationProjectionPreview(board.Palette))
        {
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, false);
            Check(preview.BrickCount == 2 && preview.BricksVisible, "Combined mode did not use the confirmed layout.");
            Check(preview.DisplayedVersion == update.Snapshot.Version, "Displayed layout version differs from B's confirmed version.");
            Check(preview.Camera.scene != workspace.gameObject.scene, "Display camera shares the source scene.");
            Check(preview.ProjectionCount > 0, "No projection marks were mirrored.");
            Save(preview.Render(900, 650), "combined.png");
            Vector3 viewPosition = preview.Camera.transform.position;
            preview.Orbit(new Vector2(55, 15));
            preview.Zoom(-2);
            Check((preview.Camera.transform.position - viewPosition).sqrMagnitude > 0.1f, "Combined mode cannot orbit/zoom.");
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, true);
            Check(!preview.BricksVisible && preview.BrickCount == 2, "Projection-only mode changed rather than hid the presentation bricks.");
            preview.ResetView();
            Save(preview.Render(900, 650), "projection-only.png");
            viewPosition = preview.Camera.transform.position;
            preview.Orbit(new Vector2(-30, -15)); preview.Zoom(1);
            Check((preview.Camera.transform.position - viewPosition).sqrMagnitude > 0.1f, "Projection-only mode cannot orbit/zoom.");
            preview.Render(700, 600);
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, false);
            Check(preview.BricksVisible, "Switching back did not restore presentation bricks.");
            preview.UpdateScene(null, adapter.Projection, controller.View, true);
            Check(preview.BrickCount == 0 && preview.ProjectionCount == 0,
                "An unconfirmed session displayed stale bricks/projection marks.");
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, false);
            Check(preview.BrickCount == 2 && preview.ProjectionCount > 0, "Confirmed display did not recover after waiting for confirmation.");
            Check(board.Version == boardVersion && board.Bricks.Count == sourceCount, "Display changed the source board.");
            Check(adapter.Projection.IsVisible == projectionVisible, "Display changed source projection visibility.");
        }
        Check(workspace.transform.position == workspacePosition && workspace.transform.rotation == workspaceRotation,
            "Preview orbit changed the original camera.");
        Check(overhead.cullingMask == captureMask && overhead.targetTexture == captureTexture, "Display changed the recognition camera configuration.");
        SimulationObservationImage after = capture.CaptureObservationImage(true);
        for (int i = 0; i < before.Rgb.Length; i += 3)
            if (Math.Abs(before.Rgb[i] - after.Rgb[i]) > 1 || Math.Abs(before.Rgb[i + 1] - after.Rgb[i + 1]) > 1 ||
                Math.Abs(before.Rgb[i + 2] - after.Rgb[i + 2]) > 1) result.captureChangedPixels++;
        Check(result.captureChangedPixels <= before.Width * before.Height / 10000,
            "Preview rendering changed the source RGB capture: " + result.captureChangedPixels + " pixels.");

        bool focused = false;
        SimulationEditAccess.ConfigureFocus(() => focused);
        editor.ClearBoard();
        Check(board.Version == boardVersion, "Readonly display focus allowed clearing the source board.");
        Check(!pointer.TryPickCell(Vector2.zero, out _, out _), "Display focus allowed source mouse picking.");
        Check(!controller.AddSpawn(new GridPoint(1, 1)), "Display focus allowed changing entrances.");
        focused = true;
        Check(SimulationEditAccess.CanEdit, "Returning to the original window did not immediately allow input.");
        SimulationEditAccess.Refresh();
        SimulationEditAccess.ConfigureFocus(null);

        SimulationDualWindowSession.Open();
        Require(SimulationDualWindowSession.LayoutWindow != null && SimulationDualWindowSession.DisplayWindow != null,
            "The two independent windows were not created.");
        yield return null;
        yield return null;
        Check(SimulationDualWindowSession.LayoutWindow != SimulationDualWindowSession.DisplayWindow, "Editing and display share one window.");
        Check((workspace.cullingMask & ((1 << 30) | (1 << 31))) == 0, "Raw layout camera still includes gameplay/projection layers.");
        Check(overhead.cullingMask == captureMask, "Opening the windows changed the recognition camera mask.");
        SimulationDualWindowSession.DisplayWindow.Focus();
        SimulationEditAccess.Refresh();
        yield return null;
        Check(!SimulationEditAccess.CanEdit, "Focusing the display window allowed source editing.");
        editor.ClearBoard();
        Check(board.Version == boardVersion, "The display window focus allowed clearing the source board.");
        SimulationDualWindowSession.LayoutWindow.Focus();
        SimulationEditAccess.Refresh();
        yield return null;
        Check(SimulationEditAccess.CanEdit, "Clicking back to the original window did not restore edit focus.");
        controller.SetAutoStep(false);
        Require(controller.AddSpawn(new GridPoint(1, 1)), "Could not prepare an entrance for the combined focus/battle check.");
        Require(controller.StartBattle(), "Could not start the focus/battle fixture.");
        Check(!board.IsEditingLocked, "Running battle still prevents live brick editing.");
        SimulationDualWindowSession.DisplayWindow.Focus();
        SimulationEditAccess.Refresh();
        yield return null;
        editor.ClearBoard();
        Check(board.Version == boardVersion, "Readonly display focus allowed an edit during battle.");
        SimulationDualWindowSession.LayoutWindow.Focus();
        SimulationEditAccess.Refresh();
        yield return null;
        Check(!board.IsEditingLocked && SimulationEditAccess.CanEdit && board.Version == boardVersion,
            "Returning edit focus did not allow live editing or changed the board by itself.");
        Require(controller.Restart(), "Could not reset the focus/battle fixture.");
        Check(!board.IsEditingLocked, "Reset did not release the battle lock with two windows open.");
        SimulationDualWindowSession.Close();
        Check(workspace.cullingMask == mask && SimulationEditAccess.CanEdit, "Closing windows did not restore camera/input configuration.");
        Check(board.Version == boardVersion && board.Bricks.Count == sourceCount, "Opening/closing windows mutated the source board.");
        IEnumerator integration = VerifyProjectionIntegration(board, editor, adapter, capture, monitor, controller, overhead);
        while (integration.MoveNext()) yield return integration.Current;
    }

    private static IEnumerator VerifyProjectionIntegration(SimulationBoardSession board, SimulationBrickEditor editor,
        SimulationLevelAdapter adapter, SimulationCameraCapture capture, SimulationVisionMonitor monitor,
        SimulationBattleController controller, Camera overhead)
    {
        // Real RGB recognition of an explicitly arranged test scene; GT is used only by the normal evaluator.
        editor.ClearBoard();
        Require(board.TryAdd(9, 9, "blue", out BoardLayout.Brick core), "Could not arrange the RGB core fixture.");
        Require(board.TryAdd(5, 5, "red", out BoardLayout.Brick tower), "Could not arrange the RGB tower fixture.");
        Require(board.TryAdd(4, 5, "green", out _), "Could not arrange the RGB wall fixture.");
        capture.CaptureWithoutProjection = true;
        monitor.Configure(3, false);
        for (int i = 0; i < 3; i++) monitor.AnalyzeOnce();
        Require(monitor.LastRawScore.ExactMatches == 100 && adapter.Feedback.Level.IsPlayable,
            "Clean RGB did not establish the playable three-brick fixture.");
        Check(adapter.Projection.MaterialNameOf(GameRole.Core) == "ProjectionCore", "Core emblem material is missing.");
        Check(adapter.Projection.MaterialNameOf(GameRole.Tower) == "ProjectionTower", "Tower pattern material is missing.");
        Check(adapter.Projection.MaterialNameOf(GameRole.Wall) == "ProjectionWall", "Wall pattern material is missing.");
        controller.ClearSpawns();
        Require(controller.AddSpawn(new GridPoint(3, 3)), "Could not set the integration entrance.");
        controller.WavePlan = new WavePlan(1, 1, 2000, 0.1, 1, 1);
        controller.TowerSettings = new TowerSettings(100, 1, 8, 0.1);
        controller.SetAutoStep(false);
        Require(board.TryMove(core.BrickId, 8, 8), "Could not move the core before a fresh confirmation.");
        typeof(SimulationVisionMonitor).GetField("interval", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(monitor, 9999f);
        monitor.Automatic = true;
        controller.SelectSpawns = true;
        Check(!controller.StartBattle() && controller.PendingStart && !controller.Running,
            "Starting used an old confirmed layout instead of waiting for fresh RGB.");
        SimulationEditAccess.ConfigureFocus(() => false);
        for (int i = 0; i < 2; i++)
        {
            monitor.AnalyzeOnce();
            yield return null;
            Check(controller.PendingStart && !controller.Running, "Battle started before the fresh confirmation window.");
        }
        monitor.AnalyzeOnce();
        yield return null;
        Require(controller.Running && !controller.PendingStart, "Fresh RGB confirmation did not start the queued battle.");
        monitor.Automatic = false;
        Check(controller.Level.CellAt(16, 16).IsCore && !controller.Level.CellAt(18, 18).IsCore,
            "Battle did not replan to the newly confirmed core.");
        Check(!controller.SelectSpawns && editor.enabled && !board.IsEditingLocked,
            "Deferred start did not end spawn selection or left the board locked.");
        SimulationEditAccess.ConfigureFocus(null);
        yield return null;
        var overlay = controller.GetComponent<SimulationBattleOverlay>();
        Require(overlay != null && overlay.MarksRoot != controller.transform, "Battle projection has no isolated source root.");
        Require(overlay.ActiveArrowCount == 7, "Flowing route arrows are missing.");
        Require(overlay.ActiveRangeRingCount == 1, "There must be exactly one visible range ring per physical tower.");
        Require(overlay.ActiveTowerArrowCount == 1, "There must be exactly one aiming arrow per physical tower.");
        Transform rangeRing = overlay.MarksRoot.Find("BattleRange");
        Transform aimArrow = overlay.MarksRoot.Find("BattleTowerAim");
        Check(rangeRing != null && Vector2.Distance(new Vector2(rangeRing.position.x, rangeRing.position.z),
            new Vector2(5.5f, 5.5f)) < 0.001f, "Tower range ring is not centered on its physical brick.");
        Check(aimArrow != null && Vector2.Distance(new Vector2(aimArrow.position.x, aimArrow.position.z),
            new Vector2(5.5f, 5.5f)) < 0.001f, "Tower aiming arrow is not centered on its physical brick.");
        controller.StepManual(0.2f);
        Require(controller.LastShots.Count > 0, "The integration battle did not produce shooting events.");
        controller.SetPaused(true);
        yield return null;
        int effects = overlay.ActiveEffectCount;
        Check(effects > 0, "Shooting events did not produce flashes/hit effects.");
        yield return null;
        yield return null;
        Check(overlay.ActiveEffectCount == effects, "Paused/manual frames consumed the same shooting batch repeatedly.");
        SimulationObservationImage clean = capture.CaptureObservationImage(false);
        SimulationObservationImage disabled;
        using (ProjectionCaptureGate.Suppress())
        {
            Check(!adapter.Projection.IsVisible, "Capture suppression did not hide role projections.");
            foreach (Renderer renderer in overlay.MarksRoot.GetComponentsInChildren<Renderer>())
                Check(renderer.forceRenderingOff, "Capture suppression left a battle projection visible.");
            disabled = capture.CaptureObservationImage(true);
        }
        result.cleanCaptureMismatchPixels = ChangedPixels(clean, disabled);
        Check(result.cleanCaptureMismatchPixels == 0, "Clean capture contains residual battle projection pixels.");
        Check(adapter.Projection.IsVisible, "Capture did not restore the role projection state.");
        foreach (Renderer renderer in overlay.MarksRoot.GetComponentsInChildren<Renderer>())
            Check(!renderer.forceRenderingOff, "Capture did not restore a battle projection renderer.");
        result.projectionChangedPixels = ChangedPixels(clean, capture.CaptureObservationImage(true));
        Check(result.projectionChangedPixels > 0, "Retaining projection did not change the actual overhead RGB.");
        RenderTexture target = overhead.targetTexture;
        try
        {
            overhead.targetTexture = null;
            bool threw = false;
            try { capture.CaptureObservationImage(false); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "Missing capture target was not rejected.");
        }
        finally { overhead.targetTexture = target; }
        Check(adapter.Projection.IsVisible, "Failed capture left the projector hidden.");
        foreach (Renderer renderer in overlay.MarksRoot.GetComponentsInChildren<Renderer>())
            Check(!renderer.forceRenderingOff, "Failed capture left a battle renderer suppressed.");
        using (var preview = new SimulationProjectionPreview(board.Palette))
        {
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, false);
            int expected = adapter.Projection.ActiveMarkCount + overlay.ActiveArrowCount +
                overlay.ActiveRangeRingCount + overlay.ActiveTowerArrowCount + overlay.ActiveEffectCount;
            Check(preview.ProjectionCount == expected, "Combined preview omitted or duplicated battle projection meshes.");
            Save(preview.Render(1000, 720), "battle-combined.png");
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, true);
            Check(!preview.BricksVisible && preview.ProjectionCount == expected,
                "Projection-only mode lost battle effects or kept confirmed brick models.");
            Save(preview.Render(1000, 720), "battle-projection-only.png");
        }
        float fadeStarted = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - fadeStarted < 1.8f) yield return null;
        bool faded = true;
        for (int i = 0; i < adapter.Projection.LastPlan.Marks.Count; i++)
        {
            ProjectionMark mark = adapter.Projection.LastPlan.Marks[i];
            if (mark.Kind == ProjectionMarkKind.ChangeHighlight &&
                adapter.Projection.MarksRoot.GetChild(i).gameObject.activeSelf) faded = false;
        }
        Check(faded, "Change rings did not fade away while the battle was paused.");
        BattleSimulation liveRun = controller.Battle;
        BattleState liveBefore = controller.State;
        BattleLevel oldMap = controller.Level;
        Require(board.TryAdd(4, 3, "green", out var wall), "Cannot add a wall during the live RGB battle.");
        monitor.AnalyzeOnce();
        yield return null;
        Check(ReferenceEquals(oldMap, controller.Level), "Unconfirmed single RGB frame changed the battle map.");
        monitor.AnalyzeOnce(); monitor.AnalyzeOnce();
        yield return null;
        Check(!ReferenceEquals(oldMap, controller.Level) && ReferenceEquals(liveRun, controller.Battle) &&
            controller.Level.CellAt(8, 6).Blocked, "Confirmed live wall did not replan the same battle.");
        Check(controller.State.Paused && Math.Abs(controller.State.Elapsed - liveBefore.Elapsed) < 1e-8 &&
            controller.State.CoreHealth == liveBefore.CoreHealth &&
            controller.State.Enemies[0].Id == liveBefore.Enemies[0].Id &&
            Math.Abs(controller.State.Enemies[0].X - liveBefore.Enemies[0].X) < 1e-8 &&
            Math.Abs(controller.State.Enemies[0].Y - liveBefore.Enemies[0].Y) < 1e-8,
            "Live wall update reset positions, time, health or user pause.");
        Require(board.TryMove(tower.BrickId, 5, 4), "Cannot move a live tower.");
        ConfirmRgb(monitor);
        yield return null;
        Check(controller.Level.CellAt(10, 8).Role == GameRole.Tower && controller.Level.CellAt(10, 10).Role == GameRole.None,
            "RGB-confirmed tower move did not update the battle map.");
        Require(board.TryDelete(wall.BrickId), "Cannot remove a live wall.");
        ConfirmRgb(monitor);
        yield return null;
        Check(!controller.Level.CellAt(8, 6).Blocked, "Removed live wall remains in navigation.");
        controller.SetPaused(false);
        Require(board.TryDelete(core.BrickId), "Cannot remove core during live battle.");
        ConfirmRgb(monitor);
        yield return null;
        Require(controller.State.WaitingForLayout && !board.IsEditingLocked, "Missing core did not wait with editing available.");
        double heldTime = controller.State.Elapsed;
        controller.StepManual(1);
        Check(Math.Abs(controller.State.Elapsed - heldTime) < 1e-8, "Missing-core wait advanced the battle.");
        Require(board.TryAdd(7, 8, "blue", out var restoredCore), "Cannot repair the missing core.");
        ConfirmRgb(monitor);
        yield return null;
        Check(!controller.State.WaitingForLayout && !controller.State.Paused && ReferenceEquals(liveRun, controller.Battle) &&
            controller.Level.CellAt(14, 16).IsCore, "RGB core repair did not resume the existing run at its new location.");
        controller.StepManual(0.1f);
        Check(controller.State.Elapsed > heldTime, "Recovered live battle did not advance.");
        Require(board.TryDelete(tower.BrickId), "Cannot remove the live tower.");
        ConfirmRgb(monitor);
        yield return null;
        Check(overlay.ActiveRangeRingCount == 0 && overlay.ActiveTowerArrowCount == 0,
            "Removed tower still has a range ring or aiming arrow.");
        Require(board.TryAdd(6, 5, "red", out _), "Cannot place a replacement tower.");
        Require(board.TryMove(restoredCore.BrickId, 8, 8), "Cannot move the recovered core.");
        ConfirmRgb(monitor);
        yield return null;
        Check(overlay.ActiveRangeRingCount == 1 && overlay.ActiveTowerArrowCount == 1 && controller.Level.CellAt(16, 16).IsCore,
            "Replacement tower/core did not update live projection and navigation.");
        using (var preview = new SimulationProjectionPreview(board.Palette))
        {
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, false);
            Save(preview.Render(1000, 720), "live-edited-combined.png");
        }
        Require(controller.Restart(), "Could not restart the combined integration battle.");
        yield return null;
        Check(!board.IsEditingLocked && overlay.ActiveArrowCount == 0 && overlay.ActiveRangeRingCount == 0 &&
            overlay.ActiveEffectCount == 0, "Retry left a battle lock or old projection effects behind.");
        monitor.Automatic = true;
        Check(!controller.StartBattle() && controller.PendingStart, "Could not queue a second fresh confirmation.");
        Check(controller.Restart() && !controller.PendingStart && !board.IsEditingLocked,
            "Retry did not cancel a pending start.");
        monitor.Automatic = false;
        controller.ClearSpawns();
        Require(controller.AddSpawn(new GridPoint(8, 7)), "Could not arrange the core-impact fixture.");
        controller.WavePlan = new WavePlan(1, 1, 2000, 20, 10, 1);
        Require(controller.StartBattle(), "Could not start the core-impact fixture.");
        yield return null;
        controller.StepManual(2f);
        yield return null;
        Check(controller.State.CoreHealth == 90, "The core-impact fixture did not leak exactly one enemy.");
        bool coreImpact = false;
        var property = new MaterialPropertyBlock();
        foreach (MeshRenderer renderer in overlay.MarksRoot.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.name != "BattleFx") continue;
            renderer.GetPropertyBlock(property);
            Color color = property.GetColor(Shader.PropertyToID("_Color"));
            if (color.r < 0.9f || color.g > 0.4f) continue;
            coreImpact = true;
            Check(Vector2.Distance(new Vector2(renderer.transform.position.x, renderer.transform.position.z),
                new Vector2(8.5f, 8.5f)) < 0.001f && renderer.transform.position.y > 1f,
                "Core damage ring is not centered above the confirmed core brick.");
        }
        Check(coreImpact, "Core damage produced no visible impact ring.");
        Require(controller.Restart(), "Could not reset the core-impact fixture.");
        yield return null;
        typeof(SimulationVisionMonitor).GetField("interval", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(monitor, 0.15f);
        monitor.Configure(3, true);
        float automaticStarted = Time.realtimeSinceStartup;
        Check(!controller.StartBattle() && controller.PendingStart, "Automatic sampling did not request a fresh start.");
        while (!controller.Running && Time.realtimeSinceStartup - automaticStarted < 3f) yield return null;
        result.automaticConfirmationSeconds = Time.realtimeSinceStartup - automaticStarted;
        Require(controller.Running, "Automatic RGB sampling failed to confirm and start within 3 seconds.");
        Check(monitor.LastRawScore.ExactMatches == 100 && !board.IsEditingLocked,
            "Automatic confirmation lost RGB accuracy or disabled live editing.");
        monitor.Automatic = false;
        controller.Restart();
    }

    private static void ConfirmRgb(SimulationVisionMonitor monitor)
    {
        for (int i = 0; i < 3; i++) monitor.AnalyzeOnce();
    }

    private static int ChangedPixels(SimulationObservationImage a, SimulationObservationImage b)
    {
        int count = 0;
        for (int i = 0; i < a.Rgb.Length; i += 3)
            if (Math.Abs(a.Rgb[i] - b.Rgb[i]) > 1 || Math.Abs(a.Rgb[i + 1] - b.Rgb[i + 1]) > 1 ||
                Math.Abs(a.Rgb[i + 2] - b.Rgb[i + 2]) > 1) count++;
        return count;
    }

    private static void Save(RenderTexture target, string name)
    {
        RenderTexture old = RenderTexture.active;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(UnityEngine.Application.dataPath, "..", name), image.EncodeToPNG());
        }
        finally { RenderTexture.active = old; Object.DestroyImmediate(image); }
    }
}
