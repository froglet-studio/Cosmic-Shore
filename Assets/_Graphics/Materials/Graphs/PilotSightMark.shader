// The PILOT SIGHT's mark (Docs/Pilots/CALLSIGNS.md §3): the Dolphin's Echo Sight halo
// (EchoSightHalo.shader), INSTANCED, so one draw marks every crystal, plant, creature or vessel the
// pilot sees. Those can number in the hundreds; EchoSightHalo takes one draw per mark because its
// colour and radius are per-material, which is right for a dozen vessels and wrong for hundreds of
// hearts. Same look: an additive soft disc with a hard ring, ZTest Always so it reads through mass,
// sized in world units with a floor in screen space so a far mark stays legible.
//
// Per instance: _MarkColor (rgb, a = intensity) and _MarkRadius (world units). Drawn by
// PilotSightMarks through Graphics.RenderMeshInstanced on Unity's built-in quad (corners at ±0.5).
Shader "CosmicShore/PilotSightMark"
{
    Properties
    {
        _MinScreenRadius ("Min Screen Radius (NDC half-height)", Range(0, 0.5)) = 0.02
        _RingPos ("Ring Position (0 centre .. 1 edge)", Range(0, 1)) = 0.45
        _RingWidth ("Ring Width", Range(0.01, 0.6)) = 0.12
        _RingGain ("Ring Gain", Float) = 1.6
        _GlowFalloff ("Glow Falloff", Float) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Overlay"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "PilotSightMark"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 disc       : TEXCOORD0;   // -1..1 across the quad
                float4 color      : TEXCOORD1;   // rgb, a = intensity
            };

            CBUFFER_START(UnityPerMaterial)
                float _MinScreenRadius;
                float _RingPos;
                float _RingWidth;
                float _RingGain;
                float _GlowFalloff;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PilotSightMarkProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _MarkColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _MarkRadius)
            UNITY_INSTANCING_BUFFER_END(PilotSightMarkProps)

            Varyings vert (Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float2 corner = input.positionOS.xy * 2.0;
                float radius = UNITY_ACCESS_INSTANCED_PROP(PilotSightMarkProps, _MarkRadius);

                // Billboard about the instance's origin, as EchoSightHalo does.
                float3 centreWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float4 centreCS = TransformWorldToHClip(centreWS);
                float w = max(abs(centreCS.w), 1e-4);
                float worldNdc = radius * UNITY_MATRIX_P._m11 / w;
                float r = max(worldNdc, _MinScreenRadius);
                float2 offset = float2(r * (_ScreenParams.y / _ScreenParams.x), r);
                centreCS.xy += corner * offset * centreCS.w;

                output.positionCS = centreCS;
                output.disc = corner;
                output.color = UNITY_ACCESS_INSTANCED_PROP(PilotSightMarkProps, _MarkColor);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float d = saturate(length(input.disc));
                float glow = pow(saturate(1.0 - d), max(_GlowFalloff, 0.01));
                float t = (d - _RingPos) / max(_RingWidth, 0.001);
                float ring = exp(-t * t);
                float amount = (glow + ring * _RingGain) * max(input.color.a, 0.0);
                return half4(input.color.rgb * amount, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
