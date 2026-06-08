#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// StellatedOctahedronMeshGenerator Tests — locks the two-tetrahedra model
    /// of the super-shield (Stella Octangula).
    ///
    /// WHY THIS MATTERS:
    /// The super-shielded prism is modeled as the union of two regular tetrahedra
    /// — 8 half-space planes for collision (ContainsPointLocal) and, since each
    /// big tetra face is coplanar with the spike faces it tiles, 8 opaque
    /// triangles for rendering instead of 24. These tests guard both invariants:
    ///   • the rendered mesh is exactly 8 triangles whose vertices are the 8
    ///     cube corners (spike tips), with outward-pointing flat-shaded normals;
    ///   • the containment test matches the stellation surface (octahedron core
    ///     and spike tips inside; concave valleys and beyond-tip points outside).
    /// If either drifts, the shield's silhouette or its overlap queries break.
    /// </summary>
    [TestFixture]
    public class StellatedOctahedronMeshGeneratorTests
    {
        private const float Tol = 1e-4f;

        // ---- Mesh topology -------------------------------------------------

        [Test]
        public void Mesh_Has8TrianglesAnd24Vertices()
        {
            Assert.AreEqual(8, StellatedOctahedronMeshGenerator.FACE_COUNT,
                "Super-shield must render as the 8 faces of the two tetrahedra (not 24 spike faces).");
            Assert.AreEqual(24, StellatedOctahedronMeshGenerator.VERTEX_COUNT);

            var mesh = StellatedOctahedronMeshGenerator.Generate(new Vector3(0.5f, 0.5f, 0.5f));
            try
            {
                Assert.AreEqual(24, mesh.vertexCount);
                Assert.AreEqual(24, mesh.triangles.Length, "8 triangles × 3 indices.");
                Assert.AreEqual(24, mesh.normals.Length);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Mesh_DistinctVerticesAreThe8CubeCorners()
        {
            var h = new Vector3(1f, 2f, 3f);
            float s = StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
            float a = h.x * s, b = h.y * s, c = h.z * s;

            var mesh = StellatedOctahedronMeshGenerator.Generate(h);
            try
            {
                var signTuples = new HashSet<(int, int, int)>();
                foreach (var v in mesh.vertices)
                {
                    // Every vertex is a spike tip: a cube corner (±a, ±b, ±c).
                    Assert.AreEqual(a, Mathf.Abs(v.x), Tol, "|x| must equal a.");
                    Assert.AreEqual(b, Mathf.Abs(v.y), Tol, "|y| must equal b.");
                    Assert.AreEqual(c, Mathf.Abs(v.z), Tol, "|z| must equal c.");
                    signTuples.Add((Sign(v.x), Sign(v.y), Sign(v.z)));
                }
                Assert.AreEqual(8, signTuples.Count,
                    "All 8 cube corners (spike tips) must be present exactly once as distinct positions.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Mesh_AllFaceNormalsPointOutwardAndAreUnitFlat()
        {
            var mesh = StellatedOctahedronMeshGenerator.Generate(new Vector3(0.5f, 1.25f, 0.75f));
            try
            {
                var verts = mesh.vertices;
                var norms = mesh.normals;

                for (int f = 0; f < StellatedOctahedronMeshGenerator.FACE_COUNT; f++)
                {
                    int i0 = f * 3, i1 = i0 + 1, i2 = i0 + 2;
                    Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];
                    Vector3 n = norms[i0];

                    // Flat shading: all 3 vertices share the face normal.
                    Assert.That((norms[i1] - n).magnitude, Is.LessThan(Tol));
                    Assert.That((norms[i2] - n).magnitude, Is.LessThan(Tol));
                    Assert.AreEqual(1f, n.magnitude, Tol, "Face normal must be unit length.");

                    // Each tetra is centered at the origin, so an outward normal
                    // has positive dot with the face centroid.
                    Vector3 centroid = (v0 + v1 + v2) * (1f / 3f);
                    Assert.Greater(Vector3.Dot(n, centroid), 0f, $"Face {f} normal points inward.");

                    // Stored normal must match the triangle winding (front face out).
                    Vector3 wound = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                    Assert.Greater(Vector3.Dot(wound, n), 1f - Tol,
                        $"Face {f} winding disagrees with its normal.");
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        // ---- Containment (two-tetrahedra union) ----------------------------

        [Test]
        public void Contains_OriginAndOctahedronCoreVertices_Inside()
        {
            var h = new Vector3(0.5f, 0.5f, 0.5f);
            float a = h.x * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
            float b = h.y * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
            float c = h.z * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

            Assert.IsTrue(Inside(Vector3.zero, h), "Origin is inside both tetrahedra.");

            // The inscribed-octahedron vertices are shared boundary points.
            Assert.IsTrue(Inside(new Vector3( a, 0, 0), h));
            Assert.IsTrue(Inside(new Vector3(-a, 0, 0), h));
            Assert.IsTrue(Inside(new Vector3(0,  b, 0), h));
            Assert.IsTrue(Inside(new Vector3(0, -b, 0), h));
            Assert.IsTrue(Inside(new Vector3(0, 0,  c), h));
            Assert.IsTrue(Inside(new Vector3(0, 0, -c), h));
        }

        [Test]
        public void Contains_AllSpikeTips_Inside()
        {
            var h = new Vector3(1f, 0.5f, 2f);
            float a = h.x * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
            float b = h.y * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
            float c = h.z * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

            for (int oct = 0; oct < 8; oct++)
            {
                float sx = (oct & 1) == 0 ? 1 : -1;
                float sy = (oct & 2) == 0 ? 1 : -1;
                float sz = (oct & 4) == 0 ? 1 : -1;
                var tip = new Vector3(sx * a, sy * b, sz * c);
                Assert.IsTrue(Inside(tip, h), $"Spike tip {tip} must be inside the stellation.");
            }
        }

        [Test]
        public void Contains_ConcaveValleyAndBeyondTip_Outside()
        {
            var h = new Vector3(0.5f, 0.5f, 0.5f);
            float a = h.x * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
            float b = h.y * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
            float c = h.z * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

            // Cube-edge midpoint between two spike tips — sits in the concave
            // valley between spikes, so it is NOT in either tetrahedron.
            Assert.IsFalse(Inside(new Vector3(a, b, 0), h), "Cube-edge midpoint is in the valley, outside.");

            // Just past a spike tip along the body diagonal.
            Assert.IsFalse(Inside(new Vector3(1.1f * a, 1.1f * b, 1.1f * c), h), "Beyond the spike tip is outside.");

            // Past the octahedron vertex along an axis (the farthest surface point on +x).
            Assert.IsFalse(Inside(new Vector3(1.5f * a, 0, 0), h), "Beyond the octahedron vertex is outside.");
        }

        // ---- helpers -------------------------------------------------------

        private static bool Inside(Vector3 localPoint, Vector3 halfExtents) =>
            StellatedOctahedronMeshGenerator.ContainsPointLocal(localPoint, halfExtents);

        private static int Sign(float v) => v >= 0f ? 1 : -1;
    }
}
#endif
