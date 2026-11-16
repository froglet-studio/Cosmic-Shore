using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A voyage in a party (Docs/Voyages/PARTY.md): the seats the host hands out, a friend's own seat moved into the one
    /// they were given, the party's Crystal Run count travelling from host to friend, and the course surviving the wire.
    /// </summary>
    public class VoyagePartyTests
    {
        static SavedSquad Squad(int mySeat)
        {
            var squad = new SavedSquad("Squad", new SquadSlot(VesselClassType.Manta, Element.Space, "p.a"),
                new SquadSlot(VesselClassType.Dolphin, Element.Charge, "p.b"), new SquadSlot(VesselClassType.Sparrow, Element.Time, "p.c"));
            squad.MySeat = mySeat;
            return squad;
        }

        [Test]
        public void SeatClients_HostThenFriendsInClientOrder_ThenAi()
        {
            var seats = VoyageRules.SeatClients(0, 0, new List<long> { 7, 3 });
            CollectionAssert.AreEqual(new long[] { 0, 3, 7 }, seats, "Friends take the seats after the host's, lowest client id first.");

            seats = VoyageRules.SeatClients(1, 0, new List<long> { 5 });
            CollectionAssert.AreEqual(new long[] { 5, 0, -1 }, seats, "With the host in seat 2 the friend takes seat 1, an AI seat 3.");

            seats = VoyageRules.SeatClients(2, 0, new List<long>());
            CollectionAssert.AreEqual(new long[] { -1, -1, 0 }, seats, "Solo: every other seat is an AI's.");
        }

        [Test]
        public void SeatClients_MatchFriendAt_SoTheLobbyAndTheVoyageAgree()
        {
            for (int mySeat = 0; mySeat < SavedSquad.SlotCount; mySeat++)
            for (int friends = 0; friends <= 3; friends++)
            {
                var ids = new List<long>();
                for (int i = 0; i < friends; i++) ids.Add(10 + i);
                var seats = VoyageRules.SeatClients(mySeat, 1, ids);
                for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
                {
                    int k = VoyageRules.FriendAt(seat, mySeat, friends);
                    long expected = seat == mySeat ? 1 : k >= 0 ? 10 + k : -1;
                    Assert.AreEqual(expected, seats[seat], $"seat {seat}, host in {mySeat}, {friends} friends");
                }
            }
        }

        [Test]
        public void WithSeat_MovesTheFriendsOwnSlotIntoTheGivenSeat()
        {
            var own = Squad(0);
            var moved = VoyageRules.WithSeat(own, 2);
            Assert.AreEqual(2, moved.MySeat);
            Assert.AreEqual(VesselClassType.Manta, moved.GetSlot(2).Vessel, "The friend's own pick must fly the seat they were given.");
            Assert.AreEqual("p.a", moved.GetSlot(2).PilotId);
            Assert.AreEqual(0, own.MySeat, "The friend's record must not change.");
            Assert.AreEqual(VesselClassType.Manta, own.GetSlot(0).Vessel);
            Assert.AreSame(own, VoyageRules.WithSeat(own, -1), "No seat in the host's list: the record sails as it is.");
            Assert.AreSame(own, VoyageRules.WithSeat(own, 0), "Already in that seat: nothing to move.");
        }

        [Test]
        public void Tally_SnapshotAndReplace_CarryTheHostsCount()
        {
            var host = new VoyageRaidTally();
            host.Add("Host", Element.Space);
            host.Add("Friend", Element.Charge);
            host.Add("Friend", Element.Charge);
            host.Kill("Friend", nearArk: true);
            host.SeatOf("Friend").Vessel = (int)VesselClassType.Dolphin;

            var course = new VoyageCourse { Kind = VoyageCourseKind.Tally, Tally = host.Snapshot() };
            var wire = JsonUtility.FromJson<VoyageCourse>(JsonUtility.ToJson(course));

            var friend = new VoyageRaidTally();
            friend.Add("Friend", Element.Time);   // counted here before the host's count arrived
            friend.Replace(wire.Tally);
            Assert.AreEqual(3, friend.Total, "The friend's count must be the host's.");
            Assert.AreEqual(2, friend.Of(Element.Charge));
            Assert.AreEqual(0, friend.Of(Element.Time), "The friend's own provisional count is replaced, not added.");
            Assert.AreEqual(1, friend.SeatOf("Friend").Guarded);
            Assert.AreEqual((int)VesselClassType.Dolphin, friend.SeatOf("Friend").Vessel);

            host.Add("Host", Element.Mass);
            Assert.AreEqual(3, friend.Total, "The snapshot must be a copy: the host's later count does not reach it until sent.");
        }

        [Test]
        public void Replace_KeepsWhatThisMachineCarries()
        {
            var host = new VoyageRaidTally();
            host.Add("Host", Element.Space);
            host.Add("Friend", Element.Charge);

            var friend = new VoyageRaidTally();
            friend.Carry("Friend", Element.Time);
            friend.Carry("Friend", Element.Time);
            friend.Replace(JsonUtility.FromJson<VoyageCourse>(JsonUtility.ToJson(
                new VoyageCourse { Kind = VoyageCourseKind.Tally, Tally = host.Snapshot() })).Tally);
            Assert.AreEqual(2, friend.Total, "The host's banked count arrives.");
            Assert.AreEqual(2, friend.CarriedBy("Friend"), "What the friend carries is theirs until they bank it.");

            Assert.AreEqual(2, friend.Bank("Friend"));
            Assert.AreEqual(0, friend.CarriedBy("Friend"));
            Assert.AreEqual(2, friend.Of(Element.Time), "Banked here at once, then reported to the host.");
        }

        [Test]
        public void Course_RoundTripsThroughJson_WithSeatsHomeAndOutcome()
        {
            var course = new VoyageCourse { Kind = VoyageCourseKind.EscortSail, Difficulty = 4, Seed = 12345, LegSerial = 1 };
            course.SeatClients.AddRange(new long[] { 0, 3, -1 });
            course.SetHome(1f, 2f, 3f, 0f, 0.7071068f, 0f, 0.7071068f);
            var wire = JsonUtility.FromJson<VoyageCourse>(JsonUtility.ToJson(course));
            Assert.AreEqual(VoyageCourseKind.EscortSail, wire.Kind);
            Assert.AreEqual(4, wire.Difficulty);
            Assert.AreEqual(12345, wire.Seed);
            Assert.IsTrue(wire.HasHome);
            Assert.AreEqual(2f, wire.HomeY);
            Assert.AreEqual(1, wire.SeatOf(3));
            Assert.AreEqual(-1, wire.ClientAt(2));
            Assert.AreEqual(-1, wire.SeatOf(99));

            var end = JsonUtility.FromJson<VoyageCourse>(JsonUtility.ToJson(new VoyageCourse
                { Kind = VoyageCourseKind.End, Outcome = VoyageCourseEnd.Docked }));
            Assert.AreEqual(VoyageCourseEnd.Docked, end.Outcome);
        }

        [Test]
        public void Report_RoundTripsThroughJson()
        {
            var report = new VoyagePartyReport
            {
                Kind = VoyagePartyReport.ReportKind.Seat, PlayerName = "Friend", SlotVessel = (int)VesselClassType.Serpent,
                SlotElement = (int)Element.Mass, PilotId = "talon#2", PilotName = "Tamako", ArtifactId = "a.1",
            };
            var wire = JsonUtility.FromJson<VoyagePartyReport>(JsonUtility.ToJson(report));
            Assert.AreEqual(VoyagePartyReport.ReportKind.Seat, wire.Kind);
            Assert.AreEqual((int)VesselClassType.Serpent, wire.SlotVessel);
            Assert.AreEqual((int)Element.Mass, wire.SlotElement);
            Assert.AreEqual("Tamako", wire.PilotName);
        }
    }
}
