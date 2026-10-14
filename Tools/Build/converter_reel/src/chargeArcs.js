// The Charge light (the Elemental Converter's seam walk and flux arcs), ported LINE FOR LINE from the shipped C#
// Assets/_Scripts/Utility/ChargeArcs.cs (ChargeArcSettings, ChargeGlowFace, ChargeSeam, ChargeSites, ChargeFluxPath,
// ChargeSeamWalker, ChargeFluxChain, ChargeArcMeshWriter, ChargeArcs) and the settings mirror of
// CrystalChargeArcsProfileSO.Settings. Held to the C# by parity_charge/ (bit-exact on the shipped combine data).
//
// Conventions:
//   - A vector is a plain array [x, y, z] whose components are float32 values (Math.fround'ed). Unity's axes, left-handed,
//     Y up: nothing is converted. Inputs may be any numbers; they are fround'ed on the way in.
//   - Every arithmetic step is rounded to float32 (Math.fround) in the C#'s own evaluation order, so random and hash
//     decisions (System.Random draws, the noise hash, sorts) match the C# exactly. Transcendentals are C#'s
//     (float)Math.Xxx(double) - Math.xxx then fround.
//   - System.Random(seed) is ported (the .NET seeded / legacy subtractive generator, which Unity's Mono also uses).
//   - The UnityEngine maths are the omni turns harness's stub (Tools/Build/omni_turns_harness/UnityStub.cs), which is what
//     the parity harness compiles against: Quaternion.FromToRotation and Quaternion * Vector3 are the stub's formulas
//     (Unity's own are native/differently ordered; they agree to float rounding).
//   - Plain script, no module syntax: attaches CR.ChargeArcs to globalThis.CR (browser and Node).
(function (CR) {
  'use strict';
  const F = Math.fround;

  // ── float literals (C# `0.731f` etc.) ──
  const C = {
    e5: F(1e-5), e6: F(1e-6), e7: F(1e-7), e9: F(1e-9), e12: F(1e-12), e4: F(1e-4),
    PI: F(Math.PI), Deg2Rad: F(Math.PI / 180.0), TwoPI: F(2 * F(Math.PI)),
    p001: F(0.001), p01: F(0.01), p05: F(0.05), p002: F(0.002), p16: F(0.16), p3: F(0.3), p4: F(0.4), p45: F(0.45),
    p6: F(0.6), p8: F(0.8), p2: F(0.2), p25: F(0.25), p731: F(0.731), p75: F(0.75), p9: F(0.9),
    s1_37: F(1.37), s2_91: F(2.91), s1_7: F(1.7), s2_2: F(2.2), s2_4: F(2.4), s1_4: F(1.4), s1_1: F(1.1),
    s19_1: F(19.1), s2_17: F(2.17), s0_999999: F(-0.999999),
  };

  // ── Mathf (the stub's, = Unity's managed definitions) ──
  const Mathf = {
    PI: C.PI,
    Deg2Rad: C.Deg2Rad,
    Abs: (v) => Math.abs(v),
    Max: (a, b) => (a > b ? a : b),
    Min: (a, b) => (a < b ? a : b),
    Clamp01: (v) => (v < 0 ? 0 : v > 1 ? 1 : v),
    Clamp: (v, a, b) => (v < a ? a : v > b ? b : v),
    Sqrt: (v) => F(Math.sqrt(v)),
    Sin: (v) => F(Math.sin(v)),
    Cos: (v) => F(Math.cos(v)),
    Acos: (v) => F(Math.acos(v)),
    Atan2: (y, x) => F(Math.atan2(y, x)),
    Exp: (v) => F(Math.exp(v)),
    Floor: (v) => F(Math.floor(v)),
    Round: (v) => F(roundHalfEven(v)),
    RoundToInt: (v) => roundHalfEven(v) | 0,
    Lerp: (a, b, t) => F(a + F(F(b - a) * Mathf.Clamp01(t))),
  };
  function roundHalfEven(v) { // System.Math.Round(double) (MidpointRounding.ToEven)
    const r = Math.round(v);
    return (Math.abs(v % 1) === 0.5 && r % 2 !== 0) ? r - 1 : r;
  }

  // ── Vector3 (the stub's, = Unity's managed definitions) ──
  const zero = () => [0, 0, 0];
  const v3 = (x, y, z) => [F(x), F(y), F(z)];
  const add = (a, b) => [F(a[0] + b[0]), F(a[1] + b[1]), F(a[2] + b[2])];
  const sub = (a, b) => [F(a[0] - b[0]), F(a[1] - b[1]), F(a[2] - b[2])];
  const mul = (a, s) => [F(a[0] * s), F(a[1] * s), F(a[2] * s)];
  const div = (a, s) => [F(a[0] / s), F(a[1] / s), F(a[2] / s)];
  const dot = (a, b) => F(F(F(a[0] * b[0]) + F(a[1] * b[1])) + F(a[2] * b[2]));
  const cross = (a, b) => [F(F(a[1] * b[2]) - F(a[2] * b[1])), F(F(a[2] * b[0]) - F(a[0] * b[2])), F(F(a[0] * b[1]) - F(a[1] * b[0]))];
  const sqrMag = (a) => dot(a, a);
  const mag = (a) => F(Math.sqrt(dot(a, a)));
  const norm = (a) => { const m = mag(a); return m > C.e5 ? div(a, m) : zero(); };
  const lerpU = (a, b, t) => add(a, mul(sub(b, a), t));
  const lerpV = (a, b, t) => add(a, mul(sub(b, a), Mathf.Clamp01(t)));
  const toV = (p) => [F(p[0]), F(p[1]), F(p[2])];

  // Quaternion: the stub's AngleAxis / FromToRotation / q * v.
  function angleAxis(degrees, axis) {
    const a = norm(axis);
    const h = degrees * Math.PI / 360.0;
    const s = F(Math.sin(h));
    return [F(a[0] * s), F(a[1] * s), F(a[2] * s), F(Math.cos(h))];
  }
  function fromToRotation(from, to) {
    const f = norm(from), t = norm(to), c = cross(f, t), d = dot(f, t);
    if (d < C.s0_999999) {
      let ax = cross([1, 0, 0], f);
      if (sqrMag(ax) < C.e6) ax = cross([0, 1, 0], f);
      return angleAxis(180, ax);
    }
    const q = [c[0], c[1], c[2], F(1 + d)];
    const m = F(Math.sqrt(F(F(F(F(q[0] * q[0]) + F(q[1] * q[1])) + F(q[2] * q[2])) + F(q[3] * q[3]))));
    return [F(q[0] / m), F(q[1] / m), F(q[2] / m), F(q[3] / m)];
  }
  function rotate(q, v) {
    const u = [q[0], q[1], q[2]], w = q[3];
    return add(add(mul(u, F(2 * dot(u, v))), mul(v, F(F(w * w) - dot(u, u)))), mul(cross(u, v), F(2 * w)));
  }

  // float.CompareTo
  function cmpF(a, b) {
    if (a < b) return -1;
    if (a > b) return 1;
    if (a === b) return 0;
    if (Number.isNaN(a)) return Number.isNaN(b) ? 0 : -1;
    return 1;
  }

  // System.Array.Sort / List<T>.Sort(Comparison) for the sizes used here (<= 16): .NET's IntroSort small-partition path
  // (2: one compare-swap; 3: three; else insertion sort). Ported exactly, ties included (JS's own sort is stable; .NET's
  // 3-element path is not).
  function netSort(keys, comparer) {
    const n = keys.length;
    if (n < 2) return keys;
    if (n > 16) throw new Error('netSort: only the IntroSort small-partition path is ported (n <= 16)');
    const swapIfGreater = (i, j) => { if (comparer(keys[i], keys[j]) > 0) { const t = keys[i]; keys[i] = keys[j]; keys[j] = t; } };
    if (n === 2) { swapIfGreater(0, 1); return keys; }
    if (n === 3) { swapIfGreater(0, 1); swapIfGreater(0, 2); swapIfGreater(1, 2); return keys; }
    for (let i = 0; i < n - 1; i++) {
      const t = keys[i + 1];
      let j = i;
      while (j >= 0 && comparer(t, keys[j]) < 0) { keys[j + 1] = keys[j]; j--; }
      keys[j + 1] = t;
    }
    return keys;
  }

  // System.Random(int seed): the .NET seeded generator (Net5CompatSeedImpl = the legacy Knuth subtractive one).
  class NetRandom {
    constructor(seed) {
      seed |= 0;
      const MBIG = 2147483647, MSEED = 161803398;
      const a = new Array(56).fill(0);
      const subtraction = seed === -2147483648 ? MBIG : Math.abs(seed);
      let mj = (MSEED - subtraction) | 0;   // C# int arithmetic wraps; doubles do not
      a[55] = mj;
      let mk = 1;
      for (let i = 1; i < 55; i++) {
        const ii = (21 * i) % 55;
        a[ii] = mk;
        mk = (mj - mk) | 0;
        if (mk < 0) mk = (mk + MBIG) | 0;
        mj = a[ii];
      }
      for (let k = 1; k < 5; k++)
        for (let i = 1; i < 56; i++) {
          a[i] = (a[i] - a[1 + (i + 30) % 55]) | 0;
          if (a[i] < 0) a[i] = (a[i] + MBIG) | 0;
        }
      this.seedArray = a;
      this.inext = 0;
      this.inextp = 21;
    }
    internalSample() {
      const MBIG = 2147483647, a = this.seedArray;
      let n = this.inext, p = this.inextp;
      if (++n >= 56) n = 1;
      if (++p >= 56) p = 1;
      let r = (a[n] - a[p]) | 0;
      if (r === MBIG) r--;
      if (r < 0) r = (r + MBIG) | 0;
      a[n] = r;
      this.inext = n;
      this.inextp = p;
      return r;
    }
    sample() { return this.internalSample() * (1.0 / 2147483647); }
    next(maxValue) { return Math.trunc(this.sample() * maxValue); }
    nextDouble() { return this.sample(); }
  }

  // ── ChargeArcSettings ──
  const LabRaw = {
    Rate: 1, Bolts: 1, Glow: 1,
    SeamStrike: 0.07, SeamDwellMin: 0.1, SeamDwellMax: 0.28, SeamFade: 0.85,
    FluxGap: 0.9, FluxCharge: 0.45, FluxStrike: 0.06, FluxHold: 0.45, FluxFade: 0.45, FluxFork: 0.3,
    FluxMinDegrees: 55, FluxMaxDegrees: 125, FluxBundle: 0.016, FluxTurns: 1,
    CoreWidth: 0.012, WritheSpeed: 3, FlickerSpeed: 4,
    SeamPetalGlow: 0.35, StrikeProtractionFrom: 0.15, StrikeProtractionFull: 0.45,
  };
  function froundSettings(raw) {
    const s = {};
    for (const k of Object.keys(raw)) s[k] = k === 'FluxTurns' ? (raw[k] | 0) : F(raw[k]);
    return s;
  }
  /** ChargeArcSettings.Lab. */
  const Lab = () => froundSettings(LabRaw);
  /** CrystalChargeArcsProfileSO.Settings, from the profile asset's serialized fields (data/charge_arcs.json "profile"). */
  function settings(p) {
    const g = (k) => F(p[k]);
    return {
      Rate: g('rate'), Bolts: g('boltIntensity'), Glow: g('glowIntensity'),
      SeamStrike: g('seamStrikeSeconds'), SeamDwellMin: g('seamDwellMinSeconds'),
      SeamDwellMax: Mathf.Max(g('seamDwellMinSeconds'), g('seamDwellMaxSeconds')),
      SeamFade: g('seamFadeSeconds'),
      FluxGap: g('fluxGapSeconds'), FluxCharge: g('fluxChargeSeconds'), FluxStrike: g('fluxStrikeSeconds'), FluxHold: g('fluxHoldSeconds'),
      FluxFade: g('fluxFadeSeconds'), FluxFork: g('fluxFork'), FluxMinDegrees: g('fluxMinDegrees'),
      FluxMaxDegrees: Mathf.Max(g('fluxMinDegrees'), g('fluxMaxDegrees')),
      FluxBundle: g('fluxBundle'), FluxTurns: p.fluxTurns | 0,
      CoreWidth: g('coreWidth'), WritheSpeed: g('writheSpeed'), FlickerSpeed: g('flickerSpeed'),
      SeamPetalGlow: g('seamPetalGlow'),
      StrikeProtractionFrom: g('strikeProtractionFrom'),
      StrikeProtractionFull: Mathf.Max(g('strikeProtractionFrom'), g('strikeProtractionFull')),
    };
  }
  /** ChargeArcSettings.Span: seconds per lab-second. */
  const span = (s) => F(1 / Mathf.Max(C.p05, s.Rate));
  /** The player's `settings.Bolts *= strength; settings.Glow *= strength;` on a copy. */
  const scaled = (s, strength) => Object.assign({}, s, { Bolts: F(s.Bolts * F(strength)), Glow: F(s.Glow * F(strength)) });

  // ── ChargeSites ──
  const SiteCount = 12;
  const Kind = { Pentagons: 1, Blooms: 2 };
  const PlateCorners = 10;
  const GlowLift = F(0.006), SeamSamples = 8, SeamLift = F(0.04), CornerLift = F(0.012);

  class Plate {
    constructor() { this.Centre = null; this.Normal = null; this.OuterCentre = null; this.Outer = null; this.Tip = -1; this.Bound = 0; }
    static tryRead(corners, thickness) {
      let centre = zero();
      let lo = 3.4028234663852886e38, hi = -3.4028234663852886e38;
      for (const p of corners) { centre = add(centre, p); const m = mag(p); lo = Mathf.Min(lo, m); hi = Mathf.Max(hi, m); }
      centre = div(centre, corners.length);
      const mid = F(F(lo + hi) / 2);
      const outer = [];
      let outerSum = zero(), innerSum = zero(), inner = 0;
      for (const p of corners)
        if (mag(p) > mid) { outer.push(p); outerSum = add(outerSum, p); }
        else { innerSum = add(innerSum, p); inner++; }
      if (outer.length !== 5 || inner !== 5)
        return { plate: null, problem: `a ten-corner plate splits ${outer.length} outer / ${inner} inner, not 5 / 5` };
      const normal = norm(sub(div(outerSum, 5), div(innerSum, 5)));
      const th = F(thickness);
      for (let i = 0; i < outer.length; i++) outer[i] = add(outer[i], mul(normal, F(dot(sub(outer[i], centre), normal) * F(th - 1))));
      let oc = zero();
      for (const p of outer) oc = add(oc, p);
      oc = div(oc, 5);
      const reference = norm(sub(outer[0], oc));
      const reference2 = cross(normal, reference);
      const angle = (p) => Mathf.Atan2(dot(sub(p, oc), reference2), dot(sub(p, oc), reference));
      netSort(outer, (a, b) => cmpF(angle(a), angle(b)));
      let bound = 0;
      for (const p of corners) bound = Mathf.Max(bound, mag(sub(p, centre)));
      const plate = new Plate();
      Object.assign(plate, { Centre: centre, Normal: normal, OuterCentre: oc, Outer: outer, Bound: bound });
      return { plate, problem: null };
    }
    side(k) { return mag(sub(this.Outer[(k + 1) % 5], this.Outer[k])); }
    isBack(k) { return this.Tip < 0 || (k !== this.Tip && (k + 1) % 5 !== this.Tip); }
  }

  function tenCornerPlates(vertices, triangles, tolerance) {
    const weldOf = new Int32Array(vertices.length);
    const welds = [];
    const cells = new Map();
    const cell = Mathf.Max(tolerance, C.e7);
    for (let i = 0; i < vertices.length; i++) {
      const v = vertices[i];
      const key = Mathf.Round(F(v[0] / cell)) + ',' + Mathf.Round(F(v[1] / cell)) + ',' + Mathf.Round(F(v[2] / cell));
      let w = cells.get(key);
      if (w === undefined) { w = welds.length; cells.set(key, w); welds.push(v); }
      weldOf[i] = w;
    }
    const parent = new Int32Array(welds.length);
    for (let i = 0; i < parent.length; i++) parent[i] = i;
    const find = (a) => { while (parent[a] !== a) a = parent[a] = parent[parent[a]]; return a; };
    for (let t = 0; t + 2 < triangles.length; t += 3) {
      const a = find(weldOf[triangles[t]]), b = find(weldOf[triangles[t + 1]]), c = find(weldOf[triangles[t + 2]]);
      parent[a] = b;
      const fc = find(c);
      parent[fc] = find(b);
    }
    const byRoot = new Map();
    const order = [];
    for (let w = 0; w < welds.length; w++) {
      const root = find(w);
      let list = byRoot.get(root);
      if (!list) { list = []; byRoot.set(root, list); order.push(root); }
      list.push(welds[w]);
    }
    const plates = [];
    for (const root of order) if (byRoot.get(root).length === PlateCorners) plates.push(byRoot.get(root));
    return plates;
  }

  function nearestOn(segments, q) {
    let best = zero(), bestDistance = 3.4028234663852886e38;
    for (const [a, b] of segments) {
      const ab = sub(b, a);
      const u = Mathf.Clamp01(F(dot(sub(q, a), ab) / Mathf.Max(C.e12, dot(ab, ab))));
      const p = add(a, mul(ab, u));
      const d = sqrMag(sub(p, q));
      if (d < bestDistance) { bestDistance = d; best = p; }
    }
    return best;
  }

  class ChargeSites {
    constructor() {
      this.Kind = 0; this.Radius = 0; this.Axes = null; this.Neighbours = null; this.Faces = null; this.FacesOfSite = null;
      this.Corners = null; this.Junctions = null; this.JunctionSites = null; this.Seams = null; this.SeamsOfJunction = null;
      this._paths = new Map();
    }

    /**
     * ChargeSites.TryBuild. vertices: array of [x,y,z] (or a flat array of 3n numbers); triangles: int array.
     * Returns { ok, sites, problem }.
     */
    static tryBuild(vertices, triangles, pentagonThickness) {
      if (vertices && vertices.length && typeof vertices[0] === 'number') {
        const flat = vertices; vertices = [];
        for (let i = 0; i + 2 < flat.length; i += 3) vertices.push([flat[i], flat[i + 1], flat[i + 2]]);
      }
      if (!vertices || vertices.length === 0 || !triangles || triangles.length < 3)
        return { ok: false, sites: null, problem: 'the model has no triangles' };
      vertices = vertices.map(toV);
      let radius = 0;
      for (const v of vertices) radius = Mathf.Max(radius, mag(v));
      const plates = tenCornerPlates(vertices, triangles, F(radius * C.e5));
      const kind = plates.length === SiteCount ? Kind.Pentagons : plates.length === SiteCount * 5 ? Kind.Blooms : 0;
      if (kind === 0)
        return { ok: false, sites: null, problem: `${plates.length} ten-corner plates (expected ${SiteCount} pentagonal prisms or ${SiteCount * 5} petals)` };
      const built = new ChargeSites();
      built.Kind = kind;
      built.Radius = radius;
      const frames = [];
      for (const corners of plates) {
        const r = Plate.tryRead(corners, kind === Kind.Pentagons ? pentagonThickness : 1);
        if (!r.plate) return { ok: false, sites: null, problem: r.problem };
        frames.push(r.plate);
      }
      let r = built._tryGroup(frames);
      if (!r.ok) return { ok: false, sites: null, problem: r.problem };
      const siteOf = r.siteOf;
      built._buildFaces(frames, siteOf);
      r = built._tryBuildNet(frames, siteOf);
      if (!r.ok) return { ok: false, sites: null, problem: r.problem };
      built._buildCorners(frames, siteOf);
      return { ok: true, sites: built, problem: null };
    }

    /** The flux arc from source to sink (built once, then shared). */
    pathOf(source, sink) {
      const key = source * SiteCount + sink;
      let path = this._paths.get(key);
      if (!path) { path = new ChargeFluxPath(this, source, sink); this._paths.set(key, path); }
      return path;
    }

    _tryGroup(plates) {
      const siteOf = new Int32Array(plates.length);
      this.Axes = new Array(SiteCount);
      if (this.Kind === Kind.Pentagons) {
        for (let q = 0; q < plates.length; q++) { siteOf[q] = q; this.Axes[q] = norm(plates[q].OuterCentre); }
      } else {
        let longSide = 0;
        for (const plate of plates) {
          let best = 0, bestSum = -1;
          for (let k = 0; k < 5; k++) {
            const sum = F(plate.side((k + 4) % 5) + plate.side(k));
            if (sum > bestSum) { bestSum = sum; best = k; }
          }
          plate.Tip = best;
          longSide = F(longSide + F(bestSum / 2));
        }
        longSide = F(longSide / plates.length);
        const seeds = [], members = [];
        for (let q = 0; q < plates.length; q++) {
          const tip = plates[q].Outer[plates[q].Tip];
          let site = -1;
          for (let s = 0; s < seeds.length && site < 0; s++)
            if (mag(sub(seeds[s], tip)) < F(0.5 * longSide)) site = s;
          if (site < 0) { site = seeds.length; seeds.push(tip); members.push(0); }
          siteOf[q] = site;
          members[site]++;
        }
        if (seeds.length !== SiteCount || members.some((n) => n !== 5))
          return { ok: false, problem: `the petals' tips meet in ${seeds.length} blooms (${members.join('/')} petals), not ${SiteCount} of 5` };
        const sums = [];
        for (let s = 0; s < SiteCount; s++) sums.push(zero());
        for (let q = 0; q < plates.length; q++) sums[siteOf[q]] = add(sums[siteOf[q]], plates[q].Outer[plates[q].Tip]);
        for (let s = 0; s < SiteCount; s++) this.Axes[s] = norm(sums[s]);
      }
      this.Neighbours = new Array(SiteCount);
      for (let s = 0; s < SiteCount; s++) {
        const others = [];
        for (let t = 0; t < SiteCount; t++) if (t !== s) others.push(t);
        const self = s, A = this.Axes;
        netSort(others, (a, b) => cmpF(dot(A[self], A[b]), dot(A[self], A[a])));
        this.Neighbours[s] = others.slice(0, 5);
      }
      return { ok: true, siteOf };
    }

    _buildFaces(plates, siteOf) {
      const centres = [], counts = new Array(SiteCount).fill(0);
      for (let s = 0; s < SiteCount; s++) centres.push(zero());
      for (let q = 0; q < plates.length; q++) { centres[siteOf[q]] = add(centres[siteOf[q]], plates[q].Centre); counts[siteOf[q]]++; }
      for (let s = 0; s < SiteCount; s++) centres[s] = div(centres[s], counts[s]);
      const reach = new Array(SiteCount).fill(0);
      for (let q = 0; q < plates.length; q++)
        for (const p of plates[q].Outer) reach[siteOf[q]] = Mathf.Max(reach[siteOf[q]], mag(sub(p, centres[siteOf[q]])));
      this.Faces = new Array(plates.length);
      const facesOfSite = [];
      for (let s = 0; s < SiteCount; s++) facesOfSite.push([]);
      for (let q = 0; q < plates.length; q++) {
        const plate = plates[q], s = siteOf[q];
        const lift = mul(plate.Normal, F(GlowLift * this.Radius));
        const ring = [], own = [], site = [], back = [];
        for (let k = 0; k < 5; k++) {
          ring.push(add(plate.Outer[k], lift));
          own.push(Mathf.Clamp01(F(mag(sub(plate.Outer[k], plate.OuterCentre)) / Mathf.Max(plate.Bound, C.e6))));
          site.push(Mathf.Clamp01(F(mag(sub(plate.Outer[k], centres[s])) / Mathf.Max(reach[s], C.e6))));
          if (plate.isBack(k)) back.push(div(add(plate.Outer[k], plate.Outer[(k + 1) % 5]), 2));
        }
        this.Faces[q] = {
          Site: s, Ring: ring, Centre: add(plate.OuterCentre, lift), OwnFalloff: own, SiteFalloff: site,
          SiteFalloffAtCentre: Mathf.Clamp01(F(mag(sub(plate.OuterCentre, centres[s])) / Mathf.Max(reach[s], C.e6))),
          Back: back,
          Sigma: this.Kind === Kind.Blooms ? F(plate.Bound * C.p75) : F(reach[0] * C.s1_7),
        };
        facesOfSite[s].push(q);
      }
      this.FacesOfSite = facesOfSite;
    }

    _tryBuildNet(plates, siteOf) {
      const outline = [];
      for (let s = 0; s < SiteCount; s++) outline.push([]);
      for (let q = 0; q < plates.length; q++)
        for (let k = 0; k < 5; k++)
          if (plates[q].isBack(k)) outline[siteOf[q]].push([plates[q].Outer[k], plates[q].Outer[(k + 1) % 5]]);
      let rOut = 0;
      for (let s = 0; s < SiteCount; s++) {
        let sum = 0;
        for (const seg of outline[s]) sum = F(sum + mag(seg[0]));
        rOut = F(rOut + F(sum / outline[s].length));
      }
      rOut = F(rOut / SiteCount);
      const lift = F(SeamLift * this.Radius);
      const gapPoint = (among, q) => {
        const target = mul(q, rOut);
        let sum = zero(), high = 0;
        for (const s of among) {
          const p = nearestOn(outline[s], target);
          sum = add(sum, p);
          high = Mathf.Max(high, mag(p));
        }
        return mul(norm(div(sum, among.length)), F(high + lift));
      };
      const N = this.Neighbours, A = this.Axes;
      const triples = [];
      for (let s = 0; s < SiteCount; s++)
        for (const t of N[s])
          for (const u of N[s])
            if (s < t && t < u && N[t].indexOf(u) >= 0) triples.push([s, t, u]);
      this.Junctions = new Array(triples.length);
      this.JunctionSites = triples;
      for (let j = 0; j < triples.length; j++)
        this.Junctions[j] = gapPoint(triples[j], norm(add(add(A[triples[j][0]], A[triples[j][1]]), A[triples[j][2]])));
      const seams = [], ofJunction = [];
      for (let j = 0; j < triples.length; j++) ofJunction.push([]);
      for (let i = 0; i < triples.length; i++)
        for (let j = i + 1; j < triples.length; j++) {
          const shared = [];
          for (const s of triples[i]) if (triples[j].indexOf(s) >= 0) shared.push(s);
          if (shared.length !== 2) continue;
          const points = new Array(SeamSamples + 1);
          const a = norm(this.Junctions[i]), b = norm(this.Junctions[j]);
          points[0] = this.Junctions[i];
          for (let k = 1; k < SeamSamples; k++) points[k] = gapPoint(shared, norm(lerpV(a, b, F(k / SeamSamples))));
          points[SeamSamples] = this.Junctions[j];
          ofJunction[i].push(seams.length);
          ofJunction[j].push(seams.length);
          let length = 0;
          for (let k = 1; k < points.length; k++) length = F(length + mag(sub(points[k], points[k - 1])));
          seams.push({ A: i, B: j, SiteA: shared[0], SiteB: shared[1], Points: points, Length: length });
        }
      this.Seams = seams;
      this.SeamsOfJunction = ofJunction;
      if (this.Junctions.length !== 20 || seams.length !== 30 || ofJunction.some((e) => e.length !== 3))
        return { ok: false, problem: `the seam net has ${this.Junctions.length} junctions and ${seams.length} seams, not the dodecahedron's 20 and 30` };
      return { ok: true };
    }

    _buildCorners(plates, siteOf) {
      this.Corners = new Array(SiteCount);
      if (this.Kind === Kind.Pentagons) {
        const lift = F(CornerLift * this.Radius);
        for (let q = 0; q < plates.length; q++) {
          const corners = [];
          for (let k = 0; k < 5; k++) corners.push(add(plates[q].Outer[k], mul(plates[q].Normal, lift)));
          this.Corners[siteOf[q]] = corners;
        }
        return;
      }
      for (let s = 0; s < SiteCount; s++) {
        const corners = [];
        for (let j = 0; j < this.Junctions.length; j++) if (this.JunctionSites[j].indexOf(s) >= 0) corners.push(this.Junctions[j]);
        this.Corners[s] = corners;
      }
    }
  }
  const seamTouches = (seam, site) => seam.SiteA === site || seam.SiteB === site;

  // ── ChargeFluxPath ──
  const FrameSamples = 128;
  const wrap = (a) => F(a - F(C.TwoPI * Mathf.Round(F(a / C.TwoPI))));

  class ChargeFluxPath {
    constructor(sites, source, sink) {
      this.Source = source;
      this.Sink = sink;
      const cs = sites.Corners[source], cd = sites.Corners[sink];
      let a = zero(), b = zero();
      for (const c of cs) a = add(a, c);
      for (const c of cd) b = add(b, c);
      a = div(a, cs.length);
      b = div(b, cd.length);
      this.sourceAxis = sites.Axes[source];
      this.sinkAxis = sites.Axes[sink];
      this.angle = Mathf.Acos(Mathf.Clamp(dot(this.sourceAxis, this.sinkAxis), -1, 1));
      this.bow = F(C.p16 + F(F(C.p3 * this.angle) / C.PI));
      this.sourceRadius = mag(a);
      this.sinkRadius = mag(b);
      this.SourceOffsets = new Array(cs.length);
      this.SinkOffsets = new Array(cs.length);
      for (let k = 0; k < cs.length; k++) { this.SourceOffsets[k] = sub(cs[k], a); this.SinkOffsets[k] = zero(); }
      this.frameTangent = new Array(FrameSamples + 1);
      this.frameE1 = new Array(FrameSamples + 1);
      this._buildFrame();

      const n = cs.length;
      const sourceAngles = new Array(n), sinkAngles = new Array(n);
      for (let k = 0; k < n; k++) sourceAngles[k] = this._angleIn(this.SourceOffsets[k], 0);
      for (let j = 0; j < n; j++) sinkAngles[j] = this._angleIn(sub(cd[j], b), 1);
      const sourceOrder = [], sinkOrder = [];
      for (let i = 0; i < n; i++) { sourceOrder.push(i); sinkOrder.push(i); }
      netSort(sourceOrder, (x, y) => cmpF(sourceAngles[x], sourceAngles[y]));
      netSort(sinkOrder, (x, y) => cmpF(sinkAngles[x], sinkAngles[y]));
      let bestShift = 0, bestCost = 3.4028234663852886e38, bestMean = 0;
      for (let shift = 0; shift < n; shift++) {
        let sum = 0;
        for (let i = 0; i < n; i++) sum = F(sum + wrap(F(sinkAngles[sinkOrder[(i + shift) % n]] - sourceAngles[sourceOrder[i]])));
        const mean = F(sum / n);
        let cost = F(mean * mean);
        for (let i = 0; i < n; i++) {
          const d = wrap(F(F(sinkAngles[sinkOrder[(i + shift) % n]] - sourceAngles[sourceOrder[i]]) - mean));
          cost = F(cost + F(d * d));
        }
        if (cost < bestCost) { bestCost = cost; bestShift = shift; bestMean = mean; }
      }
      const step = F(C.TwoPI / n);
      let sx = 0, sy = 0;
      for (let i = 0; i < n; i++) {
        const rel = F(sourceAngles[sourceOrder[i]] - F(i * step));
        sx = F(sx + Mathf.Cos(rel));
        sy = F(sy + Mathf.Sin(rel));
      }
      const reference = Mathf.Atan2(sy, sx);
      this.meanTurn = bestMean;
      this.slotAngle = new Array(n);
      this.startOff = new Array(n);
      this.endOff = new Array(n);
      for (let i = 0; i < n; i++) {
        const k = sourceOrder[i], j = sinkOrder[(i + bestShift) % n];
        this.SinkOffsets[k] = sub(cd[j], b);
        this.slotAngle[k] = F(reference + F(i * step));
        this.startOff[k] = wrap(F(sourceAngles[k] - this.slotAngle[k]));
        this.endOff[k] = wrap(F(sinkAngles[j] - F(this.slotAngle[k] + this.meanTurn)));
      }
    }

    /** The centre line at u (0 at the source, 1 at the sink). */
    centre(u) {
      const dir = norm(add(mul(this.sourceAxis, Mathf.Sin(F(F(1 - u) * this.angle))), mul(this.sinkAxis, Mathf.Sin(F(u * this.angle)))));
      const r = F(this.sourceRadius + F(F(this.sinkRadius - this.sourceRadius) * u));
      return mul(dir, F(r * F(1 + F(this.bow * Mathf.Sin(F(C.PI * u))))));
    }

    /** Strand k at u: on its source corner at 0, its paired sink corner at 1, a coaxial filament between. */
    strand(k, u, fork, bundle, turns) {
      const [e1, e2] = this._frame(u);
      const into = smooth(F(u / fork)), outOf = smooth(F(F(u - F(1 - fork)) / fork));
      let theta = F(this.slotAngle[k] + F(this.meanTurn * smooth(u)));
      const braid = smooth(F(F(u - fork) / Mathf.Max(C.e4, F(1 - F(2 * fork)))));
      theta = F(theta + F(F(C.TwoPI * turns) * braid));
      theta = F(theta + F(this.startOff[k] * F(1 - into)));
      theta = F(theta + F(this.endOff[k] * outOf));
      const ring = mul(add(mul(e1, Mathf.Cos(theta)), mul(e2, Mathf.Sin(theta))), bundle);
      const offset = add(add(mul(this.SourceOffsets[k], F(1 - into)), mul(ring, F(into * F(1 - outOf)))), mul(this.SinkOffsets[k], outOf));
      return add(this.centre(u), offset);
    }

    _buildFrame() {
      for (let i = 0; i <= FrameSamples; i++) {
        const u = F(i / FrameSamples);
        this.frameTangent[i] = norm(sub(this.centre(Mathf.Min(1, F(u + C.p002))), this.centre(Mathf.Max(0, F(u - C.p002)))));
      }
      const t0 = this.frameTangent[0];
      let seed = sub(this.SourceOffsets[0], mul(t0, dot(this.SourceOffsets[0], t0)));
      if (sqrMag(seed) < C.e12) seed = cross(t0, Math.abs(t0[0]) < C.p9 ? [1, 0, 0] : [0, 1, 0]);
      this.frameE1[0] = norm(seed);
      for (let i = 1; i <= FrameSamples; i++) {
        const carried = rotate(fromToRotation(this.frameTangent[i - 1], this.frameTangent[i]), this.frameE1[i - 1]);
        this.frameE1[i] = norm(sub(carried, mul(this.frameTangent[i], dot(carried, this.frameTangent[i]))));
      }
    }

    _frame(u) {
      const x = F(Mathf.Clamp01(u) * FrameSamples);
      const i = Math.min(Math.trunc(x), FrameSamples - 1);
      const f = F(x - i);
      const tangent = norm(lerpV(this.frameTangent[i], this.frameTangent[i + 1], f));
      const a = lerpV(this.frameE1[i], this.frameE1[i + 1], f);
      const e1 = norm(sub(a, mul(tangent, dot(a, tangent))));
      return [e1, cross(tangent, e1)];
    }

    _angleIn(offset, u) {
      const [e1, e2] = this._frame(u);
      return Mathf.Atan2(dot(offset, e2), dot(offset, e1));
    }
  }

  // ── ChargeSeamWalker ──
  class ChargeSeamWalker {
    constructor(sites, seed, startSeconds) {
      this.sites = sites;
      this.random = new NetRandom(seed | 0);
      this.legs = [];      // { Seam, Serial, Forward, Start, Struck, Leaves, Odometer }
      this.recent = [];
      this.options = [];
      this.at = this.random.next(sites.Junctions.length);
      this.previous = -1;
      this.serial = 0;
      this.end = F(startSeconds);
      this.odometer = 0;
    }
    get Legs() { return this.legs; }

    /** Strikes every seam due by `seconds` (and a second ahead), and forgets the ones that have faded out. */
    advance(seconds, settings) {
      seconds = F(seconds);
      const sites = this.sites, sp = span(settings);
      while (this.end <= F(seconds + 1)) {
        const options = this.options;
        options.length = 0;
        for (const e of sites.SeamsOfJunction[this.at]) if (e !== this.previous) options.push(e);
        if (this.recent.length >= 4) {
          const first = sites.Seams[this.recent[0]];
          for (const site of [first.SiteA, first.SiteB]) {
            if (!this.recent.every((e) => seamTouches(sites.Seams[e], site))) continue;
            if (options.some((e) => !seamTouches(sites.Seams[e], site))) {
              for (let i = options.length - 1; i >= 0; i--) if (seamTouches(sites.Seams[options[i]], site)) options.splice(i, 1);
            }
          }
        }
        const seam = options[this.random.next(options.length)];
        const edge = sites.Seams[seam];
        const forward = edge.A === this.at;
        const struck = F(this.end + F(settings.SeamStrike * sp));
        const leaves = F(struck + F(Mathf.Lerp(settings.SeamDwellMin, settings.SeamDwellMax, F(this.random.nextDouble())) * sp));
        this.legs.push({ Seam: seam, Serial: this.serial++, Forward: forward, Start: this.end, Struck: struck, Leaves: leaves, Odometer: this.odometer });
        this.odometer = F(this.odometer + edge.Length);
        this.end = leaves;
        this.at = forward ? edge.B : edge.A;
        this.previous = seam;
        this.recent.push(seam);
        if (this.recent.length > 4) this.recent.shift();
      }
      let drop = 0;
      const fade = F(settings.SeamFade * sp);
      while (drop < this.legs.length && F(seconds - this.legs[drop].Leaves) > fade) drop++;
      if (drop > 0) this.legs.splice(0, drop);
    }
  }

  // ── ChargeFluxChain ──
  class ChargeFluxChain {
    constructor(sites, seed) {
      this.sites = sites;
      this.random = new NetRandom(seed | 0);
      this.kept = [];        // { Index, Source, Sink }
      this.candidates = [];
      this.nextIndex = 0;
    }
    /** Discharge `index` (a whole number >= 0), or null once it has dropped out of the kept window (8). */
    tryGet(index, settings) {
      if (index < 0) return null;
      while (this.nextIndex <= index) {
        const first = this.kept.length === 0;
        const last = first ? null : this.kept[this.kept.length - 1];
        const source = first ? this.random.next(SiteCount) : last.Sink;
        const lo = Mathf.Cos(F(settings.FluxMaxDegrees * C.Deg2Rad)), hi = Mathf.Cos(F(settings.FluxMinDegrees * C.Deg2Rad));
        const cand = this.candidates;
        cand.length = 0;
        for (let i = 0; i < SiteCount; i++) {
          if (i === source || (!first && i === last.Source)) continue;
          const d = dot(this.sites.Axes[source], this.sites.Axes[i]);
          if (d < hi && d > lo) cand.push(i);
        }
        if (cand.length === 0) for (let i = 0; i < SiteCount; i++) if (i !== source) cand.push(i);
        const idx = this.nextIndex++;
        this.kept.push({ Index: idx, Source: source, Sink: cand[this.random.next(cand.length)] });
        if (this.kept.length > 8) this.kept.shift();
      }
      for (let i = this.kept.length - 1; i >= 0; i--) if (this.kept[i].Index === index) return this.kept[i];
      return null;
    }
  }

  // ── ChargeArcMeshWriter ──
  const PinLength = F(0.06), SampleSpacing = F(0.0125);

  class ChargeArcMeshWriter {
    constructor() {
      /** Flat float32 values: x,y,z per vertex, in the sites' (crystal-local) space. */
      this.positions = [];
      /** Flat float32 values: TEXCOORD0 x,y,z,w per vertex = (across -1..1, falloff 0..1, kind 0 strip / 1 face, intensity). */
      this.data = [];
      /** Triangle list (uint32-safe ints). */
      this.indices = [];
      this._arcAt = []; this._placeAt = []; this._bent = []; this._weight = [];
    }
    get vertexCount() { return this.positions.length / 3; }
    clear() { this.positions.length = 0; this.data.length = 0; this.indices.length = 0; }
    _push(p, x, y, z, w) {
      this.positions.push(p[0], p[1], p[2]);
      this.data.push(x, y, z, w);
    }

    /** A lit face as a fan from its centre; ownCentre picks the falloff from the face's own centre (a petal) or its site's. */
    face(face, intensity, ownCentre) {
      intensity = F(intensity);
      const c = this.vertexCount;
      this._push(face.Centre, 0, ownCentre ? 0 : face.SiteFalloffAtCentre, 1, intensity);
      for (let k = 0; k < face.Ring.length; k++) this._push(face.Ring[k], 0, ownCentre ? face.OwnFalloff[k] : face.SiteFalloff[k], 1, intensity);
      const n = face.Ring.length;
      for (let k = 0; k < n; k++) this.indices.push(c, c + 1 + k, c + 1 + (k + 1) % n);
    }

    /** ChargeArcMeshWriter.Plasma: one bolt along points[0..count) as a plasma ribbon and its sub-filament. */
    plasma(points, count, weights, coords, intensity, width, wobble, seed, seconds, settings, eye, radius, pinned = true) {
      if (count < 2 || intensity <= C.p01) return;
      const breath = F(C.p8 + F(C.p2 * fbm(F(F(seconds * settings.FlickerSpeed) + F(seed * C.p731)), 2)));
      this._ribbon(points, count, weights, coords, F(intensity * breath), width, wobble, seed, seconds, settings, eye, radius, pinned);
      this._ribbon(points, count, weights, coords, F(F(intensity * breath) * C.p4), F(width * C.p45), F(wobble * C.s1_7), (seed + 101) | 0,
        seconds, settings, eye, radius, pinned);
    }

    _ribbon(points, count, weights, coords, intensity, width, wobble, seed, seconds, settings, eye, radius, pinned) {
      const arcAt = this._arcAt, placeAt = this._placeAt, bent = this._bent, weight = this._weight;
      arcAt.length = 0;
      placeAt.length = 0;
      let total = 0;
      for (let i = 0; i < count; i++) {
        if (i > 0) total = F(total + mag(sub(points[i], points[i - 1])));
        arcAt.push(total);
        placeAt.push(coords == null ? total : F(coords[i] * radius));
      }
      if (total < C.e6) return;
      const step = F(SampleSpacing * radius);
      const from = placeAt[0], to = placeAt[count - 1];
      if (to <= from) return;
      bent.length = 0;
      weight.length = 0;
      const flow = F(seconds * settings.WritheSpeed), phase1 = F(seed * C.s1_37), phase2 = F(F(seed * C.s2_91) + 50);
      let seg = 0;
      let next = F(F(Mathf.Floor(F(from / step)) * step) + step);
      const quarter = F(C.p25 * step);
      if (F(next - from) < quarter) next = F(next + step);
      let guard = 0;
      const pinSpan = F(PinLength * radius);
      for (let place = from; guard++ < 512; place = F(to - next) < quarter ? to : next, next = F(next + step)) {
        while (seg < count - 2 && place > placeAt[seg + 1]) seg++;
        const spn = F(placeAt[seg + 1] - placeAt[seg]);
        const f = spn > C.e9 ? Mathf.Clamp01(F(F(place - placeAt[seg]) / spn)) : 0;
        const p = lerpU(points[seg], points[seg + 1], f);
        const tangent = norm(lerpV(pointTangent(points, count, seg), pointTangent(points, count, seg + 1), f));
        const n1 = norm(cross(tangent, sub(eye, p)));
        const n2 = cross(tangent, n1);
        const at = Mathf.Lerp(arcAt[seg], arcAt[seg + 1], f);
        const x = F(F(6 * place) / radius);
        const pin = !pinned ? 1 : F(smooth(F(at / pinSpan)) * smooth(F(F(total - at) / pinSpan)));
        const d1 = fbm(F(F(x + phase1) - flow), 3), d2 = fbm(F(F(x + phase2) + F(flow * C.p8)), 3);
        bent.push(add(p, mul(add(mul(n1, d1), mul(n2, F(C.p6 * d2))), F(F(wobble * radius) * pin))));
        weight.push(weights == null ? 1 : Mathf.Lerp(weights[seg], weights[seg + 1], f));
        if (place >= to) break;
      }
      const half = F(stripHalfWidth(settings.CoreWidth, width) * radius);
      const first = this.vertexCount;
      for (let k = 0; k < bent.length; k++) {
        const p = bent[k];
        const along = sub(bent[Math.min(k + 1, bent.length - 1)], bent[Math.max(k - 1, 0)]);
        const side = mul(norm(cross(along, sub(eye, p))), half);
        const v = F(intensity * weight[k]);
        this._push(add(p, side), 1, 0, 0, v);
        this._push(sub(p, side), -1, 0, 0, v);
        if (k === 0) continue;
        const i = first + 2 * (k - 1);
        this.indices.push(i, i + 1, i + 2, i + 2, i + 1, i + 3);
      }
    }
  }

  function pointTangent(points, count, i) {
    const before = i > 0 ? norm(sub(points[i], points[i - 1])) : zero();
    const after = i + 1 < count ? norm(sub(points[i + 1], points[i])) : zero();
    const t = add(before, after);
    return sqrMag(t) > C.e12 ? norm(t) : (sqrMag(after) > 0 ? after : before);
  }

  // ── ChargeArcs (static) ──
  function smooth(x) {
    x = Mathf.Clamp01(x);
    return F(F(x * x) * F(3 - F(2 * x)));
  }
  function hash(a, b) {
    let h = (Math.imul(a | 0, 374761393) ^ Math.imul(b | 0, 668265263)) >>> 0;
    h = Math.imul((h ^ (h >>> 13)) >>> 0, 1274126177) >>> 0;
    h = (h ^ (h >>> 16)) >>> 0;
    return F(F(h) / 4294967296);
  }
  function valueNoise(x) {
    const i = Mathf.Floor(x);
    let f = F(x - i);
    f = F(F(f * f) * F(3 - F(2 * f)));
    const n = Math.trunc(i) | 0;
    return Mathf.Lerp(hash(n, 977), hash((n + 1) | 0, 977), f);
  }
  function fbm(x, octaves) {
    let value = 0, amplitude = 0.5, frequency = 1;
    for (let o = 0; o < octaves; o++) {
      value = F(value + F(amplitude * F(F(valueNoise(F(F(x * frequency) + F(o * C.s19_1))) * 2) - 1)));
      frequency = F(frequency * C.s2_17);
      amplitude = F(amplitude * 0.5);
    }
    return value;
  }
  const stripHalfWidth = (coreWidth, width) => F(F(coreWidth * width) * C.s2_2);

  const SeamWobble = F(0.02), BundleWobble = F(0.02);
  const StrandSteps = 96;

  /**
   * ChargeArcs.SeamWalk. faceLight: a float array (Float32Array or Array) of sites.Faces.length, overwritten.
   * Returns the number of points on the trail.
   */
  function seamWalk(sites, walker, seconds, settings, writer, eye, faceLight) {
    seconds = F(seconds);
    eye = toV(eye);
    faceLight.fill(0);
    const sp = span(settings), fade = F(settings.SeamFade * sp);
    const pts_ = [], weights = [], coords = [], lit = [];
    const legs = walker.Legs;
    for (let l = 0; l < legs.length; l++) {
      const leg = legs[l];
      if (leg.Start > seconds) break;
      const pts = sites.Seams[leg.Seam].Points;
      const n = pts.length - 1;
      const seamLength = sites.Seams[leg.Seam].Length;
      const strike = Mathf.Max(C.e6, F(leg.Struck - leg.Start));
      const reach = F(Mathf.Clamp01(F(F(seconds - leg.Start) / strike)) * n);
      for (let i = 0; i <= n; i++) {
        if (i === 0 && pts_.length > 0) continue;
        const partial = i > reach;
        const at = partial ? reach : i;
        const lo = Math.min(Math.trunc(at), n - 1);
        const a = pts[leg.Forward ? lo : n - lo];
        const b = pts[leg.Forward ? lo + 1 : n - lo - 1];
        const point = lerpV(a, b, F(at - lo));
        const leftAt = Mathf.Min(seconds, F(leg.Start + F(F(F(leg.Leaves - leg.Start) * at) / n)));
        const life = F(1 - smooth(F(Mathf.Max(0, F(seconds - leftAt)) / fade)));
        if (life > C.p001 || pts_.length > 0) {
          pts_.push(point);
          weights.push(life);
          coords.push(F(F(leg.Odometer + F(F(seamLength * at) / n)) / sites.Radius));
          lit.push([point[0], point[1], point[2], life]);
        }
        if (partial) break;
      }
    }
    writer.plasma(pts_, pts_.length, weights, coords, F(settings.Bolts * C.s2_4), 2, SeamWobble, 7, seconds, settings, eye, sites.Radius, false);
    for (let f = 0; f < sites.Faces.length; f++) {
      const face = sites.Faces[f];
      let g = 0;
      for (const L of lit) {
        const q = [L[0], L[1], L[2]];
        for (const back of face.Back) {
          const d = F(mag(sub(back, q)) / face.Sigma);
          g = Mathf.Max(g, F(L[3] * Mathf.Exp(F(F(-d) * d))));
        }
      }
      if (g > C.p01) faceLight[f] = F(F(F(settings.Glow * settings.SeamPetalGlow) * C.s2_4) * g);
    }
    return pts_.length;
  }

  const now = (seconds, s) => Math.trunc(Mathf.Floor(F(seconds / F(s.FluxGap * span(s)))));

  // Discharge k at seconds: { discharge, sourceLight, sinkLight, grow, intensity } or null once faded.
  function discharge(chain, k, seconds, s) {
    const sp = span(s), gap = F(s.FluxGap * sp);
    const charge = F(s.FluxCharge * sp), struck = F(charge + F(s.FluxStrike * sp));
    const held = F(struck + F(s.FluxHold * sp)), gone = F(held + F(s.FluxFade * sp));
    const t = F(seconds - F(F(k) * gap));
    if (t > gone) return null;
    const d = chain.tryGet(k, s);
    if (!d) return null;
    const r = { discharge: d, sourceLight: 0, sinkLight: 0, grow: 0, intensity: 0 };
    const fading = F(1 - smooth(F(F(t - held) / F(gone - held))));
    r.sourceLight = t < charge ? F(C.s1_4 * smooth(F(t / charge))) : t < held ? C.s2_2 : F(C.s2_2 * fading);
    if (t < charge) return r;
    r.sinkLight = t < held ? F(C.s2_4 * smooth(F(F(t - charge) / F(struck - charge)))) : F(C.s2_4 * fading);
    r.grow = Mathf.Clamp01(F(F(t - charge) / F(struck - charge)));
    r.intensity = F(s.Bolts * (t < held ? 1 : fading));
    return r;
  }

  function strandPoints(path, q, steps, fork, s, radius) {
    const pts = [];
    const bundle = F(s.FluxBundle * radius);
    for (let i = 0; i <= steps; i++) pts.push(path.strand(q, F(i / StrandSteps), fork, bundle, s.FluxTurns));
    return pts;
  }

  function light(sites, faceLight, site, value) {
    for (const f of sites.FacesOfSite[site]) faceLight[f] = Mathf.Max(faceLight[f], value);
  }

  /** ChargeArcs.Flux. faceLight: sites.Faces.length floats, overwritten. Returns the number of discharges drawn. */
  function flux(sites, chain, seconds, s, writer, eye, faceLight) {
    seconds = F(seconds);
    eye = toV(eye);
    faceLight.fill(0);
    if (seconds < 0) return 0;
    const fork = s.FluxFork;
    let drawn = 0;
    const n0 = now(seconds, s);
    for (let k = n0; k >= 0 && k > n0 - 4; k--) {
      const d = discharge(chain, k, seconds, s);
      if (!d) continue;
      light(sites, faceLight, d.discharge.Source, F(s.Glow * d.sourceLight));
      if (d.sinkLight > 0) light(sites, faceLight, d.discharge.Sink, F(s.Glow * d.sinkLight));
      if (d.intensity <= C.p01) continue;
      const path = sites.pathOf(d.discharge.Source, d.discharge.Sink);
      const seed = (k * 53) | 0;
      const steps = Mathf.RoundToInt(F(d.grow * StrandSteps));
      for (let q = 0; q < path.SourceOffsets.length; q++) {
        const pts = strandPoints(path, q, steps, fork, s, sites.Radius);
        writer.plasma(pts, pts.length, null, null, F(d.intensity * C.s1_1), C.p9, BundleWobble, seed, seconds, s, eye, sites.Radius, true);
      }
      drawn++;
    }
    return drawn;
  }

  /** ChargeArcs.StrikeGate: how much of a strike a prism at column place `place` (0 top .. 1 sealed) shows. */
  function strikeGate(place, s) {
    const from = s.StrikeProtractionFrom, full = Mathf.Max(F(from + C.e4), s.StrikeProtractionFull);
    return smooth(F(F(F(1 - F(place)) - from) / F(full - from)));
  }

  function distanceToSegment(p, a, b) {
    const ab = sub(b, a);
    const u = Mathf.Clamp01(F(dot(sub(p, a), ab) / Mathf.Max(C.e12, dot(ab, ab))));
    return mag(sub(add(a, mul(ab, u)), p));
  }

  function strikeAlong(path, centres, radii, count, intensity, s, strike, seats, seatRadii) {
    if (path.length < 2) return;
    for (let j = 0; j < count; j++) {
      const r = radii[j];
      if (r <= 0) continue;
      let depth = 3.4028234663852886e38;
      const samples = seats != null ? 8 : 0;
      for (let c = 0; c <= samples; c++) {
        const along = samples === 0 ? 1 : F(c / samples);
        const p = samples === 0 ? centres[j] : lerpV(seats[j], centres[j], along);
        const rr = samples === 0 ? r : Mathf.Lerp(seatRadii[j], r, along);
        if (rr <= 0) continue;
        let d = 3.4028234663852886e38;
        for (let i = 0; i + 1 < path.length; i++) d = Mathf.Min(d, distanceToSegment(p, path[i], path[i + 1]));
        depth = Mathf.Min(depth, F(d / rr));
      }
      if (depth >= 1) continue;
      const v = F(F(F(s.Glow * C.s2_4) * intensity) * F(1 - smooth(F(F(depth - C.p6) / C.p4))));
      strike[j] = Mathf.Max(strike[j], v);
    }
  }

  /**
   * ChargeArcs.FluxStrikes, both overloads:
   *   fluxStrikes(sites, chain, seconds, settings, centres, radii, count, strike)
   *   fluxStrikes(sites, chain, seconds, settings, centres, radii, places, seats, seatRadii, count, strike)
   * centres/seats: arrays of [x,y,z] in the sites' space; radii/places/seatRadii/strike: float arrays. strike[0..count)
   * is overwritten.
   */
  function fluxStrikes(sites, chain, seconds, s, centres, radii, a, b, c, d, e) {
    let places = null, seats = null, seatRadii = null, count, strike;
    if (e === undefined) { count = a; strike = b; } else { places = a; seats = b; seatRadii = c; count = d; strike = e; }
    seconds = F(seconds);
    centres = centres.map(toV);
    radii = Array.from(radii, F);
    if (seats) { seats = seats.map(toV); seatRadii = Array.from(seatRadii, F); }
    for (let j = 0; j < count; j++) strike[j] = 0;
    if (seconds >= 0) {
      const fork = s.FluxFork;
      const n0 = now(seconds, s);
      for (let k = n0; k >= 0 && k > n0 - 4; k--) {
        const dd = discharge(chain, k, seconds, s);
        if (!dd || dd.intensity <= C.p01) continue;
        const path = sites.pathOf(dd.discharge.Source, dd.discharge.Sink);
        const steps = Mathf.RoundToInt(F(dd.grow * StrandSteps));
        for (let q = 0; q < path.SourceOffsets.length; q++) {
          const pts = strandPoints(path, q, steps, fork, s, sites.Radius);
          strikeAlong(pts, centres, radii, count, F(dd.intensity * C.s1_1), s, strike, seats, seatRadii);
        }
      }
    }
    if (places == null) return;
    for (let j = 0; j < count; j++) if (strike[j] > 0) strike[j] = F(strike[j] * strikeGate(places[j], s));
  }

  /** OmniCombinePlayer.ArcLayer.Upload's first half: the lit faces after the bolts already written. */
  function writeFaces(sites, writer, faceLight, ownCentre) {
    for (let f = 0; f < sites.Faces.length; f++) writer.face(sites.Faces[f], faceLight[f], ownCentre);
  }

  CR.ChargeArcs = {
    // settings
    settings, Lab, span, scaled,
    ChargeArcSettings: { get Lab() { return Lab(); } },
    // geometry and state
    ChargeSiteKind: Kind, ChargeSites, ChargeFluxPath, ChargeSeamWalker, ChargeFluxChain, ChargeArcMeshWriter,
    // the effects
    seamWalk, flux, fluxStrikes, strikeGate, writeFaces,
    // the noise and helpers (exposed for tests)
    smooth, hash, valueNoise, fbm, stripHalfWidth, NetRandom, netSort,
    constants: { SiteCount, StrandSteps, SeamWobble, BundleWobble, PinLength, SampleSpacing, GlowLift, SeamLift, CornerLift },
  };
})(globalThis.CR = globalThis.CR || {});
