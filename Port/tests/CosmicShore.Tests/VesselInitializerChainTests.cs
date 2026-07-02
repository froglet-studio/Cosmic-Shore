using System;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Xunit;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Vessel-initializer (menu-swap) chain — ServerPlayerVesselInitializer +
// ClientPlayerVesselInitializer + MenuServerPlayerVesselInitializer ported
// verbatim, driven end-to-end in host-mode through the REAL pipeline:
//
//   Player.OnNetworkSpawn → OnPlayerNetworkSpawnedUlong → NetcodeHooks →
//   ServerPlayerVesselInitializer.ProcessPreExistingPlayers →
//   preSpawnDelay → SpawnVesselForPlayer (prefab NetworkObject →
//   Instantiate → InjectRecursive → SpawnWithOwnership → NetVesselId) →
//   ClientPlayerVesselInitializer.InitializePair → postSpawnDelay →
//   NotifyClients → Menu.ActivateAutopilot
//
// plus the swap: RequestSwap → DespawnVessel(old) → SpawnVesselForPlayer(target)
// → ReplaceVesselForPlayer/ReInitializePair → SetPose(snapshot) → autopilot.
//
// Vessel prefabs are the C6 full templates (real VesselController/VesselStatus
// rig) with an authored engine NetworkObject + hull collider, matching the
// original prefab layout. Timing: the initializer's pre/postSpawnDelayMs run on
// GameTask.Delay — tests tick the loop through them (no dangling async-voids).
// ─────────────────────────────────────────────────────────────────────────────

// ── engine surface added for the arc: the NetworkObject component ────────────

class NetObjTestBehaviourA : NetworkBehaviour
{
    public int SpawnCalls, DespawnCalls;
    public override void OnNetworkSpawn() => SpawnCalls++;
    public override void OnNetworkDespawn() => DespawnCalls++;
}

class NetObjTestBehaviourB : NetworkBehaviour
{
    public int SpawnCalls, DespawnCalls;
    public override void OnNetworkSpawn() => SpawnCalls++;
    public override void OnNetworkDespawn() => DespawnCalls++;
}

public class NetworkObjectComponentTests : IDisposable
{
    readonly GameLoop loop = new();

    public void Dispose()
    {
        NetworkManager.Singleton = null;
        loop.Dispose();
    }

    NetworkManager MakeNetworkManager(ulong localClientId = 0)
    {
        var nm = new GameObject("nm").AddComponent<NetworkManager>();
        nm.LocalClientId = localClientId;
        NetworkManager.Singleton = nm;
        return nm;
    }

    [Fact]
    public void SpawnWithOwnership_SpawnsEveryBehaviour_WithOwnerAndRoleFlags()
    {
        MakeNetworkManager(localClientId: 0);

        var go = new GameObject("vessel");
        var a = go.AddComponent<NetObjTestBehaviourA>();
        var b = go.AddComponent<NetObjTestBehaviourB>();
        var netObj = go.AddComponent<NetworkObject>();

        // Remote client 2 owns the object: spawned on the host, IsOwner false.
        netObj.SpawnWithOwnership(2, destroyWithScene: true);

        Assert.True(netObj.IsSpawned);
        Assert.True(netObj.DestroyWithScene);
        foreach (var behaviour in new NetworkBehaviour[] { a, b })
        {
            Assert.True(behaviour.IsSpawned);
            Assert.True(behaviour.IsServer);   // host-mode role flags from the manager
            Assert.True(behaviour.IsClient);
            Assert.False(behaviour.IsOwner);
            Assert.Equal(2UL, behaviour.OwnerClientId);
        }
        Assert.Equal(1, a.SpawnCalls);
        Assert.Equal(1, b.SpawnCalls);

        // Original object-id contract: every behaviour of the object shares ONE id.
        Assert.NotEqual(0UL, netObj.NetworkObjectId);
        Assert.Equal(netObj.NetworkObjectId, a.NetworkObjectId);
        Assert.Equal(netObj.NetworkObjectId, b.NetworkObjectId);

        // Locally-owned spawn: IsOwner true (clientId == LocalClientId).
        var ownGo = new GameObject("own");
        var own = ownGo.AddComponent<NetObjTestBehaviourA>();
        ownGo.AddComponent<NetworkObject>().SpawnWithOwnership(0);
        Assert.True(own.IsOwner);
    }

