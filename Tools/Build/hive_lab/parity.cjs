#!/usr/bin/env node
// parity.cjs - holds the Ratel Raid Lab's hive core (Docs/Studios/HoneyguideRaidLab.html, the script block id="hive-core",
// window.HiveCore) to the SHIPPED C# HiveCore, step by step - the AI's chains included.
//
//   node Tools/Build/hive_lab/parity.cjs
//
// 1. Opens the lab page in headless Chromium and builds the scenarios THERE with the page's own core: hives made under
//    several rule sets and all three player domains, then played by random thumbs (starts, adds, backtracks, refused
//    cells, danger stings, ends of every length), ticks (stun, time-outs, stuck) and AI chains (the solver's plan, the
//    rookie, sloppy thumbs); random boards for the planar crystal and the reward placement; random cell sets for the
//    solver, anyChain and chainsNow. The page's core records every result and the hive's digest after each step.
// 2. Runs trace.sh: the shipped HiveCore.cs, compiled from its real path, replays the same steps.
// 3. Compares every string exactly.
// 4. Negative controls: the page's core re-run with a planted change MUST disagree. A gate that cannot see them is not a gate.
'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');
let chromium;
try { ({ chromium } = require('playwright')); } catch (e) { chromium = require(path.join(process.execPath, '../../lib/node_modules/playwright')).chromium; }

const ROOT = path.resolve(__dirname, '../../..');
const PAGE = path.join(ROOT, 'Docs/Studios/HoneyguideRaidLab.html');

