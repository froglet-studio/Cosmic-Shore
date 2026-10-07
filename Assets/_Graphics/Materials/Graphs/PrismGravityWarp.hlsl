// PrismGravityWarp.hlsl — the GPU side of the black hole's WARP
// (Docs/BLACK_HOLE.md §5, Docs/PRISM_ANIMATION.md §4.7.4: a citizen of §4.7's "global uniform"
// shape for a prism visual that depends on live gameplay data, and the second after the cradle
// that moves VERTICES.)
//
// PURPOSE. Mass near a black hole's event horizon is drawn TIDALLY STRETCHED toward the
// singularity: the face of a prism nearer the hole is pulled harder than the face farther from
// it, so the prism elongates along the radial and squeezes across it — spaghettification, the
// one thing everybody knows a black hole does to what falls in. It is the hole's signature on
// screen (the horizon itself is a black sphere; what the player SEES is the mass bending around
// it), and it reads on top of the gravity FIELD, which actually moves the prisms. The two are
// separate on purpose: the field is gameplay (positions, colliders, the spatial index), the warp
// is photons, and nothing here changes anything a gameplay query can read.
//
// WHY IT LIVES HERE AND NOT ON THE CPU. "Where is the hole relative to this prism" is live,
// per-frame, per-prism data — the hole moves, the prisms move — so it can never be a per-prism
// stamp (§1: could the GPU have computed this frame's value from what was known at the start?
// No) and a per-prism CPU pass that writes each prism's material is exactly what the
// clock-material law forbids. The sanctioned shape is a GLOBAL uniform (§4.7): O(1) writes per
// frame that every prism reads. The high-poly residency swap PrismGravityWarp.cs performs near a
// horizon is a STATE CHANGE (final at the instant it is applied, like a shield engaging), not an
// animation — the cradle established that distinction and this file inherits it.
//
// THE UNIFORMS (published by BlackHoleWarp.cs once per frame, in LateUpdate):
//   float4 _PrismGravityWarpCentre[N] — xyz: the hole's world-space centre this frame.
//                                       w:   its event-horizon radius r_s (world units).
//   float4 _PrismGravityWarpWeight[N] — x: the STRAIN at the horizon, 0..1 (< 1 always — at 1
//                                          the horizon maps onto the centre and the map folds).
//                                          Eased by the publisher on spawn and despawn so the
//                                          warp never pops on or off.
//                                       y: the REACH beyond the horizon, world units: at
//                                          r_s + reach the displacement, its derivative and the
//                                          normal correction are all exactly zero.
//   float4 _PrismGravityWarpParams    — (exponent, liveSlotCount, 0, 0). liveSlotCount is the
//                                       MASTER SENTINEL: an unpublished global reads as zero,
//                                       and zero must mean "the loop does not execute".
//
// The arrays are declared at FILE SCOPE (Shader Graph has no array property type — which is also
// why wiring this needed no property surgery on either graph) and OUTSIDE every CBUFFER: they are
// per-FRAME globals, and an array inside UnityPerMaterial is what breaks SRP batching.
//
// THE MAP, in WORLD space (a prism's scale is non-uniform, and a radial field about a point is
// only radial where the metric is isotropic — the cradle and the jiggle reach the same
// conclusion):
//
//   Let U be the hole's centre, r_s its horizon, p the vertex, and
//       rad = p − U,  d = |rad|,  dir = rad/d,  s = d − r_s.
//   s is the distance from the vertex to the HORIZON. The whole deformation is one line — every
//   affected vertex slides ALONG ITS OWN RADIUS toward the singularity by a FRACTION of its
//   distance:
//
//       p' = U + dir · f(d),      f(d) = d · (1 − g(d)),      g(d) = w · k(s)
//
//   with k the falloff (1 at and inside the horizon, 0 at the reach) and w the slot's strain.
//   Read it against the physics and it is the tidal field: g is largest where d is smallest, so
//   the near face of a prism moves farther toward the hole than its far face — the prism
//   STRETCHES along the radial — while the tangential scale b = f/d = 1 − g SQUEEZES it across.
//   That the radial stretch really exceeds the tangential squeeze is a theorem of the map, not a
//   tuning: a = f'(d) = (1 − g) − d·g'(d) and g' ≤ 0 everywhere, so a ≥ b with equality only
//   where the falloff is flat. (The harness asserts it.)
//
//   The STRAIN form — a fraction of d rather than an absolute offset — is the choice the
//   prism-morph skill's finding (c) names: a displacement that scales with the coordinate has
//   the hole's centre as a fixed point and is singularity-free, and a hole twice the size warps
//   twice the mass twice as far, which is what "stronger hole" should mean on screen. The price
//   is that w must stay below 1, which the publisher clamps.
//
//   FALLOFF.  t = saturate(s / reach),  k = pow(1 − smoothstep(0, 1, t), e), with k = 1 for
//   s ≤ 0 (inside the horizon the strain is uniform: a vertex already past the horizon is drawn
//   straight into the black sphere, where it is hidden). smoothstep is C1 at BOTH ends, so
//   k'(0) = 0 and k'(reach) = 0 — no kink at the horizon, no seam at the reach. e is clamped ≥ 1
//   because (1−S)^(e−1) diverges at t → 1 below that.
//
//   NO FOLD. f'(d) = (1 − g) − d·g' ≥ 1 − g > 0 since g' ≤ 0 and g < 1: f is strictly increasing
//   in d, so two vertices at different radii keep their order and the prism never turns inside
//   out; and f > 0, so nothing crosses the centre to the far side. Both are harness properties.
//
//   THE NORMAL is the ANALYTIC inverse-transpose of that map, not a re-derivation and not a blend.
//   For p' = U + dir·f(d) the differential is, in the local radial/tangential frame, diag(a, b, b)
//   with a = f'(d) and b = f(d)/d, so the normal transforms by diag(1/a, 1/b, 1/b):
//       n' = normalize( dir·(n·dir)/a + (n − dir·(n·dir))/b ).
//   At s ≥ reach, a = b = 1 and n' = n bit for bit. The cheap alternative — lerp n toward dir —
//   pops where n·dir crosses zero, a line down the middle of every side face (the cradle rejected
//   it on screen); a derivative is proven by its CONVERGENCE RATE in the harness, with a negative
//   control that neuters the radial term and watches the error plateau.
//
// SLOT SELECTION. With more than one hole live, the slot with the greatest authority (g = w·k)
// at this vertex wins outright; the others contribute nothing. Summing two radial fields about
// two centres is not a radial field about anything, so its normal could not be derived.
//
// SPLICE ORDER. This node sits IMMEDIATELY BEFORE the cradle on both live graphs' vertex chains
// (after grow, shield morph, jiggle, flight and suction): the cradle must stay LAST (its header
// says why — it closes mass onto a hull resting on it), and this warp must see every earlier
// stage's position so the stretch applies to the prism as it is drawn. A separate node rather than
// a map kind inside PrismCradle.hlsl, with the skill's rule weighed: the black hole is a world
// object and the cradle is one vessel's ride feel, the two never legitimately fight over a vertex,
// and the structural morph walk (Tools/Shaders/prism_vertex_chain.py) already lets every sibling
// wirer see past any number of morphs. The cost is one integer compare per vertex when no hole is
// live.
//
// MESHES. Nothing here reads a tangent, a UV, an adjacency or a face index: the map is a pure
// function of world POSITION and world NORMAL. So it is correct on any mesh — the high-poly copy
// the residency pass swaps in near a horizon (where it reads as fabric drawn into the hole), the
// authored 24-triangle prism farther out, the shield octahedra, the exploding debris (which is how
// a burst near a hole visibly leans into it with no code of its own).
//
// COST CONTRACT. A vertex with no hole live executes one integer compare and returns. With one
// live it costs: two matrix transforms in, one loop iteration per live slot (≤ 4), a handful of
// transcendentals, and two transforms out. No fragment cost, no extra varying, no texture, no
// batch split, no material swap, no draw call.
//
// KNOWN IMPRECISION. A vertex-stage effect; Entities Graphics culls by the prism's RenderBounds,
// which a per-frame global cannot expand. A vertex can move up to (strain × d), so a prism whose
// bounds are just off-screen can carry a stretched face that should be on-screen. Recorded.

