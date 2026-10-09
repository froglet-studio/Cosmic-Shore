// CrystalWormholeLens.hlsl — where light goes inside a crystal wormhole (Docs/CRYSTAL_WORMHOLE.md §2).
//
// THE IDEA. The warp field already says how big everything is: a pilot at a point where the field
// reads s is s times their own size, and so are their camera, their speed and their trail. Measured
// in the pilot's own lengths, a world length dx is dx / s. That is a METRIC (the "felt" geometry), and
// around a pole the sphere at distance r has felt radius F(r) = r / s(r). ThroatWarp shapes F into a
// NECK: smallest, and stationary, at the throat. (RadialWarp's s ∝ r makes F constant, an endless tube
// light winds round all the way. Simulated, that was a kaleidoscope.) Two necks glued at their throats
// are a wormhole, and light that follows the same felt geometry draws it with no surface anywhere.
//
// So the lens is not a picture painted on a sphere: it is the warp field's own optics. Light in the
// felt metric is light in a medium of refractive index n = 1/s, and a ray bends by
//
//     d(dir)/dl = ∇ln n − (dir·∇ln n) dir,   ∇ln n = −∇ln s
//
// (toward the pole, where s is small). Everything here traces that per pixel, through BOTH poles'
// fields (the field is their product, s = Π s_i^a_i, so ln s is a sum and so is the bend).
//
// THE THROATS. The ball of radius `throat` around each pole is not part of space: its surface is glued
// to the partner's. A ray (or a vessel: CrystalWormhole.cs uses the same rule) that reaches one throat
// at x carries on from the other throat's ANTIPODAL point,
//
//     x' = poleA + poleB − x      (a point reflection through the pair's midpoint)
//
// turned 180° about the throat normal (CrystalWormholeTurnThrough). That is the isometry of the two
// necks, so the glued geometry is smooth. The field reads the same on both sides, because the product
// field is symmetric under that reflection.
//
// WHAT IT LOOKS LIKE (Tools/Shaders/simulate_crystal_wormhole.py renders it). From far away the
// attractor is a crystal ball: the repulsor side's whole sky squeezed into a disc ~feltNeck across in
// impact parameter, with this side's sky wound round its edge as an Einstein ring. Both images compress
// into one ring (the rays that circle the neck), and there is no surface: the disc is just the set of
// rays that go through. Closer in, it opens into a tunnel converging on the far side. Nothing pops
// anywhere, because every ray moves continuously with the eye.
//
// Pure functions of their arguments; executed under clang++ by simulate_crystal_wormhole.py (--check is
// the gate). No URP symbol is referenced here.

#ifndef CRYSTAL_WORMHOLE_LENS_INCLUDED
#define CRYSTAL_WORMHOLE_LENS_INCLUDED

// Step = this fraction of the distance to the nearest pole (the tube is scale-free, so a step that
// scales with r is a constant angle of turn: ~0.15 rad here).
// Inside the neck the bend ramps to zero over this fraction of its radius (see CrystalWormholeDLnS).
#ifndef CRYSTAL_WORMHOLE_NECK_RAMP
#define CRYSTAL_WORMHOLE_NECK_RAMP 0.1
#endif

#ifndef CRYSTAL_WORMHOLE_STEP_FRACTION
#define CRYSTAL_WORMHOLE_STEP_FRACTION 0.2
#endif

struct CrystalWormholeField
{
    float3 poleA;          // the attractor's centre (world)
    float3 poleB;          // the repulsor's centre (world)
    float ampA;            // each pole's live warp amplitude × the field's eased weight (0 = flat)
    float ampB;
    float throat;          // radius of the glued spheres right now, world units (0 = closed)
    float neck;            // ThroatWarp.throatRadius: where the neck is narrowest (world units)
    float feltNeck;        // ThroatWarp's felt radius there: neck / throatScale
    float taperIn;         // ThroatWarp's taper: the field is exactly flat beyond taperOut
    float taperOut;
    float3 lensCentre;     // the lens sphere: past it space is flat
    float lensRadius;
};

