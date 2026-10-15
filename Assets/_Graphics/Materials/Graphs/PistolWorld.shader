// PistolWorld.shader — the Pistol shrimp's side-on world (the Goby & Pistol duo; GobyPistolDuo builds ONE mesh a
// frame: every prism in view, WARPED through the unroll of the Goby's path, the trail it rides, the Pistol, the rings,
// the flares, the aim). Nothing in the scene draws this way, so the mesh carries everything the shader needs:
//   vertex colour  rgb = the prism's domain colour (or the actor's), a = opacity (the depth fade)
//   uv0.x          0 = lit flat facets, 1 = pure glow (the Pistol's eye, the rings, the aim)
//   uv0.y          the "reach" of a prism: 1 inside the band the Pistol's body spans, 0 beyond it (a rim lights up in reach)
//   uv1.x          fog: 0 at the Pistol's plane, 1 at the far depth (the colour sinks toward _FogColor)
// The facets are lit from screen-space derivatives (flat shading) in VIEW space, so a warped, stretched prism still
// reads as a solid with faces whatever the deformation did to its normals, and the light does not turn with the frame
// the view is drawn in. Two-sided: the warp can flip a winding. One shader, two materials: Resources/PistolWorld.mat
// (the SOLID pass: depth written, opaque) and Resources/PistolWorldGlass.mat (the GLASS pass: blended, no depth write,
// drawn in the far-to-near order the mesh emitted it). Each is drawn as ONE instance of the mesh with the frame's
// matrix (Graphics.RenderMeshInstanced, the call Amoebius's shim also has; vessel skill rules 50 and 51).
Shader "CosmicShore/PistolWorld"
{
    Properties
    {
        _FogColor ("Fog (the far depth)", Color) = (0.05, 0.06, 0.12, 1)
        _KeyDir ("Key light direction (view space)", Vector) = (-0.45, 0.8, -0.4, 0)
        _Ambient ("Ambient", Range(0, 1)) = 0.42
        _Rim ("Reach rim", Range(0, 2)) = 0.9
        [Toggle] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PistolWorld"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PistolWorldVert
            #pragma fragment PistolWorldFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _FogColor;
            float4 _KeyDir;
            float _Ambient;
            float _Rim;
            float _ZWrite;
            float _SrcBlend;
            float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionVS : TEXCOORD2;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings PistolWorldVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionVS = TransformWorldToView(positionWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = (half4)input.color;
                output.uv = input.uv;
                output.uv1 = input.uv1;
                return output;
            }

            half4 PistolWorldFrag(Varyings input) : SV_Target
            {
                // flat facets from the surface's own derivatives, in view space: right for any warp, no vertex normals
                // needed, and the key light stays where the viewer is whatever way the frame turns
                float3 dpx = ddx(input.positionVS), dpy = ddy(input.positionVS);
                float3 n = cross(dpy, dpx);
                float len = length(n);
                float3 nv = len > 1e-6 ? n / len : float3(0.0, 0.0, 1.0);
                float3 key = normalize(_KeyDir.xyz);
                float ndl = abs(dot(nv, key));                      // two-sided: the warp can turn a face over
                float lit = _Ambient + (1.0 - _Ambient) * ndl;
                float glow = saturate(input.uv.x), reach = saturate(input.uv.y);
                // a face turned away from the viewer catches a rim in the prism's own colour while it is in reach
                float facing = abs(nv.z);
                float rim = _Rim * reach * pow(1.0 - facing, 2.0);
                half3 albedo = input.color.rgb;
                half3 rgb = lerp(albedo * lit + albedo * rim, albedo, glow);
                float fog = saturate(input.uv1.x) * (1.0 - glow);
                rgb = lerp(rgb, (half3)_FogColor.rgb, fog);
                return half4(rgb, input.color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
