Shader "CosmicShore/CapsuleMembrane"
{
    Properties
    {
        [Header(Color)]
        [HDR]_BrightColor ("Bright Color", Color) = (0.22, 0.35, 0.95, 1)
        [HDR]_DarkColor ("Dark Color", Color) = (0.02, 0.03, 0.08, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 10)) = 3.0

        [Header(Radial Pulse)]
        _NoiseFrequency ("Noise Frequency", Range(0.1, 5)) = 0.6
        _NoiseAmplitude ("Noise Amplitude", Range(0, 200)) = 40.0
        _PulseSpeed ("Pulse Speed", Range(0, 3)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "CapsuleMembrane"
            Cull Back
            ZWrite On

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 _BrightColor;
            float4 _DarkColor;
            float _FresnelPower;
            float _NoiseFrequency;
            float _NoiseAmplitude;
            float _PulseSpeed;

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

                // Get this instance's world-space pivot from the transform matrix
                float3 instanceWorldPos = float3(
                    unity_ObjectToWorld._m03,
                    unity_ObjectToWorld._m13,
                    unity_ObjectToWorld._m23
                );

                // Radial direction from sphere center (assumed at parent origin)
                // The instance position IS the radial direction * radius
                float3 radialDir = normalize(instanceWorldPos);

                // Sample Perlin noise using the instance's position on the sphere
                float time = _Time.y * _PulseSpeed;
                float3 noiseCoord = instanceWorldPos * _NoiseFrequency + float3(time, time * 0.7, time * 0.3);
                float noise = cnoise(noiseCoord);

                // Offset this entire capsule radially
                float radialOffset = noise * _NoiseAmplitude;
                float3 worldOffset = radialDir * radialOffset;

                // Transform vertex to world, apply offset, then to clip
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz + worldOffset;
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                o.worldPos = worldPos;

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = pow(1.0 - saturate(abs(dot(viewDir, i.worldNormal))), _FresnelPower);

                half4 col = lerp(_DarkColor, _BrightColor, fresnel);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
