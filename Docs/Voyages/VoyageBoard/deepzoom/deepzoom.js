// ── Deep zoom: tiled stars and clusters, from the whole galaxy to one star system ──
// The galaxy is a quadtree of tiles (level 0 = the whole 140,000 ly square, level 16 = 2 ly). Every star belongs to
// the one level its brightness earns: the brightest stars live in the biggest tiles, fainter stars in deeper ones.
// A view draws every level down to the one that keeps about a thousand stars on screen, so any zoom is full but never
// crowded. Real stars and clusters come from catalogs [S]; the rest of the sky is drawn from the board's own galaxy
// model, seeded per tile so it never changes [M]. Near Tumult the catalogs are blind behind the dust, so everything
// there is model, and the brightest stars carry names Tumult's crews gave them [A].
const DZ = (() => {
  const ROOT = 140000, X0 = -70000, Y0 = -70000, LMAX = 16, K = 200;
  const SIG_SUN = 9;                       // stars per square ly in the disk column at Sol [S, approx.]
  const NA = SIG_SUN * ROOT * ROOT;
  const FL = L => L < 0 ? 0 : Math.min(1, K * Math.pow(4, L) / NA);   // share of stars (brightest first) held by levels 0..L
  const levelOfF = F => Math.max(0, Math.min(LMAX, Math.ceil(Math.log(F * NA / K) / Math.log(4) - 1e-9)));
  // the luminosity function: log10 of the share of stars brighter than absolute magnitude M [model, after the local census]
  const LF = [[-10, -11], [-8, -9], [-6, -7.5], [-4.5, -6.5], [-2, -4], [0, -3], [2, -2], [4, -1.52], [5, -1.22], [8, -0.7], [11, -0.35], [14, -0.07], [16.5, 0]];
  function logF(M) {
    if (M <= LF[0][0]) return LF[0][1];
    for (let i = 1; i < LF.length; i++) if (M <= LF[i][0]) { const [a, fa] = LF[i - 1], [b, fb] = LF[i]; return fa + (fb - fa) * (M - a) / (b - a); }
    return 0;
  }
  function Mof(F) {
    const lf = Math.log10(Math.max(F, 1e-11));
    for (let i = 1; i < LF.length; i++) if (lf <= LF[i][1]) { const [a, fa] = LF[i - 1], [b, fb] = LF[i]; return a + (b - a) * (lf - fa) / (fb - fa); }
    return 16.5;
  }
  const tileSize = L => ROOT / (1 << L);
  const SOL3 = [0, R0, ZSUN], TUM = BY.tumult;

  // stars per square ly, the whole disk column, from the board's galaxy: an exponential disk, the arms, the bulge and bar
  const ARM_SOL = armProfile(0, R0, R0, 1500);
  function sigma(x, y) {
    const r = Math.hypot(x, y);
    let disk = SIG_SUN * Math.exp(-(Math.max(r, 3000) - R0) / 8500) * (0.6 + 0.8 * armProfile(x, y, r, 1500)) / (0.6 + 0.8 * ARM_SOL);
    if (r > 45000) disk *= Math.exp(-(r - 45000) / 2500);
    const bulge = 220 * Math.exp(-((r / 3000) ** 2)) + 90 * bar(x, y);
    return { all: disk + bulge, bulge };
  }
  const SIG_MAX = sigma(0, 0).all * 1.25;

  // colour of a star from its B-V colour index
  const BV = [[-0.4, [155, 176, 255]], [-0.2, [170, 191, 255]], [0, [202, 215, 255]], [0.3, [248, 247, 255]], [0.6, [255, 244, 234]], [0.9, [255, 226, 190]], [1.2, [255, 210, 161]], [1.6, [255, 190, 120]], [2.2, [255, 170, 100]]];
  const colCache = new Map();
  function bvCol(ci) {
    const k = Math.round(ci * 10); if (colCache.has(k)) return colCache.get(k);
    const c = Math.max(-0.4, Math.min(2.2, k / 10));
    let rgb = BV[BV.length - 1][1];
    for (let i = 1; i < BV.length; i++) if (c <= BV[i][0]) { const [a, ca] = BV[i - 1], [b, cb] = BV[i], t = (c - a) / (b - a); rgb = ca.map((v, j) => Math.round(v + (cb[j] - v) * t)); break; }
    const s = `${rgb[0]},${rgb[1]},${rgb[2]}`; colCache.set(k, s); return s;
  }
  const classOf = ci => ci < -0.25 ? "O/B" : ci < 0 ? "B" : ci < 0.3 ? "A" : ci < 0.58 ? "F" : ci < 0.81 ? "G" : ci < 1.4 ? "K" : "M";
  const KIND = { "O/B": "blue-white giant", B: "blue-white star", A: "white star", F: "yellow-white star", G: "yellow star like the Sun", K: "orange star", M: "red star" };

  // ── real catalog objects ──
  const real = new Map();       // "L/i/j" -> stars
  let clusters = [], SRC = {};
  const key = (L, i, j) => L * 1e10 + i * 1e5 + j;
  const placeNames = P.map(p => p.name.toLowerCase());
  const isPlace = nm => nm && placeNames.some(p => p === nm.toLowerCase() || p.startsWith(nm.toLowerCase() + " "));
  function load() {
    const el = document.getElementById("realsky"); if (!el) return;
    const d = JSON.parse(el.textContent); SRC = d.src;
    d.stars.forEach(s => {
      const [x, y, z, M, ci, rank, ni, hip] = s;
      const nm = ni >= 0 ? d.names[ni].split("|") : [];
      const lvl = levelOfF(Math.pow(10, logF(M)));
      const show = Math.max(0, lvl - (rank === 3 ? 4 : rank === 2 ? 2 : rank === 1 ? 1.5 : 0));
      if (isPlace(nm[0]) || (x === 0 && y === R0)) return;
      const st = { x, y, z, M, ci, real: true, name: nm[0] || "", desig: nm[1] || (hip ? "HIP " + hip : ""), rank, lvl: show, col: bvCol(ci) };
      const L = Math.max(0, Math.min(LMAX, Math.ceil(show))), s2 = tileSize(L);
      const k = key(L, Math.floor((x - X0) / s2), Math.floor((y - Y0) / s2));
      if (!real.has(k)) real.set(k, []); real.get(k).push(st);
    });
    const FAMOUS = { "Melotte 22": "Pleiades", "Melotte 25": "Hyades", "Melotte 111": "Coma Star Cluster", "NGC 2632": "Beehive (Praesepe)", "Melotte 20": "Alpha Persei Cluster",
      "NGC 869": "Double Cluster h Persei", "NGC 884": "Double Cluster χ Persei", "IC 2602": "Southern Pleiades", "IC 2391": "Omicron Velorum Cluster", "NGC 6475": "Ptolemy's Cluster (M7)",
      "NGC 6405": "Butterfly Cluster (M6)", "NGC 2287": "M41", "NGC 1912": "M38", "NGC 2099": "M37", "NGC 1960": "M36", "NGC 2168": "M35", "NGC 1039": "M34", "NGC 6705": "Wild Duck Cluster (M11)",
      "NGC 3532": "Wishing Well Cluster", "NGC 4755": "Jewel Box", "NGC 2516": "Southern Beehive", "NGC 7092": "M39", "NGC 2422": "M47", "NGC 2548": "M48", "NGC 6494": "M23", "NGC 6531": "M21",
      "NGC 6611": "Eagle Nebula cluster (M16)", "NGC 2264": "Christmas Tree Cluster", "NGC 457": "Owl Cluster", "Collinder 399": "Coathanger", "Trumpler 14": "Trumpler 14 (Carina)", "NGC 2244": "Rosette cluster",
      "Westerlund 1": "Westerlund 1", "Westerlund 2": "Westerlund 2", "Platais 3": "Platais 3", "Stock 2": "Muscleman Cluster", "NGC 752": "NGC 752", "IC 4665": "IC 4665", "NGC 6231": "NGC 6231 (Scorpius)", "NGC 1981": "NGC 1981 (Orion)" };
    d.globs.forEach(([x, y, z, M, name, dly]) => !isPlace(name.split(" · ")[0]) && !name.startsWith("NGC 5139") && clusters.push({ x, y, z, M, name, glob: true, real: true, dly, lvl: Math.max(0.3, Math.min(13, (M + 10) * 0.25 + 0.2)) }));
    d.ocs.forEach(([x, y, z, M, id, n, age, famous, dly]) => {
      const nm = FAMOUS[id];
      if (isPlace(nm)) return;
      clusters.push({ x, y, z, M, name: nm || id, id, n, age, real: true, dly, lvl: Math.max(2, Math.min(13, 3.5 + (M + 10) * 0.75 - (nm ? 4 : famous ? 1.6 : 0) - Math.log10(Math.max(n, 10)) * .6)) });
    });
  }

  // ── model stars, generated per tile, deterministic ──
  const gen = new Map(); let genOrder = [], budget = 0, pending = false;
  const SY1 = ["Ash", "Bel", "Cor", "Dun", "Ely", "Fen", "Gal", "Hal", "Isk", "Jor", "Kel", "Lor", "Mar", "Nes", "Orr", "Pel", "Quil", "Ros", "Sal", "Tor", "Ul", "Vey", "Wren", "Yar", "Zel", "Brin", "Cael", "Drift", "Hollo", "Tam"];
  const SY2 = ["a", "is", "on", "ar", "eth", "um", "ira", "ox", "el", "ane", "wick", "mere", "ell", "orn", "ys", "ide"];
  function inventName(R) { const a = SY1[Math.floor(R() * SY1.length)], b = SY2[Math.floor(R() * SY2.length)]; return R() < .3 ? a + b + " " + ["Deep", "Light", "Ward", "Reach", "Shoal", "Gate"][Math.floor(R() * 6)] : a + b; }
  function poisson(lam, R) {
    if (lam > 40) return Math.max(0, Math.round(lam + Math.sqrt(lam) * Math.sqrt(-2 * Math.log(R() + 1e-12)) * Math.cos(6.2832 * R())));
    let k = 0, p = 1; const t = Math.exp(-lam); do { k++; p *= R(); } while (p > t); return k - 1;
  }
  const expo = (R, h) => (R() < .5 ? -1 : 1) * -Math.log(1 - R() * .999) * h;
  function tileStars(L, i, j) {
    const k = key(L, i, j);
    let t = gen.get(k); if (t) return t;
    if (budget <= 0) { pending = true; return []; }       // spread new tiles over frames, so a long dive never stalls
    budget -= 1;
    const s = tileSize(L), x0 = X0 + i * s, y0 = Y0 + j * s, cx = x0 + s / 2, cy = y0 + s / 2;
    const R = rng(Math.imul(L + 1, 0x9E3779B1) ^ Math.imul(i + 7, 0x85EBCA77) ^ Math.imul(j + 13, 0xC2B2AE3D));
    const dF = FL(L) - FL(L - 1);
    t = [];
    if (dF > 0) {
      const flat = s < 600;                     // small tiles: the density is flat across them
      const sc = flat ? sigma(cx, cy) : null;
      const smax = flat ? sc.all : SIG_MAX;
      const n = Math.min(7000, poisson(smax * s * s * dF, R));
      const tumR = 2600, nearTum = Math.hypot(cx - TUM.x, cy - TUM.y) < tumR + s;
      for (let q = 0; q < n; q++) {
        const x = x0 + R() * s, y = y0 + R() * s;
        const sg = flat ? sc : sigma(x, y);
        if (!flat && R() * smax > sg.all) continue;
        const F = FL(L - 1) + R() * dF, M = Mof(F);
        const inBulge = R() * sg.all < sg.bulge;
        const giant = M > -3.5 && M < 1.8 && R() < .45;
        const z = inBulge ? expo(R, 1400) : expo(R, M < 0 ? 260 : M < 4 ? 600 : 950);
        // near Sol the catalog already holds every star bright enough to see; the model only adds the faint ones
        const dS = Math.hypot(x - SOL3[0], y - SOL3[1], z - SOL3[2]);
        if (dS < 1600 && (dS < 30 || M + 5 * Math.log10(dS / 32.6) < 7)) continue;
        if (Math.hypot(x - TUM.x, y - TUM.y) < 1.5) continue;   // Tumult's own system is the place itself
        const ci = giant ? 1.0 + R() * .7 : M < -2 ? -0.3 + R() * .1 : M < 1 ? -0.15 + (M + 2) * .1 : M < 4 ? 0.1 + (M - 1) * .15 : M < 8 ? 0.55 + (M - 4) * .2 : 1.4 + Math.min(.5, (M - 8) * .08);
        const Mv = giant ? M - 0.0 : M;
        const st = { x, y, z, M: Mv, ci, real: false, lvl: L - 1 + (F - FL(L - 1)) / dF, col: bvCol(ci), seed: q };
        if (nearTum && Math.hypot(x - TUM.x, y - TUM.y) < tumR && st.lvl < 11.5) {
          const NR = rng(k ^ Math.imul(q + 1, 2654435761));
          st.name = inventName(NR); st.desig = `TC ${L}-${q}`; st.tumult = true; st.lvl = Math.max(0, st.lvl - 2.5);
        }
        t.push(st);
      }
    }
    gen.set(k, t); genOrder.push(k);
    if (genOrder.length > 600) { const old = genOrder.splice(0, 150); old.forEach(o => gen.delete(o)); }
    return t;
  }

  // ── the view: how deep to draw, and the height slab ──
  const TIERS = [
    { name: "Galaxy", w: 110000, min: 40000, what: "arms, bulge, the halo's globular clusters" },
    { name: "Arm", w: 20000, min: 9000, what: "giant nurseries, the brightest supergiants" },
    { name: "Sector", w: 4500, min: 2200, what: "star clusters and the brightest stars" },
    { name: "Region", w: 1200, min: 550, what: "clusters, clouds and the stars you see by eye" },
    { name: "Neighbourhood", w: 280, min: 110, what: "a slab of nearby stars around one height" },
    { name: "Cell", w: 50, min: 22, what: "one 20 ly cell and the stars inside it" },
    { name: "System", w: 10, min: 0, what: "single star systems a few ly apart" },
  ];
  const MIN_W = 4;
  const tierOf = w => TIERS.find(t => w >= t.min) || TIERS[TIERS.length - 1];
  function focusZ() {
    // the height the deep tiers slice at: the nearest place to the view's centre, else the midplane near Sol
    let best = null, bd = Infinity;
    P.forEach(p => { const d = Math.hypot(p.x - view.cx, p.y - view.cy); if (d < bd) { bd = d; best = p; } });
    return best && bd < Math.max(3000, view.w * 2) ? best.z : 0;
  }
  function viewState() {
    const zwin = Math.max(30, view.w * 2.5), slab = zwin < 1200;
    let sg = 0; [[0, 0], [-.25, -.2], [.25, -.2], [-.25, .2], [.25, .2]].forEach(([u, v]) => { sg += sigma(view.cx + u * view.w, view.cy + v * view.w * H / W).all; });
    sg /= 5;
    const sEff = sg * (slab ? Math.min(1, zwin / 1000) : 1);
    const A = view.w * view.w * H / W;
    const Fcut = Math.min(1, 900 / Math.max(1e-6, sEff * A));
    const Lcut = Math.min(LMAX + 1, Math.max(0.3, Math.log(Fcut * NA / K) / Math.log(4)));
    return { Lcut, zwin, slab, zf: slab ? focusZ() : 0 };
  }

  let sel = null, last = null, side = [];
  function each(vs, fn) {
    const top = Math.min(LMAX, Math.ceil(vs.Lcut) + 1);
    const [wx0, wy0] = toWorld(-20, -20), [wx1, wy1] = toWorld(W + 20, H + 20);
    for (let L = 0; L <= top; L++) {
      const s = tileSize(L);
      const i0 = Math.max(0, Math.floor((wx0 - X0) / s)), i1 = Math.min((1 << L) - 1, Math.floor((wx1 - X0) / s));
      const j0 = Math.max(0, Math.floor((wy0 - Y0) / s)), j1 = Math.min((1 << L) - 1, Math.floor((wy1 - Y0) / s));
      if ((i1 - i0 + 1) * (j1 - j0 + 1) > 400) continue;
      for (let i = i0; i <= i1; i++) for (let j = j0; j <= j1; j++) {
        const r = real.get(key(L, i, j)); if (r) r.forEach(fn);
        tileStars(L, i, j).forEach(fn);
      }
    }
  }
  function draw() {
    const vs = viewState(); last = vs;
    budget = 3; pending = false; side = [];
    const labels = [], hits = [];
    const s = scale(), galaxyFade = Math.min(1, Math.max(0, (Math.log(110000) - Math.log(view.w)) / (Math.log(110000) - Math.log(30000))));
    let n = 0;
    const dot = (o, rel, a) => {
      const sx = (o.x - view.cx) * s + W / 2, sy = (o.y - view.cy) * s + H / 2;
      if (sx < -10 || sy < -10 || sx > W + 10 || sy > H + 10) return null;
      const r = Math.max(.45, Math.min(3.6, .55 + .42 * rel));
      ctx.fillStyle = `rgba(${o.col},${a.toFixed(2)})`;
      if (r < 1.1) ctx.fillRect(sx - r, sy - r, r * 2, r * 2);
      else {
        if (rel > 3.2) { ctx.beginPath(); ctx.arc(sx, sy, r * 2.6, 0, 7); ctx.fillStyle = `rgba(${o.col},${(a * .14).toFixed(3)})`; ctx.fill(); ctx.fillStyle = `rgba(${o.col},${a.toFixed(2)})`; }
        ctx.beginPath(); ctx.arc(sx, sy, r, 0, 7); ctx.fill();
      }
      n++;
      return [sx, sy, r];
    };
    const sysTier = view.w < 30;
    each(vs, o => {
      const rel = vs.Lcut - o.lvl;
      if (rel < -1) return;
      let a = rel < 0 ? (1 + rel) * .7 : Math.min(1, .62 + .14 * rel);
      if (vs.slab) { const dz = Math.abs(o.z - vs.zf) / vs.zwin; if (dz > 1.6) return; a *= Math.exp(-dz * dz * 1.2); }
      a *= .35 + .65 * galaxyFade;
      if (a < .05) return;
      const p = dot(o, rel, a); if (!p) return;
      if (a > .2 && side.length < 3000) side.push({ sx: p[0], wx: o.x, wy: o.y, z: o.z, col: o.col, r: p[2], a });
      if (a > .3) hits.push({ star: o, sx: p[0], sy: p[1], r: Math.max(7, p[2] + 4) });
      const named = o.name || (sysTier && o.real && o.desig);
      if (named && a > .35 && (rel > (o.real ? (o.rank === 3 ? 2 : 3.2) : 3.4) || (sysTier && rel > .5))) labels.push({ p: { id: "*" + o.x + "," + o.y, name: o.name || o.desig, region: "star" }, sx: p[0], sy: p[1], sz: p[2] + 1, pri: -1 + Math.min(.9, rel / 10), col: o.tumult ? "rgba(255,214,150,.82)" : o.real ? "rgba(214,226,246,.8)" : "rgba(200,210,230,.6)", small: true });
      if (sel === o) { ctx.beginPath(); ctx.arc(p[0], p[1], p[2] + 6, 0, 7); ctx.strokeStyle = "rgba(255,255,255,.9)"; ctx.lineWidth = 1; ctx.stroke(); }
    });
    // clusters
    clusters.forEach(c => {
      const rel = vs.Lcut - c.lvl; if (rel < -0.6) return;
      const sx = (c.x - view.cx) * s + W / 2, sy = (c.y - view.cy) * s + H / 2;
      if (sx < -30 || sy < -30 || sx > W + 30 || sy > H + 30) return;
      let a = rel < 0 ? (1 + rel / .6) * .8 : Math.min(1, .7 + .1 * rel);
      if (vs.slab) { const dz = Math.abs(c.z - vs.zf) / Math.max(vs.zwin, 200); if (dz > 2) return; a *= Math.exp(-dz * dz * .6); }
      else if (c.glob && view.w < 30000) { const dz = Math.abs(c.z - ZSUN) / (view.w * 1.5); if (dz > 2) return; a *= Math.exp(-dz * dz); }   // halo clusters far above or below the slice fade out
      if (a < .06) return;
      const physR = c.glob ? 60 : 8 + Math.sqrt(c.n || 50) * .5;            // ly, a drawing size, not a measured radius
      const rpx = Math.max(c.glob ? 3.2 : 2.6, Math.min(c.glob ? 12 : 40, physR * s));
      ctx.save(); ctx.globalAlpha = a;
      if (c.glob) {
        const g = ctx.createRadialGradient(sx, sy, 0, sx, sy, rpx * 1.4); g.addColorStop(0, "rgba(255,226,170,.95)"); g.addColorStop(.45, "rgba(255,206,140,.45)"); g.addColorStop(1, "rgba(255,200,130,0)");
        ctx.fillStyle = g; ctx.beginPath(); ctx.arc(sx, sy, rpx * 1.4, 0, 7); ctx.fill();
      } else {
        ctx.setLineDash([2, 2.5]); ctx.strokeStyle = "rgba(170,205,255,.85)"; ctx.lineWidth = 1.1; ctx.beginPath(); ctx.arc(sx, sy, rpx, 0, 7); ctx.stroke(); ctx.setLineDash([]);
      }
      if (sel === c) { ctx.beginPath(); ctx.arc(sx, sy, rpx + 5, 0, 7); ctx.strokeStyle = "#fff"; ctx.lineWidth = 1; ctx.stroke(); }
      ctx.restore();
      hits.push({ star: c, sx, sy, r: Math.max(9, rpx + 3) });
      if (rel > (c.name !== c.id && !c.glob ? 0.4 : 1.6) && a > .4) labels.push({ p: { id: "c" + c.name, name: c.name.split(" · ")[c.name.includes(" · ") ? 1 : 0], region: "star" }, sx, sy, sz: rpx + 2, pri: -.5 + Math.min(.45, rel / 12), col: c.glob ? "rgba(255,220,170,.85)" : "rgba(180,210,255,.85)", small: true });
    });
    if (pending) setTimeout(() => { needs = true; }, 0);
    window.__dz = { hits: hits.length, n, sample: hits.slice(0, 5).map(h => [h.sx, h.sy]) };
    return { labels: labels.sort((a, b) => b.pri - a.pri).slice(0, 60), hits, n };
  }

  function lum(M) { const l = Math.pow(10, 0.4 * (4.83 - M)); return l >= 100 ? fmt(l) + "×" : l >= 1 ? l.toFixed(1) + "×" : l.toFixed(l < .01 ? 4 : 2) + "×"; }
  function inspHTML(o) {
    const here = BY[S.here];
    const d3 = (a) => Math.hypot(o.x - a[0], o.y - a[1], o.z - a[2]);
    const fromSol = d3(SOL3), fromTum = d3([TUM.x, TUM.y, TUM.z]), fromArk = d3([here.x, here.y, here.z]);
    let kind, body, tag;
    if (o.glob !== undefined || o.n !== undefined) {
      kind = o.glob ? "Globular cluster" : "Open cluster";
      body = o.glob ? `A ball of hundreds of thousands of old stars orbiting through the halo, about ${lum(o.M)} the Sun's light.`
        : `${o.n ? fmt(o.n) + " member stars" : "A young star cluster"}${o.age ? `, about ${o.age >= 9 ? (Math.pow(10, o.age - 9)).toFixed(1) + " billion" : fmt(Math.pow(10, o.age - 6)) + " million"} years old` : ""}.`;
      tag = `S · ${o.glob ? SRC.globs : SRC.ocs}`;
    } else {
      const cl = classOf(o.ci);
      kind = o.real ? "Star" : o.tumult ? "Star, as Tumult's crews chart it" : "Star, from the galaxy model";
      body = `A ${o.M < -3 && o.ci > .8 ? "red supergiant" : o.M < 1.5 && o.ci > .9 ? "giant" : KIND[cl]}, ${lum(o.M)} the Sun's light (absolute magnitude ${o.M.toFixed(1)}).` + (o.desig && o.name ? ` Also ${esc(o.desig)}.` : "");
      tag = o.real ? `S · ${SRC.stars}` : o.tumult ? "A · invented name; the star is drawn from the model" : "M · drawn from the galaxy model, not a catalog star";
    }
    const title = o.name || o.desig || (o.real ? "Unnamed star" : "Uncharted star");
    return `<div class="eyebrow">${esc(kind)}</div><h3>${esc(title)}</h3><p>${body} <span class="tag">[${esc(tag)}]</span></p>
      <p class="sub">${zText(o.z)} · ${dist(fromSol)} from Sol · ${dist(fromTum)} from Tumult · ${dist(fromArk)} from the Ark, ${duration(fromArk / CELL * MIN_PER_CELL)} by Ark in a straight line</p>`;
  }
  function tipHTML(o) {
    if (o.glob !== undefined || o.n !== undefined) return `<b>${esc(o.name)}</b> <span class="k">· ${o.glob ? "globular cluster" : "open cluster"} · ${dist(o.dly)} from Sol</span>`;
    return `<b>${esc(o.name || o.desig || (o.real ? "Star" : "Uncharted star"))}</b> <span class="k">· ${classOf(o.ci)} star · ${lum(o.M)} Sun${o.real ? "" : " · model"}</span>`;
  }
  load();
  return { draw, TIERS, tierOf, MIN_W, inspHTML, tipHTML, get sel() { return sel; }, set sel(v) { sel = v; }, get last() { return last; }, get side() { return side; } };
})();
