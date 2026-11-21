// CloakGhost.shader — the Serpent's cloaked hull as its own pilot sees it, and the glow it comes
// back through when the cloak ends (R_VesselActions/SERPENT_SEED_WALL.md, "The deception it serves").
//
// PURPOSE. The pilot of a cloaked Serpent must still see their ship, and must see at a glance that
// it is hidden. The first cut drew the hull in a transparent Lit material at 30% alpha: a dark blue
// glass blob that read as "broken material", not as "cloaked" (Garrett, 2026-10-13: too dark, and
// it came back in a sharp transition). A cloak should read as light: an outline of the hull in the
// pilot's domain colour, almost empty in the middle, with a slow shimmer running down it.
//
// WHY A HAND-WRITTEN .shader. Two render states carry the look — additive blending and no depth
// write — and it is ~40 lines; the same reasoning as EchoSightHalo.shader.
//
// THE RENDER STATE.
//   Blend One One — additive. A ghost can only ADD light, so it can never darken what is behind it,
//                   which is exactly the fault it replaces, and it needs no sort order.
//   ZWrite Off    — the ghost writes no depth, so it never occludes the world it floats in.
//   Cull Back     — only the faces toward the camera, so the hull's far side does not double the
//                   rim through the middle.
//
// THE TERMS (all multiplied by _Fade, the whole effect's opacity):
//   rim     — Fresnel, pow(1 - |N.V|, _RimPower): bright at the silhouette, nothing face-on. Its
//             colour runs from the domain colour toward white at the very edge (_RimWhiten), which
//             is what makes it read as light rather than as paint.
//   fill    — a faint flat wash of the domain colour (_Fill) so the body is not completely empty.
//   shimmer — soft bands that travel nose to tail along the hull's own forward axis, measured in
//             WORLD units from the hull's origin, so they keep their spacing whatever the mesh's
//             import scale and do not crawl as the ship flies (a world-anchored pattern would
//             strobe at speed).
//   flare   — _Flare adds a solid glow of the whole body. The executor spikes it at the moments
//             the hull changes material (into the cloak, back out of it), so the swap happens
//             under a flash instead of as a cut.
//
// COST. Unlit, no texture fetch, ~20 ALU per fragment, drawn only while a Serpent is cloaked or for
// the half-second it reappears.

Shader "CosmicShore/CloakGhost"
{
    Properties
    {
        [HDR] _GhostColor ("Ghost Colour (domain colour, set at runtime)", Color) = (0.55, 0.85, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimGain ("Rim Gain", Float) = 1.6
        _RimWhiten ("Rim Whiten", Range(0, 1)) = 0.55
        _Fill ("Interior Fill", Range(0, 1)) = 0.06
        _BandSpacing ("Shimmer Band Spacing (world units)", Float) = 1.5
        _BandSpeed ("Shimmer Band Speed (world units / s)", Float) = 2.5
        _BandGain ("Shimmer Band Depth", Range(0, 1)) = 0.4
        _Fade ("Fade", Range(0, 1)) = 1
        _Flare ("Flare", Range(0, 4)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "CloakGhost"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GhostColor;
                float  _RimPower;
                float  _RimGain;
                float  _RimWhiten;
                float  _Fill;
                float  _BandSpacing;
                float  _BandSpeed;
                float  _BandGain;
                float  _Fade;
                float  _Flare;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1.0 - saturate(abs(dot(n, v))), _RimPower);

                // Distance along the hull's own forward from its origin, in world units.
                float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 forwardWS = TransformObjectToWorldDir(float3(0.0, 0.0, 1.0));
                float along = dot(input.positionWS - originWS, forwardWS);
                float spacing = max(_BandSpacing, 1e-3);
                float band = 0.5 + 0.5 * sin((along + _Time.y * _BandSpeed) / spacing * 6.2831853);
                float shimmer = lerp(1.0 - _BandGain, 1.0, band);

                float3 rimColour = lerp(_GhostColor.rgb, float3(1.0, 1.0, 1.0), _RimWhiten * rim);
                float3 ghost = (rimColour * (rim * _RimGain) + _GhostColor.rgb * _Fill) * shimmer;
                float3 flare = _GhostColor.rgb * _Flare * (0.3 + rim);

                return half4((ghost + flare) * _Fade, 1.0);
            }
            ENDHLSL
        }
    }
}
