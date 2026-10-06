using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Engine.Services.Friends;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Friends system — FriendsInitializer + FriendsServiceFacade driven LIVE
// through a fake IFriendsService installed in FriendsService.Instance (the
// engine placeholder's settable test hook, NetworkManager.Singleton pattern):
// sign-in bootstrap → facade init → SDK→SOAP sync (discriminator strip,
// availability mapping) → "In Menu" presence; relationship notifications
// raising the right SOAP events; party presence following member join/leave;
// the mutation API syncing after SDK calls; the pre-init guard; and the
// sign-out reset unwiring notifications.
// ─────────────────────────────────────────────────────────────────────────────

sealed class FakeFriendsSdk : IFriendsService
{
    public int InitCalls, RefreshCalls;
    public List<Relationship> FriendsList = new(), Incoming = new(), Outgoing = new(), BlocksList = new();
    public readonly List<(Availability availability, object activity)> PresenceSets = new();
    public readonly List<Availability> AvailabilitySets = new();

    /// <summary>Optional per-call side effect — lets a test mutate the relationship lists the way the real SDK would.</summary>
    public Action<string> AddFriendEffect, BlockEffect;

    public Task InitializeAsync() { InitCalls++; return Task.CompletedTask; }

    public IReadOnlyList<Relationship> Friends => FriendsList;
    public IReadOnlyList<Relationship> IncomingFriendRequests => Incoming;
    public IReadOnlyList<Relationship> OutgoingFriendRequests => Outgoing;
    public IReadOnlyList<Relationship> Blocks => BlocksList;

    public Task AddFriendByNameAsync(string playerName) => Task.CompletedTask;
    public Task AddFriendAsync(string playerId) { AddFriendEffect?.Invoke(playerId); return Task.CompletedTask; }
    public Task DeleteIncomingFriendRequestAsync(string playerId) => Task.CompletedTask;
    public Task DeleteOutgoingFriendRequestAsync(string playerId) => Task.CompletedTask;
    public Task DeleteFriendAsync(string playerId) => Task.CompletedTask;
    public Task AddBlockAsync(string playerId) { BlockEffect?.Invoke(playerId); return Task.CompletedTask; }
    public Task DeleteBlockAsync(string playerId) => Task.CompletedTask;

    public Task SetPresenceAsync<T>(Availability availability, T activity) where T : class
    { PresenceSets.Add((availability, activity)); return Task.CompletedTask; }
    public Task SetPresenceAvailabilityAsync(Availability availability)
    { AvailabilitySets.Add(availability); return Task.CompletedTask; }

    public Task ForceRelationshipsRefreshAsync() { RefreshCalls++; return Task.CompletedTask; }

    public event Action<IRelationshipAddedEvent> RelationshipAdded;
    public event Action<IRelationshipDeletedEvent> RelationshipDeleted;
    public event Action<IPresenceUpdatedEvent> PresenceUpdated;

    sealed class AddedEvt : IRelationshipAddedEvent { public Relationship Relationship { get; init; } }
    sealed class DeletedEvt : IRelationshipDeletedEvent { public Relationship Relationship { get; init; } }

    public void FireAdded(Relationship r) => RelationshipAdded?.Invoke(new AddedEvt { Relationship = r });
    public void FireDeleted(Relationship r) => RelationshipDeleted?.Invoke(new DeletedEvt { Relationship = r });
    public bool HasAddedSubscribers => RelationshipAdded != null;
    // PresenceUpdated is wired/unwired alongside the other two; suppress unused warning.
    public void FirePresence(IPresenceUpdatedEvent e) => PresenceUpdated?.Invoke(e);
}

public class FriendsSystemTests : IDisposable
{
    readonly GameLoop loop = new(nameof(FriendsSystemTests));

    public FriendsSystemTests() => FriendsService.Instance = null;

    public void Dispose()
    {
        FriendsService.Instance = null;
        typeof(HostConnectionService).GetProperty("Instance")!.SetValue(null, null);
        loop.Dispose();
    }

    static void Set(object target, string field, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{field}' not found on {target.GetType().Name}.");
    }

    static Relationship Rel(
        RelationshipType type, string id, string name = null,
        MemberRole role = MemberRole.None,
        Availability availability = Availability.Unknown,
        object activity = null)
        => new()
        {
            Type = type,
            Member = new Member
            {
                Id = id,
                Role = role,
                Profile = name != null ? new Profile(name) : null,
                Presence = new Presence { Availability = availability, Activity = activity },
            },
        };

    sealed class Rig
    {
        public FriendsInitializer Initializer;
        public FriendsServiceFacade Facade;
        public FriendsDataSO Data;
        public HostConnectionDataSO Conn;
        public FakeFriendsSdk Sdk;
    }

    Rig MakeRig(bool signedIn = true) => MakeRigCore(new FakeFriendsSdk(), signedIn);

