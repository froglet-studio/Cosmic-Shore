// PrismCollisionBurst.hlsl — the GPU side of a COLLISION death (Docs/PRISM_ANIMATION.md §4.11,
// Docs/PRISM_MOTION.md § "The look"). A moving prism that runs into another prism destroys both
// (PrismMotion, the prism-movement fundamental); while few prisms are moving and the budget allows,
// each of the two comes apart as a COLLISION BURST drawn on the ultra-high-res prism the Rhino's
// slice uses, instead of as the ordinary explosion debris.
//
// THE BRIEF, and the one constraint every line below answers to: spectacular when you LOOK at it,
// indistinguishable from the ordinary explosion when you SQUINT. So the burst shares the explosion's
// whole ENVELOPE and differs only at the fine scale the dense mesh affords:
//   • the same FLIGHT — the piece cloud's frame travels at the explosion's own clamped, gained
//     velocity, linearly (offset = v·t), for the explosion's own 7.5 s life (PrismDebris resolves
//     both, so the two cannot drift apart);
//   • the same SIZE — every piece stays within PrismBurstEnvelope prism radii of the travelling
//     centre, the way the explosion's wedges spin in place rather than fly off;
//   • the same COLOUR — the domain's dark face brightening to its bright rim at grazing angles;
//     accents (fracture glow, ember front, glints) are thin lines and brief instants;
//   • the same FADE — coverage falls LINEARLY with age (PrismBurstDissolveValue is uniform on [0,1),
//     so the surviving fraction is exactly 1 − progress), finishing just before retirement.
//
// WHY A STAMP AND NOT A GLOBAL (the prism-morph admission test, question 1). Everything is known
// at the instant of the collision — the pose, the velocity, the kind — so this is §1's ordinary
// shape: one stamp per prism, the GPU runs the course off _PrismClock, one scheduled retirement.
// No per-frame CPU write to any burst, ever. It is the slice's neighbour: it rides the slice's
// batch spawner and stamp channels (PrismCollisionBurst.cs), born ON the dense mesh, so there is
// no residency swap and nothing to hide.
//
// WHY THE DENSE MESH, AND WHY UNWELDED. Each burst draws HighPolyPrismMesh.GetUnwelded — the
// identical solid at subdivision 10 (1,200 triangles against the prism's 24) with every triangle
// owning its own three vertices. Unwelded is what lets the surface TEAR along any pattern the
// kind chooses (shard borders, petal diagonals, ribbon seams) with no stretched triangle across
// the tear; dense is what makes those tears and the curls smooth. TEXCOORD2 gives each vertex its
// triangle's centroid in face coordinates plus a per-triangle random, so every vertex of a
// triangle makes the SAME choice of piece — a triangle never straddles two pieces.
//
// THE FOUR KINDS (_SliceTiming.w; chosen per prism on the CPU, deterministic in the pose):
//   0 SHATTER — each face fractures into Voronoi shards (PrismBurstVoronoi over the triangle
//     centroids), each a RIGID piece that leaves along its face normal and tumbles about its own
//     seed point; the fracture lines flash white-hot and cool.
//   1 BLOOM   — each face splits along its diagonals into four petals, exactly the explosion's
//     wedge layout, and each petal CURLS OUT from its cube edge like a flower opening — an exact
//     isometric bend (PrismBurstBend), so the petal rolls without stretching.
//   2 UNRAVEL — each face peels into ribbons along its longer axis, each ribbon curling up from
//     one end into a near-loop, staggered ribbon by ribbon.
//   3 GLITTER — every triangle is its own shard: 1,200 glints that spin out and blink away one by
//     one.
//
// EXACTNESS. Rigid pieces rotate their normal with them (Rodrigues). A bend's normal is the bend's
// derivative: the strip at arc length s is rotated by a = κ·s about the hinge, so its normal is
// that same rotation of the rest normal — exact, not a shortcut (the cradle's cheap-normal lesson).
// The harness (Tools/Shaders/verify_prism_collision_burst.py) executes this file and proves: the
// first frame IS the prism, rigid pieces stay rigid, bends are isometric with the normal as the
// derivative, every piece stays inside the envelope the CPU bounds the entity by, and coverage is
// linear in age and gone before retirement. It cannot prove the burst LOOKS good; that is the
// playtest's.

#ifndef PRISM_COLLISION_BURST_INCLUDED
#define PRISM_COLLISION_BURST_INCLUDED

#define PRISM_BURST_SHATTER 0.0
#define PRISM_BURST_BLOOM   1.0
#define PRISM_BURST_UNRAVEL 2.0
#define PRISM_BURST_GLITTER 3.0

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

// Upper bound on Voronoi cells per face edge — the loops below are bounded by it.
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
// nearest bisector — positive inside the cell, which is where the fracture line glows.
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
        arcIn = sa / kappa;
        arcUp = (1.0 - ca) / kappa;
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
// which is what makes the squint the explosion's). pieceHash in [0, 1) names the vertex's piece,
// the same for every vertex of it — the dissolve's per-piece order.
void PrismBurstPose(float kind, PrismBurstFrame f, PrismBurstParams p,
    out float3 pos, out float3 nrm, out float pieceHash)
{
    float k = floor(kind + 0.5);
    if (k == PRISM_BURST_BLOOM)
    {
        // The petal is the triangle's diagonal quarter of the face; its hinge is the cube edge.
        float ax = abs(f.triUV.x);
        float ay = abs(f.triUV.y);
        float sg;
        float3 edgeVec;
        float side;
        if (ax >= ay)
        {
            sg = f.triUV.x >= 0.0 ? 1.0 : -1.0;
            edgeVec = f.faceU * sg;
            side = 1.0;
        }
        else
        {
            sg = f.triUV.y >= 0.0 ? 1.0 : -1.0;
            edgeVec = f.faceV * sg;
            side = 2.0;
        }
        float L = max(length(edgeVec), 1e-5);
        float3 inward = -edgeVec / L;
        float3 hinge = f.faceCentre + edgeVec;
        float3 h = PrismBurstHash3(f.faceKey * 5.3 + float3(side, sg, p.seed * 13.0));
        float e = PrismBurstEase(p.age - h.x * 0.12, p.tau);
        float tip = p.bloomTurns * PRISM_BURST_TAU * e * (0.8 + 0.4 * h.y);
        PrismBurstBend(f.rest, f.normal, hinge, inward, f.normal, tip / L, pos, nrm);
        pos = pos + f.normal * (min(p.spread, 1.0) * 0.35 * f.radius * e);
        pieceHash = h.z;
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

// ─── the fade ─────────────────────────────────────────────────────────────────────────────────

// How far the fade has come: linear in age, the explosion's opacity curve, reaching 1 (all gone)
// just before retirement.
float PrismBurstProgress(float age, float life)
{
    return max(age, 0.0) / max(life, 1e-4) * (1.0 + PRISM_BURST_DISSOLVE_OVERSHOOT);
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
