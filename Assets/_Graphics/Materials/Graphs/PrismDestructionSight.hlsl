// PrismDestructionSight.hlsl — the GPU side of the Dolphin's Echo Sight
// (Docs/PRISM_ANIMATION.md §4.7, the "global uniform" shape for a view-dependent prism visual).
//
// PURPOSE. While the pilot holds the sight, every prism standing inside the volume the next
// crystal blast would sweep lights up, so the gape they have been banking with every skim stops
// being an abstract angle on the HUD and becomes the actual mass it is about to remove.
//
// WHY IT LIVES HERE AND NOT ON THE CPU. "Is this prism inside the blast" is live, per-frame,
// per-prism data: the answer changes as the ship turns and as the energy meter fills. It can
// therefore never be a per-prism stamp, and running the spatial index's conic sweep every frame
// purely to tint would be exactly the per-prism CPU pass the clock-material law exists to
// prevent. The law's sanctioned shape for this case is a GLOBAL uniform — one O(1) write per
// frame that every prism reads, with zero per-prism CPU work, zero material swaps and zero
// per-instance overrides. Same contract as PrismOcclusionCorridor.hlsl, which is its sibling.
//
// THE UNIFORMS (published by PrismDestructionSight.cs once per frame):
//   float3 _PrismSightApex     — the blast's apex in world space.
//   float3 _PrismSightAxis     — the sweep axis (unit).
//   float3 _PrismSightGape     — the gape axis (unit, perpendicular to the sweep axis).
//   float3 _PrismSightParams   — (height, coreRadiusPerUnitDepth, halfLengthPerUnitDepth).
//                                height <= 0 means "sight off", which the very first branch
//                                below returns untouched.
//   float  _PrismSightStrength — highlight fade, 0-1, so the sight never pops on or off.
//
// All four vectors are Vector3 rather than Vector4 because that is what the prism graphs can
// clone a donor for exactly — packing the scalars into w channels would have meant synthesising
// a property type neither graph contains, which is precisely the kind of hand-authored schema the
// asset-surgery protocol says not to invent.
//
// THE VOLUME. Not a circular cone: the blast opens the way the jaws open. At axial depth s the
// cross-section is a 2D STADIUM — a disc of radius (_PrismSightParams.y · s) dragged along the
// gape axis for ±(_PrismSightParams.z · s). So it is narrow across the beam at every charge and
// wide across the gape in proportion to the energy banked. This is a literal transcription of
// AOEConicSweepQueryJob.Execute (PrismSpatialIndex.cs): clamp onto the cross-section's segment
// first, then measure distance to that point, which is what makes the ends round and is the same
// point-to-segment distance the CapsuleCollider trigger uses. The preview and the damage volume
// are the same shape BY CONSTRUCTION rather than by two authors agreeing.
//
// COST CONTRACT. A fragment with the sight off executes one compare (_PrismSightParams.x > 0) and
// returns. With the sight on it costs one dot for the axial band, one reject, then ~12 ALU for the
// segment distance — no texture, no extra varying beyond world position, no branch that diverges
// across a prism (the whole prism is on one side of the test at typical prism sizes, and near the
// boundary the branch is still coherent across a screen tile). Nothing here changes the render
// queue, the batch, or the draw call count.
//
// WHY IT ADDS RATHER THAN TINTS. The highlight has to read against every prism tier and both
// domains without being mistaken for one of them. REPLACING colour on a Jade prism lands in the
// same space as the domain palette and says "this prism changed team"; ADDING light says "this one
// is lit up", which is not a thing any tier's palette means, so the sight can never be confused
// with mass state (Docs/PALETTE.md - the tier colours are the language, do not borrow their space).
// The prism graphs are UNLIT and carry no Emission block, so on them additive-into-BaseColor IS
// emission - which is also why this splices exactly like PrismOcclusionFade does: it takes the
// graph's own colour in and hands the final colour back, so it composes instead of overwriting.
//
// It composes with the occlusion corridor for free: the corridor dissolves COVERAGE, not colour,
// so a highlighted prism standing in the corridor thins out exactly like its neighbours instead of
// punching through the ship.

