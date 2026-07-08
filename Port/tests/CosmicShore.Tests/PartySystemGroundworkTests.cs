using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Party-system arc groundwork — the no-UGS subset runs live: the PartyState
// machine's transition table, and PartyMemberService reconciling the SOAP
// roster against a session roster (through the real SoapPartyEventBus +
// HostConnectionDataSO events).
// ─────────────────────────────────────────────────────────────────────────────

public class PartyStateMachineTests
{
    [Fact]
    public void StartsDisconnected_AndFollowsTheHappyPath()
    {
        var sm = new PartyStateMachine();
        Assert.Equal(PartyState.Disconnected, sm.CurrentState);

        // Auth sign-in → lobby join → auto solo Relay host → invite accepted elsewhere.
        Assert.True(sm.TryTransition(PartyState.InPresenceLobby));
        Assert.True(sm.TryTransition(PartyState.HostingParty));
        Assert.Equal(PartyState.HostingParty, sm.CurrentState);
    }

    [Fact]
    public void InvalidTransition_IsRejected_StatePreserved()
    {
        var sm = new PartyStateMachine();
        // Disconnected → InParty skips the join flow — not in the table.
        Assert.False(sm.TryTransition(PartyState.InParty));
        Assert.Equal(PartyState.Disconnected, sm.CurrentState);
    }

    [Fact]
    public void OnStateChanged_ReportsFromAndTo()
    {
        var sm = new PartyStateMachine();
        var observed = new List<(PartyState from, PartyState to)>();
        sm.OnStateChanged += (from, to) => observed.Add((from, to));

        sm.TryTransition(PartyState.InPresenceLobby);

        Assert.Single(observed);
        Assert.Equal(PartyState.Disconnected, observed[0].from);
        Assert.Equal(PartyState.InPresenceLobby, observed[0].to);
    }
}

public class PartyMemberServiceTests
{
    sealed class StubPlayer : IReadOnlyPlayer
    {
        public string Id { get; init; }
        public IReadOnlyDictionary<string, PlayerProperty> Properties { get; init; } =
            new Dictionary<string, PlayerProperty>();
    }

    sealed class StubSession : ISession
    {
        public string Id => "party";
        public string Code => "CODE";
        public bool IsHost => true;
        public int MaxPlayers => 4;
        public int PlayerCount => Players.Count;
        public event Action Deleted { add { } remove { } }
        public event Action<string> PlayerLeaving { add { } remove { } }
        public List<IReadOnlyPlayer> Roster { get; } = new();
        public IReadOnlyList<IReadOnlyPlayer> Players => Roster;
        public CosmicShore.Engine.Networking.IPlayer CurrentPlayer => null;
        public System.Threading.Tasks.Task RefreshAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task SaveCurrentPlayerDataAsync() => System.Threading.Tasks.Task.CompletedTask;
    }

    static (PartyMemberService service, HostConnectionDataSO data) MakeRig()
    {
        var data = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        data.PartyMembers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
        data.OnPartyMemberJoined = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        data.OnPartyMemberLeft = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        return (new PartyMemberService(data, new SoapPartyEventBus(data)), data);
    }

    static StubPlayer Player(string id, string name = null, string avatar = null)
    {
        var props = new Dictionary<string, PlayerProperty>();
        if (name != null) props["displayName"] = new PlayerProperty(name);
        if (avatar != null) props["avatarId"] = new PlayerProperty(avatar);
        return new StubPlayer { Id = id, Properties = props };
    }

    [Fact]
    public void ReadMemberData_ParsesIdentityProperties_WithFallbacks()
    {
        var (service, _) = MakeRig();

        var full = service.ReadMemberData(Player("p1", "Ace", "7"));
        Assert.Equal("p1", full.PlayerId);
        Assert.Equal("Ace", full.DisplayName);
        Assert.Equal(7, full.AvatarId);

        var bare = service.ReadMemberData(Player("p2"));
        Assert.Equal("Unknown Pilot", bare.DisplayName);
        Assert.Equal(0, bare.AvatarId);
    }

    [Fact]
    public void SyncFromSession_AddsJoiners_RemovesLeavers_KeepsLocal()
    {
        var (service, data) = MakeRig();
        var joined = new List<string>();
        var left = new List<string>();
        data.OnPartyMemberJoined.OnRaised += m => joined.Add(m.PlayerId);
        data.OnPartyMemberLeft.OnRaised += m => left.Add(m.PlayerId);

        // Local player is seeded and must never be evicted by the reconcile.
        data.PartyMembers.Add(new PartyPlayerData("local", "Me", 1));

        var session = new StubSession();
        session.Roster.Add(Player("local", "Me", "1"));
        session.Roster.Add(Player("guest", "Guest", "2"));

        var joinedIds = service.SyncFromSession(session, "local");
        Assert.Equal(new[] { "guest" }, joinedIds);
        Assert.Equal(new[] { "guest" }, joined);
        Assert.Equal(2, data.PartyMembers.Count);

        // Guest drops from the session roster → reconcile removes them, local stays.
        session.Roster.RemoveAt(1);
        service.SyncFromSession(session, "local");
        Assert.Equal(new[] { "guest" }, left);
        Assert.Equal(1, data.PartyMembers.Count);
        Assert.Equal("local", data.PartyMembers[0].PlayerId);
    }
}
