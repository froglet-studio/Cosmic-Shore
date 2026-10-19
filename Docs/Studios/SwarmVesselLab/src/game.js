// ============================================================================================
// game.js — economies, metas, cards, scenes, scoring, the AI commander, simStep, reset and the batch.
// ============================================================================================
const BUILD_KEY = 'lab:' + LAB.name + ':build';
const BUILD = Object.assign({ species: 'termite', control: 'focus', economy: 'pheromone', meta: 'deck', scene: 'mass', deck: null, castes: { worker: 0.6, soldier: 0.25, scout: 0.15 }, toy: 'termite-mound', ai: false }, lsGet(BUILD_KEY, {}));
function saveBuild() { lsSet(BUILD_KEY, BUILD); }
function toast(m) { if (W) W.toast = { m, t: 2.2 }; }

// ================= economies =================
function initEcon() {
  const id = W.build.economy, e = { id, pool: 0, max: 10, regen: 0, food: 0, gems: 0, tanks: null, cap: P.droneCap, hiveT: 0, label: ECONOMIES_BY_ID[id].unit };
  if (id === 'pheromone') { e.max = P.pherMax; e.pool = P.pherStart; e.regen = P.pherRegen * (1 + P.elTime / 10); }
  else if (id === 'tiered') { e.tanks = [{ v: 2, max: 4, regen: 0.8 }, { v: 2, max: 6, regen: 0.35 }, { v: 1, max: 8, regen: 0.12 }]; }
  else if (id === 'hive') { e.max = 8; e.pool = 3; e.regen = 0.18; e.cap = Math.round(P.droneCap * 1.2); }
  else if (id === 'fixed') { e.max = 8; e.pool = 4; e.regen = 0.25; }
  else if (id === 'biomass') { e.max = 12; e.pool = 3; e.cap = Math.round(P.droneCap * 2.2); }
  else if (id === 'foodweb') { e.max = 12; e.pool = 3; e.food = 6; e.cap = Math.round(P.droneCap * 2.5); }
  else if (id === 'crystal') { e.max = 12; e.pool = 2; e.cap = Math.round(P.droneCap * 1.6); }
  W.econ = e;
}
function econOnEat(a) {
  const e = W.econ;
  if (e.id === 'biomass') { e.food += 1; const need = 3 * EL.size() / EL.birth(); if (e.food >= need) { e.food -= need; addAgent(a.x, a.y, childOf(a)); } e.pool = Math.min(e.max, e.pool + 0.15); }
  else if (e.id === 'foodweb') { e.food += 1; e.pool = Math.min(e.max, e.pool + 0.15); if (e.food > 4 && W.rnd() < 0.35 * EL.birth()) { e.food -= 2.5; addAgent(a.x, a.y, childOf(a)); } }
}
function econOnPetal(n) {
  // a petal drained or stung out of a rival is food for a swarm that breeds on what it takes
  const e = W.econ; if (e.id === 'biomass' || e.id === 'foodweb') { e.food += 2 * n; while (e.food >= 3) { e.food -= 3; addAgent(W.hull.x + (W.rnd() - .5) * 20, W.hull.y + (W.rnd() - .5) * 20, childOf(null)); } }
  else if (e.pool != null) e.pool = Math.min(e.max, e.pool + n);
}
function econOnCrystal(c) {
  const e = W.econ;
  if (e.id === 'crystal') { e.gems++; e.pool = Math.min(e.max, e.pool + 2); for (let i = 0; i < 4; i++) addAgent(W.hull.x + (W.rnd() - .5) * 20, W.hull.y + (W.rnd() - .5) * 20, childOf(null)); }
  else if (e.id === 'pheromone' || e.id === 'hive' || e.id === 'fixed') e.pool = Math.min(e.max, e.pool + 1);
  else if (e.id === 'tiered') e.tanks[2].v = Math.min(e.tanks[2].max, e.tanks[2].v + 1);
  else e.pool = Math.min(e.max, e.pool + 1);
}
function stepEcon(dt) {
  const e = W.econ;
  if (e.tanks) { for (const t of e.tanks) t.v = Math.min(t.max, t.v + t.regen * dt * (1 + P.elTime / 20)); }
  else if (e.regen) e.pool = Math.min(e.max, e.pool + e.regen * dt);
  if (e.id === 'hive') {
    e.hiveT -= dt * EL.birth();
    if (e.hiveT <= 0) { e.hiveT = 2.6; const h = W.homes[W.homes.length - 1] || W.hull; addAgent(h.x + (W.rnd() - .5) * 14, h.y + (W.rnd() - .5) * 14, childOf(null)); }
  }
  if (e.id === 'foodweb') {
    // upkeep: every member eats a little; with nothing in the larder, the oldest-hungry starve (withers, never a timer)
    const live = liveAgents(); e.food -= live.length * 0.035 * dt * EL.size();
    if (e.food < 0) { e.food = 0; if (W.rnd() < dt * 0.6 * Math.min(4, live.length / 10)) { const v = live[Math.floor(W.rnd() * live.length)]; if (v) { hurtAgent(v, 99); W.stats.starved = (W.stats.starved || 0) + 1; } } }
  }
}
function canPay(card) {
  const e = W.econ;
  if (e.tanks) return e.tanks[card.tier].v >= Math.min(card.cost, e.tanks[card.tier].max);
  return e.pool >= card.cost;
}
function pay(card) { const e = W.econ; if (e.tanks) e.tanks[card.tier].v -= Math.min(card.cost, e.tanks[card.tier].max); else e.pool -= card.cost; }

