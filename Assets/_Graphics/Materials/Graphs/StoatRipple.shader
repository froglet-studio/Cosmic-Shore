// StoatRipple.shader — the Stoat's annihilation ripple (R_VesselActions/STOAT_DIPOLE.md §1c "The drawn wave"): a
// space-time ripple with NO colour of its own. StoatRipple.cs keeps the live ripples, StoatRipplePass.cs draws them;
// StoatRippleExecutor.cs is what the front DOES (strips rivals, kills creatures) — this file is only the picture.
//
// HOW IT DRAWS. One full-screen triangle after the transparents, from StoatRipplePass's copy of the camera colour
// (_StoatRippleSceneColor). Screen positions are in the black-hole lens's units (screen heights from the centre, y up;
// BlackHoleLens.ScreenWell): a spherical front of radius R seen from distance D projects to a circle of screen radius
// rho around its centre. Per pixel, u = how many wavelengths the pixel sits BEHIND the front (inside the circle); the
// wave's profile (StoatRippleProfile, = StoatRippleMath.Profile) is a sine that rises sharply at the front and rings
// down over about three trailing crests. The pixel samples the scene displaced along the radius by the bend × the
// profile, and its own light is raised on a crest and lowered in a trough by the shimmer × the profile. The bend is in
// screen units already faded by age and by 1/r (StoatRippleMath.Amplitude). Nothing is ADDED: no rim, no tint, no
// glow. When the front runs through the camera itself the whole view swells and settles (_StoatRippleView.y).
// Pixels no wave reaches are discarded.
//
// LIMITS, STATED. No depth test (the URP asset keeps no depth texture): an opaque in front of the wave inside its band
// is bent with what is behind it. The front is a circle on screen, exact for a sphere seen from outside it.
Shader "CosmicShore/StoatRipple"
{
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
            // Drawn only by StoatRipplePass (after the transparents), never by URP's own passes.
            Tags { "LightMode" = "StoatRipple" }

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StoatRippleVert
            #pragma fragment StoatRippleFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Mirrors StoatRipple.MaxDrawn.
            #define STOAT_RIPPLE_MAX 4

            // Per camera, with the draw (StoatRipplePass → StoatRipple.ScreenRipples), nearest first:
            // C = (centre x, centre y, the front's screen radius, the wavelength on screen)
            // A = (the bend on screen, the shimmer, 1 = live, 0)
            float4 _StoatRippleC[STOAT_RIPPLE_MAX];
            float4 _StoatRippleA[STOAT_RIPPLE_MAX];
            float _StoatRippleCount;
            // x the camera's aspect, y the swell of a wave passing through the camera
            float4 _StoatRippleView;

            TEXTURE2D(_StoatRippleSceneColor);

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings StoatRippleVert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            // = StoatRippleMath.Profile: u wavelengths behind the front (negative ahead of it).
            float StoatRippleProfile(float u)
            {
                if (u < -1.5 || u > 4.0) return 0.0;
                float envelope = u < 0.0 ? exp(-6.0 * u * u) : exp(-0.45 * u * u);
                return sin(6.2831853 * u) * envelope;
            }

            half4 StoatRippleFrag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect = _StoatRippleView.x;
                float2 p = (uv - 0.5) * float2(aspect, 1.0);

                // The wave passing through the camera: the whole view swells out from its centre and settles.
                float2 disp = p * _StoatRippleView.y;
                float shimmer = 0.0;
                for (int k = 0; k < STOAT_RIPPLE_MAX; k++)
                {
                    if (k >= (int)_StoatRippleCount) break;
                    float4 c = _StoatRippleC[k];
                    float4 a = _StoatRippleA[k];
                    if (a.z < 0.5) continue;
                    float2 d = p - c.xy;
                    float r = length(d);
                    float profile = StoatRippleProfile((c.z - r) / max(c.w, 1e-4));
                    if (profile == 0.0 || r < 1e-5) continue;
                    disp += (d / r) * (a.x * profile);
                    shimmer += a.y * profile;
                }
                if (dot(disp, disp) < 1e-12 && abs(shimmer) < 1e-5) discard;

                float2 suv = (p + disp) / float2(aspect, 1.0) + 0.5;
                suv = 1.0 - abs(1.0 - abs(suv));   // mirrored at the edges, as the lens does
                float3 col = SAMPLE_TEXTURE2D_LOD(_StoatRippleSceneColor, sampler_LinearClamp, suv, 0).rgb;
                return half4(col * max(0.0, 1.0 + shimmer), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
