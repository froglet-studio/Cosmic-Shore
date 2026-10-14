// PrismGravityWarp.hlsl — SPAGHETTIFICATION: the GPU side of the black hole's tidal stretch
// (Docs/BLACK_HOLE.md §5, Docs/PRISM_ANIMATION.md §4.7.4: a citizen of §4.7's "global uniform"
// shape for a prism visual that depends on live gameplay data).
//
// PURPOSE. A body near a black hole is pulled harder on its near side than on its far side, so it
// is STRETCHED along the line to the hole and SQUEEZED across it — spaghettification. This file
// draws exactly that, from the physics, on every prism within reach of a horizon. It moves no mass:
// the gravity field (BlackHoleGravityField, BlackHolePhysics) is what pulls prisms toward the hole
// and swallows them; this is what tides do to their SHAPE on the way. Photons only — the collider,
// the spatial index and every gameplay query see the prism exactly where and as big as it is.
//
// THE PHYSICS. The tidal tensor a freely falling body feels near a Schwarzschild hole, in its own
// frame, is EXACTLY the Newtonian one (a textbook GR result — the Riemann components in a radially
// infalling orthonormal frame):
//
//     T = (GM / r³) · diag(+2, −1, −1)          radial, transverse, transverse
//
// — finite at the horizon, falling as 1/r³, and TRACE-FREE (tides deform; they do not compress).
// A body that yields to that tide for a response time τ (how soft it is) is drawn with the
// log-stretch half the tidal acceleration × τ² gives a free dust cloud:
//
//     ε = GM·τ² / r³,        radial ×e^ε,        transverse ×e^(−ε/2)
//
// so its volume is exactly conserved (e^ε · e^(−ε/2) · e^(−ε/2) = 1), the stretch is strongest at
// the horizon (ε_h = GM·τ²/r_s³) and, because r_s grows with the mass, a SMALL hole shreds harder at
// its horizon than a big one (ε_h ∝ 1/M², as in reality — a supermassive hole swallows you whole).
// The exponential is what makes it safe at any strength: every stretch is positive, so the map can
// never fold or turn a prism inside out, and to first order it IS the linear tidal strain.
//
// THE MAP — per PRISM, not per vertex. The tide is evaluated at the prism's CENTRE c (its object
// origin) and applied to every vertex as one affine stretch about c along n̂, the direction from
// the hole to c:
//
//     q = p − c,   q_r = q·n̂,   p' = c + n̂·q_r·e^ε + (q − n̂·q_r)·e^(−ε/2)
//
// One map per prism means flat faces stay flat and the 24-triangle prism is EXACT — there is
// nothing to subdivide, so (unlike the first version) no high-poly mesh is swapped in and no
// residency query runs. The normal is the map's inverse transpose, also exact:
//
//     n' = normalize( n̂·(n·n̂)·e^(−ε) + (n − n̂·(n·n̂))·e^(ε/2) )
//
// The first version slid every vertex toward the singularity by a fraction of its distance — a
// second, invented pull on top of the real one, with a falloff flat at the horizon that put the
// stretch's MAXIMUM mid-reach and ZERO at the horizon. Retired 2026-10-08.
//
// THE UNIFORMS (published by BlackHoleWarp.cs once per frame, in LateUpdate):
//   float4 _PrismGravityWarpCentre[N] — xyz: the hole's world-space centre this frame.
//                                       w:   its event-horizon radius r_s (world units).
//   float4 _PrismGravityWarpWeight[N] — x: GM·τ², u³ — the tidal coefficient, already scaled by
//                                          the hole's eased weight, so spawn/despawn ease the
//                                          stretch in and out (ε is linear in it).
//                                       y: the REACH beyond the horizon, world units: the stretch
//                                          is faded to exactly zero there, across the OUTER HALF of
//                                          the shell only (a C1 window, so a prism drifting outward
//                                          relaxes instead of snapping). The inner half is drawn at
//                                          exactly the physical tide; where the fade starts the tide
//                                          is already ≤ 1/43 of the horizon's (reach 5 r_s).
//   float4 _PrismGravityWarpParams    — (ln of the maximum stretch, liveSlotCount, 0, 0).
//                                       liveSlotCount is the MASTER SENTINEL: an unpublished global
//                                       reads as zero, and zero means "the loop does not execute".
//
// The arrays are file-scope, OUTSIDE every CBUFFER (per-FRAME globals; an array inside
// UnityPerMaterial breaks SRP batching; Shader Graph has no array property type).
//
// CEILING. ε eases into C = ln(maxStretch) through a 4-norm soft minimum,
//
//     ε' = ε / (1 + (ε/C)⁴)^¼
//
// — the physics to within 0.1% up to a quarter of the ceiling and 1.5% at half of it (tanh, the
// first choice, was 8% low there), monotone, and never past C — so a prism at the horizon of a tiny
// hole is a long needle, not a line to infinity. The transverse squeeze keeps ε'/2, so volume stays
// conserved.
//
// SLOT SELECTION. With more than one hole live, the hole whose tide at the prism's centre is
// largest wins outright. Tides from two holes do add (the tensors sum), but two stretch axes do not
// make a single exact stretch, and the dominant term is what the eye reads.
//
// SPLICE ORDER. Immediately BEFORE the cradle on both live graphs' vertex chains (after grow,
// shield morph, jiggle, flight and suction): it stretches the prism as it is drawn, and the cradle
// stays last. Signature unchanged from the first version, so the wiring did not move.
//
// MESHES. A pure function of world position, world normal and the object origin: correct on the
// authored prism, the shield octahedra and the exploding debris (each fragment stretched about the
// prism it came from, so a burst near a hole is drawn out toward it with no code of its own).
//
// COST CONTRACT. No hole live: one integer compare. One live: two matrix transforms in, one centre
// transform, ≤ 4 slot iterations of a few multiplies, two square roots, two exps, two transforms out. No
// fragment cost, no texture, no batch split, no material swap, no draw call, no CPU per prism.
//
// KNOWN IMPRECISION. Entities Graphics culls by the prism's RenderBounds, which a per-frame global
// cannot grow: a prism stretched ×N whose bounds are just off-screen can lose a needle tip that
// should be on-screen. And the tide is the radial-free-fall frame's: a prism that is ORBITING feels
// the same tensor to the accuracy that matters here.

