using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// V19: camera / silhouette / impact slice — CustomCameraController data+state logic
// (offsets, mode flags, follow bookkeeping, snap/track), VesselCameraCustomizer wiring
// into IVesselStatus and ICameraController, SilhouetteConfigSO / CameraSettingsSO
// defaults, SilhouetteController initialization against the phase-5 shells (element-bar
// driving via ResourceSystem level changes), ImpactorBase/VesselImpactor dispatch with
// recording ImpactEffectSO subclasses, and VesselExplosionByCrystalEffectSO pure logic.
//
// Engine trigger dispatch is phase-2 physics, so OnTriggerEnter is invoked reflectively
// (SkimmerLayerTests precedent). Paths that sink into the Deviation #13 view shells
// (SilhouetteView energy/tint/conveyor forwarding, ElementalBarsView rendering) are
// inert and covered only down to the shell's bookkeeping surface.

// ── shared doubles (file-local additions; shared VesselLayerTestDoubles untouched) ──

/// <summary>Minimal IPlayer — VesselImpactor.isInitialized only needs non-null.</summary>
class CameraSilhouettePlayerStub : IPlayer
{
    public Domains Domain { get; set; } = Domains.Jade;
    public string Name { get; set; } = "cam-pilot";
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

/// <summary>
/// StubVesselStatus with a settable vessel class. The shared stub pins VesselType to
/// Manta with a non-virtual member, so this re-implements IVesselStatus and supplies
/// the override explicitly — a file-local extension, per the doubles ground rule.
/// </summary>
class TypedVesselStatus : StubVesselStatus, IVesselStatus
{
    public VesselClassType VesselTypeOverride = VesselClassType.Manta;
    VesselClassType IVesselStatus.VesselType => VesselTypeOverride;
}

/// <summary>Recording ICameraController for VesselCameraCustomizer.Configure wiring.</summary>
class RecordingCameraController : ICameraController
{
    public CameraSettingsSO AppliedSettings;
    public Transform FollowTarget;
    public readonly List<float> Distances = new();
    public int ActivateCalls, DeactivateCalls;

    public void ApplySettings(CameraSettingsSO settings) => AppliedSettings = settings;
    public void SetFollowTarget(Transform target) => FollowTarget = target;
    public void Activate() => ActivateCalls++;
    public void Deactivate() => DeactivateCalls++;
    public void SetCameraDistance(float distance) => Distances.Add(distance);
    public float GetCameraDistance() => Distances.Count > 0 ? Distances[^1] : 0f;
    public float NeutralOffsetZ => 0f;
    public float ZoomSmoothTime => 0f;
    public bool AdaptiveZoomEnabled => false;
}

// Recording ImpactEffectSO subclasses — capture the (impactor, impactee/data) pairs the
// dispatch matrix hands to effects.
class RecordingVesselPrismEffect : VesselPrismEffectSO
{
    public readonly List<(VesselImpactor impactor, PrismImpactor impactee)> Calls = new();
    public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        => Calls.Add((impactor, prismImpactee));
}

class RecordingVesselSkimmerEffect : VesselSkimmerEffectsSO
{
    public readonly List<(VesselImpactor impactor, SkimmerImpactor impactee)> Calls = new();
    public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        => Calls.Add((impactor, impactee));
}

class RecordingVesselCrystalEffect : VesselCrystalEffectSO
{
    public readonly List<(VesselImpactor impactor, CrystalImpactData data)> Calls = new();
    public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        => Calls.Add((vesselImpactor, data));
}

/// <summary>Exposes the protected vessel-type allow-list check for direct testing.</summary>
class AllowProbeImpactEffect : ImpactEffectSO
{
    public bool Allowed(VesselClassType guest, VesselClassType[] allowed)
        => IsVesselAllowedToImpact(guest, allowed);
}

/// <summary>Reflection + rig helpers shared by the V19 test classes below.</summary>
static class V19Rig
{
    public const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>Set a private/serialized field, searching up the declaring chain.</summary>
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

    static readonly MethodInfo TriggerEnter =
        typeof(ImpactorBase).GetMethod("OnTriggerEnter", Priv)!;

    /// <summary>Engine trigger dispatch is phase-2 physics — invoke reflectively.</summary>
    public static void Trigger(ImpactorBase impactor, Collider other)
        => TriggerEnter.Invoke(impactor, new object[] { other });

    /// <summary>
    /// Fully-wired VesselImpactor (configure-before-activation): stub vessel/status pair
    /// reflected into the private Vessel backing field so Awake's ??= keeps it.
    /// </summary>
    public static (VesselImpactor impactor, TypedVesselStatus status, GameObject go) MakeVesselImpactor(
        VesselImpactorDataContainerSO container,
        VesselClassType vesselType = VesselClassType.Manta,
        bool withPlayer = true)
    {
        var status = new TypedVesselStatus { VesselTypeOverride = vesselType };
        if (withPlayer) status.Player = new CameraSilhouettePlayerStub();
        var vessel = new StubVessel { VesselStatus = status };
        status.Vessel = vessel;

        var go = new GameObject("vessel-impactor");
        go.SetActive(false);
        var impactor = go.AddComponent<VesselImpactor>();
        Set(impactor, "<Vessel>k__BackingField", vessel);
        if (container != null) Set(impactor, "vesselImpactorDataContainerSO", container);
        go.SetActive(true);
        return (impactor, status, go);
    }

