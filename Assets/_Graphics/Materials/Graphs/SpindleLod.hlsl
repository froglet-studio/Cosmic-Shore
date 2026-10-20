#ifndef SPINDLE_LOD_INCLUDED
#define SPINDLE_LOD_INCLUDED

// SpindleLod.hlsl — the GPU half of the spindle LOD (Docs/SPINDLE_LOD.md).
//
// A spindle is a WIRE, and a wire is judged by its THICKNESS on screen, never by its bounding
// sphere: the braid is ~15 units long and ~1.4 across, so a sphere-size LOD finds it "2-16 px big"
// at the very distance its three strands are each a fraction of a pixel (the 2026-10-17 sub-pixel
// LOD hid 0 of 2,898 spindles for exactly that reason). Every number here is therefore measured in
// GIRTHS — camera distance over the braid's own world-space width — which is unit-free across a
// gyroid branch, a lattice-stretched limb and a tadpole body alike (a stretch along the limb's
// length leaves its girth alone).
//
// THREE TIERS, ONE CONTINUOUS LOOK. The CPU (SpindleLodSystem) only ever swaps a mesh at a point
// where both sides of the swap draw the same picture; everything that MOVES happens here, per
// pixel and per frame, as a pure function of the camera:
//
//   near  the authored braid on SpindleGraph, untouched — this file is not in that shader.
//   mid   SpindleLodMidGraph: the braid at every third ring (SpindleBraidMid), the same lace.
//         Past convergeStart the three strands DRAW TOGETHER onto their centreline (TEXCOORD1 is
//         each ring's step to it) and the lace CLOSES into the thread's shading, on one schedule.
//   far   SpindleLodFarGraph: what the braid converged into — one faceted thread on the centreline
//         (SpindleThreadFar, 24 triangles), drawn instanced by the CPU, fading out by farFadeEnd.
//
// THE THREAD NEVER BREAKS UP. Geometry thinner than a pixel rasterises as a dotted, sparkling
// line — the reason a far spindle "doesn't look like much". Past the width where a strand would go
// sub-pixel, the vertex stage holds it at minThreadPixels by pushing every vertex out along its own
// radial arm (TEXCOORD2), and the fragment stage pays for the extra width in alpha (coverage = true
// width / drawn width). The thread keeps its spindle taper (one inflation factor per object, not
// per vertex) and keeps its facets: it is still a lit-by-facing triangular volume, never a sheet
// (Docs/claude/SHADERS_AND_PERFORMANCE.md, "Nothing in the world looks flat").
//
// Globals, published by SpindleLodSystem from SpindleLodProfileSO (file-scope like
// _PrismDepthPlane, so no graph carries them as material properties):
//   _SpindleLodBands  x convergeStart, y convergeEnd, z farFadeStart, w farFadeEnd   (girths)
//   _SpindleLodLook   x minThreadPixels, y threadBase, z threadFacet, w threadGlow
//   _SpindleLodLook2  x threadAlpha
// All-zero (no system running: a tool scene, Amoebius) leaves every function a pass-through on
// the mid graph and an unfaded thread on the far one, never a NaN.
//
// Per material: _SpindleLodGirth and _SpindleLodStrand (object-space braid width and mean strand
// radius, measured by Tools/Build/author_spindle_lod_meshes.py and copied onto each runtime LOD
// material). Girth 0 = not an LOD material = pass-through.

float4 _SpindleLodBands;
float4 _SpindleLodLook;
float4 _SpindleLodLook2;

// The braid can be inflated at most this much: past it the thread is so far below a pixel that
// its coverage alpha is under the graphs' 0.05 alpha clip anyway, and an unbounded factor would
// only grow triangles nobody can see.
#define SPINDLE_LOD_MAX_INFLATE 32.0

struct SpindleLodView
{
    float metric;      // camera distance / world girth
    float pxPerUnit;   // screen pixels per world unit at the object's distance
    float scaleXY;     // the object's cross-section scale (girth axis), world units per object unit
};

SpindleLodView SpindleLod_View(float girth)
{
    SpindleLodView v;
    v.metric = 0.0;
    v.pxPerUnit = 1e6;
    v.scaleXY = 1.0;
#if !defined(SHADERGRAPH_PREVIEW)
    float4x4 m = GetObjectToWorldMatrix();
    float3 origin = float3(m._m03, m._m13, m._m23);
    // The braid's cross-section lies in object x/y; its length is z, which a lattice limb
    // stretches. The thinner of the two cross-section scales is the one that goes sub-pixel first.
    v.scaleXY = max(min(length(float3(m._m00, m._m10, m._m20)), length(float3(m._m01, m._m11, m._m21))), 1e-6);
    float dist = max(length(origin - _WorldSpaceCameraPos.xyz), 1e-3);
    v.metric = dist / max(girth * v.scaleXY, 1e-6);
    if (unity_OrthoParams.w > 0.5)
        v.pxPerUnit = _ScreenParams.y / max(2.0 * unity_OrthoParams.y, 1e-6);
    else
        v.pxPerUnit = _ScreenParams.y * 0.5 * UNITY_MATRIX_P._m11 / dist;
#endif
    return v;
}

// 0 -> 1 across [edge0, edge1]; 0 when the band is unset or degenerate (never a divide by zero).
float SpindleLod_Ramp(float edge0, float edge1, float x)
{
    if (!(edge1 > edge0)) return 0.0;
    return smoothstep(edge0, edge1, x);
}

// How far the braid has drawn together into one thread: 0 = three strands, 1 = one thread.
float SpindleLod_Converge(float metric)
{
    return SpindleLod_Ramp(_SpindleLodBands.x, _SpindleLodBands.y, metric);
}

