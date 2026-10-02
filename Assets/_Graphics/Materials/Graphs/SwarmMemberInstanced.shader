// SwarmMemberInstanced.shader - every LIVING swarm member, drawn from one GPU buffer (Docs/SWARM_FAUNA.md §14).
//
// A swarm of ~1,000 tadpoles is data in a simulation that runs off the main thread. Once per simulation
// TICK the swarm uploads one SwarmInstance per member slot (SwarmTickJob.cs, 80 bytes: previous and
// current pose, prism shape, heart molt, tier, birth tick); every FRAME it sets a handful of per-swarm
// values (the display alpha between the two poses, the clock) and issues ~5 draws. So the CPU cost per
// member per frame is ZERO: the vertex shader interpolates the pose, builds the basis, blooms a newborn
// and re-forms a molting heart from the per-instance data alone - the clock-material law's spirit
// (Docs/PRISM_ANIMATION.md): initial conditions in, the GPU runs the course.
//
// Two PARTS, one shader (_SwarmPart): the BODY prism (one mesh, every live instance) and the HEART
// (one draw per crystal model per element over that element's index list, _SwarmHeartIdx; a molting
// heart is listed under both elements and shown by the one the molt has reached).
//
// It honours the prism occlusion corridor (a PLATFORM LAW, Docs/PRISM_ANIMATION.md §4.7): the body and
// the heart go see-through between the camera and the local ship exactly like prism mass does, through
// the same PrismOcclusionFade_float the prism graphs call. A dead slot is collapsed to a point (no
// fragments). Opaque, depth-writing, no shadows.
Shader "CosmicShore/SwarmMemberInstanced"
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
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PrismOcclusionCorridor.hlsl"
            #include "SwarmMemberInstanced.hlsl"

            // the corridor's globals (PrismOcclusionCorridor.cs publishes them; the prism graphs declare
            // them as graph properties, this hand-written shader declares them itself)
            float4 _PrismOcclusionTarget;
            float4 _PrismOcclusionParams;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                uint instanceID : SV_InstanceID;
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
                Varyings o;
                SwarmMemberVertex v = SwarmMemberPose(IN.instanceID, IN.positionOS, IN.normalOS, IN.tangentOS.xyz);
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
