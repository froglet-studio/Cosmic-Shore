#!/usr/bin/env node
// parity.cjs - holds the Honeyguide Lab's ROUND 1 Dynamo core (Docs/Studios/HoneyguideLab.round1.html, the script block
// id="dynamo-core", exposed as window.__lab.core) to the SHIPPED C# DynamoCore, input by input.
//
//   node Tools/Build/dynamo_lab/parity.cjs
//
// 1. Opens the lab page in headless Chromium and builds the scenarios THERE, from the page's own pattern library and
//    engines: the page's Engineer AI (Hard and Easy) plays each engine under each rule, mixed with random and illegal
//    inputs, Research / Keep / Discard, battery spends and drains. The page's core records every step.
// 2. Runs trace.sh: the shipped DynamoCore.cs, compiled from its real path, replays the same steps.
// 3. Compares every step exactly: legal or not, the state digest (cells, ports, AP, battery, slots, pick, offer, ticks,
//    wasted AP, RNG) and the events. Slip destinations are compared to 1e-9.
// 4. Negative controls: the page's core re-run with a planted change MUST disagree (ports turning the wrong way, the
//    placement scan reversed, a tick 0.25 s long, the slip taking the other end). A gate that cannot see them is not a gate.
'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');
let chromium;
try { ({ chromium } = require('playwright')); } catch (e) { chromium = require(path.join(process.execPath, '../../lib/node_modules/playwright')).chromium; }

const ROOT = path.resolve(__dirname, '../../..');
// Round 2 (2026-10-21) changed the Dynamo's rules while Garrett is still deciding them; DynamoCore.cs stays the round 1
// port until the lab is signed off, and this gate holds it to the round 1 page it was ported from.
const PAGE = path.join(ROOT, 'Docs/Studios/HoneyguideLab.round1.html');

