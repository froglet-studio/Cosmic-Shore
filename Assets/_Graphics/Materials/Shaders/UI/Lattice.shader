// The frosted hex lattice (Docs/MENU_REDESIGN_B.md §62, rev 11 option H): replaces the line lattice behind style-B
// panels and windows. UGUI-compatible (UI/Default structure: stencil masking, RectMask2D clip, vertex colour). Draw
// it on an Image with no sprite: the UVs then span the rect, which the edge fade, the tide and the reveal use.
//
// Three layers, one pass:
//   1. Frost: the world behind the menu, blurred (UIBackdropBlur's _FrogletBackdropBlur, sampled at the pixel's screen
//      position) and tinted toward _FrostTint. Only while that texture is live (_FrogletBackdropBlurOn) and _Frost is
//      on; otherwise this layer draws nothing and whatever is under the Image (the window's dark veil) shows.
//   2. Hexagons: Amoebius's LATTICE backdrop, one inset outline per cell, faint, fading toward the rect's corners.
//   3. Motion, picked per Image by KitBackdropMotion through UV1 (x: motion + 1, y: the time the window opened;
//      no data: _Motion) and UV2 (the rect's size, for the ring): 0 still, 1 the centre ring (H2), 2 the tide (H4),
//      3 grow on open (H6).
// Animated by _Time alone, so it never dirties a canvas. With _Reveal on, the vertex alpha (a CanvasGroup fade) also
// grows the hexagons out from the centre. The maths is UILattice.cginc.
Shader "CosmicShore/UI/Lattice"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Frost)]
        [Toggle] _Frost ("Frosted glass (the blurred world behind)", Float) = 1
        _FrostTint ("Frost tint", Color) = (0, 0.004, 0.039, 1)
        _FrostAmount ("Tint over the blurred world", Range(0, 1)) = 0.5

        [Header(Hexagons)]
        _LineColor ("Line colour", Color) = (0.345, 0.4, 0.69, 1)
        _LineAlpha ("Line alpha", Range(0, 1)) = 0.4
        _LineWidth ("Line width (canvas units)", Range(0.3, 4)) = 1
        _HexRadius ("Cell radius, centre to corner (canvas units)", Float) = 34
        _HexInset ("Outline at this fraction of the cell", Range(0.5, 1)) = 0.92
        _FadeStart ("Edge fade starts (0 centre, 1 corner)", Range(0, 1)) = 0.6

        [Header(Motion)]
        _Motion ("Motion without per-Image data (0 still, 1 ring, 2 tide, 3 grow)", Float) = 0
        _GlowColor ("Ring and tide light", Color) = (0.498, 0.847, 1, 1)
        _RingAlpha ("Ring line alpha", Range(0, 1)) = 0.85
        _RingWavelength ("Ring wavelength (canvas units)", Float) = 862
        _RingPeriod ("Ring period (seconds)", Float) = 5.2
        _TideAlpha ("Tide light", Range(0, 1)) = 0.18
        _TidePeriod ("Tide period (seconds)", Float) = 14
        _TideWidth ("Tide band half-width (0-1 of the rect)", Range(0.02, 0.6)) = 0.16
        _GrowSeconds ("Grow on open (seconds)", Float) = 1.2

        [Header(Shape)]
        [Toggle] _Reveal ("Crystallise in with the vertex alpha", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "UILattice.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex    : POSITION;
                float4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 motion        : TEXCOORD2;
                float4 screenPos     : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;
            float _Frost, _FrostAmount;
            fixed4 _FrostTint;
            fixed4 _LineColor;
            float _LineAlpha, _LineWidth, _HexRadius, _HexInset, _FadeStart;
            float _Motion;
            fixed4 _GlowColor;
            float _RingAlpha, _RingWavelength, _RingPeriod, _TideAlpha, _TidePeriod, _TideWidth, _GrowSeconds;
            float _Reveal;
            // Set by UIBackdropBlur (Assets/_Scripts/UI/Kit/UIBackdropBlur.cs): the blurred world and whether it is live.
            sampler2D _FrogletBackdropBlur;
            float _FrogletBackdropBlurOn;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                OUT.motion = float4(v.texcoord1, v.texcoord2);
                OUT.screenPos = ComputeScreenPos(OUT.vertex);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // A hidden window (its CanvasGroup at alpha 0) draws nothing: stop before the maths
                // (Docs/MENU_REDESIGN_B.md §46.3).
                if (IN.color.a < 0.004) return float4(0.0, 0.0, 0.0, 0.0);
                float2 p = IN.worldPosition.xy;
                float t = _Time.y;

                // The motion this Image wears: KitBackdropMotion writes motion + 1 (0 = no data, use the material's).
                float motion = IN.motion.x > 0.5 ? floor(IN.motion.x - 0.5) : _Motion;
                float opened = IN.motion.y;

                // r: 0 at the rect's centre, 1 at its corners. The ring is measured in canvas units from the centre.
                float2 fromCentre = (IN.texcoord - 0.5) * 2.0;
                float r = length(fromCentre) * 0.70710678;
                float fade = 1.0 - smoothstep(_FadeStart, 1.0, r);

                float2 cell;
                float edge = HexEdgeDistance(p, _HexRadius, _HexInset, cell);
                float lineCov = HexLine(edge, _LineWidth);

                float alpha = IN.color.a;
                float lines = lineCov * _LineAlpha;
                float3 lineRgb = _LineColor.rgb;
                float3 glowAdd = float3(0.0, 0.0, 0.0);

                if (motion > 0.5 && motion < 1.5)
                {
                    // H2: the ring, in canvas units from the Image's centre, measured at the cell's centre so whole
                    // cells light together, as Amoebius's do. IN.motion.zw: the rect's size (KitBackdropMotion).
                    float2 size = max(IN.motion.zw, 1.0);
                    float2 cellUv = IN.texcoord + (cell - p) / size;
                    float pulse = HexRing(length((cellUv - 0.5) * size), t, _RingWavelength, _RingPeriod);
                    lines = lineCov * lerp(_LineAlpha, _RingAlpha, pulse);
                    lineRgb = lerp(_LineColor.rgb, _GlowColor.rgb, pulse);
                    glowAdd = _GlowColor.rgb * pulse * 0.05;
                }
                else if (motion > 1.5 && motion < 2.5)
                {
                    // H4: the tide, a soft band of light crossing the rect.
                    float band = HexTide(IN.texcoord.y, t, _TidePeriod, _TideWidth);
                    glowAdd = _GlowColor.rgb * band * _TideAlpha;
                    lines = lineCov * _LineAlpha * (1.0 + band * 0.6);
                }

                float shape = fade;
                float front = 0.0;
                if (motion > 2.5)
                {
                    // H6: the hexagons grow out from the centre over _GrowSeconds after the window opened, then hold.
                    float k = saturate((t - opened) / max(_GrowSeconds, 0.01));
                    float2 rv = LatticeReveal(r, k);
                    shape *= rv.x;
                    front = rv.y;
                }
                if (_Reveal > 0.5)
                {
                    float2 rv = LatticeReveal(r, alpha);
                    shape *= rv.x;
                    front = max(front, rv.y);
                }
                lines = saturate(lines * shape + lineCov * front * 0.8);
                lineRgb = lerp(lineRgb, _GlowColor.rgb, saturate(front));

                // The frost underneath: the blurred world tinted toward navy, opaque, faded with the window.
                float frostOn = _Frost * _FrogletBackdropBlurOn;
                float3 baseRgb = float3(0.0, 0.0, 0.0);
                float baseA = 0;
                if (frostOn > 0.5)
                {
                    float2 suv = IN.screenPos.xy / max(IN.screenPos.w, 1e-6);
                    float3 world = tex2D(_FrogletBackdropBlur, suv).rgb;
                    baseRgb = lerp(world, _FrostTint.rgb, _FrostAmount) + glowAdd;
                    baseA = 1.0;
                }
                else
                {
                    // No live blur: the motion's light still reads over the veil under the Image.
                    float glowA = saturate(max(glowAdd.r, max(glowAdd.g, glowAdd.b)));
                    baseRgb = glowAdd / max(glowA, 1e-4);
                    baseA = glowA;
                }

                // Lines over the base, as straight alpha.
                float outA = lines + baseA * (1.0 - lines);
                float3 outRgb = (lineRgb * lines + baseRgb * baseA * (1.0 - lines)) / max(outA, 1e-4);

                fixed4 color;
                color.rgb = outRgb * IN.color.rgb;
                color.a = saturate(outA * alpha);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
