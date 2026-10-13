using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Stoat's own-vessel pass (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1a–§1e), as pure arithmetic: the Charge
    /// ripple's reach, front and victims (<see cref="StoatRippleMath"/>), and Time mode's timed slingshot and the orbit
    /// arc the pathfinder draws (<see cref="StoatSlingMath.SweetSpot"/>, <see cref="StoatSlingMath.TimedKick"/>,
    /// <see cref="StoatSlingMath.OrbitArc"/>).
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
            Assert.AreEqual(120f, StoatRippleMath.Radius(200f, 0.6f, 30f, 160f, 0.3f), 1e-4f, "Charge never shrinks it (floored at 1)");
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
            Assert.AreEqual(54f, StoatSlingMath.TimedKick(54f, 1f, 0.2f, 0.5f), 1e-4f, "Time never cuts it (floored at 1)");
        }

        [Test]
        public void OrbitArc_RunsRoundTheCentreTheWayTheHullIsFlying_ToTheEndOfTheWindow()
        {
            var points = new Vector3[512];
            var hull = new Vector3(100f, 0f, 0f);
            var centre = Vector3.zero;
            int n = StoatSlingMath.OrbitArc(hull, Vector3.forward, centre, 0f, 135f, 3f, points);
            Assert.Greater(n, 10);
            Assert.AreEqual(hull, points[0]);
            for (int i = 0; i < n; i++) Assert.AreEqual(100f, points[i].magnitude, 1e-3f, "every point on the orbit");
            Assert.Greater(points[1].z, 0f, "the arc leaves along the hull's heading");
            float swept = Vector3.Angle(points[0], points[n - 1]);
            Assert.AreEqual(135f, swept, 3f, "to the far edge of the window");
            Assert.AreEqual(0, StoatSlingMath.OrbitArc(hull, Vector3.forward, centre, 140f, 135f, 3f, points) - 1,
                "past the window there is nothing left to draw but the hull's own point");
        }
    }
}
