// An ALTERNATIVE skybox: the dark-blue marbled nebula that the Amoebius genome search evolved
// (seed amoebius_like/minimal__tuned, 118 zlib bytes for the whole genome, its sky is 4 nodes).
// Prototype on branch claude/genome-nebula-skybox; nothing in the game uses it yet.
// Doc: Docs/GENOME_NEBULA_SKYBOX.md. Preview / parity tool: Tools/Build/genome_nebula_preview.py.
//
// Pass 0 "Sky"   : the skybox itself (Lighting > Environment > Skybox Material, or RenderSettings.skybox).
// Pass 1 "Bake"  : draws the same sky into an equirect render target laid out like StaticSkyPanorama.shader,
//                  so GenomeNebulaSkyBaker can bake it ONCE at load and the frame cost drops to one texture
//                  sample per pixel with no texture shipped in the build.
//
// Mapping: EQUIRECT is the lab's own mapping (pixel-exact with the lab, seamless in longitude, pinches at the
// two poles, as the lab's frames do). SPHERE runs the same genome on 3D noise over the view direction: no seam,
// no poles, about 2x the ALU; the marbling reads the same but is not pixel-identical to the lab.
//
// Precision: the hashes need 32-bit floats. Do not change float to half here (Mali/Adreno mediump breaks them).
Shader "CosmicShore/GenomeNebulaSkybox"
{
    Properties
    {
        [KeywordEnum(Equirect, Sphere)] _Mapping ("Mapping", Float) = 0

        [Header(Node 0 tex.perlin  the nebula colour)]
        _ColA0 ("Colour A (linear RGB)", Vector) = (0.007902, 0.01931, 0.004392, 0)
        _ColB0 ("Colour B (linear RGB)", Vector) = (0.363948, 0.330118, 0.80943, 0)
        _Noise0 ("Freq, Octaves, Persistence, Seed", Vector) = (3, 4, 0.61647, 124.27)
        _Gamma0 ("Gamma", Range(0.2, 4)) = 1.39323

        [Header(Node 1 tex.perlin  the warp field)]
        _ColA1 ("Colour A (linear RGB)", Vector) = (0.04827, 0.029928, 0.026312, 0)
        _ColB1 ("Colour B (linear RGB)", Vector) = (0.82867, 0.82867, 0.82867, 0)
        _Noise1 ("Freq, Octaves, Persistence, Seed", Vector) = (6, 3, 0.51059, 811.41)
        _Gamma1 ("Gamma", Range(0.2, 4)) = 1.10882

        [Header(Nodes 2 and 3 tex.distort)]
        _Warp ("Warp amounts (node 2, node 3)", Vector) = (0.43137, 0.32745, 0, 0)

        [Header(Animation  not in the lab  0 is static)]
        _Drift ("Drift (warp u/s, warp v/s, colour u/s)", Vector) = (0.004, 0.0015, -0.001, 0)

        [Header(Out node and grade)]
        _SkyIntensity ("Sky gene", Range(0, 4)) = 0.34884
        _FogColor ("Fog colour (linear RGB)", Vector) = (0.015805, 0.025055, 0.07197, 0)
        _FogMix ("Fog mix", Range(0, 1)) = 0.12454
        _Grade ("Exposure, Saturation, Contrast, Bake grade (1/0)", Vector) = (0.88487, 1.02756, 1.09981, 1)
        _SphereScale ("Sphere mapping scale", Range(0.05, 1)) = 0.2387
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "GenomeNebula.hlsl"

    struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
    struct v2f { float4 pos : SV_POSITION; float3 viewDir : TEXCOORD0; float2 uv : TEXCOORD1; };

    float3 gn_sky(float3 rd)
    {
        float t = _Time.y;
    #if defined(_MAPPING_SPHERE)
        return gn_finish(gn_sky_sphere(rd, t));
    #else
        return gn_finish(gn_sky_uv(gn_equirect(rd), t));
    #endif
    }
    ENDCG

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            Name "Sky"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _MAPPING_EQUIRECT _MAPPING_SPHERE

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.vertex.xyz;
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(gn_sky(normalize(i.viewDir)), 1);
            }
            ENDCG
        }

        Pass
        {
            Name "Bake"
            ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _MAPPING_EQUIRECT _MAPPING_SPHERE

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = 0;
                o.uv = v.uv;
                return o;
            }

            // Inverse of StaticSkyPanorama.shader: u = 0.5 - atan2(z,x)/2pi, v = 1 - acos(y)/pi.
            half4 frag(v2f i) : SV_Target
            {
                float lon = (0.5 - i.uv.x) * 2.0 * UNITY_PI;
                float lat = (1.0 - i.uv.y) * UNITY_PI;
                float3 d = float3(sin(lat) * cos(lon), cos(lat), sin(lat) * sin(lon));
                return half4(gn_sky(d), 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
