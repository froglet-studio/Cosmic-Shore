#ifndef INCORPOREAL_DEPTH_INCLUDED
#define INCORPOREAL_DEPTH_INCLUDED

// IncorporealDepth.hlsl — ONE depth effect for the HyperSea's incorporeal parts (Docs/SPINDLE_LOD.md §7).
//
// Mass (prisms) has a depth language: with distance, a prism OPENS - each face pushed out along its
// own normal - on a squared-distance ramp (DistanceSpreadAndColors + SpreadSubGraph, traced in
// Docs/SWARM_FAUNA.md §15). The incorporeal parts of the sea - spindles first, then the snow shards,
// the toy switches, the membrane and the nucleus - have had no depth language at all, so far away they
// either vanish or sparkle. This file gives them the prisms' own, in lockstep:
//
//   norm    = |objectOrigin - camera|^2 / SqrDistance            (the prisms' ramp, the same constant)
//   far     = Spread * (50, 35, 20)                              (the prisms' far spread, per object axis)
//   eff     = norm > 1 ? far : lerp(-7, far, norm);  eff = max(eff, Spread)
//   opening = eff - Spread                                        (zero at rest: an incorporeal part is
//                                                                  never "a little open" up close)
//   position += NormalOS * opening / objectScale * flow           (world units, as a prism does it)
//
// Spread and SqrDistance are READ off the theme's DANGER prism (IncorporealDepthSO), so the parts open
// as hard as danger mass does and a retune of the prism materials moves them with it.
//
// THE PART IS ALIVE. Mass opening is still; what is incorporeal MOVES through it. `flow` is a wave that
// travels along each part's own flow coordinate (a spindle's length; a membrane's latitude; whatever
// the adopter passes) - 1 + amplitude x wave x openness - so the opened faces breathe outward in pulses
// running root to tip, and a glow rides the same crest. Both vanish with the opening: up close the part
// is exactly its authored self.
//
// Globals (IncorporealDepth.Publish, from Resources/IncorporealDepth):
//   _IncorporealSpread  xyz the danger tier's _Spread (x strength), w its _SqrDistance
//   _IncorporealFlow    x amplitude, y waves along the flow coordinate, z waves per second, w pulse glow
// All zero (nothing published: a tool scene, Amoebius before its port) = closed, still, unlit: a no-op.
//
// ADOPTING it on another surface: include this file from the graph's Custom Function and pick the
// form that fits what the surface is made of:
//   IncorporealDepthOpen_float - per FACE, prism-exact (per-axis spread along each face normal). For a
//       faceted solid with a few big faces and SPLIT normals: a snow shard, a switch's ring segments.
//   IncorporealDepthPart_float - per PART, rigidly along a direction you supply, by the prisms'
//       farthest face travel. For something made of strands or panels: a spindle (its strands), a
//       membrane (its panels, outward), the nucleus (its shells).
// then IncorporealDepthGlow_float in the fragment, and a Strength to scale the shared spread for that
// surface. Nothing else: the ramp, the tier and the wave are shared.

float4 _IncorporealSpread;
float4 _IncorporealFlow;

#define INCORPOREAL_DEPTH_FAR_GAIN float3(50.0, 35.0, 20.0)
#define INCORPOREAL_DEPTH_NEAR_START -7.0
#define INCORPOREAL_DEPTH_TAU 6.2831853

// The prisms' normalised squared distance for an object origin.
float IncorporealDepth_Norm(float3 originWS)
{
    float3 d = originWS - _WorldSpaceCameraPos.xyz;
    return dot(d, d) / max(_IncorporealSpread.w, 1.0);
}

// How far each object axis has opened beyond rest, in WORLD units (>= 0). The prism rule exactly,
// less its resting spread.
float3 IncorporealDepth_Opening(float norm, float3 spread)
{
    float3 far = spread * INCORPOREAL_DEPTH_FAR_GAIN;
    float3 eff = norm > 1.0 ? far : lerp(float3(INCORPOREAL_DEPTH_NEAR_START, INCORPOREAL_DEPTH_NEAR_START, INCORPOREAL_DEPTH_NEAR_START), far, norm);
    eff = max(eff, spread);
    return max(eff - spread, float3(0.0, 0.0, 0.0));
}

// 0 = at rest, 1 = fully open (the prisms' over-distance), monotone in distance.
float IncorporealDepth_Openness(float norm, float3 spread)
{
    float3 full = spread * INCORPOREAL_DEPTH_FAR_GAIN - spread;
    float denom = dot(full, full);
    if (!(denom > 1e-12)) return 0.0;
    float3 o = IncorporealDepth_Opening(norm, spread);
    return saturate(sqrt(dot(o, o) / denom));
}

// The travelling wave: +1 crest .. -1 trough, moving toward +flowCoord.
float IncorporealDepth_Wave(float flowCoord, float phase, float clock)
{
    return sin(INCORPOREAL_DEPTH_TAU * (_IncorporealFlow.y * flowCoord - _IncorporealFlow.z * clock) + phase);
}

