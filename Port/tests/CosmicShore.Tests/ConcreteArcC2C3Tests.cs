using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
// Engine's UGS placeholder also declares an IPlayer — alias the game's player interface.
using IPlayer = CosmicShore.Gameplay.IPlayer;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C2/C3 — concrete-arc iterations 2 + 3 (VESSEL_CONCRETE.md):
//   C2: PlayerDataService (Deviation #14-ext — UGS CloudSave paths staged like
//       GameSetting; pure model logic live: default profile, crystal/XP math,
//       reward unlocks, OnProfileChanged → GameDataSO sync).
//   C3: VesselStatus (verbatim concrete IVesselStatus on the full 10-component
//       RequireComponent rig) + restored SilhouetteController V19
//       AutoPilotEnabled guard.
// ─────────────────────────────────────────────────────────────────────────────

static class C2C3Reflect
{
    public const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>Set a private/serialized field, searching up the declaring chain.</summary>
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
}

// ══════════════════════════════════════════════════════════════════════════
// C2 — PlayerDataService
// ══════════════════════════════════════════════════════════════════════════

public class PlayerDataServiceC2Tests : IDisposable
{
    public void Dispose()
    {
        // Instance is a static singleton with a private setter; clear it so each
        // test (and every other test class) starts from a clean slate.
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        AuthenticationService.Reset();
        UnityServices.Reset();
    }

    static SO_ProfileIconList MakeIcons(params (string name, int id)[] icons)
    {
        var list = ScriptableObject.CreateInstance<SO_ProfileIconList>();
        list.profileIcons = icons.Select(i => new ProfileIcon(i.name, i.id, new Sprite { name = i.name })).ToList();
        return list;
    }

    static (PlayerDataService service, GameObject go) CreateService(
        SO_ProfileIconList icons = null, GameDataSO gameData = null)
    {
        var go = new GameObject("playerDataService");
        go.SetActive(false); // configure-before-activation: Awake reads profileIcons
        var service = go.AddComponent<PlayerDataService>();
        if (icons != null) C2C3Reflect.Set(service, "profileIcons", icons);
        if (gameData != null) C2C3Reflect.Set(service, "gameData", gameData);
        go.SetActive(true); // → Awake: singleton claim + local default profile
        return (service, go);
    }

    [Fact]
    public void Awake_CreatesLocalDefaultProfile()
    {
        using var loop = new GameLoop();
        var icons = MakeIcons(("first", 7), ("second", 9));
        var (service, _) = CreateService(icons);

        Assert.Same(service, PlayerDataService.Instance);
        Assert.NotNull(service.CurrentProfile);
        Assert.False(string.IsNullOrEmpty(service.CurrentProfile.userId)); // GUID seeded
        Assert.Matches(@"^Pilot\d{4}$", service.CurrentProfile.displayName);
        var suffix = int.Parse(service.CurrentProfile.displayName.Substring(5));
        Assert.InRange(suffix, 1000, 9999);
        Assert.Equal(7, service.CurrentProfile.avatarId); // first icon's Id
        Assert.Equal(0, service.CurrentProfile.crystalBalance);
        Assert.Equal(0, service.CurrentProfile.xp);
    }

    [Fact]
    public void Awake_DefaultAvatarId_ZeroWithoutIconList()
    {
        using var loop = new GameLoop();
        var (service, _) = CreateService(icons: null);
        Assert.Equal(0, service.CurrentProfile.avatarId);
    }

    [Fact]
    public void Awake_SecondInstance_IsDestroyed_SingletonHolds()
    {
        using var loop = new GameLoop();
        var (first, _) = CreateService();
        var (second, secondGo) = CreateService();

        Assert.Same(first, PlayerDataService.Instance);
        Assert.Null(second.CurrentProfile); // duplicate guard returned before profile creation

        loop.Tick(1f / 60f); // process the deferred Destroy(gameObject)
        Assert.True(secondGo.IsDestroyed);
        Assert.Same(first, PlayerDataService.Instance);
    }

