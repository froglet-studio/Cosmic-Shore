using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// V15: Prism lifecycle (pool initialize → deferred creation → damage/consume →
// restore), conserved-mass volume bookkeeping (Docs/ECOSYSTEM.md: a prism is only
// ever removed or resized by an ACTIVE force — nothing here exercises or expects
// decay), and PrismAOERegistry spatial queries over the managed-array port
// (register/unregister free-list, flag-packed skip logic, radius math, per-frame
// damage application). Cell density-grid registration stays staged (Cell V12).
public class PrismTests
{
    public PrismTests()
    {
        // Singleton<T>.Instance / AudioSystem.Instance are process-global statics; a
        // previous test's GameLoop disposal does not destroy its objects, so stale
        // instances must be cleared or Singleton<T>.Awake would Destroy the new one.
        typeof(Singleton<PrismTimerManager>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
        typeof(Singleton<PrismAOERegistry>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
        typeof(AudioSystem)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static void MakeAudio() => new GameObject("audio").AddComponent<AudioSystem>();

    static List<PrismStats> Capture(CosmicShore.ScriptableObjects.ScriptableEventPrismStats channel)
    {
        var raised = new List<PrismStats>();
        channel.OnRaised += raised.Add;
        return raised;
    }

    static List<PrismEventData> CaptureImpacts(PrismRig rig)
    {
        var impacts = new List<PrismEventData>();
        rig.ImpactChannel.OnEventReturn += data => { impacts.Add(data); return default; };
        return impacts;
    }

    // ── Prism lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public void Initialize_HidesGeometry_ThenCreatesAfterWaitTime()
    {
        using var loop = new GameLoop();
        var rig = PrismTestRig.Create();
        var created = Capture(rig.CreatedEvent);
        var collider = rig.GameObject.GetComponent<BoxCollider>();
        var renderer = rig.GameObject.GetComponent<MeshRenderer>();

        rig.Prism.Initialize("P1");

        Assert.Equal("P1", rig.Prism.PlayerName);
        Assert.False(collider.enabled);
        Assert.False(renderer.enabled);
        Assert.Empty(created);

        loop.Tick(0.7f); // waitTime (0.6s) elapses → CreateBlockCoroutine completes

        Assert.True(collider.enabled);
        Assert.True(renderer.enabled);
        Assert.Equal("P1", Assert.Single(created).OwnName);
        // Creation auto-registers with the (auto-created) AOE registry.
        Assert.NotNull(PrismAOERegistry.Instance);
        Assert.True(PrismAOERegistry.Instance.IsAvailable);
    }

    [Fact]
    public void Initialize_DestroyedDuringWaitTime_DoesNotResurrect()
    {
        using var loop = new GameLoop();
        var rig = PrismTestRig.Create();
        var created = Capture(rig.CreatedEvent);
        var destroyed = Capture(rig.DestroyedEvent);

        rig.Prism.Initialize("P1");
        rig.Prism.Damage(Vector3.forward, Domains.Ruby, "Attacker"); // AOE within waitTime

        loop.Tick(0.7f);

        Assert.True(rig.Prism.destroyed);
        Assert.Single(destroyed);
        Assert.Empty(created); // creation aborted — no resurrection
        Assert.False(rig.GameObject.GetComponent<MeshRenderer>().enabled);
        Assert.False(rig.GameObject.GetComponent<BoxCollider>().enabled);
    }

    [Fact]
    public void Damage_Unshielded_ExplodesOnce_AndSecondHitIsNoOp()
    {
        using var loop = new GameLoop();
        var rig = PrismTestRig.Create();
        var destroyed = Capture(rig.DestroyedEvent);
        var impacts = CaptureImpacts(rig);

        rig.Prism.Damage(Vector3.forward, Domains.Ruby, "Attacker");
        rig.Prism.Damage(Vector3.forward, Domains.Ruby, "Attacker"); // idempotency guard

        Assert.True(rig.Prism.destroyed);
        Assert.False(rig.Prism.devastated);
        var stats = Assert.Single(destroyed);
        Assert.Equal("Attacker", stats.AttackerName);
        Assert.Equal(1f, stats.Volume); // SetupDestruction floors the record at 1 (conserved mass, never 0)
        var impact = Assert.Single(impacts);
        Assert.Equal(PrismType.Explosion, impact.PrismType);
    }

    [Fact]
    public void Damage_Shielded_DropsShieldInsteadOfDestroying()
    {
        using var loop = new GameLoop();
        MakeAudio();
        var rig = PrismTestRig.Create();
        rig.StateManager.ActivateShield();
        Assert.True(rig.Prism.prismProperties.IsShielded);

        rig.Prism.Damage(Vector3.forward, Domains.Ruby, "Attacker");

        Assert.False(rig.Prism.destroyed);
        Assert.False(rig.Prism.prismProperties.IsShielded);
        Assert.Equal(BlockState.Normal, rig.StateManager.CurrentState);
    }

    [Fact]
    public void Damage_SuperShielded_IsFullyInvulnerable()
    {
        using var loop = new GameLoop();
        var rig = PrismTestRig.Create();
        rig.StateManager.ActivateSuperShield();

        rig.Prism.Damage(Vector3.forward, Domains.Ruby, "Attacker");

        Assert.False(rig.Prism.destroyed);
        Assert.True(rig.Prism.prismProperties.IsSuperShielded);
        Assert.Equal(BlockState.SuperShielded, rig.StateManager.CurrentState);
    }

    [Fact]
    public void Damage_Devastate_BypassesShield()
    {
        using var loop = new GameLoop();
        MakeAudio();
        var rig = PrismTestRig.Create();
        rig.StateManager.ActivateShield();

        rig.Prism.Damage(Vector3.forward, Domains.Ruby, "Attacker", devastate: true);

        Assert.True(rig.Prism.destroyed);
        Assert.True(rig.Prism.devastated);
    }

    [Fact]
    public void Consume_ImplodesTowardTarget()
    {
        using var loop = new GameLoop();
        var rig = PrismTestRig.Create();
        var impacts = CaptureImpacts(rig);
        var target = new GameObject("consumer").transform;

        rig.Prism.Consume(target, Domains.Gold, "Forager");

        Assert.True(rig.Prism.destroyed);
        var impact = Assert.Single(impacts);
        Assert.Equal(PrismType.Implosion, impact.PrismType);
        Assert.Same(target, impact.TargetTransform);
    }

    [Fact]
    public void Restore_RevivesAndRaisesRestored_ButNotWhenDevastated()
    {
        using var loop = new GameLoop();
        var rig = PrismTestRig.Create();
        var restored = Capture(rig.RestoredEvent);

        rig.Prism.Damage(Vector3.forward, Domains.Ruby, "Attacker");
        rig.Prism.Restore();

        Assert.False(rig.Prism.destroyed);
        Assert.Single(restored);
        Assert.True(rig.GameObject.GetComponent<BoxCollider>().enabled);
        Assert.True(rig.GameObject.GetComponent<MeshRenderer>().enabled);

        rig.Prism.Damage(Vector3.forward, Domains.Ruby, "Attacker", devastate: true);
        rig.Prism.Restore(); // devastated mass does not come back

        Assert.True(rig.Prism.destroyed);
        Assert.Single(restored);
    }

    // ── Conserved-mass volume bookkeeping ────────────────────────────────────

    [Fact]
    public void Grow_IsAnActiveForce_ExpandingTargetScaleByGrowthVector()
    {
        using var loop = new GameLoop();
        var rig = PrismTestRig.Create();
        Assert.Equal(new Vector3(1f, 1f, 1f), rig.ScaleAnimator.TargetScale); // authored prefab scale

        rig.Prism.Grow(2f); // 2 × GrowthVector (0,2,0)

        Assert.Equal(new Vector3(1f, 5f, 1f), rig.ScaleAnimator.TargetScale);
        Assert.True(rig.ScaleAnimator.IsScaling);
    }

    [Fact]
    public void VolumeRecord_OnlyMovesThroughActiveForces()
    {
        using var loop = new GameLoop();
        var rig = PrismTestRig.Create();
        var volumeEvents = Capture(rig.VolumeEvent);
        Assert.Equal(1f, rig.Prism.prismProperties.volume); // seeded by InitializePrismProperties

        // Ticking time alone never changes the conserved-mass record (no decay).
        loop.Tick(5f);
        loop.Tick(5f);
        Assert.Equal(1f, rig.Prism.prismProperties.volume);
        Assert.Empty(volumeEvents);

        // An active rescale moves it and reports the exact delta.
        rig.ScaleAnimator.SetTargetScale(new Vector3(2f, 2f, 2f));
        rig.ScaleAnimator.ExecuteOnScaleComplete();
        Assert.Equal(8f, rig.Prism.prismProperties.volume);
        Assert.Equal(7f, Assert.Single(volumeEvents).Volume);
    }

    // ── PrismAOERegistry: registration ───────────────────────────────────────

    [Fact]
    public void Register_AssignsSequentialIndices_AndUnregisterRecyclesViaFreeList()
    {
        using var loop = new GameLoop();
        var registry = PrismAOERegistry.EnsureInstance();
        Assert.True(registry.IsAvailable);

        var a = PrismTestRig.Create("a").Prism;
        var b = PrismTestRig.Create("b").Prism;
        var c = PrismTestRig.Create("c").Prism;

        Assert.Equal(0, registry.Register(a));
        Assert.Equal(1, registry.Register(b));

        registry.Unregister(0);
        Assert.Equal(0, registry.Register(c)); // freed slot reused before the high-water mark grows
    }

    // ── PrismAOERegistry: spatial query + damage application ────────────────

    [Fact]
    public void ProcessExplosionFrame_DamagesEnemiesInsideRadiusOnly()
    {
        using var loop = new GameLoop();
        var registry = PrismAOERegistry.EnsureInstance();

        var insideRig = PrismTestRig.Create("inside");
        insideRig.GameObject.transform.position = new Vector3(0f, 0f, 9.9f);
        insideRig.Prism.Domain = Domains.Ruby;
        int insideIdx = registry.Register(insideRig.Prism);

        var outsideRig = PrismTestRig.Create("outside");
        outsideRig.GameObject.transform.position = new Vector3(0f, 0f, 10.1f);
        outsideRig.Prism.Domain = Domains.Ruby;
        registry.Register(outsideRig.Prism);

        var insideDestroyed = Capture(insideRig.DestroyedEvent);
        var alreadyHit = new HashSet<int>();
        bool shouldContinue = registry.ProcessExplosionFrame(
            Vector3.zero, radius: 10f, speed: 5f, inertia: 1f,
            explosionDomain: Domains.Jade, affectSelf: false, destructive: true,
            devastating: false, shielding: false, anonymous: true,
            vessel: null, alreadyHit: alreadyHit);

        Assert.True(shouldContinue);
        Assert.True(insideRig.Prism.destroyed);
        Assert.False(outsideRig.Prism.destroyed);
        Assert.Equal(insideIdx, Assert.Single(alreadyHit));
        Assert.Equal("🔥GuyFawkes🔥", Assert.Single(insideDestroyed).AttackerName); // anonymous attacker
    }

    [Fact]
    public void ProcessExplosionFrame_AlreadyHitPrisms_AreNotHitTwice()
    {
        using var loop = new GameLoop();
        var registry = PrismAOERegistry.EnsureInstance();
        var rig = PrismTestRig.Create();
        registry.Register(rig.Prism);
        var destroyed = Capture(rig.DestroyedEvent);
        var alreadyHit = new HashSet<int>();

        registry.ProcessExplosionFrame(Vector3.zero, 10f, 5f, 1f, Domains.Jade,
            affectSelf: false, destructive: true, devastating: false, shielding: false,
            anonymous: true, vessel: null, alreadyHit: alreadyHit);
        registry.ProcessExplosionFrame(Vector3.zero, 10f, 5f, 1f, Domains.Jade,
            affectSelf: false, destructive: true, devastating: false, shielding: false,
            anonymous: true, vessel: null, alreadyHit: alreadyHit);

        Assert.Single(destroyed); // OnTriggerEnter once-per-pair semantics preserved
    }

    [Fact]
    public void ProcessExplosionFrame_ShieldsSameDomainPrisms_InsteadOfDamaging()
    {
        using var loop = new GameLoop();
        MakeAudio();
        var registry = PrismAOERegistry.EnsureInstance();
        var rig = PrismTestRig.Create();
        rig.Prism.Domain = Domains.Jade;
        registry.Register(rig.Prism);

        bool shouldContinue = registry.ProcessExplosionFrame(Vector3.zero, 10f, 5f, 1f,
            explosionDomain: Domains.Jade, affectSelf: false, destructive: true,
            devastating: false, shielding: false, anonymous: true,
            vessel: null, alreadyHit: new HashSet<int>());

        Assert.True(shouldContinue);
        Assert.False(rig.Prism.destroyed);
        Assert.Equal(BlockState.Shielded, rig.StateManager.CurrentState);
        Assert.True(rig.Prism.prismProperties.IsShielded);
    }

    [Fact]
    public void ProcessExplosionFrame_SuperShieldedPrism_StopsTheExplosion()
    {
        using var loop = new GameLoop();
        var registry = PrismAOERegistry.EnsureInstance();
        var rig = PrismTestRig.Create();
        rig.Prism.Domain = Domains.Ruby;
        rig.StateManager.ActivateSuperShield(); // flags captured at Register time
        registry.Register(rig.Prism);

        bool shouldContinue = registry.ProcessExplosionFrame(Vector3.zero, 10f, 5f, 1f,
            explosionDomain: Domains.Jade, affectSelf: false, destructive: true,
            devastating: false, shielding: false, anonymous: true,
            vessel: null, alreadyHit: new HashSet<int>());

        Assert.False(shouldContinue); // explosion physically blocked
        Assert.False(rig.Prism.destroyed);
        Assert.Equal(BlockState.SuperShielded, rig.StateManager.CurrentState);
    }

    [Fact]
    public void MarkDestroyed_MakesTheSpatialScanSkipTheSlot()
    {
        using var loop = new GameLoop();
        var registry = PrismAOERegistry.EnsureInstance();
        var rig = PrismTestRig.Create();
        rig.Prism.Domain = Domains.Ruby;
        int idx = registry.Register(rig.Prism);
        registry.MarkDestroyed(idx);
        var destroyed = Capture(rig.DestroyedEvent);
        var alreadyHit = new HashSet<int>();

        registry.ProcessExplosionFrame(Vector3.zero, 10f, 5f, 1f, Domains.Jade,
            affectSelf: false, destructive: true, devastating: false, shielding: false,
            anonymous: true, vessel: null, alreadyHit: alreadyHit);

        Assert.Empty(destroyed);
        Assert.Empty(alreadyHit); // filtered out in phase 1 — never reaches damage logic
    }
}
