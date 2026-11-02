using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The geometry behind the Shrike's omni-crystal morph (<c>R_VesselActions/SHRIKE_CRYSTAL_MORPH.md</c>): the cage
    /// folding into the nest flora's octahedral bud, which stands BESIDE the crystal (3 u above the collect point at the
    /// shipped missileLength 4), cast around the crystal's own middle by <see cref="CrystalMorphRecast"/>.
    ///
    /// Negative control: the plain cast from the bud's centre (<see cref="CrystalMorphMeshBuilder.TryBuild(Mesh, in CrystalMorphMeshBuilder.ConvexHullTarget, float, float, out string)"/>)
    /// fails <see cref="CastAroundTheCrystalCoversEveryFaceOfTheBud"/>: seen from the bud, the cage lies below it and
    /// lands on its underside only.
    /// </summary>
    public class ShrikeCrystalMorphGeometryTests
    {
        const float MissileLength = 4f;
        static readonly Vector3 BudCentre = new Vector3(0f, MissileLength * 0.75f, 0f);
        const float BudRadius = MissileLength * 0.3f;

        readonly List<Mesh> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _spawned) if (m) Object.DestroyImmediate(m);
            _spawned.Clear();
        }

        Mesh Track(Mesh m) { _spawned.Add(m); return m; }

        /// <summary>ShrikeMeshes.Bud: an octahedron of radius 1, flat, 8 faces.</summary>
        static Mesh Octahedron()
        {
            Vector3[] v = { new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1) };
            int[] faces = { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            var verts = new Vector3[faces.Length];
            var tris = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { verts[i] = v[faces[i]]; tris[i] = i; }
            var mesh = new Mesh { name = "Bud" };
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

        CrystalMorphMeshBuilder.ConvexHullTarget Bud()
        {
            var toLocal = Matrix4x4.TRS(BudCentre, Quaternion.identity, Vector3.one * BudRadius);
            Assert.IsTrue(CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                Track(Octahedron()), toLocal, BudCentre, out var target, out string why), why);
            return target;
        }

        Mesh Built(bool around, out Mesh cage, out CrystalMorphMeshBuilder.ConvexHullTarget target)
        {
            target = Bud();
            cage = Track(Cage(122, 1.5f, 20261101));
            string diagnosis;
            var mesh = around
                ? CrystalMorphRecast.TryBuildAround(cage, in target, cage.bounds.center, 1f, 0f, out diagnosis)
                : CrystalMorphMeshBuilder.TryBuild(cage, in target, 1f, 0f, out diagnosis);
            Assert.IsNotNull(mesh, diagnosis);
            return Track(mesh);
        }

        static List<Vector4> Uv(Mesh m, int channel)
        {
            var l = new List<Vector4>();
            m.GetUVs(channel, l);
            return l;
        }

        static int NearestFace(Vector3 p, in CrystalMorphMeshBuilder.ConvexHullTarget t, out float distance)
        {
            distance = float.MaxValue;
            int best = -1;
            for (int f = 0; f < t.FaceCount; f++)
            {
                float d = Mathf.Abs(Vector3.Dot(p - t.Corners[3 * f], t.Normals[f]));
                if (d < distance) { distance = d; best = f; }
            }
            return best;
        }

        [Test]
        public void FrameZeroIsStillTheCrystal()
        {
            var morph = Built(true, out var cage, out _);
            var src = cage.vertices;
            var tris = cage.triangles;
            var got = morph.vertices;
            for (int i = 0; i < got.Length; i++)
                Assert.AreEqual(0f, (got[i] - src[tris[i]]).magnitude, 1e-6f, $"vertex {i} moved before the morph began");
        }

        [Test]
        public void EveryVertexLandsOnTheBudWearingItsFaceNormal()
        {
            var morph = Built(true, out _, out var target);
            var uv2 = Uv(morph, CrystalMorphMeshBuilder.TargetUVChannel);
            var uv3 = Uv(morph, CrystalMorphMeshBuilder.TargetNormalUVChannel);
            for (int i = 0; i < uv2.Count; i++)
            {
                var p = new Vector3(uv2[i].x, uv2[i].y, uv2[i].z);
                var n = new Vector3(uv3[i].x, uv3[i].y, uv3[i].z);
                int f = NearestFace(p, in target, out float d);
                Assert.Less(d, 1e-4f, $"vertex {i} is off the bud's surface");
                Assert.AreEqual(1f, Vector3.Dot(n, target.Normals[f]), 1e-4f, $"vertex {i} lands with a foreign normal");
                Assert.Less((p - BudCentre).magnitude, BudRadius + 1e-3f, $"vertex {i} lands outside the bud");
            }
        }

        [Test]
        public void EachVertexKeepsItsDirectionFromTheCrystalsMiddle()
        {
            var morph = Built(true, out var cage, out _);
            var uv2 = Uv(morph, CrystalMorphMeshBuilder.TargetUVChannel);
            var src = cage.vertices;
            var tris = cage.triangles;
            Vector3 mid = cage.bounds.center;
            for (int i = 0; i < uv2.Count; i++)
            {
                Vector3 from = (src[tris[i]] - mid).normalized;
                Vector3 to = (new Vector3(uv2[i].x, uv2[i].y, uv2[i].z) - BudCentre).normalized;
                Assert.Greater(Vector3.Dot(from, to), 0.999f, $"vertex {i} was cast along the wrong direction");
            }
        }

        [Test]
        public void CastAroundTheCrystalCoversEveryFaceOfTheBud()
        {
            int Faces(bool around)
            {
                var morph = Built(around, out _, out var target);
                var hit = new HashSet<int>();
                foreach (var v in Uv(morph, CrystalMorphMeshBuilder.TargetUVChannel))
                    hit.Add(NearestFace(new Vector3(v.x, v.y, v.z), in target, out _));
                return hit.Count;
            }
            Assert.AreEqual(8, Faces(true), "cast around the crystal, the cage should cover the whole bud");
            Assert.LessOrEqual(Faces(false), 4, "negative control: the plain cast lands on the bud's underside only");
        }

        [Test]
        public void TheCullingBoundsCoverBothEnds()
        {
            var morph = Built(true, out var cage, out _);
            foreach (var v in Uv(morph, CrystalMorphMeshBuilder.TargetUVChannel))
                Assert.IsTrue(morph.bounds.Contains(new Vector3(v.x, v.y, v.z)), "a landing point is outside the bounds");
            foreach (var v in cage.vertices)
                Assert.IsTrue(morph.bounds.Contains(v), "a source vertex is outside the bounds");
        }
    }
}
