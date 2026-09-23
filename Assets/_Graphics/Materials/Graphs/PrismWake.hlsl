// PrismWake.hlsl — the GPU side of the WAKE
// (Docs/PRISM_ANIMATION.md §4.7.3, the FOURTH citizen of §4.7's "global uniform" shape for a
// prism visual that depends on live gameplay data, and the SECOND that moves VERTICES.)
//
// PURPOSE. A vessel travelling fast enough drags a travelling ripple through the mass it is
// passing — most visibly the ribbon it is laying, which runs straight down the middle of the
// disturbance. The mass around the ship's recent path swells away from that path and shrinks
// back toward it in a wave that streams BACKWARD, so the crests sit still in the world and the
// pilot flies out from under them. It reads as a boat's wake: the faster you go, the more of it
// there is, and it is a thing other pilots can see you leaving behind you.
//
// WHY IT IS A GLOBAL AND NOT A STAMP. "Where is that hull, which way is it pointing, and how
// far through the wave is it" is live per-frame data — it changes every frame for every prism
// as the ship flies. It can therefore never be a per-prism stamp (§1: could the GPU have
// computed this frame's value from what was known when the prism was laid? No — the ship had
// not arrived yet), and a per-prism CPU pass that writes each prism's material is exactly what
// the clock-material law exists to prevent. The law's sanctioned shape is a GLOBAL uniform
// (§4.7): O(1) writes per frame that every prism reads. The residency swap that gives those
// prisms a surface to ripple is NOT an exception either — a mesh override is a STATE CHANGE,
// final at the instant it is applied exactly like a shield engaging, not an animation.
//
// WHY A CYLINDER AND NOT A SPHERE. The cradle (§4.7.2) is spherical because it drapes onto a
// hull. A wake is about a PATH, so its natural frame is cylindrical about the line the ship
// just flew down — and that matters for more than tidiness: the trail ribbon is laid ON that
// line, so a field measured from the ship's CENTRE would have had the ribbon sitting at one
// roughly-constant distance and rippling as a whole, where a field measured from the AXIS has
// the ribbon rippling ALONG ITS LENGTH, which is the thing that reads as a wake.
//
// THE UNIFORMS (published by PrismWake.cs once per frame, in LateUpdate, from a frame-stamped
// registry of vessels above the wake speed):
//   float4 _PrismWakeCentre[N] — xyz: the hull's world centre this frame. w: its radius,
//                                carried for tooling and NOT read here — everything the map
//                                derives from the radius arrives already derived in Shape.
//   float4 _PrismWakeAxis[N]   — xyz: the unit wake axis, pointing BEHIND the ship (-course).
//                                w:   the wave's phase in radians, integrated on the CPU so the
//                                     crests hold still in the world while the ship flies on.
//   float4 _PrismWakeShape[N]  — x: eased strength 0..1 (the speed window's ramp).
//                                y: radial reach, world units.
//                                z: train length behind the ship, world units.
//                                w: wavenumber k, radians per world unit along the axis.
//                                All four are DERIVED ON THE CPU from the hull's radius and the
//                                config, so the shader derives nothing the publisher also
//                                derives — there is one copy of each formula, not two.
//   float4 _PrismWakeParams    — (amplitude, radialExponent, liveSlotCount, 0).
//                                liveSlotCount is the MASTER SENTINEL: an unpublished global
//                                reads as zero, and zero must mean "the loop does not execute".
//
// The arrays are declared at FILE SCOPE, not as graph properties (Shader Graph has no array
// property type — which is why wiring this needed no property surgery on either graph), and
// OUTSIDE every CBUFFER: they are per-FRAME globals, and an array inside UnityPerMaterial is
// what breaks SRP batching.
//
// THE MAP, in WORLD space and in the cylindrical frame about the ship's path:
//
//   Let U be the hull centre, â the unit wake axis (behind the ship), p the vertex, and
//       q = p − U,  x = dot(q, â),  rv = q − x·â,  r = |rv|,  r̂ = rv/r.
//   x is the distance BEHIND the ship, r the distance OUT from its path. The whole deformation
//   is one line — every affected vertex is pushed along its own radius from the path by a
//   dimensionless STRAIN:
//
//       p' = U + x·â + r·(1 + E) · r̂,     E = A · w · g(x/L) · K(r/reach) · sin(ψ − k·x)
//
//   Read it as three factors and it is the wake:
//     • sin(ψ − k·x) is the wave. ψ is integrated at the ship's own speed, so a crest sits at a
//       fixed WORLD position and the ship flies out from under it; raise the publisher's travel
//       factor and the crests stream backward as well.
//     • g(x/L) is the TRAIN: a bump that is exactly zero — value AND first derivative — at the
//       ship's own plane and again one train-length behind it. Those two planes are swept
//       through mass at speed, so a kink at either would read as an invisible wall passing.
//     • K(r/reach) is the RADIAL falloff: 1 on the path itself, flat there (K'(0) = 0, so the
//       ribbon does not crease along its own spine), decaying to exactly zero at the reach.
//
//   WHY A STRAIN AND NOT AN OFFSET. Displacing by a distance D(x,r) along r̂ is the obvious
//   version and it is singular: r̂ is undefined on the axis, and a vertex at r < D crosses the
//   path and turns the prism inside out. Scaling r instead makes the axis a FIXED POINT — the
//   displacement is r·E, which vanishes with r — so the map has no singularity anywhere and
//   never folds, and the bound that guarantees it is one dimensionless number with no hull
//   radius in it (see NO FOLD below).
//
//   FALLOFFS. The train is g(u) = 4·S(u)·S(1−u) on u ∈ [0,1] with S the smoothstep polynomial:
//   exactly 1 at the midpoint, exactly zero with zero slope at both ends. The radial falloff is
//   K(t) = (1 − S(t))^e, the cradle's own family — 1 and flat at t = 0, zero and flat at t = 1
//   for e ≥ 1, which is why e is clamped there (below 1 its derivative diverges at the edge).
//
//   THE NORMAL is the ANALYTIC inverse-transpose of that map. In the orthonormal cylindrical
//   frame (â, r̂, θ̂) the differential of p' is
//       J = [[1, 0, 0], [Rx, b, 0], [0, 0, c]]    (rows â r̂ θ̂, columns x r θ)
//   with  Rx = r·∂E/∂x   (the SHEAR — the part that is unique to a travelling wave, and the
//                         part the harness's negative control switches off),
//         b  = (1+E) + r·∂E/∂r    (radial stretch),
//         c  = 1 + E              (circumferential stretch, Rr/r).
//   Inverting and transposing gives three terms and two divisions:
//       n' = â·(n_a − Rx·n_r/b) + r̂·(n_r/b) + n_θ̂/c.
//   At E ≡ 0 that is n bit for bit, which is what makes the support boundary seamless.
//
//   NO FOLD, BY CONSTRUCTION. The map folds only if b ≤ 0 or c ≤ 0. Both are bounded by the
//   amplitude alone:
//       |E| ≤ A                       so c ≥ 1 − A
//       |r·∂E/∂r| ≤ A·max|t·K'(t)|    so b ≥ 1 − A(1 + max|t·K'(t)|)
//   and max|t·K'(t)| over e ∈ [1,6] is 0.889 (attained at e = 1, t = 2/3). So b > 0 for every
//   A < 1/1.889 = 0.529, which is why PrismWakeConfigSO clamps the amplitude to 0.45 — a proof
//   that needs no hull radius, no reach and no wavelength, and therefore cannot be invalidated
//   by retuning any of them. The harness measures it as well as deriving it.
//
// SLOT SELECTION. With several vessels boosting at once the slot with the greatest authority
// (w·g·K) at this vertex wins outright; the others contribute nothing. Summing two cylindrical
// fields about two different axes is not a cylindrical field about anything, so its normal
// could not be derived analytically — and the seam between two wakes is a place no shading is
// right, whereas a hard handover happens where both are weak.
//
// MESHES. Nothing here reads a tangent, a UV, an adjacency or a face index: the map is a pure
// function of world POSITION and world NORMAL, so it is correct on the high-poly copy the
// residency pass swaps in, on the authored 24-triangle prism, on the shield octahedra and on
// the exploding debris. There is no geometry it can be wrong about — only geometry too coarse
// to show it, which is what the residency pass exists to fix.
//
// COST CONTRACT. A vertex with no wake live executes one integer compare and returns. With one
// live it costs two matrix transforms in, one loop iteration per live slot (≤ 4), one sincos,
// two pow and two transforms out. No fragment cost, no extra varying, no texture, no batch
// split, no material swap, no draw call — the high-poly mesh is SHARED.
//
// KNOWN IMPRECISIONS, both recorded rather than fixed.
//   • The frame is the ship's CURRENT heading, not its path history. A hard turn swings the
//     whole train rather than bending it, so a wake laid through a corner reads as straighter
//     than the corner was. A true path wake needs per-vertex history, which no closed-form map
//     can carry.
//   • Entities Graphics culls by the prism's RenderBounds, which a per-frame global cannot
//     expand. A vertex can move up to A·0.213·reach, so a prism whose bounds are just off
//     screen can carry a rippled face that should be on screen and is culled with the prism.

