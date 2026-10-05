using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Party-services ring 3 — the live surfaces of PartySessionService (teardown +
// PlayerLeaving relay + transient classification), PresenceLobbyService
// (property save flow + identity property build), and NetworkTransitionService
// (shutdown gate + client-connection wait) against the engine placeholders.
// Session-surface restore (2026-07-09): the create/join/query flows now run
// LIVE against the MultiplayerSdk placeholder — covered by the
// *SessionFlowTests classes below (fake IMultiplayerService via the settable
// MultiplayerService.Instance, per the MultiplayerSessionTests precedent).
// ─────────────────────────────────────────────────────────────────────────────

sealed class Ring3Session : IHostSession
{
    public bool Host = true;
    public string IdValue = "sess-3";
    public string Id => IdValue;
    public string Code => "CODE";
    public bool IsHost => Host;
    public int MaxPlayers => 4;
    public int PlayerCount => 1;
    public event Action Deleted { add { } remove { } }
    public event Action<string> PlayerLeaving;
    public void RaisePlayerLeaving(string id) => PlayerLeaving?.Invoke(id);
    public List<IReadOnlyPlayer> Roster { get; } = new();
    public IReadOnlyList<IReadOnlyPlayer> Players => Roster;
    public RingStubCurrentPlayer Local { get; } = new();
    public CosmicShore.Engine.Networking.IPlayer CurrentPlayer => Local;
    public int Refreshes, Saves, Left, Deletes;
    public System.Threading.Tasks.Task RefreshAsync() { Refreshes++; return System.Threading.Tasks.Task.CompletedTask; }
    public System.Threading.Tasks.Task SaveCurrentPlayerDataAsync() { Saves++; return System.Threading.Tasks.Task.CompletedTask; }
    public System.Threading.Tasks.Task LeaveAsync() { Left++; return System.Threading.Tasks.Task.CompletedTask; }
    public IHostSession AsHost() => this;
    public System.Threading.Tasks.Task DeleteAsync() { Deletes++; return System.Threading.Tasks.Task.CompletedTask; }
    public System.Threading.Tasks.Task RemovePlayerAsync(string playerId) => System.Threading.Tasks.Task.CompletedTask;
}

public class PartySessionServiceTests : IDisposable
{
    readonly GameLoop loop = new(nameof(PartySessionServiceTests));
    public void Dispose() => loop.Dispose();

    (PartySessionService svc, GameDataSO gameData, HostConnectionDataSO conn) MakeRig()
    {
        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        var conn = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        return (new PartySessionService(conn, gameData), gameData, conn);
    }

    [Fact]
    public async System.Threading.Tasks.Task LeaveAsync_AsHost_DeletesTheSession_AndClears()
    {
        var (svc, gameData, _) = MakeRig();
        var session = new Ring3Session { Host = true };
        gameData.ActiveSession = session;

        await svc.LeaveAsync();

        Assert.Equal(1, session.Deletes);   // host deletes rather than leaves
        Assert.Equal(0, session.Left);
        Assert.Null(gameData.ActiveSession);
        Assert.Null(svc.ActiveSession);
    }

    [Fact]
    public async System.Threading.Tasks.Task LeaveAsync_AsMember_LeavesInstead()
    {
        var (svc, gameData, _) = MakeRig();
        var session = new Ring3Session { Host = false };
        gameData.ActiveSession = session;

        await svc.LeaveAsync();

        Assert.Equal(0, session.Deletes);
        Assert.Equal(1, session.Left);
        Assert.Null(gameData.ActiveSession);
    }

    [Fact]
    public async System.Threading.Tasks.Task RefreshAsync_DelegatesToTheSession_NoOpWhenNull()
    {
        var (svc, gameData, _) = MakeRig();
        await svc.RefreshAsync(); // null session — must not throw

        var session = new Ring3Session();
        gameData.ActiveSession = session;
        await svc.RefreshAsync();
        Assert.Equal(1, session.Refreshes);
    }
}

public class PresenceLobbyServiceTests
{
    (PresenceLobbyService svc, HostConnectionDataSO conn, LobbyPropertyWriter writer) MakeRig()
    {
        var conn = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        var writer = new LobbyPropertyWriter();
        return (new PresenceLobbyService(conn, writer), conn, writer);
    }

    static void SetActiveLobby(PresenceLobbyService svc, ISession lobby)
    {
        var f = typeof(PresenceLobbyService).GetField("_activeLobby",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f == null)
            foreach (var fi in typeof(PresenceLobbyService).GetFields(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            { if (typeof(ISession).IsAssignableFrom(fi.FieldType)) { fi.SetValue(svc, lobby); return; } }
        f?.SetValue(svc, lobby);
    }

    [Fact]
    public async System.Threading.Tasks.Task SavePropertiesAsync_WritesThroughTheMutexFlow()
    {
        var (svc, _, _) = MakeRig();
        var lobby = new Ring3Session();
        SetActiveLobby(svc, lobby);

        await svc.SavePropertiesAsync(new Dictionary<string, PlayerProperty>
        {
            ["displayName"] = new PlayerProperty("Ace"),
            ["invite_payloads"] = new PlayerProperty("t|m|s|A|1"),
        }, "test-write");

        Assert.Equal("Ace", lobby.Local.Props["displayName"].Value);
        Assert.Equal("t|m|s|A|1", lobby.Local.Props["invite_payloads"].Value);
        Assert.True(lobby.Saves >= 1);
    }

    [Fact]
    public async System.Threading.Tasks.Task LeaveAsync_HostDeletes_AndClearsActiveLobby()
    {
        var (svc, _, _) = MakeRig();
        var lobby = new Ring3Session { Host = true };
        SetActiveLobby(svc, lobby);

        await svc.LeaveAsync();

        Assert.Equal(1, lobby.Deletes);
        Assert.Null(svc.ActiveLobby);
    }

    [Fact]
    public void ForceReset_ClearsTheLobbyReference_WithoutSdkCalls()
    {
        var (svc, _, _) = MakeRig();
        var lobby = new Ring3Session();
        SetActiveLobby(svc, lobby);

        svc.ForceReset();

        Assert.Null(svc.ActiveLobby);
        Assert.Equal(0, lobby.Left);
        Assert.Equal(0, lobby.Deletes);
    }
}

sealed class Ring3SessionInfo : ISessionInfo
{
    public string Id { get; init; } = "info";
    public DateTime Created { get; init; } = new(2026, 1, 1);
    public int MaxPlayers { get; init; } = 100;
    public int AvailableSlots { get; init; } = 99;
    public bool IsLocked { get; init; }
    public bool HasPassword { get; init; }
}

sealed class Ring3MultiplayerService : IMultiplayerService
{
    public List<ISessionInfo> QueryResults { get; } = new();
    public List<string> JoinAttempts { get; } = new();
    public int CreateCalls, QueryCalls;
    public SessionOptions LastCreateOptions;
    public Func<SessionOptions, ISession> CreateHandler;
    public Func<string, ISession> JoinHandler;
    public Func<Exception> QueryThrows;

    public System.Threading.Tasks.Task<ISession> CreateSessionAsync(SessionOptions options)
    {
        CreateCalls++;
        LastCreateOptions = options;
        var session = CreateHandler != null ? CreateHandler(options) : new Ring3Session();
        return System.Threading.Tasks.Task.FromResult(session);
    }

    public System.Threading.Tasks.Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions options = null)
    {
        JoinAttempts.Add(sessionId);
        return System.Threading.Tasks.Task.FromResult(JoinHandler!(sessionId));
    }

    public System.Threading.Tasks.Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions options)
    {
        QueryCalls++;
        if (QueryThrows != null) throw QueryThrows();
        return System.Threading.Tasks.Task.FromResult(new QuerySessionsResults(QueryResults));
    }
}

/// <summary>
/// PartySessionService's restored UGS create/join flow against the
/// MultiplayerSdk placeholder (session-surface restore 2026-07-09).
/// </summary>
public class PartySessionFlowTests : IDisposable
{
    readonly GameLoop loop = new(nameof(PartySessionFlowTests));
    readonly Ring3MultiplayerService fake = new();

    public PartySessionFlowTests()
    {
        MultiplayerService.Reset();
        MultiplayerService.Instance = fake;
    }

    public void Dispose()
    {
        MultiplayerService.Reset();
        loop.Dispose();
    }

    void Pump(Func<bool> done, int maxFrames = 600)
    {
        for (int i = 0; i < maxFrames && !done(); i++) loop.Tick(1f / 60f);
        Assert.True(done(), "condition should settle within the pump budget");
    }

    (PartySessionService svc, GameDataSO gameData, HostConnectionDataSO conn) MakeRig()
    {
        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        var conn = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        return (new PartySessionService(conn, gameData), gameData, conn);
    }

    [Fact]
    public async System.Threading.Tasks.Task Create_SetsSession_StampsTime_WiresRelay_WithPrivateRelayOptions()
    {
        var (svc, gameData, conn) = MakeRig();
        conn.LocalDisplayName = "Ace";
        conn.LocalAvatarId = 7;
        var session = new Ring3Session();
        fake.CreateHandler = _ => session;
        for (int i = 0; i < 10; i++) loop.Tick(1f / 60f); // advance unscaled time past 0

        await svc.CreateAsync(maxPlayers: 4);

        Assert.Same(session, svc.ActiveSession);
        Assert.Same(session, gameData.ActiveSession); // backed by the shared SO
        Assert.True(svc.CreatedAtUnscaledTime > 0f);

        // The party session is PRIVATE (invite-only) on Relay transport.
        var opts = fake.LastCreateOptions;
        Assert.Equal(4, opts.MaxPlayers);
        Assert.True(opts.IsPrivate);
        Assert.False(opts.IsLocked);
        Assert.True(opts.UseRelay);
        Assert.Null(opts.SessionProperties); // party sessions publish no session properties

        // All 8 identity keys present so no first-refresh false negatives.
        Assert.Equal(8, opts.PlayerProperties.Count);
        Assert.Equal("Ace", opts.PlayerProperties["displayName"].Value);
        Assert.Equal("7", opts.PlayerProperties["avatarId"].Value);
        Assert.Equal(string.Empty, opts.PlayerProperties["invite_payloads"].Value);

        // PlayerLeaving relay: wired on create, unwired by ClearSession.
        string left = null;
        svc.PlayerLeaving += id => left = id;
        session.RaisePlayerLeaving("p9");
        Assert.Equal("p9", left);
        svc.ClearSession();
        left = null;
        session.RaisePlayerLeaving("p9");
        Assert.Null(left);
    }

    [Fact]
    public async System.Threading.Tasks.Task Create_NoOps_WhenASessionIsAlreadyActive()
    {
        var (svc, gameData, _) = MakeRig();
        gameData.ActiveSession = new Ring3Session();

        await svc.CreateAsync(maxPlayers: 4);

        Assert.Equal(0, fake.CreateCalls);
    }

    [Fact]
    public async System.Threading.Tasks.Task Create_RetriesHostConflict_ThenSucceeds()
    {
        var (svc, _, _) = MakeRig();
        int attempts = 0;
        fake.CreateHandler = _ => ++attempts < 2
            ? throw new Exception("NetworkManager is still shutting down")
            : new Ring3Session();

        await svc.CreateAsync(maxPlayers: 4); // host-conflict retry has no delay

        Assert.Equal(2, fake.CreateCalls);
        Assert.NotNull(svc.ActiveSession);
    }

    [Fact]
    public void Create_RateLimited429_BacksOff_ThenSucceeds()
    {
        var (svc, _, _) = MakeRig();
        int attempts = 0;
        // The restored classifier: engine RequestFailedException with ErrorCode 429
        // (the message deliberately avoids host-conflict/transient keywords).
        fake.CreateHandler = _ => ++attempts < 2
            ? throw new CosmicShore.Engine.Services.RequestFailedException(429, "Rate limited by UGS")
            : new Ring3Session();

        var task = svc.CreateAsync(maxPlayers: 4); // 2000ms game-time back-off
        Pump(() => task.IsCompleted);

        Assert.Equal(2, fake.CreateCalls);
        Assert.NotNull(svc.ActiveSession);
    }

    [Fact]
    public void Join_RetriesTransientSdkNre_ThenSucceeds_AndWiresRelay()
    {
        var (svc, _, _) = MakeRig();
        var session = new Ring3Session { Host = false };
        int attempts = 0;
        fake.JoinHandler = id => ++attempts < 2
            ? throw new SessionException(SessionError.Unknown, "lobby events subscription blew up",
                new NullReferenceException("inside the SDK"))
            : session;

        var task = svc.JoinByIdAsync("host-session"); // 1000ms transient back-off
        Pump(() => task.IsCompleted);

        Assert.Equal(new[] { "host-session", "host-session" }, fake.JoinAttempts);
        Assert.Same(session, svc.ActiveSession);

        string left = null;
        svc.PlayerLeaving += id => left = id;
        session.RaisePlayerLeaving("p2");
        Assert.Equal("p2", left);
    }
}

/// <summary>
/// PresenceLobbyService's restored join-or-create / converge flow against the
/// MultiplayerSdk placeholder (session-surface restore 2026-07-09).
/// </summary>
public class PresenceLobbyFlowTests : IDisposable
{
    readonly GameLoop loop = new(nameof(PresenceLobbyFlowTests));
    readonly Ring3MultiplayerService fake = new();

    public PresenceLobbyFlowTests()
    {
        MultiplayerService.Reset();
        MultiplayerService.Instance = fake;
    }

    public void Dispose()
    {
        MultiplayerService.Reset();
        loop.Dispose();
    }

    void Pump(Func<bool> done, int maxFrames = 600)
    {
        for (int i = 0; i < maxFrames && !done(); i++) loop.Tick(1f / 60f);
        Assert.True(done(), "condition should settle within the pump budget");
    }

    (PresenceLobbyService svc, HostConnectionDataSO conn) MakeRig()
    {
        var conn = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        return (new PresenceLobbyService(conn, new LobbyPropertyWriter()), conn);
    }

    [Fact]
    public void JoinOrCreate_NoVisibleLobbies_CreatesOwn_TaggedPresenceLobby()
    {
        var (svc, _) = MakeRig();
        fake.CreateHandler = _ => new Ring3Session { IdValue = "own-lobby" };

        // Query empty → create → 1500ms race settle → converge query (still empty,
        // we hold the canonical) — the full three-step algorithm.
        var task = svc.JoinOrCreateAsync(maxPlayers: 100);
        Pump(() => task.IsCompleted);

        Assert.Equal("own-lobby", svc.ActiveLobby.Id);
        Assert.Equal(1, fake.CreateCalls);
        Assert.Equal(2, fake.QueryCalls); // initial try-join + converge re-query

        // The lobby is PUBLIC and discoverable by the PRESENCE_LOBBY gameMode tag.
        var opts = fake.LastCreateOptions;
        Assert.False(opts.IsPrivate);
        Assert.False(opts.UseRelay); // lobby-only session — no Relay
        var tag = opts.SessionProperties["gameMode"];
        Assert.Equal("PRESENCE_LOBBY", tag.Value);
        Assert.Equal(PropertyIndex.String1, tag.Index);
        Assert.Equal(VisibilityPropertyOptions.Public, tag.Visibility);
        Assert.Equal(8, opts.PlayerProperties.Count);
        Assert.Equal("Pilot", opts.PlayerProperties["displayName"].Value); // empty name → fallback
    }

    [Fact]
    public void JoinOrCreate_JoinsTheSmallestVisibleLobbyId_First()
    {
        var (svc, _) = MakeRig();
        fake.QueryResults.Add(new Ring3SessionInfo { Id = "b-lobby" });
        fake.QueryResults.Add(new Ring3SessionInfo { Id = "a-lobby" });
        fake.JoinHandler = id => new Ring3Session { IdValue = id, Host = false };

        var task = svc.JoinOrCreateAsync(maxPlayers: 100);
        Pump(() => task.IsCompleted);

        // Deterministic ordinal order so simultaneous joiners converge.
        Assert.Equal(new[] { "a-lobby" }, fake.JoinAttempts);
        Assert.Equal("a-lobby", svc.ActiveLobby.Id);
        Assert.Equal(0, fake.CreateCalls); // joined — no create, no converge pass
    }

    [Fact]
    public async System.Threading.Tasks.Task Converge_MigratesToTheCanonicalLobby_AndReleasesOurs()
    {
        var (svc, _) = MakeRig();
        var own = new Ring3Session { IdValue = "z-lobby", Host = true };
        fake.CreateHandler = _ => own;
        await ((System.Threading.Tasks.Task)svc.GetType()
            .GetMethod("CreateAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(svc, new object[] { 100 })!);
        Assert.Equal("z-lobby", svc.ActiveLobby.Id);

        // A rival created "a-lobby" in the same instant — smaller id wins.
        fake.QueryResults.Add(new Ring3SessionInfo { Id = "a-lobby" });
        fake.QueryResults.Add(new Ring3SessionInfo { Id = "z-lobby" });
        fake.JoinHandler = id => new Ring3Session { IdValue = id, Host = false };

        await svc.ConvergeToCanonicalAsync(maxPlayers: 100);

        Assert.Equal(new[] { "a-lobby" }, fake.JoinAttempts);
        Assert.Equal("a-lobby", svc.ActiveLobby.Id);
        Assert.Equal(1, own.Deletes); // race-lost lobby released AFTER the join landed
    }

    [Fact]
    public async System.Threading.Tasks.Task Converge_KeepsOurs_WhenWeAlreadyHoldTheCanonicalId()
    {
        var (svc, _) = MakeRig();
        var own = new Ring3Session { IdValue = "a-lobby", Host = true };
        fake.CreateHandler = _ => own;
        await ((System.Threading.Tasks.Task)svc.GetType()
            .GetMethod("CreateAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(svc, new object[] { 100 })!);

        fake.QueryResults.Add(new Ring3SessionInfo { Id = "b-lobby" }); // larger — not canonical

        await svc.ConvergeToCanonicalAsync(maxPlayers: 100);

        Assert.Empty(fake.JoinAttempts);
        Assert.Equal("a-lobby", svc.ActiveLobby.Id);
        Assert.Equal(0, own.Deletes);
    }

    [Fact]
    public void JoinOrCreate_FallsBackToCreate_WhenTheQueryThrows()
    {
        var (svc, _) = MakeRig();
        fake.QueryThrows = () => new InvalidOperationException("backend unreachable");
        fake.CreateHandler = _ => new Ring3Session { IdValue = "fallback-lobby" };

        var task = svc.JoinOrCreateAsync(maxPlayers: 100);
        Pump(() => task.IsCompleted);

        Assert.Equal("fallback-lobby", svc.ActiveLobby.Id);
        Assert.Equal(1, fake.CreateCalls);
    }
}

public class NetworkTransitionServiceTests : IDisposable
{
    readonly GameLoop loop = new(nameof(NetworkTransitionServiceTests));

    public NetworkTransitionServiceTests() => NetworkManager.Singleton = null;

    public void Dispose()
    {
        NetworkManager.Singleton = null;
        loop.Dispose();
    }

    [Fact]
    public async System.Threading.Tasks.Task ShutdownAsync_CompletesTrue_OnTheSynchronousEngineShutdown()
    {
        var nm = new GameObject("nm").AddComponent<NetworkManager>();
        NetworkManager.Singleton = nm;
        Assert.True(nm.IsListening);

        var svc = new NetworkTransitionService(ScriptableObject.CreateInstance<GameDataSO>());
        var task = svc.ShutdownAsync(timeoutSeconds: 2f, CancellationToken.None);
        for (int i = 0; i < 5 && !task.IsCompleted; i++) loop.Tick(1f / 60f);

        Assert.True(await task);
        Assert.False(nm.IsListening);
    }

    [Fact]
    public async System.Threading.Tasks.Task WaitForClientConnection_SeesTheLiveClientFlags()
    {
        var nm = new GameObject("nm").AddComponent<NetworkManager>();
        NetworkManager.Singleton = nm; // defaults: IsClient + IsListening true

        var svc = new NetworkTransitionService(ScriptableObject.CreateInstance<GameDataSO>());
        var task = svc.WaitForClientConnectionAsync(timeoutSeconds: 1f, CancellationToken.None);
        for (int i = 0; i < 5 && !task.IsCompleted; i++) loop.Tick(1f / 60f);

        Assert.True(await task);
    }

    [Fact]
    public async System.Threading.Tasks.Task WaitForSceneSync_FailsSoft_WithoutANetworkSceneManager()
    {
        var svc = new NetworkTransitionService(ScriptableObject.CreateInstance<GameDataSO>());
        Assert.False(await svc.WaitForSceneSyncAsync("Menu_Main", 1f, CancellationToken.None));
    }
}
