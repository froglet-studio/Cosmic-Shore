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
}
