using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// CT1 — the contact arc.
//
// Engine trigger pass (TriggerPass): per-frame overlap detection after the Update
// phase, OnTriggerEnter/OnTriggerExit discovered reflectively (LifecycleHooks) and
// dispatched on BOTH colliders' GameObjects with the other collider as the argument.
// At least one collider of a pair must be isTrigger; events fire on enter/exit
// transitions, including exit-on-destroy and exit-on-disable. Iteration is in
// collider registration order — deterministic.
//
// Impactor dispatch now flows through REAL contacts (no reflective OnTriggerEnter
// invoke — that idiom remains valid and is still exercised by ImpactDispatchTests),
// and the V19-staged crystal cases are restored: VesselImpactor accepts
// Omni/Elemental crystal impactees, SkimmerImpactor accepts Elemental crystal
// impactees, and the newly-ported OmniCrystalImpactor / ElementalCrystalImpactor /
// TeamCrystalImpactor drive collection through the Crystal shell's CT1 surface
// (Respawn → DestroyCrystal → CellRuntimeDataSO.TryRemoveItem).
// ─────────────────────────────────────────────────────────────────────────────

// ── doubles ──────────────────────────────────────────────────────────────────

/// <summary>Records every trigger message this GameObject receives.</summary>
class TriggerRecorder : MonoBehaviour
{
    public readonly List<(string evt, Collider other)> Events = new();
    void OnTriggerEnter(Collider other) => Events.Add(("enter", other));
    void OnTriggerExit(Collider other) => Events.Add(("exit", other));
}

/// <summary>Appends to a shared journal — registration-order determinism checks.</summary>
class JournalingTriggerRecorder : MonoBehaviour
{
    public static readonly List<string> Journal = new();
    public string Label;
    void OnTriggerEnter(Collider other) => Journal.Add($"{Label}:enter:{other.gameObject.name}");
    void OnTriggerExit(Collider other) => Journal.Add($"{Label}:exit:{other.gameObject.name}");
}

class RecordingSkimmerCrystalEffect : SkimmerCrystalEffectSO
{
    public readonly List<(SkimmerImpactor impactor, CrystalImpactor impactee)> Calls = new();
    public override void Execute(SkimmerImpactor impactor, CrystalImpactor impactee)
        => Calls.Add((impactor, impactee));
}

/// <summary>
/// Records the real Crystal → CrystalManager chain (restored with the rung-3
/// CrystalManager port): NotifyManagerToExplodeCrystal → ExplodeCrystal and
/// Respawn (allowRespawnOnImpact) → RespawnCrystal.
/// </summary>
class RecordingCrystalManager : CrystalManager
{
    public readonly List<int> RespawnCalls = new();
    public readonly List<(int id, Crystal.ExplodeParams p)> ExplodeCalls = new();

    public override void RespawnCrystal(int crystalId) => RespawnCalls.Add(crystalId);

    public override void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams)
    {
        ExplodeCalls.Add((crystalId, explodeParams));
        if (cellData.TryGetCrystalById(crystalId, out var crystal) && crystal != null)
            crystal.Explode(explodeParams); // LocalCrystalManager shape
    }
}

