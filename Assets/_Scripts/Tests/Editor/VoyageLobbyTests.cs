using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Voyages Milestone 1's app shell (Frogma Rev 113 Sea, Crew, Lobby and Picker; Docs/MENU_REDESIGN_B.md section 209.1):
    /// the voyage's own seats (what the picker writes and Set sail sails), filling the AI seats from a saved squad, and
    /// how the crew popup splits open voyages from locked ones by faction.
    /// </summary>
    public class VoyageLobbyTests
    {
        [SetUp, TearDown]
        public void Forget() => VoyageSeatRecord.Clear();

        static SavedSquad Squad(VesselClassType a, VesselClassType b, VesselClassType c, string pa = "p.a", string pb = "p.b",
            string pc = "p.c") =>
            new("Squad", new SquadSlot(a, Element.Space, pa), new SquadSlot(b, Element.Charge, pb), new SquadSlot(c, Element.Time, pc));

        // ── The voyage's own seats ───────────────────────────────────────────

        [Test]
        public void Record_IsACopy_SoAPickNeverReachesTheSavedSquad()
        {
            var active = Squad(VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Sparrow);
            var seats = VoyageSeatRecord.For(VoyageSeatRecord.EscortKey, active);
            Assert.IsNotNull(seats);
            Assert.AreNotSame(active, seats, "The voyage must sail a copy, not the saved squad itself.");

            var slot = seats.GetSlot(0).WithVessel(VesselClassType.Rhino);
            seats.SetSlot(0, slot);
            Assert.AreEqual(VesselClassType.Manta, active.GetSlot(0).Vessel, "A seat pick changed the saved squad.");
            Assert.AreSame(seats, VoyageSeatRecord.Get(VoyageSeatRecord.EscortKey), "Set sail must read the same seats.");
            Assert.AreEqual(VesselClassType.Rhino, VoyageLaunch.SeatHull(VoyageSeatRecord.Get(VoyageSeatRecord.EscortKey)),
                "The hull Set sail spawns is not the one picked.");
        }

        [Test]
        public void Record_KeepsItsSeats_UntilAnotherVoyageOpens()
        {
            var active = Squad(VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Sparrow);
            var first = VoyageSeatRecord.For("job.a", active);
            Assert.AreSame(first, VoyageSeatRecord.For("job.a", Squad(VesselClassType.Rhino, VesselClassType.Rhino, VesselClassType.Rhino)),
                "Opening the same voyage again started its seats over.");
            var second = VoyageSeatRecord.For("job.b", active);
            Assert.AreNotSame(first, second);
            Assert.IsNull(VoyageSeatRecord.Get("job.a"), "Set sail on a voyage no longer open read another voyage's seats.");
            Assert.IsNull(VoyageSeatRecord.For(null, active));
        }

        [Test]
        public void JobKey_IsTheSameForADifficultyCopy()
        {
            var job = new VoyageJob { TemplateId = "job.raid.crystals", PilotId = "pilot.callsign.volt", To = "tollmoor" };
            var copy = VoyageDifficulty.At(job, 7);
            Assert.AreEqual(VoyageSeatRecord.JobKey(job), VoyageSeatRecord.JobKey(copy),
                "The voyage page sails a difficulty copy; its seats must still be found.");
        }

        // ── Fill the AI seats from a saved squad ─────────────────────────────

        [Test]
        public void Fill_TakesThePresetsWingmates_IntoAiSeatsOnly()
        {
            var seats = Squad(VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Sparrow);
            var preset = Squad(VesselClassType.Rhino, VesselClassType.Serpent, VesselClassType.Squirrel, "p.x", "p.y", "p.z");
            var filled = VoyageSeatRecord.FillAiSeats(seats, preset, friends: 0);

            Assert.AreEqual(VesselClassType.Manta, filled.GetSlot(0).Vessel, "Your own seat changed.");
            Assert.AreEqual(VesselClassType.Serpent, filled.GetSlot(1).Vessel);
            Assert.AreEqual(VesselClassType.Squirrel, filled.GetSlot(2).Vessel);
            Assert.AreEqual("p.y", filled.GetSlot(1).PilotId);
            Assert.AreEqual(VesselClassType.Dolphin, seats.GetSlot(1).Vessel, "Filling changed the seats it was given.");
        }

        [Test]
        public void Fill_LeavesAFriendsSeatAlone()
        {
            var seats = Squad(VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Sparrow);
            var preset = Squad(VesselClassType.Rhino, VesselClassType.Serpent, VesselClassType.Squirrel, "p.x", "p.y", "p.z");
            // One friend in the lobby takes seat 1 (VoyageRules.FriendAt): only seat 2 is the AI's.
            var filled = VoyageSeatRecord.FillAiSeats(seats, preset, friends: 1);
            Assert.AreEqual(VesselClassType.Dolphin, filled.GetSlot(1).Vessel, "A friend's seat was filled from the squad.");
            Assert.AreEqual(VesselClassType.Serpent, filled.GetSlot(2).Vessel, "The AI seat takes the preset's first wingmate.");
        }

        [Test]
        public void Fill_NeverSeatsYourPilotTwice()
        {
            var seats = Squad(VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Sparrow, "p.me");
            var preset = Squad(VesselClassType.Rhino, VesselClassType.Serpent, VesselClassType.Squirrel, "p.x", "p.me", "p.z");
            var filled = VoyageSeatRecord.FillAiSeats(seats, preset, friends: 0);
            Assert.AreEqual("p.me", filled.GetSlot(0).PilotId);
            Assert.AreNotEqual("p.me", filled.GetSlot(1).PilotId, "A wingmate took the pilot your own seat flies.");
        }

        [Test]
        public void AiSeats_AreTheSeatsNoPersonTakes()
        {
            Assert.IsFalse(VoyageSeatRecord.IsAiSeat(0, 0, 0), "Your seat is not an AI's.");
            Assert.IsTrue(VoyageSeatRecord.IsAiSeat(1, 0, 0));
            Assert.IsFalse(VoyageSeatRecord.IsAiSeat(1, 0, 1), "A friend's seat is not an AI's.");
            Assert.IsTrue(VoyageSeatRecord.IsAiSeat(2, 0, 1));
            Assert.IsFalse(VoyageSeatRecord.IsAiSeat(2, 0, 2));
        }

        // ── Open now and the crew popup ──────────────────────────────────────

        static VoyageBoard.Offer Offer(FactionType house, bool open) =>
            new() { House = house, SetSail = open ? _ => { } : null };

        [Test]
        public void OpenNow_IsWhatSails_AndTheRestIsLocked()
        {
            var offers = new List<VoyageBoard.Offer>
            {
                Offer(FactionType.Hunter, false), Offer(FactionType.Thief, true), Offer(FactionType.Pirate, false),
                new() { House = FactionType.None, SailLabel = "Under way" },
            };
            var open = new List<int>();
            VoyageCrewRules.OpenNow(offers, open);
            CollectionAssert.AreEqual(new[] { 1, 3 }, open, "A voyage under way is not locked.");
            Assert.AreEqual(2, VoyageCrewRules.LockedCount(offers));
        }

        [Test]
        public void Groups_AreTheFactionsWithLockedWork_InTheGamesOrder()
        {
            var offers = new List<VoyageBoard.Offer>
            {
                Offer(FactionType.Thief, false), Offer(FactionType.Hunter, false), Offer(FactionType.None, false),
                Offer(FactionType.Hunter, false), Offer(FactionType.Merchant, true),
            };
            var groups = new List<VoyageCrewRules.Group>();
            VoyageCrewRules.Groups(offers, groups);
            Assert.AreEqual(3, groups.Count, "An open voyage's faction, or a faction with no work, got a group.");
            Assert.AreEqual(FactionType.Hunter, groups[0].House);
            CollectionAssert.AreEqual(new[] { 1, 3 }, groups[0].Offers);
            Assert.AreEqual(FactionType.Thief, groups[1].House);
            Assert.AreEqual(FactionType.None, groups[2].House, "A pilot with no faction must come last.");
        }

        [Test]
        public void EveryFaction_HasAuthoredGroupWords()
        {
            var content = VoyageContentSO.Load();
            Assert.IsNotNull(content, "No shipped voyage content.");
            foreach (FactionType f in System.Enum.GetValues(typeof(FactionType)))
                Assert.IsFalse(string.IsNullOrEmpty(VoyageCrewRules.FactionName(content.Lines, f)), $"{f} has no group name.");
            var l = content.Lines;
            foreach (var line in new[] { l.SeaOpenNow, l.SeaCrewButton, l.CrewTitle, l.CrewLockedPress, l.ArkSailAs, l.ArkFillSquad,
                         l.ArkInvite, l.ArkChangeWingmate, l.PickSaved, l.PickSavedSeat })
                Assert.IsFalse(string.IsNullOrEmpty(line), "A Rev 113 line is empty in the shipped content.");
            StringAssert.Contains("{vessel}", l.ArkSailAs);
            StringAssert.Contains("{voyage}", l.PickSaved);
        }
    }
}
