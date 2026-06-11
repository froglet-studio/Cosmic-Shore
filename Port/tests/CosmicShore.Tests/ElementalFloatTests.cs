using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// V6 keystone coverage: ElementalFloat LerpUnclamped scaling, ElementalShipComponent
// reflective binding, ActionExecutorRegistry lookup, IVesselStatus default-member
// contracts. ResourceSystem regression is covered by ResourceSystemTests continuing
// to pass against the restored ElementalShipComponent base (Deviation #9 closed).
public class ElementalFloatTests
{
    // ── test doubles ────────────────────────────────────────────────────────

    class StubVesselStatus : IVesselStatus
    {
        public IVessel Vessel { get; set; }
        public bool AlignmentEnabled { get; set; }
        public Material AOEConicExplosionMaterial { get; set; }
        public Material AOEExplosionMaterial { get; set; }
        public bool IsAttached { get; set; }
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
        public List<GameObject> ShipGeometries { get; set; }
        public Transform ShipTransform => null;
        public string Name => "stub";
        public VesselClassType VesselType => VesselClassType.Manta;
        public GameObject OrientationHandle => null;
        public Material ShipMaterial { get; set; }
        public Material SkimmerMaterial { get; set; }
        public float Speed { get; set; }
        public bool IsSingleStickControls { get; set; }
        public bool IsSlowed { get; set; }
        public bool IsStationary { get; set; }
        public bool IsTranslationRestricted { get; set; }
        public IVesselHUDController VesselHUDController => null;
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
        public Transform Transform => null;

        public readonly List<(string name, Element element)> Bound = new();
        public void BindElementalFloat(string name, Element element) => Bound.Add((name, element));

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
        public void ToggleAIPilot(bool toggle) { }
        public void StartVessel() { }
        public bool AllowClearPrismInitialization() => false;
        public void DestroyVessel() { }
        public void ResetForPlay() { }
        public void SetPose(Pose pose) { }
        public void ChangePlayer(IPlayer player) { }
        public void ModifyThrottle(float amount, float duration) { }
        public void AddSlowedShipTransformToGameData() { }
        public void RemoveSlowedShipTransformFromGameData() { }
    }

    class TestElementalComponent : ElementalShipComponent
    {
        ElementalFloat driftScale = new ElementalFloat(3f);
        public ElementalFloat DriftScale => driftScale;
    }

    class TestExecutor : ShipActionExecutorBase
    {
        public IVesselStatus Initialized;
        public override void Initialize(IVesselStatus shipStatus) => Initialized = shipStatus;
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    // Min/Max/element are inspector-serialized in the original; tests stand in for
    // the deserializer, exactly as ElementalShipComponent's own reflective contract does.
    static void Configure(ElementalFloat f, float min, float max, Element element)
    {
        typeof(ElementalFloat).GetField("Min", Priv).SetValue(f, min);
        typeof(ElementalFloat).GetField("Max", Priv).SetValue(f, max);
        typeof(ElementalFloat).GetField("element", Priv).SetValue(f, element);
    }

    static (StubVessel vessel, ResourceSystem rs) MakeVessel()
    {
        var go = new GameObject("vessel");
        go.SetActive(false); // configure before Awake (runtime-AddComponent pattern)
        var rs = go.AddComponent<ResourceSystem>();
        rs.Resources = new List<Resource>();
        go.SetActive(true);
        var vessel = new StubVessel { VesselStatus = new StubVesselStatus { ResourceSystem = rs } };
        return (vessel, rs);
    }

    // ── ElementalFloat ──────────────────────────────────────────────────────

    [Fact]
    public void VesselSetter_WhenEnabled_BindsNameAndElement()
    {
        using var loop = new GameLoop();
        var (vessel, _) = MakeVessel();
        var f = new ElementalFloat(5f) { Enabled = true, Name = "Boost.speed" };
        Configure(f, 0f, 1f, Element.Charge);

        f.Vessel = vessel;

        Assert.Equal(("Boost.speed", Element.Charge), Assert.Single(vessel.Bound));
    }

