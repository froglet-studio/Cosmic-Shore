using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Collections;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Services;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
// Engine's UGS placeholder also declares an IPlayer — alias the game's player interface.
using IPlayer = CosmicShore.Gameplay.IPlayer;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C5 — concrete-arc iteration 5 (VESSEL_CONCRETE.md): the concrete `Player`
// (IPlayer NetworkBehaviour — NetworkVariables, 3-tier display-name fallback,
// deferred spawn event, domain mirror routing, PrepareForNewScene) plus the
// restored GameDataSO.BuildHumanCounts (V10 marker).
// Stubs are private to this file by design — VesselLayerTestDoubles.cs belongs
// to the C4 (VesselController) iteration.
// ─────────────────────────────────────────────────────────────────────────────

static class C5Reflect
{
    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Set(object target, string field, object value)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, Priv);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().Name, field);
    }

    public static object Get(object target, string field)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, Priv);
            if (f != null) return f.GetValue(target);
        }
        throw new MissingFieldException(target.GetType().Name, field);
    }

    /// <summary>Drive a private-setter auto-property (e.g. the AI spawner writing IsInitializedAsAI).</summary>
    public static void SetProp(object target, string property, object value) =>
        target.GetType().GetProperty(property)!.SetValue(target, value);
}

/// <summary>Minimal IVesselStatus for the repaint path — only Player (→ default Domain) matters.</summary>
class C5VesselStatus : IVesselStatus
{
    public IVessel Vessel { get; set; }
    public AIPilot AIPilot { get; set; }
    public AICinematicBehavior AICinematicBehavior { get; set; }
    public bool AlignmentEnabled { get; set; }
    public Material AOEConicExplosionMaterial { get; set; }
    public Material AOEExplosionMaterial { get; set; }
    public bool IsAttached { get; set; }
    public Prism AttachedPrism { get; set; }
    public Quaternion blockRotation { get; set; }
    public bool IsBoosting { get; set; }
    public float BoostMultiplier { get; set; }
    public float Inertia => 1f;
    public float ChargedBoostCharge { get; set; }
    public bool IsChargedBoostDischarging { get; set; }
    public Vector3 Course { get; set; }
    public bool IsDrifting { get; set; }
    public Transform CameraFollowTarget { get; set; }
    public bool GunsActive { get; set; }
    public bool HasLiveProjectiles { get; set; }
    public bool IsOverheating { get; set; }
    public IPlayer Player { get; set; }
    public bool IsPortrait { get; set; }
    public ResourceSystem ResourceSystem => null;
    public VesselAnimation VesselAnimation => null;
    public VesselCameraCustomizer VesselCameraCustomizer => null;
    public List<GameObject> ShipGeometries { get; set; }
    public Transform ShipTransform => null;
    public VesselTransformer VesselTransformer => null;
    public string Name => "C5StubVessel";
    public VesselClassType VesselType => VesselClassType.Manta;
    public Skimmer NearFieldSkimmer => null;
    public Skimmer FarFieldSkimmer => null;
    public GameObject OrientationHandle => null;
    public SilhouetteController Silhouette => null;
    public Material ShipMaterial { get; set; }
    public Material SkimmerMaterial { get; set; }
    public float Speed { get; set; }
    public bool IsSingleStickControls { get; set; }
    public bool IsSlowed { get; set; }
    public bool IsStationary { get; set; }
    public bool IsTranslationRestricted { get; set; }
    public VesselPrismController VesselPrismController => null;
    public IVesselHUDController VesselHUDController => null;
    public VesselCustomization Customization => null;
    public R_VesselActionHandler ActionHandler => null;
    public R_ShipElementStatsHandler ElementalStatsHandler => null;
    public bool IsNetworkOwner => false;
    public bool IsNetworkClient => false;
    public void ResetForPlay() { }
}

/// <summary>Recording IVessel double for Player's vessel-facing calls.</summary>
class C5Vessel : IVessel
{
    public readonly C5VesselStatus Status = new();

    public int StartVesselCalls, ResetForPlayCalls, DestroyVesselCalls;
    public readonly List<bool> AIPilotToggles = new();
    public readonly List<Material> ShipMaterialsSet = new();

    public event Action OnInitialized { add { } remove { } }
    public event Action OnBeforeDestroyed { add { } remove { } }

    public IVesselStatus VesselStatus => Status;
    public bool IsNetworkOwner => false;
    public bool IsNetworkClient => false;
    public ulong PlayerNetId => 0;
    public ulong VesselNetId => 0;
    public ulong OwnerClientNetId => 0;
    public Transform Transform => null;

