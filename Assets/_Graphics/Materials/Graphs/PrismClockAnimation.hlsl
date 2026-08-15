// PrismClockAnimation.hlsl — the GPU side of the clock-material law
// (Docs/PRISM_ANIMATION.md §4.1, LOCKED).
//
// Every function here evaluates a prism visual as a PURE FUNCTION of the shader
// clock and stamped initial conditions. The CPU stamps once (PrismRenderService
// Stamp* APIs / one MaterialPropertyBlock write on the legacy path) and never
// touches the animation again; the end-state swap is scheduled separately.
//
// Shader Graph usage: Custom Function node, Source = this file, with the
// function name WITHOUT the _float suffix (Shader Graph appends it by
// precision). Wire the _PrismClock property node (unexposed global, published
// per frame by PrismClock's publisher from the SAME value the stamps use) into
// every Clock input. NEVER the Time node: URP feeds _Time from a different
// clock domain than the stamps, which renders every animation pre-finished.
//
// Settled-state defaults: every function treats a non-positive Rate/Duration as
// "unstamped" and returns the end state (or the legacy CPU-fed parameter during
// the migration window), so existing materials render unchanged until a stamp
// arrives. This is what lets the clock properties ship on every prism material
// with zero behavior change.

#ifndef PRISM_CLOCK_ANIMATION_INCLUDED
#define PRISM_CLOCK_ANIMATION_INCLUDED

// -----------------------------------------------------------------------------
// Grow-in bloom (BlockGraph vertex stage).
// Reproduces PrismScaleManager's exponential approach exactly:
//   s(t) = 1 - (1 - startFrac) * exp(-rate * (t - t0))
// where rate is the per-second k the manager derived from GrowthRate
// (PrismScaleManager.cs — k = clamp(GrowthRate*0.04, 0.05, 0.1)/0.04).
// The entity's LocalToWorld holds FINAL scale from the stamp; this factor
// scales object-space vertex positions componentwise, so the visual blooms
// while collider, volume, and spatial state are already final
// (gameplay-final-at-start). StartFrac is PER AXIS (anisotropic retargets) and
// may exceed 1 — that's a shrink toward the new target; the exponential
// converges to 1 from either side.
// Rate <= 0 -> settled (factor 1): unstamped materials render the end state.
// -----------------------------------------------------------------------------
void PrismGrowScale_float(float Clock, float StartTime, float Rate, float3 StartFrac,
    out float3 Scale)
{
    if (Rate <= 0.0)
    {
        Scale = float3(1.0, 1.0, 1.0);
        return;
    }
    float t = max(Clock - StartTime, 0.0);
    Scale = max(float3(1.0, 1.0, 1.0) - (float3(1.0, 1.0, 1.0) - StartFrac) * exp(-Rate * t),
                float3(0.0, 0.0, 0.0));
}

// -----------------------------------------------------------------------------
// Color/state transition (BlockGraph fragment stage).
// Lerps from the stamped start colors to the TARGET colors, which are the bound
// material's authored values (or the per-instance _BrightColor/_DarkColor/
// _Spread overrides — snapped to the new material's authored values at stamp
// time by SetMaterial(refreshColors:true)). No _Target* properties exist on
// purpose: the settle swap to the end-state material IS the target.
// Matches MaterialStateManager's smoothstep easing. Duration <= 0 -> target.
// -----------------------------------------------------------------------------
void PrismColorLerp_float(float Clock, float StartTime, float Duration,
    float4 StartBright, float4 StartDark, float3 StartSpread,
    float4 TargetBright, float4 TargetDark, float3 TargetSpread,
    out float4 Bright, out float4 Dark, out float3 Spread)
{
    if (Duration <= 0.0)
    {
        Bright = TargetBright;
        Dark = TargetDark;
        Spread = TargetSpread;
        return;
    }
    float p = saturate((Clock - StartTime) / Duration);
    float t = smoothstep(0.0, 1.0, p);
    Bright = lerp(StartBright, TargetBright, t);
    Dark = lerp(StartDark, TargetDark, t);
    Spread = lerp(StartSpread, TargetSpread, t);
}

