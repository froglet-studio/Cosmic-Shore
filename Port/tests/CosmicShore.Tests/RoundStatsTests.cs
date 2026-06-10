using CosmicShore.Data;

namespace CosmicShore.Tests;

/// <summary>
/// Behavioral tests for the verbatim-ported RoundStats. Validates both lifecycle modes:
/// unspawned (single-player/local — setters raise events directly) and spawned server
/// (setters push to NetworkVariables whose change callbacks raise the events).
/// </summary>
public class RoundStatsTests
{
    [Fact]
    public void Unspawned_ScoreSetter_RaisesScoreAndAnyStatChanged()
    {
        var stats = new RoundStats();
        bool scoreChanged = false;
        IRoundStats anyStatSource = null;
        stats.OnScoreChanged += () => scoreChanged = true;
        stats.OnAnyStatChanged += s => anyStatSource = s;

        stats.Score = 100f;

        Assert.True(scoreChanged);
        Assert.Same(stats, anyStatSource);
        Assert.Equal(100f, stats.Score);
    }

    [Fact]
    public void Unspawned_CrystalsCollected_RaisesSpecificEvent()
    {
        var stats = new RoundStats();
        int observed = -1;
        stats.OnCrystalsCollectedChanged += s => observed = s.CrystalsCollected;

        stats.CrystalsCollected = 7;

        Assert.Equal(7, observed);
    }

    [Fact]
    public void Unspawned_JoustCollisions_AlwaysRaises()
    {
        var stats = new RoundStats();
        int fires = 0;
        stats.OnJoustCollisionChanged += _ => fires++;

        stats.JoustCollisions = 1;
        stats.JoustCollisions = 2;

        Assert.Equal(2, fires);
    }

    [Fact]
    public void SpawnedServer_ScoreSetter_RaisesViaReplicationCallback()
    {
        var stats = new RoundStats();
        stats.Spawn(isServer: true, isClient: true);

        bool scoreChanged = false;
        stats.OnScoreChanged += () => scoreChanged = true;

        stats.Score = 50f;

        // On the server the setter writes the NetworkVariable, whose change
        // callback (wired in OnNetworkSpawn) fires the event.
        Assert.True(scoreChanged);
        Assert.Equal(50f, stats.Score);
    }

    [Fact]
    public void SpawnedServer_NameAndDomain_PropagateThroughNetworkVariables()
    {
        var stats = new RoundStats();
        stats.Spawn(isServer: true, isClient: true);

        stats.Name = "TestPilot";
        stats.Domain = Domains.Gold;

        Assert.Equal("TestPilot", stats.Name);
        Assert.Equal(Domains.Gold, stats.Domain);
    }

    [Fact]
    public void SpawnedServer_BlocksCreated_RaisesSpecificAndAnyStat()
    {
        var stats = new RoundStats();
        stats.Spawn(isServer: true, isClient: true);

        int specific = 0, any = 0;
        stats.OnBlocksCreatedChanged += _ => specific++;
        stats.OnAnyStatChanged += _ => any++;

        stats.BlocksCreated = 3;

        Assert.Equal(1, specific);
        Assert.Equal(1, any);
        Assert.Equal(3, stats.BlocksCreated);
    }

    [Fact]
    public void Cleanup_ResetsAllStats_PreservesIdentity()
    {
        IRoundStats stats = new RoundStats();
        stats.Name = "Keep";
        stats.Domain = Domains.Jade;
        stats.Score = 10f;
        stats.BlocksCreated = 5;
        stats.CrystalsCollected = 3;
        stats.VolumeCreated = 99.5f;
        stats.JoustCollisions = 2;
        stats.Button3AbilityActiveTime = 1.5f;

        stats.Cleanup();

        Assert.Equal(0f, stats.Score);
        Assert.Equal(0, stats.BlocksCreated);
        Assert.Equal(0, stats.CrystalsCollected);
        Assert.Equal(0f, stats.VolumeCreated);
        Assert.Equal(0, stats.JoustCollisions);
        Assert.Equal(0f, stats.Button3AbilityActiveTime);
        // Cleanup intentionally does not clear identity.
        Assert.Equal("Keep", stats.Name);
        Assert.Equal(Domains.Jade, stats.Domain);
    }
}
