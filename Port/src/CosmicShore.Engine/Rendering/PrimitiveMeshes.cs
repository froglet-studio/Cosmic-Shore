using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Built-in primitive meshes for <see cref="GameObject.CreatePrimitive"/> (the mesh
    /// arc). Like the original engine's built-ins, each primitive is ONE shared Mesh
    /// asset — every created primitive references the same instance (callers that mutate
    /// must go through <see cref="MeshFilter.mesh"/> to get their own copy).
    ///
    /// Shapes match the original dimensions: Cube 1×1×1; Sphere radius 0.5; Capsule
    /// radius 0.5, total height 2 (Y axis); Cylinder radius 0.5, height 2 (Y axis, with
    /// caps); Plane 10×10 on XZ facing +Y; Quad 1×1 on XY facing −Z. Documented
    /// deviations (data-only, tessellation is not contract): Sphere is an icosphere
    /// (subdiv 2, smooth) instead of a UV sphere; Plane is a single quad instead of the
    /// original's 11×11 vertex grid.
    /// </summary>
    internal static class PrimitiveMeshes
    {
        static readonly Dictionary<Rendering.PrimitiveType, Mesh> _shared = new();
        static readonly object _gate = new(); // xunit runs test classes in parallel; guard the shared cache

        internal static Mesh GetShared(Rendering.PrimitiveType type)
        {
            lock (_gate)
            {
                if (_shared.TryGetValue(type, out var cached) && cached)
                    return cached;

                Mesh mesh = type switch
                {
                    Rendering.PrimitiveType.Cube => BuildCube(),
                    Rendering.PrimitiveType.Sphere => BuildIcosphere(subdivisions: 2, radius: 0.5f),
                    Rendering.PrimitiveType.Capsule => BuildCapsule(radius: 0.5f, cylinderHalfHeight: 0.5f, segments: 16, hemisphereRings: 6),
                    Rendering.PrimitiveType.Cylinder => BuildCylinder(radius: 0.5f, halfHeight: 1f, segments: 16),
                    Rendering.PrimitiveType.Plane => BuildPlane(halfSize: 5f),
                    Rendering.PrimitiveType.Quad => BuildQuad(),
                    _ => throw new ArgumentOutOfRangeException(nameof(type)),
                };
                mesh.name = type.ToString();
                _shared[type] = mesh;
                return mesh;
            }
        }

        static Mesh BuildCube()
        {
            // 24 verts (4 per face) for flat per-face normals — the original cube layout.
            var faces = new (Vector3 normal, Vector3 up, Vector3 right)[]
            {
                (Vector3.forward, Vector3.up, Vector3.left),
                (Vector3.back, Vector3.up, Vector3.right),
                (Vector3.up, Vector3.back, Vector3.right),
                (Vector3.down, Vector3.forward, Vector3.right),
                (Vector3.right, Vector3.up, Vector3.forward),
                (Vector3.left, Vector3.up, Vector3.back),
            };

            var verts = new Vector3[24];
            var norms = new Vector3[24];
            var uv = new Vector2[24];
            var tris = new int[36];

            for (int f = 0; f < 6; f++)
            {
                var (normal, up, right) = faces[f];
                int v = f * 4;
                verts[v + 0] = (normal - right - up) * 0.5f;
                verts[v + 1] = (normal + right - up) * 0.5f;
                verts[v + 2] = (normal + right + up) * 0.5f;
                verts[v + 3] = (normal - right + up) * 0.5f;
                norms[v + 0] = norms[v + 1] = norms[v + 2] = norms[v + 3] = normal;
                uv[v + 0] = new Vector2(0f, 0f);
                uv[v + 1] = new Vector2(1f, 0f);
                uv[v + 2] = new Vector2(1f, 1f);
                uv[v + 3] = new Vector2(0f, 1f);

                int t = f * 6;
                tris[t + 0] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }

            return Assemble(verts, norms, uv, tris);
        }

        static Mesh BuildIcosphere(int subdivisions, float radius)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>
            {
                new(-1f,  t, 0f), new( 1f,  t, 0f), new(-1f, -t, 0f), new( 1f, -t, 0f),
                new(0f, -1f,  t), new(0f,  1f,  t), new(0f, -1f, -t), new(0f,  1f, -t),
                new( t, 0f, -1f), new( t, 0f,  1f), new(-t, 0f, -1f), new(-t, 0f,  1f),
            };
            var tris = new List<int>
            {
                0, 11, 5,  0, 5, 1,  0, 1, 7,  0, 7, 10,  0, 10, 11,
                1, 5, 9,  5, 11, 4,  11, 10, 2,  10, 7, 6,  7, 1, 8,
                3, 9, 4,  3, 4, 2,  3, 2, 6,  3, 6, 8,  3, 8, 9,
                4, 9, 5,  2, 4, 11,  6, 2, 10,  8, 6, 7,  9, 8, 1,
            };

            var cache = new Dictionary<long, int>();
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Midpoint(verts, cache, a, b);
                    int bc = Midpoint(verts, cache, b, c);
                    int ca = Midpoint(verts, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }

            var positions = new Vector3[verts.Count];
            var normals = new Vector3[verts.Count];
            var uv = new Vector2[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 n = verts[i].normalized;
                positions[i] = n * radius;
                normals[i] = n;
                uv[i] = SphericalUv(n);
            }

            return Assemble(positions, normals, uv, tris.ToArray());
        }

        static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int i0, int i1)
        {
            long key = i0 < i1 ? ((long)i0 << 32) | (uint)i1 : ((long)i1 << 32) | (uint)i0;
            if (cache.TryGetValue(key, out int existing)) return existing;
            verts.Add(((verts[i0] + verts[i1]) * 0.5f).normalized);
            cache[key] = verts.Count - 1;
            return verts.Count - 1;
        }

        static Mesh BuildCapsule(float radius, float cylinderHalfHeight, int segments, int hemisphereRings)
        {
            // Latitude rings from the top pole down through the equator (duplicated so the
            // cylinder wall has a ring pair), then to the bottom pole. Top-half rings are
            // lifted by +cylinderHalfHeight, bottom-half by −cylinderHalfHeight.
            var rings = new List<(float y, float r, float ny)>();
            for (int i = 0; i <= hemisphereRings; i++)
            {
                float lat = Mathf.PI * 0.5f * i / hemisphereRings; // 0 (pole) → π/2 (equator)
                rings.Add((Mathf.Cos(lat) * radius + cylinderHalfHeight, Mathf.Sin(lat) * radius, Mathf.Cos(lat)));
            }
            for (int i = hemisphereRings; i >= 0; i--)
            {
                float lat = Mathf.PI * 0.5f * i / hemisphereRings;
                rings.Add((-(Mathf.Cos(lat) * radius) - cylinderHalfHeight, Mathf.Sin(lat) * radius, -Mathf.Cos(lat)));
            }

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uv = new List<Vector2>();
            int ringVerts = segments + 1; // seam-duplicated column for clean UVs

            for (int ringIndex = 0; ringIndex < rings.Count; ringIndex++)
            {
                var (y, r, ny) = rings[ringIndex];
                for (int s = 0; s <= segments; s++)
                {
                    float angle = Mathf.PI * 2f * s / segments;
                    float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    verts.Add(new Vector3(cos * r, y, sin * r));
                    // Hemisphere normals point from the hemisphere center; on the wall
                    // (r == radius, ny == 0) this reduces to the radial direction.
                    norms.Add(new Vector3(cos * (r / radius), ny, sin * (r / radius)).normalized);
                    uv.Add(new Vector2((float)s / segments, 1f - (float)ringIndex / (rings.Count - 1)));
                }
            }

            var tris = new List<int>();
            for (int ringIndex = 0; ringIndex + 1 < rings.Count; ringIndex++)
            {
                int a = ringIndex * ringVerts;
                int b = (ringIndex + 1) * ringVerts;
                for (int s = 0; s < segments; s++)
                {
                    tris.AddRange(new[] { a + s, a + s + 1, b + s + 1 });
                    tris.AddRange(new[] { a + s, b + s + 1, b + s });
                }
            }

            return Assemble(verts.ToArray(), norms.ToArray(), uv.ToArray(), tris.ToArray());
        }

        static Mesh BuildCylinder(float radius, float halfHeight, int segments)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();

            // Wall (seam-duplicated column).
            for (int s = 0; s <= segments; s++)
            {
                float angle = Mathf.PI * 2f * s / segments;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                var radial = new Vector3(cos, 0f, sin);
                verts.Add(new Vector3(cos * radius, halfHeight, sin * radius));
                verts.Add(new Vector3(cos * radius, -halfHeight, sin * radius));
                norms.Add(radial);
                norms.Add(radial);
                uv.Add(new Vector2((float)s / segments, 1f));
                uv.Add(new Vector2((float)s / segments, 0f));
            }
            for (int s = 0; s < segments; s++)
            {
                int a = s * 2;
                tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }

            // Caps (fan around a center vertex, flat normals).
            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? halfHeight : -halfHeight;
                Vector3 normal = cap == 0 ? Vector3.up : Vector3.down;
                int center = verts.Count;
                verts.Add(new Vector3(0f, y, 0f));
                norms.Add(normal);
                uv.Add(new Vector2(0.5f, 0.5f));
                for (int s = 0; s <= segments; s++)
                {
                    float angle = Mathf.PI * 2f * s / segments;
                    float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    verts.Add(new Vector3(cos * radius, y, sin * radius));
                    norms.Add(normal);
                    uv.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
                }
                for (int s = 0; s < segments; s++)
                {
                    int rim = center + 1 + s;
                    if (cap == 0) tris.AddRange(new[] { center, rim + 1, rim });
                    else tris.AddRange(new[] { center, rim, rim + 1 });
                }
            }

            return Assemble(verts.ToArray(), norms.ToArray(), uv.ToArray(), tris.ToArray());
        }

        static Mesh BuildPlane(float halfSize)
        {
            var verts = new[]
            {
                new Vector3(-halfSize, 0f, -halfSize),
                new Vector3(halfSize, 0f, -halfSize),
                new Vector3(halfSize, 0f, halfSize),
                new Vector3(-halfSize, 0f, halfSize),
            };
            var norms = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            var uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            var tris = new[] { 0, 3, 2, 0, 2, 1 };
            return Assemble(verts, norms, uv, tris);
        }

        static Mesh BuildQuad()
        {
            var verts = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
            };
            var norms = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            var tris = new[] { 0, 2, 1, 0, 3, 2 };
            return Assemble(verts, norms, uv, tris);
        }

        static Vector2 SphericalUv(Vector3 n) => new(
            0.5f + Mathf.Atan2(n.z, n.x) / (Mathf.PI * 2f),
            0.5f + Mathf.Asin(Mathf.Clamp(n.y, -1f, 1f)) / Mathf.PI);

        static Mesh Assemble(Vector3[] verts, Vector3[] norms, Vector2[] uv, int[] tris)
        {
            var mesh = new Mesh
            {
                vertices = verts,
                normals = norms,
                uv = uv,
                triangles = tris,
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
