// Hand translation of Assets/_Graphics/Materials/Shaders/OmniCrystalFresnelShader.shader
// (Custom/OmniCrystalFresnelShader): the omni crystal body. SpreadFresnelShader's look
// (lerp(_BrightColor, _DarkColor, (1 + N.V) / 2) after a _Spread push), plus the CrystalMorph
// path: a stamped _CrystalMorph moves each vertex to its TEXCOORD2 target and normal to its
// TEXCOORD3, and the colour hands over face by face to the prism formula on the same ease.
// _Opacity is spent as screen-door coverage.
uniform float _PrismClock;
vec3 sg_objectScale() {
  return vec3(length(vec3(sg_ObjectToWorld[0][0], sg_ObjectToWorld[1][0], sg_ObjectToWorld[2][0])),
              length(vec3(sg_ObjectToWorld[0][1], sg_ObjectToWorld[1][1], sg_ObjectToWorld[2][1])),
              length(vec3(sg_ObjectToWorld[0][2], sg_ObjectToWorld[1][2], sg_ObjectToWorld[2][2])));
}
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) {
  vec3 spread = sg_PosOS + normalize(sg_NrmOS) * (_Spread.xyz / sg_objectScale());
  CrystalMorph_float(spread, sg_Uv2, _PrismClock, _CrystalMorph.xyz, p);
  CrystalMorphNormal_float(sg_NrmOS, sg_Uv3, _PrismClock, _CrystalMorph.xyz, n);
  t = sg_TanOS.xyz;
}
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  float coverage = clamp(_Opacity, 0.0, 1.0);
  float nz = fract(52.9829189 * fract(dot(gl_FragCoord.xy, vec2(0.06711056, 0.00583715))));
  if (coverage - (nz * 0.998 + 0.001) < 0.0) discard;
  vec3 viewDir = normalize(sg_CamPos - sg_PosWS);
  float fresnel = (1.0 + dot(viewDir, sg_NrmWS)) / 2.0;
  vec4 body = mix(_BrightColor, _DarkColor, fresnel);
  // PrismFresnelColor (BlockGraph's FresnelColors): back faces keep (d + 1) * 0.2, power 4.
  float d = dot(normalize(sg_NrmWS), viewDir);
  float x = d > 0.0 ? d : (d + 1.0) * 0.2;
  float f = (1.0 - x) * (1.0 - x);
  f *= f;
  vec4 prism = mix(_DarkColor, _BrightColor, f);
  float ease = _CrystalMorph.y > 0.0 ? sgCrystalMorphEase(sg_Uv2, _PrismClock, _CrystalMorph.xyz) : 0.0;
  vec4 col = mix(body, prism, ease);
  s.BaseColor = col.rgb;
  s.Alpha = col.a;
  return s;
}