#ifndef PRISM_DESTRUCTION_SIGHT_INCLUDED
#define PRISM_DESTRUCTION_SIGHT_INCLUDED

// How much of the emission is a flat fill vs. an edge-weighted rim. A pure flat fill turns the
// zone into a slab of solid colour and hides which prisms are which; weighting toward the volume's
// BOUNDARY draws the blast's silhouette onto the mass instead.
#ifndef PRISM_SIGHT_EDGE_POWER
#define PRISM_SIGHT_EDGE_POWER 2.0
#endif

// Floor on the fill so mass deep inside the zone is still obviously marked, not just its rim.
#ifndef PRISM_SIGHT_CORE_FILL
#define PRISM_SIGHT_CORE_FILL 0.35
#endif

// The light the sight adds. Deliberately NOT a domain or tier colour (see the note above): a warm
// white-hot cast that no palette tier owns, so "lit by the sight" can never be misread as "this
// mass is shielded / danger / another team". Kept a #define rather than a uniform for the same
// reason the occlusion kernel's dials are - it is a look decision, not a per-frame quantity.
#ifndef PRISM_SIGHT_COLOR
#define PRISM_SIGHT_COLOR float3(1.0, 0.72, 0.34)
#endif

// How hard the added light drives. 1.0 roughly doubles a mid-tone prism at full fill.
#ifndef PRISM_SIGHT_GAIN
#define PRISM_SIGHT_GAIN 1.15
#endif

void PrismDestructionSight_float(
    float3 PositionWS,
    float3 Apex,        // blast apex, world space
    float3 Axis,        // sweep axis (unit)
    float3 Gape,        // gape axis (unit, perpendicular to Axis)
    float3 Params,      // (height, core radius per unit depth, half-length per unit depth)
    float  Strength,
    float3 BaseColor,
    out float3 Color)
{
    // Composes rather than overwrites: a fragment outside the volume, or with the sight released,
    // leaves with exactly the colour the graph gave it.
    Color = BaseColor;

    float Highlight = 0.0;

    // Sentinel: the publisher zeroes every uniform when the sight is released, so an unheld
    // trigger costs exactly this compare.
    float height = Params.x;
    if (height <= 0.0 || Strength <= 0.0)
        return;

    float3 rel = PositionWS - Apex;

    // Axial band. Outside [0, height] there is no blast at all - note the near clip is at the
    // apex, so mass BEHIND the vessel is never highlighted even though the cone's axis extends
    // backwards mathematically.
    float s = dot(rel, Axis);
    if (s <= 0.0 || s > height)
        return;

    // Distance from the cross-section's SEGMENT, not from the axis: clamp onto the segment first
    // so the ends are round. Mirrors AOEConicSweepQueryJob exactly.
    float3 radial = rel - Axis * s;
    float halfLength = Params.z * s;
    float along = dot(radial, Gape);
    float3 offAxis = radial - Gape * clamp(along, -halfLength, halfLength);

    float coreRadius = Params.y * s;
    if (coreRadius <= 0.0)
        return;

    float d = length(offAxis);
    if (d > coreRadius)
        return;

    // Inside. Weight toward the boundary so the blast's silhouette is drawn onto the mass rather
    // than flooding it, with a floor so deep mass still reads as marked.
    float edge = saturate(d / coreRadius);
    float fill = lerp(PRISM_SIGHT_CORE_FILL, 1.0, pow(edge, PRISM_SIGHT_EDGE_POWER));

    Highlight = fill * Strength;

    Color = BaseColor + PRISM_SIGHT_COLOR * (Highlight * PRISM_SIGHT_GAIN);
}

#endif // PRISM_DESTRUCTION_SIGHT_INCLUDED
