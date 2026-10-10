/* studio-theme.js - the ONE look of every Vessel Studio page (/vessel-studio D29, the user 2026-10-10: "the fonts,
 * spacings, line heights and font sizes are the same in every studio, so it reads as one vessel studio").
 *
 * The Stoat Flight Studio is the reference: its fonts (Chakra Petch for headings, labels and numbers; Atkinson
 * Hyperlegible for reading text), its 15px / 1.45 body, its card, heading, control and button metrics, and its colour
 * tokens with their light-theme twins. This file carries those values once; a studio loads it in <head> AFTER its own
 * <style>, so the shared rules win over a page that drifted, and the page keeps only what is truly its own (layout,
 * stage HUD, vessel-specific widgets).
 *
 *   <style> ...page layout... </style>
 *   <script src="studio-theme.js"></script>      (a source one folder up uses VesselStudio/studio-theme.js)
 *
 * Token names: the Stoat's (--font-display, --font-body, --dim, --accent ...) plus the aliases the shared scripts and
 * older pages read (--display, --body, --mono, --muted), so every page and every shared script resolves to the same
 * values. Numbers use the display face with tabular figures, as the Stoat does; there is no separate mono font.
 * --accent is the studio chrome colour (the Stoat amber), the same in every studio; domain colours (studio-domains.js)
 * are for hulls, players and lines, never chrome.
 *
 * ASCII only (served beside the pages without a charset).
 */
