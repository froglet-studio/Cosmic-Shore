// FulmenBolt.shader - the Fulmen bolt (Assets/_Scripts/Controller/Environment/Fulmen). Drawn by FulmenView: one dynamic
// world-space mesh, two crossed strips per segment, vertex colour = FulmenConfigSO.Tint x the bolt's flash alpha,
// uv.x across the strip (0..1), uv.y distance along the stream (world units, offset per bolt).
//
// The brief is SUBTLE BUT EERIE, not flashy: a thin, cool core that never blows out, a faint cold halo, a slow crawl of
// noise along the stream that pinches and shifts the core, and a faint flicker. Additive, so it stacks quietly over the
// scene; HDR-ready (_Intensity / _CoreColor go past 1 for bloom, but the defaults stay low). No textures, no keywords.
Shader "CosmicShore/FulmenBolt"
{
    Properties
    {
        [HDR] _CoreColor ("Core Colour (cool white)", Color) = (0.85, 0.93, 1.0, 1)
        _Intensity ("Overall Intensity", Range(0, 8)) = 0.9
        _CoreWidth ("Core Width (share of the strip)", Range(0.01, 0.5)) = 0.07
        _CoreGain ("Core Brightness", Range(0, 4)) = 1.1
        _HaloGain ("Halo Brightness", Range(0, 2)) = 0.18
        _HaloFalloff ("Halo Falloff", Range(0.5, 8)) = 3
        _NoiseScale ("Noise Cell (world units along the stream)", Range(0.5, 64)) = 9
        _NoiseSpeed ("Noise Crawl Speed", Range(0, 20)) = 3.5
        _Wander ("Core Wander (share of the strip)", Range(0, 0.4)) = 0.12
        _Pinch ("Noise Pinch (dark gaps along the stream)", Range(0, 1)) = 0.55
        _Flicker ("Faint Flicker", Range(0, 1)) = 0.2
        _FlickerRate ("Flicker Rate", Range(1, 60)) = 17
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        LOD 100

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half _Intensity, _CoreWidth, _CoreGain, _HaloGain, _HaloFalloff;
                float _NoiseScale, _NoiseSpeed;
                half _Wander, _Pinch, _Flicker, _FlickerRate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // 1D value noise, smooth
            float Noise1(float x)
            {
                float i = floor(x), f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), u);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float along = i.uv.y / _NoiseScale;
                float t = _Time.y * _NoiseSpeed;
                float n1 = Noise1(along + t);
                float n2 = Noise1(along * 2.7 - t * 1.3 + 17.0);

                // the core wanders a little across the strip, and pinches into dark gaps where the noise dips
                float centre = 0.5 + (n1 - 0.5) * 2.0 * _Wander;
                float d = abs(i.uv.x - centre) * 2.0;                       // 0 at the core, 1 at the strip edge
                float core = exp(-pow(d / max(_CoreWidth * 2.0, 1e-3), 2.0));
                float halo = pow(saturate(1.0 - d), _HaloFalloff);
                float pinch = lerp(1.0, smoothstep(0.15, 0.75, n2), _Pinch);

                // faint, unsteady flicker (not a strobe: the bolt's own flash/fade is the vertex alpha)
                float fl = 1.0 - _Flicker * Hash11(floor(_Time.y * _FlickerRate) + i.uv.y * 0.013);

                half3 tint = i.color.rgb;
                half3 col = lerp(tint, _CoreColor.rgb, saturate(core)) * core * _CoreGain + tint * halo * _HaloGain;
                col *= pinch * fl * i.color.a * _Intensity;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