#ifndef PRISM_GRAVITY_WARP_INCLUDED
#define PRISM_GRAVITY_WARP_INCLUDED

// How many black holes can warp at once. Mirrors BlackHoleWarp.Slots in BlackHoleWarp.cs and
// BlackHolePhysics.NativeWells.Capacity — change all three together.
#ifndef PRISM_GRAVITY_WARP_SLOTS
#define PRISM_GRAVITY_WARP_SLOTS 4
#endif

// 1 = the normal is carried through the map's inverse transpose (shipped). 0 leaves it untouched —
// the harness's negative control: the stretched faces must then light as if unstretched, and the
// "normal is perpendicular to the stretched surface" test must fail.
#ifndef PRISM_GRAVITY_WARP_NORMAL_CORRECTION
#define PRISM_GRAVITY_WARP_NORMAL_CORRECTION 1
#endif

float4 _PrismGravityWarpCentre[PRISM_GRAVITY_WARP_SLOTS];  // xyz world centre, w horizon radius
float4 _PrismGravityWarpWeight[PRISM_GRAVITY_WARP_SLOTS];  // x GM·τ² (eased), y reach beyond the horizon
float4 _PrismGravityWarpParams;                            // (ln max stretch, liveSlotCount, liveWaveCount, 0)

// THE GRAVITATIONAL WAVE (Docs/BLACK_HOLE.md §5.2, the Vessel Studio's): when a pair annihilates, its mass leaves as a
// burst moving out at gwSpeed, and every prism it crosses is stretched along its own x and squeezed along its own y by
// gwPrism × the strain at its centre, then the other way — the studio's writeBodies, scale (1 + gh, 1 − gh). The strain
// is the studio's gwStrainAt: mass·e^(−u/τ)·sin(2πu/period)·min(1, 60/r)·(1 − cos²) at u = age − r/speed, nothing
// before the front or after 8 ringdowns. BlackHoleWaves publishes the newest four (BlackHoleWarp.Flush).
#ifndef PRISM_GRAVITY_WAVE_SLOTS
#define PRISM_GRAVITY_WAVE_SLOTS 4
#endif
float4 _PrismGravityWaveCentre[PRISM_GRAVITY_WAVE_SLOTS];  // xyz where the pair met, w its age (s)
float4 _PrismGravityWaveAxis[PRISM_GRAVITY_WAVE_SLOTS];    // xyz the line the holes met along (unit, or 0), w mass
float4 _PrismGravityWaveParams;                            // (gwSpeed, gwPeriod, gwRingdown, gwPrism)

