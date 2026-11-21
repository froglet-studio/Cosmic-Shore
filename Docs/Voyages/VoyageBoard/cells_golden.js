// Prints the Voyage Board's cell numbers (the wet foam of §4d) that HyperSeaCellsTests holds as goldens (Docs/Voyages/VOYAGE_BOARD.md §4c).
// Usage: node Docs/Voyages/VoyageBoard/cells_golden.js
// It runs deepzoom/cells.js over the board's own model (the board's canvases and state stubbed, as sky_golden.js does)
// and prints the nucleus of the cell a point is in, the slice's snapping, the size fades, a few plane and upright
// sections polygon by polygon, the nuclei a slice crosses, and the nucleus whose levels a view names.
"use strict";
const fs = require("fs"), path = require("path");
const html = fs.readFileSync(path.join(__dirname, "voyage-board.html"), "utf8");
const cellsSrc = fs.readFileSync(path.join(__dirname, "deepzoom/cells.js"), "utf8");
const from = html.indexOf("// ── Canon numbers"), to = html.indexOf("// ── Crew (voyage_slice.json)");
if (from < 0 || to < 0) throw new Error("the board's model markers moved: update cells_golden.js");
const model = html.slice(from, to).replace("const LANES_ON = true;", "const LANES_ON = false;");
const ret = "return { A, NUC_R,";
if (!cellsSrc.includes(ret)) throw new Error("cells.js's return moved: update cells_golden.js");
const cellsOpen = cellsSrc.replace(ret, "return { memAlpha, nucAlpha, G, polyOf, sitesNear, sitesCrossing, wetOutline, A, NUC_R,");
const canvas = { getContext: () => ({}), width: 0, height: 0 };
const document = { getElementById: () => null, createElement: () => canvas };
const shims = "const S = { sailing: null }; let SH = 120, sideView = null, fieldView = null, needs = false;\n" +
  "const scale = () => W / view.w, schedule = () => {}, esc = s => String(s), fmt = n => String(n), dist = ly => `${ly} ly`,\n" +
  "  zText = z => String(z), arkPos = () => [0, 0, 0];\nconst DZ = { focusZ: () => focus };\nlet focus = 0;\n";
const m = new Function("document", "let view = { cx: 0, cy: 0, w: 1000 }; const W = 1000, H = 600;\n" + model + "\n" + shims +
  cellsOpen + "\nreturn { CELLS, setFocus: z => { focus = z; }, setView: (cx, cy, w) => { view.cx = cx; view.cy = cy; view.w = w; } };")(document);
const C = m.CELLS;
const f = v => Number(v).toPrecision(15);
console.log("origin", C.G.map(f).join(" "));
for (const [x, y, z] of [[0, 0, 0], [7, -3, -5], [9.9, 9.9, 4.9], [12, 3, 2], [-31, 47, 18], [900, 2100, 30], [-14.2, 26003.4, 65.5], [5, 5, 5]])
  console.log("cellAt", x, y, z, C.cellAt(x, y, z).map(f).join(" "));
for (const z of [-5, 0, 30, 34.9, 65, -17.6, 600, -620]) { m.setFocus(z); m.setView(0, 0, 100); console.log("slice", z, f(C.sliceZ())); }
for (const px of [10, 22, 30, 44, 60, 100]) console.log("fade", px, f(C.memAlpha(px)), f(C.nucAlpha(px)));
const dot3 = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
// a section: its corners, the nucleus position's projection and height, and the middle's (where the nucleus is drawn)
// with the nucleus's radius
const sec = (c, o, u, v) => { const s = C.section(c, o, u, v); if (!s) return "none"; C.polyOf(s.site); const N = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]], e = [s.site.c[0] - o[0], s.site.c[1] - o[1], s.site.c[2] - o[2]];
  return `${s.poly.length} ${f(s.cu)} ${f(s.cv)} ${f(s.h)} ${f(dot3(e, u))} ${f(dot3(e, v))} ${f(dot3(e, N))} ${f(s.site.nucR)} | ${s.poly.map(p => f(p[0]) + "," + f(p[1])).join(" ")}`; };
const H = [0, 1, 0], X = [1, 0, 0], Y = [0, 1, 0], Z = [0, 0, 1];
for (const [c, o, u, v] of [
  [[0, 0, -5], [0, 0, -5], X, Y], [[0, 0, -5], [0, 0, 0], X, Y], [[10, 10, 5], [0, 0, 0], X, Y], [[10, 10, 5], [0, 0, -5], X, Y],
  [[0, 0, -5], [0, 0, 9], X, Y], [[20, 0, -5], [0, 0, 7.5], X, Y], [[0, 0, -5], [0, 3, 0], X, Z], [[10, 10, 5], [0, 4, 0], X, Z],
  [[900, 2100, 35], [0, 0, 35], X, Y], [[0, 0, -5], [0, 0, 20], X, Y]])
  console.log("section", c.join(","), o.join(","), u.join(","), v.join(","), sec(c, o, u, v));
void H;
// how many cells some lattice points hold (a knot is more than one), and their nuclei
for (const [x, y, z] of [[0, 0, -5], [40, 20, 15], [-60, 80, -25], [900, 2100, 35], [130, -70, 5]]) {
  const near = C.sitesNear(x, y, z, 0); console.log("near", x, y, z, near.length, near.map(s => s.key + "@" + s.p.map(f).join(",")).join(" "));
}
// the nuclei a slice may cross over a box, counted, with the first and last
{ const all = C.sitesCrossing(-30, 30, -30, 30, 0); console.log("crossing", all.length, all[0].p.map(f).join(","), all[all.length - 1].p.map(f).join(",")); }
// the cell named at a point of a view
for (const [x, y, zs] of [[1, 1, -5], [33, -12, 5], [900, 2100, 35]]) {
  let best = null, bd = Infinity; for (const s of C.sitesNear(x, y, zs, 1)) { const d = (s.p[0] - x) ** 2 + (s.p[1] - y) ** 2 + 4 * (s.p[2] - zs) ** 2; if (d < bd) { bd = d; best = s; } }
  console.log("nearest", x, y, zs, best.p.map(f).join(" "));
}
// a wet outline: the section of the Heart's cell at its own height, pulled in and with its corners kept (the page rounds
// them as it draws)
{ const s = C.section([0, 0, -5], [0, 0, -5], X, Y), o = C.wetOutline(s.poly, .5 * .035 * C.A, .5 * .24 * C.A); console.log("wet", o.pts.length, o.pts.map(p => f(p[0]) + "," + f(p[1])).join(" ")); }
