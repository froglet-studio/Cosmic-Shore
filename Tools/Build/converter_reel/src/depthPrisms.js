// The Mass / omni "sealed by depth" prisms, ported from the shipped game:
//   Assets/_Scripts/Utility/DepthRatchet.cs                                  -> DepthRatchet (Seat, Column, Step, RiseTarget,
//                                                                               HorizonSealDepth, ColumnTop, HiddenBehindBody)
//   Assets/_Scripts/Controller/Environment/Crystals/CrystalDepthPrismProfileSO.cs -> profile() (Seat, SealFor)
//   Assets/_Scripts/Controller/Environment/Crystals/CrystalDepthPrisms.cs     -> stepColumns, columnBody, tryGetPlates, MAX_PLATES
//   Assets/_Scripts/Controller/Environment/Crystals/OmniCombinePlayer.cs      -> DepthColumns (+ WriteMass's column fill:
//                                                                               massSites, massPlates, setMassPlates, massFarthest),
//                                                                               DepthShape (Of / Morph), triangleRotation
//   Assets/_Graphics/Materials/Shaders/OmniDepthPrismShader.shader           -> deform() (the VERTEX stage, on the CPU)
//
// Conventions: Unity's own axes (left-handed, Y up), no conversion anywhere. A vector is a plain [x, y, z] array; a
// _DepthState entry is [x, y, z, w]; a quaternion (triangleRotation only) is [w, x, y, z], as combineCore.js has it.
// The column maths (everything but deform) runs in FLOAT32 exactly as the C# does (Math.fround after every operation,
// Unity's own operator order), so it matches the game bit for bit: parity_depth.cjs holds it to the shipped C#.
// deform() is the GPU's maths and runs in doubles (its outputs are Float32Arrays).
//
// _DepthState[plate] = [x, y, z, w]:
//   x  place of the ARRIVING prism in its column: 0 = top of the column (out at _Reach x its radius, _PlateScaleStart of
//      its size, faces split), 1 = sealed in its hole (the mesh as authored)
//   y  the arriving prism's fade-in (1 = solid). The LEAVING prism shows 1 - y (complementary dither pixels)
//   z  place of the LEAVING prism while it fades out (reseat); 1 when no reseat is under way
//   w  Charge strike glow on this prism (0..; drawn as _StrikeColor * w). DepthColumns copies it from .strike
// Plain script, no modules: (function (CR) { ... CR.DepthPrisms = {...}; })(globalThis.CR = globalThis.CR || {}).
(function (CR) {
  'use strict';
  const f = Math.fround;
  const MAX_PLATES = 60;                    // CrystalDepthPrisms.MaxPlates (the shader's _DepthState[60])
  const EPS3 = f(1e-3), EPS4 = f(1e-4), EPS5 = f(1e-5), EPS6 = f(1e-6), EPS8 = f(1e-8);
  const P999 = f(0.999), P101 = f(1.01);

  // ------------------------------------------------------------------ Unity's float maths (Vector3 / Mathf)
  const v3 = (a) => [f(a[0]), f(a[1]), f(a[2])];
  const add = (a, b) => [f(a[0] + b[0]), f(a[1] + b[1]), f(a[2] + b[2])];
  const sub = (a, b) => [f(a[0] - b[0]), f(a[1] - b[1]), f(a[2] - b[2])];
  const mul = (a, s) => [f(a[0] * s), f(a[1] * s), f(a[2] * s)];
  const div = (a, s) => [f(a[0] / s), f(a[1] / s), f(a[2] / s)];
  const dot = (a, b) => f(f(f(a[0] * b[0]) + f(a[1] * b[1])) + f(a[2] * b[2]));
  const mag = (a) => f(Math.sqrt(dot(a, a)));
  const normalized = (a) => { const m = mag(a); return m > EPS5 ? div(a, m) : [0, 0, 0]; };
  const clamp01 = (v) => (v < 0 ? 0 : v > 1 ? 1 : v);
  const max = (a, b) => (a > b ? a : b);
  const min = (a, b) => (a < b ? a : b);
  const lerp = (a, b, t) => f(a + f(f(b - a) * clamp01(t)));                     // Mathf.Lerp
  const lerpV = (a, b, t) => { t = clamp01(t); return [f(a[0] + f(f(b[0] - a[0]) * t)), f(a[1] + f(f(b[1] - a[1]) * t)), f(a[2] + f(f(b[2] - a[2]) * t))]; };
  const pow = (a, b) => f(Math.pow(a, b));                                         // (float)Math.Pow
  function roundToInt(v) {                                                         // Mathf.RoundToInt: half to even
    const r = Math.round(v);
    return Math.abs(v % 1) === 0.5 && r % 2 !== 0 ? r - 1 : r;
  }

  // ------------------------------------------------------------------ DepthRatchet.cs
  const DepthRatchet = {
    /** A prism's place in its column, 0 top .. 1 sealed: smoothstep(open, seal, d) ^ power. */
    seat(depth, openDepth, sealDepth, power) {
      const e = clamp01(f(f(depth - openDepth) / max(f(sealDepth - openDepth), EPS3)));
      return pow(f(f(e * e) * f(3 - f(2 * e))), power);
    },
    /** Column.At(seat): never stepped, seated where its depth puts it, solid. */
    at(seat) { return { place: f(seat), fade: 1, leavingPlace: 1 }; },
    /** A C# default(Column): all zero (what a DepthColumns holds before its first fresh step). */
    zero() { return { place: 0, fade: 0, leavingPlace: 0 }; },
    /** Step(c, depth, seat, dt, reseatDepth, reseatFadeSeconds, riseTargetHidden = false, riseTarget = 0): returns a NEW column
     * (C# struct semantics). Order: fading, following the seat down, rising while hidden, reseating, holding. */
    step(c, depth, seat, dt, reseatDepth, reseatFadeSeconds, riseTargetHidden, riseTarget) {
      c = { place: c.place, fade: c.fade, leavingPlace: c.leavingPlace };
      if (c.fade < 1) {
        c.fade = min(1, f(c.fade + f(dt / max(reseatFadeSeconds, EPS3))));
        c.place = seat;
      } else if (seat >= c.place) {
        c.place = seat;
      } else if (riseTargetHidden) {
        c.place = max(seat, riseTarget || 0);
      } else if (depth < reseatDepth) {
        c.leavingPlace = c.place;
        c.fade = 0;
        c.place = seat;
      }
      return c;
    },
    riseTarget(c, seat, dt, riseRate) { return max(seat, f(c.place - f(riseRate * dt))); },
    horizonSealDepth(plateRadius, cameraDistance, margin) {
      return min(P999, f(f(plateRadius / max(cameraDistance, f(plateRadius * P101))) - margin));
    },
    /** The column top at `place` (crystal space). NB as shipped: the top-face term is NOT multiplied by the column's k, unlike the
     * shader's DepthPrismPosition (see deform) - ported as the C# has it. */
    columnTop(plateCentre, direction, topHeight, place, reach, sizeAtTop) {
      const s1 = f(1 + f(f(reach - 1) * f(1 - place)));
      const s2 = f(topHeight * f(sizeAtTop + f(f(1 - sizeAtTop) * place)));
      return add(mul(plateCentre, s1), mul(direction, s2));
    },
    hiddenBehindBody(point, eye, bodyRadius) {
      const d = sub(point, eye);
      const length = mag(d);
      if (length < EPS6) return false;
      const u = div(d, length);
      const b = dot(eye, u), c = f(dot(eye, eye) - f(bodyRadius * bodyRadius)), disc = f(f(b * b) - c);
      if (disc <= 0) return false;
      const enter = f(-b - f(Math.sqrt(disc)));
      return enter > 0 && enter < f(length - EPS6);
    },
  };

  // ------------------------------------------------------------------ CrystalDepthPrismProfileSO
  /** profile(json): json is data/depth.json's `profile` (the asset's serialized names). Returns frozen float32 values plus
   * seat(depth, seal) and sealFor(plateRadius, cameraDistance). */
  function profile(json) {
    const p = {
      openDepth: f(json.openDepth), sealDepth: f(json.sealDepth), profilePower: f(json.profilePower),
      reseatDepth: f(json.reseatDepth), reseatFadeSeconds: f(json.reseatFadeSeconds), orientationJitter: f(json.orientationJitter),
      sealAtHorizon: !!json.sealAtHorizon, horizonMargin: f(json.horizonMargin), riseWhileHidden: !!json.riseWhileHidden,
      riseRate: f(json.riseRate), occluderRadius: f(json.occluderRadius),
    };
    p.seat = (depth, seal) => DepthRatchet.seat(depth, p.openDepth, seal === undefined ? p.sealDepth : seal, p.profilePower);
    p.sealFor = (plateRadius, cameraDistance) =>
      (p.sealAtHorizon ? DepthRatchet.horizonSealDepth(plateRadius, cameraDistance, p.horizonMargin) : p.sealDepth);
    return Object.freeze(p);
  }

  // ------------------------------------------------------------------ CrystalDepthPrisms.cs
  /** StepColumns: every column's ratchet against `eye` (the camera, in the prisms' own object space). `columns` (array of
   * {place, fade, leavingPlace}) is updated in place; the other arrays are read. */
  function stepColumns(columns, directions, centres, tops, plateRadius, eye, dt, fresh, prof, reach, sizeAtTop) {
    const DR = DepthRatchet;
    eye = v3(eye); dt = f(dt); plateRadius = f(plateRadius); reach = f(reach); sizeAtTop = f(sizeAtTop);
    const distance = mag(eye);
    if (distance < EPS4) return;
    const toCamera = div(eye, distance);
    const seal = prof.sealFor(plateRadius, distance);
    const rise = prof.riseWhileHidden && !fresh;
    const occluder = f(prof.occluderRadius * plateRadius);
    for (let j = 0; j < columns.length; j++) {
      const depth = dot(directions[j], toCamera);
      const seat = prof.seat(depth, seal);
      if (fresh) { columns[j] = DR.at(seat); continue; }
      let hidden = false, target = 0;
      if (rise && seat < columns[j].place && columns[j].fade >= 1) {
        target = DR.riseTarget(columns[j], seat, dt, prof.riseRate);
        hidden = DR.hiddenBehindBody(DR.columnTop(centres[j], directions[j], tops[j], target, reach, sizeAtTop), eye, occluder);
      }
      columns[j] = DR.step(columns[j], depth, seat, dt, prof.reseatDepth, prof.reseatFadeSeconds, hidden, target);
    }
  }

  /** ColumnBody: a prism's body at column place `place` -> {centre, radius} (centre = plateCentre * lerp(reach, 1, place),
   * radius = (bound + split * (1 - place)) * k * lerp(sizeStart, 1, place)). `split` = faceSplit * splitUnit. */
  function columnBody(plateCentre, bound, place, reach, sizeStart, split) {
    const k = lerp(f(reach), 1, f(place));
    const scale = f(k * lerp(f(sizeStart), 1, f(place)));
    return { centre: mul(v3(plateCentre), k), radius: f(f(f(bound) + f(f(split) * f(1 - f(place)))) * scale) };
  }

  /** TryGetPlates on a baked prism mesh {positions (3n), uv2 (4n: plate centre xyz, plate index w)} ->
   * {directions, centres, tops, bounds, plateRadius}, or {problem} when the mesh is refused (as the C# refuses it). */
  function tryGetPlates(mesh) {
    const P = mesh.positions, U = mesh.uv2, n = P.length / 3;
    if (!U || U.length !== 4 * n) return { problem: 'mesh carries no plate data in TEXCOORD2' };
    let count = 0;
    for (let i = 0; i < n; i++) count = Math.max(count, roundToInt(f(U[4 * i + 3])) + 1);
    if (count < 1 || count > MAX_PLATES) return { problem: `mesh has ${count} plates (1..${MAX_PLATES} supported)` };
    const directions = new Array(count).fill(null).map(() => [0, 0, 0]);
    for (let i = 0; i < n; i++) {
      const c = v3([U[4 * i], U[4 * i + 1], U[4 * i + 2]]);
      if (dot(c, c) > 0) directions[roundToInt(f(U[4 * i + 3]))] = normalized(c);
    }
    for (let j = 0; j < count; j++) if (dot(directions[j], directions[j]) < 9.99999944e-11) return { problem: `plate ${j} has no centre` };
    const centres = new Array(count).fill(null).map(() => [0, 0, 0]);
    const tops = new Array(count).fill(-Infinity), bounds = new Array(count).fill(0);
    for (let i = 0; i < n; i++) {
      const j = roundToInt(f(U[4 * i + 3]));
      const centre = v3([U[4 * i], U[4 * i + 1], U[4 * i + 2]]);
      const v = v3([P[3 * i], P[3 * i + 1], P[3 * i + 2]]);
      centres[j] = centre;
      tops[j] = max(tops[j], dot(sub(v, centre), directions[j]));
      bounds[j] = max(bounds[j], mag(sub(v, centre)));
    }
    let radius = 0;
    for (const c of centres) radius = f(radius + mag(c));
    radius = f(radius / count);
    if (radius <= 0) return { problem: 'plate centres at the origin' };
    return { directions, centres, tops, bounds, plateRadius: radius };
  }

  // ------------------------------------------------------------------ OmniCombinePlayer.DepthColumns
  /** One depth-prism renderer's columns. new DepthColumns(plates) (the Mass: fill with setMassPlates each frame) or
   * new DepthColumns(plates, directions, centres, tops, bounds, plateRadius) (the triangles: from tryGetPlates). */
  class DepthColumns {
    constructor(plates, directions, centres, tops, bounds, plateRadius) {
      const z3 = () => [0, 0, 0];
      this.directions = directions || Array.from({ length: plates }, z3);
      this.centres = centres || Array.from({ length: plates }, z3);
      this.tops = tops || new Array(plates).fill(0);
      this.bounds = bounds || new Array(plates).fill(0);
      this.plateRadius = f(plateRadius || 0);
      this.strike = new Array(plates).fill(0);                 // written by the Charge strike (chargeArcs), read into state.w
      this.state = Array.from({ length: MAX_PLATES }, () => [0, 0, 0, 0]);   // the shader's _DepthState (zeros past count)
      this.fresh = true;
      this.columns = Array.from({ length: plates }, DepthRatchet.zero);
    }
    get count() { return this.columns.length; }

    /** Step(eye, dt, profile, reach, sizeAtTop, strength, rest = 1): the ratchet as in play, then state ramped by `strength`
     * (0 = every prism solid at place `rest`, 1 = the ratchet). No-op (state kept, fresh kept) when the eye is at the
     * origin or plateRadius <= 0. */
    step(eye, dt, prof, reach, sizeAtTop, strength, rest = 1) {
      const e = v3(eye);
      if (dot(e, e) < EPS8 || this.plateRadius <= 0) return;
      stepColumns(this.columns, this.directions, this.centres, this.tops, this.plateRadius, e, dt, this.fresh, prof, reach, sizeAtTop);
      strength = f(strength); rest = f(rest);
      for (let j = 0; j < this.columns.length; j++) {
        const c = this.columns[j];
        this.state[j] = [lerp(rest, c.place, strength), lerp(1, c.fade, strength), lerp(1, c.leavingPlace, strength), f(this.strike[j])];
      }
      this.fresh = false;
    }

    /** WriteMass's column fill: plates[q] = [cx, cy, cz, q] (from massPlates), every top at `topHeight` (the morphed shape's). */
    setMassPlates(plates, topHeight) {
      let radius = 0;
      for (let q = 0; q < plates.length; q++) {
        const c = v3(plates[q]);
        this.centres[q] = c;
        this.directions[q] = normalized(c);
        this.tops[q] = f(topHeight);
        radius = f(radius + mag(c));
      }
      this.plateRadius = f(radius / Math.max(1, plates.length));
    }

    /** The state as one Float32Array(240) for a uniform vec4[60] upload. */
    stateArray(out) {
      out = out || new Float32Array(4 * MAX_PLATES);
      for (let j = 0; j < MAX_PLATES; j++) for (let k = 0; k < 4; k++) out[4 * j + k] = this.state[j][k];
      return out;
    }
  }

  /** The plates sharing each omni triangle (OmniCombinePlayer.Build): plateSite (the Mass crystal's, per plate) -> per plate,
   * the list of plates with its site, in the C# order (plates ascending; the order sets the float summation). */
  function massSites(plateSite) {
    const by = new Map();
    for (let q = 0; q < plateSite.length; q++) { if (!by.has(plateSite[q])) by.set(plateSite[q], []); by.get(plateSite[q]).push(q); }
    return plateSite.map((s) => by.get(s).slice());
  }

  /** WriteMass's plate centres: `pose` (flat xyz per Mass vertex, stage space), `centre` (the crystal's flight centre = the
   * Mass renderer's localPosition), `plateOf` (vertex -> plate), `sites` (massSites), `morph` (per plate 0..1).
   * Returns {plates: [[cx, cy, cz, q]...] (centres relative to `centre`: Piece.Write's UV2 and the columns), morph: mean}. */
  function massPlates(pose, centre, plateOf, sites, morph) {
    const np = sites.length;
    centre = v3(centre);
    const sum = Array.from({ length: np }, () => [0, 0, 0]), count = new Array(np).fill(0);
    for (let i = 0; i < plateOf.length; i++) {
      sum[plateOf[i]] = add(sum[plateOf[i]], sub(v3([pose[3 * i], pose[3 * i + 1], pose[3 * i + 2]]), centre));
      count[plateOf[i]]++;
    }
    let m = 0;
    const plates = new Array(np);
    for (let q = 0; q < np; q++) {
      const own = div(sum[q], Math.max(1, count[q]));
      let site = [0, 0, 0], n = 0;
      for (const s of sites[q]) { site = add(site, sum[s]); n += count[s]; }
      site = div(site, Math.max(1, n));
      const c = lerpV(own, site, f(morph[q]));
      plates[q] = [c[0], c[1], c[2], q];
      m = f(m + f(morph[q]));
    }
    return { plates, morph: f(m / Math.max(1, np)) };
  }

  /** How far out the farthest Mass prism stands (WriteMass's return), in stage units. */
  function massFarthest(centre, plates, shape, state) {
    let far = 0;
    for (let q = 0; q < plates.length; q++) far = max(far, mag(add(v3(centre), mul(v3(plates[q]), lerp(shape.reach, 1, state[q][0])))));
    return far;
  }

  // ------------------------------------------------------------------ OmniCombinePlayer.DepthShape
  /** {thickness, topHeight, splitUnit, reach, faceSplit, sizeStart} in one renderer's object units. */
  const DepthShape = {
    /** Of(material, unit, baked): `material` = data/depth.json materials.mass / .omni. Converter uses:
     *   mass      = of(materials.mass, combine.massMeshScale, combine.depth.massThickness)   (the thin combine prisms)
     *   triThin   = of(materials.omni, 1, combine.depth.omniThickness)                        (what the Mass morphs toward)
     *   triangles = of(materials.omni, 1, 1)                                                  (OmniCrystalTriangles, thick baked) */
    of(m, unit, baked) {
      unit = f(unit);
      return Object.freeze({
        thickness: f(f(m._Thickness) * f(baked)), topHeight: f(f(m._TopHeight) * unit), splitUnit: f(f(m._SplitUnit) * unit),
        reach: f(m._Reach), faceSplit: f(m._FaceSplit), sizeStart: f(m._PlateScaleStart),
      });
    },
    /** Morph(a, b, t): thickness, topHeight, splitUnit lerp a -> b; reach, faceSplit, sizeStart stay a's. */
    morph(a, b, t) {
      return Object.freeze({
        thickness: lerp(a.thickness, b.thickness, t), topHeight: lerp(a.topHeight, b.topHeight, t),
        splitUnit: lerp(a.splitUnit, b.splitUnit, t), reach: a.reach, faceSplit: a.faceSplit, sizeStart: a.sizeStart,
      });
    },
  };

  /** OmniCombinePlayer.TriangleRotation(r): r = combine.omniTriangleRotation (row-major R: the game's triangle mesh -> the
   * omni) as Quaternion.LookRotation(R's column 2, R's column 1). Returns {matrix: row-major 3x3 (9 numbers; columns are where
   * the mesh's x, y, z go), quaternion: [w, x, y, z]}. Stage point = matrix * meshPoint; mesh point = transpose * stagePoint. */
  function triangleRotation(r) {
    const nz = (a) => { const l = Math.hypot(a[0], a[1], a[2]); return [a[0] / l, a[1] / l, a[2] / l]; };
    const cr = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    const z = nz([r[2], r[5], r[8]]), x = nz(cr([r[1], r[4], r[7]], z)), y = cr(z, x);
    const m = [x[0], y[0], z[0], x[1], y[1], z[1], x[2], y[2], z[2]];
    const tr = m[0] + m[4] + m[8];
    let q;
    if (tr > 0) { const s = Math.sqrt(tr + 1) * 2; q = [0.25 * s, (m[7] - m[5]) / s, (m[2] - m[6]) / s, (m[3] - m[1]) / s]; }
    else if (m[0] > m[4] && m[0] > m[8]) { const s = Math.sqrt(1 + m[0] - m[4] - m[8]) * 2; q = [(m[7] - m[5]) / s, 0.25 * s, (m[1] + m[3]) / s, (m[2] + m[6]) / s]; }
    else if (m[4] > m[8]) { const s = Math.sqrt(1 + m[4] - m[0] - m[8]) * 2; q = [(m[2] - m[6]) / s, (m[1] + m[3]) / s, 0.25 * s, (m[5] + m[7]) / s]; }
    else { const s = Math.sqrt(1 + m[8] - m[0] - m[4]) * 2; q = [(m[3] - m[1]) / s, (m[2] + m[6]) / s, (m[5] + m[7]) / s, 0.25 * s]; }
    return { matrix: m, quaternion: q };
  }

  /** The triangles mesh from data/depth.json: {vertexCount, positions, normals, uv2, indices} (Float32Array / Uint16Array). */
  function trianglesMesh(depthJson) {
    const t = depthJson.triangles, n = t.vertexCount;
    const uv2 = new Float32Array(4 * n);
    for (let i = 0; i < n; i++) {
      const q = t.plate[i];
      uv2[4 * i] = t.plateCentres[3 * q]; uv2[4 * i + 1] = t.plateCentres[3 * q + 1]; uv2[4 * i + 2] = t.plateCentres[3 * q + 2]; uv2[4 * i + 3] = q;
    }
    return { vertexCount: n, positions: new Float32Array(t.positions), normals: new Float32Array(t.normals), uv2, indices: new Uint16Array(t.indices) };
  }

  // ------------------------------------------------------------------ OmniDepthPrismShader: the VERTEX stage on the CPU
  function stateAt(state, j) {
    if (state.length === 4 * MAX_PLATES && typeof state[0] === 'number') return [state[4 * j], state[4 * j + 1], state[4 * j + 2], state[4 * j + 3]];
    return state[j] || [0, 0, 0, 0];
  }

  /**
   * deform(mesh, state, shape, opts) - OmniDepthPrismShader's DepthPrismVert for every vertex, in OBJECT space.
   *   mesh   {positions (3n), normals (3n), uv2 (4n: plate centre xyz, plate index w)}. For the triangles: trianglesMesh().
   *          For the morphing Mass prisms: the host's unwelded Piece mesh, uv2 = massPlates().plates[plateOfCorner] per corner.
   *   state  DepthColumns.state (array of [x,y,z,w]) or a flat Float32Array(240).
   *   shape  a DepthShape (thickness, topHeight, splitUnit, reach, faceSplit, sizeStart).
   *   opts   {pass: 'arriving' (default) | 'leaving', driven: true (_DepthDriven; false = identity, the mesh as authored),
   *           thicknessInward: 1, spread: [0.01, 0.01, 0.01] (material _Spread, WORLD units),
   *           objectScale: 1 | [sx, sy, sz] (the renderer's world scale: the spread is divided by it), out: reuse buffers}
   * Returns {positions, normals, alpha, strike} (Float32Arrays, n or 3n): positions = the displaced vertex INCLUDING the spread
   * push (what UnityObjectToClipPos receives), normals = the mesh normals normalised (the shader shades with the UNdeformed
   * normal), alpha = how solid (arriving: y, leaving: 1 - y; <= 0 means the GPU collapses the vertex - draw nothing),
   * strike = max(w, 0) (0 undriven).
   */
  function deform(mesh, state, shape, opts) {
    opts = opts || {};
    const leaving = opts.pass === 'leaving';
    const driven = opts.driven !== false;
    const inward = opts.thicknessInward === undefined ? 1 : opts.thicknessInward;
    const spread = opts.spread || [0.01, 0.01, 0.01];
    const os = opts.objectScale === undefined ? [1, 1, 1] : typeof opts.objectScale === 'number' ? [opts.objectScale, opts.objectScale, opts.objectScale] : opts.objectScale;
    const P = mesh.positions, N = mesh.normals, U = mesh.uv2, n = P.length / 3;
    const out = opts.out || {};
    const pos = out.positions && out.positions.length === 3 * n ? out.positions : new Float32Array(3 * n);
    const nrm = out.normals && out.normals.length === 3 * n ? out.normals : new Float32Array(3 * n);
    const alpha = out.alpha && out.alpha.length === n ? out.alpha : new Float32Array(n);
    const strike = out.strike && out.strike.length === n ? out.strike : new Float32Array(n);
    const { thickness, topHeight, splitUnit, reach, faceSplit, sizeStart } = shape;
    for (let i = 0; i < n; i++) {
      let vx = P[3 * i], vy = P[3 * i + 1], vz = P[3 * i + 2];
      const nl = Math.hypot(N[3 * i], N[3 * i + 1], N[3 * i + 2]) || 1;
      const nx = N[3 * i] / nl, ny = N[3 * i + 1] / nl, nz = N[3 * i + 2] / nl;
      const j = Math.min(MAX_PLATES - 1, Math.max(0, Math.floor(U ? U[4 * i + 3] + 0.5 : 0)));
      const st = stateAt(state, j);
      let a;
      if (!driven) {
        a = leaving ? 0 : 1;
      } else {
        const u = leaving ? st[2] : st[0];
        a = leaving ? 1 - st[1] : st[1];
        // DepthPrismPosition(vertex, normal, centre, u)
        const cx = U[4 * i], cy = U[4 * i + 1], cz = U[4 * i + 2];
        const rr = Math.hypot(cx, cy, cz), dx = cx / rr, dy = cy / rr, dz = cz / rr;
        const k = reach + (1 - reach) * u;                              // lerp(_Reach, 1, u), unclamped like HLSL
        let lx = vx - cx, ly = vy - cy, lz = vz - cz;
        const t = (lx * dx + ly * dy + lz * dz - topHeight * inward) * (thickness - 1);
        lx += dx * t; ly += dy * t; lz += dz * t;
        const sp = faceSplit * splitUnit * (1 - u);
        lx += nx * sp; ly += ny * sp; lz += nz * sp;
        const ls = k * (sizeStart + (1 - sizeStart) * u);
        vx = dx * rr * k + lx * ls; vy = dy * rr * k + ly * ls; vz = dz * rr * k + lz * ls;
      }
      // SpreadFresnelDisplace: out along the normal by _Spread WORLD units.
      pos[3 * i] = vx + nx * (spread[0] / os[0]);
      pos[3 * i + 1] = vy + ny * (spread[1] / os[1]);
      pos[3 * i + 2] = vz + nz * (spread[2] / os[2]);
      nrm[3 * i] = nx; nrm[3 * i + 1] = ny; nrm[3 * i + 2] = nz;
      alpha[i] = a;
      strike[i] = driven ? Math.max(st[3], 0) : 0;
    }
    return { positions: pos, normals: nrm, alpha, strike };
  }

  /** Both passes at once: {arriving, leaving, leavingVisible} (leavingVisible: some vertex's leaving prism has alpha > 0, i.e. a
   * reseat is under way - draw the leaving pass only then). */
  function deformBoth(mesh, state, shape, opts) {
    opts = opts || {};
    const arriving = deform(mesh, state, shape, Object.assign({}, opts, { pass: 'arriving', out: opts.outArriving }));
    const leaving = deform(mesh, state, shape, Object.assign({}, opts, { pass: 'leaving', out: opts.outLeaving }));
    let leavingVisible = false;
    for (let i = 0; i < leaving.alpha.length && !leavingVisible; i++) leavingVisible = leaving.alpha[i] > 0;
    return { arriving, leaving, leavingVisible };
  }

  CR.DepthPrisms = {
    MAX_PLATES,
    profile,
    DepthRatchet,
    stepColumns,
    columnBody,
    tryGetPlates,
    DepthColumns,
    DepthShape,
    massSites,
    massPlates,
    massFarthest,
    triangleRotation,
    trianglesMesh,
    deform,
    deformBoth,
    f32: { v3, add, sub, mul, div, dot, mag, normalized, lerp, lerpV, roundToInt },
  };
})(globalThis.CR = globalThis.CR || {});
