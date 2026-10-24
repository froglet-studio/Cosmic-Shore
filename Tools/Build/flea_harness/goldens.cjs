// Prints the golden numbers FleaMathTests holds the C# to, by running the Vessel Studio page's OWN Flea code
// (studio-kinds.js, K.ricochet: step, normal, onContact) in node on fixed inputs. No reimplementation: the page's
// functions run with a minimal ctx (three.js vectors, no world, no steering).
//   node Tools/Build/flea_harness/goldens.cjs <studio-kinds.js> <three.min.js>
// studio-kinds.js is on Ys-bleeding-edge (Docs/Studios/VesselStudio/), three.min.js is three r128 (any local copy).
'use strict';
const [kindsPath, threePath] = process.argv.slice(2);
global.window = {}; global.self = global;
const THREE = require(require('path').resolve(threePath)); require(require('path').resolve(kindsPath));
const K = window.VesselStudioKinds.ricochet, V = THREE.Vector3, Q = THREE.Quaternion;
const ctx = { THREE, V, Q, DEG: Math.PI / 180, axes: (P) => ({ fwd: new V(0, 0, -1).applyQuaternion(P.quat), up: new V(0, 1, 0).applyQuaternion(P.quat), right: new V(1, 0, 0).applyQuaternion(P.quat) }), steer() {}, near: () => [], event() {} };
const M = Object.assign({}, K.defaults);
function pilot(speed, course, quat) { const P = { pos: new V(), quat: quat || new Q(), course: course.clone().normalize(), vel: new V(), speed, boost: 1, slow: 1, m: M, stats: { uses: 0 } }; K.init(P); return P; }
const W = { t: 0 };
const inp = (lt, rt) => ({ pad: true, L: { x: 0, y: 0 }, R: { x: 0, y: 0 }, lt, rt });
const f = (x) => +x.toFixed(6), vec = (v) => [f(v.x), f(v.y), f(v.z)], dt = 1 / 60, out = {};
// A. crawl and grip: from rest, course 90 deg off the nose, no input, 60 frames
{ const P = pilot(0, new V(1, 0, 0)); W.t = 0; const rows = []; for (let i = 1; i <= 60; i++) { W.t = i * dt; K.step(W, P, inp(0, 0), dt, ctx); if (i === 30 || i === 60) rows.push({ frame: i, speed: f(P.speed), course: vec(P.course) }); } out.crawl = rows; }
// B. crouch 0.4 s then release: charge, crouch speed, the hop
{ const P = pilot(80, new V(0, 0, -1)); const rows = []; for (let i = 1; i <= 24; i++) { W.t = 10 + i * dt; K.step(W, P, inp(1, 0), dt, ctx); } rows.push({ crouchCharge: f(P.K.charge), crouchSpeed: f(P.speed) }); W.t += dt; K.step(W, P, inp(0, 0), dt, ctx); rows.push({ hopSpeed: f(P.speed), chargeAfter: f(P.K.charge) }); out.hop = rows; }
// C. a perfect bounce: RT pressed 0.05 s before the contact, head-on into a surface whose normal is +z
{ const P = pilot(100, new V(0, 0, -1)); W.t = 20; K.step(W, P, inp(0, 1), dt, ctx); const speedBefore = P.speed; W.t = 20.05; const p = { pos: new V(0, 0, -3), quat: new Q().setFromAxisAngle(new V(1, 0, 0), Math.PI / 2), flash: 0 }; P.pos.set(0, 0, 0); K.onContact(W, P, p, ctx); out.perfect = { speedBefore: f(speedBefore), speed: f(P.speed), course: vec(P.course), chain: P.K.chain, perfects: P.K.perfects }; }
// D. a dull bounce, then a late press 0.05 s after it (inside half the window) saves it
{ const P = pilot(100, new V(0, 0, -1)); W.t = 30; const p = { pos: new V(0, 0, -3), quat: new Q().setFromAxisAngle(new V(1, 0, 0), Math.PI / 2), flash: 0 }; K.onContact(W, P, p, ctx); const dull = { speed: f(P.speed), chain: P.K.chain, dullAt: P.K.dullAt }; W.t = 30.05; K.step(W, P, inp(0, 1), dt, ctx); out.late = { dull, afterLatePress: f(P.speed), chain: P.K.chain }; }
// E. the bounce normal off a prism's axis: a prism along z at the origin, hull at (3, 1, 5); and the on-axis fallback
{ const P = pilot(0, new V(0, 0, -1)); const p = { pos: new V(0, 0, 0), quat: new Q() }; P.pos.set(3, 1, 5); const n1 = K.normal(P, p, ctx); P.pos.set(0.2, 0.1, 7); P.quat.setFromAxisAngle(new V(0, 0, 1), 0.5); const n2 = K.normal(P, p, ctx); out.normal = { offAxis: vec(n1), onAxisUsesHullUp: vec(n2) }; }
// F. a glancing skip along a ribbon prism (its axis along x): 30 deg down into it, dull: it keeps its along-track speed
{ const cin = new V(Math.cos(Math.PI / 6), -Math.sin(Math.PI / 6), 0); const P = pilot(120, cin); W.t = 40; const p = { pos: new V(0, 0, 0), quat: new Q().setFromAxisAngle(new V(0, 1, 0), Math.PI / 2) }; P.pos.set(0, 2, 0); K.onContact(W, P, p, ctx); out.skip = { courseIn: vec(cin), speed: f(P.speed), course: vec(P.course) }; }
console.log(JSON.stringify(out, null, 1));
