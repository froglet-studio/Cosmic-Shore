// Shiva's Cell - the page: the three.js view, input, HUD, settings, scorecard and decision log over sim.js.
(() => {
  'use strict';
  const $ = (id) => document.getElementById(id);
  const S = window.ShivaSim, V = S.V, bank = S.buildBank(window.PLAN_DATA || {});
  const LAB = { name: "Shiva's Cell", branch: 'claude/tandava-arena-mode', round: 1, doc: 'Docs/Studios/ShivaLab/README.md' };
  const SHIPPED = S.SHIPPED, SPEC = S.SPEC;

  // ── per-viewer conveniences (never the record) ──
  const LS = 'lab:shiva:';
  const lsGet = (k, d) => { try { const v = localStorage.getItem(LS + k); return v ? JSON.parse(v) : d; } catch (e) { return d; } };
  const lsSet = (k, v) => { try { localStorage.setItem(LS + k, JSON.stringify(v)); } catch (e) { /* private window */ } };
  const P = Object.assign({}, SHIPPED, lsGet('P', {}));
  for (const k of Object.keys(P)) if (!(k in SHIPPED)) delete P[k];
  const cfg = Object.assign({ hulls: ['Squirrel', 'Sparrow', 'Rhino'], seed: 7, speed: 1, sound: true }, lsGet('cfg', {}));
  cfg.hulls = cfg.hulls.map((h) => (S.HULLS[h] ? h : 'Squirrel'));

  // ── the model, on a fixed clock ──
  let world = null, human = -1, acc = 0, paused = false;
  function reset(seed) {
    if (seed != null) cfg.seed = seed >>> 0;
    world = new S.World(bank, P, { seed: cfg.seed, hulls: cfg.hulls, human });
    acc = 0; if (fx) fx.reset();
    $('endCard').hidden = true;
  }
  function simStep(dt) {
    acc += dt * cfg.speed;
    let n = 0;
    while (acc >= S.DT && n < 12) { readInput(); world.step(S.DT); acc -= S.DT; n++; if (fx) fx.events(world.events); world.events.length = 0; }
    if (n >= 12) acc = 0;
  }
  const score = () => world.score();

  // ── headless batch: the scorecard (no drawing). runBatch() with no options is the verifier's quick, deterministic call ──
  const SQUADS = { Mixed: ['Squirrel', 'Sparrow', 'Rhino'], Fast: ['Manta', 'Dolphin', 'Squirrel'], Slow: ['Sparrow', 'Serpent', 'Urchin'] };
  const VARIANTS = [
    { name: 'Your settings', p: {}, o: {} },
    { name: 'No swarm (control)', p: {}, o: { noSwarm: true } },
    { name: 'Sloppy squad', p: { aiNoise: 0.6 }, o: {} },
    { name: 'No clap rip (control)', p: { clapReach: 0 }, o: {} },
  ];
  function runOne(squad, variant, seed, maxSeconds) {
    const Pv = Object.assign({}, P, variant.p);
    return Object.assign({ squad, variant: variant.name, seed }, S.run(bank, Pv, Object.assign({ seed, hulls: SQUADS[squad] }, variant.o), maxSeconds));
  }
  function runBatch(opts) {
    opts = opts || {};
    const seeds = opts.seeds || [1], squads = opts.squads || ['Mixed'], vars = opts.variants ? VARIANTS.filter((v) => opts.variants.includes(v.name)) : VARIANTS.slice(0, 2), rows = [];
    for (const sq of squads) for (const v of vars) for (const s of seeds) rows.push(runOne(sq, v, s, opts.maxSeconds || 240));
    return rows;
  }

  // ── platform ──
  function detectPlatform() {
    const host = window.__labHost || {}, ua = navigator.userAgent || '';
    const mobileUA = (navigator.userAgentData && navigator.userAgentData.mobile) || /Android|iPhone|iPad|iPod|Mobile/i.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1);
    const coarseOnly = matchMedia('(pointer: coarse)').matches && !matchMedia('(any-pointer: fine)').matches;
    return { shell: host.shell || 'web', device: host.device || ((mobileUA || coarseOnly) ? 'phone' : 'pc') };
  }
  const PLATFORM = detectPlatform();
  $('platform').textContent = (PLATFORM.device === 'phone' ? 'Phone' : 'PC') + ' · ' + (PLATFORM.shell === 'web' ? 'Web' : PLATFORM.shell);

  // ═════════════════════════════════════════════ input (the same object for keys, pad and touch) ═════════════════════════════════════════════
  const keys = new Set();
  addEventListener('keydown', (e) => { if (e.target && /INPUT|TEXTAREA|SELECT/.test(e.target.tagName)) return; keys.add(e.code); if (['Space', 'ArrowUp', 'ArrowDown'].includes(e.code) && human >= 0) e.preventDefault(); });
  addEventListener('keyup', (e) => keys.delete(e.code));
  addEventListener('blur', () => keys.clear());
  const touch = { stick: null, x: 0, y: 0, boost: false };
  const stage = $('stage');
  stage.addEventListener('pointerdown', (e) => {
    if (human < 0 || e.pointerType !== 'touch') return;
    const r = stage.getBoundingClientRect(), lx = e.clientX - r.left;
    if (lx < r.width / 2) { touch.stick = { id: e.pointerId, x0: e.clientX, y0: e.clientY }; touch.x = 0; touch.y = 0; showStick(e.clientX - r.left, e.clientY - r.top, 0, 0); }
    else { touch.boost = e.pointerId; }
  });
  stage.addEventListener('pointermove', (e) => {
    if (touch.stick && e.pointerId === touch.stick.id) {
      const dx = e.clientX - touch.stick.x0, dy = e.clientY - touch.stick.y0, m = 48;
      touch.x = Math.max(-1, Math.min(1, dx / m)); touch.y = Math.max(-1, Math.min(1, dy / m));
      const r = stage.getBoundingClientRect(); showStick(touch.stick.x0 - r.left, touch.stick.y0 - r.top, touch.x * m, touch.y * m);
    }
  });
  const endTouch = (e) => { if (touch.stick && e.pointerId === touch.stick.id) { touch.stick = null; touch.x = touch.y = 0; $('touch').hidden = true; } if (touch.boost === e.pointerId) touch.boost = false; };
  stage.addEventListener('pointerup', endTouch); stage.addEventListener('pointercancel', endTouch);
  function showStick(x, y, kx, ky) { $('touch').hidden = false; Object.assign($('tStick').style, { left: x + 'px', top: y + 'px' }); Object.assign($('tKnob').style, { left: (x + kx) + 'px', top: (y + ky) + 'px' }); }
  let ease = { p: 0, y: 0 };
  function readInput() {
    if (human < 0) return;
    let p = 0, y = 0, boost = false;
    if (keys.has('KeyW') || keys.has('ArrowUp')) p -= 1;      // nose down
    if (keys.has('KeyS') || keys.has('ArrowDown')) p += 1;    // nose up
    if (keys.has('KeyA') || keys.has('ArrowLeft')) y -= 1;
    if (keys.has('KeyD') || keys.has('ArrowRight')) y += 1;
    boost = keys.has('Space') || keys.has('ShiftLeft') || keys.has('ShiftRight');
    ease.p += (p - ease.p) * 0.25; ease.y += (y - ease.y) * 0.25;
    let ip = ease.p, iy = ease.y;
    try {
      const pads = navigator.getGamepads ? navigator.getGamepads() : [];
      for (const g of pads) {
        if (!g) continue;
        const ax = g.axes[0] || 0, ay = g.axes[1] || 0;
        if (Math.abs(ax) > 0.12) iy = ax;
        if (Math.abs(ay) > 0.12) ip = -ay;               // the pad's Y is inverted against up = +y (L-STU-8)
        if ((g.buttons[7] && g.buttons[7].value > 0.3) || (g.buttons[0] && g.buttons[0].pressed)) boost = true;
        break;
      }
    } catch (e) { /* the viewer may block the Gamepad API */ }
    if (touch.stick) { iy = touch.x; ip = -touch.y; }
    if (touch.boost !== false) boost = true;
    world.input[0] = { pitch: ip, yaw: iy, boost, throttle: 1 };
  }

  // ═════════════════════════════════════════════ the view ═════════════════════════════════════════════
  const EC = [[0.98, 0.75, 0.18], [0.93, 0.27, 0.38], [0.24, 0.6, 0.95], [0.62, 0.42, 0.94]];   // Charge Mass Space Time
  const ECSS = ['var(--charge)', 'var(--mass)', 'var(--space)', 'var(--time)'];
  const DANGER = [1.9, 0.25, 1.35], GOLD = [1.6, 1.12, 0.36], JADE = [0.12, 1.0, 0.86], SHIVA = [0.07, 0.15, 0.52];
  let renderer = null, fx = null;
  try { if (window.THREE) renderer = new THREE.WebGLRenderer({ canvas: stage, antialias: true, powerPreference: 'high-performance' }); } catch (e) { renderer = null; }
  if (!renderer) { $('glfail').hidden = false; $('brief').hidden = true; }

  if (renderer) fx = buildView();
  function buildView() {
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x03040e);
    const camera = new THREE.PerspectiveCamera(55, 1, 2, 16000);
    camera.position.set(0, 600, 2600);
    let controls = null;
    if (THREE.OrbitControls) { controls = new THREE.OrbitControls(camera, stage); controls.enableDamping = true; controls.dampingFactor = 0.08; controls.target.set(0, 60, 0); controls.enabled = false; }
    let composer = null, bloom = null;
    try {
      if (THREE.EffectComposer && THREE.UnrealBloomPass) {
        const rt = new THREE.WebGLRenderTarget(2, 2, { type: THREE.HalfFloatType, format: THREE.RGBAFormat });
        composer = new THREE.EffectComposer(renderer, rt);
        composer.addPass(new THREE.RenderPass(scene, camera));
        bloom = new THREE.UnrealBloomPass(new THREE.Vector2(2, 2), 0.85, 0.5, 0.72);
        composer.addPass(bloom);
      }
    } catch (e) { composer = null; }
    const wrap = $('stageWrap');
    function resize() {
      const w = Math.max(1, wrap.clientWidth), h = Math.max(1, wrap.clientHeight);
      renderer.setSize(w, h, false); if (composer) composer.setSize(w, h);
      camera.aspect = w / h; camera.updateProjectionMatrix();
    }
    new ResizeObserver(resize).observe(wrap); resize();

    // the prism read (TandavaFormLab): lit face, a fresnel rim and a glowing edge
    const PRISM_VS = `varying vec3 vN; varying vec3 vV; varying vec3 vC; varying vec3 vL;
      void main() { mat4 im = instanceMatrix; vec4 wp = modelMatrix * im * vec4(position, 1.0);
        vN = normalize(mat3(modelMatrix * im) * normal); vV = cameraPosition - wp.xyz;
        #ifdef USE_INSTANCING_COLOR
          vC = instanceColor;
        #else
          vC = vec3(1.0);
        #endif
        vL = position; gl_Position = projectionMatrix * viewMatrix * wp; }`;
    const PRISM_FS = `uniform vec3 uLight; varying vec3 vN; varying vec3 vV; varying vec3 vC; varying vec3 vL;
      void main() { vec3 n = normalize(vN); vec3 v = normalize(vV); float fres = pow(1.0 - abs(dot(n, v)), 2.0);
        vec3 a = abs(vL) * 2.0; float e = max(a.x * a.y, max(a.y * a.z, a.x * a.z)); float edge = smoothstep(0.80, 0.97, e);
        float lam = 0.5 + 0.5 * max(dot(n, uLight), 0.0);
        gl_FragColor = vec4(vC * (0.22 + 0.5 * lam) + vC * fres * 1.2 + vC * edge * 0.8, 1.0); }`;
    const prismMat = new THREE.ShaderMaterial({ uniforms: { uLight: { value: new THREE.Vector3(-0.35, 0.8, 0.45).normalize() } }, vertexShader: PRISM_VS, fragmentShader: PRISM_FS });
    const BOX = new THREE.BoxGeometry(1, 1, 1);
    function pool(n, geo, mat) {
      const m = new THREE.InstancedMesh(geo || BOX, mat || prismMat, n);
      m.instanceMatrix.setUsage(THREE.DynamicDrawUsage); m.frustumCulled = false;
      m.setColorAt(0, new THREE.Color(1, 1, 1)); m.instanceColor.setUsage(THREE.DynamicDrawUsage);   // r128 sizes the colour buffer from count: set it first
      m.count = 0;
      scene.add(m); return m;
    }
    const M4 = new THREE.Matrix4(), Q = new THREE.Quaternion(), PV = new THREE.Vector3(), SV = new THREE.Vector3(), C = new THREE.Color(), ZA = new THREE.Vector3(0, 0, 1), TV = new THREE.Vector3();
    function put(mesh, i, p, dir, sx, sy, sz, col) {
      PV.set(p[0], p[1], p[2]);
      if (dir) { TV.set(dir[0], dir[1], dir[2]); if (TV.lengthSq() < 1e-8) TV.set(0, 0, 1); TV.normalize(); Q.setFromUnitVectors(ZA, TV); } else Q.identity();
      SV.set(sx, sy, sz); M4.compose(PV, Q, SV); mesh.setMatrixAt(i, M4);
      C.setRGB(col[0], col[1], col[2]); mesh.setColorAt(i, C);
    }
    function done(mesh, n) { mesh.count = n; mesh.instanceMatrix.needsUpdate = true; if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true; }

    // ── the sky: a nebula round the cell and stars ──
    const skyMat = new THREE.ShaderMaterial({ side: THREE.BackSide, depthWrite: false, uniforms: { uT: { value: 0 }, uTint: { value: new THREE.Vector3(1, 1, 1) } },
      vertexShader: 'varying vec3 vD; void main(){ vD = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
      fragmentShader: `varying vec3 vD; uniform float uT; uniform vec3 uTint;
        float h(vec3 p){ return fract(sin(dot(p, vec3(12.9898,78.233,37.719))) * 43758.5453); }
        float n3(vec3 p){ vec3 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
          return mix(mix(mix(h(i),h(i+vec3(1,0,0)),f.x),mix(h(i+vec3(0,1,0)),h(i+vec3(1,1,0)),f.x),f.y),
                     mix(mix(h(i+vec3(0,0,1)),h(i+vec3(1,0,1)),f.x),mix(h(i+vec3(0,1,1)),h(i+vec3(1,1,1)),f.x),f.y),f.z); }
        float fbm(vec3 p){ float a=0.5, s=0.0; for(int i=0;i<5;i++){ s+=a*n3(p); p*=2.03; a*=0.5; } return s; }
        void main(){ vec3 d = vD; float m = fbm(d*2.2 + vec3(0.0, uT*0.004, 0.0)); float m2 = fbm(d*4.0 - 3.1);
          vec3 c = vec3(0.012,0.014,0.05);
          c += vec3(0.32,0.12,0.42) * smoothstep(0.45,0.85,m) * 0.55;          // violet
          c += vec3(0.08,0.2,0.55) * smoothstep(0.5,0.9,m2) * 0.5;             // blue
          c += vec3(0.85,0.42,0.12) * smoothstep(0.62,0.95,m*m2*1.6) * 0.45;   // the ember orange of the reference
          float s = pow(h(floor(d*900.0)), 60.0) * 2.0; c += vec3(s);
          gl_FragColor = vec4(c * uTint, 1.0); }` });
    const sky = new THREE.Mesh(new THREE.SphereGeometry(9000, 48, 24), skyMat); scene.add(sky);
    // the cell's membrane: a faint rim
    const memMat = new THREE.ShaderMaterial({ transparent: true, depthWrite: false, side: THREE.BackSide, blending: THREE.AdditiveBlending, uniforms: { uCol: { value: new THREE.Vector3(0.25, 0.32, 0.9) } },
      vertexShader: 'varying vec3 vN; varying vec3 vV; void main(){ vec4 wp = modelMatrix*vec4(position,1.0); vN = normalize(mat3(modelMatrix)*normal); vV = normalize(cameraPosition-wp.xyz); gl_Position = projectionMatrix*viewMatrix*wp; }',
      fragmentShader: 'varying vec3 vN; varying vec3 vV; uniform vec3 uCol; void main(){ float f = pow(1.0-abs(dot(vN,vV)),3.0); gl_FragColor = vec4(uCol*f*0.55, 1.0); }' });
    const membrane = new THREE.Mesh(new THREE.SphereGeometry(1, 64, 32), memMat); scene.add(membrane);

    // ── Shiva: a prism figure sampled from bones; the same samples every form, so a form change is a morph ──
    const SP = 14;
    const BONES = [
      ['waist', 'chest', 140, 168], ['chest', 'neck', 168, 62], ['neck', 'head', 46, 40], ['chest', 'shoulderL', 70, 52], ['chest', 'shoulderR', 70, 52],
      ['shoulderL', 'elbowUL', 44, 34], ['elbowUL', 'handUL', 34, 26], ['shoulderR', 'elbowUR', 44, 34], ['elbowUR', 'handUR', 34, 26],
      ['shoulderL', 'elbowLL', 44, 34], ['elbowLL', 'handLL', 34, 26], ['shoulderR', 'elbowLR', 44, 34], ['elbowLR', 'handLR', 34, 26],
    ];
    const BALLS = [['head', 84, 1.12, 'head'], ['bun', 48, 1.0, 'hair'], ['handUL', 30, 1, 'hand'], ['handUR', 30, 1, 'hand'], ['handLL', 30, 1, 'hand'], ['handLR', 30, 1, 'hand']];
    const fig0 = S.figure(0, 0, 0, 0), samples = [];
    for (let b = 0; b < BONES.length; b++) {
      const [a, z, r0, r1] = BONES[b], len = V.len(V.sub(fig0[z], fig0[a])), nu = Math.max(2, Math.round(len / SP)), nt = Math.max(6, Math.round(2 * Math.PI * Math.max(r0, r1) / SP));
      for (let i = 0; i < nu; i++) for (let j = 0; j < nt; j++) samples.push({ k: 'bone', b, u: (i + 0.5) / nu, th: (j + (i % 2) * 0.5) / nt * 6.2832 });
    }
    for (let b = 0; b < BALLS.length; b++) {
      const r = BALLS[b][1], n = Math.round(4 * Math.PI * r * r * BALLS[b][2] / (SP * SP));
      for (let i = 0; i < n; i++) { const y = 1 - 2 * (i + 0.5) / n, rr = Math.sqrt(1 - y * y), th = i * 2.39996; samples.push({ k: 'ball', b, d: [rr * Math.cos(th), y, rr * Math.sin(th)] }); }
    }
    // the serpent round his neck (Vasuki), the crescent in his hair, the eyes and the third eye
    const NECK_N = 46; for (let i = 0; i < NECK_N; i++) samples.push({ k: 'vasuki', a: i / NECK_N * 6.2832 });
    const MOON_N = 16; for (let i = 0; i < MOON_N; i++) samples.push({ k: 'moon', a: -1.1 + 2.2 * i / (MOON_N - 1) });
    for (const side of [-1, 1]) samples.push({ k: 'eye', side });
    samples.push({ k: 'third' });
    const LOCKS = 10, LOCK_N = 9; for (let l = 0; l < LOCKS; l++) for (let i = 0; i < LOCK_N; i++) samples.push({ k: 'lock', l, i });
    const figMesh = pool(samples.length + 4);
    const figPos = samples.map(() => [0, 0, 0]), figNrm = samples.map(() => [0, 0, 1]), figVel = samples.map(() => [0, 0, 0]);
    function basisOf(dir) { const ref = Math.abs(dir[1]) > 0.9 ? [1, 0, 0] : [0, 1, 0], e1 = V.norm(V.cross(dir, ref)); return [e1, V.cross(dir, e1)]; }
    function layFigure(j, t, phase) {
      const bonesCache = BONES.map(([a, z, r0, r1]) => { const A = j[a], B = j[z], d = V.sub(B, A), L = V.len(d), dir = V.norm(d), [e1, e2] = basisOf(dir); return { A, dir, L, e1, e2, r0, r1 }; });
      const open = j.mouthOpen || 0;
      for (let i = 0; i < samples.length; i++) {
        const s = samples[i]; let p, n;
        if (s.k === 'bone') {
          const bc = bonesCache[s.b], r = bc.r0 + (bc.r1 - bc.r0) * s.u, c = Math.cos(s.th), sn = Math.sin(s.th);
          n = V.add(V.mul(bc.e1, c), V.mul(bc.e2, sn)); p = V.add(V.add(bc.A, V.mul(bc.dir, bc.L * s.u)), V.mul(n, r));
        } else if (s.k === 'ball') {
          const [name, r, sy] = BALLS[s.b], c = j[name]; n = s.d.slice();
          let d = [s.d[0] * r, s.d[1] * r * sy, s.d[2] * r];
          if (name === 'head' && open > 0.01 && s.d[2] > 0.25 && s.d[1] < -0.05) d = V.add(d, [0, -open * 46 * (0.5 + s.d[2]), 0]);   // the jaw drops
          p = V.add(c, d);
        } else if (s.k === 'vasuki') {
          const c = j.neck, rr = 74 + 6 * Math.sin(s.a * 3 + t * 2);
          p = [c[0] + rr * Math.cos(s.a), c[1] - 8 + 10 * Math.sin(s.a * 2 + t), c[2] + rr * Math.sin(s.a)]; n = V.norm(V.sub(p, c));
        } else if (s.k === 'moon') {
          const c = V.add(j.bun, [42, 28, 10]); p = [c[0] + 34 * Math.cos(s.a + 1.57) * 0.7, c[1] + 34 * Math.sin(s.a + 1.57), c[2] + 6]; n = [0, 0, 1];
        } else if (s.k === 'eye') { p = V.add(j.head, [s.side * 30, 4, 86]); n = [0, 0, 1]; }
        else if (s.k === 'third') { p = j.thirdEye.slice(); n = [0, 0, 1]; }
        else {   // the matted locks: they fall to his shoulders when still and fly out in the dance
          const ang = (s.l / LOCKS) * 6.2832, fly = phase === 1 ? 1 : phase === 2 ? 0.4 : 0.12, k = (s.i + 1) / LOCK_N;
          const out = [Math.cos(ang), 0, Math.sin(ang) * 0.6 - 0.4], wave = Math.sin(t * 2.2 + s.l + s.i * 0.5) * 18 * fly;
          p = V.add(j.head, [out[0] * (60 + k * (40 + 260 * fly)), 30 - k * (150 - 120 * fly) + wave, out[2] * (60 + k * (30 + 140 * fly)) - 20]); n = V.norm(out);
        }
        figPos[i] = p; figNrm[i] = n;
      }
    }
    function figColour(s, i, t, flash) {
      const tw = 0.85 + 0.15 * Math.sin(i * 1.7 + t * 1.4);
      if (s.k === 'eye') return [2.2, 2.2, 2.6];
      if (s.k === 'third') return [2.6, 1.6, 0.4];
      if (s.k === 'vasuki') return [0.15 * tw, 0.9 * tw, 0.6 * tw];
      if (s.k === 'moon') return [2.0, 1.85, 1.3];
      if (s.k === 'lock') return [0.14, 0.12, 0.3];
      if (s.k === 'ball' && BALLS[s.b][3] === 'hair') return [0.12, 0.1, 0.32];
      return [SHIVA[0] * tw + flash, SHIVA[1] * tw + flash * 0.8, SHIVA[2] * tw + flash * 0.4];
    }
    // the aura and the cosmic swirl behind his head (the reference's halo of light)
    function radialTex(stops) {
      const c = document.createElement('canvas'); c.width = c.height = 128; const g = c.getContext('2d'), gr = g.createRadialGradient(64, 64, 0, 64, 64, 64);
      for (const [o, col] of stops) gr.addColorStop(o, col); g.fillStyle = gr; g.fillRect(0, 0, 128, 128); return new THREE.CanvasTexture(c);
    }
    const auraTex = radialTex([[0, 'rgba(120,160,255,0.55)'], [0.4, 'rgba(60,90,255,0.18)'], [1, 'rgba(0,0,0,0)']]);
    const aura = new THREE.Sprite(new THREE.SpriteMaterial({ map: auraTex, blending: THREE.AdditiveBlending, depthWrite: false, transparent: true }));
    aura.scale.set(1900, 2100, 1); scene.add(aura);
    const haloMat = new THREE.ShaderMaterial({ transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, uniforms: { uT: { value: 0 }, uK: { value: 1 } },
      vertexShader: 'varying vec2 vU; void main(){ vU = uv; gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0); }',
      fragmentShader: `varying vec2 vU; uniform float uT; uniform float uK; void main(){ vec2 p = vU*2.0-1.0; float r = length(p), a = atan(p.y,p.x);
        float ring = smoothstep(0.55,0.75,r)*smoothstep(1.0,0.78,r); float sw = 0.5+0.5*sin(a*7.0 - uT*0.8 + r*9.0);
        vec3 c = mix(vec3(1.0,0.75,0.35), vec3(0.95,0.95,1.0), sw) * ring * (0.35+0.45*sw) * uK; gl_FragColor = vec4(c, 1.0); }` });
    const halo = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), haloMat); halo.scale.set(980, 980, 1); scene.add(halo);
    // the nebula he rises out of, below the waist
    const NEB = 2600, nebGeo = new THREE.BufferGeometry(), nebPos = new Float32Array(NEB * 3), nebCol = new Float32Array(NEB * 3), nebSeed = [];
    const vr = S.mulberry32(99);
    for (let i = 0; i < NEB; i++) { const r = Math.pow(vr(), 0.6) * 560, a = vr() * 6.2832, y = -300 - vr() * 260 + r * 0.1; nebSeed.push([r, a, y, 0.04 + vr() * 0.12]); const pick = vr(); const col = pick < 0.45 ? [0.5, 0.25, 0.85] : pick < 0.75 ? [0.2, 0.4, 1.0] : [1.0, 0.55, 0.2]; nebCol.set(col, i * 3); }
    nebGeo.setAttribute('position', new THREE.BufferAttribute(nebPos, 3)); nebGeo.setAttribute('color', new THREE.BufferAttribute(nebCol, 3));
    const dotTex = radialTex([[0, 'rgba(255,255,255,1)'], [0.3, 'rgba(255,255,255,0.5)'], [1, 'rgba(0,0,0,0)']]);
    const neb = new THREE.Points(nebGeo, new THREE.PointsMaterial({ size: 26, map: dotTex, vertexColors: true, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, opacity: 0.55 }));
    scene.add(neb);

    // ── the shield: three shells of danger prisms ──
    const shells = [0, 1, 2].map(() => ({ mesh: pool(4200), pts: [], broke: -1 }));
    function layShells() {
      for (let k = 0; k < 3; k++) {
        const R = S.shellR(P, k), n = Math.round(4 * Math.PI * R * R / (64 * 64)), pts = [];
        for (let i = 0; i < Math.min(n, 4200); i++) { const y = 1 - 2 * (i + 0.5) / n, rr = Math.sqrt(1 - y * y), th = i * 2.39996; pts.push([rr * Math.cos(th), y, rr * Math.sin(th)]); }
        shells[k].pts = pts; shells[k].R = R; shells[k].broke = -1;
      }
    }

    // ── the circuit ──
    const ringGeo = new THREE.TorusGeometry(1, 0.06, 8, 56);
    const ringMeshes = [];
    for (let i = 0; i < 24; i++) { const m = new THREE.Mesh(ringGeo, new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false })); m.visible = false; scene.add(m); ringMeshes.push(m); }
    const pathGeo = new THREE.BufferGeometry(); pathGeo.setAttribute('position', new THREE.BufferAttribute(new Float32Array(25 * 3), 3));
    const pathLine = new THREE.Line(pathGeo, new THREE.LineDashedMaterial({ color: 0xf2c14e, dashSize: 24, gapSize: 30, transparent: true, opacity: 0.35 })); scene.add(pathLine);

    // ── the orrery ──
    const PCOL = [[0.85, 0.5, 0.25], [0.35, 0.65, 0.8], [0.7, 0.3, 0.25], [0.65, 0.55, 0.85], [0.3, 0.55, 0.45], [0.85, 0.75, 0.55], [0.45, 0.5, 0.95]];
    const planetMat = (c) => new THREE.ShaderMaterial({ uniforms: { uC: { value: new THREE.Vector3(...c) } },
      vertexShader: 'varying vec3 vN; varying vec3 vP; void main(){ vN = normalize(mat3(modelMatrix)*normal); vP = position; gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0); }',
      fragmentShader: 'varying vec3 vN; varying vec3 vP; uniform vec3 uC; void main(){ float l = 0.25+0.75*max(dot(vN, normalize(vec3(0.2,0.5,1.0))),0.0); float b = 0.85+0.15*sin(vP.y*0.35); gl_FragColor = vec4(uC*l*b, 1.0); }' });
    const planets = []; const orbitLines = [];
    function layOrrery() {
      for (const p of planets) scene.remove(p); for (const o of orbitLines) scene.remove(o); planets.length = 0; orbitLines.length = 0;
      world.planets.forEach((pl, i) => {
        const m = new THREE.Mesh(new THREE.SphereGeometry(pl.size, 28, 16), planetMat(PCOL[i % PCOL.length]));
        if (pl.ringed) { const r = new THREE.Mesh(new THREE.RingGeometry(pl.size * 1.4, pl.size * 2.2, 48), new THREE.MeshBasicMaterial({ color: 0xc8b0ff, transparent: true, opacity: 0.4, side: THREE.DoubleSide })); r.rotation.x = 1.2; m.add(r); }
        scene.add(m); planets.push(m);
        const pts = []; for (let k = 0; k <= 96; k++) { const q = Object.assign({}, pl, { ph: 0, w: 1 }); const at = S.planetPos(q, k / 96 * 6.2832, 1); pts.push(new THREE.Vector3(...at)); }
        const ln = new THREE.LineLoop(new THREE.BufferGeometry().setFromPoints(pts), new THREE.LineBasicMaterial({ color: 0x8a7bd8, transparent: true, opacity: 0.18 })); scene.add(ln); orbitLines.push(ln);
      });
    }

    // ── the living things and the cell's contents ──
    const DART = new THREE.ConeGeometry(4.5, 18, 6); DART.rotateX(Math.PI / 2);
    const darts = pool(3, DART), trails = pool(1500), serps = pool(1400), sharks = pool(16, new THREE.ConeGeometry(5, 26, 5).rotateX(Math.PI / 2)), fishes = pool(60), claws = pool(16);
    const flora = pool(4200), floraOld = pool(4200), crystals = pool(600, new THREE.OctahedronGeometry(1, 0));
    // the black hole in his mouth
    const bh = new THREE.Group(); scene.add(bh);
    const bhCore = new THREE.Mesh(new THREE.SphereGeometry(1, 32, 16), new THREE.MeshBasicMaterial({ color: 0x000000 })); bh.add(bhCore);
    const diskMat = new THREE.ShaderMaterial({ transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, uniforms: { uT: { value: 0 } },
      vertexShader: 'varying vec2 vU; void main(){ vU = uv; gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0); }',
      fragmentShader: `varying vec2 vU; uniform float uT; void main(){ vec2 p = vU*2.0-1.0; float r = length(p), a = atan(p.y,p.x);
        float band = smoothstep(0.34,0.42,r)*smoothstep(1.0,0.5,r); float s = 0.6+0.4*sin(a*5.0 + uT*3.0 - r*14.0);
        vec3 c = mix(vec3(1.0,0.95,0.85), vec3(1.0,0.5,0.15), smoothstep(0.42,0.9,r)) * band * s * 1.6; gl_FragColor = vec4(c,1.0); }` });
    const disk = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), diskMat); bh.add(disk);
    // the clap's wave
    const waveMat = new THREE.ShaderMaterial({ transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, uniforms: { uK: { value: 1 } },
      vertexShader: 'varying vec3 vN; varying vec3 vV; void main(){ vec4 wp = modelMatrix*vec4(position,1.0); vN = normalize(mat3(modelMatrix)*normal); vV = normalize(cameraPosition-wp.xyz); gl_Position = projectionMatrix*viewMatrix*wp; }',
      fragmentShader: 'varying vec3 vN; varying vec3 vV; uniform float uK; void main(){ float f = pow(1.0-abs(dot(vN,vV)),2.5); gl_FragColor = vec4(vec3(1.0,0.85,0.55)*f*1.8*uK + vec3(0.4,0.55,1.0)*0.05*uK, 1.0); }' });
    const wave = new THREE.Mesh(new THREE.SphereGeometry(1, 48, 24), waveMat); wave.visible = false; scene.add(wave);
    // the majestic end: a burst of light from the figure, and shock rings
    const BURST = 3600, burstGeo = new THREE.BufferGeometry(), burstPos = new Float32Array(BURST * 3), burstCol = new Float32Array(BURST * 3), burstVel = [];
    for (let i = 0; i < BURST; i++) { const d = V.norm([vr() - 0.5, vr() - 0.5, vr() - 0.5]); burstVel.push(V.mul(d, 120 + Math.pow(vr(), 0.5) * 1400)); const k = vr(); burstCol.set(k < 0.5 ? [1.0, 0.8, 0.35] : k < 0.8 ? [0.45, 0.6, 1.0] : [1.0, 0.35, 0.8], i * 3); }
    burstGeo.setAttribute('position', new THREE.BufferAttribute(burstPos, 3)); burstGeo.setAttribute('color', new THREE.BufferAttribute(burstCol, 3));
    const burst = new THREE.Points(burstGeo, new THREE.PointsMaterial({ size: 20, map: dotTex, vertexColors: true, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending })); burst.visible = false; scene.add(burst);
    const shocks = [0, 1, 2].map((k) => { const m = new THREE.Mesh(new THREE.TorusGeometry(1, 0.012, 6, 96), new THREE.MeshBasicMaterial({ color: [0xf2c14e, 0x7aa2ff, 0xff4fbf][k], transparent: true, blending: THREE.AdditiveBlending, depthWrite: false })); m.rotation.set(k * 1.0 + 0.3, k * 0.7, 0); m.visible = false; scene.add(m); return m; });
    const shardsK = [];   // shell shards flying when a shell breaks

    // ── sound (only after a gesture) ──
    let ac = null;
    function audio() { if (!cfg.sound) return null; try { ac = ac || new (window.AudioContext || window.webkitAudioContext)(); if (ac.state === 'suspended') ac.resume(); } catch (e) { ac = null; } return ac; }
    function tone(f0, f1, dur, type, vol) { const a = audio(); if (!a) return; const o = a.createOscillator(), g = a.createGain(); o.type = type || 'sine'; o.frequency.setValueAtTime(f0, a.currentTime); o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), a.currentTime + dur); g.gain.setValueAtTime(vol || 0.12, a.currentTime); g.gain.exponentialRampToValueAtTime(0.0001, a.currentTime + dur); o.connect(g).connect(a.destination); o.start(); o.stop(a.currentTime + dur); }
    function noise(dur, vol, lp) { const a = audio(); if (!a) return; const n = Math.round(a.sampleRate * dur), b = a.createBuffer(1, n, a.sampleRate), d = b.getChannelData(0); for (let i = 0; i < n; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / n, 2); const s = a.createBufferSource(), f = a.createBiquadFilter(), g = a.createGain(); f.type = 'lowpass'; f.frequency.value = lp || 1800; g.gain.value = vol || 0.3; s.buffer = b; s.connect(f).connect(g).connect(a.destination); s.start(); }

    function toast(text, small) { const box = $('toasts'), t = document.createElement('div'); t.textContent = text; if (small) t.className = 'small'; box.appendChild(t); setTimeout(() => t.remove(), 3300); while (box.children.length > 4) box.firstChild.remove(); }

    const view = { cam: lsGet('cam', 'cinematic'), shake: 0, flash: 0, endT: -1, lostT: -1, wonT: -1, follow: 0 };
    const SUB = ['The rings spiral in. Thread them all before the drum stops.', 'Nataraja dances. The rings will not keep still.', 'His mouth opens on a black hole. Do not get drawn in.'];
    function events(evs) {
      for (const e of evs) {
        const me = e.pilot === human && human >= 0;
        if (e.k === 'thread') { tone(660 + 60 * e.ring, 990 + 60 * e.ring, 0.25, 'triangle', 0.08); if (e.ring === e.of - 1) toast('The last ring.', true); }
        else if (e.k === 'shatter') { const k = e.phase; shells[k].broke = world.t; shardsK.push(k); noise(1.2, 0.35, 3000); toast(['The outer shell breaks.', 'The middle shell breaks.', 'The last shell breaks.'][k]); view.shake = 0.6; }
        else if (e.k === 'clap') { toast('Shiva claps.'); }
        else if (e.k === 'wave') { tone(90, 30, 1.6, 'sine', 0.4); noise(1.4, 0.45, 600); view.shake = 1.2; view.flash = 1; }
        else if (e.k === 'regrow') { toast(S.PHASES[e.phase].name); setTimeout(() => toast(SUB[e.phase], true), 900); }
        else if (e.k === 'strip' && me) toast('The wave rips ' + e.n + ' element levels off you.', true);
        else if (e.k === 'swallowed' && me) toast('The mouth takes you and spits you out, stripped.', true);
        else if (e.k === 'latch' && me) toast('A clawfish has you. Boost for a second to shake it.', true);
        else if (e.k === 'lunge' && me) toast('A serpent rears at you.', true);
        else if (e.k === 'sting' && me) { tone(220, 110, 0.18, 'sawtooth', 0.06); view.shake = Math.max(view.shake, 0.25); }
        else if (e.k === 'crystal' && me) tone(1320, 1760, 0.08, 'sine', 0.04);
        else if (e.k === 'won') { view.wonT = world.t; startBurst(); tone(55, 40, 4, 'sine', 0.5); noise(3, 0.5, 900); setTimeout(() => { $('endTitle').textContent = 'The Tandava is broken'; $('endLine').textContent = 'His third shell falls and the god comes apart in light. The cell is yours.'; $('endCard').hidden = false; }, 2600); }
        else if (e.k === 'lost') { view.lostT = world.t; tone(110, 40, 3, 'sine', 0.35); $('endTitle').textContent = 'The drum stops'; $('endLine').textContent = 'The clock ran out in ' + S.PHASES[e.phase].name + '. His dance ends the cell.'; $('endCard').hidden = false; }
      }
    }
    function startBurst() {
      const c = world.fig.chest; for (let i = 0; i < BURST; i++) burstPos.set([c[0], c[1] + 60, c[2]], i * 3);
      burst.visible = true; burstGeo.attributes.position.needsUpdate = true;
      for (let i = 0; i < samples.length; i++) { const d = V.norm(V.sub(figPos[i], c)); figVel[i] = V.mul(d, 140 + 520 * ((i * 7919) % 101) / 101); }
      for (const s of shocks) s.visible = true;
      view.shake = 2; view.flash = 1.5;
    }

    function reset() {
      layShells(); layOrrery(); shardsK.length = 0; burst.visible = false; for (const s of shocks) s.visible = false;
      view.wonT = view.lostT = -1; view.flash = 0;
      membrane.scale.setScalar(P.cellRadius);
    }

    // ── a frame ──
    const camPos = new THREE.Vector3(0, 600, 2600), camLook = new THREE.Vector3(0, 60, 0);
    function render(dt) {
      const w = world, t = w.t, j = w.fig;
      skyMat.uniforms.uT.value = t; haloMat.uniforms.uT.value = t; diskMat.uniforms.uT.value = t;
      // the figure: laid out in his own frame, then turned to where he faces
      layFigure(S.figure(w.phase, w.clapK, w.danceT, w.mouthOpen, 0), t, w.phase);
      { const c = Math.cos(w.yaw || 0), sn = Math.sin(w.yaw || 0), r = (v) => [v[0] * c + v[2] * sn, v[1], -v[0] * sn + v[2] * c]; for (let i = 0; i < figPos.length; i++) { figPos[i] = r(figPos[i]); figNrm[i] = r(figNrm[i]); } }
      const won = view.wonT >= 0, wt = won ? t - view.wonT : 0;
      const flash = Math.max(0, view.flash) * 0.8;
      let n = 0;
      for (let i = 0; i < samples.length; i++) {
        let p = figPos[i], sc = SP * 0.98, col = figColour(samples[i], i, t, flash);
        if (won) { p = V.add(p, V.mul(figVel[i], wt)); const k = Math.max(0, 1 - wt / 5); sc *= k; col = [col[0] * 1.3 + 0.9 * (1 - k), col[1] * 1.2 + 0.65 * (1 - k), col[2] * 1.1 + 0.35 * (1 - k)]; if (k <= 0) continue; }
        const s = samples[i], big = s.k === 'eye' ? 1.6 : s.k === 'third' ? 1.9 : s.k === 'moon' || s.k === 'vasuki' ? 0.8 : s.k === 'lock' ? 0.9 : 1;
        put(figMesh, n++, p, figNrm[i], sc * big, sc * big, sc * 0.28 * big + 2, col);
      }
      done(figMesh, n);
      halo.position.set(j.head[0] - Math.sin(j.yaw) * 190, j.head[1] + 10, j.head[2] - Math.cos(j.yaw) * 190); halo.lookAt(camera.position.x, j.head[1] + 20, camera.position.z);
      haloMat.uniforms.uK.value = (won ? Math.max(0, 1 - wt / 3) : w.phase === 1 ? 1.2 : 0.8) * 0.55;
      aura.material.opacity = won ? Math.max(0, 1 - wt / 3) : 1; aura.position.set(-Math.sin(j.yaw) * 160, 40, -Math.cos(j.yaw) * 160);
      // the nebula swirls
      for (let i = 0; i < NEB; i++) { const [r, a, y, sp] = nebSeed[i], aa = a + t * sp * (w.phase === 1 ? 2 : 1); nebPos[i * 3] = r * Math.cos(aa); nebPos[i * 3 + 1] = y + Math.sin(t * 0.3 + i) * 6; nebPos[i * 3 + 2] = r * Math.sin(aa) * 0.8; }
      nebGeo.attributes.position.needsUpdate = true;
      // the shells
      for (let k = 0; k < 3; k++) {
        const sh = shells[k], up = w.shellUp[k], pulse = 0.75 + 0.25 * Math.sin(t * 2.2 + k);
        let m = 0;
        if (up || (sh.broke >= 0 && t - sh.broke < 2.4)) {
          const bt = up ? 0 : t - sh.broke, grow = 1 + bt * bt * 0.9, fade = up ? 1 : Math.max(0, 1 - bt / 2.4), lit = w.shellUp.findIndex((u) => u) === k;
          const kk = (lit ? 0.62 : 0.22) * (up ? 1 : 2.4), col = [DANGER[0] * pulse * kk, DANGER[1] * pulse * kk, DANGER[2] * pulse * kk];
          for (let i = 0; i < sh.pts.length; i++) { const d = sh.pts[i]; const p = V.mul(d, sh.R * grow + (up ? 0 : ((i * 37) % 11) * bt * 30)); put(sh.mesh, m++, p, d, 10 * fade, 10 * fade, 1.6, col); }
        }
        done(sh.mesh, m);
      }
      // the rings
      for (let i = 0; i < ringMeshes.length; i++) {
        const g = w.rings[i], m = ringMeshes[i];
        if (!g || g.done || w.state === 'wave' || w.state === 'clap') { m.visible = false; continue; }
        m.visible = true; m.position.set(g.c[0], g.c[1], g.c[2]); TV.set(g.n[0], g.n[1], g.n[2]); Q.setFromUnitVectors(ZA, TV); m.quaternion.copy(Q);
        const lit = i === w.ringIx, next = i === w.ringIx + 1, last = i === w.rings.length - 1;
        const k = lit ? 1 + 0.08 * Math.sin(t * 6) : 1; m.scale.setScalar(P.mouth * k);
        m.material.color.setRGB(...(lit ? (last ? [2.4, 0.6, 1.8] : [2.4, 1.7, 0.5]) : next ? [0.9, 0.65, 0.25] : [0.18, 0.22, 0.5]));
        m.material.opacity = lit ? 1 : next ? 0.85 : 0.5;
      }
      const pa = pathGeo.attributes.position.array; let pc = 0;
      for (let i = w.ringIx; i < w.rings.length && pc < 25; i++, pc++) pa.set(w.rings[i].c, pc * 3);
      pathGeo.setDrawRange(0, pc); pathGeo.attributes.position.needsUpdate = true; pathLine.computeLineDistances(); pathLine.visible = w.state === 'play' && pc > 1;
      // planets
      w.planets.forEach((pl, i) => { const m = planets[i]; if (m) { m.position.set(...pl.c); m.rotation.y = t * 0.2; } });
      // pilots and their trails
      n = 0;
      for (const pl of w.pilots) { const you = pl.i === human, col = you ? GOLD : [0.4, 1.4, 1.3], boost = pl.boosting ? 1.3 : 1; put(darts, n++, pl.pos, pl.fwd, 1.4 * boost, 1.4 * boost, 1.5 * boost, pl.stingFlash > 0 ? [2, 0.3, 1.2] : col); }
      done(darts, n);
      n = 0;
      for (const q of w.trails) { if (!q.alive || n >= 1500) continue; const age = (t - q.t) / P.trailLife, k = 1 - age * 0.6, you = q.owner === human; put(trails, n++, q.p, q.fwd, 3, 3, 7, you ? [GOLD[0] * k * 0.7, GOLD[1] * k * 0.7, GOLD[2] * k * 0.7] : [JADE[0] * k, JADE[1] * k * 0.8, JADE[2] * k * 0.8]); }
      done(trails, n);
      // serpents
      n = 0;
      for (const s of w.serpents) {
        const die = s.dead ? Math.max(0, 1 - (t - s.dieT) / 1.5) : 1, grow = Math.min(1, (t - s.born) / 1.2), tierOf = (i) => w.poseTier(s, i);
        for (let i = 0; i < s.plan.n && n < 1400; i++) {
          if (!s.alive[i]) continue;
          const tier = tierOf(i), danger = tier === 1 && s.state !== 'hunt', base = EC[s.plan.elem[i]];
          const col = danger ? DANGER : tier === 2 ? [base[0] * 1.05, base[1] * 1.05, base[2] * 1.05] : [base[0] * 0.55, base[1] * 0.55, base[2] * 0.55];
          const k = 4.2 * die * grow * (danger ? 1.3 : 1);
          put(serps, n++, [s.world[3 * i], s.world[3 * i + 1], s.world[3 * i + 2]], s.fwd, k, k * 0.8, k * 0.8, col);
        }
      }
      done(serps, n);
      n = 0; for (const s of w.sharks) put(sharks, n++, s.pos, s.vel, 1.3, 0.9, 1.6, s.cool > 0 ? [0.5, 0.55, 0.7] : [0.75, 0.8, 1.0]); done(sharks, n);
      n = 0; for (const f of w.fish) put(fishes, n++, f.pos, f.vel, 2.2, 2.2, 6, [1.6, 0.35, 0.25]); done(fishes, n);
      n = 0; for (const c of w.claws) put(claws, n++, c.pos, c.vel, 9, 4, 6, c.on >= 0 ? [2, 0.9, 0.2] : [1.4, 0.55, 0.15]); done(claws, n);
      // flora: grows generation by generation; the old cell collapses into him
      const rule = S.RULES[S.PHASES[w.phase].ruleKey];
      n = 0;
      for (const f of w.flora) {
        if (!f.alive || n >= 4200) continue;
        const k = Math.max(0, Math.min(1, f.grow * 1.7 - (f.gen / (rule.gens + 1)) * 0.7)); if (k <= 0.01) continue;
        const c = EC[f.elem]; put(flora, n++, f.p, f.p, 8.5 * k, 8.5 * k, 8.5 * k, [c[0] * 0.75, c[1] * 0.75, c[2] * 0.75]);
      }
      done(flora, n);
      n = 0;
      for (const f of w.floraOld) { if (n >= 4200) break; const k = 1 - f.fall / 1.6, c = EC[f.elem]; put(floraOld, n++, V.mul(f.p, k), f.p, 12 * k, 12 * k, 12 * k, [c[0] * 1.6, c[1] * 1.4, c[2] * 1.4]); }
      done(floraOld, n);
      n = 0;
      for (const c of w.crystals) { if (!c.alive || n >= 600) continue; const sp = t * 2 + n, col = EC[c.elem], armed = c.armed > t ? 0.5 : 1; TV.set(Math.sin(sp), 1, Math.cos(sp)); put(crystals, n++, c.p, [TV.x, TV.y, TV.z], 7 * armed, 10 * armed, 7 * armed, [col[0] * 1.8, col[1] * 1.8, col[2] * 1.8]); }
      done(crystals, n);
      // the black hole
      const open = w.mouthOpen;
      bh.visible = open > 0.02 && !won; if (bh.visible) { bh.position.set(...j.mouth); const r = Math.max(1, P.bhStrength * 2) * open; bhCore.scale.setScalar(r); disk.scale.setScalar(r * 5.2); disk.lookAt(camera.position); }
      // the wave
      wave.visible = w.waveR > 0; if (wave.visible) { wave.scale.setScalar(w.waveR); waveMat.uniforms.uK.value = Math.max(0.15, 1 - w.waveR / P.cellRadius); }
      // the end
      if (won) {
        for (let i = 0; i < BURST; i++) { const v = burstVel[i], k = Math.max(0, 1 - wt * 0.15); burstPos[i * 3] += v[0] * dt * k; burstPos[i * 3 + 1] += v[1] * dt * k; burstPos[i * 3 + 2] += v[2] * dt * k; }
        burstGeo.attributes.position.needsUpdate = true; burst.material.opacity = Math.max(0, 1 - wt / 9);
        shocks.forEach((s, k) => { const r = 40 + (wt - k * 0.25) * (900 + k * 300); s.visible = r > 0 && wt < 6; s.scale.setScalar(Math.max(1, r)); s.material.opacity = Math.max(0, 1 - wt / 5); });
        if (bloom) bloom.strength = 0.85 + 0.9 * Math.max(0, 1 - wt / 4);
        skyMat.uniforms.uTint.value.set(1 + 0.8 * Math.max(0, 1 - wt / 3), 1 + 0.5 * Math.max(0, 1 - wt / 3), 1);
      } else {
        if (bloom) bloom.strength = 0.85 + view.flash * 0.7;
        const dim = view.lostT >= 0 ? Math.max(0.25, 1 - (t - view.lostT) / 3) : 1; skyMat.uniforms.uTint.value.set(dim + view.flash * 0.6, dim + view.flash * 0.4, dim);
      }
      view.flash = Math.max(0, view.flash - dt * 1.2); view.shake = Math.max(0, view.shake - dt * 1.4);
      // the camera
      const follow = w.pilots[human >= 0 ? human : view.follow] || w.pilots[0];
      if (view.cam === 'chase' && follow && !won) {
        const want = new THREE.Vector3(...V.add(V.add(follow.pos, V.mul(follow.fwd, -85)), V.mul(follow.up, 26)));
        camPos.lerp(want, 1 - Math.exp(-dt * 6)); camLook.lerp(new THREE.Vector3(...V.add(follow.pos, V.mul(follow.fwd, 90))), 1 - Math.exp(-dt * 8));
        camera.up.set(follow.up[0], follow.up[1], follow.up[2]);
      } else if (view.cam === 'cinematic' || won) {
        const a = (w.yaw || 0) + 0.65 * Math.sin(t * 0.05), R = won ? 2900 + wt * 120 : 1750 + 650 * Math.sin(t * 0.06), lit = w.rings[w.ringIx];
        camPos.lerp(new THREE.Vector3(R * Math.sin(a), 120 + 300 * Math.sin(t * 0.07) + (R - 1100) * 0.2, R * Math.cos(a)), 1 - Math.exp(-dt * 1.5));
        const lt = new THREE.Vector3(0, 80, 0); if (lit && !won) lt.lerp(new THREE.Vector3(...lit.c), 0.25);
        camLook.lerp(lt, 1 - Math.exp(-dt * 2)); camera.up.set(0, 1, 0);
      }
      if (view.cam !== 'orbit' || won) { camera.position.copy(camPos); if (view.shake > 0) camera.position.add(new THREE.Vector3((Math.random() - 0.5) * view.shake * 18, (Math.random() - 0.5) * view.shake * 18, (Math.random() - 0.5) * view.shake * 18)); camera.lookAt(camLook); }
      else if (controls) controls.update();
      if (composer) composer.render(); else renderer.render(scene, camera);
    }
    function setCam(m) {
      view.cam = m; lsSet('cam', m); $('camBtn').textContent = 'Camera: ' + m;
      if (controls) { controls.enabled = m === 'orbit'; if (m === 'orbit') { controls.target.set(0, 60, 0); camera.up.set(0, 1, 0); } }
    }
    setCam(view.cam);
    return { render, reset, events, setCam, view, toast };
  }

  // ═════════════════════════════════════════════ the HUD ═════════════════════════════════════════════
  const squadBox = $('squad');
  function hud() {
    const w = world, ph = S.PHASES[w.phase];
    $('hPhaseN').textContent = 'Form ' + (w.phase + 1) + ' of 3';
    $('hForm').textContent = ph.name;
    const c = Math.max(0, w.clock), mm = Math.floor(c / 60), ss = Math.floor(c % 60);
    $('hClock').textContent = w.state === 'won' ? 'Won' : w.state === 'lost' ? '0:00' : w.state === 'play' ? mm + ':' + String(ss).padStart(2, '0') : 'Clap';
    $('hClock').classList.toggle('low', w.state === 'play' && c < 15);
    $('hRings').textContent = w.state === 'play' ? 'Ring ' + Math.min(w.ringIx + 1, w.rings.length) + ' of ' + w.rings.length : w.state === 'won' || w.state === 'lost' ? '' : 'The cell grows again';
    const ix = $('hShells').children; for (let k = 0; k < 3; k++) ix[k].className = w.shellUp[k] ? 'up' : '';
    if (squadBox.children.length !== w.pilots.length) {
      squadBox.innerHTML = '';
      for (let i = 0; i < w.pilots.length; i++) { const d = document.createElement('div'); d.className = 'pcard'; d.innerHTML = '<div class="nm"><span></span><em></em></div><div class="bars">' + [0, 1, 2, 3].map((e) => '<div title="' + S.ELEMENTS[e] + '"><span style="background:' + ECSS[e] + '"></span></div>').join('') + '</div><div class="chips"></div>'; squadBox.appendChild(d); }
    }
    for (const pl of w.pilots) {
      const d = squadBox.children[pl.i]; d.classList.toggle('you', pl.i === human);
      d.querySelector('.nm span').textContent = pl.hull; d.querySelector('.nm em').textContent = pl.i === human ? 'You' : 'AI';
      const bars = d.querySelectorAll('.bars span'); for (let e = 0; e < 4; e++) bars[e].style.width = (pl.el[e] * 10) + '%';
      const chips = []; if (world.t - pl.lastSting < 1.2) chips.push('<b>STUNG</b>'); if (pl.latched) chips.push('<b>CLAWFISH</b>'); if (pl.slowK > 0 && world.t < pl.slowUntil) chips.push('<b class="slow">SLOWED</b>'); if (pl.stripFlash > 0) chips.push('<b class="strip">STRIPPED</b>');
      const html = chips.join(''); const ch = d.querySelector('.chips'); if (ch.innerHTML !== html) ch.innerHTML = html;
    }
    const me = w.pilots[human];
    $('meter').hidden = !me; $('ctrlHint').hidden = !me || PLATFORM.device === 'phone';
    if (me) { $('hMeter').style.width = (me.H.free ? 100 : me.meter * 100) + '%'; $('hSpeed').textContent = Math.round(me.speed) + ' u/s' + (me.H.free ? ' · free boost' : ''); $('ctrlHint').textContent = 'W/S pitch · A/D yaw · Space boost'; }
  }

  // ═════════════════════════════════════════════ settings, scorecard, decisions ═════════════════════════════════════════════
  const outputs = {};
  function fmt(v, step) { const dec = step < 1 ? Math.min(3, (String(step).split('.')[1] || '').length) : 0; return Number(v).toFixed(dec); }
  function diffOnly() { const o = {}; for (const k of Object.keys(SHIPPED)) if (Math.abs(P[k] - SHIPPED[k]) > 1e-9) o[k] = P[k]; return o; }
  function refreshDiff() {
    const n = Object.keys(diffOnly()).length;
    $('diff').textContent = n ? n + ' setting' + (n > 1 ? 's differ' : ' differs') + ' from shipped' : 'shipped numbers';
    for (const [, key, , , step] of SPEC) { const o = outputs[key]; if (!o) continue; o.out.textContent = fmt(P[key], step); o.out.classList.toggle('changed', Math.abs(P[key] - SHIPPED[key]) > 1e-9); o.out.title = 'shipped ' + SHIPPED[key] + ' · yours ' + P[key]; }
  }
  const RESTART_KEYS = new Set(['ringsP1', 'ringsP2', 'ringsP3', 'ringOuter', 'ringStepDeg', 'ringRise', 'shell1', 'shell2', 'shell3', 'crystals', 'startLevel', 'serpentsP1', 'sharksP1', 'cellRadius']);
  let restartTimer = 0;
  function setParam(key, v) { P[key] = v; lsSet('P', diffOnly()); refreshDiff(); if (RESTART_KEYS.has(key)) { clearTimeout(restartTimer); restartTimer = setTimeout(() => reset(), 400); } }
  function makeTabs(barId, bodyId, tabs, storeKey) {
    const bar = $(barId), body = $(bodyId), sel = lsGet(storeKey, tabs[0].name);
    function show(name) { for (const b of bar.children) b.setAttribute('aria-selected', String(b.dataset.name === name)); body.replaceChildren(tabs.find((t) => t.name === name).build()); lsSet(storeKey, name); }
    for (const t of tabs) { const b = document.createElement('button'); b.textContent = t.name; b.dataset.name = t.name; b.setAttribute('role', 'tab'); b.onclick = () => show(t.name); bar.appendChild(b); }
    show(tabs.some((t) => t.name === sel) ? sel : tabs[0].name);
  }
  function sliderGroup(group) {
    const box = document.createElement('div');
    for (const [g, key, lo, hi, step, label, tip] of SPEC) {
      if (g !== group) continue;
      const row = document.createElement('div'); row.className = 'row';
      row.innerHTML = '<label><span></span><span class="key"></span></label><output></output><input type="range"><div class="tip"></div>';
      row.querySelector('label span').textContent = label; row.querySelector('.key').textContent = key; row.querySelector('.tip').textContent = tip;
      const inp = row.querySelector('input'); Object.assign(inp, { min: lo, max: hi, step, value: P[key], id: 'p-' + key });
      inp.oninput = () => setParam(key, parseFloat(inp.value));
      outputs[key] = { out: row.querySelector('output'), inp }; box.appendChild(row);
    }
    queueMicrotask(refreshDiff); return box;
  }
  function squadPanel() {
    const box = document.createElement('div');
    box.innerHTML = '<p class="note">Any squad hull can fly Shiva\'s cell. Each hull flies at its shipped cruise, top speed and turn; boost economies are coarse (see About).</p>';
    for (let i = 0; i < 3; i++) {
      const f = document.createElement('label'); f.className = 'field'; f.innerHTML = '<span>Seat ' + (i + 1) + '</span><select id="seat' + i + '"></select>';
      const sel = f.querySelector('select'); for (const h of S.HULL_NAMES) { const o = document.createElement('option'); o.value = h; o.textContent = h + ' (' + S.HULLS[h].cruise + '–' + S.HULLS[h].top + ' u/s)'; sel.appendChild(o); }
      sel.value = cfg.hulls[i]; sel.onchange = () => { cfg.hulls[i] = sel.value; lsSet('cfg', cfg); reset(); }; box.appendChild(f);
    }
    const presets = document.createElement('p'); presets.innerHTML = 'Squads: ';
    for (const [name, hulls] of Object.entries(SQUADS)) { const b = document.createElement('button'); b.className = 'btn'; b.textContent = name; b.onclick = () => { cfg.hulls = hulls.slice(); lsSet('cfg', cfg); reset(); show(); }; presets.appendChild(b); presets.append(' '); }
    box.appendChild(presets);
    const sp = document.createElement('label'); sp.className = 'field'; sp.innerHTML = '<span>Speed</span><select id="timeScale"><option value="0.5">Half</option><option value="1">Normal</option><option value="2">Double</option><option value="4">Four times</option></select>';
    sp.querySelector('select').value = String(cfg.speed); sp.querySelector('select').onchange = (e) => { cfg.speed = parseFloat(e.target.value); lsSet('cfg', cfg); }; box.appendChild(sp);
    const sd = document.createElement('label'); sd.className = 'field'; sd.innerHTML = '<span>Seed</span><input type="text" id="seedIn" inputmode="numeric">';
    const si = sd.querySelector('input'); si.value = cfg.seed; si.onchange = () => { reset(parseInt(si.value, 10) || 1); }; box.appendChild(sd);
    const snd = document.createElement('label'); snd.className = 'field'; snd.innerHTML = '<span>Sound</span><select id="soundSel"><option value="1">On</option><option value="0">Off</option></select>';
    snd.querySelector('select').value = cfg.sound ? '1' : '0'; snd.querySelector('select').onchange = (e) => { cfg.sound = e.target.value === '1'; lsSet('cfg', cfg); }; box.appendChild(snd);
    function show() { for (let i = 0; i < 3; i++) { const s = box.querySelector('#seat' + i); if (s) s.value = cfg.hulls[i]; } }
    return box;
  }
  makeTabs('dockTabs', 'dockBody', [{ name: 'Squad', build: squadPanel }].concat([...new Set(SPEC.map((r) => r[0]))].map((g) => ({ name: g, build: () => sliderGroup(g) }))), 'dock');

  // the scorecard: all-autopilot squads, run in slices so the page stays live
  let lastRows = null;
  function scorecardPanel() {
    const box = document.createElement('div');
    box.innerHTML = '<p><button class="btn primary" id="runScore">Score the squads</button> <span class="hint" id="scoreStatus">3 squads × 4 variants × 3 seeds, all on autopilot, with your settings.</span></p><div class="scroll-x"><table id="scoreTable"></table></div><p class="note" id="scoreRead"></p>';
    const status = box.querySelector('#scoreStatus'), table = box.querySelector('#scoreTable');
    box.querySelector('#runScore').onclick = () => {
      const jobs = []; for (const sq of Object.keys(SQUADS)) for (const v of VARIANTS) for (const s of [1, 2, 3]) jobs.push([sq, v, s]);
      const rows = []; let k = 0;
      (function next() {
        if (k >= jobs.length) { lastRows = rows; draw(); status.textContent = rows.length + ' runs.'; return; }
        const [sq, v, s] = jobs[k++]; rows.push(runOne(sq, v, s, 900)); status.textContent = 'Running ' + k + ' of ' + jobs.length + '…'; setTimeout(next, 0);
      })();
    };
    function draw() {
      if (!lastRows) return;
      const groups = {};
      for (const r of lastRows) (groups[r.squad + '|' + r.variant] = groups[r.squad + '|' + r.variant] || []).push(r);
      const mean = (a, f) => a.length ? a.reduce((s, r) => s + f(r), 0) / a.length : 0;
      const timeOf = (a, c) => { const v = a.filter((r) => r[c] > 0); return v.length ? (mean(v, (r) => r[c])).toFixed(0) : '–'; };
      table.innerHTML = '<tr><th>Squad</th><th>Variant</th><th>Win %</th><th>Still s</th><th>Nataraja s</th><th>Mouth s</th><th>Stings</th><th>Bumps</th><th>Rams</th><th>Nips</th><th>Latches</th><th>Levels ripped</th></tr>';
      for (const key of Object.keys(groups)) {
        const a = groups[key], [sq, vn] = key.split('|'), win = 100 * a.filter((r) => r.outcome === 'Won').length / a.length;
        const tr = document.createElement('tr');
        const cells = [sq, vn, win.toFixed(0), timeOf(a, 'p1'), timeOf(a, 'p2'), timeOf(a, 'p3'), mean(a, (r) => r.stings).toFixed(0), mean(a, (r) => r.bumps).toFixed(0), mean(a, (r) => r.rams).toFixed(0), mean(a, (r) => r.nips).toFixed(0), mean(a, (r) => r.latches).toFixed(0), mean(a, (r) => r.stripped).toFixed(0)];
        cells.forEach((c, i) => { const td = document.createElement('td'); td.textContent = c; if (i === 2) td.className = win >= 100 ? 'best' : win < 50 ? 'bad' : ''; tr.appendChild(td); });
        table.appendChild(tr);
      }
      box.querySelector('#scoreRead').textContent = 'Read it against the controls: "No swarm" is what the circuit alone costs, so the swarm\'s cost is the gap to "Your settings". "No clap rip" shows what losing your elements at each clap costs. A squad that wins every run with a long time left is a clock to shorten.';
    }
    draw(); return box;
  }
  // the decision log: the artifact's db collection "decisions" (Claude reads it), else this browser
  let db = null, me = '', remote = [], localLog = lsGet('log', []);
  async function record(topic, choice, note) {
    const doc = { topic, choice, note: note || '', createdAt: Date.now(), by: me, settings: { round: LAB.round, seed: cfg.seed, hulls: cfg.hulls.slice(), changed: diffOnly() } };
    if (db) { try { await db.collection('decisions').add(doc); return 'shared'; } catch (e) { /* fall through */ } }
    localLog.push(doc); lsSet('log', localLog); return 'local';
  }
  function logPanel() {
    const box = document.createElement('div');
    box.innerHTML = '<p class="hint" id="logStatus"></p><input type="text" id="logTopic" placeholder="Topic (for example: phase clocks)"><textarea id="logChoice" rows="2" placeholder="Your decision"></textarea><p><button class="btn primary" id="logSave">Record decision</button> <button class="btn" id="logCopy">Copy log</button></p><div class="scroll-x"><table id="logTable"></table></div>';
    const status = box.querySelector('#logStatus');
    status.textContent = db ? 'Shared log: saved with the lab, and Claude reads it.' : 'Kept in this browser (Copy log to send it).'; status.className = db ? 'status-ok' : 'status-warn';
    box.querySelector('#logChoice').value = 'Squad ' + cfg.hulls.join(' / ') + '; settings: ' + (Object.keys(diffOnly()).length ? JSON.stringify(diffOnly()) : 'shipped');
    box.querySelector('#logSave').onclick = async () => { const where = await record(box.querySelector('#logTopic').value || 'General', box.querySelector('#logChoice').value); status.textContent = where === 'shared' ? 'Recorded in the shared log.' : 'Recorded in this browser.'; renderLog(box); };
    box.querySelector('#logCopy').onclick = () => { const t = remote.concat(localLog).map((d) => new Date(d.createdAt).toISOString() + ' · ' + d.topic + ': ' + d.choice + ' ' + JSON.stringify(d.settings)).join('\n'); try { navigator.clipboard.writeText(t).catch(() => {}); } catch (e) { /* refused */ } };
    renderLog(box); return box;
  }
  function renderLog(box) {
    const t = (box || document).querySelector('#logTable'); if (!t) return;
    const rows = remote.concat(localLog).sort((a, b) => b.createdAt - a.createdAt).slice(0, 50);
    t.innerHTML = '<tr><th>When</th><th>Topic</th><th>Decision</th></tr>';
    for (const d of rows) { const tr = document.createElement('tr'); for (const v of [new Date(d.createdAt).toLocaleString(), d.topic, d.choice]) { const td = document.createElement('td'); td.textContent = v; tr.appendChild(td); } t.appendChild(tr); }
  }
  (async function connect() {
    const c = window.claude; if (!c || typeof c.use !== 'function') return;
    try { db = await c.use('db'); } catch (e) { db = null; }
    try { const u = await c.use('user'); if (u) me = (await u.id()) || ''; } catch (e) { me = ''; }
    if (!db) return;
    try { db.collection('decisions').orderBy('createdAt', 'desc').limit(300).onSnapshot((snap) => { remote = snap.docs.map((d) => Object.assign({ id: d.id }, d.data())); renderLog(); }, () => { db = null; }); } catch (e) { db = null; }
  })();
  function aboutPanel() {
    const box = document.createElement('div');
    box.innerHTML = `<p class="note"><b>${LAB.name}</b> · round ${LAB.round} · ${LAB.branch} · write-up ${LAB.doc}. Decides: the phase clocks, the circuit, how hard the swarm guards the rings, what the clap and the mouth cost, before Tandava is rebuilt in Unity.</p>
      <p class="note"><b>Read from the game:</b> hull cruise, top and turn (the prefabs, via regatta_balance.py, and the arena fleet table); the ring mouth (HeadlongCircuit, intensity 2); the black hole's pull cap and scale (BlackHoleConfig.asset); the piranha's speeds (Astro League Piranha config); the serpents' bodies (the shipped Tandava many-headed plans) and their lunge (TandavaDirectorSettings). Everything else is a lab proposal and says so in the source.</p>
      <p class="note"><b>Not modelled:</b> each hull's own boost economy and abilities (a coarse free-or-meter boost stands in); the sort core (a serpent here is its plan moved rigidly, not a swarm); prism physics, colliders and frame time; the real gyroid colonies (a 3D cellular automaton stands in for the regrown cell); multiplayer and network latency; the Ark; the game's AIPilot (this autopilot writes the same inputs, but it is not that code).</p>`;
    return box;
  }
  makeTabs('bottomTabs', 'bottomBody', [{ name: 'Scorecard', build: scorecardPanel }, { name: 'Decisions', build: logPanel }, { name: 'About', build: aboutPanel }], 'bottom');

  // ── header buttons ──
  $('meta').textContent = 'round ' + LAB.round;
  function fly(seat) { human = seat; $('flyBtn').textContent = human >= 0 ? 'Watch' : 'Fly seat 1'; $('flyBtn').classList.toggle('on', human >= 0); $('brief').hidden = true; reset(); if (fx) fx.setCam(human >= 0 ? 'chase' : 'cinematic'); squadBox.innerHTML = ''; }
  $('flyBtn').onclick = () => fly(human >= 0 ? -1 : 0);
  $('briefFly').onclick = () => fly(0);
  $('briefWatch').onclick = () => fly(-1);
  $('againBtn').onclick = () => { $('endCard').hidden = true; reset(); };
  $('camBtn').onclick = () => { if (!fx) return; const order = ['cinematic', 'chase', 'orbit']; fx.setCam(order[(order.indexOf(fx.view.cam) + 1) % 3]); };
  $('pause').onclick = () => { paused = !paused; $('pause').textContent = paused ? 'Resume' : 'Pause'; };
  $('restart').onclick = () => reset();
  $('resetAll').onclick = () => { Object.assign(P, SHIPPED); lsSet('P', {}); for (const k in outputs) outputs[k].inp.value = P[k]; reset(); refreshDiff(); };

  // ── the loop, with a manual clock for recorders ──
  let manualClock = false, last = performance.now();
  function frame(now) {
    const dt = Math.min(0.05, Math.max(0, (now - last) / 1000)); last = now;
    if (!manualClock) { if (!paused) simStep(dt); if (fx) fx.render(paused ? 0 : dt * cfg.speed); hud(); }
    requestAnimationFrame(frame);
  }
  reset(); refreshDiff(); hud(); requestAnimationFrame(frame);

  window.__lab = window.__shiva = {
    LAB, SHIPPED, SPEC, P, PLATFORM, VARIANTS, SQUADS,
    set manualClock(v) { manualClock = !!v; }, get manualClock() { return manualClock; },
    tick(dt, n) { n = n || 1; dt = dt || S.DT; for (let i = 0; i < n; i++) { readInput(); world.step(dt); if (fx) fx.events(world.events); world.events.length = 0; } if (fx) fx.render(dt); hud(); },
    reset, score, runBatch, setParam, record, fly,
    state: () => ({ t: world.t, seed: cfg.seed, phase: world.phase, state: world.state, ring: world.ringIx, rings: world.rings.length, clock: world.clock, outcome: world.outcome, hulls: cfg.hulls.slice() }),
    world: () => world,
  };
})();
