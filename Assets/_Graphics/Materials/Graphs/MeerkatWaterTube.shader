// MeerkatWaterTube.shader — the CHEAP mobile water pipe for every Meerkat wormhole tube (Yash, 2026-10-28: "a transparent
// watery tube, mobile friendly, an illusion of a wormhole rather than real lensing"). The candidate replacement for the
// full-screen contour-field lens (MeerkatWormholeLens.shader, 256 ray-march steps) + the additive calm wall
// (MeerkatTubeWall.shader). It is judged side by side with them in the Lavalamp lab
// (Assets/_Scenes/Game_TestDesign/LavalampWormholeLab.unity, MeerkatTubeLook); the game keeps the original look until
// Yash picks.
// One pass, one draw per tube, no screen copy, no grab pass, no textures: a near-white, mostly clear alpha-blended skin
// with a Fresnel rim, slow flow bands running from the way in to the way out, a cheap two-sine caustic shimmer and an
// optional twist, fading near the camera (so the wall right beside the rider never fogs the view) and at the live
// window's fronts (_F0.._F1, as the calm wall). ~30 ALU per pixel, half precision where it can be.
// Vertex data (MeerkatTreeMesh.BuildTubeWall, the calm wall's mesh): uv0.x = the share of the way along (0..1),
// uv1.y = the share of the way round (0..1). Positions are in world space. The recipe: Docs/Shaders/WATERY_GLASS.md.
Shader "CosmicShore/MeerkatWaterTube"
{
    Properties
    {
        _Tint ("Water tint (near white)", Color) = (0.92, 0.97, 1, 1)
        _Domain ("Maker's domain colour (set per tube)", Color) = (0.31, 0.85, 1, 1)
        _DomainMix ("How much of the domain colour tints the rim", Range(0, 1)) = 0.15
        _Clarity ("Body opacity (0 = glass clear)", Range(0, 1)) = 0.04
        _Rim ("Rim opacity", Range(0, 1)) = 0.45
        _RimPower ("Rim power", Range(0.5, 8)) = 3
        _Flow ("Flow band strength", Range(0, 1)) = 0.12
        _FlowSpeed ("Flow speed (bands per second)", Float) = 0.6
        _FlowScale ("Flow band spacing (u)", Float) = 24
        _Caustic ("Caustic shimmer", Range(0, 1)) = 0.25
        _CausticScale ("Caustic size (u)", Float) = 9
        _Twist ("Twist (turns per 100 u)", Float) = 0.15
        _NearFade ("Near fade: clear inside (x), full from (y)", Vector) = (2, 14, 0, 0)
        _Alpha ("Fade (set per tube)", Float) = 1
        _F0 ("Window start (share)", Float) = 0
        _F1 ("Window end (share)", Float) = 1
        _Edge ("Window fronts (share)", Float) = 0.06
        _L ("Tube length (u)", Float) = 100
        _T ("Time (s)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Front = inside faces only, if the wall winds outward)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+3" "IgnoreProjector" = "True" }

        Pass
        {
            Name "MeerkatWaterTube"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex MeerkatWaterTubeVert
            #pragma fragment MeerkatWaterTubeFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _Domain;
                half _DomainMix, _Clarity, _Rim, _RimPower, _Flow, _Caustic;
                float _FlowSpeed, _FlowScale, _CausticScale, _Twist;
                float4 _NearFade;
                float _Alpha, _F0, _F1, _Edge, _L, _T, _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;   // aS, -1, 0, 2 (MeerkatTreeMesh's layout)
                float2 uv1 : TEXCOORD1;   // 0, the share of the way round
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;   // unnormalised: its length is the distance to the eye
                float2 sp : TEXCOORD2;       // the share along, the share round
            };

            Varyings MeerkatWaterTubeVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = GetWorldSpaceViewDir(positionWS);
                o.sp = float2(input.uv0.x, input.uv1.y);
                return o;
            }

            half4 MeerkatWaterTubeFrag(Varyings i) : SV_Target
            {
                float vS = i.sp.x;
                // the tube's live window: nothing outside it, condensing at its fronts (as the calm wall)
                if (vS < _F0 || vS > _F1) discard;
                half win = (half)smoothstep(0.0, _Edge, min(vS - _F0, _F1 - vS));

                float dist = length(i.viewWS);
                half nearA = (half)smoothstep(_NearFade.x, max(_NearFade.y, _NearFade.x + 0.01), dist);

                // Fresnel from |n.v|, so it reads the same whichever way the wall winds
                half nv = (half)abs(dot(normalize(i.normalWS), i.viewWS / max(dist, 1e-4)));
                half fr = pow(1.0h - nv, (half)_RimPower);

                float along = vS * _L;                                   // u along the tube
                float ang = i.sp.y * 6.2831853 + along * _Twist * 0.0628319 + _T * 0.35;   // twisted angle round it
                // flow bands: soft rings sliding from the way in to the way out
                half band = (half)(0.5 + 0.5 * sin((along / max(_FlowScale, 0.5) - _T * _FlowSpeed) * 6.2831853));
                band *= band;
                // caustic shimmer: two crossed sines, sharpened (integer multiples of the angle, so no seam round the tube)
                float k = 6.2831853 / max(_CausticScale, 0.5);
                half c1 = (half)sin(ang * 7.0 + along * k - _T * 1.3);
                half c2 = (half)sin(ang * 5.0 - along * k * 0.73 + _T * 0.9);
                half caustic = saturate(c1 * c2);
                caustic *= caustic;

                half3 rimCol = lerp(_Tint.rgb, _Domain.rgb, _DomainMix);
                half3 col = lerp(_Tint.rgb, rimCol, fr) + caustic * _Caustic * 0.6h + band * _Flow * 0.3h;
                half a = saturate(_Clarity + _Rim * fr + band * _Flow * 0.5h + caustic * _Caustic * 0.35h);
                a *= win * nearA * (half)_Alpha;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
