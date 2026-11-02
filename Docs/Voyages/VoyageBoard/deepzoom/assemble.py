"""Assemble voyage-board.html = the pre-deep-zoom board + deepzoom.js + realsky.json (embedded)."""
import sys, os
D = os.path.dirname(os.path.abspath(__file__))
src = open(f"{D}/voyage-board.before-deepzoom.html").read()
js = open(f"{D}/deepzoom.js").read()
lanes = open(f"{D}/lanes.js").read()
if os.path.exists(f"{D}/realsky.json"):
    data = open(f"{D}/realsky.json").read()
else:  # no fresh build_data.py output: reuse the catalogue already embedded in the committed board
    built = open(f"{D}/../voyage-board.html").read()
    tag = '<script type="application/json" id="realsky">'
    i = built.index(tag) + len(tag)
    data = built[i:built.index("</script>", i)]
def rep(a, b, n=1):
    global src
    c = src.count(a)
    assert c == n, f"expected {n} of: {a[:80]!r}, found {c}"
    src = src.replace(a, b)

# zoom range: down to a few ly
rep("view.w = Math.max(260, Math.min(140000, w));", "view.w = Math.max(4, Math.min(140000, w));")
rep("to: { cx, cy, w: Math.max(260, Math.min(140000, w)) }, t0: performance.now(), ms };",
    "to: { cx, cy, w: Math.max(4, Math.min(140000, w)) }, t0: performance.now(), ms };")
# long zooms keep the target steady on screen: the centre moves with 1/width, not linearly
rep("    setView(anim.from.cx + (anim.to.cx - anim.from.cx) * e, anim.from.cy + (anim.to.cy - anim.from.cy) * e, Math.exp(lw));",
    """    const w0 = anim.from.w, w1 = anim.to.w, wt = Math.exp(lw), far = Math.max(w0 / w1, w1 / w0) > 6;
    const ce = !far ? e : w1 < w0 ? (1 / wt - 1 / w0) / (1 / w1 - 1 / w0) : (wt - w0) / (w1 - w0);
    setView(anim.from.cx + (anim.to.cx - anim.from.cx) * ce, anim.from.cy + (anim.to.cy - anim.from.cy) * ce, wt);""")
# stars and clusters under the routes and places
rep("""  const s = scale();
  const cellPx = CELL * s;
  // past voyages""", """  const dz = DZ.draw();
  const s = scale();
  const cellPx = CELL * s;
  // past voyages""")
rep("""    labels.push({ p, sx, sy, sz, pri: propDest.has(p.id) ? 3 : p.id === S.here ? 4 : big ? 2 : p.region === "far" ? 0 : 1 });
  });""", """    labels.push({ p, sx, sy, sz, pri: propDest.has(p.id) ? 3 : p.id === S.here ? 4 : big ? 2 : p.region === "far" ? 0 : 1 });
  });
  dz.labels.forEach(l => labels.push(l));
  dz.hits.forEach(h => hits.push(h));""")
rep("""    ctx.font = (big ? "600 12.5px" : "600 11.5px") + " 'Barlow Semi Condensed', 'Arial Narrow', sans-serif";""",
    """    ctx.font = (big ? "600 12.5px" : L.small ? "500 10.5px" : "600 11.5px") + " 'Barlow Semi Condensed', 'Arial Narrow', sans-serif";""")
rep("""    if (clash(bx)) { const left = L.sx - L.sz - 6 - bw; if (!clash(left)) bx = left; else if (L.pri < 3) return; }""",
    """    if (clash(bx)) { const left = L.sx - L.sz - 6 - bw; if (!clash(left)) bx = left; else if (L.pri < 3) return; }
    if (L.small) { boxes.push([bx, by, bw, bh]); ctx.fillStyle = "rgba(5,9,18,.5)"; ctx.fillRect(bx - 2, by, bw, bh); ctx.fillStyle = L.col; ctx.fillText(text, bx + 1, L.sy + .5); return; }""")
# taps and hovers prefer places, then stars and clusters
rep("function hitAt(x, y) { let best = null; hits.forEach(h => { const d = Math.hypot(h.sx - x, h.sy - y); if (d < h.r && (!best || d < best.d)) best = { ...h, d }; }); return best; }",
    "function hitAt(x, y) { let best = null; hits.forEach(h => { const d0 = Math.hypot(h.sx - x, h.sy - y), d = d0 + (h.star ? 8 : 0); if (d0 < h.r && (!best || d < best.d)) best = { ...h, d }; }); return best; }")
