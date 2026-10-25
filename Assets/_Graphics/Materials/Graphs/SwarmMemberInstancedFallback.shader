// SwarmMemberInstancedFallback.shader - every LIVING swarm / substrate / builder member, on a device WITHOUT
// vertex-stage structured buffers: WebGL2, OpenGL ES 3.0, anything below SM 4.5 (Docs/SWARM_FAUNA.md §14.8).
//
// The look is SwarmMemberInstanced.shader's, line for line: both include the same SwarmMemberInstanced.hlsl and run
// the same SwarmMemberPoseOf / SwarmMemberShade over one SwarmInstance. Only the FETCH differs. There the instance
// is read from a StructuredBuffer by SV_InstanceID; here it arrives as five float4 rows per instance in Unity's
// instancing constant buffer (MaterialPropertyBlock arrays on Graphics.RenderMeshInstanced, packed by
// SwarmMemberFallbackDraw.cs), and SwarmInstanceFromRows rebuilds it. Heart draws do not index a heart list on the
// GPU: the CPU gathers each element's members into that element's own rows once per tick.
//
// The matrices RenderMeshInstanced requires are identity and unread: the pose is entirely the instance data's,
// exactly as on the SM 4.5 path. The prism occlusion corridor (a PLATFORM LAW, Docs/PRISM_ANIMATION.md §4.7) is
// composed the same way. Opaque, depth-writing, no shadows. The material that carries this shader is
// Assets/Resources/SwarmMemberInstancedFallback.mat, with GPU instancing ON: that is what keeps the INSTANCING_ON
// variant in a build (Graphics settings strip instancing variants no material asks for).
Shader "CosmicShore/SwarmMemberInstancedFallback"
{
    Properties { }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "AlphaTest" }

        Pass
        {
            Name "SwarmMember"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PrismOcclusionCorridor.hlsl"
            #include "PrismDepthPlane.hlsl"
            // the CORE of the member HLSL only: this define skips its StructuredBuffer half (SM 4.5)
            #define SWARM_MEMBER_INSTANCED_INCLUDED
            #include "SwarmMemberInstanced.hlsl"

            // the corridor's globals (PrismOcclusionCorridor.cs publishes them; see SwarmMemberInstanced.shader)
            float4 _PrismOcclusionTarget;
            float4 _PrismOcclusionParams;

            // one SwarmInstance per instance, as five rows (SwarmInstanceFromRows says which field is where)
            UNITY_INSTANCING_BUFFER_START(SwarmMemberRows)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SwarmRow0)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SwarmRow1)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SwarmRow2)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SwarmRow3)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SwarmRow4)
            UNITY_INSTANCING_BUFFER_END(SwarmMemberRows)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                nointerpolation float4 dark : TEXCOORD2;
                nointerpolation float4 bright : TEXCOORD3;
            };

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings o;
                SwarmInstance s = SwarmInstanceFromRows(
                    UNITY_ACCESS_INSTANCED_PROP(SwarmMemberRows, _SwarmRow0),
                    UNITY_ACCESS_INSTANCED_PROP(SwarmMemberRows, _SwarmRow1),
                    UNITY_ACCESS_INSTANCED_PROP(SwarmMemberRows, _SwarmRow2),
                    UNITY_ACCESS_INSTANCED_PROP(SwarmMemberRows, _SwarmRow3),
                    UNITY_ACCESS_INSTANCED_PROP(SwarmMemberRows, _SwarmRow4));
                SwarmMemberVertex v = SwarmMemberPoseOf(s, IN.positionOS, IN.normalOS, IN.tangentOS.xyz);
                o.positionWS = v.positionWS;
                o.normalWS = v.normalWS;
                o.positionCS = v.visible ? TransformWorldToHClip(v.positionWS) : float4(0, 0, -2, 1);
                o.dark = v.dark;
                o.bright = v.bright;
                return o;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float alpha, clipThreshold;
                PrismOcclusionFade_float(IN.positionWS, _PrismOcclusionTarget.xyz, _PrismOcclusionParams.xyz,
                    1.0, alpha, clipThreshold);
                clip(alpha - clipThreshold);
                float3 rgb = SwarmMemberShade(IN.positionWS, IN.normalWS, IN.dark.rgb, IN.bright.rgb);
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
