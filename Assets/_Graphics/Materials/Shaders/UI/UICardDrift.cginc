// The card picture's drift (Docs/MENU_REDESIGN_B.md §72): a slow Lissajous path in canvas units. Sideways makes one
// loop per `seconds`, up-down a little slower, so the path never quite repeats a straight line. Never farther than
// `reach` on either axis, which is what lets KitCoverImage's overscan hide every edge. Pure maths on time -
// Tools/Build/lattice_preview.py compiles it with clang and checks the bound and the loop.
#ifndef COSMIC_UI_CARD_DRIFT_INCLUDED
#define COSMIC_UI_CARD_DRIFT_INCLUDED

static const float CARD_DRIFT_TAU = 6.2831853;

// The offset at time t: (0, 0) when seconds is 0 (held still).
float2 CardDriftOffset(float t, float seconds, float2 reach)
{
    if (seconds <= 0.0) return float2(0.0, 0.0);
    float phase = CARD_DRIFT_TAU * frac(t / seconds);
    float slow = CARD_DRIFT_TAU * frac(t / (seconds * 1.37));
    return float2(sin(phase) * reach.x, sin(slow + 1.3) * reach.y);
}

#endif
