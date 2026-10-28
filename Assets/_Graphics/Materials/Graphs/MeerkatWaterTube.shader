// MeerkatWaterTube.shader — THE Meerkat wormhole tube (Yash, 2026-10-29: "it feels like going through some watery-glassy
// tunnel, like looking from an underwater aquarium at the outside world ... light faint tint ... tunnels won't have
// domains, they are universal objects. The networked tunnel (omni crystal) a little upgraded look, a special feeling").
// It replaces, as the default look, the full-screen contour-field lens (MeerkatWormholeLens.shader: up to 256 ray-march
// steps x 4 tubes x a 24-segment centreline search per PIXEL, plus a camera colour copy, a half-res warp target and a held
// depth texture) and the additive calm wall (MeerkatTubeWall.shader). The old looks stay in the Lavalamp lab
// (MeerkatTubeLook) for A/B.
//
// One pass, one draw per tube, premultiplied alpha, ZWrite Off. The look:
//  * WATER-GLASS WARP: the tube shows the opaque scene behind it (URP's _CameraOpaqueTexture, the half-res copy URP makes
//    after opaques) at a screen position pushed along the wall's view-space normal (stronger toward the outline) plus a
//    slow two-sine ripple computed per VERTEX. That is the aquarium pane: the world outside, gently bent and wobbling.
//    Cost: ONE texture fetch per pixel. The C# (MeerkatWaterTubeView) holds the opaque copy on for game cameras only
//    while a tube draws, and sets _MeerkatTubeSceneOn; with it 0 the tube falls back to a clear glass skin.
//  * a faint aqua-white tint on what is seen through it (no domain colour: every tunnel looks the same),
//  * a Fresnel rim from |n.v| (the same from inside and outside), slow flow bands along the tube, a caustic shimmer
//    (two crossed sines), fading near the camera and at the live window's fronts (_F0.._F1).
//  * MEERKAT_TUBE_NETWORK (the omni crystal's network channels, Resources/MeerkatWaterTubeNetwork.mat): the same base
//    plus an iridescent thin-film rim (a cosine palette on the Fresnel term), energy streaks racing along the tube in
//    lanes, and an HDR glow (_Glow) so bloom picks it up. ~15 more ALU, no extra fetch.
// ~45 ALU + 1 fetch per pixel (network ~60), half precision where it can be. All material properties live in
// UnityPerMaterial (SRP Batcher compatible); _MeerkatTubeSceneOn is a global.
// Vertex data (MeerkatTreeMesh.BuildTubeWall): uv0.x = the share of the way along (0..1), uv1.y = the share of the way
// round (0..1). Positions are in world space. The recipe: Docs/Shaders/WATERY_GLASS.md.
Shader "CosmicShore/MeerkatWaterTube"
{
    Properties
    {
        _Tint ("Water tint (faint, near white)", Color) = (0.84, 0.95, 1, 1)
        _Transmit ("Aquarium pane: how much of the bent world it shows (0 = plain glass skin)", Range(0, 1)) = 0.9
        _Refract ("Refraction (screen share at the outline)", Range(0, 0.15)) = 0.04
        _RefractFalloff ("Refraction falloff with distance (1/u)", Float) = 0.004
        _Wobble ("Ripple wobble (screen share)", Range(0, 0.03)) = 0.006
        _WobbleScale ("Ripple size (u)", Float) = 16
        _WobbleSpeed ("Ripple speed", Float) = 0.8
        _Clarity ("Body haze (0 = glass clear)", Range(0, 1)) = 0.03
        _Rim ("Rim opacity", Range(0, 1)) = 0.4
        _RimPower ("Rim power", Range(0.5, 8)) = 3
        _RimColor ("Rim light (faint)", Color) = (0.9, 0.98, 1, 1)
        _Flow ("Flow band strength", Range(0, 1)) = 0.1
        _FlowSpeed ("Flow speed (bands per second)", Float) = 0.6
        _FlowScale ("Flow band spacing (u)", Float) = 24
        _Caustic ("Caustic shimmer", Range(0, 1)) = 0.22
        _CausticScale ("Caustic size (u)", Float) = 9
        _Twist ("Twist (turns per 100 u)", Float) = 0.15
        _NearFade ("Near fade: clear inside (x), full from (y)", Vector) = (2, 14, 0, 0)

        [Header(Network look (omni crystal))]
        [Toggle(MEERKAT_TUBE_NETWORK)] _Network ("Network look", Float) = 0
        _Iri ("Iridescent rim", Range(0, 1)) = 0.7
        _IriScale ("Iridescence bands", Float) = 1.6
        _Streak ("Energy streaks", Range(0, 2)) = 0.8
        _StreakLanes ("Streak lanes round the tube", Float) = 6
        _StreakSpacing ("Streak spacing (u)", Float) = 36
        _StreakSpeed ("Streak speed (spacings per second)", Float) = 0.9
        _Glow ("Glow (HDR, feeds bloom)", Range(1, 4)) = 1.6

        [HideInInspector] _Alpha ("Fade (set per tube)", Float) = 1
        [HideInInspector] _F0 ("Window start (share)", Float) = 0
        [HideInInspector] _F1 ("Window end (share)", Float) = 1
        [HideInInspector] _Edge ("Window fronts (share)", Float) = 0.06
        [HideInInspector] _L ("Tube length (u)", Float) = 100
        [HideInInspector] _T ("Time (s)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+3" "IgnoreProjector" = "True" }

        Pass
        {
            Name "MeerkatWaterTube"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            Blend One OneMinusSrcAlpha   // premultiplied: the bent world replaces what is behind, the light adds

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex MeerkatWaterTubeVert
            #pragma fragment MeerkatWaterTubeFrag
            #pragma shader_feature_local_fragment _ MEERKAT_TUBE_NETWORK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _RimColor;
                half _Transmit, _Clarity, _Rim, _RimPower, _Flow, _Caustic, _Iri, _Streak, _Glow;
                float _Refract, _RefractFalloff, _Wobble, _WobbleScale, _WobbleSpeed;
                float _FlowSpeed, _FlowScale, _CausticScale, _Twist;
                float _Network, _IriScale, _StreakLanes, _StreakSpacing, _StreakSpeed;
                float4 _NearFade;
                float _Alpha, _F0, _F1, _Edge, _L, _T, _Cull;
            CBUFFER_END

            // 1 while MeerkatWaterTubeView holds URP's opaque copy on for the game cameras (a global, not per material)
            float _MeerkatTubeSceneOn;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;   // aS, -1, 0, 2 (MeerkatTreeMesh's layout)
                float2 uv1 : TEXCOORD1;   // 0, the share of the way round
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;   // unnormalised: its length is the distance to the eye
                float2 sp : TEXCOORD2;       // the share along, the share round
                float4 bend : TEXCOORD3;     // xy: the view-space normal's screen direction; zw: the ripple's offset
            };

            Varyings MeerkatWaterTubeVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = GetWorldSpaceViewDir(positionWS);
                o.sp = float2(input.uv0.x, input.uv1.y);

                // the pane's bend, per vertex: the normal's direction on screen, smaller far away
                float3 nVS = TransformWorldToViewDir(o.normalWS, true);
                float atten = 1.0 / (1.0 + length(o.viewWS) * _RefractFalloff);
                o.bend.xy = nVS.xy * (_Refract * atten);
                // a slow ripple: two sines along and round the tube (integer multiples round it, so no seam)
                float along = input.uv0.x * _L, ph = along / max(_WobbleScale, 0.5) * 6.2831853;
                float rnd = input.uv1.y * 6.2831853;
                o.bend.zw = float2(sin(ph - _T * _WobbleSpeed * 6.2831853 + rnd * 3.0),
                                   cos(ph * 0.71 + _T * _WobbleSpeed * 4.3 - rnd * 2.0)) * (_Wobble * atten);
                return o;
            }

            half4 MeerkatWaterTubeFrag(Varyings i) : SV_Target
            {
                float vS = i.sp.x;
                // the tube's live window: nothing outside it, condensing at its fronts
                if (vS < _F0 || vS > _F1) discard;
                half win = (half)smoothstep(0.0, _Edge, min(vS - _F0, _F1 - vS));

                float dist = length(i.viewWS);
                half nearA = (half)smoothstep(_NearFade.x, max(_NearFade.y, _NearFade.x + 0.01), dist);
                half k = win * nearA * (half)_Alpha;

                // Fresnel from |n.v|, so it reads the same whichever way the wall winds
                half nv = (half)abs(dot(normalize(i.normalWS), i.viewWS / max(dist, 1e-4)));
                half rim = 1.0h - nv;
                half fr = pow(max(rim, 1e-3h), (half)_RimPower);

                float along = vS * _L;                                                   // u along the tube
                float ang = i.sp.y * 6.2831853 + along * _Twist * 0.0628319 + _T * 0.35; // twisted angle round it
                // flow bands: soft rings sliding from the way in to the way out
                half band = (half)(0.5 + 0.5 * sin((along / max(_FlowScale, 0.5) - _T * _FlowSpeed) * 6.2831853));
                band *= band;
                // caustic shimmer: two crossed sines, sharpened (integer multiples of the angle: no seam round the tube)
                float kc = 6.2831853 / max(_CausticScale, 0.5);
                half c1 = (half)sin(ang * 7.0 + along * kc - _T * 1.3);
                half c2 = (half)sin(ang * 5.0 - along * kc * 0.73 + _T * 0.9);
                half caustic = saturate(c1 * c2);
                caustic *= caustic;

                // the aquarium pane: the opaque world behind, bent (stronger toward the outline) and rippling
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                uv += i.bend.xy * (0.35 + 0.65 * (float)rim) + i.bend.zw;
                half3 scene = (half3)SampleSceneColor(saturate(uv));
                half sceneW = (half)saturate(_MeerkatTubeSceneOn) * _Transmit;

                half3 rimCol = _RimColor.rgb;
                half3 light = caustic * _Caustic * 0.6h + band * _Flow * 0.3h;

            #if defined(MEERKAT_TUBE_NETWORK)
                // iridescent thin film: a cosine palette walked by the Fresnel term, the way round and time
                half film = (half)frac((float)rim * _IriScale + i.sp.y + _T * 0.07);   // frac in float: _T grows past half precision
                half3 iri = 0.5h + 0.5h * cos(6.2831853h * (film + half3(0.0h, 0.33h, 0.67h)));
                rimCol = lerp(rimCol, iri, _Iri);
                // energy streaks: short bright dashes racing along the tube in lanes round it
                half lane = (half)saturate(cos(ang * max(round(_StreakLanes), 1.0)) * 4.0 - 3.0);   // thin lanes
                half dash = (half)frac(along / max(_StreakSpacing, 1.0) - _T * _StreakSpeed + 0.25 * sin(ang));   // lanes out of step
                dash = dash * dash * dash * dash;                                                 // a sharp head, a soft tail
                light += (lane * dash * _Streak) * lerp(half3(0.75h, 0.95h, 1.0h), iri, 0.5h);
                light *= (half)_Glow;
                rimCol *= (half)_Glow;
            #endif

                half haze = saturate(_Clarity + _Rim * fr + band * _Flow * 0.3h);
                half a = saturate(sceneW + haze * (1.0h - sceneW));
                half3 col = scene * _Tint.rgb * (sceneW * (1.0h - haze)) + rimCol * haze + light;
                return half4(col * k, a * k);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
