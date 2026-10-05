using System;
using System.Reflection;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// CameraManager unit (2026-07-10) — the REAL manager (replacing the Deviation
// #12 shell). Covers the live lanes: Awake's camera-trio discovery by child
// name (adding CustomCameraController where missing — each child carries the
// Camera component its Awake requires); SetupGamePlayCameras (player camera
// activated + follow target recorded on the manager AND pushed to the
// controllers, sky color written onto the activated camera, snap); the
// SetActiveCamera switch discipline (exactly one managed camera active,
// GetActiveController tracks it); SetupEndCameraFollow through a real
// VesselCameraCustomizer (upstream dereferences it unguarded);
// DeactivateAllCameras; SetMainMenuCameraActive parking the trio (the
// Cinemachine menu vCam side is a carried deviation) + the Invoke-scheduled
// LookAtCrystal surviving the 1s tick; the SOAP pair driving the same paths;
// and the engine's new MonoBehaviour.Invoke(string, float) contract.
// ─────────────────────────────────────────────────────────────────────────────

public class CameraManagerTests : IDisposable
{
    readonly GameLoop loop = new(nameof(CameraManagerTests));

    public CameraManagerTests() => ClearSingleton();

    public void Dispose()
    {
        ClearSingleton();
        loop.Dispose();
    }

    static void ClearSingleton()
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

    sealed class Rig
    {
        public CameraManager Manager;
        public GameObject PlayerCam;
        public GameObject DeathCam;
        public GameObject EndCam;
        public ScriptableEventNoParam OnReturnToMainMenu;
        public ScriptableEventTransform OnInitializePlayerCamera;
        public Color SkyColor;
    }

    Rig MakeRig()
    {
        var cmGo = new GameObject("camera-manager");
        cmGo.SetActive(false);

        GameObject MakeCam(string name)
        {
            var camGo = new GameObject(name);
            camGo.AddComponent<Camera>();
            camGo.transform.SetParent(cmGo.transform, false);
            return camGo;
        }
        var playerCamGo = MakeCam("CM PlayerCam");
        var deathCamGo = MakeCam("CM DeathCam");
        var endCamGo = MakeCam("CM EndCam");

        var manager = cmGo.AddComponent<CameraManager>();
        var onReturn = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        var onInit = ScriptableObject.CreateInstance<ScriptableEventTransform>();
        Set(manager, "_onReturnToMainMenu", onReturn);
        Set(manager, "_onInitializePlayerCamera", onInit);
        Set(manager, "_sceneNameList", ScriptableObject.CreateInstance<SceneNameListSO>());

        var sky = new Color(0.05f, 0.15f, 0.35f);
        var theme = C6Fixture.CreateTheme(out _);
        theme.ColorSet = ScriptableObject.CreateInstance<SO_ColorSet>();
        theme.ColorSet.EnvironmentColors = new EnvironmentColorSet { SkyColor = sky };
        Set(manager, "_themeManagerData", theme);

        cmGo.SetActive(true);   // Awake: singleton + trio discovery; OnEnable: SOAP pair
        loop.Tick(1f / 60f);    // Start: scene routing (name mismatch → no-op)

        return new Rig
        {
            Manager = manager,
            PlayerCam = playerCamGo, DeathCam = deathCamGo, EndCam = endCamGo,
            OnReturnToMainMenu = onReturn, OnInitializePlayerCamera = onInit,
            SkyColor = sky,
        };
    }

    [Fact]
    public void Awake_DiscoversTheTrioByChildName_AndAddsControllers()
    {
        var rig = MakeRig();

        // Each named child got a CustomCameraController added by discovery.
        Assert.NotNull(rig.PlayerCam.GetComponent<CustomCameraController>());
        Assert.NotNull(rig.DeathCam.GetComponent<CustomCameraController>());
        Assert.NotNull(rig.EndCam.GetComponent<CustomCameraController>());
        Assert.Null(rig.Manager.GetActiveController()); // nothing activated yet
        Assert.Same(rig.PlayerCam.transform, rig.Manager.GetCloseCamera());
    }

