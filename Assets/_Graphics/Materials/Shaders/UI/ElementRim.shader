// The elemental rim (Docs/MENU_REDESIGN_B.md §45.3): a style-B border sprite (the sliver line) lit by a comet of an
// element's light running round it, or - omni - the four elements blended round it, turning. UGUI-compatible
// (UI/Default structure). Needs KitRimMesh on the same Image (UV1 = the pixel's place on its rect). Animated by
// _Time alone. The maths is UIElementRim.cginc.
Shader "CosmicShore/UI/ElementRim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Rim)]
        [Toggle] _Omni ("Omni (all four elements)", Float) = 0
        _ElementColor ("Element", Color) = (1, 0.827, 0.302, 1)
        _Speed ("Laps per second", Float) = 0.12
        _Tail ("Comet length (turns)", Range(0.02, 1)) = 0.22
        _BaseLevel ("Rim light away from the comet", Range(0, 1)) = 0.45
        _ChargeColor ("Charge", Color) = (1, 0.827, 0.302, 1)
        _MassColor ("Mass", Color) = (0.498, 0.659, 1, 1)
        _SpaceColor ("Space", Color) = (0.690, 0.486, 1, 1)
        _TimeColor ("Time", Color) = (0.373, 0.941, 0.784, 1)

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
            #include "UIElementRim.cginc"

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
                float4 rimUV         : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _Omni, _Speed, _Tail, _BaseLevel;
            fixed4 _ElementColor, _ChargeColor, _MassColor, _SpaceColor, _TimeColor;

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
                half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                float4 rim = ElementRim(IN.rimUV.xy, IN.rimUV.zw, _Time.y, _Speed, _Tail, _BaseLevel, _Omni,
                                        _ElementColor.rgb, _ChargeColor.rgb, _MassColor.rgb, _SpaceColor.rgb, _TimeColor.rgb);
                fixed4 color;
                color.rgb = rim.rgb * IN.color.rgb;
                color.a = tex.a * rim.a * IN.color.a;

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
