// BlackHoleLens.hlsl — the black and white holes as the player SEES them (Docs/BLACK_HOLE.md §5.1): the
// Vessel Studio's lens, ported line for line. The studio (Docs/Studios/StoatFlightStudio.html, `lensMat`)
// draws every hole in ONE screen-space pass: each hole adds a displacement to where the pixel samples the
// scene, and the pixel samples it once. This file holds that pass's per-hole step and its composite as pure
// functions; BlackHoleLens.shader is the URP plumbing around them, and Tools/Shaders/verify_black_hole_lens.py
// compiles this file with clang++ and runs it against the studio's own GLSL, extracted from the page.
//
// WHY ONE PASS. Unity used to draw one lens SPHERE per hole (30 horizons wide), each bending a copy of the
// scene taken before any lens. A Stoat pair's two spheres overlap, so the sphere drawn last painted over its
// partner (the black hole hid the white hole), and each sphere swapped in the skybox wherever a bent ray landed
// on something in front of the hole, which in lava lamp read as a large disc around the hole. Summing every
// hole into one displacement has neither: the order of the holes cannot matter, and nothing is swapped in.
//
// SCREEN UNITS. p is the pixel in SCREEN HEIGHTS from the screen's centre, y up: (uv − 0.5) · (aspect, 1).
// A hole's centre, its sizes and its bend are in the same units, worked out per camera on the CPU
// (BlackHoleLens.ScreenWell, the studio's setLensUniforms): an angle θ from the view axis lands at f·tan θ,
// f = 0.5 / tan(fov_y / 2).
//
// ONE HOLE (c, w, m: its three uniforms):
//   c  xy its centre on screen, z its depth along the view axis (world units), w 1 = live
//   w  x the kind: 1 a black hole, 2 a white hole, 3 a smooth attractor, 4 a smooth repulsor (the crystal pair)
//      y its horizon's angular radius r_c (a smooth well: its core's)
//      z a smooth well's lens strength A (< 1, so its image never folds)
//      w the Einstein term θ_E² · lens strength (θ_E = f·tan √(2 r_s / D))
//   m  x the lens reach (the bend fades out from lensFade · reach to reach), w the foreground margin (world units)
//
// A BLACK HOLE: a pure black shadow at kShadow · r_c (2.6, the photon-capture radius b_c = 2.598 r_s), every
// pixel pulled toward the hole by θ_E² / max(θ, shadow) — an Einstein ring where something sits straight behind
// it — and a thin warm photon ring (1, 0.8, 0.55) just outside the shadow.
// A WHITE HOLE: the same bend; inside its core (kCore · r_c) the light coming out of it: the bent scene
// dimmed by coreMix, plus a white-hot glow coreBright · (1 − t)², t = θ / core.
// A SMOOTH WELL: the graded lens A · r_c · u · e^(−u²/2), u = θ / r_c, toward the centre for an attractor and
// away from it for a repulsor. No horizon, shadow or ring.

#ifndef BLACK_HOLE_LENS_INCLUDED
#define BLACK_HOLE_LENS_INCLUDED

// The most holes one pass draws (the nearest to the camera). The studio draws 4; a lava-lamp race lays a pair
// per pilot, so Unity takes the nearest 8. BlackHoleLens.MaxWells mirrors it.
#define BLACK_HOLE_LENS_MAX_WELLS 8

// The photon ring's colour and the white core's glow colour, as the studio draws them.
#define BLACK_HOLE_LENS_RING_COLOUR float3(1.0, 0.8, 0.55)
#define BLACK_HOLE_LENS_CORE_COLOUR float3(1.0, 0.97, 0.9)

// One hole's step: what it adds to the pixel's displacement, shadow, white core and glow.
//   look   x kShadow (black hole shadow, × r_c), y kCore (white core, × r_c), z ring glow, w ring width (× shadow)
//   look2  x lens fade start (× reach), y white core brightness
//   tint   rgb the owner's domain colour, a how far (0 = the studio's black shadow and white core)
void BlackHoleLensWell(float2 p, float4 c, float4 w, float4 m, float4 look, float4 look2, float4 tint,
                       inout float2 disp, inout float shadow, inout float core, inout float coreGlow,
                       inout float3 glow, inout float3 shadowColour, inout float3 coreColour)
{
    float2 d = p - c.xy;
    float th = max(length(d), 1e-6);
    float2 dir = d / th;
    float type = w.x;
    float rc = max(w.y, 1e-6);
    if (type < 2.5)
    {
        float fade = 1.0 - smoothstep(look2.x * m.x, m.x, th);
        if (type < 1.5)
        {
            // a black hole: Schwarzschild shadow + Einstein lens + photon ring
            float sh = look.x * rc;
            if (th < sh)
            {
                shadow = 1.0;
                shadowColour = tint.rgb * saturate(tint.a);
            }
            disp -= dir * (w.w / max(th, sh)) * fade;
            float x = (th - sh * 1.03) / (max(look.w, 0.005) * sh);
            glow += BLACK_HOLE_LENS_RING_COLOUR * look.z * exp(-x * x);
        }
        else
        {
            // a white hole: the SAME bending; rays through the horizon draw the white-hot core
            float sh = look.y * rc;
            disp -= dir * (w.w / max(th, sh)) * fade;
            if (th < sh)
            {
                float t = th / sh;
                float g = look2.y * (1.0 - t) * (1.0 - t);
                core = 1.0;
                if (g > coreGlow)
                {
                    coreGlow = g;
                    coreColour = lerp(BLACK_HOLE_LENS_CORE_COLOUR, tint.rgb, saturate(tint.a));
                }
            }
        }
    }
    else
    {
        // a smooth well: the graded lens, signed, summed over the wells
        float u = th / rc;
        float a = w.z * rc * u * exp(-0.5 * u * u);
        disp += (type < 3.5 ? -1.0 : 1.0) * dir * a;
    }
}

