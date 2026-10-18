// PrismCollisionBurst.hlsl — the GPU side of the prism's DETAIL LEVELS for its two fundamental
// interactions (Docs/PRISM_ANIMATION.md §4.12, Docs/PRISM_MOTION.md § "The look"): how a prism is
// DESTROYED and how it is STOLEN, drawn on the ultra-high-res prism the Rhino's slice uses whenever the
// viewer is close enough to see the detail and the budget allows, and as the ordinary effect otherwise.
// (The file keeps the name of its first use, the prism-movement collision.)
//
//   DESTRUCTION cuts the explosion's own wedges into a range of fragment sizes — the silver gnomon cut
//   (PrismSilverGnomonTiling; the Sierpinski cut stays selectable) — and every fragment bursts out on its own
//   direction, tumbles about cross(velocity, N) at the explosion's own speed-capped rate, and shrinks away at its
//   own end, the smallest first (kind FRAGMENT, PrismBurstFragmentPose). Far away the small ones simply go unseen
//   first; there is no squint target any more (the designer, 2026-10-16: "now I just want it to look awesome").
//
//   STEALING is the GRADE far away — the 0.8 s smoothstep colour lerp PrismColorLerp runs on the prism —
//   and a SHED close up: the prism takes its new colour at once and a skin wearing the OLD colours comes
//   off it. The game sheds as a ROLL (round 7, the mass crystal's camera trick, PrismBurstRoll): each face's skin
//   rolls up away from the camera and round the prism until the prism itself hides it.
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
//   6 ROLL    — shed (the game's, round 7). Each face's skin rolls up like a carpet AWAY from the camera, over the
//     edge and round onto a face the camera cannot see, until its own prism hides it (PrismBurstRoll).
//   5 FRAGMENT — destruction (round 9). Each triangle of the cut (silver gnomon or Sierpinski) bursts out on its own
//     direction, tumbles about cross(velocity, N) and its own travel at the explosion's capped rate, and shrinks away
//     at its own end, the smallest first (PrismBurstFragmentPose).
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
#define PRISM_BURST_FRAGMENT 5.0
#define PRISM_BURST_ROLL    6.0

// The ROLL shed (the designer, 2026-10-15: "the peeling can curl into a tight roll and roll along and around the
// surface until the prism occludes it ... then the rolls don't need to dissolve"). Its life is the grade's length
// × (1 + TAIL): the face is rolled up over the grade's length, the tail carries the roll over the far edge and out
// of sight behind its own prism. EASE: the share of the grade the roll takes to get up to speed (then it rolls at
// a constant speed). HIDE: the least the roll travels down the side face past the edge, in roll radii.
// FADE: the last share of the life in which a roll the prism could not hide (a very thin prism) dissolves.
#define PRISM_BURST_ROLL_TAIL 0.6
#define PRISM_BURST_ROLL_EASE 0.3
#define PRISM_BURST_ROLL_HIDE 2.0
#define PRISM_BURST_ROLL_FADE 0.15

// RotateFacesAlongAxis / TangentSlider's constants (the explosion's subgraphs; the lab's build.py reads
// them from the graph and asserts these): the pivot bias, the slide bias and the slider gain.
#ifndef PRISM_BURST_PIVOT_BIAS
#define PRISM_BURST_PIVOT_BIAS (-0.5)
#endif
#define PRISM_BURST_SLIDE_BIAS 0.5

// Scales the gradient noise so it spans about [-1, 1] (the harness measures its spread: most of it inside, a
// clamped tail).
#define PRISM_BURST_GRADIENT_GAIN 1.6
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
    float3 camera;      // roll: the camera's world position when the steal happened (the roll peels away from it)
    float life;         // roll: the stamped life (the grade's length × (1 + PRISM_BURST_ROLL_TAIL))
    float subdivision;  // roll: the shed mesh's cells along a face edge (a turn of the roll must span several)
    float rollRadius;   // roll: the innermost turn's radius, in prism radii (at least about a mesh cell)
    float rollGap;      // roll: the gap between turns, in prism radii (at least the chord's sag, so turns never cross)
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

