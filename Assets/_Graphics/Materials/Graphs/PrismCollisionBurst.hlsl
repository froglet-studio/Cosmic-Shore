// PrismCollisionBurst.hlsl — the GPU side of the prism's DETAIL LEVELS for its two fundamental
// interactions (Docs/PRISM_ANIMATION.md §4.11, Docs/PRISM_MOTION.md § "The look"): how a prism is
// DESTROYED and how it is STOLEN, drawn on the ultra-high-res prism the Rhino's slice uses whenever the
// viewer is close enough to see the detail and the budget allows, and as the ordinary effect otherwise.
// (The file keeps the name of its first use, the prism-movement collision.)
//
//   DESTRUCTION is the Sierpinski cut of the explosion (PrismSierpinskiTiling): depth 0 IS the ordinary
//   explosion's 24 wedges, and each deeper depth splits the corner triangles at their midpoints. Every
//   fragment of every depth moves by the explosion's OWN tuned law (PrismBurstWedgePose, kind WEDGE) —
//   RotateFacesAlongAxis' closed form, applied to the fragment as to a scaled wedge: the same velocity
//   response, the same spin axes and rate (amount × _ExplosiveRotation, amount = the explosion's clamped
//   speed × t), the same outward slide (amount × _ExplosiveSpead), the same flight, and the explosion's
//   own erosion fade — so at depth 0 it is the explosion exactly, and at every depth it moves like it.
//
//   STEALING is the GRADE far away — the 0.8 s smoothstep colour lerp PrismColorLerp runs on the prism —
//   and a SHED close up: the prism takes its new colour at once and a skin wearing the OLD colours comes
//   off it, its inside already the new colour. The game sheds as a BLOOM (the designer, 2026-10-15):
//   petals cut into rings from each edge inward and segments along it, so every piece is short — it
//   curls tight and stays close to the surface — and each peels with its own noise. The skin's coverage falls on the grade's own smoothstep over the grade's own length
//   (PrismBurstProgress, curve 1), so the mean colour, squinted at, IS the grade.
//
// No white accents (the designer, 2026-10-14): the only contrast is the brighter INSIDE colour on the
// back faces once the surface opens.
//
// WHY A STAMP AND NOT A GLOBAL (the prism-morph admission test, question 1). Everything is known at
// the instant of the event — the pose, the velocity, the kind, the colours — so this is §1's ordinary
// shape: one stamp per prism, the GPU runs the course off _PrismClock, one scheduled retirement. No
// per-frame CPU write to any burst, ever. It rides the slice's batch spawner and stamp channels
// (PrismCollisionBurst.cs), born ON the dense mesh, so there is no residency swap and nothing to hide.
//
// WHY THE DENSE MESH, AND WHY UNWELDED. Each burst draws HighPolyPrismMesh.GetUnwelded — the identical
// solid at subdivision 10 (1,200 triangles against the prism's 24) with every triangle owning its own
// three vertices. Unwelded is what lets the surface TEAR along any pattern the kind chooses (shard
// borders, petal diagonals, ribbon seams, scale edges) with no stretched triangle across the tear;
// dense is what makes those tears and the curls smooth. TEXCOORD2 gives each vertex its triangle's
// centroid in face coordinates plus a per-triangle random, so every vertex of a triangle makes the SAME
// choice of piece — a triangle never straddles two pieces.
//
// THE KINDS (_SliceTiming.w; chosen on the CPU, PrismInteractionLod):
//   0 SHATTER — destruction, mid range. Each face fractures into Voronoi shards (PrismBurstVoronoi over
//     the triangle centroids), each a RIGID piece that leaves along its face normal and tumbles about
//     its own seed point.
//   1 BLOOM   — shed. Each face splits along its diagonals into four quarters (the explosion's wedge
//     layout), each quarter into RINGS from its cube edge inward and SEGMENTS along it; every piece CURLS
//     up from its outer edge — an exact isometric bend (PrismBurstBend), so it rolls without stretching —
//     the rings peeling from the edge inward, each piece with its own delay, curl and lift (the noise).
//   2 UNRAVEL — shed. Each face peels into ribbons along its longer axis, each ribbon curling up from
//     one end into a near-loop, staggered ribbon by ribbon.
//   3 GLITTER — destruction, close up. Every triangle is its own shard, spinning out and blinking away.
//   4 FLAKE   — shed. The skin lifts off as rigid SCALES, a grid twice as fine as the shatter's, in a
//     wave that runs the prism's long axis: each scale tips up on its far edge as the front nears and
//     is gone when the front reaches it.
//   5 WEDGE   — destruction. Each triangle of the Sierpinski cut turns about its root wedge's two axes (the
//     wedge's tangent and cross(velocity, N)) and slides with it, by the explosion's law (PrismBurstWedgePose);
//     fades on the explosion's erosion (the shader's clip).
//
// EXACTNESS. Rigid pieces rotate their normal with them (Rodrigues). A bend's normal is the bend's
// derivative: the strip at arc length s is rotated by a = κ·s about the hinge, so its normal is that
// same rotation of the rest normal — exact, not a shortcut (the cradle's cheap-normal lesson). The
// harness (Tools/Shaders/verify_prism_collision_burst.py) executes this file and proves: the first
// frame IS the prism, rigid pieces stay rigid, bends are isometric with the normal as the derivative,
// every piece stays inside the envelope the CPU bounds the entity by, destruction coverage is linear in
// age, a shed's progress IS the shipped grade's (PrismColorLerp, executed), and both are gone before
// retirement. It cannot prove a burst LOOKS good; that is the playtest's and the lab's
// (Docs/Studios/COLLISION_BURST_LAB.md).