    public void Initialize(IPlayer player) => Status.Player = player;
    public void PerformShipControllerActions(InputEvents @event) { }
    public void StopShipControllerActions(InputEvents @event) { }
    public void Teleport(Transform transform) { }
    public void SetResourceLevels(ResourceCollection resources) { }
    public void SetShipUp(float angle) { }
    public void DisableSkimmer() { }
    public void SetBoostMultiplier(float boostMultiplier) { }
    public void SetShipMaterial(Material material) => ShipMaterialsSet.Add(material);
    public void SetBlockSilhouettePrefab(GameObject prefab) { }
    public void SetAOEExplosionMaterial(Material material) { }
    public void SetAOEConicExplosionMaterial(Material material) { }
    public void SetSkimmerMaterial(Material material) { }
    public void SetTrailColors(Color highlightColor, Color coreColor) { }
    public void BindElementalFloat(string name, Element element) { }
    public void ToggleAIPilot(bool toggle) => AIPilotToggles.Add(toggle);
    public void StartVessel() => StartVesselCalls++;
    public bool AllowClearPrismInitialization() => true;
    public void DestroyVessel() => DestroyVesselCalls++;
    public void ResetForPlay() => ResetForPlayCalls++;
    public void SetPose(Pose pose) { }
    public void SetInitialSpeed(float initialSpeed) { }
    public void ChangePlayer(IPlayer player) => Status.Player = player;
    public void ModifyThrottle(float amount, float duration) { }
    public void AddSlowedShipTransformToGameData() { }
    public void RemoveSlowedShipTransformFromGameData() { }
}

// ══════════════════════════════════════════════════════════════════════════
// Player — concrete IPlayer NetworkBehaviour
// ══════════════════════════════════════════════════════════════════════════

public class PlayerC5Tests : IDisposable
{
    public void Dispose()
    {
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        AuthenticationService.Reset();
        NetworkManager.Singleton = null;
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static GameDataSO MakeGameData(VesselClassType selected = VesselClassType.Manta)
    {
        NetworkManager.Singleton = null;
        var data = ScriptableObject.CreateInstance<GameDataSO>();
        data.OnPlayerNetworkSpawnedUlong = ScriptableObject.CreateInstance<ScriptableEventUlong>();
        data.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
        data.selectedVesselClass.Value = selected;
        return data;
    }

    static (Player player, GameObject go) MakePlayer(GameDataSO gameData)
    {
        var go = new GameObject("player");
        var player = go.AddComponent<Player>();
        C5Reflect.Set(player, "gameData", gameData);
        return (player, go);
    }

    static List<ulong> RecordSpawnEvents(GameDataSO gameData)
    {
        var raised = new List<ulong>();
        gameData.OnPlayerNetworkSpawnedUlong.OnRaised += id => raised.Add(id);
        return raised;
    }

    static PlayerDataService MakeProfileService(string displayName, int avatarId, bool initialized)
    {
        var go = new GameObject("playerDataService");
        var service = go.AddComponent<PlayerDataService>();
        // Simulate the cloud profile state the auth flow would have produced.
        typeof(PlayerDataService).GetProperty("CurrentProfile")!
            .SetValue(service, new PlayerProfileData { displayName = displayName, avatarId = avatarId });
        typeof(PlayerDataService).GetProperty("IsInitialized")!.SetValue(service, initialized);
        return service;
    }

    // ── NetworkVariable declarations (permission-gated write contract) ──────

    [Fact]
    public void NetworkVariables_DeclarePermissionsAndDefaultsMatchingOriginal()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());

        Assert.Equal(NetworkVariableReadPermission.Everyone, player.NetDefaultVesselType.ReadPerm);
        Assert.Equal(NetworkVariableWritePermission.Owner, player.NetDefaultVesselType.WritePerm);
        Assert.Equal(VesselClassType.Random, player.NetDefaultVesselType.Value);

        Assert.Equal(NetworkVariableReadPermission.Everyone, player.NetDomain.ReadPerm);
        Assert.Equal(NetworkVariableWritePermission.Server, player.NetDomain.WritePerm);
        Assert.Equal(Domains.Jade, player.NetDomain.Value);

        Assert.Equal(NetworkVariableReadPermission.Everyone, player.NetName.ReadPerm);
        Assert.Equal(NetworkVariableWritePermission.Owner, player.NetName.WritePerm);
        Assert.True(player.NetName.Value.IsEmpty);

