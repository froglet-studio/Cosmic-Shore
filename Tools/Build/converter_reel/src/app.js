// The Converter Reel page: the colour lab, the timeline, shared looks and the exporter.
(function () {
  'use strict';
  const CR = globalThis.CR, A = globalThis.REEL_ASSETS, $ = (id) => document.getElementById(id);
  const X = CR.ExtrasLook || { defaults: {}, spec: [], notModelled: [] };   // the Charge light's and the Mass prisms' looks (reelExtras.js)
  const LAB = { name: 'Converter Reel', round: 1, doc: 'Docs/Studios/CONVERTER_REEL_LAB.md' };

  // ================= SHIPPED: the game's numbers this page reads, and the lab's own defaults (marked lab) =========
  const MAT = A.materials;
  const SHIPPED = {
    // OmniCrystalBody.mat (OmniCrystalFresnelShader): the body every crystal but Mass wears
    bodyBright: MAT.body.bright,     // _BrightColor
    bodyDark: MAT.body.dark,         // _DarkColor
    bodyGain: 1,                     // lab: HDR multiplier on _BrightColor (1 = the material)
    spread: MAT.body.spread,         // _Spread (world units)
    tint: 0,                         // lab: 0 = every element wears the body pair (the game); 1 = each its own hue
    elSpace: '#5b8dff', elCharge: '#ffd23f', elMass: '#ff5a4f', elTime: '#b07cff',   // lab: not game colours
    // the Charge light (OmniChargeArcs.mat) and the Mass prisms (MassDepthPrisms.mat / OmniDepthPrisms.mat)
    ...X.defaults,
    // lab: the frame around the crystals
    bgTop: '#03050a', bgBottom: '#0a1018', glowColor: '#16301f', glowSize: 0.34, glowStrength: 0.7,
    stars: 0.10, starBright: 1.3, vignette: 0.35,
    exposure: 0, bloomStrength: 0.55, bloomRadius: 1.0, bloomThreshold: 0.75, tonemap: 2, saturation: 1, contrast: 1, grain: 0,
    // ElementalConverterView (camera) and the loop (lab)
    fov: 34,            // fieldOfView
    pitch: 18,          // pitchDegrees
    framing: 1.15,      // framing
    framingEase: 1.6,   // framingEaseSeconds
    orbitTurns: 1,      // lab: whole camera turns per loop (the game circles at 5 deg/s; one turn per loop is about that)
    orbitStart: 0,      // lab
    framingMode: 2,     // lab: 0 the game's sphere fitted to the height; 1 the sphere fitted to height and width; 2 tight
    frameRaised: 1,     // lab: 1 = the woken omni's raised triangle prisms count in the framing (the game frames the body only)
    seed: 7,            // lab: which play the game's ranges draw (OmniCombineConfig.asset)
    holdCycles: 1,      // lab: cadence cycles (8.33 s each) the woken omni holds before it lets go
    openIdle: 1.5,      // lab: seconds the four idle before the first sets off
    closeIdle: 1.0,     // lab: seconds the four idle after the last is home
    pause: 0.4,         // lab: seconds the landed crystals hold before Space sets off back out
    playSpeed: 1,       // lab: video speed (1 = the game's own timing)
    // export
    format: 0, fps: 60, supersample: 2, quality: 1, codec: 0, loops: 1,
    caption: 0, captionText: 'ELEMENTAL CONVERTER', captionColor: '#99ff80', captionSub: 'COSMIC SHORE', captionPos: 0,
    safeZones: 0,
  };
  const FORMATS = [
    { name: 'Vertical 9:16 · 1080×1920', w: 1080, h: 1920, use: 'Reels, TikTok, Shorts, Stories' },
    { name: 'Portrait 4:5 · 1080×1350', w: 1080, h: 1350, use: 'Instagram and Facebook feed' },
    { name: 'Square 1:1 · 1080×1080', w: 1080, h: 1080, use: 'feeds everywhere' },
    { name: 'Landscape 16:9 · 1920×1080', w: 1920, h: 1080, use: 'YouTube, X, LinkedIn, Steam' },
    { name: 'Vertical 9:16 4K · 2160×3840', w: 2160, h: 3840, use: 'masters, trailers' },
    { name: 'Landscape 16:9 4K · 3840×2160', w: 3840, h: 2160, use: 'masters, trailers' },
  ];
  const QUALITY = [{ name: 'Standard', bpp: 0.1 }, { name: 'High', bpp: 0.2 }, { name: 'Master', bpp: 0.4 }];
  const ELEMENTS = ['Space', 'Charge', 'Mass', 'Time'];

  // [group, key, lo, hi, step, label, tip, kind?, options?]
  const SPEC = [
    ['Body', 'bodyBright', null, null, null, 'Rim colour', 'OmniCrystalBody _BrightColor: faces turned away from the camera, and the rims.', 'color'],
    ['Body', 'bodyDark', null, null, null, 'Face colour', 'OmniCrystalBody _DarkColor: faces looking straight at the camera.', 'color'],
    ['Body', 'bodyGain', 0.2, 4, 0.05, 'Rim intensity', 'Lab: multiplies the rim colour. Above 1 it blooms.'],
    ['Body', 'spread', 0, 0.2, 0.005, 'Spread', 'OmniCrystalBody _Spread: pushes every face out along its normal, in world units (the omni is 40 units across).'],
    ['Elements', 'tint', 0, 1, 0.05, 'Element tint', 'Lab only. 0: all four wear the body colour, as in the game (element is shape, not colour). 1: each wears its own hue.'],
    ['Elements', 'elSpace', null, null, null, 'Space', 'Lab only: Space\'s hue when tinted.', 'color'],
    ['Elements', 'elCharge', null, null, null, 'Charge', 'Lab only: Charge\'s hue when tinted.', 'color'],
    ['Elements', 'elMass', null, null, null, 'Mass', 'Lab only: Mass\'s hue when tinted.', 'color'],
    ['Elements', 'elTime', null, null, null, 'Time', 'Lab only: Time\'s hue when tinted.', 'color'],
    ...X.spec,
    ['Backdrop', 'bgTop', null, null, null, 'Sky top', 'Lab: the top of the backdrop gradient. In the game the converter shows the menu skybox.', 'color'],
    ['Backdrop', 'bgBottom', null, null, null, 'Sky bottom', 'Lab: the bottom of the backdrop gradient.', 'color'],
    ['Backdrop', 'glowColor', null, null, null, 'Halo', 'Lab: a soft glow behind the crystal.', 'color'],
    ['Backdrop', 'glowSize', 0.05, 1, 0.01, 'Halo size', 'Lab: the halo\'s radius, as a share of the frame height.'],
    ['Backdrop', 'glowStrength', 0, 3, 0.05, 'Halo strength', 'Lab.'],
    ['Backdrop', 'stars', 0, 0.4, 0.01, 'Stars', 'Lab: share of sky cells holding a star. The stars are fixed to the sky, so they turn with the camera.'],
    ['Backdrop', 'starBright', 0, 4, 0.05, 'Star brightness', 'Lab.'],
    ['Backdrop', 'vignette', 0, 1, 0.01, 'Vignette', 'Lab: darkens the corners.'],
    ['Grade', 'exposure', -3, 3, 0.05, 'Exposure (stops)', 'Lab.'],
    ['Grade', 'bloomStrength', 0, 3, 0.05, 'Bloom', 'Lab: how much light bleeds from bright pixels (the game camera\'s post stack is not modelled).'],
    ['Grade', 'bloomThreshold', 0, 2, 0.05, 'Bloom threshold', 'Lab: brightness where bloom starts.'],
    ['Grade', 'bloomRadius', 0.3, 2.5, 0.05, 'Bloom radius', 'Lab.'],
    ['Grade', 'tonemap', 0, 3, 1, 'Tonemap', 'Lab: how HDR folds into the frame.', 'select', ['Clamp', 'ACES', 'Neutral (Khronos)', 'Reinhard']],
    ['Grade', 'saturation', 0, 2, 0.05, 'Saturation', 'Lab.'],
    ['Grade', 'contrast', 0.5, 1.6, 0.01, 'Contrast', 'Lab.'],
    ['Grade', 'grain', 0, 8, 0.5, 'Grain', 'Lab: film grain in 8-bit steps. Grain costs bitrate after the platform re-encodes; keep it low.'],
    ['Camera', 'fov', 15, 70, 1, 'Field of view', 'ElementalConverterView.fieldOfView (vertical degrees).'],
    ['Camera', 'pitch', -60, 60, 1, 'Pitch', 'ElementalConverterView.pitchDegrees: how far the camera looks down.'],
    ['Camera', 'framing', 0.5, 3, 0.01, 'Framing', 'ElementalConverterView.framing: distance as a multiple of the distance that just fits what is on stage now. Under Tight, 1.15 leaves about 13% of margin.'],
    ['Camera', 'framingEase', 0.1, 5, 0.1, 'Framing ease (s)', 'ElementalConverterView.framingEaseSeconds: the dolly in as each crystal lands.'],
    ['Camera', 'orbitTurns', 0, 3, 1, 'Orbit turns / loop', 'Lab: whole turns, so the camera comes back round for the loop. 0 holds still.'],
    ['Camera', 'orbitStart', 0, 359, 1, 'Orbit start', 'Lab: where round the crystal the loop starts, degrees.'],
    ['Camera', 'frameRaised', 0, 1, 1, 'Raised prisms', 'Lab: 1 keeps the woken omni\'s raised triangle prisms inside the frame. 0 frames the body only, as the game does, and lets them run off the edges.', 'select', ['Body only (game)', 'Keep in frame']],
    ['Camera', 'framingMode', 0, 2, 1, 'Fit', 'How the camera frames the stage. Game: a sphere round everything, fitted to the frame\'s height (ElementalConverterView). Sphere, both: the same sphere fitted to the narrower side. Tight (lab): what the camera actually sees, in both directions, pulled back early so nothing leaves the frame.', 'select', ['Game: sphere, height', 'Sphere, both sides', 'Tight (lab)']],
    ['Loop', 'seed', 1, 999, 1, 'Play', 'Lab: which play the game\'s ranges draw (OmniCombineConfig.asset: lead, spread, flight, arc, swirl, tumble, site spread, suck, socket). Every press in the game draws a new one.'],
    ['Loop', 'holdCycles', 0, 4, 1, 'Omni hold (cycles)', 'Lab: cadence cycles the woken omni plays (pentagon turn, wave, triangle turn, wave: 8.33 s) before it lets go.'],
    ['Loop', 'openIdle', 0.3, 6, 0.1, 'Idle before (s)', 'Lab: seconds the four idle before Space sets off.'],
    ['Loop', 'closeIdle', 0.3, 6, 0.1, 'Idle after (s)', 'Lab: seconds the four idle once Time is home.'],
    ['Loop', 'pause', 0, 4, 0.1, 'Pause before pull-out (s)', 'Lab: the landed crystals hold this long after the omni lets go.'],
    ['Loop', 'playSpeed', 0.5, 2, 0.05, 'Video speed', 'Lab: 1 plays at the game\'s own timing. The preview speed below does not change the video.'],
    ['Format', 'format', 0, FORMATS.length - 1, 1, 'Size', 'Resolution and aspect of the exported video.', 'select', FORMATS.map((f) => f.name)],
    ['Format', 'fps', 24, 60, 1, 'Frame rate', 'Frames per second.', 'select', ['24', '30', '60'], [24, 30, 60]],
    ['Format', 'supersample', 1, 3, 1, 'Anti-aliasing', 'Rendered pixels per output pixel along each edge, on top of 4x MSAA. The game renders the converter at 2x.', 'select', ['MSAA only', '2× supersample', '3× supersample']],
    ['Format', 'quality', 0, 2, 1, 'Bitrate', 'Standard about 12 Mbps, High about 25, Master about 50 at 1080×1920 60 fps.', 'select', QUALITY.map((q) => q.name)],
    ['Format', 'codec', 0, 1, 1, 'File', 'MP4 (H.264) plays everywhere and every platform takes it. WebM (VP9) only where H.264 encoding is missing.', 'select', ['MP4 · H.264', 'WebM · VP9']],
    ['Format', 'loops', 1, 3, 1, 'Loops in the file', 'Platforms loop a video themselves; one loop is usually right.'],
    ['Format', 'safeZones', 0, 1, 1, 'Show app safe zones', 'Preview only: shades where Reels/TikTok put their own buttons and captions.', 'select', ['Off', 'On']],
    ['Caption', 'caption', 0, 1, 1, 'Caption', 'Lab: text burned into the video. Off by default: the converter screen\'s CONVERT button is never drawn.', 'select', ['Off', 'On']],
    ['Caption', 'captionText', null, null, null, 'Title', 'The large line (the converter screen\'s own title by default).', 'text'],
    ['Caption', 'captionSub', null, null, null, 'Second line', 'Smaller, under the title.', 'text'],
    ['Caption', 'captionColor', null, null, null, 'Colour', 'ElementalConverterView.titleColor by default.', 'color'],
    ['Caption', 'captionPos', 0, 1, 1, 'Position', '', 'select', ['Top', 'Bottom']],
  ];
  const TABS = [
    { name: 'Look', groups: ['Body', 'Elements', ...new Set(X.spec.map((r) => r[0])), 'Backdrop', 'Grade'] },
    { name: 'Motion', groups: ['Loop', 'Camera'] },
    { name: 'Export', groups: ['Format', 'Caption'] },
    { name: 'Saved', groups: [] },
    { name: 'Check', groups: [] },
  ];
  const MOTION_KEYS = new Set(['framingMode', 'format', 'frameRaised', 'seed', 'holdCycles', 'openIdle', 'closeIdle', 'pause', 'orbitTurns', 'orbitStart', 'pitch', 'fov', 'framing', 'framingEase']);

  // Starting points for the look. "Game" is the shipped materials on a dark sky; the domain looks put the body in
  // one of the game's four domain colours (studio-domains.js); the rest are lab proposals.
  const D = A.domains;
  const PRESETS = [
    { id: 'game', name: 'Game', blurb: 'The shipped materials on a dark sky', v: {} },
    { id: 'elements', name: 'Element hues', blurb: 'Each element its own colour (lab)', v: { tint: 0.85, glowColor: '#1a1f33' } },
    ...D.filter((d) => d.key !== 'jade').map((d) => ({ id: d.key, name: d.name, blurb: 'The ' + d.name + ' domain colour', v: { bodyBright: d.color, glowColor: mixHex(d.color, '#000000', 0.82) } })),
    { id: 'jade', name: 'Jade', blurb: 'The Jade domain colour', v: { bodyBright: D.find((d) => d.key === 'jade').color, glowColor: '#0c2a2a' } },
    { id: 'abyss', name: 'Abyss', blurb: 'Teal ice in deep water (lab)', v: { bodyBright: '#5ef2ff', bgTop: '#010409', bgBottom: '#03141c', glowColor: '#06303a', glowStrength: 0.9, stars: 0.05, bloomStrength: 0.8 } },
    { id: 'solar', name: 'Solar', blurb: 'Gold on warm black (lab)', v: { bodyBright: '#ffbe3d', bodyGain: 1.3, bgTop: '#070402', bgBottom: '#160c05', glowColor: '#3a1d06', stars: 0.04, tonemap: 1 } },
    { id: 'uv', name: 'Ultraviolet', blurb: 'Magenta rims, violet haze (lab)', v: { bodyBright: '#ff4fd8', bgTop: '#06020c', bgBottom: '#12061f', glowColor: '#2c0b40', glowStrength: 1.1, bloomStrength: 0.9 } },
    { id: 'paper', name: 'Daylight', blurb: 'Dark glass on a pale ground (lab)', v: { bodyBright: '#1b2a20', bodyDark: '#e9f1ea', bgTop: '#eef3ef', bgBottom: '#cfdad3', glowColor: '#ffffff', glowStrength: 0.4, stars: 0, vignette: 0.15, bloomStrength: 0.15, bloomThreshold: 1.2 } },
  ];
  function mixHex(a, b, t) {
    const pa = parseInt(a.slice(1), 16), pb = parseInt(b.slice(1), 16), ch = (n, s) => (n >> s) & 255;
    const m = (s) => Math.round(ch(pa, s) + (ch(pb, s) - ch(pa, s)) * t);
    return '#' + ((1 << 24) | (m(16) << 16) | (m(8) << 8) | m(0)).toString(16).slice(1);
  }

  // ================= state =================
  const LS_KEY = 'lab:converter-reel:r1:P';
  const lsGet = (k, d) => { try { const v = localStorage.getItem(k); return v ? JSON.parse(v) : d; } catch (e) { return d; } };
  const lsSet = (k, v) => { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) { /* private window */ } };
  const P = Object.assign({}, SHIPPED, lsGet(LS_KEY, {}));
  for (const k of Object.keys(P)) if (!(k in SHIPPED)) delete P[k];
  let preset = lsGet(LS_KEY + ':preset', 'game');
  const diffOnly = () => { const o = {}; for (const k of Object.keys(SHIPPED)) if (P[k] !== SHIPPED[k]) o[k] = P[k]; return o; };
  const persist = () => { lsSet(LS_KEY, diffOnly()); lsSet(LS_KEY + ':preset', preset); };

  // ================= the reel =================
  const reel = new CR.Reel({ data: A.data, config: A.config, cadence: A.cadence, depth: A.depth, arcs: A.arcs });
  const canvas = $('view');
  const renderer = new CR.Render.Renderer(canvas, reel);
  if (reel.extras && reel.extras.layers) reel.extras.layers(renderer);
  const sim = { tau: 0, playing: true, previewSpeed: 1, quality: 1 };
  let manualClock = false;

  function motion() {
    return { seed: P.seed, holdCycles: P.holdCycles, openIdle: P.openIdle, closeIdle: P.closeIdle, pause: P.pause,
      orbitTurns: P.orbitTurns, orbitStart: P.orbitStart, pitch: P.pitch, fov: P.fov, framing: P.framing, framingEase: P.framingEase, frameRaised: P.frameRaised === 1, framingMode: P.framingMode, aspect: FORMATS[P.format].w / FORMATS[P.format].h };
  }
  function replan() {
    const T = reel.plan(motion());
    sim.tau = Math.min(sim.tau, T.L - 1e-6);
    drawBands();
    $('hLoop').textContent = fmtTime(T.L / P.playSpeed);
  }

  function look() {
    const L = Object.assign({}, P);
    L.elementColors = [P.elSpace, P.elCharge, P.elMass, P.elTime];
    return L;
  }

  // ================= caption overlay =================
  const overlay = document.createElement('canvas');
  function drawOverlay(w, h) {
    if (!P.caption) { renderer.setOverlay(null); return; }
    overlay.width = w; overlay.height = h;
    const g = overlay.getContext('2d');
    g.clearRect(0, 0, w, h);
    const s = Math.min(w, h) / 1080, top = P.captionPos === 0;
    g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillStyle = P.captionColor;
    g.shadowColor = 'rgba(0,0,0,0.55)'; g.shadowBlur = 18 * s;
    const y0 = top ? h * 0.085 : h * (1 - 0.13);
    g.font = '600 ' + Math.round(64 * s) + 'px "Chakra Petch", "Arial Narrow", sans-serif';
    if ('letterSpacing' in g) g.letterSpacing = Math.round(8 * s) + 'px';
    g.fillText(P.captionText || '', w / 2, y0);
    if (P.captionSub) {
      g.font = '500 ' + Math.round(28 * s) + 'px "Chakra Petch", "Arial Narrow", sans-serif';
      if ('letterSpacing' in g) g.letterSpacing = Math.round(10 * s) + 'px';
      g.globalAlpha = 0.75; g.fillText(P.captionSub, w / 2, y0 + 62 * s); g.globalAlpha = 1;
    }
    renderer.setOverlay(overlay);
  }

  // ================= preview =================
  function previewSize() {
    const f = FORMATS[P.format], stage = $('stage').getBoundingClientRect();
    const availW = Math.max(120, stage.width - 32), availH = Math.max(120, stage.height - 32);
    const scale = Math.min(availW / f.w, availH / f.h);
    const cssW = Math.floor(f.w * scale), cssH = Math.floor(f.h * scale);
    $('frame').style.width = cssW + 'px'; $('frame').style.height = cssH + 'px';
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    let w = Math.round(cssW * dpr * Math.min(1, sim.quality)), h = Math.round(cssH * dpr * Math.min(1, sim.quality));
    w = Math.min(w, f.w); h = Math.min(h, f.h);
    return { w: Math.max(2, w & ~1), h: Math.max(2, h & ~1), ss: sim.quality >= 2 ? 2 : 1 };
  }
  let previewDirty = true;
  function renderPreview() {
    if (exporting) return;
    const s = previewSize();
    renderer.resize(s.w, s.h, s.ss);
    if (previewDirty) { drawOverlay(s.w, s.h); previewDirty = false; }
    const fr = reel.frame(sim.tau, 1 / 60);
    renderer.draw(fr, look());
    $('chipRes').textContent = s.w + '×' + s.h + (s.ss > 1 ? ' ×' + s.ss : '');
    drawSafe();
    updateTransport(fr);
  }
  let last = performance.now(), fpsAcc = 0, fpsN = 0;
  function loop(now) {
    const dt = Math.min(0.1, (now - last) / 1000); last = now;
    if (!manualClock && sim.playing) sim.tau = (sim.tau + dt * sim.previewSpeed * P.playSpeed) % reel.T.L;
    fpsAcc += dt; fpsN++;
    if (fpsAcc > 0.5) { $('chipFps').textContent = Math.round(fpsN / fpsAcc) + ' fps preview'; fpsAcc = 0; fpsN = 0; }
    if (!manualClock) renderPreview();
    requestAnimationFrame(loop);
  }

  function drawSafe() {
    const el = $('safe');
    el.hidden = !(P.safeZones && FORMATS[P.format].h > FORMATS[P.format].w);
    if (el.hidden || el.childElementCount) return;
    // Reels/TikTok: the top ~14% (account, audio) and bottom ~20% (caption, buttons), and a right rail of buttons.
    el.innerHTML = '<i style="top:0;height:14%"></i><i style="bottom:0;height:20%"></i><i style="top:40%;bottom:20%;left:auto;width:14%"></i>';
  }

  // ================= timeline =================
  function bandRanges() {
    const T = reel.T, p = reel.p, out = [];
    out.push({ c: 'var(--band-idle)', a: 0, b: T.press + 0.3, row: 0, label: 'idle' });
    for (let k = 0; k < 4; k++) {
      const a = CR.Combine.Core.departs(k, p), b = CR.Combine.Core.centred(k, p) + (k > 0 ? p.SocketSeconds : 0);
      out.push({ c: 'var(--band-' + ELEMENTS[k].toLowerCase() + ')', a, b, row: 0, label: ELEMENTS[k] + ' in' });
      out.push({ c: 'var(--band-' + ELEMENTS[k].toLowerCase() + ')', a: T.out[k], b: T.home[k], row: 1, label: ELEMENTS[k] + ' out' });
    }
    out.push({ c: 'var(--band-omni)', a: T.m, b: T.back, row: 0, label: 'omni' });
    out.push({ c: 'var(--band-idle)', a: Math.max(...T.home), b: T.L, row: 1, label: 'idle' });
    return out;
  }
  function drawBands() {
    const el = $('bands'), L = reel.T.L;
    el.innerHTML = '';
    for (const r of bandRanges()) {
      const i = document.createElement('i');
      i.style.left = (100 * r.a / L) + '%'; i.style.width = (100 * Math.max(0, r.b - r.a) / L) + '%';
      i.style.background = r.c; i.style.top = r.row ? '50%' : '0'; i.style.bottom = r.row ? '0' : '50%';
      i.title = r.label + ': ' + fmtTime(r.a / P.playSpeed) + ' to ' + fmtTime(r.b / P.playSpeed);
      el.appendChild(i);
    }
    const head = document.createElement('div'); head.className = 'head'; head.id = 'head'; el.appendChild(head);
  }
  function phaseName(tau) {
    const T = reel.T, p = reel.p;
    if (tau >= T.m && tau < T.back) return tau < T.awake ? 'hand-off' : tau < T.beats0 ? 'omni wakes' : tau < T.beats1 ? 'omni awake' : tau < T.settled ? 'omni settles' : 'hand back';
    for (let k = 3; k >= 0; k--) if (tau >= T.out[k] && tau < T.home[k]) return ELEMENTS[k] + ' leaves';
    for (let k = 3; k >= 0; k--) if (tau >= CR.Combine.Core.departs(k, p) && tau < T.m) return ELEMENTS[k] + ' converts';
    return 'idle';
  }
  function updateTransport() {
    const L = reel.T.L;
    $('tc').textContent = fmtTime(sim.tau / P.playSpeed) + ' / ' + fmtTime(L / P.playSpeed);
    $('phase').textContent = phaseName(sim.tau);
    const h = $('head'); if (h) h.style.left = (100 * sim.tau / L) + '%';
    $('bands').setAttribute('aria-valuenow', sim.tau.toFixed(2));
  }
  function fmtTime(s) { const m = Math.floor(s / 60), r = s - 60 * m; return m + ':' + (r < 10 ? '0' : '') + r.toFixed(2); }
  (function scrub() {
    const el = $('bands');
    const at = (e) => { const r = el.getBoundingClientRect(); sim.tau = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width)) * reel.T.L * 0.99999; if (manualClock) renderPreview(); };
    el.addEventListener('pointerdown', (e) => { el.setPointerCapture(e.pointerId); at(e); el.onpointermove = at; });
    el.addEventListener('pointerup', () => { el.onpointermove = null; });
    el.addEventListener('keydown', (e) => {
      if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') { sim.tau = (sim.tau + (e.key === 'ArrowRight' ? 1 : -1) * (e.shiftKey ? 5 : 1 / 60) + reel.T.L) % reel.T.L; e.preventDefault(); }
      if (e.key === ' ') { togglePlay(); e.preventDefault(); }
    });
  })();
  function togglePlay() { sim.playing = !sim.playing; $('bPlay').textContent = sim.playing ? 'Pause' : 'Play'; }
  $('bPlay').onclick = togglePlay;
  $('speedSel').onchange = (e) => { sim.previewSpeed = +e.target.value; };
  $('qualSel').onchange = (e) => { sim.quality = +e.target.value; previewDirty = true; };

  // ================= the dock =================
  let currentTab = lsGet(LS_KEY + ':tab', 'Look');
  function buildTabs() {
    const bar = $('tabs');
    bar.innerHTML = '';
    for (const t of TABS) {
      const b = document.createElement('button');
      b.type = 'button'; b.role = 'tab'; b.textContent = t.name; b.setAttribute('aria-selected', String(t.name === currentTab));
      b.onclick = () => { currentTab = t.name; lsSet(LS_KEY + ':tab', currentTab); buildTabs(); buildPane(); };
      bar.appendChild(b);
    }
  }
  const fmtNum = (v, step) => (step && step < 1 ? Number(v).toFixed(Math.min(3, String(step).split('.')[1].length)) : String(Math.round(v)));
  function control(row) {
    const [, key, lo, hi, step, label, tip, kind, options, values] = row;
    const wrap = document.createElement('div'), id = 'c-' + key;
    wrap.className = 'row' + (kind === 'color' ? ' color' : '');
    const lab = document.createElement('label'); lab.htmlFor = id; lab.textContent = label; lab.title = tip + '\nshipped: ' + SHIPPED[key];
    const mark = () => lab.classList.toggle('changed', P[key] !== SHIPPED[key]);
    mark();
    wrap.appendChild(lab);
    if (kind === 'color') {
      const inp = document.createElement('input'); inp.type = 'color'; inp.id = id; inp.value = P[key];
      const hex = document.createElement('span'); hex.className = 'hex'; hex.textContent = P[key];
      inp.oninput = () => { set(key, inp.value); hex.textContent = inp.value; mark(); };
      wrap.append(inp, hex);
    } else if (kind === 'select') {
      const sel = document.createElement('select'); sel.id = id;
      options.forEach((o, i) => { const op = document.createElement('option'); op.value = String(values ? values[i] : i); op.textContent = o; sel.appendChild(op); });
      sel.value = String(P[key]);
      sel.onchange = () => { set(key, +sel.value); mark(); };
      wrap.appendChild(sel);
    } else if (kind === 'text') {
      const inp = document.createElement('input'); inp.type = 'text'; inp.id = id; inp.value = P[key]; inp.maxLength = 48;
      inp.oninput = () => { set(key, inp.value); mark(); };
      wrap.appendChild(inp);
    } else {
      const inp = document.createElement('input'); inp.type = 'range'; inp.id = id; inp.min = lo; inp.max = hi; inp.step = step; inp.value = P[key];
      const out = document.createElement('output'); out.htmlFor = id; out.textContent = fmtNum(P[key], step);
      inp.oninput = () => { set(key, +inp.value); out.textContent = fmtNum(+inp.value, step); mark(); };
      inp.ondblclick = () => { inp.value = SHIPPED[key]; inp.oninput(); };
      wrap.append(inp, out);
    }
    wrap.title = tip;
    return wrap;
  }
  let replanTimer = 0;
  function set(key, v) {
    P[key] = v;
    if (MOTION_KEYS.has(key)) { clearTimeout(replanTimer); replanTimer = setTimeout(replan, 120); }
    if (key === 'playSpeed') $('hLoop').textContent = fmtTime(reel.T.L / P.playSpeed);
    if (key.startsWith('caption') || key === 'format' || key === 'safeZones') { previewDirty = true; $('safe').innerHTML = ''; }
    if (key === 'format') updateFormatChip();
    if (!MOTION_KEYS.has(key) && !['format', 'fps', 'supersample', 'quality', 'codec', 'loops', 'caption', 'captionText', 'captionSub', 'captionColor', 'captionPos', 'safeZones', 'playSpeed'].includes(key)) { preset = 'custom'; refreshPresets(); }
    persist(); refreshDiff();
    if (manualClock) renderPreview();
  }
  function refreshDiff() { const n = Object.keys(diffOnly()).length; $('hDiff').textContent = n ? n + ' changed from shipped' : ''; }
  function updateFormatChip() { const f = FORMATS[P.format]; $('hFormat').textContent = f.w + '×' + f.h + ' · ' + P.fps + ' fps'; }

  function buildPane() {
    const pane = $('pane');
    pane.innerHTML = '';
    const tab = TABS.find((t) => t.name === currentTab) || TABS[0];
    if (tab.name === 'Look') pane.appendChild(presetsBlock());
    if (tab.name === 'Motion') pane.appendChild(playBlock());
    for (const g of tab.groups) {
      const rows = SPEC.filter((r) => r[0] === g);
      if (!rows.length) continue;
      const box = document.createElement('div'); box.className = 'group';
      const h = document.createElement('h3'); h.textContent = g;
      const reset = document.createElement('button'); reset.type = 'button'; reset.className = 'btn'; reset.style.padding = '1px 7px'; reset.textContent = 'Reset';
      reset.onclick = () => { for (const r of rows) P[r[1]] = SHIPPED[r[1]]; if (rows.some((r) => MOTION_KEYS.has(r[1]))) replan(); previewDirty = true; persist(); refreshDiff(); buildPane(); };
      h.appendChild(reset);
      box.appendChild(h);
      for (const r of rows) box.appendChild(control(r));
      pane.appendChild(box);
    }
    if (tab.name === 'Export') pane.appendChild(exportBlock());
    if (tab.name === 'Saved') pane.appendChild(savedBlock());
    if (tab.name === 'Check') pane.appendChild(checkBlock());
  }

  // Look presets, each previewed as a tiny swatch of its rim/face/sky.
  function presetsBlock() {
    const box = document.createElement('div'); box.className = 'group';
    box.innerHTML = '<h3>Starting points</h3>';
    const grid = document.createElement('div'); grid.className = 'swatches'; grid.id = 'presetGrid';
    for (const pr of PRESETS) {
      const b = document.createElement('button'); b.type = 'button'; b.className = 'swatch'; b.dataset.id = pr.id;
      const v = Object.assign({}, SHIPPED, pr.v), c = document.createElement('canvas'); c.width = 68; c.height = 68;
      const g = c.getContext('2d'), grad = g.createLinearGradient(0, 0, 0, 68);
      grad.addColorStop(0, v.bgTop); grad.addColorStop(1, v.bgBottom); g.fillStyle = grad; g.fillRect(0, 0, 68, 68);
      const rg = g.createRadialGradient(34, 34, 4, 34, 34, 26); rg.addColorStop(0, v.bodyDark); rg.addColorStop(1, v.tint > 0.5 ? v.elTime : v.bodyBright);
      g.fillStyle = rg; g.beginPath();
      for (let i = 0; i < 6; i++) { const a = Math.PI / 6 + i * Math.PI / 3; g.lineTo(34 + 22 * Math.cos(a), 34 + 22 * Math.sin(a)); }
      g.fill();
      b.append(c);
      const t = document.createElement('div'); t.innerHTML = '<b></b><small></small>'; t.querySelector('b').textContent = pr.name; t.querySelector('small').textContent = pr.blurb;
      b.append(t);
      b.onclick = () => applyPreset(pr);
      grid.appendChild(b);
    }
    box.appendChild(grid);
    setTimeout(refreshPresets, 0);
    return box;
  }
  const LOOK_KEYS = SPEC.filter((r) => ['Body', 'Elements', 'Backdrop', 'Grade', ...X.spec.map((x) => x[0])].includes(r[0])).map((r) => r[1]);
  function applyPreset(pr) {
    for (const k of LOOK_KEYS) P[k] = SHIPPED[k];
    Object.assign(P, pr.v);
    preset = pr.id; persist(); refreshDiff(); buildPane();
    if (manualClock) renderPreview();
  }
  function refreshPresets() { const g = $('presetGrid'); if (g) for (const b of g.children) b.setAttribute('aria-pressed', String(b.dataset.id === preset)); }

  function playBlock() {
    const box = document.createElement('div'); box.className = 'group';
    const p = reel.p, T = reel.T;
    box.innerHTML = '<h3>This play</h3><p class="note">Drawn from the game\'s ranges (OmniCombineConfig.asset), as one CONVERT press draws them. Change <b>Play</b> to draw another.</p>';
    const kv = document.createElement('div'); kv.className = 'kv';
    const rows = [['flight', p.Flight.toFixed(2) + ' s'], ['stagger', p.Stagger.toFixed(2) + ' s'], ['start distance', p.Spread.toFixed(2) + ' R'], ['arc', p.Arc.toFixed(2)], ['swirl', p.Swirl.toFixed(2) + ' turns'], ['tumble', p.Tumble.toFixed(2) + ' turns'],
      ['site spread', p.SiteSpread.toFixed(2)], ['Charge suck', p.Suck.toFixed(2)], ['socket spacing', p.SocketSpacing.toFixed(3)], ['socket', p.SocketSeconds.toFixed(2) + ' s'], ['Mass raise', p.MassRaise.toFixed(2)],
      ['build', fmtTime(T.m - T.press)], ['omni', fmtTime(T.back - T.m)], ['pull-out', fmtTime(Math.max(...T.home) - T.out0)]];
    for (const [k, v] of rows) { const a = document.createElement('span'), b = document.createElement('b'); a.textContent = k; b.textContent = v; kv.append(a, b); }
    box.appendChild(kv);
    return box;
  }

  // ================= export =================
  let exporting = false, cancelExport = false, pendingFile = null;
  function exportBlock() {
    const box = document.createElement('div'); box.className = 'group';
    const f = FORMATS[P.format], frames = Math.round(reel.T.L / P.playSpeed * P.fps) * P.loops;
    const mbps = QUALITY[P.quality].bpp * f.w * f.h * P.fps / 1e6;
    box.innerHTML = '<h3>The file</h3>';
    const kv = document.createElement('div'); kv.className = 'kv';
    for (const [k, v] of [['for', f.use], ['length', fmtTime(frames / P.fps)], ['frames', String(frames)], ['bitrate', mbps.toFixed(1) + ' Mbps'], ['about', Math.round(mbps * frames / P.fps / 8) + ' MB'], ['seamless', 'last frame flows into the first']]) {
      const a = document.createElement('span'), b = document.createElement('b'); a.textContent = k; b.textContent = v; kv.append(a, b);
    }
    box.appendChild(kv);
    const n = document.createElement('p'); n.className = 'note';
    n.textContent = 'Every frame is rendered at the export size and timed exactly, so the file is smooth at its frame rate whatever this device manages live. Inside claude.ai the file is offered through a save prompt; if a save is refused here, open the repo copy in Chrome.';
    box.appendChild(n);
    return box;
  }

  async function chooseCodec(w, h, fps, bitrate) {
    if (typeof VideoEncoder === 'undefined') return null;
    const avc = ['avc1.640034', 'avc1.640033', 'avc1.640032', 'avc1.64002A', 'avc1.4D4034', 'avc1.42E034'];
    const vp9 = ['vp09.00.51.08', 'vp09.00.41.08', 'vp09.00.40.08'];
    const tryList = async (list, kind) => {
      for (const codec of list) {
        const cfg = { codec, width: w, height: h, bitrate, framerate: fps, latencyMode: 'quality', bitrateMode: 'variable' };
        if (kind === 'avc') cfg.avc = { format: 'avc' };
        try { const s = await VideoEncoder.isConfigSupported(cfg); if (s.supported) return { kind, cfg: s.config || cfg }; } catch (e) { /* next */ }
      }
      return null;
    };
    if (P.codec === 0) return (await tryList(avc, 'avc')) || (await tryList(vp9, 'vp9'));
    return (await tryList(vp9, 'vp9')) || (await tryList(avc, 'avc'));
  }

  async function exportVideo() {
    if (exporting) return;
    const f = FORMATS[P.format], fps = P.fps, w = f.w, h = f.h;
    const perLoop = Math.round(reel.T.L / P.playSpeed * fps), N = perLoop * P.loops;
    const bitrate = Math.round(QUALITY[P.quality].bpp * w * h * fps);
    showBusy('Exporting', 'Checking the encoder…');
    const pick = await chooseCodec(w, h, fps, bitrate);
    if (!pick) { busyMessage('This browser cannot encode video (no WebCodecs). Open the page in Chrome or Edge on a computer.', true); return; }
    const mp4 = P.codec === 0 || pick.kind === 'avc';
    const Mux = mp4 ? globalThis.Mp4Muxer : globalThis.WebMMuxer;
    if (!Mux) { busyMessage('The muxer library did not load (it comes from cdn.jsdelivr.net). Check the connection and try again.', true); return; }
    exporting = true; cancelExport = false;
    const muxer = mp4
      ? new Mux.Muxer({ target: new Mux.ArrayBufferTarget(), video: { codec: pick.kind === 'avc' ? 'avc' : 'vp9', width: w, height: h, frameRate: fps }, fastStart: 'in-memory' })
      : new Mux.Muxer({ target: new Mux.ArrayBufferTarget(), video: { codec: 'V_VP9', width: w, height: h, frameRate: fps } });
    let failed = null;
    const encoder = new VideoEncoder({ output: (chunk, meta) => muxer.addVideoChunk(chunk, meta), error: (e) => { failed = e; } });
    encoder.configure(pick.cfg);
    renderer.resize(w, h, P.supersample);
    drawOverlay(w, h);
    const L = look(), t0 = performance.now();
    try {
      for (let i = 0; i < N && !cancelExport && !failed; i++) {
        const tau = ((i % perLoop) / fps) * P.playSpeed;
        renderer.draw(reel.frame(tau, P.playSpeed / fps), L);
        const vf = new VideoFrame(canvas, { timestamp: Math.round(i * 1e6 / fps), duration: Math.round(1e6 / fps) });
        encoder.encode(vf, { keyFrame: i % (fps * 2) === 0 });
        vf.close();
        while (encoder.encodeQueueSize > 6) await new Promise((r) => setTimeout(r, 1));
        if (i % 4 === 0) {
          const el = (performance.now() - t0) / 1000, eta = el / (i + 1) * (N - i - 1);
          busyProgress((i + 1) / N, 'Frame ' + (i + 1) + ' of ' + N + ' · about ' + Math.ceil(eta) + ' s left');
          await new Promise((r) => setTimeout(r, 0));
        }
      }
      if (failed) throw failed;
      if (cancelExport) { encoder.close(); busyMessage('Cancelled.', true); return; }
      busyProgress(1, 'Finishing the file…');
      await encoder.flush();
      muxer.finalize();
      const blob = new Blob([muxer.target.buffer], { type: mp4 ? 'video/mp4' : 'video/webm' });
      const name = 'converter-reel-' + f.w + 'x' + f.h + '-' + fps + 'fps-play' + P.seed + '-' + (preset || 'custom') + (mp4 ? '.mp4' : '.webm');
      pendingFile = { name, blob };
      const vp9Note = pick.kind === 'vp9' && P.codec === 0 ? ' This browser has no H.264 encoder, so the file holds VP9, which some apps refuse. For Instagram and TikTok, export from Chrome or Edge on a computer, which encode H.264.' : '';
      busyMessage('Ready: ' + name + ' (' + (blob.size / 1e6).toFixed(1) + ' MB, ' + (pick.kind === 'avc' ? 'H.264' : 'VP9') + ').' + vp9Note, false, true);
    } catch (e) {
      busyMessage('The encoder stopped: ' + (e && e.message || e) + '. Try a smaller size or the other file type.', true);
    } finally {
      exporting = false; previewDirty = true;
      try { encoder.state !== 'closed' && encoder.close(); } catch (e) { /* closed */ }
    }
  }

  async function saveFile(file) {
    const dl = globalThis.claude && globalThis.claude.use ? await globalThis.claude.use('downloads').catch(() => null) : null;
    if (dl) {
      try { await dl.save({ filename: file.name, data: file.blob }); return 'Saved.'; }
      catch (e) { if (e && e.code === 'declined') return 'Not saved.'; if (e && e.code !== 'unavailable') return 'Save refused (' + (e.code || e) + ').'; }
    }
    const a = document.createElement('a'); a.href = URL.createObjectURL(file.blob); a.download = file.name;
    document.body.appendChild(a); a.click(); setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 4000);
    return 'Saved to your downloads.';
  }

  function showBusy(title, note) { $('busy').hidden = false; $('busyTitle').textContent = title; $('busyNote').textContent = note; $('busyBar').style.width = '0'; $('busySave').hidden = true; $('busyCancel').textContent = 'Cancel'; }
  function busyProgress(f, note) { $('busyBar').style.width = (100 * f).toFixed(1) + '%'; $('busyNote').textContent = note; }
  function busyMessage(note, closeOnly, canSave) { $('busyNote').textContent = note; $('busySave').hidden = !canSave; $('busyCancel').textContent = closeOnly || canSave ? 'Close' : 'Cancel'; }
  $('busyCancel').onclick = () => { if (exporting) cancelExport = true; else $('busy').hidden = true; };
  $('busySave').onclick = async () => { if (!pendingFile) return; $('busyNote').textContent = await saveFile(pendingFile); };
  $('bExport').onclick = exportVideo;
  $('bStill').onclick = async () => {
    const f = FORMATS[P.format];
    renderer.resize(f.w, f.h, P.supersample); drawOverlay(f.w, f.h);
    renderer.draw(reel.frame(sim.tau, 1 / 60), look());
    const blob = await new Promise((r) => canvas.toBlob(r, 'image/png'));
    previewDirty = true;
    showBusy('Still', '');
    pendingFile = { name: 'converter-reel-' + f.w + 'x' + f.h + '-' + sim.tau.toFixed(2) + 's.png', blob };
    busyMessage('Ready: ' + pendingFile.name + ' (' + (blob.size / 1e6).toFixed(1) + ' MB).', false, true);
  };

  // ================= saved looks (shared) =================
  let db = null, me = null, looksCache = [];
  function savedBlock() {
    const box = document.createElement('div'); box.className = 'group';
    box.innerHTML = '<h3>Team looks</h3><p class="note" id="savedNote"></p>';
    const row = document.createElement('div'); row.className = 'row';
    row.innerHTML = '<label for="lookName">Name</label><input type="text" id="lookName" maxlength="40" placeholder="Name this look">';
    const save = document.createElement('button'); save.type = 'button'; save.className = 'btn primary'; save.textContent = 'Save this look for the team';
    save.onclick = saveLook;
    const list = document.createElement('div'); list.className = 'looks'; list.id = 'lookList';
    box.append(row, save, list);
    setTimeout(renderLooks, 0);
    return box;
  }
  function snapshotLook() { return { preset, settings: diffOnly() }; }
  async function saveLook() {
    const name = ($('lookName').value || '').trim() || 'Untitled look';
    const doc = { name, ...snapshotLook(), createdAt: new Date().toISOString(), by: me };
    if (db) { try { await db.collection('looks').add(doc); $('savedNote').textContent = 'Saved for everyone who opens this page.'; return; } catch (e) { $('savedNote').textContent = 'Could not save to the team list (' + (e.code || e) + '); kept in this browser.'; } }
    const local = lsGet(LS_KEY + ':looks', []); local.unshift(doc); lsSet(LS_KEY + ':looks', local.slice(0, 40)); looksCache = mergeLooks([]); renderLooks();
  }
  function mergeLooks(remote) { return remote.concat(lsGet(LS_KEY + ':looks', []).map((l) => Object.assign({ local: true }, l))); }
  function renderLooks() {
    const list = $('lookList'), note = $('savedNote');
    if (!list) return;
    if (note && !note.textContent) note.textContent = db ? 'Looks saved here are shared with everyone who opens this page.' : 'Saved in this browser only (the team list needs the page opened in claude.ai, signed in).';
    list.innerHTML = '';
    if (!looksCache.length) { const p = document.createElement('p'); p.className = 'note'; p.textContent = 'No saved looks yet. Set a look you like, name it and save it; it shows up here for the team.'; list.appendChild(p); return; }
    for (const l of looksCache) {
      const d = document.createElement('div'); d.className = 'look';
      const t = document.createElement('div'); t.innerHTML = '<b></b><small></small>';
      t.querySelector('b').textContent = l.name;
      t.querySelector('small').textContent = Object.keys(l.settings || {}).length + ' settings · ' + (l.local ? 'this browser' : 'team') + ' · ' + String(l.createdAt || '').slice(0, 10);
      const b = document.createElement('button'); b.type = 'button'; b.className = 'btn'; b.textContent = 'Apply';
      b.onclick = () => { Object.assign(P, SHIPPED, l.settings || {}); preset = l.preset || 'custom'; replan(); previewDirty = true; persist(); refreshDiff(); buildPane(); };
      d.append(t, b); list.appendChild(d);
    }
  }
  (async function connect() {
    try {
      if (!globalThis.claude || !globalThis.claude.use) { looksCache = mergeLooks([]); return; }
      const [d, u] = await Promise.all([globalThis.claude.use('db'), globalThis.claude.use('user')]);
      db = d; me = u && u.id ? await u.id() : null;
      if (!db) { looksCache = mergeLooks([]); renderLooks(); return; }
      db.collection('looks').orderBy('createdAt', 'desc').limit(60).onSnapshot((snap) => { looksCache = mergeLooks(snap.docs.map((x) => x.data())); renderLooks(); }, () => {});
    } catch (e) { looksCache = mergeLooks([]); }
  })();

  // ================= the check: the loop closes, the order is first in first out =================
  // The yardstick: the largest frame-to-frame jump of any vertex across the loop point (last frame -> first), as a
  // multiple of the largest jump between two ordinary idle frames. A seamless loop reads about 1. The control plays
  // the idle effects on one clock that restarts with the loop (what a naive cut does) and must read far above 1.
  function poseSnapshot(tau, naive) {
    const out = [];
    for (let k = 0; k < 4; k++) {
      if (naive) reel.naiveClock = true;
      reel.poseCrystal(k, tau, reel.camDir(tau));
      reel.naiveClock = false;
      out.push(Float32Array.from(reel.poses[k]));
    }
    return out;
  }
  // Shape distance (symmetric Hausdorff per crystal), not vertex distance: a Space spin or a Time flip lands on a
  // congruent shape and snaps its vertices back to rest, which a per-vertex measure reads as a jump nobody can see.
  function maxJump(a, b) {
    let m = 0;
    for (let k = 0; k < 4; k++) for (const [P, Q] of [[a[k], b[k]], [b[k], a[k]]]) for (let i = 0; i < P.length; i += 3) {
      let best = Infinity;
      for (let j = 0; j < Q.length; j += 3) { const dx = P[i] - Q[j], dy = P[i + 1] - Q[j + 1], dz = P[i + 2] - Q[j + 2], d = dx * dx + dy * dy + dz * dz; if (d < best) best = d; }
      m = Math.max(m, Math.sqrt(best));
    }
    return m;
  }
  function score(fps) {
    fps = fps || 60;
    const T = reel.T, dt = 1 / fps, R = reel.R;
    const last = T.L - dt;
    const seam = maxJump(poseSnapshot(last), poseSnapshot(0));
    const naive = maxJump(poseSnapshot(last, true), poseSnapshot(0, true));
    let idle = 0;
    for (let t = 0.2; t < T.press - dt; t += 0.37) idle = Math.max(idle, maxJump(poseSnapshot(t), poseSnapshot(t + dt)));
    const order = [0, 1, 2, 3].sort((a, b) => T.out[a] - T.out[b]).map((k) => ELEMENTS[k]).join(' > ');
    return { loopSeconds: +T.L.toFixed(3), seamR: +(seam / R).toFixed(5), idleStepR: +(idle / R).toFixed(5), seamOverIdle: +(seam / Math.max(1e-9, idle)).toFixed(2), naiveOverIdle: +(naive / Math.max(1e-9, idle)).toFixed(2), outOrder: order, inOrder: 'Space > Charge > Mass > Time' };
  }
  function runBatch() {
    const keep = P.seed, rows = [];
    for (const s of [7, 21, 64]) { P.seed = s; replan(); const r = score(60); rows.push({ seed: s, loop: r.loopSeconds, seamOverIdle: r.seamOverIdle, naiveOverIdle: r.naiveOverIdle }); }
    P.seed = keep; replan();
    return rows;
  }
  function checkBlock() {
    const box = document.createElement('div'); box.className = 'group';
    box.innerHTML = '<h3>Does it loop?</h3><p class="note">How far the crystals\' shapes move from the last frame to the first, against the most they move between two idle frames. About 1 is seamless. The control restarts every idle effect with the loop, which is what cutting the video at any point would do.</p>';
    const r = score(P.fps), t = document.createElement('table'); t.className = 'score';
    const ok = r.seamOverIdle <= 1.5, ctl = r.naiveOverIdle > 3;
    t.innerHTML = '<tr><th>measure</th><th>value</th><th></th></tr>'
      + '<tr><td>loop seam / idle step</td><td>' + r.seamOverIdle + '</td><td class="' + (ok ? 'pass' : 'fail') + '">' + (ok ? 'seamless' : 'jumps') + '</td></tr>'
      + '<tr><td>control: naive cut / idle step</td><td>' + r.naiveOverIdle + '</td><td class="' + (ctl ? 'pass' : 'fail') + '">' + (ctl ? 'jumps, as it must' : 'yardstick blind') + '</td></tr>'
      + '<tr><td>in</td><td colspan="2">' + r.inOrder + '</td></tr><tr><td>out</td><td colspan="2">' + r.outOrder + '</td></tr>'
      + '<tr><td>loop</td><td colspan="2">' + fmtTime(r.loopSeconds / P.playSpeed) + '</td></tr>';
    box.appendChild(t);
    const nm = document.createElement('div'); nm.className = 'group';
    nm.innerHTML = '<h3>What this does not model</h3><ul class="plain">'
      + '<li>The menu skybox behind the converter, and the game camera\'s post stack (bloom, tonemapping). The backdrop and grade here are lab choices.</li>'
      + '<li>The pull-out does not exist in the game. It is the game\'s own pose run backwards per crystal, aimed at the camera the same way the build is.</li>'
      + '<li>Colour is judged on this screen. Phones and platforms re-encode; check the exported file on a phone before posting.</li>'
      + '<li>The game draws a new play on every press; the reel shows the one the Play number draws, with a seeded generator instead of System.Random.</li>'
      + X.notModelled.map((s) => '<li>' + s + '</li>').join('') + '</ul>';
    box.appendChild(nm);
    return box;
  }

  // ================= hooks =================
  globalThis.__lab = {
    get manualClock() { return manualClock; }, set manualClock(v) { manualClock = !!v; },
    tick(dt, n) { dt = dt == null ? 1 / 60 : dt; n = n == null ? 1 : n; for (let i = 0; i < n; i++) sim.tau = (sim.tau + dt * P.playSpeed) % reel.T.L; renderPreview(); return sim.tau; },
    reset(seed) { if (seed != null) { P.seed = seed | 0; replan(); } sim.tau = 0; renderPreview(); },
    score: () => score(60), runBatch, state: () => ({ t: sim.tau, loop: reel.T.L, phase: phaseName(sim.tau), seed: P.seed, preset }),
    SHIPPED, SPEC, P, PLATFORM: 'desktop', reel, renderer, FORMATS,
    // The headless exporter: draw frame i of the loop at the export size; the caller reads the canvas.
    prepareExport(opts) {
      opts = opts || {};
      const f = FORMATS[opts.format != null ? opts.format : P.format];
      manualClock = true; exporting = true;
      renderer.resize(f.w, f.h, opts.supersample || P.supersample); drawOverlay(f.w, f.h);
      const fps = opts.fps || P.fps;
      return { w: f.w, h: f.h, fps, frames: Math.round(reel.T.L / P.playSpeed * fps), L: reel.T.L };
    },
    renderFrame(i, fps) { fps = fps || P.fps; const tau = (i / fps) * P.playSpeed; renderer.draw(reel.frame(tau, P.playSpeed / fps), look()); return tau; },
    readPixels() { const gl = renderer.gl, w = canvas.width, h = canvas.height, px = new Uint8Array(w * h * 4); gl.readPixels(0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, px); return px; },
    apply(settings) { Object.assign(P, settings); replan(); previewDirty = true; },
  };

  // ================= boot =================
  replan();
  buildTabs(); buildPane(); refreshDiff(); updateFormatChip();
  window.addEventListener('resize', () => { previewDirty = true; });
  if (document.fonts && document.fonts.ready) document.fonts.ready.then(() => { previewDirty = true; });
  requestAnimationFrame((t) => { last = t; loop(t); });
})();
