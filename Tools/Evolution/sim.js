// Darwin Lab - the evolution model, ported EXACTLY from the shipped C# (Docs/EVOLUTION.md §7).
//
//   core:  Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/{LifeformGenome,GenomeExpression,
//          GenomeMutation,EvolutionSettings}.cs
//   arena: Tools/Build/evolution_harness/EvolutionArena.cs
//
// "Exactly" is held by Tools/Evolution/gate_parity.cjs against Tools/Evolution/results/golden.json: the same seed
// must give the same trajectory, draw for draw, bit for bit (===). The rules that make that possible:
//   - the C# core stores GENES and the PHENOTYPE as float32 and computes in double: every place C# rounds to float,
//     this file calls Math.fround; every C# float SETTING is fround'ed on the way in (a float 0.08 is not 0.08);
//   - no transcendental function anywhere in the step (the multiplier map is rational; the normal is Irwin-Hall);
//   - uint32 arithmetic through Math.imul and >>> 0; one draw order; stable compaction.
// Any change to the C# must land here too, and the gate says when it has not.
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.Evo = factory();
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';
  const f32 = Math.fround;
  const LOCI = ['tempo', 'reach', 'fecundity', 'cohesion'];
  const LOCUS_COUNT = 4;

  // ------------------------------------------------------------------------------------ GenomeRng (xoshiro128**)
  function splitmix(st) {
    st.x = (st.x + 0x9E3779B9) >>> 0;
    let z = st.x;
    z = Math.imul(z ^ (z >>> 16), 0x85EBCA6B) >>> 0;
    z = Math.imul(z ^ (z >>> 13), 0xC2B2AE35) >>> 0;
    return (z ^ (z >>> 16)) >>> 0;
  }
  function rotl(x, k) { return ((x << k) | (x >>> (32 - k))) >>> 0; }

  class GenomeRng {
    constructor(seed) { this.draws = 0; this.reseed(seed); }
    reseed(seed) {
      const st = { x: seed >>> 0 };
      this.s0 = splitmix(st); this.s1 = splitmix(st); this.s2 = splitmix(st); this.s3 = splitmix(st);
      if (((this.s0 | this.s1 | this.s2 | this.s3) >>> 0) === 0) this.s0 = 0x9E3779B9;
    }
    nextU32() {
      const result = Math.imul(rotl(Math.imul(this.s1, 5) >>> 0, 7), 9) >>> 0;
      const t = (this.s1 << 9) >>> 0;
      this.s2 = (this.s2 ^ this.s0) >>> 0;
      this.s3 = (this.s3 ^ this.s1) >>> 0;
      this.s1 = (this.s1 ^ this.s2) >>> 0;
      this.s0 = (this.s0 ^ this.s3) >>> 0;
      this.s2 = (this.s2 ^ t) >>> 0;
      this.s3 = rotl(this.s3, 11);
      return result;
    }
    nextUnit() { this.draws++; return (this.nextU32() >>> 8) * (1.0 / 16777216.0); }
  }

  // ------------------------------------------------------------------------------------ EvolutionSettings
  // Every numeric field is a C# float: stored fround'ed.
  function settings(over) {
    const s = {
      Enabled: true,
      MutationRate: 1, MutationSigma: 0.08, FounderSpread: 0.1,
      TempoPaceRange: 1.5, TempoUpkeepRange: 1.84, ReachRadiusRange: 1.5, ReachUpkeepRange: 1.36,
      FecundityRange: 2, FecundityProvisionRange: 2, CohesionRange: 1.6,
      SnapshotIntervalSeconds: 30, MaxSnapshotsPerSpecies: 2048,
    };
    if (over) for (const k of Object.keys(over)) if (k in s) s[k] = over[k];
    for (const k of Object.keys(s)) if (typeof s[k] === 'number') s[k] = f32(s[k]);
    return s;
  }
  function withoutMutation(s) { const c = Object.assign({}, s); c.MutationRate = 0; c.MutationSigma = 0; return c; }
  function safeRange(r) { return (r !== r || r < 1) ? 1 : r; }   // NaN or below 1 reads as 1

  // ------------------------------------------------------------------------------------ LifeformGenome
  function clampGene(g) { if (g !== g) return 0; return g < -1 ? -1 : g > 1 ? 1 : g; }
  function genome(t, r, f, c) { return [f32(clampGene(f32(t))), f32(clampGene(f32(r))), f32(clampGene(f32(f))), f32(clampGene(f32(c)))]; }
  const FOUNDER = genome(0, 0, 0, 0);
  function isFounder(g) { return g[0] === 0 && g[1] === 0 && g[2] === 0 && g[3] === 0; }
  function quantize(g) { let q = Math.round(clampGene(g) * 127); if (q > 127) q = 127; if (q < -127) q = -127; return q; }
  function dequantize(q) { return f32(clampGene(f32(q / 127))); }
  function pack(g) {
    const b = l => (quantize(g[l]) & 0xFF) >>> 0;
    return (b(0) | (b(1) << 8) | (b(2) << 16) | (b(3) << 24)) >>> 0;
  }
  function unpack(u) {
    const sb = x => (x & 0x80) ? x - 256 : x;
    return [dequantize(sb(u & 0xFF)), dequantize(sb((u >>> 8) & 0xFF)), dequantize(sb((u >>> 16) & 0xFF)), dequantize(sb((u >>> 24) & 0xFF))];
  }
  function distance(a, b) { let s = 0; for (let l = 0; l < 4; l++) { const d = a[l] - b[l]; s += d * d; } return f32(Math.sqrt(s)); }

  // ------------------------------------------------------------------------------------ GenomeExpression
  function multiplier(gene, range) {
    const r = safeRange(range);           // float, widened
    const g = clampGene(gene);            // float, widened
    const k = r - 1.0;
    const f = g >= 0.0 ? 1.0 + k * g : 1.0 / (1.0 + k * (-g));
    return f32(f);
  }
  const NEUTRAL = { pace: 1, reach: 1, upkeep: 1, fecundity: 1, provision: 1, cohesion: 1 };
  const MIN_PROVISION = f32(0.1);
  function express(g, s) {
    if (!s || !s.Enabled) return NEUTRAL;
    const pace = multiplier(g[0], s.TempoPaceRange);
    const reach = multiplier(g[1], s.ReachRadiusRange);
    const upkeep = f32(multiplier(g[0], s.TempoUpkeepRange) * multiplier(g[1], s.ReachUpkeepRange));
    const fecundity = multiplier(g[2], s.FecundityRange);
    let provision = g[2] > 0 ? f32(1.0 / multiplier(g[2], s.FecundityProvisionRange)) : 1;
    if (provision < MIN_PROVISION) provision = MIN_PROVISION;
    if (provision > 1) provision = 1;
    const cohesion = multiplier(g[3], s.CohesionRange);
    return { pace, reach, upkeep, fecundity, provision, cohesion };
  }
  function roundAwayFromZero(x) { return x >= 0 ? Math.floor(x + 0.5) : -Math.floor(-x + 0.5); }
  function feedsPerOffspring(authored, p) {
    if (authored <= 0) return authored;
    const scaled = roundAwayFromZero(authored / Math.max(1e-6, p.fecundity));
    return scaled < 1 ? 1 : scaled;
  }
  function starvationSeconds(authored, p) {
    if (!(authored > 0)) return authored;
    return f32(authored / Math.max(1e-6, p.upkeep));
  }

  // ------------------------------------------------------------------------------------ GenomeMutation
  function normal(rng) { let sum = 0.0; for (let i = 0; i < 12; i++) sum += rng.nextUnit(); return sum - 6.0; }
  function mutate(parent, s, rng) {
    if (!s || !s.Enabled || !rng) return parent;
    const rate = s.MutationRate, sigma = s.MutationSigma;
    if (!(rate > 0.0) || !(sigma > 0.0)) return parent;
    const genes = parent.slice();
    for (let i = 0; i < LOCUS_COUNT; i++) {
      if (rate < 1.0 && rng.nextUnit() >= rate) continue;
      const g = genes[i] + sigma * normal(rng);
      genes[i] = clampGene(f32(g));
    }
    return genome(genes[0], genes[1], genes[2], genes[3]);
  }
  function founder(s, rng) {
    if (!s || !s.Enabled || !rng) return FOUNDER;
    const spread = s.FounderSpread;
    if (!(spread > 0.0)) return FOUNDER;
    const genes = [0, 0, 0, 0];
    for (let i = 0; i < LOCUS_COUNT; i++) genes[i] = clampGene(f32(spread * normal(rng)));
    return genome(genes[0], genes[1], genes[2], genes[3]);
  }

  // ------------------------------------------------------------------------------------ ArenaParams
  function params(over) {
    const p = {
      HerbFeedsPerOffspring: 20, HerbCooldown: 10, HerbCap: 6, HerbFloor: 4, HerbStarvation: 90,
      PredFeedsPerOffspring: 6, PredCooldown: 30, PredCap: 2, PredFloor: 1, PredStarvation: 45,
      HuntInterval: 20, PredationImmunity: 6, StomachCapacity: 16, SpawnWave: 15, ConservedStomach: false,
      FoodCap: 750, FloraGrowth: 20, GrazeRate: 1.15, HalfSaturation: 150,
      PopulationScale: 1, PredatorScale: 1, Dt: 0.5, Seeder: true, InertPhenotype: false,
      Evolution: settings(),
    };
    if (over) for (const k of Object.keys(over)) {
      if (k === 'Evolution') p.Evolution = settings(over.Evolution);
      else if (k in p) p[k] = over[k];
    }
    return p;
  }
  function scaleCount(authored, scale) {
    if (authored <= 0) return 0;
    const v = roundAwayFromZero(authored * scale);
    return v < 1 ? 1 : v;
  }

  // ------------------------------------------------------------------------------------ EvolutionArena
  class Arena {
    constructor(p, seed) {
      this.P = p;
      this.rng = new GenomeRng(seed);
      this.herbCap = scaleCount(p.HerbCap, p.PopulationScale);
      this.herbFloor = scaleCount(p.HerbFloor, p.PopulationScale);
      this.predCap = scaleCount(p.PredCap, p.PredatorScale);
      this.predFloor = scaleCount(p.PredFloor, p.PredatorScale);
      this.h = [];   // {stomach, feeds, lastBirth, born, gen, g, ph, alive}
      this.pr = [];  // {stomach, feeds, lastBirth, nextHunt, alive}
      this.food = p.FoodCap;
      this.step = 0;
      this.nextWave = 0;
      this.c = { births: 0, founders: 0, starved: 0, eaten: 0, predBirths: 0, predStarved: 0 };
      this.total = { births: 0, founders: 0, starved: 0, eaten: 0 };
      this.maxGen = 0;
      this.seedToFloor();
    }
    get t() { return this.step * this.P.Dt; }
    expressOf(g) { return this.P.InertPhenotype ? NEUTRAL : express(g, this.P.Evolution); }
    spawnHerbivore(g, stomach, gen) {
      this.h.push({ stomach, feeds: 0, lastBirth: -Infinity, born: this.t, gen, g, ph: this.expressOf(g), alive: true });
      if (gen > this.maxGen) this.maxGen = gen;
    }
    spawnPredator() { this.pr.push({ stomach: this.P.StomachCapacity, feeds: 0, lastBirth: -Infinity, nextHunt: this.t + this.P.HuntInterval, alive: true }); }
    seedToFloor() {
      let hd = this.herbFloor - this.h.length;
      if (this.herbCap > 0) hd = Math.min(hd, this.herbCap - this.h.length);
      for (let i = 0; i < hd; i++) {
        const g = founder(this.P.Evolution, this.rng);
        this.spawnHerbivore(g, this.P.StomachCapacity, 0);
        this.c.founders++; this.total.founders++;
      }
      let pd = this.predFloor - this.pr.length;
      if (this.predCap > 0) pd = Math.min(pd, this.predCap - this.pr.length);
      for (let i = 0; i < pd; i++) this.spawnPredator();
    }
    advance(n) { for (let i = 0; i < n; i++) this.stepOnce(); }
    stepOnce() {
      const P = this.P, dt = P.Dt, t = this.t;
      if (P.Seeder && t >= this.nextWave) { this.nextWave += P.SpawnWave; this.seedToFloor(); }
      if (P.FoodCap > 0) {
        const room = 1.0 - this.food / P.FoodCap;
        if (room > 0) this.food += P.FloraGrowth * dt * room;
        if (this.food > P.FoodCap) this.food = P.FoodCap;
      }
      const h = this.h, nh = h.length;
      const children = [];
      const sat = this.food > 0 ? this.food / (this.food + P.HalfSaturation) : 0.0;
      const drain = P.HerbStarvation > 0 ? P.StomachCapacity / P.HerbStarvation : 0.0;
      for (let i = 0; i < nh; i++) {
        const a = h[i];
        if (!a.alive) continue;
        const ph = a.ph;
        const rate = P.GrazeRate * ph.pace * ph.reach * ph.reach * sat;
        let pGraze = rate * dt;
        if (pGraze > 1.0) pGraze = 1.0;
        if (this.food >= 1.0 && this.rng.nextUnit() < pGraze) {
          this.food -= 1.0;
          if (P.ConservedStomach) { const s = a.stomach + P.StomachCapacity; a.stomach = s > P.StomachCapacity ? P.StomachCapacity : s; }
          else a.stomach = P.StomachCapacity;
          a.feeds = a.feeds + 1;
          const fpo = feedsPerOffspring(P.HerbFeedsPerOffspring, ph);
          const capOk = this.herbCap <= 0 || (nh + children.length) < this.herbCap;
          if (fpo > 0 && a.feeds >= fpo && (t - a.lastBirth) >= P.HerbCooldown && capOk) {
            a.lastBirth = t;
            a.feeds = 0;
            const cg = mutate(a.g, P.Evolution, this.rng);
            children.push({ g: cg, stomach: P.StomachCapacity * ph.provision, gen: a.gen + 1 });
          }
        }
        if (drain > 0) {
          a.stomach = a.stomach - drain * ph.upkeep * dt;
          if (a.stomach <= 0) { a.alive = false; this.c.starved++; this.total.starved++; }
        }
      }
      const pr = this.pr, np = pr.length;
      let predChildren = 0;
      const predDrain = P.PredStarvation > 0 ? P.StomachCapacity / P.PredStarvation : 0.0;
      for (let j = 0; j < np; j++) {
        const q = pr[j];
        if (!q.alive) continue;
        if (t >= q.nextHunt) {
          q.nextHunt = q.nextHunt + P.HuntInterval;
          if (nh > 0) {
            let target = Math.floor(this.rng.nextUnit() * nh);
            if (target >= nh) target = nh - 1;
            const prey = h[target];
            const immune = (t - prey.born) < P.PredationImmunity;
            if (prey.alive && !immune) {
              prey.alive = false;
              this.c.eaten++; this.total.eaten++;
              q.stomach = P.StomachCapacity;
              q.feeds = q.feeds + 1;
              const capOk = this.predCap <= 0 || (np + predChildren) < this.predCap;
              if (P.PredFeedsPerOffspring > 0 && q.feeds >= P.PredFeedsPerOffspring && (t - q.lastBirth) >= P.PredCooldown && capOk) {
                q.lastBirth = t; q.feeds = 0; predChildren++;
              }
            }
          }
        }
        if (predDrain > 0) {
          q.stomach = q.stomach - predDrain * dt;
          if (q.stomach <= 0) { q.alive = false; this.c.predStarved++; }
        }
      }
      // stable compaction, then the newborns
      let w = 0;
      for (let i = 0; i < h.length; i++) if (h[i].alive) h[w++] = h[i];
      h.length = w;
      w = 0;
      for (let j = 0; j < pr.length; j++) if (pr[j].alive) pr[w++] = pr[j];
      pr.length = w;
      for (let i = 0; i < children.length; i++) {
        this.spawnHerbivore(children[i].g, children[i].stomach, children[i].gen);
        this.c.births++; this.total.births++;
      }
      for (let j = 0; j < predChildren; j++) { this.spawnPredator(); this.c.predBirths++; }
      this.step++;
    }
    /** One census row as the harness writes it: [t, herb, pred, food, mean x4, sd x4, meanGen, maxGen, births, founders, starved, eaten, predBirths, predStarved, draws]. */
    census() {
      const h = this.h, n = h.length;
      const mean = [0, 0, 0, 0], sd = [0, 0, 0, 0];
      let meanGen = 0;
      if (n > 0) {
        for (let l = 0; l < 4; l++) {
          let sum = 0; for (let i = 0; i < n; i++) sum += h[i].g[l];
          const m = sum / n;
          let ss = 0; for (let i = 0; i < n; i++) { const d = h[i].g[l] - m; ss += d * d; }
          mean[l] = m; sd[l] = n > 1 ? Math.sqrt(ss / (n - 1)) : 0;
        }
        let gs = 0; for (let i = 0; i < n; i++) gs += h[i].gen;
        meanGen = gs / n;
      }
      const c = this.c;
      const row = [this.t, n, this.pr.length, this.food, mean[0], mean[1], mean[2], mean[3], sd[0], sd[1], sd[2], sd[3],
        meanGen, this.maxGen, c.births, c.founders, c.starved, c.eaten, c.predBirths, c.predStarved, this.rng.draws];
      c.births = c.founders = c.starved = c.eaten = c.predBirths = c.predStarved = 0;
      return row;
    }
    genomes() { return this.h.map(a => a.g); }
    phenotypes() { return this.h.map(a => a.ph); }
  }

  const ROW = { t: 0, herb: 1, pred: 2, food: 3, mean: 4, sd: 8, meanGen: 12, maxGen: 13, births: 14, founders: 15, starved: 16, eaten: 17, predBirths: 18, predStarved: 19, draws: 20 };

  return {
    LOCI, LOCUS_COUNT, ROW, GenomeRng, settings, withoutMutation, safeRange,
    genome, FOUNDER, isFounder, clampGene, quantize, dequantize, pack, unpack, distance,
    multiplier, express, NEUTRAL, feedsPerOffspring, starvationSeconds, normal, mutate, founder,
    params, scaleCount, Arena,
  };
});
