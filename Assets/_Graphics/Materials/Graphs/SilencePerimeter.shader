// SilencePerimeter.shader — the silenced pilot's 3D perimeter (Docs/ElementalAbilitySystem/SILENCE.md §5):
// faceted shards on the screen's edge, where a vignette would sit, each facet showing the frame behind it bent
// (reality breaking down). Ported line for line from the Silence Vignette Lab's FS_WARP and FS_SHARD
// (Docs/Studios/SilenceVignetteLab.html, between its <warp-shader> and <shard-shader> markers); the shards'
// layout and geometry are SilencePerimeter.cs, the twin the lab's parity gate measures.
//
// HOW IT DRAWS. SilencePerimeterPass.cs, after URP has drawn the TRANSPARENTS, copies the camera colour
// (_SilenceSceneColor), draws pass 0 (the warp: the frame swirls and ripples near the edges) as one
// full-screen triangle from that copy, then draws every shard with pass 1, back to front, each sampling the
// copy bent by its facet's normal, turned by its own twist and torn by hits. The shards are drawn over
// everything (ZTest Always): they belong to the screen, not the world. Only the local silenced pilot's
// machine ever enqueues the pass.
Shader "CosmicShore/SilencePerimeter"
{
    Properties
    {
        [HideInInspector] _SPAlpha ("Shard opacity (per draw, from SilencePerimeterPass)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+60"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // The scene as the camera drew it, copied by SilencePerimeterPass.cs (sampler_LinearClamp comes with Core.hlsl).
        TEXTURE2D(_SilenceSceneColor);

        // Set once per camera by SilencePerimeterPass (VesselSilenceConfigSO):
        float4 _SPLook;    // x refraction, y twist, z aberration, w danger tint
        float4 _SPLook2;   // x rim glow, y edge lines, z danger fill, w invert
        float4 _SPLook3;   // x glitch (x the hit flare), y time, z warp (x the silence), w shock (the hit flare)
        float4 _SPDanger;  // the palette's danger signal colour
        // Per shard:
        float4 _SPShard;   // xy the shard's centre on screen (uv), z its random, w unused
        float _SPAlpha;    // opacity x presence x amount

        float3 SampleScene(float2 uv)
        {
            return SAMPLE_TEXTURE2D_LOD(_SilenceSceneColor, sampler_LinearClamp, uv, 0).rgb;
        }

        float Hash1(float n) { return frac(sin(n) * 43758.5453); }
        ENDHLSL

        // 0: the warp — the whole frame from the copy, swirled and rippled near the edges.
        Pass
        {
            Name "SilencePerimeterWarp"
            Tags { "LightMode" = "SilencePerimeterWarp" }

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WarpVert
            #pragma fragment WarpFrag

            struct WarpAttributes { uint vertexID : SV_VertexID; };
            struct WarpVaryings { float4 positionCS : SV_POSITION; };

            WarpVaryings WarpVert(WarpAttributes input)
            {
                WarpVaryings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            half4 WarpFrag(WarpVaryings input) : SV_Target
            {
                float2 vUv = GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float uTime = _SPLook3.y, uWarp = _SPLook3.z, uShock = _SPLook3.w;
                // <warp-shader> the lab's FS_WARP
                float2 q = (vUv - 0.5) * 2.0;
                float edge = smoothstep(0.6, 1.3, length(q));
                float2 p = float2(q.x * aspect, q.y);
                float r = length(p);
                float ang = uWarp * edge * (2.0 + 1.5 * sin(r * 9.0 - uTime * 2.3));
                float s = sin(ang), c = cos(ang);
                p = float2(c * p.x - s * p.y, s * p.x + c * p.y);
                p *= 1.0 + uShock * 0.06 * sin(r * 14.0 - uTime * 20.0) * edge;
                return half4(SampleScene(float2(p.x / aspect, p.y) * 0.5 + 0.5), 1.0);
            }
            ENDHLSL
        }

        // 1: one shard — lit, faceted, its facets showing the copy bent; premultiplied alpha.
        Pass
        {
            Name "SilencePerimeter"
            Tags { "LightMode" = "SilencePerimeter" }

            ZWrite Off
            ZTest Always
            Cull Back
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShardVert
            #pragma fragment ShardFrag

            struct ShardAttributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 bary : TEXCOORD0;   // SilencePerimeter.Geometry's barycentrics
            };

            struct ShardVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalVS : TEXCOORD0;
                float3 viewVS : TEXCOORD1;
                float3 bary : TEXCOORD2;
            };

            ShardVaryings ShardVert(ShardAttributes input)
            {
                ShardVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 positionVS = TransformWorldToView(positionWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS), false);
                output.viewVS = -positionVS;
                output.bary = input.bary;
                return output;
            }

            half4 ShardFrag(ShardVaryings input) : SV_Target
            {
                float uRefract = _SPLook.x, uTwist = _SPLook.y, uAberr = _SPLook.z, uTint = _SPLook.w;
                float uRim = _SPLook2.x, uEdge = _SPLook2.y, uFill = _SPLook2.z, uInvert = _SPLook2.w;
                float uGlitch = _SPLook3.x, uTime = _SPLook3.y;
                float2 uCenter = _SPShard.xy;
                float uSeed = _SPShard.z;
                float3 uDanger = _SPDanger.rgb;
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                // <shard-shader> the lab's FS_SHARD
                float3 n = normalize(input.normalVS);
                float3 v = normalize(input.viewVS);
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 d = uv - uCenter; d.x *= aspect;
                float a = uTwist * (uSeed * 2.0 - 1.0) * 1.2;
                float ca = cos(a), sa = sin(a);
                d = float2(ca * d.x - sa * d.y, sa * d.x + ca * d.y);
                d *= 1.0 + 0.35 * uTwist * (frac(uSeed * 7.31) - 0.5);
                d.x /= aspect;
                float2 suv = uCenter + d + n.xy * uRefract;
                suv.x += (Hash1(floor(uv.y * 180.0) + floor(uTime * 24.0)) - 0.5) * uGlitch;
                float2 ab = n.xy * uAberr;
                float3 col = float3(SampleScene(suv + ab).r, SampleScene(suv).g, SampleScene(suv - ab).b);
                col = lerp(col, 1.0 - col, uInvert);
                float shade = max(0.0, dot(n, normalize(float3(0.35, 0.6, 0.7))));
                float lit = 0.8 + 0.3 * shade;
                float fres = pow(1.0 - abs(dot(n, v)), 3.0);
                float e = min(min(input.bary.x, input.bary.y), input.bary.z);
                float edgeLine = 1.0 - smoothstep(0.0, max(fwidth(e) * 1.5, 1e-4), e);
                col = lerp(col * lit, uDanger * dot(col, float3(0.3, 0.59, 0.11)) * 2.0, uTint)
                    + uDanger * (uFill * (0.35 + 0.65 * shade) + fres * uRim + edgeLine * uEdge);
                return half4(col * _SPAlpha, _SPAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
