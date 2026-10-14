// OmniDepthPrismShader (Assets/_Graphics/Materials/Shaders/OmniDepthPrismShader.shader) for WebGL2 / GLSL ES 3.00: the omni's
// triangle prisms (OmniDepthPrisms.mat) and the converter's Mass prisms (MassDepthPrisms.mat).
//
// TWO vertex stages, one fragment stage:
//   vertex       the shader's own vertex stage on the GPU (DepthPrismVert): reads POSITION, NORMAL and TEXCOORD2 (plate centre
//                xyz + plate index w) and uDepthState[60]. Same maths as CR.DepthPrisms.deform().
//   vertexBaked  takes positions already displaced by CR.DepthPrisms.deform() (spread included), its alpha and strike per vertex.
//   fragment     the look: OmniCrystalFresnelShader's colour (dark facing the camera, bright toward the rim), + _StrikeColor x strike,
//                and the _Opacity x alpha screen-door dither (interleaved gradient noise; clip(x < 0) discards).
//
// Render state (both Unity passes): OPAQUE, no blending, ZWrite ON, ZTest LEqual, Cull OFF, queue Geometry. Draw the mesh
// twice: pass "ArrivingPrism" (uLeaving 0) and, only while some column is mid-reseat (deformBoth().leavingVisible, or any
// state y < 1), pass "LeavingPrism" (uLeaving 1). The two passes dither on complementary pixels, so together they cover a
// fading column exactly once. Unlit: no lights, no shadows.
//
// Output: LINEAR HDR RGB in outColor.rgb (alpha 1), not tonemapped, not sRGB-encoded - the host tonemaps / encodes. Feed the
// colour uniforms the *Linear values from data/depth.json (the project is in Linear colour space, so Unity linearises the
// material's stored sRGB colours before upload).
//
// Uniforms (all of them; setters below):
//   uModel (mat4)            object -> world (the renderer's localToWorld). The spread push divides by its ROW lengths, as
//                            Unity's length(unity_ObjectToWorld._m00_m01_m02) does; normals go through its upper 3x3 (no
//                            inverse-transpose, as Unity's mul((float3x3)UNITY_MATRIX_M, normal)).
//   uViewProj (mat4)         world -> clip
//   uCameraPos (vec3)        camera position, world
//   uBright, uDark, uStrikeColor (vec4)   _BrightColorLinear, _DarkColorLinear, _StrikeColorLinear
//   uOpacity (float)         _Opacity (the converter's fade-in / appear; 1 = solid)
//   uLeaving (float)         0 arriving pass, 1 leaving pass
//   uDitherFlipHeight (float) 0: dither on gl_FragCoord as GL has it (bottom-left origin). Set to the framebuffer height to
//                            use D3D's top-left SV_Position rows, i.e. the exact pixel pattern Unity draws on Windows.
//   vertex only: uDepthState[60] (vec4), uDepthDriven, uReach, uPlateScaleStart, uThickness, uThicknessInward, uTopHeight,
//                uFaceSplit, uSplitUnit, uSpread (vec3, the material's _Spread in WORLD units)
// Attributes: vertex: aPosition (vec3), aNormal (vec3), aPlate (vec4 = TEXCOORD2). vertexBaked: aPosition (vec3, deformed),
//   aNormal (vec3), aAlpha (float), aStrike (float).
(function (CR) {
  'use strict';

  const common = `#version 300 es
precision highp float;
precision highp int;
uniform mat4 uModel;
uniform mat4 uViewProj;
out vec3 vWorldNormal;
out vec3 vWorldPos;
out float vAlpha;
out float vStrike;
`;

  // The shader's DepthPrismVert, line for line.
  const vertex = common + `
in vec3 aPosition;
in vec3 aNormal;
in vec4 aPlate;              // TEXCOORD2: plate centre xyz, plate index w
uniform vec4 uDepthState[60];
uniform float uDepthDriven, uLeaving;
uniform float uReach, uPlateScaleStart, uThickness, uThicknessInward, uTopHeight, uFaceSplit, uSplitUnit;
uniform vec3 uSpread;

vec3 DepthPrismPosition(vec3 vertex, vec3 normal, vec3 centre, float u) {
  vec3 dir = normalize(centre);
  float rr = length(centre);
  float k = mix(uReach, 1.0, u);
  vec3 local = vertex - centre;
  local += dir * (dot(local, dir) - uTopHeight * uThicknessInward) * (uThickness - 1.0);
  vec3 fn = normalize(normal);
  local += fn * uFaceSplit * uSplitUnit * (1.0 - u);
  float ls = k * mix(uPlateScaleStart, 1.0, u);
  return dir * rr * k + local * ls;
}

void main() {
  vec4 st = uDepthState[clamp(int(aPlate.w + 0.5), 0, 59)];
  vec3 p;
  float alpha;
  if (uDepthDriven <= 0.5) { p = aPosition; alpha = uLeaving > 0.5 ? 0.0 : 1.0; }
  else {
    float u = uLeaving > 0.5 ? st.z : st.x;
    alpha = uLeaving > 0.5 ? 1.0 - st.y : st.y;
    p = DepthPrismPosition(aPosition, aNormal, aPlate.xyz, u);
  }
  vAlpha = alpha;
  vStrike = uDepthDriven > 0.5 ? max(st.w, 0.0) : 0.0;
  // SpreadFresnelDisplace: out along the normal by _Spread WORLD units (Unity divides by the ROW lengths of the model matrix).
  vec3 objectScale = vec3(length(vec3(uModel[0][0], uModel[1][0], uModel[2][0])),
                          length(vec3(uModel[0][1], uModel[1][1], uModel[2][1])),
                          length(vec3(uModel[0][2], uModel[1][2], uModel[2][2])));
  p += normalize(aNormal) * (uSpread / objectScale);
  vec4 world = uModel * vec4(p, 1.0);
  vWorldPos = world.xyz;
  vWorldNormal = normalize(mat3(uModel) * aNormal);
  gl_Position = uViewProj * world;
  if (vAlpha <= 0.0) gl_Position = vec4(0.0, 0.0, 0.0, 1.0);   // nothing to show: collapse, no fragments
}
`;

  const vertexBaked = common + `
in vec3 aPosition;           // CR.DepthPrisms.deform().positions (object space, spread included)
in vec3 aNormal;
in float aAlpha;
in float aStrike;
void main() {
  vAlpha = aAlpha;
  vStrike = aStrike;
  vec4 world = uModel * vec4(aPosition, 1.0);
  vWorldPos = world.xyz;
  vWorldNormal = normalize(mat3(uModel) * aNormal);
  gl_Position = uViewProj * world;
  if (vAlpha <= 0.0) gl_Position = vec4(0.0, 0.0, 0.0, 1.0);
}
`;

  // The shader's DepthPrismFrag + SpreadFresnelColor.
  const fragment = `#version 300 es
precision highp float;
in vec3 vWorldNormal;
in vec3 vWorldPos;
in float vAlpha;
in float vStrike;
uniform vec3 uCameraPos;
uniform vec4 uBright, uDark, uStrikeColor;
uniform float uOpacity, uLeaving, uDitherFlipHeight;
out vec4 outColor;
void main() {
  vec2 px = gl_FragCoord.xy;
  if (uDitherFlipHeight > 0.0) px.y = uDitherFlipHeight - px.y;
  // Interleaved gradient noise, nudged strictly inside (0,1): clip(0) KEEPS a fragment; the leaving prism takes the complement.
  float n = fract(52.9829189 * fract(dot(px, vec2(0.06711056, 0.00583715))));
  float threshold = n * 0.998 + 0.001;
  if (uLeaving > 0.5) threshold = 1.0 - threshold;
  if (vAlpha * clamp(uOpacity, 0.0, 1.0) - threshold < 0.0) discard;
  vec3 viewDir = normalize(uCameraPos - vWorldPos);
  float fresnel = (1.0 + dot(viewDir, normalize(vWorldNormal))) / 2.0;
  vec4 color = mix(uBright, uDark, fresnel);
  color.rgb += uStrikeColor.rgb * vStrike;      // a Charge arc passing through the prism lights it
  outColor = vec4(color.rgb, 1.0);              // linear HDR
}
`;

  /** Uniform values for a material from data/depth.json (materials.mass / .omni) and a DepthShape, ready for setUniforms. */
  function uniforms(material, shape, extra) {
    const u = {
      uBright: material._BrightColorLinear, uDark: material._DarkColorLinear, uStrikeColor: material._StrikeColorLinear,
      uOpacity: material._Opacity, uSpread: material._Spread.slice(0, 3), uThicknessInward: material._ThicknessInward,
      uDepthDriven: 1, uLeaving: 0, uDitherFlipHeight: 0,
      uReach: shape ? shape.reach : material._Reach, uPlateScaleStart: shape ? shape.sizeStart : material._PlateScaleStart,
      uThickness: shape ? shape.thickness : material._Thickness, uTopHeight: shape ? shape.topHeight : material._TopHeight,
      uFaceSplit: shape ? shape.faceSplit : material._FaceSplit, uSplitUnit: shape ? shape.splitUnit : material._SplitUnit,
    };
    return Object.assign(u, extra || {});
  }

  /** Sets every known uniform present in `program` from `values` (numbers -> 1f, length 3/4 -> vec, 16 -> mat4,
   * uDepthState -> Float32Array(240) or DepthColumns.state). */
  function setUniforms(gl, program, values) {
    for (const [name, v] of Object.entries(values)) {
      if (name === 'uDepthState') {
        const loc = gl.getUniformLocation(program, 'uDepthState[0]') || gl.getUniformLocation(program, 'uDepthState');
        if (!loc) continue;
        let flat = v;
        if (!(v instanceof Float32Array)) { flat = new Float32Array(240); v.forEach((s, j) => flat.set(s, 4 * j)); }
        gl.uniform4fv(loc, flat);
        continue;
      }
      const loc = gl.getUniformLocation(program, name);
      if (!loc) continue;
      if (typeof v === 'number') gl.uniform1f(loc, v);
      else if (v.length === 3) gl.uniform3fv(loc, v);
      else if (v.length === 4) gl.uniform4fv(loc, v);
      else if (v.length === 16) gl.uniformMatrix4fv(loc, false, v);
    }
  }

  /** The render state both passes use. */
  function applyState(gl) {
    gl.disable(gl.BLEND);
    gl.enable(gl.DEPTH_TEST);
    gl.depthFunc(gl.LEQUAL);
    gl.depthMask(true);
    gl.disable(gl.CULL_FACE);
  }

  CR.DepthPrismsShader = {
    vertex, vertexBaked, fragment,
    uniforms, setUniforms, applyState,
    attributes: { vertex: ['aPosition', 'aNormal', 'aPlate'], vertexBaked: ['aPosition', 'aNormal', 'aAlpha', 'aStrike'] },
    passes: [{ name: 'ArrivingPrism', uLeaving: 0 }, { name: 'LeavingPrism', uLeaving: 1 }],
  };
})(globalThis.CR = globalThis.CR || {});
