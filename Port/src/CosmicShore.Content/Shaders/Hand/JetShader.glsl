// Hand translation of Assets/_Scripts/Game/Vessel/Animation/JetShader.shader (Custom/JetShader):
// a Lambert surface shader with alpha:fade - the vessel's exhaust.
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) { p = sg_PosOS; n = sg_NrmOS; t = sg_TanOS.xyz; }
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  vec2 uv = sg_Uv0.xy * _MainTex_ST.xy + _MainTex_ST.zw;
  vec4 c = texture(_MainTex, uv) * _Color;
  c *= _JetPower;
  c = mix(c, _AfterburnerColor, _AfterburnerIntensity);
  float machPattern = sin(uv.x * _MachDiamondFrequency * 3.14159);
  c += vec4(machPattern * _MachDiamondIntensity * _JetPower);
  vec2 distortion = sin(uv * 10.0 + sg_Time.x) * _HeatDistortion;
  c += texture(_MainTex, uv + distortion) * 0.1;
  s.BaseColor = c.rgb;
  s.Alpha = c.a;
  s.Metallic = 0.0;
  return s;
}
