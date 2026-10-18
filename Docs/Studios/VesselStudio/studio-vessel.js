/* studio-vessel.js - the Vessel Studio's GENERIC studio engine (/vessel-studio D40; the vision's B3, "the vessel recipe and
 * the instant studio"). One page skeleton (vessel_page_template.html) plus one RECIPE (vessels/<id>.json, inlined into
 * <id>.html by .claude/skills/vessel-studio/vessel_pages.py) plus one MOVEMENT KIND (studio-kinds.js) is a playable
 * studio with an AI at Easy / Medium / Hard, the three cameras, phone play, the six settings tabs, a scorecard with a
 * no-mechanic control and the shared decision log. No code is written per vessel: a new vessel is a recipe.
 *
 *   <script src="studio-kinds.js"></script><script src="studio-vessel.js"></script>
 *   <script>VesselStudioEngine.start(RECIPE);</script>
 *
 * A DESIGN studio (D2): every number is a proposal and says so (the recipe's params are lab-only; the course is the
 * game's own Skim Race track data, the only shipped numbers here, named below). The platform rules it keeps from the
 * game: the dual-stick mix of InputController (sticks sum = yaw / pitch, difference = roll / throttle), every hull lays
 * a prism trail, skimming pays boost, a hull contact costs (unless the kind makes contact its mechanic), crystals are
 * the goal, the AI is input-only and a difficulty level edits only what it believes (D4, D24), PvP is petals only.
 *
 * ASCII only: served beside the pages without a charset. Needs THREE (r128), studio-look.js, studio-domains.js,
 * studio-ide.js, ai_race_panel.js, studio-theme.js and studio-kinds.js, in that order, before it.
 */
