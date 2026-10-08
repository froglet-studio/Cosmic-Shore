#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Skim Race AI's pure parts: course geometry, the observation/action schema, the
    /// decision core's steering/throttle/recovery, crystal selection, the shipped policy asset,
    /// deployment gating and the benchmark verdict. The in-game behaviour (actually racing the
    /// track) is validated by the benchmark — Docs/SKIM_RACE_AI.md.
    /// </summary>
    public class SkimRaceAITests
    {
        // A 400 x 400 square loop in the XZ plane, laid counter-clockwise from (200,0,-200).
        static SkimRaceCourse Square(float step = 10f)
        {
            var corners = new[] { new Vector3(200, 0, -200), new Vector3(200, 0, 200), new Vector3(-200, 0, 200), new Vector3(-200, 0, -200) };
            var pts = new List<Vector3>();
            var nrm = new List<Vector3>();
            for (int c = 0; c < 4; c++)
            {
                Vector3 a = corners[c], b = corners[(c + 1) % 4];
                int n = Mathf.RoundToInt(Vector3.Distance(a, b) / step);
                for (int i = 0; i < n; i++) { pts.Add(Vector3.Lerp(a, b, (float)i / n)); nrm.Add(Vector3.up); }
            }
            return new SkimRaceCourse(pts, nrm);
        }

        static SkimRaceAIConfigSO Config() => ScriptableObject.CreateInstance<SkimRaceAIConfigSO>();

        static SkimRaceObservation Obs(Vector3 pos, Vector3 fwd, Vector3? target, float speed = 200f)
        {
            var rot = Quaternion.LookRotation(fwd, Vector3.up);
            var o = new SkimRaceObservation
            {
                Position = pos, Forward = rot * Vector3.forward, Right = rot * Vector3.right, Up = rot * Vector3.up,
                CommandedForward = rot * Vector3.forward, Speed = speed, Velocity = fwd * speed,
                BoostMultiplier = 3f, MaxBoost = 5f, TurnRateDegrees = 120f, FollowRate = 1.5f, ThrottleScaler = 60f,
            };
            if (target.HasValue)
            {
                o.HasTarget = true;
                o.TargetPosition = target.Value;
                o.ToTarget = target.Value - pos;
                o.TargetDistance = o.ToTarget.magnitude;
                o.TargetAlignment = Vector3.Dot(o.Forward, o.ToTarget.normalized);
            }
            o.Sanitize();
            return o;
        }

        // ── Course ────────────────────────────────────────────────────────

        [Test]
        public void Course_LengthProjectionAndWrap()
        {
            var c = Square();
            Assert.That(c.Length, Is.EqualTo(1600f).Within(1f));
            int hint = -1;
            float s = c.Project(new Vector3(205, 0, 0), ref hint, out var closest, out var dist);
            Assert.That(s, Is.EqualTo(200f).Within(1f), "halfway up the first side");
            Assert.That(dist, Is.EqualTo(5f).Within(0.01f));
            Assert.That(closest.x, Is.EqualTo(200f).Within(0.01f));
            Assert.That(c.Wrap(1650f), Is.EqualTo(50f).Within(0.01f));
            Assert.That(c.Wrap(-50f), Is.EqualTo(1550f).Within(0.01f));
            Assert.That(c.Ahead(1550f, 50f), Is.EqualTo(100f).Within(0.01f), "ahead wraps across the start line");
            var p = c.Sample(200f, out var tangent, out var normal);
            Assert.That(Vector3.Distance(p, new Vector3(200, 0, 0)), Is.LessThan(0.01f));
            Assert.That(Vector3.Dot(tangent, Vector3.forward), Is.GreaterThan(0.99f));
            Assert.That(Vector3.Dot(normal, Vector3.up), Is.GreaterThan(0.99f));
        }

        [Test]
        public void Course_MaxTurnAhead_SeesTheCorner()
        {
            var c = Square();
            Assert.That(c.MaxTurnAhead(100f, 50f), Is.LessThan(1f), "a straight has no turn");
            Assert.That(c.MaxTurnAhead(350f, 100f), Is.EqualTo(90f).Within(1f), "the corner at s=400 is 90 degrees");
        }

        // ── Laid-mass guard ───────────────────────────────────────────────

        static SkimRaceObservation GuardObs()
        {
            var o = Obs(Vector3.zero, Vector3.forward, new Vector3(0, 0, 3000), 200f);
            o.Rotation = Quaternion.identity;
            o.CommandedRotation = Quaternion.identity;
            return o;
        }

        [Test]
        public void MassGuard_SteersAroundLaidMassDeadAhead()
        {
            var cfg = Config();
            var d = new SkimRaceDriver(cfg);
            d.Reset();
            var o = GuardObs();
            var clear = d.Decide(o, null, 1f, 0.02f);
            Assert.That(Mathf.Abs(clear.Yaw) + Mathf.Abs(clear.Pitch), Is.LessThan(0.05f), "nothing ahead: fly straight");

            d.Obstacles.Add(new SkimRaceObstacle { Center = new Vector3(0, 0, 60), Rotation = Quaternion.identity, Half = new Vector3(3, 3, 3) });
            var dodge = d.Decide(o, null, 1.02f, 0.02f);
            Assert.That(d.MassVetoes, Is.EqualTo(1), "a box 60 u ahead at 200 u/s is inside the guard's look-ahead");
            Assert.That(Mathf.Abs(dodge.Yaw) + Mathf.Abs(dodge.Pitch), Is.GreaterThan(0.4f), "the stick swerves");
        }

        [Test]
        public void MassGuard_IgnoresMassOffThePath()
        {
            var d = new SkimRaceDriver(Config());
            d.Reset();
            d.Obstacles.Add(new SkimRaceObstacle { Center = new Vector3(40, 0, 60), Rotation = Quaternion.identity, Half = new Vector3(0.4f, 0.4f, 3f) });
            d.Decide(GuardObs(), null, 1f, 0.02f);
            Assert.That(d.MassVetoes, Is.EqualTo(0), "a rail 40 u to the side is not a threat");
        }

        [Test]
        public void Obstacle_DistanceIsBoxSurfaceDistance()
        {
            var ob = new SkimRaceObstacle { Center = Vector3.zero, Rotation = Quaternion.Euler(0, 90, 0), Half = new Vector3(1, 1, 5) };
            Assert.That(ob.Distance(new Vector3(7, 0, 0)), Is.EqualTo(2f).Within(1e-4f), "rotated: long axis now along x");
            Assert.That(ob.Distance(Vector3.zero), Is.EqualTo(0f));
        }

        // ── Observation / action schema ───────────────────────────────────

        [Test]
        public void Observation_SanitizeRemovesNaNAndFeaturesAreBounded()
        {
            var o = new SkimRaceObservation
            {
                Position = new Vector3(float.NaN, 1, 2), Forward = new Vector3(float.PositiveInfinity, 0, 0),
                Speed = float.NaN, BoostMultiplier = float.NegativeInfinity, HasTarget = true,
                TargetPosition = new Vector3(float.NaN, 0, 0), TargetDistance = float.NaN, RaceTime = float.NaN,
                Collected = 3, Remaining = 21,
            };
            o.Sanitize();
            var f = new float[SkimRaceObservation.FeatureCount];
            o.WriteFeatures(f);
            for (int i = 0; i < f.Length; i++)
            {
                Assert.IsFalse(float.IsNaN(f[i]) || float.IsInfinity(f[i]), $"feature {i} not finite");
                Assert.That(f[i], Is.InRange(-1f, 1f), $"feature {i} out of range");
            }
            Assert.IsFalse(float.IsNaN(o.Position.x));
            Assert.That(o.Forward.sqrMagnitude, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Observation_MissingTargetZeroesTargetFields()
        {
            var o = new SkimRaceObservation { HasTarget = false, TargetPosition = new Vector3(1, 2, 3), TargetDistance = 9f };
            o.Sanitize();
            Assert.AreEqual(Vector3.zero, o.TargetPosition);
            Assert.AreEqual(0f, o.TargetDistance);
        }

        [Test]
        public void Observation_SchemaIsFixed()
        {
            // Training tooling and the in-game pilot read one struct; this pins its layout so a
            // field added without bumping SchemaVersion fails here rather than silently.
            Assert.AreEqual(1, SkimRaceObservation.SchemaVersion);
            Assert.AreEqual(30, SkimRaceObservation.FeatureCount);
            Assert.Throws<System.ArgumentException>(() => new SkimRaceObservation().WriteFeatures(new float[29]));
        }

        [Test]
        public void Action_ClampedIntoLegalRanges()
        {
            var a = new SkimRaceAction { Yaw = 3f, Pitch = -7f, Roll = float.NaN, Throttle = 1.5f }.Clamped();
            Assert.AreEqual(1f, a.Yaw);
            Assert.AreEqual(-1f, a.Pitch);
            Assert.AreEqual(0f, a.Roll);
            Assert.AreEqual(1f, a.Throttle);
            var n = SkimRaceAction.Neutral;
            Assert.AreEqual(0f, n.Throttle);
            Assert.IsFalse(n.Drift);
        }

        // ── Driver ────────────────────────────────────────────────────────

        [Test]
        public void Driver_SteersTowardTheCrystal()
        {
            var d = new SkimRaceDriver(Config());
            var right = d.Decide(Obs(Vector3.zero, Vector3.forward, new Vector3(100, 0, 150)), null, 0f, 0.016f);
            Assert.That(right.Yaw, Is.GreaterThan(0.05f), "crystal to the right yaws right");
            d.Reset();
            var left = d.Decide(Obs(Vector3.zero, Vector3.forward, new Vector3(-100, 0, 150)), null, 0f, 0.016f);
            Assert.That(left.Yaw, Is.LessThan(-0.05f), "crystal to the left yaws left");
            d.Reset();
            var up = d.Decide(Obs(Vector3.zero, Vector3.forward, new Vector3(0, 100, 150)), null, 0f, 0.016f);
            Assert.That(up.Pitch, Is.LessThan(-0.05f), "crystal above pitches the nose UP (negative pitch input)");
        }

        [Test]
        public void Driver_FullThrottleOnAStraightApproach()
        {
            var d = new SkimRaceDriver(Config());
            var a = d.Decide(Obs(Vector3.zero, Vector3.forward, new Vector3(0, 0, 400)), null, 0f, 0.016f);
            Assert.AreEqual(1f, a.Throttle);
        }

        [Test]
        public void Driver_EasesThrottleForACrystalInsideTheTurningCircle()
        {
            var cfg = Config();
            var d = new SkimRaceDriver(cfg);
            // 300 u/s at 120 deg/s is a ~143u turning radius; a crystal 60u off the beam cannot be made.
            var a = d.Decide(Obs(Vector3.zero, Vector3.forward, new Vector3(60, 0, 10), 300f), null, 0f, 0.016f);
            Assert.That(a.Throttle, Is.EqualTo(cfg.MinThrottle).Within(1e-4f));
            Assert.IsTrue(d.LastDiagnostics.Unreachable);
        }

        [Test]
        public void Driver_EntersRecoveryWhenProgressStalls_AndResetClearsIt()
        {
            var cfg = Config();
            var d = new SkimRaceDriver(cfg);
            var o = Obs(Vector3.zero, Vector3.right, new Vector3(0, 0, 150));
            float t = 0f;
            for (int i = 0; i < 400 && d.CurrentMode != SkimRaceDriver.Mode.Recovering; i++, t += 0.02f)
                d.Decide(o, null, t, 0.02f);
            Assert.AreEqual(SkimRaceDriver.Mode.Recovering, d.CurrentMode, "no closing for StallSeconds engages recovery");
            Assert.AreEqual(1, d.Recoveries);
            var a = d.Decide(o, null, t, 0.02f);
            Assert.That(a.Throttle, Is.LessThanOrEqualTo(cfg.RecoveryThrottle + 1e-4f));

            d.Reset();
            Assert.AreEqual(SkimRaceDriver.Mode.Idle, d.CurrentMode);
            Assert.AreEqual(0, d.Recoveries);
        }

        [Test]
        public void Driver_NewCrystalRestartsTheProgressClock()
        {
            var d = new SkimRaceDriver(Config());
            var o = Obs(Vector3.zero, Vector3.forward, new Vector3(0, 0, 300));
            for (int i = 0; i < 100; i++) d.Decide(o, null, i * 0.02f, 0.02f);
            Assert.That(d.TimeSinceProgress, Is.GreaterThan(1.5f));
            d.Decide(Obs(Vector3.zero, Vector3.forward, new Vector3(0, 0, 900)), null, 2.1f, 0.02f);
            Assert.That(d.TimeSinceProgress, Is.LessThan(0.05f), "the crystal moved (collected and respawned)");
        }

        [Test]
        public void Driver_FollowsTheCourseWhenTheCrystalIsFarAlongIt()
        {
            var c = Square();
            var d = new SkimRaceDriver(Config());
            // Crystal on the far (top) side: ~550 ahead along the course, well past the bump.
            var o = Obs(new Vector3(200, 6, -150), Vector3.forward, new Vector3(0, 0, 200), 200f);
            int hint = -1, targetHint = -1;
            o.HasCourse = true;
            o.CourseLength = c.Length;
            o.CourseProgress = c.Project(o.Position, ref hint, out _, out o.CourseDistance);
            o.TargetAheadOnCourse = c.Ahead(o.CourseProgress, c.Project(o.TargetPosition, ref targetHint, out _, out _));
            d.Decide(o, c, 0f, 0.016f);
            Assert.IsFalse(d.LastDiagnostics.CrystalPull, "far crystal: fly the racing line");
            Assert.That(d.LastDiagnostics.AimPoint.x, Is.EqualTo(200f).Within(20f), "aim stays on the first side");
            Assert.That(d.LastDiagnostics.AimPoint.z, Is.GreaterThan(o.Position.z));
        }

        [Test]
        public void Driver_ChasesAnOvershotCrystalDirectly()
        {
            var c = Square();
            var d = new SkimRaceDriver(Config());
            // Crystal just BEHIND the vessel along the course, outside the direct radius.
            var o = Obs(new Vector3(200, 6, 100), Vector3.forward, new Vector3(200, 0, -150), 200f);
            int hint = -1;
            o.HasCourse = true;
            o.CourseLength = c.Length;
            o.CourseProgress = c.Project(o.Position, ref hint, out _, out o.CourseDistance);
            o.TargetAheadOnCourse = c.Ahead(o.CourseProgress, c.Project(o.TargetPosition, ref hint, out _, out _));
            d.Decide(o, c, 0f, 0.016f);
            Assert.IsTrue(d.LastDiagnostics.CrystalPull, "an overshot crystal is chased, not lapped");
        }

        [Test]
        public void Driver_NoTargetFallsBackToTheCourse()
        {
            var c = Square();
            var d = new SkimRaceDriver(Config());
            var o = Obs(new Vector3(200, 6, -150), Vector3.forward, null, 200f);
            int hint = -1;
            o.HasCourse = true;
            o.CourseLength = c.Length;
            o.CourseProgress = c.Project(o.Position, ref hint, out _, out o.CourseDistance);
            var a = d.Decide(o, c, 0f, 0.016f);
            Assert.IsFalse(d.LastDiagnostics.CrystalPull);
            Assert.AreEqual(1f, a.Throttle);
        }

        [Test]
        public void Driver_LanesSkimAtSeparateHeights()
        {
            // Two AI seats on the same course: lane 1's line sits LaneHeightStep above lane 0's, so
            // one seat never flies at the height of the other's trail rails.
            var c = Square();
            var cfg = Config();
            Vector3 AimFor(int lane)
            {
                var d = new SkimRaceDriver(cfg) { Lane = lane };
                d.Reset();
                var o = Obs(new Vector3(200, 6, -150), Vector3.forward, null, 200f);
                int hint = -1;
                o.HasCourse = true;
                o.CourseLength = c.Length;
                o.CourseProgress = c.Project(o.Position, ref hint, out _, out o.CourseDistance);
                d.Decide(o, c, 0f, 0.016f);
                return d.LastDiagnostics.AimPoint;
            }
            float dh = AimFor(1).y - AimFor(0).y;
            Assert.That(dh, Is.EqualTo(cfg.LaneHeightStep).Within(0.01f));
        }

        [Test]
        public void Action_ClampedIntoHumanRanges()
        {
            // The pilot clamps at actuation whatever the policy produced (anti-cheat contract).
            var a = new SkimRaceAction { Yaw = 5f, Pitch = -7f, Roll = 3f, Throttle = 4f }.Clamped();
            Assert.AreEqual(1f, a.Yaw);
            Assert.AreEqual(-1f, a.Pitch);
            Assert.AreEqual(1f, a.Roll);
            Assert.AreEqual(1f, a.Throttle);
        }

        // ── Target selection ──────────────────────────────────────────────

        static SkimRaceTargetTracker.Candidate C(Domains d, Vector3 p, bool exploding = false, bool embedded = false, bool alive = true) =>
            new() { Alive = alive, Domain = d, Position = p, Exploding = exploding, Embedded = embedded };

        [Test]
        public void Tracker_PicksNearestCrystalOfOwnDomainOnly()
        {
            var list = new List<SkimRaceTargetTracker.Candidate>
            {
                C(Domains.Ruby, new Vector3(0, 0, 10)),      // opposing team: ignored
                C(Domains.Jade, new Vector3(0, 0, 500)),
                C(Domains.Jade, new Vector3(0, 0, 50), embedded: true), // a lifeform heart: ignored
                C(Domains.Jade, new Vector3(0, 0, 200)),
                C(Domains.Jade, new Vector3(0, 0, 30), alive: false),   // destroyed: ignored
            };
            Assert.AreEqual(3, SkimRaceTargetTracker.SelectIndex(list, Domains.Jade, Vector3.zero, -1));
            Assert.AreEqual(-1, SkimRaceTargetTracker.SelectIndex(list, Domains.Gold, Vector3.zero, -1));
        }

        [Test]
        public void Tracker_ExplodingCrystalOnTopOfPilotIsSkipped_ButItsRespawnIsAValidAim()
        {
            var onTop = new List<SkimRaceTargetTracker.Candidate> { C(Domains.Jade, new Vector3(0, 0, 5), exploding: true) };
            Assert.AreEqual(-1, SkimRaceTargetTracker.SelectIndex(onTop, Domains.Jade, Vector3.zero, -1));
            var moved = new List<SkimRaceTargetTracker.Candidate> { C(Domains.Jade, new Vector3(0, 0, 500), exploding: true) };
            Assert.AreEqual(0, SkimRaceTargetTracker.SelectIndex(moved, Domains.Jade, Vector3.zero, -1));
        }

        [Test]
        public void Tracker_HysteresisHoldsANearTie()
        {
            var list = new List<SkimRaceTargetTracker.Candidate>
            {
                C(Domains.Jade, new Vector3(0, 0, 100)),
                C(Domains.Jade, new Vector3(0, 0, -95)),
            };
            Assert.AreEqual(0, SkimRaceTargetTracker.SelectIndex(list, Domains.Jade, Vector3.zero, 0), "5% closer is not enough to switch");
            Assert.AreEqual(1, SkimRaceTargetTracker.SelectIndex(list, Domains.Jade, Vector3.zero, -1));
        }

        // ── Policy asset & deployment ─────────────────────────────────────

        [Test]
        public void Policy_ShippedAssetLoadsFromResources()
        {
            var cfg = Resources.Load<SkimRaceAIConfigSO>(SkimRaceAIConfigSO.ResourcePath);
            Assert.IsNotNull(cfg, "Resources/SkimRaceAIConfig.asset must ship so a clean build loads the policy");
            Assert.IsFalse(string.IsNullOrEmpty(cfg.PolicyVersion));
            Assert.IsTrue(cfg.DeployInNormalPlay);
            Assert.AreSame(cfg, SkimRaceAIConfigSO.LoadDefault());
        }

        [Test]
        public void Deployment_ClaimsOnlyNormalSkimRaceAndRegatta()
        {
            var gd = ScriptableObject.CreateInstance<GameDataSO>();
            try
            {
                gd.GameMode = GameModes.SkimRace;
                gd.IsTraining = false;
                Assert.IsTrue(SkimRaceAIDeployment.Claims(gd));
                gd.IsTraining = true;
                Assert.IsFalse(SkimRaceAIDeployment.Claims(gd), "the genetic trainer owns the seats while training");
                gd.IsTraining = false;
                gd.GameMode = GameModes.Regatta;
                Assert.IsTrue(SkimRaceAIDeployment.Claims(gd), "Regatta's Squirrels fly the same pilot at rings");
                Assert.IsInstanceOf<RegattaRingObjective>(SkimRaceObjective.For(gd));
                gd.GameMode = GameModes.SkimRace;
                Assert.IsInstanceOf<CrystalTrackObjective>(SkimRaceObjective.For(gd));
                gd.GameMode = GameModes.Joust;
                Assert.IsNull(SkimRaceObjective.For(gd));
                Assert.IsFalse(SkimRaceAIDeployment.Claims(gd));
                Assert.IsFalse(SkimRaceAIDeployment.Claims(null));
            }
            finally { Object.DestroyImmediate(gd); }
        }

        // ── Crossing controls (off by default) ───────────────────────────

        [Test]
        public void CrossingControls_DefaultToNoOp()
        {
            var cfg = Config();
            Assert.AreEqual(0f, cfg.CrossingLeadSeconds);
            Assert.AreEqual(1f, cfg.CrossingLookaheadScale);
            Assert.AreEqual(1f, cfg.CrossingThrottle);
            Assert.IsFalse(cfg.MpcStrikeUsesBoostLoss);
            Assert.AreEqual(0f, cfg.TrackMpcStrikeCost);
        }

        // Vessel 50 u up the square's first leg, crystal 100 u further on and 60 u BELOW the ribbon:
        // too deep for the top face, so the pass is a face change.
        static SkimRaceObservation CrossingObs(SkimRaceCourse c)
        {
            var o = Obs(new Vector3(200f, 6f, -150f), Vector3.forward, new Vector3(200f, -60f, -50f));
            int h = -1;
            o.HasCourse = true;
            o.CourseLength = c.Length;
            o.CourseProgress = c.Project(o.Position, ref h, out _, out o.CourseDistance);
            c.Sample(o.CourseProgress, out o.CourseTangent, out _);
            int th = -1;
            o.TargetAheadOnCourse = c.Ahead(o.CourseProgress, c.Project(o.TargetPosition, ref th, out _, out _));
            o.TargetRadius = 24f;
            return o;
        }

        [Test]
        public void CrossingThrottle_CapsOnlyWhenSet()
        {
            var c = Square();
            var off = Config();
            off.ReachabilityMargin = 0f;   // isolate the crossing cap from the Dubins ease-off
            var d0 = new SkimRaceDriver(off);
            var a0 = d0.Decide(CrossingObs(c), c, 0f, 0.02f);
            Assert.IsTrue(d0.Crossing, "the scenario must plan a face change");
            Assert.AreEqual(off.CruiseThrottle, a0.Throttle, 1e-4f, "default: no cap");

            var on = Config();
            on.ReachabilityMargin = 0f;
            on.CrossingThrottle = 0.5f;
            on.CrossingSlowDistance = 150f;
            var a1 = new SkimRaceDriver(on).Decide(CrossingObs(c), c, 0f, 0.02f);
            Assert.AreEqual(0.5f, a1.Throttle, 1e-4f);
        }

        [Test]
        public void TrackerAndCaptureSearch_DefaultToOff_AndAreUnsetInEveryShippedPolicy()
        {
            Assert.IsFalse(Config().UseLineTracker);
            Assert.IsFalse(Config().CaptureThrottleSearch);
            foreach (var name in new[] { "SkimRaceAIConfig", "SkimRaceAIConfig_I1", "SkimRaceAIConfig_I2", "SkimRaceAIConfig_I4" })
            {
                var p = Resources.Load<SkimRaceAIConfigSO>(name);
                Assert.IsNotNull(p, name);
                Assert.IsFalse(p.UseLineTracker, name);
                Assert.IsFalse(p.CaptureThrottleSearch, name);
            }
        }

        [Test]
        public void CaptureThrottleSearch_LiftsOnlyWhenTheRolloutMisses()
        {
            // A crystal dead ahead is captured at full throttle: the search must not lift.
            var cfg = Config();
            cfg.CaptureThrottleSearch = true;
            cfg.ReachabilityMargin = 0f;
            var d = new SkimRaceDriver(cfg);
            var ahead = d.Decide(Obs(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 120f)), null, 0f, 0.02f);
            Assert.AreEqual(cfg.CruiseThrottle, ahead.Throttle, 1e-4f);

            // A crystal close and abeam at 250 u/s is passed outside the sphere at full throttle: lift.
            var d2 = new SkimRaceDriver(cfg);
            var abeam = d2.Decide(Obs(Vector3.zero, Vector3.forward, new Vector3(70f, 0f, 60f), speed: 250f), null, 0f, 0.02f);
            Assert.Less(abeam.Throttle, cfg.CruiseThrottle);
        }

        [Test]
        public void ShippedPolicies_DoNotEnableUnsetCrossingControls_OnI1()
        {
            var i1 = Resources.Load<SkimRaceAIConfigSO>("SkimRaceAIConfig_I1");
            Assert.IsNotNull(i1);
            Assert.AreEqual(0f, i1.CrossingLeadSeconds);
            Assert.AreEqual(1f, i1.CrossingLookaheadScale);
            Assert.AreEqual(1f, i1.CrossingThrottle);
        }

        // ── Benchmark verdict ─────────────────────────────────────────────

        [Test]
        public void Verdict_SuccessRequiresWinAllCrystalsAndTime()
        {
            Assert.IsTrue(SkimRaceRaceRecorder.Evaluate(true, "Ruby", "Ruby", 24, 24, 70.0f, 70f, null, out _));
            Assert.IsTrue(SkimRaceRaceRecorder.Evaluate(true, "Ruby", "Ruby", 24, 24, 48.3f, 70f, null, out _));
            Assert.IsFalse(SkimRaceRaceRecorder.Evaluate(true, "Ruby", "Ruby", 24, 24, 70.01f, 70f, null, out var slow));
            StringAssert.Contains("> 70", slow);
            Assert.IsFalse(SkimRaceRaceRecorder.Evaluate(true, "Ruby", "Ruby", 23, 24, 50f, 70f, null, out var few));
            StringAssert.Contains("23/24", few);
            Assert.IsFalse(SkimRaceRaceRecorder.Evaluate(true, "Jade", "Ruby", 24, 24, 50f, 70f, null, out _), "another domain won");
            Assert.IsFalse(SkimRaceRaceRecorder.Evaluate(false, "Blue", "Ruby", 10, 24, 0f, 70f, "timeout", out var to));
            Assert.AreEqual("timeout", to);
            Assert.IsFalse(SkimRaceRaceRecorder.Evaluate(true, "Ruby", "Ruby", 24, 24, 0f, 70f, null, out _), "no finish time is not a finish");
        }

        [Test]
        public void Verdict_JudgesAgainstTheLimitItIsGiven()
        {
            Assert.IsTrue(SkimRaceRaceRecorder.Evaluate(true, "Ruby", "Ruby", 30, 30, 79.9f, 80f, null, out _));
            Assert.IsFalse(SkimRaceRaceRecorder.Evaluate(true, "Ruby", "Ruby", 30, 30, 80.01f, 80f, null, out var slow));
            StringAssert.Contains("> 80", slow);
        }

        [Test]
        public void DefaultLimit_OnlyIntensityTwoWasRebaselined()
        {
            Assert.AreEqual(70f, SkimRaceRaceRecorder.DefaultLimitSeconds(1));
            Assert.AreEqual(80f, SkimRaceRaceRecorder.DefaultLimitSeconds(2));
            Assert.AreEqual(70f, SkimRaceRaceRecorder.DefaultLimitSeconds(3));
            Assert.AreEqual(70f, SkimRaceRaceRecorder.DefaultLimitSeconds(4));
        }

        [Test]
        public void LevelApproach_DefaultsToOff_AndIsUnsetInI1AndI4()
        {
            Assert.IsFalse(Config().UseLevelApproach);
            foreach (var name in new[] { "SkimRaceAIConfig", "SkimRaceAIConfig_I1", "SkimRaceAIConfig_I4" })
            {
                var p = Resources.Load<SkimRaceAIConfigSO>(name);
                Assert.IsNotNull(p, name);
                Assert.IsFalse(p.UseLevelApproach, name);
            }
        }
    }
}
#endif
