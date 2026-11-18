using System.Collections.Generic;
using Brick2Life.Battle;
using Brick2Life.Core;
using Unity.Collections;
using UnityEngine;

namespace Brick2Life.Feedback
{
    // A single field for the whole board. Shared paths write the nearest sample once.
    internal static class OceanCurrentMap
    {
        public const int Resolution = 512;
        private const float HalfWidth = 0.17f;
        private const float CornerTrim = 0.22f;

        public static void Bake(Texture2D texture, Vector2 boardSize, IReadOnlyList<BattleRoute> routes)
        {
            NativeArray<Color> field = texture.GetRawTextureData<Color>();
            for (int i = 0; i < field.Length; i++) field[i] = new Color(0f, 0f, 2f, 0f);
            var points = new List<Vector2>();
            Vector2 pixelSize = boardSize / Resolution;
            foreach (BattleRoute route in routes)
            {
                if (route.Route.Count < 2) continue;
                RoundCorners(route.Route, points);
                float remaining = 0f;
                for (int i = points.Count - 2; i >= 0; i--)
                    remaining += PaintSegment(field, pixelSize, points[i], points[i + 1], remaining);
            }
            texture.Apply(false, false);
        }

        private static Vector2 BoardPoint(GridPoint point) =>
            new Vector2(point.X, point.Y) / BattleLevel.NavigationPerPhysical;

        private static void RoundCorners(IReadOnlyList<GridPoint> route, List<Vector2> points)
        {
            points.Clear();
            points.Add(BoardPoint(route[0]));
            for (int i = 1; i < route.Count - 1; i++)
            {
                Vector2 corner = BoardPoint(route[i]);
                Vector2 incoming = corner - BoardPoint(route[i - 1]);
                Vector2 outgoing = BoardPoint(route[i + 1]) - corner;
                if (Mathf.Abs(Vector2.Dot(incoming.normalized, outgoing.normalized)) > 0.99f)
                {
                    points.Add(corner);
                    continue;
                }

                // Stay inside the corner cell, with the centreline tangent to both straight legs.
                float trim = Mathf.Min(CornerTrim, 0.45f * Mathf.Min(incoming.magnitude, outgoing.magnitude));
                Vector2 start = corner - incoming.normalized * trim;
                Vector2 end = corner + outgoing.normalized * trim;
                const int subdivisions = 6;
                for (int j = 0; j <= subdivisions; j++)
                {
                    float t = j / (float)subdivisions;
                    points.Add(Vector2.Lerp(Vector2.Lerp(start, corner, t), Vector2.Lerp(corner, end, t), t));
                }
            }
            points.Add(BoardPoint(route[route.Count - 1]));
        }

        private static float PaintSegment(NativeArray<Color> field, Vector2 pixelSize, Vector2 from, Vector2 to, float remaining)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.0001f) return 0f;

            Vector2 forward = delta / length;
            Vector2 side = new Vector2(-forward.y, forward.x);
            float margin = HalfWidth + Mathf.Max(pixelSize.x, pixelSize.y) * 1.5f;
            int left = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.x, to.x) - margin) / pixelSize.x));
            int right = Mathf.Min(Resolution - 1, Mathf.CeilToInt((Mathf.Max(from.x, to.x) + margin) / pixelSize.x));
            int bottom = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.y, to.y) - margin) / pixelSize.y));
            int top = Mathf.Min(Resolution - 1, Mathf.CeilToInt((Mathf.Max(from.y, to.y) + margin) / pixelSize.y));
            for (int y = bottom; y <= top; y++)
                for (int x = left; x <= right; x++)
                {
                    Vector2 relative = new Vector2((x + 0.5f) * pixelSize.x, (y + 0.5f) * pixelSize.y) - from;
                    float along = Mathf.Clamp(Vector2.Dot(relative, forward), 0f, length);
                    float distance = (relative - forward * along).magnitude;
                    int index = y * Resolution + x;
                    float normalizedDistance = distance / HalfWidth;
                    if (distance > margin || normalizedDistance >= field[index].b) continue;

                    // Measure from the destination so incoming branches agree along a shared tail.
                    field[index] = new Color(remaining + length - along,
                        Vector2.Dot(relative, side) / HalfWidth, normalizedDistance, 1f);
                }
            return length;
        }
    }
}
