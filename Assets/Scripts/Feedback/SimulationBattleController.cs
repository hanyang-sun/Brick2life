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
        [Tooltip("自动按帧推进战斗；关闭后由外部调用 StepManual 推进（用于确定性验证）。")]
        [SerializeField] private bool autoStep = true;

        private readonly List<GridPoint> spawns = new List<GridPoint>();
        private LevelState frozenLevel;
        private BattleState lastState;

        public WavePlan WavePlan { get; set; } = WavePlan.Trial;
        public TowerSettings TowerSettings { get; set; } = TowerSettings.Trial;
        public IReadOnlyList<GridPoint> Spawns => spawns.AsReadOnly();
        public BattleLevel Level { get; private set; }
        public BattleSimulation Battle { get; private set; }
        public BattleState State => Battle != null ? Battle.State : lastState;
        public bool Running => Battle != null && !Battle.State.Finished && Battle.State.Phase != BattlePhase.Ready;
        public string LastMessage { get; private set; } = "选择入口后开始战斗";
        public bool SelectSpawns { get; set; }
        public SimulationBattlePanel Panel => panel;
        public SimulationBattleView View => view;

        public void Bind(SimulationLevelAdapter levelAdapter)
        {
            adapter = levelAdapter != null ? levelAdapter : throw new ArgumentNullException(nameof(levelAdapter));
        }

        public void SetAutoStep(bool value) => autoStep = value;
        public void SetPointer(SimulationGridPointer value) => pointer = value;

        public bool AddSpawn(GridPoint cell)
        {
            if (Running) return false;
            LevelState level = CurrentLevel();
            if (level == null || !level.Definition.Contains(cell.X, cell.Y)) return false;
            if (level.At(cell.X, cell.Y).Occupied) { LastMessage = "入口必须是空格。"; return false; }
            foreach (GridPoint existing in spawns)
                if (existing.Equals(cell)) { LastMessage = "该入口已存在。"; return false; }
            spawns.Add(cell);
            LastMessage = "已选 " + spawns.Count + " 个入口。";
            return true;
        }

        public void ClearSpawns()
        {
            if (Running) return;
            spawns.Clear();
            LastMessage = "已清空入口。";
        }

        public bool Build()
        {
            LevelState level = CurrentLevel();
            if (level == null) { LastMessage = "还没有确认的关卡。"; return false; }
            BattleLevel candidate = BattleLevel.Build(level, spawns);
            Level = candidate;
            if (!candidate.IsValid)
            {
                LastMessage = candidate.Issues[0].Message;
                return false;
            }
            frozenLevel = level;
            LastMessage = "入口 " + candidate.Routes.Count + " 条通路，可以开始。";
            return true;
        }

        public bool StartBattle()
        {
            if (Running) return false;
            if (Level == null || !Level.IsValid) { if (!Build()) return false; }
            IReadOnlyList<BattleIssue> issues = BattleValidation.Check(Level, WavePlan, TowerSettings);
            if (issues.Count != 0) { LastMessage = issues[0].Message; return false; }
            Battle = new BattleSimulation(Level, WavePlan, TowerSettings);
            if (view != null) view.Build(Level);
            if (!Battle.StartWave()) { LastMessage = "无法开始第一波。"; return false; }
            LastMessage = "战斗开始：第 1 波";
            Publish();
            return true;
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
            if (view != null) view.ClearEnemies();
            LastMessage = "已重置：入口和参数可重新选择，点开始重打";
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
            if (frozenLevel != null) return frozenLevel;
            if (adapter == null || adapter.Feedback == null) return null;
            return adapter.Feedback.Level;
        }

        private void Update()
        {
            if (SelectSpawns && !Running && pointer != null && Input.GetMouseButtonDown(0))
            {
                if (pointer.TryPickCell(Input.mousePosition, out Vector2Int cell, out _))
                    AddSpawn(new GridPoint(cell.x, cell.y));
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
        }
    }
}
