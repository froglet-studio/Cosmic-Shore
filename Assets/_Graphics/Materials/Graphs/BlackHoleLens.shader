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
                float2 p = (uv - 0.5) * float2(aspect, 1.0);
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);

                float2 disp = float2(0.0, 0.0);
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
                    if (sceneZ < c.z - _BHWellM[i].w) continue;   // a foreground object is not bent
                    BlackHoleLensWell(p, c, _BHWellP[i], _BHWellM[i], _BHLook, _BHLook2, _BHWellT[i],
                                      disp, shadow, core, coreGlow, glow, shadowColour, coreColour);
                }
                if (BlackHoleLensUntouched(disp, shadow, core, glow)) discard;

                float2 suv = BlackHoleLensSampleUV(p, disp, aspect);
                float3 col = SAMPLE_TEXTURE2D_LOD(_BlackHoleSceneColor, sampler_LinearClamp, suv, 0).rgb;
                col = BlackHoleLensComposite(col, shadow, core, coreGlow, glow, _BHLook2.z, shadowColour, coreColour);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
