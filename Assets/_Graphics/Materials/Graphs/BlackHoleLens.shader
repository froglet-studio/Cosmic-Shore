// BlackHoleLens.shader — the black hole's gravitational lens and its shadow (Docs/BLACK_HOLE.md
// §5.1). Everything that decides where a light ray goes lives in BlackHoleLens.hlsl, which
// Tools/Shaders/verify_black_hole_lens.py compiles with clang++ and executes; this file is the URP
// plumbing around it, and the verifier front-end compiles it with glslang against a URP mock.
//
// HOW IT DRAWS. One SPHERE per hole — the lens volume itself, centred on the hole (BlackHoleLens.cs
// sets the object's scale to the lens DIAMETER; the mesh circumscribes the unit-diameter sphere).
// Its BACK faces are drawn (Cull Front), so exactly one layer covers every pixel whose ray passes
// through the lens, from any viewpoint: far away, up close, off to one side, or with the camera
// INSIDE the lens. (The first version drew a camera-facing quad at the hole's centre depth; a quad
// covers the lens's true screen footprint only from far away, so close up the lens was cut off at
// a hard edge, and from inside it, or with the hole behind the camera, it could not be seen at all.)
// In the transparent queue — after URP has copied the opaque scene into _CameraOpaqueTexture —
// each pixel traces its light ray backwards around the hole and paints what that ray sees: the
// opaque scene in the BENT direction (prisms and the skybox smeared into arcs and rings), or black
// where the ray fell through the horizon (the shadow). There is no painted accretion disc: what
// orbits the hole is the real mass the gravity field moves.
//
// WHAT IS LENSED. Only what is BEHIND the hole: a pixel whose opaque scene depth is in front of the
// hole's centre is left alone (discarded, the scene shows through unbent). The quad got that from
// the hardware depth test at the hole's depth; the sphere's faces are not at that depth, so the
// test is made in the shader against the depth texture (ZTest Always), which is also what lets the
// sphere draw when its far side is behind other geometry or past the far plane.
//
// Requires the camera's opaque and depth textures, which the project has OFF in URP_Asset;
// BlackHoleLens.cs switches them on per camera (UniversalAdditionalCameraData) only while a hole is
// live, and restores them after. Two screen-space limits, stated: a bent ray that leaves the
// screen samples the sky reflection cubemap instead (so off-screen PRISMS are not lensed in), and a
// bent ray that lands on something IN FRONT of the hole is rejected the same way (the copy cannot
// see what that object hides).
Shader "CosmicShore/BlackHoleLens"
{
    Properties
    {
        [Header(Written per hole by BlackHoleLens.cs through a MaterialPropertyBlock)]
        _BHHorizon ("Horizon radius r_s (world units), eased", Float) = 1
        _BHLens ("Lens (radius in r_s, step budget, unused, bend fade start 0..1)", Vector) = (30, 128, 1, 0.55)
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
            Name "BlackHoleLens"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest Always      // depth is tested in the fragment stage against the hole's centre
            Cull Front        // the far side of the lens sphere: one layer, inside or outside it
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BlackHoleLensVert
            #pragma fragment BlackHoleLensFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // DecodeHDREnvironment (the sky cubemap's HDR decode) lives here, and Core.hlsl does not
            // reach it - without this include the shader does not compile and draws magenta.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "BlackHoleLens.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BHHorizon;
                float4 _BHLens;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings BlackHoleLensVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // The lens sphere, as authored. Its depth decides nothing (ZTest Always, no depth
                // write) except clipping, so it is pinned just inside the far plane: a lens wider
                // than the camera's far distance, or seen from inside, is never cut.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
            #if UNITY_REVERSED_Z
                positionCS.z = positionCS.w * 1e-5;
            #else
                positionCS.z = positionCS.w * (1.0 - 1e-5);
            #endif

                output.positionWS = positionWS;
                output.positionCS = positionCS;
                return output;
            }

            // Screen uv of a world DIRECTION from the camera (a point at infinity): what the scene
            // shows in that direction, if it is on screen. Negative w = behind the camera.
            float2 BlackHoleDirToScreenUV(float3 dir, out float onScreen)
            {
                float4 clip = mul(UNITY_MATRIX_VP, float4(dir, 0.0));
                if (!(clip.w > 1e-5))
                {
                    onScreen = 0.0;
                    return float2(0.5, 0.5);
                }
                float4 screen = ComputeScreenPos(clip);
                float2 uv = screen.xy / screen.w;
                float2 edge = min(uv, 1.0 - uv);
                // Fade toward the sky over the outer 4% of the screen, so a ray bent off the edge
                // does not switch source with a seam.
                onScreen = saturate(min(edge.x, edge.y) / 0.04);
                return uv;
            }

            half4 BlackHoleLensFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float rs = _BHHorizon;
                if (!(rs > 1e-4)) discard;            // eased out: the hole is gone

                float3 centre = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 eye = _WorldSpaceCameraPos;
                float3 d = normalize(input.positionWS - eye);
                float3 x0 = (eye - centre) / rs;
                float lensR = _BHLens.x;

                // Outside the lens there is nothing to do: show the scene as it is. (The mesh
                // circumscribes the lens sphere, so its corners reach just past it.)
                float b = length(cross(x0, d));
                if (b >= lensR) discard;

                // Only what is BEHIND the hole is lensed: where the opaque scene at this pixel is
                // in front of the hole's centre, it is drawn unbent (the depth test the old quad
                // got from the hardware, made here because the sphere's faces are not at the
                // hole's depth). Before the trace, so an occluded pixel costs one depth read.
                float3 viewForward = -UNITY_MATRIX_V[2].xyz;
                float holeEye = dot(centre - eye, viewForward);
                float2 pixelUV = GetNormalizedScreenSpaceUV(input.positionCS);
                if (LinearEyeDepth(SampleSceneDepth(pixelUV), _ZBufferParams) < holeEye) discard;

                float3 bent;
                float escaped;
                BlackHoleLensTrace(x0, d, lensR, (int)_BHLens.y, bent, escaped);

                float3 background = float3(0.0, 0.0, 0.0);
                if (escaped > 0.5)
                {
                    float3 dirOut = BlackHoleLensFadeDir(d, bent, b, lensR, _BHLens.w);

                    // The sky in that direction: the reflection cubemap URP keeps of the skybox.
                    float3 sky = DecodeHDREnvironment(
                        SAMPLE_TEXTURECUBE_LOD(_GlossyEnvironmentCubeMap, sampler_GlossyEnvironmentCubeMap, dirOut, 0),
                        _GlossyEnvironmentCubeMap_HDR);

                    // The scene in that direction, if it is on screen and BEHIND the hole. A sample
                    // that lands on something in front of the hole is something the copy cannot
                    // see past — use the sky there instead of drawing a ghost of it.
                    float onScreen;
                    float2 uv = BlackHoleDirToScreenUV(dirOut, onScreen);
                    float3 scene = sky;
                    if (onScreen > 0.0)
                    {
                        float sampleEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                        float behind = step(holeEye - rs, sampleEye);
                        scene = lerp(sky, SampleSceneColor(uv), onScreen * behind);
                    }
                    background = scene;
                }

                // The bent scene, or the shadow's black.
                return half4(background, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