// Runs INSIDE the page. `src` is the core's source (the page's own, or a planted copy); returns scenarios + JS results.
function buildInPage(arg) {
  const L = window.__lab;
  const D = arg.src ? (new Function('root', arg.src.replace(/\}\)\(typeof window[^\n]*\n?\s*$/, '})(root);') + '\nreturn root.DynamoCore;'))({}) : L.core;
  const mul = (a) => () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
  const pats = L.PATTERNS.map((p) => ({ family: p.family, effect: p.effect, cells: p.cells }));
  const byName = (n) => L.PATTERNS.findIndex((p) => p.name === n);
  const loadouts = [['Amanita Furnace', 'Reality Detangler', 'Blink Drive'], ['Chrono-Compressor', '5D Printer', 'Anode Ray'], ['Big Red Button', 'Dragon Saddle', 'Amanita Furnace'], ['Blink Drive', 'Rosen Bridge', 'Monte Cannon']];
  const ev = (e) => ({ tinker: () => 'T' + e.from + '>' + e.to, refuel: () => 'R' + e.cell + ':' + e.colour, place: () => 'P' + e.cell + ':' + e.colour, ability: () => 'A' + e.slot + ':' + e.pid,
    charge: () => 'C' + e.colour, replaced: () => 'X' + e.colour, pick: () => 'K' + e.slot + ':' + e.pid + ':' + e.cell + ':' + e.colour + ':' + (e.auto ? 1 : 0), destroyed: () => 'D' + e.colour + ':' + e.n,
    burned: () => 'B' + e.cell + ':' + e.colour, offer: () => 'O' + e.family + ':' + e.offer.join('.'), keep: () => 'E' + e.slot + ':' + e.pid, tick: () => 't' + e.n })[e.k]();
  const scenarios = [], results = [];
  let n = 0;
  // synthetic tiny patterns: many copies on the board at once, so WHICH copy fires (the scan order) is tested too
  const tiny = [{ family: 1, effect: '', cells: [[0, 0, 1]] }, { family: 2, effect: 'amanita', cells: [[0, 0, 2], [1, 0, 1]] }, { family: 3, effect: 'bigred', cells: [[0, 0, 3], [0, 1, 3]] }];
  for (const eng of Object.keys(L.ENGINES)) for (const rot of [false, true]) for (const variant of [0, 1, 2]) {
    n++;
    const S = { tickSeconds: arg.tick || 8, apPerTick: 3, apBank: 3 + variant, bonusApCap: 1, batteryCap: variant ? 6 : 3, pickTimeout: 4, researchCost: 2,
      rotateWholeEngine: rot, allowRotatedMatches: variant >= 1, centreWild: n % 3 === 0, startFilled: n % 4 !== 1,
      ports: L.ENGINES[eng].ports, patterns: variant === 2 ? tiny : pats, loadout: variant === 2 ? [0, 1, 2] : loadouts[n % loadouts.length].map(byName) };
    const seed = 1000 + n * 7, st = D.create(S, seed), r = mul(seed ^ 0xabcdef), steps = [], rows = [{ ok: true, d: D.digest(st), e: [] }];
    const prm = n % 2 ? { top: 1, blunder: 0, noise: 5 } : { top: 4, blunder: 0.25, noise: 400 };
    for (let k = 0; k < 900; k++) {
      let step;
      const u = r();
      if (u < 0.35) step = { dt: [1 / 30, 1 / 60, 0.25, 0.5, 1.7, 3.1][Math.floor(r() * 6)] };
      else if (u < 0.75) { const inp = D.aiPlan(st, S, prm, [0, r() * 100, r() * 100, r() * 100], r); step = inp ? { inp } : { dt: 0.4 }; }
      else if (u < 0.85) { const all = D.legalInputs(st, S); step = all.length ? { inp: all[Math.floor(r() * all.length)] } : { dt: 0.2 }; }
      else if (u < 0.9) step = { inp: [{ k: 'research', family: 1 + Math.floor(r() * 3) }, { k: 'keep', choice: Math.floor(r() * 3), slot: Math.floor(r() * 3) }, { k: 'discard' }][Math.floor(r() * 3)] };
      else if (u < 0.95) step = { inp: { k: ['tinker', 'refuel', 'activate', 'pick'][Math.floor(r() * 4)], from: Math.floor(r() * 40) - 1, to: Math.floor(r() * 40) - 1, cell: Math.floor(r() * 40) - 1, slot: Math.floor(r() * 4) - 1, colour: Math.floor(r() * 4) } };
      else if (u < 0.985) step = { spend: 1 + Math.floor(r() * 3) };
      else step = { drain: 1 };
      steps.push(step);
      let ok = true;
      if (step.dt != null) D.advance(st, S, step.dt);
      else if (step.spend != null) ok = D.spend(st, step.spend);
      else if (step.drain != null) D.drainAll(st);
      else ok = D.apply(st, S, step.inp);
      rows.push({ ok, d: D.digest(st), e: st.events.splice(0).map(ev) });
    }
    scenarios.push({ S, seed, steps }); results.push(rows);
  }
  // slip cases: random-walk trails, contacts, positions, headings and reaches
  const slips = [], slipOut = [], r = mul(4242);
  for (let k = 0; k < 400; k++) {
    const n2 = 2 + Math.floor(r() * 60), pts = [];
    let x = r() * 400 - 200, z = r() * 400 - 200, h = r() * 6.283;
    for (let i = 0; i < n2; i++) { pts.push([x, k % 5 === 0 ? r() * 10 : 0, z]); h += (r() - 0.5) * 0.6; x += Math.cos(h) * 7; z += Math.sin(h) * 7; }
    const a = r() * 6.283, c = { pts, contact: Math.floor(r() * n2), pos: [r() * 400 - 200, 0, r() * 400 - 200], fwd: [Math.cos(a), 0, Math.sin(a)], reach: 20 + r() * 500, exit: r() * 20 };
    slips.push(c); slipOut.push(D.slipDestination(c.pts, c.contact, c.pos, c.fwd, c.reach, c.exit));
  }
  return { scenarios, results, slips, slipOut };
}

function compare(a, b) {
  let steps = 0, bad = 0, first = null;
  for (let s = 0; s < a.results.length; s++) for (let i = 0; i < a.results[s].length; i++) {
    steps++;
    const x = a.results[s][i], y = b.scenarios[s][i];
    if (!y || x.ok !== y.ok || x.d !== y.d || x.e.join(' ') !== y.e.join(' ')) { bad++; if (!first) first = { scenario: s, step: i, page: x, csharp: y }; }
  }
  let slipBad = 0, slipFirst = null;
  for (let k = 0; k < a.slipOut.length; k++) {
    const x = a.slipOut[k], y = b.slips[k];
    const same = (!x && !y) || (x && y && x.end === y.end && x.index === y.index && x.clipped === y.clipped && [0, 1, 2].every((j) => Math.abs(x.point[j] - y.point[j]) < 1e-9 && Math.abs(x.heading[j] - y.heading[j]) < 1e-9));
    if (!same) { slipBad++; if (!slipFirst) slipFirst = { k, page: x, csharp: y }; }
  }
  return { steps, bad, first, slips: a.slipOut.length, slipBad, slipFirst };
}

