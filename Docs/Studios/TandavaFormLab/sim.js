/* Tandava hunt model: the browser half of the shipped rules.
   Plans: the game's 21 baked Tandava body plans (SwarmPlan_tandava_*.json), upsampled exactly as SwarmPlanData.Upsample
   does. Director: TandavaDirectorCore, ported rule for rule (threat and mood, the levers, where to eat, the feed pose,
   the broken meal, the rising banks, the rise in place, the halo and the drum, the outcomes). World: TandavaHarness's
   closed cell (16 dispersed plants that regrow and replant, round-robin bites, the metabolism) and the controller's halo
   and chasers. Flock: simplified. Each member springs to a plan slot of its own element with the sort core's gain, clip,
   spacing, repulsion and inertia, slots are re-paired by swaps, members ride the body's turn rigidly (TurnCarry 1), and
   eggs are laid at open slots, paid from the stomach. */
(function (G) {
  'use strict';
  const UNIT = 2, DT = 0.1, DENSITY = 3, KW = Math.cbrt(DENSITY) * UNIT;   // plan voxels -> world
  const EGG = [20.45, 40.31, 22.18, 12.8];                                 // Charge, Mass, Space, Time
  const STOMACH = 500 * DENSITY * (EGG[0] + EGG[1] + EGG[2] + EGG[3]) * 0.25;
  const CORE = {
    cruise: 3.0, turn: 0.03, aimHold: 1.5, layRate: 0.084, layMax: 8 * DENSITY, layRamp: 15, seed: 180 * DENSITY,
    gain: 0.412, clip: 1.0, spacing: 2.25, repulsion: 0.151, inertia: 0.687, noise: 0.1, swaps: 120,
    vmax: [2.4, 2.4, 2.4, 6],   // SwarmSortParams.VMax x SortVMaxScale 3: a re-sort reads in about a second
    moltSteps: 10, bloomSteps: 8, dieSteps: 6, cross: 2,
  };
  const META = { biters: 8, starve: 30, shed: 0.25, forageBelow: 0.5 };
  const FOOD = { bite: 5.60606 * 4.67172 * 2.7822, plantR: 28, biteR: 10, regrow: 7.5, pause: 2, replant: 120, diesAt: 4, survey: 5 };
  const ARENA = { membrane: 800, nucleus: 196, inner: 240, outer: 720, reach: 160, hatch: [-430, 0, 0], pilotX: 600 };
  /** The reef (author_tandava_assets.FLORA_TABLE): species, element (research index), how many, prisms a plant, a leaf's volume. */
  const REEF = [
    { species: 'Borromean', elem: 1, count: 5, prisms: 60, leaf: 72.866 }, { species: 'Coral', elem: 1, count: 5, prisms: 110, leaf: 24.640 },
    { species: 'Lantern', elem: 1, count: 4, prisms: 70, leaf: 43.740 }, { species: 'Reed', elem: 1, count: 4, prisms: 130, leaf: 16.796 },
    { species: 'Frond', elem: 1, count: 3, prisms: 120, leaf: 22.964 }, { species: 'Tendril', elem: 2, count: 3, prisms: 84, leaf: 3.268 },
    { species: 'Coral', elem: 2, count: 3, prisms: 140, leaf: 3.377 },
  ];
  const HALO = { count: 12, toBreak: 9, radius: 167.3, mouth: 24, post: 201.91, guardR: 40, guardN: 6, guardElem: 3 };
  const PILOT = { cruise: 150, strike: 270, turn: 3.5, latAccel: 420, accel: 260, killR: 6, stingR: 9, stingSeconds: 2.5, stingSlow: 0.3,
    overshoot: 0.55, hover: 55, shadow: 260, aiSide: 45, aiFlank: 70, aiSpeeds: { off: 0, autopilot: 70, fast: 140 } };
  /** TandavaSettings.asset's Director block. */
  const SETTINGS = {
    centre: [0, 0, 0], roamRadius: ARENA.membrane * 0.97 - ARENA.reach,
    senseRadius: 450, approachSpeed: 80, damageRef: 0.04, threatRise: 0.25, threatFall: 2.5,
    waryEnter: 0.3, waryExit: 0.2, fleeEnter: 0.65, fleeExit: 0.4, fleeMinSeconds: 4, fleeReach: 500,
    lungeRadius: 380, lungeBodyMin: 0.7, lungeEnter: 0.25, lungeSeconds: 2.5, lungeCooldownSeconds: 6, lungeLead: 0.35, lungeReach: 40,
    snapReach: 150, snapLeadSeconds: 0.5, snapHoldSeconds: 1.3, turnSnap: 0.3,
    cruiseCalm: 1, cruiseWary: 1.5, cruiseFlee: 2.1, cruiseFeed: 0.5, cruiseLunge: 2.4,
    turnCalm: 1, turnWary: 1.6, turnFlee: 2.5, turnFeed: 1, turnLunge: 3,
    safeRadius: 350, fearCalm: 0.5, fearWary: 0.9, distanceBias: 300, stickBonus: 1.3, minFood: 200, restSeconds: 25, wanderReach: 350,
    arriveMargin: 70, mealBreakFraction: 0.2, giveUpSeconds: 6, feedSettleSeconds: 4, maxFeedSeconds: 30, leaveWhenStomachFill: 0.98,
    shatterFraction: 0.35, armFraction: 0.6, riseSeconds: 5, drumSeconds: 30, haloCount: HALO.count, haloToBreak: HALO.toBreak,
    danceReach: 250, matchSeconds: 0,   // no clock: the hunt ends on the creature's outcome
  };
  /** The meal formations the two serpents roll up in (tandava_plans.COILS). */
  const COILS = ['coil', 'wrap', 'eight'];
  const COIL_NAMES = { coil: 'a coil', wrap: 'a constrictor\'s wrap', eight: 'a figure-eight' };
  const serpentKeys = v => [v, v + '_feed'].concat(COILS.map(c => v + '_' + c), v.startsWith('many') ? [v + '_rear', v + '_strike'] : []);
  /** Each form's own lunge poses, [the charge, the strike] (tandava_plans.lunges_of). */
  const LUNGES = [null, ['rear', 'strike'], null, ['gape', 'snap']];
  const KEYS = [].concat(...['great_serpent_1', 'great_serpent_2', 'great_serpent_3', 'many_headed_5', 'many_headed_7', 'many_headed_10'].map(serpentKeys),
    ['dancer_1', 'dancer_2', 'dancer_3', 'antlion_1', 'antlion_1_feed', 'antlion_1_gape', 'antlion_1_snap', 'antlion_2', 'antlion_2_feed', 'antlion_2_gape', 'antlion_2_snap', 'antlion_3', 'antlion_3_feed', 'antlion_3_gape', 'antlion_3_snap']);
  /** The four forms (TandavaSettings.asset Forms), each with its three variants. */
  const FORMS = [
    { name: 'Great Serpent', role: 'eater', bankShare: 0.15, meal: 4000,
      line: 'The {0} is hungry. Wherever it eats, its hood comes off its head to guard its mouth.',
      variants: [{ key: 'great_serpent_1', name: 'Great Serpent', look: 'long and slender' },
        { key: 'great_serpent_2', name: 'Great Serpent', look: 'longer, narrow hood' },
        { key: 'great_serpent_3', name: 'Great Serpent', look: 'short and thick, broad hood' }] },
    { name: 'Many-Headed Serpent', role: 'eater', bankShare: 0.28, meal: 4000,
      line: 'Its coils split: a {0}. Every head dips to the food when it eats.',
      variants: [{ key: 'many_headed_5', name: 'Five-Headed Serpent', look: 'five heads' },
        { key: 'many_headed_7', name: 'Seven-Headed Serpent', look: 'seven heads' },
        { key: 'many_headed_10', name: 'Ten-Headed Serpent', look: 'ten heads, two tails' }] },
    { name: 'Lord of the Dance', role: 'dance', bankShare: 0, meal: 1, line: 'It has eaten enough. It rises into the Lord of the Dance.',
      variants: [{ key: 'dancer_1', name: 'Lord of the Dance', look: 'the first pose' },
        { key: 'dancer_2', name: 'Lord of the Dance', look: 'the mirror pose' },
        { key: 'dancer_3', name: 'Lord of the Dance', look: 'the hair flown wide' }] },
    { name: 'Antlion', role: 'final', bankShare: 0.4, meal: 4000,
      line: 'The drum stops. The {0} drops out of the halo, jaws open. One last feast and the cycle is complete.',
      variants: [{ key: 'antlion_1', name: 'Antlion', look: 'long sickle jaws' },
        { key: 'antlion_2', name: 'Antlion', look: 'short hooked jaws, a double fringe' },
        { key: 'antlion_3', name: 'Antlion', look: 'a broad abdomen, the longest jaws' }] },
  ];
  const PILOTS_WIN = { Shattered: 1, Starved: 1, Wiped: 1, DanceBroken: 1, HeldOff: 1 };

  // ── small vector helpers (arrays of three) ─────────────────────────────────
  const clamp = (x, a, b) => x < a ? a : x > b ? b : x;
  const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  const len = a => Math.hypot(a[0], a[1], a[2]);
  const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
  const add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
  const scale = (a, k) => [a[0] * k, a[1] * k, a[2] * k];
  const dist = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]);
  const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const norm = a => { const l = len(a); return l > 1e-9 ? [a[0] / l, a[1] / l, a[2] / l] : [0, 0, 0]; };
  function rotate(v, k, th) {   // Rodrigues: v about unit axis k by th
    const c = Math.cos(th), s = Math.sin(th), kv = cross(k, v), kd = dot(k, v) * (1 - c);
    return [v[0] * c + kv[0] * s + k[0] * kd, v[1] * c + kv[1] * s + k[1] * kd, v[2] * c + kv[2] * s + k[2] * kd];
  }
  /** Distance from p to the segment a-b. */
  function segDist2(p, a, b) {
    const ab = sub(b, a), l2 = dot(ab, ab);
    const t = l2 > 1e-9 ? clamp(dot(sub(p, a), ab) / l2, 0, 1) : 0;
    const q = [a[0] + ab[0] * t - p[0], a[1] + ab[1] * t - p[1], a[2] + ab[2] * t - p[2]];
    return dot(q, q);
  }

  function rngOf(seed) {
    let a = seed >>> 0;
    return function () {
      a = (a + 0x6D2B79F5) >>> 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  /** SwarmPlanData.Upsample(m, 0.6): m copies of every unit on a Fibonacci sphere, the body scaled by cbrt(m). */
  function upsample(src, m) {
    const N = src.n, F = src.pos.length, n2 = N * m, s = Math.cbrt(m), P0 = src.pos[0];
    let nn = 0;
    for (let i = 0; i < N; i++) {
      let best = Infinity;
      for (let j = 0; j < N; j++) {
        if (j === i) continue;
        const dx = P0[3 * i] - P0[3 * j], dy = P0[3 * i + 1] - P0[3 * j + 1], dz = P0[3 * i + 2] - P0[3 * j + 2];
        const d = dx * dx + dy * dy + dz * dz;
        if (d < best) best = d;
      }
      nn += Math.sqrt(best);
    }
    const r = 0.6 * s * nn / Math.max(1, N), ga = Math.PI * (3 - Math.sqrt(5)), dirs = [];
    for (let c = 0; c < m; c++) {
      const y = 1 - 2 * (c + 0.5) / m, rr = Math.sqrt(Math.max(0, 1 - y * y));
      dirs.push([Math.cos(ga * c) * rr, y, Math.sin(ga * c) * rr]);
    }
    const off = new Float32Array(n2 * 3), elem = [], tier = [], slot = [], half = new Float32Array(n2 * 3);
    for (let k = 0; k < N; k++) {
      let h = ((Math.imul(k, 2654435761) >>> 0) ^ 0x9E3779B9) >>> 0;
      const a1 = (h & 0xFFFF) / 65535 * 2 * Math.PI; h = (Math.imul(h, 1664525) + 1013904223) >>> 0;
      const zc = (h & 0xFFFF) / 65535 * 2 - 1; h = (Math.imul(h, 1664525) + 1013904223) >>> 0;
      const a2 = (h & 0xFFFF) / 65535 * 2 * Math.PI;
      const zr = Math.sqrt(Math.max(0, 1 - zc * zc));
      const ax = [Math.cos(a1) * zr, zc, Math.sin(a1) * zr], ca = Math.cos(a2), sa = Math.sin(a2);
      for (let c = 0; c < m; c++) {
        const v = dirs[c], o = k * m + c, d0 = ax[0] * v[0] + ax[1] * v[1] + ax[2] * v[2];
        const cx = ax[1] * v[2] - ax[2] * v[1], cy = ax[2] * v[0] - ax[0] * v[2], cz = ax[0] * v[1] - ax[1] * v[0];
        off[3 * o] = (v[0] * ca + cx * sa + ax[0] * d0 * (1 - ca)) * r;
        off[3 * o + 1] = (v[1] * ca + cy * sa + ax[1] * d0 * (1 - ca)) * r;
        off[3 * o + 2] = (v[2] * ca + cz * sa + ax[2] * d0 * (1 - ca)) * r;
        elem[o] = src.elem[k]; tier[o] = src.tier[k] || 0; slot[o] = src.slot[k] || 0;
        half[3 * o] = src.half[3 * k]; half[3 * o + 1] = src.half[3 * k + 1]; half[3 * o + 2] = src.half[3 * k + 2];
      }
    }
    const P = [], Fc = [];
    for (let f = 0; f < F; f++) {
      const p = new Float32Array(n2 * 3), q = new Float32Array(n2 * 3), sp = src.pos[f], sf = src.face[f];
      for (let k = 0; k < N; k++) for (let c = 0; c < m; c++) {
        const o = k * m + c;
        for (let a = 0; a < 3; a++) { p[3 * o + a] = sp[3 * k + a] * s + off[3 * o + a]; q[3 * o + a] = sf[3 * k + a]; }
      }
      P.push(p); Fc.push(q);
    }
    let rs = 0; for (let o = 0; o < n2; o++) rs += P[0][3 * o] ** 2 + P[0][3 * o + 1] ** 2 + P[0][3 * o + 2] ** 2;
    const mix = [0, 0, 0, 0]; for (const e of elem) mix[e]++;
    return { n: n2, per: src.fs, clip: src.clip || 1, order: src.order, elem, tier, slot, half, P, Fc, radius: Math.sqrt(rs / Math.max(1, n2)), mix };
  }

  /** Every plan the swarm config lists, upsampled to the game's density, with each eating plan's mouth and each dance
      plan's halo in WORLD units along the body axes. */
  function buildBank(data) {
    const plans = [], ix = {}, mouth = {}, ring = {}, reach = {};
    for (const key of KEYS) {
      const src = data[key];
      ix[key] = plans.length;
      plans.push(Object.assign(upsample(src, DENSITY), { key }));
      let r = 0; for (const fr of src.pos) for (let o = 0; o + 2 < fr.length; o += 3) r = Math.max(r, Math.hypot(fr[o], fr[o + 1], fr[o + 2]));
      reach[key] = r * KW;   // the farthest any unit comes from the centre (TandavaHarness.Load's Reach)
      if (src.mouth) mouth[key] = scale(src.mouth, KW);
      if (src.ring) ring[key] = { centre: scale(src.ring.centre, KW), radius: src.ring.radius * KW, orbit: src.ring.guardOrbit * KW };
    }
    return { plans, ix, mouth, ring, reach };
  }

  /** The forms a match with these picks runs: author_tandava_assets.build_forms's arithmetic. */
  function buildForms(bank, picks) {
    return FORMS.map((f, k) => {
      const v = f.variants[picks[k]], plan = bank.ix[v.key], feed = bank.ix[v.key + '_feed'];
      const coils = k < 2 ? COILS.map(c => v.key + '_' + c) : [];
      return { k, name: f.name, variant: v.name, look: v.look, key: v.key, role: f.role, line: f.line,
        coilPlanIx: coils.map(c => bank.ix[c]), coilMouths: coils.map(c => bank.mouth[c]), coilNames: coils.map(c => c.split('_').pop()),
        coilRoamRadius: coils.length ? ARENA.membrane * 0.97 - Math.max(...coils.map(c => bank.reach[c])) : 0,
        planIx: plan, feedPlanIx: feed === undefined ? -1 : feed, planCount: bank.plans[plan].n, fill: 0.9,
        bank: f.bankShare * STOMACH, meal: f.meal, mouth: bank.mouth[v.key] || [0, 0, 0], feedMouth: bank.mouth[v.key + '_feed'] || [0, 0, 0],
        lungePlanIx: LUNGES[k] ? bank.ix[v.key + '_' + LUNGES[k][0]] : -1, snapPlanIx: LUNGES[k] ? bank.ix[v.key + '_' + LUNGES[k][1]] : -1,
        lungeMouth: (LUNGES[k] && bank.mouth[v.key + '_' + LUNGES[k][0]]) || [0, 0, 0],
        ring: bank.ring[v.key] || null };
    });
  }
  function drawVariants(seed) { const r = rngOf(seed * 2654435761 + 17); return FORMS.map(f => Math.floor(r() * f.variants.length)); }

  function tryFund(st, e, cost, cross) {
    if (st[e] >= cost) { st[e] -= cost; return true; }
    let c = cost * cross, others = 0;
    for (let o = 0; o < 4; o++) if (o !== e) others += st[o];
    if (others < c) return false;
    while (c > 1e-6) {
      let best = -1;
      for (let o = 0; o < 4; o++) if (o !== e && (best < 0 || st[o] > st[best])) best = o;
      const take = Math.min(st[best], c); st[best] -= take; c -= take;
      if (take <= 0) break;
    }
    return true;
  }

  // ── the flock ──────────────────────────────────────────────────────────────
  class Swarm {
    constructor(bank, seed) {
      this.bank = bank; this.plans = bank.plans; this.rng = rngOf(seed || 7);
      const maxN = Math.max(...this.plans.map(p => p.n));
      this.cap = Math.ceil(maxN * 1.15) + 32;
      const c = this.cap;
      this.alive = new Uint8Array(c); this.age = new Int32Array(c); this.dying = new Int8Array(c); this.dieLen = new Int8Array(c);
      this.elem = new Int8Array(c); this.elemTo = new Int8Array(c); this.molt = new Float32Array(c);
      this.slot = new Int32Array(c).fill(-1); this.flash = new Float32Array(c); this.hitBy = new Int8Array(c).fill(-1);
      this.p = new Float32Array(c * 3); this.v = new Float32Array(c * 3); this.p0 = new Float32Array(c * 3);
      this.owner = new Int32Array(maxN).fill(-1);
      this.T = new Float32Array(maxN * 3); this.TV = new Float32Array(maxN * 3);
      this.planIx = 0; this.clock = 0; this.layU = 1; this.funded = true; this.grow = true;
      this.cruiseScale = 1; this.turnScale = 1; this.holdLaying = false;
      this.stomach = [0, 0, 0, 0];
      this.anchor = [0, 0, 0]; this.anchor0 = [0, 0, 0];
      this.BX = [1, 0, 0]; this.BY = [0, 1, 0]; this.BZ = [0, 0, 1];
      this.BX0 = [1, 0, 0]; this.BY0 = [0, 1, 0]; this.BZ0 = [0, 0, 1];
      this.lost = 0; this.laid = 0; this.events = [];
    }
    get plan() { return this.plans[this.planIx]; }
    get fill() { const s = this.stomach; return (s[0] + s[1] + s[2] + s[3]) / STOMACH; }
    frame(clock) {
      const plan = this.plan, L = plan.order.length, u = clock / plan.per, fl = Math.floor(u);
      const i = ((fl % L) + L) % L;
      return { a: plan.order[i], b: plan.order[(i + 1) % L], t: u - fl };
    }
    computeTargets() {
      const plan = this.plan, f = this.frame(this.clock), A = plan.P[f.a], B = plan.P[f.b], t = f.t, inv = 1 / plan.per;
      for (let k = 0; k < plan.n * 3; k++) { this.T[k] = A[k] + (B[k] - A[k]) * t; this.TV[k] = (B[k] - A[k]) * inv; }
    }
    free() { for (let i = 0; i < this.cap; i++) if (!this.alive[i]) return i; return -1; }
    spawn(s, x, y, z, hatched) {
      const i = this.free(); if (i < 0) return -1;
      const e = this.plan.elem[s];
      this.alive[i] = 1; this.dying[i] = 0; this.dieLen[i] = CORE.dieSteps; this.age[i] = hatched ? CORE.bloomSteps : 0;
      this.elem[i] = e; this.elemTo[i] = e; this.molt[i] = 1; this.slot[i] = s; this.owner[s] = i; this.hitBy[i] = -1;
      this.flash[i] = hatched ? 0 : 0.7;
      this.p[3 * i] = x; this.p[3 * i + 1] = y; this.p[3 * i + 2] = z;
      this.p0[3 * i] = x; this.p0[3 * i + 1] = y; this.p0[3 * i + 2] = z;
      this.v[3 * i] = this.v[3 * i + 1] = this.v[3 * i + 2] = 0;
      return i;
    }
    /** Hatch WHOLE as the plan (SeedMembers x density, capped at the plan): no young form. */
    seed(planIx, at, heading) {
      this.alive.fill(0); this.slot.fill(-1); this.owner.fill(-1);
      this.planIx = planIx; this.clock = 0; this.layU = 1; this.lost = 0; this.laid = 0; this.events = [];
      this.stomach = [0, 0, 0, 0]; this.cruiseScale = 1; this.turnScale = 1; this.holdLaying = false;
      this.anchor = at.slice(); this.anchor0 = at.slice();
      this.BX = [1, 0, 0]; this.BY = [0, 1, 0]; this.BZ = [0, 0, 1];
      if (heading) this.setHeading(norm(heading), true);
      this.BX0 = this.BX.slice(); this.BY0 = this.BY.slice(); this.BZ0 = this.BZ.slice();
      this.computeTargets();
      const n = Math.min(CORE.seed, this.plan.n);
      const idx = [...Array(this.plan.n).keys()];
      for (let q = idx.length - 1; q > 0; q--) { const j = Math.floor(this.rng() * (q + 1)); [idx[q], idx[j]] = [idx[j], idx[q]]; }
      for (let q = 0; q < n; q++) {
        const s = idx[q];
        this.spawn(s, this.T[3 * s] + (this.rng() - 0.5), this.T[3 * s + 1] + (this.rng() - 0.5), this.T[3 * s + 2] + (this.rng() - 0.5), true);
      }
    }
    isLive(i) { return this.alive[i] && !this.dying[i]; }
    liveCount() { let n = 0; for (let i = 0; i < this.cap; i++) if (this.isLive(i)) n++; return n; }
    mixLive() { const m = [0, 0, 0, 0]; for (let i = 0; i < this.cap; i++) if (this.isLive(i)) m[this.elemTo[i]]++; return m; }
    /** SwarmSortCore.SetLevers. */
    setLevers(cruise, turn, hold) { this.cruiseScale = cruise; this.turnScale = turn; this.holdLaying = hold; }

    /** A FORM commit (RequestPlan): every member re-paired to the new body, same element first, nearest first; the rest
        molt into whatever the new body still lacks. Nobody dies. The lay ease restarts. */
    requestForm(k) {
      if (k === this.planIx || k < 0 || k >= this.plans.length) return false;
      const from = this.planIx;
      this.planIx = k; this.owner.fill(-1); this.computeTargets();
      const plan = this.plan, members = [];
      for (let i = 0; i < this.cap; i++) if (this.isLive(i)) { members.push(i); this.slot[i] = -1; }
      const taken = new Uint8Array(this.cap), openSlots = [], order = [...Array(plan.n).keys()];
      for (let q = order.length - 1; q > 0; q--) { const j = Math.floor(this.rng() * (q + 1)); [order[q], order[j]] = [order[j], order[q]]; }
      const nearest = (s, same) => {
        let best = -1, bd = Infinity; const tx = this.T[3 * s], ty = this.T[3 * s + 1], tz = this.T[3 * s + 2];
        for (const i of members) {
          if (taken[i] || (same && this.elemTo[i] !== plan.elem[s])) continue;
          const dx = this.p[3 * i] - tx, dy = this.p[3 * i + 1] - ty, dz = this.p[3 * i + 2] - tz, d = dx * dx + dy * dy + dz * dz;
          if (d < bd) { bd = d; best = i; }
        }
        return best;
      };
      for (const s of order) { const i = nearest(s, true); if (i >= 0) { taken[i] = 1; this.slot[i] = s; this.owner[s] = i; } else openSlots.push(s); }
      let molts = 0;
      for (const s of openSlots) {
        const i = nearest(s, false); if (i < 0) break;
        taken[i] = 1; this.slot[i] = s; this.owner[s] = i;
        if (this.elemTo[i] !== plan.elem[s]) { this.elem[i] = this.elemTo[i]; this.elemTo[i] = plan.elem[s]; this.molt[i] = 0; molts++; }
      }
      for (const i of members) this.flash[i] = Math.max(this.flash[i], 0.5);
      this.layU = 0;
      this.events.push({ kind: 'form', from, to: k, molts });
      return true;
    }
    /** A POSE commit (RequestPose): the feed twin carries the same units in the same order, so every member keeps its
        slot and only the targets move. The lay ease is kept (feeding is not a wound). */
    requestPose(k) {
      if (k === this.planIx || k < 0 || k >= this.plans.length) return false;
      if (this.plans[k].n !== this.plan.n) return this.requestForm(k);
      this.planIx = k; this.computeTargets();
      this.events.push({ kind: 'pose', to: k });
      return true;
    }
    kill(i, by, dieLen) {
      if (!this.isLive(i)) return false;
      this.dieLen[i] = dieLen || CORE.dieSteps; this.dying[i] = this.dieLen[i]; this.flash[i] = 1; this.hitBy[i] = by === undefined ? -1 : by;
      if (this.slot[i] >= 0) this.owner[this.slot[i]] = -1;
      this.slot[i] = -1; this.lost++;
      return true;
    }
    /** Kill the n live members furthest along -along (body frame): a cut from that end. */
    cut(share, along, by) {
      const live = []; for (let i = 0; i < this.cap; i++) if (this.isLive(i)) live.push(i);
      const ax = along || [1, 0, 0];
      live.sort((a, b) => (this.p[3 * a] * ax[0] + this.p[3 * a + 1] * ax[1] + this.p[3 * a + 2] * ax[2]) - (this.p[3 * b] * ax[0] + this.p[3 * b + 1] * ax[1] + this.p[3 * b + 2] * ax[2]));
      const n = Math.round(share * live.length); let k = 0;
      for (let q = 0; q < n; q++) if (this.kill(live[q], by)) k++;
      return k;
    }
    starvationVictim() {
      const live = []; for (let i = 0; i < this.cap; i++) if (this.isLive(i)) live.push(i);
      return live.length ? live[Math.floor(this.rng() * live.length)] : -1;
    }
    /** SwarmSortCore.SetHeading: turn toward h by at most the turn lever; up from world Y. */
    setHeading(h, snap) {
      const H = this.BX;
      if (!snap) {
        const ang = Math.acos(clamp(dot(H, h), -1, 1)), turn = CORE.turn * this.turnScale;
        if (ang > turn) {
          let ax = cross(H, h);
          if (len(ax) < 1e-5) ax = Math.abs(H[1]) < 0.9 ? cross(H, [0, 1, 0]) : cross(H, [1, 0, 0]);
          h = norm(rotate(H, norm(ax), turn));
        }
      }
      const up = Math.abs(h[1]) < 0.95 ? [0, 1, 0] : (len(this.BY) > 0.5 ? this.BY : [1, 0, 0]);
      const y = sub(up, scale(h, dot(up, h)));
      if (len(y) < 1e-4) return;
      this.BX = h; this.BY = norm(y); this.BZ = cross(this.BX, this.BY);
    }
    /** Body coordinates (plan voxels) to world, optionally interpolated over the last step by alpha. */
    toWorld(lx, ly, lz, out, alpha) {
      let A = this.anchor, X = this.BX, Y = this.BY, Z = this.BZ;
      if (alpha !== undefined && alpha < 1) {
        const a = alpha, A0 = this.anchor0, X0 = this.BX0, Y0 = this.BY0, Z0 = this.BZ0;
        A = [A0[0] + (A[0] - A0[0]) * a, A0[1] + (A[1] - A0[1]) * a, A0[2] + (A[2] - A0[2]) * a];
        X = [X0[0] + (X[0] - X0[0]) * a, X0[1] + (X[1] - X0[1]) * a, X0[2] + (X[2] - X0[2]) * a];
        Y = [Y0[0] + (Y[0] - Y0[0]) * a, Y0[1] + (Y[1] - Y0[1]) * a, Y0[2] + (Y[2] - Y0[2]) * a];
        Z = [Z0[0] + (Z[0] - Z0[0]) * a, Z0[1] + (Z[1] - Z0[1]) * a, Z0[2] + (Z[2] - Z0[2]) * a];
      }
      out[0] = A[0] + (X[0] * lx + Y[0] * ly + Z[0] * lz) * UNIT;
      out[1] = A[1] + (X[1] * lx + Y[1] * ly + Z[1] * lz) * UNIT;
      out[2] = A[2] + (X[2] * lx + Y[2] * ly + Z[2] * lz) * UNIT;
      return out;
    }
    memberWorld(i, out, alpha) {
      const j = 3 * i;
      if (alpha === undefined || alpha >= 1) return this.toWorld(this.p[j], this.p[j + 1], this.p[j + 2], out);
      const p0 = this.p0, p = this.p, a = alpha;
      return this.toWorld(p0[j] + (p[j] - p0[j]) * a, p0[j + 1] + (p[j + 1] - p0[j + 1]) * a, p0[j + 2] + (p[j + 2] - p0[j + 2]) * a, out, alpha);
    }
    /** World offset in the body axes: x forward, y up, z side. */
    inBody(l) { const X = this.BX, Y = this.BY, Z = this.BZ; return [X[0] * l[0] + Y[0] * l[1] + Z[0] * l[2], X[1] * l[0] + Y[1] * l[1] + Z[1] * l[2], X[2] * l[0] + Y[2] * l[1] + Z[2] * l[2]]; }
    poisson(l) {
      if (l > 30) return Math.max(0, Math.round(l + Math.sqrt(l) * (this.rng() + this.rng() + this.rng() - 1.5) * 2));
      const L = Math.exp(-l); let k = 0, p = 1;
      do { k++; p *= this.rng(); } while (p > L);
      return k - 1;
    }

    step(goal) {
      const c = this.cap, P = this.p, V = this.v;
      this.p0.set(P); this.anchor0 = this.anchor.slice(); this.BX0 = this.BX.slice(); this.BY0 = this.BY.slice(); this.BZ0 = this.BZ.slice();
      this.clock++;
      this.computeTargets();
      // ── the body swims (SwarmSortCore's GAME swim): aim at the goal at the turn lever, cruise toward it, slow and
      //    station-keep within 1.5 plan radii; members ride the turn rigidly (they live in body coordinates)
      const plan = this.plan, Rw = plan.radius * UNIT;
      if (goal) {
        const toT = sub(goal, this.anchor), dT = len(toT);
        if (dT > CORE.aimHold * Rw) this.setHeading(scale(toT, 1 / dT));
        const pace = CORE.cruise * this.cruiseScale * UNIT * clamp(dT / Math.max(1, 1.5 * Rw), 0, 1);
        let sw = [0, 0, 0];
        if (dT > CORE.aimHold * Rw) sw = scale(this.BX, pace * Math.max(0, dot(this.BX, toT) / dT));
        else if (dT > 1e-3) sw = scale(toT, pace / dT);
        this.anchor = add(this.anchor, sw);
        const r = len(this.anchor), lim = ARENA.membrane * 0.97 - 40;
        if (r > lim) this.anchor = scale(this.anchor, lim / r);
      }
      // ── members
      const grid = new Map(), cell = CORE.spacing, key = (x, y, z) => ((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) | 0;
      for (let i = 0; i < c; i++) {
        if (!this.alive[i] || this.dying[i]) continue;
        const k = key(Math.floor(P[3 * i] / cell), Math.floor(P[3 * i + 1] / cell), Math.floor(P[3 * i + 2] / cell));
        let b = grid.get(k); if (!b) grid.set(k, b = []); b.push(i);
      }
      const rng = this.rng, T = this.T, TV = this.TV, s2 = CORE.spacing * CORE.spacing;
      for (let i = 0; i < c; i++) {
        if (!this.alive[i]) continue;
        const j = 3 * i;
        if (this.dying[i]) {
          V[j] *= 0.9; V[j + 1] *= 0.9; V[j + 2] *= 0.9; P[j] += V[j]; P[j + 1] += V[j + 1]; P[j + 2] += V[j + 2];
          if (--this.dying[i] <= 0) { this.alive[i] = 0; this.slot[i] = -1; }
          continue;
        }
        this.age[i]++; this.flash[i] *= 0.84;
        if (this.molt[i] < 1) { this.molt[i] = Math.min(1, this.molt[i] + 1 / CORE.moltSteps); if (this.molt[i] >= 1) this.elem[i] = this.elemTo[i]; }
        const s = this.slot[i];
        let fx, fy, fz, ffx = 0, ffy = 0, ffz = 0;
        if (s >= 0) {
          fx = (T[3 * s] - P[j]) * CORE.gain; fy = (T[3 * s + 1] - P[j + 1]) * CORE.gain; fz = (T[3 * s + 2] - P[j + 2]) * CORE.gain;
          ffx = TV[3 * s]; ffy = TV[3 * s + 1]; ffz = TV[3 * s + 2];
        } else {   // a spare: a loose cloud about the body
          const ox = Math.sin(this.clock * 0.05 + i) * 6, oy = Math.cos(this.clock * 0.04 + i * 1.7) * 4;
          fx = (ox - P[j]) * CORE.gain * 0.08; fy = (oy - P[j + 1]) * CORE.gain * 0.08; fz = (Math.sin(i * 2.3) * 6 - P[j + 2]) * CORE.gain * 0.08;
        }
        const fm = Math.hypot(fx, fy, fz);
        const clip = CORE.clip * this.plan.clip;   // SwarmSortParams.PlanWellClip: the Antlion's jaws pull harder
        if (fm > clip) { const q = clip / fm; fx *= q; fy *= q; fz *= q; }
        let rx = 0, ry = 0, rz = 0;
        const gx = Math.floor(P[j] / cell), gy = Math.floor(P[j + 1] / cell), gz = Math.floor(P[j + 2] / cell);
        for (let a = -1; a <= 1; a++) for (let b2 = -1; b2 <= 1; b2++) for (let d = -1; d <= 1; d++) {
          const bucket = grid.get(key(gx + a, gy + b2, gz + d)); if (!bucket) continue;
          for (const o of bucket) {
            if (o === i) continue;
            const ex = P[j] - P[3 * o], ey = P[j + 1] - P[3 * o + 1], ez = P[j + 2] - P[3 * o + 2], d2 = ex * ex + ey * ey + ez * ez;
            if (d2 >= s2 || d2 < 1e-8) continue;
            const dd = Math.sqrt(d2), push = (CORE.spacing - dd) / CORE.spacing * CORE.repulsion / dd;
            rx += ex * push; ry += ey * push; rz += ez * push;
          }
        }
        const nz = CORE.noise, k1 = CORE.inertia, k2 = 1 - CORE.inertia;
        V[j] = k1 * V[j] + k2 * ffx + fx + rx + (rng() - 0.5) * nz;
        V[j + 1] = k1 * V[j + 1] + k2 * ffy + fy + ry + (rng() - 0.5) * nz;
        V[j + 2] = k1 * V[j + 2] + k2 * ffz + fz + rz + (rng() - 0.5) * nz;
        const vm = CORE.vmax[this.elemTo[i]], sp = Math.hypot(V[j], V[j + 1], V[j + 2]);
        if (sp > vm) { const q = vm / sp; V[j] *= q; V[j + 1] *= q; V[j + 2] *= q; }
        P[j] += V[j]; P[j + 1] += V[j + 1]; P[j + 2] += V[j + 2];
      }
      // ── Potts swaps: two members of one element trade slots when that brings both nearer home
      const n = plan.n;
      for (let q = 0; q < CORE.swaps; q++) {
        const s1 = Math.floor(rng() * n), s2b = Math.floor(rng() * n);
        const i1 = this.owner[s1], i2 = this.owner[s2b];
        if (i1 < 0 || i2 < 0 || i1 === i2 || plan.elem[s1] !== plan.elem[s2b]) continue;
        const d = (i, s) => { const ex = P[3 * i] - T[3 * s], ey = P[3 * i + 1] - T[3 * s + 1], ez = P[3 * i + 2] - T[3 * s + 2]; return ex * ex + ey * ey + ez * ez; };
        if (d(i1, s2b) + d(i2, s1) < d(i1, s1) + d(i2, s2b)) { this.owner[s1] = i2; this.owner[s2b] = i1; this.slot[i1] = s2b; this.slot[i2] = s1; }
      }
      for (let i = 0; i < c; i++) {   // a spare takes any open slot of its element
        if (!this.isLive(i) || this.slot[i] >= 0) continue;
        for (let s = 0; s < n; s++) if (this.owner[s] < 0 && plan.elem[s] === this.elemTo[i]) { this.owner[s] = i; this.slot[i] = s; break; }
      }
      this.lay();
    }

    /** SwarmSortCore.Lay's shape: Poisson(LayRate x members) a step, at most LayMax, eased in over SortLayRampSeconds
        after a FORM commit, held by the lever; each egg paid from the stomach (TryFund) and laid at the wound. */
    lay() {
      this.lastLaid = 0;
      if (!this.grow || this.holdLaying) return;
      const plan = this.plan, n = plan.n;
      let live = 0; for (let i = 0; i < this.cap; i++) if (this.isLive(i)) live++;
      this.layU = Math.min(1, this.layU + 1 / CORE.layRamp);
      const u = this.layU, layMax = Math.max(1, Math.round(CORE.layMax * u * u * (3 - 2 * u)));
      const room = n - live; if (room <= 0) return;
      const nlay = Math.min(layMax, room, this.poisson(Math.max(CORE.layRate * live, 0.2)));
      if (nlay <= 0) return;
      const open = [[], [], [], []], total = [0, 0, 0, 0];
      for (let s = 0; s < n; s++) { total[plan.elem[s]]++; if (this.owner[s] < 0) open[plan.elem[s]].push(s); }
      for (let l = 0; l < nlay; l++) {
        let e = -1, best = -1;
        for (let k = 0; k < 4; k++) if (open[k].length) { const need = open[k].length / total[k] + this.rng() * 0.05; if (need > best) { best = need; e = k; } }
        if (e < 0) break;
        if (this.funded && !tryFund(this.stomach, e, EGG[e], CORE.cross)) break;   // hungry: it waits for a meal
        const s = open[e].splice(Math.floor(this.rng() * open[e].length), 1)[0];
        let par = -1, bd = Infinity;
        for (let i = 0; i < this.cap; i++) {
          if (!this.isLive(i)) continue;
          const dx = this.p[3 * i] - this.T[3 * s], dy = this.p[3 * i + 1] - this.T[3 * s + 1], dz = this.p[3 * i + 2] - this.T[3 * s + 2];
          const d = dx * dx + dy * dy + dz * dz; if (d < bd) { bd = d; par = i; }
        }
        const bx = par >= 0 ? this.p[3 * par] : this.T[3 * s], by = par >= 0 ? this.p[3 * par + 1] : this.T[3 * s + 1], bz = par >= 0 ? this.p[3 * par + 2] : this.T[3 * s + 2];
        const th = this.rng() * 2 * Math.PI, zz = this.rng() * 2 - 1, rr = Math.sqrt(1 - zz * zz);
        if (this.spawn(s, bx + Math.cos(th) * rr * 1.1, by + zz * 1.1, bz + Math.sin(th) * rr * 1.1, false) >= 0) { this.lastLaid++; this.laid++; }
      }
    }
    fillAll() { const plan = this.plan; for (let s = 0; s < plan.n; s++) if (this.owner[s] < 0) this.spawn(s, this.T[3 * s], this.T[3 * s + 1], this.T[3 * s + 2], false); }
    /** Is member i wearing a DANGER plate now (SwarmTickSettings.PlanDanger: a Charge member in a tier-1 well)? */
    isDanger(i) { const s = this.slot[i]; return s >= 0 && this.elemTo[i] === 0 && this.plan.tier[s] === 1; }
    isShield(i) { const s = this.slot[i]; return s >= 0 && this.elemTo[i] === 0 && this.plan.tier[s] === 2; }
  }

  // ── the director (TandavaDirectorCore, ported) ────────────────────────────
  class Director {
    constructor(forms, settings, seed) {
      this.forms = forms; this.S = settings; this.rng = rngOf((seed || 1) * 7919 + 3); this.events = [];
      this.formIx = 0; this.phase = 'Roam'; this.mood = 'Calm'; this.outcome = 'Running'; this.threat = 0; this.clock = 0;
      this.goal = settings.centre.slice(); this.targetFood = -1; this.cruiseScale = 1; this.turnScale = 1; this.holdLaying = false;
      this.eatenHere = 0; this.dancePoint = [0, 0, 0]; this.danceTime = 0; this.haloOutMask = 0; this.haloOut = 0;
      this.restUntil = new Map(); this.feedSince = 0; this.eatenAtMeal = 0; this.phaseSince = 0; this.fleeUntil = -1; this.lossRate = 0;
      this.lostAtMeal = 0; this.lostPrev = -1; this.armed = false; this.haveWander = false; this.wanderAt = [0, 0, 0];
      this.lungeUntil = -1; this.lungeReadyAt = 0; this.lunges = 0;
      this.coil = -1; this.coilFor = -1; this.coilForm = -1; this.snapUntil = -1;
    }
    get lunging() { return this.mood === 'Lunging'; }
    get form() { return this.forms[this.formIx]; }
    get feeding() { return this.phase === 'Feed'; }
    get inDance() { return this.phase === 'Dance'; }
    get finalIx() { return this.forms.length - 1; }
    get isFinalForm() { return this.formIx === this.finalIx; }
    get snapping() { return this.clock < this.snapUntil; }
    get wantPlan() {
      if (this.feeding && this.coil >= 0) return this.form.coilPlanIx[this.coil];
      if (this.snapping && this.form.snapPlanIx >= 0) return this.form.snapPlanIx;
      if (this.lunging && this.form.lungePlanIx >= 0) return this.form.lungePlanIx;
      return (this.feeding || this.lunging) && this.form.feedPlanIx >= 0 ? this.form.feedPlanIx : this.form.planIx;
    }
    /** The meal's formation, by name ('' in the strike pose). */
    get coilName() { return this.coil >= 0 ? this.form.coilNames[this.coil] : ''; }
    get mealMouth() { return this.coil >= 0 ? this.form.coilMouths[this.coil] : this.form.feedMouth; }
    get mealRoamRadius() { return this.coil >= 0 ? Math.max(this.S.roamRadius, this.form.coilRoamRadius) : this.S.roamRadius; }
    /** A formation for a meal at this plant: a different one from the last, while the form has more than one. */
    drawCoil(plantId) {
      const n = this.form.coilPlanIx.length, last = this.formIx === this.coilForm ? this.coil : -1;
      this.coil = n === 0 ? -1 : n === 1 ? 0 : last < 0 ? Math.floor(this.rng() * n) : (last + 1 + Math.floor(this.rng() * (n - 1))) % n;
      this.coilFor = plantId; this.coilForm = this.formIx;
    }
    get drumRemaining() { return this.inDance ? Math.max(0, this.S.drumSeconds - this.danceTime) : 0; }
    get riseRemaining() { return this.phase === 'Rising' ? Math.max(0, this.S.riseSeconds - (this.clock - this.phaseSince)) : 0; }
    get timeRemaining() { return this.S.matchSeconds > 0 ? Math.max(0, this.S.matchSeconds - this.clock) : -1; }
    progress(s) {
      const f = this.form;
      if (f.role === 'dance') return this.phase === 'Dance' && this.S.drumSeconds > 0 ? clamp(this.danceTime / this.S.drumSeconds, 0, 1) : 0;
      const fill = f.planCount > 0 ? s.alive / (f.fill * f.planCount) : 1;
      const bank = f.bank > 0 ? (s.stomach[0] + s.stomach[1] + s.stomach[2] + s.stomach[3]) / f.bank : 1;
      return clamp(Math.min(fill, bank), 0, 1);
    }
    bodyFraction(s) { return this.form.planCount > 0 ? s.alive / this.form.planCount : 1; }
    haloIsOut(k) { return k >= 0 && k < 32 && (this.haloOutMask & (1 << k)) !== 0; }
    breakHalo(k) {
      if (!this.inDance || this.outcome !== 'Running' || k < 0 || k >= this.S.haloCount || this.haloIsOut(k)) return false;
      this.haloOutMask |= 1 << k; this.haloOut++;
      this.events.push({ kind: 'haloBroken', ring: k, count: this.haloOut });
      if (this.haloOut >= this.S.haloToBreak) this.end('DanceBroken');
      return true;
    }
    static inBody(s, l) { const X = s.forward, Y = s.up, Z = s.side; return [X[0] * l[0] + Y[0] * l[1] + Z[0] * l[2], X[1] * l[0] + Y[1] * l[1] + Z[1] * l[2], X[2] * l[0] + Y[2] * l[1] + Z[2] * l[2]]; }

    tick(dt, s, food, pilots) {
      if (this.outcome !== 'Running') return;
      dt = Math.max(0, dt); this.clock += dt;
      const S = this.S;
      if (s.alive <= 0) { this.end(s.starving ? 'Starved' : 'Wiped'); return; }
      const form = this.form;
      if (!this.armed && s.alive >= S.armFraction * form.planCount) this.armed = true;
      if (this.armed && s.alive < S.shatterFraction * form.planCount) { this.end(s.starving ? 'Starved' : 'Shattered'); return; }
      if (S.matchSeconds > 0 && this.clock >= S.matchSeconds) { this.end('HeldOff'); return; }
      this.senseThreat(dt, s, pilots);
      switch (this.phase) {
        case 'Roam':
          if (this.progress(s) >= 1) { this.advance(s); if (this.outcome !== 'Running') return; }
          if (this.phase === 'Roam') this.roam(s, food, pilots);
          break;
        case 'Feed':
          this.feedTick(s, food);
          if (this.phase === 'Roam') this.roam(s, food, pilots);
          break;
        case 'Rising':
          this.goal = this.dancePoint.slice();
          if (this.clock - this.phaseSince >= S.riseSeconds) {
            this.phase = 'Dance'; this.phaseSince = this.clock; this.danceTime = 0; this.haloOut = 0; this.haloOutMask = 0;
            this.events.push({ kind: 'danceBegan', form: this.formIx });
          }
          break;
        case 'Dance':
          this.goal = this.dancePoint.slice();
          this.danceTime += dt;
          if (this.danceTime >= S.drumSeconds) {
            this.commit(this.finalIx, s);
            this.phase = 'Roam'; this.phaseSince = this.clock; this.danceTime = 0;
            this.roam(s, food, pilots);
          }
          break;
      }
      if (this.outcome === 'Running') this.levers();
    }
    advance(s) {
      if (this.form.role === 'final') { this.end('Completed'); return; }
      const next = this.formIx + 1;
      if (next >= this.forms.length) { this.end('Completed'); return; }
      if (this.forms[next].role === 'dance') {
        const from = this.formIx;
        this.commit(next, s);
        this.dancePoint = this.clampInside(s.anchor, Math.max(0, this.S.roamRadius - this.S.danceReach));
        this.phase = 'Rising'; this.phaseSince = this.clock; this.targetFood = -1;
        this.goal = this.dancePoint.slice();
        this.events.push({ kind: 'rising', from, to: next });
        return;
      }
      this.commit(next, s);
    }
    commit(to, s) {
      const from = this.formIx; this.formIx = to;
      this.events.push({ kind: 'form', from, to });
      this.armed = s.alive >= this.S.armFraction * this.forms[to].planCount;
    }
    senseThreat(dt, s, pilots) {
      const S = this.S;
      let prox = 0;
      for (const p of pilots || []) {
        const rel = sub(p.position, s.anchor), d = len(rel);
        if (d >= S.senseRadius) continue;
        const near = 1 - d / Math.max(1, S.senseRadius);
        const closing = d > 1e-3 ? -dot(p.velocity, rel) / d : 0;
        const charge = clamp(closing / Math.max(1, S.approachSpeed), 0, 1);
        const w = clamp(near * (0.55 + 0.45 * charge), 0, 1);
        prox = 1 - (1 - prox) * (1 - w);
      }
      const lost = this.lostPrev < 0 ? 0 : Math.max(0, s.lost - this.lostPrev);
      this.lostPrev = s.lost;
      if (dt > 0) this.lossRate += (lost / dt - this.lossRate) * (1 - Math.exp(-dt / 0.5));
      const wound = clamp(this.lossRate / Math.max(1, S.damageRef * this.form.planCount), 0, 1);
      const raw = Math.max(prox, wound);
      if (dt > 0) { const tau = raw > this.threat ? S.threatRise : S.threatFall; this.threat += (raw - this.threat) * (1 - Math.exp(-dt / Math.max(1e-3, tau))); }
      this.threat = clamp(this.threat, 0, 1);
      this.proximity = prox; this.wound = wound;
      // healthy and roaming, it turns on a pilot that comes close (a lunge); only a HURT one bolts
      const was = this.mood, roaming = this.phase === 'Roam', hurt = s.alive < S.lungeBodyMin * this.form.planCount;
      const canBolt = roaming && hurt, nearest = Director.nearestPilot(s, pilots).d;
      const canLunge = roaming && !hurt && this.clock >= this.lungeReadyAt && nearest <= S.lungeRadius;
      let mood = this.mood;
      if (this.clock < this.fleeUntil) mood = 'Fleeing';
      else if (this.mood === 'Lunging') {
        if (!roaming || hurt || this.clock >= this.lungeUntil || nearest > 1.5 * S.lungeRadius) { this.endLunge(); mood = this.threat >= S.waryExit ? 'Wary' : 'Calm'; }
      }
      else if (canLunge && this.threat >= S.lungeEnter) { mood = 'Lunging'; this.lungeUntil = this.clock + S.lungeSeconds; this.lunges++; }
      else if (this.mood === 'Calm') { if (canBolt && this.threat >= S.fleeEnter) mood = 'Fleeing'; else if (this.threat >= S.waryEnter) mood = 'Wary'; }
      else if (this.mood === 'Wary') { if (canBolt && this.threat >= S.fleeEnter) mood = 'Fleeing'; else if (this.threat < S.waryExit) mood = 'Calm'; }
      else if (this.threat < S.fleeExit || !canBolt) mood = this.threat < S.waryExit ? 'Calm' : 'Wary';
      if (mood === 'Fleeing' && was !== 'Fleeing' && this.fleeUntil < this.clock) this.fleeUntil = this.clock + S.fleeMinSeconds;
      this.setMood(mood);
    }
    endLunge() { this.lungeUntil = -1; this.lungeReadyAt = this.clock + this.S.lungeCooldownSeconds; }
    static nearestPilot(s, pilots) {
      let d = Infinity, ix = -1;
      (pilots || []).forEach((p, k) => { const dd = dist(p.position, s.anchor); if (dd < d) { d = dd; ix = k; } });
      return { d, ix };
    }
    setMood(mood) { if (mood === this.mood) return; this.events.push({ kind: 'mood', from: this.mood, to: mood }); this.mood = mood; }
    levers() { const l = Director.leversFor(this.phase, this.mood, this.S, this.snapping && this.form.snapPlanIx >= 0); this.cruiseScale = l.cruise; this.turnScale = l.turn; this.holdLaying = l.hold; }
    static leversFor(phase, mood, s, snapping) {
      if (phase === 'Feed') return { cruise: s.cruiseFeed, turn: s.turnFeed, hold: true };
      if (phase === 'Rising' || phase === 'Dance') return { cruise: s.cruiseFeed, turn: s.turnFeed, hold: false };
      if (phase === 'Over') return { cruise: 1, turn: 1, hold: false };
      if (snapping) return { cruise: s.cruiseLunge, turn: s.turnSnap, hold: false };   // committed to the bite
      if (mood === 'Lunging') return { cruise: s.cruiseLunge, turn: s.turnLunge, hold: false };
      if (mood === 'Fleeing') return { cruise: s.cruiseFlee, turn: s.turnFlee, hold: false };
      if (mood === 'Wary') return { cruise: s.cruiseWary, turn: s.turnWary, hold: false };
      return { cruise: s.cruiseCalm, turn: s.turnCalm, hold: false };
    }
    roam(s, food, pilots) {
      if (this.mood === 'Fleeing') { this.targetFood = -1; this.goal = this.fleePoint(s, pilots); return; }
      if (this.mood === 'Lunging') {
        const n = Director.nearestPilot(s, pilots);
        if (n.ix >= 0) {   // at the pilot, led by its velocity, JAWS first: the guard ring round its mouth is the weapon
          this.targetFood = -1;
          const p = pilots[n.ix], aim = add(p.position, scale(p.velocity, this.S.lungeLead));
          const bite = this.form.lungePlanIx >= 0 ? this.form.lungeMouth : this.form.feedMouth;
          this.goal = this.clampInside(sub(aim, Director.inBody(s, bite)), this.S.roamRadius);
          const jaws = add(s.anchor, Director.inBody(s, bite)), gap = dist(jaws, p.position);
          if (this.form.snapPlanIx >= 0 && !this.snapping && (gap <= this.S.snapReach || this.clock >= this.lungeUntil - this.S.snapLeadSeconds)) {
            this.snapUntil = this.clock + this.S.snapHoldSeconds;   // the jaws slam shut on it
            this.events.push({ kind: 'snapped', gap });
          }
          if (gap <= this.S.lungeReach) { this.endLunge(); this.setMood('Wary'); }
          return;
        }
      }
      const k = this.chooseFood(s, food, pilots);
      if (k < 0) { this.targetFood = -1; this.goal = this.wanderPoint(s); return; }
      const plant = food[k];
      this.targetFood = plant.id; this.haveWander = false;
      if (plant.id !== this.coilFor || this.formIx !== this.coilForm) this.drawCoil(plant.id);
      // the meal's MOUTH on the plant: the head in the strike pose; rolling up, the coil's centre - it swims over the
      // plant until its middle is there, and curls round it
      const reach = this.coil >= 0 ? this.form.coilMouths[this.coil] : this.form.mouth;
      this.goal = this.clampInside(sub(plant.position, Director.inBody(s, reach)), this.mealRoamRadius);
      const mouth = add(s.anchor, Director.inBody(s, reach));
      if (this.form.feedPlanIx >= 0 && dist(mouth, plant.position) <= this.S.arriveMargin) this.beginFeed(s, plant);
    }
    chooseFood(s, food, pilots) {
      if (!food) return -1;
      const S = this.S, fear = this.mood === 'Wary' ? S.fearWary : S.fearCalm;
      let best = -1, bestScore = 0;
      for (let k = 0; k < food.length; k++) {
        const f = food[k];
        if (f.volume < S.minFood) continue;
        const until = this.restUntil.get(f.id);
        if (until !== undefined && this.clock < until) continue;
        const d = dist(f.position, s.anchor);
        let safety = 1;
        for (const p of pilots || []) safety *= 1 - fear * Math.exp(-dist(p.position, f.position) / Math.max(1, S.safeRadius));
        let score = Math.sqrt(f.volume) * safety / (d + S.distanceBias);
        if (f.id === this.targetFood) score *= S.stickBonus;
        if (score > bestScore) { bestScore = score; best = k; }
      }
      return best;
    }
    fleePoint(s, pilots) {
      const S = this.S;
      let c = [0, 0, 0], w = 0;
      for (const p of pilots || []) { const wk = 1 / Math.max(25, dist(p.position, s.anchor)); c = add(c, scale(p.position, wk)); w += wk; }
      let away = w > 0 ? sub(s.anchor, scale(c, 1 / w)) : scale(s.forward, -1);
      if (dot(away, away) < 1e-4) away = s.forward.slice();
      away = norm(away);
      const inside = this.clampInside(add(s.anchor, scale(away, S.fleeReach)), S.roamRadius);
      if (dist(inside, s.anchor) >= 0.35 * S.fleeReach) return inside;
      let radial = sub(s.anchor, S.centre);
      radial = dot(radial, radial) > 1e-4 ? norm(radial) : [1, 0, 0];
      let along = sub(away, scale(radial, dot(away, radial)));
      if (dot(along, along) < 1e-4) along = cross(radial, Math.abs(radial[1]) < 0.9 ? [0, 1, 0] : [1, 0, 0]);
      along = norm(along);
      return this.clampInside(sub(add(s.anchor, scale(along, S.fleeReach)), scale(radial, 0.25 * S.fleeReach)), S.roamRadius);
    }
    wanderPoint(s) {
      const S = this.S;
      if (this.haveWander && dist(this.wanderAt, s.anchor) > 0.25 * S.wanderReach) return this.wanderAt.slice();
      for (let tries = 0; tries < 12; tries++) {
        const d = [this.rng() * 2 - 1, this.rng() * 2 - 1, this.rng() * 2 - 1], l2 = dot(d, d);
        if (l2 < 1e-3 || l2 > 1) continue;
        const p = add(S.centre, scale(d, S.roamRadius));
        if (dist(p, s.anchor) < S.wanderReach) continue;
        this.wanderAt = p; this.haveWander = true;
        return p.slice();
      }
      this.wanderAt = this.clampInside(sub(S.centre, sub(s.anchor, S.centre)), S.roamRadius); this.haveWander = true;
      return this.wanderAt.slice();
    }
    clampInside(p, radius) {
      const d = sub(p, this.S.centre), l = len(d);
      return l > radius && l > 1e-3 ? add(this.S.centre, scale(d, radius / l)) : p.slice();
    }
    beginFeed(s, plant) {
      this.phase = 'Feed'; this.phaseSince = this.clock;
      this.feedSince = this.clock; this.eatenAtMeal = s.eatenTotal; this.lostAtMeal = s.lost; this.eatenHere = 0;
      this.targetFood = plant.id;
      this.goal = this.clampInside(sub(plant.position, Director.inBody(s, this.mealMouth)), this.mealRoamRadius);
      this.events.push({ kind: 'feedBegan', food: plant.id, coil: this.coil });
    }
    feedTick(s, food) {
      const S = this.S;
      let k = -1;
      if (food) for (let q = 0; q < food.length; q++) if (food[q].id === this.targetFood) { k = q; break; }
      this.eatenHere = s.eatenTotal - this.eatenAtMeal;
      const sat = this.clock - this.feedSince, lostHere = s.lost - this.lostAtMeal;
      this.lostHere = lostHere;
      if (lostHere >= S.mealBreakFraction * this.form.planCount) {
        this.endMeal('Broken');
        this.fleeUntil = this.clock + S.fleeMinSeconds;
        this.threat = Math.max(this.threat, S.fleeEnter);
        this.setMood('Fleeing');
        return;
      }
      if (this.form.role === 'final' && this.progress(s) >= 1) { this.end('Completed'); return; }
      if (k < 0) { this.endMeal('Bare'); return; }
      if (this.eatenHere >= this.form.meal || this.progress(s) >= 1) { this.endMeal('Fed'); return; }
      if (s.stomachFill >= S.leaveWhenStomachFill) { this.endMeal('Full'); return; }
      if (sat > S.feedSettleSeconds + S.giveUpSeconds && s.sinceBite > S.giveUpSeconds) { this.endMeal('Bare'); return; }
      if (sat > S.maxFeedSeconds) { this.endMeal('TooLong'); return; }
      this.goal = this.clampInside(sub(food[k].position, Director.inBody(s, this.mealMouth)), this.mealRoamRadius);
    }
    endMeal(why) {
      const id = this.targetFood;
      this.phase = 'Roam'; this.phaseSince = this.clock;
      if (id >= 0) this.restUntil.set(id, this.clock + this.S.restSeconds);
      this.events.push({ kind: 'feedEnded', food: id, why });
      this.targetFood = -1;
      this.coilFor = -1;   // the next meal, even at this plant again, draws a new formation
    }
    end(outcome) {
      if (this.outcome !== 'Running') return;
      this.outcome = outcome; this.phase = 'Over';
      this.holdLaying = false; this.cruiseScale = 1; this.turnScale = 1;
      this.events.push({ kind: 'ended', form: this.formIx, outcome });
    }
  }

  // ── the closed cell's flora (TandavaHarness's plants) ─────────────────────
  function plantSite(rng, others) {
    let best = null, bd = -1;
    for (let t = 0; t < 8; t++) {
      let d;
      do d = [rng() * 2 - 1, rng() * 2 - 1, rng() * 2 - 1]; while (dot(d, d) > 1 || dot(d, d) < 1e-3);
      d = norm(d);
      const lo = ARENA.inner, hi = ARENA.outer, r = Math.cbrt(lo ** 3 + rng() * (hi ** 3 - lo ** 3));
      const p = scale(d, r);
      let near = Infinity;
      for (const o of others) if (o.alive) near = Math.min(near, dist(o.at, p));
      if (near > bd) { bd = near; best = p; }
    }
    return best;
  }
  function layPlants(seed) {
    const rng = rngOf(seed * 131 + 9), plants = [];
    for (const f of REEF) for (let k = 0; k < f.count; k++)
      plants.push({ id: plants.length, elem: f.elem, species: f.species, prisms: f.prisms, leaf: f.leaf, at: plantSite(rng, plants),
        store: f.prisms * f.leaf, lastBite: -1e9, alive: true, replantAt: 0, born: 0, spin: rng() * 6.283 });
    return { plants, rng };
  }

  // ── the pilots ─────────────────────────────────────────────────────────────
  function makePilot(id, name, at, ai) {
    return { id, name, ai, at: at.slice(), at0: at.slice(), vel: [-PILOT.cruise * 0.3, 0, 0], dir: [-1, 0, 0], speed: 0,
      cmd: { kind: 'hold' }, stunUntil: -1, kills: 0, rings: 0, stings: 0, cuts: 0, trail: [], side: 0, held: 0 };
  }

  // ── the halo (TandavaController.PlaceHalo / UpdateGuards / TandavaHaloRing.CrossedMouth) ──────────────
  /** Twelve rings round the dance ground in the body's (up, side) plane, each facing along the halo's tangent, with
      its guard post just outside it. */
  function makeHalo(swarm, dancePoint, ringDef, now) {
    const ring = ringDef || { centre: [0, 0, 0] };
    const centre = add(dancePoint, swarm.inBody(ring.centre)), up = swarm.BY.slice(), side = swarm.BZ.slice(), rings = [];
    for (let k = 0; k < HALO.count; k++) {
      const th = 2 * Math.PI * k / HALO.count, c = Math.cos(th), s = Math.sin(th);
      const dirK = [up[0] * c + side[0] * s, up[1] * c + side[1] * s, up[2] * c + side[2] * s];
      const tan = [-up[0] * s + side[0] * c, -up[1] * s + side[1] * c, -up[2] * s + side[2] * c];
      rings.push({ k, pos: add(centre, scale(dirK, HALO.radius)), post: add(centre, scale(dirK, HALO.post)), axis: tan, dir: dirK,
        guarded: false, out: false, outAt: -1, bornAt: now || 0 });
    }
    return { centre, up, side, rings, at: now || 0 };
  }
  /** A ring is GUARDED while six or more attendants (Time members) are within 40 u of its post. */
  function guardHalo(halo, swarm) {
    const w = [0, 0, 0], r2 = HALO.guardR * HALO.guardR, counts = new Array(HALO.count).fill(0);
    for (let i = 0; i < swarm.cap; i++) {
      if (!swarm.isLive(i) || swarm.elemTo[i] !== HALO.guardElem) continue;
      swarm.memberWorld(i, w);
      for (const r of halo.rings) { if (r.out) continue; const dx = w[0] - r.post[0], dy = w[1] - r.post[1], dz = w[2] - r.post[2]; if (dx * dx + dy * dy + dz * dz <= r2) counts[r.k]++; }
    }
    for (const r of halo.rings) r.guarded = !r.out && counts[r.k] >= HALO.guardN;
  }
  /** The segment a-b crosses the ring's plane inside its mouth. */
  function crossedRing(r, a, b) {
    const da = dot(sub(a, r.pos), r.axis), db = dot(sub(b, r.pos), r.axis);
    if (da * db > 0 || Math.abs(da - db) < 1e-6) return false;
    const t = clamp(da / (da - db), 0, 1), q = [a[0] + (b[0] - a[0]) * t - r.pos[0], a[1] + (b[1] - a[1]) * t - r.pos[1], a[2] + (b[2] - a[2]) * t - r.pos[2]];
    const ax = dot(q, r.axis), lat = [q[0] - r.axis[0] * ax, q[1] - r.axis[1] * ax, q[2] - r.axis[2] * ax];
    return dot(lat, lat) <= HALO.mouth * HALO.mouth;
  }

  /** One match: TandavaHarness.Sim's world with the lab's pilots, ticked as SwarmFauna runs it. */
  class Hunt {
    constructor(bank, opts) {
      opts = opts || {};
      this.bank = bank; this.seed = opts.seed || 1;
      this.picks = opts.picks || drawVariants(this.seed);
      this.forms = buildForms(bank, this.picks);
      this.swarm = new Swarm(bank, this.seed);
      this.swarm.seed(this.forms[0].planIx, ARENA.hatch, [1, 0, 0]);
      if (opts.yolk) this.swarm.stomach[1] = opts.yolk;
      this.settings = Object.assign({}, SETTINGS, opts.settings || {});
      this.director = new Director(this.forms, this.settings, this.seed);
      const lp = layPlants(this.seed); this.plants = lp.plants; this.plantRng = lp.rng; this.nextPlantId = 1000;
      this.aiSpeed = opts.aiSpeed === undefined ? PILOT.aiSpeeds.autopilot : opts.aiSpeed;
      this.pilots = [makePilot(0, 'You', [ARENA.pilotX, 0, 0], false)];
      const nAi = opts.ai === undefined ? 2 : opts.ai;
      for (let k = 0; k < nAi; k++) {
        const p = makePilot(k + 1, 'Wing ' + (k + 1), [ARENA.pilotX, 0, (k % 2 ? 1 : -1) * 60 * (1 + (k >> 1))], true);
        p.side = (k % 3 === 0 ? -1 : k % 3 === 1 ? 1 : 0) * PILOT.aiSide;
        this.pilots.push(p);
      }
      this.now = 0; this.sinceBite = Infinity; this.sinceFed = 0; this.eaten = 0; this.lastShed = -1e9; this.biteCursor = 0;
      this.lastForm = 0; this.started = false; this.steps = 0; this.halo = null; this.events = []; this.food = [];
      this.meals = 0; this.mealsBroken = 0; this.commits = []; this.lastTargetFood = -1;
    }
    get you() { return this.pilots[0]; }
    state() {
      const sw = this.swarm, st = sw.stomach, fill = sw.fill;
      return { alive: sw.liveCount(), lost: sw.lost, anchor: sw.anchor.slice(), forward: sw.BX.slice(), up: sw.BY.slice(), side: sw.BZ.slice(),
        stomach: st.slice(), stomachFill: fill, sinceBite: this.sinceBite, eatenTotal: this.eaten,
        starving: this.sinceFed >= META.starve && fill < META.forageBelow };
    }
    plantById(id) { return this.plants.find(p => p.id === id && p.alive) || null; }
    start() { if (this.started) return; this.started = true; this.events.push({ kind: 'start' }); }
    command(cmd) { this.you.cmd = cmd; this.you.cmdAt = this.now; }

    tick() {
      const sw = this.swarm, d = this.director;
      this.steps++;
      if (!this.started) {   // waiting at the hatch: it idles where it hatched; the pilots hold their line
        for (const p of this.pilots) { p.at0 = p.at.slice(); }
        sw.setLevers(1, 1, false); sw.step(sw.anchor.slice());
        return;
      }
      if (d.outcome !== 'Running') {
        for (const p of this.pilots) { p.at0 = p.at.slice(); p.vel = scale(p.vel, 0.92); p.at = add(p.at, scale(p.vel, DT)); }
        sw.setLevers(1, 1, d.outcome !== 'Completed');
        sw.step(d.outcome === 'Completed' ? add(sw.anchor, scale(sw.BX, 60)) : sw.anchor.slice());
        return;
      }
      this.now += DT;
      // feed: BitersPerStep members asked round-robin, each biting a plant it touches
      let bit = false;
      if (sw.fill < 1) {
        const reach = (FOOD.plantR + FOOD.biteR) ** 2, w = [0, 0, 0];
        for (let b = 0, tries = 0; b < META.biters && tries < sw.cap; tries++) {
          this.biteCursor = (this.biteCursor + 1) % sw.cap;
          const i = this.biteCursor;
          if (!sw.isLive(i)) continue;
          b++;
          sw.memberWorld(i, w);
          for (const p of this.plants) {
            if (!p.alive || p.store < p.leaf) continue;
            const dx = w[0] - p.at[0], dy = w[1] - p.at[1], dz = w[2] - p.at[2];
            if (dx * dx + dy * dy + dz * dz > reach) continue;
            p.store -= p.leaf; p.lastBite = this.now;
            sw.stomach[p.elem] += p.leaf; this.eaten += p.leaf; bit = true;
            break;
          }
        }
      }
      this.sinceBite = bit ? 0 : this.sinceBite + DT;
      this.sinceFed = bit || sw.fill >= 1 ? 0 : this.sinceFed + DT;
      // the plants: regrow when left alone, die eaten down, replant elsewhere later
      for (const p of this.plants) {
        if (!p.alive) {
          if (this.now >= p.replantAt) {
            p.alive = true; p.at = plantSite(this.plantRng, this.plants); p.store = 6 * p.leaf; p.lastBite = -1e9;
            p.id = this.nextPlantId++; p.born = this.now; this.events.push({ kind: 'replanted', plant: p });
          }
          continue;
        }
        if (this.now - p.lastBite > FOOD.pause) p.store = Math.min(p.prisms * p.leaf, p.store + FOOD.regrow * p.leaf * DT);
        if (p.store <= FOOD.diesAt * p.leaf) { p.alive = false; p.replantAt = this.now + FOOD.replant; p.diedAt = this.now; this.events.push({ kind: 'plantDied', plant: p }); }
      }
      // the metabolism: unfed and hungry, it sheds a member every interval
      let st = this.state();
      if (st.starving && this.now - this.lastShed >= META.shed) {
        this.lastShed = this.now;
        const v = sw.starvationVictim();
        if (v >= 0) sw.kill(v, -1);
      }
      // the pilots act (move, cut, sting, thread)
      for (const p of this.pilots) this.flyPilot(p);
      // what the director is told: every plant it can eat (outside the nucleus), every pilot; surveyed twice a second
      if (this.steps % FOOD.survey === 0 || !this.food.length) {
        this.food = this.plants.filter(p => p.alive && len(p.at) > ARENA.nucleus).map(p => ({ id: p.id, position: p.at, volume: p.store }));
      } else for (const f of this.food) { const p = this.plantById(f.id); f.volume = p ? p.store : 0; }
      const sensed = this.pilots.filter(p => !p.absent && (!p.ai || this.aiSpeed > 0)).map(p => ({ position: p.at, velocity: p.vel }));
      st = this.state();
      d.tick(DT, st, this.food, sensed);
      for (const e of d.events) {
        this.events.push(e);
        if (e.kind === 'form') this.commits.push({ t: this.now, form: e.to });
        if (e.kind === 'feedBegan') this.meals++;
        if (e.kind === 'feedEnded' && e.why === 'Broken') this.mealsBroken++;
        if (e.kind === 'danceBegan') this.placeHalo();
        if (e.kind === 'haloBroken' && this.halo) { this.halo.rings[e.ring].out = true; this.halo.rings[e.ring].outAt = this.now; }
        if (e.kind === 'ended') this.onEnded(e);
      }
      d.events.length = 0;
      if (this.halo && !d.inDance) { this.events.push({ kind: 'haloStruck' }); this.halo = null; }
      if (d.outcome !== 'Running') return;
      // the plan the director wants: a new FORM is a new body (the lay ease restarts); the same form's other pose is a re-sort
      if (sw.planIx !== d.wantPlan) { if (d.formIx !== this.lastForm) sw.requestForm(d.wantPlan); else sw.requestPose(d.wantPlan); }
      this.lastForm = d.formIx;
      sw.setLevers(d.cruiseScale, d.turnScale, d.holdLaying);
      sw.step(d.goal);
      if (this.halo && this.steps % 5 === 0) this.updateGuards();
    }
    onEnded(e) {
      // the broken dance: the figure falls back into the serpent it rose from (a molt, never a kill)
      if (e.outcome === 'DanceBroken') this.swarm.requestForm(this.forms[1].planIx);
    }

    placeHalo() { this.halo = makeHalo(this.swarm, this.director.dancePoint, this.director.form.ring, this.now); guardHalo(this.halo, this.swarm); }
    updateGuards() { guardHalo(this.halo, this.swarm); }
    nearestOpenRing(from) {
      if (!this.halo) return null;
      let best = null, bd = Infinity;
      for (const r of this.halo.rings) { if (r.out || r.guarded) continue; const dd = dist(r.pos, from); if (dd < bd) { bd = dd; best = r; } }
      return best;
    }

    // ── a pilot: where it is going (its command, or the controller's chaser for an AI), and what it hits on the way
    pilotTarget(p) {
      const sw = this.swarm, d = this.director, at = sw.anchor;
      if (p.ai) {
        if (d.inDance && this.halo) {
          const ring = this.nearestOpenRing(p.at);
          if (ring) { const from = Math.sign(dot(sub(p.at, ring.pos), ring.axis)) || 1; return { at: sub(ring.pos, scale(ring.axis, from * 30)), speed: this.aiSpeed }; }
        }
        if (d.phase === 'Feed') return { at: add(sub(at, scale(sw.BX, PILOT.aiFlank)), scale(sw.BZ, p.side)), speed: this.aiSpeed };
        const ahead = sub(d.goal, at), dir = dot(ahead, ahead) > 1 ? norm(ahead) : sw.BX.slice();
        let across = cross(dir, [0, 1, 0]); if (dot(across, across) < 1e-4) across = [0, 0, 1];
        return { at: add(add(at, scale(dir, 40)), scale(norm(across), p.side)), speed: this.aiSpeed };
      }
      const c = p.cmd;
      switch (c.kind) {
        case 'strike': return { at: c.at, speed: PILOT.strike, through: true };
        case 'goto': return { at: c.at, speed: PILOT.cruise };
        case 'camp': {   // through the plant (cutting what the dart touches), then sit beside it
          const pl = this.plantById(c.plant);
          if (!pl) { p.cmd = { kind: 'hold' }; return null; }
          if (!c.through) { if (dist(p.at, pl.at) < 16) c.through = true; else return { at: pl.at, speed: PILOT.cruise }; }
          const toward = len(pl.at) > 1 ? norm(scale(pl.at, -1)) : [1, 0, 0];
          return { at: add(pl.at, scale(toward, PILOT.hover)), speed: PILOT.cruise, hover: true };
        }
        case 'thread': {
          const r = this.halo && this.halo.rings[c.ring];
          if (!r || r.out || !d.inDance) { p.cmd = { kind: 'hold' }; return null; }
          const ax = dot(sub(p.at, r.pos), r.axis);
          if (c.from === undefined) c.from = Math.sign(ax) || 1;
          if (Math.sign(ax) === -c.from && Math.abs(ax) > 30) { p.cmd = { kind: 'hold' }; return null; }   // through and clear
          // line up on the ring's axis first, then fly through its mouth
          const lineup = add(r.pos, scale(r.axis, c.from * 75));
          if (!c.lined) { if (dist(p.at, lineup) < 28 || this.now - (c.t0 || (c.t0 = this.now)) > 4) c.lined = true; else return { at: lineup, speed: PILOT.cruise }; }
          return { at: sub(r.pos, scale(r.axis, c.from * 90)), speed: PILOT.strike };
        }
        case 'shadow': {
          const back = norm(sub(p.at, at)), w = dot(back, back) > 0.5 ? back : scale(sw.BX, -1);
          return { at: add(at, scale(w, PILOT.shadow)), speed: PILOT.strike, hover: true };
        }
        case 'body': {   // one strike run at the body: behind its centre while it feeds (clear of the guards), else its flank
          const flank = d.phase === 'Feed' ? sub(at, scale(sw.BX, PILOT.aiFlank)) : add(at, scale(sw.BZ, 0));
          p.cmd = { kind: 'strike', at: flank }; c.kind = 'strike';
          return { at: flank, speed: PILOT.strike, through: true };
        }
        case 'meals': {   // every time it settles to eat, a strike run behind its centre; otherwise ride off its flank
          if (d.phase === 'Feed') {
            const jig = ((this.steps >> 4) % 3 - 1) * 18;
            return { at: add(sub(at, scale(sw.BX, PILOT.aiFlank)), scale(sw.BZ, jig)), speed: PILOT.strike, through: true };
          }
          const back = norm(sub(p.at, at)), w = dot(back, back) > 0.5 ? back : scale(sw.BZ, 1);
          return { at: add(at, scale(w, PILOT.shadow)), speed: PILOT.cruise, hover: true };
        }
        default: return null;
      }
    }
    flyPilot(p) {
      p.at0 = p.at.slice();
      if ((p.ai && this.aiSpeed <= 0) || p.absent) { p.vel = [0, 0, 0]; return; }
      const tgt = this.pilotTarget(p);
      let want = [0, 0, 0], speed = 0;
      if (tgt) {
        const to = sub(tgt.at, p.at), dd = len(to);
        speed = tgt.speed;
        if (tgt.hover) speed = Math.min(speed, Math.max(0, dd * 1.6));
        if (tgt.through && dd < 14) {   // a strike run: carry on through the body, then back to the standing order
          p.cmd = { kind: 'coast', until: this.now + PILOT.overshoot, dir: norm(p.vel), resume: p.cmd.kind === 'strike' ? null : p.cmd };
        }
        if (!p.ai && p.cmd.kind === 'goto' && dd < 18) p.cmd = { kind: 'hold' };
        want = dd > 1e-3 ? scale(to, 1 / dd) : p.dir.slice();
      }
      if (p.cmd.kind === 'coast') {
        want = p.cmd.dir || p.dir; speed = PILOT.strike;
        if (this.now >= p.cmd.until) p.cmd = p.cmd.resume || { kind: 'hold' };
      }
      if (!tgt && p.cmd.kind === 'hold') { want = p.dir.slice(); speed = 0; }
      const stunned = this.now < p.stunUntil;
      if (stunned) speed *= PILOT.stingSlow;
      // turn the heading toward the want at the hull's turn rate, ease the speed
      const ang = Math.acos(clamp(dot(p.dir, want), -1, 1)), maxT = Math.min(PILOT.turn, PILOT.latAccel / Math.max(40, p.speed)) * DT;   // a hull turns harder slow than fast
      if (dot(want, want) > 0.5) {
        if (ang > maxT) { let ax = cross(p.dir, want); if (len(ax) < 1e-5) ax = [0, 1, 0]; p.dir = norm(rotate(p.dir, norm(ax), maxT)); }
        else p.dir = want;
      }
      const dv = clamp(speed - p.speed, -PILOT.accel * DT * 1.5, PILOT.accel * DT);
      p.speed = Math.max(0, p.speed + dv);
      p.vel = scale(p.dir, p.speed);
      p.at = add(p.at, scale(p.vel, DT));
      const r = len(p.at), lim = ARENA.membrane - 30;
      if (r > lim) { const n = scale(p.at, 1 / r); p.at = scale(n, lim); const vr = dot(p.dir, n); if (vr > 0) p.dir = norm(sub(p.dir, scale(n, 2 * vr))); }
      p.trail.push(p.at.slice()); if (p.trail.length > 36) p.trail.shift();
      this.strikeAlong(p, p.at0, p.at);
    }
    /** What a pilot's path touches this tick: members (cut), danger plates (stung, slowed), plants (prisms cut), rings. */
    strikeAlong(p, a, b) {
      const sw = this.swarm, w = [0, 0, 0], kr2 = PILOT.killR * PILOT.killR, sr2 = PILOT.stingR * PILOT.stingR;
      if (dist(a, b) < 1e-3) return;
      let cuts = 0, stung = false;
      const near = segDist2(sw.anchor, a, b) < (sw.plan.radius * UNIT * 2.6 + 40) ** 2;
      if (near) for (let i = 0; i < sw.cap; i++) {
        if (!sw.isLive(i)) continue;
        sw.memberWorld(i, w);
        const d2 = segDist2(w, a, b);
        if (sw.isDanger(i) && d2 <= sr2) { stung = true; if (sw.kill(i, p.id)) { cuts++; } continue; }
        if (d2 <= kr2 && sw.kill(i, p.id)) cuts++;
      }
      if (cuts) { p.kills += cuts; this.events.push({ kind: 'cut', pilot: p.id, n: cuts, at: b.slice() }); }
      if (stung && this.now >= p.stunUntil) { p.stunUntil = this.now + PILOT.stingSeconds; p.stings++; this.events.push({ kind: 'stung', pilot: p.id, at: b.slice() }); }
      for (const pl of this.plants) {
        if (!pl.alive || segDist2(pl.at, a, b) > (FOOD.plantR + 4) ** 2) continue;
        const k = Math.min(10, Math.floor(pl.store / pl.leaf));
        if (k <= 0) continue;
        pl.store -= k * pl.leaf; pl.lastBite = this.now;
        this.events.push({ kind: 'plantCut', pilot: p.id, plant: pl.id, n: k, at: pl.at.slice() });
      }
      if (this.halo && this.director.inDance)
        for (const r of this.halo.rings) {
          if (r.out || !crossedRing(r, a, b)) continue;
          if (r.guarded) { if (this.now - (p.heldAt || -9) > 0.6) { p.heldAt = this.now; this.events.push({ kind: 'ringHeld', pilot: p.id, ring: r.k }); } continue; }
          if (this.director.breakHalo(r.k)) { p.rings++; r.out = true; r.outAt = this.now; this.events.push({ kind: 'threaded', pilot: p.id, ring: r.k }); }
          break;
        }
    }
    score(p) { return p.kills + 25 * p.rings; }
  }

  G.TandavaSim = { UNIT, DT, DENSITY, KW, EGG, STOMACH, CORE, META, FOOD, ARENA, REEF, HALO, PILOT, SETTINGS, KEYS, COILS, COIL_NAMES, LUNGES, FORMS, PILOTS_WIN,
    rngOf, upsample, buildBank, buildForms, drawVariants, Swarm, Director, Hunt, layPlants, segDist2, makeHalo, guardHalo, crossedRing,
    vec: { dot, len, sub, add, scale, dist, cross, norm, rotate } };
})(typeof window !== 'undefined' ? window : globalThis);