    [Fact]
    public void Start_WithoutUgs_RunsOnLocalProfile_NotCloudInitialized()
    {
        using var loop = new GameLoop();
        var (service, _) = CreateService();

        loop.Tick(1f / 60f); // Start: Deviation #14 headless main path — local profile only

        Assert.False(service.IsInitialized); // cloud init never completes headless
        Assert.NotNull(service.CurrentProfile);
    }

    [Fact]
    public void AddCrystals_AccumulatesAndRaisesBothEvents()
    {
        using var loop = new GameLoop();
        var (service, _) = CreateService();

        var balances = new List<int>();
        var profileRaises = 0;
        Action<int> onBalance = b => balances.Add(b);
        PlayerDataService.OnCrystalBalanceChanged += onBalance;
        service.OnProfileChanged += _ => profileRaises++;
        try
        {
            Assert.Equal(10, service.AddCrystals(10));
            Assert.Equal(25, service.AddCrystals(15));
            Assert.Equal(new[] { 10, 25 }, balances);
            Assert.Equal(2, profileRaises);
            Assert.Equal(25, service.GetCrystalBalance());

            // Non-positive amounts are rejected without events.
            Assert.Equal(25, service.AddCrystals(0));
            Assert.Equal(25, service.AddCrystals(-5));
            Assert.Equal(2, balances.Count);
            Assert.Equal(2, profileRaises);
        }
        finally { PlayerDataService.OnCrystalBalanceChanged -= onBalance; }
    }

    [Fact]
    public void TrySpendCrystals_InsufficientFails_SufficientDeducts()
    {
        using var loop = new GameLoop();
        var (service, _) = CreateService();
        service.AddCrystals(20);

        var balances = new List<int>();
        Action<int> onBalance = b => balances.Add(b);
        PlayerDataService.OnCrystalBalanceChanged += onBalance;
        try
        {
            Assert.False(service.TrySpendCrystals(21)); // insufficient
            Assert.False(service.TrySpendCrystals(0));  // non-positive
            Assert.Empty(balances);
            Assert.Equal(20, service.GetCrystalBalance());

            Assert.True(service.TrySpendCrystals(15));
            Assert.Equal(5, service.GetCrystalBalance());
            Assert.Equal(new[] { 5 }, balances);
        }
        finally { PlayerDataService.OnCrystalBalanceChanged -= onBalance; }
    }

    [Fact]
    public void AddXP_AccumulatesAndRaises()
    {
        using var loop = new GameLoop();
        var (service, _) = CreateService();

        var totals = new List<int>();
        Action<int> onXp = x => totals.Add(x);
        PlayerDataService.OnXPChanged += onXp;
        try
        {
            Assert.Equal(50, service.AddXP(50));
            Assert.Equal(80, service.AddXP(30));
            Assert.Equal(80, service.AddXP(-1)); // rejected, returns current
            Assert.Equal(new[] { 50, 80 }, totals);
            Assert.Equal(80, service.GetXP());
        }
        finally { PlayerDataService.OnXPChanged -= onXp; }
    }

    [Fact]
    public void UnlockReward_IsIdempotent_AndQueryable()
    {
        using var loop = new GameLoop();
        var (service, _) = CreateService();

        var profileRaises = 0;
        service.OnProfileChanged += _ => profileRaises++;

        Assert.False(service.IsRewardUnlocked("vessel.squirrel"));
        service.UnlockReward("vessel.squirrel");
        Assert.True(service.IsRewardUnlocked("vessel.squirrel"));
        Assert.Equal(1, profileRaises);

        service.UnlockReward("vessel.squirrel"); // duplicate → no-op, no event
        Assert.Equal(1, profileRaises);
        Assert.Single(service.CurrentProfile.unlockedRewardIds, "vessel.squirrel");

        service.UnlockReward(null);  // rejected
        service.UnlockReward("");    // rejected
        Assert.Equal(1, profileRaises);
    }

    [Fact]
    public void SetDisplayNameAndAvatar_SyncToGameDataAfterStart()
    {
        using var loop = new GameLoop();
        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        var (service, _) = CreateService(gameData: gameData);

        loop.Tick(1f / 60f); // Start wires OnProfileChanged += SyncProfileToGameData

        service.SetDisplayName("Garrett");
        Assert.Equal("Garrett", service.CurrentProfile.displayName);
        Assert.Equal("Garrett", gameData.LocalPlayerDisplayName);

        service.SetAvatarId(42);
        Assert.Equal(42, service.CurrentProfile.avatarId);
        Assert.Equal(42, gameData.LocalPlayerAvatarId);
    }