#ifndef PRISM_COLLISION_BURST_INCLUDED
#define PRISM_COLLISION_BURST_INCLUDED

#define PRISM_BURST_SHATTER 0.0
#define PRISM_BURST_BLOOM   1.0
#define PRISM_BURST_UNRAVEL 2.0
#define PRISM_BURST_GLITTER 3.0
#define PRISM_BURST_FLAKE   4.0
#define PRISM_BURST_WEDGE   5.0

// RotateFacesAlongAxis / TangentSlider's constants (the explosion's subgraphs; the lab's build.py reads
// them from the graph and asserts these): the pivot bias, the slide bias and the slider gain.
#ifndef PRISM_BURST_PIVOT_BIAS
#define PRISM_BURST_PIVOT_BIAS (-0.5)
#endif
#define PRISM_BURST_SLIDE_BIAS 0.5
#define PRISM_BURST_SLIDER_GAIN 0.5

#define PRISM_BURST_TAU 6.28318530718

// How far past 1 the dissolve's progress runs at retirement. Progress reaches 1 — every fragment
// gone — at life / (1 + overshoot), so the retirement never beats the fade (continuity of
// existence). The harness's negative control sets it to 0 and watches fragments survive to the end.
#ifndef PRISM_BURST_DISSOLVE_OVERSHOOT
#define PRISM_BURST_DISSOLVE_OVERSHOOT 0.06
#endif

// 1 (shipped): a fragment's place in the fade is frac(piece + grain·noise), which is UNIFORM on
// [0, 1) whatever the noise does — so coverage falls exactly linearly, the explosion's own opacity
// curve, which is what keeps the squint test. 0: a plain lerp, whose triangular distribution sags
// the middle of the fade — the harness's second negative control.
#ifndef PRISM_BURST_UNIFORM_VALUE
#define PRISM_BURST_UNIFORM_VALUE 1
#endif

// 1 (shipped): a bent strip's normal is turned with it — the bend's exact derivative. 0: the rest
// normal, unturned — the cheap shortcut the cradle rejected on look, and the harness's negative
// control for the normal test.
#ifndef PRISM_BURST_BEND_NORMAL
#define PRISM_BURST_BEND_NORMAL 1
#endif

// 1 (shipped): a SHED's progress is the grade's smoothstep (PrismColorLerp), so its mean colour is the
// grade's. 0: linear like the destruction — the harness's negative control for the grade test.
#ifndef PRISM_BURST_GRADE_SMOOTH
#define PRISM_BURST_GRADE_SMOOTH 1
#endif

// The bloom's petal tie-break, in face coordinates (see PrismBurstPose).
#define PRISM_BURST_PETAL_TIE 1e-4

// Upper bound on Voronoi cells per face edge — the loops below are bounded by it. The flake's scales
// are a grid twice as fine.
#define PRISM_BURST_MAX_CELLS 6.0

