Shader "Custom/CircularGradientFresnel"
{
    Properties
    {
        [HDR]_BrightColor ("Bright Color", Color) = (1,1,1,1)
        [HDR]_DarkColor ("Dark Color", Color) = (0,0,0,1)
        _Spread ("Spread", Vector) = (1,1,1,0)
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 5
        _Radius ("Radius", Float) = 0.5 // Controls the size of the circular transparent region
        _Fade ("Fade", Float) = 0.1 // Controls the softness of the edge of the circle
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
            Blend SrcAlpha OneMinusSrcAlpha

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
                float4 screenPos : TEXCOORD2;
            };

            float3 _Spread;
            float _FresnelPower;
            float4 _BrightColor, _DarkColor;
            float _Radius;
            float _Fade;
            float _Aspect;

            v2f vert (appdata v)
            {
                v2f o;
                float3 objectScale = float3(length(unity_ObjectToWorld._m00_m01_m02), length(unity_ObjectToWorld._m10_m11_m12), length(unity_ObjectToWorld._m20_m21_m22));
                float3 spreadedNormal = v.normal * (_Spread / objectScale);
                v.vertex.xyz += spreadedNormal;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = normalize(mul((float3x3)UNITY_MATRIX_M, v.normal));
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = pow((1.0 - dot(viewDir, i.worldNormal)), _FresnelPower);
                half4 col = lerp(_BrightColor, _DarkColor, fresnel);

                // Calculate normalized screen position and adjust for circle center
                float2 centerScreenPos = i.screenPos.xy / i.screenPos.w;
                centerScreenPos = centerScreenPos * 2 - 1; // Adjust from [0,1] to [-1,1] range for both axes

                // Correct for aspect ratio to ensure circular shape
                float aspectRatio = _ScreenParams.x / _ScreenParams.y; // Width divided by Height
                centerScreenPos.y /= aspectRatio;

                // Calculate distance from the center of the screen
                float dist = length(centerScreenPos);

                // Calculate transparency based on distance from the center
                float alpha = smoothstep(_Radius, _Radius + _Fade, dist);

                col.a *= alpha; // Apply calculated transparency

                return col;
            }


            ENDCG
        }
    }
}
