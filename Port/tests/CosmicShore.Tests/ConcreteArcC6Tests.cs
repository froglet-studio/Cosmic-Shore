using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C6 — concrete-arc iteration 6 (VESSEL_CONCRETE.md, FINAL row): the verbatim
// single-player spawning pipeline — VesselSpawner (prefab clone + DI inject),
// PlayerSpawner (player clone → InitializeForSinglePlayerMode → vessel.Initialize,
// orphan destroy on vessel failure), PlayerSpawnerAdapterBase +
// MiniGamePlayerSpawnerAdapter (OnInitializeGame flow, profile-name resolution,
// AI defaults) + VolumeTestPlayerSpawnerAdapter (spawn-and-activate at Start).
//
// Prefab fixture: templates are built INACTIVE (engine prefab semantics — a
// template's lifecycle hooks must not run until a clone is activated into the
// scene). The E16 clone remap is what makes Instantiate prefab-faithful; the
// survey's corruption sentinel is `clone VesselStatus.Vessel == clone's own
// VesselController`.
//
// Known trap (C4 precedent): StartVessel/StartPlayer reach
// VesselPrismController.StartSpawn() whose SpawnLoopAsync(ct).Forget() registers
// on xunit's AsyncTestSyncContext — call StopSpawn() before the test returns.
// ─────────────────────────────────────────────────────────────────────────────

static class C6Reflect
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
}

/// <summary>Counting HUD controller; captures Player.Name at Initialize time —
/// the ordering probe proving InitializeForSinglePlayerMode ran BEFORE
/// vessel.Initialize (only then is the name already resolved when the HUD
/// initializes inside the vessel chain).</summary>
sealed class C6HudController : MonoBehaviour, IVesselHUDController
{
    public int InitializeCalls, SubscribeCalls, UnsubscribeCalls, ShowCalls, HideCalls;
    public string PlayerNameAtInitialize;
    public IVesselStatus LastInitializedStatus;
    public GameObject LastBlockPrefab;

    public void Initialize(IVesselStatus vesselStatus)
    {
        InitializeCalls++;
        LastInitializedStatus = vesselStatus;
        PlayerNameAtInitialize = vesselStatus.Player?.Name;
    }

    public void SubscribeToEvents() => SubscribeCalls++;
    public void UnsubscribeFromEvents() => UnsubscribeCalls++;
    public void ShowHUD() => ShowCalls++;
    public void HideHUD() => HideCalls++;
    public void SetBlockPrefab(GameObject prefab) => LastBlockPrefab = prefab;
}

/// <summary>Concrete VesselAnimation (base is abstract) — no-op puppetry.</summary>
sealed class C6VesselAnimation : VesselAnimation
{
    protected override void AssignTransforms() { }
    protected override void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle) { }
}

/// <summary>Handles to the template ("prefab") hierarchy for aliasing checks.</summary>
sealed class C6VesselTemplate
{
    public GameObject Root;
    public VesselController Controller;
    public VesselStatus Status;
    public C6HudController Hud;
    public Skimmer NearSkimmer;
    public Skimmer FarSkimmer;
    public GameObject OrientationHandle;
    public GameObject Geometry;
    public TrailRenderer TrailRenderer;
}

/// <summary>Builders for the C6 spawning fixture (shared across the test classes).</summary>
static class C6Fixture
{
    public static ThemeManagerDataContainerSO CreateTheme(out SO_MaterialSet jadeSet)
    {
        var theme = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
        theme.TeamMaterialSets = new Dictionary<Domains, SO_MaterialSet>();
        foreach (var domain in new[] { Domains.Jade, Domains.Ruby, Domains.Blue, Domains.Gold })
            theme.TeamMaterialSets[domain] = ScriptableObject.CreateInstance<SO_MaterialSet>();

        jadeSet = theme.TeamMaterialSets[Domains.Jade];
        jadeSet.ShipMaterial = new Material((Shader)null) { name = "jade-ship" };
        jadeSet.AOEExplosionMaterial = new Material((Shader)null) { name = "jade-aoe" };
        jadeSet.AOEConicExplosionMaterial = new Material((Shader)null) { name = "jade-conic" };
        jadeSet.SkimmerMaterial = new Material((Shader)null) { name = "jade-skim" };
        jadeSet.BlockSilhouettePrefab = new GameObject("jade-silhouette");
        return theme;
    }

    public static GameDataSO CreateGameData(ThemeManagerDataContainerSO theme,
        VesselClassType selected = VesselClassType.Sparrow)
    {
        NetworkManager.Singleton = null;
        var data = ScriptableObject.CreateInstance<GameDataSO>();
        data.ThemeManagerData = theme;
        data.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnPlayerNetworkSpawnedUlong = ScriptableObject.CreateInstance<ScriptableEventUlong>();
        data.OnVesselNetworkSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
        data.selectedVesselClass.Value = selected;
        return data;
    }

