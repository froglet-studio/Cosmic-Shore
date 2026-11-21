#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// PartyRoster tests - the party panel's seating.
    ///
    /// WHY THIS MATTERS:
    /// The bug this replaced was invisible on any single machine. Every device seated ITSELF
    /// first (PartyMemberService.SeedLocalPlayer puts the local player at index 0 everywhere)
    /// and appended the rest in session-enumeration order, so four players in one party saw
    /// four different seatings and nobody could say "third from the left" and be understood.
    /// The fix is only correct if the SAME roster comes out no matter whose machine builds it,
    /// which is exactly what a single-machine play test cannot show - so it is asserted here.
    /// </summary>
    [TestFixture]
    public class PartyRosterTests
    {
        const ulong HOST = 0;

        static PartyPlayerData P(string id, string name = null, int avatar = 0) =>
            new(id, name ?? id, avatar);

        // Fake network: UGS id -> replicated owner client id. Anything absent is "no Player
        // object on this machine yet", which is what an in-flight join looks like.
        static System.Func<string, ulong> Net(params (string id, ulong clientId)[] rows)
        {
            var map = new Dictionary<string, ulong>();
            foreach (var r in rows) map[r.id] = r.clientId;
            return id => map.TryGetValue(id, out var c) ? c : PartyRoster.UnknownClientId;
        }

        static List<string> Order(List<PartyRoster.Seat> seats)
        {
            var ids = new List<string>();
            foreach (var s in seats) ids.Add(s.PlayerId);
            return ids;
        }

        #region Host first, clients in join order

        [Test]
        public void Host_TakesFirstSeat_EvenWhenLocalPlayerIsAClient()
        {
            var result = new List<PartyRoster.Seat>();

            // Seen from client "c2": its own PartyMembers list seeds ITSELF first.
            var members = new List<PartyPlayerData> { P("c2"), P("host"), P("c1") };

            PartyRoster.Build(members, P("c2"), HOST,
                Net(("host", 0), ("c1", 1), ("c2", 2)), result);

            Assert.AreEqual(new[] { "host", "c1", "c2" }, Order(result).ToArray());
            Assert.IsTrue(result[0].IsHost, "First seat must be flagged as the host.");
            Assert.IsFalse(result[1].IsHost);
            Assert.IsFalse(result[2].IsHost);
        }

        [Test]
        public void Clients_FollowInJoinOrder()
        {
            var result = new List<PartyRoster.Seat>();
            var members = new List<PartyPlayerData> { P("host"), P("c3"), P("c1"), P("c2") };

            // Netcode assigns owner client ids in CONNECTION order, so ascending id IS join order.
            PartyRoster.Build(members, P("host"), HOST,
                Net(("host", 0), ("c1", 1), ("c2", 2), ("c3", 3)), result);

            Assert.AreEqual(new[] { "host", "c1", "c2", "c3" }, Order(result).ToArray());
        }

        [Test]
        public void EveryDevice_BuildsTheSameSeating()
        {
            var net = Net(("host", 0), ("c1", 1), ("c2", 2));

            // Each device's own PartyMembers list: local seeded first, remotes in whatever
            // order that machine's session enumeration happened to produce.
            var asHost = new List<PartyRoster.Seat>();
            PartyRoster.Build(new List<PartyPlayerData> { P("host"), P("c2"), P("c1") },
                P("host"), HOST, net, asHost);

            var asC1 = new List<PartyRoster.Seat>();
            PartyRoster.Build(new List<PartyPlayerData> { P("c1"), P("host"), P("c2") },
                P("c1"), HOST, net, asC1);

            var asC2 = new List<PartyRoster.Seat>();
            PartyRoster.Build(new List<PartyPlayerData> { P("c2"), P("c1"), P("host") },
                P("c2"), HOST, net, asC2);

            Assert.AreEqual(Order(asHost).ToArray(), Order(asC1).ToArray());
            Assert.AreEqual(Order(asHost).ToArray(), Order(asC2).ToArray());
        }

        #endregion

        #region Degrading while the spawn chain is in flight

        [Test]
        public void MembersWithNoClientIdYet_SortLast_AndDeterministically()
        {
            var result = new List<PartyRoster.Seat>();
            var members = new List<PartyPlayerData> { P("zulu"), P("host"), P("alpha") };

            // "alpha" and "zulu" are in the party but their Player objects have not spawned.
            PartyRoster.Build(members, P("host"), HOST, Net(("host", 0)), result);

            Assert.AreEqual(new[] { "host", "alpha", "zulu" }, Order(result).ToArray(),
                "Unresolved members sort behind resolved ones, and tie-break on ordinal id " +
                "so every device still agrees while the spawn chain is in flight.");
        }

        [Test]
        public void NoNetworkAtAll_StillProducesADeterministicOrder()
        {
            var a = new List<PartyRoster.Seat>();
            var b = new List<PartyRoster.Seat>();

            PartyRoster.Build(new List<PartyPlayerData> { P("m"), P("a"), P("z") },
                P("m"), PartyRoster.UnknownClientId, Net(), a);
            PartyRoster.Build(new List<PartyPlayerData> { P("z"), P("m"), P("a") },
                P("a"), PartyRoster.UnknownClientId, Net(), b);

            Assert.AreEqual(new[] { "a", "m", "z" }, Order(a).ToArray());
            Assert.AreEqual(Order(a).ToArray(), Order(b).ToArray());
        }

        [Test]
        public void NoHostClientId_MarksNobodyAsHost()
        {
            var result = new List<PartyRoster.Seat>();
            PartyRoster.Build(new List<PartyPlayerData> { P("a"), P("b") },
                P("a"), PartyRoster.UnknownClientId, Net(), result);

            foreach (var seat in result)
                Assert.IsFalse(seat.IsHost,
                    "With no live host id, no seat may claim the host badge - a guess would be " +
                    "wrong on every device that guessed differently.");
        }

        #endregion

        #region Local player

        [Test]
        public void LocalPlayer_IsSeated_EvenWhenMissingFromTheMemberList()
        {
            var result = new List<PartyRoster.Seat>();

            // PartyMembers has not caught up with the local seed yet.
            PartyRoster.Build(new List<PartyPlayerData> { P("host") }, P("me"), HOST,
                Net(("host", 0), ("me", 1)), result);

            Assert.AreEqual(new[] { "host", "me" }, Order(result).ToArray());
            Assert.IsTrue(result[1].IsLocal);
            Assert.IsFalse(result[0].IsLocal);
        }

        [Test]
        public void LocalIdentity_WinsOverTheCopyInTheMemberList()
        {
            var result = new List<PartyRoster.Seat>();

            // The list still carries the pre-cloud-profile placeholder; the live local fields
            // have already resolved. The fresher one must be what is drawn.
            var members = new List<PartyPlayerData> { P("me", "Pilot0000", 0), P("host") };

            PartyRoster.Build(members, P("me", "Wildcat", 7), HOST,
                Net(("host", 0), ("me", 1)), result);

            Assert.AreEqual("Wildcat", result[1].Member.DisplayName);
            Assert.AreEqual(7, result[1].Member.AvatarId);
        }

        #endregion

        #region Hygiene

        [Test]
        public void DuplicateRows_AreSeatedOnce()
        {
            var result = new List<PartyRoster.Seat>();
            var members = new List<PartyPlayerData> { P("host"), P("c1"), P("c1") };

            PartyRoster.Build(members, P("host"), HOST, Net(("host", 0), ("c1", 1)), result);

            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void EmptyPlayerIds_AreSkipped()
        {
            var result = new List<PartyRoster.Seat>();
            var members = new List<PartyPlayerData> { P("host"), new(null, "ghost", 0), new("", "ghost", 0) };

            PartyRoster.Build(members, P("host"), HOST, Net(("host", 0)), result);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("host", result[0].PlayerId);
        }

        [Test]
        public void Build_ClearsTheResultList()
        {
            var result = new List<PartyRoster.Seat> { new(P("stale"), false, false) };

            PartyRoster.Build(new List<PartyPlayerData>(), P("me"), HOST, Net(("me", 0)), result);

            Assert.AreEqual(new[] { "me" }, Order(result).ToArray());
        }

        [Test]
        public void NullMemberList_StillSeatsTheLocalPlayer()
        {
            var result = new List<PartyRoster.Seat>();
            PartyRoster.Build(null, P("me"), HOST, Net(("me", 0)), result);

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].IsLocal);
        }

        #endregion
    }
}
#endif
