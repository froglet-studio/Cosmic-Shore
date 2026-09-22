// PrismCradle.hlsl — the GPU side of the Urchin's CRADLE
// (Docs/PRISM_ANIMATION.md §4.7.2, the THIRD citizen of §4.7's "global uniform" shape for a
// prism visual that depends on live gameplay data, and the first that moves VERTICES.)
//
// PURPOSE. While an Urchin is RIDING a prismscape — attached, not launched, not in free flight
// — the mass around it DRAPES over the hull like a silky fabric: the whole surface of a nearby
// prism flows toward the hull's sphere, closing over the parts of the ship it swallows and
// rising to meet the parts it has not reached yet. The pilot reads it as being CRADLED by the
// mass they are grinding.
//
// WHAT THIS REPLACED, AND WHY. The first two rounds moved the prism's own 24 triangles: the
// wedge nearest the hull swung to face it, its three neighbours came partway, everything else
// stayed put. It was faithful to the letter of the request and it read badly on screen twice —
// a rigid triangle rotating about its own centroid is a FACET turning, and twenty-four facets
// is not enough surface for a turning facet to read as anything but a glitch. Tuning it down
// 10x only made a small glitch. The fix is not a different rigid motion; it is MORE SURFACE and
// a SMOOTHER map. A handful of prisms are ever in the band, so those prisms are swapped to a
// high-poly copy (PrismCradle.cs's residency pass, HighPolyPrismMesh's subdivided cube — the
// identical solid, ~3k triangles instead of 24), and the deformation below is a single smooth
// radial field with an ANALYTIC normal, so the surface bends instead of hinging.
//
// WHY IT LIVES HERE AND NOT ON THE CPU. "Where is the hull relative to this prism" is live,
// per-frame, per-prism data: it changes every frame for every prism as the Urchin slides. It
// can therefore never be a per-prism stamp (§1: could the GPU have computed this frame's value
// from what was known at the start? No — the hull moved), and a per-prism CPU pass that writes
// each prism's material is exactly what the clock-material law exists to prevent. The law's
// sanctioned shape is a GLOBAL uniform (§4.7): O(1) writes per frame that every prism reads.
// Note the residency swap is NOT an exception to that — a mesh override is a STATE CHANGE
// (final at the instant it is applied, exactly like a shield engaging), not an animation; the
// animation is still f(clock-free live uniform) with zero per-prism CPU per frame.
//
// THE UNIFORMS (published by PrismCradle.cs once per frame, in LateUpdate, from a
// frame-stamped registry of riding Urchins):
//   float4 _PrismCradleCentre[N] — xyz: the hull's world-space centre this frame.
//                                  w:   the hull's radius (world units). The Urchin is
//                                       spherical to a good approximation, and its radius is
//                                       CONSTANT — it rides beside the centre only because a
//                                       slot is one float4; it is measured once at bind.
//   float4 _PrismCradleWeight[N] — x: strength 0..1. Eased in on attach and out on detach by
//                                  the publisher, so the cradle never pops on or off (continuity
//                                  of existence applies to a deformation as much as to mass).
//   float4 _PrismCradleParams    — (drapeReach, drapeExponent, liveSlotCount, 0).
//                                  liveSlotCount is the MASTER SENTINEL: an unpublished global
//                                  reads as zero, and zero must mean "the loop does not
//                                  execute" — the same rule PrismDestructionSight's peer bank
//                                  records.
//
// The arrays are declared at FILE SCOPE here, not as graph properties (Shader Graph has no
// array property type — which is also why wiring this needed no property surgery on either
// graph), and OUTSIDE every CBUFFER: they are per-FRAME globals, and an array inside
// UnityPerMaterial is what breaks SRP batching.
//
// THE MAP, in WORLD space (the prism's scale is non-uniform — a trail slab is long and thin —
// and a radial field about a sphere is only radial in a frame where the metric is isotropic;
// PrismJiggleClock reaches the same conclusion from the other direction):
//
//   Let U be the hull centre, R its radius, p the vertex, and
//       rad = p − U,  d = |rad|,  dir = rad/d,  s = d − R.
//   s is the SIGNED distance from the vertex to the hull's SURFACE. The whole deformation is
//   one line — every affected vertex slides ALONG ITS OWN RADIUS toward that surface:
//
//       p' = U + dir · f(d),      f(d) = d − s · k(s) · w
//
//   with k the drape falloff and w the slot's eased strength. Read it in two halves and it is
//   the cradle:
//     • s < 0 — the vertex is INSIDE the hull. k = 1, so at full strength f(d) = R exactly:
//       the mass the ship would have intersected closes onto its surface instead. This is the
//       WRAP, and it is what makes a prism the Urchin is buried in read as enveloping it
//       rather than clipping through it.
//     • s > 0 — the vertex is OUTSIDE. k falls smoothly from 1 at the surface to 0 at
//       drapeReach, so the surface RISES to meet the hull near it and is perfectly still
//       farther out. This is the lip of the cradle, and it is why the effect has no edge: at
//       s = drapeReach the displacement, its first derivative and the normal correction are
//       all exactly zero, so there is no seam where the drape ends.
//   One field, one line, no facets, no adjacency, no per-triangle special case — which is
//   precisely what the previous two rounds could not offer.
//
//   FALLOFF.  t = saturate(s / reach),  k = pow(1 − smoothstep(0, 1, t), e).
//   smoothstep is C1 at BOTH ends (S'(0) = S'(1) = 0), which is what makes k'(0) = 0 and
//   k'(reach) = 0 — the two places a kink would show. e is the silkiness: 1 is a broad, soft
//   drape, larger pulls the fabric tight against the hull with a longer flat tail. It is
//   clamped ≥ 1 because (1−S)^(e−1) diverges at t → 1 below that.
//
//   THE NORMAL is the ANALYTIC inverse-transpose of that map, not a re-derivation and not a
//   blend toward the sphere normal. For p' = U + dir·f(d) the differential is, in the local
//   radial/tangential frame, diag(a, b, b) with
//       a = f'(d) = 1 − w·(k + s·k'(s))        (radial stretch)
//       b = f(d)/d                              (tangential stretch)
//   so the normal transforms by diag(1/a, 1/b, 1/b):
//       n' = normalize( dir·(n·dir)/a + (n − dir·(n·dir))/b ).
//   Two consequences worth stating. At full strength inside the hull a = 1 − w → 0, and the
//   guarded 1/a drives n' onto dir — which is CORRECT and not a degeneracy: a patch flattened
//   onto a sphere has the sphere's normal. And at s ≥ reach, a = b = 1 and n' = n bit for bit.
//   The obvious cheap alternative — lerping n toward dir·sign(n·dir) — was rejected: it pops
//   discontinuously wherever n·dir crosses zero, which on the SIDE faces of the very prism the
//   Urchin is riding is a line straight down the middle of the effect.
//
// SLOT SELECTION. With more than one Urchin riding, the slot with the greatest authority
// (k·w) at this vertex wins outright; the others contribute nothing. Summing two radial fields
// about two different centres is not a radial field about anything, so its normal could not be
// derived analytically — and two Urchins close enough to fight over one vertex is a case that
// never occurs in play, while a visibly wrong normal on every vertex would.
//
// MESHES. Nothing here reads a tangent, a UV, an adjacency or a face index: the map is a pure
// function of world POSITION and world NORMAL. So it is correct on any mesh — the high-poly
// copy the residency pass swaps in (where it looks like fabric), the authored 24-triangle
// prism (where it looks like a coarse approximation of the same solid, which is what a prism
// just outside the residency band should look like), the built-in cube, the shield octahedra,
// the exploding debris. There is no geometry it can be wrong about.
//
// COST CONTRACT. A vertex with no cradle live executes one integer compare and returns. With
// one live it costs: two matrix transforms in, one loop iteration per live slot (≤ 4), a
// handful of transcendentals, and two transforms out. No fragment cost, no extra varying, no
// texture, no batch split, no material swap, no draw call — the high-poly mesh is SHARED, so
// the swapped prisms stay in one instanced batch of their own.
//
// KNOWN IMPRECISION. The displacement is a vertex-stage effect, and Entities Graphics culls by
// the prism's RenderBounds, which this does not expand (a stamp could; a per-frame global
// cannot). A vertex can move up to drapeReach, so a prism whose bounds are just off-screen can
// carry a draped face that should be on-screen and is culled with the prism. In practice the
// reach is a few units and the ride camera sits 6.7 u off the hull looking at it, so prisms in
// the band are near the centre of the frame; recorded rather than fixed.

