Shader "BlockShaders/Prism"
{
    Properties
    {
        _Spread ("Spread", Vector) = (0.3, 0.3, 0.3)
        _DistanceThreshold ("Distance Threshold", float) = 100000
    }

    SubShader
    {
        Tags
        {

        }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float3      Spread;
                float       DistanceThreshold;
            CBUFFER_END

            struct Attributes
            {
                float3  positionOS      : POSITION;
                float3  normal          : NORMAL;
                float3  tangent         : TANGENT;
            };

            struct Varyings
            {
                
            };

            // Pull into lib core
            float GetCamSqrDistance(half3 obj_pos)
            {
                float dist = _WorldSpaceCameraPos.xyz;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                half3


                return OUT;
            }

            half4 frag(Varyings IN)     : SV_TARGET
            {

            }

            ENDHLSL
        }
    }
}