// -----------------------------------------------------------------------------
// Explosion debris flight (ExplodingBlockGraph).
// Replaces PrismEffectsManager.ProcessExplosions' per-frame feed:
//   offset  = velocity * t            (was: transform.position stepped on CPU)
//   amount  = speed * t               (was: _ExplosionAmount fed per frame)
//   opacity = 1 - t / duration        (was: _Opacity fed per frame)
// Duration <= 0 -> legacy fallback: passes through the CPU-fed _ExplosionAmount
// and _Opacity so the un-migrated path AND the TransparentPrismMaterial quirk
// (live transparent prisms rest on this graph at _ExplosionAmount = 0) keep
// rendering identically. Velocity is the WORLD-space flight velocity — the ONE
// stamped vector, shared with the shatter-spin axis chain — and the world->
// object conversion happens HERE, on the GPU, as a raw (float3x3) inverse-model
// multiply. NOT Shader Graph's Direction-mode Transform node: that emits
// TransformWorldToObjectDir, which NORMALIZES — the magnitude is destroyed and
// the direction re-skews under the prism's non-uniform scale (the wrong-vector
// bug). The raw multiply is the exact linear map; no CPU-side matrix math.
// The entity transform never moves after the stamp; RenderBounds are expanded
// at stamp to cover the whole flight envelope.
// -----------------------------------------------------------------------------
void PrismExplosionClock_float(float Clock, float StartTime, float Speed, float Duration,
    float3 Velocity, float LegacyAmount, float LegacyOpacity,
    out float Amount, out float Opacity, out float3 ObjectOffset)
{
    if (Duration <= 0.0)
    {
        Amount = LegacyAmount;
        Opacity = LegacyOpacity;
        ObjectOffset = float3(0.0, 0.0, 0.0);
        return;
    }
    float t = max(Clock - StartTime, 0.0);
    Amount = Speed * t;
    Opacity = saturate(1.0 - t / Duration);
#if defined(SHADERGRAPH_PREVIEW)
    ObjectOffset = Velocity * t;
#else
    // Full inverse-model linear transform, unnormalized (see header comment).
    ObjectOffset = mul((float3x3)GetWorldToObjectMatrix(), Velocity * t);
#endif
}

// -----------------------------------------------------------------------------
// Suction / implosion / reverse-grow progress (SuctionGraph).
// Replaces PrismEffectsManager.ProcessImplosions' per-frame _State feed:
//   Direction >= 0: implode, progress 0 -> 1
//   Direction <  0: grow (reverse suction), progress 1 -> 0
// GrowDelay holds the start (the 0.25s StartGrow delay, now baked into the
// stamp). Duration <= 0 -> legacy fallback (CPU-fed _State). _Location remains
// a separate property: stamped once (snapshot), or — ONLY under the documented
// moving-target exception (Docs/PRISM_ANIMATION.md §1) — refreshed as live
// gameplay data.
// -----------------------------------------------------------------------------
void PrismSuctionClock_float(float Clock, float StartTime, float Duration, float Direction,
    float GrowDelay, float LegacyState,
    out float State)
{
    if (Duration <= 0.0)
    {
        State = LegacyState;
        return;
    }
    float t = max(Clock - StartTime - GrowDelay, 0.0);
    float p = saturate(t / Duration);
    State = Direction < 0.0 ? 1.0 - p : p;
}

