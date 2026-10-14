#!/usr/bin/env node
// Holds src/depthPrisms.js to the SHIPPED C# (parity_depth/, see its Program.cs for what is shipped and what is transcribed):
//   1. tryGetPlates on OmniCrystalTriangles (data/depth.json) vs CrystalDepthPrisms.TryGetPlates
//   2. columnBody vs CrystalDepthPrisms.ColumnBody (every plate, places 0 .. 1)
//   3. three 20 s / 60 fps runs, every frame's _DepthState: the triangles (shipped profile: horizon seal + rise while hidden),
//      the triangles on a plain ratchet (reseats and their fades), and the Mass (moving, morphing columns; WriteMass's plate
//      centres, DepthShape.Morph, strength + rest)
//   4. NEGATIVE CONTROLS that must FAIL: the rise 10% fast; the reseat depth 0.01 off; the strike a frame late
//   5. deform() (the shader's vertex stage) against hand-derived cases - the C# cannot run HLSL, so these are NOT parity
//
//   node Tools/Build/converter_reel/parity_depth.cjs [--cs <existing dump.json>]
// Exit 0 = every parity check within TOL and every negative control failed.
'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');

const HERE = __dirname;
const ROOT = path.resolve(HERE, '..', '..', '..');
const TOL = 1e-6;
require(path.join(HERE, 'src', 'depthPrisms.js'));
const DP = globalThis.CR.DepthPrisms;
const depth = JSON.parse(fs.readFileSync(path.join(HERE, 'data', 'depth.json'), 'utf8'));
const combine = JSON.parse(fs.readFileSync(path.join(ROOT, 'Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json'), 'utf8'));

let csPath = null;
const ai = process.argv.indexOf('--cs');
if (ai > 0) csPath = process.argv[ai + 1];
else {
  csPath = path.join(os.tmpdir(), `converter_reel_depth_cs_${process.pid}.json`);
  process.stdout.write('running parity_depth (dotnet) ... ');
  const out = execFileSync('bash', [path.join(HERE, 'parity_depth', 'run.sh'), csPath], { encoding: 'utf8' });
  process.stdout.write('done\n' + out);
}
const cs = JSON.parse(fs.readFileSync(csPath, 'utf8'));
// Every C# input the JS replays is a float printed as its shortest decimal: back to that exact float first.
const F32 = (x) => (Array.isArray(x) ? x.map(F32) : typeof x === 'number' ? Math.fround(x) : x);
for (const c of cs.cases) for (const fr of c.frames) for (const k of ['eye', 'dt', 'strength', 'rest', 'strike', 'M', 'centre', 'cam', 'm']) if (k in fr) fr[k] = F32(fr[k]);

