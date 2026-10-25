// Hand translation of Assets/_Graphics/Materials/Shaders/TriangleFresnelShader.shader
// (Custom/CircularGradientFresnel): fresnel colour with a see-through circle at the screen centre.
vec3 sg_objectScale() {
  return vec3(length(vec3(sg_ObjectToWorld[0][0], sg_ObjectToWorld[1][0], sg_ObjectToWorld[2][0])),
              length(vec3(sg_ObjectToWorld[0][1], sg_ObjectToWorld[1][1], sg_ObjectToWorld[2][1])),
              length(vec3(sg_ObjectToWorld[0][2], sg_ObjectToWorld[1][2], sg_ObjectToWorld[2][2])));
}
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) {
  p = sg_PosOS + sg_NrmOS * (_Spread.xyz / sg_objectScale());
  n = sg_NrmOS; t = sg_TanOS.xyz;
}
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  vec3 viewDir = normalize(sg_CamPos - sg_PosWS);
  float fresnel = pow(max(1.0 - dot(viewDir, sg_NrmWS), 0.0), _FresnelPower);
  vec4 col = mix(_BrightColor, _DarkColor, fresnel);
  vec2 c = sg_ScreenPosRaw.xy / sg_ScreenPosRaw.w * 2.0 - 1.0;
  c.y /= sg_ScreenParams.x / sg_ScreenParams.y;
  col.a *= smoothstep(_Radius, _Radius + _Fade, length(c));
  s.BaseColor = col.rgb;
  s.Alpha = col.a;
  return s;
}
