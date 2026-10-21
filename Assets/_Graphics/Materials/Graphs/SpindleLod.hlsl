#ifndef SPINDLE_LOD_INCLUDED
#define SPINDLE_LOD_INCLUDED

// The shared depth effect of the sea's incorporeal parts; the OVERT style below is its first user.
#include "IncorporealDepth.hlsl"

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
//   _SpindleLodLook2  x threadAlpha, y STYLE (0 covert, 1 overt)
//
// TWO STYLES, ONE SET OF MESHES (SpindleLodProfileSO.style):
//   COVERT  the LOD hides itself. Bands in GIRTHS; the strands converge into one thread and the
//           width hold keeps it a continuous line. Nobody is meant to notice.
//   OVERT   the LOD IS the effect, in lockstep with the prisms' own depth effect: bands in the
//           prisms' normalised squared distance (IncorporealDepth.hlsl), the braid's three strands
//           FAN OPEN - tips travelling as far as danger mass's faces do, roots still on their node -
//           and a wave travels root to tip through them -
//           the plant reads, from afar, as mass with something alive moving through it.
//           Bands: x recedeStart, y recedeEnd (the glow ramps up from here), z/w far fade, all in
//           norm units; the recede itself is IncorporealDepth's (_IncorporealRecede).
//           Strength (_SpindleLodStrength, per material) is this limb's share of the shared spread:
//           a Charge lifeform's limbs part hardest (SpindleLodProfileSO.chargeStrength).
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

bool SpindleLod_Overt() { return _SpindleLodLook2.y > 0.5; }

struct SpindleLodView
{
    float metric;      // the style's distance: girths (covert) or the prisms' norm (overt)
    float progress;    // 0 = the braid as authored, 1 = fully transformed (converged / opened): lace + hold
    float converge;    // how far the strands have drawn onto the centreline (covert: = progress;
                       // overt: as the far-side recede closes the fan, so the braid ends as the thread)
    float pxPerUnit;   // screen pixels per world unit at the object's distance
    float scaleXY;     // the object's cross-section scale (girth axis), world units per object unit
};

// 0 -> 1 across [edge0, edge1]; 0 when the band is unset or degenerate (never a divide by zero).
float SpindleLod_Ramp(float edge0, float edge1, float x)
{
    if (!(edge1 > edge0)) return 0.0;
    return smoothstep(edge0, edge1, x);
}

// How far the braid has drawn together into one thread (covert): 0 = three strands, 1 = one thread.
float SpindleLod_Converge(float metric)
{
    return SpindleLod_Ramp(_SpindleLodBands.x, _SpindleLodBands.y, metric);
}

SpindleLodView SpindleLod_View(float girth)
{
    SpindleLodView v;
    v.metric = 0.0;
    v.progress = 0.0;
    v.converge = 0.0;
    v.pxPerUnit = 1e6;
    v.scaleXY = 1.0;
#if !defined(SHADERGRAPH_PREVIEW)
    float4x4 m = GetObjectToWorldMatrix();
    float3 origin = float3(m._m03, m._m13, m._m23);
    // The braid's cross-section lies in object x/y; its length is z, which a lattice limb
    // stretches. The thinner of the two cross-section scales is the one that goes sub-pixel first.
    v.scaleXY = max(min(length(float3(m._m00, m._m10, m._m20)), length(float3(m._m01, m._m11, m._m21))), 1e-6);
    float dist = max(length(origin - _WorldSpaceCameraPos.xyz), 1e-3);
    // abs(): URP renders into an intermediate texture on D3D, Metal and Vulkan and FLIPS the
    // projection's y there, so _m11 is negative. A negative width once read as "infinitely thin",
    // which held every strand at the 32x inflation cap - the limbs ballooned between 184 and 316
    // units as the hold ramped in, and shrank back below it (Garrett, 2026-10-21, the Lattice).
    if (unity_OrthoParams.w > 0.5)
        v.pxPerUnit = _ScreenParams.y / max(2.0 * abs(unity_OrthoParams.y), 1e-6);
    else
        v.pxPerUnit = _ScreenParams.y * 0.5 * abs(UNITY_MATRIX_P._m11) / dist;
    if (SpindleLod_Overt())
    {
        v.metric = IncorporealDepth_Norm(origin);
        // The near side follows the prisms' ramp and STAYS transformed past it (the lace never comes
        // back); the far side's recede closes the fan while drawing the strands together.
        v.progress = IncorporealDepth_RampOpenness(v.metric, _IncorporealSpread.xyz);
        v.converge = 1.0 - IncorporealDepth_Recede(v.metric);
    }
    else
    {
        v.metric = dist / max(girth * v.scaleXY, 1e-6);
        v.progress = SpindleLod_Converge(v.metric);
        v.converge = v.progress;
    }
#endif
    return v;
}

