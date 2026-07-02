using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// V16: skimmer layer — NudgeShardPoolManager pool semantics (GenericPoolManager
// subclass), NudgeShard trigger math (invoked reflectively; engine trigger dispatch is
// phase-2 physics), Skimmer booster-ring/Gaussian math, and DriftTrailActionExecutor
// start/stop against the ActionExecutorRegistry. The V16-era staged deviations have
// since been restored (VesselPrismController at V17, AutoPilotEnabled at V18); the rigs
// here use a null-AIPilot stub status (autopilot off), so the altitude event still fires.
public class SkimmerLayerTests
{
    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    public SkimmerLayerTests()
    {
        // Process-global statics survive GameLoop disposal (same reset as PrismManagerTests).
        typeof(Singleton<PrismTimerManager>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
        typeof(AudioSystem)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
        typeof(Singleton<PrismSpatialIndex>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);

        // Project layer (ProjectSettings/TagManager.asset index 8); idempotent.
        LayerMask.SetLayerName(8, "Ships");
    }

    static void Set(object target, System.Type declaringType, string field, object value)
        => declaringType.GetField(field, Priv)!.SetValue(target, value);

    static object Get(object target, System.Type declaringType, string field)
        => declaringType.GetField(field, Priv)!.GetValue(target);

    static AudioSystem MakeAudio() => new GameObject("audio").AddComponent<AudioSystem>();

    // ── doubles ─────────────────────────────────────────────────────────────

    class SkimmerPlayerStub : IPlayer
    {
        public Domains Domain { get; set; } = Domains.Jade;
        public string Name { get; set; } = "skim-pilot";
        public int AvatarId => 0;
        public string PlayerUUID => Name;
        public IVessel Vessel => null;
        public InputController InputController => null;
        public IInputStatus InputStatus => null;
        public IRoundStats RoundStats => null;
        public bool IsActive { get; set; } = true;
        public bool IsInitializedAsAI => false;
        public bool IsMultiplayerOwner => false;
        public bool IsNetworkOwner => false;
        public bool IsNetworkClient => false;
        public bool IsLocalUser => true;
        public ulong PlayerNetId => 0;
        public ulong VesselNetId => 0;
        public ulong OwnerClientNetId => 0;
        public Transform Transform { get; set; }
        public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel) { }
        public void InitializeForMultiplayerMode(IVessel vessel) { }
        public void ToggleGameObject(bool toggle) { }
        public void DestroyPlayer() { }
        public void StartPlayer() { }
        public void ResetForPlay() { }
        public void ChangeVessel(IVessel vessel) { }
    }

    // MonoBehaviour IVesselStatus so NudgeShard.OnTriggerEnter can discover it via
    // other.TryGetComponent on the collider's GameObject.
    class ShipStatusComponentStub : MonoBehaviour, IVesselStatus
    {
        public VesselClassType vesselType = VesselClassType.Manta;

        public IVessel Vessel => null;
        // V18: AIPilot/AICinematicBehavior restored on IVesselStatus.
        public AIPilot AIPilot => null;
        public AICinematicBehavior AICinematicBehavior => null;
        public bool AutoPilotEnabled => false;
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
        public List<GameObject> ShipGeometries { get; set; }
        public Transform ShipTransform => null;
        public VesselTransformer VesselTransformer { get; set; }
        public string Name => "ship-stub";
        public VesselClassType VesselType => vesselType;
        public Skimmer NearFieldSkimmer => null;
        public Skimmer FarFieldSkimmer => null;
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
        public VesselCameraCustomizer VesselCameraCustomizer => null;
        public SilhouetteController Silhouette => null;
        public VesselPrismController VesselPrismController => null;
        public R_VesselActionHandler ActionHandler => null;
        public R_ShipElementStatsHandler ElementalStatsHandler => null;
        public bool IsNetworkOwner => false;
        public bool IsNetworkClient => false;
        public void ResetForPlay() { }
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static NudgeShardPoolManager MakePool(int capacity = 0)
    {
        var prefabGo = new GameObject("shardPrefab");
        prefabGo.SetActive(false);
        prefabGo.AddComponent<NudgeShard>();

        var poolGo = new GameObject("shardPool");
        poolGo.SetActive(false); // configure before Awake builds the pool
        var pool = poolGo.AddComponent<NudgeShardPoolManager>();
        var baseType = typeof(GenericPoolManager<NudgeShard>);
        Set(pool, baseType, "prefab", prefabGo.GetComponent<NudgeShard>());
        Set(pool, baseType, "defaultCapacity", capacity);
        Set(pool, baseType, "bufferSizeTarget", 0);
        Set(pool, baseType, "enableBufferMaintenance", false);
        poolGo.SetActive(true);
        return pool;
    }