// ================= metas =================
function initMeta() {
  const m = { id: W.build.meta, deck: [], hand: [], drafts: [], wheel: 0, t: 0 };
  if (m.id === 'deck') { m.deck = (W.build.deck || W.sp.deck || DEFAULT_DECK).slice(0, 8); m.hand = m.deck.slice(0, 4); m.queue = m.deck.slice(4); }
  W.meta = m;
}
// What a newborn inherits. Evolution copies a SURVIVOR (weight = age), the ecology's endogenous-selection rule.
function childOf(parent) {
  const o = { sq: W.agents.length % 2 };
  if (W.build.meta === 'castes') {
    const c = W.build.castes, r = W.rnd() * (c.worker + c.soldier + c.scout);
    o.caste = r < c.worker ? 'worker' : r < c.worker + c.soldier ? 'soldier' : 'scout';
    if (o.caste === 'soldier') o.verb = 'sting'; else if (o.caste === 'scout') o.verb = 'ping';
  }
  if (W.sp.verbs) o.verb = W.sp.verbs[(W.agents.length + W.meta.wheel) % W.sp.verbs.length];
  if (W.molt > 0) o.verb = W.sp.alt;
  if (W.build.meta === 'evolve') {
    const live = liveAgents(); let src = parent;
    if (live.length) { let tot = 0; for (const a of live) tot += 1 + a.age; let r = W.rnd() * tot; for (const a of live) { r -= 1 + a.age; if (r <= 0) { src = a; break; } } }
    const g = src ? src.gene : { spd: 1, sense: 1, bite: 1 }, mu = () => 1 + (W.rnd() - 0.5) * 0.24;
    o.gene = { spd: clamp(g.spd * mu(), 0.5, 2.2), sense: clamp(g.sense * mu(), 0.5, 2.2), bite: clamp(g.bite * mu(), 0.5, 2.2) };
    // a fast, far-seeing body is a hungry one: genes cost hit points, so survival is the only judge
    o.costHp = (o.gene.spd + o.gene.sense + o.gene.bite) / 3;
  }
  return o;
}
const DRAFT_POOL = [
  { id: 'swift', name: 'Swift', desc: '+20% member speed', apply: () => { W.mods.spd *= 1.2; } },
  { id: 'keen', name: 'Keen', desc: '+30% sense radius', apply: () => { W.mods.sense *= 1.3; } },
  { id: 'jaws', name: 'Jaws', desc: '+35% bite and sting', apply: () => { W.mods.bite *= 1.35; } },
  { id: 'clutch', name: 'Clutch', desc: '+8 members now', apply: () => { for (let i = 0; i < 8; i++) addAgent(W.hull.x + (W.rnd() - .5) * 30, W.hull.y + (W.rnd() - .5) * 30, childOf(null)); } },
  { id: 'carapace', name: 'Carapace', desc: '+60% member hit points', apply: () => { W.mods.hp *= 1.6; for (const a of liveAgents()) a.hp *= 1.6; } },
  { id: 'bloom', name: 'Bloom', desc: 'Your economy fills now and refills 40% faster', apply: () => { const e = W.econ; if (e.tanks) e.tanks.forEach((t) => { t.v = t.max; t.regen *= 1.4; }); else { e.pool = e.max; e.regen *= 1.4; } } },
  { id: 'second', name: 'Second verb', desc: 'A third of members permanently take the species\' second verb', apply: () => { let i = 0; for (const a of liveAgents()) if (i++ % 3 === 0) a.verb = W.sp.alt; } },
  { id: 'mound', name: 'Free mound', desc: 'A mound at the swarm\'s centre', apply: () => { const c = swarmCentre(); addHome(c.x, c.y, 'mound'); } },
];
function stepMeta(dt) {
  const m = W.meta; m.t += dt;
  if (m.id === 'roulette') {
    W.wheelT = (W.wheelT || 12) - dt;
    if (W.wheelT <= 0) {
      W.wheelT = 12; m.wheel++;
      const wheel = W.sp.verbs || ['graze', 'sting', 'build', 'fetch', 'ping', 'frenzy'];
      for (const a of liveAgents()) { const i = wheel.indexOf(a.verb); a.verb = wheel[(i + 1 + wheel.length) % wheel.length]; a.tgt = null; a.carry = null; }
      toast('Roulette: every verb shifts one seat');
    }
  } else if (m.id === 'merge') {
    W.mergeMetaT = (W.mergeMetaT || 8) - dt;
    if (W.mergeMetaT <= 0) {
      W.mergeMetaT = 8;
      const live = liveAgents().filter((a) => a.big < 3);
      if (live.length >= 3) {
        const a = live[0], pals = live.slice(1).sort((p, q) => len(p.x - a.x, p.y - a.y) - len(q.x - a.x, q.y - a.y)).slice(0, 2);
        for (const b of pals) { b.alive = false; a.hp += b.hp; }
        a.big += 1; a.size *= 1.45; W.stats.merges++; W.fx.push({ k: 'ring', x: a.x, y: a.y, t: 0, r: 24, col: '#f6c27f' });
      }
    }
  } else if (m.id === 'draft') {
    if (W.t >= W.nextDraft && !W.draftOffer) {
      W.nextDraft += 20; const pool = DRAFT_POOL.slice(), offer = [];
      for (let i = 0; i < 3; i++) offer.push(pool.splice(Math.floor(W.rnd() * pool.length), 1)[0]);
      W.draftOffer = offer;
      if (W.headless) pickDraft(Math.floor(W.rnd() * 3));
    }
  }
}
function pickDraft(i) { const o = W.draftOffer && W.draftOffer[i]; if (!o) return; o.apply(); W.meta.drafts.push(o.id); W.draftOffer = null; toast('Drafted: ' + o.name); }

