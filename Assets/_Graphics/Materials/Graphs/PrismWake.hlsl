// PrismWake.hlsl — the GPU side of the WAKE
// (Docs/PRISM_ANIMATION.md §4.7.3, the FOURTH citizen of §4.7's "global uniform" shape for a
// prism visual that depends on live gameplay data, and the SECOND that moves VERTICES.)
//
// PURPOSE — a SHOCKWAVE FRONT. A detonating warhead throws its own wavefront outward through the
// mass around it: a thin spherical SHELL of rippled prisms that is born at the shell's own
// half-thickness and travels out to the exact radius that blast reaches, ONCE, as the blast
// expands. So the shell is not decoration — it is the blast's own volume, drawn on the mass rather
// than on the HUD, and it is honest precisely BECAUSE that blast touches no prism mass: the prisms
// ripple as the shockwave crosses them and are still standing afterwards, which is what happened.
//
// IT IS HALF OF A PAIR, and the pair is the point. On the way in, the round publishes its armed
// fuze volume as a LIT SPHERE (Docs/LIT.md, `Projectile.PublishFuzeLit`): mass standing where this
// warhead WILL go off, in the shooter's domain colour. That says WHERE, statically, in colour. This
// says HOW FAR, kinetically, in vertices, at the moment it happens. Two channels of the surface
// description (colour and vertices), one weapon, and neither one duplicating the other.
//
// WHY A SPHERE, having been a CYLINDER. The first cut framed this cylindrically about the path a
// fast VESSEL had just flown, because a wake is about a path — and it was pulled off the fleet
// after one playtest (an effect strong enough to be an EVENT stops being one when every hull in
// the match trails one). What is left is a warhead BLAST, and a blast's force is not about a path
// at all: it is radial about a point, it has a RADIUS the gameplay already authors, and its front
// travels outward rather than streaming backward. The frame follows the force, so the frame is a
// sphere about the blast — the cradle's frame (§4.7.2), not the old wake's. (The MISSILE in flight
// was the intermediate carrier, and it went for the reason the ball did: a front trailing a
// travelling object is a wake, and a wake is a texture. The blast is the event.)
//
// THE UNIFORMS (published by PrismWake.cs once per frame, in LateUpdate, from a frame-stamped
// registry of live shockwaves):
//   float4 _PrismWakeCentre[N] — xyz: the blast's world centre this frame.
//                                w:   the FRONT radius c, world units — where the shell is right
//                                     now. READ off the carrier's own expanding wavefront, not
//                                     integrated: the blast already owns that number (it is what
//                                     its damage pass uses), so a second clock would only drift.
//   float4 _PrismWakeShape[N]  — x: eased strength 0..1 (the live-or-not weight x the front's own
//                                   envelope, which is exactly zero at birth and at the reach).
//                                y: the front's HALF-THICKNESS sigma, world units.
//                                z: the reach — the blast's own final radius, world units.
//                                   Carried for tooling and residency parity; the MAP does not
//                                   read it, because the support is |r - c| < sigma and nothing
//                                   else.
//                                w: unused, 0.
//   float4 _PrismWakeParams    — (amplitude, wavesInFront, liveSlotCount, 0).
//                                liveSlotCount is the MASTER SENTINEL: an unpublished global
//                                reads as zero, and zero must mean "the loop does not execute".
//   _PrismWakeAxis is GONE. A sphere has no axis. Nothing writes it and nothing reads it.
//
// The arrays are declared at FILE SCOPE, not as graph properties (Shader Graph has no array
// property type — which is why wiring this needed no property surgery on either graph), and
// OUTSIDE every CBUFFER: they are per-FRAME globals, and an array inside UnityPerMaterial is
// what breaks SRP batching. The bank is still FOUR slots under the same names, which is why this
// supersession carries none of the pinned-array-length hazard Docs/LIT.md records — Unity pins a
// global array's length at its first write for the whole session, keyed on the NAME, and a length
// that does not change cannot be pinned wrong.
//
// THE MAP, in WORLD space and in the spherical frame about the blast:
//
//   Let U be the blast's centre, p the vertex, q = p - U, r = |q|, r_hat = q/r, and
//       s = (r - c) / sigma        (where the vertex sits across the shell, -1 .. 1)
//
//       P(s) = (1 - s^2)^2 * sin(2*pi*Q*s)            for |s| < 1, and EXACTLY 0 outside
//       f(r) = r + sigma * A * w * P(s)
//       p'   = U + f(r) * r_hat
//
//   Read it as two factors and it is a shockwave front:
//     * sin(2*pi*Q*s) is the pulse. Q whole cycles ACROSS THE SHELL and nothing outside it — the
//       short bandwidth is the whole design. The old cylinder carried a TRAIN of crests filling
//       its whole support; this carries one wavelet, so what the eye follows is an edge arriving
//       and passing rather than a standing corrugation.
//     * (1 - s^2)^2 is the window that makes "and nothing outside it" true to FIRST DERIVATIVE as
//       well as value: it has a DOUBLE root at both faces, so P and P' both vanish there on the
//       window's own account, whatever Q is. That matters because the shell's two faces sweep
//       through mass at speed and a kink at either would read as an invisible wall passing.
//       Taking Q WHOLE then buys the second derivative as well (the sine vanishes at the faces
//       too, so P'' does), and buys the honest reason: a whole number of cycles is a complete
//       wavelet rather than one cut off mid-swing.
//
//   WHY THE AMPLITUDE IS ABSOLUTE AND NOT A STRAIN. The old cylinder scaled r by (1+E), a
//   dimensionless strain, which made displacement grow with distance from the path — correct for
//   a 30-unit reach and wrong for a 95-unit one, where the outermost mass would move 40 units.
//   Here the displacement is sigma*A*w*P, BOUNDED by sigma*A everywhere: a front of a given
//   thickness carries a ripple of a given depth, which is what a front is. The strain map's one
//   real virtue is kept for free — the centre is still a fixed point, because P vanishes at the
//   shell's inner face and the shell never reaches the centre (see NO FOLD).
//
//   THE NORMAL is the ANALYTIC inverse-transpose of that map, and it is the CRADLE's shape rather
//   than the old wake's. A purely radial f(r) has differential diag(a, b, b) in (r_hat, theta,
//   phi), so there is NO shear term at all — the old one existed only because that wave travelled
//   along an axis while displacing along a radius, and here the travel IS the radius:
//       a = f'(r) = 1 + A*w*P'(s)        (radial stretch)
//       b = f(r)/r                       (both tangential stretches)
//       n' = r_hat*(n_r/a) + (n - r_hat*n_r)/b
//   At P == 0 that is n bit for bit, which is what makes the shell's faces seamless.
//
//   NO FOLD, BY CONSTRUCTION, AND ONE CONDITION DOES BOTH. The map folds if a <= 0 or b <= 0.
//     * a > 0  <=>  A*w*|P'(s)| < 1 everywhere, and max|P'| = 2*pi*Q, attained at s = 0. So
//       A < 1/(2*pi*Q): one dimensionless number, with no radius, no reach and no thickness in
//       it, so retuning any of those cannot invalidate it. PrismWakeConfigSO derives the clamp
//       from Q rather than hard-coding it.
//     * b > 0 follows from a > 0 and needs no second bound. a > 0 means f is strictly increasing,
//       so on the support f(r) > f(c - sigma) = c - sigma, and the publisher guarantees the front
//       is born at c >= sigma (the shell never straddles the centre). Hence f > 0, hence b > 0.
//   The harness measures both rather than trusting the derivation.
//
// SLOT SELECTION. With several shockwaves live the slot with the greatest authority
// (w * (1-s^2)^2) at this vertex wins outright; the others contribute nothing. Summing two
// spherical fields about two different centres is not a spherical field about anything, so its
// normal could not be derived analytically — and the seam between two fronts is a place no
// shading is right, whereas a hard handover happens where both are weak.
//
// MESHES. Nothing here reads a tangent, a UV, an adjacency or a face index: the map is a pure
// function of world POSITION and world NORMAL, so it is correct on the high-poly copy the
// residency pass swaps in, on the authored 24-triangle prism, on the shield octahedra and on
// the exploding debris. There is no geometry it can be wrong about — only geometry too coarse
// to show it, which is what the residency pass exists to fix.
//
// COST CONTRACT. A vertex with no shockwave live executes one integer compare and returns. With
// one live it costs two matrix transforms in, one loop iteration per live slot (<= 4), one
// sincos and two transforms out — one pow FEWER than the cylinder, since both falloffs collapsed
// into one polynomial window. No fragment cost, no extra varying, no texture, no batch split, no
// material swap, no draw call — the high-poly mesh is SHARED.
//
// KNOWN IMPRECISIONS, both recorded rather than fixed.
//   * (RETIRED by the carrier move, and worth keeping as a record of what the move bought.) While
//     the carrier was the round IN FLIGHT, the front was a sphere about a MOVING centre, so a pulse
//     launched a moment earlier was re-centred on where the round was NOW rather than on where it
//     left from — an imprecision no closed-form map can fix without per-pulse history. A detonating
//     blast does not move, so the sphere is about a fixed point for its whole sweep and the
//     imprecision is gone by construction rather than by tuning.
//   * Entities Graphics culls by the prism's RenderBounds, which a per-frame global cannot
//     expand. A vertex can move up to sigma*A, so a prism whose bounds are just off screen can
//     carry a rippled face that should be on screen and is culled with the prism.

