using System;
using System.Collections.Generic;
using Brick2Life.Battle;
using Brick2Life.Core;
using Brick2Life.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Battle projection overlay: ocean currents, octopus towers, bubble attacks,
    /// and core shock rings. These renderers generate the projector's output image;
    /// observing cameras receive its light on the board and bricks instead of seeing source quads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationBattleOverlay : MonoBehaviour, IProjectionGate
    {
        [SerializeField] private SimulationBattleController controller;
        [SerializeField] private ProjectionSimulator projector;
        [SerializeField] private Transform root;
        [SerializeField] private string projectionLayerName = "Brick2LifeProjection";
        [SerializeField, Range(0, 31)] private int fallbackLayer = 31;
        [SerializeField, Min(0.1f)] private float rippleSeconds = 0.5f;

        private sealed class Fx
        {
            public GameObject Quad;
            public float Age;
            public float Life;
            public float StartScale;
            public float EndScale;
            public Color Color;
            public Vector3 Velocity;
        }

        private readonly List<Fx> effects = new List<Fx>();
        private readonly List<GameObject> rangeRings = new List<GameObject>();
        private readonly List<GameObject> towerArrows = new List<GameObject>();
        private readonly List<Vector2> towerAim = new List<Vector2>();
        private readonly List<string> towerIds = new List<string>();
        private readonly List<Vector2> towerPositions = new List<Vector2>();
        private readonly List<GameObject> bubbles = new List<GameObject>();
        private readonly List<GameObject> faces = new List<GameObject>();
        private readonly Dictionary<string, float> celebrations = new Dictionary<string, float>();
        private readonly Dictionary<int, float> hitTimes = new Dictionary<int, float>();
        private Material armMaterial, bubbleMaterial;
        private Material[] faceMaterials;
        private readonly List<GameObject> enemyIcons = new List<GameObject>();
        private readonly List<GameObject> enemyBars = new List<GameObject>();
        private readonly List<GameObject> enemyFills = new List<GameObject>();
        private const float EnemySize = 0.58f;
        private const float EnemyHitDuration = 0.25f;
        private const float EnemyHealthDuration = 0.75f;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int HitId = Shader.PropertyToID("_Hit");
        private static readonly int SwimPhaseId = Shader.PropertyToID("_SwimPhase");
        private MaterialPropertyBlock properties;
        private readonly List<GameObject> spawnMarkers = new List<GameObject>();
        private GameObject currentLane;
        private IReadOnlyList<BattleRoute> currentRoutes;
        private Material ringMaterial, enemyMaterial, enemyBurstMaterial, barMaterial, pathMaterial, rangeMaterial;
        public int ActiveEnemyCount { get { int c = 0; foreach (GameObject e in enemyIcons) if (e != null && e.activeSelf) c++; return c; } }
        public int SpawnMarkerCount => spawnMarkers.Count;
        public int RouteCount => controller != null && controller.DisplayRoutes != null ? controller.DisplayRoutes.Count : 0;
        public bool RouteProjectionVisible => captureVisible && currentLane != null && currentLane.activeInHierarchy;
        public int ActiveRangeRingCount { get { int c = 0; foreach (GameObject r in rangeRings) if (r != null && r.activeSelf) c++; return c; } }
        public int ActiveTowerArrowCount { get { int c = 0; foreach (GameObject a in towerArrows) if (a != null && a.activeSelf) c++; return c; } }
        public int ActiveEffectCount => effects.Count;
        private int projectionLayer;
        private int lastCoreHealth = -1;

        private BattleLevel lastLevel;
        private BattleSimulation lastBattle;
        private long lastShotSequence = -1;
        public BattleLevel DisplayedLevel => lastLevel;
        private bool captureVisible = true;
        private float animationTime;
        public Transform MarksRoot => root;
        bool IProjectionGate.IsProjectionVisible => captureVisible;

        void IProjectionGate.SetProjectionVisible(bool value)
        {
            captureVisible = value;
            if (root != null)
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    renderer.forceRenderingOff = true; // Content is only drawn into the projector output image.
        }

        private void OnEnable() => ProjectionCaptureGate.Register(this);
        private void OnDisable()
        {
            ProjectionCaptureGate.Unregister(this);
            if (currentLane != null) currentLane.SetActive(false);
            HideBattleEffects();
        }

        private void Awake()
        {
            if (controller == null) controller = FindFirstObjectByType<SimulationBattleController>();
            if (projector == null) projector = FindFirstObjectByType<ProjectionSimulator>();
            if (root == null || root == transform)
            {
                root = new GameObject("BattleProjection").transform;
                root.SetParent(transform, false);
            }
            projectionLayer = LayerMask.NameToLayer(projectionLayerName);
            if (projectionLayer < 0) projectionLayer = fallbackLayer;
            properties = new MaterialPropertyBlock();
            BuildMaterials();
        }

        private void OnDestroy()
        {
            foreach (Fx fx in effects) if (fx.Quad != null) Destroy(fx.Quad);
            if (currentLane != null) Destroy(currentLane);
            foreach (GameObject ring in rangeRings) if (ring != null) Destroy(ring);
            foreach (GameObject arrow in towerArrows) if (arrow != null) Destroy(arrow);
            foreach (GameObject icon in enemyIcons) if (icon != null) Destroy(icon);
            foreach (GameObject bar in enemyBars) if (bar != null) Destroy(bar);
            foreach (GameObject fill in enemyFills) if (fill != null) Destroy(fill);
            foreach (GameObject marker in spawnMarkers) if (marker != null) Destroy(marker);
            foreach (GameObject bubble in bubbles) Destroy(bubble);
            foreach (GameObject face in faces) Destroy(face);
            foreach (Material face in faceMaterials ?? Array.Empty<Material>())
            {
                Destroy(face.mainTexture);
                Destroy(face);
            }
            if (enemyMaterial != null) Destroy(enemyMaterial);
            if (enemyBurstMaterial != null) Destroy(enemyBurstMaterial);
            foreach (Material material in new[] { ringMaterial, barMaterial, pathMaterial, rangeMaterial, armMaterial, bubbleMaterial })
                if (material != null)
                {
                    if (material.mainTexture != null && material.mainTexture != Texture2D.whiteTexture && material.mainTexture != Texture2D.blackTexture)
                        Destroy(material.mainTexture);
                    Destroy(material);
                }
        }

        private void SynchronizeLevel()
        {
            if (controller != null && (lastLevel != controller.Level || !ReferenceEquals(lastBattle, controller.Battle)))
            {
                foreach (Fx fx in effects) if (fx.Quad != null) Destroy(fx.Quad);
                effects.Clear();
                towerIds.Clear();
                celebrations.Clear();
                hitTimes.Clear();
                lastLevel = controller.Level;
                if (!ReferenceEquals(lastBattle, controller.Battle))
                {
                    lastCoreHealth = -1;
                    projector?.ResetCoreHit();
                    lastShotSequence = -1;
                }
                lastBattle = controller.Battle;
                animationTime = 0;
            }
        }

        // Confirmation may arrive after this component's Update. Refresh geometry before any preview renders it.
        public void RefreshLayout()
        {
            SynchronizeLevel();
            SyncRoutes();
            if (controller == null || controller.State == null || controller.State.WaitingForLayout ||
                (projector != null && !projector.IsVisible)) { HideBattleEffects(); return; }
            SyncRangeRings(controller.State);
            SyncBubbles();
            SyncTowerArrows(controller.State);
            SyncEnemies(controller.State);
        }

        private void Update()
        {
            SynchronizeLevel();
            BattleState state = controller != null ? controller.State : null;
            if (state == null || !state.Paused)
                projector?.UpdateWallProximity(state != null && !state.Finished ? state.Enemies : null, Time.deltaTime);
            if (state == null || state.WaitingForLayout || (projector != null && !projector.IsVisible))
            {
                SyncRoutes();
                HideBattleEffects();
                return;
            }
            if (!state.Paused) animationTime += Time.deltaTime;
            HandleShots();
            SyncRoutes();
            SyncRangeRings(state);
            SyncBubbles();
            SyncTowerArrows(state);
            SyncEnemies(state);
            HandleCore(state);
            UpdateEffects();
        }

        private void HideBattleEffects()
        {
            foreach (GameObject ring in rangeRings) if (ring != null) ring.SetActive(false);
            foreach (GameObject arrow in towerArrows) if (arrow != null) arrow.SetActive(false);
            foreach (GameObject bubble in bubbles) bubble.SetActive(false);
            foreach (GameObject face in faces) face.SetActive(false);
            foreach (GameObject icon in enemyIcons) icon.SetActive(false);
            foreach (GameObject bar in enemyBars) bar.SetActive(false);
            foreach (GameObject fill in enemyFills) fill.SetActive(false);
            foreach (Fx fx in effects) if (fx.Quad != null) fx.Quad.SetActive(false);
        }

        // ---- ocean current ------------------------------------------------------------------
        private void SyncRoutes()
        {
            BattleState state = controller != null ? controller.State : null;
            IReadOnlyList<BattleRoute> routes = controller != null ? controller.DisplayRoutes : null;
            if (!isActiveAndEnabled || pathMaterial == null || routes == null || routes.Count == 0 || controller.Level == null ||
                state?.WaitingForLayout == true || (state == null && !controller.GeneratedObjectives) ||
                (projector != null && !projector.IsVisible))
            {
                if (currentLane != null) currentLane.SetActive(false);
                return;
            }
            if (currentLane == null)
            {
                pathMaterial.mainTexture = new Texture2D(OceanCurrentMap.Resolution, OceanCurrentMap.Resolution,
                    TextureFormat.RGBAFloat, false, true)
                {
                    name = "Ocean Current Field", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                currentLane = CreateQuad("BattlePath", pathMaterial, 1f);
            }
            if (!ReferenceEquals(currentRoutes, routes))
            {
                Vector2 size = new Vector2(controller.Level.Width, controller.Level.Height) / BattleLevel.NavigationPerPhysical;
                currentLane.transform.position = new Vector3(size.x * 0.5f, 0f, size.y * 0.5f);
                currentLane.transform.localScale = new Vector3(size.x, size.y, 1f);
                OceanCurrentMap.Bake((Texture2D)pathMaterial.mainTexture, size, routes);
                currentRoutes = routes;
            }
            currentLane.SetActive(true);
            pathMaterial.SetFloat("_FlowTime", animationTime);
            pathMaterial.SetFloat("_RouteOpacity", state == null ? 0.65f : 1f);
        }

        private static Vector3 World(GridPoint cell) => World(cell, 0f);

        // ---- range rings --------------------------------------------------------------------
        private void SyncRangeRings(BattleState state)
        {
            int towers = 0;
            BattleLevel level = controller.Level;
            var seenTowers = new System.Collections.Generic.HashSet<string>();
            if (level != null)
                for (int i = 0; i < level.Cells.Count; i++)
                    if (level.Cells[i].Role == Application.GameRole.Tower && seenTowers.Add(level.Cells[i].SourceBrickId)) towers++;
            while (rangeRings.Count < towers) rangeRings.Add(CreateQuad("BattleRange", rangeMaterial, 1f));
            seenTowers.Clear();
            float range = (float)(controller.Battle != null ? controller.Battle.Towers.Range : controller.TowerSettings.Range) * 0.5f;
            int used = 0;
            for (int i = 0; i < level.Cells.Count && used < towers; i++)
            {
                BattleCell cell = level.Cells[i];
                if (cell.Role != Application.GameRole.Tower || !seenTowers.Add(cell.SourceBrickId)) continue;
                GameObject ring = rangeRings[used++];
                ring.SetActive(true);
                Vector3 center = BrickCenter(cell, 0f);
                ring.transform.position = center;
                ring.transform.localScale = new Vector3(range * 2f, range * 2f, 1f);
                bool invaded = false;
                foreach (EnemyState enemy in state.Enemies)
                {
                    float dx = (float)enemy.X * 0.5f - center.x, dz = (float)enemy.Y * 0.5f - center.z;
                    if (dx * dx + dz * dz <= range * range) { invaded = true; break; }
                }
                SetColor(ring, invaded ? new Color(0.56f, 1f, 0.93f, 0.95f) : new Color(0.35f, 0.85f, 1f, 0.60f));
            }
            for (int i = towers; i < rangeRings.Count; i++) rangeRings[i].SetActive(false);
        }

        private static Vector3 World(GridPoint cell, float height) => WorldAt(cell.X, cell.Y, height);

        private void SyncBubbles()
        {
            int used = 0;
            if (controller.Battle != null)
                foreach (BattleSimulation.Bubble bubble in controller.Battle.Bubbles)
                {
                    if (bubble.TimeRemaining > BattleSimulation.BubbleFlight) continue;
                    if (used == bubbles.Count) bubbles.Add(CreateQuad("BattleBubble", bubbleMaterial, 0.28f));
                    GameObject quad = bubbles[used++];
                    quad.SetActive(true);
                    Vector3 position = WorldAt(bubble.X, bubble.Y);
                    position.y = SurfaceHeight(position.x, position.z);
                    quad.transform.position = position;
                }
            for (int i = used; i < bubbles.Count; i++) bubbles[i].SetActive(false);
        }

        private void SyncTowerArrows(BattleState state)
        {
            BattleLevel level = controller.Level;
            if (level == null) return;
            if (towerIds.Count == 0)
            {
                towerPositions.Clear();
                foreach (BattleCell cell in level.Cells)
                {
                    if (cell.Role != Application.GameRole.Tower || towerIds.Contains(cell.SourceBrickId)) continue;
                    towerIds.Add(cell.SourceBrickId);
                    Vector3 center = BrickCenter(cell, 0f);
                    towerPositions.Add(new Vector2(center.x, center.z));
                }
                while (faces.Count < towerIds.Count)
                {
                    faces.Add(CreateQuad("OctopusFace", faceMaterials[0], 0.64f));
                    towerAim.Add(Vector2.zero);
                    for (int arm = 0; arm < 4; arm++)
                        towerArrows.Add(CreateQuad("OctopusArm", armMaterial, 0.68f));
                }
            }
            for (int i = 0; i < faces.Count; i++)
            {
                bool active = i < towerIds.Count;
                faces[i].SetActive(active);
                for (int arm = 0; arm < 4; arm++) towerArrows[i * 4 + arm].SetActive(active);
                if (!active) continue;

                Vector2 center = towerPositions[i];
                Vector2 aim = Vector2.zero;
                float charge = 0f;
                if (controller.Battle != null)
                    foreach (BattleSimulation.Bubble bubble in controller.Battle.Bubbles)
                    {
                        if (bubble.TowerId != towerIds[i]) continue;
                        foreach (EnemyState enemy in state.Enemies)
                        {
                            if (enemy.Id != bubble.EnemyId) continue;
                            aim = new Vector2((float)enemy.X * 0.5f, (float)enemy.Y * 0.5f) - center;
                            break;
                        }
                        double age = BattleSimulation.BubbleFlight + BattleSimulation.BubbleWindup - bubble.TimeRemaining;
                        charge = Mathf.SmoothStep(0f, 1f, (float)(age / BattleSimulation.BubbleWindup)) *
                            Mathf.SmoothStep(0f, 1f, (float)((bubble.TimeRemaining - BattleSimulation.BubbleFlight + 0.18) / 0.18));
                        break;
                    }
                Vector2 heading = aim.normalized;
                if (!state.Paused)
                    towerAim[i] = Vector2.Lerp(towerAim[i], heading * charge, 1f - Mathf.Exp(-20f * Time.deltaTime));

                int aimingArm = Mathf.RoundToInt((Mathf.Atan2(towerAim[i].y, towerAim[i].x) * Mathf.Rad2Deg - 45f) / 90f);
                aimingArm = (aimingArm + 4) % 4;
                for (int arm = 0; arm < 4; arm++)
                {
                    float angle = 45f + 90f * arm;
                    float stretch = arm == aimingArm ? towerAim[i].magnitude : 0f;
                    angle = Mathf.LerpAngle(angle, Mathf.Atan2(towerAim[i].y, towerAim[i].x) * Mathf.Rad2Deg, stretch);
                    angle += 5f * Mathf.Sin(animationTime * 2.6f + arm + i) * (1f - stretch);
                    float radians = angle * Mathf.Deg2Rad;
                    float radius = 0.88f + stretch * 0.08f;
                    Transform quad = towerArrows[i * 4 + arm].transform;
                    quad.position = new Vector3(center.x + Mathf.Cos(radians) * radius, 0f,
                        center.y + Mathf.Sin(radians) * radius);
                    quad.rotation = Quaternion.Euler(90f, -angle, 0f);
                    quad.localScale = new Vector3(0.94f + stretch * 0.15f, 0.84f, 1f);
                }
                Transform face = faces[i].transform;
                face.position = new Vector3(center.x, SurfaceHeight(center.x, center.y), center.y);
                float blinkPhase = Mathf.Clamp01(((animationTime + i * 0.4f) % 3.8f - 2.7f) / 0.21f);
                float blink = Mathf.Pow(Mathf.Sin(Mathf.PI * blinkPhase), 2f);
                float delight = 0f;
                if (celebrations.TryGetValue(towerIds[i], out float until))
                {
                    float age = animationTime - (until - 0.4f);
                    delight = Mathf.SmoothStep(0f, 1f, age / 0.1f) *
                        (1f - Mathf.SmoothStep(0f, 1f, (age - 0.19f) / 0.21f));
                }
                int blowingFrame = Mathf.RoundToInt(charge * 8f);
                int blinkFrame = Mathf.RoundToInt(Mathf.Max(blink, delight) * 8f);
                int expression = blowingFrame > 0 ? blowingFrame : blinkFrame == 0 ? 0 : 8 + blinkFrame;
                faces[i].GetComponent<Renderer>().sharedMaterial = faceMaterials[expression];
                face.localScale = new Vector3(0.99f, 0.99f, 1f);
            }
        }

        // ---- enemies ------------------------------------------------------------------------
        private void SyncEnemies(BattleState state)
        {
            if (state == null || enemyMaterial == null) return;
            int count = state.Enemies.Count;
            EnsureEnemyPool(count);
            int maxHealth = controller != null && controller.Battle != null ? controller.Battle.Plan.EnemyHealth : 1;
            for (int i = 0; i < enemyIcons.Count; i++)
            {
                bool active = i < count;
                enemyIcons[i].SetActive(active);
                if (!active)
                {
                    enemyBars[i].SetActive(false);
                    enemyFills[i].SetActive(false);
                    continue;
                }
                EnemyState enemy = state.Enemies[i];
                Vector3 position = WorldAt(enemy.X, enemy.Y);
                position.y = SurfaceHeight(position.x, position.z);
                float yaw = Mathf.Atan2((float)enemy.HeadingX, (float)enemy.HeadingY) * Mathf.Rad2Deg;
                Quaternion facing = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
                enemyIcons[i].transform.SetPositionAndRotation(position, facing);

                float hitAge = EnemyHealthDuration;
                if (hitTimes.TryGetValue(enemy.Id, out float hitTime))
                {
                    hitAge = animationTime - hitTime;
                    if (hitAge >= EnemyHealthDuration) hitTimes.Remove(enemy.Id);
                }
                properties.Clear();
                properties.SetFloat(HitId, 1f - Mathf.SmoothStep(0f, 1f, hitAge / EnemyHitDuration));
                properties.SetFloat(SwimPhaseId, Mathf.Sin((float)enemy.RouteProgress * 6f + enemy.Id * 1.73f));
                enemyIcons[i].GetComponent<Renderer>().SetPropertyBlock(properties);

                float ratio = Mathf.Clamp01(enemy.Health / (float)Mathf.Max(1, maxHealth));
                bool showHealth = ratio < 1f && hitAge < EnemyHealthDuration;
                enemyBars[i].SetActive(showHealth);
                enemyFills[i].SetActive(showHealth);
                if (!showHealth) continue;
                float opacity = 1f - Mathf.InverseLerp(0.50f, EnemyHealthDuration, hitAge);
                const float fillWidth = 0.36f;
                Vector3 barPosition = position + new Vector3(0f, 0f, -0.34f);
                enemyBars[i].transform.position = barPosition;
                SetColor(enemyBars[i], new Color(0.05f, 0.05f, 0.07f, 0.85f * opacity));
                float width = fillWidth * ratio;
                enemyFills[i].transform.position = barPosition + new Vector3((width - fillWidth) * 0.5f, 0f, 0f);
                enemyFills[i].transform.localScale = new Vector3(Mathf.Max(0.001f, width), 0.05f, 1f);
                SetColor(enemyFills[i], Color.Lerp(new Color(1f, 0.25f, 0.2f, opacity),
                    new Color(1f, 0.89f, 0.55f, opacity), ratio));
            }
        }

        private float SurfaceHeight(float x, float z)
        {
            BattleLevel level = controller != null ? controller.Level : null;
            if (level == null) return 0f;
            int cx = Mathf.FloorToInt(x), cz = Mathf.FloorToInt(z);
            for (int i = 0; i < level.Cells.Count; i++)
            {
                BattleCell cell = level.Cells[i];
                if (cell.Position.X / 2 != cx || cell.Position.Y / 2 != cz) continue;
                if (cell.Role == Application.GameRole.None) continue;
                // A generated core is a reserved floor cell, with no physical brick.
                if (cell.IsCore && controller.Round != null) return 0f;
                return 1.0f;
            }
            return 0f;
        }

        private void EnsureEnemyPool(int count)
        {
            while (enemyIcons.Count < count)
            {
                enemyIcons.Add(CreateQuad("BattleEnemy", enemyMaterial, EnemySize));
                GameObject bar = CreateQuad("BattleEnemyBar", barMaterial, 0.40f);
                bar.transform.localScale = new Vector3(0.40f, 0.08f, 1f);
                enemyBars.Add(bar);
                enemyFills.Add(CreateQuad("BattleEnemyFill", barMaterial, 0.3f));
            }
        }

        // ---- entrances on the projection layer -----------------------------------------------
        public void SetSpawnMarkers(IReadOnlyList<GridPoint> cells)
        {
            int count = cells != null ? cells.Count : 0;
            while (spawnMarkers.Count < count)
                spawnMarkers.Add(CreateQuad("BattleEntranceMarker", ringMaterial, 0.8f));
            for (int i = 0; i < spawnMarkers.Count; i++)
            {
                bool active = i < count;
                spawnMarkers[i].SetActive(active);
                if (!active) continue;
                GridPoint cell = cells[i];
                spawnMarkers[i].transform.position = World(BattleLevel.PhysicalCenter(cell));
                SetColor(spawnMarkers[i], new Color(0.2f, 0.95f, 1f, 0.9f));
            }
        }

        public void ClearSpawnMarkers()
        {
            foreach (GameObject marker in spawnMarkers) if (marker != null) marker.SetActive(false);
            spawnMarkers.Clear();
        }

        // ---- shot / hit / core / walls ------------------------------------------------------
        private void HandleShots()
        {
            if (lastShotSequence == controller.ShotSequence) return;
            lastShotSequence = controller.ShotSequence;
            IReadOnlyList<ShotEvent> shots = controller.LastShots;
            if (shots == null) return;
            foreach (ShotEvent shot in shots)
            {
                celebrations[shot.TowerId] = animationTime + 0.4f;
                if (!shot.Killed) hitTimes[shot.EnemyId] = animationTime;
                else hitTimes.Remove(shot.EnemyId);
                Vector3 position = WorldAt(shot.TargetX, shot.TargetY);
                position.y = SurfaceHeight(position.x, position.z);
                if (shot.Killed && enemyBurstMaterial != null)
                {
                    SpawnEffect(position, EnemySize, 0.95f, Color.white, 0.34f, enemyBurstMaterial);
                    continue;
                }
                for (int i = 0; i < 4; i++)
                {
                    float angle = (i + 0.5f) * Mathf.PI * 0.5f;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    GameObject quad = CreateQuad("BubbleSplash", bubbleMaterial, 0.16f);
                    quad.transform.position = position + direction * 0.15f;
                    effects.Add(new Fx { Quad = quad, Life = 0.32f, StartScale = 0.18f,
                        EndScale = 0.06f, Color = new Color(0.58f, 1f, 0.96f, 1f),
                        Velocity = direction * 1.3f });
                }
            }
        }

        private void HandleCore(BattleState state)
        {
            if (lastCoreHealth < 0) { lastCoreHealth = state.CoreHealth; return; }
            if (state.CoreHealth >= lastCoreHealth) { lastCoreHealth = state.CoreHealth; return; }
            lastCoreHealth = state.CoreHealth;
            projector?.PlayCoreHit();
            BattleLevel level = controller.Level;
            if (level == null) return;
            for (int i = 0; i < level.Cells.Count; i++)
            {
                if (!level.Cells[i].IsCoreCenter) continue;
                float height = controller.Round != null ? 0f : 1f;
                SpawnEffect(World(level.Cells[i].Position, height),
                    0.6f, 3.2f, new Color(1f, 0.3f, 0.35f, 0.95f), rippleSeconds * 1.4f, ringMaterial);
                break;
            }
        }

        private void SpawnEffect(Vector3 position, float startScale, float endScale, Color color, float life, Material material)
        {
            GameObject quad = CreateQuad("BattleFx", material, 1f);
            quad.SetActive(true);
            quad.transform.position = position;
            var fx = new Fx { Quad = quad, Age = 0f, Life = life, StartScale = startScale, EndScale = endScale, Color = color };
            ApplyFx(fx, 0f);
            effects.Add(fx);
        }

        private void UpdateEffects()
        {
            float delta = controller.State.Paused ? 0f : Time.deltaTime;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Fx fx = effects[i];
                if (fx.Quad == null) { effects.RemoveAt(i); continue; }
                fx.Age += delta;
                fx.Quad.transform.position += fx.Velocity * delta;
                fx.Quad.SetActive(true);
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
        private static Vector3 WorldAt(double x, double y, float height = 0f) =>
            new Vector3((float)x / BattleLevel.NavigationPerPhysical, height,
                (float)y / BattleLevel.NavigationPerPhysical);
        private static Vector3 BrickCenter(BattleCell cell, float height) =>
            new Vector3(cell.Position.X / 2 + 0.5f, height, cell.Position.Y / 2 + 0.5f);

        private void SetColor(GameObject quad, Color color)
        {
            var renderer = quad.GetComponent<Renderer>();
            if (renderer == null) return;
            properties.Clear();
            properties.SetColor(ColorId, color);
            renderer.SetPropertyBlock(properties);
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
            // All content lies on its intended receiving plane. Order transparent layers without lifting geometry.
            switch (name)
            {
                case "BattleRange": renderer.sortingOrder = 1; break;
                case "BattleEntranceMarker": renderer.sortingOrder = 2; break;
                case "OctopusArm": renderer.sortingOrder = 3; break;
                case "OctopusFace": renderer.sortingOrder = 4; break;
                case "BattleEnemy": renderer.sortingOrder = 6; break;
                case "BattleEnemyBar": renderer.sortingOrder = 7; break;
                case "BattleEnemyFill": renderer.sortingOrder = 8; break;
                case "BattleBubble": renderer.sortingOrder = 10; break;
                case "BattleFx": case "BubbleSplash": renderer.sortingOrder = 11; break;
            }
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.forceRenderingOff = true;
            var collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return quad;
        }

        private void BuildMaterials()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            ringMaterial = new Material(shader) { name = "BattleRing", mainTexture = MakeRingTexture(64) };
            Shader fishShader = Resources.Load<Shader>("ProjectedLight/Pufferfish");
            if (fishShader != null)
            {
                enemyMaterial = new Material(fishShader) { name = "Pufferfish" };
                enemyBurstMaterial = new Material(fishShader) { name = "PufferfishBurst" };
                enemyBurstMaterial.SetFloat("_Burst", 1f);
            }
            else Debug.LogError("缺少河豚敌人投影材质。", this);
            barMaterial = new Material(shader) { name = "BattleBar", mainTexture = Texture2D.whiteTexture };
            Shader currentShader = Resources.Load<Shader>("ProjectedLight/OceanCurrent");
            if (currentShader != null) pathMaterial = new Material(currentShader) { name = "OceanCurrentLane" };
            rangeMaterial = new Material(shader) { name = "BattleRangeGlow", mainTexture = MakeRangeTexture(128) };
            armMaterial = new Material(shader) { name = "OctopusArm", mainTexture = MakeArmTexture() };
            bubbleMaterial = new Material(shader) { name = "OctopusBubble", mainTexture = MakeBubbleTexture() };
            faceMaterials = new Material[17];
            for (int i = 0; i < faceMaterials.Length; i++)
                faceMaterials[i] = new Material(shader) { name = "OctopusFace" + i, mainTexture = MakeFaceTexture(i) };
        }

        private static Texture2D PaintTexture(int size, Func<Vector2, Color> paint)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = paint(new Vector2((2f * x + 1f) / size - 1f, (2f * y + 1f) / size - 1f));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D MakeRangeTexture(int size)
        {
            return PaintTexture(size * 2, point =>
            {
                float angle = Mathf.Atan2(point.y, point.x);
                float nearest = Mathf.Round(angle * 16f / Mathf.PI) * Mathf.PI / 16f;
                Vector2 center = new Vector2(Mathf.Cos(nearest), Mathf.Sin(nearest)) * 0.96f;
                float edge = Mathf.Abs((point - center).magnitude - 0.031f);
                return new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.006f, 0.015f, edge)));
            });
        }

        private static Texture2D MakeArmTexture()
        {
            var path = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                path.Add(new Vector2(Mathf.Lerp(-0.78f, 0.24f, t), -0.22f + 0.12f * Mathf.Sin(t * Mathf.PI)));
            }
            for (int i = 1; i <= 44; i++)
            {
                float t = i / 44f;
                float angle = -Mathf.PI * 0.5f + t * Mathf.PI * 1.85f;
                path.Add(new Vector2(0.24f, 0.23f) +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Lerp(0.45f, 0.10f, t));
            }
            return PaintTexture(96, point =>
            {
                float distance = float.MaxValue;
                for (int i = 0; i < path.Count; i++)
                    distance = Mathf.Min(distance, (point - path[i]).magnitude + i * 0.0016f);
                Color color = Color.Lerp(new Color(1f, 0.56f, 0.38f), new Color(0.68f, 0.24f, 0.25f),
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.13f, 0.18f, distance)));
                color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.21f, distance));
                return color;
            });
        }

        private static Texture2D MakeBubbleTexture()
        {
            return PaintTexture(64, point =>
            {
                float radius = point.magnitude;
                if ((point - new Vector2(-0.28f, 0.30f)).magnitude < 0.17f) return Color.white;
                if (radius > 0.84f) return Color.clear;
                return radius > 0.66f ? new Color(0.58f, 1f, 0.96f) : new Color(0.13f, 0.65f, 0.76f, 0.77f);
            });
        }

        private static Texture2D MakeFaceTexture(int expression)
        {
            float blow = expression <= 8 ? expression / 8f : 0f;
            float blink = expression > 8 ? (expression - 8) / 8f : 0f;
            return PaintTexture(96, point =>
            {
                float x = Mathf.Abs(point.x);
                float y = point.y;
                Vector2 corner = new Vector2(Mathf.Max(0f, x - 0.74f), Mathf.Max(0f, Mathf.Abs(y) - 0.74f));
                if (corner.magnitude > 0.24f) return Color.clear;
                Color ink = new Color(0.24f, 0.15f, 0.19f);
                Vector2 eye = new Vector2((x - 0.33f) / 0.13f,
                    (y + 0.12f) / Mathf.Lerp(0.155f, 0.034f, blink));
                if (eye.sqrMagnitude < 1f)
                {
                    Vector2 highlight = new Vector2(point.x - Mathf.Sign(point.x) * 0.33f - 0.04f, y + 0.075f);
                    return blink < 0.55f && highlight.magnitude < 0.038f ? Color.white : ink;
                }

                float mouthWidth = Mathf.Lerp(0.18f, 0.08f, blow);
                float mouthX = x / mouthWidth;
                float mouthCenter = Mathf.Lerp(-0.32f - 0.06f * (1f - mouthX * mouthX), -0.33f, blow);
                float mouthHeight = Mathf.Lerp(0.10f, 0.13f, blow) + blink * 0.02f;
                if (new Vector2(mouthX, (y - mouthCenter) / mouthHeight).sqrMagnitude < 1f) return ink;
                if (new Vector2((x - 0.50f) / (0.13f + 0.03f * blow), (y + 0.35f) / 0.07f).sqrMagnitude < 1f)
                    return new Color32(255, 171, 134, 255);
                if (new Vector2(x - 0.45f, Mathf.Abs(y) - 0.44f).magnitude < 0.20f)
                    return new Color32(255, 167, 98, 255);
                if (x > 0.88f || Mathf.Abs(y) > 0.88f) return new Color32(173, 81, 45, 255);
                return new Color32(233, 130, 62, 255);
            });
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
