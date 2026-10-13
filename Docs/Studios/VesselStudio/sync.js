/* Vessel Studio sync panel: Refresh from GitHub, a console log, merge (then delete the merged
   branch), and a shared decision log. Injected into every page of the hub (index.html and the
   studio pages) by build_artifact.py and by Refresh.

   The page never talks to GitHub itself. It writes a JOB to the artifact's shared `jobs`
   collection and messages the viewer's own Claude Code session (the built-in "Claude Code Remote"
   connector) with the job's id. That session, which already has the repo, runs
   .claude/skills/vessel-studio/sync_job.py, writes progress into the job (shown live in the
   console here) and, after a refresh or a merge, rebuilds and republishes this artifact.
   Contract: the /vessel-studio skill, section 5. Every capability may be absent on a view;
   the panel says so instead of failing. */
(() => {
  if (window.__vsSync) return; window.__vsSync = true;
  const REPO_URL = 'https://github.com/froglet-studio/Cosmic-Shore', CCR = 'Claude Code Remote';
  const TOOLS_REF = 'Ys-bleeding-edge';   // the branch that carries the /vessel-studio skill and its scripts
  const WATCH = ['cece/magical-carson-9bdq8z', 'vessel-studio', 'Ys-bleeding-edge', 'bleeding-edge'];
  const GUARDED = ['bleeding-edge', 'main', 'master', 'Ys-bleeding-edge'];
  const LS = 'vsSync.';
  const ls = { get(k, d) { try { const v = localStorage.getItem(LS + k); return v == null ? d : v; } catch { return d; } },
               set(k, v) { try { localStorage.setItem(LS + k, v); } catch {} } };

  // ---------- UI (shadow DOM so the studio's own styles never reach it) ----------
  const host = document.createElement('div'); host.id = 'vs-sync';
  host.style.cssText = 'position:fixed;right:12px;bottom:12px;z-index:2147483000';
  const root = host.attachShadow({ mode: 'open' });
  root.innerHTML = `<style>
    :host { --bg:#0e1220; --panel:#151a2c; --line:#2a3150; --fg:#e8ecff; --dim:#9aa6d6; --acc:#9dff2e; --warn:#ffb347; --err:#ff6b6b; --ok:#7be3a6;
      font: 13px/1.45 "IBM Plex Sans", system-ui, sans-serif; color: var(--fg); }
    * { box-sizing: border-box; }
    button { font: inherit; color: var(--fg); background: #222a46; border: 1px solid var(--line); border-radius: 6px; padding: 6px 10px; cursor: pointer; }
    button:hover:not(:disabled) { border-color: var(--dim); }
    button:disabled { opacity: .45; cursor: default; }
    button.pri { background: #2f4a12; border-color: #5d8f23; }
    button.danger { background: #4a1d1d; border-color: #8f3a3a; }
    .fab { display: flex; gap: 6px; align-items: center; padding: 8px 12px; border-radius: 20px; background: var(--panel); box-shadow: 0 4px 18px #0008; }
    .dot { width: 8px; height: 8px; border-radius: 50%; background: var(--dim); }
    .dot.new { background: var(--acc); } .dot.err { background: var(--err); }
    .drawer { position: fixed; right: 12px; bottom: 60px; width: min(440px, calc(100vw - 24px)); max-height: calc(100vh - 80px); overflow: auto;
      background: var(--bg); border: 1px solid var(--line); border-radius: 10px; box-shadow: 0 10px 40px #000a; padding: 12px; display: grid; gap: 12px; }
    h3 { margin: 0; font: 700 12px/1 "Saira Condensed", system-ui, sans-serif; letter-spacing: .08em; text-transform: uppercase; color: var(--dim); }
    section { display: grid; gap: 8px; padding-top: 10px; border-top: 1px solid var(--line); }
    section:first-of-type { border-top: 0; padding-top: 0; }
    .row { display: flex; gap: 6px; flex-wrap: wrap; align-items: center; }
    input, textarea { font: inherit; color: var(--fg); background: var(--panel); border: 1px solid var(--line); border-radius: 6px; padding: 6px 8px; min-width: 0; }
    input { flex: 1 1 160px; } textarea { width: 100%; min-height: 64px; resize: vertical; }
    label { color: var(--dim); font-size: 12px; }
    .log { font: 11.5px/1.5 "IBM Plex Mono", ui-monospace, monospace; background: #080b14; border: 1px solid var(--line); border-radius: 6px; padding: 8px;
      max-height: 220px; overflow: auto; white-space: pre-wrap; word-break: break-word; }
    .log .t { color: #5d6894; } .log .ok { color: var(--ok); } .log .warn { color: var(--warn); } .log .err { color: var(--err); } .log .new { color: var(--acc); }
    .q { background: var(--panel); border: 1px solid var(--line); border-radius: 8px; padding: 8px; display: grid; gap: 8px; }
    .meta { color: var(--dim); font-size: 12px; }
    .dec { display: grid; gap: 6px; max-height: 240px; overflow: auto; }
    .dec div { background: var(--panel); border-radius: 6px; padding: 6px 8px; }
    .dec small { color: var(--dim); display: block; }
    .tag { font-size: 10px; text-transform: uppercase; letter-spacing: .06em; color: var(--warn); margin-right: 6px; }
    .ico { padding: 6px 8px; } a.btn { display: inline-block; text-decoration: none; font: inherit; color: var(--fg); background: #4a1d1d; border: 1px solid #8f3a3a; border-radius: 6px; padding: 6px 10px; }
    .modal { position: fixed; inset: 0; background: #000a; display: grid; place-items: center; padding: 16px; }
    .modal > div { width: min(420px, 100%); background: var(--bg); border: 1px solid var(--line); border-radius: 10px; padding: 14px; display: grid; gap: 10px; box-shadow: 0 10px 40px #000c; }
    .modal b { font-size: 14px; }
    .modal > div { position: relative; }
    .x { position: absolute; top: 6px; right: 6px; width: 28px; height: 28px; padding: 0; border-radius: 50%; font-size: 18px; line-height: 1; background: transparent; border-color: transparent; color: var(--dim); }
    .x:hover { color: var(--fg); border-color: var(--line) !important; }
    .drawer > section:first-of-type h3, .modal b { padding-right: 30px; }
    [hidden] { display: none !important; }
  </style>
  <button class="fab" id="fab" title="Sync with GitHub, merge, decisions"><span class="dot" id="dot"></span><b>Sync</b></button>
  <div class="drawer" id="drawer" hidden role="dialog" aria-label="Sync with GitHub">
    <button class="x" id="close" title="Close" aria-label="Close">\u00d7</button>
    <section>
      <h3>Refresh from GitHub</h3>
      <div class="meta" id="cur">reading build\u2026</div>
      <div class="row"><label for="src">Pull studio from branch</label></div>
      <div class="row"><input id="src" list="branches" spellcheck="false"><button class="pri" id="refresh">\u27f3 Refresh</button></div>
      <datalist id="branches"></datalist>
      <div class="row"><label for="sess">Claude session that does the git work</label></div>
      <div class="row"><input id="sess" placeholder="session_..." spellcheck="false"><button id="newSess">Start session</button></div>
    </section>
    <section>
      <h3>Console</h3>
      <div class="log" id="log" role="log" aria-live="polite"></div>
      <div class="row"><button id="copy">Copy log</button><button id="clear">Clear</button></div>
    </section>
    <section>
      <h3>Merge</h3>
      <div class="row"><label for="from" style="flex:none;width:40px">From</label><input id="from" list="branches" placeholder="branch with the changes" spellcheck="false"></div>
      <div class="row"><label for="to" style="flex:none;width:40px">Into</label><input id="to" list="branches" placeholder="branch to merge into" spellcheck="false"></div>
      <div class="row"><button id="compare">Compare</button><button class="pri" id="merge">Merge</button><button id="swap">\u21c4 Swap</button></div>
      <div class="q" id="q" hidden></div>
    </section>
    <section>
      <h3>Decisions</h3>
      <textarea id="decText" placeholder="What did we decide? (shared with everyone who can open this artifact)"></textarea>
      <div class="row"><button class="pri" id="record">Record decision</button><span class="meta" id="decNote"></span></div>
      <div class="dec" id="decList"></div>
    </section>
  </div>
  <div class="modal" id="modal" hidden role="dialog" aria-modal="true"><div><button class="x" id="modalX" title="Close" aria-label="Close">\u00d7</button><div id="modalBox" style="display:grid;gap:10px"></div></div></div>`;
  const $ = (id) => root.getElementById(id);
  const mount = () => document.body.appendChild(host);
  if (document.body) mount(); else document.addEventListener('DOMContentLoaded', mount);
  const setOpen = (o) => { $('drawer').hidden = !o; ls.set('open', o ? '1' : '0'); };
  $('fab').addEventListener('click', () => setOpen($('drawer').hidden));
  $('close').addEventListener('click', () => setOpen(false));
  // close on any press outside the panel (the page, its links, its canvas) and on Escape
  document.addEventListener('pointerdown', (e) => { if (!$('drawer').hidden && !e.composedPath().includes(host)) setOpen(false); }, true);
  document.addEventListener('keydown', (e) => { if (e.key !== 'Escape') return; if (!$('modal').hidden) $('modal').hidden = true; else if (!$('drawer').hidden) setOpen(false); });
  if (ls.get('open', '0') === '1') $('drawer').hidden = false;

  // ---------- console ----------
  const lines = [];
  function log(msg, cls) {
    const t = new Date().toLocaleTimeString();
    lines.push(`[${t}] ${msg}`);
    console.log('[VesselStudio sync]', msg);
    const div = document.createElement('div');
    const ts = document.createElement('span'); ts.className = 't'; ts.textContent = t + '  ';
    const tx = document.createElement('span'); if (cls) tx.className = cls; tx.textContent = msg;
    div.append(ts, tx); $('log').append(div); $('log').scrollTop = $('log').scrollHeight;
  }
  $('clear').addEventListener('click', () => { lines.length = 0; $('log').textContent = ''; });
  $('copy').addEventListener('click', async () => {
    try { await navigator.clipboard.writeText(lines.join('\n')); log('log copied', 'ok'); }
    catch { const r = document.createRange(); r.selectNodeContents($('log')); const s = root.getSelection ? root.getSelection() : getSelection(); s.removeAllRanges(); s.addRange(r); log('select-all done; copy with Ctrl/Cmd+C', 'warn'); }
  });

  // ---------- capabilities ----------
  const use = (n) => (window.claude && window.claude.use ? window.claude.use(n).catch(() => null) : Promise.resolve(null));
  const caps = { mcp: use('mcp'), artifact: use('artifact'), db: use('db'), user: use('user') };

  // ---------- build info: what this artifact is showing ----------
  let build = null;
  // build.json exists only in a build (claude.ai, the mirror, Amoebius's studio server); a page opened as a file has none and must not
  // ask for it (a browser logs a file:// fetch as an error)
  const buildP = (location.protocol === 'file:' ? Promise.resolve(null) : fetch('build.json', { cache: 'no-store' }).then((r) => r.ok ? r.json() : null).catch(() => null)).then((b) => {
    build = b;
    $('cur').textContent = b ? `Showing ${b.repo} @ ${b.branch} \u00b7 ${b.pathSha.slice(0, 7)} \u201c${b.subject}\u201d (${new Date(b.committedAt).toLocaleString()})` : 'Opened as a file, not a build: Refresh, Compare, Merge and Delete run in the claude.ai artifact (' + ARTIFACT() + ') and in Amoebius (OPEN IN AMOEBIUS).';
    $('src').value = ls.get('src', b ? b.branch : WATCH[0]);
    $('from').value = ls.get('from', WATCH[0]); $('to').value = ls.get('to', WATCH[1]);
    return b;
  });

  // ---------- jobs: the page asks a Claude session to do the git work ----------
  const ARTIFACT = () => (build && build.artifact) || 'https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa';
  for (const b of WATCH) { const o = document.createElement('option'); o.value = b; $('branches').append(o); }
  function mcpError(e) {
    const c = e && e.code;
    if (c === 'server_not_connected') return 'the Claude Code Remote connector is not available to you here (open the artifact in claude.ai, signed in).';
    if (c === 'not_in_manifest') return 'access to your Claude sessions was declined for this page; allow it from the artifact\'s Permissions menu.';
    if (c === 'needs_reauth') return 'reconnect Claude Code in claude.ai Settings.';
    if (c === 'tool_error') return 'the session service said: ' + (e.message || 'error');
    return (c ? c + ': ' : '') + ((e && e.message) || String(e));
  }
  // the viewer's session id: private per person (data/users/<id>/sync), else this browser, else the publisher's
  let myId = null, userDoc = null;
  async function sessionRef() {
    const db = await caps.db, u = await caps.user;
    if (!db || !u) return null;
    if (!myId) myId = await u.id();
    return myId ? db.collection('data/users/' + myId).doc('sync') : null;
  }
  async function loadSession() {
    let sid = ls.get('session', '');
    try { const r = await sessionRef(); if (r) { const d = (await r.get()).data(); if (d && d.session) sid = d.session; } } catch {}
    if (!sid && build && build.session) sid = build.session;
    $('sess').value = sid;
    return sid;
  }
  async function saveSession(sid) {
    ls.set('session', sid); $('sess').value = sid;
    try { const r = await sessionRef(); if (r) await r.set({ session: sid, at: new Date().toISOString() }); } catch {}
  }
  buildP.then(loadSession);
  $('sess').addEventListener('change', () => saveSession($('sess').value.trim()));

  const PROMPT = () => `You handle Vessel Studio Sync jobs for the artifact ${ARTIFACT()}. Load the /vessel-studio skill (it is on branch ${TOOLS_REF}: check that branch out) and follow its section 5 for every job message you receive. Do nothing else until a job arrives.`;
  async function startSession() {
    const mcp = await caps.mcp;
    if (!mcp) { log('starting a session needs the artifact opened in claude.ai (signed in) or the studio opened in Amoebius', 'err'); return null; }
    try {
      log('starting a Claude session for sync jobs\u2026');
      const envs = await mcp.callTool(CCR, 'list_environments', { limit: 20 });
      const list = (envs.payload && (envs.payload.environments || envs.payload.data || envs.payload)) || [];
      const env = (Array.isArray(list) ? list : []).map((x) => x.environment_id || x.id).find(Boolean) || (JSON.stringify(envs.payload || envs.content || '').match(/env_[A-Za-z0-9]+/) || [])[0];
      if (!env) { log('no Claude Code environment found for your account; start a session at claude.ai/code and paste its id here', 'err'); return null; }
      const res = await mcp.callTool(CCR, 'create_session', { environment_id: env, source_url: REPO_URL, source_revision: TOOLS_REF, title: 'Vessel Studio sync', prompt: PROMPT() });
      const sid = (res.payload && (res.payload.session_id || res.payload.id)) || (JSON.stringify(res.payload || res.content || '').match(/session_[A-Za-z0-9]+/) || [])[0];
      if (!sid) { log('session started but its id could not be read; copy it from claude.ai/code', 'warn'); return null; }
      await saveSession(sid);
      log(`session ${sid} started; it takes a minute to boot`, 'ok');
      return sid;
    } catch (e) { log('could not start a session: ' + mcpError(e), 'err'); return null; }
  }
  $('newSess').addEventListener('click', startSession);

  // create the job, follow it live, message the session
  const live = new Map();
  async function dispatch(kind, args, onDone) {
    const db = await caps.db, mcp = await caps.mcp;
    if (!db || !mcp) { log('this needs the artifact opened in claude.ai (signed in) or the studio opened in Amoebius (OPEN IN AMOEBIUS): it uses their database and a Claude session', 'err'); return; }
    let sid = $('sess').value.trim() || await loadSession();
    if (!sid) { log('no Claude session set: press Start session (or paste a session id)', 'warn'); return; }
    if (!myId) { const u = await caps.user; if (u) myId = await u.id(); }
    let ref;
    try { ref = await db.collection('jobs').add({ kind, args, status: 'queued', by: myId || '', at: new Date().toISOString(), session: sid, artifact: ARTIFACT(), log: [] }); }
    catch (e) { log('could not create the job: ' + ((e && e.message) || (e && e.code)), 'err'); return; }
    let seen = 0, finished = false;
    const unsub = ref.onSnapshot((snap) => {
      const d = snap.data(); if (!d) return;
      const lines = Array.isArray(d.log) ? d.log : [];
      for (; seen < lines.length; seen++) log('  ' + lines[seen], d.status === 'failed' ? 'err' : '');
      if (!finished && (d.status === 'done' || d.status === 'failed')) {
        finished = true; $('dot').className = 'dot' + (d.status === 'failed' ? ' err' : '');
        log(`${kind} ${d.status}`, d.status === 'done' ? 'ok' : 'err');
        setTimeout(() => { try { unsub(); } catch {} live.delete(ref.id); }, 0);
        if (onDone) onDone(d.status === 'done', d.result || {});
      } else if (d.status === 'running' && seen === 0) log(`${kind}: the session picked it up`);
    }, () => {});
    live.set(ref.id, unsub);
    const msg = `Vessel Studio Sync job ${ref.id} (${kind} ${JSON.stringify(args)}) for ${ARTIFACT()}: run it with the /vessel-studio skill, section 5 (read jobs/${ref.id} from the artifact's database, do it, write the result back).`;
    try {
      await mcp.callTool(CCR, 'send_message', { session_id: sid, message: msg });
      $('dot').className = 'dot new';
      log(`${kind}: job ${ref.id} sent to ${sid}; the session reports back here`);
    } catch (e) {
      log(`${kind}: could not reach ${sid}: ${mcpError(e)}`, 'err');
      try { await ref.update({ status: 'failed', log: ['the session could not be reached; press Start session and try again'] }); } catch {}
    }
  }

  // ---------- Refresh ----------
  $('refresh').addEventListener('click', async () => {
    await buildP;
    const branch = $('src').value.trim(); ls.set('src', branch);
    if (!branch) { log('pick a branch', 'warn'); return; }
    log(`refresh: ${branch} (artifact shows ${build ? build.branch + ' @ ' + build.pathSha.slice(0, 7) : 'an unknown commit'})`);
    dispatch('refresh', { branch, shown: build && build.branch === branch ? build.pathSha : null }, (ok, r) => {
      if (!ok) return;
      if (r.upToDate) log('studio is up to date', 'ok');
      else if (r.published) { log('artifact republished; this page reloads now', 'ok'); setTimeout(() => location.reload(), 1500); }
    });
  });

  // ---------- Merge, then offer to delete the merged branch ----------
  $('swap').addEventListener('click', () => { const a = $('from').value; $('from').value = $('to').value; $('to').value = a; });
  function pair() {
    const from = $('from').value.trim(), to = $('to').value.trim(); ls.set('from', from); ls.set('to', to);
    const q = $('q'); q.hidden = false;
    if (!from || !to || from === to) { q.textContent = 'Pick two different branches.'; return null; }
    return { from, to };
  }
  function runCompare(thenMerge) {
    const p = pair(); if (!p) return;
    $('q').textContent = 'comparing (the session reports back in the console)\u2026';
    dispatch('compare', p, (ok, r) => {
      if (!ok) { $('q').textContent = 'Compare failed: see the console.'; return; }
      if (thenMerge && r.ahead > 0 && !r.guarded) confirmMerge(p.from, p.to, r); else ask(p.from, p.to, r);
    });
  }
  $('compare').addEventListener('click', () => runCompare(false));
  $('merge').addEventListener('click', () => runCompare(true));
  function ask(from, to, r) {
    const q = $('q'); q.textContent = '';
    const p = document.createElement('div');
    p.textContent = !r.ahead ? `${to} already has everything on ${from}. Nothing to merge.`
      : `${from} has ${r.ahead} commit(s) ${to} lacks${r.behind ? ` (and is ${r.behind} behind it)` : ''}. Merge ${from} into ${to} now?`;
    q.append(p);
    if (r.guarded) { const w = document.createElement('div'); w.className = 'meta'; w.style.color = 'var(--warn)'; w.textContent = `${to} is a shared base branch: merge it through a pull request, not from here.`; q.append(w); return; }
    if (!r.ahead) return;
    const row = document.createElement('div'); row.className = 'row';
    const mk = (label, cls, fn) => { const b = document.createElement('button'); b.textContent = label; if (cls) b.className = cls; b.addEventListener('click', fn); row.append(b); };
    mk('Yes, merge now', 'pri', () => confirmMerge(from, to, r));
    mk('Not now', '', async () => { q.hidden = true; log(`not merging ${from} into ${to} now`); await record(`Not merging ${from} into ${to} yet (${r.ahead} commits waiting)`, 'merge', true); });
    q.append(row);
  }
  function confirmMerge(from, to, r) {
    const q = $('q'); q.textContent = '';
    const p = document.createElement('div');
    p.textContent = `Merge ${from} into ${to} (${r.ahead} commit(s)) and push ${to}? A Claude session does it; if the branches conflict nothing is pushed.`;
    const row = document.createElement('div'); row.className = 'row';
    const go = document.createElement('button'); go.className = 'danger'; go.textContent = 'Confirm merge';
    const no = document.createElement('button'); no.textContent = 'Cancel';
    no.addEventListener('click', () => ask(from, to, r));
    go.addEventListener('click', () => {
      go.disabled = no.disabled = true; q.textContent = 'merging (the session reports back in the console)\u2026';
      dispatch('merge', { from, to }, async (ok, res) => {
        if (ok && res.merged) {
          q.textContent = `Merged ${from} into ${to}.`;
          await record(`Merged ${from} into ${to}${res.sha ? ' (' + res.sha.slice(0, 7) + ')' : ''}`, 'merge', true);
          askDelete(from, to);
        } else if (ok) q.textContent = `${to} already had everything on ${from}.`;
        else q.textContent = res.conflicts && res.conflicts.length ? `Conflicts in ${res.conflicts.length} file(s); nothing was pushed. Ask a Claude session to merge them by hand.` : 'Merge failed: see the console.';
      });
    });
    row.append(go, no); q.append(p, row);
  }

  // ---------- popups ----------
  function modal(fill) { const box = $('modalBox'); box.textContent = ''; fill(box); $('modal').hidden = false; }
  const closeModal = () => { $('modal').hidden = true; };
  $('modalX').addEventListener('click', closeModal);
  $('modal').addEventListener('click', (e) => { if (e.target === $('modal')) closeModal(); });
  function askDelete(name, mergedInto) {
    modal((box) => {
      const h = document.createElement('b'); h.textContent = `Merged ${name} into ${mergedInto}. Delete ${name}?`;
      const p = document.createElement('div'); p.className = 'meta';
      const guard = GUARDED.includes(name) || name === TOOLS_REF, shown = build && build.branch === name;
      p.textContent = guard ? `${name} is kept: it is a shared base branch or the branch the sync tools live on.`
        : (shown ? `This artifact shows ${name}; after deleting it, pick another branch under Refresh. ` : '') + 'Its commits are safe in ' + mergedInto + '. A Claude session deletes it on GitHub.';
      const row = document.createElement('div'); row.className = 'row';
      if (!guard) {
        const del = document.createElement('button'); del.className = 'danger'; del.textContent = `Delete ${name}`;
        del.addEventListener('click', () => {
          closeModal(); log(`deleting ${name}\u2026`);
          dispatch('delete', { branch: name }, (ok) => { if (ok) record(`Deleted branch ${name} (merged into ${mergedInto})`, 'delete', true); });
        });
        row.append(del);
      }
      const keep = document.createElement('button'); keep.textContent = guard ? 'OK' : `Keep ${name}`;
      keep.addEventListener('click', () => { if (!guard) { log(`keeping ${name}`); record(`Kept branch ${name} after merging it into ${mergedInto}`, 'merge', true); } closeModal(); });
      row.append(keep); box.append(h, p, row);
    });
  }

  // ---------- Decisions (shared db) ----------
  let me = null, userNs = null;
  async function record(text, kind, quiet) {
    const db = await caps.db;
    if (!db) { if (!quiet) $('decNote').textContent = 'Decisions need the artifact opened in claude.ai (signed in) or the studio opened in Amoebius.'; return; }
    try {
      if (!me && userNs) me = await userNs.id();
      await db.collection('decisions').add({ text, kind, by: me || '', at: new Date().toISOString(), branch: build ? build.branch : '', sha: build ? build.pathSha : '' });
      if (!quiet) { $('decText').value = ''; $('decNote').textContent = 'recorded'; }
    } catch (e) {
      const c = e && e.code;
      if (!quiet) $('decNote').textContent = c === 'permission_denied' || c === 'not_writer' ? 'You can read decisions but not add them.' : 'Could not record: ' + ((e && e.message) || c);
    }
  }
  $('record').addEventListener('click', () => { const t = $('decText').value.trim(); if (t) record(t, 'decision', false); });
  (async () => {
    userNs = await caps.user;
    const db = await caps.db;
    if (!db) { $('decNote').textContent = 'Decisions are recorded in the claude.ai artifact and in Amoebius (OPEN IN AMOEBIUS).'; $('record').disabled = true; return; }
    db.collection('decisions').orderBy('at', 'desc').limit(60).onSnapshot(async (snap) => {
      const docs = snap.docs.map((d) => Object.assign({ id: d.id }, d.data()));
      const ids = [...new Set(docs.map((d) => d.by).filter(Boolean))];
      let ps = {}; try { if (userNs && ids.length) ps = await userNs.profiles(ids); } catch {}
      const list = $('decList'); list.textContent = '';
      if (!docs.length) { const e = document.createElement('div'); e.className = 'meta'; e.textContent = 'No decisions yet.'; list.append(e); return; }
      for (const d of docs) {
        const el = document.createElement('div');
        if (d.kind && d.kind !== 'decision') { const t = document.createElement('span'); t.className = 'tag'; t.textContent = d.kind; el.append(t); }
        el.append(document.createTextNode(String(d.text || '')));
        const s = document.createElement('small');
        s.textContent = `${(ps[d.by] && ps[d.by].name) || 'Someone'} \u00b7 ${d.at ? new Date(d.at).toLocaleString() : ''}${d.branch ? ' \u00b7 ' + d.branch + (d.sha ? ' @ ' + String(d.sha).slice(0, 7) : '') : ''}`;
        el.append(s); list.append(el);
      }
    }, (e) => { $('decNote').textContent = 'Decision log unavailable: ' + ((e && e.code) || 'error'); });
  })();
})();
