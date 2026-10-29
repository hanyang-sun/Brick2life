using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Brick2Life.Application;
using Brick2Life.Battle;
using Brick2Life.Core;
using Brick2Life.Feedback;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Play-mode checks use real board writes, editor buttons and battle lifecycle calls.
public static class BattleEditingLockChecks
{
    [Serializable] private sealed class Result
    {
        public bool passed;
        public int assertions;
        public List<string> failures = new List<string>();
        public List<string> uiStates = new List<string>();
    }
    private static Result result;
    private static IEnumerator sequence;
    private static int lastFrame = -1;
    private static double startedAt;

    public static void Run()
    {
        result = new Result();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
            if (EditorApplication.timeSinceStartup - startedAt > 40) throw new TimeoutException("Play-mode editing-lock checks timed out.");
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
        result.passed = result.failures.Count == 0;
        File.WriteAllText(Path.Combine(UnityEngine.Application.dataPath, "..", "verification.json"), JsonUtility.ToJson(result, true));
        Debug.Log("BATTLE_EDITING_LOCK_CHECKS_" + (result.passed ? "PASSED" : "FAILED") + ": " + result.assertions + " assertions.");
        EditorApplication.Exit(result.passed ? 0 : 1);
    }

    private static void Check(bool value, string message)
    {
        result.assertions++;
        if (!value) result.failures.Add(message);
    }

    private static void Require(bool value, string message)
    {
        result.assertions++;
        if (!value) throw new InvalidOperationException(message);
    }