#ifndef PRISM_GRAVITY_WARP_INCLUDED
#define PRISM_GRAVITY_WARP_INCLUDED

// How many black holes can warp at once. Mirrors BlackHoleWarp.Slots in BlackHoleWarp.cs and
// BlackHolePhysics.NativeWells.Capacity — change all three together, since the arrays are
// declared at this length and the config refuses spawns past it.
#ifndef PRISM_GRAVITY_WARP_SLOTS
#define PRISM_GRAVITY_WARP_SLOTS 4
#endif

// Floor on the radial and tangential stretch when inverting the Jacobian. Neither is ever zero
// for a strain below 1, so this only guards an insane bank; it doubles as the harness's negative
// control (clamped to 1 it neuters the radial term, and the derivative test must then plateau).
#ifndef PRISM_GRAVITY_WARP_MIN_RADIAL
#define PRISM_GRAVITY_WARP_MIN_RADIAL 1e-3
#endif

float4 _PrismGravityWarpCentre[PRISM_GRAVITY_WARP_SLOTS];  // xyz world centre, w horizon radius
float4 _PrismGravityWarpWeight[PRISM_GRAVITY_WARP_SLOTS];  // x strain at the horizon 0..1, y reach
float4 _PrismGravityWarpParams;                            // (exponent, liveSlotCount, 0, 0)

