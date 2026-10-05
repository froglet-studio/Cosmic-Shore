using System;
using System.Collections.Generic;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;

namespace CosmicShore.Content.Models
{
    /// <summary>
    /// A Mesh serialized as a YAML asset (class 43 — the <c>.asset</c> meshes the project
    /// authors by script, e.g. <c>_Models/Testing/Prism.asset</c>, every trail prism's box).
    ///
    /// Layout (documented Unity serialization): <c>m_VertexData</c> holds up to 14 channels
    /// in the fixed VertexAttribute order (Position, Normal, Tangent, Color, TexCoord0-7,
    /// BlendWeight, BlendIndices), each with a stream, byte offset, format and dimension;
    /// the streams are packed one after another in <c>_typelessdata</c> (hex), each stream
    /// 16-byte aligned and each vertex a stride of that stream's channels rounded to 4.
    /// <c>m_IndexBuffer</c> is hex UInt16 or UInt32 (<c>m_IndexFormat</c>) and each
    /// <c>m_SubMeshes</c> entry addresses it by <c>firstByte</c>/<c>indexCount</c> plus a
    /// <c>baseVertex</c>. Only uncompressed meshes (<c>m_MeshCompression: 0</c>) are read.
    /// </summary>
    public static class SerializedMeshImporter
    {
        const int Position = 0, Normal = 1, Tangent = 2, Colour = 3, Uv0 = 4, BlendWeight = 12, BlendIndices = 13;

        struct Channel { public int Stream, Offset, Format, Dimension; }

        public static Mesh Build(YMap body)
        {
            var mesh = new Mesh { name = body.Str("m_Name") ?? "Mesh" };
            if (body.Int("m_MeshCompression") != 0) return mesh;

            var vd = body["m_VertexData"];
            int count = vd?.Int("m_VertexCount") ?? 0;
            var bytes = Hex(vd?.Str("_typelessdata"));
            var channels = new List<Channel>();
            foreach (var c in vd?["m_Channels"]?.Items ?? Array.Empty<YNode>())
                channels.Add(new Channel { Stream = c.Int("stream"), Offset = c.Int("offset"), Format = c.Int("format"), Dimension = c.Int("dimension") & 0xF });

            if (count > 0 && bytes.Length > 0 && channels.Count > 0)
            {
                // Stream strides and base offsets.
                int streams = 0;
                foreach (var c in channels) if (c.Dimension > 0) streams = Math.Max(streams, c.Stream + 1);
                var stride = new int[streams];
                foreach (var c in channels)
                    if (c.Dimension > 0) stride[c.Stream] = Math.Max(stride[c.Stream], c.Offset + c.Dimension * FormatSize(c.Format));
                var baseOffset = new int[streams];
                int acc = 0;
                for (int s = 0; s < streams; s++)
                {
                    stride[s] = (stride[s] + 3) & ~3;
                    baseOffset[s] = acc;
                    acc += stride[s] * count;
                    acc = (acc + 15) & ~15;
                }

                float[] Read(int ch, out int dim)
                {
                    dim = 0;
                    if (ch >= channels.Count) return null;
                    var c = channels[ch];
                    if (c.Dimension == 0) return null;
                    dim = c.Dimension;
                    var result = new float[count * dim];
                    int size = FormatSize(c.Format);
                    for (int v = 0; v < count; v++)
                    {
                        int at = baseOffset[c.Stream] + v * stride[c.Stream] + c.Offset;
                        for (int k = 0; k < dim; k++)
                        {
                            int p = at + k * size;
                            if (p + size > bytes.Length) return result;
                            result[v * dim + k] = Decode(bytes, p, c.Format);
                        }
                    }
                    return result;
                }

                var pos = Read(Position, out int pd);
                if (pos != null)
                {
                    var verts = new Vector3[count];
                    for (int v = 0; v < count; v++) verts[v] = new Vector3(pos[v * pd], pd > 1 ? pos[v * pd + 1] : 0, pd > 2 ? pos[v * pd + 2] : 0);
                    mesh.SetVertices(verts);
                }
                var nrm = Read(Normal, out int nd);
                if (nrm != null && nd >= 3)
                {
                    var ns = new Vector3[count];
                    for (int v = 0; v < count; v++) ns[v] = new Vector3(nrm[v * nd], nrm[v * nd + 1], nrm[v * nd + 2]);
                    mesh.SetNormals(ns);
                }
                var tan = Read(Tangent, out int td);
                if (tan != null && td == 4)
                {
                    var ts = new Vector4[count];
                    for (int v = 0; v < count; v++) ts[v] = new Vector4(tan[v * 4], tan[v * 4 + 1], tan[v * 4 + 2], tan[v * 4 + 3]);
                    mesh.SetTangents(ts);
                }
                var col = Read(Colour, out int cd);
                if (col != null && cd == 4)
                {
                    var cs = new List<Color>(count);
                    for (int v = 0; v < count; v++) cs.Add(new Color(col[v * 4], col[v * 4 + 1], col[v * 4 + 2], col[v * 4 + 3]));
                    mesh.SetColors(cs);
                }
                for (int uv = 0; uv < 8; uv++)
                {
                    var u = Read(Uv0 + uv, out int ud);
                    if (u == null || ud < 2) continue;
                    var us = new Vector2[count];
                    for (int v = 0; v < count; v++) us[v] = new Vector2(u[v * ud], u[v * ud + 1]);
                    mesh.SetUVs(uv, us);
                }
            }

            // Indices.
            var ib = Hex(body.Str("m_IndexBuffer"));
            bool wide = body.Int("m_IndexFormat") == 1;
            var subs = body["m_SubMeshes"]?.Items ?? Array.Empty<YNode>();
            if (subs.Count > 0) mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++)
            {
                var sm = subs[s];
                int first = sm.Int("firstByte"), n = sm.Int("indexCount"), baseVertex = sm.Int("baseVertex");
                int topology = sm.Int("topology");
                var idx = new int[n];
                for (int i = 0; i < n; i++)
                {
                    int p = first + i * (wide ? 4 : 2);
                    if (p + (wide ? 4 : 2) > ib.Length) { Array.Resize(ref idx, i); break; }
                    idx[i] = (wide ? (int)BitConverter.ToUInt32(ib, p) : BitConverter.ToUInt16(ib, p)) + baseVertex;
                }
                if (topology == 2 && idx.Length % 4 == 0) idx = QuadsToTriangles(idx);
                mesh.SetTriangles(idx, s);
            }

