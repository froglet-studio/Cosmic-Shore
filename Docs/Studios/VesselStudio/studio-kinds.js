/* studio-kinds.js - the MOVEMENT KINDS a vessel recipe can pick (/vessel-studio D40, the vision's B3). Each kind is one
 * way of moving through the HyperSea, written once: its physics step, what a hull contact means to it, how its AI uses it
 * through the sticks and triggers (input-only, D4), its two HUD meters, its hull, its stage visuals and its hub preview.
 * studio-vessel.js runs whichever kind the recipe names; the recipe's `params` are the kind's numbers (every one lab-only,
 * a proposal) and its `elements` say which parameter each of the four elements scales.
 *
 * The ten kinds of the concept fleet (Docs/Studios/CONCEPT_FLEET.md), each a movement players are loving right now:
 *   swing      Gibbon     tether to any prism, pendulum round it, reel in, fling (grapple / web-swing)
 *   ricochet   Flea       bounce off mass; a press inside the window banks momentum (pinball + perfect-landing timing)
 *   soar       Albatross  no engine: dive for speed, spread for lift, ride the updraft over mass (glider / dynamic soaring)
 *   slipstream Remora     fast only in a rival's wake; latch on, get towed, sling past (racing draft)
 *   hover      Dragonfly  omnidirectional thrust, instant strafes, a three-pip dash with i-frames (arena movement)
 *   burrow     Mole       slow in the open, fast INSIDE mass; dig tunnels others can follow (the obstacle is the road)
 *   momentum   Marlin     velocity decoupled from the nose: thrust, retro, flip-and-burn (3D drift / Newtonian)
 *   body       Eel        a segmented body that IS your trail; coil to turn, whip to cut, shed to speed up (snake)
 *   pulse      Octopus    inhale to charge, release to jet; pulses on your own tempo multiply (charge-release + rhythm)
 *   surface    Gecko      stick to any mass and run along it faster, hop between surfaces (wall-run)
 *
 * No THREE at the top level: the hub loads this file for the previews without three.js. ASCII only (served without a
 * charset). Functions receive `ctx` from the engine: THREE, V, Q, lerp, clamp, DEG, axes, steer, move, moveVel, near,
 * nearest, layPrism, removePrism, event, rivals, aimAt, l5, BASE, FLIGHT.
 */
