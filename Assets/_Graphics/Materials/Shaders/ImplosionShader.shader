Shader "Custom/ImplosionShader"
{
    Properties
    {
        [HDR]_BrightColor ("Bright Color", Color) = (1,1,1,1)
        [HDR]_DarkColor ("Dark Color", Color) = (0,0,0,1)
        _SinkLocation ("Sink Location", Vector) = (0,0,0,0)
        _ImplosionAmount ("Implosion Amount", Range(0,1)) = 0
        _RotationAmount ("Rotation Amount", Float) = 0
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

            float3 _SinkLocation;
            float _ImplosionAmount;
            float _RotationAmount;
            float4 _BrightColor, _DarkColor;

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = normalize(mul((float3x3)UNITY_MATRIX_M, v.normal));
                float3 dir = normalize(_SinkLocation - worldPos);
                float3 crossVec = cross(worldNormal, dir);
                worldPos += dir * (-_ImplosionAmount);
                worldPos += crossVec * _RotationAmount;
                v.vertex = mul(unity_WorldToObject, float4(worldPos,1));
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = worldNormal;
                o.worldPos = worldPos;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = (1.0 + dot(viewDir, i.worldNormal))/2;
                half4 col = lerp(_BrightColor, _DarkColor, fresnel);
                return col;
            }
            ENDCG
        }
    }
}
