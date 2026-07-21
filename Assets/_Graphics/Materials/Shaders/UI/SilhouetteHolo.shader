// Holographic readout treatment for the vessel silhouette HUD icon.
// UGUI-compatible (UI/Default structure: stencil masking, RectMask2D clip, vertex-color
// multiply preserved so the jaws' energy tint keeps working). On top of the plain sprite:
//   - body tinted toward the player's domain accent (_DomainColor, set from code),
//   - an inner edge rim derived from the sprite's alpha gradient, glowing with a slow pulse,
//   - a subtle vertical scanline shimmer sweeping the sprite.
// All look parameters live on the material asset (SilhouetteHolo.mat) - single source for
// every vessel; the domain accent is the only per-instance value.
Shader "CosmicShore/UI/SilhouetteHolo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _DomainColor ("Domain Accent", Color) = (1,1,1,1)
        _BodyMix ("Body Accent Mix", Range(0,1)) = 0.6
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimWidth ("Rim Width (texels)", Range(0.5,8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0,4)) = 1.4
        _PulseSpeed ("Rim Pulse Speed", Range(0,10)) = 2
        _PulseAmount ("Rim Pulse Amount", Range(0,1)) = 0.25
        _ScanTiling ("Scanline Tiling", Range(0,80)) = 18
        _ScanSpeed ("Scanline Speed", Range(0,5)) = 0.5
        _ScanIntensity ("Scanline Intensity", Range(0,1)) = 0.12

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
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _DomainColor;
            float _BodyMix;
            fixed4 _RimColor;
            float _RimWidth;
            float _RimIntensity;
            float _PulseSpeed;
            float _PulseAmount;
            float _ScanTiling;
            float _ScanSpeed;
            float _ScanIntensity;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half a = tex.a;

                // Inner rim: how much more solid this texel is than its most-transparent
                // neighbour - a crisp band along the sprite's edge that follows any shape.
                float2 t = _MainTex_TexelSize.xy * _RimWidth;
                half aN = min(
                    min(tex2D(_MainTex, IN.texcoord + float2(0, t.y)).a,
                        tex2D(_MainTex, IN.texcoord - float2(0, t.y)).a),
                    min(tex2D(_MainTex, IN.texcoord + float2(t.x, 0)).a,
                        tex2D(_MainTex, IN.texcoord - float2(t.x, 0)).a));
                half rim = saturate(a - aN);

                half pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);
                half scan = _ScanIntensity *
                    (0.5 + 0.5 * sin((IN.texcoord.y * _ScanTiling + _Time.y * _ScanSpeed) * 6.2831853));

                half3 body = tex.rgb * lerp(half3(1, 1, 1), _DomainColor.rgb, _BodyMix);
                body += body * scan;

                half4 color;
                color.rgb = body + _RimColor.rgb * (rim * _RimIntensity * pulse);
                color.a = a;
                color *= IN.color;   // UGUI vertex tint (jaw energy tint) still multiplies through

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
