using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Rendering;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine.UI;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Xunit;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Toy system (Docs/ToySystem/ARCHITECTURE.md) — freestyle world-space stations
// the LOCAL vessel flies into. Ported verbatim (PR #572): Toy base (trigger +
// bloom-in + local-user/freestyle gating + re-arm), SwapToy +
// SwapToySetCoordinator<T> flip-sets (set = universe \ {current}; the used toy
// flips to the option you just left), DomainChangerToySet (server-authoritative
// Player.RequestSetDomain_ServerRpc), VesselChangerToySet (swap execution is the
// menu-swap-arc deviation; reconcile/flip is live), PaintingToy + MenuShapePainter
// (fly-by-numbers; the vessel's own trail paints — the runner never creates or
// destroys mass), ToyboxSO (registry + unlock state), ToyboxController (membrane
// ring placement with fallback).
//
// Trigger interactions run through the REAL TriggerPass pipeline (ContactArcTests
// rig style): toy = trigger SphereCollider, vessel = non-trigger SphereCollider +
// real VesselStatus resolving IsLocalUser through its Player.
// ─────────────────────────────────────────────────────────────────────────────

// ── doubles ──────────────────────────────────────────────────────────────────

/// <summary>Toy subclass recording activations (the abstract OnActivated hook).</summary>
class RecordingToy : Toy
{
    public readonly List<IVesselStatus> Activations = new();
    protected override void OnActivated(IVesselStatus localVessel) => Activations.Add(localVessel);
}

/// <summary>Concrete definition for tests that only need metadata (Spawn unused).</summary>
class ToyTestDefinition : ToyDefinitionSO
{
    public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context) { }
}

/// <summary>No-op IVessel on the vessel GameObject — real Transform, recorded autopilot toggles.</summary>
class ToyStubVessel : MonoBehaviour, IVessel
{
    public VesselStatus Status;
    public readonly List<bool> AIPilotToggles = new();

    public event Action OnInitialized { add { } remove { } }
    public event Action OnBeforeDestroyed { add { } remove { } }

    public IVesselStatus VesselStatus => Status;
    public bool IsNetworkOwner => false;
    public bool IsNetworkClient => false;
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
    public void ToggleAIPilot(bool toggle) => AIPilotToggles.Add(toggle);
    public void StartVessel() { }
    public bool AllowClearPrismInitialization() => true;
    public void DestroyVessel() { }
    public void ResetForPlay() { }
    public void SetPose(Pose pose) { }
    public void ChangePlayer(IPlayer player) { }
    public void ModifyThrottle(float amount, float duration) { }
    public void AddSlowedShipTransformToGameData() { }
    public void RemoveSlowedShipTransformFromGameData() { }
}

/// <summary>Minimal IPlayer double: configurable locality/domain, pass-through vessel.</summary>
class ToyStubPlayer : IPlayer
{
    public bool LocalUser = true;
    public Domains DomainValue = Domains.Jade;
    public IVessel VesselRef;
    public InputController Input;

    public Domains Domain => DomainValue;
    public string Name => "ToyPilot";
    public int AvatarId => 0;
    public string PlayerUUID => Name;
    public IVessel Vessel => VesselRef;
    public InputController InputController => Input;
    public IInputStatus InputStatus => Input != null ? Input.InputStatus : null;
    public IRoundStats RoundStats => null;
    public bool IsActive => true;
    public bool IsInitializedAsAI => false;
    public bool IsMultiplayerOwner => LocalUser;
    public bool IsNetworkOwner => LocalUser;
    public bool IsNetworkClient => false;
    public bool IsLocalUser => LocalUser;
    public ulong PlayerNetId => 0;
    public ulong VesselNetId => 0;
    public ulong OwnerClientNetId => 0;
    public Transform Transform => VesselRef?.Transform;

    public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel) => VesselRef = vessel;
    public void InitializeForMultiplayerMode(IVessel vessel) => VesselRef = vessel;
    public void ToggleGameObject(bool toggle) { }
    public void DestroyPlayer() { }
    public void StartPlayer() { }
    public void ResetForPlay() { }
    public void ChangeVessel(IVessel vessel) => VesselRef = vessel;
}

// ── rig ──────────────────────────────────────────────────────────────────────

