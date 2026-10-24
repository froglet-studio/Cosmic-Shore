// The hex lattice behind style-B panels and windows (Docs/MENU_REDESIGN_B.md §62, rev 11 option H): Amoebius's
// LATTICE backdrop (Port/src/CosmicShore.Launcher/LauncherApp.Look.cs DrawLattice) - pointy-top hexagons, one
// outline per cell - over frosted glass, with one of three motions a window picks when it opens. Pure maths on
// canvas-space position and time - no textures but the blurred world - so the same functions draw a panel and a
// whole window, and Tools/Build/lattice_preview.py compiles and measures them.
#ifndef COSMIC_UI_LATTICE_INCLUDED
#define COSMIC_UI_LATTICE_INCLUDED

static const float LATTICE_SQ3 = 1.7320508;
static const float LATTICE_TAU = 6.2831853;

// Distance (canvas units) from p to the outline of its hexagon, and that hexagon's centre.
// r: centre to corner. Pointy-top cells: rows 1.5 r apart, every other row shifted half a cell (r * sqrt 3).
// inset: the outline drawn at this fraction of the cell (Amoebius: 0.92), so neighbours read as separate panes.
float HexEdgeDistance(float2 p, float r, float inset, out float2 centre)
{
    float w = r * LATTICE_SQ3;
    float2 s = float2(w, r * 3.0);
    // Two interleaved rectangular grids of centres; the nearer centre owns p.
    float2 a = (floor(p / s + 0.5)) * s;
    float2 b = (floor((p - s * 0.5) / s + 0.5)) * s + s * 0.5;
    centre = dot(p - a, p - a) < dot(p - b, p - b) ? a : b;
    float2 q = abs(p - centre);
    // The pointy-top hexagon's distance function: the larger of the flat sides (x) and the slanted pair.
    float apothem = w * 0.5;
    float hexD = max(q.x, dot(q, float2(0.5, LATTICE_SQ3 * 0.5)));
    return abs(apothem * inset - hexD);
}

// Line coverage in [0, 1] for a line of the given width (canvas units).
float HexLine(float edgeDistance, float lineWidth)
{
    return 1.0 - smoothstep(lineWidth * 0.5, lineWidth * 0.5 + 1.0, edgeDistance);
}

// H2, the centre ring: one narrow bright ring rippling out from the centre (Amoebius: 0.5 + 0.5 sin(d * 14 - t * 1.2),
// raised to the sixth). d in canvas units; wavelength and period are the launcher's at 1080p.
float HexRing(float d, float t, float wavelength, float period)
{
    float wave = 0.5 + 0.5 * sin(d / wavelength * LATTICE_TAU - t / max(period, 0.01) * LATTICE_TAU);
    float w2 = wave * wave;
    return w2 * w2 * w2;
}

// H4, the tide: one soft band of light crossing the rect top to bottom and back. v: 0 bottom, 1 top.
float HexTide(float v, float t, float period, float width)
{
    float centre = 0.5 + 0.38 * sin(t / max(period, 0.01) * LATTICE_TAU);
    float x = (v - centre) / max(width, 0.001);
    return exp(-x * x);
}

// The crystallise-in reveal: r is the normalised distance from the rect's centre (0 centre, 1 corner), reveal 0..1.
// Returns (mask, front): the lattice inside the growing radius, and a bright band just inside its edge that fades
// out as the reveal completes. H6 drives it from the time since the window opened; the CanvasGroup fade drives it
// otherwise.
float2 LatticeReveal(float r, float reveal)
{
    float edge = reveal * 1.15;
    float mask = 1.0 - smoothstep(edge - 0.15, edge, r);
    float front = saturate(1.0 - abs(r - (edge - 0.075)) / 0.075) * (1.0 - reveal);
    return float2(mask, front);
}

#endif
