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
public static class BattleLiveEditingChecks
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
            if (EditorApplication.timeSinceStartup - startedAt > 40) throw new TimeoutException("Play-mode live-editing checks timed out.");
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
        Debug.Log("BATTLE_LIVE_EDITING_CHECKS_" + (result.passed ? "PASSED" : "FAILED") + ": " + result.assertions + " assertions.");
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
        SimulationEditAccess.ConfigureFocus(null);
        using (var rig = new Rig())
        {
            yield return null;
            yield return null;
            Require(rig.Controller.StartBattle(), "Live editing fixture did not start.");
            yield return null;
            var battle = rig.Controller.Battle;
            Check(!rig.Board.IsEditingLocked, "Battle still locks the board model.");
            rig.CheckUi(false, "Running, editable");
            Require(rig.Board.TryAdd(2, 2, "green", out var added), "Cannot place a brick during battle.");
            Check(rig.Board.TryMove(added.BrickId, 2, 3), "Cannot move a brick during battle.");
            Check(rig.Board.TryDelete(added.BrickId), "Cannot remove a brick during battle.");
            rig.Editor.SelectTool(2);
            Check(Convert.ToInt32(GetField(rig.Editor, "tool")) == 2, "Move tool is disabled during battle.");
            rig.Clear.onClick.Invoke();
            Check(rig.Board.Bricks.Count == 0, "Clear button does not work during battle.");
            rig.Confirm();
            Check(rig.Controller.State.WaitingForLayout && ReferenceEquals(battle, rig.Controller.Battle),
                "Deleting the core reset the battle instead of waiting for a repair.");
            double frozen = rig.Controller.State.Elapsed;
            rig.Controller.StepManual(1);
            Check(Math.Abs(rig.Controller.State.Elapsed - frozen) < 1e-8, "Invalid layout advanced battle time.");
            Require(rig.Board.TryAdd(8, 8, "blue", out var core), "Cannot restore core while waiting.");
            Require(rig.Board.TryAdd(4, 4, "red", out var tower), "Cannot restore tower while waiting.");
            rig.Confirm((8, 8, "blue"), (4, 4, "red"));
            Check(!rig.Controller.State.WaitingForLayout && ReferenceEquals(battle, rig.Controller.Battle),
                "Repair did not resume the same battle.");
            rig.Controller.SetPaused(true);
            yield return null;
            rig.CheckUi(false, "Paused, editable");
            Check(rig.Board.TryMove(core.BrickId, 7, 8), "Cannot move core while paused.");
            rig.Confirm((7, 8, "blue"), (4, 4, "red"));
            Check(rig.Controller.State.Paused && rig.Controller.Level.CellAt(14, 16).IsCore,
                "Paused edit lost pause state or retained old core.");
            int version = rig.Board.Version;
            SimulationEditAccess.ConfigureFocus(() => false);
            rig.Editor.ClearBoard();
            Check(rig.Board.Version == version, "Readonly display focus allowed an edit.");
            SimulationEditAccess.ConfigureFocus(null);
            rig.Controller.SelectSpawns = true;
            Check(!rig.Controller.SelectSpawns && !rig.Controller.AddSpawn(new GridPoint(8, 0)),
                "Live brick editing unexpectedly enabled entrance changes.");
            rig.Controller.ClearSpawns();
            Check(rig.Controller.Spawns.Count == 1, "Entrances changed during battle.");
            rig.Controller.SetPaused(false);
            rig.FinishWave();
            Require(rig.Controller.State.Phase == BattlePhase.AwaitingNextWave, "Did not reach wave interval.");
            yield return null;
            rig.CheckUi(false, "Between waves, editable");
            Check(rig.Board.TryMove(tower.BrickId, 5, 4), "Cannot move tower between waves.");
            int coreHealth = rig.Controller.State.CoreHealth;
            rig.Confirm((7, 8, "blue"), (5, 4, "red"));
            Require(rig.Controller.StartNextWave(), "Updated next wave did not start.");
            Check(rig.Controller.State.Wave == 2 && rig.Controller.State.CoreHealth == coreHealth,
                "Live edit reset wave or core health.");
            Require(rig.Controller.Restart(), "Cannot end the live battle.");
            yield return null;
            rig.CheckUi(false, "After retry");
            Check(!rig.Board.IsEditingLocked, "Retry left a model lock.");
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
        private readonly BoardSyncSession session;
        private int confirmationSequence;
        private readonly List<Button> editingButtons = new List<Button>();
        public SimulationBoardSession Board { get; }
        public SimulationBrickEditor Editor { get; }
        public SimulationBattleController Controller { get; }
        public SimulationFeedbackDemo Demo { get; }
        public SimulationBattlePanel Panel { get; }
        public Button Clear { get; }
        public string TowerId { get; }
        public bool Locked => Board.IsEditingLocked;

        public Rig()
        {
            root = new GameObject("LiveEditingCheck");
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
            session = new BoardSyncSession(Board.Definition, "live-editing-check");
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
            Check(spawnButton.IsInteractable() == !Controller.Running, context + ": entrance selection button has the wrong interaction state.");
            Check(clearSpawnButton.IsInteractable() == !Controller.Running, context + ": clear-entrance button has the wrong interaction state.");
        }

        public void Confirm(params (int x, int y, string color)[] bricks)
        {
            // Explicit observation fixture, never used as RGB recognition evidence.
            var cells = new ObservedCell[session.Definition.CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = new ObservedCell(BoardCell.Empty);
            foreach (var brick in bricks) cells[session.Definition.Index(brick.x, brick.y)] =
                new ObservedCell(new BoardCell(Occupancy.Occupied, brick.color));
            var request = session.BeginFrame("live-fixture-" + (++confirmationSequence), DateTimeOffset.UtcNow);
            adapter.Apply(session.Confirm(request, new FrameObservation(session.Definition, request.FrameId,
                request.SessionId, request.Sequence, request.CapturedAtUtc, request.CalibrationId, cells)));
            Controller.StepManual(0);
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