    /// <summary>
    /// Full prefab-shaped vessel template (C4VesselRig layout): the REAL
    /// VesselController + VesselStatus + the ten RequireComponent siblings +
    /// child near/far Skimmers + orientation handle + tinted trail, serialized
    /// refs wired. The root STAYS inactive — prefab semantics; the harness
    /// activates clones, never the template.
    /// </summary>
    public static C6VesselTemplate BuildVesselPrefab(string name, VesselClassType vesselType, GameDataSO gameData)
    {
        var t = new C6VesselTemplate();
        var go = new GameObject(name);
        t.Root = go;
        go.SetActive(false); // prefab: never live in the scene

        t.Root.AddComponent<VesselPrismController>();

        var resources = go.AddComponent<ResourceSystem>();
        resources.Resources = new List<Resource> { new Resource { Name = "Energy" } };

        go.AddComponent<VesselTransformer>();

        var pilot = go.AddComponent<AIPilot>();
        C6Reflect.Set(pilot, "cellData", ScriptableObject.CreateInstance<CellRuntimeDataSO>());
        C6Reflect.Set(pilot, "OnCellItemsUpdated", ScriptableObject.CreateInstance<ScriptableEventNoParam>());
        C6Reflect.Set(pilot, "abilities", new List<AIAbility>());

        go.AddComponent<SilhouetteController>();

        var cameraCustomizer = go.AddComponent<VesselCameraCustomizer>();
        C6Reflect.Set(cameraCustomizer, "OnInitializePlayerCamera",
            ScriptableObject.CreateInstance<ScriptableEventTransform>());

        go.AddComponent<C6VesselAnimation>();

        var actionHandler = go.AddComponent<R_VesselActionHandler>();
        C6Reflect.Set(actionHandler, "_onButtonPressed", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
        C6Reflect.Set(actionHandler, "_onButtonReleased", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
        C6Reflect.Set(actionHandler, "_resourceEventClassActions", new List<ResourceEventShipActionMapping>());

        t.Geometry = new GameObject("geometry");
        t.Geometry.transform.SetParent(go.transform);
        var customization = go.AddComponent<VesselCustomization>();
        C6Reflect.Set(customization, "_shipGeometries", new List<GameObject> { t.Geometry });

        go.AddComponent<R_ShipElementStatsHandler>();

        t.Hud = go.AddComponent<C6HudController>();
        t.Controller = go.AddComponent<VesselController>();
        C6Reflect.Set(t.Controller, "gameData", gameData);
        t.Status = go.AddComponent<VesselStatus>();

        t.NearSkimmer = NewChildSkimmer(go, "nearFieldSkimmer");
        t.FarSkimmer = NewChildSkimmer(go, "farFieldSkimmer");

        t.OrientationHandle = new GameObject("orientationHandle");
        t.OrientationHandle.transform.SetParent(go.transform);

        var trailGo = new GameObject("trail");
        trailGo.transform.SetParent(go.transform);
        t.TrailRenderer = trailGo.AddComponent<TrailRenderer>();
        trailGo.AddComponent<VesselTrailCustomization>();

        C6Reflect.Set(t.Status, "_shipInstance", t.Controller);
        C6Reflect.Set(t.Status, "vesselHUDController", t.Hud);
        C6Reflect.Set(t.Status, "orientationHandle", t.OrientationHandle);
        C6Reflect.Set(t.Status, "_name", name);
        C6Reflect.Set(t.Status, "vesselType", vesselType);
        C6Reflect.Set(t.Status, "_nearFieldSkimmer", t.NearSkimmer);
        C6Reflect.Set(t.Status, "_farFieldSkimmer", t.FarSkimmer);

        return t;
    }

    static Skimmer NewChildSkimmer(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        var skimmer = go.AddComponent<Skimmer>();
        C6Reflect.Set(skimmer, "onSkimmerShipImpact", ScriptableObject.CreateInstance<ScriptableEventString>());
        return skimmer;
    }

    /// <summary>Minimal template — just enough for VesselPrefabContainer matching
    /// (IVesselStatus.VesselType) and SpawnShip's TryGetComponent (IVessel).</summary>
    public static Transform BuildMinimalVesselPrefab(VesselClassType vesselType, GameDataSO gameData)
    {
        var go = new GameObject($"minimal-{vesselType}");
        go.SetActive(false);
        var controller = go.AddComponent<VesselController>();
        C6Reflect.Set(controller, "gameData", gameData);
        var status = go.AddComponent<VesselStatus>();
        C6Reflect.Set(status, "_shipInstance", controller);
        C6Reflect.Set(status, "vesselType", vesselType);
        return go.transform;
    }

    /// <summary>Player template ("prefab"): a GameObject carrying the REAL Player
    /// with the GameDataSO serialized ref wired — PlayerSpawner._playerPrefab holds
    /// the component, RequireInterface(IPlayer) style.</summary>
    public static Player BuildPlayerPrefab(GameDataSO gameData, string name = "playerPrefab")
    {
        var go = new GameObject(name);
        var player = go.AddComponent<Player>();
        C6Reflect.Set(player, "gameData", gameData);
        return player;
    }

    public static VesselPrefabContainer MakePrefabContainer(params Transform[] prefabs)
    {
        var container = ScriptableObject.CreateInstance<VesselPrefabContainer>();
        C6Reflect.Set(container, "_shipPrefabs", prefabs);
        return container;
    }

    public static PlayerDataService MakePlayerDataService()
    {
        var go = new GameObject("playerDataService");
        return go.AddComponent<PlayerDataService>();
    }

    /// <summary>Spawner pair on one GameObject, serialized refs wired, [Inject]
    /// fields (Container self-binding, GameDataSO, PlayerDataService) populated
    /// via the same InjectGameObject path the scenes use.</summary>
    public static (VesselSpawner vesselSpawner, PlayerSpawner playerSpawner, Container container) MakeSpawners(
        GameDataSO gameData, VesselPrefabContainer prefabContainer, Player playerPrefab,
        PlayerDataService playerDataService = null)
    {
        var container = new Container();
        container.RegisterValue(gameData);
        container.RegisterValue(playerDataService != null ? playerDataService : MakePlayerDataService());

        var go = new GameObject("spawners");
        var vesselSpawner = go.AddComponent<VesselSpawner>();
        C6Reflect.Set(vesselSpawner, "vesselPrefabContainer", prefabContainer);

        var playerSpawner = go.AddComponent<PlayerSpawner>();
        C6Reflect.Set(playerSpawner, "_playerPrefab", playerPrefab);
        C6Reflect.Set(playerSpawner, "vesselSpawner", vesselSpawner);

        container.InjectGameObject(go);
        return (vesselSpawner, playerSpawner, container);
    }

    public static Transform[] MakeSpawnPoints(params Vector3[] positions)
    {
        var transforms = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var go = new GameObject($"spawnPoint{i}");
            go.transform.position = positions[i];
            transforms[i] = go.transform;
        }
        return transforms;
    }

    public static IPlayer.InitializeData Human(string name, int avatarId = 0,
        VesselClassType vesselClass = VesselClassType.Sparrow) => new()
    {
        vesselClass = vesselClass,
        PlayerName = name,
        AvatarId = avatarId,
        AllowSpawning = true,
        IsAI = false,
    };

    public static IPlayer.InitializeData AI(string name, bool allowSpawning = true,
        VesselClassType vesselClass = VesselClassType.Sparrow) => new()
    {
        vesselClass = vesselClass,
        PlayerName = name,
        AvatarId = 0,
        AllowSpawning = allowSpawning,
        IsAI = true,
    };

    public static void StopAllSpawnLoops(GameDataSO gameData)
    {
        foreach (var p in gameData.Players)
            if (p?.Vessel is VesselController controller)
                controller.VesselStatus.VesselPrismController.StopSpawn();
    }
}

// ══════════════════════════════════════════════════════════════════════════
// Conformance freeze — serialized shape of the spawners
// ══════════════════════════════════════════════════════════════════════════

public class SpawnerConformanceC6Tests
{
    [Fact]
    public void PlayerSpawner_PlayerPrefabField_IsRequireInterfaceIPlayer()
    {
        var field = typeof(PlayerSpawner).GetField("_playerPrefab",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(typeof(Object), field.FieldType);

        var attr = field.GetCustomAttribute<RequireInterfaceAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(typeof(IPlayer), attr.requiredType);
    }

    [Fact]
    public void VesselSpawner_PrefabContainerField_KeepsFormerlySerializedName()
    {
        var field = typeof(VesselSpawner).GetField("vesselPrefabContainer",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(typeof(VesselPrefabContainer), field.FieldType);
        Assert.Equal("_shipPrefabContainer",
            field.GetCustomAttribute<FormerlySerializedAsAttribute>()!.oldName);
    }

    [Fact]
    public void Adapters_ExtendPlayerSpawnerAdapterBase_OnMonoBehaviour()
    {
        Assert.True(typeof(PlayerSpawnerAdapterBase).IsSubclassOf(typeof(MonoBehaviour)));
        Assert.True(typeof(PlayerSpawnerAdapterBase).IsAbstract);
        Assert.True(typeof(MiniGamePlayerSpawnerAdapter).IsSubclassOf(typeof(PlayerSpawnerAdapterBase)));
        Assert.True(typeof(VolumeTestPlayerSpawnerAdapter).IsSubclassOf(typeof(PlayerSpawnerAdapterBase)));
    }
}

// ══════════════════════════════════════════════════════════════════════════
// VesselSpawner.SpawnShip — prefab-clone fidelity (no template aliasing)
// ══════════════════════════════════════════════════════════════════════════

public class VesselSpawnerC6Tests : IDisposable
{
    public void Dispose()
    {
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        NetworkManager.Singleton = null;
    }

    [Fact]
    public void SpawnShip_ClonesPrefab_NoTemplateAliasing_SurveyCorruptionSentinel()
    {
        using var loop = new GameLoop();
        var theme = C6Fixture.CreateTheme(out _);
        var gameData = C6Fixture.CreateGameData(theme);
        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (vesselSpawner, _, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData));

        Assert.True(vesselSpawner.SpawnShip(VesselClassType.Sparrow, out IVessel vessel));
        Assert.NotNull(vessel);

        var cloneController = Assert.IsType<VesselController>(vessel);
        var cloneGo = cloneController.gameObject;
        Assert.NotSame(template.Root, cloneGo);
        Assert.Equal("sparrow(Clone)", cloneGo.name);

        // THE survey corruption sentinel: the clone's VesselStatus.Vessel is the
        // clone's own VesselController, not the template's.
        var cloneStatus = cloneGo.GetComponent<VesselStatus>();
        Assert.Same(cloneController, cloneStatus.Vessel);
        Assert.NotSame(template.Controller, cloneStatus.Vessel);

        // Child references remapped into the clone's own subtree.
        Assert.NotSame(template.NearSkimmer, cloneStatus.NearFieldSkimmer);
        Assert.NotSame(template.FarSkimmer, cloneStatus.FarFieldSkimmer);
        Assert.Same(cloneGo.transform, cloneStatus.NearFieldSkimmer.transform.parent);
        Assert.NotSame(template.OrientationHandle, cloneStatus.OrientationHandle);
        Assert.Same(cloneGo.transform, cloneStatus.OrientationHandle.transform.parent);
        Assert.NotSame(template.Hud, cloneStatus.VesselHUDController);

        // Template untouched (clone remap never mutates the source tree).
        Assert.Same(template.Controller, template.Status.Vessel);
        Assert.Same(template.NearSkimmer, template.Status.NearFieldSkimmer);

        // Prefab semantics: clone mirrors the template's inactive root.
        Assert.False(cloneGo.activeSelf);
        Assert.False(template.Root.activeSelf);
    }

    [Fact]
    public void SpawnShip_InjectsTheCloneHierarchy()
    {
        using var loop = new GameLoop();
        var gameData = C6Fixture.CreateGameData(C6Fixture.CreateTheme(out _));
        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (vesselSpawner, _, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData));

        Assert.True(vesselSpawner.SpawnShip(VesselClassType.Sparrow, out IVessel vessel));
        var cloneGo = ((VesselController)vessel).gameObject;

        // GameObjectInjector.InjectRecursive populated the clone's [Inject] fields.
        Assert.Same(gameData, C6Reflect.Get(cloneGo.GetComponent<SilhouetteController>(), "gameData"));
        Assert.Same(gameData, C6Reflect.Get(cloneGo.GetComponent<AIPilot>(), "gameData"));
    }