    /// <summary>Impactee GameObject: collider + ImpactCollider routing to a PrismImpactor.</summary>
    public static (Collider collider, PrismImpactor impactor) MakePrismImpactee()
    {
        var go = new GameObject("prism-impactee");
        var collider = go.AddComponent<BoxCollider>();
        var prismImpactor = go.AddComponent<PrismImpactor>();
        var impactCollider = go.AddComponent<ImpactCollider>();
        Set(impactCollider, "impactorObject", prismImpactor);
        return (collider, prismImpactor);
    }

    /// <summary>Impactee GameObject: collider + ImpactCollider routing to a SkimmerImpactor.</summary>
    public static (Collider collider, SkimmerImpactor impactor) MakeSkimmerImpactee()
    {
        var go = new GameObject("skimmer-impactee");
        var collider = go.AddComponent<BoxCollider>();
        var skimmerImpactor = go.AddComponent<SkimmerImpactor>();
        var impactCollider = go.AddComponent<ImpactCollider>();
        Set(impactCollider, "impactorObject", skimmerImpactor);
        return (collider, skimmerImpactor);
    }

    /// <summary>Subscriber count on the Manta flower static event (subscription-lifecycle checks).</summary>
    public static int MantaFlowerSubscriberCount()
    {
        var field = typeof(VesselExplosionByCrystalEffectSO)
            .GetField("OnMantaFlowerExplosion", BindingFlags.Static | BindingFlags.NonPublic)!;
        return ((Delegate)field.GetValue(null))?.GetInvocationList().Length ?? 0;
    }
}

// ── CustomCameraController + CameraSettingsSO + VesselCameraCustomizer ─────────────

public class CameraLayerTests
{
    public CameraLayerTests()
    {
        // Process-global singleton survives GameLoop disposal (same reset as SkimmerLayerTests).
        typeof(Singleton<CameraManager>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
    }

    static (CustomCameraController ctrl, Camera cam, GameObject go) MakeCamera()
    {
        var go = new GameObject("camera");
        var cam = go.AddComponent<Camera>();
        var ctrl = go.AddComponent<CustomCameraController>();
        return (ctrl, cam, go);
    }

    static CameraSettingsSO MakeSettings(Action<CameraSettingsSO> mutate = null)
    {
        var settings = ScriptableObject.CreateInstance<CameraSettingsSO>();
        mutate?.Invoke(settings);
        return settings;
    }

    [Fact]
    public void CameraSettingsSO_Defaults_MatchOriginalAsset()
    {
        var s = ScriptableObject.CreateInstance<CameraSettingsSO>();

        Assert.Equal(CameraMode.FixedCamera, s.mode);
        Assert.Equal(0f, s.followOffset.x, 3);
        Assert.Equal(10f, s.followOffset.y, 3);
        Assert.Equal(-20f, s.followOffset.z, 3);
        Assert.Equal(10f, s.dynamicMinDistance, 3);
        Assert.Equal(40f, s.dynamicMaxDistance, 3);
        Assert.Equal(0.2f, s.followSmoothTime, 3);
        Assert.Equal(5f, s.rotationSmoothTime, 3);
        Assert.False(s.disableSmoothing);
        Assert.Equal(0.3f, s.nearClipPlane, 3);
        Assert.Equal(1000f, s.farClipPlane, 3);
        Assert.False(s.enableAdaptiveZoom);
        Assert.Equal(0f, s.adaptiveMaxDistance, 3);
        Assert.Equal(5f, s.orthographicSize, 3);
    }

    [Fact]
    public void Awake_BindsSiblingCamera_AndDisablesOcclusionCulling()
    {
        using var loop = new GameLoop();
        var (ctrl, cam, _) = MakeCamera();

        Assert.Same(cam, ctrl.Camera);
        Assert.False(cam.useOcclusionCulling);
    }

    [Fact]
    public void ApplySettings_FixedMode_CopiesFullOffsetClipPlanes_AndAdaptiveZoom()
    {
        using var loop = new GameLoop();
        var (ctrl, cam, _) = MakeCamera();
        var settings = MakeSettings(s =>
        {
            s.followOffset = new Vector3(1f, 2f, -30f);
            s.nearClipPlane = 0.5f;
            s.farClipPlane = 500f;
            s.enableAdaptiveZoom = true;
        });

        ctrl.ApplySettings(settings);

        var offset = ctrl.GetFollowOffset();
        Assert.Equal(1f, offset.x, 3);
        Assert.Equal(2f, offset.y, 3);
        Assert.Equal(-30f, offset.z, 3);
        Assert.Equal(0.5f, cam.nearClipPlane, 3);
        Assert.Equal(500f, cam.farClipPlane, 3);
        Assert.True(ctrl.AdaptiveZoomEnabled);
        Assert.Equal(-30f, ctrl.NeutralOffsetZ, 3); // neutral Z captured only on the fixed path
    }

    [Fact]
    public void ApplySettings_DynamicMode_TakesXYFromOffset_AndZFromDynamicMinDistance()
    {
        using var loop = new GameLoop();
        var (ctrl, _, _) = MakeCamera();
        var settings = MakeSettings(s =>
        {
            s.mode = CameraMode.DynamicCamera;
            s.followOffset = new Vector3(3f, 7f, -99f); // z must NOT be copied
            s.dynamicMinDistance = -12f;
        });

        ctrl.ApplySettings(settings);

        var offset = ctrl.GetFollowOffset();
        Assert.Equal(3f, offset.x, 3);
        Assert.Equal(7f, offset.y, 3);
        Assert.Equal(-12f, offset.z, 3);
        Assert.Equal(-12f, ctrl.GetCameraDistance(), 3);
        Assert.False(ctrl.AdaptiveZoomEnabled); // adaptive zoom is a fixed-path concern
    }

    [Fact]
    public void SetFollowOffset_ThenSetCameraDistance_ComposeOnZOnly()
    {
        using var loop = new GameLoop();
        var (ctrl, _, _) = MakeCamera();

        ctrl.SetFollowOffset(new Vector3(1f, 2f, 3f));
        ctrl.SetCameraDistance(-5f);

        var offset = ctrl.GetFollowOffset();
        Assert.Equal(1f, offset.x, 3);
        Assert.Equal(2f, offset.y, 3);
        Assert.Equal(-5f, offset.z, 3);
        Assert.Equal(-5f, ctrl.GetCameraDistance(), 3);
    }

    [Fact]
    public void SetOrthographic_TogglesProjection_SizeOnlyAppliedWhenOrtho()
    {
        using var loop = new GameLoop();
        var (ctrl, cam, _) = MakeCamera();

        ctrl.SetOrthographic(true, 12f);
        Assert.True(cam.orthographic);
        Assert.Equal(12f, cam.orthographicSize, 3);

        ctrl.SetOrthographic(false, 99f);
        Assert.False(cam.orthographic);
        Assert.Equal(12f, cam.orthographicSize, 3); // size untouched when leaving ortho
    }

    [Fact]
    public void SetFollowTarget_ResetsSmoothingState()
    {
        using var loop = new GameLoop();
        var (ctrl, _, _) = MakeCamera();
        var target = new GameObject("target");
        target.transform.position = new Vector3(4f, 5f, 6f);

        ctrl.ApplySettings(MakeSettings());
        ctrl.SetFollowTarget(target.transform);
        ctrl.SnapToTarget(); // seeds _lastTargetPos with the target position

        var retarget = new GameObject("retarget");
        ctrl.SetFollowTarget(retarget.transform);

        Assert.Equal(Vector3.zero, (Vector3)V19Rig.Get(ctrl, "_lastTargetPos"));
        Assert.Equal(Vector3.zero, (Vector3)V19Rig.Get(ctrl, "_velocity"));
        Assert.Same(retarget.transform, V19Rig.Get(ctrl, "_followTarget"));
    }

    [Fact]
    public void SnapToTarget_PlacesCameraAtRotatedOffset_LookingAtTarget()
    {
        using var loop = new GameLoop();
        var (ctrl, _, go) = MakeCamera();
        var target = new GameObject("target");
        target.transform.position = new Vector3(10f, 0f, 0f);
        target.transform.rotation = Quaternion.AngleAxis(90f, Vector3.up); // nose +x

        ctrl.ApplySettings(MakeSettings()); // fixed offset (0, 10, -20)
        ctrl.SetFollowTarget(target.transform);
        ctrl.SnapToTarget();

        // rot * (0,10,-20) with 90° yaw → (-20, 10, 0); camera at target + that.
        Assert.Equal(-10f, go.transform.position.x, 2);
        Assert.Equal(10f, go.transform.position.y, 2);
        Assert.Equal(0f, go.transform.position.z, 2);

        var expectedForward = (target.transform.position - go.transform.position).normalized;
        Assert.True(Vector3.Dot(go.transform.forward, expectedForward) > 0.9999f,
            $"camera should look at target (forward {go.transform.forward})");
    }

    [Fact]
    public void SnapToTarget_WithoutFollowTarget_NoOps()
    {
        using var loop = new GameLoop();
        var (ctrl, _, go) = MakeCamera();
        go.transform.position = new Vector3(1f, 2f, 3f);

        ctrl.SnapToTarget();

        Assert.Equal(1f, go.transform.position.x, 3);
        Assert.Equal(2f, go.transform.position.y, 3);
        Assert.Equal(3f, go.transform.position.z, 3);
    }

    [Fact]
    public void LateUpdate_FixedMode_TracksMovedTargetRigidly()
    {
        using var loop = new GameLoop();
        var (ctrl, _, go) = MakeCamera();
        var target = new GameObject("target");

        ctrl.ApplySettings(MakeSettings()); // fixed → rotation lerp disabled → rigid snap
        ctrl.SetFollowTarget(target.transform);
        loop.Tick(1f / 60f); // drain Start, first LateUpdate

        target.transform.position = new Vector3(50f, 0f, 0f);
        loop.Tick(1f / 60f);

        Assert.Equal(50f, go.transform.position.x, 2);
        Assert.Equal(10f, go.transform.position.y, 2);
        Assert.Equal(-20f, go.transform.position.z, 2);

        var expectedForward = (target.transform.position - go.transform.position).normalized;
        Assert.True(Vector3.Dot(go.transform.forward, expectedForward) > 0.999f,
            "fixed-mode camera should aim at the target every frame");
    }

    [Fact]
    public void LateUpdate_WithoutFollowTarget_DoesNotMove()
    {
        using var loop = new GameLoop();
        var (ctrl, _, go) = MakeCamera();
        ctrl.ApplySettings(MakeSettings());

        loop.Tick(1f / 60f);
        loop.Tick(1f / 60f);

        Assert.Equal(0f, go.transform.position.x, 3);
        Assert.Equal(0f, go.transform.position.y, 3);
        Assert.Equal(0f, go.transform.position.z, 3);
    }

    [Fact]
    public void LateUpdate_DynamicMode_SmoothsForwardMotion_ButSnapsLateralMotion()
    {
        using var loop = new GameLoop();
        var (ctrl, _, go) = MakeCamera();
        var target = new GameObject("target");
        target.transform.position = new Vector3(0f, 0f, 5f); // off-origin: avoids the zero-pos seed quirk
        var settings = MakeSettings(s =>
        {
            s.mode = CameraMode.DynamicCamera;
            s.followOffset = new Vector3(0f, 10f, 0f);
            s.dynamicMinDistance = -20f;
            s.disableSmoothing = false;
        });

        ctrl.ApplySettings(settings);
        ctrl.SetFollowTarget(target.transform);
        ctrl.SnapToTarget(); // converged: camera (0, 10, -15)
        loop.Tick(1f / 60f);

        // Forward jump → SmoothDamp branch: camera lags behind the new desired pos (z = 35).
        target.transform.position += new Vector3(0f, 0f, 50f);
        loop.Tick(1f / 60f);
        Assert.InRange(go.transform.position.z, -14.99f, 0f); // moved, but far from converged

        // Lateral jump dominates forward → rigid snap branch: camera exactly at the offset.
        target.transform.position += new Vector3(200f, 0f, 0f);
        loop.Tick(1f / 60f);
        Assert.Equal(200f, go.transform.position.x, 2);
        Assert.Equal(10f, go.transform.position.y, 2);
        Assert.Equal(35f, go.transform.position.z, 2);
    }

    [Fact]
    public void Activate_ReappliesClipPlanes_Deactivate_DisablesGameObject()
    {
        using var loop = new GameLoop();
        var (ctrl, cam, go) = MakeCamera();
        ctrl.ApplySettings(MakeSettings(s => { s.nearClipPlane = 0.5f; s.farClipPlane = 500f; }));

        cam.nearClipPlane = 99f; // drift the camera, Activate must restore from settings
        ctrl.Activate();
        Assert.Equal(0.5f, cam.nearClipPlane, 3);
        Assert.Equal(500f, cam.farClipPlane, 3);

        ctrl.Deactivate();
        Assert.False(go.activeSelf);

        ctrl.Activate();
        Assert.True(go.activeSelf);
    }

    // ── VesselCameraCustomizer ──────────────────────────────────────────────

    static (VesselCameraCustomizer customizer, StubVessel vessel, StubVesselStatus status,
            ScriptableEventTransform initEvent)
        MakeCustomizer(CameraSettingsSO settings)
    {
        var status = new StubVesselStatus();
        var vessel = new StubVessel { VesselStatus = status };
        status.Vessel = vessel;
        vessel.Transform = new GameObject("vessel").transform;
        status.CameraFollowTarget = new GameObject("follow-target").transform;

        var go = new GameObject("camera-customizer");
        var customizer = go.AddComponent<VesselCameraCustomizer>();
        var initEvent = ScriptableObject.CreateInstance<ScriptableEventTransform>();
        V19Rig.Set(customizer, "settings", settings);
        V19Rig.Set(customizer, "OnInitializePlayerCamera", initEvent);
        return (customizer, vessel, status, initEvent);
    }

    [Fact]
    public void Initialize_RaisesPlayerCameraEvent_WithStatusFollowTarget()
    {
        using var loop = new GameLoop();
        var (customizer, vessel, status, initEvent) = MakeCustomizer(MakeSettings());
        Transform raised = null;
        int raises = 0;
        initEvent.OnRaised += t => { raised = t; raises++; };

        customizer.Initialize(vessel);

        Assert.Equal(1, raises);
        Assert.Same(status.CameraFollowTarget, raised);
    }

    [Fact]
    public void Configure_DynamicSettings_AppliesSettingsDistance_AndTargetsVesselTransform()
    {
        using var loop = new GameLoop();
        var settings = MakeSettings(s => { s.mode = CameraMode.DynamicCamera; s.dynamicMinDistance = -7f; });
        var (customizer, vessel, _, _) = MakeCustomizer(settings);
        var fake = new RecordingCameraController();
        customizer.Initialize(vessel);

        customizer.Configure(fake);

        Assert.Same(settings, customizer.Settings);
        Assert.Same(settings, fake.AppliedSettings);
        Assert.Contains(-7f, fake.Distances); // override applied on top of ApplySettings
        Assert.Same(vessel.Transform, fake.FollowTarget);
    }

    [Fact]
    public void Configure_FixedSettings_OnConcreteController_AppliesFullOffsetAndTarget()
    {
        using var loop = new GameLoop();
        var settings = MakeSettings(s => s.followOffset = new Vector3(4f, 5f, -6f));
        var (customizer, vessel, _, _) = MakeCustomizer(settings);
        var (ctrl, _, _) = MakeCamera();
        customizer.Initialize(vessel);

        customizer.Configure(ctrl);

        var offset = ctrl.GetFollowOffset();
        Assert.Equal(4f, offset.x, 3);
        Assert.Equal(5f, offset.y, 3);
        Assert.Equal(-6f, offset.z, 3);
        Assert.Same(vessel.Transform, V19Rig.Get(ctrl, "_followTarget"));
    }

    [Fact]
    public void Configure_OrthographicSettings_EnablesOrthographicProjection()
    {
        using var loop = new GameLoop();
        var settings = MakeSettings(s => { s.mode = CameraMode.Orthographic; s.orthographicSize = 9f; });
        var (customizer, vessel, _, _) = MakeCustomizer(settings);
        var (ctrl, cam, _) = MakeCamera();
        customizer.Initialize(vessel);

        customizer.Configure(ctrl);

        Assert.True(cam.orthographic);
        Assert.Equal(9f, cam.orthographicSize, 3);
    }

    [Fact]
    public void RetargetAndApply_ConfiguresOnlyWhenCameraManagerHasActiveController()
    {
        using var loop = new GameLoop();
        var managerGo = new GameObject("camera-manager");
        var manager = managerGo.AddComponent<CameraManager>(); // Awake → Singleton Instance
        Assert.Same(manager, CameraManager.Instance);

        var settings = MakeSettings(s => { s.mode = CameraMode.DynamicCamera; s.dynamicMinDistance = -7f; });
        var (customizer, vessel, status, initEvent) = MakeCustomizer(settings);
        int raises = 0;
        initEvent.OnRaised += _ => raises++;
        var fake = new RecordingCameraController();

        customizer.RetargetAndApply(vessel); // no active controller yet → init event only
        Assert.Equal(1, raises);
        Assert.Null(fake.AppliedSettings);

        V19Rig.Set(manager, "_activeController", fake);
        customizer.RetargetAndApply(vessel);

        Assert.Equal(2, raises);
        Assert.Same(settings, fake.AppliedSettings);
        Assert.Same(vessel.Transform, fake.FollowTarget);
        Assert.Contains(-7f, fake.Distances);
    }
}

// ── SilhouetteConfigSO + ElementalBarsView shell + SilhouetteController ────────────

public class SilhouetteLayerTests
{
    static (SilhouetteController controller, ElementalBarsView bars, ResourceSystem resources,
            StubVesselStatus status, GameObject go)
        MakeRig()
    {
        var resourcesGo = new GameObject("resources");
        resourcesGo.SetActive(false);
        var resources = resourcesGo.AddComponent<ResourceSystem>();
        resources.Resources = new List<Resource>();
        resourcesGo.SetActive(true);

        var status = new StubVesselStatus { ResourceSystem = resources };
        var vessel = new StubVessel { VesselStatus = status };
        status.Vessel = vessel;

        var go = new GameObject("silhouette");
        go.SetActive(false);
        var bars = go.AddComponent<ElementalBarsView>();
        var controller = go.AddComponent<SilhouetteController>();
        V19Rig.Set(controller, "elementBars", bars);
        go.SetActive(true);

        return (controller, bars, resources, status, go);
    }