let failures = 0;
function report(what, err, extra = '') {
  const ok = err <= TOL;
  if (!ok) failures++;
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${what}: max |err| ${err.toExponential(2)}${extra ? '  (' + extra + ')' : ''}`);
}
// The C# side prints each float's SHORTEST round-trip decimal; Math.fround turns it back into that exact float.
const maxErr = (a, b) => { let e = 0; for (let i = 0; i < a.length; i++) e = Math.max(e, Math.abs(a[i] - Math.fround(b[i]))); return a.length === b.length ? e : Infinity; };

// ---------------------------------------------------------------- 1, 2
const mesh = DP.trianglesMesh(depth);
const plates = DP.tryGetPlates(mesh);
if (plates.problem) { console.log('FAIL  tryGetPlates: ' + plates.problem); process.exit(1); }
report('tryGetPlates directions', maxErr(plates.directions.flat(), cs.plates.directions.flat()), `${plates.directions.length} plates`);
report('tryGetPlates centres', maxErr(plates.centres.flat(), cs.plates.centres.flat()));
report('tryGetPlates tops', maxErr(plates.tops, cs.plates.tops));
report('tryGetPlates bounds', maxErr(plates.bounds, cs.plates.bounds));
report('tryGetPlates plateRadius', maxErr([plates.plateRadius], [cs.plates.plateRadius]), `${plates.plateRadius}`);

const mats = depth.materials, dj = depth.combine.depth;
const triShape = DP.DepthShape.of(mats.omni, 1, 1);
const massShape = DP.DepthShape.of(mats.mass, depth.combine.massMeshScale, dj.massThickness);
const triThin = DP.DepthShape.of(mats.omni, 1, dj.omniThickness);
{
  let e = 0;
  for (const [j, p, x, y, z, r] of cs.columnBody) {
    const b = DP.columnBody(plates.centres[j], plates.bounds[j], p, triShape.reach, triShape.sizeStart, Math.fround(triShape.faceSplit * triShape.splitUnit));
    e = Math.max(e, maxErr([...b.centre, b.radius], [x, y, z, r]));
  }
  report('columnBody', e, `${cs.columnBody.length} samples`);
}

// ---------------------------------------------------------------- 3
const f = Math.fround;
const massData = combine.crystals.find((c) => c.element === 'Mass');
const massSites = DP.massSites(massData.plateSite);

function runCase(c, mutate) {
  const prof = DP.profile(mutate && mutate.profile ? mutate.profile(c.profile) : c.profile);
  let worst = 0, worstFrame = -1, extra = 0;
  if (c.kind === 'triangles') {
    const cols = new DP.DepthColumns(plates.directions.length, plates.directions.map((v) => v.slice()), plates.centres.map((v) => v.slice()),
                                     plates.tops.slice(), plates.bounds.slice(), plates.plateRadius);
    let lateStrike = null;
    c.frames.forEach((fr, i) => {
      const strike = mutate && mutate.lateStrike ? (lateStrike || fr.strike.map(() => 0)) : fr.strike;
      for (let j = 0; j < cols.count; j++) cols.strike[j] = strike[j];
      lateStrike = fr.strike;
      if (fr.fresh) cols.fresh = true;
      cols.step(fr.eye, fr.dt, prof, triShape.reach, triShape.sizeStart, fr.strength);
      const e = maxErr(cols.state.slice(0, cols.count).flat(), fr.state);
      if (e > worst) { worst = e; worstFrame = i; }
    });
  } else {
    const basis = massData.basis.map(f), plateOf = massData.plate, np = massData.plateSite.length;
    const cols = new DP.DepthColumns(np);
    const pose = new Float32Array(basis.length);
    c.frames.forEach((fr, i) => {
      const M = fr.M, ce = fr.centre;
      for (let v = 0; v < basis.length / 3; v++) {
        const bx = basis[3 * v], by = basis[3 * v + 1], bz = basis[3 * v + 2];
        pose[3 * v] = f(f(f(f(M[0] * bx) + f(M[1] * by)) + f(M[2] * bz)) + ce[0]);
        pose[3 * v + 1] = f(f(f(f(M[3] * bx) + f(M[4] * by)) + f(M[5] * bz)) + ce[1]);
        pose[3 * v + 2] = f(f(f(f(M[6] * bx) + f(M[7] * by)) + f(M[8] * bz)) + ce[2]);
      }
      const morph = new Array(np);
      for (let q = 0; q < np; q++) morph[q] = Math.min(1, Math.max(0, f(f(fr.m * f(1.5)) - f(q / 120))));
      const mp = DP.massPlates(pose, ce, plateOf, massSites, morph);
      extra = Math.max(extra, maxErr(mp.plates.map((p) => p.slice(0, 3)).flat(), fr.plates), maxErr([mp.morph], [fr.morphMean]));
      const shape = DP.DepthShape.morph(massShape, triThin, mp.morph);
      cols.setMassPlates(mp.plates, shape.topHeight);
      extra = Math.max(extra, maxErr([cols.plateRadius, shape.topHeight], [fr.plateRadius, fr.topHeight]));
      const eye = DP.f32.sub(fr.cam, ce);
      cols.step(eye, fr.dt, prof, shape.reach, shape.sizeStart, fr.strength, fr.rest);
      const e = maxErr(cols.state.slice(0, cols.count).flat(), fr.state);
      if (e > worst) { worst = e; worstFrame = i; }
    });
  }
  return { worst, worstFrame, extra };
}

for (const c of cs.cases) {
  const r = runCase(c);
  report(`case ${c.name}: _DepthState, ${c.frames.length} frames`, r.worst,
         `C# branches ${JSON.stringify(c.stats)}${r.worstFrame >= 0 ? ', worst at frame ' + r.worstFrame : ''}`);
  if (c.kind === 'mass') report(`case ${c.name}: WriteMass plate centres, plate radius, morphed top height`, r.extra);
}

