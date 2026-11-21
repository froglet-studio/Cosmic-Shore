// Hand translation of Assets/_Graphics/Materials/Shaders/SpreadFresnelShader.shader
// (Custom/SpreadFresnelShader): the crystal bodies' two-colour fresnel, pushed out along the normal.
vec3 sg_objectScale() {
  return vec3(length(vec3(sg_ObjectToWorld[0][0], sg_ObjectToWorld[1][0], sg_ObjectToWorld[2][0])),
              length(vec3(sg_ObjectToWorld[0][1], sg_ObjectToWorld[1][1], sg_ObjectToWorld[2][1])),
              length(vec3(sg_ObjectToWorld[0][2], sg_ObjectToWorld[1][2], sg_ObjectToWorld[2][2])));
}
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) {
  // Unity's unity_ObjectToWorld._m00_m01_m02 is the matrix's first ROW.
  p = sg_PosOS + normalize(sg_NrmOS) * (_Spread.xyz / sg_objectScale());
  n = sg_NrmOS; t = sg_TanOS.xyz;
}
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  vec3 viewDir = normalize(sg_CamPos - sg_PosWS);
  float fresnel = (1.0 + dot(viewDir, sg_NrmWS)) / 2.0;
  vec4 col = mix(_BrightColor, _DarkColor, fresnel);
  s.BaseColor = col.rgb;
  s.Alpha = col.a;
  return s;
}