    [Fact]
    public void SpawnShip_MissingPrefab_ReturnsFalseWithNullVessel()
    {
        using var loop = new GameLoop();
        var gameData = C6Fixture.CreateGameData(C6Fixture.CreateTheme(out _));
        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (vesselSpawner, _, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData));

        Assert.False(vesselSpawner.SpawnShip(VesselClassType.Dolphin, out IVessel vessel));
        Assert.Null(vessel);
    }

    [Fact]
    public void SpawnShip_RandomAndAny_ResolveToAConcreteClass()
    {
        using var loop = new GameLoop();
        var gameData = C6Fixture.CreateGameData(C6Fixture.CreateTheme(out _));

        // Register a minimal prefab for EVERY concrete class so any random pick lands.
        var concreteClasses = Enum.GetValues(typeof(VesselClassType)).Cast<VesselClassType>()
            .Where(v => v is not VesselClassType.Random and not VesselClassType.Any)
            .ToArray();
        var prefabs = concreteClasses
            .Select(v => C6Fixture.BuildMinimalVesselPrefab(v, gameData))
            .ToArray();
        var (vesselSpawner, _, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(prefabs),
            C6Fixture.BuildPlayerPrefab(gameData));

        CosmicShore.Engine.Random.InitState(1234);
        foreach (var meta in new[] { VesselClassType.Random, VesselClassType.Any })
        {
            Assert.True(vesselSpawner.SpawnShip(meta, out IVessel vessel));
            var type = vessel.VesselStatus.VesselType;
            Assert.Contains(type, concreteClasses); // never Random/Any on the spawned vessel
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════
// PlayerSpawner.SpawnPlayerAndShip — pair wiring + orphan destroy
// ══════════════════════════════════════════════════════════════════════════

public class PlayerSpawnerC6Tests : IDisposable
{
    public void Dispose()
    {
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        NetworkManager.Singleton = null;
    }

    [Fact]
    public void SpawnPlayerAndShip_AllowSpawningFalse_ReturnsNullWithoutInstantiating()
    {
        using var loop = new GameLoop();
        var gameData = C6Fixture.CreateGameData(C6Fixture.CreateTheme(out _));
        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (_, playerSpawner, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData));

        int rootsBefore = loop.Scene.rootCount;
        var data = C6Fixture.Human("Nobody");
        data.AllowSpawning = false;

        Assert.Null(playerSpawner.SpawnPlayerAndShip(data));
        Assert.Equal(rootsBefore, loop.Scene.rootCount); // early-out before Instantiate
    }

    [Fact]
    public void SpawnPlayerAndShip_WiresThePair_InDocumentedOrder()
    {
        using var loop = new GameLoop();
        var theme = C6Fixture.CreateTheme(out var jadeSet);
        var gameData = C6Fixture.CreateGameData(theme);
        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (_, playerSpawner, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData));

        var player = playerSpawner.SpawnPlayerAndShip(C6Fixture.Human("Solo", avatarId: 3));

        Assert.NotNull(player);
        var vessel = Assert.IsType<VesselController>(player.Vessel);
        var status = vessel.gameObject.GetComponent<VesselStatus>();

        // Pair wiring: player → vessel (InitializeForSinglePlayerMode) and
        // vessel → player (vessel.Initialize).
        Assert.Same(player, status.Player);
        Assert.Same(vessel, player.Vessel);

        // InitializeForSinglePlayerMode effects (C5 contract reused verbatim).
        Assert.Equal(Domains.Jade, player.Domain);
        Assert.Equal("Solo", player.Name);
        Assert.Equal(3, player.AvatarId);
        Assert.False(player.IsInitializedAsAI);
        Assert.True(player.InputStatus.Paused);
        Assert.Equal("Solo", player.RoundStats.Name);

        // Ordering probe: the HUD initialized INSIDE vessel.Initialize and already
        // saw the resolved player name — InitializeForSinglePlayerMode ran first.
        var hud = (C6HudController)status.VesselHUDController;
        Assert.Equal(1, hud.InitializeCalls);
        Assert.Equal("Solo", hud.PlayerNameAtInitialize);
        Assert.Equal(1, hud.HideCalls);
        Assert.Equal(0, hud.SubscribeCalls); // unspawned single-player → not the local user

        // vessel.Initialize ran the full chain: Jade theme painted, ResetForPlay last.
        Assert.Same(jadeSet.ShipMaterial, status.ShipMaterial);
        Assert.Same(jadeSet.BlockSilhouettePrefab, hud.LastBlockPrefab);
        Assert.True(status.IsStationary);

        // The pair clones are their own objects — templates untouched.
        Assert.NotSame(template.Controller, vessel);
        Assert.Null(template.Status.Player);
    }

    [Fact]
    public void SpawnPlayerAndShip_VesselSpawnFailure_DestroysTheOrphanedPlayer()
    {
        using var loop = new GameLoop();
        var gameData = C6Fixture.CreateGameData(C6Fixture.CreateTheme(out _));
        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (_, playerSpawner, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData, "playerPrefab"));

        var rootsBefore = loop.Scene.GetRootGameObjects().ToArray();

        // Dolphin has no prefab → SpawnShip fails → the just-instantiated player
        // must not leak.
        Assert.Null(playerSpawner.SpawnPlayerAndShip(C6Fixture.Human("Orphan", vesselClass: VesselClassType.Dolphin)));

        var orphan = loop.Scene.GetRootGameObjects()
            .FirstOrDefault(go => !rootsBefore.Contains(go) && go.name == "playerPrefab(Clone)");
        Assert.NotNull(orphan); // instantiated...
        var orphanPlayer = orphan.GetComponent<Player>();
        Assert.NotNull(orphanPlayer);

        // Verbatim contract: `_playerPrefab` holds the Player COMPONENT
        // (RequireInterface wiring), so `Destroy(obj)` destroys the component —
        // exactly the original's behavior — via the deferred end-of-frame queue.
        loop.Tick(1f / 60f);
        Assert.True(orphanPlayer.IsDestroyed);
        Assert.Null(orphan.GetComponent<Player>());
    }

