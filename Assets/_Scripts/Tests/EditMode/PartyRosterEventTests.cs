#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for the <c>OnPartyRosterChanged</c> channel - the coalesced
    /// "the party roster moved, repaint" signal.
    ///
    /// <para>
    /// WHY THIS MATTERS. Three consumers (FriendsListPanel, ArcadeLobbyList,
    /// FriendsInitializer) each subscribed to OnPartyMemberJoined, Left AND
    /// Kicked and answered all three with the same full repopulate. A sync that
    /// moved three members therefore rebuilt every panel three times, and each
    /// rebuild ran against a roster that was still mid-mutation. This channel
    /// replaces that with one raise per settled mutation.
    /// </para>
    ///
    /// <para>
    /// Two properties make it worth having, and both are pinned here:
    /// <b>coalescing</b> (N member moves cost exactly 1 raise) and the
    /// <b>change gate</b> (a no-op costs 0). Without the gate the signal would
    /// fire on every steady-state poll tick, which is the per-tick churn it
    /// exists to remove; without coalescing it would be a rename of the events
    /// it replaced.
    /// </para>
    /// </summary>
    [TestFixture]
    public class PartyRosterEventTests
    {
        HostConnectionDataSO _data;
        ScriptableListPartyPlayerData _partyMembers;
        ScriptableEventNoParam _rosterChanged;
        ScriptableEventPartyPlayerData _memberLeft;
        ScriptableEventPartyPlayerData _memberJoined;
        ScriptableEventPartyPlayerData _memberKicked;

        SoapPartyEventBus _bus;
        PartyMemberService _service;

        int _rosterRaises;
        int _leftRaises;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<HostConnectionDataSO>();
            _partyMembers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
            _rosterChanged = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            _memberLeft = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
            _memberJoined = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
            _memberKicked = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();

            _data.PartyMembers = _partyMembers;
            _data.OnPartyRosterChanged = _rosterChanged;
            _data.OnPartyMemberLeft = _memberLeft;
            _data.OnPartyMemberJoined = _memberJoined;
            _data.OnPartyMemberKicked = _memberKicked;
            _data.LocalPlayerId = "local";
            _data.LocalDisplayName = "LocalPilot";

            _bus = new SoapPartyEventBus(_data);
            _service = new PartyMemberService(_data, _bus);

            _rosterRaises = 0;
            _leftRaises = 0;
            _rosterChanged.OnRaised += CountRoster;
            _memberLeft.OnRaised += CountLeft;
        }

        [TearDown]
        public void TearDown()
        {
            _rosterChanged.OnRaised -= CountRoster;
            _memberLeft.OnRaised -= CountLeft;

            Object.DestroyImmediate(_memberKicked);
            Object.DestroyImmediate(_memberJoined);
            Object.DestroyImmediate(_memberLeft);
            Object.DestroyImmediate(_rosterChanged);
            Object.DestroyImmediate(_partyMembers);
            Object.DestroyImmediate(_data);
        }

        void CountRoster() => _rosterRaises++;
        void CountLeft(PartyPlayerData _) => _leftRaises++;

        // ─────────────────────────────────────────────────────────────────────
        // Coalescing - the reason the channel exists
        // ─────────────────────────────────────────────────────────────────────

        #region Coalescing

        [Test]
        public void ClearWithEvents_ThreeRemotes_RaisesRosterOnce_ButLeftThreeTimes()
        {
            _partyMembers.Add(new PartyPlayerData("local", "LocalPilot", 1));
            _partyMembers.Add(new PartyPlayerData("r1", "Remote1", 2));
            _partyMembers.Add(new PartyPlayerData("r2", "Remote2", 3));
            _partyMembers.Add(new PartyPlayerData("r3", "Remote3", 4));

            _service.ClearWithEvents("local");

            Assert.AreEqual(3, _leftRaises,
                "Per-member events still fire once per member - consumers that need " +
                "to know WHO moved depend on them.");
            Assert.AreEqual(1, _rosterRaises,
                "The repaint signal must fire ONCE for the whole operation. This is the " +
                "entire point of the channel: three members moving used to cost every " +
                "panel three full rebuilds.");
        }

        [Test]
        public void ClearWithEvents_RaisesAfterRosterHasSettled()
        {
            _partyMembers.Add(new PartyPlayerData("local", "LocalPilot", 1));
            _partyMembers.Add(new PartyPlayerData("r1", "Remote1", 2));
            _partyMembers.Add(new PartyPlayerData("r2", "Remote2", 3));

            int countObservedByListener = -1;
            void Observe() => countObservedByListener = _partyMembers.Count;
            _rosterChanged.OnRaised += Observe;

            try
            {
                _service.ClearWithEvents("local");
            }
            finally
            {
                _rosterChanged.OnRaised -= Observe;
            }

            Assert.AreEqual(1, countObservedByListener,
                "A listener must observe the FINAL roster, never a half-applied one - " +
                "that is why the raise sits after the loop rather than inside it.");
        }

        [Test]
        public void ClearWithEvents_KeepsLocalPlayer()
        {
            _partyMembers.Add(new PartyPlayerData("local", "LocalPilot", 1));
            _partyMembers.Add(new PartyPlayerData("r1", "Remote1", 2));

            _service.ClearWithEvents("local");

            Assert.AreEqual(1, _partyMembers.Count);
            Assert.AreEqual("local", _partyMembers[0].PlayerId);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // Change gate - a no-op must cost nothing
        // ─────────────────────────────────────────────────────────────────────

        #region Change Gate

        [Test]
        public void ClearWithEvents_NoRemoteMembers_DoesNotRaise()
        {
            _partyMembers.Add(new PartyPlayerData("local", "LocalPilot", 1));

            _service.ClearWithEvents("local");

            Assert.AreEqual(0, _rosterRaises,
                "Nothing moved, so nothing should repaint.");
        }

        [Test]
        public void ClearSilent_EmptyRoster_DoesNotRaise()
        {
            _service.ClearSilent();

            Assert.AreEqual(0, _rosterRaises);
        }

        [Test]
        public void ClearSilent_PopulatedRoster_RaisesOnce_AndNoLeftEvents()
        {
            _partyMembers.Add(new PartyPlayerData("local", "LocalPilot", 1));
            _partyMembers.Add(new PartyPlayerData("r1", "Remote1", 2));

            _service.ClearSilent();

            Assert.AreEqual(1, _rosterRaises,
                "\"Silent\" suppresses the per-member Left events, which carry side " +
                "effects - it does not mean the UI can keep rendering a stale count.");
            Assert.AreEqual(0, _leftRaises);
        }

        [Test]
        public void SeedLocalPlayer_IntoEmptyRoster_RaisesOnce()
        {
            _service.SeedLocalPlayer(clearFirst: true);

            Assert.AreEqual(1, _partyMembers.Count);
            Assert.AreEqual(1, _rosterRaises);
        }

        [Test]
        public void SeedLocalPlayer_AlreadySeeded_NoClear_DoesNotRaise()
        {
            _service.SeedLocalPlayer(clearFirst: true);
            _rosterRaises = 0;

            _service.SeedLocalPlayer(clearFirst: false);

            Assert.AreEqual(1, _partyMembers.Count,
                "Seeding is idempotent - a second pass must not duplicate the local row.");
            Assert.AreEqual(0, _rosterRaises,
                "The idempotent re-seed changed nothing, so it must not repaint. This is " +
                "the presence-reconnect path, which runs on every lobby rejoin.");
        }

        [Test]
        public void SeedLocalPlayer_NoLocalId_EmptyRoster_DoesNotRaise()
        {
            _data.LocalPlayerId = string.Empty;

            _service.SeedLocalPlayer(clearFirst: true);

            Assert.AreEqual(0, _rosterRaises,
                "No id to seed and nothing to clear - the early return must not raise.");
        }

        [Test]
        public void SeedLocalPlayer_NoLocalId_ButClearsPopulatedRoster_Raises()
        {
            _partyMembers.Add(new PartyPlayerData("stale", "Stale", 1));
            _data.LocalPlayerId = string.Empty;

            _service.SeedLocalPlayer(clearFirst: true);

            Assert.AreEqual(0, _partyMembers.Count);
            Assert.AreEqual(1, _rosterRaises,
                "The early-return path still wiped the roster, so it still has to repaint.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // The kick path, which mutates the roster without PartyMemberService
        // ─────────────────────────────────────────────────────────────────────

        #region RemovePartyMember

        [Test]
        public void RemovePartyMember_ExistingMember_RaisesRosterChanged()
        {
            _partyMembers.Add(new PartyPlayerData("local", "LocalPilot", 1));
            _partyMembers.Add(new PartyPlayerData("victim", "Victim", 2));

            Assert.IsTrue(_data.RemovePartyMember("victim"));
            Assert.AreEqual(1, _rosterRaises,
                "The kick path bypasses PartyMemberService entirely, so it has to raise " +
                "the signal itself or a kick would be the one roster change that never " +
                "updated a count.");
        }

        [Test]
        public void RemovePartyMember_UnknownMember_DoesNotRaise()
        {
            _partyMembers.Add(new PartyPlayerData("local", "LocalPilot", 1));

            Assert.IsFalse(_data.RemovePartyMember("ghost"));
            Assert.AreEqual(0, _rosterRaises);
        }

        #endregion
    }
}
#endif
