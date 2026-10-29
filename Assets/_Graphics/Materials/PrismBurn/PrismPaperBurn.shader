// PrismPaperBurn.shader - a burning prism drawn as a hollow PAPER cuboid that chars, glows and curls into fire.
// Drawn only by PrismBurnView (Assets/_Scripts/Controller/Managers/PrismBurnView.cs) as Graphics.RenderMeshInstanced
// PROXIES over the burning prisms - the prisms' own Entities Graphics prototypes are never touched, so prism batching is
// exactly what it was. The proxy mesh is a unit cube (object space -0.5..0.5) scaled onto the prism's authored bounds, so
// every term below is size-independent.
//
// Per instance:  _Burn = (progress 0..1, seed, coverage 0..1, flags)
//   progress  burn front: 0 = fresh paper, 1 = gone. A hole opens where progress*_Span passes the burn FIELD (distance
//             from a seeded ignition point + noise), with an ember rim, a char band and a browning band ahead of it.
//   coverage  dithered screen-door coverage, for the NEAR <-> MID <-> FAR crossfades and the ignite fade-in.
//   flags     +1 INVERT the dither (MID draws the complement of NEAR, so the two tiers never overlap or gap)
//             +2 NO HOLES (a convert burn: the prism survives, so it chars but never burns through)
//
// Tiers: the NEAR material enables _BURN_CURL (vertex curl + flutter, 2-octave noise, on a subdivided cube); the MID
// material leaves it off (no displacement, 1 octave, 24-vertex cube). FAR draws nothing.
// Conventions follow PrismSlice.shader: opaque alpha-clip in the AlphaTest queue, never transparent; Cull Off (paper has
// an inside); a DepthOnly pass with the same clip and displacement. Lit by the main light only, with a cheap wrap term.
Shader "CosmicShore/PrismPaperBurn"
{
    Properties
    {
        _PaperColor ("Paper", Color) = (0.86, 0.83, 0.76, 1)
        _PaperInside ("Paper Inside (backfaces)", Color) = (0.55, 0.5, 0.44, 1)
        _ScorchColor ("Scorch (browning)", Color) = (0.55, 0.33, 0.14, 1)
        _CharColor ("Char", Color) = (0.035, 0.028, 0.025, 1)
        [HDR] _EmberColor ("Ember Rim", Color) = (6.0, 1.7, 0.35, 1)
        [HDR] _EmberHot ("Ember Rim Hot Core", Color) = (9.0, 5.5, 2.0, 1)
        _Span ("Burn Span (progress gain)", Range(1, 2)) = 1.3
        _EmberWidth ("Ember Rim Width", Range(0.005, 0.15)) = 0.035
        _CharWidth ("Char Band Width", Range(0.01, 0.4)) = 0.11
        _ScorchWidth ("Browning Band Width", Range(0.01, 0.6)) = 0.22
        _NoiseFreq ("Edge Noise Frequency", Range(0.5, 16)) = 4.5
        _NoiseAmount ("Edge Noise Amount", Range(0, 1)) = 0.38
        _EmberFlicker ("Ember Flicker", Range(0, 1)) = 0.35
        _Curl ("Curl (outward roll of burnt edges)", Range(0, 1.5)) = 0.42
        _Shrink ("Shrink (burnt edges pull in)", Range(0, 1)) = 0.18
        _Flutter ("Flutter (heat wobble)", Range(0, 0.2)) = 0.035
        _Ambient ("Ambient", Range(0, 1)) = 0.42
        [Toggle(_BURN_CURL)] _BurnCurl ("Near Tier (curl + detail)", Float) = 1
        [HideInInspector] _Burn ("Burn (per instance)", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Cull Off

        HLSLINCLUDE
        #pragma target 3.5
        #pragma multi_compile_instancing
        #pragma shader_feature_local _BURN_CURL
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _PaperColor, _PaperInside, _ScorchColor, _CharColor, _EmberColor, _EmberHot;
            half _Span, _EmberWidth, _CharWidth, _ScorchWidth, _NoiseFreq, _NoiseAmount, _EmberFlicker;
            half _Curl, _Shrink, _Flutter, _Ambient;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(PaperBurnProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Burn)
        UNITY_INSTANCING_BUFFER_END(PaperBurnProps)

        float3 Hash33(float3 p)
        {
            p = frac(p * float3(0.1031, 0.1030, 0.0973));
            p += dot(p, p.yxz + 33.33);
            return frac((p.xxy + p.yxx) * p.zyx);
        }

        float Hash31(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float ValueNoise(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            float3 u = f * f * (3.0 - 2.0 * f);
            float a = Hash31(i), b = Hash31(i + float3(1, 0, 0)), c = Hash31(i + float3(0, 1, 0)), d = Hash31(i + float3(1, 1, 0));
            float e = Hash31(i + float3(0, 0, 1)), g = Hash31(i + float3(1, 0, 1)), h = Hash31(i + float3(0, 1, 1)), k = Hash31(i + 1.0);
            return lerp(lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y), lerp(lerp(e, g, u.x), lerp(h, k, u.x), u.y), u.z);
        }

        // The burn FIELD at an object-space point: 0 at the ignition point, ~1 at the far corner, roughened by noise.
        // The front is where progress*_Span crosses it.
        float BurnField(float3 posOS, float seed)
        {
            float3 origin = (Hash33(seed.xxx * 17.13 + 3.7) - 0.5);          // a seeded point inside the unit cube...
            origin = origin / max(max(abs(origin.x), abs(origin.y)), max(abs(origin.z), 1e-3)) * 0.5;   // ...pushed to its surface
            float d = length(posOS - origin) * 0.5774;                          // / sqrt(3): 0..~1
            float3 q = posOS * _NoiseFreq + seed * 7.31;
            float n = ValueNoise(q);
        #if defined(_BURN_CURL)
            n = n * 0.68 + ValueNoise(q * 2.63 + 11.0) * 0.32;
        #endif
            return d * (1.0 - _NoiseAmount) + n * _NoiseAmount;
        }

        // How far the front is PAST this point: > 0 burned through, 0 the rim, < 0 still ahead of it.
        float BurnFront(float3 posOS, float4 burn)
        {
            float progress = burn.x;
            if (fmod(burn.w, 4.0) >= 2.0) progress = min(progress, 0.3);       // NO HOLES: chars, never burns through
            return progress * _Span - BurnField(posOS, burn.y);
        }

        // 4x4 ordered dither, 0..1
        static const float kBayer4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        float Bayer4(float2 pixel)
        {
            uint2 p = (uint2)pixel & 3u;
            return (kBayer4[p.x + p.y * 4u] + 0.5) / 16.0;
        }

        void DitherClip(float2 pixel, float4 burn)
        {
            float d = Bayer4(pixel);
            bool invert = fmod(burn.w, 2.0) >= 1.0;
            float keep = invert ? (d - (1.0 - burn.z)) : (burn.z - d);
            clip(keep);
        }

        // Burnt edges roll OUTWARD and pull in, the way a sheet curls away from its own flame; a faint heat flutter rides on top.
        float3 Displace(float3 posOS, float3 normalOS, float4 burn)
        {
        #if defined(_BURN_CURL)
            float front = BurnFront(posOS, burn);
            float c = saturate((front + _CharWidth + _ScorchWidth * 0.5) / (_CharWidth + _ScorchWidth * 0.5 + 0.12));
            c *= c;
            float flutter = sin(_Time.y * 9.0 + dot(posOS, float3(13.1, 7.7, 5.3)) + burn.y * 6.0) * _Flutter * c;
            posOS += normalOS * (c * _Curl * 0.5 + flutter) - posOS * (c * _Shrink);
        #endif
            return posOS;
        }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posOS : TEXCOORD0;      // UNDISPLACED: the burn pattern is painted on the paper, not on space
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 burn = UNITY_ACCESS_INSTANCED_PROP(PaperBurnProps, _Burn);
                float3 p = Displace(input.positionOS.xyz, input.normalOS, burn);
                o.positionCS = TransformObjectToHClip(p);
                o.posOS = input.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 Frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 burn = UNITY_ACCESS_INSTANCED_PROP(PaperBurnProps, _Burn);
                DitherClip(i.positionCS.xy, burn);

                float front = BurnFront(i.posOS, burn);
                clip(-front);                                                    // burned through

                // bands, from the rim back into fresh paper
                float rim = saturate(1.0 + front / _EmberWidth);                 // 1 at the hole, 0 one rim-width back
                float charT = saturate(1.0 + (front + _EmberWidth) / _CharWidth);
                float scorchT = saturate(1.0 + (front + _EmberWidth + _CharWidth) / _ScorchWidth);

                half3 paper = isFront ? _PaperColor.rgb : _PaperInside.rgb;
                half3 albedo = lerp(paper, _ScorchColor.rgb, scorchT * scorchT);
                albedo = lerp(albedo, _CharColor.rgb, smoothstep(0.0, 1.0, charT));

                float3 n = normalize(i.normalWS) * (isFront ? 1.0 : -1.0);
                Light light = GetMainLight();
                half wrap = saturate(dot(n, light.direction) * 0.6 + 0.4);
                half3 col = albedo * (_Ambient + wrap * light.color * (1.0 - _Ambient));

                // ember rim: flickers along its length, hottest right at the hole
                float flick = 1.0 - _EmberFlicker * ValueNoise(i.posOS * 9.0 + float3(0, _Time.y * 6.0, burn.y * 5.0));
                half3 ember = lerp(_EmberColor.rgb, _EmberHot.rgb, rim * rim) * (rim * flick);
                // the char band smoulders faintly behind the rim
                ember += _EmberColor.rgb * 0.06 * saturate(charT - rim) * flick * (1.0 - step(2.0, fmod(burn.w, 4.0)) * 0.5);
                col += ember * step(0.001, burn.x);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 posOS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DVaryings DepthVert(DAttributes input)
            {
                DVaryings o = (DVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 burn = UNITY_ACCESS_INSTANCED_PROP(PaperBurnProps, _Burn);
                o.positionCS = TransformObjectToHClip(Displace(input.positionOS.xyz, input.normalOS, burn));
                o.posOS = input.positionOS.xyz;
                return o;
            }

            half DepthFrag(DVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 burn = UNITY_ACCESS_INSTANCED_PROP(PaperBurnProps, _Burn);
                DitherClip(i.positionCS.xy, burn);
                clip(-BurnFront(i.posOS, burn));
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
