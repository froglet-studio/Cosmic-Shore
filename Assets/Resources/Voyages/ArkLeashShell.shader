// The Ark's leash shell (ArkLeashShell.cs): a ghostly lattice on a sphere at the leash radius. Additive, both faces,
// no depth write. Bright only where the pilot is about to cross (a reveal patch around _RevealPos), with a fresnel rim
// at grazing angles, a slowly drifting three-way lattice and a shimmer. _Intensity fades the whole shell by proximity.
Shader "CosmicShore/ArkLeashShell"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.45, 0.85, 1.25, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0
        _RevealPos ("Reveal Position (world)", Vector) = (0, 0, 0, 0)
        _RevealRadius ("Reveal Radius", Float) = 600
        _GridScale ("Lattice Cells Across A Radius", Float) = 60
        _Pulse ("Pulse (breached)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float4 _RevealPos;
                float _RevealRadius;
                float _GridScale;
                half _Pulse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 dirOS : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.dirOS = normalize(i.positionOS.xyz);
                return o;
            }

            float Line(float x)
            {
                float d = abs(frac(x) - 0.5);
                float w = max(fwidth(x) * 1.2, 0.015);
                return 1.0 - smoothstep(0.0, w, d - 0.008);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);
                float ndv = abs(dot(normalize(i.normalWS), view));
                float fresnel = pow(1.0 - ndv, 3.0);

                float d = distance(i.positionWS, _RevealPos.xyz);
                float reveal = saturate(1.0 - d / max(1.0, _RevealRadius));
                reveal *= reveal;

                float t = _Time.y;
                float3 p = i.dirOS * _GridScale;
                // three line families 120 degrees apart, drifting slowly: a lattice that reads as a membrane
                float a = Line(dot(p, float3(1.0, 0.25, 0.0)) + t * 0.06);
                float b = Line(dot(p, float3(-0.5, 0.25, 0.866)) - t * 0.05);
                float c = Line(dot(p, float3(-0.5, 0.25, -0.866)) + t * 0.04);
                float lattice = saturate(a + b + c);
                float shimmer = 0.55 + 0.45 * sin(dot(p, float3(1.7, 2.3, 1.1)) * 0.9 + t * 1.7);
                float pulse = 1.0 + _Pulse * (0.35 + 0.35 * sin(t * 6.2831));

                float alpha = _Intensity * pulse * (reveal * (0.05 + 0.6 * lattice * shimmer) + fresnel * 0.3 * saturate(reveal * 2.5));
                return half4(_Color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
