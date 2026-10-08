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

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float s : TEXCOORD2;
                float4 vertex : SV_POSITION;
            };

            // ShepardGraph's band position: Comparison(Start < Stop) picks a rising sweep,
            // otherwise a falling one, over (Time mod Period) / Period.
            float ShepardBand()
            {
                float lo = min(_Start, _Stop);
                float hi = max(_Start, _Stop);
                float period = max(_Period, 1e-4);
                float t = fmod(_Time.y, period) / period;
                return _Start < _Stop ? lo + (hi - lo) * t : hi - (hi - lo) * t;
            }

            v2f vert (appdata v)
            {
                v2f o;
                float s = ShepardBand();
                if (_ScaleDistance > 0.5)
                    v.vertex.xyz *= s;
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
