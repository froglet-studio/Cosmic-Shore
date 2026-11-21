// PrismSlice.shader — the Rhino sword's slice death (Docs/PRISM_ANIMATION.md §4.10).
// Everything that decides WHERE a vertex goes and WHETHER a fragment survives lives in
// PrismSlice.hlsl, which Tools/Shaders/verify_prism_slice.py compiles and executes; this file is
// the URP plumbing around it plus the colouring, and the verifier front-end compiles every pass
// of it with glslang against a mock of the URP library.
//
// It is hand-written rather than a Shader Graph for three reasons the graph cannot meet: the
// fragment stage needs the FACE SIDE (a back face seen through a cut is shaded as cut flesh), it
// needs two values the graph has no varying for (the vertex's REST depth and its far flag, both
// computed in the vertex stage before the map moves it), and the far flag must interpolate
// linearly across a triangle, which is what lets one comparison tell cap from rim.
//
// It is drawn ONLY by the batched pure-entity path (PrismSlice.cs -> PrismRenderService
// .SpawnSliceDebrisBatch), so the per-instance stamps below are DOTS-instanced properties fed by
// Entities Graphics MaterialProperty components (PrismRenderProperties.cs, the Prism Slice set).
// Their Properties-block defaults are the UNSTAMPED state — zero plane, zero motion — which
// renders the plain prism: an unstamped material is inert, never broken.
//
// Opaque + alpha clip, like every prism material (Docs/PRISM_ANIMATION.md §4.7: no prism in the
// transparent queue). Cull Off, because the back faces ARE the cut flesh seen through the rim
// and through the dissolve. No ShadowCaster pass: the render entities are spawned with shadow
// casting off, exactly like the explosion debris they replace.
Shader "CosmicShore/PrismSlice"
{
    Properties
    {
        [Header(Per instance stamps (Entities Graphics overrides) unstamped defaults)]
        [HDR] _BrightColor ("Bright (domain rim)", Color) = (1, 1, 1, 1)
        [HDR] _DarkColor ("Dark (domain base face)", Color) = (0.2, 0.2, 0.2, 1)
        _Spread ("Spread (carried by every prism entity, unused here)", Vector) = (0, 0, 0, 0)
        _SliceTiming ("Timing (start, duration, noise seed, unused)", Vector) = (0, 0, 0, 0)
        _SlicePlane ("Cut plane, object space (m, d)", Vector) = (0, 0, 0, 0)
        _SliceCentre ("Projection centre, object space (xyz) + half depth (w)", Vector) = (0, 0, 0, 1)
        _SlicePivot ("Hinge, world (xyz) + separation distance (w)", Vector) = (0, 0, 0, 0)
        _SliceAxis ("Hinge axis, world (xyz) + opening angle rad (w)", Vector) = (0, 0, 0, 0)
        _SliceDrift ("Drift velocity, world (xyz)", Vector) = (0, 0, 0, 0)

        [Header(Clock constants (written by PrismSlice from PrismSliceConfig at runtime))]
        _SliceMotionTimes ("Motion time constants (separate, open, drift drag, unused)", Vector) = (0.09, 0.3, 0.35, 0)
        _SliceDissolveWindow ("Dissolve window (start, end margin) as fractions of life", Vector) = (0.3, 0.08, 0, 0)

        [Header(Skin)]
        _FresnelPower ("Skin Fresnel Power", Range(0.5, 8)) = 3

        [Header(The cut face)]
        [HDR] _CutHotColor ("Fresh Cut Color (the instant of the cut)", Color) = (2.2, 2.2, 2.4, 1)
        _CutGlow ("Cut Face Brightness (x domain bright)", Range(0, 4)) = 1.35
        _CutShade ("Cut Face Angular Shading", Range(0, 1)) = 0.3
        _SeamCool ("Seconds the cut takes to cool from hot to flesh", Range(0.01, 1)) = 0.22
        _RimWidth ("Fresh-Cut Line Width on the skin (world units)", Range(0.01, 2)) = 0.07

        [Header(The dissolve)]
        _DissolveNoiseScale ("Dissolve Noise Cell (world units)", Range(0.05, 10)) = 0.55
        _DissolveNoise ("Dissolve Raggedness", Range(0, 1)) = 0.32
        _EmberBand ("Ember Band (fraction of the order)", Range(0.001, 0.5)) = 0.06
        _EmberGlow ("Ember Brightness (x domain bright)", Range(0, 6)) = 2.2
        // The flesh is already the domain's bright colour pushed past the bloom clamp, so an ember
        // that is only MORE of that colour is the same colour on screen. Leaning it toward the
        // fresh-cut white is what lets the front read as a front.
        _EmberWhite ("Ember Whiteness (toward the fresh-cut colour)", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BrightColor;
            float4 _DarkColor;
            float3 _Spread;
            float4 _SliceTiming;
            float4 _SlicePlane;
            float4 _SliceCentre;
            float4 _SlicePivot;
            float4 _SliceAxis;
            float4 _SliceDrift;
            float4 _SliceMotionTimes;
            float4 _SliceDissolveWindow;
            float _FresnelPower;
            float4 _CutHotColor;
            float _CutGlow;
            float _CutShade;
            float _SeamCool;
            float _RimWidth;
            float _DissolveNoiseScale;
            float _DissolveNoise;
            float _EmberBand;
            float _EmberGlow;
            float _EmberWhite;
        CBUFFER_END

        // The per-instance stamps. Sizes match the MaterialProperty components exactly
        // (PrismRenderProperties.cs): colours float4, spread float3, every slice stamp float4.
        #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _BrightColor)
                UNITY_DOTS_INSTANCED_PROP(float4, _DarkColor)
                UNITY_DOTS_INSTANCED_PROP(float3, _Spread)
                UNITY_DOTS_INSTANCED_PROP(float4, _SliceTiming)
                UNITY_DOTS_INSTANCED_PROP(float4, _SlicePlane)
                UNITY_DOTS_INSTANCED_PROP(float4, _SliceCentre)
                UNITY_DOTS_INSTANCED_PROP(float4, _SlicePivot)
                UNITY_DOTS_INSTANCED_PROP(float4, _SliceAxis)
                UNITY_DOTS_INSTANCED_PROP(float4, _SliceDrift)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #define PRISM_SLICE_PROP(type, name) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, name)
        #else
            #define PRISM_SLICE_PROP(type, name) name
        #endif

        // The platform clock every prism stamp is written against (PrismClock.cs). NEVER _Time:
        // URP feeds _Time from a different clock domain than the stamps.
        float _PrismClock;

        // The occlusion corridor's two globals (PrismOcclusionCorridor.cs, Shader.SetGlobalVector).
        // The corridor is a PLATFORM LAW (Docs/PRISM_ANIMATION.md §4.7): prism mass between the
        // camera and the pilot's ship goes see-through. The halves are born exactly where the
        // Rhino's blade kills — beside its own hull — so they obey it through the DEBRIS entry
        // point, the one ExplodingBlockGraph uses: no collider, so no nose clearance to buy.
        float4 _PrismOcclusionTarget;
        float4 _PrismOcclusionParams;

        #include "PrismSlice.hlsl"
        #include "PrismOcclusionCorridor.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 skinNormalWS : TEXCOORD0;   // the face's own normal, carried with the half
            float3 cutNormalWS : TEXCOORD1;    // the cut face's outward normal, carried with the half
            float3 positionWS : TEXCOORD2;     // where the fragment is NOW (moved)
            float4 cut : TEXCOORD3;            // x rest depth (world), y far flag, z depth fraction, w unused
            float3 noisePos : TEXCOORD4;       // world-scaled rest position, in noise cells
            nointerpolation float4 bright : TEXCOORD5;
            nointerpolation float4 dark : TEXCOORD6;
            nointerpolation float4 phase : TEXCOORD7;  // x dissolve progress, y heat (1 fresh .. 0 cool)
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings SliceVert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float4 timing = PRISM_SLICE_PROP(float4, _SliceTiming);
            float4 plane = PRISM_SLICE_PROP(float4, _SlicePlane);
            float4 centre = PRISM_SLICE_PROP(float4, _SliceCentre);
            float4 pivot = PRISM_SLICE_PROP(float4, _SlicePivot);
            float4 axis = PRISM_SLICE_PROP(float4, _SliceAxis);
            float4 drift = PRISM_SLICE_PROP(float4, _SliceDrift);

            float age = max(_PrismClock - timing.x, 0.0);

            // 1. The cut, in the prism's own frame.
            float3 posOS, nrmOS;
            float restDepth, far;
            PrismSliceCut(input.positionOS.xyz, input.normalOS, plane, centre.xyz,
                posOS, nrmOS, restDepth, far);

            // 2. Rest pose in world. A zero plane (unstamped) transforms to a zero normal; fall
            //    back to the skin's so the unstamped material stays well-formed.
            float3 restWS = TransformObjectToWorld(posOS);
            float3 skinWS = TransformObjectToWorldNormal(input.normalOS);
            float3 cutWS = dot(plane.xyz, plane.xyz) > 1e-12
                ? TransformObjectToWorldNormal(plane.xyz)
                : skinWS;

            // 3. The half's motion — rigid, in world space.
            float separate, open, driftAmount;
            PrismSliceMotion(age, _SliceMotionTimes, separate, open, driftAmount);
            float angle = axis.w * open;
            float3 posWS = PrismSliceMove(restWS, pivot.xyz, -cutWS, pivot.w,
                axis.xyz, angle, drift.xyz, separate, driftAmount);

            output.positionCS = TransformWorldToHClip(posWS);
            output.skinNormalWS = PrismSliceRotate(skinWS, axis.xyz, angle);
            output.cutNormalWS = PrismSliceRotate(cutWS, axis.xyz, angle);
            output.positionWS = posWS;

            // 4. The dissolve order. The cap sits ON the cut, so its depth is 0 whatever its rest
            //    vertex was — that is what makes the cut face the first thing to go.
            float cutDepth = far > 0.5 ? 0.0 : max(restDepth, 0.0);
            output.cut = float4(restDepth, far, cutDepth / max(centre.w, 1e-4), 0.0);

            float seed = timing.z;
            float3 scaledOS = mul((float3x3)GetObjectToWorldMatrix(), posOS);
            output.noisePos = scaledOS / max(_DissolveNoiseScale, 1e-3)
                + float3(17.13, 31.71, 7.37) * seed;

            output.bright = PRISM_SLICE_PROP(float4, _BrightColor);
            output.dark = PRISM_SLICE_PROP(float4, _DarkColor);
            float progress = PrismSliceDissolveProgress(age, timing.y, _SliceDissolveWindow.xy);
            float heat = 1.0 - smoothstep(0.0, max(_SeamCool, 1e-3), age);
            output.phase = float4(progress, heat, 0.0, 0.0);
            return output;
        }

        // The shared clip, applied identically by every pass so depth and colour agree on the
        // silhouette. Returns the fragment's dissolve value for the colour pass's ember band.
        float SliceClip(Varyings input)
        {
            float progress = input.phase.x;
            float value = 1.0;
            if (progress > 0.0)
            {
                float noise = PrismSliceNoise(input.noisePos);
                value = PrismSliceDissolveValue(input.cut.z, noise, _DissolveNoise);
            }
            clip(PrismSliceClipped(input.cut.x, input.cut.y, value, progress) ? -1.0 : 1.0);

            // The corridor, last: the same screen door every prism and every debris chunk wears.
            float corridorAlpha, corridorThreshold;
            PrismOcclusionFadeDebris_float(input.positionWS, _PrismOcclusionTarget.xyz,
                _PrismOcclusionParams.xyz, 1.0, corridorAlpha, corridorThreshold);
            clip(corridorAlpha - corridorThreshold);
            return value;
        }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SliceVert
            #pragma fragment SliceFrag
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            half4 SliceFrag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float value = SliceClip(input);
                float progress = input.phase.x;
                float heat = input.phase.y;
                float3 bright = input.bright.rgb;
                float3 V = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                // The cut flesh: the domain's bright colour, starting white-hot at the instant of
                // the cut and cooling. Shaded by the CUT's normal whether it is the cap itself or a
                // back face seen through the rim or the dissolve — one surface, one shading, so the
                // rim sliver is indistinguishable from the cap beside it.
                float3 flesh = lerp(bright * _CutGlow, _CutHotColor.rgb, heat);
                float3 cutN = SafeNormalize(input.cutNormalWS);
                float fleshShade = lerp(1.0, abs(dot(cutN, V)), _CutShade);

                bool front = IS_FRONT_VFACE(face, true, false);
                bool cap = input.cut.y >= PRISM_SLICE_CAP_FLAG;   // wholly-cap triangles only, the clip's own test
                float3 color;
                if (!front || cap)
                {
                    color = flesh * fleshShade;
                }
                else
                {
                    // The skin: the prism's own look — the domain base face, brightening to the
                    // rim at grazing angles — plus the fresh-cut line hugging the cut, which is
                    // what reads as a blade having passed through before the halves have moved.
                    float3 N = SafeNormalize(input.skinNormalWS);
                    float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                    color = lerp(input.dark.rgb, bright, fres);
                    float seam = exp(-max(input.cut.x, 0.0) / max(_RimWidth, 1e-3)) * heat;
                    color = lerp(color, _CutHotColor.rgb, saturate(seam));
                }

                // The ember band just behind the dissolve front.
                if (progress > 0.0)
                {
                    float ember = 1.0 - saturate((value - progress) / max(_EmberBand, 1e-4));
                    float3 emberColor = lerp(bright * _EmberGlow, _CutHotColor.rgb, _EmberWhite);
                    color = lerp(color, emberColor, ember);
                }

                return half4(color, 1.0);
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
            #pragma target 3.5
            #pragma vertex SliceVert
            #pragma fragment SliceDepthFrag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            half SliceDepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SliceClip(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SliceVert
            #pragma fragment SliceDepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            half4 SliceDepthNormalsFrag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SliceClip(input);

                bool front = IS_FRONT_VFACE(face, true, false);
                bool cap = input.cut.y >= PRISM_SLICE_CAP_FLAG;   // wholly-cap triangles only, the clip's own test
                float3 normalWS = (!front || cap) ? input.cutNormalWS : input.skinNormalWS;
                normalWS = SafeNormalize(front ? normalWS : -normalWS);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
