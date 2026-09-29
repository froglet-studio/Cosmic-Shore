#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;
using Tk = CosmicShore.Gameplay.PaintingStrokeToolkit;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pure-geometry tests for <see cref="PaintingStrokeToolkit"/> - the library the grandiose
    /// "Connect the Dots" constructions are built from. These lock the load-bearing math: a
    /// deterministic PRNG, a divergence-free curl field that fills space, a mathematically exact
    /// soccer ball, closed torus knots, and flyable impressionist strokes.
    /// </summary>
    public class PaintingStrokeToolkitTests
    {
        static readonly Domains[] Paintable = { Domains.Jade, Domains.Ruby, Domains.Gold };

        [Test]
        public void Rng_IsDeterministicAcrossInstances()
        {
            var a = new Tk.Rng(1234);
            var b = new Tk.Rng(1234);
            for (int i = 0; i < 200; i++)
                Assert.AreEqual(a.Next01(), b.Next01(), "same seed must produce the same stream");

            var c = new Tk.Rng(1235);
            var d = new Tk.Rng(1234);
            bool differs = false;
            for (int i = 0; i < 50; i++) if (c.Next01() != d.Next01()) { differs = true; break; }
            Assert.IsTrue(differs, "nearby seeds must produce different streams");
        }

        [Test]
        public void CurlNoise_IsFiniteNormalizedAndVaries()
        {
            var rng = new Tk.Rng(7);
            var dirs = new HashSet<Vector3>();
            for (int i = 0; i < 200; i++)
            {
                Vector3 p = rng.OnUnitSphere() * 600f;
                Vector3 v = Tk.CurlNoise(p, 0.02f, 123);
                Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), "curl is finite");
                Assert.AreEqual(1f, v.magnitude, 1e-3f, "curl is normalized");
                dirs.Add(new Vector3(Mathf.Round(v.x * 50), Mathf.Round(v.y * 50), Mathf.Round(v.z * 50)));
            }
            Assert.Greater(dirs.Count, 40, "the flow field must vary across space, not be constant");
        }

        [Test]
        public void TorusKnot_IsClosedAndNonPlanar()
        {
            var knot = Tk.TorusKnot(3, 2, 300f, 110f, 240);
            Assert.Less(Vector3.Distance(knot[0], knot[^1]), 1e-2f, "a torus knot is a closed loop");

            var b = PaintingPresetLibrary.ComputeBounds(new List<PaintingStroke> { new() { points = knot } });
            Assert.Greater(b.size.y, 0.2f * b.size.x, "the knot has real out-of-plane extent");
        }

        [Test]
        public void SoccerBallFaces_Are12Pentagons20HexagonsAndPlanar()
        {
            Tk.SoccerBallFaces(out var pentagons, out var hexagons);
            Assert.AreEqual(12, pentagons.Count);
            Assert.AreEqual(20, hexagons.Count);

            foreach (var loop in pentagons)
            {
                Assert.AreEqual(6, loop.Length, "5 verts + closing point");
                AssertPlanar(loop);
            }
            foreach (var loop in hexagons)
            {
                Assert.AreEqual(7, loop.Length, "6 verts + closing point");
                AssertPlanar(loop);
            }
        }

        static void AssertPlanar(Vector3[] loop)
        {
            // Drop the repeated closing vertex; the face must be flat.
            var n = Vector3.Cross(loop[1] - loop[0], loop[2] - loop[0]).normalized;
            float d0 = Vector3.Dot(loop[0], n);
            for (int i = 0; i < loop.Length - 1; i++)
                Assert.AreEqual(d0, Vector3.Dot(loop[i], n), 1e-3f, "face vertices must be coplanar");
        }

        [Test]
        public void FibonacciSphere_PointsAreOnUnitSphereAndSeparated()
        {
            var pts = Tk.FibonacciSphere(80);
            Assert.AreEqual(80, pts.Length);
            foreach (var p in pts) Assert.AreEqual(1f, p.magnitude, 1e-3f);

            float minSep = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
                for (int j = i + 1; j < pts.Length; j++)
                    minSep = Mathf.Min(minSep, Vector3.Distance(pts[i], pts[j]));
            Assert.Greater(minSep, 0.1f, "no two points coincide");
        }

        [Test]
        public void ImpressionistStrokes_FillAVolumeWithFlyableStrokes()
        {
            const float scale = 600f;
            var rng = new Tk.Rng(99);
            var strokes = Tk.ImpressionistStrokes(120,
                r => r.OnUnitSphere() * scale,
                _ => Domains.Jade, rng, 555, 0.015f / 1f, 22f, 6, 12);

            Assert.Greater(strokes.Count, 0);
            var all = strokes.SelectMany(s => s.points).ToList();
            var b = PaintingPresetLibrary.ComputeBounds(strokes);
            Assert.Greater(b.size.x, scale, "fills 3D on x");
            Assert.Greater(b.size.y, scale, "fills 3D on y");
            Assert.Greater(b.size.z, scale, "fills 3D on z");

            foreach (var s in strokes)
                for (int i = 1; i < s.points.Count; i++)
                {
                    float seg = Vector3.Distance(s.points[i - 1], s.points[i]);
                    Assert.Greater(seg, 6f, "impressionist steps stay flyable");
                    Assert.Less(seg, 0.6f * 2f * scale);
                }
        }

        [Test]
        public void ImpressionistStrokes_AreDeterministic()
        {
            List<PaintingStroke> Build()
                => Tk.ImpressionistStrokes(40, r => r.InUnitBall() * 500f,
                    Tk.DomainRegions(3, 0.01f), new Tk.Rng(42), 17, 0.02f, 20f, 5, 10);

            var a = Build();
            var b = Build();
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].domain, b[i].domain);
                CollectionAssert.AreEqual(a[i].points, b[i].points);
                CollectionAssert.Contains(Paintable, a[i].domain);
            }
        }

        [Test]
        public void FirTree_And_MidpointRidge_HaveNoDegenerateSegments()
        {
            var tree = Tk.FirTree(Vector3.zero, Vector3.up, 200f, 70f, 11);
            AssertNoDegenerate(tree, "fir tree");

            var ridge = Tk.MidpointRidge(3, -500f, 500f, -100f, 900f, 220f, 0.85f, 6);
            Assert.AreEqual((1 << 6) + 1, ridge.Count, "midpoint displacement yields 2^iters+1 points");
            AssertNoDegenerate(ridge, "ridge");
        }

        static void AssertNoDegenerate(List<Vector3> pts, string what)
        {
            for (int i = 1; i < pts.Count; i++)
                Assert.Greater(Vector3.Distance(pts[i - 1], pts[i]), 0.5f, $"{what} has a degenerate segment");
        }

        [Test]
        public void TubeLongitudes_SealExactlyOnAClosedKnot()
        {
            // Parallel transport around a closed loop returns rotated (holonomy) - uncorrected, every
            // longitude ends ~30u off its own start on the trefoil tube. The fix must seal them.
            const float R = 335f, rT = 140f, tube = 52f;
            const int NP = 300;
            var spine = new List<Vector3>(NP + 1);
            for (int i = 0; i <= NP; i++)
            {
                float t = (i / (float)NP) * Mathf.PI * 2f;
                float ring = R + rT * Mathf.Cos(2f * t);
                spine.Add(new Vector3(ring * Mathf.Cos(3f * t), rT * Mathf.Sin(2f * t), ring * Mathf.Sin(3f * t)));
            }
            spine[NP] = spine[0];

            var longs = Tk.TubeLongitudes(spine, tube, 6, 1);
            Assert.AreEqual(6, longs.Count);
            foreach (var line in longs)
                Assert.Less(Vector3.Distance(line[0], line[^1]), 1e-3f,
                    "every longitude must close onto itself - the knot's strokes connect on their shared surface");
        }

        [Test]
        public void RideCheckpoints_AreSpacedAndIncludeBothEnds()
        {
            // A long straight line: checkpoints every ~spacing, none closer, ends included.
            var pts = new List<Vector3>();
            for (int i = 0; i <= 100; i++) pts.Add(new Vector3(i * 10f, 0f, 0f));
            var cps = Tk.RideCheckpoints(pts, 70f, 28f);

            Assert.AreEqual(0, cps[0], "the stroke start (gate) is always a checkpoint");
            Assert.AreEqual(100, cps[^1], "the stroke end (jack) is always a checkpoint");
            for (int i = 1; i < cps.Count; i++)
            {
                Assert.Greater(cps[i], cps[i - 1], "checkpoints advance monotonically");
                float arc = (cps[i] - cps[i - 1]) * 10f;
                if (i < cps.Count - 1)
                    Assert.GreaterOrEqual(arc, 70f, "checkpoints are never closer than the spacing");
            }
            Assert.Greater(cps.Count, 5, "a 1000u straight gets several checkpoints");
            Assert.Less(cps.Count, 20, "…but far fewer than the vertex count");
        }

        [Test]
        public void RideCheckpoints_AvoidTightCurvature()
        {
            // An L: straight, hairpin corner, straight. The corner vertex must NOT be a checkpoint.
            var pts = new List<Vector3>();
            for (int i = 0; i <= 20; i++) pts.Add(new Vector3(i * 10f, 0f, 0f));      // corner at idx 20
            for (int i = 1; i <= 20; i++) pts.Add(new Vector3(200f, 0f, i * 10f));
            var cps = Tk.RideCheckpoints(pts, 70f, 28f);
            CollectionAssert.DoesNotContain(cps, 20, "no checkpoint parked on the hairpin");
            Assert.AreEqual(pts.Count - 1, cps[^1]);
        }

        [Test]
        public void RideCheckpoints_NeverStallOnAnAllTightStroke()
        {
            // A tight circle - every vertex over the turn threshold. Forced checkpoints must still
            // appear (flattest-available), so progress cannot stall.
            var pts = new List<Vector3>();
            for (int i = 0; i <= 72; i++)
            {
                float a = i / 72f * Mathf.PI * 2f;
                pts.Add(new Vector3(Mathf.Cos(a) * 60f, 0f, Mathf.Sin(a) * 60f)); // ~5.2u segs, 25° turns? tight
            }
            var cps = Tk.RideCheckpoints(pts, 70f, 2f); // threshold 2° → everything is "tight"
            Assert.Greater(cps.Count, 2, "forced checkpoints appear on an all-tight stroke");
            Assert.AreEqual(72, cps[^1]);
        }

        static PaintingStroke Stroke(Domains dom, params Vector3[] pts)
            => new() { domain = dom, points = new List<Vector3>(pts) };

        static float TransitGaps(IReadOnlyList<PaintingStroke> s)
        {
            float sum = 0f;
            for (int i = 1; i < s.Count; i++)
                sum += Vector3.Distance(s[i - 1].points[^1], s[i].points[0]);
            return sum;
        }

        [Test]
        public void OrderForFlightContinuity_ChainsFlightAndStaysDomainContiguous()
        {
            // Deliberately shuffled: consecutive authored strokes are far apart, and the two
            // domains interleave. The tour must chain starts to ends and group the domains.
            var strokes = new List<PaintingStroke>
            {
                Stroke(Domains.Jade, new Vector3(0, 0, 0), new Vector3(100, 0, 0)),
                Stroke(Domains.Ruby, new Vector3(900, 0, 0), new Vector3(1000, 0, 0)),
                Stroke(Domains.Jade, new Vector3(110, 0, 0), new Vector3(200, 0, 0)),
                Stroke(Domains.Ruby, new Vector3(1010, 0, 0), new Vector3(1100, 0, 0)),
                Stroke(Domains.Jade, new Vector3(210, 0, 0), new Vector3(300, 0, 0)),
                Stroke(Domains.Ruby, new Vector3(1110, 0, 0), new Vector3(1200, 0, 0)),
            };

            var ordered = Tk.OrderForFlightContinuity(strokes);

            CollectionAssert.AreEquivalent(strokes, ordered, "a permutation of the same strokes");
            Assert.AreSame(strokes[0], ordered[0], "the authored opening stroke keeps its place");
            Assert.Less(TransitGaps(ordered), TransitGaps(strokes),
                "the tour must shorten the stroke-to-stroke transit");

            int switches = 0;
            for (int i = 1; i < ordered.Count; i++)
                if (ordered[i - 1].domain != ordered[i].domain) switches++;
            Assert.AreEqual(1, switches, "two domains → exactly one recolour seam");

            var again = Tk.OrderForFlightContinuity(strokes);
            CollectionAssert.AreEqual(ordered, again, "ordering is deterministic");
        }

        [Test]
        public void OrderForFlightContinuity_DefersCurvierStrokesOnNearTies()
        {
            // Two candidates start equally close to the opener's end: a straight run and a
            // zigzag of the same span. Continuity ties → the flatter stroke flies first.
            var zig = new List<Vector3>();
            for (int i = 0; i <= 10; i++) zig.Add(new Vector3(120 + i * 10f, i % 2 == 0 ? 0f : 25f, 0f));
            var strokes = new List<PaintingStroke>
            {
                Stroke(Domains.Jade, new Vector3(0, 0, 0), new Vector3(100, 0, 0)),
                Stroke(Domains.Jade, zig.ToArray()),
                Stroke(Domains.Jade, new Vector3(120, 0, 0), new Vector3(220, 0, 0)),
            };

            var ordered = Tk.OrderForFlightContinuity(strokes);
            Assert.AreSame(strokes[2], ordered[1], "the straight near-tie flies before the zigzag");
            Assert.AreSame(strokes[1], ordered[2], "the curvy stroke lands last");
        }

        [Test]
        public void RideCheckpoints_ClosedLoopsKeepAMidCheckpoint()
        {
            // A ring whose end sits at its own start (a Nautilus growth line): with only
            // [start, end] the stroke would complete unridden the moment the gate fires.
            var pts = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f * 0.93f; // 335-degree near-closed loop, ~120u arc
                pts.Add(new Vector3(Mathf.Cos(a) * 20f, Mathf.Sin(a) * 20f, 0f));
            }
            var cps = Tk.RideCheckpoints(pts, 90f, 28f);
            Assert.GreaterOrEqual(cps.Count, 3, "a closed loop always keeps a mid checkpoint");
            int mid = cps[1];
            Assert.Greater(mid, 0);
            Assert.Less(mid, pts.Count - 1);
            Assert.Greater(Vector3.Distance(pts[mid], pts[0]), 25f,
                "the forced checkpoint sits toward the loop's far side, not at the gate");
        }
    }
}
#endif
