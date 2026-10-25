// The sea lines behind a style-B window (Docs/MENU_REDESIGN_B.md §63): thin wave lines in three depth layers sliding
// sideways, slowly, at their own speeds over the window's veil. UGUI-compatible (UI/Default structure: stencil
// masking, RectMask2D clip, vertex colour). Draw it on an Image with no sprite: the UVs then span the rect, which the
// top and bottom fade read. Animated by _Time alone, so it never dirties a canvas. The maths is UISeaLines.cginc.
Shader "CosmicShore/UI/SeaLines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Lines)]
        _LineColor ("Line Colour", Color) = (0.435, 0.518, 0.847, 1)
        _Spacing ("Spacing between lines (canvas units)", Float) = 46
        _Loop ("Wave loop length (canvas units)", Float) = 480
        _Amplitude ("Wave height (canvas units)", Float) = 9

        [Header(Far layer)]
        _FarAlpha ("Alpha", Range(0, 1)) = 0.13
        _FarWidth ("Width (canvas units)", Range(0.3, 4)) = 0.8
        _FarSeconds ("Seconds per loop (0 holds it still)", Float) = 90

        [Header(Middle layer)]
        _MidAlpha ("Alpha", Range(0, 1)) = 0.19
        _MidWidth ("Width (canvas units)", Range(0.3, 4)) = 1
        _MidSeconds ("Seconds per loop (0 holds it still)", Float) = 68

        [Header(Near layer)]
        _NearAlpha ("Alpha", Range(0, 1)) = 0.27
        _NearWidth ("Width (canvas units)", Range(0.3, 4)) = 1.3
        _NearSeconds ("Seconds per loop (0 holds it still)", Float) = 45

        [Header(Shape)]
        _FadeTop ("Fade at the top (share of the height)", Range(0, 0.5)) = 0.2
        _FadeBottom ("Fade at the bottom (share of the height)", Range(0, 0.5)) = 0.2

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
            #include "UISeaLines.cginc"

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
            float _Spacing, _Loop, _Amplitude;
            float _FarAlpha, _FarWidth, _FarSeconds;
            float _MidAlpha, _MidWidth, _MidSeconds;
            float _NearAlpha, _NearWidth, _NearSeconds;
            float _FadeTop, _FadeBottom;

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
                // A hidden window (its CanvasGroup at alpha 0) draws nothing: stop before the maths.
                if (IN.color.a < 0.004) return float4(0.0, 0.0, 0.0, 0.0);
                float fade = SeaEdgeFade(IN.texcoord.y, _FadeBottom, _FadeTop);
                if (fade <= 0.0) return float4(0.0, 0.0, 0.0, 0.0);
                // Canvas-space position: the lines keep their size whatever the window's.
                float a = SeaLines(IN.worldPosition.xy, _Time.y, _Spacing, _Loop, _Amplitude,
                                   float3(_FarAlpha, _MidAlpha, _NearAlpha), float3(_FarWidth, _MidWidth, _NearWidth),
                                   float3(_FarSeconds, _MidSeconds, _NearSeconds));

                fixed4 color;
                color.rgb = _LineColor.rgb * IN.color.rgb;
                color.a = a * fade * IN.color.a;

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
