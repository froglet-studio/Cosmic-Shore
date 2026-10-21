#!/usr/bin/env node
// parity.cjs - holds the Honeyguide Lab's Dynamo core (Docs/Studios/HoneyguideLab.html, the script block id="dynamo-core",
// exposed as window.__lab.core; round 4) to the SHIPPED C# DynamoCore, input by input - the Engineer AI included.
//
//   node Tools/Build/dynamo_lab/parity.cjs
//
// 1. Opens the lab page in headless Chromium and builds the scenarios THERE, from the page's own pattern library and port
//    sets: under several rule sets (reveal on / off, ports count or not, rotations, replace, shuffle, every fixed ability
//    and the random draw) a mix of clock steps, Engineer-AI steps (the page's aiPlan and the C# AIPlan each pick from the
//    same state with the same dice and memo), random legal inputs, illegal inputs, battery spends and every board effect.
//    The page's core records every step.
// 2. Runs trace.sh: the shipped DynamoCore.cs, compiled from its real path, replays the same steps.
// 3. Compares every step exactly: legal or not, the state digest (cells, ports, AP, battery, slots and uses, reveal, free
//    placements, ability, ticks, patterns made, wasted AP, RNG) and the events (the AI's chosen input among them). Also the
//    port sets the C# builds from the engines' printed rows against the page's PORT_SETS, and slip destinations to 1e-9.
// 4. Negative controls: the page's core re-run with a planted change MUST disagree. A gate that cannot see them is not a gate.
'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');
let chromium;
try { ({ chromium } = require('playwright')); } catch (e) { chromium = require(path.join(process.execPath, '../../lib/node_modules/playwright')).chromium; }

const ROOT = path.resolve(__dirname, '../../..');
const PAGE = path.join(ROOT, 'Docs/Studios/HoneyguideLab.html');

