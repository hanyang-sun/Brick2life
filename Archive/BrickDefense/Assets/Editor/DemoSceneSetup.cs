using System.IO;
using BrickDefense.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrickDefense.Editor
{
    public static class DemoSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/BrickDefenseDemo.unity";

        [MenuItem("Brick Defense/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Brick Defense Demo");
            root.AddComponent<BrickDefenseDemo>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.Refresh();
            Debug.Log($"Created demo scene: {ScenePath}");
        }
    }
}
