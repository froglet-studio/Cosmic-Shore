// The living crystal lattice behind style-B panels (Docs/MENU_REDESIGN_B.md §45.2): a triangular lattice that
// drifts by one lattice vector per period, element energy pulses running along some of its lines, and a few
// nodes twinkling in the element colours. Pure maths on canvas-space position and time - no textures - so the
// same function draws a panel and a whole window, and Tools/Build/lattice_harness compiles and renders it.
#ifndef COSMIC_UI_LATTICE_INCLUDED
#define COSMIC_UI_LATTICE_INCLUDED

static const float LATTICE_SQ3_2 = 0.8660254;

float LatticeHash(float a, float b)
{
    return frac(sin(a * 127.1 + b * 311.7) * 43758.5453);
}

// Distance (canvas units) from p to the nearest line of the family with unit normal n and spacing h;
// that line's index goes to `index`.
float LatticeLineDistance(float2 p, float2 n, float h, out float index)
{
    float d = dot(p, n) / h;
    index = floor(d + 0.5);
    return abs(d - index) * h;
}

// One of the four element colours, picked by a hash in [0, 1).
float3 LatticeElement(float hash, float3 charge, float3 mass, float3 space, float3 time)
{
    float k = floor(hash * 4.0);
    return k < 1.0 ? charge : (k < 2.0 ? mass : (k < 3.0 ? space : time));
}

// One lattice layer as premultiplied colour (rgb) and coverage (a), before the layer alpha.
// spacing: node to node, canvas units. driftPeriod: seconds per lattice vector. pulseDensity / twinkleDensity:
// fractions of lines / nodes that carry energy. pulseRepeat: distance between pulses on a carrying line.
float4 LatticeLayer(float2 p, float t, float spacing, float driftPeriod, float lineWidth, float nodeSize,
                    float pulseDensity, float pulseSpeed, float pulseRepeat, float pulseLength, float twinkleDensity,
                    float3 lineColor, float3 charge, float3 mass, float3 space, float3 time)
{
    float h = spacing * LATTICE_SQ3_2;
    float2 q = p + float2(spacing * 0.5, h) * (t / max(driftPeriod, 0.001));

    float2 n0 = float2(0.0, 1.0);
    float2 n1 = float2(-LATTICE_SQ3_2, 0.5);
    float2 n2 = float2(-LATTICE_SQ3_2, -0.5);
    float i0, i1, i2;
    float d0 = LatticeLineDistance(q, n0, h, i0);
    float d1 = LatticeLineDistance(q, n1, h, i1);
    float d2 = LatticeLineDistance(q, n2, h, i2);
    float dmin = min(d0, min(d1, d2));

    float lineCore = 1.0 - smoothstep(0.0, lineWidth, dmin);
    float lineGlow = (1.0 - smoothstep(0.0, lineWidth * 4.0, dmin)) * 0.22;
    // Two families only ever cross at a node (the third passes there too).
    float node = 1.0 - smoothstep(0.0, nodeSize, max(d0, d1));

    float coverage = lineCore + lineGlow + node * 1.2;
    float3 colour = lineColor * coverage;

    // Twinkling nodes: the nearest node in this row, a few of them glowing in an element colour.
    float row = floor(q.y / h + 0.5);
    float col = floor((q.x - row * spacing * 0.5) / spacing + 0.5);
    float2 nodePos = float2(col * spacing + row * spacing * 0.5, row * h);
    float nodeHash = LatticeHash(col, row);
    float twinkleOn = step(nodeHash, twinkleDensity);
    float wave = sin(t * (1.1 + nodeHash * 2.3) + nodeHash * 40.0) * 0.5 + 0.5;
    float twinkle = twinkleOn * wave * wave * wave * (1.0 - smoothstep(0.0, nodeSize * 2.6, length(q - nodePos)));
    colour += LatticeElement(LatticeHash(row, col), charge, mass, space, time) * twinkle * 1.6;
    coverage += twinkle;

    // Pulses: a bright head and a fading tail running along a few lines of each family.
    float2 t0 = float2(1.0, 0.0);
    float2 t1 = float2(0.5, LATTICE_SQ3_2);
    float2 t2 = float2(-0.5, LATTICE_SQ3_2);
    float tailStart = 1.0 - pulseLength / max(pulseRepeat, 1.0);
    float carry0 = step(LatticeHash(i0, 1.0), pulseDensity);
    float carry1 = step(LatticeHash(i1, 2.0), pulseDensity);
    float carry2 = step(LatticeHash(i2, 3.0), pulseDensity);
    float f0 = frac(dot(q, t0) / pulseRepeat - t * pulseSpeed / pulseRepeat + LatticeHash(i0, 11.0) * 13.0);
    float f1 = frac(dot(q, t1) / pulseRepeat - t * pulseSpeed / pulseRepeat + LatticeHash(i1, 12.0) * 13.0);
    float f2 = frac(dot(q, t2) / pulseRepeat - t * pulseSpeed / pulseRepeat + LatticeHash(i2, 13.0) * 13.0);
    float near0 = 1.0 - smoothstep(0.0, lineWidth * 3.0, d0);
    float near1 = 1.0 - smoothstep(0.0, lineWidth * 3.0, d1);
    float near2 = 1.0 - smoothstep(0.0, lineWidth * 3.0, d2);
    float p0 = carry0 * smoothstep(tailStart, 1.0, f0) * near0;
    float p1 = carry1 * smoothstep(tailStart, 1.0, f1) * near1;
    float p2 = carry2 * smoothstep(tailStart, 1.0, f2) * near2;
    colour += LatticeElement(LatticeHash(i0, 5.0), charge, mass, space, time) * p0 * 2.0
            + LatticeElement(LatticeHash(i1, 6.0), charge, mass, space, time) * p1 * 2.0
            + LatticeElement(LatticeHash(i2, 7.0), charge, mass, space, time) * p2 * 2.0;
    coverage += p0 + p1 + p2;

    return float4(colour, coverage);
}

// How much of the panel has crystallised: the lattice grows outward from the centre as `reveal` (the vertex
// alpha, so a CanvasGroup fade-in) goes 0 -> 1, and its growth front burns brighter. r: 0 at the centre,
// 1 at the corners. Returns (mask, front).
float2 LatticeReveal(float r, float reveal)
{
    float edge = reveal * 1.15;
    float mask = 1.0 - smoothstep(edge - 0.15, edge, r);
    float front = saturate(1.0 - abs(r - (edge - 0.075)) / 0.075) * (1.0 - reveal);
    return float2(mask, front);
}

#endif
