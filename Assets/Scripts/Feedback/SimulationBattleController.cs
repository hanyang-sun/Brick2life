using System;
using System.Collections.Generic;
using Brick2Life.Application;
using Brick2Life.Battle;
using Brick2Life.Core;
using Brick2Life.Simulation;
using UnityEngine;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Runs a battle on a snapshot of the confirmed level. Entrances are chosen on empty physical cells;
    /// the level is frozen at battle start so later recognition updates cannot change the running game.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationBattleController : MonoBehaviour
    {
        [SerializeField] private SimulationLevelAdapter adapter;
        [SerializeField] private SimulationBattleView view;
        [SerializeField] private SimulationBattlePanel panel;
        [SerializeField] private SimulationGridPointer pointer;
        [SerializeField] private SimulationBrickEditor brickEditor;
        [SerializeField] private SimulationBoardSession board;
        [Tooltip("自动按帧推进战斗；关闭后由外部调用 StepManual 推进（用于确定性验证）。")]
        [SerializeField] private bool autoStep = true;

        private readonly List<GridPoint> spawns = new List<GridPoint>();
        private BattleState lastState;
        private bool selectSpawns;
        private bool pendingStart;
        private float pendingSeconds;
        private bool editingLocked;
        private GameObject visionCanvas;

        public WavePlan WavePlan { get; set; } = WavePlan.Trial;
        public TowerSettings TowerSettings { get; set; } = TowerSettings.Trial;
        public IReadOnlyList<GridPoint> Spawns => spawns.AsReadOnly();
        public BattleLevel Level { get; private set; }
        public BattleSimulation Battle { get; private set; }
        public BattleState State => Battle != null ? Battle.State : lastState;
        public bool Running => Battle != null && !Battle.State.Finished && Battle.State.Phase != BattlePhase.Ready;
        public string LastMessage { get; private set; } = "先点“点棋盘选入口”，再点棋盘上的空格";
        public SimulationBattlePanel Panel => panel;
        public SimulationBattleView View => view;

        // Picking entrances must take the left click away from the brick editor, which would otherwise add bricks.
        public bool SelectSpawns
        {
            get => selectSpawns;
            set
            {
                if (value && Running)
                {
                    LastMessage = "战斗中不能更改出生点，等本局结束后再选。";
                    if (panel != null) panel.Refresh(this);
                    return;
                }
                selectSpawns = value;
                if (value && brickEditor == null) brickEditor = FindFirstObjectByType<SimulationBrickEditor>();
                if (brickEditor != null) brickEditor.enabled = !value;
                // Hide the recognition panel while picking so the whole board stays clickable.
                if (value || visionCanvas == null) visionCanvas = GameObject.Find("VisionRecognitionCanvas");
                if (visionCanvas != null) visionCanvas.SetActive(!value);
                LastMessage = value
                    ? "入口选择已开启：在棋盘空格上点选，点“结束选择”退出"
                    : "入口选择已关闭";
                if (panel != null) { panel.Refresh(this); panel.SetSelecting(value); }
            }
        }

        public void Bind(SimulationLevelAdapter levelAdapter)
        {
            adapter = levelAdapter != null ? levelAdapter : throw new ArgumentNullException(nameof(levelAdapter));
        }

        public void SetAutoStep(bool value) => autoStep = value;
        public void SetPointer(SimulationGridPointer value) => pointer = value;
        public void SetBrickEditor(SimulationBrickEditor value) => brickEditor = value;

        private void Awake()
        {
            if (brickEditor == null) brickEditor = FindFirstObjectByType<SimulationBrickEditor>();
            if (pointer == null) pointer = FindFirstObjectByType<SimulationGridPointer>();
            if (board == null) board = FindFirstObjectByType<SimulationBoardSession>();
        }

        private void PublishSpawns()
        {
            if (view != null) view.SetSpawns(spawns);
            if (panel != null) panel.Refresh(this);
        }

        public bool AddSpawn(GridPoint cell)
        {
            if (Running) return false;
            LevelState level = CurrentLevel();
            if (level == null) { LastMessage = "还没有确认的关卡，等识别面板出结果后再选入口。"; return false; }
            if (!level.Definition.Contains(cell.X, cell.Y)) { LastMessage = "点击位置不在棋盘内。"; return false; }
            if (level.At(cell.X, cell.Y).Occupied) { LastMessage = "入口必须是空格（这个格子上有积木）。"; return false; }
            foreach (GridPoint existing in spawns)
                if (existing.Equals(cell)) { LastMessage = "该入口已存在。"; return false; }
            spawns.Add(cell);
            LastMessage = "已选 " + spawns.Count + " 个入口：" + DescribeSpawns();
            PublishSpawns();
            return true;
        }

        private string DescribeSpawns()
        {
            var text = new System.Text.StringBuilder();
            foreach (GridPoint spawn in spawns)
            {
                if (text.Length > 0) text.Append(' ');
                text.Append('(').Append(spawn.X).Append(',').Append(spawn.Y).Append(')');
            }
            return text.ToString();
        }

        public void ClearSpawns()
        {
            if (Running) { LastMessage = "战斗中不能更改出生点。"; return; }
            spawns.Clear();
            LastMessage = "已清空入口。";
            PublishSpawns();
        }

        public bool Build()
        {
            // A new round always replans from the current confirmed layout and the current entrances.
            Level = null;
            LevelState level = CurrentLevel();
            if (level == null) { LastMessage = "还没有确认的关卡。"; return false; }
            if (!LayoutMatchesBoard(level)) { LastMessage = "等待识别确认最新布局，确认后会自动开始。"; return false; }
            int removed = spawns.RemoveAll(spawn => level.At(spawn.X, spawn.Y).Occupied);
            if (removed > 0)
                LastMessage = "有 " + removed + " 个入口已经被积木占用，已自动移除。";
            BattleLevel candidate = BattleLevel.Build(level, spawns);
            Level = candidate;
            if (!candidate.IsValid)
            {
                LastMessage = candidate.Issues[0].Message;
                return false;
            }
            LastMessage = "入口 " + candidate.Routes.Count + " 条通路，可以开始。";
            return true;
        }

        public bool StartBattle()
        {
            if (Running) return false;
            LevelState level = CurrentLevel();
            if (level == null) { LastMessage = "还没有确认的关卡，等识别面板出结果。"; return false; }
            if (!LayoutMatchesBoard(level))
            {
                // Never start on a layout that recognition has not confirmed yet; retry from Update.
                pendingStart = true;
                pendingSeconds = 0;
                LastMessage = "等待识别确认最新布局…（棋盘与最近一次确认不一致，确认后会自动开始）";
                return false;
            }
            pendingStart = false;
            if (!Build()) return false;
            IReadOnlyList<BattleIssue> issues = BattleValidation.Check(Level, WavePlan, TowerSettings);
            if (issues.Count != 0) { LastMessage = issues[0].Message; return false; }
            Battle = new BattleSimulation(Level, WavePlan, TowerSettings);
            SelectSpawns = false;
            LockEditing(true);
            if (view != null) { view.Build(Level); view.ClearSpawns(); }
            if (!Battle.StartWave()) { LastMessage = "无法开始第一波。"; return false; }
            LastMessage = "战斗开始：第 1 波";
            Publish();
            return true;
        }

        // BoardLayout versions count edits, confirmed snapshots count confirmations: compare contents instead.
        private bool LayoutMatchesBoard(LevelState level)
        {
            if (board == null) return true;
            BoardDefinition definition = level.Definition;
            for (int y = 0; y < definition.Height; y++)
                for (int x = 0; x < definition.Width; x++)
                {
                    BoardLayout.Brick brick = board.At(x, y);
                    LevelCell cell = level.At(x, y);
                    if (cell.Occupied != (brick != null)) return false;
                    if (brick != null && cell.ColorId != brick.ColorId) return false;
                }
            return true;
        }

        // The lock covers pause and the wait between waves; only finishing or retrying lifts it.
        private void LockEditing(bool value)
        {
            if (editingLocked == value) return;
            editingLocked = value;
            if (brickEditor == null) brickEditor = FindFirstObjectByType<SimulationBrickEditor>();
            if (brickEditor != null) brickEditor.Locked = value;
        }

        public bool StartNextWave()
        {
            if (Battle == null) return false;
            if (!Battle.StartWave()) { LastMessage = "现在不能开下一波。"; return false; }
            LastMessage = "第 " + Battle.State.Wave + " 波";
            Publish();
            return true;
        }

        public void SetPaused(bool paused)
        {
            if (Battle == null) return;
            Battle.SetPaused(paused);
            LastMessage = paused ? "已暂停" : "继续";
            Publish();
        }

        public bool Restart()
        {
            if (Battle == null) return false;
            Battle.Restart();
            Battle = null;
            lastState = null;
            Level = null;
            pendingStart = false;
            LockEditing(false);
            if (view != null) { view.ClearEnemies(); view.ClearRoutes(); }
            LastMessage = "已结束本局：入口仍在，可改积木后再开始（会重新规划路线）";
            PublishSpawns();
            Publish();
            return true;
        }

        public void StepManual(float seconds)
        {
            if (Battle == null) return;
            Battle.Step(seconds);
            Publish();
        }

        private LevelState CurrentLevel()
        {
            if (adapter == null || adapter.Feedback == null) return null;
            return adapter.Feedback.Level;
        }

        private void Update()
        {
            if (pendingStart && !Running)
            {
                pendingSeconds += Time.unscaledDeltaTime;
                LevelState level = CurrentLevel();
                if (level != null && LayoutMatchesBoard(level)) StartBattle();
                else if (pendingSeconds > 8f)
                {
                    pendingStart = false;
                    LastMessage = "识别仍未确认最新布局，请检查识别画面后再点开始。";
                    if (panel != null) panel.Refresh(this);
                }
            }
            if (SelectSpawns && !Running && Input.GetMouseButtonDown(0))
            {
                if (pointer == null) { LastMessage = "场景里没有棋盘指针组件，无法点选入口。"; }
                else if (pointer.TryPickCell(Input.mousePosition, out Vector2Int cell, out _))
                    AddSpawn(new GridPoint(cell.x, cell.y));
                else LastMessage = "这里不是棋盘空格：请点棋盘上的空位（有积木的格子不能当入口）。";
            }
            if (!autoStep || Battle == null) return;
            Battle.Step(Time.deltaTime);
            Publish();
        }

        private void Publish()
        {
            lastState = Battle != null ? Battle.State : null;
            if (view != null && Battle != null) view.Render(Battle.State, Level);
            if (panel != null) panel.Refresh(this);
            if (Battle == null) return;
            if (Battle.State.Phase == BattlePhase.Victory) LastMessage = "胜利：核心守住 " + Battle.State.CoreHealth;
            else if (Battle.State.Phase == BattlePhase.Defeat) LastMessage = "失败：核心被击破";
            if (Battle.State.Finished) LockEditing(false);
        }
    }
}
