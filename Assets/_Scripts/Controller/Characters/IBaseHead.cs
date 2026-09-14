using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The head surface as a thing features can ask questions of: where is the surface along a
    /// ray from the head centre, and what UV does that direction map to. The procedural head
    /// answers analytically; an authored head answers by ray-casting its blended mesh.
    /// </summary>
    public interface IHeadSurface
    {
        /// <summary>Surface point along <paramref name="direction"/> from the head centre.</summary>
        Vector3 Sample(Vector3 direction);
        /// <summary>The head texture's UV for a direction from the head centre.</summary>
        Vector2 Uv(Vector3 direction);
        /// <summary>The inverse: the direction a UV of the head texture looks along.</summary>
        Vector3 Direction(Vector2 uv);
        /// <summary>The head's centre in head space (the ray origin).</summary>
        Vector3 Centre { get; }
    }

    /// <summary>
    /// The fixed part of a head: vertex count, triangles and UVs never change with shape.
    /// </summary>
    public sealed class HeadTopology
    {
        public int VertexCount;
        public int[] Triangles;
        public Vector2[] Uvs;
    }

    /// <summary>
    /// THE seam the whole spike stands on. Every consumer — the resolver, the assembler, the
    /// texture painter, the human control and the chimera path alike — sees a head only through
    /// this. <see cref="ProceduralBaseHead"/> is the shipped implementation; if the procedural
    /// human does not read as a person, the answer is one authored sculpt carrying blend shapes
    /// named after <see cref="HeadAxis"/> members behind <see cref="AuthoredBaseHead"/>, and the
    /// one-file change is which implementation <c>CharacterGenerationConfigSO.CreateBaseHead</c>
    /// returns. Nothing else knows the difference.
    /// </summary>
    public interface IBaseHead
    {
        HeadTopology Topology { get; }

        /// <summary>Vertex positions for a shape. Count == Topology.VertexCount, always.</summary>
        Vector3[] Evaluate(HeadShape shape);

        /// <summary>A sampler over the surface at this shape, for placing features and painting.</summary>
        IHeadSurface Surface(HeadShape shape);

        /// <summary>The named attachment sites this head offers.</summary>
        IReadOnlyList<HeadSiteSpec> Sites { get; }

        /// <summary>The site's direction from the head centre at this shape (LEFT instance).</summary>
        Vector3 SiteDirection(HeadSiteSpec site, HeadShape shape);
    }
}