/// <summary>Shared rig builders for the contact-arc tests.</summary>
static class ContactRig
{
    /// <summary>GameObject with a SphereCollider + TriggerRecorder at a world position.</summary>
    public static (GameObject go, SphereCollider collider, TriggerRecorder recorder) MakeProbe(
        string name, Vector3 position, float radius = 1f, bool isTrigger = false)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var collider = go.AddComponent<SphereCollider>();
        collider.radius = radius;
        collider.isTrigger = isTrigger;
        var recorder = go.AddComponent<TriggerRecorder>();
        return (go, collider, recorder);
    }

    /// <summary>
    /// Crystal contact rig: trigger SphereCollider + Crystal shell (cellData wired,
    /// registered in the course list, manager injected — the rung-3 restored chain) +
    /// the requested CrystalImpactor subtype + ImpactCollider routing.
    /// </summary>
    public static (GameObject go, SphereCollider trigger, T impactor, Crystal crystal, CellRuntimeDataSO cellData)
        MakeCrystal<T>(Vector3 position, float radius = 5f,
            Element element = Element.Space, Domains ownDomain = Domains.Blue) where T : CrystalImpactor
    {
        var cellData = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
        cellData.OnCellItemsUpdated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        var managerGo = new GameObject("crystal-manager");
        managerGo.SetActive(false); // configure-before-activation
        var manager = managerGo.AddComponent<RecordingCrystalManager>();
        V19Rig.Set(manager, "cellData", cellData);
        managerGo.SetActive(true);

        var go = new GameObject("crystal-rig");
        go.transform.position = position;

        var trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = radius;

        var crystal = go.AddComponent<Crystal>();
        crystal.Initialize(1);
        crystal.ownDomain = ownDomain;
        crystal.crystalProperties = new CrystalProperties
        {
            crystal = crystal,
            Element = element,
            speedBuffAmount = 4.2f,
            crystalValue = 7f,
        };
        V19Rig.Set(crystal, "cellData", cellData);
        crystal.InjectDependencies(manager); // NotifyManagerToExplodeCrystal / Respawn route here

        var impactor = go.AddComponent<T>(); // Awake binds impactor.Crystal
        var impactCollider = go.AddComponent<ImpactCollider>();
        V19Rig.Set(impactCollider, "impactorObject", impactor);

        cellData.AddCrystalToList(crystal);
        return (go, trigger, impactor, crystal, cellData);
    }

    /// <summary>
    /// Vessel contact rig: V19Rig's fully-wired VesselImpactor plus the contact pieces —
    /// NetworkVesselImpactor pair (unspawned → local impact path), a non-trigger
    /// contact-bubble SphereCollider, and an ImpactCollider routing to the impactor.
    /// </summary>
    public static (VesselImpactor impactor, TypedVesselStatus status, GameObject go) MakeVessel(
        VesselImpactorDataContainerSO container, Vector3 position,
        VesselClassType vesselType = VesselClassType.Sparrow)
    {
        var (impactor, status, go) = V19Rig.MakeVesselImpactor(container, vesselType);
        go.transform.position = position;

        var net = go.AddComponent<NetworkVesselImpactor>();
        V19Rig.Set(impactor, "networkVesselImpactor", net);

        go.AddComponent<SphereCollider>().radius = 1f;
        var impactCollider = go.AddComponent<ImpactCollider>();
        V19Rig.Set(impactCollider, "impactorObject", impactor);
        return (impactor, status, go);
    }

    /// <summary>
    /// Skimmer contact rig: Skimmer (optionally initialized via a stubbed IVesselStatus
    /// with an active player), SkimmerImpactor, non-trigger collider, ImpactCollider.
    /// </summary>
    public static (SkimmerImpactor impactor, GameObject go) MakeSkimmer(
        Vector3 position, bool initialized, SkimmerImpactorDataContainerSO container = null)
    {
        var go = new GameObject("skimmer-rig");
        go.transform.position = position;
        go.SetActive(false);

        var skimmer = go.AddComponent<Skimmer>();
        V19Rig.Set(skimmer, "onSkimmerShipImpact", ScriptableObject.CreateInstance<ScriptableEventString>());
        if (initialized)
        {
            // Skimmer.IsInitialized => VesselStatus is { Player: { IsActive: true } }.
            var status = new StubVesselStatus { Player = new CameraSilhouettePlayerStub() };
            V19Rig.Set(skimmer, "<VesselStatus>k__BackingField", status);
        }

        var impactor = go.AddComponent<SkimmerImpactor>();
        V19Rig.Set(impactor, "skimmer", skimmer);
        if (container != null) V19Rig.Set(impactor, "skimmerImpactorDataContainer", container);

        go.AddComponent<SphereCollider>().radius = 1f;
        var impactCollider = go.AddComponent<ImpactCollider>();
        V19Rig.Set(impactCollider, "impactorObject", impactor);

        go.SetActive(true);
        return (impactor, go);
    }
}

// ── engine trigger-pass semantics ────────────────────────────────────────────

public class TriggerPassTests
{
    const float Dt = 1f / 60f;

