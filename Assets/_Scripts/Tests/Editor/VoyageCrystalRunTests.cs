using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;

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
            Assert.AreEqual(20f, t.Goal);
            Assert.AreEqual(4, t.Cells);
            CollectionAssert.AreEqual(new[] { false, true, true, true }, t.HostileCells);
            Assert.AreEqual(0.4f, t.BreachHull, 1e-4f);
            Assert.AreEqual(10f, t.VentureGoal);
            Assert.AreEqual(1, t.VentureCells);
        }

        [Test]
        public void Board_OffersTheRun_WhoeverIsSeated()
        {
            var content = Shipped();
            var state = VoyageRules.NewGalaxy(content, Domains.Jade);
            var job = Raid(state, content);
            Assert.IsNotNull(job, "the board offers no Crystal Run");
            Assert.AreEqual(20f, job.Goal);
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
            Assert.AreEqual(30f, next.Goal);
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
    }
}
