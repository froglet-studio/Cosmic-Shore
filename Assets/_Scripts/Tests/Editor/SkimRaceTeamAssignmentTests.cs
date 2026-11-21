#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="SkimRaceTeamAssignment"/> - how the AI pilots on one Skim Race team share its crystals
    /// out (every AI a different crystal, the least total distance, the last plan kept on a near-tie).
    /// The game's per-frame team plan (<see cref="SkimRaceTeamPlan"/>) and the offline simulator both
    /// run this code; what it is worth in race time is Docs/SKIM_RACE_AI.md section 13.
    /// </summary>
    public class SkimRaceTeamAssignmentTests
    {
        const float Keep = SkimRaceTargetTracker.Hysteresis;

        static int[] Plan(Vector3[] pilots, Vector3[] crystals, int[] previous, out float total)
        {
            var result = new int[Mathf.Max(1, pilots.Length)];
            total = SkimRaceTeamAssignment.Assign(pilots, crystals, previous, result, Keep);
            System.Array.Resize(ref result, pilots.Length);
            return result;
        }

        static int[] Plan(Vector3[] pilots, Vector3[] crystals, int[] previous = null) =>
            Plan(pilots, crystals, previous, out _);

        static Vector3 V(float x, float z = 0f) => new Vector3(x, 0f, z);

        // A between the two crystals' approach, B nearer the first: the nearest rule sends BOTH to c0.
        static readonly Vector3[] TwoAI = { V(0f), V(10f) };
        static readonly Vector3[] TwoCrystals = { V(100f), V(0f, 100f) };

        [Test]
        public void TwoAI_TwoCrystals_EachFliesADifferentOne()
        {
            var candidates = new List<SkimRaceTargetTracker.Candidate>();
            foreach (var c in TwoCrystals)
                candidates.Add(new SkimRaceTargetTracker.Candidate { Alive = true, Domain = Domains.Jade, Position = c });
            Assert.AreEqual(0, SkimRaceTargetTracker.SelectIndex(candidates, Domains.Jade, TwoAI[0], -1),
                "the nearest rule: A's nearest is c0 (a tie goes to the first)");
            Assert.AreEqual(0, SkimRaceTargetTracker.SelectIndex(candidates, Domains.Jade, TwoAI[1], -1),
                "...and B's nearest is c0 too - both AI chase one crystal");

            var plan = Plan(TwoAI, TwoCrystals, null, out float total);
            CollectionAssert.AreEqual(new[] { 1, 0 }, plan, "A takes c1 (100) and B its nearer c0 (90)");
            Assert.AreEqual(190f, total, 1e-3f);
        }

        [Test]
        public void TheNearestPairIsNotAlwaysTheBestPlan()
        {
            // A sits between the crystals (10 and 11 away); B is beside c0 (12) and far from c1 (33).
            // "Nearest pair first" gives A c0 and sends B the long way: 10 + 33 = 43. The plan: 11 + 12.
            var plan = Plan(new[] { V(0f), V(22f) }, new[] { V(10f), V(-11f) }, null, out float total);
            CollectionAssert.AreEqual(new[] { 1, 0 }, plan);
            Assert.AreEqual(23f, total, 1e-3f);
        }

        [Test]
        public void TheLastPlanIsKept_UntilAnotherIsClearlyCheaper()
        {
            // Swapping saves 190 vs 200.5 - under 15%: the team keeps what it was flying.
            var kept = Plan(TwoAI, TwoCrystals, new[] { 0, 1 }, out float keptTotal);
            CollectionAssert.AreEqual(new[] { 0, 1 }, kept, "a near-tie does not flip the team's aim");
            Assert.AreEqual(100f + Mathf.Sqrt(100f + 10000f), keptTotal, 1e-3f);

            // Each AI beside its own crystal but flying the other one's (201 vs 20): switch.
            var switched = Plan(new[] { V(0f), V(100f) }, new[] { V(0f, 10f), V(100f, 10f) }, new[] { 1, 0 });
            CollectionAssert.AreEqual(new[] { 0, 1 }, switched);
        }

        [Test]
        public void ALastPlanThatCannotBeKept_IsReplaced()
        {
            CollectionAssert.AreEqual(new[] { 1, 0 }, Plan(TwoAI, TwoCrystals, new[] { 0, 0 }), "one crystal twice");
            CollectionAssert.AreEqual(new[] { 1, 0 }, Plan(TwoAI, TwoCrystals, new[] { -1, 1 }), "an AI idle while a crystal is free");
            CollectionAssert.AreEqual(new[] { 1, 0 }, Plan(TwoAI, TwoCrystals, new[] { 5, 1 }), "a crystal that is gone");
            CollectionAssert.AreEqual(new[] { 1, 0 }, Plan(TwoAI, TwoCrystals, new[] { 1 }), "a plan for fewer AI");
        }

        [Test]
        public void MoreAIThanCrystals_TheBestPlacedTakeThem_TheRestGetNone()
        {
            var plan = Plan(new[] { V(0f), V(50f), V(1000f) }, new[] { V(10f), V(60f) });
            CollectionAssert.AreEqual(new[] { 0, 1, -1 }, plan);
        }

        [Test]
        public void OneAI_FliesTheNearestCrystal_LikeTheLoneAIRule()
        {
            var crystals = new[] { V(300f), V(50f), V(-80f) };
            var candidates = new List<SkimRaceTargetTracker.Candidate>();
            foreach (var c in crystals)
                candidates.Add(new SkimRaceTargetTracker.Candidate { Alive = true, Domain = Domains.Jade, Position = c });
            CollectionAssert.AreEqual(new[] { 1 }, Plan(new[] { V(0f) }, crystals));
            Assert.AreEqual(1, SkimRaceTargetTracker.SelectIndex(candidates, Domains.Jade, V(0f), -1));
        }

        [Test]
        public void ABigTeam_IsSharedOutGreedily_AndEveryAIStillGetsItsOwnCrystal()
        {
            int n = SkimRaceTeamAssignment.MaxExact + 2;
            var pilots = new Vector3[n];
            var crystals = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                pilots[i] = V(i * 100f);
                crystals[(i * 3) % n] = V(i * 100f + 5f);   // shuffled: crystal (3i mod n) sits beside AI i
            }
            var plan = Plan(pilots, crystals, null, out float total);
            var seen = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual((i * 3) % n, plan[i], $"AI {i} takes the crystal beside it");
                Assert.IsTrue(seen.Add(plan[i]));
            }
            Assert.AreEqual(5f * n, total, 1e-3f);
        }

        [Test]
        public void NoAIOrNoCrystal_AssignsNothing()
        {
            Assert.AreEqual(0f, SkimRaceTeamAssignment.Assign(new Vector3[0], TwoCrystals, null, new int[1], Keep));
            CollectionAssert.AreEqual(new[] { -1, -1 }, Plan(TwoAI, new Vector3[0]));
        }
    }
}
#endif
