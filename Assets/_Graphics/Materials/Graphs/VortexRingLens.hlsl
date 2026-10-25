// VortexRingLens.hlsl — the pure maths of the vortex ring's lens (Docs/BLACK_HOLE.md §5.3): a subtle doughnut of
// bent light round the ring's core instead of a sphere round a point. Included by VortexRingLens.shader; run on the
// CPU by Tools/Shaders/verify_vortex_ring_lens.py (nearest point against brute force, the profile, continuity, the
// foreground skip and a negative control), so keep it free of texture reads and URP includes.
//
// SPACE. Every position is in SCREEN-HEIGHT units, the black hole lens's frame: p = (uv − 0.5)·(aspect, 1), +y up.
//
// THE RING ON SCREEN. A circle seen through any perspective camera (the Pistol's off-axis seat lens included) is an
// ellipse while the whole ring is in front of the camera. VortexRingLens.ScreenRing works that ellipse out EXACTLY on
// the CPU from the camera's real matrices and hands over its centre, its semi-axes a ≥ b ≥ 0 and its major
// direction. Seen edge-on (the Pistol's side view: the ring's axis lies in the picture) b falls to 0 and the ellipse
// is the segment [−a, a] — handled as such, so the ring never vanishes.
//
// THE BEND. Each pixel finds the nearest point q on that core curve. Its distance d, in units of the tube's screen
// width σ, gives u = d/σ, and the sample is pulled toward the core by σ·bend·P(u), P(u) = u·e^(−u²/2)·(1 − (u/3)²)²:
// the smooth well's profile (BlackHoleLens.hlsl, kind 3) laid along a curve instead of round a point, cut smoothly to
// zero at u = 3. A pull toward the core magnifies what lies behind the tube, the look of a clear glass rod. Where the
// ring is nearly edge-on the doughnut's hole is narrower than the bend's reach and the nearest point flips across the
// major axis; the bend is faded to zero on that line (the medial axis) so it never creases. A faint rim lights the
// tube's edge (u ≈ 1/0.6, the tube's radius) in the ring's tint, and a slow ripple runs across the tube (the swirl).
//
// WHAT IS NOT BENT. A pixel whose opaque depth is in front of the core point by more than the ring's margin is
// skipped for that ring: a vessel between you and the ring is drawn unbent over it. No shadow, no singularity.

#ifndef COSMIC_SHORE_VORTEX_RING_LENS_INCLUDED
#define COSMIC_SHORE_VORTEX_RING_LENS_INCLUDED

// The bank: the nearest rings first (VortexRingLens.MaxRings must match).
#define VORTEX_RING_LENS_MAX_RINGS 8
// The bend reaches this many σ from the core and is exactly zero beyond.
#define VORTEX_RING_LENS_REACH 3.0
// The rim sits at the tube's radius: σ is 0.6 of the tube (VortexRingLens.SigmaPerTube), so u = 1/0.6.
#define VORTEX_RING_LENS_RIM_AT 1.6667
#define VORTEX_RING_LENS_RIM_WIDTH 0.4
// The swirl's ripple: radians of phase per σ across the tube.
#define VORTEX_RING_LENS_SWIRL_K 2.5
// The bend fades to zero over this many σ either side of the doughnut's medial axis.
#define VORTEX_RING_LENS_MEDIAL_FADE 1.0
// Bisection steps of the nearest-point search (Eberly's robust distance to an ellipse).
#define VORTEX_RING_LENS_BISECT 32
// Thinner than this share of a, the ellipse is treated as its major-axis segment (seen edge-on).
#define VORTEX_RING_LENS_SEGMENT 0.002

// The radial profile of the bend: u·e^(−u²/2), cut smoothly (C1) to zero at the reach.
float VortexRingLensProfile(float u)
{
    if (u >= VORTEX_RING_LENS_REACH) return 0.0;
    float t = u / VORTEX_RING_LENS_REACH;
    float w = 1.0 - t * t;
    return u * exp(-0.5 * u * u) * w * w;
}

// The rim: a soft band at the tube's edge, zero on the core and beyond the reach.
float VortexRingLensRim(float u)
{
    if (u >= VORTEX_RING_LENS_REACH) return 0.0;
    float t = u / VORTEX_RING_LENS_REACH;
    float w = 1.0 - t * t;
    float k = (u - VORTEX_RING_LENS_RIM_AT) / VORTEX_RING_LENS_RIM_WIDTH;
    return exp(-k * k) * w * w * min(u, 1.0);
}