// ─── the roll (kind ROLL) ─────────────────────────────────────────────────────────────────────────────

// The roll's fade: 0 for all but the last PRISM_BURST_ROLL_FADE of its life — a roll is meant to leave by being
// hidden behind its own prism, not by dissolving.
float PrismBurstRollFade(float age, float life)
{
    return saturate((age / max(life, 1e-4) - (1.0 - PRISM_BURST_ROLL_FADE)) / PRISM_BURST_ROLL_FADE);
}

// A face's skin rolled up like a carpet, AWAY from the camera. The roll direction d is the face's in-plane axis along
// which the camera is furthest off the face centre, pointing away from it; the roll starts at the near edge, winds
// the skin into an Archimedean spiral (inner radius a, a gap g between turns: r(w) = sqrt(a² + g w / π) for the
// length w wound), and crosses the face over the grade's length — eased in, then at a constant speed. The skin goes
// into the roll top side in, so the roll shows its inside: the NEW colour. Past the far edge the roll pivots round
// it onto the next face — which faces away from the camera, by the choice of d — and rolls along it until its own
// prism hides it from the camera; it stops short of that face's far edge. Exact: an isometry of the sheet (to the
// spiral's pitch, b²/2r²), its normal the spiral's.
void PrismBurstRoll(PrismBurstFrame f, PrismBurstParams p, out float3 pos, out float3 nrm)
{
    float Lu = length(f.faceU);
    float Lv = length(f.faceV);
    float3 uHat = f.faceU / max(Lu, 1e-6);
    float3 vHat = f.faceV / max(Lv, 1e-6);
    float3 toCam = p.camera - f.faceCentre;
    float cu = dot(toCam, uHat);
    float cv = dot(toCam, vHat);
    bool alongU = abs(cu) >= abs(cv);
#ifdef PRISM_BURST_ROLL_TOWARD_CAMERA
    float away = -1.0;   // negative control only: roll TOWARD the camera, onto the faces it sees
#else
    float away = 1.0;
#endif
    float3 d = (alongU ? uHat * (cu > 0.0 ? -1.0 : 1.0) : vHat * (cv > 0.0 ? -1.0 : 1.0)) * away;
    float L = alongU ? Lu : Lv;
    float3 n = f.normal;
    float3 rel = f.rest - f.faceCentre;
    float s = dot(rel, d);
    float3 lateral = rel - d * s - n * dot(rel, n);
    float w = clamp(s + L, 0.0, 2.0 * L);

    // The spiral, never coarser than the mesh: the inner turn spans about seven cells, and the gap clears the
    // sag of a cell's chord, so the polygon turns never cross.
    float seg = 2.0 * L / max(p.subdivision, 1.0);
    float a = max(p.rollRadius * f.radius, 0.9 * seg);
    float sh = sin(0.25 * seg / a);
    float g = max(max(p.rollGap * f.radius, 2.5 * a * sh * sh), 1e-5);
    float b = g / PRISM_BURST_TAU;
    float r = sqrt(a * a + 2.0 * b * w);

    float D = p.life / (1.0 + PRISM_BURST_ROLL_TAIL);
    float x = max(p.age, 0.0) / max(D, 1e-4);
    float k = PRISM_BURST_ROLL_EASE;
    float wound = 2.0 * L * (x < k ? x * x / (2.0 * k) : x - 0.5 * k) / (1.0 - 0.5 * k);
    if (x <= 1.0 && w >= wound)
    {
        pos = f.rest;   // still flat, ahead of the roll
        nrm = n;
        return;
    }
    wound = min(wound, 2.0 * L);
    float Rf = sqrt(a * a + 2.0 * b * wound);
    float delta = (Rf - r) / b;
    // The outer turn meets the face TANGENTIALLY: the spiral's pitch tilts its tangent by atan(b / R), so the
    // contact sits that far round from straight below the axis — the sheet's normal is continuous where it lifts.
    float eps = atan(b / Rf);
    float3 axis;
    float omega = 0.0;
    if (x <= 1.0)
    {
        axis = f.faceCentre + lateral + d * (wound - L) + Rf * (n * cos(eps) - d * sin(eps));
    }
    else
    {
        // Over the edge and away, rolling without slipping round the prism: a quarter turn about the far edge onto
        // the side face (which faces away from the camera), down it, and — when that face is too short to hide the
        // roll — a quarter turn about its far edge onto the face opposite the roll's own, under the prism. Fast
        // enough to be hidden well inside the tail.
        //
        // How far down the side face hides a roll 2R tall from this camera: a point at depth x below the edge,
        // R' off that face, is behind the prism once x (L + c) > h R' — h the camera's height over this face, c its
        // distance behind the near side (from the face centre). Never less than HIDE radii.
        float vEnd = 2.0 * L / (D * (1.0 - 0.5 * k));
        float camH = max(dot(toCam, n), 0.0);
        float camC = max(-dot(toCam, d), 0.0);
        float side = 2.0 * max(dot(f.faceCentre - f.centre, n), 0.0);   // the side face's depth: the prism's thickness
        float quarter = Rf * 0.25 * PRISM_BURST_TAU;
        float q1 = quarter;                 // round the far edge
        float q2 = q1 + side;               // down the side face
        float q3 = q2 + quarter;            // round its far edge
        float q4 = q3 + max(0.0, 2.0 * L - 2.0 * Rf);   // back along the opposite face, stopping short of its end
        // It STOPS at the first place that hides it — further on, a camera looking along the prism would see it
        // come out past the far end: down the side face as far as this camera needs, or, where the side face is
        // too short for that, one and a half roll radii round onto the opposite face.
        float xHide = max(PRISM_BURST_ROLL_HIDE * Rf, 1.25 * camH * 2.0 * Rf / max(L + camC, 1e-4));
        float qHide = xHide + Rf <= side ? q1 + xHide : min(q3 + 1.5 * Rf, max(q4, q3));
        float v = max(vEnd, qHide / (0.7 * PRISM_BURST_ROLL_TAIL * D));
        float q = min(v * (p.age - D), qHide);
        // The roll's ENDS tuck in as it goes over: a camera off to one side would otherwise see a strip of the roll's
        // end past the prism's corner, as wide as the roll stands off the prism times the camera's sideways slope.
        float3 latHat = alongU ? vHat : uHat;
        float W = alongU ? Lv : Lu;
        float camLat = abs(dot(toCam, latHat));
        float tuck = min(0.8 * W, 1.25 * 2.0 * Rf * camLat / max(L + camC, 1e-4));
        float ramp = smoothstep(0.0, 1.0, q / max(q1 + Rf, 1e-4));
        lateral = lateral * (1.0 - ramp * tuck / max(W, 1e-4));
        float3 edge1 = f.faceCentre + lateral + d * L;
        float3 edge2 = edge1 - n * side;
        float3 contact;
        float3 m;   // the outward normal of the surface the roll stands on
        float3 t;   // its direction of travel
        if (q < q1)
        {
            float beta = q / Rf;
            contact = edge1;
            m = n * cos(beta) + d * sin(beta);
            t = d * cos(beta) - n * sin(beta);
        }
        else if (q < q2)
        {
            contact = edge1 - n * (q - q1);
            m = d;
            t = -n;
        }
        else if (q < q3)
        {
            float gamma = (q - q2) / Rf;
            contact = edge2;
            m = d * cos(gamma) - n * sin(gamma);
            t = -n * cos(gamma) - d * sin(gamma);
        }
        else
        {
            contact = edge2 - d * (q - q3);
            m = -n;
            t = -d;
        }
        axis = contact + Rf * (m * cos(eps) - t * sin(eps));
        omega = q / Rf;
    }
    float alpha = -0.25 * PRISM_BURST_TAU + eps - delta - omega;
    float3 radial = d * cos(alpha) + n * sin(alpha);
    float3 around = n * cos(alpha) - d * sin(alpha);   // d radial / d alpha
    pos = axis + radial * r;
    // The spiral's own normal: perpendicular to its tangent (b radial + r around), the skin's top side inward.
    nrm = (around * b - radial * r) / sqrt(r * r + b * b);
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
    if (k == PRISM_BURST_ROLL)
    {
        PrismBurstRoll(f, p, pos, nrm);
        pieceHash = PrismBurstHash(f.faceKey * 7.1 + p.seed * 3.3);
        return;
    }
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

// ─── the explosion's own law (the lab draws the explosion with it, parity-gated against the graph) ───────────

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
#ifdef PRISM_BURST_EXPLOSION_UNSCALED
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
// spread (the graph's Pn + (spread - pivotBias) T gain).
float3 PrismBurstWedgePivot(float3 P, float3 N, float3 T, float3 spreadValue)
{
    return dot(P, N) * N + (spreadValue - PRISM_BURST_PIVOT_BIAS) * T * PRISM_BURST_SLIDER_GAIN;
}

// Perlin's gradient noise in 3D, about [-1, 1] (clamped): a pseudo-random gradient at every lattice point
// (PrismBurstHash3), blended by the quintic fade, so the field is continuous with a continuous derivative — two
// nearby points get nearly the same value, two far apart unrelated ones.
float PrismBurstGradientNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = p - i;
    float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float n000 = dot(PrismBurstHash3(i + float3(0.0, 0.0, 0.0)) * 2.0 - 1.0, f - float3(0.0, 0.0, 0.0));
    float n100 = dot(PrismBurstHash3(i + float3(1.0, 0.0, 0.0)) * 2.0 - 1.0, f - float3(1.0, 0.0, 0.0));
    float n010 = dot(PrismBurstHash3(i + float3(0.0, 1.0, 0.0)) * 2.0 - 1.0, f - float3(0.0, 1.0, 0.0));
    float n110 = dot(PrismBurstHash3(i + float3(1.0, 1.0, 0.0)) * 2.0 - 1.0, f - float3(1.0, 1.0, 0.0));
    float n001 = dot(PrismBurstHash3(i + float3(0.0, 0.0, 1.0)) * 2.0 - 1.0, f - float3(0.0, 0.0, 1.0));
    float n101 = dot(PrismBurstHash3(i + float3(1.0, 0.0, 1.0)) * 2.0 - 1.0, f - float3(1.0, 0.0, 1.0));
    float n011 = dot(PrismBurstHash3(i + float3(0.0, 1.0, 1.0)) * 2.0 - 1.0, f - float3(0.0, 1.0, 1.0));
    float n111 = dot(PrismBurstHash3(i + float3(1.0, 1.0, 1.0)) * 2.0 - 1.0, f - float3(1.0, 1.0, 1.0));
    float x00 = lerp(n000, n100, u.x);
    float x10 = lerp(n010, n110, u.x);
    float x01 = lerp(n001, n101, u.x);
    float x11 = lerp(n011, n111, u.x);
    return clamp(PRISM_BURST_GRADIENT_GAIN * lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z), -1.0, 1.0);
}

