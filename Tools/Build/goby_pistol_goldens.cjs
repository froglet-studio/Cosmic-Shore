#!/usr/bin/env node
// goby_pistol_goldens.cjs - the golden numbers for the Goby & Pistol Unity port, taken from the LAB's own code.
//
// The lab (Docs/Studios/VesselStudio/goby.html) is the source of truth (/artifact-to-unity). This script opens it in
// headless Chromium, feeds its own functions (window.__lab.core) the fixed inputs below, and prints what they return.
// The C# port's edit-mode tests (GobyPistolMathTests.cs) feed the SAME inputs to the C# classes and must match.
//
//   NODE_PATH=<folder with playwright> node Tools/Build/goby_pistol_goldens.cjs [--three <local three.min.js>] > goldens.json
//
// The inputs (keep them identical in the C# test, GobyPistolMathTests.GoldenPath):
//   path: 360 steps of dt = 1/30; t = k dt;
//     pos(t) = (100 sin 0.7t, 40 sin 1.1t, -55 t)
//     f(t)   = normalize(70 cos 0.7t, 44 cos 1.1t, -55)
//     u(t)   = normalize(u0 - f (f . u0)), u0 = (0.25 sin 0.5t, 1, 0)
//   the ribbon starts with ribAdd(pos0, u0, f0) + addBlock(pos0), then layPath(pos[k-1], pos[k]) with the Goby's f, u set
//   to step k's, then gobyEnds.
'use strict';
const path = require('path');
let chromium; try { ({ chromium } = require('playwright')); } catch (e) { console.error('needs playwright on NODE_PATH'); process.exit(2); }
const args = process.argv.slice(2), three = args.includes('--three') ? args[args.indexOf('--three') + 1] : null;
const page = path.resolve(__dirname, '../../Docs/Studios/VesselStudio/goby.html');

