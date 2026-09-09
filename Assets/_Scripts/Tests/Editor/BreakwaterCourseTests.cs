using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Breakwater course contract. Every station is randomly placed and randomly oriented,
    /// and the whole mode rests on a handful of things being true anyway - the course fits the
    /// cell, no corner is sharper than a Sparrow can hold, no plug stands edge-on to the line you
    /// arrive on, no two ports are close enough to be confused, and the walk never fails to
    /// finish. Those are asserted here across a 400-seed sweep of every shipped intensity rather
    /// than eyeballed in the editor, because a generated course is exactly the kind of thing that
    /// is fine on the seed you looked at.
    ///
    /// <para><b>This is the C# half of a two-sided proof.</b>
    /// <c>Tools/Build/breakwater_arena.py</c> measured all of it offline first - 0 generation
    /// failures, 0 Dubins violations and every cap holding over the same 400 x 4 sweep - and
    /// <see cref="BreakwaterCourse"/> is a bit-for-bit mirror of that model's walk. So a failure
    /// here is not "the course got unlucky"; it is the two sides having drifted apart, which also
    /// invalidates the arena's PhaseThresholds (the asset generator derives them from the same
    /// file). A <c>null</c> from <see cref="BreakwaterCourse.Generate"/> on a swept seed is a real
    /// divergence, not the documented ~0.1% residual: these particular seeds are known to walk.
    /// </para>
    ///
    /// <para><b>The seeds are the model's own schedule</b>, <c>seed * 7919 + intensity</c>, not
    /// 1..400. Sweeping a different 400 would be a different sample: at the residual failure rate
    /// a fresh set is ~0.4 expected failures over 1600 courses, so a test that invented its own
    /// seeds would be flaky for a reason that has nothing to do with the code. (Both sets were in
    /// fact checked and both are clean - but only one of them is the set the model's claims are
    /// about, and the tightest margin in the whole sweep, 0.9 units of separation at intensity 4,
    /// lives in this one.)</para>
    /// </summary>
    public class BreakwaterCourseTests
    {
        const int Seeds = 400;

        /// <summary>
        /// The course length, taken from the authored end-game default rather than retyped. It is
        /// deliberately absent from <see cref="BreakwaterCourseSettings.ForIntensity"/> - the
        /// station count IS the race target and lives in <c>EndConditionOverridesSO</c>, read
        /// once as "how many stations to hang" and once as "how many end the turn" - so a test
        /// that pasted 14 would be a third copy of the one number that must never disagree.
        /// </summary>
        const int Stations = EndConditionOverridesSO.DefaultBreakwaterStationTarget;

        // ── The Sparrow's turning circle, DERIVED ────────────────────────
        // R = v / omega, built back up from the ship's own numbers rather than pasted as ~130.1,
        // so a retune of the vessel shows up here as a course that no longer clears instead of as
        // a stale constant that still passes.
        //   v     = XDiff * DefaultThrottleScaler * boost * Mult(Time) + MinimumSpeed
        //   omega = RotationThrottleScaler * v + PitchScaler      (degrees/second)
        //
        // EVERY ONE OF THESE IS READ OFF Sparrow.prefab, NOT off VesselTransformer. The class's
        // field initializers are DefaultThrottleScaler 50, PitchScaler 130, RotationThrottleScaler
        // 0 - the prefab overrides all three (25 / 80 / 0.1, with DefaultMinimumSpeed 10 and
        // boostMultiplier 5), and re-deriving this from the class defaults yields a different ship
        // and a silently wrong clearance. That is the trap CLAUDE.md records in the general form:
        // a number read off a field initializer is not the number the game runs on.
        //
        // Evaluated at the TRANSIENT CEILING - full throttle, boosting, at the top of the
        // overcharge band - because that is the state in which a racer is least able to correct,
        // and a course has to be flyable in the state you actually take a corner in.
        const float DefaultThrottleScaler = 25f;
        const float BoostMultiplier = 5f;
        const float MinimumSpeed = 10f;
        const float RotationThrottleScaler = 0.1f;
        const float PitchScaler = 80f;
        const float CeilingThrottle = 1f;       // XDiff at full
        const float CeilingTimeMultiplier = 1.8f;   // Time 15, the overcharge ceiling

        const float CeilingSpeed =
            CeilingThrottle * DefaultThrottleScaler * BoostMultiplier * CeilingTimeMultiplier + MinimumSpeed;   // 235 u/s
        const float CeilingTurnRate = RotationThrottleScaler * CeilingSpeed + PitchScaler;                      // 103.5 deg/s

        /// <summary>~130.1 u. Never pasted - see the derivation above.</summary>
        static float CeilingTurnRadius => CeilingSpeed / (CeilingTurnRate * Mathf.Deg2Rad);

        static BreakwaterCourseSettings Settings(int intensity)
        {
            var s = BreakwaterCourseSettings.ForIntensity(intensity);
            s.StationCount = Stations;
            return s;
        }

        /// <summary>The model's seed schedule - see the class doc on why it is not 1..400.</summary>
        static int SeedFor(int seed, int intensity) => seed * 7919 + intensity;

        static List<BreakwaterStation> Course(int intensity, int seed) =>
            BreakwaterCourse.Generate(SeedFor(seed, intensity), Settings(intensity));

        // ── The walk always terminates ───────────────────────────────────

        [Test]
        public void EverySeedProducesAFullCourse([Values(1, 2, 3, 4)] int intensity)
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var course = Course(intensity, seed);
                Assert.IsNotNull(course,
                    $"intensity {intensity} seed {seed} produced no course - the model measured " +
                    "0/400 failures here, so this is a divergence from it, not bad luck.");
                Assert.AreEqual(Stations, course.Count,
                    $"intensity {intensity} seed {seed} produced {course.Count} stations.");
            }
        }

        // ── It fits the cell ─────────────────────────────────────────────

        [Test]
        public void EveryStationSitsInsideTheCourseShell([Values(1, 2, 3, 4)] int intensity)
        {
            var s = Settings(intensity);
            for (int seed = 1; seed <= Seeds; seed++)
                foreach (var station in Course(intensity, seed))
                {
                    float r = station.Position.magnitude;
                    Assert.GreaterOrEqual(r, s.InnerRadius - 0.01f,
                        $"intensity {intensity} seed {seed}: a station at {r:F1} is inside the shell floor.");
                    Assert.LessOrEqual(r, s.OuterRadius + 0.01f,
                        $"intensity {intensity} seed {seed}: a station at {r:F1} is outside the membrane.");
                }
        }

        [Test]
        public void StationOneSitsOnTheSpawnFormationPole([Values(1, 2, 3, 4)] int intensity)
        {
            // The fairness rule: pilots spawn on an EQUATORIAL ring, so a first station on that
            // ring's axis is exactly equidistant from all of them. Anywhere else and whoever
            // spawned nearest starts the race ahead - and here that is worth more than a head
            // start, because the pilot who arrives first also gets the undamaged plug and the
            // choice of how to open it.
            float d = Settings(intensity).FirstStationDistance;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var first = Course(intensity, seed)[0].Position;
                Assert.AreEqual(0f, first.x, 0.01f, $"seed {seed}: station 1 is off the pole in x.");
                Assert.AreEqual(0f, first.z, 0.01f, $"seed {seed}: station 1 is off the pole in z.");
                Assert.AreEqual(d, first.y, 0.01f, $"seed {seed}: station 1 is not at the authored pole distance.");
            }
        }

        [Test]
        public void EveryLegIsInsideTheAuthoredStepRange([Values(1, 2, 3, 4)] int intensity)
        {
            // The step is drawn from [MinStep, MaxStep] and never adjusted afterwards - the
            // wall-avoidance branch re-aims the DIRECTION and keeps the drawn length. If a leg
            // ever falls outside the range, that branch has started scaling the step, which would
            // silently invalidate the Dubins clearance below (it is a statement about legs) and
            // the shoal density (six clusters per leg, whatever the leg is).
            var s = Settings(intensity);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, seed);
                for (int i = 1; i < c.Count; i++)
                {
                    float leg = (c[i].Position - c[i - 1].Position).magnitude;
                    Assert.GreaterOrEqual(leg, s.MinStep - 0.01f,
                        $"intensity {intensity} seed {seed} leg {i}: {leg:F1} is shorter than the {s.MinStep} floor.");
                    Assert.LessOrEqual(leg, s.MaxStep + 0.01f,
                        $"intensity {intensity} seed {seed} leg {i}: {leg:F1} is longer than the {s.MaxStep} ceiling.");
                }
            }
        }

        // ── It is flyable ────────────────────────────────────────────────

        [Test]
        public void NoCornerExceedsTheTurnCap([Values(1, 2, 3, 4)] int intensity)
        {
            // Structural, not lucky: the heading only advances when a station is PLACED, and
            // every proposal - including the one that steers off the shell wall - goes through
            // the same clamp. A wall can therefore never manufacture a hairpin.
            float cap = Settings(intensity).MaxTurnDegrees;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, seed);
                for (int i = 1; i < c.Count - 1; i++)
                {
                    Vector3 inbound = (c[i].Position - c[i - 1].Position).normalized;
                    Vector3 outbound = (c[i + 1].Position - c[i].Position).normalized;
                    float turn = BreakwaterCourse.Angle(inbound, outbound);
                    Assert.LessOrEqual(turn, cap + 0.05f,
                        $"intensity {intensity} seed {seed} station {i}: {turn:F1} deg corner exceeds the {cap} cap.");
                }
            }
        }

        [Test]
        public void NoStationStandsEdgeOnToTheLineYouArriveOn([Values(1, 2, 3, 4)] int intensity)
        {
            // A plug presenting edge-on is not a hard station, it is a broken one: its eye cannot
            // be threaded at any roll, and its own dish stands between the blast and the weave, so
            // the rocket option goes with it. The jitter budget is what makes this hold - a corner
            // spends half its turn on the presentation and the jitter can only spend what is left.
            float cap = Settings(intensity).MaxPresentDegrees;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, seed);
                for (int i = 1; i < c.Count; i++)
                {
                    Vector3 arrive = (c[i].Position - c[i - 1].Position).normalized;
                    float present = BreakwaterCourse.Angle(arrive, c[i].Axis);
                    if (present > 90f) present = 180f - present;   // a port is threadable both ways
                    Assert.LessOrEqual(present, cap + 0.05f,
                        $"intensity {intensity} seed {seed} station {i}: presents {present:F1} deg off the arriving line.");
                }
            }
        }

        [Test]
        public void EveryCornerClearsTheSparrowsTurningCircleAtTheTransientCeiling([Values(1, 2, 3, 4)] int intensity)
        {
            // Dubins: pure pursuit cannot reach a target inside a circle of radius R tangent to
            // its velocity, so a leg shorter than 2R*sin(turn) is a corner nobody can make -
            // human or AI - and no amount of turning harder fixes it. Checked at the transient
            // ceiling because that is when a pilot is committed and least able to correct, and
            // because the comeback system hands the overcharge band to whoever is LOSING: the
            // hardest state to fly is the one the trailing pilot is in.
            float radius = CeilingTurnRadius;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, seed);
                for (int i = 1; i < c.Count - 1; i++)
                {
                    Vector3 inbound = (c[i].Position - c[i - 1].Position).normalized;
                    Vector3 outbound = (c[i + 1].Position - c[i].Position).normalized;
                    float turn = BreakwaterCourse.Angle(inbound, outbound) * Mathf.Deg2Rad;
                    float leg = (c[i + 1].Position - c[i].Position).magnitude;
                    float needed = 2f * radius * Mathf.Sin(turn);
                    Assert.Greater(leg, needed,
                        $"intensity {intensity} seed {seed} station {i}: leg {leg:F0} is inside the " +
                        $"turning circle at the transient ceiling ({needed:F0} needed at R={radius:F1}).");
                }
            }
        }

        // ── The stations cannot be confused ──────────────────────────────

        [Test]
        public void NoTwoStationsComeWithinTheDerivedSeparation([Values(1, 2, 3, 4)] int intensity)
        {
            // Ordered stations only read as a course while a pilot can tell which ring is theirs.
            // Four port radii is "these are clearly two places" - and it is checked over EVERY
            // pair, not just consecutive ones, because the walk folds back on itself and a station
            // eight legs later can land beside one you have already threaded.
            float floor = Settings(intensity).MinSeparation;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, seed);
                for (int i = 0; i < c.Count; i++)
                    for (int j = i + 1; j < c.Count; j++)
                    {
                        float d = (c[i].Position - c[j].Position).magnitude;
                        Assert.Greater(d, floor,
                            $"intensity {intensity} seed {seed}: stations {i} and {j} are {d:F0} apart, " +
                            $"inside the {floor:F0} separation floor.");
                    }
            }
        }

        [Test]
        public void TheSeparationFloorStaysBelowTheShortestLeg([Values(1, 2, 3, 4)] int intensity)
        {
            // THE property that starved an earlier ladder, asserted explicitly because its
            // violation does not look like a geometry bug - it looks like a course that will not
            // generate. TooClose tests a candidate against every placed station INCLUDING its
            // immediate predecessor, so a separation at or above the minimum leg rejects the
            // bottom of the step range before the walk has considered any geometry at all. The
            // cut that authored the separation directly rather than deriving it failed 21% of
            // seeds this way.
            var s = Settings(intensity);
            Assert.Less(s.MinSeparation, s.MinStep,
                $"intensity {intensity}: the {s.MinSeparation:F0} separation floor has reached the " +
                $"{s.MinStep:F0} minimum leg - the walk will starve, not misplace.");

            // ...and it really is the derivation, not a number that happens to sit below it.
            Assert.AreEqual(BreakwaterCourse.MinSeparationFor(s.PortRadius, s.MinStep), s.MinSeparation, 1e-4f,
                $"intensity {intensity}: the separation has been authored instead of derived.");
        }

        // ── It is reproducible ───────────────────────────────────────────

        [Test]
        public void TheSameSeedAlwaysProducesTheSameCourse()
        {
            // The walk is a specified xorshift32 over integer arithmetic with no UnityEngine or
            // System random anywhere, so this is a real invariant across machines and runtimes -
            // which is what lets the Python model's proofs be statements about the shipped arena.
            var a = Course(3, 987654);
            var b = Course(3, 987654);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position, $"station {i} position drifted between runs.");
                Assert.AreEqual(a[i].Axis, b[i].Axis, $"station {i} axis drifted between runs.");
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentCourses()
        {
            var a = Course(3, 11);
            var b = Course(3, 12);
            Assert.AreNotEqual(a[1].Position, b[1].Position, "two seeds produced the same course.");
        }

        // ── The shipped ladder is a ladder ───────────────────────────────

        [Test]
        public void IntensityTightensThePortAndOpensTheCornersMonotonically()
        {
            // Intensity is the COURSE AND THE DOOR, not the arena: the port narrows and the
            // corners open. Those two run OPPOSITE ways on purpose - inside a 660-unit-thick
            // shell, long legs and tight corners are the same constraint pulling against each
            // other, and the cut that gave the easiest level the gentlest corners failed 21% of
            // seeds. If either row ever stops moving in one direction the ladder has stopped
            // meaning anything, and nothing else in this file would notice.
            for (int i = 1; i < 4; i++)
            {
                var lo = BreakwaterCourseSettings.ForIntensity(i);
                var hi = BreakwaterCourseSettings.ForIntensity(i + 1);
                Assert.Less(hi.PortRadius, lo.PortRadius, $"the port did not tighten from {i} to {i + 1}.");
                Assert.Greater(hi.MaxTurnDegrees, lo.MaxTurnDegrees, $"corners did not open from {i} to {i + 1}.");
                Assert.Greater(hi.AxisJitterDegrees, lo.AxisJitterDegrees, $"stations did not twist further from {i} to {i + 1}.");
                Assert.Greater(hi.MaxPresentDegrees, lo.MaxPresentDegrees, $"the presentation cap did not widen from {i} to {i + 1}.");

                // The legs shorten as the corners open - a hard course is a busy one, not a
                // sprawling one. The strict assertion is on MaxStep because MinStep TIES between
                // 1 and 2 (both 300); the ladder spends that first step on the port and the
                // corner cap instead, so LessOrEqual is the honest shape there and still catches
                // a row that reverses.
                Assert.Less(hi.MaxStep, lo.MaxStep, $"the longest leg did not shorten from {i} to {i + 1}.");
                Assert.LessOrEqual(hi.MinStep, lo.MinStep, $"the shortest leg grew from {i} to {i + 1}.");
            }
        }

        // ── The door ladder ──────────────────────────────────────────────

        /// <summary>
        /// The port ladder is pinned at BOTH ends by the skyburst's blast, and the interesting
        /// property is where it crosses between them. <c>AOEExplosion.prefab</c> cuts a spherical
        /// door of radius 50 at resting Charge (85 at Charge 10), so:
        ///
        /// <list type="bullet">
        /// <item>at intensity 4 the port must sit UNDER 50, or the hardest course requires an
        /// element level to open - and the comeback system hands element levels to whoever is
        /// losing, so the leader would be the one who cannot open a door;</item>
        /// <item>at intensity 1 it must sit OVER 50, or one rocket takes the whole plug and the
        /// easy level's whole teaching - that WHERE you cut is a choice - never happens.</item>
        /// </list>
        ///
        /// Both are asserted rather than trusted because the port is also the dish's basis and the
        /// ammo bank's size, so it is exactly the number somebody retunes for a different reason.
        /// </summary>
        [Test]
        public void ThePortLadderStraddlesTheRestingBlastRadius()
        {
            const float RestingBlastRadius = 50f;

            Assert.LessOrEqual(BreakwaterCourseSettings.PortRadiusForIntensity(4), RestingBlastRadius,
                "intensity 4's port is wider than the resting-Charge blast - the hardest course " +
                "now needs an upgrade to open, and the comeback hands that upgrade to the loser.");

            Assert.Greater(BreakwaterCourseSettings.PortRadiusForIntensity(1), RestingBlastRadius,
                "intensity 1's port fits inside one resting-Charge blast - WHERE you cut has " +
                "stopped being a choice, which is the easy level's entire lesson.");
        }

        // ── Both gaps clear the ship ─────────────────────────────────────

        [Test]
        public void EveryPortClearsTheSparrowHull([Values(1, 2, 3, 4)] int intensity)
        {
            // An unopened plug still leaves its port, so this is the gap a pilot flies when they
            // have spent their rockets and will not stop to saw.
            float port = BreakwaterCourseSettings.PortRadiusForIntensity(intensity);
            Assert.Greater(port, BreakwaterCourseSettings.SparrowHullRadius,
                $"intensity {intensity}'s port cannot be flown through.");
        }

        /// <summary>
        /// The eye is the third choice - thread it and you spend nothing at all - so it has to be
        /// flyable and has to be frightening, and 18 units against a 12.32 hull is both at once
        /// (1.46x). It is a CONSTANT across the ladder deliberately: the port narrows around it,
        /// so at intensity 1 threading is the miser's option beside a wide easy door and at
        /// intensity 4 it is very nearly the only gap left. Nothing about the eye had to change to
        /// say that - which is exactly why an eye that quietly grew would still pass every other
        /// test in this file while deleting the choice.
        /// </summary>
        [Test]
        public void TheEyeIsThreadableAndBarelySo()
        {
            float eye = BreakwaterCourseSettings.EyeRadius;
            float hull = BreakwaterCourseSettings.SparrowHullRadius;

            Assert.Greater(eye, hull, "the eye is smaller than the ship - unthreadable at any roll.");
            Assert.LessOrEqual(eye, hull * 2f,
                $"the eye is {eye / hull:F2} hull-radii; 'barely' is under 2, and past it threading " +
                "stops costing nerve and the mode loses its third option.");
        }

        [Test]
        public void TheEyeIsAlwaysTighterThanThePort([Values(1, 2, 3, 4)] int intensity)
        {
            // The plug is clipped to the annulus [EyeRadius, PortRadius]. If the eye ever reached
            // the port there would be no weave to clip and the station would be a bare ring -
            // no rocket, no saw, nothing to choose between.
            Assert.Less(BreakwaterCourseSettings.EyeRadius,
                        BreakwaterCourseSettings.PortRadiusForIntensity(intensity),
                        $"intensity {intensity}'s plug has no annulus left to weave.");
        }

        /// <summary>
        /// <b>The eye is the mode's central promise, so it is measured off the PRISMS rather than
        /// off the constant that names it.</b>
        ///
        /// <para>EyeRadius is an input to the plug's clipping arithmetic, not a statement about
        /// what the plug emits: a bar is <c>BarCross</c> wide, so a rake line clipped on its
        /// CENTRELINE leaves half a cross-section of prism inside the hole. That shipped - the
        /// k = 1 line stands at exactly 18.0, so the "is this line inside the eye" test was false
        /// and six bar bodies straddled 16.5..19.5 at every station, a hexagon of inradius 16.5
        /// against a documented 18. Every constant was individually correct and every other test
        /// in this file passed.</para>
        ///
        /// <para>So this walks the emitted boxes and asks the only question that matters: how big
        /// is the largest disc in the port plane that no prism intrudes on. The shadow of an
        /// oriented box on that plane is a convex polygon, and the answer is the distance from the
        /// station's axis to the nearest one.</para>
        /// </summary>
        [Test]
        public void ThePlugLeavesTheWholeEyeClear([Values(1, 2, 3, 4)] int intensity)
        {
            float port = BreakwaterCourseSettings.PortRadiusForIntensity(intensity);
            float eye = BreakwaterCourseSettings.EyeRadius;
            float hull = BreakwaterCourseSettings.SparrowHullRadius;

            float nearest = float.PositiveInfinity;
            int bars = 0;

            BreakwaterStationBuilder.Build(
                new BreakwaterStation(Vector3.zero, Vector3.forward, port),
                (pos, rot, scale, kind) =>
                {
                    if (kind != PrismKind.Danger) return;   // only the weave can reach the eye
                    bars++;
                    nearest = Mathf.Min(nearest, DistanceFromStationAxis(pos, rot, scale));
                });

            Assert.Greater(bars, 0, $"intensity {intensity} wove no plug at all.");
            Assert.GreaterOrEqual(nearest, eye - 1e-3f,
                $"intensity {intensity}: the nearest plug bar's BODY reaches {nearest:F3} from the " +
                $"axis, inside the {eye} eye the collar advertises. Clip the rake against the " +
                "bar's near edge, not its centreline.");
            Assert.Greater(nearest, hull,
                $"intensity {intensity}: the clear eye ({nearest:F3}) is narrower than the hull " +
                $"({hull}) - unthreadable at any roll.");
        }

        /// <summary>
        /// The collar is the rim a pilot lines up on, and its inner faces must land on the same
        /// eye the weave is cut to. If the two disagree the station shows a mouth it does not
        /// have (or hides one it does), and no amount of aiming skill can tell which.
        /// </summary>
        [Test]
        public void TheCollarsInnerFaceLandsOnTheEye([Values(1, 2, 3, 4)] int intensity)
        {
            float port = BreakwaterCourseSettings.PortRadiusForIntensity(intensity);
            float nearest = float.PositiveInfinity;
            int blocks = 0;

            BreakwaterStationBuilder.Build(
                new BreakwaterStation(Vector3.zero, Vector3.forward, port),
                (pos, rot, scale, kind) =>
                {
                    // The collar is the only Plain cube; the dish's plates are 7 x 7 x 1.5.
                    if (kind != PrismKind.Plain) return;
                    if (!Mathf.Approximately(scale.x, scale.z)) return;
                    blocks++;
                    nearest = Mathf.Min(nearest, DistanceFromStationAxis(pos, rot, scale));
                });

            Assert.AreEqual(12, blocks, $"intensity {intensity} laid {blocks} collar blocks.");
            Assert.AreEqual(BreakwaterCourseSettings.EyeRadius, nearest, 1e-3f,
                $"intensity {intensity}: the collar's inner face sits at {nearest:F3}, not on the " +
                $"{BreakwaterCourseSettings.EyeRadius} eye the weave is cut to.");
        }

        /// <summary>
        /// <b>No station may swallow a spawn pad.</b> Pilots spawn on the equatorial ring at 480,
        /// which is INSIDE the 420..1080 shell the walk uses, and until this was added nothing in
        /// the generator knew the ring existed - so a station's weave landed on a pad and a pilot
        /// started the match embedded in Danger prisms. Measured before the fix: 7 of 14,400
        /// pad-cases inside structure and 23 more within a hull radius of it.
        /// </summary>
        [Test]
        public void NoStationEverReachesASpawnPad([Values(1, 2, 3, 4)] int intensity)
        {
            var settings = Settings(intensity);
            float reject = BreakwaterCourse.StationReach(settings.PortRadius) +
                           settings.SpawnPadClearance;

            Assert.IsNotNull(settings.SpawnPads, "ForIntensity stopped seeding the spawn pads.");
            Assert.Greater(settings.SpawnPads.Length, 0, "the pad ring is empty.");

            float worst = float.PositiveInfinity;

            for (int seed = 1; seed <= Seeds; seed++)
            {
                var course = BreakwaterCourse.Generate(SeedFor(seed, intensity), settings);
                Assert.IsNotNull(course, $"intensity {intensity}, seed {seed}: no course.");

                for (int i = 0; i < course.Count; i++)
                    for (int p = 0; p < settings.SpawnPads.Length; p++)
                        worst = Mathf.Min(worst,
                            (settings.SpawnPads[p] - course[i].Position).magnitude);
            }

            Assert.GreaterOrEqual(worst, reject - 1e-2f,
                $"intensity {intensity}: a station centre came within {worst:F1} of a spawn pad, " +
                $"inside the {reject:F1} its own structure plus the authored clearance needs.");
        }

        /// <summary>
        /// The pad test is opt-in, and a caller that leaves it off still gets a course. That is
        /// what makes every other test in this file a test of the WALK rather than of the pads.
        /// </summary>
        [Test]
        public void ACourseWithNoSpawnPadsStillGenerates([Values(1, 2, 3, 4)] int intensity)
        {
            var settings = Settings(intensity);
            settings.SpawnPads = null;

            var course = BreakwaterCourse.Generate(7919 + intensity, settings);
            Assert.IsNotNull(course);
            Assert.AreEqual(Stations, course.Count);
        }

        /// <summary>
        /// Distance from the station's own axis (the +Z line through the origin, which is how
        /// every station in these tests is posed) to an oriented box.
        ///
        /// <para>The box's shadow on the port plane is the convex hull of its eight projected
        /// corners, so the answer is the distance from the origin to that polygon. Solved rather
        /// than sampled - a grid sample can only ever over-report the clearance, which is the
        /// direction that hides the defect this test exists for.</para>
        /// </summary>
        static float DistanceFromStationAxis(Vector3 centre, Quaternion rot, Vector3 scale)
        {
            var corners = new Vector2[8];
            int n = 0;

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 q = centre + rot * new Vector3(sx * scale.x, sy * scale.y, sz * scale.z) * 0.5f;
                        corners[n++] = new Vector2(q.x, q.y);
                    }

            // The origin is inside the shadow iff it is on the same side of every hull edge; with
            // only eight points, testing every ORDERED pair as a candidate edge is exact and
            // cheaper to read than a hull construction.
            float best = float.PositiveInfinity;
            bool outside = false;

            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                {
                    if (i == j) continue;
                    Vector2 a = corners[i], b = corners[j];
                    Vector2 e = b - a;
                    if (e.sqrMagnitude < 1e-12f) continue;

                    // A supporting line of the shadow: every corner on one side of it.
                    bool supporting = true;
                    for (int k = 0; k < 8 && supporting; k++)
                        supporting = Cross(e, corners[k] - a) <= 1e-6f;
                    if (!supporting) continue;

                    if (Cross(e, -a) > 0f) outside = true;      // origin on the far side

                    float t = Mathf.Clamp01(Vector2.Dot(-a, e) / e.sqrMagnitude);
                    best = Mathf.Min(best, (a + e * t).magnitude);
                }

            return outside ? best : 0f;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