    [Fact]
    public void SpawnPlayerAndShip_AI_StartPlayerTogglesAutopilot_ResetTogglesItOff()
    {
        using var loop = new GameLoop();
        var gameData = C6Fixture.CreateGameData(C6Fixture.CreateTheme(out _));
        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (_, playerSpawner, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData));

        var bot = playerSpawner.SpawnPlayerAndShip(C6Fixture.AI("Bot-1"));
        Assert.NotNull(bot);
        Assert.True(bot.IsInitializedAsAI);

        var status = ((VesselController)bot.Vessel).gameObject.GetComponent<VesselStatus>();

        bot.StartPlayer();
        Assert.True(bot.IsActive);
        Assert.False(status.IsStationary);
        Assert.True(status.AIPilot.AutoPilotEnabled);
        Assert.True(bot.InputStatus.Paused); // AI keeps input paused

        // Stop the prism spawn loop before the test returns (C4 trap).
        status.VesselPrismController.StopSpawn();

        bot.ResetForPlay();
        Assert.False(bot.IsActive);
        Assert.False(status.AIPilot.AutoPilotEnabled);
        Assert.True(status.IsStationary);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// GameDataSO.AddPlayer effects through the spawned pair
// ══════════════════════════════════════════════════════════════════════════

public class GameDataAddPlayerC6Tests : IDisposable
{
    public void Dispose()
    {
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        NetworkManager.Singleton = null;
    }