#ifndef PRISM_WAKE_INCLUDED
#define PRISM_WAKE_INCLUDED

// How many vessels can leave a wake at once. Mirrors PrismWake.Slots in PrismWake.cs — change
// both together, since the arrays are declared at this length. Four is the largest roster any
// arcade mode seats; the publisher keeps the strongest (and then the nearest) if it overflows.
#ifndef PRISM_WAKE_SLOTS
#define PRISM_WAKE_SLOTS 4
#endif

// Floor on the two stretch terms when inverting the Jacobian. The amplitude clamp already
// proves both are positive (see NO FOLD above); this is the guard that makes "already proves"
// not load-bearing against an asset edited past its own range by hand.
#ifndef PRISM_WAKE_MIN_STRETCH
#define PRISM_WAKE_MIN_STRETCH 1e-3
#endif

// The Jacobian's SHEAR term — the off-diagonal that comes from the wave travelling along the
// axis, and the one part of this normal that the cradle's spherical map has no analogue for.
// Shipping value is 1. The harness's negative control rebuilds this file with it at 0 and
// asserts the derivative test then STOPS converging, so "the analytic normal is what holds
// that test" is a measured claim rather than an assumed one.
#ifndef PRISM_WAKE_SHEAR_GAIN
#define PRISM_WAKE_SHEAR_GAIN 1.0
#endif

