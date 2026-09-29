// ChargeCrystal.hlsl
//
// The charge crystal is a STATIC solid: 60 pentagonal extruded prisms spread over a
// spherical shell, exactly as the model authors them (ChargeCrystalExport1_7-11-25.fbx —
// 60 disjoint 10-vertex prisms, 420 faces, 900 edges, all centroids at r = 0.9406).
// Nothing here displaces a vertex: the spread IS the model's, not a shader effect.
// (The generic CrystalGraph this material used to point at is the *exploding* crystal
// shader — it pushes every vertex out along its normal by _spread and spins it on
// Cosine Time, then stacks overlay blends until the result clips. That is the blowout.)
//
// The only animation is a plasma discharge that crackles from VERTEX TO VERTEX along
// the crease edges between faces — never across a face interior, and never along a
// triangulation diagonal. Same visual language as the Squirrel's skimmer forcefield
// crackle (ForcefieldCrackle.hlsl): FBM-wobbled bolts with a hot core and a cooler halo.
//
// EDGE DATA (baked once per source mesh by CrystalEdgeArcMeshBaker, shared/cached):
//   TEXCOORD1.xyz  bary      barycentric coordinate; bary[i] = 1 at local vertex i
//   TEXCOORD2.xyz  edgeH     |h[i]| = height from vertex i to the opposite edge, in
//                            MODEL-RADIUS FRACTIONS (so every _Arc*/_Node* size below is
//                            scale-independent). SIGN < 0 marks edge i as a triangulation
//                            diagonal — geometry, not a crease — and suppresses its bolt.
//   TEXCOORD3.xyz  edgeSeed  frac() = stable per-edge hash (identical on both triangles
//                            sharing the edge); >= 1 flags that this triangle traverses
//                            the edge against its canonical direction, so the travelling
//                            head lands at the same place on both sides of the crease.
//
// Distance from the fragment to edge i is bary[i] * |edgeH[i]| — the standard
// barycentric-height identity, exact for a linear interpolant across a triangle.
//
// FAIL-SAFE: an unbaked mesh interpolates TEXCOORD1 as zero, so sum(bary) == 0 and the
// arc/node terms are skipped entirely — the crystal still renders its solid faceted
// body rather than lighting up every fragment.

#ifndef CHARGE_CRYSTAL_INCLUDED
#define CHARGE_CRYSTAL_INCLUDED

// ─── Emergence coverage ─────────────────────────────────────────────────────
//
// FadeIn.cs blooms every crystal in by driving `_opacity` 0 -> 1 through a
// MaterialPropertyBlock, which is what keeps a spawning crystal inside the platform-wide
// CONTINUITY OF EXISTENCE law (nothing pops into being). This shader is OPAQUE, so it
// spends that opacity as screen-door COVERAGE rather than as blending — the same answer
// Docs/PRISM_ANIMATION.md 4.7 reaches for prisms, and it keeps 60 mutually-overlapping
// prisms sorting by depth instead of by draw order.
//
// Interleaved gradient noise: cheap, stable per pixel, and its coverage tracks the
// threshold closely enough over a fade this short.
float ChargeDitherThreshold(float2 positionSS)
{
    float n = frac(52.9829189 * frac(dot(positionSS, float2(0.06711056, 0.00583715))));
    // Strictly inside (0,1): clip(0) KEEPS a fragment, so a threshold that can reach 0
    // leaves a confetti of survivors at _opacity == 0.
    return n * 0.998 + 0.001;
}

// Returns < 0 for fragments this frame's coverage should drop.
float ChargeCoverageClip(float opacity, float2 positionSS)
{
    return opacity >= 1.0 ? 1.0 : opacity - ChargeDitherThreshold(positionSS);
}

// ─── Noise helpers (same family as ForcefieldCrackle.hlsl) ──────────────────

float ChargeHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float ChargeValueNoise(float x)
{
    float i = floor(x);
    float f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(ChargeHash11(i), ChargeHash11(i + 1.0), f);
}