// Runs INSIDE the page. `src` is the hive core's source (the page's own, or a planted copy).
function buildInPage(arg) {
  const H = arg.src ? (new Function('root', arg.src.replace(/\}\)\(typeof window[^\n]*\n?\s*$/, '})(root);') + '\nreturn root.HiveCore;'))({ DynamoCore: window.DynamoCore }) : window.HiveCore;
  const D = window.DynamoCore;
  const mul = (a) => () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
  const sol = (s) => (s ? s.map((p) => p.join(',')).join(';') : 'null');
  const base = { minChain: 3, snakeMin: 3, snakeMax: 5, hiveHoles: 2, hiveDangerMin: 2, rewardDynamo: 2, rewardBattery: 1, hiveOtherShare: 0.45, hiveCross: 0.35, hiveArmour: 0.15, stingSeconds: 1, raidSeconds: 15 };
  const variants = [{}, { snakeMax: 7 }, { hiveHoles: 0, hiveDangerMin: 4 }, { hiveCross: 0.8 }, { hiveOtherShare: 0.6, snakeMin: 4 }, { hiveArmour: 0.4, hiveDangerMin: 1 }, { minChain: 2 }, { raidSeconds: 6, stingSeconds: 2.5 }];
  const games = [], gameOut = [];
  const r = mul(arg.seed || 777);
  for (let g = 0; g < 72; g++) {
    const P = Object.assign({}, base, variants[g % variants.length]), own = 1 + (g % 3), seed = 1000 + g * 31;
    const ops = [], out = [];
    const made = H.makeHive(mul(seed), P, own);
    const hive = made && H.create(made, own);
    out.push(hive ? H.digest(hive) + '#' + sol(hive.solution) + '#' + hive.regenerated + '#' + [0, 1, 2, 3].map((c) => hive.total[c] || 0).join(',') : 'null');
    if (hive) {
      const ai = {}; let t = 0;
      const rec = (op, res) => { ops.push(op); out.push(res + '|' + H.digest(hive)); };
      for (let k = 0; k < 160; k++) {
        const u = r();
        if (u < 0.15) { const dt = [0.05, 0.3, 0.9, 2.2][Math.floor(r() * 4)]; t += dt; H.tick(hive, dt); rec({ op: 'tick', dt }, H.endReason(hive, t, P, own)); }
        else if (u < 0.45) {   // an AI chain, then dragged cell by cell
          const op = { op: 'ai', seed: Math.floor(r() * 2147483647), solver: r() < 0.5, sloppy: [0, 0.15, 0.6][Math.floor(r() * 3)] };
          const p = H.aiNextChain(hive, ai, op.solver, op.sloppy, mul(op.seed), P, own);
          rec(op, p ? p.join(',') : 'null');
          if (p) {
            rec({ op: 'start', cell: p[0] }, H.chainStart(hive, p[0], P, own));
            for (let j = 1; j < p.length; j++) rec({ op: 'add', cell: p[j] }, H.chainAdd(hive, p[j], P));
            const e = H.chainEnd(hive, P, own);
            rec({ op: 'end' }, e ? e.colour + ':' + e.broken + ':' + (e.completed ? 1 : 0) + ':' + e.steps.map((s) => s.cell + '/' + (s.broke ? 1 : 0)).join(',') : 'null');
          }
        } else if (u < 0.5) { const cell = Math.floor(r() * 39) - 1; rec({ op: 'start', cell }, H.chainStart(hive, cell, P, own)); }
        else if (u < 0.6) {   // a thumb drag from a live cell of another domain: steps on, and sometimes back
          const live = []; for (let i = 0; i < D.N; i++) if (hive.cells[i].dom && hive.cells[i].dom !== own && !hive.cells[i].danger && hive.cells[i].hits > 0) live.push(i);
          if (live.length) {
            const s0 = live[Math.floor(r() * live.length)];
            rec({ op: 'start', cell: s0 }, H.chainStart(hive, s0, P, own));
            for (let j = 0; j < 5 && hive.chain.length; j++) {
              const ch = hive.chain, end = ch[ch.length - 1];
              const cell = ch.length >= 2 && r() < 0.3 ? ch[ch.length - 2] : D.NEIGH[end][Math.floor(r() * D.NEIGH[end].length)];
              rec({ op: 'add', cell }, H.chainAdd(hive, cell, P));
            }
          }
        }
        else if (u < 0.88) {   // a thumb step: mostly a neighbour of the chain's end (or the cell before it: a backtrack)
          const ch = hive.chain, end = ch.length ? ch[ch.length - 1] : Math.floor(r() * D.N), w = r();
          const cell = ch.length >= 2 && w < 0.25 ? ch[ch.length - 2] : w < 0.85 ? D.NEIGH[end][Math.floor(r() * D.NEIGH[end].length)] : Math.floor(r() * 39) - 1;
          rec({ op: 'add', cell }, H.chainAdd(hive, cell, P));
        } else { const e = H.chainEnd(hive, P, own); rec({ op: 'end' }, e ? e.colour + ':' + e.broken + ':' + (e.completed ? 1 : 0) + ':' + e.steps.map((s) => s.cell + '/' + (s.broke ? 1 : 0)).join(',') : 'null'); }
      }
    }
    games.push({ P, own, seed, ops }); gameOut.push(out);
  }
  // the planar crystal and the reward placement on random boards
  const boards = [], boardOut = [];
  for (let b = 0; b < 300; b++) {
    const dens = [0.1, 0.4, 0.7, 0.95][b % 4], cells = [];
    for (let i = 0; i < D.N; i++) cells.push(r() < dens ? 1 + Math.floor(r() * 3) : 0);
    if (b % 5 === 1) { const perm = H.PERMS[Math.floor(r() * 6)]; for (let i = 0; i < D.N; i++) if (r() < 0.6) cells[i] = perm[H.CLASS[i]]; }
    const rewards = []; const nr = Math.floor(r() * 4); for (let k = 0; k < nr; k++) rewards.push(1 + Math.floor(r() * 3));
    const per = 1 + Math.floor(r() * 3);
    boards.push({ cells: cells.slice(), rewards, per });
    const lc = H.largestCrystal(cells), placed = H.placeRewards(cells, rewards, per), after = placed.crystal, next = H.crystalNext(cells, after);
    boardOut.push(lc.perm.join(',') + '/' + lc.cells.join(',') + '/' + lc.size + '#' + placed.landed.map((x) => x.cell + ':' + x.colour + ':' + x.tier).join(',') + '#' + cells.join(',') + '#' +
      after.perm.join(',') + '/' + after.cells.join(',') + '/' + after.size + '#' + next.map((x) => x[0] + ':' + x[1]).join(','));
  }
  // the solver, anyChain and chainsNow on random cell sets (most unsolvable: the null path is tested too)
  const solves = [], solveOut = [];
  for (let s = 0; s < 220; s++) {
    const own = 1 + (s % 3), minChain = s % 7 === 6 ? 2 : 3;
    let cells = [];
    const h = s % 2 ? H.makeHive(mul(5000 + s), Object.assign({}, base, variants[s % variants.length], { minChain: 3 }), own) : null;
    if (h) {   // a made hive, a few cells changed: mostly still solvable, sometimes not (a planted core may make none)
      cells = h.cells.map((x) => [x.dom, x.hits, x.danger ? 1 : 0]);
      for (let k = Math.floor(r() * 3); k > 0; k--) { const i = Math.floor(r() * D.N); cells[i] = [1 + Math.floor(r() * 3), 1 + Math.floor(r() * 2), 0]; }
    } else for (let i = 0; i < D.N; i++) { const dom = r() < 0.15 ? 0 : 1 + Math.floor(r() * 3); cells.push([dom, dom ? (r() < 0.2 ? 2 : 1) : 0, dom && r() < 0.08 ? 1 : 0]); }
    solves.push({ own, minChain, cells });
    const hc = cells.map((x) => ({ dom: x[0], hits: x[1], danger: !!x[2] }));
    solveOut.push(sol(H.solveHive(hc, minChain, own)) + '#' + (H.anyChain(hc, minChain, own) ? 1 : 0) + '#' + H.chainsNow(hc, minChain, 6, own).slice(0, 40).map((p) => p.join(',')).join(';'));
  }
  return { games, gameOut, boards, boardOut, solves, solveOut };
}

