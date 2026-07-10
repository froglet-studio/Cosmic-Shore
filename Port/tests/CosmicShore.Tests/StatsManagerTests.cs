using System;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// StatsManager unit (2026-07-10) — the REAL per-round stats aggregator
// (replacing the type-preserving shell). Covers the live lanes: crystal
// per-element roll-ups (Omni vs the four elementals, None a no-op); prism
// create/destroy accounting with the friendly-vs-hostile attribution rules
// (self-destruction and same-domain are friendly; cross-domain is hostile;
// the victim's remaining prisms/volume always decrement); restore / volume-
// modify / steal transfers; skimmer + joust collision counters (the joust
// miss lane warns, never throws); per-control-type ability durations; the
// per-cell lifeform counts; unknown players never throwing anywhere; and the
// server-only record gate driven by the NetcodeHooks spawn hook.
// ─────────────────────────────────────────────────────────────────────────────

public class StatsManagerTests : IDisposable
{
    readonly GameLoop loop = new(nameof(StatsManagerTests));

    readonly GameDataSO gameData;
    readonly CellRuntimeDataSO cellData;
    readonly StatsManager stats;
    readonly RoundStats jadeA;
    readonly RoundStats jadeB;
    readonly RoundStats rubyC;

    public StatsManagerTests()
    {
        gameData = ScriptableObject.CreateInstance<GameDataSO>();
        jadeA = new RoundStats { Name = "A", Domain = Domains.Jade };
        jadeB = new RoundStats { Name = "B", Domain = Domains.Jade };
        rubyC = new RoundStats { Name = "C", Domain = Domains.Ruby };
        gameData.RoundStatsList.Add(jadeA);
        gameData.RoundStatsList.Add(jadeB);
        gameData.RoundStatsList.Add(rubyC);

        cellData = ScriptableObject.CreateInstance<CellRuntimeDataSO>();

        var go = new GameObject("stats-manager");
        go.SetActive(false);
        stats = go.AddComponent<StatsManager>();
        Set(stats, "gameData", gameData);
        Set(stats, "cellData", cellData);
        go.SetActive(true);
    }

    public void Dispose() => loop.Dispose();

