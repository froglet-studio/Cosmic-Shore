// Hand translation of Assets/_Graphics/Materials/Shaders/OmniCrystalFresnelShader.shader
// (Custom/OmniCrystalFresnelShader): the omni crystal body. The morph toward the prism shape reads
// mesh channels TEXCOORD2/3, which the graph template does not carry: drawn unmorphed (ease 0),
// i.e. the SpreadFresnel body, with _Opacity's screen-door coverage.
uniform float _PrismClock;
vec3 sg_objectScale() {
  return vec3(length(vec3(sg_ObjectToWorld[0][0], sg_ObjectToWorld[1][0], sg_ObjectToWorld[2][0])),
              length(vec3(sg_ObjectToWorld[0][1], sg_ObjectToWorld[1][1], sg_ObjectToWorld[2][1])),
              length(vec3(sg_ObjectToWorld[0][2], sg_ObjectToWorld[1][2], sg_ObjectToWorld[2][2])));
}
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) {
  p = sg_PosOS + normalize(sg_NrmOS) * (_Spread.xyz / sg_objectScale());
  n = sg_NrmOS; t = sg_TanOS.xyz;
}
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  float coverage = clamp(_Opacity, 0.0, 1.0);
  float nz = fract(52.9829189 * fract(dot(gl_FragCoord.xy, vec2(0.06711056, 0.00583715))));
  if (coverage - (nz * 0.998 + 0.001) < 0.0) discard;
  vec3 viewDir = normalize(sg_CamPos - sg_PosWS);
  float fresnel = (1.0 + dot(viewDir, sg_NrmWS)) / 2.0;
  vec4 col = mix(_BrightColor, _DarkColor, fresnel);
  s.BaseColor = col.rgb;
  s.Alpha = col.a;
  return s;
}
