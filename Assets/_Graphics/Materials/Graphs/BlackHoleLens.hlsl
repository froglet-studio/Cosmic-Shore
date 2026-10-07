// BlackHoleLens.hlsl — the black hole's GRAVITATIONAL LENS and its ACCRETION DISC
// (Docs/BLACK_HOLE.md §5.1). Everything that decides where a light ray goes and what it picks up
// on the way lives here as pure functions, so Tools/Shaders/verify_black_hole_lens.py can compile
// this file with clang++ and EXECUTE it; BlackHoleLens.shader is the URP plumbing around it.
//
// WHAT THE PLAYER SEES. A black hole is invisible; what is visible is everything behind it bent
// around it. For each pixel of the screen near the hole, we follow the light ray that reaches the
// camera BACKWARDS from the eye through Schwarzschild spacetime:
//   • a ray that falls through the horizon carries no light — that is the SHADOW, and it is not
//     the horizon's size: rays closer than the critical impact parameter b_c = (3√3/2) r_s ≈ 2.6 r_s
//     are all captured, so the black disc on screen is ~2.6× the horizon;
//   • a ray that escapes left the hole's neighbourhood along a BENT direction, and the pixel shows
//     whatever the scene has in THAT direction — the background distorts, stars and prisms smear
//     into arcs, and a point directly behind the hole becomes an Einstein ring;
//   • a ray that crosses the ACCRETION DISC on its way picks up the disc's glow, every time it
//     crosses — which is why the far side of the disc appears ABOVE and BELOW the shadow (its light
//     bends over the top of the hole to reach you), and why a thin bright PHOTON RING hugs the
//     shadow (rays that orbit near 1.5 r_s cross the disc again and again).
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
// THE DISC. A thin disc in the plane perpendicular to the hole's spin axis, from the ISCO (3 r_s —
// matter inside it plunges) to an outer edge. Its temperature follows the Shakura–Sunyaev profile
//     T⁴ ∝ r⁻³ (1 − √(r_in / r)),
// zero at the inner edge, peaking at r ≈ 1.36 r_in, so the disc has a dark gap at the ISCO and a
// hot inner ring. Each crossing is shifted by the RELATIVISTIC DOPPLER factor of the gas (orbiting
// at v = √(r_s / (2(r − r_s))) — half light speed at the ISCO) and by gravitational redshift
// √(1 − r_s/r): the side coming toward you is bluer and far brighter (beaming, ∝ shift³), the side
// leaving is redder and dimmer — the asymmetric glow every real image of a black hole has. The gas
// is not uniform: differentially rotating (Keplerian, ω ∝ r^−3/2) spiral streaks of value noise, so
// the inner disc visibly shears faster than the outer. Its DENSITY is a live input: the disc a hole
// shows is the mass it has been FED (BlackHole.DiskFeed rises with every prism it consumes and
// decays), so a hole that is eating forms its disc in real time and a starving one fades.
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

// The Shakura–Sunyaev flux shape x⁻³(1 − x^−½) at its peak (x = 49/36), so the profile can be
// normalised to 1 at the hottest ring.
#define BLACK_HOLE_DISK_FLUX_PEAK 0.05665

// ---------------- Small helpers ----------------

float BlackHoleHash(float3 p)
{
    p = frac(p * 0.3183099 + float3(0.1, 0.2, 0.3));
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Value noise in [0, 1], smooth, tileable in nothing — the disc samples it on a ring so the angle
// wraps by construction (see BlackHoleDiskEmission).
float BlackHoleNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = BlackHoleHash(i + float3(0, 0, 0));
    float n100 = BlackHoleHash(i + float3(1, 0, 0));
    float n010 = BlackHoleHash(i + float3(0, 1, 0));
    float n110 = BlackHoleHash(i + float3(1, 1, 0));
    float n001 = BlackHoleHash(i + float3(0, 0, 1));
    float n101 = BlackHoleHash(i + float3(1, 0, 1));
    float n011 = BlackHoleHash(i + float3(0, 1, 1));
    float n111 = BlackHoleHash(i + float3(1, 1, 1));
    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
}

// Blackbody colour (linear RGB, max channel 1) for a temperature in kelvin — Tanner Helland's fit
// to the CIE blackbody locus, valid ~1,000–40,000 K, converted from its sRGB output to linear.
float3 BlackHoleBlackbody(float kelvin)
{
    float t = clamp(kelvin, 1000.0, 40000.0) / 100.0;
    float r, g, b;
    if (t <= 66.0)
    {
        r = 255.0;
        g = 99.4708025861 * log(t) - 161.1195681661;
        b = t <= 19.0 ? 0.0 : 138.5177312231 * log(t - 10.0) - 305.0447927307;
    }
    else
    {
        r = 329.698727446 * pow(t - 60.0, -0.1332047592);
        g = 288.1221695283 * pow(t - 60.0, -0.0755148492);
        b = 255.0;
    }
    float3 c = float3(saturate(r / 255.0), saturate(g / 255.0), saturate(b / 255.0));
    return float3(pow(c.x, 2.2), pow(c.y, 2.2), pow(c.z, 2.2));
}

