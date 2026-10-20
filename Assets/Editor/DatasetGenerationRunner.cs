using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Brick2Life.Application;
using Brick2Life.Core;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityApplication = UnityEngine.Application;

namespace Brick2Life.Editor
{
    // Entry point for the isolated rendering project, never the interactive project.
    public static class DatasetGenerationRunner
    {
        [Serializable] private sealed class CaptureMetadata
        {
            public int schemaVersion, layoutVersion;
            public string cameraConfigurationId, lightingConfigurationId;
        }
        private static readonly string[] Presets = { "balanced-standard", "balanced-dim", "balanced-bright", "left-heavy", "right-heavy" };
        private static DatasetRequest request;
        private static DatasetManifest manifest;
        private static DatasetPlan plan;
        private static SimulationBoardSession board;
        private static SimulationCameraCapture capture;
        private static SimulationLightingSettings lighting;
        private static int index, readyFrame;
        private static bool prepared, finished, enteredPlay;
        private static string runtimeError;
        private static Stopwatch elapsed;

        public static void Run()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int arg = Array.IndexOf(args, "-datasetRequest");
                if (arg < 0 || arg + 1 >= args.Length) throw new ArgumentException("Missing -datasetRequest.");
                string requestPath = Path.GetFullPath(args[arg + 1]);
                request = DatasetJobFiles.Read<DatasetRequest>(requestPath);
                string project = Path.GetFullPath(Path.Combine(UnityApplication.dataPath, ".."));
                string marker = Path.Combine(project, ".brick2life-dataset-workspace");
                if (!UnityApplication.isBatchMode || !string.Equals(project, Path.GetFullPath(request.workDirectory), StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(marker) || File.ReadAllText(marker).Trim() != requestPath)
                    throw new InvalidOperationException("Dataset rendering must run in the isolated project created by GenerateDataset.ps1.");
                if (request.schemaVersion != 1) throw new ArgumentException("Unsupported dataset request version.");
                DatasetPlan.Validate(request.layoutCount, (DatasetLayoutFamilies)request.families, request.lightingCount);
                if (File.Exists(Path.Combine(request.outputDirectory, "manifest.json"))) throw new IOException("This run already has a manifest. Start a new run.");
                manifest = new DatasetManifest
                {
                    generatorVersion = DatasetPlan.GeneratorVersion, seed = request.seed,
                    selectedFamilies = request.families, plannedImages = request.layoutCount * request.lightingCount,
                    lightingCount = request.lightingCount, startedAtUtc = DateTime.UtcNow.ToString("O")
                };
                SaveManifest();
                if (Cancelled()) { Finish("cancelled"); return; }
                elapsed = Stopwatch.StartNew();
                EditorSceneManager.OpenScene("Assets/Scenes/Brick2LifeSimulation.unity", OpenSceneMode.Single);
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
                UnityApplication.logMessageReceived += OnLog;
                EditorApplication.playModeStateChanged += OnPlayMode;
                EditorApplication.update += Tick;
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Finish("failed", error.ToString()); }
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            // Editor services can log unrelated startup errors (e.g. Search indexing in batch mode).
            // Component failures from our runtime invalidate capture; other diagnostics remain in editor.log.
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                && trace.IndexOf("Brick2Life.Simulation.", StringComparison.Ordinal) >= 0)
                runtimeError = message + "\n" + trace;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) { enteredPlay = true; readyFrame = Time.frameCount + 5; }
            if (state == PlayModeStateChange.ExitingPlayMode && !finished) Finish("failed", "Play mode stopped before generation completed.");
        }

        private static bool Cancelled() => File.Exists(Path.Combine(request.outputDirectory, "cancel.request"));
        private static void SaveManifest() => DatasetJobFiles.Write(Path.Combine(request.outputDirectory, "manifest.json"), manifest);

        private static void Tick()
        {
            if (finished) return;
            try
            {
                if (Cancelled()) { Finish("cancelled"); return; }
                if (runtimeError != null) throw new InvalidOperationException(runtimeError);
                if (elapsed.Elapsed.TotalMinutes > 10) throw new TimeoutException("Dataset generation exceeded ten minutes. Inspect editor.log.");
                if (!enteredPlay || !EditorApplication.isPlaying || EditorApplication.isPaused || Time.frameCount < readyFrame) return;
                if (plan == null) InitializePlan();
                if (index >= plan.ImageCount) { Finish("completed"); return; }
                DatasetLayout item = plan.Layouts[index / plan.LightingCount];
                int condition = index % plan.LightingCount;
                if (!prepared)
                {
                    if (condition == 0) board.ReplaceLayout(item.Snapshot);
                    ApplyLighting(condition);
                    // Let destroyed old cubes and shadow buffers settle before rendering the next RGB.
                    prepared = true; readyFrame = Time.frameCount + 3;
                    return;
                }
                SimulationCapture frame = capture.CaptureFrame();
                if (DatasetPlan.ContentId(frame.GroundTruth) != item.GroupId) throw new InvalidOperationException("Captured board differs from the planned layout.");
                var metadata = JsonUtility.FromJson<CaptureMetadata>(frame.GroundTruthJson);
                if (metadata.schemaVersion != 3) throw new InvalidOperationException("Unsupported capture schema.");
                string image = SimulationCaptureWriter.Save(Path.Combine(request.outputDirectory, "frames"), frame);
                string truth = Path.ChangeExtension(image, ".json");
                manifest.samples.Add(new DatasetSampleRecord
                {
                    sampleIndex = index, layoutIndex = index / plan.LightingCount, frameId = frame.FrameId,
                    layoutGroupId = item.GroupId, family = item.Family.ToString(), lightingPreset = Presets[condition],
                    occupiedCells = item.Snapshot.Cells.Count(cell => cell.Occupancy == Occupancy.Occupied),
                    width = frame.ImageWidth, height = frame.ImageHeight, layoutVersion = metadata.layoutVersion,
                    image = "frames/" + Path.GetFileName(image), groundTruth = "frames/" + Path.GetFileName(truth),
                    imageSha256 = HashFile(image), groundTruthSha256 = HashFile(truth),
                    cameraConfigurationId = metadata.cameraConfigurationId, lightingConfigurationId = metadata.lightingConfigurationId
                });
                index++; manifest.completedImages = index; SaveManifest(); prepared = false;
            }
            catch (Exception error) { Finish("failed", error.ToString()); }
        }

        private static void InitializePlan()
        {
            board = UnityEngine.Object.FindFirstObjectByType<SimulationBoardSession>();
            capture = UnityEngine.Object.FindFirstObjectByType<SimulationCameraCapture>();
            lighting = UnityEngine.Object.FindFirstObjectByType<SimulationLightingSettings>();
            if (board == null || !board.IsInitialized || capture == null || lighting == null)
                throw new InvalidOperationException("The saved simulation scene requires a board session, capture and configured lighting.");
            SimulationBrickEditor editor = UnityEngine.Object.FindFirstObjectByType<SimulationBrickEditor>();
            if (editor != null) editor.enabled = false;
            plan = new DatasetPlan(board.Definition, request.layoutCount, request.seed, (DatasetLayoutFamilies)request.families, request.lightingCount);
            manifest.boardId = board.Definition.BoardId; manifest.definitionVersion = board.Definition.DefinitionVersion;
            manifest.palette = board.Definition.ColorIds.ToArray();
            for (int i = 0; i < plan.Layouts.Count; i++)
            {
                DatasetLayout item = plan.Layouts[i];
                manifest.layouts.Add(new DatasetLayoutRecord
                {
                    layoutIndex = i, layoutGroupId = item.GroupId, family = item.Family.ToString(),
                    occupiedCells = item.Snapshot.Cells.Count(cell => cell.Occupancy == Occupancy.Occupied)
                });
            }
            SaveManifest();
        }

        private static void ApplyLighting(int condition)
        {
            lighting.SetLayout((int)SimulationLightingSettings.Layout.Dual);
            lighting.SetBrightness((int)(condition == 1 ? SimulationLightingSettings.Brightness.Dim
                : condition == 2 ? SimulationLightingSettings.Brightness.Bright : SimulationLightingSettings.Brightness.Standard));
            lighting.SetLeftLightShare(condition == 3 ? 0.8f : condition == 4 ? 0.2f : 0.5f);
            lighting.SetAmbientMultiplier(condition == 1 || condition >= 3 ? 0.5f : condition == 2 ? 1.5f : 1);
            if (!lighting.ShadowsEnabled) lighting.ToggleShadows();
        }

        private static string HashFile(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static void Finish(string status, string error = null)
        {
            if (finished) return;
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            UnityApplication.logMessageReceived -= OnLog;
            try
            {
                if (manifest != null)
                {
                    manifest.status = status; manifest.error = error; manifest.finishedAtUtc = DateTime.UtcNow.ToString("O");
                    SaveManifest();
                }
                else if (request != null)
                    DatasetJobFiles.Write(Path.Combine(request.outputDirectory, "failure.json"), new DatasetLauncherState { status = "failed", error = error });
            }
            catch (Exception writeError) { UnityEngine.Debug.LogException(writeError); status = "failed"; }
            UnityEngine.Debug.Log("Dataset generation " + status + ": " + (error ?? request?.outputDirectory));
            EditorApplication.Exit(status == "failed" ? 1 : 0);
        }
    }
}
