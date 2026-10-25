// The baked HyperSea sky: ONE texture sample per pixel, where HyperSeaSkybox.shader runs two
// 3x3x3 Voronoi searches and ~20 octaves of noise. The panorama is written OFFLINE by
// Tools/Build/bake_static_skybox.py, which inverts exactly the mapping below - change one and the
// other must change with it. tex2Dlod at level 0: the equirect seam is a derivative discontinuity,
// and the texture is imported without mips, so there is nothing to select between.
Shader "CosmicShore/StaticSkyPanorama"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama (equirectangular, baked)", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.viewDir);
                float2 uv = float2(0.5 - atan2(d.z, d.x) * (0.5 / UNITY_PI),
                                   1.0 - acos(clamp(d.y, -1.0, 1.0)) / UNITY_PI);
                return tex2Dlod(_MainTex, float4(uv, 0, 0));
            }
            ENDCG
        }
    }

    Fallback Off
}
