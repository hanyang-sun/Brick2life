using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

// Real default scene and real RGB confirmation. No synthetic observation is submitted by these checks.
public static class GeneratedDefenseRoundChecks
{
    [Serializable] private sealed class Result
    {
        public bool passed;
        public int assertions, markerChangedPixels, previewChangedPixels, rawExact, seed, spawnCount;
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
            if (EditorApplication.timeSinceStartup - startedAt > 120) throw new TimeoutException("Generated-round checks timed out.");
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
        Debug.Log("GENERATED_DEFENSE_ROUND_CHECKS_" + (result.passed ? "PASSED" : "FAILED") + ": " + result.assertions + " assertions.");
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
        var monitor = Object.FindFirstObjectByType<SimulationVisionMonitor>();
        var capture = Object.FindFirstObjectByType<SimulationCameraCapture>();
        var adapter = Object.FindFirstObjectByType<SimulationLevelAdapter>();
        var controller = Object.FindFirstObjectByType<SimulationBattleController>();
        var markers = Object.FindFirstObjectByType<SimulationRoundMarkers>();
        Camera top = GameObject.Find("TopCamera").GetComponent<Camera>();
        Camera workspace = GameObject.Find("WorkspaceCamera").GetComponent<Camera>();
        Require(board != null && editor != null && monitor != null && capture != null && adapter != null && controller != null && markers != null,
            "Default scene is missing generated-round modules.");
        Require(controller.GeneratedObjectives && controller.Round != null, "Default scene did not generate system goals.");
        Check(controller.Round.Spawns.Count >= 1 && controller.Round.Spawns.Count <= 4, "Default spawn count is outside the configured random range.");
        Check(board.Bricks.Count == 0, "New default round starts with player/core bricks instead of an empty player board.");
        Check(!board.IsPlayerColorAvailable("blue") && !board.IsPlayerColorAvailable("yellow") &&
            board.IsPlayerColorAvailable("green") && board.IsPlayerColorAvailable("red"), "Player tools are not restricted to wall/tower colors.");
        Check((top.cullingMask & (1 << SimulationRoundMarkers.LayerIndex)) == 0 &&
            (workspace.cullingMask & (1 << SimulationRoundMarkers.LayerIndex)) != 0,
            "System marker layer is not isolated from RGB while visible to the editing camera.");
        monitor.Configure(3, false);
        capture.CaptureWithoutProjection = true;
        controller.SetAutoStep(false);
        Require(controller.GenerateNewRound(20261030, 1), "Could not generate a one-entrance round.");
        Check(controller.Round.Spawns.Count == 1 && !monitor.Automatic, "One-entrance generation changed count or enabled automatic sampling.");
        GeneratedRound one = controller.Round;
        Require(controller.GenerateNewRound(20261030, 1), "Could not regenerate the same seed.");
        Check(one.Core.Equals(controller.Round.Core) && one.Spawns.SequenceEqual(controller.Round.Spawns),
            "Same seed/count did not reproduce system goals.");
        int seed = 1;
        for (; seed < 1000; seed++)
        {
            GeneratedRound candidate = GeneratedRound.Generate(board.Definition, seed, 3);
            if (candidate.Core.X >= 2 && candidate.Core.X <= 7 && candidate.Core.Y >= 2 && candidate.Core.Y <= 7) break;
        }
        Require(seed < 1000 && controller.GenerateNewRound(seed, 3), "Could not generate an interior-core three-entrance round.");
        GeneratedRound round = controller.Round;
        result.seed = seed; result.spawnCount = round.Spawns.Count;
        Require(round.Spawns.Count == 3 && controller.Spawns.SequenceEqual(round.Spawns), "Generated entrances are not the actual battle entrances.");
        GridPoint[] reserved = new[] { round.Core }.Concat(round.Spawns).ToArray();
        GridPoint[] free = Enumerable.Range(0, board.Definition.CellCount)
            .Select(i => new GridPoint(i % board.Definition.Width, i / board.Definition.Width))
            .Where(p => !round.IsReserved(p.X, p.Y)).ToArray();
        for (int i = 0; i < 60; i++)
            Require(board.TryAdd(free[i].X, free[i].Y, i % 2 == 0 ? "green" : "red", out _), "An unintended wall/tower quota blocked placement.");
        Check(board.Bricks.Count == 60, "Unlimited construction did not allow more than ten walls/five towers.");
        BoardLayout.Brick movable = board.Bricks[0];
        foreach (GridPoint target in reserved)
        {
            int version = board.Version;
            Check(board.IsReserved(target.X, target.Y), "Board is not protecting a system goal.");
            Check(!board.TryAdd(target.X, target.Y, "green", out _) && !string.IsNullOrEmpty(board.LastPlacementIssue),
                "Player could add a wall over a system goal without an explanation.");
            Check(!board.TryMove(movable.BrickId, target.X, target.Y) && board.Version == version,
                "Player could move a brick into a system goal.");
        }
        editor.ClearBoard();
        Check(board.Bricks.Count == 0 && markers.Round == round && markers.MarkersRoot.GetComponentsInChildren<MeshRenderer>().Length == 4,
            "Clearing player bricks deleted or duplicated system goals.");
        Check(!board.TryAdd(free[0].X, free[0].Y, "blue", out _) && !board.TryAdd(free[0].X, free[0].Y, "yellow", out _),
            "Player could bypass the wall/tower tool restriction.");
        ConfirmRgb(monitor);
        result.rawExact = monitor.LastRawScore.ExactMatches;
        Require(result.rawExact == 100, "System markers contaminated independent RGB recognition.");
        Check(monitor.Session.Confirmed.At(round.Core.X, round.Core.Y).Occupancy == Occupancy.Empty,
            "B fabricated a physical brick at the system core.");
        Check(adapter.Round == round && adapter.Feedback.Level.Round == round &&
            adapter.Feedback.Level.At(round.Core.X, round.Core.Y).Role == GameRole.Core &&
            !adapter.Feedback.Level.At(round.Core.X, round.Core.Y).Occupied,
            "System core was not composed independently of predicted occupancy.");
        SimulationObservationImage markerOn = capture.CaptureObservationImage(false);
        markers.Render(null);
        SimulationObservationImage markerOff = capture.CaptureObservationImage(false);
        markers.Render(round);
        result.markerChangedPixels = ChangedPixels(markerOn, markerOff);
        Check(result.markerChangedPixels == 0, "System goal models entered the top-camera RGB.");
        SimulationDualWindowSession.Open();
        Require(SimulationDualWindowSession.LayoutWindow != null && SimulationDualWindowSession.DisplayWindow != null,
            "Two independent windows were not available in preparation.");
        SimulationDualWindowSession.DisplayWindow.Focus(); SimulationEditAccess.Refresh();
        yield return null;
        int focusVersion = board.Version;
        editor.ClearBoard();
        Check(!SimulationEditAccess.CanEdit && board.Version == focusVersion, "Display window permits source edits.");
        SimulationDualWindowSession.LayoutWindow.Focus(); SimulationEditAccess.Refresh();
        yield return null;
        Check(SimulationEditAccess.CanEdit, "Raw-layout window does not own editing focus.");

        GridPoint[] neighbors =
        {
            new GridPoint(round.Core.X - 1, round.Core.Y), new GridPoint(round.Core.X + 1, round.Core.Y),
            new GridPoint(round.Core.X, round.Core.Y - 1), new GridPoint(round.Core.X, round.Core.Y + 1)
        };
        GridPoint towerPosition = free.First(p => !neighbors.Contains(p));
        Require(board.TryAdd(towerPosition.X, towerPosition.Y, "red", out BoardLayout.Brick tower), "Could not place the first tower.");
        ConfirmRgb(monitor);
        using (var preview = new SimulationProjectionPreview(board.Palette))
        {
            SimulationObservationImage before = capture.CaptureObservationImage(false);
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, false);
            Save(preview.Render(1000, 720), "generated-round.png");
            Check(preview.BrickCount == 1 && preview.BricksVisible, "Combined preview created a player brick for the system core.");
            Check(!preview.Camera.scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MeshRenderer>())
                .Any(renderer => renderer.name.StartsWith("SystemCore") || renderer.name.StartsWith("SystemSpawn")),
                "Confirmed preview duplicated editing-only objective guides over the actual projection marks.");
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, true);
            Check(!preview.BricksVisible && preview.ProjectionCount > 0, "Projection-only view lost the system projection plan.");
            Check(adapter.Projection.LastPlan.Marks.Any(m => m.Role == GameRole.Core && m.Surface == ProjectionSurface.Table),
                "System core has no table-level projection mark.");
            foreach (GridPoint spawn in round.Spawns)
                Check(adapter.Projection.LastPlan.Marks.Any(m => Mathf.Abs(m.CenterX - (spawn.X + 0.5f)) < 0.01f &&
                    Mathf.Abs(m.CenterY - (spawn.Y + 0.5f)) < 0.01f), "A generated entrance has no projection plan mark.");
            preview.Render(900, 650);
            result.previewChangedPixels = ChangedPixels(before, capture.CaptureObservationImage(false));
            Check(result.previewChangedPixels == 0, "Preparation preview changed independent source RGB.");
        }
        controller.WavePlan = new WavePlan(2, 12, 9999, 0.5, 1, 0.1);
        controller.TowerSettings = new TowerSettings(100, 1, 40, 0.5);
        Require(controller.StartBattle(), "Could not start without a manually placed blue core or selected entrance.");
        controller.StepManual(0.35f);
        yield return null;
        BattleSimulation battle = controller.Battle;
        BattleState beforeBlock = controller.State;
        Require(beforeBlock.Enemies.Count >= 3, "Multi-entrance fixture did not create enough enemies.");
        Check(!board.IsEditingLocked && editor.enabled, "Generated battle disabled live construction.");
        Check(!controller.GenerateNewRound(seed + 1, 1) && ReferenceEquals(round, controller.Round), "New-round action changed active system goals.");
        var walls = new List<BoardLayout.Brick>();
        foreach (GridPoint neighbor in neighbors)
        {
            Require(board.TryAdd(neighbor.X, neighbor.Y, "green", out var wall), "Could not surround the generated core.");
            walls.Add(wall);
        }
        ConfirmRgb(monitor);
        yield return null;
        Require(controller.State.UsingPreviousRoutes && !controller.State.WaitingForLayout,
            "Blocked core paused the run instead of retaining previous valid routes.");
        Check(!string.IsNullOrEmpty(controller.State.RouteWarning), "Route fallback has no visible warning.");
        Check(ReferenceEquals(battle, controller.Battle) && controller.State.Wave == beforeBlock.Wave &&
            controller.State.CoreHealth == beforeBlock.CoreHealth && Math.Abs(controller.State.Elapsed - beforeBlock.Elapsed) < 1e-8,
            "Blocked-layout fallback reset run state.");
        foreach (EnemyState previous in beforeBlock.Enemies)
        {
            EnemyState current = controller.State.Enemies.Single(e => e.Id == previous.Id);
            Check(current.Health == previous.Health && Math.Abs(current.X - previous.X) < 1e-8 && Math.Abs(current.Y - previous.Y) < 1e-8,
                "Route fallback teleported or healed a live enemy.");
        }
        Require(battle.ActiveRoutes.Count == 3 && controller.View.RouteCount == 3, "Blocked-layout fallback lost or compressed entrance routes.");
        for (int i = 0; i < round.Spawns.Count; i++)
            Check(battle.ActiveRoutes[i].Spawn.Equals(new GridPoint(round.Spawns[i].X * 2, round.Spawns[i].Y * 2)),
                "Retained route was assigned to a different entrance.");
        var overlay = controller.GetComponent<SimulationBattleOverlay>();
        Check(overlay != null && overlay.ActiveArrowCount > 0, "Blocked-layout fallback hid the usable old route arrows.");
        using (var preview = new SimulationProjectionPreview(board.Palette))
        {
            preview.UpdateScene(adapter.Feedback.Level, adapter.Projection, controller.View, false);
            Save(preview.Render(1000, 720), "blocked-fallback.png");
        }
        controller.StepManual(0.7f);
        Check(controller.State.Elapsed > beforeBlock.Elapsed + 0.6 && controller.State.SpawnedThisWave > beforeBlock.SpawnedThisWave,
            "Old-route fallback stopped combat time or spawning.");
        foreach (EnemyState enemy in controller.State.Enemies)
            Check(enemy.SpawnIndex == (enemy.Id - 1) % round.Spawns.Count, "Fallback changed multi-entrance spawn rotation.");
        Require(board.TryDelete(tower.BrickId), "Could not remove a tower while routes were blocked.");
        ConfirmRgb(monitor);
        yield return null;
        Check(controller.Level.Cells.All(c => c.Role != GameRole.Tower) && overlay.ActiveRangeRingCount == 0,
            "Fallback kept a removed tower from the old map.");
        controller.StepManual(0.2f);
        Check(controller.LastShots.Count == 0, "Removed tower continued firing during fallback.");
        GridPoint replacement = free.First(p => !neighbors.Contains(p) && !p.Equals(towerPosition));
        Require(board.TryAdd(replacement.X, replacement.Y, "red", out _), "Could not add a tower during fallback.");
        ConfirmRgb(monitor);
        yield return null;
        controller.StepManual(0.6f);
        Check(controller.LastShots.Count > 0 && controller.State.UsingPreviousRoutes, "New tower did not join combat while old routes were retained.");
        controller.SetPaused(true);
        double pausedTime = controller.State.Elapsed;
        Require(board.TryDelete(walls[0].BrickId), "Could not reopen a route.");
        ConfirmRgb(monitor);
        yield return null;
        Check(!controller.State.UsingPreviousRoutes && !controller.State.WaitingForLayout && controller.State.Paused,
            "Reopened route did not clear fallback or lost user pause.");
        controller.StepManual(1);
        Check(Math.Abs(controller.State.Elapsed - pausedTime) < 1e-8, "Repair overrode explicit pause.");
        Check(ReferenceEquals(battle, controller.Battle) && controller.Round == round && controller.Spawns.SequenceEqual(round.Spawns),
            "Recovery changed the battle or fixed goals.");
        SimulationDualWindowSession.DisplayWindow.Focus(); SimulationEditAccess.Refresh();
        yield return null;
        int battleVersion = board.Version;
        editor.ClearBoard();
        Check(!SimulationEditAccess.CanEdit && board.Version == battleVersion, "Display focus allowed construction during fallback recovery.");
        SimulationDualWindowSession.LayoutWindow.Focus(); SimulationEditAccess.Refresh();
        yield return null;
        editor.ClearBoard(); ConfirmRgb(monitor);
        yield return null;
        Check(board.Bricks.Count == 0 && markers.Round == round && markers.MarkersRoot.GetComponentsInChildren<MeshRenderer>().Length == 4 &&
            adapter.Feedback.Level.At(round.Core.X, round.Core.Y).Role == GameRole.Core,
            "Live clear removed fixed goals or required a physical blue core.");
        Require(controller.Restart(), "Could not finish the generated-round fixture.");
        yield return null;
        Check(controller.Level != null && controller.View.RouteCount == 3,
            "Retry left preparation routes empty when the confirmed layout was unchanged.");
        SimulationDualWindowSession.Close(); SimulationEditAccess.ConfigureFocus(null);

        // Preparation can already be blocked; the system's empty-layout reference still supplies old routes.
        Require(controller.GenerateNewRound(seed, 3), "Could not prepare a second generated round.");
        foreach (GridPoint neighbor in neighbors)
            Require(board.TryAdd(neighbor.X, neighbor.Y, "green", out _), "Could not prepare a blocked starting layout.");
        ConfirmRgb(monitor);
        Require(controller.StartBattle() && controller.State.UsingPreviousRoutes && !controller.State.WaitingForLayout,
            "A blocked preparation layout could not start from system reference routes. " + controller.LastMessage +
            "; level=" + adapter.Feedback.Level?.Describe() + "; raw=" + monitor.LastRawScore?.ExactMatches +
            "; issues=" + string.Join(" | ", adapter.Feedback.Level?.Issues.Select(i => i.Message) ?? Enumerable.Empty<string>()));
        controller.StepManual(0.3f);
        Check(controller.State.Elapsed > 0 && controller.State.SpawnedThisWave >= 3 && controller.Battle.ActiveRoutes.Count == 3,
            "Initial reference-route fallback did not continue spawning at all entrances.");
        Require(controller.Restart(), "Could not reset the preparation-fallback run.");
        controller.WavePlan = new WavePlan(1, 1, 9999, 20, 10, 0.1);
        Require(controller.StartBattle(), "Could not start the generated core-impact fixture.");
        yield return null;
        controller.StepManual(3f);
        yield return null;
        Check(controller.State.CoreHealth == 90, "Generated core did not receive the expected leak damage.");
        var property = new MaterialPropertyBlock();
        bool impact = false;
        foreach (MeshRenderer renderer in markers.GetComponent<SimulationBattleOverlay>().MarksRoot.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.name != "BattleFx") continue;
            renderer.GetPropertyBlock(property);
            Color color = property.GetColor(Shader.PropertyToID("_Color"));
            if (color.r < 0.9f || color.g > 0.4f) continue;
            impact = true;
            Check(renderer.transform.position.y < 0.1f && Vector2.Distance(
                new Vector2(renderer.transform.position.x, renderer.transform.position.z),
                new Vector2(round.Core.X + 0.5f, round.Core.Y + 0.5f)) < 0.001f,
                "System core impact ring floats above the table or targets the wrong cell.");
        }
        Check(impact, "Generated core damage has no visible feedback.");
        controller.Restart();

        // The same confirmed layout can become routable when an enemy leaves a newly placed wall.
        Require(controller.GenerateNewRound(seed, 1), "Could not prepare the transient-overlap fixture.");
        controller.WavePlan = new WavePlan(2, 1, 9999, 1, 10, 0.1);
        ConfirmRgb(monitor);
        Require(controller.StartBattle(), "Could not start the transient-overlap fixture.");
        controller.StepManual(2.25f);
        EnemyState overlapped = controller.State.Enemies.Single();
        var occupiedCell = new GridPoint((int)Math.Floor(overlapped.X / 2), (int)Math.Floor(overlapped.Y / 2));
        Require(board.TryAdd(occupiedCell.X, occupiedCell.Y, "green", out _), "Could not place a wall under the live enemy.");
        ConfirmRgb(monitor);
        yield return null;
        Require(controller.Level.IsValid && controller.State.UsingPreviousRoutes,
            "Transient overlap did not retain old routes on an otherwise routable map.");
        LevelState sameObservation = adapter.Feedback.Level;
        BattleSimulation overlapBattle = controller.Battle;
        controller.StepManual(2.25f);
        yield return null;
        Check(ReferenceEquals(sameObservation, adapter.Feedback.Level) && ReferenceEquals(overlapBattle, controller.Battle),
            "Transient-overlap recovery replaced the confirmed layout or battle.");
        Check(!controller.State.UsingPreviousRoutes && !controller.State.WaitingForLayout &&
            string.IsNullOrEmpty(controller.State.RouteWarning),
            "Enemy left the obstacle, but unchanged confirmed layout never retried current routes.");
        Check(controller.State.Enemies.Single().Id == overlapped.Id &&
            controller.State.Enemies.Single().RouteProgress > overlapped.RouteProgress,
            "Transient-overlap recovery replaced the moving enemy.");
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
            if (Math.Abs(a.Rgb[i] - b.Rgb[i]) > 1 || Math.Abs(a.Rgb[i + 1] - b.Rgb[i + 1]) > 1 || Math.Abs(a.Rgb[i + 2] - b.Rgb[i + 2]) > 1) count++;
        return count;
    }
    private static void Save(RenderTexture target, string name)
    {
        RenderTexture old = RenderTexture.active;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(UnityEngine.Application.dataPath, "..", name), pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = old; Object.DestroyImmediate(pixels); }
    }
}
