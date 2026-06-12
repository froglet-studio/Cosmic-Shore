using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C4 — concrete-arc iteration 4 (VESSEL_CONCRETE.md): VesselController, the
// concrete verbatim IVessel. A NetworkBehaviour carrying the per-frame
// owner→server kinematic replication (n_Speed / n_Course / n_BlockRotation +
// n_IsTranslationRestricted), RPC direct-invoke pairs (SetPose, slowed-transform
// add/remove), the Initialize(player) chain across the full 10-component rig,
// the ChangePlayer AI/client/human matrix, and DestroyVessel's
// despawn-vs-destroy split against the C1 NetworkObject handle.
// ─────────────────────────────────────────────────────────────────────────────

// ══════════════════════════════════════════════════════════════════════════
// File-local doubles (shared VesselLayerTestDoubles untouched)
// ══════════════════════════════════════════════════════════════════════════

/// <summary>IInputStatus with a real OnToggleInputPaused event (the local-user
/// branch of R_VesselActionHandler.Initialize subscribes to it).</summary>
sealed class C4InputStatus : IInputStatus
{
    public event Action<bool> OnToggleInputPaused { add { } remove { } }
    public ScriptableEventInputEvents OnButtonPressed { get; } = new();
    public ScriptableEventInputEvents OnButtonReleased { get; } = new();
    public InputController InputController { get; set; }
    public Quaternion GetGyroRotation() => Quaternion.identity;
    public float XSum { get; set; }
    public float YSum { get; set; }
    public float XDiff { get; set; }
    public float YDiff { get; set; }
    public float Throttle { get; set; }
    public float LeftTriggerAnalog { get; set; }
    public float RightTriggerAnalog { get; set; }
    public bool Idle { get; set; }
    public bool Paused { get; set; }
    public bool IsGyroEnabled { get; set; }
    public bool InvertYEnabled { get; set; }
    public bool InvertThrottleEnabled { get; set; }
    public bool OneTouchLeft { get; set; }
    public bool CommandStickControls { get; set; }
    public InputDeviceType ActiveInputDevice { get; set; }
    public Vector2 RightJoystickHome { get; set; }
    public Vector2 LeftJoystickHome { get; set; }
    public Vector2 RightClampedPosition { get; set; }
    public Vector2 LeftClampedPosition { get; set; }
    public Vector2 RightJoystickStart { get; set; }
    public Vector2 LeftJoystickStart { get; set; }
    public Vector2 RightNormalizedJoystickPosition { get; set; }
    public Vector2 LeftNormalizedJoystickPosition { get; set; }
    public Vector2 EasedRightJoystickPosition { get; set; }
    public Vector2 EasedLeftJoystickPosition { get; set; }
    public Vector2 SingleTouchValue { get; set; }
    public Vector3 ThreeDPosition { get; set; }
    public void ResetForReplay() { }
}

/// <summary>Mutable IPlayer for the Initialize / ChangePlayer matrices.</summary>
sealed class C4Player : IPlayer
{
    public Domains Domain { get; set; } = Domains.Jade;
    public string Name { get; set; } = "c4-pilot";
    public int AvatarId => 0;
    public string PlayerUUID => Name;
    public IVessel Vessel { get; set; }
    public InputController InputController => null;
    public IInputStatus InputStatus { get; set; } = new C4InputStatus();
    public IRoundStats RoundStats => null;
    public bool IsActive { get; set; } = true;
    public bool IsInitializedAsAI { get; set; }
    public bool IsMultiplayerOwner { get; set; }
    public bool IsNetworkOwner { get; set; }
    public bool IsNetworkClient { get; set; }
    public bool IsLocalUser { get; set; } = true;
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

/// <summary>Counting HUD controller for the serialized vesselHUDController slot.</summary>
sealed class C4HudController : MonoBehaviour, IVesselHUDController
{
    public int InitializeCalls, SubscribeCalls, UnsubscribeCalls, ShowCalls, HideCalls;
    public IVesselStatus LastInitializedStatus;
    public GameObject LastBlockPrefab;
    public void Initialize(IVesselStatus vesselStatus) { InitializeCalls++; LastInitializedStatus = vesselStatus; }
    public void SubscribeToEvents() => SubscribeCalls++;
    public void UnsubscribeFromEvents() => UnsubscribeCalls++;
    public void ShowHUD() => ShowCalls++;
    public void HideHUD() => HideCalls++;
    public void SetBlockPrefab(GameObject prefab) => LastBlockPrefab = prefab;
}

/// <summary>Concrete VesselAnimation (base is abstract) counting flare stops.</summary>
sealed class C4VesselAnimation : VesselAnimation
{
    public int StopFlareEngineCalls, StopFlareBodyCalls;
    protected override void AssignTransforms() { }
    protected override void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle) { }
    public override void StopFlareEngine() => StopFlareEngineCalls++;
    public override void StopFlareBody() => StopFlareBodyCalls++;
}

