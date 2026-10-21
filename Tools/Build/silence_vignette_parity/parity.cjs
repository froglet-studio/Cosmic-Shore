// Holds the Silence Vignette Lab's twins (Docs/Studios/SilenceVignetteLab.html) to the game: the
// <mask-twin> block to SilenceVignetteMask.cs, the <perimeter-twin> block to SilencePerimeter.cs, and the
// lab's SHIPPED block to the shipped asset (Assets/Resources/VesselSilenceConfig.asset). Run through run.sh.
//
// Planted controls: mutated twins (feather doubled, facet phase flipped, breathing inverted, a shard's
// presence stagger changed, a facet wound inward) and one moved asset value must each FAIL, or the gate
// is not measuring anything.
'use strict';
const fs = require('fs');
const path = require('path');

const [, , labPath, assetPath, casesPath, csOutPath] = process.argv;
const TOL = 1e-4; // float32 (Unity) vs float64 (browser)

function twinFrom(src, tag) {
  const m = src.match(new RegExp('// <' + tag + '>[^\\n]*\\n([\\s\\S]*?)// </' + tag + '>'));
  if (!m) throw new Error('no <' + tag + '> block in ' + labPath);
  return m[1];
}
function loadTwin(block, name) {
  return new Function(block + '\nreturn ' + (name || 'MASK') + ';')();
}
function evalCases(MASK, lines, PERIM) {
  const out = [];
  for (const line of lines) {
    const f = line.trim().split(/\s+/);
    if (!f[0]) continue;
    const n = f.map(Number);
    if (f[0] === 'shape') {
      const s = { style: n[1], facets: n[2], inner: n[3], feather: n[4], roundness: n[5], fill: n[6], rimStrength: n[7], rimWidth: n[8] };
      const vals = [];
      for (let y = 0; y <= 16; y++) for (let x = 0; x <= 16; x++) {
        vals.push(MASK.distance(x / 16, y / 16, n[9], s), MASK.alpha(x / 16, y / 16, n[9], s));
      }
      out.push(['shape', vals]);
    } else if (f[0] === 'breathe') out.push(['breathe', [MASK.breathe(n[1], n[2], n[3])]]);
    else if (f[0] === 'opacity') out.push(['opacity', [MASK.opacity(n[1], n[2], n[3], n[4], n[5])]]);
    else if (f[0] === 'layout') {
      const s = { shape: n[1], count: n[2], seed: n[3], size: n[4], reach: n[5], spin: n[6], pullDrift: n[7] }, vals = [];
      vals.push(...PERIM.pullPoint(s, n[10]));
      for (let i = 0; i < s.count; i++) {
        const q = PERIM.layout(i, s, n[8], n[9], n[10], n[11], n[12]);
        vals.push(...q.position, ...q.right, ...q.up, ...q.forward, q.presence, q.seed);
      }
      out.push(['layout', vals]);
    } else if (f[0] === 'geometry') {
      const g = PERIM.geometry({ shape: n[1], count: n[2], seed: n[3] }, n[4]), vals = [];
      for (let j = 0; j < g.pos.length / 3; j++) vals.push(...g.pos.slice(j * 3, j * 3 + 3), ...g.nor.slice(j * 3, j * 3 + 3), ...g.bar.slice(j * 3, j * 3 + 3));
      out.push(['geometry', vals]);
    }
  }
  return out;
}
function compare(js, cs) {
  const errs = [];
  if (js.length !== cs.length) return ['case count ' + js.length + ' vs ' + cs.length];
  let worst = 0;
  js.forEach(([kind, vals], i) => {
    const [ck, cv] = cs[i];
    if (kind !== ck || vals.length !== cv.length) { errs.push('case ' + i + ' shape mismatch'); return; }
    vals.forEach((v, j) => {
      const d = Math.abs(v - cv[j]);
      worst = Math.max(worst, d);
      if (!(d <= TOL)) errs.push(`case ${i} (${kind}) value ${j}: js ${v} cs ${cv[j]}`);
    });
  });
  return { errs, worst };
}
function shippedFrom(src) {
  const m = src.match(/const SHIPPED = (\{[\s\S]*?\n  \});/);
  if (!m) throw new Error('no SHIPPED block');
  return new Function('return ' + m[1])();
}
function assetDrift(shipped, assetText) {
  const errs = [];
  let n = 0;
  for (const [k, v] of Object.entries(shipped)) {
    if (k.startsWith('lab')) continue;
    const m = assetText.match(new RegExp('^  ' + k + ': (.+)$', 'm'));
    if (!m) { errs.push(`SHIPPED.${k} names no field in the asset`); continue; }
    n++;
    if (Math.abs(Number(m[1]) - v) > 1e-6) errs.push(`SHIPPED.${k} = ${v}, asset has ${m[1].trim()}`);
  }
  return { errs, n };
}

