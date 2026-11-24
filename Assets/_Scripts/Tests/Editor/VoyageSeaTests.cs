#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Milestone 2 shared base (Docs/Voyages/MILESTONE_2.md; Shombith 2026-11-24): named voyages from the Codex, a run
    /// of 1-6 cells, the sea's danger on rungs I1-I4 (never a timer) and the cell clock. Pure rules, so the headless voyage
    /// harness runs them too (Tools/Build/voyage_depth_harness).
    /// </summary>
    public class VoyageSeaTests
    {
        const string SalvageType = "job.salvage.drift";
        const string CrystalRunType = "job.raid.crystals";

        static VoyageContent Shipped() => VoyageDepthHarness.LoadShippedContent();

        static readonly string[] TypeNames = { "salvage", "escort", "escort the ark", "bounty", "hold the core", "piracy", "crystal run" };

        [Test]
        public void TheCodexVoyagesAreNamedNeverByTheirType()
        {
            var c = Shipped();
            Assert.GreaterOrEqual(c.Voyages.Count, 12, "three named voyages for each of the four types");
            foreach (var v in c.Voyages)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(v.Name), v.Id);
                Assert.IsFalse(TypeNames.Contains(v.Name.Trim().ToLowerInvariant()), $"{v.Id}: a type is never a name");
                Assert.That(v.Rung, Is.InRange(VoyageSea.MinRung, VoyageSea.MaxRung), v.Id);
                Assert.That(v.Cells, Is.InRange(VoyageSea.MinCells, VoyageSea.MaxCells), v.Id);
                Assert.IsNotNull(c.Location(v.To), $"{v.Id} sails to a known place");
            }
            Assert.AreEqual(c.Voyages.Count, c.Voyages.Select(v => v.Id).Distinct().Count(), "ids are unique");
        }

        [Test]
        public void APortOffersOneSalvageVoyageByItsNameRungAndCells()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var board = VoyageRules.Board(s, c, new[] { c.Crew[0].PilotId });
            var salvage = board.Where(j => j.TemplateId == SalvageType).ToList();
            Assert.AreEqual(1, salvage.Count, "one salvage voyage a port, so the board stays near six");
            var job = salvage[0];
            Assert.IsTrue(job.IsNamed);
            var v = c.Voyage(job.VoyageId);
            Assert.IsNotNull(v);
            Assert.AreEqual(v.Name, job.Title);
            Assert.AreEqual(v.Rung, job.Intensity);
            Assert.AreEqual(v.Cells, job.Cells);
            Assert.AreEqual(v.To, job.To);
            float perCell = c.Job(SalvageType).GoalPerCell;
            if (perCell > 0f) Assert.AreEqual(perCell * v.Cells, job.Goal, 1e-3f, "the goal grows with the cells");
        }

        [Test]
        public void CrystalRunStaysOnTheBoardAsItWas()
        {
            // Shombith, 2026-11-24: keep the Milestone 1 salvage mission in the lot, unchanged.
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var run = VoyageRules.Board(s, c, new[] { c.Crew[0].PilotId }).FirstOrDefault(j => j.TemplateId == CrystalRunType);
            Assert.IsNotNull(run, "Crystal Run is still offered");
            Assert.IsFalse(run.IsNamed);
            StringAssert.StartsWith("Crystal Run to ", run.Title);
            Assert.AreEqual(0f, c.Job(CrystalRunType).GoalPerCell, "its goal does not grow with cells");
            Assert.IsFalse(c.Voyages.Any(v => v.Type == CrystalRunType), "no named voyage takes Crystal Run's place");
        }

        [Test]
        public void NamedVoyagesOfTypesNotYetBuiltStayOffTheBoard()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Ruby);
            foreach (var pilot in c.Crew.Select(p => p.PilotId))
                CollectionAssert.IsEmpty(VoyageRules.Board(s, c, new[] { pilot }).Where(j => j.IsNamed && j.TemplateId != SalvageType)
                    .Select(j => j.VoyageId), "Bounty and Hold the Core sail as named voyages once they are built");
        }

        [Test]
        public void EveryVoyageTypeHasAGiver()
        {
            // The quay (Milestone 2 shell, stage 2) reads these: who offers the work, what they say, who is keen.
            var c = Shipped();
            foreach (var type in c.Voyages.Select(v => v.Type).Distinct())
            {
                var info = c.VoyageType(type);
                Assert.IsNotNull(info, $"{type} has a giver");
                Assert.IsNotNull(c.Person(info.Giver), $"{type}: the giver is a crew pilot");
                Assert.IsEmpty(info.Keen.Intersect(info.Reluctant.Concat(info.Refuse)), $"{type}: no faction both keen and not");
                Assert.IsNotEmpty(info.Say);
            }
            Assert.IsNull(c.VoyageType("job.nope"), "a probe, not a fault");
        }

        [Test]
        public void TheLadderRoughensRungByRung()
        {
            var r = VoyageSeaRung.Defaults();
            Assert.AreEqual(4, r.Length);
            for (int i = 1; i < r.Length; i++)
            {
                Assert.GreaterOrEqual(r[i].HostileShare, r[i - 1].HostileShare, $"I{i + 1} holds as many cells");
                Assert.Greater(r[i].Life, r[i - 1].Life, $"I{i + 1} has more life");
                Assert.GreaterOrEqual(r[i].Breach, r[i - 1].Breach);
                Assert.GreaterOrEqual(r[i].KaijuCrossings, r[i - 1].KaijuCrossings);
            }
            Assert.AreEqual(0f, r[0].HostileShare, "I1 is calm");
            Assert.AreEqual(1f, r[3].HostileShare, "I4 holds every cell");
            Assert.AreEqual(r[1].Life, VoyageSea.RungOf(null, 2).Life, "a short table falls back to the Codex");
            Assert.AreEqual(VoyageSea.RungOf(r, 4).Life, VoyageSea.RungOf(r, 9).Life, "rungs clamp to I4");
        }

        [Test]
        public void TheMostDangerousCellsAreTheHeldOnes()
        {
            Assert.AreEqual(1, VoyageSea.HostileCount(3, 1f / 3f));
            Assert.AreEqual(2, VoyageSea.HostileCount(3, 2f / 3f));
            Assert.AreEqual(2, VoyageSea.HostileCount(5, 1f / 3f), "1.67 rounds to 2");
            Assert.AreEqual(0, VoyageSea.HostileCount(4, 0f));
            Assert.AreEqual(4, VoyageSea.HostileCount(4, 1f));

            var p = VoyageSea.HostilePattern(4, 0.5f, new List<float> { 0.9f, 0.1f, 0.5f, 0.2f });
            CollectionAssert.AreEqual(new[] { true, false, true, false }, p);
            var flat = VoyageSea.HostilePattern(3, 1f / 3f, null);
            CollectionAssert.AreEqual(new[] { false, false, true }, flat, "without dangers the sea roughens toward the stop");
        }

        [Test]
        public void TheMapsDangerIsCutIntoTheRunsCells()
        {
            var d = VoyageSea.DangerPerCell(new List<double> { 0.1, 0.8, 0.2, 0.3, 0.6, 0.0 }, 3);
            CollectionAssert.AreEqual(new[] { 0.8f, 0.3f, 0.6f }, d);
            var more = VoyageSea.DangerPerCell(new List<double> { 0.4 }, 3);
            CollectionAssert.AreEqual(new[] { 0.4f, 0.4f, 0.4f }, more, "one real cell spreads over the run");
            CollectionAssert.AreEqual(new[] { 0f, 0f }, VoyageSea.DangerPerCell(null, 2));
        }

        [Test]
        public void LayingTheSeaSetsHeldCellsBreachAndLife()
        {
            var job = new VoyageJob { VoyageId = "voyage.test", Cells = 9, CellLife = new List<float> { 1f } };
            var rung = VoyageSeaRung.Defaults()[2];
            VoyageSea.Lay(job, rung, new List<float> { 0f, 0f, 0f, 1f, 1f, 1f });
            Assert.AreEqual(6, job.Cells, "cells clamp to six");
            Assert.AreEqual(4, job.HostileCells.Count(h => h), "two in three held at I3");
            Assert.IsTrue(job.HostileCells[3] && job.HostileCells[4] && job.HostileCells[5]);
            Assert.AreEqual(rung.Breach, job.BreachHull, 1e-5f);
            Assert.AreEqual(6, job.CellLife.Count);
            Assert.AreEqual(rung.Life, job.CellLife[5], 1e-5f);

            var unnamed = new VoyageJob { Cells = 9 };
            VoyageSea.Lay(unnamed, rung, null);
            Assert.AreEqual(9, unnamed.Cells, "unnamed work is left as it is");
        }

        [Test]
        public void KaijuWaitOnTheCrossingsTheRungAllows()
        {
            var r = VoyageSeaRung.Defaults();
            Assert.IsFalse(VoyageSea.KaijuOn(r[1], 0));
            Assert.IsTrue(VoyageSea.KaijuOn(r[2], 0));
            Assert.IsFalse(VoyageSea.KaijuOn(r[2], 1), "I3: the first crossing only");
            Assert.IsTrue(VoyageSea.KaijuOn(r[3], 3), "I4: every crossing");
        }

        [Test]
        public void TheCellClockSetsTheArksPace()
        {
            // Constant speed: the crossing is the distance over the speed.
            Assert.AreEqual(100.0, VoyageSea.CrossingSeconds(1000, 0, 10, 10), 1e-6);
            Assert.AreEqual(100.0, VoyageSea.CrossingSeconds(1000, 500, 10, 10), 1e-6);
            // Slowing near the core takes longer than cruising all the way.
            Assert.Greater(VoyageSea.CrossingSeconds(1000, 500, 10, 40), VoyageSea.CrossingSeconds(1000, 0, 40, 40));

            // The speed found crosses in the seconds asked for.
            float speed = VoyageSea.ArkSpeedFor(6500, 3000, 4, 35, 0f);
            Assert.AreEqual(35.0, VoyageSea.CrossingSeconds(6500, 3000, speed, speed * 4), 0.05);
            Assert.AreEqual(20f, VoyageSea.ArkSpeedFor(6500, 3000, 4, 35, 20f), "never above the crawl ceiling");
        }

        [Test]
        public void TheCourseFixCountsTheCellsSailed()
        {
            // Cells count from 1: in cell 2 of 4 and at its core, half the leg is sailed.
            Assert.AreEqual(0f, VoyageCourseFix.ProgressOf(1, 4, 0f), 1e-5f);
            Assert.AreEqual(0.5f, VoyageCourseFix.ProgressOf(2, 4, 1f), 1e-5f);
            Assert.AreEqual(1f, VoyageCourseFix.ProgressOf(4, 4, 1f), 1e-5f);
            Assert.AreEqual(0f, VoyageCourseFix.ProgressOf(0, 0, 0.5f), 1e-5f);
        }
    }
}
#endif
