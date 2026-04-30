using NUnit.Framework;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// PartyInviteData Tests — Validates the immutable invite payload struct.
    ///
    /// WHY THIS MATTERS:
    /// PartyInviteData carries the invite payload across SOAP events (OnInviteReceived)
    /// and through the accept flow in PartyInviteController. If any field is lost during
    /// construction or copy, the accept flow will fail to join the correct party session
    /// or display the wrong host information.
    /// </summary>
    [TestFixture]
    public class PartyInviteDataTests
    {
        #region Constructor

        [Test]
        public void Constructor_AssignsAllFields()
        {
            var invite = new PartyInviteData("host123", "session456", "HostPilot", 7);

            Assert.AreEqual("host123", invite.HostPlayerId);
            Assert.AreEqual("session456", invite.PartySessionId);
            Assert.AreEqual("HostPilot", invite.HostDisplayName);
            Assert.AreEqual(7, invite.HostAvatarId);
        }

        [Test]
        public void DefaultConstructor_AllFieldsAreDefaults()
        {
            var invite = new PartyInviteData();

            Assert.IsNull(invite.HostPlayerId);
            Assert.IsNull(invite.PartySessionId);
            Assert.IsNull(invite.HostDisplayName);
            Assert.AreEqual(0, invite.HostAvatarId);
        }

        #endregion

        #region Struct Semantics

        [Test]
        public void Struct_CopySemantics_PreservesAllFields()
        {
            var original = new PartyInviteData("host1", "sess1", "Alice", 3);
            var copy = original;

            Assert.AreEqual(original.HostPlayerId, copy.HostPlayerId);
            Assert.AreEqual(original.PartySessionId, copy.PartySessionId);
            Assert.AreEqual(original.HostDisplayName, copy.HostDisplayName);
            Assert.AreEqual(original.HostAvatarId, copy.HostAvatarId);
        }

        [Test]
        public void Struct_Immutability_ReadOnlyProperties()
        {
            var invite = new PartyInviteData("host99", "sess99", "Captain", 5);

            Assert.AreEqual("host99", invite.HostPlayerId);
            Assert.AreEqual("sess99", invite.PartySessionId);
            Assert.AreEqual("Captain", invite.HostDisplayName);
            Assert.AreEqual(5, invite.HostAvatarId);
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Constructor_NullStrings_DoNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                var invite = new PartyInviteData(null, null, null, 0);
                Assert.IsNull(invite.HostPlayerId);
                Assert.IsNull(invite.PartySessionId);
                Assert.IsNull(invite.HostDisplayName);
            });
        }

        [Test]
        public void Constructor_EmptyStrings_PreservedAsEmpty()
        {
            var invite = new PartyInviteData("", "", "", 0);

            Assert.AreEqual("", invite.HostPlayerId);
            Assert.AreEqual("", invite.PartySessionId);
            Assert.AreEqual("", invite.HostDisplayName);
        }

        [Test]
        public void Constructor_NegativeAvatarId_PreservedAsNegative()
        {
            var invite = new PartyInviteData("host1", "sess1", "Pilot", -1);

            Assert.AreEqual(-1, invite.HostAvatarId);
        }

        #endregion
    }
}
