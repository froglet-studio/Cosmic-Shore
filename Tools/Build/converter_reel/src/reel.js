// The reel: one seamless loop of the Elemental Converter, for a social video.
//
//   idle -> BUILD (Space, Charge, Mass, Time, as the game plays a CONVERT) -> the omni wakes and holds its four
//   effects for N cadence cycles -> it settles and hands back to the landed crystals -> PULL-OUT, first in first out
//   (Space leaves first, Time last), each crystal's own flight run backwards -> idle -> loop.
//
// Every frame of the build is OmniCombinePlayer.Tick's, through the ported core (combineCore.js). The pull-out is the
// same pose function with each crystal's flight clock running backwards, and the same camera-side approach aimed at
// its departure. Each crystal's idle effects run on their own forward clock that restarts only mid-flight, where the
// effect is fully faded, so the loop point is invisible. Stateful pieces (the depth ratchets, the framing ease) are
// baked over two loops and the second is kept, so the end of the loop flows into its start.
(function (CR) {
  'use strict';
  const { Core, Cadence, fitMotion, drawParams, SPACE, CHARGE, MASS, TIME } = CR.Combine;
  const { smooth, clamp01, wrapAngle } = CR.Combine.maths;
  const CONVERT_DELAY = 0.3;   // OmniCombinePlayer.ConvertDelay

  class Reel {
    // assets: { data (OmniCombineData.json), config (OmniCombineConfig.asset values), cadence (OmniCrystalCadence.asset),
    //           depth (data/depth.json), arcs (data/charge_arcs.json) }
    constructor(assets) {
      this.assets = assets;
      this.core = new Core(assets.data);
      this.R = this.core.omniRadius;
      this.cadence = Cadence.of(assets.cadence);
      this.motion = fitMotion(assets.data, this.R, assets.cadence.plateThickness);
      if (this.motion.problem) throw new Error(this.motion.problem);
      this.omniRest = new Float32Array(assets.data.omni.verts.length);
      this.omniPos = new Float32Array(assets.data.omni.verts.length);
      this.poses = [0, 1, 2, 3].map((k) => new Float32Array(this.core.vertexCount(k) * 3));
      this.morph = [0, 1, 2, 3].map((k) => new Float32Array(this.core.crystals[k].plates.length));
      this.extras = CR.ReelExtras ? new CR.ReelExtras(this) : null;   // the Charge light and the depth prisms
    }

    // ---------------------------------------------------------------- the plan
    // o: { seed, holdCycles, openIdle, closeIdle, pause, orbitTurns, pitch, fov, framing, framingEase, orbitStart,
    //      overrides: {Flight, Arc, ...} } -> fixes every time in the loop.
    plan(o) {
      const cfg = this.assets.config;
      const p = Object.assign(drawParams(cfg, o.seed | 0), o.overrides || {});
      p.Lead = o.openIdle + CONVERT_DELAY;                    // the press lands openIdle into the loop
      const m = Core.morphEnds(p), hand = cfg.handOffSeconds, fade = p.EffectFade;
      const T = {};
      T.press = o.openIdle;
      T.m = m;
      T.awake = m + hand;                                     // the omni is whole; its effects ramp in
      T.beats0 = T.awake + fade;                              // the cadence starts: a whole pentagon turn first
      T.beats1 = T.beats0 + o.holdCycles * this.cadence.cycle;  // the last beat ends (every square and rhombus at rest)
      T.settled = T.beats1 + fade;                            // effects ramped back to rest
      T.back = T.settled + hand;                              // the omni has handed back to the landed crystals
      T.out0 = T.back + o.pause;                              // Space sets off back out
      const socket = (k) => (k > 0 ? Math.max(0, p.SocketSeconds) : 0);
      T.out = [0, 1, 2, 3].map((k) => T.out0 + k * p.Stagger);
      T.home = T.out.map((t, k) => t + socket(k) + p.Flight);  // crystal k is back where it idled
      T.L = Math.max(...T.home) + o.closeIdle;
      // A whole number of frames at 60 and 30 fps, so the last frame flows into the first.
      T.L = Math.ceil(T.L * 60 - 1e-6) / 60;
      // Each crystal's idle clock restarts mid-flight on the way IN (its idle effect is fully faded there).
      T.cut = [0, 1, 2, 3].map((k) => Core.departs(k, p) + p.Flight / 2);

      this.p = p;
      this.o = o;
      this.T = T;
      this.orbitRate = 2 * Math.PI * o.orbitTurns / T.L;      // rad/s, so the camera comes round in whole turns
      // The build aims each crystal at the camera from the press (OmniCombinePlayer.Convert -> FaceCamera).
      p.CameraElevation = o.pitch * Math.PI / 180;
      this.core.faceCamera(p, this.camAz(T.press), this.orbitRate, T.press);
      // The pull-out: the same aim, at the moment each crystal passes the arrival point on its way OUT.
      const pu = Object.assign({}, p);
      const turns = [0, 0, 0, 0];
      for (let k = 1; k < 4; k++) {
        const passes = T.out[k] + socket(k) + (1 - 0.85) * p.Flight;
        const travel = this.orbitRate * (passes - T.out0);
        const s = this.core.startOf(k, p);
        turns[k] = wrapAngle(this.camAz(T.out0) + travel - Math.atan2(s[0], s[2])) - travel;
      }
      pu.ApproachTurn = turns;
      pu.ApproachCameraAzimuth = this.camAz(T.out0);
      this.pu = pu;
      // The framing counts the raised Mass prisms, whose columns are stepped against the camera the framing places:
      // frame without them, bake the columns, then frame again with them.
      if (this.extras) { this.extras.massFar = null; this.extras.triFar = null; this.extras.massState = null; this.extras.triBodyTable = null; }
      this.bakeFraming();
      if (this.extras) { this.extras.plan(); this.bakeFraming(); }
      return T;
    }

    // ---------------------------------------------------------------- the camera (ElementalConverterView.LateUpdate)
    orbitDeg(tau) { return (this.o.orbitStart || 0) + this.orbitRate * tau * 180 / Math.PI; }
    camDir(tau) {
      const ph = this.orbitDeg(tau) * Math.PI / 180, pi = this.o.pitch * Math.PI / 180;
      return [-Math.cos(pi) * Math.sin(ph), Math.sin(pi), -Math.cos(pi) * Math.cos(ph)];
    }
    // Unwrapped atan2(x, z) of the direction toward the camera: it counts on past whole turns.
    camAz(tau) { return this.orbitDeg(tau) * Math.PI / 180 + Math.PI; }

    // ---------------------------------------------------------------- each crystal's clock
    // Which pose time and idle-effect clock crystal k is at, at loop time tau.
    clockOf(k, tau) {
      const p = this.p, T = this.T;
      const off = this.p.EffectPhase + 17.31 * k;
      let idle = tau - T.cut[k];
      if (idle < 0) idle += T.L;
      idle += off;
      if (this.naiveClock) idle = tau + off;   // the scorecard's control: one clock that restarts with the loop
      if (tau < T.out[k]) return { t: Math.min(tau, T.m), params: p, idle, out: false };
      const socket = k > 0 ? Math.max(0, p.SocketSeconds) : 0;
      const lambda = p.Flight + socket - (tau - T.out[k]);   // seconds since setting off, run backwards
      return { t: Core.departs(k, p) + lambda, params: this.pu, idle, out: true };
    }

    // Crystal k's pose at tau into this.poses[k]; returns { effect, centre, t, params, socketPlace }.
    poseCrystal(k, tau, camDir) {
      const c = this.clockOf(k, tau), params = Object.assign({}, c.params);
      params.CameraAzimuth = this.camAz(tau);
      params.CameraElevation = this.o.pitch * Math.PI / 180;
      params.EffectPhase = c.idle - c.t;   // the idle effects read t + EffectPhase: make that this crystal's own clock
      const r = this.core.poseCrystal(k, c.t, params, camDir, this.poses[k], this.morph[k]);
      r.t = c.t; r.params = params; r.idle = c.idle;
      if (k === MASS) r.columnPlace = Core.massColumnPlace(c.t, params);
      return r;
    }

    // ---------------------------------------------------------------- what is on stage at tau
    phase(tau) {
      const T = this.T, hand = this.assets.config.handOffSeconds, fade = this.p.EffectFade;
      const crystals = tau < T.awake || tau >= T.settled;
      const omni = tau >= T.m && tau < T.back;
      let appear = 0, fx = 0, beats = -1;
      if (omni) {
        appear = tau < T.settled ? smooth((tau - T.m) / hand) : 1 - smooth((tau - T.settled) / hand);
        fx = tau < T.beats1 ? smooth((tau - T.awake) / fade) : 1 - smooth((tau - T.beats1) / fade);
        if (tau >= T.beats0 && tau < T.beats1) beats = tau - T.beats0;
      }
      return { crystals, omni, appear, fx, beats, fluxSeconds: tau - T.awake };
    }

    // The omni's pose (OmniCombinePlayer.PoseOmni) into this.omniPos.
    poseOmni(tau, fx, beats, camDir) {
      const cad = this.cadence, asset = this.assets.cadence, data = this.assets.data;
      this.motion.swell(data.omni.verts, fx, this.omniRest);
      let turn = 0, progress = 0, waveSeconds = -1, wave = -1;
      if (beats >= 0) {
        const s = cad.squaresAt(beats); turn = s.turn; progress = s.progress;
        const w = cad.waveAt(beats);
        if (w) { wave = w.index; waveSeconds = data.flip.first + w.secondsIn * asset.waveSpeed; }
      }
      const params = Object.assign({}, this.p, { CameraAzimuth: this.camAz(tau) });
      this.core.omniPose(tau, params, fx, camDir, this.omniPos, this.omniRest, waveSeconds, wave);
      if (turn) this.motion.turnSquares(this.omniRest, this.omniPos, turn === 1, Cadence.smoother(progress),
        asset.pentagonLift * this.R, asset.triangleLift, asset.triangleSide);
    }

    // Everything the renderer draws at loop time tau (core units; the stage centre is the origin).
    frame(tau, dt) {
      tau = ((tau % this.T.L) + this.T.L) % this.T.L;
      const camDir = this.camDir(tau), ph = this.phase(tau), out = { tau, camDir, phase: ph, crystals: null, omni: null };
      if (ph.crystals) {
        out.crystals = [0, 1, 2, 3].map((k) => this.poseCrystal(k, tau, camDir));
      }
      if (ph.omni) {
        this.poseOmni(tau, ph.fx, ph.beats, camDir);
        out.omni = { pos: this.omniPos, appear: ph.appear, fx: ph.fx };
      }
      out.camera = this.cameraAt(tau);
      if (this.extras) this.extras.frame(out, dt);
      return out;
    }

    // ---------------------------------------------------------------- framing
    // ElementalConverterView frames a SPHERE: distance = framing * stageRadius / sin(fov/2), the stage radius being the
    // farthest posed vertex (and raised Mass prism), eased over framingEaseSeconds. That rule is mode 0 (height only, the
    // game) and mode 1 (the narrower of height and width). Mode 2 (lab) fits what is on stage as the camera actually sees
    // it, in both axes of the frame, and looks ahead so the camera pulls back BEFORE something would leave the frame.
    points(tau, add) {
      const camDir = this.camDir(tau), ph = this.phase(tau), X = this.extras;
      if (ph.crystals) {
        for (let k = 0; k < 4; k++) {
          const r = this.poseCrystal(k, tau, camDir), P = this.poses[k];
          for (let i = 0; i < P.length; i += 3) add(P[i], P[i + 1], P[i + 2], 0);
          if (k === CR.Combine.MASS && X && X.massState) {
            const c = X.massColumnsAt(r), st = X.massState, base = X.index(tau) * 4 * X.massNp;
            c.plates.forEach((q, j) => { const s = c.shape.reach + (1 - c.shape.reach) * st[base + 4 * j]; add(r.centre[0] + q[0] * s, r.centre[1] + q[1] * s, r.centre[2] + q[2] * s, 0.15 * this.R); });
          }
        }
      }
      if (ph.omni) {
        this.poseOmni(tau, ph.fx, ph.beats, camDir);
        const P = this.omniPos;
        for (let i = 0; i < P.length; i += 3) add(P[i], P[i + 1], P[i + 2], 0);
        if (this.o.frameRaised && X && X.triBodyTable) for (const b of X.triBodies(tau)) add(b[0], b[1], b[2], b[3] * ph.appear);
      }
    }

    requiredDistance(tau) {
      const o = this.o, mode = o.framingMode == null ? 0 : o.framingMode, aspect = o.aspect || 1;
      const half = o.fov * 0.5 * Math.PI / 180, tanV = Math.tan(half), tanH = tanV * aspect;
      if (mode < 2) {
        let r = 0;
        this.points(tau, (x, y, z, s) => { r = Math.max(r, Math.hypot(x, y, z) + s); });
        const h = mode === 1 && aspect < 1 ? Math.atan(tanH) : half;
        return o.framing * Math.max(r, 1e-3) / Math.sin(h);
      }
      const dir = this.camDir(tau), up0 = [0, 1, 0];
      let right = [up0[1] * dir[2] - up0[2] * dir[1], up0[2] * dir[0] - up0[0] * dir[2], up0[0] * dir[1] - up0[1] * dir[0]];
      const rl = Math.hypot(...right); right = right.map((v) => v / rl);
      const up = [dir[1] * right[2] - dir[2] * right[1], dir[2] * right[0] - dir[0] * right[2], dir[0] * right[1] - dir[1] * right[0]];
      const m = 1 / o.framing;
      let d = 0;
      this.points(tau, (x, y, z, s) => {
        const depth = x * dir[0] + y * dir[1] + z * dir[2];
        const sx = Math.abs(x * right[0] + y * right[1] + z * right[2]) + s, sy = Math.abs(x * up[0] + y * up[1] + z * up[2]) + s;
        d = Math.max(d, depth + s + sx / (tanH * m), depth + s + sy / (tanV * m));
      });
      return Math.max(d, 2 * this.R);
    }

    bakeFraming() {
      const hz = 30, n = Math.round(this.T.L * hz), want = new Float32Array(n);
      for (let i = 0; i < n; i++) want[i] = this.requiredDistance(i / hz);
      const ease = this.o.framingEase, dt = 1 / hz, a = 1 - Math.exp(-dt / ease);
      // Look ahead (tight framing only): hold the farthest the next 1.5 ease-times will need, so the eased camera is
      // already back when a crystal swings wide.
      let target = want;
      if (this.o.framingMode === 2) {
        const k = Math.ceil(1.5 * ease * hz);
        target = new Float32Array(n);
        for (let i = 0; i < n; i++) { let m = 0; for (let j = 0; j <= k; j++) m = Math.max(m, want[(i + j) % n]); target[i] = m; }
      }
      const dist = new Float32Array(n);
      let f = target[0];
      for (let pass = 0; pass < 2; pass++) for (let i = 0; i < n; i++) { f += (target[i] - f) * a; if (pass === 1) dist[i] = f; }
      this.framingTable = { hz, dist };
    }

    cameraAt(tau) {
      const t = this.framingTable, x = tau * t.hz, i0 = Math.floor(x) % t.dist.length, i1 = (i0 + 1) % t.dist.length, a = x - Math.floor(x);
      const distance = t.dist[i0] + (t.dist[i1] - t.dist[i0]) * a;
      const dir = this.camDir(tau);
      return { eye: [dir[0] * distance, dir[1] * distance, dir[2] * distance], target: [0, 0, 0], fov: this.o.fov, distance };
    }
  }

  CR.Reel = Reel;
})(globalThis.CR = globalThis.CR || {});
