/* Amoebius's Vessel Studio backend, as the claude.ai viewer gives it to the artifact: window.claude.use(name) for
   'db', 'user', 'sample' and 'mcp' (the Sync panel's "Claude Code Remote" session is Amoebius itself). Served by the
   launcher's loopback server (Port/src/CosmicShore.Launcher/Studio/StudioServer.cs) as the first script of every studio
   page it serves; the studio files themselves are the published build, unchanged (/vessel-studio D33). */
(() => {
  if (window.claude) return;
  // this script is <token>/.amoebius/bridge.js, so its own folder is the API's
  const base = document.currentScript ? new URL('./', document.currentScript.src).href : new URL('.amoebius/', location.href).href;
  const fail = (code, message) => Object.assign(new Error(message || code), { code });
  async function call(op, body) {
    let r;
    try {
      r = await fetch(base + 'api', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(Object.assign({ op }, body || {})) });
    } catch (e) { throw fail('unavailable', 'Amoebius is not reachable (is it still running?)'); }
    let j = null; try { j = await r.json(); } catch (e) { j = null; }
    if (!r.ok || !j) throw fail(r.status === 403 ? 'permission_denied' : 'unavailable', 'Amoebius answered ' + r.status);
    if (j.error) throw fail(j.error.code || 'error', j.error.message);
    return j;
  }
  const docSnap = (id, data) => ({ id, exists: data != null, data: () => (data == null ? undefined : data) });
  const querySnap = (docs) => ({ docs: docs.map((d) => docSnap(d.id, d.data)), size: docs.length, empty: !docs.length, forEach(f) { this.docs.forEach(f); } });
  // live queries: polled (the store is on this computer); a listener that errors stops, as Firestore's does
  function listen(fetcher, onData, onErr) {
    let stop = false, last = null, timer = null;
    const tick = async () => {
      if (stop) return;
      try {
        const j = await fetcher(); const k = JSON.stringify(j.docs !== undefined ? j.docs : j.data);
        if (k !== last) { last = k; onData(j); }
      } catch (e) { stop = true; if (onErr) onErr(e); return; }
      if (!stop) timer = setTimeout(tick, document.hidden ? 3000 : 700);
    };
    tick();
    return () => { stop = true; clearTimeout(timer); };
  }
  const newId = () => { const a = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789', b = crypto.getRandomValues(new Uint8Array(20)); return Array.from(b, (x) => a[x % a.length]).join(''); };
  function docRef(path, id) {
    const get = () => call('db.get', { path, id });
    return {
      id, path: path + '/' + id,
      get: async () => docSnap(id, (await get()).data),
      set: (data) => call('db.set', { path, id, data }).then(() => undefined),
      update: (data) => call('db.update', { path, id, data }).then(() => undefined),
      delete: () => call('db.delete', { path, id }).then(() => undefined),
      onSnapshot: (cb, err) => listen(get, (j) => cb(docSnap(id, j.data)), err),
    };
  }
  function query(path, q) {
    const list = () => call('db.list', Object.assign({ path }, q));
    return {
      orderBy: (field, dir) => query(path, Object.assign({}, q, { orderBy: field, dir: dir === 'desc' ? 'desc' : 'asc' })),
      limit: (n) => query(path, Object.assign({}, q, { limit: n })),
      get: async () => querySnap((await list()).docs),
      onSnapshot: (cb, err) => listen(list, (j) => cb(querySnap(j.docs)), err),
    };
  }
  const db = {
    collection(path) {
      const c = query(path, {});
      c.add = async (data) => docRef(path, (await call('db.add', { path, data })).id);
      c.doc = (id) => docRef(path, id || newId());
      return c;
    },
  };
  let myId = null;
  const user = {
    id: async () => (myId = myId || (await call('user.id')).id),
    profiles: async (ids) => (await call('user.profiles', { ids })).profiles,
  };
  // sample(prompt, { onText }): the answer streams in; onText gets the whole answer so far
  async function sample(prompt, opts) {
    opts = opts || {};
    let r;
    try {
      r = await fetch(base + 'sample', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: typeof prompt === 'string' ? prompt : JSON.stringify(prompt) }) });
    } catch (e) { throw fail('unavailable', 'Amoebius is not reachable'); }
    if (!r.ok || !r.body) throw fail(r.status === 403 ? 'permission_denied' : 'unavailable', 'Amoebius answered ' + r.status);
    const reader = r.body.getReader(), dec = new TextDecoder();
    let buf = '', text = '';
    for (;;) {
      const { value, done } = await reader.read();
      if (value) buf += dec.decode(value, { stream: true });
      let i;
      while ((i = buf.indexOf('\n')) >= 0) {
        const line = buf.slice(0, i).trim(); buf = buf.slice(i + 1);
        if (!line) continue;
        const m = JSON.parse(line);
        if (m.error) throw fail(m.error.code || 'error', m.error.message);
        if (typeof m.text === 'string') { text = m.text; if (opts.onText) opts.onText({ text }); }
        if (m.done) return { text };
      }
      if (done) break;
    }
    throw fail('unavailable', 'the answer stopped');
  }
  const mcp = { callTool: async (server, tool, args) => (await call('mcp.call', { server, tool, args: args || {} })).result };
  const caps = { db, user, sample, mcp };
  const granted = {};
  window.claude = {
    use(name) {
      if (!(name in caps)) return Promise.reject(fail('not_granted', name + ' is not available here'));
      if (!granted[name]) granted[name] = call('use', { name }).then(() => caps[name]);
      return granted[name];
    },
  };
})();