// ---------------------------------------------------------------- 4 negative controls
let ncPassed = 0;
function negative(what, caseName, mutate, patch) {
  const undo = patch ? patch() : null;
  const r = runCase(cs.cases.find((c) => c.name === caseName), mutate);
  if (undo) undo();
  const failed = r.worst > TOL;
  if (!failed) ncPassed++;
  console.log(`${failed ? 'PASS' : 'FAIL'}  negative control "${what}" on ${caseName} ${failed ? 'fails as it must' : 'PASSED - the check is blind'}: max |err| ${r.worst.toExponential(2)}`);
}
negative('rise 10% fast', 'triangles', null, () => {
  const orig = DP.DepthRatchet.riseTarget;
  DP.DepthRatchet.riseTarget = (c, seat, dt, rate) => orig(c, seat, dt, Math.fround(rate * 1.1));
  return () => { DP.DepthRatchet.riseTarget = orig; };
});
negative('rise 10% fast', 'mass', null, () => {
  const orig = DP.DepthRatchet.riseTarget;
  DP.DepthRatchet.riseTarget = (c, seat, dt, rate) => orig(c, seat, dt, Math.fround(rate * 1.1));
  return () => { DP.DepthRatchet.riseTarget = orig; };
});
negative('reseat depth 0.01 off', 'triangles-ratchet', { profile: (p) => Object.assign({}, p, { reseatDepth: p.reseatDepth + 0.01 }) });
negative('strike one frame late', 'triangles', { lateStrike: true });
failures += ncPassed;

