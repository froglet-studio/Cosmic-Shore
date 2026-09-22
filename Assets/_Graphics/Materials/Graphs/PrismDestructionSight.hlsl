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
// IT LIGHTS WHOLE PRISMS, NOT THE PART THAT INTERSECTS. The volume test is evaluated once per PRISM
// — at the prism's own origin, read from the object matrix — not per fragment. That is not a look
// preference, it is what makes the preview honest: `AOEConicSweepQueryJob.Execute` tests
// `p.Position`, ONE point per prism, and destroys the whole prism if that point is inside. A
// per-fragment test painted the geometric intersection instead, so the sight was drawing a shape the
// blast does not actually operate on — it showed half a prism lit that the blast would remove
// entirely. Per-prism sampling also makes the zone's boundary read as a jagged prism-granular edge,
// which is exactly what the damage boundary is.
//
// It costs nothing extra: the object matrix is already resident, so this replaces an interpolated
// float3 read with three matrix element reads, and the branch becomes coherent across the WHOLE
// prism instead of only across a screen tile. PositionWS stays in the signature as the fallback
// sample point, and is still live on the graph regardless — the occlusion corridor node next door
// consumes the same Position node.
//
// One known imprecision, on DEBRIS only: a flying chunk's visual position is integrated in the
// VERTEX stage off its stamped velocity (PrismFlightClock), so its object origin is where it
// SPAWNED rather than where it currently is. A chunk therefore lights according to the prism it came
// from, which is transient, already fading, and arguably the more meaningful answer anyway. It
// applies to the UNGATED lights only — a debris fragment carries no domain, so a domain-gated
// light (see THE DOMAIN GATE below) does not reach it at all.
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
// segment distance — no texture, no extra varying, and (since the sample point is the prism's own
// origin) no branch that can diverge across a prism at all. Nothing here changes the render queue,
// the batch, or the draw call count.
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

//
// TWO CHANNELS: MINE, AND EVERYONE ELSE'S (2026-08-19).
//
// The sight used to be strictly local — a remote peer saw a Dolphin flying normally. It is now
// visible to everyone, and the two cases are deliberately DIFFERENT LOOKS carried by two
// different uniform sets:
//
//   MINE   — the five scalar/vector uniforms in this function's signature, painted with
//            PRISM_SIGHT_COLOR (the pale cool cast). There is exactly ONE of these per viewer,
//            which is why it stays a plain uniform, and its code path is unchanged: with no peer
//            sight up, this file computes bit-identically to what it computed before.
//   THEIRS — up to PRISM_LIT_PEER_SLOTS volumes in the arrays below, each carrying its
//            holder's DOMAIN colour, so a lit patch of mass says WHO is about to take it.
//
// Why hue is the right channel for the second case, when the first case exists precisely to stay
// OUT of the palette's language: the question a peer's mark answers is "whose blast is this",
// and the platform already answers every whose-is-it question with domain colour. The same
// reasoning as the Charge-5 vessel halo, which drives a marked hull to its saturated domain
// colour because brightness alone cannot separate a ship from the lit mass around it. The
// palette collision is held off by PRISM_LIT_PEER_DESATURATION: a peer's tint is pulled toward
// white before it is added, so it reads as coloured LIGHT with a domain in it rather than as the
// prism having changed team. Own sight stays hueless, so "the pale one is mine" is learnable in
// one match.
//
// COMPOSITION — YOUR OWN SIGHT WINS OUTRIGHT, and peers fill in around it.
//
// A prism your own cone covers is painted by your own cone and nothing else: same colour, same
// gain, same expression, so the instrument you are aiming with reads exactly as it did when the
// sight was local-only, in every match, whoever else is on the field. That is not a tie-break
// convenience — a targeting aid that changes hue because a rival happened to sweep across the
// same mass is an aid you cannot trust, and you already know your cone covers that prism.
//
// Peers therefore only ever mark mass your own sight is NOT marking, which also draws the more
// useful picture: your cone in its pale cast, a rival's in their domain, and the boundary between
// them where the two overlap.
//
// Between THEMSELVES peers blend rather than sum. Four Dolphins can hold the trigger at once (both
// Dolphin-only modes seat four) and their cones overlap constantly in a dense arena, so summing
// would blow the mass out to white exactly where the fight is thickest. Each contributes its tint
// at weight w = fill x strength; the result is the weight-averaged HUE at the brightness of the
// STRONGEST single contributor. Two rivals aiming at one prism therefore read as a blend of two
// domains, never as a brighter mark than either made alone — verified over the shipped file by a
// clang build (Tools/Shaders/verify_prism_sight_composition.py).
//
// THE ARRAYS. Shader Graph has no array property type, so these are declared at file scope here
// and bound with Shader.SetGlobalVectorArray — the same shape PrismOcclusionCorridor.hlsl uses
// for its live-tuning dials, and the reason this change needed no graph edit at all. They sit
// outside every CBUFFER because they are per-FRAME globals, not per-material properties;
// putting an array in UnityPerMaterial is what would break SRP batching.
//