// Everything one vertex needs, all in WORLD space (the shader builds it from the object-space
// mesh and the entity matrix). World space is what keeps a piece RIGID under the prism's
// non-uniform scale: a rotation done in object space would shear a long slab's shard.
struct PrismBurstFrame
{
    float3 rest;        // the vertex's rest position
    float3 normal;      // its face's outward unit normal
    float3 centre;      // the prism's centre
    float3 faceCentre;  // its face's centre
    float3 faceU;       // face centre -> the face's +u edge (a half-extent, scaled)
    float3 faceV;       // face centre -> the face's +v edge
    float2 triUV;       // its TRIANGLE's centroid in face coordinates, [-1, 1]
    float3 faceKey;     // identity of the face (its object normal, seeded) for hashing
    float radius;       // the prism's half-diagonal
    float3 longAxis;    // the prism's longest half-axis (centre -> the middle of that end), the flake's sweep
};

struct PrismBurstParams
{
    float age;          // seconds since the collision
    float speed;        // the explosion speed the burst inherited (spin scales with it, like the wedges')
    float seed;         // per-prism random
    float spread;       // how far a piece leaves, in prism radii (≤ 1 keeps the squint)
    float tau;          // time constant of the burst's opening
    float spin;         // radians per unit speed per second
    float bloomTurns;   // a petal tip's final curl, in turns
    float unravelTurns; // a ribbon end's final curl, in turns
    float strips;       // ribbons per face
    float cells;        // shard seeds per face edge
    float progress;     // the fade's progress now (PrismBurstProgress): the flake lifts a scale ahead of it
    float rings;        // bloom: rings per quarter, from the cube edge inward
    float segments;     // bloom: segments per quarter, along the cube edge
    float noise;        // bloom: 0 every piece alike .. 1 each with its own delay, curl and lift
};

// ─── small pieces ─────────────────────────────────────────────────────────────────────────────

// Hash 3 -> 1 in [0, 1) (Dave Hoskins' hash13, the jiggle's).
float PrismBurstHash(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p = p + dot(p, float3(p.y, p.z, p.x) + 33.33);
    return frac((p.x + p.y) * p.z);
}

float3 PrismBurstHash3(float3 p)
{
    return float3(PrismBurstHash(p), PrismBurstHash(p + 19.19), PrismBurstHash(p + 47.47));
}

// 1 − e^(−t/τ): fast at the instant of the collision, settling — the opening of every kind.
float PrismBurstEase(float age, float tau)
{
    return 1.0 - exp(-max(age, 0.0) / max(tau, 1e-4));
}

// Rodrigues: v rotated by angle about the UNIT axis.
float3 PrismBurstRotate(float3 v, float3 axis, float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return v * c + cross(axis, v) * s + axis * (dot(axis, v) * (1.0 - c));
}

// A unit axis from v, or the fallback when v is too short to have a direction.
float3 PrismBurstAxis(float3 v, float3 fallback)
{
    float len2 = dot(v, v);
    return len2 > 1e-8 ? v / sqrt(len2) : fallback;
}

// The jittered seed of shard cell (i, j) on a face, in [0, 1]². Kept inside its own cell
// (20%..80%), so cells stay similar in size and no shard is a sliver.
float2 PrismBurstSeed(float i, float j, float3 faceKey, float cells)
{
    float3 h = PrismBurstHash3(faceKey * 31.0 + float3(i, j, 7.0));
    return (float2(i, j) + float2(0.2, 0.2) + float2(h.x, h.y) * 0.6) / cells;
}

// The Voronoi cell of uv01 (face coords in [0, 1]²): its seed, and the distance from uv01 to the
// nearest bisector — positive inside the cell (the harness checks every point lies in its own cell).
void PrismBurstVoronoi(float2 uv01, float3 faceKey, float cells, out float2 seed, out float border)
{
    float k = clamp(floor(cells + 0.5), 1.0, PRISM_BURST_MAX_CELLS);
    float best = 1e9;
    float2 bestSeed = float2(0.5, 0.5);
    for (float j = 0.0; j < k; j += 1.0)
    {
        for (float i = 0.0; i < k; i += 1.0)
        {
            float2 s = PrismBurstSeed(i, j, faceKey, k);
            float2 d = uv01 - s;
            float dd = dot(d, d);
            if (dd < best)
            {
                best = dd;
                bestSeed = s;
            }
        }
    }
    float edge = 1e9;
    for (float j2 = 0.0; j2 < k; j2 += 1.0)
    {
        for (float i2 = 0.0; i2 < k; i2 += 1.0)
        {
            float2 s2 = PrismBurstSeed(i2, j2, faceKey, k);
            float2 dd2 = s2 - bestSeed;
            float len2 = dot(dd2, dd2);
            if (len2 > 1e-10)
            {
                float2 mid = (s2 + bestSeed) * 0.5;
                edge = min(edge, dot(mid - uv01, dd2) / sqrt(len2));
            }
        }
    }
    seed = bestSeed;
    border = edge;
}

