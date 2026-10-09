/* The Vessel Studio look (/vessel-studio D19): the default graphics every studio's 3D stage uses, taken from the
   Stoat Flight Studio (StoatFlightStudio.html, "renderer" and "prisms" sections) so all studios read as one place.

     <script src=".../three.min.js"></script>
     <script src="studio-look.js"></script>
     const env = VesselStudioLook.install(scene, { field: 2400, prismScale: 2 });   // sky, stars, lights, prism field
     env.placeField(centre, radius, height);   // ring the drifting prism field around the course (call per course)
     env.update(dt);                           // each frame: the field tumbles slowly
     const c = VesselStudioLook.crystal(0x35e0b0, 7);   // a crystal you can see from across the course (spin c.userData.core)
     const mark = VesselStudioLook.marker(viewportEl);  // a screen marker for the thing to fly to
     mark.set(camera, worldPos, 'CRYSTAL 3/24', '#35e0b0', width, height) / mark.hide()

   The sky and stars ride with the camera, so they read as infinitely far; neither takes fog. Needs the global THREE
   (r128). ASCII only: this file is served beside the pages without a charset. */
(function () {
  'use strict';
  const T = window.THREE;
  if (!T) { console.warn('[studio-look] three.js is not loaded'); return; }

  // ---- the sky: deep blue to indigo, three nebula layers, a faint latitude/longitude grid (the Stoat's shader) ----
  const SKY_FRAG = [
    'varying vec3 vDir;',
    'void main(){',
    '  vec3 d = normalize(vDir);',
    '  float h = d.y * 0.5 + 0.5;',
    '  vec3 col = mix(vec3(0.015, 0.02, 0.07), vec3(0.05, 0.08, 0.2), smoothstep(0.0, 1.0, h));',
    '  float n = sin(d.x * 5.1 + sin(d.y * 3.7)) * sin(d.z * 4.3 + sin(d.x * 2.9)) * 0.5 + 0.5;',
    '  float n2 = sin(d.x * 11.3 + d.z * 7.1) * sin(d.y * 9.7 - d.x * 5.3) * 0.5 + 0.5;',
    '  float n3 = sin(d.z * 17.0 + d.y * 3.0) * sin(d.x * 13.0 - d.z * 6.0) * 0.5 + 0.5;',
    '  col += vec3(0.30, 0.12, 0.48) * pow(n, 3.0) * 0.55 + vec3(0.04, 0.26, 0.36) * pow(n2, 4.0) * 0.5 + vec3(0.2, 0.1, 0.05) * pow(n3, 8.0) * 0.4;',
    '  float lat = abs(fract(asin(clamp(d.y, -1.0, 1.0)) * 3.8197) - 0.5);',
    '  float lon = abs(fract(atan(d.z, d.x) * 3.8197) - 0.5);',
    '  col += vec3(0.10, 0.16, 0.32) * (smoothstep(0.485, 0.5, lat) + smoothstep(0.485, 0.5, lon)) * 0.6;',
    '  gl_FragColor = vec4(col, 1.0);',
    '}'].join('\n');

  // a deterministic generator, so the field is the same on every load (the Stoat seeds its field for the scorecard)
  function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }

  function install(scene, o) {
    o = o || {};
    const R = o.skyRadius || 3000;
    scene.background = null; scene.fog = null;
    scene.add(new T.HemisphereLight(0x9fbaff, 0x1a1230, 0.85));
    const sun = new T.DirectionalLight(0xffffff, 0.85); sun.position.set(0.4, 1, 0.3); scene.add(sun);

    const sky = new T.Mesh(new T.SphereGeometry(R, 48, 24), new T.ShaderMaterial({
      side: T.BackSide, depthWrite: false,
      vertexShader: 'varying vec3 vDir; void main(){ vDir = position; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
      fragmentShader: SKY_FRAG,
    }));
    sky.frustumCulled = false; sky.renderOrder = -10;
    sky.onBeforeRender = (r, s, c) => { sky.position.copy(c.position); sky.updateMatrixWorld(); };
    scene.add(sky);

    const N = o.stars || 5000, pos = new Float32Array(N * 3), col = new Float32Array(N * 3), rs = mulberry32(7);
    for (let i = 0; i < N; i++) {
      const u = rs() * 2 - 1, a = rs() * Math.PI * 2, r = Math.sqrt(1 - u * u);
      pos[i * 3] = R * 0.97 * r * Math.cos(a); pos[i * 3 + 1] = R * 0.97 * u; pos[i * 3 + 2] = R * 0.97 * r * Math.sin(a);
      const b = 0.5 + rs() * 0.5, t = rs();
      col[i * 3] = b * (0.8 + 0.2 * t); col[i * 3 + 1] = b * 0.9; col[i * 3 + 2] = b * (1.0 - 0.15 * t);
    }
    const sg = new T.BufferGeometry(); sg.setAttribute('position', new T.BufferAttribute(pos, 3)); sg.setAttribute('color', new T.BufferAttribute(col, 3));
    const stars = new T.Points(sg, new T.PointsMaterial({ size: 2, sizeAttenuation: false, vertexColors: true, depthWrite: false, fog: false }));
    stars.frustumCulled = false; stars.renderOrder = -9;
    stars.onBeforeRender = (r, s, c) => { stars.position.copy(c.position); stars.updateMatrixWorld(); };
    scene.add(stars);

    // ---- the drifting prism field around the course: the Stoat's blue boxes, seeded, slowly tumbling ----
    const NF = o.field == null ? 2400 : o.field, ps = o.prismScale || 1;
    let field = null; const spin = new Float32Array(NF * 3), home = new Float32Array(NF * 3), quat = [];
    if (NF > 0) {
      field = new T.InstancedMesh(new T.BoxGeometry(1.6 * ps, 0.55 * ps, 4.4 * ps), new T.MeshStandardMaterial({ color: 0xffffff, emissive: 0x0b1b4a, roughness: 0.55, flatShading: true }), NF);
      field.instanceMatrix.setUsage(T.DynamicDrawUsage); field.frustumCulled = false;
      const c = new T.Color(), rnd = mulberry32(20261009);
      for (let i = 0; i < NF; i++) {
        c.setHSL(0.6 + rnd() * 0.12, 0.65, 0.35 + rnd() * 0.2); field.setColorAt(i, c);
        quat.push(new T.Quaternion().setFromEuler(new T.Euler(rnd() * 6.28, rnd() * 6.28, rnd() * 6.28)));
        spin[i * 3] = (rnd() - 0.5) * 0.4; spin[i * 3 + 1] = (rnd() - 0.5) * 0.4; spin[i * 3 + 2] = (rnd() - 0.5) * 0.4;
      }
      scene.add(field);
    }
    const dum = new T.Object3D(), dq = new T.Quaternion(), e = new T.Euler();
    function placeField(centre, radius, height) {
      if (!field) return;
      const rnd = mulberry32(20261009 + 1), cx = centre ? centre.x : 0, cy = centre ? centre.y : 0, cz = centre ? centre.z : 0;
      const r0 = radius || 600, hh = height || r0 * 0.6;
      for (let i = 0; i < NF; i++) {   // a thick ring outside the course, like the Stoat's 330..930 u band round its rings
        const a = rnd() * Math.PI * 2, r = r0 * (0.75 + rnd() * 0.9), y = (rnd() - 0.5) * hh * 2;
        home[i * 3] = cx + Math.cos(a) * r; home[i * 3 + 1] = cy + y; home[i * 3 + 2] = cz + Math.sin(a) * r;
      }
      update(0);
    }
    function update(dt) {
      if (!field) return;
      for (let i = 0; i < NF; i++) {
        if (dt) { e.set(spin[i * 3] * dt, spin[i * 3 + 1] * dt, spin[i * 3 + 2] * dt); quat[i].multiply(dq.setFromEuler(e)); }
        dum.position.set(home[i * 3], home[i * 3 + 1], home[i * 3 + 2]); dum.quaternion.copy(quat[i]); dum.scale.setScalar(1); dum.updateMatrix();
        field.setMatrixAt(i, dum.matrix);
      }
      field.instanceMatrix.needsUpdate = true;
    }
    placeField(null, o.fieldRadius || 600, o.fieldHeight);
    if (field && field.instanceColor) field.instanceColor.needsUpdate = true;
    return { sky, stars, field, placeField, update };
  }

  // ---- a crystal you can see from across the course: a faceted core, a glow, a slow ring and a beacon ----
  let glowTex = null;
  function glowTexture() {
    if (glowTex) return glowTex;
    const c = document.createElement('canvas'); c.width = c.height = 64; const g = c.getContext('2d');
    const gr = g.createRadialGradient(32, 32, 0, 32, 32, 32); gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(0.25, 'rgba(255,255,255,0.55)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = gr; g.fillRect(0, 0, 64, 64); glowTex = new T.CanvasTexture(c); return glowTex;
  }
  function crystal(color, size) {
    const s = size || 7, g = new T.Group(), col = new T.Color(color);
    const core = new T.Mesh(new T.OctahedronGeometry(s, 0), new T.MeshStandardMaterial({ color: col, emissive: col.clone().multiplyScalar(0.55), roughness: 0.2, metalness: 0.1, flatShading: true }));
    core.scale.set(1, 1.35, 1); g.add(core);
    const shell = new T.Mesh(new T.OctahedronGeometry(s * 1.35, 0), new T.MeshBasicMaterial({ color: col, wireframe: true, transparent: true, opacity: 0.35, depthWrite: false }));
    shell.scale.set(1, 1.35, 1); g.add(shell);
    const glow = new T.Sprite(new T.SpriteMaterial({ map: glowTexture(), color: col, transparent: true, opacity: 0.85, depthWrite: false, blending: T.AdditiveBlending }));
    glow.scale.setScalar(s * 7); g.add(glow);
    const ring = new T.Mesh(new T.TorusGeometry(s * 2.4, s * 0.09, 6, 40), new T.MeshBasicMaterial({ color: col, transparent: true, opacity: 0.6, depthWrite: false }));
    g.add(ring);
    const beam = new T.Mesh(new T.CylinderGeometry(s * 0.12, s * 0.5, s * 60, 8, 1, true), new T.MeshBasicMaterial({ color: col, transparent: true, opacity: 0.18, depthWrite: false, blending: T.AdditiveBlending, side: T.DoubleSide }));
    beam.position.y = s * 30; g.add(beam);
    g.userData = { core, shell, ring, glow };
    return g;
  }
  function spinCrystal(g, t) {
    const u = g.userData; if (!u || !u.core) return;
    u.core.rotation.y = t * 1.3; u.shell.rotation.y = -t * 0.7; u.ring.rotation.set(Math.PI / 2 + Math.sin(t * 0.8) * 0.5, t * 0.9, 0);
    u.glow.material.opacity = 0.7 + 0.25 * Math.sin(t * 3);
  }

  // ---- a screen marker for the thing to fly to (the Stoat's "RING 1 . 105 u"), clamped to the edge when off screen ----
  function marker(parent) {
    const el = document.createElement('div');
    el.style.cssText = 'position:absolute;left:0;top:0;pointer-events:none;z-index:2;display:none;transform:translate(-50%,-50%);text-align:center;font:700 12px/1.2 "Saira Condensed","Arial Narrow",system-ui,sans-serif;letter-spacing:.06em;text-transform:uppercase;text-shadow:0 1px 3px #000';
    const ring = document.createElement('div'); ring.style.cssText = 'width:26px;height:26px;margin:0 auto 3px;border:2px solid currentColor;border-radius:50%;box-sizing:border-box';
    const lab = document.createElement('div'); el.append(ring, lab); parent.appendChild(el);
    const v = new T.Vector3();
    return {
      el,
      hide() { el.style.display = 'none'; },
      set(cam, pos, label, color, w, h) {
        v.copy(pos).applyMatrix4(cam.matrixWorldInverse); const behind = v.z > 0;
        v.copy(pos).project(cam);
        let x = (v.x * 0.5 + 0.5) * w, y = (-v.y * 0.5 + 0.5) * h; if (behind) { x = w - x; y = h - y; }
        const m = 26, on = !behind && x >= m && x <= w - m && y >= m && y <= h - m;
        if (!on) {   // pin to the edge, pointing the way to turn
          const cx = w / 2, cy = h / 2, dx = x - cx, dy = y - cy, k = Math.min((cx - m) / Math.max(1e-3, Math.abs(dx)), (cy - m) / Math.max(1e-3, Math.abs(dy)));
          x = cx + dx * k; y = cy + dy * k;
        }
        el.style.display = 'block'; el.style.color = color; el.style.left = x + 'px'; el.style.top = y + 'px';
        ring.style.borderStyle = on ? 'solid' : 'dashed'; ring.style.width = ring.style.height = on ? '26px' : '18px';
        lab.textContent = (on ? '' : '\u2192 ') + label;
      },
    };
  }

  window.VesselStudioLook = { install, crystal, spinCrystal, marker };
})();
