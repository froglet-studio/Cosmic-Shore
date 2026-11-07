// BrittleStarRingSmear.shader - the Brittle Star ring's MOTION SMEAR, normalized so it adds no brightness
// (Garrett, 2026-11-03; lab Docs/Studios/BrittleStarRingLab.html, "Forward + motion smear").
//
// HOW IT DRAWS. BrittleStarRingSmearPass.cs first draws the ring as it stands, with the hull's own materials, into
// _BrittleStarRingLayer (premultiplied colour, coverage in alpha, cleared to 0). Then it draws a disc in the ring's
// plane with this shader, depth-tested against the scene. Each disc pixel P is a point on every copy of the ring
// spread back over the angle the ring turned this frame; _RSBack[k] turns P back onto the ring as drawn, and the
// layer is read there. The copies are AVERAGED (weight 1 / _RSCount, summed in float) and the mean is blended over
// the frame once, premultiplied (One, OneMinusSrcAlpha): where every copy covers a pixel the result is exactly the
// still ring, and nowhere is it brighter. A copy whose point falls off the screen adds nothing (and still counts).
Shader "CosmicShore/BrittleStarRingSmear"
{
    Properties
    {
        [HideInInspector] _RSCount ("Copies averaged (per draw, from BrittleStarRingSmearPass)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BrittleStarRingSmear"
            // Drawn only by BrittleStarRingSmearPass (after the transparents), never by URP's own passes.
            Tags { "LightMode" = "BrittleStarRingSmear" }

            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RingSmearVert
            #pragma fragment RingSmearFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // BrittleStarRingSmearPass.MaxCopies
            #define RING_SMEAR_MAX_COPIES 32

            // The ring as drawn this frame (sampler_LinearClamp comes with URP's Core.hlsl: bilinear on premultiplied
            // colour is the right blend).
            TEXTURE2D(_BrittleStarRingLayer);
            // World to world: a point on copy k turned back onto the ring as drawn (copy 0 is the identity).
            float4x4 _RSBack[RING_SMEAR_MAX_COPIES];
            float _RSCount;
            // xy the layer's size in pixels, zw one over it
            float4 _RSLayerSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings RingSmearVert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 RingSmearFrag(Varyings input) : SV_Target
            {
                int count = (int)_RSCount;
                if (count < 1) discard;
                float4 sum = 0;
                [loop]
                for (int k = 0; k < RING_SMEAR_MAX_COPIES; k++)
                {
                    if (k >= count) break;
                    float3 onRing = mul(_RSBack[k], float4(input.positionWS, 1.0)).xyz;
                    // The same screen mapping URP uses for its own screen-space textures (Y flip included).
                    float2 uv = ComputeNormalizedDeviceCoordinates(onRing, UNITY_MATRIX_VP);
                    if (any(uv < 0.0) || any(uv > 1.0)) continue;
                    sum += SAMPLE_TEXTURE2D_LOD(_BrittleStarRingLayer, sampler_LinearClamp, uv, 0);
                }
                // The AVERAGE: every copy weighs 1 / count, so the weights sum to one.
                float4 mean = sum / count;
                if (mean.a <= 1e-4) discard;
                return half4(mean);
            }
            ENDHLSL
        }
    }
}