// The factor the thread is drawn wider than it is, so it never goes below minThreadPixels.
float SpindleLod_Inflate(SpindleLodView v, float strandRadius)
{
    float minPixels = _SpindleLodLook.x;
    if (!(minPixels > 0.0) || !(strandRadius > 0.0)) return 1.0;
    float trueWidthPx = 2.0 * strandRadius * v.scaleXY * v.pxPerUnit;
    return clamp(minPixels / max(trueWidthPx, 1e-6), 1.0, SPINDLE_LOD_MAX_INFLATE);
}

// The width hold, ramped in on the transformation's own schedule (convergence, or opening) rather
// than switched on wherever a strand first dips under minThreadPixels. The braid's strands go
// sub-pixel well before the near -> mid swap (the mean strand is ~1 px at 100 girths), and
// SpindleGraph - the near tier - knows nothing of the hold; holding them wide from that point would
// make the mid tier draw a visibly different braid on the frame it takes over. So: no hold at all
// while the braid is as authored, the full hold once it is fully transformed.
float SpindleLod_Hold(SpindleLodView v, float strandRadius)
{
    return lerp(1.0, SpindleLod_Inflate(v, strandRadius), v.progress);
}

// The transformed look: a faceted strand lit by how squarely each facet faces the camera, brighter
// toward threadGlow as it recedes, its alpha paid down by the coverage the width hold added; in the
// overt style, the travelling pulse rides on top.
// fades: false for a SYNTHETIC wire (the switch rings), which is a signal and never leaves.
void SpindleLod_ThreadLook(float3 dull, float3 bright, float deathState, float3 normalWS, float3 viewWS,
    SpindleLodView v, float strandRadius, bool fades, out float3 color, out float alpha)
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
    float farFade = 1.0;
    if (fades) farFade = 1.0 - SpindleLod_Ramp(_SpindleLodBands.z, _SpindleLodBands.w, v.metric);
    float threadAlpha = _SpindleLodLook2.x > 0.0 ? _SpindleLodLook2.x : 1.0;
    alpha = threadAlpha * coverage * life * farFade;
}

void SpindleLod_Thread(float3 dull, float3 bright, float deathState, float3 normalWS, float3 viewWS,
    SpindleLodView v, float strandRadius, out float3 color, out float alpha)
{
    SpindleLod_ThreadLook(dull, bright, deathState, normalWS, viewWS, v, strandRadius, true, color, alpha);
}

// The overt style's pulse on any colour (zero in the covert style, while the braid is closed, and once
// it has receded). Strength = this limb's share of the shared spread (_SpindleLodStrength).
float3 SpindleLod_Pulse(float3 color, float flowCoord, float phase, float clock, float strength)
{
    if (!SpindleLod_Overt()) return color;
    return color * (1.0 + IncorporealDepth_Pulse(flowCoord, phase, clock, strength));
}

