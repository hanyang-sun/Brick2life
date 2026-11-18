using System;
using System.Collections.Generic;
using Brick2Life.Application;
using Brick2Life.Battle;
using Brick2Life.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Generates role-pattern content for the virtual projector output. Source quads are only visible
    /// while SimulationProjectedLight bakes its image; observers see light reflected by scene surfaces.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectionSimulator : MonoBehaviour, IProjectionGate
    {
        [Tooltip("投影面片的父节点。缺失时使用本组件所在对象。")]
        [SerializeField] private Transform marksRoot;
        [Tooltip("投影材质。应使用无光照、alpha 混合的材质（例如 Sprites/Default）。留空时在运行时回退创建。")]
        [SerializeField] private Material markMaterial;
        [Tooltip("投影内容源所在层，仅由虚拟投影仪内容相机绘制。场景相机接收表面反射光。")]
        [SerializeField] private string projectionLayerName = "Brick2LifeProjection";
        [SerializeField] private int fallbackProjectionLayer = 31;
        [Tooltip("单位积木顶面高度（世界 Y）。积木中心在 0.5，顶面在 1。")]
        [SerializeField, Min(0f)] private float brickTopHeight = 1f;
        [Tooltip("桌面高度（世界 Y）。空格的格点与提示投在桌面上。")]
        [SerializeField, Min(0f)] private float tableHeight = 0f;
        [Tooltip("投影强度，乘到标记 alpha 上；用于投影开/关与强弱对照。")]
        [SerializeField, Range(0f, 1f)] private float intensity = 1f;
        [SerializeField] private bool visible = true;

        private readonly List<GameObject> pool = new List<GameObject>();
        private readonly List<WallPattern> walls = new List<WallPattern>();
        private sealed class WallPattern
        {
            public Renderer Renderer;
            public Vector2 Center;
            public Vector4 Exposed;
            public Vector4 Nearby;
        }

        // Unity objects cannot be constructed in field initializers; created in Awake instead.
        private MaterialPropertyBlock properties;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int ExposedId = Shader.PropertyToID("_Exposed");
        private static readonly int InnerCornersId = Shader.PropertyToID("_InnerCorners");
        private static readonly int NearbyId = Shader.PropertyToID("_Nearby");
        private static readonly int ShowCrestId = Shader.PropertyToID("_ShowCrest");
        private Material runtimeMaterial;
        private Material ringMaterial, coreMaterial, wallMaterial, whirlpoolMaterial;
        private bool[] isChangeMark = Array.Empty<bool>();
        private const int CoreHitFrameCount = 17;
        private const int CoreIdleFrameCount = 24;
        private const float CoreHitFrameSeconds = 0.02f;
        private const float CoreIdleFrameSeconds = 0.065f;
        private Texture2D[] coreFrames;
        private float coreHitTime = float.NegativeInfinity;
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
            if (GetComponent<SimulationProjectedLight>() == null) gameObject.AddComponent<SimulationProjectedLight>();
        }

        private void Update()
        {
            if (LastPlan == null || pool.Count == 0) return;
            float now = Time.unscaledTime;
            float fade = fadeStart < 0f ? 1f : Mathf.Clamp01((now - fadeStart) / fadeDuration);
            UpdateCoreFrame(now);
            for (int i = 0; i < pool.Count; i++)
            {
                GameObject quad = pool[i];
                if (quad == null) continue;
                bool change = i < isChangeMark.Length && isChangeMark[i];
                if (change && fade >= 1f) { quad.SetActive(false); continue; }
                if (!visible) continue;
                float alpha = i < baseAlpha.Length ? baseAlpha[i] : 1f;
                if (change) alpha *= 1f - fade;
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
            if (coreFrames != null)
                foreach (Texture2D frame in coreFrames) Destroy(frame);
            if (coreMaterial != null) Destroy(coreMaterial);
            if (wallMaterial != null) Destroy(wallMaterial);
            foreach (Material material in new[] { ringMaterial, whirlpoolMaterial })
                if (material != null) { if (material.mainTexture != null) Destroy(material.mainTexture); Destroy(material); }
        }

        public void Render(ProjectionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            LastPlan = plan;
            walls.Clear();
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
            foreach (GameObject quad in pool)
                if (quad != null) { quad.SetActive(false); Destroy(quad); }
            pool.Clear(); walls.Clear(); VisibleMarkCount = 0; LastPlan = null;
            ResetCoreHit();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (visible) { UpdateCoreFrame(Time.unscaledTime); ShowAll(); } else HideAll();
        }

        public void SetIntensity(float value)
        {
            intensity = Mathf.Clamp01(value);
            if (LastPlan != null) Render(LastPlan);
        }

        private void EnsureArrays(int count)
        {
            if (isChangeMark.Length >= count) return;
            isChangeMark = new bool[count];
            baseAlpha = new float[count];
        }

        // Change rings, core emblems and coral barriers carry the role identity; color stays auxiliary.
        private Material MaterialFor(ProjectionMark mark, Material fallback)
        {
            EnsureTextures(fallback);
            if (mark.Shape == ProjectionShape.Whirlpool) return whirlpoolMaterial != null ? whirlpoolMaterial : fallback;
            if (mark.Kind == ProjectionMarkKind.ChangeHighlight || mark.Shape == ProjectionShape.Ring)
                return ringMaterial != null ? ringMaterial : fallback;
            if (mark.Kind != ProjectionMarkKind.RoleHighlight) return fallback;
            switch (mark.Role)
            {
                case GameRole.Core: return coreMaterial != null ? coreMaterial : fallback;
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
            whirlpoolMaterial = new Material(shader) { name = "ProjectionWhirlpool", mainTexture = MakeWhirlpool(96) };
            coreMaterial = new Material(shader) { name = "ProjectionCore", mainTexture = LoadCoreFrames() };
            Shader coralShader = Resources.Load<Shader>("ProjectedLight/CoralWall");
            if (coralShader != null) wallMaterial = new Material(coralShader) { name = "ProjectionWall" };
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

        private static Texture2D MakeWhirlpool(int size)
        {
            Texture2D texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2((2f * x + 1f) / size - 1f, (2f * y + 1f) / size - 1f);
                    float radius = point.magnitude;
                    float winding = Mathf.Atan2(point.y, point.x) / (2f * Mathf.PI) + radius / 0.42f;
                    float distance = Mathf.Abs(Mathf.Repeat(winding + 0.5f, 1f) - 0.5f) * 0.42f;
                    float stroke = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.033f, 0.065f, distance));
                    float ends = Mathf.InverseLerp(0.08f, 0.19f, radius) * (1f - Mathf.InverseLerp(0.77f, 0.93f, radius));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * stroke * ends));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private Texture2D LoadCoreFrames()
        {
            const int columns = 8;
            int frameCount = CoreHitFrameCount + CoreIdleFrameCount;
            int rows = (frameCount + columns - 1) / columns;
            TextAsset data = Resources.Load<TextAsset>("SparkCoreFrames");
            var strip = NewTexture(2);
            if (data == null || !ImageConversion.LoadImage(strip, data.bytes) ||
                strip.width % columns != 0 || strip.height != strip.width / columns * rows)
            {
                Destroy(strip);
                Debug.LogError("小焰投影帧资源缺失或尺寸不正确。", this);
                return null;
            }

            int size = strip.width / columns;
            coreFrames = new Texture2D[frameCount];
            for (int i = 0; i < coreFrames.Length; i++)
            {
                Texture2D frame = NewTexture(size);
                frame.SetPixels(strip.GetPixels(i % columns * size, (rows - 1 - i / columns) * size, size, size));
                frame.Apply(false, true);
                coreFrames[i] = frame;
            }
            Destroy(strip);
            return coreFrames[CoreHitFrameCount];
        }

        public void PlayCoreHit()
        {
            coreHitTime = Time.unscaledTime;
        }

        public void ResetCoreHit()
        {
            coreHitTime = float.NegativeInfinity;
            UpdateCoreFrame(Time.unscaledTime);
        }

        private void UpdateCoreFrame(float now)
        {
            if (coreFrames == null || coreMaterial == null) return;
            float elapsed = now - coreHitTime;
            float hitDuration = (CoreHitFrameCount - 1) * CoreHitFrameSeconds;
            float idleTime = float.IsPositiveInfinity(elapsed) ? now : Mathf.Max(0f, elapsed - hitDuration);
            int index = elapsed < hitDuration
                ? Mathf.FloorToInt(elapsed / CoreHitFrameSeconds)
                : CoreHitFrameCount + Mathf.FloorToInt(idleTime / CoreIdleFrameSeconds) % CoreIdleFrameCount;
            if (coreMaterial.mainTexture != coreFrames[index])
                coreMaterial.mainTexture = coreFrames[index];
        }

        internal void UpdateWallProximity(IReadOnlyList<EnemyState> enemies, float deltaTime)
        {
            foreach (WallPattern wall in walls)
            {
                Vector4 target = Vector4.zero;
                for (int i = 0; enemies != null && i < enemies.Count; i++)
                {
                    EnemyState enemy = enemies[i];
                    // Navigation cells are half the size of physical bricks.
                    float x = (float)enemy.X * 0.5f - wall.Center.x;
                    float z = (float)enemy.Y * 0.5f - wall.Center.y;
                    float along = Mathf.Min(Mathf.Abs(x), Mathf.Abs(z));
                    float normal = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (normal < 0.5f || normal >= 1.06f || along >= 0.68f) continue;

                    int side = Mathf.Abs(x) >= Mathf.Abs(z) ? (x < 0f ? 0 : 1) : (z < 0f ? 2 : 3);
                    if (wall.Exposed[side] == 0f) continue;
                    float strength = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.56f, 1.06f, normal))) *
                        (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.68f, along)));
                    target[side] = Mathf.Max(target[side], strength);
                }

                Vector4 nearby = wall.Nearby;
                for (int side = 0; side < 4; side++)
                    nearby[side] = Mathf.MoveTowards(nearby[side], target[side],
                        deltaTime * (target[side] > nearby[side] ? 9f : 4f));
                if (nearby.Equals(wall.Nearby)) continue;
                wall.Nearby = nearby;
                wall.Renderer.GetPropertyBlock(properties);
                properties.SetVector(NearbyId, nearby);
                wall.Renderer.SetPropertyBlock(properties);
            }
        }

        private void PlaceWall(Renderer renderer, ProjectionMark mark)
        {
            bool Connected(WallConnections side) => (mark.WallNeighbors & side) == side;
            var exposed = new Vector4(Connected(WallConnections.West) ? 0f : 1f,
                Connected(WallConnections.East) ? 0f : 1f, Connected(WallConnections.South) ? 0f : 1f,
                Connected(WallConnections.North) ? 0f : 1f);
            float InnerCorner(WallConnections sides, WallConnections diagonal) =>
                Connected(sides) && !Connected(diagonal) ? 1f : 0f;
            var corners = new Vector4(
                InnerCorner(WallConnections.West | WallConnections.South, WallConnections.SouthWest),
                InnerCorner(WallConnections.East | WallConnections.South, WallConnections.SouthEast),
                InnerCorner(WallConnections.West | WallConnections.North, WallConnections.NorthWest),
                InnerCorner(WallConnections.East | WallConnections.North, WallConnections.NorthEast));
            properties.SetVector(ExposedId, exposed);
            properties.SetVector(InnerCornersId, corners);
            properties.SetVector(NearbyId, Vector4.zero);
            properties.SetFloat(ShowCrestId, mark.ShowWallCrest ? 1f : 0f);
            walls.Add(new WallPattern { Renderer = renderer, Center = new Vector2(mark.CenterX, mark.CenterY), Exposed = exposed });
        }

        private void Place(GameObject quad, ProjectionMark mark, Material material)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            float surface = mark.Surface == ProjectionSurface.BrickTop ? brickTopHeight : tableHeight;
            quad.SetActive(visible);
            quad.transform.position = new Vector3(mark.CenterX, surface, mark.CenterY);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(mark.SizeX, mark.SizeY, 1f);
            Renderer renderer = quad.GetComponent<Renderer>();
            // Content has no physical depth offset; compositing order must not change its projected location.
            renderer.sortingOrder = mark.Kind == ProjectionMarkKind.ChangeHighlight ? 20 : -10;
            renderer.sharedMaterial = MaterialFor(mark, material);
            ProjectionColor color = mark.Color;
            bool core = mark.Kind == ProjectionMarkKind.RoleHighlight && mark.Role == GameRole.Core;
            properties.Clear();
            properties.SetColor(ColorId, core
                ? new Color(1f, 1f, 1f, color.Alpha * intensity)
                : new Color(color.Red, color.Green, color.Blue, color.Alpha * intensity));
            if (wallMaterial != null && renderer.sharedMaterial == wallMaterial) PlaceWall(renderer, mark);
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
            renderer.forceRenderingOff = true; // Image source only; the projector enables it solely while baking its output.
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

        private static int ResolveLayer(string name, int fallback)
        {
            int layer = string.IsNullOrWhiteSpace(name) ? -1 : LayerMask.NameToLayer(name);
            if (layer >= 0) return layer;
            Debug.LogWarning("未找到层 " + name + "，改用 " + fallback + "；请在 Editor 中运行 Brick2Life/Simulation/Install feedback。");
            return Mathf.Clamp(fallback, 0, 31);
        }
    }
}