// -----------------------------------------------------------------------------
// Ballistic flight (BlockGraph + ExplodingBlockGraph vertex stage).
// Docs/PRISM_ANIMATION.md §5 C5 — the Sparrow Turret Stance's fired prisms.
//
// The prism's ENTITY TRANSFORM sits at the flight's END POINT from the stamp
// (gameplay-final-at-start: collider, volume and spatial registration all belong
// where the mass will rest). This function walks the VISUAL in from the muzzle:
// the offset starts at minus the whole flight vector and reaches zero exactly at
// t = Duration, so the prism is drawn leaving the barrel and arriving at its
// anchor with no CPU writes in between.
//
// The easing is the BULLETS' easing. Projectile.MoveProjectileAsync steps by
// cos(t*pi/2T), so distance travelled is its integral, v*(2T/pi)*sin(t*pi/2T) —
// which means a turret prism and a bullet released together stay abreast for the
// whole flight. Velocity is the WORLD-space muzzle velocity (units/second), so
// Velocity*(2T/pi) IS the full flight vector.
//
// World->object conversion happens HERE, as a raw unnormalized inverse-model
// multiply, for the same reason PrismExplosionClock does it: Shader Graph's
// Direction-mode Transform node emits TransformWorldToObjectDir, which
// NORMALIZES — magnitude destroyed, direction re-skewed by the prism's
// non-uniform scale. Never put a Transform node in this chain.
//
// Duration <= 0 -> unstamped: zero offset, so every existing material renders
// exactly where its transform says it is.
// -----------------------------------------------------------------------------
void PrismFlightClock_float(float Clock, float StartTime, float Duration, float3 Velocity,
    out float3 ObjectOffset)
{
    if (Duration <= 0.0)
    {
        ObjectOffset = float3(0.0, 0.0, 0.0);
        return;
    }
    float t = clamp(Clock - StartTime, 0.0, Duration);
    // Fraction of the flight covered by t, under the bullets' cosine easing:
    // 0 at the muzzle, 1 at the anchor. (1.5707963 = pi/2.)
    float covered = sin(t * 1.5707963 / Duration);
    // 0.63661977 = 2/pi. Velocity * 2T/pi is the full flight vector.
    float3 worldOffset = Velocity * (0.63661977 * Duration) * (covered - 1.0);
#if defined(SHADERGRAPH_PREVIEW)
    ObjectOffset = worldOffset;
#else
    // Full inverse-model linear transform, unnormalized (see header comment).
    ObjectOffset = mul((float3x3)GetWorldToObjectMatrix(), worldOffset);
    // A prism pulled fresh from the pool sits at localScale ZERO until its creation
    // coroutine completes (the bloom's start fraction is derived from that zero, so it
    // must not be pre-written). Its model matrix is degenerate for those frames and the
    // inverse blows up. The entity is DisableRendering'd until SetRenderVisible(true),
    // so this should never reach a raster — the guard is there so that "should" is not
    // load-bearing. Written as a NEGATED finite test: every comparison against NaN is
    // false, so NaN falls into the reset branch.
    if (!(dot(ObjectOffset, ObjectOffset) < 1e12))
        ObjectOffset = float3(0.0, 0.0, 0.0);
#endif
}

// -----------------------------------------------------------------------------
// Camera distance for a prism IN FLIGHT (BlockGraph fragment stage).
// Docs/PRISM_ANIMATION.md §5 C5, follow-up: the distance-driven look (the spread
// chain in Prism Sub Graph) used SqrDistanceSubGraph = dot(pivot - camera, ·),
// and a flying prism's PIVOT is parked at the flight's END POINT — so a turret
// shot rendered with its full-range spread from its first frame, visibly wrong
// the whole way out of the barrel.
//
// This replaces that feed: the same squared distance, measured from the pivot
// DISPLACED by the flight offset — i.e. from where the prism is visibly drawn.
// The offset formula is PrismFlightClock's, verbatim (keep the two in lockstep:
// same easing, same constants), and Duration <= 0 reduces exactly to the old
// subgraph's dot(pivot - camera, ·), so every prism not in flight renders
// byte-identically.
// -----------------------------------------------------------------------------
void PrismFlightSqrDistance_float(float Clock, float StartTime, float Duration, float3 Velocity,
    float3 ObjectPosition, float3 CameraPosition, out float SqrDistance)
{
    float3 pos = ObjectPosition;
    if (Duration > 0.0)
    {
        float t = clamp(Clock - StartTime, 0.0, Duration);
        float covered = sin(t * 1.5707963 / Duration);
        pos += Velocity * (0.63661977 * Duration) * (covered - 1.0);
    }
    float3 d = pos - CameraPosition;
    SqrDistance = dot(d, d);
}