rep("""  if (!h) { if (S.inspect) { S.inspect = null; renderInsp(); needs = true; } return; }
  if (h.prop !== undefined) { selectProp(h.prop); return; }""", """  if (!h) { if (S.inspect || DZ.sel) { S.inspect = null; DZ.sel = null; renderInsp(); needs = true; } return; }
  if (h.prop !== undefined) { selectProp(h.prop); return; }
  if (h.star) { S.inspect = null; DZ.sel = h.star; renderInsp(); needs = true; return; }
  DZ.sel = null;""")
rep("""  if (h && h.p) {
    const p = h.p;""", """  if (h && h.star) html = DZ.tipHTML(h.star);
  else if (h && h.p) {
    const p = h.p;""")
rep("""  if (!S.inspect) { el.hidden = true; return; }
  const p = BY[S.inspect], here = BY[S.here];""", """  if (DZ.sel) { el.hidden = false; el.innerHTML = DZ.inspHTML(DZ.sel); return; }
  if (!S.inspect) { el.hidden = true; return; }
  const p = BY[S.inspect], here = BY[S.here];""")
# readout: the tier ladder
rep("""  el.innerHTML = `<span>${dist(nice)}${cells >= 1 && cells <= 400 ? ` · ${fmt(cells)} cells · ${duration(cells * MIN_PER_CELL)}` : ""}</span><div class="bar" style="width:${px.toFixed(0)}px"></div>`;""",
    """  const vs = DZ.last, tier = DZ.tierOf(view.w);
  const slab = vs && vs.slab ? ` · a slice ±${dist(vs.zwin)} thick, centred ${zText(vs.zf)}` : "";
  el.innerHTML = `<span class="tier">${tier.name}<em>${esc(tier.what)}${slab}</em></span><span>${dist(nice)}${cells >= 1 && cells <= 400 ? ` · ${fmt(cells)} cells · ${duration(cells * MIN_PER_CELL)}` : ""}</span><div class="bar" style="width:${px.toFixed(0)}px"></div>`;
  document.querySelectorAll("#ladder button").forEach(b => b.setAttribute("aria-current", b.dataset.t === tier.name ? "true" : "false"));""")
rep("""  if (!S.crewEye) return fmt(ly) + " ly";""", """  if (!S.crewEye) return (ly < 9.95 ? ly.toFixed(1) : fmt(ly)) + " ly";""")
# controls
rep("""          <button class="tb" id="v-ark">Ark</button>""", """          <button class="tb" id="v-sol" title="Dive from wherever you are down to Sol's star system">Sol</button>
          <button class="tb" id="v-tum" title="Dive down to Tumult's neighbourhood">Tumult</button>
          <button class="tb" id="v-ark">Ark</button>""")
rep("""        <div class="banner" id="banner" hidden></div>""", """        <nav class="ladder" id="ladder" aria-label="Zoom tiers"></nav>
        <div class="banner" id="banner" hidden></div>""")
rep("""$("v-ark").addEventListener("click", fitBoard);""", """$("v-ark").addEventListener("click", fitBoard);
const dive = (x, y, w) => { const ratio = Math.abs(Math.log(view.w / w)) + Math.hypot(view.cx - x, view.cy - y) / view.w * .3; flyTo(x, y, w, Math.min(5200, 700 + 380 * ratio)); };
$("v-sol").addEventListener("click", () => dive(BY.sol.x, BY.sol.y, 12));
$("v-tum").addEventListener("click", () => dive(BY.tumult.x, BY.tumult.y, 12));
$("ladder").innerHTML = DZ.TIERS.map(t => `<button data-t="${t.name}" data-w="${t.w}" title="${esc(t.what)}">${t.name}</button>`).join("");
$("ladder").addEventListener("click", e => { const b = e.target.closest("button"); if (!b) return; const w = +b.dataset.w; dive(view.cx, view.cy, w > 50000 ? Math.max(110000, 110000 * W / H) : w); });""")
rep("""$("v-gal").addEventListener("click", () => flyTo(0, 4000, Math.max(110000, 110000 * W / H)));""",
    """$("v-gal").addEventListener("click", () => dive(0, 4000, Math.max(110000, 110000 * W / H)));""")
