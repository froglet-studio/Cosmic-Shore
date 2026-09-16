// PrismCradle.hlsl — the GPU side of the Urchin's CRADLE
// (Docs/PRISM_ANIMATION.md §4.7.2, the THIRD citizen of §4.7's "global uniform" shape for a
// prism visual that depends on live gameplay data).
//
// PURPOSE. While an Urchin is RIDING a prismscape — attached, not launched, not in free flight
// — the mass around it wraps itself onto the hull, ONE TRIANGLE AT A TIME. A prism face is four
// triangles fanned from its centre (a WEDGE each); the wedge whose centroid is nearest the hull
// swings so that its normal points at the hull's centre and its centroid sits ON the hull's
// surface, on the line from where it was to that centre. Its three ADJACENT wedges — the two
// beside it on the same face and the one across the prism's edge on the next face — come
// PARTWAY, by how nearly as close as the nearest they are, the cross-edge one WRAPPING around
// the edge so its OUTWARD face is what meets the hull. Every other triangle on the prism is
// untouched. As the hull rolls from one wedge to the next the two meet at the seam both
// touching the sphere, and one hands off to the other with nothing snapping. The pilot reads
// it as being CRADLED by the mass they are grinding.
//
// WHY IT LIVES HERE AND NOT ON THE CPU. "Where is the hull relative to this prism" is live,
// per-frame, per-prism data: it changes every frame for every prism as the Urchin slides. It
// can therefore never be a per-prism stamp (§1: could the GPU have computed this frame's value
// from what was known at the start? No — the hull moved), and a per-prism CPU pass that finds
// the prisms within range and writes each one's material is exactly what the clock-material
// law exists to prevent. The law's sanctioned shape for this case is a GLOBAL uniform (§4.7):
// O(1) writes per frame that every prism reads, zero per-prism CPU, zero material swaps, zero
// per-instance overrides. "Every prism in range has its material updated with the vessel
// position" is therefore satisfied literally, and for every prism at once, by publishing the
// vessel position ONCE. Sibling of PrismOcclusionCorridor.hlsl and PrismDestructionSight.hlsl.
//
// THE UNIFORMS (published by PrismCradle.cs once per frame, in LateUpdate, from a
// frame-stamped registry of riding Urchins):
//   float4 _PrismCradleCentre[N] — xyz: the hull's world-space centre this frame.
//                                  w:   the hull's radius (world units). The Urchin is
//                                       spherical to a good approximation, and its radius is
//                                       CONSTANT — it is written every frame beside the centre
//                                       because a slot is one float4, but nothing computes it
//                                       per frame: it is measured once at bind.
//   float4 _PrismCradleWeight[N] — x: strength 0..1. Eased in on attach and out on detach by
//                                  the publisher, so the cradle never pops on or off (continuity
//                                  of existence applies to a deformation as much as to mass).
//   float4 _PrismCradleParams    — (outerRange, innerRange, liveSlotCount, neighbourSpread).
//                                  The band: zero effect at a wedge-centroid distance of
//                                  outerRange (15 u), full at innerRange (10 u) and closer.
//                                  liveSlotCount is the MASTER SENTINEL: an unpublished global
//                                  reads as zero, and zero must mean "the loop does not
//                                  execute" — the same rule PrismDestructionSight's peer bank
//                                  records. neighbourSpread (world units) is how much FARTHER
//                                  than the nearest wedge a neighbour may be and still come
//                                  partway; at that excess and beyond it stays put.
//
// The arrays are declared at FILE SCOPE here, not as graph properties (Shader Graph has no
// array property type — which is also why wiring this needed no property surgery on either
// graph), and OUTSIDE every CBUFFER: they are per-FRAME globals, and an array inside
// UnityPerMaterial is what breaks SRP batching.
//
// THE GEOMETRY, per WEDGE, in WORLD space (the prism's scale is non-uniform — a trail slab is
// long and thin — and a rigid motion is only rigid in a frame where the metric is isotropic;
// PrismJiggleClock reaches the same conclusion from the other direction):
//
//   1. WHICH WEDGE this vertex belongs to. The prism mesh (Prism.asset: 72 verts, 24 tris) is
//      HARD-EDGED and fanned: each face is four triangles sharing a duplicated centre vertex,
//      and every vertex carries its face's NORMAL n plus a TANGENT t that points from the face
//      centre straight at ITS OWN wedge's outer edge. So (n, t) names the wedge, and the
//      bitangent b = n × t names the two wedges beside it. A vertex shader cannot see its
//      neighbours; it does not need to — the mesh already told it which triangle it is in.
//   2. The wedge CENTROID. The face plane passes at h = dot(n, v) from the object origin (the
//      half-extent along n, identical for every vertex of the face), the wedge's outer edge is
//      h·t further out, and a triangle's centroid is a third of the way from the apex: so the
//      centroid is h·(n + ⅔t) in object space — the same point for all three of the wedge's
//      vertices, which is what makes the motion below rigid per wedge. It survives every
//      earlier stage of the vertex chain (grow scale, jiggle, the suction lerp keep dot(n, v)
//      constant across a face) because h is read off the live position, not assumed.
//   3. WHICH WEDGE IS NEAREST, and by how much. The hull's distance to every one of the
//      prism's 24 wedge centroids is a closed form of the three model-matrix columns (each
//      centroid is origin ± h·col_a ± ⅔h·col_b), so the vertex can compute the minimum
//      itself — 24 lengths, no neighbour access, no bake. The wedge whose own distance IS that
//      minimum is the nearest; every other wedge's EXCESS over it is what decides whether it
//      comes partway:  near(X) = 1 − smoothstep(0, neighbourSpread, d_X − d_min).
//   4. ADJACENCY, continuously. Only the nearest wedge and its three neighbours may move. A
//      wedge W's weight is
//          A(W) = near(W) × max(near(W)^STRAY, near(N1), near(N2), near(N3))
//      with N1, N2 the two wedges beside it on its face ((n, ±b)) and N3 the one across its
//      outer edge ((t, n) — on the face whose normal is t, tangent pointing back at the shared
//      edge). The nearest wedge scores 1 × 1. A neighbour of the nearest scores near × 1 — it
//      comes PARTWAY, by how nearly as close as the nearest it is. A wedge adjacent to nothing
//      near the minimum is a STRAY and scores near^(1+STRAY), and near is exactly 0 beyond the
//      spread, so with a clear nearest wedge — every non-adjacent wedge more than the spread
//      farther than it — everything else on the prism is untouched, bit for bit.
//      Why the max instead of a hard "is my neighbour the nearest" test: the nearest wedge
//      CHANGES as the hull slides, and a hard test pops the new nearest's other neighbours in
//      from zero on the frame it takes over; worse, the nearest can change to a NON-adjacent
//      wedge (around a prism corner six wedges meet in a ring, and a path over the corner can
//      cross straight from one to its ring-opposite), and a hard test would then drop the old
//      nearest from full to nothing in one frame. near() is continuous in the hull's position
//      and so is a max of it, so this weight never jumps — at any hand-off both wedges read
//      near = 1 and both neighbourhoods are already lit. The price of that continuity is the
//      stray term: a non-adjacent wedge nearly tied with the nearest (a thin prism's other
//      side wedge, a square face's opposite wedge) comes a little way too, suppressed by the
//      STRAY power rather than cut, because "exactly zero until it is the nearest, then one"
//      is the snap the request forbids.
//   5. The TARGET. With U the hull centre and R its radius: the wedge's new normal is
//      nT = normalize(U − c) (pointing at the hull), its new centroid is cT = U − nT·R (on the
//      hull's surface, on the line from c to U — "the centroid moves along the line connecting
//      its old position to the vessel position, and stops at the surface").
//   6. The MOTION is the minimal rotation taking the wedge normal onto nT, applied about the
//      centroid, plus the translation c → cT, BOTH scaled by the weight w. Blending the rigid
//      motion (angle × w, translation × w) rather than the vertex positions keeps the wedge
//      rigid at EVERY intermediate weight, so a half-cradled wedge is a whole triangle half-way
//      there, never a shrunken one. The cross-edge neighbour WRAPS by construction: its normal
//      is carried onto the direction to the hull, so it is always its OUTWARD face that
//      arrives at the surface, never its back.
//   7. The WEIGHT is band × adjacency × facing × strength. Band is the 15 → 10 u ramp at the
//      wedge centroid. FACING is the one term the request did not name and the geometry
//      demands: a wedge whose normal points AWAY from the hull is on the prism's far side.
//      Wrapping it means a 180° flip about an axis the cross product cannot define — and the
//      Urchin rides the CENTRELINE of a trail, so the far side's normal line passes through the
//      hull constantly and the flip's axis would swing through every direction as it does,
//      spinning the triangle. The gate fades such wedges out between dot(n, nT) = −0.5 and 0,
//      at which point the rotation axis is always well conditioned (|sin| ≥ 0.87 wherever the
//      weight is nonzero). Nothing is lost on screen: a wedge pointing away from the hull is
//      behind the ones that point toward it. Wedges at 90° (the sides of the prism the hull sits
//      on, the faces of the neighbours it is rolling toward) are ABOVE the gate and wrap.
//
// MESHES THAT ARE NOT THE FANNED PRISM. A vertex only ever asks for its own (n, t), so any
// hard-edged mesh with a per-face tangent still moves rigidly per face: the legacy blocks on
// the built-in cube (two triangles per face, one tangent per face) and the shield octahedra
// move as whole faces about a pivot ⅔h along their tangent — the per-FACE behaviour this
// file shipped with first, no longer centred. A degenerate tangent falls back to the face
// foot as the pivot.
//
// COST CONTRACT. A vertex with no cradle live executes one integer compare and returns. With
// one live it costs: four matrix transforms, one loop iteration per live slot (≤ 4), 27 lengths
// (24 wedges for the minimum + 3 neighbours), the smoothsteps, and — only inside the band —
// two Rodrigues rotations and two more transforms. It is VERTEX work on a 72-vertex mesh; no
// fragment cost, no extra varying, no texture, no batch split, no material swap, no draw call.
// Everything stays in the same instanced batch.
//
// KNOWN IMPRECISION. The displacement is a vertex-stage effect, and Entities Graphics culls by
// the prism's RenderBounds, which this does not expand (a stamp could; a per-frame global
// cannot). A wedge can move up to outerRange from its prism, so a prism whose bounds are just
// off-screen can carry a wrapped wedge that should be on-screen and is culled with the prism.
// In practice the band is 15 u and the ride camera sits 6.7 u off the hull looking at it, so
// prisms in the band are near the centre of the frame; recorded rather than fixed.

