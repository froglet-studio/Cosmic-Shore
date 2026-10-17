// The elemental rim (Docs/MENU_REDESIGN_B.md §45.3): a comet of an element's light running round a panel's border,
// or - omni - the four elements blended round it, turning. Pure maths on the pixel's place on its rect (KitRimMesh
// writes it into UV1) and time; Tools/Build/lattice_preview.py compiles and checks it with clang.
#ifndef COSMIC_UI_ELEMENT_RIM_INCLUDED
#define COSMIC_UI_ELEMENT_RIM_INCLUDED

// The four element colours blended round the rim, from an angle in turns.
float3 RimOmniColour(float a, float3 charge, float3 mass, float3 space, float3 time)
{
    float k = frac(a) * 4.0;
    float i = floor(k);
    float3 c0 = i < 1.0 ? charge : (i < 2.0 ? mass : (i < 3.0 ? space : time));
    float3 c1 = i < 1.0 ? mass : (i < 2.0 ? space : (i < 3.0 ? time : charge));
    return lerp(c0, c1, smoothstep(0.0, 1.0, frac(k)));
}

// rect01: the pixel's place over its rect (0..1); size: the rect in canvas units; speed: laps per second;
// tail: the comet's length in turns; baseLevel: how lit the rim is away from the comet. Returns (rgb, level).
float4 ElementRim(float2 rect01, float2 size, float t, float speed, float tail, float baseLevel, float omni,
                  float3 element, float3 charge, float3 mass, float3 space, float3 time)
{
    float2 d = (rect01 - 0.5) * size;
    float angle = atan2(d.y, d.x) * 0.15915494 + 0.5;
    float head = frac(t * speed);
    float behind = frac(head - angle);
    float comet = 1.0 - smoothstep(0.0, tail, behind);
    comet *= comet;
    float3 colour = omni > 0.5 ? RimOmniColour(angle + t * speed * 0.35, charge, mass, space, time) : element;
    float level = baseLevel + comet;
    float3 rgb = colour * level + float3(1.0, 1.0, 1.0) * (comet * comet * comet * 0.55);
    return float4(rgb, saturate(level));
}

#endif
