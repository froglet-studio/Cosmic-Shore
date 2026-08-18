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

    // Per-AXIS start fraction (displayed scale at t0 as a fraction of the FINAL
    // scale, per component). float3 so anisotropic retargets stay continuous:
    // Grow() adds GrowthVector along one axis, and the displayed/new-target ratio
    // then differs per axis. Values above 1 are legal — that's a shrink toward
    // the new target (the exponential converges to 1 from either side).
    [MaterialProperty("_GrowStartFrac")]
    public struct PrismGrowStartFracOverride : IComponentData
    {
        public float3 Value;
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
    // The flight velocity is the existing WORLD-space _Velocity (also the
    // shatter-spin axis) — ONE stamped vector; the world->object conversion is
    // GPU-side inside PrismExplosionClock (raw inverse-model multiply, NOT the
    // normalizing Direction-mode Transform node — see PrismClockAnimation.hlsl).

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

    // -- Prism set: ballistic flight (Docs/PRISM_ANIMATION.md §5 C5) --
    // A prism FIRED as a projectile (the Sparrow's Turret Stance). The entity
    // transform is final at the flight's END POINT from the stamp; the vertex
    // stage walks the visual in from the muzzle off these three, on the bullets'
    // own cosine easing. Duration 0 = unstamped = "render where the transform is",
    // which is every other prism in the game.

    [MaterialProperty("_FlightStartTime")]
    public struct PrismFlightStartTimeOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_FlightDuration")]
    public struct PrismFlightDurationOverride : IComponentData
    {
        public float Value;
    }

    /// WORLD-space muzzle velocity in units/second. Velocity * 2*Duration/pi is the
    /// full flight vector; the shader does the world->object conversion with a raw
    /// inverse-model multiply (never a normalizing Transform node).
    [MaterialProperty("_FlightVelocity")]
    public struct PrismFlightVelocityOverride : IComponentData
    {
        public float3 Value;
    }

    // -- Prism set: SHIELD MORPH (Docs/PRISM_ANIMATION.md §5 B4) --
    // The octahedron shield's per-face engage bloom and its shatter overlay (and the
    // stellated super-shield's twin pair). Both run in the vertex stage on the
    // cache-SHARED settled shield mesh, off the per-face centroid the mesh generators
    // bake into TEXCOORD1 — so a shielded prism never leaves the instanced path and
    // same-size shields batch through the whole animation. Duration 0 = unstamped =
    // "render the mesh as authored", which is every prism not mid-transition.

    [MaterialProperty("_ShieldMorphStartTime")]
    public struct PrismShieldMorphStartTimeOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_ShieldMorphDuration")]
    public struct PrismShieldMorphDurationOverride : IComponentData
    {
        public float Value;
    }

    /// &gt;= 0 engage (faces bloom out from their centroids); &lt; 0 shatter (faces
    /// shrink to their centroids while flying out along their normals).
    [MaterialProperty("_ShieldMorphDirection")]
    public struct PrismShieldMorphDirectionOverride : IComponentData
    {
        public float Value;
    }

    /// Shatter fly-out distance in LOCAL units at t = 1 (unused by the bloom).
    [MaterialProperty("_ShieldMorphOffset")]
    public struct PrismShieldMorphOffsetOverride : IComponentData
    {
        public float Value;
    }
    // -- Prism set: super-shield deflection jiggle (Docs/PRISM_ANIMATION.md §5 C14) --
    // A SUPER-SHIELDED prism that absorbed a hit without being destroyed
    // (Prism.AbsorbSuperShieldHit). The vertex stage wobbles each face about the prism's
    // object origin on a precessing, nutating axis and settles to exactly zero at
    // Duration. Duration 0 = unstamped = identity, which is every prism that has never
    // deflected anything. Composes with the shield morph above rather than replacing it:
    // the morph runs first and the wobble rotates its result.
    //
    // Three properties, not four: the per-face and per-prism randomness is derived on the
    // GPU from the face normal and the object-to-world translation, so no seed needs
    // stamping and no mesh channel needs authoring.

    [MaterialProperty("_JiggleStartTime")]
    public struct PrismJiggleStartTimeOverride : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_JiggleDuration")]
    public struct PrismJiggleDurationOverride : IComponentData
    {
        public float Value;
    }

    /// (peak tilt in RADIANS, precession rate rad/s, nutation rate rad/s). Packed into one
    /// float3 because the prism graphs carry Vector1 and Vector3 property donors and no
    /// Vector4 one — synthesising a property type neither graph contains is exactly the
    /// hand-authored schema the asset-surgery protocol forbids (same ruling as
    /// PrismDestructionSight's five globals).
    [MaterialProperty("_JiggleParams")]
    public struct PrismJiggleParamsOverride : IComponentData
    {
        public float3 Value;
    }
}
