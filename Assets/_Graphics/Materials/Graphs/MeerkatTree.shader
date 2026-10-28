// MeerkatTree.shader — the Meerkat's TREE (the dead tree round every tunnel) and the bark of its ROOTS
// (R_VesselActions/MEERKAT.md §7), the Vessel Studio's treeMaterials glass (Docs/Studios/VesselStudio/meerkat.html,
// rounds 13-15): the wall's own watery glass - see-through face-on (_Glass), bright at its silhouette (_Rim at the
// _Power Fresnel), the bark's fissures lit along the wood (_Grain, _GrainN), tree rings on the cut faces (_Rings), all of
// it flowing slowly from the way in to the way out like water. It shows only the tunnel's live window (_F0.._F1, the
// share of the way along), condensing at its fronts (_Edge); each branch grows out of its root by _Bg[i]. Added to the
// scene. The recipe and its family: Docs/Shaders/WATERY_GLASS.md.
// Vertex data (MeerkatTreeMesh): uv0 = (aS, aBi, aCap, aR), uv1 = (aB, aPhi), uv2 = aBase. Positions are in world space.
Shader "CosmicShore/MeerkatTree"
{
    Properties
    {
        _CIn ("Colour at the way in", Color) = (0.3, 0.85, 1, 1)
        _COut ("Colour at the way out", Color) = (1, 1, 1, 1)
        _Alpha ("Brightness", Float) = 1
        _Glass ("Glass face-on", Float) = 0.012
        _Rim ("Glass at the silhouette", Float) = 0.28
        _Power ("Fresnel power", Float) = 3
        _Rings ("Tree rings on the cut faces", Float) = 7
        _Grain ("Bark grain", Float) = 0.22
        _GrainN ("Fissures round the wood", Float) = 22
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
            Name "MeerkatTree"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex MeerkatTreeVert
            #pragma fragment MeerkatTreeFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CIn, _COut;
                float _Alpha, _Glass, _Rim, _Power, _Rings, _Grain, _GrainN, _F0, _F1, _Edge, _T;
            CBUFFER_END
            float _Bg[16];   // each branch's growth, 0..1 (set per tree from a MaterialPropertyBlock)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;   // aS, aBi, aCap, aR
                float2 uv1 : TEXCOORD1;   // aB, aPhi
                float3 uv2 : TEXCOORD2;   // aBase
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float4 sCapR : TEXCOORD2;   // aS, aCap, aR, the branch's growth
                float2 bPhi : TEXCOORD3;    // aB, aPhi
            };

            Varyings MeerkatTreeVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float g = 1.0;
                float3 p = input.positionOS.xyz;
                if (input.uv0.y > -0.5)   // a branch grows out of its root
                {
                    g = _Bg[(int)(input.uv0.y + 0.5)];
                    p = input.uv2 + (p - input.uv2) * g;
                }
                float3 positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = GetWorldSpaceViewDir(positionWS);
                o.sCapR = float4(input.uv0.x, input.uv0.z, input.uv0.w, g);
                o.bPhi = input.uv1;
                return o;
            }

            half4 MeerkatTreeFrag(Varyings i) : SV_Target
            {
                float vS = i.sCapR.x, vCap = i.sCapR.y, vR = i.sCapR.z, vG = i.sCapR.w, vB = i.bPhi.x, vPhi = i.bPhi.y;
                // the tunnel's window: nothing outside it, condensing at its fronts
                if (vG < 0.01 || vS < _F0 || vS > _F1) discard;
                float w = smoothstep(0.0, _Edge, min(vS - _F0, _F1 - vS));
                float flow = 0.75 + 0.25 * sin((vS + vB) * 23.0 - _T * 2.4);   // water running top to bottom
                float fr = pow(1.0 - abs(dot(normalize(i.normalWS), normalize(i.viewWS))), _Power);   // the wall's glass: Fresnel
                float a = _Glass + _Rim * fr;
                // the bark's grain - fissures running along the wood, broken into lengths
                float along = vS * 9.0 + vB * 5.0, wob = 0.18 * sin(along * 1.7 + vPhi * 11.0);
                float fissure = pow(abs(sin((vPhi * _GrainN + wob) * 3.14159)), 18.0) * smoothstep(0.25, 0.6, frac(along + vPhi * 3.7));
                if (vCap < 0.5) a += _Grain * fissure * (0.35 + 0.65 * fr);
                else a = _Glass + _Rim * (0.6 * pow(0.5 + 0.5 * cos(vR * _Rings * 6.2831853), 8.0) + 0.4 * smoothstep(0.85, 1.0, vR));   // tree rings
                return half4(lerp(_CIn.rgb, _COut.rgb, vS) * a * _Alpha * w * flow, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
