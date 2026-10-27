/* Vessel Studio: ability sounds, synthesized in the browser (Web Audio), no files.
 *
 * Yash, 2026-10-28: "sound effects to the Meerkat's abilities, all that we are creating ... add sound effects one by
 * one ... I will test and confirm." Every sound is a RECIPE below: a few oscillators, a noise burst, a filter and an
 * envelope, shaped by four numbers a tester can move (volume, pitch, length, tone). A studio picks the recipes it
 * needs, triggers them from its own events (VesselStudioAudio.play / .loop), and mounts one panel (mountPanel) that
 * lists every sound with a preview button, its sliders and Confirm / Redo, which the page records in its decision log.
 *
 * These are PROTOTYPE sounds for deciding what an ability should sound like. In Unity every sound is an FMOD
 * EventReference shipped empty until sound design wires it (CLAUDE.md, the FMOD rule): a confirmed sound here is the
 * brief for that FMOD event, never a file the game plays.
 *
 * Served JavaScript is ASCII only (the vessel-studio skill, section 7).
 */
(function (root) {
  'use strict';

  var ctx = null, master = null, noiseBuf = null, unlocked = false;
  var settings = {};        // name -> { on, vol, pitch, len, tone }
  var masterVol = 0.6, muted = false;
  var loops = {};           // id -> { name, nodes, gain, stop }
  var lastPlayed = {};      // name -> time (s), for the per-sound rate limit
  var listeners = [];       // panel refresh hooks

  function ensure() {
    if (ctx) return ctx;
    var AC = root.AudioContext || root.webkitAudioContext;
    if (!AC) return null;
    try { ctx = new AC(); } catch (e) { return null; }
    master = ctx.createGain();
    master.gain.value = muted ? 0 : masterVol;
    master.connect(ctx.destination);
    var n = Math.floor(ctx.sampleRate * 2), b = ctx.createBuffer(1, n, ctx.sampleRate), d = b.getChannelData(0);
    var s = 20261028;   // seeded noise: the same burst every time (D3: Math.random never decides anything)
    for (var i = 0; i < n; i++) { s = (s * 1664525 + 1013904223) >>> 0; d[i] = (s / 4294967296) * 2 - 1; }
    noiseBuf = b;
    return ctx;
  }

  // A browser starts audio only after a user gesture: the first press anywhere unlocks it.
  function unlock() {
    if (unlocked) return;
    var c = ensure(); if (!c) return;
    if (c.state === 'suspended') c.resume();
    unlocked = true;
  }
  if (root.addEventListener) {
    ['pointerdown', 'keydown', 'touchstart'].forEach(function (ev) { root.addEventListener(ev, unlock, { once: false, passive: true }); });
  }

  // ---- building blocks -------------------------------------------------------------------------------------------
  function env(g, t0, a, peak, dur, rel) {
    g.gain.cancelScheduledValues(t0);
    g.gain.setValueAtTime(0.0001, t0);
    g.gain.exponentialRampToValueAtTime(Math.max(0.0002, peak), t0 + Math.max(0.002, a));
    g.gain.setValueAtTime(Math.max(0.0002, peak), t0 + Math.max(0.003, dur - rel));
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
  }
  function osc(type, f0, f1, t0, dur, out, peak, a, rel) {
    var o = ctx.createOscillator(), g = ctx.createGain();
    o.type = type;
    o.frequency.setValueAtTime(Math.max(1, f0), t0);
    if (f1 && f1 !== f0) o.frequency.exponentialRampToValueAtTime(Math.max(1, f1), t0 + dur);
    env(g, t0, a == null ? 0.01 : a, peak, dur, rel == null ? dur * 0.6 : rel);
    o.connect(g); g.connect(out);
    o.start(t0); o.stop(t0 + dur + 0.05);
    return o;
  }
  function noise(t0, dur, out, peak, ftype, f0, f1, q, a, rel) {
    var src = ctx.createBufferSource(), f = ctx.createBiquadFilter(), g = ctx.createGain();
    src.buffer = noiseBuf;
    f.type = ftype || 'lowpass';
    f.frequency.setValueAtTime(Math.max(20, f0), t0);
    if (f1 && f1 !== f0) f.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t0 + dur);
    f.Q.value = q == null ? 0.8 : q;
    env(g, t0, a == null ? 0.005 : a, peak, dur, rel == null ? dur * 0.7 : rel);
    src.connect(f); f.connect(g); g.connect(out);
    src.start(t0, 0.13); src.stop(t0 + dur + 0.05);
    return src;
  }
  // A frequency-modulated tone: the electric and space-y sounds.
  function fm(f0, f1, ratio, depth, t0, dur, out, peak, a, rel) {
    var car = ctx.createOscillator(), mod = ctx.createOscillator(), mg = ctx.createGain(), g = ctx.createGain();
    car.frequency.setValueAtTime(f0, t0); if (f1 !== f0) car.frequency.exponentialRampToValueAtTime(Math.max(1, f1), t0 + dur);
    mod.frequency.setValueAtTime(f0 * ratio, t0); if (f1 !== f0) mod.frequency.exponentialRampToValueAtTime(Math.max(1, f1 * ratio), t0 + dur);
    mg.gain.setValueAtTime(depth * f0, t0);
    mod.connect(mg); mg.connect(car.frequency);
    env(g, t0, a == null ? 0.01 : a, peak, dur, rel == null ? dur * 0.6 : rel);
    car.connect(g); g.connect(out);
    car.start(t0); mod.start(t0); car.stop(t0 + dur + 0.05); mod.stop(t0 + dur + 0.05);
  }
  // Where the sound is: a pan (-1 left .. 1 right) and a distance gain, passed by the page.
  function bus(opts, vol, tone) {
    var g = ctx.createGain(), f = ctx.createBiquadFilter(), out = g;
    g.gain.value = vol;
    f.type = 'lowpass';
    f.frequency.value = 400 + 18000 * Math.pow(Math.max(0, Math.min(1, tone)), 2);
    f.connect(g);
    var tail = g;
    if (ctx.createStereoPanner) {
      var p = ctx.createStereoPanner();
      p.pan.value = Math.max(-1, Math.min(1, (opts && opts.pan) || 0));
      g.connect(p); tail = p;
    }
    tail.connect(master);
    return { input: f, gain: g };   // recipes write into the filter; the gain is the sound's volume
  }

  // ---- the recipes ---------------------------------------------------------------------------------------------------
  // Each: label, group, what it is for, how it is built, its default length (s) and whether it LOOPS.
  // fn(t0, out, P) where P = { pitch, len, x } (x = an optional 0..1 strength the page passes, e.g. a bend).
  var R = {};
  function def(name, o) { R[name] = o; }

  // The wormhole (rounds 1-15), the Meerkat's one shipped ability.
  def('reticleHum', { label: 'Reticle hum', group: 'Wormhole', loop: true, len: 1,
    what: 'While the right stick bends the reticle: a soft hum that rises with the bend.',
    how: 'two detuned sines (110 Hz), the bend lifts the pitch',
    loopFn: function (out, P) {
      var a = ctx.createOscillator(), b = ctx.createOscillator(), g = ctx.createGain();
      a.type = 'sine'; b.type = 'sine'; a.frequency.value = 110 * P.pitch; b.frequency.value = 110.7 * P.pitch;
      g.gain.value = 0.12; a.connect(g); b.connect(g); g.connect(out); a.start(); b.start();
      return { set: function (x) { var f = (110 + 90 * x) * P.pitch; a.frequency.setTargetAtTime(f, ctx.currentTime, 0.05); b.frequency.setTargetAtTime(f * 1.006, ctx.currentTime, 0.05); },
               stop: function () { a.stop(); b.stop(); } };
    } });
  def('form', { label: 'Tunnel forms (RT)', group: 'Wormhole', len: 0.45,
    what: 'RT: the tunnel ripples open from its centre.',
    how: 'a rising filtered-noise swish over a sine sweep 220 to 880 Hz',
    fn: function (t, out, P) { noise(t, P.len, out, 0.5, 'bandpass', 500 * P.pitch, 3200 * P.pitch, 1.4, 0.05); osc('sine', 220 * P.pitch, 880 * P.pitch, t, P.len, out, 0.25, 0.04); } });
  // Yash, 2026-10-28: the first ride rush (band-passed noise up to 3 kHz) "is hurting": it is now bass all the way
  // through, with a sudden high glint only now and then.
  def('rideRush', { label: 'Ride rush (inside the tunnel)', group: 'Wormhole', loop: true, len: 1,
    what: 'Riding a tunnel or the labyrinth: a deep bass drone and rumble that swells with the ride speed, and now and then a sudden short high glint (rare: about one every 3-7 s; Length spaces them further apart).',
    how: 'a sub sine (42 Hz) and its fifth, low-passed noise rumble under 220 Hz, and seeded high sine glints 3-6 kHz',
    loopFn: function (out, P) {
      var t = ctx.currentTime;
      var sub = ctx.createOscillator(), fifth = ctx.createOscillator(), sg = ctx.createGain();
      sub.type = 'sine'; fifth.type = 'triangle';
      sub.frequency.value = 42 * P.pitch; fifth.frequency.value = 63 * P.pitch;
      sg.gain.value = 0.28; sub.connect(sg); fifth.connect(sg); sg.connect(out);
      var src = ctx.createBufferSource(), lp = ctx.createBiquadFilter(), ng = ctx.createGain();
      src.buffer = noiseBuf; src.loop = true; lp.type = 'lowpass'; lp.Q.value = 0.9; lp.frequency.value = 120 * P.pitch;
      ng.gain.value = 0.25; src.connect(lp); lp.connect(ng); ng.connect(out);
      // a slow throb, so the bass moves like water in a pipe
      var lfo = ctx.createOscillator(), lg = ctx.createGain(); lfo.frequency.value = 0.7; lg.gain.value = 0.07;
      lfo.connect(lg); lg.connect(sg.gain);
      sub.start(t); fifth.start(t); src.start(t); lfo.start(t);
      var rs = 917, next = t + 1.5;
      function rnd() { rs = (rs * 1664525 + 1013904223) >>> 0; return rs / 4294967296; }
      return {
        set: function (x) {
          var now = ctx.currentTime;
          sub.frequency.setTargetAtTime((42 + 16 * x) * P.pitch, now, 0.15);
          fifth.frequency.setTargetAtTime((63 + 24 * x) * P.pitch, now, 0.15);
          lp.frequency.setTargetAtTime((120 + 100 * x) * P.pitch, now, 0.15);
          sg.gain.setTargetAtTime(0.22 + 0.14 * x, now, 0.15);
          ng.gain.setTargetAtTime(0.17 + 0.2 * x, now, 0.15);
          if (now >= next) {   // the rare treble glint: short, high, quiet, never twice close together
            var f = (3000 + 3000 * rnd()) * P.pitch;
            osc('sine', f, f * (rnd() < 0.5 ? 1.25 : 0.8), now, 0.07, out, 0.05, 0.002, 0.05);
            next = now + (3 + 4 * rnd()) * Math.max(0.4, P.len);
          }
        },
        stop: function () { sub.stop(); fifth.stop(); src.stop(); lfo.stop(); },
      };
    } });
  def('exitPop', { label: 'Out of the tunnel', group: 'Wormhole', len: 0.3,
    what: 'Leaving the far mouth.',
    how: 'a low thump (sine 130 to 45 Hz) with a short noise puff',
    fn: function (t, out, P) { osc('sine', 130 * P.pitch, 45 * P.pitch, t, P.len, out, 0.6, 0.005); noise(t, P.len * 0.5, out, 0.25, 'lowpass', 2500, 400, 0.7); } });
  def('collapse', { label: 'Tunnel collapses', group: 'Wormhole', len: 0.9,
    what: 'A tunnel collapses to its centre (also every soliton that retires).',
    how: 'a falling sine 380 to 60 Hz under soft low noise',
    fn: function (t, out, P) { osc('sine', 380 * P.pitch, 60 * P.pitch, t, P.len, out, 0.3, 0.02); noise(t, P.len, out, 0.18, 'lowpass', 900, 120, 0.6, 0.05); } });

  // Garrett's soliton kit, and the Mix (Space, Time, Charge, Mass).
  def('swallow', { label: 'Soliton swallows a prism', group: 'Space: soliton', len: 0.16,
    what: 'A prism is drawn in at the way in.',
    how: 'a short gulp: sine 620 to 150 Hz',
    fn: function (t, out, P) { osc('sine', 620 * P.pitch, 150 * P.pitch, t, P.len, out, 0.35, 0.004); } });
  def('fire', { label: 'Soliton fires a prism', group: 'Space: soliton', len: 0.22,
    what: 'A prism leaves the way out as a moving prism.',
    how: 'a rising thoomp (triangle 90 to 360 Hz) with a click',
    fn: function (t, out, P) { osc('triangle', 90 * P.pitch, 360 * P.pitch, t, P.len, out, 0.45, 0.003); noise(t, 0.03, out, 0.3, 'highpass', 3000, 3000, 0.7); } });
  def('shatter', { label: 'Prisms annihilate', group: 'Space: soliton', len: 0.3,
    what: 'A moving prism hits a plain prism: both are destroyed.',
    how: 'a high noise burst and three glassy pings',
    fn: function (t, out, P) { noise(t, P.len, out, 0.4, 'highpass', 2500 * P.pitch, 6000 * P.pitch, 0.7); [1760, 2350, 3130].forEach(function (f, i) { osc('sine', f * P.pitch, f * P.pitch * 0.97, t + i * 0.025, P.len * 0.8, out, 0.12, 0.002); }); } });
  def('bounce', { label: 'Shielded prism bounces', group: 'Space: soliton', len: 0.14,
    what: 'A shielded prism rebounds instead of breaking (PRISM_MOTION 1a).',
    how: 'one clean ping, 940 Hz, with a short FM shine',
    fn: function (t, out, P) { fm(940 * P.pitch, 900 * P.pitch, 2.01, 0.6, t, P.len, out, 0.3, 0.002); } });
  def('surfaceArea', { label: 'Space level up (surface area)', group: 'Space: soliton', len: 0.5,
    what: 'Space rises: the soliton grows.',
    how: 'a widening FM swell 160 to 240 Hz',
    fn: function (t, out, P) { fm(160 * P.pitch, 240 * P.pitch, 1.5, 1.2, t, P.len, out, 0.25, 0.1); } });
  def('boostOut', { label: 'Boost out (Time L5)', group: 'Time', len: 0.4,
    what: "Garrett's Time L5: leaving every soliton with a boost.",
    how: 'a rising saw sweep 200 to 1200 Hz through a closing filter',
    fn: function (t, out, P) { osc('sawtooth', 200 * P.pitch, 1200 * P.pitch, t, P.len, out, 0.18, 0.01); noise(t, P.len, out, 0.15, 'bandpass', 800, 4000, 2); } });
  def('chainDig', { label: 'Chain Dig (Time L5, Mix)', group: 'Time', len: 0.3,
    what: "The Mix's Time L5: RT late in a ride digs the next tunnel from the far mouth.",
    how: 'two quick dig thuds and a rising blip',
    fn: function (t, out, P) { noise(t, 0.08, out, 0.4, 'lowpass', 700, 200, 0.8); noise(t + 0.09, 0.08, out, 0.4, 'lowpass', 900, 250, 0.8); osc('square', 500 * P.pitch, 1000 * P.pitch, t + 0.16, P.len * 0.45, out, 0.14, 0.003); } });
  def('sentinelPlace', { label: 'Sentinel stands up', group: 'Charge: sentinel', len: 0.28,
    what: 'A sentinel perches at a tunnel entrance.',
    how: 'a two-note chirp, 1200 then 1600 Hz',
    fn: function (t, out, P) { osc('triangle', 1200 * P.pitch, 1250 * P.pitch, t, 0.09, out, 0.22, 0.004); osc('triangle', 1600 * P.pitch, 1680 * P.pitch, t + 0.11, P.len - 0.11, out, 0.22, 0.004); } });
  def('zap', { label: 'Sentinel zap', group: 'Charge: sentinel', len: 0.08,
    what: 'A zap on an opposing vessel (sub-petal tick). The AI zaps about 5 a second, so it is short and rate-limited.',
    how: 'an FM buzz 320 Hz, deep modulation, band-limited',
    rate: 0.06,
    fn: function (t, out, P) { fm(320 * P.pitch, 280 * P.pitch, 3.7, 6, t, P.len, out, 0.2, 0.002, P.len * 0.5); } });
  def('shieldZap', { label: 'Sentinel shields a prism', group: 'Charge: sentinel', len: 0.18,
    what: 'A same-domain prism in the guard is zapped shielded.',
    how: 'a bright up-glint: sine 1400 to 2600 Hz with shimmer',
    rate: 0.05,
    fn: function (t, out, P) { fm(1400 * P.pitch, 2600 * P.pitch, 1.5, 0.3, t, P.len, out, 0.24, 0.004); } });
  def('sentinelLost', { label: 'Sentinel lost', group: 'Charge: sentinel', len: 0.45,
    what: 'A sentinel dies or retires (its soliton goes with it).',
    how: 'a falling chirp 1500 to 400 Hz',
    fn: function (t, out, P) { osc('triangle', 1500 * P.pitch, 400 * P.pitch, t, P.len, out, 0.22, 0.004); } });
  def('alarm', { label: 'Alarm call (Mix)', group: 'Charge: sentinel', len: 0.5,
    what: "The Mix: a rival enters your sentinel's guard and is marked.",
    how: 'three short barks, 900 to 700 Hz (a meerkat alarm call)',
    fn: function (t, out, P) { for (var i = 0; i < 3; i++) osc('square', 900 * P.pitch, 700 * P.pitch, t + i * P.len / 3, P.len / 4, out, 0.2, 0.003, 0.04); } });
  def('hop', { label: 'The hop', group: 'Charge: sentinel', len: 0.6,
    what: "Garrett's kit: a sentinel rides its soliton and becomes the flyer.",
    how: 'a whoosh into a rising chime',
    fn: function (t, out, P) { noise(t, P.len * 0.6, out, 0.35, 'bandpass', 600, 3000, 1.2, 0.08); osc('sine', 660 * P.pitch, 990 * P.pitch, t + P.len * 0.45, P.len * 0.55, out, 0.2, 0.01); } });
  def('seatSwap', { label: 'Seat swap', group: 'Charge: sentinel', len: 0.12,
    what: "Garrett's kit: D-pad to the next seat (flyer, sentinels).",
    how: 'a soft two-tick',
    fn: function (t, out, P) { osc('sine', 1000 * P.pitch, 1000 * P.pitch, t, 0.04, out, 0.15, 0.002); osc('sine', 1300 * P.pitch, 1300 * P.pitch, t + 0.06, 0.05, out, 0.15, 0.002); } });
  def('portal', { label: 'Exit portal forms (Mass)', group: 'Mass', len: 0.55,
    what: 'Six small prisms set down in a hexagon round the way out.',
    how: 'six quick glass notes up a pentatonic ring, one per prism',
    fn: function (t, out, P) { [523, 587, 659, 784, 880, 1047].forEach(function (f, i) { osc('sine', f * P.pitch, f * P.pitch, t + i * P.len / 7, P.len / 3, out, 0.11, 0.003); }); } });
  def('dangerPortal', { label: 'Danger portal (Mass L5)', group: 'Mass', len: 0.55,
    what: 'Mass L5: the portal prisms are danger prisms.',
    how: 'the portal ring, darker and with a grinding edge',
    fn: function (t, out, P) { [392, 415, 466, 523, 554, 622].forEach(function (f, i) { osc('sawtooth', f * P.pitch, f * P.pitch * 0.98, t + i * P.len / 7, P.len / 3, out, 0.16, 0.003); }); } });
  def('caveIn', { label: 'Cave-in (Space L5, Mix)', group: 'Space: soliton', len: 1.0,
    what: 'The Mix: B collapses a soliton and fires everything it holds.',
    how: 'a low rumble with tremolo under a thump',
    fn: function (t, out, P) {
      osc('sine', 90 * P.pitch, 40 * P.pitch, t, 0.35, out, 0.6, 0.004);
      var src = ctx.createBufferSource(), f = ctx.createBiquadFilter(), g = ctx.createGain(), lfo = ctx.createOscillator(), lg = ctx.createGain();
      src.buffer = noiseBuf; f.type = 'lowpass'; f.frequency.value = 160; lfo.frequency.value = 11; lg.gain.value = 0.15;
      lfo.connect(lg); lg.connect(g.gain); env(g, t, 0.05, 0.35, P.len, P.len * 0.6);
      src.connect(f); f.connect(g); g.connect(out); src.start(t); lfo.start(t); src.stop(t + P.len + 0.05); lfo.stop(t + P.len + 0.05);
    } });
  def('flume', { label: 'Pilot sight: FLUME (hold LB)', group: 'Pilot', loop: true, len: 1,
    what: 'Holding LB: every soliton outlined. A quiet airy shimmer while held.',
    how: 'a high sine pad with slow tremolo',
    loopFn: function (out, P) {
      var a = ctx.createOscillator(), b = ctx.createOscillator(), g = ctx.createGain(), lfo = ctx.createOscillator(), lg = ctx.createGain();
      a.frequency.value = 1320 * P.pitch; b.frequency.value = 1980 * P.pitch; g.gain.value = 0.05; lfo.frequency.value = 3; lg.gain.value = 0.03;
      lfo.connect(lg); lg.connect(g.gain); a.connect(g); b.connect(g); g.connect(out); a.start(); b.start(); lfo.start();
      return { set: function () {}, stop: function () { a.stop(); b.stop(); lfo.stop(); } };
    } });

  // The omnicrystal: collecting one arms the next RT as a 3D labyrinth through soil (round 16; both kits).
  def('omniPickup', { label: 'Omni crystal collected', group: 'Omnicrystal: labyrinth', len: 0.6,
    what: 'Picking up an omni crystal: the four elements at once.',
    how: 'a four-note sparkle chord (one note per element), rising',
    fn: function (t, out, P) { [659, 831, 988, 1319].forEach(function (f, i) { osc('triangle', f * P.pitch, f * P.pitch * 1.01, t + i * 0.04, P.len, out, 0.1, 0.005); }); } });
  def('trunkTeleport', { label: 'Labyrinth armed (next RT)', group: 'Omnicrystal: labyrinth', len: 0.55,
    what: 'The next RT will dig the labyrinth: a low armed tone.',
    how: 'a deep FM vwoom, down then up',
    fn: function (t, out, P) { fm(220 * P.pitch, 70 * P.pitch, 0.5, 2, t, P.len * 0.5, out, 0.35, 0.02); fm(70 * P.pitch, 260 * P.pitch, 0.5, 2, t + P.len * 0.5, P.len * 0.5, out, 0.3, 0.01); } });
  def('burrowGrow', { label: 'Labyrinth digs', group: 'Omnicrystal: labyrinth', len: 1.3,
    what: 'RT: the labyrinth digs itself out from its source through the soil.',
    how: 'a crackle of short dirt-dig bursts over a low rumble',
    fn: function (t, out, P) {
      noise(t, P.len, out, 0.55, 'lowpass', 220, 120, 0.7, 0.1);
      for (var i = 0; i < 9; i++) noise(t + (i / 9) * P.len * 0.9, 0.05, out, 0.7, 'bandpass', (900 + 250 * (i % 4)) * P.pitch, 600 * P.pitch, 2);
    } });
  def('burrowEnter', { label: 'Slide into the labyrinth', group: 'Omnicrystal: labyrinth', len: 0.3,
    what: 'Diving into the labyrinth at its source.',
    how: 'a soft downward slide: filtered noise 3000 to 600 Hz',
    fn: function (t, out, P) { noise(t, P.len, out, 0.35, 'bandpass', 3000 * P.pitch, 600 * P.pitch, 1.5, 0.02); } });
  def('forkTake', { label: 'Take a fork', group: 'Omnicrystal: labyrinth', len: 0.1,
    what: 'Leaning left, right, up or down through a fork (panned to the side taken).',
    how: 'a soft tick and a short rising tone',
    fn: function (t, out, P) { noise(t, 0.02, out, 0.25, 'highpass', 2000, 2000, 0.7); osc('sine', 700 * P.pitch, 900 * P.pitch, t, P.len, out, 0.12, 0.003); } });
  def('burrowExit', { label: 'Out of an opening', group: 'Omnicrystal: labyrinth', len: 0.3,
    what: 'Leaving the labyrinth by any of its openings.',
    how: 'a pop: sine 160 to 60 Hz and a puff of soil',
    fn: function (t, out, P) { osc('sine', 160 * P.pitch, 60 * P.pitch, t, P.len, out, 0.5, 0.004); noise(t, P.len * 0.6, out, 0.25, 'lowpass', 1500, 300, 0.7); } });
  def('burrowCollapse', { label: 'Labyrinth falls in', group: 'Omnicrystal: labyrinth', len: 1.0,
    what: 'The labyrinth falls in when its time is up.',
    how: 'a soft crumble: low noise with falling grit',
    fn: function (t, out, P) { noise(t, P.len, out, 0.6, 'lowpass', 600, 90, 0.6, 0.05); for (var i = 0; i < 6; i++) noise(t + i * P.len / 7, 0.04, out, 0.35, 'bandpass', 1200 - i * 120, 800, 2); } });

  // ---- playing ---------------------------------------------------------------------------------------------------
  function cfg(name) {
    var r = R[name], s = settings[name] || (settings[name] = {});
    return { on: s.on !== false, vol: s.vol == null ? 0.8 : s.vol, pitch: s.pitch == null ? 1 : s.pitch,
             len: s.len == null ? 1 : s.len, tone: s.tone == null ? 0.75 : s.tone, base: r ? r.len : 0.3 };
  }

  /** Play a one-shot. opts: { pan, gain (a distance factor 0..1), x }. Returns false when muted or unknown. */
  function play(name, opts) {
    var r = R[name]; if (!r || !r.fn) return false;
    var c = cfg(name); if (!c.on || muted) return false;
    if (!ensure() || ctx.state !== 'running') return false;
    var now = ctx.currentTime;
    var rate = r.rate || 0.03;
    if (lastPlayed[name] != null && now - lastPlayed[name] < rate) return false;   // a burst of 5 zaps a second stays a buzz, not a wall
    lastPlayed[name] = now;
    var g = (opts && opts.gain != null) ? Math.max(0, Math.min(1, opts.gain)) : 1;
    if (g < 0.02) return false;
    var out = bus(opts, c.vol * g, c.tone).input;
    r.fn(now + 0.005, out, { pitch: c.pitch, len: Math.max(0.03, c.base * c.len), x: opts && opts.x || 0 });
    return true;
  }

  /** Start (or update) a looped sound under an id; set(x) drives its strength 0..1. stop(id) ends it. */
  function loop(id, name, x, opts) {
    var r = R[name]; if (!r || !r.loopFn) return;
    var c = cfg(name);
    if (!c.on || muted || !ensure() || ctx.state !== 'running') { stop(id); return; }
    var L = loops[id];
    if (!L || L.name !== name) {
      stop(id);
      var b = bus(opts, 0.0001, c.tone);
      b.gain.gain.setValueAtTime(0.0001, ctx.currentTime);
      b.gain.gain.exponentialRampToValueAtTime(Math.max(0.0002, c.vol), ctx.currentTime + 0.08);   // fade in
      L = loops[id] = { name: name, node: r.loopFn(b.input, { pitch: c.pitch, len: c.len }), fade: b.gain };
    }
    if (L.node && L.node.set) L.node.set(Math.max(0, Math.min(1, x || 0)));
  }
  function stop(id) {
    var L = loops[id]; if (!L) return;
    delete loops[id];
    try {
      var t = ctx.currentTime;
      L.fade.gain.cancelScheduledValues(t); L.fade.gain.setValueAtTime(Math.max(0.0002, L.fade.gain.value), t);
      L.fade.gain.exponentialRampToValueAtTime(0.0001, t + 0.12);
      setTimeout(function () { try { L.node.stop(); } catch (e) {} }, 200);
    } catch (e) {}
  }
  function stopAll() { Object.keys(loops).forEach(stop); }

  // ---- the panel -------------------------------------------------------------------------------------------------
  var SLIDERS = [
    ['vol', 'Volume', 0, 1.5, 0.01, 'How loud this sound is (1 = as designed).'],
    ['pitch', 'Pitch', 0.5, 2, 0.01, 'Every frequency in the recipe times this (1 = as designed; 2 = an octave up).'],
    ['len', 'Length', 0.4, 2.5, 0.01, 'The sound\'s length times this.'],
    ['tone', 'Brightness', 0, 1, 0.01, 'A final low-pass: 0 is muffled, 1 is open.'],
  ];
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }

  /**
   * Mount the sound panel. opts:
   *   names: the recipes this studio uses, in order (default: all)
   *   state: { master, muted, sounds: { name: {on, vol, pitch, len, tone, verdict} } } to restore
   *   onChange(state): save it
   *   onVerdict(name, verdict, params): record 'confirmed' or 'redo' in the studio's decision log
   */
  function mountPanel(host, opts) {
    opts = opts || {};
    var names = (opts.names || Object.keys(R)).filter(function (n) { return R[n]; });
    var st = opts.state || {};
    if (st.master != null) masterVol = st.master;
    muted = !!st.muted;
    if (st.sounds) Object.keys(st.sounds).forEach(function (n) { settings[n] = Object.assign({}, st.sounds[n]); });
    if (master) master.gain.value = muted ? 0 : masterVol;

    function save() {
      if (master) master.gain.setTargetAtTime(muted ? 0 : masterVol, ctx.currentTime, 0.02);
      if (opts.onChange) opts.onChange({ master: masterVol, muted: muted, sounds: JSON.parse(JSON.stringify(settings)) });
    }
    var groups = [];
    names.forEach(function (n) { if (groups.indexOf(R[n].group) < 0) groups.push(R[n].group); });

    var h = '<p class="hint">Every ability\'s sound, made in the browser (no files). Press <b>Play</b> on each, shape it, then <b>Confirm</b> or <b>Redo</b>: your verdict goes to the decision log. These are the briefs for the game\'s FMOD events, which ship empty until sound design makes them.</p>' +
      '<div class="sa-row"><label title="Every sound in this studio.">Master</label><input type="range" id="saMaster" min="0" max="1" step="0.01" value="' + masterVol + '"><span id="saMasterV">' + masterVol.toFixed(2) + '</span></div>' +
      '<div class="sa-row"><label><input type="checkbox" id="saMute"' + (muted ? ' checked' : '') + '> Mute all</label></div>' +
      '<div class="sa-row"><label title="Show only one group of sounds.">Group</label><select class="arp-seg" id="saGroup"><option value="">All</option>' + groups.map(function (g) { return '<option>' + esc(g) + '</option>'; }).join('') + '</select></div>' +
      '<div id="saList"></div>';
    host.innerHTML = h;
    var list = host.querySelector('#saList');

    function verdictOf(n) { return (settings[n] && settings[n].verdict) || ''; }
    function renderList() {
      var g = host.querySelector('#saGroup').value;
      list.innerHTML = names.filter(function (n) { return !g || R[n].group === g; }).map(function (n) {
        var r = R[n], c = cfg(n), v = verdictOf(n);
        return '<details class="card sa-card" data-n="' + n + '"><summary>' + esc(r.label) + ' <span class="sa-tag">' + (r.loop ? 'loop' : 'one-shot') + '</span>' +
          (v ? ' <span class="sa-v sa-' + v + '">' + (v === 'confirmed' ? 'confirmed' : 'redo') + '</span>' : '') + '</summary>' +
          '<p class="hint">' + esc(r.what) + ' <i>Built from ' + esc(r.how) + '.</i></p>' +
          '<div class="sa-row"><label><input type="checkbox" data-k="on"' + (c.on ? ' checked' : '') + '> On</label>' +
          '<button class="btn" data-act="play">' + (r.loop ? 'Play 2 s' : 'Play') + '</button></div>' +
          SLIDERS.map(function (s) { return '<div class="sa-row"><label title="' + esc(s[5]) + '">' + s[1] + '</label><input type="range" data-k="' + s[0] + '" min="' + s[2] + '" max="' + s[3] + '" step="' + s[4] + '" value="' + c[s[0]] + '"><span>' + Number(c[s[0]]).toFixed(2) + '</span></div>'; }).join('') +
          '<div class="sa-row"><button class="btn primary" data-act="confirmed">Confirm</button><button class="btn" data-act="redo">Redo</button><button class="btn" data-act="reset" title="Back to the recipe as designed.">Reset</button></div></details>';
      }).join('');
    }
    renderList();
    host.querySelector('#saGroup').addEventListener('change', renderList);
    host.querySelector('#saMaster').addEventListener('input', function (e) { masterVol = +e.target.value; host.querySelector('#saMasterV').textContent = masterVol.toFixed(2); save(); });
    host.querySelector('#saMute').addEventListener('change', function (e) { muted = e.target.checked; if (muted) stopAll(); save(); });
    list.addEventListener('input', function (e) {
      var card = e.target.closest('.sa-card'); if (!card) return;
      var n = card.getAttribute('data-n'), k = e.target.getAttribute('data-k'); if (!k) return;
      var s = settings[n] || (settings[n] = {});
      if (k === 'on') s.on = e.target.checked; else { s[k] = +e.target.value; var lab = e.target.nextElementSibling; if (lab) lab.textContent = s[k].toFixed(2); }
      save();
    });
    list.addEventListener('change', function (e) { if (e.target.getAttribute('data-k') === 'on') { var n = e.target.closest('.sa-card').getAttribute('data-n'); (settings[n] || (settings[n] = {})).on = e.target.checked; save(); } });
    list.addEventListener('click', function (e) {
      var b = e.target.closest('button'); if (!b) return;
      var card = b.closest('.sa-card'), n = card.getAttribute('data-n'), act = b.getAttribute('data-act');
      unlock();
      if (act === 'play') {
        if (R[n].loop) { loop('preview', n, 0); var t0 = Date.now(); var iv = setInterval(function () { var x = Math.min(1, (Date.now() - t0) / 1800); loop('preview', n, x); if (x >= 1) { clearInterval(iv); stop('preview'); } }, 50); }
        else { var was = settings[n] && settings[n].on === false; if (was) settings[n].on = true; play(n, {}); if (was) settings[n].on = false; }
      } else if (act === 'reset') {
        var keep = settings[n] && settings[n].verdict; settings[n] = keep ? { verdict: keep } : {}; save(); renderList();
      } else if (act === 'confirmed' || act === 'redo') {
        (settings[n] || (settings[n] = {})).verdict = act; save(); renderList();
        if (opts.onVerdict) opts.onVerdict(n, act, Object.assign({ label: R[n].label }, cfg(n)));
      }
    });
    listeners.push(renderList);
    return { refresh: renderList };
  }

  // A small style block, once (the theme's tokens; no colours of its own beyond them).
  function style() {
    if (!root.document || root.document.getElementById('studio-audio-style')) return;
    var s = root.document.createElement('style'); s.id = 'studio-audio-style';
    s.textContent = '.sa-row{display:flex;align-items:center;gap:8px;margin:4px 0}.sa-row>label{min-width:86px}.sa-row input[type=range]{flex:1;min-width:0}' +
      '.sa-row>span{min-width:36px;text-align:right;font-variant-numeric:tabular-nums}.sa-tag{opacity:.6;font-size:.8em}' +
      '.sa-v{font-size:.8em;padding:0 6px;border-radius:6px;border:1px solid var(--line)}.sa-confirmed{color:var(--accent)}.sa-redo{opacity:.75}';
    root.document.head.appendChild(s);
  }
  if (root.document) { if (root.document.readyState === 'loading') root.document.addEventListener('DOMContentLoaded', style); else style(); }

  root.VesselStudioAudio = {
    play: play, loop: loop, stop: stop, stopAll: stopAll, mountPanel: mountPanel,
    recipes: function () { return Object.keys(R).map(function (n) { return { name: n, label: R[n].label, group: R[n].group, loop: !!R[n].loop }; }); },
    has: function (n) { return !!R[n]; },
    unlock: unlock,
    get ready() { return !!(ctx && ctx.state === 'running'); },
  };
})(typeof window !== 'undefined' ? window : this);