// The factor the thread is drawn wider than it is, so it never goes below minThreadPixels.
float SpindleLod_Inflate(SpindleLodView v, float strandRadius)
{
    float minPixels = _SpindleLodLook.x;
    if (!(minPixels > 0.0) || !(strandRadius > 0.0)) return 1.0;
    float trueWidthPx = 2.0 * strandRadius * v.scaleXY * v.pxPerUnit;
    return clamp(minPixels / max(trueWidthPx, 1e-6), 1.0, SPINDLE_LOD_MAX_INFLATE);
}

// The width hold, ramped in on the CONVERGENCE schedule rather than switched on wherever a strand
// first dips under minThreadPixels. The braid's strands go sub-pixel well before the near -> mid
// swap (the mean strand is ~1 px at 100 girths), and SpindleGraph - the near tier - knows nothing
// of the hold; holding them wide from that point would make the mid tier draw a visibly different
// braid on the frame it takes over. So: no hold at all until the strands start drawing together,
// the full hold once they are one thread (and always on the far tier, which only exists there).
float SpindleLod_Hold(SpindleLodView v, float strandRadius)
{
    return lerp(1.0, SpindleLod_Inflate(v, strandRadius), SpindleLod_Converge(v.metric));
}

// The converged look: a faceted thread lit by how squarely each facet faces the camera, brighter
// toward threadGlow as it recedes, its alpha paid down by the coverage the inflation added.
void SpindleLod_Thread(float3 dull, float3 bright, float deathState, float3 normalWS, float3 viewWS,
    SpindleLodView v, float strandRadius, out float3 color, out float alpha)
{
    float3 n = normalize(normalWS);
    float3 e = normalize(viewWS);
    float facing = saturate(abs(dot(n, e)));
    float t = saturate(_SpindleLodLook.y + _SpindleLodLook.z * facing * facing);
    float recede = SpindleLod_Ramp(_SpindleLodBands.y, _SpindleLodBands.z, v.metric);
    float glow = lerp(1.0, max(_SpindleLodLook.w, 0.0), recede);
    color = lerp(dull, bright, t) * glow;

    float coverage = 1.0 / SpindleLod_Hold(v, strandRadius);
    float life = 1.0 - smoothstep(0.0, 1.0, saturate(deathState));
    float farFade = 1.0 - SpindleLod_Ramp(_SpindleLodBands.z, _SpindleLodBands.w, v.metric);
    float threadAlpha = _SpindleLodLook2.x > 0.0 ? _SpindleLodLook2.x : 1.0;
    alpha = threadAlpha * coverage * life * farFade;
}

// ---------------------------------------------------------------------------------------------
// Vertex stage (both LOD graphs): converge the strands, then hold the thread at a visible width.
// PositionOS is the mesh's own; ToAxis = TEXCOORD1.xyz, Radial = TEXCOORD2.xyz. The result feeds
// SpindleSway, so the thread sways exactly as the braid it replaced (the sway is a shear by z,
// which convergence does not change).
// ---------------------------------------------------------------------------------------------
void SpindleLodVertex_float(float3 PositionOS, float3 ToAxis, float3 Radial, float Girth, float StrandRadius,
    out float3 Out)
{
    Out = PositionOS;
#if !defined(SHADERGRAPH_PREVIEW)
    if (!(Girth > 0.0)) return;
    SpindleLodView v = SpindleLod_View(Girth);
    float converge = SpindleLod_Converge(v.metric);
    float inflate = SpindleLod_Hold(v, StrandRadius);
    float3 strandCentre = PositionOS - Radial;
    Out = strandCentre + ToAxis * converge + Radial * inflate;
#endif
}

// ---------------------------------------------------------------------------------------------
// Fragment stage, MID graph: the lace (SpindleGraph's own chain, untouched upstream) closing into
// the thread on the same schedule the strands converge. Below convergeStart this returns the lace
// exactly, which is what makes the near -> mid mesh swap invisible.
// ---------------------------------------------------------------------------------------------
void SpindleLodSurface_float(float3 LaceColor, float LaceAlpha, float3 Dull, float3 Bright, float DeathState,
    float3 NormalWS, float3 ViewWS, float Girth, float StrandRadius,
    out float3 Color, out float Alpha)
{
    Color = LaceColor;
    Alpha = LaceAlpha;
#if !defined(SHADERGRAPH_PREVIEW)
    if (!(Girth > 0.0)) return;
    SpindleLodView v = SpindleLod_View(Girth);
    float close = SpindleLod_Converge(v.metric);
    float3 threadColor;
    float threadAlpha;
    SpindleLod_Thread(Dull, Bright, DeathState, NormalWS, ViewWS, v, StrandRadius, threadColor, threadAlpha);
    Color = lerp(LaceColor, threadColor, close);
    Alpha = lerp(LaceAlpha, threadAlpha, close);
#endif
}

// ---------------------------------------------------------------------------------------------
// Fragment stage, FAR graph: the thread alone. No Voronoi upstream at all, which is the far tier's
// fragment saving; its geometry saving is the 24-triangle mesh and its CPU saving is the instanced
// draw that replaces one renderer per limb.
// ---------------------------------------------------------------------------------------------
void SpindleLodThread_float(float3 Dull, float3 Bright, float DeathState, float3 NormalWS, float3 ViewWS,
    float Girth, float StrandRadius, out float3 Color, out float Alpha)
{
    SpindleLodView v = SpindleLod_View(max(Girth, 1e-6));
    SpindleLod_Thread(Dull, Bright, DeathState, NormalWS, ViewWS, v, StrandRadius, Color, Alpha);
}

#endif