    static Dictionary<Element, int> Levels(ElementalBarsView bars)
        => (Dictionary<Element, int>)V19Rig.Get(bars, "_currentLevels");

    [Fact]
    public void SilhouetteConfigSO_Defaults_MatchOriginalAsset()
    {
        var c = ScriptableObject.CreateInstance<SilhouetteConfigSO>();

        Assert.Equal(2f, c.worldToUIScale, 3);
        Assert.Equal(0.02f, c.imageScale, 4);
        Assert.Equal(SilhouetteConfigSO.FlowDirection.VerticalTopDown, c.flow);
        Assert.Equal(0f, c.columnRotationOffsetDeg, 3);
        Assert.Equal(10, c.minColumns);
        Assert.Equal(0, c.maxColumns);
        Assert.False(c.applyMaxColumnValues);
        Assert.True(c.smooth);
        Assert.Equal(0.08f, c.smoothingSeconds, 4);
        Assert.Equal(1f, c.thicknessMultiplier, 3);
        Assert.Equal(1f, c.lengthMultiplier, 3);
        Assert.Equal(1f, c.gapMultiplier, 3);
        Assert.Equal(1f, c.columnGapMultiplier, 3);
        Assert.True(c.useDomainPaletteColors);
        Assert.True(c.enableDangerVisual);
        Assert.Null(c.dangerBlockPrefab);
        Assert.Equal(1f, c.dangerColor.r, 3);
        Assert.Equal(0.25f, c.dangerColor.g, 3);
        Assert.Equal(0.2f, c.dangerColor.b, 3);
    }

