using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void PartySeat_IsAFriendsOwnRecord_KeptAcrossVoyages()
        {
            // A friend in the host's lobby picks one seat for whatever voyage the host sails.
            var active = Squad(VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Sparrow);
            var mine = VoyageSeatRecord.For(VoyageSeatRecord.PartyKey, active);
            Assert.IsNotNull(mine);
            Assert.AreNotSame(active, mine, "A friend's seat must be a copy, not their saved squad.");
            mine.SetSlot(0, mine.GetSlot(0).WithVessel(VesselClassType.Serpent));
            Assert.AreSame(mine, VoyageSeatRecord.For(VoyageSeatRecord.PartyKey, active), "Reopening the page started the seat over.");
            Assert.AreEqual(VesselClassType.Serpent, VoyageLaunch.SeatHull(VoyageSeatRecord.Get(VoyageSeatRecord.PartyKey)),
                "The hull a friend's voyage reads is not the one they picked.");
            Assert.AreNotEqual(VoyageSeatRecord.PartyKey, VoyageSeatRecord.EscortKey);
        }

        [Test]
        public void NoSession_IsNotAPartyGuest() =>
            Assert.IsFalse(VoyageLaunch.IsPartyGuest, "With no session the player is their own host.");

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

        // ── The Hangar line-up (Rev 115 A) ───────────────────────────────────

        [Test]
        public void SeatState_IsYou_ThenFriends_ThenTheAi()
        {
            Assert.AreEqual(VoyageArkPage.SeatState.You, VoyageArkPage.StateOf(0, 0, 1));
            Assert.AreEqual(VoyageArkPage.SeatState.Friend, VoyageArkPage.StateOf(1, 0, 1), "The first friend takes the seat after yours.");
            Assert.AreEqual(VoyageArkPage.SeatState.Ai, VoyageArkPage.StateOf(2, 0, 1));
            Assert.AreEqual(VoyageArkPage.SeatState.Ai, VoyageArkPage.StateOf(1, 0, 0), "With no friend every other seat is the AI's.");
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
                Assert.AreEqual(VoyageSeatRecord.IsAiSeat(seat, 0, 1), VoyageArkPage.StateOf(seat, 0, 1) == VoyageArkPage.SeatState.Ai,
                    "The page's AI seats are not the ones Fill fills.");
        }

        [Test]
        public void Picker_SlidesInFromTheRight()
        {
            Assert.Greater(VoyageSeatPicker.SlideOffset(0f), 0f, "The picker must start off to the right.");
            Assert.AreEqual(0f, VoyageSeatPicker.SlideOffset(1f), 1e-4f, "The picker must end where the board draws it.");
            Assert.Greater(VoyageSeatPicker.SlideOffset(0.3f), VoyageSeatPicker.SlideOffset(0.6f));
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

        // ── The crew screen, pilot first (Rev 114 C2) ────────────────────────

        [Test]
        public void Crew_PutsWhatSailsFirst_ThenTheLocked_EachInOrder()
        {
            var offers = new List<VoyageBoard.Offer>
            {
                Offer(FactionType.Hunter, false), Offer(FactionType.Thief, true), Offer(FactionType.Pirate, false),
                Offer(FactionType.None, true),
            };
            var crew = new List<int>();
            VoyageCrewRules.Crew(offers, crew);
            CollectionAssert.AreEqual(new[] { 1, 3, 0, 2 }, crew);
        }

        [Test]
        public void OtherOpen_IsTheNextVoyageThatSails_NeverThePickedOne()
        {
            var offers = new List<VoyageBoard.Offer>
            {
                Offer(FactionType.Hunter, false), Offer(FactionType.Thief, true),
                new() { House = FactionType.None, SailLabel = "Under way" }, Offer(FactionType.None, true),
            };
            Assert.AreEqual(1, VoyageCrewRules.OtherOpen(offers, 0), "A locked pick offers the first voyage that sails.");
            Assert.AreEqual(3, VoyageCrewRules.OtherOpen(offers, 1), "A voyage under way cannot be taken instead.");
            Assert.AreEqual(-1, VoyageCrewRules.OtherOpen(new List<VoyageBoard.Offer> { Offer(FactionType.None, true) }, 0));
        }

        [Test]
        public void FirstName_IsTheFirstWord()
        {
            Assert.AreEqual("Tamako", VoyageCrewRules.FirstName("Tamako Deephook"));
            Assert.AreEqual("Volt", VoyageCrewRules.FirstName(" Volt "));
            Assert.IsNull(VoyageCrewRules.FirstName(null));
        }

        [Test]
        public void Crop_FillsTheBox_AroundTheFace()
        {
            float box = 620f / 330f;
            // A square scene in the 620 x 330 box: full width, the height cut, centred on the face.
            var r = VoyageCrewPopup.Crop(1000, 1000, box, new Rect(0.45f, 0.6f, 0.1f, 0.2f));
            Assert.AreEqual(1f, r.width, 1e-4f);
            Assert.AreEqual(1f / box, r.height, 1e-4f);
            Assert.AreEqual(0.7f, r.center.y, 1e-4f, "The cut is not centred on the face.");
            // A face at the very top keeps the cut inside the picture.
            Assert.AreEqual(1f, VoyageCrewPopup.Crop(1000, 1000, box, new Rect(0.45f, 0.9f, 0.1f, 0.1f)).yMax, 1e-4f);
            // A scene wider than the box: full height, the width cut round the face.
            var wide = VoyageCrewPopup.Crop(3000, 1000, box, new Rect(0.35f, 0.4f, 0.1f, 0.2f));
            Assert.AreEqual(1f, wide.height, 1e-4f);
            Assert.AreEqual(box / 3f, wide.width, 1e-4f);
            Assert.AreEqual(0.4f, wide.center.x, 1e-4f);
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
                         l.ArkInvite, l.ArkChangeWingmate, l.PickSaved, l.PickSavedSeat, l.CrewAsks, l.CrewQuote, l.CrewWorlds,
                         l.CrewTake, l.CrewInstead, l.CrewArk, l.CrewArkAsks, l.ArkChangeSeat, l.PickSavedHere, l.PickSeatNow,
                         l.PickSeatLine, l.ArkFriendLine })
                Assert.IsFalse(string.IsNullOrEmpty(line), "A Rev 113 line is empty in the shipped content.");
            StringAssert.Contains("{vessel}", l.ArkSailAs);
            StringAssert.Contains("{voyage}", l.PickSaved);
            StringAssert.Contains("{quote}", l.CrewQuote);
            StringAssert.Contains("{pilot}", l.CrewAsks);
            StringAssert.Contains("{name}", l.ArkFriendLine);
            StringAssert.Contains("{vessel}", l.PickSeatLine);
        }
    }
}