// Signed FBM in roughly [-1, 1].
float ChargeFBM(float x)
{
    float v = 0.0;
    float a = 0.5;
    float fq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        v += a * (ChargeValueNoise(x * fq) * 2.0 - 1.0);
        fq *= 2.13;
        a *= 0.5;
    }
    return v;
}

// ─── Edge discharge ─────────────────────────────────────────────────────────
//
// arc  — bolt coverage, 0..1
// heat — how close this fragment is to a bolt's hot core (drives the core colour)
// node — vertex-terminal glow, 0..1 (where two crease edges of the same triangle meet)

void ChargeCrystalArcs(
    float3 bary,
    float3 edgeH,
    float3 edgeSeed,
    float  time,
    float  arcWidth,
    float  arcLength,
    float  arcSpeed,
    float  arcDuty,
    float  arcJitter,
    float  arcShimmer,
    float  nodeRadius,
    out float arc,
    out float heat,
    out float node)
{
    arc = 0.0;
    heat = 0.0;
    node = 0.0;

    // No baked edge data -> render the body only (see FAIL-SAFE above).
    if (bary.x + bary.y + bary.z < 0.5) return;

    // Object-space distance to each of the triangle's three edges.
    float3 h = abs(edgeH);
    float3 d = bary * h;

    // The SECOND-smallest of the three distances approximates the distance to the
    // nearest triangle corner (at a corner, the two edges meeting there are both 0).
    // Deliberately computed over ALL three edges, diagonals included: a diagonal still
    // passes through two real vertices, so it is a valid corner metric even where it is
    // not a valid bolt path.
    float dmin = min(d.x, min(d.y, d.z));
    float dmax = max(d.x, max(d.y, d.z));
    float dmid = d.x + d.y + d.z - dmin - dmax;
    node = exp(-(dmid * dmid) / max(nodeRadius * nodeRadius, 1e-8));

    float w2 = max(arcWidth * arcWidth, 1e-8);
    float l2 = max(arcLength * arcLength, 1e-8);

    [unroll]
    for (int i = 0; i < 3; i++)
    {
        // Triangulation diagonal — an edge inside a face, not between two faces.
        if (edgeH[i] <= 0.0) continue;

        float s = edgeSeed[i];
        float flipped = s >= 1.0 ? 1.0 : 0.0;
        s = frac(s);

        // Edge i runs between local vertices j and k (it is opposite vertex i).
        int j = (i + 1) % 3;
        int k = (i + 2) % 3;
        float t = bary[k] / max(bary[j] + bary[k], 1e-5);
        t = lerp(t, 1.0 - t, flipped);   // canonical direction, shared by both faces

        // Lateral wobble: the bolt wanders off the edge INTO the face, so a discharge
        // zigzags across the crease instead of drawing a ruled line.
        float wobble = ChargeFBM(t * 9.0 + s * 57.31 + time * 3.1);
        float jitter = (0.5 + 0.5 * wobble) * arcJitter * arcWidth;
        float dd = abs(d[i] - jitter);
        float core = exp(-(dd * dd) / w2);

        // Travelling head: vertex -> vertex, entering and leaving cleanly rather than
        // wrapping (a wrap puts a tail on the far vertex while the head is still here).
        float rate = arcSpeed * (0.55 + 0.9 * s);
        float phase = time * rate + s * 13.7;
        float span = 1.0 + 2.0 * arcLength;
        float head = frac(phase) * span - arcLength;
        float dt = t - head;
        float along = exp(-(dt * dt) / l2);

        // Duty cycle: an edge only discharges on some passes, so the lattice crackles
        // irregularly instead of pulsing in lockstep.
        float fire = step(arcDuty, ChargeHash11(floor(phase) * 1.37 + s * 91.7));

        // Faint always-on shimmer keeps the prism wireframe legible between discharges.
        float shim = arcShimmer * (0.35 + 0.65 * saturate(ChargeFBM(t * 11.0 + time * 2.3 + s * 29.0) * 0.5 + 0.5));

        float amp = max(along * fire, shim);
        arc = max(arc, core * amp);
        heat = max(heat, core * along * fire);
    }

    // Terminals light up ONLY while a bolt is actually there. Gating on `arc` is not just
    // taste: `dmid` is the second-smallest of three edge distances, whose level sets near a
    // corner are WEDGES rather than discs, so an always-on node term draws a permanent
    // starburst at every vertex (several triangles meet there, each contributing a wedge) and
    // the crystal reads as spiky rather than crackling. Masking by the bolt keeps only the
    // part of the wedge the discharge is already lighting — which is the terminal flash.
    node *= arc;
}

