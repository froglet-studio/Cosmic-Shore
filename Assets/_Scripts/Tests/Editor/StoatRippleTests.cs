using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Stoat's own-vessel pass (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1a–§1e), as pure arithmetic: the Charge
    /// ripple's reach, front and victims (<see cref="StoatRippleMath"/>), and Time mode's timed slingshot
    /// (<see cref="StoatSlingMath.SweetSpot"/>, <see cref="StoatSlingMath.TimedKick"/>; the catch and its rings are held by
    /// <c>StoatSlingTests</c>).
    /// </summary>
    public class StoatRippleTests
    {
        // ------------------------------------------------------------------ the ripple (Charge)

        [Test]
        public void Radius_FollowsTheSeparation_ClampedThenScaledByCharge()
        {
            Assert.AreEqual(30f, StoatRippleMath.Radius(0f, 0.6f, 30f, 160f, 1f), 1e-4f, "poles together still throw the smallest ripple");
            Assert.AreEqual(120f, StoatRippleMath.Radius(200f, 0.6f, 30f, 160f, 1f), 1e-4f, "a wide pair throws a wide ripple");
            Assert.AreEqual(160f, StoatRippleMath.Radius(1000f, 0.6f, 30f, 160f, 1f), 1e-4f, "clamped before Charge");
            Assert.AreEqual(320f, StoatRippleMath.Radius(1000f, 0.6f, 30f, 160f, 2f), 1e-4f, "Charge 10 doubles the reach");
            Assert.AreEqual(60f, StoatRippleMath.Radius(200f, 0.6f, 30f, 160f, 0.5f), 1e-4f, "a Charge deficit shrinks it, to half at the fleet floor");
        }

        [Test]
        public void Front_ExpandsFastThenSettlesAtItsReach()
        {
            Assert.AreEqual(0f, StoatRippleMath.Front(0f, 0.9f, 100f), 1e-4f);
            float early = StoatRippleMath.Front(0.3f, 0.9f, 100f);
            float late = StoatRippleMath.Front(0.6f, 0.9f, 100f);
            Assert.Greater(early, 100f / 3f, "ease-out: a third of the time covers more than a third of the reach");
            Assert.Greater(late, early);
            Assert.AreEqual(100f, StoatRippleMath.Front(0.9f, 0.9f, 100f), 1e-4f);
            Assert.AreEqual(100f, StoatRippleMath.Front(5f, 0.9f, 100f), 1e-4f, "held at its reach");
        }

        [Test]
        public void Strength_IsFullWhileExpanding_ThenFades_AndTheWaveEnds()
        {
            Assert.AreEqual(1f, StoatRippleMath.Strength(0.5f, 0.9f, 0.5f), 1e-4f);
            Assert.AreEqual(0.5f, StoatRippleMath.Strength(1.15f, 0.9f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, StoatRippleMath.Strength(2f, 0.9f, 0.5f), 1e-4f);
            Assert.IsFalse(StoatRippleMath.Done(1.3f, 0.9f, 0.5f));
            Assert.IsTrue(StoatRippleMath.Done(1.4f, 0.9f, 0.5f));
        }

        [Test]
        public void Strips_OnlyARivalWhoseDomainIsKnown()
        {
            Assert.IsTrue(StoatRippleMath.Strips(false, true, Domains.Ruby, Domains.Jade), "a rival is stripped");
            Assert.IsFalse(StoatRippleMath.Strips(true, true, Domains.Ruby, Domains.Jade), "never the owner");
            Assert.IsFalse(StoatRippleMath.Strips(false, true, Domains.Jade, Domains.Jade), "never a teammate");
            Assert.IsFalse(StoatRippleMath.Strips(false, false, Domains.Ruby, Domains.Jade), "never a pilot nobody has identified yet");
        }

        // ------------------------------------------------------------------ the drawn wave (Yash, 2026-11-21: no colour)

        [Test]
        public void Profile_RisesAtTheFront_RingsDownBehindIt_AndIsSilentAheadOrFar()
        {
            Assert.AreEqual(0f, StoatRippleMath.Profile(0f), 1e-4f, "zero on the front itself");
            Assert.AreEqual(0f, StoatRippleMath.Profile(-2f), "nothing ahead of the front");
            Assert.AreEqual(0f, StoatRippleMath.Profile(4.5f), "nothing four wavelengths behind it");
            float first = Mathf.Abs(StoatRippleMath.Profile(0.25f));
            float second = Mathf.Abs(StoatRippleMath.Profile(1.25f));
            float third = Mathf.Abs(StoatRippleMath.Profile(2.25f));
            Assert.Greater(first, 0.9f, "the first crest, right behind the front, is the strongest");
            Assert.Greater(first, second, "it rings down");
            Assert.Greater(second, third, "it rings down");
            Assert.Greater(third, 0.05f, "about three crests trail the front");
            Assert.Less(Mathf.Abs(StoatRippleMath.Profile(-0.25f)), 0.7f, "the front's leading edge is sharper than its tail");
        }

        [Test]
        public void Amplitude_IsFullNearTheMeeting_ThenThinsAsOneOverR_AndFades()
        {
            Assert.AreEqual(0.012f, StoatRippleMath.Amplitude(0.012f, 10f, 22f, 1f), 1e-6f, "full inside the near radius");
            Assert.AreEqual(0.006f, StoatRippleMath.Amplitude(0.012f, 44f, 22f, 1f), 1e-6f, "half at twice the near radius");
            Assert.AreEqual(0.0015f, StoatRippleMath.Amplitude(0.012f, 176f, 22f, 1f), 1e-6f, "an eighth at eight times");
            Assert.AreEqual(0.003f, StoatRippleMath.Amplitude(0.012f, 44f, 22f, 0.5f), 1e-6f, "and fading with the wave");
            Assert.AreEqual(0f, StoatRippleMath.Amplitude(0.012f, 44f, 22f, 0f), "gone when the wave is");
        }

        [Test]
        public void ScreenRipples_ProjectsTheFrontAhead_AndSwellsTheViewItRunsThrough()
        {
            var config = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.StoatRippleConfigSO>();
            try
            {
                var worldToCamera = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));   // a camera at the origin looking down +Z
                var c = new Vector4[StoatRipple.MaxDrawn];
                var a = new Vector4[StoatRipple.MaxDrawn];
                StoatRipple.ScreenRipples(worldToCamera, Vector3.zero, 60f, 0.3f, StoatRipple.Now + 100f, c, a, out _);   // clears older ripples

                StoatRipple.Spawn(new Vector3(0f, 0f, 100f), 50f, config);
                int count = StoatRipple.ScreenRipples(worldToCamera, Vector3.zero, 60f, 0.3f, StoatRipple.Now + 0.3f, c, a, out float pass);
                Assert.AreEqual(1, count, "one ripple, in front of the camera");
                Assert.AreEqual(0f, c[0].x, 1e-4f, "centred where the hole was");
                Assert.AreEqual(0f, c[0].y, 1e-4f);
                Assert.Greater(c[0].z, 0f, "the front has a size on screen");
                Assert.Greater(c[0].w, 0f, "and a wavelength");
                Assert.Greater(a[0].x, 0f, "it bends the scene");
                Assert.AreEqual(0f, pass, 1e-6f, "a front 60 units away does not swell the view");

                // A wave born 10 units in front of the camera has run past it a quarter-wavelength later: no ring, a swell.
                StoatRipple.ScreenRipples(worldToCamera, Vector3.zero, 60f, 0.3f, StoatRipple.Now + 100f, c, a, out _);
                StoatRipple.Spawn(new Vector3(0f, 0f, 10f), 50f, config);
                count = StoatRipple.ScreenRipples(worldToCamera, Vector3.zero, 60f, 0.3f, StoatRipple.Now + 0.0806f, c, a, out pass);
                Assert.AreEqual(0, count, "the camera is inside the front: no ring left to see");
                Assert.Greater(pass, 0f, "the view swells as the wave runs through it");
            }
            finally
            {
                StoatRipple.ScreenRipples(Matrix4x4.identity, Vector3.zero, 60f, 0.3f, StoatRipple.Now + 100f,
                    new Vector4[StoatRipple.MaxDrawn], new Vector4[StoatRipple.MaxDrawn], out _);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ShippedRipple_HasNoColourOfItsOwn()
        {
            var asset = System.IO.File.ReadAllText("Assets/_SO_Assets/VesselActions/Stoat/StoatRippleConfig.asset").Replace("\r\n", "\n");
            foreach (var row in new[] { "wavelength: 9\n", "bend: 0.012\n", "nearRadius: 22\n", "shimmer: 0.05\n", "passShare: 0.35\n" })
                StringAssert.Contains("  " + row, asset);
            StringAssert.DoesNotContain("rimGlow", asset, "the domain-colour rim is gone");
            var shader = System.IO.File.ReadAllText("Assets/_Graphics/Materials/Graphs/StoatRipple.shader");
            StringAssert.DoesNotContain("_RippleTint", shader, "the shader paints no colour");
            StringAssert.Contains("col * max(0.0, 1.0 + shimmer)", shader, "it only bends and shades the scene's own light");
        }

        // ------------------------------------------------------------------ Time mode: the timed slingshot

        [Test]
        public void SweetSpot_IsWholeAQuarterTurnRound_AndFallsOffSmoothly()
        {
            Assert.AreEqual(1f, StoatSlingMath.SweetSpot(90f, 90f, 15f, 30f), 1e-5f, "a release a quarter-turn round");
            Assert.AreEqual(1f, StoatSlingMath.SweetSpot(76f, 90f, 15f, 30f), 1e-5f, "inside the window");
            Assert.AreEqual(1f, StoatSlingMath.SweetSpot(-104f, 90f, 15f, 30f), 1e-5f, "either way round");
            Assert.AreEqual(0.5f, StoatSlingMath.SweetSpot(120f, 90f, 15f, 30f), 1e-5f, "half way down the falloff");
            Assert.AreEqual(0f, StoatSlingMath.SweetSpot(0f, 90f, 15f, 30f), 1e-5f, "the tap release earns nothing");
            Assert.AreEqual(0f, StoatSlingMath.SweetSpot(180f, 90f, 15f, 30f), 1e-5f, "a half orbit is too late");
            // Monotone toward the centre from both sides — a later release inside the falloff is never worse.
            float last = -1f;
            for (float a = 40f; a <= 75f; a += 1f)
            {
                float s = StoatSlingMath.SweetSpot(a, 90f, 15f, 30f);
                Assert.GreaterOrEqual(s, last);
                last = s;
            }
        }

        [Test]
        public void TimedKick_PaysTheMissShareOutsideTheWindow_AndScalesWithTime()
        {
            Assert.AreEqual(54f, StoatSlingMath.TimedKick(54f, 1f, 0.2f, 1f), 1e-4f, "timed: the whole kick");
            Assert.AreEqual(54f * 0.2f, StoatSlingMath.TimedKick(54f, 0f, 0.2f, 1f), 1e-4f, "missed: the miss share");
            Assert.AreEqual(108f, StoatSlingMath.TimedKick(54f, 1f, 0.2f, 2f), 1e-4f, "Time 10 doubles it");
            Assert.AreEqual(27f, StoatSlingMath.TimedKick(54f, 1f, 0.2f, 0.5f), 1e-4f, "a deficit halves it at the fleet floor");
        }
    }
}
