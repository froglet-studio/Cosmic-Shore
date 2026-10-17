// PrismCollisionBurst.shader — the prism's detail levels for DESTRUCTION (shatter, glitter) and
// STEALING (the bloom, unravel and flake sheds), Docs/PRISM_ANIMATION.md §4.12, Docs/PRISM_MOTION.md
// § "The look". Everything that decides WHERE a vertex goes and WHETHER a fragment survives lives in
// PrismCollisionBurst.hlsl, which Tools/Shaders/verify_prism_collision_burst.py compiles and EXECUTES;
// this file is the URP plumbing around it plus the colouring, and the verifier front-end compiles every
// pass of it with glslang against a mock of the URP library.
//
// It is the Rhino slice's sibling (PrismSlice.shader) and rides the slice's carrier unchanged: the
// pure render entities PrismCollisionBurst.cs spawns through PrismRenderService
// .SpawnSliceDebrisBatch, with the slice's per-instance stamp channels, five of which a burst reads:
//     _SliceTiming = (start, life, seed, kind)  — kind 0 shatter, 1 bloom, 2 unravel, 3 glitter, 4 flake,
//                                                 5 fragment (the destruction), 6 roll (the steal)
//     _SliceDrift  = (flight velocity, world xyz; explosion speed w) — zero for a shed
//     _SlicePlane  = (fade curve, -, -, -)      — 0 destruction (linear), 1 shed (the grade's smoothstep)
//     _SlicePivot  = (inside colour rgb, set w) — w 1: the inside wears this (a shed: the NEW colour);
//                                                 w 0: the domain's own bright
//     _SliceAxis   = (camera world position at the steal, shed subdivision) — the ROLL peels away from it
// Their Properties-block defaults are the UNSTAMPED state — age 0, zero flight — which renders the
// plain prism: an unstamped material is inert, never broken.
//
// No white accents (the designer, 2026-10-14): the contrast is the brighter INSIDE colour on the back
// faces, on show once the surface opens.
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
        _SlicePlane ("Fade curve (x: 0 destruction, 1 shed)", Vector) = (0, 0, 0, 0)
        _SliceCentre ("Slice centre (carried by the slice entity, unused here)", Vector) = (0, 0, 0, 1)
        _SlicePivot ("Inside colour (rgb), used when w is 1", Vector) = (0, 0, 0, 0)
        _SliceAxis ("Roll: the camera's world position at the steal (xyz), the shed mesh's subdivision (w)", Vector) = (0, 0, 0, 12)
        _SliceDrift ("Flight velocity, world (xyz) + explosion speed (w)", Vector) = (0, 0, 0, 0)

        [Header(Shape (written by PrismCollisionBurst from PrismMotionConfig at runtime))]
        _BurstShape ("Spread (prism radii), opening tau (s), spin (rad per unit speed), dissolve grain", Vector) = (0.55, 0.22, 0.3, 0.35)
        _BurstCurl ("Bloom turns, unravel turns, ribbons per face, shards per face edge", Vector) = (0.7, 0.8, 5, 3)
        _BurstPeel ("Bloom rings, bloom segments, peel noise", Vector) = (3, 3, 0.6, 0)
        _BurstRoll ("Roll inner radius, gap between turns (prism radii)", Vector) = (0.05, 0.01, 0, 0)

        [Header(The explosion tuning (copied at runtime from the explosion material))]
        _ExplosionDials ("Explosive rotation, explosive spread (per unit amount)", Vector) = (0.0169, 0.05, 0, 0)

        [Header(The destruction fragments (written by PrismCollisionBurst from PrismInteractionLodConfig))]
        _FragmentMotion ("Burst (prism radii), drift (radii per s), lift, aim", Vector) = (0.1, 0.06, 2, 0.95)
        _FragmentSize ("Size speed, size spin, spin cap (per size level), smallest level", Vector) = (0.8, 0.25, 1.6, 4)
        _FragmentFade ("Smallest gone at, largest gone at (share of life), shrink share, speed gain", Vector) = (0.45, 0.92, 0.35, 1.65)
        _FragmentNoise ("Noise: spin amplitude, frequency (per object unit), travel amplitude; speed reference (u/s)", Vector) = (0.15, 3, 0.48, 10)

        [Header(Look)]
        _FresnelPower ("Skin Fresnel Power", Range(0.5, 8)) = 4
        _InnerGlow ("Inside colour, seen as the surface opens (x the inside colour)", Range(0, 3)) = 1.2
        _DissolveNoiseScale ("Dissolve Noise Cell (world units)", Range(0.05, 10)) = 0.55
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
            float4 _BurstPeel;
            float4 _BurstRoll;
            float4 _ExplosionDials;
            float4 _FragmentMotion;
            float4 _FragmentSize;
            float4 _FragmentFade;
            float4 _FragmentNoise;
            float _FresnelPower;
            float _InnerGlow;
            float _DissolveNoiseScale;
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
            float3 tri : TEXCOORD2;           // its triangle's centroid in face coords (xy); z a random, or the Sierpinski level
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;       // the piece's normal NOW (rotated / bent with it)
            float3 positionWS : TEXCOORD1;     // where the fragment is NOW
            float3 noisePos : TEXCOORD2;       // world-scaled rest position, in noise cells
            nointerpolation float4 bright : TEXCOORD3;
            nointerpolation float4 dark : TEXCOORD4;
            nointerpolation float4 phase : TEXCOORD5;   // x progress, y curve, z kind, w piece
            nointerpolation float4 inside : TEXCOORD6;  // the colour the back faces wear
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
            float curve = PRISM_BURST_PROP(float4, _SlicePlane).x;
            float4 insideStamp = PRISM_BURST_PROP(float4, _SlicePivot);
            float4 rollStamp = PRISM_BURST_PROP(float4, _SliceAxis);
            float age = max(_PrismClock - timing.x, 0.0);
            // A roll leaves by being hidden behind its own prism; it only dissolves at the very end of its life.
            float progress = floor(timing.w + 0.5) == PRISM_BURST_ROLL
                ? PrismBurstRollFade(age, timing.y) : PrismBurstProgress(age, timing.y, curve);

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
            float3 halfX = mul(model, float3(0.5, 0.0, 0.0));
            float3 halfY = mul(model, float3(0.0, 0.5, 0.0));
            float3 halfZ = mul(model, float3(0.0, 0.0, 0.5));
            float3 longAxis = halfX;
            if (dot(halfY, halfY) > dot(longAxis, longAxis)) longAxis = halfY;
            if (dot(halfZ, halfZ) > dot(longAxis, longAxis)) longAxis = halfZ;
            f.longAxis = longAxis;

            PrismBurstParams p;
            p.rings = _BurstPeel.x;
            p.segments = _BurstPeel.y;
            p.noise = _BurstPeel.z;
            p.camera = rollStamp.xyz;
            p.life = timing.y;
            p.subdivision = rollStamp.w;
            p.rollRadius = _BurstRoll.x;
            p.rollGap = _BurstRoll.y;
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
            p.progress = progress;

            float3 posWS, nrmWS;
            float piece;
            float kind = floor(timing.w + 0.5);
            // The game's materials compile ONE kind (round 11, PrismCollisionBurst.ResolveRuntimeMaterial): the
            // destruction's (_BURST_FRAGMENT) or the steal's (_BURST_ROLL), so neither vertex program carries the
            // other's code or the five lab-only kinds (and their registers); no keyword is the lab's every-kind
            // program. The stamped kind still drives the clip and the fade, so a variant is exact for its kind.
