// The Loop Atlas runtime. Source: Docs/LoopAtlas/src/atlas.js. Built into the artifact's bundle.js by
// Tools/Build/loop_atlas.py, which puts the resolved model where MODEL_JSON stands. Plain JS, no libraries.
(function () {
  'use strict';
  var M = MODEL_JSON;

  var LETTER = { now: 'n', plan: 'p', prop: 'x' };
  var ELS = ['Time', 'Charge', 'Mass', 'Space', 'Omni'];
  var TMIN = Math.log10(0.1), TMAX = Math.log10(31536000);

  // ---------- helpers ----------
  function has(o, L) { return (o.in || 'npx').indexOf(L) >= 0; }
  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }
  function fmtT(s) {
    if (s < 1) return (Math.round(s * 10) / 10) + ' s';
    if (s < 90) return Math.round(s) + ' s';
    if (s < 5400) return Math.round(s / 60) + ' min';
    if (s < 86400) return (Math.round(s / 360) / 10) + ' h';
    if (s < 1209600) return Math.round(s / 86400) + ' d';
    if (s < 5184000) return Math.round(s / 604800) + ' wk';
    return Math.round(s / 2592000) + ' mo';
  }
  function fmtSpan(t) {
    if (!t) return '';
    var a = fmtT(t[0]), b = fmtT(t[1]);
    if (a === b) return a;
    var ua = a.split(' ')[1], ub = b.split(' ')[1];
    return ua === ub ? a.split(' ')[0] + ' to ' + b : a + ' to ' + b;
  }
  function tx(t) { return (Math.log10(Math.max(0.1, t)) - TMIN) / (TMAX - TMIN); }
  function tierOf(n) { return M.tiers[n.tier]; }
  function termOf(n) { return n.kind === 'kit' || n.kind === 'vessel' ? 'Kit' : M.tiers[n.tier].term; }
  function driver(id) { for (var i = 0; i < M.drivers.length; i++) if (M.drivers[i].id === id) return M.drivers[i]; return null; }
  function handoff(id) { for (var i = 0; i < M.handoffs.length; i++) if (M.handoffs[i].id === id) return M.handoffs[i]; return null; }

  // ---------- the tree for one layer and one vessel ----------
  function holesFor(list, L) {
    var out = [];
    (list || []).forEach(function (h) {
      if (L === 'x') { if (h.on.indexOf('p') >= 0) out.push(h); }
      else if (h.on.indexOf(L) >= 0) out.push(h);
    });
    return out;
  }

  function kitChildren(host, V, L, lead) {
    var f = M.fleet[V], out = [];
    var p = M.pulse;
    out.push({ id: host.id + '/pulse', name: p.name, tier: 0, kind: 'pulse', t: p.t, is: p.is, phases: p.phases, drivers: p.drivers,
      holes: holesFor(p.holes, L), src: [f.doc || 'Docs/FleetCodex/abilities.json'], children: [], vessel: V });
    ELS.forEach(function (E) {
      var v = f.verbs[E]; if (!v) return;
      var hand = v.hand && has(v.hand, L) ? v.hand : null;
      var arch = M.archetypes[v.arch] || M.archetypes.tap;
      var phases, feel = null, feelAt = null, holes = [];
      if (hand) { phases = hand.phases; feel = hand.feel; feelAt = hand.at; }
      else if (L === 'x') { phases = arch.phases; feel = arch.feel; feelAt = arch.at; }
      else phases = ['Trigger: ' + (v.trigger || 'not stated'), 'The effect', 'Ready again'];
      if (!hand && v.arch !== 'off') {
        holes.push({ on: 'np', q: M.verbHole.q, fill: M.verbHole.fillPrefix + arch.phases.join(', then ') + '. The feeling: ' + arch.feel });
      }
      out.push({ id: host.id + '/verb/' + E, name: v.ability, tier: 1, kind: 'verb', element: E, t: v.t, is: v.does,
        phases: phases.map(function (s) { return { n: s }; }), drivers: M.elementDrivers[E] || [], feel: feel, feelAt: feelAt,
        trigger: v.trigger, scales: v.scales, lead: lead.indexOf(E) >= 0, invented: !hand && L === 'x',
        holes: holesFor(holes, L), src: [v.source || 'Docs/FleetCodex/abilities.json'], children: [], vessel: V,
        board: f.board[E === 'Omni' ? '' : E], games: f.games[E] });
    });
    (f.combos || []).forEach(function (c, i) {
      var cin = c.in || 'x'; if (cin.indexOf(L) < 0) return;
      out.push({ id: host.id + '/combo/' + i, name: c.name, tier: 2, kind: 'combo', t: c.t, is: c.is,
        phases: c.chain.map(function (s) { return { n: s[1], e: s[0] }; }), drivers: c.drivers || [], feel: c.feel,
        invented: cin === 'x', holes: [], src: [c.src || 'invented from the kit text (proposal P3)'], children: [], vessel: V,
        propS: cin === 'x' ? 'add' : null });
    });
    return out;
  }

  function buildTree(layer, vessel) {
    var L = LETTER[layer], byId = {}, root = null, list = [];
    M.nodes.forEach(function (s) {
      var ghost = false;
      if (!has(s, L)) {
        if (L === 'n' && has(s, 'p')) ghost = true; else return;
      }
      var n = { id: s.id, name: s.name, tier: s.tier, kind: s.kind, t: s.t, is: s.is, phases: s.phases || [], drivers: s.drivers || [],
        reps: s.reps, repsOf: s.repsOf, at: s.at, core: !!s.core, ghost: ghost, kitKind: s.kit, element: s.element,
        now: s.now, plan: s.plan, prop: s.prop, inL: s.in || 'npx', holes: holesFor(s.holes, L), src: s.src || [], children: [], parentId: s.parent };
      byId[n.id] = n; list.push(n);
    });
    list.forEach(function (n) {
      if (n.parentId && byId[n.parentId]) { byId[n.parentId].children.push(n); n.parent = byId[n.parentId]; }
      else if (!n.parentId) root = n;
    });
    // kits
    list.slice().forEach(function (n) {
      if (n.kitKind) {
        var r = M.roles[n.kitKind];
        var lead = n.element === 'Mass-Space' ? ['Mass', 'Space'] : n.element ? [n.element] : [];
        var role = { id: n.id + '/role', name: r.name, tier: 3, kind: 'role', t: r.t, is: r.is, phases: r.phases, drivers: r.drivers,
          reps: r.reps, repsOf: r.repsOf, core: true, holes: holesFor(n.kitKind === 'free' ? [] : (n.kitKind === 'ms' || n.kitKind === 'cell' ? M.roleHoles : [M.roleHoles[0]]), L),
          src: ['Docs/ElementalLoop/PLAYBOOK.md section 1'], children: [], parent: n, roleKind: n.kitKind, vessel: vessel, ghost: n.ghost };
        var f = M.fleet[vessel];
        var hasCombos = (f.combos || []).some(function (c) { return (c.in || 'x').indexOf(L) >= 0; });
        var kit = { id: n.id + '/kit', name: f.label + ' kit', tier: 2, kind: 'kit', t: [8, 60], is: M.kit.is, phases: [], drivers: ['joy', 'mastery'],
          core: true, holes: L !== 'x' && hasCombos ? [] : holesFor(M.kit.holes, L), src: [f.doc || 'Docs/FleetCodex/abilities.json'], children: [], parent: role, vessel: vessel, ghost: n.ghost };
        kit.children = kitChildren(kit, vessel, L, lead);
        kit.children.forEach(function (c) { c.parent = kit; c.ghost = n.ghost; });
        role.children = [kit]; n.children.push(role);
      }
      if (n.kind === 'fleet') {
        M.fleetOrder.forEach(function (V) {
          var f = M.fleet[V];
          var hasCombos = (f.combos || []).some(function (c) { return (c.in || 'x').indexOf(L) >= 0; });
          var vn = { id: 'fleet/' + V, name: f.label, tier: 2, kind: 'vessel', t: [8, 60], is: f.is || M.kit.is, phases: [], drivers: ['joy', 'mastery'],
            holes: L !== 'x' && hasCombos ? [] : holesFor(M.kit.holes, L), src: [f.doc || 'Docs/FleetCodex/abilities.json'], children: [], parent: n, vessel: V };
          vn.children = kitChildren(vn, V, L, []);
          vn.children.forEach(function (c) { c.parent = vn; });
          n.children.push(vn);
        });
      }
    });
    // index, depth, aggregate hole counts
    var all = {};
    // A ring's count when its insides are too small to see. A kit counts once however many of its verbs are
    // unwritten, and a Round does not count the role and kit inside it: the same kit is counted under The fleet.
    (function walk(n, d) {
      n.depth = d; all[n.id] = n; n.holeSum = openHoles(n, L);
      var inner = 0;
      n.children.forEach(function (c) { walk(c, d + 1); if (c.kind !== 'role') inner += c.holeSum; });
      if (n.kind === 'kit' || n.kind === 'vessel') n.holeSum = n.holeSum + inner > 0 ? 1 : 0;
      else if (n.kind !== 'role') n.holeSum += inner;
    })(root, 0);
    root.x = 0; root.y = 0; root.r = 1; layout(root);
    return { root: root, all: all, layer: layer, L: L, vessel: vessel };
  }
  function openHoles(n, L) {
    if (L !== 'x') return n.holes.length;
    return n.holes.filter(function (h) { return !h.fill; }).length;
  }

  // ---------- layout: children inside the parent's ring ----------
  function phaseAngle(n, i) { var m = Math.max(1, n.phases.length); return -Math.PI / 2 + 2 * Math.PI * i / m; }
  function orbit(list, n, limit, inner) {
    var k = list.length; if (!k) return;
    var R = n.r, s = Math.sin(Math.PI / Math.max(k, 2)), rho, r;
    if (k === 1) { r = (limit - inner) / 2 * 0.92; rho = (limit + inner) / 2; if (inner === 0) { rho = 0; r = limit * 0.66; } }
    else {
      rho = limit / (1 + 0.88 * s); r = 0.88 * s * rho;
      if (rho - r < inner + 0.03) { r = Math.min(r, (limit - inner - 0.03) / 2); rho = limit - r; r = Math.min(r, 0.88 * s * rho); }
    }
    // rotate so each child sits near the phase it opens
    var best = 0, bestCost = Infinity;
    for (var o = 0; o < 48; o++) {
      var th0 = -Math.PI / 2 + o * Math.PI / 24, cost = 0;
      for (var i = 0; i < k; i++) {
        if (list[i].at == null) { cost += Math.abs(o) * 1e-3; continue; }
        var d = th0 + 2 * Math.PI * i / k - phaseAngle(n, list[i].at);
        d = Math.atan2(Math.sin(d), Math.cos(d)); cost += d * d;
      }
      if (cost < bestCost - 1e-9) { bestCost = cost; best = th0; }
    }
    list.forEach(function (c, i) {
      var th = best + 2 * Math.PI * i / k;
      c.x = n.x + Math.cos(th) * rho * R; c.y = n.y + Math.sin(th) * rho * R; c.r = r * R;
    });
    return { rho: rho, r: r };
  }
  function layout(n) {
    var ch = n.children; if (!ch.length) return;
    var core = null, rest = [];
    ch.forEach(function (c) { if (c.core && !core) core = c; else rest.push(c); });
    rest.sort(function (a, b) { return (a.at == null ? 99 : a.at) - (b.at == null ? 99 : b.at); });
    if (n.kind === 'kit' || n.kind === 'vessel') rest = ch.slice();
    var LIM = 0.84;
    if (core && !rest.length) { core.x = n.x; core.y = n.y + n.r * 0.07; core.r = n.r * 0.6; }
    else if (core) { core.x = n.x; core.y = n.y; core.r = n.r * 0.36; orbit(rest, n, LIM, 0.36); }
    else if (rest.length > 12) {
      var k1 = Math.ceil(rest.length * 0.61);
      var o1 = orbit(rest.slice(0, k1), n, LIM, 0);
      orbit(rest.slice(k1), n, o1.rho - o1.r - 0.03, 0);
    } else orbit(rest, n, LIM, 0);
    n.centerFree = !core && rest.length >= 3 && rest.length <= 12;
    ch.forEach(layout);
  }

  // ---------- mount ----------
  function mount(host, opts) {
    opts = opts || {};
    var S = {
      layer: opts.layer || 'now', vessel: opts.vessel || M.defaultVessel, view: opts.view || 'map',
      sel: opts.select || 'era', filter: null, cam: { x: 0, y: 0, k: 200 }, tree: null, anim: null, w: 0, h: 0, dpr: 1, colors: {}
    };
    host.innerHTML = '';
    var root = el('div', 'la'); host.appendChild(root);
    if (opts.chrome === false) root.classList.add('la-bare');

    // top bar
    var bar = el('div', 'la-bar'); root.appendChild(bar);
    var segL = el('div', 'la-seg'); segL.setAttribute('role', 'group'); segL.setAttribute('aria-label', 'Layer'); bar.appendChild(segL);
    var layerBtns = {};
    M.layers.forEach(function (l) {
      var b = el('button', 'la-btn', l.name); b.type = 'button'; b.title = l.is;
      b.addEventListener('click', function () { setLayer(l.id); });
      layerBtns[l.id] = b; segL.appendChild(b);
    });
    var segV = el('div', 'la-seg'); segV.setAttribute('role', 'group'); segV.setAttribute('aria-label', 'View'); bar.appendChild(segV);
    var viewBtns = {};
    [['map', 'Map'], ['ladder', 'Timescales'], ['drivers', 'Drivers'], ['holes', 'Holes']].forEach(function (v) {
      var b = el('button', 'la-btn', v[1]); b.type = 'button';
      b.addEventListener('click', function () { setView(v[0]); });
      viewBtns[v[0]] = b; segV.appendChild(b);
    });
    var vz = el('div', 'la-vessel'); bar.appendChild(vz);
    var vPrev = el('button', 'la-btn la-ico', '‹'); vPrev.type = 'button'; vPrev.setAttribute('aria-label', 'Previous vessel');
    var vSel = el('select', 'la-select'); vSel.setAttribute('aria-label', 'Vessel shown in every kit');
    M.fleetOrder.forEach(function (V) { var o = el('option', null, M.fleet[V].label); o.value = V; vSel.appendChild(o); });
    var vNext = el('button', 'la-btn la-ico', '›'); vNext.type = 'button'; vNext.setAttribute('aria-label', 'Next vessel');
    var vLab = el('span', 'la-lab', 'Vessel');
    vz.appendChild(vLab); vz.appendChild(vPrev); vz.appendChild(vSel); vz.appendChild(vNext);
    function stepVessel(d) { var i = M.fleetOrder.indexOf(S.vessel); setVessel(M.fleetOrder[(i + d + M.fleetOrder.length) % M.fleetOrder.length]); }
    vPrev.addEventListener('click', function () { stepVessel(-1); });
    vNext.addEventListener('click', function () { stepVessel(1); });
    vSel.addEventListener('change', function () { setVessel(vSel.value); });
    var find = el('input', 'la-find'); find.type = 'search'; find.placeholder = 'Find a loop'; find.setAttribute('aria-label', 'Find a loop');
    var dl = el('datalist'); dl.id = 'la-dl-' + Math.random().toString(36).slice(2); find.setAttribute('list', dl.id);
    bar.appendChild(find); bar.appendChild(dl);
    find.addEventListener('change', function () {
      var q = find.value.trim().toLowerCase(); if (!q) return;
      var hit = null;
      Object.keys(S.tree.all).some(function (id) { var n = S.tree.all[id]; if (n.name.toLowerCase() === q) { hit = n; return true; } return false; });
      if (!hit) Object.keys(S.tree.all).some(function (id) { var n = S.tree.all[id]; if (n.name.toLowerCase().indexOf(q) >= 0) { hit = n; return true; } return false; });
      if (hit) { setView('map'); select(hit.id, true); find.value = ''; }
    });

    // body
    var body = el('div', 'la-body'); root.appendChild(body);
    var stage = el('div', 'la-stage'); body.appendChild(stage);
    var crumbs = el('nav', 'la-crumbs'); crumbs.setAttribute('aria-label', 'Where this loop sits'); stage.appendChild(crumbs);
    var wrap = el('div', 'la-canvas'); stage.appendChild(wrap);
    var cv = el('canvas'); cv.tabIndex = 0; cv.setAttribute('role', 'img');
    cv.setAttribute('aria-label', 'Map of gameplay loops, nested by timescale. Use the Timescales, Drivers and Holes views for the same content as lists.');
    wrap.appendChild(cv);
    var hud = el('div', 'la-hud'); wrap.appendChild(hud);
    function hudBtn(t, lab, fn) { var b = el('button', 'la-btn la-ico', t); b.type = 'button'; b.setAttribute('aria-label', lab); b.title = lab; b.addEventListener('click', fn); hud.appendChild(b); return b; }
    hudBtn('+', 'Zoom in', function () { zoomBy(1.6); });
    hudBtn('−', 'Zoom out', function () { zoomBy(1 / 1.6); });
    hudBtn('↑', 'Step up to the loop above', function () { var n = cur(); if (n && n.parent) select(n.parent.id, true); });
    hudBtn('⌂', 'The whole map', function () { select('era', true); });
    var fSel = el('select', 'la-select la-filter'); fSel.setAttribute('aria-label', 'Show only loops with this driver');
    var o0 = el('option', null, 'All drivers'); o0.value = ''; fSel.appendChild(o0);
    M.drivers.forEach(function (d) { var o = el('option', null, d.name); o.value = d.id; fSel.appendChild(o); });
    fSel.addEventListener('change', function () { S.filter = fSel.value || null; draw(); if (S.view !== 'map') renderTable(); });
    hud.appendChild(fSel);
    var legend = el('div', 'la-legend'); wrap.appendChild(legend);
    var table = el('div', 'la-table'); stage.appendChild(table);
    var scale = el('div', 'la-scale'); stage.appendChild(scale);
    var insp = el('aside', 'la-insp'); insp.setAttribute('aria-live', 'polite'); body.appendChild(insp);

    var ctx = cv.getContext('2d');

    // ---------- state changes ----------
    function cur() { return S.tree.all[S.sel] || S.tree.root; }
    function rebuild() {
      var old = S.sel;
      S.tree = buildTree(S.layer, S.vessel);
      if (!S.tree.all[old]) {
        var id = old; while (id && !S.tree.all[id]) { var k = id.lastIndexOf('/'); id = k > 0 ? id.slice(0, k) : null; }
        S.sel = id || 'era';
      }
      dl.innerHTML = '';
      var seen = {};
      Object.keys(S.tree.all).forEach(function (id) { var n = S.tree.all[id]; if (!seen[n.name]) { seen[n.name] = 1; var o = el('option'); o.value = n.name; dl.appendChild(o); } });
    }
    function setLayer(id) { S.layer = id; rebuild(); syncBar(); renderAll(); focus(cur(), false); }
    function setVessel(V) { S.vessel = V; rebuild(); syncBar(); renderAll(); focus(cur(), false); }
    function setView(v) { S.view = v; syncBar(); renderAll(); if (v === 'map') { resize(); focus(cur(), false); } }
    function select(id, zoom) {
      if (!S.tree.all[id]) return;
      S.sel = id; renderCrumbs(); renderInsp(); renderScale();
      if (S.view !== 'map') markRows();
      if (zoom && S.view === 'map') focus(cur(), true); else draw();
    }
    function syncBar() {
      M.layers.forEach(function (l) { layerBtns[l.id].setAttribute('aria-pressed', l.id === S.layer ? 'true' : 'false'); });
      Object.keys(viewBtns).forEach(function (v) { viewBtns[v].setAttribute('aria-pressed', v === S.view ? 'true' : 'false'); });
      vSel.value = S.vessel; root.setAttribute('data-view', S.view); root.setAttribute('data-layer', S.layer);
    }
    function renderAll() { renderCrumbs(); renderInsp(); renderScale(); renderLegend(); if (S.view === 'map') draw(); else renderTable(); }

    // ---------- camera ----------
    function fitK(n) { return 0.43 * Math.min(S.w, S.h) / n.r; }
    function focus(n, animate) {
      var target = n;
      var tk = fitK(target), tx0 = target.x, ty0 = target.y;
      var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      if (!animate || reduce || !S.w) { S.cam = { x: tx0, y: ty0, k: tk }; S.anim = null; draw(); return; }
      var c0 = { x: S.cam.x, y: S.cam.y, k: S.cam.k }, t0 = performance.now(), dur = 420;
      S.anim = function (now) {
        var u = Math.min(1, (now - t0) / dur), e = 1 - Math.pow(1 - u, 3);
        var k = c0.k * Math.pow(tk / c0.k, e);
        S.cam.k = k;
        S.cam.x = tx0 - (tx0 - c0.x) * c0.k * (1 - e) / k;
        S.cam.y = ty0 - (ty0 - c0.y) * c0.k * (1 - e) / k;
        if (u >= 1) S.anim = null;
      };
      tick();
    }
    var raf = 0;
    function tick() { if (raf) return; raf = requestAnimationFrame(function (now) { raf = 0; if (S.anim) S.anim(now); paint(); if (S.anim) tick(); }); }
    function draw() { tick(); }
    function zoomBy(f, px, py) {
      S.anim = null;
      if (px == null) { px = S.w / 2; py = S.h / 2; }
      var wx = (px - S.w / 2) / S.cam.k + S.cam.x, wy = (py - S.h / 2) / S.cam.k + S.cam.y;
      var k = Math.max(fitK(S.tree.root) * 0.6, Math.min(S.cam.k * f, fitK(S.tree.root) * 4e5));
      S.cam.k = k; S.cam.x = wx - (px - S.w / 2) / k; S.cam.y = wy - (py - S.h / 2) / k; draw();
    }
    function sx(x) { return (x - S.cam.x) * S.cam.k + S.w / 2; }
    function sy(y) { return (y - S.cam.y) * S.cam.k + S.h / 2; }

    // ---------- painting ----------
    function readColors() {
      var cs = getComputedStyle(root), c = {};
      ['ground', 'ground-deep', 'line', 'text', 'text-soft', 'text-off', 'selection', 'proposed', 'planned', 'hole', 'tier-0', 'tier-1', 'tier-2', 'tier-3', 'tier-4', 'tier-5', 'tier-6', 'tier-7'].forEach(function (k) {
        c[k] = (cs.getPropertyValue('--' + k) || '').trim() || '#888';
      });
      S.colors = c;
    }
    function font(px, kind) { return (kind === 'read' ? '400 ' : '600 ') + px + 'px ' + (kind === 'read' ? '"Aldrich", system-ui, sans-serif' : '"Chakra Petch", system-ui, sans-serif'); }
    function label(text, x, y, px, color, kind, align) {
      ctx.font = font(px, kind); ctx.textAlign = align || 'center'; ctx.textBaseline = 'middle';
      ctx.lineJoin = 'round'; ctx.lineWidth = 4; ctx.strokeStyle = S.colors.ground; ctx.strokeText(text, x, y);
      ctx.fillStyle = color; ctx.fillText(text, x, y);
    }
    function fitText(text, maxW, px, kind) {
      ctx.font = font(px, kind);
      if (ctx.measureText(text).width <= maxW) return text;
      var t = text; while (t.length > 3 && ctx.measureText(t + '…').width > maxW) t = t.slice(0, -1);
      return t.length > 3 ? t + '…' : null;
    }
    function ringColor(n) {
      if (S.L === 'x' && (n.propS === 'add' || (n.prop && n.prop.s === 'add'))) return S.colors.proposed;
      if (S.L !== 'n' && n.inL && n.inL.indexOf('n') < 0 && n.inL.indexOf('p') >= 0) return S.colors.planned;
      if (n.ghost) return S.colors.planned;
      return S.colors['tier-' + n.tier];
    }
    function dimmed(n) {
      if (!S.filter) return false;
      return !subtreeHas(n, S.filter);
    }
    function subtreeHas(n, d) {
      if (n._fh && n._fh.d === d) return n._fh.v;
      var v = n.drivers.indexOf(d) >= 0 || n.children.some(function (c) { return subtreeHas(c, d); });
      n._fh = { d: d, v: v }; return v;
    }
    function paint() {
      if (S.view !== 'map' || !S.w) return;
      S.L = LETTER[S.layer];
      ctx.setTransform(S.dpr, 0, 0, S.dpr, 0, 0);
      ctx.fillStyle = S.colors.ground; ctx.fillRect(0, 0, S.w, S.h);
      S.hits = [];
      paintNode(S.tree.root, 1);
      paintEdges();
      var n = cur(); if (n) paintSelected(n);
    }
    function paintNode(n, alpha) {
      var X = sx(n.x), Y = sy(n.y), R = n.r * S.cam.k;
      if (R < 1.1) return;
      if (X + R < -40 || X - R > S.w + 40 || Y + R < -40 || Y - R > S.h + 40) return;
      var a = alpha * (n.ghost ? 0.6 : 1) * (dimmed(n) ? 0.22 : 1);
      var col = ringColor(n);
      n._s = { x: X, y: Y, r: R };
      ctx.globalAlpha = a;
      if (R < 5) { ctx.fillStyle = col; ctx.beginPath(); ctx.arc(X, Y, Math.max(1.4, R * 0.7), 0, 6.2832); ctx.fill(); ctx.globalAlpha = 1; return; }
      S.hits.push(n);
      // body
      ctx.beginPath(); ctx.arc(X, Y, R, 0, 6.2832);
      ctx.fillStyle = n.depth % 2 ? S.colors['ground-deep'] : S.colors.ground; ctx.fill();
      ctx.lineWidth = n.kind === 'place' || n.kind === 'hub' ? 2.5 : n.tier >= 4 ? 2 : 1.5;
      ctx.strokeStyle = col;
      var dark = S.L === 'n' && n.now && n.now.s === 'dark';
      if (n.ghost) ctx.setLineDash([2, 5]); else if (dark) ctx.setLineDash([9, 5]); else ctx.setLineDash([]);
      ctx.stroke(); ctx.setLineDash([]);
      if ((n.kind === 'place' || n.kind === 'hub') && R > 30) { ctx.beginPath(); ctx.arc(X, Y, R - 5, 0, 6.2832); ctx.lineWidth = 1; ctx.stroke(); }
      if (n.lead && R > 12) { ctx.beginPath(); ctx.arc(X, Y, R + 4, 0, 6.2832); ctx.lineWidth = 1; ctx.strokeStyle = S.colors.text; ctx.stroke(); }
      // beads
      var m = n.phases.length;
      if (m && R > 22) {
        for (var i = 0; i < m; i++) {
          var th = phaseAngle(n, i), bx = X + Math.cos(th) * R, by = Y + Math.sin(th) * R, bs = R > 110 ? 5 : 3;
          ctx.fillStyle = col; ctx.save(); ctx.translate(bx, by); ctx.rotate(Math.PI / 4); ctx.fillRect(-bs, -bs, bs * 2, bs * 2); ctx.restore();
          if (R > 60) { // direction chevron between beads
            var tm = th + Math.PI / m, cx0 = X + Math.cos(tm) * R, cy0 = Y + Math.sin(tm) * R;
            ctx.save(); ctx.translate(cx0, cy0); ctx.rotate(tm + Math.PI / 2); ctx.strokeStyle = col; ctx.lineWidth = 1.5;
            ctx.beginPath(); ctx.moveTo(-4, -4); ctx.lineTo(1, 0); ctx.lineTo(-4, 4); ctx.stroke(); ctx.restore();
          }
        }
      }
      // spokes to children that open a phase
      var ch = n.children, maxChild = 0;
      for (var j = 0; j < ch.length; j++) maxChild = Math.max(maxChild, ch[j].r * S.cam.k);
      if (m && (R > 260 || (n.id === S.sel && R > 90))) {
        ctx.strokeStyle = S.colors.line; ctx.lineWidth = 1;
        ch.forEach(function (c) {
          if (c.at == null || c.core) return;
          var th = phaseAngle(n, c.at), bx = X + Math.cos(th) * R, by = Y + Math.sin(th) * R;
          var cx1 = sx(c.x), cy1 = sy(c.y), cr = c.r * S.cam.k, dx = cx1 - bx, dy = cy1 - by, d = Math.sqrt(dx * dx + dy * dy);
          if (d <= cr) return;
          ctx.beginPath(); ctx.moveTo(bx, by); ctx.lineTo(cx1 - dx / d * cr, cy1 - dy / d * cr); ctx.stroke();
        });
      }
      // labels: a ring shows either its name or its insides, never both on top of each other
      var showKids = maxChild >= 16;
      var big = R > Math.max(S.w, S.h) * 0.75;
      var tierTerm = termOf(n).toUpperCase();
      if (!showKids) {
        if (ch.length && R > 30) { ctx.beginPath(); ctx.arc(X, Y, R * 0.84, 0, 6.2832); ctx.lineWidth = 1; ctx.strokeStyle = S.colors.line; ctx.setLineDash([1, 4]); ctx.stroke(); ctx.setLineDash([]); }
        if (R >= 18) {
          var fs = R > 70 ? 15 : R > 36 ? 12 : 10;
          var nm = fitText(n.name, R * 1.7, fs, 'label');
          if (nm) label(nm, X, Y, fs, S.colors.text, 'label');
          if (R > 46) {
            label(tierTerm, X, Y - 20, 10, col, 'label');
            label(fmtSpan(n.t), X, Y + 18, 11, S.colors['text-soft'], 'read');
          }
        }
      } else if (!big && R > 60) {
        var fs2 = R > 200 ? 16 : 13, ly, lw;
        ctx.font = font(fs2, 'label');
        var tw = ctx.measureText(n.name).width, free = 0;
        if (n.centerFree) { free = Infinity; ch.forEach(function (c) { free = Math.min(free, (Math.hypot(c.x - n.x, c.y - n.y) - c.r) * S.cam.k); }); }
        if (n.centerFree && tw <= free * 1.5) { ly = Y; lw = free * 1.5; }
        else if (ch.length === 1) { ly = Y - R * 0.77; lw = R * 0.9; }
        else { ly = Y - R + 20; lw = R * 0.62; }
        var nm2 = fitText(n.name, lw, fs2, 'label');
        if (nm2) {
          label(nm2, X, ly, fs2, S.colors.text, 'label');
          if (R > 130) label(tierTerm + ' \u00b7 ' + fmtSpan(n.t), X, ly + 17, 10, col, 'label');
        }
      }
      // badges
      if (R > 16) {
        var cnt = showKids ? openHoles(n, S.L) : n.holeSum;
        if (cnt > 0) badge(X + Math.cos(-0.82) * R, Y + Math.sin(-0.82) * R, String(cnt), S.colors.hole, R > 30);
        if (n.parent && n.reps && R > 34) tag(X + Math.cos(0.82) * R, Y + Math.sin(0.82) * R, '\u00d7' + n.reps.replace('-', '\u2013'), S.colors.text);
        var ps = S.L === 'x' ? (n.propS || (n.prop && n.prop.s)) : null;
        if (ps === 'add' && R > 26) tag(X + Math.cos(-2.3) * R, Y + Math.sin(-2.3) * R, 'NEW', S.colors.proposed);
        else if (ps && R > 110) tag(X + Math.cos(-2.3) * R, Y + Math.sin(-2.3) * R, 'CHANGED', S.colors.proposed);
        else if (ps && R > 20) badge(X + Math.cos(-2.3) * R, Y + Math.sin(-2.3) * R, '', S.colors.proposed, false);
        else if (n.ghost && R > 26) tag(X + Math.cos(-2.3) * R, Y + Math.sin(-2.3) * R, 'PLANNED', S.colors.planned);
        else if (dark && R > 26) tag(X + Math.cos(-2.3) * R, Y + Math.sin(-2.3) * R, 'OFF', S.colors['text-soft']);
        else if (S.L === 'n' && n.now && n.now.s === 'signed' && R > 26) tag(X + Math.cos(-2.3) * R, Y + Math.sin(-2.3) * R, 'SIGNED', S.colors.text);
      }
      ctx.globalAlpha = 1;
      if (showKids) for (var q = 0; q < ch.length; q++) paintNode(ch[q], alpha * (n.ghost ? 0.6 : 1));
      else ch.forEach(function clear(c) { c._s = null; c.children.forEach(clear); });
    }
    function badge(x, y, t, color, withText) {
      ctx.save(); ctx.translate(x, y); ctx.rotate(Math.PI / 4);
      var s = withText ? 8 : 4; ctx.fillStyle = color; ctx.fillRect(-s, -s, s * 2, s * 2);
      ctx.strokeStyle = S.colors.ground; ctx.lineWidth = 1.5; ctx.strokeRect(-s, -s, s * 2, s * 2); ctx.restore();
      if (withText) { ctx.font = font(10, 'label'); ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillStyle = S.colors.ground; ctx.fillText(t, x, y + 0.5); }
    }
    function tag(x, y, t, color) {
      ctx.font = font(9, 'label'); var w = ctx.measureText(t).width + 8;
      ctx.fillStyle = S.colors.ground; ctx.fillRect(x - w / 2, y - 7, w, 14);
      ctx.strokeStyle = color; ctx.lineWidth = 1; ctx.strokeRect(x - w / 2 + 0.5, y - 6.5, w - 1, 13);
      ctx.fillStyle = color; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(t, x, y + 0.5);
    }
    function paintSelected(n) {
      if (!n._s) return;
      var X = sx(n.x), Y = sy(n.y), R = n.r * S.cam.k;
      if (R < 5 || X + R < 0 || X - R > S.w || Y + R < 0 || Y - R > S.h) return;
      ctx.beginPath(); ctx.arc(X, Y, R + 3, 0, 6.2832); ctx.lineWidth = 2.5; ctx.strokeStyle = S.colors.selection; ctx.stroke();
      var m = n.phases.length;
      if (m && R > 70 && R < Math.max(S.w, S.h)) {
        for (var i = 0; i < m; i++) {
          var th = phaseAngle(n, i), c = Math.cos(th), s = Math.sin(th);
          var bx = X + c * R, by = Y + s * R;
          var txt = (i + 1) + '  ' + n.phases[i].n, al = Math.abs(c) < 0.3 ? 'center' : c > 0 ? 'left' : 'right';
          var px = bx + c * 14, py = by + s * 14 + (Math.abs(c) < 0.3 ? (s < 0 ? -4 : 4) : 0);
          var maxW = 240;
          var f = fitText(txt, Math.max(60, Math.min(maxW, S.w - 16)), 12, 'label');
          if (f) {
            ctx.font = font(12, 'label'); var fw = ctx.measureText(f).width;
            var lo = al === 'left' ? px : al === 'right' ? px - fw : px - fw / 2;
            var shift = lo < 6 ? 6 - lo : lo + fw > S.w - 6 ? S.w - 6 - lo - fw : 0;
            label(f, px + shift, py, 12, n.feelAt === i ? S.colors.proposed : S.colors.text, 'label', al);
          }
        }
      }
    }
    function paintEdges() {
      var sel = S.sel;
      M.edges.forEach(function (e) {
        if (!has(e, S.L)) return;
        var a = S.tree.all[e.from], b = S.tree.all[e.to];
        if (!a || !b || !a._s || !b._s) return;
        var A = { x: sx(a.x), y: sy(a.y), r: a.r * S.cam.k }, B = { x: sx(b.x), y: sy(b.y), r: b.r * S.cam.k };
        var lim = Math.min(S.w, S.h);
        if (A.r < 4 || B.r < 4 || Math.max(A.r, B.r) < 16 || A.r > lim * 1.4 || B.r > lim * 1.4) return;
        var on = sel === e.from || sel === e.to;
        var dx = B.x - A.x, dy = B.y - A.y, d = Math.sqrt(dx * dx + dy * dy); if (d < A.r + B.r + 6) return;
        var ux = dx / d, uy = dy / d;
        var x1 = A.x + ux * (A.r + 3), y1 = A.y + uy * (A.r + 3), x2 = B.x - ux * (B.r + 6), y2 = B.y - uy * (B.r + 6);
        var mx = (x1 + x2) / 2 - uy * d * 0.12, my = (y1 + y2) / 2 + ux * d * 0.12;
        var col = e.in === 'x' ? S.colors.proposed : on ? S.colors.selection : S.colors['text-soft'];
        ctx.globalAlpha = on ? 1 : 0.7; ctx.strokeStyle = col; ctx.lineWidth = on ? 2 : 1.25;
        ctx.setLineDash(e.kind === 'cross' ? [7, 4] : e.kind === 'feed' ? [2, 4] : []);
        ctx.beginPath(); ctx.moveTo(x1, y1); ctx.quadraticCurveTo(mx, my, x2, y2); ctx.stroke(); ctx.setLineDash([]);
        var ang = Math.atan2(y2 - my, x2 - mx);
        ctx.fillStyle = col; ctx.beginPath(); ctx.moveTo(x2 + Math.cos(ang) * 6, y2 + Math.sin(ang) * 6);
        ctx.lineTo(x2 + Math.cos(ang + 2.5) * 6, y2 + Math.sin(ang + 2.5) * 6); ctx.lineTo(x2 + Math.cos(ang - 2.5) * 6, y2 + Math.sin(ang - 2.5) * 6); ctx.fill();
        ctx.globalAlpha = 1;
        if (on && d > 90) {
          var qx = (x1 + 2 * mx + x2) / 4, qy = (y1 + 2 * my + y2) / 4;
          var f = fitText(e.kind === 'train' ? 'trains for' : e.kind === 'cross' ? 'crossing' : 'feeds', 200, 11, 'label');
          if (f) label(f, qx, qy, 11, col, 'label');
        }
      });
    }

    // ---------- pointer and keys ----------
    function hit(px, py) {
      var best = null;
      (S.hits || []).forEach(function (n) {
        var s = n._s; if (!s || s.r < 7) return;
        var dx = px - s.x, dy = py - s.y;
        if (dx * dx + dy * dy <= s.r * s.r && (!best || n.depth >= best.depth)) best = n;
      });
      return best;
    }
    var ptrs = {}, drag = null;
    cv.addEventListener('pointerdown', function (e) {
      cv.setPointerCapture(e.pointerId); ptrs[e.pointerId] = { x: e.offsetX, y: e.offsetY };
      var ids = Object.keys(ptrs);
      if (ids.length === 1) drag = { x: e.offsetX, y: e.offsetY, cx: S.cam.x, cy: S.cam.y, moved: false };
      else if (ids.length === 2) { var a = ptrs[ids[0]], b = ptrs[ids[1]]; drag = { pinch: Math.hypot(a.x - b.x, a.y - b.y), k: S.cam.k, moved: true }; }
      S.anim = null;
    });
    cv.addEventListener('pointermove', function (e) {
      if (!ptrs[e.pointerId]) { var h = hit(e.offsetX, e.offsetY); cv.style.cursor = h ? 'pointer' : 'grab'; return; }
      ptrs[e.pointerId] = { x: e.offsetX, y: e.offsetY };
      var ids = Object.keys(ptrs);
      if (drag && drag.pinch && ids.length === 2) {
        var a = ptrs[ids[0]], b = ptrs[ids[1]], dist = Math.hypot(a.x - b.x, a.y - b.y);
        var f = (drag.k * dist / drag.pinch) / S.cam.k; zoomBy(f, (a.x + b.x) / 2, (a.y + b.y) / 2);
      } else if (drag && !drag.pinch) {
        var dx = e.offsetX - drag.x, dy = e.offsetY - drag.y;
        if (Math.abs(dx) + Math.abs(dy) > 4) drag.moved = true;
        if (drag.moved) { S.cam.x = drag.cx - dx / S.cam.k; S.cam.y = drag.cy - dy / S.cam.k; draw(); }
      }
    });
    function up(e) {
      var was = drag; delete ptrs[e.pointerId];
      if (!Object.keys(ptrs).length) drag = null;
      if (was && !was.moved && !was.pinch) {
        var h = hit(e.offsetX, e.offsetY);
        if (h) select(h.id, true); else select('era', true);
      }
    }
    cv.addEventListener('pointerup', up); cv.addEventListener('pointercancel', function (e) { delete ptrs[e.pointerId]; drag = null; });
    cv.addEventListener('wheel', function (e) { e.preventDefault(); zoomBy(Math.exp(-e.deltaY * 0.0016), e.offsetX, e.offsetY); }, { passive: false });
    cv.addEventListener('keydown', function (e) {
      var n = cur(), p = n.parent, i;
      if (e.key === 'ArrowUp' || e.key === 'Escape' || e.key === 'Backspace') { if (p) select(p.id, true); }
      else if (e.key === 'ArrowDown' || e.key === 'Enter') { if (n.children.length) select(n.children[0].id, true); }
      else if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
        if (!p) return; i = p.children.indexOf(n); var k = p.children.length;
        select(p.children[(i + (e.key === 'ArrowRight' ? 1 : k - 1)) % k].id, true);
      }
      else if (e.key === '+' || e.key === '=') zoomBy(1.5);
      else if (e.key === '-') zoomBy(1 / 1.5);
      else if (e.key === 'Home' || e.key === '0') select('era', true);
      else return;
      e.preventDefault();
    });

    // ---------- crumbs ----------
    function renderCrumbs() {
      crumbs.innerHTML = '';
      var path = [], n = cur(); while (n) { path.unshift(n); n = n.parent; }
      path.forEach(function (p, i) {
        if (i) crumbs.appendChild(el('span', 'la-sep', '›'));
        var b = el('button', 'la-crumb', p.name); b.type = 'button';
        if (i === path.length - 1) b.setAttribute('aria-current', 'true');
        b.addEventListener('click', function () { if (S.view !== 'map') setView('map'); select(p.id, true); });
        crumbs.appendChild(b);
      });
    }

    // ---------- timescale strip ----------
    function renderScale() {
      scale.innerHTML = '';
      var n = cur();
      var axis = el('div', 'la-axis'); scale.appendChild(axis);
      M.tiers.forEach(function (t) {
        var b = el('div', 'la-tierbar' + (t.id === n.tier ? ' on' : ''));
        b.style.left = (tx(t.span[0]) * 100) + '%'; b.style.width = ((tx(t.span[1]) - tx(t.span[0])) * 100) + '%';
        b.style.top = (t.id % 2 ? 16 : 0) + 'px'; b.style.setProperty('--c', 'var(--tier-' + t.id + ')');
        b.appendChild(el('span', null, t.term)); b.title = t.term + ': ' + fmtSpan(t.span) + '. ' + t.is;
        axis.appendChild(b);
      });
      var me = el('div', 'la-me'); me.style.left = (tx(n.t[0]) * 100) + '%'; me.style.width = Math.max(0.6, (tx(n.t[1]) - tx(n.t[0])) * 100) + '%';
      axis.appendChild(me);
      var ticks = el('div', 'la-ticks'); scale.appendChild(ticks);
      [[1, '1 s'], [10, '10 s'], [60, '1 min'], [600, '10 min'], [3600, '1 h'], [86400, '1 day'], [604800, '1 wk'], [2592000, '1 mo'], [15552000, '6 mo']].forEach(function (k) {
        var t = el('span', null, k[1]); t.style.left = (tx(k[0]) * 100) + '%'; ticks.appendChild(t);
      });
    }

    function renderLegend() {
      legend.innerHTML = '';
      function item(cls, text) { var i = el('span', 'la-lg'); i.appendChild(el('i', cls)); i.appendChild(document.createTextNode(text)); legend.appendChild(i); }
      item('lg-ring', 'a loop; inside it, the faster loops it is made of');
      item('lg-bead', 'a step');
      item('lg-hole', 'holes');
      if (S.layer === 'now') item('lg-ghost', 'planned, not in the game');
      if (S.layer !== 'now') item('lg-plan', 'new in the plan');
      if (S.layer === 'prop') item('lg-prop', 'proposed');
      item('lg-cross', 'crossing');
      item('lg-feed', 'feeds');
    }

    // ---------- inspector ----------
    function chip(text, cls, title) { var c = el('span', 'la-chip ' + (cls || ''), text); if (title) c.title = title; return c; }
    function sec(title) { var s = el('section', 'la-sec'); s.appendChild(el('h4', null, title)); insp.appendChild(s); return s; }
    function linkTo(n, text) {
      var b = el('button', 'la-link', text || n.name); b.type = 'button';
      b.addEventListener('click', function () { if (S.view !== 'map') { select(n.id, false); } else select(n.id, true); });
      return b;
    }
    function renderInsp() {
      var n = cur(), T = tierOf(n), L = LETTER[S.layer];
      insp.innerHTML = '';
      var head = el('div', 'la-head'); insp.appendChild(head);
      var kicker = el('div', 'la-kicker'); head.appendChild(kicker);
      var tc = chip(termOf(n), 'la-tier'); tc.style.setProperty('--c', 'var(--tier-' + n.tier + ')'); kicker.appendChild(tc);
      kicker.appendChild(el('span', 'la-span', fmtSpan(n.t) + ' a pass'));
      if (n.element) kicker.appendChild(chip(n.element, 'la-el'));
      head.appendChild(el('h3', null, n.name));
      if (n.is) head.appendChild(el('p', 'la-is', n.is));
      if (n.ghost) head.appendChild(el('p', 'la-note plan', 'Planned. Not in the game today.'));
      if (n.invented) head.appendChild(el('p', 'la-note prop', 'Invented for the proposed layer from the kit text. The vessel’s owner corrects it.'));
      if (n.kind === 'vessel' && n.vessel !== S.vessel) {
        var use = el('button', 'la-btn la-cta', 'Show ' + n.name + ' in every kit'); use.type = 'button';
        use.addEventListener('click', function () { setVessel(n.vessel); }); head.appendChild(use);
      }
      // the three layers
      if (n.now || n.plan || n.prop || n.inL) {
        var ls = sec('On each layer');
        [['now', 'Now'], ['plan', 'Planned'], ['prop', 'Proposed']].forEach(function (k) {
          var here = (n.inL || 'npx').indexOf(LETTER[k[0]]) >= 0, o = n[k[0]] || {};
          var row = el('div', 'la-layer' + (k[0] === S.layer ? ' on' : '')); row.setAttribute('data-l', k[0]);
          var st = !here ? 'not there' : k[0] === 'now' ? ({ signed: 'signed off by a person', built: 'built, not signed off', part: 'partly there', dark: 'built, switched off' }[o.s] || 'there')
            : k[0] === 'plan' ? 'decided' : ({ add: 'new', change: 'changed' }[o.s] || 'as planned');
          row.appendChild(el('b', null, k[1])); row.appendChild(el('span', 'la-st', st));
          if (o.note) row.appendChild(el('p', null, o.note));
          ls.appendChild(row);
        });
      }
      // steps
      if (n.phases.length) {
        var ps = sec(n.kind === 'combo' ? 'The chain' : 'Steps of one pass');
        var ol = el('ol', 'la-steps'); ps.appendChild(ol);
        n.phases.forEach(function (p, i) {
          var li = el('li'); if (n.feelAt === i) li.className = 'feel';
          if (p.e) li.appendChild(chip(p.e, 'la-el'));
          li.appendChild(el('span', 'la-pn', p.n));
          if (p.d) li.appendChild(el('span', 'la-pd', p.d));
          n.children.forEach(function (c) { if (c.at === i && !c.core) { li.appendChild(document.createTextNode(' ')); li.appendChild(linkTo(c, 'opens ' + c.name)); } });
          ol.appendChild(li);
        });
        if (n.feel) { var fe = el('p', 'la-feel'); fe.appendChild(el('b', null, 'The feeling: ')); fe.appendChild(document.createTextNode(n.feel)); ps.appendChild(fe); }
      }
      if (n.kind === 'verb') {
        var vs = sec('The ability');
        if (n.trigger) { var p1 = el('p'); p1.appendChild(el('b', null, 'Trigger: ')); p1.appendChild(document.createTextNode(n.trigger)); vs.appendChild(p1); }
        if (n.scales) { var p2 = el('p'); p2.appendChild(el('b', null, 'More of the element: ')); p2.appendChild(document.createTextNode(n.scales)); vs.appendChild(p2); }
        if (n.games && n.games.length) { var p3 = el('p'); p3.appendChild(el('b', null, 'Its arcade game: ')); p3.appendChild(document.createTextNode(n.games.join(', '))); vs.appendChild(p3); }
        if (n.board) { var p4 = el('p'); p4.appendChild(el('b', null, 'Elemental loop, steps 1 2 3: ')); p4.appendChild(document.createTextNode(boardWords(n.board))); vs.appendChild(p4); }
      }
      if (n.kind === 'role' || n.kind === 'kit' || n.kind === 'vessel') {
        var f = M.fleet[n.vessel], rs = sec(f.label);
        var words = [];
        if (f.lean) words.push('Leans ' + f.lean + ' (' + f.leanWhy + ')');
        else words.push('Cultivator or destroyer: not measured yet');
        if (f.arenas.length) words.push('Arena games: ' + f.arenas.join(', '));
        else words.push('Seated in no arena game');
        if (f.missing) words.push('Kit missing a game for: ' + f.missing);
        words.forEach(function (w) { rs.appendChild(el('p', null, w)); });
        var tb = el('table', 'la-mini'); rs.appendChild(tb);
        ['Time', 'Charge', 'Mass', 'Space'].forEach(function (E) {
          var tr = el('tr'); tr.appendChild(el('th', null, E));
          tr.appendChild(el('td', null, (f.games[E] || []).join(', ') || (f.excused.indexOf(E) >= 0 ? 'excused' : 'no game')));
          tr.appendChild(el('td', 'la-bd', f.board[E] ? boardWords(f.board[E]) : ''));
          tb.appendChild(tr);
        });
      }
      // drivers
      if (n.drivers.length) {
        var ds = sec('What keeps you in it');
        var wrapD = el('div', 'la-chips'); ds.appendChild(wrapD);
        n.drivers.forEach(function (id) { var d = driver(id); if (d) wrapD.appendChild(chip(d.name, 'la-drv' + (S.filter === id ? ' on' : ''), d.is)); });
      }
      // hierarchy and heterarchy
      var hs = sec('Where it leads');
      if (n.parent) {
        var pu = el('p'); pu.appendChild(el('b', null, 'Steps up to: ')); pu.appendChild(linkTo(n.parent));
        var rep = n.reps ? ' after ×' + n.reps.replace('-', '–') + (n.repsOf ? ' (' + n.repsOf + ')' : '') : n.repsOf ? ' — how many passes first is not set (' + n.repsOf + ')' : '';
        pu.appendChild(document.createTextNode(rep)); hs.appendChild(pu);
      }
      if (n.children.length) {
        var pd = el('p'); pd.appendChild(el('b', null, 'Made of: '));
        n.children.forEach(function (c, i) { if (i) pd.appendChild(document.createTextNode(', ')); pd.appendChild(linkTo(c)); });
        hs.appendChild(pd);
      }
      var sid = n.id;
      M.edges.forEach(function (e) {
        if (!has(e, L)) return;
        if (e.from !== sid && e.to !== sid) return;
        var other = S.tree.all[e.from === sid ? e.to : e.from]; if (!other) return;
        var p = el('p', 'la-edge' + (e.in === 'x' ? ' prop' : ''));
        var kind = e.kind === 'train' ? (e.from === sid ? 'Trains for: ' : 'Trained by: ') : e.kind === 'cross' ? (e.from === sid ? 'Crosses to: ' : 'Crossed to from: ') : (e.from === sid ? 'Feeds: ' : 'Fed by: ');
        p.appendChild(el('b', null, kind)); p.appendChild(linkTo(other));
        var ho = handoff(e.handoff);
        p.appendChild(document.createTextNode(' — ' + e.why + (ho ? ' Hand-off: ' + ho.name.toLowerCase() + '.' : '')));
        hs.appendChild(p);
      });
      // holes
      if (n.holes.length) {
        var ho2 = sec(S.layer === 'prop' ? 'Holes and what is proposed' : 'Holes');
        n.holes.forEach(function (h) {
          var d = el('div', 'la-hole'); d.appendChild(el('p', null, h.q));
          if (S.layer === 'prop') d.appendChild(el('p', h.fill ? 'la-fill' : 'la-open', h.fill ? h.fill : 'Still open.'));
          ho2.appendChild(d);
        });
      }
      if (S.layer === 'prop') {
        var mine = M.proposals.filter(function (p) { return p.touches.indexOf(n.id) >= 0; });
        if (mine.length) { var pr = sec('Proposals that touch this'); mine.forEach(function (p) { var d = el('p'); d.appendChild(el('b', null, p.id + ' ' + p.title + '. ')); d.appendChild(document.createTextNode(p.what)); pr.appendChild(d); }); }
      }
      var T2 = sec('At this tier, ask'); T2.appendChild(el('p', 'la-ask', T.ask));
      if (n.src && n.src.length) { var ss = sec('Sources'); n.src.forEach(function (s) { ss.appendChild(el('p', 'la-src', s)); }); }
    }
    function boardWords(b) {
      var w = { D: 'done', b: 'built', '~': 'doing', '.': 'to do', R: 'redo', x: 'no game', '-': 'excused' };
      return b.split(' ').map(function (c, i) { return ['fun', 'ladder', 'AI'][i] + ' ' + (w[c] || c); }).join(' · ');
    }

    // ---------- list views ----------
    function flat() {
      var out = [], seenKit = false;
      (function walk(n) {
        if (n.kind === 'role' || n.kind === 'kit') {
          if (n.kind === 'kit') { if (seenKit) return; seenKit = true; }
        }
        if (n.kind === 'vessel') return;
        if (n.kind !== 'fleet' || true) out.push(n);
        n.children.forEach(walk);
      })(S.tree.root);
      return out;
    }
    function rowBtn(n, cls) {
      var b = el('button', 'la-row ' + (cls || '')); b.type = 'button'; b.setAttribute('data-id', n.id);
      b.addEventListener('click', function () { select(n.id, false); });
      b.addEventListener('dblclick', function () { setView('map'); select(n.id, true); });
      return b;
    }
    function markRows() {
      table.querySelectorAll('.la-row').forEach(function (b) { b.setAttribute('aria-current', b.getAttribute('data-id') === S.sel ? 'true' : 'false'); });
    }
    function renderTable() {
      table.innerHTML = '';
      if (S.view === 'ladder') renderLadder(); else if (S.view === 'drivers') renderDrivers(); else if (S.view === 'holes') renderHoles();
      markRows();
    }
    function renderLadder() {
      var nodes = flat();
      table.appendChild(el('p', 'la-lede', 'Every loop on one clock, slowest at the top. A bar is how long one pass takes; the number is how many passes before the loop above takes its next step. Kit rows show the ' + M.fleet[S.vessel].label + '.'));
      var ax = el('div', 'la-lticks');
      [[1, '1 s'], [60, '1 min'], [3600, '1 h'], [86400, '1 d'], [2592000, '1 mo']].forEach(function (k) { var t = el('span', null, k[1]); t.style.left = (tx(k[0]) * 100) + '%'; ax.appendChild(t); });
      var axRow = el('div', 'la-lrow la-lhead'); axRow.appendChild(el('span', 'la-lname', '')); var axc = el('span', 'la-lbar'); axc.appendChild(ax); axRow.appendChild(axc); axRow.appendChild(el('span', 'la-lrep', 'passes')); table.appendChild(axRow);
      for (var t = 7; t >= 0; t--) {
        var T = M.tiers[t], mine = nodes.filter(function (n) { return n.tier === t; });
        if (!mine.length) continue;
        var h = el('div', 'la-thead'); h.style.setProperty('--c', 'var(--tier-' + t + ')');
        h.appendChild(el('b', null, T.term)); h.appendChild(el('span', null, fmtSpan(T.span))); h.appendChild(el('em', null, T.is)); table.appendChild(h);
        mine.forEach(function (n) {
          if (S.filter && n.drivers.indexOf(S.filter) < 0) return;
          var b = rowBtn(n, 'la-lrow' + (n.ghost ? ' ghost' : ''));
          b.appendChild(el('span', 'la-lname', n.name));
          var bar = el('span', 'la-lbar'), fill = el('i');
          fill.style.left = (tx(n.t[0]) * 100) + '%'; fill.style.width = Math.max(0.8, (tx(n.t[1]) - tx(n.t[0])) * 100) + '%';
          fill.style.setProperty('--c', n.ghost ? 'var(--planned)' : (S.layer === 'prop' && (n.propS === 'add' || (n.prop && n.prop.s === 'add'))) ? 'var(--proposed)' : 'var(--tier-' + t + ')');
          bar.appendChild(fill); b.appendChild(bar);
          b.appendChild(el('span', 'la-lrep', n.reps ? '×' + n.reps.replace('-', '–') : n.parent ? '?' : ''));
          b.title = n.name + ': ' + fmtSpan(n.t) + (n.parent ? '. Steps up to ' + n.parent.name : '');
          table.appendChild(b);
        });
      }
    }
    function renderDrivers() {
      var nodes = flat();
      table.appendChild(el('p', 'la-lede', 'Where each driver works. A cell counts the loops at that tier that the driver keeps a player in. An empty cell is a tier where that driver does nothing yet. Pick a row to light those loops on the map.'));
      var g = el('div', 'la-grid'); table.appendChild(g);
      g.appendChild(el('span', 'la-gh', ''));
      M.tiers.forEach(function (T) { g.appendChild(el('span', 'la-gh', T.term)); });
      M.drivers.forEach(function (d) {
        var rb = el('button', 'la-gname' + (S.filter === d.id ? ' on' : ''), d.name); rb.type = 'button'; rb.title = d.is;
        rb.addEventListener('click', function () { S.filter = S.filter === d.id ? null : d.id; fSel.value = S.filter || ''; renderTable(); });
        g.appendChild(rb);
        M.tiers.forEach(function (T) {
          var c = nodes.filter(function (n) { return n.tier === T.id && n.drivers.indexOf(d.id) >= 0; }).length;
          var cell = el('span', 'la-gc' + (c ? '' : ' zero'), c ? String(c) : '·'); cell.style.setProperty('--a', String(Math.min(1, c / 6)));
          g.appendChild(cell);
        });
      });
      if (S.filter) {
        var d = driver(S.filter);
        table.appendChild(el('h4', 'la-h', 'Loops that ' + d.name.toLowerCase() + ' drives'));
        table.appendChild(el('p', 'la-lede', d.is));
        nodes.filter(function (n) { return n.drivers.indexOf(S.filter) >= 0; }).sort(function (a, b) { return b.tier - a.tier; }).forEach(function (n) {
          var b = rowBtn(n, 'la-line'); b.appendChild(el('span', 'la-ltier', termOf(n))); b.appendChild(el('span', null, n.name)); table.appendChild(b);
        });
      }
      table.appendChild(el('h4', 'la-h', 'Hand-offs: what moves a player out of one loop and into another'));
      var L = LETTER[S.layer];
      M.edges.forEach(function (e) {
        if (!has(e, L)) return;
        var a = S.tree.all[e.from], b2 = S.tree.all[e.to]; if (!a || !b2) return;
        var b = rowBtn(a, 'la-line' + (e.in === 'x' ? ' prop' : ''));
        b.appendChild(el('span', 'la-ltier', e.kind === 'train' ? 'trains' : e.kind === 'cross' ? 'crossing' : 'feeds'));
        var ho = handoff(e.handoff);
        b.appendChild(el('span', null, a.name + ' → ' + b2.name + ': ' + e.why + (ho ? ' (' + ho.name.toLowerCase() + ')' : '')));
        table.appendChild(b);
      });
    }
    function renderHoles() {
      var nodes = flat(), L = LETTER[S.layer], total = 0, open = 0;
      nodes.forEach(function (n) { total += n.holes.length; open += openHoles(n, L); });
      var lede = S.layer === 'now' ? total + ' holes in what the game has today: loops that are missing, switched off, unplayed or uncounted.'
        : S.layer === 'plan' ? total + ' holes in the plan: things the stabilised version needs that no decision covers yet.'
        : total + ' holes in the plan, ' + (total - open) + ' filled here with an invention and ' + open + ' still open. Every fill is a proposal to accept, change or throw out.';
      table.appendChild(el('p', 'la-lede', lede + ' Kit rows show the ' + M.fleet[S.vessel].label + '.'));
      if (S.layer === 'prop') {
        table.appendChild(el('h4', 'la-h', 'The twelve proposals'));
        M.proposals.forEach(function (p) {
          var d = el('div', 'la-hole prop'); var h = el('p'); h.appendChild(el('b', null, p.id + '  ' + p.title)); d.appendChild(h); d.appendChild(el('p', null, p.what));
          var t = el('p', 'la-touch'); p.touches.forEach(function (id) { var n = S.tree.all[id]; if (n) { t.appendChild(linkTo(n)); t.appendChild(document.createTextNode('  ')); } }); d.appendChild(t);
          table.appendChild(d);
        });
        table.appendChild(el('h4', 'la-h', 'Hole by hole'));
      }
      for (var t = 7; t >= 0; t--) {
        var mine = nodes.filter(function (n) { return n.tier === t && n.holes.length; });
        if (!mine.length) continue;
        var h = el('div', 'la-thead'); h.style.setProperty('--c', 'var(--tier-' + t + ')'); h.appendChild(el('b', null, M.tiers[t].term)); h.appendChild(el('em', null, M.tiers[t].ask)); table.appendChild(h);
        mine.forEach(function (n) {
          n.holes.forEach(function (ho) {
            var b = rowBtn(n, 'la-hrow'); b.appendChild(el('span', 'la-hn', n.name)); b.appendChild(el('span', 'la-hq', ho.q));
            if (S.layer === 'prop') b.appendChild(el('span', ho.fill ? 'la-fill' : 'la-open', ho.fill || 'Still open.'));
            table.appendChild(b);
          });
        });
      }
    }

    // ---------- size ----------
    function resize() {
      var rw = root.clientWidth; root.classList.toggle('la-narrow', rw < 860);
      var w = wrap.clientWidth, h = wrap.clientHeight; if (!w || !h) return;
      var first = !S.w; var k0 = S.w ? fitK(S.tree.root) : 0;
      S.dpr = Math.min(2, window.devicePixelRatio || 1); S.w = w; S.h = h;
      cv.width = Math.round(w * S.dpr); cv.height = Math.round(h * S.dpr); cv.style.width = w + 'px'; cv.style.height = h + 'px';
      if (first) focus(cur(), false); else { S.cam.k *= fitK(S.tree.root) / k0; draw(); }
    }
    if (window.ResizeObserver) new ResizeObserver(resize).observe(root); else window.addEventListener('resize', resize);
    if (window.MutationObserver) new MutationObserver(function () { readColors(); draw(); }).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme', 'class', 'style'] });
    if (window.matchMedia) { var mq = window.matchMedia('(prefers-color-scheme: dark)'); if (mq.addEventListener) mq.addEventListener('change', function () { readColors(); draw(); }); }

    rebuild(); readColors(); syncBar(); renderAll(); resize();
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(function () { readColors(); draw(); });
    return { select: select, setLayer: setLayer, setVessel: setVessel, setView: setView, state: S };
  }

  // ---------- small standalone pieces for the component previews ----------
  function ladder(host) {
    host.innerHTML = '';
    var w = el('div', 'la la-static'); host.appendChild(w);
    var g = el('div', 'la-terms'); w.appendChild(g);
    for (var t = 7; t >= 0; t--) {
      var T = M.tiers[t], r = el('div', 'la-term'); r.style.setProperty('--c', 'var(--tier-' + t + ')');
      r.appendChild(el('b', null, T.term)); r.appendChild(el('span', 'la-tspan', fmtSpan(T.span)));
      var bar = el('span', 'la-lbar'), f = el('i'); f.style.left = (tx(T.span[0]) * 100) + '%'; f.style.width = ((tx(T.span[1]) - tx(T.span[0])) * 100) + '%'; bar.appendChild(f); r.appendChild(bar);
      r.appendChild(el('span', 'la-tis', T.is)); r.appendChild(el('em', null, T.ask));
      g.appendChild(r);
    }
  }
  function drivers(host) {
    host.innerHTML = '';
    var w = el('div', 'la la-static'); host.appendChild(w);
    [['inner', 'Inner drivers: the loop is its own reason'], ['outer', 'Outer drivers: the game gives the reason'], ['handoff', 'Hand-offs: what moves a player out of a loop']].forEach(function (k) {
      w.appendChild(el('h4', 'la-h', k[1]));
      var g = el('div', 'la-dlist'); w.appendChild(g);
      (k[0] === 'handoff' ? M.handoffs : M.drivers.filter(function (d) { return d.kind === k[0]; })).forEach(function (d) {
        var r = el('div', 'la-ditem'); r.appendChild(el('span', 'la-chip la-drv', d.name)); r.appendChild(el('span', null, d.is)); g.appendChild(r);
      });
    });
  }

  window.LoopAtlas = {
    model: M, mount: mount, buildTree: buildTree, fmtSpan: fmtSpan,
    LoopAtlas: mount, TimescaleLadder: ladder, DriverChips: drivers,
    LoopRing: function (host, opts) { opts = opts || {}; opts.chrome = false; return mount(host, opts); }
  };
})();
