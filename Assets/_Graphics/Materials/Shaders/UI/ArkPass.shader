// The Ark's HyperSea pass (Docs/MENU_REDESIGN_B.md section 197; Shombith 2026-11-13: "make the transitions smoother and
// realistic as they belong in the hypersea"): going from one Ark room's backdrop to the next, a pearl membrane, bowed and
// wobbling like water, crosses the picture in the direction of travel. It bends both rooms like a lens and splits them
// into the rainbow fringe of the HyperSea tunnels in the pilot scenes (Reaper, Astrofoil); the room left drifts on and
// pushes in while the room reached settles out of the same drift; caustic light, like the holo tables on the Jugger and
// Scales bridges, washes over the arrived room and fades; the light dips between rooms. Nothing is tinted: every colour
// on screen is the rooms' own light, lifted or split (Every colour comes from the colour set).
// _MainTex is the room left (the RawImage's texture), _ToTex the room reached. _Mode: 0 shows _MainTex, 1 dissolves
// (the pier's runs: rest, conversion, Omni made), 2 is the pass. ArkBackdrop drives every property each frame on
// unscaled time (_Clock): the menu is paused behind the Ark, so _Time would hold still.
// The maths is the prototype's (https://claude.ai/artifact/R4jLUUM48M6EJu7ERx8AaW), line for line.
// _Style picks the pass (Shombith 2026-11-13: "randomise between A B and E every time for now until we finalise one"):
// 0 the pearl membrane (A), 1 the ripple from the tab pressed, like a drop landing in the sea (B), 2 the caustic veil,
// the room reached found first along caustic light veins (E). ArkBackdrop picks one per change.
Shader "CosmicShore/UI/ArkPass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Room left (the RawImage's texture)", 2D) = "black" {}
        _ToTex ("Room reached", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Pass ("Progress, eased (0-1)", Range(0, 1)) = 0
        _Mode ("0 show, 1 dissolve, 2 pass", Float) = 0
        _Dir ("Direction of travel (+1 to the right, -1 to the left)", Float) = 1
        _Clock ("Unscaled seconds", Float) = 0
        _Aspect ("Width over height", Float) = 1.7778
        _FromOn ("Room left has a picture (0 is the dark)", Float) = 1
        _ToOn ("Room reached has a picture (0 is the dark)", Float) = 1
        _Style ("Pass: 0 membrane, 1 ripple, 2 caustic veil", Float) = 0
        _Origin ("Where the ripple starts (the tab pressed, 0-1 across the picture)", Vector) = (0.5, 0.9, 0, 0)

        [Header(Membrane)]
        _Width ("Width of the membrane (share of the screen)", Range(0.02, 0.4)) = 0.11
        _Lens ("How strongly it bends the picture", Range(0, 1.5)) = 0.55
        _Sheen ("Pearl sheen", Range(0, 2)) = 0.55
        _Crest ("Crest light", Range(0, 2)) = 0.6
        _Split ("Rainbow split", Range(0, 3)) = 1

        [Header(Glide)]
        _Push ("Push in (share of the picture)", Range(0, 0.2)) = 0.07
        _Drift ("Drift (share of the width)", Range(0, 0.1)) = 0.035
        _Dip ("Light dip between rooms", Range(0, 0.6)) = 0.22
        _Caustics ("Caustic light on arrival", Range(0, 4)) = 1.6

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
            "CanUseSpriteAtlas"="False"
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
            sampler2D _ToTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;
            float _Pass, _Mode, _Dir, _Clock, _Aspect, _FromOn, _ToOn, _Style;
            float4 _Origin;
            float _Width, _Lens, _Sheen, _Crest, _Split;
            float _Push, _Drift, _Dip, _Caustics;

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

            float3 Room(sampler2D t, float2 uv, float on)
            {
                return tex2D(t, saturate(uv)).rgb * on;
            }

            float Caustic(float2 q, float t)
            {
                float v = sin(q.x * 3.1 + t) + sin(q.y * 3.7 - t * 1.3) + sin((q.x + q.y) * 2.3 + t * 0.7) +
                          sin(length(q - 1.0) * 4.0 - t);
                return pow(saturate(0.5 + 0.125 * v), 4.0);
            }

            float3 HyperSeaPass(float2 uv, float p, float t)
            {
                float s = _Dir >= 0.0 ? 1.0 : -1.0;
                // Distance along the travel from the edge the next room comes in by.
                float x = s > 0.0 ? 1.0 - uv.x : uv.x;
                float yc = uv.y * 2.0 - 1.0;
                float bow = 0.14 * yc * yc;
                float wob = 0.022 * sin(uv.y * 8.0 + t * 2.6) + 0.012 * sin(uv.y * 21.0 - t * 4.3);
                float front = lerp(-0.3, 1.3, p);
                float d = x - (front - bow + wob);               // below 0: the room reached
                float band = exp(-d * d / (_Width * _Width));
                float edge = exp(-d * d / (0.035 * 0.035));

                float2 c = uv - 0.5;
                float2 uo = 0.5 + c / (1.0 + _Push * p) + float2(s * _Drift * p, 0.0);
                float2 un = 0.5 + c / (1.0 + _Push * (1.0 - p)) - float2(s * _Drift * (1.0 - p), 0.0);

                // A water lens: the picture bulges toward the crest, and ripples run along it.
                float lens = -d * band * _Lens;
                float rip = sin(d * 48.0 - t * 7.0 + uv.y * 6.0) * band;
                float2 off = float2(s * (lens + rip * 0.006), rip * 0.004 + 0.012 * band * yc);
                // Pearl: the split follows the ripple, so the rainbow runs in bands, never one hard fringe.
                float2 ds = float2(s * (0.004 + 0.006 * abs(rip)) * band, 0.003 * rip) * _Split;

                float3 o = float3(Room(_MainTex, uo + off + ds, _FromOn).r, Room(_MainTex, uo + off, _FromOn).g,
                                  Room(_MainTex, uo + off - ds, _FromOn).b);
                float3 n = float3(Room(_ToTex, un - off + ds, _ToOn).r, Room(_ToTex, un - off, _ToOn).g,
                                  Room(_ToTex, un - off - ds, _ToOn).b);
                float m = smoothstep(-0.03, 0.03, d);
                float3 col = lerp(n, o, m);

                // The sheen is the rooms' own light lifted where the membrane is.
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col += (col * 0.8 + lum * 0.5) * band * _Sheen + (col + lum) * edge * _Crest;
                float behind = (1.0 - m) * exp(min(d, 0.0) * 5.0);
                col += col * Caustic(float2(uv.x * _Aspect, uv.y) * 5.0, t * 1.4) * behind * _Caustics;
                col *= 1.0 - _Dip * sin(UNITY_PI * p);
                return col;
            }

            float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            // B: a ring spreads from the tab pressed, like a drop landing in the sea; the room reached is inside it.
            float3 Ripple(float2 uv, float p, float t)
            {
                float2 o = _Origin.xy;
                float2 q = (uv - o) * float2(_Aspect, 1.0);
                float r = length(q);
                float2 dirv = r > 0.0 ? q / r : float2(0.0, 0.0);
                float d = r - p * 2.2;
                float band = exp(-d * d / (0.09 * 0.09));
                float edge = exp(-d * d / (0.025 * 0.025));
                float wave = sin(d * 55.0 - t * 8.0) * band;
                float trail = sin(d * 26.0 - t * 5.0) * exp(min(d, 0.0) * 3.5) * (1.0 - p) * step(d, 0.0);
                float2 toUv = dirv / float2(_Aspect, 1.0);
                float2 off = toUv * (-d * band * _Lens * 0.9 + wave * 0.012 + trail * 0.008);
                float2 ds = toUv * 0.006 * band * _Split;
                float3 a = float3(Room(_MainTex, uv + off + ds, _FromOn).r, Room(_MainTex, uv + off, _FromOn).g,
                                  Room(_MainTex, uv + off - ds, _FromOn).b);
                float2 un = o + (uv - o) / (1.0 + 0.08 * (1.0 - p));
                float3 n = float3(Room(_ToTex, un + off + ds, _ToOn).r, Room(_ToTex, un + off, _ToOn).g,
                                  Room(_ToTex, un + off - ds, _ToOn).b);
                float m = smoothstep(-0.02, 0.02, d);
                float3 col = lerp(n, a, m);
                float l = Luma(col);
                col += (col * 0.7 + l * 0.5) * band * (_Sheen * 0.9) + (col + l) * edge * (_Crest * 0.9);
                return col * (1.0 - _Dip * 0.8 * sin(UNITY_PI * p));
            }

            // E: light finds the room reached first along caustic veins, like sun through water, until it is whole.
            float3 CausticVeil(float2 uv, float p, float t)
            {
                float2 q = float2(uv.x * _Aspect, uv.y) * 5.0;
                float cpat = Caustic(q, t * 1.1);
                float slow = 0.5 + 0.5 * sin(uv.x * 3.0 + uv.y * 2.0 + t * 0.6);
                float v = 1.0 - (0.8 * sqrt(cpat) + 0.2 * slow);
                float m = smoothstep(p * 1.2 - 0.12, p * 1.2, v);
                const float e = 0.01;
                float2 g = float2(Caustic(q + float2(e, 0.0), t * 1.1) - cpat, Caustic(q + float2(0.0, e), t * 1.1) - cpat) / e;
                float k = sin(UNITY_PI * p);
                float2 off = g * 0.0025 * k;
                float3 a = Room(_MainTex, uv + off, _FromOn);
                float3 n = Room(_ToTex, uv - off * 1.5, _ToOn);
                float3 col = lerp(n, a, m);
                float h = (m - 0.5) * 3.0;
                float rim = exp(-h * h);
                float l = Luma(col);
                col += (col + l * 0.6) * rim * (_Crest * 1.15) * k;
                col += col * cpat * (_Caustics * 0.55) * k;
                return col * (1.0 - _Dip * 0.7 * k);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float3 rgb;
                if (_Mode < 0.5)
                    rgb = Room(_MainTex, uv, _FromOn);
                else if (_Mode < 1.5)
                    rgb = lerp(Room(_MainTex, uv, _FromOn), Room(_ToTex, uv, _ToOn), _Pass);
                else if (_Style < 0.5)
                    rgb = HyperSeaPass(uv, _Pass, _Clock);
                else if (_Style < 1.5)
                    rgb = Ripple(uv, _Pass, _Clock);
                else
                    rgb = CausticVeil(uv, _Pass, _Clock);

                fixed4 color = fixed4(rgb, 1.0) * IN.color;

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