#if defined(_BURST_ROLL)
            PrismBurstRoll(f, p, posWS, nrmWS);
            piece = PrismBurstHash(f.faceKey * 7.1 + p.seed * 3.3);
#else
#if !defined(_BURST_FRAGMENT)
            if (kind == PRISM_BURST_FRAGMENT)
#endif
            {
                // A fragment of the cut, in WORLD space: its own burst, the explosion's spin about cross(velocity, N)
                // at the explosion's rate (amount = the explosion's speed × t), its own end, the smallest first.
                float3 centroidOS = input.normalOS * 0.5 + uOS * (input.tri.x - 0.5) + vOS * (input.tri.y - 0.5);
                PrismBurstFragmentShape shape;
                shape.burst = _FragmentMotion.x;
                shape.drift = _FragmentMotion.y;
                shape.lift = _FragmentMotion.z;
                shape.aim = _FragmentMotion.w;
                shape.sizeSpeed = _FragmentSize.x;
                shape.sizeSpin = _FragmentSize.y;
                shape.spinCap = _FragmentSize.z;
                shape.levelRef = _FragmentSize.w;
                shape.endSmall = _FragmentFade.x;
                shape.endBig = _FragmentFade.y;
                shape.shrink = _FragmentFade.z;
                shape.tau = _BurstShape.y;
                shape.speedGain = _FragmentFade.w;
                shape.speedRef = _FragmentNoise.w;
                PrismBurstFragmentPose(f.rest, f.normal, TransformObjectToWorld(centroidOS), f.faceCentre, length(f.triUV), centroidOS,
                    input.tri.z, f.radius, age, timing.y, drift.w * age, drift.xyz, _ExplosionDials.x,
                    float4(_FragmentNoise.x, _FragmentNoise.y, timing.z, _FragmentNoise.z), shape, posWS, nrmWS);
                piece = 0.0;
            }
