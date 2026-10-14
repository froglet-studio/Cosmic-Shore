// ThresherChainFresnelShader.shader — the Thresher's chain, as a fresnel TUBE on a flat ribbon.
//
// THE LOOK. The same convention as the ball (ThresherBallFresnelShader, Docs/PALETTE.md §2): a dull
// body under a bright rim, lerp(_DarkColor, _BrightColor, (1 - N·V) ^ _FresnelPower). The chain is a
// LineRenderer — a camera-facing ribbon whose normals all face the lens, so a mesh fresnel would be
// flat. A view-aligned ribbon IS a cylinder's silhouette, though, and on a cylinder of radius 1 seen
// side-on the surface at signed distance x from the axis (in the plane of the screen) has
//     N·V = sqrt(1 - x²)
// exactly. uv.y runs 0..1 ACROSS a LineRenderer's width (any texture mode), so x = 2·uv.y − 1 and
// the fragment computes the tube's true fresnel analytically: one renderer, no mesh, no normals.
//
// It is drawn at the width that CUTS (2 × chainCutRadius), so what you see slicing is what slices.
// Power 0.8 (the ball's is 1.5): the chain is only a few pixels wide at chase distance, so the rim
// has to cover most of its width to read as a lit tube rather than a grey line with hairline edges.
//
// THE PALETTE RULES IT RESPECTS. As the ball: the executor normalises its colours by max channel,
// the body stays under the bloom threshold and the rim at or below 1.0 (tonemapping is None).
//
// RENDER STATE. Opaque, ZWrite On, Cull Off (a view-aligned ribbon flips winding when the chain
// passes over the lens's axis). Unlit; a DepthOnly pass. SRP-batcher compatible; the executor drives
// both colours through a MaterialPropertyBlock on the one LineRenderer.
//
// COST. ~10 ALU in the fragment, no texture fetch, one ribbon per Thresher.

Shader "CosmicShore/ThresherChainFresnel"
{
    Properties
    {
        [HDR] _DarkColor ("Body Colour", Color) = (0.07, 0.065, 0.065, 1)
        [HDR] _BrightColor ("Rim Colour", Color) = (0.3, 0.95, 0.9, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 0.8
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
            Name "ThresherChain"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float  across     : TEXCOORD0;   // -1 .. 1 across the ribbon
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.across = input.uv.y * 2.0 - 1.0;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float x = saturate(abs(input.across));
                float nDotV = sqrt(saturate(1.0 - x * x));
                float rim = pow(1.0 - nDotV, max(_FresnelPower, 0.01));
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 3.0

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings depthVert (DepthAttributes input)
            {
                DepthVaryings output;
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