#ifndef PRISM_WAKE_INCLUDED
#define PRISM_WAKE_INCLUDED

// How many shockwaves can be live at once. Mirrors PrismWake.Slots in PrismWake.cs — change both
// together, since the arrays are declared at this length (and see the header on why keeping it at
// four is what makes this supersession free of the pinned-length hazard).
#ifndef PRISM_WAKE_SLOTS
#define PRISM_WAKE_SLOTS 4
#endif

// Floor on the two stretch terms when inverting the Jacobian. The amplitude clamp already proves
// both are positive (see NO FOLD in the header); this is the guard that makes "already proves"
// not load-bearing against an asset edited past its own range by hand.
#ifndef PRISM_WAKE_MIN_STRETCH
#define PRISM_WAKE_MIN_STRETCH 1e-3
#endif

// The Jacobian's RADIAL stretch term a = f'(r) — the one that carries the whole derivative of the
// pulse, and therefore the whole of what makes this normal the map's own. Shipping value is 1. The
// harness's negative control rebuilds this file with it at 0 (so a collapses to 1, the identity
// stretch) and asserts the derivative test then STOPS converging, so "the analytic normal is what
// holds that test" is a measured claim rather than an assumed one.
#ifndef PRISM_WAKE_RADIAL_GAIN
#define PRISM_WAKE_RADIAL_GAIN 1.0
#endif