// ─── Full surface ───────────────────────────────────────────────────────────
//
// Unlit by design (matching the rest of the crystal family) but self-shaded off the flat
// face normal against a fixed WORLD key direction, so the 60 prisms read as distinct
// solids and catch a glint as the crystal's Rotator spins them. Every term is bounded:
// body <= bright, rim adds at most _RimStrength, emission at most _EmissionStrength.
// Only the bolt cores are allowed above 1 (that is the spark), and only on a few pixels.

void ChargeCrystalSurface(
    float3 normalWS,
    float3 viewDirWS,
    float3 bary,
    float3 edgeH,
    float3 edgeSeed,
    float  time,
    float4 dullColor,
    float4 brightColor,
    float3 keyDirWS,
    float  facetAmbient,
    float  rimPower,
    float  rimStrength,
    float  emissionStrength,
    float3 arcCoreColor,
    float  arcIntensity,
    float  arcWidth,
    float  arcLength,
    float  arcSpeed,
    float  arcDuty,
    float  arcJitter,
    float  arcShimmer,
    float  nodeRadius,
    float  nodeIntensity,
    out float3 color)
{
    float3 N = normalize(normalWS);
    float3 V = normalize(viewDirWS);

    // Flat-face shading: |N.key| so both sides of a prism catch the key as it turns.
    float facet = lerp(facetAmbient, 1.0, abs(dot(N, normalize(keyDirWS))));

    // Grazing faces pick up the bright end of the theme pair.
    float fresnel = pow(saturate(1.0 - saturate(dot(N, V))), rimPower);

    float3 body = lerp(dullColor.rgb, brightColor.rgb, saturate(fresnel * rimStrength)) * facet;
    float3 emissive = brightColor.rgb * (emissionStrength * fresnel);

    float arc, heat, node;
    ChargeCrystalArcs(bary, edgeH, edgeSeed, time,
                      arcWidth, arcLength, arcSpeed, arcDuty, arcJitter, arcShimmer, nodeRadius,
                      arc, heat, node);

    // Cool halo in the crystal's own bright colour, hot near-white core.
    float3 arcColor = lerp(brightColor.rgb, arcCoreColor, saturate(heat * heat));
    float3 discharge = arcColor * (arc * arcIntensity) + arcCoreColor * (node * nodeIntensity);

    color = body + emissive + discharge;
}

// ─── Shader Graph entry points (kept so the same code can be reused from a graph) ──

void ChargeCrystalArcs_float(
    float3 bary, float3 edgeH, float3 edgeSeed, float time,
    float arcWidth, float arcLength, float arcSpeed, float arcDuty,
    float arcJitter, float arcShimmer, float nodeRadius,
    out float Arc, out float Heat, out float Node)
{
    ChargeCrystalArcs(bary, edgeH, edgeSeed, time,
                      arcWidth, arcLength, arcSpeed, arcDuty, arcJitter, arcShimmer, nodeRadius,
                      Arc, Heat, Node);
}

void ChargeCrystalArcs_half(
    half3 bary, half3 edgeH, half3 edgeSeed, half time,
    half arcWidth, half arcLength, half arcSpeed, half arcDuty,
    half arcJitter, half arcShimmer, half nodeRadius,
    out half Arc, out half Heat, out half Node)
{
    float a, h, n;
    ChargeCrystalArcs(bary, edgeH, edgeSeed, time,
                      arcWidth, arcLength, arcSpeed, arcDuty, arcJitter, arcShimmer, nodeRadius,
                      a, h, n);
    Arc = (half)a; Heat = (half)h; Node = (half)n;
}

#endif // CHARGE_CRYSTAL_INCLUDED
