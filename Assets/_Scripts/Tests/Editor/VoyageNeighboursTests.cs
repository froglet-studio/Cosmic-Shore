#if UNITY_EDITOR
using System.Linq;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Events next door and the detour vote's AI ballots (Frogma Rev 134 WorldEvents and Vote B): the leg's plan is seeded,
    /// one event a cell at most, never beside the voyage's own stop, each with its verb; a keen pilot asks and votes to
    /// detour. Pure rules, so the headless voyage harness runs them too.
    /// </summary>
    public class VoyageNeighboursTests
    {
        [Test]
        public void TheLegsEventsAreSeededAndNeverBesideItsStop()
        {
            var a = VoyageNeighbours.Plan(42, 6, 4, 1f, 60f);
            var b = VoyageNeighbours.Plan(42, 6, 4, 1f, 60f);
            Assert.AreEqual(5, a.Count, "every cell but the last, at chance 1");
            CollectionAssert.AreEqual(a.Select(n => (n.Kind, n.Cell, n.Bearing, n.SecondsLeft)),
                b.Select(n => (n.Kind, n.Cell, n.Bearing, n.SecondsLeft)), "every machine plans the same leg");
            Assert.IsTrue(a.All(n => n.Cell >= 1 && n.Cell < 6));
            Assert.IsTrue(a.All(n => n.Bearing is 90f or -90f));
            Assert.IsTrue(a.All(n => n.SecondsLeft >= 45f && n.SecondsLeft <= 75f));
            Assert.IsTrue(a.All(n => n.Verb == VoyageNeighbours.VerbOf(n.Kind)));
            Assert.IsEmpty(VoyageNeighbours.Plan(42, 1, 4, 1f, 60f), "a one-cell voyage has only its stop");
            Assert.IsEmpty(VoyageNeighbours.Plan(42, 6, 4, 0f, 60f));
        }

        [Test]
        public void RougherWaterLeavesMoreWrecks()
        {
            int calm = 0, rough = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                calm += VoyageNeighbours.Plan(seed, 6, 1, 1f, 60f).Count(n => n.Kind == VoyageNeighbourKind.Wreck);
                rough += VoyageNeighbours.Plan(seed, 6, 9, 1f, 60f).Count(n => n.Kind == VoyageNeighbourKind.Wreck);
            }
            Assert.Greater(rough, calm);
        }

        [Test]
        public void AKeenPilotAsksAndVotesToDetour()
        {
            Assert.AreEqual(VoyageVote.Detour, VoyageNeighbours.AiBallot(PilotMood.Keen));
            Assert.AreEqual(VoyageVote.PressOn, VoyageNeighbours.AiBallot(PilotMood.Steady));
            Assert.AreEqual(VoyageVote.PressOn, VoyageNeighbours.AiBallot(PilotMood.Reluctant));
            Assert.AreEqual(2, VoyageNeighbours.Asker(new[] { PilotMood.Steady, PilotMood.None, PilotMood.Keen }, 0));
            Assert.AreEqual(1, VoyageNeighbours.Asker(new[] { PilotMood.Steady, PilotMood.Steady, PilotMood.Steady }, 1));

            // Alone with a keen wingmate: no press in time still counts the AI's detour.
            var vote = new VoyageVote(3, new[] { true, false, false });
            vote.CastAi(1, VoyageNeighbours.AiBallot(PilotMood.Keen));
            vote.Tick(VoyageVote.DefaultSeconds);
            Assert.AreEqual(VoyageVote.Detour, vote.Outcome);
        }
    }
}
#endif
