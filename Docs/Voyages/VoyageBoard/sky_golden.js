// Prints the Voyage Board's deep-zoom numbers that HyperSeaSkyTests holds as goldens (Docs/Voyages/VOYAGE_BOARD.md §4b).
// Usage: node Docs/Voyages/VoyageBoard/sky_golden.js
// It runs deepzoom/deepzoom.js over the board's own model (as export_sky.js does), with its internals exported and the
// per-frame tile budget lifted, and prints the luminosity function, the star column, star colours, a few model tiles
// star by star, and the view state (how deep a view draws and its height slab) for a few views.
"use strict";
const fs = require("fs"), path = require("path");
const html = fs.readFileSync(path.join(__dirname, "voyage-board.html"), "utf8");
const dz = fs.readFileSync(path.join(__dirname, "deepzoom/deepzoom.js"), "utf8");
const from = html.indexOf("// ── Canon numbers"), to = html.indexOf("// ── Crew (voyage_slice.json)");
if (from < 0 || to < 0) throw new Error("the board's model markers moved: update sky_golden.js");
// The tiles' random numbers are the board's own rng, defined after the model.
const rngLine = (html.match(/^function rng\(seed\).*$/m) || [])[0];
if (!rngLine) throw new Error("the board's rng moved: update this script");
const model = html.slice(from, to).replace("const LANES_ON = true;", "const LANES_ON = false;") + "\n" + rngLine;
const exportsLine = "return { draw, focusZ, TIERS,";
if (!dz.includes(exportsLine)) throw new Error("deepzoom.js's return moved: update sky_golden.js");
const dzOpen = dz.replace(exportsLine,
  "return { tileStars, sigma, Mof, logF, levelOfF, FL, bvCol, viewState, setBudget: b => { budget = b; }, draw, focusZ, TIERS,");
// The slab's height is the cells' slice (deepzoom/cells.js): the focus height snapped to the nearest nucleus layer, plus the
// slice bar's offset (0 here). cells.js draws through the board's canvases and state, stubbed here; only sliceZ is read.
const cells = fs.readFileSync(path.join(__dirname, "deepzoom/cells.js"), "utf8");
const canvas = { getContext: () => ({}), width: 0, height: 0 };
const document = { getElementById: () => null, createElement: () => canvas };   // no catalogue: the model's own stars only
const shims = "const S = { sailing: null }; let SH = 120, sideView = null, fieldView = null, needs = false;\n" +
  "const scale = () => W / view.w, schedule = () => {}, esc = s => String(s), fmt = n => String(n), dist = ly => `${ly} ly`,\n" +
  "  zText = z => String(z), arkPos = () => [0, 0, 0];\n";
const m = new Function("document", "let view = { cx: 0, cy: 0, w: 1000 }; const W = 1000, H = 600;\n" + model + "\n" + shims + cells +
  "\n" + dzOpen + "\nreturn { DZ, setView: (cx, cy, w) => { view.cx = cx; view.cy = cy; view.w = w; } };")(document);
const D = m.DZ;
const f = v => Number(v).toPrecision(15);

for (const M of [-9, -5.3, 0, 4.83, 7.7, 12, 16]) console.log("logF", M, f(D.logF(M)));
for (const F of [1e-10, 1e-6, 0.003, 0.2, 0.9]) console.log("Mof", F, f(D.Mof(F)), D.levelOfF(F));
for (const L of [-1, 0, 5, 10, 16]) console.log("FL", L, f(D.FL(L)));
for (const [x, y] of [[0, 26000], [900, 2100], [0, 0], [-12000, 15000], [47000, 0], [3000, -1500]]) {
  const s = D.sigma(x, y);
  console.log("sigma", x, y, f(s.all), f(s.bulge));
}
for (const ci of [-0.4, -0.13, 0.42, 0.65, 1.0, 1.55, 2.3]) console.log("bv", ci, D.bvCol(ci));

D.setBudget(1e9);
// tiles: [level, i, j], round Sol (i, j = floor((x + 70000) / size)), Tumult and the arms
const tile = (L, x, y) => { const s = 140000 / (1 << L); return [L, Math.floor((x + 70000) / s), Math.floor((y + 70000) / s)]; };
for (const [L, i, j] of [[0, 0, 0], tile(4, 0, 26000), tile(8, 900, 2100), tile(9, -6000, 18000), tile(12, 900, 2100),
  tile(13, 0, 26000), tile(14, 2400, 30000)]) {
  const t = D.tileStars(L, i, j);
  console.log("tile", L, i, j, t.length);
  t.slice(0, 3).forEach((s, q) => console.log("star", L, i, j, q, f(s.x), f(s.y), f(s.z), f(s.M), f(s.ci), f(s.lvl), s.col,
    s.tumult ? "T" : "-", s.name || "-", s.desig || "-"));
  const named = t.filter(s => s.tumult)[0];
  if (named) console.log("named", L, i, j, f(named.x), f(named.y), f(named.lvl), named.name, named.desig);
}
for (const [cx, cy, w] of [[0, 4000, 110000], [900, 2100, 4500], [0, 26000, 280], [900, 2100, 50], [0, 26000, 10]]) {
  m.setView(cx, cy, w);
  const v = D.viewState();
  console.log("view", cx, cy, w, f(v.Lcut), f(v.zwin), v.slab ? 1 : 0, f(v.zf), D.tierOf(w).name);
}