    [Fact]
    public void NetworkObjectId_ReportsTheFirstSpawnedBehaviour()
    {
        var go = new GameObject("multi");
        var a = go.AddComponent<NetObjTestBehaviourA>();
        var b = go.AddComponent<NetObjTestBehaviourB>();
        var netObj = go.AddComponent<NetworkObject>();

        Assert.Equal(0UL, netObj.NetworkObjectId); // never spawned sentinel

        // Only the second behaviour spawned (the C4 rig shape): its id is the object id.
        b.Spawn();
        Assert.Equal(b.NetworkObjectId, netObj.NetworkObjectId);

        // Whole-object spawn: the primary (first) behaviour's id wins.
        a.Spawn();
        Assert.Equal(a.NetworkObjectId, netObj.NetworkObjectId);
    }

    [Fact]
    public void Despawn_DespawnsAllBehaviours_AndDestroysOnRequest()
    {
        var go = new GameObject("pair");
        var a = go.AddComponent<NetObjTestBehaviourA>();
        var b = go.AddComponent<NetObjTestBehaviourB>();
        var netObj = go.AddComponent<NetworkObject>();
        netObj.Spawn();

        netObj.Despawn(true);

        Assert.False(a.IsSpawned);
        Assert.False(b.IsSpawned);
        Assert.Equal(1, a.DespawnCalls);
        Assert.Equal(1, b.DespawnCalls);
        Assert.False(go == null);   // destroy is deferred to end of frame
        loop.Tick(0.016f);
        Assert.True(go == null);
    }

    [Fact]
    public void SpawnManager_TracksSpawnedObjects_WhileASingletonIsAssigned()
    {
        var nm = MakeNetworkManager();

        var go = new GameObject("tracked");
        var behaviour = go.AddComponent<NetObjTestBehaviourA>();
        var netObj = go.AddComponent<NetworkObject>();

        netObj.SpawnWithOwnership(0);
        Assert.Same(netObj, nm.SpawnManager.SpawnedObjects[behaviour.NetworkObjectId]);

        netObj.Despawn(false);
        Assert.False(nm.SpawnManager.SpawnedObjects.ContainsKey(behaviour.NetworkObjectId));
    }

    [Fact]
    public void BehaviourHandle_ResolvesTheAuthoredComponent()
    {
        var go = new GameObject("authored");
        var netObj = go.AddComponent<NetworkObject>();
        var behaviour = go.AddComponent<NetObjTestBehaviourA>();

        // The E12 handle property now resolves the authored component (one per object).
        Assert.Same(netObj, behaviour.NetworkObject);
        Assert.Same(behaviour.NetworkObject, behaviour.NetworkObject);
    }
}

// ── shared rig (also used by the ToySystemTests end-to-end vessel-changer) ───

static class MenuSwapRig
{
    public sealed class Rig
    {
        public NetworkManager Nm;
        public GameDataSO GameData;
        public ThemeManagerDataContainerSO Theme;
        public VesselPrefabContainer PrefabContainer;
        public GameObject SpawnerGo;
        public NetcodeHooks Hooks;
        public ClientPlayerVesselInitializer Client;
        public MenuServerPlayerVesselInitializer Menu;
        public NetworkObject SpawnerNetObj;
        public Player Player;
        public C6VesselTemplate MantaTemplate;
        public C6VesselTemplate DolphinTemplate;
        public int ClientReadyCount;
    }

