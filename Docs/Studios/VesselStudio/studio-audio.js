/* Vessel Studio: ability sounds, synthesized in the browser (Web Audio), no files.
 *
 * Yash, 2026-10-28: "sound effects to the Meerkat's abilities ... one by one ... I will test and confirm", then: "it's
 * so bad right now ... professional space sounds, not childish ones ... give some common good sounds ... options to
 * turn the sound on and off." So every sound is built from the parts film and game sound use for space: deep sub
 * impacts, airy filtered whooshes, slow risers and falls, soft bell tones, all through one shared space reverb and a
 * gentle compressor. No square-wave beeps, no arpeggios. A studio triggers them from its own events (play / loop),
 * mounts the on/off toggle on its stage (mountToggle, key M) and the panel in its settings (mountPanel: every sound
 * with Play, On, four sliders and Confirm / Redo for the decision log).
 *
 * These are PROTOTYPE sounds that brief the game's FMOD events, which ship empty until sound design makes them
 * (CLAUDE.md, the FMOD rule). Unity plays none of them.
 *
 * Served JavaScript is ASCII only (the vessel-studio skill, section 7).
 */
(function (root) {
  'use strict';

  var ctx = null, master = null, comp = null, verb = null, verbIn = null, noiseBuf = null, unlocked = false;
  var settings = {};        // name -> { on, vol, pitch, len, tone, verdict }
  var masterVol = 0.7, soundOn = true;
  var loops = {};           // id -> { name, node, fade }
  var lastPlayed = {};      // name -> time (s), for the per-sound rate limit
  var saveHook = null;      // the panel's save, so the stage toggle persists too
  var toggles = [];         // stage buttons to repaint
  var STORE = 'vessel-studio-sound-on';

  try { var saved = root.localStorage && root.localStorage.getItem(STORE); if (saved === '0') soundOn = false; } catch (e) {}

  // ---- the graph: every sound -> (dry + reverb send) -> compressor -> out ------------------------------------------
  function ensure() {
    if (ctx) return ctx;
    var AC = root.AudioContext || root.webkitAudioContext;
    if (!AC) return null;
    try { ctx = new AC(); } catch (e) { return null; }
    master = ctx.createGain();
    master.gain.value = soundOn ? masterVol : 0;
    comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -18; comp.knee.value = 12; comp.ratio.value = 4; comp.attack.value = 0.004; comp.release.value = 0.25;
    master.connect(comp); comp.connect(ctx.destination);
    var sr = ctx.sampleRate, s = 20261028;
    function rnd() { s = (s * 1664525 + 1013904223) >>> 0; return s / 4294967296 * 2 - 1; }   // seeded (D3)
    var n = Math.floor(sr * 2), b = ctx.createBuffer(1, n, sr), d = b.getChannelData(0);
    for (var i = 0; i < n; i++) d[i] = rnd();
    noiseBuf = b;
    // The space: a 4 s stereo tail of decaying noise, darker as it fades (a synthetic hall).
    var L = Math.floor(sr * 4), ir = ctx.createBuffer(2, L, sr);
    for (var ch = 0; ch < 2; ch++) {
      var c = ir.getChannelData(ch), lp = 0;
      for (var k = 0; k < L; k++) { var t = k / sr, a = 0.9 - 0.75 * (t / 4); lp = lp + a * (rnd() - lp); c[k] = lp * Math.pow(1 - t / 4, 2.6) * (t < 0.012 ? t / 0.012 : 1); }
    }
    verb = ctx.createConvolver(); verb.buffer = ir;
    verbIn = ctx.createGain(); verbIn.gain.value = 0.55;
    var verbOut = ctx.createGain(); verbOut.gain.value = 0.8;
    verbIn.connect(verb); verb.connect(verbOut); verbOut.connect(master);
    return ctx;
  }
  function unlock() {
    if (unlocked) { if (ctx && ctx.state === 'suspended' && soundOn) ctx.resume(); return; }
    var c = ensure(); if (!c) return;
    if (c.state === 'suspended') c.resume();
    unlocked = true;
  }
  if (root.addEventListener) ['pointerdown', 'keydown', 'touchstart'].forEach(function (ev) { root.addEventListener(ev, unlock, { passive: true }); });

  // A sound's own bus: tone filter -> gain -> pan -> (dry to master, wet to the reverb).
  function bus(opts, vol, tone, wet) {
    var f = ctx.createBiquadFilter(), g = ctx.createGain(), tail = g;
    f.type = 'lowpass'; f.Q.value = 0.5;
    f.frequency.value = 300 + 15000 * Math.pow(Math.max(0, Math.min(1, tone)), 2);
    g.gain.value = vol;
    f.connect(g);
    if (ctx.createStereoPanner) { var p = ctx.createStereoPanner(); p.pan.value = Math.max(-1, Math.min(1, (opts && opts.pan) || 0)); g.connect(p); tail = p; }
    tail.connect(master);
    var send = ctx.createGain(); send.gain.value = wet == null ? 0.5 : wet; tail.connect(send); send.connect(verbIn);
    return { input: f, gain: g };
  }

  // ---- building blocks: what space sound is made of ---------------------------------------------------------------
  function env(g, t0, a, peak, dur, curve) {
    var p = Math.max(0.0002, peak);
    g.gain.setValueAtTime(0.0001, t0);
    g.gain.exponentialRampToValueAtTime(p, t0 + Math.max(0.003, a));
    g.gain.setTargetAtTime(0.0001, t0 + Math.max(0.004, a), Math.max(0.01, (dur - a) / (curve || 3.5)));
  }
  function stopAt(node, t) { try { node.stop(t); } catch (e) {} }
  var shaper = null;
  function warm() {   // soft saturation: a sine gains harmonics without harshness
    if (shaper) return shaper;
    var n = 1024, c = new Float32Array(n);
    for (var i = 0; i < n; i++) { var x = i / (n - 1) * 2 - 1; c[i] = Math.tanh(2.2 * x) / Math.tanh(2.2); }
    shaper = c; return c;
  }
  // A deep sine impact or swell, slightly saturated (the "boom" of every trailer).
  function sub(f0, f1, t0, dur, out, peak, a) {
    var o = ctx.createOscillator(), ws = ctx.createWaveShaper(), g = ctx.createGain();
    o.type = 'sine'; o.frequency.setValueAtTime(Math.max(20, f0), t0); o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t0 + dur);
    ws.curve = warm(); env(g, t0, a == null ? 0.006 : a, peak, dur, 3);
    o.connect(ws); ws.connect(g); g.connect(out); o.start(t0); stopAt(o, t0 + dur + 1);
  }
  // Air: noise through a moving band, wide and soft (whooshes, wind, dust).
  function air(t0, dur, out, peak, f0, f1, q, a, type) {
    var src = ctx.createBufferSource(), f = ctx.createBiquadFilter(), g = ctx.createGain();
    src.buffer = noiseBuf; src.loop = true;
    f.type = type || 'bandpass'; f.Q.value = q == null ? 0.7 : q;
    f.frequency.setValueAtTime(Math.max(30, f0), t0); f.frequency.exponentialRampToValueAtTime(Math.max(30, f1), t0 + dur);
    env(g, t0, a == null ? dur * 0.3 : a, peak, dur, 2.5);
    src.connect(f); f.connect(g); g.connect(out); src.start(t0, 0.37); stopAt(src, t0 + dur + 1);
  }
  // A soft bell: a sine with an inharmonic partial and a long decay (pings, glints, sentinels).
  function bell(f, t0, dur, out, peak, bright) {
    [[1, 1], [2.76, 0.35 * (bright == null ? 1 : bright)], [5.4, 0.12 * (bright == null ? 1 : bright)]].forEach(function (pt) {
      var o = ctx.createOscillator(), g = ctx.createGain();
      o.type = 'sine'; o.frequency.value = f * pt[0];
      env(g, t0, 0.004, peak * pt[1], dur / (pt[0] > 1 ? pt[0] * 0.6 : 1), 4);
      o.connect(g); g.connect(out); o.start(t0); stopAt(o, t0 + dur + 0.5);
    });
  }
  // A pad: detuned sines and triangles fading in and out (shimmer, sights, the omnicrystal).
  function pad(freqs, t0, dur, out, peak, a) {
    freqs.forEach(function (f, i) {
      [-6, 0, 6].forEach(function (cents) {
        var o = ctx.createOscillator(), g = ctx.createGain();
        o.type = i === 0 ? 'sine' : 'triangle'; o.frequency.value = f; o.detune.value = cents;
        env(g, t0, a == null ? dur * 0.35 : a, peak / (freqs.length * 2), dur, 2.2);
        o.connect(g); g.connect(out); o.start(t0); stopAt(o, t0 + dur + 1.5);
      });
    });
  }

  // ---- the recipes ---------------------------------------------------------------------------------------------------
  // fn(t0, out, P) with P = { pitch, len }. len is the sound's own seconds x the Length slider.
  var R = {};
  function def(name, o) { R[name] = o; }

  // The wormhole.
  def('reticleHum', { label: 'Reticle hum', group: 'Wormhole', loop: true, len: 1, wet: 0.6,
    what: 'While you shape the reticle: a low, quiet presence that opens up as you bend it.',
    how: 'a soft 55 Hz drone with a filtered-air layer that brightens with the bend',
    loopFn: function (out, P) {
      var o = ctx.createOscillator(), og = ctx.createGain(); o.type = 'sine'; o.frequency.value = 55 * P.pitch; og.gain.value = 0.12;
      var src = ctx.createBufferSource(), f = ctx.createBiquadFilter(), ag = ctx.createGain();
      src.buffer = noiseBuf; src.loop = true; f.type = 'bandpass'; f.Q.value = 0.9; f.frequency.value = 220; ag.gain.value = 0.05;
      o.connect(og); og.connect(out); src.connect(f); f.connect(ag); ag.connect(out); o.start(); src.start();
      return { set: function (x) { var t = ctx.currentTime; f.frequency.setTargetAtTime((220 + 900 * x) * P.pitch, t, 0.12); ag.gain.setTargetAtTime(0.04 + 0.08 * x, t, 0.12); },
               stop: function () { stopAt(o); stopAt(src); } };
    } });
  def('form', { label: 'Tunnel forms (RT)', group: 'Wormhole', len: 0.9, wet: 0.6,
    what: 'RT: the tunnel opens ahead of you.',
    how: 'a deep sub swell rising under an airy reverse-whoosh',
    fn: function (t, out, P) { sub(40 * P.pitch, 70 * P.pitch, t, P.len, out, 0.5, P.len * 0.5); air(t, P.len, out, 0.35, 300 * P.pitch, 2200 * P.pitch, 0.6, P.len * 0.7); } });
  def('rideRush', { label: 'Ride rush (inside the tunnel)', group: 'Wormhole', loop: true, len: 1, wet: 0.35,
    what: 'Inside a tunnel or the roots: deep bass that swells with the ride, and a rare high glint (one every 3-7 s; Length spaces them out).',
    how: 'a 42 Hz sub and its fifth with a slow throb, low rumble under 220 Hz, seeded soft bell glints 2.5-4 kHz',
    loopFn: function (out, P) {
      var t = ctx.currentTime;
      var a = ctx.createOscillator(), b = ctx.createOscillator(), sg = ctx.createGain(); a.type = 'sine'; b.type = 'sine';
      a.frequency.value = 42 * P.pitch; b.frequency.value = 63 * P.pitch; sg.gain.value = 0.16;
      var src = ctx.createBufferSource(), lp = ctx.createBiquadFilter(), ng = ctx.createGain();
      src.buffer = noiseBuf; src.loop = true; lp.type = 'lowpass'; lp.Q.value = 0.7; lp.frequency.value = 120 * P.pitch; ng.gain.value = 0.14;
      var lfo = ctx.createOscillator(), lg = ctx.createGain(); lfo.frequency.value = 0.6; lg.gain.value = 0.04;
      a.connect(sg); b.connect(sg); sg.connect(out); src.connect(lp); lp.connect(ng); ng.connect(out); lfo.connect(lg); lg.connect(sg.gain);
      a.start(t); b.start(t); src.start(t); lfo.start(t);
      var rs = 917, next = t + 1.5;
      function rnd() { rs = (rs * 1664525 + 1013904223) >>> 0; return rs / 4294967296; }
      return {
        set: function (x) {
          var now = ctx.currentTime;
          a.frequency.setTargetAtTime((42 + 16 * x) * P.pitch, now, 0.2); b.frequency.setTargetAtTime((63 + 24 * x) * P.pitch, now, 0.2);
          lp.frequency.setTargetAtTime((120 + 100 * x) * P.pitch, now, 0.2);
          sg.gain.setTargetAtTime(0.12 + 0.08 * x, now, 0.2); ng.gain.setTargetAtTime(0.1 + 0.11 * x, now, 0.2);
          if (now >= next) { bell((2500 + 1500 * rnd()) * P.pitch, now, 0.6, out, 0.06, 0.4); next = now + (3 + 4 * rnd()) * Math.max(0.4, P.len); }
        },
        stop: function () { stopAt(a); stopAt(b); stopAt(src); stopAt(lfo); },
      };
    } });
  def('exitPop', { label: 'Out of the tunnel', group: 'Wormhole', len: 0.8, wet: 0.5,
    what: 'Leaving the far mouth.',
    how: 'a sub thump with an air release falling away',
    fn: function (t, out, P) { sub(90 * P.pitch, 38 * P.pitch, t, P.len, out, 0.55); air(t, P.len * 0.7, out, 0.25, 1600 * P.pitch, 250 * P.pitch, 0.6, 0.01); } });
  def('collapse', { label: 'Tunnel collapses', group: 'Wormhole', len: 1.6, wet: 0.7,
    what: 'A tunnel (or a soliton that retires) collapses to its centre.',
    how: 'a slow falling sub with a dark air tail into the reverb',
    fn: function (t, out, P) { sub(70 * P.pitch, 30 * P.pitch, t, P.len, out, 0.35, 0.05); air(t, P.len, out, 0.18, 900 * P.pitch, 120 * P.pitch, 0.5, 0.08); } });

  // Space: the soliton.
  def('swallow', { label: 'Soliton swallows a prism', group: 'Space: soliton', len: 0.45, wet: 0.5,
    what: 'A prism is drawn in at the way in.',
    how: 'a short inward air pull and a low dip',
    rate: 0.06,
    fn: function (t, out, P) { air(t, P.len, out, 0.2, 2500 * P.pitch, 400 * P.pitch, 1.1, P.len * 0.6); sub(110 * P.pitch, 60 * P.pitch, t + P.len * 0.5, P.len * 0.5, out, 0.2, 0.02); } });
  def('fire', { label: 'Soliton fires a prism', group: 'Space: soliton', len: 0.5, wet: 0.45,
    what: 'A prism leaves the way out as a moving prism.',
    how: 'a punchy sub kick with a forward air burst',
    rate: 0.05,
    fn: function (t, out, P) { sub(140 * P.pitch, 50 * P.pitch, t, P.len, out, 0.45); air(t, P.len * 0.5, out, 0.25, 700 * P.pitch, 3200 * P.pitch, 0.8, 0.005); } });
  def('shatter', { label: 'Prisms annihilate', group: 'Space: soliton', len: 1.2, wet: 0.65,
    what: 'A moving prism hits a plain prism: both are destroyed.',
    how: 'a soft impact with a sparkling glass tail',
    rate: 0.05,
    fn: function (t, out, P) { sub(80 * P.pitch, 40 * P.pitch, t, 0.4, out, 0.35); air(t, 0.25, out, 0.2, 4000 * P.pitch, 1800 * P.pitch, 0.7, 0.003, 'highpass'); bell(1900 * P.pitch, t + 0.01, P.len, out, 0.05, 0.6); bell(2850 * P.pitch, t + 0.05, P.len * 0.8, out, 0.035, 0.6); } });
  def('bounce', { label: 'Shielded prism bounces', group: 'Space: soliton', len: 0.9, wet: 0.55,
    what: 'A shielded prism rebounds instead of breaking.',
    how: 'one clean bell tone, soft and round',
    rate: 0.05,
    fn: function (t, out, P) { bell(740 * P.pitch, t, P.len, out, 0.22, 0.7); } });
  def('surfaceArea', { label: 'Space level up (surface area)', group: 'Space: soliton', len: 1.4, wet: 0.7,
    what: 'Space rises: the soliton grows.',
    how: 'a widening pad swell on a fifth',
    fn: function (t, out, P) { pad([110 * P.pitch, 165 * P.pitch], t, P.len, out, 0.3); } });
  def('caveIn', { label: 'Cave-in (Space L5, Mix)', group: 'Space: soliton', len: 2.0, wet: 0.6,
    what: 'The Mix: B collapses a soliton and fires everything it holds.',
    how: 'a heavy sub drop, a rumble and debris air into the reverb',
    fn: function (t, out, P) { sub(60 * P.pitch, 28 * P.pitch, t, P.len, out, 0.6); air(t, P.len, out, 0.3, 400 * P.pitch, 80 * P.pitch, 0.6, 0.02, 'lowpass'); air(t + 0.05, P.len * 0.6, out, 0.1, 2500, 900, 0.8, 0.01); } });

  // Time.
  def('boostOut', { label: 'Boost out (Time L5)', group: 'Time', len: 0.9, wet: 0.45,
    what: "Garrett's Time L5: leaving every soliton with a boost.",
    how: 'a fast airy riser with a sub push',
    fn: function (t, out, P) { air(t, P.len, out, 0.3, 400 * P.pitch, 5000 * P.pitch, 0.9, P.len * 0.3); sub(50 * P.pitch, 90 * P.pitch, t, P.len, out, 0.3, 0.02); } });
  def('chainDig', { label: 'Chain Dig (Time L5, Mix)', group: 'Time', len: 0.7, wet: 0.5,
    what: "The Mix's Time L5: RT late in a ride digs the next tunnel from the far mouth.",
    how: 'a low thud into a quick forward whoosh',
    fn: function (t, out, P) { sub(100 * P.pitch, 45 * P.pitch, t, 0.3, out, 0.4); air(t + 0.08, P.len, out, 0.25, 500 * P.pitch, 3000 * P.pitch, 0.8, 0.1); } });

  // Charge: the sentinels.
  def('sentinelPlace', { label: 'Sentinel stands up', group: 'Charge: sentinel', len: 1.2, wet: 0.7,
    what: 'A sentinel perches at a tunnel entrance.',
    how: 'two soft bell tones a fifth apart',
    fn: function (t, out, P) { bell(587 * P.pitch, t, P.len, out, 0.14, 0.5); bell(880 * P.pitch, t + 0.12, P.len, out, 0.12, 0.5); } });
  def('zap', { label: 'Sentinel zap', group: 'Charge: sentinel', len: 0.18, wet: 0.3,
    what: 'A zap on an opposing vessel (the AI zaps about 5 a second, so it is short, soft and rate-limited).',
    how: 'a tight crackle of band-passed air with a low tick',
    rate: 0.09,
    fn: function (t, out, P) { air(t, P.len, out, 0.4, 2200 * P.pitch, 1400 * P.pitch, 4, 0.003); sub(180 * P.pitch, 90 * P.pitch, t, 0.08, out, 0.25); } });
  def('shieldZap', { label: 'Sentinel shields a prism', group: 'Charge: sentinel', len: 0.8, wet: 0.6,
    what: 'A same-domain prism in the guard is zapped shielded.',
    how: 'a bright, quiet bell glint',
    rate: 0.08,
    fn: function (t, out, P) { bell(1320 * P.pitch, t, P.len, out, 0.14, 0.5); } });
  def('sentinelLost', { label: 'Sentinel lost', group: 'Charge: sentinel', len: 1.4, wet: 0.7,
    what: 'A sentinel dies or retires (its soliton goes with it).',
    how: 'a falling sub with a dimming bell',
    fn: function (t, out, P) { sub(90 * P.pitch, 35 * P.pitch, t, P.len * 0.7, out, 0.3); bell(440 * P.pitch, t, P.len, out, 0.05, 0.3); } });
  def('alarm', { label: 'Alarm call (Mix)', group: 'Charge: sentinel', len: 1.0, wet: 0.6,
    what: "The Mix: a rival enters your sentinel's guard and is marked.",
    how: 'a sonar ping: one clear tone with a long tail',
    fn: function (t, out, P) { bell(1046 * P.pitch, t, P.len, out, 0.2, 0.25); bell(1046 * P.pitch, t + 0.35, P.len * 0.7, out, 0.08, 0.25); } });
  def('hop', { label: 'The hop', group: 'Charge: sentinel', len: 1.1, wet: 0.6,
    what: "Garrett's kit: a sentinel rides its soliton and becomes the flyer.",
    how: 'a sweeping whoosh through the stereo field, landing on a sub',
    fn: function (t, out, P) { air(t, P.len * 0.8, out, 0.3, 300 * P.pitch, 3500 * P.pitch, 0.8, P.len * 0.4); sub(55 * P.pitch, 45 * P.pitch, t + P.len * 0.6, P.len * 0.5, out, 0.35); } });
  def('seatSwap', { label: 'Seat swap', group: 'Charge: sentinel', len: 0.35, wet: 0.4,
    what: "Garrett's kit: D-pad to the next seat.",
    how: 'a short soft air tick',
    fn: function (t, out, P) { air(t, P.len, out, 0.35, 1800 * P.pitch, 900 * P.pitch, 1.4, 0.004); } });

  // Mass: the exit portal.
  def('portal', { label: 'Exit portal forms (Mass)', group: 'Mass', len: 1.6, wet: 0.7,
    what: 'Six small prisms set down round the way out.',
    how: 'a crystalline pad chord (one root, a fifth and an octave) with a bell on top',
    fn: function (t, out, P) { pad([220 * P.pitch, 330 * P.pitch, 440 * P.pitch], t, P.len, out, 0.22, 0.05); bell(1320 * P.pitch, t, P.len, out, 0.035, 0.5); } });
  def('dangerPortal', { label: 'Danger portal (Mass L5)', group: 'Mass', len: 1.6, wet: 0.65,
    what: 'Mass L5: the portal prisms are danger prisms.',
    how: 'the portal chord made tense (a minor second) over a growling sub',
    fn: function (t, out, P) { pad([220 * P.pitch, 233 * P.pitch, 330 * P.pitch], t, P.len, out, 0.22, 0.05); sub(55 * P.pitch, 50 * P.pitch, t, P.len, out, 0.25, 0.1); } });

  // The pilot sight.
  def('flume', { label: 'Pilot sight: FLUME (hold LB)', group: 'Pilot', loop: true, len: 1, wet: 0.8,
    what: 'Holding LB: every soliton outlined. A quiet airy shimmer while held.',
    how: 'a high pad on a fifth with slow movement',
    loopFn: function (out, P) {
      var os = [], g = ctx.createGain(), lfo = ctx.createOscillator(), lg = ctx.createGain(); g.gain.value = 0.03;
      [880, 1320].forEach(function (f) { [-7, 7].forEach(function (c) { var o = ctx.createOscillator(); o.type = 'sine'; o.frequency.value = f * P.pitch; o.detune.value = c; o.connect(g); o.start(); os.push(o); }); });
      lfo.frequency.value = 0.4; lg.gain.value = 0.012; lfo.connect(lg); lg.connect(g.gain); lfo.start(); g.connect(out);
      return { set: function () {}, stop: function () { os.forEach(function (o) { stopAt(o); }); stopAt(lfo); } };
    } });

  // The omnicrystal.
  def('omniPickup', { label: 'Omnicrystal collected', group: 'Omnicrystal: roots', len: 2.0, wet: 0.75,
    what: 'Picking up an omnicrystal: the four elements at once.',
    how: 'a bright four-voice pad bloom with a bell',
    fn: function (t, out, P) { pad([262 * P.pitch, 330 * P.pitch, 392 * P.pitch, 523 * P.pitch], t, P.len, out, 0.3, 0.08); bell(1046 * P.pitch, t, P.len, out, 0.04, 0.6); } });
  def('trunkTeleport', { label: 'Roots armed', group: 'Omnicrystal: roots', len: 1.2, wet: 0.7,
    what: 'The omnicrystal arms the roots.',
    how: 'a low resonant swell, down then up',
    fn: function (t, out, P) { sub(60 * P.pitch, 45 * P.pitch, t, P.len * 0.5, out, 0.3, 0.1); sub(45 * P.pitch, 70 * P.pitch, t + P.len * 0.5, P.len * 0.5, out, 0.25, 0.05); air(t, P.len, out, 0.08, 300, 900, 1, P.len * 0.5); } });
  def('burrowGrow', { label: 'Roots dig', group: 'Omnicrystal: roots', len: 2.0, wet: 0.5,
    what: 'The roots dig themselves out through the soil.',
    how: 'a low rumble with soft grains of earth',
    fn: function (t, out, P) { air(t, P.len, out, 0.35, 220 * P.pitch, 110 * P.pitch, 0.6, 0.2, 'lowpass'); sub(48 * P.pitch, 40 * P.pitch, t, P.len, out, 0.25, 0.2); for (var i = 0; i < 7; i++) air(t + (i / 7) * P.len * 0.85, 0.12, out, 0.06, 900 * P.pitch, 500 * P.pitch, 2); } });
  def('burrowEnter', { label: 'Slide into the roots', group: 'Omnicrystal: roots', len: 0.7, wet: 0.5,
    what: 'Diving into the roots.',
    how: 'a falling whoosh into a sub',
    fn: function (t, out, P) { air(t, P.len, out, 0.25, 2500 * P.pitch, 300 * P.pitch, 0.8, 0.02); sub(70 * P.pitch, 40 * P.pitch, t + 0.2, P.len, out, 0.3, 0.05); } });
  def('forkTake', { label: 'Take a fork', group: 'Omnicrystal: roots', len: 0.4, wet: 0.4,
    what: 'Leaning through a fork (panned to the side taken).',
    how: 'a short soft whoosh past you',
    fn: function (t, out, P) { air(t, P.len, out, 0.4, 600 * P.pitch, 1500 * P.pitch, 1, P.len * 0.4); } });
  def('burrowExit', { label: 'Out of the roots', group: 'Omnicrystal: roots', len: 0.8, wet: 0.5,
    what: 'Leaving the roots by any opening.',
    how: 'a sub thump with soil air falling away',
    fn: function (t, out, P) { sub(85 * P.pitch, 38 * P.pitch, t, P.len, out, 0.5); air(t, P.len * 0.6, out, 0.2, 1200 * P.pitch, 200 * P.pitch, 0.6, 0.01, 'lowpass'); } });
  def('burrowCollapse', { label: 'Roots fall in', group: 'Omnicrystal: roots', len: 1.8, wet: 0.6,
    what: 'The roots fall in when their time is up.',
    how: 'a long low crumble into the reverb',
    fn: function (t, out, P) { air(t, P.len, out, 0.3, 500 * P.pitch, 70 * P.pitch, 0.6, 0.05, 'lowpass'); sub(50 * P.pitch, 30 * P.pitch, t, P.len, out, 0.2, 0.1); } });

  // ---- playing ---------------------------------------------------------------------------------------------------
  function cfg(name) {
    var r = R[name], s = settings[name] || (settings[name] = {});
    return { on: s.on !== false, vol: s.vol == null ? 0.8 : s.vol, pitch: s.pitch == null ? 1 : s.pitch,
             len: s.len == null ? 1 : s.len, tone: s.tone == null ? 0.7 : s.tone, base: r ? r.len : 0.5, wet: r && r.wet };
  }
  function live() { return soundOn && ensure() && ctx.state === 'running'; }

  /** Play a one-shot. opts: { pan, gain (a distance factor 0..1) }. Returns false when off, muted or unknown. */
  function play(name, opts) {
    var r = R[name]; if (!r || !r.fn) return false;
    var c = cfg(name); if (!c.on || !live()) return false;
    var now = ctx.currentTime, rate = r.rate || 0.04;
    if (lastPlayed[name] != null && now - lastPlayed[name] < rate) return false;
    lastPlayed[name] = now;
    var g = (opts && opts.gain != null) ? Math.max(0, Math.min(1, opts.gain)) : 1;
    if (g < 0.02) return false;
    r.fn(now + 0.005, bus(opts, c.vol * g, c.tone, c.wet).input, { pitch: c.pitch, len: Math.max(0.05, c.base * c.len) });
    return true;
  }
  /** Start (or update) a looped sound under an id; x (0..1) drives its strength. stop(id) ends it. */
  function loop(id, name, x, opts) {
    var r = R[name]; if (!r || !r.loopFn) return;
    var c = cfg(name);
    if (!c.on || !live()) { stop(id); return; }
    var L = loops[id];
    if (!L || L.name !== name) {
      stop(id);
      var b = bus(opts, 0.0001, c.tone, c.wet);
      b.gain.gain.setValueAtTime(0.0001, ctx.currentTime);
      b.gain.gain.exponentialRampToValueAtTime(Math.max(0.0002, c.vol), ctx.currentTime + 0.25);
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
      L.fade.gain.exponentialRampToValueAtTime(0.0001, t + 0.3);
      setTimeout(function () { try { L.node.stop(); } catch (e) {} }, 400);
    } catch (e) {}
  }
  function stopAll() { Object.keys(loops).forEach(stop); }

  /** Sound on or off for the whole studio (remembered in this browser). */
  function setOn(on) {
    soundOn = !!on;
    try { root.localStorage && root.localStorage.setItem(STORE, soundOn ? '1' : '0'); } catch (e) {}
    if (!soundOn) stopAll();
    if (ctx && master) { master.gain.setTargetAtTime(soundOn ? masterVol : 0, ctx.currentTime, 0.03); if (soundOn && ctx.state === 'suspended') ctx.resume(); }
    toggles.forEach(function (fn) { fn(); });
    if (saveHook) saveHook();
  }

  // ---- the stage toggle -------------------------------------------------------------------------------------------
  /** A sound on/off button on the stage (top right of host), plus the M key. */
  function mountToggle(host) {
    if (!host || !root.document) return null;
    var b = root.document.createElement('button');
    b.type = 'button'; b.className = 'sa-toggle';
    b.title = 'Sound on / off (M). Every sound and its settings: Vessel Config > Sound.';
    function paint() { b.textContent = soundOn ? 'Sound: on' : 'Sound: off'; b.setAttribute('aria-pressed', soundOn ? 'true' : 'false'); }
    paint(); toggles.push(paint);
    b.addEventListener('click', function (e) { e.stopPropagation(); unlock(); setOn(!soundOn); });
    host.appendChild(b);
    root.addEventListener('keydown', function (e) {
      if (e.code !== 'KeyM' || e.repeat || e.ctrlKey || e.metaKey || e.altKey) return;
      var tg = e.target && e.target.tagName; if (tg === 'INPUT' || tg === 'TEXTAREA' || tg === 'SELECT') return;
      unlock(); setOn(!soundOn);
    });
    return { refresh: paint };
  }

  // ---- the panel -------------------------------------------------------------------------------------------------
  var SLIDERS = [
    ['vol', 'Volume', 0, 1.5, 0.01, 'How loud this sound is (1 = as designed).'],
    ['pitch', 'Pitch', 0.5, 2, 0.01, 'Every frequency in the sound times this (1 = as designed; 2 = an octave up).'],
    ['len', 'Length', 0.4, 2.5, 0.01, 'The sound\'s length times this.'],
    ['tone', 'Brightness', 0, 1, 0.01, 'A final low-pass: 0 is muffled, 1 is open.'],
  ];
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }

  /**
   * Mount the sound panel. opts:
   *   names: the sounds this studio uses, in order (default: all)
   *   state: { master, sounds: { name: {on, vol, pitch, len, tone, verdict} } } to restore
   *   onChange(state): save it
   *   onVerdict(name, verdict, params): record 'confirmed' or 'redo' in the studio's decision log
   */
  function mountPanel(host, opts) {
    opts = opts || {};
    var names = (opts.names || Object.keys(R)).filter(function (n) { return R[n]; });
    var st = opts.state || {};
    if (st.master != null) masterVol = st.master;
    if (st.sounds) Object.keys(st.sounds).forEach(function (n) { settings[n] = Object.assign({}, st.sounds[n]); });
    function save() {
      if (master) master.gain.setTargetAtTime(soundOn ? masterVol : 0, ctx.currentTime, 0.02);
      if (opts.onChange) opts.onChange({ master: masterVol, sounds: JSON.parse(JSON.stringify(settings)) });
      var cb = host.querySelector('#saOn'); if (cb) cb.checked = soundOn;
    }
    saveHook = save;
    var groups = [];
    names.forEach(function (n) { if (groups.indexOf(R[n].group) < 0) groups.push(R[n].group); });

    host.innerHTML =
      '<div class="sa-row"><label title="Every sound in this studio, on or off (also the Sound button on the stage, and the M key). Remembered in this browser."><input type="checkbox" id="saOn"' + (soundOn ? ' checked' : '') + '> Sound on</label></div>' +
      '<div class="sa-row"><label title="Every sound in this studio.">Master</label><input type="range" id="saMaster" min="0" max="1" step="0.01" value="' + masterVol + '"><span id="saMasterV">' + masterVol.toFixed(2) + '</span></div>' +
      '<p class="hint">Every ability\'s sound, made in the browser (no files): space sounds (deep sub, air, soft bells, a shared reverb). Press <b>Play</b>, shape it, then <b>Confirm</b> or <b>Redo</b>: your verdict goes to the decision log. These brief the game\'s FMOD events, which ship empty until sound design makes them.</p>' +
      '<div class="sa-row"><label title="Show only one group of sounds.">Group</label><select class="arp-seg" id="saGroup"><option value="">All</option>' + groups.map(function (g) { return '<option>' + esc(g) + '</option>'; }).join('') + '</select></div>' +
      '<div id="saList"></div>';
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
          '<button class="btn" data-act="play">' + (r.loop ? 'Play 3 s' : 'Play') + '</button></div>' +
          SLIDERS.map(function (s) { return '<div class="sa-row"><label title="' + esc(s[5]) + '">' + s[1] + '</label><input type="range" data-k="' + s[0] + '" min="' + s[2] + '" max="' + s[3] + '" step="' + s[4] + '" value="' + c[s[0]] + '"><span>' + Number(c[s[0]]).toFixed(2) + '</span></div>'; }).join('') +
          '<div class="sa-row"><button class="btn primary" data-act="confirmed">Confirm</button><button class="btn" data-act="redo">Redo</button><button class="btn" data-act="reset" title="Back to the sound as designed.">Reset</button></div></details>';
      }).join('');
    }
    renderList();
    host.querySelector('#saGroup').addEventListener('change', renderList);
    host.querySelector('#saOn').addEventListener('change', function (e) { unlock(); setOn(e.target.checked); });
    host.querySelector('#saMaster').addEventListener('input', function (e) { masterVol = +e.target.value; host.querySelector('#saMasterV').textContent = masterVol.toFixed(2); save(); });
    list.addEventListener('input', function (e) {
      var card = e.target.closest('.sa-card'); if (!card) return;
      var n = card.getAttribute('data-n'), k = e.target.getAttribute('data-k'); if (!k || k === 'on') return;
      var s = settings[n] || (settings[n] = {});
      s[k] = +e.target.value; var lab = e.target.nextElementSibling; if (lab) lab.textContent = s[k].toFixed(2);
      save();
    });
    list.addEventListener('change', function (e) { if (e.target.getAttribute('data-k') === 'on') { var n = e.target.closest('.sa-card').getAttribute('data-n'); (settings[n] || (settings[n] = {})).on = e.target.checked; save(); } });
    list.addEventListener('click', function (e) {
      var b = e.target.closest('button'); if (!b) return;
      var card = b.closest('.sa-card'), n = card.getAttribute('data-n'), act = b.getAttribute('data-act');
      unlock();
      if (act === 'play') {
        if (!soundOn) { setOn(true); }
        if (R[n].loop) { loop('preview', n, 0); var t0 = Date.now(); var iv = setInterval(function () { var x = Math.min(1, (Date.now() - t0) / 2800); loop('preview', n, x); if (x >= 1) { clearInterval(iv); stop('preview'); } }, 50); }
        else { var was = settings[n] && settings[n].on === false; if (was) settings[n].on = true; play(n, {}); if (was) settings[n].on = false; }
      } else if (act === 'reset') {
        var keep = settings[n] && settings[n].verdict; settings[n] = keep ? { verdict: keep } : {}; save(); renderList();
      } else if (act === 'confirmed' || act === 'redo') {
        (settings[n] || (settings[n] = {})).verdict = act; save(); renderList();
        if (opts.onVerdict) opts.onVerdict(n, act, Object.assign({ label: R[n].label }, cfg(n)));
      }
    });
    return { refresh: renderList };
  }

  function style() {
    if (!root.document || root.document.getElementById('studio-audio-style')) return;
    var s = root.document.createElement('style'); s.id = 'studio-audio-style';
    s.textContent = '.sa-row{display:flex;align-items:center;gap:8px;margin:4px 0}.sa-row>label{min-width:86px}.sa-row input[type=range]{flex:1;min-width:0}' +
      '.sa-row>span{min-width:36px;text-align:right;font-variant-numeric:tabular-nums}.sa-tag{opacity:.6;font-size:.8em}' +
      '.sa-v{font-size:.8em;padding:0 6px;border-radius:6px;border:1px solid var(--line)}.sa-confirmed{color:var(--accent)}.sa-redo{opacity:.75}' +
      '.sa-toggle{position:absolute;top:10px;left:50%;transform:translateX(-50%);z-index:6;font:inherit;font-size:.85rem;font-weight:700;padding:5px 10px;border-radius:6px;' +
      'border:1px solid var(--line);background:var(--panel);color:var(--fg);cursor:pointer;opacity:.9}.sa-toggle[aria-pressed=false]{opacity:.6}';
    root.document.head.appendChild(s);
  }
  if (root.document) { if (root.document.readyState === 'loading') root.document.addEventListener('DOMContentLoaded', style); else style(); }

  root.VesselStudioAudio = {
    play: play, loop: loop, stop: stop, stopAll: stopAll, mountPanel: mountPanel, mountToggle: mountToggle,
    setOn: setOn, isOn: function () { return soundOn; },
    recipes: function () { return Object.keys(R).map(function (n) { return { name: n, label: R[n].label, group: R[n].group, loop: !!R[n].loop }; }); },
    has: function (n) { return !!R[n]; },
    unlock: unlock,
    get ready() { return !!(soundOn && ctx && ctx.state === 'running'); },
  };
})(typeof window !== 'undefined' ? window : this);
