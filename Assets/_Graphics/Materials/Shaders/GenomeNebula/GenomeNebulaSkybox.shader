// An ALTERNATIVE skybox: the dark-blue marbled nebula that the Amoebius genome search evolved
// (seed amoebius_like/minimal__tuned, 118 zlib bytes for the whole genome, its sky is 4 nodes).
// Prototype on branch claude/genome-nebula-skybox; nothing in the game uses it yet.
// Doc: Docs/GENOME_NEBULA_SKYBOX.md. Tools: Tools/Build/genome_nebula_preview.py (preview, lab parity),
// Tools/Build/genome_nebula_methods.py (every way to produce it, scored).
//
// Pass 0 "Sky"         : the skybox itself (RenderSettings.skybox).
// Pass 1 "BakeEquirect": the sky into an equirect target laid out like StaticSkyPanorama.shader.
// Pass 2 "BakeNoise3D" : the two raw noise fields into one slice (_Slice) of a wrapping RGHalf 3D texture.
// Pass 3 "BakeCubeFace": the sky into one cube face (_FaceX/_FaceY/_FaceZ); the baker's fallback when
//                        Camera.RenderToCubemap refuses (face orientation not yet verified on device).
// GenomeNebulaSkyBaker drives the bakes and the cubemap modes.
//
// Mapping:
//   EQUIRECT  the lab's own mapping, pixel-exact with the lab. Pinches at the two poles (the lab's frames do too).
//   SPHERE    the same genome on 3D noise over the view direction: no seam, no poles. Reference for every bake.
//   NOISE3D   SPHERE with the two noise fields read from _Noise3D (3 fetches) and only the warp and colour run per
//             pixel: animates like SPHERE at a fraction of the cost, and needs no 32-bit floats.
//
// Precision: EQUIRECT and SPHERE hash in 32-bit floats; never change float to half there (in fp16 89% of hash
// values come out wrong, measured). NOISE3D's per-pixel math is fp16-safe; its texture must be half float
// (RG8 terraces visibly: the double warp magnifies 8-bit steps).
Shader "CosmicShore/GenomeNebulaSkybox"
{
    Properties
    {
        [KeywordEnum(Equirect, Sphere, Noise3D)] _Mapping ("Mapping", Float) = 0

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

        [Header(Sphere and Noise3D)]
        _SphereScale ("Sphere mapping scale", Range(0.05, 1)) = 0.2387
        _Period ("Tile cells (field 0, field 1)", Vector) = (2, 4, 0, 0)
        [NoScaleOffset] _Noise3D ("Baked fields (RGHalf 3D, set by GenomeNebulaSkyBaker)", 3D) = "" {}
        _Slice ("Bake slice (set by the baker)", Float) = 0
        _Noise3DSize ("Bake size (set by the baker)", Float) = 96
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    #if defined(_MAPPING_NOISE3D)
    sampler3D _Noise3D;
    // texture coordinate = p / tile length; tile length = cells per tile / base frequency
    #define GN_FIELD0(p) tex3Dlod(_Noise3D, float4((p) * (_Noise0.x / _Period.x), 0)).r
    #define GN_FIELD1(p) tex3Dlod(_Noise3D, float4((p) * (_Noise1.x / _Period.y), 0)).g
    #endif
    #include "GenomeNebula.hlsl"

    float _Slice, _Noise3DSize;
    float4 _FaceX, _FaceY, _FaceZ;

    struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
    struct v2f { float4 pos : SV_POSITION; float3 viewDir : TEXCOORD0; float2 uv : TEXCOORD1; };

    float3 gn_sky(float3 rd)
    {
        float t = _Time.y;
    #if defined(_MAPPING_SPHERE) || defined(_MAPPING_NOISE3D)
        return gn_finish(gn_sky_sphere(rd, t));
    #else
        return gn_finish(gn_sky_uv(gn_equirect(rd), t));
    #endif
    }

    v2f vert_sky(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.viewDir = v.vertex.xyz;
        o.uv = v.uv;
        return o;
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
            #pragma vertex vert_sky
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _MAPPING_EQUIRECT _MAPPING_SPHERE _MAPPING_NOISE3D

            half4 frag(v2f i) : SV_Target { return half4(gn_sky(normalize(i.viewDir)), 1); }
            ENDCG
        }

        Pass
        {
            Name "BakeEquirect"
            ZTest Always
            CGPROGRAM
            #pragma vertex vert_sky
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _MAPPING_EQUIRECT _MAPPING_SPHERE _MAPPING_NOISE3D

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

        Pass
        {
            Name "BakeNoise3D"
            ZTest Always
            CGPROGRAM
            #pragma vertex vert_sky
            #pragma fragment frag
            #pragma target 3.0

            // One slice of the tile: texel centres, both fields always procedural here (this pass has no NOISE3D).
            // A platform that flips render targets mirrors the noise, which is still the same kind of noise.
            float4 frag(v2f i) : SV_Target
            {
                float3 q = float3(i.uv, (_Slice + 0.5) / _Noise3DSize);
                return float4(gn_fbm3(q * (_Period.x / _Noise0.x), _Noise0, _Period.x),
                              gn_fbm3(q * (_Period.y / _Noise1.x), _Noise1, _Period.y), 0, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "BakeCubeFace"
            ZTest Always
            CGPROGRAM
            #pragma vertex vert_sky
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _MAPPING_EQUIRECT _MAPPING_SPHERE _MAPPING_NOISE3D

            // The GL / D3D cube face table: direction = Z + s*X + t*Y with s, t in -1..1 from the face's first texel.
            // Unity renders into a texture upside down where UVs start at the top, so t is flipped there.
            half4 frag(v2f i) : SV_Target
            {
                float s = i.uv.x * 2 - 1;
                float t = i.uv.y * 2 - 1;
            #if UNITY_UV_STARTS_AT_TOP
                t = -t;
            #endif
                return half4(gn_sky(normalize(_FaceZ.xyz + _FaceX.xyz * s + _FaceY.xyz * t)), 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
