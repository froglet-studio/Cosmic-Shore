// SilencePerimeter.shader — the silenced pilot's 3D perimeter (Docs/ElementalAbilitySystem/SILENCE.md §5): a
// dense band of faceted shards on the screen's edge, where a vignette would sit, half buried beyond it, each
// facet showing the frame behind it bent and dragged toward a pull that wanders around the screen (reality
// trying to pull you somewhere you don't want to be). Ported line for line from the Silence Vignette Lab's
// FS_WARP and FS_SHARD (Docs/Studios/SilenceVignetteLab.html, between its <warp-shader> and <shard-shader>
// markers); the shards' layout, geometry and the pull point are SilencePerimeter.cs, the twin the lab's parity
// gate measures. Tools/Shaders/verify_silence_perimeter.py compiles both passes.
//
// HOW IT DRAWS. SilencePerimeterPass.cs, after URP has drawn the TRANSPARENTS, first lays the 2D danger edge
// into the frame (pass 2: SilenceVignetteMask worked out per pixel, so it is sharp at any thickness and
// follows the seconds of silence continuously), then copies the camera colour
// (_SilenceSceneColor), draws pass 0 (the warp: the frame near the edges swirls, ripples and is dragged toward
// the pull) as one full-screen triangle from that copy, then pass 1: EVERY shard in one mesh, built far to near
// on the CPU each frame, each shard's centre on screen, random and opacity riding in TEXCOORD1. The shards are
// drawn over everything (ZTest Always): they belong to the screen, not the world. Only the local silenced
// pilot's camera ever enqueues the pass.
Shader "CosmicShore/SilencePerimeter"
{
    Properties
    {
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
        float4 _SPLook4;   // x the warp's pull (x the silence), y the shards' pull (x the silence), z streak, w tip fade
        float4 _SPPull;    // xy the pull point (screen uv)
        float4 _SPDanger;  // the palette's danger signal colour

        float3 SampleScene(float2 uv)
        {
            return SAMPLE_TEXTURE2D_LOD(_SilenceSceneColor, sampler_LinearClamp, uv, 0).rgb;
        }

        float Hash1(float n) { return frac(sin(n) * 43758.5453); }

        // Set once per camera, for pass 2 (VesselSilenceConfigSO.VignetteShapeAt(depth)):
        float4 _SPEdgeShape;   // x style (0 soft, 1 faceted), y facets, z inner, w feather
        float4 _SPEdgeShape2;  // x roundness, y fill, z rim strength, w rim width
        float4 _SPEdgeColor;   // rgb the edge's colour, a its opacity (weight x strength x breathing + the hit flare)

        // SilenceVignetteMask.Distance and .Alpha, line for line (that C# is held to the lab by
        // Tools/Build/silence_vignette_parity).
        float SilenceRepeat(float t, float length) { return t - floor(t / length) * length; }

        float SilenceSmoothStep(float a, float b, float x)
        {
            if (b <= a) return x >= a ? 1.0 : 0.0;
            float t = saturate((x - a) / (b - a));
            return t * t * (3.0 - 2.0 * t);
        }

        float SilenceMaskDistance(float u, float v, float aspect)
        {
            float x = (u - 0.5) * 2.0 * lerp(1.0, max(0.01, aspect), saturate(_SPEdgeShape2.x));
            float y = (v - 0.5) * 2.0;
            float r = sqrt(x * x + y * y);
            int facets = (int)round(_SPEdgeShape.y);
            if (round(_SPEdgeShape.x) != 1.0 || facets < 3) return r;
            float seg = 2.0 * PI / facets;
            float start = PI * 0.5 - seg * 0.5;
            float a = SilenceRepeat(atan2(y, x) - start, seg) - seg * 0.5;
            return r * cos(a);
        }

        float SilenceMaskAlpha(float u, float v, float aspect)
        {
            float inner = _SPEdgeShape.z, feather = _SPEdgeShape.w;
            float rimStrength = _SPEdgeShape2.z, rimWidth = _SPEdgeShape2.w;
            float d = SilenceMaskDistance(u, v, aspect);
            float fill = SilenceSmoothStep(inner, inner + max(0.0, feather), d) * saturate(_SPEdgeShape2.y);
            float rim = 0.0;
            if (rimStrength > 0.0 && rimWidth > 0.0)
                rim = saturate(1.0 - abs(d - inner) / rimWidth) * saturate(rimStrength);
            return saturate(fill + rim);
        }
        ENDHLSL

        // 0: the warp — the whole frame from the copy, swirled, rippled and pulled near the edges.
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
                float uAspect = _ScreenParams.x / _ScreenParams.y;
                float uTime = _SPLook3.y, uWarp = _SPLook3.z, uShock = _SPLook3.w, uPull = _SPLook4.x;
                float2 uPullPoint = _SPPull.xy;
                // <warp-shader> the lab's FS_WARP
                float2 q = (vUv - 0.5) * 2.0;
                float edge = smoothstep(0.45, 1.25, length(q));
                float2 p = float2(q.x * uAspect, q.y);
                float r = length(p);
                float ang = uWarp * edge * (2.0 + 1.5 * sin(r * 9.0 - uTime * 2.3));
                float s = sin(ang), c = cos(ang);
                p = float2(c * p.x - s * p.y, s * p.x + c * p.y);
                p *= 1.0 + uShock * 0.06 * sin(r * 14.0 - uTime * 20.0) * edge;
                float2 uv = float2(p.x / uAspect, p.y) * 0.5 + 0.5;
                // The pull: the frame near the edges is dragged toward the pull point in slow waves; the middle stays still.
                float2 toP = (uPullPoint - vUv) * float2(uAspect, 1.0);
                float dP = length(toP) + 1e-4;
                float wave = 0.5 + 0.5 * sin(dP * 14.0 - uTime * 3.0);
                uv -= (toP / dP) * uPull * edge * (0.35 + 0.65 * wave) / float2(uAspect, 1.0);
                return half4(SampleScene(uv), 1.0);
            }
            ENDHLSL
        }

        // 1: every shard — lit, faceted, its facets showing the copy bent and pulled; premultiplied alpha.
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
                float4 bary : TEXCOORD0;    // xyz SilencePerimeter.Geometry's barycentrics, w the point along the shard (-0.5..0.5, + inward)
                float4 shard : TEXCOORD1;   // xy the shard's centre on screen (uv), z its random, w its opacity
            };

            struct ShardVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalVS : TEXCOORD0;
                float3 viewVS : TEXCOORD1;
                float4 bary : TEXCOORD2;
                float4 shard : TEXCOORD3;
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
                output.shard = input.shard;
                return output;
            }

            half4 ShardFrag(ShardVaryings input) : SV_Target
            {
                float uRefract = _SPLook.x, uTwist = _SPLook.y, uAberr = _SPLook.z, uTint = _SPLook.w;
                float uRim = _SPLook2.x, uEdge = _SPLook2.y, uFill = _SPLook2.z, uInvert = _SPLook2.w;
                float uGlitch = _SPLook3.x, uTime = _SPLook3.y;
                float uPull = _SPLook4.y, uStreak = _SPLook4.z, uTipFade = _SPLook4.w;
                float2 uPullPoint = _SPPull.xy;
                float2 uCenter = input.shard.xy;
                float uSeed = input.shard.z, uAlpha = input.shard.w;
                float vAlong = input.bary.w;
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
                float2 toP = (uPullPoint - uv) * float2(aspect, 1.0);
                float2 pullDir = toP / (length(toP) + 1e-4) / float2(aspect, 1.0);
                suv -= pullDir * uPull * 0.6;
                suv.x += (Hash1(floor(uv.y * 180.0) + floor(uTime * 24.0)) - 0.5) * uGlitch;
                float2 ab = n.xy * uAberr;
                // The picture is smeared toward the pull: light streams into the maw.
                float3 col = float3(0.0, 0.0, 0.0);
                for (int k = 0; k < 6; k++)
                {
                    float2 tuv = suv - pullDir * uStreak * k;
                    col += float3(SampleScene(tuv + ab).r, SampleScene(tuv).g, SampleScene(tuv - ab).b);
                }
                col /= 6.0;
                col = lerp(col, 1.0 - col, uInvert);
                float shade = max(0.0, dot(n, normalize(float3(0.35, 0.6, 0.7))));
                float lit = 0.8 + 0.3 * shade;
                float fres = pow(1.0 - abs(dot(n, v)), 3.0);
                float e = min(min(input.bary.x, input.bary.y), input.bary.z);
                float edgeLine = 1.0 - smoothstep(0.0, max(fwidth(e) * 1.5, 1e-4), e);
                col = lerp(col * lit, uDanger * dot(col, float3(0.3, 0.59, 0.11)) * 2.0, uTint)
                    + uDanger * (uFill * (0.35 + 0.65 * shade) + fres * uRim + edgeLine * uEdge);
                // Facets differ in how solid they are, so overlapping shards dissolve into each other ...
                // ... and each fades toward its inward tip, so no shard ends in a point: it dissolves into the distortion.
                float facetAlpha = uAlpha * (0.6 + 0.4 * Hash1(dot(n, float3(12.9898, 78.233, 37.719)) * 9.0))
                                 * (1.0 - uTipFade * smoothstep(-0.1, 0.5, vAlong));
                return half4(col * facetAlpha, facetAlpha);
            }
            ENDHLSL
        }

        // 2: the 2D danger edge, laid into the frame BEFORE the copy, so the pull drags it and the facets refract it.
        Pass
        {
            Name "SilenceEdge"
            Tags { "LightMode" = "SilenceEdge" }

            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EdgeVert
            #pragma fragment EdgeFrag

            struct EdgeAttributes { uint vertexID : SV_VertexID; };
            struct EdgeVaryings { float4 positionCS : SV_POSITION; };

            EdgeVaryings EdgeVert(EdgeAttributes input)
            {
                EdgeVaryings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            half4 EdgeFrag(EdgeVaryings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect = _ScreenParams.x / _ScreenParams.y;
                return half4(_SPEdgeColor.rgb, SilenceMaskAlpha(uv.x, uv.y, aspect) * saturate(_SPEdgeColor.a));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