// Runs INSIDE the page. `src` is the core's source (the page's own, or a planted copy); returns scenarios + JS results.
function buildInPage(arg) {
  const L = window.__lab;
  const D = arg.src ? (new Function('root', arg.src.replace(/\}\)\(typeof window[^\n]*\n?\s*$/, '})(root);') + '\nreturn root.DynamoCore;'))({}) : L.core;
  const mul = (a) => () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
  const pats = L.PATTERNS.map((p) => ({ cells: p.cells, parked: !!p.parked }));
  const portSets = L.PORT_SETS.map((s) => s.map((p) => [p[0], p[1]]));
  const ev = (e) => ({ tinker: () => 'T' + e.from + '>' + e.to, refuel: () => 'R' + e.cell + ':' + e.colour, place: () => 'P' + e.cell + ':' + e.colour,
    ability: () => 'A' + e.slot + ':' + e.pid + ':' + e.use, charge: () => 'C' + e.colour, replaced: () => 'X' + e.colour,
    pick: () => 'K' + e.slot + ':' + e.pid + ':' + e.cell + ':' + e.colour, retire: () => 'Q' + e.slot + ':' + e.old + ':' + e.pid,
    shuffle: () => 'S' + e.ability + ':' + e.portSet, ports: () => 'p' + e.portSet, resume: () => 'r', effect: () => 'F' + e.kind + ':' + e.n,
    tick: () => 't' + e.n + ':' + e.ap })[e.k]();
  const inpStr = (i) => (i ? 'i' + i.k + ':' + (i.k === 'tinker' ? i.from + '>' + i.to : i.k === 'refuel' ? i.cell + ':' + (i.colour || 0) : i.slot) : 'i-');
  const KINDS = ['burn', 'purge', 'free', 'bonus', 'rewind', 'unspin', 'trio', 'scatter', 'charge', 'drain', 'drainOne'];
  const ABIL = D.ABILITIES;
  const rules = [];
  for (let v = 0; v < 24; v++) rules.push({
    tickSeconds: arg.tick || [15, 8, 4][v % 3], apPerTick: 3 + (v % 4 === 3 ? 3 : 0), apBank: 3 + (v % 5 === 4 ? 3 : 0), bonusApCap: 2, batteryCap: 3 + (v % 6 === 5 ? 3 : 0),
    revealAbility: v % 4 === 1 ? 0 : 3.5, revealPorts: v % 7 === 2 ? 0 : 2, allowRotatedMatches: v % 8 !== 7, portsCount: v % 9 !== 8, slotCount: 3,
    startFilled: v % 3 !== 2, usesPerPattern: v % 10 === 9 ? 0 : 3, patternsPerShuffle: v % 11 === 10 ? 0 : v % 5 === 3 ? 1 : 4,
    fixedAbility: v < ABIL.length ? ABIL[v] : '', firstPortSet: v % 2 ? -1 : (v * 7) % portSets.length,
  });
  const scenarios = [], results = [];
  rules.forEach((R, n) => {
    const S = Object.assign({}, R, { portSets, patterns: pats, fixedAbility: R.fixedAbility, firstPortSet: R.firstPortSet >= 0 ? R.firstPortSet : null });
    const seed = 2000 + n * 13, st = D.create(S, seed), r = mul(seed ^ 0xabcdef), steps = [], rows = [{ ok: true, d: D.digest(st), e: [] }], memo = {};
    for (let k = 0; k < 700; k++) {
      let step;
      const u = r();
      if (u < 0.3) step = { dt: [1 / 30, 1 / 60, 0.25, 0.5, 1.7, 3.1][Math.floor(r() * 6)] };
      else if (u < 0.72) step = { ai: { top: [1, 2, 4][Math.floor(r() * 3)], blunder: [0, 0.08, 0.25][Math.floor(r() * 3)] }, seed: Math.floor(r() * 2147483647) };
      else if (u < 0.82) { const all = D.legalInputs(st); step = all.length ? { inp: all[Math.floor(r() * all.length)] } : { dt: 0.2 }; }
      else if (u < 0.88) step = { inp: { k: ['tinker', 'refuel', 'activate'][Math.floor(r() * 3)], from: Math.floor(r() * 40) - 1, to: Math.floor(r() * 40) - 1, cell: Math.floor(r() * 40) - 1, slot: Math.floor(r() * 4) - 1, colour: Math.floor(r() * 4) } };
      else if (u < 0.94) step = { spend: 1 + Math.floor(r() * 3) };
      else { const kind = KINDS[Math.floor(r() * KINDS.length)]; step = { fx: kind, arg: kind === 'purge' || kind === 'charge' ? 1 + Math.floor(r() * 3) : kind === 'rewind' ? 5 : Math.floor(r() * 4) }; }
      steps.push(step);
      let ok = true;
      const pre = [];
      if (step.dt != null) D.advance(st, S, step.dt);
      else if (step.spend != null) ok = D.spend(st, step.spend);
      else if (step.fx != null) ok = D.boardEffect(st, S, step.fx, step.arg) >= 0;
      else if (step.ai != null) {
        const inp = D.aiPlan(st, S, step.ai, [0, 0, 0, 0], mul(step.seed), memo);
        if (inp && inp.k === 'activate') memo.key = '';
        pre.push(inpStr(inp));
        ok = inp ? D.apply(st, S, inp) : false;
      } else ok = D.apply(st, S, step.inp);
      rows.push({ ok, d: D.digest(st), e: pre.concat(st.events.splice(0).map(ev)) });
    }
    scenarios.push({ S: Object.assign({}, R, { patterns: pats, portSets }), seed, steps }); results.push(rows);
  });
  // slip cases: random-walk trails, contacts, positions, headings and reaches
  const slips = [], slipOut = [], r = mul(4242);
  for (let k = 0; k < 400; k++) {
    const n2 = 2 + Math.floor(r() * 60), pts = [];
    let x = r() * 400 - 200, z = r() * 400 - 200, h = r() * 6.283;
    for (let i = 0; i < n2; i++) { pts.push([x, k % 5 === 0 ? r() * 10 : 0, z]); h += (r() - 0.5) * 0.6; x += Math.cos(h) * 7; z += Math.sin(h) * 7; }
    const a = r() * 6.283, c = { pts, contact: Math.floor(r() * n2), pos: [r() * 400 - 200, 0, r() * 400 - 200], fwd: [Math.cos(a), 0, Math.sin(a)], reach: 20 + r() * 500, exit: r() * 20 };
    slips.push(c); slipOut.push(D.slipDestination(c.pts, c.contact, c.pos, c.fwd, c.reach, c.exit));
  }
  return { scenarios, results, slips, slipOut, portSets };
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
  // the engines' printed rows, in the page's ENGINES order (the C# builds the port sets from these)
  const engines = [...html.matchAll(/^\s+(\w+): \{ label: '[^']*', ports: parseEngine\((\[[^\]]*\])\) \},?$/gm)].map((x) => JSON.parse(x[2].replace(/'/g, '"')));
  if (engines.length !== 4) { console.error(`FAIL: found ${engines.length} engines in the page, not 4 (the ENGINES table moved; update the regex)`); process.exit(1); }
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.goto('file://' + PAGE);
  await page.waitForFunction(() => window.__lab && window.__lab.core);
  const ref = await page.evaluate(buildInPage, {});
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'dynamo-parity-'));
  const scnFile = path.join(tmp, 'scenarios.json');
  fs.writeFileSync(scnFile, JSON.stringify({ scenarios: ref.scenarios, slips: ref.slips, engines }));
  const cs = JSON.parse(execFileSync('bash', [path.join(__dirname, 'trace.sh'), scnFile], { cwd: ROOT, maxBuffer: 1 << 30, stdio: ['ignore', 'pipe', 'inherit'] }).toString());
  const res = compare(ref, cs);
  let fail = false;
  const count = (pred) => ref.results.reduce((s, rows) => s + rows.reduce((t, r) => t + r.e.filter(pred).length, 0), 0);
  const acts = count((e) => e[0] === 'A'), picks = count((e) => e[0] === 'K'), ticks = count((e) => e[0] === 't'), shuffles = count((e) => e[0] === 'S'),
    retires = count((e) => e[0] === 'Q'), effects = count((e) => e[0] === 'F'), aiMoves = count((e) => e[0] === 'i' && e !== 'i-'), places = count((e) => e[0] === 'P');
  const illegal = ref.results.reduce((s, rows) => s + rows.filter((r) => !r.ok).length, 0);
  const portsSame = JSON.stringify(cs.portSets) === JSON.stringify(ref.portSets);
  if (!portsSame) { fail = true; console.log(`FAIL: the C# BuildPortSets makes ${cs.portSets.length} port sets, the page ${ref.portSets.length}, or their order differs`); }
  if (res.bad || res.slipBad) {
    fail = true;
    console.log(`FAIL: ${res.bad} of ${res.steps} steps and ${res.slipBad} of ${res.slips} slips differ`);
    if (res.first) console.log(JSON.stringify(res.first, null, 1).slice(0, 2400));
    if (res.slipFirst) console.log(JSON.stringify(res.slipFirst, null, 1).slice(0, 2000));
  } else {
    console.log(`PASS: ${res.steps} steps over ${ref.scenarios.length} rule sets, ${aiMoves} Engineer-AI moves chosen alike, ${acts} patterns made, ${picks} battery picks, ${retires} retired, ${shuffles} new abilities + port patterns, ${ticks} spins, ${effects} board effects, ${places} free placements, ${illegal} illegal inputs refused alike; ${ref.portSets.length} port sets built alike from the engines; ${res.slips} slip destinations equal to 1e-9`);
  }
  if (acts < 300 || shuffles < 30 || retires < 30 || aiMoves < 1000 || places < 10) { fail = true; console.log(`FAIL: the scenarios exercised too little (${acts} made, ${shuffles} shuffles, ${retires} retired, ${aiMoves} AI moves, ${places} free placements)`); }
  // negative controls
  const plants = [
    ['ports turn counter-clockwise', (s) => s.replace('for (const p of st.ports) p[0] = rot60(p[0]);\n    const over', 'for (const p of st.ports) p[0] = rot60(rot60(rot60(rot60(rot60(p[0])))));\n    const over'), {}],
    ['placement scan reversed', (s) => s.replace('      for (let a = 0; a < N; a++) {\n        const cells = [];', '      for (let a = N - 1; a >= 0; a--) {\n        const cells = [];'), {}],
    ['AI ties broken the other way', (s) => s.replace('x.total - y.total || x.pid - y.pid', 'x.total - y.total || y.pid - x.pid'), {}],
    ['the spin clock keeps its time after a reveal', (s) => s.replace("st.reveal = null; st.clock = 0;\n    st.events.push({ k: 'resume' });", "st.reveal = null;\n    st.events.push({ k: 'resume' });"), {}],
    ['Square-Peg scatter lands on ports too', (s) => s.replace('pool = e.length ? e : empty();', 'pool = empty();'), {}],
    ['spin 15 s -> 15.25 s', (s) => s, { tick: 15.25 }],
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