// ================= cards =================
function cardTarget() { return W.focus || W.hull; }
function playCard(idx) {
  const m = W.meta; if (m.id !== 'deck') return false;
  const id = m.hand[idx]; if (!id) return false;
  const c = CARDS_BY_ID[id]; if (!canPay(c)) return false;
  pay(c); castCard(c, cardTarget());
  m.hand.splice(idx, 1, m.queue.shift()); m.queue.push(id);
  W.stats.plays++; return true;
}
function castCard(c, t) {
  const live = liveAgents();
  switch (c.id) {
    case 'brood': for (let i = 0; i < 6; i++) addAgent(W.hull.x + (W.rnd() - .5) * 24, W.hull.y + (W.rnd() - .5) * 24, childOf(null)); break;
    case 'rally': W.cast = true; W.castPt = { x: t.x, y: t.y }; W.castT = 4; break;
    case 'frenzy': W.buff.frenzy = 6; W.moodBoost = 6; break;
    case 'autothysis': {
      const near = live.slice().sort((p, q) => len(p.x - t.x, p.y - t.y) - len(q.x - t.x, q.y - t.y)).slice(0, 8);
      for (const a of near) {
        const rr = 22 * EL.reach();
        hashQuery(W.hash, a.x, a.y, rr, (p) => { if (hostile(p) && p.sh === 0) { killPrism(p); W.stats.eaten++; } });
        for (const r of W.rivals) if (r.alive && len(r.x - a.x, r.y - a.y) < rr * 1.5) { r.stun = Math.max(r.stun, 1.2); r.petalDebt += 0.3 * EL.dmg(); }
        W.fx.push({ k: 'boom', x: a.x, y: a.y, t: 0, r: rr }); hurtAgent(a, 99);
      }
      break;
    }
    case 'mound': addHome(t.x, t.y, 'mound'); W.fx.push({ k: 'ring', x: t.x, y: t.y, t: 0, r: 30, col: '#f0a043' }); break;
    case 'teamCrystal': addCrystal(t.x + (W.rnd() - .5) * 20, t.y + (W.rnd() - .5) * 20, { kind: 'omni' }); break;
    case 'teleport': W.fx.push({ k: 'ring', x: W.hull.x, y: W.hull.y, t: 0, r: 20, col: '#9ff7c6' }); W.hull.x = t.x; W.hull.y = t.y; W.hull.vx = W.hull.vy = 0; W.hull.roam = null; break;
    case 'scout': W.fx.push({ k: 'ping', x: t.x, y: t.y, t: 0, r: 220 }); for (const cc of W.crystals) if (cc.hidden && len(cc.x - t.x, cc.y - t.y) < 220) { cc.revealed = true; W.stats.revealed++; } for (const p of W.prisms) if (p.hidden && len(p.x - t.x, p.y - t.y) < 220) { p.hidden = false; W.stats.revealed++; } break;
    case 'decoy': addCrystal(t.x, t.y, { decoy: true }).decoyT = 8; break;
    case 'webs': for (let i = 0; i < 6; i++) { const a = i / 6 * TAU; W.webs.push({ x0: t.x, y0: t.y, x1: t.x + Math.cos(a) * 70, y1: t.y + Math.sin(a) * 70, hp: 2 }); } break;
    case 'molt': W.molt = 10; for (const a of live) { a.prevVerb = a.verb; a.verb = W.sp.alt; a.tgt = null; } break;
    case 'shield': W.buff.shield = 6; break;
  }
  W.fx.push({ k: 'card', x: t.x, y: t.y, t: 0, name: c.name });
}

