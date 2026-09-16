// PrismCradle.hlsl — the GPU side of the Urchin's CRADLE
// (Docs/PRISM_ANIMATION.md §4.7.2, the THIRD citizen of §4.7's "global uniform" shape for a
// prism visual that depends on live gameplay data).
//
// PURPOSE. While an Urchin is RIDING a prismscape — attached, not launched, not in free flight
// — the mass around it wraps itself onto the hull. Every prism FACE inside the cradle band
// swings so that its normal points at the hull's centre and its centroid sits ON the hull's
// surface, on the line from where the face was to that centre; faces farther out do the same
// thing by a smaller amount, so as the hull rolls from one face to the next the two faces
// meet at the seam, both touching the sphere, and one hands off to the other with nothing
// snapping. The pilot reads it as being CRADLED by the mass they are grinding.
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
//   float4 _PrismCradleParams    — (outerRange, innerRange, liveSlotCount, 0). The band:
//                                  zero effect at a face-centroid distance of outerRange
//                                  (15 u), full at innerRange (10 u) and closer. liveSlotCount
//                                  is the MASTER SENTINEL: an unpublished global reads as zero,
//                                  and zero must mean "the loop does not execute" — the same
//                                  rule PrismDestructionSight's peer bank records.
//
// The arrays are declared at FILE SCOPE here, not as graph properties (Shader Graph has no
// array property type — which is also why wiring this needed no property surgery on either
// graph), and OUTSIDE every CBUFFER: they are per-FRAME globals, and an array inside
// UnityPerMaterial is what breaks SRP batching.
//
// THE GEOMETRY, per FACE, in WORLD space (the prism's scale is non-uniform — a trail slab is
// long and thin — and a rigid motion is only rigid in a frame where the metric is isotropic;
// PrismJiggleClock reaches the same conclusion from the other direction):
//
//   1. The face this vertex belongs to. Prism meshes are HARD-EDGED (the built-in cube every
//      trail prism draws is 24 verts / 6 normals; the shield meshes split per face), so the
//      object-space NORMAL *is* the face id — every vertex of a face carries the same one.
//   2. The face CENTROID. A vertex shader cannot see its neighbours, but for a face plane that
//      passes at distance k = dot(n, v) from the object origin, the FOOT of the origin on that
//      plane, n·k, is the same point for all of the face's vertices — and for a box it IS the
//      face centroid, exactly (the shield octahedron's too, by symmetry). It survives every
//      earlier stage of the vertex chain, because each of them (grow scale, jiggle, the shatter
//      spin, the suction lerp) moves a face rigidly or by a per-axis scale, both of which keep
//      dot(n, v) constant across the face. So no mesh channel is needed and no bake.
//   3. The TARGET. With U the hull centre and R its radius: the face's new normal is
//      nT = normalize(U − c) (pointing at the hull), its new centroid is cT = U − nT·R (on the
//      hull's surface, on the line from c to U — "the centroid moves along the line connecting
//      its old position to the vessel position, and stops at the surface").
//   4. The MOTION is the minimal rotation taking the face normal onto nT, applied about the
//      centroid, plus the translation c → cT, BOTH scaled by the weight w. Blending the rigid
//      motion (angle × w, translation × w) rather than the vertex positions keeps the face rigid
//      at EVERY intermediate weight, so a half-cradled face is a whole face half-way there,
//      never a shrunken one. The two triangles of a box face share the normal and the centroid,
//      so they move as one — "the closest triangle" is the closest FACE, which is what a box's
//      diagonally-split faces make of the request.
//   5. The WEIGHT is band × facing × strength. Band is the 15 → 10 u ramp, measured at the face
//      centroid so adjacent faces read slightly different weights and the seam is a gradient.
//      FACING is the one term the request did not name and the geometry demands: a face whose
//      normal points AWAY from the hull is on the prism's far side. Wrapping it means a 180°
//      flip about an axis the cross product cannot define — and the Urchin rides the
//      CENTRELINE of a trail, so the far face's normal line passes through the hull constantly
//      and the flip's axis would swing through every direction as it does, spinning the face.
//      The gate fades such faces out between dot(n, nT) = −0.5 and 0, at which point the
//      rotation axis is always well conditioned (|sin| ≥ 0.87 wherever the weight is nonzero).
//      Nothing is lost on screen: a face pointing away from the hull is behind the faces that
//      point toward it. Faces at 90° (the side faces of the prism the hull sits on, the faces of
//      the neighbours it is rolling toward) are ABOVE the gate and wrap — that is the cradle.
//
// COST CONTRACT. A vertex with no cradle live executes one integer compare and returns. With
// one live it costs: two matrix transforms, one loop iteration per live slot (≤ 4), the
// smoothsteps, and — only inside the band — two Rodrigues rotations and two more transforms.
// It is VERTEX work on a 24-vertex mesh; no fragment cost, no extra varying, no texture, no
// batch split, no material swap, no draw call. Everything stays in the same instanced batch.
//
// KNOWN IMPRECISION. The displacement is a vertex-stage effect, and Entities Graphics culls by
// the prism's RenderBounds, which this does not expand (a stamp could; a per-frame global
// cannot). A face can move up to outerRange from its prism, so a prism whose bounds are just
// off-screen can carry a wrapped face that should be on-screen and is culled with the prism.
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