    private static IEnumerator Verify()
    {
        using (var rig = new Rig())
        {
            yield return null;
            yield return null;
            rig.CheckUi(false, "Before battle");
            SetField(rig.Editor, "draggedId", rig.TowerId);
            Require(rig.Controller.StartBattle(), "Battle did not start.");
            Check(GetField(rig.Editor, "draggedId") == null, "Battle start did not cancel the pending drag.");
            yield return null; // CanvasGroup changes propagate to Selectable during the UI frame update.
            rig.CheckLocked("Running");
            rig.Controller.SetPaused(true);
            rig.CheckLocked("Paused");
            rig.Controller.SetPaused(false);
            rig.FinishWave();
            Require(rig.Controller.State.Phase == BattlePhase.AwaitingNextWave, "Did not reach the wave interval.");
            rig.CheckLocked("Between waves");
            Require(rig.Controller.StartNextWave(), "Next wave did not start.");
            rig.CheckUi(true, "Next wave");
            rig.Controller.SetPaused(true);
            Require(rig.Controller.Restart(), "Restart failed.");
            yield return null;
            rig.CheckUi(false, "After restart");
            Check(!rig.Locked, "Restart did not release the board lock.");
            Check(rig.Board.TryAdd(1, 1, "red", out _), "Editing did not recover after restart.");
            Require(rig.Controller.StartBattle(), "A new battle did not start.");
            Check(rig.Locked, "A second battle did not reacquire the lock.");
            UnityEngine.Object.DestroyImmediate(rig.Controller);
            Check(!rig.Locked, "Destroying the battle owner left the board locked.");
        }
        yield return null;
        using (var rig = new Rig())
        {
            yield return null;
            yield return null;
            rig.Controller.WavePlan = new WavePlan(1, 1, 1, 10, 100, 0.1);
            Require(rig.Controller.StartBattle(), "Defeat fixture did not start.");
            yield return null;
            rig.FinishWave();
            Require(rig.Controller.State.Phase == BattlePhase.Defeat, "Did not reach defeat.");
            yield return null;
            rig.CheckUi(false, "After defeat");
            Check(!rig.Locked && rig.Board.TryMove(rig.TowerId, 4, 3), "Defeat did not restore movement.");
            rig.Clear.onClick.Invoke();
            Check(rig.Board.Bricks.Count == 0, "Clear button did not recover after defeat.");
        }
        yield return null;
        using (var rig = new Rig())
        {
            yield return null;
            yield return null;
            rig.Controller.WavePlan = new WavePlan(1, 1, 1, 10, 1, 0.1);
            rig.Controller.TowerSettings = new TowerSettings(5, 10, 40, 0.1);
            Require(rig.Controller.StartBattle(), "Victory fixture did not start.");
            yield return null;
            rig.FinishWave();
            Require(rig.Controller.State.Phase == BattlePhase.Victory, "Did not reach victory.");
            yield return null;
            rig.CheckUi(false, "After victory");
            Check(!rig.Locked && rig.Board.TryAdd(2, 2, "green", out _), "Victory did not restore adding bricks.");
        }
        yield return null;
        using (var rig = new Rig())
        {
            yield return null;
            yield return null;
            rig.Controller.ClearSpawns();
            Check(!rig.Controller.StartBattle(), "An empty entrance list started a battle.");
            rig.CheckUi(false, "Failed start");
            Check(!rig.Locked && rig.Board.TryAdd(2, 2, "green", out _), "A failed start locked editing.");
        }
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object GetField(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private sealed class Rig : IDisposable
    {
        private readonly GameObject root;
        private readonly Material[] materials = new Material[3];
        private readonly SimulationLevelAdapter adapter;
        private readonly List<Button> editingButtons = new List<Button>();
        public SimulationBoardSession Board { get; }
        public SimulationBrickEditor Editor { get; }
        public SimulationBattleController Controller { get; }
        public SimulationFeedbackDemo Demo { get; }
        public SimulationBattlePanel Panel { get; }
        public Button Clear { get; }
        public string TowerId { get; }
        // Reflection keeps the pre-fix version compilable, so the missing lock also produces a failing test.
        public bool Locked => (bool?)typeof(SimulationBoardSession).GetProperty("IsEditingLocked")?.GetValue(Board) ?? false;

        public Rig()
        {
            root = new GameObject("EditingLockCheck");
            root.SetActive(false);
            Transform bricks = new GameObject("Bricks").transform;
            bricks.SetParent(root.transform, false);
            var entries = new SimulationBoardSession.PaletteEntry[3];
            string[] colors = { "blue", "red", "green" };
            Shader shader = Shader.Find("Sprites/Default");
            Require(shader != null, "Test material shader is missing.");
            for (int i = 0; i < entries.Length; i++)
            {
                materials[i] = new Material(shader);
                entries[i] = new SimulationBoardSession.PaletteEntry { colorId = colors[i], label = colors[i], material = materials[i] };
            }
            Board = root.AddComponent<SimulationBoardSession>();
            Board.Initialize(bricks, entries);
            Require(Board.TryAdd(9, 9, "blue", out _), "Fixture core could not be created.");
            Require(Board.TryAdd(4, 4, "red", out var tower), "Fixture tower could not be created.");
            TowerId = tower.BrickId;

            var canvas = new GameObject("EditorCanvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(root.transform, false);
            var controls = new GameObject("BrickTools", typeof(RectTransform));
            controls.transform.SetParent(canvas.transform, false);
            Button[] tools = { Button(controls.transform, "Add"), Button(controls.transform, "Delete"), Button(controls.transform, "Move") };
            editingButtons.AddRange(tools);
            Button template = Button(controls.transform, "ColorTemplate");
            Clear = Button(controls.transform, "ClearBoardButton");
            editingButtons.Add(Clear);
            var status = new GameObject("Status", typeof(RectTransform), typeof(Text));
            status.transform.SetParent(controls.transform, false);
            var pointerObject = new GameObject("UnusedPointer");
            pointerObject.SetActive(false);
            pointerObject.transform.SetParent(root.transform, false);
            var pointer = pointerObject.AddComponent<SimulationGridPointer>();
            Editor = root.AddComponent<SimulationBrickEditor>();
            SetField(Editor, "pointer", pointer);
            SetField(Editor, "toolButtons", tools);
            SetField(Editor, "colorButtonTemplate", template);
            SetField(Editor, "colorButtonContainer", controls.GetComponent<RectTransform>());
            SetField(Editor, "statusLabel", status.GetComponent<Text>());
            Editor.Bind(Board);
            Clear.onClick.AddListener(Editor.ClearBoard);

            adapter = root.AddComponent<SimulationLevelAdapter>();
            SetField(adapter, "rules", new[]
            {
                new SimulationLevelAdapter.RoleRule { colorId = "blue", role = GameRole.Core },
                new SimulationLevelAdapter.RoleRule { colorId = "red", role = GameRole.Tower },
                new SimulationLevelAdapter.RoleRule { colorId = "green", role = GameRole.Wall }
            });
            var session = new BoardSyncSession(Board.Definition, "editing-lock-check");
            adapter.Bind(session);
            var cells = new ObservedCell[session.Definition.CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = new ObservedCell(BoardCell.Empty);
            cells[session.Definition.Index(9, 9)] = new ObservedCell(new BoardCell(Occupancy.Occupied, "blue"));
            cells[session.Definition.Index(4, 4)] = new ObservedCell(new BoardCell(Occupancy.Occupied, "red"));
            var request = session.BeginFrame("fixture", DateTimeOffset.UnixEpoch);
            adapter.Apply(session.Confirm(request, new FrameObservation(session.Definition, request.FrameId, request.SessionId,
                request.Sequence, request.CapturedAtUtc, request.CalibrationId, cells)));
            Controller = root.AddComponent<SimulationBattleController>();
            Controller.Bind(adapter);
            Controller.SetBrickEditor(Editor);
            Controller.SetAutoStep(false);
            Controller.WavePlan = new WavePlan(2, 1, 1, 10, 1, 0.1);
            Controller.TowerSettings = new TowerSettings(5, 1, 0.1, 1);
            Require(Controller.AddSpawn(new GridPoint(0, 0)), "Fixture entrance could not be created.");
            Demo = root.AddComponent<SimulationFeedbackDemo>();
            SetField(Demo, "keyboardDemo", false);
            SetField(Demo, "logToConsole", false);
            Demo.Bind(session, adapter);
            var panelRoot = new GameObject("BattlePanelRoot");
            panelRoot.transform.SetParent(root.transform, false);
            Panel = panelRoot.AddComponent<SimulationBattlePanel>();
            SetField(Panel, "controller", Controller);
            SetField(Controller, "panel", Panel);
            root.SetActive(true);
        }

        private static Button Button(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
            return go.GetComponent<Button>();
        }

        public void CheckUi(bool locked, string context)
        {
            var colors = GetField(Editor, "colorButtons") as Button[];
            Require(colors != null && colors.Length == 3, context + ": editor UI was not initialized.");
            var group = GetField(Editor, "editingControls") as CanvasGroup;
            result.uiStates.Add(context + ": boardLocked=" + Locked + ", group=" + (group == null ? "missing" :
                group.name + "/interactable=" + group.interactable + "/enabled=" + group.enabled + "/active=" + group.gameObject.activeInHierarchy) +
                ", firstButton=" + editingButtons[0].IsInteractable() + ", parent=" + editingButtons[0].transform.parent.name);
            foreach (Button button in editingButtons) Check(button.IsInteractable() != locked, context + ": " + button.name + " has the wrong interaction state.");
            foreach (Button button in colors) Check(button.IsInteractable() != locked, context + ": color button has the wrong interaction state.");
            var spawnButton = GetField(Panel, "spawnButton") as Button;
            var clearSpawnButton = GetField(Panel, "clearSpawnButton") as Button;
            Require(spawnButton != null && clearSpawnButton != null, context + ": entrance buttons were not initialized.");
            Check(spawnButton.IsInteractable() != locked, context + ": entrance selection button has the wrong interaction state.");
            Check(clearSpawnButton.IsInteractable() != locked, context + ": clear-entrance button has the wrong interaction state.");
        }

        public void CheckLocked(string context)
        {
            Check(Locked, context + ": board is not locked.");
            CheckUi(true, context);
            int version = Board.Version;
            int count = Board.Bricks.Count;
            BoardSnapshot snapshot = Board.Snapshot();
            Check(!Board.TryAdd(2, 2, "green", out _), context + ": adding a brick was allowed.");
            Check(!Board.TryMove(TowerId, 4, 3), context + ": moving a brick was allowed.");
            Check(!Board.TryDelete(TowerId), context + ": deleting a brick was allowed.");
            bool rejected = false;
            try { Board.ReplaceLayout(snapshot); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, context + ": replacing/recoloring the layout was allowed.");
            Clear.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Clear.onClick.Invoke(); // Intentionally bypass Button.Press: target methods must also be safe.
            object previousTool = GetField(Editor, "tool");
            object previousColor = GetField(Editor, "colorIndex");
            Editor.SelectTool(2);
            Editor.SelectColor(2);
            Check(previousTool.Equals(GetField(Editor, "tool")) && previousColor.Equals(GetField(Editor, "colorIndex")),
                context + ": editor tool/color callbacks were not locked.");
            typeof(SimulationBrickEditor).GetMethod("HandlePointerDown", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Editor, new object[] { Vector2.zero });
            SetField(Editor, "draggedId", TowerId);
            typeof(SimulationBrickEditor).GetMethod("EndDrag", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Editor, new object[] { Vector2.zero });
            Check(GetField(Editor, "draggedId") == null, context + ": mouse release did not cancel dragging while locked.");
            Check(Board.Version == version && Board.Bricks.Count == count, context + ": callback or pointer input changed the board.");
            long confirmedVersion = adapter.Feedback.AppliedVersion;
            Demo.SubmitEmpty();
            Demo.NextSample();
            Check(adapter.Feedback.AppliedVersion == confirmedVersion, context + ": F5/F6 demo callbacks changed the level.");
            Controller.SelectSpawns = true;
            Check(!Controller.SelectSpawns, context + ": entrance selection was enabled during battle.");
            Check(!Controller.AddSpawn(new GridPoint(8, 0)), context + ": an entrance was added.");
            Controller.ClearSpawns();
            var clearEntrance = (Button)GetField(Panel, "clearSpawnButton");
            clearEntrance.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            clearEntrance.onClick.Invoke();
            Check(Controller.Spawns.Count == 1, context + ": entrances were cleared.");
        }

        public void FinishWave()
        {
            for (int i = 0; i < 200 && !Controller.State.Finished && !Controller.State.CanStartWave; i++)
                Controller.StepManual(0.1f);
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (Material material in materials) if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
