using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests;

/// <summary>NetworkConfig.GetConfig: the fingerprint peers compare (the game logs it for B35's "NetworkConfig mismatch").</summary>
public class NetworkConfigHashTests
{
    [Fact]
    public void SameSettings_SameHash_DifferentSettings_DifferentHash()
    {
        var a = new NetworkConfig { ProtocolVersion = 3, TickRate = 30 };
        var b = new NetworkConfig { ProtocolVersion = 3, TickRate = 30 };
        Assert.Equal(a.GetConfig(), b.GetConfig());
        // Negative controls: each setting peers must agree on moves the hash.
        Assert.NotEqual(a.GetConfig(), new NetworkConfig { ProtocolVersion = 4, TickRate = 30 }.GetConfig());
        Assert.NotEqual(a.GetConfig(), new NetworkConfig { ProtocolVersion = 3, TickRate = 60 }.GetConfig());
        Assert.NotEqual(a.GetConfig(), new NetworkConfig { ProtocolVersion = 3, TickRate = 30, ConnectionApproval = false }.GetConfig());
    }

    [Fact]
    public void EmptyRegistry_HasNoOverrideLinks()
        => Assert.Empty(new NetworkConfig().Prefabs.NetworkPrefabOverrideLinks);
}
