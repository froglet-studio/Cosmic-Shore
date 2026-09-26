// PrismSlice.hlsl — the GPU side of the Rhino sword's SLICE death
// (Docs/PRISM_ANIMATION.md §4.10, R_VesselActions/RHINO_ENERGY_SWORD.md § "The slice").
//
// PURPOSE. A prism the Rhino's blade destroys does not burst into debris — it is CUT. The prism
// comes apart along one precise plane (the plane the blade swept through it), the two halves
// part like a sliced fruit — a beat together, then sliding apart and opening like a book — each
// half showing a hot, bright cut face, and then each half dissolves AWAY FROM THE CUT, the
// sliced face going first and the front marching out to the far end of the piece.
//
// WHY A STAMP AND NOT A GLOBAL (Docs/PRISM_ANIMATION.md §1, the admission test in the
// prism-morph skill). Everything about a slice is known at the instant of the hit: the plane,
// which side each half is on, how hard the blade was moving. Nothing about it depends on live
// data after that, so it is the clock-material law's ordinary shape — one stamp of initial
// conditions per half, the GPU runs the course off _PrismClock, one scheduled retirement — the
// same shape as the explosion debris it replaces for this one weapon. There is no per-frame CPU
// write to any piece, ever.
//
// WHY THE HIGH-POLY PRISM. Each half is drawn from HighPolyPrismMesh (the identical solid,
// subdivided), and that is load-bearing rather than decorative. A half is made by MAPPING the
// far side of the prism's own skin onto the cut plane (PrismSliceCut below) — the skin beyond
// the cut folds onto the plane and BECOMES the cut face. A vertex map can only be as exact as
// the triangles it moves: the one row of triangles that straddles the cut is where the fold is
// approximate, so the cut face's rim is exact to within ONE grid cell. On the authored 24-
// triangle prism a "cell" is half a face and the cap is a crude wedge; at subdivision 10 it is a
// tenth of a face, and what is left of it is covered by the rim clip + back-face fill below.
//
// ─── THE CUT ─────────────────────────────────────────────────────────────────────────────────
// The kept half is { x : dot(m, x) <= d } in OBJECT space, where (m, d) = _SlicePlane and m
// points OUT of the half (toward the other half). m is deliberately NOT unit length: the CPU
// stamps m = Mᵀ·n_w and d = n_w·(P − t) for the unit WORLD normal n_w, which makes
//     dot(m, x) − d  ==  the WORLD-space signed distance of the vertex from the cut,
// exactly, under the prism's non-uniform scale. So every depth below is in world units with no
// per-fragment matrix work, and the dissolve and the fresh-cut rim glow are the same size on a
// long thin slab as on a cube.
//
// A vertex on the kept side is left exactly where it is. A vertex beyond the cut is moved along
// the ray from C (_SliceCentre.xyz, a point strictly INSIDE the kept half) until it meets the
// plane:
//     p' = C + (p − C)·λ,   λ = −dist(C) / (dist(p) − dist(C))   ∈ (0, 1]
// This is a CENTRAL PROJECTION, and for a convex solid it is exact in the way that matters:
// because C is inside and the solid is convex, every ray from C through a far-side surface point
// crosses the plane INSIDE the cross-section, and every cross-section point is crossed by exactly
// one such ray. So the far skin maps BIJECTIVELY onto the true cut face — no overhang past the
// real edge (the failure of an orthogonal projection, which lets a slanted face's far part stick
// out beyond the cross-section), no holes, no fold. It is continuous where it meets the kept
// skin (λ → 1 as dist(p) → 0), so the half is WATERTIGHT: HighPolyPrismMesh duplicates each
// cube-edge vertex per face at identical positions, and identical inputs give identical outputs.
// And because an affine map preserves lines and ratios along them, projecting in object space
// is identical to projecting in world space — no need to leave the prism's own frame.
//
// THE CUT FACE'S NORMAL is the map's derivative, analytically and exactly: the image of the far
// region lies in the plane, so its normal is the plane's, m (transformed to world as a normal).
// Projection preserves orientation as seen from beyond the plane, so the cap's FRONT faces point
// out of the half — the verifier checks the winding rather than assuming it.
//
// THE RIM. A triangle that straddles the cut has its far corner moved onto the plane and its
// near corners left on the skin; linearly, its interior then cuts a small chamfer across the
// corner. Two things make the rim read as a knife-edge anyway:
//   • the fragment stage clips a straddling triangle's far part exactly (SliceClip: rest depth
//     < 0 on a triangle that is not wholly cap), so the SKIN ends precisely on the cut line —
//     the interpolated REST depth is exact within a triangle, so this cut is exact at any
//     subdivision;
//   • the sliver that clip opens between the skin's edge and the cap is at most one grid cell
//     wide and looks INTO the half, where every back face is shaded as cut flesh with the cut
//     face's own normal — so it is the same colour and the same shading as the cap beside it.
//   The FAR flag is what tells the two kinds of triangle apart: 1 on every far vertex, so a
//   triangle made entirely of far vertices interpolates to exactly 1 (it IS cap), while a
//   straddling one is < 1 everywhere except at its far corner.
//
// ─── THE MOTION (world space, rigid) ─────────────────────────────────────────────────────────
//   world = Pivot + Rot(axis, openAngle·R(u))·(rest − Pivot) + Away·sep·S(u) + drift·D(u)
// with u the age and S, R the exponential approaches 1 − e^(−u/τ) (τ from _SliceMotionTimes),
// D(u) = τ_drag·(1 − e^(−u/τ_drag)) the travelled time of a velocity under linear drag. A
// rotation plus translations is RIGID, so a half never stretches or shears — it is the same
// solid tumbling. Away = −(the cut's world normal), so the halves part along the cut; the
// rotation axis lies in the cut plane, perpendicular to the blade's travel, so the halves open
// like a book whose spine is the TRAILING edge of the cut. The CPU puts the pivot on that spine
// — the most-trailing point of the half, projected onto the plane — and that choice is what
// makes the opening provably non-interpenetrating: with every point of the half at or ahead of
// the spine, a rotation of up to 90° toward "away" can only carry points further from the other
// half, never into it (Tools/Shaders/verify_prism_slice.py test 7).
//
// ─── THE DISSOLVE ────────────────────────────────────────────────────────────────────────────
// Each fragment has a DEPTH FRACTION: how far into the half it sits from the cut face, 0 on the
// cut face (every cap vertex), 1 at the deepest point of the half (_SliceCentre.w, stamped by
// the CPU from the half's own corners). Mixed with a little world-scale value noise so the front
// is ragged rather than a ruled plane, it gives each fragment a place in the dissolve ORDER; a
// fragment is clipped once the front passes it. The cut face is at 0, so it goes first — "each
// piece dissolves from its sliced edge" — and the front marches to the far end. The front is a
// HARD edge with an ember band behind it, the house style (a dithered dissolve reads as the
// occlusion corridor's view effect, Docs/PRISM_ANIMATION.md §4.7). The window ends
// _SliceDissolveWindow.y of the life BEFORE retirement, so the retirement never beats the wipe
// (continuity of existence).
//
// ─── COST ────────────────────────────────────────────────────────────────────────────────────
// Per vertex: two dot products and, on the far side only, one divide; one Rodrigues rotation of
// the position and two of the normals; three exponentials shared per draw. Per fragment: one
// value-noise lookup (8 hashes) while a dissolve is running, nothing else that scales. No
// texture, no extra draw call — every half of every slice shares one mesh and one material, so a
// frame's slices are ONE instanced batch. Colliders: none, ever — these are pure render entities,
// and the prism's collider is gone the instant it was destroyed.

