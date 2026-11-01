// Prints the Voyage Board's own numbers that HyperSeaTests holds as goldens (Docs/Voyages/VOYAGE_BOARD.md §4).
// Usage: node Docs/Voyages/VoyageBoard/golden.js
// It runs the page's sea model (from "Canon numbers" to "Crew") unchanged, with only the six places around Tumult as
// berths, the way the game's VoyageChart charts the slice's locations.
"use strict";
const fs = require("fs"), path = require("path");
const html = fs.readFileSync(path.join(__dirname, "voyage-board.html"), "utf8");
const from = html.indexOf("// ── Canon numbers"), to = html.indexOf("// ── Crew (voyage_slice.json)");
if (from < 0 || to < 0) throw new Error("the board's model markers moved: update golden.js");
const m = new Function(html.slice(from, to) +
  "\nreturn { P, BY, dens, danger, seaName, seaRGB, hash, vnoise, armProfile, fromLD, zOf, hexOf, KEY, unKEY, hexDist, search, trace, measure, quickCost, safeCost, berths };")();
const f = v => Number(v).toPrecision(15);
console.log("hash", f(m.hash(3, 7)), f(m.hash(-5, 12)), f(m.hash(100000, -3)));
console.log("vnoise", f(m.vnoise(1.3, -2.7)), f(m.vnoise(-17.25, 40.5)));
for (const [x, y] of [[900, 2100], [0, 26000], [-12000, 15000], [5000, -3000], [0, 0], [1120, 1520], [40000, 1000], [-3200, 9800], [60000, 0]])
  console.log("dens", x, y, f(m.dens(x, y)), f(m.dens(x, y, 0)), f(m.dens(x, y, 240)), f(m.armProfile(x, y, Math.hypot(x, y), 1100)));
console.log("fromLD", m.fromLD(180.1, 153, -22.3).map(f).join(" "), m.zOf(153, -22.3), m.zOf(650, -57));
for (const n of [0.01, 0.3, 2, 40, 500]) console.log("sea", n, m.seaName(n), f(m.danger(n)), m.seaRGB(n).map(f).join(" "));
console.log("hexOf", m.hexOf(900, 2100), m.hexOf(-1234.5, 777.7), m.hexOf(10, 8.66));
m.berths.clear();
for (const id of ["tumult", "tollmoor", "lantern", "hollow", "verdigris", "saltmarch"]) {
  const p = m.BY[id];
  for (let dq = -3; dq <= 3; dq++) for (let dr = -3; dr <= 3; dr++)
    if (m.hexDist([0, 0], [dq, dr]) <= 3) m.berths.add(m.KEY(p.hex[0] + dq, p.hex[1] + dr));
}
const legs = [["tumult", "tollmoor"], ["tumult", "lantern"], ["lantern", "verdigris"], ["hollow", "saltmarch"], ["tollmoor", "saltmarch"], ["verdigris", "saltmarch"]];
for (const safe of [false, true]) for (const [a, b] of legs) {
  const A = m.BY[a], B = m.BY[b], gk = m.KEY(B.hex[0], B.hex[1]);
  const k = m.trace(m.search(A.hex, safe ? m.safeCost : m.quickCost, 61, gk), gk);
  if (!k) { console.log("leg", a, b, safe ? "safe" : "quick", "none"); continue; }
  const me = m.measure(k, A.z, B.z);
  console.log("leg", a, b, safe ? "safe" : "quick", k.length, m.unKEY(k[Math.floor(k.length / 2)]).join(","), me.n, me.layers, me.flatN,
    f(me.maxD), f(me.avgD), f(me.thick.n), me.courseI);
}
