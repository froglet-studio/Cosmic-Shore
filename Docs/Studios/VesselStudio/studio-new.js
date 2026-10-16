/* studio-new.js - the hub's "+ New studio" panel (/vessel-studio D36; the user, 2026-10-14: "a plus button beside the
 * studios: choose a vessel to create or modify, a mini-game to use or create, name the new ones, say what I want by
 * voice, and it goes straight to Claude Code on a new branch").
 *
 *   1. Vessel: one of the game's hulls (studio-game.js, generated from VesselClassType and the class assets) or a new
 *      one with its name; and what to do (its AI, how it flies, both, or a studio to explore it).
 *   2. Mini-game: one of the game's arcade cards (the hull's own first), a new one with its name, or none yet.
 *   3. What you want: typed or SPOKEN (the browser's speech recognition; where a view blocks the microphone, the
 *      keyboard's own dictation), and "Fill the form from this" (asks Claude to read the words into the choices).
 *   4. Create with Claude Code: files the request in the artifact's `requests` and starts a Claude Code session
 *      (the viewer's own, through the Claude Code Remote connector, as the Sync panel does) on a new branch off
 *      Ys-bleeding-edge, with a prompt that names the skills to follow. "Copy prompt" works anywhere.
 *
 * Outside claude.ai (the checkout, Unity's app window, the mirror) every choice works and Create says where it works,
 * with Copy prompt beside it. Closes with x, a press outside and Escape (D15). ASCII only (served without a charset).
 *
 *   VesselStudioNew.open({ vessel: 'Manta' })   VesselStudioNew.close()   .state()   .prompt()
 */