#ifndef PRISM_SLICE_INCLUDED
#define PRISM_SLICE_INCLUDED

// How far past 1 the dissolve progress runs by the end of its window, so the deepest fragment
// (depth fraction 1, noise 1) is provably clipped before retirement rather than on the boundary.
#ifndef PRISM_SLICE_DISSOLVE_OVERSHOOT
#define PRISM_SLICE_DISSOLVE_OVERSHOOT 0.02
#endif

// A triangle whose interpolated FAR flag is at least this is cap and is never rim-clipped. An
// all-far triangle interpolates to exactly 1; this threshold only has to sit above anything a
// straddling triangle produces away from its far corner.
#ifndef PRISM_SLICE_CAP_FLAG
#define PRISM_SLICE_CAP_FLAG 0.999
#endif

// Floor on the projection denominator. By contract C is strictly inside the kept half, so the
// true value is at least |dist(C)| > 0; the floor only matters for a broken stamp, and turns it
// into "leave the vertex where it is" (the rim clip then removes the far skin cleanly).
#ifndef PRISM_SLICE_MIN_DENOM
#define PRISM_SLICE_MIN_DENOM 1e-6
#endif

// Floor on every time constant, so a zero in a stamp is "instant", never a divide by zero.
#ifndef PRISM_SLICE_MIN_TIME
#define PRISM_SLICE_MIN_TIME 1e-4
#endif

