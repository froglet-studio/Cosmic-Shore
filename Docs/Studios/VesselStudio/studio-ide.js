/* The Vessel Studio editor layout (/vessel-studio D8): the game view in the middle, every panel a tab in a dock
   around it (right and bottom), any tab pops out into a floating window you drag, resize and dock back, splitters
   resize the docks, and the page itself never scrolls. Taken from the Stoat Flight Studio's "editor layout" section
   so every studio works the same way. Below 900 px wide (phones, narrow windows) the docks stack under the stage and
   the page scrolls, as the Stoat's do.

     VesselStudioIDE.build({
       key: 'squirrel-studio-ide',                 // remembered per browser: tabs, sizes, open windows
       host: document.querySelector('.layout'),    // emptied and rebuilt as the layout
       stage: document.getElementById('viewport'), // the game view (fills the middle)
       right: [['race', 'Race', [el, el2]], ...],  // tab key, title, the elements that tab shows
       bottom: [['score', 'Scorecard', [el]], ...],
       railW: 380, dockH: 230,                     // defaults
       winSize: { score: [900, 380] },             // a tab's popped-out window size
       onLayout() {},                              // called after any resize (the stage re-measures itself)
     }) -> { openWindow(key), dockWindow(key, select), selectTab(key), state }

   Colours come from the page's own tokens (--panel, --panel-2, --line, --fg, --muted, --accent) with fallbacks.
   ASCII only: served beside the pages without a charset. */
