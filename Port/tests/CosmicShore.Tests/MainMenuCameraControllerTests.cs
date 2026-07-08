using System;
using System.Reflection;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// MainMenuCameraController — the camera arc's LIVE surface: per-mode transition
// durations (the read MenuCrystalClickHandler un-carries this iteration), the
// SOAP-driven activation flow through the CameraManager shell's observable
// state mirror, the transform-side crystal-orbit rig (follow-target placement +
// per-frame orbit math + RotateAroundOrigin arbitration), and the freestyle
// enter/exit round trip resolving through upstream's own no-bridge fallbacks.
// ─────────────────────────────────────────────────────────────────────────────

public class MainMenuCameraControllerTests : IDisposable
{
    readonly GameLoop loop = new(nameof(MainMenuCameraControllerTests));

    public MainMenuCameraControllerTests() => ClearCameraManagerSingleton();

    public void Dispose()
    {
        ClearCameraManagerSingleton();
        loop.Dispose();
    }

    static void ClearCameraManagerSingleton()
        => typeof(Singleton<CameraManager>).GetProperty("Instance")!.SetValue(null, null);

    static void Set(object target, string field, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{field}' not found on {target.GetType().Name}.");
    }

    static T Get<T>(object target, string field)
        => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(target);

    sealed class Rig
    {
        public MainMenuCameraController Controller;
        public CameraManager Cameras;
        public Transform FollowTarget;
        public RotateAroundOrigin Rotator;
        public MenuFreestyleEventsContainerSO Freestyle;
        public CellRuntimeDataSO CellData;
        public GameDataSO GameData;
        public Crystal Crystal;
    }

    Rig MakeRig()
    {
        // CameraManager with the "Main Menu Follow Target" child the controller caches.
        // No "CM PlayerCam" child — the exit transition takes upstream's own
        // no-player-camera branch (ActivateMenuCameraImmediate).
        var cmGo = new GameObject("camera-manager");
        var cameras = cmGo.AddComponent<CameraManager>();
        // Scene layout: "CM Main Menu" must exist for CacheMenuVCam to proceed to the
        // follow-target lookup (upstream early-returns without it).
        var menuVCamGo = new GameObject("CM Main Menu");
        menuVCamGo.transform.SetParent(cmGo.transform, false);
        var followGo = new GameObject("Main Menu Follow Target");
        followGo.transform.SetParent(cmGo.transform, false);
        var rotator = followGo.AddComponent<RotateAroundOrigin>();

        var freestyle = ScriptableObject.CreateInstance<MenuFreestyleEventsContainerSO>();
        freestyle.OnGameStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        freestyle.OnMenuStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        var cellData = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
        cellData.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        var crystalGo = new GameObject("crystal");
        crystalGo.SetActive(false); // component present, no lifecycle ticking needed
        var crystal = crystalGo.AddComponent<Crystal>();
        crystalGo.transform.position = new Vector3(100f, 20f, -40f);
        cellData.Crystals.Add(crystal);

        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        var go = new GameObject("main-menu-camera-controller");
        var controller = go.AddComponent<MainMenuCameraController>();
        Set(controller, "_freestyleEvents", freestyle);
        Set(controller, "_cellData", cellData);
        Set(controller, "_gameData", gameData);

        loop.Tick(1f / 60f); // Awake (CameraManager singleton) + Start (cache + subscribe)

        return new Rig
        {
            Controller = controller, Cameras = cameras,
            FollowTarget = followGo.transform, Rotator = rotator,
            Freestyle = freestyle, CellData = cellData, GameData = gameData,
            Crystal = crystal,
        };
    }

    /// <summary>Local player stub whose vessel carries a real VesselStatus + follow target.</summary>
    static (ToyStubPlayer player, Transform followTarget) MakeLocalPlayer()
    {
        var vesselGo = new GameObject("stub-vessel");
        var vessel = vesselGo.AddComponent<ToyStubVessel>();
        vessel.Status = vesselGo.AddComponent<VesselStatus>();
        vessel.Status.CameraFollowTarget = new GameObject("vessel-follow-target").transform;
        return (new ToyStubPlayer { VesselRef = vessel }, vessel.Status.CameraFollowTarget);
    }

    [Fact]
    public void ActiveTransitionDuration_TracksTheMode()
    {
        var rig = MakeRig();

        // CrystalOrbit (default) reads the long cinematic duration…
        Assert.Equal(2f, rig.Controller.ActiveTransitionDuration);

        // …every vessel-relative mode reads the short tighten-in duration.
        rig.Controller.Mode = MenuCameraMode.VesselFollow;
        Assert.Equal(0.5f, rig.Controller.ActiveTransitionDuration);
        rig.Controller.Mode = MenuCameraMode.VesselChaseTight;
        Assert.Equal(0.5f, rig.Controller.ActiveTransitionDuration);
        rig.Controller.Mode = MenuCameraMode.VesselTopDownPan;
        Assert.Equal(0.5f, rig.Controller.ActiveTransitionDuration);

        // The setter re-activated the menu camera family for the new mode.
        Assert.Equal(CameraManager.ShellCameraState.MainMenu, rig.Cameras.ActiveShellState);
    }

