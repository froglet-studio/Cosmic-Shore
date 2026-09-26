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

    /// <summary>
    /// Where each face of this debris spins: 0 = the pivot RotateFacesAlongAxis derives
    /// (`dot(P,N)*N` plus a fixed tangent slide, a hardcoded measurement of the prism
    /// CUBE's four-wedge face), 1 = the per-face CENTROID the mesh bakes into TEXCOORD1.
    /// Docs/PRISM_ANIMATION.md §4.8.2.
    ///
    /// Per-INSTANCE and not per-material, because a dropped shield sheds through the same
    /// ExplodingBlockMaterial a dying prism's pieces use (§4.8.1) — that shared material is
    /// the thing keeping the two death visuals identical, so the mesh's face layout has to
    /// travel with the entity instead. Costs one float per debris entity and one `mad` in
    /// the vertex stage; a shader keyword or a second material would have split the batch
    /// AND forked the pipeline.
    /// </summary>
    [MaterialProperty("_FacePivotFromCentroid")]
    public struct PrismFacePivotFromCentroidOverride : IComponentData
    {
        public float Value;
    }

    // -- Implosion set (SuctionGraph): suction/reverse-grow clock; also the live
    // Prism set (BlockGraph / ExplodingBlockGraph, Docs/PRISM_ANIMATION.md §5 C9
    // cell-swap suction) --

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
    /// PrismLit's five globals).
    [MaterialProperty("_JiggleParams")]
    public struct PrismJiggleParamsOverride : IComponentData
    {
        public float3 Value;
    }

    // ----------------------------------------------------------------------
    // Living-mass sway (Docs/ECOSYSTEM.md §47). A health prism bolted to a swaying
    // spindle reads the LIMB'S OWN shear field, evaluated at its own vertices, so the
    // two move together exactly and the lockup that reads as one creature holds.
    //
    // All four are constants of the prism's ATTACHMENT, baked once when it is bound:
    // a prism does not move relative to the limb it is part of, so nothing here is
    // ever re-computed. A zero span is an exact no-op and is the default, which is
    // why a vessel's trail, an authored environment and a dead lifeform's skeleton
    // are bit-identical to before — living mass is the mass that moves.
    // ----------------------------------------------------------------------

    /// The limb's +x axis expressed in THIS prism's object space, premultiplied by the
    /// limb's sway amplitude. The change of basis is what makes a rib pitched off the
    /// fin bend WITH the fin rather than across it, and it carries the prism's own
    /// (often non-uniform) leaf scale for free.
    [MaterialProperty("_SwaySpanX")]
    public struct PrismSwaySpanXOverride : IComponentData
    {
        public float3 Value;
    }

    /// The limb's +y axis, same basis and same amplitude — the secondary wave's axis.
    [MaterialProperty("_SwaySpanY")]
    public struct PrismSwaySpanYOverride : IComponentData
    {
        public float3 Value;
    }

    /// The limb's +z as a linear FUNCTIONAL on this prism's object space, so
    /// dot(PositionOS, Axis) is a vertex's height up the limb measured from the prism's
    /// own origin. Not a direction — a row of the basis change, so it is correct under
    /// non-uniform scale where a normalized axis would not be.
    [MaterialProperty("_SwayAxis")]
    public struct PrismSwayAxisOverride : IComponentData
    {
        public float3 Value;
    }

    /// (Frequency rad/s, Phase rad, Z0 = the prism ORIGIN's height up the limb). Three
    /// scalars in one float3 for the reason _JiggleParams records: the prism graphs
    /// carry Vector1 and Vector3 property donors and no Vector4 one.
    [MaterialProperty("_SwayTiming")]
    public struct PrismSwayTimingOverride : IComponentData
    {
        public float3 Value;
    }

    // ----------------------------------------------------------------------
    // The Rhino sword's SLICE death (Docs/PRISM_ANIMATION.md §4.10, PrismSlice.hlsl).
    // Read ONLY by PrismSlice.shader, and only on the pure render entities
    // PrismSlice.cs spawns — two per sliced prism, one per half. Every one of them is
    // an INITIAL CONDITION stamped once at spawn and never written again; the GPU runs
    // the cut, the parting and the dissolve off _PrismClock. float4 throughout: the
    // slice shader is hand-written, so unlike the prism graphs it has no Vector3-donor
    // constraint forcing three-wide packing. Sizes match the shader's DOTS declarations.
    // ----------------------------------------------------------------------

    /// (start time on PrismClock, life in seconds, noise seed, unused).
    [MaterialProperty("_SliceTiming")]
    public struct PrismSliceTimingOverride : IComponentData
    {
        public float4 Value;
    }

    /// The cut in THIS half's object space, (m, d): the half keeps dot(m, x) &lt;= d, m
    /// points out of it. NOT normalised — m = Mᵀ·n_world — so dot(m, x) − d is the
    /// WORLD signed distance from the cut under any non-uniform scale.
    [MaterialProperty("_SlicePlane")]
    public struct PrismSlicePlaneOverride : IComponentData
    {
        public float4 Value;
    }

    /// (the central projection's centre in object space — strictly inside the half —,
    /// the half's deepest point measured from the cut in world units).
    [MaterialProperty("_SliceCentre")]
    public struct PrismSliceCentreOverride : IComponentData
    {
        public float4 Value;
    }

    /// (the hinge the half opens about, WORLD space, the distance the half parts by).
    [MaterialProperty("_SlicePivot")]
    public struct PrismSlicePivotOverride : IComponentData
    {
        public float4 Value;
    }

    /// (the hinge axis, WORLD space, unit; the opening angle in radians).
    [MaterialProperty("_SliceAxis")]
    public struct PrismSliceAxisOverride : IComponentData
    {
        public float4 Value;
    }

    /// (the drift velocity both halves are carried along the swing with, WORLD space; unused).
    [MaterialProperty("_SliceDrift")]
    public struct PrismSliceDriftOverride : IComponentData
    {
        public float4 Value;
    }
}
