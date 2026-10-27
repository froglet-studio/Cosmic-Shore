#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Making and joining squads (the Ark's Squads tab): at most four squads per player, made and joined together; a
    /// friend joins by publishing a claim, the owner seats the first claim on each open seat, and each side reads the
    /// other's public record back. Every rule here is <see cref="SquadMembership"/>'s, which is pure.
    /// </summary>
    [TestFixture]
    public class SquadMembershipTests
    {
        const string Me = "player-me", Ana = "player-ana", Bo = "player-bo";

        static SavedSquad Owned(string name, SquadSeat rogueOne = SquadSeat.Open, SquadSeat rogueTwo = SquadSeat.Ai) =>
            new SavedSquad(name,
                new SquadSlot(VesselClassType.Manta, Element.Mass).WithMember(Me, "Me"),
                new SquadSlot(VesselClassType.Dolphin, Element.Charge).WithSeat(rogueOne),
                new SquadSlot(VesselClassType.Sparrow, Element.Time).WithSeat(rogueTwo));

        static SquadSlot Loadout(VesselClassType v, Element e) => new SquadSlot(v, e, "pilot.x", "artifact.y");

        [Test]
        public void Book_HoldsAtMostFourSquads()
        {
            var book = new SquadBook();
            for (int i = 0; i < SquadBook.MaxSquads; i++) book.Add(Owned("S" + i));

            Assert.IsFalse(book.CanAdd());
            Assert.Throws<System.InvalidOperationException>(() => book.Add(Owned("fifth")));
            Assert.AreEqual(4, book.Squads.Count);
        }

        [Test]
        public void Normalize_TrimsAnOverfullRecord_AndMintsMissingOrRepeatedIds()
        {
            var book = new SquadBook();
            for (int i = 0; i < 6; i++) book.Squads.Add(new SavedSquad { Name = "S" + i, Id = i == 2 ? null : "same" });
            book.Normalize();

            Assert.AreEqual(SquadBook.MaxSquads, book.Squads.Count);
            var ids = new HashSet<string>();
            foreach (var s in book.Squads) Assert.IsTrue(ids.Add(s.Id), "every squad the player made has its own id");
        }

        [Test]
        public void TryJoin_TakesTheFirstOpenSeat_WithTheJoinersFourPicks()
        {
            var owner = Owned("Breakers");
            var book = new SquadBook();
            var refusal = SquadMembership.TryJoin(book, Ana, "Ana", Me, "Me", owner,
                Loadout(VesselClassType.Rhino, Element.Space), out var joined);

            Assert.AreEqual(SquadJoinRefusal.None, refusal);
            Assert.AreEqual(1, joined.MySeat);
            Assert.AreEqual(Me, joined.OwnerId);
            Assert.AreEqual(owner.Id, joined.Id, "a joined copy carries the owner's id");
            Assert.AreEqual(VesselClassType.Rhino, joined.RogueOne.Vessel);
            Assert.AreEqual(Ana, joined.RogueOne.MemberId);
            Assert.IsTrue(owner.RogueOne.IsOpen(), "joining writes only the joiner's own book");
        }

        [Test]
        public void TryJoin_Refuses_FullBook_OwnSquad_NoOpenSeat_AndTwice()
        {
            var owner = Owned("Breakers");
            var full = new SquadBook();
            for (int i = 0; i < SquadBook.MaxSquads; i++) full.Add(Owned("S" + i));
            Assert.AreEqual(SquadJoinRefusal.BookFull,
                SquadMembership.TryJoin(full, Ana, "Ana", Me, "Me", owner, default, out _));

            Assert.AreEqual(SquadJoinRefusal.OwnSquad,
                SquadMembership.TryJoin(new SquadBook(), Me, "Me", Me, "Me", owner, default, out _));

            Assert.AreEqual(SquadJoinRefusal.NoOpenSeat,
                SquadMembership.TryJoin(new SquadBook(), Ana, "Ana", Me, "Me", Owned("Closed", SquadSeat.Ai), default, out _));

            var book = new SquadBook();
            SquadMembership.TryJoin(book, Ana, "Ana", Me, "Me", owner, default, out _);
            Assert.AreEqual(SquadJoinRefusal.AlreadyIn,
                SquadMembership.TryJoin(book, Ana, "Ana", Me, "Me", owner, default, out _));
        }

        [Test]
        public void Reconcile_SeatsTheFirstClaim_AndRefusesTheSecondOnTheSameSeat()
        {
            var mine = Owned("Breakers");
            var anaBook = new SquadBook();
            SquadMembership.TryJoin(anaBook, Ana, "Ana", Me, "Me", mine, Loadout(VesselClassType.Rhino, Element.Space), out _);
            var boBook = new SquadBook();
            SquadMembership.TryJoin(boBook, Bo, "Bo", Me, "Me", mine, Loadout(VesselClassType.Urchin, Element.Time), out _);

            var records = new List<(string, SquadPublicRecord)>
            {
                (Ana, SquadMembership.BuildPublicRecord(anaBook, "Ana")),
                (Bo, SquadMembership.BuildPublicRecord(boBook, "Bo")),
            };
            Assert.IsTrue(SquadMembership.ReconcileOwned(mine, Me, records));

            Assert.AreEqual(Ana, mine.RogueOne.MemberId);
            Assert.AreEqual("Ana", mine.RogueOne.MemberName);
            Assert.AreEqual(VesselClassType.Rhino, mine.RogueOne.Vessel, "a seated friend flies their own picks");
            Assert.AreEqual(SquadSeat.Ai, mine.RogueTwo.Seat, "an AI seat is never handed to a claim");
            Assert.IsFalse(SquadMembership.ReconcileOwned(mine, Me, records), "reconciling twice changes nothing");
        }

        [Test]
        public void Reconcile_OpensTheSeatAgain_WhenTheFriendLeft_ButKeepsAFriendWhoseRecordWasNotRead()
        {
            var mine = Owned("Breakers");
            mine.RogueOne = mine.RogueOne.WithMember(Ana, "Ana");

            Assert.IsFalse(SquadMembership.ReconcileOwned(mine, Me, new List<(string, SquadPublicRecord)>()),
                "an unread record is not a departure");
            Assert.AreEqual(Ana, mine.RogueOne.MemberId);

            var left = new List<(string, SquadPublicRecord)> { (Ana, new SquadPublicRecord { PlayerName = "Ana" }) };
            Assert.IsTrue(SquadMembership.ReconcileOwned(mine, Me, left));
            Assert.IsTrue(mine.RogueOne.IsOpen());
            Assert.IsNull(mine.RogueOne.MemberId);
        }

        [Test]
        public void RefreshJoined_ReadsPendingThenSeated_SeatLost_AndDisbanded()
        {
            var owner = Owned("Breakers");
            var book = new SquadBook();
            SquadMembership.TryJoin(book, Ana, "Ana", Me, "Me", owner, Loadout(VesselClassType.Rhino, Element.Space), out var joined);

            var ownerBook = new SquadBook();
            ownerBook.Add(owner);
            Assert.AreEqual(SquadJoinState.Pending,
                SquadMembership.RefreshJoined(joined, Ana, SquadMembership.BuildPublicRecord(ownerBook, "Me")));

            SquadMembership.ReconcileOwned(owner, Me,
                new List<(string, SquadPublicRecord)> { (Ana, SquadMembership.BuildPublicRecord(book, "Ana")) });
            Assert.AreEqual(SquadJoinState.Seated,
                SquadMembership.RefreshJoined(joined, Ana, SquadMembership.BuildPublicRecord(ownerBook, "Me")));
            Assert.AreEqual(VesselClassType.Rhino, joined.RogueOne.Vessel, "the joiner's own picks win in their seat");

            owner.RogueOne = owner.RogueOne.WithSeat(SquadSeat.Ai);
            Assert.AreEqual(SquadJoinState.SeatLost,
                SquadMembership.RefreshJoined(joined, Ana, SquadMembership.BuildPublicRecord(ownerBook, "Me")));

            Assert.AreEqual(SquadJoinState.Disbanded, SquadMembership.RefreshJoined(joined, Ana, new SquadPublicRecord()));
            Assert.AreEqual(SquadJoinState.Unknown, SquadMembership.RefreshJoined(joined, Ana, null));
        }

        [Test]
        public void PublicRecord_RoundTripsThroughJson_WithSeatsAndClaims()
        {
            var book = new SquadBook();
            book.Add(Owned("Mine"));
            SquadMembership.TryJoin(book, Me, "Me", Ana, "Ana", Owned("Ana's"), Loadout(VesselClassType.Rhino, Element.Space), out _);

            var record = SquadMembership.BuildPublicRecord(book, "Me");
            var back = JsonConvert.DeserializeObject<SquadPublicRecord>(JsonConvert.SerializeObject(record));

            Assert.AreEqual(1, back.Squads.Count, "only the squads the player made are published as squads");
            Assert.AreEqual(SquadSeat.Open, back.Squads[0].RogueOne.Seat);
            Assert.AreEqual(1, back.Claims.Count);
            Assert.AreEqual(Ana, back.Claims[0].OwnerId);
            Assert.AreEqual(VesselClassType.Rhino, back.Claims[0].Loadout.Vessel);
        }

        [Test]
        public void Domain_IsTheOwners_AndAnOldRecordReadsAsJade()
        {
            var owner = Owned("Breakers");
            owner.Domain = Domains.Ruby;
            var book = new SquadBook();
            SquadMembership.TryJoin(book, Ana, "Ana", Me, "Me", owner, default, out var joined);
            Assert.AreEqual(Domains.Ruby, joined.Domain, "a joined copy takes the owner's domain");

            var ownerBook = new SquadBook();
            ownerBook.Add(owner);
            owner.Domain = Domains.Gold;
            SquadMembership.RefreshJoined(joined, Ana, SquadMembership.BuildPublicRecord(ownerBook, "Me"));
            Assert.AreEqual(Domains.Gold, joined.Domain, "the owner's change reaches the joiner");

            Assert.AreEqual(Domains.Jade, JsonConvert.DeserializeObject<SavedSquad>("{\"Name\":\"Old\"}").Domain);
        }

        [Test]
        public void SquadSlot_SavedBeforeSeatsExisted_ReadsAsAnAiSeat()
        {
            var slot = JsonConvert.DeserializeObject<SquadSlot>("{\"Vessel\":1,\"Element\":2}");
            Assert.AreEqual(SquadSeat.Ai, slot.Seat);
            Assert.IsFalse(slot.IsPlayer());
        }
    }
}
#endif
