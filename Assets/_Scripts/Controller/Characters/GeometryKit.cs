using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shared mesh arithmetic for the character generators: smooth kernels, orthonormal frames,
    /// lofts, grids and normal recomputation. Everything here clamps before it powers — in
    /// float32 <c>Sin(PI)</c> is negative and <c>Pow(negative, fractional)</c> is NaN, and one NaN
    /// vertex poisons a whole mesh's bounds.
    /// </summary>
    public static class GeometryKit
    {
        public const float Tau = Mathf.PI * 2f;

        public static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);

        public static float Smooth(float x)
        {
            x = Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        public static float SmoothStep(float a, float b, float x) =>
            Smooth(b - a == 0f ? (x >= b ? 1f : 0f) : (x - a) / (b - a));

        /// <summary>C1 bell: 1 at d=0, 0 at d≥1, zero slope at both ends.</summary>
        public static float Bell(float d)
        {
            d = d < 0f ? -d : d;
            if (d >= 1f) return 0f;
            float s = 1f - d * d;
            return s * s;
        }

        /// <summary>Safe power: base clamped to [0, ∞) first.</summary>
        public static float SafePow(float x, float e) => Mathf.Pow(x < 0f ? 0f : x, e);

        public static float SafeSqrt(float x) => x <= 0f ? 0f : Mathf.Sqrt(x);

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Signed dial in [-1,1] → a multiplier: 1 at 0, lo at -1, hi at +1 (piecewise linear).</summary>
        public static float Dial(float axis, float lo, float hi)
        {
            axis = axis < -1f ? -1f : (axis > 1f ? 1f : axis);
            return axis < 0f ? Lerp(1f, lo, -axis) : Lerp(1f, hi, axis);
        }

        /// <summary>Direction for polar θ (from +Y) and azimuth φ (from +Z toward +X).</summary>
        public static Vector3 Dir(float theta, float phi)
        {
            float st = Mathf.Sin(theta);
            return new Vector3(st * Mathf.Sin(phi), Mathf.Cos(theta), st * Mathf.Cos(phi));
        }

        public static void ToAngles(Vector3 dir, out float theta, out float phi)
        {
            var d = dir.sqrMagnitude > 1e-12f ? dir.normalized : Vector3.up;
            theta = Mathf.Acos(Mathf.Clamp(d.y, -1f, 1f));
            phi = Mathf.Atan2(d.x, d.z);
        }

        /// <summary>Shortest signed angular difference in (-π, π].</summary>
        public static float WrapAngle(float a)
        {
            a = Mathf.Repeat(a + Mathf.PI, Tau) - Mathf.PI;
            return a;
        }

        /// <summary>
        /// Right-handed tangent frame around <paramref name="normal"/>: <c>right</c> and
        /// <c>up</c> perpendicular to it, with <c>up</c> as close as possible to <paramref name="upHint"/>.
        /// </summary>
        public static void Frame(Vector3 normal, Vector3 upHint, out Vector3 right, out Vector3 up)
        {
            normal = normal.normalized;
            Vector3 u = upHint - normal * Vector3.Dot(upHint, normal);
            if (u.sqrMagnitude < 1e-8f)
            {
                var alt = Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward;
                u = alt - normal * Vector3.Dot(alt, normal);
            }
            up = u.normalized;
            right = Vector3.Cross(up, normal).normalized;
        }

        /// <summary>Rotate v about unit axis by angle (Rodrigues).</summary>
        public static Vector3 Rotate(Vector3 v, Vector3 axis, float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return v * c + Vector3.Cross(axis, v) * s + axis * (Vector3.Dot(axis, v) * (1f - c));
        }

        /// <summary>Area-weighted smooth normals over the part's triangles.</summary>
        public static void RecalculateNormals(MeshPart part)
        {
            var n = part.Normals;
            for (int i = 0; i < n.Count; i++) n[i] = Vector3.zero;
            var v = part.Verts;
            var t = part.Tris;
            for (int i = 0; i + 2 < t.Count; i += 3)
            {
                int a = t[i], b = t[i + 1], c = t[i + 2];
                Vector3 fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                n[a] += fn; n[b] += fn; n[c] += fn;
            }
            for (int i = 0; i < n.Count; i++)
                n[i] = n[i].sqrMagnitude > 1e-14f ? n[i].normalized : Vector3.up;
        }

        /// <summary>
        /// Build a closed loft: <paramref name="rings"/> rings of <paramref name="segments"/>
        /// vertices each, connected ring to ring, optionally capped at the last ring with a fan
        /// to <paramref name="tip"/>. Returns the index of the first ring's first vertex.
        /// </summary>
        public static int Loft(MeshPart part, IReadOnlyList<IReadOnlyList<Vector3>> ringPoints,
                               IReadOnlyList<IReadOnlyList<Vector2>> ringUvs, bool capTip, Vector3 tip, Vector2 tipUv)
        {
            int rings = ringPoints.Count;
            int segments = ringPoints[0].Count;
            int first = part.Verts.Count;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                    part.AddVertex(ringPoints[r][s], ringUvs[r][s]);
            for (int r = 0; r + 1 < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    int a = first + r * segments + s, b = first + r * segments + s1;
                    int c = first + (r + 1) * segments + s1, d = first + (r + 1) * segments + s;
                    part.AddQuad(a, b, c, d);
                }
            }
            if (capTip)
            {
                int tipIndex = part.AddVertex(tip, tipUv);
                int last = first + (rings - 1) * segments;
                for (int s = 0; s < segments; s++)
                    part.AddTri(last + s, last + (s + 1) % segments, tipIndex);
            }
            return first;
        }

        /// <summary>Open grid of rows × cols vertices, quads between neighbours. Returns the first index.</summary>
        public static int Grid(MeshPart part, Vector3[,] points, Vector2[,] uvs)
        {
            int rows = points.GetLength(0), cols = points.GetLength(1);
            int first = part.Verts.Count;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    part.AddVertex(points[r, c], uvs[r, c]);
            for (int r = 0; r + 1 < rows; r++)
                for (int c = 0; c + 1 < cols; c++)
                {
                    int a = first + r * cols + c, b = a + 1, cc = a + cols + 1, d = a + cols;
                    part.AddQuad(a, b, cc, d);
                }
            return first;
        }

        /// <summary>A UV sphere (rows of latitude, segments of longitude), +Z as the front pole's UV centre.</summary>
        public static void Sphere(MeshPart part, Vector3 centre, float radius, int rows, int segments, Vector3 frontAxis, Vector3 upAxis)
        {
            Frame(frontAxis, upAxis, out var right, out var up);
            Vector3 fwd = frontAxis.normalized;
            int first = part.Verts.Count;
            for (int r = 0; r <= rows; r++)
            {
                float beta = Mathf.PI * r / rows; // 0 = front pole
                float sb = Mathf.Sin(beta), cb = Mathf.Cos(beta);
                for (int s = 0; s <= segments; s++)
                {
                    float alpha = Tau * s / segments;
                    Vector3 d = fwd * cb + (right * Mathf.Cos(alpha) + up * Mathf.Sin(alpha)) * sb;
                    part.AddVertex(centre + d * radius, new Vector2(s / (float)segments, 1f - r / (float)rows));
                }
            }
            int cols = segments + 1;
            for (int r = 0; r < rows; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = first + r * cols + s, b = a + 1, c = a + cols + 1, d = a + cols;
                    part.AddQuad(a, d, c, b);   // outward: (d-a)×(c-a) = row × seg = +radial
                }
        }

        public static bool AnyNaN(MeshPart part)
        {
            foreach (var v in part.Verts)
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                    float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z)) return true;
            return false;
        }
    }
}
