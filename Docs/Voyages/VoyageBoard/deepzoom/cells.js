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

  // ── the foam (mockup round 3): the BCC lattice loosened into a wet foam ──
  // Each lattice point holds one cell, nudged off its point and fuller or emptier than its neighbours; where the sea's
  // field says so it splits into a knot of small cells. A cell is the space nearer its nucleus than any other once
  // each nucleus's weight is counted (a power diagram), so it stays a convex polyhedron and "which cell am I in" stays
  // a lookup. Everything is hashed from the lattice point, so any patch of the galaxy builds the same way every time.
  const FOAM = { jit: .12, size: .2, fine: .42, field: .16, wet: .5 };
  const hU = (a, b, c, d) => { let h = Math.imul(a | 0, 73856093) ^ Math.imul(b | 0, 19349663) ^ Math.imul(c | 0, 83492791) ^ Math.imul(d | 0, 2654435761); h = Math.imul(h ^ (h >>> 13), 1274126177); h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  const hG = (a, b, c, d) => { const u = Math.max(1e-9, hU(a, b, c, d)), v = hU(a + 7, b - 3, c + 11, d + 5); return Math.sqrt(-2 * Math.log(u)) * Math.cos(6.2832 * v); };
  const vn3 = (x, y, z, s) => { const xi = Math.floor(x), yi = Math.floor(y), zi = Math.floor(z), fx = x - xi, fy = y - yi, fz = z - zi, sm = t => t * t * (3 - 2 * t), u = sm(fx), v = sm(fy), w = sm(fz), L = (a, b, t) => a + (b - a) * t, h = (a, b, c) => hU(a, b, c, s);
    return L(L(L(h(xi, yi, zi), h(xi + 1, yi, zi), u), L(h(xi, yi + 1, zi), h(xi + 1, yi + 1, zi), u), v), L(L(h(xi, yi, zi + 1), h(xi + 1, yi, zi + 1), u), L(h(xi, yi + 1, zi + 1), h(xi + 1, yi + 1, zi + 1), u), v), w); };
  const siteCache = new Map();
  // the cells a lattice point holds; i, j, k count cells from the Heart, o = 0 or 1 picks the sublattice
  function sitesOf(i, j, k, o) {
    const key = i + "," + j + "," + k + "," + o; let S = siteCache.get(key);
    if (S) return S;
    const lx = i + o / 2, ly = j + o / 2, lz = k + o / 2, L = [lx * A + G[0], ly * A + G[1], lz * A + G[2]], heart = !i && !j && !k && !o;
    const jit = heart ? 0 : FOAM.jit * A;
    const P = [L[0] + hG(i, j, k, o * 2 + 1) * jit, L[1] + hG(i, j, k, o * 2 + 21) * jit, L[2] + hG(i, j, k, o * 2 + 41) * jit];
    let w = (heart ? 0 : FOAM.size * .5 * hG(i, j, k, o * 2 + 61)) + FOAM.field * (vn3(lx / 2.4 + 21, ly / 2.4 + 2, lz / 2.4 + 8, 91) - .5);
    S = [];
    const f = heart ? 0 : vn3(lx / 1.7 + 3, ly / 1.7 + 11, lz / 1.7 + 5, 17);
    if (f >= 1 - FOAM.fine) {
      const n = f > 1 - FOAM.fine * .3 ? 5 : f > 1 - FOAM.fine * .6 ? 3 : 1, sh = n > 1 ? .05 : .03, rr = (n > 1 ? .2 : .26) * A;
      for (let m = 0; m < n; m++) { const d = [hG(i, j, k, 100 + m * 3 + o), hG(i, j, k, 200 + m * 3 + o), hG(i, j, k, 300 + m * 3 + o)], dl = Math.hypot(...d) || 1;
        S.push({ key: key + "/" + (m + 1), L, p: [P[0] + d[0] / dl * rr, P[1] + d[1] / dl * rr, P[2] + d[2] / dl * rr], w: -sh, h: hU(i, j, k, 400 + m + o * 9) }); }
      w -= sh;
    }
    S.unshift({ key, L, p: P, w, h: hU(i, j, k, 500 + o) });
    for (const s of S) { s.w *= .55 * A * A; s.nucS = 1 + .16 * hG(s.h * 1e6 | 0, 3, 5, 7); }
    if (siteCache.size > 200000) siteCache.clear();
    siteCache.set(key, S); return S;
  }
  // every cell whose lattice point lies within r lattice steps of a point
  function sitesNear(x, y, z, r) {
    const out = [];
    for (const o of [0, 1]) { const ci = Math.round((x - G[0]) / A - o / 2), cj = Math.round((y - G[1]) / A - o / 2), ck = Math.round((z - G[2]) / A - o / 2);
      for (let i = ci - r; i <= ci + r; i++) for (let j = cj - r; j <= cj + r; j++) for (let k = ck - r; k <= ck + r; k++) for (const s of sitesOf(i, j, k, o)) out.push(s); }
    return out;
  }
  const pow = (s, x, y, z) => (s.p[0] - x) ** 2 + (s.p[1] - y) ** 2 + (s.p[2] - z) ** 2 - s.w;
  // the cell a point is in: the nucleus with the least weighted distance. The returned position carries its cell.
  function cellAt(x, y, z) {
    let best = null, bd = Infinity;
    for (const s of sitesNear(x, y, z, 1)) { const d = pow(s, x, y, z); if (d < bd) { bd = d; best = s; } }
    const C = [best.p[0], best.p[1], best.p[2]]; C.key = best.key; C.site = best; return C;
  }
  const keyOf = c => c.key || (Math.round((c[0] - G[0]) * 2 / A) + "," + Math.round((c[1] - G[1]) * 2 / A) + "," + Math.round((c[2] - G[2]) * 2 / A));
  const nCache = new Map();
  // a knot of small cells shares its lattice point's sea
  function densOf(c) {
    const L = c.site ? c.site.L : c, k = L[0].toFixed(1) + "," + L[1].toFixed(1) + "," + L[2].toFixed(1); let n = nCache.get(k);
    if (n === undefined) { n = dens(L[0], L[1], L[2]); if (nCache.size > 300000) nCache.clear(); nCache.set(k, n); }
    return n;
  }
  // a cell's polyhedron, in coordinates about its nucleus
  const polyCache = new Map(), EPSF = 1e-7;
  const sub3 = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]], add3 = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]], mul3 = (a, k) => [a[0] * k, a[1] * k, a[2] * k];
  const cross3 = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]], len3 = a => Math.hypot(a[0], a[1], a[2]);
  function clip3(faces, n, d, nb) {
    let any = false; for (const f of faces) { for (const p of f.pts) if (dot(n, p) - d > EPSF) { any = true; break; } if (any) break; }
    if (!any) return faces;
    const out = [], cap = [];
    for (const f of faces) { const o2 = [], P = f.pts;
      for (let i = 0; i < P.length; i++) { const p = P[i], q = P[(i + 1) % P.length], fp = dot(n, p) - d, fq = dot(n, q) - d;
        if (fp <= EPSF) { o2.push(p); if (fp >= -EPSF) cap.push(p); }
        if ((fp < -EPSF && fq > EPSF) || (fp > EPSF && fq < -EPSF)) { const r = add3(p, mul3(sub3(q, p), fp / (fp - fq))); o2.push(r); cap.push(r); } }
      if (o2.length >= 3) out.push({ pts: o2, nb: f.nb }); }
    const u = []; for (const p of cap) if (!u.some(q => Math.abs(q[0] - p[0]) + Math.abs(q[1] - p[1]) + Math.abs(q[2] - p[2]) < 1e-5)) u.push(p);
    if (u.length >= 3) { const c = mul3(u.reduce(add3, [0, 0, 0]), 1 / u.length), nn = mul3(n, 1 / len3(n)), ax = Math.abs(nn[0]) < .9 ? [1, 0, 0] : [0, 1, 0], U = cross3(nn, ax), V = cross3(nn, U);
      u.sort((a, b) => Math.atan2(dot(sub3(a, c), V), dot(sub3(a, c), U)) - Math.atan2(dot(sub3(b, c), V), dot(sub3(b, c), U))); out.push({ pts: u, nb }); }
    return out;
  }
  function polyOf(s) {
    let F = polyCache.get(s.key); if (F) return F;
    const h = 1.3 * A, v = (x, y, z) => [x * h, y * h, z * h];
    F = [{ pts: [v(1, -1, -1), v(1, 1, -1), v(1, 1, 1), v(1, -1, 1)] }, { pts: [v(-1, -1, -1), v(-1, -1, 1), v(-1, 1, 1), v(-1, 1, -1)] }, { pts: [v(-1, 1, -1), v(-1, 1, 1), v(1, 1, 1), v(1, 1, -1)] },
      { pts: [v(-1, -1, -1), v(1, -1, -1), v(1, -1, 1), v(-1, -1, 1)] }, { pts: [v(-1, -1, 1), v(1, -1, 1), v(1, 1, 1), v(-1, 1, 1)] }, { pts: [v(-1, -1, -1), v(-1, 1, -1), v(1, 1, -1), v(1, -1, -1)] }];
    F.forEach(f => f.nb = null);
    const cand = [];
    for (const q of sitesNear(s.p[0], s.p[1], s.p[2], 2)) if (q !== s) { const d = sub3(q.p, s.p), dd = dot(d, d); if (dd < (2.1 * A) ** 2) cand.push([dd, q, d]); }
    cand.sort((a, b) => a[0] - b[0]);
    let maxR = h * Math.sqrt(3);
    for (const [dd, q, n] of cand) { const nl = Math.sqrt(dd), d = (dd - q.w + s.w) / 2; if (d / nl > maxR) continue;
      F = clip3(F, n, d, q); if (F.length < 4) break;
      maxR = 0; for (const f of F) for (const p of f.pts) maxR = Math.max(maxR, len3(p)); }
    // its size, for its nucleus
    let vol = 0, cen = [0, 0, 0];
    for (const f of F) for (let i = 1; i < f.pts.length - 1; i++) { const a = f.pts[0], b = f.pts[i], c = f.pts[i + 1], v = dot(a, cross3(b, c)) / 6; vol += v; cen = add3(cen, mul3(add3(a, add3(b, c)), v / 4)); }
    F.vol = Math.abs(vol); const ce = vol ? mul3(cen, 1 / vol) : [0, 0, 0];
    // the nucleus sits at the cell's middle, sized to the cell and never crowding its walls
    s.c = add3(s.p, ce); const cr = Math.cbrt(F.vol * 3 / (4 * Math.PI));
    s.nucR = Math.min(cr * .32, NUC_R * s.nucS * Math.cbrt(Math.max(.05, F.vol / (A * A * A / 2))));
    if (polyCache.size > 60000) polyCache.clear();
    polyCache.set(s.key, F); return F;
  }
  // the section of one cell by a plane P = O + u U + v V: a polygon in (u, v), or null. C is a point the cell holds
  // (a lattice point or a position cellAt returned).
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
    const s = C.site || cellAt(C[0], C[1], C[2]).site, F = polyOf(s);
    const N = [U[1] * V[2] - U[2] * V[1], U[2] * V[0] - U[0] * V[2], U[0] * V[1] - U[1] * V[0]];
    const d = [s.p[0] - O[0], s.p[1] - O[1], s.p[2] - O[2]], cu = dot(d, U), cv = dot(d, V), hgt = dot(d, N);
    const pts = [];
    for (const f of F) { const P = f.pts;
      for (let i = 0; i < P.length; i++) { const p = P[i], q = P[(i + 1) % P.length], fp = dot(p, N) + hgt + 1.3e-5, fq = dot(q, N) + hgt + 1.3e-5;
        if ((fp < 0) !== (fq < 0)) { const t = fp / (fp - fq), r = [p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t, p[2] + (q[2] - p[2]) * t]; pts.push([cu + dot(r, U), cv + dot(r, V)]); } } }
    if (pts.length < 3) return null;
    const mx = pts.reduce((a, p) => a + p[0], 0) / pts.length, my = pts.reduce((a, p) => a + p[1], 0) / pts.length;
    pts.sort((a, b) => Math.atan2(a[1] - my, a[0] - mx) - Math.atan2(b[1] - my, b[0] - mx));
    let poly = []; for (const p of pts) if (!poly.length || Math.abs(p[0] - poly[poly.length - 1][0]) + Math.abs(p[1] - poly[poly.length - 1][1]) > 1e-4) poly.push(p);
    if (poly.length > 2 && Math.abs(poly[0][0] - poly[poly.length - 1][0]) + Math.abs(poly[0][1] - poly[poly.length - 1][1]) < 1e-4) poly.pop();
    if (poly.length < 3) return null;
    if (extra) for (const e of extra) { poly = clip(poly, e[0], e[1], e[2]); if (poly.length < 3) return null; }
    return { poly, cu, cv, h: hgt, site: s };
  }
  // every cell a plane z = zs crosses inside a box
  function sitesCrossing(x0, x1, y0, y1, zs) {
    const out = [];
    for (const o of [0, 1]) for (let k = Math.floor((zs - G[2]) / A - o / 2) - 1; k <= Math.ceil((zs - G[2]) / A - o / 2) + 1; k++)
      for (let j = Math.floor((y0 - G[1]) / A - o / 2) - 1; j <= Math.ceil((y1 - G[1]) / A - o / 2) + 1; j++) for (let i = Math.floor((x0 - G[0]) / A - o / 2) - 1; i <= Math.ceil((x1 - G[0]) / A - o / 2) + 1; i++)
        for (const s of sitesOf(i, j, k, o)) if (Math.abs(s.p[2] - zs) < A * 1.1) out.push(s);
    return out;
  }
  // a cell's outline pulled in by the liquid between cells and rounded at its corners (a wet foam)
  function wetOutline(poly, dl, rad) {
    let P = poly.slice(); const n = P.length; if (n < 3) return null;
    let A2 = 0; for (let i = 0; i < n; i++) { const a = P[i], b = P[(i + 1) % n]; A2 += a[0] * b[1] - b[0] * a[1]; }
    if (A2 < 0) P.reverse();
    const Ls = [];
    for (let i = 0; i < n; i++) { const a = P[i], b = P[(i + 1) % n], dx = b[0] - a[0], dy = b[1] - a[1], l = Math.hypot(dx, dy) || 1, nx = -dy / l, ny = dx / l; Ls.push([nx, ny, a[0] * nx + a[1] * ny + dl]); }
    let Q = [];
    for (let i = 0; i < n; i++) { const [a1, b1, c1] = Ls[(i + n - 1) % n], [a2, b2, c2] = Ls[i], det = a1 * b2 - a2 * b1; if (Math.abs(det) < 1e-9) continue; Q.push([(c1 * b2 - c2 * b1) / det, (a1 * c2 - a2 * c1) / det]); }
    let ok = Q.length === n;
    if (ok) for (let i = 0; i < n; i++) { const a = P[i], b = P[(i + 1) % n], qa = Q[i], qb = Q[(i + 1) % n]; if ((b[0] - a[0]) * (qb[0] - qa[0]) + (b[1] - a[1]) * (qb[1] - qa[1]) <= 0) { ok = false; break; } }
    if (!ok) { const c = P.reduce((s, p) => [s[0] + p[0] / n, s[1] + p[1] / n], [0, 0]), R = Math.max(...P.map(p => Math.hypot(p[0] - c[0], p[1] - c[1]))), k = Math.max(.2, 1 - dl / R * 1.4); Q = P.map(p => [c[0] + (p[0] - c[0]) * k, c[1] + (p[1] - c[1]) * k]); }
    for (let pass = 0; pass < 3 && Q.length > 3; pass++) { const R = [];
      for (let i = 0; i < Q.length; i++) { const p = Q[(i + Q.length - 1) % Q.length], v = Q[i], nx = Q[(i + 1) % Q.length], ax = v[0] - p[0], ay = v[1] - p[1], bx = nx[0] - v[0], by = nx[1] - v[1], la = Math.hypot(ax, ay), lb = Math.hypot(bx, by);
        if (la < .025 * A && R.length) continue; if (la > 1e-6 && lb > 1e-6 && (ax * by - ay * bx) / (la * lb) < .1 && ax * bx + ay * by > 0) continue; R.push(v); }
      if (R.length < 3) break; Q = R; }
    let ar = 0; for (let i = 0; i < Q.length; i++) { const a = Q[i], b = Q[(i + 1) % Q.length]; ar += a[0] * b[1] - b[0] * a[1]; }
    if (ar / 2 < .02 * A * A) return null;
    const cx = Q.reduce((s, p) => s + p[0], 0) / Q.length, cy = Q.reduce((s, p) => s + p[1], 0) / Q.length; let minW = Infinity;
    for (let i = 0; i < Q.length; i++) { const a = Q[i], b = Q[(i + 1) % Q.length], l = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1; minW = Math.min(minW, Math.abs((b[0] - a[0]) * (cy - a[1]) - (b[1] - a[1]) * (cx - a[0])) / l); }
    if (minW < .05 * A) return null;
    return { pts: Q, rad };
  }
  function roundPath(o, map) {
    const P = new Path2D(), Q = o.pts, n = Q.length, S = [], E = [];
    for (let i = 0; i < n; i++) { const v = Q[i], p = Q[(i + n - 1) % n], q = Q[(i + 1) % n], lp = Math.hypot(p[0] - v[0], p[1] - v[1]) || 1, lq = Math.hypot(q[0] - v[0], q[1] - v[1]) || 1, tp = Math.min(.5, o.rad / lp), tq = Math.min(.5, o.rad / lq);
      S.push([v[0] + (p[0] - v[0]) * tp, v[1] + (p[1] - v[1]) * tp]); E.push([v[0] + (q[0] - v[0]) * tq, v[1] + (q[1] - v[1]) * tq]); }
    const m = q => map(q); let a = m(S[0]); P.moveTo(a[0], a[1]);
    for (let i = 0; i < n; i++) { const s = m(S[i]), v = m(Q[i]), e = m(E[i]); P.lineTo(s[0], s[1]); P.quadraticCurveTo(v[0], v[1], e[0], e[1]); }
    P.closePath(); return P;
  }
  const offCv = {}; function offscreen(name, w, h) { let c = offCv[name]; if (!c) c = offCv[name] = document.createElement("canvas"); if (c.width !== w || c.height !== h) { c.width = w; c.height = h; } const g = c.getContext("2d"); g.setTransform(1, 0, 0, 1, 0, 0); g.filter = "none"; g.globalAlpha = 1; g.globalCompositeOperation = "source-over"; g.clearRect(0, 0, w, h); return [c, g]; }

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
    const pad = cellPx * .3, [x0, y0] = toWorld(-pad, -pad), [x1, y1] = toWorld(W + pad, H + pad);
    const map = p => [(p[0] - view.cx) * s + W / 2, (p[1] - view.cy) * s + H / 2];
    const dl = FOAM.wet * .035 * A, rc = FOAM.wet * .24 * A, memA = Math.max(0, Math.min(1, (cellPx - 22) / 22));
    const sliceAt = z => { const out = []; for (const st of sitesCrossing(x0, x1, y0, y1, z)) { const sec = section({ site: st }, [0, 0, z], [1, 0, 0], [0, 1, 0]); if (!sec) continue; const o = wetOutline(sec.poly, dl, rc); if (o) out.push({ st, sec, path: roundPath(o, map), o }); } return out; };
    const cells = sliceAt(zs);
    // the liquid between cells, thickest where three meet
    c2.fillStyle = `rgba(${MEM_COL},${(.1 + .1 * FOAM.wet) * Math.max(.4, memA)})`; c2.fillRect(0, 0, W, H);
    // the cells, blurred: the sea's colour inside, a lit shell at the rim, brightest round the nucleus
    {
      const [cv, g] = offscreen("cells", Math.round(W * DPR), Math.round(H * DPR)); g.setTransform(DPR, 0, 0, DPR, 0, 0);
      for (const { st, path } of cells) {
        const [r, gg, b] = seaRGB(densOf(st.p.site ? st.p : Object.assign(st.p.slice(), { site: st }))), [nx, ny] = (polyOf(st), map(st.c)), R = Math.cbrt(polyOf(st).vol * 3 / (4 * Math.PI)) * s;
        g.fillStyle = `rgb(${r * .34 | 0},${gg * .36 | 0},${b * .44 | 0})`; g.fill(path);
        g.save(); g.clip(path);
        const gr = g.createRadialGradient(nx, ny, 0, nx, ny, R * 1.15);
        gr.addColorStop(0, `rgba(${Math.min(255, r * 1.5) | 0},${Math.min(255, gg * 1.5) | 0},${Math.min(255, b * 1.5) | 0},.45)`); gr.addColorStop(.6, `rgba(${r | 0},${gg | 0},${b | 0},.18)`); gr.addColorStop(1, `rgba(${r | 0},${gg | 0},${b | 0},0)`);
        g.fillStyle = gr; g.fillRect(nx - R * 1.3, ny - R * 1.3, R * 2.6, R * 2.6);
        if (cellPx > 50) { g.strokeStyle = `rgba(${MEM_COL},.22)`; g.lineWidth = Math.max(2, cellPx * .07); g.stroke(path);
          g.strokeStyle = `rgba(${Math.min(255, r * 1.8) | 0},${Math.min(255, gg * 1.8) | 0},${Math.min(255, b * 1.8) | 0},.22)`; g.lineWidth = Math.max(3, cellPx * .16); g.stroke(path); }
        g.restore();
      }
      c2.save(); c2.setTransform(1, 0, 0, 1, 0, 0); c2.filter = `blur(${Math.max(0, cellPx * .027 * DPR).toFixed(1)}px)`; c2.drawImage(cv, 0, 0); c2.restore();
    }
    // the cells above and below the slice, out of focus
    if (cellPx > 60) for (const [dzs, al, bl] of [[[-.55, .5], .2, .035], [[-.27, .27], .32, .014]]) {
      const [cv, g] = offscreen("deep", Math.round(W * DPR), Math.round(H * DPR)); g.setTransform(DPR, 0, 0, DPR, 0, 0);
      g.strokeStyle = `rgba(${MEM_COL},1)`; g.lineWidth = Math.max(1.5, cellPx * .016);
      for (const dz of dzs) for (const v of sliceAt(zs + dz * A)) g.stroke(v.path);
      c2.save(); c2.setTransform(1, 0, 0, 1, 0, 0); c2.globalAlpha = al * memA; c2.filter = `blur(${Math.max(1, cellPx * bl * DPR).toFixed(1)}px)`; c2.drawImage(cv, 0, 0); c2.restore();
    }
    // cytoplasm: cosmic snow drifting inside each cell
    if (cellPx > 60) for (const { st, path } of cells) {
      const R = Math.cbrt(polyOf(st).vol * 3 / (4 * Math.PI)), n = Math.min(160, Math.round((R / A) ** 2 * 70 * (.6 + .8 * st.h) * Math.min(2.2, cellPx / 240)));
      c2.save(); c2.clip(path);
      for (let k = 0; k < n; k++) { const h1 = hU(k, 3, st.h * 1e6, 1), h2 = hU(k, 9, st.h * 1e6, 2), h3 = hU(k, 21, st.h * 1e6, 3), a = h1 * 6.283, rr = Math.sqrt(h2) * R * 1.1;
        const [sx, sy] = map([st.c[0] + Math.cos(a) * rr, st.c[1] + Math.sin(a) * rr]), sz = Math.max(.8, cellPx * .006 * (.5 + h3));
        c2.fillStyle = `rgba(220,235,255,${.3 + .55 * h3 * h3})`; c2.beginPath(); c2.arc(sx, sy, sz / 2, 0, 7); c2.fill(); }
      c2.restore();
    }
    // membranes: each cell's own wall, so neighbours show as a pair of lines with the liquid between
    if (memA > 0) { c2.lineJoin = "round"; for (const { st, path } of cells) { c2.strokeStyle = `rgba(${MEM_COL},${(.55 + .3 * st.h) * memA})`; c2.lineWidth = Math.max(1, cellPx * .008) * (.8 + .5 * st.h); c2.stroke(path); } }
    else for (const { path } of cells) { c2.strokeStyle = "rgba(5,9,18,.5)"; c2.lineWidth = .7; c2.stroke(path); }
    // nuclei: where the slice cuts one, its section; one above or below shows as a soft haze
    if (cellPx > 22) for (const { st } of sitesCrossing(x0, x1, y0, y1, zs).map(st => ({ st }))) {
      polyOf(st); const h = Math.abs(st.c[2] - zs), R = st.nucR, [nx, ny] = map(st.c);
      if (h < R) nucleus(c2, nx, ny, Math.sqrt(R * R - h * h) * s, h / R);
      else if (h < R + .9 * A / 20 * 20 && cellPx > 50) { const a = .5 * Math.exp(-(((h - R) / (.35 * A)) ** 2)), hz = R * s * (1.6 + h / A * 3); if (a < .02) continue;
        const gr = c2.createRadialGradient(nx, ny, 0, nx, ny, hz); gr.addColorStop(0, `rgba(235,242,255,${.4 * a})`); gr.addColorStop(.5, `rgba(150,200,255,${.15 * a})`); gr.addColorStop(1, "rgba(150,200,255,0)");
        c2.fillStyle = gr; c2.beginPath(); c2.arc(nx, ny, hz, 0, 7); c2.fill(); }
    }
    if (closed.size && cellPx > 6) cells.forEach(({ st, path }) => { if (!closed.has(KEY(...hexOf(st.p[0], st.p[1])))) return; c2.strokeStyle = "rgba(255,233,168,.45)"; c2.lineWidth = 1; c2.stroke(path); });
  }

  // the nucleus as the game draws it (Nucleus.prefab, CageMaterial): a white translucent sphere in a cage with
  // deep-blue edges. r is its radius on screen; cut is how far above or below its middle the slice passes
  function nucleus(c, x, y, r, cut) {
    r = Math.max(1.5, r);
    if (r > 6) { const halo = c.createRadialGradient(x, y, r * .8, x, y, r * 1.9); halo.addColorStop(0, "rgba(150,200,255,.22)"); halo.addColorStop(1, "rgba(150,200,255,0)"); c.beginPath(); c.arc(x, y, r * 1.9, 0, 7); c.fillStyle = halo; c.fill(); }
    const body = c.createRadialGradient(x - r * .3, y - r * .35, r * .05, x, y, r);
    body.addColorStop(0, "rgba(235,242,255,.55)"); body.addColorStop(.75, "rgba(235,242,255,.18)"); body.addColorStop(1, "rgba(150,200,255,.4)");
    c.beginPath(); c.arc(x, y, r, 0, 7); c.fillStyle = body; c.fill();
    if (r > 9) { const q = Math.sqrt(Math.max(0, 1 - cut * cut)); c.lineWidth = Math.max(.8, r / 45); c.strokeStyle = "rgba(83,89,192,.75)";
      for (let k = 0; k < 4; k++) { const ang = k * .785 + .3; c.beginPath(); c.ellipse(x, y, r, r * Math.max(.1, Math.abs(Math.cos(ang * 1.7)) * q + .05), ang, 0, 7); c.stroke(); } }
    c.lineWidth = Math.max(1.2, r / 18); c.strokeStyle = "rgba(0,6,191,.9)"; c.beginPath(); c.arc(x, y, r, 0, 7); c.stroke();
    c.lineWidth = Math.max(.8, r / 40); c.strokeStyle = "rgba(150,200,255,.8)"; c.beginPath(); c.arc(x, y, r * .97, 0, 7); c.stroke();
  }

  // drawn every frame over the chart: region names, the centre's rings, and inside one cell its levels
  function overlay(c) {
    const s = scale(), cellPx = A * s, labels = [];
    c.save();
    // the chart's bottom edge is the line the side view cuts along: mark it in the slice bar's green so the two lock up
    c.strokeStyle = "rgba(179,255,89,.6)"; c.lineWidth = 1.5; c.beginPath(); c.moveTo(0, H - 1); c.lineTo(W, H - 1); c.stroke();
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
      for (const st of sitesNear(view.cx, view.cy, zs, 1)) { const d = (st.p[0] - view.cx) ** 2 + (st.p[1] - view.cy) ** 2 + 4 * (st.p[2] - zs) ** 2; if (d < best) { best = d; C = Object.assign(st.p.slice(), { site: st, key: st.key }); } }
      polyOf(C.site); const NUC_R = C.site.nucR; C[0] = C.site.c[0]; C[1] = C.site.c[1]; C[2] = C.site.c[2];
      const sec = section(C, [0, 0, zs], [1, 0, 0], [0, 1, 0]);
      if (sec) {
        const n = densOf(C), [nx, ny] = toScreen(C[0], C[1]), h = C[2] - zs, heart = C.key === "0,0,0,0";
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
        tag(heart ? "Nucleus · Sgr A*, the Heart [A]" : "Nucleus", Math.abs(h) < NUC_R ? `${(NUC_R * 2).toFixed(1)} ly across · omni regrows here` : `${dist(Math.abs(h))} ${h > 0 ? "above" : "below"} this slice · ${(NUC_R * 2).toFixed(1)} ly across`, nx, ny + nucR * s + 18, "#ffeec4");
        tag("Cytoplasm", `${seaName(n)}, ${n < 10 ? n.toFixed(2) : fmt(n)} g/cm³ · flora and fauna live here`, (nx + tx) / 2, (ny + ty) / 2 + 6, "#cfe4ff");
        tag(heart ? "Membrane · the circumnuclear disk [A]" : "Membrane", `${polyOf(C.site).length} faces · about ${Math.round(Math.cbrt(polyOf(C.site).vol))} ly across`, tx, ty + 22, `rgb(${MEM_COL})`);
      }
      c.restore();
    }
  }

  // the side view at cell zoom: the cells cut upright along the chart's bottom edge, filled with their own sea,
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
    for (const o of [0, 1]) for (let j = Math.floor((ys - G[1]) / A - o / 2) - 1; j <= Math.ceil((ys - G[1]) / A - o / 2) + 1; j++)
      for (let k = Math.floor((zBot - G[2]) / A - o / 2) - 1; k <= Math.ceil((zTop - G[2]) / A - o / 2) + 1; k++) for (let i = Math.floor((x0 - G[0]) / A - o / 2) - 1; i <= Math.ceil((x1 - G[0]) / A - o / 2) + 1; i++)
        for (const st of sitesOf(i, j, k, o)) { if (Math.abs(st.p[1] - ys) > A * 1.1) continue; const sec = section({ site: st }, [0, ys, 0], [1, 0, 0], [0, 0, 1]); if (!sec) continue; polyOf(st); const C = Object.assign(st.c.slice(), { site: st, key: st.key }); sec.h = st.c[1] - ys; secs.push({ C, sec }); }
    secs.forEach(({ C, sec }) => {
      const col = seaRGB(densOf(C)); pathOn(c, sec.poly, map); c.fillStyle = `rgb(${col[0] | 0},${col[1] | 0},${col[2] | 0})`; c.fill();
      if (inner) { const [nx, ny] = map([C[0], C[2]]), g = c.createRadialGradient(nx, ny, 0, nx, ny, cellPx * .62); g.addColorStop(0, "rgba(255,248,226,.16)"); g.addColorStop(1, "rgba(255,248,226,0)"); c.fillStyle = g; c.fill(); }
    });
    secs.forEach(({ sec }) => {
      pathOn(c, sec.poly, map);
      if (inner) { c.save(); c.clip(); c.strokeStyle = "rgba(60,90,210,.35)"; c.lineWidth = mem * 7; c.stroke(); c.strokeStyle = `rgba(${MEM_COL},.22)`; c.lineWidth = mem * 2.2; c.stroke(); c.strokeStyle = `rgba(${MEM_COL},.42)`; c.lineWidth = mem * .7; c.stroke(); c.restore(); c.strokeStyle = "rgba(5,9,18,.85)"; c.lineWidth = 1.2; c.stroke(); }
      else { c.strokeStyle = cellPx > 14 ? `rgba(${MEM_COL},${Math.min(.5, .14 + cellPx / 200).toFixed(2)})` : "rgba(5,9,18,.65)"; c.lineWidth = cellPx > 14 ? 1 : .7; c.stroke(); }
    });
    secs.forEach(({ C, sec }) => { const hh = Math.abs(sec.h), R = C.site.nucR || NUC_R; if (hh < R) { const [nx, ny] = map([C[0], C[2]]); nucleus(c, nx, ny, Math.sqrt(R * R - hh * hh) * f.s, hh / R); } });
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
