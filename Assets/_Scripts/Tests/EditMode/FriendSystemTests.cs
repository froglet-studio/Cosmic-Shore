using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Friend System Tests — Comprehensive coverage of the UGS Friends integration.
    ///
    /// WHY THIS MATTERS:
    /// The friend system is the social backbone connecting players. FriendData flows
    /// through SOAP lists, events, UI views, and the invite pipeline. These tests
    /// cover: struct construction, equality contracts (HashSet/Dictionary compat),
    /// IsOnline availability logic, FriendPresenceActivity DataContract compliance,
    /// FriendsDataSO state management, and API surface contracts for all friend
    /// system classes that depend on runtime/SDK access.
    /// </summary>
    [TestFixture]
    public class FriendSystemTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // FriendData — Construction
        // ─────────────────────────────────────────────────────────────────────

        #region FriendData Construction

        [Test]
        public void FriendData_Constructor_SetsAllFields()
        {
            var data = new FriendData("player-123", "TestPilot", 1, "In Menu");

            Assert.AreEqual("player-123", data.PlayerId);
            Assert.AreEqual("TestPilot", data.DisplayName);
            Assert.AreEqual(1, data.Availability);
            Assert.AreEqual("In Menu", data.ActivityStatus);
        }

        [Test]
        public void FriendData_Constructor_DefaultsAvailabilityToZero()
        {
            var data = new FriendData("player-123", "TestPilot");

            Assert.AreEqual(0, data.Availability,
                "Availability should default to 0 (Unknown) when not specified.");
        }

        [Test]
        public void FriendData_Constructor_DefaultsActivityStatusToEmpty()
        {
            var data = new FriendData("player-123", "TestPilot");

            Assert.AreEqual("", data.ActivityStatus,
                "ActivityStatus should default to empty string when not specified.");
        }

        [Test]
        public void FriendData_Constructor_AcceptsNullPlayerId()
        {
            var data = new FriendData(null, "TestPilot");

            Assert.IsNull(data.PlayerId);
        }

        [Test]
        public void FriendData_Constructor_AcceptsNullDisplayName()
        {
            var data = new FriendData("player-123", null);

            Assert.IsNull(data.DisplayName);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendData — Equality Contract
        // ─────────────────────────────────────────────────────────────────────

        #region FriendData Equality

        [Test]
        public void FriendData_Equality_SamePlayerId_AreEqual()
        {
            var a = new FriendData("player-123", "PilotA", 1, "In Menu");
            var b = new FriendData("player-123", "PilotB", 5, "Offline");

            Assert.AreEqual(a, b,
                "FriendData equality should be by PlayerId only.");
        }

        [Test]
        public void FriendData_Equality_DifferentPlayerId_AreNotEqual()
        {
            var a = new FriendData("player-123", "SameName", 1, "In Menu");
            var b = new FriendData("player-456", "SameName", 1, "In Menu");

            Assert.AreNotEqual(a, b,
                "FriendData with different PlayerIds should not be equal.");
        }

        [Test]
        public void FriendData_GetHashCode_SamePlayerId_SameHash()
        {
            var a = new FriendData("player-123", "PilotA", 1, "In Menu");
            var b = new FriendData("player-123", "PilotB", 5, "Offline");

            Assert.AreEqual(a.GetHashCode(), b.GetHashCode(),
                "Same PlayerId must produce the same hash code for HashSet/Dictionary compat.");
        }

        [Test]
        public void FriendData_GetHashCode_NullPlayerId_DoesNotThrow()
        {
            var data = new FriendData(null, "TestPilot");

            Assert.DoesNotThrow(() => data.GetHashCode(),
                "Null PlayerId should produce 0 hash, not throw.");
            Assert.AreEqual(0, data.GetHashCode());
        }

        [Test]
        public void FriendData_Equals_NonFriendDataObject_ReturnsFalse()
        {
            var data = new FriendData("player-123", "TestPilot");

            Assert.IsFalse(data.Equals("not a FriendData"));
            Assert.IsFalse(data.Equals(42));
            Assert.IsFalse(data.Equals(null));
        }

        [Test]
        public void FriendData_HashSet_Deduplicates_ByPlayerId()
        {
            var set = new HashSet<FriendData>
            {
                new("player-123", "PilotA", 1, "In Menu"),
                new("player-123", "PilotB", 5, "Offline"),
                new("player-456", "PilotC", 2, "In Game")
            };

            Assert.AreEqual(2, set.Count,
                "HashSet should deduplicate by PlayerId. Expected 2 unique players.");
        }

        [Test]
        public void FriendData_Dictionary_LookupByPlayerId()
        {
            var dict = new Dictionary<FriendData, string>();
            var key = new FriendData("player-123", "PilotA", 1, "In Menu");
            dict[key] = "value";

            var lookup = new FriendData("player-123", "DifferentName", 5, "Offline");
            Assert.IsTrue(dict.ContainsKey(lookup),
                "Dictionary lookup should find entry by PlayerId regardless of other fields.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendData — IsOnline Logic
        // ─────────────────────────────────────────────────────────────────────

        #region FriendData IsOnline

        [TestCase(0, false, Description = "Unknown → not online")]
        [TestCase(1, true, Description = "Online → online")]
        [TestCase(2, true, Description = "Busy → online")]
        [TestCase(3, true, Description = "Away → online")]
        [TestCase(4, false, Description = "Invisible → not online")]
        [TestCase(5, false, Description = "Offline → not online")]
        [TestCase(-1, false, Description = "Negative → not online")]
        [TestCase(99, false, Description = "Out-of-range → not online")]
        public void FriendData_IsOnline_CorrectForAvailability(int availability, bool expected)
        {
            var data = new FriendData("player-123", "TestPilot", availability);

            Assert.AreEqual(expected, data.IsOnline,
                $"Availability {availability} should have IsOnline={expected}.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendData — Availability Values Documentation
        // ─────────────────────────────────────────────────────────────────────

        #region FriendData Availability Values

        [Test]
        public void FriendData_AvailabilityValues_MatchDocumented()
        {
            // Document the mapping from FriendsServiceFacade.AvailabilityToInt
            // to prevent accidental drift
            Assert.AreEqual(0, new FriendData("p", "n", 0).Availability, "Unknown");
            Assert.AreEqual(1, new FriendData("p", "n", 1).Availability, "Online");
            Assert.AreEqual(2, new FriendData("p", "n", 2).Availability, "Busy");
            Assert.AreEqual(3, new FriendData("p", "n", 3).Availability, "Away");
            Assert.AreEqual(4, new FriendData("p", "n", 4).Availability, "Invisible");
            Assert.AreEqual(5, new FriendData("p", "n", 5).Availability, "Offline");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendPresenceActivity — Construction
        // ─────────────────────────────────────────────────────────────────────

        #region FriendPresenceActivity Construction

        [Test]
        public void FriendPresenceActivity_DefaultConstructor_SetsDefaults()
        {
            var activity = new FriendPresenceActivity();

            Assert.AreEqual("Online", activity.Status);
            Assert.AreEqual("", activity.Scene);
            Assert.AreEqual("", activity.VesselClass);
            Assert.AreEqual("", activity.PartySessionId);
        }

        [Test]
        public void FriendPresenceActivity_ParameterizedConstructor_SetsAllFields()
        {
            var activity = new FriendPresenceActivity(
                "In Game", "Game_Arena", "Manta", "session-abc");

            Assert.AreEqual("In Game", activity.Status);
            Assert.AreEqual("Game_Arena", activity.Scene);
            Assert.AreEqual("Manta", activity.VesselClass);
            Assert.AreEqual("session-abc", activity.PartySessionId);
        }

        [Test]
        public void FriendPresenceActivity_PartialConstructor_DefaultsOptionalFields()
        {
            var activity = new FriendPresenceActivity("In Menu", "Menu_Main");

            Assert.AreEqual("In Menu", activity.Status);
            Assert.AreEqual("Menu_Main", activity.Scene);
            Assert.AreEqual("", activity.VesselClass);
            Assert.AreEqual("", activity.PartySessionId);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendPresenceActivity — DataContract Compliance
        // ─────────────────────────────────────────────────────────────────────

        #region FriendPresenceActivity DataContract

        [Test]
        public void FriendPresenceActivity_HasDataContractAttribute()
        {
            var type = typeof(FriendPresenceActivity);

            Assert.IsNotNull(
                type.GetCustomAttribute<DataContractAttribute>(),
                "FriendPresenceActivity must have [DataContract] for UGS serialization.");
        }

        [Test]
        public void FriendPresenceActivity_AllProperties_HaveDataMemberAttribute()
        {
            var type = typeof(FriendPresenceActivity);
            var expectedMembers = new[] { "Status", "Scene", "VesselClass", "PartySessionId" };

            foreach (var memberName in expectedMembers)
            {
                var prop = type.GetProperty(memberName);
                Assert.IsNotNull(prop,
                    $"Property '{memberName}' should exist on FriendPresenceActivity.");

                var dataMember = prop.GetCustomAttribute<DataMemberAttribute>();
                Assert.IsNotNull(dataMember,
                    $"Property '{memberName}' must have [DataMember] for UGS serialization.");
            }
        }

        [Test]
        public void FriendPresenceActivity_DataMemberNames_MatchExpected()
        {
            var type = typeof(FriendPresenceActivity);
            var expectedNameMap = new Dictionary<string, string>
            {
                { "Status", "status" },
                { "Scene", "scene" },
                { "VesselClass", "vesselClass" },
                { "PartySessionId", "partySessionId" }
            };

            foreach (var kvp in expectedNameMap)
            {
                var prop = type.GetProperty(kvp.Key);
                var dataMember = prop.GetCustomAttribute<DataMemberAttribute>();

                Assert.AreEqual(kvp.Value, dataMember.Name,
                    $"DataMember name for '{kvp.Key}' should be '{kvp.Value}' " +
                    "to match the UGS wire format.");
            }
        }

        [Test]
        public void FriendPresenceActivity_Status_IsRequired()
        {
            var prop = typeof(FriendPresenceActivity).GetProperty("Status");
            var dataMember = prop.GetCustomAttribute<DataMemberAttribute>();

            Assert.IsTrue(dataMember.IsRequired,
                "Status should be marked IsRequired=true in [DataMember].");
        }

        [Test]
        public void FriendPresenceActivity_OptionalFields_AreNotRequired()
        {
            var optionalFields = new[] { "Scene", "VesselClass", "PartySessionId" };

            foreach (var fieldName in optionalFields)
            {
                var prop = typeof(FriendPresenceActivity).GetProperty(fieldName);
                var dataMember = prop.GetCustomAttribute<DataMemberAttribute>();

                Assert.IsFalse(dataMember.IsRequired,
                    $"'{fieldName}' should not be marked IsRequired in [DataMember].");
            }
        }

        [Test]
        public void FriendPresenceActivity_PropertyCount_IsExactlyFour()
        {
            var type = typeof(FriendPresenceActivity);
            var props = type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.AreEqual(4, props.Length,
                "FriendPresenceActivity should have exactly 4 public properties. " +
                "If you added a new property, add it to these tests.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendsDataSO — Creation & Defaults
        // ─────────────────────────────────────────────────────────────────────

        #region FriendsDataSO Defaults

        FriendsDataSO _friendsDataSO;

        [SetUp]
        public void SetUp()
        {
            _friendsDataSO = ScriptableObject.CreateInstance<FriendsDataSO>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_friendsDataSO);
        }

        [Test]
        public void FriendsDataSO_CreateInstance_IsInitializedIsFalse()
        {
            Assert.IsFalse(_friendsDataSO.IsInitialized,
                "IsInitialized should be false on fresh instance.");
        }

        [Test]
        public void FriendsDataSO_CreateInstance_ListsAreNull()
        {
            // Lists are serialized fields that must be wired in inspector.
            // A fresh CreateInstance will have them null.
            Assert.IsNull(_friendsDataSO.Friends);
            Assert.IsNull(_friendsDataSO.IncomingRequests);
            Assert.IsNull(_friendsDataSO.OutgoingRequests);
            Assert.IsNull(_friendsDataSO.BlockedPlayers);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendsDataSO — Computed Properties
        // ─────────────────────────────────────────────────────────────────────

        #region FriendsDataSO Computed Properties

        [Test]
        public void FriendsDataSO_FriendCount_NullList_ReturnsZero()
        {
            Assert.AreEqual(0, _friendsDataSO.FriendCount,
                "FriendCount should return 0 when Friends list is null.");
        }

        [Test]
        public void FriendsDataSO_IncomingRequestCount_NullList_ReturnsZero()
        {
            Assert.AreEqual(0, _friendsDataSO.IncomingRequestCount,
                "IncomingRequestCount should return 0 when IncomingRequests list is null.");
        }

        [Test]
        public void FriendsDataSO_OnlineFriendCount_NullList_ReturnsZero()
        {
            Assert.AreEqual(0, _friendsDataSO.OnlineFriendCount,
                "OnlineFriendCount should return 0 when Friends list is null.");
        }

        [Test]
        public void FriendsDataSO_FriendCount_WithWiredList_ReturnsCorrectCount()
        {
            var list = ScriptableObject.CreateInstance<ScriptableListFriendData>();
            _friendsDataSO.Friends = list;

            list.Add(new FriendData("p1", "PilotA", 1));
            list.Add(new FriendData("p2", "PilotB", 5));
            list.Add(new FriendData("p3", "PilotC", 2));

            Assert.AreEqual(3, _friendsDataSO.FriendCount);

            UnityEngine.Object.DestroyImmediate(list);
        }

        [Test]
        public void FriendsDataSO_OnlineFriendCount_CountsOnlyOnlineFriends()
        {
            var list = ScriptableObject.CreateInstance<ScriptableListFriendData>();
            _friendsDataSO.Friends = list;

            list.Add(new FriendData("p1", "Online", 1));     // Online
            list.Add(new FriendData("p2", "Busy", 2));       // Busy (online)
            list.Add(new FriendData("p3", "Away", 3));       // Away (online)
            list.Add(new FriendData("p4", "Invisible", 4));  // Invisible (not online)
            list.Add(new FriendData("p5", "Offline", 5));    // Offline (not online)
            list.Add(new FriendData("p6", "Unknown", 0));    // Unknown (not online)

            Assert.AreEqual(3, _friendsDataSO.OnlineFriendCount,
                "Only availability 1 (Online), 2 (Busy), 3 (Away) should count as online.");

            UnityEngine.Object.DestroyImmediate(list);
        }

        [Test]
        public void FriendsDataSO_IncomingRequestCount_WithWiredList_ReturnsCorrectCount()
        {
            var list = ScriptableObject.CreateInstance<ScriptableListFriendData>();
            _friendsDataSO.IncomingRequests = list;

            list.Add(new FriendData("req1", "RequesterA"));
            list.Add(new FriendData("req2", "RequesterB"));

            Assert.AreEqual(2, _friendsDataSO.IncomingRequestCount);

            UnityEngine.Object.DestroyImmediate(list);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendsDataSO — ResetRuntimeData
        // ─────────────────────────────────────────────────────────────────────

        #region FriendsDataSO ResetRuntimeData

        [Test]
        public void FriendsDataSO_ResetRuntimeData_ClearsIsInitialized()
        {
            _friendsDataSO.IsInitialized = true;

            _friendsDataSO.ResetRuntimeData();

            Assert.IsFalse(_friendsDataSO.IsInitialized,
                "ResetRuntimeData must clear IsInitialized.");
        }

        [Test]
        public void FriendsDataSO_ResetRuntimeData_ClearsAllWiredLists()
        {
            var friends = ScriptableObject.CreateInstance<ScriptableListFriendData>();
            var incoming = ScriptableObject.CreateInstance<ScriptableListFriendData>();
            var outgoing = ScriptableObject.CreateInstance<ScriptableListFriendData>();
            var blocked = ScriptableObject.CreateInstance<ScriptableListFriendData>();

            _friendsDataSO.Friends = friends;
            _friendsDataSO.IncomingRequests = incoming;
            _friendsDataSO.OutgoingRequests = outgoing;
            _friendsDataSO.BlockedPlayers = blocked;

            friends.Add(new FriendData("f1", "Friend1"));
            incoming.Add(new FriendData("i1", "InReq1"));
            outgoing.Add(new FriendData("o1", "OutReq1"));
            blocked.Add(new FriendData("b1", "Blocked1"));

            _friendsDataSO.ResetRuntimeData();

            Assert.AreEqual(0, friends.Count, "Friends list should be cleared.");
            Assert.AreEqual(0, incoming.Count, "IncomingRequests list should be cleared.");
            Assert.AreEqual(0, outgoing.Count, "OutgoingRequests list should be cleared.");
            Assert.AreEqual(0, blocked.Count, "BlockedPlayers list should be cleared.");

            UnityEngine.Object.DestroyImmediate(friends);
            UnityEngine.Object.DestroyImmediate(incoming);
            UnityEngine.Object.DestroyImmediate(outgoing);
            UnityEngine.Object.DestroyImmediate(blocked);
        }

        [Test]
        public void FriendsDataSO_ResetRuntimeData_NullLists_DoesNotThrow()
        {
            // All lists null on fresh instance
            _friendsDataSO.IsInitialized = true;

            Assert.DoesNotThrow(() => _friendsDataSO.ResetRuntimeData(),
                "ResetRuntimeData should gracefully handle null lists.");
        }

        [Test]
        public void FriendsDataSO_ResetRuntimeData_ComputedProperties_ReturnZero()
        {
            var friends = ScriptableObject.CreateInstance<ScriptableListFriendData>();
            var incoming = ScriptableObject.CreateInstance<ScriptableListFriendData>();

            _friendsDataSO.Friends = friends;
            _friendsDataSO.IncomingRequests = incoming;

            friends.Add(new FriendData("f1", "Friend1", 1));
            incoming.Add(new FriendData("i1", "InReq1"));

            _friendsDataSO.ResetRuntimeData();

            Assert.AreEqual(0, _friendsDataSO.FriendCount);
            Assert.AreEqual(0, _friendsDataSO.IncomingRequestCount);
            Assert.AreEqual(0, _friendsDataSO.OnlineFriendCount);

            UnityEngine.Object.DestroyImmediate(friends);
            UnityEngine.Object.DestroyImmediate(incoming);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendsDataSO — SOAP Event Fields Exist
        // ─────────────────────────────────────────────────────────────────────

        #region FriendsDataSO SOAP Fields

        [Test]
        public void FriendsDataSO_HasAllExpectedSOAPFields()
        {
            var type = typeof(FriendsDataSO);
            var expectedFields = new[]
            {
                "Friends",
                "IncomingRequests",
                "OutgoingRequests",
                "BlockedPlayers",
                "OnFriendAdded",
                "OnFriendRemoved",
                "OnFriendRequestReceived",
                "OnFriendRequestSent",
                "OnFriendsServiceReady",
                "IsInitialized"
            };

            foreach (var fieldName in expectedFields)
            {
                var field = type.GetField(fieldName,
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(field,
                    $"FriendsDataSO should have public field '{fieldName}'.");
            }
        }

        [Test]
        public void FriendsDataSO_ListFields_AreCorrectType()
        {
            var type = typeof(FriendsDataSO);

            AssertFieldType(type, "Friends", typeof(ScriptableListFriendData));
            AssertFieldType(type, "IncomingRequests", typeof(ScriptableListFriendData));
            AssertFieldType(type, "OutgoingRequests", typeof(ScriptableListFriendData));
            AssertFieldType(type, "BlockedPlayers", typeof(ScriptableListFriendData));
        }

        [Test]
        public void FriendsDataSO_EventFields_AreCorrectType()
        {
            var type = typeof(FriendsDataSO);

            AssertFieldType(type, "OnFriendAdded", typeof(ScriptableEventFriendData));
            AssertFieldType(type, "OnFriendRemoved", typeof(ScriptableEventFriendData));
            AssertFieldType(type, "OnFriendRequestReceived", typeof(ScriptableEventFriendData));
            AssertFieldType(type, "OnFriendRequestSent", typeof(ScriptableEventFriendData));
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // ScriptableListFriendData — SOAP List Behavior
        // ─────────────────────────────────────────────────────────────────────

        #region ScriptableListFriendData

        [Test]
        public void ScriptableListFriendData_AddAndClear_Works()
        {
            var list = ScriptableObject.CreateInstance<ScriptableListFriendData>();

            list.Add(new FriendData("p1", "Pilot1"));
            list.Add(new FriendData("p2", "Pilot2"));

            Assert.AreEqual(2, list.Count);

            list.Clear();

            Assert.AreEqual(0, list.Count);

            UnityEngine.Object.DestroyImmediate(list);
        }

        [Test]
        public void ScriptableListFriendData_Contains_FindsByPlayerId()
        {
            var list = ScriptableObject.CreateInstance<ScriptableListFriendData>();
            list.Add(new FriendData("player-123", "PilotA", 1, "In Menu"));

            // Look up with different display name but same PlayerId
            var lookup = new FriendData("player-123", "DifferentName", 5, "Offline");

            Assert.IsTrue(list.Contains(lookup),
                "ScriptableList.Contains should find by PlayerId (FriendData equality).");

            UnityEngine.Object.DestroyImmediate(list);
        }

        [Test]
        public void ScriptableListFriendData_Remove_RemovesByPlayerId()
        {
            var list = ScriptableObject.CreateInstance<ScriptableListFriendData>();
            list.Add(new FriendData("player-123", "PilotA", 1, "In Menu"));
            list.Add(new FriendData("player-456", "PilotB", 2, "In Game"));

            // Remove with different metadata but same PlayerId
            var toRemove = new FriendData("player-123", "OtherName");
            list.Remove(toRemove);

            Assert.AreEqual(1, list.Count);
            Assert.IsFalse(list.Contains(new FriendData("player-123", "x")),
                "player-123 should have been removed.");
            Assert.IsTrue(list.Contains(new FriendData("player-456", "x")),
                "player-456 should still be present.");

            UnityEngine.Object.DestroyImmediate(list);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendsServiceFacade — API Contract
        // ─────────────────────────────────────────────────────────────────────

        #region FriendsServiceFacade API Contract

        [Test]
        public void FriendsServiceFacade_HasExpectedPublicMethods()
        {
            var type = typeof(Core.FriendsServiceFacade);
            var expectedMethods = new[]
            {
                "InitializeAsync",
                "HandleSignedOut",
                "SendFriendRequestByNameAsync",
                "SendFriendRequestAsync",
                "AcceptFriendRequestAsync",
                "DeclineFriendRequestAsync",
                "CancelFriendRequestAsync",
                "RemoveFriendAsync",
                "BlockPlayerAsync",
                "UnblockPlayerAsync",
                "SetPresenceAsync",
                "SetAvailabilityAsync",
                "RefreshAsync",
                "IsFriend",
                "IsBlocked"
            };

            foreach (var methodName in expectedMethods)
            {
                var method = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(method,
                    $"FriendsServiceFacade should have public method '{methodName}'.");
            }
        }

        [Test]
        public void FriendsServiceFacade_HasIsInitializedProperty()
        {
            var type = typeof(Core.FriendsServiceFacade);
            var prop = type.GetProperty("IsInitialized",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(prop, "FriendsServiceFacade should expose IsInitialized property.");
            Assert.IsTrue(prop.CanRead, "IsInitialized should be readable.");
        }

        [Test]
        public void FriendsServiceFacade_Constructor_AcceptsRequiredDependencies()
        {
            var type = typeof(Core.FriendsServiceFacade);
            var ctor = type.GetConstructor(new[]
            {
                typeof(AuthenticationDataVariable),
                typeof(FriendsDataSO),
                typeof(bool)
            });

            Assert.IsNotNull(ctor,
                "FriendsServiceFacade should have constructor accepting " +
                "(AuthenticationDataVariable, FriendsDataSO, bool).");
        }

        [Test]
        public void FriendsServiceFacade_IsFriend_ReturnsExpectedSignature()
        {
            var method = typeof(Core.FriendsServiceFacade).GetMethod("IsFriend",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.AreEqual(typeof(bool), method.ReturnType,
                "IsFriend should return bool.");
            Assert.AreEqual(1, method.GetParameters().Length,
                "IsFriend should take exactly one parameter.");
            Assert.AreEqual(typeof(string), method.GetParameters()[0].ParameterType,
                "IsFriend parameter should be string (playerId).");
        }

        [Test]
        public void FriendsServiceFacade_IsBlocked_ReturnsExpectedSignature()
        {
            var method = typeof(Core.FriendsServiceFacade).GetMethod("IsBlocked",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.AreEqual(typeof(bool), method.ReturnType,
                "IsBlocked should return bool.");
            Assert.AreEqual(1, method.GetParameters().Length,
                "IsBlocked should take exactly one parameter.");
            Assert.AreEqual(typeof(string), method.GetParameters()[0].ParameterType,
                "IsBlocked parameter should be string (playerId).");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendsInitializer — API Contract
        // ─────────────────────────────────────────────────────────────────────

        #region FriendsInitializer API Contract

        [Test]
        public void FriendsInitializer_HasPresenceMethods()
        {
            var type = typeof(Gameplay.FriendsInitializer);

            Assert.IsNotNull(type.GetMethod("SetPresenceInMenu",
                BindingFlags.Public | BindingFlags.Instance),
                "FriendsInitializer must expose SetPresenceInMenu for SOAP EventListener wiring.");
            Assert.IsNotNull(type.GetMethod("SetPresenceInGame",
                BindingFlags.Public | BindingFlags.Instance),
                "FriendsInitializer must expose SetPresenceInGame for scene transitions.");
            Assert.IsNotNull(type.GetMethod("SetPresenceOffline",
                BindingFlags.Public | BindingFlags.Instance),
                "FriendsInitializer must expose SetPresenceOffline for shutdown.");
        }

        [Test]
        public void FriendsInitializer_HasAuthEventHandlers()
        {
            var type = typeof(Gameplay.FriendsInitializer);

            Assert.IsNotNull(type.GetMethod("HandleSignedInEvent",
                BindingFlags.Public | BindingFlags.Instance),
                "FriendsInitializer must expose HandleSignedInEvent for auth SOAP event.");
            Assert.IsNotNull(type.GetMethod("HandleSignedOutEvent",
                BindingFlags.Public | BindingFlags.Instance),
                "FriendsInitializer must expose HandleSignedOutEvent for auth SOAP event.");
        }

        [Test]
        public void FriendsInitializer_SetPresenceInGame_AcceptsSceneAndVessel()
        {
            var method = typeof(Gameplay.FriendsInitializer).GetMethod("SetPresenceInGame",
                BindingFlags.Public | BindingFlags.Instance);

            var parameters = method.GetParameters();
            Assert.AreEqual(2, parameters.Length,
                "SetPresenceInGame should take 2 parameters: sceneName and vesselClass.");
            Assert.AreEqual(typeof(string), parameters[0].ParameterType);
            Assert.AreEqual(typeof(string), parameters[1].ParameterType);
        }

        [Test]
        public void FriendsInitializer_ExtendsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(Gameplay.FriendsInitializer)),
                "FriendsInitializer must be a MonoBehaviour for scene placement.");
        }

        [Test]
        public void FriendsInitializer_HasSerializedFields()
        {
            var type = typeof(Gameplay.FriendsInitializer);

            var authField = type.GetField("authenticationDataVariable",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(authField,
                "FriendsInitializer should have serialized authenticationDataVariable field.");

            var friendsField = type.GetField("friendsData",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(friendsField,
                "FriendsInitializer should have serialized friendsData field.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendsPanel — API Contract
        // ─────────────────────────────────────────────────────────────────────

        #region FriendsPanel API Contract

        [Test]
        public void FriendsPanel_HasShowAndHideMethods()
        {
            var type = typeof(UI.FriendsPanel);

            Assert.IsNotNull(type.GetMethod("Show",
                BindingFlags.Public | BindingFlags.Instance),
                "FriendsPanel must expose Show() for external callers.");
            Assert.IsNotNull(type.GetMethod("Hide",
                BindingFlags.Public | BindingFlags.Instance),
                "FriendsPanel must expose Hide() for external callers.");
        }

        [Test]
        public void FriendsPanel_ExtendsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(UI.FriendsPanel)),
                "FriendsPanel must be a MonoBehaviour for UI hierarchy.");
        }

        [Test]
        public void FriendsPanel_HasFriendsServiceInjection()
        {
            var field = typeof(UI.FriendsPanel).GetField("friendsService",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field,
                "FriendsPanel should have injected friendsService field.");
            Assert.AreEqual(typeof(Core.FriendsServiceFacade), field.FieldType,
                "friendsService should be of type FriendsServiceFacade.");
        }

        [Test]
        public void FriendsPanel_HasFriendsDataSOField()
        {
            var field = typeof(UI.FriendsPanel).GetField("friendsData",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field,
                "FriendsPanel should have serialized friendsData field.");
            Assert.AreEqual(typeof(FriendsDataSO), field.FieldType);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendEntryView — API Contract
        // ─────────────────────────────────────────────────────────────────────

        #region FriendEntryView API Contract

        [Test]
        public void FriendEntryView_HasPopulateMethod()
        {
            var method = typeof(UI.FriendEntryView).GetMethod("Populate",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method,
                "FriendEntryView must expose Populate() for FriendsPanel to configure entries.");

            var parameters = method.GetParameters();
            Assert.AreEqual(3, parameters.Length,
                "Populate should take (FriendData, Action<FriendData>, Action<FriendData>).");
            Assert.AreEqual(typeof(FriendData), parameters[0].ParameterType);
            Assert.AreEqual(typeof(Action<FriendData>), parameters[1].ParameterType);
            Assert.AreEqual(typeof(Action<FriendData>), parameters[2].ParameterType);
        }

        [Test]
        public void FriendEntryView_HasUpdateStatusMethod()
        {
            var method = typeof(UI.FriendEntryView).GetMethod("UpdateStatus",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method,
                "FriendEntryView must expose UpdateStatus() for presence updates.");

            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual(typeof(FriendData), parameters[0].ParameterType);
        }

        [Test]
        public void FriendEntryView_HasPlayerIdProperty()
        {
            var prop = typeof(UI.FriendEntryView).GetProperty("PlayerId",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(prop,
                "FriendEntryView should expose PlayerId for identification.");
            Assert.AreEqual(typeof(string), prop.PropertyType);
        }

        [Test]
        public void FriendEntryView_ExtendsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(UI.FriendEntryView)));
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendRequestEntryView — API Contract
        // ─────────────────────────────────────────────────────────────────────

        #region FriendRequestEntryView API Contract

        [Test]
        public void FriendRequestEntryView_HasPopulateIncomingMethod()
        {
            var method = typeof(UI.FriendRequestEntryView).GetMethod("PopulateIncoming",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method,
                "FriendRequestEntryView must expose PopulateIncoming() for incoming requests.");

            var parameters = method.GetParameters();
            Assert.AreEqual(3, parameters.Length,
                "PopulateIncoming takes (FriendData, Action<FriendData>, Action<FriendData>).");
            Assert.AreEqual(typeof(FriendData), parameters[0].ParameterType);
            Assert.AreEqual(typeof(Action<FriendData>), parameters[1].ParameterType);
            Assert.AreEqual(typeof(Action<FriendData>), parameters[2].ParameterType);
        }

        [Test]
        public void FriendRequestEntryView_HasPopulateOutgoingMethod()
        {
            var method = typeof(UI.FriendRequestEntryView).GetMethod("PopulateOutgoing",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method,
                "FriendRequestEntryView must expose PopulateOutgoing() for outgoing requests.");

            var parameters = method.GetParameters();
            Assert.AreEqual(2, parameters.Length,
                "PopulateOutgoing takes (FriendData, Action<FriendData>).");
            Assert.AreEqual(typeof(FriendData), parameters[0].ParameterType);
            Assert.AreEqual(typeof(Action<FriendData>), parameters[1].ParameterType);
        }

        [Test]
        public void FriendRequestEntryView_HasPlayerIdProperty()
        {
            var prop = typeof(UI.FriendRequestEntryView).GetProperty("PlayerId",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(prop,
                "FriendRequestEntryView should expose PlayerId for identification.");
            Assert.AreEqual(typeof(string), prop.PropertyType);
        }

        [Test]
        public void FriendRequestEntryView_ExtendsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(UI.FriendRequestEntryView)));
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // AddFriendPanel — API Contract
        // ─────────────────────────────────────────────────────────────────────

        #region AddFriendPanel API Contract

        [Test]
        public void AddFriendPanel_HasShowAndHideMethods()
        {
            var type = typeof(UI.AddFriendPanel);

            Assert.IsNotNull(type.GetMethod("Show",
                BindingFlags.Public | BindingFlags.Instance),
                "AddFriendPanel must expose Show().");
            Assert.IsNotNull(type.GetMethod("Hide",
                BindingFlags.Public | BindingFlags.Instance),
                "AddFriendPanel must expose Hide().");
        }

        [Test]
        public void AddFriendPanel_HasFriendsServiceInjection()
        {
            var field = typeof(UI.AddFriendPanel).GetField("friendsService",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field,
                "AddFriendPanel should have injected friendsService field.");
            Assert.AreEqual(typeof(Core.FriendsServiceFacade), field.FieldType);
        }

        [Test]
        public void AddFriendPanel_ExtendsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(UI.AddFriendPanel)));
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // SOAP Type Wiring — Type Hierarchy
        // ─────────────────────────────────────────────────────────────────────

        #region SOAP Type Hierarchy

        [Test]
        public void ScriptableListFriendData_ExtendsCorrectBase()
        {
            Assert.IsTrue(
                typeof(Obvious.Soap.ScriptableList<FriendData>)
                    .IsAssignableFrom(typeof(ScriptableListFriendData)),
                "ScriptableListFriendData should extend ScriptableList<FriendData>.");
        }

        [Test]
        public void ScriptableEventFriendData_ExtendsCorrectBase()
        {
            Assert.IsTrue(
                typeof(Obvious.Soap.ScriptableEvent<FriendData>)
                    .IsAssignableFrom(typeof(ScriptableEventFriendData)),
                "ScriptableEventFriendData should extend ScriptableEvent<FriendData>.");
        }

        [Test]
        public void EventListenerFriendData_ExtendsCorrectBase()
        {
            Assert.IsTrue(
                typeof(Obvious.Soap.EventListenerGeneric<FriendData>)
                    .IsAssignableFrom(typeof(EventListenerFriendData)),
                "EventListenerFriendData should extend EventListenerGeneric<FriendData>.");
        }

        [Test]
        public void ScriptableListFriendData_HasCreateAssetMenuAttribute()
        {
            var attr = typeof(ScriptableListFriendData)
                .GetCustomAttribute<CreateAssetMenuAttribute>();
            Assert.IsNotNull(attr,
                "ScriptableListFriendData needs [CreateAssetMenu] for Unity asset creation.");
        }

        [Test]
        public void ScriptableEventFriendData_HasCreateAssetMenuAttribute()
        {
            var attr = typeof(ScriptableEventFriendData)
                .GetCustomAttribute<CreateAssetMenuAttribute>();
            Assert.IsNotNull(attr,
                "ScriptableEventFriendData needs [CreateAssetMenu] for Unity asset creation.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // FriendData — Struct Serialization Contract
        // ─────────────────────────────────────────────────────────────────────

        #region FriendData Serialization

        [Test]
        public void FriendData_IsValueType()
        {
            Assert.IsTrue(typeof(FriendData).IsValueType,
                "FriendData should be a struct (value type) for SOAP list compatibility.");
        }

        [Test]
        public void FriendData_HasSerializableAttribute()
        {
            var attr = typeof(FriendData).GetCustomAttribute<SerializableAttribute>();
            Assert.IsNotNull(attr,
                "FriendData must be [Serializable] for Unity serialization.");
        }

        [Test]
        public void FriendData_FieldCount_IsExactlyFour()
        {
            var fields = typeof(FriendData).GetFields(
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.AreEqual(4, fields.Length,
                "FriendData should have exactly 4 backing fields. " +
                "If you added a new field, add it to these tests and update the constructor.");
        }

        [Test]
        public void FriendData_AllBackingFields_HaveSerializeFieldAttribute()
        {
            var fields = typeof(FriendData).GetFields(
                BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<SerializeField>();
                Assert.IsNotNull(attr,
                    $"Backing field '{field.Name}' must have [SerializeField] " +
                    "for Unity SO serialization in ScriptableList assets.");
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void AssertFieldType(Type declaringType, string fieldName, Type expectedType)
        {
            var field = declaringType.GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field,
                $"{declaringType.Name} should have public field '{fieldName}'.");
            Assert.AreEqual(expectedType, field.FieldType,
                $"{declaringType.Name}.{fieldName} should be of type {expectedType.Name}.");
        }
    }
}
