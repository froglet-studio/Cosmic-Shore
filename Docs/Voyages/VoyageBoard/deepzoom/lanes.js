// ── Lanes [A]: a course cruises at a height set by its heading ──────────────
// Like flight levels: courses heading the same way share a height, courses heading other ways sit above or below them,
// so lanes that cross on the chart pass at different depths of the disk. Headings are read in the galaxy's own frame
// (toward the centre, with the spin, out to the rim, against the spin), so the rule holds anywhere in the disk.
// A course climbs to its lane over its first 40% and descends over its last 40%, never faster than one layer of cells
// per cell sailed, so the climb never adds a minute; a short course rides as close to its lane as it can reach.
const LANE_N = 8, LANE_STEP = 20;                                   // one cell between neighbouring lanes
const LANE_Z = k => (k - (LANE_N - 1) / 2) * LANE_STEP;             // -70 … +70 ly about the midplane
const LANE_NAMES = ["coreward", "coreward, against the spin", "against the spin", "rimward, against the spin", "rimward", "rimward, with the spin", "with the spin", "coreward, with the spin"];
const LANE_SHORT = ["core", "core·aft", "aft", "rim·aft", "rim", "rim·spin", "spin", "core·spin"];
const RAMP = 0.4;
// [A] proposal. Off, a course climbs straight from start to end height, the game's current rule (golden.js turns it off).
const LANES_ON = true;
function laneFor(xa, ya, xb, yb, z0, z1, n) {
  if (!LANES_ON) { const k = 0; return { k, name: LANE_NAMES[k], short: LANE_SHORT[k], z: (z0 + z1) / 2, cruise: (z0 + z1) / 2, reached: false, off: true, prof: t => z0 + (z1 - z0) * t }; }
  const mx = (xa + xb) / 2, my = (ya + yb) / 2;
  let rel = Math.atan2(yb - ya, xb - xa) - Math.atan2(-my, -mx);
  rel = Math.atan2(Math.sin(rel), Math.cos(rel));
  const k = ((Math.round(rel / (Math.PI / 4)) % 8) + 8) % 8;
  const zl = LANE_Z(k), R = RAMP * n * LAYER;                       // the height a ramp can gain without costing time
  const lo = Math.max(z0, z1) - R, hi = Math.min(z0, z1) + R;
  const linear = lo > hi;                                           // the ends are too far apart in height to add a cruise
  const zc = linear ? (z0 + z1) / 2 : Math.max(lo, Math.min(hi, zl));
  const prof = linear ? t => z0 + (z1 - z0) * t
    : t => t < RAMP ? z0 + (zc - z0) * t / RAMP : t > 1 - RAMP ? zc + (z1 - zc) * (t - 1 + RAMP) / RAMP : zc;
  return { k, name: LANE_NAMES[k], short: LANE_SHORT[k], z: zl, cruise: zc, reached: !linear && Math.abs(zc - zl) < 1, prof };
}

