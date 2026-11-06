/* The Froglet Analytics console (Frogma Rev 50 B). Reads the analytics server's read API; holds no data of its own.
   API base: ?api=<url> in the address (for GitHub Pages), else the page's own origin (served by the server).
   The read key is kept in this browser's localStorage only. Every value from the server reaches the page through
   textContent (h() below), never as HTML. */
(function () {
  "use strict";

  // Preview mode: a page built by make_preview.py carries a snapshot of the server's answers (invented sample data)
  // in <script id="fa-snapshot">, and reads it instead of a server. The live console never has one.
  const SNAP_EL = document.getElementById("fa-snapshot");
  const SNAPSHOT = SNAP_EL ? JSON.parse(SNAP_EL.textContent) : null;
  const params = new URLSearchParams(location.search);
  const API = (params.get("api") || location.origin).replace(/\/$/, "");
  const KEY_STORE = "froglet-analytics-key";
  const PAGES = {
    overview: { title: "Overview", build: true, days: [7, 30, 90, 365] },
    players: { title: "Players", build: true, days: [7, 30, 90, 365] },
    retention: { title: "Retention", build: true, days: [7, 30, 90, 365] },
    matches: { title: "Matches", build: true, days: [7, 30, 90, 365] },
    ftue: { title: "FTUE", build: true, days: [7, 30, 90, 365] },
    economy: { title: "Economy", build: true, days: [7, 30, 90, 365] },
    social: { title: "Social", build: true, days: [7, 30, 90, 365] },
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

  const state = { page: "overview", build: "release", days: 90, healthDays: 30, ask: null, asking: false,
                  profiles: false, person: null, peopleQuery: "" };
  let countryNames = null;
  try { countryNames = new Intl.DisplayNames(["en"], { type: "region" }); } catch (e) {}
  const country = (code) => {
    if (!code || code === "unknown") return "Unknown";
    try { return countryNames ? `${countryNames.of(code)} (${code})` : code; } catch (e) { return code; }
  };
  const when = (ms) => (ms ? new Date(ms).toISOString().slice(0, 16).replace("T", " ") : "–");
  const seconds = (s) => (s === null || s === undefined ? "–" : s >= 90 ? `${Math.round(s / 6) / 10} min` : `${Math.round(s)} s`);

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

  function key() { if (SNAPSHOT) return "preview"; try { return localStorage.getItem(KEY_STORE) || ""; } catch (e) { return ""; } }
  function setKey(k) { try { k ? localStorage.setItem(KEY_STORE, k) : localStorage.removeItem(KEY_STORE); } catch (e) {} }

  async function api(path, body) {
    if (SNAPSHOT) return snapshotAnswer(path, body);
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

  function snapshotAnswer(path, body) {
    if (path === "/v1/erase") throw new Error("Erasure is off in this preview: there is no server behind it.");
    if (path === "/v1/ask") {
      const answer = SNAPSHOT.ask[(body && body.question || "").trim()];
      if (!answer) throw new Error("This preview has answers for the three questions below only. The live console sends any question to Claude.");
      return JSON.parse(JSON.stringify(answer));
    }
    if (path.startsWith("/v1/people")) {                 // the snapshot holds the whole list; search it here
      const query = decodeURIComponent((path.match(/[?&]q=([^&]*)/) || [])[1] || "").toLowerCase();
      const list = SNAPSHOT.get[path.replace(/&q=[^&]*/, "")];
      if (!list) throw new Error("This preview has no snapshot for that view.");
      const people = list.people.filter((p) => !query || p.distinct_id.toLowerCase().includes(query)
        || (p.display_name || "").toLowerCase().includes(query));
      return JSON.parse(JSON.stringify(Object.assign({}, list, { people, total: people.length })));
    }
    const page = SNAPSHOT.get[path];
    if (!page) throw new Error("This preview has no snapshot for that view.");
    return JSON.parse(JSON.stringify(page));
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

  // `series` (0-3) marks a row as one of up to four categories: it takes the theme's domain rim in that slot
  // (Jade, Ruby, Gold, Blue; themes.css --series-1..4). Without it the meter is plain data (the set's Blue).
  function meter(value, series) {
    const v = Math.max(0, Math.min(100, value || 0));
    const fill = series === undefined ? "" : `;background:var(--series-${(series % 4) + 1})`;
    return h("div", { class: "meter-track" }, h("div", { class: "meter-fill", style: `width:${v}%${fill}` }));
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
    return list.map((r, i) => h("tr", {},
      h("td", {}, playType(r.play_type)), h("td", {}, fmt(r.matches)),
      h("td", { class: r.completed ? "" : "soft" }, r.completed ? pct(r.rematch_pct) : "–"),
      h("td", { class: "meter" }, meter(r.rematch_pct, i)),
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
              h("td", { class: "meter" }, meter(r.finished_pct, Math.max(0, BUILDS.findIndex(([b]) => b === r.build)))),
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
        card("By mode", null, table(["Mode", "Plays", "Finished", "", "Won", "Avg flight"], m.by_mode.map((r) => h("tr", {},
          h("td", {}, r.game_mode || "Unknown"), h("td", {}, fmt(r.plays)), h("td", {}, pct(r.finished_pct)),
          h("td", { class: "meter" }, meter(r.finished_pct)), h("td", {}, pct(r.win_pct)),
          h("td", {}, r.avg_flight_s ? `${r.avg_flight_s} s` : "–"))))),
        card("By intensity", null, table(["Intensity", "Plays", "Finished", ""], m.by_intensity.map((r) => h("tr", {},
          h("td", {}, r.intensity ?? "–"), h("td", {}, fmt(r.plays)), h("td", {}, pct(r.finished_pct)),
          h("td", { class: "meter" }, meter(r.finished_pct))))))),
      h("div", { class: "grid2" },
        card("By vessel", null, table(["Vessel", "Plays", "Finished", "Won", "Avg flight"], (m.by_vessel || []).map((r) => h("tr", {},
          h("td", {}, r.vessel_class || "Unknown"), h("td", {}, fmt(r.plays)), h("td", {}, pct(r.finished_pct)),
          h("td", {}, pct(r.win_pct)), h("td", {}, r.avg_flight_s ? `${r.avg_flight_s} s` : "–"))))),
        card("What players say about modes", "favourites, repeated fails, shares",
          (m.signals || []).length
            ? table(["Mode", "Favourited", "Unfavourited", "Repeated fails", "Shared"], m.signals.map((r) => h("tr", {},
                h("td", {}, r.game_mode), h("td", {}, fmt(r.favorited)), h("td", { class: "soft" }, fmt(r.unfavorited)),
                h("td", { class: r.fails ? "bad" : "soft" }, fmt(r.fails)), h("td", {}, fmt(r.shares)))))
            : h("p", { class: "small" }, "No favourites, fails or shares in this range."),
          m.pause ? h("p", { class: "small" }, `Paused or away from the controls: ${seconds(m.pause.avg_pause_s)} a match on average, ` +
            `${pct(m.pause.paused_over_30s_pct)} of matches over 30 s. Average match: ${seconds(m.pause.avg_duration_s)}.`) : null)),
    ];
  }

  function bars(rows, labelKey, valueKey, fmtLabel) {
    const top = Math.max(1, ...rows.map((r) => r[valueKey] || 0));
    return table(["", "Players", ""], rows.map((r) => h("tr", {}, h("td", {}, fmtLabel ? fmtLabel(r[labelKey]) : r[labelKey]),
      h("td", {}, fmt(r[valueKey])), h("td", { class: "meter" }, meter(100 * (r[valueKey] || 0) / top)))));
  }

  async function players() {
    if (state.person) return profile(state.person);
    const a = await api(`/v1/audience?build=${state.build}&days=${state.days}`);
    if (a.empty) return [empty("Nothing has been received yet.")];
    const first = a.funnel.length ? a.funnel[0].players : 0;
    const parts = [
      card("Active players per day", `${state.build}, last ${state.days} days · hover a bar`,
        barChart(a.per_day.map((d) => ({ label: d.day.slice(5), value: d.players,
          tip: `${d.day}: ${fmt(d.players)} players, ${fmt(d.new_players)} new` })), { label: "Active players per day", height: 200 })),
      h("div", { class: "grid2" },
        card("Where they play from", "country from the connection, the address itself is not kept",
          a.countries.length ? bars(a.countries, "country", "players", country) : h("p", { class: "small" }, "No countries yet."),
          h("p", { class: "small" }, "IP Geolocation by ", h("a", { href: "https://db-ip.com", target: "_blank", rel: "noopener", style: "color:inherit" }, "DB-IP"))),
        card("What they play on", null,
          a.platforms.length ? bars(a.platforms, "platform", "players") : h("p", { class: "small" }, "No platforms yet."))),
      h("div", { class: "grid2" },
        card("First-day funnel", "players whose first event was in this range, and how far they have got",
          a.funnel.length ? table(["Step", "Players", "Of new", ""], a.funnel.map((f) => h("tr", {}, h("td", {}, f.step),
            h("td", {}, fmt(f.players)), h("td", {}, pct(first ? Math.round(1000 * f.players / first) / 10 : null)),
            h("td", { class: "meter" }, meter(first ? 100 * f.players / first : 0))))) : h("p", { class: "small" }, "No new players in this range.")),
        card("Launch to menu", "seconds from opening the game to the main menu",
          a.launches.length ? table(["Platform", "Launches", "Typical", "Slowest 10%"], a.launches.map((l) => h("tr", {},
            h("td", {}, l.platform), h("td", {}, fmt(l.launches)), h("td", {}, seconds(l.p50_seconds)),
            h("td", { class: l.p90_seconds > 30 ? "bad" : "" }, seconds(l.p90_seconds))))) : h("p", { class: "small" }, "No launches yet."),
          a.session_ends.length ? h("p", { class: "small" }, "How sessions ended: " +
            a.session_ends.map((e) => `${e.reason.replace(/_/g, " ")} ${fmt(e.count)}`).join(", ") + ".") : null)),
    ];
    parts.push(await peopleCard());
    return parts;
  }

  async function peopleCard() {
    if (!state.profiles) return card("Every player", null, h("p", { class: "small" },
      "Player profiles (ids, display names, countries, everything they did) open with an admin key or a key named in the server's profile_readers."));
    const list = await api(`/v1/people?build=${state.build}&days=${state.days}&q=${encodeURIComponent(state.peopleQuery)}`);
    const search = h("input", { id: "people-q", class: "field sls", type: "search", value: state.peopleQuery,
      placeholder: "Search by display name or player id" });
    const form = h("form", { class: "row" }, h("label", { class: "lab", for: "people-q", style: "position:absolute;left:-9999px" }, "Search"),
      h("div", { style: "flex:1;min-width:220px" }, search), h("button", { class: "btn sls default", type: "submit" }, "Search"));
    form.addEventListener("submit", (e) => { e.preventDefault(); state.peopleQuery = search.value.trim(); render(); });
    const open = (id) => { state.person = id; render(); window.scrollTo(0, 0); };
    return card("Every player", `${fmt(list.total)} seen in the last ${state.days} days, newest first`, form,
      list.people.length ? table(["Player", "Country", "Platform", "First seen", "Last seen", "Days", "Matches", "Finished", "Crystals"],
        list.people.map((p) => h("tr", {},
          h("td", {}, h("button", { type: "button", class: "link", style: "color:var(--fs-text);font-size:14px", onclick: () => open(p.distinct_id) },
            p.display_name || "(no name)"), h("div", { class: "small", style: "font-size:12px" }, p.distinct_id)),
          h("td", {}, p.countries.map(country).join(", ") || "–"), h("td", {}, p.platforms.join(", ") || "–"),
          h("td", {}, when(p.first_ms).slice(0, 10)), h("td", {}, when(p.last_ms)), h("td", {}, fmt(p.days_active)),
          h("td", {}, fmt(p.matches)), h("td", {}, fmt(p.finished)), h("td", {}, fmt(p.crystal_balance))))) : h("p", { class: "small" }, "Nobody matches."));
  }

  function kv(pairs) {
    return h("div", { class: "kv" }, pairs.filter((p) => p[1] !== null && p[1] !== undefined && p[1] !== "").map(([k, v]) =>
      h("div", { class: "kv-row" }, h("span", { class: "lab" }, k), h("span", { class: "small", style: "color:var(--fs-text)" }, v))));
  }
  const listOf = (obj, f) => Object.entries(obj || {}).map(([k, n]) => `${f ? f(k) : k} (${fmt(n)})`).join(", ") || "–";

  async function profile(id) {
    const p = await api(`/v1/person?id=${encodeURIComponent(id)}`);
    const back = h("button", { type: "button", class: "btn sls default", onclick: () => { state.person = null; render(); } }, "All players");
    const m = p.matches, e = p.economy, w = p.where, act = p.activity;
    const person = Object.entries(p.person || {}).filter(([k]) => k !== "display_name");
    return [
      h("div", { class: "row" }, back),
      h("section", { class: "card panel sl" },
        h("div", { class: "card-head" }, h("span", { class: "h2" }, p.display_name || "(no display name)"),
          h("span", { class: "small" }, `Player id ${p.player_id}`)),
        h("div", { class: "tiles" }, [
          ["Country", country(w.latest_country)], ["First seen", when(p.first_ms).slice(0, 10)], ["Last seen", when(p.last_ms)],
          ["Days active", fmt(act.days_active)], ["Matches", `${fmt(m.total)} (${fmt(m.finished)} finished)`], ["Crystals", fmt(e.balance)],
        ].map(([k, v]) => h("div", { class: "tile panel sl" }, h("span", { class: "lab" }, k), h("span", { class: "body num" }, v))))),
      h("div", { class: "grid2" },
        card("Where and on what", null, kv([
          ["Countries", listOf(w.countries, country)], ["Platforms", listOf(w.platforms)], ["App versions", listOf(w.app_versions)],
          ["Builds", listOf(w.builds)], ["Devices (installs)", fmt(Object.keys(w.installs || {}).length)],
          ["Launch to menu, typical", seconds(act.launch_to_menu_p50)], ["Sessions ended", listOf(act.session_ends)]])),
        card("Profile the game sent", "the latest person properties", person.length
          ? kv(person.map(([k, v]) => [k.replace(/_/g, " "), typeof v === "object" ? JSON.stringify(v) : String(v)]))
          : h("p", { class: "small" }, "None yet."))),
      card("Activity per day", "events and matches · hover a bar", act.per_day.length
        ? barChart(act.per_day.map((d) => ({ label: d.day.slice(5), value: d.events, tip: `${d.day}: ${fmt(d.events)} events, ${fmt(d.matches)} matches` })),
            { label: "Events per day", height: 160 }) : h("p", { class: "small" }, "No activity.")),
      h("div", { class: "grid2" },
        card("How they play", null, kv([["Modes", listOf(m.by_mode)], ["Vessels", listOf(m.by_vessel)],
          ["Play types", listOf(m.by_play_type, playType)], ["Matches won", fmt(m.won)], ["Time at the controls", seconds(m.flight_seconds)]])),
        card("Crystals", `${fmt(e.earned)} earned, ${fmt(e.spent)} spent`, e.recent.length
          ? table(["When", "What", "Amount", "Source", "Balance"], e.recent.slice(0, 12).map((x) => h("tr", {}, h("td", {}, when(x.ts_ms)),
              h("td", {}, x.event.replace(/_/g, " ")), h("td", {}, fmt(x.amount)), h("td", {}, x.source || "–"), h("td", {}, fmt(x.balance)))))
          : h("p", { class: "small" }, "No crystal activity."),
          e.unlocks.length ? h("p", { class: "small" }, "Unlocked: " + e.unlocks.map((u) => `${u.name} (${u.kind})`).join(", ") + ".") : null)),
      card("Matches", `latest ${Math.min(m.recent.length, 100)} of ${fmt(m.total)}`, m.recent.length
        ? table(["When", "Mode", "Int.", "Vessel", "Play type", "Humans / AI", "Finished", "Won", "Flight", "Rematched"],
            m.recent.map((x) => h("tr", {}, h("td", {}, when(x.ts_ms)), h("td", {}, x.game_mode || "–"), h("td", {}, x.intensity ?? "–"),
              h("td", {}, x.vessel_class || "–"), h("td", {}, playType(x.play_type)), h("td", {}, `${x.humans ?? "–"} / ${x.ai ?? "–"}`),
              h("td", { class: x.finished ? "" : "bad" }, x.finished ? "yes" : "no"),
              h("td", {}, x.won === null || x.won === undefined ? "–" : String(x.won) === "true" ? "yes" : "no"),
              h("td", {}, seconds(x.flight_time_seconds)), h("td", {}, x.rematched ? "yes" : "–"))))
        : h("p", { class: "small" }, "No matches.")),
      h("div", { class: "grid2" },
        card("FTUE", `${p.ftue.length} steps`, p.ftue.length ? table(["When", "Phase", "Step", "After launch"], p.ftue.map((f) => h("tr", {},
          h("td", {}, when(f.ts_ms)), h("td", {}, f.phase_index ?? "–"), h("td", {}, f.step), h("td", {}, seconds(f.seconds_since_launch)))))
          : h("p", { class: "small" }, "No FTUE steps recorded.")),
        card("Friends, parties, settings", null, kv([
          ...Object.entries(p.social).map(([k, v]) => [k.replace(/_/g, " "), `${fmt(v.count)}${v.with.length ? ": " + v.with.slice(0, 6).join(", ") : ""}`]),
          ...Object.entries(p.settings).map(([k, v]) => [`setting ${k.replace(/_/g, " ")}`, `${v.value} (${when(v.ts_ms).slice(0, 10)})`])]))),
      card("Event timeline", `latest ${p.recent_events.length} of ${fmt(act.events)} events · ${fmt(act.flagged_duplicates)} flagged as duplicates`,
        table(["When", "Event", "Build", "Platform", "Country", "Detail"], p.recent_events.map((x) => h("tr", {}, h("td", {}, when(x.ts_ms)),
          h("td", {}, x.event), h("td", {}, x.build), h("td", {}, x.platform || "–"), h("td", {}, x.country || "–"),
          h("td", { class: "soft" }, x.detail || ""))))),
      card("This player's data", null, h("p", { class: "small" },
        "Everything above is kept only on our server and is never sent to Claude. To delete it, use Erasure with this player id.")),
    ];
  }

  async function ftuePage() {
    const f = await api(`/v1/ftue?build=${state.build}&days=${state.days}`);
    if (f.empty) return [empty("Nothing has been received yet.")];
    return [
      h("div", { class: "tiles" }, [["New players", fmt(f.new_players)], ["Finished the FTUE", fmt(f.finished_ftue)],
        ["Finished", pct(f.finished_pct)]].map(([k, v]) => h("div", { class: "tile panel sl" }, h("span", { class: "lab" }, k), h("span", { class: "value num" }, v)))),
      card("Each step of the quest graph", "players who started in this range and completed the step, in the order players reach them",
        f.steps.length ? table(["Phase", "Step", "Players", "Of new", "", "After launch"], f.steps.map((s) => h("tr", {},
          h("td", {}, s.phase_index ?? "–"), h("td", {}, s.step_name), h("td", {}, fmt(s.players)), h("td", {}, pct(s.pct_of_new)),
          h("td", { class: "meter" }, meter(s.pct_of_new)), h("td", {}, seconds(s.avg_seconds)))))
          : h("p", { class: "small" }, "No FTUE steps in this range.")),
    ];
  }

  async function economyPage() {
    const e = await api(`/v1/economy?build=${state.build}&days=${state.days}`);
    if (e.empty) return [empty("Nothing has been received yet.")];
    const t = (k) => (e.totals[k] || {}).amount || 0;
    return [
      h("div", { class: "tiles" }, [["Earned", fmt(t("earned"))], ["Spent", fmt(t("spent") + t("unlock"))],
        ["Spends blocked", fmt((e.totals.blocked || {}).events || 0)], ["Paid twice", fmt(e.double_payouts.count), e.double_payouts.count > 0],
        ["Unlocks", fmt(e.unlocks.reduce((a, u) => a + u.count, 0))], ["Earned per spent", t("spent") + t("unlock") ? (t("earned") / (t("spent") + t("unlock"))).toFixed(2) : "–"]]
        .map(([k, v, bad]) => h("div", { class: "tile panel sl" + (bad ? " bad" : "") }, h("span", { class: "lab" }, k), h("span", { class: "value num" }, v)))),
      card("Crystals earned per day", "hover a bar for earned and spent",
        barChart(e.per_day.map((d) => ({ label: d.day.slice(5), value: d.earned, tip: `${d.day}: ${fmt(d.earned)} earned, ${fmt(d.spent)} spent` })),
          { label: "Crystals earned per day", height: 180 })),
      h("div", { class: "grid2" },
        card("Where crystals come from and go", null, e.sources.length ? table(["", "Source", "Times", "Crystals"], e.sources.map((r) => h("tr", {},
          h("td", {}, r.kind), h("td", {}, r.source), h("td", {}, fmt(r.events)), h("td", {}, fmt(r.amount))))) : h("p", { class: "small" }, "None.")),
        card("Balances now", "players active in the last 30 days, by their latest balance",
          e.balances.length ? bars(e.balances, "bucket", "players") : h("p", { class: "small" }, "No balances yet."))),
      h("div", { class: "grid2" },
        card("Spends blocked", "a player tried to buy without enough crystals", e.blocked.length
          ? table(["Item", "Times", "Short by"], e.blocked.map((r) => h("tr", {}, h("td", {}, r.item), h("td", {}, fmt(r.events)), h("td", {}, fmt(r.short_by)))))
          : h("p", { class: "small" }, "None.")),
        card("Unlocks", null, e.unlocks.length ? table(["Kind", "What", "Times"], e.unlocks.map((r) => h("tr", {},
          h("td", {}, r.kind), h("td", {}, r.name), h("td", {}, fmt(r.count))))) : h("p", { class: "small" }, "None."))),
    ];
  }

  async function socialPage() {
    const so = await api(`/v1/social?build=${state.build}&days=${state.days}`);
    if (so.empty) return [empty("Nothing has been received yet.")];
    const t = so.totals;
    return [
      h("div", { class: "tiles" }, [["Party invites", fmt(t.party_invite_sent)], ["Invites joined", pct(so.invite_join_pct)],
        ["Friend requests", fmt(t.friend_request_sent)], ["Requests accepted", pct(so.friend_accept_pct)], ["Shares", fmt(t.share_triggered)],
        ["Multi-human matches", fmt(so.multi_human_matches)]].map(([k, v]) => h("div", { class: "tile panel sl" }, h("span", { class: "lab" }, k), h("span", { class: "value num" }, v)))),
      card("Every social event", null, table(["Event", "Count"], Object.entries(t).map(([k, v]) => h("tr", {},
        h("td", {}, k.replace(/_/g, " ")), h("td", {}, fmt(v)))))),
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
          statusRow(true, "Editor share of matches", pct(d.editor_share_pct)),
          statusRow(!(d.double_payouts || []).length, "Crystals paid twice", fmt((d.double_payouts || []).reduce((a, r) => a + r.count, 0))),
          statusRow(!(d.cloud_save_failures || []).length, "Cloud save failures", fmt((d.cloud_save_failures || []).reduce((a, r) => a + r.count, 0))))),
      h("div", { class: "grid2" },
        card("Flagged duplicates per day", "same player, event and properties within 50 ms",
          (d.near_duplicates || []).length
            ? barChart(d.near_duplicates.map((r) => ({ label: r.day.slice(5), value: r.near_duplicates,
                tip: `${r.day}: ${fmt(r.near_duplicates)} of ${fmt(r.rows)} rows` })), { height: 180, label: "Duplicates per day", width: 520 })
            : h("p", { class: "small" }, "None.")),
        card("Crystals paid twice", "same player, source and amount within 1 s",
          (d.double_payouts || []).length
            ? table(["Day", "Build", "Source", "Times", "Crystals"], d.double_payouts.map((r) => h("tr", {}, h("td", {}, r.day),
                h("td", {}, r.build_kind), h("td", {}, r.source), h("td", { class: "bad" }, fmt(r.count)), h("td", {}, fmt(r.amount)))))
            : h("p", { class: "small" }, "None. Every payout was made once.")),
        card("Cloud save failures", null,
          (d.cloud_save_failures || []).length
            ? table(["Key", "Failures", "Last"], d.cloud_save_failures.map((r) => h("tr", {}, h("td", {}, r.key),
                h("td", { class: "bad" }, fmt(r.count)), h("td", {}, r.last_day))))
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
    $("preview-note").hidden = !SNAPSHOT;
    if (SNAPSHOT) $("preview-note").textContent = SNAPSHOT.note;
    $("sign-out").hidden = !!SNAPSHOT;
    if (!key()) { page.replaceChildren(); showGate(); return; }
    try {
      const parts = state.page === "ask" ? askPage() : state.page === "erasure" ? erasurePage()
        : await { overview, players, retention, matches, health, ftue: ftuePage, economy: economyPage, social: socialPage }[state.page]();
      page.replaceChildren(...parts);
    } catch (e) {
      if (e.message !== "unauthorised") page.replaceChildren(empty(e.message === "Failed to fetch"
        ? `The server at ${API} could not be reached.` : e.message));
    }
  }

  function route() {
    const name = location.hash.replace("#", "");
    state.page = PAGES[name] ? name : "overview";
    if (state.page !== "players") state.person = null;
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
    try {
      const me = await api("/v1/me");
      state.profiles = !!me.profiles;
      $("who-name").textContent = me.name + (me.admin ? " · admin" : "");
    } catch (e) {}
  }

  // The theme is a colour set from the game (theme.js, make_themes.py): one pick, in the rail. Each swatch is a
  // domain's two pigments as the game draws a prism: the glowing rim over the deep face.
  function looks() {
    const T = window.FA_THEMES, box = $("looks");
    if (!T || !box) return;
    const now = T.current();
    const list = h("div", { class: "sets", role: "group", "aria-label": "Theme" }, ...T.themes.map((t) =>
      h("button", { type: "button", class: "set", title: `${t.line} ${t.pigments.join(", ")}.`, "aria-pressed": String(t.id === now),
                    onclick: () => { T.pick(t.id); looks(); } },
        h("span", { class: "swatch", "aria-hidden": "true" },
          ...t.swatch.map(([rim, face]) => h("i", { style: `background:${face};box-shadow:inset 0 3px 0 ${rim}` }))),
        t.name)));
    box.replaceChildren(h("span", { class: "lab" }, "Theme"), list);
    const strip = document.querySelector(".brand .strip");
    if (strip) strip.replaceChildren(...[1, 2, 3, 4].map((n) => h("i", { style: `background:var(--series-${n})` })));
  }

  // The sea lines: the menu's SeaLines shader (Assets/_Graphics/Materials/Shaders/UI/UISeaLines.cginc, SeaLine) as one
  // mask tile per layer, one loop wide. Line i rests at i * spacing, lives in layer i mod 3, and is two sines whose
  // periods divide the loop, with its own amplitude and phase.
  function sea() {
    const T = window.FA_THEMES, layers = document.querySelectorAll(".sea-layer");
    if (!T || !T.sea || layers.length !== 3) return;
    const { spacing, loop, amplitude } = T.sea, tall = 2400, k1 = 2 * Math.PI / loop, k2 = 3 * k1;
    const mod3 = (x) => x - 3 * Math.floor(x / 3);
    T.sea.layers.forEach((layer, n) => {
      let lines = "";
      for (let i = 0; i * spacing <= tall + spacing; i++) {
        if (mod3(i) !== n) continue;
        const a1 = amplitude * (1 + 0.45 * mod3(i * 7)), a2 = amplitude * (0.45 + 0.22 * mod3(i * 5));
        const pts = [];
        for (let x = 0; x <= loop; x += 6) {
          pts.push(`${x},${(i * spacing + a1 * Math.sin(k1 * x + i * 0.9) + a2 * Math.sin(k2 * x + i * 1.7)).toFixed(1)}`);
        }
        lines += `<polyline points='${pts.join(" ")}' fill='none' stroke='#fff' stroke-opacity='${layer.alpha}' stroke-width='${layer.width}'/>`;
      }
      const svg = `<svg xmlns='http://www.w3.org/2000/svg' width='${loop}' height='${tall}' viewBox='0 0 ${loop} ${tall}'>${lines}</svg>`;
      const url = `url("data:image/svg+xml,${encodeURIComponent(svg)}")`;
      const el = layers[n];
      el.style.webkitMaskImage = url; el.style.maskImage = url;
      el.style.webkitMaskSize = `${loop}px ${tall}px`; el.style.maskSize = `${loop}px ${tall}px`;
      el.style.animationDuration = `${layer.seconds}s`;   // the shader's 90 / 68 / 45 s, far to near
    });
  }

  sea();
  looks();
  window.addEventListener("hashchange", route);
  whoAmI().then(route);
})();
