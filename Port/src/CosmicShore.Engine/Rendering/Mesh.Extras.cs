using System;
using System.Collections.Generic;
using CosmicShore.Engine.Rendering;

namespace CosmicShore.Engine
{
    public enum MeshTopology { Triangles = 0, Quads = 2, Lines = 3, LineStrip = 4, Points = 5 }

    /// <summary>One mesh to combine (original: UnityEngine.CombineInstance).</summary>
    public struct CombineInstance
    {
        public Mesh mesh;
        public int subMeshIndex;
        public Matrix4x4 transform;
        public Vector4 lightmapScaleOffset;
        public Vector4 realtimeLightmapScaleOffset;
    }

    public partial class Mesh
    {
        // Full-width UV channels (Vector3/Vector4 texcoords — e.g. the shield morph's per-face
        // centroid in TEXCOORD1). The Vector2 view of a channel stays in the base storage.
        readonly Dictionary<int, Vector4[]> _uvFull = new();
        readonly Dictionary<int, int> _uvDims = new();
        readonly Dictionary<int, MeshTopology> _topology = new();

        /// <summary>Components authored for a UV channel (2 unless a Vector3/Vector4 set was used).</summary>
        public int GetUVDimension(int channel) => _uvDims.TryGetValue(channel, out var d) ? d : 2;

        /// <summary>Render backend: channel 1's full-width values when they were authored wider than 2 (the shield meshes' face centroids), else null. Zero-copy.</summary>
        public Vector4[] RenderUv1Wide => _uvFull.TryGetValue(1, out var f) && f.Length == vertexCount ? f : null;

        readonly Dictionary<int, (Vector2[] Source, Vector4[] Wide)> _uvWidened = new();

        /// <summary>
        /// Render backend: a channel's values as Vector4 (a Vector2 channel widened with z = w = 0),
        /// or null when the channel holds nothing for these vertices. The same array is returned
        /// until the channel changes, so a renderer can compare references.
        /// </summary>
        public Vector4[] RenderUvWide(int channel)
        {
            if (_uvFull.TryGetValue(channel, out var f) && f.Length == vertexCount) return f;
            if (channel < 1 || channel > 7) return null;
            var v2 = _extraUvs[channel - 1];
            if (v2 == null || v2.Length != vertexCount || vertexCount == 0) return null;
            if (_uvWidened.TryGetValue(channel, out var cached) && ReferenceEquals(cached.Source, v2)) return cached.Wide;
            var wide = new Vector4[v2.Length];
            for (int i = 0; i < wide.Length; i++) wide[i] = new Vector4(v2[i].x, v2[i].y, 0f, 0f);
            _uvWidened[channel] = (v2, wide);
            return wide;
        }

        /// <summary>Original contract: whether the mesh carries a value per vertex for <paramref name="attribute"/>
        /// (CrystalElementPlates asks it of TEXCOORD4 before baking the plates' elements).</summary>
        public bool HasVertexAttribute(CosmicShore.Engine.Rendering.VertexAttribute attribute)
        {
            int n = vertexCount;
            if (n == 0) return false;
            switch (attribute)
            {
                case CosmicShore.Engine.Rendering.VertexAttribute.Position: return true;
                case CosmicShore.Engine.Rendering.VertexAttribute.Normal: return _normals.Length == n;
                case CosmicShore.Engine.Rendering.VertexAttribute.Tangent: return _tangents != null && _tangents.Length == n;
                case CosmicShore.Engine.Rendering.VertexAttribute.Color: return _colors.Length == n;
                case CosmicShore.Engine.Rendering.VertexAttribute.BlendWeight:
                case CosmicShore.Engine.Rendering.VertexAttribute.BlendIndices: return _boneWeights != null && _boneWeights.Length == n;
            }
            int channel = (int)attribute - (int)CosmicShore.Engine.Rendering.VertexAttribute.TexCoord0;
            if (channel < 0 || channel > 7) return false;
            if (_uvFull.TryGetValue(channel, out var full) && full.Length == n) return true;
            var v2 = channel == 0 ? _uv : _extraUvs[channel - 1];
            return v2 != null && v2.Length == n;
        }

        /// <summary>Full-width UVs of a channel (x,y from the Vector2 storage when never set wider).</summary>
        public Vector4[] GetUVsFull(int channel)
        {
            if (_uvFull.TryGetValue(channel, out var full) && full.Length == vertexCount) return full;
            var v2 = channel == 0 ? uv : GetUvChannel(channel);
            var r = new Vector4[v2.Length];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector4(v2[i].x, v2[i].y, 0f, 0f);
            return r;
        }

        void SetUVsWide(int channel, Vector4[] values, int dims)
        {
            if (channel < 0 || channel > 7) throw new ArgumentOutOfRangeException(nameof(channel));
            var v2 = new Vector2[values.Length];
            for (int i = 0; i < values.Length; i++) v2[i] = new Vector2(values[i].x, values[i].y);
            SetUvChannel(channel, v2);
            _uvFull[channel] = values;
            _uvDims[channel] = dims;
            IncrementVersion();
        }