// ================= scenes =================
function setupScene(id) {
  const rnd = W.rnd, RUBY = DOM.RUBY, GOLD = DOM.GOLD, N = DOM.NEUTRAL;
  const at = (a, r) => ({ x: Math.cos(a) * r, y: Math.sin(a) * r });
  W.fortX = 0; W.fortY = 0;
  if (id === 'time') {
    W.gateCount = 8; W.gateNext = 0;
    for (let i = 0; i < 8; i++) { const a = i / 8 * TAU + 0.3, r = 300 + (i % 2) * 140; const p = at(a, r); addCrystal(p.x, p.y, { order: i, kind: 'gate' }); }
    for (let i = 0; i < 8; i += 2) { const a = (i + 0.5) / 8 * TAU + 0.3; const p = at(a, 370); const q = at(a + 0.12, 370); line(p.x, p.y, q.x, q.y, 6, RUBY); }
    for (let i = 0; i < 5; i++) { const p = at(rnd() * TAU, 180 + rnd() * 300); clump(p.x, p.y, 18, 30, N); }
  } else if (id === 'space') {
    W.fortX = 230; W.fortY = -60; ring(W.fortX, W.fortY, 50, 14, 7, RUBY, { shieldFrac: 0.45 }); clump(W.fortX, W.fortY, 40, 40, RUBY);
    for (let i = 0; i < 6; i++) { const p = at(i / 6 * TAU + rnd(), 260 + rnd() * 220); clump(p.x, p.y, 55, 42, i % 2 ? RUBY : GOLD); }
    addRival(W.fortX + 100, W.fortY, RUBY, 'guard');
  } else if (id === 'mass') {
    for (let i = 0; i < 9; i++) { const p = at(i / 9 * TAU + rnd() * 0.5, 160 + rnd() * 330); clump(p.x, p.y, 34, 34, N); }
    for (let i = 0; i < 3; i++) { const p = at(rnd() * TAU, 250 + rnd() * 250); clump(p.x, p.y, 30, 30, RUBY); }
    addRival(-300, 250, RUBY, 'graze'); addRival(320, -260, GOLD, 'graze');
  } else if (id === 'charge') {
    for (let i = 0; i < 6; i++) { const p = at(rnd() * TAU, 150 + rnd() * 350); clump(p.x, p.y, 14, 24, N); }
    addRival(-320, 200, RUBY, 'hunt'); addRival(320, 220, GOLD, 'hunt');
    for (let i = 0; i < 4; i++) { const p = at(rnd() * TAU, 200 + rnd() * 250); addCrystal(p.x, p.y, { kind: 'omni' }); }
  } else if (id === 'dark') {
    for (let i = 0; i < 10; i++) { const p = at(rnd() * TAU, 140 + rnd() * 400); addCrystal(p.x, p.y, { hidden: true, kind: 'el' }); }
    for (let i = 0; i < 6; i++) { const p = at(rnd() * TAU, 160 + rnd() * 340); clump(p.x, p.y, 20, 30, N); }
    for (let i = 0; i < 4; i++) { const p = at(rnd() * TAU, 200 + rnd() * 300); clump(p.x, p.y, 24, 26, RUBY, {}); }
    for (const p of W.prisms) if (p.dom === RUBY) p.hidden = true;
    addRival(300, -200, RUBY, 'bait');
  } else { // sandbox
    W.fortX = -260; W.fortY = 180; ring(W.fortX, W.fortY, 44, 12, 7, RUBY, { shieldFrac: 0.4 });
    for (let i = 0; i < 6; i++) { const p = at(i / 6 * TAU + 0.4, 220 + rnd() * 250); clump(p.x, p.y, 30, 32, N); }
    for (let i = 0; i < 4; i++) { const p = at(rnd() * TAU, 240 + rnd() * 280); clump(p.x, p.y, 34, 32, i % 2 ? RUBY : GOLD); }
    addRival(300, 260, RUBY, 'graze'); addRival(-200, -330, GOLD, 'hunt');
    for (let i = 0; i < 6; i++) { const p = at(rnd() * TAU, 180 + rnd() * 300); addCrystal(p.x, p.y, { kind: rnd() < 0.5 ? 'omni' : 'el' }); }
    const hp = at(rnd() * TAU, 380); addCrystal(hp.x, hp.y, { hidden: true });
    for (let i = 0; i < 4; i++) { const p = at(rnd() * TAU, 200 + rnd() * 300); W.fauna.push({ x: p.x, y: p.y, alive: true, t: 0, tgt: null, chew: 1.5, wx: p.x, wy: p.y }); }
  }
  if (id === 'mass' || id === 'space') for (let i = 0; i < 3; i++) { const p = at(rnd() * TAU, 200 + rnd() * 300); addCrystal(p.x, p.y, { kind: 'omni' }); }
}
function countMine() { let n = 0; for (const p of W.prisms) if (p.alive && p.dom === ME) n++; return n; }