#ifndef PRISM_CRADLE_INCLUDED
#define PRISM_CRADLE_INCLUDED

// How many riding Urchins can cradle at once. Mirrors PrismCradle.Slots in PrismCradle.cs —
// change both together, since the arrays are declared at this length. Four is the largest
// roster any Urchin mode seats (Hijack and Skein, MaxPlayersAllowed 4). The publisher keeps
// the strongest if it ever overflows.
#ifndef PRISM_CRADLE_SLOTS
#define PRISM_CRADLE_SLOTS 4
#endif

// The facing gate (header, item 7). dot(wedgeNormal, directionToHull) at which a wedge starts
// to participate (LO) and is fully in (HI). HI at 0 puts every wedge that is at least edge-on
// to the hull fully in the cradle; LO at −0.5 keeps the rotation axis conditioned.
#ifndef PRISM_CRADLE_FACING_LO
#define PRISM_CRADLE_FACING_LO -0.5
#endif
#ifndef PRISM_CRADLE_FACING_HI
#define PRISM_CRADLE_FACING_HI 0.0
#endif

// The stray power (header, item 4): how hard a wedge that is near the minimum but adjacent
// to nothing near it is held back. Its weight is near^(1 + STRAY). 0 lets every wedge within
// the spread come as far as a neighbour would; larger keeps a clear nearest wedge's non-
// neighbours still while a near-tie still hands off without a snap.
#ifndef PRISM_CRADLE_STRAY_POWER
#define PRISM_CRADLE_STRAY_POWER 4.0
#endif

