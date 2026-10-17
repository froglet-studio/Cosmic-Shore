// The living crystal lattice (Docs/MENU_REDESIGN_B.md §45.2): replaces the blueprint grid (kit_grid) behind
// style-B panels and windows. UGUI-compatible (UI/Default structure: stencil masking, RectMask2D clip, vertex
// colour). Draw it on an Image with no sprite: the UVs then span the rect, which the edge fade and the reveal use.
// Animated by _Time alone, so it never dirties a canvas. Two layers: a far one drifting slowly, a near one
// carrying the element energy. With _Reveal on, the vertex alpha (a CanvasGroup fade) grows the lattice out
// from the centre instead of fading it - the window crystallises in. The maths is UILattice.cginc.
Shader "CosmicShore/UI/Lattice"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Lattice)]
        _LineColor ("Line Colour", Color) = (0.263, 0.298, 0.537, 1)
        _LineWidth ("Line Width (canvas units)", Range(0.3, 4)) = 1.1
        _NodeSize ("Node Size (canvas units)", Range(0.5, 8)) = 2.4

        [Header(Near layer)]
        _NearSpacing ("Spacing", Float) = 130
        _NearAlpha ("Alpha", Range(0, 1)) = 0.42
        _NearDrift ("Seconds per lattice step", Float) = 55

        [Header(Far layer)]
        _FarSpacing ("Spacing", Float) = 72
        _FarAlpha ("Alpha (0 skips the layer)", Range(0, 1)) = 0.2
        _FarDrift ("Seconds per lattice step", Float) = 90

        [Header(Energy)]
        _PulseDensity ("Lines carrying pulses", Range(0, 1)) = 0.22
        _PulseSpeed ("Pulse speed (units per second)", Float) = 240
        _PulseRepeat ("Distance between pulses", Float) = 1400
        _PulseLength ("Pulse length", Float) = 120
        _TwinkleDensity ("Nodes that twinkle", Range(0, 1)) = 0.05
        _ChargeColor ("Charge", Color) = (1, 0.827, 0.302, 1)
        _MassColor ("Mass", Color) = (0.498, 0.659, 1, 1)
        _SpaceColor ("Space", Color) = (0.690, 0.486, 1, 1)
        _TimeColor ("Time", Color) = (0.373, 0.941, 0.784, 1)

        [Header(Shape)]
        _FadeStart ("Edge fade starts (0 centre, 1 corner)", Range(0, 1)) = 0.55
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
            // 3.0: two lattice layers exceed the shader-model-2 instruction limits.
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "UILattice.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;
            fixed4 _LineColor;
            float _LineWidth, _NodeSize;
            float _NearSpacing, _NearAlpha, _NearDrift;
            float _FarSpacing, _FarAlpha, _FarDrift;
            float _PulseDensity, _PulseSpeed, _PulseRepeat, _PulseLength, _TwinkleDensity;
            fixed4 _ChargeColor, _MassColor, _SpaceColor, _TimeColor;
            float _FadeStart, _Reveal;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Canvas-space position: the lattice keeps its size whatever the panel's.
                float2 p = IN.worldPosition.xy;
                float t = _Time.y;

                float4 near = LatticeLayer(p, t, _NearSpacing, _NearDrift, _LineWidth, _NodeSize,
                                           _PulseDensity, _PulseSpeed, _PulseRepeat, _PulseLength, _TwinkleDensity,
                                           _LineColor.rgb, _ChargeColor.rgb, _MassColor.rgb, _SpaceColor.rgb, _TimeColor.rgb);
                float4 sum = near * _NearAlpha;
                if (_FarAlpha > 0.0)
                {
                    float4 far = LatticeLayer(p, t, _FarSpacing, _FarDrift, _LineWidth * 0.8, _NodeSize * 0.7,
                                              0.0, 0.0, 1.0, 0.0, 0.0,
                                              _LineColor.rgb, _ChargeColor.rgb, _MassColor.rgb, _SpaceColor.rgb, _TimeColor.rgb);
                    sum += far * _FarAlpha;
                }

                // r: 0 at the centre, 1 at the corners.
                float r = length((IN.texcoord - 0.5) * 2.0) * 0.70710678;
                float fade = 1.0 - smoothstep(_FadeStart, 1.0, r);
                float alpha = IN.color.a;
                float shape = fade * alpha;
                if (_Reveal > 0.5)
                {
                    float2 rv = LatticeReveal(r, alpha);
                    shape = fade * rv.x;
                    sum.rgb += sum.rgb * rv.y * 2.0;
                }

                fixed4 color;
                color.a = saturate(sum.a * shape);
                color.rgb = (sum.rgb / max(sum.a, 0.0001)) * IN.color.rgb;

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