// An exact, ISOMETRIC bend of a flat strip about a hinge line: the point at arc length s from the
// hinge (along `inward`, in the strip's plane) goes where a strip of curvature κ rolled toward
// `lift` puts it, and every vector is turned by the strip's local angle a = κ·s. Inward, lift and
// the hinge's own direction are orthonormal. As κ → 0 it is the identity, with no division.
void PrismBurstBend(float3 x, float3 n, float3 hinge, float3 inward, float3 lift, float kappa,
    out float3 pos, out float3 nrm)
{
    float3 r = x - hinge;
    float s = dot(r, inward);
    float l = dot(r, lift);
    float3 along = r - inward * s - lift * l;
    float a = kappa * s;
    float ca = cos(a);
    float sa = sin(a);
    float arcIn = s;
    float arcUp = 0.5 * kappa * s * s;
    if (abs(kappa) > 1e-5)
    {
        // 1 − cos a written as 2·sin²(a/2): the same number, without the cancellation that a GPU's
        // less exact cos would divide by a tiny κ into a visible jitter on a curl's first frames.
        float sh = sin(0.5 * a);
        arcIn = sa / kappa;
        arcUp = 2.0 * sh * sh / kappa;
    }
    pos = hinge + along + inward * arcIn + lift * arcUp + (lift * ca - inward * sa) * l;
#if PRISM_BURST_BEND_NORMAL
    float ni = dot(n, inward);
    float nl = dot(n, lift);
    nrm = n - inward * ni - lift * nl + inward * (ni * ca - nl * sa) + lift * (ni * sa + nl * ca);
#else
    nrm = n;
#endif
}

// How far from the travelling centre any piece of any kind can be, in prism radii, for a given
// spread. A piece's pivot is on the surface (1), its farthest point is at most a diameter from the
// pivot (2), it leaves by at most 1.5 spreads (the glitter's), plus 0.5 for a petal's or ribbon's
// lift. The CPU sizes the entity's culling bounds with the SAME formula
// (PrismCollisionBurst.EnvelopeRadii); the harness asserts every pose stays inside it, and its
// negative control shrinks the base to 1 and watches a pose escape.
#ifndef PRISM_BURST_ENVELOPE_BASE
#define PRISM_BURST_ENVELOPE_BASE 3.5
#endif
float PrismBurstEnvelope(float spread)
{
    return PRISM_BURST_ENVELOPE_BASE + 1.5 * max(spread, 0.0);
}

// ─── the kinds ────────────────────────────────────────────────────────────────────────────────

void PrismBurstShatter(PrismBurstFrame f, PrismBurstParams p, float shardScale, float spinScale,
    float3 pivot, float3 h, out float3 pos, out float3 nrm)
{
    float3 dir = normalize(f.normal * 1.2 + (h - 0.5) * 1.4);
    float e = PrismBurstEase(p.age, p.tau);
    float3 disp = dir * (min(p.spread, 1.0) * shardScale * f.radius * e);
    float3 axis = PrismBurstAxis(h * 2.0 - 1.0, f.normal);
    float angle = p.spin * spinScale * p.speed * max(p.age, 0.0) * (0.5 + h.z);
    pos = pivot + disp + PrismBurstRotate(f.rest - pivot, axis, angle);
    nrm = PrismBurstRotate(f.normal, axis, angle);
}

