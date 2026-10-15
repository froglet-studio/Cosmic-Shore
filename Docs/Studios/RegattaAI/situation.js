/*
 * situation.js - what a pilot was facing, doing and trying to do, moment by moment (Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md).
 *
 * Yash, 2026-10-15: "we need to also record the situations: what target the player was aiming at, where the ring or the
 * target is, where the obstacles are, how we skim the obstacles, how quickly we change direction, how we ignore some
 * skims when we must keep speed... This is how we can actually train the AI, not just by input."
 *
 * Pure maths over plain arrays (no Three.js), so the Training AI for Regatta page, Node (the golden test) and its Python
 * twin (Tools/Build/situation_features.py) compute the same numbers. The definitions are in
 * Docs/AI_SYSTEM/situation_features.json; change both together.
 *
 *   RegattaSituation.normalize(flight)              flight/2 (Unity) or flight/3 (Unity or this page) -> columns
 *   RegattaSituation.courseFrom(REGATTA_COURSE, i)  the generated course of intensity i -> { gates, lanes, laneDomains }
 *   RegattaSituation.analyze(flight, course)        -> { norm, feats, labels, segments, legs, skims, boosts, mistakes, summary }
 *   RegattaSituation.learn([analysis, ...])         -> { commit, skim, boostWithin } learned from the GOOD moments
 */
