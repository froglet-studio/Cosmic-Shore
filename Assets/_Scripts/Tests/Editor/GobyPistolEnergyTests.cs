#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Pistol's energy (Garrett, 2026-10-29: "every time prisms are not ridden because the pistol is off trail then grow
    /// those prisms ... this reward the player for making big jumps. the more prisms are skipped the more energy the pistol
    /// should collect. the amount the pistol builds when reaching the den is based on energy collected. energy is lost when
    /// falling through a hole"), and the half planes the path map shows ("one side of the trail when traveling one way and
    /// ... the other side when traveling the other direction"). The ride reports the stretch it flew (<c>Events.Flew</c>);
    /// the island names the blocks under it once each; a big jump earns more than as many small ones; a den is built from
    /// energy, so the Tiny Wings rider builds a den near the old distance rule's while a rider who never flies builds none;
    /// and each half of an island is scored on its own side. Pure, so <c>Tools/Build/goby_pistol_harness</c> runs it.
    /// </summary>
    [TestFixture]
    public class GobyPistolEnergyTests
    {
        /// <summary>The shipped ride and energy (GobyPistolConfig.asset, 2026-10-29).</summary>
        static PistolBodyMath.Settings Shipped => new()
        {
            pGravity = 19f, pDiveMul = 7.5f, pMinSpeed = 6f, pDrag = 0.05f, pGoodLand = 30f, pMinAir = 0.15f, pAirGravity = 1f,
            pSmoothBoost = 1.05f, pHardKeep = 1f, pMaxSpeed = 90f, pLaunchRange = 35f, pLaunchSpeed = 30f, pLaunchCharges = 1f,
            pLaunchCd = 9f, pGhostTime = 2.5f, pGhostCd = 8f, pVortexCd = 4f, pEdgeGrace = 1.2f, pFallDepth = 14f,
        };

        const float Gap = 6f, PerBlock = 1f, FlightBonus = 0.2f, PerDenPrism = 8f;
        const int DenMax = 80;
        static readonly PistolPlane.HillSpec Hills = new(10f, 15f, 3.6f, 7.2f, 9f, 0.08f, 1.5f);

        struct Ride { public float Energy, Seconds; public int Skipped, Blocks, Longest; public bool Twice; }

        /// <summary>Rides a <paramref name="length"/> u island as the seat does: every flown stretch's blocks give energy, the
        /// nth of a flight more. <paramref name="rider"/> null: the Pistol AI's Tiny Wings dive.</summary>
        static Ride RideIt(float length, uint seed, Func<PistolBodyMath.State, float, float, float> rider = null)
        {
            var s = Shipped;
            var ground = new PistolGround(length, Gap);
            var f = PistolPlane.For(new PlaneSegment(Vector3.zero, Vector3.forward * length, Vector3.up), 1, 0f,
                PistolPlane.HillTrack.Build(length, seed, Hills), ground);
            var p = new PistolBodyMath.State();
            PistolBodyMath.PlaceAtStart(p, f, s.pMinSpeed);
            var r = new Ride { Blocks = ground.Count };
            var seen = new HashSet<int>();
            var crossed = new List<int>();
            int nth = 0;
            const float dt = 1f / 60f;
            while (r.Seconds < 240f && p.x < f.EndX - 1f)
            {
                float play = PistolAI.TinyWingsDive(p, f, s), dive = rider != null ? rider(p, play, r.Seconds) : play;
                var ev = PistolBodyMath.Step(p, f, r.Seconds, dt, new PistolBodyMath.Input { Dive = dive }, s);
                r.Seconds += dt;
                if (ev.Flew)
                {
                    crossed.Clear();
                    ground.BlocksBetween(PistolPlane.LineAt(f, ev.FlewFrom), PistolPlane.LineAt(f, ev.FlewTo), crossed);
                    foreach (int slot in crossed)
                    {
                        if (!seen.Add(slot)) r.Twice = true;
                        r.Energy += GobyDen.EnergyFor(nth++, PerBlock, FlightBonus);
                        r.Skipped++;
                    }
                }
                if (p.grounded) { r.Longest = Mathf.Max(r.Longest, nth); nth = 0; }
            }
            return r;
        }

        static float Sloppy(PistolBodyMath.State p, float play, float t) => t % 1f < 0.3f ? 1f - play : play;

        [Test]
        public void Ground_NamesTheBlocksUnderAStretch_OnceEach()
        {
            var g = new PistolGround(30f, Gap);   // blocks at 3, 9, 15, 21, 27
            var into = new List<int>();
            g.BlocksBetween(0f, 30f, into);
            Assert.AreEqual(5, into.Count);
            for (int i = 0; i < 5; i++) Assert.AreEqual(i, into[i], "in the order the stretch passes them");
            into.Clear();
            g.BlocksBetween(30f, 0f, into);
            Assert.AreEqual(4, into[0], "riding back, the far block first");
            into.Clear();
            g.BlocksBetween(0f, 9f, into); g.BlocksBetween(9f, 15f, into);
            Assert.AreEqual(3, into.Count, "two stretches meeting on a block count it once");
            into.Clear();
            g.BlocksBetween(4f, 8.9f, into);
            Assert.AreEqual(0, into.Count, "a stretch between two blocks passes none");
        }

        [Test]
        public void Ride_ReportsTheStretchesItFlew()
        {
            var play = RideIt(900f, 1u);
            Assert.IsFalse(play.Twice, "riding one way, no block is skipped twice");
            Assert.Greater(play.Skipped, play.Blocks / 2, $"the Tiny Wings rider flies over most of the island ({play.Skipped} of {play.Blocks})");
            Assert.Greater(play.Longest, 10, "in flights of many blocks");
            var never = RideIt(900f, 1u, (p, d, t) => 0f);
            Assert.Less(never.Skipped, never.Blocks / 30, $"the control: a rider who never dives rides nearly every block ({never.Skipped})");
        }

        [Test]
        public void Energy_BigJumpsPayMore()
        {
            static float Flight(int n) { float e = 0f; for (int i = 0; i < n; i++) e += GobyDen.EnergyFor(i, PerBlock, FlightBonus); return e; }
            Assert.AreEqual(19f, Flight(10), 1e-3f, "ten blocks in one flight: 10 + 0.2 x 45");
            Assert.Greater(Flight(20), 2f * Flight(10), "one flight over 20 blocks earns more than two over 10");
            Assert.Greater(2f * Flight(10), 20f * Flight(1), "and two over 10 more than twenty hops");
            Assert.AreEqual(58f, Flight(20), 1e-3f, "twenty blocks in one flight: 20 + 0.2 x 190");
        }

        [Test]
        public void Den_IsBuiltFromEnergy_SoBigJumpsBuildBigDens()
        {
            foreach (float length in new[] { 900f, 1800f })
            {
                float play = 0f, sloppy = 0f;
                for (uint seed = 1; seed <= 3; seed++) { play += RideIt(length, seed).Energy; sloppy += RideIt(length, seed, Sloppy).Energy; }
                play /= 3f; sloppy /= 3f;
                int den = GobyDen.PrismsFor(play, PerDenPrism, 1f, 0, DenMax), old = Mathf.FloorToInt(length / 40f);
                Assert.IsTrue(den >= old * 0.7f && den <= old * 1.5f,
                    $"a {length} u island ridden well builds {den} prisms, near the distance rule's {old}");
                Assert.Less(GobyDen.PrismsFor(sloppy, PerDenPrism, 1f, 0, DenMax), den / 3, "a sloppy rider builds a fraction of that");
                Assert.AreEqual(0, GobyDen.PrismsFor(RideIt(length, 1u, (p, d, t) => 0f).Energy, PerDenPrism, 1f, 0, DenMax),
                    "a rider who never flies builds none");
            }
            // what the den could not take is kept: a nearly full den spends only what it builds
            int built = GobyDen.PrismsFor(400f, PerDenPrism, 1f, 70, DenMax);
            Assert.AreEqual(10, built);
            Assert.AreEqual(80f, GobyDen.EnergySpent(built, PerDenPrism, 1f), 1e-3f, "ten prisms cost 80 of the 400; 320 are kept");
            Assert.AreEqual(32f, GobyDen.EnergySpent(10, PerDenPrism, 2.5f), 1e-3f, "Mass makes each prism cheaper");
            Assert.AreEqual(0, GobyDen.PrismsFor(0f, PerDenPrism, 2.5f, 0, DenMax), "no energy (a fall took it), no den");
        }

        [Test]
        public void Ride_AFallenPistolFliesOverNothing()
        {
            // a slow rider rolls into a hole: once fallen, no stretch is reported (it skipped nothing; the fall costs the energy)
            var s = Shipped;
            var ground = new PistolGround(300f, Gap);
            for (int i = ground.SlotAt(120f); i <= ground.SlotAt(150f); i++) ground.SetGone(i, true);
            var f = PistolPlane.For(new PlaneSegment(Vector3.zero, Vector3.forward * 300f, Vector3.up), 1, 0f, PistolPlane.HillTrack.Flat(300f), ground);
            var p = new PistolBodyMath.State();
            PistolBodyMath.PlaceAtStart(p, f, s.pMinSpeed);
            bool fell = false, flewFallen = false;
            for (float t = 0f; t < 30f && !fell; t += 1f / 60f)
            {
                var ev = PistolBodyMath.Step(p, f, t, 1f / 60f, new PistolBodyMath.Input(), s);
                if (p.falling && ev.Flew) flewFallen = true;
                fell = ev.Fell;
            }
            Assert.IsTrue(fell, "it falls in");
            Assert.IsFalse(flewFallen, "and flies over nothing on the way down");
        }

        [Test]
        public void Paths_EachHalfIsScoredOnItsOwnSide()
        {
            var seg = new PlaneSegment(Vector3.zero, Vector3.forward * 300f, Vector3.up);
            PistolPlane.Frame ahead = PistolPlane.For(seg, 1, 0f, null), back = PistolPlane.For(seg, -1, 0f, null);
            Assert.AreEqual(-1f, Vector3.Dot(ahead.M, back.M), 1e-4f, "riding back turns the plane's +z round: the other side");
            Vector3 far = new Vector3(0f, 2f, 150f) + ahead.M * 20f, near = new Vector3(0f, 2f, 150f) - ahead.M * 20f, inPlane = new(0f, 2f, 150f);
            bool InHalf(PistolPlane.Frame f, Vector3 w) => PistolPaths.InHalf(f.ToFrame(w), f.EndX, 0f, 40f, 60f);
            Assert.IsTrue(InHalf(ahead, far) && !InHalf(back, far), "mass on one side is the ahead half's only");
            Assert.IsTrue(InHalf(back, near) && !InHalf(ahead, near), "mass on the other side is the back half's only");
            Assert.IsTrue(InHalf(ahead, inPlane) && InHalf(back, inPlane), "mass pulled into the plane is in both");
            Assert.AreEqual(2, PistolPaths.Half(new PathChoice(1, 1)));
            Assert.AreEqual(3, PistolPaths.Half(new PathChoice(2, -1)), "island 1 ridden back from perch 2");
        }
    }
}
#endif
