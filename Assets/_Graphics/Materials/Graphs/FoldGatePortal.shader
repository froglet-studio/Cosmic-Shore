// FoldGatePortal.shader — the WINDOW inside a Butterfly fold gate's ring.
//
// PURPOSE. A fold gate is a portal: thread one ring and you are at the other. For the transit to be
// seamless the pilot has to be able to see where they are going BEFORE they go — otherwise the ring
// shows the world behind it, the pilot flies through, and the whole picture changes around them in
// one frame. This surface shows the FAR side instead: FoldGatePortalView renders the world from the
// vantage the gameplay camera would have if the two mouths were one, clipped at the far mouth's own
// plane, into _FoldGatePortalTex; this disc samples that texture at its own SCREEN position. So each
// pixel of the window shows exactly what the camera would see through that pixel on the far side,
// and the ship, flying into the window, is already in the place it is about to be.
//
// SCREEN-SPACE, NOT UV-MAPPED. The texture is a render with the gameplay camera's own projection,
// CROPPED to the window's own rectangle of the screen (FoldGatePortalView renders only the
// window's footprint), so the right texel for a fragment is the one at that fragment's screen
// position, mapped into the rectangle by _FoldGatePortalUV (uv' = uv * xy + zw; identity when the
// target covers the whole screen) — a
// surface UV would paste a picture onto the disc, which is a painting of the far side rather than a
// window onto it and would swim as the camera moves. The screen position is the built-in
// ComputeScreenPos arithmetic written out here (it carries the render-target flip in
// _ProjectionParams.x, which is what lets a camera-rendered texture be sampled without a y flip on
// any API).
//
// HDR IN, NOT TONEMAPPED. The far-side camera renders with post-processing OFF into an HDR target,
// and this pass writes that linear colour straight into the gameplay camera's colour buffer — so
// the gameplay camera's own post stack (bloom, tonemapper, the speed tunnel's Panini) runs over the
// window exactly once, with the rest of the frame. A window tonemapped on its own would be tonemapped
// twice and read as a flat grey picture of the far side.
//
// RENDER STATE.
//   Queue Transparent + alpha blend — the window fades in (_PortalBlend) as a gate comes into range,
//                  because a picture may not pop into a ring any more than the ring may pop into the
//                  world. At blend 1 it is fully opaque and hides the near side outright.
//   ZWrite Off / ZTest LEqual — mass standing in front of the ring still covers the window; nothing
//                  behind it can.
//   Cull Off     — a gate can be threaded from either side, and the far-side render is clipped at the
//                  far plane on whichever side the camera stands, so both faces show the right thing.
//
// COST. One texture fetch per fragment on a single disc. The real cost is the second render of the
// world, which FoldGatePortalView pays only for the window's footprint, only while a threadable
// gate is on screen and in range.

Shader "CosmicShore/FoldGatePortal"
{
    Properties
    {
        _PortalBlend ("Portal Blend", Range(0, 1)) = 1
        _EdgeSoftness ("Edge Softness (fraction of radius)", Range(0, 0.5)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "FoldGatePortal"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // A GLOBAL, set by FoldGatePortalView after each far-side render. Global rather than a
            // per-material texture because there is exactly one far-side render at a time and it
            // belongs to whichever gate is being shown; the material stays shared.
            TEXTURE2D(_FoldGatePortalTex);
            SAMPLER(sampler_FoldGatePortalTex);
            // GLOBAL: screen UV -> the rectangle of the screen the far-side target covers.
            float4 _FoldGatePortalUV;

            CBUFFER_START(UnityPerMaterial)
                float _PortalBlend;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float2 disc       : TEXCOORD1;   // -1..1 across the disc
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // ComputeScreenPos, written out: xy in [0, w] with the render-target flip applied.
                float4 o = output.positionCS * 0.5;
                o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
                o.zw = output.positionCS.zw;
                output.screenPos = o;

                output.disc = input.uv * 2.0 - 1.0;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                uv = uv * _FoldGatePortalUV.xy + _FoldGatePortalUV.zw;
                half3 colour = SAMPLE_TEXTURE2D(_FoldGatePortalTex, sampler_FoldGatePortalTex, uv).rgb;

                // The rim tucks under the ring's tube; the soft edge only keeps a hard disc
                // silhouette from showing in the sliver between them at a grazing angle.
                float r = length(input.disc);
                float edge = 1.0 - smoothstep(1.0 - max(_EdgeSoftness, 1e-4), 1.0, r);

                return half4(colour, saturate(_PortalBlend) * edge);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