    [Fact]
    public void GetAvatarSprite_FindsByIdAndFallsBackToFirst()
    {
        using var loop = new GameLoop();
        var icons = MakeIcons(("alpha", 1), ("beta", 2));
        var (service, _) = CreateService(icons);

        Assert.Equal("beta", service.GetAvatarSprite(2).name);
        Assert.Equal("alpha", service.GetAvatarSprite(999).name); // unknown id → first icon
    }

    [Fact]
    public void GetAvatarSprite_NullWithoutIcons()
    {
        using var loop = new GameLoop();
        var (service, _) = CreateService(icons: null);
        Assert.Null(service.GetAvatarSprite(0));
    }

    [Fact]
    public void RefreshProfileVisuals_RaisesProfileChangedOnly()
    {
        using var loop = new GameLoop();
        var (service, _) = CreateService();

        var profileRaises = 0;
        var balanceRaises = 0;
        Action<int> onBalance = _ => balanceRaises++;
        PlayerDataService.OnCrystalBalanceChanged += onBalance;
        service.OnProfileChanged += _ => profileRaises++;
        try
        {
            service.RefreshProfileVisuals();
            Assert.Equal(1, profileRaises);
            Assert.Equal(0, balanceRaises);
        }
        finally { PlayerDataService.OnCrystalBalanceChanged -= onBalance; }
    }

