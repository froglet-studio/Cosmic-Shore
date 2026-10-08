// The omni crystal's BODY: SpreadFresnelShader's look (transcribed below, unchanged),
// plus the two things only the omni needs. Docs/PALETTE.md §2.10.
//
//  1. The CrystalMorph vertex path. A vessel's bespoke retirement (ScarabCrystalMorph: the
//     crystal closing onto the ball it forged; SquirrelCrystalMorph: the crystal becoming the
//     eight shielded prisms of its boost ring) bakes a morph mesh with each vertex's destination
//     in TEXCOORD2 and its destination normal in TEXCOORD3, then stamps
//     _CrystalMorph = (start, duration, stagger) once against the _PrismClock global. Duration 0
//     (every material's default) is a pass-through, so an unstamped crystal draws exactly what
//     SpreadFresnelShader draws.
//  1b. The COLOUR FORMULA travels with the shape. Both targets draw with BlockGraph, whose face
//     colour is lerp(_DarkColor, _BrightColor, (1 - N.V)^4) (FresnelColors -> FresnelPower4),
//     while this body draws lerp(_BrightColor, _DarkColor, (1 + N.V) / 2). The runner converges
//     the colour PAIR onto the target's, but the same pair through two different formulas is
//     still two different surfaces - so each face also blends from this formula to the prism's
//     on its own morph weight (CrystalMorphEase, the schedule its position runs on). At weight 0
//     - every unstamped crystal - the blend is lerp(a, b, 0) and the body is bit-identical.
//  2. A dissolve for the forge's tail, on _Opacity. This pass is opaque (ZWrite On), so coverage
//     is spent as a screen-door clip rather than blending (Docs/PRISM_ANIMATION.md §4.7). At
//     coverage 1 nothing is clipped.
//
// It deliberately does NOT read FadeIn's lowercase _opacity. FadeIn's curve is slow and
// back-loaded (about 1 s before 10%, 2.3 s to half, 2.9 s to full at 60 fps), and the omni has
// always appeared at once on respawn - its old ShepardGraph shells only had _Opacity - so wiring
// it in made a collected crystal's replacement visibly lag in Skim Race.
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
            // SpreadFresnelShader's look, transcribed verbatim (same displacement, same colour) so
            // the omni body reads exactly like the elemental crystals. Kept self-contained on
            // purpose: the elemental shader is not touched by this branch. Folding the two onto a
            // shared include is the follow-up once this one is verified in the editor.
            float3 _Spread;
            float _FresnelPower;
            float4 _BrightColor, _DarkColor;

            // Pushes a vertex out along its normal by _Spread WORLD units (divided by the object's own
            // scale, so the push does not grow with the transform).
            float3 SpreadFresnelDisplace(float3 positionOS, float3 normalOS)
            {
                float3 objectScale = float3(length(unity_ObjectToWorld._m00_m01_m02), length(unity_ObjectToWorld._m10_m11_m12), length(unity_ObjectToWorld._m20_m21_m22));
                // normalize: a blend-shape mesh hands the vertex stage a blended normal that is not unit
                // length (spacecrystalanim.fbx's stacked keys reach ~2x), and the push must not scale with it.
                float3 spreadedNormal = normalize(normalOS) * (_Spread / objectScale);
                return positionOS + spreadedNormal;
            }

            // The face colour: dark where the face looks at the camera, bright toward the rim.
            half4 SpreadFresnelColor(float3 worldPos, float3 worldNormal)
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                float fresnel = (1.0 + dot(viewDir, worldNormal))/2;
                half4 col = lerp( _BrightColor, _DarkColor, fresnel);
                return col;
            }

            #include "Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl"

            float3 _CrystalMorph;
            float _Opacity;
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
                // This vertex's morph progress, constant across a face (the phase is per solid or
                // per panel), so the colour formula hands over face by face with the geometry.
                float morphEase : TEXCOORD2;
                float4 vertex : SV_POSITION;
            };

            // The TARGET's face colour - BlockGraph's FresnelColors subgraph, transcribed:
            //   d = N.V (world); x = d > 0 ? d : (d + 1) * 0.2   (FresnelPower4's back-face branch)
            //   f = (1 - x)^4;   colour = lerp(_DarkColor, _BrightColor, f)
            // DistanceSpreadAndColors' far-distance tint is not carried: at pickup range it is
            // 0.3-2.5% of its range (_SqrDistance 100000 = 316 u), and the runner's dissolve tail
            // is what absorbs a residue that small.
            half4 PrismFresnelColor(float3 worldPos, float3 worldNormal)
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                float d = dot(normalize(worldNormal), viewDir);
                float x = d > 0.0 ? d : (d + 1.0) * 0.2;
                float f = (1.0 - x) * (1.0 - x);
                f *= f;
                return lerp(_DarkColor, _BrightColor, f);
            }

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
                CrystalMorphEase_float(v.morphTarget, _PrismClock, _CrystalMorph, o.morphEase);
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
                float coverage = saturate(_Opacity);
                float n = frac(52.9829189 * frac(dot(i.vertex.xy, float2(0.06711056, 0.00583715))));
                clip(coverage - (n * 0.998 + 0.001));
                half4 body = SpreadFresnelColor(i.worldPos, i.worldNormal);
                return lerp(body, PrismFresnelColor(i.worldPos, i.worldNormal), i.morphEase);
            }
            ENDCG
        }
    }
}