static class ToyRig
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Set(object target, string field, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, All | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{field}' not found on {target.GetType().Name}.");
    }

    public static T Get<T>(object target, string field)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, All | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            return (T)f.GetValue(target);
        }
        throw new InvalidOperationException($"Field '{field}' not found on {target.GetType().Name}.");
    }

    public static GameDataSO MakeGameData(VesselClassType selected = VesselClassType.Manta)
    {
        NetworkManager.Singleton = null;
        var data = ScriptableObject.CreateInstance<GameDataSO>();
        data.OnPlayerNetworkSpawnedUlong = ScriptableObject.CreateInstance<ScriptableEventUlong>();
        data.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
        data.selectedVesselClass.Value = selected;
        return data;
    }

    public static void SetLocalPlayer(GameDataSO data, IPlayer player) =>
        typeof(GameDataSO).GetProperty("LocalPlayer")!.SetValue(data, player);

    /// <summary>
    /// Vessel rig: non-trigger SphereCollider + stub IVessel + REAL VesselStatus whose
    /// Player controls IsLocalUser — exactly what Toy.TryGetLocalVessel resolves.
    /// </summary>
    public static (GameObject go, VesselStatus status, ToyStubVessel stub, ToyStubPlayer player) MakeVessel(
        Vector3 position, bool isLocal = true, VesselClassType vesselType = VesselClassType.Manta)
    {
        var go = new GameObject("vessel");
        go.transform.position = position;
        var col = go.AddComponent<SphereCollider>();
        col.radius = 1f;

        var stub = go.AddComponent<ToyStubVessel>();
        var status = go.AddComponent<VesselStatus>();
        Set(status, "_shipInstance", stub);
        Set(status, "vesselType", vesselType);
        stub.Status = status;

        var player = new ToyStubPlayer { LocalUser = isLocal, VesselRef = stub };
        status.Player = player;
        return (go, status, stub, player);
    }

    public static ToyContext MakeContext(GameDataSO gameData, Func<bool> freestyle = null) => new()
    {
        GameData = gameData,
        IsFreestyleActive = freestyle ?? (() => true),
    };

    /// <summary>Direct children of a transform (Transform is non-generic IEnumerable).</summary>
    public static List<Transform> Children(Transform t)
    {
        var list = new List<Transform>();
        foreach (Transform child in t) list.Add(child);
        return list;
    }

    public static string LabelText(Transform toyRoot) =>
        toyRoot.gameObject.GetComponentInChildren<TMP_Text>(includeInactive: true)?.text;

    public static void RunSeconds(GameLoop loop, float seconds, float dt = 0.1f)
    {
        int frames = (int)MathF.Ceiling(seconds / dt) + 1;
        for (int i = 0; i < frames; i++) loop.Tick(dt);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Engine surface added for the toy arc
// ─────────────────────────────────────────────────────────────────────────────

public class ToyEngineSurfaceTests : IDisposable
{
    readonly GameLoop loop = new();
    public void Dispose() => loop.Dispose();

    [Fact]
    public void LineRenderer_PositionBuffer_FollowsOriginalContract()
    {
        var go = new GameObject("line");
        var lr = go.AddComponent<LineRenderer>();

        Assert.True(lr.useWorldSpace);
        Assert.Equal(0, lr.positionCount);

        // Resize preserves existing values; new slots zero-fill.
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(1f, 2f, 3f));
        lr.SetPosition(1, new Vector3(4f, 5f, 6f));
        lr.positionCount = 3;
        Assert.Equal(new Vector3(1f, 2f, 3f), lr.GetPosition(0));
        Assert.Equal(new Vector3(4f, 5f, 6f), lr.GetPosition(1));
        Assert.Equal(Vector3.zero, lr.GetPosition(2));

        // SetPositions copies min(source, positionCount) — no resize.
        lr.SetPositions(new[] { new Vector3(7f, 0f, 0f), new Vector3(8f, 0f, 0f) });
        Assert.Equal(3, lr.positionCount);
        Assert.Equal(new Vector3(7f, 0f, 0f), lr.GetPosition(0));
        Assert.Equal(new Vector3(8f, 0f, 0f), lr.GetPosition(1));

        // Shrink truncates.
        lr.positionCount = 1;
        Assert.Equal(1, lr.positionCount);
        Assert.Equal(new Vector3(7f, 0f, 0f), lr.GetPosition(0));
    }

    [Fact]
    public void CreatePrimitive_Sphere_HasRendererAndHalfUnitSphereCollider()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Assert.Equal("Sphere", go.name);
        Assert.NotNull(go.GetComponent<MeshRenderer>());
        var col = go.GetComponent<SphereCollider>();
        Assert.NotNull(col);
        Assert.Equal(0.5f, col.radius);
        Assert.False(col.isTrigger);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Assert.Equal("Cube", cube.name);
        Assert.NotNull(cube.GetComponent<BoxCollider>());
    }

    [Fact]
    public void TmpShim_FrozenAlignmentValues_AndUsableAsComponent()
    {
        // Original TMPro numeric values (horizontal 1..4 | vertical 256/512/1024).
        Assert.Equal(257, (int)TextAlignmentOptions.TopLeft);
        Assert.Equal(514, (int)TextAlignmentOptions.Center);
        Assert.Equal(1028, (int)TextAlignmentOptions.BottomRight);

        var go = new GameObject("label");
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = "Jade";
        tmp.color = Color.green;
        tmp.alignment = TextAlignmentOptions.Center;
        Assert.Equal("Jade", go.GetComponent<TMP_Text>().text);
        Assert.Null(TMP_Settings.defaultFontAsset);
    }

    [Fact]
    public void ShadowCastingMode_FrozenValues_AndRendererDefaults()
    {
        Assert.Equal(0, (int)CosmicShore.Engine.Rendering.ShadowCastingMode.Off);
        Assert.Equal(1, (int)CosmicShore.Engine.Rendering.ShadowCastingMode.On);
        Assert.Equal(2, (int)CosmicShore.Engine.Rendering.ShadowCastingMode.TwoSided);
        Assert.Equal(3, (int)CosmicShore.Engine.Rendering.ShadowCastingMode.ShadowsOnly);

        var go = new GameObject("r");
        var renderer = go.AddComponent<MeshRenderer>();
        Assert.Equal(CosmicShore.Engine.Rendering.ShadowCastingMode.On, renderer.shadowCastingMode);
        Assert.True(renderer.receiveShadows);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Toy base — bloom-in, trigger gating, re-arm
// ─────────────────────────────────────────────────────────────────────────────

public class ToyBaseTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    GameObject Track(GameObject go) { spawned.Add(go); return go; }

    (RecordingToy toy, GameObject root) MakeToy(Vector3 position, ToyContext context, float triggerRadius = 40f)
    {
        var root = Track(ToyFactory.CreateBareRoot("test", null, position, Vector3.zero, triggerRadius));
        var toy = root.AddComponent<RecordingToy>();
        toy.Initialize(ScriptableObject.CreateInstance<ToyTestDefinition>(), context, default);
        return (toy, root);
    }

    [Fact]
    public void BloomIn_ScalesFromZero_AndArmsOnlyAfterBloom()
    {
        var gameData = ToyRig.MakeGameData();
        var (toy, root) = MakeToy(new Vector3(0f, 0f, 300f), ToyRig.MakeContext(gameData));
        var (vesselGo, _, _, _) = ToyRig.MakeVessel(Vector3.zero);
        Track(vesselGo);

        // Continuity law: the toy starts at scale zero the moment it is initialized.
        Assert.Equal(Vector3.zero, root.transform.localScale);

        // Mid-bloom the vessel flies through — the toy is not armed yet: no activation.
        loop.Tick(0.1f);
        vesselGo.transform.position = root.transform.position;
        loop.Tick(0.1f);
        Assert.Empty(toy.Activations);

        // Bloom completes (1.2s default) while the vessel sits inside — still no
        // activation (arming does not synthesize an enter), and scale reaches target.
        ToyRig.RunSeconds(loop, 1.5f);
        Assert.Equal(Vector3.one, root.transform.localScale);
        Assert.Empty(toy.Activations);

        // Leave, re-arm, and fly back through: exactly one activation per pass.
        vesselGo.transform.position = Vector3.zero;
        loop.Tick(0.1f);
        ToyRig.RunSeconds(loop, 0.5f); // > rearmDelaySeconds (0.35)
        vesselGo.transform.position = root.transform.position;
        loop.Tick(0.1f);
        Assert.Single(toy.Activations);
    }

    [Fact]
    public void RemoteVessel_NeverTripsTheToy()
    {
        var gameData = ToyRig.MakeGameData();
        var (toy, root) = MakeToy(new Vector3(0f, 0f, 300f), ToyRig.MakeContext(gameData));
        var (vesselGo, _, _, _) = ToyRig.MakeVessel(Vector3.zero, isLocal: false);
        Track(vesselGo);

        ToyRig.RunSeconds(loop, 1.5f); // bloom
        vesselGo.transform.position = root.transform.position;
        ToyRig.RunSeconds(loop, 0.3f);
        Assert.Empty(toy.Activations);
    }

    [Fact]
    public void AutopilotLavaLamp_IsInert_UntilFreestyle()
    {
        var gameData = ToyRig.MakeGameData();
        bool freestyle = false;
        var (toy, root) = MakeToy(new Vector3(0f, 0f, 300f), ToyRig.MakeContext(gameData, () => freestyle));
        var (vesselGo, _, _, _) = ToyRig.MakeVessel(Vector3.zero);
        Track(vesselGo);

        ToyRig.RunSeconds(loop, 1.5f); // bloom

        // Autopilot drift through the toy: visible but inert.
        vesselGo.transform.position = root.transform.position;
        loop.Tick(0.1f);
        Assert.Empty(toy.Activations);

        // Player takes control; the toy re-arms on exit and fires on the next pass.
        freestyle = true;
        vesselGo.transform.position = Vector3.zero;
        ToyRig.RunSeconds(loop, 0.5f);
        vesselGo.transform.position = root.transform.position;
        loop.Tick(0.1f);
        Assert.Single(toy.Activations);
    }

    [Fact]
    public void Rearm_AllowsIndefinitePlay_ToyIsNeverConsumed()
    {
        var gameData = ToyRig.MakeGameData();
        var (toy, root) = MakeToy(new Vector3(0f, 0f, 300f), ToyRig.MakeContext(gameData));
        var (vesselGo, _, _, _) = ToyRig.MakeVessel(Vector3.zero);
        Track(vesselGo);

        ToyRig.RunSeconds(loop, 1.5f); // bloom

        for (int pass = 1; pass <= 3; pass++)
        {
            vesselGo.transform.position = root.transform.position;
            loop.Tick(0.1f);
            Assert.Equal(pass, toy.Activations.Count);

            vesselGo.transform.position = Vector3.zero;
            loop.Tick(0.1f);
            ToyRig.RunSeconds(loop, 0.5f); // rearm delay
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Domain changer — full pipeline: trigger → ServerRpc → NetDomain → mirror → flip
// ─────────────────────────────────────────────────────────────────────────────

public class DomainChangerToySetTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        AuthenticationService.Reset();
        NetworkManager.Singleton = null;
    }

    GameObject Track(GameObject go) { spawned.Add(go); return go; }

    (Player player, GameDataSO gameData) SpawnLocalHumanPlayer()
    {
        var gameData = ToyRig.MakeGameData();
        var go = Track(new GameObject("player"));
        var player = go.AddComponent<Player>();
        ToyRig.Set(player, "gameData", gameData);
        AuthenticationService.Instance.PlayerName = "Pilot#0001";
        player.Spawn(); // host-mode defaults: server+client+owner → IsLocalUser
        ToyRig.SetLocalPlayer(gameData, player);
        return (player, gameData);
    }

    [Fact]
    public void FlyThrough_RequestsDomainServerSide_AndUsedToyFlipsToPreviousDomain()
    {
        var (player, gameData) = SpawnLocalHumanPlayer();

        // Vessel that trips toys resolves locality through the REAL spawned Player.
        var (vesselGo, status, _, _) = ToyRig.MakeVessel(Vector3.zero);
        Track(vesselGo);
        status.Player = player;

        var definition = ScriptableObject.CreateInstance<DomainChangerToyDefinitionSO>();
        var setParent = Track(new GameObject("toys"));
        var placement = new ToyPlacement(new Vector3(0f, 0f, 300f), Vector3.zero, 20f, 40f);
        definition.Spawn(setParent.transform, placement, ToyRig.MakeContext(gameData));

        var host = ToyRig.Children(setParent.transform).Single(); // ToySet_<id>

        // First reconcile: current = Jade → the set shows the two domains you are NOT on.
        loop.Tick(0.1f);
        var roots = ToyRig.Children(host);
        Assert.Equal(2, roots.Count);
        var labels = roots.Select(ToyRig.LabelText).ToArray();
        Assert.Contains("Ruby", labels);
        Assert.Contains("Gold", labels);
        Assert.DoesNotContain("Jade", labels);

        var rubyRoot = roots.Single(r => ToyRig.LabelText(r) == "Ruby");
        var goldRoot = roots.Single(r => ToyRig.LabelText(r) == "Gold");

        ToyRig.RunSeconds(loop, 1.5f); // bloom → armed

        // Fly through the Ruby toy.
        vesselGo.transform.position = rubyRoot.position;
        loop.Tick(0.1f); // trigger pass → Apply → RequestSetDomain_ServerRpc (local-invoke)

        // Server-authoritative write landed on NetDomain and mirrored into Player.Domain.
        Assert.Equal(Domains.Ruby, player.NetDomain.Value);
        Assert.Equal(Domains.Ruby, player.Domain);

        // Next frame's poll reconciles the set: the used toy flips to the domain you just
        // left (Jade), the untouched Gold toy is kept — set = universe \ {current}.
        loop.Tick(0.1f);
        Assert.Equal("Jade", ToyRig.LabelText(rubyRoot));
        Assert.Equal("Gold", ToyRig.LabelText(goldRoot));
        var after = ToyRig.Children(host);
        Assert.Equal(2, after.Count);
        Assert.Contains(rubyRoot, after); // flipped in place, not recreated
        Assert.Contains(goldRoot, after);
    }

    [Fact]
    public void ExternalDomainChange_ReconcilesTheSameWay()
    {
        var (player, gameData) = SpawnLocalHumanPlayer();

        var definition = ScriptableObject.CreateInstance<DomainChangerToyDefinitionSO>();
        var setParent = Track(new GameObject("toys"));
        var placement = new ToyPlacement(new Vector3(0f, 0f, 300f), Vector3.zero, 20f, 40f);
        definition.Spawn(setParent.transform, placement, ToyRig.MakeContext(gameData));
        var host = ToyRig.Children(setParent.transform).Single();

        loop.Tick(0.1f);
        var roots = ToyRig.Children(host);
        var goldRoot = roots.Single(r => ToyRig.LabelText(r) == "Gold");

        // A panel/menu reset writes the domain through the same server-side path.
        player.NetDomain.Value = Domains.Gold;
        loop.Tick(0.1f);

        var labels = ToyRig.Children(host).Select(ToyRig.LabelText).ToArray();
        Assert.Contains("Jade", labels);
        Assert.Contains("Ruby", labels);
        Assert.DoesNotContain("Gold", labels);
        Assert.Equal("Jade", ToyRig.LabelText(goldRoot)); // used/now-current slot flips to previous
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Vessel changer — flip-set reconcile + control restore (swap execution is the
// menu-swap-arc deviation; see VesselChangerToySet.Apply)
// ─────────────────────────────────────────────────────────────────────────────

public class VesselChangerToySetTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    GameObject Track(GameObject go) { spawned.Add(go); return go; }

    (Transform host, VesselStatus status, ToyStubVessel stub, ToyStubPlayer player, GameObject vesselGo, GameDataSO gameData)
        MakeRig(Func<bool> freestyle = null)
    {
        var gameData = ToyRig.MakeGameData();
        var (vesselGo, status, stub, player) = ToyRig.MakeVessel(Vector3.zero, vesselType: VesselClassType.Manta);
        Track(vesselGo);
        ToyRig.SetLocalPlayer(gameData, player);

        var definition = ScriptableObject.CreateInstance<VesselChangerToyDefinitionSO>();
        ToyRig.Set(definition, "vesselCollection",
            new[] { VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Rhino });

        var setParent = Track(new GameObject("toys"));
        var placement = new ToyPlacement(new Vector3(0f, 0f, 300f), Vector3.zero, 20f, 40f);
        definition.Spawn(setParent.transform, placement, ToyRig.MakeContext(gameData, freestyle));
        var host = ToyRig.Children(setParent.transform).Single();
        return (host, status, stub, player, vesselGo, gameData);
    }

    [Fact]
    public void Set_ShowsCollectionMinusCurrent_WithSphereFallbackBodies()
    {
        var (host, _, _, _, _, _) = MakeRig();

        loop.Tick(0.1f);
        var roots = ToyRig.Children(host);
        Assert.Equal(2, roots.Count);
        var labels = roots.Select(ToyRig.LabelText).ToArray();
        Assert.Contains("Dolphin", labels);
        Assert.Contains("Rhino", labels);
        Assert.DoesNotContain("Manta", labels); // the ship you're flying is excluded

        // Headless mini-model fallback (mesh arc deviation): procedural sphere bodies.
        foreach (var root in roots)
        {
            var body = ToyRig.Children(root).Single(c => c.name == "Body");
            Assert.Contains(ToyRig.Children(body), c => c.name == "Sphere");
        }
    }

    [Fact]
    public void ExternalSwap_FlipsUsedSlotToPreviousShip()
    {
        var (host, status, _, _, _, _) = MakeRig();

        loop.Tick(0.1f);
        var dolphinRoot = ToyRig.Children(host).Single(r => ToyRig.LabelText(r) == "Dolphin");

        // The swap pipeline replaces the vessel; the coordinator polls the live
        // VesselStatus (panel path and toy path reconcile identically).
        ToyRig.Set(status, "vesselType", VesselClassType.Dolphin);
        loop.Tick(0.1f);

        var labels = ToyRig.Children(host).Select(ToyRig.LabelText).ToArray();
        Assert.Contains("Manta", labels);  // flip-to-previous
        Assert.Contains("Rhino", labels);
        Assert.DoesNotContain("Dolphin", labels);
        Assert.Equal("Manta", ToyRig.LabelText(dolphinRoot)); // same toy object flipped in place
    }

    [Fact]
    public void FlyThrough_RestoresFreestyleControlAfterSwapDelay()
    {
        var (host, _, stub, player, vesselGo, _) = MakeRig();

        // Real InputController for the restore path (active GO so Awake wires InputStatus;
        // Update self-gates on isInitialized).
        var inputGo = Track(new GameObject("input"));
        player.Input = inputGo.AddComponent<InputController>();
        player.Input.SetPause(true);

        loop.Tick(0.1f);
        var dolphinRoot = ToyRig.Children(host).Single(r => ToyRig.LabelText(r) == "Dolphin");
        ToyRig.RunSeconds(loop, 1.5f); // bloom → armed

        vesselGo.transform.position = dolphinRoot.position;
        loop.Tick(0.1f); // activation (swap request itself is the menu-swap-arc deviation)

        Assert.True(player.Input.InputStatus.Paused);
        Assert.DoesNotContain(false, stub.AIPilotToggles);

        // RestoreDelayMs (600 ms unscaled) later, freestyle control is handed back:
        // autopilot off + input unpaused (mirrors MenuVesselSelectionPanelController).
        ToyRig.RunSeconds(loop, 0.8f);
        Assert.Contains(false, stub.AIPilotToggles);
        Assert.False(player.Input.InputStatus.Paused);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Painting toy — fly-by-numbers runner
// ─────────────────────────────────────────────────────────────────────────────

public class PaintingToyTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    GameObject Track(GameObject go) { spawned.Add(go); return go; }

    static ShapeDefinition MakeStar()
    {
        var star = ScriptableObject.CreateInstance<ShapeDefinition>();
        star.shapeName = "Star";
        star.autoGeneratePreset = ShapePreset.Star;
        star.autoGenerateRadius = 100f;
        return star;
    }

    [Fact]
    public void FlyByNumbers_GuidesThroughEveryWaypointInOrder_ThenCompletesAndFades()
    {
        var gameData = ToyRig.MakeGameData();
        var (vesselGo, _, _, _) = ToyRig.MakeVessel(Vector3.zero);
        Track(vesselGo);

        var definition = ScriptableObject.CreateInstance<PaintingToyDefinitionSO>();
        var star = MakeStar();
        ToyRig.Set(definition, "shape", star);

        var toysParent = Track(new GameObject("toys"));
        var placement = new ToyPlacement(new Vector3(0f, 0f, 1000f), Vector3.zero, 20f, 40f);
        definition.Spawn(toysParent.transform, placement, ToyRig.MakeContext(gameData));
        var toyRoot = ToyRig.Children(toysParent.transform).Single();

        ToyRig.RunSeconds(loop, 1.5f); // bloom → armed

        // Fly into the toy → a self-contained painter starts.
        vesselGo.transform.position = toyRoot.position;
        loop.Tick(0.1f);
        var painter = Object.FindFirstObjectByType<MenuShapePainter>();
        Assert.NotNull(painter);

        // The ghost outline previews the full shape (star preset waypoints), and the
        // world waypoints are the shape's local points placed ahead of the vessel.
        var expectedLocal = star.GetAllWorldWaypoints(Vector3.zero, 1f);
        var waypoints = ToyRig.Get<Vector3[]>(painter, "_worldWaypoints");
        Assert.Equal(expectedLocal.Length, waypoints.Length);
        var ghost = painter.gameObject.GetComponentInChildren<LineRenderer>(includeInactive: true);
        Assert.NotNull(ghost);
        Assert.Equal(waypoints.Length, ghost.positionCount);

        // The toy station is done for this test — the painter runs standalone
        // (retire it so waypoint flight near the station can't re-trigger a new run).
        Object.Destroy(toyRoot.gameObject);

        // A "painted trail" stand-in laid during the run: the runner must never touch
        // it — the painted structure is conserved mass, guides are the only teardown.
        var paintedTrail = Track(new GameObject("painted-prism"));
        paintedTrail.transform.position = waypoints[0];

        bool completed = false;
        painter.Completed += () => completed = true;

        // Fly the numbers: visit every waypoint in order; the marker advances with us.
        for (int i = 0; i < waypoints.Length; i++)
        {
            Assert.Equal(i, ToyRig.Get<int>(painter, "_index"));
            vesselGo.transform.position = waypoints[i];
            loop.Tick(0.1f);
        }

        Assert.True(completed); // no fail state — reaching the last point completes
        Assert.False(ToyRig.Get<bool>(painter, "_running"));

        // The guides fade out (2s unscaled), then the runner destroys ONLY itself.
        ToyRig.RunSeconds(loop, 2.5f);
        Assert.False(painter);            // runner gone
        Assert.True((bool)paintedTrail);  // painted structure untouched (mass conserved)
    }

    [Fact]
    public void SecondPassDuringActiveRun_DoesNotStartASecondPainter()
    {
        var gameData = ToyRig.MakeGameData();
        var (vesselGo, _, _, _) = ToyRig.MakeVessel(Vector3.zero);
        Track(vesselGo);

        var definition = ScriptableObject.CreateInstance<PaintingToyDefinitionSO>();
        ToyRig.Set(definition, "shape", MakeStar());

        var toysParent = Track(new GameObject("toys"));
        var placement = new ToyPlacement(new Vector3(0f, 0f, 1000f), Vector3.zero, 20f, 40f);
        definition.Spawn(toysParent.transform, placement, ToyRig.MakeContext(gameData));
        var toyRoot = ToyRig.Children(toysParent.transform).Single();

        ToyRig.RunSeconds(loop, 1.5f);

        vesselGo.transform.position = toyRoot.position;
        loop.Tick(0.1f);
        Assert.NotNull(Object.FindFirstObjectByType<MenuShapePainter>());

        // Leave, re-arm, and pass through again mid-run: the toy re-activates but the
        // painting toy keeps the one active run (no painter pile-up).
        vesselGo.transform.position = Vector3.zero;
        ToyRig.RunSeconds(loop, 0.5f);
        vesselGo.transform.position = toyRoot.position;
        loop.Tick(0.1f);

        int painterCount = 0;
        foreach (var root in GameLoop.Current.Scene.GetRootGameObjects())
            if (root && root.name == "MenuShapePainter") painterCount++;
        Assert.Equal(1, painterCount);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ToyboxSO — registry + unlock state
// ─────────────────────────────────────────────────────────────────────────────

public class ToyboxSOTests
{
    static ToyTestDefinition MakeDef(string id, bool unlockedByDefault = true)
    {
        var def = ScriptableObject.CreateInstance<ToyTestDefinition>();
        ToyRig.Set(def, "id", id);
        ToyRig.Set(def, "unlockedByDefault", unlockedByDefault);
        return def;
    }

    [Fact]
    public void UnlockState_SeedsFromDefaults_AndSetToyUnlockedGatesTheToybox()
    {
        var box = ScriptableObject.CreateInstance<ToyboxSO>();
        var a = MakeDef("a");
        var b = MakeDef("b", unlockedByDefault: false);
        box.AddToy(a);
        box.AddToy(b);

        Assert.True(box.IsUnlocked(a));
        Assert.False(box.IsUnlocked(b));
        Assert.Equal(new[] { a }, box.UnlockedToys().ToArray());

        int changed = 0;
        box.OnToyboxChanged += () => changed++;

        box.SetToyUnlocked("b", true);   // future unlock-condition hook
        Assert.Equal(1, changed);
        Assert.Equal(new ToyDefinitionSO[] { a, b }, box.UnlockedToys().ToArray());

        box.SetToyUnlocked("b", true);   // same value — dedup, no event
        Assert.Equal(1, changed);

        box.SetToyUnlocked("a", false);  // re-lock
        Assert.Equal(2, changed);
        Assert.Equal(new ToyDefinitionSO[] { b }, box.UnlockedToys().ToArray());
    }

    [Fact]
    public void Id_FallsBackToAssetName_WhenUnset()
    {
        var def = ScriptableObject.CreateInstance<ToyTestDefinition>();
        Assert.Equal(def.name, def.Id);
        ToyRig.Set(def, "id", "painting");
        Assert.Equal("painting", def.Id);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ToyboxController — placement ring + lifecycle
// ─────────────────────────────────────────────────────────────────────────────

public class ToyboxControllerTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    GameObject Track(GameObject go) { spawned.Add(go); return go; }

    static MenuFreestyleEventsContainerSO MakeFreestyleEvents()
    {
        var container = ScriptableObject.CreateInstance<MenuFreestyleEventsContainerSO>();
        container.OnGameStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        container.OnGameStateTransitionEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        container.OnMenuStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        container.OnMenuStateTransitionEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        return container;
    }

    static PaintingToyDefinitionSO MakePaintingDef(string id, float angleDeg = -1f)
    {
        var def = ScriptableObject.CreateInstance<PaintingToyDefinitionSO>();
        ToyRig.Set(def, "id", id);
        ToyRig.Set(def, "placementAngleDegrees", angleDeg);
        return def;
    }

    (ToyboxController controller, GameDataSO gameData) MakeController(ToyboxSO box)
    {
        var gameData = ToyRig.MakeGameData();
        var go = Track(new GameObject("toybox-controller"));
        var controller = go.AddComponent<ToyboxController>();
        ToyRig.Set(controller, "_gameData", gameData);
        ToyRig.Set(controller, "_freestyleEvents", MakeFreestyleEvents());
        if (box != null) ToyRig.Set(controller, "toybox", box);
        return (controller, gameData);
    }

    [Fact]
    public void PlacesUnlockedToys_OnFallbackRing_AutoDistributedAndPinned()
    {
        var box = ScriptableObject.CreateInstance<ToyboxSO>();
        box.AddToy(MakePaintingDef("a"));               // auto
        box.AddToy(MakePaintingDef("b", angleDeg: 90f)); // pinned
        box.AddToy(MakePaintingDef("c"));               // auto

        var (controller, gameData) = MakeController(box);
        loop.Tick(0.1f); // Start → subscribe

        gameData.OnClientReady.Raise(); // menu vessel ready → place
        var root = ToyRig.Children(controller.transform).Single(t => t.name == "FreestyleToybox");
        var toys = ToyRig.Children(root);
        Assert.Equal(3, toys.Count);

        // No Cell in the scene → fallback ring (center zero, radius 300). Auto-placed
        // toys distribute evenly (0°, 180°); the pinned one sits at its angle (90°).
        Vector3 PosOf(string name) => toys.Single(t => t.name == $"Toy_{name}").position;
        Assert.True((PosOf("a") - new Vector3(0f, 0f, 300f)).magnitude < 0.01f);
        Assert.True((PosOf("b") - new Vector3(300f, 0f, 0f)).magnitude < 0.01f);
        Assert.True((PosOf("c") - new Vector3(0f, 0f, -300f)).magnitude < 0.01f);

        // Second OnClientReady is idempotent (_placed guard).
        gameData.OnClientReady.Raise();
        Assert.Equal(3, ToyRig.Children(root).Count);

        // Run the blooms to completion: they were started by the Raise() above (outside a
        // Tick), so their fire-and-forget async-voids captured xunit's sync context — the
        // runner waits on them at test end (the async-void hang trap).
        ToyRig.RunSeconds(loop, 1.5f);
    }

    [Fact]
    public void LockedToys_AreNotPlaced()
    {
        var box = ScriptableObject.CreateInstance<ToyboxSO>();
        var locked = MakePaintingDef("locked");
        ToyRig.Set(locked, "unlockedByDefault", false);
        box.AddToy(MakePaintingDef("open"));
        box.AddToy(locked);

        var (controller, gameData) = MakeController(box);
        loop.Tick(0.1f);
        gameData.OnClientReady.Raise();

        var root = ToyRig.Children(controller.transform).Single(t => t.name == "FreestyleToybox");
        var toys = ToyRig.Children(root);
        Assert.Single(toys);
        Assert.Equal("Toy_open", toys[0].name);

        ToyRig.RunSeconds(loop, 1.5f); // complete the bloom started outside a Tick (async-void trap)
    }

    [Fact]
    public void ZeroConfig_SynthesisesDefaultToybox_WithTheThreeBuiltInToys()
    {
        var (controller, gameData) = MakeController(box: null);
        loop.Tick(0.1f);
        gameData.OnClientReady.Raise();

        var root = ToyRig.Children(controller.transform).Single(t => t.name == "FreestyleToybox");
        var names = ToyRig.Children(root).Select(t => t.name).ToArray();
        Assert.Equal(3, names.Length);
        Assert.Contains("Toy_painting", names);            // fly-by-numbers (with a star shape)
        Assert.Contains("ToySet_vessel_changer", names);   // coordinated flip-set
        Assert.Contains("ToySet_domain_changer", names);   // coordinated flip-set

        ToyRig.RunSeconds(loop, 1.5f); // complete the bloom started outside a Tick (async-void trap)
    }

    [Fact]
    public void Teardown_DestroysTheWholeToyboxSubtree()
    {
        var box = ScriptableObject.CreateInstance<ToyboxSO>();
        box.AddToy(MakePaintingDef("a"));
        var (controller, gameData) = MakeController(box);
        loop.Tick(0.1f);
        gameData.OnClientReady.Raise();

        var root = ToyRig.Children(controller.transform).Single(t => t.name == "FreestyleToybox");
        Assert.True((bool)root.gameObject);

        Object.Destroy(controller.gameObject);
        loop.Tick(0.1f); // destroy queue flush
        Assert.False((bool)root.gameObject);
    }
}