// ---------------------------------------------------------------- 5 deform, hand-derived (NOT parity: HLSL cannot run in C#)
{
  const n = mesh.vertexCount, P = mesh.positions, N = mesh.normals, U = mesh.uv2;
  const st = (x, y, z, w) => Array.from({ length: 60 }, () => [x, y, z, w]);
  const spread = [0.01, 0.01, 0.01];
  const nrm = (i) => { const l = Math.hypot(N[3 * i], N[3 * i + 1], N[3 * i + 2]); return [N[3 * i] / l, N[3 * i + 1] / l, N[3 * i + 2] / l]; };
  let e;

  // (a) sealed (place 1), thickness 1: the mesh as authored, pushed out along its normal by _Spread / objectScale.
  for (const scale of [1, 0.37]) {
    const d = DP.deform(mesh, st(1, 1, 1, 0), triShape, { spread, objectScale: scale });
    e = 0;
    for (let i = 0; i < n; i++) { const m = nrm(i); for (let k = 0; k < 3; k++) e = Math.max(e, Math.abs(d.positions[3 * i + k] - (P[3 * i + k] + m[k] * 0.01 / scale))); }
    report(`deform: sealed = mesh as authored + spread (objectScale ${scale})`, e);
  }
  // (b) undriven: identity whatever the state; arriving alpha 1, leaving 0, strike 0.
  {
    const s = st(0, 0.3, 0.2, 5);
    const a = DP.deform(mesh, s, triShape, { spread: [0, 0, 0], driven: false });
    const l = DP.deform(mesh, s, triShape, { spread: [0, 0, 0], driven: false, pass: 'leaving' });
    e = Math.max(maxErr(a.positions, P), ...a.alpha.map((x) => Math.abs(x - 1)), ...l.alpha.map((x) => Math.abs(x)), ...a.strike.map(Math.abs));
    report('deform: undriven = identity, alpha 1 / 0, no strike', e);
  }
  // (c) top of the column (place 0), no face split: each prism is the authored prism scaled by reach * sizeStart about its plate
  // centre, carried out to reach x its centre. Checked by the centroid map and every pairwise distance's ratio.
  {
    const shape = Object.assign({}, triShape, { faceSplit: 0 });
    const d = DP.deform(mesh, st(0, 1, 1, 0), shape, { spread: [0, 0, 0] });
    const s = shape.reach * shape.sizeStart;
    e = 0;
    for (let q = 0; q < plates.centres.length; q++) {
      const idx = []; for (let i = 0; i < n; i++) if (Math.round(U[4 * i + 3]) === q) idx.push(i);
      const c = [U[4 * idx[0]], U[4 * idx[0] + 1], U[4 * idx[0] + 2]];
      for (const i of idx) for (let k = 0; k < 3; k++) e = Math.max(e, Math.abs(d.positions[3 * i + k] - (c[k] * shape.reach + (P[3 * i + k] - c[k]) * s)));
      for (const i of idx) for (const j of idx) {
        const a0 = Math.hypot(P[3 * i] - P[3 * j], P[3 * i + 1] - P[3 * j + 1], P[3 * i + 2] - P[3 * j + 2]);
        const a1 = Math.hypot(d.positions[3 * i] - d.positions[3 * j], d.positions[3 * i + 1] - d.positions[3 * j + 1], d.positions[3 * i + 2] - d.positions[3 * j + 2]);
        e = Math.max(e, Math.abs(a1 - a0 * s));
      }
    }
    report(`deform: place 0 = prism x${s.toFixed(2)} about its centre, carried to ${shape.reach}x`, e);
  }
  // (d) thickness T sealed: the top face (height == _TopHeight above the centre along its direction) stays; a vertex at height h
  // goes to top + (h - top) * T, and nothing moves sideways.
  {
    const T = 3;
    const shape = Object.assign({}, triShape, { thickness: T });
    const d = DP.deform(mesh, st(1, 1, 1, 0), shape, { spread: [0, 0, 0] });
    e = 0;
    for (let i = 0; i < n; i++) {
      const c = [U[4 * i], U[4 * i + 1], U[4 * i + 2]], r = Math.hypot(...c), dir = c.map((x) => x / r);
      const h = (P[3 * i] - c[0]) * dir[0] + (P[3 * i + 1] - c[1]) * dir[1] + (P[3 * i + 2] - c[2]) * dir[2];
      const want = shape.topHeight + (h - shape.topHeight) * T;
      for (let k = 0; k < 3; k++) e = Math.max(e, Math.abs(d.positions[3 * i + k] - (P[3 * i + k] + dir[k] * (want - h))));
    }
    report(`deform: thickness ${T} grows inward from the top face`, e);
  }
  // (e) the leaving pass reads place z and alpha 1 - y; strike = max(w, 0).
  {
    const s = st(0, 0.25, 1, -2);
    const l = DP.deform(mesh, s, triShape, { spread: [0, 0, 0], pass: 'leaving' });
    const a = DP.deform(mesh, s, triShape, { spread: [0, 0, 0] });
    e = Math.max(maxErr(l.positions, P), ...l.alpha.map((x) => Math.abs(x - 0.75)), ...a.alpha.map((x) => Math.abs(x - 0.25)), ...a.strike.map(Math.abs));
    report('deform: leaving pass = place z (here sealed), alpha 1 - y; negative strike clamps to 0', e);
  }
  // Note (not a gate): ColumnTop (C#, used by the rise-while-hidden test) vs the shader's own top at place 0.
  {
    const d = DP.deform(mesh, st(0, 1, 1, 0), Object.assign({}, triShape, { faceSplit: 0 }), { spread: [0, 0, 0] });
    let gap = 0;
    for (let q = 0; q < plates.centres.length; q++) {
      const top = DP.DepthRatchet.columnTop(plates.centres[q], plates.directions[q], plates.tops[q], 0, triShape.reach, triShape.sizeStart);
      let hi = -Infinity;
      for (let i = 0; i < n; i++) if (Math.round(U[4 * i + 3]) === q) hi = Math.max(hi, d.positions[3 * i] * plates.directions[q][0] + d.positions[3 * i + 1] * plates.directions[q][1] + d.positions[3 * i + 2] * plates.directions[q][2]);
      gap = Math.max(gap, Math.abs(hi - DP.f32.dot(top, plates.directions[q])));
    }
    console.log(`note  ColumnTop (C#) sits ${gap.toExponential(2)} below the shader's drawn top at place 0 (top face x lerp(sizeStart,1,u) without the column's k) - shipped behaviour, ported as is`);
  }
}

if (!process.argv.includes('--cs')) try { fs.unlinkSync(csPath); } catch (e) { /* tmp */ }
console.log(failures ? `\n${failures} FAILURE(S)` : '\nALL PASS (parity within ' + TOL + ', negative controls fail)');
process.exit(failures ? 1 : 0);