// ─── the destruction's fragments (kind FRAGMENT) ──────────────────────────────────────────────────────

// How a fragment flies — written by PrismCollisionBurst from PrismInteractionLodConfig (the designer, 2026-10-16:
// "now I just want it to look awesome"; the squint against the explosion is retired).
struct PrismBurstFragmentShape
{
    float burst;      // how far a fragment bursts out, in prism radii, at the full ease
    float drift;      // and then drifts on, prism radii per second
    float lift;       // its direction: its face's own radial × how far out it sits, plus the face normal × this (1: the cube
                      // seen from its centre, every face filling its own sixth of the sphere)
    float aim;        // plus the hit's direction × this: the burst leans the way the prism was struck
    float sizeSpeed;  // per size level the smaller fly farther: distance × (1 + this × level), level capped at 6
    float sizeSpin;   // per size level the smaller spin faster: angle × min(1 + this × level, spinCap)
    float spinCap;    // the most any fragment's spin rate is multiplied — the explosion's cap, so nothing flickers
    float endSmall;   // the share of the life at which the smallest fragments are gone
    float endBig;     // the share at which the largest are
    float shrink;     // the share of its own life over which a fragment shrinks away to its centre
    float tau;        // the burst's time constant, seconds
    float levelRef;   // the size level counted as "the smallest" (the cut's finest, about 4)
    float speedGain;  // round 10: a harder hit bursts wider and leans more along it — distance and aim ×
    float speedRef;   // (1 + speedGain × (|velocity| / speedRef − 1)), the explosion's own spread growing with its speed
    float variety;    // round 12: how much one death differs from the next (PrismBurstFragmentVariety) — 0: two prisms struck
                      // alike play the same animation; the variety is per death, so within one the structure stays whole
};

