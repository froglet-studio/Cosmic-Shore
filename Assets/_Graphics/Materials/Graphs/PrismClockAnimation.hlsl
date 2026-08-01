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
// rendering identically. The flight offset is computed in WORLD space
// (velocity * t) and converted to OBJECT space HERE (unnormalized
// TransformWorldToObjectDir), so the graph just ADDS ObjectOffset to the
// object-space vertex position — no Transform node needed. The entity
// transform never moves after the stamp.
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
    ObjectOffset = TransformWorldToObjectDir(Velocity * t, false);
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

#endif // PRISM_CLOCK_ANIMATION_INCLUDED
