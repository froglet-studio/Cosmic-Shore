// BlackHoleLens.hlsl — the black hole's GRAVITATIONAL LENS (Docs/BLACK_HOLE.md §5.1). Everything
// that decides where a light ray goes lives here as pure functions, so
// Tools/Shaders/verify_black_hole_lens.py can compile this file with clang++ and EXECUTE it;
// BlackHoleLens.shader is the URP plumbing around it.
//
// WHAT THE PLAYER SEES. A black hole is invisible; what is visible is everything behind it bent
// around it. For each pixel of the screen near the hole, we follow the light ray that reaches the
// camera BACKWARDS from the eye through Schwarzschild spacetime:
//   • a ray that falls through the horizon carries no light — that is the SHADOW, and it is not
//     the horizon's size: rays closer than the critical impact parameter b_c = (3√3/2) r_s ≈ 2.6 r_s
//     are all captured, so the black disc on screen is ~2.6× the horizon;
//   • a ray that escapes left the hole's neighbourhood along a BENT direction, and the pixel shows
//     whatever the scene has in THAT direction — the background distorts, stars and prisms smear
//     into arcs, and a point directly behind the hole becomes an Einstein ring.
// There is NO painted accretion disc. What orbits and spirals into the hole is the real mass the
// gravity field moves (BlackHoleGravityField) and the warp bends (PrismGravityWarp.hlsl); a
// synthetic disc — a hot, Doppler-shifted, lensed sheet of light — was built and REMOVED
// (2026-10-07): it read as a disc slicing through the hole, not as the hole.
//
// THE EQUATION. Null geodesics in the Schwarzschild metric obey the Binet equation
//     u'' + u = (3/2) r_s u²,        u = 1/r,
// in the plane of the orbit. Integrating it in 3D is done with the classic equivalent: in units of
// r_s, a "particle" moving under the fictitious force
//     x'' = −(3/2) h² x / |x|⁵,      h = |x × x'| (conserved),
// traces EXACTLY the photon's path (the force is central, so h is conserved and the orbit's shape
// satisfies the Binet equation). That is the same family of approximation the gravity field uses
// for matter (BlackHolePhysics: the Paczyński–Wiita pseudo-potential), applied to light. The
// harness holds the two numbers that make it a black hole and not a lens: the capture threshold is
// b_c = 2.598 r_s, and a ray passing far out at b is deflected by 2 r_s / b (Einstein's 4GM/c²b).
//
// UNITS. Everything inside the trace is in units of the horizon radius r_s, centred on the hole.
// The shader converts world → hole units on the way in and back to a world direction on the way
// out, so the same trace serves a strength-1 hole and a strength-100 one.

#ifndef BLACK_HOLE_LENS_INCLUDED
#define BLACK_HOLE_LENS_INCLUDED

// The loop's compile-time bound (GPU loops want one); the material's step budget is a runtime
// value at or below it.
#ifndef BLACK_HOLE_LENS_MAX_STEPS
#define BLACK_HOLE_LENS_MAX_STEPS 192
#endif

// Spatial step as a fraction of the current radius: fine near the photon sphere, coarse far out,
// where the path is nearly straight. It is also the harness's negative control: blown up to 3 the
// photon sphere is integrated so coarsely that the capture threshold misses b_c.
#ifndef BLACK_HOLE_LENS_STEP_FRACTION
#define BLACK_HOLE_LENS_STEP_FRACTION 0.08
#endif

// Where a ray from x0 (hole units) along unit d first enters the lens sphere of radius lensR, as a
// distance along the ray. −1 when the ray never comes within lensR, 0 when x0 is already inside.
float BlackHoleLensEntry(float3 x0, float3 d, float lensR)
{
    float bProj = dot(x0, d);
    float c = dot(x0, x0) - lensR * lensR;
    if (c <= 0.0) return 0.0;                     // the eye is inside the lens
    float disc = bProj * bProj - c;
    if (disc < 0.0) return -1.0;                  // passes outside the lens entirely
    float t = -bProj - sqrt(disc);
    return t >= 0.0 ? t : -1.0;                   // the lens is behind the eye
}

// The fictitious force whose orbits are null geodesics: x'' = −(3/2) h² x / |x|⁵ (units r_s).
// Guarded at the centre, where a ray that got there is already captured.
float3 BlackHoleLensAccel(float3 x, float h2)
{
    float r2 = max(dot(x, x), 1e-8);
    float r = sqrt(r2);
    return x * (-1.5 * h2 / (r2 * r2 * r));
}

// Trace one ray backwards from the eye. x0: the eye in hole units; d: unit view direction.
// Returns (by out): the escaping direction (unit, world-aligned) and whether it escaped (1) or
// fell through the horizon (0) — the shadow.
void BlackHoleLensTrace(float3 x0, float3 d, float lensR, int maxSteps, out float3 outDir, out float escaped)
{
    outDir = d;
    escaped = 1.0;

    float tEntry = BlackHoleLensEntry(x0, d, lensR);
    if (tEntry < 0.0)
        return;                                   // never near the hole: unbent

    float3 x = x0 + d * tEntry;
    float3 v = d;
    float3 hv = cross(x, v);
    float h2 = dot(hv, hv);
    float3 accel = BlackHoleLensAccel(x, h2);

    for (int i = 0; i < BLACK_HOLE_LENS_MAX_STEPS; i++)
    {
        if (i >= maxSteps) break;

        float r = length(x);
        if (r < 1.0)
        {
            escaped = 0.0;                        // through the horizon: no light from here
            return;
        }
        if (r > lensR && dot(x, v) > 0.0)
            break;                                // left the lens moving outward: escaped

        // Step a fixed fraction of the radius in SPACE (the fictitious speed varies near the hole).
        // Velocity Verlet — second order, one force evaluation per step (the new point's
        // acceleration is carried into the next step). First-order Euler at this step size put
        // the shadow's edge 2% inside b_c; Verlet holds it (verify_black_hole_lens.py, test 2).
        float ds = max(BLACK_HOLE_LENS_STEP_FRACTION * r, 0.005) / max(length(v), 1e-4);
        float3 vHalf = v + accel * (0.5 * ds);
        x = x + vHalf * ds;
        float3 accelNew = BlackHoleLensAccel(x, h2);
        v = vHalf + accelNew * (0.5 * ds);
        accel = accelNew;
    }
    outDir = normalize(v);
}

// Fade the bending to zero toward the lens's edge. Light passing at impact parameter b is really
// deflected by ~2/b rad at any distance — it never reaches zero — so a lens of finite radius
// would draw a visible seam where it stops. The bend is blended back to the straight ray over the
// outer part of the lens (b from fadeStart·lensR to lensR); inside it the trace is exact.
float3 BlackHoleLensFadeDir(float3 d, float3 bent, float b, float lensR, float fadeStart)
{
    float w = 1.0 - smoothstep(fadeStart * lensR, lensR, b);
    float3 m = d + (bent - d) * w;
    float l = length(m);
    return l > 1e-6 ? m / l : d;
}

#endif // BLACK_HOLE_LENS_INCLUDED
