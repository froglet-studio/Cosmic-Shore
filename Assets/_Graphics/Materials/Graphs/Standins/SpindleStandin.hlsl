// SpindleStandin.hlsl - the spindle lace's Voronoi, searching only the 6 cells that can win instead of 9.
// Used by SpindleGraph, SpindleDepthGraph, SpindleLodMidGraph, SpindleLodFarGraph and SpindleWireGraph
// (a file-mode Custom Function node, Name "SpindleVoronoi6"; the LOD graphs are regenerated from SpindleGraph by
// Tools/Shaders/wire_spindle_lod.py).
//
// Reference: Unity's own Unity_Voronoi_Deterministic_float (VoronoiNode m_SGVersion 1, HashType Deterministic =
// Hash_Tchou_2_2), a 3x3 search. The lace's AngleOffset is sin(_Time.y + _Phase), so |angle| <= 1, and the node's
// feature point in a cell is (sin(h.y*angle), cos(h.x*angle))*0.5+0.5 with h in [0,1]: y lies in [0.77, 1.0], so
// the row above (+1) can never be nearest. Rows {-1, 0} x columns {-1, 0, 1} give the same nearest distance.
//
// Proven win W2 (Docs/ProvenWin/WINS.md, 2026-11-12): measured in real Unity on an RTX 3080, the spindle draws
// 1.21-1.24x faster on D3D12, Vulkan and D3D11, B faster in 200 of 200 paired samples; across 177 M pixels per API,
// about 100 pixels move by one display level (0-255), none by more (float rounding of a reordered min).
// The stand-in study's 2x2 search with polynomial sin/cos (draft PR #1878) was 1.5x but moved pixels by up to 140
// levels in real Unity, so it was not taken.

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

// Rows {-1, 0} x columns {-1, 0, 1}, the node's own hash and true sin/cos.
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

void SpindleVoronoi6_half(half2 UV, half AngleOffset, half CellDensity, out half Out)
{
    float o; SpindleVoronoi6_float(UV, AngleOffset, CellDensity, o); Out = o;
}

#endif // SPINDLE_STANDIN_INCLUDED
