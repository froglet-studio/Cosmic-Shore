using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The switch rings' synthetic wire meshes, built at load from <see cref="SpindleWireRingGeometry"/>
    /// instead of read from the baked <c>Assets/_Models/Spindles/SpindleWireRing*.asset</c> files
    /// (2.6 MB of YAML; <c>Docs/GENOME_MESH_AT_LOAD.md</c>). Each tier is built once on first ask
    /// (about 2 ms for the near tier, measured outside Unity) and the same <see cref="Mesh"/> is
    /// returned to every caller: share it, never destroy it. Same vertex layout as the asset (one
    /// stream, stride 84: position, normal, tangent, uv0.xy, uv1/2/3.xyz), same uint16 indices,
    /// same bounds, bit-identical data. Nothing consumes it yet: the profile still references the
    /// assets until a consumer is switched (see the doc).
    /// </summary>
    public static class SpindleWireRingMesh
    {
        public enum Tier { Near = 0, Low = 1, Thread = 2 }

        static readonly Mesh[] Cache = new Mesh[3];

        static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3),
        };

        /// <summary>The shared mesh for a tier, built on first ask.</summary>
        public static Mesh Get(Tier tier)
        {
            int i = (int)tier;
            if (Cache[i]) return Cache[i];
            Cache[i] = Build(tier);
            return Cache[i];
        }

        /// <summary>A fresh, unshared mesh for a tier (tests; callers want <see cref="Get"/>).</summary>
        public static Mesh Build(Tier tier)
        {
            var r = tier switch
            {
                Tier.Near => SpindleWireRingGeometry.Build(SpindleWireRingGeometry.NearSegments, SpindleWireRingGeometry.Strands),
                Tier.Low => SpindleWireRingGeometry.Build(SpindleWireRingGeometry.LowSegments, SpindleWireRingGeometry.Strands),
                _ => SpindleWireRingGeometry.Build(SpindleWireRingGeometry.LowSegments, 0),
            };
            var mesh = new Mesh
            {
                name = tier switch { Tier.Near => "SpindleWireRing", Tier.Low => "SpindleWireRingLow", _ => "SpindleWireRingThread" },
                hideFlags = HideFlags.DontSave,
            };
            const MeshUpdateFlags quiet = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
            mesh.SetVertexBufferParams(r.VertexCount, Layout);
            mesh.SetVertexBufferData(r.Vertices, 0, 0, r.Vertices.Length, 0, quiet);
            mesh.SetIndexBufferParams(r.Indices.Length, IndexFormat.UInt16);
            mesh.SetIndexBufferData(r.Indices, 0, 0, r.Indices.Length, quiet);
            var bounds = new Bounds(
                new Vector3(r.BoundsCenter[0], r.BoundsCenter[1], r.BoundsCenter[2]),
                2f * new Vector3(r.BoundsExtent[0], r.BoundsExtent[1], r.BoundsExtent[2]));
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, r.Indices.Length) { bounds = bounds, vertexCount = r.VertexCount }, quiet);
            mesh.bounds = bounds;
            return mesh;
        }
    }
}
