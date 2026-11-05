/* The Froglet Analytics console (Frogma Rev 50 B). Reads the analytics server's read API; holds no data of its own.
   API base: ?api=<url> in the address (for GitHub Pages), else the page's own origin (served by the server).
   The read key is kept in this browser's localStorage only. Every value from the server reaches the page through
   textContent (h() below), never as HTML. */
(function () {
  "use strict";

  const params = new URLSearchParams(location.search);
  const API = (params.get("api") || location.origin).replace(/\/$/, "");
  const KEY_STORE = "froglet-analytics-key";
  const PAGES = {
    overview: { title: "Overview", build: true, days: [7, 30, 90, 365] },
    retention: { title: "Retention", build: true, days: [7, 30, 90, 365] },
    matches: { title: "Matches", build: true, days: [7, 30, 90, 365] },
    health: { title: "Data health", build: false, days: [14, 30, 90] },
    ask: { title: "Ask Claude", build: false, days: null },
    erasure: { title: "Erasure", build: false, days: null },
  };
  const BUILDS = [["release", "Release"], ["editor", "Editor"], ["port", "Port"], ["development", "Development"]];
  const PLAY_TYPES = {
    solo_vs_ai: "Solo vs AI", solo_alone: "Solo", coop: "Co-op", adversarial: "Versus", mixed: "Mixed teams",
    multi_human_unknown: "Multi-human (teams not recorded)",
  };
  const SUGGESTIONS = [
    "Which play type brings players back for a rematch?",
    "Which game modes do players finish least often?",
    "How many players came back the day after their first match, by play type?",
  ];

  const state = { page: "overview", build: "release", days: 90, healthDays: 30, ask: null, asking: false };

  // ------------------------------------------------------------------ tiny DOM helpers

  function h(tag, attrs, ...children) {
    const el = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs || {})) {
      if (v === null || v === undefined || v === false) continue;
      if (k === "class") el.className = v;
      else if (k.startsWith("on")) el.addEventListener(k.slice(2), v);
      else el.setAttribute(k, v === true ? "" : v);
    }
    for (const child of children.flat()) {
      if (child === null || child === undefined || child === false) continue;
      el.append(child instanceof Node ? child : document.createTextNode(String(child)));
    }
    return el;
  }
  const svgNS = "http://www.w3.org/2000/svg";
  function s(tag, attrs, ...children) {
    const el = document.createElementNS(svgNS, tag);
    for (const [k, v] of Object.entries(attrs || {})) if (v !== null && v !== undefined) el.setAttribute(k, v);
    for (const child of children.flat()) if (child !== null && child !== undefined)
      el.append(child instanceof Node ? child : document.createTextNode(String(child)));
    return el;
  }
  const $ = (id) => document.getElementById(id);
  const fmt = (n) => (n === null || n === undefined ? "–" : Number(n).toLocaleString("en-GB"));
  const pct = (n) => (n === null || n === undefined ? "–" : `${n}%`);
  const ratio = (n) => (n === null || n === undefined ? "–" : Number(n).toFixed(2));
  const playType = (t) => PLAY_TYPES[t] || t || "Unknown";
  const ago = (ms) => {
    if (!ms) return "never";
    const s = Math.round((Date.now() - ms) / 1000);
    if (s < 90) return `${s} s ago`;
    if (s < 5400) return `${Math.round(s / 60)} min ago`;
    if (s < 172800) return `${Math.round(s / 3600)} h ago`;
    return `${Math.round(s / 86400)} days ago`;
  };

  // ------------------------------------------------------------------ the server

  function key() { try { return localStorage.getItem(KEY_STORE) || ""; } catch (e) { return ""; } }
  function setKey(k) { try { k ? localStorage.setItem(KEY_STORE, k) : localStorage.removeItem(KEY_STORE); } catch (e) {} }

  async function api(path, body) {
    const res = await fetch(API + path, {
      method: body ? "POST" : "GET",
      headers: Object.assign({ Authorization: "Bearer " + key() }, body ? { "Content-Type": "application/json" } : {}),
      body: body ? JSON.stringify(body) : undefined,
    });
    if (res.status === 401) { showGate("That key was not accepted."); throw new Error("unauthorised"); }
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || `The server answered ${res.status}.`);
    return data;
  }

  function showGate(message) {
    $("gate").hidden = false;
    $("gate-error").hidden = !message;
    $("gate-error").textContent = message || "";
    $("gate-key").focus();
  }

  // ------------------------------------------------------------------ charts (one series, blue-68)

  function niceMax(v) {
    if (!v || v <= 0) return 1;
    const p = Math.pow(10, Math.floor(Math.log10(v)));
    for (const m of [1, 2, 2.5, 5, 10]) if (m * p >= v) return m * p;
    return 10 * p;
  }

  function withTip(wrap) {
    const tip = h("div", { class: "tip", hidden: true });
    wrap.append(tip);
    return (text, x, y) => {
      if (!text) { tip.hidden = true; return; }
      tip.textContent = text;
      tip.style.left = x + "px";
      tip.style.top = y + "px";
      tip.hidden = false;
    };
  }

  function barChart(points, { height = 200, label, width = 1080 } = {}) {
    const W = width, H = height, L = 44, B = 24, T = 8;
    const max = niceMax(Math.max(0, ...points.map((p) => p.value || 0)));
    const plotW = W - L, plotH = H - B - T;
    const step = points.length ? plotW / points.length : plotW;
    const svg = s("svg", { viewBox: `0 0 ${W} ${H}`, role: "img", "aria-label": label || "Bar chart" });
    for (const f of [0, 0.5, 1]) {
      const y = T + plotH * (1 - f);
      svg.append(s("line", { class: "grid", x1: L, x2: W, y1: y, y2: y }),
        s("text", { class: "axis", x: L - 8, y: y + 4, "text-anchor": "end" }, fmt(Math.round(max * f * 10) / 10)));
    }
    const wrap = h("div", { class: "chart" }, svg);
    const tip = withTip(wrap);
    points.forEach((p, i) => {
      const bh = max ? plotH * (p.value || 0) / max : 0;
      const x = L + i * step + 1, w = Math.max(1, step - 2);
      const bar = s("rect", { class: "mark", x, y: T + plotH - bh, width: w, height: Math.max(bh, p.value ? 1 : 0) });
      const hit = s("rect", { class: "hit", x: L + i * step, y: T, width: step, height: plotH });
      const show = () => {
        bar.classList.add("hot");
        const r = svg.getBoundingClientRect(), k = r.width / W;
        tip(p.tip || `${p.label}: ${fmt(p.value)}`, (x + w / 2) * k, (T + plotH - bh) * k);
      };
      const hide = () => { bar.classList.remove("hot"); tip(null); };
      hit.addEventListener("mouseenter", show); hit.addEventListener("mouseleave", hide);
      svg.append(bar, hit);
    });
    if (points.length) {
      svg.append(s("text", { class: "axis", x: L, y: H - 4 }, points[0].label),
        s("text", { class: "axis", x: W, y: H - 4, "text-anchor": "end" }, points[points.length - 1].label));
    }
    return wrap;
  }

  function lineChart(points, { height = 220, target, label, digits = 2, width = 520 } = {}) {
    const W = width, H = height, L = 44, R = 8, B = 24, T = 10;
    const top = niceMax(Math.max(target || 0, ...points.map((p) => p.value || 0)));
    const plotW = W - L - R, plotH = H - B - T;
    const xAt = (i) => L + (points.length > 1 ? plotW * i / (points.length - 1) : plotW / 2);
    const yAt = (v) => T + plotH * (1 - v / top);
    const svg = s("svg", { viewBox: `0 0 ${W} ${H}`, role: "img", "aria-label": label || "Line chart" });
    for (const f of [0, 0.5, 1]) {
      svg.append(s("line", { class: "grid", x1: L, x2: W - R, y1: yAt(top * f), y2: yAt(top * f) }),
        s("text", { class: "axis", x: L - 8, y: yAt(top * f) + 4, "text-anchor": "end" }, (top * f).toFixed(digits === 0 ? 0 : 1)));
    }
    if (target !== undefined) {
      svg.append(s("line", { class: "target", x1: L, x2: W - R, y1: yAt(target), y2: yAt(target) }),
        s("text", { class: "axis", x: W - R, y: yAt(target) - 6, "text-anchor": "end" }, `target ${target.toFixed(1)}`));
    }
    const wrap = h("div", { class: "chart" }, svg);
    const tip = withTip(wrap);
    if (points.length) {
      svg.append(s("polyline", { class: "line", points: points.map((p, i) => `${xAt(i)},${yAt(p.value || 0)}`).join(" ") }));
      const last = points[points.length - 1];
      svg.append(s("rect", { class: "mark", x: xAt(points.length - 1) - 4, y: yAt(last.value || 0) - 4, width: 8, height: 8 }));
      const cross = s("line", { class: "grid", x1: 0, x2: 0, y1: T, y2: T + plotH, visibility: "hidden" });
      svg.append(cross);
      const slot = points.length > 1 ? plotW / (points.length - 1) : plotW;
      points.forEach((p, i) => {
        const hit = s("rect", { class: "hit", x: xAt(i) - slot / 2, y: T, width: slot, height: plotH });
        hit.addEventListener("mouseenter", () => {
          cross.setAttribute("x1", xAt(i)); cross.setAttribute("x2", xAt(i)); cross.setAttribute("visibility", "visible");
          const k = svg.getBoundingClientRect().width / W;
          tip(p.tip || `${p.label}: ${(p.value ?? 0).toFixed(digits)}`, xAt(i) * k, yAt(p.value || 0) * k);
        });
        hit.addEventListener("mouseleave", () => { cross.setAttribute("visibility", "hidden"); tip(null); });
        svg.append(hit);
      });
      svg.append(s("text", { class: "axis", x: L, y: H - 4 }, points[0].label),
        s("text", { class: "axis", x: W - R, y: H - 4, "text-anchor": "end" }, last.label));
    }
    return wrap;
  }

  function meter(value) {
    const v = Math.max(0, Math.min(100, value || 0));
    return h("div", { class: "meter-track" }, h("div", { class: "meter-fill", style: `width:${v}%` }));
  }

  function table(columns, rows) {
    return h("div", { class: "table-wrap" }, h("table", {},
      h("thead", {}, h("tr", {}, columns.map((c) => h("th", { scope: "col" }, c)))),
      h("tbody", {}, rows)));
  }

  function card(title, note, ...body) {
    return h("section", { class: "card panel sl" },
      h("div", { class: "card-head" }, h("span", { class: "lab" }, title), note ? h("span", { class: "small" }, note) : null),
      ...body);
  }

  function empty(text) { return h("div", { class: "empty panel sl" }, h("p", { class: "small" }, text)); }

  // ------------------------------------------------------------------ pages

  function readStrip(digest) {
    if (!digest || !digest.notes || !digest.notes.length) {
      return h("div", { class: "read info sls" }, h("span", { class: "small" }, "No overnight read yet. It is written each night at 05:00 UTC."));
    }
    const problem = digest.notes.some((n) => n.severity === "problem");
    const notes = h("div", { class: "read-notes", hidden: true },
      digest.notes.map((n) => h("div", { class: "note " + n.severity }, h("span", { class: "bar" }),
        h("div", {}, h("p", { class: "body" }, n.title), h("p", { class: "small" }, n.body)))));
    const toggle = h("button", { type: "button", class: "link", "aria-expanded": "false" }, "Read all");
    toggle.addEventListener("click", () => {
      notes.hidden = !notes.hidden;
      toggle.textContent = notes.hidden ? "Read all" : "Hide";
      toggle.setAttribute("aria-expanded", String(!notes.hidden));
    });
    const author = digest.author === "claude" ? "Claude" : "Rules";
    return h("div", { class: "read sls " + (problem ? "problem" : "info") },
      h("div", { class: "read-line" },
        h("span", { class: "lab", style: "color:var(--fs-text)" }, `${author} · ${digest.notes.length} notes · ${digest.day}`),
        h("span", { class: "small", style: "color:var(--fs-text);flex:1;min-width:200px" }, digest.notes.map((n) => n.title).join(". ") + "."),
        toggle),
      notes);
  }

  function tiles(t, build) {
    const list = [
      ["Players", fmt(t.players), `last ${state.days} days`],
      ["Matches", fmt(t.matches), `last ${state.days} days`],
      ["Finished", pct(t.finished_pct), "of plays sent game_completed"],
      ["Rematch", pct(t.rematch_solo_vs_ai_pct), "solo vs AI, within 10 min"],
      ["Rows per play", ratio(t.rows_per_play), "should be 1.00", t.rows_per_play > 1.05],
      ["Multi-human", fmt(t.multi_human_matches), "matches with 2+ people"],
    ];
    return h("div", { class: "tiles" }, list.map(([label, value, note, bad]) =>
      h("div", { class: "tile panel sl" + (bad ? " bad" : "") },
        h("span", { class: "lab" }, label), h("span", { class: "value num" }, value), h("span", { class: "small" }, note))));
  }

  function playTypeRows(list) {
    return list.map((r) => h("tr", {},
      h("td", {}, playType(r.play_type)), h("td", {}, fmt(r.matches)),
      h("td", { class: r.completed ? "" : "soft" }, r.completed ? pct(r.rematch_pct) : "–"),
      h("td", { class: "meter" }, meter(r.rematch_pct)),
      h("td", { class: r.d1_pct === null ? "soft" : "" }, pct(r.d1_pct)),
      h("td", { class: r.d7_pct === null ? "soft" : "" }, pct(r.d7_pct))));
  }

  async function overview() {
    const o = await api(`/v1/overview?build=${state.build}&days=${state.days}`);
    setUpdated(o.built_ms);
    if (o.empty) return [empty("Nothing has been received yet. Point a build at this server.")];
    return [
      readStrip(o.digest),
      tiles(o.tiles, state.build),
      h("div", { class: "grid2" },
        card("Rematch and return by play type", null,
          o.rematch_by_play_type.length
            ? table(["Play type", "Matches", "Rematch", "", "D1", "D7"], playTypeRows(o.rematch_by_play_type))
            : h("p", { class: "small" }, "No matches in this range."),
          h("p", { class: "small" }, "D1 and D7 count only players whose first match is at least 7 days old.")),
        h("div", { class: "page" },
          card("Match health by build", `last ${state.days} days`,
            table(["Build", "Starts", "Finished", "", "Rows per play"], o.match_health_by_build.map((r) => h("tr", {},
              h("td", {}, r.build), h("td", {}, fmt(r.starts)),
              h("td", { class: r.starts >= 10 && r.finished_pct < 20 ? "bad" : "" }, pct(r.finished_pct)),
              h("td", { class: "meter" }, meter(r.finished_pct)),
              h("td", { class: r.rows_per_play > 1.05 ? "bad" : "" }, ratio(r.rows_per_play)))))),
          card("Events, last 24 hours", state.build,
            o.events_24h.length
              ? table(["Event", "Rows", "Flagged as duplicates"], o.events_24h.map((e) => h("tr", {},
                  h("td", {}, e.event), h("td", {}, fmt(e.rows)), h("td", { class: e.near_duplicates ? "bad" : "soft" }, fmt(e.near_duplicates)))))
              : h("p", { class: "small" }, "Nothing in the last day.")))),
    ];
  }

  async function retention() {
    const r = await api(`/v1/retention?build=${state.build}&days=${state.days}`);
    setUpdated(r.built_ms);
    if (r.empty) return [empty("Nothing has been received yet.")];
    return [
      card("By play type", "rematch: another match within 10 minutes of finishing one",
        r.by_play_type.length
          ? table(["Play type", "Matches", "Rematch", "", "D1", "D7"], playTypeRows(r.by_play_type))
          : h("p", { class: "small" }, "No matches in this range.")),
      card("First-play cohorts", "players grouped by the day of their first match; 7+ days old only",
        r.cohorts.length
          ? table(["First day", "Play type", "Players", "Back next day", "Back within 7 days"], r.cohorts.slice().reverse().map((c) => h("tr", {},
              h("td", {}, c.first_day), h("td", {}, playType(c.play_type)), h("td", {}, fmt(c.players)),
              h("td", {}, `${fmt(c.returned_d1)} (${pct(c.players ? Math.round(100 * c.returned_d1 / c.players) : null)})`),
              h("td", {}, `${fmt(c.returned_d7)} (${pct(c.players ? Math.round(100 * c.returned_d7 / c.players) : null)})`))))
          : h("p", { class: "small" }, "No cohort is 7 days old yet.")),
    ];
  }

  async function matches() {
    const m = await api(`/v1/matches?build=${state.build}&days=${state.days}`);
    setUpdated(m.built_ms);
    if (m.empty) return [empty("Nothing has been received yet.")];
    return [
      card("Matches per day", `${state.build}, last ${state.days} days · hover a bar`,
        m.per_day.some((d) => d.matches)
          ? barChart(m.per_day.map((d) => ({ label: d.day.slice(5), value: d.matches,
              tip: `${d.day}: ${fmt(d.matches)} matches, ${fmt(d.completed)} of ${fmt(d.plays)} plays finished` })), { label: "Matches per day", width: 1080, height: 240 })
          : h("p", { class: "small" }, "No matches in this range.")),
      h("div", { class: "grid2" },
        card("By mode", null, table(["Mode", "Plays", "Finished", "", "Avg flight"], m.by_mode.map((r) => h("tr", {},
          h("td", {}, r.game_mode || "Unknown"), h("td", {}, fmt(r.plays)), h("td", {}, pct(r.finished_pct)),
          h("td", { class: "meter" }, meter(r.finished_pct)), h("td", {}, r.avg_flight_s ? `${r.avg_flight_s} s` : "–"))))),
        card("By intensity", null, table(["Intensity", "Plays", "Finished", ""], m.by_intensity.map((r) => h("tr", {},
          h("td", {}, r.intensity ?? "–"), h("td", {}, fmt(r.plays)), h("td", {}, pct(r.finished_pct)),
          h("td", { class: "meter" }, meter(r.finished_pct))))))),
    ];
  }

  function statusRow(ok, label, value) {
    const icon = ok
      ? s("svg", { viewBox: "0 0 18 18", "aria-label": "healthy" }, s("path", { class: "ok", d: "M3 9.5l4 4 8-9" }))
      : s("svg", { viewBox: "0 0 18 18", "aria-label": "problem" }, s("path", { class: "bad", d: "M9 3v7" }), s("path", { class: "bad", d: "M9 13.5v1" }));
    return h("div", { class: "status" }, icon, h("span", { class: "small", style: "color:var(--fs-text)" }, label), h("span", { class: "small num" }, value));
  }

  async function health() {
    const d = await api(`/v1/health?days=${state.healthDays}`);
    setUpdated(d.built_ms);
    const day = 86400 * 1000;
    const rpp = (d.rows_per_play || []).filter((r) => r.build_kind === "release");
    const worst = rpp.length ? rpp[rpp.length - 1].rows_per_play : null;
    const rejected = (d.rejects || []).reduce((a, r) => a + r.count, 0);
    return [
      h("div", { class: "grid2" },
        card("Rows per play, release builds", "game_started rows over distinct (player, match): 1.0 is healthy",
          rpp.length
            ? lineChart(rpp.map((r) => ({ label: r.day.slice(5), value: r.rows_per_play,
                tip: `${r.day}: ${ratio(r.rows_per_play)} (${fmt(r.rows)} rows, ${fmt(r.plays)} plays)` })), { target: 1, label: "Rows per play by day", width: 520 })
            : h("p", { class: "small" }, "No release matches in this range.")),
        card("The server", null,
          statusRow(d.last_event_ms && Date.now() - d.last_event_ms < day, "Last event received", ago(d.last_event_ms)),
          statusRow(worst === null || worst <= 1.05, "Rows per play, latest day", ratio(worst)),
          statusRow(rejected === 0, `Rejected events, last ${state.healthDays} days`, fmt(rejected)),
          statusRow(d.backup && Date.now() - d.backup.last_ms < 2 * day, "Nightly backup", d.backup ? ago(d.backup.last_ms) : "never"),
          statusRow(d.rebuild && Date.now() - d.rebuild.last_ms < 15 * 60 * 1000, "Numbers rebuilt", d.rebuild ? ago(d.rebuild.last_ms) : "never"),
          statusRow(true, "Deletions done", fmt(d.erasures_done)),
          statusRow(true, "Editor share of matches", pct(d.editor_share_pct)))),
      h("div", { class: "grid2" },
        card("Flagged duplicates per day", "same player, event and properties within 50 ms",
          (d.near_duplicates || []).length
            ? barChart(d.near_duplicates.map((r) => ({ label: r.day.slice(5), value: r.near_duplicates,
                tip: `${r.day}: ${fmt(r.near_duplicates)} of ${fmt(r.rows)} rows` })), { height: 180, label: "Duplicates per day", width: 520 })
            : h("p", { class: "small" }, "None.")),
        card("Rejected events", "counted, not stored",
          (d.rejects || []).length
            ? table(["Reason", "Event", "Count"], d.rejects.map((r) => h("tr", {}, h("td", {}, r.reason.replace(/_/g, " ")),
                h("td", {}, r.event || "–"), h("td", { class: "bad" }, fmt(r.count)))))
            : h("p", { class: "small" }, "None. Every event matched the schema."))),
    ];
  }

  function askPage() {
    const input = h("input", { id: "ask-q", class: "field sls", type: "text", maxlength: "500",
      placeholder: "Ask in plain words, for example: which mode do players leave after?", value: state.askDraft || "" });
    const button = h("button", { class: "btn sls default", type: "submit", disabled: state.asking || null }, state.asking ? "Asking…" : "Ask");
    const form = h("form", { class: "row" }, h("label", { class: "lab", for: "ask-q", style: "position:absolute;left:-9999px" }, "Question"),
      h("div", { style: "flex:1;min-width:240px" }, input), button);
    form.addEventListener("submit", (e) => { e.preventDefault(); runAsk(input.value); });
    input.addEventListener("input", () => { state.askDraft = input.value; });
    const chips = h("div", { class: "chips" }, SUGGESTIONS.map((q) =>
      h("button", { type: "button", class: "chip sls", onclick: () => runAsk(q) }, q)));
    const out = [card("Ask Claude", "release builds unless you name another; 50 questions a day", form, chips)];
    const a = state.ask;
    if (a) {
      if (a.error) out.push(h("div", { class: "read problem sls" }, h("p", { class: "body" }, a.question), h("p", { class: "small" }, a.error)));
      else out.push(h("div", { class: "grid2" },
        card("Answer · Claude", null, h("h2", { class: "h2" }, a.question), h("p", { class: "body" }, a.answer),
          a.caveat ? h("p", { class: "small" }, a.caveat) : null),
        card("The query it ran", a.truncated ? "first 200 rows" : `${a.rows.length} rows`,
          h("pre", { class: "code" }, a.sql),
          a.columns && a.columns.length ? table(a.columns, a.rows.slice(0, 50).map((r) => h("tr", {}, r.map((c) => h("td", {}, c ?? "–"))))) : null,
          h("p", { class: "small" }, "It reads totals by day, mode and play type only. Raw events and player ids are not open to it."))));
    }
    return out;
  }

  async function runAsk(question) {
    question = (question || "").trim();
    if (!question || state.asking) return;
    state.asking = true; state.askDraft = question; render();
    try { state.ask = Object.assign({ question }, await api("/v1/ask", { question })); }
    catch (e) { state.ask = { question, error: e.message }; }
    state.asking = false; render();
  }

  function erasurePage() {
    const id = h("input", { id: "erase-id", class: "field sls", type: "text", autocomplete: "off", placeholder: "UGS player id" });
    const again = h("input", { id: "erase-again", class: "field sls", type: "text", autocomplete: "off", placeholder: "The same id again" });
    const button = h("button", { class: "btn sls danger-btn", type: "submit" }, "Delete this player's data");
    const result = h("p", { class: "small", "aria-live": "polite" });
    const form = h("form", { style: "display:flex;flex-direction:column;gap:12px;max-width:560px" },
      h("label", { class: "lab", for: "erase-id" }, "Player id"), id,
      h("label", { class: "lab", for: "erase-again" }, "Type it again to confirm"), again,
      h("div", { class: "row" }, button), result);
    form.addEventListener("submit", async (e) => {
      e.preventDefault();
      if (!id.value.trim() || id.value.trim() !== again.value.trim()) {
        result.className = "small danger"; result.textContent = "The two ids do not match."; return;
      }
      button.disabled = true;
      try {
        const out = await api("/v1/erase", { player_id: id.value.trim() });
        result.className = "small"; result.textContent = `Deleted ${fmt(out.rows_deleted)} rows. The numbers are rebuilt without them.`;
        id.value = ""; again.value = "";
      } catch (err) { result.className = "small danger"; result.textContent = err.message; }
      button.disabled = false;
    });
    return [card("Delete one player's data", "needs an admin key",
      h("p", { class: "small" }, "Removes every event and the person record for this id, at once and for good. A player's own delete request in the game does the same on its own; this page is for a request that arrives by email."),
      form)];
  }

  // ------------------------------------------------------------------ the frame

  function setUpdated(ms) { $("updated").textContent = ms ? `Numbers from ${new Date(ms).toISOString().slice(11, 16)} UTC` : ""; }

  function segment(container, options, current, pick) {
    container.replaceChildren(...options.map(([value, label]) =>
      h("button", { type: "button", "aria-pressed": String(value === current), onclick: () => pick(value) }, label)));
  }

  async function render() {
    const meta = PAGES[state.page];
    document.title = `${meta.title} · Froglet Analytics`;
    $("page-title").textContent = meta.title;
    $("eyebrow").textContent = meta.build ? `${state.build} builds · last ${state.days} days` : "Froglet Analytics";
    for (const a of document.querySelectorAll(".nav")) {
      if (a.dataset.page === state.page) a.setAttribute("aria-current", "page");
      else a.removeAttribute("aria-current");
    }
    $("seg-build").hidden = !meta.build;
    $("seg-days").hidden = !meta.days;
    segment($("seg-build"), BUILDS, state.build, (b) => { state.build = b; render(); });
    if (meta.days) {
      const current = state.page === "health" ? state.healthDays : state.days;
      segment($("seg-days"), meta.days.map((d) => [d, `${d} d`]), current, (d) => {
        if (state.page === "health") state.healthDays = d; else state.days = d; render();
      });
    }
    if (state.page === "ask" || state.page === "erasure") setUpdated(null);
    const page = $("page");
    if (!key()) { page.replaceChildren(); showGate(); return; }
    try {
      const parts = state.page === "ask" ? askPage() : state.page === "erasure" ? erasurePage()
        : await { overview, retention, matches, health }[state.page]();
      page.replaceChildren(...parts);
    } catch (e) {
      if (e.message !== "unauthorised") page.replaceChildren(empty(e.message === "Failed to fetch"
        ? `The server at ${API} could not be reached.` : e.message));
    }
  }

  function route() {
    const name = location.hash.replace("#", "");
    state.page = PAGES[name] ? name : "overview";
    render();
  }

  $("gate-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    setKey($("gate-key").value.trim());
    $("gate").hidden = true;
    $("gate-key").value = "";
    await whoAmI();
    render();
  });
  $("sign-out").addEventListener("click", () => { setKey(""); $("who-name").textContent = "Not signed in"; showGate(); });

  async function whoAmI() {
    if (!key()) return;
    try { const me = await api("/v1/me"); $("who-name").textContent = me.name + (me.admin ? " · admin" : ""); } catch (e) {}
  }

  window.addEventListener("hashchange", route);
  whoAmI();
  route();
})();