    /// <summary>
    /// Host-mode scene layout: NetworkManager singleton, GameDataSO + theme, two full
    /// C6 vessel prefabs (Manta/Dolphin) with authored NetworkObject + hull collider,
    /// the initializer trio on one GameObject (NetcodeHooks + ClientPlayerVesselInitializer
    /// + MenuServerPlayerVesselInitializer, DI-injected), and the real local human Player.
    /// Call <see cref="SpawnChain"/> then tick ~1s to run the full spawn pipeline.
    /// </summary>
    public static Rig Build()
    {
        var rig = new Rig();

        rig.Theme = C6Fixture.CreateTheme(out _);
        rig.GameData = C6Fixture.CreateGameData(rig.Theme, selected: VesselClassType.Manta);
        rig.GameData.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        rig.GameData.OnClientReady.OnRaised += () => rig.ClientReadyCount++;

        // CreateGameData nulls the Singleton — assign the host-mode manager after.
        rig.Nm = new GameObject("network-manager").AddComponent<NetworkManager>();
        NetworkManager.Singleton = rig.Nm;

        // Vessel prefabs: full templates + the authored NetworkObject the spawner looks
        // up + a hull collider (non-trigger) like the real vessel prefabs carry.
        rig.MantaTemplate = C6Fixture.BuildVesselPrefab("manta", VesselClassType.Manta, rig.GameData);
        rig.DolphinTemplate = C6Fixture.BuildVesselPrefab("dolphin", VesselClassType.Dolphin, rig.GameData);
        foreach (var template in new[] { rig.MantaTemplate, rig.DolphinTemplate })
        {
            template.Root.AddComponent<NetworkObject>();
            template.Root.AddComponent<SphereCollider>().radius = 1f;
        }
        rig.PrefabContainer = C6Fixture.MakePrefabContainer(
            rig.MantaTemplate.Root.transform, rig.DolphinTemplate.Root.transform);

        // Initializer trio on one GameObject (the Menu_Main layout).
        rig.SpawnerGo = new GameObject("vessel-initializer");
        rig.Hooks = rig.SpawnerGo.AddComponent<NetcodeHooks>();
        rig.Client = rig.SpawnerGo.AddComponent<ClientPlayerVesselInitializer>();
        rig.Menu = rig.SpawnerGo.AddComponent<MenuServerPlayerVesselInitializer>();
        rig.SpawnerNetObj = rig.SpawnerGo.AddComponent<NetworkObject>();
        C6Reflect.Set(rig.Menu, "clientPlayerVesselInitializer", rig.Client);
        C6Reflect.Set(rig.Menu, "vesselPrefabContainer", rig.PrefabContainer);
        C6Reflect.Set(rig.Client, "themeManagerData", rig.Theme);

        var container = new Container();
        container.RegisterValue(rig.GameData);
        container.InjectGameObject(rig.SpawnerGo); // [Inject] GameDataSO + Container

        // The real local human Player (host-mode owner).
        AuthenticationService.Instance.PlayerName = "Pilot#0001";
        rig.Player = C6Fixture.BuildPlayerPrefab(rig.GameData, "player");
        return rig;
    }

    /// <summary>Player spawns first (Auth-scene order), then the initializer object —
    /// ProcessPreExistingPlayers catches the already-spawned Player.</summary>
    public static void SpawnChain(Rig rig)
    {
        rig.Player.Spawn();
        rig.SpawnerNetObj.Spawn();
    }

    public static void RunSeconds(GameLoop loop, float seconds, float dt = 0.05f)
    {
        int frames = (int)MathF.Ceiling(seconds / dt) + 1;
        for (int i = 0; i < frames; i++) loop.Tick(dt);
    }

    /// <summary>Stops every live prism spawn loop (the C4/C6 async-void trap) and resets statics.</summary>
    public static void Teardown(Rig rig)
    {
        C6Fixture.StopAllSpawnLoops(rig.GameData);
        typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
        AuthenticationService.Reset();
        NetworkManager.Singleton = null;
    }
}

// ── the chain itself ──────────────────────────────────────────────────────────

public class VesselInitializerChainTests : IDisposable
{
    readonly GameLoop loop = new();
    MenuSwapRig.Rig rig;

    public void Dispose()
    {
        if (rig != null) MenuSwapRig.Teardown(rig);
        loop.Dispose();
    }

