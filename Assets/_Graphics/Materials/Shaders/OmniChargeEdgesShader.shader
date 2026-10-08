// The charge crystal's edge discharge, drawn on the omni crystal's 12 PENTAGONAL prisms - the
// shapes that stand for Charge on the omni (Docs/PALETTE.md §2.10: an element's effect belongs on
// that element's shapes). Mass's Shepard tone rides the 20 triangles the same way.
//
// This pass draws ONLY the discharge (ChargeCrystal.hlsl's ChargeCrystalDischarge, the exact term
// the charge crystal adds to its own body), additively, over the omni body that is already there.
// Its mesh is the omni model filtered to its 10-corner plates and edge-baked by
// CrystalEdgeArcMeshBaker (CrystalEdgeArcs, plateCorners 10), so a bolt can only ever run along a
// pentagonal prism's crease edges. An unbaked mesh contributes exactly zero, so a failed bake
// leaves the omni as it was rather than drawing anything wrong.
//
// It sits on the body's own surface, so it reproduces the body's vertex path: OmniCrystalFresnelShader
// pushes every vertex out along its normal by _Spread WORLD units, and this does the same with the
// same value (the generator copies it from OmniCrystalBody), then wins the depth tie with Offset.
//
// _BrightColor is the bolt halo. Its authored value is the body's lime; CrystalAccentTint repaints
// it from the body's material whenever the crystal changes domain, so a team crystal's bolts wear
// its domain colour exactly as the charge crystal's do. The core stays near-white.
Shader "Custom/OmniChargeEdgesShader"
{
    Properties
    {
        [Header(Halo)]
        [HDR] _BrightColor ("Bolt Halo Color (painted from the body)", Color) = (0.4862745, 0.9882354, 0, 1)
        _Spread            ("Spread (match the body)", Vector) = (0.01, 0.01, 0.01, 0)

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
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+2"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OmniChargeEdges"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            // The body draws these very triangles (same mesh, same push): bias toward the camera so
            // the overlay wins the tie instead of z-fighting it.
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BrightColor;
                float4 _Spread;
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
                float3 bary       : TEXCOORD0;
                float3 edgeH      : TEXCOORD1;
                float3 edgeSeed   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // OmniCrystalFresnelShader's SpreadFresnelDisplace, transcribed: the push is in WORLD units,
            // so it is divided by the object's own scale (row lengths, exactly as the body reads them).
            float3 OmniChargeSpread(float3 positionOS, float3 normalOS)
            {
                float3 objectScale = float3(length(UNITY_MATRIX_M._m00_m01_m02),
                                            length(UNITY_MATRIX_M._m10_m11_m12),
                                            length(UNITY_MATRIX_M._m20_m21_m22));
                return positionOS + normalize(normalOS) * (_Spread.xyz / objectScale);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(OmniChargeSpread(input.positionOS.xyz, input.normalOS));
                output.bary       = input.bary;
                output.edgeH      = input.edgeH;
                output.edgeSeed   = input.edgeSeed;
                output.fogFactor  = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 color = ChargeCrystalDischarge(
                    input.bary, input.edgeH, input.edgeSeed, _Time.y,
                    _BrightColor.rgb, _ArcCoreColor.rgb, _ArcIntensity,
                    _ArcWidth, _ArcLength, _ArcSpeed, _ArcDuty, _ArcJitter, _ArcShimmer,
                    _NodeRadius, _NodeIntensity);

                // Additive: fog fades the light toward nothing, not toward the fog colour.
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
