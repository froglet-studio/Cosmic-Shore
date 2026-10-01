"""The research viewer's swarm section: one rule, four body plans, played back from rollouts.

build_viewer.py calls build_swarm(results_root). It reads results/swarm_coevo/{rollout,summary}.json
(written by `swarm_nca.py rollout`) and the training log, and returns (section_html, script). The
recorded units are realised in the browser by swarm_model.js, the same generator that builds the
targets, so the grown swarm and its target are drawn by identical code.
"""
import json
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
KINDS = ("mass", "space", "charge", "time")
NAMES = {"mass": "Whale", "space": "Jellyfish", "charge": "Pufferfish", "time": "Dragonfly"}
ELEMENTS = ("Charge", "Mass", "Space", "Time")
MAJOR = {"mass": "Mass", "space": "Space", "charge": "Charge", "time": "Time"}
EL_UI = ["#e8a93a", "#8e6bd8", "#3a7bdc", "#2fb39a"]


def _curve(log_path):
    """Inline SVG: the summed four-plan loss over training (50-point moving mean)."""
    if not os.path.isfile(log_path):
        return ""
    rows = [json.loads(l) for l in open(log_path) if l.strip()]
    if len(rows) < 3:
        return ""
    st = np.array([r["step"] for r in rows], float)
    per = {k: np.array([r[k]["sink"] for r in rows], float) for k in KINDS}
    tot = sum(per.values())
    W, H, L, R, T, B = 640, 220, 48, 16, 14, 30
    k = max(1, min(25, len(tot) // 8))
    sm = lambda a: np.convolve(a, np.ones(k) / k, mode="valid")
    xs = st[k - 1:]
    hi = float(np.ceil(sm(tot).max() / 50) * 50) or 1.0
    sx = lambda s: L + (W - L - R) * s / max(1, st[-1])
    sy = lambda v: T + (H - T - B) * (1 - v / hi)
    out = [f'<svg viewBox="0 0 {W} {H}" role="img" aria-label="Summed four-plan loss over training">']
    for v in np.linspace(0, hi, 5):
        out.append(f'<line x1="{L}" x2="{W-R}" y1="{sy(v):.1f}" y2="{sy(v):.1f}" class="grid"/>'
                   f'<text x="{L-8}" y="{sy(v)+4:.1f}" class="tick" text-anchor="end">{v:.0f}</text>')
    for s in np.linspace(0, st[-1], 5):
        out.append(f'<text x="{sx(s):.1f}" y="{H-10}" class="tick" text-anchor="middle">{int(s)}</text>')
    pts = " ".join(f"{sx(x):.1f},{sy(y):.1f}" for x, y in zip(xs, sm(tot)))
    out.append(f'<polyline points="{pts}" class="series" style="stroke:var(--ink)"/>')
    for kk, c in zip(KINDS, (EL_UI[1], EL_UI[2], EL_UI[0], EL_UI[3])):
        pts = " ".join(f"{sx(x):.1f},{sy(y):.1f}" for x, y in zip(xs, sm(per[kk])))
        out.append(f'<polyline points="{pts}" class="series" style="stroke:{c};stroke-width:1.4;opacity:.85"/>')
    out.append("</svg>")
    return "".join(out)


def _rank(d):
    """Most seedings closest to their own plan first, then the lowest own-plan divergence."""
    cross = json.load(open(os.path.join(d, "summary.json")))["cross"]
    correct = sum(min(cross[k], key=cross[k].get) == k for k in KINDS)
    return (-correct, sum(cross[k][k] for k in KINDS))


def build_swarm(results_root):
    cands = [os.path.join(results_root, n) for n in ("swarm_coevo_gpu", "swarm_coevo")]
    cands = [d for d in cands if os.path.isfile(os.path.join(d, "rollout.json"))]
    if not cands:
        return "", ""
    d = min(cands, key=_rank)
    import prism_render as pr
    roll = json.load(open(os.path.join(d, "rollout.json")))
    summ = json.load(open(os.path.join(d, "summary.json")))
    cross = summ["cross"]
    head = "".join(f'<th scope="col">{NAMES[k]}</th>' for k in KINDS)
    body = []
    correct = 0
    for k in KINDS:
        row = cross[k]
        best = min(row, key=row.get)
        correct += best == k
        cells = "".join(f'<td class="{"win" if k2 == best else ""}{" diag" if k2 == k else ""}">{row[k2]:.1f}</td>' for k2 in KINDS)
        c = summ["census"][k]
        body.append(f'<tr><th scope="row">Seeded mostly {MAJOR[k]}</th>{cells}'
                    f'<td>{c["n"]}/{c["target_n"]}</td><td>{c["deaths"]}</td><td>{c["mutants"]}</td></tr>')
    tabs = "".join(
        f'<button type="button" role="tab" data-swk="{k}" aria-selected="false">'
        f'<span class="swatch" style="background:{EL_UI[ELEMENTS.index(MAJOR[k])]}"></span>{MAJOR[k]} seed</button>' for k in KINDS)
    curve = _curve(os.path.join(d, "log.jsonl"))
    meta = summ.get("meta", {})
    note = meta.get("note", "")
    bench = f"""
  <section class="benchp" aria-labelledby="hswarm" id="swarm">
    <div class="h3dhead">
      <div class="eyebrow">Latest · one rule, four seedings</div>
      <h2 id="hswarm">One rule grows four different creatures</h2>
      <p class="caption">Every particle is a whole tadpole fauna: a heart crystal, a spindle and a prism. Its <strong>element</strong> and <strong>domain</strong> are fixed at birth: an egg is always its parent's domain, and its parent's element but for a rare mutation that survives only if the rule lets it hatch. A single learned rule, seeing only its neighbours, decides where each tadpole swims, whether an egg hatches, how its prism and spindle are shaped (inside its element's identity), and whether a Charge tadpole is plain, danger or shielded. A tadpole that dies leaves a lime crystal. The same rule is seeded four ways, sixteen tadpoles at each target's element mix, and is scored on all four at once. Pick a seed to watch it grow; drag to orbit.</p>
      {f'<p class="caption">{note}</p>' if note else ''}
    </div>
    <div class="bench">
      <div class="dish">
        <div class="tabs swtabs" role="tablist" aria-label="Seed">{tabs}</div>
        <div class="plate plate3d" id="platesw"><canvas id="cvsw" width="560" height="560" aria-label="Grown tadpole swarm, drag to orbit"></canvas><span class="hint">drag to orbit</span></div>
        <label class="row" for="swt">Growth step<input id="swt" type="range" min="0" max="1" value="1"><output id="o-swt">0</output></label>
      </div>
      <div class="panel">
        <dl class="readouts">
          <div><dt>Step</dt><dd id="rsw-step">0</dd></div>
          <div><dt>Tadpoles</dt><dd id="rsw-n">0</dd></div>
          <div><dt>Crystals</dt><dd id="rsw-cr">0</dd></div>
          <div><dt>Shields</dt><dd id="rsw-sh">0</dd></div>
        </dl>
        <div class="buttons">
          <button type="button" class="btn primary" id="bsw-play">Pause</button>
          <button type="button" class="btn" id="bsw-target" aria-pressed="false">Show target</button>
          <button type="button" class="btn" id="bsw-spin" aria-pressed="true">Auto-orbit</button>
        </div>
        <div class="swmix" id="swmix" aria-label="Element mix, grown (top) and target (bottom)"></div>
        <p class="census mono" id="rsw-census" aria-live="off"></p>
      </div>
    </div>
    <div class="record">
      <h3>Does the seed choose the body?</h3>
      <p class="caption">Each grown swarm (rows) scored against every target (columns): Sinkhorn divergence on position, element, domain region, prism, tier, facing and spindle, under the best assignment of domains to regions. Lower is closer; the lowest in each row is marked. {correct} of 4 seedings are closest to their own body plan.</p>
      <div class="tablewrap"><table class="swcross"><thead><tr><th scope="col">Grown from</th>{head}<th scope="col">Tadpoles</th><th scope="col">Crystals</th><th scope="col">Mutant eggs</th></tr></thead><tbody>{''.join(body)}</tbody></table></div>
      {f'<figure class="fig"><figcaption>Training loss: the sum over the four seedings (black) and each plan</figcaption><div class="chart">{curve}</div></figure>' if curve else ''}
    </div>
  </section>"""
    data = {"roll": roll, "cross": cross, "names": NAMES, "major": MAJOR}
    pal = np.round(pr.palette_colours()[:, :3], 4).tolist()
    script = SCRIPTSWARM.replace("/*SWARM_MODEL*/", open(os.path.join(HERE, "swarm_model.js")).read()) \
        .replace("/*SWDATA*/", json.dumps(data, separators=(",", ":"))).replace("/*PALETTE*/", json.dumps(pal)) \
        .replace("/*ELUI*/", json.dumps(EL_UI))
    return bench, script


CSS = """
.swtabs { grid-template-columns: repeat(4, minmax(0, 1fr)) !important; margin-bottom: 10px }
.swtabs button { border-bottom: 0 !important; border-right: 1px solid var(--rule) !important }
.swtabs button:last-child { border-right: 0 !important }
.swmix { display: grid; gap: 4px; margin: 12px 0 }
.swmix .bar { display: flex; height: 12px; border-radius: 6px; overflow: hidden; background: var(--rule) }
.swmix .bar i { display: block; height: 100% }
.swmix .lab { font: 12px var(--mono); color: var(--muted) }
table.swcross td.win { font-weight: 700; color: var(--accent) }
table.swcross td.diag { text-decoration: underline; text-underline-offset: 3px }
"""

SCRIPTSWARM = r"""<script>
(function () {
  if (typeof THREE === 'undefined') return;
  const exports = {}, module = { exports };
  /*SWARM_MODEL*/
  const SM = module.exports && module.exports.realise ? module.exports : window.SwarmModel;
  const D = /*SWDATA*/, PALETTE = /*PALETTE*/, EL_UI = /*ELUI*/, KINDS = ['mass', 'space', 'charge', 'time'];
  const $ = id => document.getElementById(id), cv = $('cvsw');
  const renderer = new THREE.WebGLRenderer({ canvas: cv, antialias: true });
  renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  renderer.outputEncoding = THREE.sRGBEncoding;
  const scene = new THREE.Scene(), camera = new THREE.PerspectiveCamera(30, 1, 0.5, 1500);
  scene.add(new THREE.HemisphereLight(0xffffff, 0x445066, 0.8));
  const sun = new THREE.DirectionalLight(0xffffff, 0.75); sun.position.set(30, 60, 40); scene.add(sun);
  function hull(pts) {
    pts = pts.map(p => new THREE.Vector3(...p).normalize());
    const pos = [], eps = 1e-6;
    for (let i = 0; i < pts.length; i++) for (let j = i + 1; j < pts.length; j++) for (let k = j + 1; k < pts.length; k++) {
      const n = new THREE.Vector3().subVectors(pts[j], pts[i]).cross(new THREE.Vector3().subVectors(pts[k], pts[i]));
      if (n.lengthSq() < 1e-10) continue;
      let a = 0, b = 0;
      for (const q of pts) { const d = n.dot(new THREE.Vector3().subVectors(q, pts[i])); if (d > eps) a++; else if (d < -eps) b++; if (a && b) break; }
      if (a && b) continue;
      (a ? [pts[i], pts[k], pts[j]] : [pts[i], pts[j], pts[k]]).forEach(v => pos.push(v.x, v.y, v.z));
    }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.computeVertexNormals(); return g;
  }
  function perms(base, even) {
    const out = [], P3 = even ? [[0, 1, 2], [1, 2, 0], [2, 0, 1]] : [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];
    for (const b of base) for (const p of P3) for (let s = 0; s < 8; s++) {
      const v = p.map((ix, i) => b[ix] * ((s >> i) & 1 ? -1 : 1));
      if (!out.some(o => Math.abs(o[0] - v[0]) + Math.abs(o[1] - v[1]) + Math.abs(o[2] - v[2]) < 1e-9)) out.push(v);
    }
    return out;
  }
  const PHI = (1 + Math.sqrt(5)) / 2;
  const GEO = [hull(perms([[0, 1, 3 * PHI], [1, 2 + PHI, 2 * PHI], [PHI, 2, 2 * PHI + 1]], true)), new THREE.IcosahedronGeometry(1, 1), new THREE.DodecahedronGeometry(1, 0), hull(perms([[0, 1, 2]], false))];
  const MAXP = 600, MAXS = 600 * 8;
  const mat = new THREE.MeshStandardMaterial({ roughness: 0.45, metalness: 0.06, flatShading: true });
  const gem = new THREE.MeshStandardMaterial({ roughness: 0.2, metalness: 0.1, flatShading: true, emissive: 0x3a4a66 });
  const inst = (geo, m, n) => { const x = new THREE.InstancedMesh(geo, m, n); x.instanceMatrix.setUsage(THREE.DynamicDrawUsage); x.setColorAt(0, new THREE.Color(1, 1, 1)); x.count = 0; scene.add(x); return x; };
  const boxes = inst(new THREE.BoxGeometry(2, 2, 2), mat, MAXP), octas = inst(new THREE.OctahedronGeometry(SM.SHIELD), mat, MAXP), gems = GEO.map(g => inst(g, gem, MAXP));
  const limes = GEO.map(g => inst(g, new THREE.MeshStandardMaterial({ roughness: 0.25, flatShading: true, emissive: 0x2c4a10 }), MAXP));
  const cyl = new THREE.CylinderGeometry(1, 1, 1, 6, 1, true); cyl.translate(0, 0.5, 0);
  const spMesh = new THREE.InstancedMesh(cyl, new THREE.MeshStandardMaterial({ roughness: 0.6, color: new THREE.Color(0.6, 0.72, 0.86).convertSRGBToLinear() }), MAXS);
  spMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage); spMesh.count = 0; scene.add(spMesh);
  const lin = c => new THREE.Color(...c).convertSRGBToLinear();
  const HEART = lin([0.42, 0.62, 1.0]), LIME = lin([0.62, 0.92, 0.22]);
  const M = new THREE.Matrix4(), Q = new THREE.Quaternion(), V = new THREE.Vector3(), Sv = new THREE.Vector3(), UP = new THREE.Vector3(0, 1, 0);

  // decode rollouts
  const R = {};
  for (const k of KINDS) {
    const r = D.roll[k], bin = atob(r.b64), buf = new ArrayBuffer(bin.length), u8 = new Uint8Array(buf);
    for (let i = 0; i < bin.length; i++) u8[i] = bin.charCodeAt(i);
    R[k] = { a: new Int16Array(buf), shape: r.shape, n: r.n, crystals: r.crystals };
  }
  const SC = D.roll.scale, every = Math.max(1, Math.round((D.roll.steps || 240) / Math.max(1, R.mass.shape[0] - 1)));
  const perp = f => { const a = Math.abs(f[1]) < 0.9 ? [0, 1, 0] : [1, 0, 0]; const d = a[0] * f[0] + a[1] * f[1] + a[2] * f[2];
    const v = [a[0] - d * f[0], a[1] - d * f[1], a[2] - d * f[2]], L = Math.hypot(...v) || 1; return v.map(x => x / L); };
  function swarmFrame(k, fi) {
    const r = R[k], [F, N, W] = r.shape, P = Object.assign({}, SM.TARGETS[k].defaults, { slotMap: [0, 1, 2] });
    const out = { crystals: [], prisms: [], spindles: [], shields: 0 }, n = r.n[fi];
    for (let i = 0; i < n; i++) {
      const o = (fi * N + i) * W, g = j => r.a[o + j] / SC[j];
      let f = [g(9), g(10), g(11)]; const L = Math.hypot(...f); f = L > 1e-3 ? f.map(x => x / L) : [1, 0, 0];
      const u = { p: [g(0), g(1), g(2)], f, n: perp(f), elem: g(3), spindle: { len: g(12), bend: g(13), roll: 0, thick: P.spindleThickness },
        prism: { h: [g(5), g(6), g(7)], tier: g(8), slot: g(4), roll: 0 } };
      const z = SM.realise(P, u, i); out.crystals.push(z.crystal); out.prisms.push(z.prism); out.spindles.push(z.spindle);
      if (z.prism.tier === 2) out.shields++;
    }
    return out;
  }
  const TG = {};
  const target = k => TG[k] || (TG[k] = SM.report(k, { slotMap: [0, 1, 2] }));

  function draw(g, off, dead) {
    let nb = 0, no = 0, ns = 0; const ng = [0, 0, 0, 0], nl = [0, 0, 0, 0];
    g.prisms.forEach(q => {
      const R9 = q.R, h = q.h, oct = q.tier === 2, m = oct ? octas : boxes, j = oct ? no++ : nb++;
      M.set(R9[0] * h[0], R9[1] * h[1], R9[2] * h[2], q.p[0] + off[0], R9[3] * h[0], R9[4] * h[1], R9[5] * h[2], q.p[1] + off[1], R9[6] * h[0], R9[7] * h[1], R9[8] * h[2], q.p[2] + off[2], 0, 0, 0, 1);
      m.setMatrixAt(j, M); m.setColorAt(j, lin(PALETTE[q.dom][q.tier]));
    });
    g.crystals.forEach(c => { const e = c.elem, j = ng[e]++; M.makeScale(c.r, c.r, c.r); M.setPosition(c.p[0] + off[0], c.p[1] + off[1], c.p[2] + off[2]); gems[e].setMatrixAt(j, M); gems[e].setColorAt(j, HEART); });
    (dead || []).forEach(c => { const e = c[3], j = nl[e]++; M.makeScale(0.9, 0.9, 0.9); M.setPosition(c[0], c[1], c[2]); limes[e].setMatrixAt(j, M); limes[e].setColorAt(j, LIME); });
    g.spindles.forEach(sp => {
      for (let i = 0; i + 1 < sp.pts.length && ns < MAXS; i++) {
        const a = sp.pts[i], b = sp.pts[i + 1];
        V.set(b[0] - a[0], b[1] - a[1], b[2] - a[2]); const L = V.length(); if (L < 1e-6) continue;
        Q.setFromUnitVectors(UP, V.normalize()); Sv.set(sp.r, L, sp.r);
        M.compose(new THREE.Vector3(a[0] + off[0], a[1] + off[1], a[2] + off[2]), Q, Sv); spMesh.setMatrixAt(ns++, M);
      }
    });
    boxes.count = nb; octas.count = no; spMesh.count = ns; gems.forEach((m, e) => m.count = ng[e]); limes.forEach((m, e) => m.count = nl[e]);
    [boxes, octas, spMesh, ...gems, ...limes].forEach(m => { m.instanceMatrix.needsUpdate = true; if (m.instanceColor) m.instanceColor.needsUpdate = true; });
  }
  let kind = 'mass', fi = 0, showT = false, playing = !matchMedia('(prefers-reduced-motion: reduce)').matches, spin = playing, az = 0.75, el = 0.35, dist = 120, last = 0, tk = 0;
  function mixBar(counts, label) {
    const tot = counts.reduce((a, b) => a + b, 0) || 1;
    return `<span class="lab">${label}</span><span class="bar">${counts.map((c, e) => `<i style="width:${100 * c / tot}%;background:${EL_UI[e]}" title="${['Charge', 'Mass', 'Space', 'Time'][e]} ${c}"></i>`).join('')}</span>`;
  }
  function render() {
    const r = R[kind], t = target(kind);
    let g, off = [0, 0, 0], dead = [];
    if (showT) { g = t.frames[tk % t.frames.length]; off = t.offset; }
    else { g = swarmFrame(kind, fi); const step = fi * every; dead = r.crystals.filter(c => c[4] <= step); }
    draw(g, off, dead);
    camera.position.set(Math.cos(el) * Math.cos(az) * dist, Math.sin(el) * dist, Math.cos(el) * Math.sin(az) * dist);
    camera.lookAt(0, 0, 0); renderer.render(scene, camera);
    $('rsw-step').textContent = showT ? 'target' : (fi * every).toString();
    $('rsw-n').textContent = showT ? t.units : r.n[fi] + ' / ' + t.units;
    $('rsw-cr').textContent = showT ? '–' : dead.length;
    $('rsw-sh').textContent = showT ? (t.states ? t.states[2] / t.frames.length | 0 : '–') : g.shields;
    $('o-swt').textContent = fi * every;
  }
  function mix() {
    const r = R[kind], [F, N, W] = r.shape, f = F - 1, c = [0, 0, 0, 0];
    for (let i = 0; i < r.n[f]; i++) c[Math.round(r.a[(f * N + i) * W + 3])]++;
    $('swmix').innerHTML = mixBar(c, 'grown') + mixBar(target(kind).elements, 'target');
    const row = D.cross[kind], best = Object.keys(row).reduce((a, b) => row[a] <= row[b] ? a : b);
    $('rsw-census').textContent = `${D.major[kind]} seed grows closest to the ${D.names[best]} (${row[best].toFixed(1)}; its own target ${row[kind].toFixed(1)}).`;
  }
  function load(k) {
    kind = k; fi = R[k].shape[0] - 1; tk = 0;
    document.querySelectorAll('.swtabs [data-swk]').forEach(b => b.setAttribute('aria-selected', b.dataset.swk === k));
    const t = target(k); dist = Math.max(70, 1.6 * Math.max(...t.size));
    $('swt').max = R[k].shape[0] - 1; $('swt').value = fi; mix(); render();
  }
  function resize() { const w = cv.clientWidth || 560, h = cv.clientHeight || 560; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); render(); }
  function loop(t) {
    if (t - last > 120) {
      last = t;
      if (playing) { if (showT) tk++; else fi = (fi + 1) % R[kind].shape[0]; $('swt').value = fi; }
      if (spin) az += 0.012;
      if (playing || spin) render();
    }
    requestAnimationFrame(loop);
  }
  let drag = null;
  cv.addEventListener('pointerdown', e => { drag = [e.clientX, e.clientY, az, el]; cv.setPointerCapture(e.pointerId); });
  cv.addEventListener('pointermove', e => { if (!drag) return; az = drag[2] + (e.clientX - drag[0]) * 0.01; el = Math.max(-1.5, Math.min(1.5, drag[3] + (e.clientY - drag[1]) * 0.01)); render(); });
  cv.addEventListener('pointerup', () => { drag = null; });
  cv.addEventListener('wheel', e => { e.preventDefault(); dist = Math.max(20, Math.min(500, dist * Math.exp(e.deltaY * 0.001))); render(); }, { passive: false });
  document.querySelectorAll('.swtabs [data-swk]').forEach(b => b.onclick = () => load(b.dataset.swk));
  $('swt').addEventListener('input', e => { fi = +e.target.value; playing = false; $('bsw-play').textContent = 'Play'; showT = false; $('bsw-target').setAttribute('aria-pressed', false); render(); });
  $('bsw-play').onclick = () => { playing = !playing; $('bsw-play').textContent = playing ? 'Pause' : 'Play'; };
  if (!playing) $('bsw-play').textContent = 'Play';
  $('bsw-target').onclick = () => { showT = !showT; $('bsw-target').setAttribute('aria-pressed', showT); $('bsw-target').textContent = showT ? 'Show swarm' : 'Show target'; render(); };
  $('bsw-spin').onclick = () => { spin = !spin; $('bsw-spin').setAttribute('aria-pressed', spin); };
  $('bsw-spin').setAttribute('aria-pressed', spin);
  function theme() { scene.background = new THREE.Color(getComputedStyle(document.documentElement).getPropertyValue('--dish').trim() || '#e8eef2'); render(); }
  matchMedia('(prefers-color-scheme: dark)').addEventListener('change', theme);
  new MutationObserver(theme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  new ResizeObserver(resize).observe(cv);
  load('mass'); theme(); resize(); requestAnimationFrame(loop);
})();
</script>"""