# styles
rep(""".readout .bar {""", """.readout .tier { font: 600 12px var(--f-label); letter-spacing: .1em; text-transform: uppercase; color: var(--fg); display: grid; gap: 1px; max-width: min(420px, calc(100vw - 60px)); }
.readout .tier em { font: 500 11px var(--f-mono); letter-spacing: 0; text-transform: none; font-style: normal; color: rgba(214,224,240,.72); }
.ladder { position: absolute; left: 10px; top: 10px; display: flex; flex-direction: column; gap: 3px; }
.ladder button { font: 600 10.5px var(--f-label); letter-spacing: .08em; text-transform: uppercase; text-align: left; background: rgba(10,17,32,.78); color: rgba(214,224,240,.75);
  border: 1px solid var(--line); border-radius: 6px; padding: 3px 8px; cursor: pointer; min-height: 24px; }
.ladder button:hover { color: var(--fg); border-color: var(--muted); }
.ladder button[aria-current="true"] { color: var(--void); background: var(--fg); border-color: var(--fg); }
@media (max-width: 560px) { .ladder { top: 88px; } .ladder button { font-size: 9.5px; padding: 2px 6px; min-height: 22px; } .readout .tier em { display: none; } }
.readout .bar {""")
# lanes: courses cruise at a height set by their heading [A]
rep("function measure(keys, z0 = 0, z1 = 0) {", lanes + "\nfunction measure(keys, z0 = 0, z1 = 0) {")
rep("""  const flat = keys.slice(1).map(k => { const [q, r] = unKEY(k); const [x, y] = hexXY(q, r); return { q, r, x, y }; });""",
    """  const flat = keys.slice(1).map(k => { const [q, r] = unKEY(k); const [x, y] = hexXY(q, r); return { q, r, x, y }; });
  const [qa, ra] = unKEY(keys[0]), [xa, ya] = hexXY(qa, ra), fe = flat[flat.length - 1] || { x: xa, y: ya };
  const lane = laneFor(xa, ya, fe.x, fe.y, z0, z1, flat.length);""")
rep("f = flat[Math.min(flat.length - 1, Math.max(0, Math.ceil(t * flat.length) - 1))], z = z0 + (z1 - z0) * t;",
    "f = flat[Math.min(flat.length - 1, Math.max(0, Math.ceil(t * flat.length) - 1))], z = lane.prof(t);")
rep("  const zs = pts.map((_, i) => z0 + (z1 - z0) * i / Math.max(1, pts.length - 1));", "  const zs = pts.map((_, i) => lane.prof(i / Math.max(1, pts.length - 1)));")
rep("  return { keys, cells, pts, zs,", "  return { keys, lane, cells, pts, zs,")
rep("  sv.z = sv.path.z0 + (sv.path.z1 - sv.path.z0) * t;", "  sv.z = sv.path.lane.prof(t);")
# chart: other crews' lanes, and the chosen course's crossings
rep("""  // proposals
  const sel = S.props[S.sel];""", """  // other crews' lanes, faint
  if (view.w < 9000 && view.w > 120) {
    ctx.setLineDash([1, 4]); ctx.lineWidth = 1; ctx.strokeStyle = "rgba(200,214,236,.3)";
    TRAFFIC.forEach(t => { const [ax, ay] = toScreen(t.pts[0][0], t.pts[0][1]), e = t.pts[t.pts.length - 1], [bx, by] = toScreen(e[0], e[1]); ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx, by); ctx.stroke(); });
    ctx.setLineDash([]);
  }
  // proposals
  const sel = S.props[S.sel];""")
rep("""    if (cellPx > 6) sailPath.cells.forEach(c => { const [sx, sy] = toScreen(c.x, c.y); ctx.beginPath(); ctx.arc(sx, sy, Math.min(3, cellPx * .14), 0, 7); ctx.fillStyle = col; ctx.fill(); });""",
    """    if (cellPx > 6) sailPath.cells.forEach(c => { const [sx, sy] = toScreen(c.x, c.y); ctx.beginPath(); ctx.arc(sx, sy, Math.min(3, cellPx * .14), 0, 7); ctx.fillStyle = col; ctx.fill(); });
    xings(sailPath).forEach(c => { const [sx, sy] = toScreen(c.x, c.y); ctx.beginPath(); ctx.arc(sx, sy, 5, 0, 7); ctx.fillStyle = "rgba(5,9,18,.8)"; ctx.fill(); ctx.strokeStyle = c.same ? "#b3ff59" : c.gap < CELL ? "#ff6f57" : "#ffffff"; ctx.lineWidth = 1.4; ctx.stroke(); });""")