// THE WAVES (Docs/BLACK_HOLE.md §5.2, the studio's wave loop, line for line). One wave's step (c, P, Q, ax: its
// uniforms, from BlackHoleWaves.ScreenWaves):
//   c   xy its centre on screen, z the kind (1 a light shell, 2 the gravitational wave), w 1 = live
//   a light shell:  P = (radius R·k, bend shellBend·env·k, width 28·k, glow 0.35·env)  — k = f / depth, screen units per u
//   the wave:       P = (k, -, gwAmp·mass·k, age u), Q = (gwSpeed, gwPeriod, gwRingdown, has the meeting axis), ax its
//                   direction on screen. The strain at screen distance th is the studio's gwStrainAt at r = th / k:
//                   e^(−u'/τ)·sin(2π u'/period)·min(1, 60/r) with u' = u − r/speed, sin² of the angle to the axis.
// A shell pushes the view outward across its front and glows (1, 0.96, 0.86); the wave pushes it along its strain, its
// crests (stretched) a little brighter (0.45, 0.6, 1.0) and its troughs (squeezed) a little darker — a visual aid, gwGlow.
void BlackHoleLensWave(float2 p, float4 c, float4 P, float4 Q, float4 ax, float gwGlow,
                       inout float2 disp, inout float3 glow, inout float squeeze)
{
    float2 d = p - c.xy;
    float th = max(length(d), 1e-5);
    float2 dir = d / th;
    if (c.z < 1.5)
    {
        float x = (th - P.x) / max(P.z, 1e-5);
        float g = exp(-x * x);
        disp += dir * P.y * x * g;
        glow += float3(1.0, 0.96, 0.86) * P.w * g;
    }
    else
    {
        float r = th / max(P.x, 1e-6);
        float u = P.w - r / max(Q.x, 1.0);
        if (u > 0.0 && u < 8.0 * Q.z)
        {
            float ca = dot(dir, ax.xy);
            float s2 = Q.w > 0.5 ? 1.0 - ca * ca : 1.0;   // silent along the meeting line, loudest across it
            float h = exp(-u / Q.z) * sin(6.2831853 * u / max(Q.y, 0.02)) * min(1.0, 60.0 / max(r, 1.0));
            disp += dir * P.z * h * s2;
            glow += float3(0.45, 0.6, 1.0) * 0.32 * gwGlow * max(0.0, h) * s2;
            squeeze += 0.14 * gwGlow * max(0.0, -h) * s2;
        }
    }
}

// A passing gravitational wave stretches the whole view one way and squeezes it the other (gwCam: the strain at the
// camera × gwShake): the pixel's own screen point before any lens.
float2 BlackHoleLensViewStretch(float2 p, float gwCam)
{
    return p + gwCam * float2(p.x, -p.y);
}

// Where the pixel samples the scene: p moved by the summed displacement, back to 0..1, mirrored at the
// screen's edges (a ray bent off the screen shows the screen reflected, as in the studio — never the sky).
float2 BlackHoleLensSampleUV(float2 p, float2 disp, float aspect)
{
    float2 s = p + disp;
    float2 suv = s / float2(aspect, 1.0) + 0.5;
    return 1.0 - abs(1.0 - abs(suv));
}

// The pixel's colour from the scene it sampled: the white core over it, the waves' squeeze, glow and a passing shell's
// wash, the photon ring added, the shadow last (the studio's order).
float3 BlackHoleLensComposite(float3 col, float shadow, float core, float coreGlow, float3 glow,
                              float coreMix, float3 shadowColour, float3 coreColour, float squeeze, float shellWash)
{
    if (core > 0.5) col = col * coreMix + coreColour * coreGlow;
    col = col * (1.0 - min(0.6, squeeze)) + glow + float3(1.0, 0.96, 0.86) * shellWash;
    if (shadow > 0.5) col = shadowColour;
    return col;
}

// Whether the holes and waves changed this pixel at all. Far from every hole the bend is exactly zero (past its reach)
// or far below a pixel (a smooth well's tail), the ring, core and crests are dark and no shell is washing over the
// camera: such a pixel is left exactly as the camera drew it rather than re-sampled from the copy. disp here is the
// pixel's whole move, the view's own stretch included.
bool BlackHoleLensUntouched(float2 disp, float shadow, float core, float3 glow, float squeeze, float shellWash)
{
    return dot(disp, disp) < 1e-14 && shadow < 0.5 && core < 0.5 && max(glow.x, max(glow.y, glow.z)) < 1.0 / 1024.0
        && squeeze < 1.0 / 1024.0 && shellWash < 1.0 / 1024.0;
}

#endif // BLACK_HOLE_LENS_INCLUDED
