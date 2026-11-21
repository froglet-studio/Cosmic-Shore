// Hand translation of Assets/_Graphics/Materials/Graphs/ChargeCrystal.shader ("Shader Graphs/ChargeCrystal",
// a hand-written shader despite the name): the charge crystal's 60 static pentagonal prisms, flat
// facets lit by a fixed world key, a theme-pair rim, rim emission and the edge discharge
// (ChargeCrystal.hlsl, in the library). FadeIn's _opacity is spent as screen-door coverage.
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) {
  p = sg_PosOS; n = sg_NrmOS; t = sg_TanOS.xyz;
}
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  if (sgChargeCoverageClip(_opacity, gl_FragCoord.xy) < 0.0) discard;
  s.BaseColor = sgChargeCrystalSurface(sg_NrmWS, sg_CamPos - sg_PosWS, sg_Uv1.xyz, sg_Uv2.xyz, sg_Uv3.xyz, sg_Time.x,
    _DullCrystalColor, _BrightCrystalColor, _KeyDir.xyz, _FacetAmbient, _RimPower, _RimStrength, _EmissionStrength,
    _ArcCoreColor.rgb, _ArcIntensity, _ArcWidth, _ArcLength, _ArcSpeed, _ArcDuty, _ArcJitter, _ArcShimmer,
    _NodeRadius, _NodeIntensity);
  return s;
}
