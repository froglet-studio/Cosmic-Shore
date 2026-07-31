using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace CosmicShore.ECS
{
    /// <summary>
    /// Per-instance shader property overrides for entity-rendered prisms.
    /// These mirror the three properties MaterialPropertyAnimator animates via
    /// MaterialPropertyBlock on the legacy path (_BrightColor / _DarkColor /
    /// _Spread on UnstablePrismGraph). Entities Graphics uploads them into the
    /// persistent DOTS-instancing buffer, so thousands of prisms sharing one
    /// material still batch into a single draw while keeping unique colors.
    ///
    /// Sizes must match the ShaderGraph property declarations exactly:
    /// Color → float4, Vector3 → float3 (verified against
    /// UnstablePrismGraph.shadergraph).
    /// </summary>
    [MaterialProperty("_BrightColor")]
    public struct PrismBrightColorOverride : IComponentData
    {
        public float4 Value;
    }

    [MaterialProperty("_DarkColor")]
    public struct PrismDarkColorOverride : IComponentData
    {
        public float4 Value;
    }

    [MaterialProperty("_Spread")]
    public struct PrismSpreadOverride : IComponentData
    {
        public float3 Value;
    }

    // ----------------------------------------------------------------------
    // Explosion VFX overrides (ExplodingBlockGraph: _Velocity f3,
    // _ExplosionAmount f1, _Opacity f1 — sizes verified against the graph).
    // ----------------------------------------------------------------------

    [MaterialProperty("_Velocity")]
    public struct PrismVelocityOverride : IComponentData
    {
        public float3 Value;
    }

    [MaterialProperty("_ExplosionAmount")]
    public struct PrismExplosionAmountOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_Opacity")]
    public struct PrismOpacityOverride : IComponentData
    {
        public float Value;
    }

    // ----------------------------------------------------------------------
    // Implosion / grow VFX overrides (SuctionGraph: _State f1, _Location f3).
    // ----------------------------------------------------------------------

    [MaterialProperty("_State")]
    public struct PrismImplosionStateOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_Location")]
    public struct PrismImplosionLocationOverride : IComponentData
    {
        public float3 Value;
    }

    // ----------------------------------------------------------------------
    // Clock-material animation stamps (Docs/PRISM_ANIMATION.md §4, LOCKED law).
    // These carry INITIAL CONDITIONS, never per-frame samples: the CPU writes
    // each once at animation start (PrismRenderService.Stamp*), the shader
    // evaluates the visual as f(_Time.y, stamp) via PrismClockAnimation.hlsl,
    // and a scheduled end-swap settles the prism. Defaults are the settled
    // state (rate/duration 0), so unstamped materials render unchanged.
    // Added to the prototype archetypes ONLY when PrismRenderService.
    // ClockAnimationEnabled (the graphs must declare the matching properties
    // as Hybrid Per Instance first — see PRISM_ANIMATION.md §4.4).
    // ----------------------------------------------------------------------

    // -- Prism set (BlockGraph): grow-in bloom + color/state transition --

    [MaterialProperty("_GrowStartTime")]
    public struct PrismGrowStartTimeOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_GrowRate")]
    public struct PrismGrowRateOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_GrowStartFrac")]
    public struct PrismGrowStartFracOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_ColorStartTime")]
    public struct PrismColorStartTimeOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_ColorDuration")]
    public struct PrismColorDurationOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_StartBrightColor")]
    public struct PrismStartBrightColorOverride : IComponentData
    {
        public float4 Value;
    }

    [MaterialProperty("_StartDarkColor")]
    public struct PrismStartDarkColorOverride : IComponentData
    {
        public float4 Value;
    }

    [MaterialProperty("_StartSpread")]
    public struct PrismStartSpreadOverride : IComponentData
    {
        public float3 Value;
    }

    // -- Explosion set (ExplodingBlockGraph): debris flight clock --

    [MaterialProperty("_ExplodeStartTime")]
    public struct PrismExplodeStartTimeOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_ExplodeSpeed")]
    public struct PrismExplodeSpeedOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_ExplodeDuration")]
    public struct PrismExplodeDurationOverride : IComponentData
    {
        public float Value;
    }

    // -- Implosion set (SuctionGraph): suction/reverse-grow clock --

    [MaterialProperty("_SuctionStartTime")]
    public struct PrismSuctionStartTimeOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_SuctionDuration")]
    public struct PrismSuctionDurationOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_SuctionDirection")]
    public struct PrismSuctionDirectionOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_SuctionGrowDelay")]
    public struct PrismSuctionGrowDelayOverride : IComponentData
    {
        public float Value;
    }
}
