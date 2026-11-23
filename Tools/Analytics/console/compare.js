/* The Funnels and Releases pages of the Froglet Analytics console (Frogma Rev 66 A, Docs/MENU_REDESIGN_B.md section
   142; Shombith picked "Build them A", 2026-11-17). The server works both out per player and sends counts only:
   POST /v1/funnel, GET /v1/funnel/steps and GET /v1/releases (Tools/Analytics/server/analytics_compare.py).

   Funnels: any events or FTUE steps in order, within 1 / 7 / 30 days, strict or any order, for the players whose first
   step falls in a range; conversion split by platform, device or country; the time between steps; where the ones who
   dropped at the biggest drop went instead. The steps are kept in this browser.
   Releases: app_version A against B over the first 7 days of each, or the same last 7 days: a read per measure
   (better, worse, same, too early under 30), D1 by platform, who is on which version, finished by mode, exceptions
   per day, the launch histogram, the return curve and the top exceptions.

   It draws with the console's own pieces (window.FA_CONSOLE, console.js); every value reaches the page through
   textContent (h()), never as HTML. */
(function () {
  "use strict";
  const F = window.FA_CONSOLE;
  if (!F) return;
  const { h, api, card, empty, table, chartPanel, staticLoad, barList, statRow, fmt, store, state, render, makeFrame,
          SEP, filterQuery, shortDay, segment } = F;

  const pct = (n) => (n === null || n === undefined ? "–" : `${Math.round(n * 10) / 10}%`);
  const secs = (n) => {
    if (n === null || n === undefined) return "–";
    if (n < 90) return `${Math.round(n)} s`;
    if (n < 5400) return `${Math.round(n / 6) / 10} min`;
    if (n < 172800) return `${Math.round(n / 360) / 10} h`;
    return `${Math.round(n / 8640) / 10} days`;
  };
  const isoToday = () => new Date().toISOString().slice(0, 10);
  const isoShift = (iso, n) => { const d = new Date(iso + "T00:00:00Z"); d.setUTCDate(d.getUTCDate() + n); return d.toISOString().slice(0, 10); };

  // A frame over ordered categories (steps, buckets, days) with one series per key: the console's chart pieces draw it.
  function seriesFrame(xs, keys, valueOf, extra) {
    const v = new Map();
    for (const x of xs) for (const k of keys) v.set(String(x) + SEP + String(k), valueOf(x, k));
    return makeFrame(Object.assign({ time: true, xs, keys, by: "series" }, extra || {}, { v }));
  }

  // ------------------------------------------------------------------ Funnels

  const FUNNEL_META = { title: "Funnels", build: true, device: true };
  const FUNNEL_STORE = "fa-funnel";
  const DEFAULT_FUNNEL = { steps: [{ event: "game_first_launched" }, { event: "ftue_completed" }, { event: "game_completed" },
                                   { event: "second_day" }, { event: "crystals_spent" }],
                           within_days: 7, order: "strict", by: "platform", since: "", until: "" };
  const SPLIT_OPTIONS = [["platform", "Platform"], ["device", "Device"], ["country", "Country"], ["none", "None"]];
  const fn = { def: null, steps: null, result: null, error: "", running: false };

  function funnelDef() {
    if (!fn.def) {
      const saved = store.get(FUNNEL_STORE, null);
      fn.def = saved && Array.isArray(saved.steps) && saved.steps.length >= 2 ? saved : JSON.parse(JSON.stringify(DEFAULT_FUNNEL));
    }
    return fn.def;
  }
  const saveDef = () => store.set(FUNNEL_STORE, fn.def);
  const stepValue = (st) => st.step_id ? `ftue:${st.step_id}` : st.event;
  function stepFromValue(value) {
    if (value.startsWith("ftue:")) return { event: "ftue_step_completed", step_id: value.slice(5) };
    return { event: value };
  }

  function stepSelect(st, i) {
    const def = funnelDef();
    const opts = [];
    const steps = fn.steps || { events: [], special: [], ftue_nodes: [] };
    if (i > 0) for (const sp of steps.special) opts.push(h("option", { value: sp.event }, sp.label));
    const nodes = steps.ftue_nodes || [];
    if (nodes.length) opts.push(h("optgroup", { label: "FTUE steps" }, nodes.map((n) =>
      h("option", { value: `ftue:${n.step_id}` }, `${n.name} (${fmt(n.players)})`))));
    opts.push(h("optgroup", { label: "Events" }, (steps.events || []).map((e) =>
      h("option", { value: e.event }, `${e.event.replace(/_/g, " ")} (${fmt(e.players)})`))));
    const sel = h("select", { class: "mini-select", "aria-label": `Step ${i + 1}` }, opts);
    const want = stepValue(st);
    if (![...sel.options].some((o) => o.value === want)) sel.prepend(h("option", { value: want }, want.replace(/_/g, " ")));
    sel.value = want;
    sel.addEventListener("change", () => { def.steps[i] = stepFromValue(sel.value); saveDef(); });
    const remove = def.steps.length > 2 ? h("button", { type: "button", class: "link", "aria-label": `Remove step ${i + 1}`,
      onclick: () => { def.steps.splice(i, 1); saveDef(); render(); } }, "Remove") : null;
    const up = i > 0 ? h("button", { type: "button", class: "link", "aria-label": `Move step ${i + 1} earlier`,
      onclick: () => { const t = def.steps[i - 1]; def.steps[i - 1] = def.steps[i]; def.steps[i] = t; saveDef(); render(); } }, "Earlier") : null;
    return h("div", { class: "funnel-step" }, h("span", { class: "num lab" }, String(i + 1)), sel, up, remove);
  }

  function miniSeg(label, options, current, choose) {
    const box = h("span", { class: "mini-seg-in" });
    segment(box, options, current, (v) => { choose(v); saveDef(); render(); });
    return h("span", { class: "mini-seg" }, h("span", { class: "mini-lab" }, label), box);
  }

  function builder() {
    const def = funnelDef();
    const since = h("input", { class: "field sls compact", type: "date", value: def.since || isoShift(isoToday(), -29), "aria-label": "Players who started from" });
    const until = h("input", { class: "field sls compact", type: "date", value: def.until || isoToday(), "aria-label": "Players who started to" });
    since.addEventListener("change", () => { def.since = since.value; saveDef(); });
    until.addEventListener("change", () => { def.until = until.value; saveDef(); });
    const split = h("select", { class: "mini-select", "aria-label": "Split by" }, SPLIT_OPTIONS.map(([k, l]) => h("option", { value: k }, l)));
    split.value = def.by || "platform";
    split.addEventListener("change", () => { def.by = split.value; saveDef(); });
    const add = def.steps.length < 8 ? h("button", { type: "button", class: "btn sls ghost small-btn",
      onclick: () => { def.steps.push({ event: "game_completed" }); saveDef(); render(); } }, "+ Step") : null;
    const run = h("button", { type: "button", class: "btn sls cta small-btn", onclick: () => runFunnel() }, fn.running ? "Running…" : "Run funnel");
    const reset = h("button", { type: "button", class: "link", onclick: () => { fn.def = JSON.parse(JSON.stringify(DEFAULT_FUNNEL)); saveDef(); fn.result = null; render(); } }, "Reset to the starter funnel");
    return card("Steps, in order", "any event or FTUE step; a second day is any event on a later day",
      h("div", { class: "funnel-steps" }, def.steps.map(stepSelect), add),
      h("div", { class: "funnel-controls" },
        miniSeg("Within", [[1, "1 d"], [7, "7 d"], [30, "30 d"]], def.within_days, (v) => { def.within_days = v; }),
        miniSeg("Order", [["strict", "In this order"], ["any", "Any order"]], def.order, (v) => { def.order = v; }),
        h("span", { class: "mini-seg" }, h("span", { class: "mini-lab" }, "Started"), since, h("span", { class: "small" }, "to"), until),
        h("span", { class: "mini-seg" }, h("span", { class: "mini-lab" }, "Split by"), split),
        run, reset),
      fn.error ? h("p", { class: "small danger", role: "alert" }, fn.error) : null);
  }

  async function runFunnel() {
    const def = funnelDef();
    const q = new URLSearchParams(filterQuery(FUNNEL_META));
    const body = { steps: def.steps, within_days: def.within_days, order: def.order, by: def.by || "platform",
                   since: def.since || undefined, until: def.until || undefined,
                   build: q.get("build") || "release", device: q.get("device") || "all" };
    fn.running = true; fn.error = "";
    render();
    try { fn.result = await api("/v1/funnel", body); fn.result._for = JSON.stringify([body, state.build, state.device]); }
    catch (err) { fn.error = err.message; fn.result = null; }
    fn.running = false;
    render();
  }

  function funnelResult(r) {
    const steps = r.steps || [];
    const labels = steps.map((s, i) => `${i + 1}. ${s.label.replace(/_/g, " ")}`);
    const groups = (r.groups || []).slice(0, 4);
    const top = steps[0] ? steps[0].players : 0;
    const last = steps[steps.length - 1] || {};
    const conv = chartPanel({ id: "funnels.conversion", title: "Conversion", note: `${fmt(top)} players started · each step within ${r.within_days} d`
        + (r.order === "any" ? " of the first, any order" : " of the one before"),
      measures: [["pct_of_start", "% of start"], ["players", "Players"]], forms: ["bars", "line", "table"], views: false,
      emptyText: "Nobody made the first step in this range.",
      load: staticLoad(({ measure }) => {
        if (!top) return seriesFrame([], [], () => null);
        const keys = r.by === "none" ? ["all"] : groups.map((g) => g.group);
        const at = (x, k) => {
          const i = labels.indexOf(x);
          const g = r.by === "none" ? { players: steps.map((s) => s.players), pct_of_start: steps.map((s) => s.pct_of_start) } : groups.find((gg) => gg.group === k);
          return g ? g[measure][i] : null;
        };
        return seriesFrame(labels, keys, at, { kind: measure === "players" ? "count" : "pct", measure,
          label: (k) => (k === "all" ? "All players" : r.by === "device" ? F.deviceLabel(k) : String(k)), kname: "Step", vname: measure === "players" ? "Players" : "% of start" });
      }) });
    const stepRows = steps.map((s, i) => h("tr", {}, h("td", {}, labels[i]), h("td", { class: "num" }, fmt(s.players)),
      h("td", { class: "num" }, pct(s.pct_of_start)),
      h("td", { class: "num" + (s.drop_pct >= 50 ? " danger" : "") }, s.drop_pct === null ? "–" : `-${Math.round(s.drop_pct)}%`)));
    const times = (r.times || []).map((t) => h("tr", {}, h("td", {}, `${t.from.replace(/_/g, " ")} to ${t.to.replace(/_/g, " ")}`),
      h("td", { class: "num" }, fmt(t.players)), h("td", { class: "num" }, secs(t.median_s)), h("td", { class: "num" }, secs(t.p75_s))));
    const inst = r.instead;
    return [
      statRow([["Started", fmt(top)], ["Reached the last step", `${fmt(last.players)} (${pct(last.pct_of_start)})`],
               ["Players who started", `${shortDay(r.since)} to ${shortDay(r.until)}`], ["Build", F.buildLabel(r.build)]]),
      conv,
      h("div", { class: "grid2" },
        card("Step by step", "the drop is from the step before", table(["Step", "Players", "Of start", "Drop"], stepRows)),
        card("Time between steps", r.order === "any" ? "from the first step" : "from the step before",
          times.length ? table(["Between", "Players", "Median", "3 in 4 by"], times) : h("p", { class: "small" }, "–"))),
      inst ? card("Where they went instead", `the ${fmt(inst.players)} who reached step ${inst.after_step} and stopped there: their next event`,
        barList(inst.next.map((n) => [n.event, n.players]), (k) => k.replace(/_/g, " "))) : null,
      h("p", { class: "small" }, "Counts only: the server works the funnel out per player and sends no player id. "
        + "A step in this order must come after the step before; in any order, every step counts within the window from the first."),
    ];
  }

  async function funnelsPage() {
    if (!fn.steps) {
      try { fn.steps = await api(`/v1/funnel/steps?${filterQuery(FUNNEL_META)}`); }
      catch (err) { return [empty(err.message)]; }
    }
    funnelDef();
    if (!fn.result && !fn.running && !fn.error) { runFunnel(); }
    const stale = fn.result && fn.result._for && JSON.parse(fn.result._for)[1] !== state.build;
    const parts = [builder()];
    if (fn.running && !fn.result) parts.push(h("p", { class: "small" }, "Working the funnel out…"));
    else if (fn.result) {
      if (stale) parts.push(h("p", { class: "small" }, "The build changed: Run funnel again to count it."));
      parts.push(...funnelResult(fn.result));
    }
    return parts.filter(Boolean);
  }

  // ------------------------------------------------------------------ Releases

  const RELEASE_META = { title: "Release compare", build: true, device: true };
  const READ_CLASS = { better: "ok-text", worse: "danger", "too early": "soft", same: "soft", changed: "", "no data": "soft" };
  const rel = { a: store.get("fa-rel-a", ""), b: store.get("fa-rel-b", ""), span: store.get("fa-rel-span", "first7") };

  function valueText(key, v) {
    if (v === null || v === undefined) return "–";
    if (/_pct$/.test(key)) return pct(v);
    if (/_s$/.test(key)) return `${Math.round(v * 10) / 10} s`;
    return Number(v).toFixed(2);
  }
  function changeText(key, c) {
    if (c === null || c === undefined) return "–";
    const sign = c > 0 ? "+" : c < 0 ? "−" : "";
    const a = Math.abs(c);
    return /_pct$/.test(key) ? `${sign}${Math.round(a * 10) / 10} pts` : /_s$/.test(key) ? `${sign}${Math.round(a * 10) / 10} s` : `${sign}${Math.round(a * 100) / 100}`;
  }

  function picker(versions) {
    const opt = (v) => h("option", { value: v.version }, `${v.version} · ${shortDay(v.first_day)} · ${fmt(v.new_players)} new`);
    const make = (letter, current) => {
      const sel = h("select", { class: "mini-select", "aria-label": `Version ${letter}` }, versions.map(opt));
      sel.value = current;
      sel.addEventListener("change", () => { rel[letter.toLowerCase()] = sel.value; store.set(`fa-rel-${letter.toLowerCase()}`, sel.value); render(); });
      return h("span", { class: "mini-seg rel-pick rel-" + letter.toLowerCase() }, h("span", { class: "num lab" }, letter), sel);
    };
    const box = h("span", { class: "mini-seg-in" });
    segment(box, [["first7", "First 7 days of each"], ["calendar", "Same last 7 days"]], rel.span, (v) => { rel.span = v; store.set("fa-rel-span", v); render(); });
    return h("div", { class: "funnel-controls" }, make("A", rel.a), h("span", { class: "lab" }, "against"), make("B", rel.b),
      h("span", { class: "mini-seg" }, h("span", { class: "mini-lab" }, "Compare"), box));
  }

  function pairFrame(xs, A, B, valueOf, extra) {
    return seriesFrame(xs, ["A", "B"], (x, k) => valueOf(k === "A" ? A : B, x),
      Object.assign({ label: (k) => `${k} ${k === "A" ? A.version : B.version}` }, extra || {}));
  }

  async function releasesPage() {
    const q = filterQuery(RELEASE_META);
    const list = await api(`/v1/releases?${q}`);
    const versions = list.versions || [];
    if (versions.length < 2) return [empty(versions.length ? "Only one version has sent events in this build: Release compare needs two."
      : "No version has sent events in this build yet.")];
    const known = new Set(versions.map((v) => v.version));
    if (!known.has(rel.b)) rel.b = versions[0].version;
    if (!known.has(rel.a) || rel.a === rel.b) rel.a = (versions.find((v) => v.version !== rel.b) || {}).version;
    const p = await api(`/v1/releases?${q}&a=${encodeURIComponent(rel.a)}&b=${encodeURIComponent(rel.b)}&span=${rel.span}`);
    const A = p.a, B = p.b, t = p.tally || {};
    const readRows = (p.reads || []).map((r) => h("tr", {}, h("td", {}, r.label), h("td", { class: "num" }, valueText(r.measure, r.a)),
      h("td", { class: "num" }, valueText(r.measure, r.b)), h("td", { class: "num" }, changeText(r.measure, r.change)),
      h("td", { class: READ_CLASS[r.read] || "" }, r.read)));
    const fams = [...new Set(Object.keys(A.d1_by_platform || {}).concat(Object.keys(B.d1_by_platform || {})))].sort();
    const famRows = fams.map((f) => {
      const a = (A.d1_by_platform || {})[f] || {}, b = (B.d1_by_platform || {})[f] || {};
      return h("tr", {}, h("td", {}, f), h("td", { class: "num" }, `${pct(a.pct)} of ${fmt(a.players || 0)}`),
        h("td", { class: "num" }, `${pct(b.pct)} of ${fmt(b.players || 0)}`));
    });
    const days = (A.exceptions_by_day || []).map((d) => `day ${d.day}`);
    const curveXs = (A.return_curve || []).map((c) => `D${c.day}`);
    const buckets = (A.launch_histogram || []).map((b) => b.bucket);
    const modes = p.top_modes || [];
    return [
      picker(versions),
      statRow([["A", `${A.version} · ${shortDay(A.from)} to ${shortDay(A.to)} · ${fmt(A.new_players)} new, ${fmt(A.players)} players`],
               ["B", `${B.version} · ${shortDay(B.from)} to ${shortDay(B.to)} · ${fmt(B.new_players)} new, ${fmt(B.players)} players`]]),
      card("Is B better?", `better on ${fmt(t.better || 0)}, worse on ${fmt(t.worse || 0)}, too early on ${fmt(t["too early"] || 0)}`,
        table(["Measure", `A ${A.version}`, `B ${B.version}`, "Change", "Read"], readRows),
        h("p", { class: "small" }, "Too early when either side has under 30 in what the measure is counted over. Launch to menu counts only the first menu of each app run; "
          + "D1 and D7 are each version's new players; rows per play is 1.00 when nothing is sent twice.")),
      h("div", { class: "grid2" },
        card("Back next day, by platform", "each version's new players", famRows.length ? table(["Platform", "A", "B"], famRows) : h("p", { class: "small" }, "–")),
        card("Who is on which version", "players seen in the last 7 days, by the version of their latest event",
          barList((p.on_version_now || []).map((m) => [m.version, m.players]), (k) => k, { top: 6 }))),
      chartPanel({ id: "releases.modes", title: "Finished, by mode", note: "the six modes most played across both", forms: ["table"], views: false,
        emptyText: "No matches on either version in these days.",
        load: staticLoad(() => pairFrame(modes, A, B, (S, m) => ((S.finished_by_mode || {})[m] || {}).pct ?? null, { kind: "pct", kname: "Mode", vname: "Finished" })) }),
      chartPanel({ id: "releases.exceptions", title: "Exceptions per 100 sessions", note: "by day from each version's start", forms: ["line", "bars", "table"], views: false,
        emptyText: "No sessions on either version in these days.",
        load: staticLoad(() => pairFrame(days, A, B, (S, x) => ((S.exceptions_by_day || [])[Number(x.slice(4))] || {}).per_100 ?? null, { kind: "avg", kname: "Day", vname: "Per 100 sessions" })) }),
      chartPanel({ id: "releases.launch", title: "Launch to menu", note: "launches by seconds, first menu of each app run", forms: ["line", "table"], views: false,
        emptyText: "No launches timed on either version.",
        load: staticLoad(() => pairFrame(buckets, A, B, (S, x) => ((S.launch_histogram || []).find((b) => b.bucket === x) || {}).launches ?? 0, { kname: "Seconds", vname: "Launches" })) }),
      chartPanel({ id: "releases.return", title: "Coming back", note: "share of each version's new players who played on day N or later", forms: ["line", "bars", "table"], views: false,
        emptyText: "No new players on either version.",
        load: staticLoad(() => pairFrame(curveXs, A, B, (S, x) => ((S.return_curve || [])[Number(x.slice(1))] || {}).pct ?? null, { kind: "pct", kname: "Day", vname: "Returned" })) }),
      card(`Top exceptions in B (${B.version})`, "type and first frame; never the message text",
        (B.top_exceptions || []).length ? table(["Type", "Frame", "Count"], B.top_exceptions.map((e) => h("tr", {},
          h("td", {}, e.exception_type), h("td", {}, h("span", { class: "code-inline" }, e.top_frame)), h("td", { class: "num" }, fmt(e.count)))))
          : h("p", { class: "small" }, "None sent in these days.")),
    ];
  }

  F.register("funnels", FUNNEL_META, funnelsPage);
  F.register("releases", RELEASE_META, releasesPage);
})();
