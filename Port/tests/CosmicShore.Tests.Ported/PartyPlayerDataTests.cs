using NUnit.Framework;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// PartyPlayerData Tests — Validates the immutable player identity struct.
    ///
    /// WHY THIS MATTERS:
    /// PartyPlayerData is the payload for all party SOAP events (join, leave, kick,
    /// invite). It's used as the element type in ScriptableList and compared via
    /// Equals() for duplicate detection. If Equals only compares by PlayerId (by
    /// design), that needs to be tested. If it compared by all fields, two instances
    /// of the same player with different display names would not match — breaking
    /// party member removal.
    /// </summary>
    [TestFixture]
    public class PartyPlayerDataTests
    {
        #region Constructor

        [Test]
        public void Constructor_AssignsAllFields()
        {
            var data = new PartyPlayerData("id123", "Pilot", 5);

            Assert.AreEqual("id123", data.PlayerId);
            Assert.AreEqual("Pilot", data.DisplayName);
            Assert.AreEqual(5, data.AvatarId);
        }

        [Test]
        public void DefaultConstructor_AllFieldsAreDefaults()
        {
            var data = new PartyPlayerData();

            Assert.IsNull(data.PlayerId);
            Assert.IsNull(data.DisplayName);
            Assert.AreEqual(0, data.AvatarId);
        }

        #endregion

        #region Equality — By PlayerId Only

        [Test]
        public void Equals_SamePlayerId_ReturnsTrue()
        {
            var a = new PartyPlayerData("id1", "Alice", 1);
            var b = new PartyPlayerData("id1", "Alice", 1);

            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void Equals_SamePlayerId_DifferentDisplayName_StillTrue()
        {
            // Equality is by PlayerId only — display name may change.
            var a = new PartyPlayerData("id1", "Alice", 1);
            var b = new PartyPlayerData("id1", "Alice_Updated", 2);

            Assert.IsTrue(a.Equals(b),
                "Equality should be based on PlayerId only, ignoring display name and avatar.");
        }

        [Test]
        public void Equals_DifferentPlayerId_ReturnsFalse()
        {
            var a = new PartyPlayerData("id1", "Alice", 1);
            var b = new PartyPlayerData("id2", "Alice", 1);

            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_NonPartyPlayerData_ReturnsFalse()
        {
            var a = new PartyPlayerData("id1", "Alice", 1);

            Assert.IsFalse(a.Equals("not a PartyPlayerData"));
            Assert.IsFalse(a.Equals(null));
            Assert.IsFalse(a.Equals(42));
        }

        #endregion

        #region GetHashCode

        [Test]
        public void GetHashCode_SamePlayerId_SameHash()
        {
            var a = new PartyPlayerData("id1", "Alice", 1);
            var b = new PartyPlayerData("id1", "Bob", 2);

            Assert.AreEqual(a.GetHashCode(), b.GetHashCode(),
                "Same PlayerId should produce same hash for dictionary/set compatibility.");
        }

        [Test]
        public void GetHashCode_NullPlayerId_DoesNotThrow()
        {
            var data = new PartyPlayerData();

            Assert.DoesNotThrow(() => { var _ = data.GetHashCode(); });
            Assert.AreEqual(0, data.GetHashCode(), "Null PlayerId should hash to 0.");
        }

        [Test]
        public void GetHashCode_DifferentPlayerIds_DifferentHash()
        {
            var a = new PartyPlayerData("id1", "A", 0);
            var b = new PartyPlayerData("id2", "B", 0);

            // Not strictly guaranteed by the hash contract, but very likely.
            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
        }

        #endregion

        #region Struct Semantics

        [Test]
        public void Struct_CopySemantics()
        {
            var original = new PartyPlayerData("id1", "Alice", 3);
            var copy = original;

            // Struct copies should be fully independent.
            Assert.AreEqual(original.PlayerId, copy.PlayerId);
            Assert.AreEqual(original.DisplayName, copy.DisplayName);
            Assert.AreEqual(original.AvatarId, copy.AvatarId);
        }

        [Test]
        public void Struct_Immutability_ReadOnlyProperties()
        {
            // PartyPlayerData uses private backing fields with get-only properties.
            // This test simply verifies it can be constructed and read — no mutators exist.
            var data = new PartyPlayerData("id99", "Captain", 7);

            Assert.AreEqual("id99", data.PlayerId);
            Assert.AreEqual("Captain", data.DisplayName);
            Assert.AreEqual(7, data.AvatarId);
        }

        #endregion
    }
}
