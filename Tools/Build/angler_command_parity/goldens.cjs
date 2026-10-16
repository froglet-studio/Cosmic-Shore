#!/usr/bin/env node
// The Angler's parity GOLDENS: runs the Angler Command Lab page's own CORE functions (Docs/Studios/AnglerCommandLab.html,
// window.__lab.CORE - the functions the page's model calls) on one fixed table of inputs, and writes the answers into the
// generated block of the shipped edit-mode test (Assets/_Scripts/Tests/Editor/AnglerCommandMathTests.cs), between the
// lines `// <goldens>` and `// </goldens>`. The test then holds AnglerCommandMath.cs to the page's numbers, in Unity and
// in this folder's run.sh (which compiles the shipped C# and runs the shipped test outside Unity).
//
//   node Tools/Build/angler_command_parity/goldens.cjs           # re-run the page, rewrite the block
//   node Tools/Build/angler_command_parity/goldens.cjs --check   # re-run the page, fail if the committed block differs
//
// Playwright from the container's global modules (NODE_PATH=/usr/local/lib/node_modules_global), headless Chromium.
const fs = require('fs');
const path = require('path');
const ROOT = path.resolve(__dirname, '..', '..', '..');
const PAGE = path.join(ROOT, 'Docs/Studios/AnglerCommandLab.html');
const TEST = path.join(ROOT, 'Assets/_Scripts/Tests/Editor/AnglerCommandMathTests.cs');

let chromium;
try { ({ chromium } = require('playwright')); } catch (e) { console.error('goldens: cannot load playwright (' + e.message + ')'); process.exit(2); }

