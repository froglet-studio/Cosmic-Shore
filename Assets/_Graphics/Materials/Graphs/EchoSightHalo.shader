// EchoSightHalo.shader — the halo half of the Dolphin's Charge-5 "Pilot Echo".
//
// PURPOSE. Brightening a highlighted pilot's own hull is not enough to find them: the Echo Sight
// lights up the surrounding PRISMS at the same time, so in a dense arena (Rampage's cactus forest
// was the case that proved it) a brighter ship sits inside a brighter forest and reads as more of
// the same. And a hull tint says nothing at all about a pilot who is BEHIND mass. This draws a
// domain-coloured glow that is legible in all three situations the ability has to serve — target in
// the open, target surrounded by mass, target fully occluded by mass.
//
// WHY IT IS A HAND-WRITTEN .shader AND NOT A SHADER GRAPH. The whole effect rests on two render
// states — ZTest Always and additive blending — and Shader Graph cannot express "ignore the depth
// buffer" on a URP Unlit target. It is also 40 lines; synthesising graph JSON for it would be more
// fragile than the thing it replaced, not less.
//
// THE THREE PROPERTIES OF THE RENDER STATE, all load-bearing:
//   ZTest Always  — the halo is drawn whether or not mass stands in front of it, which is the only
//                   way "behind prisms" can read at all.
//   Blend One One — additive. It can only ever ADD light, so it cannot darken the ship it marks and
//                   it never needs a correct sort order against the transparent queue.
//   ZWrite Off    — it writes no depth, so it can never occlude the world it is drawn over.
//
// THE SHAPE. A camera-facing disc built in VIEW space from the object's origin, so the quad needs no
// billboarding on the CPU and no per-frame transform write: the mesh is a unit quad, the transform
// sits at the vessel's centre, and the vertex shader spreads the corners across the view plane. Two
// terms compose:
//   - a soft radial GLOW that falls off to the disc edge, which is what makes the target findable in
//     empty space and legible through mass;
//   - a hard RING at the hull's own silhouette radius, which is what keeps it readable when the
//     target is surrounded by lit prisms — a ring is a shape nothing in the arena has, whereas a
//     glow can be mistaken for one more bright prism.
//
// COLOUR is passed in per-instance and is the pilot's own SATURATED domain colour, so the halo says
// who as well as where. Never a fixed highlight colour: two rivals in one cone must be tellable
// apart (Docs/PALETTE.md — domain identity is the palette's job and this must not borrow its space
// for something else).
//
// COST. One additive quad per highlighted vessel, only while the trigger is held, no depth read, no
// texture fetch, ~15 ALU in the fragment. Nothing here touches the prism render path.

Shader "CosmicShore/EchoSightHalo"
{
    Properties
    {
        [HDR] _HaloColor ("Halo Colour", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _Radius ("Halo World Radius", Float) = 10
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
            Name "EchoSightHalo"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 disc       : TEXCOORD0;   // -1..1 across the quad
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _HaloColor;
                float  _Intensity;
                float  _Radius;
                float  _RingPos;
                float  _RingWidth;
                float  _RingGain;
                float  _GlowFalloff;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;

                // The quad is authored in [-0.5, 0.5]; -1..1 is the convenient disc parameter.
                float2 corner = input.positionOS.xy * 2.0;

                // Billboard in VIEW space about the object's ORIGIN. Because the offset is applied
                // after the view transform, the vessel's own rotation cannot tilt or foreshorten the
                // disc, and the halo needs no CPU work per frame beyond riding its parent.
                float3 centreWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 centreVS = TransformWorldToView(centreWS);
                float3 posVS    = centreVS + float3(corner * _Radius, 0.0);

                output.positionCS = TransformWViewToHClip(posVS);
                output.disc = corner;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float d = saturate(length(input.disc));          // 0 centre .. 1 edge

                // Soft body: findable in empty space, and the part that survives being drawn over a
                // busy background.
                float glow = pow(saturate(1.0 - d), max(_GlowFalloff, 0.01));

                // Hard ring at the hull's silhouette. A Gaussian rather than a smoothstep band so
                // both of its edges are soft while its centre stays bright — a band with hard edges
                // reads as a solid coin at small screen sizes.
                float t    = (d - _RingPos) / max(_RingWidth, 0.001);
                float ring = exp(-t * t);

                float amount = (glow + ring * _RingGain) * max(_Intensity, 0.0);

                // Additive: alpha is unused by Blend One One, so the colour carries everything.
                return half4(_HaloColor.rgb * amount, 0.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