            var aabb = body["m_LocalAABB"];
            if (aabb != null)
            {
                var c = aabb["m_Center"]; var e = aabb["m_Extent"];
                mesh.bounds = new Bounds(new Vector3(c?.Float("x") ?? 0, c?.Float("y") ?? 0, c?.Float("z") ?? 0),
                    new Vector3((e?.Float("x") ?? 0) * 2, (e?.Float("y") ?? 0) * 2, (e?.Float("z") ?? 0) * 2));
            }
            else mesh.RecalculateBounds();
            return mesh;
        }

        static int[] QuadsToTriangles(int[] q)
        {
            var t = new int[q.Length / 4 * 6];
            for (int i = 0, o = 0; i < q.Length; i += 4)
            {
                t[o++] = q[i]; t[o++] = q[i + 1]; t[o++] = q[i + 2];
                t[o++] = q[i]; t[o++] = q[i + 2]; t[o++] = q[i + 3];
            }
            return t;
        }

        static int FormatSize(int f) => f switch
        {
            0 or 10 or 11 => 4,          // Float32, UInt32, SInt32
            1 or 4 or 5 or 8 or 9 => 2,  // Float16, UNorm16, SNorm16, UInt16, SInt16
            _ => 1,                      // UNorm8, SNorm8, UInt8, SInt8
        };

        static float Decode(byte[] b, int p, int f) => f switch
        {
            0 => BitConverter.ToSingle(b, p),
            1 => (float)BitConverter.ToHalf(b, p),
            2 => b[p] / 255f,
            3 => Math.Max((sbyte)b[p] / 127f, -1f),
            4 => BitConverter.ToUInt16(b, p) / 65535f,
            5 => Math.Max(BitConverter.ToInt16(b, p) / 32767f, -1f),
            6 => b[p],
            7 => (sbyte)b[p],
            8 => BitConverter.ToUInt16(b, p),
            9 => BitConverter.ToInt16(b, p),
            10 => BitConverter.ToUInt32(b, p),
            11 => BitConverter.ToInt32(b, p),
            _ => 0f,
        };

        static byte[] Hex(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();
            try { return Convert.FromHexString(s.Trim()); }
            catch (FormatException) { return Array.Empty<byte>(); }
        }
    }
}
