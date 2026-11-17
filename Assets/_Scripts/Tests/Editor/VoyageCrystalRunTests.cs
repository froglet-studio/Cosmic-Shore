using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Crystal Run (Docs/Voyages/CRYSTAL_RUN.md; Shombith, 2026-11-16) and the voyage HUD's pure rules: the job every board
    /// offers, its leg fitted to four cells (three hostile, one calm), what was stolen and by whom, the stop that makes it
    /// owed, venturing further, the Ark's alarm toasts, the objective box's lines and the scoreboard's score.
    /// </summary>
    public class VoyageCrystalRunTests
    {
        const string RaidId = "job.raid.crystals";

        // The shipped asset, as the game reads it (the JSON copy writes the hostile pattern as 0 and 1).
        static VoyageContent Shipped() => VoyageContentSO.Load().Content;

        static VoyageJob Raid(VoyageState state, VoyageContent content) =>
            VoyageRules.Board(state, content, new[] { "pilot.any" }).FirstOrDefault(j => j.TemplateId == RaidId);

        [Test]
        public void ShippedTemplate_IsTheMilestoneRun()
        {
            var t = Shipped().Job(RaidId);
            Assert.IsNotNull(t, "voyage_slice.json has no " + RaidId);
            Assert.IsTrue(t.Always);
            Assert.IsTrue(t.CountsUnderway);
            Assert.AreEqual(50f, t.Goal);
            Assert.AreEqual(4, t.Cells);
            CollectionAssert.AreEqual(new[] { false, true, true, true }, t.HostileCells);
            // Below the leak line (30%) the hold leaks before the Ark falls, so the breach sits under it.
            Assert.AreEqual(0.1f, t.BreachHull, 1e-4f);
            Assert.AreEqual(10f, t.VentureGoal);
            Assert.AreEqual(1, t.VentureCells);
            // The leg cell by cell (Shombith's storyboard, 2026-11-17).
            CollectionAssert.AreEqual(new[] { "Caldera", "Orrery", "Garland", "Geode" }, t.CellWorlds);
            CollectionAssert.AreEqual(new[] { 20f, 30f, 30f, 40f }, t.CellAnchorSeconds);
            CollectionAssert.AreEqual(new[] { 0.8f, 1.3f, 0.9f, 1.4f }, t.CellLife);
        }

        [Test]
        public void Board_CopiesTheLegCellByCell()
        {
            var content = Shipped();
            var job = Raid(VoyageRules.NewGalaxy(content, Domains.Jade), content);
            Assert.IsNotNull(job);
            CollectionAssert.AreEqual(new[] { "Caldera", "Orrery", "Garland", "Geode" }, job.CellWorlds);
            CollectionAssert.AreEqual(new[] { 20f, 30f, 30f, 40f }, job.CellAnchorSeconds);
            CollectionAssert.AreEqual(new[] { 0.8f, 1.3f, 0.9f, 1.4f }, job.CellLife);
        }

        [Test]
        public void Extract_AtHome_PaysTheEscortHomeBonus_ThenResetsIt()
        {
            var state = VoyageRules.NewGalaxy(Shipped(), Domains.Jade);
            Assert.IsTrue(state.IsHome());
            VoyageRaid.Owe(state, Element.Time, 5);
            state.HomeRewardScale = 2f;
            var x = VoyageRules.Extract(state, Shipped());
            Assert.IsNotNull(x);
            Assert.AreEqual(10, x.Crystals[(int)Element.Time]);
            Assert.AreEqual(1f, state.HomeRewardScale);
            Assert.AreEqual(0, state.OwedOf(Element.Time));
        }

        [Test]
        public void Extract_Anywhere_BanksAtTheStopAsItIs_AndTheArkIsHome()
        {
            var state = VoyageRules.NewGalaxy(Shipped(), Domains.Jade);
            string home = state.Home;
            state.At = "somewhere.else";
            VoyageRaid.Owe(state, Element.Mass, 4);
            state.HomeRewardScale = 2f;
            Assert.IsNull(VoyageRules.Extract(state, Shipped()), "away from home only Finish here banks");
            var x = VoyageRules.Extract(state, Shipped(), anywhere: true);
            Assert.IsNotNull(x);
            Assert.AreEqual(4, x.Crystals[(int)Element.Mass], "finishing away from home pays no escort bonus");
            Assert.AreEqual(home, state.At);
            Assert.AreEqual(1f, state.HomeRewardScale);
        }

        [Test]
        public void NameCells_StandsTheNamedWorlds_KeepsTheRest()
        {
            var pool = new[] { "Yggdra", "Caldera", "Orrery", "Garland", "Geode" };
            var drawn = new System.Collections.Generic.List<int> { 0, 0, 0, 0, 0 };
            var named = VoyageRaid.NameCells(drawn, new[] { "Caldera", "Nowhere", "", "geode" }, pool);
            // Named and found; unknown and empty names keep the drawn world; cells past the list keep theirs; any case.
            CollectionAssert.AreEqual(new[] { 1, 0, 0, 4, 0 }, named);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, drawn, "the drawn corridor is not changed in place");
            Assert.AreSame(drawn, VoyageRaid.NameCells(drawn, new string[0], pool));
        }

        [Test]
        public void PerCell_TakesTheLastEntryPastTheEnd()
        {
            var secs = new[] { 20f, 30f, 40f };
            Assert.AreEqual(20f, VoyageRaid.PerCell(secs, 0, 99f));
            Assert.AreEqual(40f, VoyageRaid.PerCell(secs, 2, 99f));
            Assert.AreEqual(40f, VoyageRaid.PerCell(secs, 5, 99f));
            Assert.AreEqual(99f, VoyageRaid.PerCell(secs, -1, 99f));
            Assert.AreEqual(99f, VoyageRaid.PerCell(new float[0], 1, 99f));
        }

        [Test]
        public void LayCellLife_OverridesThePacingCellByCell()
        {
            var paced = new System.Collections.Generic.List<float> { 1f, 1.3f, 0.9f, 1f, 1.2f };
            var laid = VoyageRaid.LayCellLife(paced, new[] { 0.8f, 0f, 0.9f, 1.4f });
            // A 0 entry keeps the pacing; a cell past the list (a ventured fifth) keeps the lane's.
            CollectionAssert.AreEqual(new[] { 0.8f, 1.3f, 0.9f, 1.4f, 1.2f }, laid);
        }

        [Test]
        public void Board_OffersTheRun_WhoeverIsSeated()
        {
            var content = Shipped();
            var state = VoyageRules.NewGalaxy(content, Domains.Jade);
            var job = Raid(state, content);
            Assert.IsNotNull(job, "the board offers no Crystal Run");
            Assert.AreEqual(50f, job.Goal);
            Assert.AreEqual(4, job.Cells);
            Assert.AreEqual(0, job.Venture);
            Assert.IsTrue(job.CountsUnderway);
            CollectionAssert.AreEqual(new[] { false, true, true, true }, job.HostileCells);
        }

        [Test]
        public void HostileCells_ThreeHostileOneCalm()
        {
            var job = new VoyageJob { HostileCells = { false, true, true, true } };
            Assert.IsFalse(VoyageRules.HostileCell(job, 0, 4, false));
            Assert.IsTrue(VoyageRules.HostileCell(job, 1, 4, false));
            Assert.IsTrue(VoyageRules.HostileCell(job, 3, 4, false));
            // A ventured run's extra cell takes the last entry.
            Assert.IsTrue(VoyageRules.HostileCell(job, 4, 5, false));
        }

        [Test]
        public void FitCorridor_TrimsOrPadsTheStart_KeepingTheDestinationLast()
        {
            var longer = VoyageRaid.FitCorridor(new() { 1, 2, 3, 4, 5, 6 }, 4, 8, 3, "k");
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6 }, longer);
            var shorter = VoyageRaid.FitCorridor(new() { 5, 6 }, 4, 8, 3, "k");
            Assert.AreEqual(4, shorter.Count);
            Assert.AreEqual(5, shorter[2]);
            Assert.AreEqual(6, shorter[3]);
            Assert.AreNotEqual(shorter[1], shorter[2], "a padded cell is never the cell beside it");
            CollectionAssert.AreEqual(new[] { 7, 8 }, VoyageRaid.FitCorridor(new() { 7, 8 }, 0, 8, 3, "k"));
            var scales = VoyageRaid.FitProfile(new() { 0.5f, 1f }, 4);
            CollectionAssert.AreEqual(new[] { 0.5f, 0.5f, 0.5f, 1f }, scales);
        }

        [Test]
        public void Tally_CountsElementalCrystalsBySeat_NeverOmni()
        {
            var tally = new VoyageRaidTally();
            Assert.IsTrue(tally.Add("a", Element.Space));
            Assert.IsTrue(tally.Add("a", Element.Time));
            Assert.IsTrue(tally.Add("b", Element.Mass));
            Assert.IsFalse(tally.Add("b", Element.Omni));
            Assert.IsFalse(tally.Add(string.Empty, Element.Charge));
            tally.Kill("b", nearArk: true);
            tally.Kill("b");
            Assert.AreEqual(3, tally.Total);
            Assert.AreEqual(1, tally.Of(Element.Mass));
            Assert.AreEqual(2, tally.SeatOf("b").Kills);
            Assert.AreEqual(1, tally.SeatOf("b").Guarded);
        }

        [Test]
        public void Arrive_MetGoal_SucceedsAndOwesEverythingStolenPlusPay()
        {
            var content = Shipped();
            var state = VoyageRules.NewGalaxy(content, Domains.Jade);
            var job = Raid(state, content);
            Assert.IsTrue(VoyageRules.Take(state, content, job));
            var tally = new VoyageRaidTally();
            for (int i = 0; i < 12; i++) tally.Add("a", Element.Charge);
            for (int i = 0; i < 8; i++) tally.Add("b", Element.Time);
            Assert.IsNotNull(VoyageRules.Arrive(state, content));
            var r = VoyageRaid.Arrive(state, content, tally);
            Assert.AreEqual(JobOutcome.Succeeded, r.Outcome);
            Assert.AreEqual(12, state.OwedOf(Element.Charge));
            Assert.AreEqual(8, state.OwedOf(Element.Time));
            Assert.Greater(state.OwedOf(content.Job(RaidId).RewardElement), 0, "the job's pay is owed too");
        }

        [Test]
        public void Arrive_MissedGoal_FailsButStillOwesWhatWasStolen()
        {
            var content = Shipped();
            var state = VoyageRules.NewGalaxy(content, Domains.Jade);
            var job = Raid(state, content);
            Assert.IsTrue(VoyageRules.Take(state, content, job));
            var tally = new VoyageRaidTally();
            for (int i = 0; i < 5; i++) tally.Add("a", Element.Space);
            VoyageRules.Arrive(state, content);
            var r = VoyageRaid.Arrive(state, content, tally);
            Assert.AreEqual(JobOutcome.Failed, r.Outcome);
            Assert.AreEqual(5, state.OwedOf(Element.Space));
        }

        [Test]
        public void Arrive_MissedGoal_ForfeitsTheHaulButKeepsWhatEarlierLegsOwe()
        {
            var content = Shipped();
            var state = VoyageRules.NewGalaxy(content, Domains.Jade);
            var job = Raid(state, content);
            Assert.IsTrue(VoyageRules.Take(state, content, job));
            VoyageRaid.Owe(state, Element.Mass, 3);   // an earlier leg's haul
            var tally = new VoyageRaidTally();
            for (int i = 0; i < 5; i++) tally.Add("a", Element.Space);
            VoyageRules.Arrive(state, content);
            var r = VoyageRaid.Arrive(state, content, tally, forfeitOnMiss: true);
            Assert.AreEqual(JobOutcome.Failed, r.Outcome);
            Assert.AreEqual(0, state.OwedOf(Element.Space), "the missed run's haul is lost");
            Assert.AreEqual(3, state.OwedOf(Element.Mass), "what earlier legs owe is kept");
        }

        [Test]
        public void Retry_KeepsTheGoalAndCells()
        {
            var content = Shipped();
            var state = VoyageRules.NewGalaxy(content, Domains.Jade);
            var job = Raid(state, content);
            Assert.IsTrue(VoyageRules.Take(state, content, job));
            VoyageRules.Arrive(state, content);
            var again = VoyageRaid.Retry(state, content, job);
            Assert.IsNotNull(again, "nowhere to try again");
            Assert.AreEqual(job.Venture, again.Venture);
            Assert.AreEqual(job.Goal, again.Goal);
            Assert.AreEqual(job.Cells, again.Cells);
            Assert.AreEqual(state.At, again.From);
        }

        [Test]
        public void Venture_RaisesTheGoalByTenAndAddsACell()
        {
            var content = Shipped();
            var state = VoyageRules.NewGalaxy(content, Domains.Jade);
            var job = Raid(state, content);
            Assert.IsTrue(VoyageRules.Take(state, content, job));
            VoyageRules.Arrive(state, content);
            var next = VoyageRaid.Venture(state, content, job);
            Assert.IsNotNull(next, "nowhere further to venture");
            Assert.AreEqual(1, next.Venture);
            Assert.AreEqual(60f, next.Goal);
            Assert.AreEqual(5, next.Cells);
            Assert.AreEqual(state.At, next.From);
        }

        [Test]
        public void Score_WeighsCrystalsKillsAndTheArk()
        {
            Assert.AreEqual(10 * 3 + 5 * 2 + 15 * 1, VoyageRaid.Score(3, 2, 1, 10, 5, 15));
            Assert.AreEqual(0, VoyageRaid.Score(0, 0, 0, 10, 5, 15));
        }

        [Test]
        public void Alarms_BreachCloseOnce_UnderAttackNoMoreThanEveryTwentySeconds()
        {
            var memory = new ArkAlarmToasts.Memory();
            var safe = new ArkEscortStatus { Active = true, Hull = 0.9f, HullTotal = 40, BreachLine = 0.4f };
            var close = safe;
            close.Hull = 0.45f;
            Assert.AreEqual(ArkAlarmToasts.Alarm.UnderAttack,
                ArkAlarmToasts.Decide(ref memory, ArkEscortSignalKind.ArkUnderAttack, safe, 10f));
            Assert.AreEqual(ArkAlarmToasts.Alarm.None,
                ArkAlarmToasts.Decide(ref memory, ArkEscortSignalKind.ArkUnderAttack, safe, 20f));
            Assert.AreEqual(ArkAlarmToasts.Alarm.BreachClose,
                ArkAlarmToasts.Decide(ref memory, ArkEscortSignalKind.HullChanged, close, 21f));
            Assert.AreEqual(ArkAlarmToasts.Alarm.None,
                ArkAlarmToasts.Decide(ref memory, ArkEscortSignalKind.HullChanged, close, 22f));
            // Climbing back out and dropping again warns again.
            ArkAlarmToasts.Decide(ref memory, ArkEscortSignalKind.HullChanged, safe, 23f);
            Assert.AreEqual(ArkAlarmToasts.Alarm.BreachClose,
                ArkAlarmToasts.Decide(ref memory, ArkEscortSignalKind.HullChanged, close, 24f));
            Assert.AreEqual(ArkAlarmToasts.Alarm.UnderAttack,
                ArkAlarmToasts.Decide(ref memory, ArkEscortSignalKind.ArkUnderAttack, safe, 31f));
            // No breach line: never "breach close".
            var none = close;
            none.BreachLine = 0f;
            var fresh = new ArkAlarmToasts.Memory();
            Assert.AreEqual(ArkAlarmToasts.Alarm.None,
                ArkAlarmToasts.Decide(ref fresh, ArkEscortSignalKind.HullChanged, none, 1f));
        }

        [Test]
        public void ObjectiveText_EscortGoal_Route_ArkState_AndActionPriority()
        {
            var lines = new VoyageLines();
            var o = new VoyageObjective
            {
                Goal = VoyageObjective.Kind.Escort, Difficulty = 3, Breach = 0.25f, To = "The Maw", Cells = 5, Cell = 2,
                Stops = new[] { "Shoal", "Reef", "", "Drift", "The Maw" }, World = "Reef", Inside = true, Hostile = true,
            };
            Assert.AreEqual("ESCORT THE ARK - DIFFICULTY 3", VoyageObjectiveText.Kicker(lines, o));
            Assert.AreEqual("Keep the Ark's hull above 25% to The Maw's core", VoyageObjectiveText.GoalLine(lines, o));
            Assert.AreEqual("Reef", VoyageObjectiveText.StopName(lines, o, 1));
            Assert.AreEqual("Cell 3", VoyageObjectiveText.StopName(lines, o, 2));
            Assert.AreEqual("Ark inside Reef", VoyageObjectiveText.UnderHull(lines, o));
            Assert.AreEqual((lines.ObjectiveEscortHostile, true), VoyageObjectiveText.Action(lines, o));
            o.Healing = true;
            Assert.AreEqual((lines.ObjectiveActionHealing, false), VoyageObjectiveText.Action(lines, o));
            o.UnderAttack = true;
            Assert.AreEqual((lines.ObjectiveActionUnderAttack, true), VoyageObjectiveText.Action(lines, o));
            o.Assault = ArkAssaultPhase.Incoming;
            o.AssaultSeconds = 4.2f;
            Assert.AreEqual(("Fauna turn on the Ark in 5s. Get close to it.", true), VoyageObjectiveText.Action(lines, o));
            o.Assault = ArkAssaultPhase.Active;
            o.AssaultKills = 2;
            o.AssaultKillsNeeded = 6;
            Assert.AreEqual(("Drive the assault off: 2 / 6 attackers near the Ark", true), VoyageObjectiveText.Action(lines, o));
        }

        [Test]
        public void ObjectiveText_CrystalRun_CounterGoalAndGoalMet()
        {
            var lines = new VoyageLines();
            var o = new VoyageObjective { Goal = VoyageObjective.Kind.CrystalRun, Target = 20, Done = 12, To = "Tidehold", Cells = 4, Cell = 1 };
            Assert.AreEqual(lines.ObjectiveRaidKicker, VoyageObjectiveText.Kicker(lines, o));
            Assert.AreEqual("Steal 20 crystals before the Ark reaches Tidehold", VoyageObjectiveText.GoalLine(lines, o));
            Assert.AreEqual("Tidehold", VoyageObjectiveText.StopName(lines, o, 3), "the last stop is the destination");
            Assert.AreEqual("Cell 1", VoyageObjectiveText.StopName(lines, o, 0));
            Assert.AreEqual((lines.ObjectiveCalm, false), VoyageObjectiveText.Action(lines, o));
            o.Done = 20;
            Assert.AreEqual((lines.ObjectiveGoalMet, false), VoyageObjectiveText.Action(lines, o));
            Assert.AreEqual(string.Empty, VoyageObjectiveText.UnderHull(lines, o));
        }

        [Test]
        public void ObjectiveStrip_Escort_TitleHullPlaceAndOneJobLine()
        {
            var lines = new VoyageLines();
            var o = new VoyageObjective
            {
                Goal = VoyageObjective.Kind.Escort, Difficulty = 3, Breach = 0.25f, Hull = 0.784f, To = "The Maw", Cells = 5,
                Cell = 2, Stops = new[] { "Shoal", "Scarab Reef", "", "Drift", "The Maw" }, World = "Scarab Reef",
                Inside = true, Hostile = true,
            };
            Assert.AreEqual("ESCORT THE ARK", VoyageObjectiveText.Title(lines, o), "no difficulty on the strip");
            Assert.AreEqual("78%", VoyageObjectiveText.Figure(lines, o));
            Assert.AreEqual("Scarab Reef - hostile", VoyageObjectiveText.Place(lines, o));
            Assert.AreEqual((lines.ObjectiveEscortHostile, true), VoyageObjectiveText.StripAction(lines, o));
            o.Hostile = false;
            Assert.AreEqual("Scarab Reef - calm", VoyageObjectiveText.Place(lines, o));
            Assert.AreEqual((lines.ObjectiveEscortCalm, false), VoyageObjectiveText.StripAction(lines, o));
        }

        [Test]
        public void ObjectiveStrip_CrystalRun_CountCellAndThreatOnly()
        {
            var lines = new VoyageLines();
            var o = new VoyageObjective
            {
                Goal = VoyageObjective.Kind.CrystalRun, Target = 20, Done = 12, Hull = 0.9f, To = "Tidehold", Cells = 4,
                Cell = 2, Hostile = true,
            };
            Assert.AreEqual(lines.ObjectiveRaidKicker, VoyageObjectiveText.Title(lines, o));
            Assert.AreEqual("12 / 20", VoyageObjectiveText.Figure(lines, o));
            Assert.AreEqual("Cell 2 - hostile", VoyageObjectiveText.Place(lines, o), "the cell the Ark is in, by name");
            Assert.AreEqual((string.Empty, false), VoyageObjectiveText.StripAction(lines, o),
                "a Crystal Run's strip says nothing more unless the Ark is threatened");
            o.Done = 20;
            Assert.AreEqual((lines.ObjectiveGoalMet, false), VoyageObjectiveText.StripAction(lines, o));
            o.UnderAttack = true;
            Assert.AreEqual((lines.ObjectiveActionUnderAttack, true), VoyageObjectiveText.StripAction(lines, o));
        }

        [Test]
        public void ObjectiveStrip_NoGoal_ShowsNothing_AndAnEmptyTemplateDeletesTheLine()
        {
            var lines = new VoyageLines();
            var none = new VoyageObjective { Goal = VoyageObjective.Kind.None };
            Assert.AreEqual(string.Empty, VoyageObjectiveText.Title(lines, none));
            Assert.AreEqual(string.Empty, VoyageObjectiveText.Figure(lines, none));
            Assert.AreEqual(string.Empty, VoyageObjectiveText.Place(lines, none));
            lines.ObjectiveStripPlace = string.Empty;
            var o = new VoyageObjective { Goal = VoyageObjective.Kind.Escort, World = "Reef" };
            Assert.AreEqual(string.Empty, VoyageObjectiveText.Place(lines, o));
        }

        [Test]
        public void HudLines_AreAuthored_InTheShippedContent()
        {
            var lines = VoyageContentSO.Load()?.Lines;
            Assert.IsNotNull(lines);
            foreach (var line in new[]
                     {
                         lines.ToastSetOff, lines.ToastCellHostile, lines.ToastCellCalm, lines.ToastFinalCell, lines.ToastGoalReached,
                         lines.ToastHome, lines.ToastArkUnderAttack, lines.ToastBreachClose, lines.ObjectiveRaidKicker,
                         lines.ObjectiveEscortKicker, lines.ObjectiveEscortGoal, lines.ObjectiveActionAssault, lines.ScoreFinish,
                         lines.ObjectiveStripEscort, lines.ObjectiveStripHull, lines.ObjectiveStripPlace, lines.ObjectiveStripElement,
                         lines.ScoreVenture, lines.ScoreTryAgain, lines.ScoreColGuarded, lines.ScoreColScore,
                     })
                Assert.IsFalse(string.IsNullOrEmpty(line));
        }

        [Test]
        public void AttackerMarks_EdgeArrow_PointsTheWayToTurn()
        {
            Assert.IsTrue(ArkAttackerMarks.OnScreen(new UnityEngine.Vector3(0.5f, 0.5f, 10f)));
            Assert.IsFalse(ArkAttackerMarks.OnScreen(new UnityEngine.Vector3(0.5f, 0.5f, -10f)), "behind the camera");
            Assert.IsFalse(ArkAttackerMarks.OnScreen(new UnityEngine.Vector3(1.4f, 0.5f, 10f)));
            var right = ArkAttackerMarks.EdgePoint(new UnityEngine.Vector3(3f, 0.5f, 10f), 0.4f);
            Assert.AreEqual(0.9f, right.x, 1e-4f);
            Assert.AreEqual(0.5f, right.y, 1e-4f);
            // Behind and to the right shows on the left: turn that way.
            var behind = ArkAttackerMarks.EdgePoint(new UnityEngine.Vector3(0.8f, 0.5f, -10f), 0.4f);
            Assert.Less(behind.x, 0.5f);
        }

        [Test]
        public void AttackerMarks_AreVolumes_NotFlatSheets()
        {
            foreach (var mesh in new[] { ArkAttackerMarks.Diamond(), ArkAttackerMarks.Arrow() })
            {
                var size = mesh.bounds.size;
                Assert.Greater(UnityEngine.Mathf.Min(size.x, UnityEngine.Mathf.Min(size.y, size.z)), 0.5f, mesh.name + " is flat");
                // Flat-shaded: many facing directions, and every facet faces out from the centre.
                var v = mesh.vertices;
                var n = mesh.normals;
                var t = mesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var centre = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                    Assert.Greater(UnityEngine.Vector3.Dot(n[t[i]], centre - mesh.bounds.center), 0f, mesh.name + " facet faces in");
                }
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        // ── P1 and P6 (Shombith, 2026-11-16): the raid window and carried crystals ──

        [Test]
        public void VoyageRecord_KeepsEveryLegsSeats()
        {
            // The docked board reads the whole voyage: the raid leg's count must survive the way home's fresh leg.
            var voyage = new VoyageRaidTally();
            var leg = new VoyageRaidTally();
            leg.Add("you", Element.Mass);
            leg.Add("you", Element.Space);
            leg.Kill("you", nearArk: true);
            leg.Carry("you", Element.Time);
            voyage.Absorb(leg);
            leg.Clear();
            leg.Add("you", Element.Mass);
            voyage.Absorb(leg);
            var you = voyage.SeatOf("you");
            Assert.AreEqual(3, you.Total);
            Assert.AreEqual(2, you.Crystals[(int)Element.Mass]);
            Assert.AreEqual(1, you.Kills);
            Assert.AreEqual(1, you.Guarded);
            Assert.AreEqual(0, voyage.CarriedTotal, "unbanked crystals never reach the voyage record");
            voyage.Absorb(voyage);
            Assert.AreEqual(3, voyage.Total, "absorbing itself changes nothing");
        }

        [Test]
        public void CarriedCrystals_CountOnlyOnceBanked()
        {
            var tally = new VoyageRaidTally();
            Assert.IsTrue(tally.Carry("you", Element.Time));
            Assert.IsTrue(tally.Carry("you", Element.Mass));
            Assert.IsFalse(tally.Carry("you", Element.Omni), "an omni crystal never counts");
            Assert.AreEqual(0, tally.Total, "carried is not stolen yet");
            Assert.AreEqual(2, tally.CarriedBy("you"));
            Assert.AreEqual(2, tally.Bank("you"));
            Assert.AreEqual(2, tally.Total);
            Assert.AreEqual(1, tally.Of(Element.Time));
            Assert.AreEqual(0, tally.CarriedBy("you"));
            Assert.AreEqual(0, tally.Bank("you"), "nothing left to bank");
        }

        [Test]
        public void Spill_LosesOnlyWhatIsCarried()
        {
            var tally = new VoyageRaidTally();
            tally.Carry("you", Element.Space);
            tally.Bank("you");
            tally.Carry("you", Element.Charge);
            tally.Carry("you", Element.Charge);
            var lost = tally.Spill("you");
            Assert.AreEqual(2, lost[(int)Element.Charge]);
            Assert.AreEqual(0, lost[(int)Element.Space]);
            Assert.AreEqual(1, tally.Total, "banked crystals are safe");
            Assert.AreEqual(0, tally.CarriedTotal);
            Assert.AreEqual(0, tally.Spill("nobody").Sum(), "a seat never seen spills nothing");
        }

        [Test]
        public void StripAction_ShowsTheRaidWindowThenWhatYouCarry()
        {
            var lines = new VoyageLines();
            var o = new VoyageObjective { Goal = VoyageObjective.Kind.CrystalRun, Target = 20, Anchored = true, AnchorSeconds = 42f, Carried = 3 };
            Assert.AreEqual(("Raid: the Ark sails in 0:42", false), VoyageObjectiveText.StripAction(lines, o));
            o.AnchorClosing = true;
            Assert.IsTrue(VoyageObjectiveText.StripAction(lines, o).threat, "a closing window is a threat");
            o.Anchored = false;
            Assert.AreEqual(("Carrying 3. Bank them at the Ark.", false), VoyageObjectiveText.StripAction(lines, o));
            o.Assault = ArkAssaultPhase.Active;
            Assert.IsTrue(VoyageObjectiveText.StripAction(lines, o).threat, "an assault on the Ark comes first");
            lines.ObjectiveStripCarried = string.Empty;
            o.Assault = ArkAssaultPhase.None;
            Assert.AreEqual((string.Empty, false), VoyageObjectiveText.StripAction(lines, o), "an empty line shows nothing");
        }

        [Test]
        public void Leak_TakesOneBankedCrystal_FromTheRichestSeat_NeverCarried()
        {
            var tally = new VoyageRaidTally();
            tally.Add("Ana", Element.Mass);
            tally.Add("Ana", Element.Mass);
            tally.Add("Ana", Element.Time);
            tally.Add("Bo", Element.Space);
            tally.Carry("Bo", Element.Charge);
            Assert.AreEqual(Element.Mass, tally.Leak());
            Assert.AreEqual(3, tally.Total);
            Assert.AreEqual(1, tally.SeatOf("Ana").Crystals[(int)Element.Mass]);
            tally.Leak();
            tally.Leak();
            tally.Leak();
            Assert.AreEqual(0, tally.Total, "every banked crystal leaked");
            Assert.AreEqual(Element.None, tally.Leak(), "nothing banked: nothing to lose");
            Assert.AreEqual(1, tally.CarriedBy("Bo"), "a carried crystal is not in the hold");
        }

        [Test]
        public void LeakOwed_TakesTheElementOwedMost()
        {
            var state = new VoyageState();
            VoyageRaid.Owe(state, Element.Time, 2);
            VoyageRaid.Owe(state, Element.Space, 5);
            Assert.AreEqual(Element.Space, state.LeakOwed());
            Assert.AreEqual(4, state.OwedOf(Element.Space));
            Assert.AreEqual(Element.None, new VoyageState().LeakOwed());
        }

        [Test]
        public void StripAction_LeakComesBeforeTheWindow_AndCompleteAfterTheGoal()
        {
            var lines = new VoyageLines();
            var o = new VoyageObjective { Goal = VoyageObjective.Kind.CrystalRun, Target = 50, Done = 50, Hull = 0.72f, CanComplete = true };
            Assert.AreEqual(("Goal met. Pause to complete and set sail.", false), VoyageObjectiveText.StripAction(lines, o));
            Assert.AreEqual("Ark hull 72%", VoyageObjectiveText.HullLine(lines, o));
            o.Leaking = true;
            o.LeakLine = 0.3f;
            o.Anchored = true;
            Assert.AreEqual(("Hull below 30%: the hold is losing crystals.", true), VoyageObjectiveText.StripAction(lines, o));
            var home = new VoyageObjective { Goal = VoyageObjective.Kind.SailingHome, Leaking = true, LeakLine = 0.3f, Hull = 0.2f };
            Assert.IsTrue(VoyageObjectiveText.StripAction(lines, home).threat, "the way home says the hold is leaking");
            Assert.AreEqual(string.Empty, VoyageObjectiveText.HullLine(lines, home), "the way home's figure is the hull already");
        }

        // ── Bigger cells, short crossings, kaiju (Shombith, 2026-11-17) ─────────────────────────────

        [Test]
        public void CellSpacing_IsTwoGrownMembranesAndTheGap()
        {
            Assert.AreEqual(2f * 1200f * 2.5f + 400f, CellConveyor.CellSpacingFor(1200f, 2.5f, 400f), 1e-3f);
            // A membrane smaller than the freestyle one is taken at the floor; a scale below 1 at 1; a negative gap at 0.
            Assert.AreEqual(2f * CellConveyor.MembraneRadiusFloor, CellConveyor.CellSpacingFor(300f, 0.5f, -10f), 1e-3f);
            // A larger world's membrane spaces the whole corridor so no two cells overlap.
            Assert.AreEqual(2f * 1635.2f * 2f + 400f, CellConveyor.CellSpacingFor(1635.2f, 2f, 400f), 1e-2f);
        }

        [Test]
        public void ShippedVoyage_BigCellsShortGapsScatteredLifeAndKaiju()
        {
            var settings = VoyageToyDefinitionSO.Load();
            Assert.IsNotNull(settings, "Resources/Voyages/Voyage.asset");
            var cfg = settings.BuildArkConfig(null);
            Assert.GreaterOrEqual(cfg.CellScale, 2.5f, "voyage cells are at least 2.5x their authored size");
            Assert.Greater(cfg.CellGap, 0f, "the corridor spaces voyage cells by their membranes");
            Assert.Less(cfg.CellGap, 1200f, "the open water between cells is shorter than at 1.5x (1200 u)");
            Assert.IsTrue(cfg.ScatterLifeSpawns, "a voyage cell's life is born all through its open water");
            Assert.Greater(settings.KaijuPerCrossing, 0);
            Assert.GreaterOrEqual(settings.DesktopKaijuScale, 10f, "a kaiju is huge");
            Assert.Greater(settings.KaijuMax, 0);
            Assert.Greater(settings.MobileKaijuScale, settings.HomewardRaiderScale, "a kaiju is bigger than a raider, phones too");
            Assert.IsTrue(settings.RaidersOutbound);
            var lines = VoyageContentSO.Load().Lines;
            Assert.IsFalse(string.IsNullOrEmpty(lines.ToastKaiju));
        }

        [Test]
        public void ShippedVoyage_StrayingPastTheArkCountsDownThenRecalls()
        {
            var settings = VoyageToyDefinitionSO.Load();
            Assert.IsNotNull(settings, "Resources/Voyages/Voyage.asset");
            var cfg = settings.BuildArkConfig(null);
            // The voyage's leash is its own: within reach of a whole voyage cell (the Ark anchors at the core), well short
            // of the Arkway's three cell radii, which a pilot never reached.
            Assert.GreaterOrEqual(cfg.LeashRadiusFactor, 1f, "a pilot can raid the whole cell the Ark anchors in");
            Assert.Less(cfg.LeashRadiusFactor, 2f, "straying is reached in a voyage, not three cell radii out");
            Assert.AreEqual(settings.StrayRadiusCells, cfg.LeashRadiusFactor, 1e-4f);
            Assert.AreEqual(5f, cfg.LeashGraceSeconds, 1e-4f, "the warning counts 5, 4, 3, 2, 1");
            var lines = VoyageContentSO.Load().Lines;
            StringAssert.Contains("{seconds}", lines.ToastStray, "the countdown shows the seconds left");
            StringAssert.Contains("{carried}", lines.ToastStrayCarrySub, "the warning names the crystals at stake");
            StringAssert.Contains("{count}", lines.ToastRecalledLostSub);
            Assert.IsFalse(string.IsNullOrEmpty(lines.ToastRecalled));
        }

        [Test]
        public void ShippedVoyage_PhonesTakeLighterLifeAndKaiju()
        {
            var settings = VoyageToyDefinitionSO.Load();
            Assert.IsNotNull(settings, "Resources/Voyages/Voyage.asset");
            Assert.AreEqual(1, settings.KaijuMax, "one kaiju alive at a time");
            Assert.LessOrEqual(settings.MobileCellLifeScale, settings.DesktopCellLifeScale, "a phone holds no more life");
            Assert.Less(settings.MobileKaijuScale, settings.DesktopKaijuScale, "a phone's kaiju is smaller");
            Assert.Greater(settings.MobileRaiderMax, 0);
            Assert.Greater(settings.RaidAnchorMinLife, 0, "the Ark sails through a cell with nothing to steal");
            Assert.LessOrEqual(settings.CellFloraScale, settings.DesktopCellLifeScale,
                "plants (a cell's growing prism mass) never outgrow the creatures' scale");
            Assert.Greater(CellConveyor.DepartedRetireMargin, 0f, "the cell behind retires once the Ark is clear of it");
            Assert.LessOrEqual(settings.CellLifeMax, 2.5f, "no voyage cell grows past 2.5x its creatures");
            Assert.Less(settings.HomewardLureFactor, 2f, "the way home no longer draws most of a cell onto the Ark");
        }

        [Test]
        public void KaijuSpecies_IsNeverAMissingConfig()
        {
            Assert.IsFalse(VoyageRun.IsKaijuSpecies(null));
            var empty = ScriptableObject.CreateInstance<FaunaConfigurationSO>();
            try { Assert.IsFalse(VoyageRun.IsKaijuSpecies(empty), "a config without a prefab is no kaiju"); }
            finally { UnityEngine.Object.DestroyImmediate(empty); }
        }
    }
}
