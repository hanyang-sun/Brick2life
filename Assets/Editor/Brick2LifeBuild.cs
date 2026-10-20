using System;
using System.IO;
using System.Linq;
using Brick2Life.Core;
using Brick2Life.Application;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityApplication = UnityEngine.Application;

namespace Brick2Life.Editor
{
    public static class Brick2LifeBuild
    {
        private const string ScenePath = "Assets/Scenes/Brick2LifeSimulation.unity";

        [MenuItem("Brick2Life/Build/Windows x64")]
        public static void Windows()
        {
            string root = Path.GetFullPath(Path.Combine(UnityApplication.dataPath, ".."));
            string target = Path.Combine(root, "Builds", "Brick2Life", "Brick2Life.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = target,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Brick2Life Player build failed: " + report.summary.result);
            Debug.Log("BRICK2LIFE_BUILD_PASSED: " + target);
        }

        public static void VerifyAndBuild()
        {
            CheckAssembly(typeof(BoardSnapshot), false);
            CheckAssembly(typeof(BoardSyncSession), false);
            CheckAssembly(typeof(SimulationBoardSession), true);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0)
                        throw new InvalidOperationException("Missing script: " + item.name);
            if (UnityEngine.Object.FindFirstObjectByType<SimulationBootstrap>() == null)
                throw new InvalidOperationException("Missing composition entry.");
            Windows();
            File.WriteAllText(Path.Combine(UnityApplication.dataPath, "..", "Builds", "architecture-verification.json"),
                "{\"passed\":true,\"scene\":\"" + ScenePath + "\",\"coreNoUnity\":true,\"applicationNoUnity\":true}");
        }

        private static void CheckAssembly(Type type, bool allowUnity)
        {
            foreach (var reference in type.Assembly.GetReferencedAssemblies())
                if (reference.Name.StartsWith("BrickDefense", StringComparison.Ordinal) ||
                    reference.Name.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                    (!allowUnity && reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Forbidden assembly dependency: " + type.Assembly.GetName().Name + " -> " + reference.Name);
        }
    }
}