    [Fact]
    public void Start_WhenAlreadySignedIn_InitializesAndSetsMenuPresence()
    {
        var rig = MakeRig();
        Assert.Equal(1, rig.Sdk.InitCalls);
        Assert.True(rig.Facade.IsInitialized);
        Assert.True(rig.Data.IsInitialized);

        // Initial presence: Online / "In Menu" / Menu_Main.
        var (availability, activity) = Assert.Single(rig.Sdk.PresenceSets);
        Assert.Equal(Availability.Online, availability);
        var act = Assert.IsType<FriendPresenceActivity>(activity);
        Assert.Equal("In Menu", act.Status);
        Assert.Equal("Menu_Main", act.Scene);
    }

    [Fact]
    public void Init_SyncsSdkRelationships_StrippingDiscriminators()
    {
        var sdk = new FakeFriendsSdk();
        sdk.FriendsList.Add(Rel(RelationshipType.Friend, "p1", "dragon#1234",
            availability: Availability.Online,
            activity: new FriendPresenceActivity("In Menu", "Menu_Main")));
        sdk.Incoming.Add(Rel(RelationshipType.FriendRequest, "p2", "grace", MemberRole.Source));
        sdk.BlocksList.Add(Rel(RelationshipType.Block, "p3"));

        var rig = MakeRigCore(sdk, signedIn: true);
        var data = rig.Data;

        var friend = Assert.Single(data.Friends);
        Assert.Equal("p1", friend.PlayerId);
        Assert.Equal("dragon", friend.DisplayName);           // "#1234" stripped
        Assert.True(friend.IsOnline);                          // Online → 1
        Assert.Equal("In Menu", friend.ActivityStatus);        // activity payload read back

        var request = Assert.Single(data.IncomingRequests);
        Assert.Equal("grace", request.DisplayName);

        var blocked = Assert.Single(data.BlockedPlayers);
        Assert.Equal("Unknown Pilot", blocked.DisplayName);    // no profile → fallback

        Assert.True(rig.Facade.IsFriend("p1"));
        Assert.True(rig.Facade.IsBlocked("p3"));
        Assert.False(rig.Facade.IsFriend("p3"));
    }

    Rig MakeRigCore(FakeFriendsSdk sdk, bool signedIn)
    {
        FriendsService.Instance = sdk;

        var data = ScriptableObject.CreateInstance<FriendsDataSO>();
        data.Friends = ScriptableObject.CreateInstance<ScriptableListFriendData>();
        data.IncomingRequests = ScriptableObject.CreateInstance<ScriptableListFriendData>();
        data.OutgoingRequests = ScriptableObject.CreateInstance<ScriptableListFriendData>();
        data.BlockedPlayers = ScriptableObject.CreateInstance<ScriptableListFriendData>();
        data.OnFriendAdded = ScriptableObject.CreateInstance<ScriptableEventFriendData>();
        data.OnFriendRemoved = ScriptableObject.CreateInstance<ScriptableEventFriendData>();
        data.OnFriendRequestReceived = ScriptableObject.CreateInstance<ScriptableEventFriendData>();
        data.OnFriendRequestSent = ScriptableObject.CreateInstance<ScriptableEventFriendData>();
        data.OnFriendsServiceReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        authVar.Value = new AuthenticationData
        {
            PlayerId = "local",
            IsSignedIn = signedIn,
            OnSignedIn = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        };

        var conn = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        conn.PartyMembers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
        conn.OnPartyMemberJoined = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        conn.OnPartyMemberLeft = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        conn.LocalPlayerId = "local";

        var facade = new FriendsServiceFacade(authVar, data);

        var go = new GameObject("friends-initializer");
        var initializer = go.AddComponent<FriendsInitializer>();
        Set(initializer, "authenticationDataVariable", authVar);
        Set(initializer, "friendsData", data);
        Set(initializer, "hostConnectionData", conn);
        Set(initializer, "friendsService", facade);

        loop.Tick(1f / 60f);

        return new Rig { Initializer = initializer, Facade = facade, Data = data, Conn = conn, Sdk = sdk };
    }

    [Fact]
    public void RelationshipNotifications_RaiseTheRightSoapEvents()
    {
        var rig = MakeRig();
        FriendData? added = null, requested = null, removed = null;
        rig.Data.OnFriendAdded.OnRaised += d => added = d;
        rig.Data.OnFriendRequestReceived.OnRaised += d => requested = d;
        rig.Data.OnFriendRemoved.OnRaised += d => removed = d;

        // New friendship established.
        var friendRel = Rel(RelationshipType.Friend, "p1", "ada#42");
        rig.Sdk.FriendsList.Add(friendRel);
        rig.Sdk.FireAdded(friendRel);
        Assert.Equal("ada", added?.DisplayName);
        Assert.Single(rig.Data.Friends);                       // resynced

        // Incoming request (them → us: Source role).
        var requestRel = Rel(RelationshipType.FriendRequest, "p2", "grace", MemberRole.Source);
        rig.Sdk.Incoming.Add(requestRel);
        rig.Sdk.FireAdded(requestRel);
        Assert.Equal("grace", requested?.DisplayName);

        // Outgoing request (us → them: Target role) raises NOTHING.
        requested = null;
        rig.Sdk.FireAdded(Rel(RelationshipType.FriendRequest, "p9", "turing", MemberRole.Target));
        Assert.Null(requested);

        // Friend removed.
        rig.Sdk.FriendsList.Remove(friendRel);
        rig.Sdk.FireDeleted(friendRel);
        Assert.Equal("ada", removed?.DisplayName);
        Assert.Empty(rig.Data.Friends);
    }

