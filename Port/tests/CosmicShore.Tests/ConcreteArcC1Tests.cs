using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Collections;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Services;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C1 — concrete-arc iteration 1 (VESSEL_CONCRETE.md): engine additions E11–E16
// plus the leaf game files (VesselPrefabContainer, SO_ProfileIconList,
// PlayerProfileData, VesselTrailCustomization).
// ─────────────────────────────────────────────────────────────────────────────

// ══════════════════════════════════════════════════════════════════════════
// E16 — clone intra-hierarchy reference remap (the arc's riskiest gap).
// The survey's corruption scenario: a cloned prefab-style vessel rig whose
// serialized fields (VesselStatus._shipInstance, _nearFieldSkimmer,
// vesselHUDController, orientationHandle) alias the TEMPLATE's components
// instead of the clone's own.
// ══════════════════════════════════════════════════════════════════════════
public class CloneReferenceRemapTests
{
    // Minimal stand-ins for the prefab rig's component shapes.
    interface IRigHud { }

    class RigController : MonoBehaviour
    {
        public RigStatus status;                      // back-reference across components
    }

    class RigSkimmer : MonoBehaviour, IRigHud { }

    class RigStatus : MonoBehaviour
    {
        public RigController shipInstance;            // same-GameObject component (VesselStatus._shipInstance shape)
        public RigSkimmer nearFieldSkimmer;            // child component (_nearFieldSkimmer shape)
        public IRigHud vesselHUDController;            // interface-typed in-tree component
        public GameObject orientationHandle;           // child GameObject reference
        public Transform cameraTarget;                 // grandchild Transform reference
        public RigSkimmer deepSkimmer;                 // grandchild component reference
        public List<RigSkimmer> skimmerList;           // List<> of in-tree components (+ external + null)
        public RigSkimmer[] skimmerArray;              // array of in-tree components
        public RigSkimmer externalSkimmer;             // OUTSIDE the cloned hierarchy — must stay
        public ScriptableObject configAsset;           // asset reference — must stay
        public RigController unset;                    // stays null
        public int intensity = 7;                      // value field survives

        [SerializeField] RigSkimmer _privateSkimmer;   // private serialized field remaps too
        public RigSkimmer PrivateSkimmer => _privateSkimmer;
        public void SetPrivateSkimmer(RigSkimmer s) => _privateSkimmer = s;
    }

    sealed class Rig
    {
        public GameObject Root;
        public RigStatus Status;
        public RigController Controller;
        public RigSkimmer ChildSkimmer;
        public RigSkimmer DeepSkimmer;
        public GameObject OrientationHandle;
        public RigSkimmer ExternalSkimmer;
        public ScriptableObject ConfigAsset;
    }

    static Rig BuildTemplate()
    {
        var rig = new Rig();

        rig.Root = new GameObject("VesselRig");
        rig.Status = rig.Root.AddComponent<RigStatus>();
        rig.Controller = rig.Root.AddComponent<RigController>();

        var skimmerGO = new GameObject("NearFieldSkimmer");
        skimmerGO.transform.SetParent(rig.Root.transform, worldPositionStays: false);
        rig.ChildSkimmer = skimmerGO.AddComponent<RigSkimmer>();

        rig.OrientationHandle = new GameObject("OrientationHandle");
        rig.OrientationHandle.transform.SetParent(rig.Root.transform, worldPositionStays: false);

        var deepGO = new GameObject("DeepSkimmer");
        deepGO.transform.SetParent(rig.OrientationHandle.transform, worldPositionStays: false);
        rig.DeepSkimmer = deepGO.AddComponent<RigSkimmer>();

        var externalGO = new GameObject("External");
        rig.ExternalSkimmer = externalGO.AddComponent<RigSkimmer>();

        rig.ConfigAsset = ScriptableObject.CreateInstance<SO_ProfileIconList>();

        rig.Status.shipInstance = rig.Controller;
        rig.Status.nearFieldSkimmer = rig.ChildSkimmer;
        rig.Status.vesselHUDController = rig.ChildSkimmer;
        rig.Status.orientationHandle = rig.OrientationHandle;
        rig.Status.cameraTarget = rig.DeepSkimmer.transform;
        rig.Status.deepSkimmer = rig.DeepSkimmer;
        rig.Status.skimmerList = new List<RigSkimmer> { rig.ChildSkimmer, rig.ExternalSkimmer, null, rig.DeepSkimmer };
        rig.Status.skimmerArray = new[] { rig.ChildSkimmer, rig.DeepSkimmer };
        rig.Status.externalSkimmer = rig.ExternalSkimmer;
        rig.Status.configAsset = rig.ConfigAsset;
        rig.Status.SetPrivateSkimmer(rig.ChildSkimmer);
        rig.Controller.status = rig.Status;

        return rig;
    }