float4 _PrismCradleCentre[PRISM_CRADLE_SLOTS];  // xyz world centre, w hull radius
float4 _PrismCradleWeight[PRISM_CRADLE_SLOTS];  // x strength 0..1
float4 _PrismCradleParams;                      // (outerRange, innerRange, liveSlotCount, neighbourSpread)

// Rodrigues' rotation of v about a UNIT axis.
float3 PrismCradleRotate(float3 v, float3 axis, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return v * c + cross(axis, v) * s + axis * (dot(axis, v) * (1.0 - c));
}

// The world-space centroid of wedge (face axis fa, tangent axis tb), given the model columns
// already scaled by the face half-extent (header, items 2-3).
float3 PrismCradleWedgeCentroid(float3 origin, float3 fa, float3 tb)
{
    return origin + fa + tb * (2.0 / 3.0);
}

// near(X): how nearly as close as the nearest wedge X is (header, item 3). 1 at the minimum,
// exactly 0 once X is `spread` farther than it.
float PrismCradleNear(float d, float dMin, float spread)
{
    return 1.0 - smoothstep(0.0, spread, d - dMin);
}

// Position, Normal and Tangent are OBJECT space. Position and Normal arrive at the END of the
// prism vertex chain (after grow, shield morph, jiggle, flight and suction); Tangent is the
// mesh's own (a Tangent Vector node, object space) — it only names the wedge, so it does not
// need to have been through the chain. Outputs are object space too — the graph's
// VertexDescription blocks take object space, and the model matrix is applied after this.
void PrismCradleDeform_float(float3 Position, float3 Normal, float3 Tangent,
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

    float outer = _PrismCradleParams.x;
    float inner = _PrismCradleParams.y;
    float spread = max(_PrismCradleParams.w, 1e-3);
    if (!(outer > inner) || !(outer > 0.0))
        return;                                   // unpublished / insane band: off

    // A mesh with no normals (or a degenerate vertex) has no face to move. Negated finite
    // test so NaN falls into the reset branch, the idiom the clock functions use.
    float nLenSq = dot(Normal, Normal);
    if (!(nLenSq > 1e-8))
        return;
    float3 nObj = Normal * rsqrt(nLenSq);

    // The wedge's tangent, made exactly perpendicular to the (possibly jiggled) normal. A mesh
    // with no usable tangent gets the face foot as its pivot and no neighbourhood.
    float3 tObj = Tangent - nObj * dot(nObj, Tangent);
    float tLenSq = dot(tObj, tObj);
    bool hasWedge = tLenSq > 1e-6;
    tObj = hasWedge ? tObj * rsqrt(tLenSq) : float3(0.0, 0.0, 0.0);
    float3 bObj = cross(nObj, tObj);

    // The face half-extent (header, item 2): identical for every vertex of the face.
    float h = abs(dot(nObj, Position));
    if (!(h > 1e-6))
        return;                                   // a face through the origin has no wedge

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

    // The model's origin and its three columns scaled by h: every wedge centroid on the prism
    // is origin ± col_a ± ⅔ col_b (header, item 3).
    float3 origin = mul(M, float4(0.0, 0.0, 0.0, 1.0)).xyz;
    float3 col[3];
    col[0] = mul(M, float4(h, 0.0, 0.0, 0.0)).xyz;
    col[1] = mul(M, float4(0.0, h, 0.0, 0.0)).xyz;
    col[2] = mul(M, float4(0.0, 0.0, h, 0.0)).xyz;

    // This vertex's own wedge and its three neighbours, in world space (header, items 1, 4).
    float3 nH = mul(M, float4(nObj * h, 0.0)).xyz;
    float3 tH = mul(M, float4(tObj * h, 0.0)).xyz;
    float3 bH = mul(M, float4(bObj * h, 0.0)).xyz;
    float3 cW = PrismCradleWedgeCentroid(origin, nH, tH);          // (n,  t)
    float3 cN1 = PrismCradleWedgeCentroid(origin, nH, bH);         // (n,  b)   beside, same face
    float3 cN2 = PrismCradleWedgeCentroid(origin, nH, -bH);        // (n, -b)   beside, same face
    float3 cN3 = PrismCradleWedgeCentroid(origin, tH, nH);         // (t,  n)   across the edge

    // The nearest riding hull, by this wedge's centroid distance. Nearest rather than summed
    // so two Urchins on one ribbon each own the wedges closest to them and the hand-off between
    // them is the same seam rule as between two wedges.
    float bestD = 1e30;
    float3 U = float3(0.0, 0.0, 0.0);
    float R = 0.0;
    float S = 0.0;
    for (int i = 0; i < PRISM_CRADLE_SLOTS; i++)
    {
        if (i >= count) break;
        float4 slot = _PrismCradleCentre[i];
        float d = length(slot.xyz - cW);
        if (d < bestD)
        {
            bestD = d;
            U = slot.xyz;
            R = slot.w;
            S = _PrismCradleWeight[i].x;
        }
    }
    if (bestD >= outer)
        return;                                   // outside the band entirely

    // The band: 0 at outer, 1 at inner and closer (header, item 7).
    float band = 1.0 - smoothstep(inner, outer, bestD);

    // The nearest wedge on the WHOLE prism (header, item 3): the minimum over all 24 centroids.
    float dMin = bestD;
    [unroll]
    for (int a = 0; a < 3; a++)
    {
        [unroll]
        for (int k = 1; k <= 2; k++)
        {
            float3 fa = col[a];
            float3 tb = col[(a + k) % 3];
            dMin = min(dMin, length(PrismCradleWedgeCentroid(origin,  fa,  tb) - U));
            dMin = min(dMin, length(PrismCradleWedgeCentroid(origin,  fa, -tb) - U));
            dMin = min(dMin, length(PrismCradleWedgeCentroid(origin, -fa,  tb) - U));
            dMin = min(dMin, length(PrismCradleWedgeCentroid(origin, -fa, -tb) - U));
        }
    }

    // Adjacency (header, item 4): near(self) × the nearest-ness of self (held back as a stray)
    // or of any neighbour. pow(0, k) is 0 and pow(1, k) is 1, so the nearest wedge is still
    // exactly 1 and a wedge beyond the spread is still exactly 0.
    float nearSelf = PrismCradleNear(bestD, dMin, spread);
    float nearHood = pow(nearSelf, PRISM_CRADLE_STRAY_POWER);
    if (hasWedge)
    {
        nearHood = max(nearHood, PrismCradleNear(length(cN1 - U), dMin, spread));
        nearHood = max(nearHood, PrismCradleNear(length(cN2 - U), dMin, spread));
        nearHood = max(nearHood, PrismCradleNear(length(cN3 - U), dMin, spread));
    }
    float adjacency = nearSelf * nearHood;

    float3 toHull = U - cW;
    float dU = length(toHull);
    // Target normal: at the hull's centre. A centroid AT the centre has no direction to
    // point; keep its own (it is inside the hull and cannot be seen anyway).
    float3 nT = dU > 1e-4 ? toHull / dU : nW;

    // The facing gate (header, item 7).
    float align = dot(nW, nT);
    float facing = smoothstep(PRISM_CRADLE_FACING_LO, PRISM_CRADLE_FACING_HI, align);

    float w = band * adjacency * facing * saturate(S);
    if (!(w > 0.0))
        return;

    // Target centroid: on the hull's surface, on the line from the centroid to the centre.
    float3 cT = U - nT * R;

    // The minimal rotation taking nW onto nT, scaled by w. The facing gate guarantees the
    // antiparallel case (undefined axis) has zero weight, and a wedge that already points at
    // the hull (axis length ~0, angle ~0) needs no rotation at all.
    float3 pW = mul(M, float4(Position, 1.0)).xyz;
    float3 rel = pW - cW;
    float3 nNew = nW;
    float3 axis = cross(nW, nT);
    float sinA = length(axis);
    if (sinA > 1e-5)
    {
        axis /= sinA;
        float angle = atan2(sinA, align) * w;
        rel = PrismCradleRotate(rel, axis, angle);
        nNew = PrismCradleRotate(nW, axis, angle);
    }

    float3 pNew = lerp(cW, cT, w) + rel;

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