// ================= rivals' lives =================
function stepRivalLife(dt) {
  for (const r of W.rivals) {
    r.marked = Math.max(0, (r.marked || 0) - dt);
    if (r.alive && r.petals <= 0) { r.alive = false; r.respawn = 4; W.stats.kills = (W.stats.kills || 0) + 1; W.fx.push({ k: 'boom', x: r.x, y: r.y, t: 0, r: 30 }); }
    else if (!r.alive) { r.respawn -= dt; if (r.respawn <= 0) { const a = W.rnd() * TAU; r.x = Math.cos(a) * (R - 40); r.y = Math.sin(a) * (R - 40); r.petals = 10; r.petalDebt = 0; r.alive = true; } }
  }
  for (const c of W.crystals) if (c.decoy && c.alive) { c.decoyT -= dt; if (c.decoyT <= 0) c.alive = false; }
}

// ================= scoring: one number per element game =================
function gameScore() {
  const s = W.stats, g = W.game;
  if (g === 'time') return +(W.gateNext * 10 + (W.finishT != null ? Math.max(0, P.round - W.finishT) : 0)).toFixed(1);
  if (g === 'space') return s.hostileKills || 0;
  if (g === 'mass') return countMine() - W.mineStart;
  if (g === 'charge') return s.petalsTaken - s.petalsLost + 3 * (s.kills || 0);
  if (W.scene === 'dark') return s.crystals;
  return s.crystals * 2 + s.built + s.eaten;
}
function score() {
  return { game: W.game || W.scene, score: gameScore(), t: +W.t.toFixed(2), members: liveAgents().length, built: W.stats.built, eaten: W.stats.eaten, crystals: W.stats.crystals, petals: W.stats.petalsTaken - W.stats.petalsLost, deaths: W.stats.deaths, births: W.stats.births };
}

