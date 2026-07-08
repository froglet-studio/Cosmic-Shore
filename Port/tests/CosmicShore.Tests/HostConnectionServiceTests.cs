using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// HostConnectionService — the party orchestrator's LIVE flows driven through
// fake lobby/session services (the [Inject] seams): invite send/cancel with
// property publication, the refresh cycle's online-player diff + incoming-
// invite detection/dedup, decline, and the IPartyStateQuery surface.
// ─────────────────────────────────────────────────────────────────────────────

sealed class HcsPlayer : IReadOnlyPlayer
{
    public string Id { get; init; }
    public Dictionary<string, PlayerProperty> Props { get; } = new();
    public IReadOnlyDictionary<string, PlayerProperty> Properties => Props;
}

sealed class HcsCurrentPlayer : CosmicShore.Engine.Networking.IPlayer
{
    public string Id { get; init; } = "local";
    public Dictionary<string, PlayerProperty> Props { get; } = new();
    public IReadOnlyDictionary<string, PlayerProperty> Properties => Props;
    public void SetProperty(string key, PlayerProperty property) => Props[key] = property;
}

sealed class HcsLobby : IHostSession
{
    public string Id => "presence-lobby";
    public string Code => "CODE";
    public bool IsHost => true;
    public int MaxPlayers => 100;
    public int PlayerCount => Roster.Count;
    public event Action Deleted { add { } remove { } }
    public event Action<string> PlayerLeaving { add { } remove { } }
    public List<IReadOnlyPlayer> Roster { get; } = new();
    public IReadOnlyList<IReadOnlyPlayer> Players => Roster;
    public HcsCurrentPlayer Local { get; } = new();
    public CosmicShore.Engine.Networking.IPlayer CurrentPlayer => Local;
    public Task RefreshAsync() => Task.CompletedTask;
    public Task SaveCurrentPlayerDataAsync() => Task.CompletedTask;
    public Task LeaveAsync() => Task.CompletedTask;
    public IHostSession AsHost() => this;
    public Task DeleteAsync() => Task.CompletedTask;
}

sealed class FakeLobbyService : IPresenceLobbyService
{
    public HcsLobby Lobby = new();
    public ISession ActiveLobby => Lobby;
    public Task JoinOrCreateAsync(int maxPlayers) => Task.CompletedTask;
    public Task ConvergeToCanonicalAsync(int maxPlayers) => Task.CompletedTask;
    public Task LeaveAsync() => Task.CompletedTask;
    public Task RefreshAsync() => Task.CompletedTask;
    public Task SavePropertiesAsync(Dictionary<string, PlayerProperty> properties, string operationName)
    {
        foreach (var kv in properties) Lobby.Local.SetProperty(kv.Key, kv.Value);
        return Task.CompletedTask;
    }
    public void ForceReset() { }
}

sealed class FakePartySession : IHostSession
{
    public string Id => "relay-party-1";
    public string Code => "JOIN";
    public bool IsHost => true;
    public int MaxPlayers => 4;
    public int PlayerCount => 1;
    public event Action Deleted { add { } remove { } }
    public event Action<string> PlayerLeaving { add { } remove { } }
    public IReadOnlyList<IReadOnlyPlayer> Players { get; } = Array.Empty<IReadOnlyPlayer>();
    public CosmicShore.Engine.Networking.IPlayer CurrentPlayer => null;
    public Task RefreshAsync() => Task.CompletedTask;
    public Task SaveCurrentPlayerDataAsync() => Task.CompletedTask;
    public Task LeaveAsync() => Task.CompletedTask;
    public IHostSession AsHost() => this;
    public Task DeleteAsync() => Task.CompletedTask;
}

sealed class FakePartySessionService : IPartySessionService
{
    public ISession Session = new FakePartySession();
    public ISession ActiveSession => Session;
    public float CreatedAtUnscaledTime => 0f;
    public event Action<string> PlayerLeaving { add { } remove { } }
    public Task CreateAsync(int maxPlayers) => Task.CompletedTask;
    public Task JoinByIdAsync(string sessionId) => Task.CompletedTask;
    public Task LeaveAsync() { Session = null; return Task.CompletedTask; }
    public Task RefreshAsync() => Task.CompletedTask;
    public void ClearSession() => Session = null;
}