    [Fact]
    public void Clone_PrefabRig_DoesNotAliasTemplateComponents_SurveyCorruptionScenario()
    {
        using var loop = new GameLoop();
        var template = BuildTemplate();

        var clone = Object.Instantiate(template.Root);
        var cloneStatus = clone.GetComponent<RigStatus>();
        var cloneController = clone.GetComponent<RigController>();
        var cloneSkimmer = clone.GetComponentInChildren<RigSkimmer>(includeInactive: true);

        // Every survey field points at the CLONE's own parts…
        Assert.Same(cloneController, cloneStatus.shipInstance);
        Assert.Same(cloneSkimmer, cloneStatus.nearFieldSkimmer);
        Assert.Same(cloneSkimmer, cloneStatus.vesselHUDController);
        Assert.True(cloneStatus.orientationHandle.transform.IsChildOf(clone.transform));

        // …and at none of the template's.
        Assert.NotSame(template.Controller, cloneStatus.shipInstance);
        Assert.NotSame(template.ChildSkimmer, cloneStatus.nearFieldSkimmer);
        Assert.NotSame(template.ChildSkimmer, cloneStatus.vesselHUDController);
        Assert.NotSame(template.OrientationHandle, cloneStatus.orientationHandle);

        // The template itself is untouched.
        Assert.Same(template.Controller, template.Status.shipInstance);
        Assert.Same(template.ChildSkimmer, template.Status.nearFieldSkimmer);
    }

    [Fact]
    public void Clone_RemapsBackReference_AndCrossComponentPairsAgree()
    {
        using var loop = new GameLoop();
        var template = BuildTemplate();

        var clone = Object.Instantiate(template.Root);
        var cloneStatus = clone.GetComponent<RigStatus>();
        var cloneController = clone.GetComponent<RigController>();

        Assert.Same(cloneStatus, cloneController.status);
        Assert.Same(cloneController, cloneStatus.shipInstance);
    }

    [Fact]
    public void Clone_RemapsNestedChild_GameObject_Transform_AndComponentReferences()
    {
        using var loop = new GameLoop();
        var template = BuildTemplate();

        var clone = Object.Instantiate(template.Root);
        var cloneStatus = clone.GetComponent<RigStatus>();

        // Grandchild component + its transform remap into the clone's own subtree.
        Assert.NotSame(template.DeepSkimmer, cloneStatus.deepSkimmer);
        Assert.True(cloneStatus.deepSkimmer.transform.IsChildOf(clone.transform));
        Assert.Same(cloneStatus.deepSkimmer.transform, cloneStatus.cameraTarget);
        Assert.Same(cloneStatus.deepSkimmer, cloneStatus.cameraTarget.GetComponent<RigSkimmer>());

        // Child GameObject reference remaps and keeps its authored name (root-only "(Clone)").
        Assert.Equal("OrientationHandle", cloneStatus.orientationHandle.name);
    }

