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
// ─────────────────────────────────────────────────────────────────────────────

sealed class Ring3Session : IHostSession
{
    public bool Host = true;
    public string Id => "sess-3";
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
