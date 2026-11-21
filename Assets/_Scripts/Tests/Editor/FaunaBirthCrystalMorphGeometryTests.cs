using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The geometry behind the fauna-birth omni-crystal morph (Angler, Jacks:
    /// <c>R_VesselActions/FAUNA_BIRTH_CRYSTAL_MORPH.md</c>): which of a newborn's hearts the crystal
    /// becomes, and the cage folding into a heart the size of the crystal itself, born up to 30 u
    /// away (the Jacks' worm is born <c>spawnBehind</c> = 30 u behind the hull).
    ///
    /// Negative control: the plain cast from the heart's centre fails
    /// <see cref="ADistantHeartIsCoveredOnEveryFace"/> — seen from a heart ten of its radii away, the
    /// whole cage lies in one direction and lands on the one face that looks back at it.
    /// </summary>
    public class FaunaBirthCrystalMorphGeometryTests
    {
        const float HeartRadius = 3f;                                         // about the crystal's own size
        static readonly Vector3 HeartCentre = new Vector3(4f, -2f, -30f);     // 30 u behind, a little off-axis

        readonly List<Mesh> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _spawned) if (m) Object.DestroyImmediate(m);
            _spawned.Clear();
        }

        Mesh Track(Mesh m) { _spawned.Add(m); return m; }

        // ── Which heart ────────────────────────────────────────────────────────────────────────

        [Test]
        public void NoHeartsPickNone()
        {
            Assert.AreEqual(-1, FaunaHeartMorphGeometry.NearestHeart(null, Vector3.zero));
            Assert.AreEqual(-1, FaunaHeartMorphGeometry.NearestHeart(new List<Vector3>(), Vector3.zero));
        }

        [Test]
        public void TheNearestSegmentsHeartIsPicked()
        {
            // A worm born behind the hull: its segments trail further back.
            var hearts = new List<Vector3> { new Vector3(0, 0, -40), new Vector3(0, 0, -30), new Vector3(0, 0, -50) };
            Assert.AreEqual(1, FaunaHeartMorphGeometry.NearestHeart(hearts, Vector3.zero));
        }

        [Test]
        public void ATieKeepsTheFirstSoEveryPeerAgrees()
        {
            var hearts = new List<Vector3> { new Vector3(5, 0, 0), new Vector3(-5, 0, 0) };
            Assert.AreEqual(0, FaunaHeartMorphGeometry.NearestHeart(hearts, Vector3.zero));
        }

        // ── The cage folds into the heart ──────────────────────────────────────────────────────

        static Mesh Octahedron()
        {
            Vector3[] v = { new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1) };
            int[] faces = { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            var verts = new Vector3[faces.Length];
            var tris = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { verts[i] = v[faces[i]]; tris[i] = i; }
            var mesh = new Mesh { name = "Heart" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, false);
            mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh Cage(int struts, float shellRadius, int seed)
        {
            var rnd = new System.Random(seed);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int[,] quads = { {0,1,3,2}, {4,6,7,5}, {0,2,6,4}, {1,5,7,3}, {0,4,5,1}, {2,3,7,6} };
            for (int s = 0; s < struts; s++)
            {
                float u = (float)(rnd.NextDouble() * 2 - 1);
                float th = (float)(rnd.NextDouble() * Mathf.PI * 2);
                float r = Mathf.Sqrt(Mathf.Max(0f, 1 - u * u));
                var centre = new Vector3(r * Mathf.Cos(th), u, r * Mathf.Sin(th)) * (shellRadius * (0.75f + 0.25f * (float)rnd.NextDouble()));
                float h = 0.06f * shellRadius;
                int b = verts.Count;
                for (int i = 0; i < 8; i++)
                    verts.Add(centre + new Vector3((i & 1) == 0 ? -h : h, (i & 2) == 0 ? -h : h, (i & 4) == 0 ? -h : h));
                for (int f = 0; f < 6; f++)
                {
                    tris.Add(b + quads[f, 0]); tris.Add(b + quads[f, 1]); tris.Add(b + quads[f, 2]);
                    tris.Add(b + quads[f, 0]); tris.Add(b + quads[f, 2]); tris.Add(b + quads[f, 3]);
                }
            }
            var mesh = new Mesh { name = "TestCage" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, false);
            mesh.RecalculateNormals();
            return mesh;
        }

        List<Vector3> Landed(bool aroundTheCrystal)
        {
            var cage = Track(Cage(122, HeartRadius, 20261101));
            var toLocal = Matrix4x4.TRS(HeartCentre, Quaternion.identity, Vector3.one * HeartRadius);
            Assert.IsTrue(CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                Track(Octahedron()), toLocal, HeartCentre, out var target, out string why), why);

            var morph = aroundTheCrystal
                ? CrystalMorphRecast.TryBuildAround(cage, in target, cage.bounds.center, 1f, 0f, out string diagnosis)
                : CrystalMorphMeshBuilder.TryBuild(cage, in target, 1f, 0f, out diagnosis);
            Assert.IsNotNull(morph, diagnosis);
            Track(morph);

            var uv2 = new List<Vector4>();
            morph.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv2);
            var pts = new List<Vector3>(uv2.Count);
            foreach (var v in uv2) pts.Add(new Vector3(v.x, v.y, v.z));
            return pts;
        }

        [Test]
        public void EveryVertexLandsOnTheHeart()
        {
            foreach (var p in Landed(true))
            {
                var q = p - HeartCentre;
                float l1 = Mathf.Abs(q.x) + Mathf.Abs(q.y) + Mathf.Abs(q.z);   // an octahedron's surface: |x|+|y|+|z| = r
                Assert.AreEqual(HeartRadius, l1, HeartRadius * 0.01f, $"{p} is not on the heart");
            }
        }

        [Test]
        public void ADistantHeartIsCoveredOnEveryFace()
        {
            // Which of the heart's eight faces (octants) some landed point sits on.
            int Faces(List<Vector3> pts)
            {
                var seen = new HashSet<int>();
                foreach (var p in pts)
                {
                    var q = p - HeartCentre;
                    seen.Add((q.x >= 0 ? 1 : 0) | (q.y >= 0 ? 2 : 0) | (q.z >= 0 ? 4 : 0));
                }
                return seen.Count;
            }

            Assert.AreEqual(8, Faces(Landed(true)), "cast around the crystal's middle, the cage closes over the whole heart");
            Assert.LessOrEqual(Faces(Landed(false)), 4,
                "negative control: cast from the heart, a crystal 30 u away lands on the side that faces it");
        }
    }
}