// The whole map for one vertex. pos/nrm exclude the flight (the shader adds v·t to every kind,
// which is what makes the squint the explosion's; a shed has none). pieceHash in [0, 1) names the
// vertex's piece, the same for every vertex of it — the dissolve's per-piece order (for the flake, its
// place in the wave).
void PrismBurstPose(float kind, PrismBurstFrame f, PrismBurstParams p,
    out float3 pos, out float3 nrm, out float pieceHash)
{
    float k = floor(kind + 0.5);
    if (k == PRISM_BURST_BLOOM)
    {
        // The quarter is the triangle's diagonal quarter of the face; its outer edge is a cube edge.
        float ax = abs(f.triUV.x);
        float ay = abs(f.triUV.y);
        float sg;
        float3 edgeVec;
        float side;
        float across;
        // A centroid exactly ON a diagonal (the anti-diagonal cells of the grid put one there) is a
        // tie, and float rounding would pick its petal differently per GPU. The bias settles every
        // tie on the u-edge petal on every platform; real centroids are 2/(3n) away from a diagonal.
        if (ax >= ay - PRISM_BURST_PETAL_TIE)
        {
            sg = f.triUV.x >= 0.0 ? 1.0 : -1.0;
            edgeVec = f.faceU * sg;
            side = 1.0;
            across = f.triUV.y;
        }
        else
        {
            sg = f.triUV.y >= 0.0 ? 1.0 : -1.0;
            edgeVec = f.faceV * sg;
            side = 2.0;
            across = f.triUV.x;
        }
        // Its piece: the ring (bands parallel to the cube edge, from the edge in) and the segment along it.
        // Each piece hinges on its own outer edge, so it is a band's depth long — short, so it curls tight
        // and stays close to the surface.
        float rings = max(floor(p.rings + 0.5), 1.0);
        float segments = max(floor(p.segments + 0.5), 1.0);
        float depthIn = 1.0 - (side == 1.0 ? ax : ay);
        float ring = min(floor(depthIn * rings), rings - 1.0);
        float segment = min(floor((across * 0.5 + 0.5) * segments), segments - 1.0);
        float L = max(length(edgeVec), 1e-5);
        float3 inward = -edgeVec / L;
        float band = L / rings;
        float3 hinge = f.faceCentre + edgeVec * (1.0 - ring / rings);
        float3 h = PrismBurstHash3(f.faceKey * 5.3 + float3(side * 16.0 + ring, sg * 16.0 + segment, p.seed * 13.0));
        // The noise: each piece its own start, curl and lift; the rings peel from the edge inward.
        float noise = saturate(p.noise);
        float delay = ring / rings * 0.15 + h.x * (0.06 + 0.25 * noise);
        float e = PrismBurstEase(p.age - delay, p.tau);
        float curl = lerp(1.0, 0.4 + 1.2 * h.y, noise);
        float tip = p.bloomTurns * PRISM_BURST_TAU * e * curl;
        PrismBurstBend(f.rest, f.normal, hinge, inward, f.normal, tip / band, pos, nrm);
        pos = pos + f.normal * (min(p.spread, 1.0) * 0.35 * band * e * lerp(1.0, 0.3 + 1.4 * h.z, noise));
        pieceHash = frac(h.z * 3.7 + h.x);
        return;
    }
    if (k == PRISM_BURST_UNRAVEL)
    {
        // Ribbons run along the face's longer WORLD axis; the triangle's place across it picks one.
        bool alongU = dot(f.faceU, f.faceU) >= dot(f.faceV, f.faceV);
        float3 longVec = alongU ? f.faceU : f.faceV;
        float3 crossVec = alongU ? f.faceV : f.faceU;
        float across = alongU ? f.triUV.y : f.triUV.x;
        float strips = max(floor(p.strips + 0.5), 1.0);
        float idx = min(floor((across * 0.5 + 0.5) * strips), strips - 1.0);
        float3 h = PrismBurstHash3(f.faceKey * 9.1 + float3(idx, alongU ? 1.0 : 2.0, p.seed * 7.0));
        float sg = h.x >= 0.5 ? 1.0 : -1.0;
        float stripCentre = (idx + 0.5) / strips * 2.0 - 1.0;
        float halfLong = max(length(longVec), 1e-5);
        float3 inward = -longVec * sg / halfLong;
        float3 hinge = f.faceCentre + longVec * sg + crossVec * stripCentre;
        float e = PrismBurstEase(p.age - h.y * 0.25, p.tau * 1.4);
        float tip = p.unravelTurns * PRISM_BURST_TAU * e * (0.85 + 0.3 * h.z);
        PrismBurstBend(f.rest, f.normal, hinge, inward, f.normal, tip / (2.0 * halfLong), pos, nrm);
        pos = pos + f.normal * (min(p.spread, 1.0) * 0.25 * f.radius * e);
        pieceHash = frac(h.z * 7.13 + h.y);
        return;
    }
    if (k == PRISM_BURST_FLAKE)
    {
        // The scale: a cell of a grid twice as fine as the shatter's; rigid.
        float g = clamp(floor(p.cells * 2.0 + 0.5), 1.0, 2.0 * PRISM_BURST_MAX_CELLS);
        float2 cell = min(floor((f.triUV * 0.5 + float2(0.5, 0.5)) * g), float2(g - 1.0, g - 1.0));
        float2 c11 = (cell + float2(0.5, 0.5)) / g * 2.0 - float2(1.0, 1.0);
        float3 pivot = f.faceCentre + f.faceU * c11.x + f.faceV * c11.y;
        float3 h = PrismBurstHash3(f.faceKey * 7.7 + float3(cell.x, cell.y, p.seed * 19.0));
        // The wave runs the long axis, from the end the seed picks.
        float halfLong = max(length(f.longAxis), 1e-5);
        float3 sweep = f.longAxis / halfLong * (frac(p.seed * 3.17) >= 0.5 ? 1.0 : -1.0);
        float along = saturate(0.5 + 0.5 * dot(pivot - f.centre, sweep) / halfLong);
        // Its turn in the fade (a little ragged), and its lift: from 70% of the way to its turn, fully
        // up when the front takes it. Progress 0 lifts nothing, so the first frame is the skin.
        float turn = min(along * 0.94 + h.x * 0.05, 0.999);
        float lift = saturate((p.progress - 0.7 * turn) / max(0.3 * turn, 1e-3));
        lift = lift * lift * (3.0 - 2.0 * lift);
        // It tips up on its far edge: the hinge runs across the wave, in the face; an end face (the
        // wave along its normal) hinges on its u axis.
        float3 inPlane = sweep - f.normal * dot(sweep, f.normal);
        float3 axis = PrismBurstAxis(cross(f.normal, inPlane), PrismBurstAxis(f.faceU, f.normal));
        float3 back = PrismBurstAxis(cross(axis, f.normal), f.normal);
        float reach = (abs(dot(back, f.faceU)) + abs(dot(back, f.faceV))) / g;
        float3 hinge = pivot + back * reach;
        float angle = lift * (0.55 + 0.25 * h.y) * PRISM_BURST_TAU * 0.5;
        pos = hinge + PrismBurstRotate(f.rest - hinge, axis, angle) + f.normal * (min(p.spread, 1.0) * 0.3 * f.radius * lift);
        nrm = PrismBurstRotate(f.normal, axis, angle);
        pieceHash = turn;
        return;
    }
    if (k == PRISM_BURST_GLITTER)
    {
        float3 pivot = f.faceCentre + f.faceU * f.triUV.x + f.faceV * f.triUV.y;
        float3 h = PrismBurstHash3(f.faceKey * 3.1 + float3(f.triUV.x * 57.0, f.triUV.y * 57.0, p.seed * 13.0));
        PrismBurstShatter(f, p, 1.5, 2.5, pivot, h, pos, nrm);
        pieceHash = h.x;
        return;
    }
    // SHATTER (and the fallback for an unknown kind).
    float2 seed;
    float border;
    PrismBurstVoronoi(f.triUV * 0.5 + float2(0.5, 0.5), f.faceKey, p.cells, seed, border);
    float2 s11 = seed * 2.0 - float2(1.0, 1.0);
    float3 pivot = f.faceCentre + f.faceU * s11.x + f.faceV * s11.y;
    float3 h = PrismBurstHash3(f.faceKey * 13.7 + float3(seed.x * 101.0, seed.y * 101.0, p.seed * 17.0));
    PrismBurstShatter(f, p, 1.0, 1.0, pivot, h, pos, nrm);
    pieceHash = h.x;
}

