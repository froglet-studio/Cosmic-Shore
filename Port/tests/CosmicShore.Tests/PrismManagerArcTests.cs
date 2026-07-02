using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// Manager arc: PrismScaleManager + MaterialStateManager (the Unity Jobs + Burst batched
// animation managers, ported as the sanctioned managed-array conversion — sequential
// Execute loops over the same job data; precedent: PrismSpatialIndex/BlockDensityGrid).
// Freezes the exact per-element math both managers batch:
//   scale — lerpSpeed = clamp(GrowthRate·dt, 0.05, 0.1) toward the Min/Max-clamped
//           target; snap + ExecuteOnScaleComplete inside the 0.01 sqr threshold
//   material — progress = min(1, progress + dt/duration); t = smoothstep(0,1,progress);
//           unclamped componentwise lerp of _BrightColor/_DarkColor/_Spread; completion
//           (callback + sharedMaterial swap) at progress ≥ 0.99
// plus registration/unregistration lifecycle (no leaks over a soak), determinism, and
// the upstream graceful no-manager path.
public class PrismManagerArcTests : IDisposable
{
    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    public PrismManagerArcTests()
    {
        ResetManagerStatics();
        // The exact-step math below assumes the engine default fixed step; earlier test
        // classes may leave a different process-global value (e.g. 1/60) behind.
        Time.fixedDeltaTime = 0.02f;
    }

    // Managers live on GameObjects that outlive their GameLoop; clear the process-global
    // Singleton statics after every test so no other test class inherits a stale manager.
    public void Dispose() => ResetManagerStatics();

