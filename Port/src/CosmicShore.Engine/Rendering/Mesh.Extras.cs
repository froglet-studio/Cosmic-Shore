using System;
using System.Collections.Generic;
using CosmicShore.Engine.Rendering;

namespace CosmicShore.Engine
{
    public enum MeshTopology { Triangles = 0, Quads = 2, Lines = 3, LineStrip = 4, Points = 5 }

    public partial class Mesh
    {
        // Full-width UV channels (Vector3/Vector4 texcoords — e.g. the shield morph's per-face
        // centroid in TEXCOORD1). The Vector2 view of a channel stays in the base storage.
        readonly Dictionary<int, Vector4[]> _uvFull = new();
        readonly Dictionary<int, int> _uvDims = new();
        readonly Dictionary<int, MeshTopology> _topology = new();

        /// <summary>Components authored for a UV channel (2 unless a Vector3/Vector4 set was used).</summary>
        public int GetUVDimension(int channel) => _uvDims.TryGetValue(channel, out var d) ? d : 2;

        /// <summary>The wide UV channels and topologies, for <see cref="CopyTo"/> (a clone keeps a shield mesh's face centroids).</summary>
        void CopyExtrasTo(Mesh destination)
        {
            destination._uvFull.Clear();
            foreach (var kv in _uvFull) destination._uvFull[kv.Key] = (Vector4[])kv.Value.Clone();
            destination._uvDims.Clear();
            foreach (var kv in _uvDims) destination._uvDims[kv.Key] = kv.Value;
            destination._topology.Clear();
            foreach (var kv in _topology) destination._topology[kv.Key] = kv.Value;
        }

        /// <summary>Render backend: channel 1's full-width values when they were authored wider than 2 (the shield meshes' face centroids), else null. Zero-copy.</summary>
        public Vector4[] RenderUv1Wide => _uvFull.TryGetValue(1, out var f) && f.Length == vertexCount ? f : null;

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

        public void SetColors(Color[] inColors) => colors = inColors;
        public void SetColors(Color32[] inColors) => colors32 = inColors;
        public void SetColors(List<Color32> inColors) => colors32 = inColors.ToArray();

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