// The facing gate (header, item 5). dot(faceNormal, directionToHull) at which a face starts
// to participate (LO) and is fully in (HI). HI at 0 puts every face that is at least edge-on
// to the hull fully in the cradle; LO at −0.5 keeps the rotation axis conditioned.
#ifndef PRISM_CRADLE_FACING_LO
#define PRISM_CRADLE_FACING_LO -0.5
#endif
#ifndef PRISM_CRADLE_FACING_HI
#define PRISM_CRADLE_FACING_HI 0.0
#endif

float4 _PrismCradleCentre[PRISM_CRADLE_SLOTS];  // xyz world centre, w hull radius
float4 _PrismCradleWeight[PRISM_CRADLE_SLOTS];  // x strength 0..1
float4 _PrismCradleParams;                      // (outerRange, innerRange, liveSlotCount, 0)

// Rodrigues' rotation of v about a UNIT axis.
float3 PrismCradleRotate(float3 v, float3 axis, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return v * c + cross(axis, v) * s + axis * (dot(axis, v) * (1.0 - c));
}

// Position and Normal are OBJECT space, at the END of the prism vertex chain (after grow,
// shield morph, jiggle, flight and suction). Outputs are object space too — the graph's
// VertexDescription blocks take object space, and the model matrix is applied after this.
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

    float outer = _PrismCradleParams.x;
    float inner = _PrismCradleParams.y;
    if (!(outer > inner) || !(outer > 0.0))
        return;                                   // unpublished / insane band: off

    // A mesh with no normals (or a degenerate vertex) has no face to move. Negated finite
    // test so NaN falls into the reset branch, the idiom the clock functions use.
    float nLenSq = dot(Normal, Normal);
    if (!(nLenSq > 1e-8))
        return;
    float3 nObj = Normal * rsqrt(nLenSq);

    // The face centroid proxy (header, item 2): the foot of the object origin on the face
    // plane. Identical for every vertex of the face, so the motion below is rigid per face.
    float3 cObj = nObj * dot(nObj, Position);

    float4x4 M = GetObjectToWorldMatrix();
    float4x4 Minv = GetWorldToObjectMatrix();

    float3 cW = mul(M, float4(cObj, 1.0)).xyz;
    // A normal transforms by the inverse transpose: row-vector × inverse model.
    float3 nW = mul(nObj, (float3x3)Minv);
    float nwLenSq = dot(nW, nW);
    // A prism pulled fresh from the pool sits at localScale ZERO until its creation completes;
    // its model matrix is degenerate and the inverse blows up. The entity is not rendered in
    // that window; the guard is so that "is not" is not load-bearing.
    if (!(nwLenSq > 1e-12) || !(nwLenSq < 1e12))
        return;
    nW *= rsqrt(nwLenSq);

    // The nearest riding hull, by face-centroid distance. Nearest rather than summed so two
    // Urchins on one ribbon each own the faces closest to them and the hand-off between
    // them is the same seam rule as between two faces.
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

    // The band: 0 at outer, 1 at inner and closer (header, item 5).
    float band = 1.0 - smoothstep(inner, outer, bestD);

    float3 toHull = U - cW;
    float dU = length(toHull);
    // Target normal: at the hull's centre. A face centroid AT the centre has no direction to
    // point; keep its own (it is inside the hull and cannot be seen anyway).
    float3 nT = dU > 1e-4 ? toHull / dU : nW;

    // The facing gate (header, item 5).
    float align = dot(nW, nT);
    float facing = smoothstep(PRISM_CRADLE_FACING_LO, PRISM_CRADLE_FACING_HI, align);

    float w = band * facing * saturate(S);
    if (!(w > 0.0))
        return;

    // Target centroid: on the hull's surface, on the line from the centroid to the centre.
    float3 cT = U - nT * R;

    // The minimal rotation taking nW onto nT, scaled by w. The facing gate guarantees the
    // antiparallel case (undefined axis) has zero weight, and a face that already points at
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
