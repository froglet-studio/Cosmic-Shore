// SpindleStandin.hlsl - cheaper drop-ins for the Voronoi node of the spindle lace
// (SpindleGraph, SpindleDepthGraph, SpindleLodMidGraph, SpindleWireGraph, InverseDynamicFresnelGraph).
//
// Measured offline by the spindle stand-in study (2026-11-10, sg2glsl + llvmpipe): reference is Unity's own
// Unity_Voronoi_Deterministic_float (VoronoiNode m_SGVersion 1, HashType Deterministic = Hash_Tchou_2_2).
//
// WHY A 2x2 SEARCH IS ENOUGH. The lace's AngleOffset is sin(_Time.y + _Phase), so |angle| <= 1, and the
// node's feature point in each cell is (sin(h.y*angle), cos(h.x*angle))*0.5+0.5 with h in [0,1]:
// x in [0.5, 0.92] when angle >= 0 (in [0.08, 0.5] when angle < 0) and y in [0.77, 1.0]. So the row
// above (+1) can never be nearest, and of the three columns only 0 and -sign(angle) can be (one sample in
// ~150,000 picks the third, max error 0.026 in distance). Sign(angle) is per draw, so no divergence.
//
// Shader Graph usage: Custom Function node, Type = File, Source = this file, Name WITHOUT the _float suffix.
//   SpindleVoronoi4   inputs  UV (Vector2), AngleOffset (Float), CellDensity (Float)
//                     outputs Out (Float)
//   -> replaces the "Voronoi" node (same three inputs, wire Out where the node's Out went; Cells is unused
//      in every spindle graph).
//   SpindleVoronoiAngle0  inputs UV (Vector2), CellDensity (Float); outputs Out (Float)
//   -> replaces the AngleOffset-unconnected Voronoi in InverseDynamicFresnelGraph (exact closed form).

#ifndef SPINDLE_STANDIN_INCLUDED
#define SPINDLE_STANDIN_INCLUDED

// Hash_Tchou_2_2_float, copied from com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl so the
// file does not depend on the Voronoi node having pulled that include in.
float2 SpindleStandin_Tchou22(float2 i)
{
    uint2 v = (uint2)(int2)round(i);
    v.y ^= 1103515245U;
    v.x += v.y;
    v.x *= v.y;
    v.x ^= v.x >> 5u;
    v.x *= 0x27d4eb2du;
    v.y ^= (v.x << 3u);
    return (v >> 8) * (1.0 / float(0x00ffffff));
}

// 2x2 search, polynomial sin/cos (|h*angle| <= 1: sin err <= 0.0081, cos err <= 0.0014 before the 0.5 scale),
// squared distances, one sqrt.
void SpindleVoronoi4_float(float2 UV, float AngleOffset, float CellDensity, out float Out)
{
    float2 p = UV * CellDensity;
    float2 g = floor(p);
    float2 f = p - g;
    float sx = AngleOffset >= 0.0 ? -1.0 : 1.0;
    float2 h0 = SpindleStandin_Tchou22(g + float2(0.0, -1.0));
    float2 h1 = SpindleStandin_Tchou22(g + float2(sx, -1.0));
    float2 h2 = SpindleStandin_Tchou22(g);
    float2 h3 = SpindleStandin_Tchou22(g + float2(sx, 0.0));
    float4 s = float4(h0.y, h1.y, h2.y, h3.y) * AngleOffset;
    float4 c = float4(h0.x, h1.x, h2.x, h3.x) * AngleOffset;
    float4 ox = 0.5 + 0.5 * s * (1.0 - s * s * (1.0 / 6.0));
    float4 c2 = c * c;
    float4 oy = 1.0 - 0.25 * c2 + c2 * c2 * (1.0 / 48.0);
    float4 dx = float4(0.0, sx, 0.0, sx) + ox - f.x;
    float4 dy = float4(-1.0, -1.0, 0.0, 0.0) + oy - f.y;
    float4 d2 = dx * dx + dy * dy;
    Out = sqrt(min(min(d2.x, d2.y), min(d2.z, d2.w)));
}
void SpindleVoronoi4_half(half2 UV, half AngleOffset, half CellDensity, out half Out)
{
    float o; SpindleVoronoi4_float(UV, AngleOffset, CellDensity, o); Out = o;
}

// Exact variant (bit-identical to the node in our tests): rows {-1, 0} x columns {-1, 0, 1}, true sin/cos.
void SpindleVoronoi6_float(float2 UV, float AngleOffset, float CellDensity, out float Out)
{
    float2 p = UV * CellDensity;
    float2 g = floor(p);
    float2 f = p - g;
    float res = 8.0;
    [unroll] for (int y = -1; y <= 0; y++)
    [unroll] for (int x = -1; x <= 1; x++)
    {
        float2 lat = float2(x, y);
        float2 h = SpindleStandin_Tchou22(lat + g);
        float2 o = float2(sin(h.y * AngleOffset), cos(h.x * AngleOffset)) * 0.5 + 0.5;
        res = min(res, distance(lat + o, f));
    }
    Out = res;
}

// Inverse/DynamicFresnel fringe (InverseDynamicFresnelGraph): its Voronoi has AngleOffset unconnected (0), so
// every feature point sits at (0.5, 1.0) and the node is EXACTLY a square-lattice distance, no hash at all.
void SpindleVoronoiAngle0_float(float2 UV, float CellDensity, out float Out)
{
    float2 q = UV * CellDensity - float2(0.5, 0.0);
    Out = length(q - floor(q + 0.5));
}
void SpindleVoronoiAngle0_half(half2 UV, half CellDensity, out half Out)
{
    float o; SpindleVoronoiAngle0_float(UV, CellDensity, o); Out = o;
}

#endif
