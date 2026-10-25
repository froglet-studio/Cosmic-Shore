// The sea lines behind a style-B window (Docs/MENU_REDESIGN_B.md §67, the canvas's Rev 12 Sea lines board): thin
// wave lines across the veil in three depth layers, each sliding sideways at its own slow speed (parallax). Every
// line's waves repeat every `loop` canvas units, so a layer that slides one loop per period never shows a seam.
// Pure maths on canvas-space position and time - no textures - and Tools/Build/lattice_preview.py compiles and
// renders it with clang.
#ifndef COSMIC_UI_SEA_LINES_INCLUDED
#define COSMIC_UI_SEA_LINES_INCLUDED

static const float SEA_TAU = 6.2831853;

// x mod 3 for whole numbers, negative ones included.
float SeaMod3(float x)
{
    return x - 3.0 * floor(x / 3.0);
}

// Coverage (0..1, before the layer alpha) of line `i` at p. Line i rests at y = i * spacing; its layer is i mod 3
// (0 far, 1 middle, 2 near). Its shape is two sines whose periods divide `loop`, with an amplitude and phase of its
// own, so neighbouring lines never move in step. `shift` is how far its layer has slid (canvas units).
float SeaLine(float2 p, float i, float spacing, float loop, float amplitude, float shift, float width)
{
    float k1 = SEA_TAU / loop;
    float k2 = 3.0 * k1;
    float a1 = amplitude * (1.0 + 0.45 * SeaMod3(i * 7.0));
    float a2 = amplitude * (0.45 + 0.22 * SeaMod3(i * 5.0));
    float x = p.x + shift;
    float y = i * spacing + a1 * sin(k1 * x + i * 0.9) + a2 * sin(k2 * x + i * 1.7);
    float slope = a1 * k1 * cos(k1 * x + i * 0.9) + a2 * k2 * cos(k2 * x + i * 1.7);
    // Distance across the line, not straight down: a steep stretch keeps the same thickness as a flat one.
    float d = abs(p.y - y) / sqrt(1.0 + slope * slope);
    return 1.0 - smoothstep(0.0, width, d);
}

// How far a layer has slid at time t: one loop per `seconds`, 0 holds it still.
float SeaShift(float t, float seconds, float loop)
{
    return seconds > 0.0 ? loop * frac(t / seconds) : 0.0;
}

// The three layers at p as alpha. The tallest wave stays inside half a spacing, so only the line whose rest is
// nearest p and its two neighbours can reach it.
float SeaLines(float2 p, float t, float spacing, float loop, float amplitude, float3 alphas, float3 widths,
               float3 seconds)
{
    float shiftFar = SeaShift(t, seconds.x, loop);
    float shiftMid = SeaShift(t, seconds.y, loop);
    float shiftNear = SeaShift(t, seconds.z, loop);
    float nearest = floor(p.y / spacing + 0.5);
    float sum = 0.0;
    for (int k = -1; k <= 1; k++)
    {
        float i = nearest + k;
        float layer = SeaMod3(i);
        float shift = layer < 0.5 ? shiftFar : (layer < 1.5 ? shiftMid : shiftNear);
        float width = layer < 0.5 ? widths.x : (layer < 1.5 ? widths.y : widths.z);
        float alpha = layer < 0.5 ? alphas.x : (layer < 1.5 ? alphas.y : alphas.z);
        sum += SeaLine(p, i, spacing, loop, amplitude, shift, width) * alpha;
    }
    return saturate(sum);
}

// The fade at the window's top and bottom edges: v is the rect's 0..1 height.
float SeaEdgeFade(float v, float fadeIn, float fadeOut)
{
    return smoothstep(0.0, fadeIn, v) * (1.0 - smoothstep(1.0 - fadeOut, 1.0, v));
}

// The line colour across the window: left to middle to right, u being the rect's 0..1 width.
float3 SeaLineColour(float u, float3 left, float3 mid, float3 right)
{
    return u < 0.5 ? lerp(left, mid, u * 2.0) : lerp(mid, right, u * 2.0 - 1.0);
}

// The wash under the lines as straight colour (rgb) and coverage (a): top to bottom, with a soft glow centred high up.
float4 SeaWash(float2 uv, float4 top, float4 bottom, float4 glow)
{
    float t = smoothstep(0.0, 1.0, uv.y);
    float4 w = float4(lerp(bottom.rgb, top.rgb, t), lerp(bottom.a, top.a, t));
    float2 d = (uv - float2(0.5, 0.72)) * float2(1.4, 1.8);
    float g = (1.0 - smoothstep(0.0, 1.0, length(d))) * glow.a;
    float a = w.a + g * (1.0 - w.a);
    float3 rgb = (w.rgb * w.a * (1.0 - g) + glow.rgb * g) / max(a, 0.0001);
    return float4(rgb, a);
}

// The lines (colour, coverage) over the wash: straight colour and coverage out.
float4 SeaOver(float3 lineColour, float lineAlpha, float4 wash)
{
    float a = lineAlpha + wash.a * (1.0 - lineAlpha);
    float3 rgb = (lineColour * lineAlpha + wash.rgb * wash.a * (1.0 - lineAlpha)) / max(a, 0.0001);
    return float4(rgb, a);
}

#endif
