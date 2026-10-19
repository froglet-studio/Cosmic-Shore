// ============================================================================================
// ui.js — drawing, input, the HUD and control bar, the docks, the decision log, the loop, window.__lab.
// ============================================================================================
const canvas = $('stage'), g2 = canvas.getContext('2d');
const DOM_COL = ['#4f78d6', '#1fb8b0', '#c2368e', '#d9a73a'];
const VERB_COL = { graze: '#b5c24a', gnaw: '#9b948d', build: '#f0a043', claim: '#f2c84b', fetch: '#9aa9c9', trail: '#c9693c', sting: '#ff6a3d', drain: '#c95b5b', murmur: '#7f8fa6', ping: '#a98be0', flash: '#d7f25a', steal: '#59606d', frenzy: '#e04545', network: '#e6d36a', web: '#cfcfcf', link: '#6fd4e6', plasma: '#5fe0ff', merge: '#5aa86a', march: '#b5c24a' };

// ================= platform =================
function detectPlatform() {
  const host = window.__labHost || {}, ua = navigator.userAgent || '';
  const mobileUA = (navigator.userAgentData && navigator.userAgentData.mobile) || /Android|iPhone|iPad|iPod|Mobile/i.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1);
  const coarseOnly = matchMedia('(pointer: coarse)').matches && !matchMedia('(any-pointer: fine)').matches;
  return { shell: host.shell || 'web', device: host.device || ((mobileUA || coarseOnly) ? 'phone' : 'pc') };
}
const PLATFORM = detectPlatform();
$('platform').textContent = (PLATFORM.shell === 'web' ? 'Web' : PLATFORM.shell) + ' · ' + (PLATFORM.device === 'phone' ? 'Phone' : 'PC');

// ================= view transform =================
let view = { s: 1, cx: 0, cy: 0 };
function resize() { const r = canvas.getBoundingClientRect(), dpr = Math.min(2, devicePixelRatio || 1); const w = Math.max(1, Math.round(r.width * dpr)), h = Math.max(1, Math.round(r.height * dpr)); if (canvas.width !== w || canvas.height !== h) { canvas.width = w; canvas.height = h; } }
function computeView() {
  const cb = $('ctrlBar'), over = getComputedStyle(cb).position === 'absolute';
  const w = canvas.width, h = canvas.height, bar = over ? (cb.offsetHeight || 0) * (canvas.width / Math.max(1, canvas.clientWidth)) : 0;
  const avail = Math.max(80, h - bar * 0.85);
  view.s = Math.min(w, avail) / (2 * R + 40); view.cx = w / 2; view.cy = avail / 2 + h * 0.01;
}
const wx = (x) => view.cx + x * view.s, wy = (y) => view.cy + y * view.s;
function toWorld(clientX, clientY) { const r = canvas.getBoundingClientRect(), k = canvas.width / Math.max(1, r.width); return { x: ((clientX - r.left) * k - view.cx) / view.s, y: ((clientY - r.top) * k - view.cy) / view.s }; }

// ================= fields as a small image =================
const fieldCanvas = document.createElement('canvas'); fieldCanvas.width = FG; fieldCanvas.height = FG;
const fctx = fieldCanvas.getContext('2d'), fimg = fctx.createImageData(FG, FG);
function drawFields() {
  const d = fimg.data; let any = false;
  for (let k = 0; k < FG * FG; k++) {
    const p = W.pher[k], t = W.trail[k], b = W.blood[k], i = k * 4;
    const a = Math.min(1, p * 0.12 + t * 0.07 + b * 0.25); if (a > 0.01) any = true;
    d[i] = Math.min(255, 240 * Math.min(1, p * 0.2) + 230 * Math.min(1, t * 0.12) + 220 * Math.min(1, b * 0.4));
    d[i + 1] = Math.min(255, 160 * Math.min(1, p * 0.2) + 210 * Math.min(1, t * 0.12) + 40 * Math.min(1, b * 0.4));
    d[i + 2] = Math.min(255, 60 * Math.min(1, p * 0.2) + 90 * Math.min(1, t * 0.12) + 40 * Math.min(1, b * 0.4));
    d[i + 3] = a * 150;
  }
  if (!any) return;
  fctx.putImageData(fimg, 0, 0);
  g2.imageSmoothingEnabled = true;
  g2.drawImage(fieldCanvas, wx(-R), wy(-R), 2 * R * view.s, 2 * R * view.s);
}