    [Fact]
    public void AddPlayer_RegistersRoundStats_AssignsSpawnPose_LeavesLocalPlayerUnsetForUnspawned()
    {
        using var loop = new GameLoop();
        var gameData = C6Fixture.CreateGameData(C6Fixture.CreateTheme(out _));
        var spawnPositions = new[] { new Vector3(40f, 0f, 0f), new Vector3(-40f, 0f, 0f), new Vector3(0f, 0f, 40f) };
        gameData.SetSpawnPositions(C6Fixture.MakeSpawnPoints(spawnPositions));

        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (_, playerSpawner, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData));

        var player = playerSpawner.SpawnPlayerAndShip(C6Fixture.Human("Solo"));
        gameData.AddPlayer(player);

        Assert.Contains(player, gameData.Players);
        Assert.Contains(player.RoundStats, gameData.RoundStatsList);

        // Unspawned single-player Player is not IsLocalUser (no offline single-player
        // — the local user is the spawned Relay owner), so LocalPlayer stays unset.
        Assert.Null(gameData.LocalPlayer);

        // ResetForPlay ran, then a spawn pose landed on the vessel transformer.
        Assert.False(player.IsActive);
        var vesselPos = ((VesselController)player.Vessel).transform.position;
        Assert.Contains(spawnPositions, p => (p - vesselPos).sqrMagnitude < 1e-6f);
    }