// Traffic: other crews' Arks on the lanes between neighbouring places [A]. Straight lines, the same lane rule.
const TRAFFIC = [];
{
  const ok = p => ["tumult", "sol", "core"].includes(p.region) && p.exp !== "Guarded" && p.exp !== "Far";
  const seen = new Set();
  P.filter(ok).forEach(a => {
    P.filter(b => ok(b) && b !== a && b.region === a.region)
      .map(b => ({ b, d: Math.hypot(b.x - a.x, b.y - a.y) })).filter(o => o.d > 120 && o.d < 1100)
      .sort((u, v) => u.d - v.d).slice(0, 3).forEach(({ b, d }) => {
        const key = [a.id, b.id].sort().join("|"); if (seen.has(key)) return; seen.add(key);
        // each pair runs both ways, so both lanes exist; draw the one leaving the lower id
        [[a, b], [b, a]].forEach(([u, v]) => {
          const lane = laneFor(u.x, u.y, v.x, v.y, u.z, v.z, d / CELL);
          const pts = [], zs = [];
          for (let i = 0; i <= 20; i++) { const t = i / 20; pts.push([u.x + (v.x - u.x) * t, u.y + (v.y - u.y) * t]); zs.push(lane.prof(t)); }
          TRAFFIC.push({ from: u, to: v, pts, zs, lane });
        });
      });
  });
}
// where a course crosses other courses and lanes on the chart, and how far apart in depth they pass
function crossings(path, others) {
  const out = [], P0 = path.pts, Z0 = path.zs, A = P0[0], E = P0[P0.length - 1];
  others.forEach(o => {
    const oA = o.pts[0], oE = o.pts[o.pts.length - 1], near = (p, q) => Math.hypot(p[0] - q[0], p[1] - q[1]) < 60;
    o.sharesA = near(oA, A) || near(oE, A); o.sharesE = near(oA, E) || near(oE, E);
    for (let i = 0; i < P0.length - 1; i++) {
      const [ax, ay] = P0[i], [bx, by] = P0[i + 1];
      for (let j = 0; j < o.pts.length - 1; j++) {
        const [cx, cy] = o.pts[j], [dx, dy] = o.pts[j + 1];
        const den = (bx - ax) * (dy - cy) - (by - ay) * (dx - cx); if (!den) continue;
        const t = ((cx - ax) * (dy - cy) - (cy - ay) * (dx - cx)) / den, u = ((cx - ax) * (by - ay) - (cy - ay) * (bx - ax)) / den;
        if (t < 0 || t > 1 || u < 0 || u > 1) continue;
        const x = ax + (bx - ax) * t, y = ay + (by - ay) * t;
        // lanes leaving or reaching the same harbour fan out from it: near a shared end they are traffic at a port, not a crossing
        const nearA = Math.hypot(x - A[0], y - A[1]), nearE = Math.hypot(x - E[0], y - E[1]);
        if (nearA < 40 || nearE < 40) continue;
        if ((o.sharesA && nearA < 160) || (o.sharesE && nearE < 160)) continue;
        const za = Z0[i] + (Z0[i + 1] - Z0[i]) * t, zb = o.zs[j] + (o.zs[j + 1] - o.zs[j]) * u;
        out.push({ x, y, za, zb, gap: Math.abs(za - zb), o, same: o.lane.k === path.lane.k });
      }
    }
  });
  return out;
}
// the chosen course's crossings with the other voyages on the board and with other crews' lanes (cached per board)
function xings(path) {
  if (path._x && path._xp === S.props) return path._x;
  const mine = S.props.find(c => c.paths.includes(path));
  const others = S.props.filter(c => c !== mine).map(c => ({ pts: c.paths[0].pts, zs: c.paths[0].zs, lane: c.paths[0].lane, label: c.dest.name }))
    .concat(TRAFFIC.map(t => ({ pts: t.pts, zs: t.zs, lane: t.lane, label: `${t.from.name} to ${t.to.name}` })));
  path._x = crossings(path, others); path._xp = S.props;
  return path._x;
}
function laneLine(path) {
  const L = path.lane, all = xings(path), x = all.filter(c => !c.same), n = x.length, merges = all.length - n, min = n ? Math.min(...x.map(c => c.gap)) : 0;
  const climb = L.reached ? "" : ` (its lane is ${zText(L.z)}; a course this short gets only as close as it can without losing time)`;
  const cross = (!n ? "It crosses no other lane on the chart." : `It crosses ${n} lane${n === 1 ? "" : "s"} on the chart, ${min < CELL ? `closest ${dist(min)} apart in depth, inside one cell` : `never closer than ${dist(min)} in depth`}.`)
    + (merges ? ` ${merges === 1 ? "One course heading the same way joins" : merges + " courses heading the same way join"} its lane.` : "");
  return `${L.name[0].toUpperCase() + L.name.slice(1)} lane, cruising ${zText(L.cruise)}${climb}. ${cross}`;
}

