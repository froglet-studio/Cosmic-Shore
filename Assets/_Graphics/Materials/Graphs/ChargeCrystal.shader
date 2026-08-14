// The charge crystal: a STATIC faceted solid (the model's own 60 pentagonal extruded
// prisms, undisplaced) with a plasma discharge crackling vertex-to-vertex along the
// crease edges between faces. See ChargeCrystal.hlsl for the edge-data contract and
// CrystalEdgeArcMeshBaker.cs for who bakes it.
//
// Opaque on purpose. The material this replaced was the generic transparent
// CrystalGraph, whose vertex spread + Cosine-Time spin + stacked overlay blends are
// what blew the crystal out.
//
// _DullCrystalColor / _BrightCrystalColor keep their names because Crystal.cs
// (FindColorPropertyNames -> ApplyColorSetTint) drives them per-renderer from the theme
// ColorSet through a MaterialPropertyBlock: domain colours when a domain owns the
// crystal, blue-white while it is a lifeform's heart, lime CTA while it is a free pickup.

Shader "Shader Graphs/ChargeCrystal"
{
    Properties
    {
        [Header(Crystal Body)]
        [HDR] _DullCrystalColor   ("Dull Crystal Color", Color)   = (0.18, 0.32, 0.05, 1)
        [HDR] _BrightCrystalColor ("Bright Crystal Color", Color) = (0.59, 0.92, 0.16, 1)
        _FacetAmbient      ("Facet Ambient", Range(0, 1))       = 0.22
        _RimPower          ("Rim Power", Range(0.5, 8))         = 3.5
        _RimStrength       ("Rim Strength", Range(0, 2))        = 0.5
        _EmissionStrength  ("Emission Strength", Range(0, 2))   = 0.25
        _KeyDir            ("Key Direction (World)", Vector)    = (0.4, 0.8, -0.45, 0)
        _opacity           ("Opacity (driven by FadeIn)", Range(0, 1)) = 1

        [Header(Edge Discharge)]
        [HDR] _ArcCoreColor ("Arc Core Color", Color)           = (0.85, 0.95, 1.0, 1)
        _ArcIntensity      ("Arc Intensity", Range(0, 8))       = 4.0
        _ArcWidth          ("Arc Width (model radii)", Range(0.001, 0.08)) = 0.005
        _ArcLength         ("Arc Length (edge fraction)", Range(0.02, 1))  = 0.14
        _ArcSpeed          ("Arc Speed (passes per sec)", Range(0.05, 6))  = 0.85
        _ArcDuty           ("Arc Silence (0 = always fire)", Range(0, 0.95)) = 0.35
        _ArcJitter         ("Arc Jitter (widths)", Range(0, 6)) = 3.0
        _ArcShimmer        ("Idle Shimmer", Range(0, 0.5))      = 0.03

        [Header(Vertex Terminals)]
        _NodeRadius        ("Node Radius (model radii)", Range(0.002, 0.1)) = 0.011
        _NodeIntensity     ("Node Intensity", Range(0, 8))      = 3.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "Queue"           = "Geometry"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _DullCrystalColor;
            float4 _BrightCrystalColor;
            float  _FacetAmbient;
            float  _RimPower;
            float  _RimStrength;
            float  _EmissionStrength;
            float4 _KeyDir;
            float  _opacity;
            float4 _ArcCoreColor;
            float  _ArcIntensity;
            float  _ArcWidth;
            float  _ArcLength;
            float  _ArcSpeed;
            float  _ArcDuty;
            float  _ArcJitter;
            float  _ArcShimmer;
            float  _NodeRadius;
            float  _NodeIntensity;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ChargeCrystalForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Assets/_Graphics/Materials/Graphs/ChargeCrystal.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float3 bary       : TEXCOORD1;
                float3 edgeH      : TEXCOORD2;
                float3 edgeSeed   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float3 bary       : TEXCOORD2;
                float3 edgeH      : TEXCOORD3;
                float3 edgeSeed   : TEXCOORD4;
                float  fogFactor  : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // No vertex displacement: the spread is the model's.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS  = GetWorldSpaceViewDir(positionWS);
                output.bary       = input.bary;
                output.edgeH      = input.edgeH;
                output.edgeSeed   = input.edgeSeed;
                output.fogFactor  = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Emergence coverage: FadeIn.cs blooms the crystal in via _opacity.
                clip(ChargeCoverageClip(_opacity, input.positionCS.xy));

                float3 normalWS = input.normalWS;

                float3 color;
                ChargeCrystalSurface(
                    normalWS,
                    input.viewDirWS,
                    input.bary,
                    input.edgeH,
                    input.edgeSeed,
                    _Time.y,
                    _DullCrystalColor,
                    _BrightCrystalColor,
                    _KeyDir.xyz,
                    _FacetAmbient,
                    _RimPower,
                    _RimStrength,
                    _EmissionStrength,
                    _ArcCoreColor.rgb,
                    _ArcIntensity,
                    _ArcWidth,
                    _ArcLength,
                    _ArcSpeed,
                    _ArcDuty,
                    _ArcJitter,
                    _ArcShimmer,
                    _NodeRadius,
                    _NodeIntensity,
                    color);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
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
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Assets/_Graphics/Materials/Graphs/ChargeCrystal.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 depthFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(ChargeCoverageClip(_opacity, input.positionCS.xy));
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
