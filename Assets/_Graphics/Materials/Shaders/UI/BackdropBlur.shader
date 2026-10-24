// The frosted glass's blur (Docs/MENU_REDESIGN_B.md §62): UIBackdropBlur downsamples the game camera's finished
// frame with pass 0 and softens it with pass 1, back and forth, using Blitter (Blit.hlsl's full-screen triangle).
// Pass order is fixed: UIBackdropBlur.BlurPass names them by index.
Shader "Hidden/CosmicShore/UI/BackdropBlur"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _FrogletBlurSourceTexel;   // xy: 1 / the camera's size
        float4 _FrogletBlurTexel;         // xy: 1 / the blurred copy's size

        half3 Tap(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        }
        ENDHLSL

        Pass
        {
            Name "Downsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Five bilinear taps (the centre and four diagonals one texel out) average a 4 x 4 block of the frame,
            // so the copy does not shimmer as small bright things move. Highlights are compressed so one bright
            // prism does not bloom across the glass.
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 o = _FrogletBlurSourceTexel.xy;
                half3 c = Tap(uv) * 0.5h
                        + (Tap(uv + float2(-o.x, -o.y)) + Tap(uv + float2(o.x, -o.y))
                         + Tap(uv + float2(-o.x, o.y)) + Tap(uv + float2(o.x, o.y))) * 0.125h;
                c = c / (1.0h + max(c.r, max(c.g, c.b)) * 0.25h);
                return half4(c, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // A Kawase step: four bilinear taps 1.5 texels out on the diagonals (each a 2 x 2 average), so two passes
            // spread a pixel over about a 10-texel disc of the quarter-size copy.
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 o = _FrogletBlurTexel.xy * 1.5;
                half3 c = (Tap(uv + float2(-o.x, -o.y)) + Tap(uv + float2(o.x, -o.y))
                         + Tap(uv + float2(-o.x, o.y)) + Tap(uv + float2(o.x, o.y))) * 0.25h;
                return half4(c, 1.0h);
            }
            ENDHLSL
        }
    }
}
