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
}