// ================= the AI commander: it writes INPUTS only =================
// It reads the world the way a player reads the screen, picks one objective per game, and then expresses
// it through whatever control scheme is loaded — so "how well can an expert play this scheme" is measurable.
function aiObjective() {
  const h = W.hull, g = W.game || W.scene;
  if (g === 'time') { const c = W.crystals.find((cc) => cc.alive && cc.order === W.gateNext); if (c) return c; }
  if (g === 'charge') { const r = nearestRival(h.x, h.y, 1e9); if (r) return r; }
  if (g === 'dark') { const c = W.crystals.find((cc) => cc.alive && (!cc.hidden || cc.revealed)); if (c) return c; return { x: Math.cos(W.t * 0.2) * 300, y: Math.sin(W.t * 0.2) * 300 }; }
  // space and mass: the densest patch of what this species' verb wants (sampled, so it stays cheap)
  const wantMine = false, wantNeutral = g === 'mass' && (W.sp.verb === 'claim' || W.sp.verb === 'build');
  let best = null, bs = -1;
  for (let i = 0; i < 24; i++) {
    const p = W.prisms[Math.floor(W.rnd() * W.prisms.length)]; if (!p || !p.alive || p.dom === ME || p.hidden) continue;
    if (g === 'space' && p.dom === DOM.NEUTRAL) continue;
    let n = 0; hashQuery(W.hash, p.x, p.y, 40, (q) => { if (q.dom === p.dom) n++; });
    const s = n * (wantNeutral && p.dom === DOM.NEUTRAL ? 1.5 : 1) - len(p.x - h.x, p.y - h.y) * 0.02;
    if (s > bs) { bs = s; best = p; }
  }
  if (best) W.aiPatch = { x: best.x, y: best.y };
  return W.aiPatch || h;
}
function aiInput(dt) {
  const inp = { down: false, px: null, py: null, tap: null, key: null, card: null, mood: null, box: null, release: null };
  const noise = P.aiNoise, h = W.hull;
  W.aiT = (W.aiT || 0) - dt;
  if (W.aiT > 0 && !W.aiHold) return inp;
  W.aiT = 0.5 + noise * 1.5 + W.rnd() * 0.3;
  if (W.rnd() < noise * 0.5) return inp;                       // a rookie misses decisions
  const o = aiObjective(), jit = noise * 90;
  const tx = o.x + (W.rnd() - .5) * jit, ty = o.y + (W.rnd() - .5) * jit;
  const id = W.ctl.id;
  if (id === 'focus' || id === 'bait' || id === 'split' || id === 'cards' || id === 'select') { inp.tap = { x: tx, y: ty }; if (id === 'split' && W.rnd() < 0.3) inp.key = 'squad'; }
  else if (id === 'paint') { inp.down = true; inp.px = h.x + (tx - h.x) * W.rnd(); inp.py = h.y + (ty - h.y) * W.rnd(); inp.tap = { x: tx, y: ty }; }
  else if (id === 'leash' || id === 'formation') { inp.down = true; inp.px = tx; inp.py = ty; if (id === 'formation') inp.key = (W.game === 'charge') ? 'ring' : 'wedge'; }
  else if (id === 'orders') { inp.tap = { x: tx, y: ty }; inp.key = (W.game === 'charge') ? 'attack' : (W.game === 'mass' && W.homes.length) ? 'forage' : 'forage'; }
  else if (id === 'mood') { inp.tap = { x: tx, y: ty }; inp.mood = (W.game === 'charge' || W.game === 'space') ? 0.9 : 0.45; }
  else if (id === 'sheepdog') {
    // get behind the herd, opposite the objective, and push
    const c = swarmCentre(), vx = c.x - tx, vy = c.y - ty, d = len(vx, vy) || 1;
    inp.down = true; inp.px = c.x + vx / d * 60; inp.py = c.y + vy / d * 60;
  }
  else if (id === 'cast') { if (!W.aiHold) { W.aiHold = true; W.aiHoldT = 0.6; inp.down = true; inp.px = tx; inp.py = ty; } else { W.aiHoldT -= dt; inp.down = true; inp.px = tx; inp.py = ty; if (W.aiHoldT <= 0) { inp.down = false; inp.release = { x: tx, y: ty }; W.aiHold = false; } } }
  else if (id === 'rhythm') { inp.tap = { x: tx, y: ty }; inp.key = (W.rnd() > noise * 0.8) ? 'beatGood' : 'beatBad'; }
  else if (id === 'leader') { inp.down = true; inp.px = tx; inp.py = ty; }
  // cards: play the first affordable card whose job matches the game
  if (W.meta.id === 'deck') {
    const prefer = { time: ['teleport', 'rally', 'teamCrystal', 'brood'], space: ['autothysis', 'frenzy', 'brood', 'rally'], mass: ['mound', 'brood', 'teamCrystal', 'molt'], charge: ['frenzy', 'decoy', 'shield', 'autothysis', 'brood'] }[W.game] || ['brood', 'frenzy'];
    for (const want of prefer) { const i = W.meta.hand.indexOf(want); if (i >= 0 && canPay(CARDS_BY_ID[want]) && !(want === 'mound' && W.homes.length > 1)) { inp.card = i; break; } }
    if (inp.card == null && W.econ.pool >= (W.econ.max || 10) * 0.95) inp.card = W.meta.hand.findIndex((cid) => canPay(CARDS_BY_ID[cid]));
    if (inp.card != null && inp.card < 0) inp.card = null;
  }
  if (W.draftOffer) pickDraft(0);
  return inp;
}
function swarmCentre() { const live = liveAgents(); if (!live.length) return { x: W.hull.x, y: W.hull.y }; let x = 0, y = 0; for (const a of live) { x += a.x; y += a.y; } return { x: x / live.length, y: y / live.length }; }