        Assert.Equal(NetworkVariableReadPermission.Everyone, player.NetVesselId.ReadPerm);
        Assert.Equal(NetworkVariableWritePermission.Server, player.NetVesselId.WritePerm);
        Assert.Equal(0ul, player.NetVesselId.Value);

        Assert.Equal(NetworkVariableReadPermission.Everyone, player.NetIsAI.ReadPerm);
        Assert.Equal(NetworkVariableWritePermission.Server, player.NetIsAI.WritePerm);
        Assert.False(player.NetIsAI.Value);

        Assert.Equal(NetworkVariableReadPermission.Everyone, player.NetAvatarId.ReadPerm);
        Assert.Equal(NetworkVariableWritePermission.Owner, player.NetAvatarId.WritePerm);
        Assert.Equal(0, player.NetAvatarId.Value);
    }

    [Fact]
    public void RoleFlags_SwitchOnIsSpawned()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());

        // Pre-spawn: every networked role flag is off.
        Assert.False(player.IsMultiplayerOwner);
        Assert.False(player.IsNetworkOwner);
        Assert.False(player.IsNetworkClient);
        Assert.False(player.IsLocalUser);
        Assert.Equal(0ul, player.PlayerNetId);

        AuthenticationService.Instance.PlayerName = "Pilot#0001";
        player.Spawn(); // host-mode defaults: server+client+owner

        Assert.True(player.IsSpawned);
        Assert.True(player.IsMultiplayerOwner);
        Assert.True(player.IsNetworkOwner);
        Assert.False(player.IsNetworkClient);
        Assert.True(player.IsLocalUser);
        Assert.NotEqual(0ul, player.PlayerNetId); // E12 id allocated at spawn

        player.Despawn();
        Assert.False(player.IsMultiplayerOwner);
        Assert.False(player.IsNetworkOwner);
    }

    // ── Initialization paths ────────────────────────────────────────────────

    [Fact]
    public void InitializeForSinglePlayerMode_SetsJadeDefaults_PausesInput()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());
        var vessel = new C5Vessel();

        player.InitializeForSinglePlayerMode(
            new IPlayer.InitializeData { PlayerName = "Solo", AvatarId = 3, IsAI = false }, vessel);

        Assert.Equal(Domains.Jade, player.Domain);
        Assert.Equal("Solo", player.Name);
        Assert.Equal("Solo", player.PlayerUUID);
        Assert.Equal(3, player.AvatarId);
        Assert.Same(vessel, player.Vessel);
        Assert.False(player.IsInitializedAsAI);
        Assert.True(player.InputStatus.Paused);
        Assert.Equal("Solo", player.RoundStats.Name);
        Assert.Equal(Domains.Jade, player.RoundStats.Domain);
    }

    [Fact]
    public void InitializeForSinglePlayerMode_AIFlagPropagates()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());

        player.InitializeForSinglePlayerMode(
            new IPlayer.InitializeData { PlayerName = "Bot", IsAI = true }, new C5Vessel());

        Assert.True(player.IsInitializedAsAI);
        Assert.False(player.IsLocalUser); // AI is never the local user, spawned or not
    }

    [Fact]
    public void InitializeForMultiplayerMode_Unspawned_SyncsMirrors_SkipsServerOnlySteps()
    {
        using var loop = new GameLoop();
        var (player, go) = MakePlayer(MakeGameData());
        var vessel = new C5Vessel();

        player.NetDomain.Value = Domains.Ruby;
        player.NetName.Value = "Remote";
        player.NetAvatarId.Value = 5;

        player.InitializeForMultiplayerMode(vessel);

        Assert.Equal(Domains.Ruby, player.Domain);
        Assert.Equal("Remote", player.Name);
        Assert.Equal(5, player.AvatarId);
        Assert.Same(vessel, player.Vessel);
        Assert.Equal(Domains.Ruby, player.RoundStats.Domain); // local mirror set on EVERY peer
        Assert.NotEqual("Remote", player.RoundStats.Name);    // server-only step skipped
        Assert.Equal("player", go.name);                      // SetGameObjectName skipped
    }

    [Fact]
    public void InitializeForMultiplayerMode_Server_SetsRoundStatsNameAndGameObjectName()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var (player, go) = MakePlayer(gameData);
        AuthenticationService.Instance.PlayerName = "Host#1234";
        player.Spawn(ownerClientId: 0);

        player.InitializeForMultiplayerMode(new C5Vessel());

        Assert.Equal("Host", player.Name); // from NetName written at spawn (tier 3)
        Assert.Equal("Host", player.RoundStats.Name);
        Assert.Equal("Player_0", go.name);
    }

    [Fact]
    public void InitializeForMultiplayerMode_ServerAI_NamesGameObjectAI()
    {
        using var loop = new GameLoop();
        var (player, go) = MakePlayer(MakeGameData());
        C5Reflect.SetProp(player, "IsInitializedAsAI", true); // the AI spawner's pre-spawn write
        player.Spawn();
        player.NetName.Value = "Bot-1"; // AI spawner names AI after spawn

        player.InitializeForMultiplayerMode(new C5Vessel());

        Assert.True(player.NetIsAI.Value); // server wrote it from IsInitializedAsAI at spawn
        Assert.True(player.IsInitializedAsAI);
        Assert.Equal("AI", go.name);
    }

    // ── OnNetworkSpawn — host path + spawn event ────────────────────────────

    [Fact]
    public void OnNetworkSpawn_HostPath_RegistersAndRaisesOnce()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData(VesselClassType.Manta);
        var raised = RecordSpawnEvents(gameData);
        var (player, _) = MakePlayer(gameData);
        AuthenticationService.Instance.PlayerName = "Pilot#1234";

        player.Spawn();

        Assert.Contains(player, gameData.Players);
        Assert.Equal(VesselClassType.Manta, player.NetDefaultVesselType.Value); // from selectedVesselClass
        Assert.Equal(new ulong[] { 0 }, raised); // raised exactly once, host clientId 0
        Assert.False(player.NetIsAI.Value);
    }

    [Fact]
    public void OnNetworkSpawn_OwnerKeepsPreChosenVesselType()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData(VesselClassType.Manta);
        var (player, _) = MakePlayer(gameData);
        AuthenticationService.Instance.PlayerName = "Pilot#1234";
        player.NetDefaultVesselType.Value = VesselClassType.Dolphin; // modal already wrote a concrete pick

        player.Spawn();

        Assert.Equal(VesselClassType.Dolphin, player.NetDefaultVesselType.Value);
    }

    [Fact]
    public void OnNetworkSpawn_AIOnHost_SkipsOwnerProfileWrites()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var raised = RecordSpawnEvents(gameData);
        var (player, _) = MakePlayer(gameData);
        C5Reflect.SetProp(player, "IsInitializedAsAI", true);
        AuthenticationService.Instance.PlayerName = "Human#9999";

        player.Spawn(); // AI shares the host's owner id but IsLocalUser filters it out

        Assert.True(player.NetIsAI.Value);
        Assert.True(player.NetName.Value.IsEmpty);                          // no profile write
        Assert.Equal(VesselClassType.Random, player.NetDefaultVesselType.Value); // no vessel write
        Assert.Empty(raised);                                               // not spawn-ready yet
    }

    // ── 3-tier display-name fallback ────────────────────────────────────────

    [Fact]
    public void DisplayName_Tier1_PlayerDataServiceProfile()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        gameData.LocalPlayerDisplayName = "CachedName"; // tier 2 available but must lose to tier 1
        AuthenticationService.Instance.PlayerName = "UgsName#1111";
        MakeProfileService("CloudName", 7, initialized: true);
        var (player, _) = MakePlayer(gameData);

        player.Spawn();

        Assert.Equal("CloudName", player.NetName.Value.ToString());
        Assert.Equal(7, player.NetAvatarId.Value);
        Assert.Equal("CloudName", player.Name); // OnNetNameValueChanged mirrored it
    }

    [Fact]
    public void DisplayName_Tier2_GameDataCache_WhenServiceNotInitialized()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        gameData.LocalPlayerDisplayName = "CachedName";
        gameData.LocalPlayerAvatarId = 4;
        AuthenticationService.Instance.PlayerName = "UgsName#1111";
        MakeProfileService("CloudName", 7, initialized: false); // present but not ready
        var (player, _) = MakePlayer(gameData);

        player.Spawn();

        Assert.Equal("CachedName", player.NetName.Value.ToString());
        Assert.Equal(4, player.NetAvatarId.Value);
    }

    [Fact]
    public void DisplayName_Tier3_AuthServiceWithSuffixStripped()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData(); // no service, no cache
        AuthenticationService.Instance.PlayerName = "MyName#1234";
        var (player, _) = MakePlayer(gameData);

        player.Spawn();

        Assert.Equal("MyName", player.NetName.Value.ToString());
    }

    [Theory]
    [InlineData("MyName#1234", "MyName")]
    [InlineData("NoSuffix", "NoSuffix")]
    [InlineData("#1234", "#1234")] // leading hash is not a suffix (hashIndex > 0 required)
    [InlineData("A#B#C", "A#B")]   // strips at the LAST hash
    [InlineData("", "")]
    [InlineData(null, null)]
    public void StripPlayerNameSuffix_MatchesOriginalContract(string input, string expected)
    {
        var strip = typeof(Player).GetMethod("StripPlayerNameSuffix",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Equal(expected, (string)strip.Invoke(null, new object[] { input }));
    }

    [Fact]
    public void ProfileLoadedAfterSpawn_OwnerUpdatesNetNameAndAvatar()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        AuthenticationService.Instance.PlayerName = "Stale#0001";
        var service = MakeProfileService("ignored", 0, initialized: false); // not ready at spawn → tier 3
        var (player, _) = MakePlayer(gameData);
        player.Spawn();
        Assert.Equal("Stale", player.NetName.Value.ToString());

        // Cloud profile finishes loading after spawn → OnProfileChanged fires.
        service.SetDisplayName("FreshName");

        Assert.Equal("FreshName", player.NetName.Value.ToString());
        Assert.Equal("FreshName", player.Name);
    }

    // ── Deferred spawn event (server sees a remote client's Player) ─────────

    [Fact]
    public void DeferredSpawnEvent_RaisedOnceWhenNameAndVesselTypeReplicate()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var raised = RecordSpawnEvents(gameData);
        var (player, _) = MakePlayer(gameData);

        // Server-side view of a remote client's player: owner block skipped.
        player.Spawn(isServer: true, isClient: true, isOwner: false, ownerClientId: 7);
        Assert.Empty(raised); // name empty + vessel type Random → deferred

        player.NetDefaultVesselType.Value = VesselClassType.Dolphin; // first replication
        Assert.Empty(raised); // name still empty

        player.NetName.Value = "Remote"; // second replication completes readiness
        Assert.Equal(new ulong[] { 7 }, raised);
        Assert.Equal("Remote", player.Name);

        player.NetName.Value = "Remote2"; // further changes never re-raise
        Assert.Single(raised);
    }

    [Fact]
    public void NonServerSpawn_RaisesImmediatelyForPairResolution()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var raised = RecordSpawnEvents(gameData);
        var (player, _) = MakePlayer(gameData);

        player.Spawn(isServer: false, isClient: true, isOwner: false, ownerClientId: 3);

        Assert.Equal(new ulong[] { 3 }, raised); // no readiness gate on pure clients
    }

    // ── RequestSetDomain_ServerRpc validation ───────────────────────────────

    [Fact]
    public void RequestSetDomain_AcceptsActive_RejectsInactiveAndSentinel()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        gameData.RequestedDomainCount = 2; // active set: Jade + Ruby
        var (player, _) = MakePlayer(gameData);
        AuthenticationService.Instance.PlayerName = "Picker#1";
        player.Spawn();

        player.RequestSetDomain_ServerRpc(Domains.Gold); // outside DC=2 slice
        Assert.Equal(Domains.Jade, player.NetDomain.Value);

        player.RequestSetDomain_ServerRpc(Domains.Blue); // never active (sentinel)
        Assert.Equal(Domains.Jade, player.NetDomain.Value);

        player.RequestSetDomain_ServerRpc(Domains.Ruby);
        Assert.Equal(Domains.Ruby, player.NetDomain.Value);
        Assert.Equal(Domains.Ruby, player.Domain); // OnNetDomainChanged mirrored it
    }

    // ── Domain routing: mirror + RoundStats + vessel repaint ────────────────

    [Fact]
    public void OnNetDomainChanged_UpdatesMirror_RoundStats_AndRepaintsVessel()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var (player, _) = MakePlayer(gameData);
        AuthenticationService.Instance.PlayerName = "Painter#1";
        player.Spawn();

        _ = player.RoundStats; // cache _roundStats so the mirror branch is live
        var vessel = new C5Vessel();
        vessel.Status.Player = player;
        player.ChangeVessel(vessel);

        var rubyMaterial = new Material((Shader)null);
        var theme = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
        theme.TeamMaterialSets = new Dictionary<Domains, SO_MaterialSet>
        {
            [Domains.Ruby] = MakeMaterialSet(rubyMaterial),
        };
        C5Reflect.Set(player, "_vesselThemeManagerData", theme); // ClientPlayerVesselInitializer's stash

        player.NetDomain.Value = Domains.Ruby;

        Assert.Equal(Domains.Ruby, player.Domain);
        Assert.Equal(Domains.Ruby, player.RoundStats.Domain);
        Assert.Equal(new[] { rubyMaterial }, vessel.ShipMaterialsSet); // ShipHelper repaint ran with the stashed theme
    }

    static SO_MaterialSet MakeMaterialSet(Material shipMaterial)
    {
        var set = ScriptableObject.CreateInstance<SO_MaterialSet>();
        set.ShipMaterial = shipMaterial;
        return set;
    }

    [Fact]
    public void OnNetDomainChanged_NullVesselOrTheme_SkipsRepaintWithoutThrowing()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());
        AuthenticationService.Instance.PlayerName = "NoVessel#1";
        player.Spawn();

        player.NetDomain.Value = Domains.Gold; // no vessel, no theme, no cached RoundStats

        Assert.Equal(Domains.Gold, player.Domain);
    }

    [Fact]
    public void SetDomain_ChangesLocalMirrorOnly()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());

        player.SetDomain(Domains.Gold); // shape-mode local override

        Assert.Equal(Domains.Gold, player.Domain);
        Assert.Equal(Domains.Jade, player.NetDomain.Value); // networked source untouched
    }

    // ── AvatarId / VesselId routing ─────────────────────────────────────────

    [Fact]
    public void OnNetAvatarIdChanged_UpdatesMirror()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());
        AuthenticationService.Instance.PlayerName = "Avatar#1";
        player.Spawn();

        player.NetAvatarId.Value = 9;

        Assert.Equal(9, player.AvatarId);
    }

    [Fact]
    public void OnNetVesselIdChanged_ZeroClearsVesselAndActive()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());
        AuthenticationService.Instance.PlayerName = "Linked#1";
        player.Spawn();
        var vessel = new C5Vessel();
        player.ChangeVessel(vessel);
        player.StartPlayer(); // IsActive = true

        player.NetVesselId.Value = 42;
        Assert.Equal(42ul, player.VesselNetId);
        Assert.Same(vessel, player.Vessel); // non-zero id keeps the pair

        player.NetVesselId.Value = 0; // server cleared the link
        Assert.Equal(0ul, player.VesselNetId);
        Assert.Null(player.Vessel);
        Assert.False(player.IsActive);
    }

    // ── ToggleGameObject / StartPlayer / ResetForPlay ───────────────────────

    [Fact]
    public void ToggleGameObject_TogglesActiveSelf()
    {
        using var loop = new GameLoop();
        var (player, go) = MakePlayer(MakeGameData());

        player.ToggleGameObject(false);
        Assert.False(go.activeSelf);

        player.ToggleGameObject(true);
        Assert.True(go.activeSelf);
    }

    [Fact]
    public void StartPlayer_NullVessel_SkipsWithoutThrowing()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());

        player.StartPlayer();

        Assert.False(player.IsActive);
    }

    [Fact]
    public void StartPlayer_Human_ActivatesVesselAndUnpausesInput()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());
        var vessel = new C5Vessel();
        player.InitializeForSinglePlayerMode(
            new IPlayer.InitializeData { PlayerName = "Solo", IsAI = false }, vessel);
        Assert.True(player.InputStatus.Paused);

        player.StartPlayer();

        Assert.True(player.IsActive);
        Assert.Equal(1, vessel.StartVesselCalls);
        Assert.False(player.InputStatus.Idle);
        Assert.False(player.InputStatus.Paused);
        Assert.Empty(vessel.AIPilotToggles);
    }

    [Fact]
    public void StartPlayer_AI_TogglesAIPilotOnAndKeepsInputPaused()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());
        var vessel = new C5Vessel();
        player.InitializeForSinglePlayerMode(
            new IPlayer.InitializeData { PlayerName = "Bot", IsAI = true }, vessel);

        player.StartPlayer();

        Assert.True(player.IsActive);
        Assert.Equal(new[] { true }, vessel.AIPilotToggles);
        Assert.True(player.InputStatus.Paused);
    }

    [Fact]
    public void StartPlayer_NetworkClient_StartsVesselButLeavesInputAndAIAlone()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());
        var vessel = new C5Vessel();
        player.Spawn(isServer: false, isClient: true, isOwner: false, ownerClientId: 5);
        player.ChangeVessel(vessel);
        player.InputController.SetPause(true);

        player.StartPlayer();

        Assert.True(player.IsActive);
        Assert.Equal(1, vessel.StartVesselCalls);
        Assert.True(player.InputStatus.Paused); // IsNetworkClient returns before pause toggle
        Assert.Empty(vessel.AIPilotToggles);
    }

    [Fact]
    public void ResetForPlay_ResetsVesselAndDeactivates()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());
        var vessel = new C5Vessel();
        player.InitializeForSinglePlayerMode(
            new IPlayer.InitializeData { PlayerName = "Solo", IsAI = false }, vessel);
        player.StartPlayer();
        Assert.True(player.IsActive);

        player.ResetForPlay();

        Assert.Equal(1, vessel.ResetForPlayCalls);
        Assert.False(player.IsActive);
        Assert.Empty(vessel.AIPilotToggles); // human path never touches AI pilot
    }

    [Fact]
    public void ResetForPlay_AI_TogglesAIPilotOff()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());
        var vessel = new C5Vessel();
        player.InitializeForSinglePlayerMode(
            new IPlayer.InitializeData { PlayerName = "Bot", IsAI = true }, vessel);
        player.StartPlayer(); // toggles AI on

        player.ResetForPlay();

        Assert.Equal(new[] { true, false }, vessel.AIPilotToggles);
        Assert.False(player.IsActive);
    }

    [Fact]
    public void ResetForPlay_NullVessel_NoThrow()
    {
        using var loop = new GameLoop();
        var (player, _) = MakePlayer(MakeGameData());

        player.ResetForPlay(); // persistent Player between scenes: vessel already destroyed

        Assert.False(player.IsActive);
    }

    // ── DestroyPlayer ───────────────────────────────────────────────────────

    [Fact]
    public void DestroyPlayer_Unspawned_DestroysGameObject()
    {
        using var loop = new GameLoop();
        var (player, go) = MakePlayer(MakeGameData());

        player.DestroyPlayer();
        loop.Tick(1f / 60f); // Destroy is deferred end-of-frame

        Assert.True(go.IsDestroyed);
    }

    [Fact]
    public void DestroyPlayer_SpawnedServer_DespawnsUnsubscribesAndDestroys()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var (player, go) = MakePlayer(gameData);
        AuthenticationService.Instance.PlayerName = "Doomed#1";
        player.Spawn();
        Assert.Contains(player, gameData.Players);

        player.DestroyPlayer(); // NetworkObject.Despawn(true)

        Assert.False(player.IsSpawned);
        Assert.DoesNotContain(player, gameData.Players); // OnNetworkDespawn removed it
        Assert.Null(C5Reflect.Get(player, "_vesselThemeManagerData"));

        var domainBefore = player.Domain;
        player.NetDomain.Value = Domains.Gold; // callbacks unsubscribed → mirror stays
        Assert.Equal(domainBefore, player.Domain);

        loop.Tick(1f / 60f);
        Assert.True(go.IsDestroyed);
    }

    [Fact]
    public void DestroyPlayer_SpawnedNonServer_DoesNothing()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var (player, go) = MakePlayer(gameData);
        player.Spawn(isServer: false, isClient: true, isOwner: false, ownerClientId: 2);

        player.DestroyPlayer(); // clients wait for the server's despawn

        Assert.True(player.IsSpawned);
        loop.Tick(1f / 60f);
        Assert.False(go.IsDestroyed);
    }

    // ── PrepareForNewScene ──────────────────────────────────────────────────

    [Fact]
    public void PrepareForNewScene_ClearsSceneState_SyncsMirrors_ReRegisters()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData(VesselClassType.Dolphin);
        var (player, _) = MakePlayer(gameData);
        AuthenticationService.Instance.PlayerName = "Persistent#7";
        player.Spawn();
        player.ChangeVessel(new C5Vessel());
        player.NetVesselId.Value = 42;
        player.StartPlayer();

        // Scene transition: ResetRuntimeData cleared the SOAP lists and the menu
        // config now selects a different vessel class.
        gameData.Players.Clear();
        gameData.selectedVesselClass.Value = VesselClassType.Squirrel;

        player.PrepareForNewScene();

        Assert.Null(player.Vessel);
        Assert.False(player.IsActive);
        Assert.Equal(0ul, player.VesselNetId);
        Assert.Equal(0ul, player.NetVesselId.Value); // server reset the link var
        Assert.Equal(VesselClassType.Squirrel, player.NetDefaultVesselType.Value); // always overwritten
        Assert.Equal("Persistent", player.Name); // force-synced from NetName
        Assert.Equal(player.NetDomain.Value, player.Domain);
        Assert.Contains(player, gameData.Players); // re-registered exactly once
        Assert.Single(gameData.Players);

        player.PrepareForNewScene(); // idempotent re-registration
        Assert.Single(gameData.Players);
    }

    [Fact]
    public void PrepareForNewScene_RefreshesNameFromNowLoadedProfile()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        AuthenticationService.Instance.PlayerName = "CuteAwakingLightbulb#42";
        var service = MakeProfileService("ignored", 0, initialized: false);
        var (player, _) = MakePlayer(gameData);
        player.Spawn(); // profile not ready → tier 3 UGS default
        Assert.Equal("CuteAwakingLightbulb", player.NetName.Value.ToString());

        // By game-scene time the cloud profile has loaded the menu username.
        typeof(PlayerDataService).GetProperty("CurrentProfile")!
            .SetValue(service, new PlayerProfileData { displayName = "dragon", avatarId = 2 });
        typeof(PlayerDataService).GetProperty("IsInitialized")!.SetValue(service, true);

        player.PrepareForNewScene();

        Assert.Equal("dragon", player.NetName.Value.ToString());
        Assert.Equal("dragon", player.Name);
        Assert.Equal(2, player.NetAvatarId.Value);
        Assert.Equal(2, player.AvatarId);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// GameDataSO.BuildHumanCounts — restored V10 marker