    [Fact]
    public void Enter_FiresOnceOnBothSides_WithTheOtherCollider()
    {
        using var loop = new GameLoop();
        var (_, colliderA, recorderA) = ContactRig.MakeProbe("A", Vector3.zero, isTrigger: true);
        var (_, colliderB, recorderB) = ContactRig.MakeProbe("B", new Vector3(1f, 0f, 0f));

        loop.Tick(Dt);
        loop.Tick(Dt); // still overlapping — no re-enter

        var eventA = Assert.Single(recorderA.Events);
        Assert.Equal("enter", eventA.evt);
        Assert.Same(colliderB, eventA.other);

        var eventB = Assert.Single(recorderB.Events);
        Assert.Equal("enter", eventB.evt);
        Assert.Same(colliderA, eventB.other);
    }

    [Fact]
    public void TwoNonTriggerColliders_ProduceNoEvents()
    {
        using var loop = new GameLoop();
        var (_, _, recorderA) = ContactRig.MakeProbe("A", Vector3.zero);
        var (_, _, recorderB) = ContactRig.MakeProbe("B", new Vector3(0.5f, 0f, 0f));

        loop.Tick(Dt);

        Assert.Empty(recorderA.Events);
        Assert.Empty(recorderB.Events);
    }

    [Fact]
    public void SeparatedColliders_ProduceNoEvents()
    {
        using var loop = new GameLoop();
        var (_, _, recorderA) = ContactRig.MakeProbe("A", Vector3.zero, radius: 1f, isTrigger: true);
        var (_, _, recorderB) = ContactRig.MakeProbe("B", new Vector3(5f, 0f, 0f), radius: 1f);

        loop.Tick(Dt);

        Assert.Empty(recorderA.Events);
        Assert.Empty(recorderB.Events);
    }

    [Fact]
    public void Exit_FiresOnBothSides_WhenThePairSeparates()
    {
        using var loop = new GameLoop();
        var (_, colliderA, recorderA) = ContactRig.MakeProbe("A", Vector3.zero, isTrigger: true);
        var (goB, colliderB, recorderB) = ContactRig.MakeProbe("B", new Vector3(1f, 0f, 0f));

        loop.Tick(Dt); // enter
        goB.transform.position = new Vector3(100f, 0f, 0f);
        loop.Tick(Dt); // exit

        Assert.Equal(2, recorderA.Events.Count);
        Assert.Equal(("exit", colliderB), recorderA.Events[1]);
        Assert.Equal(2, recorderB.Events.Count);
        Assert.Equal(("exit", colliderA), recorderB.Events[1]);
    }

    [Fact]
    public void Exit_FiresOnTheSurvivor_WhenTheOtherColliderIsDestroyed()
    {
        using var loop = new GameLoop();
        var (_, _, recorderA) = ContactRig.MakeProbe("A", Vector3.zero, isTrigger: true);
        var (goB, colliderB, _) = ContactRig.MakeProbe("B", new Vector3(1f, 0f, 0f));

        loop.Tick(Dt); // enter
        Engine.Object.Destroy(goB);
        loop.Tick(Dt); // destroy flushes at end of this frame
        loop.Tick(Dt); // pair detected dead → exit on the survivor

        Assert.Equal(2, recorderA.Events.Count);
        Assert.Equal("exit", recorderA.Events[1].evt);
        // The destroyed collider is passed as `other` (compare by reference — the
        // engine's == treats destroyed objects as null).
        Assert.True(ReferenceEquals(colliderB, recorderA.Events[1].other));
    }

    [Fact]
    public void DisabledCollider_DoesNotParticipate_AndDisablingMidOverlapFiresExit()
    {
        using var loop = new GameLoop();
        var (_, colliderA, recorderA) = ContactRig.MakeProbe("A", Vector3.zero, isTrigger: true);
        var (_, colliderB, recorderB) = ContactRig.MakeProbe("B", new Vector3(1f, 0f, 0f));

        colliderB.enabled = false;
        loop.Tick(Dt);
        Assert.Empty(recorderA.Events); // disabled collider produces no contacts

        colliderB.enabled = true;
        loop.Tick(Dt); // enter
        colliderB.enabled = false;
        loop.Tick(Dt); // exit (pair no longer live)

        Assert.Equal(2, recorderA.Events.Count);
        Assert.Equal(("exit", colliderB), recorderA.Events[1]);
        Assert.Equal(2, recorderB.Events.Count);
        Assert.Equal(("exit", colliderA), recorderB.Events[1]);
    }