// ================= render =================
function render() {
  if (!W) return;
  computeView();
  const s = view.s, w = canvas.width, h = canvas.height;
  g2.fillStyle = '#061014'; g2.fillRect(0, 0, w, h);
  // the cell: membrane + nucleus
  g2.strokeStyle = 'rgba(120,200,210,.35)'; g2.lineWidth = Math.max(1, 2 * s); g2.beginPath(); g2.arc(wx(0), wy(0), R * s, 0, TAU); g2.stroke();
  g2.fillStyle = 'rgba(80,140,170,.08)'; g2.beginPath(); g2.arc(wx(0), wy(0), NUC * s, 0, TAU); g2.fill();
  drawFields();
  // prisms, batched by colour
  const ps = Math.max(1.6, 4.2 * s), buckets = {};
  for (const p of W.prisms) {
    if (!p.alive || (p.hidden && W.scene === 'dark')) continue;
    const key = p.dom + (p.dom === ME && p.built === 1 ? 'b' : '') + (p.sh ? 's' : '');
    (buckets[key] || (buckets[key] = [])).push(p);
  }
  for (const key of Object.keys(buckets)) {
    const dom = +key[0], built = key.includes('b'), sh = key.includes('s');
    g2.fillStyle = built ? '#5ff0e0' : DOM_COL[dom]; g2.globalAlpha = dom === DOM.NEUTRAL ? 0.7 : 0.95;
    for (const p of buckets[key]) g2.fillRect(wx(p.x) - ps / 2, wy(p.y) - ps / 2, ps, ps);
    if (sh) { g2.strokeStyle = '#e8f6ff'; g2.lineWidth = Math.max(1, s); for (const p of buckets[key]) { const x = wx(p.x), y = wy(p.y), r = ps * 1.2; g2.beginPath(); g2.moveTo(x, y - r); g2.lineTo(x + r, y); g2.lineTo(x, y + r); g2.lineTo(x - r, y); g2.closePath(); g2.stroke(); } }
  }
  g2.globalAlpha = 1;
  // homes
  for (const hm of W.homes) { g2.strokeStyle = '#f0a043'; g2.lineWidth = Math.max(1.5, 2.5 * s); g2.beginPath(); g2.arc(wx(hm.x), wy(hm.y), 14 * s, 0, TAU); g2.stroke(); }
  // webs
  g2.strokeStyle = 'rgba(230,230,230,.6)'; g2.lineWidth = Math.max(1, 1.2 * s);
  for (const wb of W.webs) { g2.beginPath(); g2.moveTo(wx(wb.x0), wy(wb.y0)); g2.lineTo(wx(wb.x1), wy(wb.y1)); g2.stroke(); }
  // crystals
  for (const c of W.crystals) {
    if (!c.alive || c.carriedBy >= 0) continue;
    if (c.hidden && !c.revealed) { if (W.scene !== 'dark') { g2.strokeStyle = 'rgba(160,255,200,.15)'; g2.beginPath(); g2.arc(wx(c.x), wy(c.y), 5 * s, 0, TAU); g2.stroke(); } continue; }
    const r = (c.kind === 'gate' ? 9 : 7) * s * (c.order === W.gateNext && W.game === 'time' ? 1.15 + 0.15 * Math.sin(W.t * 6) : 1);
    g2.fillStyle = c.decoy ? '#ff9adf' : c.kind === 'gate' ? (c.order === W.gateNext ? '#9ff7c6' : 'rgba(159,247,198,.35)') : '#9ff7c6';
    g2.beginPath(); g2.moveTo(wx(c.x), wy(c.y) - r); g2.lineTo(wx(c.x) + r * 0.7, wy(c.y)); g2.lineTo(wx(c.x), wy(c.y) + r); g2.lineTo(wx(c.x) - r * 0.7, wy(c.y)); g2.closePath(); g2.fill();
    if (c.order >= 0) { g2.fillStyle = '#061014'; g2.font = (10 * Math.max(0.8, s)) + 'px IBM Plex Mono, monospace'; g2.textAlign = 'center'; g2.textBaseline = 'middle'; g2.fillText(String(c.order + 1), wx(c.x), wy(c.y)); }
  }
  // neutral fauna
  g2.fillStyle = '#7d9fd9'; for (const f of W.fauna) if (f.alive) { g2.beginPath(); g2.arc(wx(f.x), wy(f.y), 4 * s, 0, TAU); g2.fill(); }
  // swarm links (siphonophore chain / plasma school)
  if (W.chain.length > 1) {
    const plasma = W.chain[0].verb === 'plasma';
    g2.strokeStyle = plasma ? 'rgba(95,224,255,.85)' : 'rgba(111,212,230,.5)'; g2.lineWidth = Math.max(1, (plasma ? 2.4 : 1.6) * s);
    g2.beginPath(); W.chain.forEach((a, i) => { if (i === 0) g2.moveTo(wx(a.x), wy(a.y)); else g2.lineTo(wx(a.x), wy(a.y)); }); if (plasma && W.chain.length > 2) g2.closePath(); g2.stroke();
  }
  // members
  for (const a of W.agents) {
    if (!a.alive) continue;
    const sp = SPECIES_BY_ID[a.sp];
    let col = a.verb === sp.verb || (sp.id === 'locust' && a.verb === 'march') ? sp.col : (VERB_COL[a.verb] || sp.col);
    if (sp.id === 'locust') col = a.g > 0.5 ? '#e2b33c' : '#7fb24a';
    if (a.verb === 'flash') { const b = 0.35 + 0.65 * Math.max(0, Math.cos(a.phase)) ** 6; g2.globalAlpha = b; col = '#e9ff7a'; }
    g2.fillStyle = col;
    const r = Math.max(1.3, a.size * s * 0.9);
    g2.beginPath(); g2.arc(wx(a.x), wy(a.y), r, 0, TAU); g2.fill();
    g2.globalAlpha = 1;
    if (a.carry) { g2.fillStyle = a.carry.kind === 'crystal' ? '#9ff7c6' : '#5ff0e0'; g2.fillRect(wx(a.x) - r * 0.6, wy(a.y) - r * 2.2, r * 1.2, r * 1.2); }
    if (W.ctl.id === 'select' && a.sel) { g2.strokeStyle = 'rgba(255,255,255,.7)'; g2.lineWidth = 1; g2.beginPath(); g2.arc(wx(a.x), wy(a.y), r + 2, 0, TAU); g2.stroke(); }
    if (W.ctl.id === 'leader' && W.agents[W.leaderIdx] === a) { g2.strokeStyle = '#f0a043'; g2.lineWidth = 2; g2.beginPath(); g2.arc(wx(a.x), wy(a.y), r + 4, 0, TAU); g2.stroke(); }
  }
  // rivals
  for (const r of W.rivals) {
    if (!r.alive) continue;
    const a = Math.atan2(r.vy, r.vx), x = wx(r.x), y = wy(r.y), z = 9 * s;
    g2.fillStyle = DOM_COL[r.dom]; g2.beginPath(); g2.moveTo(x + Math.cos(a) * z * 1.4, y + Math.sin(a) * z * 1.4); g2.lineTo(x + Math.cos(a + 2.4) * z, y + Math.sin(a + 2.4) * z); g2.lineTo(x + Math.cos(a - 2.4) * z, y + Math.sin(a - 2.4) * z); g2.closePath(); g2.fill();
    if (r.stun > 0) { g2.strokeStyle = '#e9ff7a'; g2.lineWidth = 1.5; g2.beginPath(); g2.arc(x, y, z * 1.6, 0, TAU); g2.stroke(); }
    if (r.marked > 0) { g2.strokeStyle = '#59606d'; g2.setLineDash([3, 3]); g2.beginPath(); g2.arc(x, y, z * 2, 0, TAU); g2.stroke(); g2.setLineDash([]); }
    g2.fillStyle = '#e8eeee'; g2.font = (10 * Math.max(0.8, s)) + 'px IBM Plex Mono, monospace'; g2.textAlign = 'center'; g2.fillText(String(r.petals), x, y - z * 2);
  }
  // projectiles
  g2.fillStyle = '#ffd1e8'; for (const q of W.proj) g2.fillRect(wx(q.x) - 1.5, wy(q.y) - 1.5, 3, 3);
  // the hull (the queen / the vessel)
  { const hh = W.hull, x = wx(hh.x), y = wy(hh.y), a = Math.atan2(hh.hy, hh.hx), z = 11 * s;
    g2.fillStyle = '#f0a043'; g2.strokeStyle = '#fff3df'; g2.lineWidth = Math.max(1, 1.5 * s);
    g2.beginPath(); g2.moveTo(x + Math.cos(a) * z * 1.5, y + Math.sin(a) * z * 1.5); g2.lineTo(x + Math.cos(a + 2.2) * z, y + Math.sin(a + 2.2) * z); g2.lineTo(x + Math.cos(a + Math.PI) * z * 0.6, y + Math.sin(a + Math.PI) * z * 0.6); g2.lineTo(x + Math.cos(a - 2.2) * z, y + Math.sin(a - 2.2) * z); g2.closePath(); g2.fill(); g2.stroke();
    g2.fillStyle = '#fff3df'; g2.font = (10 * Math.max(0.8, s)) + 'px IBM Plex Mono, monospace'; g2.textAlign = 'center'; g2.fillText(String(hh.petals), x, y - z * 1.9); }
  // markers: focus, squad B, bait, cast wind-up, select box
  const mark = (p, col, rr) => { if (!p) return; g2.strokeStyle = col; g2.lineWidth = 1.5; const x = wx(p.x), y = wy(p.y); g2.beginPath(); g2.arc(x, y, rr, 0, TAU); g2.moveTo(x - rr - 4, y); g2.lineTo(x - rr + 4, y); g2.moveTo(x + rr - 4, y); g2.lineTo(x + rr + 4, y); g2.stroke(); };
  if (W.ctl.id !== 'leash' && W.ctl.id !== 'formation' && W.ctl.id !== 'sheepdog') mark(W.focus, 'rgba(240,160,67,.8)', 9);
  if (W.ctl.id === 'split') mark(W.focusB, 'rgba(159,247,198,.8)', 9);
  if (W.bait && W.bait.t > 0) mark(W.bait, '#ff9adf', 6 + 3 * Math.sin(W.t * 8));
  if (ui.box) { g2.strokeStyle = 'rgba(255,255,255,.7)'; g2.setLineDash([4, 4]); g2.strokeRect(wx(Math.min(ui.box.x0, ui.box.x1)), wy(Math.min(ui.box.y0, ui.box.y1)), Math.abs(ui.box.x1 - ui.box.x0) * s, Math.abs(ui.box.y1 - ui.box.y0) * s); g2.setLineDash([]); }
  if (W.ctl.id === 'cast' && liveInput.down && liveInput.px != null) { const c = swarmCentre(); g2.strokeStyle = 'rgba(201,91,91,.8)'; g2.lineWidth = 1 + (W.castCharge || 0) * 2; g2.beginPath(); g2.moveTo(wx(c.x), wy(c.y)); g2.lineTo(wx(liveInput.px), wy(liveInput.py)); g2.stroke(); }
  // fx
  for (const f of W.fx) {
    const k = f.t / 1.2, x = wx(f.x), y = wy(f.y);
    g2.globalAlpha = Math.max(0, 1 - k);
    if (f.k === 'ring' || f.k === 'ping' || f.k === 'flash' || f.k === 'boom') { g2.strokeStyle = f.k === 'flash' ? '#f4ff9a' : f.k === 'boom' ? '#ff8a4a' : f.k === 'ping' ? '#b49cf0' : (f.col || '#fff'); g2.lineWidth = f.k === 'flash' ? 4 : 1.5; g2.beginPath(); g2.arc(x, y, (f.r || 20) * s * (0.3 + k), 0, TAU); g2.stroke(); }
    else if (f.k === 'tongue') { g2.strokeStyle = '#ff9a9a'; g2.lineWidth = 2; g2.beginPath(); g2.moveTo(x, y); g2.lineTo(wx(f.x1), wy(f.y1)); g2.stroke(); }
    else if (f.k === 'card') { g2.fillStyle = '#f6c27f'; g2.font = '600 ' + Math.round(13 * Math.max(0.9, s)) + 'px Chakra Petch, sans-serif'; g2.textAlign = 'center'; g2.fillText(f.name, x, y - 18 - k * 20); }
    else if (f.k === 'sting' || f.k === 'steal' || f.k === 'snare' || f.k === 'hit') { g2.strokeStyle = f.k === 'hit' ? '#ff5d5d' : f.k === 'snare' ? '#e6e6e6' : f.k === 'steal' ? '#aab2c0' : '#ff6a3d'; g2.lineWidth = 2; g2.beginPath(); g2.arc(x, y, 6 + k * 10, 0, TAU); g2.stroke(); }
    else if (f.k === 'beat') { g2.strokeStyle = f.good ? '#9ff7c6' : '#ff5d5d'; g2.lineWidth = 3; g2.beginPath(); g2.arc(x, y, 16 + k * 26, 0, TAU); g2.stroke(); }
    else if (f.k === 'pop' || f.k === 'claim' || f.k === 'shed') { g2.fillStyle = f.k === 'claim' ? '#5ff0e0' : f.k === 'shed' ? '#e8f6ff' : '#ffffff'; g2.fillRect(x - 1.5, y - 1.5 - k * 6, 3, 3); }
  }
  g2.globalAlpha = 1;
  // dark scene: a vignette, so the reveal reads
  if (W.scene === 'dark') { const gr = g2.createRadialGradient(wx(W.hull.x), wy(W.hull.y), 30 * s, wx(W.hull.x), wy(W.hull.y), 360 * s); gr.addColorStop(0, 'rgba(0,0,0,0)'); gr.addColorStop(1, 'rgba(0,0,0,.55)'); g2.fillStyle = gr; g2.fillRect(0, 0, w, h); }
  // rhythm pulse ring around the ship
  if (W.ctl.id === 'rhythm') { const ph = ((W.beatT || 0) % BEAT) / BEAT; g2.strokeStyle = `rgba(240,160,67,${0.25 + 0.6 * (1 - ph)})`; g2.lineWidth = 2; g2.beginPath(); g2.arc(wx(W.hull.x), wy(W.hull.y), (18 + ph * 26) * s, 0, TAU); g2.stroke(); }
  drawHud();
}

