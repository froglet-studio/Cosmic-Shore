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
// scale math. Material swaps, prismProperties writes, octahedron engage/disengage and
// PrismScaleManager registration are V13 deviations (Prism V15 / MaterialPropertyAnimator
// V14 / PrismScaleManager + PrismOctahedronShield later) and are not asserted here.
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
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static void MakeAudio() => new GameObject("audio").AddComponent<AudioSystem>();

    static (PrismStateManager state, PrismTeamManager team, GameObject go) MakePrism(
        ScriptableEventPrismStats stolenEvent = null)
    {
        var go = new GameObject("prism");
        go.SetActive(false); // configure before Awake (runtime-AddComponent pattern)
        var team = go.AddComponent<PrismTeamManager>();
        if (stolenEvent != null)
            typeof(PrismTeamManager).GetField("onPrismStolen", Priv)!.SetValue(team, stolenEvent);
        var state = go.AddComponent<PrismStateManager>();
        go.SetActive(true);
        return (state, team, go);
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
    public void ExecuteOnScaleComplete_RaisesVolumeModified()
    {
        using var loop = new GameLoop();
        var volumeEvent = ScriptableObject.CreateInstance<ScriptableEventPrismStats>();
        var raised = new List<PrismStats>();
        volumeEvent.OnRaised += raised.Add;
        var (anim, _) = MakeAnimator(volumeEvent: volumeEvent);

        anim.ExecuteOnScaleComplete();

        // Volume delta is 0 until the conserved-mass record on PrismProperties (V14) /
        // Prism (V15) lands — see the V13 deviation in PrismScaleAnimator.UpdateVolume.
        Assert.Equal(0f, Assert.Single(raised).Volume);
    }

    [Fact]
    public void Initialize_RegistersOnce_AndOnDisableUnregisters()
    {
        using var loop = new GameLoop();
        var (anim, go) = MakeAnimator();

        anim.Initialize();
        Assert.True(IsRegistered(anim));

        anim.Initialize(); // idempotent
        Assert.True(IsRegistered(anim));

        go.SetActive(false); // OnDisable clears registration
        Assert.False(IsRegistered(anim));
    }
}
