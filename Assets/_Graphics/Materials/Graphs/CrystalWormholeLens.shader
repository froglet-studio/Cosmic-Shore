// CrystalWormholeLens.shader — the light of a crystal wormhole (Docs/CRYSTAL_WORMHOLE.md §2). Where light
// goes is CrystalWormholeLens.hlsl (the warp field's own optics, both throats glued); this file is the
// URP plumbing and the four pictures a traced ray is coloured from.
//
// HOW IT DRAWS. ONE sphere around the whole pair (CrystalWormholeView sizes it: half the separation plus
// the field's reach), and only its INSIDE — its far faces, Cull Front — so exactly one layer covers every
// pixel whose ray passes through the warped region, from any viewpoint, including from inside it, which
// is where a pilot flying through always is. Nothing about the sphere is ever seen: past its radius the
// field is exactly flat, so a ray that never comes near a pole leaves exactly as it came. Drawn by
// BlackHoleLensPass after the transparents (LightMode BlackHoleLens), from its copy of the frame.
//
// WHAT A RAY SHOWS, once traced to where it leaves the warp:
//   * THIS SIDE (it went through no throat, or through both): the frame copy at the bent direction, if
//     that is on screen and what the copy shows there lies beyond the bend (else the near pole's
//     panorama, else the sky).
//   * THE FAR SIDE (it went through once): the FAR EYE — a camera at the gameplay camera's pose taken
//     through the near throat (CrystalWormhole.Through), its near plane on the far throat — sampled at the
//     bent direction; or, for mass on the far side in front of that side's own bend (your own ship while
//     the camera follows it through), at the direction the ray LEFT the throat, by the far eye's depth.
//     Outside the far eye's frame: the panorama of the mouth the ray came out of.
//   * MASS IN FRONT OF THE BEND (the pilot's own hull, a prism beside them) is left where it is: the
//     pixel blends back to the frame by depth (CrystalWormholeBendDistance).
// Every picture is sampled at a mip chosen from how fast the ray's direction changes across the pixel,
// so the rings where both skies are wound up (around the edge of the crystal ball) read as smooth bands
// of colour instead of sparkle.
//
// Requires the camera's depth texture (BlackHoleLens.CameraSupport turns it on while a lens is live).
Shader "CosmicShore/CrystalWormholeLens"
{
    Properties
    {
        [Header(Written per frame by CrystalWormholeView through a MaterialPropertyBlock)]
        _CWPoleA ("Attractor (xyz, live amplitude x field weight)", Vector) = (0, 0, 0, 0)
        _CWPoleB ("Repulsor (xyz, live amplitude x field weight)", Vector) = (0, 0, 0, 0)
        _CWNeck ("Throat now, neck radius, felt neck radius, ray steps", Vector) = (0, 30, 150, 96)
        _CWTaper ("Taper in, taper out, panorama proxy, far side (1 attractor, 2 repulsor)", Vector) = (390, 570, 1200, 2)
        _CWLens ("Lens sphere (centre, radius)", Vector) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CrystalWormholeLens"
            // Drawn only by BlackHoleLensPass (after the transparents), never by URP's own passes.
            Tags { "LightMode" = "BlackHoleLens" }

            ZWrite Off
            ZTest Always      // depth is decided per pixel, against the depth texture
            Cull Front        // the INSIDE of the sphere: one layer, from outside it or within it
            Blend SrcAlpha OneMinusSrcAlpha   // alpha = how much of the pixel is bent (no discard: the
                                              // mip choice needs every lane's derivatives)

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CrystalWormholeVert
            #pragma fragment CrystalWormholeFrag
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "BlackHoleLens.hlsl"
            #include "CrystalWormholeLens.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CWPoleA;
                float4 _CWPoleB;
                float4 _CWNeck;
                float4 _CWTaper;
                float4 _CWLens;
            CBUFFER_END

            // Per renderer, outside the material's CBUFFER (textures and matrices only CrystalWormholeView sets).
            float4 _CWFarPos;            // xyz the far eye, w 1 when its picture is valid for this camera
            float4x4 _CWFarVP;           // world direction -> far eye clip (OpenGL convention)
            float4x4 _CWFarUvToView;     // (uv, raw depth, 1) -> far eye view space (reconstructs distance)
            float4 _CWFarSize;           // far eye target texels (w, h), its texels per radian, unused
            float4 _CWPanoReady;         // x attractor's panorama complete, y repulsor's, z face texels
            float _CWMainView;           // global: 1 while the camera the far eye was posed for is drawing

            TEXTURE2D(_BlackHoleSceneColor);
            TEXTURE2D(_CWFarTex);
            SAMPLER(sampler_CWFarTex);
            TEXTURE2D_FLOAT(_CWFarDepth);
            SAMPLER(sampler_CWFarDepth);
            TEXTURE2D_ARRAY(_CWPanoA);
            SAMPLER(sampler_CWPanoA);
            TEXTURE2D_ARRAY(_CWPanoB);
            SAMPLER(sampler_CWPanoB);
            TEXTURE2D_ARRAY(_BlackHoleSky);
            SAMPLER(sampler_BlackHoleSky);
            float _BlackHoleSkyReady;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings CrystalWormholeVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
                // Pinned just inside the far plane: a sphere wider than the far distance, or seen from
                // inside, is never clipped (its depth decides nothing — ZTest Always).
            #if UNITY_REVERSED_Z
                positionCS.z = positionCS.w * 1e-5;
            #else
                positionCS.z = positionCS.w * (1.0 - 1e-5);
            #endif
                output.positionWS = positionWS;
                output.positionCS = positionCS;
                return output;
            }

            CrystalWormholeField Field()
            {
                CrystalWormholeField f;
                f.poleA = _CWPoleA.xyz;
                f.poleB = _CWPoleB.xyz;
                f.ampA = _CWPoleA.w;
                f.ampB = _CWPoleB.w;
                f.throat = _CWNeck.x;
                f.neck = _CWNeck.y;
                f.feltNeck = _CWNeck.z;
                f.taperIn = _CWTaper.x;
                f.taperOut = _CWTaper.y;
                f.lensCentre = _CWLens.xyz;
                f.lensRadius = _CWLens.w;
                return f;
            }

            // Screen uv of a world direction for THIS camera, and how far inside the screen it is (fades to 0
            // over the outer 4%, so a ray bent off the edge changes source without a seam).
            float2 ScreenUV(float3 dir, out float onScreen)
            {
                float4 clip = mul(UNITY_MATRIX_VP, float4(dir, 0.0));
                if (!(clip.w > 1e-5)) { onScreen = 0.0; return float2(0.5, 0.5); }
                float4 screen = ComputeScreenPos(clip);
                float2 uv = screen.xy / screen.w;
                float2 edge = min(uv, 1.0 - uv);
                onScreen = saturate(min(edge.x, edge.y) / 0.04);
                return uv;
            }

            // The far eye's uv of a world direction, with the same edge fade.
            float2 FarUV(float3 dir, out float inFrame)
            {
                float4 clip = mul(_CWFarVP, float4(dir, 0.0));
                if (!(clip.w > 1e-5)) { inFrame = 0.0; return float2(0.5, 0.5); }
                float2 uv = clip.xy / clip.w * 0.5 + 0.5;
                float2 edge = min(uv, 1.0 - uv);
                inFrame = saturate(min(edge.x, edge.y) / 0.04);
                return uv;
            }

            float FarDistance(float2 uv)
            {
                float raw = SAMPLE_TEXTURE2D_LOD(_CWFarDepth, sampler_CWFarDepth, uv, 0).r;
                float4 v = mul(_CWFarUvToView, float4(uv, raw, 1.0));
                return length(v.xyz / max(abs(v.w), 1e-8));
            }

            float3 Sky(float3 dir)
            {
                if (_BlackHoleSkyReady < 0.5) return float3(0.0, 0.0, 0.0);
                float face;
                float2 uv = BlackHoleSkyFaceUV(dir, face);
                return SAMPLE_TEXTURE2D_ARRAY_LOD(_BlackHoleSky, sampler_BlackHoleSky, uv, face, 0).rgb;
            }

            // A pole's panorama (its six faces, the same table as the sky) by parallax: the ray leaving at
            // pos along dir is assumed to end on a proxy sphere about the capture point.
            float3 Panorama(float pole, float3 pos, float3 dir, float lod)
            {
                float3 centre = pole < 1.5 ? _CWPoleA.xyz : _CWPoleB.xyz;
                float ready = pole < 1.5 ? _CWPanoReady.x : _CWPanoReady.y;
                if (ready < 0.5) return Sky(dir);
                float3 rel = pos - centre;
                float b = dot(rel, dir);
                float c = dot(rel, rel) - _CWTaper.z * _CWTaper.z;
                float t = -b + sqrt(max(b * b - c, 0.0));
                float3 seen = normalize(rel + dir * max(t, 0.0));
                float face;
                float2 uv = BlackHoleSkyFaceUV(seen, face);
                return pole < 1.5
                    ? SAMPLE_TEXTURE2D_ARRAY_LOD(_CWPanoA, sampler_CWPanoA, uv, face, lod).rgb
                    : SAMPLE_TEXTURE2D_ARRAY_LOD(_CWPanoB, sampler_CWPanoB, uv, face, lod).rgb;
            }

            half4 CrystalWormholeFrag(Varyings input) : SV_Target
            {
                CrystalWormholeField f = Field();
                float3 eye = _WorldSpaceCameraPos;
                float3 dir = normalize(input.positionWS - eye);
                float2 pixelUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 viewForward = -UNITY_MATRIX_V[2].xyz;
                float pixelDistance = LinearEyeDepth(SampleSceneDepth(pixelUV), _ZBufferParams)
                                      / max(dot(dir, viewForward), 1e-4);

                float rEye = min(length(eye - f.poleA), length(eye - f.poleB));
                float nearest = 0.5 * max(rEye, f.throat);
                bool live = (f.ampA + f.ampB) > 1e-4 || f.throat > 0.0;

                float3 outPos = eye, outDir = dir, exitPos = eye, exitDir = dir;
                float crossings = 0.0, lastExit = 0.0, firstCross = -1.0, exhausted = 0.0;
                // The near field is never bent: skip the trace for a pixel whose mass is surely in front of
                // any bend (the bend distance is never nearer than `nearest`).
                if (live && pixelDistance >= 0.7 * nearest)
                    CrystalWormholeTrace(eye, dir, f, (int)_CWNeck.w, outPos, outDir, crossings, lastExit,
                                         exitPos, exitDir, firstCross, exhausted);

                // How fast the ray's direction changes across this pixel, in radians: the footprint every
                // picture is sampled at. Taken in uniform flow (no discard above), from every lane.
                float footprint = max(length(ddx(outDir)), length(ddy(outDir)));
                float exitFootprint = max(length(ddx(exitDir)), length(ddy(exitDir)));

                float bend = CrystalWormholeBendDistance(eye, dir, outPos, outDir, firstCross, nearest);
                float keep = 1.0 - smoothstep(0.7 * bend, bend, pixelDistance);
                bool through = frac(crossings * 0.5) > 0.25;     // an odd number of throats: the far side

                float3 colour;
                if (!through)
                {
                    float onScreen;
                    float2 uv = ScreenUV(outDir, onScreen);
                    float nearPole = length(eye - f.poleA) <= length(eye - f.poleB) ? 1.0 : 2.0;
                    float panoLod = log2(max(footprint * _CWPanoReady.z * 0.5, 1.0));
                    float3 fallback = Panorama(nearPole, outPos, outDir, panoLod);
                    float3 scene = fallback;
                    if (onScreen > 0.0)
                    {
                        float sampleDistance = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams)
                                               / max(dot(outDir, viewForward), 1e-4);
                        float beyond = smoothstep(0.7 * bend, bend, sampleDistance);
                        float3 copy = SAMPLE_TEXTURE2D_LOD(_BlackHoleSceneColor, sampler_LinearClamp, uv, 0).rgb;
                        scene = lerp(fallback, copy, onScreen * beyond);
                    }
                    colour = scene;
                }
                else
                {
                    float panoLod = log2(max(footprint * _CWPanoReady.z * 0.5, 1.0));
                    float3 pano = Panorama(lastExit, outPos, outDir, panoLod);
                    colour = pano;
                    bool farEye = _CWFarPos.w > 0.5 && _CWMainView > 0.5 && abs(lastExit - _CWTaper.w) < 0.5;
                    if (farEye)
                    {
                        float inFrame, nearInFrame;
                        float2 uv = FarUV(outDir, inFrame);
                        float2 nearUV = FarUV(exitDir, nearInFrame);
                        float lod = log2(max(footprint * _CWFarSize.z, 1.0));
                        float nearLod = log2(max(exitFootprint * _CWFarSize.z, 1.0));
                        float3 far = SAMPLE_TEXTURE2D_LOD(_CWFarTex, sampler_CWFarTex, uv, lod).rgb;
                        // Mass on the far side in front of that side's own bend is seen along the direction
                        // the ray left the throat.
                        float farBend = CrystalWormholeBendDistance(exitPos, exitDir, outPos, outDir, -1.0, 0.0);
                        float farBendFromEye = max(length(exitPos + exitDir * min(farBend, 1e6) - _CWFarPos.xyz), nearest);
                        float nearDistance = FarDistance(nearUV);
                        float nearKeep = nearInFrame * (1.0 - smoothstep(0.7 * farBendFromEye, farBendFromEye, nearDistance));
                        float3 nearColour = SAMPLE_TEXTURE2D_LOD(_CWFarTex, sampler_CWFarTex, nearUV, nearLod).rgb;
                        far = lerp(far, nearColour, nearKeep);
                        colour = lerp(pano, far, max(inFrame, nearKeep));
                    }
                }

                return half4(colour, live ? 1.0 - keep : 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
