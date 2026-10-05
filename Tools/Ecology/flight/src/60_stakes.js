// ===================================================================================================================
// 60_stakes.js - STAKES (gate e): the elemental economy's petals on the player. Docs/ELEMENTAL_ECONOMY.md section 4
// and VesselElementalDebuffByDangerPrismEffectSO are the authority; this file only mirrors them.
//
//   - A pilot holds four elements, each an integer number of PETALS in the base band [0, 10] (one petal = 0.1 level).
//   - A creature's strike is a contact with its DANGER prisms (fangs, rods, a trap's jaws). The creature belongs to
//     the cell's CONTROLLING domain (fauna spawn in it), so:
//       hostile cell (you do not hold it)  -> BURN: petals are destroyed from every element, permanently;
//       your cell                          -> a temporary debuff of the same size, decaying over 4 s.
//     Not a gate: both branches run on every contact; the domain only picks how the loss lands.
//   - A loss accrues against a pending pool and settles in WHOLE petals; you cannot lose what you do not hold.
//   - One cooldown per vessel across all danger contacts (anti-spam), as the game's effect has.
//   - Every lifeform drops one elemental crystal. Flying through it pays one petal back. That is the only gain.
//   - A thief's steal takes your WAKE (conserved mass), not petals: it is a loss of territory, not of levels.
// ===================================================================================================================
const ELEMENTS = ['mass', 'charge', 'space', 'time'];
const ELEMENT_COLOUR = { mass: [0.98, 0.42, 0.30], charge: [1.0, 0.84, 0.25], space: [0.42, 0.62, 1.0], time: [0.62, 1.0, 0.55] };
/** burn = normalized level burned per element per contact (0.1 = one petal); own = the temporary debuff's size. */
const STAKES_RULES = {
  // VesselElementalDebuffByDangerPrismEffect.asset as shipped: debuffMagnitude -0.5, debuffDuration 4, cooldown 1
  shipped: { label: 'game as shipped (5 petals x 4 per contact)', burn: 0.5, cooldown: 1.0, ownDuration: 4 },
  // the recommendation measured by stakes_eval.js (see DISCOVERIES "Stakes")
  tuned: { label: 'recommended (1 petal x 4 per contact)', burn: 0.1, cooldown: 1.0, ownDuration: 4 },
};
/** which strike kinds are danger-prism contacts, and their weight. A mobber's peck is a nibble, a quarter of a bite. */
const DANGER_KINDS = { bite: 1, burn: 1, snap: 1, sting: 1, drain: 0.25 };
/** the crystal a lifeform drops (one per lifeform, CLAUDE.md). Assigned so every element can be re-earned in the cell. */
const SPECIES_ELEMENT = { grazer: 'mass', stampede: 'mass', fortress: 'mass', locust: 'time', mobber: 'time',
  pack: 'charge', thief: 'charge', lurker: 'space', leviathan: 'space', snaptrap: 'space', siege: 'time' };
const PETAL = 0.1, LEVEL_MAX = 10;

function Stakes(opt) {
  opt = opt || {};
  this.rule = STAKES_RULES[opt.rule || 'tuned'] || STAKES_RULES.tuned;
  this.hostile = opt.hostile !== false;           // fauna belong to a cell you do not control
  const start = opt.start === undefined ? 5 : opt.start;
  this.base = {}; this.pending = {}; this.transient = {};
  for (const e of ELEMENTS) { this.base[e] = start; this.pending[e] = 0; this.transient[e] = []; }
  this.startTotal = start * ELEMENTS.length;
  this.lastContact = -1e9;
  this.burned = 0; this.gained = 0; this.contacts = 0; this.blocked = 0; this.telegraphedBurns = 0;
  this.bySpecies = {}; this.events = []; this.strippedAt = null;
}
Stakes.prototype.total = function () { let s = 0; for (const e of ELEMENTS) s += this.base[e]; return s; };
/** the level the HUD flower shows: base minus any live temporary debuff (never below 0 for display). */
Stakes.prototype.effective = function (e, t) {
  let d = 0; for (const [amt, t0] of this.transient[e]) { const x = (t - t0) / this.rule.ownDuration; if (x < 1) d += amt * (1 - x); }
  return Math.max(0, this.base[e] - d);
};
/** a danger contact at time t. src = species key, telegraphed = the strike was read in advance (lead >= 0.25 s).
 *  Returns null for a non-danger kind, else { petals: {element: n}, total, form: 'burn'|'sting'|'cooldown'|'empty' }. */