#ifndef PRISM_DESTRUCTION_SIGHT_INCLUDED
#define PRISM_DESTRUCTION_SIGHT_INCLUDED

// Sample the volume once per PRISM (at its own origin) rather than per fragment, so a prism the
// blast would destroy lights up WHOLE. See the header note — this is the sampling the damage sweep
// itself uses. Set to 0 to go back to per-fragment intersection painting.
#ifndef PRISM_SIGHT_WHOLE_PRISM
#define PRISM_SIGHT_WHOLE_PRISM 1
#endif

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

// The light the sight adds: a pale COOL cast (H~209, S~0.55, V 1.0), desaturated enough to read as
// light rather than as a recolour. Kept a #define rather than a uniform for the same reason the
// occlusion kernel's dials are - it is a look decision, not a per-frame quantity.
//
// It was a warm amber until 2026-08-17, chosen because no palette tier owns warm. Cool is a slightly
// riskier neighbourhood — the SHIELDED tier is frosty and Jade's base face is a deep blue — so two
// things keep it clear of them: it is deliberately DESATURATED (a tier colour at this lightness is
// far more saturated), and the gain below is low enough that the prism's own tier colour still shows
// through the cast rather than being flooded by it. If a lit shielded prism ever starts reading as a
// tier change, lower the gain before touching the hue.
//
// This is the OWN-sight colour specifically. A peer's sight is tinted by its holder's domain
// instead (see the peer block below), which is what makes "the hueless one is mine" readable.
#ifndef PRISM_SIGHT_COLOR
#define PRISM_SIGHT_COLOR float3(0.45, 0.70, 1.0)
#endif

// How hard the added light drives. Lowered from 1.15 on 2026-08-17: the sight was washing prisms out
// to near-white, and lighting WHOLE prisms (see PRISM_SIGHT_WHOLE_PRISM) lights strictly more screen
// area than the old partial-intersection paint did, so the same gain would have washed out harder
// still.
#ifndef PRISM_SIGHT_GAIN
#define PRISM_SIGHT_GAIN 0.7
#endif

// -----------------------------------------------------------------------------
// PEER SIGHTS — the other pilots' cones, tinted by their domain.
// -----------------------------------------------------------------------------

// How many LIGHTS other than the viewer's own aim can be shown at once. Eight covers the widest
// roster the game ships (the ARENA cards seat up to 8 hulls) with one light each; arcade modes
// lock to one hull, so the realistic case is far smaller. PrismLit.Slots mirrors this constant —
// change both together, since the arrays below are declared at this length — and PrismLit.Flush
// keeps the STRONGEST lights if it ever overflows.
//
// The per-slot cost when a prism is OUTSIDE a light is each shape's own first test: one dot and
// two compares. That is why there is no separate bounding sphere to maintain, and why raising
// this bound is cheap.
#ifndef PRISM_LIT_PEER_SLOTS
#define PRISM_LIT_PEER_SLOTS 8
#endif

// Mirrors CosmicShore.Data.LitShape — the numeric values ARE the wire format, since PrismLit
// packs the enum member straight into _PrismLitPeerShape[i].x. Change both together.
#define PRISM_LIT_SHAPE_CONE     0
#define PRISM_LIT_SHAPE_SPHERE   1
#define PRISM_LIT_SHAPE_CYLINDER 2

// How far a peer's domain colour is pulled toward white before it is added. 0 = the raw saturated
// domain signal colour, which reads as the prism having changed team and lands squarely in the
// palette's language (Docs/PALETTE.md); 1 = hueless, at which point a peer's mark is
// indistinguishable from your own. The shipped value keeps enough hue to name the domain while
// still reading as light thrown ONTO mass rather than as mass wearing a colour.
#ifndef PRISM_LIT_PEER_DESATURATION
#define PRISM_LIT_PEER_DESATURATION 0.4
#endif

