// Hand translation of Assets/_Graphics/Materials/Shaders/OmniChargeEdgesShader.shader
// (Custom/OmniChargeEdgesShader): the charge crystal's edge discharge on the omni crystal's
// pentagonal plates, added over the body. The bolt data is baked into the mesh by CrystalEdgeArcs
// (TEXCOORD1 barycentrics, TEXCOORD2 edge heights, TEXCOORD3 edge seeds); an unbaked mesh adds
// nothing. Blend One One, ZWrite Off, Offset -1 -1 and Transparent+2 come from the .shader.
vec3 sg_objectScale() {
  return vec3(length(vec3(sg_ObjectToWorld[0][0], sg_ObjectToWorld[1][0], sg_ObjectToWorld[2][0])),
              length(vec3(sg_ObjectToWorld[0][1], sg_ObjectToWorld[1][1], sg_ObjectToWorld[2][1])),
              length(vec3(sg_ObjectToWorld[0][2], sg_ObjectToWorld[1][2], sg_ObjectToWorld[2][2])));
}
// @vertex
void sg_vertex(out vec3 p, out vec3 n, out vec3 t) {
  // The body's own push (SpreadFresnelDisplace): _Spread WORLD units along the normal.
  p = sg_PosOS + normalize(sg_NrmOS) * (_Spread.xyz / sg_objectScale());
  n = sg_NrmOS; t = sg_TanOS.xyz;
}
// @fragment
SgSurface sg_surface() {
  SgSurface s = sg_defaultSurface();
  s.BaseColor = sgChargeCrystalDischarge(sg_Uv1.xyz, sg_Uv2.xyz, sg_Uv3.xyz, sg_Time.x,
    _BrightColor.rgb, _ArcCoreColor.rgb, _ArcIntensity, _ArcWidth, _ArcLength, _ArcSpeed, _ArcDuty,
    _ArcJitter, _ArcShimmer, _NodeRadius, _NodeIntensity);
  s.Alpha = 0.0;
  return s;
}