// One fixed table. A tiny LCG keeps it reproducible without the page's RNG.
function table() {
  let s = 20261015;
  const r = () => { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; };
  const v = (k) => [(r() - 0.5) * k, (r() - 0.5) * k, (r() - 0.5) * k];
  const unit = (a) => { const l = Math.hypot(a[0], a[1], a[2]); return [a[0] / l, a[1] / l, a[2] / l]; };
  const t = { brush: [], cone: [], pick: [], slot: [], follow: [], light: [], tad: [], gather: [], tame: [], lure: [], recent: [], dash: [] };
  for (const d of [0, 0.3, 0.5, 0.6, 0.75, 0.9, 0.99, 1]) for (const m of [0.1, 0.35, 1]) t.brush.push([d, m]);
  const o = [12, -40, 300], f = unit([0.3, -0.2, -0.93]);
  for (let i = 0; i < 48; i++) {
    const along = 5 + r() * 200, off = v(70), p = [o[0] + f[0] * along + off[0], o[1] + f[1] * along + off[1], o[2] + f[2] * along + off[2]];
    const cone = [4, 8, 20][i % 3], reach = [160, 90][i % 2], near = [6, 0, 12][i % 3];
    t.cone.push({ o, f, p, cone, reach, near });
  }
  // close up: 3 u ahead and 4 u off the axis - inside the 6 u near radius, outside the bare 8 deg cone (0.42 u there)
  const side = [f[1], -f[0], 0], sl = Math.hypot(side[0], side[1], side[2]), s1 = [side[0] / sl, side[1] / sl, side[2] / sl];
  t.cone.push({ o, f, p: [o[0] + f[0] * 3 + s1[0] * 4, o[1] + f[1] * 3 + s1[1] * 4, o[2] + f[2] * 3 + s1[2] * 4], cone: 8, reach: 3200, near: 6 });
  t.cone.push({ o, f, p: [o[0] + f[0] * 3 + s1[0] * 4, o[1] + f[1] * 3 + s1[1] * 4, o[2] + f[2] * 3 + s1[2] * 4], cone: 8, reach: 3200, near: 2 });
  t.cone.push({ o, f, p: [o[0] + f[0] * 2500 + s1[0] * 300, o[1] + f[1] * 2500 + s1[1] * 300, o[2] + f[2] * 2500 + s1[2] * 300], cone: 8, reach: 3200, near: 6 });  // far, inside the 351 u rim
  t.cone.push({ o, f, p: [o[0] - f[0] * 2, o[1] - f[1] * 2, o[2] - f[2] * 2], cone: 8, reach: 160, near: 6 });   // behind the nose: never
  t.cone.push({ o, f, p: [o[0] + f[0] * 170, o[1] + f[1] * 170, o[2] + f[2] * 170], cone: 8, reach: 160, near: 6 });   // past reach
  for (let c = 0; c < 10; c++) {
    const from = v(100), cands = [], claims = [];
    for (let i = 0; i < 3 + (c % 6); i++) { cands.push(v(120)); claims.push(Math.floor(r() * 3)); }
    t.pick.push({ from, cands, claims });
  }
  t.pick.push({ from: [0, 0, 0], cands: [], claims: [] });
  // Claim-sensitive cases: the distance gap sits between 800 and 1000, so the answer changes if the penalty moves off 900.
  t.pick.push({ from: [0, 0, 0], cands: [[10, 0, 0], [Math.sqrt(950), 0, 0]], claims: [1, 0] });   // 1000 vs 950 -> 1
  t.pick.push({ from: [0, 0, 0], cands: [[10, 0, 0], [Math.sqrt(1050), 0, 0]], claims: [1, 0] });  // 1000 vs 1050 -> 0
  t.pick.push({ from: [1, 2, 3], cands: [[1, 2, 13], [1, 2 + Math.sqrt(1850), 3]], claims: [2, 1] }); // 1900 vs 1850+900 -> 0
  const p = [5, 10, -20], fw = unit([0.1, 0.2, -1]), up0 = unit([0, 1, 0.2]);
  const right = unit([fw[1] * up0[2] - fw[2] * up0[1], fw[2] * up0[0] - fw[0] * up0[2], fw[0] * up0[1] - fw[1] * up0[0]]);
  const up = [right[1] * fw[2] - right[2] * fw[1], right[2] * fw[0] - right[0] * fw[2], right[0] * fw[1] - right[1] * fw[0]];
  for (let i = 0; i < 14; i++) t.slot.push({ i, p, f: fw, r: right, u: up, back: [6, -4, 20][i % 3] });
  for (const hull of [0, 12, 40, 110]) for (const gap of [0, 4, 9, 60]) for (const [min, cap] of [[18, 51], [10, 24], [30, 20]]) t.follow.push([hull, gap, min, cap]);
  for (const [mx, a, tow] of [[32, 1.25, 1.6], [28, 1, 1], [15, 1.6, 2.5]]) t.light.push([mx, a, tow]);
  for (const [mx, h, tick, gap, tow] of [[15, 10, 1.5, 4, 1.6], [15, 10, 1.5, 90, 1.6], [15, 10, 1.5, 400, 1], [12, 1, 0.01, 30, 1]]) t.tad.push([mx, h, tick, gap, tow]);
  for (const [d, min, top, hull] of [[0, 18, 40, 40], [5, 18, 40, 40], [30, 16, 35, 12], [100, 10, 15, 110]]) t.gather.push([d, min, top, hull]);
  for (const [tm, hold, dt, tt, dec] of [[0, true, 1 / 60, 2.5, 0.6], [0.5, false, 0.5, 2.5, 0.6], [0.1, false, 1, 2.5, 0.6], [0.9, true, 0.3, 0.5, 0]]) t.tame.push([tm, hold, dt, tt, dec]);
  for (const a of [14, 0, 30]) t.lure.push({ p, f: fw, u: up, a });
  t.lure.push({ p: [3, -7, 11], f: [1, 0, 0], u: [0.6, 0.8, 0], a: 10 });   // an up vector with every component non-zero
  for (let c = 0; c < 8; c++) {
    const stamps = [], claims = [];
    for (let i = 0; i < 3 + c; i++) { stamps.push(Math.floor(r() * 4) * 0.25); claims.push(Math.floor(r() * 3)); }
    t.recent.push({ stamps, claims });
  }
  t.recent.push({ stamps: [], claims: [] });
  t.recent.push({ stamps: [1, 5, 5, 3], claims: [0, 1, 0, 0] });   // fewest claims, then newest -> 2
  t.recent.push({ stamps: [2, 2, 2], claims: [1, 1, 1] });         // a full tie -> the lowest index
  for (let c = 0; c < 6; c++) t.dash.push({ p: v(300), c: v(300), frac: [0.75, 0, 1, 0.5, 0.75, 0.3][c] });
  return t;
}

