// Parametric prism whale - the target for the learned prism automaton.
//
// One file, run in two places: the whale designer page (live, with sliders) and node, where
// whale_target.py turns a saved parameter set into training frames. What the designer shows IS
// the target.
//
// Units are target voxels. Whale frame: +x toward the head, +y up (dorsal), +z to the whale's
// right. Every prism is { p:[x,y,z], h:[hx,hy,hz] (half-extents), R:[9] (row-major; its COLUMNS
// are the prism's local axes in world), dom: 0 Jade | 1 Ruby | 2 Gold, tier: 0 plain box |
// 1 danger box | 2 shield octahedron, part }. A shield octahedron reaches 3x its half-extents
// along each axis (the game's circumscribing scale), so a shielded plate is sized to fit the cell
// its box would have filled: h = cell / 3, vertices on the box's face centres.
(function (root) {
  const TAU = Math.PI * 2;
  const SHIELD = 3.0;
  const HMIN = 0.25, HMAX = 3.0;         // the automaton's half-extent range (model v2)

  const DEFAULTS = {
    // training grid (voxels): the whale is centred in it and must stay inside on every frame
    gridX: 88, gridY: 36, gridZ: 44,
    // body
    length: 56, girth: 0.11, girthPeak: 0.3, headRound: 0.55, tailTaper: 1.6, peduncle: 0.1,
    flatten: 0.8, bellyDrop: 0.18, noseCap: 1,
    // plates on the body
    ringSpacing: 2.8, arcSpacing: 2.8, plateLength: 0.9, plateWidth: 0.9, plateThickness: 0.8,
    gap: 0.15, stagger: 1, core: 1, coreSpacing: 3.4,
    // flippers
    flipperLength: 0.28, flipperChord: 0.07, flipperPos: 0.3, flipperSweep: 35, flipperDroop: 25,
    flipperTaper: 0.35, flipperThickness: 0.9, finSpacing: 1.9,
    // dorsal fin
    dorsal: 1, dorsalHeight: 0.05, dorsalChord: 0.08, dorsalPos: 0.66, dorsalSweep: 40,
    // flukes
    flukeSpan: 0.3, flukeChord: 0.1, flukeSweep: 28, flukeNotch: 0.35, flukeTaper: 0.4,
    // colour regions: [domain, tier]
    back: [0, 0], belly: [2, 0], countershade: -0.15, pleats: 1, pleatColor: [2, 2], pleatEnd: 0.45,
    mouth: 1, mouthColor: [1, 1], mouthEnd: 0.2, mouthAngle: -20, mouthBand: 14,
    fins: [0, 0], finEdge: [2, 0], flukeColor: [0, 0], dorsalColor: [0, 0], coreColor: [0, 0],
    eye: 1, eyeColor: [1, 2], eyePos: 0.14, eyeAngle: 8,
    // swim (vertical, like a real cetacean)
    frames: 8, swimAmp: 0.05, swimWave: 1.1, swimEnv: 2.2, flukePitch: 22, flipperFlap: 12,
  };

  // ---- small linear algebra --------------------------------------------------------------
  const add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
  const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
  const mul = (a, s) => [a[0] * s, a[1] * s, a[2] * s];
  const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const norm = a => { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; };
  function frame(t, n) {                 // axes [t, n x t, n]: orthonormal, right-handed, z = n
    n = norm(n);
    t = norm(sub(t, mul(n, dot(t, n))));
    return [t, cross(n, t), n];
  }
  const I3 = [[1, 0, 0], [0, 1, 0], [0, 0, 1]];
  const toR = ax => [ax[0][0], ax[1][0], ax[2][0], ax[0][1], ax[1][1], ax[2][1], ax[0][2], ax[1][2], ax[2][2]];
  const rotZ = (v, a) => { const c = Math.cos(a), s = Math.sin(a); return [c * v[0] - s * v[1], s * v[0] + c * v[1], v[2]]; };
  const rotAxis = (v, k, a) => {         // Rodrigues, unit axis k
    const c = Math.cos(a), s = Math.sin(a), kv = cross(k, v), kd = dot(k, v);
    return [v[0] * c + kv[0] * s + k[0] * kd * (1 - c), v[1] * c + kv[1] * s + k[1] * kd * (1 - c), v[2] * c + kv[2] * s + k[2] * kd * (1 - c)];
  };
  const deg = d => d * Math.PI / 180;

  // ---- body -----------------------------------------------------------------------------
  function radius(P, s) {                // half-width of the body at spine fraction s (0 = snout, 1 = tail)
    const rmax = P.girth * P.length;
    if (s <= P.girthPeak) {
      const u = s / P.girthPeak;
      return rmax * Math.pow(Math.max(0, u * (2 - u)), P.headRound);
    }
    const t = (s - P.girthPeak) / (1 - P.girthPeak);
    return rmax * (1 - (1 - P.peduncle) * Math.pow(t, P.tailTaper));
  }
  const spineX = (P, s) => P.length / 2 - s * P.length;
  // Elliptic section: half-width w (along z), half-height w*flatten (along y), centre dropped by
  // bellyDrop*w. th is measured from +z (the whale's right) toward +y (dorsal).
  function section(P, s, th) {
    const w = Math.max(radius(P, s), 1e-3), hgt = w * P.flatten, yc = -P.bellyDrop * w;
    const y = yc + hgt * Math.sin(th), z = w * Math.cos(th);
    return { p: [spineX(P, s), y, z], n: norm([0, (y - yc) / (hgt * hgt), z / (w * w)]), w, hgt, yc };
  }
  function perimeter(a, b) { return Math.PI * (3 * (a + b) - Math.sqrt((3 * a + b) * (a + 3 * b))); }
  // Arc-length-uniform angles around an ellipse (so plates are evenly spaced on the skin).
  function evenAngles(w, h, n, phase) {
    const M = 720, cum = [0];
    for (let i = 1; i <= M; i++) {
      const t = TAU * (i - 0.5) / M;
      cum.push(cum[i - 1] + Math.hypot(w * Math.sin(t), h * Math.cos(t)) * TAU / M);
    }
    const total = cum[M], out = [];
    for (let j = 0; j < n; j++) {
      let target = total * ((j + phase) / n) % total, i = 1;
      while (i < M && cum[i] < target) i++;
      out.push(TAU * (i - 1 + (target - cum[i - 1]) / Math.max(cum[i] - cum[i - 1], 1e-9)) / M);
    }
    return { angles: out, arc: total / n };
  }
  const curvatureRadius = (w, h, t) => Math.pow(w * w * Math.sin(t) ** 2 + h * h * Math.cos(t) ** 2, 1.5) / (w * h);

  function bodyColour(P, s, th, j) {
    const d = th * 180 / Math.PI, low = Math.sin(th) < P.countershade;
    const dist = a => Math.abs(((d - a) % 360 + 540) % 360 - 180);
    if (P.mouth && s < P.mouthEnd && Math.min(dist(P.mouthAngle), dist(180 - P.mouthAngle)) < P.mouthBand) return P.mouthColor;
    if (low && P.pleats && s < P.pleatEnd && j % 2 === 0) return P.pleatColor;
    return low ? P.belly : P.back;
  }

  // ---- the rest pose, each prism tagged with the spine fraction it rides on ------------------
  function restPose(P) {
    const out = [];
    const L = P.length, ht = P.plateThickness / 2;
    const push = (s, part, p, axes, h, col, extra) => out.push(Object.assign({ s, part, p, axes, h, col }, extra || {}));
    // body shell: rings along the spine, plates around each ring
    const nRing = Math.max(3, Math.round(L / P.ringSpacing)), rs = L / nRing;
    const hl = Math.max(HMIN, 0.5 * Math.min(P.plateLength * rs, rs - P.gap));
    let firstRing = null;
    for (let i = 0; i < nRing; i++) {
      const s = (i + 0.5) / nRing;
      const w = radius(P, s), hgt = w * P.flatten;
      if (hgt < 2 * ht + 0.6) {                      // too thin for a ring: one keel prism instead
        if (firstRing === null) firstRing = s;
        if (w > 0.05) push(s, "body", [spineX(P, s), -P.bellyDrop * w, 0], I3,
          [hl, Math.max(HMIN, Math.min(HMAX, hgt)), Math.max(HMIN, Math.min(HMAX, w))], P.back);
        continue;
      }
      if (firstRing === null) firstRing = s;
      const n = Math.max(4, Math.round(perimeter(w, hgt) / P.arcSpacing));
      const { angles, arc } = evenAngles(w, hgt, n, P.stagger ? 0.5 * (i % 2) : 0);
      angles.forEach((th, j) => {
        const a = section(P, s, th), b = section(P, s + 1e-3, th);
        const axes = frame(sub(a.p, b.p), a.n);                      // local x toward the head, z outward
        // no overlap around a convex ring: the chord at the plate's INNER face, at the local curvature
        const rho = curvatureRadius(w, hgt, th);
        const hw = 0.5 * P.plateWidth * arc * Math.max(0.15, (rho - 2 * ht) / rho) - P.gap / 2;
        push(s, "body", sub(a.p, mul(a.n, ht)), axes, [hl, Math.max(HMIN, hw), ht], bodyColour(P, s, th, j));
      });
    }
    // nose cap: a tight cluster closing the snout ring
    if (P.noseCap && firstRing !== null) {
      const w0 = radius(P, firstRing), x0 = spineX(P, firstRing) + hl + P.gap;
      const hc = Math.max(HMIN, Math.min(w0 * 0.55, 2.2)), yc = -P.bellyDrop * w0;
      push(0, "body", [x0 + hc * 0.6, yc, 0], I3, [hc * 0.6, hc, hc * 1.1], P.back);
    }
    // core: a lattice of cubes filling what the shell leaves inside
    if (P.core) {
      const cs = P.coreSpacing, hc = cs / 2 - P.gap / 2;
      for (let x = -L / 2 + cs / 2; x < L / 2; x += cs) {
        let wmin = 1e9, hmin = 1e9;                                   // the thinnest section the cube spans
        for (const dx of [-hc, 0, hc]) {
          const sx = Math.min(1, Math.max(0, (L / 2 - x - dx) / L)), w = radius(P, sx);
          wmin = Math.min(wmin, w); hmin = Math.min(hmin, w * P.flatten);
        }
        const s = (L / 2 - x) / L, w = radius(P, s), yc = -P.bellyDrop * w;
        const clear = 2 * ht + P.gap + hc * Math.sqrt(2);
        const a = wmin - clear, b = hmin - clear;
        if (a < 0.4 || b < 0.4) continue;
        const ny = Math.floor(b / cs + 0.5), nz = Math.floor(a / cs + 0.5);
        for (let iy = -ny; iy <= ny; iy++) for (let iz = -nz; iz <= nz; iz++) {
          const y = iy * cs, z = iz * cs;
          if (Math.hypot(y / b, z / a) <= 1) push(s, "core", [x, yc + y, z], I3, [hc, hc, hc], P.coreColor);
        }
      }
    }
    // flippers: a plate grid in the fin plane, both sides, swept back and drooped
    for (const side of [1, -1]) {
      const s0 = P.flipperPos, rt = section(P, s0, side > 0 ? -0.35 : Math.PI + 0.35);
      let span = [0, 0, side];
      span = rotAxis(span, [0, 1, 0], -side * deg(P.flipperSweep));
      span = norm(rotAxis(span, [1, 0, 0], side * deg(P.flipperDroop)));
      const chordDir = norm(sub([-1, 0, 0], mul(span, dot([-1, 0, 0], span))));  // trailing edge back
      const axes = [span, chordDir, cross(span, chordDir)];
      const len = P.flipperLength * L, ch0 = P.flipperChord * L;
      const nS = Math.max(2, Math.round(len / P.finSpacing)), hs = 0.5 * len / nS - P.gap / 2;
      const base = add(rt.p, mul(rt.n, P.flipperThickness / 2 + 0.3));  // just clear of the skin
      for (let a = 0; a < nS; a++) {
        const u = (a + 0.5) / nS;
        const chord = ch0 * (1 - (1 - P.flipperTaper) * u) * (0.65 + 0.35 * Math.sin(Math.PI * u));
        const nC = Math.max(1, Math.round(chord / P.finSpacing));
        for (let b = 0; b < nC; b++) {
          const v = (b + 0.5) / nC - 0.5;
          const p = add(add(base, mul(span, u * len)), mul(chordDir, v * chord));
          const edge = nC > 2 && b === 0;
          push(s0, edge ? "finEdge" : "fins", p, axes,
            [Math.max(HMIN, hs), Math.max(HMIN, 0.5 * chord / nC - P.gap / 2), Math.max(HMIN, P.flipperThickness / 2 * (1 - 0.5 * u))],
            edge ? P.finEdge : P.fins, { fin: { side, root: base } });
        }
      }
    }
    // dorsal fin: vertical plates on the back, raked aft
    if (P.dorsal) {
      const s0 = P.dorsalPos, top = section(P, s0, Math.PI / 2);
      const hgt = P.dorsalHeight * L, ch = P.dorsalChord * L;
      const nH = Math.max(1, Math.round(hgt / P.finSpacing));
      for (let a = 0; a < nH; a++) {
        const u = (a + 0.5) / nH, c = ch * (1 - u * 0.75), back = Math.tan(deg(P.dorsalSweep)) * u * hgt;
        const nC = Math.max(1, Math.round(c / P.finSpacing));
        for (let b = 0; b < nC; b++) {
          const v = (b + 0.5) / nC - 0.5;
          push(s0, "dorsalColor", add(top.p, [-back + v * c, u * hgt + 0.5, 0]), I3,
            [Math.max(HMIN, 0.5 * c / nC - P.gap / 2), Math.max(HMIN, 0.5 * hgt / nH - P.gap / 2), 0.45], P.dorsalColor);
        }
      }
    }
    // flukes: horizontal plates behind the tail stock, swept back, notched in the middle
    {
      const tail = spineX(P, 1), span = P.flukeSpan * L, ch = P.flukeChord * L;
      const nS = Math.max(2, Math.round((span / 2) / P.finSpacing)), hs = 0.25 * span / nS - P.gap / 2;
      const yT = -P.bellyDrop * radius(P, 1);
      for (const side of [1, -1]) for (let a = 0; a < nS; a++) {
        const u = (a + 0.5) / nS, sweep = Math.tan(deg(P.flukeSweep)) * u * span / 2;
        // the notch is cut out of the TRAILING edge at the midline; the leading edge stays on the tail stock
        const c = Math.max(1, ch * (1 - (1 - P.flukeTaper) * u) - P.flukeNotch * ch * Math.max(0, 1 - u * 2.5)), lead = 0;
        const nC = Math.max(1, Math.round(c / P.finSpacing));
        for (let b = 0; b < nC; b++) {
          const v = (b + 0.5) / nC;
          push(1, "flukeColor", [tail - P.gap - sweep - lead - v * c, yT, side * (u * span / 2 + 0.2)],
            [[1, 0, 0], [0, 0, 1], [0, -1, 0]],
            [Math.max(HMIN, 0.5 * c / nC - P.gap / 2), Math.max(HMIN, hs), 0.4], P.flukeColor, { fluke: true });
        }
      }
    }
    // eyes: the shell plate nearest each eye point takes the eye colour (an eye is a plate, not a
    // prism stuck on the skin - that one would sit proud of the neighbouring ring's tangent plates)
    if (P.eye) {
      for (const side of [1, -1]) {
        const e = section(P, P.eyePos, side > 0 ? deg(P.eyeAngle) : Math.PI - deg(P.eyeAngle)).p;
        let best = null, bd = 1e9;
        for (const q of out) if (q.part === "body") { const d = Math.hypot(...sub(q.p, e)); if (d < bd) { bd = d; best = q; } }
        if (best) { best.col = P.eyeColor; best.part = "eyeColor"; }
      }
    }
    return out;
  }

  // ---- the swim: a vertical travelling wave down the spine, flukes pitching ----------------
  function heave(P, s, k) {
    return P.swimAmp * P.length * Math.pow(s, P.swimEnv) * Math.sin(TAU * (s / P.swimWave - k / P.frames));
  }
  function swimAt(P, s, k) {
    const ds = 1e-3, dy = heave(P, s, k);
    // tangent toward the head: from spine point s + ds to spine point s
    return { dy, pitch: Math.atan2(dy - heave(P, s + ds, k), ds * P.length) };
  }

  function build(params, k) {
    const P = Object.assign({}, DEFAULTS, params || {});
    k = k || 0;
    const list = [];
    for (const r of restPose(P)) {
      const sw = swimAt(P, r.s, k), pivot = [spineX(P, r.s), 0, 0];
      let rel = sub(r.p, pivot), axes = r.axes;
      if (r.fin) {                                                     // flipper flap about the body axis
        const a = deg(P.flipperFlap) * Math.sin(TAU * k / P.frames) * r.fin.side;
        rel = add(sub(r.fin.root, pivot), rotAxis(sub(r.p, r.fin.root), [1, 0, 0], a));
        axes = axes.map(v => rotAxis(v, [1, 0, 0], a));
      }
      let angle = sw.pitch;
      if (r.fluke) angle += deg(P.flukePitch) * Math.cos(TAU * k / P.frames);
      const tier = r.col[1];
      list.push({
        p: add(add(pivot, [0, sw.dy, 0]), rotZ(rel, angle)),
        h: tier === 2 ? r.h.map(v => Math.max(HMIN, v / SHIELD)) : r.h.slice(),   // an octahedron fits its box's cell
        R: toR(axes.map(v => rotZ(v, angle))),
        dom: r.col[0], tier, part: r.part,
      });
    }
    return list;
  }

  // ---- overlap check ----------------------------------------------------------------------
  // Both shapes are convex, so the separating-axis test is exact given the right axis set: a box
  // contributes its 3 face normals and 3 edge directions, an octahedron its 4 face normals
  // (+-1,+-1,+-1 in its frame) and 3 edge directions (the 6 axis-pair diagonals x+-y, ...
  // span 6 but only the directions matter). Candidate axes: both shapes' face normals, then the
  // cross products of their edge directions.
  function axesOf(q) { const R = q.R; return [[R[0], R[3], R[6]], [R[1], R[4], R[7]], [R[2], R[5], R[8]]]; }
  function shapeOf(q) {
    const A = axesOf(q);
    if (q.tier !== 2) return { A, faces: A, edges: A };
    const H = q.h.map(v => v * SHIELD), faces = [], edges = [];
    for (const [a, b] of [[1, 1], [1, -1], [-1, 1], [-1, -1]])        // face normal ~ (1/H0, a/H1, b/H2)
      faces.push(norm(add(add(mul(A[0], 1 / H[0]), mul(A[1], a / H[1])), mul(A[2], b / H[2]))));
    for (const [i, j] of [[0, 1], [0, 2], [1, 2]]) for (const sg of [1, -1])
      edges.push(norm(sub(mul(A[i], H[i]), mul(A[j], sg * H[j]))));
    return { A, faces, edges };
  }
  function support(q, S, n) {            // half-width of q projected on unit n
    const u = [dot(S.A[0], n), dot(S.A[1], n), dot(S.A[2], n)];
    if (q.tier !== 2) return q.h[0] * Math.abs(u[0]) + q.h[1] * Math.abs(u[1]) + q.h[2] * Math.abs(u[2]);
    return SHIELD * Math.max(q.h[0] * Math.abs(u[0]), q.h[1] * Math.abs(u[1]), q.h[2] * Math.abs(u[2]));
  }
  function depth(a, b) {                 // penetration along the best separating axis (< 0: apart)
    const Sa = shapeOf(a), Sb = shapeOf(b), d = sub(b.p, a.p), ax = [...Sa.faces, ...Sb.faces];
    for (const x of Sa.edges) for (const y of Sb.edges) { const c = cross(x, y); if (Math.hypot(...c) > 1e-6) ax.push(norm(c)); }
    let best = 1e9;
    for (const n of ax) best = Math.min(best, support(a, Sa, n) + support(b, Sb, n) - Math.abs(dot(d, n)));
    return best;
  }
  function reach(q) { return q.tier === 2 ? SHIELD * Math.max(...q.h) : Math.hypot(q.h[0], q.h[1], q.h[2]); }
  // Pairs that interpenetrate by more than `tol` voxels (default 0.05).
  function overlaps(list, tol) {
    tol = tol == null ? 0.05 : tol;
    const cell = 7, grid = new Map(), key = (i, j, k) => i + "," + j + "," + k;
    const cellOf = q => q.p.map(v => Math.floor(v / cell));
    list.forEach((q, i) => { const kk = key(...cellOf(q)); if (!grid.has(kk)) grid.set(kk, []); grid.get(kk).push(i); });
    const pairs = [];
    list.forEach((a, i) => {
      const c = cellOf(a), ra = reach(a), span = Math.ceil((ra + 6) / cell);
      for (let x = -span; x <= span; x++) for (let y = -span; y <= span; y++) for (let z = -span; z <= span; z++) {
        for (const j of grid.get(key(c[0] + x, c[1] + y, c[2] + z)) || []) {
          if (j <= i) continue;
          const b = list[j];
          if (Math.hypot(...sub(a.p, b.p)) >= ra + reach(b)) continue;
          const dpt = depth(a, b);
          if (dpt > tol) pairs.push([i, j, dpt]);
        }
      }
    });
    return pairs;
  }

  function stats(list, params) {
    const P = Object.assign({}, DEFAULTS, params || {});
    const lo = [1e9, 1e9, 1e9], hi = [-1e9, -1e9, -1e9];
    let hmin = 1e9, hmax = 0, aspect = 0, outOfRange = 0;
    const census = {};
    for (const q of list) {
      const r = reach(q);
      for (let k = 0; k < 3; k++) { lo[k] = Math.min(lo[k], q.p[k] - r); hi[k] = Math.max(hi[k], q.p[k] + r); }
      hmin = Math.min(hmin, ...q.h); hmax = Math.max(hmax, ...q.h);
      if (Math.min(...q.h) < HMIN - 1e-6 || Math.max(...q.h) > HMAX + 1e-6) outOfRange++;
      aspect += Math.max(...q.h) / Math.min(...q.h);
      const c = ["Jade", "Ruby", "Gold"][q.dom] + " " + ["plain", "danger", "shield"][q.tier];
      census[c] = (census[c] || 0) + 1;
    }
    return { count: list.length, lo, hi, size: hi.map((v, k) => v - lo[k]), hmin, hmax, outOfRange,
             meanAspect: aspect / Math.max(1, list.length), census };
  }

  // Everything the designer and the exporter need, over all frames. `offset` centres the union of
  // every frame's bounds in the grid; `fits` says the centred whale clears the grid by a voxel.
  function report(params) {
    const P = Object.assign({}, DEFAULTS, params || {});
    const frames = [], hits = [], pairs = [];
    let worst = 0, lo = [1e9, 1e9, 1e9], hi = [-1e9, -1e9, -1e9];
    for (let k = 0; k < P.frames; k++) {
      const f = build(P, k), st = stats(f, P), ov = overlaps(f);
      frames.push(f); hits.push(ov.length); pairs.push(ov);
      for (const o of ov) worst = Math.max(worst, o[2]);
      lo = lo.map((v, i) => Math.min(v, st.lo[i])); hi = hi.map((v, i) => Math.max(v, st.hi[i]));
    }
    const grid = [P.gridX, P.gridY, P.gridZ], size = hi.map((v, i) => v - lo[i]);
    const offset = lo.map((v, i) => -(v + hi[i]) / 2);
    const fits = size.every((v, i) => v <= grid[i] - 2);
    const st = stats(frames[0], P);
    return { params: P, frames, pairs, overlapsPerFrame: hits, worstDepth: worst, lo, hi, size, offset, grid, fits,
             count: st.count, hmin: st.hmin, hmax: st.hmax, meanAspect: st.meanAspect, outOfRange: st.outOfRange, census: st.census };
  }

  const API = { DEFAULTS, build, overlaps, stats, report, depth, restPose, SHIELD, HMIN, HMAX };
  if (typeof module !== "undefined" && module.exports) module.exports = API; else root.WhaleModel = API;
})(typeof window !== "undefined" ? window : globalThis);