// One death's character, hashed from its seed (the designer, 2026-10-17: "adjacent prisms with the nearly identical
// impact vectors and initial orientations don't look like they are playing the same animation, but spherical explosions
// should still look beautiful … a balance between not looking noisy and not looking like copies"). Every value is ONE
// per death, the same for all its fragments, so it adds no roughness between neighbours: it changes the whole explosion's
// shape, never its grain. At variety v: the spread × (1 ± v/2), the opening's time constant × (1 ± v/2), the spin rate
// × (1 ± 0.6 v), the tumble's plane turned about each face's normal by up to ± 0.4 π v, the lean tilted off the hit by up
// to v/2, and the cloud drawn out along a random axis into a smooth ellipsoid, distance × (1 + 0.6 v (1.5 cos² − 0.5))
// (a second Legendre harmonic: zero on average, so the sphere is reshaped, never thinned or grained).
struct PrismBurstFragmentCharacter
{
    float spread;   // distance multiplier
    float tau;      // time-constant multiplier
    float spin;     // spin-rate multiplier
    float turn;     // the tumble plane's turn about N, radians
    float3 tilt;    // added to the hit's unit direction, length up to v/2
    float3 egg;     // the ellipsoid's unit axis
    float stretch;  // 0.6 v: the most a distance is drawn out along it
};