    static void Set(object target, string field, object value)
        => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);

    [Fact]
    public void CrystalCollected_RollsUpPerElement()
    {
        stats.CrystalCollected(new CrystalStats { PlayerName = "A", Element = Element.Omni });
        Assert.Equal(1, jadeA.CrystalsCollected);
        Assert.Equal(1, jadeA.OmniCrystalsCollected);
        Assert.Equal(0, jadeA.ElementalCrystalsCollected);

        stats.CrystalCollected(new CrystalStats { PlayerName = "A", Element = Element.Charge, Value = 2f });
        stats.CrystalCollected(new CrystalStats { PlayerName = "A", Element = Element.Mass, Value = 3f });
        stats.CrystalCollected(new CrystalStats { PlayerName = "A", Element = Element.Space, Value = 4f });
        stats.CrystalCollected(new CrystalStats { PlayerName = "A", Element = Element.Time, Value = 5f });

        Assert.Equal(5, jadeA.CrystalsCollected);
        Assert.Equal(4, jadeA.ElementalCrystalsCollected);
        Assert.Equal(2f, jadeA.ChargeCrystalValue);
        Assert.Equal(3f, jadeA.MassCrystalValue);
        Assert.Equal(4f, jadeA.SpaceCrystalValue);
        Assert.Equal(5f, jadeA.TimeCrystalValue);

        // Element.None counts the collection but rolls up nothing further.
        stats.CrystalCollected(new CrystalStats { PlayerName = "A", Element = Element.None });
        Assert.Equal(6, jadeA.CrystalsCollected);
        Assert.Equal(4, jadeA.ElementalCrystalsCollected);

        // Unknown player: silently ignored, nothing throws.
        stats.CrystalCollected(new CrystalStats { PlayerName = "ghost", Element = Element.Omni });
    }

    [Fact]
    public void PrismDestroyed_AttributesFriendlyVsHostile_ByNameAndDomain()
    {
        // Cross-domain: hostile for the attacker; victim loses remaining.
        stats.PrismCreated(new PrismStats { OwnName = "A", Volume = 10f });
        stats.PrismDestroyed(new PrismStats { OwnName = "A", AttackerName = "C", Volume = 10f });
        Assert.Equal(1, rubyC.BlocksDestroyed);
        Assert.Equal(1, rubyC.HostilePrismsDestroyed);
        Assert.Equal(10f, rubyC.HostileVolumeDestroyed);
        Assert.Equal(10f, rubyC.TotalVolumeDestroyed);
        Assert.Equal(0, rubyC.FriendlyPrismsDestroyed);
        Assert.Equal(0, jadeA.PrismsRemaining);       // created 1, lost 1
        Assert.Equal(0f, jadeA.VolumeRemaining);

        // Same-domain teammate: friendly.
        stats.PrismDestroyed(new PrismStats { OwnName = "A", AttackerName = "B", Volume = 4f });
        Assert.Equal(1, jadeB.FriendlyPrismsDestroyed);
        Assert.Equal(4f, jadeB.FriendlyVolumeDestroyed);
        Assert.Equal(0, jadeB.HostilePrismsDestroyed);

        // Self-destruction: friendly by name even before the domain check.
        stats.PrismDestroyed(new PrismStats { OwnName = "C", AttackerName = "C", Volume = 2f });
        Assert.Equal(1, rubyC.FriendlyPrismsDestroyed);

        // Unknown attacker: only the victim's remaining decrements.
        stats.PrismDestroyed(new PrismStats { OwnName = "A", AttackerName = "fauna", Volume = 1f });
        Assert.Equal(-2, jadeA.PrismsRemaining); // -1 teammate kill, -1 fauna kill
    }

    [Fact]
    public void PrismLifecycle_RestoreModifySteal_MoveVolumeBetweenPlayers()
    {
        stats.PrismCreated(new PrismStats { OwnName = "A", Volume = 16f });
        Assert.Equal(1, jadeA.BlocksCreated);
        Assert.Equal(16f, jadeA.VolumeCreated);

        stats.PrismRestored(new PrismStats { OwnName = "A", Volume = 6f });
        Assert.Equal(1, jadeA.BlocksRestored);
        Assert.Equal(6f, jadeA.VolumeRestored);
        Assert.Equal(2, jadeA.PrismsRemaining);
        Assert.Equal(22f, jadeA.VolumeRemaining);

        stats.PrismVolumeModified(new PrismStats { OwnName = "A", Volume = 3f });
        Assert.Equal(19f, jadeA.VolumeCreated);
        Assert.Equal(25f, jadeA.VolumeRemaining);

        // Steal: OwnName is the STEALER, AttackerName the victim (upstream shape).
        stats.PrismStolen(new PrismStats { OwnName = "C", AttackerName = "A", Volume = 5f });
        Assert.Equal(1, rubyC.PrismStolen);
        Assert.Equal(5f, rubyC.VolumeStolen);
        Assert.Equal(1, rubyC.PrismsRemaining);
        Assert.Equal(5f, rubyC.VolumeRemaining);
        Assert.Equal(1, jadeA.PrismsRemaining);
        Assert.Equal(20f, jadeA.VolumeRemaining);
    }

    [Fact]
    public void Collisions_And_AbilityDurations_Record()
    {
        stats.ExecuteSkimmerShipCollision("A");
        Assert.Equal(1, jadeA.SkimmerShipCollisions);

        stats.ExecuteJoustCollision("C");
        stats.ExecuteJoustCollision("C");
        Assert.Equal(2, rubyC.JoustCollisions);
        stats.ExecuteJoustCollision("ghost"); // warn lane, never throws

        stats.RegisterAbilityExecuted(new AbilityStats { PlayerName = "A", ControlType = InputEvents.Button1Action, Duration = 1.5f });
        stats.RegisterAbilityExecuted(new AbilityStats { PlayerName = "A", ControlType = InputEvents.FlipAction, Duration = 0.5f });
        stats.RegisterAbilityExecuted(new AbilityStats { PlayerName = "A", ControlType = InputEvents.FullSpeedStraightAction, Duration = 2f });
        Assert.Equal(1.5f, jadeA.Button1AbilityActiveTime);
        Assert.Equal(0.5f, jadeA.FlipAbilityActiveTime);
        Assert.Equal(2f, jadeA.FullSpeedStraightAbilityActiveTime);
    }

    [Fact]
    public void LifeformCounts_TrackPerCell()
    {
        stats.LifeformCreated(cellID: 7);
        stats.LifeformCreated(cellID: 7);
        stats.LifeformCreated(cellID: 9);
        stats.LifeformDestroyed(cellID: 7);

        Assert.Equal(1, cellData.CellStatsList[7].LifeFormsInCell);
        Assert.Equal(1, cellData.CellStatsList[9].LifeFormsInCell);
    }

    [Fact]
    public void ClientSpawn_ClosesTheRecordGate_ServerSpawnReopensIt()
    {
        // A StatsManager wired to NetcodeHooks: a CLIENT-side network spawn
        // closes the gate (stats are server-authoritative), a server spawn
        // keeps it open.
        var go = new GameObject("stats-networked");
        go.SetActive(false);
        var hooks = go.AddComponent<NetcodeHooks>();
        var networked = go.AddComponent<StatsManager>();
        Set(networked, "gameData", gameData);
        Set(networked, "cellData", cellData);
        Set(networked, "_netcodeHooks", hooks);
        go.SetActive(true); // OnEnable: subscribe to the spawn hook

        // Client spawn: IsServer false → gate closes → nothing records.
        hooks.OnNetworkSpawn();
        networked.ExecuteSkimmerShipCollision("A");
        Assert.Equal(0, jadeA.SkimmerShipCollisions);

        // Server spawn: gate reopens → records flow again.
        typeof(CosmicShore.Engine.Networking.NetworkBehaviour)
            .GetProperty("IsServer")!.SetValue(hooks, true);
        hooks.OnNetworkSpawn();
        networked.ExecuteSkimmerShipCollision("A");
        Assert.Equal(1, jadeA.SkimmerShipCollisions);
    }
}
