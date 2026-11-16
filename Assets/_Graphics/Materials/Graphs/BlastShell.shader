// BlastShell.shader — the shock front of the blast look (Assets/_Scripts/Controller/Projectiles/BlastLook/BLAST_LOOK.md),
// and the Grizzly trigger bomb's ping and in-pull (GrizzlyBombVisual).
//
// A FACETED BUBBLE, NOT A RING ON THE SCREEN. It is drawn on a closed icosphere (BlastLookMeshes.Shell), and each
// fragment takes its facet's normal from the derivatives of its world position, so the front is a sphere of flat
// facets: the facets the camera sees edge-on (the silhouette) carry the light, the square-on ones almost none. From
// outside it reads as a lit sphere with a faceted rim, the way a shock front reads on film; from INSIDE it (the chase
// camera of a Grizzly riding its own blast) every facet is near square-on and it all but vanishes, so it never fogs the
// pilot's view.
//
// Premultiplied alpha (One, OneMinusSrcAlpha), with a small coverage so the front darkens nothing behind it: the colour
// is the light. Cull Off, ZWrite Off: both halves of the bubble draw.
Shader "CosmicShore/BlastShell"
{
    Properties
    {
        [HDR] _BrightColor ("Bright (the front's colour once it cools)", Color) = (1, 1, 1, 1)
        [HDR] _HotColor ("Hot (the front's colour at the release)", Color) = (1, 1, 1, 1)
        _Heat ("Heat, 0 cooled to 1 white-hot", Range(0, 1)) = 0
        _Opacity ("Opacity (the front's life)", Range(0, 1)) = 1
        _Intensity ("HDR intensity of the rim", Float) = 3
        _RimPower ("Rim width (power on 1 - facing; higher is a thinner rim)", Float) = 2.5
        _FaceFill ("Light on a square-on facet (0 none)", Range(0, 1)) = 0.04
        _Coverage ("How much the front darkens what is behind it", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BlastShell"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShellVert
            #pragma fragment ShellFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BrightColor;
                half4 _HotColor;
                float _Heat;
                float _Opacity;
                float _Intensity;
                float _RimPower;
                float _FaceFill;
                float _Coverage;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShellVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 ShellFrag(Varyings input) : SV_Target
            {
                float3 n = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                float3 v = normalize(GetCameraPositionWS() - input.positionWS);
                float facing = saturate(abs(dot(n, v)));
                float rim = pow(1.0 - facing, max(0.01, _RimPower));
                float a = saturate(rim + _FaceFill) * saturate(_Opacity);
                float h = saturate(_Heat);
                half3 colour = lerp(_BrightColor.rgb, _HotColor.rgb, h * h) * _Intensity * (0.35 + 0.65 * rim);
                return half4(colour * a, a * saturate(_Coverage));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