(function () {
  'use strict';
  if (window.VesselStudioKinds) return;
  const K = {};
  const lerp = (a, b, t) => a + (b - a) * t, clamp = (x, a, b) => Math.max(a, Math.min(b, x));
  const edge = (P, key, v) => { const was = !!P.K[key]; P.K[key] = !!v; return !!v && !was; };   // a press this frame
  const fall = (P, key, v) => { const was = !!P.K[key]; P.K[key] = !!v; return !v && was; };   // a release this frame
  function mat(ctx, color, extra) { return new ctx.THREE.MeshStandardMaterial(Object.assign({ color, roughness: 0.4, metalness: 0.2, emissive: new ctx.THREE.Color(color).multiplyScalar(0.18) }, extra || {})); }
  function box(ctx, m, x, y, z, px, py, pz) { const b = new ctx.THREE.Mesh(new ctx.THREE.BoxGeometry(x, y, z), m); b.position.set(px || 0, py || 0, pz || 0); return b; }
  function line(ctx, color, n) { const g = new ctx.THREE.BufferGeometry().setFromPoints(Array.from({ length: n || 2 }, () => new ctx.V())); const l = new ctx.THREE.Line(g, new ctx.THREE.LineBasicMaterial({ color, transparent: true, opacity: 0.9 })); l.frustumCulled = false; return l; }
  function setLine(l, pts) { const a = l.geometry.attributes.position; for (let i = 0; i < pts.length && i < a.count; i++) a.setXYZ(i, pts[i].x, pts[i].y, pts[i].z); a.needsUpdate = true; l.geometry.setDrawRange(0, Math.min(pts.length, a.count)); }
  // what counts as mass to a mechanic; the engine's Scene Config can take the course ribbon out (K._ribbonIsMass = false, lab-only)
  const isMass = (p) => !p.dead && ((p.kind === 'track' && K._ribbonIsMass !== false) || p.kind === 'mass' || p.kind === 'rail' || p.kind === 'tunnel' || p.kind === 'shed' || p.kind === 'laid');
  // 2D preview helpers (the hub's card canvas)
  function bg(g, w, h) { g.fillStyle = '#05060c'; g.fillRect(0, 0, w, h); }
  function blocks(g, color, list, s) { g.fillStyle = color; for (const [x, y] of list) g.fillRect(x - (s || 7) / 2, y - (s || 7) / 2, s || 7, s || 7); }
  function dart(g, color, x, y, a, s) { s = s || 1; g.save(); g.translate(x, y); g.rotate(a); g.fillStyle = color; g.beginPath(); g.moveTo(14 * s, 0); g.lineTo(-8 * s, -7 * s); g.lineTo(-3 * s, 0); g.lineTo(-8 * s, 7 * s); g.closePath(); g.fill(); g.restore(); }

  // ================= swing: the Gibbon =================
  K.swing = {
    mechanicName: 'swinging', aiRule: 'It tethers when its crystal is more than 35 deg off the nose and a prism sits on the inside of that turn within reach, reels at 60% through the arc, and lets go when the nose points within 12 deg of the crystal or after 160 deg of swing.',
    defaults: { reach: 150, minRope: 22, reelRate: 45, kick: 1.3, kickAngle: 70, topSpeed: 170, keep: 0.25, cone: 60 },
    spec: [['reach', 60, 300, 5, 'Tether reach', 'How far the tether can reach for a prism (u)'], ['minRope', 10, 60, 1, 'Shortest rope', 'The rope cannot be reeled shorter than this (u)'], ['reelRate', 10, 120, 5, 'Reel rate', 'How fast LT reels the rope in (u/s at full pull); shortening the rope speeds you up'],
      ['kick', 1, 1.8, 0.05, 'Release kick', 'Speed multiplier on a release after a full swing'], ['kickAngle', 20, 180, 5, 'Swing for the kick', 'Degrees of arc a swing needs before its release pays the kick'], ['topSpeed', 80, 300, 5, 'Top speed', 'Speed cap on the rope and after a fling (u/s)'], ['keep', 0.05, 1.5, 0.05, 'Momentum fade', 'How fast a fling fades back to cruise (per second)'], ['cone', 20, 120, 5, 'Tether cone', 'Half-angle round the nose a prism must be in to be tethered (deg)']],
    init(P) { P.K = { anchor: null, L: 0, swept: 0, active: false, mem: null, memT: -9, lastR: null, converted: 0 }; },
    candidate(W, P, ctx) {
      const { fwd, right } = ctx.axes(P), m = P.m; let best = null, bs = -1;
      for (const p of ctx.near(W, P.pos, m.reach)) {
        if (!isMass(p) || p.owner === P.i) continue; const d = p.pos.clone().sub(P.pos), dist = d.length(); if (dist < m.minRope || dist > m.reach) continue; d.divideScalar(dist);
        const f = d.dot(fwd); if (f < Math.cos(m.cone * ctx.DEG)) continue;
        const s = f * 0.6 + Math.abs(d.dot(right)) * 0.5 + (1 - dist / m.reach) * 0.2; if (s > bs) { bs = s; best = p; }
      }
      return best;
    },
    step(W, P, inp, dt, ctx) {
      const m = P.m, Kk = P.K, { fwd } = ctx.axes(P);
      ctx.steer(P, inp, dt, Kk.active ? 0.45 : 1);
      const c = K.swing.candidate(W, P, ctx); if (c) { Kk.mem = c; Kk.memT = W.t; } Kk.memFresh = !!c;   // target memory: a press just after passing one still fires (Echo Point Nova's rule)
      if (!Kk.active) {
        const target = P.F.cruise * Math.max(0.35, P.throttle) * P.boost;
        ctx.move(P, dt, target, P.F.grip, P.speed > target ? m.keep : P.F.accel);
        if (edge(P, 'rtWas', inp.rt > 0.5)) { const a = c || (W.t - Kk.memT < 0.5 ? Kk.mem : null); if (a && !a.dead) { Kk.anchor = a; Kk.L = Math.max(m.minRope, a.pos.distanceTo(P.pos)); Kk.swept = 0; Kk.active = true; Kk.lastR = P.pos.clone().sub(a.pos).normalize(); a.flash = 1; W.dirty = true; P.stats.uses++; ctx.event(W, P, 'Tethered'); } }
        return;
      }
      // on the rope: a pendulum with no gravity, so the arc is the rope; reeling trades rope for speed
      const a = Kk.anchor, r = P.pos.clone().sub(a.pos); let dist = r.length(); const rhat = r.clone().divideScalar(Math.max(1e-3, dist));
      if (inp.lt > 0.05) { const L0 = Kk.L; Kk.L = Math.max(m.minRope, Kk.L - inp.lt * m.reelRate * dt); P.speed = Math.min(m.topSpeed, P.speed * (L0 / Kk.L)); }
      const vel = P.course.clone().multiplyScalar(P.speed);
      if (dist > Kk.L) { P.pos.copy(a.pos).addScaledVector(rhat, Kk.L); dist = Kk.L; const vr = vel.dot(rhat); if (vr > 0) vel.addScaledVector(rhat, -vr); }
      P.speed = Math.min(m.topSpeed, Math.max(P.speed, vel.length())); if (vel.lengthSq() > 1e-4) P.course.copy(vel).normalize();
      P.pos.addScaledVector(P.course, P.speed * dt); P.vel.copy(P.course).multiplyScalar(P.speed);
      // the nose follows the arc (the camera and the pilot see the swing), the sticks only lean it
      const look = new ctx.Q().setFromRotationMatrix(new ctx.THREE.Matrix4().lookAt(new ctx.V(), P.course.clone().negate(), rhat)); P.quat.slerp(look, 1 - Math.exp(-5 * dt));
      const nr = P.pos.clone().sub(a.pos).normalize(); Kk.swept += Math.acos(clamp(nr.dot(Kk.lastR), -1, 1)) / ctx.DEG; Kk.lastR = nr;
      P.K.rtWas = inp.rt > 0.5;
      if (inp.rt <= 0.5 || a.dead || dist < m.minRope * 0.8 || Kk.swept > 200) {   // release: the fling
        const earned = Kk.swept >= m.kickAngle; P.speed = Math.min(m.topSpeed, P.speed * (earned ? m.kick : 1));
        if (ctx.l5(P, 'mass') && a.owner !== P.i && a.kind !== 'track') { a.domain = P.domain.key; a.owner = P.i; Kk.converted++; P.stolen++; W.dirty = true; }
        ctx.event(W, P, earned ? 'Fling! ' + Math.round(Kk.swept) + ' deg, x' + m.kick.toFixed(2) : 'Let go at ' + Math.round(Kk.swept) + ' deg (no kick)');
        Kk.active = false; Kk.anchor = null;
      }
    },
    onContact(W, P, p) { return P.K.active && p === P.K.anchor; },
    ai(W, P, aim, base, ctx) {
      const Kk = P.K, m = P.m;
      if (Kk.active) {   // let go when the VELOCITY (not the lagging nose) points at the aim, with one frame of look-ahead at this swing's rate
        const toAim = aim.clone().sub(P.pos).normalize(), vErr = Math.acos(clamp(P.course.dot(toAim), -1, 1)) / ctx.DEG, rate = P.speed / Math.max(1, Kk.L) / ctx.DEG;
        const done = (vErr < Math.max(16, rate / 30 * 0.8) && Kk.swept >= Math.min(30, m.kickAngle * 0.5)) || Kk.swept > 170;
        if (done) Kk.releaseT = W.t; return { rt: done ? 0 : 1, lt: 0.5 };
      }
      // a swing is its speed source: tether whenever a prism is in reach and it is either off line or slow, not straight after a fling
      if (W.t - (Kk.releaseT || -9) < 1 || P.aimWhy === 'turn back') return {};
      const c = K.swing.candidate(W, P, ctx); if (!c) return {};
      const { right } = ctx.axes(P), toC = c.pos.clone().sub(P.pos), dist = toC.length(), side = toC.dot(right), toAim = aim.clone().sub(P.pos).normalize();
      const arc = Math.abs(side) / dist >= 0.3;   // a prism well off the nose makes an arc; one dead ahead only snaps the rope when it goes past
      if (!arc) return {};
      // a turn: swing round a prism on the inside of it. (Swinging on a straight for the reel's speed was tried: on I1 every
      // straight swing was a detour and the Hard pilot came in 15% behind the no-mechanic control, so the AI swings for turns.)
      if (base.err > 25) return (Math.sign(side) === Math.sign(base.yawErr)) ? { rt: 1 } : {};
      return {};
    },
    hud(P) { const m = P.m, Kk = P.K; return [['Rope', Kk.active ? Math.round(Kk.L) + ' u' : (Kk.memFresh ? 'target in reach' : 'no target'), Kk.active ? Kk.L / m.reach : 0, !Kk.active && Kk.memFresh ? '#9dff5a' : ''], ['Swing', Kk.active ? Math.round(Kk.swept) + '\u00b0' : 'RT to tether', Kk.active ? Kk.swept / m.kickAngle : 0, Kk.active && Kk.swept >= m.kickAngle ? '#9dff5a' : '']]; },
    abilityStates(P) { const m = P.m; return { charge: 'kick \u00d7' + m.kick.toFixed(2), mass: P.K.converted + ' taken', space: Math.round(m.reach) + ' u reach', time: Math.round(m.reelRate) + ' u/s reel' }; },
    hull(ctx, color) { const g = new ctx.THREE.Group(), m = mat(ctx, color); g.add(box(ctx, m, 3, 2.2, 6)); g.add(box(ctx, m, 1, 0.8, 9, -2.6, 0, -1.5)); g.add(box(ctx, m, 1, 0.8, 9, 2.6, 0, -1.5)); g.add(box(ctx, m, 5, 0.5, 2, 0, 1.2, 2.5)); return g; },
    mount(ctx, scene, P) { const l = line(ctx, P.domain.color, 2); scene.add(l); const ring = new ctx.THREE.Mesh(new ctx.THREE.TorusGeometry(5, 0.4, 6, 24), new ctx.THREE.MeshBasicMaterial({ color: P.domain.color, transparent: true, opacity: 0.7 })); scene.add(ring); return { l, ring, dispose() { scene.remove(l); scene.remove(ring); } }; },
    draw(W, P, o, t, ctx) { const Kk = P.K; const a = Kk.active ? Kk.anchor : ((!P.spec.ai || P.i === W.watch) && W.t - Kk.memT < 0.2 ? Kk.mem : null); o.l.visible = Kk.active; o.ring.visible = !!a && !a.dead; if (Kk.active) setLine(o.l, [P.pos, Kk.anchor.pos]); if (a && !a.dead) { o.ring.position.copy(a.pos); o.ring.lookAt(P.pos); o.ring.material.opacity = Kk.active ? 0.9 : 0.35 + 0.25 * Math.sin(t * 6); } },
    preview(g, w, h, t, accent) { bg(g, w, h); const ax = w * 0.55, ay = h * 0.28; blocks(g, '#3a4380', [[ax, ay], [ax + 9, ay + 4], [ax - 8, ay + 6], [w * 0.2, h * 0.3], [w * 0.88, h * 0.25]], 8); const ph = (t * 1.1) % 3; let x, y, a; if (ph < 2) { const th = -2.2 + ph * 1.1 * 1.6, r = 70 - ph * 12; x = ax + Math.sin(th) * r; y = ay + Math.cos(th) * r; a = th + Math.PI; g.strokeStyle = accent + 'aa'; g.lineWidth = 1.5; g.beginPath(); g.moveTo(ax, ay); g.lineTo(x, y); g.stroke(); } else { const u = ph - 2; x = ax + Math.sin(1.32) * 46 + u * 140; y = ay + Math.cos(1.32) * 46 - u * 30; a = -0.2; for (let i = 1; i < 6; i++) { g.globalAlpha = 0.4 - i * 0.06; dart(g, accent, x - i * 10, y + i * 2, a, 0.8); } g.globalAlpha = 1; } dart(g, accent, x, y, a); },
  };

  // ================= ricochet: the Flea =================
  K.ricochet = {
    mechanicName: 'bouncing', hullReach: 4.5, aiRule: 'It aims at a prism that lies between it and its crystal, presses RT just before the predicted contact (inside 40% of the window), and crouches to hop when it has crawled below 70% of cruise with no mass near.',
    defaults: { crawl: 50, window: 0.18, perfectGain: 1.35, dullLoss: 0.85, topSpeed: 260, grip: 2.2, drag: 0.25, hopCharge: 0.6, hopMin: 60, hopMax: 160, tellRange: 160 },
    spec: [['crawl', 15, 80, 1, 'Crawl speed', 'Speed it settles to between bounces (u/s)'], ['window', 0.06, 0.5, 0.01, 'Perfect window', 'Press RT within this many seconds of a contact for a perfect bounce'], ['perfectGain', 1.05, 1.8, 0.05, 'Perfect gain', 'Speed multiplier on a perfect bounce'], ['dullLoss', 0.5, 1, 0.05, 'Dull bounce', 'Speed multiplier on a bounce with no press'],
      ['topSpeed', 100, 400, 10, 'Top speed', 'Speed cap (u/s)'], ['grip', 0.3, 6, 0.1, 'Velocity grip', 'How fast the velocity turns toward the nose between bounces (per second): low = ballistic'], ['drag', 0.05, 1.5, 0.05, 'Momentum fade', 'How fast speed above crawl fades (per second)'], ['hopCharge', 0.2, 1.5, 0.05, 'Hop charge time', 'Seconds of LT crouch for a full hop'], ['hopMax', 60, 260, 5, 'Full hop', 'Speed of a fully charged hop (u/s)'], ['tellRange', 60, 300, 10, 'Tell range', 'How far ahead the bounce tell looks (u)']],
    init(P) { P.K = { pressT: -9, bounceT: -9, dullAt: -9, chain: 0, charge: 0, crouch: false, next: null, nextT: 9, active: false, perfects: 0 }; },
    predict(W, P, ctx) { const m = P.m; let best = null, bt = 9; for (const p of ctx.near(W, P.pos, m.tellRange)) { if (!isMass(p)) continue; const d = p.pos.clone().sub(P.pos), along = d.dot(P.course); if (along < 2) continue; const off = Math.sqrt(Math.max(0, d.lengthSq() - along * along)); if (off > 6.5) continue; const t = along / Math.max(1, P.speed); if (t < bt) { bt = t; best = p; } } P.K.next = best; P.K.nextT = bt; return best; },
    // the bounce surface: a prism is a long box, so the normal is the hull's offset from the prism's AXIS (hit the ribbon at a
    // shallow angle and you skip along it, keeping your along-track speed), falling back to the centre when the hull is on the axis
    normal(P, p, ctx) { const axis = new ctx.V(0, 0, 1).applyQuaternion(p.quat), d = P.pos.clone().sub(p.pos), n = d.clone().addScaledVector(axis, -d.dot(axis)); if (n.lengthSq() < 0.25) n.copy(new ctx.V(0, 1, 0).applyQuaternion(P.quat)); return n.normalize(); },
    step(W, P, inp, dt, ctx) {
      const m = P.m, Kk = P.K, { fwd } = ctx.axes(P);
      ctx.steer(P, inp, dt, 1);
      if (edge(P, 'rtWas', inp.rt > 0.5)) { Kk.pressT = W.t; if (W.t - Kk.dullAt <= m.window / 2) { P.speed = Math.min(m.topSpeed, P.speed * (m.perfectGain / m.dullLoss)); Kk.chain++; Kk.perfects++; P.stats.uses++; Kk.dullAt = -9; ctx.event(W, P, 'Late but perfect! chain ' + Kk.chain); } }
      Kk.crouch = inp.lt > 0.3;
      if (Kk.crouch) { P.K.ltWas = true; Kk.charge = Math.min(1, Kk.charge + dt / m.hopCharge); P.speed += (15 - P.speed) * (1 - Math.exp(-4 * dt)); }
      else if (fall(P, 'ltWas', inp.lt > 0.3) && Kk.charge > 0.05) { P.speed = Math.max(P.speed, lerp(m.hopMin, m.hopMax, Kk.charge)); P.course.copy(fwd); Kk.charge = 0; ctx.event(W, P, 'Hop'); }
      else { P.K.ltWas = false; const k = P.speed > m.crawl ? m.drag : 2.0; P.speed += (m.crawl * P.boost - P.speed) * (1 - Math.exp(-k * dt)); }
      P.course.lerp(fwd, 1 - Math.exp(-m.grip * dt)).normalize();
      P.vel.copy(P.course).multiplyScalar(P.speed); P.pos.addScaledVector(P.course, P.speed * P.slow * dt);
      K.ricochet.predict(W, P, ctx);
      if (W.t - Kk.bounceT > 4) Kk.chain = 0;   // a chain that is not kept up is lost
      Kk.active = (Kk.chain > 0 && W.t - Kk.bounceT < 2.5) || Kk.crouch;
    },
    onContact(W, P, p, ctx) {
      const m = P.m, Kk = P.K, n = K.ricochet.normal(P, p, ctx), vel = P.course.clone().multiplyScalar(P.speed), vn = vel.dot(n);
      if (vn < 0) vel.addScaledVector(n, -2 * vn); P.pos.addScaledVector(n, 2.5);
      const perfect = W.t - Kk.pressT >= 0 && W.t - Kk.pressT <= m.window / 2;
      P.speed = Math.min(m.topSpeed, vel.length() * (perfect ? m.perfectGain : m.dullLoss)); if (vel.lengthSq() > 1e-4) P.course.copy(vel).normalize();
      P.quat.slerp(new ctx.Q().setFromRotationMatrix(new ctx.THREE.Matrix4().lookAt(new ctx.V(), P.course.clone().negate(), new ctx.V(0, 1, 0))), 0.8);
      Kk.bounceT = W.t; p.flash = 1; W.dirty = true;
      if (perfect) { Kk.chain++; Kk.perfects++; P.stats.uses++; ctx.event(W, P, 'Perfect bounce! chain ' + Kk.chain + ' \u00b7 ' + Math.round(P.speed) + ' u/s'); } else { Kk.chain = 0; Kk.dullAt = W.t; ctx.event(W, P, 'Dull bounce'); }
      return true;
    },
    aiAim(W, P, aim, why, ctx) {
      const m = P.m; if (why === 'turn back') return null;
      if (why === 'line') return { aim: aim.clone().add(new ctx.V(0, -8, 0)), why: 'skip' };   // fly AT the ribbon, not above it: every contact is a skip
      const dir = aim.clone().sub(P.pos), dist = dir.length(); dir.normalize(); let best = null, bs = 0;
      for (const p of ctx.near(W, P.pos, Math.min(m.tellRange, dist))) { if (!isMass(p)) continue; const d = p.pos.clone().sub(P.pos), l = d.length(); if (l < 25) continue; const c = d.divideScalar(l).dot(dir); if (c > 0.85 && c > bs) { bs = c; best = p; } }
      return best ? { aim: best.pos.clone(), why: 'bounce' } : null;
    },
    ai(W, P, aim, base, ctx) { const m = P.m, Kk = P.K; const out = {}; if (Kk.next && Kk.nextT < m.window * 0.4) out.rt = 1; if (!Kk.next && P.speed < m.crawl * 0.7 && base.err < 20) out.lt = Kk.charge < 1 ? 1 : 0; return out; },
    hud(P) { const m = P.m, Kk = P.K, t = Kk.nextT; return [['Bounce in', Kk.next ? t.toFixed(2) + ' s' : (Kk.crouch ? 'hop ' + Math.round(Kk.charge * 100) + '%' : 'no mass ahead'), Kk.next ? 1 - clamp(t / 1.5, 0, 1) : Kk.charge, Kk.next && t <= m.window ? '#9dff5a' : ''], ['Chain', '\u00d7' + Kk.chain, clamp(Kk.chain / 6, 0, 1)]]; },
    abilityStates(P) { const m = P.m; return { charge: '\u00d7' + m.perfectGain.toFixed(2) + ' perfect', mass: Math.round(m.topSpeed) + ' u/s cap', space: Math.round(m.hopMax) + ' u/s hop', time: (m.window * 1000).toFixed(0) + ' ms window' }; },
    hull(ctx, color) { const g = new ctx.THREE.Group(), m = mat(ctx, color); const b = new ctx.THREE.Mesh(new ctx.THREE.SphereGeometry(2.6, 10, 8), m); b.scale.set(1, 0.8, 1.3); g.add(b); g.add(box(ctx, m, 0.8, 3.5, 1, -2.4, -1, 1.5)); g.add(box(ctx, m, 0.8, 3.5, 1, 2.4, -1, 1.5)); return g; },
    poseHull(h, P) { h.scale.setScalar(1 + 0.4 * Math.exp(-(P.K.tSince || 9))); },   // the hull puffs on a bounce
    draw(W, P) { P.K.tSince = (W.t - P.K.bounceT) * 6; },
    preview(g, w, h, t, accent) { bg(g, w, h); blocks(g, '#3a4380', [[w * 0.2, h * 0.8], [w * 0.5, h * 0.18], [w * 0.8, h * 0.78], [w * 0.95, h * 0.3]], 9); const pts = [[w * 0.05, h * 0.5], [w * 0.2, h * 0.75], [w * 0.5, h * 0.23], [w * 0.8, h * 0.73], [w * 0.95, h * 0.35]]; const u = (t * 0.6) % 4, i = Math.floor(u), f = u - i, a = pts[i], b = pts[i + 1]; const x = lerp(a[0], b[0], f), y = lerp(a[1], b[1], f); g.strokeStyle = accent + '55'; g.lineWidth = 1; g.beginPath(); g.moveTo(pts[0][0], pts[0][1]); for (const p of pts) g.lineTo(p[0], p[1]); g.stroke(); if (f < 0.15) { g.strokeStyle = '#9dff5a'; g.lineWidth = 2; g.beginPath(); g.arc(a[0], a[1], 12 + f * 60, 0, Math.PI * 2); g.stroke(); } g.fillStyle = accent; g.beginPath(); g.arc(x, y, 6, 0, Math.PI * 2); g.fill(); },
  };

  // ================= soar: the Albatross =================
  K.soar = {
    mechanicName: 'soaring', aiRule: 'Below 90 u/s it folds (RT) and noses down until it has 150 again; high above the line and slowing it folds to come back down; in an updraft with speed to spare it spreads (LT) and climbs. The line pilot points the nose; gravity does the rest.',
    defaults: { g: 25, liftK: 0.0065, dragK: 0.00008, spreadLift: 1.8, spreadDrag: 2.2, foldLift: 0.3, foldDrag: 0.45, thermal: 15, thermalR: 70, thermalH: 350, topSpeed: 300, stall: 30, grip: 4, maxLift: 120 },
    spec: [['g', 0, 40, 0.5, 'Ballast', 'The Albatross\'s own gravity (u/s squared): nothing else in the HyperSea falls. Heavy on purpose: at 10 a dive for 60 u/s took 16 s and 1,100 u of height; at 25 it is a 3 s plunge the pilot can pay back'], ['liftK', 0.0005, 0.03, 0.0005, 'Wing lift', 'Lift per speed squared at neutral wings and a quarter angle of attack (level at about 125 u/s; it was 60, a third of the racers, and the AI could never pay a crystal dive back)'], ['dragK', 0.00002, 0.003, 0.00002, 'Drag', 'Drag per speed squared at neutral wings: a glide ratio of about 8 at 125 u/s (at 0.0004 it was 1.6, a brick, and a dive bought no speed)'], ['spreadLift', 1, 3, 0.1, 'Spread lift', 'Lift multiplier with wings spread (LT)'], ['spreadDrag', 1, 4, 0.1, 'Spread drag', 'Drag multiplier with wings spread'],
      ['foldLift', 0.05, 1, 0.05, 'Fold lift', 'Lift multiplier with wings folded (RT)'], ['foldDrag', 0.1, 1, 0.05, 'Fold drag', 'Drag multiplier folded'], ['maxLift', 15, 120, 5, 'Wing limit', 'The most lift the wings can carry (u/s squared): past it a dive is a dive'], ['thermal', 0, 60, 1, 'Updraft', 'Upward acceleration in the column above mass (u/s squared, at neutral wings): below the ballast at neutral, above it only with the wings spread, so soaring is a choice (at 22 the whole ribbon was an elevator and no dive could get down through it)'], ['thermalR', 20, 200, 5, 'Updraft radius', 'How far from a prism its updraft reaches sideways (u)'], ['thermalH', 50, 800, 10, 'Updraft height', 'How high above mass the updraft reaches (u)'], ['topSpeed', 100, 500, 10, 'Top speed', 'Speed cap (u/s)'], ['stall', 10, 60, 1, 'Stall speed', 'Below it the nose barely answers (u/s)']],
    init(P) { P.K = { spread: 0, fold: 0, up: 0, active: false, thermals: 0, wasUp: false, column: null }; P.vel.set(0, 0, 0).addScaledVector(P.course, 130); P.speed = 130; },
    step(W, P, inp, dt, ctx) {
      const m = P.m, Kk = P.K; Kk.spread = W.mech ? inp.lt : 0; Kk.fold = W.mech ? inp.rt : 0;   // the control: neutral wings, no updraft
      const auth = clamp(P.speed / (m.stall * 1.6), 0.25, 1);
      ctx.steer(P, inp, dt, auth, { pitchYaw: 95, roll: 140 });
      const { fwd, up } = ctx.axes(P), v = P.vel.length();
      const liftMul = lerp(lerp(1, m.spreadLift, Kk.spread), m.foldLift, Kk.fold), dragMul = lerp(lerp(1, m.spreadDrag, Kk.spread), m.foldDrag, Kk.fold);
      const acc = new ctx.V(0, -m.g, 0);
      // lift follows the angle of attack, as a wing's does: a nose above the flight path pulls, a nose below it pushes down, so the
      // pilot's pitch is the lift control (an always-on lift along hull-up looped the hull at any speed). Capped at maxLift.
      // It acts PERPENDICULAR to the flight path (hull-up with its along-track part removed), as a wing's does, so lift never
      // does work: only gravity and drag change the speed, and a dive buys exactly the speed a climb spends. Lift along hull-up
      // itself put ~90 u/s^2 against the velocity in a hard pull-up and bled 127 u/s to 54 in two seconds.
      const velDir = v > 1e-3 ? P.vel.clone().divideScalar(v) : fwd.clone(), aoaF = clamp(0.25 + 3 * -velDir.dot(up), -0.5, 2.5);
      const liftDir = up.clone().addScaledVector(velDir, -up.dot(velDir)); if (liftDir.lengthSq() > 1e-6) liftDir.normalize(); else liftDir.copy(up);
      acc.addScaledVector(liftDir, clamp(m.liftK * v * v * liftMul * aoaF, -m.maxLift, m.maxLift));
      if (v > 1e-3) acc.addScaledVector(P.vel, -m.dragK * v * dragMul);
      // the updraft: a column above every prism within thermalR sideways, up to thermalH above it
      let col = null; for (const p of ctx.near(W, new ctx.V(P.pos.x, P.pos.y - m.thermalH / 2 + 30, P.pos.z), Math.max(m.thermalR, m.thermalH / 2 + 30) + 20)) { if (!isMass(p)) continue; const dy = P.pos.y - p.pos.y; if (dy < -60 || dy > m.thermalH) continue; if (Math.hypot(P.pos.x - p.pos.x, P.pos.z - p.pos.z) > m.thermalR) continue; col = p; break; }
      if (!W.mech) col = null;
      Kk.up = col ? m.thermal * liftMul : 0; Kk.column = col; if (col) acc.y += Kk.up;
      if (col && !Kk.wasUp) { Kk.thermals++; P.stats.uses++; ctx.event(W, P, 'Updraft'); } Kk.wasUp = !!col;
      P.vel.addScaledVector(acc, dt);
      const sp = P.vel.length(); if (sp > 1e-3) { const dir = P.vel.clone().divideScalar(sp); dir.lerp(fwd, 1 - Math.exp(-m.grip * dt * auth)).normalize(); P.vel.copy(dir).multiplyScalar(Math.min(m.topSpeed, sp)); }
      ctx.moveVel(P, dt);
      Kk.active = !!col || Kk.fold > 0.5;
    },
    aiAim(W, P, aim, why, ctx) { return why === 'line' ? { aim: aim.clone().add(new ctx.V(0, 120, 0)), why } : null; },   // fly HIGH in the ribbon's updraft (it reaches 350 u up): a dive needs room below it, and the hull stays clear of the mass
    // a glider brushing mass skips off it, never reset: the velocity into the prism is reflected (taking it away instead left
    // the hull lying on the ribbon under its own ballast at 20 u/s)
    onContact(W, P, p, ctx) { const n = P.pos.clone().sub(p.pos).normalize(), vn = P.vel.dot(n); if (vn < 0) P.vel.addScaledVector(n, -2 * vn * 0.9); P.pos.addScaledVector(n, 2); return true; },
    // slow and OUT of an updraft: nose down and dive for speed (harder when it has fallen below the line). Slow IN an updraft it
    // spreads and climbs instead (below), so it never dives into the ribbon it is riding.
    // the energy cycle a soaring pilot flies: below 90 u/s it DIVES (fold, nose down) until it has 150, then it flies the line nose-up,
    // trading the speed back for height, spreading in a column to take the free lift. Hysteresis, so it never dithers at the edge.
    // A dive is an ATTITUDE, the nose held about 40 degrees below the horizon by the aim controller, never a held stick: a pitch
    // push added to the base aim (120 u above the line) never beat a full stick-up, and a held stick is a pitch RATE, which looped
    // the hull 400 degrees in eight seconds under the line, bleeding 90 u/s to 56 and never reaching the 150 that ends the dive.
    aiSticks(W, P, aim, base, ctx) {
      const Kk = P.K; if (P.speed < 90) Kk.diving = true; else if (P.speed > 150 || P.pos.y < aim.y - 200) Kk.diving = false; if (!Kk.diving) return null;   // a dive ends with the speed, or 200 u under the line
      const { fwd } = ctx.axes(P), flat = new ctx.V(fwd.x, 0, fwd.z); if (flat.lengthSq() < 1e-4) flat.set(P.course.x, 0, P.course.z); flat.normalize();
      const d = ctx.aimAt(P, P.pos.clone().addScaledVector(flat, 120).add(new ctx.V(0, -100, 0))); return { L: d.L, R: d.R };
    },
    ai(W, P, aim) {
      const Kk = P.K, above = P.pos.y - aim.y, out = {};
      if (Kk.diving) { out.rt = 1; return out; }
      if (above > 60 && P.speed < 130) { out.rt = 1; return out; }  // high and slowing: dive back to the line
      if (Kk.column && P.speed > 110) out.lt = 1;                  // in lift with speed to spare: spread and climb
      return out;
    },
    hud(P) { const m = P.m, Kk = P.K; return [['Updraft', Kk.column ? '+' + Kk.up.toFixed(0) + ' u/s\u00b2' : 'none here', Kk.up / Math.max(1, m.thermal * m.spreadLift), Kk.column ? '#9dff5a' : ''], ['Wings', Kk.fold > 0.5 ? 'folded (dive)' : Kk.spread > 0.5 ? 'spread (lift)' : 'glide', Kk.fold > 0.5 ? Kk.fold : Kk.spread]]; },
    abilityStates(P) { const m = P.m; return { charge: '+' + m.thermal.toFixed(0) + ' updraft', mass: 'g ' + m.g.toFixed(1), space: 'lift ' + (m.liftK * 1000).toFixed(2), time: 'drag ' + (m.dragK * 1000).toFixed(2) }; },
    hull(ctx, color) { const g = new ctx.THREE.Group(), m = mat(ctx, color); g.add(box(ctx, m, 1.6, 1.4, 8)); const wl = box(ctx, m, 14, 0.3, 2.4, -7.5, 0, 0.5), wr = box(ctx, m, 14, 0.3, 2.4, 7.5, 0, 0.5); wl.name = 'wl'; wr.name = 'wr'; g.add(wl); g.add(wr); g.add(box(ctx, m, 4, 0.3, 1.5, 0, 0.3, 3.8)); return g; },
    poseHull(h, P) { const s = lerp(lerp(1, 1.3, P.K.spread), 0.45, P.K.fold); for (const n of ['wl', 'wr']) { const w = h.getObjectByName(n); if (w) { w.scale.x = s; w.position.x = Math.sign(w.position.x) * 7.5 * s; } } },
    mount(ctx, scene, P) { const c = new ctx.THREE.Mesh(new ctx.THREE.CylinderGeometry(1, 1, 1, 16, 1, true), new ctx.THREE.MeshBasicMaterial({ color: 0x9dff5a, transparent: true, opacity: 0.08, side: ctx.THREE.DoubleSide, depthWrite: false })); scene.add(c); return { c, dispose() { scene.remove(c); } }; },
    draw(W, P, o, t) { const m = P.m, col = P.K.column; o.c.visible = !!col; if (col) { o.c.position.set(col.pos.x, col.pos.y + m.thermalH / 2, col.pos.z); o.c.scale.set(m.thermalR, m.thermalH, m.thermalR); o.c.material.opacity = 0.06 + 0.03 * Math.sin(t * 2); } },
    preview(g, w, h, t, accent) { bg(g, w, h); blocks(g, '#3a4380', [[w * 0.3, h * 0.85], [w * 0.34, h * 0.86], [w * 0.75, h * 0.86], [w * 0.79, h * 0.84]], 9); g.strokeStyle = '#9dff5a33'; g.lineWidth = 8; for (const x of [w * 0.32, w * 0.77]) { g.beginPath(); g.moveTo(x, h * 0.8); g.lineTo(x, h * 0.15); g.stroke(); } const u = (t * 0.25) % 1, x = u * w, y = h * 0.45 + Math.sin(u * Math.PI * 2 * 2) * h * 0.25, a = Math.atan2(Math.cos(u * Math.PI * 4) * h * 0.25 * 4 * Math.PI / w, 1) * 0.5; g.save(); g.translate(x, y); g.rotate(a); g.fillStyle = accent; g.fillRect(-18, -1.5, 36, 3); g.fillRect(-4, -2.5, 10, 5); g.restore(); },
  };

  // ================= slipstream: the Remora =================
  K.slipstream = {
    mechanicName: 'drafting', aiRule: 'It heads for the wake of the nearest rival ahead (within 300 u) unless its crystal is within 150 u, latches (LT) when within reach behind it, and slings when the crystal comes within 180 u, after 4 s of tow, or when the rival is slower than its own cruise.',
    defaults: { aloneCap: 55, draftCap: 1.3, draftR: 14, railDraft: 1.15, latchReach: 60, towGap: 16, slingMult: 1.7, slingBonus: 40, slingTime: 2.5, topSpeed: 240, petalRate: 0 },
    spec: [['aloneCap', 20, 120, 1, 'Alone cap', 'Top speed with nothing to draft (u/s)'], ['draftCap', 1, 1.8, 0.05, 'Draft cap', 'In a rival\'s wake your cap is their speed times this'], ['draftR', 5, 40, 1, 'Wake width', 'How far from a rival\'s path its wake still drafts you (u)'], ['railDraft', 1, 1.6, 0.05, 'Rail draft', 'Cap multiplier beside a rival\'s laid rail'],
      ['latchReach', 20, 150, 5, 'Latch reach', 'How close behind a rival LT can latch (u)'], ['towGap', 8, 40, 1, 'Tow gap', 'Where the latched hull rides behind the rival (u)'], ['slingMult', 1, 2.5, 0.05, 'Sling', 'Release speed as a multiple of the rival\'s'], ['slingBonus', 0, 120, 5, 'Draft bonus', 'Extra sling speed from a full draft meter (u/s)'], ['slingTime', 0.5, 6, 0.1, 'Sling fade', 'Seconds a sling takes to fade'], ['topSpeed', 100, 400, 10, 'Top speed', 'Speed cap (u/s)'], ['petalRate', 0, 2, 0.1, 'Leech', 'Petals taken per second while latched (PvP is petals only)']],
    init(P) { P.K = { drafting: null, latched: null, latchT: 0, draft: 0, slingUntil: -9, slingSpeed: 0, active: false, slings: 0 }; },
    wakeOf(P, R, m) { let best = null, bd = m.draftR * m.draftR; for (const s of R.wake) { const d = s.pos.distanceToSquared(P.pos); if (d < bd) { bd = d; best = s; } } return best; },
    step(W, P, inp, dt, ctx) {
      const m = P.m, Kk = P.K, { fwd } = ctx.axes(P);
      if (Kk.latched && (Kk.latched.finishT != null || Kk.latched.pos.distanceTo(P.pos) > m.latchReach * 2.5)) Kk.latched = null;
      if (Kk.latched) {
        const R = Kk.latched; if (inp.lt <= 0.5 || inp.rt > 0.5) { K.slipstream.sling(W, P, R, ctx); }
        else {
          ctx.steer(P, inp, dt, 0.3);
          const want = R.pos.clone().addScaledVector(R.course, -m.towGap); P.pos.lerp(want, 1 - Math.exp(-8 * dt)); P.course.copy(R.course); P.speed = R.speed; P.vel.copy(P.course).multiplyScalar(P.speed);
          P.quat.slerp(R.quat, 1 - Math.exp(-6 * dt)); Kk.draft = Math.min(1, Kk.draft + dt / 2); Kk.latchT += dt;
          if (m.petalRate > 0) { P.petals += m.petalRate * dt; }
          Kk.active = true; return;
        }
      }
      ctx.steer(P, inp, dt, 1);
      let cap = m.aloneCap, drafting = null;
      for (const R of ctx.rivals(W, P)) { if (R.pos.distanceTo(P.pos) > 260) continue; const s = K.slipstream.wakeOf(P, R, m); if (s && R.pos.clone().sub(P.pos).dot(fwd) > -10) { const c = Math.max(m.aloneCap, R.speed * m.draftCap); if (c > cap) { cap = c; drafting = R; } } }
      if (!drafting) { const rail = ctx.nearest(W, P.pos, m.draftR, (p) => p.kind === 'rail' && p.owner !== P.i); if (rail) cap = m.aloneCap * m.railDraft; }
      if (!W.mech) { cap = m.aloneCap; drafting = null; }   // the control: no draft at all
      Kk.drafting = drafting; if (drafting) Kk.draft = Math.min(1, Kk.draft + dt / 4);
      if (W.t < Kk.slingUntil) cap = Math.max(cap, Kk.slingSpeed * (Kk.slingUntil - W.t) / m.slingTime + m.aloneCap * (1 - (Kk.slingUntil - W.t) / m.slingTime));
      const target = Math.min(m.topSpeed, cap * Math.max(0.5, P.throttle) * P.boost);
      ctx.move(P, dt, target, P.F.grip, P.speed > target ? 0.8 : P.F.accel);
      if (edge(P, 'ltWas', inp.lt > 0.5)) { let best = null, bd = m.latchReach; for (const R of ctx.rivals(W, P)) { const d = R.pos.clone().sub(P.pos), l = d.length(); if (l < bd && d.dot(fwd) > l * 0.3) { bd = l; best = R; } } if (best) { Kk.latched = best; Kk.latchT = 0; ctx.event(W, P, 'Latched onto ' + best.domain.name); P.stats.uses++; } }
      Kk.active = !!drafting || W.t < Kk.slingUntil;
    },
    sling(W, P, R, ctx) { const m = P.m, Kk = P.K; Kk.slingSpeed = Math.min(m.topSpeed, R.speed * m.slingMult + m.slingBonus * Kk.draft); Kk.slingUntil = W.t + m.slingTime; P.speed = Kk.slingSpeed; P.vel.copy(P.course).multiplyScalar(P.speed); Kk.slings++; Kk.latched = null; Kk.draft = 0; ctx.event(W, P, 'Sling! ' + Math.round(Kk.slingSpeed) + ' u/s'); },
    aiAim(W, P, aim, why, ctx) { if (why === 'turn back' || P.K.latched) return null; const m = P.m; if (P.crystal && P.crystal.pos.distanceTo(P.pos) < 150) return null; const { fwd } = ctx.axes(P); let best = null, bd = 300; for (const R of ctx.rivals(W, P)) { const d = R.pos.clone().sub(P.pos), l = d.length(); if (l < bd && d.dot(fwd) > 0) { bd = l; best = R; } } return best ? { aim: best.pos.clone().addScaledVector(best.course, -(m.towGap + 10)), why: 'draft' } : null; },
    ai(W, P, aim, base, ctx) { const m = P.m, Kk = P.K; if (Kk.latched) { const R = Kk.latched, near = P.crystal && P.crystal.pos.distanceTo(P.pos) < 180; return { lt: (near || Kk.latchT > 4 || R.speed < m.aloneCap) ? 0 : 1 }; } const { fwd } = ctx.axes(P); for (const R of ctx.rivals(W, P)) { const d = R.pos.clone().sub(P.pos), l = d.length(); if (l < m.latchReach * 0.8 && d.dot(fwd) > l * 0.5 && !(P.crystal && P.crystal.pos.distanceTo(P.pos) < 150)) return { lt: 1 }; } return {}; },
    hud(P) { const m = P.m, Kk = P.K, t = Math.max(0, Kk.slingUntil - (P.K._t || 0)); return [['Draft', Kk.latched ? 'towed by ' + Kk.latched.domain.name : Kk.drafting ? 'in ' + Kk.drafting.domain.name + '\'s wake' : 'alone (cap ' + Math.round(m.aloneCap) + ')', Kk.draft, Kk.latched || Kk.drafting ? '#9dff5a' : ''], ['Sling', Kk.latched ? 'release to sling' : Kk.slings + ' slings', Kk.latched ? 1 : 0]]; },
    draw(W, P, o, t, ctx) { P.K._t = W.t; const rs = ctx.rivals(W, P); for (let i = 0; i < o.lines.length; i++) { const R = rs[i], l = o.lines[i]; l.visible = !!R && R.wake.length > 1 && (P.i === W.watch); if (l.visible) { setLine(l, R.wake.map((s) => s.pos)); l.material.color.set(R.domain.color); } } },
    mount(ctx, scene, P) { const lines = [0, 1, 2].map(() => { const l = line(ctx, 0xffffff, 40); l.material.opacity = 0.35; scene.add(l); return l; }); return { lines, dispose() { for (const l of lines) scene.remove(l); } }; },
    abilityStates(P) { const m = P.m; return { charge: '\u00d7' + m.slingMult.toFixed(2) + ' sling', mass: m.petalRate ? m.petalRate.toFixed(1) + ' petals/s' : 'no leech', space: Math.round(m.latchReach) + ' u latch', time: '\u00d7' + m.draftCap.toFixed(2) + ' draft' }; },
    hull(ctx, color) { const g = new ctx.THREE.Group(), m = mat(ctx, color); g.add(box(ctx, m, 2.4, 1.2, 8)); const disc = new ctx.THREE.Mesh(new ctx.THREE.CylinderGeometry(1.6, 1.6, 0.4, 12), m); disc.position.set(0, 0.8, -1.5); g.add(disc); g.add(box(ctx, m, 0.4, 2.2, 1.6, 0, -0.5, 3.6)); return g; },
    preview(g, w, h, t, accent, DOM) { bg(g, w, h); const rx = ((t * 120) % (w + 120)) - 60, ry = h * 0.45; g.strokeStyle = DOM('ruby') + '66'; g.lineWidth = 12; g.beginPath(); g.moveTo(rx - 300, ry); g.lineTo(rx - 14, ry); g.stroke(); dart(g, DOM('ruby'), rx, ry, 0, 1.2); const ph = Math.sin(t * 1.3); const fx = rx - 40 + (ph > 0.6 ? (ph - 0.6) * 220 : 0), fy = ry + (ph > 0.6 ? -(ph - 0.6) * 60 : 0); dart(g, accent, fx, fy, 0, 0.8); if (ph > 0.6) { for (let i = 1; i < 5; i++) { g.globalAlpha = 0.4 - i * 0.08; dart(g, accent, fx - i * 14, fy + i * 3, 0, 0.7); } g.globalAlpha = 1; } },
  };

  // ================= hover: the Dragonfly =================
  K.hover = {
    mechanicName: 'dashing', keyLayout: 'omni', aiRule: 'It turns to face its crystal and thrusts straight at it, strafing out any sideways drift; it dashes when the crystal is more than 150 u away with two or more pips, and brakes when it would overshoot.',
    defaults: { accel: 180, maxSpeed: 75, drag: 2.4, brakeDrag: 8, dashSpeed: 190, dashTime: 0.25, dashPips: 3, dashRegen: 1.2, turn: 150, climb: 90 },
    spec: [['accel', 30, 250, 5, 'Thrust', 'Acceleration from the left stick (u/s squared)'], ['maxSpeed', 30, 160, 5, 'Hover speed', 'Top speed without a dash (u/s)'], ['drag', 0.5, 6, 0.1, 'Drag', 'How fast it stops when the stick is released (per second)'], ['brakeDrag', 2, 20, 0.5, 'Brake', 'Drag with LT held'], ['dashSpeed', 80, 400, 10, 'Dash speed', 'Speed of a dash (u/s)'], ['dashTime', 0.1, 0.8, 0.05, 'Dash time', 'Seconds a dash lasts (hull contacts are ignored meanwhile)'],
      ['dashPips', 1, 5, 1, 'Dash pips', 'How many dashes are banked'], ['dashRegen', 0.3, 4, 0.1, 'Pip regen', 'Seconds to regain one pip'], ['turn', 40, 300, 5, 'Turn rate', 'How fast the right stick turns the nose (deg/s)'], ['climb', 0, 150, 5, 'Climb thrust', 'Acceleration from A (up) / B (down) (u/s squared)']],
    init(P) { P.K = { pips: 3, regenT: 0, dashUntil: -9, active: false, dashes: 0 }; P.vel.set(0, 0, 0); P.speed = 0; },
    step(W, P, inp, dt, ctx) {
      const m = P.m, Kk = P.K; Kk.pips = Math.min(Kk.pips, m.dashPips);
      // the right stick turns the nose; roll levels itself
      const dq = new ctx.Q(); dq.setFromAxisAngle(new ctx.V(0, 1, 0), -inp.R.x * m.turn * ctx.DEG * dt); P.quat.multiply(dq); dq.setFromAxisAngle(new ctx.V(1, 0, 0), inp.R.y * m.turn * 0.7 * ctx.DEG * dt); P.quat.multiply(dq);
      { const ax = ctx.axes(P), roll = Math.atan2(ax.right.y, ax.up.y); dq.setFromAxisAngle(new ctx.V(0, 0, 1), -roll * (1 - Math.exp(-3 * dt))); P.quat.multiply(dq); P.quat.normalize(); }
      const { fwd, right, up } = ctx.axes(P);
      const dashing = W.t < Kk.dashUntil;
      if (!dashing) {
        const acc = new ctx.V().addScaledVector(right, inp.L.x * m.accel).addScaledVector(fwd, inp.L.y * m.accel).addScaledVector(up, ((inp.a ? 1 : 0) - (inp.b ? 1 : 0)) * m.climb);
        P.vel.addScaledVector(acc, dt); P.vel.multiplyScalar(Math.exp(-(inp.lt > 0.3 ? m.brakeDrag : m.drag) * dt));
        const sp = P.vel.length(); if (sp > m.maxSpeed * P.boost) P.vel.multiplyScalar((m.maxSpeed * P.boost) / sp);
        if (Kk.pips >= 1 && edge(P, 'rtWas', inp.rt > 0.5)) { const d = new ctx.V().addScaledVector(right, inp.L.x).addScaledVector(fwd, inp.L.y); if (d.lengthSq() < 0.04) d.copy(fwd); d.normalize(); P.vel.copy(d).multiplyScalar(m.dashSpeed); Kk.dashUntil = W.t + m.dashTime; Kk.pips--; Kk.dashes++; P.stats.uses++; ctx.event(W, P, 'Dash'); }
      }
      if (Kk.pips < m.dashPips) { Kk.regenT += dt; if (Kk.regenT >= m.dashRegen) { Kk.regenT = 0; Kk.pips++; } }
      ctx.moveVel(P, dt); if (P.speed < 1) P.course.copy(fwd);
      Kk.active = dashing;
    },
    onContact(W, P) { return W.t < P.K.dashUntil; },
    aiSticks(W, P, aim, base, ctx) {
      const m = P.m, { fwd, right } = ctx.axes(P), to = aim.clone().sub(P.pos), dist = to.length(); to.divideScalar(Math.max(1e-3, dist));
      const desired = to.clone().multiplyScalar(Math.min(m.maxSpeed, 20 + dist * 1.5)), dv = desired.clone().sub(P.vel);
      const R = { x: clamp(base.yawErr * 2, -1, 1), y: clamp(base.pitchErr * 2, -1, 1) };
      const L = { x: clamp(dv.dot(right) / 30, -1, 1), y: clamp(dv.dot(fwd) / 30, -1, 1) };
      P.K._dvUp = dv.y; return { L, R };
    },
    ai(W, P, aim, base) { const m = P.m, Kk = P.K, dist = aim.distanceTo(P.pos); const out = {}; if (Kk._dvUp > 12) out.a = true; else if (Kk._dvUp < -12) out.b = true; if (Kk.pips >= 2 && base.err < 12 && P.speed < m.maxSpeed * 1.1 && P.aimWhy === 'line') out.rt = 1; if (P.aimWhy !== 'line' && P.speed > dist * 1.5 + 10) out.lt = 1; return out; },
    hud(P) { const m = P.m, Kk = P.K; return [['Dash', Kk.pips + ' / ' + m.dashPips + (Kk.pips < m.dashPips ? ' \u00b7 ' + (m.dashRegen - Kk.regenT).toFixed(1) + ' s' : ''), Kk.pips / m.dashPips, Kk.pips >= 1 ? '#9dff5a' : ''], ['Hover', Math.round(P.speed) + ' / ' + Math.round(m.maxSpeed) + ' u/s', clamp(P.speed / m.maxSpeed, 0, 1)]]; },
    abilityStates(P) { const m = P.m; return { charge: Math.round(m.accel) + ' thrust', mass: 'brake ' + m.brakeDrag.toFixed(1), space: Math.round(m.dashSpeed * m.dashTime) + ' u dash', time: m.dashRegen.toFixed(1) + ' s regen' }; },
    hull(ctx, color) { const g = new ctx.THREE.Group(), m = mat(ctx, color); g.add(box(ctx, m, 1.2, 1.2, 9)); for (const [x, z, n] of [[-5, -1, 'w0'], [5, -1, 'w1'], [-4.5, 1.6, 'w2'], [4.5, 1.6, 'w3']]) { const w = box(ctx, m, 8, 0.15, 1.6, x, 0.4, z); w.name = n; g.add(w); } return g; },
    poseHull(h, P, t) { for (let i = 0; i < 4; i++) { const w = h.getObjectByName('w' + i); if (w) w.rotation.z = Math.sin(t * 40 + i) * 0.35 * (i < 2 ? 1 : -1); } },
    preview(g, w, h, t, accent) { bg(g, w, h); blocks(g, '#3a4380', [[w * 0.3, h * 0.3], [w * 0.3, h * 0.7], [w * 0.65, h * 0.5], [w * 0.9, h * 0.25]], 10); const u = (t * 0.7) % 3; let x = w * 0.15, y = h * 0.5; if (u < 1) { x = w * 0.15 + u * w * 0.25; y = h * 0.5; } else if (u < 1.5) { x = w * 0.4; y = h * 0.5 - (u - 1) * 2 * h * 0.3; } else { x = w * 0.4 + (u - 1.5) * w * 0.4; y = h * 0.2; if (u > 1.6 && u < 1.85) { g.strokeStyle = accent + '88'; g.lineWidth = 3; g.beginPath(); g.moveTo(x - 60, y); g.lineTo(x, y); g.stroke(); } } g.fillStyle = accent; g.fillRect(x - 10, y - 2, 20, 4); g.fillStyle = accent + 'aa'; g.fillRect(x - 6, y - 12 + Math.sin(t * 30) * 2, 12, 2); g.fillRect(x - 6, y + 10 - Math.sin(t * 30) * 2, 12, 2); },
  };

  // ================= burrow: the Mole =================
  K.burrow = {
    mechanicName: 'burrowing', aiRule: 'In the open it aims at the nearest mass within 300 u that lies within 50 deg of its crystal, and digs (LT) when no mass is within 80 u ahead, leaving a tunnel for the next lap; inside mass it follows the line, and presses RT as the mass runs out.',
    defaults: { openCruise: 40, openTurn: 90, inRadius: 16, inCap: 150, inTurn: 220, inRamp: 1.5, exitDecay: 2.5, breachWindow: 0.25, breachKick: 1.4, digMult: 1.6, tunnelEvery: 6, tunnelScale: 2.4, topSpeed: 220, grace: 1.5 },
    spec: [['openCruise', 15, 80, 1, 'Open cruise', 'Speed in open space (u/s)'], ['openTurn', 30, 180, 5, 'Open turn', 'Turn rate in open space (deg/s)'], ['inRadius', 6, 40, 1, 'In-mass reach', 'Within this distance of a prism you are inside mass (u)'], ['inCap', 60, 300, 5, 'Mass speed', 'Top speed inside mass (u/s)'], ['inTurn', 60, 400, 10, 'Mass turn', 'Turn rate inside mass (deg/s)'], ['inRamp', 0.3, 4, 0.1, 'Spool up', 'Rate speed climbs inside mass (per second)'],
      ['exitDecay', 0.5, 6, 0.1, 'Momentum keep', 'Seconds the mass speed fades after leaving it'], ['breachWindow', 0.05, 0.6, 0.05, 'Breach window', 'RT within this many seconds of leaving mass pays the kick'], ['breachKick', 1, 1.8, 0.05, 'Breach kick', 'Speed multiplier for a timed breach'], ['digMult', 1, 3, 0.1, 'Dig speed', 'Open cruise multiplier while digging (LT)'], ['tunnelEvery', 3, 15, 1, 'Tunnel spacing', 'A tunnel prism every this many units (u)'], ['tunnelScale', 1, 5, 0.2, 'Tunnel prism', 'Size of a tunnel prism (u)'], ['topSpeed', 100, 400, 10, 'Top speed', 'Speed cap (u/s)']],
    init(P) { P.K = { inMass: false, exitT: -9, exitSpeed: 0, digAcc: 0, active: false, breaches: 0, lastDig: null }; },
    inMass(W, P, ctx, pos) { const m = P.m; for (const p of ctx.near(W, pos || P.pos, m.inRadius + 4)) { if (!isMass(p)) continue; if (p.owner === P.i && W.t - (p.born || 0) < m.grace) continue; if (p.pos.distanceTo(pos || P.pos) <= m.inRadius) return true; } return false; },
    step(W, P, inp, dt, ctx) {
      const m = P.m, Kk = P.K, was = Kk.inMass; Kk.inMass = W.mech && K.burrow.inMass(W, P, ctx);   // the control: mass is never a road
      ctx.steer(P, inp, dt, 1, { pitchYaw: Kk.inMass ? m.inTurn : m.openTurn, roll: 130 });
      const dig = inp.lt > 0.5 && !Kk.inMass;
      let target, rate = P.F.accel;
      if (Kk.inMass) { target = Math.min(m.topSpeed, m.inCap * Math.max(0.5, P.throttle) * P.boost); rate = m.inRamp; if (!was) { P.stats.uses++; ctx.event(W, P, 'Into mass'); } }
      else {
        if (was) { Kk.exitT = W.t; Kk.exitSpeed = P.speed; }
        const since = W.t - Kk.exitT, keep = since < m.exitDecay ? lerp(Kk.exitSpeed, m.openCruise, since / m.exitDecay) : 0;
        target = Math.max(keep, m.openCruise * (dig ? m.digMult : 1) * Math.max(0.5, P.throttle) * P.boost); rate = since < m.exitDecay ? 3 : P.F.accel;
        if (edge(P, 'rtWas', inp.rt > 0.5) && since <= m.breachWindow) { P.speed = Math.min(m.topSpeed, P.speed * m.breachKick); Kk.exitSpeed = P.speed; Kk.breaches++; ctx.event(W, P, 'Breach! \u00d7' + m.breachKick.toFixed(2)); }
      }
      if (Kk.inMass) P.K.rtWas = inp.rt > 0.5;
      ctx.move(P, dt, target, P.F.grip, rate);
      if (dig) { Kk.digAcc += P.speed * dt; while (Kk.digAcc >= m.tunnelEvery) { Kk.digAcc -= m.tunnelEvery; ctx.layPrism(W, P, P.pos, P.quat, [m.tunnelScale, m.tunnelScale, m.tunnelEvery * 1.1], { kind: 'tunnel' }); } }
      Kk.active = Kk.inMass;
    },
    onContact() { return true; },   // the Mole passes through mass: contact is its road, never a reset
    aiAim(W, P, aim, why, ctx) { if (P.K.inMass || why === 'turn back') return null; const dir = aim.clone().sub(P.pos).normalize(); let best = null, bs = 0; for (const p of ctx.near(W, P.pos, 300)) { if (!isMass(p) || (p.owner === P.i && W.t - (p.born || 0) < 3)) continue; const d = p.pos.clone().sub(P.pos), l = d.length(); if (l < 20) continue; const c = d.divideScalar(l).dot(dir); if (c > Math.cos(50 * ctx.DEG)) { const s = c - l / 600; if (s > bs) { bs = s; best = p; } } } return best ? { aim: best.pos.clone(), why: 'to mass' } : null; },
    ai(W, P, aim, base, ctx) { const m = P.m, Kk = P.K, out = {}; if (!Kk.inMass) { const ahead = P.pos.clone().addScaledVector(P.course, 50); if (!ctx.nearest(W, ahead, 80, (p) => isMass(p) && p.owner !== P.i)) out.lt = 1; } else { if (!K.burrow.inMass(W, P, ctx, P.pos.clone().addScaledVector(P.course, Math.max(20, P.speed * 0.2)))) out.rt = 1; } return out; },
    hud(P) { const m = P.m, Kk = P.K; return [['Mass', Kk.inMass ? 'inside \u00b7 cap ' + Math.round(m.inCap) : 'open \u00b7 cap ' + Math.round(m.openCruise), clamp(P.speed / m.inCap, 0, 1), Kk.inMass ? '#9dff5a' : ''], ['Breach', Kk.breaches + ' timed', 0]]; },
    abilityStates(P) { const m = P.m; return { charge: '\u00d7' + m.breachKick.toFixed(2) + ' breach', mass: m.tunnelScale.toFixed(1) + ' u tunnel', space: Math.round(m.inRadius) + ' u reach', time: Math.round(m.inCap) + ' u/s in mass' }; },
    hull(ctx, color) { const g = new ctx.THREE.Group(), m = mat(ctx, color); const b = new ctx.THREE.Mesh(new ctx.THREE.ConeGeometry(2.6, 8, 8), m); b.rotation.x = -Math.PI / 2; g.add(b); g.add(box(ctx, m, 1.2, 0.6, 3, -2.4, -1, -1)); g.add(box(ctx, m, 1.2, 0.6, 3, 2.4, -1, -1)); return g; },
    mount(ctx, scene, P) { const s = new ctx.THREE.Mesh(new ctx.THREE.SphereGeometry(1, 14, 10), new ctx.THREE.MeshBasicMaterial({ color: P.domain.color, transparent: true, opacity: 0.12, depthWrite: false })); scene.add(s); return { s, dispose() { scene.remove(s); } }; },
    draw(W, P, o, t) { o.s.visible = P.K.inMass; o.s.position.copy(P.pos); o.s.scale.setScalar(P.m.inRadius * (1 + 0.05 * Math.sin(t * 8))); },
    preview(g, w, h, t, accent) { bg(g, w, h); const band = []; for (let i = 0; i < 26; i++) band.push([w * 0.25 + i * 14, h * 0.5 + Math.sin(i * 0.6) * 14 + ((i % 2) ? 9 : -9)]); blocks(g, '#3a4380', band, 11); const u = (t * 0.4) % 1; let x, s; if (u < 0.3) { x = u / 0.3 * w * 0.25; s = 0.7; } else { x = w * 0.25 + (u - 0.3) / 0.7 * w * 0.72; s = 1.2; } const y = x > w * 0.25 ? h * 0.5 + Math.sin((x - w * 0.25) / 14 * 0.6) * 14 : h * 0.5; if (s > 1) { g.fillStyle = accent + '33'; g.beginPath(); g.arc(x, y, 22, 0, Math.PI * 2); g.fill(); } dart(g, accent, x, y, 0, s); },
  };

  // ================= momentum: the Marlin =================
  K.momentum = {
    mechanicName: 'burning', aiRule: 'It wants a velocity toward its crystal (up to 80% of top speed, less as it closes), points the nose along the difference between that and its velocity, thrusts (RT) when the nose is within 25 deg of it, flips (B) when the correction is behind it, and brakes (LT) when it would overshoot.',
    defaults: { thrust: 70, retro: 45, turn: 180, topSpeed: 220, assist: 1.2, flipTime: 0.3, drag: 0.1, kick: 0 },
    spec: [['thrust', 20, 200, 5, 'Main thrust', 'Acceleration along the nose with RT (u/s squared)'], ['retro', 0, 150, 5, 'Retro thrust', 'Braking acceleration against the velocity with LT (u/s squared)'], ['turn', 60, 360, 10, 'Turn rate', 'How fast the nose turns, free of the velocity (deg/s)'], ['topSpeed', 80, 400, 10, 'Top speed', 'Speed cap (u/s)'],
      ['assist', 0, 4, 0.1, 'Drift assist', 'Lateral damping that bends the velocity toward the nose (per second); 0 = pure Newtonian'], ['flipTime', 0.1, 1, 0.05, 'Flip time', 'Seconds B takes to turn the nose 180 deg'], ['drag', 0, 1, 0.02, 'Space drag', 'A small drag, so it is not forever (per second)']],
    init(P) { P.K = { flipUntil: -9, flipTo: null, active: false, burns: 0, slip: 0 }; P.vel.copy(P.course).multiplyScalar(40); P.speed = 40; },
    step(W, P, inp, dt, ctx) {
      const m = P.m, Kk = P.K;
      if (W.t < Kk.flipUntil && Kk.flipTo) P.quat.slerp(Kk.flipTo, 1 - Math.exp(-(6 / m.flipTime) * dt));
      else ctx.steer(P, inp, dt, 1, { pitchYaw: m.turn, roll: 140 });
      if (edge(P, 'bWas', inp.b)) { const { up } = ctx.axes(P); Kk.flipTo = P.quat.clone().premultiply(new ctx.Q().setFromAxisAngle(up, Math.PI)); Kk.flipUntil = W.t + m.flipTime; ctx.event(W, P, 'Flip'); }
      const { fwd } = ctx.axes(P), acc = new ctx.V();
      if (inp.rt > 0.05) { acc.addScaledVector(fwd, m.thrust * inp.rt); if (edge(P, 'rtOn', true)) { Kk.burns++; P.stats.uses++; } } else P.K.rtOn = false;
      const sp = P.vel.length();
      if (inp.lt > 0.05 && sp > 1) acc.addScaledVector(P.vel.clone().divideScalar(sp), -Math.min(m.retro * inp.lt, sp / dt));
      const vLat = P.vel.clone().addScaledVector(fwd, -P.vel.dot(fwd)); acc.addScaledVector(vLat, -m.assist);
      acc.addScaledVector(P.vel, -m.drag);
      P.vel.addScaledVector(acc, dt); const s2 = P.vel.length(); if (s2 > m.topSpeed * P.boost) P.vel.multiplyScalar((m.topSpeed * P.boost) / s2);
      ctx.moveVel(P, dt); if (P.speed < 1) P.course.copy(fwd);
      Kk.slip = Math.acos(clamp(P.course.dot(fwd), -1, 1)) / ctx.DEG;
      Kk.active = inp.rt > 0.3 || W.t < Kk.flipUntil;
    },
    aiSticks(W, P, aim, base, ctx) {
      const m = P.m, to = aim.clone().sub(P.pos), dist = to.length(); to.divideScalar(Math.max(1e-3, dist));
      const want = m.topSpeed * 0.85 * clamp(1 - base.err / 110, 0.3, 1);   // fast when lined up, slower through a turn
      const desired = to.clone().multiplyScalar(Math.min(want, 30 + dist * 1.2)), a = desired.clone().sub(P.vel); P.K._a = a; P.K._dist = dist;
      if (a.length() < 6) return null;
      const point = P.pos.clone().addScaledVector(a.clone().normalize(), 100), s = ctx.aimAt(P, point); P.K._aErr = s.err; return { L: s.L, R: s.R };
    },
    ai(W, P, aim, base, ctx) { const m = P.m, Kk = P.K, a = Kk._a || new ctx.V(), out = {}; const { fwd } = ctx.axes(P); const al = a.length(); if (al > 6 && (Kk._aErr || 0) < 35) out.rt = clamp(al / m.thrust * 1.2, 0.3, 1); if (al > 30 && a.clone().normalize().dot(fwd) < -0.5 && W.t > Kk.flipUntil + 0.5) out.b = true; const closing = P.vel.dot(aim.clone().sub(P.pos).normalize()); if (P.aimWhy !== 'line' && closing > (Kk._dist || 0) * 1.5 + 20) out.lt = 1; return out; },
    hud(P) { const m = P.m, Kk = P.K; return [['Thrust', P.lastInp ? Math.round((P.lastInp.rt || 0) * 100) + '%' : '0%', P.lastInp ? P.lastInp.rt || 0 : 0], ['Drift', Math.round(Kk.slip) + '\u00b0 slip', clamp(Kk.slip / 90, 0, 1), Kk.slip > 30 ? '#c48bff' : '']]; },
    abilityStates(P) { const m = P.m; return { charge: Math.round(m.thrust) + ' thrust', mass: Math.round(m.topSpeed) + ' u/s cap', space: Math.round(m.retro) + ' retro', time: Math.round(m.turn) + '\u00b0/s turn' }; },
    hull(ctx, color) { const g = new ctx.THREE.Group(), m = mat(ctx, color); g.add(box(ctx, m, 1.8, 1.8, 7)); const spear = new ctx.THREE.Mesh(new ctx.THREE.ConeGeometry(0.5, 6, 6), m); spear.rotation.x = -Math.PI / 2; spear.position.z = -6; g.add(spear); g.add(box(ctx, m, 0.3, 3.2, 2.2, 0, 1.2, 2.6)); g.add(box(ctx, m, 5, 0.3, 1.6, 0, 0, 1)); return g; },
    mount(ctx, scene, P) { const l = line(ctx, P.domain.color, 2); l.material.opacity = 0.6; scene.add(l); return { l, dispose() { scene.remove(l); } }; },
    draw(W, P, o) { o.l.visible = P.speed > 5; setLine(o.l, [P.pos, P.pos.clone().addScaledVector(P.vel, 0.8)]); },
    preview(g, w, h, t, accent) { bg(g, w, h); blocks(g, '#3a4380', [[w * 0.5, h * 0.2], [w * 0.55, h * 0.8], [w * 0.9, h * 0.5]], 9); const u = (t * 0.5) % 1, x = u * w, y = h * 0.5 + Math.sin(u * Math.PI) * h * 0.2, a = -0.9 + u * 1.8; g.strokeStyle = accent + '55'; g.lineWidth = 1.5; g.beginPath(); g.moveTo(x, y); g.lineTo(x + 50, y - Math.cos(u * Math.PI) * 30); g.stroke(); g.fillStyle = '#ffb347'; g.beginPath(); g.moveTo(x - Math.cos(a) * 10, y - Math.sin(a) * 10); g.lineTo(x - Math.cos(a) * 30 + Math.sin(a) * 4, y - Math.sin(a) * 30 - Math.cos(a) * 4); g.lineTo(x - Math.cos(a) * 30 - Math.sin(a) * 4, y - Math.sin(a) * 30 + Math.cos(a) * 4); g.closePath(); g.fill(); dart(g, accent, x, y, a, 1.1); },
  };

  // ================= body: the Eel =================
  K.body = {
    mechanicName: 'coiling', trail: false, aiRule: 'It coils (LT) when its crystal is more than 50 deg off the nose and releases the whip as it comes round; it sheds five segments (RT) on a straight when its body is longer than 16, trading length for speed.',
    defaults: { startLen: 8, maxLen: 30, segGap: 6, cruiseShort: 72, cruiseLong: 45, shedCount: 5, shedKick: 1.25, coilTurn: 2.2, coilGap: 3, whipReach: 10, growPerCrystal: 2, growPerSkims: 10, segScale: 2.2 },
    spec: [['startLen', 2, 20, 1, 'Starting body', 'Segments at the start of a race'], ['maxLen', 10, 60, 1, 'Longest body', 'Segment cap'], ['segGap', 3, 12, 0.5, 'Segment gap', 'Distance between segments (u)'], ['cruiseShort', 30, 150, 1, 'Short cruise', 'Cruise speed at the starting length (u/s)'], ['cruiseLong', 20, 120, 1, 'Long cruise', 'Cruise speed at the longest body (u/s)'],
      ['shedCount', 1, 12, 1, 'Shed', 'Segments RT drops as standing prisms'], ['shedKick', 1, 1.8, 0.05, 'Shed kick', 'Speed multiplier on a shed'], ['coilTurn', 1, 4, 0.1, 'Coil turn', 'Turn multiplier while coiled (LT)'], ['coilGap', 1, 6, 0.5, 'Coil gap', 'Segment gap while coiled: the body bunches (u)'], ['whipReach', 3, 30, 1, 'Whip reach', 'How far from each segment the whip cuts on release (u)'], ['segScale', 1, 5, 0.1, 'Segment size', 'Size of a body segment (u)']],
    init(P) { P.K = { path: [P.pos.clone()], body: [], len: P.m.startLen, coil: 0, whips: 0, sheds: 0, skimAcc: 0, active: false }; },
    step(W, P, inp, dt, ctx) {
      const m = P.m, Kk = P.K;
      const coiling = inp.lt > 0.5; Kk.coil += ((coiling ? 1 : 0) - Kk.coil) * (1 - Math.exp(-5 * dt));
      ctx.steer(P, inp, dt, 1 + (m.coilTurn - 1) * Kk.coil);
      const cruise = lerp(m.cruiseShort, m.cruiseLong, clamp((Kk.len - m.startLen) / Math.max(1, m.maxLen - m.startLen), 0, 1));
      ctx.move(P, dt, cruise * Math.max(0.35, P.throttle) * P.boost, P.F.grip, P.F.accel);
      // the path the body follows: a point every unit of travel, trimmed to what the body needs
      const last = Kk.path[Kk.path.length - 1]; if (last.distanceTo(P.pos) >= 1) Kk.path.push(P.pos.clone());
      const need = Math.ceil(Kk.len * m.segGap) + 40; if (Kk.path.length > need) Kk.path.splice(0, Kk.path.length - need);
      const gap = lerp(m.segGap, m.coilGap, Kk.coil), body = Kk.body; body.length = 0; let acc = 0, i = Kk.path.length - 1;
      while (body.length < Kk.len && i > 0) { acc += Kk.path[i].distanceTo(Kk.path[i - 1]); if (acc >= gap) { acc -= gap; body.push(Kk.path[i - 1]); } i--; }
      if (fall(P, 'ltWas', coiling) && Kk.coil > 0.3) {   // the whip: the bunched body snaps straight; rival and neutral mass within reach of any segment is cut
        let cut = 0; for (const seg of body) for (const p of ctx.near(W, seg, m.whipReach + 4)) { if (isMass(p) && p.owner !== P.i && p.kind !== 'track' && seg.distanceTo(p.pos) <= m.whipReach) { ctx.removePrism(W, p); cut++; } }
        Kk.whips++; P.stats.uses++; ctx.event(W, P, 'Whip! ' + cut + ' prisms cut');
      }
      if (edge(P, 'rtWas', inp.rt > 0.5) && Kk.len > Math.max(2, m.startLen)) {
        // the shed segments flick off to the sides of the line, alternating, so the Eel is not laying a wall across its own next lap
        const n = Math.min(m.shedCount, Kk.len - 2), dropped = body.slice(body.length - n), { right } = ctx.axes(P);
        dropped.forEach((seg, i) => ctx.layPrism(W, P, seg.clone().addScaledVector(right, (i % 2 ? 1 : -1) * 9), P.quat, [m.segScale, m.segScale, m.segScale * 1.6], { kind: 'shed', shield: true }));
        Kk.len -= n; P.speed = Math.min(m.cruiseShort * 1.8, P.speed * m.shedKick); Kk.sheds++; Kk.shedT = W.t; P.stats.uses++; ctx.event(W, P, 'Shed ' + n + ' segments \u00b7 \u00d7' + m.shedKick.toFixed(2));
      }
      Kk.active = Kk.coil > 0.3 || W.t - (Kk.shedT || -9) < 1.5;
    },
    onCrystal(W, P) { P.K.len = Math.min(P.m.maxLen, P.K.len + P.m.growPerCrystal); },
    onSkim(W, P) { P.K.skimAcc++; if (P.K.skimAcc >= P.m.growPerSkims) { P.K.skimAcc = 0; P.K.len = Math.min(P.m.maxLen, P.K.len + 1); } },
    stepWorld(W, dt, ctx) {   // a rival's hull in your body = their reset (the body is shielded mass)
      for (const P of W.pilots) { if (!P.K.body) continue; for (const R of W.pilots) { if (R === P || R.finishT != null || (R.K._bodyCool || 0) > W.t) continue; for (const seg of P.K.body) { if (seg.distanceToSquared(R.pos) < (P.m.segScale + 1.6) ** 2) { R.boost = 1; R.slow = Math.min(R.slow, 0.85); R.resets++; R.K._bodyCool = W.t + 0.8; ctx.event(W, R, 'Hit ' + P.domain.name + '\'s body'); break; } } } }
    },
    ai(W, P, aim, base) { const Kk = P.K, out = {}; if (base.err > 50) out.lt = 1; else if (Kk.coil > 0.3 && base.err < 25) out.lt = 0; if (Kk.len > 16 && base.err < 10) out.rt = 1; return out; },
    hud(P) { const m = P.m, Kk = P.K; return [['Body', Kk.len + ' segments', Kk.len / m.maxLen], ['Coil', Math.round(Kk.coil * 100) + '%', Kk.coil, Kk.coil > 0.3 ? '#c48bff' : '']]; },
    abilityStates(P) { const m = P.m; return { charge: '\u00d7' + m.shedKick.toFixed(2) + ' shed', mass: m.segScale.toFixed(1) + ' u segments', space: Math.round(m.whipReach) + ' u whip', time: '\u00d7' + m.coilTurn.toFixed(1) + ' coil turn' }; },
    hull(ctx, color) { const g = new ctx.THREE.Group(), m = mat(ctx, color); const head = new ctx.THREE.Mesh(new ctx.THREE.SphereGeometry(2.4, 10, 8), m); head.scale.set(1, 0.8, 1.4); g.add(head); g.add(box(ctx, m, 0.3, 2.4, 1.6, 0, 1.4, 0.5)); return g; },
    mount(ctx, scene, P) { const n = 64, im = new ctx.THREE.InstancedMesh(new ctx.THREE.BoxGeometry(1, 1, 1), mat(ctx, P.domain.color, { roughness: 0.5 }), n); im.instanceMatrix.setUsage(ctx.THREE.DynamicDrawUsage); im.frustumCulled = false; scene.add(im); return { im, dispose() { scene.remove(im); } }; },
    draw(W, P, o, t, ctx) { const b = P.K.body || [], m4 = new ctx.THREE.Matrix4(), q = new ctx.Q(), s = new ctx.V(), up = new ctx.V(0, 1, 0); let n = 0; for (let i = 0; i < b.length && n < 64; i++) { const a = b[i], nx = b[i + 1] || P.pos, d = nx.clone().sub(a); if (d.lengthSq() > 1e-4) q.setFromRotationMatrix(new ctx.THREE.Matrix4().lookAt(new ctx.V(), d.negate(), up)); const sc = P.m.segScale * (1 - i / (b.length + 2) * 0.5); s.set(sc, sc * 0.8, sc * 1.6); m4.compose(a, q, s); o.im.setMatrixAt(n++, m4); } o.im.count = n; o.im.instanceMatrix.needsUpdate = true; },
    preview(g, w, h, t, accent) { bg(g, w, h); const pts = []; for (let i = 0; i < 18; i++) { const u = t * 0.5 - i * 0.07; pts.push([w * 0.5 + Math.cos(u) * w * 0.3, h * 0.5 + Math.sin(u * 2) * h * 0.3]); } for (let i = pts.length - 1; i >= 1; i--) { const s = 9 - i * 0.35; g.fillStyle = accent; g.globalAlpha = 1 - i * 0.04; g.fillRect(pts[i][0] - s / 2, pts[i][1] - s / 2, s, s); } g.globalAlpha = 1; const a = Math.atan2(pts[0][1] - pts[1][1], pts[0][0] - pts[1][0]); dart(g, accent, pts[0][0], pts[0][1], a, 1.1); blocks(g, '#3a4380', [[w * 0.12, h * 0.2], [w * 0.88, h * 0.82]], 9); },
  };

  // ================= pulse: the Octopus =================
  K.pulse = {
    mechanicName: 'pulsing', aiRule: 'It breathes on a fixed tempo: inhale (LT) for 0.72 s of every second, release; Hard keeps the beat exactly, Easy and Medium press late and lose the chain. It inks (RT) when a rival is within 50 u behind it.',
    defaults: { idle: 20, drag: 0.8, inhaleTime: 0.7, jetMin: 55, jetMax: 205, inhaleDrag: 2.5, beatWindow: 0.15, chainStep: 0.1, chainCap: 1.8, inkCount: 10, inkScale: 2.2, topSpeed: 260, aiTempo: 1.15 },
    spec: [['idle', 5, 60, 1, 'Idle drift', 'Speed it sinks to between pulses (u/s)'], ['drag', 0.1, 3, 0.05, 'Pulse fade', 'How fast a jet fades (per second)'], ['inhaleTime', 0.2, 2, 0.05, 'Inhale time', 'Seconds of LT for a full charge'], ['jetMin', 20, 120, 5, 'Weak jet', 'Jet speed from an empty charge (u/s)'], ['jetMax', 80, 350, 5, 'Full jet', 'Jet speed from a full charge (u/s)'], ['inhaleDrag', 0.5, 6, 0.1, 'Inhale brake', 'Drag while inhaling'],
      ['beatWindow', 0.03, 0.4, 0.01, 'Beat window', 'A release within this fraction of your last interval keeps the chain'], ['chainStep', 0, 0.3, 0.01, 'Chain step', 'Jet multiplier gained per chained pulse'], ['chainCap', 1, 3, 0.1, 'Chain cap', 'Highest jet multiplier'], ['inkCount', 0, 24, 1, 'Ink cloud', 'Danger prisms RT lays behind (costs one pulse of charge)'], ['inkScale', 1, 5, 0.2, 'Ink prism', 'Size of an ink prism (u)'], ['topSpeed', 100, 400, 10, 'Top speed', 'Speed cap (u/s)']],
    init(P) { P.K = { charge: 0, inhaling: false, lastPulseT: -9, interval: 0, chain: 0, mult: 1, ink: 1, pulses: 0, active: false, aiT: -1 }; },
    step(W, P, inp, dt, ctx) {
      const m = P.m, Kk = P.K, { fwd } = ctx.axes(P);
      Kk.inhaling = inp.lt > 0.3;
      ctx.steer(P, inp, dt, Kk.inhaling ? 0.7 : 1);
      if (Kk.inhaling) Kk.charge = Math.min(1, Kk.charge + dt / m.inhaleTime);
      if (fall(P, 'ltWas', Kk.inhaling)) {   // the jet; a release on your own beat chains
        const since = W.t - Kk.lastPulseT; let onBeat = false;
        if (Kk.interval > 0 && Kk.lastPulseT > 0) onBeat = Math.abs(since - Kk.interval) <= m.beatWindow * Kk.interval;
        Kk.chain = onBeat ? Kk.chain + 1 : 0; Kk.mult = Math.min(m.chainCap, 1 + m.chainStep * Kk.chain); Kk.interval = since; Kk.lastPulseT = W.t;
        P.speed = Math.min(m.topSpeed, Math.max(P.speed, lerp(m.jetMin, m.jetMax, Kk.charge) * Kk.mult)); P.course.lerp(fwd, 0.7).normalize();
        Kk.ink = Math.min(1, Kk.ink + 0.5); Kk.charge = 0; Kk.pulses++; P.stats.uses++; ctx.event(W, P, (onBeat ? 'On the beat! chain ' + Kk.chain + ' \u00d7' + Kk.mult.toFixed(2) : 'Pulse') + ' \u00b7 ' + Math.round(P.speed) + ' u/s');
      }
      ctx.move(P, dt, m.idle * P.boost, P.F.grip, Kk.inhaling ? m.inhaleDrag : m.drag);
      if (edge(P, 'rtWas', inp.rt > 0.5) && Kk.ink >= 1 && m.inkCount > 0) { const c = P.pos.clone().addScaledVector(P.course, -14), r = W.rand; for (let i = 0; i < m.inkCount; i++) ctx.layPrism(W, P, c.clone().add(new ctx.V(r() * 2 - 1, r() * 2 - 1, r() * 2 - 1).multiplyScalar(9)), P.quat, [m.inkScale, m.inkScale, m.inkScale], { kind: 'ink', danger: true }); Kk.ink = 0; ctx.event(W, P, 'Ink'); }
      Kk.active = Kk.inhaling || W.t - Kk.lastPulseT < 0.6;
    },
    ai(W, P, aim, base, ctx) { const m = P.m, Kk = P.K, out = {}; if (Kk.aiT < 0) Kk.aiT = W.t; const phase = ((W.t - Kk.aiT) % m.aiTempo) / m.aiTempo; out.lt = phase < 0.72 ? 1 : 0; if (Kk.ink >= 1) for (const R of ctx.rivals(W, P)) { const d = R.pos.clone().sub(P.pos); if (d.length() < 50 && d.dot(P.course) < -0.5 * d.length()) { out.rt = 1; break; } } return out; },
    hud(P) { const m = P.m, Kk = P.K, t = Kk._t || 0, ph = Kk.interval > 0 ? ((t - Kk.lastPulseT) / Kk.interval) % 1 : 0, inWin = Kk.interval > 0 && Math.abs((t - Kk.lastPulseT) - Kk.interval) <= m.beatWindow * Kk.interval; return [['Beat', Kk.inhaling ? 'charge ' + Math.round(Kk.charge * 100) + '%' : Kk.interval > 0 ? 'tempo ' + Kk.interval.toFixed(2) + ' s \u00b7 chain ' + Kk.chain + ' \u00d7' + Kk.mult.toFixed(2) : 'hold LT, release', Kk.inhaling ? Kk.charge : ph, inWin ? '#9dff5a' : ''], ['Ink', Kk.ink >= 1 ? 'ready' : Math.round(Kk.ink * 100) + '%', Kk.ink]]; },
    draw(W, P) { P.K._t = W.t; },
    abilityStates(P) { const m = P.m; return { charge: Math.round(m.jetMax) + ' u/s jet', mass: m.inkCount + ' ink prisms', space: Math.round(m.beatWindow * 100) + '% beat window', time: m.inhaleTime.toFixed(2) + ' s inhale' }; },
    hull(ctx, color) { const g = new ctx.THREE.Group(), m = mat(ctx, color); const bulb = new ctx.THREE.Mesh(new ctx.THREE.SphereGeometry(2.8, 12, 10), m); bulb.name = 'bulb'; bulb.scale.set(1, 0.9, 1.3); g.add(bulb); for (let i = 0; i < 8; i++) { const a = i / 8 * Math.PI * 2, c = new ctx.THREE.Mesh(new ctx.THREE.ConeGeometry(0.45, 6, 5), m); c.name = 't' + i; c.position.set(Math.cos(a) * 1.6, Math.sin(a) * 1.6, 4.5); c.rotation.x = Math.PI / 2; g.add(c); } return g; },
    poseHull(h, P, t) { const b = h.getObjectByName('bulb'); if (b) { const k = 1 + 0.45 * P.K.charge; b.scale.set(k, k * 0.9, 1.3 * (1 - 0.2 * P.K.charge)); } const spread = clamp(P.speed / 150, 0, 1); for (let i = 0; i < 8; i++) { const c = h.getObjectByName('t' + i); if (c) { const a = i / 8 * Math.PI * 2, r = 1.6 + 1.8 * (1 - spread) + 0.3 * Math.sin(t * 6 + i); c.position.set(Math.cos(a) * r, Math.sin(a) * r, 4.5); } } },
    preview(g, w, h, t, accent) { bg(g, w, h); const beat = (t * 1.2) % 1, x = w * 0.2 + ((t * 70) % (w * 0.7)), y = h * 0.5; for (let i = 1; i < 4; i++) { g.fillStyle = '#14101c'; g.globalAlpha = 0.9; g.beginPath(); g.arc(x - 40 - i * 26 + ((t * 70) % 1), y + Math.sin(i * 2.1) * 8, 9, 0, Math.PI * 2); g.fill(); } g.globalAlpha = 1; const r = 12 * (1 + 0.5 * (beat < 0.7 ? beat / 0.7 : 0)); g.fillStyle = accent; g.beginPath(); g.ellipse(x, y, r, r * 0.8, 0, 0, Math.PI * 2); g.fill(); for (let i = 0; i < 6; i++) { g.strokeStyle = accent + 'aa'; g.lineWidth = 2; g.beginPath(); g.moveTo(x - r, y - 6 + i * 2.4); g.lineTo(x - r - 18 - (beat > 0.7 ? 14 : 0), y - 10 + i * 4); g.stroke(); } if (beat > 0.7) { g.strokeStyle = '#9dff5a'; g.lineWidth = 2; g.beginPath(); g.arc(x, y, r + 8 + (beat - 0.7) * 60, 0, Math.PI * 2); g.stroke(); } },
  };

  // ================= surface: the Gecko =================
  K.surface = {
    mechanicName: 'running', aiRule: 'It sticks (LT) to a prism chain within reach whose direction is within 55 deg of its crystal, runs it, and hops off (release) when the chain bends more than 50 deg away or the crystal is within 120 u and off the chain.',
    defaults: { cruise: 50, stickReach: 10, runCap: 130, runRamp: 25, hopKick: 1.4, hopKeep: 3, chainLook: 24, clear: 4.5, topSpeed: 200, turnStuck: 0.4, convertScale: 1 },
    spec: [['cruise', 20, 100, 1, 'Free cruise', 'Speed off the wall (u/s)'], ['stickReach', 4, 30, 1, 'Stick reach', 'How far from a prism LT can grab it (u)'], ['runCap', 60, 300, 5, 'Run speed', 'Top speed on a surface (u/s)'], ['runRamp', 5, 80, 1, 'Run ramp', 'Acceleration along the surface (u/s squared)'], ['hopKick', 1, 1.8, 0.05, 'Hop kick', 'Speed multiplier on a hop off'], ['hopKeep', 0.5, 6, 0.1, 'Hop momentum', 'Seconds a hop\'s speed fades back to cruise'],
      ['chainLook', 8, 60, 1, 'Chain look', 'How far along the structure it looks for the next prism (u)'], ['clear', 2, 10, 0.5, 'Clearance', 'How far off the surface it runs (u)'], ['topSpeed', 100, 400, 10, 'Top speed', 'Speed cap (u/s)'], ['turnStuck', 0.1, 1, 0.05, 'Stuck steering', 'Stick authority while running (the surface guides)'], ['convertScale', 1, 2.5, 0.1, 'Convert growth', 'A rival rail it runs grows by this']],
    init(P) { P.K = { stuck: null, next: null, hopUntil: -9, hopSpeed: 0, releaseT: -9, active: false, hops: 0, converted: 0 }; },
    // the next prism along the structure: ahead of the run (never the one it just left, never one behind), else the structure ends here
    nextOnChain(W, P, cur, ctx) { const m = P.m; let best = null, bs = 0.1; for (const p of ctx.near(W, cur.pos, m.chainLook)) { if (!isMass(p) || p === cur || p === P.K.prev) continue; const d = p.pos.clone().sub(cur.pos), l = d.length(); if (l < 1 || l > m.chainLook) continue; const c = d.divideScalar(l).dot(P.course); if (c < 0.3) continue; const s = c - l / (m.chainLook * 4); if (s > bs) { bs = s; best = p; } } return best; },
    step(W, P, inp, dt, ctx) {
      const m = P.m, Kk = P.K;
      if (Kk.stuck && (Kk.stuck.dead)) Kk.stuck = null;
      if (!Kk.stuck) {
        ctx.steer(P, inp, dt, 1);
        const since = W.t - Kk.releaseT, keep = since < m.hopKeep ? lerp(Kk.hopSpeed, m.cruise, since / m.hopKeep) : 0;
        ctx.move(P, dt, Math.max(keep, m.cruise * Math.max(0.4, P.throttle) * P.boost), P.F.grip, since < m.hopKeep ? 3 : P.F.accel);
        if (inp.lt > 0.5) { const p = ctx.nearest(W, P.pos, m.stickReach + 4, (x) => isMass(x) && x.pos.distanceTo(P.pos) <= m.stickReach + 3); if (p) { Kk.prev = null; Kk.stuck = p; Kk.next = K.surface.nextOnChain(W, P, p, ctx); P.stats.uses++; ctx.event(W, P, 'Stuck on'); } }
        P.K.ltWas = inp.lt > 0.5; P.K.rtWas = inp.rt > 0.5; Kk.active = false; return;
      }
      // running the surface: the chain's direction guides the course, the clearance keeps the hull off the mass
      ctx.steer(P, inp, dt, m.turnStuck);
      const cur = Kk.stuck; if (!Kk.next || Kk.next.dead) Kk.next = K.surface.nextOnChain(W, P, cur, ctx);
      const dir = Kk.next ? Kk.next.pos.clone().sub(cur.pos).normalize() : P.course.clone();
      P.course.lerp(dir, 1 - Math.exp(-8 * dt)).normalize();
      P.speed = Math.min(m.runCap * P.boost, P.speed + m.runRamp * dt);
      const seg = Kk.next ? Kk.next.pos.clone().sub(cur.pos) : P.course.clone(), tt = Kk.next ? clamp(P.pos.clone().sub(cur.pos).dot(seg) / Math.max(1e-3, seg.lengthSq()), 0, 1) : 0;
      const closest = cur.pos.clone().addScaledVector(seg, tt), nrm = P.pos.clone().sub(closest); if (nrm.lengthSq() < 1e-3) nrm.set(0, 1, 0); nrm.normalize();
      const want = closest.addScaledVector(nrm, m.clear + 1.5); P.pos.lerp(want, 1 - Math.exp(-10 * dt)); P.pos.addScaledVector(P.course, P.speed * dt); P.vel.copy(P.course).multiplyScalar(P.speed);
      const look = new ctx.Q().setFromRotationMatrix(new ctx.THREE.Matrix4().lookAt(new ctx.V(), P.course.clone().negate(), nrm)); P.quat.slerp(look, 1 - Math.exp(-6 * dt));
      if (cur.kind === 'rail' && cur.owner !== P.i && cur.owner >= 0) { cur.domain = P.domain.key; cur.owner = P.i; cur.scale = [cur.scale[0] * m.convertScale, cur.scale[1] * m.convertScale, cur.scale[2]]; Kk.converted++; P.stolen++; W.dirty = true; }
      // step to the next prism when the hull reaches it OR has run past the segment's end (it used to hover 20 u past the end, pulled back by the clearance)
      if (Kk.next && (P.pos.distanceTo(Kk.next.pos) < m.clear + 4 || tt >= 0.98)) { Kk.prev = Kk.stuck; Kk.stuck = Kk.next; Kk.next = K.surface.nextOnChain(W, P, Kk.stuck, ctx); }
      const hop = inp.lt <= 0.5 || edge(P, 'rtWas', inp.rt > 0.5) || !Kk.next;
      if (hop) { const { fwd } = ctx.axes(P); P.course.copy(fwd).addScaledVector(nrm, 0.35).normalize(); Kk.hopSpeed = Math.min(m.topSpeed, P.speed * m.hopKick); P.speed = Kk.hopSpeed; Kk.releaseT = W.t; Kk.hopUntil = W.t + m.hopKeep; Kk.hops++; ctx.event(W, P, 'Hop! ' + Math.round(P.speed) + ' u/s'); Kk.stuck = null; Kk.next = null; }
      P.K.ltWas = inp.lt > 0.5; Kk.active = !!Kk.stuck;
    },
    onContact(W, P) { return !!P.K.stuck || W.t - P.K.releaseT < 0.35; },
    ai(W, P, aim, base, ctx) {
      const m = P.m, Kk = P.K, dir = aim.clone().sub(P.pos).normalize(), out = {}, forCrystal = P.aimWhy !== 'line' && P.aimWhy !== 'skip';
      if (Kk.stuck) { const nx = Kk.next; const chain = nx ? nx.pos.clone().sub(Kk.stuck.pos).normalize() : P.course; const bend = Math.acos(clamp(chain.dot(dir), -1, 1)) / ctx.DEG; const off = forCrystal && bend > 25; out.lt = (bend > 50 || off) ? 0 : 1; if (!out.lt) Kk.noStickUntil = W.t + (off ? 2.5 : 0.8); return out; }
      if (W.t < (Kk.noStickUntil || 0)) return out;   // it just hopped off for its crystal: fly to it before grabbing the next wall
      const p = ctx.nearest(W, P.pos, m.stickReach * 1.5, (x) => isMass(x)); if (p) { const nx = K.surface.nextOnChain(W, P, p, ctx); if (nx) { const chain = nx.pos.clone().sub(p.pos).normalize(); if (Math.acos(clamp(chain.dot(dir), -1, 1)) / ctx.DEG < (forCrystal ? 25 : 55)) out.lt = 1; } }
      return out;
    },
    hud(P) { const m = P.m, Kk = P.K, t = Kk._t || 0; return [['Surface', Kk.stuck ? 'running \u00b7 ' + Math.round(P.speed) + ' u/s' : 'free', clamp(P.speed / m.runCap, 0, 1), Kk.stuck ? '#9dff5a' : ''], ['Hop', t - Kk.releaseT < m.hopKeep ? 'momentum ' + Math.round((1 - (t - Kk.releaseT) / m.hopKeep) * 100) + '%' : Kk.hops + ' hops', t - Kk.releaseT < m.hopKeep ? 1 - (t - Kk.releaseT) / m.hopKeep : 0]]; },
    abilityStates(P) { const m = P.m; return { charge: '\u00d7' + m.hopKick.toFixed(2) + ' hop', mass: '\u00d7' + m.convertScale.toFixed(1) + ' converted', space: Math.round(m.stickReach) + ' u reach', time: Math.round(m.runCap) + ' u/s run' }; },
    hull(ctx, color) { const g = new ctx.THREE.Group(), m = mat(ctx, color); g.add(box(ctx, m, 2.6, 1, 6)); g.add(box(ctx, m, 1.2, 0.6, 4, 0, 0, 5)); for (const [x, z] of [[-2, -1.8], [2, -1.8], [-2, 1.8], [2, 1.8]]) g.add(box(ctx, m, 1.6, 0.4, 0.8, x, -0.5, z)); return g; },
    mount(ctx, scene, P) { const l = line(ctx, P.domain.color, 2); scene.add(l); return { l, dispose() { scene.remove(l); } }; },
    draw(W, P, o) { P.K._t = W.t; o.l.visible = !!P.K.stuck; if (P.K.stuck) setLine(o.l, [P.pos, P.K.stuck.pos]); },
    preview(g, w, h, t, accent) { bg(g, w, h); const rowA = [], rowB = []; for (let i = 0; i < 14; i++) { rowA.push([w * 0.08 + i * 16, h * 0.7 + Math.sin(i * 0.5) * 6]); rowB.push([w * 0.45 + i * 16, h * 0.3 - Math.sin(i * 0.5) * 6]); } blocks(g, '#3a4380', rowA.concat(rowB), 10); const u = (t * 0.45) % 1; let x, y; if (u < 0.45) { const i = u / 0.45 * 13; x = w * 0.08 + i * 16; y = h * 0.7 + Math.sin(i * 0.5) * 6 - 11; } else if (u < 0.6) { const f = (u - 0.45) / 0.15; x = lerp(w * 0.08 + 13 * 16, w * 0.45 + 2 * 16, f); y = lerp(h * 0.7 - 11, h * 0.3 + 11, f) - Math.sin(f * Math.PI) * 30; } else { const i = 2 + (u - 0.6) / 0.4 * 11; x = w * 0.45 + i * 16; y = h * 0.3 - Math.sin(i * 0.5) * 6 + 11; } g.fillStyle = accent; g.fillRect(x - 9, y - 3, 18, 6); g.fillRect(x - 11, y - 5, 4, 2); g.fillRect(x + 7, y - 5, 4, 2); g.fillRect(x - 11, y + 3, 4, 2); g.fillRect(x + 7, y + 3, 4, 2); },
  };

  window.VesselStudioKinds = K;
})();
