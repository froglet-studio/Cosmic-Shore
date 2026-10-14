// PrismCollisionBurst.shader — the collision death of a MOVING prism (Docs/PRISM_ANIMATION.md
// §4.11, Docs/PRISM_MOTION.md § "The look"). Everything that decides WHERE a vertex goes and
// WHETHER a fragment survives lives in PrismCollisionBurst.hlsl, which
// Tools/Shaders/verify_prism_collision_burst.py compiles and EXECUTES; this file is the URP
// plumbing around it plus the colouring, and the verifier front-end compiles every pass of it with
// glslang against a mock of the URP library.
//
// It is the Rhino slice's sibling (PrismSlice.shader) and rides the slice's carrier unchanged: the
// pure render entities PrismCollisionBurst.cs spawns through PrismRenderService
// .SpawnSliceDebrisBatch, with the slice's per-instance stamp channels, two of which a burst reads:
//     _SliceTiming = (start, life, seed, kind)       — kind 0 shatter, 1 bloom, 2 unravel, 3 glitter
//     _SliceDrift  = (flight velocity, world xyz; explosion speed w)
// Their Properties-block defaults are the UNSTAMPED state — age 0, zero flight — which renders the
// plain prism: an unstamped material is inert, never broken.
//
// Opaque + alpha clip like every prism material (no prism in the transparent queue). Cull Off:
// the pieces are single skins, and once they part the inside of the cube is on show. No
// ShadowCaster pass: the entities cast none, like the explosion debris they stand in for.
Shader "CosmicShore/PrismCollisionBurst"
{
    Properties
    {
        [Header(Per instance stamps (Entities Graphics overrides) unstamped defaults)]
        [HDR] _BrightColor ("Bright (domain rim)", Color) = (1, 1, 1, 1)
        [HDR] _DarkColor ("Dark (domain base face)", Color) = (0.2, 0.2, 0.2, 1)
        _Spread ("Spread (carried by every prism entity, unused here)", Vector) = (0, 0, 0, 0)
        _SliceTiming ("Timing (start, life, seed, kind)", Vector) = (0, 7.5, 0, 0)
        _SlicePlane ("Slice plane (carried by the slice entity, unused here)", Vector) = (0, 0, 0, 0)
        _SliceCentre ("Slice centre (carried by the slice entity, unused here)", Vector) = (0, 0, 0, 1)
        _SlicePivot ("Slice pivot (carried by the slice entity, unused here)", Vector) = (0, 0, 0, 0)
        _SliceAxis ("Slice axis (carried by the slice entity, unused here)", Vector) = (0, 0, 0, 0)
        _SliceDrift ("Flight velocity, world (xyz) + explosion speed (w)", Vector) = (0, 0, 0, 0)

        [Header(Shape (written by PrismCollisionBurst from PrismMotionConfig at runtime))]
        _BurstShape ("Spread (prism radii), opening tau (s), spin (rad per unit speed), heat (s)", Vector) = (0.55, 0.22, 0.3, 0.35)
        _BurstCurl ("Bloom turns, unravel turns, ribbons per face, shards per face edge", Vector) = (0.7, 0.8, 5, 3)
        _BurstFade ("Dissolve grain, ember band, fracture line width (face fraction), glint", Vector) = (0.35, 0.05, 0.035, 0.6)

        [Header(Look)]
        _FresnelPower ("Skin Fresnel Power", Range(0.5, 8)) = 3
        [HDR] _HotColor ("Hot Colour (fracture flash, glints, ember)", Color) = (2.2, 2.2, 2.4, 1)
        _InnerGlow ("Inside of the cube, seen as it opens (x domain bright)", Range(0, 3)) = 1.0
        _DissolveNoiseScale ("Dissolve Noise Cell (world units)", Range(0.05, 10)) = 0.55
        _EmberGlow ("Ember Brightness (x domain bright)", Range(0, 6)) = 2.0
        _EmberWhite ("Ember Whiteness (toward the hot colour)", Range(0, 1)) = 0.5
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
            float4 _BurstShape;
            float4 _BurstCurl;
            float4 _BurstFade;
            float _FresnelPower;
            float4 _HotColor;
            float _InnerGlow;
            float _DissolveNoiseScale;
            float _EmberGlow;
            float _EmberWhite;
        CBUFFER_END

        // The per-instance stamps. Sizes match the MaterialProperty components exactly
        // (PrismRenderProperties.cs, the Prism Slice set): colours float4, spread float3, every
        // slice stamp float4.
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
            #define PRISM_BURST_PROP(type, name) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, name)
        #else
            #define PRISM_BURST_PROP(type, name) name
        #endif

        // The platform clock every prism stamp is written against (PrismClock.cs). NEVER _Time.
        float _PrismClock;

        // The occlusion corridor's globals — the platform law every prism and every debris chunk
        // obeys (Docs/PRISM_ANIMATION.md §4.7). A burst is born where a prism was rammed or thrown,
        // often beside the pilot's own hull, so it obeys it through the DEBRIS entry point.
        float4 _PrismOcclusionTarget;
        float4 _PrismOcclusionParams;

        #include "PrismCollisionBurst.hlsl"
        #include "PrismOcclusionCorridor.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;       // the face's +u axis (HighPolyPrismMesh)
            float2 faceUV : TEXCOORD0;        // the vertex's face coordinates, [0, 1]
            float3 tri : TEXCOORD2;           // its triangle's centroid in face coords (xy), a per-triangle random (z)
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;       // the piece's normal NOW (rotated / bent with it)
            float3 positionWS : TEXCOORD1;     // where the fragment is NOW
            float2 faceUV : TEXCOORD2;         // its REST face coordinates — the fracture lines live here
            float3 noisePos : TEXCOORD3;       // world-scaled rest position, in noise cells
            nointerpolation float4 bright : TEXCOORD4;
            nointerpolation float4 dark : TEXCOORD5;
            nointerpolation float4 phase : TEXCOORD6;   // x progress, y heat (1 fresh .. 0 cool), z kind, w piece
            nointerpolation float4 face : TEXCOORD7;    // xyz face key, w age
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings BurstVert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float4 timing = PRISM_BURST_PROP(float4, _SliceTiming);
            float4 drift = PRISM_BURST_PROP(float4, _SliceDrift);
            float age = max(_PrismClock - timing.x, 0.0);

            // The vertex's frame, in WORLD space: rigid pieces must be rigid under the prism's
            // non-uniform scale, so every rotation and bend below happens after the scale.
            float3x3 model = (float3x3)GetObjectToWorldMatrix();
            float3 uOS = input.tangentOS.xyz;
            float3 vOS = cross(input.normalOS, uOS);
            PrismBurstFrame f;
            f.rest = TransformObjectToWorld(input.positionOS.xyz);
            f.normal = TransformObjectToWorldNormal(input.normalOS);
            f.centre = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
            f.faceCentre = TransformObjectToWorld(input.normalOS * 0.5);
            f.faceU = mul(model, uOS * 0.5);
            f.faceV = mul(model, vOS * 0.5);
            f.triUV = input.tri.xy * 2.0 - float2(1.0, 1.0);
            f.faceKey = input.normalOS * 3.7 + timing.z * 11.0;
            f.radius = length(mul(model, float3(0.5, 0.5, 0.5)));

            PrismBurstParams p;
            p.age = age;
            p.speed = drift.w;
            p.seed = timing.z;
            p.spread = _BurstShape.x;
            p.tau = _BurstShape.y;
            p.spin = _BurstShape.z;
            p.bloomTurns = _BurstCurl.x;
            p.unravelTurns = _BurstCurl.y;
            p.strips = _BurstCurl.z;
            p.cells = _BurstCurl.w;

            float3 posWS, nrmWS;
            float piece;
            PrismBurstPose(timing.w, f, p, posWS, nrmWS, piece);

            // The flight: the explosion's own, linear and undamped, so the cloud's centre goes
            // exactly where the ordinary debris would have gone.
            posWS += drift.xyz * age;

            output.positionCS = TransformWorldToHClip(posWS);
            output.normalWS = nrmWS;
            output.positionWS = posWS;
            output.faceUV = input.faceUV;
            float3 scaledOS = mul(model, input.positionOS.xyz);
            output.noisePos = scaledOS / max(_DissolveNoiseScale, 1e-3) + float3(17.13, 31.71, 7.37) * timing.z;
            output.bright = PRISM_BURST_PROP(float4, _BrightColor);
            output.dark = PRISM_BURST_PROP(float4, _DarkColor);
            float heat = 1.0 - smoothstep(0.0, max(_BurstShape.w, 1e-3), age);
            output.phase = float4(PrismBurstProgress(age, timing.y), heat, floor(timing.w + 0.5), piece);
            output.face = float4(f.faceKey, age);
            return output;
        }

        // The shared clip, applied identically by every pass so depth and colour agree on the
        // silhouette. Returns the fragment's dissolve value for the colour pass's ember band.
        float BurstClip(Varyings input)
        {
            float progress = input.phase.x;
            // Glitter shards blink out WHOLE; the other kinds erode raggedly within a piece.
            float grain = input.phase.z == PRISM_BURST_GLITTER ? 0.0 : _BurstFade.x;
            float value = PrismBurstDissolveValue(input.phase.w, PrismBurstNoise(input.noisePos), grain);
            clip(PrismBurstClipped(value, progress) ? -1.0 : 1.0);

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
            #pragma vertex BurstVert
            #pragma fragment BurstFrag
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            half4 BurstFrag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float value = BurstClip(input);
                float progress = input.phase.x;
                float heat = input.phase.y;
                float kind = input.phase.z;
                float3 bright = input.bright.rgb;
                float3 V = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                bool front = IS_FRONT_VFACE(face, true, false);

                // The skin: the prism's own look, the domain base brightening to the rim at
                // grazing angles — the same colour budget the explosion spends.
                float3 N = SafeNormalize(front ? input.normalWS : -input.normalWS);
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float3 color = lerp(input.dark.rgb, bright, fres);

                // The inside of the cube, on show once a petal curls or a ribbon lifts.
                if (!front && (kind == PRISM_BURST_BLOOM || kind == PRISM_BURST_UNRAVEL))
                    color = lerp(color, bright * _InnerGlow, 0.6);

                // SHATTER: the fracture lines flash white-hot along the shard borders and cool.
                if (kind == PRISM_BURST_SHATTER && heat > 0.0)
                {
                    float2 seed;
                    float border;
                    PrismBurstVoronoi(input.faceUV, input.face.xyz, _BurstCurl.w, seed, border);
                    float crack = exp(-max(border, 0.0) / max(_BurstFade.z, 1e-4)) * heat;
                    color = lerp(color, _HotColor.rgb, saturate(crack));
                }

                // GLITTER: every shard catches the light now and then — a brief, sparse glint.
                if (kind == PRISM_BURST_GLITTER)
                {
                    float phase = frac(input.phase.w * 7.31 + input.face.w * 0.9);
                    float glint = saturate(1.0 - abs(phase - 0.5) * 10.0) * _BurstFade.w;
                    color = lerp(color, _HotColor.rgb, glint);
                }

                // The ember band just behind the dissolve front.
                if (progress > 0.0)
                {
                    float ember = 1.0 - saturate((value - progress) / max(_BurstFade.y, 1e-4));
                    float3 emberColor = lerp(bright * _EmberGlow, _HotColor.rgb, _EmberWhite);
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
            #pragma vertex BurstVert
            #pragma fragment BurstDepthFrag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            half BurstDepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                BurstClip(input);
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
            #pragma vertex BurstVert
            #pragma fragment BurstDepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            half4 BurstDepthNormalsFrag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                BurstClip(input);

                bool front = IS_FRONT_VFACE(face, true, false);
                float3 normalWS = SafeNormalize(front ? input.normalWS : -input.normalWS);

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
