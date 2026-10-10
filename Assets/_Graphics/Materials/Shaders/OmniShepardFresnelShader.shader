// The omni crystal's Shepard-tone TRIANGLES, in the same shader family as its body
// (OmniCrystalFresnelShader) so the two read as ONE crystal. Docs/PALETTE.md §2.10.
//
// COLOUR is the body's, verbatim: lerp(_BrightColor, _DarkColor, (1 + N.V) / 2) on the same two
// properties - dark where a face looks at the camera, bright toward the silhouette. A triangle in
// flight is therefore the body's own triangle, only see-through; it can never drift to a different
// lime than the plate it lands on, and a domain recolour (ThemeManager) paints both alike.
//
// MOTION and ALPHA are ShepardGraph's, transcribed: s sweeps _Start -> _Stop once per _Period
// (rising when _Start < _Stop, falling otherwise), the mesh is scaled by s about the origin when
// _ScaleDistance is on, and Alpha = (1.05 - s) * _Opacity, clipped at 0.01.
//
// Two OPT-IN contrast controls, both inert at their defaults (so the omni triangles, which author
// neither, draw exactly the formula above):
//   _RimPower    the bright weight is rim^_RimPower, where rim = 1 - fresnel above. 1 is the linear
//                SpreadFresnel ramp; higher pulls the bright colour out to the silhouette.
//   _FaceForward measures N.V on the side FACING the camera. These shells are transparent and
//                Cull Off, so a shell's back faces show through its front ones, and on the formula
//                above a back face is always at the BRIGHT end - which washes a see-through crystal
//                toward its bright colour. Facing it, rim = 1 - |N.V|: dark at the centre of every
//                face, bright only edge-on, front and back alike. The Mass crystal turns both on
//                (Tools/Build/author_mass_crystal_look.py).
//
// Four OPT-IN motion/shape controls (Omni Shepard Lab round 4), all inert at their defaults, so
// every material that does not author them (the Mass crystal's shells) draws exactly as before:
//   _Breathe        the band PING-PONGS: s runs _Start -> _Stop -> _Start once per _Period
//                   instead of sweeping once and jumping back. With breathing on, _Start/_Stop are
//                   the WHOLE trip and _Period the whole round trip; layers share it and differ only
//                   in _PhaseOffset.
//   _PhaseOffset    a fraction of _Period added to this layer's clock (layer k of N: k / N).
//   _PlateScaleStart  each plate's own size multiplier at _Start, easing to 1 at _Stop (about the
//                   plate's own centre, so it changes how big a triangle is, not where it flies).
//   _Thickness      each plate stretched along its outward direction about its own centre.
// The last two need every vertex's PLATE CENTRE, which Tools/Build/author_omni_crystal_triangles.py
// bakes into OmniCrystalTriangles.asset's TEXCOORD2. A mesh without it reads (0,0,0), which is
// harmless at the defaults: c * s + (v - c) * s == v * s.
//
// Like the body, it does NOT read FadeIn's lowercase _opacity: the omni appears at once on respawn
// (see OmniCrystalFresnelShader for why).
Shader "Custom/OmniShepardFresnelShader"
{
    Properties
    {
        [HDR]_BrightColor ("Bright Color", Color) = (1,1,1,1)
        [HDR]_DarkColor ("Dark Color", Color) = (0,0,0,1)
        _Start ("Start", Float) = 1
        _Stop ("Stop", Float) = 0.5
        _Period ("Period", Float) = 3
        [Toggle] _ScaleDistance ("Scale Distance", Float) = 1
        _Opacity ("Opacity", Range(0, 1)) = 1
        _RimPower ("Rim Power", Range(0.25, 8)) = 1
        [ToggleUI] _FaceForward ("Face Forward (see-through contrast)", Float) = 0
        [ToggleUI] _Breathe ("Breathe (ping-pong the band)", Float) = 0
        _PhaseOffset ("Phase Offset (fraction of a period)", Float) = 0
        _PlateScaleStart ("Plate Scale at Start", Range(0.1, 3)) = 1
        _Thickness ("Plate Thickness", Range(0.2, 3)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "UniversalMaterialType" = "Unlit"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }
        Pass
        {
            Name "CustomPass"
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            float4 _BrightColor, _DarkColor;
            float _Start;
            float _Stop;
            float _Period;
            float _ScaleDistance;
            float _Opacity;
            float _RimPower;
            float _FaceForward;
            float _Breathe;
            float _PhaseOffset;
            float _PlateScaleStart;
            float _Thickness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 plateCentre : TEXCOORD2;   // baked per vertex; (0,0,0) on meshes without it
            };

            struct v2f
            {
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float s : TEXCOORD2;
                float4 vertex : SV_POSITION;
            };

            // ShepardGraph's band position: Comparison(Start < Stop) picks a rising sweep,
            // otherwise a falling one, over (Time mod Period) / Period. _PhaseOffset shifts this
            // layer's clock (frac of a value already in [0, 1) is that value, so 0 changes nothing);
            // _Breathe folds the sweep into a there-and-back.
            float ShepardBand()
            {
                float lo = min(_Start, _Stop);
                float hi = max(_Start, _Stop);
                float period = max(_Period, 1e-4);
                float t = frac(fmod(_Time.y, period) / period + _PhaseOffset);
                if (_Breathe > 0.5) t = 1.0 - abs(1.0 - 2.0 * t);
                return _Start < _Stop ? lo + (hi - lo) * t : hi - (hi - lo) * t;
            }

            // The shell scaled by s about the crystal centre, each plate sized and thickened about
            // its OWN centre c. u is how far along the band this layer is (0 at _Start, 1 at _Stop).
            // At _PlateScaleStart 1 and _Thickness 1 this is v * s exactly (c * s + (v - c) * s).
            float3 ShepardShellPosition(float3 v, float3 c, float s)
            {
                float span = _Stop - _Start;
                float u = abs(span) > 1e-5 ? saturate((s - _Start) / span) : 1.0;
                float3 local = v - c;
                float cl = length(c);
                if (cl > 1e-5)
                {
                    float3 dir = c / cl;
                    local += dir * dot(local, dir) * (_Thickness - 1.0);
                }
                return c * s + local * (s * lerp(_PlateScaleStart, 1.0, u));
            }

            v2f vert (appdata v)
            {
                v2f o;
                float s = ShepardBand();
                if (_ScaleDistance > 0.5)
                    v.vertex.xyz = ShepardShellPosition(v.vertex.xyz, v.plateCentre, s);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = normalize(mul((float3x3)UNITY_MATRIX_M, v.normal));
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.s = s;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float alpha = (1.05 - i.s) * _Opacity;
                clip(alpha - 0.01);

                // The body's colour (OmniCrystalFresnelShader / SpreadFresnelShader): at the
                // defaults rim = 1 - (1 + N.V) / 2 and this is lerp(_BrightColor, _DarkColor,
                // fresnel) verbatim. See the header for _RimPower / _FaceForward.
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float ndv = dot(viewDir, i.worldNormal);
                float rim = _FaceForward > 0.5 ? 1.0 - abs(ndv) : (1.0 - ndv) * 0.5;
                half4 col = lerp( _DarkColor, _BrightColor, pow(saturate(rim), _RimPower));
                return half4(col.rgb, saturate(alpha));
            }
            ENDCG
        }
    }
}