// ─── the explosion's law, for every fragment (kind WEDGE) ─────────────────────────────────────────

// RotateFacesAlongAxis' rotation: v about axis by angle; a zero axis is left unnormalised (the graph's own
// behaviour, held by the lab's parity gate against the evaluated subgraph).
float3 PrismBurstRotateAboutAxis(float3 v, float3 axis, float angle)
{
    float m2 = dot(axis, axis);
    float3 a = m2 > 0.0 ? axis / sqrt(m2) : axis;
    float s = sin(angle);
    float c = cos(angle);
    float k = 1.0 - c;
    float3 r0 = float3(k * a.x * a.x + c, k * a.x * a.y - s * a.z, k * a.x * a.z + s * a.y);
    float3 r1 = float3(k * a.x * a.y + s * a.z, k * a.y * a.y + c, k * a.y * a.z - s * a.x);
    float3 r2 = float3(k * a.x * a.z - s * a.y, k * a.y * a.z + s * a.x, k * a.z * a.z + c);
    return float3(dot(r0, v), dot(r1, v), dot(r2, v));
}

// The explosion's map of one vertex, in OBJECT space, about any pivot (RotateFacesAlongAxis' closed form,
// W = 0, generalised from the wedge's own pivot): the vertex turns about the piece's tangent T, then about
// cross(velocity, N), by amount × rotation — the rotation done in the object-SCALED frame, as the graph does
// it — and the piece slides out along T by amount × spreadPerAmount. velocity is the stamped WORLD velocity,
// used as the graph uses it.
void PrismBurstExplosionMap(float3 P, float3 N, float3 T, float3 pivot, float3 objectScale, float amount,
    float3 velocity, float rotation, float spreadPerAmount, out float3 pos, out float3 nrm)
{
#ifdef PRISM_BURST_WEDGE_PIVOT_UNSCALED
    float3 os = float3(1.0, 1.0, 1.0);   // negative control only: the turn in the unscaled frame shears a piece
#else
    float3 os = max(abs(objectScale), float3(1e-5, 1e-5, 1e-5));
#endif
    float3 local = os * (P - pivot);
    float angle = amount * rotation;
    float3 axis2 = cross(velocity, N);
    float3 r = PrismBurstRotateAboutAxis(PrismBurstRotateAboutAxis(local, T, angle), axis2, angle);
    nrm = PrismBurstRotateAboutAxis(PrismBurstRotateAboutAxis(N, T, angle), axis2, angle);
    pos = r / os + pivot + (PRISM_BURST_SLIDE_BIAS + PRISM_BURST_PIVOT_BIAS + amount * spreadPerAmount) * T * PRISM_BURST_SLIDER_GAIN;
}