/// <summary>
/// Prefab-shaped vessel GameObject around the REAL VesselController: the ten
/// RequireComponent siblings (engine doesn't auto-add requirements), child
/// near/far Skimmers, a child trail (TrailRenderer + VesselTrailCustomization),
/// a GameDataSO with theme + fail-loud SOAP events, serialized refs wired
/// before activation (configure-before-activation pattern from the C3 rig).
/// </summary>
sealed class C4VesselRig
{
    public GameObject Go;
    public VesselController Controller;
    public VesselStatus Status;
    public C4HudController Hud;
    public GameObject OrientationHandle;
    public GameObject Geometry;

    public VesselPrismController PrismController;
    public ResourceSystem Resources;
    public VesselTransformer Transformer;
    public AIPilot Pilot;
    public SilhouetteController Silhouette;
    public VesselCameraCustomizer CameraCustomizer;
    public C4VesselAnimation Animation;
    public R_VesselActionHandler ActionHandler;
    public VesselCustomization Customization;
    public R_ShipElementStatsHandler ElementStats;

    public Skimmer NearSkimmer;
    public Skimmer FarSkimmer;
    public TrailRenderer TrailRenderer;
    public VesselTrailCustomization TrailCustomization;

    public GameDataSO GameData;
    public ThemeManagerDataContainerSO Theme;
    public SO_MaterialSet JadeSet;
    public ScriptableEventNoParam OnVesselNetworkSpawned;
    public ScriptableEventTransform OnInitializePlayerCamera;

