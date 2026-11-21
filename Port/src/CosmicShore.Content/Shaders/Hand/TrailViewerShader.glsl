// Hand translation of Assets/_Graphics/Materials/Shaders/TrailViewerShader.shader
// (Custom/ViewAngleBasedColorBlendHDR): a line's colour by the angle between it and the view.
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) { p = sg_PosOS; n = sg_NrmOS; t = sg_TanOS.xyz; }
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  vec3 viewDir = normalize(sg_CamPos - sg_PosWS);
  vec3 lineDir = normalize(dFdx(sg_PosWS) + dFdy(sg_PosWS));
  float angleCosine = abs(dot(lineDir, viewDir));
  vec4 c = mix(_Color1, _Color0, angleCosine);
  c.a *= _Opacity;
  c *= _Brightness;
  s.BaseColor = c.rgb;
  s.Alpha = c.a;
  return s;
}
