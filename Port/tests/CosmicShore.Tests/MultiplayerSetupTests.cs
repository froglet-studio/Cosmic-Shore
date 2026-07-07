using System;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// MultiplayerSetup host lifecycle — FULLY live after the transport-arc restore:
// EnsureHostStarted wires the real Netcode callbacks onto the engine
// NetworkManager, connection approval auto-creates player objects, and a
// transport failure tears the session down through the real handler body.
// ─────────────────────────────────────────────────────────────────────────────

public class MultiplayerSetupTests : IDisposable
{
    readonly GameLoop loop = new(nameof(MultiplayerSetupTests));

    public MultiplayerSetupTests() => NetworkManager.Singleton = null;

    public void Dispose()
    {
        NetworkManager.Singleton = null;
        loop.Dispose();
    }

    sealed class StubSession : ISession
    {
        public string Id => "stub-session";
        public string Code => "CODE";
        public bool IsHost => true;
        public int MaxPlayers => 4;
        public int PlayerCount => 1;
        public event Action Deleted { add { } remove { } }
        public event Action<string> PlayerLeaving { add { } remove { } }
    }

    (MultiplayerSetup setup, NetworkManager nm, GameDataSO gameData) MakeRig()
    {
        var nm = new GameObject("nm").AddComponent<NetworkManager>();
        NetworkManager.Singleton = nm;

        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnSessionEnded = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        var setup = new GameObject("multiplayer-setup").AddComponent<MultiplayerSetup>();
        Set(setup, "gameData", gameData);
        return (setup, nm, gameData);
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

    static void InvokeEnsureHostStarted(MultiplayerSetup setup) =>
        typeof(MultiplayerSetup).GetMethod("EnsureHostStarted", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(setup, null);

    [Fact]
    public void EnsureHostStarted_WiresApproval_ThatAutoCreatesPlayerObjects()
    {
        var (setup, nm, _) = MakeRig();
        Assert.Null(nm.ConnectionApprovalCallback);

        InvokeEnsureHostStarted(setup);

        // The real OnConnectionApprovalCallback is now on the manager: approve + create.
        Assert.NotNull(nm.ConnectionApprovalCallback);
        var request = new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 3 };
        var response = new NetworkManager.ConnectionApprovalResponse();
        nm.ConnectionApprovalCallback(request, response);
        Assert.True(response.Approved);
        Assert.True(response.CreatePlayerObject);
        Assert.Equal(Vector3.zero, response.Position);
        Assert.Equal(Quaternion.identity, response.Rotation);
        Assert.Null(response.PlayerPrefabHash);
    }

    [Fact]
    public void EnsureHostStarted_Twice_DoesNotDoubleWire()
    {
        var (setup, nm, _) = MakeRig();
        InvokeEnsureHostStarted(setup);
        InvokeEnsureHostStarted(setup); // same manager instance → no re-wire

        Assert.Single(nm.ConnectionApprovalCallback!.GetInvocationList());
    }

    [Fact]
    public void TransportFailure_TearsDown_ShutsDownManager_AndEndsSession()
    {
        var (setup, nm, gameData) = MakeRig();
        InvokeEnsureHostStarted(setup);

        gameData.ActiveSession = new StubSession();
        int sessionEnded = 0;
        gameData.OnSessionEnded.OnRaised += () => sessionEnded++;

        nm.NotifyTransportFailure(); // → the real async handler starts

        // Handler: ActiveSession = null → Shutdown() → await Delay(500ms) → InvokeOnSessionEnded.
        Assert.Null(gameData.ActiveSession);
        Assert.False(nm.IsListening);
        Assert.Equal(0, sessionEnded); // still inside the 500 ms delay

        for (int i = 0; i < 40; i++) loop.Tick(1f / 60f); // ~0.67 s
        Assert.Equal(1, sessionEnded);
    }
}