#ifndef PRISM_CRADLE_INCLUDED
#define PRISM_CRADLE_INCLUDED

// How many riding Urchins can cradle at once. Mirrors PrismCradle.Slots in PrismCradle.cs —
// change both together, since the arrays are declared at this length. Four is the largest
// roster any Urchin mode seats (Hijack and Skein, MaxPlayersAllowed 4). The publisher keeps
// the strongest if it ever overflows.
#ifndef PRISM_CRADLE_SLOTS
#define PRISM_CRADLE_SLOTS 4
#endif

// Floor on the radial stretch when inverting the Jacobian. At full strength inside the hull
// the true value is 0 (the interior collapses onto the sphere), and the floor is what turns
// that into "the normal is the sphere's" instead of a divide by zero.
#ifndef PRISM_CRADLE_MIN_RADIAL
#define PRISM_CRADLE_MIN_RADIAL 1e-3
#endif

float4 _PrismCradleCentre[PRISM_CRADLE_SLOTS];  // xyz world centre, w hull radius
float4 _PrismCradleWeight[PRISM_CRADLE_SLOTS];  // x strength 0..1
float4 _PrismCradleParams;                      // (drapeReach, drapeExponent, liveSlotCount, 0)

// The drape falloff k(s) and its derivative k'(s), together because every caller needs both
// (the position wants k, the normal wants k + s·k'). s is the signed distance to the hull
// surface; inside the hull k is exactly 1 and flat, so the wrap is complete and kink-free.
void PrismCradleFalloff(float s, float reach, float e, out float k, out float dk)
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