    [Fact]
    public void Clone_RemapsListElements_WithoutMutatingTemplateList()
    {
        using var loop = new GameLoop();
        var template = BuildTemplate();
        var templateList = template.Status.skimmerList;

        var clone = Object.Instantiate(template.Root);
        var cloneStatus = clone.GetComponent<RigStatus>();
        var cloneSkimmer = clone.GetComponentInChildren<RigSkimmer>(includeInactive: true);

        // Clone got its OWN list instance (field copy must not leave a shared container).
        Assert.NotSame(templateList, cloneStatus.skimmerList);
        Assert.Equal(4, cloneStatus.skimmerList.Count);

        // In-tree elements remapped; external + null elements preserved verbatim.
        Assert.Same(cloneSkimmer, cloneStatus.skimmerList[0]);
        Assert.Same(template.ExternalSkimmer, cloneStatus.skimmerList[1]);
        Assert.Null(cloneStatus.skimmerList[2]);
        Assert.Same(cloneStatus.deepSkimmer, cloneStatus.skimmerList[3]);

        // Template list untouched — still the template's own components.
        Assert.Same(templateList, template.Status.skimmerList);
        Assert.Same(template.ChildSkimmer, templateList[0]);
        Assert.Same(template.ExternalSkimmer, templateList[1]);
        Assert.Null(templateList[2]);
        Assert.Same(template.DeepSkimmer, templateList[3]);
    }

    [Fact]
    public void Clone_RemapsArrayElements_WithoutMutatingTemplateArray()
    {
        using var loop = new GameLoop();
        var template = BuildTemplate();
        var templateArray = template.Status.skimmerArray;

        var clone = Object.Instantiate(template.Root);
        var cloneStatus = clone.GetComponent<RigStatus>();

        Assert.NotSame(templateArray, cloneStatus.skimmerArray);
        Assert.Equal(2, cloneStatus.skimmerArray.Length);
        Assert.Same(cloneStatus.nearFieldSkimmer, cloneStatus.skimmerArray[0]);
        Assert.Same(cloneStatus.deepSkimmer, cloneStatus.skimmerArray[1]);

        Assert.Same(template.ChildSkimmer, templateArray[0]);
        Assert.Same(template.DeepSkimmer, templateArray[1]);
    }

    [Fact]
    public void Clone_LeavesReferencesOutsideTheHierarchyUntouched()
    {
        using var loop = new GameLoop();
        var template = BuildTemplate();

        var clone = Object.Instantiate(template.Root);
        var cloneStatus = clone.GetComponent<RigStatus>();

        Assert.Same(template.ExternalSkimmer, cloneStatus.externalSkimmer);   // scene object outside tree
        Assert.Same(template.ConfigAsset, cloneStatus.configAsset);           // asset reference
    }

    [Fact]
    public void Clone_RemapsPrivateSerializedFields_AndPreservesNullAndValueFields()
    {
        using var loop = new GameLoop();
        var template = BuildTemplate();

        var clone = Object.Instantiate(template.Root);
        var cloneStatus = clone.GetComponent<RigStatus>();
        var cloneSkimmer = clone.GetComponentInChildren<RigSkimmer>(includeInactive: true);

        Assert.Same(cloneSkimmer, cloneStatus.PrivateSkimmer);
        Assert.Null(cloneStatus.unset);
        Assert.Equal(7, cloneStatus.intensity);
    }

    [Fact]
    public void Clone_RootGetsCloneSuffix_ChildrenKeepAuthoredNames()
    {
        using var loop = new GameLoop();
        var template = BuildTemplate();

        var clone = Object.Instantiate(template.Root);

        Assert.Equal("VesselRig(Clone)", clone.name);
        Assert.Equal("NearFieldSkimmer", clone.transform.GetChild(0).name);
        Assert.Equal("OrientationHandle", clone.transform.GetChild(1).name);
    }

