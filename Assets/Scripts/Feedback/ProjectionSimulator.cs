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
        private Material ringMaterial, coreMaterial, towerMaterial, wallMaterial;
        private bool[] isChangeMark = Array.Empty<bool>();
        private bool[] isCoreMark = Array.Empty<bool>();
        private float[] baseAlpha = Array.Empty<float>();
        private float fadeStart = -1f;
        private float fadeDuration = 1.6f;
        private long lastChangeVersion = -1;

        public ProjectionPlan LastPlan { get; private set; }
        public int VisibleMarkCount { get; private set; }
        public int ActiveMarkCount
        {
            get { int count = 0; foreach (GameObject quad in pool) if (quad != null && quad.activeSelf) count++; return count; }
        }
        public int LastChangeMarkCount => LastPlan == null ? 0 : LastPlan.Count(ProjectionMarkKind.ChangeHighlight);
        public int RoleMarkCount(GameRole role)
        {
            if (LastPlan == null) return 0;
            int count = 0;
            foreach (ProjectionMark mark in LastPlan.Marks)
                if (mark.Kind == ProjectionMarkKind.RoleHighlight && mark.Role == role) count++;
            return count;
        }
        public string MaterialNameOf(GameRole role)
        {
            if (LastPlan == null) return "none";
            for (int i = 0; i < Mathf.Min(VisibleMarkCount, LastPlan.Marks.Count); i++)
            {
                ProjectionMark mark = LastPlan.Marks[i];
                if (mark.Kind != ProjectionMarkKind.RoleHighlight || mark.Role != role) continue;
                if (i >= pool.Count || pool[i] == null) continue;
                var renderer = pool[i].GetComponent<Renderer>();
                return renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "none";
            }
            return "none";
        }
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

        private void Update()
        {
            if (LastPlan == null || pool.Count == 0) return;
            float now = Time.unscaledTime;
            float fade = fadeStart < 0f ? 1f : Mathf.Clamp01((now - fadeStart) / fadeDuration);
            float breath = 0.85f + 0.15f * Mathf.Sin(now * 2.2f);
            for (int i = 0; i < pool.Count; i++)
            {
                GameObject quad = pool[i];
                if (quad == null) continue;
                bool change = i < isChangeMark.Length && isChangeMark[i];
                if (change && fade >= 1f) { quad.SetActive(false); continue; }
                if (!visible) continue;
                float alpha = i < baseAlpha.Length ? baseAlpha[i] : 1f;
                if (change) alpha *= 1f - fade;
                else if (i < isCoreMark.Length && isCoreMark[i]) alpha *= breath;
                ApplyAlpha(quad, alpha);
            }
        }

        private void ApplyAlpha(GameObject quad, float alpha)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            var renderer = quad.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.GetPropertyBlock(properties);
            Color color = properties.GetColor(ColorId);
            color.a = Mathf.Clamp01(alpha * intensity);
            properties.SetColor(ColorId, color);
            renderer.SetPropertyBlock(properties);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
            foreach (Material material in new[] { ringMaterial, coreMaterial, towerMaterial, wallMaterial })
                if (material != null) { if (material.mainTexture != null) Destroy(material.mainTexture); Destroy(material); }
        }

        public void Render(ProjectionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            LastPlan = plan;
            if (marksRoot == null) marksRoot = transform;
            Material material = EnsureMaterial();
            if (material == null) { HideAll(); return; }
            int used = 0;
            EnsureArrays(plan.Marks.Count);
            if (plan.LevelVersion != lastChangeVersion)
            {
                lastChangeVersion = plan.LevelVersion;
                fadeStart = Time.unscaledTime;
                fadeDuration = Mathf.Max(0.1f, plan.ChangeFadeSeconds);
            }
            foreach (ProjectionMark mark in plan.Marks)
            {
                GameObject quad = Obtain(used);
                isChangeMark[used] = mark.Kind == ProjectionMarkKind.ChangeHighlight;
                isCoreMark[used] = mark.Kind == ProjectionMarkKind.RoleHighlight && mark.Role == GameRole.Core;
                baseAlpha[used] = mark.Color.Alpha;
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

        // Counts marks whose quad falls outside the board footprint; useful when tuning sizes or clamping.
        public int CountMarksOutsideBoard(ProjectionPlan plan)
        {
            if (plan == null) return -1;
            int count = 0;
            for (int i = 0; i < Mathf.Min(VisibleMarkCount, plan.Marks.Count); i++)
            {
                if (!TryGetMarkCenter(i, out Vector3 center)) continue;
                ProjectionMark mark = plan.Marks[i];
                float halfX = mark.SizeX * 0.5f, halfY = mark.SizeY * 0.5f;
                if (mark.CenterX - halfX < -1e-3f || mark.CenterY - halfY < -1e-3f ||
                    mark.CenterX + halfX > plan.Definition.Width + 1e-3f ||
                    mark.CenterY + halfY > plan.Definition.Height + 1e-3f) count++;
            }
            return count;
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

        private void EnsureArrays(int count)
        {
            if (isChangeMark.Length >= count) return;
            isChangeMark = new bool[count];
            isCoreMark = new bool[count];
            baseAlpha = new float[count];
        }

        // Change rings, core emblems, tower targets and wall courses carry the role identity; color stays auxiliary.
        private Material MaterialFor(ProjectionMark mark, Material fallback)
        {
            EnsureTextures(fallback);
            if (mark.Kind == ProjectionMarkKind.ChangeHighlight) return ringMaterial != null ? ringMaterial : fallback;
            if (mark.Kind != ProjectionMarkKind.RoleHighlight) return fallback;
            switch (mark.Role)
            {
                case GameRole.Core: return coreMaterial != null ? coreMaterial : fallback;
                case GameRole.Tower: return towerMaterial != null ? towerMaterial : fallback;
                case GameRole.Wall: return wallMaterial != null ? wallMaterial : fallback;
                default: return fallback;
            }
        }

        private void EnsureTextures(Material fallback)
        {
            if (ringMaterial != null) return;
            Shader shader = fallback != null ? fallback.shader : Shader.Find("Sprites/Default");
            if (shader == null) return;
            ringMaterial = new Material(shader) { name = "ProjectionRing", mainTexture = MakeRing(96, 0.55f) };
            coreMaterial = new Material(shader) { name = "ProjectionCore", mainTexture = MakeCoreEmblem(96) };
            towerMaterial = new Material(shader) { name = "ProjectionTower", mainTexture = MakeTowerTarget(96) };
            wallMaterial = new Material(shader) { name = "ProjectionWall", mainTexture = MakeWallPattern(96) };
        }

        private static Texture2D NewTexture(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        private static Texture2D MakeRing(int size, float inner)
        {
            Texture2D texture = NewTexture(size);
            float center = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x - center, y - center).magnitude / center;
                    float alpha = (r >= inner && r <= 0.94f) ? 1f : 0f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return texture;
        }

        private static Texture2D MakeCoreEmblem(int size)
        {
            Texture2D texture = NewTexture(size);
            float center = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center, dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float diamond = Mathf.Abs(dx) + Mathf.Abs(dy);
                    float alpha = 0f;
                    if (r < 0.85f) alpha = 0.18f;
                    if (r > 0.52f && r < 0.62f) alpha = 0.9f;
                    if (r > 0.78f && r < 0.86f) alpha = 0.75f;
                    if (diamond < 0.3f) alpha = Mathf.Max(alpha, 0.95f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return texture;
        }

        private static Texture2D MakeTowerTarget(int size)
        {
            Texture2D texture = NewTexture(size);
            float center = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center, dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 0f;
                    if (r < 0.9f && r > 0.84f) alpha = 0.85f;
                    if (r < 0.5f && r > 0.44f) alpha = 0.7f;
                    if (Mathf.Abs(dx) < 0.035f && Mathf.Abs(dy) < 0.42f) alpha = Mathf.Max(alpha, 0.8f);
                    if (Mathf.Abs(dy) < 0.035f && Mathf.Abs(dx) < 0.42f) alpha = Mathf.Max(alpha, 0.8f);
                    if (r < 0.12f) alpha = 0.95f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return texture;
        }

        private static Texture2D MakeWallPattern(int size)
        {
            Texture2D texture = NewTexture(size);
            var pixels = new Color32[size * size];
            const int courses = 4;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1), v = y / (float)(size - 1);
                    int course = Mathf.FloorToInt(v * courses);
                    float offset = course % 2 == 0 ? 0f : 0.5f;
                    float joint = Mathf.Abs(((u * 2f + offset) % 1f) - 0.5f);
                    float alpha = joint < 0.06f ? 0.55f : 0.16f;
                    float border = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    if (border < 0.07f) alpha = 0.85f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return texture;
        }

        private void Place(GameObject quad, ProjectionMark mark, Material material)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            float surface = mark.Surface == ProjectionSurface.BrickTop ? brickTopHeight : tableHeight;
            quad.SetActive(visible);
            quad.transform.position = new Vector3(mark.CenterX, surface + surfaceOffset, mark.CenterY);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(mark.SizeX, mark.SizeY, 1f);
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialFor(mark, material);
            ProjectionColor color = mark.Color;
            properties.SetColor(ColorId, new Color(color.Red, color.Green, color.Blue, color.Alpha * intensity));
            renderer.SetPropertyBlock(properties);
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
