// MeerkatWormholeWarp.shader — a Meerkat tunnel's wall drawn as a screen-space OFFSET (the Vessel Studio's twMat,
// Docs/Studios/VesselStudio/meerkat.html): the wall's view-space normal, zero at the tube's centre line AND at its
// silhouette (sin(pi * rim)), so it reads as a smooth glass tube with no seam, bending in toward the way in and out toward
// the way out (uv.x, cos of the way along × the tunnel's fade); uv.y is 0.05 × warpStrength. MeerkatWormholeLensPass.cs
// draws MeerkatWormholeView.WarpMesh with it, additively, into a half-float target the lens adds to its bend.
Shader "CosmicShore/MeerkatWormholeWarp"
{
    Properties { }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "MeerkatWormholeWarp"
            Tags { "LightMode" = "MeerkatWormholeWarp" }
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MeerkatWarpVert
            #pragma fragment MeerkatWarpFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalVS : TEXCOORD0;
                float3 viewVS : TEXCOORD1;
                float2 sk : TEXCOORD2;
            };

            Varyings MeerkatWarpVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS), true);
                output.viewVS = -positionVS;
                output.sk = input.uv;
                return output;
            }

            float4 MeerkatWarpFrag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(input.normalVS);
                if (!frontFace) n = -n;
                float rim = 1.0 - abs(dot(n, normalize(input.viewVS)));
                return float4(n.xy * sin(3.14159 * rim) * input.sk.y * input.sk.x, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
