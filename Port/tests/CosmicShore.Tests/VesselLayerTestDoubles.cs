using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// V15: a fully-assembled prism GameObject. Prism.Awake hard-requires its four sibling
// managers plus MeshRenderer/BoxCollider, and the restored PrismTeamManager /
// PrismStateManager paths evaluate _themeManagerData getters at the call site, so every
// rig prism gets a theme whose per-domain material sets exist but hold null materials —
// MaterialPropertyAnimator.UpdateMaterial rejects null materials and returns, leaving
// state-machine behavior observable without a render stack. All four prism event
// channels are wired (fail-loud SOAP policy: Raise() on a null channel should throw,
// so tests wire real instances and may subscribe to observe).
internal sealed class PrismRig
{
    public Prism Prism;
    public GameObject GameObject;
    public PrismStateManager StateManager;
    public PrismTeamManager TeamManager;
    public PrismScaleAnimator ScaleAnimator;
    public MaterialPropertyAnimator MaterialAnimator;
    public ScriptableEventPrismStats CreatedEvent;
    public ScriptableEventPrismStats DestroyedEvent;
    public ScriptableEventPrismStats RestoredEvent;
    public ScriptableEventPrismStats StolenEvent;
    public ScriptableEventPrismStats VolumeEvent;
    public PrismEventChannelWithReturnSO ImpactChannel;
    public ThemeManagerDataContainerSO Theme;
}

internal static class PrismTestRig
{
    static readonly System.Reflection.BindingFlags Priv =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    static void Set(object target, string field, object value)
        => target.GetType().GetField(field, Priv)!.SetValue(target, value);

    /// <summary>Theme with a material set per domain; materials stay null (UpdateMaterial no-ops).</summary>
    public static ThemeManagerDataContainerSO CreateTheme()
    {
        var theme = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
        theme.TeamMaterialSets = new Dictionary<Domains, SO_MaterialSet>();
        foreach (var domain in new[] { Domains.Jade, Domains.Ruby, Domains.Blue, Domains.Gold })
            theme.TeamMaterialSets[domain] = ScriptableObject.CreateInstance<SO_MaterialSet>();
        return theme;
    }

    /// <summary>
    /// Builds a complete prism GameObject (configure-before-activation pattern), wiring
    /// the theme into every consumer and the SOAP channels into their serialized fields.
    /// <paramref name="beforeActivate"/> runs after all components are attached but
    /// before SetActive(true) drives the Awake chain.
    /// </summary>
    public static PrismRig Create(string name = "prism", Action<PrismRig> beforeActivate = null)
    {
        var rig = new PrismRig
        {
            Theme = CreateTheme(),
            CreatedEvent = ScriptableObject.CreateInstance<ScriptableEventPrismStats>(),
            DestroyedEvent = ScriptableObject.CreateInstance<ScriptableEventPrismStats>(),
            RestoredEvent = ScriptableObject.CreateInstance<ScriptableEventPrismStats>(),
            StolenEvent = ScriptableObject.CreateInstance<ScriptableEventPrismStats>(),
            VolumeEvent = ScriptableObject.CreateInstance<ScriptableEventPrismStats>(),
            ImpactChannel = ScriptableObject.CreateInstance<PrismEventChannelWithReturnSO>(),
        };

        var go = new GameObject(name);
        rig.GameObject = go;
        go.SetActive(false);

        go.AddComponent<MeshRenderer>();
        go.AddComponent<BoxCollider>();

        rig.MaterialAnimator = go.AddComponent<MaterialPropertyAnimator>();
        Set(rig.MaterialAnimator, "_themeManagerData", rig.Theme);

        rig.ScaleAnimator = go.AddComponent<PrismScaleAnimator>();
        Set(rig.ScaleAnimator, "onPrismVolumeModified", rig.VolumeEvent);

        rig.TeamManager = go.AddComponent<PrismTeamManager>();
        Set(rig.TeamManager, "_themeManagerData", rig.Theme);
        Set(rig.TeamManager, "onPrismStolen", rig.StolenEvent);

        rig.StateManager = go.AddComponent<PrismStateManager>();
        Set(rig.StateManager, "_themeManagerData", rig.Theme);

        rig.Prism = go.AddComponent<Prism>();
        rig.Prism.prismProperties = new PrismProperties();
        Set(rig.Prism, "_onTrailBlockCreatedEventChannel", rig.CreatedEvent);
        Set(rig.Prism, "_onTrailBlockDestroyedEventChannel", rig.DestroyedEvent);
        Set(rig.Prism, "_onTrailBlockRestoredEventChannel", rig.RestoredEvent);
        Set(rig.Prism, "OnBlockImpactedEventChannel", rig.ImpactChannel); // internal field — reflection

        beforeActivate?.Invoke(rig);
        go.SetActive(true);
        return rig;
    }

    /// <summary>Rig prism positioned at <paramref name="position"/> (Trail / density-grid tests).</summary>
    public static Prism CreatePrismAt(Vector3 position, string name = "prism")
    {
        var rig = Create(name);
        rig.GameObject.transform.position = position;
        return rig.Prism;
    }
}