// The explosion's own pivot for a wedge of the prism cube: the face point under P, slid along T by the
// spread (the graph's Pn + (spread - pivotBias) T gain). Used by the explosion itself (depth 0).
float3 PrismBurstWedgePivot(float3 P, float3 N, float3 T, float3 spreadValue)
{
    return dot(P, N) * N + (spreadValue - PRISM_BURST_PIVOT_BIAS) * T * PRISM_BURST_SLIDER_GAIN;
}

// The tangent of the explosion WEDGE a face point belongs to: the face's in-plane object axis, signed, along which
// the point lies furthest from the face centre (the prism is the axis-aligned unit cube in object space, and its
// faces are cut into four wedges along their diagonals — Prism.asset's tangent for each). A fragment's centroid
// lies strictly inside its wedge, so it is never on a diagonal.
float3 PrismBurstWedgeTangent(float3 offsetInFace)
{
    float3 a = abs(offsetInFace);
    if (a.x >= a.y && a.x >= a.z) return float3(offsetInFace.x >= 0.0 ? 1.0 : -1.0, 0.0, 0.0);
    if (a.y >= a.z) return float3(0.0, offsetInFace.y >= 0.0 ? 1.0 : -1.0, 0.0);
    return float3(0.0, 0.0, offsetInFace.z >= 0.0 ? 1.0 : -1.0);
}

// A Sierpinski fragment as a SCALED WEDGE (kind WEDGE). It turns about its ROOT WEDGE's own two axes — the wedge's
// tangent T and cross(velocity, N), the explosion's velocity response — by the explosion's angle, and slides along
// that same T: every fragment of a wedge spins exactly as the explosion spins that wedge and is sent the way it is
// sent. Its pivot keeps the explosion's pivot-to-centroid offset, scaled by the fragment's size (2^-level), so the
// pieces tumble apart about their own centres; at level 0 — the explosion's own wedges — this IS the explosion.
// All in object space, as the explosion's graph.
void PrismBurstWedgePose(float3 P, float3 N, float3 pieceCentroid, float level, float3 objectScale, float amount,
    float3 velocity, float3 spreadValue, float rotation, float spreadPerAmount, out float3 pos, out float3 nrm)
{
    float3 faceCentre = dot(P, N) * N;
#ifdef PRISM_BURST_WEDGE_OWN_TANGENT
    float3 T = PrismBurstAxis(pieceCentroid - faceCentre, float3(0.0, 0.0, 0.0));   // negative control only: round 5's radial tangent
#else
    float3 T = PrismBurstWedgeTangent(pieceCentroid - faceCentre);
#endif
    // A wedge's centroid lies a third of its height from the face centre; the explosion pivots it at
    // (spread - pivotBias) × gain. The fragment keeps that offset at its own scale.
    float3 wedgeOffset = (spreadValue - PRISM_BURST_PIVOT_BIAS) * T * PRISM_BURST_SLIDER_GAIN - T * (1.0 / 3.0);
    float3 pivot = pieceCentroid + wedgeOffset * exp2(-max(level, 0.0));
    PrismBurstExplosionMap(P, N, T, pivot, objectScale, amount, velocity, rotation, spreadPerAmount, pos, nrm);
}

