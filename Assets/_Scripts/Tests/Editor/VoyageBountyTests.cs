#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Bounty (Docs/Voyages/MILESTONE_2.md §7; the Voyage Codex; Frogma Rev 137's rules): what a Bounty asks at each
    /// difficulty, the runaway crew that routs only when all three are silenced at once, the rival Ark that is walled by
    /// her escorts and breaks at her line, the crew that makes port when the clock runs out, the Rival Breaker's shares,
    /// and the named voyages on the board. Pure rules, so the headless voyage harness runs them too.
    /// </summary>
    public class VoyageBountyTests
    {
        public const string BountyType = "job.bounty.crew";

        static readonly VoyageBountyPlan Plan = new() { EscapeSeconds = 60f, Hold = 20, BreakAt = 0.5f, OpenAt = 2 };

        [Test]
        public void TheBountyHardensWithDifficulty()
        {
            var one = VoyageBountyPlan.At(null, 1);
            var nine = VoyageBountyPlan.At(null, 9);
            Assert.Greater(one.EscapeSeconds, nine.EscapeSeconds, "a harder crew makes port sooner");
            Assert.Greater(nine.Hold, one.Hold, "and carries more");
            Assert.Less(nine.BreakAt, one.BreakAt, "and her hull holds longer");
            Assert.AreEqual(2, VoyageBountyPlan.At(null, 4).OpenAt);
            Assert.AreEqual(3, VoyageBountyPlan.At(null, 7).OpenAt, "from 7 every escort must be silenced to open her");
            Assert.AreEqual(VoyageBountyPlan.At(null, 9).EscapeSeconds, VoyageBountyPlan.At(null, 40).EscapeSeconds, "clamped");
        }

        [Test]
        public void ARunawayCrewRoutsOnlyWhenAllThreeAreSilencedAtOnce()
        {
            var fight = new VoyageBountyFight(Plan, sailsArk: false);
            Assert.IsFalse(fight.Tick(1f, new[] { 3f, 3f, 0f }, 1f), "two of three is not a rout");
            Assert.IsFalse(fight.Tick(1f, new[] { 0f, 3f, 3f }, 1f), "two again, a different two: still not at once");
            Assert.IsFalse(fight.Open, "a runaway crew has no hull to open");
            Assert.IsTrue(fight.Tick(1f, new[] { 1f, 2f, 3f }, 1f));
            Assert.AreEqual(VoyageBountyFight.Outcome.Routed, fight.State);
            Assert.IsTrue(fight.Beaten);
            Assert.IsFalse(fight.Tick(1f, new[] { 0f, 0f, 0f }, 1f), "decided once");
        }

        [Test]
        public void ARivalArkIsWalledUntilHerEscortsAreSilencedAndBreaksAtHerLine()
        {
            var fight = new VoyageBountyFight(Plan, sailsArk: true);
            fight.Tick(1f, new[] { 0f, 0f, 0f }, 1f);
            Assert.IsFalse(fight.Open, "her escorts wall her");
            fight.Tick(1f, new[] { 4f, 0f, 0f }, 1f);
            Assert.IsFalse(fight.Open, "one silenced is not enough");
            fight.Tick(1f, new[] { 4f, 2f, 0f }, 0.8f);
            Assert.IsTrue(fight.Open, "two silenced escorts cannot wall: her hull is open");
            Assert.IsFalse(fight.Tick(1f, new[] { 4f, 2f, 3f }, 0.8f), "all three silenced does not rout an Ark crew");
            Assert.IsTrue(fight.Tick(1f, new[] { 0f, 0f, 0f }, 0.5f), "her hull at the line breaks her");
            Assert.AreEqual(VoyageBountyFight.Outcome.Broken, fight.State);
            Assert.IsFalse(fight.Open, "a decided fight is no longer open");
        }

        [Test]
        public void OpeningNeedsNoMoreEscortsThanSheHas()
        {
            var hard = new VoyageBountyPlan { EscapeSeconds = 60f, Hold = 1, BreakAt = 0.4f, OpenAt = 3 };
            Assert.AreEqual(3, VoyageBountyFight.OpenNeeded(hard, 3));
            Assert.AreEqual(2, VoyageBountyFight.OpenNeeded(hard, 2));
            Assert.AreEqual(1, VoyageBountyFight.OpenNeeded(hard, 0));
        }

        [Test]
        public void TheCrewMakesPortWhenTheClockRunsOut()
        {
            var fight = new VoyageBountyFight(Plan, sailsArk: true);
            Assert.IsFalse(fight.Tick(59f, new[] { 0f, 0f }, 1f));
            Assert.AreEqual(1f, fight.Left, 1e-4f);
            Assert.IsTrue(fight.Tick(1f, new[] { 0f, 0f }, 1f));
            Assert.AreEqual(VoyageBountyFight.Outcome.Escaped, fight.State);
            Assert.IsFalse(fight.Beaten, "the warrant goes unserved");
        }

        [Test]
        public void ThePlateShowsEachEscortAndTheEnd()
        {
            var crew = new RivalCrew { Id = "rival.x", Name = "X", ArkName = "Undertow", Faction = FactionType.Pirate };
            var hulls = new List<VesselClassType> { VesselClassType.Serpent, VesselClassType.Sparrow };
            var silence = new List<float> { 3f, 0f };
            var fight = new VoyageBountyFight(Plan, crew.SailsArk);
            var fix = new VoyageRivalFix();
            fight.Tick(1f, silence, 0.9f);
            fight.Fill(fix, crew, hulls, silence);
            Assert.AreEqual(VoyageRivalKind.Ark, fix.Kind);
            Assert.AreEqual("Undertow", fix.ArkName);
            Assert.AreEqual(Plan.Hold, fix.Hold);
            Assert.AreEqual(0.9f, fix.Hull, 1e-4f);
            Assert.AreEqual(VoyageEscortState.Silenced, fix.Escorts[0].State);
            Assert.AreEqual(VoyageEscortState.Guarding, fix.Escorts[1].State);
            Assert.AreEqual(3f, fix.OpenSeconds, 1e-4f);

            fight.Tick(1f, silence, 0.4f);
            fight.Fill(fix, crew, hulls, silence);
            Assert.AreEqual(VoyageRivalAct.Broken, fix.Act);
            Assert.AreEqual(0, fix.Hold, "the hold burst");
            Assert.IsTrue(fix.Escorts.All(e => e.State == VoyageEscortState.TurningBack));
        }

        [Test]
        public void RivalBreakerSharesTheBlows()
        {
            var blows = new VoyageRivalBlows();
            blows.Add("A", 3);
            blows.Add("B");
            blows.Add(null);
            Assert.AreEqual(0.75f, blows.Share("A"), 1e-4f);
            Assert.AreEqual(0.25f, blows.Share("B"), 1e-4f);
            Assert.AreEqual(0f, blows.Share("C"));
            blows.Clear();
            Assert.AreEqual(0f, blows.Share("A"));
        }

        [Test]
        public void EachBountyHuntsANamedCrewArksInTheCrossingRunawaysInTheDrift()
        {
            var c = VoyageDepthHarness.LoadShippedContent();
            var bounties = c.Voyages.Where(v => v.Type == BountyType).ToList();
            Assert.AreEqual(3, bounties.Count);
            foreach (var v in bounties)
                Assert.IsNotNull(c.Rival(v.Rival), $"{v.Name} hunts a crew the content holds");
            // Rev 137: rival Arks sail The Crossing (Black Tide Rising, The Grin Runs Last); runaways Carmine Drift.
            Assert.IsFalse(c.Rival(c.Voyage("voyage.bounty.spear").Rival).SailsArk, "the Quiet Spear runs with no Ark");
            Assert.IsTrue(c.Rival(c.Voyage("voyage.bounty.tide").Rival).SailsArk);
            Assert.IsTrue(c.Rival(c.Voyage("voyage.bounty.grin").Rival).SailsArk);
        }

        [Test]
        public void BountyVoyagesSailFromTheBoardAndCarryTheirCrew()
        {
            var c = VoyageDepthHarness.LoadShippedContent();
            Assert.IsTrue(c.VoyageType(BountyType) is { OnBoard: true }, "Bounty is built");
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var jobs = VoyageRules.Board(s, c, c.Crew.Select(p => p.PilotId).Take(3).ToArray());
            var hunts = jobs.Where(j => j.TemplateId == BountyType && j.IsNamed).ToList();
            Assert.AreEqual(1, hunts.Count, "one Bounty a port, by its name");
            var job = hunts[0];
            var v = c.Voyage(job.VoyageId);
            Assert.AreEqual(v.Name, job.Title);
            Assert.AreEqual(v.Rival, job.RivalId, "the job hunts the voyage's crew");
            Assert.AreEqual(JobKind.Bounty, job.Kind);
        }
    }
}
#endif