(function () {
  'use strict';
  if (window.VesselStudioNew) return;
  var ARTIFACT = 'https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa';
  var REPO_URL = 'https://github.com/froglet-studio/Cosmic-Shore', BASE = 'Ys-bleeding-edge', CCR = 'Claude Code Remote';
  var DRAFT = 'vs.newStudio.draft.v1';
  var GOALS = {
    existing: [['ai', 'Create or improve its AI'], ['fly', 'Change how it flies or its abilities'], ['both', 'Change the vessel and its AI'], ['explore', 'A studio to explore it (no game change yet)']],
    'new': [['new', 'Create the vessel and its AI'], ['newNoAi', 'Create the vessel (its AI later)']]
  };
  var G = function () { return window.VesselStudioGame || { vessels: [], games: [] }; };

  var CSS = [
    '.nsp-overlay{position:fixed;inset:0;z-index:2147482000;background:rgba(4,6,16,.66);display:grid;place-items:center;padding:16px}',
    '.nsp-overlay[hidden]{display:none!important}',
    '.nsp{position:relative;width:min(600px,100%);max-height:calc(100vh - 32px);overflow:auto;display:grid;gap:12px;background:var(--bg);color:var(--fg);',
    'border:1px solid var(--line);border-radius:10px;padding:16px 18px;box-shadow:0 10px 40px rgba(0,0,0,.6);font-family:var(--font-body);font-size:15px;line-height:1.45}',
    '.nsp h2{margin:0;padding-right:36px}.nsp h2 em{font-style:normal;color:var(--accent)}',
    '.nsp .nsp-x{position:absolute;top:10px;right:10px;width:32px;height:32px;padding:0;border-radius:6px;border:1px solid var(--line);background:transparent;color:var(--fg);font-size:18px;line-height:1;cursor:pointer}',
    '.nsp .nsp-x:hover{border-color:var(--accent)}',
    '.nsp .card{display:grid;gap:8px}',
    '.nsp-row{display:grid;grid-template-columns:86px minmax(0,1fr);gap:8px;align-items:center}',
    '.nsp-row>label{color:var(--dim);font-size:.85rem}',
    '.nsp-row select,.nsp-row input{width:100%;min-width:0;box-sizing:border-box}',
    '.nsp textarea{width:100%;min-height:96px;resize:vertical;box-sizing:border-box}',
    '.nsp .nsp-btns{display:flex;flex-wrap:wrap;gap:6px;align-items:center}',
    '.nsp .btn.rec{border-color:var(--bad);color:var(--bad)}',
    '.nsp .nsp-sum{margin:0;font-family:var(--font-display);font-size:.85rem;letter-spacing:.04em;color:var(--fg)}',
    '.nsp .nsp-state{color:var(--dim);font-size:.82rem;min-height:1.2em}',
    '.nsp .nsp-state.err{color:var(--bad)}.nsp .nsp-state.ok{color:var(--good)}',
    '.nsp [hidden]{display:none!important}'
  ].join('\n');

  var el, $ = function (id) { return document.getElementById(id); };
  var caps = null;
  function cap(name) {
    if (!caps) caps = {};
    if (!(name in caps)) caps[name] = (window.claude && typeof window.claude.use === 'function') ? window.claude.use(name).catch(function () { return null; }) : Promise.resolve(null);
    return caps[name];
  }
  function inClaude() { return !!(window.claude && typeof window.claude.use === 'function'); }

  function opt(sel, value, text, group) {
    var o = document.createElement('option'); o.value = value; o.textContent = text; (group || sel).appendChild(o); return o;
  }

  function build() {
    var st = document.createElement('style'); st.id = 'nsp-style'; st.textContent = CSS; document.head.appendChild(st);
    el = document.createElement('div'); el.className = 'nsp-overlay'; el.hidden = true;
    el.innerHTML =
      '<div class="nsp" role="dialog" aria-modal="true" aria-labelledby="nspTitle">' +
      '<button type="button" class="nsp-x" id="nspClose" aria-label="Close" title="Close (Esc)">\u00d7</button>' +
      '<h2 id="nspTitle">New <em>studio</em></h2>' +
      '<p class="note" style="margin:0">Pick the vessel and the mini-game, say what you want, and Claude Code builds it on a new branch off ' + BASE + '. ' +
      'It reads the game first (the vessel, mini-game and AI skills), so name things here and describe the feel; it asks back if something is unclear.</p>' +
      '<section class="card" id="nspVesselCard"><h3>Vessel</h3>' +
      '<div class="nsp-row"><label for="nspVesselMode">Vessel</label><select id="nspVesselMode"><option value="existing">Choose a vessel of the fleet</option><option value="new">Create a new vessel</option></select></div>' +
      '<div class="nsp-row" id="nspVesselRow"><label for="nspVessel">Which</label><select id="nspVessel"></select></div>' +
      '<div class="nsp-row" id="nspVesselNameRow" hidden><label for="nspVesselName">Name</label><input type="text" id="nspVesselName" maxlength="40" placeholder="e.g. Heron" autocomplete="off"></div>' +
      '<div class="nsp-row"><label for="nspGoal">Do</label><select id="nspGoal"></select></div>' +
      '<div class="note" id="nspVesselNote"></div></section>' +
      '<section class="card" id="nspGameCard"><h3>Mini-game</h3>' +
      '<div class="nsp-row"><label for="nspGameMode">Mini-game</label><select id="nspGameMode"><option value="existing">Use a mini-game of the game</option><option value="new">Create a new mini-game</option><option value="none">None yet (the studio only)</option></select></div>' +
      '<div class="nsp-row" id="nspGameRow"><label for="nspGame">Which</label><select id="nspGame"></select></div>' +
      '<div class="nsp-row" id="nspGameNameRow" hidden><label for="nspGameName">Name</label><input type="text" id="nspGameName" maxlength="40" placeholder="e.g. Heron Dash" autocomplete="off"></div>' +
      '<div class="note" id="nspGameNote"></div></section>' +
      '<section class="card" id="nspTalkCard"><h3>What you want</h3>' +
      '<textarea id="nspText" placeholder="Say or type what the vessel should do, how it should feel, what its AI and mini-game are about."></textarea>' +
      '<div class="nsp-btns"><button type="button" class="btn" id="nspMic" aria-pressed="false">Speak</button>' +
      '<button type="button" class="btn" id="nspFill" title="Claude reads what you said or wrote into the choices above">Fill the form from this</button></div>' +
      '<div class="nsp-state" id="nspVoiceState" aria-live="polite"></div></section>' +
      '<p class="nsp-sum" id="nspSummary"></p>' +
      '<div class="nsp-btns"><button type="button" class="btn primary" id="nspSend">Create with Claude Code</button>' +
      '<button type="button" class="btn" id="nspCopy">Copy prompt</button></div>' +
      '<div class="nsp-state" id="nspState" aria-live="polite"></div>' +
      '</div>';
    document.body.appendChild(el);

    fillVessels();
    $('nspVesselMode').addEventListener('change', function () { sync(true); });
    $('nspVessel').addEventListener('change', function () { fillGames(); sync(); });
    ['nspGoal', 'nspGameMode', 'nspGame'].forEach(function (id) { $(id).addEventListener('change', function () { sync(); }); });
    ['nspVesselName', 'nspGameName', 'nspText'].forEach(function (id) { $(id).addEventListener('input', function () { sync(); }); });
    $('nspClose').addEventListener('click', close);
    el.addEventListener('mousedown', function (e) { if (e.target === el) close(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !el.hidden) close(); });
    $('nspSend').addEventListener('click', send);
    $('nspCopy').addEventListener('click', copy);
    $('nspMic').addEventListener('click', toggleMic);
    $('nspFill').addEventListener('click', fillFromWords);

    restore();
  }

  function fillVessels() {
    var sel = $('nspVessel'); sel.textContent = '';
    G().vessels.forEach(function (v) {
      var tags = [];
      if (v.studio) tags.push('has a studio');
      if (!v.hasClass) tags.push('no class asset yet');
      else tags.push(v.games.length + (v.games.length === 1 ? ' mini-game' : ' mini-games'));
      opt(sel, v.name, v.name + ' \u00b7 ' + tags.join(', '));
    });
    fillGames();
  }

  function vesselRec(name) { return G().vessels.filter(function (v) { return v.name === name; })[0] || null; }

  function fillGames() {
    var sel = $('nspGame'), keep = sel.value, vName = $('nspVesselMode').value === 'existing' ? $('nspVessel').value : '';
    sel.textContent = '';
    var games = G().games, groups = [
      ['Flies the ' + (vName || 'vessel'), function (g) { return vName && g.vessels.indexOf(vName) >= 0; }],
      ['Arcade', function (g) { return g.menu === 'arcade'; }],
      ['Arena', function (g) { return g.menu === 'arena'; }],
      ['Not in a menu', function () { return true; }]
    ], used = {};
    groups.forEach(function (grp) {
      var list = games.filter(function (g) { return !used[g.card] && grp[1](g); });
      if (!list.length) return;
      var og = document.createElement('optgroup'); og.label = grp[0]; sel.appendChild(og);
      list.forEach(function (g) { used[g.card] = 1; opt(sel, g.card, g.name.replace(/\s+/g, '') === g.mode ? g.name : g.name + ' \u00b7 ' + g.mode, og); });
    });
    if (keep && sel.querySelector('option[value="' + keep + '"]')) sel.value = keep;
  }

  function gameRec(card) { return G().games.filter(function (g) { return g.card === card; })[0] || null; }

  function setGoals(mode) {
    var sel = $('nspGoal'), keep = sel.value; sel.textContent = '';
    GOALS[mode].forEach(function (g) { opt(sel, g[0], g[1]); });
    if (GOALS[mode].some(function (g) { return g[0] === keep; })) sel.value = keep;
  }

  /** Reads the form into one plain object (the request's spec). */
  function state() {
    var vm = $('nspVesselMode').value, gm = $('nspGameMode').value;
    return {
      vessel: vm === 'new' ? { mode: 'new', name: $('nspVesselName').value.trim() } : { mode: 'existing', name: $('nspVessel').value },
      goal: $('nspGoal').value,
      game: gm === 'new' ? { mode: 'new', name: $('nspGameName').value.trim() } : gm === 'existing' ? { mode: 'existing', card: $('nspGame').value } : { mode: 'none' },
      text: $('nspText').value.trim()
    };
  }

  function goalText(id) {
    var all = GOALS.existing.concat(GOALS['new']);
    for (var i = 0; i < all.length; i++) if (all[i][0] === id) return all[i][1];
    return id;
  }

  function sync(modeChanged) {
    var vm = $('nspVesselMode').value, gm = $('nspGameMode').value;
    $('nspVesselRow').hidden = vm !== 'existing';
    $('nspVesselNameRow').hidden = vm !== 'new';
    if (modeChanged || !$('nspGoal').options.length || !GOALS[vm].some(function (g) { return g[0] === $('nspGoal').value; })) setGoals(vm);
    $('nspGameRow').hidden = gm !== 'existing';
    $('nspGameNameRow').hidden = gm !== 'new';
    if (modeChanged) fillGames();
    var s = state(), v = s.vessel.mode === 'existing' ? vesselRec(s.vessel.name) : null, g = s.game.mode === 'existing' ? gameRec(s.game.card) : null;
    $('nspVesselNote').textContent = s.vessel.mode === 'new'
      ? 'A new hull: its class, prefab, four abilities over the four elements, HUD and studio (the /vessel skill).'
      : v ? (v.studio ? v.name + ' has a studio: the work lands in it.' : v.name + ' has no studio yet: one is built for it.') +
            (v.games.length ? ' Its mini-games: ' + v.games.join(', ') + '.' : ' No mini-game seats it yet.') : '';
    $('nspGameNote').textContent = s.game.mode === 'new'
      ? 'A new arcade mode: its GameModes value, scene, card and the four-step intensity ladder (the /arcadegame skill).'
      : g ? g.name + ': GameModes.' + g.mode + (g.scene ? ', scene ' + g.scene : '') + (g.vessels.length ? ', seats ' + g.vessels.join(', ') : '') +
            (s.vessel.mode === 'existing' && g.vessels.indexOf(s.vessel.name) < 0 ? '. It does not seat the ' + s.vessel.name + ' yet: adding it is part of the work.' : '.')
      : 'The studio only; a mini-game can follow.';
    $('nspSummary').textContent = summary(s);
    saveDraft();
  }

  function vesselName(s) { return s.vessel.name || (s.vessel.mode === 'new' ? 'a new vessel' : '?'); }
  function gameName(s) {
    if (s.game.mode === 'none') return 'no mini-game yet';
    if (s.game.mode === 'new') return 'new mini-game ' + (s.game.name || '(unnamed)');
    var g = gameRec(s.game.card); return g ? g.name : '?';
  }
  function summary(s) {
    return (s.vessel.mode === 'new' ? 'New vessel ' + (s.vessel.name || '(unnamed)') : s.vessel.name) + ' \u00b7 ' + goalText(s.goal) + ' \u00b7 ' + gameName(s);
  }

  function problems(s) {
    var out = [];
    if (s.vessel.mode === 'new' && !/^[A-Z][A-Za-z0-9]{1,39}$/.test(s.vessel.name)) out.push('Give the new vessel a name: one word, starting with a capital (it becomes its class name).');
    if (s.vessel.mode === 'new' && vesselRec(s.vessel.name)) out.push(s.vessel.name + ' is already a vessel: choose it from the fleet instead.');
    if (s.game.mode === 'new' && !/^[A-Za-z][A-Za-z0-9 ]{1,39}$/.test(s.game.name)) out.push('Give the new mini-game a name (letters, digits and spaces).');
    return out;
  }

  /** The Claude Code prompt: the request, then which skills to follow and in what order. */
  function prompt(s, requestId) {
    s = s || state();
    var v = s.vessel.mode === 'existing' ? vesselRec(s.vessel.name) : null, g = s.game.mode === 'existing' ? gameRec(s.game.card) : null;
    var lines = [];
    lines.push('Vessel Studio: NEW STUDIO request, from the "+ New studio" panel of the Vessel Studio artifact ' + ARTIFACT + '.');
    lines.push('');
    lines.push('Vessel: ' + (s.vessel.mode === 'new'
      ? 'a NEW vessel named "' + s.vessel.name + '" (not in VesselClassType yet).'
      : s.vessel.name + ' (VesselClassType.' + s.vessel.name + ' = ' + (v ? v.value : '?') + (v && v.studio ? '; its studio: Docs/Studios/VesselStudio/ studios.json id "' + v.studio + '"' : '; no studio yet') + ').'));
    lines.push('Goal: ' + goalText(s.goal) + '.');
    lines.push('Mini-game: ' + (s.game.mode === 'new' ? 'a NEW mini-game named "' + s.game.name + '".'
      : s.game.mode === 'none' ? 'none yet (the studio only).'
      : (g ? g.name + ' (GameModes.' + g.mode + ' = ' + g.modeValue + ', card Assets/_SO_Assets/Games/' + g.card + (g.scene ? ', scene ' + g.scene : '') + ')' : s.game.card) + '.'));
    lines.push('What the requester wants, in their words:');
    lines.push(s.text ? s.text : '(nothing written; ask them what the vessel should do before building)');
    lines.push('');
    lines.push('How to work:');
    lines.push('- Work on a NEW branch off ' + BASE + ' (never push to ' + BASE + ', bleeding-edge or main). Commit and push as you go.');
    lines.push('- Load and follow these skills: /vessel-studio (section 0 the one artifact, section 2 "A new studio, in order", section 1.5 the universal panel kit, section 4 publishing)' +
      (s.vessel.mode === 'new' || s.goal === 'fly' || s.goal === 'both' ? ', /vessel (the fleet contract; a new hull\'s class, prefab, ability map and HUD)' : '') +
      (s.goal === 'ai' || s.goal === 'both' || s.goal === 'new' ? ', /vessel-ai (input-only AI, Easy/Medium/Hard)' : '') +
      (s.game.mode === 'new' ? ', /arcadegame (a new mode: its registrations, generator and intensity ladder; /arenagame if it seats several hulls)' : '') +
      ', /labmaker (rounds), and /artifact-to-unity (the artifact is the one source of truth; the game is its clone).');
    lines.push('- Build or extend the studio page FIRST, in the artifact (Docs/Studios/VesselStudio/, studios.json, the hub card), gate it with check_studio.cjs, and publish it into the ONE artifact from your branch so the requester can fly it. The game side follows the studio once the requester decides, through /artifact-to-unity.');
    lines.push('- When something in this request is unclear, ask the requester in the session before guessing.');
    if (requestId) lines.push('- This request is document "' + requestId + '" in the Vessel Studio\'s "requests" collection (the claude.ai artifact\'s database; when this chat runs in Amoebius, its local studio store, which you cannot reach: then skip this): set its status to "in progress" with your branch in "reply" when you start, and to "done" with what to test when you finish.');
    return lines.join('\n');
  }

  // ---------------- voice ----------------
  var rec = null, recOn = false, baseText = '';
  function voiceState(msg, cls) { var s = $('nspVoiceState'); s.textContent = msg || ''; s.className = 'nsp-state' + (cls ? ' ' + cls : ''); }
  function noVoice(why) {
    voiceState((why ? why + ' ' : '') + 'Use your keyboard\'s dictation instead: Win+H on Windows, Fn twice on a Mac, the microphone key on a phone keyboard.', 'err');
    $('nspText').focus();
  }
  function toggleMic() {
    if (recOn && rec) { rec.stop(); return; }
    var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) { noVoice('Speaking is not available in this browser view.'); return; }
    try { rec = new SR(); } catch (e) { noVoice('Speaking is not available here.'); return; }
    rec.lang = navigator.language || 'en-US'; rec.continuous = true; rec.interimResults = true;
    baseText = $('nspText').value.replace(/\s+$/, '');
    rec.onresult = function (ev) {
      var finals = '', interim = '';
      for (var i = 0; i < ev.results.length; i++) { var t = ev.results[i][0].transcript; if (ev.results[i].isFinal) finals += t; else interim += t; }
      $('nspText').value = (baseText ? baseText + ' ' : '') + (finals + interim).trim();
      sync();
    };
    rec.onerror = function (ev) {
      var code = ev && ev.error;
      if (code === 'not-allowed' || code === 'service-not-allowed') noVoice('The microphone is not allowed in this view.');
      else if (code === 'no-speech') voiceState('Nothing heard. Press Speak and talk.');
      else voiceState('Speaking stopped (' + code + ').', 'err');
    };
    rec.onend = function () { recOn = false; $('nspMic').textContent = 'Speak'; $('nspMic').classList.remove('rec'); $('nspMic').setAttribute('aria-pressed', 'false'); };
    try { rec.start(); } catch (e) { noVoice('The microphone could not start.'); return; }
    recOn = true; $('nspMic').textContent = 'Stop listening'; $('nspMic').classList.add('rec'); $('nspMic').setAttribute('aria-pressed', 'true');
    voiceState('Listening\u2026 say what you want; press Stop listening when done.');
  }

  /** Claude reads the words into the choices (claude.ai only: the `sample` capability). */
  function fillFromWords() {
    var text = $('nspText').value.trim();
    if (!text) { voiceState('Say or type what you want first.'); return; }
    cap('sample').then(function (sample) {
      if (!sample) { voiceState('Filling the form from your words asks Claude: it works in the claude.ai Vessel Studio and in Amoebius. Choose above meanwhile.', 'err'); return; }
      voiceState('Reading your words\u2026');
      var ask = 'You fill a form from a game developer\'s request. Answer with JSON only: ' +
        '{"vesselMode":"existing"|"new","vessel":string,"goal":"ai"|"fly"|"both"|"explore"|"new"|"newNoAi","gameMode":"existing"|"new"|"none","game":string}. ' +
        'Goals for an existing vessel: ai, fly, both, explore; for a new vessel: new, newNoAi. For an existing vessel or game use EXACTLY one of the names listed; ' +
        'for a new one, its name as one capitalised word for a vessel and a short title for a game. Leave a field "" when the request does not say.\n' +
        'Vessels: ' + G().vessels.map(function (v) { return v.name; }).join(', ') + '\n' +
        'Mini-games (name = card): ' + G().games.map(function (g) { return g.name + ' = ' + g.card; }).join('; ') + '\n' +
        'Request: ' + text;
      (sample.json ? sample.json(ask, { cache: false }) : sample(ask, { cache: false }).then(function (r) { return JSON.parse(r.text); })).then(function (j) {
        apply(j || {}); voiceState('Filled from your words: check the choices above.', 'ok');
      }, function (e) {
        voiceState(e && e.code === 'not_granted' ? 'Reading the words was not allowed in this view.' : 'Could not read the words (' + ((e && e.code) || 'error') + ').', 'err');
      });
    });
  }

  function apply(j) {
    var ok = function (sel, v) { return v && sel.querySelector('option[value="' + String(v).replace(/"/g, '') + '"]'); };
    if (j.vesselMode === 'new' || j.vesselMode === 'existing') { $('nspVesselMode').value = j.vesselMode; sync(true); }
    if (j.vesselMode === 'new' && j.vessel) $('nspVesselName').value = String(j.vessel).replace(/[^A-Za-z0-9]/g, '').slice(0, 40);
    else if (ok($('nspVessel'), j.vessel)) { $('nspVessel').value = j.vessel; fillGames(); }
    if (ok($('nspGoal'), j.goal)) $('nspGoal').value = j.goal;
    if (j.gameMode === 'new' || j.gameMode === 'existing' || j.gameMode === 'none') $('nspGameMode').value = j.gameMode;
    sync();
    if (j.gameMode === 'new' && j.game) $('nspGameName').value = String(j.game).slice(0, 40);
    else if (j.gameMode === 'existing') {
      var card = ok($('nspGame'), j.game) ? j.game : (G().games.filter(function (g) { return g.name === j.game; })[0] || {}).card;
      if (ok($('nspGame'), card)) $('nspGame').value = card;
    }
    sync();
  }

  // ---------------- send ----------------
  function setState(msg, cls) { var s = $('nspState'); s.textContent = ''; s.className = 'nsp-state' + (cls ? ' ' + cls : ''); if (msg) s.append(msg); }
  function mcpError(e) {
    var c = e && e.code;
    if (c === 'server_not_connected') return 'the Claude Code Remote connector is not available to you here.';
    if (c === 'not_in_manifest' || c === 'not_granted') return 'access to your Claude sessions was declined; allow it from the artifact\'s Permissions menu.';
    if (c === 'needs_reauth') return 'reconnect Claude Code in claude.ai Settings.';
    return (c ? c + ': ' : '') + ((e && e.message) || String(e));
  }

  function send() {
    var s = state(), bad = problems(s);
    if (bad.length) { setState(bad.join(' '), 'err'); return; }
    if (!inClaude()) {
      var a = window.VesselStudioTheme ? VesselStudioTheme.artifactLink('open the claude.ai artifact') : document.createTextNode(ARTIFACT);
      setState('Sending to Claude Code works in the claude.ai Vessel Studio: ', 'err'); $('nspState').append(a, document.createTextNode(', press + there, or Copy prompt into any Claude Code session.'));
      return;
    }
    $('nspSend').disabled = true; setState('Filing the request\u2026');
    var reqRef = null, me = '';
    Promise.all([cap('db'), cap('user'), cap('mcp')]).then(function (c) {
      var db = c[0], user = c[1], mcp = c[2];
      if (!mcp) throw { message: 'starting a Claude Code session needs the artifact opened in claude.ai, signed in.' };
      return (user && user.id ? user.id().catch(function () { return ''; }) : Promise.resolve('')).then(function (id) {
        me = id || '';
        if (!db) return null;
        return db.collection('requests').add({ vessel: vesselName(s), kind: 'new studio', text: summary(s) + (s.text ? '\n' + s.text : ''), spec: s, status: 'open', createdAt: Date.now(), by: me })
          .then(function (r) { reqRef = r; return r; }, function () { return null; });
      }).then(function () {
        setState('Starting a Claude Code session\u2026');
        return mcp.callTool(CCR, 'list_environments', { limit: 20 });
      }).then(function (envs) {
        var list = (envs.payload && (envs.payload.environments || envs.payload.data || envs.payload)) || [];
        var env = (Array.isArray(list) ? list : []).map(function (x) { return x.environment_id || x.id; }).filter(Boolean)[0] ||
          (JSON.stringify(envs.payload || envs.content || '').match(/env_[A-Za-z0-9]+/) || [])[0];
        if (!env) throw { message: 'no Claude Code environment was found for your account; open claude.ai/code once, or Copy prompt.' };
        return mcp.callTool(CCR, 'create_session', {
          environment_id: env, source_url: REPO_URL, source_revision: BASE,
          title: 'New studio: ' + summary(s), prompt: prompt(s, reqRef && reqRef.id), tags: ['vessel-studio']
        });
      }).then(function (res) {
        var sid = (res.payload && (res.payload.session_id || res.payload.id)) || (JSON.stringify(res.payload || res.content || '').match(/session_[A-Za-z0-9]+/) || [])[0];
        if (reqRef && sid) reqRef.update({ session: sid }).catch(function () {});
        if (res.payload && res.payload.where) {   // the connector says where the work runs (an AGENT chat on this computer)
          setState('Sent: ' + res.payload.where + ' is building it now.', 'ok');
          try { localStorage.removeItem(DRAFT); } catch (e) { /* storage blocked */ }
          return;
        }
        var link = document.createElement('a'); link.target = '_blank'; link.rel = 'noopener';
        link.href = sid ? 'https://claude.ai/code/' + sid : 'https://claude.ai/code'; link.textContent = sid ? 'open the session' : 'claude.ai/code';
        setState('Sent. Claude Code is building it on a new branch (it takes a minute to start): ', 'ok');
        $('nspState').append(link, document.createTextNode(reqRef ? '. The request is in the Requests list below.' : '.'));
        try { localStorage.removeItem(DRAFT); } catch (e) { /* storage blocked */ }
      });
    }).catch(function (e) {
      setState('Not sent: ' + mcpError(e) + ' Copy prompt works in any Claude Code session.', 'err');
    }).then(function () { $('nspSend').disabled = false; });
  }

  function copy() {
    var s = state(), bad = problems(s), text = prompt(s);
    if (bad.length) { setState(bad.join(' '), 'err'); return; }
    var done = function () { setState('Copied. Paste it into a Claude Code session (claude.ai/code, the CLI, or Amoebius\'s AGENT page).', 'ok'); };
    try { navigator.clipboard.writeText(text).then(done, function () { fallback(text); }); } catch (e) { fallback(text); }
  }
  function fallback(text) {
    var ta = $('nspText'), keep = ta.value; ta.value = text; ta.focus(); ta.select();
    var ok = false; try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
    ta.value = keep; sync();
    setState(ok ? 'Copied.' : 'Copying was blocked in this view.', ok ? 'ok' : 'err');
  }

  // ---------------- open / close / draft ----------------
  function saveDraft() { try { localStorage.setItem(DRAFT, JSON.stringify(state())); } catch (e) { /* storage blocked: fine */ } }
  function restore() {
    var d = null; try { d = JSON.parse(localStorage.getItem(DRAFT) || 'null'); } catch (e) { d = null; }
    if (d && d.vessel) {
      $('nspVesselMode').value = d.vessel.mode === 'new' ? 'new' : 'existing'; sync(true);
      if (d.vessel.mode === 'new') $('nspVesselName').value = d.vessel.name || '';
      else if (vesselRec(d.vessel.name)) { $('nspVessel').value = d.vessel.name; fillGames(); }
      sync(); if (d.goal && $('nspGoal').querySelector('option[value="' + d.goal + '"]')) $('nspGoal').value = d.goal;
      if (d.game) { $('nspGameMode').value = d.game.mode || 'existing'; if (d.game.mode === 'new') $('nspGameName').value = d.game.name || ''; }
      sync(); if (d.game && d.game.card && gameRec(d.game.card)) $('nspGame').value = d.game.card;
      $('nspText').value = d.text || '';
    }
    sync(true);
  }

  var lastFocus = null;
  function open(pre) {
    if (!el) build();
    if (pre && pre.vessel) {
      if (vesselRec(pre.vessel)) { $('nspVesselMode').value = 'existing'; sync(true); $('nspVessel').value = pre.vessel; fillGames(); }
      else { $('nspVesselMode').value = 'new'; sync(true); $('nspVesselName').value = pre.vessel; }
      sync();
    }
    lastFocus = document.activeElement;
    el.hidden = false; setState('');
    $('nspVesselMode').focus();
  }
  function close() {
    if (!el || el.hidden) return;
    if (recOn && rec) rec.stop();
    el.hidden = true;
    if (lastFocus && lastFocus.focus) lastFocus.focus();
  }

  window.VesselStudioNew = {
    open: open, close: close,
    state: function () { if (!el) build(); return state(); },
    prompt: function () { if (!el) build(); return prompt(state()); },
    problems: function () { if (!el) build(); return problems(state()); }
  };
  // #new-studio opens the panel on load (Unity's + card adds it after the host word)
  if (/(^|[#&])new-studio\b/.test(location.hash)) {
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { open(); });
    else open();
  }
})();
