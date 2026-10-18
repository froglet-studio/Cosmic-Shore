#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Pistol's SCREEN: the parallax about the Pistol (its end-on body is one screen point at every depth, a far
    /// layer never moves with the Pistol's jump), the depth zones, what is on screen, the ground tiles on the line,
    /// the warm downslopes, the camera's targets, the flat frame and the off-screen arrow (<see cref="PistolWarp"/>).
    /// </summary>
    public class PistolViewMathTests
    {
        const float D = 160f, W = 460f, Aspect = 1.6f, Lift = 0.08f * W / Aspect;

        [Test]
        public void Parallax_IsCentredOnThePistolsX_AndTheCamerasHeight()
        {
            float px = 100f, scX = px + 0.2f * W, scY = 40f, cy = scY - Lift;
            foreach (float d in new[] { -80f, -49f, 0f, 49f, 135f, 480f })
            {
                var p = PistolWarp.Parallax(px, 77f, d, scX, scY, px, cy, D);
                Assert.AreEqual(px - scX, p.x, 1e-4f, $"d={d}: every point of the Pistol's axis draws at one screen x");
                Assert.AreEqual(d, p.z, 1e-6f);
            }
            var q = PistolWarp.Parallax(300f, cy, 480f, scX, scY, px, cy, D);
            Assert.AreEqual(cy, q.y, 1e-4f, "a point at the parallax centre's height stays there at any depth");
            Assert.AreEqual(0.5f, PistolWarp.Scale(D, D), 1e-6f, "the lab's pParallax: half size that deep");
            Assert.AreEqual(2f, PistolWarp.Scale(-D / 2f, D), 1e-6f);
        }

        [Test]
        public void TubeAxis_IsOneScreenPoint_WhenTheCameraHasSettled()
        {
            float px = 250f, py = 12f, scX = px + 0.2f * W, scY = py + Lift, cy = scY - Lift, half = 49f, bodyR = 5f;
            var near = PistolWarp.Parallax(px, py, -half, scX, scY, px, cy, D);
            var far = PistolWarp.Parallax(px, py, half, scX, scY, px, cy, D);
            Assert.AreEqual(near.x, far.x, 1e-4f); Assert.AreEqual(near.y, far.y, 1e-4f, "the caps share one screen point");
            Assert.AreEqual(1.441f, PistolWarp.Scale(-half, D), 0.002f, "the near cap's scale"); Assert.AreEqual(0.766f, PistolWarp.Scale(half, D), 0.002f, "the far cap's");
            // the drawn hit cross-section at a depth is the body's radius at that depth's scale: what is drawn inside the cone is hit
            foreach (float d in new[] { -40f, 0f, 30f })
            {
                float s = PistolWarp.Scale(d, D);
                var edge = PistolWarp.Parallax(px + (bodyR + 3f), py, d, scX, scY, px, cy, D);
                Assert.AreEqual((bodyR + 3f) * s, edge.x - near.x, 1e-3f, $"d={d}");
            }
        }

        [Test]
        public void Jump_DoesNotMoveTheBackground()
        {
            float px = 100f, scX = px + 0.2f * W, scY = 40f, cy = scY - Lift;
            var a = PistolWarp.Parallax(px + 300f, 90f, 400f, scX, scY, px, cy, D);
            // the Pistol jumps 60 u: px/py change nothing here but the (unchanged) camera centre; the far point stays put
            var b = PistolWarp.Parallax(px + 300f, 90f, 400f, scX, scY, px, cy, D);
            Assert.AreEqual(a.y - scY, b.y - scY, 1e-6f, "a far layer's screen height depends on the eased camera only");
            float s = PistolWarp.Scale(400f, D);
            Assert.AreEqual((90f - cy) * s + cy - scY, a.y - scY, 1e-4f);
        }

        [Test]
        public void Zone_FollowsTheTable()
        {
            const float band = 49f, vision = 45f;
            PistolWarp.Zone z;
            z = PistolWarp.ZoneOf(0f, band, vision, D, false); Assert.IsTrue(z.Drawn && z.Warped && z.Solid); Assert.AreEqual(1f, z.Reach); Assert.AreEqual(0f, z.Fog); Assert.AreEqual(1f, z.Alpha);
            z = PistolWarp.ZoneOf(49f, band, vision, D, false); Assert.AreEqual(1f, z.Reach, 1e-6f); Assert.AreEqual(0f, z.Fog, 1e-6f); Assert.IsTrue(z.Solid);
            z = PistolWarp.ZoneOf(55f, band, vision, D, false); Assert.AreEqual(0.5f, z.Reach, 1e-6f); Assert.IsTrue(z.Warped && z.Solid); Assert.Greater(z.Fog, 0f); Assert.AreEqual(1f, z.Alpha);
            z = PistolWarp.ZoneOf(61f, band, vision, D, false); Assert.AreEqual(0f, z.Reach, 1e-6f);
            z = PistolWarp.ZoneOf(135f, band, vision, D, false); Assert.AreEqual(0.35f, z.Fog, 1e-5f); Assert.IsTrue(z.Warped && z.Solid, "the near field's edge is still warped and solid");
            z = PistolWarp.ZoneOf(136f, band, vision, D, false); Assert.IsFalse(z.Warped); Assert.IsFalse(z.Solid); Assert.Less(z.Alpha, 1f);
            z = PistolWarp.ZoneOf(480f, band, vision, D, false); Assert.AreEqual(0.85f, z.Fog, 1e-5f); Assert.AreEqual(0.45f, z.Alpha, 1e-5f); Assert.IsTrue(z.Drawn);
            z = PistolWarp.ZoneOf(-49f, band, vision, D, false); Assert.IsTrue(z.Solid); Assert.AreEqual(1f, z.Alpha);
            z = PistolWarp.ZoneOf(-80f, band, vision, D, false); Assert.IsTrue(z.Drawn && z.Warped); Assert.IsFalse(z.Solid); Assert.AreEqual(0.08f, z.Alpha, 1e-5f);
            z = PistolWarp.ZoneOf(481f, band, vision, D, false); Assert.IsFalse(z.Drawn, "beyond the window");
            z = PistolWarp.ZoneOf(-81f, band, vision, D, false); Assert.IsFalse(z.Drawn);
            z = PistolWarp.ZoneOf(600f, band, vision, D, true); Assert.IsTrue(z.Drawn); Assert.AreEqual(480f, z.D, 1e-6f); Assert.AreEqual(0.35f, z.Alpha, 1e-6f); Assert.AreEqual(0f, z.Reach); Assert.IsFalse(z.Solid);
            z = PistolWarp.ZoneOf(-100f, band, vision, D, true); Assert.AreEqual(-80f, z.D, 1e-6f, "a primed prism beyond the window draws at its edge");
            z = PistolWarp.ZoneOf(10f, band, vision, D, false); PistolWarp.Forecast(ref z); Assert.AreEqual(0.55f, z.Fog, 1e-6f); Assert.AreEqual(0.6f, z.Alpha, 1e-6f); Assert.IsFalse(z.Solid);
        }

        [Test]
        public void OnScreen_KeepsAFarPrismParallaxBringsOnScreen()
        {
            float px = 100f, scX = px + 0.2f * W, scY = 40f, cy = scY - Lift;
            Assert.IsTrue(PistolWarp.OnScreen(scX + 800f, scY, 480f, scX, scY, px, cy, D, W, Aspect, 60f), "800 u out at a quarter scale is on screen");
            Assert.IsFalse(PistolWarp.OnScreen(scX + 800f, scY, 0f, scX, scY, px, cy, D, W, Aspect, 60f), "at the plane it is not");
            Assert.IsTrue(PistolWarp.OnScreen(scX + 230f + 50f, scY, 0f, scX, scY, px, cy, D, W, Aspect, 60f), "its size is allowed for at the edge");
        }

        [Test]
        public void GroundTile_LiesOnTheLine_KeepsItsDepth()
        {
            var xhd = new Vector3[8];
            float slope = Mathf.Tan(30f * Mathf.Deg2Rad);
            PistolWarp.TileCorners(100f, 20f, slope, 9.25f, Vector3.forward * 2f, Vector3.up * 1f, Vector3.right * 3f, Vector3.forward, Vector3.up, Vector3.right, xhd);
            Vector3 c = Vector3.zero; foreach (var v in xhd) c += v / 8f;
            Assert.AreEqual(100f, c.x, 1e-4f); Assert.AreEqual(20f, c.y, 1e-4f); Assert.AreEqual(9.25f, c.z, 1e-4f, "the tile keeps its own depth (the Goby lays two rails)");
            // the long axis follows the slope: corner 1 - corner 0 is the full x half-axis doubled
            var along = xhd[1] - xhd[0];
            Assert.AreEqual(Mathf.Atan(slope), Mathf.Atan2(along.y, along.x), 1e-4f, "tilted to the slope");
            Assert.AreEqual(4f, along.magnitude, 1e-4f, "its length along the path");
            Assert.AreEqual(6f, (xhd[4] - xhd[0]).magnitude, 1e-4f, "its width across");
            PistolWarp.TileCorners(0f, 0f, 0f, 0f, Vector3.forward * 2f, Vector3.up, Vector3.right, Vector3.forward, Vector3.up, Vector3.right, xhd);
            Assert.AreEqual(0f, (xhd[1] - xhd[0]).y, 1e-6f, "flat ground: axis-aligned");
        }

        [Test]
        public void DiveTint_WarmsDownslopesOnly()
        {
            Assert.AreEqual(0.6f, PistolWarp.DiveTint(-0.5f), 1e-6f);
            Assert.AreEqual(0.3f, PistolWarp.DiveTint(-0.25f), 1e-6f);
            Assert.AreEqual(0f, PistolWarp.DiveTint(0f), 1e-6f);
            Assert.AreEqual(0f, PistolWarp.DiveTint(0.5f), 1e-6f);
            Assert.AreEqual(0.6f, PistolWarp.DiveTint(-2f), 1e-6f);
        }

        [Test]
        public void SideCamTargets_AreTheLabs()
        {
            PistolWarp.SideCamTarget(100f, 30f, 50f, W, Aspect, out float tx, out float ty);
            Assert.AreEqual(100f + 0.2f * W, tx, 1e-4f);
            Assert.AreEqual(50f + 0.08f * W / Aspect, ty, 1e-4f, "above the ground when it is higher than the Pistol");
            PistolWarp.SideCamTarget(100f, 90f, 50f, W, Aspect, out _, out ty);
            Assert.AreEqual(90f + 0.08f * W / Aspect, ty, 1e-4f, "above the Pistol when it is higher");
        }

        [Test]
        public void FlatFrame_MapsTUR_AndRoundTrips()
        {
            Vector3 T = new Vector3(0.6f, 0.2f, -0.7f).normalized, U = Vector3.Cross(Vector3.Cross(T, Vector3.up), T).normalized, R = Vector3.Cross(T, U);
            var q = PistolWarp.FlatFrame(T, U, R);
            Near(T, q * Vector3.right, 1e-4f, "x → T"); Near(U, q * Vector3.up, 1e-4f, "y → U"); Near(R, q * Vector3.forward, 1e-4f, "z → R");
            var f = new Vector3(12f, -3f, 40f); var stage = new Vector3(0f, -40000f, 0f);
            Near(f, Quaternion.Inverse(q) * (stage + q * f - stage), 0.02f, "the plane point round-trips (float at 40 000 u is good to ~4 mm)");
        }

        [Test]
        public void ArrowPlace_OnlyWhenTheGobyIsOffScreen()
        {
            float scX = 500f, scY = 40f;
            Assert.IsTrue(PistolWarp.ArrowPlace(scX + 400f, 500f, scX, scY, W, Aspect, out float x, out float y));
            Assert.AreEqual(scX + 0.5f * W - 6f, x, 1e-4f); Assert.AreEqual(scY + 0.5f * W / Aspect - 8f, y, 1e-4f, "clamped to the screen");
            Assert.IsFalse(PistolWarp.ArrowPlace(scX + 100f, 40f, scX, scY, W, Aspect, out _, out _));
        }

        [Test]
        public void FrameAt_OnARibbonOfFewerThanTwoSamples_IsAFrameNotAThrow()
        {
            // the first frames after the Goby spawns: Amoebius caught X(k + 1) on a one-sample ribbon (2026-10-15)
            foreach (var mode in new[] { PistolUnroll.WorldUp, PistolUnroll.ArcLength, PistolUnroll.GobyUp })
            {
                var rb = new PistolRibbon(mode);
                rb.FrameAt(0f, out var T, out var U, out var R);
                Near(Vector3.forward, T, 1e-6f, mode + " empty: T"); Near(Vector3.up, U, 1e-6f, mode + " empty: U"); Near(Vector3.left, R, 1e-6f, mode + " empty: R");
                rb.AddSample(new Vector3(3f, 1f, -2f), Vector3.up, new Vector3(0.6f, 0f, 0.8f));
                rb.FrameAt(5f, out T, out U, out R);
                if (mode == PistolUnroll.GobyUp) { Near(Vector3.forward, T, 1e-6f, "GobyUp one sample: the world's forward"); }
                else { Near(new Vector3(0.6f, 0f, 0.8f), T, 1e-6f, mode + " one sample: its heading"); Near(new Vector3(-0.8f, 0f, 0.6f), R, 1e-6f, mode + " one sample: R across it"); }
                Near(Vector3.up, U, 1e-6f, mode + " one sample: U");
                rb.AddSample(new Vector3(9f, 1f, 6f), Vector3.up, new Vector3(0.6f, 0f, 0.8f));
                rb.FrameAt(5f, out T, out _, out _);
                Near(new Vector3(0.6f, 0f, 0.8f), T, 1e-5f, mode + " two samples: the segment's own frame");
            }
        }

        [Test]
        public void Frustum_ProjectsExactlyAsTheParallax()
        {
            // the eye at (cx, cy, -D): a point's screen fraction through the off-axis frustum equals the parallax formula's place on a W-wide screen
            float scX = 192f, scY = 40f, cx = 100f, cy = 17f, D = 160f, near = 0.5f;
            PistolWarp.Frustum(scX, scY, cx, cy, D, W, Aspect, near, out float l, out float r, out float b, out float t);
            Assert.Less(l, r); Assert.Less(b, t);
            foreach (var d in new[] { -70f, -20f, 0f, 49f, 160f, 470f })
                foreach (var x in new[] { 40f, 100f, 300f })
                {
                    float h = 27f + 0.3f * x;
                    float xv = x - cx, yv = h - cy, zv = D + d;                          // view space: right, up, into the depth
                    float u = (xv * near / zv - l) / (r - l), v = (yv * near / zv - b) / (t - b);
                    var p = PistolWarp.Parallax(x, h, d, scX, scY, cx, cy, D);
                    Near((p.x + 0.5f * W) / W, u, 1e-4f, $"x fraction at d={d} x={x}");
                    Near((p.y - scY + 0.5f * W / Aspect) / (W / Aspect), v, 1e-4f, $"y fraction at d={d} x={x}");
                }
        }

        [Test]
        public void FlatTransform_PutsABoxWhereRigidCornersDo()
        {
            Vector3 T = new Vector3(0.6f, 0.1f, 0.79f).normalized, U = Vector3.Cross(Vector3.Cross(T, Vector3.up), T).normalized, R = Vector3.Cross(T, U);
            var rot = Quaternion.LookRotation(new Vector3(0.3f, -0.2f, 0.9f), new Vector3(0.1f, 1f, 0.2f)); var scale = new Vector3(2f, 3f, 5f);
            var l2w = Matrix4x4.TRS(new Vector3(300f, 20f, -40f), rot, scale);
            Vector3 ax = (Vector3)(l2w * new Vector4(0.5f, 0f, 0f, 0f)), ay = (Vector3)(l2w * new Vector4(0f, 0.5f, 0f, 0f)), az = (Vector3)(l2w * new Vector4(0f, 0f, 0.5f, 0f));   // half-axes of a unit cube
            var at = new Vector3(120f, 30f, 12f);
            var want = new Vector3[8]; PistolWarp.RigidCorners(at, ax, ay, az, T, U, R, want);
            var m = PistolWarp.FlatTransform(l2w, T, U, R, at);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
                Near(want[i], m.MultiplyPoint3x4(c), 1e-4f, "corner " + i);
            }
        }

        [Test]
        public void TileTransform_PutsABlockWhereTileCornersDo()
        {
            Vector3 T = Vector3.forward, U = Vector3.up, R = Vector3.right;
            Vector3 ax = new Vector3(2.5f, 0f, 0f), ay = new Vector3(0f, 0.45f, 0f), az = new Vector3(0f, 0f, 6.5f);   // a block 5 x 0.9 x 13 along the path
            var want = new Vector3[8]; PistolWarp.TileCorners(200f, 15f, 0.4f, 7f, ax, ay, az, T, U, R, want);
            var m = PistolWarp.TileTransform(200f, 15f, 0.4f, 7f, ax, ay, az, T, U, R, new Vector3(2.5f, 0.45f, 6.5f), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -2.5f : 2.5f, (i & 2) == 0 ? -0.45f : 0.45f, (i & 4) == 0 ? -6.5f : 6.5f);
                Near(want[i], m.MultiplyPoint3x4(c), 1e-4f, "corner " + i);
            }
        }

        static void Near(float want, float got, float tol, string what) => Assert.AreEqual(want, got, tol, what);
        static void Near(Vector3 want, Vector3 got, float tol, string what)
        {
            Assert.AreEqual(want.x, got.x, tol, what + ".x"); Assert.AreEqual(want.y, got.y, tol, what + ".y"); Assert.AreEqual(want.z, got.z, tol, what + ".z");
        }
    }
}
#endif
