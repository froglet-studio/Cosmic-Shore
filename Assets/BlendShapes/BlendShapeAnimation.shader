Shader "Custom/BlendShapeAnimation"
{
    Properties
    {
        _BlendShapeData ("Blend Shape Data", 2D) = "white" {}
        _AnimationDuration ("Animation Duration", Float) = 1
        [Header(Debug Options)]
        _UseManualWeights ("Use Manual Weights", Float) = 0
        _BlendWeights ("Manual Blend Weights", Vector) = (0,0,0,0)
        _PauseAtProgress ("Pause At Progress", Range(0,1)) = 0
        _DebugMode ("Debug Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            sampler2D _BlendShapeData;
            float _AnimationDuration;
            float _UseManualWeights;
            float4 _BlendWeights;
            float _PauseAtProgress;
            float _DebugMode;

            // Sample blend shape offset placeholder
            float3 SampleBlendShapeOffset(uint vertexID, uint shapeIndex, bool sampleNormal)
            {
                return float3(0,0,0);
            }

            float4 GetBlendShapeWeightsEnhanced(float time)
            {
                // Check for manual override
                if (_UseManualWeights > 0.5)
                {
                    return _BlendWeights;
                }
                
                // Check for pause
                float normalizedTime;
                if (_PauseAtProgress > 0)
                {
                    normalizedTime = _PauseAtProgress;
                }
                else
                {
                    normalizedTime = frac(time / _AnimationDuration);
                }
                
                float4 weights = float4(0, 0, 0, 0);
                
                // Enhanced easing for smoother transitions
                if (normalizedTime < 0.25)
                {
                    float t = normalizedTime * 4.0;
                    t = smoothstep(0.0, 1.0, t); // Smooth interpolation
                    weights.x = t;
                }
                else if (normalizedTime < 0.5)
                {
                    float t = (normalizedTime - 0.25) * 4.0;
                    t = smoothstep(0.0, 1.0, t);
                    weights.x = 1.0;
                    weights.y = t;
                }
                else if (normalizedTime < 0.75)
                {
                    float t = (normalizedTime - 0.5) * 4.0;
                    t = smoothstep(0.0, 1.0, t);
                    weights.z = t;
                }
                else
                {
                    float t = (normalizedTime - 0.75) * 4.0;
                    t = smoothstep(0.0, 1.0, t);
                    weights.z = 1.0;
                    weights.w = t;
                }
                
                return weights;
            }

            void ApplyBlendShapesWithNormalPreservation(
                inout float3 positionOS, 
                inout float3 normalOS, 
                inout float3 tangentOS,
                uint vertexID)
            {
                float4 weights = GetBlendShapeWeightsEnhanced(_Time.y);
                
                // Store original normal for blending
                float3 originalNormal = normalOS;
                float3 accumulatedNormalDelta = float3(0, 0, 0);
                
                // Apply each blend shape
                for (uint i = 0; i < 4; i++)
                {
                    if (weights[i] > 0.001)
                    {
                        float3 vertexDelta = SampleBlendShapeOffset(vertexID, i, false);
                        float3 normalDelta = SampleBlendShapeOffset(vertexID, i, true);
                        
                        positionOS += vertexDelta * weights[i];
                        accumulatedNormalDelta += normalDelta * weights[i];
                    }
                }
                
                // Apply normal delta with preservation
                normalOS += accumulatedNormalDelta;
                
                // Ensure normal remains unit length
                normalOS = normalize(normalOS);
                
                // Recalculate tangent if needed
                if (length(tangentOS) > 0.001)
                {
                    tangentOS = normalize(tangentOS - dot(tangentOS, normalOS) * normalOS);
                }
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v, uint id : SV_VertexID)
            {
                float3 pos = v.vertex.xyz;
                float3 norm = v.normal;
                float3 tan = v.tangent.xyz;
                ApplyBlendShapesWithNormalPreservation(pos, norm, tan, id);
                v2f o;
                o.pos = UnityObjectToClipPos(float4(pos, 1));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(1,1,1,1);
            }
            ENDCG
        }
    }
}
