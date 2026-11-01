using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The geometry behind the spherical-blast omni-crystal morph (Butterfly, Rhino, Manta:
    /// <c>R_VesselActions/SPHERE_BLAST_CRYSTAL_MORPH.md</c>): the blast's growth curve read at the
    /// hand-off, and the cage cast onto the sphere from the CRYSTAL'S middle rather than the blast's.
    ///
    /// Negative control for the cast: casting from the sphere's centre (the Scarab's plain cast)
    /// fails <see cref="CastFromTheCrystalSpreadsTheCageOverTheSphere"/> — a crystal 30 units off a
    /// 375-unit sphere's centre piles onto one patch.
    /// </summary>
    public class SphereBlastCrystalMorphGeometryTests
    {
        readonly List<Mesh> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _spawned) if (m) Object.DestroyImmediate(m);
            _spawned.Clear();
        }

        Mesh Track(Mesh m) { _spawned.Add(m); return m; }

        // ── The growth curve is AOEExplosion.ExplodeAsync's ────────────────────────────────────

        [Test]
        public void EaseIsZeroThroughTheDelay()
        {
            Assert.AreEqual(0f, SphereBlastMorphGeometry.EaseAt(0f, 0.2f, 3f), 0f);
            Assert.AreEqual(0f, SphereBlastMorphGeometry.EaseAt(0.2f, 0.2f, 3f), 0f);
        }

        [Test]
        public void EaseFollowsTheBlastsSineAndHoldsAtFull()
        {
            // Butterfly bloom: no delay, 0.6 s. At the fleet's hand-off (0.85 × 0.44 s = 0.374 s).
            float t = 0.374f / 0.6f;
            Assert.AreEqual(Mathf.Sin(t * Mathf.PI * 0.5f), SphereBlastMorphGeometry.EaseAt(0.374f, 0f, 0.6f), 1e-6f);
            Assert.AreEqual(1f, SphereBlastMorphGeometry.EaseAt(0.6f, 0f, 0.6f), 1e-6f);
            Assert.AreEqual(1f, SphereBlastMorphGeometry.EaseAt(9f, 0f, 0.6f), 1e-6f);
        }

        [Test]
        public void EaseIsMonotonic()
        {
            float last = -1f;
            for (float s = 0f; s < 4f; s += 0.01f)
            {
                float e = SphereBlastMorphGeometry.EaseAt(s, 0.2f, 3f);
                Assert.GreaterOrEqual(e, last, $"the blast shrank at {s:F2}s");
                last = e;
            }
        }

        [Test]
        public void AZeroDurationBlastIsFullSizeAtOnce()
        {
            Assert.AreEqual(1f, SphereBlastMorphGeometry.EaseAt(0.5f, 0.2f, 0f), 1e-6f);
        }

        // ── The cast origin ────────────────────────────────────────────────────────────────────

        [Test]
        public void ACrystalWellInsideIsCastFromItsOwnMiddle()
        {
            var c = new Vector3(10f, 0f, 0f);
            Assert.AreEqual(0f, (SphereBlastMorphGeometry.CastCentre(Vector3.zero, 100f, c) - c).magnitude, 1e-6f);
        }

        [Test]
        public void ACrystalNearOrPastTheSurfaceIsClampedStrictlyInside()
        {
            var sphere = new Vector3(5f, 5f, 5f);
            foreach (float d in new[] { 60f, 99f, 150f, 1000f })
            {
                var c = sphere + new Vector3(0f, d, 0f);
                var got = SphereBlastMorphGeometry.CastCentre(sphere, 100f, c);
                Assert.AreEqual(100f * SphereBlastMorphGeometry.MaxCastOffsetFraction, (got - sphere).magnitude, 1e-3f);
                Assert.Greater(Vector3.Dot(got - sphere, c - sphere), 0f, "clamped toward the crystal");
            }
        }

        [Test]
        public void ASizelessSphereCastsFromItsCentre()
        {
            var sphere = new Vector3(1f, 2f, 3f);
            Assert.AreEqual(0f, (SphereBlastMorphGeometry.CastCentre(sphere, 0f, Vector3.zero) - sphere).magnitude, 0f);
        }

        // ── The cage lands on the sphere ───────────────────────────────────────────────────────

        const float SphereRadius = 375f;      // Butterfly bloom at the hand-off: 0.5 × 900 × 0.83
        static readonly Vector3 CrystalOffset = new Vector3(18f, -6f, 24f);   // hull to crystal centre

        static Mesh Cage(int struts, float shellRadius, Vector3 centreOffset, int seed)
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
                var centre = centreOffset + new Vector3(r * Mathf.Cos(th), u, r * Mathf.Sin(th))
                             * (shellRadius * (0.75f + 0.25f * (float)rnd.NextDouble()));
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

        /// <summary>The blast's sphere: radius 0.5 at unit scale, as Unity's built-in is, scaled to
        /// the hand-off size and centred on the hull (the origin here).</summary>
        Mesh BlastSphere() => Track(IcosphereMeshGenerator.Generate(3, 0.5f, true));

        List<Vector3> Landed(bool castFromCrystal, out Mesh cage)
        {
            cage = Track(Cage(122, 3f, CrystalOffset, 20261101));
            var toLocal = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * (2f * SphereRadius));
            Vector3 castFrom = castFromCrystal
                ? SphereBlastMorphGeometry.CastCentre(Vector3.zero, SphereRadius, cage.bounds.center)
                : Vector3.zero;

            Assert.IsTrue(CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                BlastSphere(), toLocal, castFrom, out var target, out string why), why);
            var morph = CrystalMorphMeshBuilder.TryBuild(cage, in target, 1f, 0f, out string diagnosis);
            Assert.IsNotNull(morph, diagnosis);
            Track(morph);

            var uv2 = new List<Vector4>();
            morph.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv2);
            var pts = new List<Vector3>(uv2.Count);
            foreach (var v in uv2) pts.Add(new Vector3(v.x, v.y, v.z));
            return pts;
        }

        [Test]
        public void EveryVertexLandsOnTheSphere()
        {
            foreach (var p in Landed(true, out _))
                Assert.AreEqual(SphereRadius, p.magnitude, SphereRadius * 0.01f,
                    "a vertex did not land on the blast's surface (subdivision-3 facets sit within 1%)");
        }

        [Test]
        public void CastFromTheCrystalSpreadsTheCageOverTheSphere()
        {
            // Coverage: the fraction of 26 axis/diagonal directions that some landed point sits near.
            float Coverage(List<Vector3> pts)
            {
                int hit = 0, total = 0;
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && y == 0 && z == 0) continue;
                    total++;
                    var dir = new Vector3(x, y, z).normalized;
                    foreach (var p in pts)
                        if (Vector3.Dot(p.normalized, dir) > 0.8f) { hit++; break; }
                }
                return (float)hit / total;
            }

            float fromCrystal = Coverage(Landed(true, out _));
            float fromCentre = Coverage(Landed(false, out _));
            Assert.Greater(fromCrystal, 0.9f, "cast from the crystal, the cage should open over the whole sphere");
            Assert.Less(fromCentre, 0.2f,
                "negative control: cast from the blast's centre, a crystal well off-centre lands on one patch");
        }

        [Test]
        public void TheCageOpensTheWayItFaces()
        {
            var pts = Landed(true, out var cage);
            var src = cage.vertices;
            var tris = cage.triangles;
            Vector3 mid = cage.bounds.center;
            int agree = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 from = src[tris[i]] - mid;
                if (Vector3.Dot(from.normalized, (pts[i] - mid).normalized) > 0.9f) agree++;
            }
            Assert.Greater((float)agree / pts.Count, 0.95f,
                "each vertex should travel along its own direction from the crystal's middle");
        }
    }
}