// The strain one wave puts on a point (the studio's gwStrainAt).
float PrismGravityWaveStrain(float3 at, float4 centre, float4 axis, float4 prm)
{
    float3 d = at - centre.xyz;
    float r = length(d) + 1e-6;
    float u = centre.w - r / max(1.0, prm.x);
    float tau = max(0.01, prm.z);
    if (u <= 0.0 || u > 8.0 * tau) return 0.0;
    float ca = dot(d, axis.xyz) / r;
    return axis.w * exp(-u / tau) * sin(6.2831853 * u / max(0.02, prm.y)) * min(1.0, 60.0 / r) * (1.0 - ca * ca);
}

// The fade to zero at the reach: exactly 1 across the inner half of the shell (the tide is drawn as
// the physics gives it), then 1 − smoothstep across the outer half — C1 at both ends, so the stretch
// of a prism drifting through the reach changes smoothly and is exactly 0 at it.
float PrismGravityWarpWindow(float s, float reach)
{
    if (s >= reach) return 0.0;
    float u = saturate(2.0 * s / reach - 1.0);
    return 1.0 - u * u * (3.0 - 2.0 * u);
}

// The CEILING's soft minimum: ε / (1 + (ε/C)⁴)^¼ — the identity while ε ≪ C, C as ε → ∞, monotone
// between. x is capped so x⁴ stays finite in float (ε' is C to seven digits long before x = 1e4).
float PrismGravityWarpCeiling(float eps, float ceiling)
{
    float x = min(eps / ceiling, 1e4);
    float x2 = x * x;
    return ceiling * x * rsqrt(sqrt(1.0 + x2 * x2));
}

// The tidal log-stretch at distance d from a hole: GM·τ²/d³, faded by the window. d is floored at
// the horizon (a centre inside it is a prism being swallowed this frame, and the tensor is finite
// there anyway).
float PrismGravityWarpTide(float d, float rs, float k, float reach)
{
    float r = max(d, rs);
    return k / (r * r * r) * PrismGravityWarpWindow(d - rs, reach);
}

// A SMOOTH well's tide (Docs/CRYSTAL_WORMHOLE.md): the Plummer core ε softens 1/r³ to 1/(r² + ε²)^1.5 —
// finite and smooth through the centre, no horizon floor. ε ≤ 0 is the black hole's tide above.
float PrismGravityWarpTideSoft(float d, float rs, float k, float reach, float softening)
{
    if (!(softening > 0.0)) return PrismGravityWarpTide(d, rs, k, reach);
    float q = d * d + softening * softening;
    return k / (q * sqrt(q)) * PrismGravityWarpWindow(max(d - rs, 0.0), reach);
}

