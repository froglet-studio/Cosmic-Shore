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
    /// Pure-geometry tests for <see cref="PaintingStrokeToolkit"/> — the library the grandiose
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
        public void PetalLoop_IsClosedAndCupped()
        {
            var petal = Tk.PetalLoop(Vector3.zero, Vector3.forward, Vector3.up, 140f, 56f, 0.35f, 5);
            Assert.Less(Vector3.Distance(petal[0], petal[^1]), 1e-2f, "petal loop is closed");
            var b = PaintingPresetLibrary.ComputeBounds(new List<PaintingStroke> { new() { points = petal } });
            Assert.Greater(b.size.y, 10f, "the cup lifts the petal out of its base plane");
        }
    }
}
#endif
