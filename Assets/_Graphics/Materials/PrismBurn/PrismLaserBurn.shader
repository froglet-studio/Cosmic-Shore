// PrismLaserBurn.shader - Fulmen's flameless "laser" burn: the prism's skin peels off behind a thin red-hot line and
// turns to ash in the DOMAIN colour, which then crumbles away - a lightning strike on a heap of dry leaves, minus the fire.
// No flame, no light that expands, no bloom (all colours are LDR), no screen-space anything, no textures, no _Time.
//
// Drawn only by PrismBurnView (Assets/_Scripts/Controller/Managers/PrismBurnView.cs) as Graphics.RenderMeshInstanced
// PROXIES (24-vertex unit cube, -0.5..0.5, scaled onto the prism's bounds) while the real prism is hidden. The prisms'
// own Entities Graphics materials are never touched. Docs: Docs/Shaders/PRISM_EFFECTS.md ("Burn - laser").
//
// Per instance (MaterialPropertyBlock arrays, so one draw call per 1023 burns and no material instances):
//   _Burn = (progress 0..1, seed, coverage 0..1, flags)   - exactly the paper burn's layout
//            flags +1 invert the dither (tier crossfade), +2 NO HOLES (a convert burn: scorches, never burns through)
//   _Ash  = (domain colour rgb, unused)                   - the skin AND the ash are tinted from it
//
// The two fronts, both "how far past this point": > 0 = passed.
//   skin front  = progress * span - field             -> the red-hot line; behind it the skin is ash
//   ash  front  = (progress - _AshLag) * span - field -> behind it the ash has crumbled away (clipped)
//   span = 1.05 / (1 - _AshLag), so at progress 1 the ash front has passed EVERY point: the proxy is gone exactly when
//   PrismBurnManager removes the prism (silent burnout). Tools/Shaders/verify_prism_burn.py proves this in C++.
//
// Mobile: target 3.0 (GLES 3.0), one octave of value noise, opaque alpha-clip in AlphaTest (no sorting, no overdraw
// blend), main light only with a wrap term. Tweak the look on PrismLaserBurn.mat; the timing lives in FulmenConfigSO.
Shader "CosmicShore/PrismLaserBurn"
{
    Properties
    {
        _SkinBrightness ("Skin Brightness (x domain colour)", Range(0, 2)) = 1.0
        _HeatColor ("Heat (skin darkening ahead of the line)", Color) = (0.45, 0.06, 0.02, 1)
        _EmberColor ("Red-Hot Line", Color) = (1.0, 0.32, 0.06, 1)
        _EmberCore ("Red-Hot Line Core", Color) = (1.0, 0.78, 0.45, 1)
        _EmberIntensity ("Red-Hot Intensity (keep <= ~2 for no bloom)", Range(0, 3)) = 1.6
        _EmberWidth ("Red-Hot Line Width", Range(0.005, 0.12)) = 0.03
        _HeatWidth ("Heat Band Width", Range(0.01, 0.4)) = 0.12
        _AshDarken ("Ash Darkening (0 = domain colour, 1 = black)", Range(0, 1)) = 0.55
        _AshSpeckle ("Ash Flake Speckle", Range(0, 1)) = 0.5
        _AshEmbers ("Smouldering Embers In Ash", Range(0, 1)) = 0.35
        _AshLag ("Ash Lag (how long ash lingers, progress)", Range(0.05, 0.8)) = 0.35
        _NoiseFreq ("Edge Noise Frequency", Range(0.5, 16)) = 3.2
        _NoiseAmount ("Edge Noise Amount", Range(0, 1)) = 0.42
        _Ambient ("Ambient", Range(0, 1)) = 0.45
        [HideInInspector] _Burn ("Burn (per instance)", Vector) = (0, 0, 1, 0)
        [HideInInspector] _Ash ("Domain colour (per instance)", Vector) = (0.2, 0.45, 1, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Cull Off

        HLSLINCLUDE
        #pragma target 3.0
        #pragma multi_compile_instancing
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "PrismEffectCommon.hlsl"   // Hash31 / ValueNoise / PrismIgnitionPoint / PrismDitherClip

        CBUFFER_START(UnityPerMaterial)
            half4 _HeatColor, _EmberColor, _EmberCore;
            half _SkinBrightness, _EmberIntensity, _EmberWidth, _HeatWidth, _AshDarken, _AshSpeckle, _AshEmbers;
            half _AshLag, _NoiseFreq, _NoiseAmount, _Ambient;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(LaserBurnProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Burn)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Ash)
        UNITY_INSTANCING_BUFFER_END(LaserBurnProps)

        // The burn FIELD: 0 at the seeded strike point on the surface, ~1 at the far corner, roughened by one noise octave.
        float LaserField(float3 posOS, float seed)
        {
            float3 origin = PrismIgnitionPoint(seed);
            float d = length(posOS - origin) * 0.5774;
            float n = ValueNoise(posOS * _NoiseFreq + seed * 7.31);
            return d * (1.0 - _NoiseAmount) + n * _NoiseAmount;
        }

        // Both fronts from ONE field evaluation (the fragment's only noise octave): x = skin front (the red-hot line;
        // > 0 behind it = ash), y = ash front (> 0 = the ash has crumbled away, clipped). NO HOLES caps the progress
        // and never lets the ash front through.
        float2 LaserFronts(float field, float4 burn)
        {
            bool noHoles = fmod(burn.w, 4.0) >= 2.0;
            float p = noHoles ? min(burn.x, 0.55) : burn.x;
            float span = 1.05 / max(1.0 - _AshLag, 0.05);
            float ash = (p - _AshLag) * span - field;
            return float2(p * span - field, noHoles ? min(ash, -0.01) : ash);
        }

        float LaserSkinFront(float3 posOS, float4 burn) { return LaserFronts(LaserField(posOS, burn.y), burn).x; }
        float LaserAshFront(float3 posOS, float4 burn) { return LaserFronts(LaserField(posOS, burn.y), burn).y; }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.posOS = input.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 Frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 burn = UNITY_ACCESS_INSTANCED_PROP(LaserBurnProps, _Burn);
                half3 domain = (half3)UNITY_ACCESS_INSTANCED_PROP(LaserBurnProps, _Ash).rgb;
                PrismDitherClip(i.positionCS.xy, burn.z, fmod(burn.w, 2.0) >= 1.0);

                float2 fronts = LaserFronts(LaserField(i.posOS, burn.y), burn);
                float ashF = fronts.y;
                // the crumble edge is flaky, like the last of a burnt leaf: speckled pixels go a little early
                float flake = Hash31(floor(i.posOS * 48.0) + burn.y);
                clip(-(ashF + flake * _AshSpeckle * 0.06));

                float skinF = fronts.x;
                float hot = saturate(1.0 - abs(skinF) / _EmberWidth);              // 1 on the red-hot line
                float heat = saturate(1.0 + skinF / _HeatWidth) * step(skinF, 0.0); // skin warming ahead of the line
                float ash = step(0.0, skinF);                                        // behind the line: ash

                // skin: the domain colour, darkening toward the heat band
                half3 albedo = domain * _SkinBrightness;
                albedo = lerp(albedo, _HeatColor.rgb, heat * heat);
                // ash: the same domain colour, darkened and speckled into flakes
                half3 ashCol = domain * (1.0 - _AshDarken) * (1.0 - _AshSpeckle * 0.5 * flake);
                albedo = lerp(albedo, ashCol, ash);

                float3 n = normalize(i.normalWS) * (isFront ? 1.0 : -1.0);
                Light light = GetMainLight();
                half wrap = saturate(dot(n, light.direction) * 0.6 + 0.4);
                half3 col = albedo * (_Ambient + wrap * light.color * (1.0 - _Ambient));

                // the red-hot line (unlit, LDR): a hot core fading to red; no light thrown anywhere else
                half3 ember = lerp(_EmberColor.rgb, _EmberCore.rgb, hot * hot) * hot;
                // embers dying out in the fresh ash, keyed to progress (no _Time: they fade as the burn advances)
                float cool = saturate(1.0 - skinF / max(_AshLag * 0.6, 0.01));
                ember += _EmberColor.rgb * _AshEmbers * ash * cool * step(0.82, ValueNoise(i.posOS * 11.0 + burn.y));
                col += ember * _EmberIntensity * step(0.001, burn.x);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 posOS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DVaryings DepthVert(DAttributes input)
            {
                DVaryings o = (DVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.posOS = input.positionOS.xyz;
                return o;
            }

            half DepthFrag(DVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 burn = UNITY_ACCESS_INSTANCED_PROP(LaserBurnProps, _Burn);
                PrismDitherClip(i.positionCS.xy, burn.z, fmod(burn.w, 2.0) >= 1.0);
                float flake = Hash31(floor(i.posOS * 48.0) + burn.y);
                clip(-(LaserAshFront(i.posOS, burn) + flake * _AshSpeckle * 0.06));
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
