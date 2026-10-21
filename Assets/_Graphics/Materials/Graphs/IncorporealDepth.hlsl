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
// AND IT SETTLES BACK WITH DISTANCE. The prisms stay fully open forever; an incorporeal part does not.
// Past the peak (the prisms' over-distance, norm 1) it holds open to `recedeStart`, then RECEDES
// smoothly - linear in DISTANCE, not in squared distance, so it eases over a long way - back to rest
// by `recedeEnd`. A far plant arrives on screen as a minimal presence and the effect grows as you
// close in, instead of appearing at full strength wherever its fade-in happens to start (Garrett,
// 2026-10-20: "the onset is very abrupt"). The near side is the prisms' ramp, untouched (lockstep).
//
// Globals (IncorporealDepth.Publish, from Resources/IncorporealDepth):
//   _IncorporealSpread  xyz the danger tier's _Spread (x strength), w its _SqrDistance
//   _IncorporealFlow    x amplitude, y waves along the flow coordinate, z waves per second, w pulse glow
//   _IncorporealRecede  x recedeStart, y recedeEnd (normalised squared distance; y <= x = never recedes)
// All zero (nothing published: a tool scene, Amoebius before its port) = closed, still, unlit: a no-op.
//
// ADOPTING it on another surface: include this file from the graph's Custom Function and pick the
// form that fits what the surface is made of:
//   IncorporealDepthOpen_float - per FACE, prism-exact (per-axis spread along each face normal). For a
//       faceted solid with a few big faces and SPLIT normals: a snow shard, a switch's ring segments.
//   IncorporealDepthPart_float - per PART, rigidly along a direction you supply, by the prisms'
//       farthest face travel. For something made of strands or panels: a spindle (its strands), a
//       membrane (its panels, outward), the nucleus (its shells).
//   IncorporealDepthBloom_float - per OBJECT, outward from its pivot, by POSITION ALONE. For a
//       surface whose parts no channel names - a creature's own model, skinned or not - where any
//       per-vertex attribute would tear it at its seams.
// then IncorporealDepthGlow_float (or IncorporealDepth_BloomPulse) in the fragment, and a Strength to
// scale the shared spread for that surface. Nothing else: the ramp, the tier and the wave are shared.

float4 _IncorporealSpread;
float4 _IncorporealFlow;
float4 _IncorporealRecede;

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

// The prisms' ramp alone: 0 = at rest, 1 = fully open (the over-distance), monotone in distance.
float IncorporealDepth_RampOpenness(float norm, float3 spread)
{
    float3 full = spread * INCORPOREAL_DEPTH_FAR_GAIN - spread;
    float denom = dot(full, full);
    if (!(denom > 1e-12)) return 0.0;
    float3 o = IncorporealDepth_Opening(norm, spread);
    return saturate(sqrt(dot(o, o) / denom));
}

// The far side: 1 out to recedeStart, easing to 0 by recedeEnd, smooth in DISTANCE (sqrt of the norm).
float IncorporealDepth_Recede(float norm)
{
    float a = _IncorporealRecede.x;
    float b = _IncorporealRecede.y;
    if (!(b > a) || !(a > 0.0)) return 1.0;
    return 1.0 - smoothstep(sqrt(a), sqrt(b), sqrt(max(norm, 0.0)));
}

// How open the part is overall: the prisms' ramp coming in, the recede going out.
float IncorporealDepth_Openness(float norm, float3 spread)
{
    return IncorporealDepth_RampOpenness(norm, spread) * IncorporealDepth_Recede(norm);
}

// The travelling wave's phase angle along the flow coordinate (one definition: the bloom also takes
// its derivative).
float IncorporealDepth_WaveArg(float flowCoord, float phase, float clock)
{
    return INCORPOREAL_DEPTH_TAU * (_IncorporealFlow.y * flowCoord - _IncorporealFlow.z * clock) + phase;
}

