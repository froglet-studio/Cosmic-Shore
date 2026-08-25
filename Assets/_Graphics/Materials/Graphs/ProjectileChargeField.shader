Shader "Shader Graphs/ProjectileChargeField"
{
    Properties
    {
        // NEUTRAL is blue, DANGER is red. _CrackleColorA is EnvironmentColors.Danger
        // from OriginalColorSetSO, verbatim — the shared, domain-independent danger
        // colour, so a round's hot core is the same red the arena's danger mass wears.
        [Header(Crackle Colors   neutral blue plus danger red)]
        _CrackleColorA ("Core Arc Color", Color) = (1.4979111, 0.0058463, 0.0068495, 1.0)
        _CrackleColorB ("Outer Glow Color", Color) = (0.10, 0.35, 1.0, 1.0)
        [Header(Arc Pattern)]
        _ArcSeeds ("Discharge Points", Range(1, 6)) = 3
        _ArcDensity ("Arc Count", Range(1, 6)) = 5
        _ArcSharpness ("Arc Width", Range(0.01, 0.5)) = 0.12
        _ArcIntensity ("Arc Intensity", Range(0, 1)) = 1
        _ArcReach ("Arc Reach", Range(0.1, 1)) = 1
        _CoreThreshold ("Danger Core Threshold", Range(0, 0.99)) = 0.75
        [Header(Wave and Expansion)]
        _RingThickness ("Ring Thickness", Range(0.05, 1)) = 0.9
        _CenterFillAmount ("Center Fill", Range(0, 1)) = 0.12
        _RippleSpeed ("Ripple Speed", Range(0.2, 3)) = 1.6
        _CrackleRate ("Discharges Per Second", Range(0.1, 12)) = 6
        [Header(Fresnel Rim)]
        _FresnelRimColor ("Rim Color", Color) = (0.25, 0.55, 1.0, 1.0)
        _FresnelRimIntensity ("Rim Intensity", Range(0, 1)) = 0.18
        _FresnelRimPower ("Rim Power", Range(1, 8)) = 2.5

        [Header(Charge)]
        // The shell reads its OWN world radius, so growth needs no per-instance CPU write.
        // Absolute and fleet-wide, exactly like the speed tunnel's mapping: the same hit
        // radius looks the same on any round, and a Mass 10 shot reaches deeper because it
        // IS bigger. 4.95 is the Mass 10 end-of-flight hit radius.
        _ChargeReferenceRadius ("Fully Charged Radius", Float) = 4.95
        // What an un-grown round gets. Not zero: a round that has just left the muzzle must
        // still show the volume it deletes.
        _ChargeFloor ("Uncharged Floor", Range(0, 1)) = 0.35
        // How strongly the shell's own radius offsets its animation phase — the thing that
        // stops a whole volley discharging in unison. Radians per world unit.
        _PhaseByRadius ("Phase Offset Per Radius", Float) = 1.7
        _PhaseSpeed ("Phase Speed", Float) = 1
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
                float  _ArcSeeds;
                float  _ArcDensity;
                float  _ArcSharpness;
                float  _ArcIntensity;
                float  _ArcReach;
                float  _CoreThreshold;
                float  _RingThickness;
                float  _CenterFillAmount;
                float  _RippleSpeed;
                float  _CrackleRate;
                float  _FresnelRimIntensity;
                float  _FresnelRimPower;
                float  _ChargeReferenceRadius;
                float  _ChargeFloor;
                float  _PhaseByRadius;
                float  _PhaseSpeed;
            CBUFFER_END

            #include "Assets/_Graphics/Materials/Graphs/ProjectileChargeField.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS   : TEXCOORD1;
                float3 viewDirOS  : TEXCOORD2;
                float2 charge     : TEXCOORD3;   // x = phase (seconds), y = charge 0..1
                float  fogFactor  : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalOS   = input.normalOS;
                output.fogFactor  = ComputeFogFactor(output.positionCS.z);

                float3 cameraPosOS = TransformWorldToObject(GetCameraPositionWS());
                output.viewDirOS = cameraPosOS - input.positionOS.xyz;

                // The shell's own world radius IS the round's hit radius — the CPU sizes it
                // to exactly that every frame (Projectile.SizeHitVolumeField). Reading it back
                // off the model matrix is what lets growth drive the visual with no stamp.
                // The mesh is Unity's built-in sphere, object radius 0.5.
                float4x4 m = GetObjectToWorldMatrix();
                float worldRadius = 0.5 * length(float3(m[0][0], m[1][0], m[2][0]));

                output.charge.x = _Time.y * _PhaseSpeed + worldRadius * _PhaseByRadius;
                output.charge.y = saturate(worldRadius / max(_ChargeReferenceRadius, 1e-3));

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 emissionColor;
                float alpha;
                ProjectileChargeField_float(
                    input.positionOS, input.normalOS, input.viewDirOS,
                    input.charge.x, input.charge.y,
                    emissionColor, alpha);

                emissionColor = MixFog(emissionColor, input.fogFactor);
                return half4(emissionColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