// -----------------------------------------------------------------------------
// Shield morph — the per-face bloom (engage) and the shatter overlay (disengage),
// for BOTH shield tiers (BlockGraph + ExplodingBlockGraph vertex stage).
// Docs/PRISM_ANIMATION.md §5 B4. This replaces the last sanctioned CPU ticker
// (PrismOctahedronShieldManager), which rebuilt a per-prism morph MESH every frame
// for the whole 0.35-0.7 s animation.
//
// The one thing a vertex shader cannot derive is which face a vertex belongs to,
// so the mesh generators bake each vertex's own FACE CENTROID into TEXCOORD1
// (OctahedronMeshGenerator/StellatedOctahedronMeshGenerator.FaceCentroidUVChannel).
// With that, both animations are the same two-term expression the CPU used:
//
//   engage  (Direction >= 0):  p = centroid + t*(v - centroid)
//   shatter (Direction <  0):  p = centroid + (1-t)*(v - centroid) + t*Offset*n
//
// t is smoothstep(0,1,progress), which IS AnimationCurve.EaseInOut(0,0,1,1) — a Hermite
// with zero end tangents is 3p^2-2p^3 (Unity's own serialization of that constructor
// carries inSlope/outSlope 0; cross-checked on SpaceCrystalAnimator.shrinkCurve). Every
// shield whose component is added at RUNTIME therefore animates identically to before.
// The two prefabs that serialize the curve (BlueBlock, OctahedronShieldTest) carry a
// hand-altered variant with end tangents 2 — 2p^3-3p^2+2p, fast-slow-fast, up to 0.19
// away from smoothstep — and now ease like the rest of the fleet. The curve fields are
// retired with the CPU driver: an arbitrary AnimationCurve has no GPU evaluation, and
// smoothstep is the easing every other clock transition already uses (PrismColorLerp).
//
// Because the morph runs on the SETTLED shared mesh, a shielded prism never leaves
// the instanced path: same-size shields batch into one draw through the entire
// animation. Normal is the OBJECT-space flat face normal (the generators author one
// normal per face), and it is deliberately NOT re-derived after displacement — a
// per-face rigid translation cannot change a face's normal.
//
// Duration <= 0 -> unstamped: the position passes through untouched, which is every
// prism in the game that is not mid-shield-morph (and every mesh with no TEXCOORD1).
// -----------------------------------------------------------------------------
void PrismShieldMorph_float(float Clock, float StartTime, float Duration, float Direction,
    float ShatterOffset, float3 Position, float3 Normal, float3 FaceCentroid,
    out float3 MorphedPosition)
{
    if (Duration <= 0.0)
    {
        MorphedPosition = Position;
        return;
    }
    float p = saturate((Clock - StartTime) / Duration);
    float t = smoothstep(0.0, 1.0, p);

    // Branchless select: both tiers and both directions share one expression, so a
    // shattering prism and a blooming prism in the same batch never diverge.
    float shatter   = Direction < 0.0 ? 1.0 : 0.0;
    float faceScale = lerp(t, 1.0 - t, shatter);
    float offset    = shatter * t * ShatterOffset;

    MorphedPosition = FaceCentroid + faceScale * (Position - FaceCentroid) + offset * Normal;
}

#endif // PRISM_CLOCK_ANIMATION_INCLUDED
