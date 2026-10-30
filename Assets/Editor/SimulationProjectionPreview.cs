using System;
using System.Collections.Generic;
using Brick2Life.Application;
using Brick2Life.Core;
using Brick2Life.Feedback;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Brick2Life.Editor
{
    // Owns an isolated preview scene. Source geometry, physics and capture cameras are never changed.
    public sealed class SimulationProjectionPreview : IDisposable
    {
        private readonly PreviewRenderUtility preview;
        private readonly Transform root, boardRoot, brickRoot, projectionRoot, battleRoot, objectiveRoot;
        private readonly Dictionary<int, MeshRenderer> objectiveCopies = new Dictionary<int, MeshRenderer>();
        private readonly Dictionary<string, Material> brickMaterials = new Dictionary<string, Material>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly Dictionary<int, MeshRenderer> projectionCopies = new Dictionary<int, MeshRenderer>();
        private readonly Dictionary<int, MeshRenderer> battleCopies = new Dictionary<int, MeshRenderer>();
        private readonly Dictionary<int, LineRenderer> routeCopies = new Dictionary<int, LineRenderer>();
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly List<int> stale = new List<int>();
        private readonly List<GameObject> bricks = new List<GameObject>();
        private RenderTexture texture;
        private LevelState renderedLevel;
        private Vector3 center = new Vector3(5, 0.4f, 5);
        private float yaw = 38, pitch = 55, distance = 22;
        private bool disposed;

        public Camera Camera => preview.camera;
        public int BrickCount => bricks.Count;
        public bool BricksVisible => brickRoot.gameObject.activeSelf;
        public int ProjectionCount => projectionCopies.Count;
        public long DisplayedVersion => renderedLevel == null ? -1 : renderedLevel.SourceVersion;
        public RenderTexture Texture => texture;

        public SimulationProjectionPreview(IReadOnlyList<SimulationBoardSession.PaletteEntry> palette)
        {
            preview = new PreviewRenderUtility();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.018f, 0.025f, 0.035f);
            Camera.nearClipPlane = 0.05f;
            Camera.farClipPlane = 150;
            Camera.fieldOfView = 43;
            Camera.allowHDR = false;
            preview.lights[0].color = Color.white;
            preview.lights[0].intensity = 1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(55, 35, 0);
            preview.lights[1].color = Color.white;
            preview.lights[1].intensity = 0.38f;
            preview.lights[1].transform.rotation = Quaternion.Euler(30, -140, 0);
            foreach (Light light in preview.lights) light.enabled = true;
            root = new GameObject("ProjectionPreview").transform;
            root.gameObject.hideFlags = HideFlags.HideAndDontSave;
            preview.AddSingleGO(root.gameObject);
            boardRoot = Child("Board");
            brickRoot = Child("ConfirmedBricks");
            projectionRoot = Child("Projection");
            battleRoot = Child("Battle");
            objectiveRoot = Child("SystemObjectives");
            Material boardMaterial = Material(new Color(0.055f, 0.07f, 0.085f));
            GameObject floor = Cube(boardRoot, "Board", boardMaterial);
            floor.transform.position = new Vector3(5, -0.035f, 5);
            floor.transform.localScale = new Vector3(10, 0.04f, 10);
            Material grid = Material(new Color(0.16f, 0.20f, 0.24f));
            Material edge = Material(new Color(0.34f, 0.43f, 0.49f));
            for (int i = 0; i <= 10; i++)
            {
                GridLine(new Vector3(i, 0, 0), new Vector3(i, 0, 10), i == 0 || i == 10 ? edge : grid, i == 0 || i == 10 ? 0.035f : 0.012f);
                GridLine(new Vector3(0, 0, i), new Vector3(10, 0, i), i == 0 || i == 10 ? edge : grid, i == 0 || i == 10 ? 0.035f : 0.012f);
            }
            if (palette != null)
                foreach (SimulationBoardSession.PaletteEntry entry in palette)
                    if (!string.IsNullOrWhiteSpace(entry.colorId) && entry.material != null)
                        brickMaterials[entry.colorId] = PlasticMaterial(entry.material.color);
            PositionCamera();
        }

        private Transform Child(string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(root, false);
            return child;
        }

        private Material Material(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("缺少预览所需的基础 Shader。");
            var material = new Material(shader) { color = color, hideFlags = HideFlags.HideAndDontSave };
            ownedMaterials.Add(material);
            return material;
        }

        private Material PlasticMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("缺少积木预览所需的 Standard Shader。");
            var material = new Material(shader)
            {
                name = "PreviewBrickPlastic", color = color, hideFlags = HideFlags.HideAndDontSave
            };
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Glossiness", 0.22f);
            ownedMaterials.Add(material);
            return material;
        }

        private static GameObject Cube(Transform parent, string name, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private void GridLine(Vector3 from, Vector3 to, Material material, float width)
        {
            var go = new GameObject("GridLine");
            go.transform.SetParent(boardRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from); line.SetPosition(1, to);
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        public void UpdateScene(LevelState level, ProjectionSimulator projection, SimulationBattleView battle, bool projectionOnly)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SimulationProjectionPreview));
            if (!ReferenceEquals(renderedLevel, level))
            {
                foreach (GameObject go in bricks) Object.DestroyImmediate(go);
                bricks.Clear();
                renderedLevel = level;
                if (level != null)
                    for (int y = 0; y < level.Definition.Height; y++)
                        for (int x = 0; x < level.Definition.Width; x++)
                        {
                            LevelCell cell = level.At(x, y);
                            if (!cell.Occupied) continue;
                            if (!brickMaterials.TryGetValue(cell.ColorId, out Material material))
                            {
                                material = PlasticMaterial(Color.gray);
                                brickMaterials[cell.ColorId] = material;
                            }
                            GameObject go = Cube(brickRoot, "ConfirmedBrick_" + x + "_" + y, material);
                            go.transform.position = new Vector3(x + 0.5f, 0.5f, y + 0.5f);
                            bricks.Add(go);
                        }
            }
            brickRoot.gameObject.SetActive(!projectionOnly);
            bool confirmedProjection = level != null && projection != null && projection.LastPlan != null &&
                projection.LastPlan.LevelVersion == level.SourceVersion;
            projectionRoot.gameObject.SetActive(confirmedProjection && projection.IsVisible);
            var objectives = battle != null ? battle.GetComponent<SimulationRoundMarkers>() : null;
            // System goals are public configuration; before RGB confirmation show only their guides.
            bool showObjectiveGuides = !confirmedProjection || (!projectionOnly && !projection.IsVisible);
            objectiveRoot.gameObject.SetActive(showObjectiveGuides);
            MirrorMeshes(showObjectiveGuides && objectives != null ? objectives.MarkersRoot : null,
                objectiveRoot, objectiveCopies);
            var overlay = battle != null ? battle.GetComponent<SimulationBattleOverlay>() : null;
            MirrorMeshes(confirmedProjection ? projection.MarksRoot : null, projectionRoot, projectionCopies,
                confirmedProjection && overlay != null ? overlay.MarksRoot : null);
            battleRoot.gameObject.SetActive(!projectionOnly);
            MirrorMeshes(battle != null ? battle.transform : null, battleRoot, battleCopies,
                excludedLayer: projection != null ? projection.ProjectionLayer : 31,
                additionalExcludedLayer: SimulationRoundMarkers.LayerIndex);
            MirrorRoutes(battle != null ? battle.transform : null);
        }

        private void MirrorMeshes(Transform sourceRoot, Transform parent, Dictionary<int, MeshRenderer> copies,
            Transform additionalRoot = null, int excludedLayer = -1, int additionalExcludedLayer = -1)
        {
            stale.Clear();
            stale.AddRange(copies.Keys);
            foreach (Transform sourceGroup in new[] { sourceRoot, additionalRoot })
            if (sourceGroup != null)
                foreach (MeshRenderer source in sourceGroup.GetComponentsInChildren<MeshRenderer>())
                {
                    MeshFilter filter = source.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || !source.enabled || source.forceRenderingOff ||
                        source.gameObject.layer == excludedLayer || source.gameObject.layer == additionalExcludedLayer) continue;
                    int id = source.GetInstanceID();
                    stale.Remove(id);
                    if (!copies.TryGetValue(id, out MeshRenderer target))
                    {
                        var go = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                        go.transform.SetParent(parent, false);
                        target = go.GetComponent<MeshRenderer>();
                        target.shadowCastingMode = ShadowCastingMode.Off;
                        target.receiveShadows = false;
                        copies.Add(id, target);
                    }
                    target.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    target.sharedMaterials = source.sharedMaterials;
                    source.GetPropertyBlock(properties);
                    target.SetPropertyBlock(properties);
                    target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                    target.transform.localScale = source.transform.lossyScale;
                }
            foreach (int id in stale) { Object.DestroyImmediate(copies[id].gameObject); copies.Remove(id); }
        }

        private void MirrorRoutes(Transform sourceRoot)
        {
            stale.Clear(); stale.AddRange(routeCopies.Keys);
            if (sourceRoot != null)
                foreach (LineRenderer source in sourceRoot.GetComponentsInChildren<LineRenderer>())
                {
                    if (!source.enabled) continue;
                    int id = source.GetInstanceID(); stale.Remove(id);
                    if (!routeCopies.TryGetValue(id, out LineRenderer target))
                    {
                        var go = new GameObject(source.name, typeof(LineRenderer));
                        go.transform.SetParent(battleRoot, false);
                        target = go.GetComponent<LineRenderer>(); routeCopies.Add(id, target);
                    }
                    target.useWorldSpace = true;
                    target.positionCount = source.positionCount;
                    for (int i = 0; i < source.positionCount; i++)
                        target.SetPosition(i, source.useWorldSpace ? source.GetPosition(i) : source.transform.TransformPoint(source.GetPosition(i)));
                    target.widthMultiplier = source.widthMultiplier;
                    target.sharedMaterials = source.sharedMaterials;
                    target.startColor = source.startColor; target.endColor = source.endColor;
                    target.shadowCastingMode = ShadowCastingMode.Off;
                }
            foreach (int id in stale) { Object.DestroyImmediate(routeCopies[id].gameObject); routeCopies.Remove(id); }
        }

        public void Orbit(Vector2 delta)
        {
            yaw += delta.x * 0.35f;
            pitch = Mathf.Clamp(pitch - delta.y * 0.35f, 15, 85);
            PositionCamera();
        }

        public void Zoom(float steps)
        {
            distance = Mathf.Clamp(distance * Mathf.Exp(steps * 0.08f), 7, 38);
            PositionCamera();
        }

        public void ResetView()
        {
            yaw = 38; pitch = 55; distance = 22;
            PositionCamera();
        }

        private void PositionCamera()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            float fittedDistance = distance * Mathf.Max(1, 1 / Mathf.Max(0.1f, Camera.aspect));
            Camera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * fittedDistance, rotation);
        }

        public RenderTexture Render(int width, int height)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SimulationProjectionPreview));
            width = Mathf.Clamp(width, 64, 2048); height = Mathf.Clamp(height, 64, 2048);
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
                texture = new RenderTexture(width, height, 24) { hideFlags = HideFlags.HideAndDontSave, name = "ProjectionDisplay" };
                texture.Create();
            }
            Camera.targetTexture = texture;
            Camera.aspect = (float)width / height;
            PositionCamera();
            Camera.Render();
            return texture;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Camera.targetTexture = null;
            if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
            preview.Cleanup();
            foreach (Material material in ownedMaterials) Object.DestroyImmediate(material);
        }
    }
}
