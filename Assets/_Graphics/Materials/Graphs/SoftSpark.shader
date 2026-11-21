// SoftSpark.shader — a round, hot-cored additive spark for billboard particles.
//
// The ball's particle systems were drawn with URP Particles/Unlit and NO texture, which renders a
// solid SQUARE quad per particle — so the wake dust, the payload aura and the strike burst all read
// as confetti. This is the one-line fix every spark in that family needs: a radial falloff from the
// quad's centre with a small white-hot core, tinted by the particle's vertex colour (which carries
// the system's startColor x colorOverLifetime, so fades still fade).
//
// Additive (Blend One One): can only add light, needs no sort order, never darkens what it crosses.
// No texture fetch; ~8 ALU.

Shader "CosmicShore/SoftSpark"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 1.4
        _CoreSize ("Core Size", Range(0.02, 0.5)) = 0.14
        _Falloff ("Glow Falloff", Float) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "SoftSpark"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _CoreSize;
                float _Falloff;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * 2.0 - 1.0;
                output.color = input.color;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float d = saturate(length(input.uv));
                float glow = pow(saturate(1.0 - d), max(_Falloff, 0.01));
                float t = d / max(_CoreSize, 1e-3);
                float core = exp(-t * t);

                float3 tint = input.color.rgb;
                float3 rgb = (tint * glow + lerp(tint, float3(1.0, 1.0, 1.0), 0.8) * core)
                           * input.color.a * max(_Intensity, 0.0);
                return half4(rgb, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