// ══════════════════════════════════════════════════════════════════════════

public class BuildHumanCountsC5Tests : IDisposable
{
    public void Dispose()
    {
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        AuthenticationService.Reset();
        NetworkManager.Singleton = null;
    }

    static Player MakeHuman(Domains domain, bool isAI = false)
    {
        var go = new GameObject("human");
        var player = go.AddComponent<Player>();
        player.NetDomain.Value = domain;
        player.NetIsAI.Value = isAI;
        return player;
    }

    [Fact]
    public void BuildHumanCounts_CountsHumansPerActiveDomain_ZeroFillsUnpicked()
    {
        using var loop = new GameLoop();
        var humans = new List<Player>
        {
            MakeHuman(Domains.Jade),
            MakeHuman(Domains.Jade),
            MakeHuman(Domains.Ruby),
        };

        var counts = GameDataSO.BuildHumanCounts(humans, GameDataSO.ActiveDomains);

        Assert.Equal(3, counts.Count); // keyed by EVERY active domain
        Assert.Equal(2, counts[Domains.Jade]);
        Assert.Equal(1, counts[Domains.Ruby]);
        Assert.Equal(0, counts[Domains.Gold]); // zero entry, not missing
        Assert.False(counts.ContainsKey(Domains.Blue));
    }