const lab = fs.readFileSync(labPath, 'utf8');
const asset = fs.readFileSync(assetPath, 'utf8');
const lines = fs.readFileSync(casesPath, 'utf8').split('\n');
const cs = fs.readFileSync(csOutPath, 'utf8').trim().split('\n').map((l) => {
  const f = l.trim().split(/\s+/);
  return [f[0], f.slice(1).map(Number)];
});
const block = twinFrom(lab, 'mask-twin');
const pblock = twinFrom(lab, 'perimeter-twin');
const PERIM = loadTwin(pblock, 'PERIM');
let failed = false;

const real = compare(evalCases(loadTwin(block), lines, PERIM), cs);
if (real.errs.length) { failed = true; console.log('FAIL  twin vs C#:\n  ' + real.errs.slice(0, 8).join('\n  ')); }
else console.log(`ok    twin vs C#: ${cs.length} cases, ${cs.reduce((a, c) => a + c[1].length, 0)} values, worst |js - cs| ${real.worst.toExponential(2)}`);

const shipped = shippedFrom(lab);
const drift = assetDrift(shipped, asset);
if (drift.errs.length) { failed = true; console.log('FAIL  SHIPPED vs asset:\n  ' + drift.errs.join('\n  ')); }
else console.log(`ok    SHIPPED vs asset: ${drift.n} fields equal`);

// Planted controls: each must be caught.
const MUTANTS = [
  ['feather doubled', 's.inner + Math.max(0, s.feather)', 's.inner + 2 * Math.max(0, s.feather)'],
  ['facet phase flipped', '- seg * 0.5;\n      return', '+ seg * 0.25;\n      return'],
  ['breathing inverted', 'return 1 - clamp01(depth) * (1 - wave);', 'return 1 - clamp01(depth) * wave;'],
];
for (const [name, from, to] of MUTANTS) {
  if (!block.includes(from)) { failed = true; console.log(`FAIL  control "${name}": its anchor is gone from the twin; update the control`); continue; }
  const r = compare(evalCases(loadTwin(block.replace(from, to)), lines, PERIM), cs);
  if (r.errs.length) console.log(`ok    control "${name}" caught (${r.errs.length} values differ)`);
  else { failed = true; console.log(`FAIL  control "${name}" was NOT caught`); }
}
const PMUTANTS = [
  ['presence stagger changed', '- 0.35 * r(2)', '- 0.3 * r(2)'],
  ['the maw turned off', '(0.55 + 0.9 * maw)', '(0.55 + 0.9 * 0)'],
  ['a facet wound inward', 'if (nx * mx + ny * my + nz * mz < 0)', 'if (nx * mx + ny * my + nz * mz > 0)'],
];
for (const [name, from, to] of PMUTANTS) {
  if (!pblock.includes(from)) { failed = true; console.log(`FAIL  control "${name}": its anchor is gone from the twin; update the control`); continue; }
  const r = compare(evalCases(loadTwin(block), lines, loadTwin(pblock.replace(from, to), 'PERIM')), cs);
  if (r.errs.length) console.log(`ok    control "${name}" caught (${r.errs.length} values differ)`);
  else { failed = true; console.log(`FAIL  control "${name}" was NOT caught`); }
}
const movedAsset = asset.replace(/^  vignetteInner: .*$/m, '  vignetteInner: 0.61');
if (assetDrift(shipped, movedAsset).errs.length) console.log('ok    control "asset vignetteInner moved" caught');
else { failed = true; console.log('FAIL  control "asset vignetteInner moved" was NOT caught'); }

console.log(failed ? 'FAILED' : 'PASS');
process.exit(failed ? 1 : 0);