    public static C4VesselRig Build(string name = "c4vessel")
    {
        var rig = new C4VesselRig();

        // Shared data: theme with a fully-populated Jade material set so the
        // ShipHelper.SetShipProperties leg of Initialize is observable.
        rig.Theme = PrismTestRig.CreateTheme();
        rig.JadeSet = rig.Theme.TeamMaterialSets[Domains.Jade];
        rig.JadeSet.ShipMaterial = new Material((Shader)null) { name = "jade-ship" };
        rig.JadeSet.AOEExplosionMaterial = new Material((Shader)null) { name = "jade-aoe" };
        rig.JadeSet.AOEConicExplosionMaterial = new Material((Shader)null) { name = "jade-conic" };
        rig.JadeSet.SkimmerMaterial = new Material((Shader)null) { name = "jade-skim" };
        rig.JadeSet.BlockSilhouettePrefab = new GameObject("jade-silhouette");

        rig.GameData = ScriptableObject.CreateInstance<GameDataSO>();
        rig.GameData.ThemeManagerData = rig.Theme;
        rig.OnVesselNetworkSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        rig.GameData.OnVesselNetworkSpawned = rig.OnVesselNetworkSpawned;

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
        rig.OnInitializePlayerCamera = ScriptableObject.CreateInstance<ScriptableEventTransform>();
        C2C3Reflect.Set(rig.CameraCustomizer, "OnInitializePlayerCamera", rig.OnInitializePlayerCamera);

        rig.Animation = go.AddComponent<C4VesselAnimation>();

        rig.ActionHandler = go.AddComponent<R_VesselActionHandler>();
        C2C3Reflect.Set(rig.ActionHandler, "_onButtonPressed", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
        C2C3Reflect.Set(rig.ActionHandler, "_onButtonReleased", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
        C2C3Reflect.Set(rig.ActionHandler, "_resourceEventClassActions", new List<ResourceEventShipActionMapping>());

        rig.Geometry = new GameObject("geometry");
        rig.Geometry.transform.SetParent(go.transform);
        rig.Customization = go.AddComponent<VesselCustomization>();
        C2C3Reflect.Set(rig.Customization, "_shipGeometries", new List<GameObject> { rig.Geometry });

        rig.ElementStats = go.AddComponent<R_ShipElementStatsHandler>();

        // Subject + HUD double + VesselStatus.
        rig.Hud = go.AddComponent<C4HudController>();
        rig.Controller = go.AddComponent<VesselController>();
        C2C3Reflect.Set(rig.Controller, "gameData", rig.GameData);
        rig.Status = go.AddComponent<VesselStatus>();

        // Child skimmers (near + far), orientation handle, tinted trail.
        rig.NearSkimmer = NewChildSkimmer(go, "nearFieldSkimmer");
        rig.FarSkimmer = NewChildSkimmer(go, "farFieldSkimmer");

        rig.OrientationHandle = new GameObject("orientationHandle");
        rig.OrientationHandle.transform.SetParent(go.transform);

        var trailGo = new GameObject("trail");
        trailGo.transform.SetParent(go.transform);
        rig.TrailRenderer = trailGo.AddComponent<TrailRenderer>();
        rig.TrailCustomization = trailGo.AddComponent<VesselTrailCustomization>();

        C2C3Reflect.Set(rig.Status, "_shipInstance", rig.Controller);
        C2C3Reflect.Set(rig.Status, "vesselHUDController", rig.Hud);
        C2C3Reflect.Set(rig.Status, "orientationHandle", rig.OrientationHandle);
        C2C3Reflect.Set(rig.Status, "_name", "C4Vessel");
        C2C3Reflect.Set(rig.Status, "vesselType", VesselClassType.Sparrow);
        C2C3Reflect.Set(rig.Status, "_nearFieldSkimmer", rig.NearSkimmer);
        C2C3Reflect.Set(rig.Status, "_farFieldSkimmer", rig.FarSkimmer);

        go.SetActive(true);
        return rig;
    }

    static Skimmer NewChildSkimmer(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        var skimmer = go.AddComponent<Skimmer>();
        C2C3Reflect.Set(skimmer, "onSkimmerShipImpact", ScriptableObject.CreateInstance<ScriptableEventString>());
        return skimmer;
    }

    public NetworkVariable<T> NetVar<T>(string field) =>
        (NetworkVariable<T>)C2C3Reflect.Get(Controller, field);
}

// ══════════════════════════════════════════════════════════════════════════
// Conformance freeze
// ══════════════════════════════════════════════════════════════════════════

public class VesselControllerConformanceC4Tests
{
    [Fact]
    public void VesselController_IsTheConcreteVerbatimIVessel_OnNetworkBehaviour()
    {
        Assert.True(typeof(IVessel).IsAssignableFrom(typeof(VesselController)));
        // The kinematic replication lives HERE (not on the plain-MonoBehaviour
        // VesselStatus — see the C3 conformance freeze).
        Assert.True(typeof(NetworkBehaviour).IsAssignableFrom(typeof(VesselController)));
    }

    [Fact]
    public void RequireComponent_IVesselStatus_IsFrozen()
    {
        var declared = typeof(VesselController)
            .GetCustomAttributes(typeof(RequireComponentAttribute), false)
            .Cast<RequireComponentAttribute>()
            .Select(a => a.m_Type0)
            .ToArray();
        Assert.Equal(new[] { typeof(IVesselStatus) }, declared);
    }

    [Fact]
    public void KinematicNetworkVariables_AreOwnerWrite_EveryoneRead()
    {
        // The four replicated members and their permissions are wire contract.
        var fields = typeof(VesselController)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(f => f.FieldType.IsGenericType &&
                        f.FieldType.GetGenericTypeDefinition() == typeof(NetworkVariable<>))
            .ToDictionary(f => f.Name, f => f.FieldType.GetGenericArguments()[0]);

        Assert.Equal(4, fields.Count);
        Assert.Equal(typeof(float), fields["n_Speed"]);
        Assert.Equal(typeof(Vector3), fields["n_Course"]);
        Assert.Equal(typeof(Quaternion), fields["n_BlockRotation"]);
        Assert.Equal(typeof(bool), fields["n_IsTranslationRestricted"]);

        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        Assert.Equal(NetworkVariableWritePermission.Owner, rig.NetVar<float>("n_Speed").WritePerm);
        Assert.Equal(NetworkVariableWritePermission.Owner, rig.NetVar<Vector3>("n_Course").WritePerm);
        Assert.Equal(NetworkVariableWritePermission.Owner, rig.NetVar<Quaternion>("n_BlockRotation").WritePerm);
        Assert.Equal(NetworkVariableWritePermission.Owner, rig.NetVar<bool>("n_IsTranslationRestricted").WritePerm);
        Assert.Equal(NetworkVariableReadPermission.Everyone, rig.NetVar<float>("n_Speed").ReadPerm);
    }

    [Fact]
    public void VesselStatusAccessor_LazyResolvesTheSibling()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        Assert.Same(rig.Status, rig.Controller.VesselStatus);
        Assert.Same(rig.Controller.VesselStatus, rig.Controller.VesselStatus); // cached
        Assert.Same(rig.Go.transform, rig.Controller.Transform);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// Initialize(player) chain
// ══════════════════════════════════════════════════════════════════════════

public class VesselControllerInitializeC4Tests
{
    [Fact]
    public void Initialize_LocalUser_RunsTheFullDocumentedChain()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        var player = new C4Player { IsLocalUser = true, Domain = Domains.Jade };

        var initializedRaises = 0;
        rig.Controller.OnInitialized += () => initializedRaises++;
        var cameraTargets = new List<Transform>();
        rig.OnInitializePlayerCamera.OnRaised += t => cameraTargets.Add(t);

        rig.Controller.Initialize(player);

        // Player attach + camera target defaulting.
        Assert.Same(player, rig.Status.Player);
        Assert.Same(rig.Go.transform, rig.Status.CameraFollowTarget);

        // HUD: initialized with the status, hidden, and (local user) subscribed.
        Assert.Equal(1, rig.Hud.InitializeCalls);
        Assert.Same(rig.Status, rig.Hud.LastInitializedStatus);
        Assert.Equal(1, rig.Hud.HideCalls);
        Assert.Equal(1, rig.Hud.SubscribeCalls);

        // Local-user camera customizer raised with the follow target.
        Assert.Equal(new[] { rig.Go.transform }, cameraTargets);

        // Skimmers initialized against the status.
        Assert.Same(rig.Status, rig.NearSkimmer.VesselStatus);
        Assert.Same(rig.Status, rig.FarSkimmer.VesselStatus);

        // Transformer activated.
        Assert.True((bool)C2C3Reflect.Get(rig.Transformer, "isActive"));

        // ShipHelper painted the Jade theme set through the IVessel setters.
        Assert.Same(rig.JadeSet.ShipMaterial, rig.Status.ShipMaterial);
        Assert.Same(rig.JadeSet.AOEExplosionMaterial, rig.Status.AOEExplosionMaterial);
        Assert.Same(rig.JadeSet.AOEConicExplosionMaterial, rig.Status.AOEConicExplosionMaterial);
        Assert.Same(rig.JadeSet.SkimmerMaterial, rig.Status.SkimmerMaterial);
        Assert.Same(rig.JadeSet.BlockSilhouettePrefab, rig.Hud.LastBlockPrefab);

        // Customization adopted the geometry list; ResetForPlay ran last.
        Assert.Contains(rig.Geometry, rig.Status.ShipGeometries);
        Assert.True(rig.Status.IsStationary);
        Assert.Equal(1, rig.Animation.StopFlareEngineCalls);

        Assert.Equal(1, initializedRaises);
    }

    [Fact]
    public void Initialize_NonLocalUser_SkipsTheLocalOnlyBranch()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        var player = new C4Player { IsLocalUser = false };

        var cameraRaises = 0;
        rig.OnInitializePlayerCamera.OnRaised += _ => cameraRaises++;

        rig.Controller.Initialize(player);

        Assert.Equal(1, rig.Hud.InitializeCalls);
        Assert.Equal(1, rig.Hud.HideCalls);
        Assert.Equal(0, rig.Hud.SubscribeCalls); // local-only
        Assert.Equal(0, cameraRaises);           // local-only
        Assert.True((bool)C2C3Reflect.Get(rig.Transformer, "isActive"));
        Assert.True(rig.Status.IsStationary);
    }

    [Fact]
    public void Initialize_DoubleInitialization_IsRejected()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        var first = new C4Player { Name = "first" };
        var second = new C4Player { Name = "second" };

        var initializedRaises = 0;
        rig.Controller.OnInitialized += () => initializedRaises++;

        rig.Controller.Initialize(first);
        rig.Controller.Initialize(second); // logs "Double initialization not allowed!" and returns

        Assert.Same(first, rig.Status.Player);
        Assert.Equal(1, initializedRaises);
        Assert.Equal(1, rig.Hud.InitializeCalls);
    }

