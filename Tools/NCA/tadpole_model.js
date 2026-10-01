// Tadpole-unit targets: creatures assembled from a population of tadpole fauna.
//
// One file, run in the designer page and in node (tadpole_target.py). What the designer shows IS
// the training target.
//
// THE UNIT is the game's tadpole (TadPoleFauna.prefab + the four `Tadpole Fauna <Element>`
// variants): a HEART crystal at the front, a SPINDLE limb out of the heart, and ONE body PRISM
// riding the spindle's end. What each part may do in a learned rule:
//   crystal  FIXED by the unit's element: shape and size (Charge|Mass|Space|Time), never changes
//   spindle  length, bend (arc angle), bend roll (which way it curls), thickness
//   prism    domain (Jade|Ruby|Gold), tier (plain box | danger box | shield octahedron), three
//            half-extents, roll about its long axis - unlike most fauna, free to change
// Measured from the prefab, in game units (the designer's `unitScale` maps them to voxels):
//   heart world size 2.298 (Charge, Space) | 1.737 (Mass, Time)  -> crystal radius = size / 2
//   body prism 1.05 x 1.05 x 3.5 (Charge, Space) | 0.32 x 0.32 x 2.8 (Mass, Time), centred
//   0.7 * 5.81 | 0.4 * 5.81 behind the heart
//
// Frame: +x toward the creature's head (whale) / +y up, units = voxels. A unit's own frame is
// [f, s, n]: f points FORWARD (from its prism to its heart), n is the outward surface normal.
// Output of build(): { units: [...], crystals: [...], prisms: [...], spindles: [...] } where
//   crystal {p, r, elem, unit}     prism {p, h, R (row-major, columns = local axes), dom, tier, unit}
//   spindle {pts: [[x,y,z]...], r, unit}
(function (root) {
  const TAU = Math.PI * 2, SHIELD = 3.0, HMIN = 0.25, HMAX = 3.0;
  const ELEMENTS = ["Charge", "Mass", "Space", "Time"];
  const HEART = [2.298, 1.737, 2.298, 1.737];                       // heart world size per element
  const REST = [                                                    // resting body prism half-extents, and spindle length
    { h: [1.75, 0.525, 0.525], spindle: 1.2 }, { h: [1.4, 0.16, 0.16], spindle: 0.4 },
    { h: [1.75, 0.525, 0.525], spindle: 1.2 }, { h: [1.4, 0.16, 0.16], spindle: 0.4 }];

  // ---- vector helpers ---------------------------------------------------------------------
  const add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
  const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
  const mul = (a, s) => [a[0] * s, a[1] * s, a[2] * s];
  const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const len = a => Math.hypot(a[0], a[1], a[2]);
  const norm = a => { const l = len(a) || 1; return [a[0] / l, a[1] / l, a[2] / l]; };
  const deg = d => d * Math.PI / 180;
  const lerp = (a, b, t) => a + (b - a) * t;
  function frame(f, n) {                  // [f, s, n], right-handed, n forced perpendicular to f
    f = norm(f); n = norm(sub(n, mul(f, dot(n, f))));
    if (len(n) < 1e-6) n = norm(Math.abs(f[1]) < 0.9 ? cross(f, [0, 1, 0]) : cross(f, [1, 0, 0]));
    return [f, cross(n, f), n];
  }
  const toR = ax => [ax[0][0], ax[1][0], ax[2][0], ax[0][1], ax[1][1], ax[2][1], ax[0][2], ax[1][2], ax[2][2]];
  const rotAxis = (v, k, a) => {
    const c = Math.cos(a), s = Math.sin(a), kv = cross(k, v), kd = dot(k, v);
    return [v[0] * c + kv[0] * s + k[0] * kd * (1 - c), v[1] * c + kv[1] * s + k[1] * kd * (1 - c), v[2] * c + kv[2] * s + k[2] * kd * (1 - c)];
  };
  const clampH = v => Math.max(HMIN, Math.min(HMAX, v));

  // ---- the unit ----------------------------------------------------------------------------
  // u = { p (heart centre), f (forward), n (normal), elem, spindle: {len, bend, roll, thick},
  //       prism: {h:[long, wide, thin], dom, tier, roll} }
  const crystalR = (P, e) => 0.5 * HEART[e] * P.unitScale * P.crystalSize;
  function realise(P, u, id) {
    const [f, s, n] = frame(u.f, u.n);
    const r = crystalR(P, u.elem), sp = u.spindle;
    const L = r + sp.len;                                           // the limb roots inside the heart
    const b = norm(add(mul(n, Math.cos(sp.roll)), mul(s, Math.sin(sp.roll))));
    const k = Math.abs(sp.bend) < 1e-6 ? 0 : sp.bend / L, back = mul(f, -1);
    const at = t => k === 0 ? add(u.p, mul(back, t))
      : add(add(u.p, mul(back, Math.sin(k * t) / k)), mul(b, (1 - Math.cos(k * t)) / k));
    const tan = t => norm(add(mul(back, Math.cos(k * t)), mul(b, Math.sin(k * t))));
    const nrm = t => norm(add(mul(f, Math.sin(k * t)), mul(b, Math.cos(k * t))));   // in-plane normal, = b at t = 0
    const pts = []; for (let i = 0; i <= 8; i++) pts.push(at(L * i / 8));
    const T = tan(L), N0 = nrm(L);
    const N = rotAxis(rotAxis(N0, T, -sp.roll), T, u.prism.roll || 0); // undo the curl's roll so the plate lies on the surface
    const h = u.prism.h.map(clampH);
    const pc = add(at(L), mul(T, h[0]));
    const ax = frame(mul(T, -1), N);
    const tier = u.prism.tier;
    return {
      crystal: { p: u.p, r, elem: u.elem, unit: id },
      spindle: { pts, r: sp.thick * P.unitScale, unit: id },
      prism: { p: pc, h: tier === 2 ? h.map(v => Math.max(HMIN, v / SHIELD)) : h, R: toR(ax), dom: u.prism.dom, tier, unit: id },
      tail: add(pc, mul(T, h[0])), tailDir: T,
    };
  }
  function assemble(P, units) {
    const out = { units, crystals: [], prisms: [], spindles: [] };
    units.forEach((u, i) => { const g = realise(P, u, i); out.crystals.push(g.crystal); out.prisms.push(g.prism); out.spindles.push(g.spindle); });
    return out;
  }
  const unit = (p, f, n, elem, sp, pr) => ({ p, f, n, elem, spindle: sp, prism: pr });

  // A chain of units laid head to tail (a tentacle, a fin ray): each heart sits just behind the
  // previous prism's tail, on the previous limb's end tangent - so bends accumulate into curls.
  function chain(P, start, dir, n, count, mk) {
    const out = []; let p = start, d = norm(dir), nn = n;
    for (let i = 0; i < count; i++) {
      const c = mk(i);                                                // {elem, spindle, prism}
      const r = crystalR(P, c.elem);
      const heart = add(p, mul(d, r));
      const u = unit(heart, mul(d, -1), nn, c.elem, c.spindle, c.prism);
      out.push(u);
      const g = realise(P, u, 0);
      p = add(g.tail, mul(g.tailDir, P.gap)); d = g.tailDir;
      nn = norm(sub(nn, mul(d, dot(nn, d))));
    }
    return out;
  }

  // Ring tiling of a surface: rings along a parameter s (0 = the head/apex), units around each
  // ring, each unit pointing forward toward the head along the surface. `surf(s, th)` -> {p, n};
  // each ring's prisms reach back to just before the next ring's hearts.
  function tile(P, surf, sList, perRing, look, rmaxOf) {
    const out = [], ht = P.plateThickness / 2;
    const rings = sList.map((s, i) => {
      const ths = evenAngles(th => surf(s, th).p, perRing[i], P.stagger ? 0.5 * (i % 2) : 0);
      return { ths, hearts: ths.map(th => { const a = surf(s, th); return { a, h: sub(a.p, mul(a.n, ht)) }; }) };
    });
    for (let i = 0; i < sList.length; i++) {
      const s = sList[i], n = perRing[i];
      const next = i + 1 < sList.length ? sList[i + 1] : null;
      const { ths, hearts } = rings[i];
      for (let j = 0; j < n; j++) {
        const th = ths[j], a = hearts[j].a, look_ = look(s, th, j, i);
        const ahead = surf(Math.max(0, s - 1e-3), th);
        const f = norm(sub(ahead.p, a.p));
        const heart = hearts[j].h;
        const r = crystalR(P, look_.elem);
        let reach;                                                   // distance to the next ring's hearts
        if (next !== null) reach = len(sub(surf(next, th).p, a.p)) - rmaxOf(i + 1) - P.gap - P.ringSlack;   // slack: room to bend while swimming
        else reach = look_.tailReach;
        // around the ring: half the chord to the nearer neighbour, less what the inner corners
        // lose where the surface turns between them (ht * tan(angle / 2)), less half the gap
        let wide = 1e9;
        // measured at the heart AND where the plate ends (the next ring), since a tapering body
        // draws neighbouring plates together along their length
        const ends = [s, Math.min(1, next !== null ? s + 0.8 * (next - s) : i > 0 ? s + 0.8 * (s - sList[i - 1]) : s)];
        for (const k of [(j + 1) % n, (j + n - 1) % n]) for (const se of ends) {
          const A = surf(se, th), B = surf(se, ths[k]);
          const chord = len(sub(sub(B.p, mul(B.n, ht)), sub(A.p, mul(A.n, ht))));
          const turn = Math.acos(Math.max(-1, Math.min(1, dot(A.n, B.n))));
          wide = Math.min(wide, 0.5 * P.plateWidth * chord - 1.5 * ht * Math.tan(Math.min(turn, 2.8) / 2) - P.gap / 2);
        }
        // back along the body: stop short of any next-ring heart in the prism's lane
        if (next !== null) for (const o of rings[i + 1].hearts) {
          const d = sub(o.h, heart), along = -dot(d, f), lat = len(add(d, mul(f, along)));
          if (along > 0 && lat < wide + rmaxOf(i + 1) + P.gap) reach = Math.min(reach, along - rmaxOf(i + 1) - P.gap - P.ringSlack);
        }
        const lenSp = Math.max(0, Math.min(look_.spindle, reach - r - 2 * HMIN));
        const hl = 0.5 * (reach - r - lenSp);
        out.push(unit(heart, f, a.n, look_.elem, { len: lenSp, bend: 0, roll: 0, thick: P.spindleThickness },
          { h: [hl, wide, ht], dom: look_.col[0], tier: look_.col[1], roll: 0 }));
      }
    }
    return out;
  }
  // n angles around a closed curve, evenly spaced in ARC LENGTH (so units sit evenly on the skin)
  function evenAngles(curve, n, phase) {
    const M = 360, cum = [0]; let prev = curve(0);
    for (let i = 1; i <= M; i++) { const q = curve(TAU * i / M); cum.push(cum[i - 1] + len(sub(q, prev))); prev = q; }
    const total = cum[M], out = [];
    for (let j = 0; j < n; j++) {
      const target = (total * (j + phase) / n) % total;
      let i = 1; while (i < M && cum[i] < target) i++;
      out.push(TAU * (i - 1 + (target - cum[i - 1]) / Math.max(cum[i] - cum[i - 1], 1e-9)) / M);
    }
    return out;
  }

  function perimeter(a, b) { return Math.PI * (3 * (a + b) - Math.sqrt((3 * a + b) * (a + 3 * b))); }

  // ================================= WHALE =================================================
  const WHALE = {
    gridX: 96, gridY: 40, gridZ: 64, unitScale: 1, crystalSize: 1, spindleThickness: 0.22, gap: 0.2,
    length: 64, girth: 0.12, girthPeak: 0.3, headRound: 0.55, tailTaper: 1.6, peduncle: 0.12, flatten: 0.8, bellyDrop: 0.18,
    ringPitch: 6.5, ringSlack: 0.4, arcSpacing: 3.0, plateWidth: 0.92, plateThickness: 0.9, stagger: 0, spindle: 1.0,
    flipperRays: 3, flipperUnits: 3, flipperPos: 0.3, flipperSweep: 35, flipperDroop: 25, flipperSpread: 12, flipperCurl: 0,
    keel: 1, dorsal: 1, dorsalPos: 0.66, dorsalUnits: 1, dorsalRake: 40,
    flukeRays: 3, flukeUnits: 2, flukeSweep: 30, flukeSpread: 18,
    backElem: 2, bellyElem: 0, finElem: 1, flukeElem: 3, eyeElem: 3, elemMix: 0,
    back: [0, 0], belly: [2, 0], countershade: -0.15, pleats: 1, pleatColor: [2, 2], pleatEnd: 0.45,
    fins: [0, 0], flukeColor: [0, 0], dorsalColor: [0, 0], eye: 1, eyeColor: [1, 2], eyePos: 0.14,
    frames: 8, swimAmp: 0.05, swimWave: 1.1, swimEnv: 2.2, flukePitch: 22, flipperFlap: 12, spindleSway: 0,
  };
  function whaleRadius(P, s) {
    const rmax = P.girth * P.length;
    if (s <= P.girthPeak) { const u = s / P.girthPeak; return rmax * Math.pow(Math.max(0, u * (2 - u)), P.headRound); }
    const t = (s - P.girthPeak) / (1 - P.girthPeak);
    return rmax * (1 - (1 - P.peduncle) * Math.pow(t, P.tailTaper));
  }
  function whaleRest(P) {
    const L = P.length, xOf = s => L / 2 - s * L;
    const surf = (s, th) => {
      const w = Math.max(whaleRadius(P, s), 1e-3), h = w * P.flatten, yc = -P.bellyDrop * w;
      const y = yc + h * Math.sin(th), z = w * Math.cos(th);
      return { p: [xOf(s), y, z], n: norm([0, (y - yc) / (h * h), z / (w * w)]), w, h };
    };
    const elemFor = (base, i, j) => P.elemMix ? (base + i + j) % 4 : base;
    const rmax = Math.max(...[0, 1, 2, 3].map(e => crystalR(P, e)));
    // rings: first where the section can hold three hearts, pitch along the body
    const sList = [], per = [], looks = [];
    for (let x = L / 2 - rmax - 0.5; x > -L / 2; x -= P.ringPitch) {
      const s = (L / 2 - x) / L, w = whaleRadius(P, s), h = w * P.flatten;
      const ht = P.plateThickness / 2, inner = Math.max(0, w - 2 * ht), innerH = Math.max(0, h - 2 * ht);
      const n = Math.floor(perimeter(w - ht, h - ht) / Math.max(P.arcSpacing, 2 * rmax + P.gap));
      if (n < 3 || h < 2 * ht + 2 * rmax * 0.5) { if (sList.length) break; else continue; }
      sList.push(s); per.push(n); looks.push(perimeter(inner, innerH));
    }
    const tagged = [];
    const bodyUnits = tile(P, surf, sList, per, (s, th, j, i) => {
      const low = Math.sin(th) < P.countershade;
      let col = low ? P.belly : P.back;
      if (low && P.pleats && s < P.pleatEnd && j % 2 === 0) col = P.pleatColor;
      return { elem: elemFor(low ? P.bellyElem : P.backElem, i, j), spindle: P.spindle, col, arc: looks[i],
               tailReach: Math.max(2, P.ringPitch * 0.9) };
    }, () => rmax);
    bodyUnits.forEach((u, k) => tagged.push({ u, s: (L / 2 - u.p[0]) / L, part: "body" }));
    // eyes: the body unit nearest each eye point becomes an eye unit (its heart the eye)
    if (P.eye) for (const side of [1, -1]) {
      const e = surf(P.eyePos, side > 0 ? 0.15 : Math.PI - 0.15).p;
      let best = null, bd = 1e9;
      for (const t of tagged) { const d = len(sub(t.u.p, e)); if (d < bd) { bd = d; best = t; } }
      if (best) { best.u.elem = P.eyeElem; best.u.prism.dom = P.eyeColor[0]; best.u.prism.tier = P.eyeColor[1]; best.part = "eye"; }
    }
    const tailS = sList.length ? sList[sList.length - 1] : 1;
    // flippers: rays fanned in the fin plane, each ray a chain of units pointing back at the body
    for (const side of [1, -1]) {
      const s0 = P.flipperPos, a = surf(s0, side > 0 ? -0.35 : Math.PI + 0.35);
      let span = [0, 0, side];
      span = rotAxis(span, [0, 1, 0], -side * deg(P.flipperSweep));
      span = norm(rotAxis(span, [1, 0, 0], side * deg(P.flipperDroop)));
      const chord = norm(sub([-1, 0, 0], mul(span, dot([-1, 0, 0], span))));
      const fn = norm(cross(span, chord));
      for (let r = 0; r < P.flipperRays; r++) {
        const v = P.flipperRays > 1 ? r / (P.flipperRays - 1) - 0.5 : 0;
        const dir = rotAxis(span, fn, deg(P.flipperSpread) * v * 2);        // + toward the trailing edge: rays fan apart
        const start = add(add(a.p, mul(a.n, 2 * rmax + P.gap)), mul(chord, v * (P.flipperRays - 1) * (2 * Math.max(rmax, 0.9) + P.gap)));
        chain(P, start, dir, fn, P.flipperUnits, i => ({
          elem: elemFor(P.finElem, r, i),
          spindle: { len: P.spindle, bend: side * deg(P.flipperCurl), roll: Math.PI / 2, thick: P.spindleThickness },
          prism: { h: [2.0 - 0.35 * i, 0.9, 0.45], dom: P.fins[0], tier: P.fins[1], roll: 0 } }))
          .forEach(u => tagged.push({ u, s: s0, part: "flipper", fin: { side, root: a.p } }));
      }
    }
    // dorsal: a short upright chain
    if (P.dorsal) {
      const top = surf(P.dorsalPos, Math.PI / 2);
      const dir = norm([-Math.sin(deg(P.dorsalRake)), Math.cos(deg(P.dorsalRake)), 0]);
      chain(P, add(top.p, [0, rmax - P.plateThickness / 2 + P.gap, 0]), dir, [0, 0, 1], P.dorsalUnits, i => ({
        elem: P.finElem, spindle: { len: P.spindle * 0.6, bend: 0, roll: 0, thick: P.spindleThickness },
        prism: { h: [1.4, 0.4, 1.1], dom: P.dorsalColor[0], tier: P.dorsalColor[1], roll: 0 } }))
        .forEach(u => tagged.push({ u, s: P.dorsalPos, part: "dorsal" }));
    }
    // tail stock: where the body is too thin for a ring, a single file of tadpoles down the spine
    let x0 = xOf(tailS) - Math.max(2, P.ringPitch * 0.9) - P.ringSlack - P.gap, y0 = -P.bellyDrop * whaleRadius(P, tailS);
    if (P.keel) {
      const room = x0 - (-L / 2), pitch = 2 * rmax + P.spindle * 0.6 + 2 * 1.4 + P.gap;
      const count = Math.max(0, Math.floor(room / pitch));
      if (count) {
        const keel = chain(P, [x0, y0, 0], [-1, 0, 0], [0, 1, 0], count, i => {
          const s = Math.min(1, (L / 2 - (x0 - (i + 0.5) * pitch)) / L), w = whaleRadius(P, s);
          return { elem: P.backElem, spindle: { len: P.spindle * 0.6, bend: 0, roll: 0, thick: P.spindleThickness },
                   prism: { h: [1.4, Math.max(0.5, w * 0.9), Math.max(0.5, w * P.flatten * 0.9)], dom: P.back[0], tier: P.back[1], roll: 0 } };
        });
        keel.forEach(u => tagged.push({ u, s: Math.min(1, (L / 2 - u.p[0]) / L), part: "body" }));
        const end = realise(P, keel[keel.length - 1], 0);
        x0 = end.tail[0] - P.gap - P.ringSlack;
      }
    }
    // flukes: rays fanned back and out from the tail stock, in the horizontal plane
    {
      const tail = surf(tailS, Math.PI / 2);
      for (const side of [1, -1]) for (let r = 0; r < P.flukeRays; r++) {
        const v = P.flukeRays > 1 ? r / (P.flukeRays - 1) : 0.5;
        const ang = deg(P.flukeSweep + P.flukeSpread * (v - 0.5) * 2);
        const dir = norm([-Math.sin(ang), 0, side * Math.cos(ang)]);
        chain(P, [x0 - r * (2 * Math.max(rmax, 0.9) + P.gap), y0, side * (rmax + P.gap)], dir, [0, 1, 0], P.flukeUnits, i => ({
          elem: elemFor(P.flukeElem, r, i), spindle: { len: P.spindle * 0.8, bend: 0, roll: 0, thick: P.spindleThickness },
          prism: { h: [1.8 - 0.3 * i, 0.9, 0.4], dom: P.flukeColor[0], tier: P.flukeColor[1], roll: 0 } }))
          .forEach(u => tagged.push({ u, s: 1, part: "fluke" }));
      }
      void tail;
    }
    return tagged;
  }
  function whaleAt(P, k) {
    const heave = s => P.swimAmp * P.length * Math.pow(Math.max(0, s), P.swimEnv) * Math.sin(TAU * (s / P.swimWave - k / P.frames));
    const units = [];
    for (const t of whaleRest(P)) {
      const u = JSON.parse(JSON.stringify(t.u));
      const s = t.s, ds = 1e-3, dy = heave(s);
      let pitch = Math.atan2(dy - heave(s + ds), ds * P.length);
      if (t.part === "fluke") pitch += deg(P.flukePitch) * Math.cos(TAU * k / P.frames);
      const pivot = [P.length / 2 - s * P.length, 0, 0];
      let rel = sub(u.p, pivot), f = u.f, n = u.n;
      if (t.fin) {                                                     // flipper flap about the body axis at the root
        const a = deg(P.flipperFlap) * Math.sin(TAU * k / P.frames) * t.fin.side;
        rel = add(sub(t.fin.root, pivot), rotAxis(sub(u.p, t.fin.root), [1, 0, 0], a));
        f = rotAxis(f, [1, 0, 0], a); n = rotAxis(n, [1, 0, 0], a);
      }
      const z = [0, 0, 1];
      u.p = add(add(pivot, [0, dy, 0]), rotAxis(rel, z, pitch));
      u.f = rotAxis(f, z, pitch); u.n = rotAxis(n, z, pitch);
      if (P.spindleSway) u.spindle.bend += deg(P.spindleSway) * Math.sin(TAU * (s * 2 - k / P.frames));
      u.part = t.part;
      units.push(u);
    }
    return units;
  }

  // ================================= JELLYFISH =============================================
  const JELLY = {
    gridX: 64, gridY: 64, gridZ: 64, unitScale: 1, crystalSize: 1, spindleThickness: 0.22, gap: 0.2,
    bellRadius: 13, bellHeight: 9.5, bellSweep: 105, ringPitch: 5.5, ringSlack: 0.2, arcSpacing: 3.0, plateWidth: 0.9, plateThickness: 0.9,
    stagger: 1, spindle: 0.8, apexUnit: 1,
    tentacles: 8, tentacleUnits: 3, tentacleCurl: 18, tentacleInset: 0.92,
    arms: 4, armUnits: 2, armCurl: 10, armRadius: 3.5,
    bellElem: 2, rimElem: 0, tentacleElem: 1, armElem: 3, elemMix: 0,
    bell: [1, 2], rim: [1, 1], tentacleColor: [2, 0], armColor: [0, 2], rimRows: 1,
    frames: 8, pulse: 0.16, pulseLift: 2.5, tentacleWave: 22, tentacleLag: 0.6,
  };
  function jellyUnits(P, k) {
    const ph = TAU * (k || 0) / P.frames, c = 0.5 - 0.5 * Math.cos(ph);   // 0 relaxed .. 1 contracted
    const lift = P.pulseLift * Math.sin(ph);
    const sweep = deg(P.bellSweep), rmax = Math.max(...[0, 1, 2, 3].map(e => crystalR(P, e)));
    const elemFor = (base, i, j) => P.elemMix ? (base + i + j) % 4 : base;
    const bellAt = (con) => (s, th) => {                              // s: 0 apex .. 1 rim
      const phi = sweep * s, squeeze = 1 - P.pulse * con * s * s;
      const R = P.bellRadius * squeeze, H = P.bellHeight;
      const rho = R * Math.sin(phi) / Math.sin(Math.min(sweep, Math.PI / 2)), y = H * Math.cos(phi) + lift;
      const p = [rho * Math.cos(th), y, rho * Math.sin(th)];
      const nr = [Math.cos(th) * Math.sin(phi) * H, Math.cos(phi) * R / Math.sin(Math.min(sweep, Math.PI / 2)), Math.sin(th) * Math.sin(phi) * H];
      return { p, n: norm(nr), rho };
    };
    // ring layout from the CONTRACTED bell, so no frame can squeeze units into each other
    const tight = bellAt(1), sList = [], per = [], circs = [];
    const arcLen = s => { let a = 0, prev = tight(0, 0).p; for (let i = 1; i <= 40; i++) { const q = tight(s * i / 40, 0).p; a += len(sub(q, prev)); prev = q; } return a; };
    const total = arcLen(1);
    for (let d = rmax + 1.5; d < total; d += P.ringPitch) {
      let lo = 0, hi = 1; for (let it = 0; it < 30; it++) { const m = (lo + hi) / 2; if (arcLen(m) < d) lo = m; else hi = m; }
      const s = (lo + hi) / 2, circ = TAU * tight(s, 0).rho, n = Math.floor(circ / Math.max(P.arcSpacing, 2 * rmax + P.gap));
      if (n < 3) continue;
      sList.push(s); per.push(n); circs.push(circ);
    }
    const surf = bellAt(c);
    const units = tile(P, surf, sList, per, (s, th, j, i) => {
      const rimRow = i >= sList.length - P.rimRows;
      return { elem: elemFor(rimRow ? P.rimElem : P.bellElem, i, j), spindle: P.spindle, col: rimRow ? P.rim : P.bell,
               arc: TAU * surf(s, 0).rho, tailReach: P.ringPitch * 0.85 };
    }, () => rmax).map(u => Object.assign(u, { part: "bell" }));
    // apex: one unit pointing up, its prism hanging inside the bell (the stomach)
    if (P.apexUnit) units.push(Object.assign(unit(add(surf(0, 0).p, [0, -rmax - 0.6, 0]), [0, 1, 0], [1, 0, 0], P.armElem,
      { len: P.spindle, bend: 0, roll: 0, thick: P.spindleThickness }, { h: [2.2, 1.2, 1.2], dom: P.armColor[0], tier: P.armColor[1], roll: 0 }), { part: "apex" }));
    // tentacles: chains hanging from inside the rim, curling, with a wave travelling down them
    const rimS = sList.length ? sList[sList.length - 1] : 1;
    for (let t = 0; t < P.tentacles; t++) {
      const th = TAU * (t + 0.5) / P.tentacles, rp = surf(Math.min(1, rimS + 0.04), th);
      const start = [rp.p[0] * P.tentacleInset, rp.p[1] - P.ringPitch * 0.85 - rmax - 0.5, rp.p[2] * P.tentacleInset];
      const radial = norm([Math.cos(th), 0, Math.sin(th)]);
      chain(P, start, [0, -1, 0], radial, P.tentacleUnits, i => ({
        elem: elemFor(P.tentacleElem, t, i),
        spindle: { len: P.spindle, bend: deg(P.tentacleCurl + P.tentacleWave * Math.sin(ph - i * P.tentacleLag)), roll: 0, thick: P.spindleThickness },
        prism: { h: [2.2 - 0.25 * i, 0.35, 0.35], dom: P.tentacleColor[0], tier: P.tentacleColor[1], roll: 0 } }))
        .forEach(u => units.push(Object.assign(u, { part: "tentacle" })));
    }
    // oral arms: shorter, heavier chains from the centre under the bell
    for (let a = 0; a < P.arms; a++) {
      const th = TAU * a / Math.max(1, P.arms), radial = norm([Math.cos(th), 0, Math.sin(th)]);
      const start = add([0, surf(0, 0).p[1] - P.bellHeight * 0.55, 0], mul(radial, P.armRadius));
      chain(P, start, [0, -1, 0], radial, P.armUnits, i => ({
        elem: elemFor(P.armElem, a, i),
        spindle: { len: P.spindle * 0.7, bend: deg(P.armCurl * Math.sin(ph * 0.5 - i * 0.8 + a)), roll: Math.PI / 2, thick: P.spindleThickness },
        prism: { h: [1.8, 1.0, 0.5], dom: P.armColor[0], tier: P.armColor[1], roll: 0 } }))
        .forEach(u => units.push(Object.assign(u, { part: "arm" })));
    }
    return units;
  }

  // ================================= overlap check =========================================
  // Solids: prisms (box / shield octahedron, exact separating-axis test) and crystals (spheres,
  // exact distance). Spindles are limbs and are exempt, as in the game (a limb may pass a plate).
  function axesOf(q) { const R = q.R; return [[R[0], R[3], R[6]], [R[1], R[4], R[7]], [R[2], R[5], R[8]]]; }
  function shapeOf(q) {
    const A = axesOf(q);
    if (q.tier !== 2) return { A, faces: A, edges: A };
    const H = q.h.map(v => v * SHIELD), faces = [], edges = [];
    for (const [a, b] of [[1, 1], [1, -1], [-1, 1], [-1, -1]]) faces.push(norm(add(add(mul(A[0], 1 / H[0]), mul(A[1], a / H[1])), mul(A[2], b / H[2]))));
    for (const [i, j] of [[0, 1], [0, 2], [1, 2]]) for (const sg of [1, -1]) edges.push(norm(sub(mul(A[i], H[i]), mul(A[j], sg * H[j]))));
    return { A, faces, edges };
  }
  function support(q, S, n) {
    const u = [dot(S.A[0], n), dot(S.A[1], n), dot(S.A[2], n)];
    if (q.tier !== 2) return q.h[0] * Math.abs(u[0]) + q.h[1] * Math.abs(u[1]) + q.h[2] * Math.abs(u[2]);
    return SHIELD * Math.max(q.h[0] * Math.abs(u[0]), q.h[1] * Math.abs(u[1]), q.h[2] * Math.abs(u[2]));
  }
  function prismPrism(a, b) {
    const Sa = shapeOf(a), Sb = shapeOf(b), d = sub(b.p, a.p), ax = [...Sa.faces, ...Sb.faces];
    for (const x of Sa.edges) for (const y of Sb.edges) { const c = cross(x, y); if (len(c) > 1e-6) ax.push(norm(c)); }
    let best = 1e9;
    for (const n of ax) best = Math.min(best, support(a, Sa, n) + support(b, Sb, n) - Math.abs(dot(d, n)));
    return best;
  }
  function octaVerts(q) { const A = axesOf(q), out = []; for (let i = 0; i < 3; i++) for (const s of [1, -1]) out.push(add(q.p, mul(A[i], s * SHIELD * q.h[i]))); return out; }
  function closestOnSeg(p, a, b) { const ab = sub(b, a), t = Math.max(0, Math.min(1, dot(sub(p, a), ab) / Math.max(dot(ab, ab), 1e-12))); return add(a, mul(ab, t)); }
  function spherePrism(c, q) {                                     // penetration depth (< 0: apart)
    const A = axesOf(q), d = sub(c.p, q.p), u = [dot(d, A[0]), dot(d, A[1]), dot(d, A[2])];
    if (q.tier !== 2) {
      const cl = u.map((v, i) => Math.max(-q.h[i], Math.min(q.h[i], v)));
      const out = Math.hypot(u[0] - cl[0], u[1] - cl[1], u[2] - cl[2]);
      if (out > 0) return c.r - out;
      return c.r + Math.min(...u.map((v, i) => q.h[i] - Math.abs(v)));
    }
    const S = shapeOf(q), axes = [...S.faces];
    const V = octaVerts(q);
    for (const v of V) { const w = sub(c.p, v); if (len(w) > 1e-9) axes.push(norm(w)); }
    for (let i = 0; i < 6; i++) for (let j = i + 1; j < 6; j++) {
      if ((i >> 1) === (j >> 1)) continue;                            // opposite vertices: not an edge
      const w = sub(c.p, closestOnSeg(c.p, V[i], V[j])); if (len(w) > 1e-9) axes.push(norm(w));
    }
    let best = 1e9;
    for (const n of axes) best = Math.min(best, c.r + support(q, S, n) - Math.abs(dot(d, n)));
    return best;
  }
  function overlaps(g, tol) {
    tol = tol == null ? 0.05 : tol;
    const items = [];
    g.prisms.forEach(q => items.push({ kind: 0, q, reach: q.tier === 2 ? SHIELD * Math.max(...q.h) : len(q.h) }));
    g.crystals.forEach(q => items.push({ kind: 1, q, reach: q.r }));
    const cell = 8, grid = new Map(), key = (a, b, c) => a + "," + b + "," + c, cellOf = p => p.map(v => Math.floor(v / cell));
    items.forEach((it, i) => { const k = key(...cellOf(it.q.p)); if (!grid.has(k)) grid.set(k, []); grid.get(k).push(i); });
    const pairs = [];
    items.forEach((a, i) => {
      const c = cellOf(a.q.p), span = Math.ceil((a.reach + 6) / cell);
      for (let x = -span; x <= span; x++) for (let y = -span; y <= span; y++) for (let z = -span; z <= span; z++)
        for (const j of grid.get(key(c[0] + x, c[1] + y, c[2] + z)) || []) {
          if (j <= i) continue;
          const b = items[j];
          if (a.q.unit === b.q.unit) continue;                        // a unit's own heart and prism are one body
          if (len(sub(a.q.p, b.q.p)) >= a.reach + b.reach) continue;
          let dpt;
          if (a.kind === 0 && b.kind === 0) dpt = prismPrism(a.q, b.q);
          else if (a.kind === 1 && b.kind === 1) dpt = a.q.r + b.q.r - len(sub(a.q.p, b.q.p));
          else dpt = a.kind === 1 ? spherePrism(a.q, b.q) : spherePrism(b.q, a.q);
          if (dpt > tol) pairs.push({ a: i, b: j, depth: dpt, kinds: a.kind + b.kind, units: [a.q.unit, b.q.unit] });
        }
    });
    return pairs;
  }

  // ================================= report ================================================
  const TARGETS = { whale: { defaults: WHALE, at: whaleAt }, jelly: { defaults: JELLY, at: jellyUnits } };
  function build(kind, params, k) { const T = TARGETS[kind], P = Object.assign({}, T.defaults, params || {}); return assemble(P, T.at(P, k || 0)); }
  function reachOf(g) {
    const lo = [1e9, 1e9, 1e9], hi = [-1e9, -1e9, -1e9];
    const grow = (p, r) => { for (let i = 0; i < 3; i++) { lo[i] = Math.min(lo[i], p[i] - r); hi[i] = Math.max(hi[i], p[i] + r); } };
    g.prisms.forEach(q => grow(q.p, q.tier === 2 ? SHIELD * Math.max(...q.h) : len(q.h)));
    g.crystals.forEach(q => grow(q.p, q.r));
    return { lo, hi };
  }
  function report(kind, params) {
    const T = TARGETS[kind], P = Object.assign({}, T.defaults, params || {});
    const frames = [], pairs = [];
    let lo = [1e9, 1e9, 1e9], hi = [-1e9, -1e9, -1e9], worst = 0;
    for (let k = 0; k < P.frames; k++) {
      const g = assemble(P, T.at(P, k)), ov = overlaps(g), b = reachOf(g);
      frames.push(g); pairs.push(ov);
      ov.forEach(o => { worst = Math.max(worst, o.depth); });
      lo = lo.map((v, i) => Math.min(v, b.lo[i])); hi = hi.map((v, i) => Math.max(v, b.hi[i]));
    }
    const grid = [P.gridX, P.gridY, P.gridZ], size = hi.map((v, i) => v - lo[i]);
    const g0 = frames[0], elems = [0, 0, 0, 0], census = {};
    g0.crystals.forEach(c => elems[c.elem]++);
    let hmin = 1e9, hmax = 0, aspect = 0;
    g0.prisms.forEach(q => {
      const key = ["Jade", "Ruby", "Gold"][q.dom] + " " + ["plain", "danger", "shield"][q.tier];
      census[key] = (census[key] || 0) + 1;
      hmin = Math.min(hmin, ...q.h); hmax = Math.max(hmax, ...q.h); aspect += Math.max(...q.h) / Math.min(...q.h);
    });
    const parts = {}; g0.units.forEach(u => { parts[u.part] = (parts[u.part] || 0) + 1; });
    return { kind, params: P, frames, pairs, overlapsPerFrame: pairs.map(p => p.length), worstDepth: worst,
             lo, hi, size, grid, offset: lo.map((v, i) => -(v + hi[i]) / 2), fits: size.every((v, i) => v <= grid[i] - 2),
             units: g0.units.length, elements: elems, census, parts, hmin, hmax, meanAspect: aspect / Math.max(1, g0.prisms.length) };
  }

  const API = { ELEMENTS, HEART, REST, SHIELD, HMIN, HMAX, WHALE, JELLY, build, report, overlaps, realise, crystalR };
  if (typeof module !== "undefined" && module.exports) module.exports = API; else root.TadpoleModel = API;
})(typeof window !== "undefined" ? window : globalThis);