// The point of the ellipse x²/a² + y²/b² = 1 (a ≥ b ≥ 0) nearest to (y0, y1), both ≥ 0 (the first quadrant; the
// caller reflects). Eberly's robust bisection, "Distance from a Point to an Ellipse" (2013). Below
// VORTEX_RING_LENS_SEGMENT·a the ellipse is its major-axis segment.
float2 VortexRingLensNearestQuadrant(float y0, float y1, float a, float b)
{
    if (b <= a * VORTEX_RING_LENS_SEGMENT) return float2(min(y0, a), 0.0);
    if (y1 > 0.0)
    {
        if (y0 > 0.0)
        {
            float z0 = y0 / a;
            float z1 = y1 / b;
            float g = z0 * z0 + z1 * z1 - 1.0;
            if (g == 0.0) return float2(y0, y1);
            float r0 = (a / b) * (a / b);
            float n0 = r0 * z0;
            float s0 = z1 - 1.0;
            float s1 = g < 0.0 ? 0.0 : sqrt(n0 * n0 + z1 * z1) - 1.0;
            float s = 0.0;
            for (int i = 0; i < VORTEX_RING_LENS_BISECT; i++)
            {
                s = 0.5 * (s0 + s1);
                float q0 = n0 / (s + r0);
                float q1 = z1 / (s + 1.0);
                float gs = q0 * q0 + q1 * q1 - 1.0;
                if (gs > 0.0) s0 = s; else s1 = s;
            }
            return float2(r0 * y0 / (s + r0), y1 / (s + 1.0));
        }
        return float2(0.0, b);
    }
    float numer0 = a * y0;
    float denom0 = a * a - b * b;
    if (numer0 < denom0)
    {
        float x = numer0 / denom0;
        return float2(a * x, b * sqrt(max(0.0, 1.0 - x * x)));
    }
    return float2(a, 0.0);
}

// The nearest point in the ellipse's own frame (x along the major axis), any quadrant.
float2 VortexRingLensNearest(float2 l, float a, float b)
{
    float2 q = VortexRingLensNearestQuadrant(abs(l.x), abs(l.y), a, b);
    return float2(l.x < 0.0 ? -q.x : q.x, l.y < 0.0 ? -q.y : q.y);
}

// One ring at the pixel p (screen-height units), its opaque depth sceneZ (eye units). Adds its pull to disp and its
// rim to glow. The rows, filled per camera by VortexRingLens.ScreenRing:
//   rc  xy the ellipse's centre, z a (major semi-axis), w b (minor semi-axis)
//   ra  xy the major axis (unit), z the cull reach (3·σ at the ring's nearest point), w 1 when live
//   rz  xyz the inverse depth of the core point at angle τ: x + y·cos τ + z·sin τ (the sin τ term faded out as the
//       ring turns edge-on); w σ per unit of inverse depth
//   rl  x the bend (material bend × strength × near fade), y the rim (material rim × strength × fade), z swirl, w phase
//   rt  rgb the rim's tint, w the foreground margin (eye units)
void VortexRingLensRing(float2 p, float sceneZ, float4 rc, float4 ra, float4 rz, float4 rl, float4 rt,
                        inout float2 disp, inout float3 glow)
{
    float a = rc.z;
    float b = rc.w;
    float2 e = ra.xy;
    float2 f = float2(-e.y, e.x);
    float2 d0 = p - rc.xy;
    float2 l = float2(dot(d0, e), dot(d0, f));
    if (abs(l.x) > a + ra.z || abs(l.y) > b + ra.z) return;

    float2 q = VortexRingLensNearest(l, a, b);
    float ct = clamp(q.x / a, -1.0, 1.0);
    float st = sqrt(max(0.0, 1.0 - ct * ct)) * (l.y < 0.0 ? -1.0 : 1.0);
    // Seen nearly edge-on (the ellipse thinner than the tube) the near and far halves of the ring overlap on screen
    // and which one a pixel sees is undefined: their sin τ term fades out and both read the mean depth.
    float open = smoothstep(0.0, rz.w * rz.x, b);
    float invZ = rz.x + rz.y * ct + rz.z * st * open;
    if (invZ <= 0.0) return;
    if (sceneZ < 1.0 / invZ - rt.w) return;   // an opaque thing in front of the ring is not bent

    float sigma = rz.w * invZ;
    float2 dl = l - q;
    float dist = sqrt(dot(dl, dl));
    float u = dist / sigma;
    if (u >= VORTEX_RING_LENS_REACH) return;

    // Inside the doughnut's hole the nearest point flips side on the medial axis (the major axis, out to the centres
    // of curvature of the tips): fade the bend to zero there so it never creases.
    float g = 1.0;
    if (b > a * VORTEX_RING_LENS_SEGMENT && l.x * l.x * b * b + l.y * l.y * a * a < a * a * b * b)
    {
        float m0 = (a * a - b * b) / a;
        float mx = max(abs(l.x) - m0, 0.0);
        float m = sqrt(mx * mx + l.y * l.y);
        g = smoothstep(0.0, sigma * VORTEX_RING_LENS_MEDIAL_FADE, m);
    }

    float2 dirL = dist > 0.0 ? dl / dist : float2(0.0, 0.0);
    float2 dir = dirL.x * e + dirL.y * f;
    float swirl = 1.0 + rl.z * sin(VORTEX_RING_LENS_SWIRL_K * u - rl.w);
    float amp = rl.x * sigma * VortexRingLensProfile(u) * swirl * g;
    disp -= dir * amp;
    glow += rt.rgb * (rl.y * VortexRingLensRim(u) * g);
}

// True when no ring changed the pixel: it keeps exactly what the camera drew.
bool VortexRingLensUntouched(float2 disp, float3 glow)
{
    return dot(disp, disp) < 1e-14 && max(glow.r, max(glow.g, glow.b)) < (1.0 / 1024.0);
}

// The copy's uv for a displaced sample; a ray bent off the screen shows the screen mirrored at its edge.
float2 VortexRingLensSampleUV(float2 p, float2 disp, float aspect)
{
    float2 suv = (p + disp) / float2(aspect, 1.0) + 0.5;
    return 1.0 - abs(1.0 - abs(suv));
}

#endif
