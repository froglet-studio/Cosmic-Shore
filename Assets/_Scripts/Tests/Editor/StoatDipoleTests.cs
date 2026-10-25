using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Stoat's field dipole and pathfinder (<c>R_VesselActions/STOAT_DIPOLE.md</c>): the pure
    /// arithmetic in <see cref="StoatDipoleMath"/>, held to the numbers the Stoat Flight Studio
    /// measured for round 15 (<c>Docs/Studios/README.md</c>). Each test names the behaviour the
    /// designer decided, so a failure says which decision moved.
    /// </summary>
    [TestFixture]
    public class StoatDipoleTests
    {
        const float SideMax = 200f, LengthMax = 120f, Curve = 1.5f;

        static StoatDipoleMath.Field Dipole(float separation, float strengthGM = 120000f, float horizon = 3.5f) =>
            new StoatDipoleMath.Field
            {
                Sink = new Vector3(0.5f * separation, 0f, 0f), Source = new Vector3(-0.5f * separation, 0f, 0f),
                SinkGM = strengthGM, SinkHorizon = horizon, SourceGM = strengthGM, SourceHorizon = horizon,
                SourceSoftening = 2f * horizon, AccelerationCap = 3000f,
            };

        static readonly StoatDipoleMath.FlightSettings Flight = new() { TurnCap = 12f, Grip = 0.5f, GravitySpeedCeiling = 240f };

        static StoatDipoleMath.PathSettings Path(bool portal = true) => new()
        {
            Length = 600f, Step = 3f, NoseOffset = 8f, LoopMargin = 8f, MinLoop = 60f, WarpDegrees = 3f, ExitGap = 1.05f, PortalOpen = portal,
        };

        // ------------------------------------------------------------------ placement

        [Test]
        public void OneTrigger_PullsThePolesApartSideways_WithASlightDiagonal()
        {
            var s = StoatDipoleMath.TargetSeparation(0f, 1f, Curve, SideMax, LengthMax);
            Assert.AreEqual(200f, s.Lateral, 1e-3f, "RT alone: the full sideways separation, sink to the right");
            Assert.AreEqual(60f, s.Longitudinal, 1e-3f, "…and half the lengthways one (the slight diagonal)");
            var l = StoatDipoleMath.TargetSeparation(1f, 0f, Curve, SideMax, LengthMax);
            Assert.AreEqual(-200f, l.Lateral, 1e-3f, "LT alone mirrors it: the sink to the left");
        }

        [Test]
        public void BothTriggers_LineTheSinkUpDeadAhead_WithTheSourceBeyond()
        {
            var s = StoatDipoleMath.TargetSeparation(1f, 1f, Curve, SideMax, LengthMax);
            Assert.AreEqual(0f, s.Lateral, 1e-4f);
            Assert.AreEqual(120f, s.Longitudinal, 1e-3f);
            StoatDipoleMath.PolePositions(Vector3.zero, Vector3.right, Vector3.forward, s, out var sink, out var source);
            Assert.Less(sink.z, source.z, "the sink is the nearer pole: you shoot into it and out of the source");
            Assert.AreEqual(0f, sink.x, 1e-4f);
        }

        [Test]
        public void TheSqueezeCurve_AppliesToEachTrigger()
        {
            var half = StoatDipoleMath.TargetSeparation(0f, 0.5f, Curve, SideMax, LengthMax);
            Assert.AreEqual(200f * Mathf.Pow(0.5f, 1.5f), half.Lateral, 1e-3f);
        }

        [Test]
        public void LettingGoOfBoth_AnnihilatesOnlyOnceTheHorizonsTouch()
        {
            Assert.IsFalse(StoatDipoleMath.ShouldAnnihilate(true, 0f, 3.5f, 3.5f), "held: never");
            Assert.IsFalse(StoatDipoleMath.ShouldAnnihilate(false, 20f, 3.5f, 3.5f), "let go but still apart: closing");
            Assert.IsTrue(StoatDipoleMath.ShouldAnnihilate(false, 3f, 3.5f, 3.5f), "let go and touching: annihilate");
        }

        // ------------------------------------------------------------------ the field and the warp

        [Test]
        public void SteeringAlone_NeverWarpsThePath()
        {
            var r = StoatDipoleMath.PredictPath(new StoatDipoleMath.Body { Rotation = Quaternion.identity }, 60f,
                new Vector3(0f, 2f, 0f), default, hasField: false, Flight, Path(), new Vector3[256], new int[8]);
            Assert.IsFalse(r.Warped, "a held full turn draws a circle, and a circle is not a warp");
            Assert.AreEqual(StoatDipoleMath.PathEnd.Loop, r.End, "it still ends where it would cross its own trail");
        }

        [Test]
        public void APairFarBeyondThePath_DoesNotWarpIt()
        {
            var start = new StoatDipoleMath.Body { Position = new Vector3(0f, 0f, -2000f), Rotation = Quaternion.LookRotation(Vector3.left) };
            var r = StoatDipoleMath.PredictPath(start, 60f, Vector3.zero, Dipole(71f), true, Flight, Path(), new Vector3[256], new int[8]);
            Assert.IsFalse(r.Warped);
        }

        [Test]
        public void AimedAtTheSink_ThePathGoesThroughTheWormholeAndIsWarped()
        {
            var field = Dipole(71f);
            var start = new StoatDipoleMath.Body { Position = field.Sink + new Vector3(0f, 0f, -250f), Rotation = Quaternion.identity };
            var pts = new Vector3[256];
            var jumps = new int[8];
            var r = StoatDipoleMath.PredictPath(start, 60f, Vector3.zero, field, true, Flight, Path(), pts, jumps);
            Assert.IsTrue(r.Warped);
            Assert.GreaterOrEqual(r.JumpCount, 1, "dead at the sink: through the wormhole");
            Assert.Less(Vector3.Distance(pts[jumps[0]], field.Source), 3f * field.SourceHorizon, "out at the source");
        }

        [Test]
        public void TheZone_LetsA180TurnGo_ButTakesALoopPast200()
        {
            var field = Dipole(400f, 60000f, 3.5f / 3f);
            field.ThroughRadius = 90f;
            field.ThroughDegrees = 200f;
            // A body circling the sink at 40 u, its heading turning toward the sink in 1° steps.
            var b = new StoatDipoleMath.Body { Rotation = Quaternion.identity };
            bool through = false;
            int degrees = 0;
            for (; degrees < 360 && !through; degrees++)
            {
                var before = b.Rotation * Vector3.forward;
                b.Rotation = Quaternion.AngleAxis(-1f, Vector3.up) * b.Rotation;   // turning left, toward a sink on the left
                b.Position = field.Sink + Quaternion.AngleAxis(-(degrees + 1), Vector3.up) * new Vector3(40f, 0f, 0f);
                through = StoatDipoleMath.WindThrough(ref b, before, field);
                if (degrees + 1 == 180) Assert.IsFalse(through, "a U-turn round the pole is the pilot's");
            }
            Assert.IsTrue(through);
            Assert.AreEqual(200, degrees, 1, "taken through at the threshold");

            // Turning AWAY unwinds; leaving the zone clears.
            b.Winding = 150f;
            var fwd = b.Rotation * Vector3.forward;
            b.Rotation = Quaternion.AngleAxis(30f, Vector3.up) * b.Rotation;
            StoatDipoleMath.WindThrough(ref b, fwd, field);
            Assert.AreEqual(120f, b.Winding, 0.5f, "30° away takes 30° off");
            b.Position = field.Sink + new Vector3(0f, 0f, 200f);
            StoatDipoleMath.WindThrough(ref b, b.Rotation * Vector3.forward, field);
            Assert.AreEqual(0f, b.Winding, "out of the zone: cleared");
        }

        [Test]
        public void TheSlam_ClosesFromRest_AndFieldAndDistanceReachNothingTogether()
        {
            Assert.AreEqual(1f, StoatDipoleMath.SlamRemaining(0f, 0.3f), 1e-6f, "let go: still apart, full strength");
            Assert.AreEqual(0.75f, StoatDipoleMath.SlamRemaining(0.15f, 0.3f), 1e-5f, "half the time, a quarter of the way: accelerating");
            Assert.AreEqual(0f, StoatDipoleMath.SlamRemaining(0.3f, 0.3f), 1e-6f, "they meet: nothing left");
            Assert.AreEqual(0f, StoatDipoleMath.SlamRemaining(1f, 0.3f), 1e-6f);
            float early = 1f - StoatDipoleMath.SlamRemaining(0.03f, 0.3f), late = StoatDipoleMath.SlamRemaining(0.27f, 0.3f);
            Assert.Less(early, late, "the last tenth closes more than the first: they SLAM, not settle");
        }

        [Test]
        public void TheSoftenedSink_BendsAPassSmoothly_WhereTheSteepLawJumps()
        {
            // The pull on a line of points walking in toward the sink: the softened law never exceeds its core's peak,
            // the steep law blows up at the horizon.
            var steep = Dipole(2000f, 40000f, 1.75f);
            var soft = steep;
            soft.SinkSoftening = 14f * 1.75f;
            soft.AccelerationCap = steep.AccelerationCap = 1e9f;
            float peakSoft = 0f, peakSteep = 0f;
            for (float r = 2f; r <= 120f; r += 0.5f)
            {
                var p = steep.Sink + new Vector3(0f, 0f, r);
                peakSoft = Mathf.Max(peakSoft, StoatDipoleMath.FieldAcceleration(p, soft).magnitude);
                peakSteep = Mathf.Max(peakSteep, StoatDipoleMath.FieldAcceleration(p, steep).magnitude);
            }
            float coreMax = 40000f * 2f / (3f * Mathf.Sqrt(3f) * (24.5f * 24.5f));   // GM·r/(r²+ε²)^1.5 peaks at r = ε/√2
            Assert.AreEqual(coreMax, peakSoft, 0.05f * coreMax, "the softened pull peaks at its core");
            Assert.Greater(peakSteep, 50f * peakSoft, "the steep law is what made a near pass all-or-nothing");
        }

        [Test]
        public void ThroughTheZone_YouComeOutAtTheSourcesMouth()
        {
            var field = Dipole(400f, 60000f, 3.5f / 3f);
            var exit = StoatDipoleMath.ExitPoint(field.Sink + new Vector3(0f, 0f, 60f), field, 1.05f);
            Assert.AreEqual(1.05f * field.SourceHorizon, Vector3.Distance(exit, field.Source), 1e-3f, "at the mouth, not 60 u out");
            Assert.Less(Vector3.Dot(exit - field.Source, Vector3.forward), 0f, "on the opposite side (the point reflection)");
        }

        [Test]
        public void ThePrediction_EasesIntoATurnTheWayTheHullDoes()
        {
            // VesselTransformer: the stick turns the command, the hull slerps after it at 1.5/s. A held 48°/s yaw from
            // straight flight: after 1 s the hull has turned far less than 48°.
            var start = new StoatDipoleMath.Body { Rotation = Quaternion.identity, Command = Quaternion.identity, HasCommand = true };
            var response = new StoatDipoleMath.HullResponse { Enabled = true, NoseFollowRate = 1.5f, SpeedFollowRate = 1.5f, TargetSpeed = 60f };
            var yaw = new Vector3(0f, 48f * Mathf.Deg2Rad, 0f);
            var pts = new Vector3[512];
            var eased = StoatDipoleMath.PredictPath(start, 60f, yaw, default, false, Flight, Path(), pts, new int[8], response);
            var at60 = pts[Mathf.Min(eased.Count - 1, 20)];   // ~60 u on
            var instant = StoatDipoleMath.PredictPath(start, 60f, yaw, default, false, Flight, Path(), pts, new int[8]);
            var instant60 = pts[Mathf.Min(instant.Count - 1, 20)];
            Assert.Less(Mathf.Abs(at60.x), Mathf.Abs(instant60.x), "the eased line bends later than the instant one");
        }

        [Test]
        public void WithThePortalShut_ThePathStopsAtTheHorizon()
        {
            var field = Dipole(71f);
            var start = new StoatDipoleMath.Body { Position = field.Sink + new Vector3(0f, 0f, -250f), Rotation = Quaternion.identity };
            var r = StoatDipoleMath.PredictPath(start, 60f, Vector3.zero, field, true, Flight, Path(portal: false), new Vector3[256], new int[8]);
            Assert.AreEqual(StoatDipoleMath.PathEnd.Hole, r.End);
            Assert.AreEqual(0, r.JumpCount);
        }

        [Test]
        public void ThePrediction_IsTheFlightsOwnStep()
        {
            // Fly a body with Step at a fine fixed step; the predicted line must follow it.
            var field = Dipole(71f);
            var start = new StoatDipoleMath.Body { Position = field.Sink + new Vector3(60f, 0f, -250f), Rotation = Quaternion.identity };
            var pts = new Vector3[256];
            var r = StoatDipoleMath.PredictPath(start, 60f, Vector3.zero, field, true, Flight, Path(), pts, new int[8]);
            var b = start;
            float worst = 0f;
            for (int i = 0; i < 400; i++)
            {
                StoatDipoleMath.Step(ref b, field, 60f, 0.004f, Flight);
                float best = float.MaxValue;
                for (int k = 0; k < r.Count; k++) best = Mathf.Min(best, Vector3.Distance(pts[k], b.Position));
                if (Vector3.Distance(b.Position, start.Position) > 10f) worst = Mathf.Max(worst, best);
            }
            Assert.Less(worst, 3f, "the line is where the flight goes (1.6 s of flight, off the sink)");
        }

        // ------------------------------------------------------------------ the autopilot, the dots, the strip

        [Test]
        public void TheAutopilot_PullsBoth_ForATargetDeadAhead_AndOneSide_ForATargetOffTheNose()
        {
            Assert.IsTrue(StoatDipoleMath.AutopilotTriggers(new Vector3(0f, 0f, 1000f), 300f, 12f, 75f, out bool l, out bool r));
            Assert.IsTrue(l && r, "dead ahead: both triggers, the sink straight in front");
            Assert.IsTrue(StoatDipoleMath.AutopilotTriggers(new Vector3(600f, 0f, 800f), 300f, 12f, 75f, out l, out r));
            Assert.IsTrue(r && !l, "off to the right: the right trigger, the sink on that side");
            Assert.IsTrue(StoatDipoleMath.AutopilotTriggers(new Vector3(-600f, 0f, 800f), 300f, 12f, 75f, out l, out r));
            Assert.IsTrue(l && !r);
            Assert.IsFalse(StoatDipoleMath.AutopilotTriggers(new Vector3(0f, 0f, 100f), 300f, 12f, 75f, out _, out _), "too near");
            Assert.IsFalse(StoatDipoleMath.AutopilotTriggers(new Vector3(0f, 0f, -1000f), 300f, 12f, 75f, out _, out _), "behind: a pair laid ahead cannot help");
        }

        [Test]
        public void TheDots_AreEvenlySpacedOnScreen_AndBreakAtAWormholePass()
        {
            var screen = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f), new Vector2(500f, 0f), new Vector2(600f, 0f) };
            var visible = new[] { true, true, true, true };
            var dots = new Vector2[64];
            var src = new int[64];
            int n = StoatDipoleMath.LayDots(screen, visible, null, 2, 44f, dots, src);
            Assert.AreEqual(3, n, "0, 44, 88 along a 100 px segment");
            Assert.AreEqual(44f, dots[1].x - dots[0].x, 1e-3f);
            Assert.AreEqual(44f, dots[2].x - dots[1].x, 1e-3f);

            var breaks = new[] { false, false, true, false };
            n = StoatDipoleMath.LayDots(screen, visible, breaks, 4, 44f, dots, src);
            bool dotInTheGap = false;
            for (int i = 0; i < n; i++) if (dots[i].x > 100.5f && dots[i].x < 499.5f) dotInTheGap = true;
            Assert.IsFalse(dotInTheGap, "no dots across the jump from the sink to the source");
            Assert.AreEqual(500f, dots[3].x, 1e-3f, "the far side restarts with a dot where it comes out");
        }

        [Test]
        public void TheDots_PackTighterWithDepth_AndShrinkToTheFloor()
        {
            // Two segments of 100 px: the first at full size, the second at half (twice as deep, squared).
            var screen = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f), new Vector2(200f, 0f) };
            var visible = new[] { true, true, true };
            var scale = new[] { 1f, 1f, 0.5f };
            var dots = new Vector2[64];
            var src = new int[64];
            int n = StoatDipoleMath.LayDots(screen, visible, null, scale, 3, 20f, dots, src);
            Assert.AreEqual(20f, dots[1].x - dots[0].x, 1e-3f, "near: the authored spacing");
            Assert.AreEqual(10f, dots[n - 1].x - dots[n - 2].x, 1e-3f, "far: the spacing of a half-size dot");

            int plain = StoatDipoleMath.LayDots(screen, visible, null, 3, 20f, dots, src);
            int unscaled = StoatDipoleMath.LayDots(screen, visible, null, null, 3, 20f, dots, src);
            Assert.AreEqual(plain, unscaled, "no scales = the screen-even layout, unchanged");

            Assert.AreEqual(1f, StoatDipoleMath.DepthScale(40f, 40f, 0.35f), 1e-5f, "the nearest dot is full size");
            Assert.AreEqual(0.5f, StoatDipoleMath.DepthScale(160f, 40f, 0.35f), 1e-5f, "four times as deep: half size");
            Assert.AreEqual(0.35f, StoatDipoleMath.DepthScale(4000f, 40f, 0.35f), 1e-5f, "far: floored");
            Assert.AreEqual(1f, StoatDipoleMath.DepthScale(20f, 40f, 0.35f), 1e-5f, "never larger than the nearest");
        }

        [Test]
        public void TheSink_StripsARival_NeverItsOwnerOrATeammate()
        {
            Assert.IsTrue(BlackHoleCrystalStrip.OwesStrip(1f, false, true, CosmicShore.Data.Domains.Ruby, CosmicShore.Data.Domains.Jade));
            Assert.IsFalse(BlackHoleCrystalStrip.OwesStrip(1f, true, true, CosmicShore.Data.Domains.Ruby, CosmicShore.Data.Domains.Jade), "the owner");
            Assert.IsFalse(BlackHoleCrystalStrip.OwesStrip(1f, false, true, CosmicShore.Data.Domains.Jade, CosmicShore.Data.Domains.Jade), "a teammate");
            Assert.IsFalse(BlackHoleCrystalStrip.OwesStrip(1f, false, false, CosmicShore.Data.Domains.Ruby, CosmicShore.Data.Domains.Jade), "domain unknown");
            Assert.IsFalse(BlackHoleCrystalStrip.OwesStrip(0f, false, true, CosmicShore.Data.Domains.Ruby, CosmicShore.Data.Domains.Jade), "share 0");
        }

        [Test]
        public void TheBoost_RisesWhileWarped_AndFadesAfter()
        {
            float m = 1f;
            for (int i = 0; i < 60; i++) m = StoatDipoleMath.StepBoost(m, true, 3f, 20f, 0.6f, 1f / 60f);
            Assert.AreEqual(3f, m, 0.01f, "a second warped reaches the full boost");
            for (int i = 0; i < 120; i++) m = StoatDipoleMath.StepBoost(m, false, 3f, 20f, 0.6f, 1f / 60f);
            Assert.Less(m, 1.1f, "two seconds after the warp ends it is nearly gone");
            Assert.GreaterOrEqual(m, 1f);
        }

        [Test]
        public void OrbitCap_OneLapRoundTheSinkIsThreeSixtyDegrees()
        {
            // The reported defect: the autopilot circled its own sink four to six times, because a hull in
            // orbit keeps its path warped and the watching hold only ended at its 15 s maximum. The cap sums
            // the angle swept round the sink frame by frame; one lap has to read as one lap, whatever the
            // radius, the centre or the frame rate.
            var centre = new Vector3(40f, -15f, 300f);
            foreach (float radius in new[] { 30f, 150f })
            foreach (int frames in new[] { 30, 600 })
            {
                float swept = 0f;
                var last = centre + new Vector3(radius, 0f, 0f);
                for (int k = 1; k <= frames; k++)
                {
                    float a = 2f * Mathf.PI * k / frames;
                    var now = centre + new Vector3(radius * Mathf.Cos(a), 0f, radius * Mathf.Sin(a));
                    swept += StoatDipoleMath.SweptAround(centre, last, now);
                    last = now;
                }
                Assert.AreEqual(360f, swept, 0.5f, $"one lap at r={radius}, {frames} frames");
                // Float sums land a hair either side of 360 on the closing frame; one frame on, it has tripped.
                float a1 = 2f * Mathf.PI * (frames + 1) / frames;
                swept += StoatDipoleMath.SweptAround(centre, last,
                    centre + new Vector3(radius * Mathf.Cos(a1), 0f, radius * Mathf.Sin(a1)));
                Assert.IsTrue(StoatDipoleMath.OrbitCapReached(swept, 360f), "one lap and a frame reaches the one-lap cap");
                Assert.IsFalse(StoatDipoleMath.OrbitCapReached(swept - 30f, 360f), "a frame or two short of the lap does not");
            }
        }

        [Test]
        public void OrbitCap_APassByTheSinkNeverTripsIt()
        {
            // Flying straight past the sink (the dipole's ordinary use) sweeps under 180 degrees round it, so a
            // one-lap cap can never cut a pass short.
            var centre = Vector3.zero;
            float swept = 0f;
            var last = new Vector3(-5000f, 0f, 20f);
            for (int k = 1; k <= 2000; k++)
            {
                var now = new Vector3(-5000f + k * 5f, 0f, 20f);
                swept += StoatDipoleMath.SweptAround(centre, last, now);
                last = now;
            }
            Assert.Less(swept, 180f);
            Assert.IsFalse(StoatDipoleMath.OrbitCapReached(swept, 360f));
            Assert.IsFalse(StoatDipoleMath.OrbitCapReached(10000f, 0f), "0 = no cap");
            Assert.AreEqual(0f, StoatDipoleMath.SweptAround(centre, centre, Vector3.one), "a position on the centre sweeps nothing");
        }

        [Test]
        public void OrbitCap_LetsGoEarlyEnoughThatTheClosingPairEndsTheLap()
        {
            // Letting go AT 360 left the hull another half lap round the pair while its poles closed (the studio
            // measured 1.5 laps). The cap counts the sweep still to come: 200 degrees swept at 160 deg/s with a second
            // of closing left ends at 360, so it lets go now.
            float closing = StoatDipoleMath.ClosingSeconds(separation: 2.718282f * 10f, sinkHorizon: 10f, sourceHorizon: 10f, followRate: 1f);
            Assert.AreEqual(1f, closing, 1e-3f, "e x the threshold at rate 1 closes in one second");
            Assert.IsTrue(StoatDipoleMath.OrbitCapReached(200f, 360f, 160f, closing));
            Assert.IsFalse(StoatDipoleMath.OrbitCapReached(150f, 360f, 160f, closing), "a lap that would end at 310 holds on");
            Assert.AreEqual(0f, StoatDipoleMath.ClosingSeconds(5f, 10f, 10f, 1f), "poles already inside the threshold close at once");
        }

        [Test]
        public void Autopilot_OnARaceLine_JudgesTheRingsRange_WithTheLinesBearing()
        {
            // Regatta's race line keeps the steering point one look-ahead (60 u at cruise) ahead. Judged alone it is inside
            // the 90 u "far enough to lay a pair" test, so the Stoat never laid one; the ring it is after is 500 u away.
            var steer = new Vector3(20f, 0f, 57f);   // 60 u ahead, a little right
            Assert.IsFalse(StoatDipoleMath.AutopilotTriggers(steer, 90f, 12f, 180f, out _, out _),
                "negative control: the steering point alone never lays a pair");
            var gauge = StoatDipoleMath.AutopilotGauge(steer, 500f);
            Assert.AreEqual(500f, gauge.magnitude, 1e-3f);
            Assert.AreEqual(0f, Vector3.Angle(steer, gauge), 1e-3f, "the bearing stays the line's");
            Assert.IsTrue(StoatDipoleMath.AutopilotTriggers(gauge, 90f, 12f, 180f, out bool l, out bool r));
            Assert.IsTrue(r && !l, "the line bends right: the right trigger");
            Assert.IsFalse(StoatSlingMath.TryAutopilotSide(steer, 30f, 120f, out _),
                "negative control: the sling's 120 u test fails on the steering point too");
            Assert.AreEqual(Vector3.zero, StoatDipoleMath.AutopilotGauge(Vector3.zero, 500f), "no bearing: no target");
        }
    }
}
