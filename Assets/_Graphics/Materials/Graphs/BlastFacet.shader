// BlastFacet.shader — the solid half of the blast look (Assets/_Scripts/Controller/Projectiles/BlastLook/BLAST_LOOK.md):
// the white-hot core, the casing shards and the embers a blast throws, and the Grizzly trigger bomb's own body and spikes.
//
// THE FLEET'S TWO-TONE, NOT A FILL. Each fragment takes its normal from the screen-space derivatives of its world
// position, so every triangle is one flat facet whatever the mesh's normals (the meshes carry radial normals for the
// _Spread push). A facet square-on to the camera wears the bright signal, an edge-on facet the dark body (the switch
// ring's measured rule, Docs/SPINDLE_LOD.md: a dark body at a fraction of the signal and the facets across the whole
// range), so a shard reads as a volume at any distance and a cooled shard is still a faceted solid, never a sprite.
//
// HEAT. 0 is the two-tone; 1 is white-hot: every facet glows _HotColor x _HeatIntensity (HDR, so the camera's bloom
// picks it up), the square-on facets a little brighter than the edge-on ones so the facets still read while hot. Heat
// is _Heat (a property block: the core, the bomb) or the mesh's uv0.x (_HeatFromVertex 1: the shards, which share one
// mesh per blast and cool one by one).
//
// Opaque, so no sorting and no additive sheet: a shard fades by SHRINKING (BlastLookSystem), never by alpha. Cull Off,
// because the facet shading is the same from either side and the bomb's spikes are open cones.
Shader "CosmicShore/BlastFacet"
{
    Properties
    {
        [HDR] _BrightColor ("Bright (a square-on facet: the signal)", Color) = (1, 1, 1, 1)
        [HDR] _DarkColor ("Dark (an edge-on facet: the body)", Color) = (0, 0, 0, 1)
        [HDR] _HotColor ("Hot (what a white-hot facet glows)", Color) = (1, 1, 1, 1)
        _Heat ("Heat, 0 cold to 1 white-hot (uniform)", Range(0, 1)) = 0
        _HeatFromVertex ("Heat from the mesh's uv0.x instead of Heat (1: shards)", Range(0, 1)) = 0
        _HeatIntensity ("HDR glow of a white-hot facet", Float) = 6
        _FacetPower ("Facet contrast (power on facing)", Float) = 1.5
        _Spread ("Push along the mesh normal, world units", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BrightColor;
            half4 _DarkColor;
            half4 _HotColor;
            float _Heat;
            float _HeatFromVertex;
            float _HeatIntensity;
            float _FacetPower;
            float _Spread;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float heat : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings FacetVert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            // The push is in world units along the (radial) mesh normal - what Custom/SpreadFresnelShader did, so a
            // bomb that changes shader keeps its shipped visible size.
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            float3 positionWS = TransformObjectToWorld(input.positionOS) + normalWS * _Spread;
            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.heat = lerp(_Heat, input.uv.x, saturate(_HeatFromVertex));
            return output;
        }

        // The facet's own normal: the derivatives of the world position across the triangle.
        float FacetFacing(float3 positionWS)
        {
            float3 n = normalize(cross(ddy(positionWS), ddx(positionWS)));
            float3 v = normalize(GetCameraPositionWS() - positionWS);
            return saturate(abs(dot(n, v)));
        }
        ENDHLSL

        Pass
        {
            Name "BlastFacet"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FacetVert
            #pragma fragment FacetFrag
            #pragma multi_compile_instancing

            half4 FacetFrag(Varyings input) : SV_Target
            {
                float lit = pow(FacetFacing(input.positionWS), max(0.01, _FacetPower));
                half3 body = lerp(_DarkColor.rgb, _BrightColor.rgb, lit);
                float h = saturate(input.heat);
                half3 hot = _HotColor.rgb * _HeatIntensity * (0.55 + 0.45 * lit);
                half3 colour = lerp(body, hot, h * h * (3.0 - 2.0 * h));
                return half4(colour, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FacetVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            half DepthFrag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
