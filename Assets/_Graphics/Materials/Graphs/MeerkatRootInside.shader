// MeerkatRootInside.shader — the Meerkat's ROOTS seen from INSIDE (R_VesselActions/MEERKAT.md §7), the Vessel Studio's
// rootInMat (Docs/Studios/VesselStudio/meerkat.html; round 20 made it an underground world, then Yash asked for it calm,
// 2026-10-28: "there should not be any black colored fill ... the inner tube need to be very soft ... calming, inviting"):
// a soft, smooth inner tube - one slow warm tone - a little dense (_Op), denser at its silhouette, the world outside still
// showing through, lit by the rider's lamp (_Lamp, falling off over _Fall u from _HullP), fading to CLEAR _Fog u off.
// Each root's ends stay open (_Lo0/_Lo1: 0 at an opening, a little at a fork, over _EndW u), so you see on into the next root and the light at a tip. Drawn only while the local pilot is inside this
// network. Positions are in world space. The recipe and its family: Docs/Shaders/WATERY_GLASS.md.
Shader "CosmicShore/MeerkatRootInside"
{
    Properties
    {
        _F0 ("Window start (share)", Float) = 0
        _F1 ("Window end (share)", Float) = 1
        _Edge ("Window fronts (share)", Float) = 0.05
        _Len ("Root length (u)", Float) = 100
        _EndW ("Open ends over (u)", Float) = 12
        _Lo0 ("Opacity at the start end", Float) = 0
        _Lo1 ("Opacity at the far end", Float) = 0
        _Op ("Opacity", Float) = 0.94
        _Lamp ("Lamp", Float) = 1
        _Fall ("Lamp falls off over (u)", Float) = 80
        _Fog ("Dark after (u)", Float) = 160
        _HullP ("Lamp position", Vector) = (0, 0, 0, 0)
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+8" "IgnoreProjector" = "True" }

        Pass
        {
            Name "MeerkatRootInside"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex MeerkatRootInVert
            #pragma fragment MeerkatRootInFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _F0, _F1, _Edge, _Len, _EndW, _Lo0, _Lo1, _Op, _Lamp, _Fall, _Fog, _Seed;
                float4 _HullP;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;   // aS, ...
                float2 uv1 : TEXCOORD1;   // aB, aPhi
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 sPhi : TEXCOORD2;
            };

            Varyings MeerkatRootInVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.sPhi = float2(input.uv0.x, input.uv1.y);
                return o;
            }

            float H(float3 q) { return frac(sin(dot(q, float3(127.1, 311.7, 74.7)) + _Seed) * 43758.5453); }
            float VN(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(H(i), H(i + float3(1, 0, 0)), f.x), lerp(H(i + float3(0, 1, 0)), H(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(H(i + float3(0, 0, 1)), H(i + float3(1, 0, 1)), f.x), lerp(H(i + float3(0, 1, 1)), H(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half4 MeerkatRootInFrag(Varyings i) : SV_Target
            {
                float vS = i.sPhi.x, vPhi = i.sPhi.y;
                if (vS < _F0 || vS > _F1) discard;
                float w = smoothstep(0.0, _Edge, min(vS - _F0, _F1 - vS)), sl = vS * _Len;
                float ends = lerp(_Lo0, 1.0, smoothstep(0.0, _EndW, sl)) * lerp(_Lo1, 1.0, smoothstep(0.0, _EndW, _Len - sl));   // its ends stay open
                // Yash, 2026-10-28: no black fill. The inner tube is soft and smooth - one slow warm tone, no fibres - a little
                // denser at its silhouette, lit by your lamp, and the far walls fade to CLEAR (not into the dark).
                float n = VN(i.positionWS * 0.025);
                float3 c = lerp(float3(0.44, 0.32, 0.22), float3(0.66, 0.52, 0.38), n);
                float3 nn = normalize(i.normalWS);
                if (dot(nn, _WorldSpaceCameraPos - i.positionWS) < 0.0) nn = -nn;                                            // the face toward you
                float fr = 1.0 - abs(dot(nn, normalize(_WorldSpaceCameraPos - i.positionWS)));
                float3 L = _HullP.xyz - i.positionWS;
                float dh = max(1e-3, length(L));
                float lit = 0.75 + _Lamp * 0.6 * (0.4 + 0.6 * max(0.0, dot(nn, L / dh))) / (1.0 + dh * dh / (_Fall * _Fall));   // your lamp
                float fade = exp(-length(_WorldSpaceCameraPos - i.positionWS) / _Fog);                                         // far walls fade to clear
                return half4(c * lit, _Op * (0.35 + 0.65 * fr) * fade * w * ends);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
