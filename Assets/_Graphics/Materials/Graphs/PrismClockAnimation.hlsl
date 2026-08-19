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
// Super-shield deflection jiggle (BlockGraph + ExplodingBlockGraph vertex stage).
// Docs/PRISM_ANIMATION.md §5 C14 — a super-shielded prism that is HIT but not
// destroyed (Prism.AbsorbSuperShieldHit) wobbles and settles.
//
// Super-shielded mass is fully invulnerable, so every hit on it was silent: the
// impactor's sparks fired, the prism did not move, and the deflection read as the
// shot missing. This is the deflection made visible, and it is animation in the
// §1 sense — a pure function of the clock and the stamped hit conditions, so it
// is a per-instance STAMP, not a global uniform (contrast PrismOcclusionFade /
// PrismDestructionSight, which are view-dependent and therefore per-frame globals).
//
// The motion is a struck body's FREE PRECESSION, applied per FACE:
//   * every face rotates about the prism's object ORIGIN, so the stella's outer
//     spike tips wag far while the core barely moves — the "jiggly" read;
//   * the rotation AXIS lies on a cone about that face's own normal. It PRECESSES
//     around the normal at Params.y and NUTATES — the cone half-angle breathes
//     0..PI/2 at Params.z — so the face alternates between an in-plane twist
//     (axis ON the normal) and a maximum tip (axis in the face plane) while the
//     tip direction sweeps. Rates are deliberately non-commensurate so the
//     pattern never repeats inside one deflection;
//   * the ANGLE is amplitude * envelope(t), and the envelope reaches EXACTLY zero
//     at t = Duration — so the scheduled ClearJiggleStamp is invisible and a stamp
//     that is never cleared is a permanent no-op rather than a stuck prism.
//
// Randomness needs NO mesh channel and NO extra stamped property. Prism meshes are
// hard-edged (the box is 24 verts / 6 distinct normals; the super-shield stella is
// 72 verts / 24 — StellatedOctahedronMeshGenerator splits per face for exactly this
// reason), so the object-space NORMAL *is* the face id; the object-to-world
// translation is a free per-prism seed, so neighbouring prisms in one blast do not
// wobble in lockstep; and StartTime re-rolls every hit. This matters because the
// stella carries no tangents and no UVs — the tangent basis is therefore built
// branchlessly FROM THE NORMAL (Duff et al. 2017), never read from the vertex
// stream, where it would be zero.
//
// Scale correction: prisms are non-uniformly scaled (a trail slab is long and
// thin), and an object-space rotation seen through that scale is a shear that wags
// the long axis far more than the others. The rotation is therefore done in the
// locally-ISOTROPIC frame (position * objectScale), matching what the shatter's
// RotateFacesAlongAxis subgraph does on ExplodingBlockGraph. The normal is carried
// through the same frame inverted, because a normal transforms by the inverse
// transpose — get this backwards and the fresnel rim slides the wrong way.
//
// Duration <= 0 -> unstamped: identity on both outputs, so every prism that has
// never absorbed a super-shield hit renders byte-identically.
// -----------------------------------------------------------------------------

// Envelope decay across the deflection. The (1 - u) factor is what guarantees the
// exact zero at u = 1; this only shapes how front-loaded the wobble is.
#define PRISM_JIGGLE_DECAY 2.5
// Maximum nutation half-angle. PI/2 lets the axis swing from the face normal all
// the way into the face plane, which is the difference between "it twists" and
// "it wobbles".
#define PRISM_JIGGLE_CONE 1.5707963
#define PRISM_JIGGLE_TAU 6.2831853