(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=swiftshader'] });
  const page = await (await browser.newContext()).newPage();
  await page.goto('file://' + PAGE);
  await page.waitForTimeout(300);
  const t = table();
  const out = await page.evaluate((t) => {
    const C = window.__lab.CORE;
    return {
      brush: t.brush.map(([d, m]) => C.brushScale(d, m)),
      cone: t.cone.map((c) => C.inBrush(c.o, c.f, c.p, c.cone, c.reach, c.near)),
      pick: t.pick.map((c) => C.pickTarget(c.from, c.cands, c.claims)),
      slot: t.slot.map((c) => C.formationSlot(c.i, c.p, c.f, c.r, c.u, c.back)),
      follow: t.follow.map(([h, g, m, c]) => C.followSpeed(h, g, m, c)),
      light: t.light.map(([m, a, w]) => C.lightFollowCap(m, a, w)),
      tad: t.tad.map(([m, h, k, g, w]) => C.tadFollowCap(m, h, k, g, w)),
      gather: t.gather.map(([d, m, top, h]) => C.gatherSpeed(d, m, top, h)),
      tame: t.tame.map(([tm, h, dt, tt, dec]) => C.tameStep(tm, h, dt, tt, dec)),
      lure: t.lure.map((c) => C.lurePoint(c.p, c.f, c.u, c.a)),
      recent: t.recent.map((c) => C.pickRecent(c.stamps, c.claims)),
      dash: t.dash.map((c) => C.dashPoint(c.p, c.c, c.frac)),
    };
  }, t);
  await browser.close();

  // A brush case whose point sits within a hair of the brush's edge, its reach or the nose plane would make float32 and
  // float64 disagree for no reason: refuse to emit one rather than let rounding decide a golden.
  for (const c of t.cone) {
    const dx = c.p[0] - c.o[0], dy = c.p[1] - c.o[1], dz = c.p[2] - c.o[2], along = dx * c.f[0] + dy * c.f[1] + dz * c.f[2];
    const perp = Math.sqrt(Math.max(0, dx * dx + dy * dy + dz * dz - along * along)), rr = Math.max(c.near, along * Math.tan(c.cone * Math.PI / 180));
    const margin = Math.min(Math.abs(along - c.reach), Math.abs(along), Math.abs(perp - rr) / Math.max(1, rr));
    if (margin < 1e-3) { console.error('goldens: a brush case sits on an edge; move the table'); process.exit(1); }
  }

  const F = (x) => { const s = Number(x).toPrecision(9); return (s.includes('e') ? Number(s).toString() : s) + 'f'; };
  const V = (a) => `new Vector3(${F(a[0])}, ${F(a[1])}, ${F(a[2])})`;
  const L = [];
  L.push('        // Generated by Tools/Build/angler_command_parity/goldens.cjs from the lab page\'s own CORE functions - do not edit.');
  L.push('        static readonly (float depth, float brushMin, float want)[] BrushCases =');
  L.push('        {'); t.brush.forEach(([d, m], i) => L.push(`            (${F(d)}, ${F(m)}, ${F(out.brush[i])}),`)); L.push('        };');
  L.push('        static readonly (Vector3 o, Vector3 f, Vector3 p, float cone, float reach, float near, bool want)[] BrushReachCases =');
  L.push('        {'); t.cone.forEach((c, i) => L.push(`            (${V(c.o)}, ${V(c.f)}, ${V(c.p)}, ${F(c.cone)}, ${F(c.reach)}, ${F(c.near)}, ${out.cone[i]}),`)); L.push('        };');
  L.push('        static readonly (Vector3 from, Vector3[] candidates, int[] claims, int want)[] PickCases =');
  L.push('        {'); t.pick.forEach((c, i) => L.push(`            (${V(c.from)}, new Vector3[] { ${c.cands.map(V).join(', ')} }, new int[] { ${c.claims.join(', ')} }, ${out.pick[i]}),`)); L.push('        };');
  L.push('        static readonly (int i, Vector3 p, Vector3 f, Vector3 r, Vector3 u, float back, Vector3 want)[] SlotCases =');
  L.push('        {'); t.slot.forEach((c, i) => L.push(`            (${c.i}, ${V(c.p)}, ${V(c.f)}, ${V(c.r)}, ${V(c.u)}, ${F(c.back)}, ${V(out.slot[i])}),`)); L.push('        };');
  L.push('        static readonly (float hull, float gap, float min, float cap, float want)[] FollowCases =');
  L.push('        {'); t.follow.forEach((c, i) => L.push(`            (${c.map(F).join(', ')}, ${F(out.follow[i])}),`)); L.push('        };');
  L.push('        static readonly (float max, float aggression, float tow, float want)[] LightCapCases =');
  L.push('        {'); t.light.forEach((c, i) => L.push(`            (${c.map(F).join(', ')}, ${F(out.light[i])}),`)); L.push('        };');
  L.push('        static readonly (float max, float hunt, float tick, float gap, float tow, float want)[] TadCapCases =');
  L.push('        {'); t.tad.forEach((c, i) => L.push(`            (${c.map(F).join(', ')}, ${F(out.tad[i])}),`)); L.push('        };');
  L.push('        static readonly (float d, float min, float top, float hull, float want)[] GatherCases =');
  L.push('        {'); t.gather.forEach((c, i) => L.push(`            (${c.map(F).join(', ')}, ${F(out.gather[i])}),`)); L.push('        };');
  L.push('        static readonly (float tame, bool hold, float dt, float tameTime, float decay, float want)[] TameCases =');
  L.push('        {'); t.tame.forEach(([tm, h, dt, tt, dec], i) => L.push(`            (${F(tm)}, ${h}, ${F(dt)}, ${F(tt)}, ${F(dec)}, ${F(out.tame[i])}),`)); L.push('        };');
  L.push('        static readonly (Vector3 p, Vector3 f, Vector3 u, float ahead, Vector3 want)[] LureCases =');
  L.push('        {'); t.lure.forEach((c, i) => L.push(`            (${V(c.p)}, ${V(c.f)}, ${V(c.u)}, ${F(c.a)}, ${V(out.lure[i])}),`)); L.push('        };');
  L.push('        static readonly (float[] stamps, int[] claims, int want)[] RecentCases =');
  L.push('        {'); t.recent.forEach((c, i) => L.push(`            (new float[] { ${c.stamps.map(F).join(', ')} }, new int[] { ${c.claims.join(', ')} }, ${out.recent[i]}),`)); L.push('        };');
  L.push('        static readonly (Vector3 p, Vector3 c, float frac, Vector3 want)[] DashCases =');
  L.push('        {'); t.dash.forEach((c, i) => L.push(`            (${V(c.p)}, ${V(c.c)}, ${F(c.frac)}, ${V(out.dash[i])}),`)); L.push('        };');
  const block = L.join('\n') + '\n';

  const src = fs.readFileSync(TEST, 'utf8');
  const lines = src.split('\n');
  const open = lines.map((l, i) => /^\s*\/\/ <goldens>\s*$/.test(l) ? i : -1).filter((i) => i >= 0);
  const close = lines.map((l, i) => /^\s*\/\/ <\/goldens>\s*$/.test(l) ? i : -1).filter((i) => i >= 0);
  if (open.length !== 1 || close.length !== 1 || close[0] < open[0]) { console.error('goldens: the test must carry exactly one `// <goldens>` line and one `// </goldens>` line'); process.exit(1); }
  const have = lines.slice(open[0] + 1, close[0]).join('\n') + '\n';
  if (process.argv.includes('--check')) {
    if (have !== block) { console.error('goldens: AnglerCommandMathTests.cs is not the page\'s - re-run without --check (the page or the table moved)'); process.exit(1); }
    console.log(`goldens: OK - the test's ${t.brush.length + t.cone.length + t.pick.length + t.slot.length + t.follow.length + t.light.length + t.tad.length + t.gather.length + t.tame.length + t.lure.length + t.recent.length + t.dash.length} cases are the page's`);
    return;
  }
  const next = lines.slice(0, open[0] + 1).join('\n') + '\n' + block + lines.slice(close[0]).join('\n');
  fs.writeFileSync(TEST, next);
  console.log('goldens: wrote ' + path.relative(ROOT, TEST));
})();