(function () {
  'use strict';
  if (window.VesselStudioTheme) return;
  var FONTS = 'https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@500;700&family=Atkinson+Hyperlegible:ital,wght@0,400;0,700;1,400&display=swap';
  var LIGHT = '--bg: #eef1fb; --panel: #ffffff; --panel-2: #f5f7ff; --line: #c9d0ee; --fg: #141a3a; --dim: #4b567f; --accent: #a65f00; --accent-2: #0a6fa8; --good: #1f8a4c; --warn: #8a6100; --bad: #b3243a; color-scheme: light;';
  var CSS = [
    // ---- tokens: the Stoat's, dark first (the stage is the HyperSea at night), the chrome follows the viewer ----
    ':root {',
    '  --bg: #0b1026; --panel: #121a3a; --panel-2: #0e1531; --line: #27336a; --fg: #e8ecff; --dim: #9aa6d6;',
    '  --accent: #ffb347; --accent-2: #7fd7ff; --good: #6fe39a; --warn: #ffcf5c; --bad: #ff7a8a;',
    '  --font-display: "Chakra Petch", "Segoe UI", system-ui, sans-serif;',
    '  --font-body: "Atkinson Hyperlegible", "Segoe UI", system-ui, sans-serif;',
    '  --display: var(--font-display); --body: var(--font-body); --mono: var(--font-display); --muted: var(--dim);',
    '  color-scheme: dark;',
    '}',
    '@media (prefers-color-scheme: light) { :root:not([data-theme="dark"]) { ' + LIGHT + ' } }',
    ':root[data-theme="light"] { ' + LIGHT + ' }',
    // ---- type ----
    'body { background: var(--bg); color: var(--fg); font-family: var(--font-body); font-size: 15px; line-height: 1.45; }',
    'button, input, select, textarea { font-family: inherit; color: inherit; }',
    'h1 { font-family: var(--font-display); font-weight: 700; font-size: 1.5rem; line-height: 1.2; margin: 0; letter-spacing: 0.01em; text-transform: none; text-wrap: balance; }',
    'h2 { font-family: var(--font-display); font-weight: 700; font-size: 1.1rem; line-height: 1.25; margin: 0; letter-spacing: 0.03em; text-transform: none; text-wrap: balance; }',
    'a { color: var(--accent); }',
    ':focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }',
    'code { font-size: 0.88em; background: var(--panel-2); border: 1px solid var(--line); border-radius: 4px; padding: 0 4px; }',
    'kbd { font-family: var(--font-display); font-size: 0.8em; border: 1px solid var(--line); border-bottom-width: 2px; border-radius: 4px; padding: 0 5px; background: var(--panel-2); }',
    '.round { font-family: var(--font-display); font-size: 0.8rem; font-weight: 500; line-height: 1.2; letter-spacing: 0.12em; text-transform: uppercase; color: var(--accent); border: 0; padding: 0; border-radius: 0; }',
    'header p, .sub { color: var(--dim); }',
    '.note, .hint { color: var(--dim); font-size: 0.82rem; }',
    // ---- cards and panels: one box, one heading style ----
    '.card, .panel { background: var(--panel); border: 1px solid var(--line); border-radius: 8px; padding: 10px 12px; min-width: 0; }',
    '.card h3, .panel h3 { margin: 0 0 6px; font-family: var(--font-display); font-weight: 700; font-size: 0.95rem; line-height: 1.25; letter-spacing: 0.04em; text-transform: uppercase; color: var(--fg); }',
    // a section title inside a settings tab reads the same whether the page built it as <details> or as a panel
    '[role=tabpanel] .panel > h3, [role=tabpanel] .card > h3, .vside-win .panel > h3, .fwin .card > h3 { font-size: 0.9rem; line-height: 1.45; margin: 0 0 6px; }',
    '.card h2, .panel h2 { font-family: var(--font-display); font-weight: 700; font-size: 1.1rem; letter-spacing: 0.03em; text-transform: none; }',
    'details.card > summary { cursor: pointer; font-family: var(--font-display); font-weight: 700; font-size: 0.9rem; letter-spacing: 0.04em; text-transform: uppercase; }',
    'details.card > summary small, .card h3 small, .panel h3 small, .card h3 > span, .panel h3 > span { font-family: var(--font-body); font-weight: 400; text-transform: none; letter-spacing: 0; color: var(--dim); font-size: 0.78rem; }',
    // ---- controls ----
    'select, input[type=text], input[type=number], textarea { font-family: var(--font-body); font-size: 0.9rem; background: var(--panel-2); color: var(--fg); border: 1px solid var(--line); border-radius: 6px; padding: 5px 7px; }',
    '.btn, button.pick { font-family: var(--font-body); font-weight: 700; font-size: 0.85rem; line-height: 1.2; background: transparent; color: var(--fg); border: 1px solid var(--line); border-radius: 6px; padding: 5px 10px; cursor: pointer; }',
    '.btn:hover:not([disabled]), button.pick:hover { border-color: var(--accent); }',
    '.btn.primary, button.primary { background: var(--accent); color: #1a1200; border-color: var(--accent); }',
    '.btn.small { padding: 4px 10px; font-size: 0.78rem; }',
    '.btns { gap: 6px; }',
    // ---- the transport (D18): the same bar, the same buttons, on every stage ----
    '.transport { display: flex; gap: 2px; padding: 3px; border-radius: 8px; background: rgba(8, 10, 20, 0.78); box-shadow: 0 2px 10px rgba(0, 0, 0, 0.4); }',
    '.transport button, .transport .btn { font-family: var(--font-display); font-weight: 700; font-size: 0.8rem; line-height: normal; letter-spacing: 0.06em; text-transform: uppercase; color: #e8ecff; background: transparent; border: 0; border-radius: 5px; padding: 5px 10px; cursor: pointer; }',
    '.transport button:hover:not(:disabled) { background: rgba(255, 255, 255, 0.1); }',
    '.transport button:disabled { opacity: 0.35; cursor: default; }',
    '.transport button.on, .transport .btn.on { color: #1a1200; background: var(--accent); opacity: 1; }',
    '.transport button[aria-pressed="true"] { color: #04131c; background: #7fd7ff; }',
    // ---- the start / finish card on the stage: the Stoat's box, whatever the page calls it ----
    '.overlay .box, .overlay > .card { background: rgba(8, 12, 34, 0.86); color: #e8ecff; border: 1px solid #35448a; border-radius: 10px; padding: 16px 18px; backdrop-filter: blur(3px); }',
    '.overlay .box h2, .overlay > .card h2 { color: #ffb347; }',
    '.overlay .box p, .overlay > .card p { margin: 0; color: #c3cbef; }',
    '.overlay .box kbd, .overlay > .card kbd { background: #121a3a; border-color: #35448a; color: #e8ecff; }',
    // ---- the editor layout (D8): the Stoat's compact header and its dock tabs, whichever dock builder a page uses ----
    'body.ide header h1, body.vside-on header h1 { font-size: 1.05rem; white-space: nowrap; }',
    '.vside-tabs button, .dock .tabs button { font-family: var(--font-display); font-weight: 400; font-size: 0.72rem; line-height: normal; letter-spacing: 0.05em; text-transform: uppercase; }',
    '.vside-bar button, .vside-win .vside-fbar button, .panebar button, .fwin .fbar button { font-family: var(--font-body); font-size: 0.75rem; }',
    // ---- the stage stays night whatever the page theme: anything drawn over the 3D view keeps the dark tokens ----
    '.hud, #hud, .board, .feed, .camtag, .stagebtns, .transport, .touch, #touchUI, .overlay { --fg: #e8ecff; --dim: #9aa6d6; --muted: #9aa6d6; --line: #27336a; --panel: #121a3a; --panel-2: #0e1531; color: #e8ecff; }',
    '.hud, #hud { font-family: var(--font-display); font-variant-numeric: tabular-nums; }',
  ].join('\n');

  function install() {
    var head = document.head || document.documentElement;
    if (!document.querySelector('link[data-vs-fonts]')) {
      var pc = document.createElement('link'); pc.rel = 'preconnect'; pc.href = 'https://fonts.gstatic.com'; pc.crossOrigin = ''; head.appendChild(pc);
      var l = document.createElement('link'); l.rel = 'stylesheet'; l.href = FONTS; l.setAttribute('data-vs-fonts', ''); head.appendChild(l);
    }
    var st = document.createElement('style'); st.id = 'vessel-studio-theme'; st.textContent = CSS; head.appendChild(st);
  }
  install();
  // ---- the one artifact (/vessel-studio section 0): every page is the same page wherever it is opened (claude.ai,
  // the live mirror, Unity, Amoebius). What only claude.ai can do (Ask, Requests, the shared log, Sync) says so and
  // links here, so no option is ever missing, only one click away.
  var ARTIFACT = 'https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa';
  var inClaude = !!(window.claude && typeof window.claude.use === 'function');
  function artifactLink(label) {
    var a = document.createElement('a'); a.href = ARTIFACT; a.target = '_blank'; a.rel = 'noopener';
    a.textContent = label || 'open it in the claude.ai artifact'; a.style.marginLeft = '6px';
    return a;
  }
  window.VesselStudioTheme = { fonts: FONTS, css: CSS, artifact: ARTIFACT, inClaude: inClaude, artifactLink: artifactLink };
})();