    [Fact]
    public void AddPlayer_DeduplicatesByPlayerName()
    {
        using var loop = new GameLoop();
        var gameData = C6Fixture.CreateGameData(C6Fixture.CreateTheme(out _));
        gameData.SetSpawnPositions(C6Fixture.MakeSpawnPoints(Vector3.zero));
        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (_, playerSpawner, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData));

        var first = playerSpawner.SpawnPlayerAndShip(C6Fixture.Human("Twin"));
        var second = playerSpawner.SpawnPlayerAndShip(C6Fixture.Human("Twin"));
        gameData.AddPlayer(first);
        gameData.AddPlayer(second); // same display name → list entries deduplicated

        Assert.Single(gameData.Players);
        Assert.Single(gameData.RoundStatsList);
        Assert.Same(first, gameData.Players[0]);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// Adapters — OnInitializeGame flow, profile-name resolution, AI defaults
// ══════════════════════════════════════════════════════════════════════════

public class SpawnerAdapterC6Tests : IDisposable
{
    public void Dispose()
    {
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        NetworkManager.Singleton = null;
    }

    static T MakeAdapter<T>(GameDataSO gameData, PlayerSpawner playerSpawner, Container container,
        IPlayer.InitializeData[] initializeDatas, Transform[] spawnTransforms,
        bool spawnAIAtStart = false) where T : PlayerSpawnerAdapterBase
    {
        var go = new GameObject("adapter");
        go.SetActive(false); // configure-before-activation: Start must see wired fields
        var adapter = go.AddComponent<T>();
        C6Reflect.Set(adapter, "_playerSpawner", playerSpawner);
        C6Reflect.Set(adapter, "_initializeDatas", initializeDatas);
        C6Reflect.Set(adapter, "_spawnTransforms", spawnTransforms);
        if (adapter is MiniGamePlayerSpawnerAdapter)
            C6Reflect.Set(adapter, "_spawnAIAtStart", spawnAIAtStart);
        container.InjectGameObject(go); // [Inject] GameDataSO (+ PlayerDataService)
        go.SetActive(true);
        return adapter;
    }