rep("""      <dt>Height</dt>""", """      <dt>Lane</dt><dd>${laneLine(path)}<span class="tag">[A]</span></dd>
      <dt>Height</dt>""")
# side view: taller, stars at their heights, lane levels, every course and lane as a profile, crossings with their depth gap
rep(".side { position: relative; width: 100%; height: 136px;", ".side { position: relative; width: 100%; height: 210px;")
rep("@media (max-width: 1040px) { .side { height: 116px; } }", "@media (max-width: 1040px) { .side { height: 170px; } }")
rep("""    c.drawImage(sideImg, sx, top, v.W * ky, SH * ky);
  }""", """    c.drawImage(sideImg, sx, top, v.W * ky, SH * ky);
  }
  // the stars on the chart, at their heights
  DZ.side.forEach(s => { const y = sideY(s.z, f); if (y < 20 || y > SH) return; const r = Math.max(.5, s.r * .75); c.fillStyle = `rgba(${s.col},${(s.a * .75).toFixed(2)})`; c.fillRect(s.sx - r, y - r, r * 2, r * 2); });
  // the lanes: one height for each heading
  if (LANE_STEP * f.s > 6) {
    c.font = "500 9.5px 'JetBrains Mono', monospace"; c.textAlign = "right"; c.textBaseline = "middle";
    for (let k = 0; k < LANE_N; k++) {
      const y = sideY(LANE_Z(k), f); if (y < 24 || y > SH - 4) continue;
      c.beginPath(); c.moveTo(60, y); c.lineTo(W - 70, y); c.setLineDash([1, 6]); c.strokeStyle = "rgba(170,190,225,.3)"; c.lineWidth = 1; c.stroke(); c.setLineDash([]);
      if (LANE_STEP * f.s > 10) { c.fillStyle = "rgba(170,190,225,.7)"; c.fillText(LANE_SHORT[k] + " lane", W - 6, y); }
    }
  }
  // other crews' lanes and the board's other voyages, as height profiles
  const sideProf = (pts, zs, style, lw) => { c.beginPath(); pts.forEach(([x, y], i) => { const [px] = toScreen(x, y), py = sideY(zs[i], f); i ? c.lineTo(px, py) : c.moveTo(px, py); }); c.strokeStyle = style; c.lineWidth = lw; c.stroke(); };
  if (!(view.w > 15000) && !S.sailing) S.props.forEach((cc, i) => { if (i !== S.sel) sideProf(cc.paths[0].pts, cc.paths[0].zs, cc.home ? "rgba(255,199,64,.5)" : "rgba(228,235,245,.45)", 1.3); });""")
rep("""  // the Ark
  const [ax, ay, az] = arkPos(), [asx] = toScreen(ax, ay), asy = sideY(az, f);""", """  // where the chosen course crosses another lane on the chart: the two heights, and the gap between them
  if (path) xings(path).forEach(x => {
    const [px] = toScreen(x.x, x.y), ya = sideY(x.za, f), yb = sideY(x.zb, f); if (px < 60 || px > W) return;
    const col = x.gap < CELL ? "#ff6f57" : "rgba(255,255,255,.8)";
    c.beginPath(); c.moveTo(px, ya); c.lineTo(px, yb); c.setLineDash([2, 2]); c.strokeStyle = col; c.lineWidth = 1; c.stroke(); c.setLineDash([]);
    c.beginPath(); c.arc(px, yb, 2.4, 0, 7); c.fillStyle = col; c.fill();
    if (Math.abs(yb - ya) > 14) { c.font = "500 9.5px 'JetBrains Mono', monospace"; c.textAlign = "left"; c.textBaseline = "middle"; c.fillStyle = col; c.fillText("↕ " + dist(x.gap), px + 4, (ya + yb) / 2); }
  });
  // the Ark
  const [ax, ay, az] = arkPos(), [asx] = toScreen(ax, ay), asy = sideY(az, f);""")