    [Fact]
    public void ElementalBarsView_SetLevel_GatedUntilBuild_AndBuildIsIdempotent()
    {
        using var loop = new GameLoop();
        var bars = new GameObject("bars").AddComponent<ElementalBarsView>();

        Assert.False(bars.IsBuilt);
        bars.SetLevel(Element.Charge, 5); // pre-Build: ignored
        Assert.Empty(Levels(bars));

        bars.Build();
        Assert.True(bars.IsBuilt);
        bars.Build(); // second Build no-ops

        bars.SetLevel(Element.Charge, 5);
        Assert.Equal(5, Levels(bars)[Element.Charge]);
    }

    [Fact]
    public void ElementalBarsView_ClampsToOriginalLevelRange()
    {
        using var loop = new GameLoop();
        var bars = new GameObject("bars").AddComponent<ElementalBarsView>();
        bars.Build();

        bars.SetLevel(Element.Mass, 99);
        bars.SetLevel(Element.Space, -99);
        bars.SetLevel(Element.Time, 3, Color.red); // domain-colour overload delegates

        var levels = Levels(bars);
        Assert.Equal(15, levels[Element.Mass]);   // MaxLevel
        Assert.Equal(-5, levels[Element.Space]);  // MinLevel
        Assert.Equal(3, levels[Element.Time]);
    }