    [Fact]
    public void Clone_ViaComponentOverload_TheVesselSpawnerShape_AlsoRemaps()
    {
        using var loop = new GameLoop();
        var template = BuildTemplate();

        // VesselSpawner clones the prefab's Transform: Instantiate(shipPrefab).
        Transform spawned = Object.Instantiate(template.Root.transform);

        Assert.NotSame(template.Root.transform, spawned);
        var cloneStatus = spawned.gameObject.GetComponent<RigStatus>();
        Assert.Same(spawned.gameObject.GetComponent<RigController>(), cloneStatus.shipInstance);
        Assert.True(cloneStatus.nearFieldSkimmer.transform.IsChildOf(spawned));
    }
}

// ══════════════════════════════════════════════════════════════════════════
// E11 — FixedString128Bytes
// ══════════════════════════════════════════════════════════════════════════
public class FixedString128BytesTests
{
    [Fact]
    public void Capacity_Is125Utf8Bytes()
    {
        Assert.Equal(125, FixedString128Bytes.Capacity);

        var exact = new FixedString128Bytes(new string('a', 125));
        Assert.Equal(125, exact.Length);

        var over = new FixedString128Bytes(new string('a', 130));
        Assert.Equal(125, over.Length);
    }

    [Fact]
    public void Truncation_RespectsCodePointBoundaries()
    {
        // 124 ASCII bytes + '€' (3 UTF-8 bytes) would land at 127 — the rune must be
        // dropped whole, never split.
        var s = new FixedString128Bytes(new string('a', 124) + "€");
        Assert.Equal(new string('a', 124), s.Value);

        // 41 × '€' = 123 bytes; a 2-byte 'é' fits exactly at 125.
        var euros = string.Concat(System.Linq.Enumerable.Repeat("€", 41));
        var fits = new FixedString128Bytes(euros + "é");
        Assert.Equal(euros + "é", fits.Value);
    }