// Triangle waves of the seed in [-1, 1], one per frequency: uniform when the seed is (SeedOf hashes the pose, so two
// neighbours' seeds are already unrelated), and gentle — a hash13 of the seed would multiply its rounding by about a
// hundred, so the same death would come out a different character on two GPUs (and on the lab's page against this).
float3 PrismBurstSeedWave(float seed, float3 frequency, float3 phase)
{
    return 1.0 - 4.0 * abs(frac(seed * frequency + phase) - 0.5);
}

PrismBurstFragmentCharacter PrismBurstFragmentVariety(float seed, float variety)
{
    float3 a = PrismBurstSeedWave(seed, float3(1.0, 2.0, 3.0), float3(0.13, 0.57, 0.31));
    float3 b = PrismBurstSeedWave(seed, float3(5.0, 7.0, 11.0), float3(0.71, 0.23, 0.89));
    float3 c = PrismBurstSeedWave(seed, float3(13.0, 17.0, 19.0), float3(0.47, 0.05, 0.63));
    float3 d = PrismBurstSeedWave(seed, float3(23.0, 29.0, 31.0), float3(0.37, 0.81, 0.19));
    float v = saturate(variety);
    PrismBurstFragmentCharacter ch;
    ch.spread = 1.0 + 0.5 * v * a.x;
    ch.tau = 1.0 + 0.5 * v * a.y;
    ch.spin = 1.0 + 0.6 * v * a.z;
    ch.turn = 0.4 * 3.14159265 * v * c.x;
    ch.tilt = b * (0.5 * v / max(length(b), 1.0));                          // inside the ball of radius v/2
    ch.egg = normalize(d + float3(1e-3, 0.0, 0.0));
    ch.stretch = 0.6 * v;
    return ch;
}