// Position and Normal are OBJECT space. They arrive at the END of the prism vertex chain
// (after grow, shield morph, jiggle, flight and suction). Outputs are object space too — the
// graph's VertexDescription blocks take object space, and the model matrix is applied after
// this.
void PrismCradleDeform_float(float3 Position, float3 Normal,
    out float3 OutPosition, out float3 OutNormal)
{
    OutPosition = Position;
    OutNormal = Normal;

#if defined(SHADERGRAPH_PREVIEW)
    // The preview has no model matrix and no published bank: identity.
    return;
#else
    int count = (int)_PrismCradleParams.z;
    if (count <= 0)
        return;                                   // master sentinel: no Urchin is riding

    float reach = _PrismCradleParams.x;
    float expo = max(_PrismCradleParams.y, 1.0);
    if (!(reach > 0.0))
        return;                                   // unpublished / insane reach: off

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

    // The slot with the greatest authority (k·w) at THIS vertex, resolved before anything is
    // moved. One radial field wins outright — see SLOT SELECTION in the header.
    float bestAuthority = 0.0;
    float bestK = 0.0;
    float bestDk = 0.0;
    float bestW = 0.0;
    float bestD = 0.0;
    float bestS = 0.0;
    float3 bestDir = nW;
    float3 bestU = float3(0.0, 0.0, 0.0);

    for (int i = 0; i < PRISM_CRADLE_SLOTS; i++)
    {
        if (i >= count) break;

        float4 slot = _PrismCradleCentre[i];
        float w = saturate(_PrismCradleWeight[i].x);
        if (!(w > 0.0) || !(slot.w > 0.0)) continue;

        float3 rad = pW - slot.xyz;
        float d = length(rad);
        if (!(d > 1e-4)) continue;                // dead centre: no radius to slide along

        float s = d - slot.w;
        if (s >= reach) continue;                 // outside the drape entirely

        float k, dk;
        PrismCradleFalloff(s, reach, expo, k, dk);

        float authority = k * w;
        if (authority <= bestAuthority) continue;

        bestAuthority = authority;
        bestK = k;
        bestDk = dk;
        bestW = w;
        bestD = d;
        bestS = s;
        bestDir = rad / d;
        bestU = slot.xyz;
    }

    if (!(bestAuthority > 0.0))
        return;                                   // nothing reaches this vertex

    // The map (header): slide along the radius toward the hull's surface.
    float f = bestD - bestS * bestK * bestW;
    float3 pNew = bestU + bestDir * f;

    // The analytic inverse-transpose of that map, in the radial/tangential frame.
    float a = max(1.0 - bestW * (bestK + bestS * bestDk), PRISM_CRADLE_MIN_RADIAL);
    float b = max(f / bestD, PRISM_CRADLE_MIN_RADIAL);
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

#endif // PRISM_CRADLE_INCLUDED