    [Fact]
    public void Initialize_BuildsBars_AndSeedsAllFourElementLevels()
    {
        using var loop = new GameLoop();
        var (controller, bars, _, status, _) = MakeRig();

        controller.Initialize(status);

        Assert.True(bars.IsBuilt);
        var levels = Levels(bars);
        Assert.Equal(4, levels.Count);
        Assert.Equal(0, levels[Element.Charge]);
        Assert.Equal(0, levels[Element.Mass]);
        Assert.Equal(0, levels[Element.Space]);
        Assert.Equal(0, levels[Element.Time]);
    }

    [Fact]
    public void ResourceSystemLevelChanges_DriveElementBars()
    {
        using var loop = new GameLoop();
        var (controller, bars, resources, status, _) = MakeRig();
        controller.Initialize(status);

        resources.AdjustLevel(Element.Charge, 0.83f);  // floor(0.83 * 10) = 8
        resources.AdjustLevel(Element.Space, -0.31f);  // floor(-3.1) = -4

        var levels = Levels(bars);
        Assert.Equal(8, levels[Element.Charge]);
        Assert.Equal(-4, levels[Element.Space]);
        Assert.Equal(0, levels[Element.Mass]); // untouched elements stay seeded
    }

    [Fact]
    public void Disable_UnsubscribesElementBarDriving()
    {
        using var loop = new GameLoop();
        var (controller, bars, resources, status, go) = MakeRig();
        controller.Initialize(status);

        go.SetActive(false);
        resources.AdjustLevel(Element.Time, 0.9f);

        Assert.Equal(0, Levels(bars)[Element.Time]);
    }

