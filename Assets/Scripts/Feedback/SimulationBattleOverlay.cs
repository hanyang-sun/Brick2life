using System;
using System.Collections.Generic;
using Brick2Life.Battle;
using Brick2Life.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Battle projection overlay: flowing route arrows, tower range rings, muzzle flashes, hit ripples,
    /// core shock rings and wall ripples. Everything sits on the projection layer, so the overhead camera
    /// sees it exactly like the rest of the projector output and the top camera can keep sampling it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationBattleOverlay : MonoBehaviour
    {
        [SerializeField] private SimulationBattleController controller;
        [SerializeField] private ProjectionSimulator projector;
        [SerializeField] private Transform root;
        [SerializeField] private string projectionLayerName = "Brick2LifeProjection";
        [SerializeField, Range(0, 31)] private int fallbackLayer = 31;
        [SerializeField, Min(1)] private int arrowsPerRoute = 7;
        [SerializeField, Min(0.05f)] private float arrowSize = 0.34f;
        [SerializeField, Min(0.1f)] private float arrowSpeed = 1.6f;
        [SerializeField, Min(0.05f)] private float ringHeight = 0.02f;
        [SerializeField, Min(0.1f)] private float flashSeconds = 0.18f;
        [SerializeField, Min(0.1f)] private float rippleSeconds = 0.5f;

        private sealed class Fx
        {
            public GameObject Quad;
            public float Age;
            public float Life;
            public float StartScale;
            public float EndScale;
            public Color Color;
        }

        private readonly List<Fx> effects = new List<Fx>();
        private readonly List<GameObject> arrows = new List<GameObject>();
        private readonly List<GameObject> rangeRings = new List<GameObject>();
        private readonly List<GameObject> towerArrows = new List<GameObject>();
        private readonly List<Vector2> towerAim = new List<Vector2>();
        private readonly List<string> towerIds = new List<string>();
        private readonly List<Vector2> towerPositions = new List<Vector2>();
        private int arrowLevelHash;
        private Material arrowMaterial, ringMaterial;
        public int ActiveArrowCount { get { int c = 0; foreach (GameObject a in arrows) if (a != null && a.activeSelf) c++; return c; } }
        public int ActiveRangeRingCount { get { int c = 0; foreach (GameObject r in rangeRings) if (r != null && r.activeSelf) c++; return c; } }
        public int ActiveTowerArrowCount { get { int c = 0; foreach (GameObject a in towerArrows) if (a != null && a.activeSelf) c++; return c; } }
        public int ActiveEffectCount => effects.Count;
        private int projectionLayer;
        private int lastCoreHealth = -1;

        private BattleLevel lastLevel;
        private readonly List<float> wallCooldown = new List<float>();

        private void Awake()
        {
            if (controller == null) controller = FindFirstObjectByType<SimulationBattleController>();
            if (projector == null) projector = FindFirstObjectByType<ProjectionSimulator>();
            if (root == null) root = transform;
            projectionLayer = LayerMask.NameToLayer(projectionLayerName);
            if (projectionLayer < 0) projectionLayer = fallbackLayer;
            BuildMaterials();
        }

        private void OnDestroy()
        {
            foreach (Fx fx in effects) if (fx.Quad != null) Destroy(fx.Quad);
            foreach (GameObject arrow in arrows) if (arrow != null) Destroy(arrow);
            foreach (GameObject ring in rangeRings) if (ring != null) Destroy(ring);
            foreach (GameObject arrow in towerArrows) if (arrow != null) Destroy(arrow);
            if (arrowMaterial != null) Destroy(arrowMaterial);
            if (ringMaterial != null) Destroy(ringMaterial);
        }

        private void Update()
        {
            if (controller == null || controller.State == null) { HideAll(); return; }
            bool visible = projector == null || projector.IsVisible;
            if (!visible) { HideAll(); return; }
            BattleState state = controller.State;
            SyncRoutes();
            SyncRangeRings();
            SyncTowerArrows(state);
            HandleShots(state);
            HandleCore(state);
            HandleWallRipples(state);
            UpdateEffects();
            if (lastLevel != controller.Level) { lastLevel = controller.Level; lastCoreHealth = -1; }
        }

        private void HideAll()
        {
            foreach (GameObject arrow in arrows) if (arrow != null) arrow.SetActive(false);
            foreach (GameObject ring in rangeRings) if (ring != null) ring.SetActive(false);
            foreach (GameObject arrow in towerArrows) if (arrow != null) arrow.SetActive(false);
            foreach (Fx fx in effects) if (fx.Quad != null) fx.Quad.SetActive(false);
        }

        // ---- arrows -------------------------------------------------------------------------
        private void SyncRoutes()
        {
            int routes = controller.Level != null ? controller.Level.Routes.Count : 0;
            int wanted = routes * arrowsPerRoute;
            while (arrows.Count < wanted) arrows.Add(CreateQuad("BattleArrow", arrowMaterial, arrowSize));
            for (int i = 0; i < arrows.Count; i++) arrows[i].SetActive(i < wanted);
            if (routes == 0) return;
            float t = Time.unscaledTime * arrowSpeed;
            for (int r = 0; r < routes; r++)
            {
                IReadOnlyList<GridPoint> route = controller.Level.Routes[r].Route;
                for (int a = 0; a < arrowsPerRoute; a++)
                {
                    float progress = Mathf.Repeat(t / Mathf.Max(1, route.Count) + a / (float)arrowsPerRoute, 1f);
                    Sample(route, progress, out Vector3 position, out Vector3 direction);
                    GameObject arrow = arrows[r * arrowsPerRoute + a];
                    arrow.transform.position = position;
                    if (direction.sqrMagnitude > 1e-4f)
                        arrow.transform.rotation = Quaternion.Euler(90f, -Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg, 0f);
                }
            }
        }

        private static void Sample(IReadOnlyList<GridPoint> route, float progress, out Vector3 position, out Vector3 direction)
        {
            float scaled = Mathf.Clamp01(progress) * (route.Count - 1);
            int index = Mathf.Min((int)scaled, route.Count - 2);
            float fraction = scaled - index;
            Vector3 from = World(route[index]), to = World(route[index + 1]);
            position = Vector3.Lerp(from, to, fraction);
            direction = to - from;
        }

        private static Vector3 World(GridPoint cell) => new Vector3(cell.X * 0.5f, 0.035f, cell.Y * 0.5f);

        // ---- range rings --------------------------------------------------------------------
        private void SyncRangeRings()
        {
            int towers = 0;
            BattleLevel level = controller.Level;
            var seenTowers = new System.Collections.Generic.HashSet<string>();
            if (level != null)
                for (int i = 0; i < level.Cells.Count; i++)
                    if (level.Cells[i].Role == Application.GameRole.Tower && seenTowers.Add(level.Cells[i].SourceBrickId)) towers++;
            while (rangeRings.Count < towers) rangeRings.Add(CreateQuad("BattleRange", ringMaterial, 1f));
            if (towers == 0) return;
            float range = (float)controller.TowerSettings.Range * 0.5f;
            int used = 0;
            for (int i = 0; i < level.Cells.Count && used < towers; i++)
            {
                BattleCell cell = level.Cells[i];
                if (cell.Role != Application.GameRole.Tower || !seenTowers.Add(cell.SourceBrickId)) continue;
                GameObject ring = rangeRings[used++];
                ring.SetActive(true);
                ring.transform.position = new Vector3(cell.Position.X * 0.5f, ringHeight, cell.Position.Y * 0.5f);
                ring.transform.localScale = new Vector3(range * 2f, range * 2f, 1f);
                SetColor(ring, new Color(0.35f, 0.85f, 1f, 0.16f));
            }
            for (int i = towers; i < rangeRings.Count; i++) rangeRings[i].SetActive(false);
        }

        // Every tower aims an arrow at its nearest enemy; without enemies the last heading is kept.
        // Every tower aims an arrow at its nearest enemy; without enemies the last heading is kept.
        private void SyncTowerArrows(BattleState state)
        {
            BattleLevel level = controller.Level;
            if (level == null) { foreach (GameObject a in towerArrows) if (a != null) a.SetActive(false); return; }
            int hash = level.Width * 397 ^ level.Height * 17 ^ level.Cells.Count;
            if (towerIds.Count == 0 || arrowLevelHash != hash)
            {
                arrowLevelHash = hash;
                towerIds.Clear(); towerPositions.Clear();
                foreach (BattleCell cell in level.Cells)
                {
                    if (cell.Role != Application.GameRole.Tower || towerIds.Contains(cell.SourceBrickId)) continue;
                    towerIds.Add(cell.SourceBrickId);
                    towerPositions.Add(new Vector2(cell.Position.X * 0.5f, cell.Position.Y * 0.5f));
                }
                while (towerArrows.Count < towerIds.Count) { towerArrows.Add(CreateQuad("BattleTowerAim", arrowMaterial, 0.3f)); towerAim.Add(Vector2.right); }
            }
            for (int i = 0; i < towerArrows.Count; i++)
            {
                bool active = i < towerIds.Count;
                towerArrows[i].SetActive(active);
                if (!active) continue;
                Vector2 tower = towerPositions[i];
                Vector2 aim = towerAim[i];
                double best = double.MaxValue;
                foreach (EnemyState enemy in state.Enemies)
                {
                    double dx = enemy.X * 0.5 - tower.x, dz = enemy.Y * 0.5 - tower.y;
                    double distance = dx * dx + dz * dz;
                    if (distance >= best) continue;
                    best = distance; aim = new Vector2((float)dx, (float)dz);
                }
                if (aim.sqrMagnitude < 1e-4f) aim = Vector2.right;
                towerAim[i] = aim.normalized;
                GameObject arrow = towerArrows[i];
                arrow.transform.position = new Vector3(tower.x, 1.03f, tower.y);
                arrow.transform.rotation = Quaternion.Euler(90f, -Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, 0f);
                SetColor(arrow, new Color(1f, 0.7f, 0.25f, 0.95f));
            }
        }
        // ---- shot / hit / core / walls ------------------------------------------------------
        private void HandleShots(BattleState state)
        {
            IReadOnlyList<ShotEvent> shots = controller.LastShots;
            if (shots == null) return;
            foreach (ShotEvent shot in shots)
            {
                SpawnEffect(WorldAt(shot.TowerX, shot.TowerY), 0.12f, 0.5f, 1.5f, new Color(1f, 0.85f, 0.3f, 0.9f), flashSeconds);
                SpawnEffect(WorldAt(shot.TargetX, shot.TargetY), 0.2f, 0.4f, 1.8f, new Color(1f, 0.45f, 0.25f, 0.85f), rippleSeconds);
            }
        }

        private void HandleCore(BattleState state)
        {
            if (lastCoreHealth < 0) { lastCoreHealth = state.CoreHealth; return; }
            if (state.CoreHealth >= lastCoreHealth) return;
            lastCoreHealth = state.CoreHealth;
            BattleLevel level = controller.Level;
            if (level == null) return;
            for (int i = 0; i < level.Cells.Count; i++)
            {
                if (level.Cells[i].Role != Application.GameRole.Core) continue;
                SpawnEffect(WorldAt(level.Cells[i].Position.X * 0.5f + 0.25f, level.Cells[i].Position.Y * 0.5f + 0.25f),
                    0.4f, 0.6f, 3.2f, new Color(1f, 0.3f, 0.35f, 0.95f), rippleSeconds * 1.4f);
                break;
            }
        }

        // A wall ripple appears where an enemy is passing next to a wall, with a per-cell cooldown.
        private void HandleWallRipples(BattleState state)
        {
            BattleLevel level = controller.Level;
            if (level == null) return;
            while (wallCooldown.Count < level.Cells.Count) wallCooldown.Add(0f);
            for (int i = 0; i < wallCooldown.Count; i++) wallCooldown[i] = Mathf.Max(0f, wallCooldown[i] - Time.deltaTime);
            foreach (EnemyState enemy in state.Enemies)
            {
                float wx = (float)enemy.X * 0.5f, wz = (float)enemy.Y * 0.5f;
                for (int i = 0; i < level.Cells.Count; i++)
                {
                    if (level.Cells[i].Role != Application.GameRole.Wall) continue;
                    BattleCell cell = level.Cells[i];
                    float cx = cell.Position.X * 0.5f + 0.25f, cz = cell.Position.Y * 0.5f + 0.25f;
                    if (Mathf.Abs(cx - wx) > 0.8f || Mathf.Abs(cz - wz) > 0.8f) continue;
                    if (wallCooldown[i] > 0f) continue;
                    wallCooldown[i] = 0.7f;
                    SpawnEffect(new Vector3(cx, 1.01f, cz), 0.12f, 0.4f, 1.4f, new Color(0.75f, 0.9f, 1f, 0.7f), rippleSeconds);
                    break;
                }
            }
        }

        private void SpawnEffect(Vector3 position, float height, float startScale, float endScale, Color color, float life)
        {
            GameObject quad = CreateQuad("BattleFx", ringMaterial, 1f);
            quad.SetActive(true);
            quad.transform.position = new Vector3(position.x, height + 0.01f, position.z);
            var fx = new Fx { Quad = quad, Age = 0f, Life = life, StartScale = startScale, EndScale = endScale, Color = color };
            ApplyFx(fx, 0f);
            effects.Add(fx);
        }

        private void UpdateEffects()
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Fx fx = effects[i];
                if (fx.Quad == null) { effects.RemoveAt(i); continue; }
                fx.Age += Time.deltaTime;
                float t = fx.Age / fx.Life;
                if (t >= 1f) { Destroy(fx.Quad); effects.RemoveAt(i); continue; }
                ApplyFx(fx, t);
            }
        }

        private void ApplyFx(Fx fx, float t)
        {
            float scale = Mathf.Lerp(fx.StartScale, fx.EndScale, t);
            fx.Quad.transform.localScale = new Vector3(scale, scale, 1f);
            Color color = fx.Color;
            color.a *= 1f - t;
            SetColor(fx.Quad, color);
        }

        // ---- helpers ------------------------------------------------------------------------
        private static Vector3 WorldAt(double x, double y) => new Vector3((float)x * 0.5f, 0.03f, (float)y * 0.5f);

        private void SetColor(GameObject quad, Color color)
        {
            var renderer = quad.GetComponent<Renderer>();
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(Shader.PropertyToID("_Color"), color);
            renderer.SetPropertyBlock(block);
        }

        private GameObject CreateQuad(string name, Material material, float size)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.layer = projectionLayer;
            quad.transform.SetParent(root, false);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(size, size, 1f);
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return quad;
        }

        private void BuildMaterials()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            arrowMaterial = new Material(shader) { name = "BattleArrow", mainTexture = MakeArrowTexture(64) };
            ringMaterial = new Material(shader) { name = "BattleRing", mainTexture = MakeRingTexture(64) };
        }

        // Triangle pointing along +X so the quad rotation can aim it down the route.
        private static Texture2D MakeArrowTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1), v = y / (float)(size - 1);
                    float alpha = 0f;
                    if (u > 0.15f && u < 0.95f && Mathf.Abs(v - 0.5f) < (0.95f - u) * 0.5f) alpha = 1f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return texture;
        }

        private static Texture2D MakeRingTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x - center, y - center).magnitude / center;
                    float alpha = (r > 0.78f && r < 0.98f) ? 1f : 0f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return texture;
        }
    }
}
