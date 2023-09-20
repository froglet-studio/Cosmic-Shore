// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader"Custom/FresnelLineShader"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _BlendColor ("Blend Color", Color) = (0, 0, 0, 1)
        _OverlayIntensity ("Overlay Intensity", Range(0, 1)) = 0.5
        _LineWidth ("Line Width", Range(0, 5)) = 1.0
        
        _NumberOfPulses ("Number of Pulses", Range(1, 4)) = 1
        _PulseColors ("Pulse Colors", Color) = (1, 0, 0, 1)
        _PulseFrequencies ("Pulse Frequencies", Vector) = (1, 1, 1, 1)
        _PulseSpeeds ("Pulse Speeds", Vector) = (1, 1, 1, 1)
        _PulseAmplitudes ("Pulse Amplitudes", Vector) = (1, 1, 1, 1)
        
        _StartFadeDistance ("Start Fade Distance", float) = 0.0
        _EndFadeDistance ("End Fade Distance", float) = 1.0
        
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
    float4 nextVertex : TEXCOORD1;
    float2 uv : TEXCOORD0;
};


struct v2f
{
    float2 uv : TEXCOORD0;
    float fresnel : TEXCOORD1;
    float4 vertex : SV_POSITION;
    float3 worldPos : TEXCOORD2;
    float3 nextWorldPos : TEXCOORD3;
};

float4 _Color;
float4 _BlendColor;
float _OverlayIntensity;
float _LineWidth;

int _NumberOfPulses;
float4 _PulseColors;
float4 _PulseFrequencies;
float4 _PulseSpeeds;
float4 _PulseAmplitudes;

float _StartFadeDistance;
float _EndFadeDistance;

float _EdgeGlow;
float _WaveAmplitude;
float _WaveFrequency;
float _SpikeHeight;


v2f vert(appdata v)
{
    v2f o;

    // Compute world position of the vertex
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.nextWorldPos = mul(unity_ObjectToWorld, v.nextVertex).xyz;

    // Spike effect
    float spikeFactor = 4.0 * (v.uv.x - 0.5) * (v.uv.x - 0.5);
    v.vertex.y += _SpikeHeight / (spikeFactor + 0.01);
    
    // Sin Wave Distortion
    v.vertex.y += _WaveAmplitude * sin(v.vertex.x * _WaveFrequency + _Time.y);

    // Fresnel calculation
    float3 viewDirection = normalize(_WorldSpaceCameraPos - o.worldPos); // Direction pointing from vertex to camera
    float2 direction = float2(1, 0); // Horizontal direction for line
    o.fresnel = dot(direction, viewDirection.xy);
    o.fresnel = (o.fresnel + 1.0) * 0.5;

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    return o;
}

half4 frag(v2f i) : SV_Target
{
    half4 col = _Color;

    // Compute direction based on the vertices the fragment is between
    float3 direction = normalize(i.nextWorldPos - i.worldPos);

    // Calculate view direction
    float3 viewDirection = normalize(_WorldSpaceCameraPos - i.worldPos);

    // Fresnel calculation
    float fresnelValue = dot(direction, viewDirection);
    fresnelValue = (fresnelValue + 1.0) * 0.5;

    // Fresnel blending
    col = lerp(col, _BlendColor, fresnelValue * _OverlayIntensity);

    //// Edge Glow effect
    //half edgeFactor = min(i.uv.x, 1.0 - i.uv.x);
    //col.rgb += _EdgeGlow * edgeFactor;

    return col;
}ENDCG
        }
    }
}

