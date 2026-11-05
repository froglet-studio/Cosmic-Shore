// BlackHoleLens.shader — every black and white hole the camera sees, drawn in ONE full-screen pass: the
// Vessel Studio's lens (Docs/Studios/StoatFlightStudio.html, `lensMat`), ported line for line
// (Docs/BLACK_HOLE.md §5.1). The per-hole step and the composite live in BlackHoleLens.hlsl, which
// Tools/Shaders/verify_black_hole_lens.py runs against the studio's own GLSL; this file is the URP plumbing.
//
// HOW IT DRAWS. BlackHoleLensPass.cs, after URP has drawn the TRANSPARENTS, copies the camera colour
// (_BlackHoleSceneColor: opaques, skybox AND transparents — the snow shards, particles) and draws one
// full-screen triangle with this shader. Each pixel sums every hole's displacement, samples the copy once at
// the displaced point, and lays the white core, the photon ring and the shadow over it. The holes' screen
// positions and sizes are worked out per camera on the CPU (BlackHoleLens.ScreenWell) and handed over in a
// MaterialPropertyBlock with the draw. A pixel no hole changes is discarded: it keeps exactly what the camera drew.
//
// WHAT IS BENT. A hole is skipped for a pixel whose opaque depth is in FRONT of the hole by more than its margin
// (the studio's `sceneZ < wC.z − wM.w`): a vessel between you and the hole is drawn unbent over it. The copy
// cannot see behind what is in front, so a bend that lands on a foreground object shows that object, as in the
// studio. A ray bent off the screen shows the screen mirrored at its edge, as in the studio.
//
// Requires the camera's depth texture, which the project has OFF in URP_Asset; BlackHoleLens.CameraSupport
// switches it on for every game camera while a hole is live and restores it after.
//
// THE WAVES' LIGHT IS A SECOND PASS (Docs/BLACK_HOLE.md §5.2, 2026-10-28). The studio composites its lens on the
// FINAL picture, in display (sRGB) values, into an 8-bit target with no bloom: a light shell's 0.35 glow and a passing
// shell's 0.3 wash are display values. Drawn here, before URP's post-processing, the same numbers were LINEAR HDR
// light fed to the gameplay Bloom (threshold 0.2, intensity 2.5) and the tonemapper — a white flash that filled the
// screen (play, 2026-10-14). So pass 0 bends the scene and adds only the holes' own light (the waves' bend and
// trough darkening stay here); pass 1, "BlackHoleWaveLight", drawn by BlackHoleLensPass AFTER post-processing,
// adds the shells' and crests' glow and the shell's wash to the finished picture in display values — the studio's
// sum, where the studio adds it.
Shader "CosmicShore/BlackHoleLens"
{
    Properties
    {
        [HideInInspector] _BHWellCount ("Holes in the bank (per camera, from BlackHoleLensPass)", Float) = 0
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
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BlackHoleLensVert
            #pragma fragment BlackHoleLensFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "BlackHoleLens.hlsl"

            // Per camera, with the draw (BlackHoleLensPass → BlackHoleLens.ScreenWells): one row per hole, the
            // nearest BLACK_HOLE_LENS_MAX_WELLS first. Layout in BlackHoleLens.hlsl.
            float4 _BHWellC[BLACK_HOLE_LENS_MAX_WELLS];
            float4 _BHWellP[BLACK_HOLE_LENS_MAX_WELLS];
            float4 _BHWellM[BLACK_HOLE_LENS_MAX_WELLS];
            float4 _BHWellT[BLACK_HOLE_LENS_MAX_WELLS];
            float _BHWellCount;
            // x kShadow, y kCore, z ring glow, w ring width (BlackHoleConfig)
            float4 _BHLook;
            // x lens fade start, y white core brightness, z white core sky mix, w the camera's aspect
            float4 _BHLook2;
            // The waves (BlackHoleWaves.ScreenWaves): light shells and the gravitational wave, the newest 4 first.
            float4 _BHWaveC[4];
            float4 _BHWaveP[4];
            float4 _BHWaveQ[4];
            float4 _BHWaveA[4];
            float _BHWaveCount;
            // x gwCam (the view's own stretch), y the wash of a shell passing the camera, z gwGlow
            float4 _BHWave;

            // The scene as the camera drew it up to the lens — opaques, skybox and transparents — copied by
            // BlackHoleLensPass.cs (sampler_LinearClamp comes with URP's Core.hlsl).
            TEXTURE2D(_BlackHoleSceneColor);

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings BlackHoleLensVert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            half4 BlackHoleLensFrag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect = _BHLook2.w;
                float2 p0 = (uv - 0.5) * float2(aspect, 1.0);
                float2 p = BlackHoleLensViewStretch(p0, _BHWave.x);
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);

                float2 disp = float2(0.0, 0.0);
                float squeeze = 0.0;
                float shadow = 0.0;
                float core = 0.0;
                float coreGlow = 0.0;
                float3 glow = float3(0.0, 0.0, 0.0);
                float3 waveGlow = float3(0.0, 0.0, 0.0);   // the waves' light: added after post-processing (pass 1)
                float3 shadowColour = float3(0.0, 0.0, 0.0);
                float3 coreColour = BLACK_HOLE_LENS_CORE_COLOUR;
                for (int k = 0; k < 4; k++)
                {
                    if (k >= (int)_BHWaveCount) break;
                    if (_BHWaveC[k].w < 0.5) continue;
                    BlackHoleLensWave(p, _BHWaveC[k], _BHWaveP[k], _BHWaveQ[k], _BHWaveA[k], _BHWave.z, disp, waveGlow, squeeze);
                }
                for (int i = 0; i < BLACK_HOLE_LENS_MAX_WELLS; i++)
                {
                    if (i >= (int)_BHWellCount) break;
                    float4 c = _BHWellC[i];
                    if (c.w < 0.5) continue;
                    if (sceneZ < c.z - _BHWellM[i].w) continue;   // a foreground object is not bent
                    BlackHoleLensWell(p, c, _BHWellP[i], _BHWellM[i], _BHLook, _BHLook2, _BHWellT[i],
                                      disp, shadow, core, coreGlow, glow, shadowColour, coreColour);
                }
                // No wash here: it is the waves' light, pass 1's.
                if (BlackHoleLensUntouched(p - p0 + disp, shadow, core, glow, squeeze, 0.0)) discard;

                float2 suv = BlackHoleLensSampleUV(p, disp, aspect);
                float3 col = SAMPLE_TEXTURE2D_LOD(_BlackHoleSceneColor, sampler_LinearClamp, suv, 0).rgb;
                col = BlackHoleLensComposite(col, shadow, core, coreGlow, glow, _BHLook2.z, shadowColour, coreColour,
                                             squeeze, 0.0);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            // The waves' LIGHT on the finished picture (see the header): drawn by BlackHoleLensPass's second pass at
            // AfterRenderingPostProcessing, from its copy of the camera colour AFTER bloom and tonemapping, so the
            // studio's display values are added as display values and nothing blooms them.
            Name "BlackHoleWaveLight"
            Tags { "LightMode" = "BlackHoleWaveLight" }

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BlackHoleLensVert
            #pragma fragment BlackHoleWaveLightFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "BlackHoleLens.hlsl"

            // The same per-camera rows as pass 0 (BlackHoleLensPass sets both draws from one camera's numbers).
            float4 _BHWellC[BLACK_HOLE_LENS_MAX_WELLS];
            float4 _BHWellP[BLACK_HOLE_LENS_MAX_WELLS];
            float4 _BHWellM[BLACK_HOLE_LENS_MAX_WELLS];
            float4 _BHWellT[BLACK_HOLE_LENS_MAX_WELLS];
            float _BHWellCount;
            float4 _BHLook;
            float4 _BHLook2;
            float4 _BHWaveC[4];
            float4 _BHWaveP[4];
            float4 _BHWaveQ[4];
            float4 _BHWaveA[4];
            float _BHWaveCount;
            float4 _BHWave;

            // The camera colour after post-processing, copied by BlackHoleLensPass.
            TEXTURE2D(_BlackHoleSceneColor);

            // Display values: the studio's buffer is what the screen shows. In a Linear project the camera colour is
            // linear light, so the light is added to its sRGB encoding and decoded back (a Gamma project needs neither).
            #if defined(UNITY_COLORSPACE_GAMMA)
                #define BLACK_HOLE_TO_DISPLAY(c) (c)
                #define BLACK_HOLE_FROM_DISPLAY(c) (c)
            #else
                #define BLACK_HOLE_TO_DISPLAY(c) LinearToSRGB(max(c, 0.0))
                #define BLACK_HOLE_FROM_DISPLAY(c) SRGBToLinear(c)
            #endif

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings BlackHoleLensVert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            half4 BlackHoleWaveLightFrag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect = _BHLook2.w;
                float2 p0 = (uv - 0.5) * float2(aspect, 1.0);
                float2 p = BlackHoleLensViewStretch(p0, _BHWave.x);
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);

                // The waves' light at this pixel (pass 0 took their bend and trough darkening).
                float2 disp = float2(0.0, 0.0);
                float squeeze = 0.0;
                float3 waveGlow = float3(0.0, 0.0, 0.0);
                for (int k = 0; k < 4; k++)
                {
                    if (k >= (int)_BHWaveCount) break;
                    if (_BHWaveC[k].w < 0.5) continue;
                    BlackHoleLensWave(p, _BHWaveC[k], _BHWaveP[k], _BHWaveQ[k], _BHWaveA[k], _BHWave.z, disp, waveGlow, squeeze);
                }
                // A hole's shadow is drawn last in the studio, over every light: no wave light there.
                float shadow = 0.0;
                float core = 0.0;
                float coreGlow = 0.0;
                float3 glow = float3(0.0, 0.0, 0.0);
                float3 shadowColour = float3(0.0, 0.0, 0.0);
                float3 coreColour = BLACK_HOLE_LENS_CORE_COLOUR;
                for (int i = 0; i < BLACK_HOLE_LENS_MAX_WELLS; i++)
                {
                    if (i >= (int)_BHWellCount) break;
                    float4 c = _BHWellC[i];
                    if (c.w < 0.5) continue;
                    if (sceneZ < c.z - _BHWellM[i].w) continue;   // a foreground object is not shadowed
                    BlackHoleLensWell(p, c, _BHWellP[i], _BHWellM[i], _BHLook, _BHLook2, _BHWellT[i],
                                      disp, shadow, core, coreGlow, glow, shadowColour, coreColour);
                }
                if (BlackHoleLensWaveLightUntouched(shadow, waveGlow, _BHWave.y)) discard;

                float3 col = SAMPLE_TEXTURE2D_LOD(_BlackHoleSceneColor, sampler_LinearClamp, uv, 0).rgb;
                col = BLACK_HOLE_FROM_DISPLAY(BlackHoleLensWaveLight(BLACK_HOLE_TO_DISPLAY(col), waveGlow, _BHWave.y));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
