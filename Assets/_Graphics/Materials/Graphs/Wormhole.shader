// Wormhole.shader — the SURFACE of a wormhole mouth (WormholeMouth.cs).
//
// PURPOSE. A wormhole is two spheres whose insides are one place: fly into one and you come out of
// the other (WormholeGeometry.cs states the model). For that to be seamless the sphere must show,
// from every side, what lies beyond the OTHER mouth — so the pilot flies into the place they see.
// This surface paints that picture from two sources, both rendered by cameras attached to the mouths:
//
//   EXACT VIEW (the player's camera, carried through the pair). WormholeView renders the world from
//   the gameplay camera's own pose displaced by the pair's translation, with that camera's own
//   projection, clipped at the far ball and cropped to this sphere's footprint on screen, into
//   _WormholeExactTex. This surface samples it at its own SCREEN position (the FoldGatePortal
//   arithmetic, written out), so each pixel is exactly what the camera would see through it —
//   near things included. Valid only while the camera it was rendered for is drawing, which is what
//   the global _WormholeMainView says (1 for the gameplay camera, 0 for every other one).
//
//   PANORAMA (all directions). The PARTNER mouth's camera captures its surroundings as six 90° faces
//   (_WormholePanorama, a 2D array, face table in WormholeGeometry.FaceOf/FaceUV — mirrored in
//   SamplePanorama below). The view ray is continued from the entry point as if it had come out of
//   the far mouth, assumed to end on a proxy sphere _ProxyRadius from the capture point, and the
//   panorama is sampled in that direction (WormholeGeometry.ParallaxDirection). Any camera can use
//   it, from any distance, at the cost of parallax for things nearer than the proxy.
//
//   The two are crossfaded by _WormholeExactBlend (WormholeView: 1 inside the mouth's exact range,
//   fading to 0 across the band past it). At blend 1 the panorama is never sampled.
//
// HDR IN, NOT TONEMAPPED. Both pictures are rendered with post-processing OFF into HDR targets and
// written straight into the gameplay camera's colour buffer, so its own post stack runs over the
// sphere exactly once, with the rest of the frame.
//
// THE RIM WEARS THE DOMAIN. The only part of the surface that is the mouth itself rather than the place
// beyond it is the fresnel rim, and it is painted in the owning domain's hue (_WormholeRimTint, per
// renderer, from the theme's domain colour; _RimColor is only the fallback for a mouth built with no
// tint). _DomainRimBoost lifts the theme's LDR colour so the rim blooms in that hue instead of reading
// as a pale wash.
//
// SEALED (_WormholeSealed = 1): a domain-locked mouth seen by a viewer who may not thread it — a Butterfly
// rival — shows NO view through it, because a view through is a promise you can go there. It draws only a
// fresnel shell in the domain colour and clips its interior (colour and depth alike), so a rival sees a
// domain-coloured bubble they will fly straight through. An unpaired mouth (one withering away) is sealed too.
//
// A CAMERA INSIDE A MOUTH DOES NOT DRAW IT. Within _WormholeClearance of the surface (a few near
// clips, WormholeGeometry.Clearance) the fragment is dropped, so a camera carried through sees the
// world beyond the far mouth directly instead of the inside of a sphere — and the carry hands over at
// that same distance, so the hand-over is a change of frame with nothing on screen to show it.
//
// IT NEVER HIDES THE PILOT'S SHIP. A fold lays its destination mouth AROUND the Butterfly, and while
// it blooms (or whenever a mouth ends up between the chase camera and the ship) its front face stands
// between the two. The mouth honours the camera->ship occlusion corridor (a PLATFORM LAW,
// PrismOcclusionCorridor.cs / Docs/PRISM_ANIMATION.md §4.7) through the same PrismOcclusionFade_float
// the prism graphs call: inside the corridor the surface dissolves through the same screen door prism
// mass does, in the depth pass as well as the colour pass, so the ship behind it is drawn as itself.
// The one mouth that must NOT open is the one the camera is being carried through: there the corridor
// target is the ship mapped back through the pair and the exact view is what shows it, so a hole would
// show the empty near-side interior instead. WormholeView clears _WormholeCorridor on that mouth.
//
// RENDER STATE. Opaque, ZWrite On, Cull Back: from outside the sphere covers its whole footprint;
// from inside, its back faces are culled (and the clearance drops the rest).
//
// COST. One screen-space fetch, or one array fetch, per fragment; a small rim term; the corridor's
// segment test (~10 ALU), whose dither kernel runs only on fragments inside it. The real cost is
// the renders that fill the two textures, which WormholeView budgets.

