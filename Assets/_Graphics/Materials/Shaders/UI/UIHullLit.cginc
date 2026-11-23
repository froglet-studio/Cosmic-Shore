// A hull render lit for a dark card (Docs/MENU_REDESIGN_B.md §73; Shombith 2026-10-25: the pale plate behind the hull -
// "dont like the white bg"). The renders are near-black with pale trim and vanish on navy, so the body is lifted toward a
// cool grey (gain, then an added lift) and a thin white edge is drawn from the sprite's own alpha. Still: no time term.
// Tools/Build/lattice_preview.py compiles it with clang and checks the darkest body reads on the card.
#ifndef COSMIC_UI_HULL_LIT_INCLUDED
#define COSMIC_UI_HULL_LIT_INCLUDED

// rgb: the texel's colour; alpha: its alpha; alphaMin: the least alpha among its neighbours `width` texels away.
float3 HullLit(float3 rgb, float alpha, float alphaMin, float gain, float3 lift, float3 rimColour, float rimIntensity)
{
    float3 body = saturate(rgb * gain + lift);
    float rim = saturate(alpha - alphaMin);
    return saturate(body + rimColour * (rim * rimIntensity));
}

#endif