// Peers drive slightly softer than your own sight. Your cone is information you are acting on this
// second; theirs is context. Same clamp band, so a peer mark can never out-shout the mark you are
// aiming with.
#ifndef PRISM_LIT_PEER_GAIN
#define PRISM_LIT_PEER_GAIN 0.55
#endif

// -----------------------------------------------------------------------------
// THE DOMAIN GATE — a light may be restricted to ONE domain's mass.
// -----------------------------------------------------------------------------
//
// _PrismLitPeerShape[i].y carries the Domains value a light is gated to, or 0 for "light
// everything". The prism's own domain arrives as the Domain parameter, which is a PER-MATERIAL
// value: ThemeManager clones every prism material once per domain at Awake, so a prism's
// MATERIAL is its domain and _PrismLitDomain is the cheapest honest place to read it from — no
// per-instance override, no per-frame CPU, and a stolen prism carries its new domain the instant
// its material is swapped. It costs one float in UnityPerMaterial and nothing per frame.
//
// WHY THE GATE IS PER-LIGHT AND OFF BY DEFAULT. The two AIM producers exist precisely to light
// mass their owner does NOT own — "that rival is about to take YOUR trail" is the whole sentence
// the Echo Sight and the proximity fuze say. Only the PASSTHROUGH is own-domain by nature,
// because what IT says is "that blast went through here and spared this". A blanket own-domain
// rule would silently delete the other two producers, so the restriction belongs to the light,
// not to the fundamental.
//
// ZERO IS SAFE AT BOTH ENDS, and that is what makes this cheap. Domains has no zero member (Jade
// 1, Ruby 2, Blue 3, Gold 4), so an unset gate reads as "no gate" and an unset prism domain reads
// as "this thing has no domain". The second half excludes DEBRIS for free: a dying prism's
// fragments draw with the pooled debris material, which nothing paints per domain, so they read 0
// and no gated light can reach them. That is the honest answer rather than a special case —
// fragments are not mass any more, and a blast that spared the prism they came from did not spare
// THEM, it never touched them.

// Bound with Shader.SetGlobalVectorArray / SetGlobalFloat once per frame. Declared at file scope
// because Shader Graph has no array property type, and OUTSIDE every CBUFFER because these are
// per-frame globals rather than per-material properties (an array inside UnityPerMaterial is what
// breaks SRP batching). Same mechanism PrismOcclusionCorridor.hlsl uses for its tuning dials.
//
// The three geometry vectors carry the shape's three params in their w channels, so every shape
// fits the same four float4s and only the TAG is extra:
//   PeerApex[i]  = (origin.xyz, params.x)   cone: height   sphere: radius  cylinder: reach
//   PeerAxis[i]  = (axis.xyz,   params.y)   cone: tanCore  sphere: -       cylinder: radius
//   PeerGape[i]  = (gape.xyz,   params.z)   cone: tanGape  sphere: -       cylinder: mirrored
//   PeerTint[i]  = (tint.rgb,   strength)
//   PeerShape[i] = (shape, gate, -, -)      shape: PRISM_LIT_SHAPE_*; gate: a Domains value, 0 = none
//
// _PrismLitPeerCount is the master sentinel: unpublished globals read as zero (a player build
// before any producer lights anything, or the editor between play sessions), the loop below does
// not execute, and this file behaves exactly as it did when the sight was local-only.
//
// The names are historical — this file, its guid and its entry point predate the LIT fundamental
// and are deliberately unchanged, because renaming the entry point means editing every prism
// graph's m_FunctionName for no functional gain, and a Custom Function node that cannot resolve
// its function renders the material UNMATERIALED with nothing in the console.
float4 _PrismLitPeerApex[PRISM_LIT_PEER_SLOTS];
float4 _PrismLitPeerAxis[PRISM_LIT_PEER_SLOTS];
float4 _PrismLitPeerGape[PRISM_LIT_PEER_SLOTS];
float4 _PrismLitPeerTint[PRISM_LIT_PEER_SLOTS];
float4 _PrismLitPeerShape[PRISM_LIT_PEER_SLOTS];
float  _PrismLitPeerCount;

