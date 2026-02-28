using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// PartyInviteSystem Tests — Comprehensive coverage of the party/invite system.
    ///
    /// WHY THIS MATTERS:
    /// The invite system is the backbone of multiplayer party formation. These tests
    /// cover the full data lifecycle: invite payload formatting, parsing, SOAP container
    /// state management with wired lists, collection contracts (HashSet/Dictionary
    /// compatibility), and slot management under realistic conditions.
    ///
    /// Existing test files cover the basics (struct construction, null-list defaults).
    /// This file fills the gaps: wired-list behavior, ParseInvite edge cases,
    /// collection equality contracts, and multi-member party state management.
    /// </summary>
    [TestFixture]
    public class PartyInviteSystemTests
    {
        HostConnectionDataSO _data;
        ScriptableListPartyPlayerData _partyMembers;
        ScriptableListPartyPlayerData _onlinePlayers;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<HostConnectionDataSO>();
            _partyMembers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
            _onlinePlayers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
            _data.PartyMembers = _partyMembers;
            _data.OnlinePlayers = _onlinePlayers;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_onlinePlayers);
            Object.DestroyImmediate(_partyMembers);
            Object.DestroyImmediate(_data);
        }

        // ─────────────────────────────────────────────────────────────────────
        // HostConnectionDataSO — HasOpenSlots (with wired list)
        // ─────────────────────────────────────────────────────────────────────

        #region HasOpenSlots With Wired List

        [Test]
        public void HasOpenSlots_EmptyList_ReturnsTrue()
        {
            Assert.IsTrue(_data.HasOpenSlots,
                "An empty party should have open slots.");
        }

        [Test]
        public void HasOpenSlots_OneMember_ReturnsTrue()
        {
            _partyMembers.Add(new PartyPlayerData("local", "LocalPilot", 1));

            Assert.IsTrue(_data.HasOpenSlots,
                "A party with 1 of 4 slots filled should have open slots.");
        }

        [Test]
        public void HasOpenSlots_ThreeMembers_ReturnsTrue()
        {
            _partyMembers.Add(new PartyPlayerData("p1", "Pilot1", 1));
            _partyMembers.Add(new PartyPlayerData("p2", "Pilot2", 2));
            _partyMembers.Add(new PartyPlayerData("p3", "Pilot3", 3));

            Assert.IsTrue(_data.HasOpenSlots,
                "A party with 3 of 4 slots should still have one open slot.");
        }

        [Test]
        public void HasOpenSlots_FourMembers_ReturnsFalse()
        {
            _partyMembers.Add(new PartyPlayerData("p1", "Pilot1", 1));
            _partyMembers.Add(new PartyPlayerData("p2", "Pilot2", 2));
            _partyMembers.Add(new PartyPlayerData("p3", "Pilot3", 3));
            _partyMembers.Add(new PartyPlayerData("p4", "Pilot4", 4));

            Assert.IsFalse(_data.HasOpenSlots,
                "A party at max capacity (4/4) should have no open slots.");
        }

        [Test]
        public void HasOpenSlots_AfterRemovingMember_ReturnsTrue()
        {
            _partyMembers.Add(new PartyPlayerData("p1", "Pilot1", 1));
            _partyMembers.Add(new PartyPlayerData("p2", "Pilot2", 2));
            _partyMembers.Add(new PartyPlayerData("p3", "Pilot3", 3));
            _partyMembers.Add(new PartyPlayerData("p4", "Pilot4", 4));

            Assert.IsFalse(_data.HasOpenSlots, "Party should be full before removal.");

            _partyMembers.RemoveAt(3);

            Assert.IsTrue(_data.HasOpenSlots,
                "Party should have an open slot after removing a member.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // HostConnectionDataSO — RemotePartyMemberCount (with wired list)
        // ─────────────────────────────────────────────────────────────────────

        #region RemotePartyMemberCount With Wired List

        [Test]
        public void RemotePartyMemberCount_EmptyList_ReturnsZero()
        {
            _data.LocalPlayerId = "localId";

            Assert.AreEqual(0, _data.RemotePartyMemberCount);
        }

        [Test]
        public void RemotePartyMemberCount_OnlyLocalPlayer_ReturnsZero()
        {
            _data.LocalPlayerId = "localId";
            _partyMembers.Add(new PartyPlayerData("localId", "LocalPilot", 1));

            Assert.AreEqual(0, _data.RemotePartyMemberCount,
                "Local player should not count as a remote member.");
        }

        [Test]
        public void RemotePartyMemberCount_LocalPlusOneRemote_ReturnsOne()
        {
            _data.LocalPlayerId = "localId";
            _partyMembers.Add(new PartyPlayerData("localId", "LocalPilot", 1));
            _partyMembers.Add(new PartyPlayerData("remote1", "RemotePilot", 2));

            Assert.AreEqual(1, _data.RemotePartyMemberCount);
        }

        [Test]
        public void RemotePartyMemberCount_LocalPlusThreeRemote_ReturnsThree()
        {
            _data.LocalPlayerId = "localId";
            _partyMembers.Add(new PartyPlayerData("localId", "LocalPilot", 1));
            _partyMembers.Add(new PartyPlayerData("r1", "Remote1", 2));
            _partyMembers.Add(new PartyPlayerData("r2", "Remote2", 3));
            _partyMembers.Add(new PartyPlayerData("r3", "Remote3", 4));

            Assert.AreEqual(3, _data.RemotePartyMemberCount);
        }

        [Test]
        public void RemotePartyMemberCount_AllRemote_CountsAll()
        {
            // Edge case: LocalPlayerId doesn't match anyone in the list
            _data.LocalPlayerId = "nobody";
            _partyMembers.Add(new PartyPlayerData("p1", "Pilot1", 1));
            _partyMembers.Add(new PartyPlayerData("p2", "Pilot2", 2));

            Assert.AreEqual(2, _data.RemotePartyMemberCount,
                "When local player isn't in the list, all members are remote.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // HostConnectionDataSO — RemovePartyMember (with wired list)
        // ─────────────────────────────────────────────────────────────────────

        #region RemovePartyMember With Wired List

        [Test]
        public void RemovePartyMember_ExistingMember_ReturnsTrue()
        {
            _partyMembers.Add(new PartyPlayerData("p1", "Pilot1", 1));
            _partyMembers.Add(new PartyPlayerData("target", "TargetPilot", 2));

            bool result = _data.RemovePartyMember("target");

            Assert.IsTrue(result, "Should return true when member is found and removed.");
            Assert.AreEqual(1, _partyMembers.Count, "List should have 1 member remaining.");
        }

        [Test]
        public void RemovePartyMember_NonExistentMember_ReturnsFalse()
        {
            _partyMembers.Add(new PartyPlayerData("p1", "Pilot1", 1));

            bool result = _data.RemovePartyMember("nonexistent");

            Assert.IsFalse(result, "Should return false when player is not in the party.");
            Assert.AreEqual(1, _partyMembers.Count, "List should be unchanged.");
        }

        [Test]
        public void RemovePartyMember_EmptyList_ReturnsFalse()
        {
            bool result = _data.RemovePartyMember("anyId");

            Assert.IsFalse(result, "Should return false when party list is empty.");
        }

        [Test]
        public void RemovePartyMember_RemovesCorrectMember()
        {
            _partyMembers.Add(new PartyPlayerData("keep1", "Keep1", 1));
            _partyMembers.Add(new PartyPlayerData("remove", "Remove", 2));
            _partyMembers.Add(new PartyPlayerData("keep2", "Keep2", 3));

            _data.RemovePartyMember("remove");

            Assert.AreEqual(2, _partyMembers.Count);
            Assert.AreEqual("keep1", _partyMembers[0].PlayerId);
            Assert.AreEqual("keep2", _partyMembers[1].PlayerId);
        }

        [Test]
        public void RemovePartyMember_LastMember_ListBecomesEmpty()
        {
            _partyMembers.Add(new PartyPlayerData("only", "OnlyPilot", 1));

            _data.RemovePartyMember("only");

            Assert.AreEqual(0, _partyMembers.Count);
            Assert.IsTrue(_partyMembers.IsEmpty);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // HostConnectionDataSO — ResetRuntimeData (with wired lists)
        // ─────────────────────────────────────────────────────────────────────

        #region ResetRuntimeData With Wired Lists

        [Test]
        public void ResetRuntimeData_ClearsPartyMembers()
        {
            _partyMembers.Add(new PartyPlayerData("p1", "Pilot1", 1));
            _partyMembers.Add(new PartyPlayerData("p2", "Pilot2", 2));

            _data.ResetRuntimeData();

            Assert.AreEqual(0, _partyMembers.Count,
                "ResetRuntimeData should clear the PartyMembers list.");
        }

        [Test]
        public void ResetRuntimeData_ClearsOnlinePlayers()
        {
            _onlinePlayers.Add(new PartyPlayerData("o1", "Online1", 1));
            _onlinePlayers.Add(new PartyPlayerData("o2", "Online2", 2));

            _data.ResetRuntimeData();

            Assert.AreEqual(0, _onlinePlayers.Count,
                "ResetRuntimeData should clear the OnlinePlayers list.");
        }

        [Test]
        public void ResetRuntimeData_ClearsAllFieldsAndLists()
        {
            _data.LocalPlayerId = "player123";
            _data.LocalDisplayName = "TestPilot";
            _data.LocalAvatarId = 5;
            _data.IsConnected = true;
            _data.IsHost = true;
            _partyMembers.Add(new PartyPlayerData("p1", "P1", 1));
            _onlinePlayers.Add(new PartyPlayerData("o1", "O1", 1));

            _data.ResetRuntimeData();

            Assert.AreEqual(string.Empty, _data.LocalPlayerId);
            Assert.AreEqual(string.Empty, _data.LocalDisplayName);
            Assert.AreEqual(0, _data.LocalAvatarId);
            Assert.IsFalse(_data.IsConnected);
            Assert.IsFalse(_data.IsHost);
            Assert.AreEqual(0, _partyMembers.Count);
            Assert.AreEqual(0, _onlinePlayers.Count);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // ParseInvite — Format Validation (via reflection)
        // ─────────────────────────────────────────────────────────────────────

        #region ParseInvite Format Validation

        /// <summary>
        /// Invokes the private static ParseInvite method on HostConnectionService
        /// via reflection for unit testing.
        /// </summary>
        static PartyInviteData? InvokeParseInvite(string raw)
        {
            var method = typeof(CosmicShore.Gameplay.HostConnectionService)
                .GetMethod("ParseInvite",
                    BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method,
                "ParseInvite method should exist on HostConnectionService.");

            return (PartyInviteData?)method.Invoke(null, new object[] { raw });
        }

        [Test]
        public void ParseInvite_ValidFormat_ReturnsInviteData()
        {
            var result = InvokeParseInvite("host123|session456|HostPilot|7");

            Assert.IsTrue(result.HasValue, "Valid format should parse successfully.");
            Assert.AreEqual("host123", result.Value.HostPlayerId);
            Assert.AreEqual("session456", result.Value.PartySessionId);
            Assert.AreEqual("HostPilot", result.Value.HostDisplayName);
            Assert.AreEqual(7, result.Value.HostAvatarId);
        }

        [Test]
        public void ParseInvite_NullInput_ReturnsNull()
        {
            var result = InvokeParseInvite(null);

            Assert.IsFalse(result.HasValue,
                "Null input should return null.");
        }

        [Test]
        public void ParseInvite_EmptyString_ReturnsNull()
        {
            var result = InvokeParseInvite("");

            Assert.IsFalse(result.HasValue,
                "Empty string should return null.");
        }

        [Test]
        public void ParseInvite_TooFewParts_ReturnsNull()
        {
            var result = InvokeParseInvite("host|session|name");

            Assert.IsFalse(result.HasValue,
                "Less than 4 pipe-separated parts should return null.");
        }

        [Test]
        public void ParseInvite_TwoParts_ReturnsNull()
        {
            var result = InvokeParseInvite("host|session");

            Assert.IsFalse(result.HasValue);
        }

        [Test]
        public void ParseInvite_OnePart_ReturnsNull()
        {
            var result = InvokeParseInvite("justOneValue");

            Assert.IsFalse(result.HasValue);
        }

        [Test]
        public void ParseInvite_NonNumericAvatarId_ReturnsNull()
        {
            var result = InvokeParseInvite("host|session|name|notANumber");

            Assert.IsFalse(result.HasValue,
                "Non-numeric avatar ID should fail parsing.");
        }

        [Test]
        public void ParseInvite_FloatAvatarId_ReturnsNull()
        {
            var result = InvokeParseInvite("host|session|name|3.14");

            Assert.IsFalse(result.HasValue,
                "Float avatar ID should fail int.TryParse.");
        }

        [Test]
        public void ParseInvite_NegativeAvatarId_Succeeds()
        {
            var result = InvokeParseInvite("host|session|name|-1");

            Assert.IsTrue(result.HasValue,
                "Negative avatar IDs should parse successfully (int.TryParse allows them).");
            Assert.AreEqual(-1, result.Value.HostAvatarId);
        }

        [Test]
        public void ParseInvite_ZeroAvatarId_Succeeds()
        {
            var result = InvokeParseInvite("host|session|name|0");

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(0, result.Value.HostAvatarId);
        }

        [Test]
        public void ParseInvite_ExtraPipes_StillSucceeds()
        {
            // More than 4 parts — the parser only reads the first 4
            var result = InvokeParseInvite("host|session|name|5|extra|data");

            Assert.IsTrue(result.HasValue,
                "Extra pipe-separated fields should be ignored (parts.Length >= 4 is sufficient).");
            Assert.AreEqual("host", result.Value.HostPlayerId);
            Assert.AreEqual("session", result.Value.PartySessionId);
            Assert.AreEqual("name", result.Value.HostDisplayName);
            Assert.AreEqual(5, result.Value.HostAvatarId);
        }

        [Test]
        public void ParseInvite_EmptyFieldsButValidFormat_Succeeds()
        {
            var result = InvokeParseInvite("||name|3");

            Assert.IsTrue(result.HasValue,
                "Empty hostPlayerId and sessionId are allowed by the parser.");
            Assert.AreEqual("", result.Value.HostPlayerId);
            Assert.AreEqual("", result.Value.PartySessionId);
        }

        [Test]
        public void ParseInvite_DisplayNameWithSpaces_PreservedCorrectly()
        {
            var result = InvokeParseInvite("host1|sess1|Captain Cosmic|2");

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual("Captain Cosmic", result.Value.HostDisplayName);
        }

        [Test]
        public void ParseInvite_UnicodeDisplayName_PreservedCorrectly()
        {
            var result = InvokeParseInvite("host1|sess1|\u5b87\u5b99\u98db\u884c\u58eb|4");

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual("\u5b87\u5b99\u98db\u884c\u58eb", result.Value.HostDisplayName);
        }

        [Test]
        public void ParseInvite_LargeAvatarId_Succeeds()
        {
            var result = InvokeParseInvite("host|sess|name|999999");

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(999999, result.Value.HostAvatarId);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // Invite Format Round-Trip
        // ─────────────────────────────────────────────────────────────────────

        #region Invite Format Round-Trip

        [Test]
        public void InviteFormat_RoundTrip_PreservesAllFields()
        {
            // Simulate the exact format used by HostConnectionService.SendInviteAsync
            string hostId = "host_abc123";
            string sessionId = "sess_xyz789";
            string hostName = "CosmicPilot";
            int avatarId = 3;

            string formatted = $"{hostId}|{sessionId}|{hostName}|{avatarId}";
            var parsed = InvokeParseInvite(formatted);

            Assert.IsTrue(parsed.HasValue);
            Assert.AreEqual(hostId, parsed.Value.HostPlayerId);
            Assert.AreEqual(sessionId, parsed.Value.PartySessionId);
            Assert.AreEqual(hostName, parsed.Value.HostDisplayName);
            Assert.AreEqual(avatarId, parsed.Value.HostAvatarId);
        }

        [Test]
        public void InviteFormat_PipeInDisplayName_CorruptsData()
        {
            // Demonstrates that a pipe in the display name breaks parsing.
            // This is an expected limitation of the pipe-delimited format.
            // The parser splits on '|' so a name containing '|' shifts fields.
            var result = InvokeParseInvite("host|sess|Name|With|Pipe|3");

            // With a pipe in the name, parts[3] = "With" which is not a number
            Assert.IsTrue(result.HasValue || !result.HasValue,
                "This test documents the pipe-in-name edge case behavior.");

            // The actual behavior: parts = ["host","sess","Name","With","Pipe","3"]
            // parts[3] = "With" → int.TryParse fails → returns null
            Assert.IsFalse(result.HasValue,
                "A pipe character in the display name corrupts the invite format.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // PartyPlayerData — Collection Contract (HashSet / Dictionary)
        // ─────────────────────────────────────────────────────────────────────

        #region Collection Contract

        [Test]
        public void HashSet_SamePlayerId_Deduplicated()
        {
            var set = new HashSet<PartyPlayerData>
            {
                new("id1", "Alice", 1),
                new("id1", "Alice_Updated", 2) // Same ID, different name
            };

            Assert.AreEqual(1, set.Count,
                "HashSet should deduplicate by PlayerId (Equals + GetHashCode contract).");
        }

        [Test]
        public void HashSet_DifferentPlayerIds_BothPresent()
        {
            var set = new HashSet<PartyPlayerData>
            {
                new("id1", "Alice", 1),
                new("id2", "Alice", 1) // Different ID, same name
            };

            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void HashSet_Contains_FindsByPlayerId()
        {
            var set = new HashSet<PartyPlayerData>
            {
                new("id1", "Alice", 1)
            };

            var lookup = new PartyPlayerData("id1", "DifferentName", 99);

            Assert.IsTrue(set.Contains(lookup),
                "Contains should find the entry by PlayerId regardless of other fields.");
        }

        [Test]
        public void HashSet_Remove_RemovesByPlayerId()
        {
            var set = new HashSet<PartyPlayerData>
            {
                new("id1", "Alice", 1),
                new("id2", "Bob", 2)
            };

            bool removed = set.Remove(new PartyPlayerData("id1", "ChangedName", 99));

            Assert.IsTrue(removed,
                "Remove should find and remove by PlayerId.");
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void Dictionary_SamePlayerId_OverwritesEntry()
        {
            var dict = new Dictionary<PartyPlayerData, string>();
            var key1 = new PartyPlayerData("id1", "Alice", 1);
            var key2 = new PartyPlayerData("id1", "Alice_Updated", 2);

            dict[key1] = "first";
            dict[key2] = "second";

            Assert.AreEqual(1, dict.Count,
                "Same PlayerId should map to the same dictionary entry.");
            Assert.AreEqual("second", dict[key1]);
        }

        [Test]
        public void Dictionary_DifferentPlayerIds_SeparateEntries()
        {
            var dict = new Dictionary<PartyPlayerData, int>
            {
                { new PartyPlayerData("id1", "Alice", 1), 100 },
                { new PartyPlayerData("id2", "Bob", 2), 200 }
            };

            Assert.AreEqual(2, dict.Count);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // ScriptableList + PartyPlayerData Integration
        // ─────────────────────────────────────────────────────────────────────

        #region ScriptableList Integration

        [Test]
        public void ScriptableList_Contains_UsesEqualsByPlayerId()
        {
            _partyMembers.Add(new PartyPlayerData("id1", "Alice", 1));

            bool found = _partyMembers.Contains(
                new PartyPlayerData("id1", "DifferentName", 99));

            Assert.IsTrue(found,
                "ScriptableList.Contains should use PartyPlayerData.Equals (by PlayerId).");
        }

        [Test]
        public void ScriptableList_TryAdd_PreventsDuplicateByPlayerId()
        {
            _partyMembers.Add(new PartyPlayerData("id1", "Alice", 1));

            bool added = _partyMembers.TryAdd(
                new PartyPlayerData("id1", "AliceUpdated", 2));

            Assert.IsFalse(added,
                "TryAdd should reject a duplicate PlayerId.");
            Assert.AreEqual(1, _partyMembers.Count);
        }

        [Test]
        public void ScriptableList_TryAdd_AllowsDifferentPlayerId()
        {
            _partyMembers.Add(new PartyPlayerData("id1", "Alice", 1));

            bool added = _partyMembers.TryAdd(
                new PartyPlayerData("id2", "Bob", 2));

            Assert.IsTrue(added);
            Assert.AreEqual(2, _partyMembers.Count);
        }

        [Test]
        public void ScriptableList_Add_FiresOnItemAdded()
        {
            PartyPlayerData? addedItem = null;
            _partyMembers.OnItemAdded += item => addedItem = item;

            var player = new PartyPlayerData("id1", "Alice", 1);
            _partyMembers.Add(player);

            Assert.IsTrue(addedItem.HasValue);
            Assert.AreEqual("id1", addedItem.Value.PlayerId);
        }

        [Test]
        public void ScriptableList_RemoveAt_FiresOnItemRemoved()
        {
            _partyMembers.Add(new PartyPlayerData("id1", "Alice", 1));

            PartyPlayerData? removedItem = null;
            _partyMembers.OnItemRemoved += item => removedItem = item;

            _partyMembers.RemoveAt(0);

            Assert.IsTrue(removedItem.HasValue);
            Assert.AreEqual("id1", removedItem.Value.PlayerId);
        }

        [Test]
        public void ScriptableList_Clear_FiresOnCleared()
        {
            _partyMembers.Add(new PartyPlayerData("id1", "Alice", 1));
            _partyMembers.Add(new PartyPlayerData("id2", "Bob", 2));

            bool cleared = false;
            _partyMembers.OnCleared += () => cleared = true;

            _partyMembers.Clear();

            Assert.IsTrue(cleared);
            Assert.AreEqual(0, _partyMembers.Count);
        }

        [Test]
        public void ScriptableList_OnItemCountChanged_FiresOnAddAndRemove()
        {
            int countChangedCalls = 0;
            _partyMembers.OnItemCountChanged += () => countChangedCalls++;

            _partyMembers.Add(new PartyPlayerData("id1", "Alice", 1));
            Assert.AreEqual(1, countChangedCalls, "Should fire once on Add.");

            _partyMembers.Add(new PartyPlayerData("id2", "Bob", 2));
            Assert.AreEqual(2, countChangedCalls, "Should fire again on second Add.");

            _partyMembers.RemoveAt(0);
            Assert.AreEqual(3, countChangedCalls, "Should fire on RemoveAt.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // Party Slot Management — Realistic Scenarios
        // ─────────────────────────────────────────────────────────────────────

        #region Party Slot Scenarios

        [Test]
        public void PartySlot_HostCreatesParty_SelfIsFirstMember()
        {
            // Simulates: HostConnectionService joins lobby, adds self
            _data.LocalPlayerId = "host1";
            _data.LocalDisplayName = "HostPilot";
            _data.LocalAvatarId = 1;

            _partyMembers.Add(_data.LocalPlayerData);

            Assert.AreEqual(1, _partyMembers.Count);
            Assert.AreEqual("host1", _partyMembers[0].PlayerId);
            Assert.AreEqual(0, _data.RemotePartyMemberCount);
            Assert.IsTrue(_data.HasOpenSlots);
        }

        [Test]
        public void PartySlot_RemoteJoins_CountIncreases()
        {
            _data.LocalPlayerId = "host1";
            _partyMembers.Add(new PartyPlayerData("host1", "HostPilot", 1));
            _partyMembers.Add(new PartyPlayerData("client1", "ClientPilot", 2));

            Assert.AreEqual(2, _partyMembers.Count);
            Assert.AreEqual(1, _data.RemotePartyMemberCount);
            Assert.IsTrue(_data.HasOpenSlots);
        }

        [Test]
        public void PartySlot_RemoteLeaves_CountDecreases()
        {
            _data.LocalPlayerId = "host1";
            _partyMembers.Add(new PartyPlayerData("host1", "HostPilot", 1));
            _partyMembers.Add(new PartyPlayerData("client1", "ClientPilot", 2));

            _data.RemovePartyMember("client1");

            Assert.AreEqual(1, _partyMembers.Count);
            Assert.AreEqual(0, _data.RemotePartyMemberCount);
        }

        [Test]
        public void PartySlot_FullParty_ThenKick_OpensSlot()
        {
            _data.LocalPlayerId = "host";
            _data.IsHost = true;
            _partyMembers.Add(new PartyPlayerData("host", "Host", 0));
            _partyMembers.Add(new PartyPlayerData("p1", "Player1", 1));
            _partyMembers.Add(new PartyPlayerData("p2", "Player2", 2));
            _partyMembers.Add(new PartyPlayerData("p3", "Player3", 3));

            Assert.IsFalse(_data.HasOpenSlots, "Party should be full.");
            Assert.AreEqual(3, _data.RemotePartyMemberCount);

            _data.RemovePartyMember("p2");

            Assert.IsTrue(_data.HasOpenSlots, "Kicking a member should open a slot.");
            Assert.AreEqual(2, _data.RemotePartyMemberCount);
        }

        [Test]
        public void PartySlot_AcceptInvite_SelfAndHostInList()
        {
            // Simulates: AcceptInviteAsync flow in HostConnectionService
            _data.LocalPlayerId = "client1";
            _data.LocalDisplayName = "ClientPilot";
            _data.LocalAvatarId = 2;

            var invite = new PartyInviteData("host1", "sess1", "HostPilot", 1);

            // Simulate AcceptInviteAsync behavior
            _partyMembers.Clear();
            _partyMembers.Add(_data.LocalPlayerData);
            var hostData = new PartyPlayerData(
                invite.HostPlayerId, invite.HostDisplayName, invite.HostAvatarId);
            _partyMembers.Add(hostData);
            _data.IsHost = false;

            Assert.AreEqual(2, _partyMembers.Count);
            Assert.AreEqual("client1", _partyMembers[0].PlayerId);
            Assert.AreEqual("host1", _partyMembers[1].PlayerId);
            Assert.IsFalse(_data.IsHost);
            Assert.AreEqual(1, _data.RemotePartyMemberCount);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // Online Players List
        // ─────────────────────────────────────────────────────────────────────

        #region Online Players

        [Test]
        public void OnlinePlayers_AddAndClear_WorksCorrectly()
        {
            _onlinePlayers.Add(new PartyPlayerData("p1", "Player1", 1));
            _onlinePlayers.Add(new PartyPlayerData("p2", "Player2", 2));
            _onlinePlayers.Add(new PartyPlayerData("p3", "Player3", 3));

            Assert.AreEqual(3, _onlinePlayers.Count);

            _onlinePlayers.Clear();

            Assert.AreEqual(0, _onlinePlayers.Count);
        }

        [Test]
        public void OnlinePlayers_ContainsByPlayerId()
        {
            _onlinePlayers.Add(new PartyPlayerData("target", "TargetPilot", 5));

            bool found = _onlinePlayers.Contains(
                new PartyPlayerData("target", "AnyName", 0));

            Assert.IsTrue(found,
                "OnlinePlayers.Contains should match by PlayerId.");
        }

        [Test]
        public void OnlinePlayers_Enumeration_ReturnsAllItems()
        {
            _onlinePlayers.Add(new PartyPlayerData("p1", "P1", 1));
            _onlinePlayers.Add(new PartyPlayerData("p2", "P2", 2));

            var ids = new List<string>();
            foreach (var p in _onlinePlayers)
                ids.Add(p.PlayerId);

            Assert.AreEqual(2, ids.Count);
            Assert.Contains("p1", ids);
            Assert.Contains("p2", ids);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // Dedup Guard — _lastFiredInvite Pattern Validation
        // ─────────────────────────────────────────────────────────────────────

        #region Dedup Guard Logic

        [Test]
        public void DedupGuard_SameSessionId_ShouldNotReFire()
        {
            // Validates the dedup pattern used in HostConnectionService.RefreshAsync
            PartyInviteData? lastFired = null;
            var inviteA = new PartyInviteData("host1", "sess_ABC", "Host", 1);

            // First encounter
            if (!lastFired.HasValue ||
                lastFired.Value.PartySessionId != inviteA.PartySessionId)
            {
                lastFired = inviteA;
            }

            Assert.IsTrue(lastFired.HasValue);

            // Repeated encounter with same session ID
            bool wouldFire = !lastFired.HasValue ||
                lastFired.Value.PartySessionId != inviteA.PartySessionId;

            Assert.IsFalse(wouldFire,
                "Same PartySessionId should be deduped on subsequent refreshes.");
        }

        [Test]
        public void DedupGuard_DifferentSessionId_ShouldFire()
        {
            PartyInviteData? lastFired = new PartyInviteData("host1", "sess_OLD", "Host", 1);
            var inviteNew = new PartyInviteData("host2", "sess_NEW", "Host2", 2);

            bool wouldFire = !lastFired.HasValue ||
                lastFired.Value.PartySessionId != inviteNew.PartySessionId;

            Assert.IsTrue(wouldFire,
                "Different PartySessionId should trigger a new invite event.");
        }

        [Test]
        public void DedupGuard_NullLastFired_ShouldFire()
        {
            PartyInviteData? lastFired = null;
            var invite = new PartyInviteData("host1", "sess1", "Host", 1);

            bool wouldFire = !lastFired.HasValue ||
                lastFired.Value.PartySessionId != invite.PartySessionId;

            Assert.IsTrue(wouldFire,
                "First invite should always fire.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // PartyInviteController — IsTransitioning State Validation
        // ─────────────────────────────────────────────────────────────────────

        #region IsTransitioning State

        [Test]
        public void PartyInviteController_IsTransitioning_ExistsAsPublicProperty()
        {
            // Verifies the property exists and is accessible via reflection
            var prop = typeof(CosmicShore.Gameplay.PartyInviteController)
                .GetProperty("IsTransitioning", BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(prop,
                "IsTransitioning property should exist on PartyInviteController.");
            Assert.AreEqual(typeof(bool), prop.PropertyType);
        }

        [Test]
        public void PartyInviteController_TransitionGuard_BackedByPrivateField()
        {
            var field = typeof(CosmicShore.Gameplay.PartyInviteController)
                .GetField("_transitioning", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field,
                "_transitioning field should exist as the backing guard for IsTransitioning.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // HostConnectionService — API Contract Validation
        // ─────────────────────────────────────────────────────────────────────

        #region HostConnectionService API Contract

        [Test]
        public void HostConnectionService_HasParseInviteMethod()
        {
            var method = typeof(CosmicShore.Gameplay.HostConnectionService)
                .GetMethod("ParseInvite",
                    BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "ParseInvite should exist as a private static method.");
            Assert.AreEqual(typeof(PartyInviteData?), method.ReturnType);
        }

        [Test]
        public void HostConnectionService_HasSingletonPattern()
        {
            var prop = typeof(CosmicShore.Gameplay.HostConnectionService)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

            Assert.IsNotNull(prop, "Instance property should exist for singleton access.");
        }

        [Test]
        public void HostConnectionService_HasPublicInviteAPI()
        {
            var type = typeof(CosmicShore.Gameplay.HostConnectionService);

            Assert.IsNotNull(type.GetMethod("SendInviteAsync",
                BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(type.GetMethod("AcceptInviteAsync",
                BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(type.GetMethod("DeclineInviteAsync",
                BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(type.GetMethod("KickPartyMemberAsync",
                BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(type.GetMethod("CreatePartySessionPublicAsync",
                BindingFlags.Public | BindingFlags.Instance));
        }

        [Test]
        public void HostConnectionService_HasConstantKeys()
        {
            var type = typeof(CosmicShore.Gameplay.HostConnectionService);
            var flags = BindingFlags.Static | BindingFlags.NonPublic;

            var inviteTarget = type.GetField("INVITE_TARGET_KEY", flags);
            var inviteData = type.GetField("INVITE_DATA_KEY", flags);
            var displayName = type.GetField("DISPLAY_NAME_KEY", flags);
            var avatarId = type.GetField("AVATAR_ID_KEY", flags);

            Assert.IsNotNull(inviteTarget, "INVITE_TARGET_KEY constant should exist.");
            Assert.IsNotNull(inviteData, "INVITE_DATA_KEY constant should exist.");
            Assert.IsNotNull(displayName, "DISPLAY_NAME_KEY constant should exist.");
            Assert.IsNotNull(avatarId, "AVATAR_ID_KEY constant should exist.");

            Assert.AreEqual("invite_target", inviteTarget.GetValue(null));
            Assert.AreEqual("invite_data", inviteData.GetValue(null));
            Assert.AreEqual("displayName", displayName.GetValue(null));
            Assert.AreEqual("avatarId", avatarId.GetValue(null));
        }

        [Test]
        public void HostConnectionService_InviteFormatString_MatchesParseExpectation()
        {
            // The format used in SendInviteAsync:
            // $"{connectionData.LocalPlayerId}|{_partySession.Id}|{connectionData.LocalDisplayName}|{connectionData.LocalAvatarId}"
            //
            // ParseInvite expects: parts[0]=hostPlayerId, parts[1]=sessionId, parts[2]=displayName, parts[3]=avatarId

            string hostId = "testHost";
            string sessionId = "testSession";
            string displayName = "TestPilot";
            int avatarId = 42;

            string formatted = $"{hostId}|{sessionId}|{displayName}|{avatarId}";
            var parsed = InvokeParseInvite(formatted);

            Assert.IsTrue(parsed.HasValue);
            Assert.AreEqual(hostId, parsed.Value.HostPlayerId);
            Assert.AreEqual(sessionId, parsed.Value.PartySessionId);
            Assert.AreEqual(displayName, parsed.Value.HostDisplayName);
            Assert.AreEqual(avatarId, parsed.Value.HostAvatarId);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendsInitializer — API Contract
        // ─────────────────────────────────────────────────────────────────────

        #region FriendsInitializer API Contract

        [Test]
        public void FriendsInitializer_HasPresenceMethods()
        {
            var type = typeof(CosmicShore.Gameplay.FriendsInitializer);

            Assert.IsNotNull(type.GetMethod("SetPresenceInMenu",
                BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(type.GetMethod("SetPresenceInGame",
                BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(type.GetMethod("SetPresenceOffline",
                BindingFlags.Public | BindingFlags.Instance));
        }

        [Test]
        public void FriendsInitializer_HasAuthEventHandlers()
        {
            var type = typeof(CosmicShore.Gameplay.FriendsInitializer);

            Assert.IsNotNull(type.GetMethod("HandleSignedInEvent",
                BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(type.GetMethod("HandleSignedOutEvent",
                BindingFlags.Public | BindingFlags.Instance));
        }

        #endregion
    }
}
