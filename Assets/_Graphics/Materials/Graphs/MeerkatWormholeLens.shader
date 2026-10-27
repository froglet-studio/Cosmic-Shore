// MeerkatWormholeLens.shader — the bent space round a Meerkat's tunnels, drawn in ONE full-screen pass: the Vessel Studio's
// lensMat (Docs/Studios/VesselStudio/meerkat.html), ported line for line (R_VesselActions/MEERKAT.md). Drawn only by
// MeerkatWormholeLensPass.cs after the transparents, from its copy of the camera colour (_MeerkatSceneColor):
//   - each tunnel's two mouths dent space, a graded dent exactly as wide as the mouth: in at the way in, out at the way out
//     (a vessel or other opaque mass in front of a mouth is not bent);
//   - the tubes' warp (_MeerkatWarpTex, drawn by the same pass from MeerkatWormholeWarp.shader) bends the world behind a
//     tube where its wall runs;
//   - a collapse ripples the screen's EDGES only: a ring running from rippleEdge out past the corners; nothing inside it
//     moves (Yash, 2026-10-27: no distortion in the player's eyesight).
// A ray bent off the screen shows the screen mirrored at its edge, as in the studio.
Shader "CosmicShore/MeerkatWormholeLens"
{
    Properties { }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+50" "IgnoreProjector" = "True" }

        Pass
        {
            Name "MeerkatWormholeLens"
            Tags { "LightMode" = "MeerkatWormholeLens" }
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MeerkatLensVert
            #pragma fragment MeerkatLensFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MEERKAT_LENS_MAX_WELLS 8

            // Per camera, with the draw (MeerkatWormholeLensPass.ScreenWells): c xy the mouth's centre on screen (screen
            // heights from the centre, y up), z its depth, w 1; p x 3 (way in) or 4 (way out), y its angular radius, z the
            // dent's strength, w the foreground margin.
            float4 _MWWellC[MEERKAT_LENS_MAX_WELLS];
            float4 _MWWellP[MEERKAT_LENS_MAX_WELLS];
            // x strength, y its front (share of the half diagonal), z where it starts
            float4 _MWRipple;
            // x the camera's aspect, y the tubes' warp is drawn (1) or not, z mouths in the bank
            float4 _MWView;

            TEXTURE2D(_MeerkatSceneColor);
            TEXTURE2D(_MeerkatWarpTex);

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings MeerkatLensVert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            half4 MeerkatLensFrag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect = _MWView.x;
                float2 p = (uv - 0.5) * float2(aspect, 1.0);
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float2 disp = float2(0.0, 0.0);
                for (int i = 0; i < MEERKAT_LENS_MAX_WELLS; i++)
                {
                    if (i >= (int)_MWView.z) break;
                    float4 c = _MWWellC[i];
                    float4 w = _MWWellP[i];
                    if (c.w < 0.5) continue;
                    if (sceneZ < c.z - w.w) continue;   // a foreground object is not bent
                    float2 d = p - c.xy;
                    float th = max(length(d), 1e-6);
                    float2 dir = d / th;
                    float rc = max(w.y, 1e-6), u = th / rc, a = w.z * rc * u * exp(-0.5 * u * u);
                    disp += (w.x < 3.5 ? -1.0 : 1.0) * dir * a;
                }
                if (_MWView.y > 0.5) disp += SAMPLE_TEXTURE2D_LOD(_MeerkatWarpTex, sampler_LinearClamp, uv, 0).xy;
                if (_MWRipple.x > 0.0)
                {
                    float hd = 0.5 * length(float2(aspect, 1.0)), r = length(p) / hd;
                    float mask = smoothstep(_MWRipple.z, min(1.0, _MWRipple.z + 0.25), r), x = (r - _MWRipple.y) / 0.07;
                    disp += p / max(length(p), 1e-4) * _MWRipple.x * mask * exp(-x * x) * sin(x * 3.14159);
                }
                if (dot(disp, disp) < 1e-12) discard;   // nothing bends this pixel: it keeps what the camera drew
                float2 s = p + disp;
                float2 suv = s / float2(aspect, 1.0) + 0.5;
                suv = 1.0 - abs(1.0 - abs(suv));
                return half4(SAMPLE_TEXTURE2D_LOD(_MeerkatSceneColor, sampler_LinearClamp, suv, 0).rgb, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