    [Fact]
    public void HandleDataServiceReady_InitializesAndNotifiesCurrentTotals()
    {
        // The cloud-ready path is LIVE (#14 un-carried). With an initialized-but-empty
        // cloud repo (no cloud userId), MergeCloudProfile keeps the local profile,
        // IsInitialized flips, and both static views refresh with the current totals.
        using var loop = new GameLoop();
        var (service, _) = CreateService();
        var ds = new GameObject("ugs-data-service").AddComponent<CosmicShore.Core.UGSDataService>();
        C2C3Reflect.Set(service, "_ugsDataService", ds); // Awake created empty repos
        service.AddCrystals(12);
        service.AddXP(34);

        var balances = new List<int>();
        var xps = new List<int>();
        Action<int> onBalance = b => balances.Add(b);
        Action<int> onXp = x => xps.Add(x);
        PlayerDataService.OnCrystalBalanceChanged += onBalance;
        PlayerDataService.OnXPChanged += onXp;
        try
        {
            typeof(PlayerDataService).GetMethod("HandleDataServiceReady", C2C3Reflect.Priv)!
                .Invoke(service, null);

            Assert.True(service.IsInitialized);
            Assert.Equal(new[] { 12 }, balances);
            Assert.Equal(new[] { 34 }, xps);
        }
        finally
        {
            PlayerDataService.OnCrystalBalanceChanged -= onBalance;
            PlayerDataService.OnXPChanged -= onXp;
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════
// C3 — VesselStatus on the full RequireComponent rig
// ══════════════════════════════════════════════════════════════════════════

/// <summary>IVessel-implementing MonoBehaviour standing in for VesselController (C4).</summary>
class RigVesselController : MonoBehaviour, IVessel
{
    public event Action OnInitialized { add { } remove { } }
    public event Action OnBeforeDestroyed { add { } remove { } }
    public IVesselStatus VesselStatus { get; set; }
    public bool NetworkOwnerFlag, NetworkClientFlag;
    public bool IsNetworkOwner => NetworkOwnerFlag;
    public bool IsNetworkClient => NetworkClientFlag;
    public ulong PlayerNetId => 0;
    public ulong VesselNetId => 0;
    public ulong OwnerClientNetId => 0;
    public Transform Transform => transform;

    public void Initialize(IPlayer player) { }
    public void PerformShipControllerActions(InputEvents @event) { }
    public void StopShipControllerActions(InputEvents @event) { }
    public void Teleport(Transform transform) { }
    public void SetResourceLevels(ResourceCollection resources) { }
    public void SetShipUp(float angle) { }
    public void DisableSkimmer() { }
    public void SetBoostMultiplier(float boostMultiplier) { }
    public void SetShipMaterial(Material material) { }
    public void SetBlockSilhouettePrefab(GameObject prefab) { }
    public void SetAOEExplosionMaterial(Material material) { }
    public void SetAOEConicExplosionMaterial(Material material) { }
    public void SetSkimmerMaterial(Material material) { }
    public void SetTrailColors(Color highlightColor, Color coreColor) { }
    public void BindElementalFloat(string name, Element element) { }
    public void ToggleAIPilot(bool toggle) { }
    public void StartVessel() { }
    public bool AllowClearPrismInitialization() => false;
    public void DestroyVessel() { }
    public void ResetForPlay() { }
    public void SetPose(Pose pose) { }
    public void SetInitialSpeed(float initialSpeed) { }
    public void ChangePlayer(IPlayer player) { }
    public void ModifyThrottle(float amount, float duration) { }
    public void AddSlowedShipTransformToGameData() { }
    public void RemoveSlowedShipTransformFromGameData() { }
}

/// <summary>MonoBehaviour HUD controller double for the serialized vesselHUDController slot.</summary>
class RigHudController : MonoBehaviour, IVesselHUDController
{
    public void Initialize(IVesselStatus vesselStatus) { }
    public void SubscribeToEvents() { }
    public void UnsubscribeFromEvents() { }
    public void ShowHUD() { }
    public void HideHUD() { }
    public void SetBlockPrefab(GameObject prefab) { }
}

/// <summary>Concrete VesselAnimation (the base is abstract) counting flare stops.</summary>
class RigVesselAnimation : VesselAnimation
{
    public int StopFlareEngineCalls, StopFlareBodyCalls;
    protected override void AssignTransforms() { }
    protected override void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle) { }
    public override void StopFlareEngine() => StopFlareEngineCalls++;
    public override void StopFlareBody() => StopFlareBodyCalls++;
}

/// <summary>
/// Prefab-shaped vessel GameObject: VesselStatus plus all ten of its
/// [RequireComponent] siblings (the engine doesn't auto-add requirements — the
/// rig attaches them like the Unity prefab does), serialized refs wired before
/// activation, AIPilot wired per the V18 bare-pilot recipe.
/// </summary>
sealed class VesselStatusRig
{
    public GameObject Go;
    public VesselStatus Status;
    public RigVesselController Vessel;
    public RigHudController Hud;
    public GameObject OrientationHandle;

    public VesselPrismController PrismController;
    public ResourceSystem Resources;
    public VesselTransformer Transformer;
    public AIPilot Pilot;
    public SilhouetteController Silhouette;
    public VesselCameraCustomizer CameraCustomizer;
    public RigVesselAnimation Animation;
    public R_VesselActionHandler ActionHandler;
    public VesselCustomization Customization;
    public R_ShipElementStatsHandler ElementStats;

    public static VesselStatusRig Build(string name = "vessel")
    {
        var rig = new VesselStatusRig();
        var go = new GameObject(name);
        rig.Go = go;
        go.SetActive(false); // configure-before-activation

        // The ten RequireComponent siblings.
        rig.PrismController = go.AddComponent<VesselPrismController>();

        rig.Resources = go.AddComponent<ResourceSystem>();
        rig.Resources.Resources = new List<Resource> { new Resource { Name = "Energy" } };

        rig.Transformer = go.AddComponent<VesselTransformer>();

        rig.Pilot = go.AddComponent<AIPilot>();
        C2C3Reflect.Set(rig.Pilot, "cellData", ScriptableObject.CreateInstance<CellRuntimeDataSO>());
        C2C3Reflect.Set(rig.Pilot, "OnCellItemsUpdated", ScriptableObject.CreateInstance<ScriptableEventNoParam>());
        C2C3Reflect.Set(rig.Pilot, "abilities", new List<AIAbility>());

        rig.Silhouette = go.AddComponent<SilhouetteController>();
        rig.CameraCustomizer = go.AddComponent<VesselCameraCustomizer>();
        rig.Animation = go.AddComponent<RigVesselAnimation>();
        rig.ActionHandler = go.AddComponent<R_VesselActionHandler>();
        rig.Customization = go.AddComponent<VesselCustomization>();
        rig.ElementStats = go.AddComponent<R_ShipElementStatsHandler>();

        // IVessel + HUD doubles and VesselStatus itself.
        rig.Vessel = go.AddComponent<RigVesselController>();
        rig.Hud = go.AddComponent<RigHudController>();
        rig.Status = go.AddComponent<VesselStatus>();
        rig.Vessel.VesselStatus = rig.Status;

        rig.OrientationHandle = new GameObject("orientationHandle");
        rig.OrientationHandle.transform.SetParent(go.transform);

        C2C3Reflect.Set(rig.Status, "_shipInstance", rig.Vessel);
        C2C3Reflect.Set(rig.Status, "vesselHUDController", rig.Hud);
        C2C3Reflect.Set(rig.Status, "orientationHandle", rig.OrientationHandle);
        C2C3Reflect.Set(rig.Status, "_name", "RigVessel");
        C2C3Reflect.Set(rig.Status, "vesselType", VesselClassType.Sparrow);

        go.SetActive(true);
        return rig;
    }
}

public class VesselStatusC3Tests
{
    static readonly Type[] RequiredComponents =
    {
        typeof(VesselPrismController), typeof(ResourceSystem), typeof(VesselTransformer),
        typeof(AIPilot), typeof(SilhouetteController), typeof(VesselCameraCustomizer),
        typeof(VesselAnimation), typeof(R_VesselActionHandler), typeof(VesselCustomization),
        typeof(R_ShipElementStatsHandler),
    };

    // ------------------------------------------------------------- conformance freeze

    [Fact]
    public void VesselStatus_IsTheConcreteVerbatimIVesselStatus()
    {
        Assert.True(typeof(IVesselStatus).IsAssignableFrom(typeof(VesselStatus)));
        Assert.True(typeof(MonoBehaviour).IsAssignableFrom(typeof(VesselStatus)));

        // Source remark freeze: "Keep this class as monobehaviour, as the network
        // vessel status needs to be a network behaviour" — VesselStatus is NOT a
        // NetworkBehaviour and carries no NetworkVariables; kinematic replication
        // (n_Speed/n_Course/n_BlockRotation) lives on VesselController (C4), which
        // follows the InputStatus IsSpawned-switch precedent.
        Assert.False(typeof(NetworkBehaviour).IsAssignableFrom(typeof(VesselStatus)));
        var networkVarFields = typeof(VesselStatus)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.FieldType.IsGenericType &&
                        f.FieldType.GetGenericTypeDefinition() == typeof(NetworkVariable<>));
        Assert.Empty(networkVarFields);
    }

    [Fact]
    public void VesselStatus_RequireComponentSet_IsFrozen()
    {
        var declared = typeof(VesselStatus)
            .GetCustomAttributes(typeof(RequireComponentAttribute), false)
            .Cast<RequireComponentAttribute>()
            .Select(a => a.m_Type0)
            .ToHashSet();
        Assert.Equal(RequiredComponents.ToHashSet(), declared);
    }

    // ------------------------------------------------------------- lazy accessors

    [Fact]
    public void LazyAccessors_ResolveTheAttachedSiblings_AndCache()
    {
        using var loop = new GameLoop();
        var rig = VesselStatusRig.Build();
        var s = rig.Status;

        Assert.Same(rig.ActionHandler, s.ActionHandler);
        Assert.Same(rig.Customization, s.Customization);
        Assert.Same(rig.Animation, s.VesselAnimation); // subclass found through abstract slot
        Assert.Same(rig.PrismController, s.VesselPrismController);
        Assert.Same(rig.Resources, s.ResourceSystem);
        Assert.Same(rig.Transformer, s.VesselTransformer);
        Assert.Same(rig.Pilot, s.AIPilot);
        Assert.Same(rig.Silhouette, s.Silhouette);
        Assert.Same(rig.CameraCustomizer, s.VesselCameraCustomizer);
        Assert.Same(rig.ElementStats, s.ElementalStatsHandler);

        // Cached: second read returns the identical instance.
        Assert.Same(s.ResourceSystem, s.ResourceSystem);
        Assert.Same(s.AIPilot, s.AIPilot);
    }

    [Fact]
    public void LazyAccessor_AddsComponentWhenMissing()
    {
        using var loop = new GameLoop();
        var rig = VesselStatusRig.Build();

        // AICinematicBehavior is not in the RequireComponent set and not pre-attached:
        // the accessor GetOrAdds it on first touch, then reuses it.
        Assert.Null(rig.Go.GetComponent<AICinematicBehavior>());
        var added = rig.Status.AICinematicBehavior;
        Assert.NotNull(added);
        Assert.Same(added, rig.Go.GetComponent<AICinematicBehavior>());
        Assert.Same(added, rig.Status.AICinematicBehavior);
    }

    // ------------------------------------------------------------- fail-loud nulls

    [Fact]
    public void Vessel_FailLoud_NullWithoutInspectorWiring()
    {
        using var loop = new GameLoop();
        var go = new GameObject("bare");
        var status = go.AddComponent<VesselStatus>();

        Assert.Null(status.Vessel); // logs "ShipInstance is not referenced…", returns null
    }

    [Fact]
    public void VesselHUDController_FailLoud_NullOrWrongType()
    {
        using var loop = new GameLoop();
        var go = new GameObject("bare");
        var status = go.AddComponent<VesselStatus>();

        Assert.Null(status.VesselHUDController); // unwired

        var wrong = go.AddComponent<RigVesselController>(); // MonoBehaviour, not a HUD controller
        C2C3Reflect.Set(status, "vesselHUDController", wrong);
        Assert.Null(status.VesselHUDController); // wrong interface → fail-loud null

        var hud = go.AddComponent<RigHudController>();
        C2C3Reflect.Set(status, "vesselHUDController", hud);
        Assert.Same(hud, status.VesselHUDController);
    }

    // ------------------------------------------------------------- property routing

    [Fact]
    public void InterfaceRouting_WritesThroughToTheConcreteState()
    {
        using var loop = new GameLoop();
        var rig = VesselStatusRig.Build();
        IVesselStatus iface = rig.Status;

        iface.Speed = 42.5f;
        iface.Course = new Vector3(1f, 2f, 3f);
        iface.blockRotation = Quaternion.Euler(0f, 90f, 0f);
        iface.IsDrifting = true;
        iface.IsBoosting = true;
        iface.BoostMultiplier = 6f;
        iface.ChargedBoostCharge = 0.5f;
        iface.IsTranslationRestricted = true;

        Assert.Equal(42.5f, rig.Status.Speed);
        Assert.Equal(new Vector3(1f, 2f, 3f), rig.Status.Course);
        Assert.Equal(iface.blockRotation, rig.Status.blockRotation);
        Assert.True(rig.Status.IsDrifting);
        Assert.True(rig.Status.IsBoosting);
        Assert.Equal(6f, rig.Status.BoostMultiplier);
        Assert.Equal(0.5f, rig.Status.ChargedBoostCharge);
        Assert.True(rig.Status.IsTranslationRestricted);
        iface.IsTranslationRestricted = true; // same-value early-out keeps state
        Assert.True(rig.Status.IsTranslationRestricted);

        // Serialized meta + defaults.
        Assert.Equal("RigVessel", iface.Name);
        Assert.Equal(VesselClassType.Sparrow, iface.VesselType);
        Assert.Equal(4f, VesselStatusRig.Build("defaults").Status.BoostMultiplier);
        Assert.Equal(1f, rig.Status.Inertia);
        Assert.Same(rig.OrientationHandle, iface.OrientationHandle);
    }

    [Fact]
    public void ShipTransform_AndNetworkFlags_RouteThroughVessel()
    {
        using var loop = new GameLoop();
        var rig = VesselStatusRig.Build();
        IVesselStatus iface = rig.Status;

        Assert.Same(rig.Vessel, iface.Vessel);
        Assert.Same(rig.Go.transform, iface.ShipTransform); // Vessel.Transform routing

        Assert.False(iface.IsNetworkOwner);
        Assert.False(iface.IsNetworkClient);
        rig.Vessel.NetworkOwnerFlag = true;
        rig.Vessel.NetworkClientFlag = true;
        Assert.True(iface.IsNetworkOwner);  // live read-through, no snapshotting
        Assert.True(iface.IsNetworkClient);
    }

    // ------------------------------------------------------------- restored V19 chain

    [Fact]
    public void AutoPilotEnabled_DefaultMember_TracksAIPilotOnTheRig()
    {
        using var loop = new GameLoop();
        var rig = VesselStatusRig.Build();
        IVesselStatus iface = rig.Status;

        Assert.False(iface.AutoPilotEnabled);
        rig.Pilot.StartAIPilot();
        Assert.True(iface.AutoPilotEnabled);
        rig.Pilot.StopAIPilot();
        Assert.False(iface.AutoPilotEnabled);
    }

    [Fact]
    public void RestoredV19_SilhouetteHeadTracking_SkipsWhileAutopilotEngaged()
    {
        using var loop = new GameLoop();
        var rig = VesselStatusRig.Build();
        rig.Silhouette.Initialize(rig.Status); // _vessel = status.Vessel (RigVesselController)

        var onBlockCreated = typeof(SilhouetteController).GetMethod("OnBlockCreated", C2C3Reflect.Priv)!;
        var args = new object[] { 1f, 2f, 3f, 4f, 5f };

        rig.Pilot.StartAIPilot();
        onBlockCreated.Invoke(rig.Silhouette, args); // restored guard rejects autopilot blocks
        Assert.False((bool)C2C3Reflect.Get(rig.Silhouette, "_haveHead"));

        rig.Pilot.StopAIPilot();
        onBlockCreated.Invoke(rig.Silhouette, args);
        Assert.True((bool)C2C3Reflect.Get(rig.Silhouette, "_haveHead"));
    }

    // ------------------------------------------------------------- ResetForPlay

    [Fact]
    public void ResetForPlay_RestoresDocumentedState_AndCascades()
    {
        using var loop = new GameLoop();
        var rig = VesselStatusRig.Build();
        var s = rig.Status;

        // Dirty every flag ResetForPlay owns.
        s.IsStationary = false;
        s.IsBoosting = true;
        s.IsChargedBoostDischarging = true;
        s.IsDrifting = true;
        s.IsAttached = true;
        s.AttachedPrism = PrismTestRig.Create("attached").Prism;
        s.GunsActive = true;
        s.ChargedBoostCharge = 0.25f;
        s.IsSlowed = true;
        s.IsOverheating = true;

        // Dirty the cascade targets.
        rig.Resources.Resources[0].CurrentAmount = 0.2f;
        rig.Go.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
        rig.PrismController.Trail.Add(PrismTestRig.CreatePrismAt(new Vector3(1f, 0f, 0f), "trailed"));
        Assert.Single(rig.PrismController.Trail.TrailList);

        s.ResetForPlay();

        Assert.True(s.IsStationary);
        Assert.False(s.IsBoosting);
        Assert.False(s.IsChargedBoostDischarging);
        Assert.False(s.IsDrifting);
        Assert.False(s.IsAttached);
        Assert.Null(s.AttachedPrism);
        Assert.False(s.GunsActive);
        Assert.Equal(1f, s.ChargedBoostCharge);
        Assert.False(s.IsSlowed);
        Assert.False(s.IsOverheating);

        // Cascade: ResourceSystem.Reset → initial amounts; VesselTransformer.ResetTransformer
        // → identity rotation + default speed params; VesselPrismController → trails cleared;
        // VesselAnimation → both flares stopped.
        Assert.Equal(rig.Resources.Resources[0].InitialAmount, rig.Resources.Resources[0].CurrentAmount);
        Assert.True(rig.Go.transform.rotation == Quaternion.identity);
        Assert.Equal(rig.Transformer.DefaultMinimumSpeed, rig.Transformer.MinimumSpeed);
        Assert.Empty(rig.PrismController.Trail.TrailList);
        Assert.Equal(1, rig.Animation.StopFlareEngineCalls);
        Assert.Equal(1, rig.Animation.StopFlareBodyCalls);
    }
}
