#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A broken trail (Garrett, 2026-10-28: "when the goby's trail is destroyed the shrimp should have to clear the destroyed
    /// section. if they fall they should not hit phantom prisms. they should fall into a fun animation that brings them back
    /// to a den, but without the mass creation."): where a ground block was destroyed the Pistol's ground is a HOLE
    /// (<see cref="PistolGround"/>), either way it rides; the ride flies over a hole it is high and fast enough for and lands
    /// beyond it; a Pistol that sinks into one has fallen in (<c>Events.FellIn</c>) and lands on nothing after (no phantom
    /// ground); the fall restores the gap's blocks it got past and sets it down beyond the gap (Garrett, 2026-10-29), so every
    /// pass shrinks the gap till one crosses it; and the AI floats or dives to land on ground. Pure (no asset, no scene), so <c>Tools/Build/goby_pistol_harness</c> runs it
    /// without the editor; the ride's numbers are the shipped <c>GobyPistolConfig.asset</c>'s.
    /// </summary>
    [TestFixture]
    public class GobyPistolTrailHoleTests
    {
        /// <summary>The shipped ride (GobyPistolConfig.asset, 2026-10-28).</summary>
        static PistolBodyMath.Settings Shipped => new()
        {
            pGravity = 19f, pDiveMul = 7.5f, pMinSpeed = 6f, pDrag = 0.05f, pGoodLand = 30f, pMinAir = 0.15f, pAirGravity = 1f,
            pSmoothBoost = 1.05f, pHardKeep = 1f, pMaxSpeed = 90f, pLaunchRange = 35f, pLaunchSpeed = 30f, pLaunchCharges = 1f,
            pLaunchCd = 9f, pGhostTime = 2.5f, pGhostCd = 8f, pVortexCd = 4f, pEdgeGrace = 1.2f,
        };

        static readonly PistolPlane.HillSpec Hills = new(10f, 15f, 3.6f, 7.2f, 9f, 0.08f, 1.5f);
        const float Gap = 6f;

        static PistolPlane.Frame Island(float length, int dir, PistolGround ground, bool flat = false, uint seed = 1u)
            => PistolPlane.For(new PlaneSegment(Vector3.zero, Vector3.forward * length, Vector3.up), dir, 0f,
                flat ? PistolPlane.HillTrack.Flat(length) : PistolPlane.HillTrack.Build(length, seed, Hills), ground);

        /// <summary>Destroys the blocks laid from <paramref name="from"/> to <paramref name="to"/> along the line.</summary>
        static void Break(PistolGround ground, float from, float to)
        {
            for (int i = ground.SlotAt(from); i <= ground.SlotAt(to); i++) ground.SetGone(i, true);
        }

        struct Outcome { public bool Arrived, Fell; public float FellAt, FallenTo, Seconds; public int LandingsAfterFalling; public List<Vector2> Flights; }

        /// <summary>Rides <paramref name="f"/> from the perch at <paramref name="speed"/> (0: the floor speed; null rider: the
        /// Pistol AI's Tiny Wings dive). A flight is (takeoff x, landing x).</summary>
        static Outcome Ride(PistolPlane.Frame f, Func<PistolBodyMath.State, float> rider = null, float speed = 0f)
        {
            var s = Shipped;
            var p = new PistolBodyMath.State();
            PistolBodyMath.PlaceAtStart(p, f, Mathf.Max(s.pMinSpeed, speed));
            var o = new Outcome { Flights = new List<Vector2>() };
            const float dt = 1f / 60f;
            float takeoff = 0f;
            while (o.Seconds < 240f)
            {
                float dive = rider != null ? rider(p) : PistolAI.TinyWingsDive(p, f, s);
                bool wasFalling = p.falling, wasGrounded = p.grounded;
                var ev = PistolBodyMath.Step(p, f, o.Seconds, dt, new PistolBodyMath.Input { Dive = dive }, s);
                o.Seconds += dt;
                if (wasGrounded && !p.grounded) takeoff = p.x;
                if (ev.Landed) { o.Flights.Add(new Vector2(takeoff, p.x)); if (wasFalling) o.LandingsAfterFalling++; }
                if (ev.FellIn) { o.Fell = true; o.FellAt = o.FallenTo = p.x; return FallOn(o, p, f, s); }
                if (!p.falling && p.x >= f.EndX - 1f) { o.Arrived = true; return o; }
            }
            return o;
        }

        /// <summary>Keeps a fallen Pistol falling for two seconds (the seat would set it down beyond the gap at once): it must
        /// land on nothing, however far it carries.</summary>
        static Outcome FallOn(Outcome o, PistolBodyMath.State p, PistolPlane.Frame f, PistolBodyMath.Settings s)
        {
            for (float t = 0f; t < 2f; t += 1f / 60f)
            {
                if (PistolBodyMath.Step(p, f, o.Seconds + t, 1f / 60f, new PistolBodyMath.Input(), s).Landed) o.LandingsAfterFalling++;
                o.FallenTo = Mathf.Max(o.FallenTo, p.x);
            }
            return o;
        }

        [Test]
        public void Ground_ADestroyedBlockIsAHole_EitherWay()
        {
            var ground = new PistolGround(300f, Gap);
            var ahead = Island(300f, 1, ground, flat: true);
            var back = Island(300f, -1, ground, flat: true);
            Assert.IsTrue(PistolPlane.SolidAt(ahead, 150f) && PistolPlane.SolidAt(back, 150f), "every block stands: all ground");
            ground.SetGone(ground.SlotAt(150f), true);
            Assert.IsTrue(ground.AnyGone);
            Assert.IsFalse(PistolPlane.SolidAt(ahead, 151f), "the destroyed block's slot is a hole riding ahead");
            Assert.IsFalse(PistolPlane.SolidAt(back, 300f - 151f), "and riding back (x runs from the far perch)");
            Assert.IsTrue(PistolPlane.SolidAt(ahead, 140f) && PistolPlane.SolidAt(ahead, 160f), "its neighbours stand");
            Assert.IsTrue(PistolPlane.SolidAt(back, 300f - 140f));
            ground.SetGone(0, true);
            Assert.IsFalse(PistolPlane.SolidAt(ahead, 3f), "the first block gone is a hole just off the perch");
            Assert.IsTrue(PistolPlane.SolidAt(ahead, 0f) && PistolPlane.SolidAt(ahead, 300f), "the perches themselves always stand");
            ground.SetGone(ground.SlotAt(150f), false);
            Assert.IsTrue(PistolPlane.SolidAt(ahead, 151f), "a restored block is ground again");
        }

        [Test]
        public void Ride_FallsThroughAHole_AndLandsOnNothingAfter()
        {
            // a slow Pistol (never diving, at its floor speed) on level ground rolls off the hole's edge and falls in
            var whole = Ride(Island(300f, 1, new PistolGround(300f, Gap), flat: true), p => 0f);
            Assert.IsTrue(whole.Arrived && !whole.Fell, "the control: the whole trail carries it to the perch");

            var ground = new PistolGround(300f, Gap);
            Break(ground, 120f, 150f);
            var broken = Ride(Island(300f, 1, ground, flat: true), p => 0f);
            Assert.IsTrue(broken.Fell && !broken.Arrived, "it falls through the destroyed section");
            Assert.IsTrue(broken.FellAt >= 114f && broken.FellAt <= 156f, $"in the hole, not before or past it (fell at {broken.FellAt:F1} u)");
            Assert.AreEqual(0, broken.LandingsAfterFalling, "it lands on nothing on the way down: no phantom ground");

            // a shorter hole at 20 u/s: it sinks well under the line (past the edge's grace) before it reaches the far side,
            // and passes under that lip instead of landing on it
            var lip = new PistolGround(300f, Gap);
            Break(lip, 120f, 126f);
            var under = Ride(Island(300f, 1, lip, flat: true), p => 0f, speed: 20f);
            Assert.IsTrue(under.Fell && !under.Arrived, "a Pistol that fell in is not caught by the ground past the hole");
            Assert.AreEqual(0, under.LandingsAfterFalling, "no landing under the far lip");
            Assert.IsTrue(under.FallenTo > 132f, $"it carried on under the far lip (to {under.FallenTo:F1} u) without landing on it");
            // the control: the same hole at 60 u/s is cleared (it sinks less than the grace across it)
            Assert.IsTrue(Ride(Island(300f, 1, lip, flat: true), p => 0f, speed: 60f).Arrived, "fast enough, it skims the hole");
        }

        [Test]
        public void Ride_ClearsAHoleItFliesOver_AndAGluedRiderFallsIn()
        {
            var whole = Ride(Island(900f, 1, new PistolGround(900f, Gap)));
            Assert.IsTrue(whole.Arrived, "the control: the Tiny Wings rider crosses the whole island");
            var longest = new Vector2(0f, 0f);
            foreach (var flight in whole.Flights) if (flight.y - flight.x > longest.y - longest.x) longest = flight;
            Assert.Greater(longest.y - longest.x, 8f * Gap, "it flies over many blocks at once");

            // break the ground under the middle of its longest flight: it must fly over it, and does
            float mid = 0.5f * (longest.x + longest.y);
            var ground = new PistolGround(900f, Gap);
            Break(ground, mid - 1.5f * Gap, mid + 1.5f * Gap);
            var cleared = Ride(Island(900f, 1, ground));
            Assert.IsTrue(cleared.Arrived && !cleared.Fell, $"it clears the destroyed section at {mid:F0} u and rides on to the perch");

            // the same hole under a rider who never lets go (glued to the ground, it hardly flies) is a fall
            var glued = Ride(Island(900f, 1, ground), p => 1f);
            Assert.IsTrue(glued.Fell, "a rider who never flies has to fall in: the hole is real");
        }

        [Test]
        public void Ai_InTheAir_PicksTheLandingOnGround()
        {
            // in the air over level ground, 10 u up at 30 u/s: diving lands about 11 u on and steep, floating about 31 u on
            // and shallow, so on whole ground the AI floats (the smoother landing)
            var s = Shipped;
            PistolBodyMath.State Air() => new() { x = 0f, y = 10f, vx = 30f, vy = 0f, grounded = false };
            var whole = Island(300f, 1, new PistolGround(300f, Gap), flat: true);
            Assert.AreEqual(0f, PistolAI.TinyWingsDive(Air(), whole, s), "the control: on whole ground it floats to the smoother landing");

            var floatHole = new PistolGround(300f, Gap);
            Break(floatHole, 24f, 36f);
            Assert.AreEqual(1f, PistolAI.TinyWingsDive(Air(), Island(300f, 1, floatHole, flat: true), s),
                "with a hole where the float would land, it dives onto the ground before it");

            var diveHole = new PistolGround(300f, Gap);
            Break(diveHole, 6f, 18f);
            Assert.AreEqual(0f, PistolAI.TinyWingsDive(Air(), Island(300f, 1, diveHole, flat: true), s),
                "with a hole where the dive would land, it floats over it");
        }

        [Test]
        public void Ground_AFallRestoresWhatItGotPast_AndLandsBeyondTheGap()
        {
            // a gap from 120 to 156 u (blocks at 123 ... 153); the Pistol sinks into it at 133 u
            var g = new PistolGround(300f, Gap);
            Break(g, 120f, 155f);
            var passed = new List<int>();
            float land = g.AfterGap(133f, 1, passed);
            Assert.AreEqual(3, passed.Count, "riding ahead it touched the blocks at 123 and 129, and fell into 135's");
            Assert.AreEqual(20, passed[0]); Assert.AreEqual(22, passed[2]);
            Assert.AreEqual(157.5f, land, 1e-3f, "and comes out a quarter block into the standing ground beyond the gap");
            passed.Clear();
            land = g.AfterGap(133f, -1, passed);
            Assert.AreEqual(4, passed.Count, "riding back it touched 153, 147 and 141, and fell into 135's");
            Assert.AreEqual(25, passed[0]);
            Assert.AreEqual(118.5f, land, 1e-3f, "and comes out on the ground before the gap, the way it rides");
            passed.Clear();
            Break(g, 160f, 299f);
            Assert.AreEqual(300f, g.AfterGap(133f, 1, passed), 1e-3f, "a gap that runs to the perch sets it at the perch");
            passed.Clear();
            g.AfterGap(121f, 1, passed);
            Assert.AreEqual(1, passed.Count, "sinking into the gap's first block still restores that one: every fall makes progress");
            passed.Clear();
            var whole = new PistolGround(300f, Gap);
            Assert.AreEqual(127.5f, whole.AfterGap(127f, 1, passed), 1e-3f, "the control: no gap, it stays on its own block");
            Assert.AreEqual(0, passed.Count, "and restores nothing");
        }

        [Test]
        public void Ride_EveryPassShrinksTheGap_TillItIsCrossed()
        {
            // Garrett, 2026-10-29: "they restore some trail to try again on the next pass". A 300 u gap is far more than the
            // Tiny Wings rider flies at once; as the seat does, a jump restores the gap's blocks it flies over, and a pass that
            // falls in restores what it got past, so the next gets further
            float length = 1200f;
            var g = new PistolGround(length, Gap);
            Break(g, 400f, 700f);
            var f = Island(length, 1, g);
            var s = Shipped;
            int gone = Gone(g), passes = 0;
            bool crossed = false;
            var passed = new List<int>();
            for (; passes < 12 && !crossed; passes++)
            {
                var p = new PistolBodyMath.State();
                PistolBodyMath.PlaceAtStart(p, f, s.pMinSpeed);
                bool fell = false;
                for (float t = 0f; t < 240f && p.x < f.EndX - 1f && !fell; t += 1f / 60f)
                {
                    var ev = PistolBodyMath.Step(p, f, t, 1f / 60f, new PistolBodyMath.Input { Dive = PistolAI.TinyWingsDive(p, f, s) }, s);
                    fell = ev.FellIn;
                    if (!ev.Flew) continue;
                    passed.Clear();
                    g.BlocksBetween(PistolPlane.LineAt(f, ev.FlewFrom), PistolPlane.LineAt(f, ev.FlewTo), passed);
                    foreach (int slot in passed) g.SetGone(slot, false);   // the seat's RewardBlock: a jump heals what it flies over
                }
                if (!fell) { crossed = true; break; }
                passed.Clear();
                float land = g.AfterGap(PistolPlane.LineAt(f, p.x), 1, passed);
                foreach (int slot in passed) g.SetGone(slot, false);
                Assert.Greater(land, PistolPlane.LineAt(f, p.x), "it comes out beyond where it fell");
                PistolBodyMath.PlaceOnGround(p, f, land, Mathf.Max(s.pMinSpeed, p.vx));
                Assert.IsFalse(p.falling, "set down on the ground");
                Assert.IsTrue(PistolPlane.SolidAt(f, p.x), "standing ground");
                int now = Gone(g);
                Assert.Less(now, gone, $"pass {passes + 1} restored some of the gap ({gone} -> {now} blocks)");
                gone = now;
            }
            Assert.IsTrue(crossed, $"after {passes} passes the gap is crossed ({gone} blocks of it left)");
            Assert.Greater(passes, 1, "the control: the gap could not be crossed in one or two passes");
        }

        static int Gone(PistolGround g) { int n = 0; for (int i = 0; i < g.Count; i++) if (g.Gone(i)) n++; return n; }
    }
}
#endif