float4 _PrismWakeCentre[PRISM_WAKE_SLOTS];  // xyz hull centre, w hull radius
float4 _PrismWakeAxis[PRISM_WAKE_SLOTS];    // xyz unit axis (behind the ship), w phase radians
float4 _PrismWakeShape[PRISM_WAKE_SLOTS];   // x strength, y reach, z train length, w wavenumber
float4 _PrismWakeParams;                    // (amplitude, radialExponent, liveSlotCount, 0)

// The train envelope g(u) and its derivative dg/du, together because every caller needs both
// (the position wants g, the normal wants g and g'). g = 4·S(u)·S(1−u) is exactly 1 at the
// midpoint and exactly zero — value and slope — at u = 0 and u = 1. Note S'(u) and S'(1−u) are
// the SAME polynomial (6u(1−u) is symmetric about ½), which is what collapses the derivative to
// one product.
void PrismWakeTrain(float u, out float g, out float dg)
{
    if (u <= 0.0 || u >= 1.0)
    {
        g = 0.0;
        dg = 0.0;
        return;
    }
    float v = 1.0 - u;
    float Su = u * u * (3.0 - 2.0 * u);           // smoothstep(0,1,u)
    float Sv = v * v * (3.0 - 2.0 * v);           // smoothstep(0,1,1-u)
    float dS = 6.0 * u * v;                       // S'(u) == S'(1-u)
    g = 4.0 * Su * Sv;
    dg = 4.0 * dS * (Sv - Su);
}

// The radial falloff K(t) and its derivative dK/dt on t = r/reach. The cradle's own family: 1
// and FLAT at t = 0 (so the ribbon lying on the path does not crease along its spine) and zero
// and flat at t = 1 (so the outer edge of the wake has no seam).
void PrismWakeRadial(float t, float e, out float K, out float dK)
{
    if (t <= 0.0)
    {
        K = 1.0;
        dK = 0.0;
        return;
    }
    if (t >= 1.0)
    {
        K = 0.0;
        dK = 0.0;
        return;
    }
    float S = t * t * (3.0 - 2.0 * t);
    float dS = 6.0 * t * (1.0 - t);
    float u = 1.0 - S;
    K = pow(u, e);
    dK = -e * pow(u, e - 1.0) * dS;
}

