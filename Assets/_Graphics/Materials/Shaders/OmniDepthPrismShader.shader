// The omni and Mass crystals' triangular prisms, "sealed by depth" (Omni Shepard Lab round 7, the Depth
// ratchet; Docs/Studios/OMNI_SHEPARD_LAB.md). Opaque, on the BODY's look: OmniCrystalFresnelShader's colour
// formula, _Spread push and _Opacity screen-door dissolve, transcribed, so a sealed prism is the body's own
// plate.
//
// Each prism rides a column over its hole. Where it is in the column (u: 0 at the top, 1 sealed) and how solid
// it is come per plate from CrystalDepthPrisms, which steps the ratchet against the camera and writes
// _DepthState[plate] = (u, fade, leaving prism's u, 0) and _DepthDriven = 1 through a property block. The plate
// index and centre are baked into TEXCOORD2 (xyz centre, w index). Undriven (_DepthDriven 0: a husk, a codex
// model, a material without its driver) every prism draws sealed and solid - the whole crystal at rest.
//
// The mesh is authored SEATED: at u = 1 a vertex is exactly where the mesh has it. Toward the top of the column
// the prism moves out along its centre direction to _Reach x its radius, shrinks to _PlateScaleStart of its
// size, and its five faces part along their own normals by _FaceSplit plate radii (_SplitUnit = one plate
// radius in mesh units). _Thickness stretches each prism along its column; with _ThicknessInward 1 it grows
// from its top face (_TopHeight above the plate centre) into the crystal, so a sealed prism stays flush with
// the body at any thickness.
//
// Two passes draw the two prisms a column can hold at once: the arriving one (pass 0, u = x of the state) and,
// during a reseat, the leaving one fading out in its hole (pass 1, u = z). They dither on COMPLEMENTARY pixels
// (noise vs 1 - noise), so the swap never shows a gap or a double. URP draws an untagged (SRPDefaultUnlit)
// pass and a UniversalForward pass of the same material in the same frame.
//
// Like the body it deliberately does NOT read FadeIn's lowercase _opacity (see OmniCrystalFresnelShader).
//
// JS twin: VS_SHELL's depth block in Docs/Studios/OmniShepardLab.html, checked against this file by
// Tools/Build/omni_depth_prism_shader_parity.py. Change one, change both.
Shader "Custom/OmniDepthPrismShader"
{
    Properties
    {
        [HDR]_BrightColor ("Bright Color", Color) = (1,1,1,1)
        [HDR]_DarkColor ("Dark Color", Color) = (0,0,0,1)
        _Spread ("Spread", Vector) = (1,1,1,0)
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 5
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Reach ("Top of Column (x plate radius)", Range(1, 4)) = 2
        _PlateScaleStart ("Size at Top of Column", Range(0.1, 2)) = 0.5
        _Thickness ("Plate Thickness", Range(0.2, 8)) = 1
        [Toggle] _ThicknessInward ("Thickness Grows Inward", Float) = 1
        _TopHeight ("Top Face Height (mesh units)", Float) = 0
        _FaceSplit ("Face Split (plate radii at the top)", Range(0, 2)) = 0
        _SplitUnit ("Plate Radius (mesh units)", Float) = 0
        _DepthDriven ("Driven by CrystalDepthPrisms", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Unlit"
            "Queue"="Geometry"
        }

        CGINCLUDE
        #include "UnityCG.cginc"

        float3 _Spread;
        float _FresnelPower;
        float4 _BrightColor, _DarkColor;
        float _Opacity;
        float _Reach, _PlateScaleStart, _Thickness, _ThicknessInward, _TopHeight, _FaceSplit, _SplitUnit;
        float _DepthDriven;
        float4 _DepthState[60];   // per plate: (arriving u, its fade, leaving u, -) - CrystalDepthPrisms

        // OmniCrystalFresnelShader's push: out along the normal by _Spread WORLD units.
        float3 SpreadFresnelDisplace(float3 positionOS, float3 normalOS)
        {
            float3 objectScale = float3(length(unity_ObjectToWorld._m00_m01_m02), length(unity_ObjectToWorld._m10_m11_m12), length(unity_ObjectToWorld._m20_m21_m22));
            return positionOS + normalize(normalOS) * (_Spread / objectScale);
        }

        // OmniCrystalFresnelShader's face colour: dark where the face looks at the camera, bright toward the rim.
        half4 SpreadFresnelColor(float3 worldPos, float3 worldNormal)
        {
            float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
            float fresnel = (1.0 + dot(viewDir, worldNormal)) / 2;
            return lerp(_BrightColor, _DarkColor, fresnel);
        }

        // Where one vertex of a prism is at column place u (object space, before the spread).
        float3 DepthPrismPosition(float3 vertex, float3 normal, float3 centre, float u)
        {
            float3 dir = normalize(centre);
            float rr = length(centre);
            float k = lerp(_Reach, 1.0, u);
            float3 local = vertex - centre;
            local += dir * (dot(local, dir) - _TopHeight * _ThicknessInward) * (_Thickness - 1.0);
            float3 fn = normalize(normal);
            local += fn * sign(dot(fn, vertex - centre)) * _FaceSplit * _SplitUnit * (1.0 - u);
            float ls = k * lerp(_PlateScaleStart, 1.0, u);
            return dir * rr * k + local * ls;
        }

        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 plate : TEXCOORD2;   // xyz: the plate's centre; w: its index
        };

        struct v2f
        {
            float3 worldNormal : TEXCOORD0;
            float3 worldPos : TEXCOORD1;
            float alpha : TEXCOORD2;
            float4 vertex : SV_POSITION;
        };

        v2f DepthPrismVert(appdata v, float leaving)
        {
            v2f o;
            float4 st = _DepthState[(int)(v.plate.w + 0.5)];
            bool driven = _DepthDriven > 0.5;
            float u = driven ? (leaving > 0.5 ? st.z : st.x) : 1.0;
            o.alpha = driven ? (leaving > 0.5 ? 1.0 - st.y : st.y) : (leaving > 0.5 ? 0.0 : 1.0);

            float3 p = DepthPrismPosition(v.vertex.xyz, v.normal, v.plate.xyz, u);
            v.vertex.xyz = SpreadFresnelDisplace(p, v.normal);
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.worldNormal = normalize(mul((float3x3)UNITY_MATRIX_M, v.normal));
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            // A prism with nothing to show collapses to a point: no fragments, no clip work.
            if (o.alpha <= 0.0) o.vertex = float4(0, 0, 0, 1);
            return o;
        }

        half4 DepthPrismFrag(v2f i, float leaving)
        {
            // Interleaved gradient noise, nudged strictly inside (0,1): clip(0) KEEPS a fragment. The leaving
            // prism takes the complementary pixels, so the two together always cover the plate exactly once.
            float n = frac(52.9829189 * frac(dot(i.vertex.xy, float2(0.06711056, 0.00583715))));
            float threshold = n * 0.998 + 0.001;
            if (leaving > 0.5) threshold = 1.0 - threshold;
            clip(i.alpha * saturate(_Opacity) - threshold);
            return SpreadFresnelColor(i.worldPos, normalize(i.worldNormal));
        }
        ENDCG

        Pass
        {
            Name "ArrivingPrism"
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            v2f vert(appdata v) { return DepthPrismVert(v, 0.0); }
            half4 frag(v2f i) : SV_Target { return DepthPrismFrag(i, 0.0); }
            ENDCG
        }

        Pass
        {
            Name "LeavingPrism"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            v2f vert(appdata v) { return DepthPrismVert(v, 1.0); }
            half4 frag(v2f i) : SV_Target { return DepthPrismFrag(i, 1.0); }
            ENDCG
        }
    }
}
