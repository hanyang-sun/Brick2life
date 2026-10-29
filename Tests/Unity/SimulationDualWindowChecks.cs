using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Brick2Life.Application;
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
        public int assertions, captureChangedPixels;
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
            if (EditorApplication.timeSinceStartup - startedAt > 50) throw new TimeoutException("Dual-window checks timed out.");
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
        Check(board.IsEditingLocked, "Opening two windows removed the battle lock.");
        SimulationDualWindowSession.DisplayWindow.Focus();
        SimulationEditAccess.Refresh();
        yield return null;
        SimulationDualWindowSession.LayoutWindow.Focus();
        SimulationEditAccess.Refresh();
        yield return null;
        editor.ClearBoard();
        Check(board.IsEditingLocked && board.Version == boardVersion,
            "Returning edit focus bypassed the active battle lock.");
        Require(controller.Restart(), "Could not reset the focus/battle fixture.");
        Check(!board.IsEditingLocked, "Reset did not release the battle lock with two windows open.");
        SimulationDualWindowSession.Close();
        Check(workspace.cullingMask == mask && SimulationEditAccess.CanEdit, "Closing windows did not restore camera/input configuration.");
        Check(board.Version == boardVersion && board.Bricks.Count == sourceCount, "Opening/closing windows mutated the source board.");
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