    [Fact]
    public void Initialize_PresetCameraFollowTarget_IsKept()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        var preset = new GameObject("preset-follow").transform;
        rig.Status.CameraFollowTarget = preset;

        rig.Controller.Initialize(new C4Player());

        Assert.Same(preset, rig.Status.CameraFollowTarget);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// IVessel surface routing
// ══════════════════════════════════════════════════════════════════════════

public class VesselControllerSurfaceC4Tests
{
    [Fact]
    public void MaterialAndBoostSetters_WriteThroughToStatus()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        IVessel vessel = rig.Controller;

        var ship = new Material((Shader)null) { name = "ship" };
        var aoe = new Material((Shader)null) { name = "aoe" };
        var conic = new Material((Shader)null) { name = "conic" };
        var skim = new Material((Shader)null) { name = "skim" };

        vessel.SetShipMaterial(ship);
        vessel.SetAOEExplosionMaterial(aoe);
        vessel.SetAOEConicExplosionMaterial(conic);
        vessel.SetSkimmerMaterial(skim);
        vessel.SetBoostMultiplier(7.5f);

        Assert.Same(ship, rig.Status.ShipMaterial);
        Assert.Same(aoe, rig.Status.AOEExplosionMaterial);
        Assert.Same(conic, rig.Status.AOEConicExplosionMaterial);
        Assert.Same(skim, rig.Status.SkimmerMaterial);
        Assert.Equal(7.5f, rig.Status.BoostMultiplier);
    }

    [Fact]
    public void SetBlockSilhouettePrefab_RoutesToHudController()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        var prefab = new GameObject("silhouette");