    static List<NudgeShard> ActiveShards(NudgeShardPoolManager pool)
    {
        var result = new List<NudgeShard>();
        for (int i = 0; i < pool.transform.childCount; i++)
        {
            var go = pool.transform.GetChild(i).gameObject;
            if (go.activeSelf && go.TryGetComponent(out NudgeShard shard)) result.Add(shard);
        }
        return result;
    }

    static (Skimmer skimmer, StubVesselStatus status, SkimmerPlayerStub player) MakeSkimmer(
        NudgeShardPoolManager pool = null,
        ResourceSystem resourceSystem = null,
        float preScale = 8f,
        ScriptableEventString impactEvent = null)
    {
        var go = new GameObject("skimmer");
        go.SetActive(false);
        var skimmer = go.AddComponent<Skimmer>();
        if (pool != null) Set(skimmer, typeof(Skimmer), "nudgeShardPoolManager", pool);
        if (impactEvent != null) Set(skimmer, typeof(Skimmer), "onSkimmerShipImpact", impactEvent);
        go.SetActive(true);

        // Pre-Initialize authored scale: _sweetSpot = localScale.x / 4 is captured before
        // ApplyScaleIfChanged() snaps localScale to the elemental Scale value.
        go.transform.localScale = new Vector3(preScale, preScale, preScale);

        var player = new SkimmerPlayerStub { Transform = new GameObject("skim-player").transform };
        var status = new StubVesselStatus
        {
            Player = player,
            Vessel = new StubVessel(),
            ResourceSystem = resourceSystem,
        };
        skimmer.Initialize(status);
        return (skimmer, status, player);
    }

    static (Prism a, Prism b) MakeTrailPair()
    {
        var a = PrismTestRig.CreatePrismAt(new Vector3(0f, 0f, 0f), "pA");
        var b = PrismTestRig.CreatePrismAt(new Vector3(0f, 0f, 20f), "pB");
        var trail = new Trail();
        trail.Add(a);
        trail.Add(b);
        a.Trail = trail;
        b.Trail = trail;
        a.prismProperties.Index = 0;
        b.prismProperties.Index = 1;
        return (a, b);
    }

    static void InvokeTrigger(NudgeShard shard, Collider other)
        => typeof(NudgeShard).GetMethod("OnTriggerEnter", Priv)!.Invoke(shard, new object[] { other });

    static List<ShipVelocityModifier> VelocityModifiersOf(VesselTransformer transformer)
        => (List<ShipVelocityModifier>)Get(transformer, typeof(VesselTransformer), "VelocityModifiers");

    // ── IVesselStatus restore (#10c, V16 slice) ─────────────────────────────

    [Fact]
    public void IVesselStatus_SkimmerMembers_AreRestored()
    {
        Assert.Equal(typeof(Skimmer), typeof(IVesselStatus).GetProperty("NearFieldSkimmer")!.PropertyType);
        Assert.Equal(typeof(Skimmer), typeof(IVesselStatus).GetProperty("FarFieldSkimmer")!.PropertyType);
    }

    // ── NudgeShardPoolManager: GenericPoolManager subclass semantics ────────

    [Fact]
    public void Pool_Get_PositionsParentsAndActivates()
    {
        using var loop = new GameLoop();
        var pool = MakePool();
        var parent = new GameObject("host").transform;

        var shard = pool.Get(new Vector3(1f, 2f, 3f), Quaternion.identity, parent);

        Assert.True(shard.gameObject.activeSelf);
        Assert.Equal(new Vector3(1f, 2f, 3f), shard.transform.position);
        Assert.Same(parent, shard.transform.parent);
    }

