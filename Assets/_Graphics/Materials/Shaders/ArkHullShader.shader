// ArkHullShader - the Ark's own shading (ArkModel; Docs/VesselModels/ArkReverse.md "In the game").
//
// The vessels' VesselGraph is unlit: colour = lerp(_Color1 rim, _Color2 face, facing) x _ColorMultiplier. That suits a
// 10-unit vessel seen at chase range. On a 196-unit ship it flattens the five levels of panels, ribs and jets the
// model was built with, so the Ark read as nothing like its studio render (Shombith, 2026-11-16). This shader keeps
// the SAME colours (copied at runtime from the slot's vessel material, so every colour still comes from the colour
// set) and gives them form: a key light fixed in VIEW space (upper left, toward the camera, like the studio render),
// a fill floor (high on the glowing slots: domain trim, windows, jets), a tight specular, and the vessels' rim.
// Opaque, depth-writing and double-sided, as ArkModel's copies of the vessel materials were.
Shader "CosmicShore/Ark Hull"
{
    Properties
    {
        _Color1 ("Rim (the vessel material's _Color1)", Color) = (1,1,1,1)
        _Color2 ("Face (the vessel material's _Color2)", Color) = (0,0,0,1)
        _ColorMultiplier ("Brightness", Float) = 1
        _ArkAlbedoGain ("Albedo gain", Range(0, 4)) = 2.2
        _ArkFill ("Fill (0 = lit only by the key, 1 = self-lit)", Range(0, 1)) = 0.2
        _ArkKey ("Key light", Range(0, 2)) = 1.2
        _ArkSpecular ("Specular", Range(0, 2)) = 0.8
        _ArkRim ("Rim", Range(0, 1)) = 0.5
        _ArkKeyDirection ("Key direction (view space, toward the light)", Vector) = (-0.45, 0.65, 0.6, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Unlit"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color1;
            half4 _Color2;
            float _ColorMultiplier;
            float _ArkAlbedoGain;
            float _ArkFill;
            float _ArkKey;
            float _ArkSpecular;
            float _ArkRim;
            float4 _ArkKeyDirection;
            float _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ArkHull"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalVS : TEXCOORD0;
                float3 positionVS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionVS = TransformWorldToView(positionWS);
                o.normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS), true);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half Luma(half3 c) { return dot(c, half3(0.2126h, 0.7152h, 0.0722h)); }

            half4 Frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(input.normalVS);
                n = frontFace ? n : -n;                              // the model's parts are wound both ways
                float3 v = normalize(-input.positionVS);             // view space: the camera is at the origin
                float facing = saturate(dot(n, v));

                // The slot's own colour: the brighter of its two tones (the body's gunmetal rim, the domain's face).
                half3 tone = Luma(_Color1.rgb) > Luma(_Color2.rgb) ? _Color1.rgb : _Color2.rgb;
                half3 albedo = tone * _ColorMultiplier * _ArkAlbedoGain;

                float3 l = normalize(_ArkKeyDirection.xyz);
                float lambert = saturate(dot(n, l));
                float3 h = normalize(l + v);
                float specular = pow(saturate(dot(n, h)), 40.0);

                half3 color = albedo * (_ArkFill + _ArkKey * lambert * (1.0 - _ArkFill))
                            + albedo * _ArkSpecular * specular * 4.0
                            + tone * _ColorMultiplier * _ArkRim * pow(1.0 - facing, 3.0);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(DepthAttributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
