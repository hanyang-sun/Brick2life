using System;
using System.Collections.Generic;
using System.Globalization;
using Brick2Life.Application;
using Brick2Life.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brick2Life.Feedback
{
    /// <summary>
    /// Fixed round targets in the editing view. These guides are not observations or projection marks;
    /// the overhead RGB camera must exclude their dedicated layer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationRoundMarkers : MonoBehaviour
    {
        public const int LayerIndex = 29;
        private const int TextureSize = 96;
        private const float SurfaceHeight = 0.025f;

        private sealed class Marker
        {
            public GameObject Object;
            public Material Material;
            public Texture2D Texture;
        }

        private Transform markersRoot;
        private Mesh quadMesh;
        private Marker coreMarker;
        private readonly List<Marker> spawnMarkers = new List<Marker>();

        public Transform MarkersRoot => markersRoot;
        public GeneratedRound Round { get; private set; }

        private void Awake() => EnsureRoot();
        private void OnEnable()
        {
            if (markersRoot != null) markersRoot.gameObject.SetActive(Round != null);
        }
        private void OnDisable()
        {
            if (markersRoot != null) markersRoot.gameObject.SetActive(false);
        }

        public void Render(GeneratedRound round)
        {
            EnsureRoot();
            Round = round;
            markersRoot.gameObject.SetActive(round != null && isActiveAndEnabled);
            if (round == null) return;

            if (coreMarker == null)
                coreMarker = CreateMarker("SystemCore", new Color(0.25f, 0.65f, 1f), 0);
            Place(coreMarker, round.Core);

            while (spawnMarkers.Count > round.Spawns.Count)
            {
                int last = spawnMarkers.Count - 1;
                ReleaseMarker(spawnMarkers[last]);
                spawnMarkers.RemoveAt(last);
            }
            while (spawnMarkers.Count < round.Spawns.Count)
            {
                int number = spawnMarkers.Count + 1;
                spawnMarkers.Add(CreateMarker("SystemSpawn_" + number, new Color(1f, 0.76f, 0.18f), number));
            }
            for (int i = 0; i < spawnMarkers.Count; i++) Place(spawnMarkers[i], round.Spawns[i]);
        }

        private void EnsureRoot()
        {
            if (markersRoot != null) return;
            markersRoot = new GameObject("SystemRoundMarkers").transform;
            markersRoot.SetParent(transform, false);
            markersRoot.gameObject.layer = LayerIndex;
            markersRoot.gameObject.SetActive(false);
            quadMesh = new Mesh { name = "RoundMarkerQuad", hideFlags = HideFlags.HideAndDontSave };
            quadMesh.vertices = new[]
            {
                new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f),
                new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f)
            };
            quadMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quadMesh.RecalculateNormals();
            quadMesh.RecalculateBounds();
        }

        private Marker CreateMarker(string name, Color color, int number)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("缺少系统目标提示所需的 Sprites/Default Shader。");
            Texture2D texture = MakeTexture(number);
            var material = new Material(shader)
            {
                name = name + "Material", mainTexture = texture, color = color,
                hideFlags = HideFlags.HideAndDontSave
            };
            var instance = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            instance.transform.SetParent(markersRoot, false);
            instance.layer = LayerIndex;
            instance.GetComponent<MeshFilter>().sharedMesh = quadMesh;
            var renderer = instance.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Constructing a mesh directly avoids even a one-frame physics collider on fixed targets.
            return new Marker { Object = instance, Material = material, Texture = texture };
        }

        private static void Place(Marker marker, GridPoint point)
        {
            marker.Object.transform.position = new Vector3(point.X + 0.5f, SurfaceHeight, point.Y + 0.5f);
            marker.Object.transform.localScale = new Vector3(0.88f, 1f, 0.88f);
        }

        private static Texture2D MakeTexture(int number)
        {
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = (x + 0.5f) / TextureSize - 0.5f;
                    float v = (y + 0.5f) / TextureSize - 0.5f;
                    float distance = Mathf.Sqrt(u * u + v * v);
                    float alpha = 1f - Mathf.SmoothStep(0.02f, 0.035f, Mathf.Abs(distance - 0.415f));
                    if (number == 0)
                    {
                        if ((Mathf.Abs(u) < 0.035f && Mathf.Abs(v) < 0.23f) ||
                            (Mathf.Abs(v) < 0.035f && Mathf.Abs(u) < 0.23f)) alpha = 1;
                    }
                    else if (v > 0.17f && v < 0.33f && Mathf.Abs(u) < (0.33f - v) * 0.8f)
                        alpha = 1;
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255));
                }
            if (number > 0) DrawNumber(pixels, number);
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = number == 0 ? "SystemCoreGuide" : "SystemSpawnGuide_" + number,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // Compact bitmap digits keep labels deterministic and independent of installed fonts.
        private static void DrawNumber(Color32[] pixels, int number)
        {
            string[] digits =
            {
                "01110100011001110101110011000101110", "00100011000010000100001000010001110",
                "01110100010000100010001000100011111", "11110000010000101110000010000111110",
                "00010001100101010010111110001000010", "11111100001111000001000011000101110",
                "00110010001000011110100011000101110", "11111000010001000100010000100001000",
                "01110100011000101110100011000101110", "01110100011000101111000010001001100"
            };
            string label = number.ToString(CultureInfo.InvariantCulture);
            int scale = Mathf.Max(1, Mathf.Min(4, 50 / (label.Length * 6)));
            int startX = (TextureSize - (label.Length * 6 - 1) * scale) / 2;
            int startY = 25;
            for (int digit = 0; digit < label.Length; digit++)
                for (int row = 0; row < 7; row++)
                    for (int column = 0; column < 5; column++)
                    {
                        if (digits[label[digit] - '0'][row * 5 + column] != '1') continue;
                        for (int by = 0; by < scale; by++)
                            for (int bx = 0; bx < scale; bx++)
                            {
                                int x = startX + (digit * 6 + column) * scale + bx;
                                int y = startY + (6 - row) * scale + by;
                                if (x >= 0 && x < TextureSize && y >= 0 && y < TextureSize)
                                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, 255);
                            }
                    }
        }

        private static void ReleaseMarker(Marker marker)
        {
            if (marker == null) return;
            if (marker.Object != null) marker.Object.SetActive(false);
            Release(marker.Object); Release(marker.Material); Release(marker.Texture);
        }

        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            ReleaseMarker(coreMarker);
            foreach (Marker marker in spawnMarkers) ReleaseMarker(marker);
            spawnMarkers.Clear();
            Release(quadMesh);
            if (markersRoot != null) Release(markersRoot.gameObject);
        }
    }
}
