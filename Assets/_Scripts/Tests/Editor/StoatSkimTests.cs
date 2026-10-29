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

        // ------------------------------------------------------------------ the chain (skips)

        [Test]
        public void Chain_OpensOnlyInsideTheSkipWindow_WithSkipsLeft()
        {
            Assert.IsTrue(StoatSkimMath.Chains(0.5f, 1.2f, 0, 5));
            Assert.IsTrue(StoatSkimMath.Chains(1.2f, 1.2f, 4, 5), "the window's last instant still chains");
            Assert.IsFalse(StoatSkimMath.Chains(1.21f, 1.2f, 0, 5), "a late press starts a fresh run");
            Assert.IsFalse(StoatSkimMath.Chains(float.PositiveInfinity, 1.2f, 0, 5), "no launch yet");
            Assert.IsFalse(StoatSkimMath.Chains(-0.1f, 1.2f, 0, 5));
            Assert.IsFalse(StoatSkimMath.Chains(0.5f, 1.2f, 5, 5), "five skips is the longest run");
        }

        [Test]
        public void Perfect_IsTheWindowsFirstStretch()
        {
            Assert.IsTrue(StoatSkimMath.Perfect(0f, 0.4f));
            Assert.IsTrue(StoatSkimMath.Perfect(0.4f, 0.4f));
            Assert.IsFalse(StoatSkimMath.Perfect(0.41f, 0.4f));
        }

        [Test]
        public void SkipGain_DecaysByTheSkip_APerfectSkipPaysItsBonus_TheCapHolds()
        {
            for (int n = 0; n <= 5; n++)
                Assert.That(StoatSkimMath.SkipScale(0.8f, n), Is.EqualTo(Mathf.Pow(0.8f, n)).Within(1e-5f));
            // 100 × (1 + 0.9 × 0.8 × 1.1) = 179.2 for a perfect first skip.
            Assert.That(StoatSkimMath.LaunchSpeed(100f, 0.9f, 1f, 0.3f, 1f, 0f, 0.8f, 1, 0.1f), Is.EqualTo(179.2f).Within(1e-3f));
            Assert.That(StoatSkimMath.LaunchSpeed(100f, 0.9f, 1f, 0.3f, 1f, 0f, 0.8f, 1), Is.EqualTo(172f).Within(1e-3f));
            // Every skip of a run, however perfect, under the cap (3 × a 50 u/s cruise).
            float v = 50f;
            for (int n = 0; n <= 5; n++)
            {
                v = StoatSkimMath.LaunchSpeed(v, 3f, 1f, 0.3f, 3f, 150f, 0.8f, n, 0.1f);
                Assert.That(v, Is.LessThanOrEqualTo(150f + 1e-3f));
            }
        }

        [Test]
        public void PredictedLine_WithNoHook_IsStraight()
        {
            var p = StoatSkimMath.PredictLine(Vector3.zero, Vector3.forward, Vector3.up, 0f, 0.8f, 0f, 100f, 160f, 16, out var h);
            Assert.That((p - new Vector3(0f, 0f, 160f)).magnitude, Is.LessThan(1e-3f));
            Assert.That(Vector3.Angle(h, Vector3.forward), Is.LessThan(1e-3f));
        }

        [Test]
        public void PredictedLine_FollowsTheHook_AllOfItsTurnAndNoMore()
        {
            // A left hook (axis down) at 1 rad/s fading over 0.8 s: 0.4 rad ≈ 22.9° in all; 160 u at 100 u/s outlasts it.
            float total = StoatSkimMath.HookTotalDegrees(1f, 0.8f);
            var p = StoatSkimMath.PredictLine(Vector3.zero, Vector3.forward, Vector3.down, 1f, 0.8f, 0f, 100f, 160f, 32, out var h);
            Assert.That(Vector3.Angle(h, Vector3.forward), Is.EqualTo(total).Within(0.2f));
            Assert.That(h.x, Is.LessThan(0f), "the left hook turns the line left");
            Assert.That(p.x, Is.LessThan(-5f));
            Assert.That(p.magnitude, Is.LessThan(160f), "a bent path ends nearer than its length");
            // Pressed after the hook has run out, the line is straight again.
            StoatSkimMath.PredictLine(Vector3.zero, Vector3.forward, Vector3.down, 1f, 0.8f, 0.9f, 100f, 160f, 32, out h);
            Assert.That(Vector3.Angle(h, Vector3.forward), Is.LessThan(1e-3f));
        }

        [Test]
        public void ChainedShell_SitsTangentToTheLine_SoAFlatPassIsCaughtWhole()
        {
            var centre = StoatSkimMath.ChainCentre(new Vector3(0f, 0f, 160f), Vector3.forward, Vector3.up, left: true, R);
            Assert.That((centre - new Vector3(-R, 0f, 160f)).magnitude, Is.LessThan(1e-3f));
            Assert.That(StoatSkimMath.EntryIncidenceDegrees(Vector3.zero, Vector3.forward, centre, R), Is.EqualTo(0f).Within(1e-3f));
            // Fly the straight line: the Tidepool catches it as it passes, the entry flat, the speed all kept.
            bool caught = false;
            for (float z = 0f; z < 320f && !caught; z += 1f)
                caught = StoatSkimMath.Captures(new Vector3(0f, 0f, z), Vector3.forward, centre, R, StoatSkimMath.TidepoolRadius(R, 1.6f), 60f);
            Assert.IsTrue(caught);
            Assert.That(StoatSkimMath.EntryCarry(StoatSkimMath.EntryIncidenceDegrees(Vector3.zero, Vector3.forward, centre, R), 0.6f),
                Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ChainedShell_OnTheHookedLine_IsPassedTangent_WhereAStraightGuessIsNot()
        {
            const float speed = 100f, rate = 1f, secs = 0.8f, ahead = 160f;
            var axis = Vector3.down;   // a left ride's hook
            var point = StoatSkimMath.PredictLine(Vector3.zero, Vector3.forward, axis, rate, secs, 0f, speed, ahead, 32, out var h);
            var centre = StoatSkimMath.ChainCentre(point, h, Vector3.up, left: true, R);
            var straight = StoatSkimMath.ChainCentre(new Vector3(0f, 0f, ahead), Vector3.forward, Vector3.up, left: true, R);
            // Fly it with the hook the executor flies (StepHook), finely.
            var p = Vector3.zero;
            var f = Vector3.forward;
            float closest = float.PositiveInfinity, closestStraight = float.PositiveInfinity;
            const float dt = 0.002f;
            for (float t = 0f; t < 3f; t += dt)
            {
                float deg = rate * StoatSkimMath.HookDecay(t, secs) * dt * Mathf.Rad2Deg;
                f = Quaternion.AngleAxis(deg, axis) * f;
                p += f * speed * dt;
                closest = Mathf.Min(closest, (p - centre).magnitude);
                closestStraight = Mathf.Min(closestStraight, (p - straight).magnitude);
            }
            Assert.That(closest, Is.EqualTo(R).Within(1.5f), "the hooked line passes the chained shell tangent");
            Assert.That(Mathf.Abs(closestStraight - R), Is.GreaterThan(10f), "ignoring the hook misses the tangent badly");
        }

        [Test]
        public void EntryIncidence_IsTheLinesAngle_TangentFlat_ThroughTheCentreStraightIn()
        {
            var centre = new Vector3(0f, 0f, 200f);
            Assert.That(StoatSkimMath.EntryIncidenceDegrees(Vector3.zero, Vector3.forward, centre, R), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(StoatSkimMath.EntryIncidenceDegrees(new Vector3(R * 0.5f, 0f, 0f), Vector3.forward, centre, R),
                Is.EqualTo(60f).Within(1e-2f));
            Assert.That(StoatSkimMath.EntryIncidenceDegrees(new Vector3(R * 2f, 0f, 0f), Vector3.forward, centre, R), Is.EqualTo(0f));
        }

        // ------------------------------------------------------------------ graze skips

        static Vector3 Heading(float degreesInward) =>
            // Flying +z along the shell's +x side; tilted inward (−x) by the given angle.
            new Vector3(-Mathf.Sin(degreesInward * Mathf.Deg2Rad), 0f, Mathf.Cos(degreesInward * Mathf.Deg2Rad));

        [Test]
        public void Graze_IsAFlatInwardCrossing_WithNoTriggerHeld()
        {
            var radial = Vector3.right;   // the hull at the shell's +x edge
            Assert.IsTrue(StoatSkimMath.Grazes(false, R + 1f, R - 0.5f, R, Heading(10f), radial, 20f));
            Assert.IsFalse(StoatSkimMath.Grazes(true, R + 1f, R - 0.5f, R, Heading(10f), radial, 20f), "a held trigger rides, it does not graze");
            Assert.IsFalse(StoatSkimMath.Grazes(false, R + 1f, R - 0.5f, R, Heading(40f), radial, 20f), "steep passes through");
            Assert.IsFalse(StoatSkimMath.Grazes(false, R - 2f, R - 3f, R, Heading(10f), radial, 20f), "already inside: no crossing");
            Assert.IsFalse(StoatSkimMath.Grazes(false, R + 3f, R + 1f, R, Heading(10f), radial, 20f), "not yet reached");
            Assert.IsFalse(StoatSkimMath.Grazes(false, R + 1f, R - 0.5f, R, Heading(-10f), radial, 20f), "moving out is no graze");
        }

        [Test]
        public void Graze_ReflectsTheNormalSpeedAtTheRestitution_KeepsTheTangent_KickUnderTheCap()
        {
            var v = Heading(10f) * 100f;
            var n = Vector3.right;
            var b = StoatSkimMath.GrazeReflect(v, n, 0.9f);
            Assert.That(Vector3.Dot(b, n), Is.EqualTo(-0.9f * Vector3.Dot(v, n)).Within(1e-3f));
            Assert.That(b.z, Is.EqualTo(v.z).Within(1e-3f));
            Assert.That(b.magnitude, Is.LessThanOrEqualTo(v.magnitude + 1e-3f));
            Assert.That(StoatSkimMath.GrazeKick(100f, 0.08f, 0f), Is.EqualTo(8f).Within(1e-4f));
            Assert.That(StoatSkimMath.GrazeKick(146f, 0.08f, 150f), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(StoatSkimMath.GrazeKick(160f, 0.08f, 150f), Is.EqualTo(0f));
        }

        [Test]
        public void SkipRipple_GrowsFromTheShell_AndFades()
        {
            Assert.That(StoatSkimMath.RippleRadius(R, 120f, 0f), Is.EqualTo(R));
            Assert.That(StoatSkimMath.RippleRadius(R, 120f, 0.5f), Is.EqualTo(R + 60f).Within(1e-3f));
            Assert.That(StoatSkimMath.RippleAlpha(0f, 0.6f), Is.EqualTo(1f));
            Assert.That(StoatSkimMath.RippleAlpha(0.3f, 0.6f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(StoatSkimMath.RippleAlpha(0.9f, 0.6f), Is.EqualTo(0f));
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
            Assert.That(skim.MaxSkips, Is.EqualTo(5));
            Assert.That(skim.SkipDecay, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(skim.SkipWindow, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(skim.PerfectWindow, Is.LessThan(skim.SkipWindow));
            Assert.That(skim.PerfectBonus, Is.GreaterThan(0f));
            Assert.That(skim.GrazeAngle, Is.GreaterThan(0f).And.LessThan(45f));
            Assert.That(skim.ChainBreakCarry, Is.GreaterThan(skim.PlunkCarry), "a straight-in plunk breaks the run");
        }
    }
}