        public void SetUVs(int channel, List<Vector3> uvs)
        {
            var a = new Vector4[uvs?.Count ?? 0];
            for (int i = 0; i < a.Length; i++) a[i] = new Vector4(uvs[i].x, uvs[i].y, uvs[i].z, 0f);
            SetUVsWide(channel, a, 3);
        }

        public void SetUVs(int channel, Vector3[] uvs)
        {
            var a = new Vector4[uvs?.Length ?? 0];
            for (int i = 0; i < a.Length; i++) a[i] = new Vector4(uvs[i].x, uvs[i].y, uvs[i].z, 0f);
            SetUVsWide(channel, a, 3);
        }

        public void SetUVs(int channel, List<Vector4> uvs) => SetUVsWide(channel, uvs?.ToArray() ?? Array.Empty<Vector4>(), 4);
        public void SetUVs(int channel, Vector4[] uvs) => SetUVsWide(channel, (Vector4[])(uvs ?? Array.Empty<Vector4>()).Clone(), 4);

        public void GetUVs(int channel, List<Vector3> uvs)
        {
            uvs.Clear();
            foreach (var v in GetUVsFull(channel)) uvs.Add(new Vector3(v.x, v.y, v.z));
        }

        public void GetUVs(int channel, List<Vector4> uvs)
        {
            uvs.Clear();
            uvs.AddRange(GetUVsFull(channel));
        }

        public void GetVertices(List<Vector3> vertices) { vertices.Clear(); vertices.AddRange(this.vertices); }
        public void GetNormals(List<Vector3> normals) { normals.Clear(); normals.AddRange(this.normals); }
        public void GetTangents(List<Vector4> tangents) { tangents.Clear(); tangents.AddRange(this.tangents); }
        public void GetColors(List<Color> colors) { colors.Clear(); colors.AddRange(this.colors); }
        public void GetTriangles(List<int> triangles, int submesh) { triangles.Clear(); triangles.AddRange(GetTriangles(submesh)); }
        public void GetIndices(List<int> indices, int submesh) => GetTriangles(indices, submesh);
        public int[] GetIndices(int submesh) => GetTriangles(submesh);
        public MeshTopology GetTopology(int submesh) => _topology.TryGetValue(submesh, out var t) ? t : MeshTopology.Triangles;
        public uint GetIndexCount(int submesh) => (uint)GetTriangles(submesh).Length;
        public uint GetIndexStart(int submesh) => 0;
        public uint GetBaseVertex(int submesh) => 0;