// Hash 3 -> 1, [0,1). Dave Hoskins' hash13; used only for phase offsets, so its
// distribution matters and its cryptographic quality does not.
float PrismJiggleHash13(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

// Branchless orthonormal basis from a unit vector (Duff et al. 2017, "Building an
// Orthonormal Basis, Revisited"). Stable across the whole sphere, including n.z
// near -1 where the naive cross-product construction degenerates.
void PrismJiggleBasis(float3 n, out float3 t, out float3 b)
{
    float s = n.z >= 0.0 ? 1.0 : -1.0;
    float a = -1.0 / (s + n.z);
    float c = n.x * n.y * a;
    t = float3(1.0 + s * n.x * n.x * a, s * c, -s * n.x);
    b = float3(c, s + n.y * n.y * a, -n.y);
}

// Rodrigues' rotation of v about a UNIT axis.
float3 PrismJiggleRotate(float3 v, float3 axis, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return v * c + cross(axis, v) * s + axis * (dot(axis, v) * (1.0 - c));
}

void PrismJiggleClock_float(float Clock, float StartTime, float Duration, float3 Params,
    float3 Position, float3 Normal,
    out float3 OutPosition, out float3 OutNormal)
{
    OutPosition = Position;
    OutNormal = Normal;

    if (Duration <= 0.0)
        return;                                   // unstamped -> identity

    float t = Clock - StartTime;
    if (t <= 0.0 || t >= Duration)
        return;                                   // before the hit / after the settle

    // A mesh with no normals (or a degenerate vertex) has nothing to rotate about.
    // Negated finite test: every comparison against NaN is false, so NaN bails.
    float nLenSq = dot(Normal, Normal);
    if (!(nLenSq > 1e-8))
        return;
    float3 n = Normal * rsqrt(nLenSq);

    // Locally-isotropic frame (see header). Preview has no model matrix.
#if defined(SHADERGRAPH_PREVIEW)
    float3 scale = float3(1.0, 1.0, 1.0);
    float3 origin = float3(0.0, 0.0, 0.0);
#else
    float3x3 m = (float3x3)GetObjectToWorldMatrix();
    float3 scale = float3(length(float3(m._m00, m._m10, m._m20)),
                          length(float3(m._m01, m._m11, m._m21)),
                          length(float3(m._m02, m._m12, m._m22)));
    float3 origin = float3(GetObjectToWorldMatrix()._m03,
                           GetObjectToWorldMatrix()._m13,
                           GetObjectToWorldMatrix()._m23);
#endif
    // A prism pulled fresh from the pool sits at localScale ZERO until its creation
    // coroutine completes, so the frame is degenerate and 1/scale blows up. The birth
    // rule already refuses to stamp there (PrismSuperShieldJiggle); this is so that
    // "already" is not load-bearing.
    if (!(all(scale > 1e-5)))
        return;

    float amplitude = Params.x;                   // peak tilt, radians
    float precessRate = Params.y;                 // rad/s, axis sweep about the normal
    float nutateRate = Params.z;                  // rad/s, cone half-angle breathing

    float u = t / Duration;
    float env = (1.0 - u) * exp(-PRISM_JIGGLE_DECAY * u);

    float seedA = PrismJiggleHash13(n * 17.0 + origin * 0.013 + StartTime);
    float seedB = PrismJiggleHash13(n * 29.0 - origin * 0.017 + StartTime * 1.7 + 11.0);

    float3 tangent, bitangent;
    PrismJiggleBasis(n, tangent, bitangent);

    float phi = precessRate * t + seedA * PRISM_JIGGLE_TAU;                        // precession
    float theta = PRISM_JIGGLE_CONE *
                  (0.5 - 0.5 * cos(nutateRate * t + seedB * PRISM_JIGGLE_TAU));    // nutation

    float sp, cp, st, ct;
    sincos(phi, sp, cp);
    sincos(theta, st, ct);
    float3 axis = n * ct + (tangent * cp + bitangent * sp) * st;

    float angle = amplitude * env;

    // Position rotates in the isotropic frame; the normal rotates in the frame a
    // normal actually lives in (inverse transpose => divide by scale going in,
    // multiply coming out — the mirror of the position's).
    OutPosition = PrismJiggleRotate(Position * scale, axis, angle) / scale;
    OutNormal = PrismJiggleRotate(Normal / scale, axis, angle) * scale;
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
// -- THE SHATTER IS THE PRISM EXPLOSION, APPLIED PER FACE (§4.8.1) ------------
// A shield does not fall apart on its own; something BREAKS it. So the shatter takes
// the same initial condition the prism explosion takes — a WORLD-space Velocity, the
// force that dropped the shield — and reproduces the explosion's motion model on the
// shield's faces:
//
//   drift   p += velocityObj * tSec              (PrismExplosionClock's ObjectOffset)
//   tumble  p  = centroid + R(p - centroid)      (RotateFacesAlongAxis, per face)
//           R  = Rodrigues about normalize(cross(velocityObj, n)),
//                angle = PRISM_SHIELD_SHATTER_SPIN * |Velocity| * tSec
//
// which is the explosion's `_ExplosionAmount * _ExplosiveRotation` with the same
// speed*seconds gain. Note the two clocks: the SHAPE terms (contraction, fly-out) run
// on the normalized eased t, while drift and tumble run on tSec — real seconds — so a
// hard hit throws the shards further and spins them harder, exactly as the explosion's
// debris does. It is expressed here rather than by reusing ExplodingBlockGraph's
// RotateFacesAlongAxis subgraph for two reasons: that subgraph is not on BlockGraph
// (where a shielded prism's own material lives), and it rotates about the object ORIGIN
// off a TANGENT vector the shield meshes do not carry (they author positions, normals
// and TEXCOORD1 only) — a zero tangent turns its second rotation into a cos(angle)
// scale pulse. PrismJiggleClock re-expressed the same subgraph in HLSL for the same
// reason (§4.9); this shares its helpers.
//
// Velocity ZERO is the identity for both new terms — no axis, no drift — so every
// direction-less disengage (a timer expiring, an arena teardown, a domain change, a
// herbivore stripping armour) renders exactly the symmetric puff it always did.
//
// Rotation runs in the locally-ISOTROPIC frame (position * objectScale), the same
// correction PrismJiggleClock documents: prisms are non-uniformly scaled, and an
// object-space rotation seen through that scale is a shear that wags the long axis far
// more than the others. The normal is carried through the same frame INVERTED, because
// a normal transforms by the inverse transpose.
//
// KNOWN LIMITATION, deliberate: the tumble rotates POSITIONS ONLY — a shard's normal
// does not follow it, so a tumbling face lights as though it never moved. Carrying the
// normal through this node was built and REVERTED: on ExplodingBlockGraph the only
// acyclic source for an incoming normal is RotateFacesAlongAxis' output, and that
// subgraph is fed BY this node's position output, so routing its normal back in makes
// the two nodes a cycle and ShaderGraph fails the whole graph (pink materials on every
// explosion). Fixing it properly needs a SECOND custom function that rotates only the
// normal, downstream of both — not a wider signature on this one. The shard shrinks to
// nothing in 0.6-0.7 s, so the stale shading is a small price next to that risk.
//
// Because the morph runs on the SETTLED shared mesh, a shielded prism never leaves the
// instanced path: same-size shields batch into one draw through the entire animation.
// Normal is the OBJECT-space flat face normal (the generators author one normal per
// face) and is deliberately not re-derived: a per-face rigid translation cannot change a
// face's normal, and the tumble's effect on it is the known limitation above.
//
// Duration <= 0 -> unstamped: position and normal pass through untouched, which is every
// prism in the game that is not mid-shield-morph (and every mesh with no TEXCOORD1).
// -----------------------------------------------------------------------------

// Radians of face tumble per world unit travelled by the breaking impulse — the shield's
// counterpart of the explosion material's _ExplosiveRotation, and the ONE shape constant
// of the shatter (its per-shield dials are authored on the shield components: duration,
// fly-out offset, and the drift speed cap that bounds BOTH terms below). At the shipped
// 20 u/s cap this is ~3 rad of tumble across a 0.6 s shatter, most of it spent while the
// face is still large enough to read.
#define PRISM_SHIELD_SHATTER_SPIN 0.25

void PrismShieldMorph_float(float Clock, float StartTime, float Duration, float Direction,
    float ShatterOffset, float3 Velocity, float3 Position, float3 Normal, float3 FaceCentroid,
    out float3 MorphedPosition)
{
    MorphedPosition = Position;

    if (Duration <= 0.0)
        return;                                   // unstamped -> the position passes through

    float p = saturate((Clock - StartTime) / Duration);
    float t = smoothstep(0.0, 1.0, p);

    // Branchless select: both tiers and both directions share one expression, so a
    // shattering prism and a blooming prism in the same batch never diverge.
    float shatter   = Direction < 0.0 ? 1.0 : 0.0;
    float faceScale = lerp(t, 1.0 - t, shatter);
    float offset    = shatter * t * ShatterOffset;

    MorphedPosition = FaceCentroid + faceScale * (Position - FaceCentroid) + offset * Normal;

    // ---- the explosion terms: only a SHATTER carries an impulse -------------
    if (shatter == 0.0)
        return;

    // Elapsed SECONDS, not the normalized eased progress: drift and tumble are physical
    // quantities (units/second, radians/unit) and must not be reshaped by the easing that
    // governs the face's contraction. Held at Duration so a stamp that outlives its
    // scheduled retirement freezes rather than flying away forever.
    float tSec = clamp(Clock - StartTime, 0.0, Duration);

    // Negated finite test: every comparison against NaN is false, so NaN bails.
    float vLenSq = dot(Velocity, Velocity);
    if (!(vLenSq > 1e-8))
        return;                                   // direction-less disengage -> pure puff

#if defined(SHADERGRAPH_PREVIEW)
    float3 velocityObj = Velocity;
    float3 scale = float3(1.0, 1.0, 1.0);
#else
    // Full inverse-model linear transform, unnormalized — never Shader Graph's
    // Direction-mode Transform node, which emits TransformWorldToObjectDir and
    // NORMALIZES (magnitude destroyed, direction re-skewed by the prism's non-uniform
    // scale). Same rule as PrismExplosionClock / PrismFlightClock.
    float3 velocityObj = mul((float3x3)GetWorldToObjectMatrix(), Velocity);
    float3x3 m = (float3x3)GetObjectToWorldMatrix();
    float3 scale = float3(length(float3(m._m00, m._m10, m._m20)),
                          length(float3(m._m01, m._m11, m._m21)),
                          length(float3(m._m02, m._m12, m._m22)));
#endif
    // A prism pulled fresh from the pool sits at localScale ZERO until its creation
    // coroutine completes, so the frame is degenerate and 1/scale blows up. Birth
    // transitions already disengage instantly (PrismStateManager.IsBirthTransition);
    // this is so that "already" is not load-bearing.
    if (!(all(scale > 1e-5)) || !(dot(velocityObj, velocityObj) < 1e12))
        return;

    // Drift: the whole shard cloud rides the breaking impulse. Object-space so the
    // WORLD displacement is exactly Velocity * tSec, matching the explosion's debris.
    MorphedPosition += velocityObj * tSec;

    // Tumble: each face rotates about ITS OWN centroid, on the axis perpendicular to
    // both the impulse and the face — so a face struck edge-on cartwheels while one
    // struck dead-on (cross ~ 0) is simply pushed, which is the correct read for both.
    float nLenSq = dot(Normal, Normal);
    if (!(nLenSq > 1e-8))
        return;                                   // no normals -> nothing to rotate about
    float3 n = Normal * rsqrt(nLenSq);

    float3 axis = cross(velocityObj, n);
    float axisLenSq = dot(axis, axis);
    if (!(axisLenSq > 1e-8))
        return;                                   // impulse along the face normal
    axis *= rsqrt(axisLenSq);

    // |Velocity| is the WORLD speed — the same channel the explosion's shatter rate
    // rides — so the tumble reads identically whatever the prism's local scale is.
    float angle = PRISM_SHIELD_SHATTER_SPIN * sqrt(vLenSq) * tSec;

    // Rotation runs in the isotropic frame about the face centroid (see the header).
    float3 rel = (MorphedPosition - FaceCentroid) * scale;
    MorphedPosition = FaceCentroid + PrismJiggleRotate(rel, axis, angle) / scale;
}

#endif // PRISM_CLOCK_ANIMATION_INCLUDED
