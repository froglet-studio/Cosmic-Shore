using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// The ghost pilots' two faceted meshes, built in code from the genome lab's own vertex lists
    /// (amoebius-genome <c>agenome/render.py</c> <c>_mesh</c>): the <b>dart</b> body and the <b>pyramid</b> trail prism.
    /// Every triangle has its own three vertices and one face normal, so a lit material shades them as cut facets
    /// (nothing in the world looks flat: Docs/claude/SHADERS_AND_PERFORMANCE.md). Unit size; the flock scales them.
    /// </summary>
    public static class GhostPilotMeshes
    {
        /// <summary>The lab's dart: nose at +z, a raised spine and a keel.</summary>
        public static Mesh BuildDart() => BuildFaceted("GhostPilotDart",
            new[]
            {
                new Vector3(0f, 0f, 0.6f), new Vector3(-0.5f, 0f, -0.4f), new Vector3(0.5f, 0f, -0.4f),
                new Vector3(0f, 0.25f, -0.3f), new Vector3(0f, -0.12f, -0.35f),
            },
            new[] { 0, 3, 1, 0, 2, 3, 0, 1, 4, 0, 4, 2, 1, 3, 2, 1, 2, 4 });

        /// <summary>The lab's pyramid trail prism: a square base at y = -0.5, apex at +y.</summary>
        public static Mesh BuildPyramid() => BuildFaceted("GhostPilotTrailPrism",
            new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0f, 0.5f, 0f),
            },
            new[] { 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4, 0, 2, 1, 0, 3, 2 });

        /// <summary>
        /// Unshared vertices with one normal per face, wound outward from the origin (the lab's <c>_flat</c>: a face
        /// whose normal points toward the origin is flipped).
        /// </summary>
        public static Mesh BuildFaceted(string name, Vector3[] points, int[] tris)
        {
            var verts = new List<Vector3>(tris.Length);
            var normals = new List<Vector3>(tris.Length);
            var indices = new int[tris.Length];
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = points[tris[t]], b = points[tris[t + 1]], c = points[tris[t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, a + b + c) < 0f)
                {
                    (b, c) = (c, b);
                    n = -n;
                }
                n.Normalize();
                // Unity's front face is the one whose cross(b - a, c - a) points at the viewer, so this order
                // faces outward.
                verts.Add(a); verts.Add(b); verts.Add(c);
                normals.Add(n); normals.Add(n); normals.Add(n);
                indices[t] = t; indices[t + 1] = t + 1; indices[t + 2] = t + 2;
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