float4 _PrismWakeCentre[PRISM_WAKE_SLOTS];  // xyz round centre, w front radius c
float4 _PrismWakeShape[PRISM_WAKE_SLOTS];   // x strength, y half-thickness sigma, z reach, w 0
float4 _PrismWakeParams;                    // (amplitude, wavesInFront, liveSlotCount, 0)

// The front's wavelet P(s) and its derivative dP/ds, together because every caller needs both
// (the position wants P, the normal wants P'). P = (1-s^2)^2 * sin(2*pi*Q*s) is exactly zero —
// value AND slope — at both faces of the shell, because the window has a double root there; a
// whole Q kills the curvature too. max|P'| = 2*pi*Q exactly, at s = 0, which is the number the
// amplitude clamp is derived from.
void PrismWakeFront(float s, float cycles, out float P, out float dP)
{
    if (s <= -1.0 || s >= 1.0)
    {
        P = 0.0;
        dP = 0.0;
        return;
    }
    float e = 1.0 - s * s;
    float win = e * e;                            // (1 - s^2)^2
    float dwin = -4.0 * s * e;                    // d/ds (1 - s^2)^2
    float k = 6.28318530718 * cycles;             // 2*pi*Q
    float sn, cs;
    sincos(k * s, sn, cs);
    P = win * sn;
    dP = dwin * sn + win * k * cs;
}

// Position and Normal are OBJECT space. They arrive in the middle of the prism vertex chain (after
// grow, shield morph, sway, jiggle, flight and suction, and BEFORE the cradle — the cradle closes
// mass onto a hull resting on it and must see the rippled position). Outputs are object space too
// — the graph's VertexDescription blocks take object space, and the model matrix is applied after.
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
        return;                                   // master sentinel: no shockwave is live

    float amp = _PrismWakeParams.x;
    float cycles = max(_PrismWakeParams.y, 1.0);
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

    // The slot with the greatest authority (w * window) at THIS vertex, resolved before anything
    // is moved. One spherical field wins outright — see SLOT SELECTION in the header.
    float bestAuthority = 0.0;
    float bestP = 0.0;
    float bestdP = 0.0;
    float bestR = 0.0;
    float bestSigma = 0.0;
    float bestW = 0.0;
    float3 bestRadial = nW;
    float3 bestU = float3(0.0, 0.0, 0.0);

    for (int i = 0; i < PRISM_WAKE_SLOTS; i++)
    {
        if (i >= count) break;

        float4 shape = _PrismWakeShape[i];
        float w = saturate(shape.x);
        float sigma = shape.y;
        if (!(w > 0.0) || !(sigma > 0.0)) continue;

        float4 centreSlot = _PrismWakeCentre[i];
        float front = centreSlot.w;
        if (!(front > 0.0)) continue;             // no front in flight in this slot

        float3 q = pW - centreSlot.xyz;
        float r = length(q);
        if (!(r > 1e-4)) continue;                // at the blast's own centre: nothing to push

        float s = (r - front) / sigma;
        if (s <= -1.0 || s >= 1.0) continue;      // outside the shell

        float e = 1.0 - s * s;
        float authority = w * e * e;
        if (authority <= bestAuthority) continue;

        float P, dP;
        PrismWakeFront(s, cycles, P, dP);

        bestAuthority = authority;
        bestP = P;
        bestdP = dP;
        bestR = r;
        bestSigma = sigma;
        bestW = w;
        bestRadial = q / r;
        bestU = centreSlot.xyz;
    }

    if (!(bestAuthority > 0.0))
        return;                                   // no front reaches this vertex

    // The map (header): displace the vertex along its own radius from the blast by the front's
    // wavelet, in world units. The centre is a fixed point — the shell never reaches it.
    float aw = amp * bestW;
    float f = bestR + bestSigma * aw * bestP;
    float3 pNew = bestU + bestRadial * f;

    // The analytic inverse-transpose of that map, in the spherical frame. No shear term: a purely
    // radial f(r) has a diagonal differential.
    float a = max(1.0 + PRISM_WAKE_RADIAL_GAIN * aw * bestdP, PRISM_WAKE_MIN_STRETCH);
    float b = max(f / bestR, PRISM_WAKE_MIN_STRETCH);

    float nr = dot(nW, bestRadial);
    float3 nt = nW - bestRadial * nr;             // the two tangential components together
    float3 nNew = bestRadial * (nr / a) + nt / b;
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
