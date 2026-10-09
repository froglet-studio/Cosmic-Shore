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
// Drawn by BlackHoleLensPass.cs AFTER the transparents, from its copy of the camera colour
// (_BlackHoleSceneColor: opaques, skybox AND transparents — the snow shards, particles), each pixel
// traces its light ray backwards around the hole and paints what that ray sees: the scene in the
// BENT direction (prisms, shards and the skybox smeared into arcs and rings), or black where the ray
// fell through the horizon (the shadow). The pass's LightMode is BlackHoleLens, so URP's own passes
// never draw it. (It first drew in the transparent queue from URP's _CameraOpaqueTexture, a copy
// taken BEFORE any transparent: the shards were never bent, and the lens painted over the ones
// behind it — a 30 r_s ball with no shards in it.) There is no painted accretion disc: what
// orbits the hole is the real mass the gravity field moves.
//
// WHAT IS LENSED. Only what is BEHIND the hole: a pixel whose opaque scene depth is in front of the
// hole's centre is left alone (discarded, the scene shows through unbent). The quad got that from
// the hardware depth test at the hole's depth; the sphere's faces are not at that depth, so the
// test is made in the shader against the depth texture (ZTest Always), which is also what lets the
// sphere draw when its far side is behind other geometry or past the far plane.
//
// Requires the camera's depth texture, which the project has OFF in URP_Asset; BlackHoleLens.cs
// switches it on per camera (UniversalAdditionalCameraData) only while a hole is live, and restores
// it after. A transparent IN FRONT of the hole has no depth, so it is in the copy and bent with the
// background (stated limit; opaque mass in front stays unbent). Two screen-space limits, stated: a bent ray that leaves the
// screen samples THE SCENE'S OWN SKYBOX instead (RenderSettings.skybox, rendered by BlackHoleSky.cs
// into six faces — so off-screen PRISMS are not lensed in, but the sky is the real one), and a bent
// ray that lands on something IN FRONT of the hole is rejected the same way (the copy cannot see what
// that object hides).
Shader "CosmicShore/BlackHoleLens"
{
    Properties
    {
        [Header(Written per hole by BlackHoleLens.cs through a MaterialPropertyBlock)]
        _BHHorizon ("Horizon radius r_s (world units), eased", Float) = 1
        _BHLens ("Lens (radius in r_s, step budget, trace polarity: +1 every horizon hole / -1 a smooth source, bend fade start 0..1)", Vector) = (30, 128, 1, 0.55)
        _BHThroat ("Wormhole mouth radius at the centre (world units, 0 = none)", Float) = 0
        _BHSmooth ("Smooth well lens strength A, signed by polarity outside (0 = the black hole's ray trace)", Float) = 0
        _BHWhite ("1 = a WHITE hole: its horizon disc emits a white-hot core", Float) = 0
        _BHCore ("White core (brightness HDR, sky mix, 0, 0)", Vector) = (4, 0.8, 0, 0)
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
            // Drawn only by BlackHoleLensPass (after the transparents), never by URP's own passes.
            Tags { "LightMode" = "BlackHoleLens" }

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "BlackHoleLens.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BHHorizon;
                float4 _BHLens;
                float _BHThroat;
                float _BHSmooth;
                float _BHWhite;
                float4 _BHCore;
            CBUFFER_END

            // The scene as the camera drew it up to the lens — opaques, skybox and transparents —
            // copied by BlackHoleLensPass.cs (sampler_LinearClamp comes with URP's Core.hlsl).
            // The SMOOTH WELLS, every one (BlackHoleLens.PublishSmoothWells, per frame): xyz centre, w core
            // width; strength.x the signed, amplitude-scaled lens strength. Summed by every smooth lens.
            float4 _SmoothWellCentre[4];
            float4 _SmoothWellStrength[4];
            float _SmoothWellCount;

            TEXTURE2D(_BlackHoleSceneColor);

            float3 BlackHoleSceneColour(float2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(_BlackHoleSceneColor, sampler_LinearClamp, uv, 0).rgb;
            }

            // The sky: the scene's own skybox in six faces (BlackHoleSky.cs; face layout in
            // BlackHoleSkyFaceUV). Per-frame globals, outside the material's CBUFFER. _BlackHoleSkyReady
            // is 0 until the first render lands — an unbound array must not be read as a sky.
            TEXTURE2D_ARRAY(_BlackHoleSky);
            SAMPLER(sampler_BlackHoleSky);
            float _BlackHoleSkyReady;

            float3 BlackHoleSkyColour(float3 dir)
            {
                if (_BlackHoleSkyReady < 0.5) return float3(0.0, 0.0, 0.0);
                float face;
                float2 uv = BlackHoleSkyFaceUV(dir, face);
                return SAMPLE_TEXTURE2D_ARRAY_LOD(_BlackHoleSky, sampler_BlackHoleSky, uv, face, 0).rgb;
            }

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
                // _BHLens.z is the polarity: +1 an attractor / black hole, −1 a repulsor / white hole.
                float polarity = _BHLens.z < 0.0 ? -1.0 : 1.0;
                if (_BHSmooth > 0.0)
                {
                    // A SMOOTH well (Docs/CRYSTAL_WORMHOLE.md): the graded bulge replaces the trace —
                    // nothing is captured, nothing folds — and it is the SUM over every smooth well, so
                    // overlapping lenses agree and opposite wells cancel.
                    float3 deflection = float3(0.0, 0.0, 0.0);
                    for (int w = 0; w < 4; w++)
                    {
                        if (w >= (int)_SmoothWellCount) break;
                        float4 wc = _SmoothWellCentre[w];
                        deflection += BlackHoleSmoothLensDeflection(eye - wc.xyz, d, wc.w, _SmoothWellStrength[w].x);
                    }
                    bent = BlackHoleSmoothLensApply(d, deflection);
                    escaped = 1.0;
                }
                else
                {
                    BlackHoleLensTraceSigned(x0, d, lensR, (int)_BHLens.y, polarity, bent, escaped);
                }

                float3 background = float3(0.0, 0.0, 0.0);
                if (escaped < 0.5 && _BHWhite > 0.5)
                {
                    // A WHITE hole (Docs/BLACK_HOLE.md §11): light comes OUT of the horizon. Outside
                    // the horizon its spacetime is the black hole's, so the trace is the same (a
                    // horizon hole's lens is traced with polarity +1 whatever its sign — §13); the
                    // backward trace crossed the horizon travelling along `bent`, the line the light
                    // came out on, carrying what fell into the paired black hole from the far side —
                    // the sky continues through the tunnel. White-hot at the core's centre (b → 0),
                    // the emitted sky showing through toward its rim (b → b_c = 2.598 r_s).
                    float t = saturate(b / 2.598);
                    float glow = (1.0 - t) * (1.0 - t);
                    background = BlackHoleSkyColour(bent) * _BHCore.y + _BHCore.x * glow;
                }
                else if (escaped > 0.5)
                {
                    // The smooth lens fades itself (Gaussian); the trace is faded toward the lens edge.
                    float3 dirOut = _BHSmooth > 0.0 ? bent : BlackHoleLensFadeDir(d, bent, b, lensR, _BHLens.w);

                    // The sky in that direction: the scene's own skybox (BlackHoleSky.cs).
                    float3 sky = BlackHoleSkyColour(dirOut);

                    // The scene in that direction, if it is on screen and BEHIND the hole. A sample
                    // that lands on something in front of the hole is something the copy cannot
                    // see past — use the sky there instead of drawing a ghost of it.
                    float onScreen;
                    float2 uv = BlackHoleDirToScreenUV(dirOut, onScreen);
                    float3 scene = sky;
                    if (onScreen > 0.0)
                    {
                        float sampleEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                        // A wormhole mouth seated at the centre (Docs/BLACK_HOLE.md §12) is solid to
                        // the lens: a bent ray that lands on it is something the copy cannot see past,
                        // so it takes the sky — the mouth is seen only where it is, in place of the
                        // shadow (the depth test above already shows it there), never in the rings.
                        float front = _BHThroat > 0.0 ? holeEye + _BHThroat : holeEye - rs;
                        float behind = step(front, sampleEye);
                        scene = lerp(sky, BlackHoleSceneColour(uv), onScreen * behind);
                    }
                    background = scene;
                }

                // The bent scene, the shadow's black (a sink only), or a white hole's core.
                return half4(background, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
