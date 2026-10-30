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

        // Opens the simulation scene from the menu or from a command line launch without entering play mode.
        [MenuItem("Brick2Life/Simulation/Open simulation scene")]
        public static void OpenSimulationScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + SceneName + ".unity", OpenSceneMode.Single);
            Debug.Log("已打开场景：" + SceneName);
        }

        // Batch-friendly wrapper: installs, saves the scene and assets, and logs a single completion line.
        [MenuItem("Brick2Life/Simulation/Install feedback and save scene")]
        public static void InstallAndSave()
        {
            Install();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("任务 C 反馈已安装并保存场景：" + SceneName);
        }

        [MenuItem("Brick2Life/Simulation/Install feedback")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出 Play Mode 后再配置任务 C 反馈。");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName)
            {
                // Opening the scene directly keeps the menu usable from a fresh editor or -executeMethod.
                string path = "Assets/Scenes/" + SceneName + ".unity";
                if (!System.IO.File.Exists(System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scenes", SceneName + ".unity")))
                    throw new InvalidOperationException("请先打开 " + SceneName + " 场景。");
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
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
            EnsureLayer(SimulationRoundMarkers.LayerIndex, "Brick2LifeObjectives");

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
            EnsureBattleRig(root, adapter);
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

        // Battle rig: controller and view on a child object, the parameter panel on its own canvas object.
        private static void EnsureBattleRig(GameObject root, SimulationLevelAdapter adapter)
        {
            Transform battleRoot = EnsureChild(root.transform, "Battle");
            SimulationBattleView view = EnsureComponent<SimulationBattleView>(battleRoot.gameObject);
            Material enemyMaterial = EnsureMaterial("Battle_Enemy.mat", Shader.Find("Standard"), new Color(0.85f, 0.25f, 0.2f));
            Material routeMaterial = EnsureMaterial("Battle_Route.mat", Shader.Find("Sprites/Default"), new Color(0.2f, 0.9f, 1f, 0.9f));
            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("root").objectReferenceValue = battleRoot;
            viewObject.FindProperty("enemyMaterial").objectReferenceValue = enemyMaterial;
            viewObject.FindProperty("routeMaterial").objectReferenceValue = routeMaterial;
            viewObject.ApplyModifiedProperties();

            SimulationBattleController controller = EnsureComponent<SimulationBattleController>(battleRoot.gameObject);
            SimulationBattleOverlay overlay = EnsureComponent<SimulationBattleOverlay>(battleRoot.gameObject);
            var overlayObject = new SerializedObject(overlay);
            overlayObject.FindProperty("controller").objectReferenceValue = controller;
            overlayObject.FindProperty("projector").objectReferenceValue = FindFirst<ProjectionSimulator>();
            overlayObject.FindProperty("root").objectReferenceValue = battleRoot;
            overlayObject.ApplyModifiedProperties();
            GameObject panelObject = GameObject.Find("BattlePanelRoot");
            if (panelObject == null)
            {
                panelObject = new GameObject("BattlePanelRoot");
                Undo.RegisterCreatedObjectUndo(panelObject, "Install battle panel");
                panelObject.transform.SetParent(root.transform, false);
            }
            SimulationBattlePanel panel = EnsureComponent<SimulationBattlePanel>(panelObject);
            var controllerObject = new SerializedObject(controller);
            controllerObject.FindProperty("adapter").objectReferenceValue = adapter;
            controllerObject.FindProperty("generateObjectives").boolValue = true;
            controllerObject.FindProperty("view").objectReferenceValue = view;
            controllerObject.FindProperty("panel").objectReferenceValue = panel;
            controllerObject.FindProperty("pointer").objectReferenceValue = FindFirst<SimulationGridPointer>();
            controllerObject.ApplyModifiedProperties();
            var panelSerialized = new SerializedObject(panel);
            panelSerialized.FindProperty("controller").objectReferenceValue = controller;
            panelSerialized.ApplyModifiedProperties();
            Debug.Log("战斗装置已安装：Battle（控制器 + 视图）与 BattlePanelRoot（参数面板）。", root);
        }

        private static void ConfigureView(SimulationLevelView view, Transform levelRoot, Camera topCamera,            Material core, Material wall, Material tower)
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
            bool hasCore = false;
            for (int i = 0; i < paletteColors.Length; i++)
            {
                GameRole role = RoleFor(paletteColors[i]);
                if (role == GameRole.Core) hasCore = true;
                SerializedProperty entry = rules.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("colorId").stringValue = paletteColors[i];
                entry.FindPropertyRelative("role").enumValueIndex = (int)role;
            }
            // A level needs exactly one core; if the palette has no known core color, promote the first entry.
            if (!hasCore && paletteColors.Length > 0)
                rules.GetArrayElementAtIndex(0).FindPropertyRelative("role").enumValueIndex = (int)GameRole.Core;
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

        // Default semantics follow the task book example: blue is the core, red is a tower, green/yellow are walls.
        private static GameRole RoleFor(string colorId)
        {
            switch ((colorId ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "blue": return GameRole.Core;
                case "red": return GameRole.Tower;
                case "green": return GameRole.Wall;
                case "yellow": return GameRole.Wall;
                default: return GameRole.Wall;
            }
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
                mask &= ~(1 << SimulationRoundMarkers.LayerIndex);
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
