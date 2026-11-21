(function () {
  'use strict';
  const S = window.TandavaSim, V = S.vec;
  const $ = id => document.getElementById(id);
  const REDUCED = !!(window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches);
  const fmt = n => Math.round(n).toLocaleString('en-US');
  const mmss = s => { s = Math.max(0, Math.ceil(s)); return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`; };
  const pct = x => `${Math.round(100 * Math.max(0, x))}%`;
  const cap = s => s ? s[0].toUpperCase() + s.slice(1) : '';

  // ── colours: the elements, the one-colour law, the tiers, and the gold of every burst (no fire anywhere) ──
  const EC = [[0.98, 0.75, 0.18], [0.93, 0.27, 0.38], [0.24, 0.60, 0.95], [0.62, 0.42, 0.94]];
  const ENAME = ['Charge', 'Mass', 'Space', 'Time'], ECSS = ['var(--charge)', 'var(--mass)', 'var(--space)', 'var(--time)'];
  const ONE = [0.80, 0.12, 0.78], ONE_HI = [1.1, 0.62, 1.2];
  const DANGER = [1.95, 0.28, 1.6], DANGER_ONE = [1.9, 1.5, 1.8], SHIELD = [0.95, 1.08, 1.4], GOLD = [1.55, 1.1, 0.36], GOLD_DEEP = [1.15, 0.68, 0.14], PEARL = [1.45, 1.36, 1.15];
  const JADE = [0.12, 1.0, 0.92], WING = [0.5, 0.86, 1.15];
  // the cell's own colours (the standard cell), and TandavaSettings.AscensionPalette: gold, for the rise and the dance alone
  const BASE = { mb: [0.36976, 0.39672, 0.95597], md: [0, 0.02759, 1], ne: [0, 0.0235, 0.749], ns: [0.325, 0.348, 0.755], cy: [0.3805, 0.3805, 0.5] };
  const ASC = { mb: [1.4, 1.12, 0.42], md: [0.16, 0.11, 0.02], ne: [1, 0.8, 0.3], ns: [1.15, 0.96, 0.52], cy: [0.86, 0.72, 0.36] };
  // TandavaSettings.asset's narration and HUD labels
  const LINES = {
    start: 'Something in the reef remembers the old shapes.',
    feeding: 'It rolls up round the plant in {0} - its guards circle it. Strike the body.',
    feedingPlain: 'It is feeding - its guards are out round its mouth. Strike the body.',
    mealBroken: 'Its meal is broken. It bolts.',
    rising: 'It has eaten enough. It rises into the Lord of the Dance.',
    haloLit: 'The halo is lit. Break the rings before the drum stops.',
    firstHalo: n => `A ring is broken. Break ${n} and the dance falls.`,
    lastHalo: (a, b) => `${a} of ${b}. One more ring!`,
    completed: 'The Antlion has fed. The cycle is complete.',
    won: 'Its body is broken. The reef keeps its turn.',
    danceBroken: 'The dance is broken. The reef keeps its turn.',
    heldOff: 'Time. You held it off - the cycle is unfinished.',
  };
  const LABEL = { Calm: 'Roaming', Wary: 'Wary', Fleeing: 'Fleeing', Lunging: 'Lunging - mind its guards', Feed: 'Feeding - strike the body', Rising: 'Rising', Dance: 'Dancing - break the halo' };
  const OUT = {
    Shattered: ['Victory', 'Its body is broken', LINES.won], Starved: ['Victory', 'It starved to pieces', LINES.won],
    Wiped: ['Victory', 'The swarm is gone', LINES.won], DanceBroken: ['Victory', 'The dance is broken', LINES.danceBroken],
    HeldOff: ['Victory', 'You held it off', LINES.heldOff], Completed: ['Defeat', 'The cycle is complete', LINES.completed],
  };

  const ui = { mode: 'hunt', speed: 1, cam: 'creature', colour: 'element', sound: true, paused: false, ai: 'off', tour: true };
  let bank, hunt, pg, matchSeed = 1 + Math.floor(Math.random() * 9000);

  // ── sound: synthesized, started by the first click ─────────────────────────
  const audio = {
    ctx: null, master: null, noise: null,
    init() {
      if (this.ctx) { if (this.ctx.state === 'suspended') this.ctx.resume(); return; }
      const AC = window.AudioContext || window.webkitAudioContext; if (!AC) return;
      try {
        this.ctx = new AC(); this.master = this.ctx.createGain(); this.master.gain.value = 0.5; this.master.connect(this.ctx.destination);
        const n = this.ctx.sampleRate, buf = this.ctx.createBuffer(1, n, n), d = buf.getChannelData(0);
        for (let i = 0; i < n; i++) d[i] = Math.random() * 2 - 1;
        this.noise = buf;
      } catch (e) { this.ctx = null; }
    },
    ok() { return ui.sound && this.ctx && this.ctx.state === 'running'; },
    tone(type, f0, f1, dur, gain, delay) {
      const c = this.ctx, t = c.currentTime + (delay || 0), o = c.createOscillator(), g = c.createGain();
      o.type = type; o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t + dur);
      g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(gain, t + 0.008); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
      o.connect(g); g.connect(this.master); o.start(t); o.stop(t + dur + 0.05);
    },
    hiss(freq, q, dur, gain, delay, type) {
      const c = this.ctx, t = c.currentTime + (delay || 0), s = c.createBufferSource(), f = c.createBiquadFilter(), g = c.createGain();
      s.buffer = this.noise; f.type = type || 'bandpass'; f.frequency.value = freq; f.Q.value = q;
      g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(gain, t + 0.01); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
      s.connect(f); f.connect(g); g.connect(this.master); s.start(t); s.stop(t + dur + 0.05);
    },
    drum(u) { if (!this.ok()) return; this.tone('sine', 150 + 40 * u, 70, 0.28, 0.45); this.tone('sine', 230 + 60 * u, 120, 0.14, 0.26, 0.075); this.hiss(1400, 1.2, 0.05, 0.14); this.hiss(1700, 1.2, 0.05, 0.1, 0.075); },
    gong(big) { if (!this.ok()) return; const g = big ? 0.42 : 0.28; this.tone('sine', 98, 84, big ? 3.6 : 2.4, g); this.tone('sine', 294, 262, big ? 2.6 : 1.8, g * 0.3); this.tone('triangle', 784, 760, 1.4, g * 0.1); },
    chime() { if (!this.ok()) return; this.tone('sine', 1320, 1300, 0.9, 0.16); this.tone('sine', 1980, 1960, 0.7, 0.08, 0.02); this.tone('triangle', 660, 650, 0.6, 0.06); },
    hit() { if (!this.ok()) return; this.tone('triangle', 900, 320, 0.07, 0.12); this.hiss(2600, 2, 0.04, 0.08); },
    sting() { if (!this.ok()) return; this.tone('square', 1400, 260, 0.22, 0.07); this.hiss(4200, 1.5, 0.18, 0.12, 0, 'highpass'); },
    snap() { if (!this.ok()) return; this.tone('square', 260, 70, 0.09, 0.14); this.hiss(3200, 3, 0.05, 0.16, 0, 'highpass'); this.tone('triangle', 120, 60, 0.18, 0.12, 0.02); },
    deny() { if (!this.ok()) return; this.tone('square', 170, 130, 0.2, 0.06); this.tone('square', 125, 95, 0.26, 0.06, 0.1); },
    boom() { if (!this.ok()) return; this.tone('sine', 72, 34, 2.2, 0.6); this.hiss(320, 0.6, 1.2, 0.3, 0, 'lowpass'); this.tone('sine', 523, 520, 2.4, 0.07, 0.15); },
  };
  window.addEventListener('pointerdown', () => audio.init(), { capture: true });
  window.addEventListener('keydown', () => audio.init(), { capture: true });

  // ── three.js ───────────────────────────────────────────────────────────────
  const canvas = $('gl'), view = $('view');
  let renderer = null;
  try { if (window.THREE) renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' }); } catch (e) { renderer = null; }
  if (!renderer) { $('glfail').hidden = false; $('brief').hidden = true; return; }
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x05060c);
  scene.fog = new THREE.FogExp2(0x05060c, 0.00034);
  const camera = new THREE.PerspectiveCamera(50, 1, 2, 12000);
  const controls = new THREE.OrbitControls(camera, canvas);
  controls.enableDamping = true; controls.dampingFactor = 0.08; controls.minDistance = 40; controls.maxDistance = 4200;
  let composer = null;
  try {
    if (THREE.EffectComposer && THREE.UnrealBloomPass) {
      const rt = new THREE.WebGLRenderTarget(2, 2, { type: THREE.HalfFloatType, format: THREE.RGBAFormat });
      composer = new THREE.EffectComposer(renderer, rt);
      composer.addPass(new THREE.RenderPass(scene, camera));
      composer.addPass(new THREE.UnrealBloomPass(new THREE.Vector2(2, 2), 0.85, 0.5, 0.62));
    }
  } catch (e) { composer = null; }
  let viewH = 600;
  function resize() {
    const w = Math.max(1, view.clientWidth), h = Math.max(1, view.clientHeight);
    renderer.setSize(w, h, false); viewH = h;
    if (composer) composer.setSize(w, h);
    camera.aspect = w / h; camera.updateProjectionMatrix();
  }
  new ResizeObserver(resize).observe(view);

  // the prism read: the face lit, a bright fresnel rim and a glowing edge
  const PRISM_VS = `varying vec3 vN; varying vec3 vV; varying vec3 vC; varying vec3 vL;
    void main() {
      mat4 im = instanceMatrix;
      vec4 wp = modelMatrix * im * vec4(position, 1.0);
      vN = normalize(mat3(modelMatrix * im) * normal);
      vV = cameraPosition - wp.xyz;
      #ifdef USE_INSTANCING_COLOR
        vC = instanceColor;
      #else
        vC = vec3(1.0);
      #endif
      vL = position;
      gl_Position = projectionMatrix * viewMatrix * wp;
    }`;
  const PRISM_FS = `uniform vec3 uLight; varying vec3 vN; varying vec3 vV; varying vec3 vC; varying vec3 vL;
    void main() {
      vec3 n = normalize(vN); vec3 v = normalize(vV);
      float fres = pow(1.0 - abs(dot(n, v)), 2.0);
      vec3 a = abs(vL) * 2.0;
      float e = max(a.x * a.y, max(a.y * a.z, a.x * a.z));
      float edge = smoothstep(0.80, 0.97, e);
      float lam = 0.5 + 0.5 * max(dot(n, uLight), 0.0);
      gl_FragColor = vec4(vC * (0.22 + 0.5 * lam) + vC * fres * 1.25 + vC * edge * 0.85, 1.0);
    }`;
  const prismMat = new THREE.ShaderMaterial({ uniforms: { uLight: { value: new THREE.Vector3(-0.35, 0.8, -0.45).normalize() } }, vertexShader: PRISM_VS, fragmentShader: PRISM_FS });
  const box = new THREE.BoxGeometry(1, 1, 1);
  const tmpC = new THREE.Color(1, 1, 1);
  const ZERO = new THREE.Matrix4().makeScale(0, 0, 0);
  function instanced(geo, mat, n) {
    const m = new THREE.InstancedMesh(geo, mat, n);
    m.instanceMatrix.setUsage(THREE.DynamicDrawUsage); m.frustumCulled = false;
    for (let i = 0; i < n; i++) { m.setColorAt(i, tmpC); m.setMatrixAt(i, ZERO); }
    return m;
  }
  const MAX_MEMBERS = 800, MAX_SHARDS = 1100;
  // every plant owns a fixed run of instances, its species' prism budget (a replant keeps its species)
  const PLANT_SLOTS = [];
  { let at = 0; for (const f of S.REEF) for (let k = 0; k < f.count; k++) { PLANT_SLOTS.push({ start: at, n: f.prisms }); at += f.prisms; } PLANT_SLOTS.total = at; }
  const PLANT_CAP = Math.max(PLANT_SLOTS.total, 140);
  const members = instanced(box, prismMat, MAX_MEMBERS);
  const plantMesh = instanced(box, prismMat, PLANT_CAP);
  const shardMesh = instanced(box, prismMat, MAX_SHARDS);
  scene.add(members, plantMesh, shardMesh);

  // the cell: the membrane (a fresnel wash and the capsules it is built of), the cytoplasm's motes, the glass nucleus
  const R = S.ARENA.membrane;
  const membraneMat = new THREE.ShaderMaterial({
    uniforms: { uBright: { value: new THREE.Color() }, uDull: { value: new THREE.Color() } },
    vertexShader: `varying vec3 vN; varying vec3 vV; void main(){ vec4 mv = modelViewMatrix * vec4(position, 1.0);
      vN = normalize(normalMatrix * normal); vV = normalize(-mv.xyz); gl_Position = projectionMatrix * mv; }`,
    fragmentShader: `uniform vec3 uBright; uniform vec3 uDull; varying vec3 vN; varying vec3 vV;
      void main(){ float f = pow(1.0 - abs(dot(normalize(vN), normalize(vV))), 1.6); gl_FragColor = vec4(mix(uDull * 0.08, uBright * 0.5, f), 1.0); }`,
    side: THREE.BackSide, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false,
  });
  scene.add(new THREE.Mesh(new THREE.SphereGeometry(R + 30, 64, 48), membraneMat));
  const CAPS = 1100;
  const capsules = instanced(new THREE.CylinderGeometry(1, 1, 1, 6, 1), new THREE.MeshBasicMaterial({ fog: true, toneMapped: false }), CAPS);
  const capMix = new Float32Array(CAPS);
  {
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), p = new THREE.Vector3(), s = new THREE.Vector3(), n = new THREE.Vector3(),
      t = new THREE.Vector3(), Y = new THREE.Vector3(0, 1, 0), r = S.rngOf(5), ga = Math.PI * (3 - Math.sqrt(5));
    for (let i = 0; i < CAPS; i++) {
      const y = 1 - 2 * (i + 0.5) / CAPS, rr = Math.sqrt(1 - y * y);
      n.set(Math.cos(ga * i) * rr, y, Math.sin(ga * i) * rr);
      p.copy(n).multiplyScalar(R + 12);
      t.set(r() - 0.5, r() - 0.5, r() - 0.5).cross(n).normalize();
      q.setFromUnitVectors(Y, t);
      s.set(3.2, 26 + r() * 46, 3.2);
      m.compose(p, q, s); capsules.setMatrixAt(i, m); capMix[i] = r();
    }
  }
  scene.add(capsules);
  const softSprite = (() => {
    const c = document.createElement('canvas'); c.width = c.height = 64;
    const g = c.getContext('2d'), gr = g.createRadialGradient(32, 32, 0, 32, 32, 32);
    gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(0.35, 'rgba(255,255,255,.45)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = gr; g.fillRect(0, 0, 64, 64);
    return new THREE.CanvasTexture(c);
  })();
  const moteMat = new THREE.PointsMaterial({ size: 14, map: softSprite, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false });
  {
    const pts = [], r = S.rngOf(3);
    for (let x = -R; x <= R; x += 180) for (let y = -R; y <= R; y += 180) for (let z = -R; z <= R; z += 180) {
      const px = x + (r() - 0.5) * 90, py = y + (r() - 0.5) * 90, pz = z + (r() - 0.5) * 90;
      if (Math.hypot(px, py, pz) < R - 60) pts.push(px, py, pz);
    }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
    scene.add(new THREE.Points(g, moteMat));
  }
  const nucleusMat = new THREE.ShaderMaterial({
    uniforms: { uEdge: { value: new THREE.Color() } },
    vertexShader: `varying vec3 vN; varying vec3 vV; void main(){ vec4 mv = modelViewMatrix * vec4(position, 1.0);
      vN = normalize(normalMatrix * normal); vV = normalize(-mv.xyz); gl_Position = projectionMatrix * mv; }`,
    fragmentShader: `uniform vec3 uEdge; varying vec3 vN; varying vec3 vV;
      void main(){ float f = pow(1.0 - abs(dot(normalize(vN), normalize(vV))), 2.2); gl_FragColor = vec4(uEdge * (0.02 + f * 0.32), 1.0); }`,
    transparent: true, blending: THREE.AdditiveBlending, depthWrite: false,
  });
  scene.add(new THREE.Mesh(new THREE.SphereGeometry(S.ARENA.nucleus, 48, 32), nucleusMat));
  const cageMat = new THREE.LineBasicMaterial({ transparent: true, opacity: 0.13, blending: THREE.AdditiveBlending, toneMapped: false, depthWrite: false });
  scene.add(new THREE.LineSegments(new THREE.EdgesGeometry(new THREE.IcosahedronGeometry(S.ARENA.nucleus + 2, 1)), cageMat));

  // the darts: you and the wing, each with a trail
  const dartGeo = new THREE.ConeGeometry(4.2, 24, 7); dartGeo.rotateZ(-Math.PI / 2);
  const darts = [0, 1, 2].map(k => {
    const mat = new THREE.MeshBasicMaterial({ color: new THREE.Color(...(k ? WING : JADE)), toneMapped: false });
    const mesh = new THREE.Mesh(dartGeo, mat); mesh.visible = false; scene.add(mesh);
    const tg = new THREE.BufferGeometry(); tg.setAttribute('position', new THREE.BufferAttribute(new Float32Array(40 * 3), 3).setUsage(THREE.DynamicDrawUsage));
    const trail = new THREE.Line(tg, new THREE.LineBasicMaterial({ color: new THREE.Color(...(k ? WING : JADE)), transparent: true, opacity: 0.55, blending: THREE.AdditiveBlending, toneMapped: false, depthWrite: false }));
    trail.frustumCulled = false; trail.visible = false; scene.add(trail);
    const halo = new THREE.Sprite(new THREE.SpriteMaterial({ map: softSprite, color: new THREE.Color(...(k ? WING : JADE)), transparent: true, opacity: 0.5, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
    halo.scale.setScalar(k ? 22 : 34); halo.visible = false; scene.add(halo);
    return { mesh, mat, trail, halo };
  });

  // the halo rings (pearl), the target plant's marker, flashes and shock rings
  const ringGeo = new THREE.TorusGeometry(S.HALO.mouth, 2.4, 8, 56);
  let haloMeshes = [], haloRef = null;
  function buildHaloMeshes(halo) {
    clearHaloMeshes(true);
    haloRef = halo;
    haloMeshes = halo.rings.map(r => {
      const m = new THREE.Mesh(ringGeo, new THREE.MeshBasicMaterial({ color: new THREE.Color(...PEARL), transparent: true, opacity: 0.95, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
      m.position.set(...r.pos);
      m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 0, 1), new THREE.Vector3(...r.axis));
      m.userData = { r, drawn: 0, wither: -1 };
      scene.add(m); return m;
    });
  }
  function clearHaloMeshes(now) {
    for (const m of haloMeshes) { if (now) { scene.remove(m); m.material.dispose(); } else if (m.userData.wither < 0) m.userData.wither = 0; }
    if (now) haloMeshes = [];
    haloRef = null;
  }
  const markerMat = new THREE.MeshBasicMaterial({ color: new THREE.Color(...JADE), transparent: true, opacity: 0.8, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false });
  const marker = new THREE.Mesh(new THREE.TorusGeometry(64, 0.7, 4, 96), markerMat); marker.visible = false; scene.add(marker);
  const flashes = [...Array(8)].map(() => {
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: softSprite, color: new THREE.Color(...GOLD), transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
    s.visible = false; s.userData = { t: 1 }; scene.add(s); return s;
  });
  let flashIx = 0;
  function flashAt(p, rgb, size, dur) {
    const s = flashes[flashIx++ % flashes.length];
    s.position.set(p[0], p[1], p[2]); s.material.color.setRGB(...rgb); s.userData = { t: 0, size, dur: dur || 0.6 }; s.visible = true;
  }
  const shocks = [...Array(12)].map(() => {
    const m = new THREE.Mesh(new THREE.RingGeometry(0.88, 1, 64), new THREE.MeshBasicMaterial({ transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false, side: THREE.DoubleSide }));
    m.visible = false; m.userData = { t: 1 }; scene.add(m); return m;
  });
  let shockIx = 0;
  function shockAt(p, rgb, size, dur) {
    const m = shocks[shockIx++ % shocks.length];
    m.position.set(p[0], p[1], p[2]); m.userData = { t: 0, size, dur: dur || 0.5 }; m.material.color.setRGB(...rgb); m.visible = true;
  }

  // ── gold prism debris (TandavaGoldBurst): prism shards thrown from where the thing was, tumbling, shrinking ──
  const shards = [];
  function shard(at, vel, rgb, size, life) {
    if (shards.length >= MAX_SHARDS) shards.shift();
    const ax = V.norm([Math.random() - 0.5, Math.random() - 0.5, Math.random() - 0.5]);
    shards.push({ p: at.slice(), v: vel, rgb, size, life, max: life, ax: new THREE.Vector3(...ax), spin: (Math.random() - 0.5) * 14, ang: Math.random() * 6 });
  }
  function burst(at, n, speed, size, life, palette) {
    for (let i = 0; i < n; i++) {
      const th = Math.random() * 2 * Math.PI, z = Math.random() * 2 - 1, rr = Math.sqrt(1 - z * z), s = speed * (0.4 + Math.random() * 0.6);
      const rgb = palette ? palette[i % palette.length] : (i % 3 ? GOLD : GOLD_DEEP);
      shard(at, [Math.cos(th) * rr * s, z * s, Math.sin(th) * rr * s], rgb, size * (0.6 + Math.random() * 0.7), life * (0.7 + Math.random() * 0.5));
    }
  }
  /** The creature's own members throw the gold: n shards from n of its members, flung out from its centre. */
  function burstFromBody(sw, n, speed, size) {
    const live = []; for (let i = 0; i < sw.cap; i++) if (sw.isLive(i)) live.push(i);
    if (!live.length) return;
    const c = sw.anchor, w = [0, 0, 0];
    for (let q = 0; q < n; q++) {
      const i = live[Math.floor(Math.random() * live.length)];
      sw.memberWorld(i, w);
      const out = V.norm(V.sub(w, c)), s = speed * (0.5 + Math.random() * 0.8);
      shard(w, [out[0] * s + (Math.random() - 0.5) * 20, out[1] * s + 10, out[2] * s + (Math.random() - 0.5) * 20], q % 3 ? GOLD : GOLD_DEEP, size * (0.7 + Math.random() * 0.6), 1.4 + Math.random() * 0.8);
    }
  }
  let lastShardN = 0;
  const SM = new THREE.Matrix4(), SQ = new THREE.Quaternion(), SP = new THREE.Vector3(), SS = new THREE.Vector3();
  function drawShards(dt) {
    let n = 0;
    for (let i = shards.length - 1; i >= 0; i--) {
      const q = shards[i]; q.life -= dt;
      if (q.life <= 0) { shards.splice(i, 1); continue; }
      const k = Math.pow(0.35, dt); q.v[0] *= k; q.v[1] *= k; q.v[2] *= k;
      q.p[0] += q.v[0] * dt; q.p[1] += q.v[1] * dt; q.p[2] += q.v[2] * dt; q.ang += q.spin * dt;
    }
    for (const q of shards) {
      if (n >= MAX_SHARDS) break;
      const f = Math.min(1, q.life / q.max * 1.6), s = q.size * f;
      SP.set(q.p[0], q.p[1], q.p[2]); SQ.setFromAxisAngle(q.ax, q.ang); SS.set(s * 1.6, s, s * 0.5);
      SM.compose(SP, SQ, SS); shardMesh.setMatrixAt(n, SM);
      tmpC.setRGB(q.rgb[0] * (0.6 + 0.6 * f), q.rgb[1] * (0.6 + 0.6 * f), q.rgb[2] * (0.6 + 0.6 * f)); shardMesh.setColorAt(n, tmpC);
      n++;
    }
    for (let i = n; i < lastShardN; i++) shardMesh.setMatrixAt(i, ZERO);
    lastShardN = n;
    shardMesh.instanceMatrix.needsUpdate = true; if (shardMesh.instanceColor) shardMesh.instanceColor.needsUpdate = true;
  }

  // ── the cell's colour (CellVisualTint): its own colours always, gold for the rise and the dance alone ──
  const clonePal = p => ({ mb: p.mb.slice(), md: p.md.slice(), ne: p.ne.slice(), ns: p.ns.slice(), cy: p.cy.slice() });
  const lerpPal = (a, b, t) => { const o = {}; for (const k in a) o[k] = a[k].map((v, i) => v + (b[k][i] - v) * t); return o; };
  const tint = { from: clonePal(BASE), to: clonePal(BASE), shown: clonePal(BASE), t0: 0, sec: 1, flash: 0, active: true, pulse: 0, gold: false };
  const now = () => performance.now() / 1000;
  function tintTo(pal, seconds, flash) { tint.settled = false; tint.from = clonePal(tint.shown); tint.to = clonePal(pal); tint.sec = Math.max(0.05, seconds); tint.flash = flash || 0; tint.t0 = now(); tint.active = true; tint.gold = pal === ASC; }
  function updateTint(dt) {
    tint.pulse = Math.max(0, tint.pulse - dt * 3.2);
    if (!tint.active && tint.pulse <= 0 && tint.settled) return;
    tint.settled = !tint.active && tint.pulse <= 0;
    const t = Math.min(1, Math.max(0, (now() - tint.t0) / tint.sec)), eased = t * t * (3 - 2 * t);
    const bloom = tint.flash > 0 && t < 1 ? tint.flash * Math.sin(Math.PI * Math.min(1, t * 2)) * (t < 0.2 ? t / 0.2 : 1) : 0;
    if (tint.active) tint.shown = lerpPal(tint.from, tint.to, eased);
    applyPalette(tint.shown, 1 + bloom + tint.pulse * 0.4);
    if (t >= 1) tint.active = false;
  }
  function applyPalette(p, k) {
    membraneMat.uniforms.uBright.value.setRGB(p.mb[0] * k, p.mb[1] * k, p.mb[2] * k);
    membraneMat.uniforms.uDull.value.setRGB(p.md[0] * k, p.md[1] * k, p.md[2] * k);
    for (let i = 0; i < CAPS; i++) {
      const m = capMix[i], f = (0.16 + 0.36 * m) * k;
      tmpC.setRGB((p.md[0] + (p.mb[0] - p.md[0]) * m) * f, (p.md[1] + (p.mb[1] - p.md[1]) * m) * f, (p.md[2] + (p.mb[2] - p.md[2]) * m) * f);
      capsules.setColorAt(i, tmpC);
    }
    capsules.instanceColor.needsUpdate = true;
    moteMat.color.setRGB(p.cy[0] * k * 0.7, p.cy[1] * k * 0.7, p.cy[2] * k * 0.7);
    nucleusMat.uniforms.uEdge.value.setRGB(p.ne[0] * k, p.ne[1] * k, p.ne[2] * k);
    cageMat.color.setRGB(p.ns[0] * k, p.ns[1] * k, p.ns[2] * k);
    const bg = new THREE.Color(0.012 + p.cy[0] * 0.028 * k, 0.014 + p.cy[1] * 0.028 * k, 0.03 + p.cy[2] * 0.028 * k);
    scene.background.copy(bg); scene.fog.color.copy(bg);
  }

  // ── drama: title cards, the shake, the vignette ────────────────────────────
  function titleCard(t1, t2) {
    const el = $('title');
    $('title-1').textContent = t1; $('title-2').textContent = t2 || '';
    el.classList.remove('show'); void el.offsetWidth; el.classList.add('show');
  }
  const shake = { amp: 0 };
  function kick(a) { if (!REDUCED) shake.amp = Math.max(shake.amp, a); }
  function vignette(kind) {
    const v = $('vignette'); v.classList.toggle('sting', kind === 'sting'); v.classList.add('beat');
    requestAnimationFrame(() => requestAnimationFrame(() => v.classList.remove('beat')));
  }
  function toast(text, kind) {
    const box = $('toasts'), t = document.createElement('div');
    t.className = 'toast' + (kind ? ' ' + kind : ''); t.textContent = text;
    box.appendChild(t);
    while (box.children.length > 3) box.firstChild.remove();
    const quiet = kind === 'quiet';
    setTimeout(() => t.classList.add('out'), quiet ? 2400 : 4600);
    setTimeout(() => t.remove(), quiet ? 3100 : 5300);
  }
  let hudRevision = 0, hudShownRevision = -1;
  function bumpHud() { hudRevision++; }

  // ── the model ──────────────────────────────────────────────────────────────
  function newHunt(seed) {
    matchSeed = seed;
    hunt = new S.Hunt(bank, { seed, ai: 2, aiSpeed: S.PILOT.aiSpeeds[ui.ai] });
    glyphs.clear(); clearHaloMeshes(true); shards.length = 0; outcomeShown = false;
    $('outcome').hidden = true; $('brief').hidden = ui.mode !== 'hunt';
    $('draw-note').textContent = `This match drew: ${hunt.forms.map(f => f.variant === f.name ? `${f.name}, ${f.look}` : f.variant).join(' · ')}.`;
    $('b-start').disabled = false;
    tintTo(BASE, 1.2, 0); bumpHud();
    setVerb(null);
  }
  /** The Forms tab's swarm: free to grow (fed), swimming a loop of the cell, or holding still for a pose or the dance. */
  function newPlayground() {
    const sw = new S.Swarm(bank, 11); sw.funded = false;
    pg = { sw, k: 0, v: [0, 0, 0, 0], pose: false, halo: null, haloAt: -1, step: 0, stepT: 0, clock: 0, angle: 0, cutAt: -1 };
    sw.seed(bank.ix[S.FORMS[0].variants[0].key], [440, 0, 0], [0, 0, -1]);
    renderForms();
  }
  const activeSwarm = () => ui.mode === 'hunt' ? hunt.swarm : pg.sw;
  const pgForm = () => S.FORMS[pg.k], pgVariant = () => S.FORMS[pg.k].variants[pg.v[pg.k]];
  function pgTake(k, v, quiet) {
    pg.k = k; pg.v[k] = v; pg.pose = false; pg.halo = null; pg.haloAt = -1; pgClearHalo();
    const key = pgVariant().key, sw = pg.sw;
    sw.setLevers(1, 1, false);
    if (sw.requestForm(bank.ix[key])) {
      burstFromBody(sw, 64, 70 * 2.2, 5); flashAt(sw.anchor, GOLD, 300, 0.6); audio.gong(k >= 2);
      if (!quiet) titleCard(pgVariant().name, pgVariant().look);
      bumpHud();
    }
    if (S.FORMS[k].role === 'dance') { pg.haloAt = pg.clock + 3; tintTo(ASC, 3, 1.1); }
    else if (tint.gold) tintTo(BASE, 4, 0);
    markForms();
  }
  /** The poses a form eats (and strikes) in: the serpents' three coils, then the strike pose. */
  const pgPoses = () => (pg.k < 2 ? S.COILS.slice() : []).concat(pgForm().role === 'dance' ? [] : ['feed']).concat(S.LUNGES[pg.k] || []);
  /** pose: a name from pgPoses, false (travel), or true (the next one along; past the last, back to travel). */
  function pgSetPose(pose) {
    const f = pgForm(), v = pgVariant(), sw = pg.sw, list = pgPoses();
    if (pose === true) { const at = list.indexOf(pg.pose); pose = at + 1 < list.length ? list[at + 1] : false; }
    if (f.role === 'dance' || (pose && !list.includes(pose))) pose = false;
    pg.pose = pose;
    const k = bank.ix[v.key + (pose ? '_' + pose : '')];
    if (k !== undefined) sw.requestPose(k);
    sw.setLevers(pose ? 0.5 : 1, 1, !!pose);
    $('b-pose').setAttribute('aria-pressed', pose ? 'true' : 'false');
    const POSE_LABEL = { feed: pg.k === 3 ? 'Meal pose ' : 'Strike pose ', gape: 'Lunge: jaws wide ', snap: 'Lunge: jaws snapped ',
      rear: 'Lunge: heads reared ', strike: 'Lunge: every head strikes ' };
    $('b-pose').firstChild.textContent = pose ? (POSE_LABEL[pose] || `Rolled up: ${pose === 'eight' ? 'figure-eight' : pose} `) : 'Meal pose ';
    if (pose === 'gape') toast('Lunging, it holds its jaws wide, six danger-plate teeth on their inner edges.', 'quiet');
    if (pose === 'snap') { toast('As its jaws reach a pilot they SNAP shut.', 'quiet'); audio.snap(); kick(4); }
    if (pose === 'rear') toast('Lunging, it rears every head back over its collar, their hoods turned to danger plates.', 'quiet');
    if (pose === 'strike') { toast('As it reaches a pilot, EVERY head strikes at once.', 'quiet'); audio.snap(); kick(4); }
    if (pose && S.COIL_NAMES[pose]) toast(`Rolled up in ${S.COIL_NAMES[pose]}: ${pg.k === 0 ? 'round the plant, so most of the body is in reach of the food' : 'under its raised heads, which ring the food'}.`, 'quiet');
    bumpHud();
  }
  function pgCut() {
    const sw = pg.sw, n = sw.cut(0.3, [1, 0, 0], 0);
    pg.cutAt = pg.clock; pg.cutLive = sw.liveCount() + 0; pg.cutN = n;
    toast(pg.pose ? `${n} cut from the tail. It is feeding, so it does not regrow until it leaves the plant.` : `${n} cut from the tail. Watch it grow back into the same form.`, 'quiet');
    audio.hit(); kick(6);
  }
  function pgClearHalo() { if (haloRef && ui.mode === 'forms') clearHaloMeshes(true); }
  // the tour: each form travels, sits in its feed pose, loses its tail and regrows; the dance lights its halo
  const TOUR = [
    { t: 6, a: () => pgTake(0, pg.v[0], false) }, { t: 5, a: () => pgSetPose('coil') }, { t: 5, a: () => pgSetPose('wrap') }, { t: 4, a: () => pgSetPose('eight') },
    { t: 4, a: () => pgCut() }, { t: 4, a: () => pgSetPose(false) }, { t: 4, a: () => pgCut() },
    { t: 7, a: () => pgTake(1, pg.v[1], false) }, { t: 5, a: () => pgSetPose('coil') }, { t: 5, a: () => pgSetPose('wrap') }, { t: 5, a: () => pgSetPose('eight') }, { t: 4, a: () => pgSetPose(false) },
    { t: 20, a: () => pgTake(2, pg.v[2], false) },
    { t: 7, a: () => pgTake(3, pg.v[3], false) }, { t: 7, a: () => pgSetPose('feed') }, { t: 3, a: () => { pgSetPose(false); for (let k = 0; k < 4; k++) pg.v[k] = (pg.v[k] + 1) % 3; } },
  ];
  function pgTick() {
    const sw = pg.sw;
    pg.clock += S.DT;
    if (ui.tour) {
      pg.stepT -= S.DT;
      if (pg.stepT <= 0) { const st = TOUR[pg.step % TOUR.length]; st.a(); pg.stepT = st.t; pg.step++; }
    }
    let goal;
    if (pg.pose || pgForm().role === 'dance') goal = sw.anchor.slice();
    else { pg.angle = Math.atan2(-sw.anchor[2], sw.anchor[0]) + 0.32; goal = [Math.cos(pg.angle) * 440, 120 + Math.sin(pg.clock * 0.05) * 90, -Math.sin(pg.angle) * 440]; }
    sw.step(goal);
    if (pgForm().role === 'dance' && pg.haloAt >= 0 && pg.clock >= pg.haloAt) {
      pg.halo = S.makeHalo(sw, sw.anchor, bank.ring[pgVariant().key], pg.clock); pg.haloAt = -1;
      buildHaloMeshes(pg.halo); toast('The halo lights: twelve rings, each held while an attendant pack is over its post.', 'quiet');
    }
    if (pg.halo && Math.round(pg.clock * 10) % 5 === 0) S.guardHalo(pg.halo, sw);
    sw.events.length = 0;
  }

  // ── the plants: each species' silhouette, its prism budget laid along it (ModePreviewPlantingModel's glyphs, in prisms)
  const LEAF = { Borromean: [[5.6, 4.67, 2.78], [6.2, 2.4, 0.6]], Coral: [[3.51, 3.51, 2], [2.6, 2.6, 0.5]], Lantern: [[5.4, 5.4, 1.5], [3, 3, 0.7]],
    Reed: [[3.24, 3.24, 1.6], [2, 2, 0.6]], Frond: [[4.05, 4.05, 1.4], [2.5, 2.5, 0.5]], Tendril: [[2.97, 7.29, 1.1], [1.8, 3.6, 0.5]] };
  function speciesPaths(sp, r) {
    const V3 = (x, y, z) => new THREE.Vector3(x, y, z), paths = [];
    if (sp === 'Borromean') {
      for (const [A, B] of [[V3(1, 0, 0), V3(0, 1, 0)], [V3(0, 1, 0), V3(0, 0, 1)], [V3(0, 0, 1), V3(1, 0, 0)]]) {
        const pts = []; for (let k = 0; k <= 24; k++) { const a = k / 24 * 2 * Math.PI; pts.push(A.clone().multiplyScalar(Math.cos(a) * 26).add(B.clone().multiplyScalar(Math.sin(a) * 16))); }
        paths.push(pts);
      }
    } else if (sp === 'Coral') {
      const branch = (p, d, L, depth) => {
        const q = p.clone().addScaledVector(d, L); paths.push([p, q]);
        if (!depth) return;
        for (let j = 0; j < 2; j++) branch(q, d.clone().add(V3(r() - 0.5, r() * 0.3, r() - 0.5).multiplyScalar(1.6)).normalize(), L * 0.72, depth - 1);
      };
      branch(V3(0, -18, 0), V3(0, 1, 0), 16, 3);
    } else if (sp === 'Lantern') {
      paths.push([V3(0, -20, 0), V3(0, 8, 0)]);
      for (let t = 0; t < 6; t++) { const pts = []; for (let k = 0; k <= 10; k++) { const b = Math.PI * k / 10, a = 2 * Math.PI * t / 6; pts.push(V3(Math.sin(b) * Math.cos(a) * 12, 18 - Math.cos(b) * 12, Math.sin(b) * Math.sin(a) * 12)); } paths.push(pts); }
    } else if (sp === 'Reed') {
      for (let k = 0; k < 7; k++) { const x = (r() - 0.5) * 10, z = (r() - 0.5) * 10; paths.push([V3(x, -20, z), V3(x + (r() - 0.5) * 12, 18 + r() * 14, z + (r() - 0.5) * 12)]); }
    } else if (sp === 'Frond') {
      for (let k = 0; k < 6; k++) { const a = Math.PI * (0.15 + 0.7 * k / 5), d = V3(Math.cos(a), Math.sin(a), (r() - 0.5) * 0.5).normalize(); const pts = []; for (let j = 0; j <= 8; j++) pts.push(V3(0, -14, 0).addScaledVector(d, 34 * j / 8).add(V3(0, -3 * (j / 8) ** 2 * 3, 0))); paths.push(pts); }
    } else {   // Tendril: one curling helix
      const pts = []; for (let k = 0; k <= 40; k++) { const t = k / 40; pts.push(V3(Math.cos(t * 9) * 14 * t, -20 + 44 * t, Math.sin(t * 9) * 14 * t)); } paths.push(pts);
    }
    return paths;
  }
  const glyphs = new Map();
  function glyphOf(pl) {
    let g = glyphs.get(pl.id);
    if (g) return g;
    const r = S.rngOf(Math.abs(pl.id) * 977 + 13), rot = new THREE.Quaternion().setFromEuler(new THREE.Euler(pl.spin * 0.4, pl.spin, pl.spin * 0.25));
    const up = new THREE.Vector3(...pl.at).normalize(), tilt = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), up.lengthSq() > 0.5 ? up.negate() : new THREE.Vector3(0, 1, 0));
    const paths = speciesPaths(pl.species || 'Borromean', r), X = new THREE.Vector3(1, 0, 0), leaf = (LEAF[pl.species] || LEAF.Borromean)[pl.elem === 2 ? 1 : 0];
    const segs = []; let total = 0;
    for (const pts of paths) for (let i = 0; i + 1 < pts.length; i++) { const L = pts[i].distanceTo(pts[i + 1]); segs.push({ a: pts[i], b: pts[i + 1], L, at: total }); total += L; }
    const n = pl.prisms || 60, base = new THREE.Vector3(...pl.at);
    g = [];
    for (let k = 0; k < n; k++) {
      const u = (k + 0.5) / n * total; let sg = segs[0];
      for (const x of segs) if (u >= x.at) sg = x;
      const t = sg.L > 0 ? (u - sg.at) / sg.L : 0, pos = sg.a.clone().lerp(sg.b, Math.min(1, t)), tan = sg.b.clone().sub(sg.a).normalize();
      pos.applyQuaternion(rot).applyQuaternion(tilt).add(base); tan.applyQuaternion(rot).applyQuaternion(tilt);
      g.push({ p: pos, q: new THREE.Quaternion().setFromUnitVectors(X, tan), s: new THREE.Vector3(...leaf) });
    }
    // the order prisms are eaten in, scattered through the plant
    for (let q = g.length - 1; q > 0; q--) { const j = (q * 7919 + Math.abs(pl.id) * 31) % (q + 1); [g[q], g[j]] = [g[j], g[q]]; }
    glyphs.set(pl.id, g);
    return g;
  }
  const M = new THREE.Matrix4(), Q = new THREE.Quaternion(), P = new THREE.Vector3(), SC = new THREE.Vector3(), D = new THREE.Vector3(), XA = new THREE.Vector3(1, 0, 0);
  function drawPlants(t) {
    let idx = 0;
    if (ui.mode === 'hunt') {
      const nowS = hunt.now;
      hunt.plants.forEach((pl, pi) => {
        const g = glyphOf(pl), slot = PLANT_SLOTS[pi];
        idx = slot.start;
        const died = !pl.alive ? Math.max(0, 1 - (nowS - (pl.diedAt || 0)) / 1.2) : 1;
        const vis = pl.alive ? Math.ceil(pl.store / pl.leaf - 1e-6) : (died > 0 ? S.FOOD.diesAt : 0);
        const bitten = pl.alive && nowS - pl.lastBite < 0.35, base = ui.colour === 'domain' ? [0.55, 0.62, 0.9] : EC[pl.elem];
        const grow = pl.born ? Math.min(1, (nowS - pl.born) / 1.5) : 1;
        for (let k = 0; k < slot.n; k++, idx++) {
          if (k >= vis) { plantMesh.setMatrixAt(idx, ZERO); continue; }
          const e = g[k]; SC.copy(e.s).multiplyScalar(died * grow); M.compose(e.p, e.q, SC); plantMesh.setMatrixAt(idx, M);
          const f = bitten ? 1.3 : 0.82; tmpC.setRGB(base[0] * f, base[1] * f, base[2] * f); plantMesh.setColorAt(idx, tmpC);
        }
      });
      idx = PLANT_SLOTS.total;
      // its next meal: a ring round the plant it is going for, gold while it eats there
      const d = hunt.director, tp = d.targetFood >= 0 ? hunt.plantById(d.targetFood) : null;
      marker.visible = !!tp && hunt.started && d.outcome === 'Running';
      if (tp) {
        marker.position.set(...tp.at); marker.lookAt(camera.position);
        const feeding = d.phase === 'Feed';
        markerMat.color.setRGB(...(feeding ? GOLD : JADE)); markerMat.opacity = feeding ? 0.55 : 0.28 + 0.14 * Math.sin(t * 4);
        marker.scale.setScalar(feeding ? 1 + 0.06 * Math.sin(t * 6) : 1);
      }
    } else if (pg.pose && !(S.LUNGES[pg.k] || []).includes(pg.pose)) {   // the Forms tab: a plant at a meal pose's mouth, for scale
      marker.visible = false;
      const sw = pg.sw, v = pgVariant(), mouth = bank.mouth[v.key + '_' + pg.pose];
      if (mouth) {
        const at = V.add(sw.anchor, sw.inBody(mouth)), g = glyphOf({ id: -1 - pg.k * 3 - pg.v[pg.k], elem: 1, species: 'Borromean', prisms: 60, at, spin: 1.3 });
        for (let k = 0; k < g.length; k++, idx++) {
          const e = g[k]; M.compose(e.p, e.q, e.s); plantMesh.setMatrixAt(idx, M);
          tmpC.setRGB(...EC[1].map(x => x * 0.8)); plantMesh.setColorAt(idx, tmpC);
        }
        glyphs.delete(-1 - pg.k * 3 - pg.v[pg.k]);   // it follows the mouth: rebuilt each frame
      }
    } else marker.visible = false;
    for (; idx < PLANT_CAP; idx++) plantMesh.setMatrixAt(idx, ZERO);
    plantMesh.instanceMatrix.needsUpdate = true; if (plantMesh.instanceColor) plantMesh.instanceColor.needsUpdate = true;
  }

  // ── the creature ───────────────────────────────────────────────────────────
  const halfCache = new Float32Array(MAX_MEMBERS * 3).fill(0.6), w3 = [0, 0, 0];
  function drawMembers(sw, alpha, t) {
    const plan = sw.plan, fr = sw.frame(sw.clock - 1 + alpha), FA = plan.Fc[fr.a], FB = plan.Fc[fr.b], ft = fr.t;
    const L = (a, b) => [a[0] + (b[0] - a[0]) * alpha, a[1] + (b[1] - a[1]) * alpha, a[2] + (b[2] - a[2]) * alpha];
    const X = L(sw.BX0, sw.BX), Y = L(sw.BY0, sw.BY), Z = L(sw.BZ0, sw.BZ);
    const n = Math.min(sw.cap, MAX_MEMBERS), pulse = 0.68 + 0.32 * Math.sin(t * 10), one = ui.colour === 'domain';
    const A0 = sw.anchor0, A1 = sw.anchor, ax = A0[0] + (A1[0] - A0[0]) * alpha, ay = A0[1] + (A1[1] - A0[1]) * alpha, az = A0[2] + (A1[2] - A0[2]) * alpha, U = S.UNIT;
    const p0 = sw.p0, p1 = sw.p;
    for (let i = 0; i < n; i++) {
      if (!sw.alive[i]) { members.setMatrixAt(i, ZERO); continue; }
      const j = 3 * i, lx = p0[j] + (p1[j] - p0[j]) * alpha, ly = p0[j + 1] + (p1[j + 1] - p0[j + 1]) * alpha, lz = p0[j + 2] + (p1[j + 2] - p0[j + 2]) * alpha;
      P.set(ax + (X[0] * lx + Y[0] * ly + Z[0] * lz) * U, ay + (X[1] * lx + Y[1] * ly + Z[1] * lz) * U, az + (X[2] * lx + Y[2] * ly + Z[2] * lz) * U);
      const s = sw.slot[i];
      let fx, fy, fz;
      if (s >= 0) {
        fx = FA[3 * s] + (FB[3 * s] - FA[3 * s]) * ft; fy = FA[3 * s + 1] + (FB[3 * s + 1] - FA[3 * s + 1]) * ft; fz = FA[3 * s + 2] + (FB[3 * s + 2] - FA[3 * s + 2]) * ft;
        for (let a = 0; a < 3; a++) halfCache[3 * i + a] += (plan.half[3 * s + a] - halfCache[3 * i + a]) * 0.25;
      } else { fx = sw.v[3 * i] || 1; fy = sw.v[3 * i + 1]; fz = sw.v[3 * i + 2]; }
      D.set(X[0] * fx + Y[0] * fy + Z[0] * fz, X[1] * fx + Y[1] * fy + Z[1] * fz, X[2] * fx + Y[2] * fy + Z[2] * fz);
      if (D.lengthSq() < 1e-8) D.set(1, 0, 0); else D.normalize();
      Q.setFromUnitVectors(XA, D);
      const age = sw.age[i] + alpha, bl = age < S.CORE.bloomSteps ? Math.min(1, age / S.CORE.bloomSteps) : 1;
      const grow = bl * bl * (3 - 2 * bl), die = sw.dying[i] > 0 ? Math.max(0, (sw.dying[i] - alpha) / Math.max(1, sw.dieLen[i])) : 1;
      const danger = sw.isDanger(i), shield = sw.isShield(i);
      const k = 2 * S.UNIT * grow * die * (danger ? 1.35 : 1);
      SC.set(Math.max(0.5, halfCache[3 * i] * k), Math.max(0.5, halfCache[3 * i + 1] * k), Math.max(0.5, halfCache[3 * i + 2] * k));
      M.compose(P, Q, SC); members.setMatrixAt(i, M);
      let r, g, b;
      if (danger) { const c = one ? DANGER_ONE : DANGER; r = c[0] * pulse; g = c[1] * pulse; b = c[2] * pulse; }
      else if (one) { const c = shield ? ONE_HI : ONE; r = c[0]; g = c[1]; b = c[2]; }
      else {
        const A = EC[sw.elem[i]], B = EC[sw.elemTo[i]], m = sw.molt[i];
        r = A[0] + (B[0] - A[0]) * m; g = A[1] + (B[1] - A[1]) * m; b = A[2] + (B[2] - A[2]) * m;
        if (shield) { r = r * 0.45 + SHIELD[0] * 0.55; g = g * 0.45 + SHIELD[1] * 0.55; b = b * 0.45 + SHIELD[2] * 0.55; }
      }
      const f = Math.min(1, sw.flash[i]);
      tmpC.setRGB(r + (1.6 - r) * f, g + (1.45 - g) * f, b + (1.3 - b) * f);
      members.setColorAt(i, tmpC);
    }
    for (let i = n; i < MAX_MEMBERS; i++) members.setMatrixAt(i, ZERO);
    members.instanceMatrix.needsUpdate = true; members.instanceColor.needsUpdate = true;
  }
  function drawHalo(dt, t) {
    for (let q = haloMeshes.length - 1; q >= 0; q--) {
      const m = haloMeshes[q], u = m.userData, r = u.r;
      if (u.wither >= 0 || r.out) {
        u.wither = (u.wither < 0 ? 0 : u.wither) + dt / 0.6;
        m.scale.setScalar(Math.max(0.001, (u.drawn || 1) * (1 - u.wither)));
        m.material.opacity = Math.max(0, 0.95 * (1 - u.wither));
        if (u.wither >= 1) { scene.remove(m); m.material.dispose(); haloMeshes.splice(q, 1); }
        continue;
      }
      const target = r.guarded ? 0.35 : 1, bloom = Math.min(1, (t - (u.born || (u.born = t))) / 1.2);
      u.drawn = (u.drawn || 0) + (target - (u.drawn || 0)) * Math.min(1, dt * 6);
      m.scale.setScalar(Math.max(0.001, u.drawn * bloom));
      m.material.color.setRGB(...(r.guarded ? [0.75, 0.62, 1.05] : PEARL));
      m.material.opacity = r.guarded ? 0.55 : 0.9 + 0.1 * Math.sin(t * 5 + r.k);
    }
  }
  function drawDarts(alpha, t) {
    const on = ui.mode === 'hunt';
    darts.forEach((d, k) => {
      const p = on ? hunt.pilots[k] : null, show = !!p && (k === 0 || hunt.aiSpeed > 0);
      d.mesh.visible = d.trail.visible = d.halo.visible = show;
      if (!show) return;
      const a = p.at0 || p.at, x = a[0] + (p.at[0] - a[0]) * alpha, y = a[1] + (p.at[1] - a[1]) * alpha, z = a[2] + (p.at[2] - a[2]) * alpha;
      d.mesh.position.set(x, y, z); d.halo.position.set(x, y, z);
      D.set(...p.dir); if (D.lengthSq() > 0.5) d.mesh.quaternion.setFromUnitVectors(XA, D.normalize());
      const stunned = hunt.now < p.stunUntil, base = k ? WING : JADE;
      const c = stunned && Math.sin(t * 30) > 0 ? DANGER : base;
      d.mat.color.setRGB(...c); d.halo.material.color.setRGB(...c);
      const arr = d.trail.geometry.attributes.position.array, tr = p.trail, nT = Math.min(40, tr.length);
      for (let i = 0; i < nT; i++) { const q = tr[tr.length - nT + i]; arr[3 * i] = q[0]; arr[3 * i + 1] = q[1]; arr[3 * i + 2] = q[2]; }
      d.trail.geometry.setDrawRange(0, nT); d.trail.geometry.attributes.position.needsUpdate = true;
    });
  }
  function drawFx(dt) {
    for (const s of flashes) {
      if (!s.visible) continue;
      s.userData.t += dt / s.userData.dur;
      if (s.userData.t >= 1) { s.visible = false; continue; }
      const u = s.userData.t; s.scale.setScalar(s.userData.size * (0.4 + 0.6 * Math.sqrt(u))); s.material.opacity = 0.8 * (1 - u) * (1 - u);
    }
    for (const m of shocks) {
      if (!m.visible) continue;
      m.userData.t += dt / m.userData.dur;
      if (m.userData.t >= 1) { m.visible = false; continue; }
      const u = m.userData.t; m.scale.setScalar(4 + u * m.userData.size); m.material.opacity = (1 - u) * (1 - u); m.lookAt(camera.position);
    }
  }

  // ── camera ─────────────────────────────────────────────────────────────────
  let camTween = null, cinematicUntil = 0;
  function followPoint(alpha) {
    if (ui.cam === 'dart' && ui.mode === 'hunt') { const p = hunt.you, a = p.at0 || p.at; return new THREE.Vector3(a[0] + (p.at[0] - a[0]) * alpha, a[1] + (p.at[1] - a[1]) * alpha, a[2] + (p.at[2] - a[2]) * alpha); }
    const sw = activeSwarm(); sw.toWorld(0, 0, 0, w3, alpha); return new THREE.Vector3(w3[0], w3[1], w3[2]);
  }
  function snapCamera(dur) {
    const tgt = ui.cam === 'wide' ? new THREE.Vector3(0, 0, 0) : followPoint(1);
    let off;
    if (ui.cam === 'wide') off = new THREE.Vector3(-800, 900, 1500);
    else if (ui.cam === 'dart') off = new THREE.Vector3(140, 90, 160);
    else { const sw = activeSwarm(), r = Math.max(60, sw.plan.radius * S.UNIT); off = new THREE.Vector3(-1.2, 1.0, 2.6).multiplyScalar(r * 1.7); }
    camTween = { t: 0, dur: dur || 1.1, fromP: camera.position.clone(), fromT: controls.target.clone(), toT: tgt, toP: tgt.clone().add(off) };
  }
  /** The rise: stand in front of the figure and a little below it, looking up into its halo. */
  function cinematic() {
    const sw = activeSwarm(), c = sw.toWorld(0, 6, 0, [0, 0, 0]), p = sw.toWorld(230, -40, 70, [0, 0, 0]);
    camTween = { t: 0, dur: 2.6, fromP: camera.position.clone(), fromT: controls.target.clone(), toT: new THREE.Vector3(...c), toP: new THREE.Vector3(...p) };
    cinematicUntil = now() + 3;
  }
  function updateCamera(dt, alpha) {
    if (camTween) {
      camTween.t = Math.min(1, camTween.t + dt / camTween.dur);
      const e = camTween.t * camTween.t * (3 - 2 * camTween.t);
      if (ui.cam !== 'wide' && now() > cinematicUntil) { const f = followPoint(alpha), d = f.clone().sub(camTween.toT); camTween.toP.add(d); camTween.toT.copy(f); }
      camera.position.lerpVectors(camTween.fromP, camTween.toP, e); controls.target.lerpVectors(camTween.fromT, camTween.toT, e);
      if (camTween.t >= 1) camTween = null;
    } else if (ui.cam !== 'wide') {
      const f = followPoint(alpha), k = 1 - Math.exp(-dt * 3.2), d = f.sub(controls.target).multiplyScalar(k);
      controls.target.add(d); camera.position.add(d);
    }
    controls.update();
  }

  // ── your dart: click to fly it there; the verbs ────────────────────────────
  const ray = new THREE.Raycaster(), ndc = new THREE.Vector2();
  let down = null;
  canvas.addEventListener('pointerdown', e => { down = { x: e.clientX, y: e.clientY, t: performance.now() }; });
  canvas.addEventListener('pointerup', e => {
    if (!down) return;
    const moved = Math.hypot(e.clientX - down.x, e.clientY - down.y), quick = performance.now() - down.t < 450;
    down = null;
    if (moved > 6 || !quick) return;
    const r = canvas.getBoundingClientRect();
    ndc.set((e.clientX - r.left) / r.width * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
    ray.setFromCamera(ndc, camera);
    if (ui.mode === 'forms') { formsClick(); return; }
    if (!hunt.started) { startHunt(); return; }
    if (hunt.director.outcome !== 'Running') return;
    if (haloMeshes.length) {
      const hit = ray.intersectObjects(haloMeshes)[0];
      if (hit) { const k = hit.object.userData.r.k; hunt.command({ kind: 'thread', ring: k }); setVerb('v-thread'); shockAt(hit.point.toArray(), PEARL, 40, 0.35); return; }
    }
    let hit = ray.intersectObject(members)[0];
    if (hit) { hunt.command({ kind: 'strike', at: hit.point.toArray() }); setVerb(null); shockAt(hit.point.toArray(), JADE, 30, 0.35); return; }
    hit = ray.intersectObject(plantMesh)[0];
    if (hit && hit.instanceId !== undefined) {
      const target = hunt.plants[PLANT_SLOTS.findIndex(sl => hit.instanceId >= sl.start && hit.instanceId < sl.start + sl.n)];
      if (target && target.alive) { hunt.command({ kind: 'camp', plant: target.id }); setVerb('v-deny'); shockAt(target.at, JADE, 70, 0.4); toast('Camping that plant: a pilot near a plant spoils it for the creature.', 'quiet'); return; }
    }
    // anywhere else: fly there, at the creature's depth
    const depth = camera.position.distanceTo(new THREE.Vector3(...hunt.swarm.anchor));
    const at = ray.ray.at(Math.min(depth, 2600), new THREE.Vector3()).toArray();
    const l = V.len(at); if (l > R - 60) for (let a = 0; a < 3; a++) at[a] *= (R - 60) / l;
    hunt.command({ kind: 'goto', at }); setVerb(null); shockAt(at, JADE, 26, 0.3);
  });
  function formsClick() {
    if (pg.halo) {
      const hit = ray.intersectObjects(haloMeshes)[0];
      if (hit) {
        const r = hit.object.userData.r;
        if (r.guarded) { audio.deny(); toast('Held: an attendant pack is over that ring.', 'quiet'); return; }
        r.out = true; burst(r.pos, 18, 160, 4.5, 1.1); flashAt(r.pos, GOLD, 120, 0.5); audio.chime();
        if (pg.halo.rings.every(x => x.out)) setTimeout(() => { if (pg.halo) { pg.halo = S.makeHalo(pg.sw, pg.sw.anchor, bank.ring[pgVariant().key], pg.clock); buildHaloMeshes(pg.halo); } }, 1500);
        return;
      }
    }
    const hit = ray.intersectObject(members)[0];
    if (hit) { const k = killNear(pg.sw, hit.point, 14); if (k) { burst(hit.point.toArray(), Math.min(12, k), 120, 3, 0.7, [EC[1], EC[0]]); audio.hit(); } }
  }
  function killNear(sw, p, r) {
    let k = 0; for (let i = 0; i < sw.cap; i++) { if (!sw.isLive(i)) continue; sw.memberWorld(i, w3); if (Math.hypot(w3[0] - p.x, w3[1] - p.y, w3[2] - p.z) <= r && sw.kill(i, 0)) k++; }
    return k;
  }
  const VERBS = ['v-strike', 'v-deny', 'v-thread', 'v-shadow', 'v-meals', 'v-hold'];
  function setVerb(id) { VERBS.forEach(v => $(v).setAttribute('aria-pressed', v === id ? 'true' : 'false')); }
  function verb(id) {
    if (ui.mode !== 'hunt') setMode('hunt');
    if (!hunt.started) startHunt();
    if (hunt.director.outcome !== 'Running') return;
    const d = hunt.director;
    if (id === 'v-strike') { hunt.command({ kind: 'body' }); setVerb(null); }
    else if (id === 'v-deny') {
      const pl = d.targetFood >= 0 ? hunt.plantById(d.targetFood) : null;
      if (!pl) { toast('It is not heading for a plant right now.', 'quiet'); return; }
      hunt.command({ kind: 'camp', plant: pl.id }); setVerb(id);
    } else if (id === 'v-thread') {
      const r = hunt.nearestOpenRing(hunt.you.at);
      if (!r) { toast(hunt.halo ? 'Every open ring is held or broken.' : 'There is no halo yet. It lights when the Lord of the Dance has risen.', 'quiet'); return; }
      hunt.command({ kind: 'thread', ring: r.k }); setVerb(id);
    } else if (id === 'v-shadow') { hunt.command({ kind: 'shadow' }); setVerb(id); }
    else if (id === 'v-meals') { hunt.command({ kind: 'meals' }); setVerb(id); }
    else { hunt.command({ kind: 'hold' }); setVerb(id); }
  }
  VERBS.forEach(id => $(id).addEventListener('click', () => verb(id)));
  const KEYMAP = { s: 'v-strike', d: 'v-deny', h: 'v-thread', f: 'v-shadow', m: 'v-meals', x: 'v-hold' };

  // ── the panel ──────────────────────────────────────────────────────────────
  function renderForms() {
    const list = $('form-list'); list.textContent = '';
    S.FORMS.forEach((f, k) => {
      const card = document.createElement('div'); card.className = 'form'; card.id = 'form-' + k;
      const plan0 = bank.plans[bank.ix[f.variants[0].key]], tot = plan0.n, mix = plan0.mix;
      const what = f.role === 'eater' ? `bank ${Math.round(f.bankShare * 100)}% of the stomach · a meal ${fmt(f.meal)}`
        : f.role === 'dance' ? `does not eat · ${S.HALO.count} rings, break ${S.HALO.toBreak} in ${S.SETTINGS.drumSeconds} s`
        : `its bank is the last feast: ${Math.round(f.bankShare * 100)}% of the stomach`;
      card.innerHTML = `<div class="top"><span class="fname">${f.name}</span><span class="fnum">Form ${k + 1}</span></div>
        <div class="mix">${mix.map((m, e) => `<i style="width:${100 * m / tot}%;background:${ECSS[e]}" title="${ENAME[e]} ${m}"></i>`).join('')}</div>
        <div class="chips">${f.variants.map((v, j) => `<button class="chip" type="button" id="chip-${k}-${j}" aria-pressed="false">${v.name === f.name ? v.look : v.name}</button>`).join('')}</div>
        <div class="fstat">${f.variants.map(v => fmt(bank.plans[bank.ix[v.key]].n)).join(' / ')} tadpoles · ${what}</div>`;
      list.appendChild(card);
      f.variants.forEach((v, j) => $(`chip-${k}-${j}`).addEventListener('click', () => { stopTour(); if (ui.mode !== 'forms') setMode('forms'); pgTake(k, j, false); }));
    });
    markForms();
  }
  function markForms() {
    if (!pg) return;
    S.FORMS.forEach((f, k) => {
      const card = $('form-' + k); if (!card) return;
      card.dataset.on = k === pg.k ? 'true' : 'false';
      f.variants.forEach((v, j) => { const c = $(`chip-${k}-${j}`); if (c) c.setAttribute('aria-pressed', k === pg.k && j === pg.v[k] ? 'true' : 'false'); });
    });
    $('b-pose').disabled = S.FORMS[pg.k].role === 'dance';
  }
  function stopTour() { ui.tour = false; $('c-tour').checked = false; }
  function seg(id, key, onChange) {
    const el = $(id);
    el.addEventListener('click', e => {
      const b = e.target.closest('button'); if (!b) return;
      el.querySelectorAll('button').forEach(x => x.setAttribute('aria-pressed', x === b ? 'true' : 'false'));
      const v = isNaN(+b.dataset.v) ? b.dataset.v : +b.dataset.v;
      ui[key] = v; onChange && onChange(v);
    });
  }
  function syncSeg(id, v) { $(id).querySelectorAll('button').forEach(x => x.setAttribute('aria-pressed', String(x.dataset.v) === String(v) ? 'true' : 'false')); }

  function setMode(m) {
    ui.mode = m;
    $('tab-hunt').setAttribute('aria-selected', m === 'hunt' ? 'true' : 'false');
    $('tab-forms').setAttribute('aria-selected', m === 'forms' ? 'true' : 'false');
    $('pane-hunt').hidden = m !== 'hunt'; $('pane-forms').hidden = m !== 'forms';
    $('outcome').hidden = !(m === 'hunt' && outcomeShown && hunt.director.outcome !== 'Running' && !outcomeDismissed);
    $('brief').hidden = !(m === 'hunt' && !hunt.started);
    clearHaloMeshes(true);
    if (m === 'hunt' && hunt.halo) buildHaloMeshes(hunt.halo);
    if (m === 'forms' && pg.halo) buildHaloMeshes(pg.halo);
    const gold = m === 'hunt' ? ['Rising', 'Dance'].includes(hunt.director.phase) : S.FORMS[pg.k].role === 'dance';
    tintTo(gold ? ASC : BASE, 1.0, 0);
    if (ui.cam === 'dart' && m === 'forms') { ui.cam = 'creature'; syncSeg('seg-cam', 'creature'); }
    $('hint').textContent = m === 'hunt' ? 'Drag to orbit · click the creature to strike · a plant to camp it · a ring to thread it' : 'Drag to orbit · click the creature to cut it · click a halo ring to break it';
    snapCamera(); bumpHud();
  }
  function startHunt() {
    if (hunt.started) return;
    hunt.start(); $('brief').hidden = true; $('b-start').disabled = true;
    titleCard('Tandava', 'Hunt it before it takes its last form.');
    toast(LINES.start);
    const h = hunt; setTimeout(() => { if (hunt === h) toast(h.forms[0].line.replace('{0}', h.forms[0].variant), 'gold'); }, 2600);
    bumpHud();
  }
  let outcomeShown = false, outcomeDismissed = false;
  $('tab-hunt').addEventListener('click', () => setMode('hunt'));
  $('tab-forms').addEventListener('click', () => setMode('forms'));
  $('b-start').addEventListener('click', () => { if (ui.mode !== 'hunt') setMode('hunt'); startHunt(); });
  $('brief-go').addEventListener('click', startHunt);
  $('b-reset').addEventListener('click', () => { newHunt(matchSeed); snapCamera(); });
  $('b-draw').addEventListener('click', () => { newHunt(1 + Math.floor(Math.random() * 9000)); snapCamera(); toast('A new draw: ' + hunt.forms.map(f => f.variant).join(', '), 'quiet'); });
  $('out-again').addEventListener('click', () => { newHunt(1 + Math.floor(Math.random() * 9000)); snapCamera(); });
  $('out-close').addEventListener('click', () => { $('outcome').hidden = true; outcomeDismissed = true; });
  $('b-pose').addEventListener('click', () => { stopTour(); pgSetPose(true); });
  $('b-cut').addEventListener('click', () => { stopTour(); pgCut(); });
  $('b-rehatch').addEventListener('click', () => { stopTour(); const k = pg.k; pg.sw.seed(bank.ix[pgVariant().key], pg.sw.anchor.slice(), pg.sw.BX.slice()); pgSetPose(false); if (S.FORMS[k].role === 'dance') pg.haloAt = pg.clock + 2; bumpHud(); });
  $('c-tour').addEventListener('change', e => { ui.tour = e.target.checked; pg.stepT = 0; });
  $('c-sound').addEventListener('change', e => { ui.sound = e.target.checked; if (ui.sound) audio.init(); });
  $('b-pause').addEventListener('click', togglePause);
  function togglePause() { ui.paused = !ui.paused; $('b-pause').firstChild.textContent = ui.paused ? 'Resume ' : 'Pause '; }
  seg('seg-speed', 'speed');
  seg('seg-cam', 'cam', () => snapCamera());
  seg('seg-colour', 'colour');
  seg('seg-ai', 'ai', v => { hunt.aiSpeed = S.PILOT.aiSpeeds[v]; toast(v === 'off' ? 'The wing stands down.' : `Two wingmates join at ${hunt.aiSpeed} u/s, flying the game's chase: at its body, behind it while it eats, at the halo in the dance.`, 'quiet'); });
  window.addEventListener('keydown', e => {
    if (e.target && /INPUT|TEXTAREA|SELECT/.test(e.target.tagName)) return;
    const k = e.key.toLowerCase();
    if (e.key === ' ') { e.preventDefault(); togglePause(); return; }
    if (ui.mode === 'hunt' && KEYMAP[k]) { verb(KEYMAP[k]); return; }
    if (ui.mode === 'forms') {
      if (k === 'p') { stopTour(); pgSetPose(true); }
      else if (k === 'c') { stopTour(); pgCut(); }
      else if (k >= '1' && k <= '4') { stopTour(); pgTake(+k - 1, pg.v[+k - 1], false); }
    }
  });

  // ── the HUD: the goal rows, the bench's numbers ─────────────────────────────
  function goal(ix, label, value, bar, cls, barColour) {
    const row = $('g' + ix);
    $('g' + ix + '-l').textContent = label; $('g' + ix + '-v').textContent = value;
    row.className = 'goal' + (ix === 1 ? ' primary' : '') + (cls ? ' ' + cls : '');
    const b = $('g' + ix + '-b');
    if (b) { b.style.width = (100 * Math.max(0, Math.min(1, bar || 0))) + '%'; b.style.background = barColour || 'var(--jade)'; }
  }
  function updateHud() {
    if (hudShownRevision !== hudRevision) {
      hudShownRevision = hudRevision;
      const g1 = $('g1'); g1.classList.remove('flare'); void g1.offsetWidth; g1.classList.add('flare');
    }
    if (ui.mode === 'forms') {
      const sw = pg.sw, f = pgForm(), v = pgVariant(), live = sw.liveCount(), n = sw.plan.n;
      $('g-eyebrow').textContent = `${f.name} · variant ${pg.v[pg.k] + 1} of 3`;
      goal(1, v.name, `Form ${pg.k + 1} of 4`, (pg.k + 1) / 4, f.role === 'dance' ? 'gold' : '', 'var(--gold)');
      const doing = f.role === 'dance' ? (pg.halo ? 'Dancing - the halo is lit' : 'Rising') : pg.pose ? ({ feed: pg.k === 3 ? 'Clasping its meal - not healing' : 'Strike pose - not healing', gape: 'Lunging - jaws wide', snap: 'Lunging - jaws snapped shut', rear: 'Lunging - heads reared', strike: 'Lunging - every head strikes' }[pg.pose] || 'Rolled up to eat - not healing') : live < n ? 'Travelling - growing back' : 'Travelling';
      goal(2, doing, `Body ${pct(live / n)}`, live / n, pg.pose ? 'alert' : f.role === 'dance' ? 'gold' : '');
      goal(3, cap(v.look), `${fmt(n)} tadpoles`); $('g3').hidden = false;
      return;
    }
    const h = hunt, d = h.director, st = h.state(), f = d.form, body = d.bodyFraction(st);
    $('g-eyebrow').textContent = `Goals · form ${d.formIx + 1} of 4`;
    if (!h.started) {
      goal(1, f.variant, '0%', 0); goal(2, 'Waiting at the hatch', `Body ${pct(body)}`, body); $('g3').hidden = true;
    } else if (d.phase === 'Dance') {
      goal(1, 'Halo rings broken', `${d.haloOut}/${S.HALO.toBreak}`, d.haloOut / S.HALO.toBreak, 'gold', 'var(--gold)');
      goal(2, LABEL.Dance, `Body ${pct(body)}`, body, 'gold');
      goal(3, 'Drum', mmss(d.drumRemaining)); $('g3').hidden = false;
    } else if (d.phase === 'Rising') {
      goal(1, f.variant, 'rising', 1 - d.riseRemaining / S.SETTINGS.riseSeconds, 'gold', 'var(--gold)');
      goal(2, LABEL.Rising, `Body ${pct(body)}`, body, 'gold'); $('g3').hidden = true;
    } else {
      const prog = d.progress(st), name = f.role === 'final' ? `${f.variant} - the last feast` : f.variant;
      goal(1, name, pct(prog), prog, d.outcome === 'Completed' ? 'alert' : '');
      const lab = d.outcome !== 'Running' ? OUT[d.outcome][1]
        : d.phase === 'Feed' ? (d.coilName ? `Feeding in ${S.COIL_NAMES[d.coilName]} - strike the body` : LABEL.Feed) : LABEL[d.mood];
      goal(2, lab, `Body ${pct(body)}`, body, d.phase === 'Feed' ? 'gold' : d.mood === 'Fleeing' || d.mood === 'Lunging' ? 'alert' : '',
           body < S.SETTINGS.lungeBodyMin ? 'var(--danger)' : 'var(--jade)');
      $('g3').hidden = true;
    }
    // the bench
    $('h-clock').textContent = `${h.now.toFixed(1)} s`;
    $('h-doing').textContent = !h.started ? 'Waiting at the hatch' : d.outcome !== 'Running' ? OUT[d.outcome][1]
      : d.phase === 'Feed' ? `Eating · ${fmt(d.eatenHere)} of ${fmt(f.meal)} · lost ${fmt(d.lostHere || 0)} of ${fmt(S.SETTINGS.mealBreakFraction * f.planCount)} breaks it`
      : d.phase === 'Rising' ? `Rising · halo in ${d.riseRemaining.toFixed(0)} s` : d.phase === 'Dance' ? `Dancing · drum ${mmss(d.drumRemaining)}`
      : d.mood === 'Lunging' ? 'Charging the nearest pilot, guards out' : d.mood === 'Fleeing' ? 'Hurt: bolting away from you'
      : d.targetFood >= 0 ? `Swimming to a plant` : 'Wandering: nothing worth eating';
    $('h-mood').textContent = `${d.mood} · ${Math.round(V.len([h.swarm.anchor[0] - h.swarm.anchor0[0], h.swarm.anchor[1] - h.swarm.anchor0[1], h.swarm.anchor[2] - h.swarm.anchor0[2]]) * 10)} u/s`;
    $('h-commits').textContent = h.commits.length ? h.commits.map(c => `${h.forms[c.form].variant} ${c.t.toFixed(0)} s`).join(' · ') : '-';
    $('h-meals').textContent = `${h.meals}${h.mealsBroken ? ` · ${h.mealsBroken} broken` : ''}`;
    $('h-you').textContent = fmt(h.you.kills);
    $('h-ai').textContent = hunt.aiSpeed > 0 ? fmt(h.pilots.slice(1).reduce((a, p) => a + p.kills, 0)) : 'off';
    $('h-stings').textContent = fmt(h.pilots.reduce((a, p) => a + p.stings, 0));
    $('h-rings').textContent = h.halo ? `${d.haloOut} broken · ${h.halo.rings.filter(r => r.guarded).length} held now` : d.formIx >= 2 && d.phase !== 'Rising' ? `${d.haloOut} broken` : '-';
    $('h-threat').textContent = d.threat.toFixed(2);
    $('h-threat-b').style.width = (100 * d.threat) + '%';
    $('h-threat-b').style.background = d.threat >= 0.65 ? 'var(--danger)' : d.threat >= 0.3 ? 'var(--gold)' : 'var(--jade)';
    const sto = st.stomach[0] + st.stomach[1] + st.stomach[2] + st.stomach[3], bankV = f.bank, scaleV = Math.max(S.STOMACH, 1);
    $('h-stomach').textContent = bankV > 0 ? `${fmt(sto)} / ${fmt(bankV)} to ${f.role === 'final' ? 'finish' : 'change'}` : `${fmt(sto)} · the drum decides`;
    $('h-stomach-b').style.width = (100 * Math.min(1, sto / scaleV)) + '%';
    $('h-bank').style.left = bankV > 0 ? (100 * bankV / scaleV) + '%' : '-10px';
  }
  function showOutcome(o) {
    const h = hunt, d = h.director, [verdict, label] = OUT[o], win = !!S.PILOTS_WIN[o];
    $('out-verdict').textContent = verdict.toUpperCase(); $('out-verdict').className = 'verdict ' + (win ? 'win' : 'loss');
    $('out-label').textContent = label;
    const wing = h.pilots.slice(1).reduce((a, p) => a + p.kills, 0), rings = h.pilots.reduce((a, p) => a + p.rings, 0);
    const rows = [['Time', `${h.now.toFixed(0)} s`], ['It reached', d.form.variant], ['Meals', `${h.meals}${h.mealsBroken ? ` (${h.mealsBroken} broken)` : ''}`],
      ['You cut', fmt(h.you.kills)], ['Halo rings', `${rings}`], ['Your score', fmt(h.score(h.you))]];
    if (h.aiSpeed > 0) rows.splice(4, 0, ['The wing cut', fmt(wing)]);
    $('out-stats').innerHTML = rows.map(([k, v]) => `<dt>${k}</dt><dd>${v}</dd>`).join('');
    $('outcome').hidden = false; outcomeShown = true; outcomeDismissed = false;
  }

  // ── events from the model ──────────────────────────────────────────────────
  let feedToasts = 0, lungeToasts = 0, snapToasts = 0, lastHitSound = 0;
  function drainHunt() {
    const h = hunt, sw = h.swarm;
    for (const e of h.events) {
      if (e.kind === 'form') {
        const f = h.forms[e.to];
        burstFromBody(sw, 64, 70 * 2.2, 5); flashAt(sw.anchor, GOLD, 300, 0.6); kick(f.role === 'dance' ? 18 : 10); audio.gong(e.to >= 2);
        if (f.role !== 'dance') { titleCard(f.variant, f.variant === f.name ? cap(f.look) : `${f.name} · ${f.look}`); toast(f.line.replace('{0}', f.variant), 'gold'); }
        if (f.role === 'final') tintTo(BASE, 5, 0.6);   // the dance is over: the cell eases back to its own colours
        bumpHud();
      } else if (e.kind === 'rising') {
        tintTo(ASC, 3, 1.2); titleCard('The Lord of the Dance', 'It rises where it stands. The halo lights in 12 s.'); toast(LINES.rising, 'gold');
        if (ui.speed > 1) { ui.speed = 1; syncSeg('seg-speed', 1); toast('Time slows for the dance.', 'quiet'); }
        if (ui.cam === 'creature') cinematic();
        bumpHud();
      } else if (e.kind === 'danceBegan') {
        if (h.halo) buildHaloMeshes(h.halo);
        toast(LINES.haloLit, 'gold'); audio.gong(true); bumpHud();
      } else if (e.kind === 'haloBroken') {
        const r = h.halo && h.halo.rings[e.ring];
        if (r) { burst(r.pos, 18, 70 * 2.4, 4.5, 1.2); flashAt(r.pos, GOLD, 160, 0.6); shockAt(r.pos, GOLD, 90, 0.5); }
        audio.chime(); bumpHud();
        if (e.count === 1) toast(LINES.firstHalo(S.HALO.toBreak), 'gold');
        else if (e.count === S.HALO.toBreak - 1) toast(LINES.lastHalo(e.count, S.HALO.toBreak), 'gold');
      } else if (e.kind === 'ringHeld') {
        if (e.pilot === 0) { audio.deny(); toast('Held: an attendant pack is over that ring. Try another.', 'quiet'); }
      } else if (e.kind === 'haloStruck') clearHaloMeshes(false);
      else if (e.kind === 'feedBegan') {
        const how = hunt.director.coilName;
        if (feedToasts++ < 4) toast(how ? LINES.feeding.replace('{0}', S.COIL_NAMES[how]) : LINES.feedingPlain, 'gold');
        bumpHud();
      }
      else if (e.kind === 'feedEnded') {
        if (e.why === 'Broken') { toast(LINES.mealBroken, 'gold'); titleCard('Meal broken', 'It bolts, hungry. Cuts made while it ate stay cut until it eats again.'); }
        else if (e.why === 'Bare') toast('It gives up on a bare plant.', 'quiet');
        bumpHud();
      } else if (e.kind === 'mood') {
        if (e.to === 'Fleeing') toast('Hurt, it bolts.', 'quiet');
        if (e.to === 'Lunging') {
          const fk = hunt.director.formIx;
          if (lungeToasts++ < 2) toast(fk === 3 ? 'It charges you, jaws wide - they snap shut as they reach you. Hurt it and it runs.'
            : fk === 1 ? 'It charges you, every head reared - they all strike as it reaches you. Hurt it and it runs.'
            : 'It turns on you - its guards are out round its jaws. Hurt it and it runs.', 'gold');
          else toast('It lunges.', 'quiet');
          kick(5);
        }
      } else if (e.kind === 'snapped') {
        const d = hunt.director, at = V.add(hunt.swarm.anchor, hunt.swarm.inBody(d.form.lungeMouth));
        flashAt(at, DANGER, 120, 0.35); shockAt(at, DANGER, 70, 0.35); audio.snap(); kick(6);
        const heads = d.formIx === 1;
        if (snapToasts++ < 3) toast(heads ? (e.gap < 60 ? 'Every head strikes at you.' : 'Every head strikes - just short.')
          : (e.gap < 60 ? 'Its jaws snap shut on you.' : 'Its jaws snap shut - just short.'), 'quiet');
      }
      else if (e.kind === 'cut') {
        burst(e.at, Math.min(8, 2 + e.n), 90, 2.6, 0.6, ui.colour === 'domain' ? [ONE] : [EC[1], EC[2], EC[0]]);
        if (performance.now() - lastHitSound > 70) { audio.hit(); lastHitSound = performance.now(); }
        if (e.pilot === 0 && e.n >= 25) toast(`${e.n} cut in one pass`, 'quiet');
      } else if (e.kind === 'stung') {
        shockAt(e.at, DANGER, 60, 0.45); flashAt(e.at, DANGER, 90, 0.4);
        if (e.pilot === 0) { vignette('sting'); audio.sting(); kick(7); toast('Stung by a guard plate: your dart is slowed for 2.5 s.', 'quiet'); }
      } else if (e.kind === 'plantCut') burst(e.at, Math.min(6, e.n), 60, 3, 0.6, [EC[1]]);
      else if (e.kind === 'ended') {
        const win = !!S.PILOTS_WIN[e.outcome];
        if (win && e.outcome !== 'HeldOff' && e.outcome !== 'DanceBroken') {   // the body breaks into gold
          burstFromBody(sw, 220, 70 * 3, 5.5);
          for (let i = 0; i < sw.cap; i++) if (sw.isLive(i)) sw.kill(i, -1, 10 + Math.floor(Math.random() * 14));
        }
        if (e.outcome === 'DanceBroken') { burstFromBody(sw, 140, 70 * 2.6, 5); tintTo(BASE, 5, 0.8); }
        flashAt(sw.anchor, win ? GOLD : DANGER, 420, 0.9); kick(24); audio.boom();
        toast(OUT[e.outcome][2], win ? 'gold' : '');
        if (e.outcome !== 'Completed' && tint.gold) tintTo(BASE, 5, 0.4);
        clearHaloMeshes(false);
        setTimeout(() => { if (hunt === h && ui.mode === 'hunt') showOutcome(e.outcome); }, 2600);
        bumpHud();
      }
    }
    h.events.length = 0;
    sw.events.length = 0;
  }

  // ── the loop: the model at 10 Hz (the game's tick), drawn every frame between ticks ──
  let last = performance.now(), acc = 0, beatAcc = 0, hudAt = 0;
  const shakeV = new THREE.Vector3();
  function frame(tms) {
    const dt = Math.min(0.1, (tms - last) / 1000); last = tms;
    const t = tms / 1000;
    if (!ui.paused) {
      acc += dt * ui.speed;
      let n = 0;
      while (acc >= S.DT && n < 40) { acc -= S.DT; if (ui.mode === 'hunt') hunt.tick(); else pgTick(); n++; }
      if (n === 40) acc = 0;
    }
    const alpha = ui.paused ? 1 : Math.min(1, acc / S.DT);
    if (ui.mode === 'hunt') drainHunt();
    // the drum: it quickens over the 30 s
    const inDance = ui.mode === 'hunt' ? hunt.director.inDance : !!pg.halo;
    if (inDance && !ui.paused) {
      const u = ui.mode === 'hunt' ? Math.min(1, hunt.director.danceTime / S.SETTINGS.drumSeconds) : (pg.clock % 30) / 30;
      beatAcc -= dt * ui.speed;
      if (beatAcc <= 0) { beatAcc = 1.1 - 0.75 * Math.pow(u, 1.3); audio.drum(u); tint.pulse = 1; if (!REDUCED) vignette('beat'); }
    }
    updateTint(dt);
    updateCamera(dt, alpha);
    const sw = activeSwarm();
    drawMembers(sw, alpha, t);
    drawPlants(t);
    drawHalo(dt, t);
    drawDarts(alpha, t);
    drawShards(ui.paused ? 0 : dt);
    drawFx(dt);
    if (tms - hudAt > 100) { hudAt = tms; updateHud(); }
    shake.amp *= Math.pow(0.02, dt);
    shakeV.set((Math.random() - 0.5) * shake.amp, (Math.random() - 0.5) * shake.amp, (Math.random() - 0.5) * shake.amp);
    camera.position.add(shakeV);
    if (composer) composer.render(); else renderer.render(scene, camera);
    camera.position.sub(shakeV);
    requestAnimationFrame(frame);
  }

  // ── boot ───────────────────────────────────────────────────────────────────
  function start(data) {
    data = data || {};
    for (const k of ['speed', 'cam', 'colour', 'sound', 'ai']) if (data[k] !== undefined) ui[k] = data[k];
    syncSeg('seg-speed', ui.speed); syncSeg('seg-cam', ui.cam); syncSeg('seg-colour', ui.colour); syncSeg('seg-ai', ui.ai);
    $('c-sound').checked = ui.sound;
    resize();
    bank = S.buildBank(window.PLAN_DATA);
    newPlayground();
    newHunt(matchSeed);
    applyPalette(BASE, 1);
    // first frame: the hatchling and the plants round it, from the side
    const a = hunt.swarm.anchor;
    controls.target.set(a[0], a[1], a[2]); camera.position.set(a[0] - 160, a[1] + 210, a[2] + 430);
    setMode(data.mode === 'forms' ? 'forms' : 'hunt');
    camTween = null;
    requestAnimationFrame(frame);
  }
  window.__tandava = { get hunt() { return hunt; }, get pg() { return pg; }, get ui() { return ui; } };
  if (window.claude && window.claude.hot && window.claude.hot.snapshot)
    window.claude.hot.snapshot(() => ({ mode: ui.mode, speed: ui.speed, cam: ui.cam, colour: ui.colour, sound: ui.sound, ai: ui.ai }));
  if (window.claude && window.claude.hot && window.claude.hot.ready) window.claude.hot.ready(start);
  else start((window.claude && window.claude.hot && window.claude.hot.data) || {});
})();