    [Fact]
    public void MantaFlowerExplosionSubscription_FollowsEnableDisable()
    {
        using var loop = new GameLoop();
        int baseline = V19Rig.MantaFlowerSubscriberCount();

        var (_, _, _, _, go) = MakeRig(); // SetActive(true) → OnEnable subscribes
        Assert.Equal(baseline + 1, V19Rig.MantaFlowerSubscriberCount());

        go.SetActive(false); // OnDisable unsubscribes — no leaked handler
        Assert.Equal(baseline, V19Rig.MantaFlowerSubscriberCount());
    }

    [Fact]
    public void OnBlockCreated_RequiresInitializedVessel_ThenAcceptsHeadData()
    {
        using var loop = new GameLoop();
        var (controller, _, _, status, _) = MakeRig();
        var onBlockCreated = typeof(SilhouetteController).GetMethod("OnBlockCreated", V19Rig.Priv)!;
        var args = new object[] { 1f, 2f, 3f, 4f, 5f };

        onBlockCreated.Invoke(controller, args); // not initialized → vessel null → rejected
        Assert.False((bool)V19Rig.Get(controller, "_haveHead"));

        controller.Initialize(status);
        onBlockCreated.Invoke(controller, args);
        Assert.True((bool)V19Rig.Get(controller, "_haveHead"));
    }
}

// ── ImpactorBase / VesselImpactor dispatch matrix ───────────────────────────────────

public class ImpactDispatchTests
{
    [Fact]
    public void IsVesselAllowedToImpact_EmptyListAllowsAll_OtherwiseFiltersByType()
    {
        var probe = ScriptableObject.CreateInstance<AllowProbeImpactEffect>();

        Assert.True(probe.Allowed(VesselClassType.Manta, Array.Empty<VesselClassType>()));
        Assert.True(probe.Allowed(VesselClassType.Manta,
            new[] { VesselClassType.Sparrow, VesselClassType.Manta }));
        Assert.False(probe.Allowed(VesselClassType.Dolphin,
            new[] { VesselClassType.Sparrow, VesselClassType.Manta }));
    }

    [Fact]
    public void TriggerEnter_VesselOverPrism_RunsVesselPrismEffects_WithBothParties()
    {
        using var loop = new GameLoop();
        var effect = ScriptableObject.CreateInstance<RecordingVesselPrismEffect>();
        var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
        V19Rig.Set(container, "vesselPrismEffects", new VesselPrismEffectSO[] { effect });
        var (vesselImpactor, _, _) = V19Rig.MakeVesselImpactor(container);
        var (collider, prismImpactor) = V19Rig.MakePrismImpactee();

        V19Rig.Trigger(vesselImpactor, collider);

        var call = Assert.Single(effect.Calls);
        Assert.Same(vesselImpactor, call.impactor);
        Assert.Same(prismImpactor, call.impactee);
    }