// The travelling wave: +1 crest .. -1 trough, moving toward +flowCoord.
float IncorporealDepth_Wave(float flowCoord, float phase, float clock)
{
    return sin(IncorporealDepth_WaveArg(flowCoord, phase, clock));
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
    float3 opening = IncorporealDepth_Opening(norm, spread) * IncorporealDepth_Recede(norm);
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
    float3 opening = IncorporealDepth_Opening(norm, spread) * IncorporealDepth_Recede(norm);
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
// Vertex stage, BLOOM form. A creature's own model has no parts this file can name: its strands
// are not laid along a UV, and its UV seams and hard edges SPLIT vertices, so anything read off a
// vertex (its normal, its UV) would move the two copies of a seam apart and tear the surface. So
// the bloom is a function of POSITION ALONE: every point moves outward from the object's pivot,
// anchored there and reaching the prisms' farthest face travel at Reach world units out (a limb's
// fan, root to tip, with the creature's heart for the root), and the wave runs heart ->
// extremities along that same radius. In world units whatever the object's scale.
//
// The travel is capped as a share of Reach. The wave makes the radial stretch vary along the
// radius, and a crest that pushes harder than the trough ahead of it can overtake it and fold the
// surface through itself. With q = 2 pi x waves x the wave's amplitude (the steepest the wave's
// share of the travel changes per unit of r / Reach), the displaced radius's derivative is at least
// 1 - (travel / Reach) x q, inside Reach and out; capping travel / Reach at 0.8 / q keeps it >= 0.2,
// so the bloom never folds and can be inverted (BloomRestRadius). Never more than
// INCORPOREAL_DEPTH_BLOOM_MAX_SHARE of Reach either: a body may bloom, not double in size.
// ---------------------------------------------------------------------------------------------
#define INCORPOREAL_DEPTH_BLOOM_MAX_SHARE 0.3
#define INCORPOREAL_DEPTH_BLOOM_MIN_SLOPE 0.2

float IncorporealDepth_BloomAmount(float3 opening, float reach, float openness)
{
    float q = INCORPOREAL_DEPTH_TAU * max(_IncorporealFlow.y, 0.0) * max(_IncorporealFlow.x, 0.0) * openness;
    float share = min(INCORPOREAL_DEPTH_BLOOM_MAX_SHARE, (1.0 - INCORPOREAL_DEPTH_BLOOM_MIN_SLOPE) / max(q, 1e-6));
    return min(max(opening.x, max(opening.y, opening.z)), share * reach);
}

// The displaced radius of a point at rest radius r (and its derivative): r stretched by the
// travel, linearly out to Reach (the fan's root-to-tip weight), by the full travel beyond it.
void IncorporealDepth_BloomRadius(float r, float amount, float reach, float openness, float phase, float clock,
    out float rd, out float drd)
{
    float k = INCORPOREAL_DEPTH_TAU * _IncorporealFlow.y / reach;   // d(arg)/dr
    float arg = IncorporealDepth_WaveArg(r / reach, phase, clock);
    float a = _IncorporealFlow.x * openness;
    float flow = 1.0 + a * sin(arg);
    float dflow = a * cos(arg) * k;
    if (r < reach)
    {
        rd = r + amount * flow * r / reach;
        drd = 1.0 + amount * (flow + r * dflow) / reach;
    }
    else
    {
        rd = r + amount * flow;
        drd = 1.0 + amount * dflow;
    }
}

// The inverse: the rest radius of a point the bloom moved to rdisplaced. The fragment stage sees
// only displaced positions, and the pulse must ride the crest the vertex stage put there.
// The bloom is monotone (the cap above), so Newton converges from the no-wave guess.
float IncorporealDepth_BloomRestRadius(float rdisplaced, float amount, float reach, float openness,
    float phase, float clock)
{
    float r = rdisplaced / (1.0 + amount / max(rdisplaced, reach));
    for (int i = 0; i < 4; i++)
    {
        float rd, drd;
        IncorporealDepth_BloomRadius(r, amount, reach, openness, phase, clock, rd, drd);
        r = max(r - (rd - rdisplaced) / max(drd, INCORPOREAL_DEPTH_BLOOM_MIN_SLOPE), 0.0);
    }
    return r;
}

void IncorporealDepthBloom_float(float3 PositionOS, float Reach, float Phase, float Clock, float Strength,
    out float3 Out, out float Openness)
{
    Out = PositionOS;
    Openness = 0.0;
#if !defined(SHADERGRAPH_PREVIEW)
    float3 spread = _IncorporealSpread.xyz * max(Strength, 0.0);
    float norm = IncorporealDepth_Norm(IncorporealDepth_Origin());
    float3 opening = IncorporealDepth_Opening(norm, spread) * IncorporealDepth_Recede(norm);
    Openness = IncorporealDepth_Openness(norm, spread);
    float reach = max(Reach, 1e-3);
    float amount = IncorporealDepth_BloomAmount(opening, reach, Openness);
    // The pivot is the object origin, so a point's world offset from it is the matrix applied to
    // its object position, and scaling the object position scales that offset along itself: the
    // world displacement is radial from the pivot whatever the object's scale.
    float r = length(mul((float3x3)GetObjectToWorldMatrix(), PositionOS));
    float rd, drd;
    IncorporealDepth_BloomRadius(r, amount, reach, Openness, Phase, Clock, rd, drd);
    Out = r > 1e-6 ? PositionOS * (rd / r) : PositionOS;
#endif
}

// The bloom's pulse at a DISPLACED world position: the crest the vertex stage moved there.
float IncorporealDepth_BloomPulse(float3 PositionWS, float Reach, float Phase, float Clock, float Strength)
{
#if defined(SHADERGRAPH_PREVIEW)
    return 0.0;
#else
    float3 spread = _IncorporealSpread.xyz * max(Strength, 0.0);
    float3 origin = IncorporealDepth_Origin();
    float norm = IncorporealDepth_Norm(origin);
    float3 opening = IncorporealDepth_Opening(norm, spread) * IncorporealDepth_Recede(norm);
    float openness = IncorporealDepth_Openness(norm, spread);
    float reach = max(Reach, 1e-3);
    float amount = IncorporealDepth_BloomAmount(opening, reach, openness);
    float r = IncorporealDepth_BloomRestRadius(length(PositionWS - origin), amount, reach, openness, Phase, Clock);
    float crest = saturate(IncorporealDepth_Wave(r / reach, Phase, Clock));
    return max(_IncorporealFlow.w, 0.0) * openness * crest * crest;
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