        rig.Controller.SetBlockSilhouettePrefab(prefab);

        Assert.Same(prefab, rig.Hud.LastBlockPrefab);
    }

    [Fact]
    public void SetShipUp_RotatesTheOrientationHandle()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();

        rig.Controller.SetShipUp(90f);

        Assert.True(rig.OrientationHandle.transform.localRotation == Quaternion.Euler(0f, 0f, 90f));
    }

    [Fact]
    public void SetResourceLevels_SeedsTheResourceSystemElementLevels()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();

        rig.Controller.SetResourceLevels(new ResourceCollection(mass: .2f, charge: .4f, space: .6f, time: .8f));

        Assert.Equal(.2f, rig.Resources.GetNormalizedLevel(Element.Mass), 5);
        Assert.Equal(.4f, rig.Resources.GetNormalizedLevel(Element.Charge), 5);
        Assert.Equal(.6f, rig.Resources.GetNormalizedLevel(Element.Space), 5);
        Assert.Equal(.8f, rig.Resources.GetNormalizedLevel(Element.Time), 5);
    }

    [Fact]
    public void BindElementalFloat_RoutesIntoTheElementalStatsHandler()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();

        rig.Controller.BindElementalFloat("Scale", Element.Space);
        rig.Controller.BindElementalFloat("Scale", Element.Space); // duplicate name → no-op

        var stats = (List<R_ShipElementStatsHandler.ElementStat>)C2C3Reflect.Get(rig.ElementStats, "ElementStats");
        Assert.Single(stats);
        Assert.Equal("Scale", stats[0].StatName);
        Assert.Equal(Element.Space, stats[0].Element);
    }

    [Fact]
    public void Teleport_SnapsToTheTargetTransform()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        var target = new GameObject("teleport-target").transform;
        target.position = new Vector3(10f, 20f, 30f);
        target.rotation = Quaternion.Euler(0f, 45f, 0f);

        rig.Controller.Teleport(target);

        Assert.Equal(target.position, rig.Go.transform.position);
        Assert.True(rig.Go.transform.rotation == target.rotation);
    }

    [Fact]
    public void SetPose_LocalAndSpawned_BothLandOnTheTransformer()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        var poseA = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 30f, 0f));

        rig.Controller.SetPose(poseA); // not spawned → SetPose_Local
        Assert.Equal(poseA.position, rig.Go.transform.position);
        Assert.True(rig.Go.transform.rotation == poseA.rotation);

        rig.Controller.Spawn(); // host-mode owner → SetPose_ClientRpc (direct invoke)
        var poseB = new Pose(new Vector3(-4f, 0f, 9f), Quaternion.Euler(0f, 0f, 60f));
        rig.Controller.SetPose(poseB);
        Assert.Equal(poseB.position, rig.Go.transform.position);
        Assert.True(rig.Go.transform.rotation == poseB.rotation);
    }

    [Fact]
    public void ToggleAIPilot_StartsAndStopsAutopilot()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Initialize(new C4Player());

        rig.Controller.ToggleAIPilot(true);
        Assert.True(rig.Pilot.AutoPilotEnabled);
        Assert.True(((IVesselStatus)rig.Status).AutoPilotEnabled);

        rig.Controller.ToggleAIPilot(false);
        Assert.False(rig.Pilot.AutoPilotEnabled);
    }

    [Fact]
    public void DisableSkimmer_DeactivatesBothSkimmerGameObjects()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        Assert.True(rig.NearSkimmer.gameObject.activeSelf);
        Assert.True(rig.FarSkimmer.gameObject.activeSelf);

        rig.Controller.DisableSkimmer();

        Assert.False(rig.NearSkimmer.gameObject.activeSelf);
        Assert.False(rig.FarSkimmer.gameObject.activeSelf);
    }

    [Fact]
    public void SetTrailColors_RetintsTheChildTrail_PreservingAlphaKeys()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        var originalAlphas = rig.TrailRenderer.colorGradient.alphaKeys;

        rig.Controller.SetTrailColors(Color.red, Color.blue);

        var gradient = rig.TrailRenderer.colorGradient;
        Assert.Equal(Color.red, gradient.colorKeys[0].color);
        Assert.Equal(Color.blue, gradient.colorKeys[1].color);
        Assert.Equal(originalAlphas.Length, gradient.alphaKeys.Length);
        Assert.Equal(originalAlphas[0].alpha, gradient.alphaKeys[0].alpha, 5);
    }

    [Fact]
    public void StartVessel_UnstationsAndEnablesPrismSpawning()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Initialize(new C4Player()); // ResetForPlay leaves IsStationary=true, spawner stopped
        Assert.True(rig.Status.IsStationary);

        rig.Controller.StartVessel();

        Assert.False(rig.Status.IsStationary);
        Assert.True((bool)C2C3Reflect.Get(rig.PrismController, "spawnerEnabled"));

        // StartSpawn launched SpawnLoopAsync(...).Forget() — an async void that only
        // completes via cancellation. Invoked from a test body it registers on xunit's
        // AsyncTestSyncContext, which waits for it; cancel the loop or the test hangs.
        rig.PrismController.StopSpawn();
    }

    [Fact]
    public void ResetForPlay_OwnerZeroesKinematics_LocalOnlyCascades()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Initialize(new C4Player());

        // Not spawned: kinematics are NOT touched by the controller (only the
        // status cascade runs).
        rig.Status.Speed = 50f;
        rig.Status.IsStationary = false;
        rig.Controller.ResetForPlay();
        Assert.Equal(50f, rig.Status.Speed);
        Assert.True(rig.Status.IsStationary);

        // Spawned owner: Speed/Course/blockRotation reset before the cascade.
        rig.Controller.Spawn();
        rig.Status.Speed = 50f;
        rig.Status.Course = new Vector3(1f, 0f, 0f);
        rig.Status.blockRotation = Quaternion.Euler(0f, 90f, 0f);
        rig.Controller.ResetForPlay();
        Assert.Equal(0f, rig.Status.Speed);
        Assert.Equal(rig.Go.transform.forward, rig.Status.Course);
        Assert.True(rig.Status.blockRotation == Quaternion.identity);
    }

    [Fact]
    public void AllowClearPrismInitialization_OwnerOrAIMatrix()
    {
        using var loop = new GameLoop();

        var human = C4VesselRig.Build("human");
        human.Controller.Initialize(new C4Player { IsInitializedAsAI = false });
        Assert.False(human.Controller.AllowClearPrismInitialization()); // not spawned, not AI

        var ai = C4VesselRig.Build("ai");
        ai.Controller.Initialize(new C4Player { IsInitializedAsAI = true, IsLocalUser = false });
        Assert.True(ai.Controller.AllowClearPrismInitialization()); // AI

        human.Controller.Spawn(); // spawned owner
        Assert.True(human.Controller.AllowClearPrismInitialization());

        var client = C4VesselRig.Build("client");
        client.Controller.Initialize(new C4Player { IsLocalUser = false });
        client.Controller.Spawn(isServer: false, isClient: true, isOwner: false, ownerClientId: 2);
        Assert.False(client.Controller.AllowClearPrismInitialization()); // spawned non-owner, not AI
    }
}

