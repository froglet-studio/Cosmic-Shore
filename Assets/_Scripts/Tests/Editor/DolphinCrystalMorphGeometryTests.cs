using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The geometry behind the Dolphin's omni-crystal retirement: the crystal's cage bursting onto
    /// the Echo Obliteration cone (DOLPHIN_CRYSTAL_MORPH.md). The target is the blast's own
    /// PrimitiveCone — a closed 20-sided cone, apex +Y, base −Y, unit height and diameter — posed
    /// the way the blast draws it at the hand-off: hundreds of units long, opening forward from the
    /// hull's nose, far away from and much larger than the crystal.
    ///
    /// What these protect, each a property the animation needs rather than a number:
    /// the last frame is the cone's real surface wearing its real facet normals; the cage opens
    /// the way it FACES (each vertex keeps its direction from the crystal's middle); and the cage
    /// covers the cone instead of piling onto one patch of it — the failure the plain centre cast
    /// has against a distant target, which the contrast test measures.
    /// </summary>
    public class DolphinCrystalMorphGeometryTests
    {
        const int ConeSegments = 20;          // PrimitiveCone.asset: 20 base corners, 38 triangles
        const float ConeLength = 500f;        // ≈ the blast's reach at the hand-off, at rest Space
        const float ConeBase = 220f;          // ≈ its base diameter at mid charge
        static readonly Vector3 Apex = new Vector3(0f, 0f, 6f);   // the hull's nose, just ahead of the crystal

        readonly List<Mesh> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _spawned) if (m) Object.DestroyImmediate(m);
            _spawned.Clear();
        }

        Mesh Track(Mesh m) { _spawned.Add(m); return m; }

        /// <summary>The blast's mesh, vertex for vertex the shipped PrimitiveCone (decoded from the
        /// asset): a base ring of radius 0.5 at y = −0.5 fanned from corner 0, and the mantle fanned
        /// from the apex at y = +0.5.</summary>
        static Mesh PrimitiveCone()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < ConeSegments; i++)
            {
                float a = i * 2f * Mathf.PI / ConeSegments;
                verts.Add(new Vector3(0.5f * Mathf.Cos(a), -0.5f, 0.5f * Mathf.Sin(a)));
            }
            for (int i = 1; i < ConeSegments - 1; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }

            int apex = verts.Count;
            verts.Add(new Vector3(0f, 0.5f, 0f));
            int ring = verts.Count;
            for (int i = 0; i < ConeSegments; i++) verts.Add(verts[i]);
            for (int i = 0; i < ConeSegments; i++)
            {
                tris.Add(apex);
                tris.Add(ring + (i + 1) % ConeSegments);
                tris.Add(ring + i);
            }

            var mesh = new Mesh { name = "PrimitiveCone" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, false);
            return mesh;
        }

        /// <summary>
        /// The cone's object-to-crystal matrix, built the way AOEConicExplosion poses it: a container
        /// at the apex looking down +Z scaled (base, base, reach), the mesh child turned −90° about X
        /// (apex −Z) and offset +0.5 along the container's Z so the apex sits at the container origin.
        /// </summary>
        static Matrix4x4 ConeToCrystal()
        {
            var container = Matrix4x4.TRS(Apex, Quaternion.identity, new Vector3(ConeBase, ConeBase, ConeLength));
            // Euler(-90, 0, 0): mesh +Y → container −Z, mesh +Z → container +Y.
            var child = Matrix4x4.TRS(new Vector3(0f, 0f, 0.5f),
                                      Quaternion.LookRotation(Vector3.up, Vector3.back), Vector3.one);
            return container * child;
        }

        CrystalMorphMeshBuilder.ConvexHullTarget ConeTarget()
        {
            var toLocal = ConeToCrystal();
            Vector3 centre = toLocal.MultiplyPoint3x4(Vector3.zero);   // the mesh origin: the middle of the cone's axis
            Assert.IsTrue(CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                Track(PrimitiveCone()), toLocal, centre, out var target, out string diagnosis),
                $"the blast cone should read as a morph target: {diagnosis}");
            return target;
        }

        /// <summary>A stand-in for the omni crystal's cage: many disjoint box struts on a shell
        /// around the origin, as the shipped body is 122 disjoint solids.</summary>
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
                var centre = new Vector3(r * Mathf.Cos(th), u, r * Mathf.Sin(th))
                             * (shellRadius * (0.75f + 0.25f * (float)rnd.NextDouble()));
                float h = 0.06f * shellRadius;

                int b = verts.Count;
                for (int i = 0; i < 8; i++)
                    verts.Add(centre + new Vector3((i & 1) == 0 ? -h : h,
                                                   (i & 2) == 0 ? -h : h,
                                                   (i & 4) == 0 ? -h : h));
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

        Mesh Morph(out Mesh cage, out CrystalMorphMeshBuilder.ConvexHullTarget target, bool castAroundCrystal = true)
        {
            target = ConeTarget();
            cage = Track(Cage(122, 3f, 20261101));
            var mesh = castAroundCrystal
                ? CrystalMorphMeshBuilder.TryBuild(cage, in target, Vector3.zero, 1f, 0f, out string diagnosis)
                : CrystalMorphMeshBuilder.TryBuild(cage, in target, 1f, 0f, out diagnosis);
            Assert.IsNotNull(mesh, $"the builder should produce a mesh: {diagnosis}");
            return Track(mesh);
        }

        static List<Vector3> Landed(Mesh morph)
        {
            var uv2 = new List<Vector4>();
            morph.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv2);
            var list = new List<Vector3>(uv2.Count);
            foreach (var v in uv2) list.Add(new Vector3(v.x, v.y, v.z));
            return list;
        }

        [Test]
        public void TheConeIsClosedAndEveryFacetFacesOutward()
        {
            var target = ConeTarget();
            Assert.AreEqual(2 * ConeSegments - 2, target.FaceCount, "the blast's cone: an 18-triangle cap and a 20-triangle mantle");
            for (int f = 0; f < target.FaceCount; f++)
            {
                Vector3 centroid = (target.Corners[3 * f] + target.Corners[3 * f + 1] + target.Corners[3 * f + 2]) / 3f;
                Assert.Greater(Vector3.Dot(target.Normals[f], centroid - target.Centre), 0f,
                    $"facet {f} faces inward — the crystal would land on the cone wearing the inside of it");
            }
        }

        /// <summary>Frame 0 is the crystal exactly, however far away the target is.</summary>
        [Test]
        public void FrameZeroIsTheCrystalVertexForVertex()
        {
            var morph = Morph(out var cage, out _);
            var src = cage.vertices;
            var tris = cage.triangles;
            var got = morph.vertices;
            Assert.AreEqual(tris.Length, morph.vertexCount, "the morph mesh is emitted unshared");
            for (int i = 0; i < got.Length; i++)
                Assert.AreEqual(0f, (got[i] - src[tris[i]]).magnitude, 1e-5f, $"vertex {i} does not start where the crystal drew it");
        }

        /// <summary>Frame 1 is the cone's real surface wearing its real facet normals — what lets the
        /// blast take the surface at the hand-off.</summary>
        [Test]
        public void EveryVertexLandsOnTheConeWearingItsFacetNormal()
        {
            var morph = Morph(out _, out var target);
            var landed = Landed(morph);
            var uv3 = new List<Vector4>(); morph.GetUVs(CrystalMorphMeshBuilder.TargetNormalUVChannel, uv3);

            for (int i = 0; i < landed.Count; i++)
            {
                Vector3 p = landed[i];
                var n = new Vector3(uv3[i].x, uv3[i].y, uv3[i].z);

                // On a convex hull a surface point is on the plane of the facet it is closest to and
                // inside every other facet's plane.
                float worst = float.MinValue, best = float.MaxValue;
                int bestFace = -1;
                for (int f = 0; f < target.FaceCount; f++)
                {
                    float d = Vector3.Dot(p - target.Corners[3 * f], target.Normals[f]);
                    worst = Mathf.Max(worst, d);
                    if (Mathf.Abs(d) < best) { best = Mathf.Abs(d); bestFace = f; }
                }

                Assert.Less(best, 1e-2f, $"vertex {i} does not land on the cone's surface (units: world, cone {ConeLength} long)");
                Assert.Less(worst, 1e-2f, $"vertex {i} lands outside the cone");
                Assert.AreEqual(1f, Vector3.Dot(n, target.Normals[bestFace]), 1e-3f,
                    $"vertex {i} arrives with a normal that is not its facet's");
            }
        }

        /// <summary>The cage opens the way it FACES: each vertex lands along its own direction from
        /// the crystal's middle, cast from the cone's centroid — front plates to the front.</summary>
        [Test]
        public void EveryVertexKeepsItsDirectionFromTheCrystalsMiddle()
        {
            var morph = Morph(out var cage, out var target);
            var landed = Landed(morph);
            var src = cage.vertices;
            var tris = cage.triangles;

            for (int i = 0; i < landed.Count; i++)
            {
                Vector3 from = src[tris[i]].normalized;
                Vector3 to = (landed[i] - target.Centre).normalized;
                Assert.Greater(Vector3.Dot(from, to), 0.9999f,
                    $"vertex {i} turned on its way to the cone — the cage would land scrambled");
            }
        }

        /// <summary>
        /// The cage COVERS the cone: its landing points reach from near the apex to the base along
        /// the blast's axis. The plain centre cast — every crystal point seen from the cone's
        /// centroid, hundreds of units away — piles the whole cage onto one patch near the apex,
        /// which is the measured reason the Dolphin casts around the crystal's own middle.
        /// </summary>
        [Test]
        public void TheCageCoversTheConeInsteadOfPilingOntoOnePatch()
        {
            float aroundCrystal = AxialSpread(Landed(Morph(out _, out _)));
            float aroundCone = AxialSpread(Landed(Morph(out _, out _, castAroundCrystal: false)));

            Assert.Greater(aroundCrystal, 0.75f * ConeLength,
                $"cast around the crystal, the cage should span the cone apex to base (spread {aroundCrystal:F0})");
            Assert.Less(aroundCone, 0.1f * ConeLength,
                $"control: cast around the cone's centre it should collapse onto one patch (spread {aroundCone:F0}) — " +
                "if this fails the test no longer measures what it claims");
        }

        static float AxialSpread(List<Vector3> points)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in points) { lo = Mathf.Min(lo, p.z); hi = Mathf.Max(hi, p.z); }
            return hi - lo;
        }

        /// <summary>The overload is the Scarab's cast exactly when the crystal's middle IS the
        /// hull's centre — the ball forged at the crystal is unchanged by it.</summary>
        [Test]
        public void CastingAroundTheHullCentreIsTheOriginalCastBitForBit()
        {
            var ball = Track(IcosphereMeshGenerator.Generate(IcosphereMeshGenerator.DefaultSubdivisions, 0.5f, true));
            Assert.IsTrue(CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                ball, Matrix4x4.identity, Vector3.zero, out var target, out _));
            var cage = Track(Cage(40, 1.5f, 7));

            var original = Track(CrystalMorphMeshBuilder.TryBuild(cage, in target, 1f, 0f, out _));
            var overload = Track(CrystalMorphMeshBuilder.TryBuild(cage, in target, target.Centre, 1f, 0f, out _));

            var a = new List<Vector4>(); original.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, a);
            var b = new List<Vector4>(); overload.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, b);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].x, b[i].x, 0f); Assert.AreEqual(a[i].y, b[i].y, 0f);
                Assert.AreEqual(a[i].z, b[i].z, 0f); Assert.AreEqual(a[i].w, b[i].w, 0f);
            }
        }

        /// <summary>The culling envelope covers the crystal AND the far cone, or the morph is
        /// frustum-culled the moment the crystal leaves the screen while its plates are still flying.</summary>
        [Test]
        public void BoundsCoverTheCrystalAndTheCone()
        {
            var morph = Morph(out var cage, out var target);
            var b = morph.bounds;
            // Bounds store centre + extents, so a corner that DEFINES the box can round a hair outside
            // it at these coordinates; a millimetre of slack is nothing to the culler.
            const float Slack = 1e-3f;
            bool Inside(Vector3 p) =>
                p.x >= b.min.x - Slack && p.x <= b.max.x + Slack &&
                p.y >= b.min.y - Slack && p.y <= b.max.y + Slack &&
                p.z >= b.min.z - Slack && p.z <= b.max.z + Slack;
            Assert.IsTrue(Inside(cage.bounds.min) && Inside(cage.bounds.max), "bounds must contain the crystal");
            for (int i = 0; i < target.Corners.Length; i++)
                Assert.IsTrue(Inside(target.Corners[i]), $"bounds must contain the cone it lands on (corner {i} at {target.Corners[i]})");
        }
    }
}