    static void ResetManagerStatics()
    {
        foreach (var singletonType in new[]
                 {
                     typeof(Singleton<PrismScaleManager>),
                     typeof(Singleton<MaterialStateManager>),
                     typeof(Singleton<PrismTimerManager>),
                     typeof(Singleton<PrismSpatialIndex>),
                 })
        {
            singletonType
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
                .SetValue(null, null);
        }
        typeof(AudioSystem)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    const float Dt = 0.02f; // Time.fixedDeltaTime — one manager step per tick at interval 1

    static PrismScaleManager MakeScaleManager() =>
        new GameObject("prismScaleManager").AddComponent<PrismScaleManager>();

    static MaterialStateManager MakeMaterialManager() =>
        new GameObject("materialStateManager").AddComponent<MaterialStateManager>();

    static Material MakeMaterial(string name, Color bright, Color dark, Vector3 spread)
    {
        var material = new Material(Shader.Find("CosmicShore/Prism")) { name = name };
        material.SetColor("_BrightColor", bright);
        material.SetColor("_DarkColor", dark);
        material.SetVector("_Spread", new Vector4(spread.x, spread.y, spread.z, 0f));
        return material;
    }

    /// <summary>Full prism rig whose theme carries REAL block materials (every domain),
    /// so MaterialPropertyAnimator.ValidateMaterials resolves and animations can run.</summary>
    static PrismRig MakeThemedRig(Material opaque, Material transparent, string name = "prism")
        => PrismTestRig.Create(name, beforeActivate: rig =>
        {
            foreach (var set in rig.Theme.TeamMaterialSets.Values)
            {
                set.BlockMaterial = opaque;
                set.TransparentBlockMaterial = transparent;
            }
        });

    static bool IsRegistered(MaterialPropertyAnimator animator) =>
        (bool)typeof(MaterialPropertyAnimator).GetField("isRegistered", Priv)!.GetValue(animator);

    // ── PrismScaleManager: exact growth math ────────────────────────────────

    [Theory]
    [InlineData(1f)]   // 1·0.02 = 0.02 → clamped UP to the 0.05 floor
    [InlineData(4f)]   // 4·0.02 = 0.08 → inside the clamp window, used as-is
    [InlineData(100f)] // 100·0.02 = 2 → clamped DOWN to the 0.1 ceiling
    public void ScaleManager_SingleStep_UsesClampedGrowthLerp(float growthRate)
    {
        using var loop = new GameLoop();
        MakeScaleManager();
        var rig = PrismTestRig.Create();
        var anim = rig.ScaleAnimator;

        anim.Initialize();
        anim.GrowthRate = growthRate;
        anim.SetTargetScale(new Vector3(4f, 4f, 4f));
        anim.BeginGrowthAnimation(); // localScale is zero (Awake contract)

        loop.Tick(Dt); // exactly one manager step of effectiveDeltaTime = fixedDeltaTime

        float lerpSpeed = Mathf.Clamp(growthRate * Dt, 0.05f, 0.1f);
        var expected = Vector3.LerpUnclamped(Vector3.zero, new Vector3(4f, 4f, 4f), lerpSpeed);
        Assert.Equal(expected, rig.GameObject.transform.localScale);
        Assert.True(anim.IsScaling);
    }

    [Fact]
    public void ScaleManager_ReachesTarget_SnapsExactly_AndCompletes()
    {
        using var loop = new GameLoop();
        var manager = MakeScaleManager();
        var rig = PrismTestRig.Create();
        var anim = rig.ScaleAnimator;
        var volumeRaises = new List<PrismStats>();
        rig.VolumeEvent.OnRaised += volumeRaises.Add;

        anim.Initialize();
        anim.GrowthRate = 5f; // 5·0.02 = 0.1 per step
        anim.SetTargetScale(new Vector3(2f, 2f, 2f));
        anim.BeginGrowthAnimation();
        Assert.Equal(1, manager.ActiveAnimatorCount);

        loop.Run(60, Dt); // geometric approach crosses the 0.01 sqr threshold well within 60 steps

        // Snap semantics: the completion queue writes the clamped target EXACTLY.
        Assert.Equal(new Vector3(2f, 2f, 2f), rig.GameObject.transform.localScale);
        Assert.False(anim.IsScaling);
        Assert.Equal(0, manager.ActiveAnimatorCount);
        Assert.Equal(1, manager.RegisteredAnimatorCount);

        // Completion contract: ExecuteOnScaleComplete ran once — conserved-mass volume
        // record moves 1 → 8 and the delta is raised on the SOAP channel.
        Assert.Equal(7f, Assert.Single(volumeRaises).Volume);
        Assert.Equal(8f, rig.Prism.prismProperties.volume);
    }

    [Fact]
    public void ScaleManager_ClampsTargetIntoLiveMinMax_AndFlagsLargest()
    {
        using var loop = new GameLoop();
        new GameObject("audio").AddComponent<AudioSystem>(); // largest → ActivateShield → SFX
        MakeScaleManager();
        var rig = PrismTestRig.Create();
        var anim = rig.ScaleAnimator;

        anim.Initialize();
        anim.GrowthRate = 5f;
        anim.SetTargetScale(new Vector3(8f, 8f, 8f)); // legal against the authored max (10)
        anim.MaxScale = new Vector3(2f, 2f, 2f);      // live re-clamp: the manager applies
                                                      // Vector3.Min(Vector3.Max(target, Min), Max) per frame
        anim.BeginGrowthAnimation();

        loop.Run(120, Dt);

        // Manager animated toward the CLAMPED target and snapped there; the completion
        // sees TargetScale (8) above MaxScale (2) → shields + flags largest (upstream).
        Assert.Equal(new Vector3(2f, 2f, 2f), rig.GameObject.transform.localScale);
        Assert.False(anim.IsScaling);
        Assert.True(rig.Prism.IsLargest);
        Assert.Equal(BlockState.Shielded, rig.StateManager.CurrentState);
    }

    // ── PrismScaleManager: lifecycle / soak / determinism ───────────────────

    [Fact]
    public void ScaleManager_Soak_RegistrationAndActiveSetsDrainToZero()
    {
        using var loop = new GameLoop();
        var manager = MakeScaleManager();

        for (int wave = 0; wave < 3; wave++)
        {
            var rigs = new List<PrismRig>();
            for (int i = 0; i < 40; i++)
            {
                var rig = PrismTestRig.Create($"prism-w{wave}-{i}");
                rigs.Add(rig);
                var anim = rig.ScaleAnimator;
                anim.Initialize();
                anim.GrowthRate = 5f;
                anim.SetTargetScale(new Vector3(1f + i % 3, 2f, 2f));
                anim.BeginGrowthAnimation();
            }
            Assert.Equal(40, manager.RegisteredAnimatorCount);
            Assert.Equal(40, manager.ActiveAnimatorCount);

            loop.Run(90, Dt); // run every animator to completion

            Assert.Equal(0, manager.ActiveAnimatorCount);
            Assert.Equal(40, manager.RegisteredAnimatorCount);
            foreach (var rig in rigs)
            {
                Assert.False(rig.ScaleAnimator.IsScaling);
                Engine.Object.Destroy(rig.GameObject);
            }

            loop.Tick(Dt); // flush destroy queue → OnDisable/OnDestroy unregister

            Assert.Equal(0, manager.RegisteredAnimatorCount);
            Assert.Equal(0, manager.ActiveAnimatorCount);
        }
    }

    [Fact]
    public void ScaleManager_SameSequence_ProducesIdenticalEndState()
    {
        var first = RunScaleScenario();
        var second = RunScaleScenario();

        Assert.Equal(first.Length, second.Length);
        for (int i = 0; i < first.Length; i++)
            Assert.Equal(first[i], second[i]); // exact float equality — determinism freeze
    }

    Vector3[] RunScaleScenario()
    {
        ResetManagerStatics();
        using var loop = new GameLoop();
        MakeScaleManager();

        var rigs = new PrismRig[5];
        for (int i = 0; i < rigs.Length; i++)
        {
            rigs[i] = PrismTestRig.Create($"prism-{i}");
            var anim = rigs[i].ScaleAnimator;
            anim.Initialize();
            anim.GrowthRate = 0.5f + i; // spans the clamp floor and interior
            anim.SetTargetScale(new Vector3(1f + i, 2f + i, 3f));
            anim.BeginGrowthAnimation();
        }

        loop.Run(30, Dt); // mid-flight for the slow animators, complete for the fast

        var scales = new Vector3[rigs.Length];
        for (int i = 0; i < rigs.Length; i++)
            scales[i] = rigs[i].GameObject.transform.localScale;
        return scales;
    }

    // ── MaterialStateManager: exact lerp batching ───────────────────────────

    // Dyadic-rational channel values keep every lerp step exact in float32.
    static readonly Color StartBright = new Color(0f, 0.25f, 0.5f, 1f);
    static readonly Color StartDark = new Color(0.25f, 0f, 0.75f, 1f);
    static readonly Color TargetBright = new Color(1f, 0.75f, 0.25f, 1f);
    static readonly Color TargetDark = new Color(0.5f, 1f, 0f, 1f);
    static readonly Vector3 StartSpread = new Vector3(0f, 0.5f, 1f);
    static readonly Vector3 TargetSpread = new Vector3(2f, 1f, 0f);

    (PrismRig rig, Material opaqueTarget, Material transparentTarget) MakeMaterialScenario()
    {
        var opaqueStart = MakeMaterial("opaque-start", StartBright, StartDark, StartSpread);
        var transparentStart = MakeMaterial("transparent-start", StartBright, StartDark, StartSpread);
        var opaqueTarget = MakeMaterial("opaque-target", TargetBright, TargetDark, TargetSpread);
        var transparentTarget = MakeMaterial("transparent-target", TargetBright, TargetDark, TargetSpread);
        var rig = MakeThemedRig(opaqueStart, transparentStart);
        return (rig, opaqueTarget, transparentTarget);
    }

    [Fact]
    public void MaterialManager_MidAnimation_WritesExactSmoothstepLerp()
    {
        using var loop = new GameLoop();
        MakeMaterialManager();
        var (rig, opaqueTarget, transparentTarget) = MakeMaterialScenario();

        // duration 0.08 → progress advances exactly 0.25 per 0.02 step
        rig.MaterialAnimator.UpdateMaterial(transparentTarget, opaqueTarget, duration: 0.08f);
        loop.Tick(Dt);

        float progress = Mathf.Min(1f, 0f + Dt / 0.08f);
        Assert.Equal(progress, rig.MaterialAnimator.AnimationProgress);

        float t = Mathf.SmoothStep(0f, 1f, progress); // 0.15625 — exact
        var block = new MaterialPropertyBlock();
        rig.MaterialAnimator.MeshRenderer.GetPropertyBlock(block);
        Assert.Equal(LerpColor(StartBright, TargetBright, t), block.GetColor("_BrightColor"));
        Assert.Equal(LerpColor(StartDark, TargetDark, t), block.GetColor("_DarkColor"));
        var spread = Vector3.LerpUnclamped(StartSpread, TargetSpread, t);
        Assert.Equal(new Vector4(spread.x, spread.y, spread.z, 0f), block.GetVector("_Spread"));
    }

    static Color LerpColor(Color a, Color b, float t) => new Color(
        a.r + (b.r - a.r) * t,
        a.g + (b.g - a.g) * t,
        a.b + (b.b - a.b) * t,
        a.a + (b.a - a.a) * t); // the manager's float4 math.lerp, per channel

    [Fact]
    public void MaterialManager_ReachesTargets_FiresCompletion_SwapsSharedMaterial()
    {
        using var loop = new GameLoop();
        var manager = MakeMaterialManager();
        var (rig, opaqueTarget, transparentTarget) = MakeMaterialScenario();
        Assert.Equal(1, manager.RegisteredAnimatorCount); // Awake auto-registered

        int completions = 0;
        rig.MaterialAnimator.UpdateMaterial(transparentTarget, opaqueTarget, duration: 0.08f,
            onComplete: () => completions++);
        Assert.True(rig.MaterialAnimator.IsAnimating);
        Assert.Equal(1, manager.ActiveAnimatorCount);

        loop.Run(3, Dt); // progress 0.75 < 0.99 — still animating
        Assert.True(rig.MaterialAnimator.IsAnimating);
        Assert.Equal(0, completions);

        loop.Tick(Dt); // progress hits 1.0 exactly → completion

        Assert.Equal(1, completions);
        Assert.False(rig.MaterialAnimator.IsAnimating);
        Assert.Equal(0, manager.ActiveAnimatorCount);
        Assert.Null(rig.MaterialAnimator.OnAnimationComplete);
        Assert.Equal(1f, rig.MaterialAnimator.AnimationProgress);

        // Colors land on the targets exactly (t = smoothstep(1) = 1, dyadic channels).
        var block = new MaterialPropertyBlock();
        rig.MaterialAnimator.MeshRenderer.GetPropertyBlock(block);
        Assert.Equal(TargetBright, block.GetColor("_BrightColor"));
        Assert.Equal(TargetDark, block.GetColor("_DarkColor"));
        Assert.Equal(new Vector4(TargetSpread.x, TargetSpread.y, TargetSpread.z, 0f), block.GetVector("_Spread"));

        // Completion swaps the sharedMaterial (prism is opaque → opaque target).
        Assert.Same(opaqueTarget, rig.MaterialAnimator.MeshRenderer.sharedMaterial);
    }

    [Fact]
    public void MaterialManager_BatchesManyAnimators_AllReachTargetsAndComplete()
    {
        using var loop = new GameLoop();
        var manager = MakeMaterialManager();

        var rigs = new List<(PrismRig rig, Material opaqueTarget)>();
        int completions = 0;
        for (int i = 0; i < 20; i++)
        {
            var (rig, opaqueTarget, transparentTarget) = MakeMaterialScenario();
            rigs.Add((rig, opaqueTarget));
            rig.MaterialAnimator.UpdateMaterial(transparentTarget, opaqueTarget, duration: 0.08f,
                onComplete: () => completions++);
        }
        Assert.Equal(20, manager.ActiveAnimatorCount);

        loop.Run(5, Dt);

        Assert.Equal(20, completions);
        Assert.Equal(0, manager.ActiveAnimatorCount);
        foreach (var (rig, opaqueTarget) in rigs)
        {
            Assert.False(rig.MaterialAnimator.IsAnimating);
            var block = new MaterialPropertyBlock();
            rig.MaterialAnimator.MeshRenderer.GetPropertyBlock(block);
            Assert.Equal(TargetBright, block.GetColor("_BrightColor"));
            Assert.Same(opaqueTarget, rig.MaterialAnimator.MeshRenderer.sharedMaterial);
        }

        // Lifecycle: destroying the prisms unregisters every animator (no leaks).
        foreach (var (rig, _) in rigs)
            Engine.Object.Destroy(rig.GameObject);
        loop.Tick(Dt);
        Assert.Equal(0, manager.RegisteredAnimatorCount);
    }

    // ── graceful no-manager paths (upstream contract) ───────────────────────

    [Fact]
    public void MaterialAnimator_WithoutManager_StaysUnregistered_AndDoesNotAnimate()
    {
        using var loop = new GameLoop();
        var (rig, opaqueTarget, transparentTarget) = MakeMaterialScenario();

        Assert.False(IsRegistered(rig.MaterialAnimator));

        bool completed = false;
        rig.MaterialAnimator.UpdateMaterial(transparentTarget, opaqueTarget, duration: 0.08f,
            onComplete: () => completed = true);
        Assert.True(rig.MaterialAnimator.IsAnimating); // armed, but nothing drives it

        loop.Run(10, Dt);

        Assert.True(rig.MaterialAnimator.IsAnimating); // no manager → no progress (upstream)
        Assert.False(completed);
        Assert.Equal(0f, rig.MaterialAnimator.AnimationProgress);

        Engine.Object.Destroy(rig.GameObject); // OnDisable/OnDestroy no-manager guards hold
        loop.Tick(Dt);
    }
}
