// ── Cells in 3D: truncated octahedra around a body-centred cubic lattice of nuclei ──
// Every cell grows round one nucleus. The nuclei sit on a body-centred cubic (BCC) lattice, and each cell is the
// space nearer its own nucleus than any other: that Voronoi cell is the truncated octahedron, 6 square faces and
// 8 hexagonal ones. It is Kelvin's foam cell, the equal-volume cell that spends the least membrane. A cell is 20 ly
// across its square faces [S: 20 ly cells]; its nucleus is the game's, 392 u = 3.3 ly in radius [S: Docs/ECOSYSTEM.md],
// and the lattice is anchored so the central cell's nucleus is Sgr A* [A]. The chart shows the cells as a slice at
// one height (octagons and squares at a layer, diamonds between layers); the side view slices them upright.
const CELLS = (() => {
  const A = CELL, HALF = A / 2;
  const NUC_R = 392 / 120;                       // ly [S]
  const FACES = [];
  [[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1]].forEach(n => FACES.push({ n, b: HALF }));
  for (const a of [1, -1]) for (const b of [1, -1]) for (const c of [1, -1]) FACES.push({ n: [a, b, c], b: 3 * A / 4 });
  const G = [BY.heart.x, BY.heart.y, BY.heart.z];   // the lattice's origin: Sgr A* is a nucleus [A]
  let off = 0;                                   // the slice's offset from its focus, ly (Slice ▲ ▼)
  const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

  // the nucleus nearest a point is the point's cell
  function cellAt(x, y, z) {
    x -= G[0]; y -= G[1]; z -= G[2];
    const a = [Math.round(x / A) * A, Math.round(y / A) * A, Math.round(z / A) * A];
    const b = [(Math.floor(x / A) + .5) * A, (Math.floor(y / A) + .5) * A, (Math.floor(z / A) + .5) * A];
    const da = (a[0] - x) ** 2 + (a[1] - y) ** 2 + (a[2] - z) ** 2, db = (b[0] - x) ** 2 + (b[1] - y) ** 2 + (b[2] - z) ** 2;
    const c = da <= db ? a : b; return [c[0] + G[0], c[1] + G[1], c[2] + G[2]];
  }
  const keyOf = c => Math.round((c[0] - G[0]) * 2 / A) + "," + Math.round((c[1] - G[1]) * 2 / A) + "," + Math.round((c[2] - G[2]) * 2 / A);
  const nCache = new Map();
  function densOf(c) {
    const k = keyOf(c); let n = nCache.get(k);
    if (n === undefined) { n = dens(c[0], c[1], c[2]); if (nCache.size > 300000) nCache.clear(); nCache.set(k, n); }
    return n;
  }

  // the section of one cell by a plane P = O + u U + v V: a convex polygon in (u, v), or null
  function clip(poly, a, b, r) {
    const out = [];
    for (let i = 0; i < poly.length; i++) {
      const p = poly[i], q = poly[(i + 1) % poly.length], fp = a * p[0] + b * p[1] - r, fq = a * q[0] + b * q[1] - r;
      if (fp <= 0) out.push(p);
      if ((fp < 0) !== (fq < 0) && fp !== fq) { const t = fp / (fp - fq); out.push([p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t]); }
    }
    return out;
  }
  function section(C, O, U, V, extra) {
    const d = [C[0] - O[0], C[1] - O[1], C[2] - O[2]], cu = dot(d, U), cv = dot(d, V);
    let poly = [[cu - A, cv - A], [cu + A, cv - A], [cu + A, cv + A], [cu - A, cv + A]];
    for (const f of FACES) { poly = clip(poly, dot(f.n, U), dot(f.n, V), f.b + dot(f.n, d)); if (poly.length < 3) return null; }
    if (extra) for (const e of extra) { poly = clip(poly, e[0], e[1], e[2]); if (poly.length < 3) return null; }
    const N = [U[1] * V[2] - U[2] * V[1], U[2] * V[0] - U[0] * V[2], U[0] * V[1] - U[1] * V[0]];
    return { poly, cu, cv, h: dot(d, N) };
  }

  // ecology regions: the sea's own zones [S: fact sheet], the centre's rings [A], and the named clouds and bubbles
  const CLOUD_NAMES = ["Taurus clouds", "Orion A", "Orion B", "Rho Ophiuchi", "Lupus clouds", "Coalsack", "Aquila Rift", "Perseus cloud", "Cepheus Flare", "Cygnus X", "Carina", "Eagle", "Lagoon"];
  const NAMED = [];
  CLOUDS.slice(0, CLOUD_NAMES.length).forEach((c, i) => NAMED.push({ name: CLOUD_NAMES[i], x: c.x, y: c.y, z: c.z, r: (c.rx + c.ry) / 2, cloud: c }));
  const ci = CLOUD_NAMES.length + 12;
  [["Sgr B2", ci], ["The Brick", ci + 1], ["Sgr C", ci + 2]].forEach(([name, i]) => { const c = CLOUDS[i]; if (c) NAMED.push({ name, x: c.x, y: c.y, z: c.z, r: (c.rx + c.ry) / 2, cloud: c }); });
  ["Local Bubble", "Orion-Eridanus Superbubble", "Loop I", "Tumult's Bubble"].forEach((name, i) => { const b = BUBBLES[i]; if (b) NAMED.push({ name, x: b.x, y: b.y, z: 0, r: b.r, bubble: b }); });
  const ZONES = [
    { name: "Hot bubble", feel: "spray", max: .1, what: "hot thin gas blown out by supernovae; Sol sits in one" },
    { name: "Warm medium", feel: "froth", max: .6, what: "the warm gas between clouds" },
    { name: "Open water", feel: "water", max: 5, what: "the disk's average sea" },
    { name: "Cold cloud", feel: "thick water", max: 100, what: "cold atomic clouds, rich and rough" },
    { name: "Molecular cloud", feel: "reef", max: Infinity, what: "star nurseries, too dense to cross" },
  ];
  const RINGS = [
    { r: 10, name: "The central cell", what: "Sgr A* and the minispiral; its nucleus is the Heart [A]" },
    { r: 30, name: "Ring 1 · circumnuclear disk", what: "a solid reef ring round the Heart [A]" },
    { r: 650, name: "Central Molecular Zone", what: "reef country, 100 to 160 ly thick, lanes only through gaps [A]" },
  ];
  const zoneIx = n => ZONES.findIndex(z => n < z.max);
  function regionAt(x, y, z, n) {
    const r = Math.hypot(x - G[0], y - G[1]), zone = ZONES[zoneIx(n)];
    let ring = null;
    if (r < 10 && Math.abs(z - G[2]) < 15) ring = RINGS[0]; else if (r < 30 && Math.abs(z - G[2]) < 25) ring = RINGS[1]; else if (r < 650 && Math.abs(z - G[2]) < 80) ring = RINGS[2];
    let named = null;
    for (const f of NAMED) {
      if (f.bubble && Math.hypot(x - f.x, y - f.y) < f.r) { named = f; break; }
      if (f.cloud) { const c = f.cloud, dx = x - c.x, dy = y - c.y, u = dx * c.c + dy * c.s, v = -dx * c.s + dy * c.c; if (u * u / (c.rx * c.rx) + v * v / (c.ry * c.ry) < 2.2 && Math.abs(z - c.z) < c.h * 1.6) { named = f; break; } }
    }
    return { zone, ring, named };
  }
  const regionHTML = (x, y, z, n) => {
    const g = regionAt(x, y, z, n);
    return `<span class="k">Ecology region:</span> ${g.ring ? esc(g.ring.name) + " · " : ""}${g.zone.name}${g.named ? " in " + esc(g.named.name) : ""} <span class="k">(${g.zone.feel})</span>`;
  };

  // the height the chart slices the cells at: the deep zoom's focus height, else the Ark's, plus the reader's offset
  const cellZoom = () => CELL * scale() > 14;
  // what a cell shows at a size: its membrane from 22 px wide (full at 44), its nucleus from a 4 px radius (full at 8),
  // so neither turns into moire as cells shrink; under 14 px the smooth sea takes over and the cells are gone
  const memAlpha = px => Math.max(0, Math.min(1, (px - 22) / 22));
  const nucAlpha = px => Math.max(0, Math.min(1, (px * NUC_R / A - 4) / 4));
  const POLY_PX = 40;   // cells wider than this are drawn as polygons; smaller ones pixel by pixel
  // snapped to the nearest layer of nuclei, so by default the slice cuts nuclei: octagons round them, squares between
  function baseZ() { const raw = view.w * 2.5 < 1200 ? DZ.focusZ() : arkPos()[2]; return Math.round((raw - G[2]) / HALF) * HALF + G[2]; }
  function sliceZ() { return Math.max(-DISK_HALF - 100, Math.min(DISK_HALF + 100, baseZ() + off)); }
  let barY = null;   // where the slice bar sits in the side view, for dragging
  const MEM_COL = "214,236,255";
  // the fast way to draw many small cells: every pixel asks which cell it is in (no density evaluation past the cache),
  // a membrane is where that answer changes, and a nucleus is a pixel within NUC_R of its own nucleus
  function pixels(ctx2, fw, fh, step, world, px) {
    const ma = memAlpha(px), na = nucAlpha(px);
    const img = ctx2.createImageData(fw, fh), d = img.data, keys = new Int32Array(fw * fh), cols = new Map();
    let n = 0;
    for (let j = 0; j < fh; j++) for (let i = 0; i < fw; i++) {
      const [x, y, z] = world(i * step + step / 2, j * step + step / 2), C = cellAt(x, y, z), k = keyOf(C);
      let e = cols.get(k); if (e === undefined) { const col = seaRGB(densOf(C)); e = { id: ++n, r: col[0], g: col[1], b: col[2] }; cols.set(k, e); }
      const o = (j * fw + i) * 4, dn = Math.hypot(x - C[0], y - C[1], z - C[2]);
      keys[j * fw + i] = e.id;
      d[o] = e.r; d[o + 1] = e.g; d[o + 2] = e.b;
      if (na > 0 && dn < NUC_R) { const u = dn / NUC_R, glow = 1 - u * u, rim = Math.exp(-((u - .88) ** 2) * 60); d[o] += (255 - d[o]) * na; d[o + 1] += (236 + 14 * glow - d[o + 1]) * na; d[o + 2] += (180 + 50 * glow + 60 * rim - d[o + 2]) * na; }
      d[o + 3] = 255;
    }
    if (ma > 0) for (let j = 1; j < fh; j++) for (let i = 1; i < fw; i++) {
      const k = j * fw + i; if (keys[k] === keys[k - 1] && keys[k] === keys[k - fw]) continue;
      const o = k * 4, a = .5 * ma; d[o] += (214 - d[o]) * a; d[o + 1] += (236 - d[o + 1]) * a; d[o + 2] += (255 - d[o + 2]) * a;
    }
    ctx2.putImageData(img, 0, 0);
  }
  let dirty = false;
  const takeDirty = () => { const v = dirty; dirty = false; return v; };
  const pixCv = document.createElement("canvas"), pixCtx = pixCv.getContext("2d");
  const pathOn = (c, poly, map) => { c.beginPath(); poly.forEach((p, i) => { const [sx, sy] = map(p); i ? c.lineTo(sx, sy) : c.moveTo(sx, sy); }); c.closePath(); };

  // the chart at cell zoom: every cell this height crosses, filled with its own sea, walled by its membrane
  function renderChart(c2, step) {
    const s = scale(), cellPx = A * s, zs = sliceZ();
    if (cellPx < POLY_PX) {
      const fw = Math.ceil(W / step), fh = Math.ceil(H / step), cv2 = pixCv, g = pixCtx;
      if (cv2.width !== fw || cv2.height !== fh) { cv2.width = fw; cv2.height = fh; }
      pixels(g, fw, fh, step, (px, py) => [(px - W / 2) / s + view.cx, (py - H / 2) / s + view.cy, zs], cellPx);
      c2.imageSmoothingEnabled = step > 1; c2.drawImage(cv2, 0, 0, W, H);
      return;
    }
    const [x0, y0] = toWorld(-cellPx, -cellPx), [x1, y1] = toWorld(W + cellPx, H + cellPx);
    const map = p => [(p[0] - view.cx) * s + W / 2, (p[1] - view.cy) * s + H / 2];
    const inner = cellPx > 90, mem = Math.max(1, Math.min(cellPx * .035, 14));
    const cells = [];
    for (const o of [0, .5]) {
      const cz = (Math.round((zs - G[2]) / A - o) + o) * A + G[2];
      if (Math.abs(cz - zs) > HALF) continue;
      for (let j = Math.floor((y0 - G[1]) / A - o); j <= Math.ceil((y1 - G[1]) / A - o); j++) for (let i = Math.floor((x0 - G[0]) / A - o); i <= Math.ceil((x1 - G[0]) / A - o); i++) {
        const C = [(i + o) * A + G[0], (j + o) * A + G[1], cz], sec = section(C, [0, 0, zs], [1, 0, 0], [0, 1, 0]);
        if (sec) cells.push({ C, sec });
      }
    }
    // the sea in each cell
    cells.forEach(({ C, sec }) => {
      const col = seaRGB(densOf(C));
      pathOn(c2, sec.poly, map); c2.fillStyle = `rgb(${col[0] | 0},${col[1] | 0},${col[2] | 0})`; c2.fill();
      if (inner) {
        // cytoplasm: the sea brightens toward the nucleus, where omni regrows, and deepens to the membrane's blue at the walls
        const [nx, ny] = map([C[0], C[1]]), g = c2.createRadialGradient(nx, ny, 0, nx, ny, cellPx * .62);
        g.addColorStop(0, "rgba(255,248,226,.14)"); g.addColorStop(.6, "rgba(255,248,226,0)"); g.addColorStop(1, "rgba(37,54,160,.45)");
        c2.fillStyle = g; c2.fill();
      }
    });
    // membranes: a dark seam between cells and a pale wall on each side of it
    cells.forEach(({ sec }) => {
      pathOn(c2, sec.poly, map);
      if (inner) {
        c2.save(); c2.clip();
        c2.strokeStyle = "rgba(60,90,210,.35)"; c2.lineWidth = mem * 7; c2.stroke();   // the membrane's blue, seen from inside
        c2.strokeStyle = `rgba(${MEM_COL},.22)`; c2.lineWidth = mem * 2.2; c2.stroke();
        c2.strokeStyle = `rgba(${MEM_COL},.5)`; c2.lineWidth = mem * .7; c2.stroke();
        c2.restore();
        c2.strokeStyle = "rgba(5,9,18,.85)"; c2.lineWidth = 1.2; c2.stroke();
      } else {
        c2.strokeStyle = cellPx > 14 ? `rgba(${MEM_COL},${Math.min(.5, .14 + cellPx / 200).toFixed(2)})` : "rgba(5,9,18,.65)"; c2.lineWidth = cellPx > 14 ? 1 : .7; c2.stroke();
      }
    });
    // nuclei: where the slice cuts one, its section; where it passes above or below, its outline, dashed
    if (cellPx > 22) cells.forEach(({ C, sec }) => {
      const [nx, ny] = map([C[0], C[1]]), h = Math.abs(sec.h);
      if (h < NUC_R) nucleus(c2, nx, ny, Math.sqrt(NUC_R * NUC_R - h * h) * s, h / NUC_R);
      else if (cellPx > 50) {
        c2.beginPath(); c2.arc(nx, ny, NUC_R * s, 0, 7); c2.setLineDash([3, 4]); c2.strokeStyle = `rgba(255,236,190,${(.8 - h / A * .6).toFixed(2)})`; c2.lineWidth = 1.2; c2.stroke(); c2.setLineDash([]);
      }
    });
    if (closed.size && cellPx > 6) cells.forEach(({ C, sec }) => { if (!closed.has(KEY(...hexOf(C[0], C[1])))) return; pathOn(c2, sec.poly, map); c2.strokeStyle = "rgba(255,233,168,.45)"; c2.lineWidth = 1; c2.stroke(); });
  }

  // the nucleus as the game draws it: a translucent caged sphere with a bright rim (the cage shader's fresnel edge),
  // lit from the upper left, and a halo in the sea around it. r is the sphere's radius on screen; cut is how far
  // above or below its centre the slice passes (0 through the middle), which flattens the cage's rings
  function nucleus(c, x, y, r, cut) {
    r = Math.max(1.5, r);
    if (r > 8) { const halo = c.createRadialGradient(x, y, r, x, y, r * 1.9); halo.addColorStop(0, "rgba(255,236,190,.32)"); halo.addColorStop(1, "rgba(255,236,190,0)"); c.beginPath(); c.arc(x, y, r * 1.9, 0, 7); c.fillStyle = halo; c.fill(); }
    // the body: translucent, brighter where the light falls, darker at the limb's far side
    const body = c.createRadialGradient(x - r * .35, y - r * .35, r * .1, x, y, r);
    body.addColorStop(0, "rgba(255,252,240,.85)"); body.addColorStop(.55, "rgba(255,238,196,.45)"); body.addColorStop(.92, "rgba(255,224,160,.35)"); body.addColorStop(1, "rgba(255,246,220,.95)");
    c.beginPath(); c.arc(x, y, r, 0, 7); c.fillStyle = body; c.fill();
    if (r < 7) return;
    // the cage: great circles of the sphere, seen in projection; the slice's own circle is the rim
    c.save(); c.beginPath(); c.arc(x, y, r, 0, 7); c.clip();
    const q = Math.sqrt(Math.max(0, 1 - cut * cut));
    c.strokeStyle = "rgba(255,248,225,.55)"; c.lineWidth = Math.max(.8, r * .03);
    const ring = (rx, ry, rot) => { c.beginPath(); c.ellipse(x, y, Math.max(.5, rx), Math.max(.5, ry), rot, 0, 7); c.stroke(); };
    for (let i = 0; i < 4; i++) ring(r, r * (.12 + .22 * i) * q, Math.PI / 4 * i + .3);
    for (let i = 0; i < 3; i++) ring(r * (.28 + .3 * i), r, .25 + i * .9);
    c.restore();
    // the fresnel rim, bright and thin, with a soft inner band
    c.beginPath(); c.arc(x, y, r - Math.max(1, r * .04), 0, 7); c.strokeStyle = "rgba(255,236,190,.35)"; c.lineWidth = Math.max(2, r * .09); c.stroke();
    c.beginPath(); c.arc(x, y, r, 0, 7); c.strokeStyle = "rgba(255,250,236,.95)"; c.lineWidth = Math.max(1, r * .035); c.stroke();
    // the highlight
    const hl = c.createRadialGradient(x - r * .42, y - r * .42, 0, x - r * .42, y - r * .42, r * .5); hl.addColorStop(0, "rgba(255,255,255,.55)"); hl.addColorStop(1, "rgba(255,255,255,0)");
    c.beginPath(); c.arc(x, y, r, 0, 7); c.fillStyle = hl; c.fill();
  }

  // drawn every frame over the chart: region names, the centre's rings, and inside one cell its levels
  function overlay(c) {
    const s = scale(), cellPx = A * s, labels = [];
    c.save();
    c.textAlign = "center"; c.textBaseline = "middle";
    // the centre's rings
    RINGS.forEach((g, i) => {
      const rp = g.r * s, [sx, sy] = toScreen(G[0], G[1]);
      if (rp < 26 || rp > Math.max(W, H) * 2.5 || i === 0 && cellPx > 6.5) return;
      c.beginPath(); c.arc(sx, sy, rp, 0, 7); c.setLineDash(i === 2 ? [6, 6] : [2, 4]); c.strokeStyle = i === 2 ? "rgba(255,170,130,.45)" : "rgba(255,200,150,.6)"; c.lineWidth = 1; c.stroke(); c.setLineDash([]);
      labels.push({ t: g.name.toUpperCase(), x: sx, y: sy - rp - 9, col: "rgba(255,206,170,.85)" });
    });
    // named clouds and bubbles, while they are a readable size
    if (view.w < 30000) NAMED.forEach(f => {
      const rp = f.r * s; if (rp < 34 || rp > Math.max(W, H) * 1.2) return;
      const [sx, sy] = toScreen(f.x, f.y); if (sx < -rp || sy < -rp || sx > W + rp || sy > H + rp) return;
      labels.push({ t: f.name.toUpperCase() + (f.bubble ? " · SPRAY" : " · REEF"), x: Math.max(80, Math.min(W - 80, sx)), y: Math.max(16, Math.min(H - 40, sy - (f.bubble ? rp * .55 : rp + 10))), col: f.bubble ? "rgba(190,206,232,.7)" : "rgba(255,180,150,.75)" });
    });
    c.font = "600 10.5px 'Barlow Semi Condensed', 'Arial Narrow', sans-serif";
    labels.forEach(l => { const w = c.measureText(l.t).width + 10; c.fillStyle = "rgba(5,9,18,.45)"; c.fillRect(l.x - w / 2, l.y - 7, w, 14); c.fillStyle = l.col; c.fillText(l.t, l.x, l.y + .5); });
    c.restore();
  }
  // inside the cell nearest the centre of the view: its levels, named (drawn last, over the stars' labels)
  function tags(c) {
    const s = scale(), cellPx = A * s;
    if (cellPx > 200) {
      c.save(); c.textBaseline = "middle";
      const zs = sliceZ();
      // of the two layers the slice crosses, the nucleus nearest the view's centre, counting its height
      let C = null, best = Infinity;
      for (const o of [0, .5]) { const k = [view.cx, view.cy, zs].map((v, a) => (Math.round((v - G[a]) / A - o) + o) * A + G[a]), d = (k[0] - view.cx) ** 2 + (k[1] - view.cy) ** 2 + 4 * (k[2] - zs) ** 2; if (d < best) { best = d; C = k; } }
      const sec = section(C, [0, 0, zs], [1, 0, 0], [0, 1, 0]);
      if (sec) {
        const n = densOf(C), [nx, ny] = toScreen(C[0], C[1]), h = sec.h, heart = C[0] === G[0] && C[1] === G[1] && C[2] === G[2];
        const top = sec.poly.reduce((a, p) => p[1] < a[1] ? p : a), [tx, ty] = toScreen(top[0], top[1]);
        const tag = (t, sub, x, y, col) => {
          c.font = "600 12px 'Barlow Semi Condensed', 'Arial Narrow', sans-serif"; const w1 = c.measureText(t).width;
          c.font = "500 10.5px 'JetBrains Mono', monospace"; const w2 = sub ? c.measureText(sub).width : 0, w = Math.max(w1, w2) + 12;
          const bx = Math.max(4, Math.min(W - w - 4, x - w / 2)), by = Math.max(4, Math.min(H - 60, y));
          c.fillStyle = "rgba(5,9,18,.78)"; c.fillRect(bx, by - 9, w, sub ? 30 : 18);
          c.textAlign = "left"; c.fillStyle = col; c.font = "600 12px 'Barlow Semi Condensed', 'Arial Narrow', sans-serif"; c.fillText(t, bx + 6, by);
          if (sub) { c.font = "500 10.5px 'JetBrains Mono', monospace"; c.fillStyle = "rgba(214,224,240,.75)"; c.fillText(sub, bx + 6, by + 14); }
        };
        const nucR = Math.abs(h) < NUC_R ? Math.sqrt(NUC_R * NUC_R - h * h) : NUC_R;
        tag(heart ? "Nucleus · Sgr A*, the Heart [A]" : "Nucleus", Math.abs(h) < NUC_R ? `6.5 ly across · omni regrows here` : `${dist(Math.abs(h))} ${h > 0 ? "above" : "below"} this slice · 6.5 ly across`, nx, ny + nucR * s + 18, "#ffeec4");
        tag("Cytoplasm", `${seaName(n)}, ${n < 10 ? n.toFixed(2) : fmt(n)} g/cm³ · flora and fauna live here`, (nx + tx) / 2, (ny + ty) / 2 + 6, "#cfe4ff");
        tag(heart ? "Membrane · the circumnuclear disk [A]" : "Membrane", "14 faces · 20 ly across the square ones", tx, ty + 22, `rgb(${MEM_COL})`);
      }
      c.restore();
    }
  }

  // the side view at cell zoom: the cells cut upright through the middle of the chart, filled with their own sea,
  // their membranes and nuclei, and the chart's slice as a bar you can drag to another height
  function side(c, f) {
    barY = null;
    if (!cellZoom()) return;
    const ys = f.sliceY, [x0] = toWorld(0, 0), [x1] = toWorld(W, 0);
    const zTop = f.zc + (SH / 2 + 8) / f.s, zBot = f.zc - (SH / 2 - 8) / f.s, cellPx = A * f.s;
    const map = p => [(p[0] - view.cx) * f.s + W / 2, sideY(p[1], f)];
    const inner = cellPx > 90, mem = Math.max(1, Math.min(cellPx * .035, 14));
    c.save();
    const secs = [];
    if (cellPx >= POLY_PX)
    for (const o of [0, .5]) {
      const cy = (Math.round((ys - G[1]) / A - o) + o) * A + G[1]; if (Math.abs(cy - ys) > HALF) continue;
      for (let k = Math.floor((zBot - G[2]) / A - o) - 1; k <= Math.ceil((zTop - G[2]) / A - o) + 1; k++) for (let i = Math.floor((x0 - G[0]) / A - o) - 1; i <= Math.ceil((x1 - G[0]) / A - o) + 1; i++) {
        const C = [(i + o) * A + G[0], cy, (k + o) * A + G[2]], sec = section(C, [0, ys, 0], [1, 0, 0], [0, 0, 1]); if (sec) secs.push({ C, sec });
      }
    }
    secs.forEach(({ C, sec }) => {
      const col = seaRGB(densOf(C)); pathOn(c, sec.poly, map); c.fillStyle = `rgb(${col[0] | 0},${col[1] | 0},${col[2] | 0})`; c.fill();
      if (inner) { const [nx, ny] = map([C[0], C[2]]), g = c.createRadialGradient(nx, ny, 0, nx, ny, cellPx * .62); g.addColorStop(0, "rgba(255,248,226,.16)"); g.addColorStop(1, "rgba(255,248,226,0)"); c.fillStyle = g; c.fill(); }
    });
    secs.forEach(({ sec }) => {
      pathOn(c, sec.poly, map);
      if (inner) { c.save(); c.clip(); c.strokeStyle = "rgba(60,90,210,.35)"; c.lineWidth = mem * 7; c.stroke(); c.strokeStyle = `rgba(${MEM_COL},.22)`; c.lineWidth = mem * 2.2; c.stroke(); c.strokeStyle = `rgba(${MEM_COL},.42)`; c.lineWidth = mem * .7; c.stroke(); c.restore(); c.strokeStyle = "rgba(5,9,18,.85)"; c.lineWidth = 1.2; c.stroke(); }
      else { c.strokeStyle = cellPx > 14 ? `rgba(${MEM_COL},${Math.min(.5, .14 + cellPx / 200).toFixed(2)})` : "rgba(5,9,18,.65)"; c.lineWidth = cellPx > 14 ? 1 : .7; c.stroke(); }
    });
    secs.forEach(({ C, sec }) => { const hh = Math.abs(sec.h); if (hh < NUC_R) { const [nx, ny] = map([C[0], C[2]]); nucleus(c, nx, ny, Math.sqrt(NUC_R * NUC_R - hh * hh) * f.s, hh / NUC_R); } });
    // the chart's slice: a bar with a handle; drag it to slice the chart at another height
    const zs = sliceZ(), y = sideY(zs, f); barY = y;
    c.beginPath(); c.moveTo(60, y); c.lineTo(W, y); c.strokeStyle = "rgba(5,9,18,.7)"; c.lineWidth = 4; c.stroke();
    c.strokeStyle = "rgba(179,255,89,.95)"; c.lineWidth = 1.5; c.stroke();
    c.beginPath(); c.roundRect ? c.roundRect(W - 74, y - 9, 66, 18, 4) : c.rect(W - 74, y - 9, 66, 18); c.fillStyle = "#b3ff59"; c.fill();
    c.font = "600 10px 'JetBrains Mono', monospace"; c.textAlign = "center"; c.textBaseline = "middle"; c.fillStyle = "#050912"; c.fillText("▲ slice ▼", W - 41, y + .5);
    c.font = "500 9.5px 'JetBrains Mono', monospace"; c.textAlign = "left"; c.fillStyle = "rgba(5,9,18,.75)"; const zt = zText(zs), tw = c.measureText(zt).width, ly = y < 44 ? y + 10.5 : y - 10.5; c.fillRect(62, ly - 6.5, tw + 6, 13); c.fillStyle = "rgba(179,255,89,.95)"; c.fillText(zt, 65, ly);
    c.restore();
  }

  // the side view's sea at cell zoom: the cells cut upright, pixel by pixel (the polygon pass above takes over when they are big)
  function sidePixels(ctx2, fw, fh, step, f) {
    if (A * f.s >= POLY_PX) { ctx2.fillStyle = "#050912"; ctx2.fillRect(0, 0, fw, fh); return; }
    pixels(ctx2, fw, fh, step, (px, py) => [(px - W / 2) / f.s + view.cx, f.sliceY, f.zc - (py - SH / 2 - 8) / f.s], A * f.s);
  }

  // the course profile: the cells a vertical sheet along the course cuts, segment by segment
  const profCells = new Map();
  function profile(c, path, cum, at, Y, zmin, zmax, top, bot, pxPerLy) {
    if (A * pxPerLy < 4) return;
    let polys = profCells.get(path.sig + "|" + zmin.toFixed(0) + "|" + zmax.toFixed(0));
    if (!polys) {
      polys = [];
      const pts = path.pts;
      for (let i = 0; i < pts.length - 1; i++) {
        const [ax, ay] = pts[i], [bx, by] = pts[i + 1], L = Math.hypot(bx - ax, by - ay); if (L < 1e-6) continue;
        const U = [(bx - ax) / L, (by - ay) / L, 0], O = [ax, ay, 0], seen = new Set();
        for (let u = 0; u <= L; u += A / 3) for (let z = zmin; z <= zmax; z += A / 3) {
          const C = cellAt(ax + U[0] * u, ay + U[1] * u, z), k = keyOf(C); if (seen.has(k)) continue; seen.add(k);
          const sec = section(C, O, U, [0, 0, 1], [[-1, 0, 0], [1, 0, L]]); if (!sec) continue;
          polys.push({ poly: sec.poly.map(p => [cum[i] + p[0], p[1]]), h: Math.abs(sec.h), nu: cum[i] + sec.cu, nz: sec.cv });
        }
      }
      if (profCells.size > 30) profCells.clear();
      profCells.set(path.sig + "|" + zmin.toFixed(0) + "|" + zmax.toFixed(0), polys);
    }
    c.save(); c.beginPath(); c.rect(at(0), top, at(cum[cum.length - 1]) - at(0), bot - top); c.clip();
    polys.forEach(p => {
      pathOn(c, p.poly, q => [at(q[0]), Y(q[1])]); c.strokeStyle = `rgba(${MEM_COL},.26)`; c.lineWidth = 1; c.stroke();
      if (p.h < NUC_R) { c.beginPath(); c.arc(at(p.nu), Y(p.nz), Math.max(1.2, Math.sqrt(NUC_R * NUC_R - p.h * p.h) * pxPerLy), 0, 7); c.fillStyle = "rgba(255,240,205,.3)"; c.fill(); }
    });
    c.restore();
  }

  // the cells a course passes through, for the dots along it
  function courseCells(path) {
    if (path._oc) return path._oc;
    const out = [], seen = new Set();
    for (let i = 0; i < path.pts.length - 1; i++) {
      const [ax, ay] = path.pts[i], [bx, by] = path.pts[i + 1], za = path.zs[i], zb = path.zs[i + 1], L = Math.hypot(bx - ax, by - ay), n = Math.max(1, Math.ceil(L / 2));
      for (let t = 0; t <= n; t++) { const u = t / n, C = cellAt(ax + (bx - ax) * u, ay + (by - ay) * u, za + (zb - za) * u), k = keyOf(C); if (!seen.has(k)) { seen.add(k); out.push({ x: C[0], y: C[1], z: C[2] }); } }
    }
    return (path._oc = out);
  }
  function shift(dz, live) { off = dz === 0 ? 0 : off + dz; profCache = null; if (live) dirty = true; else { sideView = null; fieldView = null; schedule(true); } needs = true; }
  // dragging the slice bar: the slice follows the pointer by the distance it moved, and the panel's window stays
  // where it was, scrolling only when the bar reaches its edge, so nothing moves under the pointer
  let drag = null;
  function dragStart(y, f) { drag = { z0: sliceZ(), y0: y, s: f.s, zc: f.zc }; }
  function dragMove(y) {
    if (!drag) return;
    const z = Math.max(-DISK_HALF - 100, Math.min(DISK_HALF + 100, drag.z0 + (drag.y0 - y) / drag.s)), half = (SH / 2 - 30) / drag.s;
    if (z > drag.zc + half) drag.zc = z - half; else if (z < drag.zc - half) drag.zc = z + half;
    off = z - baseZ(); profCache = null; dirty = true; needs = true;
  }
  function dragEnd() { drag = null; off = Math.round(off * 10) / 10; sideView = null; fieldView = null; schedule(true); needs = true; }
  const dragZc = () => drag ? drag.zc : null;
  // the slice bar: a drag in the side view sets the chart's slice height from the pointer's height

  const hitBar = y => barY !== null && Math.abs(y - barY) < 12;
  const sliceText = () => `cells sliced ${zText(sliceZ())}${Math.abs(off) >= .05 ? ` (${off > 0 ? "+" : "−"}${dist(Math.abs(off))} from the focus)` : ""}`;
  return { A, NUC_R, tags, ZONES, RINGS, cellAt, densOf, section, zoneIx, regionAt, regionHTML, sliceZ, renderChart, overlay, side, profile, courseCells, shift, dragStart, dragMove, dragEnd, dragZc, hitBar, cellZoom, baseZ, sliceText, sidePixels, takeDirty, POLY_PX, get off() { return off; } };
})();