#if !defined(_BURST_FRAGMENT)
            else
            {
                PrismBurstPose(timing.w, f, p, posWS, nrmWS, piece);
            }
#endif
#endif

            // The flight: the explosion's own, linear and undamped, so the cloud's centre goes
            // exactly where the ordinary debris would have gone. A shed's is zero: the prism stays.
            posWS += drift.xyz * age;

            output.positionCS = TransformWorldToHClip(posWS);
            output.normalWS = nrmWS;
            output.positionWS = posWS;
            float3 scaledOS = mul(model, input.positionOS.xyz);
            output.noisePos = scaledOS / max(_DissolveNoiseScale, 1e-3) + float3(17.13, 31.71, 7.37) * timing.z;
            output.bright = PRISM_BURST_PROP(float4, _BrightColor);
            output.dark = PRISM_BURST_PROP(float4, _DarkColor);
            output.phase = float4(progress, curve, kind, piece);
            output.inside = insideStamp.w > 0.5 ? float4(insideStamp.rgb, 1.0) : output.bright;
            return output;
        }

        // The shared clip, applied identically by every pass so depth and colour agree on the
        // silhouette.
        void BurstClip(Varyings input)
        {
            float progress = input.phase.x;
            // A fragment leaves by shrinking away to its own centre (PrismBurstFragmentPose): no clip of its own.
#if !defined(_BURST_FRAGMENT)
            if (input.phase.z != PRISM_BURST_FRAGMENT)
            {
                // Glitter shards and flake scales go out WHOLE (a scale when the wave reaches it); the
                // other kinds erode raggedly within a piece.
                bool whole = input.phase.z == PRISM_BURST_GLITTER || input.phase.z == PRISM_BURST_FLAKE;
                float grain = whole ? 0.0 : _BurstShape.w;
                float value = PrismBurstDissolveValue(input.phase.w, PrismBurstNoise(input.noisePos), grain);
                clip(PrismBurstClipped(value, progress) ? -1.0 : 1.0);
            }
#endif

            float corridorAlpha, corridorThreshold;
            PrismOcclusionFadeDebris_float(input.positionWS, _PrismOcclusionTarget.xyz,
                _PrismOcclusionParams.xyz, 1.0, corridorAlpha, corridorThreshold);
            clip(corridorAlpha - corridorThreshold);
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
            #pragma multi_compile_local _ _BURST_FRAGMENT _BURST_ROLL
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            half4 BurstFrag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                BurstClip(input);
                float3 V = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                bool front = IS_FRONT_VFACE(face, true, false);

                // The skin: the prism's own look, the domain base brightening to the rim at
                // grazing angles — the same colour budget the explosion spends.
                float3 N = SafeNormalize(front ? input.normalWS : -input.normalWS);
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float3 color = lerp(input.dark.rgb, input.bright.rgb, fres);

                // The inside, on show once a shard turns, a petal curls, a ribbon lifts or a scale
                // tips: the brighter inside colour (a shed's is the NEW colour) — the only accent.
                if (!front)
                    color = lerp(color, input.inside.rgb * _InnerGlow, 0.6);

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
            #pragma multi_compile_local _ _BURST_FRAGMENT _BURST_ROLL
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
            #pragma multi_compile_local _ _BURST_FRAGMENT _BURST_ROLL
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
