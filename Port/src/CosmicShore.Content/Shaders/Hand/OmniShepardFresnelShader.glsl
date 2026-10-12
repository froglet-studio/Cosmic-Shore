// Hand translation of Assets/_Graphics/Materials/Shaders/OmniShepardFresnelShader.shader
// (Custom/OmniShepardFresnelShader): the omni crystal's Shepard-tone triangles.
// Render state and property uniforms come from the .shader itself (ShaderLabSource).
float ShepardBand() {
  float lo = min(_Start, _Stop);
  float hi = max(_Start, _Stop);
  float period = max(_Period, 1e-4);
  float t = mod(sg_Time.x, period) / period;
  return _Start < _Stop ? lo + (hi - lo) * t : hi - (hi - lo) * t;
}
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) {
  p = sg_PosOS;
  if (_ScaleDistance > 0.5) p *= ShepardBand();
  n = sg_NrmOS; t = sg_TanOS.xyz;
}
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  float alpha = (1.05 - ShepardBand()) * _Opacity;
  if (alpha - 0.01 < 0.0) discard;
  vec3 viewDir = normalize(sg_CamPos - sg_PosWS);
  float ndv = dot(viewDir, sg_NrmWS);
  float rim = _FaceForward > 0.5 ? 1.0 - abs(ndv) : (1.0 - ndv) * 0.5;
  vec4 col = mix(_DarkColor, _BrightColor, pow(clamp(rim, 0.0, 1.0), _RimPower));
  s.BaseColor = col.rgb;
  s.Alpha = clamp(alpha, 0.0, 1.0);
  return s;
}
