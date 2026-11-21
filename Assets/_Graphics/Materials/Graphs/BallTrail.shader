// BallTrail.shader — the comet wake an Astro League / Scarab ball leaves behind it.
//
// WHAT IT REPLACED. A flat URP/Unlit ribbon 0.6..5 units wide behind a 14-unit ball, painted the
// ball's SPAWN colour once and never again — so a ball a Ruby striker had claimed still trailed its
// neutral colour, and at any speed the wake read as a thin line drawn behind a planet. This is the
// same TrailRenderer (no new mesh, no new renderer, no per-frame CPU beyond one property block),
// drawn as a plasma comet:
//
//   HALO      a soft domain-coloured body the full ribbon width, brightest at the head so the wake
//             grows OUT of the ball rather than starting behind it;
//   CORE      a white-hot filament down the centreline that thins toward the tail;
//   BRAID     three filaments, 120° apart in phase, that start converged at the ball and twist
//             apart down the wake — a helix seen side-on — so the trail reads as the ball's own
//             spin being shed. Their spread is driven by speed (_Speed01): a lazy roll is a tight
//             rope, a screamer unravels;
//   PACKETS   travelling brightness along the filaments, scrolling from head to tail, so energy
//             visibly pours OUT of the ball instead of the ribbon sitting inert.
//
// WHY IT KEYS ON VERTEX ALPHA, NOT UV.X. The ball writes a white colour gradient whose alpha runs
// 1 at the head to 0 at the tail, so `1 - color.a` is the normalised position along the trail —
// independent of the TrailRenderer's texture mode and of which end Unity calls u = 0. uv.y is the
// position ACROSS the ribbon (0..1), which is unambiguous.
//
// THE PALETTE RULES IT RESPECTS (Docs/PALETTE.md). Gameplay bloom is CLAMPED at 0.5, so brightness
// above it is a dead dial — this buys its glow with bright AREA (the halo spans the full ribbon)
// rather than with intensity. ACES desaturates anything bright, so the part that should read as
// colour (halo, braid) stays well under 1 while only the core is allowed to go white.
//
// RENDER STATE. Blend One One (additive: it can only add light, needs no sort order against the
// transparent queue, and can never darken the ball or the arena it crosses), ZWrite Off, Cull Off
// (a view-aligned ribbon still flips winding on a hard bounce), ZTest LEqual (a goal frame or a
// forest in front of the wake still occludes it).
//
// COST. One additive ribbon per live ball, ~40 ALU in the fragment, no texture fetch.

Shader "CosmicShore/BallTrail"
{
    Properties
    {
        [HDR] _Color ("Domain Colour", Color) = (0.3, 0.8, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _Speed01 ("Speed 0..1", Range(0, 1)) = 0.5
        _HaloGain ("Halo Gain", Float) = 0.5
        _CoreGain ("Core Gain", Float) = 1.25
        _CoreWidth ("Core Width (head)", Range(0.02, 0.6)) = 0.16
        _BraidGain ("Braid Gain", Float) = 1.0
        _BraidWidth ("Braid Filament Width", Range(0.02, 0.4)) = 0.085
        _BraidTwists ("Braid Twists Along Trail", Float) = 2.6
        _BraidSpin ("Braid Spin Speed", Float) = 7
        _PacketDensity ("Packet Density", Float) = 9
        _PacketSpeed ("Packet Flow Speed", Float) = 3.2
        _FadePower ("Tail Fade Power", Float) = 1.6
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "BallTrail"
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
                float2 trail      : TEXCOORD0;   // x = age 0 head .. 1 tail, y = -1..1 across
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Intensity;
                float  _Speed01;
                float  _HaloGain;
                float  _CoreGain;
                float  _CoreWidth;
                float  _BraidGain;
                float  _BraidWidth;
                float  _BraidTwists;
                float  _BraidSpin;
                float  _PacketDensity;
                float  _PacketSpeed;
                float  _FadePower;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.trail = float2(1.0 - input.color.a, input.uv.y * 2.0 - 1.0);
                return output;
            }

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // Smooth 1D value noise — continuous, so a packet slides rather than blinking.
            float ValueNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), u);
            }

            float Gauss(float x, float width)
            {
                float t = x / max(width, 1e-3);
                return exp(-t * t);
            }

            half4 frag (Varyings input) : SV_Target
            {
                float age = saturate(input.trail.x);
                float s   = clamp(input.trail.y, -1.0, 1.0);
                float t   = _Time.y;

                // Along-length envelope: full at the head, easing to nothing at the tail, plus a
                // flare right at the head so the wake is continuous with the ball's own glow.
                float fade = pow(saturate(1.0 - age), max(_FadePower, 0.01));
                float head = exp(-age * age * 60.0);

                // HALO — soft body across the full ribbon width, zero at both edges (no seam).
                float halo = pow(saturate(1.0 - s * s), 2.0) * (0.55 + 0.45 * head);

                // CORE — thins toward the tail.
                float core = Gauss(s, lerp(_CoreWidth, _CoreWidth * 0.3, age));

                // BRAID — three filaments converged at the ball, unravelling down the wake.
                float spread = lerp(0.18, 0.62, saturate(_Speed01)) * smoothstep(0.0, 0.45, age);
                float phase  = age * _BraidTwists * 6.2831853 - t * _BraidSpin;
                float fw     = lerp(_BraidWidth, _BraidWidth * 1.8, age);
                float braid  = 0.0;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    float c = spread * sin(phase + i * 2.0943951);
                    // Depth cue: the strand swinging toward the viewer (cos > 0) reads brighter,
                    // which is what sells a helix instead of three sine waves on a plane.
                    float front = 0.55 + 0.45 * cos(phase + i * 2.0943951);
                    braid += Gauss(s - c, fw) * front;
                }

                // PACKETS — energy pouring out of the ball: noise scrolled toward the tail.
                float n = ValueNoise(age * _PacketDensity - t * _PacketSpeed);
                float packets = 0.6 + 1.1 * n * n * n;

                float3 hue = _Color.rgb;
                float3 hot = lerp(hue, float3(1.0, 1.0, 1.0), 0.8);

                float3 rgb = hue * (halo * _HaloGain)
                           + lerp(hue, hot, 0.35) * (braid * _BraidGain * packets)
                           + hot * (core * _CoreGain * (0.75 + 0.25 * packets));

                rgb *= fade * max(_Intensity, 0.0);

                // Additive: alpha is unused by Blend One One, so the colour carries everything.
                return half4(rgb, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
