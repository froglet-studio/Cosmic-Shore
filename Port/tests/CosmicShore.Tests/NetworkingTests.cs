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
