// Four body plans for one swarm of tadpole fauna - one per element - as targets for a single
// learned rule set that, seeded with a different majority element, converges to that element's
// body plan.
//
// One file, run in the designer page and in node (swarm_target.py). What the designer shows IS
// the training target.
//
// THE UNIT is the game's tadpole (TadPoleFauna.prefab + the four `Tadpole Fauna <Element>`
// variants): a HEART crystal at the front, a SPINDLE limb out of the heart, ONE body PRISM on the
// limb's end.
//   crystal  fixed by the element (shape and size). While the tadpole lives every heart is the
//            same blue-and-white; only a DEAD tadpole leaves a lime crystal behind
//   spindle  length, bend, bend roll, thickness - free
//   prism    size and roll free WITHIN the element's identity (below); tier changes are Charge's
//            alone; a tadpole's domain never changes and its offspring inherit it
// Measured from the prefab, in game units (`unitScale` maps them to voxels): heart world size
// 2.298 (Charge, Space) | 1.737 (Mass, Time).
//
// Body plans: MASS the whale (chunky skin), SPACE the jellyfish (an umbrella of rods), CHARGE the
// pufferfish (plates that shield, then swing out into danger spines), TIME the dragonfly
// (beating wings outlined by runners). In every plan the minority elements keep their identity:
// Space makes the rods, Mass the bulk, Charge the armour and anything that changes state, and Time
// the runners that zip round the structure.
//
// Frame: +x the creature's head, +y up, units = voxels.
(function (root) {
  const TAU = Math.PI * 2, SHIELD = 3.0, HMIN = 0.25, HMAX = 3.0;
  const ELEMENTS = ["Charge", "Mass", "Space", "Time"];
  const HEART = [2.298, 1.737, 2.298, 1.737];
  const perimeter = (a, b) => Math.PI * (3 * (a + b) - Math.sqrt((3 * a + b) * (a + 3 * b)));

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


  // ---- element identity: every unit obeys its element, whatever a target asks for ----------
  // Prism half-extents are [long (along the limb), wide, thin]. A request that breaks the identity
  // is SHRUNK into it where it can be (shrinking never makes a new overlap) and grown only where
  // it must be (a Space rod too short to be a rod).
  //   Charge  the only element that changes STATE (plain | danger | shield); any shape 0.3-2.2
  //   Mass    chunky: every axis 0.5-1.8, longest at most 1.6x the shortest
  //   Space   a rod: long 0.9-3.0, cross 0.25-0.42 and at most a quarter of the length
  //   Time    small and streamlined: long 0.45-1.2, cross 0.3-0.6 and at most long / 1.5;
  //           Time units are the fast ones - in every body plan they are the runners
  const C = (v, a, b) => Math.max(a, Math.min(b, v));
  function identity(elem, h, tier) {
    let o;
    if (elem === 0) o = h.map(v => C(v, 0.3, 2.2));
    else if (elem === 1) { o = h.map(v => C(v, 0.5, 1.8)); const m = Math.min(...o); o = o.map(v => Math.min(v, m * 1.6)); }
    else if (elem === 2) { const L = C(h[0], 0.9, 3.0), c = x => C(Math.min(x, L / 4), 0.25, 0.42); o = [L, c(h[1]), c(h[2])]; }
    else { const L = C(h[0], 0.45, 1.2), c = x => C(Math.min(x, L / 1.5), 0.3, 0.6); o = [L, c(h[1]), c(h[2])]; }
    return { h: o, tier: elem === 0 ? tier : 0 };
  }

  // ---- the unit ----------------------------------------------------------------------------
  // u = { p (heart centre), f (forward), n (normal), elem, spindle: {len, bend, roll, thick},
  //       prism: {h:[long, wide, thin], tier, roll}, role }
  // DOMAINS ARE SLOTS. A swarm may hold several domains (each lineage breeding true), so a target
  // paints its regions with abstract slots A | B | C (prism.slot 0 | 1 | 2), and P.slotMap says
  // which real domain each slot shows as in a preview. The loss is meant to be DOMAIN-NEUTRAL:
  // the minimum over every slot -> domain assignment (distinct slots, distinct domains), so it
  // rewards forming regions of different domains, never one particular domain.
  const crystalR = (P, e) => 0.5 * HEART[e] * P.unitScale * P.crystalSize;
  const unitLength = (P, e, sp, hl) => 2 * crystalR(P, e) + sp + 2 * hl + P.gap;
  function realise(P, u, id) {
    const [f, s, n] = frame(u.f, u.n);
    const r = crystalR(P, u.elem), sp = u.spindle;
    const L = r + sp.len;                                           // the limb roots inside the heart
    const b = norm(add(mul(n, Math.cos(sp.roll)), mul(s, Math.sin(sp.roll))));
    const k = Math.abs(sp.bend) < 1e-6 ? 0 : sp.bend / L, back = mul(f, -1);
    const at = t => k === 0 ? add(u.p, mul(back, t))
      : add(add(u.p, mul(back, Math.sin(k * t) / k)), mul(b, (1 - Math.cos(k * t)) / k));
    const tan = t => norm(add(mul(back, Math.cos(k * t)), mul(b, Math.sin(k * t))));
    const nrm = t => norm(add(mul(f, Math.sin(k * t)), mul(b, Math.cos(k * t))));
    const pts = []; for (let i = 0; i <= 8; i++) pts.push(at(L * i / 8));
    const T = tan(L), N = rotAxis(rotAxis(nrm(L), T, -sp.roll), T, u.prism.roll || 0);
    const id_ = identity(u.elem, u.prism.h.map(clampH), u.prism.tier || 0), h = id_.h, tier = id_.tier;
    const pc = add(at(L), mul(T, h[0]));
    return {
      crystal: { p: u.p, r, elem: u.elem, unit: id },
      spindle: { pts, r: sp.thick * P.unitScale, unit: id },
      prism: { p: pc, h: tier === 2 ? h.map(v => Math.max(HMIN, v / SHIELD)) : h, R: toR(frame(mul(T, -1), N)), dom: (P.slotMap || [0, 1, 2])[u.prism.slot || 0], slot: u.prism.slot || 0, tier, unit: id },
      tail: add(pc, mul(T, h[0])), tailDir: T,
    };
  }
  function assemble(P, units) {
    const out = { units, crystals: [], prisms: [], spindles: [] };
    units.forEach((u, i) => { const g = realise(P, u, i); out.crystals.push(g.crystal); out.prisms.push(g.prism); out.spindles.push(g.spindle); });
    return out;
  }
  const unit = (p, f, n, elem, sp, pr, role) => ({ p, f, n, elem, spindle: sp, prism: pr, role });
  const limb = (P, len, bend, roll) => ({ len, bend: bend || 0, roll: roll || 0, thick: P.spindleThickness });

  // A chain of units laid head to tail: each heart just behind the previous prism's tail, on the
  // previous limb's end tangent, so bends accumulate into curls (tentacles, rays, an abdomen).
  function chain(P, start, dir, n, count, mk, role) {
    const out = []; let p = start, d = norm(dir), nn = n;
    for (let i = 0; i < count; i++) {
      const c = mk(i), r = crystalR(P, c.elem);
      const u = unit(add(p, mul(d, r)), mul(d, -1), nn, c.elem, c.spindle, c.prism, role);
      out.push(u);
      const g = realise(P, u, 0);
      p = add(g.tail, mul(g.tailDir, P.gap)); d = g.tailDir;
      nn = norm(sub(nn, mul(d, dot(nn, d))));
    }
    return out;
  }

  // n angles around a closed curve, evenly spaced in ARC LENGTH
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

  // Ring tiling of a surface `surf(s, th) -> {p, n}` (s = 0 at the head/apex): rings at sList,
  // perRing[i] units around ring i, each pointing forward along the surface toward s = 0, hearts
  // at the plate mid-layer, prisms reaching back to just before the next ring's hearts.
  // look(s, th, j, i) -> {elem, spindle, tier, tailReach}.
  function tile(P, surf, sList, perRing, look, rmax, ht, slack, phaseOf) {
    const out = [];
    const rings = sList.map((s, i) => {
      const ths = evenAngles(th => surf(s, th).p, perRing[i], phaseOf ? phaseOf(i) : 0);
      return { ths, hearts: ths.map(th => { const a = surf(s, th); return { a, h: sub(a.p, mul(a.n, ht)) }; }) };
    });
    for (let i = 0; i < sList.length; i++) {
      const s = sList[i], n = perRing[i], next = i + 1 < sList.length ? sList[i + 1] : null;
      const { ths, hearts } = rings[i];
      for (let j = 0; j < n; j++) {
        const th = ths[j], a = hearts[j].a, lk = look(s, th, j, i), heart = hearts[j].h;
        const f = norm(sub(surf(Math.max(0, s - 1e-3), th).p, a.p));
        const r = crystalR(P, lk.elem);
        let reach = next !== null ? len(sub(surf(next, th).p, a.p)) - rmax - P.gap - slack : lk.tailReach;
        let wide = 1e9;
        const ends = [s, Math.min(1, next !== null ? s + 0.8 * (next - s) : i > 0 ? s + 0.8 * (s - sList[i - 1]) : s)];
        for (const k of [(j + 1) % n, (j + n - 1) % n]) for (const se of ends) {
          if (k === j) continue;
          const A = surf(se, th), B = surf(se, ths[k]);
          const chord = len(sub(sub(B.p, mul(B.n, ht)), sub(A.p, mul(A.n, ht))));
          const turn = Math.acos(Math.max(-1, Math.min(1, dot(A.n, B.n))));
          wide = Math.min(wide, 0.5 * P.plateWidth * chord - 1.5 * ht * Math.tan(Math.min(turn, 2.8) / 2) - P.gap / 2);
        }
        if (next !== null) for (const o of rings[i + 1].hearts) {
          const d = sub(o.h, heart), along = -dot(d, f), lat = len(add(d, mul(f, along)));
          if (along > 0 && lat < wide + rmax + P.gap) reach = Math.min(reach, along - rmax - P.gap - slack);
        }
        const lenSp = Math.max(0, Math.min(lk.spindle, reach - r - 2 * HMIN));
        out.push(unit(heart, f, a.n, lk.elem, limb(P, lenSp), { h: [0.5 * (reach - r - lenSp), wide, ht], tier: lk.tier || 0, slot: lk.slot }, lk.role || "skin"));
      }
    }
    return out;
  }

  // Runners on a closed loop `curve(t) -> {p, n}`, t in [0, 1): `count` evenly spaced Time units
  // travelling forward; over one cycle each advances `laps` spacings, so the pattern repeats
  // exactly while every unit really moves (the swarm's fast element, zipping round the body).
  function loopLength(curve) { let a = 0, prev = curve(0).p; for (let i = 1; i <= 200; i++) { const q = curve(i / 200).p; a += len(sub(q, prev)); prev = q; } return a; }
  function runners(P, curve, k, opts) {
    const L = loopLength(curve), uL = unitLength(P, 3, opts.spindle, opts.h[0]);
    const count = Math.max(0, Math.floor(L / (uL * opts.spacing)));
    const out = [], shift = count ? opts.laps * (k / P.frames) / count : 0;
    for (let i = 0; i < count; i++) {
      const t = ((i / count + shift) % 1 + 1) % 1, a = curve(t), b = curve((t + 1e-3) % 1);
      out.push(unit(a.p, norm(sub(b.p, a.p)), a.n, 3, limb(P, opts.spindle), { h: opts.h.slice(), tier: 0, slot: opts.slot }, "runner"));
    }
    return out;
  }
  const smooth = x => { x = C(x, 0, 1); return x * x * (3 - 2 * x); };
  const firstFitRings = (circAt, sFrom, sTo, step, minUnit, spacing) => {
    const sList = [], per = [];
    for (let s = sFrom; s <= sTo + 1e-9; s += step) {
      const n = Math.floor(circAt(s) / Math.max(spacing, minUnit));
      if (n >= 3) { sList.push(s); per.push(n); }
    }
    return { sList, per };
  };

  // ============================ MASS: the whale ============================================
  const WHALE = {
    slotMap: [0, 1, 2], backSlot: 0, bellySlot: 1, finSlot: 0, eyeSlot: 1, runnerSlot: 1, countershade: -0.15,
    gridX: 100, gridY: 44, gridZ: 72, unitScale: 1, crystalSize: 1, spindleThickness: 0.22, gap: 0.2,
    length: 66, girth: 0.14, girthPeak: 0.3, headRound: 0.55, tailTaper: 1.6, peduncle: 0.12, flatten: 0.8, bellyDrop: 0.18,
    ringPitch: 5.0, ringSlack: 0.4, arcSpacing: 2.6, plateWidth: 0.95, plateThickness: 1.6, spindle: 0.4,
    pleats: 1, pleatEnd: 0.45, pleatFlash: 1, eyes: 1, eyePos: 0.13,
    flipperRays: 3, flipperUnits: 2, flipperPos: 0.3, flipperSweep: 35, flipperDroop: 25, flipperSpread: 8,
    flukeRays: 3, flukeUnits: 2, flukeSweep: 30, flukeSpread: 14, dorsal: 1, dorsalPos: 0.66,
    runners: 1, laneHigh: 48, laneLow: 22, runFrom: 0.16, runTo: 0.7, runnerSpacing: 1.6, runnerLaps: 2,
    frames: 8, swimAmp: 0.045, swimWave: 1.1, swimEnv: 2.2, flukePitch: 22, flipperFlap: 12,
  };
  function whaleRadius(P, s) {
    const rmax = P.girth * P.length;
    if (s <= P.girthPeak) { const u = s / P.girthPeak; return rmax * Math.pow(Math.max(0, u * (2 - u)), P.headRound); }
    const t = (s - P.girthPeak) / (1 - P.girthPeak);
    return rmax * (1 - (1 - P.peduncle) * Math.pow(t, P.tailTaper));
  }
  function whaleAt(P, k) {
    const L = P.length, xOf = s => L / 2 - s * L, ht = P.plateThickness / 2, rmax = crystalR(P, 0);
    const surf = (s, th) => {
      const w = Math.max(whaleRadius(P, s), 1e-3), h = w * P.flatten, yc = -P.bellyDrop * w;
      const y = yc + h * Math.sin(th), z = w * Math.cos(th);
      return { p: [xOf(s), y, z], n: norm([0, (y - yc) / (h * h), z / (w * w)]) };
    };
    const sList = [], per = [];
    for (let x = L / 2 - rmax - 0.5; x > -L / 2; x -= P.ringPitch) {
      const s = (L / 2 - x) / L, w = whaleRadius(P, s), h = w * P.flatten;
      const n = Math.floor(perimeter(w - ht, h - ht) / Math.max(P.arcSpacing, 2 * rmax + P.gap));
      if (n < 3 || h < 2 * ht + rmax) { if (sList.length) break; else continue; }
      sList.push(s); per.push(n);
    }
    const ph = TAU * k / P.frames, out = [];
    const tagged = tile(P, surf, sList, per, (s, th, j, i) => {
      const low = Math.sin(th) < P.countershade, pleat = P.pleats && low && s < P.pleatEnd && j % 2 === 0;
      return pleat ? { elem: 0, spindle: P.spindle, role: "pleat", tailReach: P.ringPitch * 0.8, slot: P.bellySlot,
                       tier: P.pleatFlash ? (Math.sin(ph - s * 9) > 0 ? 1 : 0) : 1 }
                   : { elem: 1, spindle: P.spindle, tailReach: P.ringPitch * 0.8, slot: low ? P.bellySlot : P.backSlot };
    }, rmax, ht, P.ringSlack);
    tagged.forEach(u => out.push({ u, s: (L / 2 - u.p[0]) / L }));
    if (P.eyes) for (const side of [1, -1]) {                         // the nearest skin unit becomes a Charge eye
      const e = surf(P.eyePos, side > 0 ? 0.15 : Math.PI - 0.15).p;
      let best = null, bd = 1e9;
      for (const t of out) { const d = len(sub(t.u.p, e)); if (d < bd) { bd = d; best = t; } }
      if (best) { best.u.elem = 0; best.u.prism.tier = 2; best.u.prism.slot = P.eyeSlot; best.u.role = "eye"; }
    }
    const space = (h0) => ({ h: [h0, 0.4, 0.4], tier: 0, slot: P.finSlot });
    for (const side of [1, -1]) {                                     // flippers: fans of Space rods
      const s0 = P.flipperPos, a = surf(s0, side > 0 ? -0.35 : Math.PI + 0.35);
      let span = rotAxis([0, 0, side], [0, 1, 0], -side * deg(P.flipperSweep));
      span = norm(rotAxis(span, [1, 0, 0], side * deg(P.flipperDroop)));
      const chord = norm(sub([-1, 0, 0], mul(span, dot([-1, 0, 0], span)))), fn = norm(cross(span, chord));
      for (let r = 0; r < P.flipperRays; r++) {
        const v = P.flipperRays > 1 ? r / (P.flipperRays - 1) - 0.5 : 0;
        const dir = rotAxis(span, fn, deg(P.flipperSpread) * v * 2);
        const start = add(add(a.p, mul(a.n, 2 * rmax + P.gap)), mul(chord, v * (P.flipperRays - 1) * (2 * rmax + P.gap)));
        chain(P, start, dir, fn, P.flipperUnits, i => ({ elem: 2, spindle: limb(P, 0.6), prism: space(2.6 - 0.5 * i) }), "fin")
          .forEach(u => out.push({ u, s: s0, fin: { side, root: a.p } }));
      }
    }
    if (P.dorsal) {
      const top = surf(P.dorsalPos, Math.PI / 2);
      chain(P, add(top.p, [0, rmax - ht + P.gap, 0]), norm([-0.6, 0.8, 0]), [0, 0, 1], 1, () => ({ elem: 2, spindle: limb(P, 0.4), prism: space(1.6) }), "fin")
        .forEach(u => out.push({ u, s: P.dorsalPos }));
    }
    {                                                                  // flukes: Space rays fanned from the tail stock
      const tailS = sList[sList.length - 1], x0 = xOf(tailS) - P.ringPitch * 0.8 - P.ringSlack - P.gap, y0 = -P.bellyDrop * whaleRadius(P, tailS);
      for (const side of [1, -1]) for (let r = 0; r < P.flukeRays; r++) {
        const v = P.flukeRays > 1 ? r / (P.flukeRays - 1) : 0.5, ang = deg(P.flukeSweep + P.flukeSpread * (v - 0.5) * 2);
        chain(P, [x0 - r * (2 * rmax + P.gap), y0, side * (rmax + P.gap)], norm([-Math.sin(ang), 0, side * Math.cos(ang)]), [0, 1, 0], P.flukeUnits,
          i => ({ elem: 2, spindle: limb(P, 0.5), prism: space(2.4 - 0.4 * i) }), "fluke").forEach(u => out.push({ u, s: 1, fluke: true }));
      }
    }
    if (P.runners) for (const side of [1, -1]) {                      // Time runners zipping round a loop on each flank
      const lift = rmax - ht + crystalR(P, 3) + 2 * P.gap + 0.4;
      // a closed path: along the high lane toward the tail, across, along the low lane back to the head
      const loop = t => {
        t = ((t % 1) + 1) % 1;
        const seg = t * 4;
        let s, th;
        if (seg < 1.7) { s = P.runFrom + (P.runTo - P.runFrom) * (seg / 1.7); th = P.laneHigh; }
        else if (seg < 2) { s = P.runTo; th = lerp(P.laneHigh, P.laneLow, (seg - 1.7) / 0.3); }
        else if (seg < 3.7) { s = P.runTo - (P.runTo - P.runFrom) * ((seg - 2) / 1.7); th = P.laneLow; }
        else { s = P.runFrom; th = lerp(P.laneLow, P.laneHigh, (seg - 3.7) / 0.3); }
        const a = surf(s, side > 0 ? deg(th) : Math.PI - deg(th));
        return { p: add(a.p, mul(a.n, lift)), n: a.n };
      };
      runners(P, loop, k, { spindle: 0.3, h: [1.0, 0.5, 0.4], spacing: P.runnerSpacing, laps: P.runnerLaps, slot: P.runnerSlot })
        .forEach(u => out.push({ u, s: (L / 2 - u.p[0]) / L }));
    }
    // swim: a vertical travelling wave, each unit carried rigidly by the spine where it sits
    const heave = s => P.swimAmp * L * Math.pow(Math.max(0, s), P.swimEnv) * Math.sin(TAU * (s / P.swimWave - k / P.frames));
    return out.map(t => {
      const u = t.u, s = C(t.s, 0, 1), ds = 1e-3, dy = heave(s);
      let pitch = Math.atan2(dy - heave(s + ds), ds * L);
      if (t.fluke) pitch += deg(P.flukePitch) * Math.cos(ph);
      const pivot = [xOf(s), 0, 0];
      let rel = sub(u.p, pivot), f = u.f, n = u.n;
      if (t.fin) {
        const a = deg(P.flipperFlap) * Math.sin(ph) * t.fin.side;
        rel = add(sub(t.fin.root, pivot), rotAxis(sub(u.p, t.fin.root), [1, 0, 0], a));
        f = rotAxis(f, [1, 0, 0], a); n = rotAxis(n, [1, 0, 0], a);
      }
      const z = [0, 0, 1];
      return Object.assign(u, { p: add(add(pivot, [0, dy, 0]), rotAxis(rel, z, pitch)), f: rotAxis(f, z, pitch), n: rotAxis(n, z, pitch) });
    });
  }

  // ============================ SPACE: the jellyfish =======================================
  const JELLY = {
    slotMap: [0, 1, 2], ribSlot: 0, rimSlot: 1, tentacleSlot: 1, armSlot: 0, stomachSlot: 1, runnerSlot: 0,
    gridX: 72, gridY: 80, gridZ: 72, unitScale: 1, crystalSize: 1, spindleThickness: 0.2, gap: 0.2,
    bellRadius: 15, bellHeight: 10, bellSweep: 104, ribs: 18, ribPitch: 6.8, ribSlack: 0.3, plateWidth: 0.9, spindle: 0.6,
    rim: 1, rimShield: 1, stomach: 2, tentacles: 8, tentacleUnits: 3, tentacleCurl: 4, tentacleInset: 0.9,
    arms: 4, armUnits: 3, armCurl: 10, armRadius: 5.5,
    runners: 1, runnerRadius: 0.6, runnerHeight: 0.42, runnerSpacing: 1.7, runnerLaps: 3,
    frames: 8, pulse: 0.15, pulseLift: 2.5, tentacleWave: 16, tentacleLag: 0.6,
  };
  function jellyAt(P, k) {
    const ph = TAU * k / P.frames, con = 0.5 - 0.5 * Math.cos(ph), lift = P.pulseLift * Math.sin(ph);
    const sweep = deg(P.bellSweep), rS = crystalR(P, 2), rmax = crystalR(P, 0), ht = 0.42;
    const bellAt = c => (s, th) => {
      const phi = sweep * s, R = P.bellRadius * (1 - P.pulse * c * s * s), H = P.bellHeight, sm = Math.sin(Math.min(sweep, Math.PI / 2));
      const rho = R * Math.sin(phi) / sm, y = H * Math.cos(phi) + lift;
      return { p: [rho * Math.cos(th), y, rho * Math.sin(th)], n: norm([Math.cos(th) * Math.sin(phi) * H, Math.cos(phi) * R / sm, Math.sin(th) * Math.sin(phi) * H]), rho };
    };
    const tight = bellAt(1), surf = bellAt(con);
    const arcTo = s => { let a = 0, prev = tight(0, 0).p; for (let i = 1; i <= 40; i++) { const q = tight(s * i / 40, 0).p; a += len(sub(q, prev)); prev = q; } return a; };
    const sAt = d => { let lo = 0, hi = 1; for (let it = 0; it < 30; it++) { const m = (lo + hi) / 2; if (arcTo(m) < d) lo = m; else hi = m; } return (lo + hi) / 2; };
    // ribs: rows of Space rods down the meridians; a row too small for every rib carries every other one
    const sList = [], per = [];
    for (let d = rS + 1.0; d < arcTo(1) - 0.5; d += P.ribPitch) {
      const s = sAt(d), circ = TAU * tight(s, 0).rho, need = 2 * rS + P.gap + 0.3;
      const n = circ >= P.ribs * need ? P.ribs : circ >= (P.ribs / 2) * need ? P.ribs / 2 : 0;
      if (n) { sList.push(s); per.push(n); }
    }
    if (P.rim) {                                                       // the last rib row must leave room for a whole rod
      const room = 2 * rS + 2 * 0.9 + P.spindle + P.gap + P.ribSlack + rmax;
      while (sList.length && arcTo(1) - arcTo(sList[sList.length - 1]) < room) { sList.pop(); per.pop(); }
      sList.push(1); per.push(Math.floor(TAU * tight(1, 0).rho / (2 * rmax + P.gap + 0.5)));
    }
    const rimTier = P.rimShield ? (con < 0.35 ? 2 : con < 0.7 ? 0 : 1) : (con > 0.6 ? 1 : 0);   // Charge state follows the pulse
    const out = tile(P, surf, sList, per, (s, th, j, i) => (P.rim && i === sList.length - 1)
      ? { elem: 0, spindle: 0.3, tier: rimTier, tailReach: 1.4, role: "rim", slot: P.rimSlot }
      : { elem: 2, spindle: P.spindle, tailReach: P.ribPitch * 0.8, role: "rib", slot: P.ribSlot }, rmax, ht, P.ribSlack);
    const top = surf(0, 0).p;
    // stomach: chunky Mass units hanging under the apex
    chain(P, [0, top[1] - rS - ht - 0.8, 0], [0, -1, 0], [1, 0, 0], P.stomach, () => ({ elem: 1, spindle: limb(P, 0.3), prism: { h: [1.3, 1.1, 1.1], tier: 0, slot: P.stomachSlot } }), "stomach")
      .forEach(u => out.push(u));
    // Time runners circling inside the bell
    if (P.runners) {
      const yR = P.bellHeight * P.runnerHeight + lift, rR = P.bellRadius * P.runnerRadius * (1 - P.pulse * con * 0.5);
      runners(P, t => ({ p: [rR * Math.cos(TAU * t), yR, rR * Math.sin(TAU * t)], n: [0, 1, 0] }), k,
        { spindle: 0.3, h: [1.0, 0.5, 0.4], spacing: P.runnerSpacing, laps: P.runnerLaps, slot: P.runnerSlot }).forEach(u => out.push(u));
    }
    // tentacles: long Space rods hanging inside the rim, a wave travelling down them
    const rimP = surf(1, 0);
    const below = rimP.p[1] - 1.4 * 2 - rmax - 1.2;
    for (let t = 0; t < P.tentacles; t++) {
      const th = TAU * (t + 0.5) / P.tentacles, rr = surf(1, th);
      const radial = norm([Math.cos(th), 0, Math.sin(th)]);
      chain(P, [rr.p[0] * P.tentacleInset, below, rr.p[2] * P.tentacleInset], [0, -1, 0], radial, P.tentacleUnits, i => ({ elem: 2,
        spindle: limb(P, P.spindle, deg(P.tentacleCurl + P.tentacleWave * Math.sin(ph - i * P.tentacleLag))), prism: { h: [2.8 - 0.3 * i, 0.32, 0.32], tier: 0, slot: P.tentacleSlot } }), "tentacle")
        .forEach(u => out.push(u));
    }
    // oral arms: curling Space rods from under the stomach
    const armY = top[1] - rS - ht - 0.8 - P.stomach * unitLength(P, 1, 0.3, 1.1) - 0.6;
    for (let a = 0; a < P.arms; a++) {
      const th = TAU * a / Math.max(1, P.arms), radial = norm([Math.cos(th), 0, Math.sin(th)]);
      chain(P, add([0, armY, 0], mul(radial, P.armRadius)), [0, -1, 0], radial, P.armUnits, i => ({ elem: 2,
        spindle: limb(P, 0.5, deg(P.armCurl * Math.sin(ph * 0.5 - i * 0.8 + a)), Math.PI / 2), prism: { h: [2.4, 0.4, 0.4], tier: 0, slot: P.armSlot } }), "arm")
        .forEach(u => out.push(u));
    }
    return out;
  }

  // ============================ CHARGE: the pufferfish =====================================
  const PUFFER = {
    slotMap: [0, 1, 2], backSlot: 0, bellySlot: 1, bands: 0, finSlot: 0, beakSlot: 1, runnerSlot: 1, countershade: -0.2,
    gridX: 72, gridY: 72, gridZ: 72, unitScale: 1, crystalSize: 1, spindleThickness: 0.2, gap: 0.2,
    bodyLength: 30, bodyWidth: 22, inflate: 0.32, ringPitch: 4.6, ringSlack: 0.3, arcSpacing: 2.8, plateWidth: 0.92, plateThickness: 0.9, spindle: 0.3,
    spine: 2.0, shieldFrom: 0.3, dangerFrom: 0.66, beak: 1,
    pectoralRays: 3, tailRays: 5, finUnits: 1, finFlutter: 30,
    runners: 1, orbits: 2, orbitTilt: 18, orbitMargin: 2.5, runnerSpacing: 2.0, runnerLaps: 2,
    frames: 8,
  };
  function pufferAt(P, k) {
    const ph = TAU * k / P.frames, c = 0.5 - 0.5 * Math.cos(ph), m = 1 + P.inflate * c;
    const a = P.bodyLength / 2, b = P.bodyWidth / 2, ht = P.plateThickness / 2, rmax = crystalR(P, 0);
    const bodyAt = (mm, mx) => (s, th) => {
      const psi = Math.PI * s, rho = b * mm * Math.sin(psi), x = a * mx * Math.cos(psi);
      return { p: [x, rho * Math.sin(th), rho * Math.cos(th)], n: norm([Math.cos(psi) / (a * mx), Math.sin(psi) * Math.sin(th) / (b * mm), Math.sin(psi) * Math.cos(th) / (b * mm)]) };
    };
    const mx = 1 + 0.4 * P.inflate * c, surf = bodyAt(m, mx), rest = bodyAt(1, 1);
    // rings from the DEFLATED body (inflating only spreads them)
    const arcTo = s => { let q = 0, prev = rest(0, 0).p; for (let i = 1; i <= 40; i++) { const p = rest(s * i / 40, 0).p; q += len(sub(p, prev)); prev = p; } return q; };
    const total = arcTo(0.92), sList = [], per = [];
    for (let d = 2 * rmax + 0.8; d < total; d += P.ringPitch) {
      let lo = 0, hi = 0.92; for (let it = 0; it < 30; it++) { const mm = (lo + hi) / 2; if (arcTo(mm) < d) lo = mm; else hi = mm; }
      const s = (lo + hi) / 2, rho = b * Math.sin(Math.PI * s) - ht;
      const n = Math.floor(TAU * rho / Math.max(P.arcSpacing, 2 * rmax + P.gap));
      if (n >= 3) { sList.push(s); per.push(n); }
    }
    const tier = c < P.shieldFrom ? 0 : c < P.dangerFrom ? 2 : 1;
    const spike = smooth((c - P.dangerFrom) / (1 - P.dangerFrom));    // plates swing out into spines
    const shell = tile(P, rest, sList, per, (s, th, j, i) => ({ elem: 0, spindle: P.spindle, tier, tailReach: P.ringPitch * 0.7, role: "shell",
      slot: P.bands ? (i % 2 ? P.bellySlot : P.backSlot) : Math.sin(th) < P.countershade ? P.bellySlot : P.backSlot }), rmax, ht, P.ringSlack,
      i => (i % 2) * 0.5);
    // re-seat each shell unit on the inflated body, then curl its limb outward and stretch its prism into a spine
    const out = shell.map(u => {
      let best = null, bd = 1e9;                                      // the (s, th) it was laid at
      for (const s of sList) { const th = Math.atan2(u.p[1], u.p[2]); const q = rest(s, th); const d = len(sub(sub(q.p, mul(q.n, ht)), u.p)); if (d < bd) { bd = d; best = [s, th]; } }
      const q = surf(best[0], best[1]), f = norm(sub(surf(Math.max(0, best[0] - 1e-3), best[1]).p, q.p));
      const h = u.prism.h;
      return Object.assign(u, { p: sub(q.p, mul(q.n, ht)), f, n: q.n,
        spindle: Object.assign(u.spindle, { bend: deg(95) * spike }),
        prism: { h: [lerp(h[0], P.spine, spike), lerp(h[1], 0.35, spike), lerp(h[2], 0.35, spike)], tier, slot: u.prism.slot } });
    });
    const front = surf(0, 0).p, back = surf(1, 0).p;
    if (P.beak) {                                                      // a chunky Mass beak at the front
      chain(P, add(front, [rmax * 0.3, 0, 0]), [1, 0, 0], [0, 1, 0], 1, () => ({ elem: 1, spindle: limb(P, 0.3), prism: { h: [1.0, 1.4, 1.0], tier: 0, slot: P.beakSlot } }), "beak")
        .forEach(u => { u.f = [1, 0, 0]; u.p = add(front, [crystalR(P, 1) + P.gap + 0.3, 0, 0]); out.push(u); });
    }
    const spineOut = P.spine * 2 * spike;
    const ray = (h0) => ({ h: [h0, 0.38, 0.38], tier: 0, slot: P.finSlot });
    for (const side of [1, -1]) {                                     // pectoral fans of Space rods, fluttering
      const s0 = 0.38, q = surf(s0, side > 0 ? 0 : Math.PI), flut = deg(P.finFlutter) * Math.sin(ph * 3);
      for (let r = 0; r < P.pectoralRays; r++) {
        const v = P.pectoralRays > 1 ? r / (P.pectoralRays - 1) - 0.5 : 0;
        const dir = norm(rotAxis([-0.35, v * 1.2, side], [1, 0, 0], side * flut));
        chain(P, add(add(q.p, mul(q.n, 2 * rmax + P.gap + spineOut)), [0, v * (P.pectoralRays - 1) * (2 * rmax + P.gap + 0.2), 0]), dir, [0, 1, 0], P.finUnits,
          () => ({ elem: 2, spindle: limb(P, 0.4), prism: ray(2.2) }), "fin").forEach(u => out.push(u));
      }
    }
    for (let r = 0; r < P.tailRays; r++) {                            // caudal fan
      const v = P.tailRays > 1 ? r / (P.tailRays - 1) - 0.5 : 0, sw = deg(20) * Math.sin(ph * 2);
      const dir = norm(rotAxis([-1, v * 1.3, 0], [0, 1, 0], sw));
      chain(P, add(back, [-(rmax + P.gap + spineOut * 0.5), v * (P.tailRays - 1) * (2 * rmax + P.gap + 0.2), 0]), dir, [0, 0, 1], P.finUnits + 1,
        i => ({ elem: 2, spindle: limb(P, 0.4), prism: ray(2.4 - 0.4 * i) }), "tail").forEach(u => out.push(u));
    }
    if (P.runners) for (let o = 0; o < P.orbits; o++) {                // Time runners racing round slanted girth rings, like electrons
      const xo = -a * (0.12 + 0.34 * o), so = Math.acos(C(xo / a, -1, 1)) / Math.PI;
      const R = b * (1 + P.inflate) * Math.sin(Math.PI * so) + P.spine * 2 + rmax + P.orbitMargin, tilt = deg(P.orbitTilt);                                       // parallel rings never cross
      runners(P, t => {
        const p = rotAxis([0, R * Math.sin(TAU * t), R * Math.cos(TAU * t)], [0, 0, 1], tilt);
        return { p: add(p, [xo, 0, 0]), n: rotAxis([1, 0, 0], [0, 0, 1], tilt) };
      }, k, { spindle: 0.3, h: [1.0, 0.5, 0.4], spacing: P.runnerSpacing, laps: P.runnerLaps * (o % 2 ? -1 : 1), slot: P.runnerSlot }).forEach(u => out.push(u));
    }
    return out;
  }

  // ============================ TIME: the dragonfly ========================================
  const DRAGONFLY = {
    slotMap: [0, 1, 2], thoraxSlot: 0, abdomenSlot: 0, stripeSlot: 1, eyeSlot: 1, sparSlot: 0, wingSlot: 2, legSlot: 0,
    gridX: 84, gridY: 56, gridZ: 96, unitScale: 1, crystalSize: 1, spindleThickness: 0.2, gap: 0.2,
    thorax: 3, eyes: 1, abdomen: 9, flick: 14, wingSpan: 30, wingChord: 7.5, wingGap: 2.6, foreSweep: 6, hindSweep: -8,
    spar: 3, flap: 32, beats: 2, hindLag: 0.25, wingInset: 3.5, wingRunnerSpacing: 1.4, runnerLaps: 2, legs: 1,
    frames: 8,
  };
  function dragonflyAt(P, k) {
    const ph = TAU * k / P.frames, out = [], rM = crystalR(P, 1), rS = crystalR(P, 2), rT = crystalR(P, 3), rC = crystalR(P, 0);
    // thorax: chunky Mass units along -x from the neck
    const thx = chain(P, [2, 0, 0], [-1, 0, 0], [0, 1, 0], P.thorax, () => ({ elem: 1, spindle: limb(P, 0.3), prism: { h: [1.3, 1.25, 1.1], tier: 0, slot: P.thoraxSlot } }), "thorax");
    thx.forEach(u => out.push(u));
    const thTail = realise(P, thx[thx.length - 1], 0).tail;
    // head: two shielded Charge eyes
    if (P.eyes) for (const side of [1, -1])
      out.push(unit([2 + rC + P.gap + 1.0, 0.6, side * (rC + 0.4)], [1, 0, 0], [0, 1, 0], 0, limb(P, 0.2), { h: [0.9, 0.9, 0.9], tier: 2, slot: P.eyeSlot }, "eye"));
    // abdomen: a long chain of Time units, flicking up and down twice a cycle
    chain(P, add(thTail, [-P.gap, 0, 0]), [-1, 0, 0], [0, 1, 0], P.abdomen, i => ({ elem: 3,
      spindle: limb(P, 0.3, deg(P.flick) * Math.sin(2 * ph - i * 0.5)), prism: { h: [1.15, 0.55, 0.55], tier: 0, slot: i % 2 ? P.stripeSlot : P.abdomenSlot } }), "abdomen").forEach(u => out.push(u));
    // wings: a Space spar along the leading edge and a loop of Time runners round the outline,
    // beating `beats` times a cycle, the hind pair lagging
    const xs = out.filter(u => u.role === "thorax").map(u => u.p[0]);
    const roots = [Math.max(...xs) - 0.5, Math.max(...xs) - 0.5 - P.wingChord - P.wingGap - 2 * rS];
    roots.forEach((rx, w) => {
      const sweep = deg(w === 0 ? P.foreSweep : P.hindSweep), lag = w === 0 ? 0 : P.hindLag * TAU;
      for (const side of [1, -1]) {
        const flap = deg(P.flap) * Math.sin(P.beats * ph - lag) * side;
        const root = [rx, 1.2, side * (1.3 + rM)];
        const tw = v => rotAxis(v, [1, 0, 0], flap);                   // the whole wing turns about the body axis at its root
        const span = norm([-Math.sin(sweep), 0, side * Math.cos(sweep)]), chordD = norm(cross([0, 1, 0], span)).map(v => v * side);
        const nW = tw([0, 1, 0]);
        // spar
        chain(P, add(root, tw(mul(span, 0.6))), tw(span), nW, P.spar, i => ({ elem: 2, spindle: limb(P, 0.4), prism: { h: [2.6 - 0.3 * i, 0.32, 0.32], tier: 0, slot: P.sparSlot } }), "spar")
          .forEach(u => out.push(u));
        // membrane outline: an ellipse behind the spar in the wing plane
        const L = P.wingSpan, ch = P.wingChord, inset = rS + 0.42 + rT + P.gap + 0.4;
        const r0 = ch / 2, straight = Math.max(0, L - 2 * r0), base = add(root, mul(span, P.wingInset));
        const per = 2 * straight + TAU * r0;                         // a stadium: two straight edges, round ends
        runners(P, t => {
          let d = t * per, u, v;
          if (d < straight) { u = r0 + d; v = -r0; }
          else if ((d -= straight) < Math.PI * r0) { const a = d / r0 - Math.PI / 2; u = r0 + straight + r0 * Math.cos(a); v = r0 * Math.sin(a); }
          else if ((d -= Math.PI * r0) < straight) { u = r0 + straight - d; v = r0; }
          else { d -= straight; const a = d / r0 + Math.PI / 2; u = r0 + r0 * Math.cos(a); v = r0 * Math.sin(a); }
          const p = add(add(base, mul(span, u)), mul(chordD, -(r0 + inset) + v));
          return { p: add(root, tw(sub(p, root))), n: nW };
        }, k, { spindle: 0.2, h: [1.0, 0.45, 0.35], spacing: P.wingRunnerSpacing, laps: P.runnerLaps * side, slot: P.wingSlot }).forEach(u => out.push(u));
      }
    });
    // legs: Space rods tucked under the thorax
    if (P.legs) thx.forEach((t, i) => { for (const side of [1, -1])
      out.push(unit(add(t.p, [0, -1.6 - rS, side * 1.4]), norm([0.3, 1, -side * 0.5]), [1, 0, 0], 2, limb(P, 0.3, deg(-25)), { h: [2.0, 0.3, 0.3], tier: 0, slot: P.legSlot }, "leg")); });
    return out;
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

  // ================================= report ================================================
  const TARGETS = {
    mass: { name: "Whale", elem: 1, defaults: WHALE, at: whaleAt },
    space: { name: "Jellyfish", elem: 2, defaults: JELLY, at: jellyAt },
    charge: { name: "Pufferfish", elem: 0, defaults: PUFFER, at: pufferAt },
    time: { name: "Dragonfly", elem: 3, defaults: DRAGONFLY, at: dragonflyAt },
  };
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
    const g0 = frames[0], elems = [0, 0, 0, 0], census = {}, roles = {};
    g0.units.forEach(u => { elems[u.elem]++; roles[u.role] = (roles[u.role] || 0) + 1; });
    // per-element speed: a unit's heart displacement per frame (units keep their index across frames)
    const speed = [0, 0, 0, 0], sc = [0, 0, 0, 0];
    let sameCount = true;
    for (let k = 0; k < frames.length; k++) {
      const a = frames[k], b = frames[(k + 1) % frames.length];
      if (a.units.length !== b.units.length) { sameCount = false; continue; }
      a.crystals.forEach((c, i) => { speed[c.elem] += len(sub(b.crystals[i].p, c.p)); sc[c.elem]++; });
    }
    const slots = [0, 0, 0]; g0.prisms.forEach(q => slots[q.slot]++);  // domain-slot census (regions)
    const states = [0, 0, 0];                                         // tier usage over all frames (Charge only)
    frames.forEach(g => g.prisms.forEach(q => states[q.tier]++));
    let hmin = 1e9, hmax = 0;
    g0.prisms.forEach(q => {
      const key = ["plain", "danger", "shield"][q.tier];
      census[key] = (census[key] || 0) + 1; hmin = Math.min(hmin, ...q.h); hmax = Math.max(hmax, ...q.h);
    });
    const total = g0.units.length, majority = elems.indexOf(Math.max(...elems));
    return { kind, name: T.name, elem: T.elem, params: P, frames, pairs, overlapsPerFrame: pairs.map(p => p.length), worstDepth: worst,
             lo, hi, size, grid, offset: lo.map((v, i) => -(v + hi[i]) / 2), fits: size.every((v, i) => v <= grid[i] - 2),
             units: total, elements: elems, share: elems.map(v => v / Math.max(1, total)), majorityOk: majority === T.elem,
             speed: speed.map((v, i) => sc[i] ? v / sc[i] : 0), sameCount, states, slots, domainsUsed: slots.filter(v => v).length, census, roles, hmin, hmax };
  }
  // The four seedings: each target's element proportions, the swarm a rule must turn into it.
  function seedings(reports) { return reports.map(r => ({ kind: r.kind, units: r.units, share: r.share })); }

  const API = { ELEMENTS, HEART, SHIELD, HMIN, HMAX, TARGETS, identity, build, report, overlaps, realise, crystalR, seedings };
  if (typeof module !== "undefined" && module.exports) module.exports = API; else root.SwarmModel = API;
})(typeof window !== "undefined" ? window : globalThis);
