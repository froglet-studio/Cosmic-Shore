Shader "Custom/SpikeShader"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _EdgeGlow ("Edge Glow", Range(0, 1)) = 0.2
        _WaveAmplitude ("Wave Amplitude", Range(0, 1)) = 0.1
        _WaveFrequency ("Wave Frequency", Range(0, 10)) = 1
        _SpikeHeight ("Spike Height", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
#include "UnityCG.cginc"

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float4 vertex : SV_POSITION;
};

float4 _Color;
float _EdgeGlow;
float _WaveAmplitude;
float _WaveFrequency;
float _SpikeHeight;

v2f vert(appdata v)
{
    v2f o;
                // Spike effect
    float spikeFactor = 4.0 * (v.uv.x - 0.5) * (v.uv.x - 0.5);
    v.vertex.y += _SpikeHeight / (spikeFactor + 0.01);
                
                // Sin Wave Distortion
    v.vertex.y += _WaveAmplitude * sin(v.vertex.x * _WaveFrequency + _Time.y);
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    return o;
}

half4 frag(v2f i) : SV_Target
{
    half4 col = _Color;
    half edgeFactor = min(i.uv.x, 1.0 - i.uv.x);
    col.rgb += _EdgeGlow * edgeFactor; // Edge Glow effect
    return col;
}ENDCG
        }
    }
}

