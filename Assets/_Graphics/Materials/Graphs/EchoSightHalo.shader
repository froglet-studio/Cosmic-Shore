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
// THE SHAPE. A camera-facing disc built from the object's origin in CLIP space, so the quad needs no
// billboarding on the CPU and no per-frame transform write: the mesh is a unit quad, the transform
// sits at the vessel's centre, and the vertex shader spreads the corners across the screen. Two
// terms compose:
//   - a soft radial GLOW that falls off to the disc edge, which is what makes the target findable in
//     empty space and legible through mass;
//   - a hard RING at the hull's own silhouette radius, which is what keeps it readable when the
//     target is surrounded by lit prisms — a ring is a shape nothing in the arena has, whereas a
//     glow can be mistaken for one more bright prism.
//
// IT DOES NOT SHRINK WITH DISTANCE, which is the whole point of a locator. A world-sized disc obeys
// perspective and so vanishes exactly when it is most needed — a rival across the arena is the case
// the pilot cannot solve by looking harder. The radius is therefore
// `max(what _Radius subtends at this depth, _MinScreenRadius)`: up close the disc is hull-sized and
// its ring traces the silhouette, and past the crossover depth it holds a CONSTANT ANGULAR SIZE, so
// a distant pilot is exactly as findable as a near one. Raise _MinScreenRadius past every practical
// hull size to make it constant at all distances.
//
// One consequence to accept rather than fix: once the floor engages, the ring no longer lands on the
// hull's silhouette — it becomes a reticle AROUND the ship. That is the correct trade. The silhouette
// trace exists to separate a marked ship from mass it is tangled up in, which is a close-range
// problem; at range the job is "there is a pilot over there", and a constant glyph does that better
// than an accurate one too small to see.
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
        _MinScreenRadius ("Min Screen Radius (NDC half-height)", Range(0, 0.5)) = 0.055
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
                float  _MinScreenRadius;
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

                // Billboard about the object's ORIGIN, offsetting in CLIP space. Because the offset
                // is applied after the projection, the vessel's own rotation cannot tilt or
                // foreshorten the disc, its SCALE cannot squash it, and the halo needs no CPU work
                // per frame beyond riding its parent.
                float3 centreWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float4 centreCS = TransformWorldToHClip(centreWS);

                // The NDC-y half-extent that _Radius world units subtends at this depth.
                // UNITY_MATRIX_P._m11 is cot(fovY/2), so radius * m11 / w is exact rather than an
                // approximation — and it tracks the speed tunnel's live FOV for free, because the
                // projection matrix is where that effect lands.
                float w = max(abs(centreCS.w), 1e-4);
                float worldNdc = _Radius * UNITY_MATRIX_P._m11 / w;

                // The floor is what makes the halo distance-independent. See the header note.
                float r = max(worldNdc, _MinScreenRadius);

                // NDC x and y both span -1..1 while x covers a wider field, so the x offset carries
                // the inverse aspect or the disc renders as an ellipse.
                float2 offset = float2(r * (_ScreenParams.y / _ScreenParams.x), r);

                // Pre-multiplied by w so the offset SURVIVES the perspective divide — that is what
                // turns it into a screen-space size instead of a world-space one.
                centreCS.xy += corner * offset * centreCS.w;

                output.positionCS = centreCS;
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
                return half4(_HaloColor.rgb * amount, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