// ================= HUD =================
let hudFrame = 0;
function drawHud() {
  if (hudFrame++ % 6) return;
  const toy = TOYS_BY_ID[BUILD.toy];
  $('hudToy').textContent = (toy ? toy.title : 'Custom build') + '  ·  ' + SPECIES_BY_ID[BUILD.species].name + ' / ' + CONTROLS_BY_ID[BUILD.control].name;
  $('hudFun').textContent = toy ? toy.fun : (SPECIES_BY_ID[BUILD.species].fun);
  const sc = score(), live = sc.members, lines = [];
  const sceneName = SCENES_BY_ID[W.scene].name;
  lines.push(sceneName + (W.game ? '  ' + Math.max(0, P.round - W.t).toFixed(0) + 's' : ''));
  lines.push('score ' + sc.score + (W.done ? '  (final)' : ''));
  lines.push('members ' + live + (W.econ.cap ? '/' + agentCap() : ''));
  if (W.game === 'time') lines.push('gate ' + Math.min(W.gateNext + 1, W.gateCount) + '/' + W.gateCount);
  if (W.agents.some((a) => a.alive && a.verb === 'flash')) lines.push('sync ' + (W.sync * 100).toFixed(0) + '%  flashes ' + W.stats.flashes);
  if (W.sp.id === 'locust') { const l = liveAgents().filter((a) => a.sp === 'locust'); lines.push('gregarious ' + (l.length ? Math.round(100 * l.filter((a) => a.g > 0.5).length / l.length) : 0) + '%'); }
  if (W.build.meta === 'evolve') { const l = liveAgents(); if (l.length) { const m = (k) => (l.reduce((t, a) => t + a.gene[k], 0) / l.length).toFixed(2); lines.push('genes spd ' + m('spd') + ' sense ' + m('sense') + ' bite ' + m('bite')); } }
  if (W.ctl.id === 'orders') lines.push('order ' + ['Forage', 'Gather', 'Attack', 'Defend'][W.order]);
  if (W.ctl.id === 'rhythm') lines.push('streak ' + (W.streak || 0));
  if (W.stats.starved) lines.push('starved ' + W.stats.starved);
  if (W.stats.merges) lines.push('merges ' + W.stats.merges);
  if (W.stats.snared) lines.push('snared ' + W.stats.snared);
  $('hudRight').textContent = lines.join('\n');
  const t = $('toast');
  if (W.done) { t.textContent = 'Round over — score ' + W.final.score + '. Restart to go again.'; t.classList.add('show'); }
  else if (W.toast) { t.textContent = W.toast.m; t.classList.add('show'); } else t.classList.remove('show');
  updateCtrlBar();
  updateDraft();
}

