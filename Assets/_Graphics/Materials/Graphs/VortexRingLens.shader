// VortexRingLens.shader — every vortex ring the camera sees, drawn in ONE full-screen pass as a subtle doughnut of
// bent light (Docs/BLACK_HOLE.md §5.3). The sibling of BlackHoleLens.shader: the same plumbing, a toroid instead of
// a point. The per-ring maths lives in VortexRingLens.hlsl, which Tools/Shaders/verify_vortex_ring_lens.py runs on
// the CPU; this file is the URP plumbing.
//
// HOW IT DRAWS. VortexRingLensPass.cs, after URP has drawn the TRANSPARENTS, copies the camera colour
// (_VortexRingSceneColor) and draws one full-screen triangle with this shader. Each pixel sums every ring's pull,
// samples the copy at the displaced point (a whisper of chromatic split: red bent a little more, blue a little
// less) and adds the rim. The rings' screen ellipses are worked out per camera on the CPU from the camera's REAL
// matrices (VortexRingLens.ScreenRing), so the Pistol's off-axis seat lens is exact, and handed over in a
// MaterialPropertyBlock with the draw. A pixel no ring changes is discarded: it keeps exactly what the camera drew.
//
// THE KNOBS are this material's properties (Resources/VortexRingLens.mat): how hard the tube bends, how bright its
// rim is, the chromatic split, and the swirl. The pass never overrides them.
//
// Requires the camera's depth texture, which the project has OFF in URP_Asset; VortexRingLens switches it on
// (through BlackHoleLens.CameraSupport) for every game camera while a ring is live and restores it after.
Shader "CosmicShore/VortexRingLens"
{
    Properties
    {
        _VRBend ("Bend (how hard the tube bends the light, in tube widths)", Range(0, 2)) = 0.55
        _VRRim ("Rim (the tube edge's faint glow)", Range(0, 1)) = 0.07
        _VRChroma ("Chromatic split (share of the bend)", Range(0, 0.5)) = 0.12
        _VRSwirl ("Swirl (ripple across the tube, share of the bend)", Range(0, 1)) = 0.3
        _VRSwirlRate ("Swirl rate (radians of ripple per radian of the ring's spin)", Range(0, 2)) = 0.5
        [HideInInspector] _VRCount ("Rings in the bank (per camera, from VortexRingLensPass)", Float) = 0
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
            Name "VortexRingLens"
            // Drawn only by VortexRingLensPass (after the transparents), never by URP's own passes.
            Tags { "LightMode" = "VortexRingLens" }

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VortexRingLensVert
            #pragma fragment VortexRingLensFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "VortexRingLens.hlsl"

            // Per camera, with the draw (VortexRingLensPass → VortexRingLens.ScreenRings): one row per ring, the
            // nearest VORTEX_RING_LENS_MAX_RINGS first. Layout in VortexRingLens.hlsl.
            float4 _VRC[VORTEX_RING_LENS_MAX_RINGS];
            float4 _VRA[VORTEX_RING_LENS_MAX_RINGS];
            float4 _VRZ[VORTEX_RING_LENS_MAX_RINGS];
            float4 _VRL[VORTEX_RING_LENS_MAX_RINGS];
            float4 _VRT[VORTEX_RING_LENS_MAX_RINGS];
            float _VRCount;
            // x the camera's aspect, y the time (seconds) the swirl runs on
            float4 _VRView;

            float _VRBend;
            float _VRRim;
            float _VRChroma;
            float _VRSwirl;
            float _VRSwirlRate;

            // The scene as the camera drew it up to the lens, copied by VortexRingLensPass.cs (sampler_LinearClamp
            // comes with URP's Core.hlsl).
            TEXTURE2D(_VortexRingSceneColor);

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings VortexRingLensVert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            half4 VortexRingLensFrag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect = _VRView.x;
                float2 p = (uv - 0.5) * float2(aspect, 1.0);
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);

                float2 disp = float2(0.0, 0.0);
                float3 glow = float3(0.0, 0.0, 0.0);
                for (int i = 0; i < VORTEX_RING_LENS_MAX_RINGS; i++)
                {
                    if (i >= (int)_VRCount) break;
                    if (_VRA[i].w < 0.5) continue;
                    float4 l = _VRL[i];
                    // the material's knobs × the ring's own strength; the swirl's phase runs on the ring's spin
                    l = float4(l.x * _VRBend, l.y * _VRRim, _VRSwirl, _VRView.y * _VRSwirlRate * l.w);
                    VortexRingLensRing(p, sceneZ, _VRC[i], _VRA[i], _VRZ[i], l, _VRT[i], disp, glow);
                }
                if (VortexRingLensUntouched(disp, glow)) discard;

                float r = SAMPLE_TEXTURE2D_LOD(_VortexRingSceneColor, sampler_LinearClamp,
                                               VortexRingLensSampleUV(p, disp * (1.0 + _VRChroma), aspect), 0).r;
                float g = SAMPLE_TEXTURE2D_LOD(_VortexRingSceneColor, sampler_LinearClamp,
                                               VortexRingLensSampleUV(p, disp, aspect), 0).g;
                float b = SAMPLE_TEXTURE2D_LOD(_VortexRingSceneColor, sampler_LinearClamp,
                                               VortexRingLensSampleUV(p, disp * (1.0 - _VRChroma), aspect), 0).b;
                float3 col = float3(r, g, b);
                return half4(col + glow, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
