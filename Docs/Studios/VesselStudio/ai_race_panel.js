/* ai_race_panel.js - the universal AI race config panel of the Vessel Studio (/vessel-studio D17).
 *
 * One panel, one vocabulary, in every studio that tests an AI: the Squirrel's Skim Race, the Stoat's
 * Slingshot, and every arcade-mode studio after them. A page mounts it into its own panel and keeps its
 * own simulation; the panel only owns the controls and their state, and reports each change.
 *
 *   const panel = StudioRacePanel.mount(hostElement, {
 *     courses: [{ v: 1, label: 'I1', note: 'flat octagon, 3 laps' }, ...],   // the 4-step intensity ladder (D7)
 *     seats: [{ name: 'Ruby', color: '#ff4f7b' }, ...],                     // the AI rival seats (up to 3)
 *     value: { course: 1, you: 'You', rivals: ['Hard', 'Hard', 'Off'], camera: 'Chase', speed: 1,
 *              thinking: true, autoRestart: false },                          // the page's saved state
 *     supports: { rivals: true, thinking: true, autoRestart: true },        // false or a string = not in this
 *                                                                            // studio yet (the string says why)
 *     levelNote: 'What Easy / Medium / Hard mean here, and where the numbers come from.',
 *     onChange: (key, value, state) => { ... },                              // after every user change
 *     styles: ['Balanced', 'Comet', ...],   // optional: each rival seat also picks a play style (D23); state.rivalStyles
 *     players: { host: element, max: 4, domains: VesselStudioDomains.list } (studio-domains.js; the default) },
 *                           // optional (D25): a Players list in the studio's Game Config tab, + / - to add or remove
 *                           // a player, each opening with Is AI, Domain, Difficulty, Play style, View and Camera.
 *                           // It replaces the Your hull / seat rows; state.players, state.view; onChange('players' | 'view')
 *     sceneHost: element,   // optional: Course, Camera and Speed go here, the studio's Scene Config tab (D21);
 *                           // without it they stay in the panel
 *   });
 *   panel.state;              // a copy of the current state
 *   panel.set('camera', 'Free', true);   // set from code (true = do not call onChange)
 *
 * Keys and values are fixed for every studio:
 *   course       one of courses[].v
 *   you          'You' | 'Easy' | 'Medium' | 'Hard'    (who flies your hull: you, or the AI at a level)
 *   rivals       one level per seat: 'Off' | 'Easy' | 'Medium' | 'Hard'   (each AI rival is its own seat
 *                with its own level, /vessel-studio D20; 'Off' = the seat is empty)
 *   camera       'Chase' | 'Follow' | 'Free'            (the three studio cameras, D16)
 *   speed        1 | 2 | 4                              (simulation speed)
 *   rivalStyles  one name of cfg.styles per seat (only when cfg.styles is given)
 *   thinking     boolean                                (draw each AI's target: believed vs real)
 *   autoRestart  boolean                                (start the next race on its own)
 *
 * Every choice is a dropdown (<select>), never a row of buttons (artifact comment, 2026-10-09): the
 * panel stays one column wide in any rail, and a level shows in its own colour.
 *
 * Plain DOM with its own `arp-` class names and the page's colour tokens (with fallbacks), so it looks
 * native in any studio and never restyles the page. ASCII-only source: served beside the pages without
 * a charset, so every non-ASCII character is written as a \u escape (/vessel-studio section 7).
 */