    [Fact]
    public void PartyPresence_FollowsMemberJoin_AndLastRemoteLeave()
    {
        var rig = MakeRig();
        rig.Sdk.PresenceSets.Clear(); // drop the init-time "In Menu"

        rig.Conn.PartyMembers.Add(new PartyPlayerData("local", "Me", 1));
        rig.Conn.PartyMembers.Add(new PartyPlayerData("guest-1", "Guest", 2));
        rig.Conn.OnPartyMemberJoined.Raise(new PartyPlayerData("guest-1", "Guest", 2));

        var (availability, activity) = Assert.Single(rig.Sdk.PresenceSets);
        Assert.Equal(Availability.Online, availability);
        var act = Assert.IsType<FriendPresenceActivity>(activity);
        Assert.Equal("In Party", act.Status);
        Assert.Equal(2, act.PartyMemberCount);
        Assert.Equal(rig.Conn.MaxPartySlots, act.PartyMaxSlots);

        // Remote member leaves → party is solo again → back to "In Menu".
        rig.Conn.PartyMembers.Remove(new PartyPlayerData("guest-1", "Guest", 2));
        rig.Conn.OnPartyMemberLeft.Raise(new PartyPlayerData("guest-1", "Guest", 2));

        Assert.Equal(2, rig.Sdk.PresenceSets.Count);
        var menuAct = Assert.IsType<FriendPresenceActivity>(rig.Sdk.PresenceSets[1].activity);
        Assert.Equal("In Menu", menuAct.Status);
    }

    [Fact]
    public void AcceptFriendRequest_CallsSdk_AndResyncsLists()
    {
        var sdk = new FakeFriendsSdk();
        var pending = Rel(RelationshipType.FriendRequest, "p2", "grace", MemberRole.Source);
        sdk.Incoming.Add(pending);
        // The real SDK converts the request into a friendship on AddFriendAsync.
        sdk.AddFriendEffect = id =>
        {
            sdk.Incoming.Remove(pending);
            sdk.FriendsList.Add(Rel(RelationshipType.Friend, id, "grace"));
        };
        var rig = MakeRigCore(sdk, signedIn: true);

        rig.Facade.AcceptFriendRequestAsync("p2").GetAwaiter().GetResult();

        Assert.Empty(rig.Data.IncomingRequests);
        var friend = Assert.Single(rig.Data.Friends);
        Assert.Equal("p2", friend.PlayerId);
        Assert.True(rig.Facade.IsFriend("p2"));
    }

    [Fact]
    public void MutationsBeforeInit_ThrowTheGuard()
    {
        var data = ScriptableObject.CreateInstance<FriendsDataSO>();
        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        var facade = new FriendsServiceFacade(authVar, data);

        Assert.False(facade.IsInitialized);
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => facade.SendFriendRequestAsync("p1")).GetAwaiter().GetResult();
        Assert.Contains("not initialized", ex.Message);
        Assert.False(facade.IsFriend("p1")); // queries stay false rather than throwing
    }

    [Fact]
    public void SignedOut_ResetsData_AndUnwiresNotifications()
    {
        var sdk = new FakeFriendsSdk();
        sdk.FriendsList.Add(Rel(RelationshipType.Friend, "p1", "ada"));
        var rig = MakeRigCore(sdk, signedIn: true);
        Assert.Single(rig.Data.Friends);
        Assert.True(sdk.HasAddedSubscribers);

        rig.Initializer.HandleSignedOutEvent();

        Assert.False(rig.Facade.IsInitialized);
        Assert.False(rig.Data.IsInitialized);
        Assert.Empty(rig.Data.Friends);
        Assert.False(sdk.HasAddedSubscribers);                 // events unwired

        // A stray SDK notification after sign-out raises nothing.
        int addedRaises = 0;
        rig.Data.OnFriendAdded.OnRaised += _ => addedRaises++;
        rig.Sdk.FireAdded(Rel(RelationshipType.Friend, "p5", "hopper"));
        Assert.Equal(0, addedRaises);
    }

    [Fact]
    public void Destroy_SetsOfflineAvailability()
    {
        var rig = MakeRig();

        CosmicShore.Engine.Object.Destroy(rig.Initializer.gameObject);
        loop.Tick(1f / 60f);

        Assert.Contains(Availability.Offline, rig.Sdk.AvailabilitySets);
    }
}
