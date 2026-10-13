// StoatRipple.shader — the Stoat's annihilation ripple (R_VesselActions/STOAT_DIPOLE.md §1c), a spherical
// space-time wave that bends the scene as its front passes. StoatRipple.cs drives it; StoatRippleExecutor.cs is
// what the front DOES (strips rivals, kills creatures) — this file is only the picture.
//
// HOW IT DRAWS. The black-hole lens's own trick (BlackHoleLens.shader): one back-faced sphere circumscribing the
// front, drawn by BlackHoleLensPass AFTER the transparents from its copy of the camera colour
// (_BlackHoleSceneColor), with LightMode = BlackHoleLens so URP's own passes never draw it. Per pixel: the view
// ray's closest approach b to the ripple's centre. A shell of radius R projects to the ring b = R; within a band
// either side of it the ray is bent toward the centre (outside the front) and away from it (inside) by a
// derivative-of-Gaussian profile, so the scene behind is pushed out ahead of the wave and pulled in behind it — a
// travelling lens, not a painted ring. The red and blue channels bend a little more and less than green (the
// fringe), and a thin rim at the front carries the owner's domain colour (pre-multiplied by the C# side's glow
// and fade). Everything outside the band is discarded, so the pass leaves the rest of the screen untouched.
//
// LIMITS, STATED. No depth test: an opaque in front of the wave inside its band is bent with the background (the
// wave is ~1 s long). A camera INSIDE the front sees nothing — the wave has already passed it.
Shader "CosmicShore/StoatRipple"
{
    Properties
    {
        [Header(Written per ripple by StoatRipple.cs through a MaterialPropertyBlock)]
        _RippleRadius ("Front radius (world units)", Float) = 10
        _RippleBand ("Band half-width (world units)", Float) = 2
        _RippleBend ("Bend at the band's peak (radians, fades with age)", Float) = 0.025
        _RippleSplit ("Red / blue split of the bend (0 = none)", Float) = 0.12
        _RippleTint ("Rim colour (HDR, pre-multiplied by glow and fade); a = strength", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+60"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "StoatRipple"
            // Drawn only by BlackHoleLensPass (after the transparents), never by URP's own passes.
            Tags { "LightMode" = "BlackHoleLens" }

            ZWrite Off
            ZTest Always
            Cull Front
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StoatRippleVert
            #pragma fragment StoatRippleFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _RippleRadius;
                float _RippleBand;
                float _RippleBend;
                float _RippleSplit;
                float4 _RippleTint;
            CBUFFER_END

            TEXTURE2D(_BlackHoleSceneColor);

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings StoatRippleVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
                // Pinned just inside the far plane (the lens's rule): a wave wider than the far distance is never cut.
            #if UNITY_REVERSED_Z
                positionCS.z = positionCS.w * 1e-5;
            #else
                positionCS.z = positionCS.w * (1.0 - 1e-5);
            #endif
                output.positionWS = positionWS;
                output.positionCS = positionCS;
                return output;
            }

            // Screen uv of a world DIRECTION from the camera; onScreen fades over the outer 4% so a bent ray that
            // leaves the screen does not tear.
            float2 StoatRippleDirToUV(float3 dir, out float onScreen)
            {
                float4 clip = mul(UNITY_MATRIX_VP, float4(dir, 0.0));
                if (!(clip.w > 1e-5))
                {
                    onScreen = 0.0;
                    return float2(0.5, 0.5);
                }
                float4 screen = ComputeScreenPos(clip);
                float2 uv = screen.xy / screen.w;
                float2 edge = min(uv, 1.0 - uv);
                onScreen = saturate(min(edge.x, edge.y) / 0.04);
                return uv;
            }

            half4 StoatRippleFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radius = _RippleRadius;
                float band = max(_RippleBand, 1e-3);
                if (!(radius > 1e-3) || _RippleTint.a <= 0.0) discard;

                float3 centre = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 eye = _WorldSpaceCameraPos;
                float3 d = normalize(input.positionWS - eye);
                float3 toCentre = centre - eye;
                float along = dot(toCentre, d);
                // Inside the front: the wave has passed the camera. Behind the camera: nothing to see.
                if (length(toCentre) < radius - band || along <= 0.0) discard;
                float3 perp = toCentre - along * d;          // from the ray's closest point toward the centre
                float b = length(perp);
                float s = (b - radius) / band;               // -1 .. 1 across the band; 0 on the front
                if (abs(s) >= 1.0) discard;
                float3 inward = b > 1e-4 ? perp / b : float3(0.0, 0.0, 0.0);

                // Derivative of a Gaussian, peak 1 at s = ±0.5, zero at the front and at the band's edges: outside the
                // front the ray bends toward the centre (the scene is pushed out), inside it bends away (pulled in).
                float profile = s * exp(0.5 - 2.0 * s * s) * (1.0 - s * s);
                float bend = _RippleBend * profile;

                float onR, onG, onB;
                float2 uvR = StoatRippleDirToUV(normalize(d + inward * bend * (1.0 + _RippleSplit)), onR);
                float2 uvG = StoatRippleDirToUV(normalize(d + inward * bend), onG);
                float2 uvB = StoatRippleDirToUV(normalize(d + inward * bend * (1.0 - _RippleSplit)), onB);
                float2 pixelUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 straight = SAMPLE_TEXTURE2D_LOD(_BlackHoleSceneColor, sampler_LinearClamp, pixelUV, 0).rgb;
                float3 bent;
                bent.r = SAMPLE_TEXTURE2D_LOD(_BlackHoleSceneColor, sampler_LinearClamp, uvR, 0).r;
                bent.g = SAMPLE_TEXTURE2D_LOD(_BlackHoleSceneColor, sampler_LinearClamp, uvG, 0).g;
                bent.b = SAMPLE_TEXTURE2D_LOD(_BlackHoleSceneColor, sampler_LinearClamp, uvB, 0).b;
                float3 scene = lerp(straight, bent, min(onR, min(onG, onB)));

                // A thin rim at the front, in the owner's domain colour.
                float rim = exp(-12.0 * s * s);
                return half4(scene + _RippleTint.rgb * rim, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