// ================= control: turn an input into focus / order / mood / selection =================
const BEAT = 0.75;
function applyControl(inp, dt) {
  const id = W.ctl.id;
  if (inp.mood != null) W.mood = clamp(inp.mood, 0, 1);
  if (id === 'focus' || id === 'cards' || id === 'mood') { if (inp.tap) W.focus = { x: inp.tap.x, y: inp.tap.y }; }
  else if (id === 'paint') {
    if (inp.down && inp.px != null) { for (let dx = -1; dx <= 1; dx++) for (let dy = -1; dy <= 1; dy++) { const k = fieldIdx(inp.px + dx * FS, inp.py + dy * FS); if (k >= 0) W.pher[k] = Math.min(8, W.pher[k] + (dx || dy ? 1.2 : 3)); } W.focus = { x: inp.px, y: inp.py }; }
  }
  else if (id === 'orders') { if (inp.tap) W.focus = { x: inp.tap.x, y: inp.tap.y }; const map = { forage: 0, gather: 1, attack: 2, defend: 3 }; if (inp.key in map) W.order = map[inp.key]; }
  else if (id === 'select') {
    if (inp.box) { const { x0, y0, x1, y1 } = inp.box; for (const a of liveAgents()) { a.sel = a.x >= Math.min(x0, x1) && a.x <= Math.max(x0, x1) && a.y >= Math.min(y0, y1) && a.y <= Math.max(y0, y1); if (!a.sel) a.homeFocus = { x: a.x, y: a.y }; } }
    if (inp.key === 'all') for (const a of liveAgents()) a.sel = true;
    if (inp.tap) W.focus = { x: inp.tap.x, y: inp.tap.y };
  }
  else if (id === 'formation') { if (['ring', 'line', 'wedge', 'blob'].includes(inp.key)) W.formation = inp.key; }
  else if (id === 'bait') { if (inp.tap) { W.bait = { x: inp.tap.x, y: inp.tap.y, t: 6 }; W.focus = { x: inp.tap.x, y: inp.tap.y }; } }
  else if (id === 'sheepdog') { W.focus = swarmCentre(); }
  else if (id === 'cast') {
    if (inp.down) W.castCharge = Math.min(2, (W.castCharge || 0) + dt);
    if (inp.release) { W.cast = true; W.castPt = { x: inp.release.x, y: inp.release.y }; W.castT = 1.4 + (W.castCharge || 0); W.castCharge = 0; W.focus = W.castPt; W.mood = Math.max(W.mood, 0.8); }
  }
  else if (id === 'split') {
    if (inp.key === 'squad') W.squadNext = 1 - (W.squadNext || 0);
    if (inp.tap) { if ((W.squadNext || 0) === 1) W.focusB = { x: inp.tap.x, y: inp.tap.y }; else W.focus = { x: inp.tap.x, y: inp.tap.y }; }
  }
  else if (id === 'rhythm') {
    W.beatT = (W.beatT || 0) + dt;
    const k = inp.key;
    if (k === 'beat' || k === 'beatGood' || k === 'beatBad') {
      const ph = (W.beatT % BEAT) / BEAT, off = Math.min(ph, 1 - ph), good = k === 'beatGood' || (k === 'beat' && off < 0.16);
      if (good) { W.syncKick = Math.min(3, W.syncKick + 0.6); W.buff.frenzy = Math.max(W.buff.frenzy, 1.2); W.streak = (W.streak || 0) + 1; W.fx.push({ k: 'beat', x: W.hull.x, y: W.hull.y, t: 0, good: true }); }
      else { W.syncKick = Math.max(0, W.syncKick - 0.8); W.streak = 0; for (const a of liveAgents()) a.phase += (W.rnd() - .5) * 1.5; W.fx.push({ k: 'beat', x: W.hull.x, y: W.hull.y, t: 0, good: false }); }
    }
    if (inp.tap) W.focus = { x: inp.tap.x, y: inp.tap.y };
  }
  else if (id === 'leader') {
    if (inp.down && inp.px != null) {
      let L = W.agents[W.leaderIdx];
      if (!L || !L.alive) { let bd = 1e9; W.agents.forEach((a, i) => { if (a.alive) { const d = len(a.x - inp.px, a.y - inp.py); if (d < bd) { bd = d; W.leaderIdx = i; } } }); L = W.agents[W.leaderIdx]; }
      if (L) { const dx = inp.px - L.x, dy = inp.py - L.y, d = len(dx, dy) || 1, s = Math.min(memberSpeed(L) * 1.5, d * 4); L.vx = dx / d * s; L.vy = dy / d * s; L.x += L.vx * dt; L.y += L.vy * dt; }
      W.focus = { x: inp.px, y: inp.py };
    }
  }
  if (inp.card != null) playCard(inp.card);
}

// ================= simStep =================
let liveInput = { down: false, px: null, py: null, tap: null, key: null, card: null, mood: null, box: null, release: null };
function takeLiveInput() { const i = liveInput; liveInput = { down: i.down, px: i.px, py: i.py, tap: null, key: null, card: null, mood: null, box: null, release: null }; return i; }
function simStep(dt) {
  if (!W || W.done) return;
  if (W.draftOffer && !W.headless) return;               // the draft pauses the match until you pick
  W.t += dt; W.tick++;
  const inp = (W.headless || W.build.ai) ? aiInput(dt) : takeLiveInput();
  W.hash = hashBuild(W.prisms);
  applyControl(inp, dt);
  // buffs and timed states
  for (const k of Object.keys(W.buff)) W.buff[k] = Math.max(0, W.buff[k] - dt);
  if (W.castT > 0) { W.castT -= dt; if (W.castT <= 0) W.cast = false; }
  if (W.bait && W.bait.t > 0) W.bait.t -= dt;
  if (W.molt > 0) { W.molt -= dt; if (W.molt <= 0) for (const a of liveAgents()) if (a.prevVerb) { a.verb = a.prevVerb; a.prevVerb = null; a.tgt = null; } }
  if (W.moodBoost > 0) { W.moodBoost -= dt; }
  const baseMood = W.ctl.id === 'mood' ? W.mood : P.aggression;
  W.mood = W.moodBoost > 0 ? 1 : (W.ctl.id === 'mood' ? W.mood : baseMood);
  if (W.buff.shield > 0) W.formation = 'ring';
  stepHull(dt, inp);
  stepAgents(dt);
  if (W.buff.shield > 0) for (const a of liveAgents()) { const ang = W.agents.indexOf(a) * 2.4 + W.t * 2; a.x += (W.hull.x + Math.cos(ang) * 22 - a.x) * Math.min(1, dt * 6); a.y += (W.hull.y + Math.sin(ang) * 22 - a.y) * Math.min(1, dt * 6); }
  stepRivals(dt); stepRivalLife(dt); stepProjectiles(dt); stepFauna(dt);
  stepEcon(dt); stepMeta(dt);
  W.fieldT = (W.fieldT || 0) - dt;
  if (W.fieldT <= 0) { W.fieldT = 0.1; fieldDiffuse(W.pher, 0.985, 0.2); fieldDiffuse(W.trail, 0.992, 0.12); fieldDiffuse(W.blood, 0.95, 0.25); }
  if (W.tick % 60 === 0) W.prisms = W.prisms.filter((p) => p.alive);
  for (const f of W.fx) f.t += dt; W.fx = W.fx.filter((f) => f.t < 1.2);
  if (W.toast) { W.toast.t -= dt; if (W.toast.t <= 0) W.toast = null; }
  if (W.game && W.t >= P.round) { W.done = true; W.final = score(); }
}