    [Fact]
    public void ImplicitConversions_Equality_AndEmptySemantics_MatchTheFixedString64Contract()
    {
        FixedString128Bytes fromString = "PilotName";
        string roundTrip = fromString;
        Assert.Equal("PilotName", roundTrip);

        Assert.True(new FixedString128Bytes("a") == new FixedString128Bytes("a"));
        Assert.True(new FixedString128Bytes("a") != new FixedString128Bytes("b"));

        var empty = default(FixedString128Bytes);
        Assert.True(empty.IsEmpty);
        Assert.Equal(string.Empty, empty.Value);
        Assert.True(new FixedString128Bytes(null).IsEmpty);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// E12 — NetworkObjectId allocation + NetworkObject.Despawn(bool)
// ══════════════════════════════════════════════════════════════════════════
public class NetworkObjectIdTests
{
    class TestNetBehaviour : NetworkBehaviour
    {
        public int DespawnCalls;
        public override void OnNetworkDespawn() => DespawnCalls++;
    }

    [Fact]
    public void NetworkObjectId_IsZeroBeforeSpawn_AndAllocatedMonotonicallyAtSpawn()
    {
        using var loop = new GameLoop();
        var a = new GameObject("A").AddComponent<TestNetBehaviour>();
        var b = new GameObject("B").AddComponent<TestNetBehaviour>();

        Assert.Equal(0UL, a.NetworkObjectId);
        Assert.Equal(0UL, b.NetworkObjectId);

        a.Spawn();
        b.Spawn();

        Assert.NotEqual(0UL, a.NetworkObjectId);
        Assert.NotEqual(0UL, b.NetworkObjectId);
        Assert.True(b.NetworkObjectId > a.NetworkObjectId, "ids allocate monotonically");

        ulong firstId = a.NetworkObjectId;
        a.Despawn();
        Assert.Equal(firstId, a.NetworkObjectId);   // id retained after despawn
        a.Spawn();
        Assert.True(a.NetworkObjectId > b.NetworkObjectId, "respawn allocates a fresh id");
    }

    [Fact]
    public void NetworkObject_Handle_ExposesId_AndIsStable()
    {
        using var loop = new GameLoop();
        var behaviour = new GameObject("Net").AddComponent<TestNetBehaviour>();
        behaviour.Spawn();

        Assert.Same(behaviour.NetworkObject, behaviour.NetworkObject);
        Assert.Equal(behaviour.NetworkObjectId, behaviour.NetworkObject.NetworkObjectId);
        Assert.True(behaviour.NetworkObject.IsSpawned);
    }

    [Fact]
    public void Despawn_WithDestroyTrue_DespawnsAndDestroysTheGameObject()
    {
        using var loop = new GameLoop();
        var go = new GameObject("Player");
        var behaviour = go.AddComponent<TestNetBehaviour>();
        behaviour.Spawn();

        behaviour.NetworkObject.Despawn(true);   // the Player.DestroyPlayer shape

        Assert.False(behaviour.IsSpawned);
        Assert.Equal(1, behaviour.DespawnCalls);
        Assert.False(go == null);                // destroy is deferred to end of frame
        loop.Tick(0.016f);
        Assert.True(go == null);                 // fake-null after the destroy frame
    }

    [Fact]
    public void Despawn_WithDestroyFalse_LeavesTheGameObjectAlive()
    {
        using var loop = new GameLoop();
        var go = new GameObject("Player");
        var behaviour = go.AddComponent<TestNetBehaviour>();
        behaviour.Spawn();

        behaviour.NetworkObject.Despawn(false);
        loop.Tick(0.016f);

        Assert.False(behaviour.IsSpawned);
        Assert.Equal(1, behaviour.DespawnCalls);
        Assert.True(go != null);
    }

    [Fact]
    public void Clone_OfASpawnedBehaviour_StartsUnspawned_WithItsOwnHandle()
    {
        using var loop = new GameLoop();
        var go = new GameObject("Template");
        var behaviour = go.AddComponent<TestNetBehaviour>();
        behaviour.Spawn();

        var clone = Object.Instantiate(go);
        var cloneBehaviour = clone.GetComponent<TestNetBehaviour>();

        // E16 stop-set: engine network state is never field-copied — a fresh
        // instance starts unspawned and its NetworkObject wraps ITSELF, not the template.
        Assert.False(cloneBehaviour.IsSpawned);
        Assert.Equal(0UL, cloneBehaviour.NetworkObjectId);
        Assert.NotSame(behaviour.NetworkObject, cloneBehaviour.NetworkObject);

        cloneBehaviour.NetworkObject.Despawn(true);
        loop.Tick(0.016f);
        Assert.True(behaviour.IsSpawned);        // template untouched by the clone's handle
        Assert.True(go != null);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// E13 — UGS AuthenticationService shim
// ══════════════════════════════════════════════════════════════════════════
public class AuthenticationServiceShimTests
{
    [Fact]
    public void Defaults_AreBenign_AndHarnessConfigurable()
    {
        AuthenticationService.Reset();

        Assert.NotNull(AuthenticationService.Instance);
        Assert.False(AuthenticationService.Instance.IsSignedIn);
        Assert.Equal(string.Empty, AuthenticationService.Instance.PlayerName);
        Assert.Equal(string.Empty, AuthenticationService.Instance.PlayerId);

        AuthenticationService.Instance.PlayerName = "Pilot#1234";
        AuthenticationService.Instance.PlayerId = "ugs-id";
        AuthenticationService.Instance.IsSignedIn = true;

        Assert.Equal("Pilot#1234", AuthenticationService.Instance.PlayerName);
        Assert.Equal("ugs-id", AuthenticationService.Instance.PlayerId);
        Assert.True(AuthenticationService.Instance.IsSignedIn);

        AuthenticationService.Reset();
        Assert.False(AuthenticationService.Instance.IsSignedIn);
        Assert.Equal(string.Empty, AuthenticationService.Instance.PlayerName);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// E14 — Gradient evaluation + TrailRenderer.colorGradient
// ══════════════════════════════════════════════════════════════════════════
public class GradientTests
{
    static void AssertColor(Color expected, Color actual)
    {
        Assert.Equal(expected.r, actual.r, 5);
        Assert.Equal(expected.g, actual.g, 5);
        Assert.Equal(expected.b, actual.b, 5);
        Assert.Equal(expected.a, actual.a, 5);
    }

    [Fact]
    public void DefaultGradient_IsSolidWhite_FullyOpaque()
    {
        var gradient = new Gradient();
        AssertColor(Color.white, gradient.Evaluate(0f));
        AssertColor(Color.white, gradient.Evaluate(0.5f));
        AssertColor(Color.white, gradient.Evaluate(1f));
    }

    [Fact]
    public void Evaluate_BlendsColorAndAlphaTracksIndependently()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.blue, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

        AssertColor(new Color(1f, 0f, 0f, 1f), gradient.Evaluate(0f));
        AssertColor(new Color(0.5f, 0f, 0.5f, 0.5f), gradient.Evaluate(0.5f));
        AssertColor(new Color(0f, 0f, 1f, 0f), gradient.Evaluate(1f));
    }

    [Fact]
    public void Evaluate_ClampsOutsideTheKeyRange()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.red, 0.25f), new GradientColorKey(Color.blue, 0.75f) },
            new[] { new GradientAlphaKey(0.4f, 0.25f), new GradientAlphaKey(0.8f, 0.75f) });

        AssertColor(new Color(1f, 0f, 0f, 0.4f), gradient.Evaluate(0f));     // before first key
        AssertColor(new Color(0f, 0f, 1f, 0.8f), gradient.Evaluate(1f));     // after last key
        AssertColor(gradient.Evaluate(0f), gradient.Evaluate(-5f));          // clamp01 below
        AssertColor(gradient.Evaluate(1f), gradient.Evaluate(5f));           // clamp01 above
    }

    [Fact]
    public void KeyProperties_ReturnCopies_SnapshotsCannotBeMutatedExternally()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.blue, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.2f, 1f) });

        var snapshot = gradient.alphaKeys;
        snapshot[0].alpha = 0f;

        Assert.Equal(0.8f, gradient.alphaKeys[0].alpha, 5);
        Assert.NotSame(gradient.colorKeys, gradient.colorKeys);
    }

    [Fact]
    public void TrailRenderer_ColorGradient_DefaultsNonNull_AndNullAssignmentRestoresDefault()
    {
        using var loop = new GameLoop();
        var trail = new GameObject("Trail").AddComponent<TrailRenderer>();

        Assert.NotNull(trail.colorGradient);
        Assert.Equal(2, trail.colorGradient.alphaKeys.Length);
        Assert.Equal(1f, trail.colorGradient.alphaKeys[0].alpha, 5);

        var custom = new Gradient();
        trail.colorGradient = custom;
        Assert.Same(custom, trail.colorGradient);

        trail.colorGradient = null;
        Assert.NotNull(trail.colorGradient);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// E15 — GameObjectInjector façade + Container self-resolve
// ══════════════════════════════════════════════════════════════════════════
public class GameObjectInjectorTests
{
    interface IService { }
    class ServiceImpl : IService { }

    class RootConsumer : MonoBehaviour
    {
        [Inject] IService _service;
        [Inject] Container _container;        // the VesselSpawner/PlayerSpawner shape
        public IService Service => _service;
        public Container Container => _container;
    }

    class ChildConsumer : MonoBehaviour
    {
        [Inject] IService _service;
        public IService Service => _service;
    }

    [Fact]
    public void InjectRecursive_PopulatesInjectFields_ThroughTheWholeHierarchy()
    {
        using var loop = new GameLoop();
        var container = new Container();
        var service = new ServiceImpl();
        container.RegisterValue<IService>(service);

        var root = new GameObject("Root");
        var rootConsumer = root.AddComponent<RootConsumer>();
        var child = new GameObject("Child");
        child.transform.SetParent(root.transform, worldPositionStays: false);
        var childConsumer = child.AddComponent<ChildConsumer>();

        GameObjectInjector.InjectRecursive(root, container);

        Assert.Same(service, rootConsumer.Service);
        Assert.Same(service, childConsumer.Service);
    }

    [Fact]
    public void InjectContainer_SelfResolves_ToTheInjectingScope()
    {
        using var loop = new GameLoop();
        var parent = new Container();
        parent.RegisterValue<IService>(new ServiceImpl());
        var child = parent.CreateChild();

        var go = new GameObject("Spawner");
        var consumer = go.AddComponent<RootConsumer>();

        GameObjectInjector.InjectRecursive(go, child);

        Assert.Same(child, consumer.Container);          // nearest scope, not the root
        Assert.Same(child, child.Resolve<Container>());  // direct resolve self-binds too
        Assert.Same(parent, parent.Resolve<Container>());
    }
}

// ══════════════════════════════════════════════════════════════════════════
// Leaf game files — VesselPrefabContainer, SO_ProfileIconList,
// PlayerProfileData, VesselTrailCustomization
// ══════════════════════════════════════════════════════════════════════════
public class ConcreteArcC1LeafTests
{
    // Minimal attachable IVesselStatus for the prefab-container lookup (mirrors
    // VesselLayerTestDoubles.StubVesselStatus, but as a component so
    // Transform.TryGetComponent finds it on the "prefab").
    class FakeVesselStatusComponent : MonoBehaviour, IVesselStatus
    {
        public VesselClassType vesselType = VesselClassType.Manta;

        public IVessel Vessel => null;
        public AIPilot AIPilot => null;
        public AICinematicBehavior AICinematicBehavior => null;
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
        public ResourceSystem ResourceSystem => null;
        public VesselAnimation VesselAnimation => null;
        public VesselCameraCustomizer VesselCameraCustomizer => null;
        public List<GameObject> ShipGeometries { get; set; }
        public Transform ShipTransform => null;
        public VesselTransformer VesselTransformer => null;
        public string Name => "fake";
        public VesselClassType VesselType => vesselType;
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

    static VesselPrefabContainer MakeContainer(params Transform[] prefabs)
    {
        var container = ScriptableObject.CreateInstance<VesselPrefabContainer>();
        typeof(VesselPrefabContainer)
            .GetField("_shipPrefabs", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(container, prefabs);
        return container;
    }

    static Transform MakeVesselPrefab(VesselClassType type, string name)
    {
        var go = new GameObject(name);
        go.AddComponent<FakeVesselStatusComponent>().vesselType = type;
        return go.transform;
    }

    [Fact]
    public void VesselPrefabContainer_EmptyOrUnset_FailsWithNullOut()
    {
        using var loop = new GameLoop();

        var unset = ScriptableObject.CreateInstance<VesselPrefabContainer>();
        Assert.False(unset.TryGetShipPrefab(VesselClassType.Manta, out var none));
        Assert.Null(none);

        var empty = MakeContainer();
        Assert.False(empty.TryGetShipPrefab(VesselClassType.Manta, out _));
    }

    [Fact]
    public void VesselPrefabContainer_FindsPrefabByVesselType_SkippingNullsAndNonVessels()
    {
        using var loop = new GameLoop();
        var manta = MakeVesselPrefab(VesselClassType.Manta, "MantaPrefab");
        var sparrow = MakeVesselPrefab(VesselClassType.Sparrow, "SparrowPrefab");
        var notAVessel = new GameObject("Decoy").transform;

        var container = MakeContainer(null, notAVessel, manta, sparrow);

        Assert.True(container.TryGetShipPrefab(VesselClassType.Sparrow, out var found));
        Assert.Same(sparrow, found);

        Assert.True(container.TryGetShipPrefab(VesselClassType.Manta, out var foundManta));
        Assert.Same(manta, foundManta);

        Assert.False(container.TryGetShipPrefab(VesselClassType.Rhino, out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void SO_ProfileIconList_Defaults_AndProfileIconConstruction()
    {
        var list = ScriptableObject.CreateInstance<SO_ProfileIconList>();
        Assert.Equal(nameof(SO_ProfileIconList), list.name);
        Assert.Null(list.profileIcons);   // inspector-authored in the original; defaults unset

        var sprite = new Sprite { name = "icon_07" };
        var icon = new ProfileIcon("Commander", 7, sprite);
        Assert.Equal("Commander", icon.Name);
        Assert.Equal(7, icon.Id);
        Assert.Same(sprite, icon.IconSprite);

        list.profileIcons = new List<ProfileIcon> { icon };
        Assert.Single(list.profileIcons);
    }

    [Fact]
    public void PlayerProfileData_Defaults_StartEmptyWithLiveRewardList()
    {
        var profile = new PlayerProfileData();

        Assert.Null(profile.userId);
        Assert.Null(profile.displayName);
        Assert.Equal(0, profile.avatarId);
        Assert.Equal(0, profile.crystalBalance);
        Assert.Equal(0, profile.xp);
        Assert.NotNull(profile.unlockedRewardIds);
        Assert.Empty(profile.unlockedRewardIds);
    }

    [Fact]
    public void VesselTrailCustomization_ReTint_PreservesPrefabAuthoredAlphaKeys()
    {
        using var loop = new GameLoop();

        var root = new GameObject("Vessel");
        var trailGO = new GameObject("Trail");
        trailGO.transform.SetParent(root.transform, worldPositionStays: false);
        var trail = trailGO.AddComponent<TrailRenderer>();

        // Prefab-authored alpha curve.
        var authored = new Gradient();
        authored.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.2f, 1f) });
        trail.colorGradient = authored;

        // Awake runs at attach: discovers child trails, snapshots alpha keys.
        var customization = root.AddComponent<VesselTrailCustomization>();

        customization.SetTrailColors(Color.red, Color.blue);

        var colorKeys = trail.colorGradient.colorKeys;
        Assert.Equal(2, colorKeys.Length);
        Assert.Equal(1f, colorKeys[0].color.r, 5);
        Assert.Equal(0f, colorKeys[0].time, 5);
        Assert.Equal(1f, colorKeys[1].color.b, 5);
        Assert.Equal(1f, colorKeys[1].time, 5);

        var alphaKeys = trail.colorGradient.alphaKeys;
        Assert.Equal(0.8f, alphaKeys[0].alpha, 5);
        Assert.Equal(0.2f, alphaKeys[1].alpha, 5);

        // Second re-tint still uses the ORIGINAL authored alpha curve, not the
        // first re-tint's output.
        customization.SetTrailColors(Color.green, Color.yellow);
        alphaKeys = trail.colorGradient.alphaKeys;
        Assert.Equal(0.8f, alphaKeys[0].alpha, 5);
        Assert.Equal(0.2f, alphaKeys[1].alpha, 5);
        Assert.Equal(1f, trail.colorGradient.colorKeys[0].color.g, 5);
    }

    [Fact]
    public void VesselTrailCustomization_DiscoversInactiveChildTrails_AndTintsThemAll()
    {
        using var loop = new GameLoop();

        var root = new GameObject("Vessel");
        var activeGO = new GameObject("TrailA");
        activeGO.transform.SetParent(root.transform, worldPositionStays: false);
        var activeTrail = activeGO.AddComponent<TrailRenderer>();

        var inactiveGO = new GameObject("TrailB");
        inactiveGO.transform.SetParent(root.transform, worldPositionStays: false);
        var inactiveTrail = inactiveGO.AddComponent<TrailRenderer>();
        inactiveGO.SetActive(false);

        var customization = root.AddComponent<VesselTrailCustomization>();
        customization.SetTrailColors(Color.red, Color.blue);

        Assert.Equal(1f, activeTrail.colorGradient.colorKeys[0].color.r, 5);
        Assert.Equal(1f, inactiveTrail.colorGradient.colorKeys[0].color.r, 5);
        Assert.Equal(1f, inactiveTrail.colorGradient.colorKeys[1].color.b, 5);
    }
}
