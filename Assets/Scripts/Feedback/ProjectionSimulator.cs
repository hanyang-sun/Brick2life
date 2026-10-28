using System;
using System.Collections.Generic;
using Brick2Life.Application;
using Brick2Life.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Virtual overhead projector. Each projection mark becomes a surface-aligned quad that is part of the scene,
    /// so the overhead camera captures the projected color instead of a UI overlay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectionSimulator : MonoBehaviour, IProjectionGate
    {
        [Tooltip("投影面片的父节点。缺失时使用本组件所在对象。")]
        [SerializeField] private Transform marksRoot;
        [Tooltip("可选的顶置相机；用于检查投影层是否真的会被拍进实验图像。")]
        [SerializeField] private Camera overheadCamera;
        [Tooltip("投影材质。应使用无光照、alpha 混合的材质（例如 Sprites/Default）。留空时在运行时回退创建。")]
        [SerializeField] private Material markMaterial;
        [Tooltip("投影面片所在层。该层必须被顶置相机包含，否则投影不会进入 RGB 图像。")]
        [SerializeField] private string projectionLayerName = "Brick2LifeProjection";
        [SerializeField] private int fallbackProjectionLayer = 31;
        [Tooltip("单位积木顶面高度（世界 Y）。积木中心在 0.5，顶面在 1。")]
        [SerializeField, Min(0f)] private float brickTopHeight = 1f;
        [Tooltip("桌面高度（世界 Y）。空格的格点与提示投在桌面上。")]
        [SerializeField, Min(0f)] private float tableHeight = 0f;
        [Tooltip("贴面偏移，避免与积木顶面 z-fighting。")]
        [SerializeField, Min(0f)] private float surfaceOffset = 0.006f;
        [Tooltip("投影强度，乘到标记 alpha 上；用于投影开/关与强弱对照。")]
        [SerializeField, Range(0f, 1f)] private float intensity = 1f;
        [SerializeField] private bool visible = true;

        private readonly List<GameObject> pool = new List<GameObject>();
        // Unity objects cannot be constructed in field initializers; created in Awake instead.
        private MaterialPropertyBlock properties;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private Material runtimeMaterial;

        public ProjectionPlan LastPlan { get; private set; }
        public int VisibleMarkCount { get; private set; }
        public bool IsVisible => visible;
        public float Intensity => intensity;
        public int ProjectionLayer { get; private set; }
        public Transform MarksRoot => marksRoot;

        // Capture adapter sees the projector through this gate; no cross-assembly reference is needed.
        bool IProjectionGate.IsProjectionVisible => visible;
        void IProjectionGate.SetProjectionVisible(bool value) => SetVisible(value);

        private void OnEnable() => ProjectionCaptureGate.Register(this);

        private void OnDisable() => ProjectionCaptureGate.Unregister(this);

        private void Awake()
        {
            ProjectionLayer = ResolveLayer(projectionLayerName, fallbackProjectionLayer);
            if (marksRoot == null) marksRoot = transform;
            properties = new MaterialPropertyBlock();
        }

        private void Start() => WarnIfCameraMissesProjection();

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }

        public void Render(ProjectionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            LastPlan = plan;
            if (marksRoot == null) marksRoot = transform;
            Material material = EnsureMaterial();
            if (material == null) { HideAll(); return; }
            int used = 0;
            foreach (ProjectionMark mark in plan.Marks)
            {
                GameObject quad = Obtain(used);
                Place(quad, mark, material);
                used++;
            }
            for (int i = used; i < pool.Count; i++) if (pool[i] != null) pool[i].SetActive(false);
            VisibleMarkCount = used;
            if (visible) ShowAll();
        }

        public void Clear()
        {
            foreach (GameObject quad in pool) if (quad != null) Destroy(quad);
            pool.Clear(); VisibleMarkCount = 0; LastPlan = null;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (visible) ShowAll(); else HideAll();
        }

        public void SetIntensity(float value)
        {
            intensity = Mathf.Clamp01(value);
            if (LastPlan != null) Render(LastPlan);
        }

        // World center of a projected mark; used by alignment checks and the guide's measured report.
        public bool TryGetMarkCenter(int index, out Vector3 center)
        {
            center = Vector3.zero;
            if (index < 0 || index >= VisibleMarkCount || pool.Count <= index || pool[index] == null) return false;
            center = pool[index].transform.position;
            return true;
        }

        // The center of a projected mark must be the cell center; zero by construction for this axis-aligned projector.
        public float MeasureAlignmentError(ProjectionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            float worst = 0f;
            int count = Mathf.Min(VisibleMarkCount, plan.Marks.Count);
            for (int i = 0; i < count; i++)
            {
                if (!TryGetMarkCenter(i, out Vector3 center)) continue;
                ProjectionMark mark = plan.Marks[i];
                worst = Mathf.Max(worst, new Vector2(center.x - mark.CenterX, center.z - mark.CenterY).magnitude);
            }
            return worst;
        }

        private void Place(GameObject quad, ProjectionMark mark, Material material)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            float surface = mark.Surface == ProjectionSurface.BrickTop ? brickTopHeight : tableHeight;
            quad.SetActive(visible);
            quad.transform.position = new Vector3(mark.CenterX, surface + surfaceOffset, mark.CenterY);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(mark.SizeX, mark.SizeY, 1f);
            ProjectionColor color = mark.Color;
            properties.SetColor(ColorId, new Color(color.Red, color.Green, color.Blue, color.Alpha * intensity));
            quad.GetComponent<Renderer>().SetPropertyBlock(properties);
        }

        private GameObject Obtain(int index)
        {
            while (pool.Count <= index) pool.Add(CreateQuad());
            GameObject quad = pool[index];
            if (quad == null)
            {
                quad = CreateQuad();
                pool[index] = quad;
            }
            return quad;
        }

        private GameObject CreateQuad()
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ProjectionMark";
            quad.layer = ProjectionLayer;
            quad.transform.SetParent(marksRoot, false);
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = EnsureMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return quad;
        }

        private void ShowAll()
        {
            int limit = Mathf.Min(VisibleMarkCount, pool.Count);
            for (int i = 0; i < limit; i++) if (pool[i] != null) pool[i].SetActive(true);
        }

        private void HideAll()
        {
            foreach (GameObject quad in pool) if (quad != null) quad.SetActive(false);
        }

        private Material EnsureMaterial()
        {
            if (markMaterial != null) return markMaterial;
            if (runtimeMaterial != null) return runtimeMaterial;
            string[] candidates = { "Sprites/Default", "Unlit/Transparent", "Legacy Shaders/Transparent/Diffuse", "Standard" };
            foreach (string candidate in candidates)
            {
                Shader shader = Shader.Find(candidate);
                if (shader == null) continue;
                runtimeMaterial = new Material(shader) { name = "RuntimeProjectionMark" };
                Debug.LogWarning("投影材质未指定，已回退使用 " + candidate + "；请在 Editor 中安装投影材质。", this);
                return runtimeMaterial;
            }
            Debug.LogError("找不到可用的投影 shader，投影已停用。", this);
            return null;
        }

        private void WarnIfCameraMissesProjection()
        {
            if (overheadCamera == null) return;
            if ((overheadCamera.cullingMask & (1 << ProjectionLayer)) == 0)
                Debug.LogWarning("顶置相机不包含投影层 " + projectionLayerName + "（" + ProjectionLayer +
                    "），投影不会进入实验图像。", this);
        }

        private static int ResolveLayer(string name, int fallback)
        {
            int layer = string.IsNullOrWhiteSpace(name) ? -1 : LayerMask.NameToLayer(name);
            if (layer >= 0) return layer;
            Debug.LogWarning("未找到层 " + name + "，改用 " + fallback + "；请在 Editor 中运行 Brick2Life/Simulation/Install feedback。");
            return Mathf.Clamp(fallback, 0, 31);
        }
    }
}
