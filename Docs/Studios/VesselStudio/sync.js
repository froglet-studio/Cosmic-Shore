/* Vessel Studio sync panel: Refresh from GitHub, a console log, merge suggestions with a
   merge-now question, and a shared decision log. Injected into every page of the hub
   (index.html, stoat.html, squirrel.html). Talks to GitHub through the viewer's own GitHub
   connector (the `mcp` capability), republishes this artifact with the `artifact`
   capability, and keeps decisions in the artifact's shared `db`. Every capability may be
   absent on a given view; the panel says so instead of failing. */
(() => {
  if (window.__vsSync) return; window.__vsSync = true;
  const OWNER = 'froglet-studio', REPO = 'cosmic-shore', DIR = 'Docs/Studios/VesselStudio', GH = 'GitHub';
  const WATCH = ['cece/magical-carson-9bdq8z', 'claude/peaceful-rubin-hhw49n', 'vessel-studio', 'Ys-bleeding-edge', 'bleeding-edge'];
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
  const buildP = fetch('build.json', { cache: 'no-store' }).then((r) => r.ok ? r.json() : null).catch(() => null).then((b) => {
    build = b;
    $('cur').textContent = b ? `Showing ${b.repo} @ ${b.branch} \u00b7 ${b.pathSha.slice(0, 7)} \u201c${b.subject}\u201d (${new Date(b.committedAt).toLocaleString()})` : 'No build.json: this page does not know which commit it shows.';
    $('src').value = ls.get('src', b ? b.branch : WATCH[0]);
    $('from').value = ls.get('from', WATCH[0]); $('to').value = ls.get('to', WATCH[1]);
    return b;
  });

  // ---------- GitHub through the viewer's connector ----------
  async function gh(tool, input) {
    const mcp = await caps.mcp;
    if (!mcp) throw { code: 'no_mcp', message: 'GitHub is not reachable from this view (open the hub page in claude.ai, signed in).' };
    const r = await mcp.callTool(GH, tool, Object.assign({ owner: OWNER, repo: REPO }, input), { cache: false });
    return r;
  }
  function ghError(e) {
    const c = e && e.code;
    if (c === 'server_not_connected') return 'GitHub connector not added for you: add it in claude.ai Settings \u2192 Connectors, then press Refresh.';
    if (c === 'needs_reauth') return 'GitHub connector needs reconnecting: claude.ai Settings \u2192 Connectors \u2192 GitHub.';
    if (c === 'not_in_manifest') return 'GitHub access was declined for this page. Allow it from the artifact\'s Permissions menu.';
    if (c === 'selection_required') return 'You have more than one GitHub connector: pick one in the prompt, then press Refresh.';
    if (c === 'blocked_by_policy') return 'Your organization blocks this GitHub tool.';
    if (c === 'approval_required') return 'GitHub call needs your approval: press the button again and allow it.';
    if (c === 'tool_error') return 'GitHub said: ' + (e.message || 'error');
    return (c ? c + ': ' : '') + ((e && e.message) || String(e));
  }
  const arr = (r) => { const p = r && r.payload; return Array.isArray(p) ? p : (p && Array.isArray(p.items) ? p.items : []); };
  function fileText(r) {
    for (const b of (r && r.content) || []) {
      if (b.type === 'resource' && b.resource) {
        if (typeof b.resource.text === 'string') return b.resource.text;
        if (typeof b.resource.blob === 'string') return new TextDecoder().decode(Uint8Array.from(atob(b.resource.blob), (c) => c.charCodeAt(0)));
      }
    }
    const p = r && r.payload;
    if (p && typeof p.content === 'string' && p.encoding === 'base64') return new TextDecoder().decode(Uint8Array.from(atob(p.content.replace(/\s/g, '')), (c) => c.charCodeAt(0)));
    throw { code: 'no_content', message: 'GitHub returned no file text' };
  }
  const getFile = async (path, branch) => fileText(await gh('get_file_contents', { path: DIR + '/' + path, ref: 'refs/heads/' + branch }));
  const commits = async (branch, n, path) => arr(await gh('list_commits', Object.assign({ sha: branch, perPage: n, fields: ['sha', 'commit'] }, path ? { path } : {})));
  const subj = (c) => ((c.commit && c.commit.message) || '').split('\n')[0];
  const when = (c) => (c.commit && c.commit.committer && c.commit.committer.date) || '';

  // branch list for the pickers (names only; loaded once the drawer is used)
  let branchesLoaded = false;
  async function loadBranches() {
    if (branchesLoaded) return; branchesLoaded = true;
    try {
      const names = [];
      for (let page = 1; page <= 10; page++) { const b = arr(await gh('list_branches', { perPage: 100, page })); names.push(...b.map((x) => x.name)); if (b.length < 100) break; }
      const dl = $('branches'); dl.textContent = '';
      for (const n of [...new Set(WATCH.concat(names))]) { const o = document.createElement('option'); o.value = n; dl.append(o); }
      log(`${names.length} branches on ${OWNER}/${REPO}`);
    } catch (e) { branchesLoaded = false; log('branch list: ' + ghError(e), 'err'); }
  }

  // ahead/behind from the last 100 commits of each side (enough for two working branches)
  async function compare(a, b) {
    const [ca, cb] = await Promise.all([commits(a, 100), commits(b, 100)]);
    const sa = new Set(ca.map((c) => c.sha)), sb = new Set(cb.map((c) => c.sha));
    const ahead = ca.filter((c) => !sb.has(c.sha)), behind = cb.filter((c) => !sa.has(c.sha));
    return { ahead, behind, far: ahead.length >= 100 || behind.length >= 100, headA: ca[0], headB: cb[0] };
  }
  const n = (list, far) => (far && list.length >= 100 ? '100+' : String(list.length));

  // ---------- Refresh ----------
  let busy = false;
  async function refresh() {
    if (busy) return; busy = true; $('refresh').disabled = true; $('dot').className = 'dot';
    const src = $('src').value.trim(); ls.set('src', src);
    try {
      await buildP; loadBranches();
      log(`repo ${OWNER}/${REPO} \u00b7 this artifact shows ${build ? build.branch + ' @ ' + build.pathSha.slice(0, 7) : 'an unknown commit'}`);
      const [head] = await commits(src, 1);
      if (!head) throw { message: `branch "${src}" not found` };
      log(`${src} head: ${head.sha.slice(0, 7)} \u201c${subj(head)}\u201d ${new Date(when(head)).toLocaleString()}`);
      const touch = await commits(src, 10, DIR);
      const latest = touch[0];
      if (!latest) throw { message: `${src} has no ${DIR} folder` };
      const same = build && build.branch === src && build.pathSha === latest.sha;
      if (same) { log(`studio is up to date (last studio commit ${latest.sha.slice(0, 7)} \u201c${subj(latest)}\u201d)`, 'ok'); }
      else {
        const idx = build ? touch.findIndex((c) => c.sha === build.pathSha) : -1;
        const fresh = idx < 0 ? touch : touch.slice(0, idx);
        log(build && build.branch !== src ? `switching studio source ${build.branch} \u2192 ${src}` : `${idx < 0 ? fresh.length + '+' : fresh.length} new studio commit(s) on ${src}:`, 'new');
        for (const c of fresh.slice(0, 6)) log(`  ${c.sha.slice(0, 7)} ${subj(c)}`, 'new');
        $('dot').className = 'dot new';
        await update(src, head, latest);
      }
      await suggestions(src);
    } catch (e) { $('dot').className = 'dot err'; log('refresh failed: ' + ghError(e), 'err'); }
    finally { busy = false; $('refresh').disabled = false; }
  }
  $('refresh').addEventListener('click', refresh);

  const inject = (html) => html.includes('src="sync.js"') ? html : (/<\/body>/i.test(html) ? html.replace(/<\/body>(?![\s\S]*<\/body>)/i, '<script src="sync.js"></script></body>') : html + '<script src="sync.js"></script>');
  async function update(src, head, latest) {
    const art = await caps.artifact;
    if (!art) { log('cannot republish from this view (open the artifact in claude.ai). The new commits are listed above.', 'warn'); return; }
    log('fetching studio files\u2026');
    const cat = JSON.parse(await getFile('studios.json', src));
    const names = ['index.html'].concat((cat.studios || []).map((s) => s.file).filter(Boolean));
    const files = { 'studios.json': JSON.stringify(cat, null, 2) + '\n' };
    for (const f of names) { const t = await getFile(f, src); files[f] = inject(t); log(`  ${f} ${(t.length / 1024).toFixed(0)} KB`); }
    files['build.json'] = JSON.stringify({ repo: OWNER + '/' + REPO, branch: src, sha: head.sha, pathSha: latest.sha, subject: subj(latest), committedAt: when(latest), publishedAt: new Date().toISOString() }, null, 2);
    try {
      await art.publish(files);
      log(`artifact updated to ${src} @ ${latest.sha.slice(0, 7)} \u2014 reloading`, 'ok');
      await record(`Refreshed studio to ${src} @ ${latest.sha.slice(0, 7)} \u201c${subj(latest)}\u201d`, 'refresh', true);
      try { sessionStorage.setItem(LS + 'reopen', '1'); } catch {}
      setTimeout(() => location.reload(), 1200);
    } catch (e) {
      const c = e && e.code;
      if (c === 'conflict') log('someone else updated the artifact first; this view is reloading to their version', 'warn');
      else if (c === 'not_writer' || c === 'not_granted' || c === 'capability_disabled') log('you can view but not update this artifact. Ask its owner for edit access, or for them to press Refresh.', 'warn');
      else if (c === 'too_large') log('the studio files are too large to publish', 'err');
      else log('publish failed: ' + ((c ? c + ': ' : '') + (e && e.message || e)), 'err');
    }
  }
  try { if (sessionStorage.getItem(LS + 'reopen')) { sessionStorage.removeItem(LS + 'reopen'); $('drawer').hidden = false; } } catch {}

  async function suggestions(src) {
    const others = WATCH.filter((b) => b !== src);
    log('merge suggestions vs ' + src + ':');
    for (const b of others) {
      try {
        const r = await compare(b, src);
        if (r.far) { log(`  ${b}: far apart (100+ commits), merge from a Claude session`, 't'); continue; }
        if (!r.ahead.length && !r.behind.length) { log(`  ${b}: identical`); continue; }
        const s = r.ahead.length ? `\u2192 suggest merging ${b} into ${src}` : 'nothing to take';
        log(`  ${b}: ${r.ahead.length} ahead, ${r.behind.length} behind ${s}`, r.ahead.length ? 'new' : '');
      } catch (e) { log(`  ${b}: ${ghError(e)}`, 'err'); }
    }
  }

  // ---------- Merge: compare, then ask ----------
  $('swap').addEventListener('click', () => { const a = $('from').value; $('from').value = $('to').value; $('to').value = a; });
  $('from').addEventListener('focus', loadBranches); $('to').addEventListener('focus', loadBranches); $('src').addEventListener('focus', loadBranches);
  async function compareUI(thenMerge) {
    const from = $('from').value.trim(), to = $('to').value.trim(); ls.set('from', from); ls.set('to', to);
    const q = $('q'); q.hidden = false; q.textContent = 'comparing\u2026';
    if (!from || !to || from === to) { q.textContent = 'Pick two different branches.'; return; }
    try {
      const r = await compare(from, to);
      log(`${from} vs ${to}: ${n(r.ahead, r.far)} ahead, ${n(r.behind, r.far)} behind`);
      for (const c of r.ahead.slice(0, 8)) log(`  + ${c.sha.slice(0, 7)} ${subj(c)}`);
      if (thenMerge && r.ahead.length && !r.far) confirmMerge(from, to, r, false); else ask(from, to, r);
    } catch (e) { q.textContent = ghError(e); log('compare: ' + ghError(e), 'err'); }
  }
  $('compare').addEventListener('click', () => compareUI(false));
  $('merge').addEventListener('click', () => compareUI(true));

  // ---------- Delete a branch (popup) ----------
  // The GitHub connector has no delete-branch tool, so Delete opens the branch on GitHub's Branches
  // page (one click on its bin icon deletes it) and logs the request where a Claude session can act on it.
  function modal(build) { const box = $('modalBox'); box.textContent = ''; build(box); $('modal').hidden = false; }
  const closeModal = () => { $('modal').hidden = true; };
  $('modalX').addEventListener('click', closeModal);
  $('modal').addEventListener('click', (e) => { if (e.target === $('modal')) closeModal(); });
  function askDelete(name, mergedInto) {
    if (!name) { log('pick a branch to delete', 'warn'); return; }
    modal((box) => {
      const h = document.createElement('b');
      h.textContent = mergedInto ? `Merged ${name} into ${mergedInto}. Delete ${name}?` : `Delete branch ${name}?`;
      const p = document.createElement('div'); p.className = 'meta';
      const guard = GUARDED.includes(name), shown = build && build.branch === name;
      p.textContent = guard ? `${name} is a shared base branch. It cannot be deleted from here.`
        : (shown ? `This artifact pulls its studio from ${name}; after deleting it, pick another branch under Refresh. ` : '')
          + 'Delete opens the branch on GitHub: click the bin icon next to it to delete it (the GitHub connector has no delete tool). Keep leaves it as it is.';
      const row = document.createElement('div'); row.className = 'row';
      if (!guard) {
        const del = document.createElement('a'); del.className = 'btn'; del.target = '_blank'; del.rel = 'noopener';
        del.href = `https://github.com/${OWNER}/${REPO}/branches/all?query=${encodeURIComponent(name)}`;
        del.textContent = `\ud83d\uddd1 Delete ${name}`;
        del.addEventListener('click', () => { log(`delete requested: ${name} (opened on GitHub)`, 'warn'); record(`Delete branch ${name}${mergedInto ? ' (merged into ' + mergedInto + ')' : ''}`, 'delete', true); setTimeout(closeModal, 300); });
        row.append(del);
      }
      const keep = document.createElement('button'); keep.textContent = guard ? 'OK' : `Keep ${name}`;
      keep.addEventListener('click', () => { if (!guard) log(`keeping ${name}`); closeModal(); });
      row.append(keep); box.append(h, p, row);
    });
  }
  function ask(from, to, r) {
    const q = $('q'); q.textContent = '';
    const p = document.createElement('div');
    const guarded = GUARDED.includes(to);
    let text;
    if (!r.ahead.length) text = `${to} already has everything on ${from}. Nothing to merge.`;
    else if (r.far) text = `${from} and ${to} are more than 100 commits apart. Merge them in a Claude session so conflicts get resolved properly. You can still open a PR here.`;
    else text = `${from} has ${r.ahead.length} commit(s) ${to} lacks${r.behind.length ? ` (and is ${r.behind.length} behind it)` : ''}. Merge ${from} into ${to} now?`;
    p.textContent = text; q.append(p);
    if (guarded) { const w = document.createElement('div'); w.className = 'meta'; w.style.color = 'var(--warn)'; w.textContent = `${to} is a shared base branch: merging here affects everyone.`; q.append(w); }
    if (!r.ahead.length) return;
    const row = document.createElement('div'); row.className = 'row';
    const mk = (label, cls, fn) => { const b = document.createElement('button'); b.textContent = label; if (cls) b.className = cls; b.addEventListener('click', fn); row.append(b); return b; };
    if (!r.far) mk('Yes, merge now', guarded ? 'danger' : 'pri', () => confirmMerge(from, to, r, false));
    mk('Open a PR only', '', () => confirmMerge(from, to, r, true));
    mk('Not now', '', async () => { q.hidden = true; log(`decided not to merge ${from} \u2192 ${to} now`); await record(`Not merging ${from} into ${to} yet (${r.ahead.length} commits waiting)`, 'merge', true); });
    q.append(row);
  }
  function confirmMerge(from, to, r, prOnly) {
    const q = $('q'); q.textContent = '';
    const p = document.createElement('div');
    p.textContent = prOnly ? `Open a pull request ${from} \u2192 ${to} on GitHub (as you)?` : `Merge ${from} into ${to} on GitHub now, as you, with a merge commit? This pushes to ${to}. If the branches conflict GitHub refuses and the PR stays open.`;
    const row = document.createElement('div'); row.className = 'row';
    const go = document.createElement('button'); go.className = prOnly ? 'pri' : 'danger'; go.textContent = prOnly ? 'Open PR' : 'Confirm merge';
    const no = document.createElement('button'); no.textContent = 'Cancel';
    no.addEventListener('click', () => ask(from, to, r));
    go.addEventListener('click', () => { go.disabled = no.disabled = true; doMerge(from, to, r, prOnly); });
    row.append(go, no); q.append(p, row);
  }
  async function doMerge(from, to, r, prOnly) {
    const q = $('q');
    try {
      const open = arr(await gh('list_pull_requests', { head: OWNER + ':' + from, base: to, state: 'open', fields: ['number', 'html_url', 'title'] }));
      let pr = open[0];
      if (pr) log(`using open PR #${pr.number} ${pr.html_url}`);
      else {
        const body = `Merge ${from} into ${to}.\n\n${r.ahead.slice(0, 30).map((c) => '- ' + c.sha.slice(0, 7) + ' ' + subj(c)).join('\n')}\n\nOpened from the Vessel Studio sync panel.`;
        const res = await gh('create_pull_request', { title: `Merge ${from} into ${to}`, head: from, base: to, body });
        const pl = res.payload || {}, url = pl.html_url || pl.url || ((res.content || []).map((b) => b.text || '').join(' ').match(/https:\/\/github\.com\/\S+\/pull\/\d+/) || [])[0] || '';
        const num = pl.number || +((url.match(/\/pull\/(\d+)/) || [])[1] || 0);
        pr = { number: num, html_url: url.replace('api.github.com/repos', 'github.com').replace('/pulls/', '/pull/') };
        log(`opened PR #${pr.number || '?'} ${pr.html_url}`, 'ok');
      }
      if (prOnly) { q.textContent = `PR #${pr.number || '?'}: ${pr.html_url}`; await record(`Opened PR ${from} \u2192 ${to}: ${pr.html_url}`, 'merge', true); return; }
      if (!pr.number) { q.textContent = 'PR opened, but its number could not be read: merge it on GitHub.'; log('could not read the PR number; merge it on GitHub', 'warn'); return; }
      await gh('merge_pull_request', { pullNumber: pr.number, merge_method: 'merge', commit_title: `Merge ${from} into ${to}` });
      log(`merged ${from} into ${to} (PR #${pr.number})`, 'ok');
      q.textContent = `Merged ${from} into ${to}. Press Refresh to pull it into this artifact.`;
      await record(`Merged ${from} into ${to} (PR #${pr.number})`, 'merge', true);
      askDelete(from, to);
    } catch (e) {
      const m = ghError(e);
      log('merge: ' + m, 'err');
      q.textContent = /conflict|not mergeable|405/i.test(m) ? `GitHub could not merge (conflicts). The PR stays open; resolve it in a Claude session. ${m}` : m;
    }
  }

  // ---------- Decisions (shared db) ----------
  let me = null, userNs = null;
  async function record(text, kind, quiet) {
    const db = await caps.db;
    if (!db) { if (!quiet) $('decNote').textContent = 'Decisions need the artifact opened in claude.ai, signed in.'; return; }
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
    if (!db) { $('decNote').textContent = 'Shared decisions are available on the hub page in claude.ai.'; $('record').disabled = true; return; }
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