// The falloff k(s) and its derivative k'(s), together because every caller needs both (the
// position wants k, the normal wants d·g'). s is the distance beyond the horizon; inside it k is
// exactly 1 and flat, so the strain is uniform and kink-free there.
void PrismGravityWarpFalloff(float s, float reach, float e, out float k, out float dk)
{
    if (s <= 0.0)
    {
        k = 1.0;
        dk = 0.0;
        return;
    }
    if (s >= reach)
    {
        k = 0.0;
        dk = 0.0;
        return;
    }
    float t = s / reach;
    float S = t * t * (3.0 - 2.0 * t);            // smoothstep(0,1,t)
    float dS = 6.0 * t * (1.0 - t);               // dS/dt
    float u = 1.0 - S;
    k = pow(u, e);
    dk = -e * pow(u, e - 1.0) * dS / reach;
}

// Position and Normal are OBJECT space. They arrive after grow, shield morph, jiggle, flight and
// suction, and BEFORE the cradle (see SPLICE ORDER). Outputs are object space too — the next node
// (the cradle) and the graph's VertexDescription blocks take object space.
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
    if (count <= 0)
        return;                                   // master sentinel: no hole is live

    float expo = max(_PrismGravityWarpParams.x, 1.0);

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

    // The slot with the greatest authority (g = w·k) at THIS vertex, resolved before anything is
    // moved. One radial field wins outright — see SLOT SELECTION in the header.
    float bestG = 0.0;
    float bestDg = 0.0;
    float bestD = 0.0;
    float3 bestDir = nW;
    float3 bestU = float3(0.0, 0.0, 0.0);

    for (int i = 0; i < PRISM_GRAVITY_WARP_SLOTS; i++)
    {
        if (i >= count) break;

        float4 slot = _PrismGravityWarpCentre[i];
        float4 weight = _PrismGravityWarpWeight[i];
        float w = saturate(weight.x);
        float reach = weight.y;
        if (!(w > 0.0) || !(slot.w > 0.0) || !(reach > 0.0)) continue;

        float3 rad = pW - slot.xyz;
        float d = length(rad);
        if (!(d > 1e-4)) continue;                // dead centre: no radius to slide along

        float s = d - slot.w;
        if (s >= reach) continue;                 // outside the warp entirely

        float k, dk;
        PrismGravityWarpFalloff(s, reach, expo, k, dk);

        float g = w * k;
        if (g <= bestG) continue;

        bestG = g;
        bestDg = w * dk;                          // g'(d) = w · k'(s), ds/dd = 1
        bestD = d;
        bestDir = rad / d;
        bestU = slot.xyz;
    }

    if (!(bestG > 0.0))
        return;                                   // nothing reaches this vertex

    // The map (header): slide along the radius toward the singularity by the strain fraction.
    float f = bestD * (1.0 - bestG);
    float3 pNew = bestU + bestDir * f;

    // The analytic inverse-transpose of that map, in the radial/tangential frame.
    float a = max((1.0 - bestG) - bestD * bestDg, PRISM_GRAVITY_WARP_MIN_RADIAL);
    float b = max(1.0 - bestG, PRISM_GRAVITY_WARP_MIN_RADIAL);
    float nr = dot(nW, bestDir);
    float3 nt = nW - bestDir * nr;
    float3 nNew = bestDir * (nr / a) + nt / b;
    float nNewLenSq = dot(nNew, nNew);
    if (!(nNewLenSq > 1e-12))
        nNew = bestDir * (nr >= 0.0 ? 1.0 : -1.0);

    // Back to object space: the point through the inverse model (w = 1), the normal through
    // the model's transpose (the inverse of the inverse-transpose above), renormalised.
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
