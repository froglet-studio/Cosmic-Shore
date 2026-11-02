// Hand translation of Assets/_Graphics/Materials/Shaders/CrystalChargeArcsShader.shader
// (Custom/CrystalChargeArcsShader): the Charge element's light, drawn additively over the crystal.
// CrystalChargeArcs writes one mesh per crystal each frame; TEXCOORD0 = (across the strip -1..1,
// falloff from the light's centre 0..1, kind, intensity). Kind 0 is a plasma strip (a wide
// Gaussian glow and a narrow hot core), kind 1 a lit face, brighter toward its rim.
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) { p = sg_PosOS; n = sg_NrmOS; t = sg_TanOS.xyz; }
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  vec4 data = sg_Uv0;
  float intensity = data.w;
  vec3 color;
  if (data.z < 0.5) {
    float x = data.x;
    float heat = exp(-x * x / 0.03);
    float glow = exp(-x * x / 0.16);
    float a = intensity * max(heat, glow * 0.45) * 0.6;
    vec3 glowColor = mix(_BrightColor.rgb, _ArcCoreColor.rgb, 0.3);
    color = mix(glowColor, _ArcCoreColor.rgb, heat * heat) * (1.0 + 1.5 * heat) * (a * _BoltIntensity);
  } else {
    float e = intensity * (0.35 + 0.65 * clamp(data.y, 0.0, 1.0));
    color = mix(_BrightColor.rgb, _ArcCoreColor.rgb, 0.55) * (e * 0.5 * _GlowIntensity);
  }
  s.BaseColor = color;
  s.Alpha = 1.0;
  return s;
}