    [Fact]
    public void Pool_Release_DeactivatesAndReturnsUnderPool_GetReuses()
    {
        using var loop = new GameLoop();
        var pool = MakePool();
        var shard = pool.Get(Vector3.one, Quaternion.identity, new GameObject("host").transform);

        pool.Release(shard);

        Assert.False(shard.gameObject.activeSelf);
        Assert.Same(pool.transform, shard.transform.parent);

        var again = pool.Get(Vector3.zero, Quaternion.identity);
        Assert.Same(shard, again);
        Assert.True(again.gameObject.activeSelf);
    }

    [Fact]
    public void Pool_Awake_PrewarmsInactiveInstances()
    {
        using var loop = new GameLoop();
        var pool = MakePool(capacity: 3);

        Assert.Equal(3, pool.transform.childCount);
        Assert.Empty(ActiveShards(pool));
    }

    // ── NudgeShard: trigger math ────────────────────────────────────────────

    static (NudgeShard shard, GameObject shipGo, ShipStatusComponentStub status, ResourceSystem rs)
        MakeTriggerRig(GameLoop loop, VesselClassType vesselType)
    {
        var parent = new GameObject("shardParent");
        parent.transform.localScale = new Vector3(2f, 3f, 4f);

        var shardGo = new GameObject("shard");
        shardGo.transform.SetParent(parent.transform, false);
        var shard = shardGo.AddComponent<NudgeShard>();
        shard.Prisms = new List<Prism>();
        Set(shard, typeof(NudgeShard), "audioSystem", MakeAudio());

        var rsGo = new GameObject("rs");
        rsGo.SetActive(false);
        var rs = rsGo.AddComponent<ResourceSystem>();
        rs.Resources = new List<Resource> { new Resource { Name = "energy" } };
        rsGo.SetActive(true);

        var shipGo = new GameObject("ship");
        shipGo.layer = LayerMask.NameToLayer("Ships");
        shipGo.AddComponent<BoxCollider>();
        var status = shipGo.AddComponent<ShipStatusComponentStub>();
        status.vesselType = vesselType;
        status.VesselTransformer = new GameObject("vt").AddComponent<VesselTransformer>();
        status.ResourceSystem = rs;
        status.Player = new SkimmerPlayerStub { Domain = Domains.Ruby };

        loop.Tick(1f / 60f); // Start: shard scales by parent (40*2*3=240, 0.3*4=1.2); RS snaps to InitialAmount

        return (shard, shipGo, status, rs);
    }

    [Fact]
    public void NudgeShard_TriggerFromShip_ModifiesVelocity_ScaledByParentScale()
    {
        using var loop = new GameLoop();
        var (shard, shipGo, status, _) = MakeTriggerRig(loop, VesselClassType.Manta);

        InvokeTrigger(shard, shipGo.GetComponent<BoxCollider>());

        var modifiers = VelocityModifiersOf(status.VesselTransformer);
        Assert.Single(modifiers);
        // Displacement = 40 * scale.x(2) * scale.y(3) = 240 along the parent's forward
        Assert.Equal(new Vector3(0f, 0f, 240f), modifiers[0].initialValue);
        // Duration = 0.3 * scale.z(4)
        Assert.Equal(1.2f, modifiers[0].duration, 4);
    }

    [Fact]
    public void NudgeShard_SquirrelTrigger_GainsEnergy_AndStealsPrisms()
    {
        using var loop = new GameLoop();
        var (shard, shipGo, status, rs) = MakeTriggerRig(loop, VesselClassType.Squirrel);

        var prism = PrismTestRig.CreatePrismAt(Vector3.zero, "steal-me");
        shard.Prisms.Add(prism);
        rs.SetResourceAmount(0, 0.2f);

        InvokeTrigger(shard, shipGo.GetComponent<BoxCollider>());

        Assert.Equal(0.25f, rs.Resources[0].CurrentAmount, 4); // +energyAmount (0.05)
        Assert.Equal(Domains.Ruby, prism.Domain);              // stolen to the player's domain
        Assert.Single(VelocityModifiersOf(status.VesselTransformer));
    }

    [Fact]
    public void NudgeShard_NonShipLayer_IsIgnored()
    {
        using var loop = new GameLoop();
        var (shard, shipGo, status, _) = MakeTriggerRig(loop, VesselClassType.Manta);
        shipGo.layer = LayerMask.NameToLayer("Default");

        InvokeTrigger(shard, shipGo.GetComponent<BoxCollider>());

        Assert.Empty(VelocityModifiersOf(status.VesselTransformer));
    }