// How deep inside one blast volume a point stands, on the edge-weighted curve, or 0 if outside.
//
// This is THE containment test, factored out so the own sight and every peer sight run literally
// the same code — three transcriptions of AOEConicSweepQueryJob already exist across the codebase
// (the sweep, ExplosionHelper.Contains, the capsule trigger) and a fourth living inside a loop
// body would have been a fifth place for the shape to drift.
float PrismSightFill(float3 samplePos, float3 apex, float3 axis, float3 gape, float3 params)
{
    float height = params.x;
    if (height <= 0.0)
        return 0.0;

    float3 rel = samplePos - apex;

    // Axial band. Outside [0, height] there is no blast at all - note the near clip is at the
    // apex, so mass BEHIND the vessel is never highlighted even though the cone's axis extends
    // backwards mathematically.
    float s = dot(rel, axis);
    if (s <= 0.0 || s > height)
        return 0.0;

    float coreRadius = params.y * s;
    if (coreRadius <= 0.0)
        return 0.0;

    // Distance from the cross-section's SEGMENT, not from the axis: clamp onto the segment first
    // so the ends are round. Mirrors AOEConicSweepQueryJob exactly.
    float3 radial = rel - axis * s;
    float halfLength = params.z * s;
    float along = dot(radial, gape);
    float3 offAxis = radial - gape * clamp(along, -halfLength, halfLength);

    float d = length(offAxis);
    if (d > coreRadius)
        return 0.0;

    // Inside. Weight toward the boundary so the blast's silhouette is drawn onto the mass rather
    // than flooding it, with a floor so deep mass still reads as marked.
    float edge = saturate(d / coreRadius);
    return lerp(PRISM_SIGHT_CORE_FILL, 1.0, pow(edge, PRISM_SIGHT_EDGE_POWER));
}

// The SPHERE arm — an ordinary AOE blast, and the Sparrow warhead's proximity fuze. Mirrors
// AOESpatialQueryJob: one dot against the squared radius, and the root is taken only on a hit.
float PrismLitFillSphere(float3 rel, float radius)
{
    if (radius <= 0.0)
        return 0.0;

    float dSq = dot(rel, rel);
    float rSq = radius * radius;
    if (dSq > rSq)
        return 0.0;

    float edge = saturate(sqrt(dSq) / radius);
    return lerp(PRISM_SIGHT_CORE_FILL, 1.0, pow(edge, PRISM_SIGHT_EDGE_POWER));
}

// The CYLINDER arm — the Scarab's cavitation plate: swept along its own face normal, flat end
// caps, radius CONSTANT along the sweep. Mirrors AOECylinderSweepQueryJob, including the mirror:
// a non-zero `mirrored` tests |axial|, so one volume claims the slabs either side of the start
// plane together. Note the radial arm is taken from the SIGNED projection, never the mirrored
// one, or a reflected point's distance to the axis is measured from the wrong side.
float PrismLitFillCylinder(float3 rel, float3 axis, float reach, float radius, float mirrored)
{
    if (radius <= 0.0)
        return 0.0;

    float s = dot(rel, axis);
    float axial = (mirrored > 0.0) ? abs(s) : s;
    if (axial < 0.0 || axial > reach)
        return 0.0;

    float3 radial = rel - axis * s;
    float d = length(radial);
    if (d > radius)
        return 0.0;

    float edge = saturate(d / radius);
    return lerp(PRISM_SIGHT_CORE_FILL, 1.0, pow(edge, PRISM_SIGHT_EDGE_POWER));
}

// Dispatch on the shape tag. The CONE arm still calls PrismSightFill above, unchanged and
// uninlined here, so the viewer's own aim and every cone light are bit-identical to what this
// file computed before the bank carried shapes at all — which is what keeps
// Tools/Shaders/verify_prism_sight_composition.py meaningful as a regression proof rather than
// merely a fresh measurement.
//
// The branch is UNIFORM across a wave: every prism in a draw reads the same bank, so the shape
// switch costs a scalar compare and diverges never.
float PrismLitFill(float3 samplePos, float3 origin, float3 axis, float3 gape, float3 params,
                   float shape)
{
    if (params.x <= 0.0)
        return 0.0;

    if (shape >= PRISM_LIT_SHAPE_CYLINDER)
        return PrismLitFillCylinder(samplePos - origin, axis, params.x, params.y, params.z);

    if (shape >= PRISM_LIT_SHAPE_SPHERE)
        return PrismLitFillSphere(samplePos - origin, params.x);

    return PrismSightFill(samplePos, origin, axis, gape, params);
}