rep("""  document.getElementById("sidecap").innerHTML = `Side view, true scale <span>disk 1,000 ly · 50 cells · 50 min · Ark ${Math.abs(az) < 10 ? "on the midplane" : (az > 0 ? "+" : "−") + dist(Math.abs(Math.round(az)))}</span>`;""",
    """  const xs = path ? xings(path) : [], minGap = xs.length ? Math.min(...xs.map(x => x.gap)) : 0;
  const laneCap = path ? ` · course on the ${path.lane.short} lane, ${xs.length} crossing${xs.length === 1 ? "" : "s"}${xs.length ? `, closest ${dist(minGap)} apart in depth` : ""}` : "";
  document.getElementById("sidecap").innerHTML = `Side view, true scale <span>disk 1,000 ly · 50 cells · Ark ${Math.abs(az) < 10 ? "on the midplane" : (az > 0 ? "+" : "−") + dist(Math.abs(Math.round(az)))}${laneCap}</span>`;""")
rep("""        <li><b>Crew's eye</b> switches distances""", """        <li><b>Courses keep to lanes by heading.</b> Every course cruises at a height set by which way it heads: toward the centre, with the spin, out to the rim or against it. Courses that cross on the chart pass at different depths. With a voyage chosen, the side view is that voyage's profile: distance along the course across, height up, the sea sliced along the course, the eight lanes as dotted levels, and every lane it crosses marked at its own height with the depth between them.</li>
        <li><b>Crew's eye</b> switches distances""")
rep("""        <li>Places near Tumult are invented""", """        <li>Lanes: eight headings, 40 ly apart, from 140 ly below the midplane to 140 ly above; a course climbs to its lane in its first 30% and down in its last 30%, never more than one layer of cells per cell, so the climb costs no time <span class="t">[A] proposed</span></li>
        <li>Places near Tumult are invented""")
rep("""        <li><b>Voyages run 8 to 55 minutes</b>""", """        <li><b>Courses fly lanes by heading</b>, like aircraft flight levels, so crossing traffic passes above or below. The open question is whether lanes are law (Terminal-kept, with pirates who break them) or just custom.</li>
        <li><b>Voyages run 8 to 55 minutes</b>""")

rep("""  c.fillStyle = "#050912"; c.fillRect(0, 0, W, SH);
  const f = sideFrame();""", """  c.fillStyle = "#050912"; c.fillRect(0, 0, W, SH);
  // with a course chosen or under way, the side view is that course's profile; zoomed out to the galaxy, the disk edge-on
  const pp = S.sailing ? S.sailing.path : S.props[S.sel] && view.w <= 15000 ? S.props[S.sel].paths[S.path] || S.props[S.sel].paths[0] : null;
  if (pp) { drawProfile(c, pp); return; }
  const f = sideFrame();""")

# notes
rep("""        <li><b>Crew's eye</b> switches distances""", """        <li><b>Seven zoom tiers, from the whole galaxy to one star system.</b> Sol and Tumult dive straight down; the ladder on the left jumps a tier. Each tier draws the stars that matter at that size, about a thousand on screen at any zoom. From Neighbourhood down, the chart shows a slice of the disk around one height, so nearby stars are not buried under the whole column.</li>
        <li><b>Real stars near Sol, a model everywhere else.</b> Within 1,600 ly of Sol the stars are the catalog's; past that, and behind the dust round Tumult, stars are drawn from the galaxy model, the same every visit. Near Tumult the brightest carry names Tumult's crews gave them. Tap any star or cluster to read it.</li>
        <li><b>Crew's eye</b> switches distances""")
rep("""        <li>Cloud and bubble densities are standard ISM values""", """        <li>Stars within 1,600 ly of Sol: HYG v4.1 by David Nash / astronexus (Hipparcos, Yale, Gliese), licensed CC BY-SA 4.0; this page's star table is adapted from it and shared under the same licence. Globular clusters: Harris 1996, 2010 edition (McMaster). Open clusters: Hunt &amp; Reffert 2024, A&amp;A 686, A42 (CDS VizieR) <span class="t">[S]</span></li>
        <li>Every other star is drawn from the galaxy model: a disk falling off over 8,500 ly, the arms, the bulge and bar, and the local mix of bright and faint stars; about 9 stars per square ly at Sol, 30 times that near Tumult <span class="t">[M] model</span></li>
        <li>Cloud and bubble densities are standard ISM values""")

# deep-zoom module and data
rep("// ── Rail ──", js + "\n// ── Rail ──")
assert "</script>" not in data
rep("<script>\n(() => {", '<script type="application/json" id="realsky">' + data + "</script>\n<script>\n(() => {")
open(sys.argv[1], "w").write(src)
print("wrote", sys.argv[1], len(src))