sealed class FakeTransition : INetworkTransitionService
{
    public Task<bool> ShutdownAsync(float timeoutSeconds, CancellationToken ct) => Task.FromResult(true);
    public Task<bool> WaitForClientConnectionAsync(float timeoutSeconds, CancellationToken ct) => Task.FromResult(true);
    public Task<bool> WaitForSceneSyncAsync(string sceneName, float timeoutSeconds, CancellationToken ct) => Task.FromResult(true);
    public void ClearStaleReferences() { }
}

public class HostConnectionServiceTests : IDisposable
{
    readonly GameLoop loop = new(nameof(HostConnectionServiceTests));

    public HostConnectionServiceTests() => NetworkManager.Singleton = null;

    public void Dispose()
    {
        // Clear the DontDestroyOnLoad singleton so the next test's Awake takes it.
        typeof(HostConnectionService).GetProperty("Instance")!.SetValue(null, null);
        NetworkManager.Singleton = null;
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

    (HostConnectionService svc, HostConnectionDataSO conn, FakeLobbyService lobby,
     FakePartySessionService party, InviteService invites) MakeRig()
    {
        var conn = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        conn.OnHostConnectionEstablished = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        conn.OnHostConnectionLost = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        conn.OnlinePlayers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
        conn.PartyMembers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
        conn.OnPartyMemberJoined = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        conn.OnPartyMemberLeft = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        conn.OnPartyMemberKicked = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        conn.OnInviteReceived = ScriptableObject.CreateInstance<ScriptableEventPartyInviteData>();
        conn.OnInviteSent = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        conn.OnPartyJoinCompleted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        conn.OnInviteResolved = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        conn.LocalPlayerId = "local";
        conn.LocalDisplayName = "Me";
        conn.LocalAvatarId = 1;

        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        authVar.Value = new AuthenticationData
        {
            PlayerId = "local",
            IsSignedIn = false, // Start()'s HandleSignedInEvent early-returns — flows driven directly
            OnSignedIn = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        };

        var lobby = new FakeLobbyService();
        var party = new FakePartySessionService();
        var writer = new LobbyPropertyWriter();
        var bus = new SoapPartyEventBus(conn);
        var invites = new InviteService();

        var go = new GameObject("host-connection-service");
        var svc = go.AddComponent<HostConnectionService>();
        Set(svc, "connectionData", conn);
        Set(svc, "authenticationDataVariable", authVar);
        Set(svc, "_gameData", ScriptableObject.CreateInstance<GameDataSO>());
        Set(svc, "_lobbyService", lobby);
        Set(svc, "_partySessionService", party);
        Set(svc, "_memberService", new PartyMemberService(conn, bus));
        Set(svc, "_networkTransition", new FakeTransition());
        Set(svc, "_acceptanceService", new AcceptanceSignalService());
        Set(svc, "_inviteService", invites);
        Set(svc, "_propertyWriter", writer);
        Set(svc, "_eventBus", bus);
        Set(svc, "_scheduler", new LobbyRefreshScheduler(3f));
        loop.Tick(1f / 60f); // Awake (Instance) + Start (signed-out → idle)

        return (svc, conn, lobby, party, invites);
    }

    static HcsPlayer RemotePlayer(string id, string name = "Guest", string invitePayloads = null)
    {
        var p = new HcsPlayer { Id = id };
        p.Props["displayName"] = new PlayerProperty(name);
        p.Props["avatarId"] = new PlayerProperty("2");
        if (invitePayloads != null)
            p.Props["invite_payloads"] = new PlayerProperty(invitePayloads);
        return p;
    }

    Task RunRefresh(HostConnectionService svc)
    {
        // Drive the private refresh INSIDE a tick (the C4/C6 sync-context discipline),
        // then pump frames until the returned Task completes.
        Task refresh = null;
        var driver = new GameObject("driver").AddComponent<TickDriver>();
        driver.Action = () => refresh =
            (Task)typeof(HostConnectionService)
                .GetMethod("RefreshAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(svc, null);
        loop.Tick(1f / 60f);
        for (int i = 0; i < 120 && !refresh.IsCompleted; i++) loop.Tick(1f / 60f);
        Assert.True(refresh.IsCompleted, "refresh cycle should settle");
        return refresh;
    }

    sealed class TickDriver : MonoBehaviour
    {
        public Action Action;
        void Update() { var a = Action; Action = null; a?.Invoke(); }
    }

    [Fact]
    public async Task SendInvite_TracksPublishes_AndRaisesInviteSent()
    {
        var (svc, conn, lobby, _, invites) = MakeRig();
        conn.OnlinePlayers.Add(new PartyPlayerData("guest-1", "Guest", 2));
        int sent = 0;
        conn.OnInviteSent.OnRaised += _ => sent++;

        await svc.SendInviteAsync("guest-1");

        Assert.True(invites.Contains("guest-1"));
        Assert.Equal(1, sent);
        // The composite payload landed on OUR lobby player with the REAL session id.
        Assert.True(lobby.Lobby.Local.Props.TryGetValue("invite_payloads", out var payload));
        Assert.Contains("guest-1|local|relay-party-1|Me|1", payload.Value);
    }

    [Fact]
    public async Task CancelInvite_ClearsTracker_Republishes_AndFiresCleared()
    {
        var (svc, conn, lobby, _, invites) = MakeRig();
        conn.OnlinePlayers.Add(new PartyPlayerData("guest-1", "Guest", 2));
        string cleared = null;
        svc.OutgoingInviteCleared += id => cleared = id;

        await svc.SendInviteAsync("guest-1");
        await svc.CancelInviteAsync("guest-1");

        Assert.False(invites.Contains("guest-1"));
        Assert.Equal("guest-1", cleared);
        Assert.True(lobby.Lobby.Local.Props.TryGetValue("invite_payloads", out var payload));
        Assert.DoesNotContain("guest-1", payload.Value ?? string.Empty);
    }

    [Fact]
    public async Task Refresh_DiffsOnlinePlayers_ExcludingSelf()
    {
        var (svc, conn, lobby, _, _) = MakeRig();
        lobby.Lobby.Roster.Add(RemotePlayer("local", "Me"));      // self — excluded
        lobby.Lobby.Roster.Add(RemotePlayer("guest-1", "Ada"));
        lobby.Lobby.Roster.Add(RemotePlayer("guest-2", "Grace"));

        await RunRefresh(svc);
        Assert.Equal(2, conn.OnlinePlayers.Count);

        // guest-2 leaves the lobby → next refresh drops them.
        lobby.Lobby.Roster.RemoveAt(2);
        await RunRefresh(svc);
        Assert.Equal(1, conn.OnlinePlayers.Count);
        Assert.Equal("guest-1", conn.OnlinePlayers[0].PlayerId);
    }

    [Fact]
    public async Task Refresh_DetectsIncomingInvite_AndDedupsRepeats()
    {
        var (svc, conn, lobby, _, _) = MakeRig();
        int received = 0;
        conn.OnInviteReceived.OnRaised += _ => received++;

        // A remote host is inviting US: their invite_payloads carries a line targeting "local".
        lobby.Lobby.Roster.Add(RemotePlayer("host-9", "Host",
            invitePayloads: "local|host-9|sess-9|Host|3"));

        await RunRefresh(svc);
        Assert.Equal(1, received);
        Assert.NotNull(svc.LastPendingInvite);
        Assert.Equal("host-9", svc.LastPendingInvite.Value.HostPlayerId);
        Assert.Equal("sess-9", svc.LastPendingInvite.Value.PartySessionId);

        // Same invite on the next refresh — deduped, no second SOAP raise.
        await RunRefresh(svc);
        Assert.Equal(1, received);
    }

    [Fact]
    public async Task DeclineInvite_ResolvesThePendingInvite()
    {
        var (svc, conn, lobby, _, _) = MakeRig();
        int resolved = 0;
        conn.OnInviteResolved.OnRaised += () => resolved++;
        lobby.Lobby.Roster.Add(RemotePlayer("host-9", "Host",
            invitePayloads: "local|host-9|sess-9|Host|3"));
        await RunRefresh(svc);
        Assert.NotNull(svc.LastPendingInvite);

        await svc.DeclineInviteAsync();

        Assert.Null(svc.LastPendingInvite);
        Assert.True(resolved >= 1);
    }

    [Fact]
    public void PartyStateQuery_MirrorsTheStateMachine()
    {
        var (svc, _, _, _, _) = MakeRig();
        IPartyStateQuery query = svc;

        Assert.Equal(PartyState.Disconnected, query.CurrentState);
        Assert.True(svc.StateMachine.TryTransition(PartyState.InPresenceLobby));
        Assert.Equal(PartyState.InPresenceLobby, query.CurrentState);
    }
}
