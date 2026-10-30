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
        [Tooltip("系统生成固定核心与入口，玩家只摆墙和塔。")]
        [SerializeField] private bool generateObjectives;
        [SerializeField, Range(1, 12)] private int minimumSpawns = 1;
        [SerializeField, Range(1, 12)] private int maximumSpawns = 4;

        private readonly List<GridPoint> spawns = new List<GridPoint>();
        private BattleState lastState;
        private bool selectSpawns;
        private GameObject visionCanvas;
        private LevelState observedBattleLayout;
        private SimulationVisionMonitor pendingVision;
        private BoardSyncSession pendingSession;
        private long pendingConfirmation;
        private float pendingSeconds;
        private BattleLevel lastValidNavigation;
        private LevelState preparationObservation;
        private SimulationRoundMarkers roundMarkers;
        private double nextRouteRetryAt;
        public bool GeneratedObjectives => generateObjectives;
        public GeneratedRound Round { get; private set; }

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
                if (generateObjectives && value) { LastMessage = "出生点由系统固定生成，无需手选。"; return; }
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
            if (generateObjectives)
            {
                if (adapter == null) adapter = FindFirstObjectByType<SimulationLevelAdapter>();
                GenerateNewRound();
            }
        }

        public bool GenerateNewRound(int? seed = null, int? spawnCount = null)
        {
            if (!generateObjectives || Running || PendingStart || !SimulationEditAccess.CanEdit) return false;
            var board = brickEditor != null ? brickEditor.Board : FindFirstObjectByType<SimulationBoardSession>();
            if (board == null || !board.IsInitialized || board.IsEditingLocked || adapter == null || !adapter.IsBound) return false;
            if (!adapter.TryGetColorForRole(GameRole.Wall, out string wall) ||
                !adapter.TryGetColorForRole(GameRole.Tower, out string tower))
            { LastMessage = "请配置城墙和防御塔的颜色。"; return false; }
            int actualSeed = seed ?? unchecked(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
            int min = Mathf.Clamp(minimumSpawns, 1, 12), max = Mathf.Clamp(maximumSpawns, min, 12);
            int count = spawnCount ?? (min + (int)((uint)actualSeed % (uint)(max - min + 1)));
            GeneratedRound next = GeneratedRound.Generate(board.Definition, actualSeed, count);
            EndSpawnSelection();
            for (int i = board.Bricks.Count - 1; i >= 0; i--) board.TryDelete(board.Bricks[i].BrickId);
            var reserved = new List<GridPoint>(next.Spawns) { next.Core };
            board.SetDefenseEditing(reserved, wall, tower);
            if (brickEditor != null) brickEditor.RefreshPlacementRules();
            Round = next;
            adapter.SetGeneratedRound(next);
            spawns.Clear(); foreach (GridPoint spawn in next.Spawns) spawns.Add(spawn);
            Battle = null; lastState = null; observedBattleLayout = null; preparationObservation = null;
            nextRouteRetryAt = 0;
            LastShots = Array.Empty<ShotEvent>(); ShotSequence++;
            lastValidNavigation = BattleLevel.Build(next.CreateReferenceLevel(adapter.Feedback.Rules), spawns);
            Level = lastValidNavigation;
            if (view != null) { view.ClearEnemies(); view.Build(Level); view.ClearSpawns(); }
            roundMarkers = GetComponent<SimulationRoundMarkers>();
            if (roundMarkers == null) roundMarkers = gameObject.AddComponent<SimulationRoundMarkers>();
            roundMarkers.Render(next);
            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.name == "TopCamera") camera.cullingMask &= ~(1 << SimulationRoundMarkers.LayerIndex);
            var vision = FindFirstObjectByType<SimulationVisionMonitor>();
            if (vision != null && vision.Session != null) vision.ResetTracking();
            LastMessage = "关卡已生成：核心与 " + count + " 个入口固定；墙/塔不限，等待 RGB 确认。";
            if (panel != null) panel.Refresh(this);
            Debug.Log("Generated round: seed=" + actualSeed + ", core=" + next.Core + ", spawns=" + DescribeSpawns(), this);
            return true;
        }

        private void PublishSpawns()
        {
            if (view != null) view.SetSpawns(spawns);
            if (panel != null) panel.Refresh(this);
        }

        public bool AddSpawn(GridPoint cell)
        {
            if (generateObjectives || Running || !SimulationEditAccess.CanEdit) return false;
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
            if (generateObjectives || Running || !SimulationEditAccess.CanEdit) return;
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
            if (generateObjectives && !candidate.IsValid && level.IsPlayable && lastValidNavigation != null)
            {
                Level = lastValidNavigation;
                LastMessage = "布局堵路：将沿用上次有效路线。";
                return true;
            }
            Level = candidate;
            if (!candidate.IsValid)
            {
                LastMessage = candidate.Issues[0].Message;
                return false;
            }
            LastMessage = "入口 " + candidate.Routes.Count + " 条通路，可以开始。";
            if (generateObjectives) lastValidNavigation = candidate;
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
            Battle = new BattleSimulation(Level, WavePlan, TowerSettings, keepLastValidRoutes: generateObjectives);
            if (generateObjectives)
            {
                BattleLevel latest = BattleLevel.Build(CurrentLevel(), spawns);
                if (!Battle.TryUpdateLevel(latest, out string reason))
                { Battle = null; LastMessage = reason; return false; }
                Level = Battle.CurrentLevel;
            }
            observedBattleLayout = CurrentLevel();
            EndSpawnSelection();
            LastShots = Array.Empty<ShotEvent>();
            ShotSequence++;
            if (view != null) { view.Build(Level, Battle.ActiveRoutes); view.ClearSpawns(); }
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
            preparationObservation = null;
            nextRouteRetryAt = 0;
            Level = null;
            lastState = null;
            LastShots = Array.Empty<ShotEvent>();
            ShotSequence++;
            if (generateObjectives) Level = lastValidNavigation;
            if (view != null)
            {
                view.ClearEnemies();
                if (generateObjectives) view.Build(Level); else view.ClearRoutes();
            }
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
            SyncPreparationRoutes();
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
            if (!Battle.State.Finished && Battle.State.UsingPreviousRoutes) LastMessage = Battle.State.RouteWarning;
        }

        private void SyncConfirmedLayout()
        {
            if (!Running) return;
            LevelState confirmed = CurrentLevel();
            bool unchanged = ReferenceEquals(observedBattleLayout, confirmed);
            if (unchanged)
            {
                // A wall under an enemy can stop replanning while entrance routes remain valid.
                // Retry as the enemy moves out, without requiring another visual layout change.
                if (!Battle.State.UsingPreviousRoutes || !Battle.CurrentLevel.IsValid ||
                    (Battle.State.Enemies.Count > 0 && Battle.State.Elapsed < nextRouteRetryAt)) return;
            }
            nextRouteRetryAt = Battle.State.Elapsed + 0.25;
            observedBattleLayout = confirmed;
            BattleLevel candidate = unchanged ? Battle.CurrentLevel :
                confirmed != null ? BattleLevel.Build(confirmed, spawns) : null;
            LastShots = Array.Empty<ShotEvent>();
            ShotSequence++;
            if (Battle.TryUpdateLevel(candidate, out string reason))
            {
                Level = Battle.CurrentLevel;
                if (!Battle.State.UsingPreviousRoutes) lastValidNavigation = Level;
                if (view != null) view.Build(Level, Battle.ActiveRoutes);
                LastMessage = Battle.State.UsingPreviousRoutes ? Battle.State.RouteWarning :
                    "布局已确认并更新：敌人从当前位置重新寻路，本局进度保留。";
            }
            else
            {
                if (view != null) view.ClearRoutes();
                LastMessage = "战斗等待修正布局：" + reason + "（仍可增删、移动积木）";
            }
            Publish();
        }

        private void SyncPreparationRoutes()
        {
            if (!generateObjectives || Battle != null || PendingStart) return;
            LevelState confirmed = CurrentLevel();
            if (confirmed == null || ReferenceEquals(preparationObservation, confirmed)) return;
            preparationObservation = confirmed;
            BattleLevel candidate = BattleLevel.Build(confirmed, spawns);
            if (candidate.IsValid) lastValidNavigation = candidate;
            Level = lastValidNavigation;
            if (view != null) view.Build(Level);
            LastMessage = candidate.IsValid ? "准备就绪：墙/塔不限，可开始战斗。" :
                "布局堵路或冲突：" + candidate.Issues[0].Message + "；堵路时沿用上次有效路线。";
            if (panel != null) panel.Refresh(this);
        }
    }
}
