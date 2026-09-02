using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The geometry behind a vessel's bespoke omni-crystal retirement: a crystal's cage closing onto
    /// a convex hull (the Scarab's crystal becoming the ball it forged).
    ///
    /// Every assertion here is about a property the ANIMATION depends on, not about a number:
    /// frame 0 must be the crystal exactly, frame 1 must be the target's real surface wearing the
    /// target's real facet normals, and a solid must travel on one schedule. Each one has been
    /// negative-controlled — carrying the source normal instead of the facet's, and staggering
    /// per-vertex instead of per-solid, both fail this suite.
    /// </summary>
    public class CrystalMorphMeshBuilderTests
    {
        const float HullRadius = 0.5f;

        readonly List<Mesh> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _spawned) if (m) Object.DestroyImmediate(m);
            _spawned.Clear();
        }

        Mesh Track(Mesh m) { _spawned.Add(m); return m; }

        /// <summary>The ball's own hull, from the shipped generator at the ball collider's radius.</summary>
        Mesh BallHull() =>
            Track(IcosphereMeshGenerator.Generate(IcosphereMeshGenerator.DefaultSubdivisions, HullRadius, true));

        CrystalMorphMeshBuilder.ConvexHullTarget Target()
        {
            Assert.IsTrue(CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                BallHull(), Matrix4x4.identity, Vector3.zero, out var target, out string diagnosis),
                $"the ball's hull should read as a morph target: {diagnosis}");
            return target;
        }

        /// <summary>
        /// A stand-in for the omni crystal's cage. What matters is that it is MANY DISJOINT SOLIDS
        /// well outside the target — 122 of them, as the shipped model has — because the solid
        /// grouping is what keeps a strut rigid, and a single-shell source would not exercise it.
        /// </summary>
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

        Mesh BuiltMorph(out Mesh cage, out CrystalMorphMeshBuilder.ConvexHullTarget target,
                        float phaseNear = 1f, float phaseFar = 0f)
        {
            target = Target();
            cage = Track(Cage(122, HullRadius * 3f, 20260901));
            var mesh = CrystalMorphMeshBuilder.TryBuild(cage, in target, phaseNear, phaseFar, out string diagnosis);
            Assert.IsNotNull(mesh, $"the builder should produce a mesh: {diagnosis}");
            return Track(mesh);
        }

        [Test]
        public void EveryFacetNormalPointsOutward()
        {
            var target = Target();
            for (int f = 0; f < target.FaceCount; f++)
            {
                Vector3 centroid = (target.Corners[3 * f] + target.Corners[3 * f + 1] + target.Corners[3 * f + 2]) / 3f;
                Assert.Greater(Vector3.Dot(target.Normals[f], centroid), 0f,
                    $"facet {f} faces inward — both shaders read (1 - N.V)^4, so an inverted normal " +
                    "lands the crystal on the ball wearing the wrong surface");
            }
        }

        /// <summary>Frame 0 has to be the crystal EXACTLY, or the animation pops on the one frame
        /// that must be free.</summary>
        [Test]
        public void FrameZeroIsTheSourceVertexForVertex()
        {
            var morph = BuiltMorph(out var cage, out _);
            var srcVerts = cage.vertices;
            var srcTris = cage.triangles;
            var got = morph.vertices;

            Assert.AreEqual(srcTris.Length, morph.vertexCount,
                "the morph mesh is emitted UNSHARED — one vertex per source triangle corner");
            for (int i = 0; i < got.Length; i++)
                Assert.AreEqual(0f, (got[i] - srcVerts[srcTris[i]]).magnitude, 1e-6f,
                    $"vertex {i} does not start where the crystal drew it");
        }

        /// <summary>Frame 1 has to be the ball's REAL surface, not an approximation of it — that is
        /// what lets the ball take the surface at the hand-off with nothing to see.</summary>
        [Test]
        public void EveryVertexLandsOnTheHullWearingItsFacetNormal()
        {
            var morph = BuiltMorph(out _, out var target);
            var uv2 = new List<Vector4>(); morph.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv2);
            var uv3 = new List<Vector4>(); morph.GetUVs(CrystalMorphMeshBuilder.TargetNormalUVChannel, uv3);

            Assert.AreEqual(morph.vertexCount, uv2.Count, "UV2 carries one target position per vertex");
            Assert.AreEqual(morph.vertexCount, uv3.Count, "UV3 carries one target normal per vertex");

            for (int i = 0; i < uv2.Count; i++)
            {
                var p = new Vector3(uv2[i].x, uv2[i].y, uv2[i].z);
                var n = new Vector3(uv3[i].x, uv3[i].y, uv3[i].z);

                float best = float.MaxValue;
                int bestFace = -1;
                for (int f = 0; f < target.FaceCount; f++)
                {
                    float d = Mathf.Abs(Vector3.Dot(p - target.Corners[3 * f], target.Normals[f]));
                    if (d < best) { best = d; bestFace = f; }
                }

                Assert.Less(best, 1e-4f, $"vertex {i} does not land on the hull's surface");
                Assert.AreEqual(1f, Vector3.Dot(n, target.Normals[bestFace]), 1e-4f,
                    $"vertex {i} arrives with a normal that is not its facet's — the shape would be " +
                    "right and the shading nonsense");
            }
        }

        /// <summary>A SOLID travels on one schedule. Per-vertex phase would stretch every strut
        /// between two moments instead of moving it.</summary>
        [Test]
        public void EverySolidTravelsOnOneSchedule()
        {
            var morph = BuiltMorph(out _, out _);
            var uv2 = new List<Vector4>(); morph.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv2);

            // 8 verts -> 12 triangles -> 36 emitted corners per strut, contiguous in emit order.
            const int CornersPerStrut = 36;
            for (int s = 0; s * CornersPerStrut < uv2.Count; s++)
            {
                float phase = uv2[s * CornersPerStrut].w;
                for (int i = s * CornersPerStrut; i < (s + 1) * CornersPerStrut && i < uv2.Count; i++)
                    Assert.AreEqual(phase, uv2[i].w, 1e-6f, $"strut {s} is split across two schedules");
            }
        }

        /// <summary>Position and normal MUST share the phase. They are two Custom Function nodes
        /// reading one stamp, so a face whose normal is on its own schedule is shaded before or
        /// after it lands — the exact seam the normal carry exists to remove.</summary>
        [Test]
        public void PositionAndNormalCarryTheIdenticalPhase()
        {
            var morph = BuiltMorph(out _, out _);
            var uv2 = new List<Vector4>(); morph.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv2);
            var uv3 = new List<Vector4>(); morph.GetUVs(CrystalMorphMeshBuilder.TargetNormalUVChannel, uv3);

            for (int i = 0; i < uv2.Count; i++)
                Assert.AreEqual(uv2[i].w, uv3[i].w, 0f, $"vertex {i}'s shape and shading disagree on when to move");
        }

        /// <summary>The cascade spreads, and inverting the authored pair inverts it — which is the
        /// whole reason the phase is authored as two ends rather than a direction flag.</summary>
        [Test]
        public void ThePhaseSpanCoversTheAuthoredRangeAndInverts()
        {
            var outward = BuiltMorph(out _, out _, phaseNear: 1f, phaseFar: 0f);
            var inward = BuiltMorph(out _, out _, phaseNear: 0f, phaseFar: 1f);

            var a = new List<Vector4>(); outward.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, a);
            var b = new List<Vector4>(); inward.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, b);

            float aMin = 2f, aMax = -1f;
            for (int i = 0; i < a.Count; i++) { aMin = Mathf.Min(aMin, a[i].w); aMax = Mathf.Max(aMax, a[i].w); }
            Assert.GreaterOrEqual(aMin, 0f, "phases must stay inside [0,1] — the shader saturates them");
            Assert.LessOrEqual(aMax, 1f, "phases must stay inside [0,1] — the shader saturates them");
            Assert.Greater(aMax - aMin, 0.5f, "the cascade should actually spread across the window");

            for (int i = 0; i < a.Count && i < b.Count; i++)
                Assert.AreEqual(a[i].w, 1f - b[i].w, 1e-4f,
                    "swapping the authored ends must invert the cascade exactly");
        }

        /// <summary>An unreadable source is refused BY NAME rather than throwing. This runs inside an
        /// impact-effect dispatch, and an exception there unwinds a caller that has already minted
        /// the ball — leaving a ball with no crystal and no explanation.</summary>
        [Test]
        public void ANullSourceIsRefusedByName()
        {
            var target = Target();
            var mesh = CrystalMorphMeshBuilder.TryBuild(null, in target, 1f, 0f, out string diagnosis);
            Assert.IsNull(mesh, "a null source cannot produce a morph");
            Assert.IsNotEmpty(diagnosis, "the refusal must name what was wrong");
        }

        /// <summary>A malformed target is refused the same way.</summary>
        [Test]
        public void AnEmptyTargetIsRefusedByName()
        {
            var cage = Track(Cage(4, 1f, 1));
            var empty = new CrystalMorphMeshBuilder.ConvexHullTarget(Vector3.zero, null, null);
            var mesh = CrystalMorphMeshBuilder.TryBuild(cage, in empty, 1f, 0f, out string diagnosis);
            Assert.IsNull(mesh, "an empty hull cannot be landed on");
            Assert.IsNotEmpty(diagnosis, "the refusal must name what was wrong");
        }

        /// <summary>The culling envelope has to cover BOTH ends, or the mesh is frustum-culled
        /// mid-flight — a vertex-displacing animation whose bounds describe only its start.</summary>
        [Test]
        public void BoundsCoverBothEndsOfTheFlight()
        {
            var morph = BuiltMorph(out var cage, out var target);
            var b = morph.bounds;
            Assert.IsTrue(b.Contains(cage.bounds.min) && b.Contains(cage.bounds.max),
                "the morph's bounds must contain the crystal it starts as");
            for (int i = 0; i < target.Corners.Length; i++)
                Assert.IsTrue(b.Contains(target.Corners[i]),
                    "the morph's bounds must contain the hull it ends on");
        }
    }
}