Shader "CosmicShore/Wormhole"
{
    Properties
    {
        [HDR] _RimColor ("Rim Colour (fallback when no domain tint is set)", Color) = (0.45, 0.75, 1.6, 1)
        _DomainRimBoost ("Domain Rim Boost", Range(0, 8)) = 2
        _RimPower ("Rim Power", Range(0.5, 16)) = 6
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 0.35
        _RimDarken ("Rim Darkening", Range(0, 1)) = 0.25
        _FlareIntensity ("Transit Flare", Range(0, 8)) = 2.5
        _ProxyRadius ("Panorama Proxy Distance", Float) = 600
        [HDR] _VoidColor ("Void Colour (before the first capture)", Color) = (0.01, 0.015, 0.04, 1)
        _SealedRimPower ("Sealed Rim Power", Range(0.5, 8)) = 2.5
        _SealedRimCutoff ("Sealed Rim Cutoff", Range(0, 1)) = 0.2
        _SealedIntensity ("Sealed Rim Intensity", Range(0, 8)) = 1.5
        [Header(Seamless mouth (a crystal wormhole, Docs CRYSTAL_WORMHOLE.md))]
        _SoftEdge ("Seamless edge: 0 = the fold's hard sphere and rim; above 0 the fraction of the radius the view dissolves over", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "PrismOcclusionCorridor.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _RimColor;
            float _RimPower;
            float _RimIntensity;
            float _RimDarken;
            float _FlareIntensity;
            float _ProxyRadius;
            float4 _VoidColor;
            float _DomainRimBoost;
            float _SealedRimPower;
            float _SealedRimCutoff;
            float _SealedIntensity;
            float _SoftEdge;
        CBUFFER_END

        // PER RENDERER (WormholeMouth.ApplySurface, through a MaterialPropertyBlock).
        float4 _WormholeSphere;          // xyz centre, w world radius
        float4 _WormholeExactUV;         // screen uv -> exact target uv: uv * xy + zw
        float _WormholeExactBlend;       // 0 panorama .. 1 exact
        float _WormholePanoramaReady;    // the partner's six faces have all been captured
        float _WormholeFlare;            // 1 on a transit, decaying
        float4 _WormholeRimTint;         // rgb the domain's hue (linear); a = 1 when set
        float _WormholeSealed;           // 1 = this viewer may not thread it: outline only
        float _WormholeCorridor;         // 1 = dissolve inside the camera->ship occlusion corridor

        // GLOBAL (WormholeView).
        float _WormholeMainView;         // 1 while the gameplay camera is drawing
        float _WormholeClearance;        // world units; a camera this close to the surface is IN it

        // GLOBAL (PrismOcclusionCorridor.cs; the prism graphs declare them as graph properties, a
        // hand-written shader declares them itself — SwarmMemberInstanced does the same).
        float4 _PrismOcclusionTarget;
        float4 _PrismOcclusionParams;

        // Dropped for a camera inside (or at) the mouth — shared by every pass so depth and colour
        // always agree.
        void ClipForInsideCamera()
        {
            float dist = distance(_WorldSpaceCameraPos, _WormholeSphere.xyz);
            clip(dist - (_WormholeSphere.w + max(_WormholeClearance, 0.0)));
        }

        // Dropped where the mouth stands between the camera and the pilot's ship — the prisms'
        // screen door, shared by every pass so depth and colour always agree.
        void ClipForOcclusionCorridor(float3 positionWS)
        {
            if (_WormholeCorridor < 0.5) return;
            float alpha, threshold;
            PrismOcclusionFade_float(positionWS, _PrismOcclusionTarget.xyz, _PrismOcclusionParams.xyz,
                1.0, alpha, threshold);
            clip(alpha - threshold);
        }

        // The rim's colour: the owning domain's hue when the mouth was given one.
        float3 RimColour()
        {
            return _WormholeRimTint.a > 0.5 ? _WormholeRimTint.rgb * _DomainRimBoost : _RimColor.rgb;
        }

        // A sealed mouth keeps only its fresnel shell; everything inside the cutoff is dropped, in
        // the depth pass as well as the colour pass. Returns the shell's strength.
        float SealedShell(float3 positionWS)
        {
            float3 n = normalize(positionWS - _WormholeSphere.xyz);
            float3 toEye = normalize(_WorldSpaceCameraPos - positionWS);
            float s = pow(1.0 - saturate(dot(n, toEye)), max(_SealedRimPower, 0.5));
            clip(s - _SealedRimCutoff);
            return s;
        }
        ENDHLSL

        Pass
        {
            Name "Wormhole"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // The fold's material: One Zero, ZWrite On (opaque). A seamless material: SrcAlpha
            // OneMinusSrcAlpha, ZWrite Off, transparent queue.
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma require 2darray

            TEXTURE2D(_WormholeExactTex);
            SAMPLER(sampler_WormholeExactTex);
            TEXTURE2D_ARRAY(_WormholePanorama);
            SAMPLER(sampler_WormholePanorama);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);

                // ComputeScreenPos, written out: xy in [0, w] with the render-target flip applied.
                float4 o = output.positionCS * 0.5;
                o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
                o.zw = output.positionCS.zw;
                output.screenPos = o;
                return output;
            }

            // WormholeGeometry.FaceOf + FaceUV, written out: the face is the direction's largest
            // component; right = cross(up, forward) is the face camera's own right.
            half3 SamplePanorama(float3 dir)
            {
                float3 a = abs(dir);
                float slice;
                float3 f;
                float3 u;
                if (a.x >= a.y && a.x >= a.z)
                {
                    bool p = dir.x >= 0.0;
                    slice = p ? 0.0 : 1.0;
                    f = float3(p ? 1.0 : -1.0, 0.0, 0.0);
                    u = float3(0.0, 1.0, 0.0);
                }
                else if (a.y >= a.z)
                {
                    bool p = dir.y >= 0.0;
                    slice = p ? 2.0 : 3.0;
                    f = float3(0.0, p ? 1.0 : -1.0, 0.0);
                    u = float3(0.0, 0.0, p ? -1.0 : 1.0);
                }
                else
                {
                    bool p = dir.z >= 0.0;
                    slice = p ? 4.0 : 5.0;
                    f = float3(0.0, 0.0, p ? 1.0 : -1.0);
                    u = float3(0.0, 1.0, 0.0);
                }
                float3 r = cross(u, f);
                float z = max(dot(dir, f), 1e-5);
                float2 uv = float2(dot(dir, r), dot(dir, u)) / z * 0.5 + 0.5;
                return SAMPLE_TEXTURE2D_ARRAY(_WormholePanorama, sampler_WormholePanorama, uv, slice).rgb;
            }

            // WormholeGeometry.ParallaxDirection: continue the ray from the entry point (relative to
            // this centre == relative to the partner's) to a proxy sphere about the capture point.
            float3 ParallaxDirection(float3 rel, float3 viewDir, float proxy)
            {
                float b = dot(rel, viewDir);
                float c = dot(rel, rel) - proxy * proxy;
                float t = -b + sqrt(max(b * b - c, 0.0));
                return rel + viewDir * max(t, 0.0);
            }

            half4 frag (Varyings input) : SV_Target
            {
                ClipForInsideCamera();
                ClipForOcclusionCorridor(input.positionWS);

                if (_WormholeSealed > 0.5)
                {
                    float shell = SealedShell(input.positionWS);
                    float lit = _SealedIntensity * shell + saturate(_WormholeFlare) * _FlareIntensity;
                    return half4(RimColour() * lit, 1.0);
                }

                float3 centre = _WormholeSphere.xyz;
                float radius = max(_WormholeSphere.w, 1e-4);
                float3 viewDir = normalize(input.positionWS - _WorldSpaceCameraPos);
                float3 rel = input.positionWS - centre;
                float3 normal = rel / max(length(rel), 1e-5);

                float exact = saturate(_WormholeExactBlend) * step(0.5, _WormholeMainView);

                half3 colour = _VoidColor.rgb;
                if (exact < 0.999)
                {
                    half3 pano = _VoidColor.rgb;
                    if (_WormholePanoramaReady > 0.5)
                    {
                        float proxy = max(_ProxyRadius, radius * 1.01);
                        pano = SamplePanorama(ParallaxDirection(rel, viewDir, proxy));
                    }
                    colour = pano;
                }
                if (exact > 0.001)
                {
                    float2 uv = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                    uv = uv * _WormholeExactUV.xy + _WormholeExactUV.zw;
                    half3 seen = SAMPLE_TEXTURE2D(_WormholeExactTex, sampler_WormholeExactTex, uv).rgb;
                    colour = lerp(colour, seen, exact);
                }

                // A SEAMLESS mouth (Docs/CRYSTAL_WORMHOLE.md) has no surface of its own at all: the view
                // beyond it dissolves into the world toward its silhouette — opaque at the centre of
                // the disc, gone at its edge, graded by the view ray's impact parameter — and there is
                // no rim and no flare. Nothing on screen says "here is a surface".
                if (_SoftEdge > 0.0)
                {
                    float3 toC = centre - _WorldSpaceCameraPos;
                    float b = length(toC - viewDir * dot(toC, viewDir));
                    float u = saturate(b / radius);
                    float alpha = 1.0 - smoothstep(1.0 - saturate(_SoftEdge), 1.0, u);
                    alpha = alpha * alpha * (3.0 - 2.0 * alpha);   // eased twice: no visible ramp
                    return half4(colour, alpha);
                }

                // The rim: the only thing on the surface that is the mouth itself rather than the
                // place beyond it — a faint darkening and a glow in the DOMAIN's hue at the
                // silhouette, punched up for a moment on every transit.
                float rim = pow(1.0 - saturate(dot(normal, -viewDir)), max(_RimPower, 0.5));
                float glow = _RimIntensity + saturate(_WormholeFlare) * _FlareIntensity;
                colour = colour * (1.0 - rim * saturate(_RimDarken)) + RimColour() * (rim * glow);

                return half4(colour, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma target 3.5

            struct AttributesDepth
            {
                float4 positionOS : POSITION;
            };

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            VaryingsDepth vertDepth (AttributesDepth input)
            {
                VaryingsDepth output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half fragDepth (VaryingsDepth input) : SV_Target
            {
                ClipForInsideCamera();
                ClipForOcclusionCorridor(input.positionWS);
                if (_WormholeSealed > 0.5) SealedShell(input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
