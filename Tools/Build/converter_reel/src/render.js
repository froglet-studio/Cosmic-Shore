// The reel's renderer: WebGL2, HDR (RGBA16F) with MSAA, optional supersampling, bloom, tonemap, then an sRGB frame
// on the output canvas at the exact export size.
//
// The crystal bodies are OmniCrystalFresnelShader (the omni's body, which the converter's Charge, Space and Time
// crystals also wear): colour = lerp(_BrightColor, _DarkColor, (1 + N.V) / 2), unlit, Cull Off, pushed out along
// the normal by _Spread world units, and _Opacity spent as an interleaved-gradient screen-door clip. Meshes are
// built the way OmniCombinePlayer.Piece builds them: each triangle wound away from its plate's centre, coplanar
// triangles of one plate (within 20 degrees) sharing a flat face normal recomputed from the pose every frame.
//
// Unity's frame is left-handed; flipping z on every position, normal and the eye gives Unity's own picture in a
// right-handed GL view (Unity's worldToCamera does the same flip).
(function (CR) {
  'use strict';
  const COPLANAR = Math.cos(20 * Math.PI / 180);   // CrystalEdgeArcMeshBaker.CoplanarAngleDegrees

  // ---------------------------------------------------------------- mesh topology (OmniCombinePlayer.Piece)
  function piece(points, tris, plate, element) {
    const P = (i) => [points[3 * i], points[3 * i + 1], points[3 * i + 2]];
    let np = 0; for (const q of plate) np = Math.max(np, q + 1);
    const sum = Array.from({ length: np }, () => [0, 0, 0]), count = new Int32Array(np);
    for (let i = 0; i < plate.length; i++) { const v = P(i); sum[plate[i]][0] += v[0]; sum[plate[i]][1] += v[1]; sum[plate[i]][2] += v[2]; count[plate[i]]++; }
    const centre = sum.map((s, q) => [s[0] / Math.max(1, count[q]), s[1] / Math.max(1, count[q]), s[2] / Math.max(1, count[q])]);
    const nt = tris.length / 3, corner = new Int32Array(tris.length), normal = [];
    const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
    const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    const unit = (a) => { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; };
    for (let t = 0; t < nt; t++) {
      const a = tris[3 * t], b = tris[3 * t + 1], c = tris[3 * t + 2], pa = P(a), pb = P(b), pc = P(c);
      const n = cross(sub(pb, pa), sub(pc, pa)), m = [(pa[0] + pb[0] + pc[0]) / 3, (pa[1] + pb[1] + pc[1]) / 3, (pa[2] + pb[2] + pc[2]) / 3];
      const flip = (n[0] * (m[0] - centre[plate[a]][0]) + n[1] * (m[1] - centre[plate[a]][1]) + n[2] * (m[2] - centre[plate[a]][2])) < 0;
      corner[3 * t] = a; corner[3 * t + 1] = flip ? c : b; corner[3 * t + 2] = flip ? b : c;
      normal.push(unit(cross(sub(P(corner[3 * t + 1]), P(corner[3 * t])), sub(P(corner[3 * t + 2]), P(corner[3 * t])))));
    }
    const parent = Int32Array.from({ length: nt }, (_, i) => i);
    const find = (x) => { while (parent[x] !== x) x = parent[x] = parent[parent[x]]; return x; };
    const owner = new Map();
    for (let t = 0; t < nt; t++) for (let e = 0; e < 3; e++) {
      const i = corner[3 * t + e], j = corner[3 * t + (e + 1) % 3], key = i < j ? i * 1e6 + j : j * 1e6 + i;
      if (owner.has(key)) {
        const o = owner.get(key), no = normal[o], nn = normal[t];
        if (plate[corner[3 * o]] === plate[i] && no[0] * nn[0] + no[1] * nn[1] + no[2] * nn[2] >= COPLANAR) parent[find(t)] = find(o);
      } else owner.set(key, t);
    }
    const faceOfTri = new Int32Array(nt), ids = new Map();
    for (let t = 0; t < nt; t++) { const r = find(t); if (!ids.has(r)) ids.set(r, ids.size); faceOfTri[t] = ids.get(r); }
    const cornerPlate = new Int32Array(corner.length);
    for (let c = 0; c < corner.length; c++) cornerPlate[c] = plate[corner[c]];
    const elementOf = new Float32Array(corner.length);
    for (let c = 0; c < corner.length; c++) elementOf[c] = typeof element === 'function' ? element(cornerPlate[c]) : element;
    return { corner, faceOfTri, faces: ids.size, cornerPlate, elementOf, pos: new Float32Array(corner.length * 3), nrm: new Float32Array(corner.length * 3), faceN: new Float32Array(ids.size * 3) };
  }

  // Writes the corners' positions from a pose (xyz per source vertex) and the flat per-face normals (Piece.Write).
  function writePiece(pc, pose, origin) {
    const { corner, faceOfTri, pos, nrm, faceN } = pc, ox = origin ? origin[0] : 0, oy = origin ? origin[1] : 0, oz = origin ? origin[2] : 0;
    for (let c = 0; c < corner.length; c++) { const i = corner[c]; pos[3 * c] = pose[3 * i] - ox; pos[3 * c + 1] = pose[3 * i + 1] - oy; pos[3 * c + 2] = pose[3 * i + 2] - oz; }
    faceN.fill(0);
    for (let t = 0; t < faceOfTri.length; t++) {
      const a = 9 * t, f = 3 * faceOfTri[t];
      const ux = pos[a + 3] - pos[a], uy = pos[a + 4] - pos[a + 1], uz = pos[a + 5] - pos[a + 2];
      const vx = pos[a + 6] - pos[a], vy = pos[a + 7] - pos[a + 1], vz = pos[a + 8] - pos[a + 2];
      faceN[f] += uy * vz - uz * vy; faceN[f + 1] += uz * vx - ux * vz; faceN[f + 2] += ux * vy - uy * vx;
    }
    for (let t = 0; t < faceOfTri.length; t++) {
      const f = 3 * faceOfTri[t], l = Math.hypot(faceN[f], faceN[f + 1], faceN[f + 2]) || 1;
      for (let k = 0; k < 3; k++) { const o = 9 * t + 3 * k; nrm[o] = faceN[f] / l; nrm[o + 1] = faceN[f + 1] / l; nrm[o + 2] = faceN[f + 2] / l; }
    }
  }

  // ---------------------------------------------------------------- matrices (right-handed GL, z already flipped)
  function lookAt(eye, target, up) {
    const z = norm([eye[0] - target[0], eye[1] - target[1], eye[2] - target[2]]), x = norm(cr(up, z)), y = cr(z, x);
    return new Float32Array([x[0], y[0], z[0], 0, x[1], y[1], z[1], 0, x[2], y[2], z[2], 0, -dt(x, eye), -dt(y, eye), -dt(z, eye), 1]);
  }
  function perspective(fovY, aspect, near, far) {
    const f = 1 / Math.tan(fovY / 2), nf = 1 / (near - far);
    return new Float32Array([f / aspect, 0, 0, 0, 0, f, 0, 0, 0, 0, (far + near) * nf, -1, 0, 0, 2 * far * near * nf, 0]);
  }
  function mul4(a, b) {
    const o = new Float32Array(16);
    for (let c = 0; c < 4; c++) for (let r = 0; r < 4; r++) { let s = 0; for (let k = 0; k < 4; k++) s += a[k * 4 + r] * b[c * 4 + k]; o[c * 4 + r] = s; }
    return o;
  }
  function invert4(m) {
    const a = m, inv = new Float32Array(16);
    inv[0] = a[5] * a[10] * a[15] - a[5] * a[11] * a[14] - a[9] * a[6] * a[15] + a[9] * a[7] * a[14] + a[13] * a[6] * a[11] - a[13] * a[7] * a[10];
    inv[4] = -a[4] * a[10] * a[15] + a[4] * a[11] * a[14] + a[8] * a[6] * a[15] - a[8] * a[7] * a[14] - a[12] * a[6] * a[11] + a[12] * a[7] * a[10];
    inv[8] = a[4] * a[9] * a[15] - a[4] * a[11] * a[13] - a[8] * a[5] * a[15] + a[8] * a[7] * a[13] + a[12] * a[5] * a[11] - a[12] * a[7] * a[9];
    inv[12] = -a[4] * a[9] * a[14] + a[4] * a[10] * a[13] + a[8] * a[5] * a[14] - a[8] * a[6] * a[13] - a[12] * a[5] * a[10] + a[12] * a[6] * a[9];
    inv[1] = -a[1] * a[10] * a[15] + a[1] * a[11] * a[14] + a[9] * a[2] * a[15] - a[9] * a[3] * a[14] - a[13] * a[2] * a[11] + a[13] * a[3] * a[10];
    inv[5] = a[0] * a[10] * a[15] - a[0] * a[11] * a[14] - a[8] * a[2] * a[15] + a[8] * a[3] * a[14] + a[12] * a[2] * a[11] - a[12] * a[3] * a[10];
    inv[9] = -a[0] * a[9] * a[15] + a[0] * a[11] * a[13] + a[8] * a[1] * a[15] - a[8] * a[3] * a[13] - a[12] * a[1] * a[11] + a[12] * a[3] * a[9];
    inv[13] = a[0] * a[9] * a[14] - a[0] * a[10] * a[13] - a[8] * a[1] * a[14] + a[8] * a[2] * a[13] + a[12] * a[1] * a[10] - a[12] * a[2] * a[9];
    inv[2] = a[1] * a[6] * a[15] - a[1] * a[7] * a[14] - a[5] * a[2] * a[15] + a[5] * a[3] * a[14] + a[13] * a[2] * a[7] - a[13] * a[3] * a[6];
    inv[6] = -a[0] * a[6] * a[15] + a[0] * a[7] * a[14] + a[4] * a[2] * a[15] - a[4] * a[3] * a[14] - a[12] * a[2] * a[7] + a[12] * a[3] * a[6];
    inv[10] = a[0] * a[5] * a[15] - a[0] * a[7] * a[13] - a[4] * a[1] * a[15] + a[4] * a[3] * a[13] + a[12] * a[1] * a[7] - a[12] * a[3] * a[5];
    inv[14] = -a[0] * a[5] * a[14] + a[0] * a[6] * a[13] + a[4] * a[1] * a[14] - a[4] * a[2] * a[13] - a[12] * a[1] * a[6] + a[12] * a[2] * a[5];
    inv[3] = -a[1] * a[6] * a[11] + a[1] * a[7] * a[10] + a[5] * a[2] * a[11] - a[5] * a[3] * a[10] - a[9] * a[2] * a[7] + a[9] * a[3] * a[6];
    inv[7] = a[0] * a[6] * a[11] - a[0] * a[7] * a[10] - a[4] * a[2] * a[11] + a[4] * a[3] * a[10] + a[8] * a[2] * a[7] - a[8] * a[3] * a[6];
    inv[11] = -a[0] * a[5] * a[11] + a[0] * a[7] * a[9] + a[4] * a[1] * a[11] - a[4] * a[3] * a[9] - a[8] * a[1] * a[7] + a[8] * a[3] * a[5];
    inv[15] = a[0] * a[5] * a[10] - a[0] * a[6] * a[9] - a[4] * a[1] * a[10] + a[4] * a[2] * a[9] + a[8] * a[1] * a[6] - a[8] * a[2] * a[5];
    const det = a[0] * inv[0] + a[1] * inv[4] + a[2] * inv[8] + a[3] * inv[12];
    for (let i = 0; i < 16; i++) inv[i] /= det;
    return inv;
  }
  const dt = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  const cr = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const norm = (a) => { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; };

  // ---------------------------------------------------------------- shaders
  const FULL_VS = `#version 300 es
  out vec2 vUv;
  void main() { vec2 p = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2)); vUv = p; gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0); }`;

  // Background: a vertical two-colour gradient, a soft glow behind the stage, and a starfield fixed to the sky
  // (directions, so it turns with the orbit and the loop closes on it).
  const BG_FS = `#version 300 es
  precision highp float;
  in vec2 vUv; out vec4 o;
  uniform mat4 uInvVP; uniform vec3 uTop, uBottom, uGlow; uniform float uGlowSize, uGlowStrength, uStars, uStarBright, uAspect;
  uniform vec2 uGlowAt;
  float h31(vec3 p) { p = fract(p * vec3(443.897, 441.423, 437.195)); p += dot(p, p.yzx + 19.19); return fract((p.x + p.y) * p.z); }
  vec3 star(vec3 d, float scale) {
    vec3 g = d * scale, c = floor(g), f = fract(g) - 0.5;
    float r = h31(c);
    if (r > uStars) return vec3(0.0);
    vec3 off = vec3(h31(c + 1.7), h31(c + 3.1), h31(c + 5.3)) - 0.5;
    float dd = length(f - off * 0.7);
    float s = smoothstep(0.08, 0.0, dd) * (0.3 + 0.7 * h31(c + 9.9));
    vec3 tint = mix(vec3(0.75, 0.85, 1.0), vec3(1.0, 0.9, 0.75), h31(c + 7.7));
    return tint * s;
  }
  void main() {
    vec4 a = uInvVP * vec4(vUv * 2.0 - 1.0, 1.0, 1.0);
    vec3 d = normalize(a.xyz / a.w);
    vec3 col = mix(uBottom, uTop, smoothstep(0.0, 1.0, vUv.y));
    vec2 q = (vUv - uGlowAt) * vec2(uAspect, 1.0);
    col += uGlow * uGlowStrength * exp(-dot(q, q) / max(1e-4, uGlowSize * uGlowSize));
    if (uStars > 0.0) col += uStarBright * (star(d, 90.0) + 0.6 * star(d.yzx, 160.0));
    o = vec4(col, 1.0);
  }`;

  const BODY_VS = `#version 300 es
  in vec3 aPos; in vec3 aNrm; in float aEl;
  uniform mat4 uVP; uniform float uSpread;
  out vec3 vPos; out vec3 vN; flat out int vEl;
  void main() {
    vec3 n = normalize(aNrm), p = aPos + n * uSpread;   // SpreadFresnelDisplace
    p.z = -p.z; n.z = -n.z;
    vPos = p; vN = n; vEl = int(aEl + 0.5);
    gl_Position = uVP * vec4(p, 1.0);
  }`;
  const BODY_FS = `#version 300 es
  precision highp float;
  in vec3 vPos; in vec3 vN; flat in int vEl; out vec4 o;
  uniform vec3 uEye; uniform vec3 uBright[4]; uniform vec3 uDark[4]; uniform float uOpacity;
  void main() {
    float n = fract(52.9829189 * fract(dot(gl_FragCoord.xy, vec2(0.06711056, 0.00583715))));
    if (clamp(uOpacity, 0.0, 1.0) - (n * 0.998 + 0.001) < 0.0) discard;
    vec3 v = normalize(uEye - vPos);
    float f = (1.0 + dot(v, normalize(vN))) / 2.0;
    o = vec4(mix(uBright[vEl], uDark[vEl], f), 1.0);
  }`;

  // Bloom: a soft-knee prefilter, then a dual-filter down/up chain.
  const PRE_FS = `#version 300 es
  precision highp float; in vec2 vUv; out vec4 o; uniform sampler2D uSrc; uniform vec2 uTexel; uniform float uThreshold, uKnee;
  void main() {
    vec3 c = vec3(0.0);
    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) c += texture(uSrc, vUv + vec2(x, y) * uTexel).rgb;
    c /= 9.0;
    float br = max(c.r, max(c.g, c.b)), rq = clamp(br - uThreshold + uKnee, 0.0, 2.0 * uKnee);
    rq = rq * rq / (4.0 * uKnee + 1e-4);
    o = vec4(c * max(rq, br - uThreshold) / max(br, 1e-4), 1.0);
  }`;
  const DOWN_FS = `#version 300 es
  precision highp float; in vec2 vUv; out vec4 o; uniform sampler2D uSrc; uniform vec2 uTexel;
  void main() {
    vec3 c = texture(uSrc, vUv).rgb * 4.0;
    c += texture(uSrc, vUv + uTexel * vec2(-1, -1)).rgb + texture(uSrc, vUv + uTexel * vec2(1, -1)).rgb;
    c += texture(uSrc, vUv + uTexel * vec2(-1, 1)).rgb + texture(uSrc, vUv + uTexel * vec2(1, 1)).rgb;
    o = vec4(c / 8.0, 1.0);
  }`;
  const UP_FS = `#version 300 es
  precision highp float; in vec2 vUv; out vec4 o; uniform sampler2D uSrc; uniform vec2 uTexel; uniform float uRadius;
  void main() {
    vec2 t = uTexel * uRadius; vec3 c = vec3(0.0);
    c += texture(uSrc, vUv + t * vec2(-2, 0)).rgb + texture(uSrc, vUv + t * vec2(2, 0)).rgb;
    c += texture(uSrc, vUv + t * vec2(0, -2)).rgb + texture(uSrc, vUv + t * vec2(0, 2)).rgb;
    c += 2.0 * (texture(uSrc, vUv + t * vec2(-1, -1)).rgb + texture(uSrc, vUv + t * vec2(1, -1)).rgb);
    c += 2.0 * (texture(uSrc, vUv + t * vec2(-1, 1)).rgb + texture(uSrc, vUv + t * vec2(1, 1)).rgb);
    o = vec4(c / 12.0, 1.0);
  }`;
  // Composite: exposure, bloom, tonemap, grade, vignette, grain, sRGB; averages the supersamples AFTER tonemapping so
  // bright edges stay anti-aliased; then the caption layer.
  const COMP_FS = `#version 300 es
  precision highp float; in vec2 vUv; out vec4 o;
  uniform sampler2D uScene, uBloom, uOverlay; uniform vec2 uSceneTexel; uniform int uSS;
  uniform float uExposure, uBloomStrength, uSat, uContrast, uVignette, uGrain, uFrame, uOverlayOn; uniform int uTone;
  vec3 aces(vec3 x) { return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0); }
  vec3 neutral(vec3 c) {   // Khronos PBR Neutral
    float x = min(c.r, min(c.g, c.b)), off = x < 0.08 ? x - 6.25 * x * x : 0.04; c -= off;
    float peak = max(c.r, max(c.g, c.b)); if (peak < 0.76) return c;
    float d = 0.24, nw = 1.0 - d * d / (peak + d - 0.76); c *= nw / peak;
    float g = 1.0 - 1.0 / (0.15 * (peak - nw) + 1.0); return mix(c, vec3(nw), g);
  }
  vec3 tone(vec3 c) {
    if (uTone == 1) return aces(c);
    if (uTone == 2) return neutral(c);
    if (uTone == 3) return c / (1.0 + c);
    return clamp(c, 0.0, 1.0);
  }
  vec3 grade(vec3 c) {
    float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
    c = mix(vec3(l), c, uSat);
    c = (c - 0.18) * uContrast + 0.18;
    return max(c, 0.0);
  }
  vec3 srgb(vec3 c) { return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c)); }
  float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233)) + uFrame * 0.6180339) * 43758.5453); }
  float h2(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
  void main() {
    vec3 bloom = texture(uBloom, vUv).rgb * uBloomStrength;
    vec3 acc = vec3(0.0);
    ivec2 base = ivec2(gl_FragCoord.xy) * uSS;
    for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) {
      if (x >= uSS || y >= uSS) continue;
      vec3 s = texelFetch(uScene, base + ivec2(x, y), 0).rgb;
      acc += tone(grade((s + bloom) * uExposure));
    }
    vec3 c = acc / float(uSS * uSS);
    vec2 q = vUv - 0.5; c *= 1.0 - uVignette * smoothstep(0.2, 0.85, length(q * vec2(1.0, 1.0)));
    c = srgb(clamp(c, 0.0, 1.0));
    // a fixed half-step triangular dither: dark gradients would otherwise band in 8 bits
    c += (h2(gl_FragCoord.xy) + h2(gl_FragCoord.xy + 17.31) - 1.0) / 255.0;
    c += (hash(gl_FragCoord.xy) - 0.5) * uGrain;
    if (uOverlayOn > 0.5) { vec4 ov = texture(uOverlay, vec2(vUv.x, 1.0 - vUv.y)); c = mix(c, ov.rgb, ov.a); }
    o = vec4(c, 1.0);
  }`;

  function srgbToLinear(c) { return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4); }
  function hexToLinear(hex, gain) {
    const n = parseInt(hex.slice(1), 16), g = gain == null ? 1 : gain;
    return [srgbToLinear(((n >> 16) & 255) / 255) * g, srgbToLinear(((n >> 8) & 255) / 255) * g, srgbToLinear((n & 255) / 255) * g];
  }

  class Renderer {
    constructor(canvas, reel) {
      this.canvas = canvas;
      this.reel = reel;
      const gl = canvas.getContext('webgl2', { antialias: false, alpha: false, depth: false, preserveDrawingBuffer: true, premultipliedAlpha: false });
      if (!gl) throw new Error('WebGL2 is not available in this browser');
      this.gl = gl;
      this.floatOk = !!gl.getExtension('EXT_color_buffer_float');
      gl.getExtension('OES_texture_float_linear');
      this.maxSamples = gl.getParameter(gl.MAX_SAMPLES) || 1;
      this.progs = {
        bg: this.program(FULL_VS, BG_FS), body: this.program(BODY_VS, BODY_FS),
        pre: this.program(FULL_VS, PRE_FS), down: this.program(FULL_VS, DOWN_FS), up: this.program(FULL_VS, UP_FS), comp: this.program(FULL_VS, COMP_FS),
      };
      this.vao = gl.createVertexArray();
      this.layers = [];   // extra draw layers (the depth prisms, the Charge light): { draw(gl, frame, view) }
      this.buildPieces();
      this.overlayTex = gl.createTexture();
      this.overlayOn = false;
      this.frameNo = 0;
    }

    program(vs, fs) {
      const gl = this.gl, mk = (type, src) => {
        const s = gl.createShader(type); gl.shaderSource(s, src); gl.compileShader(s);
        if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(s) + '\n' + src.split('\n').slice(0, 4).join('\n'));
        return s;
      };
      const p = gl.createProgram();
      gl.attachShader(p, mk(gl.VERTEX_SHADER, vs)); gl.attachShader(p, mk(gl.FRAGMENT_SHADER, fs)); gl.linkProgram(p);
      if (!gl.getProgramParameter(p, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(p));
      const u = {}, n = gl.getProgramParameter(p, gl.ACTIVE_UNIFORMS);
      for (let i = 0; i < n; i++) { const info = gl.getActiveUniform(p, i), name = info.name.replace(/\[0\]$/, ''); u[name] = gl.getUniformLocation(p, info.name); }
      return { p, u, a: (name) => gl.getAttribLocation(p, name) };
    }

    // The four crystals that wear the body material (Space, Charge, Time; Mass is the depth layer's) and the omni
    // body with its 20 triangle holes cut.
    buildPieces() {
      const gl = this.gl, data = this.reel.assets.data, { MASS } = CR.Combine;
      this.pieces = [];
      for (let k = 0; k < 4; k++) {
        if (k === MASS) { this.pieces.push(null); continue; }
        const d = this.reel.core.crystals[k].data;
        this.pieces.push(this.gpuPiece(piece(d.basis, d.tris, d.plate, k)));
      }
      const tris = [];
      for (let t = 0; t < data.omni.tris.length; t += 3)
        if (data.omni.plateOwner[data.omni.plate[data.omni.tris[t]]] !== MASS) tris.push(data.omni.tris[t], data.omni.tris[t + 1], data.omni.tris[t + 2]);
      this.omniPiece = this.gpuPiece(piece(data.omni.verts, tris, data.omni.plate, (q) => data.omni.plateOwner[q]));
    }

    gpuPiece(pc) {
      const gl = this.gl, b = this.progs.body;
      pc.vao = gl.createVertexArray();
      gl.bindVertexArray(pc.vao);
      const buf = (data, loc, size, usage) => {
        const id = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, id); gl.bufferData(gl.ARRAY_BUFFER, data, usage);
        if (loc >= 0) { gl.enableVertexAttribArray(loc); gl.vertexAttribPointer(loc, size, gl.FLOAT, false, 0, 0); }
        return id;
      };
      pc.posBuf = buf(pc.pos, b.a('aPos'), 3, gl.DYNAMIC_DRAW);
      pc.nrmBuf = buf(pc.nrm, b.a('aNrm'), 3, gl.DYNAMIC_DRAW);
      pc.elBuf = buf(pc.elementOf, b.a('aEl'), 1, gl.STATIC_DRAW);
      gl.bindVertexArray(null);
      return pc;
    }

    uploadPiece(pc) {
      const gl = this.gl;
      gl.bindBuffer(gl.ARRAY_BUFFER, pc.posBuf); gl.bufferSubData(gl.ARRAY_BUFFER, 0, pc.pos);
      gl.bindBuffer(gl.ARRAY_BUFFER, pc.nrmBuf); gl.bufferSubData(gl.ARRAY_BUFFER, 0, pc.nrm);
    }

    // ---------------------------------------------------------------- targets
    resize(w, h, ss) {
      const gl = this.gl;
      ss = Math.max(1, Math.min(3, ss | 0));
      const maxTex = gl.getParameter(gl.MAX_TEXTURE_SIZE), maxRb = gl.getParameter(gl.MAX_RENDERBUFFER_SIZE);
      while (ss > 1 && (w * ss > Math.min(maxTex, maxRb) || h * ss > Math.min(maxTex, maxRb))) ss--;
      if (this.size && this.size.w === w && this.size.h === h && this.size.ss === ss) return;
      this.canvas.width = w; this.canvas.height = h;
      this.size = { w, h, ss, W: w * ss, H: h * ss };
      this.freeTargets();
      const fmt = this.floatOk ? gl.RGBA16F : gl.RGBA8, W = w * ss, H = h * ss;
      const samples = Math.min(4, this.maxSamples);
      const t = this.targets = {};
      t.msFbo = gl.createFramebuffer();
      t.msColor = gl.createRenderbuffer(); gl.bindRenderbuffer(gl.RENDERBUFFER, t.msColor);
      gl.renderbufferStorageMultisample(gl.RENDERBUFFER, samples, fmt, W, H);
      t.msDepth = gl.createRenderbuffer(); gl.bindRenderbuffer(gl.RENDERBUFFER, t.msDepth);
      gl.renderbufferStorageMultisample(gl.RENDERBUFFER, samples, gl.DEPTH_COMPONENT24, W, H);
      gl.bindFramebuffer(gl.FRAMEBUFFER, t.msFbo);
      gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.RENDERBUFFER, t.msColor);
      gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.RENDERBUFFER, t.msDepth);
      t.scene = this.texTarget(W, H, fmt, gl.NEAREST);
      // bloom chain at the output size, from half resolution down
      t.bloom = [];
      let bw = Math.max(1, w >> 1), bh = Math.max(1, h >> 1);
      for (let i = 0; i < 7 && bw > 2 && bh > 2; i++) { t.bloom.push(this.texTarget(bw, bh, fmt, gl.LINEAR)); bw = Math.max(1, bw >> 1); bh = Math.max(1, bh >> 1); }
      t.up = t.bloom.map((b) => this.texTarget(b.w, b.h, fmt, gl.LINEAR));
      gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    }

    texTarget(w, h, fmt, filter) {
      const gl = this.gl, tex = gl.createTexture();
      gl.bindTexture(gl.TEXTURE_2D, tex);
      gl.texStorage2D(gl.TEXTURE_2D, 1, fmt, w, h);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, filter); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, filter);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
      const fbo = gl.createFramebuffer();
      gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
      gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0);
      return { tex, fbo, w, h };
    }

    freeTargets() {
      const gl = this.gl, t = this.targets;
      if (!t) return;
      gl.deleteFramebuffer(t.msFbo); gl.deleteRenderbuffer(t.msColor); gl.deleteRenderbuffer(t.msDepth);
      for (const x of [t.scene, ...t.bloom, ...t.up]) { gl.deleteTexture(x.tex); gl.deleteFramebuffer(x.fbo); }
      this.targets = null;
    }

    setOverlay(canvas2d) {
      const gl = this.gl;
      this.overlayOn = !!canvas2d;
      if (!canvas2d) return;
      gl.bindTexture(gl.TEXTURE_2D, this.overlayTex);
      gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, canvas2d);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    }

    // ---------------------------------------------------------------- one frame
    // frame: Reel.frame(tau); look: the colour lab's values (see LOOK in the page).
    draw(frame, look) {
      const gl = this.gl, S = this.size, t = this.targets, cam = frame.camera, R = this.reel.R;
      // camera: ElementalConverterView's near/far, fitted to the frame's aspect
      const aspect = S.w / S.h, fovY = cam.fov * Math.PI / 180;
      const distance = cam.distance;
      const dir = norm(cam.eye), eyeU = [dir[0] * distance, dir[1] * distance, dir[2] * distance];
      const eye = [eyeU[0], eyeU[1], -eyeU[2]];
      const framingR = R * (this.reel.assets.config.maxStartRadii || 4);
      const near = Math.max(0.01 * R, 0.5 * (distance - framingR)), far = distance + 2 * framingR;
      const view = lookAt(eye, [0, 0, 0], [0, 1, 0]), proj = perspective(fovY, aspect, near, far), vp = mul4(proj, view);
      const V = { vp, view, proj, eye, eyeUnity: eyeU, near, far, distance, W: S.W, H: S.H, R, look, unitScale: R / 40 };
      this.frameNo++;

      // 1. scene into the multisampled HDR target
      gl.bindFramebuffer(gl.FRAMEBUFFER, t.msFbo);
      gl.viewport(0, 0, S.W, S.H);
      gl.disable(gl.BLEND); gl.disable(gl.DEPTH_TEST); gl.depthMask(false);
      const bg = this.progs.bg;
      gl.useProgram(bg.p);
      gl.uniformMatrix4fv(bg.u.uInvVP, false, invert4(vp));
      gl.uniform3fv(bg.u.uTop, hexToLinear(look.bgTop)); gl.uniform3fv(bg.u.uBottom, hexToLinear(look.bgBottom));
      gl.uniform3fv(bg.u.uGlow, hexToLinear(look.glowColor)); gl.uniform1f(bg.u.uGlowSize, look.glowSize); gl.uniform1f(bg.u.uGlowStrength, look.glowStrength);
      gl.uniform2f(bg.u.uGlowAt, 0.5, 0.5);
      gl.uniform1f(bg.u.uStars, look.stars); gl.uniform1f(bg.u.uStarBright, look.starBright); gl.uniform1f(bg.u.uAspect, aspect);
      gl.bindVertexArray(this.vao); gl.drawArrays(gl.TRIANGLES, 0, 3);

      gl.enable(gl.DEPTH_TEST); gl.depthFunc(gl.LEQUAL); gl.depthMask(true);
      gl.clear(gl.DEPTH_BUFFER_BIT);
      gl.disable(gl.CULL_FACE);   // Cull Off
      const body = this.progs.body;
      gl.useProgram(body.p);
      gl.uniformMatrix4fv(body.u.uVP, false, vp);
      gl.uniform3fv(body.u.uEye, eye);
      gl.uniform1f(body.u.uSpread, look.spread * V.unitScale);   // _Spread world units -> stage units
      const bright = [], dark = [];
      for (let k = 0; k < 4; k++) {
        const tint = look.tint;
        const b = hexToLinear(look.bodyBright, look.bodyGain), e = hexToLinear(look.elementColors[k], look.bodyGain);
        bright.push(b[0] + (e[0] - b[0]) * tint, b[1] + (e[1] - b[1]) * tint, b[2] + (e[2] - b[2]) * tint);
        dark.push(...hexToLinear(look.bodyDark));
      }
      gl.uniform3fv(body.u.uBright, bright); gl.uniform3fv(body.u.uDark, dark);
      if (frame.crystals) {
        gl.uniform1f(body.u.uOpacity, 1);
        for (let k = 0; k < 4; k++) {
          const pc = this.pieces[k];
          if (!pc) continue;
          writePiece(pc, this.reel.poses[k]);
          this.uploadPiece(pc);
          gl.bindVertexArray(pc.vao); gl.drawArrays(gl.TRIANGLES, 0, pc.corner.length);
        }
      }
      if (frame.omni) {
        gl.uniform1f(body.u.uOpacity, frame.omni.appear);
        writePiece(this.omniPiece, frame.omni.pos);
        this.uploadPiece(this.omniPiece);
        gl.bindVertexArray(this.omniPiece.vao); gl.drawArrays(gl.TRIANGLES, 0, this.omniPiece.corner.length);
      }
      for (const layer of this.layers) layer.draw(gl, frame, V);
      gl.bindVertexArray(null);

      // 2. resolve
      gl.bindFramebuffer(gl.READ_FRAMEBUFFER, t.msFbo);
      gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, t.scene.fbo);
      gl.blitFramebuffer(0, 0, S.W, S.H, 0, 0, S.W, S.H, gl.COLOR_BUFFER_BIT, gl.NEAREST);
      gl.disable(gl.DEPTH_TEST); gl.depthMask(false); gl.disable(gl.BLEND);
      gl.bindVertexArray(this.vao);

      // 3. bloom
      const pass = (prog, src, dst, setup) => {
        gl.bindFramebuffer(gl.FRAMEBUFFER, dst.fbo); gl.viewport(0, 0, dst.w, dst.h);
        gl.useProgram(prog.p); gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, src.tex);
        gl.uniform1i(prog.u.uSrc, 0);
        if (setup) setup(prog);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
      };
      const B = t.bloom;
      if (B.length) {
        gl.bindTexture(gl.TEXTURE_2D, t.scene.tex);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        pass(this.progs.pre, t.scene, B[0], (p) => { gl.uniform2f(p.u.uTexel, 1 / S.W * S.ss, 1 / S.H * S.ss); gl.uniform1f(p.u.uThreshold, look.bloomThreshold); gl.uniform1f(p.u.uKnee, Math.max(0.05, look.bloomThreshold * 0.5)); });
        for (let i = 1; i < B.length; i++) pass(this.progs.down, B[i - 1], B[i], (p) => gl.uniform2f(p.u.uTexel, 1 / B[i - 1].w, 1 / B[i - 1].h));
        // up: start from the smallest, add each level on the way back
        let src = B[B.length - 1];
        for (let i = B.length - 2; i >= 0; i--) {
          const dst = t.up[i];
          pass(this.progs.up, src, dst, (p) => { gl.uniform2f(p.u.uTexel, 1 / src.w, 1 / src.h); gl.uniform1f(p.u.uRadius, look.bloomRadius); });
          gl.enable(gl.BLEND); gl.blendFunc(gl.ONE, gl.ONE);
          pass(this.progs.down, B[i], dst, (p) => gl.uniform2f(p.u.uTexel, 0.25 / B[i].w, 0.25 / B[i].h));
          gl.disable(gl.BLEND);
          src = dst;
        }
        this.bloomTex = src.tex;
        gl.bindTexture(gl.TEXTURE_2D, t.scene.tex);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
      }

      // 4. composite to the canvas
      gl.bindFramebuffer(gl.FRAMEBUFFER, null);
      gl.viewport(0, 0, S.w, S.h);
      const c = this.progs.comp;
      gl.useProgram(c.p);
      gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, t.scene.tex); gl.uniform1i(c.u.uScene, 0);
      gl.activeTexture(gl.TEXTURE1); gl.bindTexture(gl.TEXTURE_2D, this.bloomTex || t.scene.tex); gl.uniform1i(c.u.uBloom, 1);
      gl.activeTexture(gl.TEXTURE2); gl.bindTexture(gl.TEXTURE_2D, this.overlayTex); gl.uniform1i(c.u.uOverlay, 2);
      gl.uniform1i(c.u.uSS, S.ss);
      gl.uniform1f(c.u.uExposure, Math.pow(2, look.exposure)); gl.uniform1f(c.u.uBloomStrength, B.length ? look.bloomStrength : 0);
      gl.uniform1f(c.u.uSat, look.saturation); gl.uniform1f(c.u.uContrast, look.contrast);
      gl.uniform1f(c.u.uVignette, look.vignette); gl.uniform1f(c.u.uGrain, look.grain / 255); gl.uniform1f(c.u.uFrame, this.frameNo % 997);
      gl.uniform1i(c.u.uTone, look.tonemap | 0); gl.uniform1f(c.u.uOverlayOn, this.overlayOn ? 1 : 0);
      gl.drawArrays(gl.TRIANGLES, 0, 3);
      gl.activeTexture(gl.TEXTURE0);
      gl.bindVertexArray(null);
      return V;
    }
  }

  CR.Render = { Renderer, piece, writePiece, hexToLinear, srgbToLinear, mul4, lookAt, perspective, invert4 };
})(globalThis.CR = globalThis.CR || {});