// ================= control bar =================
function ctlButton(label, key, pressed) { const b = document.createElement('button'); b.className = 'ctl'; b.textContent = label; b.dataset.key = key; if (pressed != null) b.setAttribute('aria-pressed', String(pressed)); b.onclick = () => { liveInput.key = key; }; return b; }
function buildCtrlBar() {
  const bar = $('ctrlBar'); bar.replaceChildren();
  const id = BUILD.control, meter = document.createElement('div'); meter.className = 'meter'; meter.id = 'econMeter'; bar.appendChild(meter);
  if (id === 'orders') { const ks = [['Forage', 'forage', 'Q'], ['Gather', 'gather', 'W'], ['Attack', 'attack', 'E'], ['Defend', 'defend', 'R']]; for (const [l, k, h] of ks) { const b = ctlButton(l + ' ' + h, k, false); b.dataset.order = k; bar.appendChild(b); } }
  if (id === 'formation') for (const [l, k, h] of [['Ring', 'ring', 'Q'], ['Line', 'line', 'W'], ['Wedge', 'wedge', 'E'], ['Blob', 'blob', 'R']]) { const b = ctlButton(l + ' ' + h, k, false); b.dataset.form = k; bar.appendChild(b); }
  if (id === 'split') { const b = ctlButton('Next tap: A', 'squad', null); b.id = 'squadBtn'; bar.appendChild(b); }
  if (id === 'select') bar.appendChild(ctlButton('Select all', 'all', null));
  if (id === 'rhythm') { const b = ctlButton('Beat (Space)', 'beat', null); b.style.minWidth = '120px'; bar.appendChild(b); }
  if (id === 'mood') { const w = document.createElement('label'); w.className = 'moodWrap'; w.innerHTML = '<span>Mood: calm &harr; rush</span><input type="range" min="0" max="1" step="0.01" id="moodRange">'; bar.appendChild(w); const r = w.querySelector('input'); r.value = W.mood; r.oninput = () => { liveInput.mood = parseFloat(r.value); }; }
  if (BUILD.meta === 'deck') { const hand = document.createElement('div'); hand.id = 'hand'; hand.style.display = 'flex'; hand.style.gap = '6px'; bar.appendChild(hand); }
  const ai = document.createElement('button'); ai.className = 'ctl'; ai.id = 'aiBtn'; ai.textContent = 'Watch AI'; ai.setAttribute('aria-pressed', String(!!BUILD.ai)); ai.title = 'Let the AI commander drive this control scheme'; ai.onclick = () => { BUILD.ai = !BUILD.ai; W.build.ai = BUILD.ai; saveBuild(); ai.setAttribute('aria-pressed', String(BUILD.ai)); }; bar.appendChild(ai);
  lastHandSig = '';
}
let lastHandSig = '';
function updateCtrlBar() {
  const e = W.econ, m = $('econMeter');
  if (m) {
    let html = '<span>' + ECONOMIES_BY_ID[e.id].name + '</span>';
    if (e.tanks) html += e.tanks.map((t, i) => '<div class="bar" title="tank ' + (i + 1) + '"><i style="width:' + (100 * t.v / t.max).toFixed(0) + '%;background:' + ['#f6c27f', '#f0a043', '#c46a12'][i] + '"></i></div>').join('');
    else html += '<div class="bar"><i style="width:' + (100 * e.pool / e.max).toFixed(0) + '%"></i></div><span>' + e.pool.toFixed(1) + ' / ' + e.max + ' ' + e.label + (e.id === 'foodweb' || e.id === 'biomass' ? ' · food ' + e.food.toFixed(1) : '') + (e.id === 'crystal' ? ' · gems ' + e.gems : '') + '</span>';
    m.innerHTML = html;
  }
  for (const b of document.querySelectorAll('#ctrlBar [data-order]')) b.setAttribute('aria-pressed', String(['forage', 'gather', 'attack', 'defend'][W.order] === b.dataset.order));
  for (const b of document.querySelectorAll('#ctrlBar [data-form]')) b.setAttribute('aria-pressed', String((W.formation || 'ring') === b.dataset.form));
  const sq = $('squadBtn'); if (sq) sq.textContent = 'Next tap: ' + ((W.squadNext || 0) ? 'B' : 'A');
  const hand = $('hand');
  if (hand && W.meta.id === 'deck') {
    const sig = W.meta.hand.join(',') + '|' + W.meta.hand.map((id) => canPay(CARDS_BY_ID[id]) ? 1 : 0).join('');
    if (sig !== lastHandSig) {
      lastHandSig = sig; hand.replaceChildren();
      W.meta.hand.forEach((id, i) => {
        const c = CARDS_BY_ID[id], b = document.createElement('button'); b.className = 'card' + (canPay(c) ? '' : ' poor');
        b.innerHTML = '<span class="cost"></span><span class="nm"></span><span class="ky"></span>';
        b.querySelector('.cost').textContent = e.tanks ? 'T' + (c.tier + 1) + ':' + c.cost : c.cost; b.querySelector('.nm').textContent = c.name; b.querySelector('.ky').textContent = 'key ' + (i + 1);
        b.title = c.desc + ' (' + c.src + ')'; b.onclick = () => { liveInput.card = i; };
        hand.appendChild(b);
      });
    }
  }
}
function updateDraft() {
  const box = $('draftBox');
  if (!W.draftOffer) { if (!box.hidden) box.hidden = true; return; }
  if (!box.hidden) return;
  box.innerHTML = '<h3>Draft: pick one</h3><div class="opts"></div>';
  W.draftOffer.forEach((o, i) => { const b = document.createElement('button'); b.innerHTML = '<b></b><span></span>'; b.querySelector('b').textContent = o.name; b.querySelector('span').textContent = o.desc; b.onclick = () => { pickDraft(i); box.hidden = true; }; box.querySelector('.opts').appendChild(b); });
  box.hidden = false;
}

