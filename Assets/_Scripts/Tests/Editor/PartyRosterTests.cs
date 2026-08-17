#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for <see cref="IPartyRoster"/> as implemented by
    /// <see cref="HostConnectionDataSO"/> - the LOCAL, authoritative answer to
    /// "how big is my party" and "is this player in it".
    ///
    /// <para>
    /// WHY THIS MATTERS. A three-player party rendered three different sizes on
    /// three screens simultaneously (2/4, 1/4, 3/4) because
    /// <c>FriendsListPanel</c> labelled its own party members from each peer's
    /// self-published <c>partyCount</c> presence property instead of the shared
    /// roster. That is a wrong-source-of-truth bug, not a latency bug: with N
    /// members there are N independently-published scalars and nothing that
    /// reconciles them, so no poll cadence could ever have fixed it. It broke
    /// the locked "session is authoritative over presence" invariant and
    /// <c>PartySystem/ARCHITECTURE.md</c> exit criterion 3.
    /// </para>
    ///
    /// <para>
    /// These tests pin the properties that make the local answer trustworthy:
    /// it counts the roster LIST (never a field on its items, which are
    /// identity-only and carry zeros), it includes the local player, and it
    /// answers membership by PlayerId regardless of how stale the rest of a
    /// row's data is.
    /// </para>
    /// </summary>
    [TestFixture]
    public class PartyRosterTests
    {
        HostConnectionDataSO _data;
        ScriptableListPartyPlayerData _partyMembers;
        IPartyRoster _roster;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<HostConnectionDataSO>();
            _partyMembers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
            _data.PartyMembers = _partyMembers;
            _data.LocalPlayerId = "local";
            _roster = _data;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_partyMembers);
            Object.DestroyImmediate(_data);
        }

        // ─────────────────────────────────────────────────────────────────────
        // MemberCount
        // ─────────────────────────────────────────────────────────────────────

        #region MemberCount

        [Test]
        public void MemberCount_UnwiredList_ReturnsZero()
        {
            _data.PartyMembers = null;
            Assert.AreEqual(0, _roster.MemberCount,
                "An unwired roster must report 0, not throw.");
        }

        [Test]
        public void MemberCount_EmptyList_ReturnsZero()
        {
            Assert.AreEqual(0, _roster.MemberCount);
        }

        [Test]
        public void MemberCount_IncludesLocalPlayer()
        {
            _partyMembers.Add(new PartyPlayerData("local", "LocalPilot", 1));

            Assert.AreEqual(1, _roster.MemberCount,
                "MemberCount is what an 'IN YOUR PARTY n/4' label renders, so it " +
                "must include the local player - unlike RemotePartyMemberCount.");
        }

        [Test]
        public void MemberCount_ThreePlayerParty_ReturnsThree()
        {
            _partyMembers.Add(new PartyPlayerData("local", "A", 1));
            _partyMembers.Add(new PartyPlayerData("b", "B", 2));
            _partyMembers.Add(new PartyPlayerData("c", "C", 3));

            Assert.AreEqual(3, _roster.MemberCount,
                "The A/B/C repro: every member of one party must read the same " +
                "size from their own local roster.");
        }

        /// <summary>
        /// The trap this whole commit exists to close. Entries in PartyMembers
        /// come from <c>PartyMemberService.ReadMemberData</c>, which builds them
        /// from the party SESSION's player list via the identity-only
        /// constructor - so every one of them reports
        /// <c>AdvertisedPartyMemberCount == 0</c>. A "fix" that pointed the label
        /// at a member's own advertised field would render 0/4. MemberCount
        /// counts the list instead, so it is immune.
        /// </summary>
        [Test]
        public void MemberCount_IsIndependentOfAdvertisedFieldsOnItems()
        {
            _partyMembers.Add(new PartyPlayerData("local", "A", 1));
            _partyMembers.Add(new PartyPlayerData("b", "B", 2));

            Assert.AreEqual(0, _partyMembers[0].AdvertisedPartyMemberCount,
                "Identity-only rows carry zeroed advertised fields by design.");
            Assert.AreEqual(2, _roster.MemberCount,
                "MemberCount must count the list, never read a field off its items.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // Contains
        // ─────────────────────────────────────────────────────────────────────

        #region Contains

        [Test]
        public void Contains_UnwiredList_ReturnsFalse()
        {
            _data.PartyMembers = null;
            Assert.IsFalse(_roster.Contains("anyone"));
        }

        [Test]
        public void Contains_NullOrEmptyId_ReturnsFalse()
        {
            _partyMembers.Add(new PartyPlayerData("b", "B", 2));

            Assert.IsFalse(_roster.Contains(null));
            Assert.IsFalse(_roster.Contains(string.Empty),
                "An empty id must never match - a row with no id is not a member.");
        }

        [Test]
        public void Contains_MemberPresent_ReturnsTrue()
        {
            _partyMembers.Add(new PartyPlayerData("b", "B", 2));
            Assert.IsTrue(_roster.Contains("b"));
        }

        [Test]
        public void Contains_MemberAbsent_ReturnsFalse()
        {
            _partyMembers.Add(new PartyPlayerData("b", "B", 2));
            Assert.IsFalse(_roster.Contains("c"));
        }

        [Test]
        public void Contains_LocalPlayer_ReturnsTrue()
        {
            _partyMembers.Add(new PartyPlayerData("local", "LocalPilot", 1));
            Assert.IsTrue(_roster.Contains("local"),
                "The local player is a member of their own party.");
        }

        [Test]
        public void Contains_MatchesByIdRegardlessOfStaleRowData()
        {
            _partyMembers.Add(new PartyPlayerData("b", "OldName", 2));

            Assert.IsTrue(_roster.Contains("b"),
                "Membership is keyed on PlayerId only, so a mid-party rename or a " +
                "stale avatar can never drop someone out of the roster.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // MaxSlots / HasOpenSlots / IsHost
        // ─────────────────────────────────────────────────────────────────────

        #region MaxSlots, HasOpenSlots, IsHost

        [Test]
        public void MaxSlots_DefaultIsFour()
        {
            Assert.AreEqual(4, _roster.MaxSlots);
        }

        [Test]
        public void MaxSlots_MatchesMaxPartySlots()
        {
            Assert.AreEqual(_data.MaxPartySlots, _roster.MaxSlots,
                "The interface must not become a second, drifting copy of the limit.");
        }

        [Test]
        public void HasOpenSlots_FullParty_ReturnsFalse()
        {
            for (int i = 0; i < 4; i++)
                _partyMembers.Add(new PartyPlayerData($"p{i}", $"Pilot{i}", i));

            Assert.IsFalse(_roster.HasOpenSlots);
            Assert.AreEqual(_roster.MaxSlots, _roster.MemberCount,
                "A full party reads exactly n/n - the 4/4 an 'IN YOUR PARTY' label shows.");
        }

        [Test]
        public void HasOpenSlots_MatchesConcreteProperty()
        {
            _partyMembers.Add(new PartyPlayerData("local", "A", 1));
            Assert.AreEqual(_data.HasOpenSlots, _roster.HasOpenSlots);
        }

        [Test]
        public void IsHost_MirrorsIsPartyHost()
        {
            _data.IsPartyHost = false;
            Assert.IsFalse(_roster.IsHost);

            _data.IsPartyHost = true;
            Assert.IsTrue(_roster.IsHost,
                "IsHost gates the kick affordance, so it must track the party-session " +
                "host flag - not the presence-lobby host flag.");
        }

        [Test]
        public void IsHost_IsNotPresenceLobbyHost()
        {
            _data.IsPresenceLobbyHost = true;
            _data.IsPartyHost = false;

            Assert.IsFalse(_roster.IsHost,
                "Owning the global discovery lobby confers no party authority - " +
                "conflating the two would hand kick rights to whoever signed in first.");
        }

        #endregion
    }
}
#endif