// ─── THE CUT ─────────────────────────────────────────────────────────────────────────────────
// Position/Normal: object space. Plane: (m, d) as documented above. Centre: object space, strictly
// inside the kept half. Outputs: the mapped position and normal (object space; the normal is
// unnormalised — it is transformed and normalised by the caller), the vertex's REST signed depth
// into the half in world units (negative beyond the cut), and the FAR flag (1 = moved onto the
// cut face). An unstamped plane (all zero) reads every vertex as dist 0, i.e. kept: identity.
void PrismSliceCut(float3 Position, float3 Normal, float4 Plane, float3 Centre,
    out float3 OutPosition, out float3 OutNormal, out float RestDepth, out float Far)
{
    float dp = dot(Plane.xyz, Position) - Plane.w;     // > 0 : beyond the cut
    RestDepth = -dp;

    if (!(dp > 0.0))
    {
        OutPosition = Position;
        OutNormal = Normal;
        Far = 0.0;
        return;
    }

    float dc = dot(Plane.xyz, Centre) - Plane.w;       // < 0 : C is inside the kept half
    float denom = dp - dc;
    float lambda = denom > PRISM_SLICE_MIN_DENOM ? saturate(-dc / denom) : 1.0;

    OutPosition = Centre + (Position - Centre) * lambda;
    OutNormal = Plane.xyz;
    Far = 1.0;
}

// ─── THE MOTION ──────────────────────────────────────────────────────────────────────────────
// Rodrigues rotation of v about the UNIT axis by Angle radians. A zero axis (an unstamped piece)
// leaves v unchanged at angle 0, which is the only angle an unstamped piece is ever asked for.
float3 PrismSliceRotate(float3 v, float3 Axis, float Angle)
{
    float s, c;
    sincos(Angle, s, c);
    return v * c + cross(Axis, v) * s + Axis * (dot(Axis, v) * (1.0 - c));
}

// The three motion envelopes at age Age. Times = (separation τ, opening τ, drift drag τ, unused).
//   Separate — 1 − e^(−u/τ): the halves part fast and settle (the wedge of the blade).
//   Open     — 1 − e^(−u/τ): the book opens more slowly than it parts.
//   Drift    — τ·(1 − e^(−u/τ)) SECONDS: the distance a unit velocity covers under linear drag,
//              so the halves are carried along the swing and coast to a stop instead of flying
//              off forever. Multiplied by the stamped drift velocity, it is a distance.
void PrismSliceMotion(float Age, float4 Times, out float Separate, out float Open, out float Drift)
{
    float u = max(Age, 0.0);
    float ts = max(Times.x, PRISM_SLICE_MIN_TIME);
    float tr = max(Times.y, PRISM_SLICE_MIN_TIME);
    float td = max(Times.z, PRISM_SLICE_MIN_TIME);
    Separate = 1.0 - exp(-u / ts);
    Open = 1.0 - exp(-u / tr);
    Drift = td * (1.0 - exp(-u / td));
}

