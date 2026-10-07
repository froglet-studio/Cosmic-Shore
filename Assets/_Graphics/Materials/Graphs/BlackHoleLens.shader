// BlackHoleLens.shader — the black hole's gravitational lens and accretion disc (Docs/BLACK_HOLE.md
// §5.1). Everything that decides where a light ray goes lives in BlackHoleLens.hlsl, which
// Tools/Shaders/verify_black_hole_lens.py compiles with clang++ and executes; this file is the URP
// plumbing around it, and the verifier front-end compiles it with glslang against a URP mock.
//
// HOW IT DRAWS. One camera-facing billboard per hole, centred on the hole and sized to its lens
// (BlackHoleLens.cs sets the object's scale to the lens DIAMETER; the vertex stage turns the quad
// toward the camera). In the transparent queue — after URP has copied the opaque scene into
// _CameraOpaqueTexture — each pixel traces its light ray backwards around the hole and paints what
// that ray sees: the opaque scene in the BENT direction (prisms and the skybox smeared into arcs
// and rings), black where the ray fell through the horizon (the shadow), plus the accretion disc's
// glow every time the ray crossed it. The billboard sits at the hole's CENTRE depth, so anything in
// front of the hole occludes it by the ordinary depth test and is drawn unbent, exactly as it
// should be — only what is behind the hole is lensed.
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
        _BHSpin ("Spin axis (world xyz)", Vector) = (0, 0, 1, 0)
        _BHDisk ("Disc (inner r_s, outer r_s, density, brightness)", Vector) = (3, 14, 0.15, 3)
        _BHDisk2 ("Disc (peak temperature K, Doppler 0..1, phase time, noise scale)", Vector) = (7000, 1, 0, 1)
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
            ZTest LEqual
            Cull Off
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
                float4 _BHSpin;
                float4 _BHDisk;
                float4 _BHDisk2;
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

                // A camera-facing quad at the hole's centre, as wide as the object's scale (the
                // lens diameter). The view matrix's first two rows are the camera's right and up.
                float3 centre = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float size = length(float3(objectToWorld[0][0], objectToWorld[1][0], objectToWorld[2][0]));
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 positionWS = centre + (right * input.positionOS.x + up * input.positionOS.y) * size;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
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

                // Outside the lens circle there is nothing to do: show the scene as it is.
                float b = length(cross(x0, d));
                if (b >= lensR) discard;

                float3 axis = normalize(_BHSpin.xyz + float3(0.0, 0.0, 1e-6));
                float3 bent;
                float escaped;
                float4 diskLight;
                BlackHoleLensTrace(x0, d, lensR, (int)_BHLens.y, axis, _BHDisk, _BHDisk2, bent, escaped, diskLight);

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
                        float holeEye = dot(centre - eye, -UNITY_MATRIX_V[2].xyz);
                        float sampleEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                        float behind = step(holeEye - rs, sampleEye);
                        scene = lerp(sky, SampleSceneColor(uv), onScreen * behind);
                    }
                    background = scene;
                }

                // The disc's light rolled off by its brightest channel (hue kept), then over the
                // background it lets through — the project has no tonemapper to do it later.
                float3 colour = BlackHoleDiskTonemap(diskLight.rgb) + (1.0 - diskLight.a) * background;
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
