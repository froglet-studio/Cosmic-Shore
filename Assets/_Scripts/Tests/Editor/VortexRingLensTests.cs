#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The vortex ring lens's camera maths and registry (Docs/BLACK_HOLE.md §5.3). Its failures are silent: a ring
    /// projected with the field of view instead of the real matrices draws beside the ring on the Pistol's off-axis seat
    /// camera, and a ring seen edge-on that "degenerates" simply vanishes. Each check carries a control that shows it
    /// can fail. The per-pixel maths is held by <c>Tools/Shaders/verify_vortex_ring_lens.py</c>.
    /// </summary>
    public class VortexRingLensTests
    {
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/VortexRingLens.hlsl";

        static Matrix4x4 View(Vector3 eye, Vector3 forward, Vector3 up) =>
            Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(eye, Quaternion.LookRotation(forward, up), Vector3.one).inverse;

        static Vector2 Screen(Matrix4x4 projection, Matrix4x4 view, float aspect, Vector3 world, out float w)
        {
            Vector4 clip = projection * (view * new Vector4(world.x, world.y, world.z, 1f));
            w = clip.w;
            return new Vector2(clip.x / clip.w * aspect * 0.5f, clip.y / clip.w * 0.5f);
        }

        /// <summary>Distance of a screen point from the ellipse a row describes, as a share of its major semi-axis.</summary>
        static float OffEllipse(Vector4 rc, Vector4 ra, Vector2 p)
        {
            Vector2 e = new(ra.x, ra.y), f = new(-ra.y, ra.x), d = p - new Vector2(rc.x, rc.y);
            float lx = Vector2.Dot(d, e), ly = Vector2.Dot(d, f);
            if (rc.w < 0.002f * rc.z) return (Mathf.Abs(ly) + Mathf.Max(0f, Mathf.Abs(lx) - rc.z)) / rc.z;
            return Mathf.Abs(Mathf.Sqrt(lx * lx / (rc.z * rc.z) + ly * ly / (rc.w * rc.w)) - 1f) * rc.w / rc.z;
        }

        [Test]
        public void FaceOnRing_IsACircleOfTheProjectedRadius()
        {
            float aspect = 16f / 9f, depth = 120f, radius = 22f;
            var projection = Matrix4x4.Perspective(60f, aspect, 0.3f, 5000f);
            var view = View(Vector3.zero, Vector3.forward, Vector3.up);
            Assert.IsTrue(VortexRingLens.ScreenRing(projection, view, aspect, 0.3f, new Vector3(0f, 0f, depth), Vector3.forward,
                radius, radius * 0.16f, out var rc, out var ra, out var rz, out float w, out float fade));
            float expected = radius * projection.m11 * 0.5f / depth;
            Assert.AreEqual(0f, rc.x, 1e-5f);
            Assert.AreEqual(0f, rc.y, 1e-5f);
            Assert.AreEqual(expected, rc.z, expected * 1e-4f);
            Assert.AreEqual(expected, rc.w, expected * 1e-4f);
            Assert.AreEqual(1f / depth, rz.x, 1e-6f, "a face-on ring is all at one depth");
            Assert.AreEqual(0f, rz.y, 1e-6f);
            Assert.AreEqual(0f, rz.z, 1e-6f);
            Assert.AreEqual(depth, w, 1e-3f);
            Assert.AreEqual(1f, fade);

            // control: tilted 60 degrees, the same ring is an ellipse half as tall as it is wide
            Assert.IsTrue(VortexRingLens.ScreenRing(projection, view, aspect, 0.3f, new Vector3(0f, 0f, depth),
                Quaternion.AngleAxis(60f, Vector3.right) * Vector3.forward, radius, radius * 0.16f, out var tilted, out _, out _, out _, out _));
            Assert.AreEqual(0.5f, tilted.w / tilted.z, 0.02f);
        }

        [Test]
        public void OffAxisCamera_PutsTheRingWhereTheMatricesDo()
        {
            // the Pistol's seat lens: a window off the camera's axis (PistolPlane.OffAxis → Matrix4x4.Frustum)
            float aspect = 1.6f, near = 1f;
            var projection = Matrix4x4.Frustum(0.35f, 1.15f, -0.45f, 0.05f, near, 5000f);
            var eye = new Vector3(5f, 2f, -3f);
            var look = Quaternion.LookRotation(new Vector3(0.2f, -0.1f, 1f), Vector3.up);
            var view = View(eye, look * Vector3.forward, Vector3.up);
            // in the middle of the off-axis window: right of and below the camera's axis
            var centre = eye + look * (new Vector3(0.75f, -0.2f, 1f) * 200f);
            var axis = new Vector3(0.6f, 0.3f, -0.5f).normalized;
            Assert.IsTrue(VortexRingLens.ScreenRing(projection, view, aspect, near, centre, axis, 22f, 3.5f,
                out var rc, out var ra, out _, out _, out _));

            Vector3 e1 = Vector3.Cross(axis, Vector3.up).normalized, e2 = Vector3.Cross(axis, e1);
            float worst = 0f;
            for (int k = 0; k < 64; k++)
            {
                float t = k / 64f * Mathf.PI * 2f;
                var p = Screen(projection, view, aspect, centre + 22f * (Mathf.Cos(t) * e1 + Mathf.Sin(t) * e2), out _);
                worst = Mathf.Max(worst, OffEllipse(rc, ra, p));
            }
            Assert.Less(worst, 1e-3f, "every point of the ring lies on the ellipse");
            Assert.Less(Vector2.Distance(new Vector2(rc.x, rc.y), Screen(projection, view, aspect, centre, out _)), rc.z,
                "the ellipse is round the ring's own projected centre");

            // control: through the field of view alone (a symmetric lens, the black hole's ScreenWell) the ring lands
            // more than a screen height away
            float fovY = 2f * Mathf.Atan(1f / projection.m11) * Mathf.Rad2Deg;
            var symmetric = Matrix4x4.Perspective(fovY, aspect, near, 5000f);
            Assert.Greater(Vector2.Distance(new Vector2(rc.x, rc.y), Screen(symmetric, view, aspect, centre, out _)), 1f);
        }

        [Test]
        public void EdgeOnRing_IsASegmentAndStillDraws()
        {
            // the Pistol's side view: the ring's axis lies in the picture and its plane holds the eye
            float aspect = 16f / 9f;
            var projection = Matrix4x4.Perspective(50f, aspect, 0.3f, 5000f);
            var view = View(Vector3.zero, Vector3.forward, Vector3.up);
            var centre = new Vector3(0f, 0f, 150f);
            Assert.IsTrue(VortexRingLens.ScreenRing(projection, view, aspect, 0.3f, centre, Vector3.right, 22f, 3.5f,
                out var rc, out var ra, out var rz, out _, out _));
            Assert.Less(rc.w, rc.z * 1e-3f, "seen edge-on the ellipse closes to its major axis");
            Assert.AreEqual(1f, Mathf.Abs(ra.y), 1e-3f, "the segment stands across the screen, along the ring's diameter");
            Assert.Greater(rc.z, 0.05f);
            Assert.Greater(ra.z, 0f, "the bend still reaches out from the segment");
            Assert.Greater(Mathf.Abs(rz.z), 0f, "the near and far halves of the ring sit at different depths");

            // control: a quarter turn of the axis toward the camera opens it into the full circle
            Assert.IsTrue(VortexRingLens.ScreenRing(projection, view, aspect, 0.3f, centre, Vector3.forward, 22f, 3.5f, out var open, out _, out _, out _, out _));
            Assert.AreEqual(open.z, open.w, open.z * 1e-3f);
        }

        [Test]
        public void RingAtTheNearPlane_IsNotDrawn()
        {
            float aspect = 1.5f;
            var projection = Matrix4x4.Perspective(60f, aspect, 0.3f, 5000f);
            var view = View(Vector3.zero, Vector3.forward, Vector3.up);
            // the ring's lower edge passes behind the camera
            Assert.IsFalse(VortexRingLens.ScreenRing(projection, view, aspect, 0.3f, new Vector3(0f, 0f, 10f), Vector3.up, 22f, 3.5f,
                out _, out _, out _, out _, out _));
            Assert.IsFalse(VortexRingLens.ScreenRing(projection, view, aspect, 0.3f, new Vector3(0f, 0f, -80f), Vector3.forward, 22f, 3.5f,
                out _, out _, out _, out _, out _), "behind the camera");
            // fading as it closes on the near plane
            Assert.IsTrue(VortexRingLens.ScreenRing(projection, view, aspect, 0.3f, new Vector3(0f, 0f, 24f), Vector3.up, 22f, 3.5f,
                out _, out _, out _, out _, out float fade));
            Assert.Less(fade, 1f);
            Assert.Greater(fade, 0f);
            // control: far in front, it draws at full strength
            Assert.IsTrue(VortexRingLens.ScreenRing(projection, view, aspect, 0.3f, new Vector3(0f, 0f, 200f), Vector3.up, 22f, 3.5f,
                out _, out _, out _, out _, out float far));
            Assert.AreEqual(1f, far);
        }

        [Test]
        public void LifeFade_SwellsInAndFadesOut()
        {
            Assert.AreEqual(0f, VortexRingLens.LifeFade(0f, 2.6f));
            Assert.AreEqual(1f, VortexRingLens.LifeFade(1.3f, 1.3f));
            Assert.AreEqual(0f, VortexRingLens.LifeFade(2.6f, 0f));
            Assert.Less(VortexRingLens.LifeFade(0.1f, 2.5f), VortexRingLens.LifeFade(0.2f, 2.4f));
            Assert.Less(VortexRingLens.LifeFade(2.4f, 0.2f), VortexRingLens.LifeFade(2.3f, 0.3f));
            // control: with no fade-in the ring is at full strength the moment it is born
            Assert.AreEqual(1f, VortexRingLens.LifeFade(0f, 2.6f, 0f));
        }

        [Test]
        public void Registry_KeepsOneRingPerKey_AndDrawsTheNearestFirst()
        {
            var go = new GameObject("VortexRingLensTests camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 5000f;
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                VortexRingLens.Clear();

                var keys = new object[12];
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i] = new object();
                    // farthest first, so the bank has to sort
                    VortexRingLens.Set(keys[i], new Vector3(0f, 0f, 1000f - i * 60f), Vector3.forward, 22f, 3.5f, 1f, VortexRingLens.DefaultTint);
                }
                VortexRingLens.Set(keys[0], new Vector3(0f, 0f, 1000f), Vector3.forward, 22f, 3.5f, 1f, VortexRingLens.DefaultTint);
                Assert.AreEqual(12, VortexRingLens.Count, "a second Set with the same key moves the ring, it adds none");

                var c = new Vector4[VortexRingLens.MaxRings];
                var a = new Vector4[VortexRingLens.MaxRings];
                var z = new Vector4[VortexRingLens.MaxRings];
                var l = new Vector4[VortexRingLens.MaxRings];
                var t = new Vector4[VortexRingLens.MaxRings];
                int n = VortexRingLens.ScreenRings(cam, c, a, z, l, t);
                Assert.AreEqual(VortexRingLens.MaxRings, n, "the bank is capped");
                for (int i = 1; i < n; i++) Assert.Greater(c[i - 1].z, c[i].z, "nearest first: a nearer ring is larger on screen");
                Assert.AreEqual(22f * cam.projectionMatrix.m11 * 0.5f / (1000f - 11 * 60f), c[0].z, 1e-4f, "the nearest ring is first");

                Assert.IsTrue(VortexRingLens.Remove(keys[11]));
                Assert.IsFalse(VortexRingLens.Remove(keys[11]), "removing twice is a no-op");
                Assert.IsFalse(VortexRingLens.Remove(new object()));
                VortexRingLens.Set(keys[3], new Vector3(0f, 0f, 1000f - 3 * 60f), Vector3.forward, 22f, 3.5f, 0f, VortexRingLens.DefaultTint);
                for (int i = 0; i < 11; i++)
                    if (i != 3) VortexRingLens.Remove(keys[i]);
                // control: a ring with no strength is live but draws nothing, so the pass (and its copy) is skipped
                Assert.AreEqual(1, VortexRingLens.Count);
                Assert.AreEqual(0, VortexRingLens.ScreenRings(cam, c, a, z, l, t));
            }
            finally
            {
                VortexRingLens.Clear();
                Object.DestroyImmediate(go);
            }
            Assert.AreEqual(0, VortexRingLens.Count);
        }

        [Test]
        public void ShaderBank_MatchesTheCSharp()
        {
            string hlsl = File.ReadAllText(HlslPath);
            Assert.AreEqual(VortexRingLens.MaxRings, int.Parse(Regex.Match(hlsl, @"#define VORTEX_RING_LENS_MAX_RINGS (\d+)").Groups[1].Value));
            Assert.AreEqual(VortexRingLens.Reach, float.Parse(Regex.Match(hlsl, @"#define VORTEX_RING_LENS_REACH ([\d.]+)").Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture));
            var material = Resources.Load<Material>(VortexRingLens.MaterialResourcePath);
            Assert.IsNotNull(material, "Resources/VortexRingLens.mat");
            Assert.AreEqual("CosmicShore/VortexRingLens", material.shader.name);
            Assert.IsTrue(BlackHoleLens.IsDrawable(material, out string reason), reason);
            Assert.GreaterOrEqual(material.FindPass(VortexRingLensPass.LightModeName), 0, "the pass the lens draws, by its name");
        }
    }
}
#endif
