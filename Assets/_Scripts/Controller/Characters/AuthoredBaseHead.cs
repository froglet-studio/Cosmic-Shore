using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The swap-in for a sculpted head: a Unity <see cref="Mesh"/> whose blend shapes are named
    /// after <see cref="HeadAxis"/> members (a "<c>MuzzleLength</c>" shape, optionally with
    /// "<c>MuzzleLength-</c>" for the negative direction), evaluated linearly like every real
    /// character creator. Sites are authored angles like the procedural head's, and the surface
    /// sampler ray-casts the blended mesh from the head centre. Head space is the same: origin at
    /// the skull centre, +Y crown, +Z face, height ≈ 1 — scale the sculpt to that before importing.
    ///
    /// This file is the whole of the "if the control fails" plan. Nothing else changes.
    /// </summary>
    public sealed class AuthoredBaseHead : IBaseHead
    {
        readonly Vector3[] _neutral;
        readonly Vector3[][] _plus = new Vector3[HeadShape.AxisCount][];
        readonly Vector3[][] _minus = new Vector3[HeadShape.AxisCount][];
        readonly HeadTopology _topology;
        readonly HeadSiteSpec[] _sites;

        public AuthoredBaseHead(Mesh mesh, HeadSiteSpec[] sites)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            _neutral = mesh.vertices;
            _topology = new HeadTopology { VertexCount = _neutral.Length, Triangles = mesh.triangles, Uvs = mesh.uv };
            _sites = sites ?? Array.Empty<HeadSiteSpec>();

            int count = mesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                bool negative = name.EndsWith("-", StringComparison.Ordinal);
                string axisName = negative ? name.Substring(0, name.Length - 1) : name;
                if (!Enum.TryParse(axisName, true, out HeadAxis axis)) continue;
                var deltas = new Vector3[_neutral.Length];
                var dn = new Vector3[_neutral.Length];
                var dt = new Vector3[_neutral.Length];
                mesh.GetBlendShapeFrameVertices(i, mesh.GetBlendShapeFrameCount(i) - 1, deltas, dn, dt);
                if (negative) _minus[(int)axis] = deltas; else _plus[(int)axis] = deltas;
            }
        }

        public HeadTopology Topology => _topology;
        public IReadOnlyList<HeadSiteSpec> Sites => _sites;

        public Vector3 SiteDirection(HeadSiteSpec site, HeadShape shape)
        {
            float phi = site.PhiDeg;
            if (site.SpreadDegPerUnit != 0f) phi += shape.Clamped(site.SpreadAxis) * site.SpreadDegPerUnit;
            return GeometryKit.Dir(site.ThetaDeg * Mathf.Deg2Rad, phi * Mathf.Deg2Rad);
        }

        public Vector3[] Evaluate(HeadShape shape)
        {
            var verts = (Vector3[])_neutral.Clone();
            for (int a = 0; a < HeadShape.AxisCount; a++)
            {
                float w = shape.Clamped((HeadAxis)a);
                Vector3[] deltas = null;
                float k = 0f;
                if (w > 0f && _plus[a] != null) { deltas = _plus[a]; k = w; }
                else if (w < 0f)
                {
                    if (_minus[a] != null) { deltas = _minus[a]; k = -w; }
                    else if (_plus[a] != null) { deltas = _plus[a]; k = w; } // mirror the positive shape
                }
                if (deltas == null || k == 0f) continue;
                for (int i = 0; i < verts.Length; i++) verts[i] += deltas[i] * k;
            }
            return verts;
        }

        public IHeadSurface Surface(HeadShape shape) => new MeshSurface(Evaluate(shape), _topology);

        /// <summary>Brute-force ray/triangle sampler; the head has a few thousand triangles and a bake asks a few hundred times.</summary>
        sealed class MeshSurface : IHeadSurface
        {
            readonly Vector3[] _v;
            readonly int[] _t;
            public MeshSurface(Vector3[] verts, HeadTopology topo) { _v = verts; _t = topo.Triangles; }
            public Vector3 Centre => Vector3.zero;

            public Vector3 Sample(Vector3 direction)
            {
                Vector3 d = direction.normalized;
                float best = -1f;
                for (int i = 0; i + 2 < _t.Length; i += 3)
                {
                    if (RayTri(d, _v[_t[i]], _v[_t[i + 1]], _v[_t[i + 2]], out float dist) && dist > best) best = dist;
                }
                return best > 0f ? d * best : d * 0.45f;
            }

            public Vector2 Uv(Vector3 direction)
            {
                GeometryKit.ToAngles(direction, out float theta, out float phi);
                return new Vector2(0.5f + phi / GeometryKit.Tau, 1f - theta / Mathf.PI);
            }

            public Vector3 Direction(Vector2 uv) =>
                GeometryKit.Dir((1f - uv.y) * Mathf.PI, (uv.x - 0.5f) * GeometryKit.Tau);

            static bool RayTri(Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
            {
                t = 0f;
                Vector3 e1 = b - a, e2 = c - a;
                Vector3 p = Vector3.Cross(d, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-9f) return false;
                float inv = 1f / det;
                Vector3 s = -a;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f) return false;
                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(d, q) * inv;
                if (v < 0f || u + v > 1f) return false;
                t = Vector3.Dot(e2, q) * inv;
                return t > 0f;
            }
        }
    }
}
