// PrismOcclusionDitherPreview.shader — the Occlusion Dither Lab's viewport.
//
// This shader exists so the Lab's preview cannot LIE. It includes
// PrismOcclusionCorridor.hlsl and calls PrismOcclusionDitherThreshold — the same dispatch
// the prism graphs call, reading the same globals the Lab publishes — so what the window
// draws is the GPU code the game runs, not a CPU re-implementation of it that can drift.
// It also means adding a kernel to the corridor adds it to the Lab for free.
//
// It is a Hidden/ shader with no material asset: the Lab creates one at runtime with
// HideFlags.HideAndDontSave. Nothing here is prism mass, nothing here renders in a scene,
// and the corridor's own asset gates (which key off the wired prism GRAPH names and off
// prefabs carrying a Prism) do not and must not see it.
//
// MODES (_PreviewRect.z):
//   0  corridor cross-section — alpha follows the real radial profile, so the preview is a
//      slice through the actual cone the ship flies inside.
//   1  alpha ramp — a straight sweep across the band, which is where the unit shape reads.
//   2  RAW, for measurement — R = the kernel's threshold, G = the alpha at that pixel.
//      The Lab reads this back and computes |coverage − alpha| from real rendered output,
//      which is the same methodology as the in-situ numbers in the corridor's own comments.
Shader "Hidden/CosmicShore/PrismOcclusionDitherPreview"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PrismOcclusionCorridor.hlsl"

            float4 _PreviewRect;     // (width px, height px, mode, time seconds)
            float4 _PreviewProfile;  // (innerRatio, outerRatio, coreAlpha, extent)
            float4 _PreviewRamp;     // (alphaLo, alphaHi, 0, 0)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                // Preview pixels ARE the kernel's pixels: the screen-anchored kernels are
                // shown at 1:1, which is the only honest scale for judging a dither.
                float2 pixel = IN.uv * _PreviewRect.xy;
                int mode = (int)_PreviewRect.z;
                float time = _PreviewRect.w;

                float alpha;
                float radialRatio;
                float angleTurns;

                if (mode == 1)
                {
                    alpha = lerp(_PreviewRamp.x, _PreviewRamp.y, IN.uv.x);
                    // The spiral is corridor-anchored, so on a ramp it is given the ramp's
                    // own axes rather than a fake cone — it reads as bands, which is what
                    // it is. Judge the spiral on the cross-section, not here.
                    radialRatio = IN.uv.x;
                    angleTurns = IN.uv.y;
                }
                else
                {
                    // The real profile: C2 smootherstep between the two cone radii, exactly
                    // as PrismOcclusionFade_float computes it, so the gradient band the
                    // pattern has to survive is the true one.
                    float2 centred = IN.uv - 0.5;
                    float r = length(centred) * 2.0 * _PreviewProfile.w;
                    float inner = _PreviewProfile.x;
                    float outer = _PreviewProfile.y;
                    float clear = 1.0 - PrismOcclusionSmootherStep(
                        (r - inner) / max(outer - inner, 1e-4));
                    alpha = lerp(1.0, _PreviewProfile.z, clear);
                    radialRatio = saturate(r / max(outer, 1e-4));
                    angleTurns = atan2(centred.y, centred.x) * (1.0 / 6.28318530718);
                }

                // polarValid = true: the preview synthesizes a valid corridor frame in
                // every mode, so the spiral never takes its out-of-corridor IGN fallback here.
                // Both volumetric kernels (SHATTER3D and SHARD3D) get positionWS =
                // (pixel, 0) at angularScale 1, so preview pixels ARE world units and
                // they render their z = 0 slice at 1:1 — the honest scale, same as
                // every screen kernel. That slice cannot see a glancing-plane flash.
                // viewDepth = 0: the preview is a flat slice with no depth, so SHATTER's
                // depth band-phase contributes nothing here. Like the depth parallax before
                // it, that dial can only be judged on real stacked mass in play mode.
                float threshold = PrismOcclusionDitherThreshold(pixel, radialRatio, angleTurns, true,
                                                               float3(pixel, 0.0), 1.0, 0.0, time);

                if (mode == 2)
                    return float4(threshold, alpha, 0.0, 1.0);

                // Same comparison the opaque prism materials make: URP discards a fragment
                // when alpha < AlphaClipThreshold, so a pixel survives at alpha >= threshold.
                float kept = alpha >= threshold ? 1.0 : 0.0;
                float shade = kept * 0.86 + 0.07;
                return float4(shade, shade, shade, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