    static (GameDataSO gameData, PlayerSpawner playerSpawner, Container container) MakePipeline(
        PlayerDataService service = null)
    {
        var gameData = C6Fixture.CreateGameData(C6Fixture.CreateTheme(out _));
        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (_, playerSpawner, container) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData), service);
        return (gameData, playerSpawner, container);
    }

    static PlayerDataService MakeProfileService(string displayName, int avatarId, bool initialized)
    {
        var service = C6Fixture.MakePlayerDataService();
        typeof(PlayerDataService).GetProperty("CurrentProfile")!
            .SetValue(service, new PlayerProfileData { displayName = displayName, avatarId = avatarId });
        typeof(PlayerDataService).GetProperty("IsInitialized")!.SetValue(service, initialized);
        return service;
    }

    [Fact]
    public void MiniGameAdapter_OnInitializeGame_SpawnsProfileHuman_PlusAllowedAIDefaults()
    {
        using var loop = new GameLoop();
        var service = MakeProfileService("CloudName", 7, initialized: true);
        var (gameData, playerSpawner, container) = MakePipeline(service);

        MakeAdapter<MiniGamePlayerSpawnerAdapter>(gameData, playerSpawner, container,
            new[] { C6Fixture.AI("Bot-A"), C6Fixture.AI("Bot-B", allowSpawning: false) },
            C6Fixture.MakeSpawnPoints(new Vector3(10f, 0f, 0f), new Vector3(-10f, 0f, 0f)));

        loop.Tick(1f / 60f); // Start(): subscribe + AddSpawnPosesToGameData
        Assert.Empty(gameData.Players);
        Assert.Equal(2, gameData.SpawnPoses.Length);

        gameData.InitializeGame(); // raises OnInitializeGame → adapter spawns

        Assert.Equal(2, gameData.Players.Count); // human + the one allowed AI
        var human = gameData.Players[0];
        Assert.Equal("CloudName", human.Name);  // tier 1: PlayerDataService profile
        Assert.Equal(7, human.AvatarId);
        Assert.False(human.IsInitializedAsAI);
        var bot = gameData.Players[1];
        Assert.Equal("Bot-A", bot.Name);
        Assert.True(bot.IsInitializedAsAI);
        Assert.DoesNotContain(gameData.Players, p => p.Name == "Bot-B"); // AllowSpawning=false

        C6Fixture.StopAllSpawnLoops(gameData);
    }

    [Fact]
    public void MiniGameAdapter_NameFallback_GameDataCache_WhenServiceNotInitialized()
    {
        using var loop = new GameLoop();
        var service = MakeProfileService("ignored", 9, initialized: false);
        var (gameData, playerSpawner, container) = MakePipeline(service);
        gameData.LocalPlayerDisplayName = "CachedName";
        gameData.LocalPlayerAvatarId = 4;

        MakeAdapter<MiniGamePlayerSpawnerAdapter>(gameData, playerSpawner, container,
            Array.Empty<IPlayer.InitializeData>(), C6Fixture.MakeSpawnPoints(Vector3.zero));
        loop.Tick(1f / 60f);

        gameData.InitializeGame();

        var human = Assert.Single(gameData.Players);
        Assert.Equal("CachedName", human.Name); // tier 2: GameDataSO cache
        Assert.Equal(4, human.AvatarId);

        C6Fixture.StopAllSpawnLoops(gameData);
    }

    [Fact]
    public void MiniGameAdapter_NameFallback_HumanJadeDefault_WhenNothingAvailable()
    {
        using var loop = new GameLoop();
        var service = MakeProfileService(null, 0, initialized: false);
        var (gameData, playerSpawner, container) = MakePipeline(service);

        MakeAdapter<MiniGamePlayerSpawnerAdapter>(gameData, playerSpawner, container,
            Array.Empty<IPlayer.InitializeData>(), C6Fixture.MakeSpawnPoints(Vector3.zero));
        loop.Tick(1f / 60f);

        gameData.InitializeGame();

        var human = Assert.Single(gameData.Players);
        Assert.Equal("HumanJade", human.Name); // hardcoded last resort
        Assert.Equal(0, human.AvatarId);

        C6Fixture.StopAllSpawnLoops(gameData);
    }

    [Fact]
    public void MiniGameAdapter_SpawnAIAtStart_PreSpawnsDefaults_InitializeGameDedupes()
    {
        using var loop = new GameLoop();
        var service = MakeProfileService("CloudName", 7, initialized: true);
        var (gameData, playerSpawner, container) = MakePipeline(service);

        MakeAdapter<MiniGamePlayerSpawnerAdapter>(gameData, playerSpawner, container,
            new[] { C6Fixture.AI("Bot-A") }, C6Fixture.MakeSpawnPoints(Vector3.zero),
            spawnAIAtStart: true);

        loop.Tick(1f / 60f); // Start(): AI defaults spawn immediately

        var bot = Assert.Single(gameData.Players);
        Assert.Equal("Bot-A", bot.Name);

        gameData.InitializeGame(); // human + AI defaults again — AddPlayer dedups by name

        Assert.Equal(2, gameData.Players.Count);
        Assert.Contains(gameData.Players, p => p.Name == "CloudName");
        Assert.Single(gameData.Players, p => p.Name == "Bot-A");

        C6Fixture.StopAllSpawnLoops(gameData);
    }

    [Fact]
    public void VolumeTestAdapter_Start_SpawnsDefaultsAndActivatesThem()
    {
        using var loop = new GameLoop();
        var (gameData, playerSpawner, container) = MakePipeline();

        MakeAdapter<VolumeTestPlayerSpawnerAdapter>(gameData, playerSpawner, container,
            new[] { C6Fixture.AI("Bot-A"), C6Fixture.AI("Bot-B") },
            C6Fixture.MakeSpawnPoints(new Vector3(5f, 0f, 0f), new Vector3(-5f, 0f, 0f)));

        loop.Tick(1f / 60f); // Start(): InitializeGame + spawn defaults + SetPlayersActive

        Assert.Equal(2, gameData.Players.Count);
        foreach (var bot in gameData.Players)
        {
            Assert.True(bot.IsInitializedAsAI);
            Assert.True(bot.IsActive); // SetPlayersActive → StartPlayer
            var status = ((VesselController)bot.Vessel).gameObject.GetComponent<VesselStatus>();
            Assert.False(status.IsStationary);
            Assert.True(status.AIPilot.AutoPilotEnabled);
        }

        // Cancel every StartVessel-launched spawn loop before the test returns,
        // and toggle the AI pilots back off (ResetForPlay does both legs).
        C6Fixture.StopAllSpawnLoops(gameData);
        foreach (var bot in gameData.Players)
            bot.ResetForPlay();
    }
}