// ThroatWarp's law (ThroatWarp.cs carries the same lines). Felt radius of the sphere at distance r:
//   F(r) = r + λ·e^(−u − u²/2),  u = (r − neck)/λ,  λ = feltNeck − neck     (r ≥ neck)
// and inside the neck the scale holds at its floor, s = neck / feltNeck (flat: nothing outside ever
// gets in — a throat is glued — so only an eye just carried through is ever in there, and it must see
// straight out, not spiral round a centre that is not part of space).
// F is smallest — and STATIONARY — at the neck (F' = 1 − (1+u)e^(...) = 0 there): a catenoid-like
// throat, so light that skims it winds only logarithmically near one ring (an Ellis wormhole's optics)
// instead of all along a tube. s = r / F, eased to exactly 1 between taperIn and taperOut:
//   ln s = W(r)·(ln r − ln F),  W = 1 − smootherstep(taperIn, taperOut, r).
float CrystalWormholeTaper(float r, CrystalWormholeField f, out float slope)
{
    float span = max(f.taperOut - f.taperIn, 1e-3);
    float t = clamp((r - f.taperIn) / span, 0.0, 1.0);
    slope = -30.0 * t * t * (t - 1.0) * (t - 1.0) / span;
    return 1.0 - t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

// ln s at distance r from one pole (amplitude 1).
float CrystalWormholeLnS(float r, CrystalWormholeField f)
{
    r = max(r, 1e-3);
    float lambda = max(f.feltNeck - f.neck, 1e-3);
    float lnS = log(max(f.neck, 1e-3)) - log(max(f.feltNeck, 1e-3));
    if (r > f.neck)
    {
        float u = (r - f.neck) / lambda;
        lnS = log(r) - log(r + lambda * exp(-u - 0.5 * u * u));
    }
    float slope;
    return CrystalWormholeTaper(r, f, slope) * lnS;
}

// d(ln s)/dr at distance r from one pole (amplitude 1).
float CrystalWormholeDLnS(float r, CrystalWormholeField f)
{
    r = max(r, 1e-3);
    // Inside the neck: flat, but reached through a thin ramp so the bend is CONTINUOUS at the sphere. A
    // ray glued onto the far sphere starts exactly on it, and float rounding puts its first sample on
    // either side; a bend that jumped there sent neighbouring pixels down two paths ~2° apart (speckle).
    if (r <= f.neck)
    {
        float ramp = clamp((r - f.neck * (1.0 - CRYSTAL_WORMHOLE_NECK_RAMP)) / (f.neck * CRYSTAL_WORMHOLE_NECK_RAMP), 0.0, 1.0);
        float slope0;
        return ramp * ramp * (3.0 - 2.0 * ramp) * CrystalWormholeTaper(f.neck, f, slope0) / max(f.neck, 1e-3);
    }
    float lambda = max(f.feltNeck - f.neck, 1e-3);
    float u = (r - f.neck) / lambda;
    float e = exp(-u - 0.5 * u * u);
    float felt = r + lambda * e;
    float slope;
    float w = CrystalWormholeTaper(r, f, slope);
    float d = w * (1.0 / r - (1.0 - (1.0 + u) * e) / felt);
    if (slope != 0.0) d += slope * (log(r) - log(felt));   // only inside the taper band
    return d;
}

// ∇ln n = −∇ln s: the pull on a ray, toward each pole in proportion to its amplitude.
float3 CrystalWormholeBend(float3 p, CrystalWormholeField f)
{
    float3 a = p - f.poleA;
    float3 b = p - f.poleB;
    float ra = max(length(a), 1e-3);
    float rb = max(length(b), 1e-3);
    return -(a * (f.ampA * CrystalWormholeDLnS(ra, f) / ra) + b * (f.ampB * CrystalWormholeDLnS(rb, f) / rb));
}

// The ray's turn rate vector for direction d at p: the part of the bend across the ray.
float3 CrystalWormholeTurn(float3 p, float3 d, CrystalWormholeField f)
{
    float3 g = CrystalWormholeBend(p, f);
    return g - d * dot(g, d);
}

// Does the step from `from` to `to` ENTER the ball (start outside, reach it)? t = where, 0..1.
bool CrystalWormholeEnters(float3 from, float3 to, float3 centre, float radius, out float t)
{
    t = 0.0;
    if (!(radius > 0.0)) return false;
    float3 o = from - centre;
    float c = dot(o, o) - radius * radius;
    if (c <= 0.0) return false;                     // started inside: never glued (fly out first)
    float3 s = to - from;
    float a = max(dot(s, s), 1e-12);
    float b = dot(o, s);
    float disc = b * b - a * c;
    if (disc < 0.0 || b >= 0.0) return false;       // misses, or moving away
    t = (-b - sqrt(disc)) / a;
    return t <= 1.0;
}

// Where a point on one throat comes out of the other: the ANTIPODAL point of the partner's sphere —
// the point reflection through the pair's midpoint.
float3 CrystalWormholeGlue(float3 p, CrystalWormholeField f)
{
    return f.poleA + f.poleB - p;
}

// What a direction becomes going through, at the throat point whose outward normal is n: turned 180°
// about n. That keeps its radial part (falling in becomes climbing out, since the far sphere's outward
// normal at the antipode is −n) and reverses its sideways part, which is exactly how the antipodal map
// carries the sphere's own tangent vectors — so the glued felt geometry is SMOOTH there (an isometry),
// a ray's angular momentum about its pole carries straight through, and both sides' images compress
// into one continuous ring instead of meeting at an edge. Vessels go through by the same rule
// (CrystalWormhole.Through), so what a pilot sees through a throat is where they come out.
float3 CrystalWormholeTurnThrough(float3 d, float3 n)
{
    return n * (2.0 * dot(d, n)) - d;
}

// One RK4 step of length h for the ray at (x, d): the position and direction after it.
void CrystalWormholeStep(float3 x, float3 d, float h, CrystalWormholeField f, out float3 xn, out float3 dn)
{
    float3 k1 = CrystalWormholeTurn(x, d, f);
    float3 d2 = normalize(d + k1 * (0.5 * h));
    float3 k2 = CrystalWormholeTurn(x + d * (0.5 * h), d2, f);
    float3 d3 = normalize(d + k2 * (0.5 * h));
    float3 k3 = CrystalWormholeTurn(x + d2 * (0.5 * h), d3, f);
    float3 d4 = normalize(d + k3 * h);
    float3 k4 = CrystalWormholeTurn(x + d3 * h, d4, f);
    xn = x + (d + (d2 + d3) * 2.0 + d4) * (h / 6.0);
    dn = normalize(d + (k1 + (k2 + k3) * 2.0 + k4) * (h / 6.0));
}

// Trace the ray from `eye` along unit `dir` through the field until it leaves the lens sphere.
// Out: where (outPos) and which way (outDir) the ray leaves; how many throats it passed (crossings);
// which mouth it last came out of (lastExit: 0 none, 1 the attractor's, 2 the repulsor's), and where
// and which way it came out of it (exitPos, exitDir — what NEAR mass on the far side is seen along); how
// far it had travelled when it first went into a throat (firstCross, world units; −1 never); and whether
// it ran out of steps (exhausted = 1: a ray circling a neck near the ring — drawn from wherever it got
// to, which is inside the band where the sky is wound up anyway).
// A ray that never enters the lens sphere comes back unchanged (outPos = eye, outDir = dir).
void CrystalWormholeTrace(float3 eye, float3 dir, CrystalWormholeField f, int steps,
                          out float3 outPos, out float3 outDir, out float crossings, out float lastExit,
                          out float3 exitPos, out float3 exitDir, out float firstCross, out float exhausted)
{
    outPos = eye;
    outDir = dir;
    exitPos = eye;
    exitDir = dir;
    crossings = 0.0;
    lastExit = 0.0;
    firstCross = -1.0;
    exhausted = 0.0;

    // Start at the lens sphere if the eye is outside it (straight until then: the field is flat there).
    float3 rel = eye - f.lensCentre;
    float b = dot(rel, dir);
    float c = dot(rel, rel) - f.lensRadius * f.lensRadius;
    float3 x = eye;
    float travelled = 0.0;
    if (c > 0.0)
    {
        float disc = b * b - c;
        if (disc <= 0.0 || b >= 0.0) return;      // never enters the lens: nothing bends it
        travelled = -b - sqrt(disc);
        x = eye + dir * travelled;
    }

    float3 d = dir;
    float minStep = 0.02 * max(f.neck, 1.0);
    float maxStep = 0.25 * f.lensRadius;
    exhausted = 1.0;
    for (int i = 0; i < steps; i++)
    {
        float ra = length(x - f.poleA);
        float rb = length(x - f.poleB);
        float h = clamp(CRYSTAL_WORMHOLE_STEP_FRACTION * min(ra, rb), minStep, maxStep);

        float3 xn, dn;
        CrystalWormholeStep(x, d, h, f, xn, dn);

        // A throat on the way: step EXACTLY to it (a truncated step is a kink, and a kink that moves
        // from pixel to pixel is noise), then carry on from the partner's antipodal point, turned through (CrystalWormholeTurnThrough).
        float t;
        bool intoA = CrystalWormholeEnters(x, xn, f.poleA, f.throat, t);
        bool intoB = !intoA && CrystalWormholeEnters(x, xn, f.poleB, f.throat, t);
        if (intoA || intoB)
        {
            float3 pole = intoA ? f.poleA : f.poleB;
            CrystalWormholeStep(x, d, h * t, f, xn, dn);
            float3 n = (xn - pole) / max(length(xn - pole), 1e-6);
            travelled += h * t;
            if (firstCross < 0.0) firstCross = travelled;
            x = CrystalWormholeGlue(pole + n * f.throat, f);
            d = CrystalWormholeTurnThrough(dn, n);
            exitPos = x;
            exitDir = d;
            crossings += 1.0;
            lastExit = intoA ? 2.0 : 1.0;
            continue;
        }

        travelled += h;
        x = xn;
        d = dn;
        float3 away = x - f.lensCentre;
        if (dot(away, away) > f.lensRadius * f.lensRadius && dot(away, d) > 0.0)
        {
            exhausted = 0.0;
            break;
        }
    }
    outPos = x;
    outDir = d;
}

// How far along the view ray the bending HAPPENS, for depth: the point where the straight ray from
// the eye and the line the traced ray leaves on come closest — the thin-lens picture of a ray that
// went straight, turned once, and went straight again — or, for a ray that went into a throat, the
// throat (whatever it shows came from the far side, however little it was turned). Mass nearer than
// this is in front of the bend and is drawn where it is; mass the bent ray finds must lie beyond it.
// Never nearer than `nearest` (the near field — the pilot's own hull — is never "behind" a bend);
// effectively infinite for a ray that was hardly turned and never went through.
float CrystalWormholeBendDistance(float3 eye, float3 dir, float3 outPos, float3 outDir, float firstCross,
                                  float nearest)
{
    float t = 1e9;
    float k = dot(dir, outDir);
    float denom = 1.0 - k * k;
    if (denom >= 1e-6)
    {
        float3 w = eye - outPos;
        t = (k * dot(outDir, w) - dot(dir, w)) / denom;
    }
    if (firstCross >= 0.0) t = min(t, firstCross);
    return max(t, nearest);
}

#endif // CRYSTAL_WORMHOLE_LENS_INCLUDED
