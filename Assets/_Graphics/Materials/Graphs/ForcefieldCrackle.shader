Shader "Shader Graphs/ForcefieldCrackle"
{
    Properties
    {
        [HideInInspector] _ImpactCount ("Impact Count", Int) = 0

        [Header(Crackle Colors)]
        _CrackleColorA ("Primary Color", Color) = (0.3, 0.6, 1.0, 1.0)
        _CrackleColorB ("Secondary Color", Color) = (0.8, 0.9, 1.0, 1.0)

        [Header(Voronoi Pattern)]
        _VoronoiCellSize ("Cell Size", Range(2, 40)) = 12
        _EdgeSharpness ("Edge Sharpness", Range(0.01, 0.5)) = 0.15

        [Header(Ring and Wave)]
        _RingThickness ("Ring Thickness", Range(0.05, 1)) = 0.4
        _CenterFillAmount ("Center Fill", Range(0, 1)) = 0.3
        _RippleSpeed ("Ripple Speed", Range(0.2, 3)) = 1

        [Header(Fresnel Rim)]
        _FresnelRimColor ("Rim Color", Color) = (0.3, 0.5, 0.8, 1.0)
        _FresnelRimIntensity ("Rim Intensity", Range(0, 0.5)) = 0.08
        _FresnelRimPower ("Rim Power", Range(1, 8)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForcefieldCrackle"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One          // Additive blending
            ZWrite Off
            ZTest LEqual
            Cull Off               // Render both faces

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            // Include the crackle function
            #include "Assets/_Graphics/Materials/Graphs/ForcefieldCrackle.hlsl"

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 emissionColor;
                float alpha;
                ForcefieldCrackle_float(input.positionOS, input.normalOS, emissionColor, alpha);

                // Apply fog
                emissionColor = MixFog(emissionColor, input.fogFactor);

                return half4(emissionColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
