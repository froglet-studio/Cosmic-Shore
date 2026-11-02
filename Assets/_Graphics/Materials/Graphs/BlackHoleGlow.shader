// BlackHoleGlow.shader — the CHEAP mobile look of every black hole, white hole and wave (Docs/BLACK_HOLE.md §5.4; Yash,
// 2026-10-29: "make sure we are not warping space ... mobile friendly ... a subtle clean refreshing user experience").
// The Meerkat water tube's recipe (MeerkatWaterTube.shader, Docs/Shaders/WATERY_GLASS.md) applied to the holes: ONE
// forward pass, one small mesh draw per hole / wave / ripple, no screen copy, no grab pass, no depth texture, no
// textures at all — the hardware depth test (ZTest LEqual) hides what is behind a prism. Nothing behind it is bent:
// it draws light (and a little shade) ON TOP of the scene, it never moves the scene.
//
// The mesh (BlackHoleGlow.Mesh) is a flat annulus, positionOS = (cos θ, sin θ, t): t = 0 the inner edge, 1 the outer.
// The vertex stage faces it to the camera around _Centre and sizes it to _Radii (world units), so a hole costs a small
// disc on screen and a ring costs only its band. Per-pixel: the distance from the centre and ~15-25 ALU, half precision
// for colour. Premultiplied blending (One, OneMinusSrcAlpha): rgb ADDS light, alpha SHADES what is behind — so one
// material draws a black core, a bright core and a light ring alike.
//
// _Mode: 0 black hole (dark core + thin photon ring + faint halo), 1 white hole (bright soft core), 2 light ring (a
// white hole's shell, the Stoat's charge ripple), 3 gravitational ripple (the studio's ringdown, as faint rings).
Shader "CosmicShore/BlackHoleGlow"
{
    Properties
    {
        _Centre ("Centre (world, set per draw)", Vector) = (0, 0, 0, 0)
        _Radii ("Inner, outer radius (u, set per draw)", Vector) = (0, 1, 0, 0)
        _Shape ("Shape (per mode, set per draw)", Vector) = (1, 0.06, 0.55, 0.08)
        _Wave ("Ripple: age, speed, period, ringdown", Vector) = (0, 120, 0.35, 1)
        _Axis ("Ripple: meeting axis (xyz), has axis (w)", Vector) = (0, 0, 0, 0)
        _Colour ("Colour (set per draw)", Color) = (1, 0.93, 0.8, 1)
        _Fade ("Fade (set per draw)", Float) = 1
        _Mode ("Mode (set per draw)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+5" "IgnoreProjector" = "True" }

        Pass
        {
            Name "BlackHoleGlow"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BlackHoleGlowVert
            #pragma fragment BlackHoleGlowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Centre, _Radii, _Shape, _Wave, _Axis;
                half4 _Colour;
                float _Fade, _Mode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // cos θ, sin θ, t (0 inner edge, 1 outer edge)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 offsetWS : TEXCOORD0;    // from the centre, world units (its length is r)
                half fade : TEXCOORD1;          // the per-draw fade × the near-camera fade
            };

            float dist_share(float dist, float outer) { return dist / max(outer, 1e-3); }

            Varyings BlackHoleGlowVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                // face the camera: the view matrix's rows are the camera's right and up in world space
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float radius = lerp(_Radii.x, _Radii.y, input.positionOS.z);
                float3 offset = (right * input.positionOS.x + up * input.positionOS.y) * radius;
                o.positionCS = TransformWorldToHClip(_Centre.xyz + offset);
                o.offsetWS = offset;
                // near fade: a core (modes 0, 1) the camera flies into fades before it fills the view; a ring (2, 3) stays
                // while the camera is inside it (a thin band round the view) and fades only right at its centre
                float near = dist_share(length(_WorldSpaceCameraPos - _Centre.xyz), _Radii.y);
                o.fade = (half)(_Fade * (_Mode < 1.5 ? smoothstep(0.6, 1.2, near) : smoothstep(0.05, 0.25, near)));
                return o;
            }

            half4 BlackHoleGlowFrag(Varyings i) : SV_Target
            {
                float r = length(i.offsetWS);
                half3 light = 0;
                half shade = 0;
                if (_Mode < 0.5)
                {
                    // black hole: x shadow radius (u), y ring width (share), z ring glow, w halo
                    float q = r / max(_Shape.x, 1e-3);
                    shade = (half)(1.0 - smoothstep(0.9, 1.0, q));
                    float p = (q - 1.04) / max(_Shape.y, 1e-3);
                    half ring = (half)(exp(-p * p) * _Shape.z + _Shape.w * exp(-3.0 * max(q - 1.0, 0.0)));
                    light = _Colour.rgb * ring * (1.0h - shade);
                }
                else if (_Mode < 1.5)
                {
                    // white hole: x core radius (u), z brightness (clamped to the display's white), w body opacity
                    float q = r / max(_Shape.x, 1e-3);
                    half core = (half)exp(-2.5 * q * q);
                    light = _Colour.rgb * saturate(core * (half)_Shape.z);
                    shade = core * (half)_Shape.w;
                }
                else if (_Mode < 2.5)
                {
                    // light ring: x front radius (u), y half width (u), z brightness, w shade
                    float p = (r - _Shape.x) / max(_Shape.y, 1e-3);
                    half prof = (half)exp(-p * p);
                    light = _Colour.rgb * prof * (half)_Shape.z;
                    shade = prof * (half)_Shape.w;
                }
                else
                {
                    // gravitational ripple (BlackHoleWaves.Strain): the ringdown behind a front moving out at speed,
                    // 1/r beyond the flat core, sin² of the angle to the line the holes met along; crests glow faintly,
                    // troughs shade faintly — the wiggle, drawn, without moving a single pixel of the scene.
                    float u = _Wave.x - r / max(_Wave.y, 1.0);
                    float tau = max(_Wave.w, 0.01);
                    if (u > 0.0 && u < 8.0 * tau)
                    {
                        float ca = _Axis.w > 0.5 ? dot(i.offsetWS, _Axis.xyz) / max(r, 1e-3) : 0.0;
                        float s = exp(-u / tau) * sin(6.2831853 * u / max(_Wave.z, 0.02)) * min(1.0, 60.0 / max(r, 1e-3)) * (1.0 - ca * ca);
                        light = _Colour.rgb * (half)(max(s, 0.0) * _Shape.z);
                        shade = (half)(max(-s, 0.0) * _Shape.w);
                    }
                }
                // the mesh's outer edge (the last fifth of its band): everything eases to exactly 0 there, so no mode ever shows the disc's rim
                half edge = (half)(1.0 - smoothstep(0.8, 1.0, (r - _Radii.x) / max(_Radii.y - _Radii.x, 1e-3)));
                return half4(light, saturate(shade)) * (i.fade * edge);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