(async () => {
  const html = fs.readFileSync(PAGE, 'utf8');
  const m = /<script id="dynamo-core">([\s\S]*?)<\/script>/.exec(html);
  if (!m) { console.error('FAIL: no <script id="dynamo-core"> block in ' + PAGE); process.exit(1); }
  const coreSrc = m[1];
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.goto('file://' + PAGE);
  await page.waitForFunction(() => window.__lab && window.__lab.core);
  const ref = await page.evaluate(buildInPage, {});
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'dynamo-parity-'));
  const scnFile = path.join(tmp, 'scenarios.json');
  fs.writeFileSync(scnFile, JSON.stringify({ scenarios: ref.scenarios, slips: ref.slips }));
  const cs = JSON.parse(execFileSync('bash', [path.join(__dirname, 'trace.sh'), scnFile], { cwd: ROOT, maxBuffer: 1 << 30, stdio: ['ignore', 'pipe', 'ignore'] }).toString());
  const res = compare(ref, cs);
  let fail = false;
  const acts = ref.results.reduce((s, rows) => s + rows.filter((r) => r.e.some((e) => e[0] === 'A')).length, 0);
  const picks = ref.results.reduce((s, rows) => s + rows.reduce((t, r) => t + r.e.filter((e) => e[0] === 'K').length, 0), 0);
  const ticks = ref.results.reduce((s, rows) => s + rows.reduce((t, r) => t + r.e.filter((e) => e[0] === 't').length, 0), 0);
  const offers = ref.results.reduce((s, rows) => s + rows.reduce((t, r) => t + r.e.filter((e) => e[0] === 'O').length, 0), 0);
  const illegal = ref.results.reduce((s, rows) => s + rows.filter((r) => !r.ok).length, 0);
  if (res.bad || res.slipBad) {
    fail = true;
    console.log(`FAIL: ${res.bad} of ${res.steps} steps and ${res.slipBad} of ${res.slips} slips differ`);
    if (res.first) console.log(JSON.stringify(res.first, null, 1).slice(0, 2000));
    if (res.slipFirst) console.log(JSON.stringify(res.slipFirst, null, 1).slice(0, 2000));
  } else {
    console.log(`PASS: ${res.steps} steps over ${ref.scenarios.length} scenarios (4 engines x ports/whole-engine x 3 rule sets, one with synthetic tiny patterns), ${acts} activations, ${picks} picks, ${ticks} ticks, ${offers} research offers, ${illegal} illegal inputs refused alike; ${res.slips} slip destinations equal to 1e-9`);
  }
  if (acts < 50 || picks < 50 || offers < 10) { fail = true; console.log(`FAIL: the scenarios exercised too little (${acts} activations, ${picks} picks, ${offers} offers)`); }
  // negative controls
  const plants = [
    ['ports turn counter-clockwise', (s) => s.replace('return ring[(RING_POS[i] + 1) % ring.length];', 'return ring[(RING_POS[i] + ring.length - 1) % ring.length];'), {}],
    ['placement scan reversed', (s) => s.replace('      for (let a = 0; a < N; a++) {\n        const cells = [];', '      for (let a = N - 1; a >= 0; a--) {\n        const cells = [];'), {}],
    ['tick 8 s -> 8.25 s', (s) => s, { tick: 8.25 }],
    ['the slip takes the other end', (s) => s.replace('const end = d1 >= d0 ? 1 : 0', 'const end = d1 >= d0 ? 0 : 1'), {}],
  ];
  for (const [name, plant, extra] of plants) {
    const src = plant(coreSrc);
    if (src === coreSrc && !extra.tick) { fail = true; console.log(`FAIL: control "${name}" did not apply (the core's text moved; update the plant)`); continue; }
    const bad = await page.evaluate(buildInPage, Object.assign({ src }, extra));
    const r = compare(bad, cs);
    if (r.bad || r.slipBad) console.log(`control ${name}: caught (${r.bad} steps, ${r.slipBad} slips differ)`);
    else { fail = true; console.log(`FAIL: control ${name} was NOT caught`); }
  }
  await browser.close();
  process.exit(fail ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(1); });
