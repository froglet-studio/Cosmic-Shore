// The Charge element's light (CrystalChargeArcs, Omni Motion Lab round 5): the seam walk on the Charge crystal and the
// chained flux arcs on the omni. CrystalChargeArcs writes ONE mesh per crystal each frame, in the crystal's own space:
// camera-facing bolt strips and the lit plates' faces. This pass draws both, additively, over the crystal already there.
//
// TEXCOORD0 = (across the strip -1..1, falloff from the light's centre 0..1, kind, intensity):
//   kind 0 - a bolt strip. Brightest down its middle, falling to nothing at its edges (the lab's ribbon). A bolt is a
//            halo strip and a body strip; a flux arc's thick middle also a narrow hot core, so under bloom it reads as a
//            glowing tube rather than a flat band.
//   kind 1 - a lit face (a petal beside the seam walk, a pentagon charging or taking a discharge), brighter toward its
//            rim, in the crystal's bright colour mixed toward the arc core colour.
//
// _BrightColor is painted from the crystal's body by CrystalAccentTint, so a team crystal's lit faces wear its domain
// colour; the bolts stay the near-white arc core colour (ChargeCrystalMaterial's own _ArcCoreColor).
Shader "Custom/CrystalChargeArcsShader"
{
    Properties
    {
        [HDR] _BrightColor  ("Bright Color (painted from the body)", Color) = (0.4862745, 0.9882354, 0, 1)
        [HDR] _ArcCoreColor ("Arc Core Color", Color) = (0.85, 0.95, 1.0, 1)
        _BoltIntensity      ("Bolt Intensity", Range(0, 8)) = 1.0
        _GlowIntensity      ("Glow Intensity", Range(0, 8)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+2"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CrystalChargeArcs"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            // Bolts face the camera from either side; a lit face sits just off its plate and is hidden by the body when
            // it faces away.
            Cull Off
            // A face rides a hair above its plate and an arc lands on a pentagon's corners: win the depth tie.
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BrightColor;
                float4 _ArcCoreColor;
                float  _BoltIntensity;
                float  _GlowIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 data       : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 data       : TEXCOORD0;
                float  fogFactor  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.data = input.data;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float intensity = input.data.w;
                float3 color;
                if (input.data.z < 0.5)
                {
                    // a strip: bright down the middle, nothing at the edges
                    float a = intensity * pow(saturate(1.0 - abs(input.data.x)), 1.6);
                    color = _ArcCoreColor.rgb * (a * _BoltIntensity);
                }
                else
                {
                    // a lit face: brighter toward the rim
                    float e = intensity * (0.35 + 0.65 * saturate(input.data.y));
                    color = lerp(_BrightColor.rgb, _ArcCoreColor.rgb, 0.55) * (e * 0.5 * _GlowIntensity);
                }
                // Additive: fog fades the light toward nothing, not toward the fog colour.
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
