#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="SkimRaceCoopShare"/> - which ring a Skim Race pilot flies when it shares its domain:
    /// its own, one per spawn point, never its teammate's (Yash, 2026-11-17: two AI of one domain each
    /// tried to take both rings at a spawn point and turned back for the second). The rule is a pure
    /// function of facts every machine holds identically, because the two pilots may run on different
    /// machines; Docs/SKIM_RACE_AI.md section 13.
    /// </summary>
    public class SkimRaceCoopShareTests
    {
        const Domains Team = Domains.Jade;

        static SkimRaceTargetTracker.Candidate Ring(int id, Vector3 at, Domains domain = Team, bool exploding = false,
            bool embedded = false) =>
            new() { Alive = true, Domain = domain, Position = at, Id = id, Exploding = exploding, Embedded = embedded };

        static Vector3 V(float x, float z = 0f) => new Vector3(x, 0f, z);

        static int Pick(List<SkimRaceTargetTracker.Candidate> rings, Vector3 pilot, int rank, int pilots, int current = -1) =>
            SkimRaceCoopShare.SelectIndex(rings, Team, pilot, current, rank, pilots);

        [Test]
        public void TwoPilots_TwoRingsAtOneSpawnPoint_EachFliesADifferentOne()
        {
            // Both rings stand at one spawn point; both pilots are nearer ring 0, so the nearest rule
            // sends both at it.
            var rings = new List<SkimRaceTargetTracker.Candidate> { Ring(1, V(200f, -20f)), Ring(2, V(200f, 30f)) };
            Vector3 a = V(0f, -5f), b = V(10f, -5f);
            Assert.AreEqual(0, SkimRaceTargetTracker.SelectIndex(rings, Team, a, -1));
            Assert.AreEqual(0, SkimRaceTargetTracker.SelectIndex(rings, Team, b, -1), "the nearest rule: both chase ring 0");

            Assert.AreEqual(0, Pick(rings, a, rank: 0, pilots: 2));
            Assert.AreEqual(1, Pick(rings, b, rank: 1, pilots: 2));
        }

        [Test]
        public void TheSplit_DoesNotDependOnWhereThePilotsAre()
        {
            // The two pilots may run on different machines, which see each other's hull a few units
            // off. Whatever each machine believes about positions, the split is the same.
            var rings = new List<SkimRaceTargetTracker.Candidate> { Ring(7, V(200f, -20f)), Ring(4, V(200f, 30f)) };
            foreach (var at in new[] { V(0f), V(500f, 80f), V(200f, 29f), V(-900f, -900f) })
            {
                Assert.AreEqual(1, Pick(rings, at, rank: 0, pilots: 2), "rank 0 flies the lower crystal id (4)");
                Assert.AreEqual(0, Pick(rings, at, rank: 1, pilots: 2), "rank 1 flies the higher crystal id (7)");
            }
        }

        [Test]
        public void AfterTakingItsRing_ThePilotFliesToTheNextSpawnPoint_NotBackForTheOther()
        {
            // The pilot has just taken ring 1, which reappeared at the next spawn point 400 u on. Its
            // teammate's ring 2 still stands 15 u beside it.
            var rings = new List<SkimRaceTargetTracker.Candidate> { Ring(1, V(400f)), Ring(2, V(0f, 15f)) };
            Vector3 collector = V(0f);

            Assert.AreEqual(1, SkimRaceTargetTracker.SelectIndex(rings, Team, collector, -1),
                "the nearest rule turns back for the teammate's ring");
            Assert.AreEqual(0, Pick(rings, collector, rank: 0, pilots: 2), "the share: on to the next spawn point");
            Assert.AreEqual(1, Pick(rings, collector, rank: 1, pilots: 2), "and the teammate still has its own to take");
        }

        [Test]
        public void ARingTakenThisFrame_IsNotReplacedByTheTeammatesRing()
        {
            // The pilot's ring is mid-collection and still sits on it (it moves next frame). No target
            // is the right answer for that frame - the teammate's ring is never a substitute.
            var rings = new List<SkimRaceTargetTracker.Candidate>
                { Ring(1, V(2f), exploding: true), Ring(2, V(0f, 15f)) };
            Assert.AreEqual(-1, Pick(rings, V(0f), rank: 0, pilots: 2));
            Assert.AreEqual(1, Pick(rings, V(0f), rank: 1, pilots: 2), "the ranks do not shift while a ring is being taken");
        }

        [Test]
        public void APilotAloneOnItsDomain_IsUnchanged()
        {
            var rings = new List<SkimRaceTargetTracker.Candidate>
            {
                Ring(3, V(300f)), Ring(1, V(50f)), Ring(2, V(-46f)), Ring(9, V(10f), Domains.Ruby),
                Ring(5, V(5f), embedded: true), Ring(6, V(1f), exploding: true),
            };
            var pilots = new[] { V(0f), V(120f, 40f), V(-300f), V(49f) };
            foreach (var at in pilots)
                for (int current = -1; current < rings.Count; current++)
                    Assert.AreEqual(SkimRaceTargetTracker.SelectIndex(rings, Team, at, current),
                        Pick(rings, at, rank: 0, pilots: 1, current), $"pilot {at}, current {current}");

            // ...and so is a domain of two pilots with only one ring to fly: nothing to share.
            var one = new List<SkimRaceTargetTracker.Candidate> { Ring(1, V(50f)), Ring(2, V(10f), Domains.Ruby) };
            Assert.AreEqual(0, Pick(one, V(0f), rank: 0, pilots: 2));
            Assert.AreEqual(0, Pick(one, V(0f), rank: 1, pilots: 2));
        }

        [Test]
        public void OtherDomainsRings_AndEmbeddedCrystals_DoNotCount()
        {
            var rings = new List<SkimRaceTargetTracker.Candidate>
            {
                Ring(1, V(5f), Domains.Ruby), Ring(2, V(6f), embedded: true),
                Ring(3, V(200f)), Ring(4, V(210f)),
            };
            Assert.AreEqual(2, Pick(rings, V(0f), rank: 0, pilots: 2));
            Assert.AreEqual(3, Pick(rings, V(0f), rank: 1, pilots: 2));
        }

        [Test]
        public void UnevenCounts_NoRingIsNobodys_AndNoPilotIsIdle()
        {
            // Three rings, two pilots (a teammate left; its ring stayed): the spare is dealt to rank 0,
            // which flies the nearer of its two and keeps it on a near-tie.
            var three = new List<SkimRaceTargetTracker.Candidate> { Ring(1, V(100f)), Ring(2, V(50f)), Ring(3, V(-95f)) };
            Assert.AreEqual(2, Pick(three, V(0f), rank: 0, pilots: 2), "rank 0 has rings 1 and 3; ring 3 is nearer");
            Assert.AreEqual(0, Pick(three, V(0f), rank: 0, pilots: 2, current: 0), "a near-tie keeps the ring being flown");
            Assert.AreEqual(1, Pick(three, V(0f), rank: 1, pilots: 2));
            for (int ring = 0; ring < 3; ring++)
                Assert.IsTrue(SkimRaceCoopShare.IsMine(ring, 3, 0, 2) ^ SkimRaceCoopShare.IsMine(ring, 3, 1, 2),
                    $"ring {ring} has exactly one pilot");

            // Two rings, three pilots: every pilot still has a ring.
            for (int pilot = 0; pilot < 3; pilot++)
                Assert.IsTrue(SkimRaceCoopShare.IsMine(0, 2, pilot, 3) ^ SkimRaceCoopShare.IsMine(1, 2, pilot, 3),
                    $"pilot {pilot} has exactly one ring");
        }

        [Test]
        public void PilotsAreRanked_ByNetworkId_ThenName_ThenRosterPosition()
        {
            Assert.IsTrue(SkimRaceCoopShare.RanksBefore(4, "Tiger", 1, 9, "Lion", 0), "network id decides, whatever the name");
            Assert.IsFalse(SkimRaceCoopShare.RanksBefore(9, "Lion", 0, 4, "Tiger", 1));
            Assert.IsTrue(SkimRaceCoopShare.RanksBefore(0, "Lion", 1, 0, "Tiger", 0), "no network id: the name");
            Assert.IsTrue(SkimRaceCoopShare.RanksBefore(0, "Lion", 0, 0, "Lion", 1), "the same name: the roster position");
            Assert.IsFalse(SkimRaceCoopShare.RanksBefore(0, "Lion", 1, 0, "Lion", 1), "a pilot does not rank before itself");
        }

        // Skim Race's intensity-2 anchors are 186-646 u apart with a 35 u jitter; these are that shape.
        static readonly Vector3[] Anchors =
        {
            V(0f), V(300f), V(520f, 200f), V(900f, 260f), V(1100f, -40f), V(1500f, -90f), V(1700f, 250f), V(2100f, 300f),
        };

        /// <summary>
        /// Flies a two-pilot team round the anchors with a target rule and counts the times a pilot's
        /// next target stood at a spawn point it had already scored at - a turn back for the second
        /// ring. Each ring walks the anchors on its own, as <c>CrystalManager.CalculateNewSpawnPos</c>
        /// moves it. Both pilots fly at one speed: the next ring taken is the one a pilot reaches first.
        /// </summary>
        static int TurnBacks(System.Func<List<SkimRaceTargetTracker.Candidate>, Vector3, int, int> rule, out int[] taken)
        {
            var ringAnchor = new[] { 0, 0 };
            var rings = new List<SkimRaceTargetTracker.Candidate>
                { Ring(1, Anchors[0] + V(0f, -20f)), Ring(2, Anchors[0] + V(0f, 25f)) };
            var pilots = new[] { V(-200f, -10f), V(-215f, 10f) };
            var flown = new float[2];
            var lastScoredAt = new[] { -1, -1 };
            taken = new int[2];
            int turnBacks = 0;

            for (int step = 0; step < 2 * (Anchors.Length - 1); step++)
            {
                int mover = -1, target = -1;
                float arrives = float.MaxValue;
                for (int p = 0; p < 2; p++)
                {
                    int t = rule(rings, pilots[p], p);
                    if (t < 0) continue;
                    if (ringAnchor[t] <= lastScoredAt[p]) turnBacks++;
                    float at = flown[p] + (rings[t].Position - pilots[p]).magnitude;
                    if (at < arrives) { arrives = at; mover = p; target = t; }
                }
                if (mover < 0) break;

                flown[mover] = arrives;
                pilots[mover] = rings[target].Position;
                lastScoredAt[mover] = ringAnchor[target];
                taken[mover]++;
                if (ringAnchor[target] + 1 >= Anchors.Length) break;
                ringAnchor[target]++;
                var moved = rings[target];
                moved.Position = Anchors[ringAnchor[target]] + V(0f, target == 0 ? -20f : 25f);
                rings[target] = moved;
            }
            return turnBacks;
        }

        [Test]
        public void OverARace_NoPilotGoesBackToASpawnPointItScoredAt()
        {
            int shared = TurnBacks((rings, at, rank) => Pick(rings, at, rank, pilots: 2), out var sharedTaken);
            Assert.AreEqual(0, shared, "one ring per spawn point: every target is at a spawn point further on");
            Assert.AreEqual(sharedTaken[0], sharedTaken[1], 1f, "the two pilots share the work");

            // The control: the nearest rule, which is what a pilot flew when its teammate ran on another
            // machine. It goes back for the second ring at spawn point after spawn point.
            int nearest = TurnBacks((rings, at, _) => SkimRaceTargetTracker.SelectIndex(rings, Team, at, -1), out _);
            Assert.Greater(nearest, 3, "the nearest rule turns back");
        }
    }
}
#endif
