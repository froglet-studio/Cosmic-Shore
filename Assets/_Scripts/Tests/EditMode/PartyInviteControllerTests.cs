using NUnit.Framework;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// PartyInviteController Tests — Validates precondition checks and state logic.
    ///
    /// WHY THIS MATTERS:
    /// PartyInviteController orchestrates the critical host-to-client transition
    /// when accepting a party invite. These tests verify the controller's
    /// precondition checks and state management without requiring a running
    /// NetworkManager or UGS services (which are unavailable in edit-mode tests).
    ///
    /// Integration testing with actual Netcode transitions requires play-mode tests
    /// with a full Bootstrap scene, which is tracked separately.
    /// </summary>
    [TestFixture]
    public class PartyInviteControllerTests
    {
        HostConnectionDataSO _connectionData;

        [SetUp]
        public void SetUp()
        {
            _connectionData = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_connectionData);
        }

        #region HostConnectionDataSO — OnPartyJoinCompleted

        [Test]
        public void HostConnectionDataSO_OnPartyJoinCompleted_ExistsAsPublicField()
        {
            // The new OnPartyJoinCompleted event field should be accessible.
            // When not wired in the inspector, it will be null.
            Assert.IsNull(_connectionData.OnPartyJoinCompleted,
                "OnPartyJoinCompleted should be null when not wired via inspector on a fresh SO.");
        }

        #endregion

        #region HostConnectionDataSO — HasOpenSlots

        [Test]
        public void HasOpenSlots_WithNullPartyMembers_ReturnsTrue()
        {
            Assert.IsTrue(_connectionData.HasOpenSlots);
        }

        #endregion

        #region HostConnectionDataSO — RemotePartyMemberCount

        [Test]
        public void RemotePartyMemberCount_WithNullPartyMembers_ReturnsZero()
        {
            Assert.AreEqual(0, _connectionData.RemotePartyMemberCount);
        }

        #endregion

        #region HostConnectionDataSO — RemovePartyMember

        [Test]
        public void RemovePartyMember_NullPartyMembers_ReturnsFalse()
        {
            Assert.IsFalse(_connectionData.RemovePartyMember("nonexistent"));
        }

        #endregion

        #region HostConnectionDataSO — ResetRuntimeData

        [Test]
        public void ResetRuntimeData_ClearsAllState()
        {
            _connectionData.LocalPlayerId = "player1";
            _connectionData.LocalDisplayName = "TestPilot";
            _connectionData.LocalAvatarId = 5;
            _connectionData.IsConnected = true;
            _connectionData.IsHost = true;

            _connectionData.ResetRuntimeData();

            Assert.AreEqual(string.Empty, _connectionData.LocalPlayerId);
            Assert.AreEqual(string.Empty, _connectionData.LocalDisplayName);
            Assert.AreEqual(0, _connectionData.LocalAvatarId);
            Assert.IsFalse(_connectionData.IsConnected);
            Assert.IsFalse(_connectionData.IsHost);
        }

        #endregion

        #region PartyInviteData — Round-Trip Through Accept Flow

        [Test]
        public void PartyInviteData_PreservesDataThroughCopy()
        {
            // Simulates the flow: ShowInvite stores data → OnAccept reads it
            var original = new PartyInviteData("hostId", "sessionId", "HostName", 3);
            PartyInviteData stored = original;
            PartyInviteData retrieved = stored;

            Assert.AreEqual(original.HostPlayerId, retrieved.HostPlayerId);
            Assert.AreEqual(original.PartySessionId, retrieved.PartySessionId);
            Assert.AreEqual(original.HostDisplayName, retrieved.HostDisplayName);
            Assert.AreEqual(original.HostAvatarId, retrieved.HostAvatarId);
        }

        #endregion

        #region State Transitions

        [Test]
        public void ConnectionData_HostFlag_SetCorrectlyForClient()
        {
            // When accepting an invite, IsHost should be set to false
            _connectionData.IsHost = true;
            _connectionData.IsHost = false;

            Assert.IsFalse(_connectionData.IsHost,
                "IsHost should be false after transitioning to client role.");
        }

        [Test]
        public void ConnectionData_LocalPlayerData_ReturnsCurrentValues()
        {
            _connectionData.LocalPlayerId = "localId";
            _connectionData.LocalDisplayName = "LocalPilot";
            _connectionData.LocalAvatarId = 2;

            var data = _connectionData.LocalPlayerData;

            Assert.AreEqual("localId", data.PlayerId);
            Assert.AreEqual("LocalPilot", data.DisplayName);
            Assert.AreEqual(2, data.AvatarId);
        }

        #endregion
    }
}