    [Fact]
    public void TriggerEnter_VesselOverSkimmer_RunsOnlyVesselSkimmerEffects()
    {
        using var loop = new GameLoop();
        var prismEffect = ScriptableObject.CreateInstance<RecordingVesselPrismEffect>();
        var skimmerEffect = ScriptableObject.CreateInstance<RecordingVesselSkimmerEffect>();
        var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
        V19Rig.Set(container, "vesselPrismEffects", new VesselPrismEffectSO[] { prismEffect });
        V19Rig.Set(container, "vesselSkimmerEffects", new VesselSkimmerEffectsSO[] { skimmerEffect });
        var (vesselImpactor, _, _) = V19Rig.MakeVesselImpactor(container);
        var (collider, skimmerImpactor) = V19Rig.MakeSkimmerImpactee();

        V19Rig.Trigger(vesselImpactor, collider);

        var call = Assert.Single(skimmerEffect.Calls);
        Assert.Same(vesselImpactor, call.impactor);
        Assert.Same(skimmerImpactor, call.impactee);
        Assert.Empty(prismEffect.Calls);
    }

    [Fact]
    public void TriggerEnter_GatedUntilPlayerAssigned()
    {
        using var loop = new GameLoop();
        var effect = ScriptableObject.CreateInstance<RecordingVesselPrismEffect>();
        var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
        V19Rig.Set(container, "vesselPrismEffects", new VesselPrismEffectSO[] { effect });
        var (vesselImpactor, status, _) = V19Rig.MakeVesselImpactor(container, withPlayer: false);
        var (collider, _) = V19Rig.MakePrismImpactee();

        V19Rig.Trigger(vesselImpactor, collider); // isInitialized false → ignored
        Assert.Empty(effect.Calls);

        status.Player = new CameraSilhouettePlayerStub();
        V19Rig.Trigger(vesselImpactor, collider);
        Assert.Single(effect.Calls);
    }

    [Fact]
    public void TriggerEnter_ColliderWithoutImpactCollider_IsIgnored()
    {
        using var loop = new GameLoop();
        var effect = ScriptableObject.CreateInstance<RecordingVesselPrismEffect>();
        var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
        V19Rig.Set(container, "vesselPrismEffects", new VesselPrismEffectSO[] { effect });
        var (vesselImpactor, _, _) = V19Rig.MakeVesselImpactor(container);
        var bareCollider = new GameObject("bare").AddComponent<BoxCollider>();

        V19Rig.Trigger(vesselImpactor, bareCollider);

        Assert.Empty(effect.Calls);
    }

    [Fact]
    public void TriggerEnter_MissingEffectArrays_NoOps()
    {
        using var loop = new GameLoop();
        // Default container: every effect array null → DoesEffectExist gate rejects all.
        var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
        var (vesselImpactor, _, _) = V19Rig.MakeVesselImpactor(container);
        var (prismCollider, _) = V19Rig.MakePrismImpactee();
        var (skimmerCollider, _) = V19Rig.MakeSkimmerImpactee();

        V19Rig.Trigger(vesselImpactor, prismCollider);   // must not throw
        V19Rig.Trigger(vesselImpactor, skimmerCollider); // must not throw
    }

    [Fact]
    public void ExecuteOmniCrystalImpact_RunsVesselCrystalEffects_WithDataPassthrough()
    {
        using var loop = new GameLoop();
        var effect = ScriptableObject.CreateInstance<RecordingVesselCrystalEffect>();
        var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
        V19Rig.Set(container, "vesselCrystalEffects", new VesselCrystalEffectSO[] { effect });
        var (vesselImpactor, _, _) = V19Rig.MakeVesselImpactor(container);

        vesselImpactor.ExecuteOmniCrystalImpact(
            new CrystalImpactData { Element = Element.Omni, SpeedBuffAmount = 4.2f });

        var call = Assert.Single(effect.Calls);
        Assert.Same(vesselImpactor, call.impactor);
        Assert.Equal(Element.Omni, call.data.Element);
        Assert.Equal(4.2f, call.data.SpeedBuffAmount, 3);
    }