// ================= reset =================
let seed = 1;
function reset(s) {
  if (s != null) seed = s;
  const sc = BUILD.scene;
  W = newWorld(seed);
  W.build = JSON.parse(JSON.stringify(BUILD));
  W.sp = SPECIES_BY_ID[W.build.species] || SPECIES[0];
  W.ctl = CONTROLS_BY_ID[W.build.control] || CONTROLS[0];
  W.ctl = Object.assign({}, W.ctl, { hull: W.ctl.hull });
  W.scene = sc; W.game = GAMES.includes(sc) ? sc : (sc === 'dark' ? 'dark' : null);
  W.buff = { frenzy: 0, shield: 0 }; W.mods = { spd: 1, sense: 1, bite: 1, hp: 1 };
  W.chain = []; W.webs = []; W.decoyDensity = 0; W.flashCd = 0; W.mergeT = 0; W.gateNext = 0; W.gateCount = 0; W.molt = 0; W.moodBoost = 0;
  W.mood = W.ctl.id === 'mood' ? 0.35 : P.aggression;
  setupScene(sc);
  W.hull = makeHull(-60, 40);
  initEcon(); initMeta();
  if (W.sp.verb === 'build' || W.sp.verb === 'trail' || W.build.economy === 'hive') addHome(W.hull.x + 30, W.hull.y + 10, 'mound');
  const n = Math.max(0, Math.round(W.sp.n * P.pop));
  for (let i = 0; i < n; i++) { const a = W.rnd() * TAU, r = 10 + W.rnd() * 30; const m = addAgent(W.hull.x + Math.cos(a) * r, W.hull.y + Math.sin(a) * r, childOf(null)); if (m) m.sq = i % 2; }
  W.stats.births = 0;
  W.hostileStart = W.prisms.filter((p) => p.dom !== ME && p.dom !== DOM.NEUTRAL).length;
  W.mineStart = countMine();
  W.hash = hashBuild(W.prisms);
  W.toast = { m: W.ctl.name + ': ' + W.ctl.how, t: 5 };
}

// ================= the headless batch =================
// Default: the loaded build in all four element games, one seed, 12 s at 10 Hz — small enough that the
// verifier can run it three times. The Scorecard panel drives bigger sweeps with the same function.
function runBatch(opts) {
  opts = opts || {};
  const builds = opts.builds || [Object.assign({}, BUILD)], games = opts.games || GAMES, seeds = opts.seeds || [1], seconds = opts.seconds || 12, dt = opts.dt || 0.1;
  const savedBuild = JSON.parse(JSON.stringify(BUILD)), savedW = W, savedSeed = seed, savedP = Object.assign({}, P), savedRound = P.round, rows = [];
  for (const b of builds) {
    for (const g of games) {
      let sum = 0;
      for (const s of seeds) {
        Object.assign(BUILD, savedBuild, b, { scene: g, ai: true }); Object.assign(P, savedP, b.P || {}); P.round = seconds;
        reset(s); W.headless = true;
        const steps = Math.round(seconds / dt); for (let i = 0; i < steps && !W.done; i++) simStep(dt);
        sum += gameScore();
      }
      rows.push({ build: b.label || (b.species + '/' + b.control + '/' + b.economy + '/' + b.meta), game: g, score: +(sum / seeds.length).toFixed(1) });
    }
  }
  Object.assign(BUILD, savedBuild); Object.assign(P, savedP); P.round = savedRound; W = savedW; seed = savedSeed;
  return rows;
}
