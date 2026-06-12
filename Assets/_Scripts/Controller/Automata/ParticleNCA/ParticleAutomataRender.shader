// ParticleAutomataRender.shader — URP unlit billboard renderer for the
// Particle NCA. Vertex stage: each instance is one automaton particle;
// six vertices expand into a camera-facing quad. Fragment stage: soft
// circular falloff, additive blend. Color lerps accent -> base by the
// vitality channel (state0.x) so regrowing regions are visible.

Shader "CosmicShore/Automata/ParticleAutomata"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.23, 0.84, 0.75, 0.9)
        _AccentColor ("Accent Color", Color) = (1.0, 0.35, 0.65, 0.9)
        _ParticleSize ("Particle Size", Float) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ParticleAutomataForward"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct AutomataParticle
            {
                float3 position;
                float4 state0;
                float4 state1;
            };

            StructuredBuffer<AutomataParticle> _Particles;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _AccentColor;
                float _ParticleSize;
                float4x4 _LocalToWorld;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
            };

            static const float2 QuadCorners[6] =
            {
                float2(-0.5, -0.5), float2(0.5, -0.5), float2(0.5, 0.5),
                float2(-0.5, -0.5), float2(0.5, 0.5), float2(-0.5, 0.5)
            };

            Varyings Vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                AutomataParticle particle = _Particles[instanceId];

                float3 worldCenter = mul(_LocalToWorld, float4(particle.position, 1.0)).xyz;

                // Camera-facing billboard axes from the view matrix rows.
                float3 camRight = UNITY_MATRIX_V[0].xyz;
                float3 camUp = UNITY_MATRIX_V[1].xyz;

                float2 corner = QuadCorners[vertexId];
                float3 worldPos = worldCenter
                    + camRight * corner.x * _ParticleSize
                    + camUp * corner.y * _ParticleSize;

                float vitality = saturate(particle.state0.x);

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = corner * 2.0;
                output.color = lerp(_AccentColor, _BaseColor, vitality);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r2 = dot(input.uv, input.uv);
                float falloff = saturate(1.0 - r2);
                falloff *= falloff;
                return half4(input.color.rgb, input.color.a * falloff);
            }
            ENDHLSL
        }
    }
}
