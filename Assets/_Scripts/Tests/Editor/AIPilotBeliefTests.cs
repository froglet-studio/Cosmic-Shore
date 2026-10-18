#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="AIPilotBelief"/> - the lobby AI difficulty on the platform autopilot (the Arena's Astro
    /// League, Brood Rush and Broadside). It only edits what the pilot BELIEVES about its target, so these
    /// hold the belief itself: no handicap sees the truth, a new target is noticed late, a moving target is
    /// flown to where it was last seen, and a misjudged target sits off the line of sight until the pilot
    /// reaches or passes it.
    /// </summary>
    public class AIPilotBeliefTests
    {
        static readonly Vector3 Target = new(0f, 0f, 500f);

        [Test]
        public void NoHandicap_SeesTheTruth()
        {
            var b = new AIPilotBelief(default, 1);
            Assert.AreEqual(Target, b.Believe(Target, Vector3.zero, Vector3.forward, 0f));
            Vector3 moved = Target + Vector3.right * 300f;
            Assert.AreEqual(moved, b.Believe(moved, Vector3.zero, Vector3.forward, 0.01f));
        }

        [Test]
        public void TheFirstTarget_IsSeenAtOnce()
        {
            var b = new AIPilotBelief(new SkimRaceHandicapLevel(5f, 0f), 7);
            Assert.AreEqual(Target, b.Believe(Target, Vector3.zero, Vector3.forward, 0f));
        }

        [Test]
        public void ANewTarget_IsNoticedLate_ThenSeen()
        {
            var b = new AIPilotBelief(new SkimRaceHandicapLevel(1f, 0f), 7);
            b.Believe(Target, Vector3.zero, Vector3.forward, 0f);

            Vector3 next = Target + Vector3.right * (AIPilotBelief.NewTargetDistance * 3f);
            Assert.AreEqual(Target, b.Believe(next, Vector3.zero, Vector3.forward, 0.01f), "still flying for the old target");
            Assert.IsTrue(b.Unnoticed);

            // the next look comes within ReactionSpreadMax x the level's seconds
            Assert.AreEqual(next, b.Believe(next, Vector3.zero, Vector3.forward, SkimRaceHandicap.ReactionSpreadMax + 0.01f));
            Assert.IsFalse(b.Unnoticed);
        }

        [Test]
        public void AMovingTarget_IsFlownToWhereItWasLastSeen()
        {
            var b = new AIPilotBelief(new SkimRaceHandicapLevel(1f, 0f), 7);
            b.Believe(Target, Vector3.zero, Vector3.forward, 0f);
            Vector3 drifted = Target + Vector3.right * 5f;      // a small move is the same target
            Assert.AreEqual(Target, b.Believe(drifted, Vector3.zero, Vector3.forward, 0.01f));
            Assert.IsFalse(b.Unnoticed, "a target that only moves is not a new one");
        }

        [Test]
        public void Misjudged_IsOffTheLineOfSight_UntilPassed()
        {
            var b = new AIPilotBelief(new SkimRaceHandicapLevel(0f, 1f), 3);
            Vector3 believed = b.Believe(Target, Vector3.zero, Vector3.forward, 0f);
            Assert.IsTrue(b.Misjudging);
            Assert.AreEqual(1, b.Mistakes);
            Vector3 off = believed - Target;
            Assert.AreEqual(AIPilotBelief.MissDistance, off.magnitude, 1e-3f);
            Assert.AreEqual(0f, Vector3.Dot(off, Vector3.forward), 1e-3f, "to one side, not short or long");

            // fly past the believed point: now the pilot sees the real target
            Vector3 past = believed + Vector3.forward * 50f;
            Assert.AreEqual(Target, b.Believe(Target, past, Vector3.forward, 1f));
            Assert.IsFalse(b.Misjudging);
        }

        [Test]
        public void Misjudged_NeverOutlastsTheCap()
        {
            var b = new AIPilotBelief(new SkimRaceHandicapLevel(0f, 1f), 3);
            b.Believe(Target, Vector3.zero, Vector3.forward, 0f);
            Assert.IsTrue(b.Misjudging);
            // never reached, never passed (the pilot sits still): the cap alone ends it
            Assert.AreEqual(Target, b.Believe(Target, Vector3.zero, Vector3.forward, SkimRaceHandicap.MistakeMaxSeconds + 0.1f));
            Assert.IsFalse(b.Misjudging);
        }

        [Test]
        public void Reset_ForgetsTheCount()
        {
            var b = new AIPilotBelief(new SkimRaceHandicapLevel(0f, 1f), 3);
            b.Believe(Target, Vector3.zero, Vector3.forward, 0f);
            b.Reset();
            Assert.AreEqual(0, b.Mistakes);
            Assert.IsFalse(b.Misjudging);
        }

        [Test]
        public void SideOffset_IsPerpendicularToAnySight()
        {
            foreach (var sight in new[] { Vector3.forward, Vector3.right, Vector3.up, new Vector3(1f, 2f, 3f) })
                for (float a = 0f; a < 6.3f; a += 0.7f)
                {
                    Vector3 o = AIPilotBelief.SideOffset(sight, a, 10f);
                    Assert.AreEqual(10f, o.magnitude, 1e-3f);
                    Assert.AreEqual(0f, Vector3.Dot(o, sight.normalized), 1e-3f);
                }
        }
    }
}
#endif
