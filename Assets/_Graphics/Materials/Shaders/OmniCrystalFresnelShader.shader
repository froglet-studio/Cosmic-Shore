// The omni crystal's BODY: SpreadFresnelShader's look (via the shared SpreadFresnelCore.hlsl),
// plus the two things only the omni needs. Docs/PALETTE.md §2.10.
//
//  1. The CrystalMorph vertex path. The Scarab's crystal->ball forge (ScarabCrystalMorph) bakes a
//     morph mesh with each vertex's destination in TEXCOORD2 and its destination normal in
//     TEXCOORD3, then stamps _CrystalMorph = (start, duration, stagger) once against the
//     _PrismClock global. Duration 0 (every material's default) is a pass-through, so an
//     unstamped crystal draws exactly what SpreadFresnelShader draws.
//  2. A dissolve. FadeIn blooms every crystal model in on _opacity and the forge's tail fades on
//     _Opacity; this pass is opaque (ZWrite On), so coverage is spent as a screen-door clip
//     rather than blending (Docs/PRISM_ANIMATION.md §4.7). At coverage 1 nothing is clipped.
Shader "Custom/OmniCrystalFresnelShader"
{
    Properties
    {
        [HDR]_BrightColor ("Bright Color", Color) = (1,1,1,1)
        [HDR]_DarkColor ("Dark Color", Color) = (0,0,0,1)
        _Spread ("Spread", Vector) = (1,1,1,0)
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 5
        _CrystalMorph ("Crystal Morph (start, duration, stagger)", Vector) = (0,0,0,0)
        _Opacity ("Opacity", Range(0, 1)) = 1
        [HideInInspector] _opacity ("FadeIn Opacity", Float) = 1
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
            #include "SpreadFresnelCore.hlsl"
            #include "Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl"

            float3 _CrystalMorph;
            float _Opacity;
            float _opacity;
            float _PrismClock;   // UNEXPOSED global, published by PrismClock — the clock the stamp uses

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 morphTarget : TEXCOORD2;
                float4 morphNormal : TEXCOORD3;
            };

            struct v2f
            {
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 normalOS;
                // The morph runs LAST, on the already-spread position, so t = 0 is exactly the
                // crystal as drawn and t = 1 is the bare target (CrystalMorph.hlsl's contract).
                float3 positionOS;
                CrystalMorph_float(SpreadFresnelDisplace(v.vertex.xyz, v.normal), v.morphTarget,
                                   _PrismClock, _CrystalMorph, positionOS);
                CrystalMorphNormal_float(v.normal, v.morphNormal, _PrismClock, _CrystalMorph, normalOS);
                v.vertex.xyz = positionOS;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = normalize(mul((float3x3)UNITY_MATRIX_M, normalOS));

                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Interleaved gradient noise, nudged strictly inside (0,1): clip(0) KEEPS a
                // fragment, so a raw 0 threshold would leave confetti at coverage 0.
                float coverage = saturate(_Opacity * _opacity);
                float n = frac(52.9829189 * frac(dot(i.vertex.xy, float2(0.06711056, 0.00583715))));
                clip(coverage - (n * 0.998 + 0.001));
                return SpreadFresnelColor(i.worldPos, i.worldNormal);
            }
            ENDCG
        }
    }
}
