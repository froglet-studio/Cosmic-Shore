/* omega-studio.js - the Omega Crews studio (omega.html): COMBINER crew vessels. Three shipped vessels fly as themselves
 * (MEGA forms), gather into one OMEGA form with three seats (Helm, Arms, Core), swap seats with the arena's one gesture,
 * and split again when the omega meter runs dry. The three crews are DATA (CREWS below); the machinery is shared.
 * A DESIGN studio (/vessel-studio D2): every member's flight and move here is a lab approximation of the game's vessel
 * (named per member in Vessel Config > Members); the only shipped numbers are the Skim Race courses
 * (VesselStudioEngine.TRACKS) and the platform baseline (its BASE). The lab contract: /labmaker (window.__lab).
 * ASCII only: served without a charset. Needs THREE (r128), studio-look.js, studio-domains.js, studio-ide.js,
 * studio-theme.js and studio-vessel.js (for TRACKS / BASE / DIFF; its engine is not started) before it.
 */
(function () {
  'use strict';
  const T = window.THREE, V = T ? T.Vector3 : null, Q = T ? T.Quaternion : null;
  const ENGINE = window.VesselStudioEngine, TRACKS = ENGINE.TRACKS, COURSE_INFO = ENGINE.COURSE_INFO, BASE = ENGINE.BASE, DIFF = ENGINE.DIFF;
  const $ = (id) => document.getElementById(id);
  const DEG = Math.PI / 180, lerp = (a, b, t) => a + (b - a) * t, clamp = (x, a, b) => Math.max(a, Math.min(b, x));
  const ease = (x) => x < 0 ? Math.cos(x * Math.PI / 4) - 1 : -(Math.cos(x * Math.PI / 4) - 1);
  const easeInv = (e) => { e = clamp(e, -0.999, 0.999); return Math.sign(e) * (4 / Math.PI) * Math.acos(1 - Math.abs(e)); };
  function rng(seed) { let s = (seed >>> 0) || 1; return () => { s ^= s << 13; s >>>= 0; s ^= s >> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; }
  const elx = (t, c, x) => { const e = document.createElement(t); if (c) e.className = c; if (x != null) e.textContent = x; return e; };
  const ELS = ['charge', 'mass', 'space', 'time'], EL_COLOR = { charge: '--charge', mass: '--mass', space: '--space', time: '--time' };
  const SEATS = ['Helm', 'Arms', 'Core'];
  // Every crew is three ROLES (Yash, 2026-10-26: 'defender, midfielder and attacker'): member 0 is the MOBILITY vessel (it does
  // the manoeuvring), member 1 the ATTACKER (the shooting), member 2 the DEFENDER (the protecting). The Unity clone flies
  // them as one rigid formation with those roles (OMEGA_CREW.md); this page keeps the mega / omega forms.
  const ROLES = ['Mobility', 'Attack', 'Defence'];

  // ================= the members: nine shipped vessels as MEGA forms (lab approximations of the game's kit) =================
  // flight: cruise u/s, turn deg/s (pitch/yaw), roll deg/s. move: the one RT move this lab gives the mega form, named after
  // the ability the game ships (R_VesselActions/<VESSEL>.md); every number is a proposal.
  const MEMBERS = {
    Stoat:     { cruise: 60, turn: 110, roll: 130, color: 0xc48bff, body: 'plates',  move: { kind: 'sling',  name: 'Dipole sling',    ship: 'Field Dipole (Space): a pole pair ahead catches and slings you', boost: 2.0, time: 1.2, cool: 4 } },
    Thresher:  { cruise: 70, turn: 130, roll: 150, color: 0x7fe0ff, body: 'ball',    move: { kind: 'whip',   name: 'Wrecking ball',   ship: 'Wrecking Ball (Charge): the ball on its chain smashes mass', reach: 30, cool: 0.4 } },
    Butterfly: { cruise: 55, turn: 80,  roll: 90,  color: 0xffd0f0, body: 'wings',   move: { kind: 'gate',   name: 'Fold gate',       ship: 'Fold (Time): a gate pair; fly into one, come out of the other', span: 260, cool: 6 } },
    Rhino:     { cruise: 58, turn: 90,  roll: 100, color: 0xff6a4d, body: 'wedge',   move: { kind: 'ram',    name: 'Plough',          ship: 'Plough + Ramp Spool (Time): mass ahead is shoved aside, a straight run spools a boost', reach: 16, boost: 1.4, time: 1.0, cool: 3 } },
    Grizzly:   { cruise: 52, turn: 85,  roll: 90,  color: 0xd9b36a, body: 'box',     move: { kind: 'cannon', name: 'Charged cannon',  ship: 'Charged Cannon (Space): hold to charge, release a shot that bursts', speed: 240, blast: 18, charge: 1.5 } },
    Scarab:    { cruise: 75, turn: 120, roll: 150, color: 0x7affc2, body: 'disc',    move: { kind: 'blast',  name: 'Cavitation dash', ship: 'Cavitation Blast (Charge): a dash whose swept plate shreds mass', speed: 260, time: 0.3, reach: 14, cool: 2.5 } },
    Squirrel:  { cruise: 66, turn: 120, roll: 140, color: 0x13fff2, body: 'cone',    move: { kind: 'drift',  name: 'Skim boost',      ship: 'Skimming (Time) + Boost Ring (Mass): a burst of the boost it banked', boost: 1.5, time: 1.0, cool: 2.5 } },
    Sparrow:   { cruise: 72, turn: 110, roll: 160, color: 0xffa700, body: 'fighter', move: { kind: 'guns',   name: 'Pulsefire',       ship: 'Pulsefire Cannons (Space): rounds that destroy prisms ahead', range: 220, rate: 0.1 } },
    Manta:     { cruise: 68, turn: 100, roll: 120, color: 0x6680ff, body: 'delta',   move: { kind: 'tunnel', name: 'Boost tunnel',    ship: 'Yastri boost tunnel (Mass): a ring any member passes through for a boost', radius: 14, boost: 1.6, life: 20, cool: 3 } },
  };

  // ================= the three crews (proposals; Yash 2026-10-26: the Stoat in one) =================
  const CREWS = [
    { id: 'warden', name: 'Warden', tag: 'the field crew', accent: '#c48bff', members: ['Stoat', 'Sparrow', 'Rhino'],
      weapon: { kind: 'whip', name: 'Ripple whip', reach: 60, hits: 2, cool: 0.6, info: 'the Thresher\'s ball, grown: a 60 u sweep that smashes mass and cracks a wall in two hits' },
      core: { shield: 'Fold ward', finisher: 'Annihilation', info: 'Core holds the Butterfly\'s fold as a ward; its finisher is the Stoat\'s dipole collapsing: a 180 u cone of annihilation' },
      omega: { turn: 0.6, speed: 1.15 }, lore: 'Wormholes, a wrecking chain and folded space: the crew that bends the field. Its omega form is a long lance of plates with the ball swinging from its prow.' },
    { id: 'bulwark', name: 'Bulwark', tag: 'the heavy crew', accent: '#ff6a4d', members: ['Scarab', 'Grizzly', 'Butterfly'],
      weapon: { kind: 'cannon', name: 'Siege cannon', speed: 220, blast: 30, charge: 1.6, info: 'the Grizzly\'s cannon at omega scale: a full charge breaks a wall outright' },
      core: { shield: 'Switch wall', finisher: 'Breaker', info: 'Core lays the Scarab\'s switch as a shield ring; its finisher is the Rhino\'s Breaker: the whole hull a blade, a 150 u cone cut' },
      omega: { turn: 0.5, speed: 1.1 }, lore: 'A plough, a cannon and a punch. The slowest omega and the one that does not stop: it bulldozes mass it touches.' },
    { id: 'tempest', name: 'Tempest', tag: 'the fast crew', accent: '#13fff2', members: ['Squirrel', 'Thresher', 'Manta'],
      weapon: { kind: 'guns', name: 'Pulsefire battery', range: 260, rate: 0.08, wallSeconds: 2.5, info: 'the Sparrow\'s cannons as a battery: a wall under fire for 2.5 s breaks' },
      core: { shield: 'Afterburner veil', finisher: 'Kabloom', info: 'Core keeps the Manta\'s tunnel rings coming behind the omega (a boost for whoever follows); its finisher is the Manta\'s Kabloom: a 200 u cone' },
      omega: { turn: 0.75, speed: 1.3 }, lore: 'The racer, the shooter and the stinger. The fastest omega, and the one whose Core leaves boost rings in its wake.' },
  ];
  const CREW_BY = {}; CREWS.forEach((c) => { CREW_BY[c.id] = c; });

  // ---- the crew's lab numbers (the SPEC sliders; every one a proposal) ----
  const DEFAULTS = { combineSeconds: 1.6, gatherRange: 140, meterPerCrystal: 0.22, meterPerSkim: 0.004, regen: 0.02, drain: 0.05, combinedCrystal: 0.5, shieldDrain: 0.06, combineAt: 0.3, finisherAt: 0.9, finisherCone: 180, finisherCost: 0.7, splitSeconds: 0.5, omegaContactPlough: 1 };
  const SPEC = [
    ['combineSeconds', 0.5, 4, 0.1, 'Combine time', 'Seconds the members take to dock into the omega form once gathered'],
    ['gatherRange', 60, 300, 10, 'Gather range', 'Every member must be within this many u of the one holding LT for the combine to start'],
    ['meterPerCrystal', 0.05, 0.5, 0.01, 'Meter per crystal', 'How much of the omega meter a collected crystal adds'],
    ['meterPerSkim', 0, 0.02, 0.001, 'Meter per skim', 'How much a skim adds (any member, any prism)'],
    ['regen', 0, 0.08, 0.005, 'Apart regen', 'Meter gained per second while the crew flies apart (the recovery after a finisher)'],
    ['drain', 0, 0.15, 0.005, 'Omega drain', 'Meter lost per second while combined'],
    ['combinedCrystal', 0, 1, 0.05, 'Combined crystal pay', 'How much of a crystal\'s meter the omega form earns (apart earns it all)'],
    ['shieldDrain', 0, 0.2, 0.005, 'Shield drain', 'Extra meter lost per second while Core holds the shield'],
    ['combineAt', 0, 0.8, 0.05, 'Combine at', 'The meter a combine needs (and the AI crew waits for)'],
    ['finisherAt', 0.3, 1, 0.05, 'Finisher at', 'The meter Core needs to fire the finisher'],
    ['finisherCone', 60, 320, 10, 'Finisher reach', 'How far ahead the finisher\'s cone destroys everything (u)'],
    ['finisherCost', 0.2, 1, 0.05, 'Finisher cost', 'Meter the finisher spends; the crew splits as it fires'],
    ['splitSeconds', 0.2, 2, 0.1, 'Split time', 'Seconds Helm holds LT to split on purpose'],
  ];
  const ELEMENTS = {   // the omega's four, proposals (the fleet contract: one scaled parameter per element)
    charge: { name: 'Weapon power', param: 'weaponPower', range: [1, 1.6], l5: 'Overload', tip: 'Charge scales the Arms seat\'s weapon: reach, blast and rate.' },
    mass: { name: 'Ward', param: 'shieldDrain', range: [1, 0.4], l5: 'Bulwark', tip: 'Mass makes the Core shield cheaper to hold.' },
    space: { name: 'Finisher reach', param: 'finisherCone', range: [1, 1.5], l5: 'Horizon', tip: 'Space lengthens the finisher cone.' },
    time: { name: 'Quick combine', param: 'combineSeconds', range: [1, 0.5], l5: 'Instant', tip: 'Time shortens the dock and the split.' },
  };
  const STYLES = [
    { name: 'Balanced', lv: { charge: 0, mass: 0, space: 0, time: 0 }, info: 'Every element at 0: the lab\'s numbers.' },
    { name: 'Finisher', lv: { charge: 10, mass: 0, space: 10, time: 0 }, info: 'The big moment: a long weapon reach and a wide finisher.' },
    { name: 'Marathon', lv: { charge: 0, mass: 10, space: 0, time: 10 }, info: 'Stay combined longer: a cheap shield and a quick dock.' },
    { name: 'Maxed', lv: { charge: 10, mass: 10, space: 10, time: 10 }, info: 'Every element at 10. A ceiling, not a match state.' },
  ];

  // ================= settings =================
  const S = { course: 1, crew: 'warden', seat: 0, levels: ['Hard', 'Hard', 'Hard'], nobody: false, view: 0, cam: 'Chase', speed: 1, thinking: true,
    idleHold: true, aiMoves: true, aiCombine: true, massDensity: 1, trigDepth: 1, style: 'Balanced', lv: { charge: 0, mass: 0, space: 0, time: 0 }, params: {} };
  const LS = 'cs.studio.omega.v1';
  try { const s = JSON.parse(localStorage.getItem(LS) || 'null'); if (s) Object.assign(S, s, { lv: Object.assign({}, S.lv, s.lv || {}), params: Object.assign({}, s.params || {}), levels: (s.levels || S.levels).slice(0, 3) }); } catch (e) { /* storage blocked */ }
  if (!CREW_BY[S.crew]) S.crew = 'warden';
  function save() { try { localStorage.setItem(LS, JSON.stringify(S)); } catch (e) { /* storage blocked */ } }
  const par = (k) => (k in S.params ? S.params[k] : DEFAULTS[k]);
  function mults(lv) {
    const m = {}; for (const k in DEFAULTS) m[k] = par(k); m.weaponPower = 1;
    for (const el of ELS) { const e = ELEMENTS[el], r = e.range; m[e.param] = (e.param in DEFAULTS ? par(e.param) : 1) * lerp(r[0], r[1], clamp(lv[el] / 10, 0, 1)); }
    m.l5 = { charge: lv.charge >= 5, mass: lv.mass >= 5, space: lv.space >= 5, time: lv.time >= 5 };
    return m;
  }
  const styleOf = (name) => STYLES.find((s) => s.name === name) || STYLES[0];

  // ================= the course (the game's Skim Race tracks, as studio-vessel.js builds them) =================
  function spline(p, seg, t) {
    const c = p.length, p0 = p[((seg - 1) % c + c) % c], p1 = p[seg], p2 = p[(seg + 1) % c], p3 = p[(seg + 2) % c], t2 = t * t, t3 = t2 * t;
    const f = (i) => 0.5 * ((2 * p1[i]) + (-p0[i] + p2[i]) * t + (2 * p0[i] - 5 * p1[i] + 4 * p2[i] - p3[i]) * t2 + (-p0[i] + 3 * p1[i] - 3 * p2[i] + p3[i]) * t3);
    return new V(f(0), f(1), f(2));
  }
  function buildTrack(def) {
    const w = def.wp, n = w.length, line = [];
    for (let seg = 0; seg < n; seg++) {
      let len = 0;
      if (!def.spline) len = new V(...w[seg]).distanceTo(new V(...w[(seg + 1) % n]));
      else { let prev = spline(w, seg, 0); for (let s = 1; s <= 20; s++) { const c = spline(w, seg, s / 20); len += prev.distanceTo(c); prev = c; } }
      const blocks = Math.max(1, Math.round(len / BASE.blockSpacing));
      for (let i = 0; i < blocks; i++) { const t = i / blocks; line.push(def.spline ? spline(w, seg, t) : new V(...w[seg]).lerp(new V(...w[(seg + 1) % n]), t)); }
    }
    const tan = line.map((p, i) => line[(i + 1) % line.length].clone().sub(p).normalize());
    return { def, line, tan, anchors: def.an.map((a) => new V(...a)), target: def.wp.length * def.laps };
  }
  function Grid(cell) { this.cell = cell; this.map = new Map(); }
  Grid.prototype.h = function (x, y, z) { return ((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) | 0; };
  Grid.prototype.add = function (p) { const c = this.cell; const k = this.h(Math.floor(p.pos.x / c), Math.floor(p.pos.y / c), Math.floor(p.pos.z / c)); let a = this.map.get(k); if (!a) this.map.set(k, a = []); a.push(p); p.k = k; };
  Grid.prototype.remove = function (p) { const a = this.map.get(p.k); if (!a) return; const i = a.indexOf(p); if (i >= 0) a.splice(i, 1); };
  Grid.prototype.near = function (pos, r, out) {
    out.length = 0; const c = this.cell, n = Math.ceil(r / c), cx = Math.floor(pos.x / c), cy = Math.floor(pos.y / c), cz = Math.floor(pos.z / c);
    for (let x = cx - n; x <= cx + n; x++) for (let y = cy - n; y <= cy + n; y++) for (let z = cz - n; z <= cz + n; z++) { const a = this.map.get(this.h(x, y, z)); if (a) for (const p of a) if (!p.dead) out.push(p); }
    return out;
  };
  function basisQuat(t) { const s = new V().crossVectors(t, new V(0, 1, 0)); if (s.lengthSq() < 1e-4) s.set(1, 0, 0); s.normalize(); const u = new V().crossVectors(s, t).normalize(); return new Q().setFromRotationMatrix(new T.Matrix4().makeBasis(s, u, t.clone().negate())); }
  const tmpArr = [], AX = new V(1, 0, 0), AY = new V(0, 1, 0), AZ = new V(0, 0, 1), dq = new Q();
  const fwdOf = (b) => new V(0, 0, -1).applyQuaternion(b.quat), upOf = (b) => new V(0, 1, 0).applyQuaternion(b.quat), rightOf = (b) => new V(1, 0, 0).applyQuaternion(b.quat);

  function addPrism(W, o) { o.cool = []; o.hull = []; o.flash = 0; W.prisms.push(o); W.grid.add(o); W.dirty = true; return o; }
  function removePrism(W, o) { if (o.dead) return; W.grid.remove(o); o.dead = true; W.dirty = true; }
  function smashNear(W, pos, r, walls) {   // destroy mass round a point; a wall only when `walls` says so (returns prisms destroyed, walls hit)
    let n = 0, wall = null;
    for (const p of W.grid.near(pos, r + 6, tmpArr)) { if (p.pos.distanceTo(pos) > r) continue; if (p.kind === 'wall') { wall = p.wallRef; continue; } if (p.kind === 'track') continue; removePrism(W, p); n++; }
    if (wall && walls) hitWall(W, wall, walls);
    return { n, wall };
  }
  function hitWall(W, wall, hits) {
    if (wall.broken) return false; wall.hp -= hits; for (const p of wall.prisms) p.flash = 1; W.dirty = true;
    if (wall.hp <= 0) { wall.broken = true; for (const p of wall.prisms) removePrism(W, p); W.wallsBroken++; W.events.push('Wall broken: crystal ' + (wall.crystalIndex + 1) + ' unlocked'); return true; }
    return false;
  }

  // ================= the world: three bodies (mega forms) or one (the omega) =================
  // a BODY is a flying thing: a member in mega form, or the omega. The crew's state (meter, form, seats) is W.crew.
  function makeBody(W, name, member, pos, quat, i) {
    return { i, name, m: member, pos: pos.clone(), quat: quat.clone(), course: fwdOf({ quat }), vel: new V(), speed: 0, slow: 1, boost: 1, boostT: 0, throttle: 1,
      F: { cruise: member.cruise, turn: member.turn, roll: member.roll }, K: { cool: 0, hold: 0, active: false, gates: [] }, railAcc: 0, rails: 0, skims: 0, resets: 0,
      stats: { top: 0, uses: 0 }, hc: null, believed: null, aim: null, aimWhy: '', aiState: '', target: -1, lastInp: null, omega: false, wake: [] };
  }
  function makeWorld(courseId, crewId, levels, seed, opts) {
    opts = opts || {};
    const crew = CREW_BY[crewId], track = buildTrack(TRACKS[courseId]);
    const W = { track, course: courseId, crewDef: crew, prisms: [], grid: new Grid(24), bodies: [], t: 0, rand: rng(seed), seed, events: [], over: false, dirty: true, watch: 0, finishT: null,
      massDensity: opts.massDensity != null ? opts.massDensity : S.massDensity, walls: [], wallsBroken: 0, crystals: [], collected: 0, lit: [], rings: [], shots: [], seatsAI: levels.slice(), nobody: !!opts.nobody,
      policy: opts.policy || 'crew', lv: Object.assign({}, opts.lv || S.lv), m: null, swaps: 0, combinedT: 0,
      crew: { form: 'mega', meter: opts.meter != null ? opts.meter : 0.5, gather: 0, dock: 0, split: 0, seat: opts.seat != null ? opts.seat : S.seat, shield: false, finisherT: -9, aim: { yaw: 0, pitch: 0 }, weapon: { cool: 0, charge: 0, fireT: 0 }, omega: null, uses: 0, combines: 0 } };
    W.m = mults(W.lv);
    for (let i = 0; i < track.line.length; i++) addPrism(W, { pos: track.line[i].clone(), quat: basisQuat(track.tan[i]), scale: [3.2, 3.2, 9], owner: -1, domain: 'track', shield: false, danger: false, kind: 'track' });
    scatterMass(W);
    // the crystals: one per anchor per lap; every third is LOCKED inside a wall across the course just before its anchor
    for (let k = 0; k < track.target; k++) {
      const a = track.anchors[k % track.anchors.length], ai = nearestIndex(track, a, 0, true);
      const c = { i: k, pos: a.clone(), taken: false, locked: (k % 3) === 2 && k < track.target - 1, wall: null };
      if (c.locked) {
        const base = track.line[ai], tan = track.tan[ai], q = basisQuat(tan), right = new V(1, 0, 0).applyQuaternion(q), up = new V(0, 1, 0).applyQuaternion(q);
        const wall = { hp: 1, broken: false, crystalIndex: k, prisms: [], centre: base.clone().addScaledVector(up, 0), quat: q };
        for (let x = -2; x <= 2; x++) for (let y = -2; y <= 2; y++) wall.prisms.push(addPrism(W, { pos: base.clone().addScaledVector(right, x * 9).addScaledVector(up, y * 9 + 4), quat: q.clone(), scale: [8.6, 8.6, 5], owner: -1, domain: 'wall', shield: true, danger: false, kind: 'wall', wallRef: wall }));
        c.pos.copy(base).addScaledVector(up, 4); c.wall = wall; W.walls.push(wall);
      }
      W.crystals.push(c);
    }
    relight(W);
    const t0 = track.tan[0], q0 = basisQuat(t0), side = new V(1, 0, 0).applyQuaternion(q0);
    crew.members.forEach((name, i) => {
      const B = makeBody(W, name, MEMBERS[name], track.line[0].clone().addScaledVector(side, (i - 1) * 26).addScaledVector(t0, -40), q0, i);
      B.hc = { level: DIFF[levels[i]] || DIFF.Hard, rng: rng(seed * 31 + i * 7 + 1), tracking: -1, noticeAt: 0, misjudged: false, until: 0, off: new V(), unnoticed: false, pressAt: 0 };
      W.bodies.push(B);
    });
    return W;
  }
  function scatterMass(W) {
    const d = W.massDensity; if (d <= 0) return;
    const r = rng(W.seed * 7 + 99), L = W.track.line, n = L.length, every = BASE.mass.every / d; let acc = 0;
    for (let i = 0; i < n; i++) {
      acc += L[i].distanceTo(L[(i + 1) % n]); if (acc < every) continue; acc -= every;
      const tan = W.track.tan[i], s = new V().crossVectors(tan, new V(0, 1, 0)); if (s.lengthSq() < 1e-4) s.set(1, 0, 0); s.normalize();
      const u = new V().crossVectors(s, tan).normalize(), a = r() * Math.PI * 2, off = lerp(BASE.mass.lateral[0], BASE.mass.lateral[1], r());
      const c = L[i].clone().addScaledVector(s, Math.cos(a) * off).addScaledVector(u, Math.sin(a) * off), cnt = Math.round(lerp(BASE.mass.count[0], BASE.mass.count[1], r()));
      for (let k = 0; k < cnt; k++) { const j = new V(r() * 2 - 1, r() * 2 - 1, r() * 2 - 1).multiplyScalar(BASE.mass.ball); addPrism(W, { pos: c.clone().add(j), quat: new Q().setFromEuler(new T.Euler(r() * 6.28, r() * 6.28, r() * 6.28)), scale: [3, 3, 6], owner: -1, domain: 'mass', shield: false, danger: false, kind: 'mass' }); }
    }
  }
  function relight(W) { W.lit = []; const first = W.crystals.findIndex((c) => !c.taken); if (first < 0) return; for (let k = first; k < Math.min(first + 3, W.crystals.length); k++) if (!W.crystals[k].taken) W.lit.push(W.crystals[k]); }
  function nearestIndex(track, pos, hint, full) {
    const L = track.line, n = L.length; let best = hint, bd = Infinity;
    if (full) { for (let i = 0; i < n; i++) { const d = L[i].distanceToSquared(pos); if (d < bd) { bd = d; best = i; } } return best; }
    for (let k = -30; k <= 60; k++) { const i = ((hint + k) % n + n) % n; const d = L[i].distanceToSquared(pos); if (d < bd) { bd = d; best = i; } }
    return best;
  }
  window.__omegaParts = { T, V, Q, TRACKS, COURSE_INFO, BASE, DIFF, DEG, lerp, clamp, ease, easeInv, rng, elx, ELS, EL_COLOR, SEATS, ROLES, MEMBERS, CREWS, CREW_BY, DEFAULTS, SPEC, ELEMENTS, STYLES, S, LS, save, par, mults, styleOf,
    buildTrack, Grid, basisQuat, tmpArr, AX, AY, AZ, dq, fwdOf, upOf, rightOf, addPrism, removePrism, smashNear, hitWall, makeBody, makeWorld, relight, nearestIndex, $ };
})();

// ================= part B: the physics of a body, the members' moves, the combine, the omega seats =================
(function () {
  'use strict';
  const P = window.__omegaParts, { T, V, Q, BASE, DEG, lerp, clamp, ease, easeInv, MEMBERS, SEATS, tmpArr, AX, AY, AZ, dq, fwdOf, upOf, rightOf, addPrism, removePrism, smashNear, hitWall, relight, nearestIndex } = P;

  function sticks(inp) {
    if (inp.pad) return { XSum: ease(inp.R.x + inp.L.x), YSum: -ease(inp.R.y + inp.L.y), YDiff: ease(inp.R.y - inp.L.y), XDiff: clamp((inp.R.x - inp.L.x + 2) / 4, 0, 1) };
    return { XSum: inp.k.yaw, YSum: -inp.k.pitch, YDiff: inp.k.roll, XDiff: inp.k.throttle };
  }
  function steer(B, inp, dt, turnMult) {
    const s = sticks(inp), k = (turnMult == null ? 1 : turnMult) * DEG * dt, F = B.F;
    dq.setFromAxisAngle(AX, -s.YSum * F.turn * k); B.quat.multiply(dq);
    dq.setFromAxisAngle(AY, -s.XSum * F.turn * k); B.quat.multiply(dq);
    dq.setFromAxisAngle(AZ, s.YDiff * F.roll * k); B.quat.multiply(dq);
    B.quat.normalize(); return s;
  }
  // course-following flight: speed eases to its target, the course follows the nose (the recipe engine's `move`)
  function fly(B, dt, targetSpeed, grip, rate) {
    const fwd = fwdOf(B);
    B.speed += (targetSpeed - B.speed) * (1 - Math.exp(-(rate == null ? 1.5 : rate) * dt));
    B.course.lerp(fwd, 1 - Math.exp(-(grip == null ? 30 : grip) * dt)).normalize();
    B.vel.copy(B.course).multiplyScalar(B.speed);
    B.pos.addScaledVector(B.course, B.speed * B.slow * dt);
  }
  const edge = (B, key, v) => { const was = !!B.K[key]; B.K[key] = !!v; return !!v && !was; };

  // ---- the members' MEGA moves (RT) ----
  function megaMove(W, B, inp, dt) {
    const mv = B.m.move, K = B.K; K.cool = Math.max(0, K.cool - dt); K.active = false;
    const rt = inp.rt > 0.5, press = edge(B, 'rtWas', rt);
    switch (mv.kind) {
      case 'sling': case 'drift':
        if (press && K.cool <= 0) { B.boost = mv.boost; B.boostT = mv.time; K.cool = mv.cool; B.stats.uses++; W.events.push(B.name + ': ' + mv.name); }
        K.active = B.boostT > 0; break;
      case 'ram':
        if (press && K.cool <= 0) { K.ramT = mv.time; K.cool = mv.cool; B.stats.uses++; B.boost = Math.max(B.boost, mv.boost); B.boostT = mv.time; }
        if (K.ramT > 0) { K.ramT -= dt; K.active = true; const r = smashNear(W, B.pos.clone().addScaledVector(fwdOf(B), mv.reach * 0.6), mv.reach, 0); if (r.n) B.smashed = (B.smashed || 0) + r.n; }
        break;
      case 'whip':
        K.ball = K.ball || new V(); K.ball.copy(B.pos).addScaledVector(fwdOf(B), mv.reach * 0.8).addScaledVector(rightOf(B), Math.sin(W.t * 7) * mv.reach * 0.5);
        if (rt) { K.active = true; if (K.cool <= 0) { const r = smashNear(W, K.ball, mv.reach * 0.6, 0); if (r.n) { K.cool = mv.cool; B.stats.uses++; B.smashed = (B.smashed || 0) + r.n; } } }
        break;
      case 'guns':
        if (rt) { K.active = true; K.gunT = (K.gunT || 0) - dt; if (K.gunT <= 0) { K.gunT = mv.rate; const fwd = fwdOf(B); let best = null, bd = mv.range; for (const p of W.grid.near(B.pos.clone().addScaledVector(fwd, mv.range / 2), mv.range / 2 + 20, tmpArr)) { if (p.kind === 'track' || p.kind === 'wall') continue; const d = p.pos.clone().sub(B.pos), along = d.dot(fwd); if (along < 4 || along > mv.range) continue; if (Math.sqrt(Math.max(0, d.lengthSq() - along * along)) > 6) continue; if (along < bd) { bd = along; best = p; } } if (best) { removePrism(W, best); B.stats.uses++; B.smashed = (B.smashed || 0) + 1; W.shots.push({ a: B.pos.clone(), b: best.pos.clone(), t: 0.12 }); } } }
        break;
      case 'cannon':
        if (rt) { K.charge = Math.min(1, (K.charge || 0) + dt / mv.charge); K.active = true; }
        else if (edge(B, 'rtRel', !rt) && (K.charge || 0) > 0.1) { W.shots.push({ shot: true, pos: B.pos.clone().addScaledVector(fwdOf(B), 6), vel: fwdOf(B).multiplyScalar(mv.speed).add(B.vel.clone().multiplyScalar(0.3)), blast: mv.blast * (0.5 + K.charge), life: 2.5, owner: B, wallHits: 0 }); B.stats.uses++; K.charge = 0; }
        if (!rt) edge(B, 'rtRel', false);
        break;
      case 'blast':
        if (press && K.cool <= 0) { K.dashT = mv.time; K.cool = mv.cool; B.stats.uses++; }
        if (K.dashT > 0) { K.dashT -= dt; K.active = true; B.speed = mv.speed; const r = smashNear(W, B.pos, mv.reach, 0); if (r.n) B.smashed = (B.smashed || 0) + r.n; }
        break;
      case 'gate': {
        if (press && K.cool <= 0) {
          if (!K.gateA) K.gateA = { pos: B.pos.clone(), quat: B.quat.clone(), born: W.t };
          else { const g = { a: K.gateA, b: { pos: B.pos.clone(), quat: B.quat.clone() }, born: W.t, life: 25 }; W.rings.push(Object.assign({ gate: true, owner: B }, g)); K.gateA = null; K.cool = mv.cool; B.stats.uses++; W.events.push(B.name + ': fold gate laid'); }
        }
        K.active = !!K.gateA; break;
      }
      case 'tunnel':
        if (press && K.cool <= 0) { W.rings.push({ tunnel: true, pos: B.pos.clone(), quat: B.quat.clone(), radius: mv.radius, boost: mv.boost, born: W.t, life: mv.life, owner: B }); K.cool = mv.cool; B.stats.uses++; }
        break;
    }
  }
  // rings and gates: a body through a tunnel ring is boosted; through a gate it comes out of the other gate
  function throughRings(W, B, dt) {
    for (const r of W.rings) {
      if (r.dead) continue;
      if (r.tunnel) { if (B.pos.distanceTo(r.pos) < r.radius && (r.cool || 0) <= W.t - 1 + (B.i + 1) * 0) { if ((r.last || {})[B.i] == null || W.t - r.last[B.i] > 2) { r.last = r.last || {}; r.last[B.i] = W.t; B.boost = Math.max(B.boost, r.boost); B.boostT = 2; W.events.push(B.name + ' rides the tunnel'); } } }
      else if (r.gate) { for (const [from, to] of [[r.a, r.b], [r.b, r.a]]) { if (B.pos.distanceTo(from.pos) < 10 && (r.lastT == null || W.t - r.lastT > 1)) { r.lastT = W.t; B.pos.copy(to.pos).addScaledVector(fwdOf({ quat: to.quat }), 12); B.quat.copy(to.quat); B.course.copy(fwdOf(B)); W.events.push(B.name + ' folds through'); } } }
    }
  }
  function stepShots(W, dt) {
    for (const s of W.shots) {
      if (s.dead) continue;
      if (!s.shot) { s.t -= dt; if (s.t <= 0) s.dead = true; continue; }
      s.life -= dt; s.pos.addScaledVector(s.vel, dt); if (s.life <= 0) { s.dead = true; continue; }
      const near = W.grid.near(s.pos, 10, tmpArr); let hit = null;
      for (const p of near) { if (p.kind === 'track') continue; if (p.pos.distanceTo(s.pos) < 6 + (p.kind === 'wall' ? 4 : 0)) { hit = p; break; } }
      if (hit) { const r = smashNear(W, s.pos, s.blast, s.wallHits); s.dead = true; W.events.push((s.owner ? s.owner.name : 'Omega') + ': cannon burst' + (r.wall && s.wallHits ? ' on a wall' : '')); if (s.owner) s.owner.smashed = (s.owner.smashed || 0) + r.n; }
    }
    if (W.shots.length > 60) W.shots = W.shots.filter((s) => !s.dead);
    for (const r of W.rings) if (!r.dead && W.t - r.born > r.life) r.dead = true;
    if (W.rings.length > 40) W.rings = W.rings.filter((r) => !r.dead);
  }

  // ---- the common step of a body: contacts, skims, crystals, trail ----
  function contacts(W, B, dt, plough) {
    const near = W.grid.near(B.pos, BASE.skimR + 10, tmpArr), reachBase = B.omega ? 7 : BASE.hullReach;
    for (const p of near) {
      const d = p.pos.distanceTo(B.pos), own = p.owner === B.i && !B.omega, reach = reachBase + (p.kind === 'track' ? 1.6 : p.kind === 'wall' ? 4 : 0.9) * (p.scale ? p.scale[0] / 3 : 1);
      if (d < reach + 1 && (p.hull[B.i] || 0) <= W.t && !(own && p.kind === 'rail' && W.t - (p.born || 0) < BASE.rail.grace)) {
        p.hull[B.i] = W.t + 0.6;
        if (p.kind === 'wall') { // a wall stops everyone: the omega bounces off it too, only its weapon breaks it
          const n = B.pos.clone().sub(p.pos).normalize(); B.pos.addScaledVector(n, 3); B.course.reflect(n.negate()).negate(); B.speed *= 0.5; B.resets++; p.flash = 1; W.dirty = true; W.events.push((B.omega ? 'Omega' : B.name) + ' hits the wall');
          continue;
        }
        if (plough && p.kind !== 'track') { removePrism(W, p); B.smashed = (B.smashed || 0) + 1; continue; }   // the omega bulldozes mass
        if (W.crew.shield && B.omega) { removePrism(W, p); continue; }
        B.boost = 1; B.boostT = 0; B.slow = Math.min(B.slow, 0.9); B.resets++; W.events.push((B.omega ? 'Omega' : B.name) + ': hull contact');
        continue;
      }
      if ((p.cool[B.i] || 0) > W.t || d > BASE.skimR || own || p.kind === 'wall') continue;
      p.cool[B.i] = W.t + 0.6; p.flash = 1; W.dirty = true;
      B.boost = clamp(B.boost + BASE.skimAdd, 1, BASE.maxBoost); B.skims++; W.crew.meter = Math.min(1, W.crew.meter + W.m.meterPerSkim);
    }
    // crystals: any lit, unlocked crystal within reach
    const reach = B.omega ? BASE.captureReach * 1.3 : BASE.captureReach;
    for (const c of W.lit) {
      if (c.taken || (c.locked && !c.wall.broken)) continue;
      if (c.pos.distanceTo(B.pos) < reach) { c.taken = true; W.collected++; W.crew.meter = Math.min(1, W.crew.meter + W.m.meterPerCrystal * (B.omega ? W.m.combinedCrystal : 1)); W.events.push((B.omega ? 'Omega' : B.name) + ' takes crystal ' + (c.i + 1)); relight(W); if (W.collected >= W.track.target) { W.finishT = W.t; W.events.push('Omega Run done in ' + W.t.toFixed(1) + ' s'); } break; }
    }
    if (!B.omega && W.t > BASE.rail.startDelay) { B.railAcc += B.speed * dt; while (B.railAcc >= BASE.rail.spacing) { B.railAcc -= BASE.rail.spacing; const right = rightOf(B); for (const s of [-1, 1]) addPrism(W, { pos: B.pos.clone().addScaledVector(right, s * BASE.rail.offset), quat: B.quat.clone(), scale: [0.83, 0.83, 6.1], owner: B.i, domain: W.crewDef.id, shield: false, danger: false, kind: 'rail', born: W.t }); B.rails += 2; if (B.rails > 120) { let rm = 0; for (const p of W.prisms) if (!p.dead && p.kind === 'rail' && p.owner === B.i) { removePrism(W, p); B.rails--; if (++rm >= 2) break; } } } }
    B.wakeT = (B.wakeT || 0) + dt; if (B.wakeT > 0.1) { B.wakeT = 0; B.wake.push(B.pos.clone()); if (B.wake.length > 30) B.wake.shift(); }
  }
  function stepMember(W, B, inp, dt) {
    B.lastInp = inp; B.slow += (1 - B.slow) * (1 - Math.exp(-dt));
    if (B.boostT > 0) B.boostT -= dt; else if (B.boost > 1) B.boost = Math.max(1, B.boost - BASE.boostDecay * dt);
    steer(B, inp, dt, 1);
    const holding = W.crew.calling && W.crew.form === 'mega' && B.i === W.crew.seat && inp.ai;
    if (!(B.K.dashT > 0)) fly(B, dt, B.F.cruise * B.boost * (holding ? 0.45 : 1), 30, 1.5); else { B.course.copy(fwdOf(B)); B.vel.copy(B.course).multiplyScalar(B.speed); B.pos.addScaledVector(B.course, B.speed * dt); }
    if (W.aiMoves !== false || !inp.ai) megaMove(W, B, inp, dt);
    throughRings(W, B, dt);
    contacts(W, B, dt, false);
    B.stats.top = Math.max(B.stats.top, B.speed);
  }

  // ---- the omega form: one body, three seats ----
  function omegaFlight(def) { const ms = def.members.map((n) => MEMBERS[n]); const avg = (k) => ms.reduce((a, m) => a + m[k], 0) / ms.length; return { cruise: avg('cruise') * def.omega.speed, turn: avg('turn') * def.omega.turn, roll: avg('roll') * def.omega.turn }; }
  function beginGather(W) { const c = W.crew; if (c.form !== 'mega' || c.meter < W.m.combineAt) return false; c.form = 'gather'; c.gather = 0; return true; }
  function gatherPoint(W) { const c = W.crew, lead = W.bodies[Math.min(c.seat, 2)]; return { pos: lead.pos, quat: lead.quat }; }
  function slotOf(W, i) { const g = gatherPoint(W), fwd = fwdOf(g), right = rightOf(g), up = upOf(g); return i === 0 ? g.pos.clone() : i === 1 ? g.pos.clone().addScaledVector(up, 9).addScaledVector(fwd, -4) : g.pos.clone().addScaledVector(up, -8).addScaledVector(fwd, -6); }
  function dockProgress(W) { let far = 0; for (let i = 0; i < 3; i++) far = Math.max(far, W.bodies[i].pos.distanceTo(slotOf(W, i))); return far; }
  function completeCombine(W) {
    const c = W.crew, lead = W.bodies[0], g = gatherPoint(W), def = W.crewDef;
    const O = P.makeBody(W, 'Omega', { cruise: 0, turn: 0, roll: 0, color: 0xffffff, body: 'omega', move: { kind: 'none' } }, g.pos, g.quat, 3);
    O.F = omegaFlight(def); O.omega = true; O.speed = Math.max(...W.bodies.map((B) => B.speed)); O.course.copy(fwdOf(O)); O.K.cool = 0;
    c.omega = O; c.form = 'omega'; c.dock = 0; c.combines++; c.weapon = { cool: 0, charge: 0, fireT: 0 }; c.aim = { yaw: 0, pitch: 0 }; c.shield = false;
    W.events.push(def.name + ': OMEGA FORM');
  }
  function splitNow(W, why) {
    const c = W.crew, O = c.omega; if (!O) return;
    const fwd = fwdOf(O), right = rightOf(O), up = upOf(O);
    W.bodies.forEach((B, i) => { B.pos.copy(O.pos).addScaledVector(i === 0 ? up : i === 1 ? right : right.clone().negate(), i === 0 ? 0 : 16).addScaledVector(fwd, i === 0 ? 8 : -6); B.quat.copy(O.quat); B.course.copy(fwd); B.speed = O.speed * 0.9; B.vel.copy(B.course).multiplyScalar(B.speed); B.boost = 1; B.boostT = 0; B.K.cool = 0; });
    c.omega = null; c.form = 'mega'; c.split = 0; c.shield = false; W.events.push(def(W).name + ': split' + (why ? ' (' + why + ')' : ''));
  }
  const def = (W) => W.crewDef;
  function omegaWeapon(W, O, inp, dt) {
    const c = W.crew, w = def(W).weapon, wp = c.weapon, pw = W.m.weaponPower; wp.cool = Math.max(0, wp.cool - dt);
    // the Arms seat aims a turret with the right stick (offsets from the nose); a bounded cone
    c.aim.yaw = clamp(c.aim.yaw + (inp.R ? inp.R.x : 0) * 2.2 * dt, -0.9, 0.9); c.aim.pitch = clamp(c.aim.pitch + (inp.R ? inp.R.y : 0) * 2.2 * dt, -0.7, 0.7);
    const aimDir = fwdOf(O).applyAxisAngle(upOf(O), -c.aim.yaw).applyAxisAngle(rightOf(O), c.aim.pitch).normalize();
    O.aimDir = aimDir; const rt = inp.rt > 0.5;
    if (w.kind === 'whip') { if (rt && wp.cool <= 0) { const at = O.pos.clone().addScaledVector(aimDir, w.reach * 0.8 * pw); const r = smashNear(W, at, w.reach * 0.55 * pw, 2 / w.hits); wp.cool = w.cool; if (r.n || r.wall) c.uses++; wp.fireT = W.t; W.shots.push({ a: O.pos.clone(), b: at, t: 0.15, wide: true }); } }
    else if (w.kind === 'cannon') { if (rt) wp.charge = Math.min(1, wp.charge + dt / w.charge); else if (wp.charge > 0.1) { W.shots.push({ shot: true, pos: O.pos.clone().addScaledVector(aimDir, 14), vel: aimDir.clone().multiplyScalar(w.speed), blast: w.blast * (0.5 + wp.charge) * pw, life: 3, owner: null, wallHits: wp.charge >= 0.95 ? 1 : wp.charge > 0.5 ? 0.5 : 0 }); c.uses++; wp.fireT = W.t; wp.charge = 0; } else wp.charge = 0; }
    else if (w.kind === 'guns') { if (rt) { wp.gunT = (wp.gunT || 0) - dt; if (wp.gunT <= 0) { wp.gunT = w.rate; let best = null, bd = w.range * pw, wall = null; for (const p of W.grid.near(O.pos.clone().addScaledVector(aimDir, bd / 2), bd / 2 + 20, tmpArr)) { if (p.kind === 'track') continue; const d = p.pos.clone().sub(O.pos), along = d.dot(aimDir); if (along < 6 || along > bd) continue; if (Math.sqrt(Math.max(0, d.lengthSq() - along * along)) > (p.kind === 'wall' ? 12 : 7)) continue; if (along < bd) { bd = along; best = p; } } if (best) { W.shots.push({ a: O.pos.clone(), b: best.pos.clone(), t: 0.08 }); c.uses++; wp.fireT = W.t; if (best.kind === 'wall') hitWall(W, best.wallRef, w.rate / w.wallSeconds); else removePrism(W, best); } } } }
  }
  function omegaCore(W, O, inp, dt) {
    const c = W.crew; c.shield = inp.lt > 0.3;
    if (inp.rt > 0.5 && c.meter >= W.m.finisherAt && W.t - c.finisherT > 1) {   // the finisher: a cone ahead, everything in it, then the split
      c.finisherT = W.t; const fwd = fwdOf(O), reach = W.m.finisherCone; let n = 0;
      for (const p of W.grid.near(O.pos.clone().addScaledVector(fwd, reach / 2), reach / 2 + 40, tmpArr)) { if (p.kind === 'track') continue; const d = p.pos.clone().sub(O.pos), along = d.dot(fwd); if (along < 0 || along > reach) continue; if (Math.sqrt(Math.max(0, d.lengthSq() - along * along)) > along * 0.45 + 10) continue; if (p.kind === 'wall') { if (!p.wallRef.broken) hitWall(W, p.wallRef, 9); } else { removePrism(W, p); n++; } }
      c.meter = Math.max(0, c.meter - W.m.finisherCost); c.uses++; W.events.push(def(W).name + ': ' + def(W).core.finisher + '! ' + n + ' prisms'); W.finisher = { pos: O.pos.clone(), quat: O.quat.clone(), t: 0.6, reach };
      splitNow(W, 'finisher');
    }
  }
  function stepOmega(W, inputs, dt) {
    const c = W.crew, O = c.omega; O.slow += (1 - O.slow) * (1 - Math.exp(-dt));
    if (O.boostT > 0) O.boostT -= dt; else if (O.boost > 1) O.boost = Math.max(1, O.boost - BASE.boostDecay * dt);
    const helm = inputs[0], arms = inputs[1], core = inputs[2];
    steer(O, helm, dt, 1); fly(O, dt, O.F.cruise * O.boost, 30, 1.2);
    if (helm.lt > 0.3) { c.split += dt; if (c.split >= W.m.splitSeconds) { splitNow(W, 'Helm'); return; } } else c.split = 0;
    omegaWeapon(W, O, arms, dt); if (c.form !== 'omega') return;
    omegaCore(W, O, core, dt); if (c.form !== 'omega') return;
    for (const r of W.rings) if (!r.dead && r.tunnel && O.pos.distanceTo(r.pos) < r.radius + 6 && (r.lastO == null || W.t - r.lastO > 2)) { r.lastO = W.t; O.boost = Math.max(O.boost, r.boost); O.boostT = 2; }
    if (def(W).id === 'tempest') { c.ringT = (c.ringT || 0) + dt; if (c.ringT > 4) { c.ringT = 0; W.rings.push({ tunnel: true, pos: O.pos.clone().addScaledVector(fwdOf(O), -20), quat: O.quat.clone(), radius: 16, boost: 1.5, born: W.t, life: 15 }); } }
    contacts(W, O, dt, W.m.omegaContactPlough > 0);
    c.meter = Math.max(0, c.meter - (W.m.drain + (c.shield ? W.m.shieldDrain : 0)) * dt); W.combinedT += dt;
    O.stats.top = Math.max(O.stats.top, O.speed);
    if (c.meter <= 0) splitNow(W, 'meter empty');
  }
  // ---- one step of the world: inputs is [seat0, seat1, seat2] (mega: one per member; omega: Helm, Arms, Core) ----
  function stepWorld(W, dt, inputs) {
    W.t += dt; const c = W.crew;
    if (c.form === 'omega') { stepOmega(W, inputs, dt); }
    else {
      if (c.form === 'gather') {
        c.gather += dt; const lead = W.bodies[c.seat]; stepMember(W, lead, inputs[c.seat], dt);
        const k = clamp(c.gather / Math.max(0.1, W.m.combineSeconds), 0, 1), e = k * k * (3 - 2 * k);
        W.bodies.forEach((B, i) => { if (i === c.seat) return; const slot = slotOf(W, i); B.pos.lerp(slot, Math.min(1, dt * (2 + 10 * e))); B.quat.slerp(lead.quat, Math.min(1, dt * 4)); B.course.copy(lead.course); B.speed = lead.speed; B.vel.copy(lead.vel); B.K.active = false; B.aiState = 'docking'; });
        if (k >= 1) completeCombine(W);
      } else W.bodies.forEach((B, i) => stepMember(W, B, inputs[i], dt));
      c.meter = Math.min(1, c.meter + W.m.regen * dt);
    }
    stepShots(W, dt);
    let dead = 0; for (const p of W.prisms) { if (p.flash > 0) { p.flash = Math.max(0, p.flash - dt * 3); W.dirty = true; } if (p.dead) dead++; }
    if (dead > 200) { W.prisms = W.prisms.filter((p) => !p.dead); W.dirty = true; }
    if (W.finisher) { W.finisher.t -= dt; if (W.finisher.t <= 0) W.finisher = null; }
    if (!W.over && (W.finishT != null || W.t > BASE.limit)) W.over = true;
  }
  const bodiesNow = (W) => W.crew.form === 'omega' ? [W.crew.omega] : W.bodies;
  Object.assign(P, { sticks, steer, fly, edge, megaMove, stepMember, stepOmega, stepWorld, beginGather, splitNow, slotOf, gatherPoint, dockProgress, omegaFlight, bodiesNow });
})();

// ================= part C: the AI (input-only, D4): members, the three omega seats, the crew's combine policy =================
(function () {
  'use strict';
  const P = window.__omegaParts, { V, BASE, DEG, lerp, clamp, easeInv, tmpArr, fwdOf, upOf, rightOf, nearestIndex, slotOf, bodiesNow } = P;
  const NO_INPUT = () => ({ pad: true, L: { x: 0, y: 0 }, R: { x: 0, y: 0 }, lt: 0, rt: 0, a: false, b: false, ai: true });

  // the sticks that point a body's nose at `aim` (the recipe engine's aimSticks: steering first, roll to level)
  function aimSticks(B, aim) {
    const fwd = fwdOf(B), up = upOf(B), right = rightOf(B);
    const dir = aim.clone().sub(B.pos).normalize(), x = dir.dot(right), y = dir.dot(up), z = dir.dot(fwd);
    const yawErr = Math.atan2(x, z), pitchErr = Math.atan2(y, Math.max(0.05, Math.hypot(x, z)) * (z < 0 ? -1 : 1));
    const yawCmd = clamp(yawErr * 2.4, -1, 1), pitchCmd = clamp(pitchErr * 2.4, -1, 1), rollCmd = clamp(-right.y * 2, -1, 1);
    const sx = easeInv(yawCmd), sy = easeInv(pitchCmd), dy = easeInv(rollCmd), dx = Math.min(2, 2 - Math.abs(sx));
    return { L: { x: (sx - dx) / 2, y: clamp((sy - dy) / 2, -1, 1) }, R: { x: (sx + dx) / 2, y: clamp((sy + dy) / 2, -1, 1) }, err: Math.acos(clamp(z, -1, 1)) / DEG };
  }
  // the handicap: SkimRaceHandicap's two mistakes applied to what a body BELIEVES about its crystal (D24)
  function believe(W, B, c) {
    if (!c) return null;
    const h = B.hc; if (!h || (h.level.reaction <= 0 && h.level.mistake <= 0)) return c.pos;
    if (h.tracking !== c.i) {
      h.tracking = c.i; h.noticeAt = W.t + h.level.reaction * lerp(0.5, 1.5, h.rng()); h.misjudged = h.rng() < h.level.mistake;
      if (h.misjudged) { B.mistakes = (B.mistakes || 0) + 1; const near = W.track.line[nearestIndex(W.track, c.pos, 0, true)]; const away = c.pos.clone().sub(near); if (away.lengthSq() < 1) away.set(0, 1, 0); h.off.copy(away.normalize().multiplyScalar(2 * BASE.captureReach + 4)); h.until = W.t + 6; }
    }
    h.unnoticed = W.t < h.noticeAt; if (h.unnoticed) return null;
    if (h.misjudged) { const fake = c.pos.clone().add(h.off), toFake = fake.clone().sub(B.pos), fwd = fwdOf(B); if (W.t > h.until || toFake.length() < 12 || toFake.dot(fwd) < 0) h.misjudged = false; else return fake; }
    return c.pos;
  }
  // which lit crystal each member goes for: the nearest one nobody else in the crew has claimed (a locked one only when it is open)
  function assignTargets(W) {
    const open = W.lit.filter((c) => !c.taken && !(c.locked && !c.wall.broken)), taken = new Set();
    for (const B of W.bodies) B.target = -1;
    const order = W.bodies.slice().sort((a, b) => a.i - b.i);
    for (const B of order) { let best = null, bd = Infinity; for (const c of open) { if (taken.has(c.i)) continue; const d = c.pos.distanceToSquared(B.pos); if (d < bd) { bd = d; best = c; } } if (best) { B.target = best.i; taken.add(best.i); } }
    for (const B of order) if (B.target < 0 && open.length) { let best = null, bd = Infinity; for (const c of open) { const d = c.pos.distanceToSquared(B.pos); if (d < bd) { bd = d; best = c; } } B.target = best.i; }
    const locked = W.lit.find((c) => !c.taken && c.locked && !c.wall.broken);
    for (const B of order) if (B.target < 0 && locked) { B.target = locked.i; B.toWall = true; } else if (B.target >= 0) B.toWall = false;
  }
  const lookAhead = (W, B, lookU) => { const n = W.track.line.length; B.hint = nearestIndex(W.track, B.pos, B.hint || 0, false); const look = clamp(Math.round((24 + B.speed * 0.5 + (lookU || 0)) / BASE.blockSpacing), 2, 16); return W.track.line[(B.hint + look) % n].clone().addScaledVector(new V(0, 1, 0), 11); };
  function massAhead(W, B, range) { const fwd = fwdOf(B); let n = 0; for (const p of W.grid.near(B.pos.clone().addScaledVector(fwd, range / 2), range / 2 + 10, tmpArr)) { if (p.kind !== 'mass' && p.kind !== 'rail') continue; const d = p.pos.clone().sub(B.pos), along = d.dot(fwd); if (along < 4 || along > range) continue; if (Math.sqrt(Math.max(0, d.lengthSq() - along * along)) < 14) n++; } return n; }
  function memberAI(W, B) {
    const c = W.crew, inp = NO_INPUT();
    if (c.form === 'gather') {   // fly to your slot round the one who called the gather
      const slot = slotOf(W, B.i), d = slot.distanceTo(B.pos);
      const s = aimSticks(B, d > 6 ? slot : slot.clone().addScaledVector(fwdOf(P.gatherPoint(W)), 40)); inp.L = s.L; inp.R = s.R; inp.gatherAI = true; B.aim = slot; B.aimWhy = 'gather'; B.aiState = 'gathering'; return inp;
    }
    if (c.calling && B.i !== c.seat) {   // called to gather: fly to the caller (a little behind it)
      const L = W.bodies[c.seat], at = L.pos.clone().addScaledVector(fwdOf(L), -12); const s = aimSticks(B, at); inp.L = s.L; inp.R = s.R; B.aim = at; B.aimWhy = 'gather'; B.aiState = 'called to gather'; B.believed = null; return inp;
    }
    const target = W.crystals[B.target] || null; let b = believe(W, B, target); B.believed = b;
    if (b && B.toWall && target && target.wall) { const ap = target.wall.centre.clone().addScaledVector(fwdOf({ quat: target.wall.quat }), -90); b = B.pos.distanceTo(ap) < 40 ? ap.clone().addScaledVector(rightOf(B), 80) : ap; }
    let aim = lookAhead(W, B), why = 'line';
    if (b) { aim = b.clone(); why = B.toWall ? 'to the wall' : 'crystal'; }
    const s = aimSticks(B, aim); inp.L = s.L; inp.R = s.R; B.aim = aim; B.aimWhy = why;
    if (W.aiMoves !== false) {   // the member's RT move when it helps: smashers on mass ahead, boosts when the nose is on the line
      const mv = B.m.move, h = B.hc, late = h ? h.level.press : 0; let want = false;
      if (mv.kind === 'whip' || mv.kind === 'ram' || mv.kind === 'guns' || mv.kind === 'blast') want = massAhead(W, B, mv.kind === 'guns' ? 200 : 40) > 0;
      else if (mv.kind === 'sling' || mv.kind === 'drift') want = s.err < 12 && B.K.cool <= 0 && (!b || b.distanceTo(B.pos) > 120);
      else if (mv.kind === 'cannon') want = massAhead(W, B, 160) > 2 ? (B.K.charge || 0) < 0.9 : false;
      else if (mv.kind === 'tunnel') want = B.K.cool <= 0 && W.bodies.some((X) => X !== B && X.pos.distanceTo(B.pos) < 60 && X.pos.clone().sub(B.pos).dot(fwdOf(B)) < 0);
      else if (mv.kind === 'gate') want = B.K.cool <= 0 && !!b && b.distanceTo(B.pos) > 300 && s.err < 10;
      if (want && !B.K._wantOn && h) h.pressAt = W.t + late * lerp(0.5, 1.5, h.rng()); B.K._wantOn = want;
      if (want && (!late || W.t >= (h ? h.pressAt : 0))) inp.rt = 1;
    }
    B.aiState = h(B) ? (B.hc.unnoticed ? 'not noticed' : B.hc.misjudged ? 'misjudged' : B.K.active ? B.m.move.name : why) : why;
    return inp;
  }
  const h = (B) => !!B.hc;
  function nearestWall(W, pos, range) { let best = null, bd = range; for (const w of W.walls) { if (w.broken) continue; const d = w.centre.distanceTo(pos); if (d < bd) { bd = d; best = w; } } return best; }
  function omegaAI(W, seat) {
    const c = W.crew, O = c.omega, inp = NO_INPUT(); if (!O) return inp;
    const nextLit = W.lit.find((x) => !x.taken) || null;
    if (seat === 0) {   // Helm: fly for the lit crystal (a locked one too: the weapon opens it), the course line otherwise
      let aim = lookAhead(W, O), why = 'line';
      const wall = nearestWall(W, O.pos, 700);
      const lockedLit = W.lit.find((x) => !x.taken && x.locked && !x.wall.broken), tgt = lockedLit || nextLit;
      const goal = tgt ? (tgt.locked && !tgt.wall.broken ? (O.pos.distanceTo(tgt.wall.centre) > 130 ? tgt.wall.centre.clone().addScaledVector(fwdOf({ quat: tgt.wall.quat }), -90) : tgt.wall.centre.clone()) : tgt.pos) : null;
      if (goal) { aim = goal; why = tgt.locked && !tgt.wall.broken ? 'to the wall' : 'crystal'; }
      const s = aimSticks(O, aim); inp.L = s.L; inp.R = s.R; O.aim = aim; O.aimWhy = why; O.aiState = why;
      if (W.policy === 'crew' && c.meter > 0.15 && !W.lit.some((x) => !x.taken && x.locked && !x.wall.broken) && W.t - (c.formT || 0) > 2) inp.lt = 1;   // nothing locked ahead: split
      if (wall) O.aimWall = wall;
    } else if (seat === 1) {   // Arms: turn the turret onto the nearest wall in range, else the mass ahead, and fire
      const wall = nearestWall(W, O.pos, 320); let at = null;
      if (wall) at = wall.centre; else if (massAhead(W, O, 120) > 0) at = O.pos.clone().addScaledVector(fwdOf(O), 60);
      if (at) { const dir = at.clone().sub(O.pos).normalize(), fwd = fwdOf(O), right = rightOf(O), up = upOf(O); const wantYaw = Math.atan2(dir.dot(right), Math.max(0.05, dir.dot(fwd))), wantPitch = Math.asin(clamp(dir.dot(up), -1, 1)); inp.R = { x: clamp((wantYaw - c.aim.yaw) * 3, -1, 1), y: clamp((wantPitch - c.aim.pitch) * 3, -1, 1) }; const err = Math.abs(wantYaw - c.aim.yaw) + Math.abs(wantPitch - c.aim.pitch); const w = W.crewDef.weapon; if (w.kind === 'cannon') inp.rt = c.weapon.charge < 0.97 ? 1 : (err > 0.12 && c.weapon.charge < 1 ? 1 : 0); else inp.rt = err < 0.25 ? 1 : 0; }
    } else {   // Core: the shield when mass is about to hit, the finisher when the meter is there and a wall is in the cone
      const wall = nearestWall(W, O.pos, W.m.finisherCone * 0.9);
      inp.lt = massAhead(W, O, 45) > 2 && c.meter > 0.3 ? 1 : 0;
      if (wall && c.meter >= W.m.finisherAt) { const d = wall.centre.clone().sub(O.pos), along = d.dot(fwdOf(O)); if (along > 20 && Math.sqrt(Math.max(0, d.lengthSq() - along * along)) < along * 0.4) inp.rt = 1; }
    }
    return inp;
  }
  // the crew's combine policy, for the seats the AI holds: 'crew' decides, 'never' stays apart, 'always' combines at once and never splits
  function wantCombine(W, humanSeat) {
    const c = W.crew; if (c.form !== 'mega' || W.policy === 'never' || c.meter < W.m.combineAt) return false;
    if (W.policy === 'always') return true;
    if (humanSeat >= 0 && W.aiCombine === false) return false;
    const lockedC = W.lit.find((x) => !x.taken && x.locked && !x.wall.broken); if (!lockedC) return false;
    const unlockedLeft = W.lit.some((x) => !x.taken && !(x.locked && !x.wall.broken));
    const nearLocked = Math.min(...W.bodies.map((B) => B.pos.distanceTo(lockedC.wall.centre))) < 320;
    return nearLocked || !unlockedLeft;
  }
  function crewPolicy(W, inputs, humanSeat) {
    const c = W.crew; if (c.form !== 'mega' || !c.calling) return;
    const near = W.bodies.every((B) => B.pos.distanceTo(W.bodies[c.seat].pos) < W.m.gatherRange);
    if (near && P.beginGather(W)) c.formT = W.t;
  }
  // headless: the whole crew on AI, one policy, until it finishes or the limit
  function runHeadless(course, crewId, level, seed, o) {
    o = o || {};
    const W = P.makeWorld(course, crewId, [level, level, level], seed, { massDensity: o.massDensity, policy: o.policy || 'crew', lv: o.lv, nobody: true, meter: o.meter });
    W.aiMoves = o.aiMoves !== false; W.aiCombine = true; W.watch = -1;
    const dt = 1 / 30; let n = 0, sp = 0;
    if (W.policy === 'always') { P.beginGather(W); }
    while (!W.over) { stepAll(W, dt, -1, null); n++; const bs = bodiesNow(W); sp += bs.reduce((a, B) => a + B.speed, 0) / bs.length; }
    return { finish: W.finishT, collected: W.collected, walls: W.wallsBroken, combinedShare: +(W.combinedT / Math.max(1e-6, W.t)).toFixed(3), combines: W.crew.combines, avgSpeed: +(sp / n).toFixed(1), uses: W.crew.uses };
  }
  // one step with the AI filling every seat the human does not hold (humanSeat -1 = nobody; humanInput = the human's input)
  function stepAll(W, dt, humanSeat, humanInput) {
    const c = W.crew; let inputs;
    if (c.form === 'omega') inputs = [0, 1, 2].map((s) => s === humanSeat && humanInput ? humanInput : omegaAI(W, s));
    else {
      assignTargets(W);
      const humanCalls = humanSeat >= 0 && humanInput && humanInput.lt > 0.3;
      c.calling = c.form === 'mega' && (humanCalls || wantCombine(W, humanSeat));
      inputs = [0, 1, 2].map((i) => i === humanSeat && humanInput ? humanInput : memberAI(W, W.bodies[i]));
      crewPolicy(W, inputs, humanSeat);
      if (c.form === 'gather') for (let i = 0; i < 3; i++) if (i !== humanSeat) inputs[i] = memberAI(W, W.bodies[i]);
    }
    P.stepWorld(W, dt, inputs);
  }
  Object.assign(P, { aimSticks, believe, assignTargets, memberAI, omegaAI, crewPolicy, runHeadless, stepAll, NO_INPUT, nearestWall });
})();

// ================= part D: rendering (the members' bodies, the omega, the walls, the marks) and the cameras (D16) =================
(function () {
  'use strict';
  const P = window.__omegaParts, { T, V, Q, BASE, S, MEMBERS, SEATS, clamp, lerp, fwdOf, upOf, rightOf, bodiesNow, slotOf, $ } = P;
  const canvas = $('c');
  const renderer = VesselStudioTheme.renderer(T, { canvas, antialias: true });
  renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  const scene = new T.Scene(), cam = new T.PerspectiveCamera(70, 16 / 10, 0.5, 9000);
  const ENV = VesselStudioLook.install(scene, { field: 1800, prismScale: 2.4 });
  const mark = VesselStudioLook.marker($('viewport'));
  const MAXP = 12000;
  const prismMesh = new T.InstancedMesh(new T.BoxGeometry(1, 1, 1), new T.MeshStandardMaterial({ roughness: 0.5, metalness: 0.05, emissive: 0x0b1b4a, flatShading: true }), MAXP);
  prismMesh.instanceMatrix.setUsage(T.DynamicDrawUsage); scene.add(prismMesh);
  const shieldMesh = new T.InstancedMesh(new T.BoxGeometry(1, 1, 1), new T.MeshBasicMaterial({ color: 0xbfe6ff, wireframe: true, transparent: true, opacity: 0.5 }), MAXP);
  shieldMesh.instanceMatrix.setUsage(T.DynamicDrawUsage); scene.add(shieldMesh);
  const COL = { track: new T.Color(0x3a4380), mass: new T.Color(0x56608f), wall: new T.Color(0x8fa6ff), warden: new T.Color(0xc48bff), bulwark: new T.Color(0xff6a4d), tempest: new T.Color(0x13fff2) };
  const WHITE = new T.Color(0xffffff), m4 = new T.Matrix4(), tmpC = new T.Color(), sc = new V();
  function drawPrisms(W) {
    let n = 0, ns = 0;
    for (const p of W.prisms) {
      if (p.dead || n >= MAXP) continue;
      sc.set(p.scale[0], p.scale[1], p.scale[2]); m4.compose(p.pos, p.quat, sc); prismMesh.setMatrixAt(n, m4);
      tmpC.copy(COL[p.domain] || COL.track); if (p.flash > 0) tmpC.lerp(WHITE, p.flash); prismMesh.setColorAt(n, tmpC); n++;
      if (p.shield && ns < MAXP) { sc.multiplyScalar(1.25); m4.compose(p.pos, p.quat, sc); shieldMesh.setMatrixAt(ns++, m4); }
    }
    prismMesh.count = n; shieldMesh.count = ns; prismMesh.instanceMatrix.needsUpdate = true; shieldMesh.instanceMatrix.needsUpdate = true;
    if (prismMesh.instanceColor) prismMesh.instanceColor.needsUpdate = true; W.dirty = false;
  }
  // ---- the members' bodies: a silhouette per vessel (lab stand-ins for the game's models) ----
  const mat = (color) => new T.MeshStandardMaterial({ color, roughness: 0.4, metalness: 0.25, emissive: new T.Color(color).multiplyScalar(0.2), flatShading: true });
  const box = (m, w, h, d, x, y, z) => { const b = new T.Mesh(new T.BoxGeometry(w, h, d), m); b.position.set(x, y, z); return b; };
  function memberHull(name) {
    const mb = MEMBERS[name], m = mat(mb.color), g = new T.Group();
    switch (mb.body) {
      case 'plates': { const b = new T.Mesh(T.CapsuleGeometry ? new T.CapsuleGeometry(2.4, 7, 3, 8) : new T.CylinderGeometry(2.4, 2.4, 9, 8), m); b.rotation.x = Math.PI / 2; g.add(b); g.add(box(m, 7, 0.5, 3, 0, 1.6, 1)); g.add(box(m, 7, 0.5, 3, 0, -1.6, 1)); break; }
      case 'ball': { g.add(box(m, 2.4, 1.6, 7, 0, 0, 0)); const ball = new T.Mesh(new T.SphereGeometry(2.2, 10, 8), m); ball.name = 'ball'; g.add(ball); const ln = new T.Line(new T.BufferGeometry().setFromPoints([new V(), new V(0, 0, -20)]), new T.LineBasicMaterial({ color: mb.color })); ln.name = 'chain'; g.add(ln); break; }
      case 'wings': g.add(box(m, 1.6, 0.8, 7, 0, 0, 0)); g.add(box(m, 14, 0.25, 5, 0, 0, 0.5)); break;
      case 'wedge': { const c = new T.Mesh(new T.ConeGeometry(3, 10, 4), m); c.rotation.x = -Math.PI / 2; c.rotation.z = Math.PI / 4; g.add(c); g.add(box(m, 0.8, 2.6, 2, 0, 2.2, -1)); break; }
      case 'box': g.add(box(m, 5, 3, 8, 0, 0, 0)); g.add(box(m, 1.2, 1.2, 9, 1.8, 1.4, -3)); break;
      case 'disc': { const d = new T.Mesh(new T.CylinderGeometry(4.2, 4.2, 1.4, 10), m); g.add(d); g.add(box(m, 1.6, 1.2, 5, 0, 0.8, -2)); break; }
      case 'cone': { const c = new T.Mesh(new T.ConeGeometry(2.2, 9, 6), m); c.rotation.x = -Math.PI / 2; g.add(c); g.add(box(m, 9, 0.4, 2.6, 0, 0, 2)); break; }
      case 'fighter': { const c = new T.Mesh(new T.ConeGeometry(1.6, 10, 5), m); c.rotation.x = -Math.PI / 2; g.add(c); g.add(box(m, 12, 0.3, 3.5, 0, -0.3, 1.5)); g.add(box(m, 0.3, 3, 2.5, 0, 1.6, 3.5)); break; }
      case 'delta': { const s = new T.Shape(); s.moveTo(0, -6); s.lineTo(7, 4); s.lineTo(0, 2); s.lineTo(-7, 4); s.closePath(); const d = new T.Mesh(new T.ExtrudeGeometry(s, { depth: 0.8, bevelEnabled: false }), m); d.rotation.x = -Math.PI / 2; d.position.y = -0.4; g.add(d); break; }
    }
    return g;
  }
  const view = { hulls: [], crystals: [], lines: [], omega: null, struts: null, aimLine: null, walls: [], rings: [], shots: null, fin: null };
  function mountWorld(W) {
    { const ln = W.track.line, lo = new V(Infinity, Infinity, Infinity), hi = new V(-Infinity, -Infinity, -Infinity); for (const q of ln) { lo.min(q); hi.max(q); } const ctr = lo.clone().add(hi).multiplyScalar(0.5); let rad = 0; for (const q of ln) rad = Math.max(rad, Math.hypot(q.x - ctr.x, q.z - ctr.z)); ENV.placeField(ctr, (rad + 200) / 0.75, Math.max(160, (hi.y - lo.y) * 0.5 + 140)); }
    for (const o of [...view.hulls, ...view.crystals, ...view.lines, ...view.rings, view.struts, view.aimLine, view.shots, view.fin]) if (o) scene.remove(o);
    view.hulls = []; view.crystals = []; view.lines = []; view.rings = [];
    for (const B of W.bodies) {
      const h = memberHull(B.name); scene.add(h); view.hulls.push(h);
      const lg = new T.BufferGeometry().setFromPoints([new V(), new V(), new V(), new V()]);
      const ln = new T.LineSegments(lg, new T.LineBasicMaterial({ color: B.m.color, transparent: true, opacity: 0.85 })); ln.frustumCulled = false; scene.add(ln); view.lines.push(ln);
    }
    for (let i = 0; i < 3; i++) { const c = VesselStudioLook.crystal(0xffffff, 7); scene.add(c); view.crystals.push(c); }
    view.struts = new T.Group(); const sm = mat(0xffffff); view.struts.add(box(sm, 1.2, 20, 1.2, 0, 0, -2)); view.struts.add(box(sm, 14, 1, 1, 0, 6, -2)); view.struts.visible = false; scene.add(view.struts);
    view.aimLine = new T.Line(new T.BufferGeometry().setFromPoints([new V(), new V()]), new T.LineBasicMaterial({ color: 0xff5ea8, transparent: true, opacity: 0.6 })); view.aimLine.frustumCulled = false; view.aimLine.visible = false; scene.add(view.aimLine);
    view.shots = new T.LineSegments(new T.BufferGeometry().setFromPoints(Array.from({ length: 120 }, () => new V())), new T.LineBasicMaterial({ color: 0xffe28a, transparent: true, opacity: 0.9 })); view.shots.frustumCulled = false; scene.add(view.shots);
    view.fin = new T.Mesh(new T.ConeGeometry(1, 1, 24, 1, true), new T.MeshBasicMaterial({ color: 0xff5ea8, transparent: true, opacity: 0.25, side: T.DoubleSide, depthWrite: false })); view.fin.visible = false; scene.add(view.fin);
  }
  const ringGeo = new T.TorusGeometry(1, 0.08, 6, 24), gateGeo = new T.TorusGeometry(1, 0.1, 6, 20);
  function ringMesh(r) { const m = new T.Mesh(r.tunnel ? ringGeo : gateGeo, new T.MeshBasicMaterial({ color: r.tunnel ? 0x6680ff : 0xffd0f0, transparent: true, opacity: 0.7 })); m.userData.r = r; scene.add(m); view.rings.push(m); return m; }
  function drawWorld(W, t) {
    if (W.dirty) drawPrisms(W);
    const c = W.crew, O = c.omega;
    W.bodies.forEach((B, i) => {
      const h = view.hulls[i];
      if (O) { const q = O.quat, slot = slotOf(W, i), s = i === 0 ? O.pos : slot; h.position.copy(O.pos).add(s.clone().sub(P.gatherPoint(W).pos)); h.quaternion.copy(q); h.scale.setScalar(1.25); }
      else { h.position.copy(B.pos); h.quaternion.copy(B.quat); h.scale.setScalar(1); }
      h.visible = true;
      const ball = h.getObjectByName('ball'), chain = h.getObjectByName('chain');
      if (ball && chain) { const K = B.K, local = K.ball && !O ? h.worldToLocal(K.ball.clone()) : new V(0, -3, 18 * 0.4); ball.position.copy(local); chain.geometry.setFromPoints([new V(), local.clone()]); }
      const ln = view.lines[i], show = S.thinking && !O && !!B.aim && B.lastInp && B.lastInp.ai && W.finishT == null; ln.visible = show;
      if (show) { const a = ln.geometry.attributes.position, b = B.believed || B.pos; a.setXYZ(0, B.pos.x, B.pos.y, B.pos.z); a.setXYZ(1, B.aim.x, B.aim.y, B.aim.z); a.setXYZ(2, B.pos.x, B.pos.y + 3, B.pos.z); a.setXYZ(3, b.x, b.y + 3, b.z); a.needsUpdate = true; ln.material.color.set(B.aimWhy === 'gather' ? 0xb98bff : B.hc && B.hc.misjudged ? 0xff5c6c : B.hc && B.hc.unnoticed ? 0xffb454 : 0x6fe39a); }
    });
    view.struts.visible = !!O; if (O) { view.struts.position.copy(O.pos); view.struts.quaternion.copy(O.quat); }
    view.aimLine.visible = !!O && !!O.aimDir; if (O && O.aimDir) { const w = W.crewDef.weapon, reach = (w.reach || w.range || 200) * W.m.weaponPower; view.aimLine.geometry.setFromPoints([O.pos.clone(), O.pos.clone().addScaledVector(O.aimDir, reach)]); }
    W.lit.forEach((cr, i) => { const m = view.crystals[i]; m.visible = !cr.taken; m.position.copy(cr.pos); VesselStudioLook.spinCrystal(m, t + i); m.traverse((o) => { if (o.material && o.material.color) o.material.color.set(cr.locked && !cr.wall.broken ? 0x8fa6ff : 0xffffff); }); });
    for (let i = W.lit.length; i < 3; i++) view.crystals[i].visible = false;
    // rings and gates
    for (const m of view.rings) m.visible = false;
    let k = 0; for (const r of W.rings) { if (r.dead) continue; if (r.gate) { for (const g of [r.a, r.b]) { const m = view.rings[k] || ringMesh(r); k++; m.visible = true; m.position.copy(g.pos); m.quaternion.copy(g.quat); m.scale.setScalar(9); } } else { const m = view.rings[k] || ringMesh(r); k++; m.visible = true; m.position.copy(r.pos); m.quaternion.copy(r.quat); m.scale.setScalar(r.radius); } }
    // shots and beams
    const sp = view.shots.geometry.attributes.position; let n = 0;
    for (const s of W.shots) { if (s.dead || n >= 118) continue; if (s.shot) { sp.setXYZ(n++, s.pos.x, s.pos.y, s.pos.z); const q = s.pos.clone().addScaledVector(s.vel, -0.04); sp.setXYZ(n++, q.x, q.y, q.z); } else { sp.setXYZ(n++, s.a.x, s.a.y, s.a.z); sp.setXYZ(n++, s.b.x, s.b.y, s.b.z); } }
    view.shots.geometry.setDrawRange(0, n); sp.needsUpdate = true;
    view.fin.visible = !!W.finisher; if (W.finisher) { const f = W.finisher, r = f.reach; view.fin.scale.set(r * 0.45, r, r * 0.45); view.fin.quaternion.copy(f.quat); view.fin.quaternion.multiply(new Q().setFromAxisAngle(new V(1, 0, 0), Math.PI / 2)); view.fin.position.copy(f.pos).addScaledVector(fwdOf(f), r / 2); view.fin.material.opacity = 0.35 * f.t / 0.6; }
  }
  // ---- cameras: Chase (the body you fly), Follow (the viewed body, wide), Free (orbit) ----
  const camPos = new V(), camLook = new V(), free = { yaw: 2.6, pitch: 0.35, dist: 110 };
  function flown(W) { const c = W.crew; return c.form === 'omega' ? c.omega : W.bodies[Math.min(c.seat, 2)]; }
  function viewedBody(W) { const c = W.crew; if (c.form === 'omega') return c.omega; return W.bodies[Math.min(S.view, 2)] || W.bodies[0]; }
  function placeCamera(W, dt, snap, keys) {
    let tag; const B = S.cam === 'Chase' ? flown(W) : viewedBody(W), name = B.omega ? W.crewDef.name + ' omega' : B.name;
    if (S.cam === 'Free') {
      const k = (c) => keys.has(c) ? 1 : 0;
      free.yaw += (k('KeyJ') - k('KeyL')) * 1.4 * dt; free.pitch = clamp(free.pitch + (k('KeyI') - k('KeyK')) * 1.1 * dt, -1.3, 1.3); free.dist = clamp(free.dist * (1 + (k('KeyO') - k('KeyU')) * 1.2 * dt), 25, 3000);
      const want = new V(Math.sin(free.yaw) * Math.cos(free.pitch), Math.sin(free.pitch), Math.cos(free.yaw) * Math.cos(free.pitch)).multiplyScalar(free.dist).add(B.pos);
      cam.position.lerp(want, snap ? 1 : 1 - Math.exp(-12 * dt)); cam.up.set(0, 1, 0); cam.lookAt(B.pos); tag = 'Free: orbiting ' + name + ' (drag, wheel)';
    } else {
      const fwd = fwdOf(B), up = upOf(B), behind = B.course.clone(); if (behind.lengthSq() < 1e-4) behind.copy(fwd);
      const wide = S.cam === 'Follow', big = B.omega ? 1.8 : 1;
      const wantOff = new V().addScaledVector(behind, -((wide ? 40 : 26) * big) - B.speed * 0.03).addScaledVector(wide ? new V(0, 1, 0) : up, (wide ? 16 : 9) * big), lookOff = new V().addScaledVector(fwd, 30).addScaledVector(up, 2);
      const k = snap ? 1 : 1 - Math.exp(-6 * dt); camPos.lerp(wantOff, k); camLook.lerp(lookOff, k);
      cam.position.copy(B.pos).add(camPos); cam.up.copy(wide ? new V(0, 1, 0) : up); cam.lookAt(B.pos.clone().add(camLook));
      tag = (wide ? 'Following ' : 'Chase: ') + name;
    }
    $('camTag').textContent = tag + ' \u00b7 ' + S.speed + '\u00d7';
  }
  let lastW = 0, lastH = 0;
  function resize() { const w = canvas.clientWidth || 960, h = canvas.clientHeight || Math.round(w * 10 / 16); if (w === lastW && h === lastH) return; lastW = w; lastH = h; renderer.setSize(w, h, false); cam.aspect = w / h; cam.updateProjectionMatrix(); }
  window.addEventListener('resize', resize);
  Object.assign(P, { renderer, scene, cam, ENV, mark, canvas, mountWorld, drawWorld, placeCamera, free, flown, viewedBody, resize, forceResize: () => { lastW = lastH = 0; resize(); } });
})();

// ================= part E: input (pad, keys, phone: one input object), the run, the panels, the HUD, the loop, the hooks =================
(function () {
  'use strict';
  const P = window.__omegaParts, { T, V, BASE, COURSE_INFO, DIFF, S, LS, save, par, mults, styleOf, MEMBERS, CREWS, CREW_BY, DEFAULTS, SPEC, ELEMENTS, STYLES, ELS, EL_COLOR, SEATS, clamp, lerp, elx, $, bodiesNow, flown, canvas, renderer, scene, cam, ENV, mark } = P;
  const fmt = (x) => (Math.abs(x) >= 100 ? x.toFixed(0) : Math.abs(x) >= 10 ? x.toFixed(1) : x.toFixed(2)).replace(/\.0+$/, '');
  const crewDef = () => CREW_BY[S.crew];
  document.documentElement.style.setProperty('--accent-vessel', crewDef().accent);

  // ---- input ----
  const keys = new Set(); let armed = false, swapQueued = 0;
  const isForm = (t) => t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || t.isContentEditable);
  const GAME_KEYS = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'KeyQ', 'KeyE', 'KeyF', 'KeyG', 'KeyH', 'KeyR', 'KeyP', 'KeyC', 'KeyI', 'KeyJ', 'KeyK', 'KeyL', 'KeyU', 'KeyO', 'Space', 'Enter', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown']);
  window.addEventListener('keydown', (e) => {
    if (isForm(e.target) || !armed) return;
    if (GAME_KEYS.has(e.code)) e.preventDefault(); if (e.repeat) return; keys.add(e.code); humanTouched();
    if (e.code === 'Enter') startRun(); if (e.code === 'KeyR') stopRun(); if (e.code === 'KeyP') togglePause(); if (e.code === 'KeyC') cycleCamera();
    if (e.code === 'ArrowLeft') swapQueued = -1; if (e.code === 'ArrowRight') swapQueued = 1;
  });
  window.addEventListener('keyup', (e) => keys.delete(e.code)); window.addEventListener('blur', () => keys.clear());
  document.addEventListener('pointerdown', (e) => { armed = $('viewport').contains(e.target); }); canvas.addEventListener('focus', () => { armed = true; });
  let drag = null;
  canvas.addEventListener('pointerdown', (e) => { if (S.cam === 'Free' && !playMode) drag = { x: e.clientX, y: e.clientY }; });
  canvas.addEventListener('wheel', (e) => { if (S.cam !== 'Free') return; e.preventDefault(); P.free.dist = clamp(P.free.dist * (1 + Math.sign(e.deltaY) * 0.1), 25, 3000); }, { passive: false });
  window.addEventListener('pointermove', (e) => { if (!drag) return; P.free.yaw -= (e.clientX - drag.x) * 0.008; P.free.pitch = clamp(P.free.pitch + (e.clientY - drag.y) * 0.006, -1.3, 1.3); drag = { x: e.clientX, y: e.clientY }; });
  window.addEventListener('pointerup', () => { drag = null; });
  function readPad() { let pads = []; try { pads = navigator.getGamepads ? Array.from(navigator.getGamepads()) : []; } catch (e) { return null; } for (const g of pads) if (g && g.connected && g.axes.length >= 4) return g; return null; }
  function stick(x, y) { const m = Math.hypot(x, y); if (m < 0.15) return { x: 0, y: 0 }; const s = Math.min(1, (m - 0.15) / 0.85) / m; return { x: x * s, y: y * s }; }
  let playMode = false, padLeftWas = false, padRightWas = false;
  const touch = { used: false, L: { x: 0, y: 0 }, R: { x: 0, y: 0 }, lt: 0, rt: 0, a: false, b: false };
  const STICK_R = 56, TRIG_TRAVEL = () => Math.max(80, $('ttL').clientHeight - 44);
  function bindStick(zoneId, stickId, side) {
    const zone = $(zoneId), el = $(stickId), knob = el.firstElementChild; let id = null, ox = 0, oy = 0;
    const home = () => { el.classList.add('idle'); el.style.left = side === 'L' ? '22%' : '78%'; el.style.top = '72%'; knob.style.transform = ''; touch[side].x = touch[side].y = 0; };
    home();
    zone.addEventListener('pointerdown', (e) => { if (id !== null || e.target.closest('button')) return; id = e.pointerId; try { zone.setPointerCapture(id); } catch (err) { /* lost */ } touch.used = true; humanTouched(); const r = zone.getBoundingClientRect(); ox = e.clientX - r.left; oy = e.clientY - r.top; el.classList.remove('idle'); el.style.left = ox + 'px'; el.style.top = oy + 'px'; e.preventDefault(); });
    zone.addEventListener('pointermove', (e) => { if (e.pointerId !== id) return; const r = zone.getBoundingClientRect(); let dx = e.clientX - r.left - ox, dy = e.clientY - r.top - oy; const m = Math.hypot(dx, dy); if (m > STICK_R) { dx *= STICK_R / m; dy *= STICK_R / m; } knob.style.transform = 'translate(' + dx + 'px,' + dy + 'px)'; touch[side].x = dx / STICK_R; touch[side].y = -dy / STICK_R; e.preventDefault(); });
    const end = (e) => { if (e.pointerId !== id) return; id = null; home(); }; zone.addEventListener('pointerup', end); zone.addEventListener('pointercancel', end);
  }
  function bindTrigger(trigId, side) {
    const el = $(trigId), handle = el.querySelector('.thandle'), fill = el.querySelector('.tfill'), key = side === 'L' ? 'lt' : 'rt'; let id = null, y0 = 0;
    const show = (v) => { handle.style.transform = 'translateY(' + (v * TRIG_TRAVEL()) + 'px)'; fill.style.height = (v * 100) + '%'; };
    el.addEventListener('pointerdown', (e) => { if (id !== null) return; id = e.pointerId; try { el.setPointerCapture(id); } catch (err) { /* lost */ } y0 = e.clientY; touch.used = true; humanTouched(); touch[key] = S.trigDepth; show(touch[key]); e.preventDefault(); });
    el.addEventListener('pointermove', (e) => { if (e.pointerId !== id) return; const d = (e.clientY - y0) / TRIG_TRAVEL(); if (Math.abs(d) < 0.04) return; const v = clamp(d, 0.06, 1); touch[key] = v; show(v); e.preventDefault(); });
    const end = (e) => { if (e.pointerId !== id) return; id = null; touch[key] = 0; show(0); }; el.addEventListener('pointerup', end); el.addEventListener('pointercancel', end);
  }
  function bindButton(id, fn) { const el = $(id); const dn = (e) => { fn(true); touch.used = true; humanTouched(); el.classList.add('on'); e.preventDefault(); }, up = () => { fn(false); el.classList.remove('on'); }; el.addEventListener('pointerdown', dn); el.addEventListener('pointerup', up); el.addEventListener('pointercancel', up); el.addEventListener('pointerleave', up); }
  bindStick('tzL', 'tsL', 'L'); bindStick('tzR', 'tsR', 'R'); bindTrigger('ttL', 'L'); bindTrigger('ttR', 'R');
  bindButton('tA', (on) => { if (on) swapQueued = 1; }); bindButton('tB', (on) => { touch.b = on; });
  // the two-finger swipe swaps seats (the game's phone gesture, Docs/CREW_STATIONS.md)
  const fingers = new Map(); let swipeDone = false;
  $('touch').addEventListener('pointerdown', (e) => { fingers.set(e.pointerId, { x: e.clientX, x0: e.clientX }); if (fingers.size < 2) swipeDone = false; }, { capture: true });
  $('touch').addEventListener('pointermove', (e) => { const f = fingers.get(e.pointerId); if (!f) return; f.x = e.clientX; if (fingers.size >= 2 && !swipeDone) { let dx = 0; for (const g of fingers.values()) dx += g.x - g.x0; dx /= fingers.size; if (Math.abs(dx) > 60) { swipeDone = true; swapQueued = Math.sign(dx); } } }, { capture: true });
  const lift = (e) => fingers.delete(e.pointerId); $('touch').addEventListener('pointerup', lift, { capture: true }); $('touch').addEventListener('pointercancel', lift, { capture: true });
  function setPlayMode(on) {
    playMode = !!on; document.body.classList.toggle('play', playMode);
    if (playMode) { const v = $('viewport'), req = v.requestFullscreen || v.webkitRequestFullscreen; if (req) { try { const pr = req.call(v, { navigationUI: 'hide' }); if (pr && pr.catch) pr.catch(() => {}); } catch (e) { /* not allowed */ } } try { if (screen.orientation && screen.orientation.lock) screen.orientation.lock('landscape').catch(() => {}); } catch (e) { /* unsupported */ } }
    else { const ex = document.exitFullscreen || document.webkitExitFullscreen; if (document.fullscreenElement || document.webkitFullscreenElement) { try { const pr = ex.call(document); if (pr && pr.catch) pr.catch(() => {}); } catch (e) { /* ignore */ } } touch.lt = touch.rt = 0; touch.a = touch.b = false; touch.L.x = touch.L.y = touch.R.x = touch.R.y = 0; touch.used = false; }
    P.forceResize();
  }
  document.addEventListener('fullscreenchange', () => P.forceResize());
  $('playBtn').addEventListener('click', () => setPlayMode(true)); $('tExit').addEventListener('click', () => setPlayMode(false));
  const PLATFORM = VesselStudioIDE.platform();
  $('touch').addEventListener('pointerdown', () => { if (playMode && !document.fullscreenElement && !document.webkitFullscreenElement) { const v = $('viewport'), req = v.requestFullscreen || v.webkitRequestFullscreen; if (req) { try { const pr = req.call(v, { navigationUI: 'hide' }); if (pr && pr.catch) pr.catch(() => {}); } catch (e) { /* not allowed here */ } } } }, { capture: true });
  const playPause = () => { if (run === 'running') togglePause(); else startRun(); };
  $('tStart').addEventListener('click', playPause); $('tBack').addEventListener('click', () => stopRun()); $('tCam').addEventListener('click', () => cycleCamera());
  $('tpPlay').addEventListener('click', playPause); $('tpStop').addEventListener('click', () => stopRun()); $('startBtn').addEventListener('click', () => startRun()); $('resetBtn').addEventListener('click', () => newRun());
  function keyInput() {
    const k = (c) => keys.has(c) ? 1 : 0;
    return { pad: false, k: { yaw: k('KeyD') - k('KeyA'), pitch: k('KeyS') - k('KeyW'), roll: k('KeyE') - k('KeyQ'), throttle: 1 }, L: { x: k('KeyD') - k('KeyA'), y: k('KeyW') - k('KeyS') }, R: { x: k('KeyD') - k('KeyA'), y: k('KeyW') - k('KeyS') }, lt: k('Space') * S.trigDepth, rt: k('KeyF'), a: keys.has('KeyG'), b: keys.has('KeyH'), kb: true };
  }
  function humanInput() {
    if (playMode && touch.used) return { pad: true, L: stick(touch.L.x, touch.L.y), R: stick(touch.R.x, touch.R.y), lt: touch.lt, rt: touch.rt, a: touch.a, b: touch.b };
    const g = readPad();
    if (g) {
      const inp = { pad: true, L: stick(g.axes[0], -g.axes[1]), R: stick(g.axes[2], -g.axes[3]), lt: g.buttons[6] ? g.buttons[6].value : 0, rt: g.buttons[7] ? g.buttons[7].value : 0, a: !!(g.buttons[0] && g.buttons[0].pressed), b: !!(g.buttons[1] && g.buttons[1].pressed) };
      const dl = !!(g.buttons[14] && g.buttons[14].pressed), dr = !!(g.buttons[15] && g.buttons[15].pressed);
      if (dl && !padLeftWas) swapQueued = -1; if (dr && !padRightWas) swapQueued = 1; padLeftWas = dl; padRightWas = dr;
      if (g.buttons[9] && g.buttons[9].pressed && run !== 'running') startRun();
      if (Math.abs(inp.L.x) + Math.abs(inp.L.y) + Math.abs(inp.R.x) + Math.abs(inp.R.y) > 0.2 || inp.lt > 0.1 || inp.rt > 0.1 || inp.a || inp.b) humanTouched();
      return inp;
    }
    return keyInput();
  }
  const IDLE_S = 3; let lastHumanT = 0, aiHolding = false;
  function humanTouched() { lastHumanT = performance.now() / 1000; aiHolding = false; }

  // ---- the run ----
  let W = null, run = 'ready', paused = false, seedN = 1, stopHeld = false; const feed = [];
  const humanSeat = () => (S.nobody ? -1 : clamp(S.seat, 0, 2));
  function newRun() {
    W = P.makeWorld(S.course, S.crew, S.levels, seedN++, { massDensity: S.massDensity, policy: 'crew', lv: S.lv, nobody: S.nobody, seat: humanSeat() < 0 ? 0 : humanSeat() });
    W.aiMoves = S.aiMoves; W.aiCombine = S.aiCombine; W.crew.seat = Math.max(0, humanSeat());
    P.mountWorld(W); P.drawWorld(W, 0); P.placeCamera(W, 0, true, keys);
    run = 'ready'; paused = false; feed.length = 0; renderFeed(); lastHumanT = performance.now() / 1000; aiHolding = false;
    $('ovTitle').textContent = crewDef().name + ' \u00b7 Omega Run, intensity ' + S.course; $('startBtn').textContent = 'Start'; $('overlay').hidden = false;
    $('courseInfo').textContent = 'I' + S.course + ' \u00b7 ' + COURSE_INFO[S.course] + ' \u00b7 ' + W.track.target + ' crystals, ' + W.walls.length + ' walls';
    syncSeats();
  }
  function startRun() { if (run === 'finished') newRun(); run = 'running'; paused = false; stopHeld = false; $('overlay').hidden = true; lastHumanT = performance.now() / 1000; canvas.focus({ preventScroll: true }); }
  function stopRun() { newRun(); stopHeld = true; }
  function togglePause() { if (run !== 'running') return; paused = !paused; say(paused ? 'Paused' : 'Resumed'); }
  function finishRun() {
    run = 'finished'; const t = W.finishT;
    runs.unshift({ course: S.course, crew: crewDef().name, you: S.nobody ? 'nobody' : SEATS[humanSeat()] + ' / ' + crewDef().members[humanSeat()], time: t, walls: W.wallsBroken + '/' + W.walls.length, combined: Math.round(W.combinedT / Math.max(1e-6, W.t) * 100), swaps: W.swaps });
    runs.length = Math.min(runs.length, 40); saveRuns(); renderRuns();
    $('ovTitle').textContent = t != null ? 'Omega Run \u00b7 ' + t.toFixed(1) + ' s' : 'Run over'; $('ovText').textContent = W.collected + ' / ' + W.track.target + ' crystals, ' + W.wallsBroken + ' / ' + W.walls.length + ' walls, combined ' + Math.round(W.combinedT / Math.max(1e-6, W.t) * 100) + '% of the time, ' + W.crew.combines + ' combine(s), ' + W.swaps + ' seat swap(s).';
    $('startBtn').textContent = 'Run again'; $('overlay').hidden = false;
  }
  // the seat swap: the arena's one gesture steps the person round the three seats (the AI takes the one they leave)
  function swapSeat(dir) {
    if (!W || S.nobody) return; const c = W.crew; const next = ((c.seat + dir) % 3 + 3) % 3; c.seat = next; S.seat = next; save(); W.swaps++;
    say('You: ' + (c.form === 'omega' ? SEATS[next] : crewDef().members[next]) + (c.form === 'omega' ? ' seat' : '')); syncSeats();
  }
  function cycleCamera() { S.cam = S.cam === 'Chase' ? 'Follow' : S.cam === 'Follow' ? 'Free' : 'Chase'; $('camSel').value = S.cam; save(); }
  function say(m) { feed.unshift(m); feed.length = Math.min(feed.length, 4); renderFeed(); }
  function renderFeed() { const el = $('feed'); el.textContent = ''; for (const m of feed) el.appendChild(elx('div', null, m)); }

  // ---- the panels ----
  const courseSel = $('courseSel'); [1, 2, 3, 4].forEach((v) => { const o = elx('option', null, 'I' + v + ' \u00b7 ' + COURSE_INFO[v]); o.value = v; courseSel.appendChild(o); }); courseSel.value = S.course;
  courseSel.addEventListener('change', () => { S.course = +courseSel.value; save(); newRun(); });
  const crewSel = $('crewSel'); CREWS.forEach((c) => { const o = elx('option', null, c.name + ' \u00b7 ' + c.members.join(', ')); o.value = c.id; crewSel.appendChild(o); }); crewSel.value = S.crew;
  crewSel.addEventListener('change', () => { S.crew = crewSel.value; save(); document.documentElement.style.setProperty('--accent-vessel', crewDef().accent); renderCrew(); newRun(); });
  function renderCrew() {
    const c = crewDef(); $('crewTag').textContent = c.tag; $('crewInfo').textContent = c.lore + ' Arms: ' + c.weapon.name + ' (' + c.weapon.info + '). Core: ' + c.core.shield + ' and ' + c.core.finisher + ' (' + c.core.info + ').';
    const tb = $('members'); tb.textContent = '';
    c.members.forEach((n, i) => { const m = MEMBERS[n], tr = elx('tr'); tr.append(elx('td', null, SEATS[i]), elx('td', null, n), elx('td', null, String(m.cruise)), elx('td', null, m.turn + '\u00b0/s'), elx('td', null, m.move.name + ': ' + m.move.ship)); tb.appendChild(tr); });
    const of = P.omegaFlight(c); $('omegaInfo').textContent = 'Omega form: cruise ' + Math.round(of.cruise) + ' u/s, turn ' + Math.round(of.turn) + '\u00b0/s (the members\' average x ' + c.omega.speed + ' and x ' + c.omega.turn + '); it ploughs mass it touches and bounces off walls.';
    const vs = $('viewSel'); vs.textContent = ''; c.members.forEach((n, i) => { const o = elx('option', null, n); o.value = i; vs.appendChild(o); }); vs.value = Math.min(S.view, 2);
    for (const el of ELS) { const e = ELEMENTS[el], card = abCards[el]; card.nm.textContent = e.name; }
    syncSeats();
  }
  $('viewSel').addEventListener('change', (e) => { S.view = +e.target.value; save(); });
  $('camSel').value = S.cam; $('camSel').addEventListener('change', (e) => { S.cam = e.target.value; save(); });
  $('speedSel').value = String(S.speed); $('speedSel').addEventListener('change', (e) => { S.speed = +e.target.value; save(); });
  $('thinking').checked = S.thinking; $('thinking').addEventListener('change', (e) => { S.thinking = e.target.checked; save(); });
  $('massDensity').value = S.massDensity; $('massOut').textContent = S.massDensity + '\u00d7';
  $('massDensity').addEventListener('input', (e) => { S.massDensity = +e.target.value; $('massOut').textContent = S.massDensity + '\u00d7'; save(); }); $('massDensity').addEventListener('change', () => newRun());
  for (const [id, key] of [['aiMoves', 'aiMoves'], ['aiCombine', 'aiCombine'], ['idleHold', 'idleHold']]) { $(id).checked = S[key]; $(id).addEventListener('change', (e) => { S[key] = e.target.checked; save(); if (W) { W.aiMoves = S.aiMoves; W.aiCombine = S.aiCombine; } }); }
  function syncSeats() {
    const box = $('seats'); box.textContent = ''; const c = crewDef(), form = W ? W.crew.form : 'mega';
    c.members.forEach((n, i) => {
      const row = elx('div', 'seat'), dot = elx('span', 'sd'); dot.style.background = '#' + MEMBERS[n].color.toString(16).padStart(6, '0');
      const who = elx('div'); who.append(elx('b', null, SEATS[i] + ' \u00b7 ' + n), elx('small', null, form === 'omega' ? (i === 0 ? 'flies the omega' : i === 1 ? 'aims and fires ' + c.weapon.name : c.core.shield + ' (LT), ' + c.core.finisher + ' (RT)') : MEMBERS[n].move.name + ' on RT'));
      const you = elx('button', 'btn small', !S.nobody && S.seat === i ? 'You' : 'Take'); you.type = 'button'; if (!S.nobody && S.seat === i) you.classList.add('primary');
      you.addEventListener('click', () => { S.nobody = false; S.seat = i; if (W) { W.crew.seat = i; W.swaps++; } save(); syncSeats(); });
      const lv = elx('select'); ['Easy', 'Medium', 'Hard'].forEach((l) => { const o = elx('option', null, 'AI ' + l); o.value = l; lv.appendChild(o); }); lv.value = S.levels[i] || 'Hard'; lv.setAttribute('aria-label', SEATS[i] + ' AI level');
      lv.addEventListener('change', () => { S.levels[i] = lv.value; save(); if (W) W.bodies[i].hc.level = DIFF[lv.value]; });
      row.append(dot, who, you, lv); box.appendChild(row);
    });
    const nb = elx('label', 'check'); const cb = document.createElement('input'); cb.type = 'checkbox'; cb.checked = S.nobody; cb.addEventListener('change', () => { S.nobody = cb.checked; save(); syncSeats(); }); nb.append(cb, document.createTextNode(' Nobody: the whole crew flies itself (watch it)')); box.appendChild(nb);
  }
  // Play Style Config: styles over the four element levels (proposals)
  const styleSel = $('styleSel'); STYLES.forEach((s) => { const o = elx('option', null, s.name); o.value = s.name; styleSel.appendChild(o); });
  styleSel.addEventListener('change', () => { S.style = styleSel.value; Object.assign(S.lv, styleOf(S.style).lv); syncRail(); save(); applyLevels(); });
  const elRows = {}, abCards = {};
  for (const el of ELS) {
    const e = ELEMENTS[el], row = elx('div', 'el'); row.style.setProperty('--accent', 'var(' + EL_COLOR[el] + ')');
    const lab = elx('label', null, el[0].toUpperCase() + el.slice(1)); lab.htmlFor = 'lv-' + el; lab.style.color = 'var(' + EL_COLOR[el] + ')';
    const inp = document.createElement('input'); inp.type = 'range'; inp.min = 0; inp.max = 10; inp.step = 1; inp.id = 'lv-' + el; inp.title = e.tip;
    const out = elx('output'), what = elx('div', 'what'), wl = elx('span'), badge = elx('span', 'l5', e.l5); what.append(wl, badge);
    inp.addEventListener('input', () => { S.lv[el] = +inp.value; const m = STYLES.find((t) => ELS.every((x) => t.lv[x] === S.lv[x])); S.style = m ? m.name : 'Custom'; syncRail(); save(); applyLevels(); });
    row.append(lab, inp, out, what); $('elements').appendChild(row); elRows[el] = { inp, out, wl, badge, e };
    const c = elx('div', 'ab'); c.style.setProperty('--accent', 'var(' + EL_COLOR[el] + ')'); const st = elx('div', 'st'), nm = elx('div', 'nm', e.name); c.append(elx('div', 'ic', el[0].toUpperCase()), nm, st); $('abilities').appendChild(c); abCards[el] = { c, st, nm };
  }
  function applyLevels() { if (!W) return; W.lv = Object.assign({}, S.lv); W.m = mults(W.lv); }
  const specInputs = {};
  for (const [key, lo, hi, step, label, tip] of SPEC) {
    const row = elx('div', 'el'); const lab = elx('label', null, label); lab.htmlFor = 'sp-' + key;
    const inp = document.createElement('input'); inp.type = 'range'; inp.min = lo; inp.max = hi; inp.step = step; inp.id = 'sp-' + key; inp.title = tip + ' (lab-only; lab value ' + DEFAULTS[key] + ')';
    const out = elx('output'), what = elx('div', 'what'), wl = elx('span', null, tip), src = elx('span', 'l5', 'lab ' + DEFAULTS[key]); what.append(wl, src);
    inp.addEventListener('input', () => { S.params[key] = +inp.value; if (+inp.value === DEFAULTS[key]) delete S.params[key]; syncRail(); save(); applyLevels(); });
    row.append(lab, inp, out, what); $('mechanic').appendChild(row); specInputs[key] = { inp, out };
  }
  $('mechReset').addEventListener('click', () => { S.params = {}; syncRail(); save(); applyLevels(); });
  function syncRail() {
    styleSel.value = STYLES.some((s) => s.name === S.style) ? S.style : STYLES[0].name; if (S.style === 'Custom') { if (!styleSel.querySelector('option[value=Custom]')) { const o = elx('option', null, 'Custom'); o.value = 'Custom'; styleSel.appendChild(o); } styleSel.value = 'Custom'; }
    $('styleInfo').textContent = S.style === 'Custom' ? 'Custom levels.' : (styleOf(S.style).info || '');
    const m = mults(S.lv);
    for (const el of ELS) { const r = elRows[el]; r.inp.value = S.lv[el]; r.out.textContent = S.lv[el]; r.badge.classList.toggle('on', S.lv[el] >= 5); r.wl.textContent = ''; r.wl.append(document.createTextNode(r.e.name + ' '), elx('b', null, fmt(m[r.e.param]))); }
    for (const key in specInputs) { const s = specInputs[key]; s.inp.value = par(key); s.out.textContent = fmt(par(key)); s.inp.classList.toggle('changed', key in S.params); }
    $('mechCount').textContent = Object.keys(S.params).length ? Object.keys(S.params).length + ' changed' : 'lab values';
  }
  (function controls() {
    const box = $('controls'); box.textContent = '';
    const rows = [['Gamepad', 'both sticks steer the body you fly \u00b7 LT: apart = hold to COMBINE (crew within gather range); Helm = hold to split; Core = shield \u00b7 RT: your member\'s move; Arms = fire; Core = finisher \u00b7 D-pad left / right = swap seat'],
      ['Keys', 'W S pitch \u00b7 A D yaw \u00b7 Q E roll \u00b7 Space = LT \u00b7 F = RT \u00b7 \u2190 \u2192 swap seat'], ['Arms seat', 'the right stick aims the turret; RT fires (the cannon charges while held)'],
      ['Free cam', 'orbits the viewed body: drag to turn \u00b7 wheel to zoom \u00b7 J L / I K turn \u00b7 U O zoom'], ['Transport', 'Enter play \u00b7 P pause / resume \u00b7 R stop \u00b7 C next camera'],
      ['Phone', 'opens by itself on a phone: two thumb sticks, LT / RT handles, a two-finger swipe (or the swap button) swaps seats']];
    for (const [k, v] of rows) box.append(elx('span', null, k), elx('span', null, v));
  })();
  $('trigDepth').value = String(S.trigDepth); $('trigDepth').addEventListener('change', (e) => { S.trigDepth = +e.target.value; save(); });
  let runs = []; try { runs = JSON.parse(localStorage.getItem(LS + '.runs') || '[]') || []; } catch (e) { runs = []; }
  function saveRuns() { try { localStorage.setItem(LS + '.runs', JSON.stringify(runs)); } catch (e) { /* blocked */ } }
  function renderRuns() {
    const tb = $('runs'); tb.textContent = '';
    if (!runs.length) { const tr = elx('tr'), td = elx('td', null, 'No runs yet. Finish an Omega Run to add one.'); td.colSpan = 7; td.style.color = 'var(--muted)'; tr.appendChild(td); tb.appendChild(tr); return; }
    for (const r of runs.slice(0, 12)) { const tr = elx('tr'); tr.append(elx('td', null, 'I' + r.course), elx('td', null, r.crew), elx('td', null, r.you), elx('td', null, r.time != null ? r.time.toFixed(1) : 'DNF'), elx('td', null, r.walls), elx('td', null, r.combined + '%'), elx('td', null, String(r.swaps))); tb.appendChild(tr); }
  }
  // the scorecard (D5): the crew on AI per level, and the two controls (never combine, always combined)
  const COLS = ['Easy', 'Medium', 'Hard', 'Never', 'Always'];
  function renderScore(rows) {
    const tb = $('score'); tb.textContent = '';
    for (const c of [1, 2, 3, 4]) { const r = rows && rows[c], tr = elx('tr'); tr.append(elx('td', null, 'I' + c)); for (const k of COLS) tr.append(elx('td', null, r && k in r ? (r[k] != null ? r[k].toFixed(1) : 'DNF') : '\u00b7')); tr.append(elx('td', null, r && r.comb != null ? Math.round(r.comb * 100) + '%' : '\u00b7'), elx('td', null, r && r.walls != null ? r.walls : '\u00b7')); tb.appendChild(tr); }
  }
  let scoreRows = null; try { scoreRows = JSON.parse(localStorage.getItem(LS + '.score.' + S.crew) || 'null'); } catch (e) { scoreRows = null; } renderScore(scoreRows);
  function runScorecard(seeds, done) {
    const rows = {}, jobs = []; for (const c of [1, 2, 3, 4]) { rows[c] = {}; for (const k of COLS) jobs.push([c, k]); }
    const med = (a) => { const v = a.filter((x) => x != null).sort((x, y) => x - y); return v.length > a.length / 2 ? v[Math.floor(v.length / 2)] : null; };
    let j = 0;
    const next = () => {
      if (j >= jobs.length) { done(rows); return; }
      const [c, k] = jobs[j++], rs = seeds.map((s) => P.runHeadless(c, S.crew, k === 'Never' || k === 'Always' ? 'Hard' : k, s, { policy: k === 'Never' ? 'never' : k === 'Always' ? 'always' : 'crew', lv: S.lv, massDensity: S.massDensity }));
      rows[c][k] = med(rs.map((r) => r.finish));
      if (k === 'Hard') { rows[c].comb = rs.reduce((a, r) => a + r.combinedShare, 0) / rs.length; rows[c].walls = rs.map((r) => r.walls).join(' / '); }
      renderScore(rows); setTimeout(next, 0);
    };
    setTimeout(next, 20);
  }
  $('scoreBtn').addEventListener('click', () => { $('scoreBtn').disabled = true; $('scoreNote').textContent = 'Running the ' + crewDef().name + ' on every course at every level, then without combining, then always combined\u2026'; runScorecard([11, 22, 33], (rows) => { scoreRows = rows; try { localStorage.setItem(LS + '.score.' + S.crew, JSON.stringify(rows)); } catch (e) { /* blocked */ } renderScore(rows); $('scoreBtn').disabled = false; $('scoreNote').textContent = crewDef().name + ': median of three seeds per cell, seconds; DNF = no finish in ' + BASE.limit + ' s. Never combine cannot open a locked crystal (DNF expected); Always combined collects one crystal at a time. If the crew is not faster than both, combining and splitting are decoration. "Walls" counts the Hard runs\' broken walls per seed.'; }); });
  // the shared decision log (D33)
  const LOCAL_LOG = LS + '.decisions'; let db = null, me = '', localLog = []; try { localLog = JSON.parse(localStorage.getItem(LOCAL_LOG) || '[]') || []; } catch (e) { localLog = []; }
  function settingsDiff() { const changed = {}; for (const k in S.params) changed[k] = S.params[k]; return { vessel: 'omega', crew: S.crew, style: S.style, lv: Object.assign({}, S.lv), changed, course: S.course, massDensity: S.massDensity, seat: S.seat }; }
  function renderDecisions(shared) {
    const box = $('decList'); box.textContent = '';
    const all = (shared || []).concat(localLog.map((d) => Object.assign({ local: true }, d))).sort((a, b) => (b.createdAt || 0) - (a.createdAt || 0));
    if (!all.length) { box.appendChild(elx('p', 'note', 'No decisions yet for the Omega crews. Record the first: which crew, what you tried, what you decided.')); return; }
    for (const d of all.slice(0, 30)) { const r = elx('div', 'dec'); r.append(elx('div', 'meta', (d.topic || 'Other') + (d.settings && d.settings.crew ? ' \u00b7 ' + d.settings.crew : '') + ' \u00b7 ' + (d.createdAt ? new Date(d.createdAt).toLocaleString() : '') + (d.local ? ' \u00b7 this browser' : '')), elx('div', 'txt', d.choice + (d.note ? ' \u2014 ' + d.note : ''))); box.appendChild(r); }
  }
  (async function connectLog() {
    const c = window.claude; renderDecisions([]);
    if (!c || typeof c.use !== 'function') { $('decState').textContent = 'Kept in this browser (Copy log to send it). The shared log is in the claude.ai artifact:'; $('decState').appendChild(VesselStudioTheme.artifactLink()); return; }
    try { db = await c.use('db'); } catch (e) { db = null; }
    try { const u = await c.use('user'); if (u) me = (await u.id()) || ''; } catch (e) { me = ''; }
    if (!db) { $('decState').textContent = 'The shared log is unavailable here; decisions stay in this browser.'; return; }
    db.collection('decisions').orderBy('createdAt', 'desc').limit(300).onSnapshot((snap) => { renderDecisions(snap.docs.map((x) => Object.assign({ id: x.id }, x.data())).filter((d) => d.vessel === 'omega' || (d.settings && d.settings.vessel === 'omega'))); }, () => { $('decState').textContent = 'The shared log could not be read; decisions stay in this browser.'; });
  })();
  $('decBtn').addEventListener('click', async () => {
    const choice = $('decChoice').value.trim(); if (!choice) { $('decState').textContent = 'Say what you decided first.'; return; }
    const doc = { vessel: 'omega', topic: $('decTopic').value, choice, note: $('decNote').value.trim(), settings: settingsDiff(), createdAt: Date.now(), by: me, kind: 'decision' };
    if (db) { try { await db.collection('decisions').add(doc); $('decState').textContent = 'Recorded in the shared log.'; $('decChoice').value = ''; $('decNote').value = ''; return; } catch (e) { /* fall through */ } }
    localLog.unshift(doc); try { localStorage.setItem(LOCAL_LOG, JSON.stringify(localLog)); } catch (e) { /* blocked */ } renderDecisions([]); $('decState').textContent = 'Recorded in this browser. Use Copy log to send it.'; $('decChoice').value = ''; $('decNote').value = '';
  });
  $('decCopy').addEventListener('click', async () => { const all = localLog.slice().reverse().map((d) => '- [' + d.topic + '] ' + d.choice + (d.note ? ' \u2014 ' + d.note : '') + ' ' + JSON.stringify(d.settings)).join('\n'); try { await navigator.clipboard.writeText('Omega Crews studio decisions\n' + all); $('decState').textContent = 'Copied.'; } catch (e) { $('decState').textContent = 'Could not copy here.'; } });
  $('aboutFantasy').textContent = 'Power Rangers SPD, in the HyperSea: every member is a zord with its own way of flying and one move, and the crew is a Megazord with three seats. The finisher ends the fight and the crew splits to recover.';
  $('aboutWhy').textContent = 'Asked for by Yash (2026-10-26): 2-3 pilots who swap seats and run one vessel in different ways, each vessel with its own mega form, then an omega form they combine into. The lab puts three such crews side by side, built from vessels the game already ships, so the question "which three, and does combining feel good" is decided by flying.';
  const notList = $('aboutNot'); for (const s of ['Each member\'s flight and move is a lab approximation of the game\'s vessel (one move each; the Stoat\'s dipole is a boost arc, the Thresher\'s ball a sweep, the Butterfly\'s fold a gate pair).', 'The omega body is the three member bodies docked on struts, not a modelled hull; in the game it would be a model of its own (/vessel-model).', 'Element scaling is a proposal: Charge weapon power, Mass ward cost, Space finisher reach, Time combine speed; nothing is signed off.', 'The course is the game\'s Skim Race track; the walls and the three-lit-crystals rule are this lab\'s mini-game (the Omega Run), not a shipped mode.', 'No sound, no rivals, no lifeforms; PvP is petals only and there is no PvP here.']) notList.appendChild(elx('li', null, s));
  const srcList = $('aboutSources'); for (const s of ['Courses: MinigameSkimRace.unity via Tools/Build/skimrace_track_fingerprint.py --emit-track (VesselStudioEngine.TRACKS); the platform baseline (skim 12 u, boost cap 1.6x, hull reach) is studio-vessel.js BASE; difficulty: SkimRaceDifficulty.asset through DIFF.', 'Members: cruise and turn rates are the lab\'s reading of each vessel\'s class (Docs/claude/ABOUT_PROJECT.md, R_VesselActions/*.md); every number is a proposal, named in Vessel Config > Members.', 'Seats and the swap: Docs/CREW_STATIONS.md (one gesture, seats then hulls), the Orca (every empty seat an AI, ORCA_CREW.md).']) srcList.appendChild(elx('li', null, s));

  // ---- HUD ----
  function hud() {
    if (!W) return; const c = W.crew, B = S.cam === 'Chase' ? flown(W) : P.viewedBody(W), you = humanSeat(), def = crewDef();
    $('hWho').textContent = (B.omega ? def.name + ' omega' : B.name) + (B.omega ? ' \u00b7 ' + SEATS[c.seat] + ' seat' : (you === B.i ? ' \u00b7 you' : ' \u00b7 AI ' + S.levels[B.i])) + ' \u00b7 speed u/s';
    $('hSpeed').textContent = Math.round(B.speed * B.slow); $('hSub').textContent = B.omega ? (c.seat === 1 ? def.weapon.name + (def.weapon.kind === 'cannon' ? ' \u00b7 charge ' + Math.round(c.weapon.charge * 100) + '%' : '') : c.seat === 2 ? def.core.shield + (c.shield ? ' UP' : '') + ' \u00b7 ' + def.core.finisher + (c.meter >= W.m.finisherAt ? ' READY' : ' at ' + Math.round(W.m.finisherAt * 100) + '%') : 'hold LT to split') : B.m.move.name + (B.K.cool > 0 ? ' \u00b7 ' + B.K.cool.toFixed(1) + ' s' : ' \u00b7 ready');
    $('hForm').textContent = c.form === 'omega' ? def.name.toUpperCase() + ' \u00b7 OMEGA FORM' : c.form === 'gather' ? 'GATHERING \u00b7 ' + Math.round(P.dockProgress(W)) + ' u' : '';
    $('hOmega').textContent = Math.round(c.meter * 100) + '%'; $('mOmega').style.width = (c.meter * 100) + '%';
    const near = c.form === 'mega' && W.bodies.every((X) => X.pos.distanceTo(W.bodies[c.seat].pos) < W.m.gatherRange);
    $('hM1Lbl').textContent = c.form === 'mega' ? (near ? 'Crew in range: hold LT to combine' : 'Crew apart') : 'Combined'; $('hM1').textContent = c.form === 'mega' ? Math.round(Math.max(...W.bodies.map((X) => X.pos.distanceTo(W.bodies[c.seat].pos)))) + ' u' : Math.round(W.combinedT) + ' s'; $('mM1').style.width = c.form === 'mega' ? clamp(1 - Math.max(...W.bodies.map((X) => X.pos.distanceTo(W.bodies[c.seat].pos))) / (W.m.gatherRange * 2), 0, 1) * 100 + '%' : '100%';
    $('hBoost').textContent = B.boost.toFixed(2) + '\u00d7'; $('mBoost').style.width = ((B.boost - 1) / (BASE.maxBoost - 1) * 100) + '%';
    const b = $('board'); b.textContent = ''; const hd = elx('div', 'hd'); hd.append(elx('span', null, 'I' + S.course + ' \u00b7 ' + W.t.toFixed(1) + ' s'), elx('span', null, W.collected + '/' + W.track.target + ' \u00b7 walls ' + W.wallsBroken + '/' + W.walls.length)); b.appendChild(hd);
    def.members.forEach((n, i) => { const row = elx('div', 'pl' + (you === i && !S.nobody ? ' you' : '')), dot = elx('span', 'dot'); dot.style.background = '#' + MEMBERS[n].color.toString(16).padStart(6, '0'); const X = W.bodies[i]; row.append(dot, elx('span', null, (c.form === 'omega' ? SEATS[i] + ' \u00b7 ' : '') + n), elx('span', 'st', you === i && !S.nobody ? (aiHolding ? 'AI holding' : 'you') : 'AI ' + S.levels[i] + (c.form === 'mega' && X.aiState ? ' \u00b7 ' + X.aiState : ''))); b.appendChild(row); });
    const lit = W.lit.filter((x) => !x.taken).map((x) => 'crystal ' + (x.i + 1) + (x.locked && !x.wall.broken ? ' (locked)' : '')).join(', '); b.appendChild(elx('div', 'st', 'lit: ' + (lit || 'none')));
    const m = W.m; for (const el of ELS) { const e = ELEMENTS[el], card = abCards[el]; card.st.textContent = fmt(m[e.param]) + (el === 'time' || el === 'mass' ? (el === 'time' ? ' s' : '/s') : el === 'space' ? ' u' : '\u00d7'); card.c.style.borderColor = W.lv[el] >= 5 ? 'var(' + EL_COLOR[el] + ')' : ''; }
    $('readWho').textContent = B.omega ? def.name + ' omega' : B.name;
    const rows = [['speed', (B.speed * B.slow).toFixed(1) + ' u/s'], ['top speed', B.stats.top.toFixed(0) + ' u/s'], ['form', c.form], ['meter', Math.round(c.meter * 100) + '%'], ['crystals', W.collected + ' / ' + W.track.target], ['walls broken', W.wallsBroken + ' / ' + W.walls.length], ['combined', Math.round(W.combinedT / Math.max(1e-6, W.t) * 100) + '%'], ['combines', String(c.combines)], ['seat swaps', String(W.swaps)], ['moves used', String(W.bodies.reduce((a, X) => a + X.stats.uses, 0) + c.uses)], ['mass smashed', String(W.bodies.reduce((a, X) => a + (X.smashed || 0), 0) + ((c.omega && c.omega.smashed) || 0))], ['skims', String(W.bodies.reduce((a, X) => a + X.skims, 0))], ['hull contacts', String(W.bodies.reduce((a, X) => a + X.resets, 0) + ((c.omega && c.omega.resets) || 0))]];
    const box = $('read'); if (box.childElementCount !== rows.length * 2) { box.textContent = ''; for (const [k] of rows) box.append(elx('span', null, k), elx('b')); } rows.forEach(([, v], i) => { box.children[i * 2 + 1].textContent = v; });
    const live = run === 'running' && !paused; $('tpPlay').textContent = live ? '\u275a\u275a Pause' : '\u25b6 Play'; $('tpPlay').classList.toggle('on', live); $('tpPlay').setAttribute('aria-pressed', paused ? 'true' : 'false'); $('tpStop').disabled = run === 'ready' && !stopHeld; $('tStart').textContent = live ? '\u275a\u275a Pause' : '\u25b6 Play';
    $('hAlert').hidden = !(aiHolding && run === 'running'); $('hAlert').textContent = 'AI HOLDING YOUR SEAT \u00b7 touch any control to fly';
    while (W.events.length) say(W.events.shift());
  }
  function markObjective(W) {
    const B = S.cam === 'Chase' ? flown(W) : P.viewedBody(W); if (run !== 'running') { mark.hide(); return; }
    let target = null; if (!B.omega && B.target >= 0) target = W.crystals[B.target]; if (!target) target = W.lit.find((x) => !x.taken) || null;
    if (!target) { mark.hide(); return; } cam.updateMatrixWorld();
    mark.set(cam, target.pos, (target.locked && !target.wall.broken ? 'Locked crystal ' : 'Crystal ') + (target.i + 1) + '/' + W.track.target + ' \u00b7 ' + Math.round(target.pos.distanceTo(B.pos)) + ' u', target.locked && !target.wall.broken ? '#8fa6ff' : '#ffffff', canvas.clientWidth, canvas.clientHeight);
  }
  // ---- loop ----
  let last = performance.now(), hudT = 0; const LAB = { manualClock: false };
  function frame(now) {
    const dt = Math.min(0.05, (now - last) / 1000); last = now; P.resize();
    const hin = humanInput(); const you = humanSeat();
    if (swapQueued) { swapSeat(swapQueued); swapQueued = 0; }
    if (W && run === 'running' && !paused && !LAB.manualClock) {
      const idle = S.idleHold && you >= 0 && (performance.now() / 1000 - lastHumanT) > IDLE_S && !(hin.kb && keys.size > 0); aiHolding = idle;
      for (let s = 0; s < S.speed; s++) { P.stepAll(W, dt, idle ? -1 : you, idle ? null : hin); if (W.over) { finishRun(); break; } }
    }
    ENV.update(dt);
    if (W) { P.drawWorld(W, now / 1000); P.placeCamera(W, dt, false, keys); markObjective(W); }
    renderer.render(scene, cam);
    hudT += dt; if (hudT > 0.1) { hudT = 0; hud(); }
    requestAnimationFrame(frame);
  }
  // ---- the editor layout (D8): the six settings tabs in the universal order (D21, D25, D27) ----
  const IDE = VesselStudioIDE.build({
    key: 'omega-studio-ide-v1', host: document.querySelector('.layout'), stage: $('viewport'),
    right: [['scene', 'Scene Config', [$('grp-scene'), $('grp-world')]], ['game', 'Game Config', [$('grp-intensity'), $('grp-crew'), $('grp-players')]], ['ai', 'AI Config', [$('grp-ai'), document.querySelector('.afternote')]],
      ['style', 'Play Style Config', [$('grp-style'), $('grp-elements')]], ['input', 'Input', [$('grp-controls'), $('grp-feel')]], ['others', 'Vessel Config', [$('grp-mechanic'), $('grp-members'), $('grp-abilities'), $('grp-live')]]],
    bottom: [['score', 'Scorecard', [$('grp-score')]], ['runs', 'Runs', [$('grp-runs')]], ['decisions', 'Decisions', [$('grp-decisions')]], ['about', 'About', [document.querySelector('header .sub'), $('grp-about')]]],
    railW: 390, dockH: 250, winSize: { score: [900, 400], runs: [700, 360], decisions: [520, 420] }, onLayout: () => P.forceResize(),
  });
  VesselStudioIDE.sectionPicker(document.querySelector('.vside-pane[data-tab="others"]'), 'omega-studio', 'vessel');
  renderCrew(); syncRail(); renderRuns(); newRun(); hud();
  requestAnimationFrame((t) => { last = t; frame(t); });
  if (PLATFORM.device === 'phone') setPlayMode(true);

  // ---- hooks: the /labmaker contract (__lab) and the studio hook ----
  const SHIPPED = Object.assign({}, DEFAULTS);
  Object.assign(LAB, {
    SHIPPED, SPEC: SPEC.map((r) => ['crew'].concat(r)), P: S.params, PLATFORM,
    reset: (seed) => { seedN = seed == null ? 1 : seed; newRun(); run = 'running'; paused = false; $('overlay').hidden = true; },
    tick: (dt, n) => { for (let i = 0; i < (n || 1); i++) { if (!W || W.over) break; P.stepAll(W, dt, -1, null); } },
    state: () => ({ t: W ? +W.t.toFixed(4) : 0, run, form: W ? W.crew.form : '', meter: W ? +W.crew.meter.toFixed(3) : 0, crew: S.crew }),
    score: (done) => runScorecard([11, 22, 33], done || (() => {})),
    runBatch: () => { const h = P.runHeadless(1, 'warden', 'Hard', 11), n = P.runHeadless(1, 'warden', 'Hard', 11, { policy: 'never' }), a = P.runHeadless(1, 'warden', 'Hard', 11, { policy: 'always' }); return { hardI1: h.finish, walls: h.walls, combined: h.combinedShare, combines: h.combines, neverI1: n.finish, neverWalls: n.walls, alwaysI1: a.finish }; },
  });
  window.__lab = LAB;
  window.__omegaStudio = {
    get state() { const c = W.crew; return { run, t: +W.t.toFixed(2), crew: S.crew, course: S.course, form: c.form, meter: +c.meter.toFixed(3), seat: c.seat, collected: W.collected, target: W.track.target, walls: W.wallsBroken + '/' + W.walls.length, combines: c.combines, swaps: W.swaps, bodies: bodiesNow(W).map((B) => ({ name: B.name, speed: +B.speed.toFixed(1), pos: B.pos.toArray().map((x) => +x.toFixed(1)), state: B.aiState })) }; },
    get race() { return run; }, get world() { return W; },
    startRace: startRun, stopRace: stopRun, newRace: newRun, togglePause, swapSeat, runHeadless: P.runHeadless, runScorecard, makeWorld: P.makeWorld, stepAll: P.stepAll, CREWS, MEMBERS, DEFAULTS, S, ide: IDE,
    set: (k, v) => { if (k === 'crew') { S.crew = v; crewSel.value = v; document.documentElement.style.setProperty('--accent-vessel', crewDef().accent); renderCrew(); save(); newRun(); return; } if (k === 'course') { S.course = v; courseSel.value = v; save(); newRun(); return; } if (k === 'seat') { S.nobody = v === 'nobody'; if (!S.nobody) S.seat = v; save(); newRun(); return; } S[k] = v; save(); newRun(); },
  };
})();