    [Fact]
    public void HostSpawnChain_SpawnsVessel_InitializesPair_AndActivatesAutopilot()
    {
        rig = MenuSwapRig.Build();

        // A stale non-menu domain: the menu override must reset it server-side pre-spawn.
        rig.Player.Spawn();
        rig.Player.NetDomain.Value = Domains.Ruby;
        rig.SpawnerNetObj.Spawn();

        MenuSwapRig.RunSeconds(loop, 1.0f);

        // Pair initialized through the real pipeline.
        var vessel = Assert.IsType<VesselController>(rig.Player.Vessel);
        Assert.Equal(VesselClassType.Manta, vessel.VesselStatus.VesselType);
        Assert.Same(rig.Player, vessel.VesselStatus.Player);
        Assert.Same(rig.Player, rig.GameData.LocalPlayer);
        Assert.Contains(vessel, rig.GameData.Vessels);
        Assert.Equal(1, rig.ClientReadyCount);

        // Server wrote the vessel id back onto the player.
        Assert.NotEqual(0UL, rig.Player.NetVesselId.Value);
        Assert.Equal(vessel.NetworkObjectId, rig.Player.NetVesselId.Value);
        Assert.Equal(rig.Player.NetVesselId.Value, rig.Player.VesselNetId);

        // Menu domain reset (the ONLY menu domain reset — server-side, pre-spawn).
        Assert.Equal(Domains.Jade, rig.Player.NetDomain.Value);
        Assert.Equal(Domains.Jade, rig.Player.Domain);

        // Menu vessels survive scene loads (despawn is explicit).
        Assert.False(vessel.NetworkObject.DestroyWithScene);

        // ActivateAutopilot: vessel started, AI pilot on, input paused.
        Assert.True(rig.Player.IsActive);
        Assert.True(vessel.VesselStatus.AutoPilotEnabled);
        Assert.True(rig.Player.InputStatus.Paused);
    }

    [Fact]
    public void RequestSwap_DespawnsOldVessel_SpawnsTarget_AndReinitializesThePair()
    {
        rig = MenuSwapRig.Build();
        MenuSwapRig.SpawnChain(rig);
        MenuSwapRig.RunSeconds(loop, 1.0f);

        var oldVessel = Assert.IsType<VesselController>(rig.Player.Vessel);
        var oldGo = oldVessel.gameObject;
        ulong oldVesselId = oldVessel.NetworkObjectId;

        // Fly somewhere distinctive: the swap snapshots and restores the pose.
        var pose = new Vector3(40f, -15f, 260f);
        oldVessel.transform.position = pose;

        rig.Menu.RequestSwap(VesselClassType.Dolphin);
        MenuSwapRig.RunSeconds(loop, 1.0f);

        // The pair now runs the target class, re-initialized through ReInitializePair.
        var newVessel = Assert.IsType<VesselController>(rig.Player.Vessel);
        Assert.NotSame(oldVessel, newVessel);
        Assert.Equal(VesselClassType.Dolphin, newVessel.VesselStatus.VesselType);
        Assert.Same(rig.Player, newVessel.VesselStatus.Player);
        Assert.False(rig.Menu.IsSwapping);

        // Ownership + id rewire.
        Assert.True(newVessel.IsSpawned);
        Assert.Equal(newVessel.NetworkObjectId, rig.Player.NetVesselId.Value);
        Assert.NotEqual(oldVesselId, newVessel.NetworkObjectId);

        // Old vessel despawned + destroyed; registry swapped over.
        Assert.True(oldGo == null);
        Assert.DoesNotContain((IVessel)oldVessel, rig.GameData.Vessels);
        Assert.Contains((IVessel)newVessel, rig.GameData.Vessels);

        // Pose snapshot applied to the replacement.
        Assert.True((newVessel.transform.position - pose).magnitude < 0.01f);

        // Autopilot re-engaged on the swapped vessel.
        Assert.True(newVessel.VesselStatus.AutoPilotEnabled);
        Assert.True(rig.Player.InputStatus.Paused);

        // RequestSwap was invoked directly (outside a Tick), so the new vessel's prism
        // spawn loop async-void captured xunit's AsyncTestSyncContext — stop it BEFORE
        // the method returns (the C4/C6 trap; the runner waits on it ahead of Dispose).
        C6Fixture.StopAllSpawnLoops(rig.GameData);
    }

    [Fact]
    public void RequestSwap_ToTheCurrentClass_IsANoOp()
    {
        rig = MenuSwapRig.Build();
        MenuSwapRig.SpawnChain(rig);
        MenuSwapRig.RunSeconds(loop, 1.0f);

        var vessel = rig.Player.Vessel;
        rig.Menu.RequestSwap(VesselClassType.Manta); // already flying a Manta
        MenuSwapRig.RunSeconds(loop, 0.5f);

        Assert.Same(vessel, rig.Player.Vessel);
        Assert.False(rig.Menu.IsSwapping);
    }
}
