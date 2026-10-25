// Hand translation of Assets/_Graphics/Materials/Graphs/Wormhole.shader ("CosmicShore/Wormhole"): a
// wormhole mouth's surface. The exact view through the pair (WormholeView's render of the gameplay
// camera carried through, sampled at this pixel's screen position) over the void colour, a fresnel rim
// in the owning domain's hue, the sealed shell a rival sees, the inside-camera clearance and the
// camera->ship occlusion corridor's screen door. The partner's six-face panorama is a texture array,
// which Amoebius does not have: its SystemInfo.copyTextureSupport is None, so the game never marks a
// panorama ready and the void colour stands where the shader would sample one.
// Per-renderer (WormholeMouth.ApplySurface's property block) and global inputs the Properties block
// does not declare:
uniform vec4 _WormholeSphere;          // xyz centre, w world radius
uniform vec4 _WormholeExactUV;         // screen uv -> exact target uv: uv * xy + zw
uniform float _WormholeExactBlend;     // 0 panorama .. 1 exact
uniform float _WormholePanoramaReady;
uniform float _WormholeFlare;          // 1 on a transit, decaying
uniform vec4 _WormholeRimTint;         // rgb the domain's hue (linear); a = 1 when set
uniform float _WormholeSealed;         // 1 = this viewer may not thread it
uniform float _WormholeCorridor;       // 1 = dissolve inside the occlusion corridor
uniform float _WormholeMainView;       // global: 1 while the gameplay camera draws
uniform float _WormholeClearance;      // global: a camera this close to the surface is in it
uniform vec4 _PrismOcclusionTarget;
uniform vec4 _PrismOcclusionParams;
uniform sampler2D _WormholeExactTex;
vec3 wormholeRimColour() {
  return _WormholeRimTint.a > 0.5 ? _WormholeRimTint.rgb * _DomainRimBoost : _RimColor.rgb;
}
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) {
  p = sg_PosOS; n = sg_NrmOS; t = sg_TanOS.xyz;
}
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  // A camera inside (or at) the mouth does not draw it.
  if (distance(sg_CamPos, _WormholeSphere.xyz) - (_WormholeSphere.w + max(_WormholeClearance, 0.0)) < 0.0) discard;
  // Never between the chase camera and the pilot's ship: the prisms' screen door.
  if (_WormholeCorridor >= 0.5) {
    float alpha, threshold;
    PrismOcclusionFade_float(sg_PosWS, _PrismOcclusionTarget.xyz, _PrismOcclusionParams.xyz, 1.0, alpha, threshold);
    if (alpha - threshold < 0.0) discard;
  }
  vec3 centre = _WormholeSphere.xyz;
  if (_WormholeSealed > 0.5) {
    vec3 n = normalize(sg_PosWS - centre);
    vec3 toEye = normalize(sg_CamPos - sg_PosWS);
    float shell = pow(1.0 - clamp(dot(n, toEye), 0.0, 1.0), max(_SealedRimPower, 0.5));
    if (shell - _SealedRimCutoff < 0.0) discard;
    s.BaseColor = wormholeRimColour() * (_SealedIntensity * shell + clamp(_WormholeFlare, 0.0, 1.0) * _FlareIntensity);
    s.Alpha = 1.0;
    return s;
  }
  vec3 viewDir = normalize(sg_PosWS - sg_CamPos);
  vec3 rel = sg_PosWS - centre;
  vec3 normal = rel / max(length(rel), 1e-5);
  float exact = clamp(_WormholeExactBlend, 0.0, 1.0) * step(0.5, _WormholeMainView);
  vec3 colour = _VoidColor.rgb;
  if (exact > 0.001) {
    vec2 uv = sg_ScreenPosRaw.xy / max(sg_ScreenPosRaw.w, 1e-5);
    uv = uv * _WormholeExactUV.xy + _WormholeExactUV.zw;
    colour = mix(colour, texture(_WormholeExactTex, uv).rgb, exact);
  }
  // The rim: a faint darkening and a glow in the domain's hue at the silhouette, punched up on a transit.
  float rim = pow(1.0 - clamp(dot(normal, -viewDir), 0.0, 1.0), max(_RimPower, 0.5));
  float glow = _RimIntensity + clamp(_WormholeFlare, 0.0, 1.0) * _FlareIntensity;
  s.BaseColor = colour * (1.0 - rim * clamp(_RimDarken, 0.0, 1.0)) + wormholeRimColour() * (rim * glow);
  s.Alpha = 1.0;
  return s;
}
