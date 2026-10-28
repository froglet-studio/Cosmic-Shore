// MeerkatTubeWall.shader — the calm inner wall of every Meerkat wormhole tube (R_VesselActions/MEERKAT.md §7a, round 26):
// a formed tunnel's and each channel of an omnicrystal's wormhole network. It is the Vessel Studio's round-21 bore
// (Docs/Studios/VesselStudio/meerkat.html, treeMaterials' glass where aR is 2, drawn by drawBore since round 26) on its own,
// because the tree that carried it ships off: a smooth sheen with no lines, _Inner x (0.55 + 0.45 x Fresnel^1.4) with the
// Fresnel at _Power, its maker's domain colour at the way in toward white at the way out, flowing slowly like water. It shows
// only the tube's live window (_F0.._F1, the share of the way along), condensing at its fronts (_Edge). Added to the scene.
// The recipe and its family: Docs/Shaders/WATERY_GLASS.md.
// Vertex data (MeerkatTreeMesh.BuildTubeWall): uv0.x = aS, the share of the way along. Positions are in world space.
Shader "CosmicShore/MeerkatTubeWall"
{
    Properties
    {
        _CIn ("Colour at the way in", Color) = (0.3, 0.85, 1, 1)
        _COut ("Colour at the way out", Color) = (1, 1, 1, 1)
        _Alpha ("Brightness", Float) = 1
        _Inner ("The calm wall's sheen", Float) = 0.1
        _Power ("Fresnel power", Float) = 3
        _F0 ("Window start (share)", Float) = 0
        _F1 ("Window end (share)", Float) = 1
        _Edge ("Window fronts (share)", Float) = 0.06
        _T ("Time (s)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+3" "IgnoreProjector" = "True" }

        Pass
        {
            Name "MeerkatTubeWall"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex MeerkatTubeWallVert
            #pragma fragment MeerkatTubeWallFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CIn, _COut;
                float _Alpha, _Inner, _Power, _F0, _F1, _Edge, _T;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;   // aS, -1, 0, 2 (MeerkatTreeMesh's layout)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float vS : TEXCOORD2;
            };

            Varyings MeerkatTubeWallVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = GetWorldSpaceViewDir(positionWS);
                o.vS = input.uv0.x;
                return o;
            }

            half4 MeerkatTubeWallFrag(Varyings i) : SV_Target
            {
                float vS = i.vS;
                // the tube's window: nothing outside it, condensing at its fronts
                if (vS < _F0 || vS > _F1) discard;
                float w = smoothstep(0.0, _Edge, min(vS - _F0, _F1 - vS));
                float flow = 0.75 + 0.25 * sin(vS * 23.0 - _T * 2.4);   // water running from the way in to the way out
                float fr = pow(1.0 - abs(dot(normalize(i.normalWS), normalize(i.viewWS))), _Power);
                float a = _Inner * (0.55 + 0.45 * pow(fr, 1.4));   // the calm wall: smooth, no lines, a gentle sheen
                return half4(lerp(_CIn.rgb, _COut.rgb, vS) * a * _Alpha * w * flow, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
