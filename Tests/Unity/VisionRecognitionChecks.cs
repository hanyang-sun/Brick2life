using System;
using System.Globalization;
using System.IO;
using System.Text;
using Brick2Life.Core;
using Brick2Life.Simulation;
using Brick2Life.Vision;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Runs in an isolated project against the real TopCamera, palette and brick meshes.
public static class VisionRecognitionChecks
{
    [Serializable] private sealed class Result
    {
        public bool passed;
        public int assertions, rawExact, eventCount;
        public double projectionError;
        public string events, report, error;
    }

    private static Result result;
    private static string output;
    private static int startedAt;

    public static void Run()
    {
        result = new Result();
        output = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Brick2LifeSimulation.unity");
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
        if (Time.frameCount < startedAt + 8) return;
        EditorApplication.update -= Tick;
        try { Verify(); result.passed = true; Finish(null); }
        catch (Exception error) { Finish(error); }
    }

    private static void Verify()
    {
        var monitor = UnityEngine.Object.FindFirstObjectByType<SimulationVisionMonitor>();
        var board = UnityEngine.Object.FindFirstObjectByType<SimulationBoardSession>();
        var capture = UnityEngine.Object.FindFirstObjectByType<SimulationCameraCapture>();
        Camera camera = GameObject.Find("TopCamera").GetComponent<Camera>();
        Check(monitor != null && monitor.enabled && board != null && board.IsInitialized && capture != null, "Vision monitor was not bound.");
        monitor.Configure(3, false);
        SimulationObservationImage image = capture.CaptureObservationImage();
        result.projectionError = ProjectionError(camera, image);
        SavePng(image);
        monitor.AnalyzeOnce();
        monitor.AnalyzeOnce();
        monitor.AnalyzeOnce();
        Check(monitor.LastError == null, monitor.LastError);
        Check(monitor.HasBaseline && monitor.LastOutcome == Brick2Life.Application.UpdateOutcome.Baseline, "Three stable frames did not establish a baseline.");
        CheckScore(monitor.LastRawScore, camera, image);
        Check(monitor.Events.Count == 0, "Baseline emitted change events.");
        Canvas.ForceUpdateCanvases();
        Check(monitor.IsPanelVisible, "Recognition panel was not open in the center.");
        RectTransform accuracy = GameObject.Find("VisionAccuracy").GetComponent<RectTransform>();
        RectTransform matrix = GameObject.Find("VisionMatrix").GetComponent<RectTransform>();
        Vector3[] accuracyCorners = new Vector3[4];
        Vector3[] matrixCorners = new Vector3[4];
        accuracy.GetWorldCorners(accuracyCorners);
        matrix.GetWorldCorners(matrixCorners);
        Check(matrixCorners[1].y <= accuracyCorners[0].y + 0.5f, "Status text overlaps the matrix.");
        Check(monitor.MatrixRowWidth > 300 && monitor.MatrixRowWidthDelta >= 0 && monitor.MatrixRowWidthDelta < 1f,
            "Matrix rows are not equal width: " + monitor.MatrixRowWidth + " delta " + monitor.MatrixRowWidthDelta);
        monitor.SetPanelVisible(false);
        Check(!monitor.IsPanelVisible, "The switch did not hide the recognition panel.");
        monitor.AnalyzeOnce();
        Check(monitor.LastRawScore != null && monitor.LastRawScore.ExactMatches == 100, "Closing the panel stopped recognition.");
        monitor.SetPanelVisible(true);
        int eventsBefore = monitor.Events.Count;

        Vector2Int empty = FindEmpty(board);
        Check(board.TryAdd(empty.x, empty.y, "green", out BoardLayout.Brick added), "Could not add a green brick.");
        monitor.AnalyzeOnce();
        image = capture.CaptureObservationImage();
        CheckScore(monitor.LastRawScore, camera, image);
        Check(monitor.Events.Count == eventsBefore, "Add was emitted before three agreeing frames.");
        monitor.AnalyzeOnce();
        monitor.AnalyzeOnce();
        Check(HasEvent(monitor, LayoutEventKind.Add, empty.x, empty.y, "green"), "Add event did not match the new green brick. " + EventText(monitor));
        CheckScore(monitor.LastStableScore, camera, image);

        Check(board.TryDelete(added.BrickId), "Could not delete the green brick.");
        monitor.AnalyzeOnce();
        monitor.AnalyzeOnce();
        monitor.AnalyzeOnce();
        image = capture.CaptureObservationImage();
        CheckScore(monitor.LastRawScore, camera, image);
        Check(HasEvent(monitor, LayoutEventKind.Remove, empty.x, empty.y, "green"), "Remove event did not match the deleted brick. " + EventText(monitor));

        BoardLayout.Brick moving = board.Bricks[0];
        int fromX = moving.GridX, fromY = moving.GridY;
        string color = moving.ColorId;
        Check(board.TryMove(moving.BrickId, empty.x, empty.y), "Could not move a brick.");
        monitor.AnalyzeOnce();
        monitor.AnalyzeOnce();
        monitor.AnalyzeOnce();
        image = capture.CaptureObservationImage();
        CheckScore(monitor.LastRawScore, camera, image);
        LayoutChangeEvent move = Latest(monitor, LayoutEventKind.Move);
        Check(move != null && move.From.HasValue && move.From.Value.X == fromX && move.From.Value.Y == fromY &&
            move.Cell.X == empty.x && move.Cell.Y == empty.y && move.ColorId == color && !move.Ambiguous,
            "Move event did not match the dragged brick. " + EventText(monitor));
        Vector2Int corner = FindEmpty(board);
        Check(board.TryAdd(corner.x, corner.y, "yellow", out _), "Could not add a yellow brick.");
        monitor.AnalyzeOnce();
        monitor.AnalyzeOnce();
        monitor.AnalyzeOnce();
        image = capture.CaptureObservationImage();
        CheckScore(monitor.LastRawScore, camera, image);
        Check(HasEvent(monitor, LayoutEventKind.Add, corner.x, corner.y, "yellow"), "Yellow corner brick was not reported. " + EventText(monitor));
        Check(monitor.ReportText.Contains("100/100") && monitor.ReportText.Contains("增加") && monitor.ReportText.Contains("移动") && monitor.ReportText.Contains("黄"),
            "Recognition panel did not show the matrix accuracy and events.");
        for (int extra = 0; extra < 5; extra++)
        {
            Vector2Int cell = FindEmpty(board);
            Check(board.TryAdd(cell.x, cell.y, "blue", out BoardLayout.Brick extraBrick), "Could not add another brick for the event log.");
            monitor.AnalyzeOnce(); monitor.AnalyzeOnce(); monitor.AnalyzeOnce();
            Check(board.TryDelete(extraBrick.BrickId), "Could not remove the extra brick.");
            monitor.AnalyzeOnce(); monitor.AnalyzeOnce(); monitor.AnalyzeOnce();
        }
        Check(monitor.Events.Count > 8, "Not enough events to require scrolling.");
        ScrollRect eventScroll = GameObject.Find("VisionEventScroll").GetComponent<ScrollRect>();
        Check(eventScroll != null && eventScroll.vertical && !eventScroll.horizontal, "Event log is not a vertical scroll view.");
        Check(eventScroll.content.GetComponentInChildren<Text>(true).text.Contains(monitor.Events[0].ToString()), "The earliest event is missing from the scroll log.");
        Check(eventScroll.content.GetComponentInChildren<Text>(true).text.Contains(monitor.Events[monitor.Events.Count - 1].ToString()), "The latest event is missing from the scroll log.");
        Canvas.ForceUpdateCanvases();
        Check(eventScroll.content.rect.height > eventScroll.viewport.rect.height, "The event list does not extend beyond the visible area.");
        Check(GameObject.Find("VisionMatrix").GetComponentInParent<ScrollRect>() == null, "The matrix was placed inside the event scroller.");
        foreach (Graphic graphic in eventScroll.GetComponentsInChildren<Graphic>(true))
            Check(!graphic.raycastTarget, "Event area still blocks the workbench: " + graphic.name);
        var pointer = new PointerEventData(EventSystem.current);
        Vector3[] corners = new Vector3[4];
        eventScroll.viewport.GetWorldCorners(corners);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f);
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(!hits.Exists(hit => hit.gameObject == eventScroll.gameObject || hit.gameObject.transform.IsChildOf(eventScroll.transform)),
            "A click on the event list does not reach the workbench.");
        float before = eventScroll.verticalNormalizedPosition;
        eventScroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, 1f) });
        float after = eventScroll.verticalNormalizedPosition;
        if (Mathf.Abs(after - before) < 0.001f)
        {
            eventScroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -1f) });
            after = eventScroll.verticalNormalizedPosition;
        }
        Check(Mathf.Abs(after - before) > 0.001f, "The mouse wheel did not scroll the event list.");
        result.rawExact = monitor.LastRawScore.ExactMatches;
        result.eventCount = monitor.Events.Count;
        result.events = EventText(monitor);
        result.report = monitor.ReportText;
        Check(result.projectionError < 1.5, "Camera matrix projection disagrees with WorldToViewportPoint by " +
            result.projectionError.ToString("0.00", CultureInfo.InvariantCulture) + " pixels.");
    }

    private static double ProjectionError(Camera camera, SimulationObservationImage image)
    {
        var projection = new CameraProjection(image.Width, image.Height, image.WorldToCamera, image.Projection);
        double worst = 0;
        Vector3[] points = { new Vector3(5, 1, 5), new Vector3(0, 1, 0), new Vector3(10, 1, 0), new Vector3(10, 1, 10), new Vector3(0, 1, 10) };
        foreach (Vector3 point in points)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            Check(viewport.z > 0 && projection.TryProject(point.x, point.y, point.z, out double pixelX, out double pixelY), "Top point is outside the camera.");
            projection.TryProject(point.x, point.y, point.z, out pixelX, out pixelY);
            double expectedX = viewport.x * image.Width - 0.5;
            double expectedY = (1.0 - viewport.y) * image.Height - 0.5;
            worst = Math.Max(worst, Math.Max(Math.Abs(pixelX - expectedX), Math.Abs(pixelY - expectedY)));
        }
        return worst;
    }

    private static void CheckScore(RecognitionScore score, Camera camera, SimulationObservationImage image)
    {
        Check(score != null && score.ExactMatches == 100, Describe(score, camera, image));
    }

    private static string Describe(RecognitionScore score, Camera camera, SimulationObservationImage image)
    {
        if (score == null) return "No recognition score.";
        var text = new StringBuilder();
        text.Append("Accuracy ").Append(score.ExactMatches).Append('/').Append(score.Total).Append(". ");
        foreach (string mismatch in score.Mismatches)
        {
            text.Append(mismatch).Append(' ').Append(Pixel(camera, image, mismatch)).Append("; ");
        }
        return text.ToString();
    }

    private static string Pixel(Camera camera, SimulationObservationImage image, string mismatch)
    {
        int open = mismatch.IndexOf('('), comma = mismatch.IndexOf(',', open + 1), close = mismatch.IndexOf(')', comma + 1);
        if (open < 0 || comma < 0 || close < 0) return "";
        int x = int.Parse(mismatch.Substring(open + 1, comma - open - 1), CultureInfo.InvariantCulture);
        int y = int.Parse(mismatch.Substring(comma + 1, close - comma - 1), CultureInfo.InvariantCulture);
        Vector3 viewport = camera.WorldToViewportPoint(new Vector3(x + 0.5f, 1f, y + 0.5f));
        int pixelX = Mathf.FloorToInt((float)(viewport.x * image.Width - 0.5f) + 0.5f);
        int pixelY = Mathf.FloorToInt((float)((1.0 - viewport.y) * image.Height - 0.5f) + 0.5f);
        if (pixelX < 0 || pixelY < 0 || pixelX >= image.Width || pixelY >= image.Height) return "pixel outside";
        int index = (pixelY * image.Width + pixelX) * 3;
        return "rgb(" + image.Rgb[index] + "," + image.Rgb[index + 1] + "," + image.Rgb[index + 2] + ")";
    }

    private static Vector2Int FindEmpty(SimulationBoardSession board)
    {
        BoardSnapshot snapshot = board.Snapshot();
        for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
                if (snapshot.At(x, y).Occupancy == Occupancy.Empty) return new Vector2Int(x, y);
        throw new InvalidOperationException("The board has no empty cell.");
    }

    private static bool HasEvent(SimulationVisionMonitor monitor, LayoutEventKind kind, int x, int y, string color)
    {
        foreach (LayoutChangeEvent item in monitor.Events)
            if (item.Kind == kind && item.Cell.X == x && item.Cell.Y == y && item.ColorId == color) return true;
        return false;
    }

    private static LayoutChangeEvent Latest(SimulationVisionMonitor monitor, LayoutEventKind kind)
    {
        LayoutChangeEvent found = null;
        foreach (LayoutChangeEvent item in monitor.Events)
            if (item.Kind == kind) found = item;
        return found;
    }

    private static string EventText(SimulationVisionMonitor monitor)
    {
        var text = new StringBuilder();
        foreach (LayoutChangeEvent item in monitor.Events) text.Append(item).Append(" | ");
        return text.ToString();
    }

    private static void SavePng(SimulationObservationImage image)
    {
        var colors = new Color32[image.Width * image.Height];
        for (int y = 0; y < image.Height; y++)
        {
            int source = y * image.Width * 3;
            int destination = (image.Height - 1 - y) * image.Width;
            for (int x = 0; x < image.Width; x++)
            {
                int index = source + x * 3;
                colors[destination + x] = new Color32(image.Rgb[index], image.Rgb[index + 1], image.Rgb[index + 2], 255);
            }
        }
        var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGB24, false);
        texture.SetPixels32(colors);
        texture.Apply(false, false);
        File.WriteAllBytes(Path.Combine(output, "vision-frame.png"), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void Check(bool condition, string message)
    {
        result.assertions++;
        if (!condition) throw new Exception(message);
    }

    private static void Finish(Exception error)
    {
        if (error != null) { result.error = error.ToString(); Debug.LogException(error); }
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(result, true));
        Debug.Log("VISION_RECOGNITION_CHECKS: " + JsonUtility.ToJson(result));
        EditorApplication.Exit(result.passed ? 0 : 1);
    }
}