(function () {
  'use strict';
  if (window.VesselStudioEngine) return;
  const T = window.THREE;

  // ================= the game's data (the only shipped numbers in a concept studio) =================
  // The four Skim Race courses of MinigameSkimRace.unity, as Tools/Build/skimrace_track_fingerprint.py --emit-track reads
  // them (the same data the Squirrel studio embeds): waypoints, crystal anchors, spline flag, laps.
  const TRACKS = {"1":{"spline":false,"laps":3,"wp":[[700,0,0],[500,0,500],[0,0,700],[-500,0,500],[-700,0,0],[-500,0,-500],[0,0,-700],[500,0,-500]],"an":[[670,0,75],[425,0,530],[-75,0,670],[-530,0,425],[-670,0,-75],[-425,0,-530],[75,0,-670],[530,0,-425]]},"2":{"spline":true,"laps":3,"wp":[[700,0,0],[445,-324,433],[198,-610,280],[-198,-610,-280],[-445,-324,-433],[-700,0,0],[-445,324,433],[-198,610,280],[198,610,-280],[445,324,-433]],"an":[[662,-49,65],[408,-367,410],[139,-610,196],[-235,-567,-303],[-483,-275,-368],[-662,49,65],[-408,367,410],[-139,610,196],[235,567,-303],[483,275,-368]]},"3":{"spline":true,"laps":2,"wp":[[-2785,0,-60],[-2438.8,20,-60],[-1746.3,-20,-60],[-1053.7,20,-60],[-361.2,-20,-60],[-15,0,-60],[96.6,0,-265.3],[310.7,0,-358.8],[537.1,0,-301.2],[680.5,0,-116.8],[680.5,0,116.8],[537.1,0,301.2],[310.7,0,358.8],[96.6,0,265.3],[-15,0,60],[-361.2,20,60],[-1053.7,-20,60],[-1746.3,20,60],[-2438.8,-20,60],[-2785,0,60],[-2896.6,0,265.3],[-3110.7,0,358.8],[-3337.1,0,301.2],[-3480.5,0,116.8],[-3480.5,0,-116.8],[-3337.1,0,-301.2],[-3110.7,0,-358.8],[-2896.6,0,-265.3]],"an":[[700,0,0],[370.1,0,358.7],[-361.2,20,60],[-1053.7,-20,60],[-1746.3,20,60],[-2438.8,-20,60],[-3170.1,0,358.7],[-3500,0,0],[-3170.1,0,-358.7],[-2438.8,20,-60],[-1746.3,-20,-60],[-1053.7,20,-60],[-361.2,-20,-60],[370.1,0,-358.7]]},"4":{"spline":false,"laps":2,"wp":[[700,0,0],[600,100,-350],[350,300,-550],[0,500,-500],[-300,550,-250],[-500,400,100],[-550,200,350],[-450,-100,550],[-300,-450,450],[-200,-650,200],[-250,-550,-100],[-350,-250,-300],[-400,0,-350],[-300,150,-500],[-100,250,-550],[150,300,-400],[350,200,-150],[400,100,150],[300,0,400],[150,-150,550],[0,-300,400],[-100,-350,150],[0,-300,-100],[150,-200,-300],[350,-100,-400],[500,-50,-300],[600,0,-150]],"an":[[685,15,-53],[563,130,-380],[298,330,-543],[-45,508,-463],[-330,528,-198],[-508,370,138],[-535,155,380],[-428,-153,535],[-285,-480,413],[-208,-635,155],[-265,-505,-130],[-358,-213,-308],[-385,23,-373],[-270,165,-508],[-63,258,-528],[180,285,-363],[358,185,-105],[385,85,188],[278,-23,423],[128,-173,528],[-15,-308,363],[-85,-343,113],[23,-285,-130],[180,-185,-315],[373,-93,-385],[515,-43,-278],[615,0,-128]]}};
  const COURSE_INFO = { 1: 'flat octagon, 3 laps', 2: 'tilted spline, 3 laps', 3: 'long spline, 2 laps', 4: '3D course, 2 laps' };
  // SkimRaceDifficulty.asset: Easy / Medium notice a new target late and misjudge a share of them (D24). `press` is a
  // LAB-ONLY third mistake for a movement vessel: the level presses its mechanic that many seconds late.
  const DIFF = { Easy: { reaction: 0.5, mistake: 0.099, press: 0.2 }, Medium: { reaction: 0.25, mistake: 0.045, press: 0.1 }, Hard: { reaction: 0, mistake: 0, press: 0 } };
  // the platform baseline every concept flies on (the Squirrel studio's numbers where the game has one, else lab-only)
  const BASE = {
    captureReach: 25.5, hullReach: 1.6, skimR: 12, skimAdd: 0.04, maxBoost: 1.6, boostDecay: 0.3,
    blockSpacing: 12, jitter: 35, limit: 540, rail: { offset: 9.66, spacing: 7, grace: 1, startDelay: 2, cap: 300 },   // limit: a concept may be slow and still deserve a number (I3 is 18 km)
    mass: { every: 70, lateral: [45, 120], count: [4, 8], ball: 14 },   // lab-only: neutral mass along the course, a stand-in for a cell's flora
  };
  const DEG = Math.PI / 180;
  const lerp = (a, b, t) => a + (b - a) * t, clamp = (x, a, b) => Math.max(a, Math.min(b, x));
  const ease = (x) => x < 0 ? Math.cos(x * Math.PI / 4) - 1 : -(Math.cos(x * Math.PI / 4) - 1);   // InputController's stick easing
  const easeInv = (e) => { e = clamp(e, -0.999, 0.999); return Math.sign(e) * (4 / Math.PI) * Math.acos(1 - Math.abs(e)); };
  function rng(seed) { let s = (seed >>> 0) || 1; return () => { s ^= s << 13; s >>>= 0; s ^= s >> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; }
  const V = T ? T.Vector3 : null, Q = T ? T.Quaternion : null;
  const elx = (t, c, x) => { const e = document.createElement(t); if (c) e.className = c; if (x != null) e.textContent = x; return e; };

  // ================= the course =================
  function spline(p, seg, t) {
    const c = p.length, p0 = p[((seg - 1) % c + c) % c], p1 = p[seg], p2 = p[(seg + 1) % c], p3 = p[(seg + 2) % c];
    const t2 = t * t, t3 = t2 * t;
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
    let len = 0; for (let i = 0; i < line.length; i++) len += line[i].distanceTo(line[(i + 1) % line.length]);
    return { def, line, tan, len, anchors: def.an.map((a) => new V(...a)), target: def.wp.length * def.laps };
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

  function start(RECIPE) {
    const $ = (id) => document.getElementById(id);
    const KINDS = window.VesselStudioKinds || {};
    const KIND = KINDS[RECIPE.kind];
    if (!KIND) throw new Error('studio-vessel: unknown movement kind "' + RECIPE.kind + '"');
    const DOMAINS = VesselStudioDomains.list.map((d, i) => ({ key: d.key, color: d.hex, css: d.color, name: d.name }));
    const ELS = ['charge', 'mass', 'space', 'time'];
    const EL_COLOR = { charge: '--charge', mass: '--mass', space: '--space', time: '--time' };
    const FLIGHT = Object.assign({ cruise: 60, pitchYaw: 120, roll: 130, grip: 30, accel: 1.5, topSpeed: 200, chase: 'course' }, RECIPE.flight || {});
    const PARAMS = Object.assign({}, KIND.defaults || {}, RECIPE.params || {});   // the kind's lab-only numbers, the recipe's values
    const STYLES = (RECIPE.styles && RECIPE.styles.length ? RECIPE.styles : [{ name: 'Balanced', lv: { charge: 0, mass: 0, space: 0, time: 0 }, info: 'Every element at 0.' }]);
    const SPEC = (KIND.spec || []).map((r) => r.slice());   // [[key, lo, hi, step, label, tip], ...]

    // ---- the page's words, from the recipe ----
    document.title = RECIPE.name + ' Studio';
    $('h1').textContent = RECIPE.name + ' Studio'; $('round').textContent = RECIPE.round || 'concept \u00b7 round 1';
    $('sub').textContent = RECIPE.summary || ''; $('hSub').textContent = RECIPE.tagline || '';
    document.documentElement.style.setProperty('--accent-vessel', RECIPE.accent || '#ffb347');

    // ================= settings =================
    const S = { course: 1, players: [{ ai: false, domain: 'jade', level: 'Hard', style: STYLES[0].name }], view: 0, cam: 'Chase', speed: 1, thinking: true, autoRestart: false,
      idleHold: true, aiMech: true, massDensity: 1, trigDepth: 1, style: STYLES[0].name, lv: Object.assign({}, STYLES[0].lv), params: {} };
    if (RECIPE.players) S.players = RECIPE.players.map((p) => Object.assign({}, p));
    const LS = 'cs.studio.' + RECIPE.id + '.v1';
    try { const s = JSON.parse(localStorage.getItem(LS) || 'null'); if (s) Object.assign(S, s, { lv: Object.assign({}, S.lv, s.lv || {}), params: Object.assign({}, s.params || {}) }); } catch (e) { /* storage blocked */ }
    function save() { try { localStorage.setItem(LS, JSON.stringify(S)); } catch (e) { /* storage blocked */ } }
    const par = (k) => (k in S.params ? S.params[k] : PARAMS[k]);   // the base value of a kind param: the recipe's, or the tester's slider

    // ---- element scaling: one scaled parameter per element (the fleet contract), a multiplier range over level 0..10 ----
    function mults(lv) {
      const m = {}; for (const k in PARAMS) m[k] = par(k);
      for (const el of ELS) { const e = RECIPE.elements && RECIPE.elements[el]; if (!e || !e.param || !(e.param in m)) continue; const r = e.range || [1, 1]; m[e.param] = par(e.param) * lerp(r[0], r[1], clamp(lv[el] / 10, 0, 1)); }
      m.l5 = { charge: lv.charge >= 5, mass: lv.mass >= 5, space: lv.space >= 5, time: lv.time >= 5 };
      return m;
    }
    const styleOf = (name) => STYLES.find((s) => s.name === name) || STYLES[0];

    // ================= the world (never draws: the scorecard runs it headless) =================
    const tmpArr = [], AX = new V(1, 0, 0), AY = new V(0, 1, 0), AZ = new V(0, 0, 1), dq = new Q();
    const ctx = {
      THREE: T, V, Q, lerp, clamp, DEG, ease, easeInv, BASE, FLIGHT, PARAMS, DIFF,
      axes(P) { return { fwd: new V(0, 0, -1).applyQuaternion(P.quat), right: new V(1, 0, 0).applyQuaternion(P.quat), up: new V(0, 1, 0).applyQuaternion(P.quat) }; },
      sticks(inp) {   // the game's dual-stick mix (InputController): sums steer, differences roll and throttle
        if (inp.pad) return { XSum: ease(inp.R.x + inp.L.x), YSum: -ease(inp.R.y + inp.L.y), YDiff: ease(inp.R.y - inp.L.y), XDiff: clamp((inp.R.x - inp.L.x + 2) / 4, 0, 1) };
        return { XSum: inp.k.yaw, YSum: -inp.k.pitch, YDiff: inp.k.roll, XDiff: inp.k.throttle };
      },
      steer(P, inp, dt, turnMult, f) {   // the sticks turn the nose; f overrides the rates (default: the recipe's flight)
        const s = ctx.sticks(inp), F = f || P.F, k = (turnMult == null ? 1 : turnMult) * DEG * dt;
        P.throttle = s.XDiff;
        dq.setFromAxisAngle(AX, -s.YSum * F.pitchYaw * k); P.quat.multiply(dq);
        dq.setFromAxisAngle(AY, -s.XSum * F.pitchYaw * k); P.quat.multiply(dq);
        dq.setFromAxisAngle(AZ, s.YDiff * F.roll * k); P.quat.multiply(dq);
        P.quat.normalize(); return s;
      },
      move(P, dt, targetSpeed, grip, rate) {   // course-following flight: speed eases to its target, the course follows the nose at `grip`
        const fwd = new V(0, 0, -1).applyQuaternion(P.quat);
        P.speed += (targetSpeed - P.speed) * (1 - Math.exp(-(rate == null ? P.F.accel : rate) * dt));
        P.course.lerp(fwd, 1 - Math.exp(-(grip == null ? P.F.grip : grip) * dt)).normalize();
        P.vel.copy(P.course).multiplyScalar(P.speed);
        P.pos.addScaledVector(P.course, P.speed * P.slow * dt);
      },
      moveVel(P, dt) {   // velocity flight: integrate P.vel; keep speed / course in step for the camera and HUD
        P.pos.addScaledVector(P.vel, P.slow * dt);
        P.speed = P.vel.length(); if (P.speed > 1e-3) P.course.copy(P.vel).divideScalar(P.speed);
      },
      syncVel(P) { P.vel.copy(P.course).multiplyScalar(P.speed); },
      near(W, pos, r) { return W.grid.near(pos, r, tmpArr); },
      nearest(W, pos, r, filter) { let best = null, bd = r * r; for (const p of W.grid.near(pos, r, tmpArr)) { if (filter && !filter(p)) continue; const d = p.pos.distanceToSquared(pos); if (d < bd) { bd = d; best = p; } } return best; },
      addPrism, removePrism,
      layPrism(W, P, pos, quat, scale, o) { return addPrism(W, Object.assign({ pos: pos.clone(), quat: quat.clone(), scale, owner: P.i, domain: P.domain.key, shield: false, danger: false, kind: 'laid', born: W.t }, o || {})); },
      event(W, P, text) { if (P.i === W.watch) W.events.push(text); },
      rivals(W, P) { return W.pilots.filter((X) => X !== P && X.finishT == null); },
      aimAt(P, point) { return aimSticks(P, point); },
      l5(P, el) { return !!(P.m.l5 && P.m.l5[el]); },
      pressLate(W, P) { return P.hc ? P.hc.level.press : 0; },
    };
    function addPrism(W, o) { o.cool = []; o.hull = []; o.flash = 0; W.prisms.push(o); W.grid.add(o); W.dirty = true; return o; }
    function removePrism(W, o) { if (o.dead) return; W.grid.remove(o); o.dead = true; W.dirty = true; }

    function makeWorld(courseId, pilotsSpec, seed, opts) {
      opts = opts || {};
      const track = buildTrack(TRACKS[courseId]);
      const W = { track, course: courseId, prisms: [], grid: new Grid(24), pilots: [], t: 0, rand: rng(seed), seed, events: [], over: false, dirty: true, watch: 0, mech: opts.mech !== false, massDensity: opts.massDensity != null ? opts.massDensity : S.massDensity };
      for (let i = 0; i < track.line.length; i++) addPrism(W, { pos: track.line[i].clone(), quat: basisQuat(track.tan[i]), scale: [3.2, 3.2, 9], owner: -1, domain: 'track', shield: false, danger: false, kind: 'track' });
      scatterMass(W);
      const t0 = track.tan[0], q0 = basisQuat(t0), side = new V(1, 0, 0).applyQuaternion(q0);
      pilotsSpec.forEach((spec, i) => {
        const off = (i - (pilotsSpec.length - 1) / 2) * 26, lv = Object.assign({}, spec.lv);
        const P = {
          i, spec, domain: DOMAINS.find((d) => d.key === spec.domain) || DOMAINS[i % DOMAINS.length],
          pos: track.line[0].clone().addScaledVector(side, off).addScaledVector(t0, -40), quat: q0.clone(), course: t0.clone(), vel: t0.clone().multiplyScalar(0.01),
          speed: 0, slow: 1, boost: 1, throttle: 0.5, lv, m: mults(lv), F: Object.assign({}, FLIGHT), K: {},
          crystals: 0, anchor: 0, crystal: null, finishT: null, hint: 0, skims: 0, resets: 0, petals: 0, stolen: 0, mistakes: 0, railAcc: 0, rails: 0,
          wake: [], wakeT: 0, stats: { mechT: 0, top: 0, uses: 0 },
          hc: spec.ai ? { level: DIFF[spec.ai], rng: rng(seed * 31 + i * 7 + 1), tracking: -1, noticeAt: 0, misjudged: false, until: 0, off: new V(), unnoticed: false, pressAt: 0 } : null,
          believed: null, aiState: '', aim: null,
        };
        if (KIND.init) KIND.init(P, ctx, W);
        W.pilots.push(P); spawnCrystal(W, P);
      });
      return W;
    }
    function scatterMass(W) {   // lab-only neutral mass along the course: a cluster every ~70 u, out to one side; stands in for a cell's flora
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
    function spawnCrystal(W, P) {
      const a = W.track.anchors[P.anchor % W.track.anchors.length], r = W.rand; let j;
      do { j = new V(r() * 2 - 1, r() * 2 - 1, r() * 2 - 1); } while (j.lengthSq() > 1);
      P.crystal = { pos: a.clone().addScaledVector(j, BASE.jitter), id: (P.i + 1) * 100000 + P.crystals };
    }
    function nearestIndex(track, pos, hint, full) {
      const L = track.line, n = L.length; let best = hint, bd = Infinity;
      if (full) { for (let i = 0; i < n; i++) { const d = L[i].distanceToSquared(pos); if (d < bd) { bd = d; best = i; } } return best; }
      for (let k = -30; k <= 60; k++) { const i = ((hint + k) % n + n) % n; const d = L[i].distanceToSquared(pos); if (d < bd) { bd = d; best = i; } }
      return best;
    }

    // ---- the handicap: SkimRaceHandicap's two mistakes, applied to what the AI believes (D4, D24) ----
    function believe(W, P) {
      const c = P.crystal; if (!c) return null;
      const h = P.hc; if (!h || (h.level.reaction <= 0 && h.level.mistake <= 0)) return c.pos;
      if (h.tracking !== c.id) {
        h.tracking = c.id; h.noticeAt = W.t + h.level.reaction * lerp(0.5, 1.5, h.rng()); h.misjudged = h.rng() < h.level.mistake;
        if (h.misjudged) { P.mistakes++; const near = W.track.line[nearestIndex(W.track, c.pos, 0, true)]; const away = c.pos.clone().sub(near); if (away.lengthSq() < 1) away.set(0, 1, 0); h.off.copy(away.normalize().multiplyScalar(2 * BASE.captureReach + 4)); h.until = W.t + 6; }
      }
      h.unnoticed = W.t < h.noticeAt; if (h.unnoticed) return null;
      if (h.misjudged) { const fake = c.pos.clone().add(h.off), toFake = fake.clone().sub(P.pos), fwd = new V(0, 0, -1).applyQuaternion(P.quat); if (W.t > h.until || toFake.length() < 12 || toFake.dot(fwd) < 0) h.misjudged = false; else return fake; }
      return c.pos;
    }
    // ---- the generic aim: follow the course line, lean out for the crystal (the Squirrel studio's pilot) ----
    const LOOK = { base: 24, perSpeed: 0.5, reachMargin: 17 };   // the Squirrel studio's pilot, looking a little further at speed (these hulls reach 150-250 u/s)
    function aiAim(W, P) {
      const tr = W.track, n = tr.line.length, fwd = new V(0, 0, -1).applyQuaternion(P.quat);
      P.hint = nearestIndex(tr, P.pos, P.hint, false);
      const look = clamp(Math.round((LOOK.base + P.speed * LOOK.perSpeed) / BASE.blockSpacing), 2, 16), li = (P.hint + look) % n;
      let aim = tr.line[li].clone().addScaledVector(new V(0, 1, 0), 11), why = 'line';   // just above the ribbon: inside the 12 u skim, outside the hull's reach
      const b = believe(W, P); P.believed = b;
      if (b) {
        if (!P.bLine || P.bLine.at !== b) P.bLine = { at: b, i: nearestIndex(tr, b, 0, true) };
        let ahead = P.bLine.i - P.hint; if (ahead < -n / 2) ahead += n; if (ahead > n / 2) ahead -= n;
        if (ahead < -2 || (ahead <= 2 && b.clone().sub(P.pos).dot(fwd) < 0)) { aim = b; why = 'turn back'; }
        else if (ahead <= look + 2) { const base = tr.line[P.bLine.i], perp = b.clone().sub(base), need = Math.max(0, perp.length() - (P.hc && P.hc.misjudged ? 0 : LOOK.reachMargin)); aim = base.clone().addScaledVector(perp.normalize(), need); why = 'crystal'; if (ahead < 1) aim.addScaledVector(tr.tan[P.bLine.i], 20); }
      }
      if (KIND.aiAim) { const r = KIND.aiAim(W, P, aim, why, ctx); if (r) { aim = r.aim || aim; why = r.why || why; } }
      return { aim, why };
    }
    function aimSticks(P, aim) {   // the sticks that point the nose at `aim`, steering first, throttle with what is left (the game's mix inverted)
      const fwd = new V(0, 0, -1).applyQuaternion(P.quat), up = new V(0, 1, 0).applyQuaternion(P.quat), right = new V(1, 0, 0).applyQuaternion(P.quat);
      const dir = aim.clone().sub(P.pos).normalize(), x = dir.dot(right), y = dir.dot(up), z = dir.dot(fwd);
      const yawErr = Math.atan2(x, z), pitchErr = Math.atan2(y, Math.max(0.05, Math.hypot(x, z)) * (z < 0 ? -1 : 1));
      const yawCmd = clamp(yawErr * 2.4, -1, 1), pitchCmd = clamp(pitchErr * 2.4, -1, 1), rollCmd = clamp(-right.y * 2, -1, 1);
      const sx = easeInv(yawCmd), sy = easeInv(pitchCmd), dy = easeInv(rollCmd), dx = Math.min(2, 2 - Math.abs(sx));
      return { L: { x: (sx - dx) / 2, y: clamp((sy - dy) / 2, -1, 1) }, R: { x: (sx + dx) / 2, y: clamp((sy + dy) / 2, -1, 1) }, err: Math.acos(clamp(z, -1, 1)) / DEG, yawErr, pitchErr };
    }
    function aiInput(W, P) {
      const { aim, why } = aiAim(W, P); P.aim = aim; P.aimWhy = why;
      const base = aimSticks(P, aim);
      let inp = { pad: true, L: base.L, R: base.R, lt: 0, rt: 0, a: false, b: false, ai: true };
      if (KIND.aiSticks) { const r = KIND.aiSticks(W, P, aim, base, ctx); if (r) { inp.L = r.L || inp.L; inp.R = r.R || inp.R; } }
      if (W.mech && KIND.ai) {
        const want = KIND.ai(W, P, aim, base, ctx) || {};
        // the level's lab-only press delay: a want that just appeared is pressed `press` seconds late
        const h = P.hc, late = h ? h.level.press : 0, on = !!(want.lt || want.rt || want.a || want.b);
        if (on && !P.K._wantOn) h && (h.pressAt = W.t + late * lerp(0.5, 1.5, h.rng()));
        P.K._wantOn = on;
        if (!late || !on || W.t >= (h ? h.pressAt : 0)) { inp.lt = want.lt || 0; inp.rt = want.rt || 0; inp.a = !!want.a; inp.b = !!want.b; }
      }
      P.aiState = P.hc && P.hc.unnoticed ? 'not noticed' : P.hc && P.hc.misjudged ? 'misjudged' : (P.K.active ? (KIND.mechanicName || 'mechanic') : why);
      return inp;
    }

    // ---- one step of one pilot: the same physics for a player and an AI ----
    function stepPilot(W, P, inp, dt) {
      if (P.finishT != null) { P.speed *= Math.exp(-1.5 * dt); P.pos.addScaledVector(P.course, P.speed * dt); P.vel.copy(P.course).multiplyScalar(P.speed); return; }
      P.lastInp = inp;
      P.slow += (1 - P.slow) * (1 - Math.exp(-1 * dt));
      if (P.boost > 1) P.boost = Math.max(1, P.boost - BASE.boostDecay * dt);
      KIND.step(W, P, inp, dt, ctx);
      if (P.K.active) P.stats.mechT += dt;
      P.stats.top = Math.max(P.stats.top, P.speed);
      const fwd = new V(0, 0, -1).applyQuaternion(P.quat), right = new V(1, 0, 0).applyQuaternion(P.quat), up = new V(0, 1, 0).applyQuaternion(P.quat);
      // the trail every hull lays (VesselPrismController): two rails, a pair per 7 u
      if (KIND.trail !== false && W.t > BASE.rail.startDelay) { P.railAcc += P.speed * dt; while (P.railAcc >= BASE.rail.spacing) { P.railAcc -= BASE.rail.spacing; layRails(W, P, right); } }
      // the wake, for anything that drafts (a sample every 0.1 s, 4 s long)
      P.wakeT += dt; if (P.wakeT >= 0.1) { P.wakeT = 0; P.wake.push({ pos: P.pos.clone(), speed: P.speed, t: W.t }); if (P.wake.length > 40) P.wake.shift(); }
      // contacts: the hull (a cost, unless the kind makes contact its mechanic) and the skim (boost)
      // the skim and the hull keep separate cooldowns: a prism skimmed on the way in must still be there to hit (the
      // Flea never bounced while one cooldown served both: the skim cooled the prism 12 u before the hull reached it)
      const near = W.grid.near(P.pos, BASE.skimR + 8, tmpArr);
      for (const p of near) {
        const d = p.pos.distanceTo(P.pos), own = p.owner === P.i, reach = (KIND.hullReach || BASE.hullReach) + (p.kind === 'track' ? 1.6 : 0.9) * (p.scale ? p.scale[0] / 3 : 1);
        if (d < reach + 1 && (p.hull[P.i] || 0) <= W.t && !(own && (p.kind === 'rail' || p.kind === 'laid') && W.t - (p.born || 0) < BASE.rail.grace)) {
          p.hull[P.i] = W.t + 0.6;
          if (KIND.onContact && KIND.onContact(W, P, p, ctx)) continue;
          P.boost = 1; P.slow = Math.min(P.slow, 0.9); P.resets++; ctx.event(W, P, 'Hull contact');
          continue;
        }
        if ((p.cool[P.i] || 0) > W.t) continue;
        if (KIND.skim === false || d > BASE.skimR || own) continue;
        p.cool[P.i] = W.t + 0.6; p.flash = 1; W.dirty = true;
        P.boost = clamp(P.boost + BASE.skimAdd * (p.danger ? 2 : 1), 1, BASE.maxBoost); P.skims++;
        if (KIND.onSkim) KIND.onSkim(W, P, p, ctx);
      }
      if (P.crystal && P.crystal.pos.distanceTo(P.pos) < BASE.captureReach * (P.m.captureMult || 1)) {
        P.crystals++; P.anchor++; if (KIND.onCrystal) KIND.onCrystal(W, P, ctx);
        if (P.crystals >= W.track.target) { P.finishT = W.t; P.crystal = null; ctx.event(W, P, 'Finished in ' + W.t.toFixed(1) + ' s'); }
        else spawnCrystal(W, P);
      }
    }
    function layRails(W, P, right) {
      for (const s of [-1, 1]) addPrism(W, { pos: P.pos.clone().addScaledVector(right, s * BASE.rail.offset), quat: P.quat.clone(), scale: [0.83, 0.83, 6.1], owner: P.i, domain: P.domain.key, shield: false, danger: false, kind: 'rail', born: W.t });
      P.rails += 2;
      if (P.rails > BASE.rail.cap) { let removed = 0; for (const p of W.prisms) { if (!p.dead && p.kind === 'rail' && p.owner === P.i) { removePrism(W, p); P.rails--; if (++removed >= 2) break; } } }
    }
    function stepWorld(W, dt, inputs) {
      W.t += dt;
      W.pilots.forEach((P, i) => { stepPilot(W, P, inputs[i], dt); if (!P.spec.ai) P.hint = nearestIndex(W.track, P.pos, P.hint, false); });
      if (KIND.stepWorld) KIND.stepWorld(W, dt, ctx);
      let dead = 0;
      for (const p of W.prisms) { if (p.flash > 0) { p.flash = Math.max(0, p.flash - dt * 3); W.dirty = true; } if (p.dead) dead++; }
      if (dead > 200) { W.prisms = W.prisms.filter((p) => !p.dead); W.dirty = true; }
      if (!W.over && (W.pilots.every((P) => P.finishT != null) || W.t > BASE.limit)) W.over = true;
    }
    // ---- headless race for the scorecard: one AI pilot alone, with or without its mechanic ----
    function raceHeadless(course, level, seed, o) {
      o = o || {};
      // a vessel whose mechanic needs company (the Remora drafts) seats AI rivals in its headless races: recipe.scorecard.rivals
      const st = styleOf(o.style || S.style), nRivals = o.rivals != null ? o.rivals : ((RECIPE.scorecard && RECIPE.scorecard.rivals) || 0);
      const specs = [{ ai: level, domain: 'jade', lv: Object.assign({}, o.lv || st.lv), style: st.name }];
      for (let k = 0; k < nRivals; k++) specs.push({ ai: 'Hard', domain: DOMAINS[(k + 1) % DOMAINS.length].key, lv: Object.assign({}, STYLES[0].lv), style: STYLES[0].name });
      const W = makeWorld(course, specs, seed, { mech: o.mech !== false, massDensity: o.massDensity });
      W.watch = -1; const dt = 1 / 30, P = W.pilots[0]; let n = 0, sp = 0;
      while (!W.over && P.finishT == null) { stepWorld(W, dt, W.pilots.map((X) => aiInput(W, X))); n++; sp += P.speed; }
      return { finish: P.finishT, crystals: P.crystals, avgSpeed: +(sp / n).toFixed(1), top: +P.stats.top.toFixed(1), mechShare: +(P.stats.mechT / Math.max(1e-6, W.t)).toFixed(3), uses: P.stats.uses, resets: P.resets, skims: P.skims, mistakes: P.mistakes };
    }

    // ================= rendering =================
    const canvas = $('c');
    const renderer = VesselStudioTheme.renderer(T, { canvas, antialias: true });   // no WebGL: the stage says so, every tab still works (D34)
    renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
    const scene = new T.Scene(), cam = new T.PerspectiveCamera(70, 16 / 10, 0.5, 9000);
    const ENV = VesselStudioLook.install(scene, { field: 1800, prismScale: 2.4 });
    const mark = VesselStudioLook.marker($('viewport'));
    const MAXP = 12000;
    const prismMesh = new T.InstancedMesh(new T.BoxGeometry(1, 1, 1), new T.MeshStandardMaterial({ roughness: 0.5, metalness: 0.05, emissive: 0x0b1b4a, flatShading: true }), MAXP);
    prismMesh.instanceMatrix.setUsage(T.DynamicDrawUsage); scene.add(prismMesh);
    const shieldMesh = new T.InstancedMesh(new T.BoxGeometry(1, 1, 1), new T.MeshBasicMaterial({ color: 0xbfe6ff, wireframe: true, transparent: true, opacity: 0.5 }), MAXP);
    shieldMesh.instanceMatrix.setUsage(T.DynamicDrawUsage); scene.add(shieldMesh);
    const COL = { track: new T.Color(0x3a4380), mass: new T.Color(0x56608f) };
    for (const d of DOMAINS) COL[d.key] = new T.Color(d.color);
    const DANGER = new T.Color(0xff2a2a), WHITE = new T.Color(0xffffff), INK = new T.Color(0x14101c), m4 = new T.Matrix4(), tmpC = new T.Color(), sc = new V();
    function drawPrisms(W) {
      let n = 0, ns = 0;
      for (const p of W.prisms) {
        if (p.dead || n >= MAXP) continue;
        sc.set(p.scale[0], p.scale[1], p.scale[2]); m4.compose(p.pos, p.quat, sc); prismMesh.setMatrixAt(n, m4);
        tmpC.copy(p.kind === 'ink' ? INK : (COL[p.domain] || COL.track)); if (p.danger) tmpC.lerp(DANGER, 0.5); if (p.flash > 0) tmpC.lerp(WHITE, p.flash);
        prismMesh.setColorAt(n, tmpC); n++;
        if (p.shield && ns < MAXP) { sc.multiplyScalar(1.4); m4.compose(p.pos, p.quat, sc); shieldMesh.setMatrixAt(ns++, m4); }
      }
      prismMesh.count = n; shieldMesh.count = ns;
      prismMesh.instanceMatrix.needsUpdate = true; shieldMesh.instanceMatrix.needsUpdate = true;
      if (prismMesh.instanceColor) prismMesh.instanceColor.needsUpdate = true;
      W.dirty = false;
    }
    function defaultHull(color) {
      const g = new T.Group(), mat = new T.MeshStandardMaterial({ color, roughness: 0.4, metalness: 0.2, emissive: new T.Color(color).multiplyScalar(0.18) });
      const body = new T.Mesh(new T.ConeGeometry(2.2, 9, 6), mat); body.rotation.x = -Math.PI / 2; g.add(body);
      const wing = new T.Mesh(new T.BoxGeometry(9, 0.4, 2.6), mat); wing.position.z = 2; g.add(wing);
      return g;
    }
    const view = { hulls: [], crystals: [], skims: [], lines: [], kind: [] };
    function mountWorld(W) {
      { const ln = W.track.line, lo = new V(Infinity, Infinity, Infinity), hi = new V(-Infinity, -Infinity, -Infinity); for (const q of ln) { lo.min(q); hi.max(q); } const ctr = lo.clone().add(hi).multiplyScalar(0.5); let rad = 0; for (const q of ln) rad = Math.max(rad, Math.hypot(q.x - ctr.x, q.z - ctr.z)); ENV.placeField(ctr, (rad + 200) / 0.75, Math.max(160, (hi.y - lo.y) * 0.5 + 140)); }
      for (const o of [...view.hulls, ...view.crystals, ...view.skims, ...view.lines]) scene.remove(o);
      for (const k of view.kind) if (k && k.dispose) k.dispose();
      view.hulls = []; view.crystals = []; view.skims = []; view.lines = []; view.kind = [];
      for (const P of W.pilots) {
        const h = (KIND.hull ? KIND.hull(ctx, P.domain.color, P) : null) || defaultHull(P.domain.color); scene.add(h); view.hulls.push(h);
        const c = VesselStudioLook.crystal(P.domain.color, 7); scene.add(c); view.crystals.push(c);
        const s = new T.Mesh(new T.SphereGeometry(1, 18, 12), new T.MeshBasicMaterial({ color: P.domain.color, wireframe: true, transparent: true, opacity: 0.05 })); scene.add(s); view.skims.push(s);
        const lg = new T.BufferGeometry().setFromPoints([new V(), new V(), new V(), new V()]);
        const ln = new T.LineSegments(lg, new T.LineBasicMaterial({ color: P.domain.color, transparent: true, opacity: 0.85 })); ln.frustumCulled = false; scene.add(ln); view.lines.push(ln);
        view.kind.push(KIND.mount ? KIND.mount(ctx, scene, P) : null);
      }
    }
    function drawWorld(W, t) {
      if (W.dirty) drawPrisms(W);
      W.pilots.forEach((P, i) => {
        const h = view.hulls[i]; h.position.copy(P.pos); h.quaternion.copy(P.quat); h.visible = !(KIND.hideHull && KIND.hideHull(P));
        if (KIND.poseHull) KIND.poseHull(h, P, t, ctx);
        const c = view.crystals[i]; c.visible = !!P.crystal; if (P.crystal) { c.position.copy(P.crystal.pos); VesselStudioLook.spinCrystal(c, t + i); }
        view.skims[i].visible = i !== chased && KIND.skim !== false; view.skims[i].position.copy(P.pos); view.skims[i].scale.setScalar(BASE.skimR);
        const ln = view.lines[i], show = S.thinking && !!P.spec.ai && !!P.crystal && P.finishT == null && !!P.aim; ln.visible = show;
        if (show) {   // the AI's line to where it steers (green = flying for it, amber = not noticed, red = misjudged, violet = working the mechanic)
          const a = ln.geometry.attributes.position, b = P.believed || P.pos;
          a.setXYZ(0, P.pos.x, P.pos.y, P.pos.z); a.setXYZ(1, P.aim.x, P.aim.y, P.aim.z); a.setXYZ(2, P.pos.x, P.pos.y + 3, P.pos.z); a.setXYZ(3, b.x, b.y + 3, b.z); a.needsUpdate = true;
          ln.material.color.set(P.hc && P.hc.misjudged ? 0xff5c6c : P.hc && P.hc.unnoticed ? 0xffb454 : P.K.active ? 0xb98bff : 0x6fe39a);
        }
        if (KIND.draw) KIND.draw(W, P, view.kind[i], t, ctx);
      });
    }

    // ---- cameras: Chase, Follow (the viewed player), Free (orbit) (D16) ----
    const camPos = new V(), camLook = new V(), free = { yaw: 2.6, pitch: 0.35, dist: 90 };
    let chased = -1;
    const viewed = (W) => W.pilots[Math.min(S.view, W.pilots.length - 1)] || W.pilots[0];
    function placeCamera(W, dt, snap) {
      let tag; chased = -1;
      const P = S.cam === 'Chase' && S.view === 0 ? W.pilots[0] : viewed(W);
      if (S.cam === 'Free') {
        const k = (c) => keys.has(c) ? 1 : 0;
        free.yaw += (k('KeyJ') - k('KeyL')) * 1.4 * dt; free.pitch = clamp(free.pitch + (k('KeyI') - k('KeyK')) * 1.1 * dt, -1.3, 1.3); free.dist = clamp(free.dist * (1 + (k('KeyO') - k('KeyU')) * 1.2 * dt), 25, 3000);
        if (playMode && touch.used && camTouch) { free.yaw -= touch.R.x * dt * 1.6; free.pitch = clamp(free.pitch + touch.R.y * dt * 1.2, -1.3, 1.3); free.dist = clamp(free.dist * (1 + touch.L.y * dt * 1.5), 25, 3000); }
        const want = new V(Math.sin(free.yaw) * Math.cos(free.pitch), Math.sin(free.pitch), Math.cos(free.yaw) * Math.cos(free.pitch)).multiplyScalar(free.dist).add(P.pos);
        const kk = snap ? 1 : 1 - Math.exp(-12 * dt); cam.position.lerp(want, kk); cam.up.set(0, 1, 0); cam.lookAt(P.pos);
        tag = 'Free: orbiting ' + P.domain.name + ' (drag, wheel)';
      } else {
        const fwd = new V(0, 0, -1).applyQuaternion(P.quat), up = new V(0, 1, 0).applyQuaternion(P.quat);
        const behind = (FLIGHT.chase === 'nose' ? fwd : P.course).clone(); if (behind.lengthSq() < 1e-4) behind.copy(fwd);
        const wide = S.cam === 'Follow';
        const wantOff = new V().addScaledVector(behind, -(wide ? 40 : 24) - P.speed * 0.03).addScaledVector(wide ? new V(0, 1, 0) : up, wide ? 16 : 8), lookOff = new V().addScaledVector(fwd, 30).addScaledVector(up, 2);
        const k = snap ? 1 : 1 - Math.exp(-6 * dt); camPos.lerp(wantOff, k); camLook.lerp(lookOff, k);   // the offset is smoothed, never the position
        cam.position.copy(P.pos).add(camPos); cam.up.copy(wide ? new V(0, 1, 0) : up); cam.lookAt(P.pos.clone().add(camLook));
        chased = P.i;
        tag = (wide ? 'Following ' : 'Chase: ') + P.domain.name + (P.spec.ai ? ' (AI ' + P.spec.ai + ')' : '');
      }
      $('camTag').textContent = tag + ' \u00b7 ' + S.speed + '\u00d7';
    }

    // ================= input: gamepad, keys, phone (one input object, D9) =================
    const keys = new Set(); let kbThrottle = 0.75, armed = false;
    const isForm = (t) => t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || t.isContentEditable);
    const GAME_KEYS = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'KeyQ', 'KeyE', 'KeyF', 'KeyG', 'KeyH', 'KeyR', 'KeyP', 'KeyC', 'KeyI', 'KeyJ', 'KeyK', 'KeyL', 'KeyU', 'KeyO', 'Space', 'Enter', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);
    window.addEventListener('keydown', (e) => {
      if (isForm(e.target) || !armed) return;
      if (GAME_KEYS.has(e.code)) e.preventDefault(); if (e.repeat) return; keys.add(e.code); humanTouched();
      if (e.code === 'Enter') startRace(); if (e.code === 'KeyR') stopRace(); if (e.code === 'KeyP') togglePause(); if (e.code === 'KeyC') cycleCamera();
    });
    window.addEventListener('keyup', (e) => keys.delete(e.code)); window.addEventListener('blur', () => keys.clear());
    document.addEventListener('pointerdown', (e) => { armed = $('viewport').contains(e.target); }); canvas.addEventListener('focus', () => { armed = true; });
    let drag = null;
    canvas.addEventListener('pointerdown', (e) => { if (S.cam === 'Free' && !playMode) drag = { x: e.clientX, y: e.clientY }; });
    canvas.addEventListener('wheel', (e) => { if (S.cam !== 'Free') return; e.preventDefault(); free.dist = clamp(free.dist * (1 + Math.sign(e.deltaY) * 0.1), 25, 3000); }, { passive: false });
    window.addEventListener('pointermove', (e) => { if (!drag) return; free.yaw -= (e.clientX - drag.x) * 0.008; free.pitch = clamp(free.pitch + (e.clientY - drag.y) * 0.006, -1.3, 1.3); drag = { x: e.clientX, y: e.clientY }; });
    window.addEventListener('pointerup', () => { drag = null; });
    function readPad() { let pads = []; try { pads = navigator.getGamepads ? Array.from(navigator.getGamepads()) : []; } catch (e) { return null; } for (const g of pads) if (g && g.connected && g.axes.length >= 4) return g; return null; }
    function stick(x, y) { const m = Math.hypot(x, y); if (m < 0.15) return { x: 0, y: 0 }; const s = Math.min(1, (m - 0.15) / 0.85) / m; return { x: x * s, y: y * s }; }
    let playMode = false, camTouch = false;
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
    function bindButton(id, key) { const el = $(id); const dn = (e) => { touch[key] = true; touch.used = true; humanTouched(); el.classList.add('on'); e.preventDefault(); }, up = () => { touch[key] = false; el.classList.remove('on'); }; el.addEventListener('pointerdown', dn); el.addEventListener('pointerup', up); el.addEventListener('pointercancel', up); el.addEventListener('pointerleave', up); }
    bindStick('tzL', 'tsL', 'L'); bindStick('tzR', 'tsR', 'R'); bindTrigger('ttL', 'L'); bindTrigger('ttR', 'R'); bindButton('tA', 'a'); bindButton('tB', 'b');
    function setPlayMode(on) {
      playMode = !!on; document.body.classList.toggle('play', playMode);
      if (playMode) { const v = $('viewport'), req = v.requestFullscreen || v.webkitRequestFullscreen; if (req) { try { const pr = req.call(v, { navigationUI: 'hide' }); if (pr && pr.catch) pr.catch(() => {}); } catch (e) { /* not allowed */ } } try { if (screen.orientation && screen.orientation.lock) screen.orientation.lock('landscape').catch(() => {}); } catch (e) { /* unsupported */ } }
      else { const ex = document.exitFullscreen || document.webkitExitFullscreen; if (document.fullscreenElement || document.webkitFullscreenElement) { try { const pr = ex.call(document); if (pr && pr.catch) pr.catch(() => {}); } catch (e) { /* ignore */ } } touch.lt = touch.rt = 0; touch.a = touch.b = false; touch.L.x = touch.L.y = touch.R.x = touch.R.y = 0; touch.used = false; }
      lastW = lastH = 0; resize();
    }
    document.addEventListener('fullscreenchange', () => { lastW = lastH = 0; resize(); });
    $('playBtn').addEventListener('click', () => setPlayMode(true)); $('tExit').addEventListener('click', () => setPlayMode(false));
    const PLATFORM = VesselStudioIDE.platform();   // detected, never asked (D26)
    $('touch').addEventListener('pointerdown', () => { if (playMode && !document.fullscreenElement && !document.webkitFullscreenElement) { const v = $('viewport'), req = v.requestFullscreen || v.webkitRequestFullscreen; if (req) { try { const pr = req.call(v, { navigationUI: 'hide' }); if (pr && pr.catch) pr.catch(() => {}); } catch (e) { /* not allowed here */ } } } }, { capture: true });
    const playPause = () => { if (race === 'running') togglePause(); else startRace(); };
    $('tStart').addEventListener('click', playPause); $('tBack').addEventListener('click', () => stopRace()); $('tCam').addEventListener('click', () => cycleCamera());
    $('tpPlay').addEventListener('click', playPause); $('tpStop').addEventListener('click', () => stopRace()); $('startBtn').addEventListener('click', () => startRace()); $('resetBtn').addEventListener('click', () => newRace());
    // the keyboard layout: 'fly' (W S pitch, A D yaw, Q E roll, arrows throttle) or 'omni' (W A S D move, arrows turn); Space = LT, F = RT, G = A, H = B
    const keyLayout = KIND.keyLayout || 'fly';
    function keyInput(dt) {
      const k = (c) => keys.has(c) ? 1 : 0;
      if (keyLayout === 'omni') return { pad: true, L: { x: k('KeyD') - k('KeyA'), y: k('KeyW') - k('KeyS') }, R: { x: k('ArrowRight') - k('ArrowLeft'), y: k('ArrowUp') - k('ArrowDown') }, lt: k('Space') * S.trigDepth, rt: k('KeyF'), a: keys.has('KeyG'), b: keys.has('KeyH'), kb: true };
      if (keys.has('ArrowUp')) kbThrottle = Math.min(1, kbThrottle + dt * 0.6); if (keys.has('ArrowDown')) kbThrottle = Math.max(0, kbThrottle - dt * 0.6);
      // L / R carry the same keys as two sticks (left W A S D, right the arrows) for a kind whose sticks stop steering in a
      // mode (the Gibbon's space mode aims a tether with each); a kind that steers reads `k` through ctx.sticks and never sees them
      return { pad: false, k: { yaw: k('KeyD') - k('KeyA'), pitch: k('KeyS') - k('KeyW'), roll: k('KeyE') - k('KeyQ'), throttle: kbThrottle }, L: { x: k('KeyD') - k('KeyA'), y: k('KeyW') - k('KeyS') }, R: { x: k('ArrowRight') - k('ArrowLeft'), y: k('ArrowUp') - k('ArrowDown') }, lt: k('Space') * S.trigDepth, rt: k('KeyF'), a: keys.has('KeyG'), b: keys.has('KeyH'), kb: true };
    }
    function humanInput(dt) {
      camTouch = S.cam === 'Free' && S.players[0].ai;   // with an AI on your hull, the phone's sticks fly the free camera
      if (playMode && touch.used) return camTouch ? { pad: true, L: { x: 0, y: 0 }, R: { x: 0, y: 0 }, lt: 0, rt: 0, a: false, b: false } : { pad: true, L: stick(touch.L.x, touch.L.y), R: stick(touch.R.x, touch.R.y), lt: touch.lt, rt: touch.rt, a: touch.a, b: touch.b };
      const g = readPad();
      if (g) {
        const inp = { pad: true, L: stick(g.axes[0], -g.axes[1]), R: stick(g.axes[2], -g.axes[3]), lt: g.buttons[6] ? g.buttons[6].value : 0, rt: g.buttons[7] ? g.buttons[7].value : 0, a: !!(g.buttons[0] && g.buttons[0].pressed), b: !!(g.buttons[1] && g.buttons[1].pressed) };
        if (g.buttons[9] && g.buttons[9].pressed && race !== 'running') startRace();
        if (Math.abs(inp.L.x) + Math.abs(inp.L.y) + Math.abs(inp.R.x) + Math.abs(inp.R.y) > 0.2 || inp.lt > 0.1 || inp.rt > 0.1 || inp.a || inp.b) humanTouched();
        return inp;
      }
      return keyInput(dt);
    }
    // D31: your hull never leaves the course unattended. With Is AI off and nobody on the controls for 3 s, the AI
    // flies it; the first input hands it straight back (both triggers let go at the switch).
    const IDLE_TAKEOVER_S = 3; let lastHumanT = 0, aiHolding = false;
    function humanTouched() { lastHumanT = performance.now() / 1000; if (aiHolding) { aiHolding = false; } }
    function pilotInput(W, P, hin) {
      if (P.spec.ai) return aiInput(W, P);
      const idle = S.idleHold && race === 'running' && (performance.now() / 1000 - lastHumanT) > IDLE_TAKEOVER_S && !(hin.kb && (keys.size > 0));
      if (idle) { if (!aiHolding) { aiHolding = true; } const inp = aiInput(W, P); return inp; }
      aiHolding = false; return hin;
    }

    // ================= race flow =================
    let W = null, race = 'ready', paused = false, seedN = 1, stopHeld = false;
    function pilotsSpec() { return S.players.map((p, i) => { const st = styleOf(p.style); return { ai: p.ai || i > 0 ? p.level : null, domain: p.domain, lv: Object.assign({}, i === 0 ? S.lv : st.lv), style: i === 0 ? S.style : st.name }; }); }
    function newRace() {
      W = makeWorld(S.course, pilotsSpec(), seedN++); S.view = Math.min(S.view, W.pilots.length - 1);
      mountWorld(W); drawWorld(W, 0); placeCamera(W, 0, true);
      race = 'ready'; paused = false; feed.length = 0; renderFeed(); lastHumanT = performance.now() / 1000; aiHolding = false;
      $('ovTitle').textContent = RECIPE.name + ' \u00b7 Skim Race course, intensity ' + S.course; $('startBtn').textContent = 'Start'; $('overlay').hidden = false;
      $('courseInfo').textContent = 'I' + S.course + ' \u00b7 ' + COURSE_INFO[S.course] + ' \u00b7 ' + W.track.target + ' crystals';
    }
    function startRace() { if (race === 'finished') newRace(); race = 'running'; paused = false; stopHeld = false; $('overlay').hidden = true; lastHumanT = performance.now() / 1000; canvas.focus({ preventScroll: true }); }
    function stopRace() { newRace(); stopHeld = true; }   // D18: Stop ends the run and HOLDS at the start until Play
    function togglePause() { if (race !== 'running') return; paused = !paused; say(paused ? 'Paused' : 'Resumed'); }
    function finishRace() {
      race = 'finished';
      const order = W.pilots.slice().sort((a, b) => (a.finishT ?? 1e9) - (b.finishT ?? 1e9) || b.crystals - a.crystals);
      for (const P of W.pilots) runs.unshift({ course: S.course, pilot: P.i === 0 ? (P.spec.ai ? 'Your hull (AI ' + P.spec.ai + ')' : 'You') : P.domain.name + ' (AI ' + P.spec.ai + ')', style: P.spec.style, time: P.finishT, place: order.indexOf(P) + 1, mech: Math.round(P.stats.mechT / Math.max(1e-6, W.t) * 100), top: Math.round(P.stats.top) });
      runs.length = Math.min(runs.length, 40); saveRuns(); renderRuns();
      const me = W.pilots[0];
      $('ovTitle').textContent = me.finishT != null ? 'Place ' + (order.indexOf(me) + 1) + ' \u00b7 ' + me.finishT.toFixed(1) + ' s' : 'Race over';
      $('ovText').textContent = order.map((P, i) => (i + 1) + '. ' + P.domain.name + (P.spec.ai ? ' (AI ' + P.spec.ai + ')' : '') + ': ' + (P.finishT != null ? P.finishT.toFixed(1) + ' s' : P.crystals + '/' + W.track.target)).join(' \u00b7 ');
      $('startBtn').textContent = 'Race again';
      if (S.autoRestart) setTimeout(() => { if (race === 'finished') { newRace(); startRace(); } }, 1500); else $('overlay').hidden = false;
    }
    function cycleCamera() {
      const n = W ? W.pilots.length : 1;
      if (S.cam === 'Chase') { if (n > 1) { S.cam = 'Follow'; S.view = 1; } else { S.cam = 'Free'; S.view = 0; } }
      else if (S.cam === 'Follow') { S.view++; if (S.view >= n) { S.cam = 'Free'; S.view = 0; } }
      else { S.cam = 'Chase'; S.view = 0; }
      racePanel.set('camera', S.cam, true); racePanel.set('view', S.view, true); save();
    }
    const feed = [];
    function say(m) { feed.unshift(m); feed.length = Math.min(feed.length, 4); renderFeed(); }
    function renderFeed() { const el = $('feed'); el.textContent = ''; for (const m of feed) { const d = elx('div', null, m); el.appendChild(d); } }

    // ================= the docks' panels =================
    // the universal race panel (D17) with the Players list (D25); Intensity in Game Config (D28), Camera and Speed in Scene Config (D21)
    const racePanel = StudioRacePanel.mount($('racePanel'), {
      gameHost: $('intensityPanel'), sceneHost: $('scenePanel'),
      courses: [1, 2, 3, 4].map((v) => ({ v, label: 'I' + v, note: COURSE_INFO[v] })),
      players: { host: $('playersPanel'), max: 4, domains: VesselStudioDomains.list.map((d) => ({ key: d.key, name: d.name, color: d.color })) },
      styles: STYLES.map((s) => s.name),
      value: { course: S.course, camera: S.cam, speed: S.speed, thinking: S.thinking, autoRestart: S.autoRestart, players: S.players, view: S.view },
      supports: { rivals: true, thinking: true, autoRestart: true },
      levelNote: 'Hard is the studio pilot (lab-only: it follows the course line, leans out for its crystal and works the ' + RECIPE.name + '\'s mechanic by the rule in AI Config). Medium and Easy are Hard plus SkimRaceDifficulty.asset\'s two mistakes in what it BELIEVES: it notices each new crystal late (0.25 / 0.5 s mean, x 0.5-1.5) and misjudges 4.5% / 9.9% of them; plus one lab-only movement mistake: it presses the mechanic 0.15 / 0.3 s late.',
      onChange: (key, v) => {
        if (key === 'players') { S.players = v; save(); newRace(); return; }
        if (key === 'view') { S.view = v; save(); return; }
        if (key === 'camera') { S.cam = v; if (v === 'Free') S.view = Math.max(0, S.view); } else if (key === 'course') S.course = v; else if (key === 'speed') S.speed = v; else if (key === 'thinking') S.thinking = v; else if (key === 'autoRestart') S.autoRestart = v;
        save(); if (key === 'course') newRace();
      },
    });
    $('idleHold').checked = S.idleHold; $('idleHold').addEventListener('change', (e) => { S.idleHold = e.target.checked; save(); });
    $('aiMech').checked = S.aiMech; $('aiMech').addEventListener('change', (e) => { S.aiMech = e.target.checked; save(); if (W) W.mech = S.aiMech; });
    $('aiRule').textContent = KIND.aiRule || 'The AI uses the mechanic whenever it helps it toward its crystal.';
    // Scene Config: course mass (lab-only)
    $('massDensity').value = S.massDensity; $('massOut').textContent = S.massDensity + '\u00d7';
    $('massDensity').addEventListener('input', (e) => { S.massDensity = +e.target.value; $('massOut').textContent = S.massDensity + '\u00d7'; save(); });
    $('massDensity').addEventListener('change', () => newRace());
    // lab-only: whether the course ribbon counts as mass to the mechanic (it is prisms in the game; off tests a bare course)
    if (S.ribbonMass == null) S.ribbonMass = true;
    const ribbon = () => { KINDS._ribbonIsMass = S.ribbonMass !== false; };
    $('ribbonMass').checked = S.ribbonMass !== false; $('ribbonMass').addEventListener('change', (e) => { S.ribbonMass = e.target.checked; ribbon(); save(); newRace(); }); ribbon();
    // Play Style Config: the named styles over the four element levels (D6), the sliders under them
    const styleSel = $('styleSel'); STYLES.forEach((s) => { const o = elx('option', null, s.name); o.value = s.name; styleSel.appendChild(o); });
    styleSel.addEventListener('change', () => { S.style = styleSel.value; Object.assign(S.lv, styleOf(S.style).lv); syncRail(); save(); applyLevels(); });
    const elRows = {};
    for (const el of ELS) {
      const e = (RECIPE.elements && RECIPE.elements[el]) || { name: '(open design slot)', l5: '' };
      const row = elx('div', 'el'); row.style.setProperty('--accent', 'var(' + EL_COLOR[el] + ')');
      const lab = elx('label', null, el[0].toUpperCase() + el.slice(1)); lab.htmlFor = 'lv-' + el; lab.style.color = 'var(' + EL_COLOR[el] + ')';
      const inp = document.createElement('input'); inp.type = 'range'; inp.min = 0; inp.max = 10; inp.step = 1; inp.id = 'lv-' + el; inp.title = e.tip || (e.name + (e.param ? ': scales ' + e.param : ''));
      const out = elx('output'), what = elx('div', 'what'), wl = elx('span'), badge = elx('span', 'l5', e.l5 || 'L5 open'); what.append(wl, badge);
      inp.addEventListener('input', () => { S.lv[el] = +inp.value; const m = STYLES.find((t) => ELS.every((x) => t.lv[x] === S.lv[x])); S.style = m ? m.name : 'Custom'; syncRail(); save(); applyLevels(); });
      row.append(lab, inp, out, what); $('elements').appendChild(row); elRows[el] = { inp, out, wl, badge, e };
    }
    function applyLevels() { if (!W) return; const P = W.pilots[0]; P.lv = Object.assign({}, S.lv); P.m = mults(P.lv); }
    // Vessel Config: the kind's lab-only sliders (one table, SPEC), the ability row and the live numbers
    const specInputs = {};
    for (const [key, lo, hi, step, label, tip] of SPEC) {
      const row = elx('div', 'el'); const lab = elx('label', null, label || key); lab.htmlFor = 'sp-' + key;
      const inp = document.createElement('input'); inp.type = 'range'; inp.min = lo; inp.max = hi; inp.step = step; inp.id = 'sp-' + key; inp.title = (tip || '') + ' (lab-only; recipe ' + PARAMS[key] + ')';
      const out = elx('output'), what = elx('div', 'what'), wl = elx('span', null, tip || ''), src = elx('span', 'l5', 'recipe ' + PARAMS[key]); what.append(wl, src);
      inp.addEventListener('input', () => { S.params[key] = +inp.value; if (+inp.value === PARAMS[key]) delete S.params[key]; syncRail(); save(); applyParams(); });
      row.append(lab, inp, out, what); $('mechanic').appendChild(row); specInputs[key] = { inp, out, src };
    }
    $('mechReset').addEventListener('click', () => { S.params = {}; syncRail(); save(); applyParams(); });
    function applyParams() { if (!W) return; for (const P of W.pilots) P.m = mults(P.lv); }
    const abCards = {};
    for (const el of ELS) { const e = (RECIPE.elements && RECIPE.elements[el]) || { name: 'open' }; const c = elx('div', 'ab'); c.style.setProperty('--accent', 'var(' + EL_COLOR[el] + ')'); const st = elx('div', 'st'), cd = elx('i', 'cd'); c.append(elx('div', 'ic', el[0].toUpperCase()), elx('div', 'nm', e.name), st, cd); $('abilities').appendChild(c); abCards[el] = { c, st, cd }; }
    // Input: the controls card from the recipe
    (function controls() {
      const c = RECIPE.controls || {}, box = $('controls'); box.textContent = '';
      const rows = [['Gamepad', (keyLayout === 'omni' ? 'left stick moves (strafe, forward), right stick turns' : 'both sticks steer (the game\'s mix)') + (c.lt ? ' \u00b7 LT ' + c.lt : '') + (c.rt ? ' \u00b7 RT ' + c.rt : '') + (c.a ? ' \u00b7 A ' + c.a : '') + (c.b ? ' \u00b7 B ' + c.b : '')],
        ['Keys', (keyLayout === 'omni' ? 'W A S D move \u00b7 arrows turn' : 'W S pitch \u00b7 A D yaw \u00b7 Q E roll \u00b7 \u2191 \u2193 throttle') + (c.lt ? ' \u00b7 Space ' + c.lt : '') + (c.rt ? ' \u00b7 F ' + c.rt : '') + (c.a ? ' \u00b7 G ' + c.a : '') + (c.b ? ' \u00b7 H ' + c.b : '')],
        ['Free cam', 'orbits the viewed hull: drag to turn \u00b7 wheel to zoom \u00b7 J L / I K turn \u00b7 U O zoom'],
        ['Transport', 'Enter play \u00b7 P pause / resume \u00b7 R stop \u00b7 C next camera'],
        ['Phone', 'opens by itself on a phone: two thumb sticks, LT / RT handles, A / B buttons']].concat(c.rows || []);   // a recipe's own rows (a mode that remaps the sticks)
      for (const [k, v] of rows) box.append(elx('span', null, k), elx('span', null, v));
      $('keysGamepad').textContent = rows[0][1]; $('keysKeys').textContent = rows[1][1];
      let at = $('keysKeys'); for (const [k, v] of (c.rows || [])) { const ks = elx('span', null, k), vs = elx('span', null, v); at.after(ks, vs); at = vs; }   // the start card gets a recipe's own rows too
    })();
    $('trigDepth').value = String(S.trigDepth); $('trigDepth').addEventListener('change', (e) => { S.trigDepth = +e.target.value; save(); });
    function syncRail() {
      styleSel.value = STYLES.some((s) => s.name === S.style) ? S.style : STYLES[0].name; if (S.style === 'Custom') { if (!styleSel.querySelector('option[value=Custom]')) { const o = elx('option', null, 'Custom'); o.value = 'Custom'; styleSel.appendChild(o); } styleSel.value = 'Custom'; }
      const st = styleOf(S.style); $('styleInfo').textContent = S.style === 'Custom' ? 'Custom levels.' : (st.info || '');
      const m = mults(S.lv);
      for (const el of ELS) { const r = elRows[el]; r.inp.value = S.lv[el]; r.out.textContent = S.lv[el]; r.badge.classList.toggle('on', S.lv[el] >= 5); r.wl.textContent = ''; const e = r.e; r.wl.append(document.createTextNode(e.name + ' '), elx('b', null, e.param && e.param in m ? fmt(m[e.param]) + (e.unit ? ' ' + e.unit : '') : '')); }
      for (const key in specInputs) { const s = specInputs[key]; s.inp.value = par(key); s.out.textContent = fmt(par(key)); s.inp.classList.toggle('changed', key in S.params); }
      $('mechCount').textContent = Object.keys(S.params).length ? Object.keys(S.params).length + ' changed' : 'recipe values';
    }
    const fmt = (x) => (Math.abs(x) >= 100 ? x.toFixed(0) : Math.abs(x) >= 10 ? x.toFixed(1) : x.toFixed(2)).replace(/\.0+$/, '');
    let runs = []; try { runs = JSON.parse(localStorage.getItem(LS + '.runs') || '[]') || []; } catch (e) { runs = []; }
    function saveRuns() { try { localStorage.setItem(LS + '.runs', JSON.stringify(runs)); } catch (e) { /* storage blocked */ } }
    function renderRuns() {
      const tb = $('runs'); tb.textContent = '';
      if (!runs.length) { const tr = elx('tr'), td = elx('td', null, 'No runs yet. Finish a race to add one.'); td.colSpan = 7; td.style.color = 'var(--muted)'; tr.appendChild(td); tb.appendChild(tr); return; }
      for (const r of runs.slice(0, 12)) { const tr = elx('tr'); tr.append(elx('td', null, 'I' + r.course), elx('td', null, r.pilot), elx('td', null, r.style || ''), elx('td', null, r.time != null ? r.time.toFixed(1) : 'DNF'), elx('td', null, String(r.place)), elx('td', null, r.mech + '%'), elx('td', null, String(r.top))); tb.appendChild(tr); }
    }
    // the scorecard (D5): a column per question, the no-mechanic control beside Hard, so it can say NO
    function renderScore(rows) {
      const tb = $('score'); tb.textContent = '';
      for (const c of [1, 2, 3, 4]) {
        const r = rows && rows[c], tr = elx('tr'); tr.append(elx('td', null, 'I' + c));
        for (const k of ['Easy', 'Medium', 'Hard', 'NoMech']) tr.append(elx('td', null, r && k in r ? (r[k] != null ? r[k].toFixed(1) : 'DNF') : '\u00b7'));
        tr.append(elx('td', null, r && r.mech != null ? Math.round(r.mech * 100) + '%' : '\u00b7'), elx('td', null, r && r.top != null ? String(Math.round(r.top)) : '\u00b7'));
        tb.appendChild(tr);
      }
    }
    let scoreRows = null; try { scoreRows = JSON.parse(localStorage.getItem(LS + '.score') || 'null'); } catch (e) { scoreRows = null; }
    renderScore(scoreRows);
    function runScorecard(seeds, done) {
      const rows = {}, jobs = []; for (const c of [1, 2, 3, 4]) { rows[c] = {}; for (const k of ['Easy', 'Medium', 'Hard', 'NoMech']) jobs.push([c, k]); }
      const med = (a) => { const v = a.filter((x) => x != null).sort((x, y) => x - y); return v.length > a.length / 2 ? v[Math.floor(v.length / 2)] : null; };
      let j = 0;
      const next = () => {
        if (j >= jobs.length) { done(rows); return; }
        const [c, k] = jobs[j++], rs = seeds.map((s) => raceHeadless(c, k === 'NoMech' ? 'Hard' : k, s, { mech: k !== 'NoMech' }));
        rows[c][k] = med(rs.map((r) => r.finish));
        if (k === 'Hard') { rows[c].mech = rs.reduce((a, r) => a + r.mechShare, 0) / rs.length; rows[c].top = Math.max(...rs.map((r) => r.top)); }
        renderScore(rows); setTimeout(next, 0);
      };
      setTimeout(next, 20);
    }
    $('scoreBtn').addEventListener('click', () => {
      $('scoreBtn').disabled = true; $('scoreNote').textContent = 'Racing every level on every course, with and without the mechanic\u2026';
      runScorecard([11, 22, 33], (rows) => { scoreRows = rows; try { localStorage.setItem(LS + '.score', JSON.stringify(rows)); } catch (e) { /* storage blocked */ } renderScore(rows); $('scoreBtn').disabled = false; $('scoreNote').textContent = 'Median of three seeds per cell, seconds; DNF = no finish in ' + BASE.limit + ' s. "No mech" is the Hard pilot forbidden its mechanic: if Hard is not faster than it, the mechanic is decoration. "Mech %" is the share of Hard\'s race spent in the mechanic; "Top" its top speed (u/s).'; });
    });

    // ---- the shared decision log (D33, the vision's rule: every accepted change is a recorded decision) ----
    const LOCAL_LOG = LS + '.decisions'; let db = null, me = '', localLog = []; try { localLog = JSON.parse(localStorage.getItem(LOCAL_LOG) || '[]') || []; } catch (e) { localLog = []; }
    function settingsDiff() { const changed = {}; for (const k in S.params) changed[k] = S.params[k]; return { vessel: RECIPE.id, kind: RECIPE.kind, style: S.style, lv: Object.assign({}, S.lv), changed, course: S.course, massDensity: S.massDensity }; }
    function renderDecisions(shared) {
      const box = $('decList'); box.textContent = '';
      const all = (shared || []).concat(localLog.map((d) => Object.assign({ local: true }, d))).sort((a, b) => (b.createdAt || 0) - (a.createdAt || 0));
      if (!all.length) { box.appendChild(elx('p', 'note', 'No decisions yet for the ' + RECIPE.name + '. Record the first: what you tried, what you decided.')); return; }
      for (const d of all.slice(0, 30)) { const r = elx('div', 'dec'); const h = elx('div', 'meta', (d.topic || 'Other') + ' \u00b7 ' + (d.createdAt ? new Date(d.createdAt).toLocaleString() : '') + (d.local ? ' \u00b7 this browser' : '')); const t = elx('div', 'txt', d.choice + (d.note ? ' \u2014 ' + d.note : '')); r.append(h, t); box.appendChild(r); }
    }
    (async function connectLog() {
      const c = window.claude; renderDecisions([]);
      if (!c || typeof c.use !== 'function') { $('decState').textContent = 'Kept in this browser (Copy log to send it). The shared log is in the claude.ai artifact and in Amoebius:'; $('decState').appendChild(VesselStudioTheme.artifactLink()); return; }
      try { db = await c.use('db'); } catch (e) { db = null; }
      try { const u = await c.use('user'); if (u) me = (await u.id()) || ''; } catch (e) { me = ''; }
      if (!db) { $('decState').textContent = 'The shared log is unavailable here; decisions stay in this browser.'; return; }
      db.collection('decisions').orderBy('createdAt', 'desc').limit(300).onSnapshot((snap) => { renderDecisions(snap.docs.map((x) => Object.assign({ id: x.id }, x.data())).filter((d) => !d.vessel || d.vessel === RECIPE.id || (d.settings && d.settings.vessel === RECIPE.id))); }, () => { $('decState').textContent = 'The shared log could not be read; decisions stay in this browser.'; });
    })();
    $('decBtn').addEventListener('click', async () => {
      const choice = $('decChoice').value.trim(); if (!choice) { $('decState').textContent = 'Say what you decided first.'; return; }
      const doc = { vessel: RECIPE.id, topic: $('decTopic').value, choice, note: $('decNote').value.trim(), settings: settingsDiff(), createdAt: Date.now(), by: me, kind: 'decision' };
      if (db) { try { await db.collection('decisions').add(doc); $('decState').textContent = 'Recorded in the shared log.'; $('decChoice').value = ''; $('decNote').value = ''; return; } catch (e) { /* fall through */ } }
      localLog.unshift(doc); try { localStorage.setItem(LOCAL_LOG, JSON.stringify(localLog)); } catch (e) { /* blocked */ } renderDecisions([]); $('decState').textContent = 'Recorded in this browser. Use Copy log to send it.'; $('decChoice').value = ''; $('decNote').value = '';
    });
    $('decCopy').addEventListener('click', async () => {
      const all = localLog.slice().reverse().map((d) => '- [' + d.topic + '] ' + d.choice + (d.note ? ' \u2014 ' + d.note : '') + ' ' + JSON.stringify(d.settings.changed)).join('\n');
      try { await navigator.clipboard.writeText(RECIPE.name + ' studio decisions\n' + all); $('decState').textContent = 'Copied.'; } catch (e) { $('decState').textContent = 'Could not copy here.'; }
    });
    // About: the recipe's words
    $('aboutWhy').textContent = RECIPE.why || ''; $('aboutFantasy').textContent = RECIPE.fantasy || '';
    const notList = $('aboutNot'); notList.textContent = ''; for (const s of (RECIPE.notModelled || []).concat(['The course is the game\'s Skim Race track; the neutral mass beside it is a lab stand-in for a cell\'s flora.', 'The AI is a simplified line-following pilot plus this vessel\'s mechanic rule; its difficulty mistakes are the game\'s, its press delay is lab-only.', 'Trails are capped at ' + BASE.rail.cap + ' prisms per pilot for the browser (the game conserves mass; nothing here decays).', 'No sound, no lifeforms, no team play.'])) notList.appendChild(elx('li', null, s));
    const srcList = $('aboutSources'); srcList.textContent = ''; for (const s of (RECIPE.sources || []).concat(['Courses: MinigameSkimRace.unity via Tools/Build/skimrace_track_fingerprint.py --emit-track; difficulty: SkimRaceDifficulty.asset; the stick mix: InputController.', 'Every mechanic number is a proposal (lab-only): Vessel Config > Mechanic shows each with its recipe value.'])) srcList.appendChild(elx('li', null, s));

    // ================= HUD =================
    function hud() {
      if (!W) return;
      const P = S.cam === 'Chase' && S.view === 0 ? W.pilots[0] : viewed(W); W.watch = P.i;
      $('hWho').textContent = P.domain.name + (P.spec.ai ? ' \u00b7 AI ' + P.spec.ai : '') + ' \u00b7 speed u/s';
      $('hSpeed').textContent = Math.round(P.speed * P.slow);
      $('hBoost').textContent = P.boost.toFixed(2) + '\u00d7'; $('mBoost').style.width = ((P.boost - 1) / (BASE.maxBoost - 1) * 100) + '%';
      const meters = (KIND.hud ? KIND.hud(P, ctx) : []) || [];
      for (let i = 0; i < 2; i++) { const m = meters[i]; $('hM' + (i + 1) + 'Lbl').textContent = m ? m[0] : ''; $('hM' + (i + 1)).textContent = m ? m[1] : ''; $('mM' + (i + 1)).style.width = (m ? clamp(m[2] || 0, 0, 1) * 100 : 0) + '%'; $('mM' + (i + 1)).style.background = m && m[3] ? m[3] : ''; }
      const b = $('board'); b.textContent = '';
      const hd = elx('div', 'hd'); hd.append(elx('span', null, 'I' + S.course + ' \u00b7 ' + W.t.toFixed(1) + ' s'), elx('span', null, 'crystals / ' + W.track.target)); b.appendChild(hd);
      W.pilots.slice().sort((a, c) => (a.finishT ?? 1e9) - (c.finishT ?? 1e9) || c.crystals - a.crystals).forEach((X) => {
        const row = elx('div', 'pl'), dot = elx('span', 'dot'); dot.style.background = X.domain.css;
        const st = elx('span', 'st', X.finishT != null ? X.finishT.toFixed(1) + ' s' : X.crystals + (X.spec.ai ? ' \u00b7 ' + X.aiState : ''));
        if (X.hc && X.hc.misjudged) st.style.color = 'var(--hard)'; else if (X.hc && X.hc.unnoticed) st.style.color = 'var(--medium)';
        row.append(dot, elx('span', null, X.domain.name + (X.spec.ai ? ' \u00b7 ' + X.spec.ai : '')), st); b.appendChild(row);
      });
      const ab = (KIND.abilityStates ? KIND.abilityStates(P, ctx) : {}) || {};
      for (const el of ELS) { const c = abCards[el]; c.st.textContent = ab[el] != null ? ab[el] : ''; c.c.style.borderColor = P.lv[el] >= 5 ? 'var(' + EL_COLOR[el] + ')' : ''; c.cd.style.width = ab[el + 'Fill'] != null ? clamp(ab[el + 'Fill'], 0, 1) * 100 + '%' : '0'; }
      $('readWho').textContent = P.domain.name + (P.spec.ai ? ' (AI ' + P.spec.ai + ')' : '');
      const fwd = new V(0, 0, -1).applyQuaternion(P.quat);
      const rows = [['speed', (P.speed * P.slow).toFixed(1) + ' u/s'], ['top speed', P.stats.top.toFixed(0) + ' u/s'], ['boost', P.boost.toFixed(2) + '\u00d7'], ['slip', (Math.acos(clamp(P.course.dot(fwd), -1, 1)) / DEG).toFixed(1) + '\u00b0'], ['crystals', P.crystals + ' / ' + W.track.target], ['in mechanic', Math.round(P.stats.mechT / Math.max(1e-6, W.t) * 100) + '%'], ['uses', String(P.stats.uses)], ['skims', String(P.skims)], ['hull contacts', String(P.resets)], ['petals', P.petals.toFixed(1)], ['mistakes', P.spec.ai ? String(P.mistakes) : '\u2014'], ['AI state', P.spec.ai ? P.aiState : (aiHolding ? 'holding your idle hull' : 'player')]].concat((KIND.live ? KIND.live(P, ctx) : []) || []);
      const box = $('read'); if (box.childElementCount !== rows.length * 2) { box.textContent = ''; for (const [k] of rows) box.append(elx('span', null, k), elx('b')); }
      rows.forEach(([, v], i) => { box.children[i * 2 + 1].textContent = v; });
      const live = race === 'running' && !paused;
      $('tpPlay').textContent = live ? '\u275a\u275a Pause' : '\u25b6 Play'; $('tpPlay').classList.toggle('on', live); $('tpPlay').setAttribute('aria-pressed', paused ? 'true' : 'false');
      $('tpStop').disabled = race === 'ready' && !stopHeld; $('tStart').textContent = live ? '\u275a\u275a Pause' : '\u25b6 Play';
      $('hAlert').hidden = !(aiHolding && race === 'running'); $('hAlert').textContent = 'AI HOLDING YOUR HULL \u00b7 touch any control to fly';
      while (W.events.length) say(W.events.shift());
    }
    function markCrystal(W) {
      const P = S.cam === 'Chase' && S.view === 0 ? W.pilots[0] : viewed(W);
      if (!P.crystal || race !== 'running') { mark.hide(); return; }   // not over the start or finish card
      cam.updateMatrixWorld();
      mark.set(cam, P.crystal.pos, 'Crystal ' + (P.crystals + 1) + '/' + W.track.target + ' \u00b7 ' + Math.round(P.crystal.pos.distanceTo(P.pos)) + ' u', '#' + new T.Color(P.domain.color).getHexString(), canvas.clientWidth, canvas.clientHeight);
    }

    // ================= loop =================
    let lastW = 0, lastH = 0;
    function resize() { const w = canvas.clientWidth || 960, h = canvas.clientHeight || Math.round(w * 10 / 16); if (w === lastW && h === lastH) return; lastW = w; lastH = h; renderer.setSize(w, h, false); cam.aspect = w / h; cam.updateProjectionMatrix(); }
    window.addEventListener('resize', resize);
    let last = performance.now(), hudT = 0;
    const LAB = { manualClock: false };
    function frame(now) {
      const dt = Math.min(0.05, (now - last) / 1000); last = now; resize();
      const hin = humanInput(dt);
      if (W && race === 'running' && !paused && !LAB.manualClock) {
        for (let s = 0; s < S.speed; s++) { stepWorld(W, dt, W.pilots.map((P) => pilotInput(W, P, hin))); if (W.over) { finishRace(); break; } }
      }
      ENV.update(dt);
      if (W) { drawWorld(W, now / 1000); placeCamera(W, dt, false); markCrystal(W); }
      renderer.render(scene, cam);
      hudT += dt; if (hudT > 0.1) { hudT = 0; hud(); }
      requestAnimationFrame(frame);
    }
    // ================= the editor layout (D8): the six settings tabs in the universal order (D21, D25, D27) =================
    const IDE = VesselStudioIDE.build({
      key: RECIPE.id + '-studio-ide-v1', host: document.querySelector('.layout'), stage: $('viewport'),
      right: [['scene', 'Scene Config', [$('grp-scene'), $('grp-world')]], ['game', 'Game Config', [$('grp-intensity'), $('grp-players')]], ['ai', 'AI Config', [$('grp-ai'), document.querySelector('.afternote')]],
        ['style', 'Play Style Config', [$('grp-style'), $('grp-elements')]], ['input', 'Input', [$('grp-controls'), $('grp-feel')]],
        ['others', 'Vessel Config', [$('grp-mechanic'), $('grp-abilities'), $('grp-live')]]],
      bottom: [['score', 'Scorecard', [$('grp-score')]], ['runs', 'Runs', [$('grp-runs')]], ['decisions', 'Decisions', [$('grp-decisions')]], ['about', 'About', [document.querySelector('header .sub'), $('grp-about')]]],
      railW: 390, dockH: 250, winSize: { score: [820, 400], runs: [700, 360], decisions: [520, 420] },
      onLayout: () => { lastW = lastH = 0; },
    });
    VesselStudioIDE.sectionPicker(document.querySelector('.vside-pane[data-tab="others"]'), RECIPE.id + '-studio', 'vessel');   // Vessel Config: one section at a time (D27)
    syncRail(); renderRuns();
    newRace(); hud();
    requestAnimationFrame((t) => { last = t; frame(t); });
    if (PLATFORM.device === 'phone') setPlayMode(true);   // detected, never asked (D26)

    // ================= hooks: the studio hook (D14) and the /labmaker contract (__lab) =================
    const SHIPPED = Object.assign({}, PARAMS);   // the recipe's values: this studio's "shipped" block (a design: nothing ships, D2)
    Object.assign(LAB, {
      SHIPPED, SPEC: SPEC.map((r) => ['mechanic'].concat(r)), P: S.params, PLATFORM,   // the labmaker shape: [group, key, lo, hi, step, label, tip]
      reset: (seed) => { seedN = seed == null ? 1 : seed; newRace(); race = 'running'; paused = false; $('overlay').hidden = true; },
      tick: (dt, n) => { for (let i = 0; i < (n || 1); i++) { if (!W || W.over) break; stepWorld(W, dt, W.pilots.map((P) => P.spec.ai ? aiInput(W, P) : { pad: true, L: { x: 0, y: 0 }, R: { x: 0, y: 0 }, lt: 0, rt: 0, a: false, b: false })); } },
      state: () => ({ t: W ? +W.t.toFixed(4) : 0, race, vessel: RECIPE.id }),
      score: (done) => runScorecard([11, 22, 33], done || (() => {})),
      runBatch: () => { const h = raceHeadless(1, 'Hard', 11), e = raceHeadless(1, 'Easy', 11), c = raceHeadless(1, 'Hard', 11, { mech: false }); return { hardI1: h.finish, hardMech: h.mechShare, hardTop: h.top, easyI1: e.finish, noMechI1: c.finish }; },
    });
    window.__lab = LAB;
    window.__vesselStudio = {
      get state() { return { race, t: +W.t.toFixed(2), vessel: RECIPE.id, kind: RECIPE.kind, course: S.course, target: W.track.target, prisms: W.prisms.filter((p) => !p.dead).length, pilots: W.pilots.map((p) => ({ name: p.domain.name, ai: p.spec.ai, style: p.spec.style, crystals: p.crystals, finishT: p.finishT, speed: +p.speed.toFixed(1), top: +p.stats.top.toFixed(1), mechT: +p.stats.mechT.toFixed(2), uses: p.stats.uses, active: !!p.K.active, resets: p.resets, skims: p.skims, mistakes: p.mistakes, state: p.aiState })) }; },
      get race() { return race; },
      set: (k, v) => {
        if (k === 'you') { const ps = S.players.map((x) => Object.assign({}, x)); ps[0].ai = v !== 'You'; if (v !== 'You') ps[0].level = v; racePanel.set('players', ps); return; }
        if (k === 'course') { racePanel.set('course', v); return; }
        if (k === 'players') { racePanel.set('players', v); return; }
        S[k] = v; save(); newRace();
      },
      startRace, stopRace, newRace, togglePause, raceHeadless, runScorecard, makeWorld, stepWorld, aiInput, mults, RECIPE, KIND, PARAMS, BASE, S, ide: IDE,
      get world() { return W; },
    };
  }

  window.VesselStudioEngine = { start, TRACKS, COURSE_INFO, BASE, DIFF };
})();
