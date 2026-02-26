using NUnit.Framework;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// HostConnectionDataSO Tests — Validates the party/connection state container.
    ///
    /// WHY THIS MATTERS:
    /// HostConnectionDataSO is the central SOAP container for multiplayer party state.
    /// It tracks who's in the party, whether slots are open, and local player identity.
    /// If HasOpenSlots returns true when the party is full, extra players will join and
    /// crash the game. If ResetRuntimeData doesn't clear IsHost, returning from a
    /// multiplayer session will leave stale host state.
    /// </summary>
    [TestFixture]
    public class HostConnectionDataSOTests
    {
        HostConnectionDataSO _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        #region Default Values

        [Test]
        public void Default_LocalPlayerId_IsEmpty()
        {
            // Fields are initialized by Unity — in a fresh SO they default to type defaults.
            Assert.IsTrue(string.IsNullOrEmpty(_data.LocalPlayerId));
        }

        [Test]
        public void Default_IsConnected_IsFalse()
        {
            Assert.IsFalse(_data.IsConnected);
        }

        [Test]
        public void Default_IsHost_IsFalse()
        {
            Assert.IsFalse(_data.IsHost);
        }

        #endregion

        #region HasOpenSlots

        [Test]
        public void HasOpenSlots_NullPartyMembers_ReturnsTrue()
        {
            // When PartyMembers is null (not yet initialized), there are open slots.
            Assert.IsTrue(_data.HasOpenSlots);
        }

        #endregion

        #region RemotePartyMemberCount

        [Test]
        public void RemotePartyMemberCount_NullPartyMembers_ReturnsZero()
        {
            Assert.AreEqual(0, _data.RemotePartyMemberCount);
        }

        #endregion

        #region ResetRuntimeData

        [Test]
        public void ResetRuntimeData_ClearsLocalPlayerId()
        {
            _data.LocalPlayerId = "player123";

            _data.ResetRuntimeData();

            Assert.AreEqual(string.Empty, _data.LocalPlayerId);
        }

        [Test]
        public void ResetRuntimeData_ClearsDisplayName()
        {
            _data.LocalDisplayName = "CosmicPilot";

            _data.ResetRuntimeData();

            Assert.AreEqual(string.Empty, _data.LocalDisplayName);
        }

        [Test]
        public void ResetRuntimeData_ResetsAvatarId()
        {
            _data.LocalAvatarId = 5;

            _data.ResetRuntimeData();

            Assert.AreEqual(0, _data.LocalAvatarId);
        }

        [Test]
        public void ResetRuntimeData_SetsIsConnectedToFalse()
        {
            _data.IsConnected = true;

            _data.ResetRuntimeData();

            Assert.IsFalse(_data.IsConnected);
        }

        [Test]
        public void ResetRuntimeData_SetsIsHostToFalse()
        {
            _data.IsHost = true;

            _data.ResetRuntimeData();

            Assert.IsFalse(_data.IsHost);
        }

        #endregion

        #region LocalPlayerData

        [Test]
        public void LocalPlayerData_ReturnsCurrentValues()
        {
            _data.LocalPlayerId = "id42";
            _data.LocalDisplayName = "TestPlayer";
            _data.LocalAvatarId = 3;

            var playerData = _data.LocalPlayerData;

            Assert.AreEqual("id42", playerData.PlayerId);
            Assert.AreEqual("TestPlayer", playerData.DisplayName);
            Assert.AreEqual(3, playerData.AvatarId);
        }

        #endregion

        #region RemovePartyMember

        [Test]
        public void RemovePartyMember_NullPartyMembers_ReturnsFalse()
        {
            bool result = _data.RemovePartyMember("anyId");

            Assert.IsFalse(result);
        }

        #endregion

        #region MaxPartySlots

        [Test]
        public void MaxPartySlots_DefaultIsFour()
        {
            // Default from [SerializeField] private int maxPartySlots = 4
            Assert.AreEqual(4, _data.MaxPartySlots);
        }

        #endregion
    }
}