        public void SetVertices(List<Vector3> inVertices, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => SetVertices(inVertices.GetRange(start, length));
        public void SetVertices(Vector3[] inVertices, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => SetVertices(new ArraySegment<Vector3>(inVertices, start, length).ToArray());
        public void SetNormals(System.Collections.Generic.List<Vector3> inNormals, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => SetNormals(inNormals.GetRange(start, length).ToArray());

        public void SetNormals(Vector3[] inNormals, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => SetNormals(new ArraySegment<Vector3>(inNormals, start, length).ToArray());

        // The ranged UV setters: the channel takes uvs[start .. start + length).
        public void SetUVs(int channel, Vector2[] uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => SetUVs(channel, new ArraySegment<Vector2>(uvs, start, length).ToArray());
        public void SetUVs(int channel, Vector3[] uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => SetUVs(channel, new ArraySegment<Vector3>(uvs, start, length).ToArray());
        public void SetUVs(int channel, Vector4[] uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => SetUVsWide(channel, new ArraySegment<Vector4>(uvs, start, length).ToArray(), 4);
        public void SetUVs(int channel, List<Vector2> uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => SetUVs(channel, uvs.GetRange(start, length).ToArray());
        public void SetUVs(int channel, List<Vector3> uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => SetUVs(channel, uvs.GetRange(start, length).ToArray());
        public void SetUVs(int channel, List<Vector4> uvs, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => SetUVsWide(channel, uvs.GetRange(start, length).ToArray(), 4);

        public void SetColors(Color[] inColors) => colors = inColors;
        public void SetColors(Color32[] inColors) => colors32 = inColors;
        public void SetColors(List<Color32> inColors) => colors32 = inColors.ToArray();
        public void SetColors(Color[] inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => colors = new ArraySegment<Color>(inColors, start, length).ToArray();
        public void SetColors(Color32[] inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => colors32 = new ArraySegment<Color32>(inColors, start, length).ToArray();
        public void SetColors(List<Color> inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => colors = inColors.GetRange(start, length).ToArray();
        public void SetColors(List<Color32> inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => colors32 = inColors.GetRange(start, length).ToArray();

        /// <summary>Original contract: indices[indicesStart .. +indicesLength] become the submesh.</summary>
        public void SetIndices(int[] indices, int indicesStart, int indicesLength, MeshTopology topology, int submesh,
            bool calculateBounds = true, int baseVertex = 0)
            => SetIndices(new ArraySegment<int>(indices, indicesStart, indicesLength).ToArray(), topology, submesh, calculateBounds, baseVertex);

        public void SetIndices(List<int> indices, int indicesStart, int indicesLength, MeshTopology topology, int submesh,
            bool calculateBounds = true, int baseVertex = 0)
            => SetIndices(indices.GetRange(indicesStart, indicesLength).ToArray(), topology, submesh, calculateBounds, baseVertex);

        /// <summary>
        /// Original contract (UnityEngine.Mesh.CombineMeshes): each instance's mesh is appended whole (all its vertices),
        /// transformed by its matrix when <paramref name="useMatrices"/>; its <c>subMeshIndex</c> submesh becomes one
        /// submesh of the result, or all of them merge into submesh 0 when <paramref name="mergeSubMeshes"/>. Normals take
        /// the inverse transpose; positions, normals, tangents, the first UV channel and colours are carried.
        /// </summary>
        public void CombineMeshes(CombineInstance[] combine, bool mergeSubMeshes = true, bool useMatrices = true, bool hasLightmapData = false)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var cols = new List<Color>();
            var subs = new List<List<int>>();
            bool anyNormals = false, anyUv = false, anyColors = false;
            foreach (var ci in combine ?? Array.Empty<CombineInstance>())
                if (ci.mesh) { anyNormals |= ci.mesh.normals.Length > 0; anyUv |= ci.mesh.uv.Length > 0; anyColors |= ci.mesh.colors.Length > 0; }
            foreach (var ci in combine ?? Array.Empty<CombineInstance>())
            {
                var m = ci.mesh;
                if (!m) continue;
                int baseIndex = verts.Count;
                var mv = m.vertices;
                var mn = m.normals;
                var mu = m.uv;
                var mc = m.colors;
                var nm = ci.transform.inverse.transpose;
                for (int i = 0; i < mv.Length; i++)
                {
                    verts.Add(useMatrices ? ci.transform.MultiplyPoint3x4(mv[i]) : mv[i]);
                    if (anyNormals) norms.Add(i < mn.Length ? (useMatrices ? nm.MultiplyVector(mn[i]).normalized : mn[i]) : Vector3.up);
                    if (anyUv) uvs.Add(i < mu.Length ? mu[i] : default);
                    if (anyColors) cols.Add(i < mc.Length ? mc[i] : Color.white);
                }
                var tris = m.GetTriangles(Math.Clamp(ci.subMeshIndex, 0, Math.Max(0, m.subMeshCount - 1)));
                if (mergeSubMeshes) { if (subs.Count == 0) subs.Add(new List<int>()); }
                else subs.Add(new List<int>());
                var dst = subs[^1];
                for (int i = 0; i < tris.Length; i++) dst.Add(tris[i] + baseIndex);
            }
            Clear();
            if (verts.Count > 65535) indexFormat = IndexFormat.UInt32;
            vertices = verts.ToArray();
            if (anyNormals) normals = norms.ToArray();
            if (anyUv) uv = uvs.ToArray();
            if (anyColors) colors = cols.ToArray();
            subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) SetTriangles(subs[s].ToArray(), s);
            RecalculateBounds();
        }

        public void SetTriangles(int[] triangles, int submesh, bool calculateBounds, int baseVertex = 0)
        {
            SetTriangles(Offset(triangles, baseVertex), submesh);
            if (calculateBounds) RecalculateBounds();
        }

        public void SetTriangles(List<int> triangles, int submesh, bool calculateBounds, int baseVertex = 0)
            => SetTriangles(triangles.ToArray(), submesh, calculateBounds, baseVertex);

        public void SetIndices(int[] indices, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0)
        {
            _topology[submesh] = topology;
            SetTriangles(indices, submesh, calculateBounds, baseVertex);
        }

        public void SetIndices(List<int> indices, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0)
            => SetIndices(indices.ToArray(), topology, submesh, calculateBounds, baseVertex);

        static int[] Offset(int[] tris, int baseVertex)
        {
            if (baseVertex == 0 || tris == null) return tris;
            var r = new int[tris.Length];
            for (int i = 0; i < r.Length; i++) r[i] = tris[i] + baseVertex;
            return r;
        }

        /// <summary>Uploads to the GPU; the renderer re-uploads on version change (original: UploadMeshData).</summary>
        public void UploadMeshData(bool markNoLongerReadable) => IncrementVersion();

        public void Optimize() { }
        public void OptimizeIndexBuffers() { }
        public void OptimizeReorderVertexBuffer() { }
        public void RecalculateTangents() { }
        public void RecalculateUVDistributionMetrics(float uvAreaThreshold = 1e-9f) { }

        /// <summary>Monotonic content version — the renderer compares it to re-upload changed meshes.</summary>
        public int version { get; private set; }
        internal void IncrementVersion() => version++;
    }
}
