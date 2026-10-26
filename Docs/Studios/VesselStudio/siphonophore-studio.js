/* siphonophore-studio.js - the Siphonophore studio (siphonophore.html): a VOLTRON vessel designed from the ground up to be
 * three separate zooids AND one colony at alternating parts of its own game loop (the design principle, 2026-10-27:
 * "designed from the ground up to be both many separate vessels and one big vessel at alternating parts of its game
 * loop", never a combine bolted onto autonomous vessels). None of the three parts is a complete vessel: the BELL can only
 * see (it lights dark crystals), the STING can only open (it breaks the husk round a lit crystal), the BRACT can only
 * catch (it nets a freed crystal into the hold); nothing scores until the three FUSE into the colony, which digests the
 * hold, opens walls and cannot light, open or catch. The hold empty, the colony splits itself: the loop alternates by
 * design, not by a meter. A DESIGN studio (/vessel-studio D2): every number is a lab proposal; the only shipped numbers
 * are the Skim Race courses (VesselStudioEngine.TRACKS) and the platform baseline (its BASE). Lab contract: /labmaker
 * (window.__lab). ASCII only: served without a charset. Needs THREE (r128), studio-look.js, studio-domains.js,
 * studio-ide.js, studio-theme.js and studio-vessel.js (for TRACKS / BASE / DIFF; its engine is not started) before it.
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
  const ROLES = ['Mobility', 'Attack', 'Defence'];

  // ================= the three zooids (parts, never vessels) and the colony =================
  // Each zooid CAN do one thing in the loop and nothing else: the Bell lights, the Sting opens, the Bract catches. Its flight
  // and its one RT move are lab proposals. Their average flight x the colony multipliers is the colony's.
  const ZOOIDS = [
    { id: 'bell', name: 'Bell', role: 'Mobility', can: 'light', cruise: 78, turn: 130, roll: 150, color: 0x9be7ff, body: 'bell',
      move: { name: 'Pulse', info: 'a burst of speed, and the lantern flashes at twice its range', boost: 1.8, time: 1.0, cool: 3 },
      lore: 'The swimming bell: fast, sharp, and blind to everything but light. It carries the lantern that finds crystals in the dark. It has no weapon and no net.' },
    { id: 'sting', name: 'Sting', role: 'Attack', can: 'open', cruise: 60, turn: 85, roll: 100, color: 0xff6fb0, body: 'sting',
      move: { name: 'Venom shot', info: 'breaks the husk round a LIT crystal ahead, one prism per shot; smashes mass', range: 160, rate: 0.25 },
      lore: 'The stinging tentacle: slow to turn, and the only part that can open a husk. It cannot see a dark crystal and cannot carry one.' },
    { id: 'bract', name: 'Bract', role: 'Defence', can: 'catch', cruise: 66, turn: 100, roll: 110, color: 0xa3ff8f, body: 'bract',
      move: { name: 'Cast the net', info: 'catches a FREED crystal within the net radius into the hold', cool: 0.8 },
      lore: 'The shield leaf: the only part with a hold. It nets a freed crystal and carries it, unscored, until the colony digests it. LT is its ward: mass near it is shed.' },
  ];
  const BY_ID = {}; ZOOIDS.forEach((z) => { BY_ID[z.id] = z; });
  const COLONY = { name: 'Siphonophore', speed: 1.25, turn: 0.55, lore: 'The colony: one animal. It digests the hold into the score, ploughs mass and rams walls open; it cannot light, open or catch anything. The hold empty, it splits.' };
  const SEATS = ['Helm', 'Lance', 'Ward'];   // the colony's three seats, one per zooid (Bell flies, Sting lances, Bract wards)

  // ---- the loop's numbers (the SPEC sliders; every one a proposal) ----
  const DEFAULTS = { lanternRange: 90, huskPrisms: 6, huskPerShot: 1, netRadius: 24, holdCap: 3, gatherRange: 120, fuseSeconds: 1.2, digestSeconds: 4, wallHits: 3, lanceRange: 140, lanceRate: 0.3, shieldRadius: 30, splitSeconds: 0.5, surgeSeconds: 0 };
  const SPEC = [
    ['lanternRange', 30, 200, 5, 'Lantern range', 'How far the Bell lights a dark crystal (u); the pulse doubles it for its second'],
    ['huskPrisms', 3, 12, 1, 'Husk prisms', 'Shielded prisms round every crystal; the Sting must break them all before the Bract can net it'],
    ['huskPerShot', 1, 6, 1, 'Husk per shot', 'Husk prisms one venom shot breaks'],
    ['netRadius', 8, 60, 2, 'Net radius', 'How far the Bract casts its net for a freed crystal (u)'],
    ['holdCap', 1, 6, 1, 'Hold', 'Crystals the Bract carries before the colony must digest them'],
    ['gatherRange', 40, 300, 10, 'Gather range', 'Every zooid within this of the one calling (hold LT) for the fuse to start'],
    ['fuseSeconds', 0.4, 3, 0.1, 'Fuse time', 'Seconds the zooids take to dock into the colony once gathered'],
    ['digestSeconds', 1, 10, 0.5, 'Digest time', 'Seconds the colony takes to digest one crystal of the hold into the score'],
    ['wallHits', 1, 8, 1, 'Wall hits', 'Lance hits or rams that open a wall (only the colony can)'],
    ['lanceRange', 60, 300, 10, 'Lance range', 'How far the Lance seat reaches (u)'],
    ['lanceRate', 0.1, 1, 0.05, 'Lance rate', 'Seconds between lance shots'],
    ['shieldRadius', 10, 80, 2, 'Ward radius', 'The Ward seat\'s shield, and the Bract\'s ward apart: mass within this of it is shed (u)'],
    ['splitSeconds', 0.2, 2, 0.1, 'Split time', 'Seconds Helm holds LT to split with crystals still in the hold'],
    ['surgeSeconds', 0, 6, 0.5, 'Surge', 'Seconds the zooids keep the colony\'s speed after a split (Time L5 proposes 3)'],
  ];
  const ELEMENTS = {   // the colony's four, proposals (the fleet contract: one scaled parameter per element)
    charge: { name: 'Venom', param: 'huskPerShot', range: [1, 3], l5: 'Contagion', tip: 'Charge (the Sting): each venom round breaks more of a husk (x1 to x3). L5 Contagion (proposal, not flown yet): a husk the venom opens poisons the next husk in the window.' },
    mass: { name: 'Trawl', param: 'netRadius', range: [1, 1.6], l5: 'Trawl', tip: 'Mass (the Bract): a wider net (x1 to x1.6). L5 Trawl: the hold takes one more.' },
    space: { name: 'Lantern', param: 'lanternRange', range: [1, 1.8], l5: 'Beacon', tip: 'Space (the Bell): the lantern reaches farther (x1 to x1.8). L5 Beacon: the pulse lights every crystal in the window.' },
    time: { name: 'Digest', param: 'digestSeconds', range: [1, 0.5], l5: 'Surge', tip: 'Time (the colony): the colony digests faster (x1 to x0.5). L5 Surge: the zooids keep the colony\'s speed for 3 s after a split.' },
  };
  // ================= the ability kit (round 2 proposal, 2026-10-27): every zooid's passive and two triggers, the colony's seats; borrowed from the fleet =================
  // state: 'sim' = flown by this page today, 'part' = flown in part, 'new' = a proposal the sim does not fly yet
  const KIT = [["Bell", "space", "passive", "Lantern", "Lights every dark crystal within its reach. Space grows the reach x1 to x1.8.", "Dolphin Echo Sight, Angler Paint", "sim"], ["Bell", "space", "RT", "Pulse Jet", "Hold to inhale, release to jet. Release on the beat of your last two pulses and the chain grows x1.1 a pulse, up to x1.8. Each pulse doubles the lantern for 1 s.", "Octopus pulse jet", "part"], ["Bell", "space", "LT", "Slipstream", "Hold to lead: the Sting and the Bract form up in the Bell's wake, one, two, three, and draft at its speed x1.3. The march is a Bell ability, not a rule.", "Remora draft, Jacks The Call", "new"], ["Bell", "space", "L5", "Beacon", "A pulse lights every crystal of the window at once.", "Dolphin Pilot Echo", "sim"], ["Sting", "charge", "RT", "Nematocyst Volley", "Hold for aimed venom out to 160 u. A round breaks the husk of a lit crystal (Charge: x1 to x3 husk prisms a round); with no crystal on the line it smashes the mass there.", "Sparrow Pulsefire, Urchin Chain Spikes", "sim"], ["Sting", "charge", "LT", "Tentacle Lash", "The Sting streams a tentacle of its own trail. Hold to coil: the turn doubles and the tentacle bunches. Let go and it whips straight, cutting the mass it sweeps; a rival hull it strikes loses petals, which spill as crystals.", "Eel coil and whip, Thresher, Wrasse Kite Ram", "new"], ["Sting", "charge", "passive", "Reel", "When a volley or a lash lands on a target more than 120 u away, a strand pulls the Sting toward it like a bungee: the slowest zooid catches up by hitting things.", "Gibbon web, Shrike Void Sling", "new"], ["Sting", "charge", "L5", "Contagion", "A husk the venom opens poisons the next husk in the window: one volley opens two crystals.", "Manta Contagion, Urchin Overcharge", "new"], ["Bract", "mass", "RT", "Net Cast", "Catches the freed crystals within its reach into the hold (three at most). Mass grows the reach x1 to x1.6. Only the Bract can carry the catch.", "Jacks netting, Angler Lure", "sim"], ["Bract", "mass", "LT", "Bract Shell", "Hold to ring the Bract in super-shielded plates, open ahead, that clear the mass they meet. Let go and the plates drop where they are as danger prisms: a trap for a rival on the hold's tail.", "Orca Danger Shield, Rhino afterimages", "part"], ["Bract", "mass", "passive", "Anchor", "Stop on mass and the Bract digs in: its net reaches twice as far and the other two dock to it in half the time. Where the Bract sits is where the colony forms.", "Grizzly Dig In, Shrike Nest, Gecko stick", "part"], ["Bract", "mass", "L5", "Trawl", "The hold takes one more, and the net also catches the crystals a rival sheds when the Sting lashes it.", "Wrasse Crystal Steal, Squirrel Steal", "part"], ["Colony", "time", "passive", "Digest", "One held crystal into the score every 2 s. Time shortens it x1 to x0.5. The hold empty and the wall open, the colony splits on its own.", "Siphonophore (its own)", "sim"], ["Colony", "time", "Helm RT", "Ram Spool", "Hold to wind the colony up. At full spool it runs red and a ram opens a wall cell and breaks shielded mass in one hit.", "Rhino Ramp Spool and Breaker, Thresher smash speed", "part"], ["Colony", "time", "Helm LT", "Scatter", "Hold to split. Let go and the three zooids are flung x1.4 along the stick, each toward its own next crystal.", "Remora sling, Gecko hop, Butterfly Fold", "part"], ["Colony", "time", "Lance RT", "Siphon Lance", "Aim with the right stick: a hitscan line out to 400 u that cuts mass and wall cells.", "Shrike Void Lance, Serpent Sniper Shot", "sim"], ["Colony", "time", "Ward LT", "Colony Shell", "While held, the hull ploughs through the mass it meets instead of being slowed.", "Grizzly Rush, Brittle Star Speed Mode", "sim"], ["Colony", "time", "Ward RT", "Chain Wake", "The colony lays its own body behind it as a short chain of shielded prisms, a wall a chasing rival bounces off.", "Eel body, Brittle Star corner trails", "new"], ["Colony", "time", "L5", "Surge", "After a split the zooids keep the colony's speed for 3 s.", "Remora sling, Urchin Slipstream", "sim"]].map(([part, el, input, name, what, from, state]) => ({ part, el, input, name, what, from, state }));
  const KIT_COMBOS = ["Light, venom, net, digest: each zooid's verb is the next one's condition, so none of them scores alone.", "Lash, Trawl, Digest: the Sting knocks a rival's petals loose as crystals, the Bract nets them, the colony digests them. A steal no single zooid can finish.", "Slipstream carries the slow pair: the Bell leads, and the Sting and the Bract keep its pace only in its wake.", "Anchor sets the fuse point, and the Bract Shell's danger drop covers the docking.", "The Bell's pulse chain carries into the fuse: the colony's spool starts at the chain's multiplier.", "Scatter aims the split: the three zooids land on the window's three crystals, one each."];
  const STYLES = [
    { name: 'Balanced', lv: { charge: 0, mass: 0, space: 0, time: 0 }, info: 'Every element at 0: the lab\'s numbers.' },
    { name: 'Scout', lv: { charge: 0, mass: 0, space: 10, time: 10 }, info: 'See far, digest fast: short colony phases, a long apart phase.' },
    { name: 'Hunter', lv: { charge: 10, mass: 10, space: 0, time: 0 }, info: 'Open and catch fast: the apart phase is short, the hold fills.' },
    { name: 'Maxed', lv: { charge: 10, mass: 10, space: 10, time: 10 }, info: 'Every element at 10. A ceiling, not a match state.' },
  ];

  // ================= settings =================
  const S = { course: 1, seat: 0, levels: ['Hard', 'Hard', 'Hard'], nobody: false, view: 0, cam: 'Chase', speed: 1, thinking: true,
    idleHold: true, aiMoves: true, aiFuse: true, massDensity: 1, trigDepth: 1, style: 'Balanced', lv: { charge: 0, mass: 0, space: 0, time: 0 }, params: {} };
  const LS = 'cs.studio.siphonophore.v1';
  try { const s = JSON.parse(localStorage.getItem(LS) || 'null'); if (s) Object.assign(S, s, { lv: Object.assign({}, S.lv, s.lv || {}), params: Object.assign({}, s.params || {}), levels: (s.levels || S.levels).slice(0, 3) }); } catch (e) { /* storage blocked */ }
  function save() { try { localStorage.setItem(LS, JSON.stringify(S)); } catch (e) { /* storage blocked */ } }
  const par = (k) => (k in S.params ? S.params[k] : DEFAULTS[k]);
  function mults(lv) {
    const m = {}; for (const k in DEFAULTS) m[k] = par(k);
    for (const el of ELS) { const e = ELEMENTS[el], r = e.range; m[e.param] = par(e.param) * lerp(r[0], r[1], clamp(lv[el] / 10, 0, 1)); }
    m.huskPerShot = Math.max(1, Math.round(m.huskPerShot));
    m.l5 = { charge: lv.charge >= 5, mass: lv.mass >= 5, space: lv.space >= 5, time: lv.time >= 5 };
    if (m.l5.mass) m.holdCap += 1;
    if (m.l5.time && par('surgeSeconds') === DEFAULTS.surgeSeconds) m.surgeSeconds = 3;
    return m;
  }
  const styleOf = (name) => STYLES.find((s) => s.name === name) || STYLES[0];
  const colonyFlight = () => { const avg = (k) => ZOOIDS.reduce((a, z) => a + z[k], 0) / ZOOIDS.length; return { cruise: avg('cruise') * COLONY.speed, turn: avg('turn') * COLONY.turn, roll: avg('roll') * COLONY.turn }; };

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
  function removePrism(W, o) { if (o.dead) return; W.grid.remove(o); o.dead = true; W.dirty = true; if (o.kind === 'husk' && o.crystal) { o.crystal.husk--; if (o.crystal.husk <= 0 && o.crystal.state === 'lit') { o.crystal.state = 'freed'; W.events.push('Crystal ' + (o.crystal.i + 1) + ' freed'); } } }
  function smashNear(W, pos, r, walls) {   // destroy mass round a point; a wall only when `walls` says so; husks never (the Sting's shot is the only key)
    let n = 0, wall = null;
    for (const p of W.grid.near(pos, r + 6, tmpArr)) { if (p.pos.distanceTo(pos) > r) continue; if (p.kind === 'wall') { wall = p.wallRef; continue; } if (p.kind === 'track' || p.kind === 'husk') continue; removePrism(W, p); n++; }
    if (wall && walls) hitWall(W, wall, walls);
    return { n, wall };
  }
  function hitWall(W, wall, hits) {
    if (wall.broken) return false; wall.hp -= hits; for (const p of wall.prisms) p.flash = 1; W.dirty = true;
    if (wall.hp <= 0) { wall.broken = true; for (const p of wall.prisms) removePrism(W, p); W.wallsBroken++; W.events.push('Wall ' + (wall.index + 1) + ' open'); return true; }
    return false;
  }

  // ================= the world: three zooids (apart) or one colony =================
  function makeBody(W, z, pos, quat, i) {
    return { i, name: z.name, z, pos: pos.clone(), quat: quat.clone(), course: fwdOf({ quat }), vel: new V(), speed: 0, slow: 1, boost: 1, boostT: 0,
      F: { cruise: z.cruise, turn: z.turn, roll: z.roll }, K: { cool: 0, rtWas: false, ltWas: false, pulseT: 0, netT: 0, active: false }, railAcc: 0, rails: 0, skims: 0, resets: 0,
      stats: { top: 0, uses: 0 }, hc: null, believed: null, aim: null, aimWhy: '', aiState: '', target: -1, lastInp: null, colony: false, surgeT: 0 };
  }
  function makeWorld(courseId, levels, seed, opts) {
    opts = opts || {};
    const track = buildTrack(TRACKS[courseId]);
    const W = { track, course: courseId, prisms: [], grid: new Grid(24), bodies: [], t: 0, rand: rng(seed), seed, events: [], over: false, dirty: true, watch: 0, finishT: null,
      massDensity: opts.massDensity != null ? opts.massDensity : S.massDensity, walls: [], wallsBroken: 0, crystals: [], digested: 0, rings: [], shots: [], seatsAI: levels.slice(), nobody: !!opts.nobody,
      policy: opts.policy || 'crew', lv: Object.assign({}, opts.lv || S.lv), m: null, swaps: 0, fusedT: 0, apartT: 0,
      crew: { form: 'apart', hold: 0, held: [], gather: 0, split: 0, seat: opts.seat != null ? opts.seat : S.seat, shield: false, calling: false, colony: null, fuses: 0, digest: 0, lanceT: 0, ramT: 0, uses: 0, aim: { yaw: 0, pitch: 0 } } };
    W.m = mults(W.lv);
    for (let i = 0; i < track.line.length; i++) addPrism(W, { pos: track.line[i].clone(), quat: basisQuat(track.tan[i]), scale: [3.2, 3.2, 9], owner: -1, domain: 'track', shield: false, danger: false, kind: 'track' });
    scatterMass(W);
    // the crystals: one per anchor per lap, each DARK inside a HUSK of shielded prisms; every third sits behind a wall across the course
    for (let k = 0; k < track.target; k++) {
      const a = track.anchors[k % track.anchors.length], ai = nearestIndex(track, a, 0, true), q = basisQuat(track.tan[ai]);
      const c = { i: k, pos: a.clone(), state: 'dark', husk: 0, wall: null, quat: q, litAt: -1 };
      if ((k % 3) === 2 && k < track.target - 1) {
        const base = track.line[ai], right = new V(1, 0, 0).applyQuaternion(q), up = new V(0, 1, 0).applyQuaternion(q);
        const wall = { hp: W.m.wallHits, broken: false, index: W.walls.length, crystalIndex: k, prisms: [], centre: base.clone(), quat: q };
        for (let x = -2; x <= 2; x++) for (let y = -2; y <= 2; y++) wall.prisms.push(addPrism(W, { pos: base.clone().addScaledVector(right, x * 9).addScaledVector(up, y * 9 + 4), quat: q.clone(), scale: [8.6, 8.6, 5], owner: -1, domain: 'wall', shield: true, danger: false, kind: 'wall', wallRef: wall }));
        c.wall = wall; W.walls.push(wall);
      }
      const n = Math.round(W.m.huskPrisms), right = new V(1, 0, 0).applyQuaternion(q), up = new V(0, 1, 0).applyQuaternion(q);
      for (let h = 0; h < n; h++) { const ang = h / n * Math.PI * 2; const p = addPrism(W, { pos: c.pos.clone().addScaledVector(right, Math.cos(ang) * 9).addScaledVector(up, Math.sin(ang) * 9), quat: q.clone().multiply(new Q().setFromAxisAngle(AZ, ang)), scale: [2.4, 2.4, 6], owner: -1, domain: 'husk', shield: true, danger: false, kind: 'husk', crystal: c }); c.husk++; }
      W.crystals.push(c);
    }
    const t0 = track.tan[0], q0 = basisQuat(t0), side = new V(1, 0, 0).applyQuaternion(q0);
    ZOOIDS.forEach((z, i) => {
      const B = makeBody(W, z, track.line[0].clone().addScaledVector(side, (i - 1) * 26).addScaledVector(t0, -40), q0, i);
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
  // the WINDOW: the next three crystals not yet in the hold or digested (the AI works on these; the markers show them)
  function windowOf(W) { const out = []; for (const c of W.crystals) { if (c.state === 'held' || c.state === 'digested') continue; out.push(c); if (out.length >= 3) break; } return out; }
  function nearestIndex(track, pos, hint, full) {
    const L = track.line, n = L.length; let best = hint, bd = Infinity;
    if (full) { for (let i = 0; i < n; i++) { const d = L[i].distanceToSquared(pos); if (d < bd) { bd = d; best = i; } } return best; }
    for (let k = -30; k <= 60; k++) { const i = ((hint + k) % n + n) % n; const d = L[i].distanceToSquared(pos); if (d < bd) { bd = d; best = i; } }
    return best;
  }
  window.__siphParts = { T, V, Q, TRACKS, COURSE_INFO, BASE, DIFF, DEG, lerp, clamp, ease, easeInv, rng, elx, ELS, EL_COLOR, SEATS, ROLES, ZOOIDS, BY_ID, COLONY, DEFAULTS, SPEC, ELEMENTS, STYLES, KIT, KIT_COMBOS, S, LS, save, par, mults, styleOf, colonyFlight,
    buildTrack, Grid, basisQuat, tmpArr, AX, AY, AZ, dq, fwdOf, upOf, rightOf, addPrism, removePrism, smashNear, hitWall, makeBody, makeWorld, windowOf, nearestIndex, $ };
})();

// ================= part B: the physics of a body, the zooids' moves, the fuse, the colony's seats =================
(function () {
  'use strict';
  const P = window.__siphParts, { T, V, Q, BASE, DEG, lerp, clamp, ease, easeInv, ZOOIDS, COLONY, SEATS, colonyFlight, tmpArr, AX, AY, AZ, dq, fwdOf, upOf, rightOf, addPrism, removePrism, smashNear, hitWall, windowOf, nearestIndex } = P;

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
  function fly(B, dt, targetSpeed, grip, rate) {
    const fwd = fwdOf(B);
    B.speed += (targetSpeed - B.speed) * (1 - Math.exp(-(rate == null ? 1.5 : rate) * dt));
    B.course.lerp(fwd, 1 - Math.exp(-(grip == null ? 30 : grip) * dt)).normalize();
    B.vel.copy(B.course).multiplyScalar(B.speed);
    B.pos.addScaledVector(B.course, B.speed * B.slow * dt);
  }
  const edge = (B, key, v) => { const was = !!B.K[key]; B.K[key] = !!v; return !!v && !was; };

  // ---- what each zooid CAN do (its one verb in the loop) and its RT move ----
  function lightNear(W, B, range) {   // the Bell's lantern: every dark crystal within range is lit (it stays lit)
    let n = 0; for (const c of W.crystals) { if (c.state !== 'dark' || (c.wall && !c.wall.broken)) continue; if (c.pos.distanceTo(B.pos) < range) { c.state = 'lit'; c.litAt = W.t; n++; W.events.push('Crystal ' + (c.i + 1) + ' lit'); } } return n;
  }
  function zooidMove(W, B, inp, dt) {
    const z = B.z, mv = z.move, K = B.K; K.cool = Math.max(0, K.cool - dt); K.active = false;
    const rt = inp.rt > 0.5, press = edge(B, 'rtWas', rt), lt = inp.lt > 0.3;
    if (z.can === 'light') {
      lightNear(W, B, W.m.lanternRange * (K.pulseT > 0 ? 2 : 1));
      if (press && K.cool <= 0) { B.boost = mv.boost; B.boostT = mv.time; K.pulseT = 1; K.cool = mv.cool; B.stats.uses++; W.events.push('Bell: pulse'); if (W.m.l5.space) for (const c of windowOf(W)) if (c.state === 'dark' && !(c.wall && !c.wall.broken)) { c.state = 'lit'; c.litAt = W.t; } }
      if (K.pulseT > 0) K.pulseT -= dt; K.active = B.boostT > 0;
    } else if (z.can === 'open') {
      if (rt) {
        K.active = true; K.gunT = (K.gunT || 0) - dt;
        if (K.gunT <= 0) {
          K.gunT = mv.rate; const fwd = fwdOf(B); let hit = null, bd = mv.range;
          // a LIT crystal of the window whose centre lies within 12 u of the nose line: the venom breaks its husk
          for (const c of windowOf(W)) { if (c.state !== 'lit' || (c.wall && !c.wall.broken)) continue; const d = c.pos.clone().sub(B.pos), along = d.dot(fwd); if (along < 0 || along > mv.range) continue; if (Math.sqrt(Math.max(0, d.lengthSq() - along * along)) > 12) continue; if (along < bd) { bd = along; hit = c; } }
          if (hit) {
            W.shots.push({ a: B.pos.clone(), b: hit.pos.clone(), t: 0.12 }); B.stats.uses++;
            let left = W.m.huskPerShot; for (const h of W.prisms) { if (left <= 0) break; if (!h.dead && h.kind === 'husk' && h.crystal === hit) { removePrism(W, h); left--; } }
          } else {   // no crystal on the line: the nearest mass on it is smashed
            let best = null; bd = mv.range;
            for (const p of W.grid.near(B.pos.clone().addScaledVector(fwd, mv.range / 2), mv.range / 2 + 20, tmpArr)) { if (p.kind !== 'mass' && p.kind !== 'rail') continue; const d = p.pos.clone().sub(B.pos), along = d.dot(fwd); if (along < 4 || along > mv.range) continue; if (Math.sqrt(Math.max(0, d.lengthSq() - along * along)) > 8) continue; if (along < bd) { bd = along; best = p; } }
            if (best) { W.shots.push({ a: B.pos.clone(), b: best.pos.clone(), t: 0.12 }); removePrism(W, best); B.stats.uses++; B.smashed = (B.smashed || 0) + 1; }
          }
        }
      } else K.gunT = 0;
    } else if (z.can === 'catch') {
      if (press && K.cool <= 0) {
        K.netT = 0.35; K.cool = mv.cool; B.stats.uses++; let got = 0;
        for (const c of W.crystals) { if (c.state !== 'freed' || (c.wall && !c.wall.broken) || W.crew.hold >= W.m.holdCap) continue; if (c.pos.distanceTo(B.pos) < W.m.netRadius) { c.state = 'held'; W.crew.hold++; W.crew.held.push(c.i); got++; W.events.push('Bract nets crystal ' + (c.i + 1) + ' (hold ' + W.crew.hold + '/' + W.m.holdCap + ')'); if (!W.m.l5.mass || got >= 2) break; } }
        if (!got) W.events.push('Bract: the net is empty');
      }
      if (K.netT > 0) K.netT -= dt;
      if (lt) { K.active = true; smashNear(W, B.pos, W.m.shieldRadius * 0.6, 0); }
    }
  }
  // ---- the common step of a body: contacts, skims, trail ----
  function contacts(W, B, dt, plough) {
    const near = W.grid.near(B.pos, BASE.skimR + 10, tmpArr), reachBase = B.colony ? 8 : BASE.hullReach;
    for (const p of near) {
      const d = p.pos.distanceTo(B.pos), own = p.owner === B.i && !B.colony, reach = reachBase + (p.kind === 'track' ? 1.6 : p.kind === 'wall' ? 4 : 0.9) * (p.scale ? p.scale[0] / 3 : 1);
      if (d < reach + 1 && (p.hull[B.i] || 0) <= W.t && !(own && p.kind === 'rail' && W.t - (p.born || 0) < BASE.rail.grace)) {
        p.hull[B.i] = W.t + 0.6;
        if (p.kind === 'wall') {   // a wall stops a zooid; the colony RAMS it (one hit per ram, Helm's pulse)
          const n = B.pos.clone().sub(p.pos).normalize(); B.pos.addScaledVector(n, 3); B.course.reflect(n.negate()).negate(); B.speed *= 0.5; B.resets++; p.flash = 1; W.dirty = true;
          if (B.colony && W.crew.ramT <= 0) { W.crew.ramT = 0.8; hitWall(W, p.wallRef, 1); W.events.push('Colony rams the wall'); } else if (!B.colony) W.events.push(B.name + ' hits the wall');
          continue;
        }
        if (p.kind === 'husk') { const n = B.pos.clone().sub(p.pos).normalize(); B.pos.addScaledVector(n, 2); B.speed *= 0.7; B.resets++; continue; }
        if (plough && p.kind !== 'track') { removePrism(W, p); B.smashed = (B.smashed || 0) + 1; continue; }
        if (W.crew.shield && B.colony) { removePrism(W, p); continue; }
        B.boost = 1; B.boostT = 0; B.slow = Math.min(B.slow, 0.9); B.resets++; W.events.push((B.colony ? 'Colony' : B.name) + ': hull contact');
        continue;
      }
      if ((p.cool[B.i] || 0) > W.t || d > BASE.skimR || own || p.kind === 'wall' || p.kind === 'husk') continue;
      p.cool[B.i] = W.t + 0.6; p.flash = 1; W.dirty = true;
      B.boost = clamp(B.boost + BASE.skimAdd, 1, BASE.maxBoost); B.skims++;
    }
    if (!B.colony && W.t > BASE.rail.startDelay) { B.railAcc += B.speed * dt; while (B.railAcc >= BASE.rail.spacing) { B.railAcc -= BASE.rail.spacing; const right = rightOf(B); for (const s of [-1, 1]) addPrism(W, { pos: B.pos.clone().addScaledVector(right, s * BASE.rail.offset), quat: B.quat.clone(), scale: [0.83, 0.83, 6.1], owner: B.i, domain: 'rail', shield: false, danger: false, kind: 'rail', born: W.t }); B.rails += 2; if (B.rails > 120) { let rm = 0; for (const p of W.prisms) if (!p.dead && p.kind === 'rail' && p.owner === B.i) { removePrism(W, p); B.rails--; if (++rm >= 2) break; } } } }
  }
  function stepZooid(W, B, inp, dt) {
    B.lastInp = inp; B.slow += (1 - B.slow) * (1 - Math.exp(-dt));
    if (B.boostT > 0) B.boostT -= dt; else if (B.boost > 1) B.boost = Math.max(1, B.boost - BASE.boostDecay * dt);
    if (B.surgeT > 0) B.surgeT -= dt;
    steer(B, inp, dt, 1);
    const holding = W.crew.calling && W.crew.form === 'apart' && B.i === gatherSeat(W) && inp.ai;
    const cruise = B.surgeT > 0 ? Math.max(B.F.cruise, colonyFlight().cruise) : B.F.cruise;
    const working = inp.ai && B.aim && (B.aimWhy === 'to a lit crystal' || B.aimWhy === 'to a freed crystal') && B.aim.distanceTo(B.pos) < 90;   // the Sting and the Bract slow over their crystal so a shot or a cast lands
    fly(B, dt, cruise * B.boost * (holding ? 0.45 : working ? 0.55 : 1), 30, 1.5);
    if (W.aiMoves !== false || !inp.ai) zooidMove(W, B, inp, dt);
    contacts(W, B, dt, false);
    B.stats.top = Math.max(B.stats.top, B.speed);
  }

  // ---- the fuse: the zooids dock round the caller, kinematically (L-STU-144) ----
  function beginGather(W) { const c = W.crew; if (c.form !== 'apart') return false; c.form = 'gather'; c.gather = 0; return true; }
  const gatherSeat = (W) => (W.crew.humanSeat == null || W.crew.humanSeat < 0 ? 2 : Math.min(W.crew.humanSeat, 2));   // the person's zooid calls the fuse, else the Bract (it carries the hold)
  function gatherPoint(W) { const lead = W.bodies[gatherSeat(W)]; return { pos: lead.pos, quat: lead.quat }; }
  function slotOf(W, i) { const g = gatherPoint(W), fwd = fwdOf(g), up = upOf(g); return i === 0 ? g.pos.clone().addScaledVector(up, 7) : i === 1 ? g.pos.clone().addScaledVector(up, -6).addScaledVector(fwd, 5) : g.pos.clone().addScaledVector(fwd, -6); }
  function dockProgress(W) { let far = 0; for (let i = 0; i < 3; i++) far = Math.max(far, W.bodies[i].pos.distanceTo(slotOf(W, i))); return far; }
  function completeFuse(W) {
    const c = W.crew, g = gatherPoint(W), F = colonyFlight();
    const O = P.makeBody(W, { id: 'colony', name: COLONY.name, cruise: F.cruise, turn: F.turn, roll: F.roll, color: 0xffffff, body: 'colony', can: 'digest', move: { name: 'Ram', cool: 0 } }, g.pos, g.quat, 3);
    O.colony = true; O.speed = Math.max(...W.bodies.map((B) => B.speed)); O.course.copy(fwdOf(O));
    c.colony = O; c.form = 'colony'; c.fuses++; c.callWall = null; c.digest = 0; c.lanceT = 0; c.ramT = 0; c.shield = false; c.split = 0; c.aim = { yaw: 0, pitch: 0 }; c.fusedAt = W.t;
    const win = windowOf(W); c.wallGoal = win.length && win[0].wall && !win[0].wall.broken ? win[0].wall : nearestWallAhead(W, O, 420);   // the wall it fused for: it stays the colony until that wall is open
    W.events.push('THE COLONY (hold ' + c.hold + ')');
  }
  function splitNow(W, why) {
    const c = W.crew, O = c.colony; if (!O) return;
    const fwd = fwdOf(O), right = rightOf(O), up = upOf(O);
    W.bodies.forEach((B, i) => { B.pos.copy(O.pos).addScaledVector(i === 0 ? up : i === 1 ? right : right.clone().negate(), i === 0 ? 10 : 16).addScaledVector(fwd, i === 0 ? 8 : -6); B.quat.copy(O.quat); B.course.copy(fwd); B.speed = O.speed * 0.9; B.vel.copy(B.course).multiplyScalar(B.speed); B.boost = 1; B.boostT = 0; B.K.cool = 0; B.surgeT = W.m.surgeSeconds; });
    c.colony = null; c.form = 'apart'; c.split = 0; c.shield = false; c.splitAt = W.t; W.events.push('Split' + (why ? ' (' + why + ')' : ''));
  }
  // ---- the colony: one body, three seats (Helm flies and rams; Lance opens walls; Ward shields); it DIGESTS the hold ----
  function colonyLance(W, O, inp, dt) {
    const c = W.crew; c.lanceT = Math.max(0, c.lanceT - dt);
    c.aim.yaw = clamp(c.aim.yaw + (inp.R ? inp.R.x : 0) * 2.2 * dt, -0.9, 0.9); c.aim.pitch = clamp(c.aim.pitch + (inp.R ? inp.R.y : 0) * 2.2 * dt, -0.7, 0.7);
    const aimDir = fwdOf(O).applyAxisAngle(upOf(O), -c.aim.yaw).applyAxisAngle(rightOf(O), c.aim.pitch).normalize(); O.aimDir = aimDir;
    if (inp.rt > 0.5 && c.lanceT <= 0) {
      c.lanceT = W.m.lanceRate; let best = null, bd = W.m.lanceRange;
      for (const p of W.grid.near(O.pos.clone().addScaledVector(aimDir, bd / 2), bd / 2 + 20, tmpArr)) { if (p.kind === 'track' || p.kind === 'husk') continue; const d = p.pos.clone().sub(O.pos), along = d.dot(aimDir); if (along < 6 || along > bd) continue; if (Math.sqrt(Math.max(0, d.lengthSq() - along * along)) > (p.kind === 'wall' ? 12 : 7)) continue; if (along < bd) { bd = along; best = p; } }
      if (best) { W.shots.push({ a: O.pos.clone(), b: best.pos.clone(), t: 0.1 }); c.uses++; if (best.kind === 'wall') hitWall(W, best.wallRef, 1); else removePrism(W, best); }
    }
  }
  function stepColony(W, inputs, dt) {
    const c = W.crew, O = c.colony; O.slow += (1 - O.slow) * (1 - Math.exp(-dt));
    if (O.boostT > 0) O.boostT -= dt; else if (O.boost > 1) O.boost = Math.max(1, O.boost - BASE.boostDecay * dt);
    c.ramT = Math.max(0, c.ramT - dt);
    const helm = inputs[0], lance = inputs[1], ward = inputs[2];
    steer(O, helm, dt, 1); fly(O, dt, O.F.cruise * O.boost, 30, 1.2);
    if (helm.rt > 0.5 && edge(O, 'rtWas', true) && O.K.cool <= 0) { O.boost = 1.6; O.boostT = 1; O.K.cool = 2; W.events.push('Colony: ram pulse'); } else if (helm.rt <= 0.5) edge(O, 'rtWas', false);
    O.K.cool = Math.max(0, O.K.cool - dt);
    if (helm.lt > 0.3) { c.split += dt; if (c.split >= W.m.splitSeconds) { splitNow(W, 'Helm'); return; } } else c.split = 0;
    colonyLance(W, O, lance, dt);
    c.shield = ward.lt > 0.3; if (c.shield) smashNear(W, O.pos, W.m.shieldRadius, 0);
    contacts(W, O, dt, true);
    // the digest: one crystal of the hold into the score every digestSeconds; the hold empty, the colony splits itself
    if (c.hold > 0) { c.digest += dt; if (c.digest >= W.m.digestSeconds) { c.digest = 0; c.hold--; const idx = c.held.shift(); if (idx != null) W.crystals[idx].state = 'digested'; W.digested++; W.events.push('Digested: ' + W.digested + ' / ' + W.track.target); if (W.digested >= W.track.target) { W.finishT = W.t; W.events.push('Tide Run done in ' + W.t.toFixed(1) + ' s'); } } }
    W.fusedT += dt; O.stats.top = Math.max(O.stats.top, O.speed);
    if (c.hold <= 0 && W.policy !== 'always' && W.finishT == null) { const goal = c.wallGoal && !c.wallGoal.broken ? c.wallGoal : nearestWallAhead(W, O, 200); if (!goal) splitNow(W, 'hold empty'); }
  }
  function nearestWallAhead(W, B, range) { const fwd = fwdOf(B); let best = null, bd = range; for (const w of W.walls) { if (w.broken) continue; const d = w.centre.clone().sub(B.pos), along = d.dot(fwd); if (along < -20) continue; const dist = d.length(); if (dist < bd) { bd = dist; best = w; } } return best; }
  // ---- one step of the world: inputs is [zooid 0, 1, 2] apart, [Helm, Lance, Ward] combined ----
  function stepWorld(W, dt, inputs) {
    W.t += dt; const c = W.crew;
    if (c.form === 'colony') stepColony(W, inputs, dt);
    else {
      if (c.form === 'gather') {
        c.gather += dt; const gs = gatherSeat(W), lead = W.bodies[gs]; stepZooid(W, lead, inputs[gs], dt);
        const k = clamp(c.gather / Math.max(0.1, W.m.fuseSeconds), 0, 1), e = k * k * (3 - 2 * k);
        W.bodies.forEach((B, i) => { if (i === gs) return; const slot = slotOf(W, i); B.pos.lerp(slot, Math.min(1, dt * (2 + 10 * e))); B.quat.slerp(lead.quat, Math.min(1, dt * 4)); B.course.copy(lead.course); B.speed = lead.speed; B.vel.copy(lead.vel); B.K.active = false; B.aiState = 'docking'; });
        if (k >= 1) completeFuse(W);
      } else W.bodies.forEach((B, i) => stepZooid(W, B, inputs[i], dt));
      W.apartT += dt;
    }
    for (const s of W.shots) { if (s.dead) continue; s.t -= dt; if (s.t <= 0) s.dead = true; }
    if (W.shots.length > 60) W.shots = W.shots.filter((s) => !s.dead);
    let dead = 0; for (const p of W.prisms) { if (p.flash > 0) { p.flash = Math.max(0, p.flash - dt * 3); W.dirty = true; } if (p.dead) dead++; }
    if (dead > 200) { W.prisms = W.prisms.filter((p) => !p.dead); W.dirty = true; }
    if (!W.over && (W.finishT != null || W.t > BASE.limit)) W.over = true;
  }
  const bodiesNow = (W) => W.crew.form === 'colony' ? [W.crew.colony] : W.bodies;
  Object.assign(P, { sticks, steer, fly, edge, zooidMove, stepZooid, stepColony, stepWorld, beginGather, splitNow, slotOf, gatherPoint, gatherSeat, dockProgress, bodiesNow, nearestWallAhead, lightNear });
})();

// ================= part C: the AI (input-only, D4): each zooid's one job, the colony's three seats, the fuse policy =================
(function () {
  'use strict';
  const P = window.__siphParts, { V, BASE, DEG, lerp, clamp, easeInv, tmpArr, fwdOf, upOf, rightOf, nearestIndex, slotOf, bodiesNow, windowOf, nearestWallAhead, gatherSeat } = P;
  const NO_INPUT = () => ({ pad: true, L: { x: 0, y: 0 }, R: { x: 0, y: 0 }, lt: 0, rt: 0, a: false, b: false, ai: true });

  function aimSticks(B, aim) {
    const fwd = fwdOf(B), up = upOf(B), right = rightOf(B);
    const dir = aim.clone().sub(B.pos).normalize(), x = dir.dot(right), y = dir.dot(up), z = dir.dot(fwd);
    const yawErr = Math.atan2(x, z), pitchErr = Math.atan2(y, Math.max(0.05, Math.hypot(x, z)) * (z < 0 ? -1 : 1));
    const yawCmd = clamp(yawErr * 2.4, -1, 1), pitchCmd = clamp(pitchErr * 2.4, -1, 1), rollCmd = clamp(-right.y * 2, -1, 1);
    const sx = easeInv(yawCmd), sy = easeInv(pitchCmd), dy = easeInv(rollCmd), dx = Math.min(2, 2 - Math.abs(sx));
    return { L: { x: (sx - dx) / 2, y: clamp((sy - dy) / 2, -1, 1) }, R: { x: (sx + dx) / 2, y: clamp((sy + dy) / 2, -1, 1) }, err: Math.acos(clamp(z, -1, 1)) / DEG };
  }
  // the handicap: SkimRaceHandicap's two mistakes applied to what a zooid BELIEVES about its crystal (D24)
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
  // each zooid's target is the crystal its ONE verb applies to: the Bell a dark one, the Sting a lit one, the Bract a freed one
  function assignTargets(W) {
    const win = windowOf(W);
    for (const B of W.bodies) {
      const want = B.z.can === 'light' ? 'dark' : B.z.can === 'open' ? 'lit' : 'freed';
      let best = null, bd = Infinity;
      for (const c of win) { if (c.state !== want) continue; if (c.wall && !c.wall.broken) continue; const d = c.pos.distanceToSquared(B.pos); if (d < bd) { bd = d; best = c; } }
      B.target = best ? best.i : -1; B.toWall = false;
    }
  }
  const lookAhead = (W, B, lookU) => { const n = W.track.line.length; B.hint = nearestIndex(W.track, B.pos, B.hint || 0, false); const look = clamp(Math.round((24 + B.speed * 0.5 + (lookU || 0)) / BASE.blockSpacing), 2, 16); return W.track.line[(B.hint + look) % n].clone().addScaledVector(new V(0, 1, 0), 11); };
  function massAhead(W, B, range) { const fwd = fwdOf(B); let n = 0; for (const p of W.grid.near(B.pos.clone().addScaledVector(fwd, range / 2), range / 2 + 10, tmpArr)) { if (p.kind !== 'mass' && p.kind !== 'rail') continue; const d = p.pos.clone().sub(B.pos), along = d.dot(fwd); if (along < 4 || along > range) continue; if (Math.sqrt(Math.max(0, d.lengthSq() - along * along)) < 14) n++; } return n; }
  function zooidAI(W, B) {
    const c = W.crew, inp = NO_INPUT();
    if (c.form === 'gather') {
      const slot = slotOf(W, B.i), d = slot.distanceTo(B.pos);
      const s = aimSticks(B, d > 6 ? slot : slot.clone().addScaledVector(fwdOf(P.gatherPoint(W)), 40)); inp.L = s.L; inp.R = s.R; B.aim = slot; B.aimWhy = 'gather'; B.aiState = 'docking'; return inp;
    }
    if (c.calling && B.i !== gatherSeat(W)) {
      const L = W.bodies[gatherSeat(W)], at = L.pos.clone().addScaledVector(fwdOf(L), -12); const s = aimSticks(B, at); inp.L = s.L; inp.R = s.R; B.aim = at; B.aimWhy = 'gather'; B.aiState = 'called to fuse'; B.believed = null; return inp;
    }
    const target = B.target >= 0 ? W.crystals[B.target] : null; let b = believe(W, B, target); B.believed = b;
    if (b && B.toWall && target && target.wall) { const ap = target.wall.centre.clone().addScaledVector(fwdOf({ quat: target.wall.quat }), -90); b = B.pos.distanceTo(ap) < 40 ? ap.clone().addScaledVector(rightOf(B), 80) : ap; }
    let aim, why;
    if (b) {
      aim = b.clone(); why = B.z.can === 'light' ? 'to a dark crystal' : B.z.can === 'open' ? 'to a lit crystal' : 'to a freed crystal';
      if (B.z.can === 'open') {   // the Sting shoots down a straight line: too close and off the line, it stands off to 95 u and comes back straight
        const d = b.distanceTo(B.pos), errTo = Math.acos(clamp(b.clone().sub(B.pos).normalize().dot(fwdOf(B)), -1, 1)) / DEG;
        if (B.K.standoff) { if (d > 95) B.K.standoff = false; else { aim = B.pos.clone().add(B.pos.clone().sub(b).normalize().multiplyScalar(120)); why = 'standing off'; } }
        else if (d < 45 && errTo > 12) B.K.standoff = true;
      }
    }
    else { const L = W.bodies[(B.i + 2) % 3]; aim = L.pos.clone().addScaledVector(fwdOf(L), -14).addScaledVector(rightOf(L), B.i === 1 ? 18 : -18); why = 'with the ' + L.name; }   // nothing for it: shadow the zooid before it in the loop
    const s = aimSticks(B, aim); inp.L = s.L; inp.R = s.R; B.aim = aim; B.aimWhy = why;
    if (W.aiMoves !== false) {
      const h = B.hc, late = h ? h.level.press : 0; let want = false;
      if (B.z.can === 'light') want = s.err < 15 && B.K.cool <= 0 && !!b && b.distanceTo(B.pos) > W.m.lanternRange * 1.2 && b.distanceTo(B.pos) < W.m.lanternRange * 3;
      else if (B.z.can === 'open') want = !!b && !B.K.standoff && b.distanceTo(B.pos) < B.z.move.range * 0.9 && s.err < 12;
      else if (B.z.can === 'catch') want = !!b && b.distanceTo(B.pos) < W.m.netRadius * 0.9;
      if (want && !B.K._wantOn && h) h.pressAt = W.t + late * lerp(0.5, 1.5, h.rng()); B.K._wantOn = want;
      if (want && (!late || W.t >= (h ? h.pressAt : 0))) inp.rt = 1;
      if (B.z.can === 'catch' && massAhead(W, B, 30) > 1) inp.lt = 1;
    }
    B.aiState = h(B) ? (B.hc.unnoticed ? 'not noticed' : B.hc.misjudged ? 'misjudged' : B.K.active ? B.z.move.name : why) : why;
    return inp;
  }
  const h = (B) => !!B.hc;
  function colonyAI(W, seat) {
    const c = W.crew, O = c.colony, inp = NO_INPUT(); if (!O) return inp;
    const wall = (c.wallGoal && !c.wallGoal.broken) ? c.wallGoal : nearestWallAhead(W, O, 700);
    if (seat === 0) {   // Helm: to the next wall while the hold digests, else along the line; ram when the wall is close ahead
      let aim = lookAhead(W, O), why = 'line';
      if (wall) { const d = O.pos.distanceTo(wall.centre); aim = d > 130 ? wall.centre.clone().addScaledVector(fwdOf({ quat: wall.quat }), -90) : wall.centre.clone(); why = 'to the wall'; }
      const s = aimSticks(O, aim); inp.L = s.L; inp.R = s.R; O.aim = aim; O.aimWhy = why; O.aiState = why;
      if (wall && O.pos.distanceTo(wall.centre) < 70 && s.err < 10 && O.K.cool <= 0) inp.rt = 1;
      if (W.policy === 'crew' && c.hold === 0 && !wall && W.t - (c.fusedAt || 0) > 1) inp.lt = 1;
    } else if (seat === 1) {   // Lance: onto the nearest wall in range and fire; else the mass ahead
      let at = null; if (wall && O.pos.distanceTo(wall.centre) < W.m.lanceRange * 1.5) at = wall.centre; else if (massAhead(W, O, 120) > 0) at = O.pos.clone().addScaledVector(fwdOf(O), 60);
      if (at) { const dir = at.clone().sub(O.pos).normalize(), fwd = fwdOf(O), right = rightOf(O), up = upOf(O); const wantYaw = Math.atan2(dir.dot(right), Math.max(0.05, dir.dot(fwd))), wantPitch = Math.asin(clamp(dir.dot(up), -1, 1)); inp.R = { x: clamp((wantYaw - c.aim.yaw) * 3, -1, 1), y: clamp((wantPitch - c.aim.pitch) * 3, -1, 1) }; const err = Math.abs(wantYaw - c.aim.yaw) + Math.abs(wantPitch - c.aim.pitch); inp.rt = err < 0.25 ? 1 : 0; }
    } else inp.lt = massAhead(W, O, 45) > 2 ? 1 : 0;   // Ward: the shield when mass is about to hit
    return inp;
  }
  // the fuse policy for the seats the AI holds: 'crew' decides, 'never' stays apart (nothing ever scores), 'always' fuses at once and never splits
  function wantFuse(W, humanSeat) {
    const c = W.crew; if (c.form !== 'apart' || W.policy === 'never') return false;
    if (W.policy === 'always') return true;
    if (humanSeat >= 0 && W.aiFuse === false) return false;
    const win = windowOf(W), walled = win.length && win[0].wall && !win[0].wall.broken ? win[0].wall : null;
    if (c.callWall && !c.callWall.broken) return true;   // once called for a wall, the call stands until the fuse
    if (c.hold >= W.m.holdCap) { return true; }
    const workable = win.some((x) => x.state !== 'held' && !(x.wall && !x.wall.broken));
    if (walled && !workable) { c.callWall = walled; return true; }   // only the colony opens a wall: fuse for it, with or without a hold
    if (c.hold > 0 && !workable) return true;
    return false;
  }
  function crewPolicy(W, humanSeat) {
    const c = W.crew; if (c.form !== 'apart' || !c.calling) return;
    const near = W.bodies.every((B) => B.pos.distanceTo(W.bodies[gatherSeat(W)].pos) < W.m.gatherRange);
    if (near) P.beginGather(W);
  }
  function runHeadless(course, level, seed, o) {
    o = o || {};
    const W = P.makeWorld(course, [level, level, level], seed, { massDensity: o.massDensity, policy: o.policy || 'crew', lv: o.lv, nobody: true });
    W.aiMoves = o.aiMoves !== false; W.aiFuse = true; W.watch = -1;
    const dt = 1 / 30; let n = 0, sp = 0;
    if (W.policy === 'always') { W.crew.hold = 0; P.beginGather(W); }
    while (!W.over) { stepAll(W, dt, -1, null); n++; const bs = bodiesNow(W); sp += bs.reduce((a, B) => a + B.speed, 0) / bs.length; }
    return { finish: W.finishT, digested: W.digested, walls: W.wallsBroken, fusedShare: +(W.fusedT / Math.max(1e-6, W.t)).toFixed(3), fuses: W.crew.fuses, avgSpeed: +(sp / n).toFixed(1), uses: W.bodies.reduce((a, B) => a + B.stats.uses, 0) + W.crew.uses };
  }
  function stepAll(W, dt, humanSeat, humanInput) {
    const c = W.crew; let inputs; c.humanSeat = humanSeat;
    if (c.form === 'colony') inputs = [0, 1, 2].map((s) => s === humanSeat && humanInput ? humanInput : colonyAI(W, s));
    else {
      assignTargets(W);
      const humanCalls = humanSeat >= 0 && humanInput && humanInput.lt > 0.3 && humanSeat !== 2;   // the Bract's LT is its ward; it calls with the swap button... no: the Bract calls too when its hold is full
      const bractCalls = humanSeat === 2 && humanInput && humanInput.lt > 0.3 && c.hold > 0;
      c.calling = c.form === 'apart' && (humanCalls || bractCalls || wantFuse(W, humanSeat));
      inputs = [0, 1, 2].map((i) => i === humanSeat && humanInput ? humanInput : zooidAI(W, W.bodies[i]));
      crewPolicy(W, humanSeat);
      if (c.form === 'gather') for (let i = 0; i < 3; i++) if (i !== humanSeat) inputs[i] = zooidAI(W, W.bodies[i]);
    }
    P.stepWorld(W, dt, inputs);
  }
  Object.assign(P, { aimSticks, believe, assignTargets, zooidAI, colonyAI, crewPolicy, runHeadless, stepAll, NO_INPUT, wantFuse });
})();

// ================= part D: rendering (the zooids, the colony, the crystals in their husks, the walls) and the cameras (D16) =================
(function () {
  'use strict';
  const P = window.__siphParts, { T, V, Q, BASE, S, ZOOIDS, COLONY, SEATS, clamp, lerp, fwdOf, upOf, rightOf, bodiesNow, slotOf, windowOf, $ } = P;
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
  const COL = { track: new T.Color(0x3a4380), mass: new T.Color(0x56608f), wall: new T.Color(0x8fa6ff), husk: new T.Color(0x6a7a9a), huskLit: new T.Color(0xd8e6ff), rail: new T.Color(0x9be7ff) };
  const WHITE = new T.Color(0xffffff), m4 = new T.Matrix4(), tmpC = new T.Color(), sc = new V();
  function drawPrisms(W) {
    let n = 0, ns = 0;
    for (const p of W.prisms) {
      if (p.dead || n >= MAXP) continue;
      sc.set(p.scale[0], p.scale[1], p.scale[2]); m4.compose(p.pos, p.quat, sc); prismMesh.setMatrixAt(n, m4);
      tmpC.copy(p.kind === 'husk' ? (p.crystal.state === 'dark' ? COL.husk : COL.huskLit) : (COL[p.domain] || COL.track)); if (p.flash > 0) tmpC.lerp(WHITE, p.flash); prismMesh.setColorAt(n, tmpC); n++;
      if (p.shield && ns < MAXP && !(p.kind === 'husk' && p.crystal.state === 'dark')) { sc.multiplyScalar(1.25); m4.compose(p.pos, p.quat, sc); shieldMesh.setMatrixAt(ns++, m4); }
    }
    prismMesh.count = n; shieldMesh.count = ns; prismMesh.instanceMatrix.needsUpdate = true; shieldMesh.instanceMatrix.needsUpdate = true;
    if (prismMesh.instanceColor) prismMesh.instanceColor.needsUpdate = true; W.dirty = false;
  }
  // ---- the zooids' bodies and the colony: lab stand-ins (each a part, none a hull of its own) ----
  const mat = (color) => new T.MeshStandardMaterial({ color, roughness: 0.4, metalness: 0.25, emissive: new T.Color(color).multiplyScalar(0.2), flatShading: true });
  const box = (m, w, h, d, x, y, z) => { const b = new T.Mesh(new T.BoxGeometry(w, h, d), m); b.position.set(x, y, z); return b; };
  function zooidHull(z) {
    const m = mat(z.color), g = new T.Group();
    switch (z.body) {
      case 'bell': { const dome = new T.Mesh(new T.SphereGeometry(3.6, 10, 7, 0, Math.PI * 2, 0, Math.PI * 0.6), m); dome.rotation.x = -Math.PI / 2; g.add(dome); const skirt = new T.Mesh(new T.CylinderGeometry(3.4, 2.2, 3, 10, 1, true), new T.MeshStandardMaterial({ color: z.color, transparent: true, opacity: 0.5, side: T.DoubleSide, flatShading: true })); skirt.rotation.x = Math.PI / 2; skirt.position.z = 2.5; g.add(skirt); const lamp = new T.Mesh(new T.SphereGeometry(1.1, 8, 6), new T.MeshBasicMaterial({ color: 0xffffff })); lamp.name = 'lamp'; lamp.position.z = -2.6; g.add(lamp); break; }
      case 'sting': { const c = new T.Mesh(new T.ConeGeometry(1.6, 9, 6), m); c.rotation.x = -Math.PI / 2; g.add(c); for (let k = 0; k < 3; k++) { const ln = new T.Line(new T.BufferGeometry().setFromPoints([new V(0, 0, 3), new V(Math.cos(k * 2.1) * 2, Math.sin(k * 2.1) * 2 - 1, 16)]), new T.LineBasicMaterial({ color: z.color, transparent: true, opacity: 0.7 })); g.add(ln); } break; }
      case 'bract': { const s = new T.Shape(); s.moveTo(0, -6); s.quadraticCurveTo(6, -2, 5, 4); s.quadraticCurveTo(0, 7, -5, 4); s.quadraticCurveTo(-6, -2, 0, -6); const leaf = new T.Mesh(new T.ExtrudeGeometry(s, { depth: 0.6, bevelEnabled: false }), m); leaf.rotation.x = -Math.PI / 2; leaf.position.y = -0.3; g.add(leaf); g.add(box(m, 1.2, 1.6, 7, 0, 0.8, 0)); const net = new T.Mesh(new T.TorusGeometry(1, 0.06, 6, 24), new T.MeshBasicMaterial({ color: z.color, transparent: true, opacity: 0.6 })); net.name = 'net'; net.visible = false; g.add(net); break; }
    }
    return g;
  }
  const view = { hulls: [], crystals: [], lines: [], float: null, struts: null, aimLine: null, shots: null, lantern: null };
  function mountWorld(W) {
    { const ln = W.track.line, lo = new V(Infinity, Infinity, Infinity), hi = new V(-Infinity, -Infinity, -Infinity); for (const q of ln) { lo.min(q); hi.max(q); } const ctr = lo.clone().add(hi).multiplyScalar(0.5); let rad = 0; for (const q of ln) rad = Math.max(rad, Math.hypot(q.x - ctr.x, q.z - ctr.z)); ENV.placeField(ctr, (rad + 200) / 0.75, Math.max(160, (hi.y - lo.y) * 0.5 + 140)); }
    for (const o of [...view.hulls, ...view.crystals, ...view.lines, view.float, view.struts, view.aimLine, view.shots, view.lantern]) if (o) scene.remove(o);
    view.hulls = []; view.crystals = []; view.lines = [];
    for (const B of W.bodies) {
      const hh = zooidHull(B.z); scene.add(hh); view.hulls.push(hh);
      const lg = new T.BufferGeometry().setFromPoints([new V(), new V(), new V(), new V()]);
      const ln = new T.LineSegments(lg, new T.LineBasicMaterial({ color: B.z.color, transparent: true, opacity: 0.85 })); ln.frustumCulled = false; scene.add(ln); view.lines.push(ln);
    }
    for (let i = 0; i < 3; i++) { const c = VesselStudioLook.crystal(0xffffff, 7); scene.add(c); view.crystals.push(c); }
    view.float = new T.Mesh(new T.SphereGeometry(5, 10, 8), new T.MeshStandardMaterial({ color: 0xdff4ff, transparent: true, opacity: 0.55, roughness: 0.3, flatShading: true })); view.float.visible = false; scene.add(view.float);
    view.struts = new T.Group(); const sm = mat(0xffffff); view.struts.add(box(sm, 0.8, 14, 0.8, 0, 0, 0)); view.struts.add(box(sm, 0.8, 0.8, 12, 0, -2, -1)); view.struts.visible = false; scene.add(view.struts);
    view.aimLine = new T.Line(new T.BufferGeometry().setFromPoints([new V(), new V()]), new T.LineBasicMaterial({ color: 0xff6fb0, transparent: true, opacity: 0.6 })); view.aimLine.frustumCulled = false; view.aimLine.visible = false; scene.add(view.aimLine);
    view.shots = new T.LineSegments(new T.BufferGeometry().setFromPoints(Array.from({ length: 120 }, () => new V())), new T.LineBasicMaterial({ color: 0xffe28a, transparent: true, opacity: 0.9 })); view.shots.frustumCulled = false; scene.add(view.shots);
    view.lantern = new T.Mesh(new T.SphereGeometry(1, 16, 12), new T.MeshBasicMaterial({ color: 0x9be7ff, transparent: true, opacity: 0.06, side: T.BackSide, depthWrite: false })); scene.add(view.lantern);
  }
  function drawWorld(W, t) {
    if (W.dirty) drawPrisms(W);
    const c = W.crew, O = c.colony;
    W.bodies.forEach((B, i) => {
      const hh = view.hulls[i];
      if (O) { const slot = slotOf(W, i), g = P.gatherPoint(W); hh.position.copy(O.pos).add(slot.clone().sub(g.pos)); hh.quaternion.copy(O.quat); hh.scale.setScalar(1.15); }
      else { hh.position.copy(B.pos); hh.quaternion.copy(B.quat); hh.scale.setScalar(1); }
      hh.visible = true;
      const net = hh.getObjectByName('net'); if (net) { net.visible = !O && B.K.netT > 0; if (net.visible) { const k = 1 - B.K.netT / 0.35; net.scale.setScalar(W.m.netRadius * (0.3 + 0.7 * k)); net.material.opacity = 0.7 * (1 - k); } }
      const lamp = hh.getObjectByName('lamp'); if (lamp) lamp.material.color.setHSL(0.55, 0.3, B.K.pulseT > 0 ? 1 : 0.75);
      const ln = view.lines[i], show = S.thinking && !O && !!B.aim && B.lastInp && B.lastInp.ai && W.finishT == null; ln.visible = show;
      if (show) { const a = ln.geometry.attributes.position, b = B.believed || B.pos; a.setXYZ(0, B.pos.x, B.pos.y, B.pos.z); a.setXYZ(1, B.aim.x, B.aim.y, B.aim.z); a.setXYZ(2, B.pos.x, B.pos.y + 3, B.pos.z); a.setXYZ(3, b.x, b.y + 3, b.z); a.needsUpdate = true; ln.material.color.set(B.aimWhy === 'gather' ? 0xb98bff : B.hc && B.hc.misjudged ? 0xff5c6c : B.hc && B.hc.unnoticed ? 0xffb454 : 0x6fe39a); }
    });
    view.float.visible = !!O; view.struts.visible = !!O; if (O) { view.float.position.copy(O.pos).addScaledVector(upOf(O), 12); view.struts.position.copy(O.pos); view.struts.quaternion.copy(O.quat); }
    view.aimLine.visible = !!O && !!O.aimDir; if (O && O.aimDir) view.aimLine.geometry.setFromPoints([O.pos.clone(), O.pos.clone().addScaledVector(O.aimDir, W.m.lanceRange)]);
    const bell = W.bodies[0]; view.lantern.visible = !O; if (!O) { view.lantern.position.copy(bell.pos); view.lantern.scale.setScalar(W.m.lanternRange * (bell.K.pulseT > 0 ? 2 : 1)); }
    const win = windowOf(W);
    win.forEach((cr, i) => { const m = view.crystals[i]; m.visible = cr.state !== 'held'; m.position.copy(cr.pos); VesselStudioLook.spinCrystal(m, t + i); const col = cr.state === 'dark' ? 0x3c4664 : cr.state === 'lit' ? 0xd8e6ff : 0xffffff; m.traverse((o) => { if (o.material && o.material.color) o.material.color.set(col); }); m.scale.setScalar(cr.state === 'dark' ? 0.55 : 1); });
    for (let i = win.length; i < 3; i++) view.crystals[i].visible = false;
    const sp = view.shots.geometry.attributes.position; let n = 0;
    for (const s of W.shots) { if (s.dead || n >= 118) continue; sp.setXYZ(n++, s.a.x, s.a.y, s.a.z); sp.setXYZ(n++, s.b.x, s.b.y, s.b.z); }
    view.shots.geometry.setDrawRange(0, n); sp.needsUpdate = true;
  }
  // ---- cameras: Chase (the body you fly), Follow (the viewed body, wide), Free (orbit) ----
  const camPos = new V(), camLook = new V(), free = { yaw: 2.6, pitch: 0.35, dist: 110 };
  function flown(W) { const c = W.crew; return c.form === 'colony' ? c.colony : W.bodies[Math.min(c.seat, 2)]; }
  function viewedBody(W) { const c = W.crew; if (c.form === 'colony') return c.colony; return W.bodies[Math.min(S.view, 2)] || W.bodies[0]; }
  function placeCamera(W, dt, snap, keys) {
    let tag; const B = S.cam === 'Chase' ? flown(W) : viewedBody(W), name = B.colony ? 'the colony' : 'the ' + B.name;
    if (S.cam === 'Free') {
      const k = (c) => keys.has(c) ? 1 : 0;
      free.yaw += (k('KeyJ') - k('KeyL')) * 1.4 * dt; free.pitch = clamp(free.pitch + (k('KeyI') - k('KeyK')) * 1.1 * dt, -1.3, 1.3); free.dist = clamp(free.dist * (1 + (k('KeyO') - k('KeyU')) * 1.2 * dt), 25, 3000);
      const want = new V(Math.sin(free.yaw) * Math.cos(free.pitch), Math.sin(free.pitch), Math.cos(free.yaw) * Math.cos(free.pitch)).multiplyScalar(free.dist).add(B.pos);
      cam.position.lerp(want, snap ? 1 : 1 - Math.exp(-12 * dt)); cam.up.set(0, 1, 0); cam.lookAt(B.pos); tag = 'Free: orbiting ' + name + ' (drag, wheel)';
    } else {
      const fwd = fwdOf(B), up = upOf(B), behind = B.course.clone(); if (behind.lengthSq() < 1e-4) behind.copy(fwd);
      const wide = S.cam === 'Follow', big = B.colony ? 1.8 : 1;
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
  const P = window.__siphParts, { T, V, BASE, COURSE_INFO, DIFF, S, LS, save, par, mults, styleOf, ZOOIDS, COLONY, DEFAULTS, SPEC, ELEMENTS, STYLES, KIT, KIT_COMBOS, ELS, EL_COLOR, SEATS, ROLES, clamp, lerp, elx, $, bodiesNow, flown, canvas, renderer, scene, cam, ENV, mark, windowOf, colonyFlight } = P;
  const fmt = (x) => (Math.abs(x) >= 100 ? x.toFixed(0) : Math.abs(x) >= 10 ? x.toFixed(1) : x.toFixed(2)).replace(/\.0+$/, '');

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
    W = P.makeWorld(S.course, S.levels, seedN++, { massDensity: S.massDensity, policy: 'crew', lv: S.lv, nobody: S.nobody, seat: humanSeat() < 0 ? 0 : humanSeat() });
    W.aiMoves = S.aiMoves; W.aiFuse = S.aiFuse; W.crew.seat = Math.max(0, humanSeat());
    P.mountWorld(W); P.drawWorld(W, 0); P.placeCamera(W, 0, true, keys);
    run = 'ready'; paused = false; feed.length = 0; renderFeed(); lastHumanT = performance.now() / 1000; aiHolding = false;
    $('ovTitle').textContent = 'Siphonophore \u00b7 Tide Run, intensity ' + S.course; $('startBtn').textContent = 'Start'; $('overlay').hidden = false;
    $('courseInfo').textContent = 'I' + S.course + ' \u00b7 ' + COURSE_INFO[S.course] + ' \u00b7 ' + W.track.target + ' crystals, ' + W.walls.length + ' walls';
    syncSeats();
  }
  function startRun() { if (run === 'finished') newRun(); run = 'running'; paused = false; stopHeld = false; $('overlay').hidden = true; lastHumanT = performance.now() / 1000; canvas.focus({ preventScroll: true }); }
  function stopRun() { newRun(); stopHeld = true; }
  function togglePause() { if (run !== 'running') return; paused = !paused; say(paused ? 'Paused' : 'Resumed'); }
  function finishRun() {
    run = 'finished'; const t = W.finishT;
    runs.unshift({ course: S.course, you: S.nobody ? 'nobody' : ZOOIDS[humanSeat()].name, time: t, walls: W.wallsBroken + '/' + W.walls.length, fused: Math.round(W.fusedT / Math.max(1e-6, W.t) * 100), fuses: W.crew.fuses, swaps: W.swaps });
    runs.length = Math.min(runs.length, 40); saveRuns(); renderRuns();
    $('ovTitle').textContent = t != null ? 'Tide Run \u00b7 ' + t.toFixed(1) + ' s' : 'Run over'; $('ovText').textContent = W.digested + ' / ' + W.track.target + ' crystals digested, ' + W.wallsBroken + ' / ' + W.walls.length + ' walls, the colony ' + Math.round(W.fusedT / Math.max(1e-6, W.t) * 100) + '% of the time over ' + W.crew.fuses + ' fuse(s), ' + W.swaps + ' seat swap(s).';
    $('startBtn').textContent = 'Run again'; $('overlay').hidden = false;
  }
  function swapSeat(dir) {
    if (!W || S.nobody) return; const c = W.crew; const next = ((c.seat + dir) % 3 + 3) % 3; c.seat = next; S.seat = next; save(); W.swaps++;
    say('You: ' + (c.form === 'colony' ? SEATS[next] + ' seat' : 'the ' + ZOOIDS[next].name)); syncSeats();
  }
  function cycleCamera() { S.cam = S.cam === 'Chase' ? 'Follow' : S.cam === 'Follow' ? 'Free' : 'Chase'; $('camSel').value = S.cam; save(); }
  function say(m) { feed.unshift(m); feed.length = Math.min(feed.length, 4); renderFeed(); }
  function renderFeed() { const el = $('feed'); el.textContent = ''; for (const m of feed) el.appendChild(elx('div', null, m)); }

  // ---- the panels ----
  const courseSel = $('courseSel'); [1, 2, 3, 4].forEach((v) => { const o = elx('option', null, 'I' + v + ' \u00b7 ' + COURSE_INFO[v]); o.value = v; courseSel.appendChild(o); }); courseSel.value = S.course;
  courseSel.addEventListener('change', () => { S.course = +courseSel.value; save(); newRun(); });
  function renderCrew() {
    const tb = $('members'); tb.textContent = '';
    ZOOIDS.forEach((z, i) => { const tr = elx('tr'); tr.append(elx('td', null, z.role), elx('td', null, z.name), elx('td', null, z.can === 'light' ? 'lights dark crystals' : z.can === 'open' ? 'opens lit husks' : 'nets freed crystals'), elx('td', null, String(z.cruise)), elx('td', null, z.turn + '\u00b0/s'), elx('td', null, z.move.name + ': ' + z.move.info)); tb.appendChild(tr); });
    const F = colonyFlight(); $('colonyInfo').textContent = COLONY.lore + ' Cruise ' + Math.round(F.cruise) + ' u/s, turn ' + Math.round(F.turn) + '\u00b0/s (the zooids\' average x ' + COLONY.speed + ' and x ' + COLONY.turn + '). Seats: Helm (the Bell) flies and rams, Lance (the Sting) opens walls, Ward (the Bract) shields.';
    const vs = $('viewSel'); vs.textContent = ''; ZOOIDS.forEach((z, i) => { const o = elx('option', null, z.name); o.value = i; vs.appendChild(o); }); vs.value = Math.min(S.view, 2);
    for (const el of ELS) { const e = ELEMENTS[el], card = abCards[el]; card.nm.textContent = e.name; }
    syncSeats();
  }
  $('viewSel').addEventListener('change', (e) => { S.view = +e.target.value; save(); });
  $('camSel').value = S.cam; $('camSel').addEventListener('change', (e) => { S.cam = e.target.value; save(); });
  $('speedSel').value = String(S.speed); $('speedSel').addEventListener('change', (e) => { S.speed = +e.target.value; save(); });
  $('thinking').checked = S.thinking; $('thinking').addEventListener('change', (e) => { S.thinking = e.target.checked; save(); });
  $('massDensity').value = S.massDensity; $('massOut').textContent = S.massDensity + '\u00d7';
  $('massDensity').addEventListener('input', (e) => { S.massDensity = +e.target.value; $('massOut').textContent = S.massDensity + '\u00d7'; save(); }); $('massDensity').addEventListener('change', () => newRun());
  for (const [id, key] of [['aiMoves', 'aiMoves'], ['aiFuse', 'aiFuse'], ['idleHold', 'idleHold']]) { $(id).checked = S[key]; $(id).addEventListener('change', (e) => { S[key] = e.target.checked; save(); if (W) { W.aiMoves = S.aiMoves; W.aiFuse = S.aiFuse; } }); }
  function syncSeats() {
    const box = $('seats'); box.textContent = ''; const form = W ? W.crew.form : 'apart';
    ZOOIDS.forEach((z, i) => {
      const row = elx('div', 'seat'), dot = elx('span', 'sd'); dot.style.background = '#' + z.color.toString(16).padStart(6, '0');
      const who = elx('div'); who.append(elx('b', null, (form === 'colony' ? SEATS[i] + ' \u00b7 ' : '') + z.name + ' \u00b7 ' + z.role), elx('small', null, form === 'colony' ? (i === 0 ? 'flies the colony; RT ram pulse; hold LT to split' : i === 1 ? 'aims the lance (right stick); RT fires at walls' : 'LT holds the ward') : z.move.name + ' on RT' + (i === 2 ? '; LT ward' : '; LT calls the fuse')));
      const you = elx('button', 'btn small', !S.nobody && S.seat === i ? 'You' : 'Take'); you.type = 'button'; if (!S.nobody && S.seat === i) you.classList.add('primary');
      you.addEventListener('click', () => { S.nobody = false; S.seat = i; if (W) { W.crew.seat = i; W.swaps++; } save(); syncSeats(); });
      const lv = elx('select'); ['Easy', 'Medium', 'Hard'].forEach((l) => { const o = elx('option', null, 'AI ' + l); o.value = l; lv.appendChild(o); }); lv.value = S.levels[i] || 'Hard'; lv.setAttribute('aria-label', z.name + ' AI level');
      lv.addEventListener('change', () => { S.levels[i] = lv.value; save(); if (W) W.bodies[i].hc.level = DIFF[lv.value]; });
      row.append(dot, who, you, lv); box.appendChild(row);
    });
    const nb = elx('label', 'check'); const cb = document.createElement('input'); cb.type = 'checkbox'; cb.checked = S.nobody; cb.addEventListener('change', () => { S.nobody = cb.checked; save(); syncSeats(); }); nb.append(cb, document.createTextNode(' Nobody: the whole colony flies itself (watch it)')); box.appendChild(nb);
  }
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
  (function renderKit() {
    const STATE = { sim: 'in the sim', part: 'in part', new: 'proposal' }, host = $('kit'), ROLE = { Bell: 'mobility \u00b7 Space', Sting: 'attack \u00b7 Charge', Bract: 'defence \u00b7 Mass', Colony: 'fused \u00b7 Time' };
    let grp = null, last = '';
    for (const k of KIT) {
      if (k.part !== last) { last = k.part; grp = elx('div', 'kgrp'); grp.style.setProperty('--accent', 'var(' + EL_COLOR[k.el] + ')'); grp.appendChild(elx('h4', null, k.part + ' \u00b7 ' + ROLE[k.part])); host.appendChild(grp); }
      const card = elx('div', 'kcard'), top = elx('div', 'ktop');
      top.append(elx('span', 'kin', k.input), elx('span', 'kname', k.name), elx('span', 'kst ' + k.state, STATE[k.state]));
      card.append(top, elx('div', 'kwhat', k.what), elx('div', 'kfrom', 'from ' + k.from)); grp.appendChild(card);
    }
    const ul = $('kitCombos'); for (const c of KIT_COMBOS) ul.appendChild(elx('li', null, c));
  })();
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
    const rows = [['Gamepad', 'both sticks steer the zooid you fly \u00b7 RT: its one move (Bell pulse, Sting shot, Bract net) \u00b7 LT: Bell / Sting = hold to call the FUSE; Bract = ward (and the call, with a hold) \u00b7 D-pad left / right = swap seat'],
      ['Colony', 'Helm: sticks fly, RT ram pulse, hold LT to split \u00b7 Lance: right stick aims, RT fires at walls \u00b7 Ward: LT shield'],
      ['Keys', 'W S pitch \u00b7 A D yaw \u00b7 Q E roll \u00b7 Space = LT \u00b7 F = RT \u00b7 \u2190 \u2192 swap seat'],
      ['Free cam', 'orbits the viewed body: drag to turn \u00b7 wheel to zoom \u00b7 J L / I K turn \u00b7 U O zoom'], ['Transport', 'Enter play \u00b7 P pause / resume \u00b7 R stop \u00b7 C next camera'],
      ['Phone', 'opens by itself on a phone: two thumb sticks, LT / RT handles, a two-finger swipe (or the swap button) swaps seats']];
    for (const [k, v] of rows) box.append(elx('span', null, k), elx('span', null, v));
  })();
  $('trigDepth').value = String(S.trigDepth); $('trigDepth').addEventListener('change', (e) => { S.trigDepth = +e.target.value; save(); });
  let runs = []; try { runs = JSON.parse(localStorage.getItem(LS + '.runs') || '[]') || []; } catch (e) { runs = []; }
  function saveRuns() { try { localStorage.setItem(LS + '.runs', JSON.stringify(runs)); } catch (e) { /* blocked */ } }
  function renderRuns() {
    const tb = $('runs'); tb.textContent = '';
    if (!runs.length) { const tr = elx('tr'), td = elx('td', null, 'No runs yet. Finish a Tide Run to add one.'); td.colSpan = 7; td.style.color = 'var(--muted)'; tr.appendChild(td); tb.appendChild(tr); return; }
    for (const r of runs.slice(0, 12)) { const tr = elx('tr'); tr.append(elx('td', null, 'I' + r.course), elx('td', null, r.you), elx('td', null, r.time != null ? r.time.toFixed(1) : 'DNF'), elx('td', null, r.walls), elx('td', null, r.fused + '%'), elx('td', null, String(r.fuses)), elx('td', null, String(r.swaps))); tb.appendChild(tr); }
  }
  // the scorecard (D5): the colony on AI per level, and the two controls (never fuse, always fused): both must DNF, or the loop is not forced by design
  const COLS = ['Easy', 'Medium', 'Hard', 'Never', 'Always'];
  function renderScore(rows) {
    const tb = $('score'); tb.textContent = '';
    for (const c of [1, 2, 3, 4]) { const r = rows && rows[c], tr = elx('tr'); tr.append(elx('td', null, 'I' + c)); for (const k of COLS) tr.append(elx('td', null, r && k in r ? (r[k] != null ? r[k].toFixed(1) : 'DNF') : '\u00b7')); tr.append(elx('td', null, r && r.fused != null ? Math.round(r.fused * 100) + '%' : '\u00b7'), elx('td', null, r && r.fuses != null ? r.fuses : '\u00b7')); tb.appendChild(tr); }
  }
  let scoreRows = null; try { scoreRows = JSON.parse(localStorage.getItem(LS + '.score') || 'null'); } catch (e) { scoreRows = null; } renderScore(scoreRows);
  function runScorecard(seeds, done) {
    const rows = {}, jobs = []; for (const c of [1, 2, 3, 4]) { rows[c] = {}; for (const k of COLS) jobs.push([c, k]); }
    const med = (a) => { const v = a.filter((x) => x != null).sort((x, y) => x - y); return v.length > a.length / 2 ? v[Math.floor(v.length / 2)] : null; };
    let j = 0;
    const next = () => {
      if (j >= jobs.length) { done(rows); return; }
      const [c, k] = jobs[j++], rs = seeds.map((s) => P.runHeadless(c, k === 'Never' || k === 'Always' ? 'Hard' : k, s, { policy: k === 'Never' ? 'never' : k === 'Always' ? 'always' : 'crew', lv: S.lv, massDensity: S.massDensity }));
      rows[c][k] = med(rs.map((r) => r.finish));
      if (k === 'Hard') { rows[c].fused = rs.reduce((a, r) => a + r.fusedShare, 0) / rs.length; rows[c].fuses = rs.map((r) => r.fuses).join(' / '); }
      renderScore(rows); setTimeout(next, 0);
    };
    setTimeout(next, 20);
  }
  $('scoreBtn').addEventListener('click', () => { $('scoreBtn').disabled = true; $('scoreNote').textContent = 'Running the colony on every course at every level, then never fusing, then always fused\u2026'; runScorecard([11, 22, 33], (rows) => { scoreRows = rows; try { localStorage.setItem(LS + '.score', JSON.stringify(rows)); } catch (e) { /* blocked */ } renderScore(rows); $('scoreBtn').disabled = false; $('scoreNote').textContent = 'Median of three seeds per cell, seconds; DNF = no finish in ' + BASE.limit + ' s. Never fuse must DNF (nothing scores apart) and Always fused must DNF (the colony cannot light, open or catch): the two controls that prove the loop alternates by design. Hard should be fastest on every course (D24). "Fuses" counts the Hard runs\' colony phases per seed.'; }); });
  const LOCAL_LOG = LS + '.decisions'; let db = null, me = '', localLog = []; try { localLog = JSON.parse(localStorage.getItem(LOCAL_LOG) || '[]') || []; } catch (e) { localLog = []; }
  function settingsDiff() { const changed = {}; for (const k in S.params) changed[k] = S.params[k]; return { vessel: 'siphonophore', style: S.style, lv: Object.assign({}, S.lv), changed, course: S.course, massDensity: S.massDensity, seat: S.seat }; }
  function renderDecisions(shared) {
    const box = $('decList'); box.textContent = '';
    const all = (shared || []).concat(localLog.map((d) => Object.assign({ local: true }, d))).sort((a, b) => (b.createdAt || 0) - (a.createdAt || 0));
    if (!all.length) { box.appendChild(elx('p', 'note', 'No decisions yet for the Siphonophore. Record the first: what you tried, what you decided.')); return; }
    for (const d of all.slice(0, 30)) { const r = elx('div', 'dec'); r.append(elx('div', 'meta', (d.topic || 'Other') + ' \u00b7 ' + (d.createdAt ? new Date(d.createdAt).toLocaleString() : '') + (d.local ? ' \u00b7 this browser' : '')), elx('div', 'txt', d.choice + (d.note ? ' \u2014 ' + d.note : ''))); box.appendChild(r); }
  }
  (async function connectLog() {
    const c = window.claude; renderDecisions([]);
    if (!c || typeof c.use !== 'function') { $('decState').textContent = 'Kept in this browser (Copy log to send it). The shared log is in the claude.ai artifact:'; $('decState').appendChild(VesselStudioTheme.artifactLink()); return; }
    try { db = await c.use('db'); } catch (e) { db = null; }
    try { const u = await c.use('user'); if (u) me = (await u.id()) || ''; } catch (e) { me = ''; }
    if (!db) { $('decState').textContent = 'The shared log is unavailable here; decisions stay in this browser.'; return; }
    db.collection('decisions').orderBy('createdAt', 'desc').limit(300).onSnapshot((snap) => { renderDecisions(snap.docs.map((x) => Object.assign({ id: x.id }, x.data())).filter((d) => d.vessel === 'siphonophore' || (d.settings && d.settings.vessel === 'siphonophore'))); }, () => { $('decState').textContent = 'The shared log could not be read; decisions stay in this browser.'; });
  })();
  $('decBtn').addEventListener('click', async () => {
    const choice = $('decChoice').value.trim(); if (!choice) { $('decState').textContent = 'Say what you decided first.'; return; }
    const doc = { vessel: 'siphonophore', topic: $('decTopic').value, choice, note: $('decNote').value.trim(), settings: settingsDiff(), createdAt: Date.now(), by: me, kind: 'decision' };
    if (db) { try { await db.collection('decisions').add(doc); $('decState').textContent = 'Recorded in the shared log.'; $('decChoice').value = ''; $('decNote').value = ''; return; } catch (e) { /* fall through */ } }
    localLog.unshift(doc); try { localStorage.setItem(LOCAL_LOG, JSON.stringify(localLog)); } catch (e) { /* blocked */ } renderDecisions([]); $('decState').textContent = 'Recorded in this browser. Use Copy log to send it.'; $('decChoice').value = ''; $('decNote').value = '';
  });
  $('decCopy').addEventListener('click', async () => { const all = localLog.slice().reverse().map((d) => '- [' + d.topic + '] ' + d.choice + (d.note ? ' \u2014 ' + d.note : '') + ' ' + JSON.stringify(d.settings)).join('\n'); try { await navigator.clipboard.writeText('Siphonophore studio decisions\n' + all); $('decState').textContent = 'Copied.'; } catch (e) { $('decState').textContent = 'Could not copy here.'; } });
  $('aboutFantasy').textContent = 'A siphonophore is one animal made of parts that are not animals on their own: a swimming bell, stinging tentacles, shield leaves. In the HyperSea the Bell lights the dark, the Sting opens husks, the Bract nets the catch; only the colony digests it into the score and opens walls. Many bodies and one body, taking turns.';
  $('aboutWhy').textContent = 'The design principle (2026-10-27): a Voltron vessel is designed from the ground up to be both many separate vessels and one big vessel at alternating parts of its own game loop, never a combine bolted onto vessels that already have a complete loop (the Omega crews lab showed such a crew has no reason to split). Here no part can score alone and the colony cannot collect, so the loop alternates by design; the scorecard\'s two controls (never fuse, always fused) must both fail.';
  const notList = $('aboutNot'); for (const s of ['The zooids\' flight numbers and moves are lab proposals; nothing ships yet (the Unity clone comes by /artifact-to-unity once the loop is decided).', 'The bodies are stand-ins (a dome, a cone with tentacles, a leaf); the colony is the three docked under a float. Models come from /vessel-model.', 'Element scaling is a proposal: Charge venom per shot, Mass net radius, Space lantern range, Time digest speed; nothing is signed off.', 'The course is the game\'s Skim Race track; the husks, the dark, the walls and the digest are this lab\'s mini-game (the Tide Run), not a shipped mode.', 'No sound, no rivals, no lifeforms; PvP is petals only and there is no PvP here.']) notList.appendChild(elx('li', null, s));
  const srcList = $('aboutSources'); for (const s of ['Courses: MinigameSkimRace.unity via Tools/Build/skimrace_track_fingerprint.py --emit-track (VesselStudioEngine.TRACKS); the platform baseline (skim 12 u, boost cap 1.6x, hull reach) is studio-vessel.js BASE; difficulty: SkimRaceDifficulty.asset through DIFF.', 'The loop\'s numbers: Vessel Config > Loop numbers, every one a lab proposal (DEFAULTS in siphonophore-studio.js).', 'Seats and the swap: Docs/CREW_STATIONS.md (one gesture, seats then hulls); every empty seat an AI (the Orca\'s rule, ORCA_CREW.md).']) srcList.appendChild(elx('li', null, s));

  // ---- HUD ----
  function hud() {
    if (!W) return; const c = W.crew, B = S.cam === 'Chase' ? flown(W) : P.viewedBody(W), you = humanSeat();
    $('hWho').textContent = (B.colony ? 'The colony \u00b7 ' + SEATS[c.seat] + ' seat' : 'The ' + B.name + (you === B.i ? ' \u00b7 you' : ' \u00b7 AI ' + S.levels[B.i])) + ' \u00b7 speed u/s';
    $('hSpeed').textContent = Math.round(B.speed * B.slow);
    $('hSub').textContent = B.colony ? (c.seat === 0 ? 'ram pulse on RT' + (O_cool(c) > 0 ? ' \u00b7 ' + O_cool(c).toFixed(1) + ' s' : ' \u00b7 ready') + ' \u00b7 hold LT to split' : c.seat === 1 ? 'lance: ' + W.walls.filter((w) => !w.broken).length + ' wall(s) left' : 'ward' + (c.shield ? ' UP' : '')) : B.z.move.name + (B.K.cool > 0 ? ' \u00b7 ' + B.K.cool.toFixed(1) + ' s' : ' \u00b7 ready') + (B.z.can === 'catch' ? ' \u00b7 hold ' + c.hold + '/' + W.m.holdCap : '');
    $('hForm').textContent = c.form === 'colony' ? 'THE COLONY \u00b7 DIGESTING ' + c.hold : c.form === 'gather' ? 'FUSING \u00b7 ' + Math.round(P.dockProgress(W)) + ' u' : '';
    $('hHold').textContent = c.hold + ' / ' + W.m.holdCap; $('mHold').style.width = (c.hold / W.m.holdCap * 100) + '%';
    const gs = P.gatherSeat(W), near = c.form === 'apart' && W.bodies.every((X) => X.pos.distanceTo(W.bodies[gs].pos) < W.m.gatherRange);
    $('hM1Lbl').textContent = c.form === 'colony' ? 'Digest' : (near ? (c.hold > 0 ? 'In range: hold LT to fuse' : 'In range (nothing to digest)') : 'Zooids apart'); $('hM1').textContent = c.form === 'colony' ? (c.hold > 0 ? Math.round(c.digest / W.m.digestSeconds * 100) + '%' : 'done') : Math.round(Math.max(...W.bodies.map((X) => X.pos.distanceTo(W.bodies[gs].pos)))) + ' u'; $('mM1').style.width = c.form === 'colony' ? (c.hold > 0 ? c.digest / W.m.digestSeconds * 100 : 100) + '%' : clamp(1 - Math.max(...W.bodies.map((X) => X.pos.distanceTo(W.bodies[gs].pos))) / (W.m.gatherRange * 2), 0, 1) * 100 + '%';
    $('hBoost').textContent = B.boost.toFixed(2) + '\u00d7'; $('mBoost').style.width = ((B.boost - 1) / (BASE.maxBoost - 1) * 100) + '%';
    const b = $('board'); b.textContent = ''; const hd = elx('div', 'hd'); hd.append(elx('span', null, 'I' + S.course + ' \u00b7 ' + W.t.toFixed(1) + ' s'), elx('span', null, W.digested + '/' + W.track.target + ' \u00b7 walls ' + W.wallsBroken + '/' + W.walls.length)); b.appendChild(hd);
    ZOOIDS.forEach((z, i) => { const row = elx('div', 'pl' + (you === i && !S.nobody ? ' you' : '')), dot = elx('span', 'dot'); dot.style.background = '#' + z.color.toString(16).padStart(6, '0'); const X = W.bodies[i]; row.append(dot, elx('span', null, (c.form === 'colony' ? SEATS[i] + ' \u00b7 ' : '') + z.name), elx('span', 'st', you === i && !S.nobody ? (aiHolding ? 'AI holding' : 'you') : 'AI ' + S.levels[i] + (c.form === 'apart' && X.aiState ? ' \u00b7 ' + X.aiState : ''))); b.appendChild(row); });
    const win = windowOf(W).map((x) => 'crystal ' + (x.i + 1) + ' ' + x.state + (x.wall && !x.wall.broken ? ' (walled)' : '')).join(', '); b.appendChild(elx('div', 'st', 'window: ' + (win || 'none')));
    const m = W.m; for (const el of ELS) { const e = ELEMENTS[el], card = abCards[el]; card.st.textContent = fmt(m[e.param]) + (el === 'time' ? ' s' : el === 'charge' ? ' / shot' : ' u'); card.c.style.borderColor = W.lv[el] >= 5 ? 'var(' + EL_COLOR[el] + ')' : ''; }
    $('readWho').textContent = B.colony ? 'the colony' : 'the ' + B.name;
    const rows = [['speed', (B.speed * B.slow).toFixed(1) + ' u/s'], ['top speed', B.stats.top.toFixed(0) + ' u/s'], ['form', c.form], ['hold', c.hold + ' / ' + m.holdCap], ['digested', W.digested + ' / ' + W.track.target], ['lit', String(W.crystals.filter((x) => x.state !== 'dark').length)], ['freed', String(W.crystals.filter((x) => x.state === 'freed').length)], ['walls open', W.wallsBroken + ' / ' + W.walls.length], ['colony time', Math.round(W.fusedT / Math.max(1e-6, W.t) * 100) + '%'], ['fuses', String(c.fuses)], ['seat swaps', String(W.swaps)], ['moves used', String(W.bodies.reduce((a, X) => a + X.stats.uses, 0) + c.uses)], ['skims', String(W.bodies.reduce((a, X) => a + X.skims, 0))], ['hull contacts', String(W.bodies.reduce((a, X) => a + X.resets, 0) + ((c.colony && c.colony.resets) || 0))]];
    const box = $('read'); if (box.childElementCount !== rows.length * 2) { box.textContent = ''; for (const [k] of rows) box.append(elx('span', null, k), elx('b')); } rows.forEach(([, v], i) => { box.children[i * 2 + 1].textContent = v; });
    const live = run === 'running' && !paused; $('tpPlay').textContent = live ? '\u275a\u275a Pause' : '\u25b6 Play'; $('tpPlay').classList.toggle('on', live); $('tpPlay').setAttribute('aria-pressed', paused ? 'true' : 'false'); $('tpStop').disabled = run === 'ready' && !stopHeld; $('tStart').textContent = live ? '\u275a\u275a Pause' : '\u25b6 Play';
    $('hAlert').hidden = !(aiHolding && run === 'running'); $('hAlert').textContent = 'AI HOLDING YOUR SEAT \u00b7 touch any control to fly';
    while (W.events.length) say(W.events.shift());
  }
  const O_cool = (c) => (c.colony ? c.colony.K.cool : 0);
  function markObjective(W) {
    const B = S.cam === 'Chase' ? flown(W) : P.viewedBody(W); if (run !== 'running') { mark.hide(); return; }
    let target = null; if (!B.colony && B.target >= 0) target = W.crystals[B.target]; if (!target) target = windowOf(W)[0] || null;
    if (!target) { mark.hide(); return; } cam.updateMatrixWorld();
    const walled = target.wall && !target.wall.broken;
    mark.set(cam, target.pos, 'Crystal ' + (target.i + 1) + '/' + W.track.target + ' \u00b7 ' + target.state + (walled ? ', walled' : '') + ' \u00b7 ' + Math.round(target.pos.distanceTo(B.pos)) + ' u', walled ? '#8fa6ff' : target.state === 'dark' ? '#6a7a9a' : '#ffffff', canvas.clientWidth, canvas.clientHeight);
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
  const IDE = VesselStudioIDE.build({
    key: 'siphonophore-studio-ide-v1', host: document.querySelector('.layout'), stage: $('viewport'),
    right: [['scene', 'Scene Config', [$('grp-scene'), $('grp-world')]], ['game', 'Game Config', [$('grp-intensity'), $('grp-loop'), $('grp-players')]], ['ai', 'AI Config', [$('grp-ai'), document.querySelector('.afternote')]],
      ['style', 'Play Style Config', [$('grp-style'), $('grp-elements')]], ['input', 'Input', [$('grp-controls'), $('grp-feel')]], ['others', 'Vessel Config', [$('grp-kit'), $('grp-mechanic'), $('grp-members'), $('grp-abilities'), $('grp-live')]]],
    bottom: [['score', 'Scorecard', [$('grp-score')]], ['runs', 'Runs', [$('grp-runs')]], ['decisions', 'Decisions', [$('grp-decisions')]], ['about', 'About', [document.querySelector('header .sub'), $('grp-about')]]],
    railW: 390, dockH: 250, winSize: { score: [900, 400], runs: [700, 360], decisions: [520, 420] }, onLayout: () => P.forceResize(),
  });
  VesselStudioIDE.sectionPicker(document.querySelector('.vside-pane[data-tab="others"]'), 'siphonophore-studio', 'vessel');
  renderCrew(); syncRail(); renderRuns(); newRun(); hud();
  requestAnimationFrame((t) => { last = t; frame(t); });
  if (PLATFORM.device === 'phone') setPlayMode(true);

  // ---- hooks: the /labmaker contract (__lab) and the studio hook ----
  const SHIPPED = Object.assign({}, DEFAULTS);
  Object.assign(LAB, {
    KIT, KIT_COMBOS,
    SHIPPED, SPEC: SPEC.map((r) => ['loop'].concat(r)), P: S.params, PLATFORM,
    reset: (seed) => { seedN = seed == null ? 1 : seed; newRun(); run = 'running'; paused = false; $('overlay').hidden = true; },
    tick: (dt, n) => { for (let i = 0; i < (n || 1); i++) { if (!W || W.over) break; P.stepAll(W, dt, -1, null); } },
    state: () => ({ t: W ? +W.t.toFixed(4) : 0, run, form: W ? W.crew.form : '', hold: W ? W.crew.hold : 0, digested: W ? W.digested : 0 }),
    score: (done) => runScorecard([11, 22, 33], done || (() => {})),
    runBatch: () => { const h = P.runHeadless(1, 'Hard', 11), n = P.runHeadless(1, 'Hard', 11, { policy: 'never' }), a = P.runHeadless(1, 'Hard', 11, { policy: 'always' }); return { hardI1: h.finish, walls: h.walls, fused: h.fusedShare, fuses: h.fuses, neverI1: n.finish, neverDigested: n.digested, alwaysI1: a.finish, alwaysDigested: a.digested }; },
  });
  window.__lab = LAB;
  window.__siphonophoreStudio = {
    get state() { const c = W.crew; return { run, t: +W.t.toFixed(2), course: S.course, form: c.form, hold: c.hold, seat: c.seat, digested: W.digested, target: W.track.target, walls: W.wallsBroken + '/' + W.walls.length, fuses: c.fuses, swaps: W.swaps, crystals: W.crystals.map((x) => x.state), bodies: bodiesNow(W).map((B) => ({ name: B.name, speed: +B.speed.toFixed(1), pos: B.pos.toArray().map((x) => +x.toFixed(1)), state: B.aiState })) }; },
    get race() { return run; }, get world() { return W; },
    startRace: startRun, stopRace: stopRun, newRace: newRun, togglePause, swapSeat, runHeadless: P.runHeadless, runScorecard, makeWorld: P.makeWorld, stepAll: P.stepAll, ZOOIDS, COLONY, DEFAULTS, S, ide: IDE,
    set: (k, v) => { if (k === 'course') { S.course = v; courseSel.value = v; save(); newRun(); return; } if (k === 'seat') { S.nobody = v === 'nobody'; if (!S.nobody) S.seat = v; save(); newRun(); return; } S[k] = v; save(); newRun(); },
  };
})();