    [Fact]
    public void BuildHumanCounts_ExcludesAIAndNullEntries()
    {
        using var loop = new GameLoop();
        var humans = new List<Player>
        {
            MakeHuman(Domains.Jade),
            MakeHuman(Domains.Jade, isAI: true), // AI never counts as a human
            null,                                 // destroyed/missing slots are skipped
            MakeHuman(Domains.Gold),
        };

        var counts = GameDataSO.BuildHumanCounts(humans, GameDataSO.ActiveDomains);

        Assert.Equal(1, counts[Domains.Jade]);
        Assert.Equal(0, counts[Domains.Ruby]);
        Assert.Equal(1, counts[Domains.Gold]);
    }

    [Fact]
    public void BuildHumanCounts_OutOfSetDomains_NotCounted()
    {
        using var loop = new GameLoop();
        var activeDomains = new List<Domains> { Domains.Jade, Domains.Ruby }; // DC=2 slice
        var humans = new List<Player>
        {
            MakeHuman(Domains.Jade),
            MakeHuman(Domains.Gold),  // outside the DC=2 active set
            MakeHuman(Domains.Blue),  // "no team" sentinel — NormalizeUnassignedHumans territory
        };

        var counts = GameDataSO.BuildHumanCounts(humans, activeDomains);

        Assert.Equal(2, counts.Count);
        Assert.Equal(1, counts[Domains.Jade]);
        Assert.Equal(0, counts[Domains.Ruby]);
        Assert.False(counts.ContainsKey(Domains.Gold));
        Assert.False(counts.ContainsKey(Domains.Blue));
    }

    [Fact]
    public void BuildHumanCounts_EmptyHumans_ReturnsAllZeros()
    {
        var counts = GameDataSO.BuildHumanCounts(
            Array.Empty<Player>(), GameDataSO.ActiveDomains);

        Assert.All(counts.Values, v => Assert.Equal(0, v));
        Assert.Equal(3, counts.Count);
    }
}