    [Fact]
    public void InactiveGameObject_DoesNotParticipate()
    {
        using var loop = new GameLoop();
        var (_, _, recorderA) = ContactRig.MakeProbe("A", Vector3.zero, isTrigger: true);
        var (goB, _, recorderB) = ContactRig.MakeProbe("B", new Vector3(1f, 0f, 0f));

        goB.SetActive(false);
        loop.Tick(Dt);

        Assert.Empty(recorderA.Events);
        Assert.Empty(recorderB.Events);
    }

    [Fact]
    public void DisabledBehaviour_StillReceivesTriggerMessages()
    {
        // Original-engine quirk parity: the per-behaviour enabled flag gates the update
        // loop, not physics messages.
        using var loop = new GameLoop();
        var (_, _, recorderA) = ContactRig.MakeProbe("A", Vector3.zero, isTrigger: true);
        recorderA.enabled = false;
        ContactRig.MakeProbe("B", new Vector3(1f, 0f, 0f));

        loop.Tick(Dt);

        var evt = Assert.Single(recorderA.Events);
        Assert.Equal("enter", evt.evt);
    }

    [Fact]
    public void Events_DispatchInColliderRegistrationOrder()
    {
        using var loop = new GameLoop();
        JournalingTriggerRecorder.Journal.Clear();
        try
        {
            // T registered first, then A, then B — all mutually overlapping.
            var goT = new GameObject("T");
            var trigger = goT.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2f;
            goT.AddComponent<JournalingTriggerRecorder>().Label = "T";

            var goA = new GameObject("A");
            goA.transform.position = new Vector3(1f, 0f, 0f);
            goA.AddComponent<SphereCollider>().radius = 2f;
            goA.AddComponent<JournalingTriggerRecorder>().Label = "A";

            var goB = new GameObject("B");
            goB.transform.position = new Vector3(-1f, 0f, 0f);
            goB.AddComponent<SphereCollider>().radius = 2f;
            goB.AddComponent<JournalingTriggerRecorder>().Label = "B";

            loop.Tick(Dt);

            // Pairs in (i, j) registration order: (T,A) then (T,B) — A↔B skipped (no
            // trigger). Within a pair the earlier-registered side is notified first.
            Assert.Equal(
                new[] { "T:enter:A", "A:enter:T", "T:enter:B", "B:enter:T" },
                JournalingTriggerRecorder.Journal);
        }
        finally
        {
            JournalingTriggerRecorder.Journal.Clear();
        }
    }

    [Fact]
    public void BoxAndSphere_Overlap_TreatsBoxAsWorldAabb()
    {
        using var loop = new GameLoop();
        var goBox = new GameObject("box");
        var box = goBox.AddComponent<BoxCollider>();
        box.size = new Vector3(2f, 2f, 2f); // extents 1
        box.isTrigger = true;
        var boxRecorder = goBox.AddComponent<TriggerRecorder>();

        var (goSphere, _, _) = ContactRig.MakeProbe("sphere", new Vector3(1.5f, 0f, 0f), radius: 1f);

        loop.Tick(Dt);
        Assert.Single(boxRecorder.Events); // closest AABB point (1,0,0) is 0.5 from center → inside r=1

        goSphere.transform.position = new Vector3(2.5f, 0f, 0f);
        loop.Tick(Dt);
        Assert.Equal(2, boxRecorder.Events.Count); // exit — now 1.5 away > r
        Assert.Equal("exit", boxRecorder.Events[1].evt);
    }
}

// ── impactor dispatch through real contacts ──────────────────────────────────

public class ContactImpactDispatchTests
{
    const float Dt = 1f / 60f;

