// ThresherBallFresnelShader.shader — the Thresher's wrecking ball (and its studs).
//
// THE LOOK. The game's own fresnel convention (Docs/PALETTE.md §2, SpreadFresnelShader, every
// crystal graph): a DULL body and a BRIGHT rim, composed as a straight
//     lerp(_DarkColor, _BrightColor, (1 - N·V) ^ _FresnelPower)
// so the ball reads as a solid, lit object against the HyperSea rather than a flat disc. The
// executor drives both colours per frame through one MaterialPropertyBlock (domain colour cool,
// palette danger red at smash speed, the lit shielded rim with Charge L5 'Lit').
//
// WHY POWER 1.5, NOT THE CRYSTALS' 4. Area-weighted over a sphere's disc the mean rim weight is
// 2 / ((p+1)(p+2)): 0.067 at p = 4 (a hairline — right for a crystal tens of units across), 0.229
// at p = 1.5, where the rim passes half weight over the outer ~14% of the disc. The ball is a few
// units across and often 100+ units from the lens, so a hairline would be sub-pixel. The six cube
// studs are flat, so each face takes one fresnel value — edge-on faces flare, facing ones go dull —
// which is what makes the ball's ROLL readable on a body that is otherwise rotationally symmetric.
//
// THE PALETTE RULES IT RESPECTS. Tonemapping is None (channels above 1.0 clip hard and shift hue)
// and gameplay bloom is clamped at a 0.5 source (brightness above it is a dead dial), so the
// executor normalises each colour by its max channel: the body sits under the clamp until the ball
// is hot (then ON it — the whole ball blooms), and the rim stays at or below 1.0.
//
// RENDER STATE. Opaque, ZWrite On, Cull Back: only front faces are ever seen, so the ramp spans
// dark centre -> bright silhouette exactly as on an opaque prism (the see-through-shell trap in
// .claude/skills/asset-surgery/SKILL.md does not apply). Unlit; a DepthOnly pass so the ball is in
// the depth texture like any opaque. SRP-batcher compatible (UnityPerMaterial), instancing-ready.
//
// COST. ~12 ALU in the fragment, no texture fetch, seven renderers per Thresher.

Shader "CosmicShore/ThresherBallFresnel"
{
    Properties
    {
        [HDR] _DarkColor ("Body Colour", Color) = (0.03, 0.2, 0.19, 1)
        [HDR] _BrightColor ("Rim Colour", Color) = (0.3, 0.95, 0.9, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector"= "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _DarkColor;
            float4 _BrightColor;
            float  _FresnelPower;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ThresherBall"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(_WorldSpaceCameraPos - input.positionWS);
                float rim = pow(1.0 - saturate(dot(n, v)), max(_FresnelPower, 0.01));
                return half4(lerp(_DarkColor.rgb, _BrightColor.rgb, rim), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings depthVert (DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half depthFrag (DepthVaryings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