// How far, in WORLD units, a WEDGE fragment can be from the prism's travelling centre (the flight is added
// after). Proved, not fitted: the fragment's centroid and every vertex lie inside the prism (within R of the
// centre, so a vertex is within 2R of its centroid); the pivot sits off the centroid by the wedge offset, at
// most o = max |(spread - pivotBias) × gain - 1/3| object units (≤ o × maxScale in the world); the turn keeps
// the WORLD distance to the pivot (it is done in the scaled frame); the slide adds amount × spreadPerAmount ×
// gain along a unit object direction. So 3R + 2 o maxScale + slide. The CPU sizes the culling bounds by the
// same sum (PrismCollisionBurst.WedgeReach); the harness asserts every pose stays inside it.
float PrismBurstWedgeReach(float radius, float maxScale, float amount, float spreadPerAmount, float3 spreadValue)
{
    float3 o = abs((spreadValue - PRISM_BURST_PIVOT_BIAS) * PRISM_BURST_SLIDER_GAIN - 1.0 / 3.0);
    float offset = max(o.x, max(o.y, o.z));
    return 3.0 * radius + (2.0 * offset + abs(amount * spreadPerAmount) * PRISM_BURST_SLIDER_GAIN) * maxScale;
}

// ─── the fade ─────────────────────────────────────────────────────────────────────────────────

// How far the fade has come, reaching 1 (all gone) at life / (1 + overshoot), before retirement.
// curve 0 — DESTRUCTION: linear in age, the explosion's opacity curve. curve 1 — SHED: the grade's
// smoothstep (PrismColorLerp: t = smoothstep(0, 1, saturate(elapsed / duration))), so with the life
// stamped as the grade's duration × (1 + overshoot) the skin's coverage is 1 − t: squinted at, the
// mean colour runs exactly the grade's course.
float PrismBurstProgress(float age, float life, float curve)
{
    float x = max(age, 0.0) / max(life, 1e-4) * (1.0 + PRISM_BURST_DISSOLVE_OVERSHOOT);
#if PRISM_BURST_GRADE_SMOOTH
    if (curve > 0.5)
        return smoothstep(0.0, 1.0, saturate(x));
#endif
    return x;
}

// A fragment's place in the fade, in [0, 1): its piece's own time plus a ragged grain.
float PrismBurstDissolveValue(float pieceHash, float noise, float grain)
{
#if PRISM_BURST_UNIFORM_VALUE
    return frac(pieceHash + saturate(grain) * noise);
#else
    return lerp(pieceHash, noise, saturate(grain));
#endif
}

bool PrismBurstClipped(float value, float progress)
{
    return value < progress;
}

// Value noise in [0, 1], trilinear with a smoothstep weight — the slice's.
float PrismBurstNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = x - i;
    float3 w = f * f * (3.0 - 2.0 * f);
    float n000 = PrismBurstHash(i + float3(0.0, 0.0, 0.0));
    float n100 = PrismBurstHash(i + float3(1.0, 0.0, 0.0));
    float n010 = PrismBurstHash(i + float3(0.0, 1.0, 0.0));
    float n110 = PrismBurstHash(i + float3(1.0, 1.0, 0.0));
    float n001 = PrismBurstHash(i + float3(0.0, 0.0, 1.0));
    float n101 = PrismBurstHash(i + float3(1.0, 0.0, 1.0));
    float n011 = PrismBurstHash(i + float3(0.0, 1.0, 1.0));
    float n111 = PrismBurstHash(i + float3(1.0, 1.0, 1.0));
    float nx00 = lerp(n000, n100, w.x);
    float nx10 = lerp(n010, n110, w.x);
    float nx01 = lerp(n001, n101, w.x);
    float nx11 = lerp(n011, n111, w.x);
    float nxy0 = lerp(nx00, nx10, w.y);
    float nxy1 = lerp(nx01, nx11, w.y);
    return lerp(nxy0, nxy1, w.z);
}

#endif // PRISM_COLLISION_BURST_INCLUDED