    // ── Skimmer: math + impact helpers ──────────────────────────────────────

    [Fact]
    public void ComputeGaussian_PeaksAtCenter_SymmetricMonotoneFalloff()
    {
        Assert.Equal(1f, Skimmer.ComputeGaussian(3f, 3f, 2f), 5);
        Assert.Equal(Skimmer.ComputeGaussian(4.5f, 3f, 2f), Skimmer.ComputeGaussian(1.5f, 3f, 2f), 5);
        Assert.True(Skimmer.ComputeGaussian(4f, 3f, 2f) > Skimmer.ComputeGaussian(5f, 3f, 2f));
        Assert.Equal(0.60653f, Skimmer.ComputeGaussian(1f, 0f, 1f), 4); // e^-0.5
    }

    [Fact]
    public void Initialize_AppliesElementalScale_BindsName_AndReparentsPool()
    {
        using var loop = new GameLoop();
        var pool = MakePool();
        var (skimmer, _, player) = MakeSkimmer(pool, preScale: 8f);

        // ApplyScaleIfChanged snapped localScale to Scale.Value (1) after capturing sweet spot.
        Assert.Equal(Vector3.one, skimmer.transform.localScale);
        Assert.Equal(2f, (float)Get(skimmer, typeof(Skimmer), "_sweetSpot"), 4); // 8 / 4

        // BindElementalFloats stamped the field name onto the ElementalFloat.
        var scale = (ElementalFloat)Get(skimmer, typeof(Skimmer), "Scale");
        Assert.Equal("Skimmer.Scale", (string)typeof(ElementalFloat).GetField("name", Priv)!.GetValue(scale));

        // Pool reparented under the player for lifetime ownership.
        Assert.Same(player.Transform, pool.transform.parent);
    }

    [Fact]
    public void ExecuteImpactOnShip_RaisesEventWithPlayerName()
    {
        using var loop = new GameLoop();
        var impactEvent = ScriptableObject.CreateInstance<ScriptableEventString>();
        var (skimmer, _, _) = MakeSkimmer(impactEvent: impactEvent);

        string raised = null;
        impactEvent.OnRaised += name => raised = name;

        skimmer.ExecuteImpactOnShip(null);

        Assert.Equal("skim-pilot", raised);
    }

    [Fact]
    public void ExecuteImpactOnPrism_SpawnsBoosterRing_AndReleasesAfterEightSeconds()
    {
        using var loop = new GameLoop();
        var pool = MakePool();
        var rsGo = new GameObject("rs");
        rsGo.SetActive(false);
        var rs = rsGo.AddComponent<ResourceSystem>();
        rs.Resources = new List<Resource> { new Resource { Name = "energy" } };
        rsGo.SetActive(true);

        var (skimmer, _, _) = MakeSkimmer(pool, rs, preScale: 8f);
        var (prismA, _) = MakeTrailPair();

        loop.Tick(5f); // advance past the 4s booster cooldown; runs Start chains

        skimmer.ExecuteImpactOnPrism(prismA);

        int spawned = ActiveShards(pool).Count;
        Assert.True(spawned > 0, "expected the booster ring to spawn nudge shards");

        // Cooldown: an immediate second impact must not spawn another ring.
        skimmer.ExecuteImpactOnPrism(prismA);
        Assert.Equal(spawned, ActiveShards(pool).Count);

        // DrawCircle holds the ring for 8 seconds, then releases every marker.
        for (int i = 0; i < 10; i++) loop.Tick(1f);
        Assert.Empty(ActiveShards(pool));
    }

    [Fact]
    public void ExecuteImpactOnPrism_RespectsAffectSelfDomainGuard()
    {
        using var loop = new GameLoop();
        var pool = MakePool();
        var (skimmer, status, player) = MakeSkimmer(pool, preScale: 8f);
        Set(skimmer, typeof(Skimmer), "affectSelf", false);

        var (prismA, _) = MakeTrailPair();
        prismA.Domain = player.Domain; // own-team prism

        loop.Tick(5f); // past cooldown — the guard, not the timer, must block

        skimmer.ExecuteImpactOnPrism(prismA);

        Assert.Empty(ActiveShards(pool));
    }