// ══════════════════════════════════════════════════════════════════════════
// Kinematic replication: IsSpawned switching + owner gating
// (InputStatus precedent — see InputStatusTests)
// ══════════════════════════════════════════════════════════════════════════

public class VesselControllerKinematicsC4Tests
{
    [Fact]
    public void Update_NotSpawned_WritesNothing()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Status.Speed = 33f;

        loop.Tick(1f / 60f);

        Assert.Equal(0f, rig.NetVar<float>("n_Speed").Value);
    }

    [Fact]
    public void Update_SpawnedOwner_ReplicatesSpeedCourseBlockRotation()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Spawn(); // host-mode owner

        rig.Status.Speed = 12.5f;
        rig.Status.Course = new Vector3(0f, 1f, 0f);
        rig.Status.blockRotation = Quaternion.Euler(0f, 90f, 0f);
        loop.Tick(1f / 60f);

        Assert.Equal(12.5f, rig.NetVar<float>("n_Speed").Value);
        Assert.Equal(new Vector3(0f, 1f, 0f), rig.NetVar<Vector3>("n_Course").Value);
        Assert.True(rig.NetVar<Quaternion>("n_BlockRotation").Value == rig.Status.blockRotation);
    }

    [Fact]
    public void Update_SpawnedNonOwner_DoesNotWrite()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Spawn(isServer: true, isClient: true, isOwner: false, ownerClientId: 2);

        rig.Status.Speed = 33f;
        loop.Tick(1f / 60f);

        Assert.Equal(0f, rig.NetVar<float>("n_Speed").Value);
    }

    [Fact]
    public void OnNetworkSpawn_NonOwner_AppliesReplicatedValuesToStatus()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Spawn(isServer: true, isClient: true, isOwner: false, ownerClientId: 2);

        // Simulate replication arriving from the owner.
        rig.NetVar<float>("n_Speed").Value = 21f;
        rig.NetVar<Vector3>("n_Course").Value = new Vector3(0f, 0f, -1f);
        rig.NetVar<Quaternion>("n_BlockRotation").Value = Quaternion.Euler(45f, 0f, 0f);
        rig.NetVar<bool>("n_IsTranslationRestricted").Value = true;

        Assert.Equal(21f, rig.Status.Speed);
        Assert.Equal(new Vector3(0f, 0f, -1f), rig.Status.Course);
        Assert.True(rig.Status.blockRotation == Quaternion.Euler(45f, 0f, 0f));
        Assert.True(rig.Status.IsTranslationRestricted);
    }

    [Fact]
    public void OnNetworkSpawn_Owner_DoesNotSubscribe()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Spawn(); // owner → early return before SubscribeToNetworkVariables

        rig.NetVar<float>("n_Speed").Value = 21f;

        Assert.Equal(0f, rig.Status.Speed); // no callback applied it
    }

    [Fact]
    public void OnNetworkDespawn_NonOwner_StopsApplyingReplication()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Spawn(isServer: true, isClient: true, isOwner: false, ownerClientId: 2);
        rig.NetVar<float>("n_Speed").Value = 5f;
        Assert.Equal(5f, rig.Status.Speed);

        rig.Controller.Despawn();

        rig.NetVar<float>("n_Speed").Value = 99f;
        Assert.Equal(5f, rig.Status.Speed); // unsubscribed
    }

    [Fact]
    public void SetTranslationRestricted_IsOwnerGated()
    {
        using var loop = new GameLoop();

        // Not spawned: local mirror only.
        var local = C4VesselRig.Build("local");
        local.Controller.SetTranslationRestricted(true);
        Assert.True(local.Status.IsTranslationRestricted);
        Assert.False(local.NetVar<bool>("n_IsTranslationRestricted").Value);

        // Spawned owner: NetworkVariable + local mirror.
        var owner = C4VesselRig.Build("owner");
        owner.Controller.Spawn();
        owner.Controller.SetTranslationRestricted(true);
        Assert.True(owner.Status.IsTranslationRestricted);
        Assert.True(owner.NetVar<bool>("n_IsTranslationRestricted").Value);

        // Spawned non-owner: local mirror only (write perm is Owner).
        var client = C4VesselRig.Build("client");
        client.Controller.Spawn(isServer: false, isClient: true, isOwner: false, ownerClientId: 2);
        client.Controller.SetTranslationRestricted(true);
        Assert.True(client.Status.IsTranslationRestricted);
        Assert.False(client.NetVar<bool>("n_IsTranslationRestricted").Value);
    }

    [Fact]
    public void NetIds_TrackTheC1NetworkObjectAllocation()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        Assert.Equal(0ul, rig.Controller.VesselNetId); // never spawned sentinel

        rig.Controller.Spawn(isServer: true, isClient: true, isOwner: false, ownerClientId: 7);

        Assert.NotEqual(0ul, rig.Controller.VesselNetId);
        Assert.Equal(rig.Controller.NetworkObjectId, rig.Controller.VesselNetId);
        Assert.Equal(rig.Controller.NetworkObject.NetworkObjectId, rig.Controller.VesselNetId);
        Assert.Equal(7ul, rig.Controller.OwnerClientNetId);
        Assert.Equal(0ul, rig.Controller.PlayerNetId); // never assigned by VesselController
    }
}