Stakes.prototype.contact = function (t, kind, src, telegraphed) {
  const w = DANGER_KINDS[kind]; if (!w) return null;
  this.contacts++;
  if (t - this.lastContact < this.rule.cooldown) { this.blocked++; return { petals: {}, total: 0, form: 'cooldown' }; }
  this.lastContact = t;
  const amt = this.rule.burn * w, out = {}; let tot = 0;
  if (!this.hostile) {
    for (const e of ELEMENTS) { this.transient[e].push([amt / PETAL, t]); this.transient[e] = this.transient[e].filter(q => t - q[1] < this.rule.ownDuration); }
    const ev = { t, kind, src, form: 'sting', petals: {}, total: 0, telegraphed: !!telegraphed }; this.events.push(ev);
    return ev;
  }
  for (const e of ELEMENTS) {
    const takeable = this.base[e] * PETAL;
    if (takeable < PETAL - 1e-9) { this.pending[e] = 0; continue; }     // nothing left to give: drop the remainder
    this.pending[e] += Math.min(amt, takeable);
    let n = Math.floor((this.pending[e] + 1e-4) / PETAL);
    n = Math.min(n, this.base[e]);
    if (n > 0) { this.pending[e] = Math.max(0, this.pending[e] - n * PETAL); this.base[e] -= n; out[e] = n; tot += n; }
  }
  this.burned += tot;
  if (tot && telegraphed) this.telegraphedBurns += tot;
  if (src) this.bySpecies[src] = (this.bySpecies[src] || 0) + tot;
  if (this.strippedAt === null && this.total() === 0) this.strippedAt = t;
  const ev = { t, kind, src, form: tot ? 'burn' : 'empty', petals: out, total: tot, telegraphed: !!telegraphed };
  this.events.push(ev);
  return ev;
};
/** a crystal picked up: one petal of its element, inside the base band. Returns petals actually gained (0 if full). */
Stakes.prototype.collect = function (e, t) {
  if (!(e in this.base) || this.base[e] >= LEVEL_MAX) return 0;
  this.base[e]++; this.gained++;
  this.events.push({ t, kind: 'crystal', element: e, form: 'gain', total: 1 });
  return 1;
};
Stakes.prototype.summary = function (minutes) {
  return { rule: this.rule.burn, hostile: this.hostile, start: this.startTotal, end: this.total(), burned: this.burned,
    gained: this.gained, burned_per_min: +(this.burned / minutes).toFixed(2), contacts: this.contacts, blocked: this.blocked,
    telegraphed_share: this.burned ? +(this.telegraphedBurns / this.burned).toFixed(2) : null,
    stripped_at: this.strippedAt === null ? null : +this.strippedAt.toFixed(1), by_species: this.bySpecies };
};

/** TELEGRAPH TRACKER: per species, when did its agent nearest the player last start showing intent > 0.5?
 *  A strike from that species counts as telegraphed when the intent had been showing for >= 0.25 s (the bestiary's
 *  first-strike rule). Cheap enough for every step: one nearest-agent scan per species. */
function TeleTrack() { this.on = {}; this.view = {}; }
TeleTrack.prototype.observe = function (t, player, species) {
  for (const sp of species) {
    const key = sp.key || sp.name;
    if (!sp.view) continue;
    const v = sp.view(this.view[key] || (this.view[key] = {}));
    const m = v.m, P = v.P, I = v.I; if (!m || !I) { delete this.on[key]; continue; }
    let bd = Infinity, bj = 0;
    for (let j = 0; j < m; j++) { const d = (P[3 * j] - player.pos[0]) ** 2 + (P[3 * j + 1] - player.pos[1]) ** 2 + (P[3 * j + 2] - player.pos[2]) ** 2; if (d < bd) { bd = d; bj = j; } }
    if (I[bj] > 0.5) { if (!(key in this.on)) this.on[key] = t; }
    else if (I[bj] < 0.2) delete this.on[key];
  }
};
TeleTrack.prototype.lead = function (t, key) { return key in this.on ? t - this.on[key] : 0; };
