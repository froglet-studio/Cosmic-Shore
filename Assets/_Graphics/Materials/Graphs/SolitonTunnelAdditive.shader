// SolitonTunnelAdditive.shader — a Meerkat's reticle grid (lines) and its bridge's spindles (triangles): the colour is
// the vertex colour, already scaled by its brightness, added to the scene (R_VesselActions/MEERKAT.md). SolitonTunnelView
// builds both meshes once a frame and draws them as one identity instance each, through Resources/SolitonTunnelAdditive.mat.
Shader "CosmicShore/SolitonTunnelAdditive"
{
    Properties { }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+5" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SolitonTunnelAdditive"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex MeerkatAddVert
            #pragma fragment MeerkatAddFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings MeerkatAddVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = (half4)input.color;
                return output;
            }

            half4 MeerkatAddFrag(Varyings input) : SV_Target
            {
                return half4(input.color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
