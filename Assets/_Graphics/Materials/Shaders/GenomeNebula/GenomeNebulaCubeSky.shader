// Draws a cubemap the GenomeNebulaSkyBaker baked from CosmicShore/GenomeNebulaSkybox: ONE texture sample per pixel,
// no seam and no pole (a cube has neither). Measured best static way to draw the nebula on a phone
// (Docs/GENOME_NEBULA_SKYBOX.md section 6): 512^2 faces beat a 4096x2048 panorama at a fifth of the memory.
Shader "CosmicShore/GenomeNebulaCubeSky"
{
    Properties
    {
        [NoScaleOffset] _Cube ("Baked sky (cubemap, set by GenomeNebulaSkyBaker)", Cube) = "black" {}
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            samplerCUBE _Cube;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target { return half4(texCUBElod(_Cube, float4(i.dir, 0)).rgb, 1); }
            ENDCG
        }
    }

    Fallback Off
}
