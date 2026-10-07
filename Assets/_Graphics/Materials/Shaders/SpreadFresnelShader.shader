Shader "Custom/SpreadFresnelShader"
{
    Properties
    {
        [HDR]_BrightColor ("Bright Color", Color) = (1,1,1,1)
        [HDR]_DarkColor ("Dark Color", Color) = (0,0,0,1)
        _Spread ("Spread", Vector) = (1,1,1,0)
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 5
    }

    SubShader
    {
            Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Unlit"
            "Queue"="Geometry"
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="UniversalUnlitSubTarget"
        }
        Pass
        {
            Name "CustomPass"
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            // The shared core (properties, displacement, colour) lives in SpreadFresnelCore.hlsl so
            // OmniCrystalFresnelShader can extend it without forking it.
            #include "SpreadFresnelCore.hlsl"

            v2f vert (appdata v)
            {
                v2f o;
                v.vertex.xyz = SpreadFresnelDisplace(v.vertex.xyz, v.normal);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = normalize(mul((float3x3)UNITY_MATRIX_M, v.normal));

                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return SpreadFresnelColor(i.worldPos, i.worldNormal);
            }
            ENDCG
        }
    }
}
