using System;
using System.Collections.Generic;
using CosmicShore.Engine.Rendering;

namespace CosmicShore.Engine.Rendering
{
    /// <summary>One submesh's slice of the index buffer (original: UnityEngine.Rendering.SubMeshDescriptor).</summary>
    public struct SubMeshDescriptor
    {
        public Bounds bounds;
        public MeshTopology topology;
        public int indexStart;
        public int indexCount;
        public int baseVertex;
        public int firstVertex;
        public int vertexCount;

        public SubMeshDescriptor(int indexStart, int indexCount, MeshTopology topology = MeshTopology.Triangles)
        {
            this.indexStart = indexStart;
            this.indexCount = indexCount;
            this.topology = topology;
            bounds = default; baseVertex = 0; firstVertex = 0; vertexCount = 0;
        }
    }
}

namespace CosmicShore.Engine
{
    /// <summary>One mesh (one of its submeshes) to merge with <see cref="Mesh.CombineMeshes(CombineInstance[], bool, bool, bool)"/> (original: UnityEngine.CombineInstance).</summary>
    public struct CombineInstance
    {
        public Mesh mesh { get; set; }
        public int subMeshIndex { get; set; }
        public Matrix4x4 transform { get; set; }
        public Vector4 lightmapScaleOffset { get; set; }
        public Vector4 realtimeLightmapScaleOffset { get; set; }
    }

    public partial class Mesh
    {
        /// <summary>
        /// Original contract: submesh <paramref name="index"/>'s slice of the concatenated index buffer (the port stores
        /// one index array per submesh; <c>indexStart</c> is where it falls in <see cref="triangles"/>).
        /// </summary>
        public SubMeshDescriptor GetSubMesh(int index)
        {
            if ((uint)index >= (uint)subMeshCount) throw new IndexOutOfRangeException($"Mesh.GetSubMesh: submesh {index} of {subMeshCount}");
            int start = 0;
            for (int i = 0; i < index; i++) start += GetTriangles(i).Length;
            var tris = GetTriangles(index);
            int lo = int.MaxValue, hi = -1;
            foreach (int t in tris) { if (t < lo) lo = t; if (t > hi) hi = t; }
            var d = new SubMeshDescriptor(start, tris.Length, GetTopology(index));
            if (hi >= 0) { d.firstVertex = lo; d.vertexCount = hi - lo + 1; }
            d.bounds = bounds;
            return d;
        }

        public void SetColors(Color[] inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => colors = new ArraySegment<Color>(inColors, start, length).ToArray();
        public void SetColors(Color32[] inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => colors32 = new ArraySegment<Color32>(inColors, start, length).ToArray();
        public void SetColors(List<Color> inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => colors = inColors.GetRange(start, length).ToArray();
        public void SetColors(List<Color32> inColors, int start, int length, MeshUpdateFlags flags = MeshUpdateFlags.Default)
            => colors32 = inColors.GetRange(start, length).ToArray();

        public void SetIndices(int[] indices, int indicesStart, int indicesLength, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0)
            => SetIndices(new ArraySegment<int>(indices, indicesStart, indicesLength).ToArray(), topology, submesh, calculateBounds, baseVertex);
        public void SetIndices(List<int> indices, int indicesStart, int indicesLength, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0)
            => SetIndices(indices.GetRange(indicesStart, indicesLength).ToArray(), topology, submesh, calculateBounds, baseVertex);

        /// <summary>
        /// Original contract: replaces this mesh with the given meshes' geometry. Each instance contributes all of its
        /// source mesh's vertices (positions and normals transformed by its matrix when <paramref name="useMatrices"/>;
        /// uv, uv2 and colours copied) and the indices of its <c>subMeshIndex</c>; <paramref name="mergeSubMeshes"/>
        /// puts every instance in one submesh, otherwise each is a submesh of its own, in order. Lightmap data is ignored.
        /// </summary>
        public void CombineMeshes(CombineInstance[] combine, bool mergeSubMeshes = true, bool useMatrices = true, bool hasLightmapData = false)
        {
            if (combine == null) throw new ArgumentNullException(nameof(combine));
            var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var uv0 = new List<Vector2>(); var uv1 = new List<Vector2>();
            var col = new List<Color>();
            var subs = new List<int[]>(); var topo = new List<MeshTopology>();
            bool anyNormals = false, anyUv = false, anyUv2 = false, anyColors = false;
            foreach (var ci in combine) if (ci.mesh) { var m = ci.mesh; anyNormals |= m.normals.Length > 0; anyUv |= m.uv.Length > 0; anyUv2 |= m.uv2.Length > 0; anyColors |= m.colors.Length > 0; }
            var merged = new List<int>();
            foreach (var ci in combine)
            {
                var m = ci.mesh;
                if (!m || (uint)ci.subMeshIndex >= (uint)m.subMeshCount) continue;
                int baseV = pos.Count, n = m.vertexCount;
                var mx = ci.transform; var src = m.vertices; var sn = m.normals; var su = m.uv; var su2 = m.uv2; var sc = m.colors;
                for (int i = 0; i < n; i++)
                {
                    pos.Add(useMatrices ? mx.MultiplyPoint3x4(src[i]) : src[i]);
                    if (anyNormals) nrm.Add(i < sn.Length ? (useMatrices ? mx.MultiplyVector(sn[i]).normalized : sn[i]) : Vector3.zero);
                    if (anyUv) uv0.Add(i < su.Length ? su[i] : Vector2.zero);
                    if (anyUv2) uv1.Add(i < su2.Length ? su2[i] : Vector2.zero);
                    if (anyColors) col.Add(i < sc.Length ? sc[i] : Color.white);
                }
                var tris = m.GetTriangles(ci.subMeshIndex);
                var shifted = new int[tris.Length];
                for (int i = 0; i < tris.Length; i++) shifted[i] = tris[i] + baseV;
                if (mergeSubMeshes) merged.AddRange(shifted);
                else { subs.Add(shifted); topo.Add(m.GetTopology(ci.subMeshIndex)); }
            }
            Clear();
            vertices = pos.ToArray();
            if (anyNormals) normals = nrm.ToArray();
            if (anyUv) uv = uv0.ToArray();
            if (anyUv2) uv2 = uv1.ToArray();
            if (anyColors) colors = col.ToArray();
            if (mergeSubMeshes) { subMeshCount = 1; SetTriangles(merged.ToArray(), 0); }
            else
            {
                subMeshCount = subs.Count;
                for (int i = 0; i < subs.Count; i++) SetIndices(subs[i], topo[i], i, false);
            }
            RecalculateBounds();
        }
    }
}