// Position and Normal are OBJECT space. They arrive at the END of the prism vertex chain (after
// grow, shield morph, jiggle, flight, suction and the cradle). Outputs are object space too —
// the graph's VertexDescription blocks take object space, and the model matrix is applied after
// this.
void PrismWakeDeform_float(float3 Position, float3 Normal,
    out float3 OutPosition, out float3 OutNormal)
{
    OutPosition = Position;
    OutNormal = Normal;

#if defined(SHADERGRAPH_PREVIEW)
    // The preview has no model matrix and no published bank: identity.
    return;
#else
    int count = (int)_PrismWakeParams.z;
    if (count <= 0)
        return;                                   // master sentinel: nobody is moving fast enough

    float amp = _PrismWakeParams.x;
    float expo = max(_PrismWakeParams.y, 1.0);
    if (!(amp > 0.0))
        return;                                   // unpublished / insane amplitude: off

    // A mesh with no normals (or a degenerate vertex) has no surface to bend. Negated finite
    // test so NaN falls into the reset branch, the idiom the clock functions use.
    float nLenSq = dot(Normal, Normal);
    if (!(nLenSq > 1e-8))
        return;
    float3 nObj = Normal * rsqrt(nLenSq);

    float4x4 M = GetObjectToWorldMatrix();
    float4x4 Minv = GetWorldToObjectMatrix();

    // A prism pulled fresh from the pool sits at localScale ZERO until its creation completes;
    // its model matrix is degenerate and the inverse blows up. The entity is not rendered in
    // that window; the guard is so that "is not" is not load-bearing.
    float3 nW = mul(nObj, (float3x3)Minv);        // a normal transforms by the inverse transpose
    float nwLenSq = dot(nW, nW);
    if (!(nwLenSq > 1e-12) || !(nwLenSq < 1e12))
        return;
    nW *= rsqrt(nwLenSq);

    float3 pW = mul(M, float4(Position, 1.0)).xyz;

    // The slot with the greatest authority (w·g·K) at THIS vertex, resolved before anything is
    // moved. One cylindrical field wins outright — see SLOT SELECTION in the header.
    float bestAuthority = 0.0;
    float bestE = 0.0;
    float bestEx = 0.0;
    float bestEr = 0.0;
    float bestR = 0.0;
    float bestX = 0.0;
    float3 bestAxis = float3(0.0, 0.0, 1.0);
    float3 bestRadial = nW;
    float3 bestU = float3(0.0, 0.0, 0.0);

    for (int i = 0; i < PRISM_WAKE_SLOTS; i++)
    {
        if (i >= count) break;

        float4 shape = _PrismWakeShape[i];
        float w = saturate(shape.x);
        float reach = shape.y;
        float trainLen = shape.z;
        float waveK = shape.w;
        if (!(w > 0.0) || !(reach > 0.0) || !(trainLen > 0.0)) continue;

        float4 axisSlot = _PrismWakeAxis[i];
        float3 axis = axisSlot.xyz;
        // A published axis is always unit; 0.25 rejects an unset slot and a NaN without
        // pretending to renormalise something that carries no direction.
        if (!(dot(axis, axis) > 0.25)) continue;

        float3 q = pW - _PrismWakeCentre[i].xyz;
        float x = dot(q, axis);
        if (!(x > 0.0) || x >= trainLen) continue; // in front of the ship, or past the train

        float3 rv = q - axis * x;
        float r = length(rv);
        if (!(r > 1e-4) || r >= reach) continue;   // on the path line, or outside the reach

        float g, dg;
        PrismWakeTrain(x / trainLen, g, dg);
        float K, dK;
        PrismWakeRadial(r / reach, expo, K, dK);

        float authority = w * g * K;
        if (authority <= bestAuthority) continue;

        float sn, cs;
        sincos(axisSlot.w - waveK * x, sn, cs);
        float aw = amp * w;

        bestAuthority = authority;
        bestE  = aw * g * K * sn;
        bestEx = aw * K * ((dg / trainLen) * sn - g * waveK * cs);
        bestEr = aw * g * (dK / reach) * sn;
        bestR = r;
        bestX = x;
        bestAxis = axis;
        bestRadial = rv / r;
        bestU = _PrismWakeCentre[i].xyz;
    }

    if (!(bestAuthority > 0.0))
        return;                                   // no wake reaches this vertex

    // The map (header): scale the vertex's distance from the path by the local strain. The
    // axis itself is a fixed point, which is what makes the map singularity-free.
    float stretch = 1.0 + bestE;
    float3 pNew = bestU + bestAxis * bestX + bestRadial * (bestR * stretch);

    // The analytic inverse-transpose of that map, in the cylindrical frame.
    float c = max(stretch, PRISM_WAKE_MIN_STRETCH);
    float b = max(stretch + bestR * bestEr, PRISM_WAKE_MIN_STRETCH);
    float shear = bestR * bestEx;

    float na = dot(nW, bestAxis);
    float nr = dot(nW, bestRadial);
    float3 nt = nW - bestAxis * na - bestRadial * nr;   // the circumferential component
    float3 nNew = bestAxis * (na - PRISM_WAKE_SHEAR_GAIN * shear * nr / b)
                + bestRadial * (nr / b)
                + nt / c;
    float nNewLenSq = dot(nNew, nNew);
    if (!(nNewLenSq > 1e-12))
        nNew = nW;

    // Back to object space: the point through the inverse model (w = 1), the normal through the
    // model's transpose (the inverse of the inverse-transpose above), renormalised.
    float3 outPos = mul(Minv, float4(pNew, 1.0)).xyz;
    float3 outNrm = mul(nNew, (float3x3)M);
    float outNrmLenSq = dot(outNrm, outNrm);
    if (!(dot(outPos, outPos) < 1e12) || !(outNrmLenSq > 1e-12))
        return;                                   // degenerate frame: leave the vertex alone

    OutPosition = outPos;
    OutNormal = outNrm * rsqrt(outNrmLenSq);
#endif
}

#endif // PRISM_WAKE_INCLUDED
