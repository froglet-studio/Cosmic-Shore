using System;
using CosmicShore.Engine;
using CosmicShore.Engine.Collections;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests;

public class NetworkVariableTests
{
    [Fact]
    public void ValueChange_FiresCallback_WithPreviousAndNew()
    {
        var nv = new NetworkVariable<int>(5);
        int observedPrev = -1, observedNew = -1;
        nv.OnValueChanged += (prev, next) => { observedPrev = prev; observedNew = next; };

        nv.Value = 9;

        Assert.Equal(5, observedPrev);
        Assert.Equal(9, observedNew);
    }

    [Fact]
    public void SameValue_DoesNotFire()
    {
        var nv = new NetworkVariable<float>(1f);
        int fires = 0;
        nv.OnValueChanged += (_, _) => fires++;

        nv.Value = 1f;

        Assert.Equal(0, fires);
    }

    [Fact]
    public void Permissions_DefaultTo_EveryoneRead_ServerWrite()
    {
        var nv = new NetworkVariable<int>();
        Assert.Equal(NetworkVariableReadPermission.Everyone, nv.ReadPerm);
        Assert.Equal(NetworkVariableWritePermission.Server, nv.WritePerm);
    }

    [Fact]
    public void NamedArgumentConstruction_MatchesPortedCallSites()
    {
        // Mirrors the exact construction pattern used throughout RoundStats.
        var nv = new NetworkVariable<int>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        Assert.Equal(0, nv.Value);
    }
}

public class NetworkBehaviourTests
{
    class TestBehaviour : NetworkBehaviour
    {
        public int SpawnCalls;
        public int DespawnCalls;
        public override void OnNetworkSpawn() => SpawnCalls++;
        public override void OnNetworkDespawn() => DespawnCalls++;
    }

    [Fact]
    public void Spawn_SetsFlags_AndInvokesHook()
    {
        var b = new TestBehaviour();
        Assert.False(b.IsSpawned);

        b.Spawn(isServer: true, isClient: true, isOwner: true);

        Assert.True(b.IsSpawned);
        Assert.True(b.IsServer);
        Assert.True(b.IsHost);
        Assert.Equal(1, b.SpawnCalls);
    }

    [Fact]
    public void Despawn_OnlyFiresWhenSpawned()
    {
        var b = new TestBehaviour();
        b.Despawn();
        Assert.Equal(0, b.DespawnCalls);

        b.Spawn();
        b.Despawn();
        Assert.Equal(1, b.DespawnCalls);
        Assert.False(b.IsSpawned);
    }
}

public class FixedString64BytesTests
{
    [Fact]
    public void ImplicitConversions_RoundTrip()
    {
        FixedString64Bytes fixedStr = "PlayerName";
        string back = fixedStr;
        Assert.Equal("PlayerName", back);
        Assert.Equal("PlayerName", fixedStr.ToString());
    }

    [Fact]
    public void LongString_TruncatesTo61Utf8Bytes()
    {
        FixedString64Bytes fixedStr = new string('a', 100);
        Assert.Equal(61, fixedStr.ToString().Length);
    }

    [Fact]
    public void MultibyteTruncation_RespectsCodePointBoundary()
    {
        // Each '日' is 3 UTF-8 bytes; 30 of them = 90 bytes → truncates to 20 chars (60 bytes).
        FixedString64Bytes fixedStr = new string('日', 30);
        Assert.Equal(20, fixedStr.ToString().Length);
    }

    [Fact]
    public void DefaultValue_IsEmptyString()
    {
        FixedString64Bytes fixedStr = default;
        Assert.Equal(string.Empty, fixedStr.ToString());
        Assert.True(fixedStr.IsEmpty);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// NetworkManager Netcode callback surface (transport arc) — StartHost approval,
// Shutdown teardown, and the transport-driver notify entry points.
// ─────────────────────────────────────────────────────────────────────────────

public class NetworkManagerCallbackTests : IDisposable
{
    readonly GameLoop loop = new(nameof(NetworkManagerCallbackTests));

    public void Dispose()
    {
        NetworkManager.Singleton = null;
        loop.Dispose();
    }

    NetworkManager MakeOffline()
    {
        var nm = new GameObject("nm").AddComponent<NetworkManager>();
        nm.IsServer = false;
        nm.IsClient = false;
        nm.IsListening = false;
        nm.ConnectedClientsIds.Clear();
        return nm;
    }

    [Fact]
    public void StartHost_RunsLocalClientThroughApproval_AndListens()
    {
        var nm = MakeOffline();
        ulong approvedId = ulong.MaxValue;
        nm.ConnectionApprovalCallback += (request, response) =>
        {
            approvedId = request.ClientNetworkId;
            response.Approved = true;
            response.CreatePlayerObject = true;
        };

        Assert.True(nm.StartHost());
        Assert.Equal(0UL, approvedId);        // the host's own client id
        Assert.True(nm.IsListening);
        Assert.True(nm.IsHost);
        Assert.Contains(0UL, nm.ConnectedClientsIds);
    }

    [Fact]
    public void StartHost_RejectedApproval_DoesNotStart()
    {
        var nm = MakeOffline();
        nm.ConnectionApprovalCallback += (request, response) => response.Approved = false;

        Assert.False(nm.StartHost());
        Assert.False(nm.IsListening);
        Assert.False(nm.IsHost);
    }

    [Fact]
    public void StartHost_WithoutApprovalCallback_StartsByDefault()
    {
        var nm = MakeOffline();
        Assert.True(nm.StartHost());
        Assert.True(nm.IsListening);
    }

    [Fact]
    public void StartHost_WhenAlreadyListening_ReturnsFalse()
    {
        var nm = MakeOffline();
        Assert.True(nm.StartHost());
        Assert.False(nm.StartHost());
    }

    [Fact]
    public void Shutdown_StopsListening_ClearsClientTables_Synchronously()
    {
        var nm = MakeOffline();
        nm.StartHost();
        nm.ConnectedClients[0] = new NetworkClient { ClientId = 0 };
        nm.ConnectedClientsList.Add(nm.ConnectedClients[0]);

        nm.Shutdown();

        // The original's `await WaitUntil(() => !IsListening)` completes on first check.
        Assert.False(nm.IsListening);
        Assert.False(nm.IsServer);
        Assert.False(nm.IsClient);
        Assert.Empty(nm.ConnectedClients);
        Assert.Empty(nm.ConnectedClientsList);
        Assert.Empty(nm.ConnectedClientsIds);
    }

    [Fact]
    public void NotifyEntryPoints_RaiseTheNetcodeCallbacks()
    {
        var nm = MakeOffline();
        ulong dropped = 0; int failures = 0;
        nm.OnClientDisconnectCallback += id => dropped = id;
        nm.OnTransportFailure += () => failures++;

        nm.NotifyClientDisconnect(7);
        nm.NotifyTransportFailure();

        Assert.Equal(7UL, dropped);
        Assert.Equal(1, failures);
    }
}