// ---------------- The lens ----------------

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

// The disc's contribution at one crossing: premultiplied rgb (emission) and alpha (opacity).
//   xi      — the crossing point (hole units, in the disc plane)
//   rayDir  — the direction the traced ray was moving (camera → scene); light flows the other way
//   axis    — unit spin axis (the disc's normal; gas orbits prograde about it)
//   disk    — (inner, outer, density, brightness)
//   disk2   — (peak temperature K, Doppler strength 0..1, time, noise scale)
float4 BlackHoleDiskEmission(float3 xi, float3 rayDir, float3 axis, float4 disk, float4 disk2)
{
    float r = length(xi);
    float inner = disk.x, outer = disk.y;
    if (!(r > inner) || !(r < outer)) return float4(0.0, 0.0, 0.0, 0.0);

    // Shakura–Sunyaev temperature, normalised to the peak ring.
    float x = r / inner;
    float flux = max(pow(x, -3.0) * (1.0 - rsqrt(x)), 0.0) / BLACK_HOLE_DISK_FLUX_PEAK;
    float tLocal = disk2.x * pow(flux, 0.25);

    // Relativistic Doppler × gravitational redshift. Gas orbits at v = √(1 / (2(r − 1))) (units
    // c = r_s = 1), prograde about the axis; the photon reaching the camera travels −rayDir.
    float beta = min(sqrt(0.5 / max(r - 1.0, 1e-3)), 0.95);
    float3 orbit = cross(axis, xi);
    float orbitLen = length(orbit);
    float cosTheta = orbitLen > 1e-6 ? dot(orbit / orbitLen, -normalize(rayDir)) : 0.0;
    float gamma = rsqrt(1.0 - beta * beta);
    float doppler = 1.0 / (gamma * (1.0 - beta * cosTheta));
    float shift = doppler * sqrt(max(1.0 - 1.0 / r, 0.0));
    shift = lerp(1.0, shift, saturate(disk2.y));

    // The gas: differentially rotating spiral streaks. The angle is never taken with atan2 —
    // the point's own unit position in the disc plane is rotated by the Keplerian phase, so the
    // pattern wraps around the hole with no seam.
    float3 e1 = normalize(abs(axis.y) < 0.99 ? cross(axis, float3(0.0, 1.0, 0.0)) : cross(axis, float3(1.0, 0.0, 0.0)));
    float3 e2 = cross(axis, e1);
    float c0 = dot(xi, e1) / r;
    float s0 = dot(xi, e2) / r;
    float omega = sqrt(0.5 / (r * r * r));        // Keplerian, units c = r_s = 1
    float phase = omega * disk2.z + 2.2 * log(r); // shear + a logarithmic spiral twist
    float cp = cos(phase), sp = sin(phase);
    float rc = c0 * cp + s0 * sp;
    float rsn = s0 * cp - c0 * sp;
    float n = BlackHoleNoise(float3(rc * disk2.w * 3.0, rsn * disk2.w * 3.0, log(r) * disk2.w * 4.0));
    n = 0.55 * n + 0.45 * BlackHoleNoise(float3(rc * disk2.w * 7.0, rsn * disk2.w * 7.0, log(r) * disk2.w * 9.0 + 3.1));

    float edge = smoothstep(inner, inner * 1.15, r) * (1.0 - smoothstep(outer * 0.7, outer, r));
    float alpha = saturate(disk.z * (0.35 + 0.9 * n) * edge);

    float3 colour = BlackHoleBlackbody(tLocal * shift) * (disk.w * flux * shift * shift * shift);
    return float4(colour * alpha, alpha);
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
// Returns (by out): the escaping direction (unit, world-aligned), whether it escaped (1) or fell
// through the horizon (0), and the disc light it collected front-to-back (premultiplied rgb, a).
void BlackHoleLensTrace(float3 x0, float3 d, float lensR, int maxSteps, float3 axis,
    float4 disk, float4 disk2, out float3 outDir, out float escaped, out float4 diskLight)
{
    outDir = d;
    escaped = 1.0;
    diskLight = float4(0.0, 0.0, 0.0, 0.0);

    float tEntry = BlackHoleLensEntry(x0, d, lensR);
    if (tEntry < 0.0)
        return;                                   // never near the hole: unbent, nothing collected

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
        float3 xNew = x + vHalf * ds;
        float3 accelNew = BlackHoleLensAccel(xNew, h2);
        v = vHalf + accelNew * (0.5 * ds);
        accel = accelNew;

        // Did this segment cross the disc's plane? Each crossing adds the disc's light,
        // front-to-back, so the near side covers the far side and both show where it is thin.
        float a0 = dot(x, axis);
        float a1 = dot(xNew, axis);
        if ((a0 > 0.0) != (a1 > 0.0) && diskLight.a < 0.995)
        {
            float t = a0 / (a0 - a1);
            float3 xi = x + (xNew - x) * t;
            // e is premultiplied, so "over" for colour and alpha is one expression.
            float4 e = BlackHoleDiskEmission(xi, v, axis, disk, disk2);
            diskLight += (1.0 - diskLight.a) * e;
        }
        x = xNew;
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