// The most a character stretches a distance: (1 + v/2)(1 + 0.6 v). PrismBurstFragmentReach multiplies by it.
float PrismBurstFragmentVarietyReach(float variety)
{
    float v = saturate(variety);
    return (1.0 + 0.5 * v) * (1.0 + 0.6 * v);
}

// How much harder than the gentlest a hit is (≥ 1): 1 at speedRef (the explosion's slowest debris), growing by speedGain
// per speedRef more. The explosion's own slide grows with its speed the same way (amount × spreadPerAmount).
float PrismBurstFragmentBoost(float flightSpeed, PrismBurstFragmentShape s)
{
    return 1.0 + s.speedGain * max(flightSpeed / max(s.speedRef, 1e-3) - 1.0, 0.0);
}

// When a fragment of size level is gone: the smallest first, the largest last — from far away the small ones simply
// go unseen first — a little jittered, never after 95% of the life.
float PrismBurstFragmentEnd(float level, float life, float jitter, PrismBurstFragmentShape s)
{
    float share = lerp(s.endBig, s.endSmall, saturate(max(level, 0.0) / max(s.levelRef, 1e-3)));
    return life * min(share * (1.0 + 0.08 * jitter), 0.95);
}

// A fragment's size as it leaves: 1 until the last shrink share of its own life, then down to 0 at its end.
float PrismBurstFragmentScale(float age, float end, float shrink)
{
    return 1.0 - smoothstep(end * (1.0 - saturate(shrink)), end, age);
}