// ================= input: pointer + keyboard =================
const ui = { down: false, t0: 0, x0: 0, y0: 0, box: null, moved: 0 };
canvas.addEventListener('pointerdown', (ev) => {
  canvas.setPointerCapture(ev.pointerId); const p = toWorld(ev.clientX, ev.clientY);
  ui.down = true; ui.t0 = performance.now(); ui.x0 = ev.clientX; ui.y0 = ev.clientY; ui.moved = 0; ui.w0 = p;
  liveInput.down = true; liveInput.px = p.x; liveInput.py = p.y;
});
canvas.addEventListener('pointermove', (ev) => {
  if (!ui.down) return; const p = toWorld(ev.clientX, ev.clientY);
  ui.moved = Math.max(ui.moved, Math.hypot(ev.clientX - ui.x0, ev.clientY - ui.y0));
  liveInput.px = p.x; liveInput.py = p.y;
  if (BUILD.control === 'select' && ui.moved > 10) ui.box = { x0: ui.w0.x, y0: ui.w0.y, x1: p.x, y1: p.y };
});
function endPointer(ev) {
  if (!ui.down) return; ui.down = false; const p = toWorld(ev.clientX, ev.clientY);
  const quick = performance.now() - ui.t0 < 300 && ui.moved < 12;
  if (ui.box) { liveInput.box = ui.box; ui.box = null; }
  else if (quick || BUILD.control !== 'cast') liveInput.tap = { x: p.x, y: p.y };
  if (BUILD.control === 'cast') liveInput.release = { x: p.x, y: p.y };
  liveInput.down = false;
}
canvas.addEventListener('pointerup', endPointer);
canvas.addEventListener('pointercancel', endPointer);
addEventListener('keydown', (ev) => {
  if (ev.target && /INPUT|TEXTAREA|SELECT/.test(ev.target.tagName)) return;
  const k = ev.key.toLowerCase();
  if (k >= '1' && k <= '4') { liveInput.card = +k - 1; return; }
  const row = { q: 0, w: 1, e: 2, r: 3 }[k];
  if (row != null) { const o = BUILD.control === 'orders' ? ['forage', 'gather', 'attack', 'defend'] : BUILD.control === 'formation' ? ['ring', 'line', 'wedge', 'blob'] : null; if (o) liveInput.key = o[row]; return; }
  if (k === ' ') { ev.preventDefault(); if (BUILD.control === 'rhythm') liveInput.key = 'beat'; else togglePause(); }
  if (k === 'a' && BUILD.control === 'split') liveInput.key = 'squad';
});

// ================= shipped-vs-yours + params =================
const outputs = {};
function fmt(v, step) { const dec = step < 1 ? Math.min(3, String(step).split('.')[1].length) : 0; return Number(v).toFixed(dec); }
function diffOnly() { const o = {}; for (const k of Object.keys(SHIPPED)) if (Math.abs(P[k] - SHIPPED[k]) > 1e-9) o[k] = P[k]; return o; }
function refreshDiff() {
  const n = Object.keys(diffOnly()).length;
  $('diff').textContent = n ? n + ' setting' + (n > 1 ? 's differ' : ' differs') + ' from shipped' : 'shipped numbers';
  for (const [, key, , , step] of SPEC) { const o = outputs[key]; if (!o || !o.out.isConnected) continue; o.out.textContent = fmt(P[key], step); o.out.classList.toggle('changed', Math.abs(P[key] - SHIPPED[key]) > 1e-9); o.out.title = 'shipped ' + SHIPPED[key]; }
}
function setParam(key, v) { P[key] = v; lsSet(LS_KEY, diffOnly()); if (key === 'pop' || key === 'droneCap' || key === 'pherMax' || key === 'pherStart') reset(); refreshDiff(); }

// ================= tabs =================
function makeTabs(barId, bodyId, tabs, storeKey) {
  const bar = $(barId), body = $(bodyId), sel = lsGet(storeKey, tabs[0].name);
  function show(name) { for (const b of bar.children) b.setAttribute('aria-selected', String(b.dataset.name === name)); body.replaceChildren(tabs.find((t) => t.name === name).build()); lsSet(storeKey, name); }
  for (const t of tabs) { const b = document.createElement('button'); b.textContent = t.name; b.dataset.name = t.name; b.setAttribute('role', 'tab'); b.onclick = () => show(t.name); bar.appendChild(b); }
  show(tabs.some((t) => t.name === sel) ? sel : tabs[0].name);
  return show;
}
const el = (tag, cls, text) => { const e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e; };

// ---- Toys ----
let toyFilter = lsGet('lab:' + LAB.name + ':toyFilter', 'all');
function toysPanel() {
  const box = el('div');
  box.appendChild(el('p', 'hint', TOYS.length + ' toys. Each is one fun moment to test. Tap a toy to load it, play it, then Keep / Cut / Merge it — the vote goes to the decision log.'));
  const f = el('div', 'flex'); f.style.marginBottom = '8px';
  for (const [k, l] of [['all', 'All'], ['time', 'Time'], ['space', 'Space'], ['mass', 'Mass'], ['charge', 'Charge'], ['other', 'Other']]) {
    const b = el('button', 'btn', l); b.setAttribute('aria-pressed', String(toyFilter === k)); b.onclick = () => { toyFilter = k; lsSet('lab:' + LAB.name + ':toyFilter', k); box.replaceWith(toysPanel()); }; f.appendChild(b);
  }
  box.appendChild(f);
  const list = el('div', 'toyList');
  for (const t of TOYS) {
    const elx = (SCENES_BY_ID[t.scene] || {}).el || '', bucket = GAMES.includes(t.scene) ? t.scene : 'other';
    if (toyFilter !== 'all' && toyFilter !== bucket) continue;
    const card = el('div', 'toy'); card.setAttribute('role', 'button'); card.tabIndex = 0; if (BUILD.toy === t.id) card.setAttribute('aria-current', 'true');
    const title = el('div', 't'); title.appendChild(el('span', '', t.title)); title.appendChild(el('small', '', SPECIES_BY_ID[t.species].name)); card.appendChild(title);
    const chips = el('div', 'chips');
    chips.appendChild(el('span', 'chip', CONTROLS_BY_ID[t.control].name)); chips.appendChild(el('span', 'chip', ECONOMIES_BY_ID[t.economy].name));
    if (t.meta !== 'none') chips.appendChild(el('span', 'chip', METAS_BY_ID[t.meta].name));
    chips.appendChild(el('span', 'chip el-' + elx, SCENES_BY_ID[t.scene].name)); card.appendChild(chips);
    card.appendChild(el('div', 'f', t.fun));
    if (BUILD.toy === t.id) {
      card.appendChild(el('div', 'f', 'Try: ' + t.try)); card.appendChild(el('div', 'f', 'Watch: ' + t.watch));
      if (t.notion) card.appendChild(el('div', 'f', 'Source: ' + t.notion));
      const v = el('div', 'flex'); v.style.marginTop = '4px';
      for (const [l, c] of [['Keep', 'keep'], ['Cut', 'cut'], ['Merge into a vessel', 'merge']]) { const b = el('button', 'btn', l); b.onclick = async (ev) => { ev.stopPropagation(); const where = await record('toy:' + t.id, c, t.title); b.textContent = l + (where === 'shared' ? ' (saved)' : ' (saved here)'); }; v.appendChild(b); }
      card.appendChild(v);
    }
    card.onclick = () => { loadToy(t.id); };
    card.onkeydown = (ev) => { if (ev.key === 'Enter') card.onclick(); };
    list.appendChild(card);
  }
  box.appendChild(list);
  return box;
}
function loadToy(id) {
  const t = TOYS_BY_ID[id]; if (!t) return;
  Object.assign(BUILD, { toy: id, species: t.species, control: t.control, economy: t.economy, meta: t.meta, scene: t.scene, deck: null });
  saveBuild(); reset(); buildCtrlBar();
  if (typeof refreshDock === 'function') refreshDock();
}

