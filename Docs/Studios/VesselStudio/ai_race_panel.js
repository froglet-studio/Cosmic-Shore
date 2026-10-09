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
 *   thinking     boolean                                (draw each AI's target: believed vs real)
 *   autoRestart  boolean                                (start the next race on its own)
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
  var DEFAULT_SEATS = [{ name: 'Ruby', color: '#ff4f7b' }, { name: 'Gold', color: '#ffc247' }, { name: 'Blue', color: '#6aa8ff' }];
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
    '.arp-seg{display:flex;border:1px solid var(--line,#262b47);border-radius:6px;overflow:hidden;min-width:0}',
    '.arp-seg button{flex:1;border:0;background:transparent;color:inherit;padding:5px 4px;font:inherit;font-size:12px;min-width:0;cursor:pointer}',
    '.arp-seg button[aria-pressed="true"]{background:var(--panel-2,#181c30);color:var(--accent,var(--jade,#35e0b0));font-weight:600}',
    '.arp-seg button[data-lv="Easy"][aria-pressed="true"]{color:var(--easy,#4be38f)}',
    '.arp-seg button[data-lv="Medium"][aria-pressed="true"]{color:var(--medium,#ffb454)}',
    '.arp-seg button[data-lv="Hard"][aria-pressed="true"]{color:var(--hard,#ff5c6c)}',
    '.arp-seg button:disabled{opacity:.4;cursor:default}',
    '.arp-off{font-size:11px;color:var(--muted,var(--dim,#8e95bf));grid-column:2}',
    '.arp-check{display:flex;gap:8px;align-items:center;font-size:13px}',
    '.arp-check input:disabled+span{opacity:.45}',
    '.arp-note{font-size:12px;color:var(--muted,var(--dim,#8e95bf));margin:0}',
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
    if (sup.rivals !== true) state.rivals = seats.map(function () { return 'Off'; });

    var root = el('div', 'arp'); root.setAttribute('data-arp', '');
    var groups = {};
    function seg(key, label, values, text, opts) {
      opts = opts || {};
      var row = el('div', 'arp-row'), name = el('span', null, label), box = el('div', 'arp-seg');
      box.setAttribute('role', 'group'); box.setAttribute('aria-label', label);
      values.forEach(function (val) {
        var b = el('button', null, text(val)); b.type = 'button'; b.setAttribute('data-v', String(val));
        if (opts.level) { var lv = opts.level(val); if (lv) b.setAttribute('data-lv', lv); }
        if (opts.tip) b.title = opts.tip(val);
        if (opts.disabled) { b.disabled = true; b.title = opts.disabled; }
        b.addEventListener('click', function () { if (opts.onPick) opts.onPick(val); else set(key, val); });
        box.appendChild(b);
      });
      row.appendChild(name); row.appendChild(box);
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
      for (var i = 0; i < courses.length; i++) if (courses[i].v === val) return courses[i].label || String(val);
      return String(val);
    }, { tip: function (val) { for (var i = 0; i < courses.length; i++) if (courses[i].v === val) return courses[i].note || ''; return ''; } });
    seg('you', 'Your hull', ['You'].concat(LEVELS), function (val) { return val === 'You' ? 'You fly' : 'AI ' + val; },
      { level: function (val) { return val === 'You' ? null : val; } });
    // one row per AI rival seat, each with its own level (D20)
    var rivalsOff = sup.rivals === true ? null : why(sup.rivals);
    var seatBox = el('div', 'arp-seats'); seatBox.setAttribute('aria-label', 'AI rivals, each with its own level'); root.appendChild(seatBox);
    seats.forEach(function (seat, k) {
      seg('seat' + k, 'AI ' + seat.name, SEAT_LEVELS, String, {
        into: seatBox, color: seat.color, level: function (val) { return val === 'Off' ? null : val; },
        disabled: rivalsOff, showOff: k === seats.length - 1,   // the reason once, under the last seat
        onPick: function (val) { var arr = state.rivals.slice(); arr[k] = val; set('rivals', arr); }
      });
    });
    seg('camera', 'Camera', CAMERAS, String, { tip: function (val) { return CAMERA_TIPS[val]; } });
    seg('speed', 'Speed', SPEEDS, function (val) { return val + '\u00d7'; });
    check('thinking', 'Show AI thinking', sup.thinking);
    check('autoRestart', 'Restart each race on its own', sup.autoRestart);
    if (cfg.levelNote) root.appendChild(el('p', 'arp-note', cfg.levelNote));

    host.textContent = ''; host.appendChild(root);

    function sync() {
      function press(box, val) { for (var i = 0; i < box.children.length; i++) { var b = box.children[i]; b.setAttribute('aria-pressed', String(b.getAttribute('data-v') === String(val))); } }
      ['course', 'you', 'camera', 'speed'].forEach(function (k) { if (groups[k]) press(groups[k], state[k]); });
      seats.forEach(function (_, k) { press(groups['seat' + k], state.rivals[k]); });
      groups.thinking.checked = !!state.thinking; groups.autoRestart.checked = !!state.autoRestart;
    }
    function set(key, val, silent) {
      if (!(key in state)) return;
      if (key === 'course') { var c = null; for (var i = 0; i < courses.length; i++) if (String(courses[i].v) === String(val)) c = courses[i].v; if (c == null) return; val = c; }
      if (key === 'rivals') { if (!Array.isArray(val) || sup.rivals !== true) return; val = seatLevels(val); }
      if (key === 'speed') { val = +val; if (SPEEDS.indexOf(val) < 0) return; }
      if (key === 'you' && ['You'].concat(LEVELS).indexOf(val) < 0) return;
      if (key === 'camera' && CAMERAS.indexOf(val) < 0) return;
      if (key === 'thinking' || key === 'autoRestart') val = !!val;
      var changed = key === 'rivals' ? state.rivals.join() !== val.join() : state[key] !== val;
      state[key] = val; sync();
      if (changed && !silent && typeof cfg.onChange === 'function') { var c = Object.assign({}, state); c.rivals = state.rivals.slice(); cfg.onChange(key, key === 'rivals' ? val.slice() : val, c); }
    }
    sync();
    return {
      get state() { var c = Object.assign({}, state); c.rivals = state.rivals.slice(); return c; },
      set: set,
      refresh: sync,
      element: root
    };
  }

  window.StudioRacePanel = { mount: mount, LEVELS: LEVELS.slice(), SEAT_LEVELS: SEAT_LEVELS.slice(), CAMERAS: CAMERAS.slice(), SPEEDS: SPEEDS.slice() };
})();