// ---------------------------------------------------------------------------------------------
// Vertex stage (both LOD graphs). PositionOS / NormalOS are the mesh's own, UV.x runs a strand's
// length (the flow coordinate, 0 at the root), ToAxis = TEXCOORD1.xyz, Radial = TEXCOORD2.xyz,
// Splay = TEXCOORD3.xyz.
//   covert: converge the strands onto the centreline, then hold the thread at a visible width.
//   overt:  hold the strands at a visible width, then FAN the three strands open along their own
//           splay on the prisms' ramp, root-anchored, breathing with the travelling wave
//           (IncorporealDepthPart).
// The result feeds SpindleSway, so either form sways exactly as the braid it replaced.
// ---------------------------------------------------------------------------------------------
void SpindleLodVertex_float(float3 PositionOS, float3 NormalOS, float3 UV, float3 ToAxis, float3 Radial,
    float3 Splay, float Girth, float StrandRadius, float Strength, float Phase, float Clock, out float3 Out)
{
    Out = PositionOS;
#if !defined(SHADERGRAPH_PREVIEW)
    if (!(Girth > 0.0)) return;
    SpindleLodView v = SpindleLod_View(Girth);
    float inflate = SpindleLod_Hold(v, StrandRadius);
    float3 strandCentre = PositionOS - Radial;
    if (SpindleLod_Overt())
    {
        // The braid's parts are its three STRANDS. Each FANS along its own splay (TEXCOORD3: the way
        // its tip already leans), anchored at the root and reaching the danger prisms' face travel at
        // the tip: the braid blooms like a whisk opening, and stays a bond to the node it grows from.
        // Past the recede the strands also draw onto the centreline, so a fully receded braid is the
        // far thread's own construction and the swap to the 12-triangle thread is never seen.
        float3 held = strandCentre + ToAxis * v.converge + Radial * inflate;
        float3 parted;
        float openness;
        IncorporealDepthPart_float(held, Splay, UV.x, Phase, Clock, Strength, parted, openness);
        Out = held + (parted - held) * saturate(UV.x);
    }
    else
    {
        Out = strandCentre + ToAxis * v.converge + Radial * inflate;
    }
#endif
}

// ---------------------------------------------------------------------------------------------
// Fragment stage, MID graph: the lace (SpindleGraph's own chain, untouched upstream) closing into
// the transformed look on the same schedule as the geometry. While the braid is as authored this
// returns the lace exactly, which is what makes the near -> mid mesh swap invisible.
// ---------------------------------------------------------------------------------------------
void SpindleLodSurface_float(float3 LaceColor, float LaceAlpha, float3 Dull, float3 Bright, float DeathState,
    float3 NormalWS, float3 ViewWS, float3 UV, float Girth, float StrandRadius, float Strength, float Phase,
    float Clock, out float3 Color, out float Alpha)
{
    Color = LaceColor;
    Alpha = LaceAlpha;
#if !defined(SHADERGRAPH_PREVIEW)
    if (!(Girth > 0.0)) return;
    SpindleLodView v = SpindleLod_View(Girth);
    float3 threadColor;
    float threadAlpha;
    SpindleLod_Thread(Dull, Bright, DeathState, NormalWS, ViewWS, v, StrandRadius, threadColor, threadAlpha);
    Color = SpindleLod_Pulse(lerp(LaceColor, threadColor, v.progress), UV.x, Phase, Clock, Strength);
    Alpha = lerp(LaceAlpha, threadAlpha, v.progress);
#endif
}

// ---------------------------------------------------------------------------------------------
// Fragment stage, FAR graph: the transformed look alone. No Voronoi upstream at all, which is the far
// tier's fragment saving; its geometry saving is the far mesh and its CPU saving is the instanced
// draw that replaces one renderer per limb.
// ---------------------------------------------------------------------------------------------
void SpindleLodThread_float(float3 Dull, float3 Bright, float DeathState, float3 NormalWS, float3 ViewWS,
    float3 UV, float Girth, float StrandRadius, float Strength, float Phase, float Clock, out float3 Color, out float Alpha)
{
    SpindleLodView v = SpindleLod_View(max(Girth, 1e-6));
    SpindleLod_Thread(Dull, Bright, DeathState, NormalWS, ViewWS, v, StrandRadius, Color, Alpha);
    Color = SpindleLod_Pulse(Color, UV.x, Phase, Clock, Strength);
}