    [Fact]
    public void VesselSetter_WhenDisabled_DoesNotBindOrScale()
    {
        using var loop = new GameLoop();
        var (vessel, rs) = MakeVessel();
        var f = new ElementalFloat(5f) { Enabled = false, Name = "Boost.speed" };
        Configure(f, 10f, 20f, Element.Mass);

        f.Vessel = vessel;
        rs.AdjustLevel(Element.Mass, 0.5f);

        Assert.Empty(vessel.Bound);
        Assert.Equal(5f, f.Value);
    }

    [Theory]
    [InlineData(0.5f, 15f)]   // level  5 → midpoint
    [InlineData(1.0f, 20f)]   // level 10 → Max
    [InlineData(1.5f, 25f)]   // level 15 → extrapolates past Max (comeback range)
    [InlineData(-0.5f, 5f)]   // level -5 → extrapolates below Min
    public void ScaleValueWithLevel_LerpsUnclampedAcrossFullLevelRange(float normalized, float expected)
    {
        using var loop = new GameLoop();
        var (vessel, rs) = MakeVessel();
        var f = new ElementalFloat(0f) { Enabled = true, Name = "Drift.scale" };
        Configure(f, 10f, 20f, Element.Mass);
        f.Vessel = vessel;

        rs.SetElementLevel(Element.Mass, normalized);

        Assert.Equal(expected, f.Value, 3);
    }

    [Fact]
    public void ScaleValueWithLevel_IgnoresOtherElements()
    {
        using var loop = new GameLoop();
        var (vessel, rs) = MakeVessel();
        var f = new ElementalFloat(7f) { Enabled = true, Name = "Trail.length" };
        Configure(f, 10f, 20f, Element.Time);
        f.Vessel = vessel;

        rs.AdjustLevel(Element.Mass, 0.5f);

        Assert.Equal(7f, f.Value);
    }

    // ── ElementalShipComponent reflective binding ───────────────────────────

    [Fact]
    public void BindElementalFloats_BindsPrivateFields_ComposingTypeDotFieldName()
    {
        using var loop = new GameLoop();
        var (vessel, rs) = MakeVessel();
        var go = new GameObject("component");
        var component = go.AddComponent<TestElementalComponent>();
        component.DriftScale.Enabled = true;
        Configure(component.DriftScale, 0f, 10f, Element.Space);

        component.BindElementalFloats(vessel);

        Assert.Equal(("TestElementalComponent.driftScale", Element.Space), Assert.Single(vessel.Bound));

        rs.SetElementLevel(Element.Space, 1.0f);
        Assert.Equal(10f, component.DriftScale.Value, 3);
    }

    [Fact]
    public void ResourceSystem_IsAnElementalShipComponent_DeviationNineClosed()
    {
        Assert.True(typeof(ElementalShipComponent).IsAssignableFrom(typeof(ResourceSystem)));
    }

    // ── ActionExecutorRegistry ──────────────────────────────────────────────

    [Fact]
    public void InitializeAll_InitializesListedExecutors_AndGetReturnsThem()
    {
        using var loop = new GameLoop();
        var go = new GameObject("registry");
        var registry = go.AddComponent<ActionExecutorRegistry>();
        var executor = go.AddComponent<TestExecutor>();
        typeof(ActionExecutorRegistry).GetField("_executors", Priv)
            .SetValue(registry, new List<ShipActionExecutorBase> { executor });
        var status = new StubVesselStatus();

        registry.InitializeAll(status);

        Assert.Same(status, registry.VesselStatus);
        Assert.Same(status, executor.Initialized);
        Assert.Same(executor, registry.Get<TestExecutor>());
    }

    [Fact]
    public void Get_FallsBackToComponentSearch_WhenNotRegistered()
    {
        using var loop = new GameLoop();
        var go = new GameObject("registry");
        var registry = go.AddComponent<ActionExecutorRegistry>();
        var executor = go.AddComponent<TestExecutor>();

        Assert.Same(executor, registry.Get<TestExecutor>());
    }

    // ── IVesselStatus default-member contracts ──────────────────────────────

    [Fact]
    public void VesselStatusDefaults_NullPlayer_FallBackToNoNameAndJade()
    {
        IVesselStatus status = new StubVesselStatus(); // Player left null
        Assert.Equal("No-name", status.PlayerName);
        Assert.Equal(Domains.Jade, status.Domain);
    }
}
