Shader "CosmicShore/BulkVoronoiMirror"
{
    Properties
    {
        _BaseColor ("Mirror Tint", Color) = (0.12, 0.5, 0.95, 0.58)
        _Color ("Color", Color) = (0.12, 0.5, 0.95, 0.58)
        _LineColor ("Cell Line Color", Color) = (0.03, 0.9, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.58
        _Pulse ("Pulse", Range(0, 4)) = 0
        _MirrorStrength ("Mirror Strength", Range(0, 1)) = 0.9
        _Distortion ("Facet Distortion", Range(0, 2)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _Color;
            fixed4 _LineColor;
            float _Alpha;
            float _Pulse;
            float _MirrorStrength;
            float _Distortion;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float voronoiEdge(float2 uv)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float d1 = 8.0;
                float d2 = 8.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 lattice = float2(x, y);
                        float2 offset = hash22(g + lattice);
                        offset = 0.5 + 0.48 * sin(_Time.y * 0.22 + 6.2831853 * offset);
                        float2 r = lattice + offset - f;
                        float d = dot(r, r);
                        if (d < d1)
                        {
                            d2 = d1;
                            d1 = d;
                        }
                        else if (d < d2)
                        {
                            d2 = d;
                        }
                    }
                }

                return sqrt(d2) - sqrt(d1);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float fresnel = pow(1.0 - saturate(dot(n, viewDir)), 2.4);
                float facet = sin(dot(floor(i.uv * 7.0), float2(13.7, 41.3)) + _Time.y * 0.6);
                float reflection = sin((i.worldPos.x + i.worldPos.z) * 0.028 + facet * _Distortion + _Time.y * 0.85) * 0.5 + 0.5;
                float edge = 1.0 - smoothstep(0.025, 0.105, voronoiEdge(i.uv * 2.8));

                float3 mirror = lerp(_BaseColor.rgb * 0.36, float3(0.85, 0.96, 1.0), reflection * _MirrorStrength);
                mirror += fresnel * float3(0.18, 0.55, 1.0);
                mirror = lerp(mirror, _LineColor.rgb * (1.0 + _Pulse * 0.55), edge);

                fixed4 color;
                color.rgb = mirror;
                color.a = saturate(_Alpha * (0.3 + fresnel * 0.5 + edge * 0.7 + _Pulse * 0.08));
                return color;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
}