// ── The course profile: the side view of one voyage ──────────────────────
// Distance along the course runs left to right; height in the disk runs up. The sea is sliced along the course itself,
// the eight lanes are the dotted levels, and every lane this course crosses on the chart shows as a mark at its own height,
// with the depth between the two. Heights are stretched to fit the panel, and the caption says by how much.
let profCache = null;
function drawProfile(c, path) {
  const pts = path.pts, zs = path.zs, cum = [0];
  for (let i = 1; i < pts.length; i++) cum.push(cum[i - 1] + Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]));
  const Ltot = Math.max(1, cum[cum.length - 1]), X0p = 64, X1p = W - 12, at = d => X0p + d / Ltot * (X1p - X0p);
  const alongPt = d => { let i = 1; while (i < cum.length - 1 && cum[i] < d) i++; const u = (d - cum[i - 1]) / Math.max(1e-6, cum[i] - cum[i - 1]); return [pts[i - 1][0] + (pts[i][0] - pts[i - 1][0]) * u, pts[i - 1][1] + (pts[i][1] - pts[i - 1][1]) * u]; };
  const project = (x, y) => { let best = { d: Infinity }; for (let i = 0; i < pts.length - 1; i++) { const [ax, ay] = pts[i], [bx, by] = pts[i + 1], dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy || 1; const u = Math.max(0, Math.min(1, ((x - ax) * dx + (y - ay) * dy) / l2)); const d = Math.hypot(x - ax - dx * u, y - ay - dy * u); if (d < best.d) best = { d, s: cum[i] + u * Math.sqrt(l2) }; } return best; };
  const xs = xings(path);
  let zmin = Math.min(LANE_Z(0), ...zs, ...xs.map(x => x.zb)) - 25, zmax = Math.max(LANE_Z(LANE_N - 1), ...zs, ...xs.map(x => x.zb)) + 25;
  const top = 30, bot = SH - 18, sv = (bot - top) / (zmax - zmin), Y = z => top + (zmax - z) * sv;
  const stretch = sv / ((X1p - X0p) / Ltot);
  // the sea, sliced along the course (cached per course and panel size)
  const key = path.sig + "|" + W + "|" + SH;
  if (!profCache || profCache.key !== key) {
    const step = 3, fw = Math.ceil((X1p - X0p) / step), fh = Math.ceil((bot - top) / step);
    const cv2 = document.createElement("canvas"); cv2.width = fw; cv2.height = fh;
    const g = cv2.getContext("2d"), img = g.createImageData(fw, fh);
    for (let i = 0; i < fw; i++) {
      const [x, y] = alongPt((i + .5) / fw * Ltot);
      for (let j = 0; j < fh; j++) { const z = zmax - (j + .5) / fh * (zmax - zmin), col = seaRGB(dens(x, y, z)), o = (j * fw + i) * 4; img.data[o] = col[0] * .8; img.data[o + 1] = col[1] * .8; img.data[o + 2] = col[2] * .8; img.data[o + 3] = 255; }
    }
    g.putImageData(img, 0, 0);
    profCache = { key, img: cv2 };
  }
  c.imageSmoothingEnabled = true; c.drawImage(profCache.img, X0p, top, X1p - X0p, bot - top);
  // stars within 60 ly of the course on the chart, at their heights
  DZ.side.forEach(s => { if (s.wx === undefined) return; const p = project(s.wx, s.wy); if (p.d > 150) return; const y = Y(s.z); if (y < top || y > bot) return; const r = Math.max(.6, s.r * .8); c.fillStyle = `rgba(${s.col},${Math.min(1, s.a * (1 - p.d / 190)).toFixed(2)})`; c.fillRect(at(p.s) - r, y - r, r * 2, r * 2); });
  // the eight lanes
  c.font = "500 9.5px 'JetBrains Mono', monospace"; c.textBaseline = "middle"; c.textAlign = "right";
  for (let k = 0; k < LANE_N; k++) {
    const y = Y(LANE_Z(k)), mine = k === path.lane.k;
    c.beginPath(); c.moveTo(X0p, y); c.lineTo(X1p, y); c.setLineDash(mine ? [4, 4] : [1, 6]); c.strokeStyle = mine ? "rgba(179,255,89,.55)" : "rgba(190,205,230,.32)"; c.lineWidth = 1; c.stroke(); c.setLineDash([]);
    c.fillStyle = "rgba(5,9,18,.6)"; const t = LANE_SHORT[k], tw = c.measureText(t).width; c.fillRect(X1p - tw - 6, y - 6, tw + 4, 12);
    c.fillStyle = mine ? "rgba(179,255,89,.95)" : "rgba(190,205,230,.75)"; c.fillText(t, X1p - 4, y);
  }
  // the midplane
  { const y = Y(0); c.beginPath(); c.moveTo(X0p, y); c.lineTo(X1p, y); c.strokeStyle = "rgba(214,224,240,.18)"; c.stroke(); }
  // the course
  const col = path === (S.sailing && S.sailing.path) ? (S.sailing.c.home ? "#ffc740" : "#b3ff59") : (S.props[S.sel] && S.props[S.sel].home ? "#ffc740" : "#b3ff59");
  c.beginPath(); pts.forEach((_, i) => { const x = at(cum[i]), y = Y(zs[i]); i ? c.lineTo(x, y) : c.moveTo(x, y); });
  c.strokeStyle = "rgba(5,9,18,.85)"; c.lineWidth = 5; c.stroke(); c.strokeStyle = col; c.lineWidth = 2.2; c.stroke();
  // crossings: the other lane passes through the page at its own height
  let lastLabel = -1e9;
  xs.slice().sort((a, b) => project(a.x, a.y).s - project(b.x, b.y).s).forEach(x => {
    const d = project(x.x, x.y).s, px = at(d), ya = Y(x.za), yb = Y(x.zb), bad = x.gap < CELL && !x.same, xc = x.same ? "rgba(179,255,89,.9)" : bad ? "#ff6f57" : "rgba(255,255,255,.85)";
    c.beginPath(); c.moveTo(px, ya); c.lineTo(px, yb); c.setLineDash([2, 2]); c.strokeStyle = xc; c.lineWidth = 1; c.stroke(); c.setLineDash([]);
    c.beginPath(); c.arc(px, yb, 3.2, 0, 7); c.fillStyle = "rgba(5,9,18,.9)"; c.fill(); c.strokeStyle = xc; c.lineWidth = 1.5; c.stroke();
    if (px - lastLabel > 46) { c.textAlign = "left"; c.fillStyle = xc; c.fillText(x.same ? "joins" : "↕" + dist(x.gap), px + 4, yb + (yb < ya ? -8 : 8)); lastLabel = px; }
  });
  // ends, and the Ark while sailing
  c.font = "600 11px 'Barlow Semi Condensed', sans-serif"; c.textBaseline = "middle";
  const end = (name, d, z, left) => { const x = at(d), y = Y(z); c.beginPath(); c.arc(x, y, 3.5, 0, 7); c.fillStyle = "#f1f5fb"; c.fill(); const t = `${name} · ${zText(z)}`, tw = c.measureText(t).width, lx = left ? Math.min(x + 6, X1p - tw - 4) : Math.max(X0p + 2, x - tw - 6), ly = Math.max(top + 8, Math.min(bot - 8, y - 12)); c.fillStyle = "rgba(5,9,18,.75)"; c.fillRect(lx - 3, ly - 7, tw + 6, 14); c.fillStyle = "#f1f5fb"; c.textAlign = "left"; c.fillText(t, lx, ly + .5); };
  const sailing = S.sailing && S.sailing.path === path, from = sailing ? null : BY[S.here], dest = (sailing ? S.sailing.c : S.props[S.sel]).dest;
  end(sailing ? "From" : from.name, 0, zs[0], true); end(dest.name, Ltot, zs[zs.length - 1], false);
  if (sailing) { const p = project(S.sailing.pos[0], S.sailing.pos[1]); c.beginPath(); c.arc(at(p.s), Y(S.sailing.z), 5, 0, 7); c.fillStyle = "#ffc740"; c.shadowColor = "rgba(255,199,64,.8)"; c.shadowBlur = 8; c.fill(); c.shadowBlur = 0; }
  // minutes along the bottom
  c.font = "500 9.5px 'JetBrains Mono', monospace"; c.fillStyle = "rgba(214,224,240,.6)"; c.textAlign = "center";
  const mins = path.minutes, every = mins <= 15 ? 5 : mins <= 40 ? 10 : 20;
  for (let m = every; m < mins; m += every) { const x = at(m / mins * Ltot); c.fillRect(x, bot, 1, 4); c.fillText(m + " min", x, bot + 10); }
  // the depth gauge: where in the disk's 1,000 ly the panel's window sits
  const gx = 18, g0 = 26, g1 = SH - 10, gz = z => g1 - (z + 700) / 1400 * (g1 - g0);
  c.fillStyle = "rgba(5,9,18,.85)"; c.fillRect(0, 22, X0p - 4, SH);
  c.fillStyle = "rgba(42,104,184,.35)"; c.fillRect(gx - 4, gz(DISK_HALF), 8, gz(-DISK_HALF) - gz(DISK_HALF));
  c.strokeStyle = "rgba(255,233,168,.6)"; c.lineWidth = 1; c.strokeRect(gx - 4.5, gz(DISK_HALF) + .5, 9, gz(-DISK_HALF) - gz(DISK_HALF) - 1);
  c.strokeStyle = "rgba(228,235,245,.9)"; c.strokeRect(gx - 8.5, gz(zmax), 17, Math.max(2, gz(zmin) - gz(zmax)));
  c.font = "500 9.5px 'JetBrains Mono', monospace"; c.fillStyle = "rgba(214,224,240,.7)"; c.textAlign = "left"; c.fillText("1,000", 32, gz(70)); c.fillText("ly", 32, gz(-50));
  const cr = xs.filter(x => !x.same), minGap = cr.length ? Math.min(...cr.map(x => x.gap)) : 0;
  document.getElementById("sidecap").innerHTML = `Course profile <span>${esc(path.lane.short)} lane · ${cr.length} crossing${cr.length === 1 ? "" : "s"}${cr.length ? `, closest ${dist(minGap)} apart in depth` : ""}${xs.length > cr.length ? ` · ${xs.length - cr.length} joining` : ""} · heights ×${stretch < 9.5 ? stretch.toFixed(1) : fmt(stretch)}</span>`;
}
