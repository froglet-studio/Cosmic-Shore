Shader "Shader Graphs/ProjectileChargeField"
{
    Properties
    {
        // NEUTRAL is blue, DANGER is red. _CrackleColorA is EnvironmentColors.Danger
        // from OriginalColorSetSO, verbatim — the shared, domain-independent danger
        // colour, so a round's hot core is the same red the arena's danger mass wears.
        [Header(Arc Colors   neutral blue plus danger red)]
        _CrackleColorA ("Core Arc Color", Color) = (1.4979111, 0.0058463, 0.0068495, 1.0)
        _CrackleColorB ("Outer Glow Color", Color) = (0.10, 0.35, 1.0, 1.0)

        // ONE ROUND IS ONE STROKE. Every knob here is bounded by that: the shell draws a
        // single wobbling bolt on a single randomly-oriented great circle, and the SPHERE
        // is assembled by the player out of a burst's worth of them. Raising _ArcCount
        // past 1, or the rim past a whisper, hands the sphere back to one round.
        [Header(The Arc)]
        _ArcCount ("Simultaneous Arcs", Range(1, 4)) = 1
        _ArcSpan ("Arc Length (radians)", Range(0.2, 6.283)) = 5
        // How far the circle's pole may lean toward the camera. 0 = the circle passes through
        // the disc's centre (a straight slash); larger = an offset, more curved chord. It must
        // stay well under pi/2 or the stroke retreats to the limb and the round reads as a plain
        // disc — the failure this whole pass exists to fix.
        _ArcTiltRange ("Arc Tilt Range (radians)", Range(0, 1.2)) = 0.55
        // How far the stroke's centre may wander from the camera-facing point of the circle.
        _ArcStartSpread ("Arc Centre Spread (radians)", Range(0, 3.14)) = 1.2
        _ArcSharpness ("Arc Width", Range(0.005, 0.25)) = 0.075
        _ArcWander ("Bolt Wander Off Circle", Range(0, 0.5)) = 0.26
        _ArcWanderScale ("Bolt Wander Frequency", Range(0.2, 8)) = 2.6
        _ArcIntensity ("Arc Intensity", Range(0, 4)) = 2.4
        _TipGlow ("Striking Tip Glow", Range(0, 2)) = 0.9
        _CoreThreshold ("Danger Core Threshold", Range(0, 0.99)) = 0.7

        [Header(Discharge Timing)]
        // Discharges per unit of Phase. Phase carries BOTH time and the round's own world
        // radius, so this scales the per-round stroke rate AND how quickly consecutive
        // rounds in a stream land on different great circles — the two cannot be tuned
        // apart, because a round's radius is simultaneously its identity and its progress.
        _CrackleRate ("Discharges Per Phase Unit", Range(0.1, 12)) = 3.5
        _StrikeTime ("Strike Time (fraction of cycle)", Range(0.02, 0.9)) = 0.25
        _HoldTime ("Hold Until (fraction of cycle)", Range(0.05, 0.95)) = 0.5
        _FadeShape ("Fade Falloff", Range(0.3, 4)) = 1

        [Header(Fresnel Rim)]
        // A whisper, on purpose: enough that a round is never fully dark between strokes
        // (continuity of existence), nowhere near enough to read as a sphere.
        _FresnelRimColor ("Rim Color", Color) = (0.25, 0.55, 1.0, 1.0)
        _FresnelRimIntensity ("Rim Intensity", Range(0, 1)) = 0.05
        _FresnelRimPower ("Rim Power", Range(1, 8)) = 3.5

        [Header(Charge)]
        // The shell reads its OWN world radius, so growth needs no per-instance CPU write.
        // Absolute and fleet-wide, exactly like the speed tunnel's mapping: the same hit
        // radius looks the same on any round, and a Mass 10 shot reaches deeper because it
        // IS bigger. 4.95 is the Mass 10 end-of-flight hit radius.
        _ChargeReferenceRadius ("Fully Charged Radius", Float) = 4.95
        // What an un-grown round gets. Not zero: a round that has just left the muzzle must
        // still show the volume it deletes.
        _ChargeFloor ("Uncharged Floor", Range(0, 1)) = 0.4
        // How strongly the shell's own radius offsets its animation phase — the thing that
        // stops a whole volley discharging in unison, and the thing that puts consecutive
        // rounds on different great circles. Radians per world unit.
        _PhaseByRadius ("Phase Offset Per Radius", Float) = 0.43
        _PhaseSpeed ("Phase Speed", Float) = 2.6
        // A round's identity is an explicit per-instance SEED (see the include). These say what
        // the seed CHANGES: the circle's angle around the view axis, its tilt, the bolt's
        // jaggedness, and where the round sits in its own discharge cycle.
        _SeedSpin ("Seed -> Circle Angle", Float) = 6.283
        _SeedTilt ("Seed -> Circle Tilt", Float) = 6.283
        _SeedWobble ("Seed -> Bolt Shape", Float) = 40
        _PhaseBySeed ("Seed -> Phase Offset", Float) = 1
        // Written per SHOT by Projectile.StampChargeFieldSeed through a MaterialPropertyBlock,
        // and read out of the GPU-instancing buffer. The material default only matters for a
        // renderer nothing ever stamps, which would draw every round alike.
        [PerRendererData] _RoundSeed ("Round Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ProjectileChargeField"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One          // Additive: it can only ADD light, so it never darkens
                                   // what it covers and needs no sort order against itself.
            ZWrite Off             // Never occludes the world.
            ZTest LEqual           // But the world still occludes it.
            Cull Back              // Front faces only. The donor skimmer runs Cull Off so it
                                   // reads from inside; a pilot is never inside their own
                                   // round, and a single shell halves the overdraw of ~54
                                   // simultaneous transparent spheres AND keeps the read
                                   // sparse enough to see the arena through.

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // SRP Batcher compatibility: every material property lives in UnityPerMaterial,
            // and nothing here is per-instance, so every round in the match draws from one
            // material with no property block. This is what makes the effect affordable at
            // 180 rounds/s per Sparrow.
            CBUFFER_START(UnityPerMaterial)
                float4 _CrackleColorA;
                float4 _CrackleColorB;
                float4 _FresnelRimColor;
                float  _ArcCount;
                float  _ArcSpan;
                float  _ArcTiltRange;
                float  _ArcStartSpread;
                float  _ArcSharpness;
                float  _ArcWander;
                float  _ArcWanderScale;
                float  _ArcIntensity;
                float  _TipGlow;
                float  _CoreThreshold;
                float  _CrackleRate;
                float  _StrikeTime;
                float  _HoldTime;
                float  _FadeShape;
                float  _FresnelRimIntensity;
                float  _FresnelRimPower;
                float  _ChargeReferenceRadius;
                float  _ChargeFloor;
                float  _PhaseByRadius;
                float  _PhaseSpeed;
                float  _SeedSpin;
                float  _SeedTilt;
                float  _SeedWobble;
                float  _PhaseBySeed;
            CBUFFER_END

            // The ONE per-instance value. Declared outside UnityPerMaterial on purpose: this
            // material is GPU-instanced rather than SRP-batched, because ~54 identical spheres
            // that must all look DIFFERENT need per-instance data more than they need one
            // uniform buffer. Cost is one SetPropertyBlock per shot, never per frame.
            UNITY_INSTANCING_BUFFER_START(PCFPerRound)
                UNITY_DEFINE_INSTANCED_PROP(float, _RoundSeed)
            UNITY_INSTANCING_BUFFER_END(PCFPerRound)

            #include "Assets/_Graphics/Materials/Graphs/ProjectileChargeField.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS   : TEXCOORD1;
                float3 viewDirOS  : TEXCOORD2;
                float3 charge     : TEXCOORD3;   // phase (seconds), charge 0..1, round seed
                float3 viewAxisOS : TEXCOORD5;   // object-space camera direction, per OBJECT
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalOS   = input.normalOS;
                output.fogFactor  = ComputeFogFactor(output.positionCS.z);

                float3 cameraPosOS = TransformWorldToObject(GetCameraPositionWS());
                output.viewDirOS = cameraPosOS - input.positionOS.xyz;

                // Every per-round difference — which great circle this round's stroke lands
                // on, and how charged it reads — comes out of the shell's own object-to-world
                // matrix and nothing else: no stamp, no property block, one batch for the
                // whole match. The resolution itself lives in the include so the verification
                // harness can compile and run the SHIPPED version of it.
                // (Column 0's LENGTH is the shell's diameter, and the CPU sizes the shell to
                // exactly the round's hit radius every frame — Projectile.SizeChargeField —
                // so growth drives the visual for free. Mesh is Unity's built-in sphere.)
                float4x4 m = GetObjectToWorldMatrix();
                output.charge.z = UNITY_ACCESS_INSTANCED_PROP(PCFPerRound, _RoundSeed);
                ProjectileChargeFieldPhase_float(
                    float3(m[0][0], m[1][0], m[2][0]),
                    output.charge.z,
                    _Time.y,
                    output.charge.x, output.charge.y);

                // Per-OBJECT view axis (camera -> the round's centre), NOT the per-fragment view
                // direction: the circle's orientation has to be one answer for the whole shell.
                output.viewAxisOS = cameraPosOS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 emissionColor;
                float alpha;
                ProjectileChargeField_float(
                    input.positionOS, input.normalOS, input.viewDirOS,
                    input.charge.x, input.charge.y, input.charge.z, input.viewAxisOS,
                    emissionColor, alpha);

                emissionColor = MixFog(emissionColor, input.fogFactor);
                return half4(emissionColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
