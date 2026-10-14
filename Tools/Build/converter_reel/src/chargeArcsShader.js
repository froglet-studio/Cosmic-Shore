// CrystalChargeArcsShader (Assets/_Graphics/Materials/Shaders/CrystalChargeArcsShader.shader), ported to WebGL2 /
// GLSL ES 3.00 for the converter reel. It draws what CR.ChargeArcs' ChargeArcMeshWriter writes: the camera-facing plasma
// strips (already camera-facing on the CPU - the shader only transforms) and the lit plate faces.
//
// ATTRIBUTES (one vertex each, from the writer after ChargeArcs.writeFaces):
//   aPosition (location 0) vec3 = writer.positions[3i..3i+2], in the sites' own space (the crystal's local space, Unity
//                                 axes - the same space the eye passed to seamWalk/flux was given in).
//   aData     (location 1) vec4 = writer.data[4i..4i+3] = Unity's TEXCOORD0:
//                                 x across the strip (-1..1; 0 on a face), y falloff from the light's centre (0..1; 0 on a
//                                 strip), z kind (0 plasma strip, 1 lit face), w intensity.
//   Draw writer.indices as TRIANGLES (indices can exceed 65535: use UNSIGNED_INT).
//
// UNIFORMS:
//   uObjectToClip  mat4  projection * view * model, model = the layer's transform (the crystal's frame; identity for
//                        a layer drawn in the stage's own space). Build it in whatever handedness the host renderer uses;
//                        the positions are Unity's (left-handed, Y up), exactly as written.
//   uBrightColor   vec3  _BrightColor, LINEAR (data/charge_arcs.json materials.OmniChargeArcs.colors._BrightColor.linear).
//   uArcCoreColor  vec3  _ArcCoreColor, LINEAR (... _ArcCoreColor.linear).
//   uBoltIntensity float _BoltIntensity (material float, 1).
//   uGlowIntensity float _GlowIntensity (material float, 1).
//   uFogAmount     float optional, 0 (the GL default) = no fog; = 1 - Unity's fog factor (MixFogColor toward black:
//                        colour *= 1 - uFogAmount). The converter stage has no fog; leave it unset.
//
// OUTPUT: fragColor.rgb = LINEAR HDR radiance to ADD to the frame (values > 1 are the bloom's), alpha 0. The material is
//   Blend One One (pure additive), ZWrite Off, ZTest LEqual, Cull Off, Offset -1,-1, Queue Transparent+2:
//     gl.enable(gl.BLEND); gl.blendFunc(gl.ONE, gl.ONE); gl.depthMask(false); gl.depthFunc(gl.LEQUAL);
//     gl.disable(gl.CULL_FACE); gl.enable(gl.POLYGON_OFFSET_FILL); gl.polygonOffset(-1, -1);
//   drawn after the opaque crystals (it reads their depth, writes none). Tonemapping and bloom are the host's.
(function (CR) {
  'use strict';

  const vertex = `#version 300 es
precision highp float;
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec4 aData;
uniform mat4 uObjectToClip;
out vec4 vData;
void main() {
  vData = aData;
  gl_Position = uObjectToClip * vec4(aPosition, 1.0);
}
`;

  const fragment = `#version 300 es
precision highp float;
in vec4 vData;
uniform vec3 uBrightColor;
uniform vec3 uArcCoreColor;
uniform float uBoltIntensity;
uniform float uGlowIntensity;
uniform float uFogAmount;
out vec4 fragColor;
void main() {
  float intensity = vData.w;
  vec3 color;
  if (vData.z < 0.5) {
    // plasma across the strip: a wide Gaussian glow and a narrow hot core (the crackle's arcLine)
    float x = vData.x;
    float heat = exp(-x * x / 0.03);
    float glow = exp(-x * x / 0.16);
    float a = intensity * max(heat, glow * 0.45) * 0.6;
    vec3 glowColor = mix(uBrightColor, uArcCoreColor, 0.3);
    color = mix(glowColor, uArcCoreColor, heat * heat) * (1.0 + 1.5 * heat) * (a * uBoltIntensity);
  } else {
    // a lit face: brighter toward the rim
    float e = intensity * (0.35 + 0.65 * clamp(vData.y, 0.0, 1.0));
    color = mix(uBrightColor, uArcCoreColor, 0.55) * (e * 0.5 * uGlowIntensity);
  }
  // Additive: fog fades the light toward nothing (Unity's MixFogColor(color, 0, fogFactor) = color * fogFactor).
  color *= 1.0 - uFogAmount;
  fragColor = vec4(color, 0.0);
}
`;

  /** Uniform defaults from data/charge_arcs.json (pass the parsed json; material defaults to its arcMaterial). */
  function uniformsFrom(json, materialName) {
    const m = json.materials[materialName || json.arcMaterial];
    return {
      uBrightColor: m.colors._BrightColor.linear.slice(0, 3),
      uArcCoreColor: m.colors._ArcCoreColor.linear.slice(0, 3),
      uBoltIntensity: m.floats._BoltIntensity,
      uGlowIntensity: m.floats._GlowIntensity,
      uFogAmount: 0,
    };
  }

  CR.ChargeArcsShader = {
    vertex,
    fragment,
    attributes: { aPosition: 0, aData: 1 },
    uniforms: {
      uObjectToClip: 'mat4: projection * view * model (the layer\'s frame)',
      uBrightColor: 'vec3 linear: material _BrightColor',
      uArcCoreColor: 'vec3 linear: material _ArcCoreColor',
      uBoltIntensity: 'float: material _BoltIntensity',
      uGlowIntensity: 'float: material _GlowIntensity',
      uFogAmount: 'float: 0 = no fog (default)',
    },
    uniformsFrom,
    renderState: { blend: ['ONE', 'ONE'], depthWrite: false, depthFunc: 'LEQUAL', cull: false, polygonOffset: [-1, -1], queue: 'Transparent+2' },
    /** The fragment maths on the CPU (same formulas), for tests or a software path: data = [x,y,z,w] -> [r,g,b]. */
    shade(data, u) {
      const lerp = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
      const intensity = data[3];
      let c;
      if (data[2] < 0.5) {
        const x = data[0], heat = Math.exp(-x * x / 0.03), glow = Math.exp(-x * x / 0.16);
        const a = intensity * Math.max(heat, glow * 0.45) * 0.6;
        const k = (1 + 1.5 * heat) * (a * u.uBoltIntensity);
        c = lerp(lerp(u.uBrightColor, u.uArcCoreColor, 0.3), u.uArcCoreColor, heat * heat).map((v) => v * k);
      } else {
        const e = intensity * (0.35 + 0.65 * Math.min(1, Math.max(0, data[1])));
        c = lerp(u.uBrightColor, u.uArcCoreColor, 0.55).map((v) => v * e * 0.5 * u.uGlowIntensity);
      }
      const fog = 1 - (u.uFogAmount || 0);
      return c.map((v) => v * fog);
    },
  };
})(globalThis.CR = globalThis.CR || {});