    [Fact]
    public void ExecuteElementalCrystalImpact_RoutesToTheMatchingElementArray()
    {
        using var loop = new GameLoop();
        var byElement = new Dictionary<Element, RecordingVesselCrystalEffect>
        {
            [Element.Mass] = ScriptableObject.CreateInstance<RecordingVesselCrystalEffect>(),
            [Element.Charge] = ScriptableObject.CreateInstance<RecordingVesselCrystalEffect>(),
            [Element.Space] = ScriptableObject.CreateInstance<RecordingVesselCrystalEffect>(),
            [Element.Time] = ScriptableObject.CreateInstance<RecordingVesselCrystalEffect>(),
        };
        var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
        V19Rig.Set(container, "vesselMassCrystalEffects", new VesselCrystalEffectSO[] { byElement[Element.Mass] });
        V19Rig.Set(container, "vesselChargeCrystalEffects", new VesselCrystalEffectSO[] { byElement[Element.Charge] });
        V19Rig.Set(container, "vesselSpaceCrystalEffects", new VesselCrystalEffectSO[] { byElement[Element.Space] });
        V19Rig.Set(container, "vesselTimeCrystalEffects", new VesselCrystalEffectSO[] { byElement[Element.Time] });
        var (vesselImpactor, _, _) = V19Rig.MakeVesselImpactor(container);

        foreach (var element in new[] { Element.Mass, Element.Charge, Element.Space, Element.Time })
            vesselImpactor.ExecuteElementalCrystalImpact(new CrystalImpactData { Element = element });

        foreach (var (element, effect) in byElement)
        {
            var call = Assert.Single(effect.Calls);
            Assert.Equal(element, call.data.Element);
        }

        // Unmapped element → effects resolve null → DoesEffectExist gate, no dispatch.
        vesselImpactor.ExecuteElementalCrystalImpact(new CrystalImpactData { Element = Element.None });
        foreach (var effect in byElement.Values)
            Assert.Single(effect.Calls);
    }
}

// ── VesselExplosionByCrystalEffectSO pure logic ─────────────────────────────────────

public class VesselExplosionByCrystalEffectTests
{
    static VesselExplosionByCrystalEffectSO MakeEffect(
        out List<VesselImpactor> rhinoRaises, out List<VesselImpactor> squirrelRaises)
    {
        var effect = ScriptableObject.CreateInstance<VesselExplosionByCrystalEffectSO>();
        var rhinoEvent = ScriptableObject.CreateInstance<ScriptableEventVesselImpactor>();
        var squirrelEvent = ScriptableObject.CreateInstance<ScriptableEventVesselImpactor>();
        V19Rig.Set(effect, "rhinoCrystalExplosionEvent", rhinoEvent);
        V19Rig.Set(effect, "squirrelCrystalExplosionEvent", squirrelEvent);

        var rhino = new List<VesselImpactor>();
        var squirrel = new List<VesselImpactor>();
        rhinoEvent.OnRaised += v => rhino.Add(v);
        squirrelEvent.OnRaised += v => squirrel.Add(v);
        rhinoRaises = rhino;
        squirrelRaises = squirrel;
        return effect;
    }

    [Fact]
    public void Execute_NullImpactor_OrVessellessImpactor_RaisesNothing()
    {
        using var loop = new GameLoop();
        var effect = MakeEffect(out var rhinoRaises, out var squirrelRaises);
        int mantaRaises = 0;
        Action<VesselImpactor> handler = _ => mantaRaises++;
        VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion += handler;
        try
        {
            effect.Execute(null, default);

            // Impactor whose Awake found no IVessel sibling → Vessel null → early out.
            var bare = new GameObject("bare-impactor").AddComponent<VesselImpactor>();
            effect.Execute(bare, default);

            Assert.Equal(0, mantaRaises);
            Assert.Empty(rhinoRaises);
            Assert.Empty(squirrelRaises);
        }
        finally
        {
            VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion -= handler;
        }
    }

    [Fact]
    public void Execute_Manta_RaisesFlowerExplosion_SuppressedWithinCooldown()
    {
        using var loop = new GameLoop();
        var effect = MakeEffect(out _, out _);
        var (manta, _, _) = V19Rig.MakeVesselImpactor(null, VesselClassType.Manta);
        var raises = new List<VesselImpactor>();
        Action<VesselImpactor> handler = v => raises.Add(v);
        VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion += handler;
        try
        {
            effect.Execute(manta, default);
            effect.Execute(manta, default); // same frame → inside 0.15s cooldown
            Assert.Single(raises);
            Assert.Same(manta, raises[0]);

            loop.Tick(0.2f); // advance Time.time past the cooldown
            effect.Execute(manta, default);
            Assert.Equal(2, raises.Count);
        }
        finally
        {
            VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion -= handler;
        }
    }

    [Fact]
    public void Execute_CooldownIsTrackedPerImpactor()
    {
        using var loop = new GameLoop();
        var effect = MakeEffect(out _, out _);
        var (mantaA, _, _) = V19Rig.MakeVesselImpactor(null, VesselClassType.Manta);
        var (mantaB, _, _) = V19Rig.MakeVesselImpactor(null, VesselClassType.Manta);
        int raises = 0;
        Action<VesselImpactor> handler = _ => raises++;
        VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion += handler;
        try
        {
            effect.Execute(mantaA, default);
            effect.Execute(mantaB, default); // distinct impactor, same frame → still fires

            Assert.Equal(2, raises);
        }
        finally
        {
            VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion -= handler;
        }
    }

    [Fact]
    public void Execute_RoutesRhinoAndSquirrelToTheirEvents_OtherClassesStaySilent()
    {
        using var loop = new GameLoop();
        var effect = MakeEffect(out var rhinoRaises, out var squirrelRaises);
        var (rhino, _, _) = V19Rig.MakeVesselImpactor(null, VesselClassType.Rhino);
        var (squirrel, _, _) = V19Rig.MakeVesselImpactor(null, VesselClassType.Squirrel);
        var (dolphin, _, _) = V19Rig.MakeVesselImpactor(null, VesselClassType.Dolphin);
        int mantaRaises = 0;
        Action<VesselImpactor> handler = _ => mantaRaises++;
        VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion += handler;
        try
        {
            effect.Execute(rhino, default);
            effect.Execute(squirrel, default);
            effect.Execute(dolphin, default);

            Assert.Same(rhino, Assert.Single(rhinoRaises));
            Assert.Same(squirrel, Assert.Single(squirrelRaises));
            Assert.Equal(0, mantaRaises);
        }
        finally
        {
            VesselExplosionByCrystalEffectSO.OnMantaFlowerExplosion -= handler;
        }
    }
}