// Position and Normal are OBJECT space. They arrive after grow, shield morph, jiggle, flight and
// suction, and BEFORE the cradle (see SPLICE ORDER). Outputs are object space too.
void PrismGravityWarpDeform_float(float3 Position, float3 Normal,
    out float3 OutPosition, out float3 OutNormal)
{
    OutPosition = Position;
    OutNormal = Normal;

#if defined(SHADERGRAPH_PREVIEW)
    // The preview has no model matrix and no published bank: identity.
    return;
#else
    int count = (int)_PrismGravityWarpParams.y;
    int waves = (int)_PrismGravityWarpParams.z;
    if (count <= 0 && waves <= 0)
        return;                                   // master sentinel: no hole and no wave is live

    float nLenSq = dot(Normal, Normal);
    if (!(nLenSq > 1e-8))
        return;                                   // no surface to light (and NaN lands here)
    float3 nObj = Normal * rsqrt(nLenSq);

    float4x4 M = GetObjectToWorldMatrix();
    float4x4 Minv = GetWorldToObjectMatrix();

    // A prism pulled fresh from the pool sits at scale zero until its creation completes; its
    // matrix is degenerate. It is not rendered then; the guard keeps "is not" from being load-bearing.
    float3 nW = mul(nObj, (float3x3)Minv);        // a normal transforms by the inverse transpose
    float nwLenSq = dot(nW, nW);
    if (!(nwLenSq > 1e-12) || !(nwLenSq < 1e12))
        return;
    nW *= rsqrt(nwLenSq);

    float3 c = mul(M, float4(0.0, 0.0, 0.0, 1.0)).xyz;   // the prism's centre: its object origin

    // The hole whose tide at the prism's centre is largest (SLOT SELECTION), by MAGNITUDE: a white
    // hole (Docs/BLACK_HOLE.md §12) publishes a NEGATIVE k, and its tide is the black hole's
    // negated — compression along the line to it, spreading across: antisymmetric.
    float bestTide = 0.0;
    float3 bestDir = float3(0.0, 0.0, 1.0);
    for (int i = 0; i < PRISM_GRAVITY_WARP_SLOTS; i++)
    {
        if (i >= count) break;

        float4 slot = _PrismGravityWarpCentre[i];
        float4 weight = _PrismGravityWarpWeight[i];
        float k = weight.x;
        float reach = weight.y;
        if (!(abs(k) > 0.0) || !(slot.w > 0.0) || !(reach > 0.0)) continue;

        float3 rad = c - slot.xyz;
        float d = length(rad);
        if (!(d > 1e-4)) continue;                // centre on the singularity: no direction
        if (d - slot.w >= reach) continue;        // beyond the reach: no tide drawn

        float tide = PrismGravityWarpTideSoft(d, slot.w, k, reach, weight.z);
        if (abs(tide) <= abs(bestTide)) continue;
        bestTide = tide;
        bestDir = rad / d;
    }

    // The wave's stretch at the prism's centre: the studio's sum over waves, dropped below 0.002, clamped to ±0.8.
    float h = 0.0;
    for (int j = 0; j < PRISM_GRAVITY_WAVE_SLOTS; j++)
    {
        if (j >= waves) break;
        h += PrismGravityWaveStrain(c, _PrismGravityWaveCentre[j], _PrismGravityWaveAxis[j], _PrismGravityWaveParams);
    }
    if (abs(h) < 0.002) h = 0.0;
    float gh = clamp(_PrismGravityWaveParams.w * h, -0.8, 0.8);

    if (!(abs(bestTide) > 0.0) && gh == 0.0)
        return;                                   // no hole and no wave reaches this prism

    // Ease into the ceiling: the physics while small, never past ln(maxStretch) — in either sign.
    float ceiling = max(_PrismGravityWarpParams.x, 1e-3);
    float eps = abs(bestTide) > 0.0 ? sign(bestTide) * PrismGravityWarpCeiling(abs(bestTide), ceiling) : 0.0;
    float radial = exp(eps);                      // stretch along the line to the hole
    float across = exp(-0.5 * eps);               // squeeze across it — volume conserved

    // The affine stretch about the prism's centre.
    float3 pW = mul(M, float4(Position, 1.0)).xyz;
    float3 q = pW - c;
    float qr = dot(q, bestDir);
    float3 pNew = c + bestDir * (qr * radial) + (q - bestDir * qr) * across;

#if PRISM_GRAVITY_WARP_NORMAL_CORRECTION
    // Its inverse transpose: the radial component shrinks by e^ε, the transverse grows by e^(ε/2).
    float nr = dot(nW, bestDir);
    float3 nNew = bestDir * (nr / radial) + (nW - bestDir * nr) / across;
#else
    float3 nNew = nW;
#endif

    // The wave: (1 + gh) along the prism's own x, (1 − gh) along its own y, about its centre; the normal by the
    // inverse transpose. The object axes in world are orthogonal under a TRS model matrix.
    if (gh != 0.0)
    {
        float3 xA = normalize(mul(M, float4(1.0, 0.0, 0.0, 0.0)).xyz);
        float3 yA = normalize(mul(M, float4(0.0, 1.0, 0.0, 0.0)).xyz);
        float3 q2 = pNew - c;
        pNew = c + q2 + xA * (dot(q2, xA) * gh) - yA * (dot(q2, yA) * gh);
#if PRISM_GRAVITY_WARP_NORMAL_CORRECTION
        nNew = nNew + xA * (dot(nNew, xA) * (1.0 / (1.0 + gh) - 1.0)) + yA * (dot(nNew, yA) * (1.0 / (1.0 - gh) - 1.0));
#endif
    }

    // Back to object space: the point through the inverse model, the normal through the model's
    // transpose (the inverse of the inverse-transpose above), renormalised.
    float3 outPos = mul(Minv, float4(pNew, 1.0)).xyz;
    float3 outNrm = mul(nNew, (float3x3)M);
    float outNrmLenSq = dot(outNrm, outNrm);
    if (!(dot(outPos, outPos) < 1e12) || !(outNrmLenSq > 1e-12))
        return;                                   // degenerate frame: leave the vertex alone

    OutPosition = outPos;
    OutNormal = outNrm * rsqrt(outNrmLenSq);
#endif
}

#endif // PRISM_GRAVITY_WARP_INCLUDED