(async () => {
  const b = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  const pg = await (await b.newContext({ viewport: { width: 1200, height: 800 } })).newPage();
  const errs = []; pg.on('pageerror', (e) => errs.push(e.message));
  if (three) await pg.route('**/three.min.js', (r) => r.fulfill({ path: three, contentType: 'application/javascript' }));
  await pg.goto('file://' + page); await pg.waitForTimeout(400);
  const out = await pg.evaluate(() => {
    const L = window.__lab, C = L.core, P = Object.assign({}, L.SHIPPED);
    const V = (x, y, z) => ({ x, y, z }), nrm = (a) => { const l = Math.hypot(a.x, a.y, a.z) || 1; return V(a.x / l, a.y / l, a.z / l); };
    const dot = (a, b) => a.x * b.x + a.y * b.y + a.z * b.z, cross = (a, b) => V(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
    const pose = (t) => {
      const pos = V(100 * Math.sin(0.7 * t), 40 * Math.sin(1.1 * t), -55 * t), f = nrm(V(70 * Math.cos(0.7 * t), 44 * Math.cos(1.1 * t), -55));
      const u0 = V(0.25 * Math.sin(0.5 * t), 1, 0), d = dot(f, u0), u = nrm(V(u0.x - f.x * d, u0.y - f.y * d, u0.z - f.z * d));
      return { pos, f, u, r: nrm(cross(f, u)) };
    };
    const build = (mode) => {
      const W = { P, rb: C.makeRibbon(mode), o: {}, blocks: [], endX: 0, endY: 0, gx: 0 };
      const p0 = pose(0); W.g = { pos: p0.pos, f: p0.f, u: p0.u, r: p0.r, acc: 0, blockAcc: 0, blockN: 0, speed: 80 };
      C.ribAdd(W.rb, p0.pos, p0.u, p0.f); C.addBlock(W, p0.pos);
      for (let k = 1; k <= 360; k++) { const a = pose((k - 1) / 30), q = pose(k / 30); W.g.f = q.f; W.g.u = q.u; W.g.r = q.r; C.layPath(W, a.pos, q.pos); W.g.pos = q.pos; C.gobyEnds(W); }
      return W;
    };
    const r6 = (v) => +v.toFixed(6), rv = (a) => [r6(a.x), r6(a.y), r6(a.z)];
    const ribbon = {};
    for (const mode of ['world', 'arc', 'goby']) {
      const W = build(mode), rb = W.rb, n = rb.n, X = (k) => (mode === 'world' ? rb.sig[k] : rb.s[k]);
      const queries = [0.8, 2.1, 3.7, 5.2, 7.9, 10.4].map((t, j) => { const q = pose(t).pos; return V(q.x + [12, -20, 30, -8, 4, -35][j], q.y + [9, -14, 3, 22, -6, 11][j], q.z + [-7, 15, 2, -19, 11, 5][j]); });
      const map = queries.map((q) => { const m = C.ribMap(rb, q, -1e9, 1e9, {}); return { q: rv(q), x: r6(m.x), h: r6(m.h), d: r6(m.d), amb: !!m.amb }; });
      const unmapIn = [[10, 5, 0], [120.5, -12, 20], [300, 30, -35], [455.25, 0, 44], [610, -40, 10], [-5, 3, 2]];
      const unmap = unmapIn.map(([x, h, d]) => ({ x, h, d, w: rv(C.ribUnmap(rb, x, h, d, V(0, 0, 0))) }));
      const xs = [0, 33.3, 100, 187.5, 250, 401, 512.25, X(n - 1) - 3];
      const ground = xs.map((x) => { const y = C.groundAt(W, x); return { x: r6(x), y: r6(y), slope: r6(C.GS) }; });
      const frames = [50, 260, 480].map((x) => { const F = C.ribFrameAt(rb, x, {}); return { x, T: rv(F.T), U: rv(F.U), R: rv(F.R) }; });
      ribbon[mode] = { n, cps: rb.cx.length, cp: rb.cx.slice(0, 6).map((x, i) => [r6(x), r6(rb.cy[i])]), lastX: r6(X(n - 1)), endX: r6(W.endX), endY: r6(W.endY), blocks: W.g.blockN, map, unmap, ground, frames };
    }
    // the Pistol's body on the World-up ribbon: dive on a 1 s rhythm, a launch at step 100
    const W = build('world'); W.t = 0; W.stats = { air: 0, goodLand: 0, badLand: 0, launches: 0 }; W.N = 0; W.fx = []; W.vortices = [];
    const y0 = C.groundAt(W, 40);
    W.p = { x: 40, y: y0, vx: 0, vy: 0, v: 60, grounded: true, launch: P.pLaunchCharges, launchT: 0, lastLaunchAt: -9, ghostAt: 0, ghostUntil: -1, vortexAt: 0, prevVortex: false, prevGhost: false, freeBoom: false, wpos: V(0, 0, 0) };
    const body = [];
    for (let k = 0; k <= 240; k++) {
      if (k % 30 === 0) body.push({ k, x: r6(W.p.x), y: r6(W.p.y), grounded: W.p.grounded, v: r6(W.p.v), vx: r6(W.p.vx), vy: r6(W.p.vy), launch: W.p.launch });
      if (k === 240) break;
      W.t += 1 / 60;
      C.pistolStep(W, 1 / 60, { dive: k % 60 < 25 ? 1 : 0, launch: k === 100 ? { dx: 120, dy: 90 } : null });
    }
    // the vortex ring: one ring from the origin along -z through 24 prisms
    const Wv = { P, t: 0, N: 24, alive: [], capt: [], px: [], py: [], pz: [], ra: [], vortices: [], fx: [], stats: { vortexKills: 0 }, kind: [], primed: [] };
    for (let i = 0; i < 24; i++) { const a = i / 24 * Math.PI * 2; Wv.px.push(Math.cos(a) * 22 + (i % 2) * 5); Wv.py.push(Math.sin(a) * 22); Wv.pz.push(-40 + ((i % 3) - 1) * 6); Wv.alive.push(1); Wv.capt.push(0); Wv.ra.push(0); Wv.kind.push(1); Wv.primed.push(-1); }
    const n = V(0, 0, -1), e1 = nrm(cross(n, V(0, 1, 0))), e2 = cross(n, e1);
    Wv.vortices.push({ c: V(0, 0, 0), n, e1, e2, life: P.pVortexLife, age: 0, cap: [] });
    const vring = [];
    for (let k = 1; k <= 60; k++) {
      Wv.t += 1 / 60; C.vortexStep(Wv, 1 / 60);
      if (k % 20 === 0) { const v = Wv.vortices[0]; vring.push({ k, c: rv(v.c), cap: v.cap.length, first: v.cap.slice(0, 3).map((c) => ({ i: c.i, th: r6(c.th), ph: r6(c.ph), rho: r6(c.rho), p: [r6(Wv.px[c.i]), r6(Wv.py[c.i]), r6(Wv.pz[c.i])] })) }); }
    }
    // the Pistol's AI on the World-up ribbon: the Pistol at x = 100 on its ground, a cluster of 8 at x ~ 200 (one primed),
    // a friendly block just ahead of it; three thoughts at t = 0.05, 0.4, 1.0 per level, each level on a fresh rng
    const rng0 = C.mulberry32(31337 + 17), rngSeq = [rng0(), rng0(), rng0(), rng0()].map(r6);
    const ai = {};
    for (const lvl of ['Hard', 'Medium', 'Easy']) {
      const Wa = build('world'); Wa.N = 0; Wa.alive = []; Wa.px = []; Wa.py = []; Wa.pz = []; Wa.kind = []; Wa.primed = [];
      const px = 100, py = C.groundAt(Wa, px), add = (x, h, d, kind, primed) => { const w = C.ribUnmap(Wa.rb, x, h, d, V(0, 0, 0)); Wa.px.push(w.x); Wa.py.push(w.y); Wa.pz.push(w.z); Wa.kind.push(kind); Wa.primed.push(primed); Wa.alive.push(1); Wa.N++; };
      const gy = C.groundAt(Wa, 200);
      for (let i = 0; i < 8; i++) add(200 + (i % 4) * 6, gy + 20 + Math.floor(i / 4) * 6, ((i % 3) - 1) * 10, 1, i === 5 ? 99 : -1);
      add(130, py + 5, 0, 3, -1);
      Wa.p = { x: px, y: py, vx: 0, vy: 0, v: 60, grounded: true, launch: 2, launchT: 0, lastLaunchAt: -9, ghostAt: 0, ghostUntil: -1, vortexAt: 0, wpos: C.ribUnmap(Wa.rb, px, py, 0, V(0, 0, 0)) };
      Wa.aiP = { scanAt: 0, vis: [], key: -1, noticeAt: 0, mis: false, out: {}, aim: null }; Wa.rngAI = C.mulberry32(31337 + 17);
      const prisms = []; for (let i = 0; i < Wa.N; i++) prisms.push([r6(Wa.px[i]), r6(Wa.py[i]), r6(Wa.pz[i])]);
      ai[lvl] = { p: [r6(px), r6(py)], prisms, out: [0.05, 0.4, 1.0].map((t) => { Wa.t = t; const o = C.pistolAI(Wa, lvl); return { t, vis: Wa.aiP.vis.length, dive: o.dive, launch: o.launch ? [r6(o.launch.dx), r6(o.launch.dy)] : null, ghost: !!o.ghost, vortex: o.vortex ? [r6(o.vortex.dx), r6(o.vortex.dy)] : null }; }) };
    }
    // the dual-stick mix and its inverse (the AI presses what unmix says)
    const mixes = [[0.3, -0.2, 0.1, 0.9], [-1, 0.5, -0.4, 0.2], [0, 0, 0, 1]].map(([y, p, r, t]) => { const s = C.unmix(y, p, r, t), m = C.mix(s.L, s.R); return { in: [y, p, r, t], L: [r6(s.L.x), r6(s.L.y)], R: [r6(s.R.x), r6(s.R.y)], back: [r6(m.yaw), r6(m.pitch), r6(m.roll), r6(m.thr)] }; });
    return { shipped: P, ribbon, body, vring, rngSeq, ai, mixes };
  });
  if (errs.length) { console.error(errs.join('\n')); process.exit(1); }
  process.stdout.write(JSON.stringify(out, null, 1) + '\n');
  await b.close();
})();
