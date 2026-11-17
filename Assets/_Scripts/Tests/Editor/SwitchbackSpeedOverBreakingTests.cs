using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Speed over breaking in Switchback (Yash, 2026-11-17; MINIGAME_TRAINING_PLAN.md 6.2), headless: the decision
    /// between "fly the ring" and "go and take a crystal to break what blocks it", as pure functions of the layout.
    /// A blocked ring with a crystal ahead takes the crystal; a ring partly opened and passable is flown even with
    /// another crystal nearby; an open ring is never left; a pilot on a pass keeps it; "passable" is measured against
    /// the prisms' real boxes and agrees with what the shipped layouts promise (the bore and the eye at I1-I3, the
    /// sealed gauntlet at I4).
    /// </summary>
    public class SwitchbackSpeedOverBreakingTests
    {
        const float Speed = 120f, Mouth = 40f, Eye = 40f;
        static readonly Vector3 Ring = Vector3.zero, Flow = Vector3.forward;
        static readonly Vector3 Pilot = Ring - Flow * 320f;
        /// <summary>A key on the pilot's line to the ring, and one 70 u beside it (what the pilot would have to turn away for).</summary>
        static readonly Vector3 KeyAhead = Ring - Flow * 200f, KeyBeside = Ring - Flow * 210f + Vector3.right * 70f;
        const float WallZ = -60f, Pitch = 12f, Cube = 10f;
        const int Half = 4;   // a wall is 9 x 9 cubes: 108 u across
        const int Walls = 3;  // three walls, 24 u apart: ramming the line is dear, so the cost model alone wants the crystal
        const int PerWall = (2 * Half + 1) * (2 * Half + 1);

        sealed class World : ISwitchbackAIWorld
        {
            public readonly HashSet<int> Gone = new();
            public readonly HashSet<int> KeysGone = new();
            public bool PrismStanding(int k) => !Gone.Contains(k);
            public bool KeyAvailable(int station) => !KeysGone.Contains(station);
        }

        /// <summary>One blocked gate: its key, and three walls of 10 u cubes across the leg from 60 u before the ring. Prism
        /// index of the cube at wall cell (x, y) is <see cref="At"/>; a blast (or a ram) is the world marking cubes gone.</summary>
        static SwitchbackBlastPlanner Gate(Vector3 key, bool blockerInTheHole = false)
        {
            var layout = new SwitchbackFeatureLayout();
            layout.Stations.Add(new BlastRunStation(0, key, Vector3.Distance(key, Ring), Ring + Flow * 8f, Flow, Eye, false,
                                                    BlastRunWorld.Blocks, 0));
            for (int wall = 0; wall < Walls; wall++)
                for (int y = -Half; y <= Half; y++)
                    for (int x = -Half; x <= Half; x++)
                        layout.Prisms.Add(new SwitchbackFeaturePrism(new Vector3(x * Pitch, y * Pitch, WallZ - 24f * wall),
                                                                     Quaternion.identity, Vector3.one * Cube, PrismKind.Plain));
            if (blockerInTheHole)
                layout.Prisms.Add(new SwitchbackFeaturePrism(new Vector3(0f, 0f, WallZ), Quaternion.identity, Vector3.one * 4f,
                                                             PrismKind.Plain));
            return new SwitchbackBlastPlanner(layout, 2, 0f, Eye, 5 * Pitch, SwitchbackBlastSlot.Default);
        }

        static int At(int x, int y, int wall = 0) => wall * PerWall + (y + Half) * (2 * Half + 1) + (x + Half);

        /// <summary>The hole a blast leaves: every wall's cubes within <paramref name="cells"/> of its centre are gone.</summary>
        static World Blown(int cells)
        {
            var w = new World();
            for (int wall = 0; wall < Walls; wall++)
                for (int y = -cells; y <= cells; y++)
                    for (int x = -cells; x <= cells; x++) w.Gone.Add(At(x, y, wall));
            return w;
        }

        static SwitchbackAIDecision Decide(SwitchbackBlastPlanner planner, World world, int level = 4, uint seed = 5,
                                           SwitchbackAIDecision held = default) =>
            planner.Decide(0, Pilot, Speed, Ring, 0f, SwitchbackAISkill.ForLevel(level), world, seed, in held, Mouth);

        [Test]
        public void ABlockedRing_WithACrystalAhead_TakesTheCrystal()
        {
            var planner = Gate(KeyAhead);
            var world = new World();
            Assert.IsFalse(planner.Passable(0, world, Pilot, Ring, Mouth, 0f, Speed, false, default, out _),
                           "a whole wall across the leg is not passable");
            var d = Decide(planner, world);
            Assert.AreEqual(SwitchbackAIPlan.Blast, d.Plan, $"why={d.Why}");
            Assert.IsFalse(d.Pass);
            Assert.IsTrue(d.KeyOnWay, "a key on the line to the ring is on the way");
        }

        [Test]
        public void ABlockedRing_MayStillTakeACrystalBesideIt()
        {
            // Not passable: the rule does not forbid the crystal, the cost model decides as before.
            var d = Decide(Gate(KeyBeside), new World());
            Assert.IsFalse(d.Pass);
            Assert.IsFalse(d.KeyOnWay);
            Assert.AreNotEqual("passable", d.Why);
            Assert.AreNotEqual("committed", d.Why);
            Assert.AreEqual(SwitchbackAIPlan.Blast, d.Plan, $"why={d.Why} thread={d.CostThread} blast={d.CostBlast}");
        }

        [Test]
        public void ARingPartlyOpened_IsFlown_EvenWithAnotherCrystalNearby([Values(1, 2, 3, 4)] int level)
        {
            // A blast blew ONE cube out of each wall: a 14 u hole, the hull is 6 u wide. Most of the obstacle still stands.
            var planner = Gate(KeyBeside);
            var world = Blown(0);
            Assert.Greater(planner.Standing(0, world), 200, "the obstacle is far from cleared");
            Assert.IsTrue(planner.Passable(0, world, Pilot, Ring, Mouth, 0f, Speed, false, default, out Vector3 through));
            Assert.Less(Vector3.Distance(through, Ring), 0.01f, "the hole is on the centre line");
            for (uint seed = 1; seed <= 12; seed++)
            {
                var d = Decide(planner, world, level, seed);
                Assert.AreEqual(SwitchbackAIPlan.Thread, d.Plan, $"level {level} seed {seed}: left a passable ring for a crystal");
                Assert.AreEqual("passable", d.Why);
                Assert.IsTrue(d.Pass);
                Assert.IsTrue(float.IsInfinity(d.CostBlast), "the crystal is not even costed");
            }
        }

        [Test]
        public void TheRuleIsWhatKeepsItOnTheRing()
        {
            // Control: the same pilot, hole and crystal with the rule off flies for the crystal (the fault Yash saw),
            // and Pass still reports that the ring was flyable, which is what a race counts as a turn-away.
            var planner = Gate(KeyBeside);
            planner.SpeedOverBreaking = false;
            var d = Decide(planner, Blown(0));
            Assert.AreEqual(SwitchbackAIPlan.Blast, d.Plan, $"why={d.Why} thread={d.CostThread} blast={d.CostBlast}");
            Assert.IsTrue(d.Pass && !d.KeyOnWay, "the turn-away is visible to the counter");
        }

        [Test]
        public void APassableRing_StillTakesACrystalThatIsOnItsWay()
        {
            // Flying through a key on the line costs nothing: the rule leaves that choice to the cost model.
            var d = Decide(Gate(KeyAhead), Blown(0));
            Assert.IsTrue(d.Pass);
            Assert.IsTrue(d.KeyOnWay);
            Assert.AreNotEqual("passable", d.Why);
        }

        [Test]
        public void AHoleTooSmallForTheHull_IsNotAPass()
        {
            // The same wall with 5 u cubes on the 12 u pitch leaves 7 u between cubes everywhere: under the hull's 6 u
            // plus its air. With the centre cube gone the hole is 19 u: a pass.
            var layout = new SwitchbackFeatureLayout();
            layout.Stations.Add(new BlastRunStation(0, KeyBeside, 220f, Ring + Flow * 8f, Flow, Eye, false, BlastRunWorld.Blocks, 0));
            for (int y = -Half; y <= Half; y++)
                for (int x = -Half; x <= Half; x++)
                    layout.Prisms.Add(new SwitchbackFeaturePrism(new Vector3(x * Pitch + 6f, y * Pitch + 6f, WallZ), Quaternion.identity,
                                                                 Vector3.one * 5f, PrismKind.Plain));
            var planner = new SwitchbackBlastPlanner(layout, 2, 0f, Eye, 5 * Pitch, SwitchbackBlastSlot.Default);
            var world = new World();
            Assert.IsFalse(planner.Passable(0, world, Pilot, Ring, Mouth, 0f, Speed, false, default, out _),
                           "7 u between cubes is not a gap for a 6 u hull with 2 u of air each side");
            // A Danger prism wants more air than a plain one: the same 14 u hole ringed by Danger cubes is refused.
            var danger = new SwitchbackFeatureLayout();
            danger.Stations.Add(layout.Stations[0]);
            for (int y = -Half; y <= Half; y++)
                for (int x = -Half; x <= Half; x++)
                    danger.Prisms.Add(new SwitchbackFeaturePrism(new Vector3(x * Pitch, y * Pitch, WallZ), Quaternion.identity,
                                                                 Vector3.one * Cube, PrismKind.Danger));
            var dangerPlanner = new SwitchbackBlastPlanner(danger, 2, 0f, Eye, 5 * Pitch, SwitchbackBlastSlot.Default);
            Assert.IsFalse(dangerPlanner.Passable(0, Blown(0), Pilot, Ring, Mouth, 0f, Speed, false, default, out _),
                           "a 14 u hole between Danger prisms leaves 4 u of air, under the 5 u a dead stop is worth");
            Assert.IsTrue(dangerPlanner.Passable(0, Blown(1), Pilot, Ring, Mouth, 0f, Speed, false, default, out _));
        }

        [Test]
        public void AHoleOffTheCentre_IsFlownThroughWhereItIs()
        {
            // The blast opened a 38 u hole and left a 4 u cube standing in its middle: the centre line is blocked, the
            // pass runs beside it, inside the mouth.
            var planner = Gate(KeyBeside, blockerInTheHole: true);
            var world = Blown(1);
            Assert.IsTrue(planner.Passable(0, world, Pilot, Ring, Mouth, 0f, Speed, false, default, out Vector3 through));
            float off = Vector3.Distance(through, Ring);
            Assert.Greater(off, 5f, "the centre is blocked");
            Assert.Less(off, SwitchbackBlastPlanner.PassMouthShare * Mouth, "the pass point is well inside the mouth");
            var d = Decide(planner, world);
            Assert.AreEqual("passable", d.Why);
            Assert.Less(Vector3.Distance(d.PassThrough, through), 0.01f);
            // A narrow ring keeps the pass point inside ITS mouth, whatever the door's eye is.
            Assert.IsFalse(planner.Passable(0, world, Pilot, Ring, 9f, 0f, Speed, false, default, out _),
                           "a 9 u mouth has no room beside the blocker");
        }

        [Test]
        public void APilotOnAPass_KeepsIt_WhenALaterLookFindsTheLineCrossed()
        {
            var planner = Gate(KeyBeside);
            var world = Blown(0);
            var first = Decide(planner, world);
            Assert.AreEqual("passable", first.Why);
            // The hole closes (a mover, a rival's trail read as standing): no longer passable from here.
            world.Gone.Clear();
            Assert.IsFalse(planner.Passable(0, world, Pilot, Ring, Mouth, 0f, Speed, false, default, out _));
            var fresh = Decide(planner, world);
            Assert.AreEqual(SwitchbackAIPlan.Blast, fresh.Plan, "a pilot NOT on a pass may take the crystal");
            var kept = Decide(planner, world, held: first);
            Assert.AreEqual(SwitchbackAIPlan.Thread, kept.Plan, "a pilot that turned for the ring does not turn away again");
            Assert.AreEqual("committed", kept.Why);
            Assert.IsTrue(kept.Pass);
            // The pass belongs to its gate: at another gate the old decision holds nothing.
            var other = first; other.Gate = 1;
            Assert.AreEqual(SwitchbackAIPlan.Blast, Decide(planner, world, held: other).Plan);
        }

        [Test]
        public void AnOpenRing_IsNeverLeftForACrystal()
        {
            var planner = Gate(KeyBeside);
            var world = new World();
            // Gate 1 has no station: open.
            Vector3 ring1 = new(0f, 0f, 600f);
            Assert.IsTrue(planner.Passable(1, world, ring1 - Flow * 300f, ring1, Mouth, 0f, Speed, false, default, out _));
            var d = planner.Decide(1, ring1 - Flow * 300f, Speed, ring1, 0f, SwitchbackAISkill.ForLevel(4), world, 3);
            Assert.AreEqual(SwitchbackAIPlan.Direct, d.Plan);
            Assert.IsTrue(d.Pass);
            // The any-prism sense: with the ring flyable no sensed blocker is worth a crystal, however poor the dodge.
            Assert.IsFalse(SwitchbackPrismAvoidance.BlastsBlocker(corridorClearance: 0f, turnDegrees: 60f, wantedClearance: 10f,
                                                                  ringPassable: true));
            // A blocked ring's obstacle already blasted away is open too.
            var cleared = new World();
            for (int k = 0; k < planner.Layout.Prisms.Count; k++) cleared.Gone.Add(k);
            var c = Decide(planner, cleared);
            Assert.AreEqual(SwitchbackAIPlan.Direct, c.Plan);
            Assert.AreEqual("cleared", c.Why);
            Assert.IsTrue(c.Pass);
        }

        [Test]
        public void ASensedBlocker_IsBlastedOnlyWhenTheHullCannotGetBy()
        {
            float hull = SwitchbackPrismAvoidance.PassClearance;
            // A corridor the hull fits is flown, however much less air than the skill would like and however big the turn.
            Assert.IsFalse(SwitchbackPrismAvoidance.BlastsBlocker(hull + 0.5f, 50f, 10f, ringPassable: false));
            // No way by, ring not flyable: the crystal.
            Assert.IsTrue(SwitchbackPrismAvoidance.BlastsBlocker(hull - 0.5f, 5f, 10f, ringPassable: false));
            // No way by this prism, but the ring is flyable: no crystal.
            Assert.IsFalse(SwitchbackPrismAvoidance.BlastsBlocker(hull - 0.5f, 5f, 10f, ringPassable: true));
            // The rule off is the rule of before 2026-11-17: any poor dodge is a blast.
            Assert.IsTrue(SwitchbackPrismAvoidance.BlastsBlocker(hull + 0.5f, 5f, 10f, ringPassable: true, speedOverBreaking: false));
            Assert.IsTrue(SwitchbackPrismAvoidance.BlastsBlocker(20f, 30f, 10f, ringPassable: true, speedOverBreaking: false));
            Assert.IsFalse(SwitchbackPrismAvoidance.BlastsBlocker(20f, 10f, 10f, ringPassable: false, speedOverBreaking: false));
        }

        [Test]
        public void OnTheWay_IsAheadAndCostsAlmostNothing()
        {
            var planner = Gate(KeyAhead);
            var world = new World();
            Assert.IsTrue(planner.KeyOnTheWay(0, world, Pilot, Ring, Speed), "on the line, ahead");
            Assert.IsFalse(planner.KeyOnTheWay(0, world, KeyAhead + Flow * 30f, Ring, Speed), "behind the pilot");
            world.KeysGone.Add(0);
            Assert.IsFalse(planner.KeyOnTheWay(0, world, Pilot, Ring, Speed), "taken");
            Assert.IsFalse(Gate(KeyBeside).KeyOnTheWay(0, new World(), Pilot, Ring, Speed), "70 u beside the line");
            // The cost is distance and the bend at the key: zero on the line, growing as the key moves off it.
            Assert.AreEqual(0f, SwitchbackBlastPlanner.KeyDetourSeconds(KeyAhead, Pilot, Ring, Speed), 1e-3f);
            float near = SwitchbackBlastPlanner.KeyDetourSeconds(KeyAhead + Vector3.right * 8f, Pilot, Ring, Speed);
            float far = SwitchbackBlastPlanner.KeyDetourSeconds(KeyAhead + Vector3.right * 60f, Pilot, Ring, Speed);
            Assert.LessOrEqual(near, SwitchbackBlastPlanner.OnTheWaySeconds, "a key the hull brushes anyway");
            Assert.Greater(far, SwitchbackBlastPlanner.OnTheWaySeconds);
            Assert.Greater(far, near);
            Assert.IsTrue(float.IsInfinity(SwitchbackBlastPlanner.KeyDetourSeconds(Ring + Flow * 50f, Pilot, Ring, Speed)), "past the ring");
        }

        [Test]
        public void OfTwoCrystalsThatCoverABlocker_TheOneOnTheWayIsTaken()
        {
            var pts = new Vector3[16]; int n = 0;
            for (int y = -2; y <= 2; y++) pts[n++] = new Vector3(0f, y * 4f, 150f);   // a wall across the line
            var slot = SwitchbackBlastSlot.Default;
            var skill = SwitchbackAvoidSkill.ForLevel(4);
            Vector3 blocker = new(0f, 0f, 150f);
            Vector3 onLine = new(0f, 0f, 70f), beside = new(45f, 0f, 70f);
            foreach (var order in new[] { new[] { onLine, beside }, new[] { beside, onLine } })
            {
                Assert.IsTrue(SwitchbackPrismAvoidance.TryBlast(Vector3.zero, Vector3.forward, blocker, 20f, pts, n, order, 2, in slot,
                                                                in skill, out Vector3 c, out _, out _, out _));
                Assert.AreEqual(onLine, c, "the crystal on the pilot's line, whichever was listed first");
            }
            // Control: the one beside it does cover the wall, so it WAS a candidate.
            Assert.IsTrue(SwitchbackPrismAvoidance.TryBlast(Vector3.zero, Vector3.forward, blocker, 20f, pts, n, new[] { beside }, 1,
                                                            in slot, in skill, out _, out _, out _, out _));
        }

        [Test]
        public void SegmentTouchesBox_IsTheHullAgainstThePrismsRealBox()
        {
            Vector3 half = new(5f, 5f, 5f);
            Assert.IsTrue(SwitchbackBlastPlanner.SegmentTouchesBox(new(0, 0, -20), new(0, 0, 20), Vector3.zero, Quaternion.identity, half));
            Assert.IsFalse(SwitchbackBlastPlanner.SegmentTouchesBox(new(6, 0, -20), new(6, 0, 20), Vector3.zero, Quaternion.identity, half));
            Assert.IsFalse(SwitchbackBlastPlanner.SegmentTouchesBox(new(0, 0, -20), new(0, 0, -6), Vector3.zero, Quaternion.identity, half),
                           "the segment ends short of the box");
            // A long thin pillar turned across the line blocks it; turned along a line 6 u away it does not (its
            // bounding sphere would).
            Vector3 pillar = new(3f, 3f, 35f);
            Assert.IsTrue(SwitchbackBlastPlanner.SegmentTouchesBox(new(20, 0, -20), new(20, 0, 20), Vector3.zero,
                                                                   Quaternion.AngleAxis(90f, Vector3.up), pillar));
            Assert.IsFalse(SwitchbackBlastPlanner.SegmentTouchesBox(new(6, 0, -20), new(6, 0, 20), Vector3.zero, Quaternion.identity, pillar));
        }

        // ── the shipped layouts ──

        const float Inner = 480f, Outer = 1080f, ClearRadius = Inner / 1.22f + 30f;

        static (List<RaceGate> course, SwitchbackBlastPlanner planner) Fixed(int intensity)
        {
            var s = SwitchbackCourseSettings.ForIntensity(intensity);
            s.GateCount = 20; s.InnerRadius = Inner; s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up; s.FirstGateDistance = 620f;
            var course = SwitchbackCourse.Generate(SwitchbackBlastRun.FixedSeed(intensity), s);
            var layout = SwitchbackBlastRun.Layout(course, intensity, Vector3.zero, ClearRadius);
            return (course, new SwitchbackBlastPlanner(layout, course.Count, intensity));
        }

        /// <summary>I1-I3 lay a bore down every blocked leg and leave the door's eye open, so a pilot on the leg can always
        /// fly its ring: "passable", measured against the prisms' boxes, must agree with what the layout promises.</summary>
        [Test]
        public void BelowTheGauntlet_TheBoreAndTheEyeAreAPass([Values(1, 2, 3)] int intensity)
        {
            var (course, planner) = Fixed(intensity);
            var world = new World();
            int stations = 0, passable = 0;
            foreach (var st in planner.Layout.Stations)
            {
                if (st.Gate < 1) continue;
                Vector3 ring = course[st.Gate].Position, leg = (ring - course[st.Gate - 1].Position).normalized;
                stations++;
                // On the leg line, short of the key: the bore runs from here to the ring.
                if (planner.Passable(st.Gate, world, st.Key - leg * 40f, ring, course[st.Gate].Radius, 0f, Speed, false, default, out _))
                    passable++;
            }
            Assert.Greater(stations, 0);
            Assert.GreaterOrEqual(passable, 0.9f * stations, $"I{intensity}: {passable} of {stations} bores read as a pass");
        }

        /// <summary>The I4 gauntlet seals every eye and fills the leg: nothing is passable until it is broken, and a
        /// broken gate is.</summary>
        [Test]
        public void TheGauntlet_IsNotAPassUntilItIsBroken()
        {
            var (course, planner) = Fixed(4);
            var standing = new World();
            var gone = new World();
            for (int k = 0; k < planner.Layout.Prisms.Count; k++) gone.Gone.Add(k);
            int gates = 0;
            foreach (var st in planner.Layout.Stations)
            {
                if (st.Stage != 0 || st.Gate < 1) continue;
                gates++;
                Vector3 ring = course[st.Gate].Position, leg = (ring - course[st.Gate - 1].Position).normalized;
                Vector3 self = ring - leg * 400f;
                Assert.IsFalse(planner.Passable(st.Gate, standing, self, ring, course[st.Gate].Radius, 0f, Speed, false, default, out _),
                               $"gate {st.Gate}: a sealed, filled leg read as a pass");
                if (Vector3.Dot(leg, st.FlowAxis) >= SwitchbackBlastPlanner.PassMinAxisCos + 0.05f)
                    Assert.IsTrue(planner.Passable(st.Gate, gone, self, ring, course[st.Gate].Radius, 0f, Speed, false, default, out _),
                                  $"gate {st.Gate}: an emptied leg did not read as a pass");
            }
            Assert.Greater(gates, 0);
        }

        [Test]
        public void Passable_IsCheap_AndDeterministic()
        {
            // The same question twice is the same answer (it runs at the replan rate, from any pilot), and a planner
            // asked from every station of the densest course returns the same pass point each time.
            var (course, planner) = Fixed(4);
            var world = new World();
            for (int k = 0; k < planner.Layout.Prisms.Count; k += 3) world.Gone.Add(k);
            foreach (var st in planner.Layout.Stations)
            {
                Vector3 ring = course[st.Gate].Position, self = st.Key - (st.Target - st.Key).normalized * 80f;
                bool a = planner.Passable(st.Gate, world, self, ring, course[st.Gate].Radius, 1.3f, Speed, false, default, out Vector3 ta);
                bool b = planner.Passable(st.Gate, world, self, ring, course[st.Gate].Radius, 1.3f, Speed, false, default, out Vector3 tb);
                Assert.AreEqual(a, b);
                Assert.AreEqual(ta, tb);
            }
        }
    }
}
