Shader "Custom/JetShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _JetPower ("Jet Power", Range(0,1)) = 1
        _AfterburnerIntensity ("Afterburner Intensity", Range(0,1)) = 0
        _AfterburnerColor ("Afterburner Color", Color) = (1,0.5,0,1)
        _MachDiamondFrequency ("Mach Diamond Frequency", Float) = 1
        _MachDiamondIntensity ("Mach Diamond Intensity", Range(0,1)) = 0.5
        _HeatDistortion ("Heat Distortion", Range(0,0.1)) = 0.01
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        LOD 100

        CGPROGRAM
        #pragma surface surf Lambert alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _JetPower;
        float _AfterburnerIntensity;
        fixed4 _AfterburnerColor;
        float _MachDiamondFrequency;
        float _MachDiamondIntensity;
        float _HeatDistortion;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            
            // Apply jet power
            c *= _JetPower;
            
            // Apply afterburner
            c = lerp(c, _AfterburnerColor, _AfterburnerIntensity);
            
            // Apply Mach diamonds
            float machPattern = sin(IN.uv_MainTex.x * _MachDiamondFrequency * 3.14159);
            c += (machPattern * _MachDiamondIntensity * _JetPower);
            
            // Apply heat distortion
            float2 distortion = sin(IN.uv_MainTex.xy * 10 + _Time.y) * _HeatDistortion;
            c += tex2D(_MainTex, IN.uv_MainTex + distortion) * 0.1;

            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}