(function (root) {
  'use strict';
  var VERSION = 1;
  var DEG = 180 / Math.PI;
  var PRISM_HALF = 3;          // a rail prism's half-width (6 x 6 x 8 u)
  var LEAD_CAP = 480, LEAD_SHARE = 0.4, THROUGH = 320;   // GateRaceController's shipped approach and through points

  // ---------- small vector helpers (arrays of 3) ----------
  function sub(a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; }
  function add(a, b) { return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]; }
  function mul(a, s) { return [a[0] * s, a[1] * s, a[2] * s]; }
  function dot(a, b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
  function cross(a, b) { return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]; }
  function len(a) { return Math.sqrt(dot(a, a)); }
  function norm(a) { var l = len(a); return l > 1e-9 ? mul(a, 1 / l) : [0, 0, 0]; }
  function dist(a, b) { return len(sub(a, b)); }
  function clamp(x, lo, hi) { return x < lo ? lo : x > hi ? hi : x; }
  function angle(a, b) { var la = len(a), lb = len(b); if (la < 1e-9 || lb < 1e-9) return 0; return Math.acos(clamp(dot(a, b) / (la * lb), -1, 1)) * DEG; }
  function r2(x) { return Math.round(x * 100) / 100; }
  function pct(arr, q) { if (!arr.length) return null; var s = arr.slice().sort(function (a, b) { return a - b; }); return s[Math.min(s.length - 1, Math.max(0, Math.round(q * (s.length - 1))))]; }

  // ---------- the course ----------
  function courseFrom(COURSE, intensity) {
    var src = COURSE[intensity];
    if (!src) return null;
    var lanes = src.lanes.map(function (flat) { var pts = []; for (var k = 0; k + 2 < flat.length; k += 3) pts.push([flat[k], flat[k + 1], flat[k + 2]]); return pts; });
    return { intensity: intensity, gates: src.gates.map(function (g) { return { p: g.p.slice(), a: norm(g.a), r: g.r }; }), lanes: lanes, laneDomains: (COURSE.laneDomains || []).slice() };
  }

  // ---------- a flight -> columns ----------
  // flight/2 (Unity HumanFlightRecorder): columns t,x,y,z,fx,fy,fz,ux,uy,uz,speed,boost,xSum,ySum,xDiff,yDiff,throttle,rings
  // flight/3 adds lt, rt, skims, rams, held (when the recorder knows them) and an aligned "rivals" array.
  function normalize(f) {
    var cols = f.columns || [], idx = {};
    cols.forEach(function (c, i) { idx[c] = i; });
    var rows = f.samples || [], n = rows.length;
    function col(name, dflt) { var k = idx[name]; return rows.map(function (r) { return k == null || r[k] == null ? dflt : +r[k]; }); }
    var N = {
      schema: f.schema, source: f.source || 'unity', mode: f.mode, hull: f.hull, hullKey: (f.hullKey || f.hull || '').toLowerCase(),
      verb: f.verb || ((f.hull || '').toLowerCase() === 'squirrel' ? 'skim' : ''), domain: (f.domain || '').toLowerCase(),
      intensity: +f.intensity || 1, hz: +f.sampleHz || 10, turn: f.turn || null, skimReach: f.skimReach != null ? +f.skimReach : ((f.hull || '').toLowerCase() === 'squirrel' ? 7.5 : 0),
      raceTarget: +f.raceTarget || 0, finished: !!f.finished, overrides: f.overrides || [],
      n: n, t: col('t', 0), spd: col('speed', 0), boost: col('boost', 1), xs: col('xSum', 0), ys: col('ySum', 0), xd: col('xDiff', 0), yd: col('yDiff', 0),
      thr: col('throttle', 1), lt: col('lt', 0), rt: col('rt', 0), rings: col('rings', 0), held: col('held', 0),
      skims: idx.skims != null ? col('skims', 0) : null, rams: idx.rams != null ? col('rams', 0) : null,
      p: [], f: [], u: [], rv: f.rivals || null, rivalHulls: f.rivalHulls || [],
    };
    for (var i = 0; i < n; i++) {
      var r = rows[i];
      N.p.push([+r[idx.x], +r[idx.y], +r[idx.z]]);
      N.f.push(norm([+r[idx.fx], +r[idx.fy], +r[idx.fz]]));
      N.u.push(norm([+r[idx.ux], +r[idx.uy], +r[idx.uz]]));
    }
    return N;
  }

  // ---------- the hull's turn: (scaler + speed x RotationThrottleScaler) degrees a second ----------
  function omegaFn(N, turnRates) {
    if (N.turn && N.turn.p != null) { var t = N.turn; return function (v) { return Math.min(t.p, t.y) + v * (t.rts || 0); }; }
    var hi = pct(turnRates.map(Math.abs), 0.95) || 60;   // no hull numbers (a Unity flight): what this pilot actually reached
    hi = Math.max(30, hi);
    return function () { return hi; };
  }

  // ---------- per-sample features (situation_features.json "features") ----------
  function features(N, C) {
    var G = C.gates, ng = G.length, n = N.n, dt = 1 / N.hz, out = new Array(n);
    var own = N.domain ? C.laneDomains.indexOf(N.domain) : -1;
    var turnRates = new Array(n);
    for (var i = 0; i < n; i++) {
      var a = N.f[Math.max(0, i - 1)], b = N.f[Math.min(n - 1, i + 1)], span = (Math.min(n - 1, i + 1) - Math.max(0, i - 1)) * dt;
      turnRates[i] = span > 0 ? angle(a, b) / span : 0;
    }
    var omega = omegaFn(N, turnRates);
    for (i = 0; i < n; i++) {
      var p = N.p[i], f = N.f[i], u = N.u[i], right = norm(cross(f, u)), v = Math.max(1, N.spd[i]);
      var ring = N.rings[i] % ng, g = G[ring], g2 = G[(ring + 1) % ng], g0 = G[(ring - 1 + ng) % ng];
      var rel = sub(g.p, p), d = len(rel), fz = dot(rel, f);
      var side = dot(sub(p, g.p), g.a) >= 0 ? 1 : -1, inDir = mul(g.a, -side);
      var fa = dot(f, g.a), mouthOffset = -1, threads = false;
      if (Math.abs(fa) > 1e-4) {
        var tHit = -dot(sub(p, g.p), g.a) / fa;
        if (tHit > 0) { var hit = add(p, mul(f, tHit)), off = sub(hit, g.p); mouthOffset = len(sub(off, mul(g.a, dot(off, g.a)))); threads = mouthOffset <= g.r; }
      }
      var legLen = dist(g0.p, g.p), lead = Math.min(LEAD_CAP, LEAD_SHARE * legLen);
      var headingErr = angle(f, rel), w = omega(N.spd[i]) / DEG, rMin = v / Math.max(1e-3, w);
      var rNeed = headingErr > 1 ? d / (2 * Math.sin(Math.min(Math.PI / 2, headingErr / DEG))) : 1e6;
      // the rails around: the nearest prism (any lane), the nearest ahead, and the cheapest skim on the way
      var gap = 1e6, gapOwn = false, aheadN = 0, aheadGap = 1e6, aheadBearing = 0, skimDetour = 1e6, skimT = 1e6, skimPt = null, skimLane = -1;
      var reach = N.skimReach, horizon = Math.max(60, v * 2);
      for (var l = 0; l < C.lanes.length; l++) {
        var pts = C.lanes[l];
        for (var k = 0; k < pts.length; k++) {
          var q = pts[k], dx = q[0] - p[0], dy = q[1] - p[1], dz = q[2] - p[2];
          if (Math.abs(dx) > horizon + 10 || Math.abs(dy) > horizon + 10 || Math.abs(dz) > horizon + 10) continue;
          var dq = [dx, dy, dz], dd = len(dq), gq = dd - PRISM_HALF;
          if (gq < gap) { gap = gq; gapOwn = l === own; }
          var fwdq = dot(dq, f);
          if (fwdq <= 0) continue;
          var ang = angle(f, dq);
          if (ang < 30 && dd < 160) { aheadN++; if (gq < aheadGap) { aheadGap = gq; aheadBearing = Math.atan2(dot(dq, right), fwdq) * DEG; } }
          if (reach > 0 && ang < 35 && dd > 15 && dd < horizon) {
            // a skim point: beside the prism by the skim reach, on the side the pilot is already on
            var det = dd + dist(q, g.p) - d;
            if (det < skimDetour) { skimDetour = det; skimT = dd / v; skimPt = q; skimLane = l; }
          }
        }
      }
      var place = 1, rv = N.rv && N.rv[i];
      if (rv) for (var j = 0; j + 3 < rv.length; j += 4) if (rv[j + 3] > N.rings[i]) place++;
      out[i] = {
        t: N.t[i], ring: ring, dist: d, bearing: Math.atan2(dot(rel, right), fz) * DEG, elevation: Math.atan2(dot(rel, u), Math.sqrt(fz * fz + Math.pow(dot(rel, right), 2))) * DEG,
        headingErr: headingErr, axisAngle: angle(f, inDir), mouthOffset: mouthOffset, threadsIfStraight: threads, mouthRadius: g.r,
        timeToMouth: d / v, legLen: legLen, legTurn: angle(sub(g.p, g0.p), sub(g2.p, g.p)), nextLeg: dist(g.p, g2.p),
        approachLead: lead, rMin: rMin, rNeed: rNeed, canTurn: rNeed >= rMin,
        turnRate: turnRates[i], turnJerk: 0, speed: N.spd[i], throttle: N.thr[i], boost: N.boost[i], lt: N.lt[i],
        gap: gap, gapOwnRail: gapOwn, aheadPrisms: aheadN, aheadGap: aheadGap, aheadBearing: aheadBearing,
        skimInReach: reach > 0 && gap < reach, skimDetour: skimDetour, skimTime: skimT, skimPoint: skimPt, skimLane: skimLane,
        lap: Math.floor(N.rings[i] / ng) + 1, ringsLeft: N.raceTarget ? N.raceTarget - N.rings[i] : null, place: rv ? place : null,
        side: side, held: N.held[i] > 0.5,
      };
    }
    for (i = 0; i < n; i++) out[i].turnJerk = i > 0 ? (out[i].turnRate - out[i - 1].turnRate) * N.hz : 0;
    return out;
  }

  // ---------- events: rings, missed mouths, rams, skims, boost presses ----------
  function events(N, C, F) {
    var G = C.gates, ng = G.length, n = N.n, ev = { ring: [], miss: [], ram: [], skim: [], boostPress: [] };
    var skimHull = N.skimReach > 0;
    for (var i = 1; i < n; i++) {
      if (N.rings[i] > N.rings[i - 1]) ev.ring.push(i);
      else {
        var g = G[N.rings[i - 1] % ng], s0 = dot(sub(N.p[i - 1], g.p), g.a), s1 = dot(sub(N.p[i], g.p), g.a);
        if (s0 * s1 < 0) {
          var t = s0 / (s0 - s1), hit = add(N.p[i - 1], mul(sub(N.p[i], N.p[i - 1]), t)), off = sub(hit, g.p), lat = len(sub(off, mul(g.a, dot(off, g.a))));
          if (lat > g.r && lat < g.r * 3) ev.miss.push(i);   // crossed the ring's plane beside the mouth
        }
      }
      if (N.rams ? N.rams[i] > N.rams[i - 1] : N.spd[i] < N.spd[i - 1] * 0.65) ev.ram.push(i);
      if (skimHull && (N.skims ? N.skims[i] > N.skims[i - 1] : N.boost[i] > N.boost[i - 1] + 0.04)) ev.skim.push(i);
      var on = N.lt[i] > 0.5 || (!skimHull && N.boost[i] > 1.05), was = N.lt[i - 1] > 0.5 || (!skimHull && N.boost[i - 1] > 1.05);
      if (on && !was) ev.boostPress.push(i);
    }
    return ev;
  }

  // ---------- what the pilot was trying to do ----------
  function candidates(N, C, F, i) {
    var x = F[i], G = C.gates, ng = G.length, g = G[x.ring], inDir = mul(g.a, -x.side), list = [
      { kind: 'mouth', at: g.p },
      { kind: 'approach', at: add(g.p, mul(g.a, x.side * x.approachLead)) },
      { kind: 'through', at: add(g.p, mul(inDir, THROUGH)) },
      { kind: 'next ring', at: G[(x.ring + 1) % ng].p },
    ];
    if (x.skimPoint && x.skimDetour < 40) list.push({ kind: 'skim', at: x.skimPoint });
    var rv = N.rv && N.rv[i];
    if (rv) { var best = null, bd = 150; for (var j = 0; j + 3 < rv.length; j += 4) { var q = [rv[j], rv[j + 1], rv[j + 2]], d = dist(q, N.p[i]); if (d < bd) { bd = d; best = q; } } if (best) list.push({ kind: 'rival', at: best }); }
    return list;
  }
  function intent(N, C, F, i) {
    var K = Math.max(2, Math.round(0.8 * N.hz)), cands = candidates(N, C, F, i), scores = [];
    for (var c = 0; c < cands.length; c++) {
      var s = 0, m = 0;
      for (var k = 1; k <= K && i + k < N.n; k++) { s += angle(N.f[i + k], sub(cands[c].at, N.p[i + k])); m++; }
      scores.push(m ? s / m : 180);
    }
    var order = scores.map(function (s, c) { return c; }).sort(function (a, b) { return scores[a] - scores[b]; });
    var best = order[0], second = order.length > 1 ? scores[order[1]] : 180;
    return { target: cands[best].kind, at: cands[best].at, err: scores[best], conf: clamp((second - scores[best]) / Math.max(1, second), 0, 1) };
  }

  var PHASES = ['cruise', 'approach', 'line-up', 'thread', 'exit', 'skim run', 'boost straight', 'recovery'];
  function labels(N, C, F, ev) {
    var n = N.n, hz = N.hz, L = new Array(n), near = function (list, i, before, after) {
      for (var k = 0; k < list.length; k++) { var e = list[k]; if (e - i >= -after * hz && e - i <= before * hz) return true; } return false;
    };
    var recovering = false, lastRing = -1;
    for (var i = 0; i < n; i++) {
      var x = F[i], it = intent(N, C, F, i), ph;
      if (ev.miss.indexOf(i) >= 0) recovering = true;
      if (ev.ring.indexOf(i) >= 0) { recovering = false; lastRing = i; }
      if (recovering && x.headingErr < 25) recovering = false;
      var boosting = N.lt[i] > 0.5 || (N.skimReach === 0 && N.boost[i] > 1.05);
      if (recovering) ph = 'recovery';
      else if (near(ev.ring, i, 1.0, 0)) ph = 'thread';
      else if (lastRing >= 0 && i - lastRing <= 0.8 * hz) ph = 'exit';
      else if (near(ev.skim, i, 0.5, 0.5) || it.target === 'skim') ph = 'skim run';
      else if (x.timeToMouth < 4 && x.turnRate > 25) ph = 'line-up';
      else if (boosting && x.turnRate < 12) ph = 'boost straight';
      else if (x.headingErr < 30 || it.target === 'approach' || it.target === 'mouth') ph = 'approach';
      else ph = 'cruise';
      L[i] = { phase: ph, target: it.target, targetAt: it.at, targetErr: it.err, conf: it.conf, corrected: false };
    }
    (N.overrides || []).forEach(function (o) {   // a person's correction beats the guess
      for (var i = 0; i < n; i++) if (N.t[i] >= o.t0 && N.t[i] <= o.t1) { if (o.phase) L[i].phase = o.phase; if (o.target) L[i].target = o.target; L[i].corrected = true; }
    });
    return L;
  }

  function segments(N, F, L, ev) {
    var out = [], s = 0;
    for (var i = 1; i <= N.n; i++) {
      if (i < N.n && L[i].phase === L[s].phase) continue;
      var e = i - 1, threaded = false, missed = false;
      for (var k = 0; k < ev.ring.length; k++) if (ev.ring[k] >= s && ev.ring[k] <= Math.min(N.n - 1, e + 1)) threaded = true;
      for (k = 0; k < ev.miss.length; k++) if (ev.miss[k] >= s && ev.miss[k] <= e) missed = true;
      out.push({ phase: L[s].phase, i0: s, i1: e, t0: r2(N.t[s]), t1: r2(N.t[e]), dur: r2(N.t[e] - N.t[s] + 1 / N.hz), speedIn: Math.round(N.spd[s]), speedOut: Math.round(N.spd[e]), threaded: threaded, missed: missed });
      s = i;
    }
    return out;
  }

  // one leg = from one ring threaded to the next; the commit point is where the last line-up / thread run began
  function legs(N, C, F, L, ev) {
    var out = [], start = 0;
    for (var k = 0; k < ev.ring.length; k++) {
      var j = ev.ring[k], c = j - 1;
      // the commit point: where the pilot stopped going for the approach point (beside the ring, on its axis) and went
      // for the ring itself - the same meaning as the AI's aiCommitDistance. No approach in the leg: nothing to learn.
      while (c > start && L[c].target !== 'approach') c--;
      var commitDist = c > start && c < j - 1 ? Math.round(F[c + 1].dist) : null;
      var missed = ev.miss.some(function (m) { return m > start && m <= j; }), rams = ev.ram.filter(function (m) { return m > start && m <= j; }).length;
      out.push({ ring: N.rings[j - 1] % C.gates.length, i0: start, i1: j, t0: r2(N.t[start]), time: r2(N.t[j] - N.t[start]), speedIn: Math.round(N.spd[start]), speedOut: Math.round(N.spd[j]),
        legLen: Math.round(F[j - 1].legLen), legTurn: Math.round(F[j - 1].legTurn), commitDist: commitDist, clean: !missed && rams === 0 && !F.slice(start, j).some(function (x) { return x.held; }), missed: missed, rams: rams });
      start = j;
    }
    return out;
  }

  // skims taken, and skims SKIPPED (cheap and close, but the pilot held the line), with what each would have cost
  function skimChoices(N, F, ev) {
    if (!(N.skimReach > 0)) return [];
    var out = [], hz = N.hz, last = -1e9;
    // a skim RUN is one choice: the first touch after 1.5 s without one, judged from half a second before it
    var prev = -1e9;
    ev.skim.forEach(function (i) {
      var gapT = N.t[i] - prev; prev = N.t[i]; if (gapT < 1.5) return;
      var b = Math.max(0, i - Math.round(0.5 * hz)), x = F[b];
      out.push({ i: b, t: r2(N.t[b]), taken: true, detour: r2(Math.min(x.skimDetour, 99)), timeToMouth: r2(x.timeToMouth), headingErr: r2(x.headingErr), speed: Math.round(x.speed) });
    });
    for (var i = 0; i < N.n; i++) {
      var x = F[i];
      if (x.held || x.skimDetour > 12 || x.skimTime > 2 || N.t[i] - last < 1.5) continue;
      var took = ev.skim.some(function (e) { return e > i && e <= i + 2 * hz; });
      if (took) continue;
      last = N.t[i];
      out.push({ i: i, t: r2(N.t[i]), taken: false, detour: r2(x.skimDetour), timeToMouth: r2(x.timeToMouth), headingErr: r2(x.headingErr), speed: Math.round(x.speed) });
    }
    return out.sort(function (a, b) { return a.t - b.t; });
  }

  function analyze(flight, course) {
    var N = flight.columns ? normalize(flight) : flight, C = course;
    var F = features(N, C), ev = events(N, C, F), L = labels(N, C, F, ev), segs = segments(N, F, L, ev), lg = legs(N, C, F, L, ev), sk = skimChoices(N, F, ev);
    var boosts = ev.boostPress.map(function (i) { var x = F[i]; return { i: i, t: r2(N.t[i]), headingErr: r2(x.headingErr), dist: Math.round(x.dist), timeToMouth: r2(x.timeToMouth) }; });
    var mistakes = ev.miss.map(function (i) { return { i: i, t: r2(N.t[i]), kind: 'missed mouth' }; }).concat(ev.ram.map(function (i) { return { i: i, t: r2(N.t[i]), kind: 'rail hit' }; }))
      .sort(function (a, b) { return a.t - b.t; });
    var share = {}; PHASES.forEach(function (p) { share[p] = 0; }); L.forEach(function (l) { share[l.phase] = (share[l.phase] || 0) + 1; });
    Object.keys(share).forEach(function (p) { share[p] = r2(share[p] / Math.max(1, N.n)); });
    return { version: VERSION, norm: N, feats: F, labels: L, events: ev, segments: segs, legs: lg, skims: sk, boosts: boosts, mistakes: mistakes,
      summary: { samples: N.n, seconds: r2(N.n / N.hz), rings: ev.ring.length, missed: ev.miss.length, rams: ev.ram.length, skimsTaken: sk.filter(function (s) { return s.taken; }).length,
        skimsSkipped: sk.filter(function (s) { return !s.taken; }).length, boostPresses: boosts.length, meanTurnRate: r2(F.reduce(function (a, x) { return a + x.turnRate; }, 0) / Math.max(1, N.n)), phaseShare: share } };
  }

  // ---------- learning, from the GOOD moments only (step 1 curves, step 2 rules) ----------
  function learn(analyses, opts) {
    var o = opts || {}, pts = [], sk = [], boostErr = [];
    analyses.forEach(function (A) {
      var good = A.legs.filter(function (g) { return g.clean && (o.goodLeg ? o.goodLeg(g, A) : true); });
      good.forEach(function (g) { if (g.legLen > 0 && g.commitDist != null && g.commitDist > 0) pts.push([g.legLen, g.commitDist]); });
      A.skims.forEach(function (s) { sk.push(s); });
      A.boosts.forEach(function (b) { boostErr.push(b.headingErr); });
      A.feats.forEach(function (x, i) { if (x.lt > 0.5 && !x.held) boostErr.push(x.headingErr); });
    });
    // step 1: the commit distance as a curve over the leg's length (least squares, clamped)
    var commit = null;
    if (pts.length >= 3) {
      var mx = 0, my = 0; pts.forEach(function (q) { mx += q[0]; my += q[1]; }); mx /= pts.length; my /= pts.length;
      var sxy = 0, sxx = 0, syy = 0; pts.forEach(function (q) { sxy += (q[0] - mx) * (q[1] - my); sxx += (q[0] - mx) * (q[0] - mx); syy += (q[1] - my) * (q[1] - my); });
      var b = sxx > 1e-6 ? clamp(sxy / sxx, -0.5, 1) : 0, a = clamp(my - b * mx, 60, 800);
      var fit = sxx > 0 && syy > 0 ? sxy * sxy / (sxx * syy) : 0, med = pct(pts.map(function (q) { return q[1]; }), 0.5);
      // the leg explains under a fifth of it: a curve would be noise, so the median, the same on every leg
      if (fit < 0.2) { a = clamp(med, 60, 800); b = 0; }
      commit = { a: Math.round(a), b: r2(b), n: pts.length, r2: r2(fit), median: Math.round(med) };
    }
    // step 2: when to take a skim and when to hold the line - the rule (detour <= D and time to the mouth >= T) that
    // agrees with the most of the pilot's own choices
    var skim = null;
    if (sk.length >= 4 && sk.some(function (s) { return s.taken; }) && sk.some(function (s) { return !s.taken; })) {
      var best = null;
      for (var D = 1; D <= 15; D += 1) for (var T = 0; T <= 3.001; T += 0.25) {
        var ok = 0; sk.forEach(function (s) { if ((s.detour <= D && s.timeToMouth >= T) === s.taken) ok++; });
        if (!best || ok > best.ok) best = { ok: ok, D: D, T: r2(T) };
      }
      skim = { maxDetour: best.D, minTimeToMouth: best.T, agreement: r2(best.ok / sk.length), n: sk.length, taken: sk.filter(function (s) { return s.taken; }).length };
    }
    var boostWithin = boostErr.length >= 5 ? { deg: Math.round(pct(boostErr, 0.9)), n: boostErr.length } : null;
    return { commit: commit, skim: skim, boostWithin: boostWithin };
  }

  var api = { VERSION: VERSION, PHASES: PHASES, courseFrom: courseFrom, normalize: normalize, features: features, analyze: analyze, learn: learn,
    _v: { sub: sub, add: add, mul: mul, dot: dot, cross: cross, len: len, norm: norm, angle: angle } };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  root.RegattaSituation = api;
})(typeof window !== 'undefined' ? window : globalThis);