    [Fact]
    public void VesselOverPrism_RealContact_RunsVesselPrismEffects_Once()
    {
        using var loop = new GameLoop();
        var effect = ScriptableObject.CreateInstance<RecordingVesselPrismEffect>();
        var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
        V19Rig.Set(container, "vesselPrismEffects", new VesselPrismEffectSO[] { effect });
        var (vesselImpactor, _, vesselGo) = ContactRig.MakeVessel(container, Vector3.zero);
        // V19Rig's prism impactee carries a (non-trigger) BoxCollider at the origin;
        // make the vessel's bubble the trigger side.
        vesselGo.GetComponent<SphereCollider>().isTrigger = true;
        var (_, prismImpactor) = V19Rig.MakePrismImpactee();

        loop.Tick(Dt);
        loop.Tick(Dt); // still overlapping — enter fires once

        var call = Assert.Single(effect.Calls);
        Assert.Same(vesselImpactor, call.impactor);
        Assert.Same(prismImpactor, call.impactee);
    }

    // ── restored V19 crystal cases, end to end ──────────────────────────────

    [Fact]
    public void OmniCrystal_RealContact_CollectsOnBothSides_AndCrystalRemovesItself()
    {
        using var loop = new GameLoop();

        // Vessel side: recording vessel-crystal effect (restored VesselImpactor case →
        // ExecuteOmniCrystalImpact, local path — NetworkVesselImpactor unspawned).
        var vesselEffect = ScriptableObject.CreateInstance<RecordingVesselCrystalEffect>();
        var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
        V19Rig.Set(container, "vesselCrystalEffects", new VesselCrystalEffectSO[] { vesselEffect });
        var (vesselImpactor, _, _) = ContactRig.MakeVessel(container, Vector3.zero);

        // Crystal side: OmniCrystalImpactor with the crystal-stats SOAP channel wired.
        var (_, _, omniImpactor, crystal, cellData) =
            ContactRig.MakeCrystal<OmniCrystalImpactor>(Vector3.zero, element: Element.Space);
        var onCollected = ScriptableObject.CreateInstance<ScriptableEventCrystalStats>();
        V19Rig.Set(omniImpactor, "OnCrystalCollected", onCollected);
        var collected = new List<CrystalStats>();
        onCollected.OnRaised += stats => collected.Add(stats);

        loop.Tick(Dt);

        // Crystal-side: stats event with the vessel's player identity + crystal payload.
        var stats = Assert.Single(collected);
        Assert.Equal("cam-pilot", stats.PlayerName);
        Assert.Equal(Element.Space, stats.Element);
        Assert.Equal(7f, stats.Value, 3);

        // Vessel-side: restored OmniCrystalImpactor case ran the vessel crystal effects
        // with the CrystalImpactData passthrough.
        var call = Assert.Single(vesselEffect.Calls);
        Assert.Same(vesselImpactor, call.impactor);
        Assert.Equal(Element.Space, call.data.Element);
        Assert.Equal(4.2f, call.data.SpeedBuffAmount, 3);

        // The restored manager chain ran: ExecuteEffect → NotifyManagerToExplodeCrystal
        // → CrystalManager.ExplodeCrystal (Sparrow ≠ Manta, so the explode fires).
        var manager = (RecordingCrystalManager)crystal.CrystalManager;
        var explode = Assert.Single(manager.ExplodeCalls);
        Assert.Equal(crystal.Id, explode.id);
        Assert.Equal("cam-pilot", explode.p.PlayerName.ToString());
        Assert.Empty(manager.RespawnCalls); // allowRespawnOnImpact=false → DestroyCrystal path

        // The crystal removed itself: Respawn → DestroyCrystal → TryRemoveItem +
        // Destroy(gameObject), flushed at the end of the same frame.
        Assert.Empty(cellData.CellItems);
        Assert.Empty(cellData.Crystals);
        Assert.True(crystal.IsDestroyed);
    }