(function () {
  'use strict';
  const CSS = [
    'body.vside-on { height: 100vh; height: 100dvh; overflow: hidden; margin: 0; }',
    'body.vside-on .wrap { box-sizing: border-box; max-width: none; height: 100%; padding-block: 6px; padding-inline: 8px; display: grid; grid-template-rows: auto minmax(0, 1fr); gap: 6px; }',
    '.vside { display: grid !important; grid-template-columns: minmax(0, 1fr) 6px var(--vside-railW, 380px); gap: 0 !important; min-height: 0; height: 100%; align-items: stretch !important; }',
    '.vside-main { display: grid; grid-template-rows: minmax(0, 1fr) 6px var(--vside-dockH, 230px); min-height: 0; min-width: 0; height: 100%; }',
    '.vside-main > .vside-stage { min-height: 0; height: 100% !important; aspect-ratio: auto !important; }',
    '.vside-split { position: relative; z-index: 2; touch-action: none; }',
    '.vside-split::after { content: ""; position: absolute; background: var(--line, #262b47); border-radius: 2px; }',
    '.vside-split.v { cursor: col-resize; } .vside-split.v::after { left: 2px; width: 2px; top: 30%; bottom: 30%; }',
    '.vside-split.h { cursor: row-resize; } .vside-split.h::after { top: 2px; height: 2px; left: 40%; right: 40%; }',
    '.vside-split:hover::after, .vside-split.drag::after { background: var(--accent, #35e0b0); }',
    '.vside-dock { display: grid !important; grid-template-rows: auto minmax(0, 1fr); gap: 0 !important; min-height: 0; min-width: 0; background: var(--panel, #121524); border: 1px solid var(--line, #262b47); border-radius: 8px; overflow: hidden; }',
    '.vside-tabs { display: flex; flex-wrap: wrap; gap: 2px; padding: 4px 4px 0; background: var(--panel-2, #181c30); border-bottom: 1px solid var(--line, #262b47); }',
    '.vside-tabs button { font: 600 11px/1 var(--display, system-ui, sans-serif); letter-spacing: .05em; text-transform: uppercase; color: var(--muted, #8e95bf); background: transparent; border: 1px solid transparent; border-bottom: 0; border-radius: 6px 6px 0 0; padding: 7px 9px; cursor: pointer; }',
    '.vside-tabs button:hover { color: var(--fg, #e9ecff); }',
    '.vside-tabs button[aria-selected="true"] { color: var(--accent, #35e0b0); background: var(--panel, #121524); border-color: var(--line, #262b47); margin-bottom: -1px; }',
    '.vside-tabs button.floating { font-style: italic; } .vside-tabs button.floating::after { content: " \\29c9"; }',
    '.vside-panes { min-height: 0; display: grid; }',
    '.vside-pane { min-height: 0; min-width: 0; overflow-y: auto; overflow-x: hidden; padding: 10px; display: grid; grid-template-columns: minmax(0, 1fr); gap: 10px; align-content: start; }',
    '.vside-pane > * { min-width: 0; max-width: 100%; }',
    '.vside-pane > .panel { border: 0; padding: 0; background: transparent; }',
    '.vside-bar { display: flex; justify-content: flex-end; margin-bottom: -4px; }',
    '.vside-bar button, .vside-win .vside-fbar button { font: inherit; font-size: 12px; color: var(--muted, #8e95bf); background: transparent; border: 1px solid var(--line, #262b47); border-radius: 5px; padding: 2px 8px; cursor: pointer; }',
    '.vside-bar button:hover, .vside-win .vside-fbar button:hover { color: var(--fg, #e9ecff); border-color: var(--accent, #35e0b0); }',
    '.vside-win { position: fixed; z-index: 60; display: grid; grid-template-rows: auto minmax(0, 1fr); min-width: 260px; min-height: 140px; max-width: calc(100vw - 16px); max-height: calc(100vh - 16px); resize: both; overflow: hidden; background: var(--panel, #121524); border: 1px solid var(--line, #262b47); border-radius: 8px; box-shadow: 0 10px 40px #000a; }',
    '.vside-win .vside-fbar { display: flex; align-items: center; gap: 8px; padding: 5px 8px; background: var(--panel-2, #181c30); border-bottom: 1px solid var(--line, #262b47); cursor: move; user-select: none; touch-action: none; }',
    '.vside-win .vside-fbar b { font: 700 12px/1 var(--display, system-ui, sans-serif); letter-spacing: .06em; text-transform: uppercase; color: var(--accent, #35e0b0); flex: 1; }',
    '@media (max-width: 900px) {',
    '  body.vside-on { height: auto; overflow: auto; }',
    '  body.vside-on .wrap { height: auto; display: flex; flex-direction: column; }',
    '  .vside { grid-template-columns: minmax(0, 1fr); height: auto; }',
    '  .vside-main { grid-template-rows: auto auto auto; height: auto; }',
    '  .vside-main > .vside-stage { height: 58vh !important; }',
    '  .vside-split { display: none; }',
    '  .vside-dock { max-height: 70vh; }',
    '}',
    // touch play (body.play) puts the stage full screen: the dock's stage height must not hold it to 58vh on a phone
    'body.play .vside-main > .vside-stage { height: auto !important; }',
  ].join('\n');

  function build(o) {
    if (!document.getElementById('vside-css')) { const st = document.createElement('style'); st.id = 'vside-css'; st.textContent = CSS; document.head.appendChild(st); }
    const KEY = o.key || 'vessel-studio-ide';
    let saved = {}; try { saved = JSON.parse(localStorage.getItem(KEY) || '{}') || {}; } catch (e) { saved = {}; }
    const st = Object.assign({ R: o.right[0] && o.right[0][0], B: o.bottom[0] && o.bottom[0][0], railW: o.railW || 380, dockH: o.dockH || 230, wins: {} }, saved);
    const save = () => { try { localStorage.setItem(KEY, JSON.stringify(st)); } catch (e) { /* private window */ } };
    const TAB = {}; let winZ = 60;
    const host = o.host, main = document.createElement('div'), dockR = document.createElement('div'), dockB = document.createElement('div');
    const sv = document.createElement('div'), sh = document.createElement('div');
    sv.className = 'vside-split v'; sh.className = 'vside-split h'; sv.setAttribute('aria-hidden', 'true'); sh.setAttribute('aria-hidden', 'true');
    main.className = 'vside-main'; dockR.className = 'vside-dock vside-dockR'; dockB.className = 'vside-dock vside-dockB';
    dockR.setAttribute('aria-label', 'Settings'); dockB.setAttribute('aria-label', 'Records');
    o.stage.classList.add('vside-stage');
    const make = (box, d, list) => {
      const tabs = document.createElement('div'), panes = document.createElement('div');
      tabs.className = 'vside-tabs'; tabs.setAttribute('role', 'tablist'); panes.className = 'vside-panes';
      for (const [key, title, els] of list) {
        const btn = document.createElement('button'); btn.type = 'button'; btn.textContent = title; btn.setAttribute('role', 'tab'); btn.dataset.tab = key;
        const pane = document.createElement('div'); pane.className = 'vside-pane'; pane.setAttribute('role', 'tabpanel'); pane.dataset.tab = key;
        const bar = document.createElement('div'); bar.className = 'vside-bar';
        bar.innerHTML = '<button type="button" title="Open this tab in its own window">\u29c9 Pop out</button>';
        bar.firstChild.addEventListener('click', () => openWindow(key));
        pane.appendChild(bar);
        for (const el of els) { if (!el) continue; collapsible(el, o.key); pane.appendChild(el); }
        btn.addEventListener('click', () => { if (TAB[key].win) { raise(key); return; } selectTab(key); });
        tabs.appendChild(btn); panes.appendChild(pane);
        TAB[key] = { dock: d, title, btn, pane, panes, win: null };
      }
      box.appendChild(tabs); box.appendChild(panes);
    };
    // take every element first (some live in the host we are about to empty), then rebuild
    make(dockR, 'R', o.right); make(dockB, 'B', o.bottom);
    host.textContent = ''; host.classList.add('vside');
    main.append(o.stage, sh, dockB); host.append(main, sv, dockR);
    document.body.classList.add('vside-on');
    const apply = () => { document.body.style.setProperty('--vside-railW', st.railW + 'px'); document.body.style.setProperty('--vside-dockH', st.dockH + 'px'); if (o.onLayout) o.onLayout(); };
    const bindSplit = (el, k, measure, lo, hi, box) => {
      el.addEventListener('pointerdown', (e) => {
        el.classList.add('drag'); try { el.setPointerCapture(e.pointerId); } catch (err) { /* synthetic */ }
        const move = (ev) => { const r = box.getBoundingClientRect(); st[k] = Math.round(Math.max(lo, Math.min(hi, measure(k === 'railW' ? ev.clientX : ev.clientY, r)))); apply(); };
        const up = () => { el.classList.remove('drag'); el.removeEventListener('pointermove', move); el.removeEventListener('pointerup', up); save(); };
        el.addEventListener('pointermove', move); el.addEventListener('pointerup', up); e.preventDefault();
      });
    };
    bindSplit(sv, 'railW', (x, r) => r.right - x, 260, 720, host);
    bindSplit(sh, 'dockH', (y, r) => r.bottom - y, 100, 620, main);

    function selectTab(key) {
      const t = TAB[key]; if (!t) return;
      if (t.win) { raise(key); return; }
      for (const k in TAB) { const x = TAB[k]; if (x.dock !== t.dock || x.win) continue; const on = k === key; x.btn.setAttribute('aria-selected', String(on)); x.pane.hidden = !on; }
      st[t.dock] = key; save();
    }
    const firstDocked = (dock, not) => { for (const k in TAB) if (TAB[k].dock === dock && !TAB[k].win && k !== not) return k; return null; };
    const remember = (key) => { const w = TAB[key].win; if (!w) return; st.wins[key] = { x: w.offsetLeft, y: w.offsetTop, w: w.offsetWidth, h: w.offsetHeight, open: true }; save(); };
    const raise = (key) => { const w = TAB[key].win; if (w) w.style.zIndex = String(++winZ); };
    function openWindow(key) {
      const t = TAB[key]; if (!t) return;
      if (t.win) { raise(key); return; }
      const g = st.wins[key] || {}, vw = window.innerWidth, vh = window.innerHeight, def = (o.winSize && o.winSize[key]) || [420, 520];
      const w = Math.min(g.w || def[0], vw - 16), h = Math.min(g.h || def[1], vh - 16);
      const win = document.createElement('div'); win.className = 'vside-win'; win.setAttribute('role', 'dialog'); win.setAttribute('aria-label', t.title);
      win.style.width = w + 'px'; win.style.height = h + 'px';
      win.style.left = Math.max(8, Math.min(vw - w - 8, g.x != null ? g.x : (vw - w) / 2)) + 'px'; win.style.top = Math.max(8, Math.min(vh - 60, g.y != null ? g.y : 70)) + 'px';
      win.innerHTML = '<div class="vside-fbar"><b></b><button type="button" data-a="dock" title="Put it back in its tab">\u21f2 Dock</button><button type="button" data-a="close" title="Close (back to its tab)">\u2715</button></div>';
      win.querySelector('b').textContent = t.title;
      t.pane.querySelector('.vside-bar').hidden = true; t.pane.hidden = false; win.appendChild(t.pane);
      document.body.appendChild(win); t.win = win; t.btn.classList.add('floating'); t.btn.setAttribute('aria-selected', 'false');
      win.querySelector('[data-a="dock"]').addEventListener('click', () => dockWindow(key, true));
      win.querySelector('[data-a="close"]').addEventListener('click', () => dockWindow(key, false));
      win.addEventListener('pointerdown', () => raise(key));
      const bar = win.querySelector('.vside-fbar');
      bar.addEventListener('pointerdown', (e) => {
        if (e.target.closest('button')) return;
        const ox = e.clientX - win.offsetLeft, oy = e.clientY - win.offsetTop; try { bar.setPointerCapture(e.pointerId); } catch (err) { /* synthetic */ }
        const move = (ev) => { win.style.left = Math.max(0, Math.min(window.innerWidth - 80, ev.clientX - ox)) + 'px'; win.style.top = Math.max(0, Math.min(window.innerHeight - 40, ev.clientY - oy)) + 'px'; };
        const up = () => { bar.removeEventListener('pointermove', move); bar.removeEventListener('pointerup', up); remember(key); };
        bar.addEventListener('pointermove', move); bar.addEventListener('pointerup', up); e.preventDefault();
      });
      if (window.ResizeObserver) new ResizeObserver(() => remember(key)).observe(win);
      if (st[t.dock] === key) { const k = firstDocked(t.dock, key); if (k) selectTab(k); }
      raise(key); remember(key);
    }
    function dockWindow(key, select) {
      const t = TAB[key]; if (!t || !t.win) return;
      t.pane.querySelector('.vside-bar').hidden = false; t.panes.appendChild(t.pane); t.win.remove(); t.win = null; t.btn.classList.remove('floating');
      if (st.wins[key]) st.wins[key].open = false; save();
      if (select || st[t.dock] === key) selectTab(key); else t.pane.hidden = true;
    }
    apply();
    selectTab(TAB[st.R] ? st.R : o.right[0][0]); selectTab(TAB[st.B] ? st.B : o.bottom[0][0]);
    for (const k in st.wins) if (st.wins[k] && st.wins[k].open && TAB[k]) openWindow(k);
    return { openWindow, dockWindow, selectTab, state: st, tabs: TAB };
  }

  // ---- every section in a tab folds: a - / + at its heading (the user, 2026-10-09; /vessel-studio D22) ----
  // A <details> keeps its own <summary> as the heading; any other section folds on its first heading (h2-h4,
  // or an element marked .vsc-head). Open by default; each section's state is remembered per studio and id.
  const FOLD_CSS = [
    'details.vsc > summary { display: flex !important; justify-content: space-between; align-items: baseline; gap: 8px; cursor: pointer; list-style: none; }',
    'details.vsc > summary::-webkit-details-marker { display: none; }',
    'details.vsc > summary::after { content: "\\2212"; font: 700 14px/1 ui-monospace, monospace; color: var(--muted, var(--dim, #8e95bf)); margin-left: auto; }',
    'details.vsc:not([open]) > summary::after { content: "+"; }',
    '.vsc-head { display: flex !important; align-items: center; gap: 8px; }',
    '.vsc-btn { margin-left: auto; flex: none; width: 22px; height: 22px; padding: 0; border: 1px solid var(--line, #262b47); border-radius: 5px; background: transparent; color: var(--muted, var(--dim, #8e95bf)); font: 700 14px/1 ui-monospace, monospace; cursor: pointer; }',
    '.vsc-btn:hover { color: var(--fg, #e9ecff); border-color: var(--accent, #35e0b0); }',
    '.vsc-closed > :not(.vsc-head) { display: none !important; }',
  ].join('\n');
  function foldStore(key) {
    const k = (key || 'vessel-studio') + ':fold';
    let m = {}; try { m = JSON.parse(localStorage.getItem(k) || '{}') || {}; } catch (e) { m = {}; }
    return {
      get: (id) => !!(id && m[id]),
      set: (id, closed) => { if (!id) return; if (closed) m[id] = 1; else delete m[id]; try { localStorage.setItem(k, JSON.stringify(m)); } catch (e) { /* storage blocked */ } },
    };
  }
  const stores = {};
  function collapsible(el, key) {
    if (!el || el.dataset.vsc) return el;
    if (!document.getElementById('vsc-css')) { const st = document.createElement('style'); st.id = 'vsc-css'; st.textContent = FOLD_CSS; (document.head || document.documentElement).appendChild(st); }
    const store = stores[key] || (stores[key] = foldStore(key)), id = el.id;
    el.dataset.vsc = '1';
    if (el.tagName === 'DETAILS') {
      el.classList.add('vsc'); el.open = !store.get(id);
      el.addEventListener('toggle', () => store.set(id, !el.open));
      return el;
    }
    const head = el.querySelector(':scope > h2, :scope > h3, :scope > h4, :scope > .vsc-head');
    if (!head) return el;   // a section with no heading has nothing to fold on
    head.classList.add('vsc-head');
    const btn = document.createElement('button'); btn.type = 'button'; btn.className = 'vsc-btn';
    const show = (closed) => { el.classList.toggle('vsc-closed', closed); btn.textContent = closed ? '+' : '\u2212'; btn.title = closed ? 'Expand' : 'Minimize'; btn.setAttribute('aria-expanded', String(!closed)); };
    btn.addEventListener('click', (e) => { e.stopPropagation(); const closed = !el.classList.contains('vsc-closed'); show(closed); store.set(id, closed); });
    head.appendChild(btn); show(store.get(id));
    return el;
  }

  // ---- the platform, answered once at load (/vessel-studio D9, D26) ----
  // A studio never asks "play on phone?": it detects the device and opens that device's interface only.
  // A host (Amoebius) may say it: window.__studioHost = { shell, device } or the page hash #amoebius / #prisma.
  // Phone = a mobile browser (UA, userAgentData, iPadOS posing as a Mac) or a touch-only screen; a touchscreen
  // laptop still has a fine pointer, so it is a PC. Sets body.dev-pc / body.dev-phone for CSS.
  function platform() {
    const host = window.__studioHost || (/(^|[#&])(amoebius|prisma)\b/i.test(location.hash) ? { shell: 'amoebius' } : {});
    const ua = navigator.userAgent || '', mq = (q) => { try { return window.matchMedia(q).matches; } catch (e) { return false; } };
    const uaMobile = (navigator.userAgentData && navigator.userAgentData.mobile === true) || /Android|iPhone|iPad|iPod|Mobile|Silk|Kindle|Opera Mini/i.test(ua)
      || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1);
    const touchOnly = mq('(pointer: coarse)') && !mq('(any-pointer: fine)');
    const device = host.device === 'pc' || host.device === 'phone' ? host.device : uaMobile || touchOnly ? 'phone' : 'pc';
    document.body.classList.toggle('dev-pc', device === 'pc'); document.body.classList.toggle('dev-phone', device === 'phone');
    return { shell: host.shell || 'web', device, why: host.device ? 'host' : uaMobile ? 'mobile browser' : touchOnly ? 'touch-only screen' : 'mouse / trackpad' };
  }

  // ---- one section at a time: a dropdown picks which section of a tab shows (/vessel-studio D27) ----
  // For a tab of many unrelated sections (Vessel Config): the tab stays clean, one section visible, the rest a pick
  // away. The choice is remembered per studio and tab. A pane's own bar (pop-out) is left alone.
  function sectionPicker(pane, key, tab) {
    if (!pane || pane.dataset.vsp) return null;
    const secs = [...pane.children].filter((el) => !el.matches('.vside-bar, .panebar, .vsp'));
    if (!secs.length) return null;
    pane.dataset.vsp = '1';
    const label = (el) => {
      const h = el.querySelector(':scope > summary, :scope > h2, :scope > h3, :scope > h4, :scope > .vsc-head');
      const c = (h || el).cloneNode(true); c.querySelectorAll('button, .vsc-btn').forEach((b) => b.remove());
      return (c.textContent || el.id || 'Section').replace(/\s+/g, ' ').trim().slice(0, 48);
    };
    const k = (key || 'vessel-studio') + ':pick:' + (tab || pane.id || 'tab');
    let cur = 0; try { cur = Math.max(0, Math.min(secs.length - 1, +localStorage.getItem(k) || 0)); } catch (e) { cur = 0; }
    const row = document.createElement('label'); row.className = 'vsp';
    row.style.cssText = 'display:grid;grid-template-columns:auto minmax(0,1fr);gap:8px;align-items:center;margin:0 0 8px;font-size:12px;color:var(--muted,#8e95bf)';
    const sel = document.createElement('select'); sel.setAttribute('aria-label', 'Section');
    sel.style.cssText = 'width:100%;min-width:0;background:var(--panel-2,#181c30);color:var(--fg,#e9ecff);border:1px solid var(--line,#262b47);border-radius:6px;padding:5px 6px;font:inherit;font-size:12px';
    secs.forEach((el, i) => { const o = document.createElement('option'); o.value = String(i); o.textContent = label(el); sel.appendChild(o); });
    const show = (i) => { secs.forEach((el, j) => { el.hidden = j !== i; if (j === i && el.tagName === 'DETAILS') el.open = true; }); sel.value = String(i); };
    sel.addEventListener('change', () => { const i = +sel.value; show(i); try { localStorage.setItem(k, String(i)); } catch (e) { /* storage blocked */ } });
    row.appendChild(document.createTextNode('Section')); row.appendChild(sel);
    pane.insertBefore(row, secs[0]); show(cur);
    return { select: sel, show };
  }

  window.VesselStudioIDE = { build, collapsible, platform, sectionPicker };
})();
