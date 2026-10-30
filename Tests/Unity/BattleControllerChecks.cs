using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Brick2Life.Application;
using Brick2Life.Battle;
using Brick2Life.Core;
using Brick2Life.Feedback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Runs the battle panel's actual controller entry points with confirmed observation fixtures.
public static class BattleControllerChecks
{
    [Serializable] private sealed class Result
    {
        public bool passed;
        public int assertions;
        public List<string> failures = new List<string>();
    }

    private static Result result;

    public static void Run()
    {
        result = new Result();
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RunCase("restart uses new core and entrance", RestartUsesLatestInputs);
            RunCase("rebuild uses new core", BuildUsesLatestCore);
            RunCase("start uses changed entrance", StartUsesLatestEntrance);
            RunCase("cleared entrances cannot start", ClearedEntrancesPreventStart);
            RunCase("entrance uses new occupancy", EntranceUsesLatestOccupancy);
            RunCase("finished battle uses new inputs", FinishedBattleUsesLatestInputs);
            RunCase("invalid new layout cannot reuse old map", InvalidNewLayoutCannotStart);
            RunCase("new walls affect the next route", NewWallsAffectNextRoute);
            RunCase("running and paused battle accept confirmed layouts", RunningBattleUpdatesConfirmedLayout);
            RunCase("next wave uses updated layout", NextWaveUsesUpdatedLayout);
        }
        catch (Exception error) { result.failures.Add(error.ToString()); }
        result.passed = result.failures.Count == 0;
        string output = Path.Combine(UnityEngine.Application.dataPath, "..", "verification.json");
        File.WriteAllText(output, JsonUtility.ToJson(result, true), new UTF8Encoding(false));
        Debug.Log("BATTLE_CONTROLLER_CHECKS_" + (result.passed ? "PASSED" : "FAILED") + ": " +
            result.assertions + " assertions; " + result.failures.Count + " failures.");
        EditorApplication.Exit(result.passed ? 0 : 1);
    }

    private static void RunCase(string name, Action check)
    {
        try { check(); }
        catch (Exception error) { result.failures.Add(name + ": " + error); }
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

    private static void CheckRoute(Fixture fixture, GridPoint spawn, GridPoint core, string context)
    {
        BattleLevel level = fixture.Controller.Level;
        Require(level != null && level.Routes.Count == 1, context + ": expected one route.");
        BattleRoute route = level.Routes[0];
        var expectedSpawn = new GridPoint(spawn.X * 2, spawn.Y * 2);
        Check(route.Spawn.Equals(expectedSpawn), context + ": starts at " + route.Spawn + ", expected " + expectedSpawn + ".");
        Check(route.TargetCoreId == "brick-" + core.X + "-" + core.Y,
            context + ": targets " + route.TargetCoreId + ", expected brick-" + core.X + "-" + core.Y + ".");
        GridPoint end = route.Route[route.Route.Count - 1];
        Check(end.X / 2 == core.X && end.Y / 2 == core.Y, context + ": ends in the previous core cell.");
    }

    private static void RestartUsesLatestInputs()
    {
        using (var fixture = new Fixture())
        {
            fixture.StartInitial();
            Require(fixture.Controller.Restart(), "Restart failed.");
            Check(fixture.Controller.Level == null, "Restart retained the previous battle map.");
            fixture.Confirm(new GridPoint(2, 7));
            fixture.Controller.ClearSpawns();
            Require(fixture.Controller.AddSpawn(new GridPoint(8, 0)), "New entrance rejected.");
            Require(fixture.Controller.StartBattle(), "New battle failed to start.");
            CheckRoute(fixture, new GridPoint(8, 0), new GridPoint(2, 7), "After restart");
        }
    }

    private static void BuildUsesLatestCore()
    {
        using (var fixture = new Fixture())
        {
            fixture.Confirm(new GridPoint(9, 9));
            Require(fixture.Controller.AddSpawn(new GridPoint(0, 0)), "Initial entrance rejected.");
            Require(fixture.Controller.Build(), "Initial build failed.");
            fixture.Confirm(new GridPoint(2, 7));
            Require(fixture.Controller.Build(), "Rebuild failed.");
            CheckRoute(fixture, new GridPoint(0, 0), new GridPoint(2, 7), "Rebuild");
        }
    }

    private static void StartUsesLatestEntrance()
    {
        using (var fixture = new Fixture())
        {
            fixture.Confirm(new GridPoint(9, 9));
            Require(fixture.Controller.AddSpawn(new GridPoint(0, 0)), "Initial entrance rejected.");
            Require(fixture.Controller.Build(), "Initial build failed.");
            fixture.Controller.ClearSpawns();
            Require(fixture.Controller.AddSpawn(new GridPoint(8, 0)), "New entrance rejected.");
            Require(fixture.Controller.StartBattle(), "Start failed after changing entrance.");
            CheckRoute(fixture, new GridPoint(8, 0), new GridPoint(9, 9), "Changed entrance");
        }
    }

    private static void ClearedEntrancesPreventStart()
    {
        using (var fixture = new Fixture())
        {
            fixture.Confirm(new GridPoint(9, 9));
            Require(fixture.Controller.AddSpawn(new GridPoint(0, 0)), "Initial entrance rejected.");
            Require(fixture.Controller.Build(), "Initial build failed.");
            fixture.Controller.ClearSpawns();
            Check(!fixture.Controller.StartBattle(), "Clearing entrances still started the previous route.");
        }
    }

    private static void EntranceUsesLatestOccupancy()
    {
        using (var fixture = new Fixture())
        {
            fixture.Confirm(new GridPoint(9, 9));
            Require(fixture.Controller.AddSpawn(new GridPoint(0, 0)), "Initial entrance rejected.");
            Require(fixture.Controller.Build(), "Initial build failed.");
            fixture.Confirm(new GridPoint(2, 7));
            fixture.Controller.ClearSpawns();
            Check(!fixture.Controller.AddSpawn(new GridPoint(2, 7)), "New core was accepted as an empty entrance.");
            Check(fixture.Controller.AddSpawn(new GridPoint(9, 9)), "Old core is still treated as occupied.");
        }
    }

    private static void FinishedBattleUsesLatestInputs()
    {
        using (var fixture = new Fixture())
        {
            fixture.StartInitial();
            fixture.FinishWave();
            Require(fixture.Controller.State.Finished, "Initial battle did not finish.");
            fixture.Confirm(new GridPoint(2, 7));
            fixture.Controller.ClearSpawns();
            Require(fixture.Controller.AddSpawn(new GridPoint(8, 0)), "New entrance rejected after battle finished.");
            Require(fixture.Controller.StartBattle(), "Next battle failed to start.");
            CheckRoute(fixture, new GridPoint(8, 0), new GridPoint(2, 7), "After battle finished");
        }
    }

    private static void InvalidNewLayoutCannotStart()
    {
        using (var fixture = new Fixture())
        {
            fixture.StartInitial();
            Require(fixture.Controller.Restart(), "Restart failed.");
            fixture.Confirm(null);
            Check(!fixture.Controller.StartBattle(), "A new layout without a core reused the old playable layout.");
        }
    }

    private static void NewWallsAffectNextRoute()
    {
        using (var fixture = new Fixture())
        {
            fixture.StartInitial();
            GridPoint? wall = null;
            foreach (GridPoint step in fixture.Controller.Level.Routes[0].Route)
            {
                var physical = new GridPoint(step.X / 2, step.Y / 2);
                if (physical.Equals(new GridPoint(0, 0)) || physical.Equals(new GridPoint(9, 9))) continue;
                wall = physical;
                break;
            }
            Require(wall.HasValue, "Initial route has no cell suitable for the wall fixture.");
            Require(fixture.Controller.Restart(), "Restart failed.");
            // Put the new wall on the old route; the new map and route must incorporate it.
            fixture.Confirm(new GridPoint(9, 9), wall.Value);
            Require(fixture.Controller.StartBattle(), "Battle failed with a wall and an alternative route.");
            Check(fixture.Controller.Level.CellAt(wall.Value.X * 2, wall.Value.Y * 2).Blocked,
                "New wall was not included in the next battle map.");
            foreach (GridPoint step in fixture.Controller.Level.Routes[0].Route)
                Require(!fixture.Controller.Level.CellAt(step.X, step.Y).Blocked, "New route crosses a wall.");
        }
    }

    private static void RunningBattleUpdatesConfirmedLayout()
    {
        using (var fixture = new Fixture())
        {
            fixture.StartInitial();
            BattleLevel level = fixture.Controller.Level;
            BattleSimulation battle = fixture.Controller.Battle;
            fixture.Confirm(new GridPoint(2, 7));
            Check(!fixture.Controller.AddSpawn(new GridPoint(8, 0)), "Running battle accepted a new entrance.");
            Check(!fixture.Controller.Build(), "Running battle allowed its map to be rebuilt.");
            Check(ReferenceEquals(level, fixture.Controller.Level), "Rebuild replaced the running map.");
            Check(ReferenceEquals(battle, fixture.Controller.Battle), "Recognition replaced the running battle.");
            fixture.Controller.SetPaused(true);
            Check(!ReferenceEquals(level, fixture.Controller.Level), "Confirmed layout did not replace the live map.");
            Check(!fixture.Controller.StartBattle(), "Paused battle allowed a new battle.");
            Check(!fixture.Controller.Build(), "Paused battle allowed its map to be rebuilt.");
            fixture.Controller.ClearSpawns();
            Check(fixture.Controller.Spawns.Count == 1, "Paused battle allowed its entrances to be cleared.");
            CheckRoute(fixture, new GridPoint(0, 0), new GridPoint(2, 7), "During live battle");
        }
    }

    private static void NextWaveUsesUpdatedLayout()
    {
        using (var fixture = new Fixture())
        {
            fixture.Controller.WavePlan = new WavePlan(2, 1, 1, 10, 1, 0.1);
            fixture.StartInitial();
            fixture.FinishWave();
            Require(fixture.Controller.State.Phase == BattlePhase.AwaitingNextWave, "Did not reach the wave interval.");
            BattleLevel level = fixture.Controller.Level;
            fixture.Confirm(new GridPoint(2, 7));
            Check(!fixture.Controller.Build(), "Wave interval allowed the map to be rebuilt.");
            Check(!fixture.Controller.AddSpawn(new GridPoint(8, 0)), "Wave interval allowed a new entrance.");
            Require(fixture.Controller.StartNextWave(), "Next wave failed to start.");
            Check(!ReferenceEquals(level, fixture.Controller.Level), "Next wave ignored the latest confirmed map.");
            CheckRoute(fixture, new GridPoint(0, 0), new GridPoint(2, 7), "Next wave");
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root;
        private readonly BoardDefinition definition = new BoardDefinition(colorIds: new[] { "red", "green", "blue" });
        private readonly BoardSyncSession sync;
        private readonly SimulationLevelAdapter adapter;
        private int sequence;
        public SimulationBattleController Controller { get; }

        public Fixture()
        {
            root = new GameObject("BattleControllerCheck");
            root.SetActive(false);
            adapter = root.AddComponent<SimulationLevelAdapter>();
            typeof(SimulationLevelAdapter).GetField("rules", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(adapter, new[]
                {
                    new SimulationLevelAdapter.RoleRule { colorId = "blue", role = GameRole.Core },
                    new SimulationLevelAdapter.RoleRule { colorId = "red", role = GameRole.Tower },
                    new SimulationLevelAdapter.RoleRule { colorId = "green", role = GameRole.Wall }
                });
            sync = new BoardSyncSession(definition, "battle-controller-check");
            adapter.Bind(sync);
            Controller = root.AddComponent<SimulationBattleController>();
            Controller.Bind(adapter);
            Controller.SetAutoStep(false);
            Controller.WavePlan = new WavePlan(1, 1, 1, 10, 100, 0.1);
            Controller.TowerSettings = new TowerSettings(5, 1, 0.1, 1);
        }

        public void Confirm(GridPoint? core, GridPoint? wall = null)
        {
            var cells = new ObservedCell[definition.CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = new ObservedCell(BoardCell.Empty);
            if (core.HasValue)
                cells[definition.Index(core.Value.X, core.Value.Y)] = new ObservedCell(new BoardCell(Occupancy.Occupied, "blue"));
            if (wall.HasValue)
                cells[definition.Index(wall.Value.X, wall.Value.Y)] = new ObservedCell(new BoardCell(Occupancy.Occupied, "green"));
            ObservationRequest request = sync.BeginFrame("fixture-" + ++sequence, DateTimeOffset.UnixEpoch.AddSeconds(sequence));
            var observation = new FrameObservation(definition, request.FrameId, request.SessionId, request.Sequence,
                request.CapturedAtUtc, request.CalibrationId, cells);
            BoardUpdate update = sync.Confirm(request, observation);
            Require(update.Outcome == UpdateOutcome.Baseline || update.Outcome == UpdateOutcome.Changed,
                "Confirmed observation fixture was not accepted.");
            adapter.Apply(update);
        }

        public void StartInitial()
        {
            Confirm(new GridPoint(9, 9));
            Require(Controller.AddSpawn(new GridPoint(0, 0)), "Initial entrance rejected.");
            Require(Controller.StartBattle(), "Initial battle failed to start.");
        }

        public void FinishWave()
        {
            for (int i = 0; i < 200 && !Controller.State.Finished && !Controller.State.CanStartWave; i++)
                Controller.StepManual(0.1f);
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
    }
}
