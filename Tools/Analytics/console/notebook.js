/* The Notebook page of the Froglet Analytics console: a chat, a report and a notebook in one (Shombith, 2026-11-07:
   "a chat session or report data session or notebook inside the analytics tool"). The server side and its rules are
   Tools/Analytics/server/analytics_notebook.py.

   A notebook is cells in order: notes (a small markdown subset), SQL over the aggregates (the Query Studio's
   read-only door), charts (of a SQL cell's result, or one of the console's switchable series) and questions to
   Claude. The ask box is pinned under the cells: a question goes in as a cell and Claude's answer comes back as cells
   (a note, and when it wrote a query, that query already run and a chart of it). Claude reads the notebook's earlier
   cells, re-run on the server, so a follow-up builds on them. Report shows the outputs only, with the title, the date
   and the filters, for Print / Save as PDF and Copy as Markdown.

   It draws with the console's own pieces (window.FA_CONSOLE, console.js) and every value reaches the page through
   textContent (h()), never as HTML. The notebook's filters ARE the header's while it is open: opening one sets the
   header to its filters, and Save keeps the header's. */
(function () {
  "use strict";
  const F = window.FA_CONSOLE;
  if (!F) return;
  const { h, api, card, empty, table, chartPanel, seriesPanel, staticLoad, resultFrame, fmt, when, store, state } = F;

  const OPEN_STORE = "fa-nb-open";
  const KIND_LABEL = { text: "Note", sql: "SQL", chart: "Chart", ask: "Question" };
  const VIS = [["me", "Only me"], ["readers", "Readers"], ["admins", "Admins"]];
  const SUGGEST = ["Which play type brings players back for a rematch?", "Which modes do players finish least often?",
                   "How many matches a day this month, by platform?"];
  const SERIES_DEFAULT = { from: "series", measure: "matches", by: "play_type", over: "week", form: "bars" };
  const nb = { list: null, book: null, saved: "", report: false, editing: new Set(), running: new Set(), asking: false,
               status: "", statusBad: false, draft: "", asksLeft: null, views: new Map(), copied: null, focus: null };
  const newId = () => "c_" + Math.random().toString(36).slice(2, 10);
  const filtersNow = () => F.pageFilters(META);
  const isMine = () => !nb.book || !nb.book.owner || nb.book.owner === state.me;
  const visLabel = (v) => (VIS.find(([k]) => k === v) || [, v])[1];

  // ------------------------------------------------------------------ the notebook as the server keeps it

  // Only the fields the server keeps (analytics_notebook.check_cells): a cell carries no page state.
  function clean(cell) {
    const out = { id: cell.id, kind: cell.kind, source: cell.source || "", by: cell.by === "claude" ? "claude" : "me" };
    if (cell.at_ms) out.at_ms = cell.at_ms;
    if (cell.kind === "chart") out.chart = Object.assign({}, cell.chart || SERIES_DEFAULT);
    if ((cell.kind === "sql" || cell.kind === "ask") && cell.output) {
      const o = cell.output, kept = {};
      for (const k of ["columns", "rows", "truncated", "ms", "ran_ms", "error"]) if (o[k] !== undefined && o[k] !== null) kept[k] = o[k];
      if (cell.kind === "ask") { if (kept.error) out.output = { error: kept.error }; }
      else out.output = kept;
    }
    return out;
  }
  const snapshotOf = (b) => JSON.stringify({ name: b.name, visibility: b.visibility, filters: b.filters, cells: b.cells.map(clean) });
  const dirty = () => !!nb.book && snapshotOf(Object.assign({}, nb.book, { filters: filtersNow() })) !== nb.saved;

  function blank() {
    return { id: null, name: "Untitled notebook", visibility: "me", owner: state.me, filters: filtersNow(), cells: [], updated_ms: null };
  }
  function adopt(book) {
    nb.book = book;
    nb.book.cells = (book.cells || []).map((c) => Object.assign({}, c));
    nb.saved = snapshotOf(nb.book);
    nb.editing.clear();
    nb.views.clear();
    store.set(OPEN_STORE, book.id || "");
  }
  // Opening a notebook puts its filters in the header (an era that no longer exists is dropped).
  function applyFilters(f) {
    f = f || {};
    if (f.build) state.build = f.build;
    if (f.days) state.days = f.days;
    state.play = f.play || "all";
    state.era = f.era && F.eraById(f.era) ? f.era : "";
  }
  async function loadList() {
    try { nb.list = (await api("/v1/notebooks")).notebooks || []; } catch (e) { nb.list = []; nb.status = e.message; nb.statusBad = true; }
  }
  async function open(id) {
    if (!id) { adopt(blank()); return; }
    const book = await api(`/v1/notebooks?id=${encodeURIComponent(id)}`);
    applyFilters(book.filters);
    adopt(book);
  }
  function say(text, bad) { nb.status = text; nb.statusBad = !!bad; }

  // Chart cells keep the switches the reader set (form, measure, split, over): read them back before each redraw.
  function harvest() {
    if (!nb.book) return;
    for (const cell of nb.book.cells) {
      const view = nb.views.get(cell.id);
      if (!view || cell.kind !== "chart" || typeof view.pick !== "function") continue;
      const p = view.pick();
      const c = cell.chart || (cell.chart = Object.assign({}, SERIES_DEFAULT));
      if (p.form) c.form = p.form;
      if (c.from === "series") for (const k of ["measure", "by", "over"]) if (p[k]) c[k] = p[k];
    }
  }

  async function save() {
    if (F.SNAPSHOT) { say("Saving is off in this preview: there is no server behind it.", true); return F.render(); }
    harvest();
    const b = nb.book;
    const body = { name: (b.name || "").trim() || "Untitled notebook", visibility: b.visibility, filters: filtersNow(), cells: b.cells.map(clean) };
    try {
      let made;
      if (b.id && isMine()) made = await api("/v1/notebooks", Object.assign({ id: b.id }, body), "PUT");
      else made = await api("/v1/notebooks", b.id ? Object.assign({}, body, { name: `Copy of ${body.name}`.slice(0, 80), visibility: "me" }) : body);
      adopt(made);
      await loadList();
      say(`Saved ${when(made.updated_ms)} UTC${made.visibility === "me" ? ", for you" : ` for ${visLabel(made.visibility).toLowerCase()}`}.`);
    } catch (e) { say(e.message, true); }
    F.render();
  }
  async function duplicate() {
    if (!nb.book.id) { nb.book.name = `Copy of ${nb.book.name}`.slice(0, 80); say("Copied. Save keeps it."); return F.render(); }
    if (F.SNAPSHOT) { say("Duplicating is off in this preview: there is no server behind it.", true); return F.render(); }
    try {
      const made = await api("/v1/notebooks", { duplicate: nb.book.id });
      adopt(made); await loadList(); say("Duplicated: this copy is yours, only you see it.");
    } catch (e) { say(e.message, true); }
    F.render();
  }
  async function remove() {
    const b = nb.book;
    if (!b.id) { adopt(blank()); return F.render(); }
    if (!window.confirm(`Delete the notebook "${b.name}" for everyone who can see it?`)) return;
    if (F.SNAPSHOT) { say("Deleting is off in this preview: there is no server behind it.", true); return F.render(); }
    try {
      await api(`/v1/notebooks?id=${encodeURIComponent(b.id)}`, null, "DELETE");
      await loadList();
      adopt(blank()); say("Deleted.");
    } catch (e) { say(e.message, true); }
    F.render();
  }
  async function pick(id) {
    if (dirty() && !window.confirm("This notebook has changes that are not saved. Open another and lose them?")) return F.render();
    try { await open(id); say(""); } catch (e) { say(e.message, true); }
    F.render();
  }

  // ------------------------------------------------------------------ running cells, asking Claude

  async function runSql(cell) {
    if (F.SNAPSHOT) { cell.output = Object.assign({}, cell.output || {}, { error: "Running a query needs the server; this review copy shows the result saved with the notebook." }); return F.render(); }
    nb.running.add(cell.id); F.render();
    try { cell.output = (await api("/v1/notebooks/run", { cell: { kind: "sql", source: cell.source } })).output; }
    catch (e) { cell.output = { error: e.message }; }
    nb.running.delete(cell.id);
    F.render();
  }

  async function ask(question) {
    question = (question || "").trim();
    if (!question || nb.asking) return;
    harvest();
    const earlier = nb.book.cells.map(clean);
    const cell = { id: newId(), kind: "ask", source: question.slice(0, 500), by: "me", at_ms: Date.now() };
    nb.book.cells.push(cell);
    nb.draft = "";
    if (F.SNAPSHOT) {
      cell.output = { error: "Asking Claude needs the server: this review copy has none behind it. On the live console Claude reads this notebook's cells and answers here, as new cells." };
      F.render(); return scrollDown();
    }
    nb.asking = true; F.render(); scrollDown();
    try {
      const out = await api("/v1/notebooks/ask", { question: cell.source, cells: earlier, filters: filtersNow() });
      if (out.error) cell.output = { error: out.error };
      const at = nb.book.cells.indexOf(cell);
      nb.book.cells.splice(at + 1, 0, ...(out.cells || []));
      if (typeof out.asks_left === "number") nb.asksLeft = out.asks_left;
    } catch (e) { cell.output = { error: e.message }; }
    nb.asking = false;
    F.render(); scrollDown();
  }
  // The dock is pinned, so scrolling to it moves nothing: go to the end of the page, where the new cells are.
  function scrollDown() { setTimeout(() => window.scrollTo({ top: document.documentElement.scrollHeight, behavior: "smooth" }), 80); }

  function addCell(kind, after) {
    harvest();
    const cell = { id: newId(), kind, source: "", by: "me", at_ms: Date.now() };
    if (kind === "chart") cell.chart = Object.assign({}, after && after.kind === "sql" ? chartOfSql(after) : SERIES_DEFAULT);
    if (kind === "sql") cell.source = "SELECT game_mode, COUNT(DISTINCT match_id) AS matches\nFROM plays\nWHERE build_kind = 'release'\nGROUP BY game_mode\nORDER BY matches DESC\nLIMIT 12";
    const cells = nb.book.cells;
    cells.splice(after ? cells.indexOf(after) + 1 : cells.length, 0, cell);
    if (kind !== "chart") { nb.editing.add(cell.id); nb.focus = cell.id; }
    F.render();
  }
  function chartOfSql(cell) {
    const o = cell.output || {}, cols = o.columns || [], rows = o.rows || [];
    const y = cols.find((c, i) => i > 0 && rows.some((r) => typeof r[i] === "number")) || cols[1] || cols[0];
    return { from: cell.id, form: "bars", x: cols[0], y };
  }
  function move(cell, by) {
    harvest();
    const cells = nb.book.cells, i = cells.indexOf(cell), j = i + by;
    if (j < 0 || j >= cells.length) return;
    cells.splice(i, 1); cells.splice(j, 0, cell);
    F.render();
  }
  function drop(cell) {
    harvest();
    const used = nb.book.cells.filter((c) => c.kind === "chart" && c.chart && c.chart.from === cell.id);
    if (used.length && !window.confirm(`This query has ${used.length} chart${used.length > 1 ? "s" : ""}; delete ${used.length > 1 ? "them" : "it"} too?`)) return;
    nb.book.cells = nb.book.cells.filter((c) => c !== cell && !used.includes(c));
    F.render();
  }

  // ------------------------------------------------------------------ markdown (a small subset, drawn as DOM)
  // # ## ### headings, - or * lists, 1. lists, ``` fenced code, **bold**, *italic* and `code` inside a line.

  function inline(text) {
    const out = [];
    const re = /(`[^`]+`|\*\*[^*]+\*\*|\*[^*\s][^*]*\*)/g;
    let last = 0, m;
    while ((m = re.exec(text))) {
      if (m.index > last) out.push(text.slice(last, m.index));
      const t = m[0];
      out.push(t[0] === "`" ? h("code", { class: "nb-code" }, t.slice(1, -1)) : t.startsWith("**") ? h("strong", {}, t.slice(2, -2)) : h("em", {}, t.slice(1, -1)));
      last = m.index + t.length;
    }
    if (last < text.length) out.push(text.slice(last));
    return out;
  }
  function markdown(src) {
    const lines = String(src || "").replace(/\r/g, "").split("\n"), out = [];
    let i = 0;
    while (i < lines.length) {
      const line = lines[i];
      if (!line.trim()) { i++; continue; }
      if (/^```/.test(line)) {
        const code = []; i++;
        while (i < lines.length && !/^```/.test(lines[i])) code.push(lines[i++]);
        i++; out.push(h("pre", { class: "code" }, code.join("\n"))); continue;
      }
      const head = /^(#{1,3})\s+(.*)$/.exec(line);
      if (head) { out.push(h(["h2", "h3", "h4"][head[1].length - 1], { class: ["h2", "h3", "nb-h4"][head[1].length - 1] }, inline(head[2]))); i++; continue; }
      if (/^\s*([-*]|\d+\.)\s+/.test(line)) {
        const ordered = /^\s*\d+\./.test(line), items = [];
        while (i < lines.length && /^\s*([-*]|\d+\.)\s+/.test(lines[i])) items.push(h("li", {}, inline(lines[i++].replace(/^\s*([-*]|\d+\.)\s+/, ""))));
        out.push(h(ordered ? "ol" : "ul", { class: "nb-list" }, items)); continue;
      }
      const para = [];
      while (i < lines.length && lines[i].trim() && !/^(#{1,3}\s|```|\s*([-*]|\d+\.)\s+)/.test(lines[i])) para.push(lines[i++]);
      out.push(h("p", { class: "body" }, para.flatMap((l, n) => (n ? [h("br", {}), ...inline(l)] : inline(l)))));
    }
    return h("div", { class: "nb-md" }, out.length ? out : h("p", { class: "small" }, "An empty note."));
  }

  // ------------------------------------------------------------------ cells

  function resultTable(o) {
    if (!o) return null;
    if (o.error) return h("p", { class: "small danger" }, o.error);
    const cols = o.columns || [], rows = o.rows || [];
    if (!cols.length) return h("p", { class: "small" }, "No result yet: Run it.");
    return h("div", { class: "nb-result" },
      table(cols, rows.map((r) => h("tr", {}, r.map((c) => h("td", {}, c === null || c === undefined ? "–" : typeof c === "number" ? fmt(c) : String(c)))))),
      h("p", { class: "small num" }, [`${fmt(rows.length)} row${rows.length === 1 ? "" : "s"}${o.truncated ? " (the first 200)" : ""}`,
        o.ms !== undefined ? `${fmt(o.ms)} ms` : null, o.ran_ms ? `run ${when(o.ran_ms)} UTC` : null].filter(Boolean).join(" · ")));
  }

  function chartOf(cell, report) {
    const c = cell.chart || SERIES_DEFAULT, id = `nb.${cell.id}`;
    // the cell's own switches win over this browser's memory of the chart (a shared notebook draws as its owner left it)
    store.set("fa-chart-" + id, c.from === "series" ? { form: c.form, measure: c.measure, by: c.by, over: c.over } : { form: c.form });
    let panel;
    if (c.from === "series") {
      const title = `${(F.MEASURES[c.measure] || [c.measure])[0]}${c.by && c.by !== "none" ? ` by ${c.by.replace(/_/g, " ")}` : ""}, ${({ day: "per day", week: "per week", none: "in total" })[c.over] || ""}`;
      // a Toybox measure keeps the Toybox's switches; a match measure the match charts' (the server hides what it cannot split)
      const toybox = /^freestyle_/.test(c.measure || "");
      const family = toybox ? Object.keys(F.MEASURES).filter((k) => /^freestyle_/.test(k)) : F.MATCH_MEASURES;
      panel = seriesPanel(id, c.title || title, { form: c.form, measure: c.measure, by: c.by, over: c.over }, { views: false, cls: "nb-chart",
        measures: family.map((k) => [k, F.MEASURES[k][0]]),
        splits: toybox ? ["none", "era", "build_kind", "vessel_class", "from_vessel_class", "switch_source"] : F.MATCH_SPLITS });
    } else {
      const src = nb.book.cells.find((x) => x.id === c.from);
      const o = src && src.output;
      if (!o || o.error || !(o.columns || []).length) return card(c.title || "Chart", null, h("p", { class: "small" }, "Its query has no result yet: run the SQL cell above."));
      const cols = o.columns;
      if (!cols.includes(c.x)) c.x = cols[0];
      if (!cols.includes(c.y)) c.y = chartOfSql(src).y;
      const picker = (label, key) => {
        const sel = h("select", { class: "mini-select", "aria-label": label }, cols.map((k) => h("option", { value: k, selected: k === c[key] || null }, k)));
        sel.addEventListener("change", () => { harvest(); c[key] = sel.value; F.render(); });
        return h("label", { class: "mini-seg" }, h("span", { class: "mini-lab" }, label), sel);
      };
      const n = nb.book.cells.filter((x) => x.kind === "sql").indexOf(src) + 1;
      panel = chartPanel({ id, title: c.title || `Query ${n}: ${c.y} by ${c.x}`, views: false, cls: "nb-chart",
        forms: ["bars", "line", "pie", "table", "number"], defaults: { form: c.form || "bars" },
        load: staticLoad(() => resultFrame(o, c.x, c.y)),
        after: report ? null : h("div", { class: "row" }, picker("X", "x"), picker("Y", "y")) });
    }
    const view = panel.querySelector(".chart-view");
    if (view) nb.views.set(cell.id, view);
    return panel;
  }

  function actions(cell, i, count) {
    const btn = (label, onclick, extra) => h("button", Object.assign({ type: "button", class: "nb-act", onclick }, extra || {}), label);
    const editing = nb.editing.has(cell.id);
    const items = [];
    if (cell.kind === "sql") items.push(btn(nb.running.has(cell.id) ? "Running…" : "Run", () => runSql(cell), { disabled: nb.running.has(cell.id) || null, class: "nb-act run" }));
    if (cell.kind === "sql" && cell.output && (cell.output.columns || []).length) items.push(btn("Chart it", () => addCell("chart", cell)));
    if (cell.kind === "text" || cell.kind === "sql") items.push(btn(editing ? "Done" : "Edit", () => { harvest(); editing ? nb.editing.delete(cell.id) : nb.editing.add(cell.id); F.render(); }));
    items.push(btn("↑", () => move(cell, -1), { "aria-label": "Move up", disabled: i === 0 || null }),
      btn("↓", () => move(cell, 1), { "aria-label": "Move down", disabled: i === count - 1 || null }),
      btn("×", () => drop(cell), { "aria-label": "Delete this cell", class: "nb-act x" }));
    return h("div", { class: "nb-actions" }, items);
  }

  function cellView(cell, i, count) {
    const editing = nb.editing.has(cell.id);
    const claude = cell.by === "claude";
    const label = h("span", { class: "lab nb-kind" }, cell.kind === "ask" ? "You asked" : KIND_LABEL[cell.kind],
      claude ? h("span", { class: "nb-by" }, "Claude") : null);
    const head = h("div", { class: "nb-head" }, label, actions(cell, i, count));
    let body;
    if (cell.kind === "ask") {
      body = h("div", { class: "nb-bubble sls" }, h("p", { class: "body" }, cell.source),
        cell.output && cell.output.error ? h("p", { class: "small danger" }, cell.output.error) : null);
    } else if (cell.kind === "text") {
      if (editing) {
        const area = h("textarea", { class: "field code-field nb-edit sls", rows: "6", "aria-label": "Note (markdown)", spellcheck: "true" }, cell.source);
        area.addEventListener("input", () => { cell.source = area.value; });
        body = h("div", { class: "card panel sl" }, area, h("p", { class: "small" }, "# heading, - list, **bold**, `code`. Done shows it."));
        if (nb.focus === cell.id) { nb.focus = null; setTimeout(() => area.focus(), 0); }
      } else body = h("div", { class: "card panel sl nb-note" + (claude ? " claude" : ""), ondblclick: () => { nb.editing.add(cell.id); F.render(); } }, markdown(cell.source));
    } else if (cell.kind === "sql") {
      const code = editing
        ? (() => {
          const area = h("textarea", { class: "field code-field sls", rows: "7", spellcheck: "false", "aria-label": "SQL" }, cell.source);
          area.addEventListener("input", () => { cell.source = area.value; });
          area.addEventListener("keydown", (e) => { if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) { e.preventDefault(); runSql(cell); } });
          if (nb.focus === cell.id) { nb.focus = null; setTimeout(() => area.focus(), 0); }
          return area;
        })()
        : h("pre", { class: "code nb-sql", ondblclick: () => { nb.editing.add(cell.id); F.render(); } }, cell.source || "–");
      body = h("div", { class: "card panel sl" + (claude ? " claude" : "") }, code, resultTable(cell.output),
        editing ? h("p", { class: "small" }, "Ctrl + Enter runs it. aggregates.db only, read-only, 200 rows.") : null);
    } else body = chartOf(cell, false);
    return h("div", { class: `nb-cell nb-k-${cell.kind}${claude ? " by-claude" : ""}`, "data-cell": cell.id }, head, body);
  }

  // ------------------------------------------------------------------ the page parts

  function picker() {
    const list = nb.list || [];
    const mine = list.filter((n) => n.owner === state.me), shared = list.filter((n) => n.owner !== state.me);
    const label = (n) => `${n.name} · ${n.cell_count} cells${n.owner !== state.me ? ` · ${n.owner}` : n.visibility !== "me" ? ` · ${visLabel(n.visibility).toLowerCase()}` : ""}`;
    const sel = h("select", { class: "mini-select nb-pick", "aria-label": "Open a notebook" },
      h("option", { value: "", selected: !nb.book.id || null }, nb.book.id ? "New notebook" : "New notebook (this one)"),
      mine.length ? h("optgroup", { label: "Mine" }, mine.map((n) => h("option", { value: n.id, selected: n.id === nb.book.id || null }, label(n)))) : null,
      shared.length ? h("optgroup", { label: "Shared with me" }, shared.map((n) => h("option", { value: n.id, selected: n.id === nb.book.id || null }, label(n)))) : null);
    sel.addEventListener("change", () => pick(sel.value));
    return h("label", { class: "mini-seg" }, h("span", { class: "mini-lab" }, "Open"), sel);
  }

  function toolbar() {
    const b = nb.book, mine = isMine();
    const name = h("input", { class: "field sls nb-title", type: "text", maxlength: "80", value: b.name, "aria-label": "Notebook name" });
    name.addEventListener("input", () => { b.name = name.value; });
    // no redraw on change: a redraw on blur would swap out the button the reader is clicking
    const vis = h("div", { class: "mini-seg", role: "group", "aria-label": "Who sees it" }, h("span", { class: "mini-lab" }, "Who sees it"),
      VIS.map(([k, l]) => h("button", { type: "button", "aria-pressed": String(b.visibility === k), disabled: !mine || null,
        onclick: () => { b.visibility = k; F.render(); } }, l)));
    const changed = dirty();
    const f = filtersNow(), keptF = JSON.stringify(b.filters || {}) !== JSON.stringify(f);
    const facts = [b.id ? (mine ? "yours" : `${b.owner}'s, shared with you: Save makes your own copy`) : "not saved yet",
      b.updated_ms ? `saved ${when(b.updated_ms)} UTC` : null, `filters ${F.filtersText(f) || "none"}${b.id && keptF ? " (changed in the header)" : ""}`];
    return card("Notebook", null,
      h("div", { class: "nb-bar" }, picker(),
        h("div", { class: "nb-bar-buttons" },
          h("button", { type: "button", class: "btn sls ghost nb-btn", onclick: () => pick("") }, "New"),
          h("button", { type: "button", class: "btn sls ghost nb-btn", onclick: duplicate }, "Duplicate"),
          h("button", { type: "button", class: "btn sls ghost nb-btn", onclick: remove, disabled: (b.id && !mine) || null }, "Delete"),
          h("button", { type: "button", class: "btn sls ghost nb-btn", onclick: () => { harvest(); nb.report = true; F.render(); window.scrollTo(0, 0); } }, "Report"),
          h("button", { type: "button", class: "btn sls nb-btn " + (changed ? "cta" : "default"), onclick: save }, mine ? "Save" : "Save a copy"))),
      h("div", { class: "nb-bar" }, name, vis),
      h("p", { class: "small" + (nb.statusBad ? " danger" : ""), "aria-live": "polite" },
        nb.status || [changed ? "Unsaved changes" : null, ...facts].filter(Boolean).join(" · ")));
  }

  function adders() {
    const add = (kind, label) => h("button", { type: "button", class: "qb-add", onclick: () => addCell(kind) }, label);
    return h("div", { class: "nb-adders" }, h("span", { class: "lab" }, "Add"), add("text", "+ Note"), add("sql", "+ SQL"), add("chart", "+ Chart"));
  }

  function dock() {
    const area = h("textarea", { id: "nb-ask", class: "field sls nb-ask-input", rows: "1", maxlength: "500", "aria-label": "Ask Claude",
      placeholder: nb.book.cells.length ? "Ask a follow-up: now split that by platform…" : "Ask Claude a question about the game…" }, nb.draft);
    const grow = () => { area.style.height = "auto"; area.style.height = Math.min(160, area.scrollHeight + 2) + "px"; };
    area.addEventListener("input", () => { nb.draft = area.value; grow(); });
    area.addEventListener("keydown", (e) => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); ask(area.value); } });
    setTimeout(grow, 0);
    const send = h("button", { type: "button", class: "btn sls cta nb-send", disabled: nb.asking || null, onclick: () => ask(area.value) }, nb.asking ? "Asking…" : "Ask");
    const left = nb.asksLeft !== null ? `${fmt(nb.asksLeft)} questions left today · ` : "";
    return h("div", { id: "nb-dock", class: "nb-dock panel sls" },
      h("div", { class: "nb-dock-row" }, area, send),
      h("p", { class: "small" }, `${left}Claude reads this notebook's cells and their totals, never a player id. Enter sends, Shift + Enter is a new line.`));
  }

  function emptyState() {
    return h("div", { class: "card panel sl nb-empty" },
      h("span", { class: "lab" }, "An empty notebook"),
      h("p", { class: "body" }, "A notebook is notes, queries and charts in order, and a chat with Claude: ask below and the answer comes back as cells. Claude reads the cells above a question, so a follow-up builds on them."),
      h("div", { class: "chips" }, SUGGEST.map((q) => h("button", { type: "button", class: "chip sls", onclick: () => ask(q) }, q))));
  }

  function editPage() {
    const cells = nb.book.cells;
    const parts = [toolbar()];
    if (!cells.length && !nb.asking) parts.push(emptyState());
    const list = h("div", { class: "nb-cells" }, cells.map((c, i) => cellView(c, i, cells.length)));
    if (nb.asking) list.append(h("div", { class: "nb-cell nb-pending by-claude" }, h("div", { class: "nb-head" }, h("span", { class: "lab nb-kind" }, h("span", { class: "nb-by" }, "Claude"))),
      h("div", { class: "card panel sl claude" }, h("p", { class: "small" }, "Reading the notebook and the aggregates…"))));
    parts.push(list, adders(), dock());
    return parts;
  }

  // ------------------------------------------------------------------ Report: outputs only, to print or copy

  function reportMeta() {
    const b = nb.book, f = filtersNow();
    const era = f.era && F.eraById(f.era);
    return { title: (b.name || "").trim() || "Untitled notebook",
             date: b.updated_ms ? `${when(b.updated_ms)} UTC` : `${when(Date.now())} UTC (not saved)`,
             filters: [f.build ? `${f.build} builds` : null, era ? `${era.name} (${era.from} to ${era.to})` : f.days ? `last ${f.days} days` : null,
                       f.play && f.play !== "all" ? ({ solo_vs_ai: "solo vs AI", solo: "solo", multi: "multiplayer" })[f.play] : "every play type"].filter(Boolean).join(" · "),
             owner: b.owner || state.me };
  }

  function mdTable(o) {
    const cols = o.columns || [], rows = (o.rows || []).slice(0, 50);
    const esc = (v) => String(v === null || v === undefined ? "–" : v).replace(/\|/g, "\\|").replace(/\n/g, " ");
    return [`| ${cols.map(esc).join(" | ")} |`, `| ${cols.map(() => "---").join(" | ")} |`, ...rows.map((r) => `| ${r.map(esc).join(" | ")} |`),
      (o.rows || []).length > 50 ? `\n_${o.rows.length - 50} more rows in the notebook._` : ""].filter((x) => x !== "").join("\n");
  }
  function markdownOf() {
    const m = reportMeta(), out = [`# ${m.title}`, `_${m.date} · ${m.owner} · ${m.filters}_`];
    for (const c of nb.book.cells) {
      if (c.kind === "text") out.push(c.source.trim());
      else if (c.kind === "ask" && !(c.output && c.output.error)) out.push(`**Q:** ${c.source}`);   // a question with no answer is no output
      else if (c.kind === "sql") {
        const o = c.output || {};
        out.push(o.error ? `_Query not run: ${o.error}_` : (o.columns || []).length ? mdTable(o) : "_A query with no result yet._");
        out.push("```sql\n" + c.source.trim() + "\n```");
      } else if (c.kind === "chart") {
        const ch = c.chart || {};
        out.push(ch.from === "series"
          ? `_Chart: ${(F.MEASURES[ch.measure] || [ch.measure])[0]}${ch.by && ch.by !== "none" ? ` by ${ch.by.replace(/_/g, " ")}` : ""}, ${({ day: "per day", week: "per week", none: "in total" })[ch.over] || ""} (${ch.form || "bars"})._`
          : `_Chart of the result above: ${ch.y} by ${ch.x} (${ch.form || "bars"})._`);
      }
    }
    return out.filter(Boolean).join("\n\n") + "\n";
  }
  async function copyMarkdown() {
    const text = markdownOf();
    try { await navigator.clipboard.writeText(text); nb.copied = null; say("Copied as Markdown."); }
    catch (e) { nb.copied = text; say("This browser would not copy it: the Markdown is below, select it and copy."); }
    F.render();
  }

  function reportPage() {
    const m = reportMeta();
    const head = h("header", { class: "nb-report-head" },
      h("span", { class: "lab" }, "Froglet Analytics · Notebook report"),
      h("h2", { class: "h1 nb-report-title" }, m.title),
      h("p", { class: "small num" }, `${m.date} · by ${m.owner}`),
      h("p", { class: "small" }, `Filters: ${m.filters}`));
    const bar = h("div", { class: "row nb-report-bar" },
      h("button", { type: "button", class: "btn sls cta", onclick: () => window.print() }, "Print / Save as PDF"),
      h("button", { type: "button", class: "btn sls default", onclick: copyMarkdown }, "Copy as Markdown"),
      h("button", { type: "button", class: "btn sls ghost", onclick: () => { nb.report = false; nb.copied = null; say(""); F.render(); } }, "Back to the notebook"),
      nb.status ? h("span", { class: "small" + (nb.statusBad ? " danger" : ""), "aria-live": "polite" }, nb.status) : null);
    const copied = nb.copied ? h("textarea", { class: "field code-field sls nb-copy", rows: "10", readonly: true, "aria-label": "The report as Markdown",
      onfocus: (e) => e.target.select() }, nb.copied) : null;
    const body = nb.book.cells.filter((c) => !(c.kind === "ask" && c.output && c.output.error)).map((c) => {
      if (c.kind === "text") return h("div", { class: "nb-r nb-r-text" }, markdown(c.source));
      if (c.kind === "ask") return h("div", { class: "nb-r nb-r-ask" }, h("span", { class: "lab" }, "Question"), h("p", { class: "h3" }, c.source));
      if (c.kind === "sql") {
        const o = c.output || {};
        return h("div", { class: "nb-r nb-r-sql" }, o.error ? h("p", { class: "small" }, `Query not run: ${o.error}`) : resultTable(o),
          h("details", { class: "nb-r-query" }, h("summary", { class: "small" }, "The query"), h("pre", { class: "code" }, c.source)));
      }
      return h("div", { class: "nb-r nb-r-chart" }, chartOf(c, true));
    });
    return [bar, copied, h("article", { class: "nb-report panel sl" }, head, body.length ? body : h("p", { class: "small" }, "This notebook has no cells yet."))];
  }

  // ------------------------------------------------------------------ the page

  const META = { title: "Notebook", build: true, days: F.DAYS, play: true, era: true };

  async function page() {
    F.setUpdated(null);
    if (nb.list === null) await loadList();
    if (!nb.book) {
      const want = store.get(OPEN_STORE, "") || "";
      const known = (nb.list || []).find((n) => n.id === want)
        || (F.SNAPSHOT ? (nb.list || []).find((n) => n.owner === state.me) || (nb.list || [])[0] : null);
      try { await open(known ? known.id : ""); } catch (e) { adopt(blank()); say(e.message, true); }
      if (known) return F.render(), [];      // its filters are now the header's: draw again with them
    }
    harvest();
    nb.views.clear();
    document.body.classList.toggle("nb-printing", nb.report);
    return nb.report ? reportPage() : editPage();
  }

  F.register("notebook", META, page);
  // Leaving the page leaves Report mode, so the print stylesheet never reaches another page.
  window.addEventListener("hashchange", () => { if (location.hash !== "#notebook") { nb.report = false; document.body.classList.remove("nb-printing"); } });
})();