float3 IncorporealDepth_ObjectScale()
{
#if defined(SHADERGRAPH_PREVIEW)
    return float3(1.0, 1.0, 1.0);
#else
    float3x3 m = (float3x3)GetObjectToWorldMatrix();
    return max(float3(length(float3(m._m00, m._m10, m._m20)),
                      length(float3(m._m01, m._m11, m._m21)),
                      length(float3(m._m02, m._m12, m._m22))), float3(1e-5, 1e-5, 1e-5));
#endif
}

float3 IncorporealDepth_Origin()
{
#if defined(SHADERGRAPH_PREVIEW)
    return float3(0.0, 0.0, 0.0);
#else
    float4x4 m = GetObjectToWorldMatrix();
    return float3(m._m03, m._m13, m._m23);
#endif
}

// ---------------------------------------------------------------------------------------------
// Vertex stage. Out = the opened position; Openness feeds IncorporealDepthGlow and any adopter's own
// "how open am I" (the spindle LOD closes its lace on it).
// ---------------------------------------------------------------------------------------------
void IncorporealDepthOpen_float(float3 PositionOS, float3 NormalOS, float FlowCoord, float Phase, float Clock,
    float Strength, out float3 Out, out float Openness)
{
    Out = PositionOS;
    Openness = 0.0;
#if !defined(SHADERGRAPH_PREVIEW)
    float3 spread = _IncorporealSpread.xyz * max(Strength, 0.0);
    float norm = IncorporealDepth_Norm(IncorporealDepth_Origin());
    float3 opening = IncorporealDepth_Opening(norm, spread);
    Openness = IncorporealDepth_Openness(norm, spread);
    float flow = 1.0 + _IncorporealFlow.x * Openness * IncorporealDepth_Wave(FlowCoord, Phase, Clock);
    Out = PositionOS + NormalOS * (opening / IncorporealDepth_ObjectScale()) * flow;
#endif
}

// ---------------------------------------------------------------------------------------------
// Vertex stage, PARTS form. Where a part's natural "faces" are not its triangles - a spindle's are its
// three STRANDS, a membrane's might be its panels - pushing every small face along its own normal
// shreds it (measured on the braid: 216 faces, each flung its own way, read as fur). Instead each
// part moves rigidly along a DirectionOS the adopter supplies (a strand: away from the braid's
// centreline), by the distance the prisms' farthest-travelling face goes - the largest axis of the
// same opening - in WORLD units whatever the object's scale, breathing with the same wave. Same
// ramp, same strength, same wave; only the unit of what parts differs.
// ---------------------------------------------------------------------------------------------
void IncorporealDepthPart_float(float3 PositionOS, float3 DirectionOS, float FlowCoord, float Phase, float Clock,
    float Strength, out float3 Out, out float Openness)
{
    Out = PositionOS;
    Openness = 0.0;
#if !defined(SHADERGRAPH_PREVIEW)
    float dirLen = length(DirectionOS);
    float3 spread = _IncorporealSpread.xyz * max(Strength, 0.0);
    float norm = IncorporealDepth_Norm(IncorporealDepth_Origin());
    float3 opening = IncorporealDepth_Opening(norm, spread);
    Openness = IncorporealDepth_Openness(norm, spread);
    if (!(dirLen > 1e-6)) return;
    float amount = max(opening.x, max(opening.y, opening.z));
    float flow = 1.0 + _IncorporealFlow.x * Openness * IncorporealDepth_Wave(FlowCoord, Phase, Clock);
    // World units along the world image of the direction: divide by how much the object's matrix
    // stretches that direction, so a lattice limb scaled 6x along its length parts no further.
    float worldLen = length(mul((float3x3)GetObjectToWorldMatrix(), DirectionOS));
    Out = PositionOS + DirectionOS * (amount * flow / max(worldLen, 1e-6));
#endif
}

// ---------------------------------------------------------------------------------------------
// Fragment stage: the pulse of light riding the wave's crest, in proportion to how open the part is.
// Openness is recomputed from the object origin (a per-object constant), so no interpolator is needed.
// ---------------------------------------------------------------------------------------------
float IncorporealDepth_Pulse(float FlowCoord, float Phase, float Clock, float Strength)
{
#if defined(SHADERGRAPH_PREVIEW)
    return 0.0;
#else
    float3 spread = _IncorporealSpread.xyz * max(Strength, 0.0);
    float openness = IncorporealDepth_Openness(IncorporealDepth_Norm(IncorporealDepth_Origin()), spread);
    float crest = saturate(IncorporealDepth_Wave(FlowCoord, Phase, Clock));
    return max(_IncorporealFlow.w, 0.0) * openness * crest * crest;
#endif
}

void IncorporealDepthGlow_float(float3 Color, float FlowCoord, float Phase, float Clock, float Strength,
    out float3 Out)
{
    Out = Color * (1.0 + IncorporealDepth_Pulse(FlowCoord, Phase, Clock, Strength));
}

#endif