// ---- Build (the workbench) ----
function selectField(label, value, items, onChange, descOf) {
  const f = el('label', 'field'); f.appendChild(el('span', '', label));
  const s = el('select'); for (const it of items) { const o = el('option', '', it.name + (it.source ? '  [' + it.source + ']' : '')); o.value = it.id; s.appendChild(o); } s.value = value;
  const d = el('p', 'desc', descOf(value)); s.onchange = () => { onChange(s.value); d.textContent = descOf(s.value); };
  f.appendChild(s); f.appendChild(d); return f;
}
function buildPanel() {
  const box = el('div');
  box.appendChild(el('p', 'hint', 'A swarm vessel is a combination. Change any axis; the others hold. Changing anything restarts the arena.'));
  const change = (k) => (v) => { BUILD[k] = v; BUILD.toy = null; if (k === 'species') BUILD.deck = null; saveBuild(); reset(); buildCtrlBar(); box.replaceWith(buildPanel()); };
  box.appendChild(selectField('Body (species and its verb)', BUILD.species, SPECIES, change('species'), (v) => { const s = SPECIES_BY_ID[v]; return s.fun + ' Verb: ' + (s.verbs ? s.verbs.join(', ') : s.verb) + '; second verb: ' + s.alt + '. Role guess: ' + s.role + '.'; }));
  box.appendChild(selectField('Control', BUILD.control, CONTROLS, change('control'), (v) => CONTROLS_BY_ID[v].how));
  box.appendChild(selectField('Economy', BUILD.economy, ECONOMIES, change('economy'), (v) => ECONOMIES_BY_ID[v].how));
  box.appendChild(selectField('Meta', BUILD.meta, METAS, change('meta'), (v) => METAS_BY_ID[v].how));
  box.appendChild(selectField('Arena', BUILD.scene, SCENES, change('scene'), (v) => SCENES_BY_ID[v].how));
  if (BUILD.meta === 'deck') {
    box.appendChild(el('h2', 'sec', 'Deck (pick 8)'));
    const deck = (BUILD.deck || SPECIES_BY_ID[BUILD.species].deck || DEFAULT_DECK).slice(0, 8), grid = el('div', 'chips');
    for (const c of CARDS) {
      const lab = el('label', 'chip'); const cb = el('input'); cb.type = 'checkbox'; cb.checked = deck.includes(c.id);
      cb.onchange = () => { const d = new Set(BUILD.deck || deck); if (cb.checked) d.add(c.id); else d.delete(c.id); const arr = [...d]; while (arr.length < 4) arr.push(DEFAULT_DECK.find((x) => !arr.includes(x))); BUILD.deck = arr.slice(0, 8); saveBuild(); reset(); buildCtrlBar(); box.replaceWith(buildPanel()); };
      lab.appendChild(cb); lab.appendChild(document.createTextNode(' ' + c.name + ' ' + c.cost)); lab.title = c.desc + ' — ' + c.src; grid.appendChild(lab);
    }
    box.appendChild(grid);
  }
  if (BUILD.meta === 'castes') {
    box.appendChild(el('h2', 'sec', 'Caste ratio'));
    for (const k of ['worker', 'soldier', 'scout']) {
      const row = el('div', 'row'); row.innerHTML = '<label><span></span></label><output></output><input type="range" min="0" max="1" step="0.05">';
      row.querySelector('span').textContent = { worker: 'Workers (the species verb)', soldier: 'Soldiers (sting)', scout: 'Scouts (ping + fetch)' }[k];
      const inp = row.querySelector('input'), out = row.querySelector('output'); inp.value = BUILD.castes[k]; out.textContent = BUILD.castes[k];
      inp.oninput = () => { BUILD.castes[k] = parseFloat(inp.value); out.textContent = inp.value; saveBuild(); W.build.castes = BUILD.castes; };
      box.appendChild(row);
    }
  }
  box.appendChild(el('h2', 'sec', 'This combination'));
  const fp = el('div'); const run = el('button', 'btn primary', 'Fingerprint it (4 games, AI commander)'); const out = el('div', 'tblWrap');
  run.onclick = () => { run.disabled = true; run.textContent = 'Scoring…'; setTimeout(() => { const rows = runBatch({ seconds: 15 }); out.replaceChildren(fingerprintTable([{ label: 'This build', rows }])); run.disabled = false; run.textContent = 'Fingerprint it again'; }, 30); };
  fp.appendChild(run); fp.appendChild(out); box.appendChild(fp);
  return box;
}

// ---- Tune ----
function tunePanel() {
  const box = el('div');
  for (const group of [...new Set(SPEC.map((r) => r[0]))]) {
    box.appendChild(el('h2', 'sec', group));
    for (const [gname, key, lo, hi, step, label, tip] of SPEC) {
      if (gname !== group) continue;
      const row = el('div', 'row'); row.innerHTML = '<label><span></span><span class="key"></span></label><output></output><input type="range"><div class="tip"></div>';
      row.querySelector('label span').textContent = label; row.querySelector('.key').textContent = key; row.querySelector('.tip').textContent = tip;
      const inp = row.querySelector('input'); Object.assign(inp, { min: lo, max: hi, step, value: P[key] });
      inp.oninput = () => setParam(key, parseFloat(inp.value));
      outputs[key] = { out: row.querySelector('output'), inp };
      box.appendChild(row);
    }
  }
  queueMicrotask(refreshDiff);
  return box;
}

