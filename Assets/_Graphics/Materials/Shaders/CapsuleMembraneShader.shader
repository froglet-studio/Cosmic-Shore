Shader "CosmicShore/CapsuleMembrane"
{
    Properties
    {
        [Header(Spindle Surface)]
        [HDR]_BrightColor ("Bright Color", Color) = (0.37, 0.40, 0.96, 1)
        [HDR]_DullColor ("Dull Color", Color) = (0, 0.028, 1, 1)
        [HDR]_Color1 ("Color 1", Color) = (0.12, 0.10, 1.49, 0.94)
        [HDR]_Color2 ("Color 2", Color) = (0, 0, 1.30, 1)
        _CellDensity ("Cell Density", Range(0.1, 10)) = 1.5
        _Distance ("Distance Fade", Float) = 400000

        [Header(Animation)]
        _DeathAnimation ("Death Animation", Range(-0.01, 1)) = 0

        [Header(Radial Pulse)]
        _NoiseFrequency ("Noise Frequency", Range(0.001, 2)) = 0.6
        _NoiseAmplitude ("Noise Amplitude", Range(0, 200)) = 40.0
        _PulseSpeed ("Pulse Speed", Range(0, 3)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "CapsuleMembrane"
            Cull Off
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float phase : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Spindle properties
            float4 _BrightColor;
            float4 _DullColor;
            float4 _Color1;
            float4 _Color2;
            float _CellDensity;
            float _Distance;
            float _DeathAnimation;

            // Radial pulse
            float _NoiseFrequency;
            float _NoiseAmplitude;
            float _PulseSpeed;

            // ---- Hash / noise utilities ----

            // Deterministic phase from world position
            float hashPhase(float3 p)
            {
                return frac(sin(dot(p, float3(12.9898, 78.233, 45.164))) * 43758.5453) * 6.2831853;
            }

            // 2D Voronoi (matches Unity Shader Graph Voronoi node)
            float2 voronoiHash(float2 p)
            {
                float3 q = float3(
                    dot(p, float2(127.1, 311.7)),
                    dot(p, float2(269.5, 183.3)),
                    dot(p, float2(419.2, 371.9))
                );
                return frac(sin(q.xy) * 43758.5453);
            }

            float voronoi(float2 uv, float angleOffset, float cellDensity)
            {
                float2 g = floor(uv * cellDensity);
                float2 f = frac(uv * cellDensity);

                float minDist = 8.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 lattice = float2(x, y);
                        float2 offset = voronoiHash(g + lattice);

                        // Animate cell centers
                        float angle = angleOffset;
                        offset = float2(sin(angle + offset.x * 6.2831853), cos(angle + offset.y * 6.2831853)) * 0.5 + 0.5;

                        float2 diff = lattice + offset - f;
                        float dist = dot(diff, diff);
                        minDist = min(minDist, dist);
                    }
                }

                return sqrt(minDist);
            }

            // ---- 3D Perlin noise (Ashima Arts) ----
            float3 mod289(float3 x) { return x - floor(x / 289.0) * 289.0; }
            float4 mod289(float4 x) { return x - floor(x / 289.0) * 289.0; }
            float4 permute(float4 x) { return mod289((x * 34.0 + 1.0) * x); }
            float4 taylorInvSqrt(float4 r) { return 1.79284291400159 - r * 0.85373472095314; }
            float3 fade(float3 t) { return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }

            float cnoise(float3 P)
            {
                float3 Pi0 = floor(P);
                float3 Pi1 = Pi0 + 1.0;
                Pi0 = mod289(Pi0);
                Pi1 = mod289(Pi1);
                float3 Pf0 = frac(P);
                float3 Pf1 = Pf0 - 1.0;

                float4 ix = float4(Pi0.x, Pi1.x, Pi0.x, Pi1.x);
                float4 iy = float4(Pi0.y, Pi0.y, Pi1.y, Pi1.y);
                float4 iz0 = Pi0.zzzz;
                float4 iz1 = Pi1.zzzz;

                float4 ixy = permute(permute(ix) + iy);
                float4 ixy0 = permute(ixy + iz0);
                float4 ixy1 = permute(ixy + iz1);

                float4 gx0 = ixy0 / 7.0;
                float4 gy0 = frac(floor(gx0) / 7.0) - 0.5;
                gx0 = frac(gx0);
                float4 gz0 = 0.5 - abs(gx0) - abs(gy0);
                float4 sz0 = step(gz0, 0.0);
                gx0 -= sz0 * (step(0.0, gx0) - 0.5);
                gy0 -= sz0 * (step(0.0, gy0) - 0.5);

                float4 gx1 = ixy1 / 7.0;
                float4 gy1 = frac(floor(gx1) / 7.0) - 0.5;
                gx1 = frac(gx1);
                float4 gz1 = 0.5 - abs(gx1) - abs(gy1);
                float4 sz1 = step(gz1, 0.0);
                gx1 -= sz1 * (step(0.0, gx1) - 0.5);
                gy1 -= sz1 * (step(0.0, gy1) - 0.5);

                float3 g000 = float3(gx0.x, gy0.x, gz0.x);
                float3 g100 = float3(gx0.y, gy0.y, gz0.y);
                float3 g010 = float3(gx0.z, gy0.z, gz0.z);
                float3 g110 = float3(gx0.w, gy0.w, gz0.w);
                float3 g001 = float3(gx1.x, gy1.x, gz1.x);
                float3 g101 = float3(gx1.y, gy1.y, gz1.y);
                float3 g011 = float3(gx1.z, gy1.z, gz1.z);
                float3 g111 = float3(gx1.w, gy1.w, gz1.w);

                float4 norm0 = taylorInvSqrt(float4(dot(g000,g000), dot(g010,g010), dot(g100,g100), dot(g110,g110)));
                g000 *= norm0.x; g010 *= norm0.y; g100 *= norm0.z; g110 *= norm0.w;
                float4 norm1 = taylorInvSqrt(float4(dot(g001,g001), dot(g011,g011), dot(g101,g101), dot(g111,g111)));
                g001 *= norm1.x; g011 *= norm1.y; g101 *= norm1.z; g111 *= norm1.w;

                float n000 = dot(g000, Pf0);
                float n100 = dot(g100, float3(Pf1.x, Pf0.yz));
                float n010 = dot(g010, float3(Pf0.x, Pf1.y, Pf0.z));
                float n110 = dot(g110, float3(Pf1.xy, Pf0.z));
                float n001 = dot(g001, float3(Pf0.xy, Pf1.z));
                float n101 = dot(g101, float3(Pf1.x, Pf0.y, Pf1.z));
                float n011 = dot(g011, float3(Pf0.x, Pf1.yz));
                float n111 = dot(g111, Pf1);

                float3 f = fade(Pf0);
                float4 n_z = lerp(float4(n000, n100, n010, n110), float4(n001, n101, n011, n111), f.z);
                float2 n_yz = lerp(n_z.xy, n_z.zw, f.y);
                return lerp(n_yz.x, n_yz.y, f.x);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Instance world-space pivot
                float3 instanceWorldPos = float3(
                    unity_ObjectToWorld._m03,
                    unity_ObjectToWorld._m13,
                    unity_ObjectToWorld._m23
                );

                // Per-instance phase derived from position on sphere
                o.phase = hashPhase(instanceWorldPos);

                // Radial direction
                float3 radialDir = normalize(instanceWorldPos);

                // Perlin noise radial offset
                float time = _Time.y * _PulseSpeed;
                float3 noiseCoord = instanceWorldPos * _NoiseFrequency + float3(time, time * 0.7, time * 0.3);
                float noise = cnoise(noiseCoord);
                float3 worldOffset = radialDir * (noise * _NoiseAmplitude);

                // Transform vertex to world + offset
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz + worldOffset;
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                o.worldPos = worldPos;
                o.uv = v.uv;

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = normalize(i.worldNormal);

                // ---- Inverse Fresnel Power 4 (matches SpindleGraph sub-graph) ----
                float NdotV = saturate(dot(viewDir, normal));
                float inverseFresnel = NdotV * NdotV;
                inverseFresnel = inverseFresnel * inverseFresnel; // pow4

                // ---- Voronoi cellular pattern ----
                float angleOffset = _Time.y + i.phase;
                float voronoiDist = voronoi(i.uv, angleOffset, _CellDensity);

                // ---- Squared distance fade ----
                float3 objWorldPos = float3(
                    unity_ObjectToWorld._m03,
                    unity_ObjectToWorld._m13,
                    unity_ObjectToWorld._m23
                );
                float3 toCam = _WorldSpaceCameraPos - objWorldPos;
                float sqrDist = dot(toCam, toCam);
                float distanceFade = saturate(sqrDist / max(_Distance, 0.001));

                // ---- Color ----
                // Blend BrightColor / DullColor by inverse Fresnel
                float3 baseColor = lerp(_DullColor.rgb, _BrightColor.rgb, inverseFresnel);

                // ---- Alpha ----
                // Voronoi drives transparency, modulated by distance
                float voronoiAlpha = 1.0 - voronoiDist;
                float alpha = voronoiAlpha * distanceFade;

                // Clamp and add inverse fresnel contribution (face-on = more opaque)
                alpha = saturate(alpha + inverseFresnel * 0.3);

                // ---- Death animation ----
                if (_DeathAnimation > 0)
                {
                    // Dissolve: reduce alpha based on death progress
                    alpha *= 1.0 - _DeathAnimation;
                    clip(alpha - 0.05);
                }

                clip(alpha - 0.05);

                half4 col = half4(baseColor, alpha);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
