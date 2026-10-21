#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Pistol's VIEW maths, which the lab does not have (its side view places each prism at one point): the drawing
    /// mapper that must agree with the lab's <see cref="PistolRibbon.Map"/>, the path's curvature, and the stretch of a
    /// prism the Goby circles (<see cref="PistolWarp"/>): a point at distance r from a lap's centre is beside the path
    /// over an arc R·Δθ, so its image along x is R / r times its size. The lap is built the way the game builds one:
    /// laid sample by sample along a circle.
    /// </summary>
    public class PistolWorldMathTests
    {
        const float R = 29f;   // the Squirrel's turn radius, the lap the Goby really flies
        static readonly PistolRibbon.LaySettings Lay = new() { SampleGap = 4f, BlockGap = 16f, HillEvery = 4f, HillAmp = 26f };

        static Vector3 OnCircle(float th) => new(R * Mathf.Cos(th), 0f, R * Mathf.Sin(th));
        static Vector3 Tangent(float th) => new(-Mathf.Sin(th), 0f, Mathf.Cos(th));

        /// <summary>1.2 laps round the origin, anticlockwise seen from above, laid 400 steps at a time.</summary>
        static PistolRibbon Lap(PistolUnroll mode = PistolUnroll.WorldUp)
        {
            var rb = new PistolRibbon(mode);
            Vector3 f0 = Tangent(0f), u = Vector3.up;
            rb.Begin(OnCircle(0f), f0, u, Vector3.Cross(f0, u).normalized, Lay);
            const int steps = 400; float total = 2f * Mathf.PI * 1.2f;
            for (int i = 1; i <= steps; i++)
            {
                float a = (i - 1) * total / steps, b = i * total / steps; var f = Tangent(b);
                rb.Lay(OnCircle(a), OnCircle(b), f, u, Vector3.Cross(f, u).normalized, Lay);
            }
            return rb;
        }

        struct Pose { public Vector3 Pos, F, U, R; }
        static Pose At(float t)
        {
            var pos = new Vector3(100f * Mathf.Sin(0.7f * t), 40f * Mathf.Sin(1.1f * t), -55f * t);
            var f = new Vector3(70f * Mathf.Cos(0.7f * t), 44f * Mathf.Cos(1.1f * t), -55f).normalized;
            var u0 = new Vector3(0.25f * Mathf.Sin(0.5f * t), 1f, 0f);
            var u = (u0 - f * Vector3.Dot(f, u0)).normalized;
            return new Pose { Pos = pos, F = f, U = u, R = Vector3.Cross(f, u).normalized };
        }
        /// <summary>The golden path of <c>GobyPistolMathTests</c> (the same formula), so the drawing mapper is held to the same ribbon.</summary>
        static PistolRibbon Golden(PistolUnroll mode)
        {
            var rb = new PistolRibbon(mode); var p0 = At(0f);
            rb.Begin(p0.Pos, p0.F, p0.U, p0.R, Lay);
            for (int k = 1; k <= 360; k++) { var a = At((k - 1) / 30f); var q = At(k / 30f); rb.Lay(a.Pos, q.Pos, q.F, q.U, q.R, Lay); }
            return rb;
        }

        [Test]
        public void Lay_WithANonPositiveSampleGap_StillTerminates()
        {
            // a mis-written asset (tSampleGap 0) would otherwise lay samples forever from Update; the lab has no input for the gap
            var rb = new PistolRibbon(PistolUnroll.WorldUp);
            var bad = new PistolRibbon.LaySettings { SampleGap = 0f, BlockGap = 16f, HillEvery = 4f, HillAmp = 26f };
            rb.Begin(Vector3.zero, Vector3.forward, Vector3.up, Vector3.right, bad);
            rb.Lay(Vector3.zero, new Vector3(0f, 0f, 2f), Vector3.forward, Vector3.up, Vector3.right, bad);
            Assert.Less(rb.Count, 3000, "the gap is floored, so a 2 u move lays a bounded number of samples");
            Assert.Greater(rb.Count, 1);
        }

        [Test]
        public void MapFast_AgreesWithTheLabsMap_AwayFromFolds([Values(PistolUnroll.WorldUp, PistolUnroll.ArcLength, PistolUnroll.GobyUp)] PistolUnroll mode)
        {
            var rb = Golden(mode);
            int checked_ = 0;
            for (float t = 0.3f; t < 11.5f; t += 0.37f)
                for (int j = 0; j < 4; j++)
                {
                    var q = At(t).Pos + new Vector3((j - 1.5f) * 17f, 11f - j * 7f, (j % 2 == 0 ? 9f : -13f));
                    var slow = rb.Map(q, -1e9f, 1e9f); if (!slow.Ok || slow.Ambiguous) continue;
                    var fast = rb.MapFast(q, -1e9f, 1e9f);
                    Assert.IsTrue(fast.Ok, $"{mode} t={t} j={j}: fast found nothing");
                    Assert.AreEqual(slow.X, fast.X, 1e-3f, $"{mode} t={t} j={j}: x"); Assert.AreEqual(slow.H, fast.H, 1e-3f, "h"); Assert.AreEqual(slow.D, fast.D, 1e-3f, "d");
                    Assert.AreEqual(slow.K, fast.K, "segment");
                    checked_++;
                }
            Assert.Greater(checked_, 80, "the comparison covered the path");
        }

        [Test]
        public void MapFast_HonoursTheWindow()
        {
            var rb = Golden(PistolUnroll.WorldUp);
            var q = At(6f).Pos + new Vector3(5f, 3f, 0f);
            var all = rb.MapFast(q, -1e9f, 1e9f);
            var windowed = rb.MapFast(q, all.X - 40f, all.X + 40f);
            Assert.AreEqual(all.X, windowed.X, 1e-3f, "inside its window the answer is the same");
            var far = rb.MapFast(q, all.X + 300f, all.X + 400f);
            Assert.IsTrue(far.Ok && far.X >= all.X + 299f, "outside its window the nearest segment OF THE WINDOW answers (the ribbon only exists on the screen)");
        }

        [Test]
        public void Curvature_OfALap_IsOneOverItsRadius_TowardItsCentre()
        {
            var rb = Lap();
            var centre = rb.Map(Vector3.zero, -1e9f, 1e9f);   // the lap's centre, R to one side of every segment
            Assert.IsTrue(centre.Ok); Assert.AreEqual(R, Mathf.Abs(centre.D), 0.5f, "the centre is R beside the path");
            int hits = 0;
            for (int k = 3; k < rb.Count - 4; k += 7)
            {
                float kappa = rb.Curvature(k);
                Assert.AreEqual(1f / R, Mathf.Abs(kappa), 0.05f / R, $"segment {k}: |curvature| = 1/R");
                Assert.AreEqual(Mathf.Sign(centre.D), Mathf.Sign(kappa), $"segment {k}: the bend is toward the centre's side");
                hits++;
            }
            Assert.Greater(hits, 5);   // 1.2 laps at 4 u a sample is ~55 segments
            var straight = Golden(PistolUnroll.WorldUp);
            Assert.Less(Mathf.Abs(straight.Curvature(2)), 1f / 150f, "the golden path's start is nearly straight");
        }

        [Test]
        public void CornerWindow_UsesAsin_AndClassifies()
        {
            const float gap = 4f, kappa = 1f / 29f;   // ρ = 29, the centre at d = +29
            Assert.AreEqual(6, PistolWarp.CornerWindow(5f, kappa, 19f, gap, 100), "r = 10: ρ·asin(0.5) = 15.2 u → 4 segments + 2");
            int w = PistolWarp.CornerWindow(10f, kappa, 29f - 12.5f, gap, 100);
            Assert.AreEqual(9, w, "hd = 10 at r = 12.5: ρ·asin(0.8) = 26.9 u → 7 + 2");
            Assert.Greater(w, Mathf.CeilToInt(10f / 12.5f * 29f / gap) + 2, "the small-angle form (23.2 u → 8) falls short here");
            Assert.AreEqual(2, PistolWarp.CornerWindow(5f, 0f, 19f, gap, 100), "a straight path");
            Assert.AreEqual(PistolWarp.Kind.Straight, PistolWarp.Classify(5f, 0f, 19f, gap, 100, out _));
            Assert.AreEqual(PistolWarp.Kind.Bend, PistolWarp.Classify(5f, kappa, 19f, gap, 100, out _));
            Assert.AreEqual(PistolWarp.Kind.Bar, PistolWarp.Classify(5f, kappa, 29f - 6.5f, gap, 100, out int bar), "r ≤ hd + half a sample holds the centre");
            Assert.AreEqual(100, bar, "and its window is everything");
            Assert.AreEqual(4, PistolWarp.CornerWindow(5f, kappa, 19f, gap, 4), "clamped to the ribbon's span");
            Assert.AreEqual(PistolWarp.Kind.Bend, PistolWarp.Classify(5f, kappa, -19f, gap, 100, out int outside), "outside the bend (r = 48) the corners barely spread: ρ·asin(5/48) = 3 u");
            Assert.AreEqual(3, outside);
        }

        [Test]
        public void OutsideTheBend_APrismCompresses()
        {
            var rb = Lap(); const float s = 2f;
            var corners = new Vector3[8]; var xhd = new Vector3[8];
            Vector3 c = new(-(R + 20f), 0f, 0f);   // 20 u outside the lap, opposite the seam
            PistolWarp.BoxCorners(c, Vector3.right * s, Vector3.up * s, Vector3.forward * s, corners);
            var centre = rb.Map(c, -1e9f, 1e9f); Assert.IsTrue(centre.Ok);
            int window = PistolWarp.CornerWindow(s * Mathf.Sqrt(3f), rb.Curvature(centre.K), centre.D, Lay.SampleGap, rb.Count);
            Assert.IsTrue(PistolWarp.WarpBox(rb, centre.K, corners, -1e9f, 1e9f, window, xhd));
            float expected = 2f * s * R / (R + 20f);   // the arc the box's 4 u subtend at r = 49, on the lap's radius
            Assert.AreEqual(expected, PistolWarp.XRange(xhd), expected * 0.12f + 0.3f, "4 u at r = 49 is 2.4 u along the lap");
            Assert.Less(PistolWarp.XRange(xhd), 2f * s, "compressed, not stretched");
        }

        [Test]
        public void LapExtent_IsTheLap_StopsAtTheExit_AndDoesNotJitter()
        {
            // 1.2 laps, then a 200 u straight exit along the tangent
            var rb = Lap(); float thEnd = 2f * Mathf.PI * 1.2f; Vector3 exitFrom = OnCircle(thEnd), f = Tangent(thEnd);
            var r = Vector3.Cross(f, Vector3.up).normalized;
            rb.Lay(exitFrom, exitFrom + f * 200f, f, Vector3.up, r, Lay);
            const float hd = 5.2f;
            var corners = new Vector3[8];
            PistolWarp.BoxCorners(Vector3.zero, Vector3.right * 3f, Vector3.up * 3f, Vector3.forward * 3f, corners);
            var centre = rb.Map(Vector3.zero, -1e9f, 1e9f);
            rb.LapExtent(Vector3.zero, centre.K, R + hd + 2f, -1e9f, 1e9f, out int kLo, out int kHi, out bool loByDist, out bool hiByDist);
            Assert.AreEqual(0, kLo, "the run starts where the ribbon starts (the lap's first segment)");
            Assert.IsFalse(loByDist, "it stopped at the ribbon's start, not on the distance");
            Assert.IsTrue(hiByDist, "and left the lap on the straight exit");
            float lap = 1.2f * 2f * Mathf.PI * R;
            Assert.AreEqual(lap, rb.X(kHi + 1), hd + Lay.SampleGap + 22f, "the run reaches the exit, overshooting it by less than the threshold's reach down the tangent");
            PistolWarp.BarExtent(rb, Vector3.zero, centre.K, R, hd, corners, -1e9f, 1e9f, out float xA, out float xB);
            Assert.AreEqual(0f, xA, 1e-3f);
            Assert.AreEqual(lap, xB, hd + Lay.SampleGap, "tightened to the corners' own x at the exit");
            // a nudge of the point moves each end by at most a sample; the same call twice is identical
            PistolWarp.BarExtent(rb, new Vector3(0.3f, 0f, 0f), centre.K, R, hd, corners, -1e9f, 1e9f, out float xA2, out float xB2);
            Assert.AreEqual(xA, xA2, Lay.SampleGap); Assert.AreEqual(xB, xB2, Lay.SampleGap + 0.5f);
            PistolWarp.BarExtent(rb, Vector3.zero, centre.K, R, hd, corners, -1e9f, 1e9f, out float xA3, out float xB3);
            Assert.AreEqual(xA, xA3); Assert.AreEqual(xB, xB3);
        }

        [Test]
        public void BeyondEnd_ForecastsAlongTheHeading()
        {
            var rb = new PistolRibbon(PistolUnroll.WorldUp);
            rb.Begin(Vector3.zero, Vector3.forward, Vector3.up, Vector3.right, Lay);
            rb.Lay(Vector3.zero, new Vector3(0f, 0f, 400f), Vector3.forward, Vector3.up, Vector3.right, Lay);
            var q = new Vector3(7f, 3f, rb.EndX + 40f);
            var ahead = rb.MapFast(q, -1e9f, 1e9f, true);
            Assert.IsTrue(ahead.Ok); Assert.AreEqual(rb.EndX + 40f, ahead.X, 0.1f, "40 u past the end along the heading"); Assert.AreEqual(7f, Mathf.Abs(ahead.D), 0.1f);
            var piled = rb.MapFast(q, -1e9f, 1e9f, false);
            Assert.AreEqual(rb.X(rb.Count - 1), piled.X, 0.1f, "without it the point piles up at the end (the lab's map)");
            var near = rb.MapNear(q, rb.Count - 2, 3, -1e9f, 1e9f, true);
            Assert.AreEqual(ahead.X, near.X, 1e-3f, "the corner mapper forecasts the same way");
        }

        [Test]
        public void TearGuard_FallsBackOnAFold()
        {
            // a WorldUp helix: radius 20, pitch 30 a turn - every pass projects onto the same circle
            var rb = new PistolRibbon(PistolUnroll.WorldUp);
            Vector3 At(float th) => new(20f * Mathf.Cos(th), 30f * th / (2f * Mathf.PI), 20f * Mathf.Sin(th));
            Vector3 Tan(float th) => new Vector3(-Mathf.Sin(th), 30f / (2f * Mathf.PI * 20f), Mathf.Cos(th)).normalized;
            rb.Begin(At(0f), Tan(0f), Vector3.up, Vector3.Cross(Tan(0f), Vector3.up).normalized, Lay);
            const int steps = 600; float total = 2f * Mathf.PI * 3f;
            for (int i = 1; i <= steps; i++) { float a = (i - 1) * total / steps, b = i * total / steps; var f = Tan(b); rb.Lay(At(a), At(b), f, Vector3.up, Vector3.Cross(f, Vector3.up).normalized, Lay); }
            // a box near the helix's axis, between the passes: its corners' nearest ground-track segments scatter along the lap
            var corners = new Vector3[8]; var xhd = new Vector3[8];
            Vector3 c = new(1.5f, 45f, 0f);
            PistolWarp.BoxCorners(c, Vector3.right * 2f, Vector3.up * 2f, Vector3.forward * 2f, corners);
            var centre = rb.Map(c, -1e9f, 1e9f); float hd = 2f * Mathf.Sqrt(3f), kappa = rb.Curvature(centre.K);
            int window = PistolWarp.CornerWindow(hd, kappa, centre.D, Lay.SampleGap, rb.Count);
            Assert.IsTrue(PistolWarp.WarpBox(rb, centre.K, corners, -1e9f, 1e9f, window, xhd));
            float bound = PistolWarp.TearBound(hd, kappa, centre.D, Lay.SampleGap);
            Assert.Greater(PistolWarp.XRange(xhd), bound, "the corners tore across the lap: beyond the honest bound");
            rb.Frame(centre.K, centre.T, out var T, out var U, out var Rr);
            PistolWarp.RigidCorners(new Vector3(centre.X, centre.H, centre.D), Vector3.right * 2f, Vector3.up * 2f, Vector3.forward * 2f, T, U, Rr, xhd);
            Assert.LessOrEqual(PistolWarp.XRange(xhd), 2f * hd + 1e-3f, "the rigid box is the prism's own size");
        }

        /// <summary>A 4 u box at distance r inside the lap: its image along x spans the arc its corners subtend, R·Δθ.</summary>
        [TestCase(4f)]
        [TestCase(10f)]
        [TestCase(6f)]
        [TestCase(20f)]
        public void APrismTheGobyCircles_StretchesByRoverR(float r)
        {
            var rb = Lap();
            const float s = 2f;
            var corners = new Vector3[8]; var xhd = new Vector3[8];
            Vector3 c = new(-r, 0f, 0f);   // opposite the seam (the lap starts and ends at +x)
            PistolWarp.BoxCorners(c, Vector3.right * s, Vector3.up * s, Vector3.forward * s, corners);
            var centre = rb.Map(c, -1e9f, 1e9f); Assert.IsTrue(centre.Ok);
            int window = PistolWarp.CornerWindow(s * Mathf.Sqrt(3f), rb.Curvature(centre.K), centre.D, Lay.SampleGap, rb.Count);
            Assert.IsTrue(PistolWarp.WarpBox(rb, centre.K, corners, -1e9f, 1e9f, window, xhd));
            float thMin = float.PositiveInfinity, thMax = float.NegativeInfinity;
            foreach (var p in corners) { float th = Mathf.Atan2(p.z, p.x); if (th < 0f) th += 2f * Mathf.PI; thMin = Mathf.Min(thMin, th); thMax = Mathf.Max(thMax, th); }
            float expected = R * (thMax - thMin), xMin = float.PositiveInfinity, xMax = float.NegativeInfinity;
            foreach (var p in xhd) { xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x); }
            Assert.AreEqual(expected, xMax - xMin, expected * 0.08f + 0.5f, $"r={r}: the box's x-extent is the arc its corners subtend");
            Assert.Greater(expected, 2f * s * 1.2f, "and that is wider than the box (it stretched)");
            // the control: a window too narrow for the bend cannot reach the far corners, so the stretch is lost
            if (r < 8f)   // at r = 10 a 2-segment window (±8 u, ±0.28 rad) already reaches the corners (±0.25 rad): the control only bites nearer the centre
            {
                var narrow = new Vector3[8];
                Assert.IsTrue(PistolWarp.WarpBox(rb, centre.K, corners, -1e9f, 1e9f, 2, narrow));
                float nMin = float.PositiveInfinity, nMax = float.NegativeInfinity;
                foreach (var p in narrow) { nMin = Mathf.Min(nMin, p.x); nMax = Mathf.Max(nMax, p.x); }
                Assert.Less(nMax - nMin, expected * 0.7f, "without the adaptive window the corners clamp to the nearby segments");
            }
        }

        [Test]
        public void APrismHoldingTheLapsCentre_IsTheWholeLap()
        {
            var rb = Lap();
            var corners = new Vector3[8]; var xhd = new Vector3[8];
            PistolWarp.BoxCorners(Vector3.zero, Vector3.right * 3f, Vector3.up * 3f, Vector3.forward * 3f, corners);
            var centre = rb.Map(Vector3.zero, -1e9f, 1e9f);
            int window = PistolWarp.CornerWindow(3f * Mathf.Sqrt(3f), rb.Curvature(centre.K), centre.D, Lay.SampleGap, rb.Count);
            Assert.AreEqual(rb.Count, window, "the window is everything");
            Assert.IsTrue(PistolWarp.WarpBox(rb, centre.K, corners, -1e9f, 1e9f, window, xhd));
            float xMin = float.PositiveInfinity, xMax = float.NegativeInfinity;
            foreach (var p in xhd) { xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x); }
            Assert.Greater(xMax - xMin, Mathf.PI * R * 0.9f, "its corners lie on opposite sides of the lap: the image spans at least half of it");
        }

        [Test]
        public void OnAStraightPath_NothingStretches()
        {
            var rb = new PistolRibbon(PistolUnroll.WorldUp);
            rb.Begin(Vector3.zero, Vector3.forward, Vector3.up, Vector3.right, Lay);
            rb.Lay(Vector3.zero, new Vector3(0f, 0f, 400f), Vector3.forward, Vector3.up, Vector3.right, Lay);
            var corners = new Vector3[8]; var xhd = new Vector3[8];
            PistolWarp.BoxCorners(new Vector3(30f, 5f, 200f), Vector3.right * 2f, Vector3.up * 2f, Vector3.forward * 2f, corners);
            var centre = rb.Map(corners[0], -1e9f, 1e9f);
            int window = PistolWarp.CornerWindow(2f * Mathf.Sqrt(3f), rb.Curvature(centre.K), centre.D, Lay.SampleGap, rb.Count);
            Assert.AreEqual(2, window);
            Assert.IsTrue(PistolWarp.WarpBox(rb, centre.K, corners, -1e9f, 1e9f, window, xhd));
            Assert.AreEqual(1f, PistolWarp.Stretch(xhd, corners) * Mathf.Sqrt(3f) / 1f * 0f + (xhd[4].x - xhd[0].x) / 4f, 1e-3f, "4 u along the path is 4 u along x");
            Assert.AreEqual(30f, Mathf.Abs(xhd[0].z), 2.5f, "30 u beside the path is |d| = 30 (give or take the box's half-size)");
        }

        [Test]
        public void Trilinear_ReturnsTheCornersAtTheCorners()
        {
            var box = new Vector3[8];
            for (int i = 0; i < 8; i++) box[i] = new Vector3(i * 10f, i * i, -i);
            Assert.AreEqual(box[0], PistolWarp.Trilinear(box, 0f, 0f, 0f));
            Assert.AreEqual(box[7], PistolWarp.Trilinear(box, 1f, 1f, 1f));
            Assert.AreEqual(box[5], PistolWarp.Trilinear(box, 1f, 0f, 1f));
            var mid = PistolWarp.Trilinear(box, 0.5f, 0.5f, 0.5f); Vector3 avg = Vector3.zero; foreach (var b in box) avg += b / 8f;
            Assert.AreEqual(avg.x, mid.x, 1e-4f); Assert.AreEqual(avg.y, mid.y, 1e-4f); Assert.AreEqual(avg.z, mid.z, 1e-4f);
        }

        [Test]
        public void Parallax_HalvesALayerAtDepthD_AndDoublesOneHalfwayToTheCamera()
        {
            const float D = 160f, scX = 500f, scY = 20f;
            var plane = PistolWarp.Parallax(scX + 100f, scY + 50f, 0f, scX, scY, D);
            Assert.AreEqual(100f, plane.x, 1e-4f); Assert.AreEqual(scY + 50f, plane.y, 1e-4f); Assert.AreEqual(0f, plane.z, 1e-6f);
            var back = PistolWarp.Parallax(scX + 100f, scY + 50f, D, scX, scY, D);
            Assert.AreEqual(50f, back.x, 1e-4f); Assert.AreEqual(scY + 25f, back.y, 1e-4f);
            var front = PistolWarp.Parallax(scX + 100f, scY + 50f, -D / 2f, scX, scY, D);
            Assert.AreEqual(200f, front.x, 1e-4f); Assert.AreEqual(scY + 100f, front.y, 1e-4f);
        }
    }
}
#endif
