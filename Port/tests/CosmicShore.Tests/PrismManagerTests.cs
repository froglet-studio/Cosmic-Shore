using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// V13: PrismStateManager state transitions (incl. PrismTimerManager-driven timed
// shield deactivation), PrismTeamManager team/domain bookkeeping, PrismScaleAnimator
// scale math. V15 restored the Prism-keyed deviations, so prismProperties writes and
// the conserved-mass volume record are now live (asserted below via PrismTestRig).
// The manager arc restored the PrismScaleManager / MaterialStateManager registration
// deviations — manager-batched animation itself is covered in PrismManagerArcTests;
// Octahedron engage/disengage remains staged (PrismOctahedronShield not yet ported).
public class PrismManagerTests
{
    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    public PrismManagerTests()
    {
        // Singleton<T>.Instance and AudioSystem.Instance are process-global statics; a
        // previous test's GameLoop disposal does not destroy its objects, so stale
        // instances must be cleared or Singleton<T>.Awake would Destroy the new one.
        typeof(Singleton<PrismTimerManager>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
        typeof(AudioSystem)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
        typeof(Singleton<PrismSpatialIndex>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
        typeof(Singleton<PrismScaleManager>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
        typeof(Singleton<MaterialStateManager>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static void MakeAudio() => new GameObject("audio").AddComponent<AudioSystem>();

    static (PrismStateManager state, PrismTeamManager team, GameObject go) MakePrism(
        ScriptableEventPrismStats stolenEvent = null)
    {
        // V15: the managers' restored paths dereference the sibling Prism and theme
        // getters, so tests build the full rig (configure-before-Awake pattern inside).
        var rig = PrismTestRig.Create();
        if (stolenEvent != null)
            typeof(PrismTeamManager).GetField("onPrismStolen", Priv)!.SetValue(rig.TeamManager, stolenEvent);
        return (rig.StateManager, rig.TeamManager, rig.GameObject);
    }

    static (PrismScaleAnimator anim, GameObject go) MakeAnimator(
        Vector3? initialScale = null,
        ScriptableEventPrismStats volumeEvent = null,
        bool withMeshRenderer = true)
    {
        var go = new GameObject("prismScale");
        go.SetActive(false);
        if (withMeshRenderer) go.AddComponent<MeshRenderer>();
        var anim = go.AddComponent<PrismScaleAnimator>();
        if (volumeEvent != null)
            typeof(PrismScaleAnimator).GetField("onPrismVolumeModified", Priv)!.SetValue(anim, volumeEvent);
        go.transform.localScale = initialScale ?? new Vector3(2f, 2f, 2f);
        go.SetActive(true);
        return (anim, go);
    }

    static bool IsRegistered(PrismScaleAnimator anim) =>
        (bool)typeof(PrismScaleAnimator).GetField("isRegistered", Priv)!.GetValue(anim);

    // ── PrismStateManager: state transitions ────────────────────────────────

    [Fact]
    public void CurrentState_DefaultsToNormal()
    {
        using var loop = new GameLoop();
        var (state, _, _) = MakePrism();

        Assert.Equal(BlockState.Normal, state.CurrentState);
    }

    [Fact]
    public void MakeDangerous_SetsDangerousState()
    {
        using var loop = new GameLoop();
        var (state, _, _) = MakePrism();

        state.MakeDangerous();

        Assert.Equal(BlockState.Dangerous, state.CurrentState);
    }

    [Fact]
    public void ActivateShield_NoDuration_SetsShieldedState()
    {
        using var loop = new GameLoop();
        MakeAudio();
        var (state, _, _) = MakePrism();

        state.ActivateShield();

        Assert.Equal(BlockState.Shielded, state.CurrentState);
    }

    [Fact]
    public void ActivateSuperShield_SetsSuperShieldedState()
    {
        using var loop = new GameLoop();
        var (state, _, _) = MakePrism();

        state.ActivateSuperShield();

        Assert.Equal(BlockState.SuperShielded, state.CurrentState);
    }

    [Fact]
    public void DeactivateShields_Immediate_ReturnsToNormal()
    {
        using var loop = new GameLoop();
        MakeAudio();
        var (state, _, _) = MakePrism();
        state.ActivateShield();

        state.DeactivateShields();

        Assert.Equal(BlockState.Normal, state.CurrentState);
    }

    [Fact]
    public void ActivateShield_WithDuration_DeactivatesWhenTimerExpires()
    {
        using var loop = new GameLoop();
        MakeAudio();
        var (state, _, _) = MakePrism();

        state.ActivateShield(1f);
        Assert.Equal(BlockState.Shielded, state.CurrentState);

        loop.Tick(0.5f); // t = 0.5 < 1.0 — still shielded
        Assert.Equal(BlockState.Shielded, state.CurrentState);

        loop.Tick(0.6f); // t = 1.1 ≥ 1.0 — timer fires ExecuteTimerDeactivation
        Assert.Equal(BlockState.Normal, state.CurrentState);
    }

    [Fact]
    public void DeactivateShields_WithDelay_StaysShieldedUntilTimerExpires()
    {
        using var loop = new GameLoop();
        MakeAudio();
        var (state, _, _) = MakePrism();
        state.ActivateShield();

        state.DeactivateShields(0.5f);
        Assert.Equal(BlockState.Shielded, state.CurrentState);

        loop.Tick(0.3f);
        Assert.Equal(BlockState.Shielded, state.CurrentState);

        loop.Tick(0.3f); // t = 0.6 ≥ 0.5
        Assert.Equal(BlockState.Normal, state.CurrentState);
    }

    [Fact]
    public void ActivateShield_AfterTimedShield_CancelsPendingDeactivation()
    {
        using var loop = new GameLoop();
        MakeAudio();
        var (state, _, _) = MakePrism();

        state.ActivateShield(0.5f);
        state.ActivateShield(); // re-shield with no duration — cancels the pending timer

        loop.Tick(1f);
        Assert.Equal(BlockState.Shielded, state.CurrentState);
    }

    [Fact]
    public void OnDisable_CancelsPendingTimers()
    {
        using var loop = new GameLoop();
        MakeAudio();
        var (state, _, go) = MakePrism();
        state.ActivateShield(0.5f);

        go.SetActive(false); // OnDisable cancels the scheduled deactivation
        loop.Tick(1f);

        Assert.Equal(BlockState.Shielded, state.CurrentState);
    }

    // ── PrismTimerManager ───────────────────────────────────────────────────

    [Fact]
    public void EnsureInstance_AutoCreates_AndReturnsSameInstance()
    {
        using var loop = new GameLoop();

        var first = PrismTimerManager.EnsureInstance();
        var second = PrismTimerManager.EnsureInstance();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void ScheduleShieldDeactivation_SameTarget_ReplacesPendingTimer()
    {
        using var loop = new GameLoop();
        MakeAudio();
        var (state, _, _) = MakePrism();
        state.ActivateShield();

        var timers = PrismTimerManager.EnsureInstance();
        timers.ScheduleShieldDeactivation(state, 5f);
        timers.ScheduleShieldDeactivation(state, 0.2f); // cancels + replaces the 5s timer

        loop.Tick(0.3f);
        Assert.Equal(BlockState.Normal, state.CurrentState);
    }

    // ── PrismTeamManager: team/domain bookkeeping ───────────────────────────

    [Fact]
    public void Domain_DefaultsToBlue()
    {
        using var loop = new GameLoop();
        var (_, team, _) = MakePrism();

        Assert.Equal(Domains.Blue, team.Domain);
    }

    [Fact]
    public void SetInitialTeam_FromBlue_SetsDomainAndFiresTeamChanged()
    {
        using var loop = new GameLoop();
        var (_, team, _) = MakePrism();
        var changes = new List<(Domains oldDomain, Domains newDomain)>();
        team.OnTeamChanged += (o, n) => changes.Add((o, n));

        team.SetInitialTeam(Domains.Jade);

        Assert.Equal(Domains.Jade, team.Domain);
        Assert.Equal((Domains.Blue, Domains.Jade), Assert.Single(changes));
    }

    [Fact]
    public void SetInitialTeam_WhenAlreadyAssigned_DoesNothing()
    {
        using var loop = new GameLoop();
        var (_, team, _) = MakePrism();
        team.SetInitialTeam(Domains.Jade);

        team.SetInitialTeam(Domains.Ruby); // only assigns from the Blue sentinel

        Assert.Equal(Domains.Jade, team.Domain);
    }

    [Fact]
    public void ChangeTeam_SameDomain_DoesNotFireTeamChanged()
    {
        using var loop = new GameLoop();
        var (_, team, _) = MakePrism();
        team.SetInitialTeam(Domains.Jade);
        var changes = 0;
        team.OnTeamChanged += (_, _) => changes++;

        team.ChangeTeam(Domains.Jade);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void Steal_SameDomain_DoesNotRaiseOrChangeTeam()
    {
        using var loop = new GameLoop();
        var stolenEvent = ScriptableObject.CreateInstance<ScriptableEventPrismStats>();
        var raised = new List<PrismStats>();
        stolenEvent.OnRaised += raised.Add;
        var (_, team, _) = MakePrism(stolenEvent);
        team.SetInitialTeam(Domains.Jade);

        team.Steal("Attacker", Domains.Jade, superSteal: false);

        Assert.Empty(raised);
        Assert.Equal(Domains.Jade, team.Domain);
    }

    [Fact]
    public void Steal_DifferentDomain_RaisesPrismStolenAndChangesTeam()
    {
        using var loop = new GameLoop();
        var stolenEvent = ScriptableObject.CreateInstance<ScriptableEventPrismStats>();
        var raised = new List<PrismStats>();
        stolenEvent.OnRaised += raised.Add;
        var (_, team, _) = MakePrism(stolenEvent);
        team.SetInitialTeam(Domains.Jade);
        var changes = new List<(Domains oldDomain, Domains newDomain)>();
        team.OnTeamChanged += (o, n) => changes.Add((o, n));

        team.Steal("Attacker", Domains.Ruby, superSteal: false);

        Assert.Equal("Attacker", Assert.Single(raised).OwnName);
        Assert.Equal(Domains.Ruby, team.Domain);
        Assert.Equal((Domains.Jade, Domains.Ruby), Assert.Single(changes));
    }

    [Fact]
    public void Steal_NullPlayerName_DefaultsToNoName()
    {
        using var loop = new GameLoop();
        var stolenEvent = ScriptableObject.CreateInstance<ScriptableEventPrismStats>();
        var raised = new List<PrismStats>();
        stolenEvent.OnRaised += raised.Add;
        var (_, team, _) = MakePrism(stolenEvent);
        team.SetInitialTeam(Domains.Jade);

        team.Steal(null, Domains.Gold, superSteal: false);

        Assert.Equal("No name", Assert.Single(raised).OwnName);
    }

    // ── PrismScaleAnimator: scale math ──────────────────────────────────────

    [Fact]
    public void Awake_CapturesAuthoredScale_SetsTarget_AndZeroesLocalScale()
    {
        using var loop = new GameLoop();
        var (anim, go) = MakeAnimator(initialScale: new Vector3(2f, 3f, 4f));

        Assert.Equal(new Vector3(2f, 3f, 4f), anim.AuthoredTargetScale);
        Assert.Equal(new Vector3(2f, 3f, 4f), anim.TargetScale);
        Assert.Equal(Vector3.zero, go.transform.localScale);
    }

    [Fact]
    public void Awake_WithoutMeshRenderer_DisablesComponent()
    {
        using var loop = new GameLoop();
        var (anim, _) = MakeAnimator(withMeshRenderer: false);

        Assert.False(anim.enabled);
    }

    [Fact]
    public void SetTargetScale_ClampsEachAxisToMinAndMax()
    {
        using var loop = new GameLoop();
        var (anim, _) = MakeAnimator(); // defaults: min (0.5,0.5,0.5), max (10,10,10)

        anim.SetTargetScale(new Vector3(0.1f, 5f, 20f));

        Assert.Equal(new Vector3(0.5f, 5f, 10f), anim.TargetScale);
    }

    [Fact]
    public void SetTargetScale_WhenDisabled_DoesNothing()
    {
        using var loop = new GameLoop();
        var (anim, _) = MakeAnimator(initialScale: new Vector3(2f, 2f, 2f));
        anim.enabled = false;

        anim.SetTargetScale(new Vector3(5f, 5f, 5f));

        Assert.Equal(new Vector3(2f, 2f, 2f), anim.TargetScale);
    }

    [Fact]
    public void BeginGrowthAnimation_SetsIsScaling_AndResetToZeroZeroesScale()
    {
        using var loop = new GameLoop();
        var (anim, go) = MakeAnimator();
        go.transform.localScale = new Vector3(1f, 1f, 1f);

        anim.BeginGrowthAnimation(resetToZero: true);

        Assert.True(anim.IsScaling);
        Assert.Equal(Vector3.zero, go.transform.localScale);
    }

    [Fact]
    public void GetCurrentVolume_UsesLossyScale_IncludingParentScaling()
    {
        using var loop = new GameLoop();
        var (anim, go) = MakeAnimator();
        var parent = new GameObject("parent");
        parent.transform.localScale = new Vector3(2f, 1f, 1f);
        go.transform.SetParent(parent.transform);
        go.transform.localScale = new Vector3(2f, 3f, 4f);

        Assert.Equal(48f, anim.GetCurrentVolume(), 3); // (2*2) * 3 * 4
    }

    [Fact]
    public void GetCurrentVolume_WhenDisabled_ReturnsZero()
    {
        using var loop = new GameLoop();
        var (anim, go) = MakeAnimator();
        go.transform.localScale = new Vector3(2f, 3f, 4f);
        anim.enabled = false;

        Assert.Equal(0f, anim.GetCurrentVolume());
    }

    [Fact]
    public void ExecuteOnScaleComplete_RaisesVolumeModified_WithConservedMassDelta()
    {
        using var loop = new GameLoop();
        var rig = PrismTestRig.Create();
        var raised = new List<PrismStats>();
        rig.VolumeEvent.OnRaised += raised.Add;

        // Prism.Awake seeds the conserved-mass record at volume 1 (InitializePrismProperties).
        Assert.Equal(1f, rig.Prism.prismProperties.volume);

        rig.ScaleAnimator.SetTargetScale(new Vector3(2f, 3f, 4f));
        rig.ScaleAnimator.ExecuteOnScaleComplete();

        // V15: the V13-staged 0-delta is gone — UpdateVolume writes the conserved-mass
        // record on PrismProperties and reports the true delta (24 − 1). No decay path
        // exists: the record only moves when an active force rescales the prism.
        Assert.Equal(23f, Assert.Single(raised).Volume);
        Assert.Equal(24f, rig.Prism.prismProperties.volume);
    }

    [Fact]
    public void Initialize_RegistersOnce_AndOnDisableUnregisters()
    {
        using var loop = new GameLoop();
        // Manager arc: registration goes through the real PrismScaleManager now —
        // Initialize() early-returns (upstream contract) when no manager instance exists.
        new GameObject("scaleManager").AddComponent<PrismScaleManager>();
        var (anim, go) = MakeAnimator();

        anim.Initialize();
        Assert.True(IsRegistered(anim));

        anim.Initialize(); // idempotent
        Assert.True(IsRegistered(anim));

        go.SetActive(false); // OnDisable clears registration
        Assert.False(IsRegistered(anim));
    }

    [Fact]
    public void Initialize_WithoutManager_StaysUnregistered_AndPrismStillWorks()
    {
        using var loop = new GameLoop();
        var (anim, go) = MakeAnimator();

        anim.Initialize(); // upstream graceful path: no PrismScaleManager.Instance → no-op

        Assert.False(IsRegistered(anim));

        // The animator still functions standalone: targets clamp, growth arms, and
        // disabling/destroying without a manager neither throws nor unregisters.
        anim.SetTargetScale(new Vector3(3f, 3f, 3f));
        anim.BeginGrowthAnimation();
        Assert.True(anim.IsScaling);

        loop.Tick(0.02f); // nothing drives the scale without a manager — no exception
        Assert.Equal(Vector3.zero, go.transform.localScale);
        Assert.True(anim.IsScaling);
    }
}
