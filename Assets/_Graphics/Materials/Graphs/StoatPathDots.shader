// StoatPathDots.shader — the Stoat pathfinder's dots in the scene (R_VesselActions/STOAT_DIPOLE.md §3b), and Time
// mode's detection / orbit rings (§1b). StoatPathfinderWorldDots.cs builds ONE mesh a frame: a camera-facing quad per
// dot, its colour (and depth fade) in the vertex colour, uv 0..1 across the quad. This file only draws a round dot with
// a dark rim from that uv, so a dot never reads as a star.
//
// WHY ITS OWN SHADER (2026-10-14). The first cut drew the dots INSTANCED on the built-in Sprites/Default. That shader's
// instanced variant reads the sprite renderer's per-instance flip and colour (unity_SpriteFlipArray /
// unity_SpriteRendererColorArray), which no Graphics.RenderMeshInstanced call fills: both read zero, every quad
// collapsed to a point, and the path vanished ("I no longer see the foresight"). Nothing here reads per-draw sprite
// data. The mesh is drawn as ONE instance with an identity matrix (Graphics.RenderMeshInstanced - the call Amoebius's
// shim also has), so the only per-instance datum is that matrix, which the call fills; the instancing macros below are
// the standard URP ones. Shipped through Resources/StoatPathDots.mat (instancing enabled), so a build always carries it.
Shader "CosmicShore/StoatPathDots"
{
    Properties { }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "StoatPathDots"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex StoatPathDotsVert
            #pragma fragment StoatPathDotsFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings StoatPathDotsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = (half4)input.color;
                output.uv = input.uv;
                return output;
            }

            half4 StoatPathDotsFrag(Varyings input) : SV_Target
            {
                // 0 at the centre, 1 at the quad's inscribed circle.
                float d = length(input.uv * 2.0 - 1.0);
                half alpha = (half)saturate((1.0 - d) * 8.0) * input.color.a;
                // The dark rim: the outer 30% of the radius at 7% of the colour (the old disc texture's 18/255).
                half rim = (half)smoothstep(0.66, 0.74, d);
                half3 rgb = input.color.rgb * lerp((half)1.0, (half)0.07, rim);
                return half4(rgb * alpha, alpha);   // pre-multiplied (Blend One OneMinusSrcAlpha)
            }
            ENDHLSL
        }
    }
    Fallback Off
}