    [Fact]
    public void OmniCrystal_SecondContact_GatedByIsImpactingWindow()
    {
        using var loop = new GameLoop();
        var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
        var (_, _, _) = ContactRig.MakeVessel(container, Vector3.zero);

        var (_, _, omniImpactor, _, _) = ContactRig.MakeCrystal<OmniCrystalImpactor>(Vector3.zero);
        // allowRespawnOnImpact=true keeps the crystal alive (Respawn → the manager's
        // RespawnCrystal — the restored rung-3 chain) so a second impactee can hit
        // inside the 0.5s window.
        var crystal2 = omniImpactor.Crystal;
        V19Rig.Set(crystal2, "allowRespawnOnImpact", true);
        var onCollected = ScriptableObject.CreateInstance<ScriptableEventCrystalStats>();
        V19Rig.Set(omniImpactor, "OnCrystalCollected", onCollected);
        var collected = new List<CrystalStats>();
        onCollected.OnRaised += stats => collected.Add(stats);

        loop.Tick(Dt);
        Assert.Single(collected);
        // Respawn routed into the manager (the crystal survived in place).
        Assert.Equal(new[] { crystal2.Id }, ((RecordingCrystalManager)crystal2.CrystalManager).RespawnCalls);
        Assert.False(crystal2.IsDestroyed);

        // A second vessel arriving inside the IsImpacting window is ignored.
        ContactRig.MakeVessel(container, Vector3.zero);
        loop.Tick(Dt);
        Assert.Single(collected);
    }

    [Fact]
    public void TeamCrystal_RealContact_OnlyMatchingDomainCollects()
    {
        // Mismatched domain: Jade crystal, Ruby vessel → no collection.
        using (var loop = new GameLoop())
        {
            var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
            var (_, status, _) = ContactRig.MakeVessel(container, Vector3.zero);
            ((CameraSilhouettePlayerStub)status.Player).Domain = Domains.Ruby;

            var (_, _, teamImpactor, crystal, cellData) =
                ContactRig.MakeCrystal<TeamCrystalImpactor>(Vector3.zero, ownDomain: Domains.Jade);
            var onCollected = ScriptableObject.CreateInstance<ScriptableEventCrystalStats>();
            V19Rig.Set(teamImpactor, "OnCrystalCollected", onCollected);
            int raised = 0;
            onCollected.OnRaised += _ => raised++;

            loop.Tick(Dt);

            Assert.Equal(0, raised);
            Assert.False(crystal.IsDestroyed);
            Assert.Single(cellData.Crystals);
        }

        // Matching domain: Jade crystal, Jade vessel → collected.
        using (var loop = new GameLoop())
        {
            var container = ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>();
            var (_, status, _) = ContactRig.MakeVessel(container, Vector3.zero);
            ((CameraSilhouettePlayerStub)status.Player).Domain = Domains.Jade;

            var (_, _, teamImpactor, crystal, cellData) =
                ContactRig.MakeCrystal<TeamCrystalImpactor>(Vector3.zero, ownDomain: Domains.Jade);
            var onCollected = ScriptableObject.CreateInstance<ScriptableEventCrystalStats>();
            V19Rig.Set(teamImpactor, "OnCrystalCollected", onCollected);
            int raised = 0;
            onCollected.OnRaised += _ => raised++;

            loop.Tick(Dt);

            Assert.Equal(1, raised);
            Assert.True(crystal.IsDestroyed);
            Assert.Empty(cellData.Crystals);
        }
    }