    // ── DriftTrailActionExecutor: start/stop against the registry ───────────

    static (DriftTrailActionExecutor exec, ActionExecutorRegistry registry, DriftTrailActionSO so,
            StubVesselStatus status, ScriptableEventNoParam turnEnd, List<float> values)
        MakeDriftRig()
    {
        var vesselGo = new GameObject("driftVessel"); // identity rotation → forward = +Z
        var vessel = new StubVessel { Transform = vesselGo.transform };
        var status = new StubVesselStatus { Vessel = vessel, Course = new Vector3(0f, 0f, 0.25f) };
        vessel.VesselStatus = status;

        var turnEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        var go = new GameObject("executor");
        go.SetActive(false); // wire serialized fields before OnEnable subscribes
        var registry = go.AddComponent<ActionExecutorRegistry>();
        var exec = go.AddComponent<DriftTrailActionExecutor>();
        Set(exec, typeof(DriftTrailActionExecutor), "OnMiniGameTurnEnd", turnEnd);
        Set(registry, typeof(ActionExecutorRegistry), "_executors",
            new List<ShipActionExecutorBase> { exec });
        go.SetActive(true);

        registry.InitializeAll(status);

        var so = ScriptableObject.CreateInstance<DriftTrailActionSO>();
        var values = new List<float>();
        exec.OnChangeDriftAltitude += v => values.Add(v);
        return (exec, registry, so, status, turnEnd, values);
    }

    // Note: every drift test must stop the loop before returning. UpdateDotProductLoopAsync
    // is fired with .Forget() (async void), and xunit's AsyncTestSyncContext waits for all
    // async-void operations at test end — an abandoned loop deadlocks the test host.

    [Fact]
    public void StartAction_RunsAltitudeLoop_WithCourseDotForward()
    {
        using var loop = new GameLoop();
        var (_, registry, so, status, _, values) = MakeDriftRig();

        so.StartAction(registry, status);

        Assert.Single(values); // loop body runs synchronously to its first await
        Assert.Equal(0.25f, values[0], 4);

        loop.Run(12, 0.05f); // one 50ms delay elapses per tick
        Assert.True(values.Count >= 5, $"expected the loop to keep firing, got {values.Count}");
        Assert.All(values, v => Assert.Equal(0.25f, v, 4));

        so.StopAction(registry, status);
    }

    [Fact]
    public void Begin_IsIdempotent_WhileLoopIsRunning()
    {
        using var loop = new GameLoop();
        var (_, registry, so, status, _, values) = MakeDriftRig();

        so.StartAction(registry, status);
        so.StartAction(registry, status); // second Begin must not start a second loop

        Assert.Single(values);
        loop.Run(1, 0.05f);
        Assert.Equal(2, values.Count); // one loop ⇒ one tick adds exactly one sample

        so.StopAction(registry, status);
    }

    [Fact]
    public void StopAction_CancelsLoop_AndSignalsLevelFlight()
    {
        using var loop = new GameLoop();
        var (_, registry, so, status, _, values) = MakeDriftRig();

        so.StartAction(registry, status);
        loop.Run(4, 0.05f);
        so.StopAction(registry, status);

        Assert.Equal(1f, values[^1]); // EndInternal reports dot product 1 (level)
        int countAfterStop = values.Count;

        loop.Run(10, 0.05f);
        Assert.Equal(countAfterStop, values.Count); // loop is dead
    }

    [Fact]
    public void MiniGameTurnEnd_EndsLoop_AndAllowsRestart()
    {
        using var loop = new GameLoop();
        var (_, registry, so, status, turnEnd, values) = MakeDriftRig();

        so.StartAction(registry, status);
        loop.Run(2, 0.05f);

        turnEnd.Raise();
        Assert.Equal(1f, values[^1]);
        int countAfterEnd = values.Count;
        loop.Run(5, 0.05f);
        Assert.Equal(countAfterEnd, values.Count);

        // _cts was cleared, so the action can begin again.
        so.StartAction(registry, status);
        Assert.Equal(countAfterEnd + 1, values.Count);
        Assert.Equal(0.25f, values[^1], 4);

        so.StopAction(registry, status);
    }
}