// A world point of the half at rest -> where it is now. Rigid by construction.
float3 PrismSliceMove(float3 WorldPoint, float3 Pivot, float3 Away, float SepDistance,
    float3 Axis, float Angle, float3 DriftVelocity,
    float Separate, float Drift)
{
    float3 r = PrismSliceRotate(WorldPoint - Pivot, Axis, Angle);
    return Pivot + r + Away * (SepDistance * Separate) + DriftVelocity * Drift;
}

// ─── THE DISSOLVE ────────────────────────────────────────────────────────────────────────────
float PrismSliceHash(float3 p)
{
    p = frac(p * 0.3183099 + float3(0.71, 0.113, 0.419));
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Trilinear value noise in [0, 1], C1 in every direction (smoothstep weights).
float PrismSliceNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = x - i;
    float3 w = f * f * (3.0 - 2.0 * f);

    float n000 = PrismSliceHash(i + float3(0.0, 0.0, 0.0));
    float n100 = PrismSliceHash(i + float3(1.0, 0.0, 0.0));
    float n010 = PrismSliceHash(i + float3(0.0, 1.0, 0.0));
    float n110 = PrismSliceHash(i + float3(1.0, 1.0, 0.0));
    float n001 = PrismSliceHash(i + float3(0.0, 0.0, 1.0));
    float n101 = PrismSliceHash(i + float3(1.0, 0.0, 1.0));
    float n011 = PrismSliceHash(i + float3(0.0, 1.0, 1.0));
    float n111 = PrismSliceHash(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, w.x);
    float nx10 = lerp(n010, n110, w.x);
    float nx01 = lerp(n001, n101, w.x);
    float nx11 = lerp(n011, n111, w.x);
    float nxy0 = lerp(nx00, nx10, w.y);
    float nxy1 = lerp(nx01, nx11, w.y);
    return lerp(nxy0, nxy1, w.z);
}

// How far the dissolve front has come, as a place in the dissolve ORDER: 0 before the window
// opens, rising linearly to 1 + overshoot at the window's end. Window = (start, end margin), both
// fractions of Duration. Duration <= 0 (unstamped) never dissolves.
float PrismSliceDissolveProgress(float Age, float Duration, float2 Window)
{
    if (!(Duration > 0.0))
        return 0.0;
    float a = saturate(Window.x) * Duration;
    float b = (1.0 - saturate(Window.y)) * Duration;
    float span = max(b - a, PRISM_SLICE_MIN_TIME);
    return saturate((Age - a) / span) * (1.0 + PRISM_SLICE_DISSOLVE_OVERSHOOT);
}

// A fragment's place in the dissolve order: its depth fraction into the half, roughened by noise.
// Both inputs are saturated, so the result is in [0, 1] and a progress past 1 clips everything.
float PrismSliceDissolveValue(float DepthFraction, float Noise, float NoiseAmount)
{
    return lerp(saturate(DepthFraction), saturate(Noise), saturate(NoiseAmount));
}

// Should the fragment be clipped? Two reasons, and only two:
//   • the precise cut — a straddling triangle's part beyond the plane (RestDepth < 0 on a
//     triangle that is not wholly cap);
//   • the dissolve — the front has passed this fragment's place in the order.
bool PrismSliceClipped(float RestDepth, float Far, float DissolveValue, float Progress)
{
    if (RestDepth < 0.0 && Far < PRISM_SLICE_CAP_FLAG)
        return true;
    return Progress > 0.0 && DissolveValue < Progress;
}

#endif // PRISM_SLICE_INCLUDED
