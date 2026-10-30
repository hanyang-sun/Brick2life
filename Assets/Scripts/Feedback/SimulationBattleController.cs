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
    /// Runs a battle from confirmed RGB observations, updating live routes while preserving run state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationBattleController : MonoBehaviour
    {
        [SerializeField] private SimulationLevelAdapter adapter;
        [SerializeField] private SimulationBattleView view;
        [SerializeField] private SimulationBattlePanel panel;
        [SerializeField] private SimulationGridPointer pointer;
        [SerializeField] private SimulationBrickEditor brickEditor;
        [Tooltip("自动按帧推进战斗；关闭后由外部调用 StepManual 推进（用于确定性验证）。")]
        [SerializeField] private bool autoStep = true;

        private readonly List<GridPoint> spawns = new List<GridPoint>();
        private BattleState lastState;
        private bool selectSpawns;
        private GameObject visionCanvas;
        private LevelState observedBattleLayout;
        private SimulationVisionMonitor pendingVision;
        private BoardSyncSession pendingSession;
        private long pendingConfirmation;
        private float pendingSeconds;

        public WavePlan WavePlan { get; set; } = WavePlan.Trial;
        public TowerSettings TowerSettings { get; set; } = TowerSettings.Trial;
        public IReadOnlyList<GridPoint> Spawns => spawns.AsReadOnly();
        public BattleLevel Level { get; private set; }
        public BattleSimulation Battle { get; private set; }
        public BattleState State => Battle != null ? Battle.State : lastState;
        public IReadOnlyList<ShotEvent> LastShots { get; private set; } = Array.Empty<ShotEvent>();
        public long ShotSequence { get; private set; }
        public bool PendingStart => pendingVision != null;
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
                if (!SimulationEditAccess.CanEdit) return;
                if (value && Running) { LastMessage = "战斗中不能更换出生点。"; return; }
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
        }

        private void PublishSpawns()
        {
            if (view != null) view.SetSpawns(spawns);
            if (panel != null) panel.Refresh(this);
        }

        public bool AddSpawn(GridPoint cell)
        {
            if (Running || !SimulationEditAccess.CanEdit) return false;
            LevelState level = CurrentLevel();
            if (level == null) { LastMessage = "还没有确认的关卡，等识别面板出结果后再选入口。"; return false; }
            if (!level.Definition.Contains(cell.X, cell.Y)) { LastMessage = "点击位置不在棋盘内。"; return false; }
            if (level.At(cell.X, cell.Y).Occupied) { LastMessage = "入口必须是空格（这个格子上有积木）。"; return false; }
            foreach (GridPoint existing in spawns)
                if (existing.Equals(cell)) { LastMessage = "该入口已存在。"; return false; }
            spawns.Add(cell);
            Level = null;
            if (view != null) view.ClearRoutes();
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
            if (Running || !SimulationEditAccess.CanEdit) return;
            spawns.Clear();
            Level = null;
            if (view != null) view.ClearRoutes();
            LastMessage = "已清空入口。";
            PublishSpawns();
        }

        public bool Build()
        {
            if (Running) { LastMessage = "战斗中不能重新生成地图，请先重试结束本局。"; return false; }
            if (view != null) view.ClearRoutes();
            LevelState level = CurrentLevel();
            if (level == null) { Level = null; LastMessage = "还没有确认的关卡。"; return false; }
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
            if (Running || PendingStart) return false;
            var vision = FindFirstObjectByType<SimulationVisionMonitor>();
            if (vision != null && vision.isActiveAndEnabled && vision.Automatic && vision.Session != null)
            {
                pendingVision = vision;
                pendingSession = vision.Session;
                pendingConfirmation = vision.RequestFreshConfirmation();
                pendingSeconds = 0;
                LastMessage = "等待任务 B 从新采集的 RGB 确认布局，确认后自动开始…";
                if (panel != null) panel.Refresh(this);
                return false;
            }
            // Standalone/manual observation fixtures already supply an explicitly confirmed layout.
            return BeginBattle();
        }

        private bool BeginBattle()
        {
            // Every new battle starts with the latest confirmed layout and current entrances.
            if (!Build()) return false;
            IReadOnlyList<BattleIssue> issues = BattleValidation.Check(Level, WavePlan, TowerSettings);
            if (issues.Count != 0) { LastMessage = issues[0].Message; return false; }
            Battle = new BattleSimulation(Level, WavePlan, TowerSettings);
            observedBattleLayout = CurrentLevel();
            EndSpawnSelection();
            LastShots = Array.Empty<ShotEvent>();
            ShotSequence++;
            if (view != null) { view.Build(Level); view.ClearSpawns(); }
            if (!Battle.StartWave()) { LastMessage = "无法开始第一波。"; return false; }
            LastMessage = "战斗开始：第 1 波";
            Publish();
            return true;
        }

        public bool StartNextWave()
        {
            if (Battle == null) return false;
            SyncConfirmedLayout();
            if (!Battle.StartWave()) { LastMessage = "现在不能开下一波。"; return false; }
            LastMessage = "第 " + Battle.State.Wave + " 波";
            Publish();
            return true;
        }

        public void SetPaused(bool paused)
        {
            if (Battle == null) return;
            SyncConfirmedLayout();
            Battle.SetPaused(paused);
            LastMessage = paused ? "已暂停" : "继续";
            Publish();
        }

        public bool Restart()
        {
            if (PendingStart)
            {
                pendingVision = null;
                pendingSession = null;
                LastMessage = "已取消等待识别确认。";
                if (panel != null) panel.Refresh(this);
                return true;
            }
            if (Battle == null) return false;
            Battle.Restart();
            Battle = null;
            observedBattleLayout = null;
            Level = null;
            lastState = null;
            LastShots = Array.Empty<ShotEvent>();
            ShotSequence++;
            if (view != null) { view.ClearEnemies(); view.ClearRoutes(); }
            LastMessage = "已重置：入口保留，可调整积木和参数后开始";
            PublishSpawns();
            Publish();
            return true;
        }

        public void StepManual(float seconds)
        {
            if (Battle == null) return;
            SyncConfirmedLayout();
            LastShots = Battle.Step(seconds);
            ShotSequence++;
            Publish();
        }

        private LevelState CurrentLevel()
        {
            if (adapter == null || adapter.Feedback == null) return null;
            return adapter.Feedback.Level;
        }

        private void Update()
        {
            if (PendingStart)
            {
                pendingSeconds += Time.unscaledDeltaTime;
                if (!pendingVision.isActiveAndEnabled || !pendingVision.Automatic ||
                    pendingVision.Session != pendingSession || pendingSeconds > 8f)
                {
                    pendingVision = null;
                    pendingSession = null;
                    LastMessage = "未取得新的布局确认，请检查 RGB 识别或标定后重新开始。";
                    if (panel != null) panel.Refresh(this);
                }
                else if (pendingVision.HasConfirmed(pendingConfirmation))
                {
                    pendingVision = null;
                    pendingSession = null;
                    BeginBattle();
                }
            }
            if (SelectSpawns && !Running && Input.GetMouseButtonDown(0))
            {
                if (pointer == null) { LastMessage = "场景里没有棋盘指针组件，无法点选入口。"; }
                else if (pointer.TryPickCell(Input.mousePosition, out Vector2Int cell, out _))
                    AddSpawn(new GridPoint(cell.x, cell.y));
                else LastMessage = "这里不是棋盘空格：请点棋盘上的空位（有积木的格子不能当入口）。";
            }
            SyncConfirmedLayout();
            if (!autoStep || Battle == null) return;
            LastShots = Battle.Step(Time.deltaTime);
            ShotSequence++;
            Publish();
        }

        private void EndSpawnSelection()
        {
            // Ending a selection is internal cleanup, even when the display window currently owns focus.
            selectSpawns = false;
            if (brickEditor != null) brickEditor.enabled = true;
            if (visionCanvas != null) visionCanvas.SetActive(true);
            if (panel != null) panel.SetSelecting(false);
        }

        private void Publish()
        {
            lastState = Battle != null ? Battle.State : null;
            if (view != null && Battle != null) view.Render(Battle.State, Level);
            if (panel != null) panel.Refresh(this);
            if (Battle == null) return;
            if (Battle.State.Phase == BattlePhase.Victory) LastMessage = "胜利：核心守住 " + Battle.State.CoreHealth;
            else if (Battle.State.Phase == BattlePhase.Defeat) LastMessage = "失败：核心被击破";
        }

        private void SyncConfirmedLayout()
        {
            if (!Running) return;
            LevelState confirmed = CurrentLevel();
            if (ReferenceEquals(observedBattleLayout, confirmed)) return;
            observedBattleLayout = confirmed;
            BattleLevel candidate = confirmed != null ? BattleLevel.Build(confirmed, spawns) : null;
            LastShots = Array.Empty<ShotEvent>();
            ShotSequence++;
            if (Battle.TryUpdateLevel(candidate, out string reason))
            {
                Level = candidate;
                if (view != null) view.Build(Level);
                LastMessage = "布局已确认并更新：敌人从当前位置重新寻路，本局进度保留。";
            }
            else
            {
                if (view != null) view.ClearRoutes();
                LastMessage = "战斗等待修正布局：" + reason + "（仍可增删、移动积木）";
            }
            Publish();
        }
    }
}