    [Fact]
    public void SetupGamePlayCameras_ActivatesThePlayerCamera_AndPaintsTheSky()
    {
        var rig = MakeRig();
        var followTarget = new GameObject("vessel-follow").transform;
        followTarget.position = new Vector3(5f, 10f, -20f);

        rig.Manager.SetupGamePlayCameras(followTarget);

        Assert.Same(followTarget, rig.Manager.PlayerFollowTarget);
        Assert.Same(rig.PlayerCam.GetComponent<CustomCameraController>(), rig.Manager.GetActiveController());
        Assert.True(rig.PlayerCam.activeSelf);
        Assert.False(rig.DeathCam.activeSelf);
        Assert.False(rig.EndCam.activeSelf);
        // The sky color went onto the just-activated camera, not Camera.main —
        // the tag lookup can be stale in the first frame after a transition.
        Assert.Equal(rig.SkyColor, rig.PlayerCam.GetComponent<Camera>().backgroundColor);
        // SnapToTarget parked the camera at the controller's own follow pose
        // (identity target rotation → position + serialized default offset).
        var controller = rig.PlayerCam.GetComponent<CustomCameraController>();
        Assert.Equal(followTarget.position + controller.GetFollowOffset(), rig.PlayerCam.transform.position);
    }

    [Fact]
    public void SetActiveCamera_SwitchDiscipline_ExactlyOneManagedCameraLive()
    {
        var rig = MakeRig();
        rig.Manager.SetupGamePlayCameras(new GameObject("t").transform);

        rig.Manager.SetDeathCameraActive();
        Assert.Same(rig.DeathCam.GetComponent<CustomCameraController>(), rig.Manager.GetActiveController());
        Assert.True(rig.DeathCam.activeSelf);
        Assert.False(rig.PlayerCam.activeSelf);
        Assert.False(rig.EndCam.activeSelf);

        rig.Manager.DeactivateAllCameras();
        Assert.Null(rig.Manager.GetActiveController());
        Assert.False(rig.PlayerCam.activeSelf);
        Assert.False(rig.DeathCam.activeSelf);
        Assert.False(rig.EndCam.activeSelf);
    }

    [Fact]
    public void SetupEndCameraFollow_ConfiguresThroughTheVesselCustomizer()
    {
        var rig = MakeRig();

        // A follow target carrying a real VesselCameraCustomizer (upstream
        // dereferences it unguarded — the menu vessel always has one).
        var vesselGo = new GameObject("stub-vessel");
        var vessel = vesselGo.AddComponent<ToyStubVessel>();
        vessel.Status = vesselGo.AddComponent<VesselStatus>();
        var customizer = vesselGo.AddComponent<VesselCameraCustomizer>();
        Set(customizer, "settings", ScriptableObject.CreateInstance<CameraSettingsSO>());
        Set(customizer, "vessel", vessel);

        rig.Manager.SetupEndCameraFollow(vesselGo.transform);

        Assert.Same(rig.EndCam.GetComponent<CustomCameraController>(), rig.Manager.GetActiveController());
        Assert.True(rig.EndCam.activeSelf);
        Assert.False(rig.PlayerCam.activeSelf);
    }

    [Fact]
    public void SoapPair_DrivesTheSamePaths()
    {
        var rig = MakeRig();
        // OnEnteredMainMenu writes the sky through Camera.main — give the scene one.
        typeof(Camera).GetProperty("main")!.SetValue(null, rig.PlayerCam.GetComponent<Camera>());
        var followTarget = new GameObject("soap-follow").transform;

        rig.OnInitializePlayerCamera.Raise(followTarget);
        Assert.Same(followTarget, rig.Manager.PlayerFollowTarget);
        Assert.True(rig.PlayerCam.activeSelf);

        rig.OnReturnToMainMenu.Raise();
        Assert.Null(rig.Manager.GetActiveController());
        Assert.False(rig.PlayerCam.activeSelf);
        Assert.False(rig.DeathCam.activeSelf);
        Assert.False(rig.EndCam.activeSelf);

        // SetMainMenuCameraActive scheduled Invoke("LookAtCrystal", 1f) — the
        // Cinemachine LookAt body is a carried deviation, but the scheduled call
        // must survive the delay without faulting the loop.
        for (int i = 0; i < 70; i++) loop.Tick(1f / 60f);
        typeof(Camera).GetProperty("main")!.SetValue(null, null);
    }

    // ── engine growth: MonoBehaviour.Invoke(string, float) ──────────────────

    sealed class InvokeProbe : MonoBehaviour
    {
        public int Calls;
        void Ping() => Calls++;
    }

    [Fact]
    public void EngineInvoke_FiresTheNamedMethodOnce_AfterTheScaledDelay()
    {
        var probe = new GameObject("probe").AddComponent<InvokeProbe>();

        probe.Invoke("Ping", 0.5f);
        for (int i = 0; i < 29; i++) loop.Tick(1f / 60f); // ~0.48s — not yet
        Assert.Equal(0, probe.Calls);

        for (int i = 0; i < 5; i++) loop.Tick(1f / 60f);  // past 0.5s — fired once
        Assert.Equal(1, probe.Calls);

        for (int i = 0; i < 60; i++) loop.Tick(1f / 60f); // no repeat
        Assert.Equal(1, probe.Calls);
    }
}