// Shared vessel-layer test doubles: minimal IVessel / IVesselStatus implementations
// for exercising ElementalFloat binding, VesselAnimation, and action plumbing.
    class StubVesselStatus : IVesselStatus
    {
        public IVessel Vessel { get; set; }
        // V18: AIPilot/AICinematicBehavior restored on IVesselStatus. Settable so
        // autopilot-guard tests can wire a live AIPilot. The explicit AutoPilotEnabled
        // implementation treats a null AIPilot as autopilot-off (the interface default
        // dereferences AIPilot unconditionally).
        public AIPilot AIPilot { get; set; }
        public AICinematicBehavior AICinematicBehavior { get; set; }
        public bool AutoPilotEnabled => AIPilot != null && AIPilot.AutoPilotEnabled;
        public bool AlignmentEnabled { get; set; }
        public Material AOEConicExplosionMaterial { get; set; }
        public Material AOEExplosionMaterial { get; set; }
        public bool IsAttached { get; set; }
        public Prism AttachedPrism { get; set; }
        public Quaternion blockRotation { get; set; }
        public bool IsBoosting { get; set; }
        public float BoostMultiplier { get; set; }
        public float Inertia => 0f;
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
        public ResourceSystem ResourceSystem { get; set; }
        public VesselAnimation VesselAnimation => null;
        // Overrides the interface default (Player.InputStatus) so animation tests can
        // drive input without a full IPlayer chain.
        public IInputStatus InputStatus { get; set; }
        public List<GameObject> ShipGeometries { get; set; }
        public Transform ShipTransform => null;
        public VesselTransformer VesselTransformer { get; set; }
        public string Name => "stub";
        public VesselClassType VesselType => VesselClassType.Manta;
        // V16: Skimmer members restored on IVesselStatus.
        public Skimmer NearFieldSkimmer { get; set; }
        public Skimmer FarFieldSkimmer { get; set; }
        public GameObject OrientationHandle => null;
        public Material ShipMaterial { get; set; }
        public Material SkimmerMaterial { get; set; }
        public float Speed { get; set; }
        public bool IsSingleStickControls { get; set; }
        public bool IsSlowed { get; set; }
        public bool IsStationary { get; set; }
        public bool IsTranslationRestricted { get; set; }
        public IVesselHUDController VesselHUDController => null;
        public VesselCustomization Customization => null;
        public VesselPrismController VesselPrismController { get; set; }
        public R_VesselActionHandler ActionHandler => null;
        public R_ShipElementStatsHandler ElementalStatsHandler => null;
        public bool IsNetworkOwner => false;
        public bool IsNetworkClient => false;
        public void ResetForPlay() { }
    }

    class StubVessel : IVessel
    {
        public event Action OnInitialized { add { } remove { } }
        public event Action OnBeforeDestroyed { add { } remove { } }
        public IVesselStatus VesselStatus { get; set; }
        public bool IsNetworkOwner => false;
        public bool IsNetworkClient => false;
        public ulong PlayerNetId => 0;
        public ulong VesselNetId => 0;
        public ulong OwnerClientNetId => 0;
        // V16: settable so DriftTrailActionExecutor tests can give the vessel a real
        // transform (the dot-product loop reads Transform.forward); defaults to null.
        public Transform Transform { get; set; }

        public readonly List<(string name, Element element)> Bound = new();
        public void BindElementalFloat(string name, Element element) => Bound.Add((name, element));

        public int SlowedAdds, SlowedRemoves;
        public Material LastShipMaterial, LastSkimmerMaterial, LastAOEMaterial, LastConicMaterial;
        public GameObject LastSilhouettePrefab;
        public (Color highlight, Color core)? LastTrailColors;

        public void Initialize(IPlayer player) { }
        public void PerformShipControllerActions(InputEvents @event) { }
        public void StopShipControllerActions(InputEvents @event) { }
        public void Teleport(Transform transform) { }
        public void SetResourceLevels(ResourceCollection resources) { }
        public void SetShipUp(float angle) { }
        public void DisableSkimmer() { }
        public void SetBoostMultiplier(float boostMultiplier) { }
        public void SetShipMaterial(Material material) => LastShipMaterial = material;
        public void SetBlockSilhouettePrefab(GameObject prefab) => LastSilhouettePrefab = prefab;
        public void SetAOEExplosionMaterial(Material material) => LastAOEMaterial = material;
        public void SetAOEConicExplosionMaterial(Material material) => LastConicMaterial = material;
        public void SetSkimmerMaterial(Material material) => LastSkimmerMaterial = material;
        public void SetTrailColors(Color highlightColor, Color coreColor) => LastTrailColors = (highlightColor, coreColor);
        public void ToggleAIPilot(bool toggle) { }
        public void StartVessel() { }
        public bool AllowClearPrismInitialization() => false;
        public void DestroyVessel() { }
        public void ResetForPlay() { }
        public void SetPose(Pose pose) { }
        public void ChangePlayer(IPlayer player) { }
        public void ModifyThrottle(float amount, float duration) { }
        public void AddSlowedShipTransformToGameData() => SlowedAdds++;
        public void RemoveSlowedShipTransformFromGameData() => SlowedRemoves++;
    }

