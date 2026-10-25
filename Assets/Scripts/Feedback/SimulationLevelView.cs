using System;
using System.Collections.Generic;
using Brick2Life.Application;
using Brick2Life.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityApplication = UnityEngine.Application;

namespace Brick2Life.Feedback
{
    /// <summary>Renders the gameplay world (core/wall/tower) of a confirmed level on its own layer.</summary>
    [DisallowMultipleComponent]
    public sealed class SimulationLevelView : MonoBehaviour
    {
        [Serializable] public struct RoleStyle
        {
            public GameRole role;
            public Material material;
            public PrimitiveType shape;
            [Min(0.1f)] public float height;
            [Range(0.1f, 1f)] public float footprint;
        }

        [Tooltip("数字游戏对象的父节点。缺失时使用本组件所在对象。")]
        [SerializeField] private Transform levelRoot;
        [Tooltip("核心、城墙、防御塔的形状、高度与材质；缺少角色样式时该格不生成对象。")]
        [SerializeField] private RoleStyle[] styles = Array.Empty<RoleStyle>();
        [Tooltip("数字对象所在层。顶置相机必须排除该层，否则虚拟建筑会被当成积木拍进图像。")]
        [SerializeField] private string gameLayerName = "Brick2LifeGame";
        [SerializeField] private int fallbackGameLayer = 30;
        [Tooltip("可选的顶置相机，仅用于检查分层是否会把游戏对象拍进实验图像。")]
        [SerializeField] private Camera overheadCamera;

        private readonly Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameRole> roles = new Dictionary<int, GameRole>();
        private BoardDefinition definition;

        public int RenderedCount => roles.Count;
        public int GameLayer { get; private set; }

        public RoleStyle[] Styles
        {
            get => styles;
            set => styles = value ?? Array.Empty<RoleStyle>();
        }

        private void Awake()
        {
            GameLayer = ResolveLayer(gameLayerName, fallbackGameLayer);
            if (levelRoot == null) levelRoot = transform;
            WarnIfCameraSeesGameLayer();
        }

        public void Render(LevelState level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (definition == null || !definition.IsCompatible(level.Definition))
            {
                Clear();
                definition = level.Definition;
            }
            if (levelRoot == null) levelRoot = transform;
            var desired = new Dictionary<int, GameRole>();
            for (int y = 0; y < level.Definition.Height; y++)
                for (int x = 0; x < level.Definition.Width; x++)
                {
                    LevelCell cell = level.At(x, y);
                    if (cell.Occupied && cell.Role != GameRole.None) desired[level.Definition.Index(x, y)] = cell.Role;
                }
            foreach (var pair in desired)
                if (!roles.TryGetValue(pair.Key, out GameRole current) || current != pair.Value)
                    Replace(pair.Key, pair.Value);
            var removed = new List<int>();
            foreach (var pair in roles) if (!desired.ContainsKey(pair.Key)) removed.Add(pair.Key);
            foreach (int index in removed) DestroyCell(index);
        }

        public void Clear()
        {
            foreach (var pair in objects)
                if (pair.Value != null) DestroyNow(pair.Value);
            objects.Clear(); roles.Clear();
        }

        public bool TryGetObject(int x, int y, out GameObject instance)
        {
            instance = null;
            return definition != null && definition.Contains(x, y) &&
                objects.TryGetValue(definition.Index(x, y), out instance);
        }

        public bool TryGetMaterial(GameRole role, out Material material)
        {
            material = null;
            foreach (RoleStyle style in styles)
                if (style.role == role && style.material != null) { material = style.material; return true; }
            return false;
        }

        private void Replace(int index, GameRole role)
        {
            DestroyCell(index);
            if (!TryGetStyle(role, out RoleStyle style))
            {
                Debug.LogWarning("关卡视图缺少角色样式，已跳过该格：" + role, this);
                return;
            }
            GameObject instance = GameObject.CreatePrimitive(style.shape);
            instance.name = "Game_" + role + "_" + index;
            instance.layer = GameLayer;
            instance.transform.SetParent(levelRoot, false);
            float height = Mathf.Max(0.1f, style.height);
            float footprint = Mathf.Clamp(style.footprint, 0.1f, 1f);
            // A Unity cylinder is two units tall; every other primitive is one unit tall.
            float verticalScale = style.shape == PrimitiveType.Cylinder ? height * 0.5f : height;
            instance.transform.localScale = new Vector3(footprint, verticalScale, footprint);
            instance.transform.localPosition = new Vector3(index % definition.Width + 0.5f, height * 0.5f,
                index / definition.Width + 0.5f);
            Renderer renderer = instance.GetComponent<Renderer>();
            renderer.sharedMaterial = style.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Gameplay objects must never intercept the brick editor's pick rays.
            Collider collider = instance.GetComponent<Collider>();
            if (collider != null) DestroyNow(collider);
            objects[index] = instance; roles[index] = role;
        }

        private bool TryGetStyle(GameRole role, out RoleStyle style)
        {
            foreach (RoleStyle candidate in styles)
                if (candidate.role == role && candidate.material != null) { style = candidate; return true; }
            style = default;
            return false;
        }

        private void DestroyCell(int index)
        {
            if (objects.TryGetValue(index, out GameObject instance) && instance != null) DestroyNow(instance);
            objects.Remove(index); roles.Remove(index);
        }

        private void WarnIfCameraSeesGameLayer()
        {
            if (overheadCamera == null) return;
            if ((overheadCamera.cullingMask & (1 << GameLayer)) != 0)
                Debug.LogWarning("顶置相机包含游戏对象层 " + gameLayerName + "（" + GameLayer +
                    "），虚拟建筑会被拍进实验图像；请在安装菜单或相机上排除该层。", this);
        }

        private static int ResolveLayer(string name, int fallback)
        {
            int layer = string.IsNullOrWhiteSpace(name) ? -1 : LayerMask.NameToLayer(name);
            if (layer >= 0) return layer;
            Debug.LogWarning("未找到层 " + name + "，改用 " + fallback + "；请在 Editor 中运行 Brick2Life/Simulation/Install feedback。");
            return Mathf.Clamp(fallback, 0, 31);
        }

        private static void DestroyNow(UnityEngine.Object target)
        {
            if (target == null) return;
            if (UnityApplication.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
    }
}