void PrismDestructionSight_float(
    float3 PositionWS,
    float3 Apex,        // OWN sight: blast apex, world space
    float3 Axis,        // OWN sight: sweep axis (unit)
    float3 Gape,        // OWN sight: gape axis (unit, perpendicular to Axis)
    float3 Params,      // OWN sight: (height, core radius per unit depth, half-length per unit depth)
    float  Strength,    // OWN sight: highlight fade, 0-1
    float3 BaseColor,
    float  Domain,      // THIS PRISM's domain (a Domains value; 0 = it has none). Per-material.
    out float3 Color)
{
    // Composes rather than overwrites: a fragment outside every volume, with no sight held
    // anywhere, leaves with exactly the colour the graph gave it.
    Color = BaseColor;

    // Both sentinels: an unheld own trigger with nobody else holding one costs exactly these two
    // compares and nothing else.
    int peerCount = min((int)_PrismLitPeerCount, PRISM_LIT_PEER_SLOTS);
    if ((Params.x <= 0.0 || Strength <= 0.0) && peerCount <= 0)
        return;

    // ONE sample point for the whole prism: its own origin, which is the exact point
    // AOEConicSweepQueryJob tests. The preview and the damage therefore select the same prisms by
    // construction, and a prism is lit all-or-nothing just as it is destroyed all-or-nothing.
    // Mirrors the object-origin idiom PrismClockAnimation.hlsl already uses, preview guard included.
#if PRISM_SIGHT_WHOLE_PRISM && !defined(SHADERGRAPH_PREVIEW)
    float3 samplePos = float3(GetObjectToWorldMatrix()._m03,
                              GetObjectToWorldMatrix()._m13,
                              GetObjectToWorldMatrix()._m23);
#else
    float3 samplePos = PositionWS;
#endif

    // ---- YOUR OWN SIGHT, first and exclusive ----
    // Deliberately the whole of the old function, expression for expression: a prism your cone
    // covers must be painted the same way it was before peers existed, with no division and no
    // averaging that could move it by even a bit. It also short-circuits the loop below in the
    // one case that runs every frame you are holding the trigger.
    if (Params.x > 0.0 && Strength > 0.0)
    {
        float own = PrismSightFill(samplePos, Apex, Axis, Gape, Params) * Strength;
        if (own > 0.0)
        {
            Color = BaseColor + PRISM_SIGHT_COLOR * (own * PRISM_SIGHT_GAIN);
            return;
        }
    }

    // ---- EVERYONE ELSE, blended among themselves ----
    // `weighted` accumulates tint x weight and `peak` tracks the strongest single weight, so the
    // result is the weight-averaged HUE carried at ONE contributor's brightness. Adding a second
    // rival to a prism changes its colour, never how brightly it is lit.
    float3 weighted = float3(0.0, 0.0, 0.0);
    float  total    = 0.0;
    float  peak     = 0.0;

    // [loop] rather than an unroll: the bound is a uniform, and at peerCount 0 — every frame in
    // which no rival is aiming — this costs one compare instead of four dead iterations.
    [loop]
    for (int i = 0; i < peerCount; i++)
    {
        float4 apex = _PrismLitPeerApex[i];
        float4 axis = _PrismLitPeerAxis[i];
        float4 gape = _PrismLitPeerGape[i];
        float4 tint = _PrismLitPeerTint[i];
        float4 tag  = _PrismLitPeerShape[i];
        float  shape = tag.x;

        // The domain gate, tested BEFORE any geometry: a light restricted to one domain's mass
        // rejects a foreign prism (and every fragment, which carries no domain at all) for one
        // compare. Both sides are small integers held exactly in float, so this is an exact test
        // rather than a tolerance, and it is uniform across the whole prism — Domain is a material
        // constant and the slot is a global, so the branch can never diverge.
        if (tag.y > 0.0 && tag.y != Domain)
            continue;

        float w = PrismLitFill(samplePos, apex.xyz, axis.xyz, gape.xyz,
                               float3(apex.w, axis.w, gape.w), shape) * tint.a;
        if (w <= 0.0)
            continue;

        // Pulled toward white so a rival's mark reads as coloured light on the mass rather than as
        // the mass having changed domain.
        float3 peerColor = lerp(tint.rgb, float3(1.0, 1.0, 1.0), PRISM_LIT_PEER_DESATURATION);
        weighted += peerColor * w;
        total    += w;
        peak      = max(peak, w);
    }

    if (total <= 0.0)
        return;

    Color = BaseColor + (weighted / total) * (peak * PRISM_LIT_PEER_GAIN);
}

#endif // PRISM_DESTRUCTION_SIGHT_INCLUDED