// =============================================================================================
// SpindleWireGraph: the SYNTHETIC WIRE (the switch rings, Docs/SPINDLE_LOD.md section 9). A machined
// three-strand cable with the braid's own channels, so every vertex behaviour above applies to it as
// it is: the strands unravel (fan) on the prisms' ramp, draw back together as it recedes, and hold a
// visible width. Only the SURFACE differs, and it runs the plant's story the other way round:
//
//   at rest (up close, and settled far away)   the thread's solid, faceted, lit-by-facing look - a
//                                              synthetic construct, no lace at all;
//   while the effect is open                   the spindle's lace (SpindleGraph's own chain), in
//                                              proportion to how open it is - a spindle from afar.
//
// So the near tier (this graph on the full cable) and every other tier draw the same picture at
// their boundaries, and the swap is never seen. It never fades and is never culled: a switch is a
// signal you fly toward.
// =============================================================================================
void SpindleWireSurface_float(float3 LaceColor, float LaceAlpha, float3 Dull, float3 Bright, float DeathState,
    float3 NormalWS, float3 ViewWS, float3 UV, float Girth, float StrandRadius, float Strength, float Phase,
    float Clock, out float3 Color, out float Alpha)
{
    Color = LaceColor;
    Alpha = LaceAlpha;
#if !defined(SHADERGRAPH_PREVIEW)
    if (!(Girth > 0.0)) return;
    SpindleLodView v = SpindleLod_View(Girth);
    float3 wireColor;
    float wireAlpha;
    SpindleLod_ThreadLook(Dull, Bright, DeathState, NormalWS, ViewWS, v, StrandRadius, false, wireColor, wireAlpha);
    float lace = SpindleLod_Overt() ? IncorporealDepth_Openness(v.metric, _IncorporealSpread.xyz) : 0.0;
    Color = SpindleLod_Pulse(lerp(wireColor, LaceColor, lace), UV.x, Phase, Clock, Strength);
    Alpha = lerp(wireAlpha, LaceAlpha, lace);
#endif
}

// =============================================================================================
// SpindleDepthGraph: the OVERT effect on a spindle-material surface that is NOT the braid
// (Docs/SPINDLE_LOD.md §8). SpindleGraph's lace, sway and death clock untouched, with the shared
// depth effect spliced in before the sway and its pulse on the colour. No LOD meshes and no tiers:
// these surfaces are few, and most of them move.
//
//   Form 0  BLOOM (creatures: the shark, brittlestar, worm and quadfish models, skinned or not).
//           Outward from the object's pivot by position alone (IncorporealDepthBloom), reaching the
//           full travel at Reach world units (_SpindleDepthReach, measured per renderer by
//           SpindleLodSystem from its bounds); the wave runs heart -> extremities. And it LEAVES with
//           the limbs: its alpha fades out on the thread's own band (_SpindleLodBands z -> w), so every
//           spindle in the sea is gone at the same distance.
//   Form 1  FACETS (the switch rings). Prism-exact: every flat facet opens along its own normal
//           (IncorporealDepthOpen), and the wave runs along UV.x, round the ring.
//
// Covert style (or no system running): a pass-through, so the graph draws SpindleGraph exactly.
// Strength 0 (the template's default, any material nobody tuned) is a pass-through too.
// =============================================================================================
void SpindleDepthVertex_float(float3 PositionOS, float3 NormalOS, float3 UV, float Reach, float Form,
    float Strength, float Phase, float Clock, out float3 Out)
{
    Out = PositionOS;
#if !defined(SHADERGRAPH_PREVIEW)
    if (!SpindleLod_Overt()) return;
    float openness;
    if (Form > 0.5)
        IncorporealDepthOpen_float(PositionOS, NormalOS, UV.x, Phase, Clock, Strength, Out, openness);
    else
        IncorporealDepthBloom_float(PositionOS, Reach, Phase, Clock, Strength, Out, openness);
#endif
}

void SpindleDepthGlow_float(float3 Color, float Alpha, float3 PositionWS, float3 UV, float Reach, float Form,
    float Strength, float Phase, float Clock, out float3 OutColor, out float OutAlpha)
{
    OutColor = Color;
    OutAlpha = Alpha;
#if !defined(SHADERGRAPH_PREVIEW)
    if (!SpindleLod_Overt()) return;
    if (Form > 0.5)
    {
        OutColor = Color * (1.0 + IncorporealDepth_Pulse(UV.x, Phase, Clock, Strength));
        return;
    }
    OutColor = Color * (1.0 + IncorporealDepth_BloomPulse(PositionWS, Reach, Phase, Clock, Strength));
    float norm = IncorporealDepth_Norm(IncorporealDepth_Origin());
    OutAlpha = Alpha * (1.0 - SpindleLod_Ramp(_SpindleLodBands.z, _SpindleLodBands.w, norm));
#endif
}

#endif
