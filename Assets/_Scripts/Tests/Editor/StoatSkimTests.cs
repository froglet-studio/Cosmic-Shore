using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Stoat's SKIM SLING, Time mode since 2026-10-29 (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1q): the pure
    /// arithmetic in <see cref="StoatSkimMath"/> and the shipped config's wiring. Each test names the decision it holds.
    /// </summary>
    [TestFixture]
    public class StoatSkimTests
    {
        const float R = 60f;

        // ------------------------------------------------------------------ placement and handedness

        [Test]
        public void Pair_IsLaidAheadAndToThePressedSide()
        {
            var mid = StoatSkimMath.Midpoint(Vector3.zero, Vector3.forward, Vector3.right, left: true, ahead: 160f, side: 40f);
            Assert.That(mid.z, Is.EqualTo(160f).Within(1e-3f));
            Assert.That(mid.x, Is.EqualTo(-40f).Within(1e-3f));
            mid = StoatSkimMath.Midpoint(Vector3.zero, Vector3.forward, Vector3.right, left: false, ahead: 160f, side: 40f);
            Assert.That(mid.x, Is.EqualTo(40f).Within(1e-3f));
        }

        [Test]
        public void SpinAxis_IsUpForTheRightTrigger_DownForTheLeft()
        {
            Assert.That(StoatSkimMath.SpinAxis(false, Vector3.up), Is.EqualTo(Vector3.up));
            Assert.That(StoatSkimMath.SpinAxis(true, Vector3.up), Is.EqualTo(Vector3.down));
        }

        [Test]
        public void LeftShell_FlowCarriesAHullOnItsRightEdgeForward_SoTheRideCirclesLeft()
        {
            // An LT pair is laid to the left, so the hull meets it on the shell's right (radial +x), flying +z.
            var flow = StoatSkimMath.SpinFlow(StoatSkimMath.SpinAxis(true, Vector3.up), Vector3.right);
            Assert.That(Vector3.Dot(flow, Vector3.forward), Is.GreaterThan(0.99f));
            // The RT mirror: shell on the right, hull on its left edge.
            flow = StoatSkimMath.SpinFlow(StoatSkimMath.SpinAxis(false, Vector3.up), Vector3.left);
            Assert.That(Vector3.Dot(flow, Vector3.forward), Is.GreaterThan(0.99f));
        }

        [Test]
        public void LeftHook_TurnsTheNoseLeft()
        {
            var turned = Quaternion.AngleAxis(10f, StoatSkimMath.SpinAxis(true, Vector3.up)) * Vector3.forward;
            Assert.That(turned.x, Is.LessThan(0f));
        }

        [Test]
        public void SpinFlow_IsNothingAtThePoles()
        {
            Assert.That(StoatSkimMath.SpinFlow(Vector3.up, Vector3.up).magnitude, Is.LessThan(1e-4f));
        }

        // ------------------------------------------------------------------ capture and the plunk

        [Test]
        public void Tidepool_CapturesAsYouPass_NotWhileStillApproaching()
        {
            float pool = StoatSkimMath.TidepoolRadius(R, 1.6f);
            Assert.That(pool, Is.EqualTo(96f).Within(1e-3f));
            var centre = new Vector3(-80f, 0f, 0f);
            // Beside the shell (centre 90° off the nose): captured.
            Assert.IsTrue(StoatSkimMath.Captures(Vector3.zero, Vector3.forward, centre, R, pool, 60f));
            // Same distance, but heading at it (centre 0° off the nose): not yet.
            Assert.IsFalse(StoatSkimMath.Captures(Vector3.zero, Vector3.left, centre, R, pool, 60f));
            // Outside the Tidepool: never.
            Assert.IsFalse(StoatSkimMath.Captures(Vector3.zero, Vector3.forward, new Vector3(-120f, 0f, 0f), R, pool, 60f));
            // Inside the Skimshell itself: always.
            Assert.IsTrue(StoatSkimMath.Captures(Vector3.zero, Vector3.left, new Vector3(-50f, 0f, 0f), R, pool, 60f));
        }

        [Test]
        public void FlatEntry_KeepsItAll_StraightIn_Plunks()
        {
            Assert.That(StoatSkimMath.IncidenceDegrees(Vector3.forward, Vector3.right), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(StoatSkimMath.IncidenceDegrees(Vector3.left, Vector3.right), Is.EqualTo(90f).Within(1e-2f));
            Assert.That(StoatSkimMath.EntryCarry(0f, 0.6f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(StoatSkimMath.EntryCarry(90f, 0.6f), Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(StoatSkimMath.EntryCarry(45f, 0.6f), Is.EqualTo(0.8f).Within(1e-3f));
        }

        // ------------------------------------------------------------------ the spin

        [Test]
        public void Squeeze_SetsTheSpin_OnACurve()
        {
            Assert.That(StoatSkimMath.SpinSpeed(0f, 40f, 1.5f), Is.EqualTo(0f));
            Assert.That(StoatSkimMath.SpinSpeed(1f, 40f, 1.5f), Is.EqualTo(40f).Within(1e-3f));
            Assert.That(StoatSkimMath.SpinSpeed(0.5f, 40f, 1.5f), Is.EqualTo(40f * Mathf.Pow(0.5f, 1.5f)).Within(1e-3f));
        }

        [Test]
        public void WithTheSpin_Gains_AgainstIt_Bleeds()
        {
            Assert.That(StoatSkimMath.SpinTarget(40f, Vector3.forward, Vector3.forward), Is.EqualTo(40f).Within(1e-3f));
            Assert.That(StoatSkimMath.SpinTarget(40f, Vector3.back, Vector3.forward), Is.EqualTo(-40f).Within(1e-3f));
            float eased = StoatSkimMath.EaseSpin(0f, 40f, 0.8f, 1f);
            Assert.That(eased, Is.GreaterThan(0f).And.LessThan(40f));
        }

        [Test]
        public void Precess_DrawsTheTrackOntoTheSkimline_EitherWay()
        {
            var heading = (Vector3.forward + Vector3.up).normalized;
            var with = StoatSkimMath.Precess(heading, Vector3.forward, 0.3f, 1f);
            Assert.That(Vector3.Angle(with, Vector3.forward), Is.LessThan(45f));
            var against = StoatSkimMath.Precess(heading, Vector3.back, 0.3f, 1f);
            Assert.That(Vector3.Angle(against, Vector3.forward), Is.LessThan(45f), "against the spin is allowed, not undone");
        }

        // ------------------------------------------------------------------ the launch

        [Test]
        public void Launch_PaysTheGainInTheSweetSpot_TheMissShareOutside()
        {
            float g = StoatSkimMath.LaunchGain(1f, 0.25f, 0.9f);
            Assert.That(g, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(StoatSkimMath.LaunchGain(0f, 0.25f, 0.9f), Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(StoatSkimMath.LaunchSpeed(100f, g, 1f, 0.3f, 1f, 0f), Is.EqualTo(190f).Within(1e-3f));
            Assert.That(StoatSkimMath.LaunchSpeed(100f, g, 0f, 0.3f, 1f, 0f), Is.EqualTo(127f).Within(1e-3f));
        }

        [Test]
        public void Launch_IsCapped()
        {
            Assert.That(StoatSkimMath.LaunchSpeed(100f, 0.9f, 1f, 0.3f, 3f, 150f), Is.EqualTo(150f).Within(1e-3f));
            Assert.That(StoatSkimMath.Kick(150f, 100f), Is.EqualTo(50f).Within(1e-3f));
            Assert.That(StoatSkimMath.Kick(80f, 100f), Is.EqualTo(0f));
        }

        [Test]
        public void EachSkip_PaysALittleLess()
        {
            float first = StoatSkimMath.LaunchSpeed(100f, 0.9f, 1f, 0.3f, 1f, 0f, 0.8f, 0);
            float second = StoatSkimMath.LaunchSpeed(100f, 0.9f, 1f, 0.3f, 1f, 0f, 0.8f, 1);
            Assert.That(second, Is.LessThan(first));
            Assert.That(second, Is.EqualTo(100f * (1f + 0.9f * 0.8f)).Within(1e-3f));
        }

        [Test]
        public void SkipIndex_CountsCapturesWithinTheWindow_UpToTheMax()
        {
            Assert.That(StoatSkimMath.SkipIndex(0, 0.5f, 1.2f, 5), Is.EqualTo(1));
            Assert.That(StoatSkimMath.SkipIndex(5, 0.5f, 1.2f, 5), Is.EqualTo(5));
            Assert.That(StoatSkimMath.SkipIndex(3, 2f, 1.2f, 5), Is.EqualTo(0));
            Assert.That(StoatSkimMath.SkipIndex(3, float.PositiveInfinity, 1.2f, 5), Is.EqualTo(0));
        }

        [Test]
        public void Hook_FollowsTheSpin_AndFades()
        {
            float rate = StoatSkimMath.HookRate(1f, 40f, R);
            Assert.That(rate, Is.EqualTo(40f / R).Within(1e-4f));
            Assert.That(StoatSkimMath.HookTotalDegrees(rate, 0.8f), Is.EqualTo(0.5f * rate * 0.8f * Mathf.Rad2Deg).Within(1e-3f));
            Assert.That(StoatSkimMath.HookDecay(0f, 0.8f), Is.EqualTo(1f));
            Assert.That(StoatSkimMath.HookDecay(0.4f, 0.8f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(StoatSkimMath.HookDecay(2f, 0.8f), Is.EqualTo(0f));
            Assert.That(StoatSkimMath.HookRate(1f, 0f, R), Is.EqualTo(0f), "no squeeze, no hook");
        }

        // ------------------------------------------------------------------ rivals (ELEMENTAL_ECONOMY §9b)

        [Test]
        public void Rival_RidesOnlyByItsOwnChoice()
        {
            Assert.IsFalse(StoatSkimMath.RivalOptsIn(false, true, 5f, 20f), "rule off");
            Assert.IsFalse(StoatSkimMath.RivalOptsIn(true, false, 5f, 20f), "not holding its own input");
            Assert.IsFalse(StoatSkimMath.RivalOptsIn(true, true, 40f, 20f), "not flat");
            Assert.IsTrue(StoatSkimMath.RivalOptsIn(true, true, 5f, 20f));
        }

        // ------------------------------------------------------------------ drawing

        [Test]
        public void Skimline_LiesOnTheShell_SquareToTheSpin_AndScrollsWithThePhase()
        {
            var points = new Vector3[48];
            var centre = new Vector3(3f, 4f, 5f);
            int n = StoatSkimMath.Ring(centre, Vector3.up, R, 0f, 48, points);
            Assert.That(n, Is.EqualTo(48));
            for (int i = 0; i < n; i++)
            {
                Assert.That((points[i] - centre).magnitude, Is.EqualTo(R).Within(1e-3f));
                Assert.That(points[i].y, Is.EqualTo(centre.y).Within(1e-3f));
            }
            var first = points[0] - centre;
            StoatSkimMath.Ring(centre, Vector3.up, R, 30f, 48, points);
            Assert.That(Vector3.Angle(first, points[0] - centre), Is.EqualTo(30f).Within(0.05f));
            // It scrolls the spin's way: toward the flow at the first point.
            Assert.That(Vector3.Dot(points[0] - centre - first, StoatSkimMath.SpinFlow(Vector3.up, first)), Is.GreaterThan(0f));
        }

        // ------------------------------------------------------------------ the shipped config

        [Test]
        public void ShippedSlingConfig_FliesTheSkim()
        {
            var sling = AssetDatabase.LoadAssetAtPath<StoatSlingConfigSO>("Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset");
            Assert.IsNotNull(sling, "StoatSlingConfig.asset");
            Assert.IsNotNull(sling.Skim, "Time mode is the Skim Sling: StoatSlingConfig.skim must be set");
            var skim = sling.Skim;
            Assert.That(skim.TidepoolScale, Is.GreaterThan(1f));
            Assert.That(skim.LaunchGainMax, Is.GreaterThanOrEqualTo(skim.LaunchGainMin));
            Assert.That(skim.CapCruises, Is.GreaterThan(1f));
            Assert.IsFalse(skim.RivalRiding, "rival riding ships off until Garrett signs off (ELEMENTAL_ECONOMY §9b)");
        }
    }
}
