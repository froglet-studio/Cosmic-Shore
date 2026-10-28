// MeerkatWormholeGlass.shader — the glass surface of a Meerkat's reticle and of its tunnels' bridge (the Vessel Studio's
// whFill, Docs/Studios/VesselStudio/meerkat.html): lit at its silhouette (Fresnel), so the tube has a body. The vertex colour
// carries the way-in to way-out shade (rgb) and the brightness × glass (a); added to the scene.
Shader "CosmicShore/MeerkatWormholeGlass"
{
    Properties { }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+4" "IgnoreProjector" = "True" }

        Pass
        {
            Name "MeerkatWormholeGlass"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex MeerkatGlassVert
            #pragma fragment MeerkatGlassFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings MeerkatGlassVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.color = (half4)input.color;
                return output;
            }

            half4 MeerkatGlassFrag(Varyings input) : SV_Target
            {
                float rim = 1.0 - abs(dot(normalize(input.normalWS), normalize(input.viewWS)));
                float k = 0.05 + 0.55 * rim * rim * rim;
                return half4(input.color.rgb * input.color.a * k, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
