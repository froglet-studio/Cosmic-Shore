// The presence orb (Docs/MENU_REDESIGN_B.md §45.4): the soft Space glow, the inner ring your party sits on, the
// dashed orbit turning slowly with one dot per friend online, and a flowing bond from you to each party member in
// their domain colour. UGUI-compatible (UI/Default structure). Draw it on an Image with no sprite: its UVs then span
// the rect, and every radius is a fraction of the half-size, so the orb scales with its rect. KitPresenceOrb owns
// the one material instance and writes _Online and the three bond colours only when the party or the online list
// changes; the motion is _Time alone, so the orb never dirties a canvas. The maths is UIPresence.cginc.
Shader "CosmicShore/UI/Presence"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Orb)]
        _SpaceColor ("Space", Color) = (0.690, 0.486, 1, 1)
        _GlowAlpha ("Glow", Range(0, 1)) = 0.32
        _InnerRadius ("Inner ring radius (party)", Range(0, 1)) = 0.66
        _InnerAlpha ("Inner ring alpha", Range(0, 1)) = 0.34
        _OuterRadius ("Orbit radius (online)", Range(0, 1)) = 0.92
        _OuterAlpha ("Orbit alpha", Range(0, 1)) = 0.55
        _RingWidth ("Ring half-width", Range(0, 0.1)) = 0.012
        _Soft ("Edge softness", Range(0.001, 0.1)) = 0.03
        _Dashes ("Orbit dashes", Float) = 28
        _SpinSeconds ("Seconds per orbit turn", Float) = 40

        [Header(Online)]
        _Online ("Dots on the orbit (friends online)", Float) = 0
        _DotSize ("Dot radius", Range(0, 0.2)) = 0.055

        [Header(Party bonds)]
        _MemberRadius ("Member seat radius", Range(0, 1)) = 0.66
        _BondAngles ("Member seat angles (degrees, x y z)", Vector) = (135, 45, 270, 0)
        _Bond0 ("Bond 1 (alpha 0: empty seat)", Color) = (0, 0, 0, 0)
        _Bond1 ("Bond 2", Color) = (0, 0, 0, 0)
        _Bond2 ("Bond 3", Color) = (0, 0, 0, 0)
        _BondWidth ("Bond half-width", Range(0, 0.1)) = 0.022
        _BondPeriod ("Bond dash spacing", Float) = 0.16
        _BondSpeed ("Bond flow speed", Float) = 0.22

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
            fixed4 _SpaceColor;
            float _GlowAlpha, _InnerRadius, _InnerAlpha, _OuterRadius, _OuterAlpha, _RingWidth, _Soft, _Dashes, _SpinSeconds;
            float _Online, _DotSize;
            float _MemberRadius, _BondWidth, _BondPeriod, _BondSpeed;
            float4 _BondAngles;
            fixed4 _Bond0, _Bond1, _Bond2;

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
                // A hidden window (its CanvasGroup at alpha 0) draws nothing: stop before the maths. The output
                // there is already transparent; this only stops paying for it (Docs/MENU_REDESIGN_B.md §46.3).
                if (IN.color.a < 0.004) return float4(0.0, 0.0, 0.0, 0.0);
                // -1..1 across the rect: every radius is a fraction of the half-size.
                float2 p = (IN.texcoord - 0.5) * 2.0;
                float d = length(p);
                float t = _Time.y;
                float3 space = _SpaceColor.rgb;

                float glow = 1.0 - smoothstep(0.0, 1.0, d);
                glow = glow * glow * _GlowAlpha;
                float inner = PresenceRing(d, _InnerRadius, _RingWidth, _Soft) * _InnerAlpha;
                float orbit = PresenceOrbit(p, t, _OuterRadius, _RingWidth, _Soft, _Dashes, _SpinSeconds) * _OuterAlpha;
                float dots = PresenceDots(p, t, _OuterRadius, _DotSize, _Soft, _Online, _SpinSeconds, 0.08);

                float3 rgb = space * (glow + inner + orbit) + (space * 1.3 + float3(0.25, 0.25, 0.25)) * dots;
                float coverage = glow + inner + orbit + dots;

                float3 deg = _BondAngles.xyz * 0.01745329;
                float4 b0 = PresenceBondTo(p, t, float2(cos(deg.x), sin(deg.x)) * _MemberRadius, _Bond0,
                                           _BondWidth, _Soft, _BondPeriod, _BondSpeed);
                float4 b1 = PresenceBondTo(p, t, float2(cos(deg.y), sin(deg.y)) * _MemberRadius, _Bond1,
                                           _BondWidth, _Soft, _BondPeriod, _BondSpeed);
                float4 b2 = PresenceBondTo(p, t, float2(cos(deg.z), sin(deg.z)) * _MemberRadius, _Bond2,
                                           _BondWidth, _Soft, _BondPeriod, _BondSpeed);
                rgb += (b0.rgb + b1.rgb + b2.rgb) * 1.5;
                coverage += b0.a + b1.a + b2.a;

                fixed4 color;
                color.a = saturate(coverage) * IN.color.a;
                color.rgb = (rgb / max(coverage, 0.0001)) * IN.color.rgb;

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
