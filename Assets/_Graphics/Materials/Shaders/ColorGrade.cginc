// The colour grade: every texel a shader draws follows the worn colour set, as every authored colour does (Garrett,
// 2026-11-10, LOCKED: every colour comes from the colour set; Docs/VesselModels/COLOR_TRACE.md). The colour trace's own
// rule, applied to every colour on an sRGB grid and blended smoothly across family borders, lives in a 33^3 table per
// set and role (Tools/Build/colour_grade.py); ColorGrade.cs builds it into a Texture3D when a set is worn and switches
// COSMIC_COLOR_GRADE on. Photic, the set the game was authored in, has none: the keyword is off and this costs nothing.
//
// A shader that grades declares the keyword itself (a pragma in an include is not one Unity reads):
//     #pragma multi_compile _ COSMIC_COLOR_GRADE
//     #include "<path>/ColorGrade.cginc"
// and passes the texture's colour (as the shader holds it: linear in a Linear project) through COSMIC_GRADE_UI (canvas
// art) or COSMIC_GRADE_WORLD (the sky). A grey is left exactly as it is, without the lookup: it keeps its value in
// every set, and most UI texels are white or grey masks.

#ifndef COSMIC_COLOR_GRADE_INCLUDED
#define COSMIC_COLOR_GRADE_INCLUDED

#if defined(COSMIC_COLOR_GRADE)

sampler3D _CosmicGradeUI;
sampler3D _CosmicGradeWorld;
float4 _CosmicGradeScaleOffset;   // x (n - 1) / n, y 0.5 / n: a colour's sRGB grid position to its texel's centre

half3 CosmicGradeLookup(sampler3D grade, half3 c)
{
    half spread = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
    UNITY_BRANCH
    if (spread < 0.002h)
        return c;
    half3 s = saturate(c);
#if !defined(UNITY_COLORSPACE_GAMMA)
    s = LinearToGammaSpace(s);
#endif
    half3 g = tex3D(grade, s * _CosmicGradeScaleOffset.x + _CosmicGradeScaleOffset.y).rgb;
#if !defined(UNITY_COLORSPACE_GAMMA)
    g = GammaToLinearSpace(g);
#endif
    return g;
}

#define COSMIC_GRADE_UI(c) CosmicGradeLookup(_CosmicGradeUI, c)
#define COSMIC_GRADE_WORLD(c) CosmicGradeLookup(_CosmicGradeWorld, c)

#else

#define COSMIC_GRADE_UI(c) (c)
#define COSMIC_GRADE_WORLD(c) (c)

#endif

#endif