// ══════════════════════════════════════════════════════════════════════════
// Spawn registration, GameData routing, despawn/destroy semantics
// ══════════════════════════════════════════════════════════════════════════

public class VesselControllerLifecycleC4Tests
{
    [Fact]
    public void OnNetworkSpawn_RegistersInGameData_AndRaisesVesselNetworkSpawned()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        var spawnedRaises = 0;
        rig.OnVesselNetworkSpawned.OnRaised += () => spawnedRaises++;

        rig.Controller.Spawn();

        Assert.Contains(rig.Controller, rig.GameData.Vessels);
        Assert.Equal(1, spawnedRaises);
    }

    [Fact]
    public void SlowedShipTransforms_LocalPath_AddsAndRemoves()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();

        rig.Controller.AddSlowedShipTransformToGameData();
        Assert.Contains(rig.Go.transform, rig.GameData.SlowedShipTransforms);

        rig.Controller.RemoveSlowedShipTransformFromGameData();
        Assert.Empty(rig.GameData.SlowedShipTransforms);
    }

    [Fact]
    public void SlowedShipTransforms_SpawnedPath_RoutesThroughTheRpcPair()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Spawn(); // ServerRpc → ClientRpc → local (direct invoke)

        rig.Controller.AddSlowedShipTransformToGameData();
        Assert.Contains(rig.Go.transform, rig.GameData.SlowedShipTransforms);

        rig.Controller.RemoveSlowedShipTransformFromGameData();
        Assert.Empty(rig.GameData.SlowedShipTransforms);
    }

    [Fact]
    public void DestroyVessel_NotSpawned_DestroysTheGameObject_FiringOnBeforeDestroyed()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        var beforeDestroyed = 0;
        rig.Controller.OnBeforeDestroyed += () => beforeDestroyed++;

        rig.Controller.DestroyVessel();
        loop.Tick(1f / 60f); // deferred Destroy processes end-of-frame

        Assert.True(rig.Go.IsDestroyed);
        Assert.Equal(1, beforeDestroyed);
    }

    [Fact]
    public void DestroyVessel_SpawnedServer_DespawnsThenDestroys()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Spawn(); // host: server + owner
        var beforeDestroyed = 0;
        rig.Controller.OnBeforeDestroyed += () => beforeDestroyed++;

        rig.Controller.DestroyVessel(); // NetworkObject.Despawn(true)

        Assert.False(rig.Controller.IsSpawned); // despawned immediately
        loop.Tick(1f / 60f);
        Assert.True(rig.Go.IsDestroyed);
        Assert.Equal(1, beforeDestroyed);
    }

    [Fact]
    public void DestroyVessel_SpawnedClient_IsServerAuthoritative_NoOp()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Spawn(isServer: false, isClient: true, isOwner: false, ownerClientId: 2);

        rig.Controller.DestroyVessel(); // spawned but not server → return

        Assert.True(rig.Controller.IsSpawned);
        loop.Tick(1f / 60f);
        Assert.False(rig.Go.IsDestroyed);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// ChangePlayer AI / network-client / local-human matrix
