using System;
using System.Collections.Generic;
using System.Linq;
using Brick2Life.Application;
using Brick2Life.Feedback;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Brick2Life.Editor
{
    /// <summary>Installs the task C feedback objects, materials, layers and camera masks into the simulation scene.</summary>
    public static class SimulationFeedbackSetup
    {
        private const string SceneName = "Brick2LifeSimulation";
        private const string RootName = "SimulationFeedback";
        private const string MaterialFolder = "Assets/Materials/Simulation";
        private const int GameLayerIndex = 30;
        private const int ProjectionLayerIndex = 31;
        private const string GameLayerName = "Brick2LifeGame";
        private const string ProjectionLayerName = "Brick2LifeProjection";

        [MenuItem("Brick2Life/Simulation/Install feedback")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出 Play Mode 后再配置任务 C 反馈。");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName)
                throw new InvalidOperationException("请先打开 " + SceneName + " 场景。");
            SimulationBoardSession board = FindFirst<SimulationBoardSession>();
            if (board == null)
                throw new InvalidOperationException("仿真场景缺少 SimulationBoardSession。");
            // The board session only initializes its palette in Awake, so edit mode must read the serialized field.
            string[] paletteColors = ReadPaletteColors(board);
            if (paletteColors.Length == 0)
                throw new InvalidOperationException("SimulationBoardSession 没有序列化的调色板，无法生成颜色→角色规则。");
            Camera topCamera = CameraByName("TopCamera");
            Camera workspaceCamera = CameraByName("WorkspaceCamera");

            EnsureLayer(GameLayerIndex, GameLayerName);
            EnsureLayer(ProjectionLayerIndex, ProjectionLayerName);

            Material coreMaterial = EnsureMaterial("Game_Core.mat", UnityEngine.Shader.Find("Standard"), new Color(0.35f, 0.72f, 1f));
            Material wallMaterial = EnsureMaterial("Game_Wall.mat", UnityEngine.Shader.Find("Standard"), new Color(0.78f, 0.82f, 0.88f));
            Material towerMaterial = EnsureMaterial("Game_Tower.mat", UnityEngine.Shader.Find("Standard"), new Color(1f, 0.64f, 0.22f));
            Material projectionMaterial = EnsureMaterial("ProjectionMark.mat", UnityEngine.Shader.Find("Sprites/Default"), Color.white);

            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Install feedback");
            }
            Transform levelRoot = EnsureChild(root.transform, "Level");
            Transform projectionRoot = EnsureChild(root.transform, "Projection");

            SimulationLevelView view = EnsureComponent<SimulationLevelView>(root);
            ConfigureView(view, levelRoot, topCamera, coreMaterial, wallMaterial, towerMaterial);

            ProjectionSimulator projector = EnsureComponent<ProjectionSimulator>(root);
            ConfigureProjection(projector, projectionRoot, topCamera, projectionMaterial);

            SimulationLevelAdapter adapter = EnsureComponent<SimulationLevelAdapter>(root);
            ConfigureAdapter(adapter, view, projector, paletteColors);

            SimulationFeedbackDemo demo = EnsureComponent<SimulationFeedbackDemo>(root);

            SimulationFeedbackBootstrap bootstrap = EnsureComponent<SimulationFeedbackBootstrap>(root);
            var bootstrapObject = new SerializedObject(bootstrap);
            bootstrapObject.FindProperty("board").objectReferenceValue = board;
            bootstrapObject.FindProperty("adapter").objectReferenceValue = adapter;
            bootstrapObject.FindProperty("demo").objectReferenceValue = demo;
            bootstrapObject.FindProperty("calibrationId").stringValue = "brick2life-simulation";
            bootstrapObject.ApplyModifiedProperties();

            ApplyCameraMasks(topCamera, workspaceCamera);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("任务 C 反馈已安装：" + RootName + "（关卡视图、投影、适配器、人工目标演示）；" +
                "顶置相机排除 " + GameLayerName + "、包含 " + ProjectionLayerName + "。", root);
        }

        [MenuItem("Brick2Life/Simulation/Verify feedback layering")]
        public static void VerifyLayering()
        {
            Camera topCamera = CameraByName("TopCamera");
            Camera workspaceCamera = CameraByName("WorkspaceCamera");
            int game = LayerMask.NameToLayer(GameLayerName);
            int projection = LayerMask.NameToLayer(ProjectionLayerName);
            if (game < 0 || projection < 0)
                throw new InvalidOperationException("缺少层 " + GameLayerName + " / " + ProjectionLayerName + "，请先安装反馈。");
            if (topCamera == null) throw new InvalidOperationException("场景缺少 TopCamera。");
            bool topExcludesGame = (topCamera.cullingMask & (1 << game)) == 0;
            bool topIncludesProjection = (topCamera.cullingMask & (1 << projection)) != 0;
            bool workspaceIncludesBoth = workspaceCamera == null ||
                ((workspaceCamera.cullingMask & (1 << game)) != 0 && (workspaceCamera.cullingMask & (1 << projection)) != 0);
            Debug.Log("任务 C 分层检查：顶置相机排除游戏层=" + topExcludesGame + "，包含投影层=" + topIncludesProjection +
                "，操作相机包含两层=" + workspaceIncludesBoth + "；顶置相机掩码=" + topCamera.cullingMask);
        }

        private static void ConfigureView(SimulationLevelView view, Transform levelRoot, Camera topCamera,
            Material core, Material wall, Material tower)
        {
            var serialized = new SerializedObject(view);
            serialized.FindProperty("levelRoot").objectReferenceValue = levelRoot;
            serialized.FindProperty("gameLayerName").stringValue = GameLayerName;
            serialized.FindProperty("fallbackGameLayer").intValue = GameLayerIndex;
            serialized.FindProperty("overheadCamera").objectReferenceValue = topCamera;
            SerializedProperty styles = serialized.FindProperty("styles");
            styles.arraySize = 3;
            SetStyle(styles.GetArrayElementAtIndex(0), GameRole.Core, core, PrimitiveType.Cube, 0.9f, 0.7f);
            SetStyle(styles.GetArrayElementAtIndex(1), GameRole.Wall, wall, PrimitiveType.Cube, 0.45f, 1f);
            SetStyle(styles.GetArrayElementAtIndex(2), GameRole.Tower, tower, PrimitiveType.Cylinder, 1.6f, 0.55f);
            serialized.ApplyModifiedProperties();
        }

        private static void SetStyle(SerializedProperty entry, GameRole role, Material material, PrimitiveType shape,
            float height, float footprint)
        {
            entry.FindPropertyRelative("role").enumValueIndex = (int)role;
            entry.FindPropertyRelative("material").objectReferenceValue = material;
            entry.FindPropertyRelative("shape").enumValueIndex = (int)shape;
            entry.FindPropertyRelative("height").floatValue = height;
            entry.FindPropertyRelative("footprint").floatValue = footprint;
        }

        private static void ConfigureProjection(ProjectionSimulator projector, Transform projectionRoot, Camera topCamera,
            Material material)
        {
            var serialized = new SerializedObject(projector);
            serialized.FindProperty("marksRoot").objectReferenceValue = projectionRoot;
            serialized.FindProperty("overheadCamera").objectReferenceValue = topCamera;
            serialized.FindProperty("markMaterial").objectReferenceValue = material;
            serialized.FindProperty("projectionLayerName").stringValue = ProjectionLayerName;
            serialized.FindProperty("fallbackProjectionLayer").intValue = ProjectionLayerIndex;
            serialized.FindProperty("brickTopHeight").floatValue = 1f;
            serialized.FindProperty("tableHeight").floatValue = 0f;
            serialized.FindProperty("surfaceOffset").floatValue = 0.006f;
            serialized.FindProperty("intensity").floatValue = 1f;
            serialized.FindProperty("visible").boolValue = true;
            serialized.ApplyModifiedProperties();
        }

        private static void ConfigureAdapter(SimulationLevelAdapter adapter, SimulationLevelView view,
            ProjectionSimulator projector, string[] paletteColors)
        {
            var serialized = new SerializedObject(adapter);
            serialized.FindProperty("levelView").objectReferenceValue = view;
            serialized.FindProperty("projection").objectReferenceValue = projector;
            SerializedProperty rules = serialized.FindProperty("rules");
            rules.arraySize = paletteColors.Length;
            for (int i = 0; i < paletteColors.Length; i++)
            {
                SerializedProperty entry = rules.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("colorId").stringValue = paletteColors[i];
                entry.FindPropertyRelative("role").enumValueIndex = (int)RoleFor(i);
            }
            serialized.ApplyModifiedProperties();
        }

        // The board session only builds its palette in Awake; the installer reads the serialized array instead.
        private static string[] ReadPaletteColors(SimulationBoardSession board)
        {
            var serialized = new SerializedObject(board);
            SerializedProperty palette = serialized.FindProperty("palette");
            if (palette == null || !palette.isArray) return Array.Empty<string>();
            var colors = new string[palette.arraySize];
            for (int i = 0; i < palette.arraySize; i++)
            {
                SerializedProperty color = palette.GetArrayElementAtIndex(i).FindPropertyRelative("colorId");
                colors[i] = color != null ? color.stringValue : string.Empty;
            }
            return colors.Where(color => !string.IsNullOrWhiteSpace(color)).ToArray();
        }

        // Deterministic default mapping: the first palette entry is the core, then wall, tower, wall, ...
        private static GameRole RoleFor(int index)
        {
            if (index == 0) return GameRole.Core;
            if (index == 2) return GameRole.Tower;
            return GameRole.Wall;
        }

        private static Camera CameraByName(string name)
        {
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (camera.name == name && camera.gameObject.scene == EditorSceneManager.GetActiveScene()) return camera;
            return null;
        }

        private static T FindFirst<T>() where T : Component =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item.gameObject.scene == EditorSceneManager.GetActiveScene());

        private static T EnsureComponent<T>(GameObject owner) where T : Component
        {
            T existing = owner.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(owner);
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create feedback child");
            child = created.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void ApplyCameraMasks(Camera topCamera, Camera workspaceCamera)
        {
            int game = LayerMask.NameToLayer(GameLayerName);
            int projection = LayerMask.NameToLayer(ProjectionLayerName);
            if (topCamera != null)
            {
                int mask = topCamera.cullingMask;
                if (game >= 0) mask &= ~(1 << game);
                if (projection >= 0) mask |= 1 << projection;
                topCamera.cullingMask = mask;
            }
            if (workspaceCamera != null)
            {
                int mask = workspaceCamera.cullingMask;
                if (game >= 0) mask |= 1 << game;
                if (projection >= 0) mask |= 1 << projection;
                workspaceCamera.cullingMask = mask;
            }
        }

        private static void EnsureLayer(int index, string name)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                throw new InvalidOperationException("找不到 ProjectSettings/TagManager.asset。");
            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || layers.arraySize <= index)
                throw new InvalidOperationException("TagManager 的层数组不足以容纳索引 " + index + "。");
            SerializedProperty slot = layers.GetArrayElementAtIndex(index);
            if (slot.stringValue == name) return;
            if (!string.IsNullOrEmpty(slot.stringValue))
                throw new InvalidOperationException("层 " + index + " 已被占用：" + slot.stringValue);
            slot.stringValue = name;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private static Material EnsureMaterial(string fileName, Shader shader, Color color)
        {
            string path = MaterialFolder + "/" + fileName;
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            if (shader == null) throw new InvalidOperationException("找不到创建 " + fileName + " 所需的 shader。");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
                AssetDatabase.CreateFolder("Assets/Materials", "Simulation");
            }
            var material = new Material(shader) { name = fileName.Replace(".mat", ""), color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
