// A bond of the party molecule (Docs/MENU_REDESIGN_B.md §45.4): a line from the leader to a member, its light
// flowing outward in dashes. UGUI-compatible (UI/Default structure). Draw it on a thin Image laid along the bond
// (rotated, its left edge at the centre) with KitRimMesh on it, which writes the pixel's place on the rect into UV1.
// The colour is the vertex colour, so the party panel tints a bond by the member's domain the way it tints the
// avatar ring - only when that colour changes. Animated by _Time alone. The maths is UIPresence.cginc.
Shader "CosmicShore/UI/Bond"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Bond)]
        _Width ("Core half-width (canvas units)", Range(0.2, 6)) = 1.4
        _Soft ("Edge softness (canvas units)", Range(0.2, 4)) = 1
        _Period ("Dash spacing (canvas units)", Float) = 18
        _Speed ("Flow speed (canvas units per second)", Float) = 26

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
            #include "UIPresence.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex    : POSITION;
                float4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 rimUV         : TEXCOORD2;   // KitRimMesh: place on the rect (xy), its size (zw)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;
            float _Width, _Soft, _Period, _Speed;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.rimUV = v.texcoord1;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // A hidden window (its CanvasGroup at alpha 0) draws nothing: stop before the maths. The output
                // there is already transparent; this only stops paying for it (Docs/MENU_REDESIGN_B.md §46.3).
                if (IN.color.a < 0.004) return float4(0.0, 0.0, 0.0, 0.0);
                // Along the line from the centre end (canvas units), and across it from its centre line.
                float along = IN.rimUV.x * IN.rimUV.z;
                float across = abs(IN.rimUV.y - 0.5) * IN.rimUV.w;
                float b = PresenceBond(across, along, _Time.y, _Width, _Soft, _Period, _Speed);
                fixed4 color;
                color.rgb = IN.color.rgb * (1.0 + b * 0.35);
                color.a = saturate(b) * IN.color.a;

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
