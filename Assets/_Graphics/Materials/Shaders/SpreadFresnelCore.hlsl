// SpreadFresnelCore.hlsl — the shared body of the Spread Fresnel crystal shaders.
//
// This is the "base class". Unity shaders have no inheritance (UsePass can reuse a whole pass
// but cannot add to one), so a family of shaders shares its core through an include. Each
// shader in the family includes this and adds only what it alone needs:
//
//   SpreadFresnelShader.shader      the elemental crystals (Space, Time). The core, nothing else.
//   OmniCrystalFresnelShader.shader the omni crystal's body. The core, plus the CrystalMorph
//                                   vertex path the Scarab's crystal->ball forge stamps, plus an
//                                   _Opacity / _opacity dissolve (FadeIn's bloom-in, the forge's
//                                   tail).
//
// Change the LOOK here and every crystal in the family follows. Add a feature to one crystal in
// its own shader file, so the elementals are never paying for, or exposed to, the omni's extras.

#ifndef SPREAD_FRESNEL_CORE_INCLUDED
#define SPREAD_FRESNEL_CORE_INCLUDED

float3 _Spread;
float _FresnelPower;
float4 _BrightColor, _DarkColor;

// Pushes a vertex out along its normal by _Spread WORLD units (divided by the object's own
// scale, so the push does not grow with the transform).
float3 SpreadFresnelDisplace(float3 positionOS, float3 normalOS)
{
    float3 objectScale = float3(length(unity_ObjectToWorld._m00_m01_m02), length(unity_ObjectToWorld._m10_m11_m12), length(unity_ObjectToWorld._m20_m21_m22));
    // normalize: a blend-shape mesh hands the vertex stage a blended normal that is not unit
    // length (spacecrystalanim.fbx's stacked keys reach ~2x), and the push must not scale with it.
    float3 spreadedNormal = normalize(normalOS) * (_Spread / objectScale);
    return positionOS + spreadedNormal;
}

// The face colour: dark where the face looks at the camera, bright toward the rim.
half4 SpreadFresnelColor(float3 worldPos, float3 worldNormal)
{
    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
    float fresnel = (1.0 + dot(viewDir, worldNormal))/2;
    half4 col = lerp( _BrightColor, _DarkColor, fresnel);
    return col;
}

#endif // SPREAD_FRESNEL_CORE_INCLUDED
