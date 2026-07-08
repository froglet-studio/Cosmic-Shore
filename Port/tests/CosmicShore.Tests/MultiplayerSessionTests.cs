using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// UGS Multiplayer session surface — MultiplayerSetup's matchmaking flow LIVE
// end to end (query → filter → join-with-race-retry → host-fresh with
// rate-limit backoff, plus the session/player property maps), and the
// LocalMultiplayerService's honest single-process semantics.
// ─────────────────────────────────────────────────────────────────────────────

public class MultiplayerSessionTests : IDisposable
{
    readonly GameLoop loop = new(nameof(MultiplayerSessionTests));

    public MultiplayerSessionTests()
    {
        MultiplayerService.Reset();
        AuthenticationService.Reset();
        NetworkManager.Singleton = null;
    }

    public void Dispose()
    {
        MultiplayerService.Reset();
        AuthenticationService.Reset();
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

    static T ForceValue<T>(ScriptableVariable<T> variable, T value) where T : class
    {
        typeof(ScriptableVariable<T>)
            .GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(variable, value);
        return value;
    }

    void Pump(Func<bool> done, int maxFrames = 600)
    {
        for (int i = 0; i < maxFrames && !done(); i++) loop.Tick(1f / 60f);
        Assert.True(done(), "condition should settle within the pump budget");
    }

    // ── fakes ───────────────────────────────────────────────────────────

    sealed class FakeSessionInfo : ISessionInfo
    {
        public string Id { get; init; } = "remote";
        public DateTime Created { get; init; } = new(2026, 1, 1);
        public int MaxPlayers { get; init; } = 4;
        public int AvailableSlots { get; init; } = 3;
        public bool IsLocked { get; init; }
        public bool HasPassword { get; init; }
    }

    sealed class FakeSession : ISession
    {
        public FakeSession(string id) => Id = id;
        public string Id { get; }
        public string Code => "FAKE";
        public bool IsHost => false;
        public int MaxPlayers => 4;
        public int PlayerCount => 1;
        public event Action Deleted { add { } remove { } }
        public event Action<string> PlayerLeaving { add { } remove { } }
        public IReadOnlyList<IReadOnlyPlayer> Players { get; } = new List<IReadOnlyPlayer>();
        public CosmicShore.Engine.Networking.IPlayer CurrentPlayer => null;
        public Task RefreshAsync() => Task.CompletedTask;
        public Task SaveCurrentPlayerDataAsync() => Task.CompletedTask;
        public Task LeaveAsync() => Task.CompletedTask;
        public IHostSession AsHost() => throw new NotSupportedException();
    }

    sealed class FakeMultiplayerService : IMultiplayerService
    {
        public List<ISessionInfo> QueryResults { get; } = new();
        public List<string> JoinAttempts { get; } = new();
        public int CreateCalls, QueryCalls;
        public SessionOptions LastCreateOptions;
        public Func<string, ISession> JoinHandler;
        public Func<SessionOptions, ISession> CreateHandler;

        public Task<ISession> CreateSessionAsync(SessionOptions options)
        {
            CreateCalls++;
            LastCreateOptions = options;
            var session = CreateHandler != null
                ? CreateHandler(options)
                : new FakeSession("fake-hosted");
            return Task.FromResult(session);
        }

        public Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions options = null)
        {
            JoinAttempts.Add(sessionId);
            return Task.FromResult(JoinHandler!(sessionId));
        }

        public Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions options)
        {
            QueryCalls++;
            return Task.FromResult(new QuerySessionsResults(QueryResults));
        }
    }

    // ── rig ─────────────────────────────────────────────────────────────

    sealed class Rig
    {
        public MultiplayerSetup Setup;
        public GameDataSO GameData;
        public NetworkManager Nm;
        public int SessionStartedRaises;
    }

    /// <summary>
    /// A signed-in multiplayer-mode MultiplayerSetup whose Start() dispatches the
    /// matchmaking flow on the first tick (the OnAuthenticationSignedIn fast path).
    /// </summary>
    Rig MakeRig(GameModes mode = GameModes.HexRace, int players = 4)
    {
        var nm = new GameObject("nm").AddComponent<NetworkManager>(); // IsListening=true default
        NetworkManager.Singleton = nm;

        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.IsMultiplayerMode = true;
        gameData.GameMode = mode;
        gameData.SelectedPlayerCount = ScriptableObject.CreateInstance<IntVariable>();
        gameData.SelectedPlayerCount.Value = players;
        gameData.OnSessionStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        ForceValue(authVar, new AuthenticationData
        {
            IsSignedIn = true,
            OnSignedIn = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        });

        var go = new GameObject("multiplayer-setup");
        var setup = go.AddComponent<MultiplayerSetup>();
        Set(setup, "gameData", gameData);
        Set(setup, "authenticationDataVariable", authVar);

        var rig = new Rig { Setup = setup, GameData = gameData, Nm = nm };
        gameData.OnSessionStarted.OnRaised += () => rig.SessionStartedRaises++;
        return rig;
    }

    // ── ExecuteMultiplayerSetup end to end ──────────────────────────────

    [Fact]
    public void NoRemoteSessions_ShutsDownLocalHost_AndHostsFreshLocally()
    {
        var rig = MakeRig();

        Pump(() => rig.GameData.ActiveSession != null);

        // The local host was shut down for the intentional local→Relay transition.
        Assert.False(rig.Nm.IsListening);

        // LocalMultiplayerService: queries see nothing, so the flow converges on
        // hosting a fresh in-process session with a deterministic id.
        Assert.Equal("local-session-1", rig.GameData.ActiveSession.Id);
        Assert.True(rig.GameData.ActiveSession.IsHost);
        Assert.Equal(1, rig.SessionStartedRaises);
    }

    [Fact]
    public void ExistingPartySession_FastPath_SkipsMatchmakingEntirely()
    {
        var fake = new FakeMultiplayerService();
        MultiplayerService.Instance = fake;

        var rig = MakeRig();
        rig.GameData.ActiveSession = new FakeSession("party-handoff");

        Pump(() => rig.SessionStartedRaises > 0);

        // The Relay transport is already active — no shutdown, no query, no create.
        Assert.True(rig.Nm.IsListening);
        Assert.Equal(0, fake.QueryCalls);
        Assert.Equal(0, fake.CreateCalls);
        Assert.Equal("party-handoff", rig.GameData.ActiveSession.Id);
    }

    [Fact]
    public void HostOptions_CarryTheSessionAndPlayerPropertyMaps()
    {
        AuthenticationService.Instance.PlayerName = "TestPilot";
        var fake = new FakeMultiplayerService();
        MultiplayerService.Instance = fake;

        var rig = MakeRig(GameModes.MultiplayerJoust, players: 3);

        Pump(() => rig.GameData.ActiveSession != null);
        var opts = fake.LastCreateOptions;

        Assert.Equal(3, opts.MaxPlayers);
        Assert.False(opts.IsLocked);
        Assert.False(opts.IsPrivate);
        Assert.True(opts.UseRelay); // WithRelayNetwork() records the transport request

        // GetSessionProperties: gameMode → String1 (Public), maxPlayers → String2 (Public).
        var gameMode = opts.SessionProperties["gameMode"];
        Assert.Equal("MultiplayerJoust", gameMode.Value);
        Assert.Equal(VisibilityPropertyOptions.Public, gameMode.Visibility);
        Assert.Equal(PropertyIndex.String1, gameMode.Index);
        var maxPlayers = opts.SessionProperties["maxPlayers"];
        Assert.Equal("3", maxPlayers.Value);
        Assert.Equal(PropertyIndex.String2, maxPlayers.Index);

        // GetPlayerProperties: the authenticated player name, Member-visible.
        var playerName = opts.PlayerProperties["playerName"];
        Assert.Equal("TestPilot", playerName.Value);
        Assert.Equal(VisibilityPropertyOptions.Member, playerName.Visibility);
    }

    [Fact]
    public void JoinRace_FallsThroughAFilledSession_ToTheNextCandidate()
    {
        var fake = new FakeMultiplayerService();
        fake.QueryResults.Add(new FakeSessionInfo { Id = "older", Created = new DateTime(2026, 1, 1) });
        fake.QueryResults.Add(new FakeSessionInfo { Id = "newer", Created = new DateTime(2026, 1, 2) });
        fake.JoinHandler = id => id == "older"
            ? throw new SessionException(SessionError.SessionDeleted, "filled between query and join")
            : new FakeSession(id);
        MultiplayerService.Instance = fake;

        var rig = MakeRig();

        Pump(() => rig.GameData.ActiveSession != null);

        // Oldest first; the race-filled candidate is skipped, not fatal.
        Assert.Equal(new[] { "older", "newer" }, fake.JoinAttempts);
        Assert.Equal("newer", rig.GameData.ActiveSession.Id);
        Assert.Equal(0, fake.CreateCalls);
    }

    [Fact]
    public void UnjoinableSessions_AreFilteredOut_AndWeHostInstead()
    {
        var fake = new FakeMultiplayerService();
        fake.QueryResults.Add(new FakeSessionInfo { Id = "locked", IsLocked = true });
        fake.QueryResults.Add(new FakeSessionInfo { Id = "full", AvailableSlots = 0 });
        fake.QueryResults.Add(new FakeSessionInfo { Id = "passworded", HasPassword = true });
        fake.JoinHandler = id => throw new InvalidOperationException("no candidate should be joined");
        MultiplayerService.Instance = fake;

        var rig = MakeRig();

        Pump(() => rig.GameData.ActiveSession != null);

        Assert.Empty(fake.JoinAttempts);
        Assert.Equal(1, fake.CreateCalls);
        Assert.Equal("fake-hosted", rig.GameData.ActiveSession.Id);
    }

    [Fact]
    public void RateLimitedCreate_BacksOff_ThenSucceeds()
    {
        int attempts = 0;
        var fake = new FakeMultiplayerService
        {
            CreateHandler = _ => ++attempts < 2
                ? throw new Exception("429 Too Many Requests")
                : new FakeSession("after-backoff"),
        };
        MultiplayerService.Instance = fake;

        var rig = MakeRig();

        // The retry waits RATE_LIMIT_BASE_DELAY_MS (2000ms game time = 120 ticks).
        Pump(() => rig.GameData.ActiveSession != null);

        Assert.Equal(2, fake.CreateCalls);
        Assert.Equal("after-backoff", rig.GameData.ActiveSession.Id);
        Assert.Equal(1, rig.SessionStartedRaises);
    }

    // ── LocalMultiplayerService semantics ───────────────────────────────

    [Fact]
    public async Task LocalService_HonestSingleProcessSemantics()
    {
        var svc = new LocalMultiplayerService();

        // Discovery sees no remote sessions.
        var results = await svc.QuerySessionsAsync(new QuerySessionsOptions());
        Assert.Empty(results.Sessions);

        // Cross-process joins fail with SessionNotFound — there is no wire to cross.
        var ex = await Assert.ThrowsAsync<SessionException>(
            () => svc.JoinSessionByIdAsync("somewhere-else"));
        Assert.Equal(SessionError.SessionNotFound, ex.Error);

        // Creation yields deterministic in-process host sessions.
        var first = await svc.CreateSessionAsync(new SessionOptions { MaxPlayers = 6 });
        var second = await svc.CreateSessionAsync(new SessionOptions { MaxPlayers = 2 });
        Assert.Equal("local-session-1", first.Id);
        Assert.Equal("local-session-2", second.Id);
        Assert.Equal(6, first.MaxPlayers);
        Assert.True(first.IsHost);
        Assert.Same(first, first.AsHost());
        Assert.Equal("LOCAL", first.Code);

        // Reset restores the default service AND the id counter (test isolation).
        MultiplayerService.Reset();
        var fresh = await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions());
        Assert.Equal("local-session-1", fresh.Id);
    }
}
