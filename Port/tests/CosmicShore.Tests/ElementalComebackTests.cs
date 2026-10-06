using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.Engine.Networking;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
// Engine's UGS placeholder also declares an IPlayer — alias the game's player interface.
using IPlayer = CosmicShore.Gameplay.IPlayer;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// Rung-4 groundwork: ElementalComebackSystem + SO_ElementalComebackProfile ported
// verbatim. The comeback system is the canonical fundamentals composition — it sizes
// elemental buffs (the one buff/debuff system) to the DOMAIN deficit from the leader,
// never to the individual: a player on the leading domain gets no buff even when they
// personally trail their teammates. Buffs write only the 0.0–1.5 normalized band
// (levels 0–15); the base pips below 0 are reserved for the overtake impact effect.
public class ElementalComebackTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    // ── doubles ─────────────────────────────────────────────────────────────

    sealed class ComebackPlayer : IPlayer
    {
        public ComebackPlayer(string name, Domains domain, ResourceSystem rs, bool isLocalUser = false)
        {
            Name = name;
            IsLocalUser = isLocalUser;
            RoundStats = new RoundStats { Name = name, Domain = domain };
            Vessel = new StubVessel { VesselStatus = new StubVesselStatus { ResourceSystem = rs } };
        }

        public Domains Domain => RoundStats.Domain;
        public string Name { get; }
        public int AvatarId => 0;
        public string PlayerUUID => Name;
        public IVessel Vessel { get; }
        public InputController InputController => null;
        public IInputStatus InputStatus => null;
        public IRoundStats RoundStats { get; }
        public bool IsActive => true;
        public bool IsInitializedAsAI => false;
        public bool IsMultiplayerOwner => IsLocalUser;
        public bool IsNetworkOwner => IsLocalUser;
        public bool IsNetworkClient => !IsLocalUser;
        public bool IsLocalUser { get; }
        public ulong PlayerNetId => 0;
        public ulong VesselNetId => 0;
        public ulong OwnerClientNetId => 0;
        public Transform Transform => null;

        public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel) { }
        public void InitializeForMultiplayerMode(IVessel vessel) { }
        public void ToggleGameObject(bool toggle) { }
        public void DestroyPlayer() { }
        public void StartPlayer() { }
        public void ResetForPlay() { }
        public void SetPoseOfVessel(Pose pose) { }
        public void ChangeVessel(IVessel vessel) { }
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static void SetField(object target, string name, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var field = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{name}' not found on {target.GetType().Name}.");
    }

    static SO_ElementalComebackProfile MakeProfile(float chargeWeight = 1f, float initialCharge = 0f)
    {
        var profile = ScriptableObject.CreateInstance<SO_ElementalComebackProfile>();
        SetField(profile, "defaultConfig", new SO_ElementalComebackProfile.VesselComebackConfig
        {
            ChargeWeight = chargeWeight,
            InitialCharge = initialCharge,
        });
        return profile;
    }

    static GameDataSO MakeGameData()
    {
        NetworkManager.Singleton = null;
        var data = ScriptableObject.CreateInstance<GameDataSO>();
        data.OnMiniGameTurnStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnMiniGameTurnEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnMiniGameEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        return data;
    }

    ResourceSystem MakeResourceSystem()
    {
        var go = new GameObject("resources");
        spawned.Add(go);
        return go.AddComponent<ResourceSystem>();
    }

    ComebackPlayer AddPlayer(GameDataSO data, string name, Domains domain, int crystals, bool local = false)
    {
        var player = new ComebackPlayer(name, domain, MakeResourceSystem(), local);
        player.RoundStats.CrystalsCollected = crystals;
        data.Players.Add(player);
        data.RoundStatsList.Add(player.RoundStats);
        return player;
    }

    GameObject SpawnSystem(GameDataSO data, SO_ElementalComebackProfile profile)
    {
        var go = new GameObject("comeback");
        go.SetActive(false);
        spawned.Add(go);
        var system = go.AddComponent<ElementalComebackSystem>();
        SetField(system, "gameData", data);
        SetField(system, "comebackProfile", profile);
        go.SetActive(true); // OnEnable subscribes to the game events
        return go;
    }

    // ── SO_ElementalComebackProfile ─────────────────────────────────────────

    [Fact]
    public void Profile_GetConfig_SelectsVesselEntryOrDefault()
    {
        var profile = ScriptableObject.CreateInstance<SO_ElementalComebackProfile>();
        var manta = new SO_ElementalComebackProfile.VesselComebackConfig
        {
            VesselClass = VesselClassType.Manta,
            MassWeight = 2f,
        };
        SetField(profile, "vesselConfigs", new List<SO_ElementalComebackProfile.VesselComebackConfig> { manta });
        SetField(profile, "defaultConfig", new SO_ElementalComebackProfile.VesselComebackConfig { MassWeight = 7f });

        Assert.Equal(2f, profile.GetConfig(VesselClassType.Manta).GetWeight(Element.Mass));
        Assert.Equal(7f, profile.GetConfig(VesselClassType.Sparrow).GetWeight(Element.Mass)); // fallback
    }

    [Fact]
    public void Profile_ConfigSwitchesCoverAllElements()
    {
        var config = new SO_ElementalComebackProfile.VesselComebackConfig
        {
            MassWeight = 1f, ChargeWeight = 2f, SpaceWeight = 3f, TimeWeight = 4f,
            InitialMass = 5f, InitialCharge = 6f, InitialSpace = 7f, InitialTime = 8f,
        };
        Assert.Equal(1f, config.GetWeight(Element.Mass));
        Assert.Equal(2f, config.GetWeight(Element.Charge));
        Assert.Equal(3f, config.GetWeight(Element.Space));
        Assert.Equal(4f, config.GetWeight(Element.Time));
        Assert.Equal(5f, config.GetInitialLevel(Element.Mass));
        Assert.Equal(6f, config.GetInitialLevel(Element.Charge));
        Assert.Equal(7f, config.GetInitialLevel(Element.Space));
        Assert.Equal(8f, config.GetInitialLevel(Element.Time));
    }

    // ── ElementalComebackSystem ─────────────────────────────────────────────

    [Fact]
    public void TrailingDomainGetsBuff_LeadingDomainDoesNot()
    {
        var data = MakeGameData();
        var leader = AddPlayer(data, "Leader", Domains.Jade, crystals: 10);
        var trailer = AddPlayer(data, "Trailer", Domains.Ruby, crystals: 4);
        SpawnSystem(data, MakeProfile(chargeWeight: 1f));

        data.OnMiniGameTurnStarted.Raise(); // baselines + activation
        loop.Tick(1.1f);                    // past the 1s update interval

        // Ruby trails Jade by 6 crystals → 6 bonus levels on Charge (weight 1) → 0.6 normalized.
        var trailerRs = trailer.Vessel.VesselStatus.ResourceSystem;
        var leaderRs = leader.Vessel.VesselStatus.ResourceSystem;
        Assert.Equal(0.6f, trailerRs.GetNormalizedLevel(Element.Charge), 3);
        Assert.Equal(0f, leaderRs.GetNormalizedLevel(Element.Charge), 3);
    }

    [Fact]
    public void BuffIsDomainAggregate_NotIndividual()
    {
        // Two Jade players: one personally behind their teammate, but Jade leads as a
        // domain — neither gets a buff. The lone Ruby player gets the full team deficit.
        var data = MakeGameData();
        var jadeStar = AddPlayer(data, "JadeStar", Domains.Jade, crystals: 9);
        var jadeSlow = AddPlayer(data, "JadeSlow", Domains.Jade, crystals: 1);
        var ruby = AddPlayer(data, "Ruby", Domains.Ruby, crystals: 6);
        SpawnSystem(data, MakeProfile(chargeWeight: 1f));

        data.OnMiniGameTurnStarted.Raise();
        loop.Tick(1.1f);

        Assert.Equal(0f, jadeStar.Vessel.VesselStatus.ResourceSystem.GetNormalizedLevel(Element.Charge), 3);
        Assert.Equal(0f, jadeSlow.Vessel.VesselStatus.ResourceSystem.GetNormalizedLevel(Element.Charge), 3);
        // Ruby domain total 6 vs Jade 10 → deficit 4 → 0.4 normalized.
        Assert.Equal(0.4f, ruby.Vessel.VesselStatus.ResourceSystem.GetNormalizedLevel(Element.Charge), 3);
    }

    [Fact]
    public void BuffClampsToOnePointFive_NeverTouchesBasePips()
    {
        var data = MakeGameData();
        AddPlayer(data, "Leader", Domains.Jade, crystals: 100);
        var trailer = AddPlayer(data, "Trailer", Domains.Ruby, crystals: 0);
        SpawnSystem(data, MakeProfile(chargeWeight: 1f));

        data.OnMiniGameTurnStarted.Raise();
        loop.Tick(1.1f);

        // 100-crystal deficit → raw 10.0 normalized, clamped to the 1.5 ceiling.
        Assert.Equal(1.5f, trailer.Vessel.VesselStatus.ResourceSystem.GetNormalizedLevel(Element.Charge), 3);
    }

    [Fact]
    public void TurnStart_AppliesInitialLevelsFromProfile()
    {
        var data = MakeGameData();
        var p1 = AddPlayer(data, "P1", Domains.Jade, crystals: 0);
        AddPlayer(data, "P2", Domains.Ruby, crystals: 0);
        SpawnSystem(data, MakeProfile(chargeWeight: 0f, initialCharge: 8f));

        data.OnMiniGameTurnStarted.Raise();

        // Initial level 8 → 0.8 normalized, applied at baseline time (before any Update).
        Assert.Equal(0.8f, p1.Vessel.VesselStatus.ResourceSystem.GetNormalizedLevel(Element.Charge), 3);
    }

    [Fact]
    public void TurnEnd_DeactivatesBuffUpdates()
    {
        var data = MakeGameData();
        AddPlayer(data, "Leader", Domains.Jade, crystals: 10);
        var trailer = AddPlayer(data, "Trailer", Domains.Ruby, crystals: 0);
        SpawnSystem(data, MakeProfile(chargeWeight: 1f));

        data.OnMiniGameTurnStarted.Raise();
        data.OnMiniGameTurnEnd.Raise(); // deactivates before the first interval elapses
        loop.Tick(1.1f);

        Assert.Equal(0f, trailer.Vessel.VesselStatus.ResourceSystem.GetNormalizedLevel(Element.Charge), 3);
    }
}