function compare(a, b) {
  let rows = 0, bad = 0, first = null;
  const cmp = (kind, xs, ys) => {
    for (let i = 0; i < xs.length; i++) {
      const x = xs[i], y = ys[i];
      if (Array.isArray(x)) { for (let j = 0; j < x.length; j++) { rows++; if (!y || x[j] !== y[j]) { bad++; if (!first) first = { kind, i, j, page: x[j], csharp: y && y[j] }; } } }
      else { rows++; if (x !== y) { bad++; if (!first) first = { kind, i, page: x, csharp: y }; } }
    }
  };
  cmp('game', a.gameOut, b.games); cmp('board', a.boardOut, b.boards); cmp('solve', a.solveOut, b.solves);
  return { rows, bad, first };
}

(async () => {
  const html = fs.readFileSync(PAGE, 'utf8');
  const m = /<script id="hive-core">([\s\S]*?)<\/script>/.exec(html);
  if (!m) { console.error('FAIL: no <script id="hive-core"> block in ' + PAGE); process.exit(1); }
  const coreSrc = m[1];
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.goto('file://' + PAGE);
  await page.waitForFunction(() => window.HiveCore && window.DynamoCore);
  const ref = await page.evaluate(buildInPage, {});
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'hive-parity-'));
  const scnFile = path.join(tmp, 'scenarios.json');
  fs.writeFileSync(scnFile, JSON.stringify({ games: ref.games, boards: ref.boards, solves: ref.solves }));
  const cs = JSON.parse(execFileSync('bash', [path.join(__dirname, 'trace.sh'), scnFile], { cwd: ROOT, maxBuffer: 1 << 30, stdio: ['ignore', 'pipe', 'inherit'] }).toString());
  const res = compare(ref, cs);
  let fail = false;
  const all = ref.gameOut.flat();
  const made = ref.gameOut.filter((g) => g[0] !== 'null').length;
  const count = (re) => all.filter((s) => re.test(s)).length;
  const stings = count(/^2\|/), ends = all.filter((s) => /^\d:\d+:[01]:/.test(s)).length, completes = all.filter((s) => /^\d:\d+:1:/.test(s)).length;
  const backtracks = count(/^3\|/), aiPaths = all.filter((s, i) => /^\d+(,\d+)+\|/.test(s)).length, reasons = count(/^(clear|time|stuck)\|/);
  const unsolvable = ref.solveOut.filter((s) => s.startsWith('null#')).length, solvable = ref.solveOut.length - unsolvable, onCrystal = ref.boardOut.filter((s) => /:0(,|#)/.test(s.split('#')[1] + '#')).length;
  if (res.bad) {
    fail = true;
    console.log(`FAIL: ${res.bad} of ${res.rows} results differ`);
    console.log(JSON.stringify(res.first, null, 1).slice(0, 3000));
  } else console.log(`PASS: ${res.rows} results alike: ${made} hives made over ${ref.games.length} games (8 rule sets, all three domains), ${ends} chains let go (${completes} completing a colour), ${stings} stings, ${backtracks} backtracks, ${aiPaths} AI chains, ${reasons} hive-mode ends; ${ref.boards.length} crystal boards (${onCrystal} with a reward on the crystal); ${ref.solves.length} solver cases (${solvable} solvable, ${unsolvable} not)`);
  if (made < 60 || stings < 20 || completes < 20 || backtracks < 20 || aiPaths < 200 || unsolvable < 20 || solvable < 40 || onCrystal < 20) { fail = true; console.log(`FAIL: the scenarios exercised too little (${made} hives, ${stings} stings, ${completes} completions, ${backtracks} backtracks, ${aiPaths} AI chains, ${solvable} solvable, ${unsolvable} unsolvable, ${onCrystal} on-crystal boards)`); }
  // negative controls
  const plants = [
    ['a chain of 2 is let go', (s) => s.replace('if (ch.length < Pw.minChain) return null;', 'if (ch.length < Pw.minChain - 1) return null;')],
    ['danger does not sting a start', (s) => s.replace("if (x.danger && x.dom) { sting(hive, Pw); return 2; }\n    if (othersOf(own)", "if (othersOf(own)")],
    ['a shield takes one pass', (s) => s.replace('x.hits--;\n      if (x.hits <= 0)', 'x.hits = 0;\n      if (x.hits <= 0)')],
    ['both colours do not add our own', (s) => s.replace('if (othersOf(own).every((c) => done.indexOf(c) >= 0)) out.push(own);', '')],
    ['the crystal ignores class', (s) => s.replace("if (CLASS[i] !== want) { tiers[2].push(i); continue; }", '')],
    ['rewards sort by index only', (s) => s.replace('tiers[t].sort((a, b) => D.ringOf(a) - D.ringOf(b) || a - b);', 'tiers[t].sort((a, b) => a - b);')],
    ['the solver tries paths of 4 at most', (s) => s.replace('pathsThrough(comp, lowest, minChain, 8)', 'pathsThrough(comp, lowest, minChain, 4)')],
    ['the solver takes the highest standing cell', (s) => s.replace('lowest = standing[0]', 'lowest = standing[standing.length - 1]')],
    ['the rookie picks from the top 5', (s) => s.replace('Math.min(4, all.length)', 'Math.min(5, all.length)')],
    ['no armour', (s) => s.replace('Math.max(1, Math.round(rest.length * Pw.hiveArmour))', 'Math.round(rest.length * Pw.hiveArmour * 0)')],
    ['one danger fewer', (s) => s.replace('for (let d = 0; d < Pw.hiveDangerMin; d++)', 'for (let d = 1; d < Pw.hiveDangerMin; d++)')],
  ];
  for (const [name, plant] of plants) {
    const src = plant(coreSrc);
    if (src === coreSrc) { fail = true; console.log(`FAIL: control "${name}" did not apply (the core's text moved; update the plant)`); continue; }
    const bad = await page.evaluate(buildInPage, { src });
    const r = compare(bad, cs);
    if (r.bad) console.log(`control ${name}: caught (${r.bad} results differ)`);
    else { fail = true; console.log(`FAIL: control ${name} was NOT caught`); }
  }
  await browser.close();
  process.exit(fail ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(1); });