    [Fact]
    public void ElementalCrystal_RealSkimmerContact_CollectsOnBothSides_ThenDestroysAfterFlight()
    {
        using var loop = new GameLoop();

        // Skimmer side: initialized skimmer with a recording crystal effect (restored
        // SkimmerImpactor case) and a VesselTransformer for the move-to-vessel flight.
        var skimmerEffect = ScriptableObject.CreateInstance<RecordingSkimmerCrystalEffect>();
        var skimmerContainer = ScriptableObject.CreateInstance<SkimmerImpactorDataContainerSO>();
        V19Rig.Set(skimmerContainer, "skimmerCrystalEffectsSO", new SkimmerCrystalEffectSO[] { skimmerEffect });
        var (skimmerImpactor, skimmerGo) = ContactRig.MakeSkimmer(Vector3.zero, initialized: true, skimmerContainer);
        var status = (StubVesselStatus)skimmerImpactor.Skimmer.VesselStatus;
        status.VesselTransformer = skimmerGo.AddComponent<VesselTransformer>();

        // Crystal side: ElementalCrystalImpactor with a recording elemental effect.
        var crystalEffect = ScriptableObject.CreateInstance<RecordingSkimmerCrystalEffect>();
        var (_, trigger, elementalImpactor, crystal, cellData) =
            ContactRig.MakeCrystal<ElementalCrystalImpactor>(Vector3.zero, element: Element.Mass);
        V19Rig.Set(elementalImpactor, "elementalCrystalShipEffects",
            new SkimmerCrystalEffectSO[] { crystalEffect });
        V19Rig.Set(elementalImpactor, "moveToVesselDuration", 0.05f); // short flight for the test

        var collectedBy = new List<string>();
        Action<string> onCollected = name => collectedBy.Add(name);
        ElementalCrystalImpactor.OnCrystalCollected += onCollected;
        try
        {
            loop.Tick(Dt);

            // Crystal side accepted the skimmer: collection effect ran, the crystal's
            // collider was disabled immediately.
            var crystalCall = Assert.Single(crystalEffect.Calls);
            Assert.Same(skimmerImpactor, crystalCall.impactor);
            Assert.Same(elementalImpactor, crystalCall.impactee);
            Assert.False(trigger.enabled);

            // Skimmer side (restored case) ran its SkimmerCrystalEffects too.
            var skimmerCall = Assert.Single(skimmerEffect.Calls);
            Assert.Same(skimmerImpactor, skimmerCall.impactor);
            Assert.Same(elementalImpactor, skimmerCall.impactee);

            // Move-to-vessel flight announced the collector...
            Assert.Equal(new[] { "cam-pilot" }, collectedBy);

            // ...and after the flight completes the crystal destroys itself
            // (no space-collect animator in the headless shell — CT1 staging).
            for (int i = 0; i < 12; i++) loop.Tick(Dt); // 0.05s flight @ 60Hz + slack
            Assert.True(crystal.IsDestroyed);
            Assert.Empty(cellData.Crystals);
        }
        finally
        {
            ElementalCrystalImpactor.OnCrystalCollected -= onCollected;
        }
    }

    [Fact]
    public void ElementalCrystal_UninitializedSkimmerSide_StillCollectsOnCrystalSide()
    {
        // The crystal-side gate is `impactee is SkimmerImpactor` — the skimmer's own
        // isInitialized only gates the skimmer-side dispatch.
        using var loop = new GameLoop();
        var skimmerEffect = ScriptableObject.CreateInstance<RecordingSkimmerCrystalEffect>();
        var skimmerContainer = ScriptableObject.CreateInstance<SkimmerImpactorDataContainerSO>();
        V19Rig.Set(skimmerContainer, "skimmerCrystalEffectsSO", new SkimmerCrystalEffectSO[] { skimmerEffect });
        var (skimmerImpactor, _) = ContactRig.MakeSkimmer(Vector3.zero, initialized: false, skimmerContainer);

        var crystalEffect = ScriptableObject.CreateInstance<RecordingSkimmerCrystalEffect>();
        var (_, trigger, elementalImpactor, _, _) =
            ContactRig.MakeCrystal<ElementalCrystalImpactor>(Vector3.zero);
        V19Rig.Set(elementalImpactor, "elementalCrystalShipEffects",
            new SkimmerCrystalEffectSO[] { crystalEffect });

        loop.Tick(Dt);

        Assert.Single(crystalEffect.Calls);   // crystal side collected
        Assert.Empty(skimmerEffect.Calls);    // skimmer side gated by isInitialized
        Assert.False(trigger.enabled);
        // No VesselStatus → the move-to-vessel flight early-outs; the crystal lingers
        // (collected, inert) — verbatim original behavior.
    }

    [Fact]
    public void OmniCrystal_IgnoresNonVesselImpactees()
    {
        using var loop = new GameLoop();
        // A skimmer contact must not collect an omni crystal (its switch only accepts
        // VesselImpactor).
        ContactRig.MakeSkimmer(Vector3.zero, initialized: true);
        var (_, _, omniImpactor, crystal, cellData) =
            ContactRig.MakeCrystal<OmniCrystalImpactor>(Vector3.zero);
        var onCollected = ScriptableObject.CreateInstance<ScriptableEventCrystalStats>();
        V19Rig.Set(omniImpactor, "OnCrystalCollected", onCollected);
        int raised = 0;
        onCollected.OnRaised += _ => raised++;

        loop.Tick(Dt);

        Assert.Equal(0, raised);
        Assert.False(crystal.IsDestroyed);
        Assert.Single(cellData.Crystals);
    }
}