// One fragment of the cut, in WORLD space: it bursts out along its own direction — its face's radial from the face
// centre weighted by how far out on the face it sits (outward: 0 at the centre, √2 at a corner, in face half-widths, so
// a face's centre leaves straight off it and its corners lean out; with lift 1 that is the cube seen from its own
// centre, every face filling its sixth of the sphere whatever the prism's proportions), lifted off the face, leaning
// along the hit — eased in (1 - e^(-t/tau)) and drifting on, the smaller a
// little farther; it tumbles about its own centre on two axes, its direction of travel and cross(velocity, N) — the
// explosion's velocity response — by the explosion's angle (amount × rotation, amount = the explosion's clamped
// speed × t), the smaller a little faster up to spinCap; and it shrinks away to its centre at its own end, the
// smallest first. velocity is the stamped flight (world): its direction leans the burst (aim) and sets the second spin
// axis, its speed boosts the distance and the lean (PrismBurstFragmentBoost). A continuous gradient noise at its centroid (noisePoint, object space × frequency, moved by the
// death's seed) varies its two spin angles (amplitude noise.x), its distance and its direction (noise.w), so
// neighbours move alike and never in step. The death's character (PrismBurstFragmentVariety, from the same seed) reshapes
// the whole cloud, its timing and its tumble once per death, so two prisms struck alike never play one animation.
// noise = (spin amplitude, frequency, seed, travel amplitude). The first
// frame is the prism: at age 0 every term is zero and the scale is 1. middle is the fragment's centroid in world space
// (`centroid` is an HLSL interpolation keyword and does not compile as a name).
void PrismBurstFragmentPose(float3 rest, float3 N, float3 middle, float3 faceCentre, float outward, float3 noisePoint, float level,
    float radius, float age, float life, float amount, float3 velocity, float rotation, float4 noise,
    PrismBurstFragmentShape s, out float3 pos, out float3 nrm)
{
    float3 q = noisePoint * noise.y + noise.z * float3(17.13, 31.71, 7.37);
#ifdef PRISM_BURST_FRAGMENT_NOISE_OFF
    float n1 = 0.0, n2 = 0.0, n3 = 0.0, n4 = 0.0, n5 = 0.0;   // negative control only: neighbours in perfect step
#else
    float n1 = PrismBurstGradientNoise(q);
    float n2 = PrismBurstGradientNoise(q + float3(41.3, 11.9, 23.7));
    float n3 = PrismBurstGradientNoise(q + float3(73.1, 5.3, 61.7));
    float n4 = PrismBurstGradientNoise(q + float3(13.9, 89.3, 37.1));
    float n5 = PrismBurstGradientNoise(q + float3(97.7, 53.9, 3.1));
#endif
    float3 inPlane = middle - faceCentre;
    inPlane = inPlane - N * dot(inPlane, N);
    float3 side = PrismBurstAxis(cross(N, inPlane), PrismBurstAxis(cross(N, float3(0.31, 0.83, 0.47)), float3(1.0, 0.0, 0.0)));
#ifdef PRISM_BURST_FRAGMENT_FACE_ONLY
    float3 radial = float3(0.0, 0.0, 0.0);   // negative control only: every fragment of a face leaves the same way
#else
    float3 radial = PrismBurstAxis(inPlane, float3(0.0, 0.0, 0.0)) * outward;
#endif
#ifdef PRISM_BURST_FRAGMENT_VELOCITY_OFF
    velocity = float3(0.0, 0.0, 0.0);   // negative control only: the pose ignores the hit (the flight is added after)
#endif
#ifdef PRISM_BURST_FRAGMENT_VARIETY_OFF
    PrismBurstFragmentCharacter ch = PrismBurstFragmentVariety(noise.z, 0.0);   // negative control only: every death alike
#else
    PrismBurstFragmentCharacter ch = PrismBurstFragmentVariety(noise.z, s.variety);
#endif
    float3 hit = PrismBurstAxis(velocity, float3(0.0, 0.0, 0.0));
    hit = dot(hit, hit) > 0.0 ? normalize(hit + ch.tilt) : hit;
    float boost = PrismBurstFragmentBoost(length(velocity), s);
    float3 dir = PrismBurstAxis(radial + N * s.lift + hit * (s.aim * boost) + (PrismBurstAxis(inPlane, side) * n4 + side * n5) * noise.w, N);

    float size = clamp(level, 0.0, 6.0);
    float dist = radius * (s.burst * (1.0 - exp(-max(age, 0.0) / max(s.tau * ch.tau, 1e-3))) + s.drift * max(age, 0.0))
               * (1.0 + s.sizeSpeed * size) * (1.0 + noise.w * n3) * boost * ch.spread
               * (1.0 + ch.stretch * (1.5 * dot(dir, ch.egg) * dot(dir, ch.egg) - 0.5));
    float spin = min((1.0 + s.sizeSpin * size) * ch.spin, s.spinCap);
    float a1 = amount * rotation * spin * (1.0 + noise.x * n1);
    float a2 = amount * rotation * spin * (1.0 + noise.x * n2);
    float3 axis2 = PrismBurstRotate(PrismBurstAxis(cross(velocity, N), side), N, ch.turn);
    float scale = PrismBurstFragmentScale(age, PrismBurstFragmentEnd(level, life, n4, s), s.shrink);

    pos = middle + dir * dist + PrismBurstRotate(PrismBurstRotate(rest - middle, dir, a1), axis2, a2) * scale;
    nrm = PrismBurstRotate(PrismBurstRotate(N, dir, a1), axis2, a2);
}

// How far, in WORLD units, a fragment can be from the prism's travelling centre (the flight is added after).
// Proved, not fitted: its centroid lies within R of the centre and every vertex within 2R of its centroid (the
// tumble keeps it, the shrink only pulls in); the travel is at most R × (burst + drift × life) × (1 + 6 sizeSpeed)
// × (1 + |travel noise|) × the boost at the flight's speed × the variety's most stretch. The CPU sizes the culling bounds by the same sum
// (PrismCollisionBurst.FragmentReach); the harness asserts every pose stays inside it.
float PrismBurstFragmentReach(float radius, float life, PrismBurstFragmentShape s, float travelNoise, float flightSpeed)
{
    return radius * (3.0 + (s.burst + s.drift * life) * (1.0 + 6.0 * s.sizeSpeed) * (1.0 + abs(travelNoise))
                         * PrismBurstFragmentBoost(flightSpeed, s) * PrismBurstFragmentVarietyReach(s.variety));
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