// ---- Scorecard + Team ----
// A fingerprint is a build's score in each element game, normalised against two baselines that are measured
// in the same pass: "Hull only" (the same arena with no swarm at all) and the best build in the table.
let SCORES = lsGet('lab:' + LAB.name + ':scores', null);
function normalise(sets) {
  const base = sets.find((s) => s.label === 'Hull only'), out = {};
  for (const g of GAMES) {
    const b = base ? (base.rows.find((r) => r.game === g) || { score: 0 }).score : 0;
    const top = Math.max(b + 1, ...sets.map((s) => (s.rows.find((r) => r.game === g) || { score: 0 }).score));
    out[g] = { base: b, top };
  }
  return out;
}
function roleOf(n) {
  const names = { time: 'Runner', space: 'Breaker', mass: 'Builder', charge: 'Hunter' };
  const sorted = GAMES.slice().sort((a, b) => n[b] - n[a]);
  if (n[sorted[0]] < 15) return 'Passenger';
  return names[sorted[0]] + (n[sorted[1]] > 0.7 * n[sorted[0]] && n[sorted[1]] > 25 ? ' / ' + names[sorted[1]] : '');
}
function fingerprintTable(sets) {
  const norm = normalise(sets), t = el('table');
  const hr = el('tr'); for (const h of ['Build', 'Time', 'Space', 'Mass', 'Charge', 'Role']) hr.appendChild(el('th', '', h)); t.appendChild(hr);
  const best = {}; for (const g of GAMES) best[g] = Math.max(...sets.map((s) => (s.rows.find((r) => r.game === g) || { score: -1e9 }).score));
  for (const s of sets) {
    const tr = el('tr'); tr.appendChild(el('td', '', s.label)); const n = {};
    for (const g of GAMES) {
      const r = s.rows.find((x) => x.game === g) || { score: 0 }, nb = norm[g];
      n[g] = Math.round(100 * (r.score - nb.base) / Math.max(1, nb.top - nb.base));
      const td = el('td', r.score === best[g] && sets.length > 1 ? 'best' : '', r.score + '  (' + n[g] + ')'); td.title = 'raw score (normalised 0-100 against Hull only and the best build)'; tr.appendChild(td);
    }
    tr.appendChild(el('td', 'role', s.label === 'Hull only' ? 'baseline' : roleOf(n)));
    s.norm = n; t.appendChild(tr);
  }
  return t;
}
function speciesBuilds() {
  return SPECIES.map((s) => ({ label: s.name, species: s.id, control: s.control, economy: s.economy, meta: s.meta, deck: null }));
}
function scorecardPanel() {
  const box = el('div');
  box.appendChild(el('p', 'hint', 'Every species in its default build, played by the AI commander in all four element games (15 s each, seed 1). "Hull only" is the same arena with no swarm, so a swarm that adds nothing scores 0. The rookie row re-runs the loaded build with the AI at full noise: a scheme whose rookie score collapses is a scheme that needs skill.'));
  const run = el('button', 'btn primary', 'Score every species'), status = el('span', 'hint'), out = el('div', 'tblWrap');
  const row = el('div', 'flex'); row.appendChild(run); row.appendChild(status); box.appendChild(row); box.appendChild(out);
  const show = () => { if (SCORES) out.replaceChildren(fingerprintTable(SCORES)); };
  run.onclick = () => {
    const builds = [{ label: 'Hull only', species: BUILD.species, control: 'focus', economy: 'fixed', meta: 'none', P: { pop: 0 } }].concat(speciesBuilds());
    const cur = Object.assign({}, BUILD, { label: 'Loaded build, rookie AI', P: { aiNoise: 1 } });
    builds.push(Object.assign({}, BUILD, { label: 'Loaded build, expert AI', P: { aiNoise: 0 } }), cur);
    const sets = []; let i = 0; run.disabled = true;
    (function next() {
      if (i >= builds.length) { SCORES = sets; lsSet('lab:' + LAB.name + ':scores', sets); status.textContent = 'Done — ' + sets.length + ' builds.'; run.disabled = false; show(); return; }
      status.textContent = 'Scoring ' + builds[i].label + ' (' + (i + 1) + '/' + builds.length + ')…';
      sets.push({ label: builds[i].label, species: builds[i].species, rows: runBatch({ builds: [builds[i]], seconds: 15 }) }); i++;
      setTimeout(next, 0);
    })();
  };
  show();
  return box;
}
function teamPanel() {
  const box = el('div');
  box.appendChild(el('p', 'hint', 'Pick three swarm vessels for one team. The bars sum their normalised fingerprints (from the Scorecard), so you can see which element games the team covers and where two vessels are doing the same job.'));
  if (!SCORES) { box.appendChild(el('p', 'status-warn', 'Run "Score every species" on the Scorecard tab first.')); return box; }
  const sets = SCORES.filter((s) => s.label !== 'Hull only' && !/Loaded build/.test(s.label)); fingerprintTable(SCORES);
  const team = lsGet('lab:' + LAB.name + ':team', [sets[0].label, sets[3] ? sets[3].label : sets[0].label, sets[6] ? sets[6].label : sets[0].label]);
  const picks = el('div', 'grid2'), result = el('div');
  const draw = () => {
    const sum = { time: 0, space: 0, mass: 0, charge: 0 }, roles = [];
    for (const lbl of team) { const s = sets.find((x) => x.label === lbl); if (!s) continue; for (const g of GAMES) sum[g] += Math.max(0, s.norm[g]); roles.push(roleOf(s.norm)); }
    result.replaceChildren();
    const t = el('table'); const tr = el('tr'); for (const h of ['Time', 'Space', 'Mass', 'Charge']) tr.appendChild(el('th', '', h)); t.appendChild(tr);
    const vr = el('tr'); for (const g of GAMES) { const td = el('td'); const bar = el('span', 'spark'); const i = el('i'); i.style.height = '10px'; i.style.width = Math.min(160, sum[g] * 0.8) + 'px'; i.style.background = 'var(--accent)'; bar.appendChild(i); td.appendChild(bar); td.appendChild(document.createTextNode(' ' + sum[g])); vr.appendChild(td); } t.appendChild(vr);
    result.appendChild(t);
    const dup = roles.filter((r, i) => roles.indexOf(r) !== i);
    const gaps = GAMES.filter((g) => sum[g] < 40).map((g) => ({ time: 'Time', space: 'Space', mass: 'Mass', charge: 'Charge' })[g]);
    result.appendChild(el('p', 'note', 'Roles: ' + roles.join(', ') + (dup.length ? '. Overlap: ' + [...new Set(dup)].join(', ') + '.' : '.') + (gaps.length ? ' Uncovered: ' + gaps.join(', ') + '.' : ' Every element game covered.')));
  };
  for (let k = 0; k < 3; k++) {
    const s = el('select'); for (const x of sets) { const o = el('option', '', x.label); o.value = x.label; s.appendChild(o); } s.value = team[k];
    s.onchange = () => { team[k] = s.value; lsSet('lab:' + LAB.name + ':team', team); draw(); };
    const f = el('label', 'field'); f.appendChild(el('span', '', 'Vessel ' + (k + 1))); f.appendChild(s); picks.appendChild(f);
  }
  box.appendChild(picks); box.appendChild(result); draw();
  const save = el('button', 'btn', 'Record this team'); save.onclick = async () => { const w = await record('team', team.join(' + '), result.textContent); save.textContent = w === 'shared' ? 'Recorded (shared)' : 'Recorded (this browser)'; };
  box.appendChild(save);
  return box;
}

// ---- Decision log ----
let db = null, me = '', remote = [], localLog = lsGet('lab:' + LAB.name + ':log', []);
function snapshot() { return { round: LAB.round, seed, build: Object.assign({}, BUILD), changed: diffOnly() }; }
async function record(topic, choice, note) {
  const doc = { topic, choice, note: note || '', createdAt: Date.now(), by: me, settings: snapshot() };
  if (db) { try { await db.collection('decisions').add(doc); return 'shared'; } catch (e) { /* fall through to local */ } }
  localLog.push(doc); lsSet('lab:' + LAB.name + ':log', localLog); renderLog(); return 'local';
}
function logPanel() {
  const box = el('div');
  box.innerHTML = '<p class="hint" id="logStatus"></p><input type="text" id="logTopic" placeholder="Topic (e.g. Which swarm is the Builder?)"><textarea id="logChoice" rows="2" placeholder="Your decision"></textarea><p><button class="btn primary" id="logSave">Record decision</button> <button class="btn" id="logCopy">Copy log</button></p><div class="tblWrap"><table id="logTable"></table></div>';
  const status = box.querySelector('#logStatus');
  status.textContent = db ? 'Shared log: saved with the lab, readable by Claude next round.' : 'Kept in this browser (use Copy log to send it).';
  status.className = db ? 'status-ok' : 'status-warn';
  box.querySelector('#logChoice').value = 'Build: ' + BUILD.species + ' / ' + BUILD.control + ' / ' + BUILD.economy + ' / ' + BUILD.meta;
  box.querySelector('#logSave').onclick = async () => { const where = await record(box.querySelector('#logTopic').value || 'General', box.querySelector('#logChoice').value); status.textContent = where === 'shared' ? 'Recorded in the shared log.' : 'Recorded in this browser.'; renderLog(box); };
  box.querySelector('#logCopy').onclick = () => { const t = remote.concat(localLog).map((d) => new Date(d.createdAt).toISOString() + ' · ' + d.topic + ': ' + d.choice + ' ' + JSON.stringify(d.settings)).join('\n'); if (navigator.clipboard) navigator.clipboard.writeText(t).catch(() => {}); };
  renderLog(box);
  return box;
}
function renderLog(box) {
  const t = (box || document).querySelector('#logTable'); if (!t) return;
  const rows = remote.concat(localLog).sort((a, b) => b.createdAt - a.createdAt).slice(0, 60);
  t.innerHTML = '<tr><th>When</th><th>Topic</th><th>Decision</th></tr>';
  for (const d of rows) { const tr = el('tr'); for (const v of [new Date(d.createdAt).toLocaleString(), d.topic, d.choice]) tr.appendChild(el('td', '', v)); t.appendChild(tr); }
}
(async function connect() {
  const c = window.claude;
  if (!c || typeof c.use !== 'function') return;
  try { db = await c.use('db'); } catch (e) { db = null; }
  try { const u = await c.use('user'); if (u && u.id) me = (await u.id()) || ''; } catch (e) { me = ''; }
  if (!db) return;
  try { db.collection('decisions').orderBy('createdAt', 'desc').limit(300).onSnapshot((snap) => { remote = snap.docs.map((d) => Object.assign({ id: d.id }, d.data())); renderLog(); }, () => { db = null; }); } catch (e) { db = null; }
})();

