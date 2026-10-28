using System;
using System.Collections.Generic;
using Brick2Life.Battle;
using Brick2Life.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Draws the battle: one polyline per entrance route plus pooled enemy cubes. Everything lives on the
    /// game layer so the overhead recognition camera never sees it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationBattleView : MonoBehaviour
    {
        [SerializeField] private Transform root;
        [SerializeField] private Material enemyMaterial;
        [SerializeField] private Material routeMaterial;
        [SerializeField, Min(0.05f)] private float enemyScale = 0.45f;
        [SerializeField, Min(0.1f)] private float routeWidth = 0.08f;
        [SerializeField] private string gameLayerName = "Brick2LifeGame";

        private readonly List<GameObject> enemyObjects = new List<GameObject>();
        private readonly List<LineRenderer> routeLines = new List<LineRenderer>();
        private readonly List<GameObject> spawnMarkers = new List<GameObject>();
        private int gameLayer = 30;

        public int EnemyCount => enemyObjects.Count;
        public int RouteCount => routeLines.Count;
        public int SpawnMarkerCount => spawnMarkers.Count;

        private void Awake()
        {
            gameLayer = ResolveLayer();
            if (root == null) root = transform;
        }

        public void Build(BattleLevel level)
        {
            ClearRoutes();
            if (level == null) return;
            if (root == null) root = transform;
            foreach (BattleRoute route in level.Routes)
            {
                var lineObject = new GameObject("BattleRoute");
                lineObject.layer = gameLayer;
                lineObject.transform.SetParent(root, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.widthMultiplier = routeWidth;
                line.positionCount = route.Route.Count;
                for (int i = 0; i < route.Route.Count; i++)
                    line.SetPosition(i, World(route.Route[i]));
                if (routeMaterial != null) line.sharedMaterial = routeMaterial;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                routeLines.Add(line);
            }
        }

        public void Render(BattleState state, BattleLevel level)
        {
            if (state == null) return;
            EnsurePool(state.Enemies.Count);
            for (int i = 0; i < enemyObjects.Count; i++)
            {
                bool active = i < state.Enemies.Count;
                enemyObjects[i].SetActive(active);
                if (!active) continue;
                EnemyState enemy = state.Enemies[i];
                enemyObjects[i].transform.position = World(enemy.X, enemy.Y, 0.5f);
                float tint = state.Enemies.Count == 0 ? 1f : Mathf.Clamp01(enemy.Health / (float)Mathf.Max(1, enemy.Health));
                var renderer = enemyObjects[i].GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.Lerp(new Color(0.75f, 0.15f, 0.15f), new Color(1f, 0.45f, 0.2f), tint);
            }
        }

        public void ClearEnemies()
        {
            foreach (GameObject item in enemyObjects) if (item != null) Destroy(item);
            enemyObjects.Clear();
        }

        /// <summary>Shows a flat ring on every chosen entrance so the pick is visible on the board.</summary>
        public void SetSpawns(IReadOnlyList<GridPoint> cells)
        {
            if (cells == null) cells = Array.Empty<GridPoint>();
            EnsureSpawnPool(cells.Count);
            for (int i = 0; i < spawnMarkers.Count; i++)
            {
                bool active = i < cells.Count;
                spawnMarkers[i].SetActive(active);
                if (!active) continue;
                GridPoint cell = cells[i];
                spawnMarkers[i].transform.position = new Vector3(cell.X + 0.5f, 0.03f, cell.Y + 0.5f);
            }
        }

        public void ClearSpawns()
        {
            foreach (GameObject marker in spawnMarkers) if (marker != null) Destroy(marker);
            spawnMarkers.Clear();
        }

        private void EnsureSpawnPool(int count)
        {
            while (spawnMarkers.Count < count)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "BattleEntrance";
                marker.layer = gameLayer;
                marker.transform.SetParent(root != null ? root : transform, false);
                marker.transform.localScale = new Vector3(0.7f, 0.02f, 0.7f);
                var renderer = marker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Sprites/Default"));
                    renderer.material.color = new Color(0.2f, 0.9f, 1f, 0.85f);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                var collider = marker.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                spawnMarkers.Add(marker);
            }
        }

        public void ClearRoutes()
        {
            foreach (LineRenderer line in routeLines) if (line != null) Destroy(line.gameObject);
            routeLines.Clear();
        }

        private void EnsurePool(int count)
        {
            while (enemyObjects.Count < count)
            {
                GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                enemy.name = "BattleEnemy";
                enemy.layer = gameLayer;
                enemy.transform.SetParent(root, false);
                enemy.transform.localScale = Vector3.one * enemyScale;
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // Instanced material so per-enemy health tint does not leak into other enemies.
                    renderer.material = enemyMaterial != null ? new Material(enemyMaterial) : renderer.material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                var collider = enemy.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                enemyObjects.Add(enemy);
            }
        }

        // Battle grid is 20×20 over a 10×10 world, so one battle cell is half a world unit.
        private static Vector3 World(GridPoint cell) => new Vector3(cell.X * 0.5f, 1.05f, cell.Y * 0.5f);
        private static Vector3 World(double x, double y, float height) => new Vector3((float)x * 0.5f, height, (float)y * 0.5f);

        private int ResolveLayer()
        {
            int layer = string.IsNullOrWhiteSpace(gameLayerName) ? -1 : LayerMask.NameToLayer(gameLayerName);
            return layer >= 0 ? layer : 30;
        }
    }
}
