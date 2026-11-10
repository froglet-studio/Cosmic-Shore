using System;
using System.Collections.Generic;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The synthetic wire's geometry (the switch rings, <c>Docs/SPINDLE_LOD.md</c> section 9), as pure
    /// numbers: a line-for-line port of <c>build_wire</c>, <c>_emit_quad</c> and <c>compute_tangents</c>
    /// in <c>Tools/Build/author_spindle_lod_meshes.py</c>, which writes the shipped
    /// <c>Assets/_Models/Spindles/SpindleWireRing*.asset</c> meshes. Every step runs in double, in the
    /// script's order, and is narrowed to float only at the end, so the result matches the baked
    /// vertex buffer (<c>Docs/GENOME_MESH_AT_LOAD.md</c>).
    ///
    /// No UnityEngine dependency on purpose: the parity harness (<c>Tools/Build/genome_mesh_parity/</c>)
    /// compiles this file alone outside Unity and diffs it against the asset bytes.
    /// <see cref="SpindleWireRingMesh"/> turns it into a shared <c>Mesh</c>.
    /// </summary>
    public static class SpindleWireRingGeometry
    {
        // The script's constants, same names (author_spindle_lod_meshes.py WIRE_*).
        public const double RingRadius = 0.5;
        public const double Helix = 0.022;
        public const double Strand = 0.016;
        public const int Sides = 6;
        public const int Twists = 24;
        public const int Strands = 3;
        public const double Wraps = 4.0;

        /// <summary>SpindleWireRing.asset: 144 segments x 3 strands.</summary>
        public const int NearSegments = 144;
        /// <summary>SpindleWireRingLow.asset and SpindleWireRingThread.asset: 48 segments.</summary>
        public const int LowSegments = 48;

        /// <summary>Floats per vertex: position 3, normal 3, tangent 4, uv0 2, uv1 3, uv2 3, uv3 3 (stride 84 bytes).</summary>
        public const int FloatsPerVertex = 21;

        public readonly struct Result
        {
            /// <summary>Interleaved vertices, <see cref="FloatsPerVertex"/> floats each, in the asset's channel order.</summary>
            public readonly float[] Vertices;
            public readonly ushort[] Indices;
            public readonly float[] BoundsCenter;
            public readonly float[] BoundsExtent;
            public int VertexCount => Vertices.Length / FloatsPerVertex;

            public Result(float[] vertices, ushort[] indices, float[] center, float[] extent)
            {
                Vertices = vertices; Indices = indices; BoundsCenter = center; BoundsExtent = extent;
            }
        }

        struct V3
        {
            public double X, Y, Z;
            public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
            public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static V3 operator *(V3 a, double s) => new V3(a.X * s, a.Y * s, a.Z * s);
            public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
            public double Length => Math.Sqrt(Dot(this, this));
            public V3 Normalized { get { double m = Length; if (m == 0.0) m = 1.0; return this * (1.0 / m); } }
        }

        struct Corner
        {
            public V3 P, N, Uv1, Uv2, Uv3;
            public double U, V;
        }

        struct RingPoint
        {
            public V3 Centre, Axis, Outward, Up, Splay;
            public double U;
        }

        static V3 WirePoint(V3 centre, V3 outward, V3 up, double angle, double r)
            => centre + (outward * (r * Math.Cos(angle)) + up * (r * Math.Sin(angle)));

        /// <summary>
        /// The cable: <paramref name="strands"/> hexagonal strands laid round the axis at a constant
        /// twist; strands = 0 is the single converged tube on the axis (SpindleWireRingThread).
        /// </summary>
        public static Result Build(int segments, int strands)
        {
            var verts = new List<Corner>(Math.Max(strands, 1) * segments * Sides * 4);
            var tris = new List<int>(Math.Max(strands, 1) * segments * Sides * 6);
            var sides = new double[Sides + 1];
            for (int j = 0; j <= Sides; j++) sides[j] = 2.0 * Math.PI * j / Sides;
            var ring = new RingPoint[2];
            var corners = new Corner[4];
            var cornerSide = new int[4];
            var cornerRing = new int[] { 0, 0, 1, 1 };

            for (int s = 0; s < Math.Max(strands, 1); s++)
            {
                for (int i = 0; i < segments; i++)
                {
                    for (int r = 0; r < 2; r++)
                    {
                        int k = i + r;
                        double theta = 2.0 * Math.PI * k / segments;
                        double c = Math.Cos(theta), sn = Math.Sin(theta);
                        var axis = new V3(RingRadius * c, RingRadius * sn, 0.0);
                        var outward = new V3(c, sn, 0.0);
                        var up = new V3(0.0, 0.0, 1.0);
                        V3 centre, splay;
                        if (strands != 0)
                        {
                            double lay = 2.0 * Math.PI * s / strands + Twists * theta;
                            centre = WirePoint(axis, outward, up, lay, Helix);
                            splay = (centre - axis).Normalized;
                        }
                        else
                        {
                            centre = axis;
                            splay = new V3(0.0, 0.0, 0.0);
                        }
                        ring[r] = new RingPoint
                        {
                            Centre = centre, Axis = axis, Outward = outward, Up = up, Splay = splay,
                            U = 1.0 + Wraps * k / segments,
                        };
                    }

                    for (int j = 0; j < Sides; j++)
                    {
                        cornerSide[0] = j; cornerSide[1] = j + 1; cornerSide[2] = j + 1; cornerSide[3] = j;
                        for (int q = 0; q < 4; q++)
                        {
                            var rp = ring[cornerRing[q]];
                            int side = cornerSide[q];
                            var p = WirePoint(rp.Centre, rp.Outward, rp.Up, sides[side], Strand);
                            corners[q] = new Corner
                            {
                                P = p, U = rp.U, V = (double)side / Sides,
                                Uv1 = rp.Axis - rp.Centre, Uv2 = p - rp.Centre, Uv3 = rp.Splay,
                            };
                        }

                        // A flat facet (machined): one normal per quad, pointing out of its strand.
                        var n = V3.Cross(corners[1].P - corners[0].P, corners[3].P - corners[0].P).Normalized;
                        var mid = ((corners[0].Uv2 + corners[1].Uv2) + (corners[2].Uv2 + corners[3].Uv2)) * 0.25;
                        if (V3.Dot(n, mid) < 0.0) n = n * -1.0;
                        for (int q = 0; q < 4; q++) corners[q].N = n;
                        EmitQuad(verts, tris, corners);
                    }
                }
            }
            return Pack(verts, tris);
        }

        static void EmitQuad(List<Corner> verts, List<int> tris, Corner[] corners)
        {
            int b = verts.Count;
            for (int q = 0; q < 4; q++) verts.Add(corners[q]);
            EmitTri(tris, corners, b, b, b + 1, b + 2);
            EmitTri(tris, corners, b, b, b + 2, b + 3);
        }

        static void EmitTri(List<int> tris, Corner[] corners, int b, int t0, int t1, int t2)
        {
            var p0 = corners[t0 - b].P; var p1 = corners[t1 - b].P; var p2 = corners[t2 - b].P;
            var n = (corners[t0 - b].N + corners[t1 - b].N) + corners[t2 - b].N;
            if (V3.Dot(V3.Cross(p1 - p0, p2 - p0), n) < 0.0) { int t = t1; t1 = t2; t2 = t; }
            tris.Add(t0); tris.Add(t1); tris.Add(t2);
        }

        static Result Pack(List<Corner> verts, List<int> tris)
        {
            int n = verts.Count;
            var tan = new V3[n];
            var bit = new V3[n];
            for (int t = 0; t < tris.Count; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                V3 p0 = verts[a].P, p1 = verts[b].P, p2 = verts[c].P;
                double x1 = p1.X - p0.X, x2 = p2.X - p0.X;
                double y1 = p1.Y - p0.Y, y2 = p2.Y - p0.Y;
                double z1 = p1.Z - p0.Z, z2 = p2.Z - p0.Z;
                double s1 = verts[b].U - verts[a].U, s2 = verts[c].U - verts[a].U;
                double t1 = verts[b].V - verts[a].V, t2 = verts[c].V - verts[a].V;
                double d = s1 * t2 - s2 * t1;
                if (Math.Abs(d) < 1e-12) continue;
                double r = 1.0 / d;
                var sd = new V3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                var td = new V3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);
                tan[a] += sd; tan[b] += sd; tan[c] += sd;
                bit[a] += td; bit[b] += td; bit[c] += td;
            }

            var data = new float[n * FloatsPerVertex];
            var lo = new double[] { double.MaxValue, double.MaxValue, double.MaxValue };
            var hi = new double[] { double.MinValue, double.MinValue, double.MinValue };
            for (int i = 0; i < n; i++)
            {
                var v = verts[i];
                var nr = v.N;
                var tv = tan[i];
                double dot = nr.X * tv.X + nr.Y * tv.Y + nr.Z * tv.Z;
                var o = new V3(tv.X - nr.X * dot, tv.Y - nr.Y * dot, tv.Z - nr.Z * dot);
                double m = Math.Sqrt(o.X * o.X + o.Y * o.Y + o.Z * o.Z);
                if (m < 1e-9)
                {
                    var rf = Math.Abs(nr.X) < 0.9 ? new V3(1.0, 0.0, 0.0) : new V3(0.0, 1.0, 0.0);
                    o = new V3(nr.Y * rf.Z - nr.Z * rf.Y, nr.Z * rf.X - nr.X * rf.Z, nr.X * rf.Y - nr.Y * rf.X);
                    m = Math.Sqrt(o.X * o.X + o.Y * o.Y + o.Z * o.Z);
                    if (m == 0.0) m = 1.0;
                }
                o = new V3(o.X / m, o.Y / m, o.Z / m);
                double cx = nr.Y * o.Z - nr.Z * o.Y;
                double cy = nr.Z * o.X - nr.X * o.Z;
                double cz = nr.X * o.Y - nr.Y * o.X;
                double w = (cx * bit[i].X + cy * bit[i].Y + cz * bit[i].Z) < 0.0 ? -1.0 : 1.0;

                int f = i * FloatsPerVertex;
                Put(data, ref f, v.P); Put(data, ref f, v.N); Put(data, ref f, o); data[f++] = (float)w;
                data[f++] = (float)v.U; data[f++] = (float)v.V;
                Put(data, ref f, v.Uv1); Put(data, ref f, v.Uv2); Put(data, ref f, v.Uv3);

                lo[0] = Math.Min(lo[0], v.P.X); lo[1] = Math.Min(lo[1], v.P.Y); lo[2] = Math.Min(lo[2], v.P.Z);
                hi[0] = Math.Max(hi[0], v.P.X); hi[1] = Math.Max(hi[1], v.P.Y); hi[2] = Math.Max(hi[2], v.P.Z);
            }

            var idx = new ushort[tris.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = checked((ushort)tris[i]);
            var ctr = new float[3];
            var ext = new float[3];
            for (int k = 0; k < 3; k++)
            {
                ctr[k] = (float)((lo[k] + hi[k]) / 2);
                ext[k] = (float)((hi[k] - lo[k]) / 2);
            }
            return new Result(data, idx, ctr, ext);
        }

        static void Put(float[] d, ref int f, V3 v)
        {
            d[f++] = (float)v.X; d[f++] = (float)v.Y; d[f++] = (float)v.Z;
        }
    }
}
