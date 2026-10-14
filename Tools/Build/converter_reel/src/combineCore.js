// The Elemental Converter's motion, ported line for line from the shipped C#:
//   Assets/_Scripts/Utility/OmniCombineCore.cs      (Pose, OmniPose, Frame, FaceCamera, CrystalFrame, seeding, flip rig)
//   Assets/_Scripts/Utility/OmniCombineMotion.cs    (TryFit, Swell, TurnSquares)
//   Assets/_Scripts/Utility/SquareTurnFit.cs        (TryFit, At)
//   Assets/_Scripts/Utility/OmniCadence.cs          (SquaresAt, WaveAt, Smoother)
//   OmniCombineConfigSO.Draw                         (one play's knobs, from the config's ranges)
// Unity's axes (left-handed, Y up), no conversion. Vectors are [x, y, z] arrays; quaternions [w, x, y, z].
// tools/parity_core gates this file against the C# (Tools/Build/converter_reel/parity_core/).
(function (CR) {
  'use strict';
  const SPACE = 0, CHARGE = 1, MASS = 2, TIME = 3;
  const ORDER = ['Space', 'Charge', 'Mass', 'Time'];
  const RNG_ORDER = [CHARGE, MASS, SPACE, TIME];
  const TIME_FLIP_SALT = 11, OMNI_FLIP_SALT = 23;
  const APPROACH_TURN_ENDS = 0.7, ARRIVAL_AT = 0.85;

  // ------------------------------------------------------------------ maths (the core's, in doubles)
  const add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
  const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
  const mul = (a, s) => [a[0] * s, a[1] * s, a[2] * s];
  const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const len = (a) => Math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
  const unit = (a) => { const l = len(a); return l > 0 ? mul(a, 1 / l) : a; };
  const lerp3 = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
  const V = (a, i) => [a[3 * i], a[3 * i + 1], a[3 * i + 2]];
  const clamp01 = (x) => (x < 0 ? 0 : x > 1 ? 1 : x);
  const smooth = (x) => { x = clamp01(x); return x * x * (3 - 2 * x); };
  const easeIO = (u, k) => { u = clamp01(u); return u < 0.5 ? 0.5 * Math.pow(2 * u, k) : 1 - 0.5 * Math.pow(2 * (1 - u), k); };
  function rotAxis(axis, ang, v) {
    const c = Math.cos(ang), s = Math.sin(ang), d = dot(axis, v), cr = cross(axis, v);
    return [v[0] * c + cr[0] * s + axis[0] * d * (1 - c), v[1] * c + cr[1] * s + axis[1] * d * (1 - c), v[2] * c + cr[2] * s + axis[2] * d * (1 - c)];
  }
  function quatRotate(q, v) {
    const u = [q[1], q[2], q[3]], t = mul(cross(u, v), 2);
    return add(add(v, mul(t, q[0])), cross(u, t));
  }
  function slerpIdentity(q, t) {
    const w = Math.max(-1, Math.min(1, q[0])), ang = 2 * Math.acos(Math.abs(w)), s = Math.sin(ang / 2), sgn = w < 0 ? -1 : 1;
    if (s < 1e-6) return [1, 0, 0, 0];
    const a = ang * t / 2, k = Math.sin(a) / s;
    return [Math.cos(a), q[1] * sgn * k, q[2] * sgn * k, q[3] * sgn * k];
  }
  // OmniCombineCore.Hash / Mulberry32: int32 arithmetic, as unchecked C#.
  function hash(a, b) {
    let h = Math.imul(a | 0, 374761393) ^ Math.imul(b | 0, 668265263);
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
  }
  function mulberry32(seed) {
    let a = seed | 0;
    return () => {
      a = (a + 0x6D2B79F5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  function wrapAngle(a) {
    a %= 2 * Math.PI;
    if (a > Math.PI) a -= 2 * Math.PI;
    else if (a <= -Math.PI) a += 2 * Math.PI;
    return a;
  }
  // Unity's Quaternion.AngleAxis(deg, axis) * v, with the axis normalised as Unity does.
  const angleAxis = (deg, axis, v) => rotAxis(unit(axis), deg * Math.PI / 180, v);

  // ------------------------------------------------------------------ plates and the flip rig
  function platesOf(points, plateIds) {
    let np = 0;
    for (const p of plateIds) np = Math.max(np, p + 1);
    const lists = Array.from({ length: np }, () => []);
    plateIds.forEach((p, i) => lists[p].push(i));
    return lists.map((idx) => {
      let c = [0, 0, 0], lo = Infinity, hi = -Infinity;
      const rad = idx.map((i) => { const v = V(points, i); c = add(c, v); const r = len(v); lo = Math.min(lo, r); hi = Math.max(hi, r); return r; });
      c = mul(c, 1 / idx.length);
      const mid = (hi + lo) / 2, outer = idx.filter((_, k) => rad[k] > mid);
      let oc = [0, 0, 0];
      for (const i of outer) oc = add(oc, V(points, i));
      oc = mul(oc, 1 / outer.length);
      const n = unit(c), r1 = unit(sub(V(points, outer[0]), oc)), r2 = cross(n, r1);
      // C#'s Array.Sort(keys, items) is not stable; ties do not occur on these meshes.
      const sorted = outer.map((i) => { const dv = sub(V(points, i), oc); return [Math.atan2(dot(dv, r2), dot(dv, r1)), i]; })
        .sort((a, b) => a[0] - b[0]).map((x) => x[1]);
      let best = 0, ax = [1, 0, 0];
      for (const a of sorted) for (const b of sorted) {
        const dv = sub(V(points, a), V(points, b)), l = len(dv);
        if (l > best) { best = l; ax = mul(dv, 1 / l); }
      }
      return { idx, c, dir: n, diag: ax, reach: best / 2 };
    });
  }

  function flipRig(plates, ids) {
    const a5 = Math.atan(1 / ((1 + Math.sqrt(5)) / 2)), near = Math.cos(4 * Math.PI / 180), axes = [];
    for (const p of ids) for (const sgn of [1, -1]) {
      const u = unit(add(mul(plates[p].dir, Math.cos(a5)), mul(plates[p].diag, sgn * Math.sin(a5))));
      if (!axes.some((x) => dot(x, u) > near)) axes.push(u);
    }
    const plans = axes.map((start) => {
      const hs = ids.map((p) => dot(plates[p].dir, start));
      const sortedH = hs.slice().sort((a, b) => b - a), levels = [];
      for (const h of sortedH) if (!levels.length || levels[levels.length - 1] - h > 0.05) levels.push(h);
      const plan = new Map();
      ids.forEach((p, k) => {
        const pl = plates[p], h = hs[k];
        let ring = 0;
        for (let i = 0; i < levels.length; i++) if (Math.abs(levels[i] - h) < Math.abs(levels[ring] - h)) ring = i;
        const toward = unit(sub(start, mul(pl.dir, h))), lng = pl.diag, sht = unit(cross(pl.dir, lng));
        let axis = Math.abs(dot(lng, toward)) < Math.abs(dot(sht, toward)) ? lng : sht;
        if (dot(cross(axis, pl.dir), toward) > 0) axis = mul(axis, -1);
        plan.set(p, { ring, axis, toward });
      });
      return plan;
    });
    return { axes, plans, startLoop: -1, start: 0 };
  }

  function curveAt(keys, t, empty) {
    if (!keys || keys.length < 2) return empty;
    if (t <= keys[0]) return keys[1];
    for (let i = 2; i + 1 < keys.length; i += 2) if (t <= keys[i]) {
      const t0 = keys[i - 2], v0 = keys[i - 1], t1 = keys[i], v1 = keys[i + 1];
      return v0 + (v1 - v0) * (t - t0) / (t1 - t0);
    }
    return keys[keys.length - 1];
  }

  function flipStart(rig, loopIndex, salt) {
    const n = rig.axes.length;
    if (rig.startLoop < 0 || loopIndex < rig.startLoop) { rig.startLoop = 0; rig.start = Math.floor(hash(salt, 0) * n); }
    for (let k = rig.startLoop + 1; k <= loopIndex; k++) rig.start = (rig.start + 1 + Math.floor(hash(salt, k) * (n - 1))) % n;
    rig.startLoop = Math.max(rig.startLoop, loopIndex);
    return rig.start;
  }

  // ------------------------------------------------------------------ the core
  class Core {
    constructor(data) {
      if (!data.crystals || data.crystals.length !== 4) throw new Error('the combine needs exactly four crystals');
      this.data = data;
      this.crystals = ORDER.map((name, k) => {
        const d = data.crystals.find((c) => c.element === name);
        if (!d) throw new Error('no ' + name + ' crystal in the combine data');
        return this._model(k, d);
      });
      const ov = data.omni.verts;
      this.omniRadius = 0;
      for (let i = 0; i < ov.length; i += 3) this.omniRadius = Math.max(this.omniRadius, Math.hypot(ov[i], ov[i + 1], ov[i + 2]));
      this.omniPlates = platesOf(ov, data.omni.plate);
      this.timeRig = flipRig(this.crystals[TIME].plates, this.crystals[TIME].plates.map((_, i) => i));
      const omniTime = [];
      this.omniPlates.forEach((_, p) => { if (data.omni.plateOwner[p] === TIME) omniTime.push(p); });
      this.omniRig = flipRig(this.omniPlates, omniTime);
      this.tumbleAxis = unit(data.tumble.direction);
      this.tumbleSpeed = data.tumble.speed * Math.PI / 180;
      this.seededWith = null;
      this.tumbleAxes = [null, null, null, null];
    }

    _model(k, d) {
      const n = d.basis.length / 3;
      if (!d.final || d.final.length !== d.basis.length || d.plate.length !== n) throw new Error(d.element + ': basis, final and plate disagree');
      const plates = platesOf(d.basis, d.plate);
      const finalCentre = plates.map((pl) => { let s = [0, 0, 0]; for (const i of pl.idx) s = add(s, V(d.final, i)); return mul(s, 1 / pl.idx.length); });
      const m = { k, data: d, n, plates, finalCentre, plateR: new Float64Array(plates.length) };
      if (k === CHARGE || k === MASS) {
        m.axis = plates.map((_, q) => V(d.plateAxis, q));
        m.twist = plates.map((_, q) => d.plateTwist[q] * Math.PI / 180);
      }
      if (k === TIME) m.quat = plates.map((_, q) => [d.plateQuat[4 * q], d.plateQuat[4 * q + 1], d.plateQuat[4 * q + 2], d.plateQuat[4 * q + 3]]);
      return m;
    }

    vertexCount(k) { return this.crystals[k].n; }

    seed(seed) {
      if (seed === this.seededWith) return;
      this.seededWith = seed;
      const rnd = mulberry32(seed);
      for (const k of RNG_ORDER) {
        const c = this.crystals[k];
        const z = 2 * rnd() - 1, a = 2 * Math.PI * rnd(), s = Math.sqrt(1 - z * z);
        this.tumbleAxes[k] = [s * Math.cos(a), z, s * Math.sin(a)];
        const siteR = new Map();
        for (let q = 0; q < c.plates.length; q++) {
          const key = k === CHARGE || k === MASS ? c.data.plateSite[q] : q;
          if (!siteR.has(key)) siteR.set(key, rnd());
          c.plateR[q] = siteR.get(key);
        }
      }
    }

    startOf(k, p) {
      const d = p.Spread * this.omniRadius;
      let s;
      if (p.Layout === 0) s = mul(V(this.crystals[k].data.offset, 0), p.Spread / 3);
      else if (p.Layout === 1) { const a = k * Math.PI / 2 + Math.PI / 4; s = [Math.cos(a) * d, 0, Math.sin(a) * d]; }
      else {
        const kk = d / Math.sqrt(3);
        s = [[kk, kk, kk], [kk, -kk, -kk], [-kk, kk, -kk], [-kk, -kk, kk]][k];
      }
      return p.LayoutYaw !== 0 ? rotAxis([0, 1, 0], p.LayoutYaw, s) : s;
    }

    // OmniCombineCore.Frame: the crystal's flight centre and its two turns at t.
    frame(k, t, p) {
      const t0 = Core.departs(k, p), u = clamp01((t - t0) / p.Flight), e = easeIO(u, p.Ease);
      const s = this.startOf(k, p), left = 1 - e;
      const aim = p.ApproachTurn ? smooth(e / APPROACH_TURN_ENDS) : 0;
      const turn = aim > 0 ? (p.ApproachTurn[k] + (p.CameraAzimuth - p.ApproachCameraAzimuth)) * aim : 0;
      let centre = rotAxis([0, 1, 0], p.Swirl * 2 * Math.PI * left + turn, mul(s, left));
      centre = [centre[0], centre[1] + p.Arc * len(s) * Math.sin(Math.PI * e), centre[2]];
      if (aim > 0 && p.ApproachElevation) {
        const az = p.CameraAzimuth, across = [-Math.cos(az), 0, Math.sin(az)];
        centre = rotAxis(across, (p.CameraElevation + p.ApproachAboveCamera - p.ApproachElevation[k]) * aim, centre);
      }
      const tumble = p.Tumble * 2 * Math.PI * left;
      const spin = k === CHARGE && p.IdleTumble ? ((this.tumbleSpeed * Math.min(t, t0)) % (2 * Math.PI)) * (1 - e) : 0;
      return { centre, spin, tumble };
    }

    // OmniCombineCore.FaceCamera.
    faceCamera(p, cameraAzimuth, cameraRadiansPerSecond, now) {
      const turns = [0, 0, 0, 0], elevations = [0, 0, 0, 0];
      for (let k = 1; k < 4; k++) {
        const straight = Object.assign({}, p, { ApproachTurn: null });
        const at = this.frame(k, Core.departs(k, p) + ARRIVAL_AT * p.Flight, straight).centre;
        elevations[k] = Math.asin(Math.max(-1, Math.min(1, at[1] / Math.max(1e-12, len(at)))));
        const s = this.startOf(k, p), arrives = Core.departs(k, p) + ARRIVAL_AT * p.Flight;
        const travel = cameraRadiansPerSecond * (arrives - now);
        turns[k] = wrapAngle(cameraAzimuth + travel - Math.atan2(s[0], s[2])) - travel;
      }
      p.ApproachTurn = turns;
      p.ApproachElevation = elevations;
      p.ApproachCameraAzimuth = cameraAzimuth;
      p.CameraAzimuth = cameraAzimuth;
    }

    crystalFrame(k, t, p) {
      this.seed(p.Seed);
      const f = this.frame(k, t, p);
      return { centre: f.centre, spinAxis: this.tumbleAxis, spin: f.spin, tumbleAxis: this.tumbleAxes[k], tumble: f.tumble };
    }

    spinWeights(t, names) {
      const sp = this.data.spin, per = 1 / sp.cycleSpeed + sp.pause;
      const n = Math.floor(t / per), ph = (t - n * per) * sp.cycleSpeed, five = (n & 1) === 0;
      const w1 = ph < 1 ? clamp01(2 * ph) : 0, w2 = ph < 1 ? clamp01(2 * ph - 1) : 0;
      return names.map((nm) => (nm.startsWith('5Point') === five ? (nm.includes('1st') ? w1 : w2) : 0));
    }

    seatOf(dir, towardCamera) {
      const D = this.data.depth, d = dot(dir, towardCamera), e = clamp01((d - D.open) / (D.seal - D.open));
      return Math.pow(e * e * (3 - 2 * e), D.power);
    }

    flipPose(rig, q, pl, t, salt, fx) {
      const loop = Math.floor(t / this.data.flip.loop);
      return this.flipPoseAt(rig, q, pl, loop, t - loop * this.data.flip.loop, salt, fx);
    }

    flipPoseAt(rig, q, pl, loop, local, salt, fx) {
      const fl = this.data.flip, step = rig.plans[flipStart(rig, loop, salt)].get(q);
      const tk = local - (fl.first + fl.stagger * step.ring);
      if (tk < 0) return null;
      const cv = step.ring === 0 ? fl.lead : fl.later;
      return {
        axis: step.axis,
        angle: Math.PI * curveAt(cv.flip, tk, 0) * fx,
        scale: 1 + (curveAt(cv.scale, tk, 1) - 1) * fx,
        shift: mul(add(mul(step.toward, curveAt(cv.slide, tk, 0)), mul(pl.dir, -curveAt(cv.dip, tk, 0))), pl.reach * fx),
      };
    }

    static applyFlip(f, pl, b) { return add(add(pl.c, f.shift), mul(rotAxis(f.axis, f.angle, sub(b, pl.c)), f.scale)); }

    // OmniCombineCore.Pose, for ONE crystal k (the C# loops all four with the same arguments; the reel drives each
    // crystal on its own clock, so it asks for them one at a time). Writes positions (Float32Array, xyz per vertex)
    // and plateMorph (per plate); returns { effect, centre }.
    poseCrystal(k, t, p, towardCamera, positions, plateMorph) {
      this.seed(p.Seed);
      const c = this.crystals[k], d = c.data, te = t + p.EffectPhase;
      const t0 = Core.departs(k, p), u = clamp01((t - t0) / p.Flight);
      const fxIdle = 1 - smooth((t - t0) / p.EffectFade);
      const socket = k > 0 && p.SocketSpacing > 0 ? Core.socketIn(k, t, p) : 0;
      const effect = Math.max(0, fxIdle);
      const fr = this.frame(k, t, p), centre = fr.centre, spin = fr.spin, ang = fr.tumble;
      const mS = Math.min(p.MorphStart, 1 - p.MorphLength);
      const idleSpin = d.idle && d.idle.keys && d.idle.keys.length > 0 && fxIdle > 0;
      const sw = idleSpin ? this.spinWeights(te, d.idle.names) : null;
      for (let q = 0; q < c.plates.length; q++) {
        const pl = c.plates[q];
        const start = mS + p.SiteSpread * c.plateR[q] * (1 - p.MorphLength - mS);
        const w = smooth((u - start) / p.MorphLength);
        if (plateMorph) plateMorph[q] = w;
        let flip = null, seatScale = 1, seatLift = [0, 0, 0];
        if (fxIdle > 0 && k === TIME) flip = this.flipPose(this.timeRig, q, pl, te, TIME_FLIP_SALT, fxIdle);
        if (fxIdle > 0 && k === MASS && !p.ShaderDepth) {
          const sd = this.seatOf(pl.dir, towardCamera);
          seatScale = 1 + (this.data.depth.massSizeStart - 1) * (1 - sd) * fxIdle;
          seatLift = mul(pl.c, (this.data.depth.massReach - 1) * (1 - sd) * fxIdle);
        }
        const extra = k > 0 && p.SocketSpacing > 0 ? w * p.SocketSpacing * (1 - socket) : 0;
        for (const i of pl.idx) {
          let b = V(d.basis, i);
          if (sw) for (let key = 0; key < sw.length; key++) b = add(b, mul(V(d.idle.keys[key].delta, i), sw[key] * fxIdle));
          if (flip) b = Core.applyFlip(flip, pl, b);
          if (seatScale !== 1) b = add(add(pl.c, mul(sub(b, pl.c), seatScale)), seatLift);
          let v;
          if (k === TIME) {
            const rq = slerpIdentity(c.quat[q], w), cc = lerp3(pl.c, c.finalCentre[q], w);
            v = add(cc, quatRotate(rq, sub(b, pl.c)));
          } else if (k === CHARGE) {
            const s1 = smooth(w / p.Suck), s2 = smooth((w - p.Suck) / (1 - p.Suck));
            const v1 = lerp3(b, add(b, sub(V(d.stage1, i), V(d.basis, i))), s1);
            v = lerp3(rotAxis(c.axis[q], c.twist[q] * s2, v1), V(d.final, i), s2);
          } else if (k === MASS) {
            v = lerp3(rotAxis(c.axis[q], c.twist[q] * w, b), V(d.final, i), w);
          } else {
            v = lerp3(b, V(d.final, i), w);
          }
          if (extra !== 0) v = add(v, mul(c.finalCentre[q], extra));
          if (spin !== 0) v = rotAxis(this.tumbleAxis, spin, v);
          if (ang !== 0) v = rotAxis(this.tumbleAxes[k], ang, v);
          v = add(v, centre);
          positions[3 * i] = v[0]; positions[3 * i + 1] = v[1]; positions[3 * i + 2] = v[2];
        }
      }
      return { effect, centre };
    }

    // OmniCombineCore.OmniPose (the game-motion path and the lab path).
    omniPose(t, p, fx, towardCamera, positions, rest, waveSeconds, wave) {
      const ov = p.GameMotion && rest ? rest : this.data.omni.verts;
      positions.set(ov);
      if (fx <= 0) return;
      const te = t + p.EffectPhase;
      if (!p.GameMotion) {
        const sq = this.data.squares, w = Core.squaresWeights(te, sq);
        sq.keys.forEach((key, k) => {
          const wk = w[k] * fx;
          key.verts.forEach((j, n) => { for (let a = 0; a < 3; a++) positions[3 * j + a] += key.delta[3 * n + a] * wk; });
        });
      }
      this.omniPlates.forEach((pl, q) => {
        const owner = this.data.omni.plateOwner[q];
        if (owner === TIME) {
          if (p.GameMotion && wave < 0) return;
          const f = p.GameMotion ? this.flipPoseAt(this.omniRig, q, pl, wave, waveSeconds, OMNI_FLIP_SALT, fx)
            : this.flipPose(this.omniRig, q, pl, te, OMNI_FLIP_SALT, fx);
          if (!f) return;
          for (const i of pl.idx) { const v = Core.applyFlip(f, pl, V(ov, i)); positions[3 * i] = v[0]; positions[3 * i + 1] = v[1]; positions[3 * i + 2] = v[2]; }
        } else if (owner === MASS && !p.ShaderDepth) {
          const D = this.data.depth, s = this.seatOf(pl.dir, towardCamera), sc = 1 + (D.omniSizeStart - 1) * (1 - s) * fx;
          const lift = mul(pl.c, (D.omniReach - 1) * (1 - s) * fx);
          for (const i of pl.idx) { const v = add(add(pl.c, mul(sub(V(ov, i), pl.c), sc)), lift); positions[3 * i] = v[0]; positions[3 * i + 1] = v[1]; positions[3 * i + 2] = v[2]; }
        }
      });
    }

    static squaresWeights(t, sq) {
      const nk = sq.keys.length, frames = sq.weights.length / nk, fp = ((t * sq.fps) % frames + frames) % frames;
      const i0 = Math.floor(fp), i1 = (i0 + 1) % frames;
      let a = fp - i0;
      for (let k = 0; k < nk; k++) if (Math.abs(sq.weights[i1 * nk + k] - sq.weights[i0 * nk + k]) > 0.5) a = 0;
      const out = [];
      for (let k = 0; k < nk; k++) out.push(sq.weights[i0 * nk + k] + (sq.weights[i1 * nk + k] - sq.weights[i0 * nk + k]) * a);
      return out;
    }

    static departs(k, p) { return p.Lead + k * p.Stagger; }
    static centred(k, p) { return Core.departs(k, p) + p.Flight; }
    static morphEnds(p) { return Core.centred(TIME, p) + Math.max(0, p.SocketSeconds); }
    static socketIn(k, t, p) {
      const since = t - Core.centred(k, p);
      return p.SocketSeconds > 0 ? smooth(since / p.SocketSeconds) : since >= 0 ? 1 : 0;
    }
    static massColumnPlace(t, p) { return 1 - clamp01(p.MassRaise) * (1 - Core.socketIn(MASS, t, p)); }
  }

  // ------------------------------------------------------------------ SquareTurnFit
  function fitTurn(idx, rest, offset, degrees, tolerance) {
    let a = idx[0];
    for (const i of idx) if (dot(offset(i), offset(i)) > dot(offset(a), offset(a))) a = i;
    let best = [0, 0, 0];
    for (const i of idx) { const c = cross(offset(a), offset(i)); if (dot(c, c) > dot(best, best)) best = c; }
    if (dot(best, best) < 1e-16) return { problem: 'a square\'s key does not turn it' };
    const axis = unit(best);
    let bestError = Infinity, sign = 1;
    for (const s of [1, -1]) {
      let error = 0;
      for (const i of idx) error = Math.max(error, len(sub(angleAxis(s * degrees, axis, rest[i]), add(rest[i], offset(i)))));
      if (error < bestError) { bestError = error; sign = s; }
    }
    if (bestError > tolerance) return { problem: 'a square\'s key is not a rigid ' + degrees + '-degree turn (misses by ' + bestError.toPrecision(3) + ')' };
    return { axis, sign };
  }

  function squareTurnFit(corners, rest, centre, pentKey, step1, step2, tolerance) {
    const pent = fitTurn(corners, rest, pentKey, 72, tolerance);
    if (pent.problem) return pent;
    const tri = fitTurn(corners, rest, (i) => add(step1(i), step2(i)), 120, tolerance);
    if (tri.problem) return tri;
    const half = angleAxis(tri.sign * 60, tri.axis, centre);
    let stepCentre = [0, 0, 0];
    for (const i of corners) stepCentre = add(stepCentre, add(rest[i], step1(i)));
    stepCentre = mul(stepCentre, 1 / corners.length);
    const offset = sub(stepCentre, half), outward = unit(half), lift = dot(offset, outward);
    return { centre, pentagonAxis: pent.axis, pentagonSign: pent.sign, triangleAxis: tri.axis, triangleSign: tri.sign, lift, side: sub(offset, mul(outward, lift)) };
  }

  // SquareTurnFit.At: the rigid turn (as a function v -> rotated v) and the shift.
  function squareAt(f, pentagon, e, pentagonHop, triangleLift, triangleSide) {
    const hop = Math.sin(Math.PI * e);
    if (pentagon) {
      const deg = f.pentagonSign * 72 * e;
      return { deg, axis: f.pentagonAxis, shift: mul(unit(angleAxis(deg, f.pentagonAxis, f.centre)), pentagonHop * hop) };
    }
    const deg = f.triangleSign * 120 * e, sideDeg = f.triangleSign * (120 * e - 60);
    const shift = add(mul(unit(angleAxis(deg, f.triangleAxis, f.centre)), triangleLift * f.lift * hop), mul(angleAxis(sideDeg, f.triangleAxis, f.side), triangleSide * hop));
    return { deg, axis: f.triangleAxis, shift };
  }

  // ------------------------------------------------------------------ OmniCombineMotion
  function fitMotion(data, omniRadius, thickness) {
    const ov = data.omni.verts, n = ov.length / 3, thickOffset = new Float32Array(ov.length), rest = [];
    for (let i = 0; i < n; i++) rest.push(V(ov, i));
    const byPlate = new Map();
    for (let i = 0; i < n; i++) { const p = data.omni.plate[i]; if (!byPlate.has(p)) byPlate.set(p, []); byPlate.get(p).push(i); }
    const keys = data.squares && data.squares.keys;
    if (!keys || keys.length < 3) return { problem: 'the combine data carries no square keys' };
    const offsets = keys.slice(0, 3).map((key) => { const m = new Map(); key.verts.forEach((v, j) => m.set(v, [key.delta[3 * j], key.delta[3 * j + 1], key.delta[3 * j + 2]])); return m; });
    const off = (k, i) => offsets[k].get(i) || [0, 0, 0];
    const squares = [], corners = [], tolerance = 1e-3 * omniRadius;
    for (const [plate, idx] of byPlate) {
      const owner = data.omni.plateOwner[plate];
      if (owner === MASS) continue;
      let centre = [0, 0, 0], lo = Infinity, hi = -Infinity;
      for (const i of idx) { centre = add(centre, rest[i]); const r = len(rest[i]); lo = Math.min(lo, r); hi = Math.max(hi, r); }
      centre = mul(centre, 1 / idx.length);
      const mid = (lo + hi) / 2;
      let outer = [0, 0, 0], inner = [0, 0, 0], no = 0, ni = 0;
      for (const i of idx) if (len(rest[i]) > mid) { outer = add(outer, rest[i]); no++; } else { inner = add(inner, rest[i]); ni++; }
      if (no > 0 && ni > 0) {
        const normal = unit(sub(mul(outer, 1 / no), mul(inner, 1 / ni)));
        for (const i of idx) {
          const d = mul(normal, dot(sub(rest[i], centre), normal) * (thickness - 1));
          thickOffset[3 * i] = d[0]; thickOffset[3 * i + 1] = d[1]; thickOffset[3 * i + 2] = d[2];
        }
      }
      if (owner !== SPACE) continue;
      const fit = squareTurnFit(idx, rest, centre, (i) => off(0, i), (i) => off(1, i), (i) => off(2, i), tolerance);
      if (fit.problem) return fit;
      squares.push(fit); corners.push(idx);
    }
    if (!squares.length) return { problem: 'the combine\'s omni has no square plates to turn' };
    return {
      thickOffset, squares, corners,
      swell(verts, swell, into) { for (let i = 0; i < verts.length; i++) into[i] = verts[i] + thickOffset[i] * swell; },
      turnSquares(restVerts, pose, pentagon, e, pentagonHop, triangleLift, triangleSide) {
        squares.forEach((f, sq) => {
          const at = squareAt(f, pentagon, e, pentagonHop, triangleLift, triangleSide);
          for (const i of corners[sq]) {
            const v = add(angleAxis(at.deg, at.axis, V(restVerts, i)), at.shift);
            pose[3 * i] = v[0]; pose[3 * i + 1] = v[1]; pose[3 * i + 2] = v[2];
          }
        });
      },
    };
  }

  // ------------------------------------------------------------------ OmniCadence
  class Cadence {
    constructor(pentagonSeconds, waveSeconds, triangleSeconds, gapSeconds) {
      this.pentagon = Math.max(0.05, pentagonSeconds);
      this.wave = Math.max(0.05, waveSeconds);
      this.triangle = Math.max(0.05, triangleSeconds);
      this.gap = Math.max(0, gapSeconds);
    }
    static of(asset) { return new Cadence(asset.pentagonTurnSeconds, asset.waveActiveSeconds / Math.max(asset.waveSpeed, 0.05), asset.triangleTurnSeconds, asset.gapSeconds); }
    get cycle() { return this.pentagon + this.triangle + 2 * this.wave + 4 * this.gap; }
    waveStart(k) { return k === 0 ? this.pentagon + this.gap : this.pentagon + this.wave + this.triangle + 3 * this.gap; }
    get triangleStart() { return this.pentagon + this.wave + 2 * this.gap; }
    local(seconds) { const c = this.cycle, cycle = Math.floor(seconds / c); return { local: seconds - cycle * c, cycle }; }
    squaresAt(seconds) {
      const { local } = this.local(seconds);
      if (local < this.pentagon) return { turn: 1, progress: local / this.pentagon };
      const s = local - this.triangleStart;
      if (s >= 0 && s < this.triangle) return { turn: 2, progress: s / this.triangle };
      return { turn: 0, progress: 0 };
    }
    waveAt(seconds) {
      const { local, cycle } = this.local(seconds);
      for (let k = 0; k < 2; k++) {
        const s = local - this.waveStart(k);
        if (s >= 0 && s < this.wave) return { secondsIn: s, index: 2 * cycle + k };
      }
      return null;
    }
    static smoother(u) { u = clamp01(u); return u * u * u * (u * (u * 6 - 15) + 10); }
  }

  // ------------------------------------------------------------------ OmniCombineConfigSO.Draw
  // The game draws with System.Random; the reel draws the same ranges from a seeded mulberry32 so a seed names one play.
  function drawParams(config, seed) {
    const rnd = mulberry32(seed ^ 0x2545F491);
    const pick = (r) => r[0] + (r[1] - r[0]) * rnd();
    const fl = pick(config.flight);
    return {
      Seed: Math.floor(rnd() * 2147483647) | 0,
      Lead: pick(config.lead), Layout: config.layout, Spread: pick(config.spread),
      LayoutYaw: pick(config.layoutYawDegrees) * Math.PI / 180,
      Flight: fl, Stagger: fl * pick(config.staggerOfFlight), Ease: config.ease,
      Arc: pick(config.arc), Swirl: pick(config.swirl), Tumble: pick(config.tumble),
      MorphStart: config.morphStart, MorphLength: config.morphLength,
      SiteSpread: pick(config.siteSpread), Suck: pick(config.suck),
      SocketSpacing: pick(config.socketSpacing), SocketSeconds: pick(config.socketSeconds), MassRaise: config.massRaise,
      ApproachAboveCamera: config.approachAboveCameraDegrees * Math.PI / 180,
      EffectFade: config.effectFade, EffectPhase: rnd() * 100, GameMotion: true,
      // OmniCombinePlayer.Begin
      ShaderDepth: true, IdleTumble: false,
      ApproachTurn: null, ApproachElevation: null, ApproachCameraAzimuth: 0, CameraAzimuth: 0, CameraElevation: 0,
    };
  }

  CR.Combine = {
    SPACE, CHARGE, MASS, TIME, ORDER, Core, Cadence, fitMotion, squareTurnFit, squareAt, drawParams,
    maths: { add, sub, mul, dot, cross, len, unit, lerp3, rotAxis, angleAxis, quatRotate, smooth, clamp01, easeIO, hash, mulberry32, wrapAngle },
  };
})(globalThis.CR = globalThis.CR || {});