// ══════════════════════════════════════════════════════════════════════════
// Headless motion — StartPlayer drives the clone through VesselTransformer
// ══════════════════════════════════════════════════════════════════════════

public class SpawnedVesselMotionC6Tests : IDisposable
{
    public void Dispose()
    {
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        NetworkManager.Singleton = null;
    }

    [Fact]
    public void StartPlayer_TickedFrames_MoveTheClone_ResetForPlayFreezesIt()
    {
        using var loop = new GameLoop();
        var gameData = C6Fixture.CreateGameData(C6Fixture.CreateTheme(out _));
        gameData.SetSpawnPositions(C6Fixture.MakeSpawnPoints(Vector3.zero));
        var template = C6Fixture.BuildVesselPrefab("sparrow", VesselClassType.Sparrow, gameData);
        var (_, playerSpawner, _) = C6Fixture.MakeSpawners(
            gameData, C6Fixture.MakePrefabContainer(template.Root.transform),
            C6Fixture.BuildPlayerPrefab(gameData));

        var player = playerSpawner.SpawnPlayerAndShip(C6Fixture.Human("Solo"));
        gameData.AddPlayer(player);

        // Scene placement: prefab clones come out matching the template's inactive
        // root — the harness activates them (Awake/OnEnable run with copied fields).
        var vesselGo = ((VesselController)player.Vessel).gameObject;
        vesselGo.SetActive(true);
        var status = vesselGo.GetComponent<VesselStatus>();

        player.StartPlayer();
        Assert.True(player.IsActive);
        Assert.False(status.IsStationary);
        Assert.False(player.InputStatus.Paused); // human path unpauses input
        Assert.True((bool)C6Reflect.Get(status.VesselPrismController, "spawnerEnabled"));
        status.VesselPrismController.StopSpawn(); // end the async spawn loop (C4 trap)

        var before = vesselGo.transform.position;
        loop.Run(30, 1f / 60f);
        var moved = (vesselGo.transform.position - before).magnitude;
        Assert.True(moved > 0.5f, $"vessel should move under VesselTransformer; moved {moved}");

        player.ResetForPlay();
        Assert.True(status.IsStationary);
        Assert.False(player.IsActive);

        var frozen = vesselGo.transform.position;
        loop.Run(10, 1f / 60f);
        Assert.Equal(frozen, vesselGo.transform.position); // stationary vessel holds still
    }
}