// ══════════════════════════════════════════════════════════════════════════

public class VesselControllerChangePlayerC4Tests : IDisposable
{
    public void Dispose()
    {
        // ChangePlayer's human leg touches the CameraManager singleton; reset the
        // static so later test classes see a clean slate.
        typeof(Singleton<CameraManager>).GetProperty("Instance")!.SetValue(null, null);
    }

    [Fact]
    public void ChangePlayer_AI_HidesHudAndKeepsTransformerActive()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Initialize(new C4Player());

        var ai = new C4Player { IsInitializedAsAI = true, IsLocalUser = false };
        rig.Controller.ChangePlayer(ai);

        Assert.Same(ai, rig.Status.Player);
        Assert.Equal(1, rig.Hud.UnsubscribeCalls);
        Assert.Equal(2, rig.Hud.HideCalls); // 1 from Initialize, 1 from ChangePlayer
        Assert.True((bool)C2C3Reflect.Get(rig.Transformer, "isActive"));

        // AI branch did not subscribe to the NetworkVariables.
        rig.NetVar<float>("n_Speed").Value = 11f;
        Assert.Equal(0f, rig.Status.Speed);
    }

    [Fact]
    public void ChangePlayer_NetworkClient_DisablesTransformer_AndSubscribes()
    {
        using var loop = new GameLoop();
        var rig = C4VesselRig.Build();
        rig.Controller.Initialize(new C4Player());

        var client = new C4Player { IsNetworkClient = true, IsLocalUser = false };
        rig.Controller.ChangePlayer(client);

        Assert.Same(client, rig.Status.Player);
        Assert.False((bool)C2C3Reflect.Get(rig.Transformer, "isActive"));
        Assert.Equal(1, rig.Hud.UnsubscribeCalls);

        // Subscribed: replicated kinematics now land on the status.
        rig.NetVar<float>("n_Speed").Value = 14f;
        Assert.Equal(14f, rig.Status.Speed);
    }

    [Fact]
    public void ChangePlayer_LocalHuman_RestoresHudControlAndCamera_AndUnsubscribes()
    {
        using var loop = new GameLoop();
        new GameObject("cameraManager").AddComponent<CameraManager>(); // RetargetAndApply reads Instance

        var rig = C4VesselRig.Build();
        rig.Controller.Initialize(new C4Player());

        var cameraTargets = new List<Transform>();
        rig.OnInitializePlayerCamera.OnRaised += t => cameraTargets.Add(t);

        // Hand to a network client first (subscribes), then back to a local human.
        rig.Controller.ChangePlayer(new C4Player { IsNetworkClient = true, IsLocalUser = false });
        rig.NetVar<float>("n_Speed").Value = 14f;
        Assert.Equal(14f, rig.Status.Speed);

        var human = new C4Player();
        rig.Controller.ChangePlayer(human);

        Assert.Same(human, rig.Status.Player);
        Assert.Equal(2, rig.Hud.SubscribeCalls); // 1 from Initialize's local branch, 1 from ChangePlayer
        Assert.Equal(1, rig.Hud.ShowCalls);
        Assert.True((bool)C2C3Reflect.Get(rig.Transformer, "isActive"));
        Assert.Equal(new[] { rig.Go.transform }, cameraTargets); // RetargetAndApply re-raised with the follow target

        // Unsubscribed: replication no longer lands.
        rig.NetVar<float>("n_Speed").Value = 77f;
        Assert.Equal(14f, rig.Status.Speed);
    }
}