(function () {
  'use strict';
  if (window.StudioRacePanel) return;

  var LEVELS = ['Easy', 'Medium', 'Hard'];
  var CAMERAS = ['Chase', 'Follow', 'Free'];
  var SPEEDS = [1, 2, 4];
  var SEAT_LEVELS = ['Off'].concat(LEVELS);
  // the game's domain colours (studio-domains.js, generated from OriginalColorSetSO.asset); the fallback is the same table
  var GAME_DOMAINS = window.VesselStudioDomains ? window.VesselStudioDomains.list : [{ key: 'jade', name: 'Jade', color: '#13fff2' }, { key: 'ruby', name: 'Ruby', color: '#ff00f9' }, { key: 'gold', name: 'Gold', color: '#ffa700' }, { key: 'blue', name: 'Blue', color: '#6680ff' }];
  var DEFAULT_SEATS = GAME_DOMAINS.slice(1).map(function (d) { return { name: d.name, color: d.color }; });
  var CAMERA_TIPS = {
    Chase: 'Close behind the watched hull, rolling with it.',
    Follow: 'Wider and level with the world; press C to pick which pilot to follow.',
    Free: 'Detached from the hull: look around and move freely (or orbit the hull, where the studio offers it).'
  };

  var CSS = [
    '.arp{display:grid;gap:8px;min-width:0}',
    '.arp-row{display:grid;grid-template-columns:86px minmax(0,1fr);gap:8px;align-items:center}',
    '.arp-row>span{font-size:12px;color:var(--muted,var(--dim,#8e95bf))}',
    '.arp-seats{display:grid;gap:8px}',
    '.arp-pair{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1.2fr);gap:6px;min-width:0}',
    '.arp-seg{width:100%;min-width:0;background:var(--panel-2,#181c30);color:var(--fg,#e9ecff);border:1px solid var(--line,#262b47);border-radius:6px;padding:5px 6px;font:inherit;font-size:12px;cursor:pointer}',
    '.arp-seg option{background:var(--panel-2,#181c30);color:var(--fg,#e9ecff)}',
    '.arp-seg[data-lv="Easy"]{color:var(--easy,#4be38f)}',
    '.arp-seg[data-lv="Medium"]{color:var(--medium,#ffb454)}',
    '.arp-seg[data-lv="Hard"]{color:var(--hard,#ff5c6c)}',
    '.arp-seg[data-lv="Off"]{color:var(--muted,var(--dim,#8e95bf))}',
    '.arp-seg:disabled{opacity:.4;cursor:default}',
    '.arp-off{font-size:11px;color:var(--muted,var(--dim,#8e95bf));grid-column:2}',
    '.arp-check{display:flex;gap:8px;align-items:center;font-size:13px}',
    '.arp-check input:disabled+span{opacity:.45}',
    '.arp-note{font-size:12px;color:var(--muted,var(--dim,#8e95bf));margin:0}',
    '.arp-phead{display:flex;align-items:center;gap:6px;font-size:12px;color:var(--muted,var(--dim,#8e95bf))}',
    '.arp-phead b{color:var(--fg,#e9ecff);font-size:13px;margin-right:auto}',
    '.arp-pbtn{min-width:26px;height:24px;padding:0 7px;border:1px solid var(--line,#262b47);border-radius:5px;background:transparent;color:var(--fg,#e9ecff);font:700 13px/1 ui-monospace,monospace;cursor:pointer}',
    '.arp-pbtn:disabled{opacity:.35;cursor:default}',
    '.arp-pbtn[aria-pressed="true"]{color:var(--accent,var(--jade,#35e0b0));border-color:var(--accent,var(--jade,#35e0b0))}',
    '.arp-player{border:1px solid var(--line,#262b47);border-radius:6px;padding:5px 8px;display:grid;gap:6px}',
    '.arp-player>summary{cursor:pointer;font-size:12px;display:flex;gap:6px;align-items:center;list-style:none}',
    '.arp-player>summary::-webkit-details-marker{display:none}',
    '.arp-player>summary::after{content:"+";margin-left:auto;font:700 13px/1 ui-monospace,monospace;color:var(--muted,var(--dim,#8e95bf))}',
    '.arp-player[open]>summary::after{content:"\u2212"}',
    '.arp-dot{width:9px;height:9px;border-radius:50%;display:inline-block;flex:none}',
    '.arp :focus-visible{outline:2px solid var(--accent,var(--jade,#35e0b0));outline-offset:1px}'
  ].join('\n');

  function injectCss() {
    if (document.getElementById('arp-css')) return;
    var st = document.createElement('style'); st.id = 'arp-css'; st.textContent = CSS;
    (document.head || document.documentElement).appendChild(st);
  }
  function el(tag, cls, text) { var e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e; }
  function why(s) { return typeof s === 'string' && s ? s : 'Not in this studio yet.'; }

  function mount(host, cfg) {
    if (!host) throw new Error('StudioRacePanel.mount: no host element');
    cfg = cfg || {};
    injectCss();
    var courses = (cfg.courses && cfg.courses.length) ? cfg.courses : [{ v: 1, label: 'I1' }, { v: 2, label: 'I2' }, { v: 3, label: 'I3' }, { v: 4, label: 'I4' }];
    var sup = Object.assign({ rivals: true, thinking: true, autoRestart: true }, cfg.supports || {});
    var seats = (cfg.seats && cfg.seats.length ? cfg.seats : DEFAULT_SEATS).slice(0, 3);
    var v = cfg.value || {};
    function seatLevels(arr) {
      return seats.map(function (_, k) { var x = arr && arr[k]; return SEAT_LEVELS.indexOf(x) >= 0 ? x : 'Off'; });
    }
    var state = {
      course: v.course != null ? v.course : courses[0].v,
      you: ['You'].concat(LEVELS).indexOf(v.you) >= 0 ? v.you : 'You',
      rivals: Array.isArray(v.rivals) ? seatLevels(v.rivals) : seats.map(function (_, k) { return k < 2 ? 'Hard' : 'Off'; }),
      camera: CAMERAS.indexOf(v.camera) >= 0 ? v.camera : 'Chase',
      speed: SPEEDS.indexOf(+v.speed) >= 0 ? +v.speed : 1,
      thinking: v.thinking != null ? !!v.thinking : true,
      autoRestart: !!v.autoRestart
    };
    var styles = Array.isArray(cfg.styles) && cfg.styles.length ? cfg.styles.slice() : null;
    function seatStyles(arr) { return seats.map(function (_, k) { var x = arr && arr[k]; return styles.indexOf(x) >= 0 ? x : styles[0]; }); }
    if (styles) state.rivalStyles = seatStyles(v.rivalStyles);
    if (sup.rivals !== true) state.rivals = seats.map(function () { return 'Off'; });

    var root = el('div', 'arp'); root.setAttribute('data-arp', '');
    // D21: Course, Camera and Speed describe the scene, so a studio can show them in its Scene Config tab
    var sceneRoot = cfg.sceneHost ? el('div', 'arp') : root;
    if (cfg.sceneHost) sceneRoot.setAttribute('data-arp-scene', '');
    var groups = {};
    function seg(key, label, values, text, opts) {
      opts = opts || {};
      var row = el('div', 'arp-row'), name = el('span', null, label), box = el('select', 'arp-seg');
      box.setAttribute('aria-label', label);
      values.forEach(function (val) {
        var o = el('option', null, text(val)); o.value = String(val);
        if (opts.level) { var lv = opts.level(val); if (lv) o.setAttribute('data-lv', lv); }
        if (opts.tip) o.title = opts.tip(val);
        box.appendChild(o);
      });
      if (opts.disabled) { box.disabled = true; box.title = opts.disabled; }
      box.addEventListener('change', function () {
        var val = values[box.selectedIndex];
        if (opts.onPick) opts.onPick(val); else set(key, val);
      });
      row.appendChild(name);
      if (opts.extra) { var two = el('div', 'arp-pair'); two.appendChild(box); two.appendChild(opts.extra); row.appendChild(two); } else row.appendChild(box);
      if (opts.disabled && opts.showOff !== false) row.appendChild(el('span', 'arp-off', opts.disabled));
      (opts.into || root).appendChild(row); groups[key] = box;
      if (opts.color) name.style.color = opts.color;
    }
    function check(key, label, support) {
      var lab = el('label', 'arp-check'), inp = document.createElement('input'), txt = el('span', null, label);
      inp.type = 'checkbox'; inp.setAttribute('data-k', key);
      if (support !== true) { inp.disabled = true; lab.title = why(support); txt.textContent = label + ' (' + why(support).replace(/\.$/, '') + ')'; }
      inp.addEventListener('change', function () { set(key, inp.checked); });
      lab.appendChild(inp); lab.appendChild(txt); root.appendChild(lab); groups[key] = inp;
    }

    seg('course', 'Course', courses.map(function (c) { return c.v; }), function (val) {
      for (var i = 0; i < courses.length; i++) if (courses[i].v === val) return (courses[i].label || String(val)) + (courses[i].note ? ' \u00b7 ' + courses[i].note : '');
      return String(val);
    }, { into: sceneRoot, tip: function (val) { for (var i = 0; i < courses.length; i++) if (courses[i].v === val) return courses[i].note || ''; return ''; } });
    // ---- D25: the Players list (Game Config). Player 1 is your hull; every other player is an AI rival ----
    var PL = cfg.players && cfg.players.host ? cfg.players : null;
    var domains = PL && PL.domains && PL.domains.length ? PL.domains : GAME_DOMAINS.map(function (x) { return { key: x.key, name: x.name, color: x.color }; });
    var maxPlayers = PL ? Math.max(1, Math.min(8, PL.max || 4)) : 0, openCards = { 0: true };
    function domainOf(key) { for (var i = 0; i < domains.length; i++) if (domains[i].key === key) return domains[i]; return domains[0]; }
    function cleanPlayers(arr) {
      var out = [];
      (Array.isArray(arr) ? arr : []).slice(0, maxPlayers).forEach(function (p, i) {
        p = p || {};
        out.push({ ai: i === 0 ? !!p.ai : true, domain: domainOf(p.domain).key, level: LEVELS.indexOf(p.level) >= 0 ? p.level : 'Hard', style: styles && styles.indexOf(p.style) >= 0 ? p.style : (styles ? styles[0] : '') });
      });
      if (!out.length) out.push({ ai: false, domain: domains[0].key, level: 'Hard', style: styles ? styles[0] : '' });
      return out;
    }
    function derive() {   // the older keys, kept in step for pages that still read them
      var ps = state.players; state.you = ps[0].ai ? ps[0].level : 'You';
      state.rivals = seats.map(function (_, k) { return ps[k + 1] ? ps[k + 1].level : 'Off'; });
      if (styles) state.rivalStyles = seats.map(function (_, k) { return ps[k + 1] ? ps[k + 1].style : styles[0]; });
    }
    if (PL) {
      var vp = v.players;
      if (!Array.isArray(vp)) {   // build the list from the older keys
        vp = [{ ai: state.you !== 'You', level: state.you === 'You' ? 'Hard' : state.you, domain: domains[0].key, style: styles ? styles[0] : '' }];
        state.rivals.forEach(function (lv, k) { if (lv !== 'Off') vp.push({ ai: true, level: lv, domain: (domains[(k + 1) % domains.length] || domains[0]).key, style: state.rivalStyles ? state.rivalStyles[k] : '' }); });
      }
      state.players = cleanPlayers(vp); state.view = Math.max(0, Math.min(state.players.length - 1, +v.view || 0)); derive();
    }
    var playerBox = null, camSelects = [];
    function buildPlayers() {
      if (!PL) return;
      if (!playerBox) { playerBox = el('div', 'arp'); playerBox.setAttribute('data-arp-players', ''); PL.host.textContent = ''; PL.host.appendChild(playerBox); }
      playerBox.textContent = ''; camSelects = [];
      var ps = state.players, head = el('div', 'arp-phead');
      head.appendChild(el('b', null, 'Players ' + ps.length));
      var minus = el('button', 'arp-pbtn', '\u2212'); minus.type = 'button'; minus.title = 'Remove the last player'; minus.disabled = ps.length <= 1;
      var plus = el('button', 'arp-pbtn', '+'); plus.type = 'button'; plus.title = 'Add an AI player'; plus.disabled = ps.length >= maxPlayers;
      minus.addEventListener('click', function () { set('players', ps.slice(0, -1)); });
      plus.addEventListener('click', function () {
        var used = ps.map(function (p) { return p.domain; }), d = domains.filter(function (x) { return used.indexOf(x.key) < 0; })[0] || domains[ps.length % domains.length];
        openCards[ps.length] = true;
        set('players', ps.concat([{ ai: true, domain: d.key, level: 'Hard', style: styles ? styles[ps.length % styles.length] : '' }]));
      });
      head.appendChild(minus); head.appendChild(plus); playerBox.appendChild(head);
      ps.forEach(function (p, i) {
        var d = domainOf(p.domain), card = el('details', 'arp-player'); card.open = !!openCards[i];
        card.addEventListener('toggle', function () { openCards[i] = card.open; });
        var sum = el('summary'), dot = el('span', 'arp-dot'); dot.style.background = d.color;
        sum.appendChild(dot); sum.appendChild(el('span', null, 'Player ' + (i + 1) + (i === 0 ? ' (your hull)' : '') + ' \u00b7 ' + (p.ai ? 'AI ' + p.level : 'You fly') + ' \u00b7 ' + d.name + (p.style ? ' \u00b7 ' + p.style : '') + (state.view === i ? ' \u00b7 viewed' : '')));
        card.appendChild(sum);
        var edit = function (k, val) { var arr = state.players.map(function (x) { return Object.assign({}, x); }); arr[i][k] = val; set('players', arr); };
        var row = function (label, node) { var r = el('div', 'arp-row'); r.appendChild(el('span', null, label)); r.appendChild(node); card.appendChild(r); };
        var pick = function (label, list, cur, onPick, opts) {
          var s = el('select', 'arp-seg'); s.setAttribute('aria-label', 'Player ' + (i + 1) + ' ' + label.toLowerCase());
          list.forEach(function (x) { var o = el('option', null, x.name || x); o.value = x.key || x; s.appendChild(o); });
          s.value = cur; if (opts && opts.lv) s.setAttribute('data-lv', cur);
          if (opts && opts.off) { s.disabled = true; s.title = opts.off; }
          s.addEventListener('change', function () { onPick(s.value); }); row(label, s); return s;
        };
        var ai = document.createElement('input'); ai.type = 'checkbox'; ai.checked = p.ai; ai.setAttribute('aria-label', 'Player ' + (i + 1) + ' is AI');
        if (i > 0) { ai.disabled = true; ai.title = 'Only player 1 can be flown by you: every other player is an AI.'; }
        ai.addEventListener('change', function () { edit('ai', ai.checked); });
        var aiWrap = el('label', 'arp-check'); aiWrap.appendChild(ai); aiWrap.appendChild(el('span', null, i === 0 ? (p.ai ? 'the AI flies your hull' : 'you fly it') : 'always (one human per studio)'));
        row('Is AI', aiWrap);
        pick('Domain', domains, p.domain, function (x) { edit('domain', x); });
        pick('Difficulty', LEVELS, p.level, function (x) { edit('level', x); }, { lv: true, off: p.ai ? null : 'You are flying this hull: the difficulty applies when the AI does.' });
        if (styles) pick('Play style', styles, p.style, function (x) { edit('style', x); });
        var view = el('button', 'arp-pbtn', state.view === i ? '\u25c9 Viewing' : '\u25cb View'); view.type = 'button'; view.setAttribute('aria-pressed', String(state.view === i));
        view.title = 'The camera follows this player'; view.addEventListener('click', function () { set('view', i); });
        var cam = el('select', 'arp-seg'); cam.setAttribute('aria-label', 'Camera'); CAMERAS.forEach(function (c) { var o = el('option', null, c); o.value = c; o.title = CAMERA_TIPS[c]; cam.appendChild(o); });
        cam.value = state.camera; cam.addEventListener('change', function () { set('view', i); set('camera', cam.value); }); camSelects.push(cam);
        var two = el('div', 'arp-pair'); two.appendChild(view); two.appendChild(cam); row('View', two);
        playerBox.appendChild(card);
      });
    }
    if (!PL) seg('you', 'Your hull', ['You'].concat(LEVELS), function (val) { return val === 'You' ? 'You fly' : 'AI ' + val; },
      { level: function (val) { return val === 'You' ? null : val; } });
    // one row per AI rival seat, each with its own level (D20)
    var rivalsOff = sup.rivals === true ? null : why(sup.rivals);
    var seatBox = el('div', 'arp-seats'); seatBox.setAttribute('aria-label', 'AI rivals, each with its own level'); if (!PL) root.appendChild(seatBox);
    if (!PL) seats.forEach(function (seat, k) {
      var extra = null;
      if (styles) {   // D23: the seat's play style, beside its level
        extra = el('select', 'arp-seg'); extra.setAttribute('aria-label', 'AI ' + seat.name + ' play style');
        styles.forEach(function (n) { var o = el('option', null, n); o.value = n; extra.appendChild(o); });
        if (rivalsOff) { extra.disabled = true; extra.title = rivalsOff; }
        extra.addEventListener('change', function () { var arr = state.rivalStyles.slice(); arr[k] = extra.value; set('rivalStyles', arr); });
        groups['style' + k] = extra;
      }
      seg('seat' + k, 'AI ' + seat.name, SEAT_LEVELS, String, { extra: extra,
        into: seatBox, color: seat.color, level: function (val) { return val === 'Off' ? null : val; },
        disabled: rivalsOff, showOff: k === seats.length - 1,   // the reason once, under the last seat
        onPick: function (val) { var arr = state.rivals.slice(); arr[k] = val; set('rivals', arr); }
      });
    });
    seg('camera', 'Camera', CAMERAS, String, { into: sceneRoot, tip: function (val) { return CAMERA_TIPS[val]; } });
    seg('speed', 'Speed', SPEEDS, function (val) { return val + '\u00d7'; }, { into: sceneRoot });
    check('thinking', 'Show AI thinking', sup.thinking);
    check('autoRestart', 'Restart each race on its own', sup.autoRestart);
    if (cfg.levelNote) root.appendChild(el('p', 'arp-note', cfg.levelNote));

    host.textContent = ''; host.appendChild(root);
    if (cfg.sceneHost) { cfg.sceneHost.textContent = ''; cfg.sceneHost.appendChild(sceneRoot); }

    function sync() {
      function press(box, val) {   // select the option and colour the dropdown by its level
        box.value = String(val);
        var o = box.options[box.selectedIndex], lv = o && o.getAttribute('data-lv');
        if (lv) box.setAttribute('data-lv', lv); else if (String(val) === 'Off') box.setAttribute('data-lv', 'Off'); else box.removeAttribute('data-lv');
      }
      ['course', 'you', 'camera', 'speed'].forEach(function (k) { if (groups[k]) press(groups[k], state[k]); });
      if (!PL) seats.forEach(function (_, k) { press(groups['seat' + k], state.rivals[k]); if (styles) groups['style' + k].value = state.rivalStyles[k]; });
      buildPlayers();
      groups.thinking.checked = !!state.thinking; groups.autoRestart.checked = !!state.autoRestart;
    }
    function set(key, val, silent) {
      if (!(key in state)) return;
      if (key === 'course') { var c = null; for (var i = 0; i < courses.length; i++) if (String(courses[i].v) === String(val)) c = courses[i].v; if (c == null) return; val = c; }
      if (key === 'rivals') { if (!Array.isArray(val) || sup.rivals !== true) return; val = seatLevels(val); }
      if (key === 'players') { if (!PL || !Array.isArray(val)) return; val = cleanPlayers(val); }
      if (key === 'view') { if (!PL) return; val = Math.max(0, Math.min(state.players.length - 1, +val || 0)); }
      if (key === 'rivalStyles') { if (!styles || !Array.isArray(val)) return; val = seatStyles(val); }
      if (key === 'speed') { val = +val; if (SPEEDS.indexOf(val) < 0) return; }
      if (key === 'you' && ['You'].concat(LEVELS).indexOf(val) < 0) return;
      if (key === 'camera' && CAMERAS.indexOf(val) < 0) return;
      if (key === 'thinking' || key === 'autoRestart') val = !!val;
      var arrKey = key === 'rivals' || key === 'rivalStyles' || key === 'players';
      var changed = key === 'players' ? JSON.stringify(state.players) !== JSON.stringify(val) : arrKey ? state[key].join() !== val.join() : state[key] !== val;
      state[key] = val;
      if (key === 'players') { derive(); if (state.view >= val.length) state.view = 0; }
      sync();
      if (changed && !silent && typeof cfg.onChange === 'function') { var c = Object.assign({}, state); c.rivals = state.rivals.slice(); if (styles) c.rivalStyles = state.rivalStyles.slice(); if (PL) c.players = JSON.parse(JSON.stringify(state.players)); cfg.onChange(key, key === 'players' ? JSON.parse(JSON.stringify(val)) : arrKey ? val.slice() : val, c); }
    }
    sync();
    return {
      get state() { var c = Object.assign({}, state); c.rivals = state.rivals.slice(); if (styles) c.rivalStyles = state.rivalStyles.slice(); if (PL) c.players = JSON.parse(JSON.stringify(state.players)); return c; },
      set: set,
      refresh: sync,
      element: root
    };
  }

  window.StudioRacePanel = { mount: mount, LEVELS: LEVELS.slice(), SEAT_LEVELS: SEAT_LEVELS.slice(), CAMERAS: CAMERAS.slice(), SPEEDS: SPEEDS.slice() };
})();
