#if UNITY_EDITOR
using System.Linq;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Hold the Core (Docs/Voyages/MILESTONE_2.md §6; the Voyage Codex): what a hold asks at each difficulty, the hold clock
    /// that runs only while the core is yours, the turned core that is lost, the waves, and the named voyages on the board.
    /// Pure rules, so the headless voyage harness runs them too.
    /// </summary>
    public class VoyageCoreHoldTests
    {
        public const string HoldType = "job.defence.aid";

        [Test]
        public void TheHoldGrowsWithDifficultyAsTheCodexSays()
        {
            var one = VoyageCoreHoldPlan.At(null, 1);
            Assert.AreEqual(45f, one.HoldSeconds);
            Assert.AreEqual(1, one.Waves);
            Assert.AreEqual(0, one.RetakeHulls, "no rival at 1-2");
            var four = VoyageCoreHoldPlan.At(null, 4);
            Assert.AreEqual(60f, four.HoldSeconds);
            Assert.AreEqual(2, four.Waves);
            Assert.Greater(four.RetakeHulls, 0, "a crew retakes in wave 2 from 3");
            var eight = VoyageCoreHoldPlan.At(null, 8);
            Assert.AreEqual(90f, eight.HoldSeconds);
            Assert.AreEqual(3, eight.Waves);
            Assert.AreEqual(3, eight.RetakeHulls, "strength 3 at 7-9");
            Assert.AreEqual(45f, VoyageCoreHoldPlan.At(null, 0).HoldSeconds, "below 1 is 1");
            Assert.AreEqual(90f, VoyageCoreHoldPlan.At(null, 12).HoldSeconds, "above 9 is 9");
            var d = VoyageCoreHoldPlan.Defaults();
            Assert.AreEqual(VoyageDifficulty.Max, d.Length, "one row a difficulty");
            for (int i = 1; i < d.Length; i++) Assert.GreaterOrEqual(d[i].HoldSeconds, d[i - 1].HoldSeconds);
        }

        [Test]
        public void TheClockRunsOnlyWhileTheCoreIsYours()
        {
            var hold = new VoyageCoreHold(VoyageCoreHoldPlan.At(null, 4));
            for (int i = 0; i < 100; i++) hold.Tick(0.5f, 10f, 30f);
            Assert.AreEqual(0f, hold.Held, "theirs from the anchor: the clock waits");
            Assert.AreEqual(VoyageCoreHold.Outcome.Holding, hold.State, "a core never yours is not lost by turning");
            Assert.AreEqual(0.25f, hold.Share, 1e-4f);
            for (int i = 0; i < 20; i++) hold.Tick(0.5f, 40f, 30f);
            Assert.AreEqual(10f, hold.Held, 1e-3f);
            Assert.IsTrue(hold.Yours);
            hold.Tick(1f, 0f, 0f);
            Assert.AreEqual(10f, hold.Held, 1e-3f, "an empty nucleus is nobody's: the clock waits");
            Assert.AreEqual(0f, hold.Turned, "and nobody turns it");
        }

        [Test]
        public void ACoreThatTurnsAndStaysTheirsIsLost()
        {
            var hold = new VoyageCoreHold(VoyageCoreHoldPlan.At(null, 4));
            hold.Tick(5f, 50f, 10f);
            for (int i = 0; i < 14; i++) hold.Tick(1f, 10f, 50f);
            Assert.AreEqual(VoyageCoreHold.Outcome.Holding, hold.State);
            Assert.AreEqual(1f, hold.TurnedLeft, 1e-3f);
            hold.Tick(0.5f, 50f, 10f);
            Assert.AreEqual(0f, hold.Turned, "taking it back resets the countdown");
            for (int i = 0; i < 15; i++) hold.Tick(1f, 10f, 50f);
            Assert.AreEqual(VoyageCoreHold.Outcome.Turned, hold.State);
            Assert.AreEqual(0, hold.Tick(1f, 50f, 0f), "a decided hold does not tick");
            Assert.AreEqual(VoyageCoreHold.Outcome.Turned, hold.State);
        }

        [Test]
        public void WavesComeAsTheClockRunsAndTheHoldIsWon()
        {
            var hold = new VoyageCoreHold(VoyageCoreHoldPlan.At(null, 8));
            Assert.AreEqual(1, hold.Tick(0.1f, 0f, 10f), "wave 1 at the anchor");
            var waves = Enumerable.Range(0, 1000).Select(_ => hold.Tick(0.1f, 20f, 10f)).Where(w => w > 0).ToList();
            CollectionAssert.AreEqual(new[] { 2, 3 }, waves, "wave 2 at a third, wave 3 at two thirds");
            Assert.AreEqual(VoyageCoreHold.Outcome.Held, hold.State);
            Assert.AreEqual(90f, hold.Held, 1e-3f);
            Assert.AreEqual(3, VoyageCoreHold.WaveAt(hold.Plan, 90f), "never past the last wave");
        }

        [Test]
        public void HoldTheCoreVoyagesSailFromTheBoard()
        {
            var c = VoyageDepthHarness.LoadShippedContent();
            Assert.IsTrue(c.VoyageType(HoldType) is { OnBoard: true }, "Hold the Core is built");
            Assert.AreEqual(3, c.Voyages.Count(v => v.Type == HoldType));
            var s = VoyageRules.NewGalaxy(c, Domains.Ruby);
            var jobs = VoyageRules.Board(s, c, c.Crew.Select(p => p.PilotId).Take(3).ToArray());
            var held = jobs.Where(j => j.TemplateId == HoldType && j.IsNamed).ToList();
            Assert.AreEqual(1, held.Count, "one Hold the Core voyage a port, by its name");
            var job = held[0];
            var v = c.Voyage(job.VoyageId);
            Assert.AreEqual(v.Name, job.Title);
            Assert.AreEqual(v.Difficulty, job.Difficulty);
            Assert.AreEqual(WorkVerb.Hold, job.Verb);
            VoyageSea.ContestLast(job);
            Assert.IsTrue(job.HostileCells[job.Cells - 1], "the core is contested");
        }
    }
}
#endif