    [Fact]
    public void MenuCrystalClickHandler_ReadsTheControllerDuration()
    {
        // The un-carried pair: with a controller wired, CurrentTransitionDuration
        // returns its per-mode ActiveTransitionDuration; without one, the serialized
        // fallback (2f).
        var rig = MakeRig();
        rig.Controller.Mode = MenuCameraMode.VesselFollow;

        var handlerGo = new GameObject("click-handler");
        handlerGo.SetActive(false); // no lifecycle — we only invoke the private read
        var handler = handlerGo.AddComponent<MenuCrystalClickHandler>();
        var read = typeof(MenuCrystalClickHandler).GetMethod(
            "CurrentTransitionDuration", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(2f, (float)read.Invoke(handler, null)); // no controller → fallback

        Set(handler, "cameraController", rig.Controller);
        Assert.Equal(0.5f, (float)read.Invoke(handler, null)); // controller wins, per-mode
    }

    [Fact]
    public void ClientReady_ActivatesTheMenuCameraFamily()
    {
        var rig = MakeRig();
        Assert.Equal(CameraManager.ShellCameraState.None, rig.Cameras.ActiveShellState);

        rig.GameData.InvokeClientReady();

        Assert.Equal(CameraManager.ShellCameraState.MainMenu, rig.Cameras.ActiveShellState);
        Assert.False(Get<bool>(rig.Controller, "_isInFreestyle"));
    }

    [Fact]
    public void CrystalSpawn_PlacesTheOrbitRig_AndUpdateOrbitsIt()
    {
        var rig = MakeRig();
        Assert.True(rig.Rotator.enabled);
        Assert.NotNull(Get<Transform>(rig.Controller, "_menuFollowTarget")); // Start cached the rig
        Assert.NotNull(rig.CellData.CrystalTransform);                       // crystal resolvable

        rig.CellData.OnCrystalSpawned.Raise();

        // Follow target parked at orbit radius behind the crystal; the world-origin
        // rotator is disabled so the controller's own crystal-pivot orbit takes over.
        var pivot = rig.Crystal.transform.position;
        Assert.Equal(pivot + Vector3.back * 80f, rig.FollowTarget.position);
        Assert.False(rig.Rotator.enabled);

        // Ticking in CrystalOrbit mode orbits the follow target around the crystal:
        // the position moves but the orbit radius is preserved.
        var before = rig.FollowTarget.position;
        for (int i = 0; i < 30; i++) loop.Tick(1f / 60f);
        var after = rig.FollowTarget.position;

        Assert.NotEqual(before, after);
        Assert.Equal(80f, (after - pivot).magnitude, 2);
        Assert.Equal(before.y, after.y, 3); // yaw-only orbit
    }

    [Fact]
    public void FreestyleRoundTrip_HandsOffToGameplay_ThenBackToMenu()
    {
        var rig = MakeRig();
        var (player, followTarget) = MakeLocalPlayer();
        typeof(GameDataSO).GetProperty("LocalPlayer")!.SetValue(rig.GameData, player);

        // Enter freestyle: with no bridge vCam constructible, upstream's own
        // no-bridge branch hands straight off to the gameplay camera pipeline.
        rig.Freestyle.OnGameStateTransitionStart.Raise();
        loop.Tick(1f / 60f);

        Assert.Equal(CameraManager.ShellCameraState.Gameplay, rig.Cameras.ActiveShellState);
        Assert.Same(followTarget, rig.Cameras.LastGameplayFollowTarget);
        Assert.True(Get<bool>(rig.Controller, "_isInFreestyle"));

        // Exit freestyle: no "CM PlayerCam" child exists, so upstream's own
        // no-player-camera branch activates the menu family immediately.
        rig.Freestyle.OnMenuStateTransitionStart.Raise();
        loop.Tick(1f / 60f);

        Assert.Equal(CameraManager.ShellCameraState.MainMenu, rig.Cameras.ActiveShellState);
        Assert.False(Get<bool>(rig.Controller, "_isInFreestyle"));
    }

    [Fact]
    public void EnterFreestyle_WithoutAVessel_IsANoOp()
    {
        var rig = MakeRig();

        rig.Freestyle.OnGameStateTransitionStart.Raise();
        loop.Tick(1f / 60f);

        Assert.Equal(CameraManager.ShellCameraState.None, rig.Cameras.ActiveShellState);
        Assert.False(Get<bool>(rig.Controller, "_isInFreestyle"));
    }
}