// ---- About ----
function aboutPanel() {
  const box = el('div');
  box.appendChild(el('p', '', LAB.name + ' · round ' + LAB.round + ' · ' + LAB.branch + ' · write-up: ' + LAB.doc));
  const g = el('div', 'grid2');
  const sec = (title, items) => { const d = el('div'); d.appendChild(el('h2', 'sec', title)); const u = el('ul'); for (const i of items) u.appendChild(el('li', 'hint', i)); d.appendChild(u); return d; };
  g.appendChild(sec('How to read it', [
    'A swarm vessel = a BODY (species + verb) x a CONTROL x an ECONOMY x a META. The Build tab swaps one axis at a time.',
    'Controls only ever write a focus point, an order, a mood or a selection. Members decide their own moves — so a scheme is a question of what you get to say, not of steering bodies.',
    'The four arenas are the element minigames: Time = race, Space = destruction, Mass = creation, Charge = dogfight (petals). A build\'s scores across them are its team role.',
    'Watch AI lets an expert commander drive whatever scheme is loaded; the Scorecard runs it headless, including a rookie run.',
  ]));
  g.appendChild(sec('Where the ideas came from', [
    'Shipped: the Termite\'s six cards and pheromone tank, the tadpole boid, the locust\'s phase rules (Tools/Ecology bestiary).',
    'Notion: the School (six fish bound by plasma), D-pad fauna orders, Shepherd / sheepdog herding, Boldt\'s tiered fuel, the Shuffle octopus, the Angler\'s fake crystal, Beast Tamer bait, the 2024 termite commander (cell vs queen), the NCA backlog\'s aggression knob (B5), slime-mold and siphonophore concepts.',
    'Lab: ants, pigeons, rats, bees, wasps, starlings, bats, fireflies, mosquitoes, crows, piranhas, spiders — the docs name few of these, so their verbs are proposals.',
  ]));
  g.appendChild(sec('What this does NOT model', [
    '2D, top-down. The game is 3D: a swarm that surrounds a ship in a sphere reads (and defends) very differently from a ring.',
    'Member counts here (6-60) are far below what the GPU could draw and far above what the collider budget allows if every member were a game lifeform. A shipped swarm needs the drone path (vessel agents, not lifeforms), and its collider cost is not modelled.',
    'Rivals are simple scripted ships, not other players. Petal transfer follows the elemental economy\'s rule (whole petals move; nothing partial leaves) but not its full crystal ejection.',
    'Networking is absent: no replication cost, no ownership, no latency on orders.',
    'No gamepad. Touch, mouse and keyboard only.',
    'Prism mass here is a dot with one hit point, no volume, no growth. Mass scores count prisms, not volume.',
    'The AI commander is a heuristic, not a skilled player. Its scores rank schemes coarsely; treat a 10% gap as noise.',
  ]));
  box.appendChild(g);
  return box;
}

// ================= wiring =================
const showDock = makeTabs('dockTabs', 'dockBody', [{ name: 'Toys', build: toysPanel }, { name: 'Build', build: buildPanel }, { name: 'Tune', build: tunePanel }], 'lab:' + LAB.name + ':dock');
function refreshDock() { const cur = [...$('dockTabs').children].find((b) => b.getAttribute('aria-selected') === 'true'); if (cur) showDock(cur.dataset.name); }
makeTabs('bottomTabs', 'bottomBody', [{ name: 'Scorecard', build: scorecardPanel }, { name: 'Team', build: teamPanel }, { name: 'Decisions', build: logPanel }, { name: 'About', build: aboutPanel }], 'lab:' + LAB.name + ':bottom');
$('title').textContent = LAB.name; $('meta').textContent = 'round ' + LAB.round + ' · ' + TOYS.length + ' toys · ' + SPECIES.length + ' species';
let paused = false;
function togglePause() { paused = !paused; $('pause').textContent = paused ? 'Resume' : 'Pause'; }
$('pause').onclick = togglePause;
$('restart').onclick = () => { reset(); buildCtrlBar(); };
$('resetAll').onclick = () => { Object.assign(P, SHIPPED); lsSet(LS_KEY, {}); for (const k in outputs) if (outputs[k].inp) outputs[k].inp.value = P[k]; reset(); refreshDiff(); };

addEventListener('resize', resize);
let manualClock = false, last = performance.now();
function frame(now) {
  const dt = Math.min(0.05, Math.max(0, (now - last) / 1000)); last = now;
  if (!manualClock) { resize(); if (!paused) simStep(dt); render(); }
  requestAnimationFrame(frame);
}
if (BUILD.toy && !TOYS_BY_ID[BUILD.toy]) BUILD.toy = null;
reset(); buildCtrlBar(); resize(); refreshDiff(); render(); requestAnimationFrame(frame);

// ================= the test surface =================
window.__lab = {
  LAB, SHIPPED, SPEC, P, PLATFORM, SPECIES, CONTROLS, ECONOMIES, METAS, CARDS, TOYS,
  set manualClock(v) { manualClock = !!v; }, get manualClock() { return manualClock; },
  tick(dt, n) { n = n || 1; for (let i = 0; i < n; i++) simStep(dt); resize(); hudFrame = 0; render(); },
  reset: (s) => { reset(s); buildCtrlBar(); }, score, runBatch, setParam, record, loadToy,
  input: (o) => Object.assign(liveInput, o),
  state: () => ({ t: W.t, seed, build: Object.assign({}, BUILD), members: liveAgents().length, prisms: W.prisms.filter((p) => p.alive).length, score: gameScore(), done: W.done }),
};
