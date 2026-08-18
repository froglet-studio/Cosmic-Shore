Shader "Shader Graphs/ForcefieldCrackleCapsule"
{
    // The Rhino energy-sword blade's crackle overlay: identical look family to
    // "Shader Graphs/ForcefieldCrackle", but parameterized for the blade's stretched
    // capsule — impacts are object-space POSITIONS and the ripple metric is WORLD
    // UNITS (see ForcefieldCrackleCapsule_float in ForcefieldCrackle.hlsl).
    Properties
    {
        [HideInInspector] _ImpactCount ("Impact Count", Int) = 0

        [Header(Crackle Colors)]
        _CrackleColorA ("Core Arc Color", Color) = (0.7, 0.85, 1.0, 1.0)
        _CrackleColorB ("Outer Glow Color", Color) = (0.3, 0.6, 1.0, 1.0)

        [Header(Arc Pattern)]
        _ArcDensity ("Arc Count", Range(1, 20)) = 8
        _ArcSharpness ("Arc Width", Range(0.01, 0.5)) = 0.06

        [Header(Wave and Expansion)]
        _RingThickness ("Ring Thickness", Range(0.05, 1)) = 0.4
        _CenterFillAmount ("Center Fill", Range(0, 1)) = 0.15
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
            Name "ForcefieldCrackleCapsule"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One          // Additive blending
            ZWrite Off
            ZTest LEqual
            Cull Off               // Render both sides so it's visible from inside

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
                float3 viewDirOS  : TEXCOORD3;
                float3 scaleOS    : TEXCOORD4;
            };

            // Include the crackle functions
            #include "Assets/_Graphics/Materials/Graphs/ForcefieldCrackle.hlsl"

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                // Camera position in object space for proper fresnel
                float3 cameraPosOS = TransformWorldToObject(GetCameraPositionWS());
                output.viewDirOS = cameraPosOS - input.positionOS.xyz;

                // Object -> world scale per axis (the model matrix's column lengths),
                // so the fragment can measure ripple distances in WORLD units on the
                // blade's non-uniform, energy-stretched capsule.
                float4x4 m = GetObjectToWorldMatrix();
                output.scaleOS = float3(
                    length(float3(m._m00, m._m10, m._m20)),
                    length(float3(m._m01, m._m11, m._m21)),
                    length(float3(m._m02, m._m12, m._m22)));

                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                // Flip normal for back faces so fresnel works from inside the capsule
                float3 normal = input.normalOS * (facing > 0 ? 1.0 : -1.0);

                float3 emissionColor;
                float alpha;
                ForcefieldCrackleCapsule_float(input.positionOS, normal, input.viewDirOS,
                                               input.scaleOS, emissionColor, alpha);

                // Apply fog
                emissionColor = MixFog(emissionColor, input.fogFactor);

                return half4(emissionColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
