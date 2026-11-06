/* The Froglet Analytics console (Frogma Rev 50 B, grown to Rev 65: filters, chart switches, cohorts, eras, play
   types, the Query Studio and dashboards; Docs/MENU_REDESIGN_B.md §141). Reads the analytics server's read API; holds
   no data of its own. API base: ?api=<url> in the address (for GitHub Pages), else the page's own origin (served by
   the server). The read key is kept in this browser's localStorage only. Every value from the server reaches the page
   through textContent (h() below), never as HTML. */
(function () {
  "use strict";

  // Preview mode: a page built by make_preview.py carries a snapshot of the server's answers (invented sample data)
  // in <script id="fa-snapshot">, and reads it instead of a server. The live console never has one.
  const SNAP_EL = document.getElementById("fa-snapshot");
  const SNAPSHOT = SNAP_EL ? JSON.parse(SNAP_EL.textContent) : null;
  const params = new URLSearchParams(location.search);
  const API = (params.get("api") || location.origin).replace(/\/$/, "");
  const KEY_STORE = "froglet-analytics-key";
  const DAYS = [7, 30, 90, 365];
  // build / days / play / era: which header filters a page takes (and so which it sends to the server).
  const PAGES = {
    overview: { title: "Overview", build: true, days: DAYS, play: true, era: true },
    players: { title: "Players", build: true, days: DAYS, play: true, era: true },
    cohorts: { title: "Cohorts and lifespans", build: true, days: DAYS, play: true, era: true },
    eras: { title: "Eras", build: true, days: DAYS, play: false, era: false },
    playtypes: { title: "Solo vs AI, Solo, Multiplayer", build: true, days: DAYS, play: false, era: true },
    retention: { title: "Retention", build: true, days: DAYS, play: true, era: true },
    matches: { title: "Matches", build: true, days: DAYS, play: true, era: true },
    ftue: { title: "FTUE", build: true, days: DAYS },
    economy: { title: "Economy", build: true, days: DAYS },
    social: { title: "Social", build: true, days: DAYS },
    health: { title: "Data health", build: false, days: [14, 30, 90] },
    ask: { title: "Ask Claude", build: false, days: null },
    query: { title: "Query Studio", build: true, days: DAYS, play: true, era: true, admin: true },
    dashboards: { title: "Dashboards", build: true, days: DAYS, play: true, era: true, admin: true },
    erasure: { title: "Erasure", build: false, days: null, admin: true },
  };
  const BUILDS = [["release", "Release"], ["editor", "Editor"], ["port", "Port"], ["development", "Dev"]];
  const PLAYS = [["all", "All"], ["solo_vs_ai", "Solo vs AI"], ["solo", "Solo"], ["multi", "Multiplayer"]];
  const PLAY_TYPES = {
    solo_vs_ai: "Solo vs AI", solo_alone: "Solo", solo: "Solo", multi: "Multiplayer", coop: "Co-op", adversarial: "Versus",
    mixed: "Mixed teams", multi_human_unknown: "Multi-human (teams not recorded)",
  };
  // The three play groups the Play filter picks (analytics_views.PLAYS) and their fixed colours: Jade solo vs AI,
  // Gold solo, Ruby multiplayer (the series slots 1, 3, 2), Blue plain data.
  const GROUPS = [["solo_vs_ai", "Solo vs AI", "var(--series-1)"], ["solo", "Solo", "var(--series-3)"], ["multi", "Multiplayer", "var(--series-2)"]];
  const groupOf = (t) => (t === "solo_vs_ai" ? "solo_vs_ai" : t === "solo_alone" || t === "solo" ? "solo" : t ? "multi" : null);
  const GROUP_COLOUR = Object.fromEntries(GROUPS.map(([k, , c]) => [k, c]));
  const GROUP_LABEL = Object.fromEntries(GROUPS.map(([k, l]) => [k, l]));
  const OTHER = "var(--fs-text-off)";
  const SUGGESTIONS = [
    "Which play type brings players back for a rematch?",
    "Which game modes do players finish least often?",
    "How many players came back the day after their first match, by play type?",
  ];

  const state = { page: "overview", build: "release", days: 90, healthDays: 30, play: "all", era: "", ask: null, asking: false,
                  profiles: false, admin: false, me: "", person: null, eras: null, milestones: [], unfiltered: [], presets: [] };
  let countryNames = null;
  try { countryNames = new Intl.DisplayNames(["en"], { type: "region" }); } catch (e) {}
  const country = (code) => {
    if (!code || code === "unknown") return "Unknown";
    try { return countryNames ? `${countryNames.of(code)} (${code})` : code; } catch (e) { return code; }
  };
  const when = (ms) => (ms ? new Date(ms).toISOString().slice(0, 16).replace("T", " ") : "–");
  const dayOf = (ms) => (ms ? new Date(ms).toISOString().slice(0, 10) : "–");
  const seconds = (s) => (s === null || s === undefined ? "–" : s >= 90 ? `${Math.round(s / 6) / 10} min` : `${Math.round(s)} s`);
  const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
  const shortDay = (iso) => {                        // "2026-11-01" -> "1 Nov"
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(iso || ""));
    return m ? `${Number(m[3])} ${MONTHS[Number(m[2]) - 1]}` : String(iso ?? "–");
  };
  const DAY_MS = 86400000;
  const isoMs = (iso) => Date.parse(String(iso).slice(0, 10) + "T00:00:00Z");
  const todayIso = () => new Date().toISOString().slice(0, 10);

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
  const fmt = (n) => (n === null || n === undefined || n === "" ? "–" : Number.isFinite(Number(n)) ? Number(n).toLocaleString("en-GB") : String(n));
  const pct = (n) => (n === null || n === undefined ? "–" : `${Math.round(n * 10) / 10}%`);
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
  const sum = (list, f) => list.reduce((a, x) => a + (Number(f ? f(x) : x) || 0), 0);
  const median = (list) => {
    const v = list.filter((x) => x !== null && x !== undefined && !Number.isNaN(x)).sort((a, b) => a - b);
    if (!v.length) return null;
    return v.length % 2 ? v[(v.length - 1) / 2] : (v[v.length / 2 - 1] + v[v.length / 2]) / 2;
  };

  // Browser storage: per-viewer conveniences only (each chart's switches, the Players filters). It can be missing or
  // throw (a private window, blocked site data); everything works without it.
  const store = {
    get(k, fallback) { try { const v = localStorage.getItem(k); return v === null ? fallback : JSON.parse(v); } catch (e) { return fallback; } },
    set(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) {} },
  };

  // ------------------------------------------------------------------ the server

  function key() { if (SNAPSHOT) return "preview"; try { return localStorage.getItem(KEY_STORE) || ""; } catch (e) { return ""; } }
  function setKey(k) { try { k ? localStorage.setItem(KEY_STORE, k) : localStorage.removeItem(KEY_STORE); } catch (e) {} }

  class ApiError extends Error { constructor(message, status) { super(message); this.status = status; } }

  async function api(path, body, method) {
    if (SNAPSHOT) return snapshotAnswer(path, body, method);
    const verb = method || (body ? "POST" : "GET");
    const res = await fetch(API + path, {
      method: verb,
      headers: Object.assign({ Authorization: "Bearer " + key() }, body ? { "Content-Type": "application/json" } : {}),
      body: body ? JSON.stringify(body) : undefined,
    });
    if (res.status === 401) { showGate("That key was not accepted."); throw new ApiError("unauthorised", 401); }
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new ApiError(data.error || `The server answered ${res.status}.`, res.status);
    return data;
  }

  // GETs that several panels ask for at once (the series behind each chart, the eras) are shared for a few seconds.
  const getCache = new Map();
  function cached(path, ttl = 20000) {
    const hit = getCache.get(path);
    if (hit && Date.now() - hit.t < ttl) return hit.p;
    const p = api(path);
    getCache.set(path, { t: Date.now(), p });
    p.catch(() => getCache.delete(path));
    return p;
  }

  function snapshotAnswer(path, body, method) {
    if (method && method !== "GET" && method !== "POST") throw new ApiError("Saving is off in this preview: there is no server behind it.", 403);
    if (path === "/v1/erase") throw new ApiError("Erasure is off in this preview: there is no server behind it.", 403);
    if (path === "/v1/ask") {
      const answer = SNAPSHOT.ask[(body && body.question || "").trim()];
      if (!answer) throw new ApiError("This preview has answers for the three questions below only. The live console sends any question to Claude.", 404);
      return JSON.parse(JSON.stringify(answer));
    }
    if (body) throw new ApiError("Saving is off in this preview: there is no server behind it.", 403);
    if (path.startsWith("/v1/people")) {                 // the snapshot holds the whole list; search it here
      const list = SNAPSHOT.get[path.replace(/&q=[^&]*/, "")] || SNAPSHOT.get[path.replace(/&q=[^&]*/, "").replace(/&play=[^&]*|&era=[^&]*/g, "")];
      if (!list) throw new ApiError("This preview has no snapshot for that view.", 404);
      return JSON.parse(JSON.stringify(list));
    }
    const page = SNAPSHOT.get[path] || SNAPSHOT.get[path.replace(/&play=all/, "")];
    if (!page) throw new ApiError("This preview has no snapshot for that view.", 404);
    return JSON.parse(JSON.stringify(page));
  }

  function showGate(message) {
    $("gate").hidden = false;
    $("gate-error").hidden = !message;
    $("gate-error").textContent = message || "";
    $("gate-key").focus();
  }

  // The header filters as a query string, for the filters this page takes.
  function filterQuery(meta, extra) {
    meta = meta || PAGES[state.page];
    const q = new URLSearchParams();
    if (meta.build) q.set("build", state.build);
    if (meta.days) q.set("days", String(state.era && meta.era ? 365 : state.days));
    if (meta.play && state.play !== "all") q.set("play", state.play);
    if (meta.era && state.era) q.set("era", state.era);
    for (const [k, v] of Object.entries(extra || {})) if (v !== null && v !== undefined && v !== "") q.set(k, v);
    return q.toString();
  }
  const eraById = (id) => (state.eras || []).find((e) => e.id === id);
  const eraColour = (era) => `var(--cs-${["jade", "ruby", "gold", "blue"].includes(era && era.colour) ? era.colour : "blue"})`;
  // The window a page's numbers cover: the chosen era's range, else the last N days.
  function windowRange() {
    const era = state.era && eraById(state.era);
    const now = Date.now();
    if (era) return [isoMs(era.from), Math.min(now, isoMs(era.to) + DAY_MS)];
    return [now - state.days * DAY_MS, now];
  }
  const rangeLabel = () => {
    const era = state.era && eraById(state.era);
    return era ? era.name : `last ${state.days} days`;
  };

  // ------------------------------------------------------------------ charts
  // A chart draws a FRAME: {time, xs, keys, by, kind, v: Map} where v.get(x + SEP + k) is a value.
  //   time frame: xs are days/weeks (or ordered points), keys are the series ([null] for one series).
  //   category frame: xs = [null], keys are the categories (modes, vessels, play types...).
  // kind: "count" (adds up and stacks), "pct", "avg" or "ratio" (do not). label(k) / xlabel(x) name things.
  // Colours: a play-type or era split wears its entity's colour; any other split takes --series-1..4 in order (Jade,
  // Ruby, Gold, Blue) and folds the 5th part on into "Other" (grey). Text is always in text tokens.

  const SEP = "\u0001";
  const MEASURES = {
    matches: ["Matches", "count"], players: ["Players", "count"], finished_pct: ["Finished %", "pct"],
    rematch_pct: ["Rematch %", "pct"], flight_min: ["Minutes a match", "avg"], new_players: ["New players", "count"],
    events: ["Events", "count"],
    // Freestyle (the Toybox): its own panel offers these; the match charts do not
    freestyle_entries: ["Toybox entries", "count"], freestyle_players: ["Toybox players", "count"],
    freestyle_minutes: ["Toybox minutes", "count"], freestyle_switches: ["Vessel switches", "count"],
  };
  const MATCH_MEASURES = ["matches", "players", "finished_pct", "rematch_pct", "flight_min", "new_players", "events"];
  const SPLITS = [["none", "None"], ["play_type", "Play type"], ["element", "Element"], ["era", "Era"], ["platform", "Platform"],
                  ["country", "Country"], ["game_mode", "Mode"], ["vessel_class", "Vessel"], ["build_kind", "Build"],
                  ["intensity", "Intensity"], ["from_vessel_class", "Switched from"], ["switch_source", "Source"]];
  const MATCH_SPLITS = SPLITS.map(([k]) => k).filter((k) => !["from_vessel_class", "switch_source"].includes(k));
  // ELEMENTS HAVE NO COLOUR (LOCKED): an element split is one neutral hue in steps (the text token mixed into the
  // ground), with its name beside every mark; never Jade, Ruby, Gold, Blue or the domain series colours.
  const ELEMENT_ORDER = ["Charge", "Mass", "Space", "Time", "Omni", "none"];
  const ELEMENT_SHADE = { Charge: 100, Mass: 78, Space: 60, Time: 45, Omni: 35, none: 24 };
  const elementColour = (k) => `color-mix(in srgb, var(--fs-text) ${ELEMENT_SHADE[k] || ELEMENT_SHADE.none}%, var(--fs-ground))`;
  const elementLabel = (k) => (!k || k === "none" ? "No element" : String(k));
  const SWITCH_SOURCE = { event: "Exact (game event)", recovered: "Recovered (approx.)" };
  const OVERS = [["day", "Day"], ["week", "Week"], ["none", "Total"]];

  function makeFrame(o) {
    const f = Object.assign({ time: false, xs: [null], keys: [null], by: "none", kind: "count", v: new Map(),
                              label: null, xlabel: null, colours: null }, o);
    f.get = (x, k) => f.v.get(String(x) + SEP + String(k));
    f.total = (k) => sum(f.xs, (x) => f.get(x, k));
    return f;
  }
  // From GET /v1/series's points.
  function frameFromSeries(r) {
    const time = r.over && r.over !== "none";
    const v = new Map();
    const keys = (r.keys && r.keys.length ? r.keys : [...new Set((r.points || []).map((p) => p.key))]).map((k) => k ?? null);
    if (!keys.length) keys.push(null);
    let xs = [null];
    if (time) xs = [...new Set((r.points || []).map((p) => p.x))].sort();
    const kind = (MEASURES[r.measure] || [, "count"])[1];
    // A count split by play type folds the raw types (co-op, versus, mixed...) into the three groups and their colours.
    const merge = r.by === "play_type" && kind === "count";
    const keyOf = (k) => (merge && groupOf(k) ? groupOf(k) : k ?? null);
    const ks = merge ? [...new Set(keys.map(keyOf))] : keys;
    for (const p of r.points || []) {
      const at = String(time ? p.x : null) + SEP + String(keyOf(p.key));
      v.set(at, merge ? (v.get(at) || 0) + (p.value || 0) : p.value);
    }
    const notes = (r.notes || []).slice();
    if (Array.isArray(r.secondary) && r.secondary.length)   // two-element modes: their second element, listed apart
      notes.unshift(`As a second element: ${r.secondary.map((x) => `${x.key} ${fmt(x.value)} matches`).join(", ")}.`);
    return makeFrame({ time, xs, keys: ks, by: r.by || "none", kind, v, measure: r.measure, unfiltered: r.unfiltered || [],
                       answerable: Array.isArray(r.answerable_by) ? r.answerable_by : null, notes });
  }
  // From plain rows: [{x, value}] over time, or [{key, value}] by category.
  function timeFrame(rows, xKey, valueKey, extra) {
    const v = new Map();
    rows.forEach((r) => v.set(String(r[xKey]) + SEP + "null", r[valueKey]));
    return makeFrame(Object.assign({ time: true, xs: rows.map((r) => r[xKey]) }, extra || {}, { v }));
  }
  function catFrame(rows, keyKey, valueKey, extra) {
    const v = new Map();
    rows.forEach((r) => v.set("null" + SEP + String(r[keyKey]), r[valueKey]));
    return makeFrame(Object.assign({ time: false, keys: rows.map((r) => r[keyKey]) }, extra || {}, { v }));
  }

  const keyLabel = (f, k) => {
    if (f.label) return f.label(k);
    if (k === null || k === undefined) return MEASURES[f.measure] ? MEASURES[f.measure][0] : "Value";
    if (k === "other") return "Other";
    if (f.by === "play_type") return GROUP_LABEL[groupOf(k)] && !PLAY_TYPES[k] ? GROUP_LABEL[groupOf(k)] : playType(k);
    if (f.by === "era") return (eraById(k) || {}).name || k;
    if (f.by === "country") return country(k);
    if (f.by === "intensity") return `Intensity ${k}`;
    if (f.by === "element") return elementLabel(k);
    if (f.by === "switch_source") return SWITCH_SOURCE[k] || String(k);
    if ((f.by === "vessel_class" || f.by === "from_vessel_class") && k === "unknown") return "Not named (older builds)";
    return String(k);
  };
  const xLabel = (f, x) => (f.xlabel ? f.xlabel(x) : /^\d{4}-\d{2}-\d{2}/.test(String(x)) ? shortDay(x) : String(x ?? ""));
  const valueText = (f, v) => (v === null || v === undefined ? "–" : f.kind === "pct" ? pct(v)
    : f.kind === "avg" ? (Math.round(v * 10) / 10).toLocaleString("en-GB") : f.kind === "ratio" ? ratio(v) : fmt(Math.round(v * 100) / 100));
  // A key's colour; `i` is its place among the shown keys.
  function keyColour(f, k, i) {
    if (f.colours && f.colours[k]) return f.colours[k];
    if (k === "other") return OTHER;
    if (f.by === "play_type" && groupOf(k)) {                // several raw multiplayer types: the first wears Ruby
      const same = f.keys.filter((x) => groupOf(x) === groupOf(k));
      if (same.length === 1 || same[0] === k) return GROUP_COLOUR[groupOf(k)];
      return same.indexOf(k) === 1 ? "var(--series-4)" : OTHER;
    }
    if (f.by === "era" && eraById(k)) return eraColour(eraById(k));
    if (f.by === "element") return elementColour(k);
    if (f.keys.length === 1 && (k === null || !f.time)) return "var(--data)";
    return i < 4 ? `var(--series-${i + 1})` : OTHER;
  }
  // At most four named series and "Other" for a picture (the Table shows every one).
  function folded(f) {
    if (f.keys.length <= 5 || f.by === "play_type" || f.by === "era" || f.by === "element") return f;
    const keep = f.keys.filter((k) => k !== "other").slice(0, 4), rest = f.keys.filter((k) => !keep.includes(k));
    if (f.kind !== "count") return makeFrame(Object.assign({}, f, { keys: keep, hidden: rest.length }));
    const v = new Map(f.v);
    for (const x of f.xs) v.set(String(x) + SEP + "other", sum(rest, (k) => f.get(x, k)));
    return makeFrame(Object.assign({}, f, { keys: keep.concat("other"), v }));
  }

  function niceMax(v) {
    if (!v || v <= 0) return 1;
    const p = Math.pow(10, Math.floor(Math.log10(v)));
    for (const m of [1, 2, 2.5, 5, 10]) if (m * p >= v) return m * p;
    return 10 * p;
  }

  // A tooltip in the chart's box; it follows the mark and stays inside the box.
  function tipFor(box) {
    const tip = h("div", { class: "tip", hidden: true });
    box.append(tip);
    return (text, x, y) => {
      if (!text) { tip.hidden = true; return; }
      tip.textContent = text;
      tip.hidden = false;
      const w = box.clientWidth, tw = tip.offsetWidth;
      tip.style.left = Math.max(tw / 2, Math.min(w - tw / 2, x)) + "px";
      tip.style.top = y + "px";
    };
  }

  // Draws at the box's real width (so text stays 12px on a phone), again whenever the width changes.
  function responsive(draw, cls) {
    const box = h("div", { class: "chart" + (cls ? " " + cls : "") });
    let last = 0;
    const paint = () => {
      const w = Math.floor(box.clientWidth);
      if (!w || Math.abs(w - last) < 4) return;
      last = w;
      box.replaceChildren();
      const out = draw(w, box);
      for (const n of [].concat(out)) if (n) box.append(n);
    };
    if (window.ResizeObserver) new ResizeObserver(() => paint()).observe(box);
    requestAnimationFrame(paint);
    setTimeout(paint, 60);
    return box;
  }

  function legend(f, keys, withTotals) {
    if (keys.length < 2) return null;
    return h("div", { class: "legend" }, keys.map((k, i) => h("span", { class: "legend-item" },
      h("i", { style: `background:${keyColour(f, k, i)}` }),
      h("span", {}, keyLabel(f, k) + (withTotals && f.kind === "count" ? ` ${fmt(f.total(k))}` : "")))));
  }

  function xTicks(xs, every) {
    const n = xs.length, want = Math.max(2, Math.min(n, every));
    const out = new Set([0, n - 1]);
    for (let i = 1; i < want - 1; i++) out.add(Math.round(i * (n - 1) / (want - 1)));
    return [...out].sort((a, b) => a - b);
  }

  // Vertical bars over time; several count series stack.
  function drawTimeBars(f0, W, box, { height = 220 } = {}) {
    const f = folded(f0);
    const H = height, L = 46, B = 24, T = 10, R = 6;
    const stackOf = (x) => sum(f.keys, (k) => f.get(x, k));
    const max = niceMax(Math.max(0, ...f.xs.map((x) => (f.keys.length > 1 ? stackOf(x) : f.get(x, f.keys[0]) || 0))));
    const plotW = W - L - R, plotH = H - B - T;
    const step = f.xs.length ? plotW / f.xs.length : plotW;
    const svg = s("svg", { width: W, height: H, viewBox: `0 0 ${W} ${H}`, role: "img", "aria-label": "Bar chart" });
    for (const fr of [0, 0.5, 1]) {
      const y = T + plotH * (1 - fr);
      svg.append(s("line", { class: "grid", x1: L, x2: W - R, y1: y, y2: y }),
        s("text", { class: "axis", x: L - 8, y: y + 4, "text-anchor": "end" }, valueText(f, max * fr)));
    }
    const tip = tipFor(box);
    const gap = step > 6 ? 2 : step > 3 ? 1 : 0;
    f.xs.forEach((x, i) => {
      let y0 = T + plotH;
      const bx = L + i * step + gap / 2, bw = Math.max(1, step - gap);
      const segs = [];
      f.keys.forEach((k, ki) => {
        const val = f.get(x, k) || 0;
        const bh = max ? plotH * val / max : 0;
        if (bh <= 0) return;
        const r = s("rect", { class: "mark", x: bx, y: y0 - bh, width: bw, height: Math.max(bh - (f.keys.length > 1 ? 1 : 0), 1),
                              style: `fill:${keyColour(f, k, ki)}` });
        segs.push(r); svg.append(r); y0 -= bh;
      });
      const hit = s("rect", { class: "hit", x: L + i * step, y: T, width: Math.max(step, 1), height: plotH });
      const text = f.keys.length > 1
        ? `${xLabel(f, x)}: ` + f.keys.map((k) => `${keyLabel(f, k)} ${valueText(f, f.get(x, k))}`).join(", ")
        : `${xLabel(f, x)}: ${valueText(f, f.get(x, f.keys[0]))}${f.tips && f.tips[x] ? " · " + f.tips[x] : ""}`;
      hit.addEventListener("mouseenter", () => { segs.forEach((r) => r.classList.add("hot")); tip(text, bx + bw / 2, y0); });
      hit.addEventListener("mouseleave", () => { segs.forEach((r) => r.classList.remove("hot")); tip(null); });
      svg.append(hit);
    });
    const every = Math.max(2, Math.floor(plotW / 90));
    for (const i of xTicks(f.xs, every)) {
      const cx = L + i * step + step / 2;
      svg.append(s("text", { class: "axis", x: cx, y: H - 6, "text-anchor": i === 0 && step < 30 ? "start" : i === f.xs.length - 1 && step < 30 ? "end" : "middle" }, xLabel(f, f.xs[i])));
    }
    return [svg, legend(f, f.keys, true)];
  }

  // One 2px line per series; a crosshair reads every series at a point; up to four lines are named at their end.
  function drawLines(f0, W, box, { height = 220, target } = {}) {
    const f = folded(f0);
    const named = f.keys.length > 1 && f.keys.length <= (f.by === "element" ? 6 : 4) && W > 520;
    const H = height, L = 46, R = named ? 104 : 12, B = 24, T = 12;
    const all = [];
    f.keys.forEach((k) => f.xs.forEach((x) => { const v = f.get(x, k); if (v !== null && v !== undefined) all.push(v); }));
    const top = niceMax(Math.max(target || 0, ...all, 0));
    const plotW = W - L - R, plotH = H - B - T;
    const xAt = (i) => L + (f.xs.length > 1 ? plotW * i / (f.xs.length - 1) : plotW / 2);
    const yAt = (v) => T + plotH * (1 - v / top);
    const svg = s("svg", { width: W, height: H, viewBox: `0 0 ${W} ${H}`, role: "img", "aria-label": "Line chart" });
    for (const fr of [0, 0.5, 1]) {
      svg.append(s("line", { class: "grid", x1: L, x2: W - R, y1: yAt(top * fr), y2: yAt(top * fr) }),
        s("text", { class: "axis", x: L - 8, y: yAt(top * fr) + 4, "text-anchor": "end" }, valueText(f, top * fr)));
    }
    if (target !== undefined) {
      svg.append(s("line", { class: "target", x1: L, x2: W - R, y1: yAt(target), y2: yAt(target) }),
        s("text", { class: "axis", x: W - R, y: yAt(target) - 6, "text-anchor": "end" }, `target ${valueText(f, target)}`));
    }
    const ends = [];
    f.keys.forEach((k, ki) => {
      const pts = [];
      f.xs.forEach((x, i) => { const v = f.get(x, k); if (v !== null && v !== undefined) pts.push([xAt(i), yAt(v)]); });
      if (!pts.length) return;
      const colour = keyColour(f, k, ki);
      svg.append(s("polyline", { class: "line", style: `stroke:${colour}`, points: pts.map((p) => p.join(",")).join(" ") }));
      const [lx, ly] = pts[pts.length - 1];
      svg.append(s("rect", { x: lx - 4, y: ly - 4, width: 8, height: 8, style: `fill:${colour}` }));
      ends.push([ly, k]);
    });
    if (named) {                                         // direct labels, nudged apart
      ends.sort((a, b) => a[0] - b[0]);
      let prev = -99;
      for (const e of ends) { const y = Math.max(e[0] + 4, prev + 14); prev = y;
        svg.append(s("text", { class: "axis label", x: W - R + 10, y }, keyLabel(f, e[1]))); }
    }
    const tip = tipFor(box);
    const cross = s("line", { class: "grid", x1: 0, x2: 0, y1: T, y2: T + plotH, visibility: "hidden" });
    svg.append(cross);
    const slot = f.xs.length > 1 ? plotW / (f.xs.length - 1) : plotW;
    f.xs.forEach((x, i) => {
      const hit = s("rect", { class: "hit", x: xAt(i) - slot / 2, y: T, width: Math.max(slot, 1), height: plotH });
      hit.addEventListener("mouseenter", () => {
        cross.setAttribute("x1", xAt(i)); cross.setAttribute("x2", xAt(i)); cross.setAttribute("visibility", "visible");
        const text = f.keys.length > 1
          ? `${xLabel(f, x)}: ` + f.keys.map((k) => `${keyLabel(f, k)} ${valueText(f, f.get(x, k))}`).join(", ")
          : `${xLabel(f, x)}: ${valueText(f, f.get(x, f.keys[0]))}${f.tips && f.tips[x] ? " · " + f.tips[x] : ""}`;
        tip(text, xAt(i), yAt(Math.max(0, ...f.keys.map((k) => f.get(x, k) || 0))));
      });
      hit.addEventListener("mouseleave", () => { cross.setAttribute("visibility", "hidden"); tip(null); });
      svg.append(hit);
    });
    const every = Math.max(2, Math.floor(plotW / 90));
    for (const i of xTicks(f.xs, every)) {
      svg.append(s("text", { class: "axis", x: xAt(i), y: H - 6, "text-anchor": i === 0 ? "start" : i === f.xs.length - 1 ? "end" : "middle" }, xLabel(f, f.xs[i])));
    }
    return [svg, named ? null : legend(f, f.keys, false)];
  }

  // Ranked horizontal bars, one per category (or per series total over time). HTML rows, so labels wrap.
  function drawHBars(f, W, box, { limit = 12 } = {}) {
    const items = f.time ? f.keys.map((k) => [k, f.kind === "count" ? f.total(k) : median(f.xs.map((x) => f.get(x, k)))])
      : f.keys.map((k) => [k, f.get(null, k)]);
    const shown = items.slice(0, limit);
    const max = f.kind === "pct" ? 100 : Math.max(1, ...shown.map((r) => Number(r[1]) || 0));
    const tip = tipFor(box);
    const entity = f.by === "play_type" || f.by === "era" || f.by === "element" || f.colours;   // otherwise one series: colour is not identity
    const rows = h("div", { class: "hbars" }, shown.map(([k, v], i) => {
      const colour = entity ? keyColour(f, k, i) : "var(--data)";
      const fill = h("div", { class: "hbar-fill", style: `width:${Math.max(0, Math.min(100, 100 * (Number(v) || 0) / max))}%;background:${colour}` });
      const row = h("div", { class: "hbar" }, h("span", { class: "hbar-label" }, keyLabel(f, k)),
        h("div", { class: "hbar-track" }, fill), h("span", { class: "hbar-value num" }, valueText(f, v)));
      row.addEventListener("mouseenter", () => { fill.classList.add("hot"); tip(`${keyLabel(f, k)}: ${valueText(f, v)}${f.tips && f.tips[k] ? " · " + f.tips[k] : ""}`, row.offsetLeft + row.offsetWidth / 2, row.offsetTop); });
      row.addEventListener("mouseleave", () => { fill.classList.remove("hot"); tip(null); });
      return row;
    }));
    const more = items.length > limit ? h("p", { class: "small" }, `+${items.length - limit} more in the Table.`) : null;
    return [rows, more];
  }

  // A donut for one whole in at most six parts (the fifth and sixth fold into Other).
  function drawDonut(f0, W, box, { centre, max = 200 } = {}) {
    let items = f0.time ? f0.keys.map((k) => [k, f0.total(k)]) : f0.keys.map((k) => [k, Number(f0.get(null, k)) || 0]);
    const entity = f0.by === "play_type" || f0.by === "era" || f0.by === "element" || f0.colours;
    if (!entity && items.length > 4) {
      const rest = items.slice(4);
      items = items.slice(0, 4).concat([["other", sum(rest, (r) => r[1])]]);
    }
    const total = sum(items, (r) => r[1]);
    const size = Math.min(max, Math.max(Math.min(140, max), W * 0.42)), R = size / 2, r0 = R * 0.62;
    const svg = s("svg", { width: size, height: size, viewBox: `0 0 ${size} ${size}`, role: "img", "aria-label": "Donut chart" });
    const tip = tipFor(box);
    let a = -Math.PI / 2;
    items.forEach(([k, v], i) => {
      if (!v) return;
      const da = 2 * Math.PI * v / total, a1 = a + da;
      const big = da > Math.PI ? 1 : 0;
      const p = (rad, ang) => `${R + rad * Math.cos(ang)},${R + rad * Math.sin(ang)}`;
      const d = da >= 2 * Math.PI - 1e-6
        ? `M${p(R, a)} A${R},${R} 0 1 1 ${p(R, a + Math.PI)} A${R},${R} 0 1 1 ${p(R, a)} M${p(r0, a)} A${r0},${r0} 0 1 0 ${p(r0, a + Math.PI)} A${r0},${r0} 0 1 0 ${p(r0, a)}Z`
        : `M${p(R, a)} A${R},${R} 0 ${big} 1 ${p(R, a1)} L${p(r0, a1)} A${r0},${r0} 0 ${big} 0 ${p(r0, a)}Z`;
      const colour = entity ? keyColour(f0, k, i) : k === "other" ? OTHER : `var(--series-${i + 1})`;
      const path = s("path", { d, class: "slice", style: `fill:${colour}`, "fill-rule": "evenodd" });
      const mid = a + da / 2;
      path.addEventListener("mouseenter", () => { path.classList.add("hot"); tip(`${keyLabel(f0, k)}: ${valueText(f0, v)} (${pct(100 * v / total)})`, R + R * 0.8 * Math.cos(mid), R + R * 0.8 * Math.sin(mid)); });
      path.addEventListener("mouseleave", () => { path.classList.remove("hot"); tip(null); });
      svg.append(path);
      a = a1;
    });
    svg.append(s("text", { class: "donut-num", x: R, y: R + (size < 130 ? 8 : 6), "text-anchor": "middle", style: size < 130 ? "font-size:22px" : null }, fmt(Math.round(total))));
    if (size >= 130) svg.append(s("text", { class: "axis", x: R, y: R + 26, "text-anchor": "middle" }, centre || (MEASURES[f0.measure] || ["total"])[0].toLowerCase()));
    const leg = h("div", { class: "donut-legend" }, items.map(([k, v], i) => h("div", { class: "donut-key" },
      h("i", { style: `background:${entity ? keyColour(f0, k, i) : k === "other" ? OTHER : `var(--series-${i + 1})`}` }),
      h("span", {}, keyLabel(f0, k)), h("span", { class: "num" }, `${valueText(f0, v)} · ${pct(total ? 100 * v / total : 0)}`))));
    return h("div", { class: "donut" }, svg, leg);
  }

  function drawTable(f) {
    if (f.table) return table(f.table.columns, f.table.rows.map((r) => h("tr", {}, r.map((c) => h("td", {}, c ?? "–")))));
    if (f.time) {
      const cols = [f.xname || "When"].concat(f.keys.map((k) => keyLabel(f, k)));
      return table(cols, f.xs.slice().reverse().map((x) => h("tr", {}, h("td", {}, xLabel(f, x)),
        f.keys.map((k) => h("td", {}, valueText(f, f.get(x, k)))))));
    }
    const total = f.kind === "count" ? sum(f.keys, (k) => f.get(null, k)) : 0;
    return table([f.kname || "", MEASURES[f.measure] ? MEASURES[f.measure][0] : f.vname || "Value"].concat(total ? ["Share"] : []),
      f.keys.map((k) => h("tr", {}, h("td", {}, keyLabel(f, k)), h("td", {}, valueText(f, f.get(null, k))),
        total ? h("td", { class: "soft" }, pct(100 * (f.get(null, k) || 0) / total)) : null)));
  }

  function drawNumber(f) {
    const days = f.time ? f.xs.map((x) => f.get(x, f.keys[0])).filter((v) => v !== null && v !== undefined) : [];
    const v = f.keys.length === 1 && !f.time ? f.get(null, f.keys[0]) : f.kind === "count" ? sum(f.keys, (k) => f.total(k))
      : days.length ? sum(days) / days.length : null;
    return h("div", { class: "big-number" }, h("span", { class: "value num" }, valueText(f, v)),
      h("span", { class: "small" }, f.time ? (f.kind === "count" ? `in all, ${xLabel(f, f.xs[0])} to ${xLabel(f, f.xs[f.xs.length - 1])}` : `the average of ${days.length} days`)
        : keyLabel(f, f.keys[0])));
  }

  // ------------------------------------------------------------------ the chart switches
  // Every chart panel carries its own small switches, so business, marketing and analysis read the same chart their
  // own way (Shombith, 2026-11-06). chartView(cfg) returns the switches plus the chart; chartPanel(cfg) wraps it in a
  // card. cfg:
  //   id        a stable name; the reader's choice is remembered per chart (localStorage, try/catch)
  //   load      async ({measure, by, over}) -> a frame (seriesLoad() asks GET /v1/series)
  //   measures  [[key, label]]  MEASURE switch, when more than one
  //   splits    [key...]        SPLIT switch (None | Play type | Era | ...); an option the server answers 400 is hidden
  //   overs     [key...]        OVER switch (Day | Week | Total)
  //   forms     the builtin forms it may offer, of bars | line | pie | table | number (default the first four);
  //             Pie only when the split has 2-6 parts, Line only over time
  //   custom    {name: {label, draw(frame, width, box)}} extra forms (heatmap, gantt, plan), offered first
  //   defaults  {form, measure, by, over}
  const FORM_LABEL = { bars: "Bars", line: "Line", pie: "Pie", table: "Table", number: "Number" };
  const rejected = new Set();                            // "measure|by|over" the server answered 400

  function seriesLoad(extra) {
    const load = async ({ measure, by, over }) => {
      const meta = PAGES[state.page];
      const path = `/v1/series?${filterQuery(Object.assign({}, meta, { build: true, days: meta.days || DAYS, play: true, era: true }),
        Object.assign({ measure, by, over }, extra || {}))}`;
      return frameFromSeries(await cached(path));
    };
    load.series = true;                                  // a saved view of it can be a dashboard tile
    return load;
  }

  function formsFor(f, cfg) {
    const allowed = cfg.forms || ["bars", "line", "pie", "table"];
    const out = Object.keys(cfg.custom || {});
    if (!f) return out;
    const parts = f.keys.length;
    const stackable = f.kind === "count" || parts === 1;
    if (allowed.includes("bars") && (!f.time || stackable)) out.push("bars");
    if (allowed.includes("line") && f.time && f.xs.length >= 2) out.push("line");
    const pieParts = f.time ? parts : f.keys.length;
    if (allowed.includes("pie") && pieParts >= 2 && pieParts <= 6 && f.kind === "count") out.push("pie");
    if (allowed.includes("number")) out.push("number");
    if (allowed.includes("table")) out.push("table");
    return out.filter((x, i) => out.indexOf(x) === i);
  }

  function chartView(cfg) {
    const saved = store.get("fa-chart-" + cfg.id, {}) || {};
    const d = cfg.defaults || {};
    const pick = { form: saved.form || d.form, measure: saved.measure || d.measure || (cfg.measures ? cfg.measures[0][0] : null),
                   by: saved.by || d.by || "none", over: saved.over || d.over || (cfg.overs ? cfg.overs[0] : null) };
    if (cfg.measures && !cfg.measures.some(([k]) => k === pick.measure)) pick.measure = cfg.measures[0][0];
    if (cfg.splits && !cfg.splits.includes(pick.by)) pick.by = d.by || "none";
    if (cfg.overs && !cfg.overs.includes(pick.over)) pick.over = cfg.overs[0];
    const switches = h("div", { class: "switches" });
    const body = h("div", { class: "chart-body" });
    const note = h("p", { class: "small chart-note", hidden: true });
    const view = h("div", { class: "chart-view" }, switches, body, note);
    let frame = null, loading = 0;
    const remember = () => store.set("fa-chart-" + cfg.id, pick);
    const combo = (m, b, o) => `${m}|${b}|${o}`;

    function seg(label, options, current, choose) {
      if (options.length < 2) return null;
      return h("div", { class: "mini-seg", role: "group", "aria-label": label },
        h("span", { class: "mini-lab" }, label),
        options.map(([v, l]) => h("button", { type: "button", "aria-pressed": String(v === current), onclick: () => choose(v) }, l)));
    }
    function drawSwitches() {
      const forms = formsFor(frame, cfg);
      const formOpts = forms.map((x) => [x, (cfg.custom && cfg.custom[x] && cfg.custom[x].label) || FORM_LABEL[x]]);
      let formCtl;
      if (cfg.compact && formOpts.length > 1) {          // a small tile: the forms as one select
        const sel = h("select", { class: "mini-select", "aria-label": "Chart form" }, formOpts.map(([v, l]) => h("option", { value: v, selected: v === pick.form || null }, l)));
        sel.addEventListener("change", () => { pick.form = sel.value; remember(); paint(); });
        formCtl = h("label", { class: "mini-seg" }, h("span", { class: "mini-lab" }, "Form"), sel);
      } else formCtl = seg("Form", formOpts, pick.form, (v) => { pick.form = v; remember(); paint(); });
      const parts = [formCtl];
      if (cfg.measures) parts.push(seg("Measure", cfg.measures.filter(([k]) => !rejected.has(combo(k, "none", pick.over)) || k === pick.measure),
        pick.measure, (v) => { pick.measure = v; remember(); load(); }));
      if (cfg.overs) parts.push(seg("Over", OVERS.filter(([k]) => cfg.overs.includes(k)), pick.over, (v) => { pick.over = v; remember(); load(); }));
      if (cfg.splits) {
        const opts = SPLITS.filter(([k]) => cfg.splits.includes(k) && (k === pick.by || (!rejected.has(combo(pick.measure, k, pick.over))
          && !(frame && frame.answerable && !frame.answerable.includes(k)))));
        if (opts.length > 1) {
          const id = `split-${cfg.id.replace(/[^a-z0-9]/gi, "-")}`;
          const sel = h("select", { class: "mini-select", id, "aria-label": "Split by" },
            opts.map(([k, l]) => h("option", { value: k, selected: k === pick.by || null }, l)));
          sel.addEventListener("change", () => { pick.by = sel.value; remember(); load(); });
          sel.addEventListener("focus", probe);           // the reader is about to choose: ask which splits answer
          sel.addEventListener("pointerenter", probe);
          parts.push(h("label", { class: "mini-seg", for: id }, h("span", { class: "mini-lab" }, "Split"), sel));
        }
      }
      switches.replaceChildren(...parts.filter(Boolean));
      switches.hidden = !switches.children.length;
    }
    function paint() {
      const forms = formsFor(frame, cfg);
      if (!forms.includes(pick.form)) pick.form = forms.includes(cfg.defaults && cfg.defaults.form) ? cfg.defaults.form : forms[0];
      drawSwitches();
      if (!frame) return;
      const empty = cfg.isEmpty ? cfg.isEmpty(frame) : (!frame.custom && frame.keys.every((k) => frame.xs.every((x) => !frame.get(x, k))));
      if (empty && pick.form !== "table") { body.replaceChildren(h("p", { class: "small" }, cfg.emptyText || "Nothing in this range.")); return; }
      const custom = cfg.custom && cfg.custom[pick.form];
      let out;
      if (custom) out = responsive((w, box) => custom.draw(frame, w, box));
      else if (pick.form === "table") out = drawTable(frame);
      else if (pick.form === "number") out = drawNumber(frame);
      else if (pick.form === "pie") out = responsive((w, box) => drawDonut(frame, w, box, cfg.donut || {}));
      else if (pick.form === "line") out = responsive((w, box) => drawLines(frame, w, box, { height: cfg.height || 220, target: cfg.target }));
      else if (frame.time) out = responsive((w, box) => drawTimeBars(frame, w, box, { height: cfg.height || 220 }));
      else out = responsive((w, box) => drawHBars(frame, w, box, { limit: cfg.limit || 12 }));
      body.replaceChildren(out);
      const uf = (frame.unfiltered || []).length && (state.play !== "all" || state.era) ? filterWord() : "";
      const hidden = frame.hidden && pick.form !== "table" ? `${frame.hidden} smaller parts are in the Table.` : "";
      const notes = (frame.notes || []).map((n) => (typeof n === "string" ? n : n && (n.text || n.note || n.title))).filter(Boolean);
      note.textContent = [uf && `Not split by ${uf}: ${frame.unfiltered.join(", ").replace(/_/g, " ")}.`, hidden, ...notes].filter(Boolean).join(" ");
      note.hidden = !note.textContent;
      if (cfg.compact && !note.hidden) { view.title = note.textContent; note.hidden = true; }   // a small tile keeps its room for the chart
    }
    async function load() {
      const ticket = ++loading;
      body.classList.add("loading");
      try {
        const f = await cfg.load({ measure: pick.measure, by: pick.by, over: pick.over });
        if (ticket !== loading) return;
        frame = f;
        body.classList.remove("loading");
        paint();
      } catch (e) {
        if (ticket !== loading) return;
        body.classList.remove("loading");
        if (e.status === 400 && (pick.by !== "none" || (cfg.measures && pick.measure !== cfg.measures[0][0]))) {
          rejected.add(combo(pick.measure, pick.by, pick.over));
          if (pick.by !== "none") pick.by = "none"; else pick.measure = cfg.measures[0][0];
          remember();
          return load();
        }
        frame = null; drawSwitches();
        body.replaceChildren(h("p", { class: "small" }, e.message === "Failed to fetch" ? "The server could not be reached." : e.message));
      }
    }
    // Ask the server, quietly and one at a time, which splits it can answer for this measure; hide the ones it cannot.
    const probed = new Set();
    async function probe() {
      const m = pick.measure, o = pick.over;
      if (!cfg.splits || (frame && frame.answerable) || probed.has(m + "|" + o)) return;
      probed.add(m + "|" + o);
      for (const k of cfg.splits) {
        if (k === "none" || k === pick.by || rejected.has(combo(m, k, o))) continue;
        try { await cfg.load({ measure: m, by: k, over: o }); }
        catch (e) { if (e.status === 400) { rejected.add(combo(m, k, o)); if (m === pick.measure && o === pick.over) drawSwitches(); } else return; }
      }
    }
    view.reload = load;
    view.pick = () => Object.assign({}, pick);
    load();
    return view;
  }

  function chartPanel(cfg) {
    const view = chartView(cfg);
    const views = presetBar(cfg, view);
    const head = h("div", { class: "card-head" }, h("span", { class: "lab" }, cfg.title),
      cfg.note ? h("span", { class: "small" }, cfg.note) : null, cfg.tag ? ufTag() : null, views ? views.bar : null);
    return h("section", { class: "card panel sl" + (cfg.cls ? " " + cfg.cls : "") }, head, views ? views.form : null, view, cfg.after || null);
  }

  // ------------------------------------------------------------------ saved views ("presets")
  // Any chart panel saves its switches (form, measure, split, over) and the page's filters (build, days, play, era)
  // under a name, private or public (Shombith, 2026-11-06); the Views list applies one; the Dashboards page lists them
  // all and makes a series view a tile. A view is its key's own: a private one is never shown to another key.
  const CHART_TITLES = {
    "overview.series": "Matches over time", "overview.freestyle": "The Toybox over time",
    "overview.freestyle.switches": "Vessels switched to in the Toybox", "overview.freestyle.vessels": "Vessels flown in the Toybox",
    "retention.series": "Players and rematches over time", "matches.series": "Matches per day", "matches.element": "By element",
    "players.series": "Active players", "overview.playtype": "Rematch and return by play type", "overview.health": "Match health by build",
  };
  const chartTitle = (id) => CHART_TITLES[id] || String(id || "").replace(/[._]/g, " ");
  const presetable = (cfg) => !cfg.compact && cfg.views !== false && !/^(profile|dash|query)\./.test(cfg.id) && !(state.page === "players" && state.person);
  const presetsFor = (id) => (state.presets || []).filter((p) => p.chart_id === id);
  async function loadPresets() {
    try { const r = await api("/v1/presets"); state.presets = r.presets || []; } catch (e) { state.presets = []; }
  }
  function pageFilters(meta) {
    const f = {};
    if (meta.build) f.build = state.build;
    if (meta.days && !(meta.era && state.era)) f.days = state.days;
    if (meta.play) f.play = state.play;
    if (meta.era && state.era) f.era = state.era;
    return f;
  }
  const settingsText = (st) => [st.measure && (MEASURES[st.measure] || [st.measure])[0], st.by && st.by !== "none" && `by ${((SPLITS.find(([k]) => k === st.by) || [, st.by])[1]).toLowerCase()}`,
    st.over && ({ day: "per day", week: "per week", none: "in total" })[st.over], st.form].filter(Boolean).join(" · ");
  const filtersText = (f) => [f.build, f.era ? (eraById(f.era) || {}).name || f.era : f.days ? `${f.days} d` : null,
    f.play && f.play !== "all" ? GROUP_LABEL[f.play] : null].filter(Boolean).join(" · ");
  function applyPreset(p) {
    const f = p.filters || {};
    if (f.build) state.build = f.build;
    if (f.days) state.days = f.days;
    state.play = f.play || "all";
    state.era = f.era && eraById(f.era) ? f.era : "";
    store.set("fa-chart-" + p.chart_id, Object.assign({}, store.get("fa-chart-" + p.chart_id, {}) || {}, p.settings || {}));
    state.person = null;
    getCache.clear();
    if (state.page !== p.page) location.hash = "#" + p.page; else render();
    window.scrollTo(0, 0);
  }
  function presetBar(cfg, view) {
    if (!presetable(cfg)) return null;
    const mine = presetsFor(cfg.id);
    const status = h("span", { class: "small", "aria-live": "polite" });
    const name = h("input", { class: "field sls compact", type: "text", maxlength: "80", placeholder: "Name this view", "aria-label": "View name", required: true });
    const pub = h("input", { type: "checkbox" });
    const form = h("form", { class: "save-view", hidden: true }, name,
      h("label", { class: "small check-label" }, pub, "Public"),
      h("button", { class: "btn sls default small-btn", type: "submit" }, "Save"), status);
    form.addEventListener("submit", async (e) => {
      e.preventDefault();
      const meta = PAGES[state.page];
      try {
        const made = await api("/v1/presets", { name: name.value.trim(), page: state.page, chart_id: cfg.id, series: !!(cfg.load && cfg.load.series),
          settings: view.pick(), filters: pageFilters(meta), public: pub.checked });
        state.presets = (state.presets || []).filter((x) => x.id !== made.id).concat(made);
        status.className = "small"; status.textContent = `Saved${made.public ? " for everyone" : " for you"}. It is under Views here and in Dashboards.`;
        name.value = "";
        setTimeout(() => { form.hidden = true; status.textContent = ""; redrawViews(); }, 1600);
      } catch (err) { status.className = "small danger"; status.textContent = err.message; }
    });
    const bar = h("span", { class: "views" });
    function redrawViews() {
      const list = presetsFor(cfg.id);
      const items = [];
      if (list.length) {
        const sel = h("select", { class: "mini-select", "aria-label": `Saved views of ${cfg.title}` },
          h("option", { value: "" }, `Views (${list.length})`),
          list.map((p) => h("option", { value: p.id }, `${p.name}${p.public ? " · public" : ""}${p.owner !== state.me ? ` · ${p.owner}` : ""}`)));
        sel.addEventListener("change", () => { const p = list.find((x) => x.id === sel.value); if (p) applyPreset(p); });
        items.push(h("label", { class: "mini-seg" }, h("span", { class: "mini-lab" }, "Views"), sel));
      }
      items.push(h("button", { type: "button", class: "link save-link", "aria-expanded": String(!form.hidden), onclick: () => {
        form.hidden = !form.hidden; if (!form.hidden) name.focus();
      } }, "Save view"));
      bar.replaceChildren(...items);
    }
    redrawViews();
    return { bar, form, mine };
  }
  // A chart over data the page already holds.
  const staticLoad = (make) => async (pick) => make(pick);

  // ------------------------------------------------------------------ shared pieces

  // `series` (0-3) marks a row as one of up to four categories: it takes the theme's domain rim in that slot
  // (Jade, Ruby, Gold, Blue; themes.css --series-1..4). A colour string is used as given. Without it the meter is
  // plain data (the set's Blue).
  function meter(value, series) {
    const v = Math.max(0, Math.min(100, value || 0));
    const fill = series === undefined ? "" : typeof series === "string" ? `;background:${series}` : `;background:var(--series-${(series % 4) + 1})`;
    return h("div", { class: "meter-track" }, h("div", { class: "meter-fill", style: `width:${v}%${fill}` }));
  }

  function table(columns, rows) {
    return h("div", { class: "table-wrap" }, h("table", {},
      h("thead", {}, h("tr", {}, columns.map((c) => h("th", { scope: "col" }, c)))),
      h("tbody", {}, rows)));
  }

  // Which header filters a number ignores, in words ("play type", "era").
  function filterWord() {
    return [state.play !== "all" && "play type", state.era && "era"].filter(Boolean).join(" or ");
  }
  function ufTag() {
    const words = [state.play !== "all" && "all play types", state.era && "all eras"].filter(Boolean);
    return words.length ? h("span", { class: "uf-tag" }, words.join(" · ")) : null;
  }
  // card(title, note, ...body); a title listed in the page's "unfiltered" gets the small "all play types" tag.
  function card(title, note, ...body) {
    return h("section", { class: "card panel sl" },
      h("div", { class: "card-head" }, h("span", { class: "lab" }, title), note ? h("span", { class: "small" }, note) : null),
      ...body);
  }
  // A number is unfiltered when the page lists it, or a part of it ("business" covers "business.crystals_earned").
  const isUnfiltered = (field) => !!field && (state.unfiltered || []).some((u) => u === field || u.startsWith(field + ".") || field.startsWith(u + "."));
  function ucard(field, title, note, ...body) {
    const el = card(title, note, ...body);
    if (isUnfiltered(field)) { const t = ufTag(); if (t) el.firstChild.append(t); }
    return el;
  }

  function empty(text) { return h("div", { class: "empty panel sl" }, h("p", { class: "small" }, text)); }

  function tileRow(list, cls) {
    return h("div", { class: "tiles" + (cls ? " " + cls : "") }, list.filter(Boolean).map(([label, value, note, bad, field]) =>
      h("div", { class: "tile panel sl" + (bad ? " bad" : "") },
        h("span", { class: "lab" }, label), h("span", { class: "value num" }, value),
        note ? h("span", { class: "small" }, note) : null,
        isUnfiltered(field) ? ufTag() : null)));
  }

  // A stacked bar of a play mix [[group, count]...] with the group colours (2px gaps between parts).
  function mixBar(mix, { width = 120, height = 10 } = {}) {
    const total = sum(mix, (m) => m[1]);
    const box = h("div", { class: "mix", style: `width:${width}px;height:${height}px`,
      title: mix.map(([g, n]) => `${GROUP_LABEL[g]} ${fmt(n)}`).join(", ") });
    if (!total) { box.classList.add("none"); return box; }
    for (const [g, n] of mix) if (n) box.append(h("i", { style: `flex:${n};background:${GROUP_COLOUR[g]}` }));
    return box;
  }
  function mixLegend(mix) {
    return h("div", { class: "legend" }, mix.map(([g, n]) => h("span", { class: "legend-item" },
      h("i", { style: `background:${GROUP_COLOUR[g]}` }), h("span", {}, `${GROUP_LABEL[g]} ${fmt(n)}`))));
  }

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

  function playTypeRows(list) {
    return list.map((r) => h("tr", {},
      h("td", {}, playType(r.play_type)), h("td", {}, fmt(r.matches)),
      h("td", { class: r.completed ? "" : "soft" }, r.completed ? pct(r.rematch_pct) : "–"),
      h("td", { class: "meter" }, meter(r.rematch_pct, GROUP_COLOUR[groupOf(r.play_type)])),
      h("td", { class: r.d1_pct === null ? "soft" : "" }, pct(r.d1_pct)),
      h("td", { class: r.d7_pct === null ? "soft" : "" }, pct(r.d7_pct))));
  }
  // The rematch/return by play type rows as a switchable chart (Bars | Table), measure Rematch | D1 | D7 | Matches.
  function playTypePanel(id, title, note, list) {
    return chartPanel({ id, title, note,
      measures: [["rematch_pct", "Rematch %"], ["d1_pct", "D1 %"], ["d7_pct", "D7 %"], ["matches", "Matches"]],
      forms: ["bars", "pie", "table"],
      load: staticLoad(({ measure }) => Object.assign(catFrame(list, "play_type", measure,
        { by: "play_type", kind: measure === "matches" ? "count" : "pct", label: playType, measure }),
        { table: { columns: ["Play type", "Matches", "Rematch", "D1", "D7"], rows: list.map((r) => [playType(r.play_type), fmt(r.matches),
          r.completed ? pct(r.rematch_pct) : "–", pct(r.d1_pct), pct(r.d7_pct)]) } })),
      emptyText: "No matches in this range.",
      after: h("p", { class: "small" }, "D1 and D7 count only players whose first match is at least 7 days old.") });
  }

  // The series chart most pages lead with: any measure, split any way, over days, weeks or in total.
  function seriesPanel(id, title, defaults, extra) {
    return chartPanel(Object.assign({ id, title, note: `${state.build} builds, ${rangeLabel()} · hover a mark`,
      measures: MATCH_MEASURES.map((k) => [k, MEASURES[k][0]]),
      splits: MATCH_SPLITS, overs: ["day", "week", "none"],
      load: seriesLoad(), defaults }, extra || {}));
  }

  // ------------------------------------------------------------------ Overview

  async function overview() {
    const o = await api(`/v1/overview?${filterQuery()}`);
    setUpdated(o.built_ms);
    state.unfiltered = o.unfiltered || [];
    if (o.empty) return [empty("Nothing has been received yet. Point a build at this server.")];
    const t = o.tiles;
    const mk = o.marketing || null, biz = o.business || null;
    const perDay = mk && Array.isArray(mk.new_players_per_day) ? mk.new_players_per_day : null;
    const newPlayers = mk ? (perDay ? sum(perDay, (d) => d.new_players ?? d.players ?? d.value) : mk.new_players_per_day) : null;
    const top = (list, k) => (Array.isArray(list) && list.length ? list.slice().sort((a, b) => (b.players || 0) - (a.players || 0))[0][k] : null);
    const shares = mk && typeof mk.shares === "object" && mk.shares ? sum(Object.values(mk.shares)) : mk && mk.shares;
    const parts = [
      readStrip(o.digest),
      tileRow([
        ["Players", fmt(t.players), rangeLabel(), false, "players"],
        ["Matches", fmt(t.matches), rangeLabel(), false, "matches"],
        ["Finished", pct(t.finished_pct), "of plays sent game_completed", false, "finished_pct"],
        ["Rematch", pct(t.rematch_solo_vs_ai_pct), "solo vs AI, within 10 min", false, "rematch"],
        ["Rows per play", ratio(t.rows_per_play), "should be 1.00", t.rows_per_play > 1.05, "rows_per_play"],
        ["Multi-human", fmt(t.multi_human_matches), "matches with 2+ people", false, "multi_human_matches"],
      ]),
    ];
    if (mk || biz) {
      parts.push(h("div", { class: "grid2" },
        card("Marketing", "who arrives and from where",
          tileRow([
            ["New players", fmt(newPlayers), rangeLabel(), false, "marketing.new_players_per_day"],
            ["Top country", top(mk && mk.by_country, "country") ? country(top(mk.by_country, "country")) : "–", "most players", false, "marketing.by_country"],
            ["Top platform", fmt(top(mk && mk.by_platform, "platform")), "most players", false, "marketing.by_platform"],
            ["Shares", fmt(shares), "share_triggered", false, "marketing.shares"],
            ["Invites sent", fmt(mk && mk.invites_sent), "party invites", false, "marketing.invites_sent"],
          ], "tiles-inner")),
        card("Business", "crystals, counts only",
          tileRow([
            ["Crystals earned", fmt(biz && biz.crystals_earned), rangeLabel(), false, "business.crystals_earned"],
            ["Crystals spent", fmt(biz && biz.crystals_spent), rangeLabel(), false, "business.crystals_spent"],
            ["Whales", fmt(biz && biz.whales_2000), "players holding 2,000+", false, "business.whales_2000"],
            ["Earned per spent", biz && biz.crystals_spent ? ratio(biz.crystals_earned / biz.crystals_spent) : "–", "above 1 means saving"],
          ], "tiles-inner"))));
    }
    parts.push(seriesPanel("overview.series", "Matches over time", { measure: "matches", by: "play_type", over: "day", form: "bars" }));
    parts.push(...await freestyleSection());
    parts.push(h("div", { class: "grid2" },
      playTypePanel("overview.playtype", "Rematch and return by play type", null, o.rematch_by_play_type || []),
      h("div", { class: "page" },
        chartPanel({ id: "overview.health", title: "Match health by build", note: rangeLabel(),
          measures: [["finished_pct", "Finished %"], ["starts", "Starts"], ["rows_per_play", "Rows per play"]],
          forms: ["bars", "pie", "table"], defaults: { form: "table" },
          load: staticLoad(({ measure }) => Object.assign(catFrame(o.match_health_by_build || [], "build", measure,
            { kind: measure === "starts" ? "count" : measure === "rows_per_play" ? "ratio" : "pct", measure, vname: measure.replace(/_/g, " "),
              colours: Object.fromEntries(BUILDS.map(([b], i) => [b, `var(--series-${i + 1})`])) }),
            { table: { columns: ["Build", "Starts", "Finished", "Rows per play"], rows: (o.match_health_by_build || []).map((r) =>
              [r.build, fmt(r.starts), pct(r.finished_pct), ratio(r.rows_per_play)]) } })) }),
        ucard("events_24h", "Events, last 24 hours", state.build,
          (o.events_24h || []).length
            ? table(["Event", "Rows", "Flagged as duplicates"], o.events_24h.map((e) => h("tr", {},
                h("td", {}, e.event), h("td", {}, fmt(e.rows)), h("td", { class: e.near_duplicates ? "bad" : "soft" }, fmt(e.near_duplicates)))))
            : h("p", { class: "small" }, "Nothing in the last day.")))));
    if (mk && (Array.isArray(mk.by_country) || Array.isArray(mk.by_platform))) {
      parts.push(h("div", { class: "grid2" },
        Array.isArray(mk.by_country) ? chartPanel({ id: "overview.country", title: "New players by country", forms: ["bars", "pie", "table"],
          load: staticLoad(() => catFrame(mk.by_country, "country", "players", { by: "country", kname: "Country", vname: "Players" })) }) : null,
        Array.isArray(mk.by_platform) ? chartPanel({ id: "overview.platform", title: "New players by platform", forms: ["bars", "pie", "table"],
          load: staticLoad(() => catFrame(mk.by_platform, "platform", "players", { kname: "Platform", vname: "Players" })) }) : null));
    }
    return parts;
  }

  // ------------------------------------------------------------------ Freestyle (the Toybox)
  // Shombith, 2026-11-06: players who "went inside freestyle/toybox and played vessels". Entries come from the
  // PostHog history; minutes and exact switches from builds after 2026-11-06; older switches are recovered from the
  // identify history (the selected vessel changed between two profile updates; time approximate).

  function freestyleHistory(hi) {
    hi = hi || {};
    const entries = hi.first_imported_entry_day ? `Entries since ${shortDay(hi.first_imported_entry_day)} from the PostHog history`
      : hi.first_entry_day ? `Entries since ${shortDay(hi.first_entry_day)}` : "No Toybox entries yet";
    const minutes = `minutes and vessels from builds after 2026-11-06${hi.first_minutes_day ? ` (first on ${shortDay(hi.first_minutes_day)})` : " (none received yet)"}`;
    const recovered = hi.first_recovered_switch_day ? `; older switches recovered from the identify history since ${shortDay(hi.first_recovered_switch_day)}, at approximate times` : "";
    return `${entries}; ${minutes}${recovered}.`;
  }

  async function freestyleSection() {
    let f;
    try { f = await cached(`/v1/freestyle?${filterQuery(PAGES.overview)}`); }
    catch (e) { return [h("h2", { class: "h3 section-title" }, "Freestyle (Toybox)"), empty(e.message)]; }
    const t = f.tiles || {};
    const tag = state.play !== "all";                    // the Toybox is not a match: the Play filter does not reach it
    if (tag && !state.unfiltered.includes("freestyle")) state.unfiltered = state.unfiltered.concat("freestyle");
    const pairs = f.switch_pairs || [];
    return [
      h("h2", { class: "h3 section-title" }, "Freestyle (Toybox)"),
      h("p", { class: "small" }, freestyleHistory(f.history)),
      tileRow([
        ["Entries", fmt(t.entries), rangeLabel(), false, "freestyle"],
        ["Players", fmt(t.players), "who entered the Toybox", false, "freestyle"],
        ["First of a session", fmt(t.first_entries), "entries straight after launch"],
        ["Minutes flown", t.stretches ? fmt(t.minutes) : "–", t.stretches ? `${fmt(t.stretches)} stretches` : "from builds after 6 Nov"],
        ["Vessels a stretch", t.avg_vessels_flown ? ratio(t.avg_vessels_flown) : "–", "distinct vessels flown"],
        ["Vessel switches", fmt((t.switches || 0) + (t.recovered_switches || 0)), `${fmt(t.switches)} exact · ${fmt(t.recovered_switches)} recovered`],
      ]),
      h("div", { class: "grid2" },
        chartPanel({ id: "overview.freestyle", title: "The Toybox over time", note: `${state.build} builds, ${rangeLabel()}`, tag,
          measures: [["freestyle_entries", "Entries"], ["freestyle_players", "Players"], ["freestyle_minutes", "Minutes"]],
          splits: ["none", "vessel_class", "era", "build_kind"], overs: ["day", "week", "none"], load: seriesLoad(),
          defaults: { measure: "freestyle_entries", by: "none", over: "day", form: "bars" }, emptyText: "Nobody entered the Toybox in this range." }),
        chartPanel({ id: "overview.freestyle.switches", title: "Vessels switched to in the Toybox", note: "the vessel they changed into", tag,
          measures: [["freestyle_switches", "Switches"]], splits: ["vessel_class", "from_vessel_class", "switch_source", "none", "era", "build_kind"],
          overs: ["none", "week", "day"], load: seriesLoad(), defaults: { measure: "freestyle_switches", by: "vessel_class", over: "none", form: "bars" },
          emptyText: "No vessel switches in this range. Exact switches come from builds after 6 Nov; recovered ones from the identify history (builds since 12 Oct)." })),
      h("div", { class: "grid2" },
        chartPanel({ id: "overview.freestyle.vessels", title: "Vessels flown in the Toybox", note: "per vessel, in this range", tag,
          measures: [["minutes", "Minutes"], ["switches_in", "Switched to"], ["stretches", "Ended in"], ["entries", "Entered in"]],
          forms: ["bars", "pie", "table"], emptyText: "No vessels named yet: builds after 6 Nov name them.",
          load: staticLoad(({ measure }) => {
            const rows = (f.vessels || []).map((v) => Object.assign({}, v, { switches_in: (v.switches_to || 0) + (v.recovered_to || 0) }))
              .filter((v) => v.vessel_class !== "unknown" || measure === "entries").sort((a, b) => (b[measure] || 0) - (a[measure] || 0));
            return Object.assign(catFrame(rows.filter((v) => (v[measure] || 0) > 0), "vessel_class", measure, { by: "vessel_class", kname: "Vessel", vname: measure.replace(/_/g, " ") }),
              { table: { columns: ["Vessel", "Minutes", "Stretches", "Entered in", "Switched to", "Switched from"], rows: rows.map((v) =>
                [v.vessel_class === "unknown" ? "Not named (older builds)" : v.vessel_class, fmt(v.minutes), fmt(v.stretches), fmt(v.entries),
                 fmt(v.switches_in), fmt((v.switches_from || 0) + (v.recovered_from || 0))]) } });
          }) }),
        ucard("freestyle", "Switches, from → to", "the most common pairs in this range",
          pairs.length ? table(["From", "To", "Switches", "Recovered"], pairs.map((x) => h("tr", {},
            h("td", {}, x.from_vessel === "unknown" ? "Not named" : x.from_vessel), h("td", {}, x.to_vessel),
            h("td", { class: "num" }, fmt(x.switches)), h("td", { class: "num soft" }, fmt(x.recovered)))))
            : h("p", { class: "small" }, "No switches in this range."))),
    ];
  }

  // ------------------------------------------------------------------ Retention

  async function retention() {
    const r = await api(`/v1/retention?${filterQuery()}`);
    setUpdated(r.built_ms);
    state.unfiltered = r.unfiltered || [];
    if (r.empty) return [empty("Nothing has been received yet.")];
    return [
      seriesPanel("retention.series", "Players and rematches over time", { measure: "players", by: "play_type", over: "week", form: "line" }),
      playTypePanel("retention.playtype", "By play type", "rematch: another match within 10 minutes of finishing one", r.by_play_type || []),
      card("First-play cohorts", "players grouped by the day of their first match; 7+ days old only",
        (r.cohorts || []).length
          ? table(["First day", "Play type", "Players", "Back next day", "Back within 7 days"], r.cohorts.slice().reverse().map((c) => h("tr", {},
              h("td", {}, c.first_day), h("td", {}, playType(c.play_type)), h("td", {}, fmt(c.players)),
              h("td", {}, `${fmt(c.returned_d1)} (${pct(c.players ? Math.round(100 * c.returned_d1 / c.players) : null)})`),
              h("td", {}, `${fmt(c.returned_d7)} (${pct(c.players ? Math.round(100 * c.returned_d7 / c.players) : null)})`))))
          : h("p", { class: "small" }, "No cohort is 7 days old yet.")),
    ];
  }

  // ------------------------------------------------------------------ Matches

  // A list of rows by category (mode, vessel, intensity) as a switchable chart; its Table keeps every column.
  function byPanel(id, title, rows, keyField, keyName, measures, columns, cells) {
    return chartPanel({ id, title, forms: ["bars", "pie", "table"], measures, defaults: { form: "bars" },
      load: staticLoad(({ measure }) => Object.assign(catFrame(rows, keyField, measure, {
        kind: /pct$/.test(measure) ? "pct" : /_s$/.test(measure) ? "avg" : "count", measure,
        label: (k) => (k === null || k === undefined || k === "null" ? "Unknown" : keyField === "intensity" ? `Intensity ${k}` : String(k)),
        vname: (measures.find((m) => m[0] === measure) || [, measure])[1] }),
        { table: { columns, rows: rows.map(cells) } })) });
  }

  // Matches by element: the match's own (builds from 2026-11-06) or its mode's genre (mode_elements.json, so older
  // matches and the PostHog history have one too). A neutral hue in steps: elements have no colour.
  function elementPanel(m) {
    const rows = m.by_element || [];
    const second = m.element_secondary || [];
    const measures = [["matches", "Matches"], ["plays", "Plays"], ["finished_pct", "Finished %"], ["win_pct", "Won %"]];
    return chartPanel({ id: "matches.element", title: "By element", note: "the mode's genre: Charge, Mass, Space, Time", forms: ["bars", "pie", "table"], measures,
      load: staticLoad(({ measure }) => Object.assign(catFrame(rows, "element", measure, { by: "element", measure: measure === "plays" ? null : measure,
        kind: /pct$/.test(measure) ? "pct" : "count", kname: "Element", vname: (measures.find((x) => x[0] === measure) || [, measure])[1] }),
        { table: { columns: ["Element", "Matches", "Plays", "Finished", "Won", "Sent by the build", "From the mode"], rows: rows.map((r) =>
          [elementLabel(r.element), fmt(r.matches), fmt(r.plays), pct(r.finished_pct), pct(r.win_pct), fmt(r.sent_by_build), fmt(r.from_mode_table)]) } })),
      emptyText: "No matches in this range.",
      after: h("p", { class: "small" }, "A two-element mode counts under its first" + (second.length ? `; as a second element: ${second.map((x) => `${x.element} ${fmt(x.matches)} matches`).join(", ")}` : "") +
        ". Builds from 2026-11-06 send the element; older matches take their mode's genre from the game's ModeGenre. No element: a mode with no genre, or an old mode name.") });
  }

  async function matches() {
    const m = await api(`/v1/matches?${filterQuery()}`);
    setUpdated(m.built_ms);
    state.unfiltered = m.unfiltered || [];
    if (m.empty) return [empty("Nothing has been received yet.")];
    const plays = [["plays", "Plays"], ["finished_pct", "Finished %"], ["win_pct", "Won %"], ["avg_flight_s", "Avg flight s"]];
    return [
      seriesPanel("matches.series", "Matches per day", { measure: "matches", by: "none", over: "day", form: "bars" }),
      elementPanel(m),
      h("div", { class: "grid2" },
        byPanel("matches.mode", "By mode", m.by_mode || [], "game_mode", "Mode", plays,
          ["Mode", "Element", "Plays", "Finished", "Won", "Avg flight"], (r) => [r.game_mode || "Unknown",
            r.element ? elementLabel(r.element) + (r.element_secondary ? ` + ${r.element_secondary}` : "") : "–",
            fmt(r.plays), pct(r.finished_pct), pct(r.win_pct), r.avg_flight_s ? `${r.avg_flight_s} s` : "–"]),
        byPanel("matches.intensity", "By intensity", m.by_intensity || [], "intensity", "Intensity", plays.slice(0, 2),
          ["Intensity", "Plays", "Finished"], (r) => [r.intensity ?? "–", fmt(r.plays), pct(r.finished_pct)])),
      h("div", { class: "grid2" },
        byPanel("matches.vessel", "By vessel", m.by_vessel || [], "vessel_class", "Vessel", plays,
          ["Vessel", "Plays", "Finished", "Won", "Avg flight"], (r) => [r.vessel_class || "Unknown", fmt(r.plays), pct(r.finished_pct), pct(r.win_pct), r.avg_flight_s ? `${r.avg_flight_s} s` : "–"]),
        ucard("signals", "What players say about modes", "favourites, repeated fails, shares",
          (m.signals || []).length
            ? table(["Mode", "Favourited", "Unfavourited", "Repeated fails", "Shared"], m.signals.map((r) => h("tr", {},
                h("td", {}, r.game_mode), h("td", {}, fmt(r.favorited)), h("td", { class: "soft" }, fmt(r.unfavorited)),
                h("td", { class: r.fails ? "bad" : "soft" }, fmt(r.fails)), h("td", {}, fmt(r.shares)))))
            : h("p", { class: "small" }, "No favourites, fails or shares in this range."),
          m.pause ? h("p", { class: "small" }, `Paused or away from the controls: ${seconds(m.pause.avg_pause_s)} a match on average, ` +
            `${pct(m.pause.paused_over_30s_pct)} of matches over 30 s. Average match: ${seconds(m.pause.avg_duration_s)}.`) : null)),
    ];
  }

  // ------------------------------------------------------------------ Players

  const PF_STORE = "fa-players-filters";
  const PF_DEFAULT = { q: "", countries: [], platforms: [], active: "any", minMatches: 0, multi: "any", crystals: "any", sort: "last_ms", dir: "desc" };
  const ACTIVE = [["any", "Any"], ["1", "Last 1 d"], ["7", "Last 7 d"], ["14", "Last 14 d"], ["30", "Last 30 d"], ["lapsed14", "Lapsed 14 d+"]];
  const MIN_MATCHES = [[0, "Any"], [1, "≥ 1"], [5, "≥ 5"], [10, "≥ 10"], [25, "≥ 25"], [50, "≥ 50"]];
  const MULTI = [["any", "Any"], ["yes", "Multiplayer"], ["no", "Never multiplayer"]];
  const CRYSTALS = [["any", "Any"], ["0", "None"], ["1-999", "1–999"], ["1000-1999", "1,000–1,999"], ["2000+", "2,000+"]];
  let peopleList = null;                                 // the last list, for the filter bar's re-draws

  function filtersNow() { return Object.assign({}, PF_DEFAULT, store.get(PF_STORE, {}) || {}); }
  function saveFilters(f) { store.set(PF_STORE, f); }

  function applyFilters(list, f) {
    const now = Date.now(), q = (f.q || "").trim().toLowerCase();
    return list.filter((p) => {
      if (q && !p.distinct_id.toLowerCase().includes(q) && !(p.display_name || "").toLowerCase().includes(q)) return false;
      if (f.countries.length && !(p.countries || []).some((c) => f.countries.includes(c))) return false;
      if (f.platforms.length && !(p.platforms || []).some((c) => f.platforms.includes(c))) return false;
      if (f.active === "lapsed14" ? now - p.last_ms < 14 * DAY_MS : f.active !== "any" && now - p.last_ms > Number(f.active) * DAY_MS) return false;
      if ((p.matches || 0) < Number(f.minMatches || 0)) return false;
      if (f.multi === "yes" && !(p.multi > 0)) return false;
      if (f.multi === "no" && p.multi > 0) return false;
      const c = p.crystal_balance || 0;
      if (f.crystals === "0" && c !== 0) return false;
      if (f.crystals === "1-999" && !(c >= 1 && c <= 999)) return false;
      if (f.crystals === "1000-1999" && !(c >= 1000 && c <= 1999)) return false;
      if (f.crystals === "2000+" && c < 2000) return false;
      return true;
    });
  }

  const lifespanDays = (p) => Math.max(1, Math.round((p.last_ms - p.first_ms) / DAY_MS) + 1);
  const COLUMNS = [
    ["display_name", "Player", (p) => (p.display_name || p.distinct_id).toLowerCase()],
    ["country", "Country", (p) => (p.countries || [])[0] || "~"],
    ["platform", "Platform", (p) => (p.platforms || [])[0] || "~"],
    ["lifespan", "Lifespan", (p) => p.last_ms - p.first_ms],
    ["mix", "Play mix", (p) => (p.matches ? (p.multi || 0) / p.matches : -1)],
    ["matches", "Matches", (p) => p.matches || 0],
    ["finished", "Finished", (p) => (p.matches ? (p.finished || 0) / p.matches : -1)],
    ["won", "Won", (p) => p.won || 0],
    ["freestyle_entries", "Toybox", (p) => p.freestyle_entries || 0],
    ["crystal_balance", "Crystals", (p) => p.crystal_balance ?? -1],
    ["last_ms", "Last seen", (p) => p.last_ms || 0],
  ];
  function sorted(list, f) {
    const col = COLUMNS.find((c) => c[0] === f.sort) || COLUMNS[COLUMNS.length - 1];
    const dir = f.dir === "asc" ? 1 : -1;
    return list.slice().sort((a, b) => { const x = col[2](a), y = col[2](b); return (x < y ? -1 : x > y ? 1 : 0) * dir; });
  }

  // A player's lifespan within the page's window: first to last seen, a tick on every day they played.
  function lifespanBar(p, [from, to], width = 150) {
    const span = Math.max(1, to - from), xAt = (ms) => Math.max(0, Math.min(width, (ms - from) / span * width));
    const svg = s("svg", { width, height: 14, viewBox: `0 0 ${width} 14`, class: "lifespan", role: "img",
      "aria-label": `${dayOf(p.first_ms)} to ${dayOf(p.last_ms)}, ${fmt(p.days_active)} days played` },
      s("title", {}, `${dayOf(p.first_ms)} to ${dayOf(p.last_ms)} · ${fmt(p.days_active)} days played`));
    svg.append(s("line", { class: "grid", x1: 0, x2: width, y1: 7, y2: 7 }));
    const x0 = xAt(p.first_ms), x1 = Math.max(xAt(p.last_ms), x0 + 2);
    svg.append(s("rect", { x: x0, y: 3, width: x1 - x0, height: 8, style: "fill:var(--data);opacity:.45" }));
    for (const d of p.active_days || []) {
      const x = xAt(isoMs(d) + DAY_MS / 2);
      svg.append(s("rect", { x: x - 1, y: 1, width: 2, height: 12, style: "fill:var(--fs-text)" }));
    }
    return svg;
  }

  function csvDownload(name, columns, rows) {
    const cell = (v) => {
      let t = v === null || v === undefined ? "" : String(v);
      if (/^[=+\-@\t\r]/.test(t)) t = "'" + t;          // a spreadsheet must not run a cell as a formula
      return /[",\n\r]/.test(t) ? `"${t.replace(/"/g, '""')}"` : t;
    };
    const text = [columns, ...rows].map((r) => r.map(cell).join(",")).join("\r\n");
    const url = URL.createObjectURL(new Blob(["﻿" + text], { type: "text/csv;charset=utf-8" }));
    const a = h("a", { href: url, download: name });
    document.body.append(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 2000);
  }

  // A filter chip that opens a small menu: one choice (radio) or several (checkboxes).
  function chipMenu(label, options, current, multi, onChange) {
    const valueText = () => {
      if (!multi) return (options.find((o) => String(o[0]) === String(current)) || options[0])[1];
      if (!current.length) return "Any";
      const names = current.map((v) => (options.find((o) => o[0] === v) || [v, v])[2] || v);
      return names.length > 2 ? `${names.slice(0, 2).join(", ")} +${names.length - 2}` : names.join(", ");
    };
    const active = multi ? current.length > 0 : String(current) !== String(options[0][0]);
    const btn = h("button", { type: "button", class: "fchip sls" + (active ? " on" : ""), "aria-haspopup": "true", "aria-expanded": "false" },
      h("span", { class: "fchip-lab" }, label), h("span", { class: "fchip-val" }, valueText()));
    const menu = h("div", { class: "menu panel", role: "menu", hidden: true });
    const wrap = h("div", { class: "fchip-wrap" }, btn, menu);
    const close = (e) => { if (e && wrap.contains(e.target)) return; menu.hidden = true; btn.setAttribute("aria-expanded", "false"); document.removeEventListener("pointerdown", close); };
    btn.addEventListener("click", () => {
      if (!menu.hidden) return close();
      menu.replaceChildren(...options.map(([v, l]) => {
        const on = multi ? current.includes(v) : String(v) === String(current);
        return h("button", { type: "button", class: "menu-item", role: multi ? "menuitemcheckbox" : "menuitemradio", "aria-checked": String(on),
          onclick: () => {
            if (multi) { const next = on ? current.filter((x) => x !== v) : current.concat(v); onChange(next); }
            else { close(); onChange(v); }
          } }, h("span", { class: "check", "aria-hidden": "true" }, on ? "■" : "□"), l);
      }), multi && current.length ? h("button", { type: "button", class: "menu-item soft", onclick: () => { close(); onChange([]); } }, "Clear") : "");
      menu.hidden = false; btn.setAttribute("aria-expanded", "true");
      setTimeout(() => document.addEventListener("pointerdown", close), 0);
    });
    return wrap;
  }

  async function players() {
    if (state.person) return profile(state.person);
    const parts = [];
    let segments = [];
    if (!state.profiles) {
      parts.push(card("Every player", null, h("p", { class: "small" },
        "Player profiles (ids, display names, countries, everything they did) open with an admin key or a key named in the server's profile_readers.")));
    } else {
      const [list, seg] = await Promise.all([
        api(`/v1/people?${filterQuery()}`),
        api("/v1/segments").catch(() => ({ segments: [] })),
      ]);
      peopleList = list;
      state.unfiltered = list.unfiltered || [];
      segments = seg.segments || [];
      parts.push(...peopleSection(list, segments));
    }
    parts.push(...await audienceSection());
    return parts;
  }

  function peopleSection(list, segments) {
    const people = list.people || [];
    const countries = [...new Set(people.flatMap((p) => p.countries || []))].sort().map((c) => [c, country(c), c]);
    const platforms = [...new Set(people.flatMap((p) => p.platforms || []))].sort().map((c) => [c, c, c]);
    const bar = h("div", { class: "filterbar" });
    const segBar = h("div", { class: "filterbar" });
    const summary = h("div", { class: "summary" });
    const tableBox = h("section", { class: "card panel sl" });
    const win = list.since ? [isoMs(list.since), list.until ? isoMs(list.until) + DAY_MS : Date.now()] : windowRange();

    function redraw(focusSearch) {
      const f = filtersNow();
      const set = (k, v) => { const next = Object.assign(filtersNow(), { [k]: v }); saveFilters(next); redraw(); };
      const search = h("input", { class: "field sls", type: "search", id: "people-q", value: f.q, placeholder: "Search name or player id", "aria-label": "Search players" });
      let timer = null;
      search.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(() => { saveFilters(Object.assign(filtersNow(), { q: search.value })); redraw(true); }, 180); });
      bar.replaceChildren(h("div", { class: "search-box" }, search),
        chipMenu("Country", countries, f.countries, true, (v) => set("countries", v)),
        chipMenu("Platform", platforms, f.platforms, true, (v) => set("platforms", v)),
        chipMenu("Active", ACTIVE, f.active, false, (v) => set("active", v)),
        chipMenu("Matches", MIN_MATCHES, f.minMatches, false, (v) => set("minMatches", v)),
        chipMenu("Has played", MULTI, f.multi, false, (v) => set("multi", v)),
        chipMenu("Crystals", CRYSTALS, f.crystals, false, (v) => set("crystals", v)));
      if (focusSearch) { search.focus(); search.setSelectionRange(search.value.length, search.value.length); }

      const shown = sorted(applyFilters(people, f), f);
      // saved segments, save, export
      const segOptions = segments.map((sg) => [sg.id, sg.name + (sg.shared ? " · shared" : "")]);
      const segChip = segments.length ? chipMenu("Segment", [["", "None"]].concat(segOptions), "", false, (id) => {
        const sg = segments.find((x) => x.id === id);
        if (sg) { saveFilters(Object.assign({}, PF_DEFAULT, sg.filters || {})); redraw(); }
      }) : null;
      const saveBox = h("form", { class: "save-seg", hidden: true },
        h("input", { class: "field sls", type: "text", maxlength: "60", placeholder: "Segment name", "aria-label": "Segment name", required: true }),
        state.admin ? h("label", { class: "small check-label" }, h("input", { type: "checkbox" }), "Share with readers") : null,
        h("button", { class: "btn sls default small-btn", type: "submit" }, "Save"),
        h("span", { class: "small", "aria-live": "polite" }));
      saveBox.addEventListener("submit", async (e) => {
        e.preventDefault();
        const name = saveBox.querySelector("input[type=text]").value.trim();
        const shared = !!(saveBox.querySelector("input[type=checkbox]") || {}).checked;
        const out = saveBox.querySelector("span[aria-live]");
        try {
          const made = await api("/v1/segments", { name, filters: filtersNow(), shared });
          segments.push(made.segment || made); out.textContent = "Saved."; setTimeout(redraw, 600);
        } catch (err) { out.textContent = err.message; }
      });
      const delBtn = segments.length ? chipMenu("Delete segment", [["", "Pick one"]].concat(segOptions), "", false, async (id) => {
        if (!id) return;
        try { await api(`/v1/segments?id=${encodeURIComponent(id)}`, null, "DELETE"); segments = segments.filter((x) => x.id !== id); redraw(); }
        catch (err) { alertLine(segBar, err.message); }
      }) : null;
      segBar.replaceChildren(...[segChip,
        h("button", { type: "button", class: "btn sls ghost small-btn", onclick: () => { saveBox.hidden = !saveBox.hidden; if (!saveBox.hidden) saveBox.querySelector("input").focus(); } }, "Save segment"),
        h("button", { type: "button", class: "btn sls default small-btn", onclick: () => exportPeople(shown) }, "Export CSV"),
        JSON.stringify(f) !== JSON.stringify(Object.assign({}, PF_DEFAULT, { sort: f.sort, dir: f.dir })) ? h("button", { type: "button", class: "link", onclick: () => { saveFilters(Object.assign({}, PF_DEFAULT, { sort: f.sort, dir: f.dir })); redraw(); } }, "Clear filters") : null,
        delBtn, saveBox].filter(Boolean));

      // summary tiles and the set's play mix
      const mix = [["solo_vs_ai", sum(shown, (p) => p.solo_vs_ai)], ["solo", sum(shown, (p) => p.solo)], ["multi", sum(shown, (p) => p.multi)]];
      const lapsed = shown.filter((p) => Date.now() - p.last_ms > 14 * DAY_MS).length;
      summary.replaceChildren(
        tileRow([
          ["Players", fmt(shown.length), `match the filters, of ${fmt(people.length)}`],
          ["Matches", fmt(sum(shown, (p) => p.matches)), "by these players"],
          ["Median lifespan", shown.length ? `${fmt(median(shown.map(lifespanDays)))} d` : "–", "first to last seen"],
          ["Lapsed", fmt(lapsed), "no play in 14 days", lapsed > 0],
        ], "tiles-4"),
        h("div", { class: "tile panel sl mix-tile" }, h("span", { class: "lab" }, "Play mix of these players"),
          responsive((w) => mixBar(mix, { width: w, height: 16 })), mixLegend(mix)));

      // the table
      const head = h("tr", {}, COLUMNS.map(([k, label]) => {
        const on = f.sort === k;
        const b = h("button", { type: "button", class: "sort" + (on ? " on" : ""), onclick: () => {
          const now = filtersNow();
          saveFilters(Object.assign(now, { sort: k, dir: now.sort === k && now.dir === "desc" ? "asc" : "desc" })); redraw();
        } }, label, on ? h("span", { class: "arrow", "aria-hidden": "true" }, f.dir === "asc" ? " ▲" : " ▼") : null);
        return h("th", { scope: "col", "aria-sort": on ? (f.dir === "asc" ? "ascending" : "descending") : null }, b);
      }));
      const open = (id) => { state.person = id; render(); window.scrollTo(0, 0); };
      const rows = shown.map((p) => h("tr", {},
        h("td", {}, h("button", { type: "button", class: "link name", onclick: () => open(p.distinct_id) }, p.display_name || "(no name)"),
          h("div", { class: "small id" }, p.distinct_id)),
        h("td", {}, firstOf((p.countries || []).map(country))), h("td", {}, firstOf(p.platforms || [])),
        h("td", {}, lifespanBar(p, win, 88)),
        h("td", {}, mixBar([["solo_vs_ai", p.solo_vs_ai || 0], ["solo", p.solo || 0], ["multi", p.multi || 0]], { width: 72 })),
        h("td", {}, fmt(p.matches)), h("td", {}, p.matches ? pct(Math.round(100 * (p.finished || 0) / p.matches)) : "–"),
        h("td", {}, fmt(p.won)),
        h("td", { title: p.freestyle_seconds ? `${seconds(p.freestyle_seconds)} flown there (builds after 6 Nov)` : "Toybox entries" }, fmt(p.freestyle_entries || 0)),
        h("td", {}, fmt(p.crystal_balance)), h("td", {}, dayOf(p.last_ms))));
      tableBox.replaceChildren(
        h("div", { class: "card-head" }, h("span", { class: "lab" }, "Every player"),
          h("span", { class: "small" }, `${fmt(shown.length)} of ${fmt(list.total ?? people.length)} · ${rangeLabel()} · sorted by ${(COLUMNS.find((c) => c[0] === f.sort) || COLUMNS[COLUMNS.length - 1])[1].toLowerCase()} · click any column to sort`)),
        shown.length ? h("div", { class: "table-wrap" }, h("table", { class: "people" }, h("thead", {}, head), h("tbody", {}, rows)))
          : h("p", { class: "small" }, "Nobody matches these filters."),
        list.total > people.length ? h("p", { class: "small" }, `The server sends the newest ${fmt(people.length)}; narrow the range or era to see the rest.`) : "");
    }
    redraw();
    return [bar, segBar, summary, tableBox];
  }

  function alertLine(where, text) {
    const p = h("p", { class: "small danger" }, text);
    where.append(p); setTimeout(() => p.remove(), 4000);
  }

  function exportPeople(list) {
    csvDownload(`froglet-players-${state.build}-${todayIso()}.csv`,
      ["player_id", "display_name", "countries", "platforms", "first_seen", "last_seen", "days_active", "matches", "finished", "won",
       "solo_vs_ai", "solo", "multi", "toybox_entries", "toybox_seconds", "crystal_balance", "app_version"],
      list.map((p) => [p.distinct_id, p.display_name, (p.countries || []).join(" "), (p.platforms || []).join(" "), when(p.first_ms), when(p.last_ms),
        p.days_active, p.matches, p.finished, p.won, p.solo_vs_ai, p.solo, p.multi, p.freestyle_entries, p.freestyle_seconds, p.crystal_balance, p.app_version]));
  }

  async function audienceSection() {
    let a;
    try { a = await api(`/v1/audience?${filterQuery(PAGES.ftue)}`); } catch (e) { return [empty(e.message)]; }
    if (a.empty) return state.profiles ? [] : [empty("Nothing has been received yet.")];
    const first = a.funnel.length ? a.funnel[0].players : 0;
    return [
      h("h2", { class: "h3 section-title" }, "Who plays"),
      seriesPanel("players.series", "Active players", { measure: "players", by: "none", over: "day", form: "bars" },
        { measures: [["players", "Players"], ["new_players", "New players"]] }),
      h("div", { class: "grid2" },
        chartPanel({ id: "players.country", title: "Where they play from", note: "country from the connection, the address itself is not kept",
          forms: ["bars", "pie", "table"], tag: state.play !== "all" || !!state.era,
          load: staticLoad(() => catFrame(a.countries, "country", "players", { by: "country", kname: "Country", vname: "Players" })),
          after: h("p", { class: "small" }, "IP Geolocation by ", h("a", { href: "https://db-ip.com", target: "_blank", rel: "noopener", style: "color:inherit" }, "DB-IP")) }),
        chartPanel({ id: "players.platform", title: "What they play on", forms: ["bars", "pie", "table"], tag: state.play !== "all" || !!state.era,
          load: staticLoad(() => catFrame(a.platforms, "platform", "players", { kname: "Platform", vname: "Players" })) })),
      h("div", { class: "grid2" },
        chartPanel({ id: "players.funnel", title: "First-day funnel", note: "players whose first event was in this range, and how far they have got",
          forms: ["bars", "table"], tag: state.play !== "all" || !!state.era, emptyText: "No new players in this range.",
          load: staticLoad(() => Object.assign(catFrame(a.funnel, "step", "players", { kname: "Step", vname: "Players" }),
            { tips: Object.fromEntries(a.funnel.map((x) => [x.step, `${pct(first ? Math.round(1000 * x.players / first) / 10 : null)} of new`])) })) }),
        card("Launch to menu", "seconds from opening the game to the main menu",
          a.launches.length ? table(["Platform", "Launches", "Typical", "Slowest 10%"], a.launches.map((l) => h("tr", {},
            h("td", {}, l.platform), h("td", {}, fmt(l.launches)), h("td", {}, seconds(l.p50_seconds)),
            h("td", { class: l.p90_seconds > 30 ? "bad" : "" }, seconds(l.p90_seconds))))) : h("p", { class: "small" }, "No launches yet."),
          a.session_ends.length ? barGroup("How sessions ended", barList(a.session_ends.map((e) => [e.reason, e.count]))) : null)),
    ];
  }

  // ------------------------------------------------------------------ Profile: one long page, everything shown

  function kv(pairs) {
    return h("div", { class: "kv" }, pairs.filter((p) => p[1] !== null && p[1] !== undefined && p[1] !== "").map(([k, v]) =>
      h("div", { class: "kv-row" }, h("span", { class: "lab" }, k), h("span", { class: "small", style: "color:var(--fs-text)" }, v))));
  }
  // A count breakdown as ranked bars, largest first: one row per item (label, bar, count), at most `top` rows and the
  // rest folded into "Other (n)". Replaces every "A (3), B (2), C (1)" comma list (Shombith, 2026-11-06: "Anywhere where
  // there is data like this REMOVE and make it better").
  function barList(obj, labelOf, opts) {
    const o = Object.assign({ top: 8, empty: "–" }, opts || {});
    let rows = (Array.isArray(obj) ? obj : Object.entries(obj || {})).map(([k, n]) => [k, Number(n) || 0]).filter(([, n]) => n > 0)
      .sort((a, b) => b[1] - a[1]);
    if (!rows.length) return h("p", { class: "small" }, o.empty);
    if (rows.length > o.top) {
      const rest = rows.slice(o.top - 1);
      rows = rows.slice(0, o.top - 1).concat([[`Other (${rest.length})`, rest.reduce((t, r) => t + r[1], 0), true]]);
    }
    const max = Math.max(...rows.map((r) => r[1]));
    const total = rows.reduce((t, r) => t + r[1], 0);
    return h("div", { class: "barlist" }, ...rows.map(([k, n, other]) => {
      const label = other ? k : (labelOf ? labelOf(k) : String(k).replace(/_/g, " "));
      return h("div", { class: "barlist-row", title: `${label}: ${fmt(n)} (${Math.round(100 * n / total)}%)` },
        h("span", { class: "barlist-label" }, label),
        h("span", { class: "barlist-track" }, h("span", { class: "barlist-bar" + (other ? " other" : ""), style: `width:${Math.max(2, 100 * n / max)}%` })),
        h("span", { class: "barlist-n num" }, fmt(n)));
    }));
  }
  function firstOf(list) {
    if (!list.length) return "–";
    return h("span", { title: list.join(" · ") }, list[0], list.length > 1 ? h("span", { class: "small more" }, ` +${list.length - 1}`) : null);
  }
  function barGroup(title, body) {
    return h("div", { class: "bargroup" }, h("span", { class: "lab" }, title), body);
  }
  function statRow(items) {
    return h("div", { class: "statrow" }, ...items.filter(([, v]) => v !== null && v !== undefined && v !== "").map(([k, v]) =>
      h("div", { class: "stat" }, h("span", { class: "lab" }, k), h("span", { class: "stat-v num" }, v))));
  }
  const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
  const isWin = (w) => w === true || w === "true" || w === 1;

  // Hour x weekday: 7 rows of 24 cells, one hue light to dark (the set's data colour mixed into the ground).
  function drawHeatmap(grid, W, box) {
    const max = Math.max(1, ...grid.flat());
    const L = 34, T = 4, cw = Math.max(5, Math.min(24, Math.floor((W - L) / 24))), ch = Math.max(16, Math.min(22, cw)), gap = cw > 9 ? 2 : 1;
    const H = T + 7 * ch + 18, Wd = L + 24 * cw;
    const svg = s("svg", { width: Wd, height: H, viewBox: `0 0 ${Wd} ${H}`, role: "img", "aria-label": "Matches by hour and weekday" });
    const tip = tipFor(box);
    grid.forEach((row, d) => {
      svg.append(s("text", { class: "axis", x: L - 6, y: T + d * ch + ch / 2 + 4, "text-anchor": "end" }, WEEKDAYS[d]));
      row.forEach((n, hr) => {
        const share = n ? 18 + 82 * n / max : 0;
        const r = s("rect", { x: L + hr * cw, y: T + d * ch, width: cw - gap, height: ch - 2,
          style: n ? `fill:color-mix(in srgb, var(--data) ${share.toFixed(0)}%, var(--fs-ground))` : "fill:var(--fs-surface)" });
        r.addEventListener("mouseenter", () => tip(`${WEEKDAYS[d]} ${String(hr).padStart(2, "0")}:00 UTC: ${fmt(n)} matches`, L + hr * cw + cw / 2, T + d * ch));
        r.addEventListener("mouseleave", () => tip(null));
        svg.append(r);
      });
    });
    for (const hr of [0, 6, 12, 18]) svg.append(s("text", { class: "axis", x: L + hr * cw, y: H - 4 }, String(hr).padStart(2, "0")));
    return svg;
  }
  function busiest(grid) {
    const days = grid.map((r) => sum(r)), hours = Array.from({ length: 24 }, (_, i) => sum(grid, (r) => r[i]));
    if (!sum(days)) return "No matches yet.";
    const d = days.indexOf(Math.max(...days));
    let best = 0, at = 0;
    for (let i = 0; i < 24; i++) { const w = hours[i] + hours[(i + 1) % 24] + hours[(i + 2) % 24] + hours[(i + 3) % 24]; if (w > best) { best = w; at = i; } }
    return `Busiest: ${["Mondays", "Tuesdays", "Wednesdays", "Thursdays", "Fridays", "Saturdays", "Sundays"][d]}, and ${String(at).padStart(2, "0")}:00 to ${String((at + 4) % 24).padStart(2, "0")}:00 UTC.`;
  }

  // The match timeline: a lane per mode (the five most played, then Other), a bar per match from its start for its
  // length; filled = finished, outline = not; a square above = a win; the colour is the play type.
  function drawGantt(list, W, box) {
    const counts = {};
    list.forEach((m) => { counts[m.game_mode || "Unknown"] = (counts[m.game_mode || "Unknown"] || 0) + 1; });
    const lanes = Object.entries(counts).sort((a, b) => b[1] - a[1]).map((x) => x[0]);
    const named = lanes.slice(0, lanes.length > 6 ? 5 : 6), laneOf = (m) => (named.includes(m.game_mode || "Unknown") ? m.game_mode || "Unknown" : "Other");
    if (lanes.length > named.length) named.push("Other");
    const L = Math.min(130, Math.max(80, W * 0.16)), R = 10, T = 8, lane = 30, B = 24;
    const H = T + named.length * lane + B;
    const t0 = Math.min(...list.map((m) => m.ts_ms)), t1 = Math.max(...list.map((m) => m.ts_ms + 1000 * (m.duration_seconds || m.flight_time_seconds || 60)));
    const span = Math.max(DAY_MS, t1 - t0), plotW = W - L - R;
    const xAt = (ms) => L + plotW * (ms - t0) / span;
    const svg = s("svg", { width: W, height: H, viewBox: `0 0 ${W} ${H}`, role: "img", "aria-label": "Match timeline" });
    named.forEach((n, i) => svg.append(
      s("text", { class: "axis label", x: 0, y: T + i * lane + lane / 2 + 4 }, n.length > 16 ? n.slice(0, 15) + "…" : n),
      s("line", { class: "grid faint", x1: L, x2: W - R, y1: T + (i + 1) * lane, y2: T + (i + 1) * lane })));
    const ticks = Math.max(2, Math.min(6, Math.floor(plotW / 110)));
    for (let i = 0; i < ticks; i++) {
      const ms = t0 + span * i / (ticks - 1), x = xAt(ms);
      svg.append(s("line", { class: "grid faint", x1: x, x2: x, y1: T, y2: T + named.length * lane }),
        s("text", { class: "axis", x, y: H - 6, "text-anchor": i === 0 ? "start" : i === ticks - 1 ? "end" : "middle" }, shortDay(dayOf(ms))));
    }
    const tip = tipFor(box);
    for (const m of list) {
      const li = named.indexOf(laneOf(m));
      const colour = GROUP_COLOUR[groupOf(m.play_type)] || "var(--data)";
      const x = xAt(m.ts_ms), w = Math.max(3, plotW * 1000 * (m.duration_seconds || m.flight_time_seconds || 60) / span);
      const y = T + li * lane + 9, bh = 13;
      const g = s("g", { class: "gmark" });
      g.append(m.finished ? s("rect", { x, y, width: w, height: bh, style: `fill:${colour}` })
        : s("rect", { x: x + 0.75, y: y + 0.75, width: Math.max(1.5, w - 1.5), height: bh - 1.5, style: `fill:none;stroke:${colour};stroke-width:1.5` }));
      if (isWin(m.won)) g.append(s("rect", { x: x + w / 2 - 2.5, y: y - 7, width: 5, height: 5, style: "fill:var(--fs-text)" }));
      g.append(s("rect", { class: "hit", x: x - 3, y: y - 8, width: w + 6, height: bh + 10 }));
      const text = `${when(m.ts_ms)} · ${m.game_mode || "–"} · ${m.vessel_class || "–"} · ${playType(m.play_type)} · ` +
        `${m.finished ? "finished" : "not finished"}${isWin(m.won) ? ", won" : ""} · ${seconds(m.duration_seconds ?? m.flight_time_seconds)}`;
      g.addEventListener("mouseenter", () => tip(text, x + w / 2, y - 8));
      g.addEventListener("mouseleave", () => tip(null));
      svg.append(g);
    }
    return [svg, h("div", { class: "legend" }, GROUPS.map(([k, l, c]) => h("span", { class: "legend-item" }, h("i", { style: `background:${c}` }), h("span", {}, l))),
      h("span", { class: "legend-item" }, h("i", { class: "outline" }), h("span", {}, "outline: not finished")),
      h("span", { class: "legend-item" }, h("i", { class: "win" }), h("span", {}, "square: a win")))];
  }

  // The profile's Toybox block: visits, minutes, vessels flown there, and every vessel switch in order.
  function freestyleProfile(fs) {
    if (!fs) return null;
    const list = fs.switches || [];
    const note = "Visits go back to the PostHog history. Minutes and exact switches come from builds after 2026-11-06; " +
      "switches marked approx. are recovered from the identify history (builds since 2026-10-12): the selected vessel changed between two profile updates, at about that time.";
    return h("div", { class: "grid2" },
      card("Freestyle (Toybox)", fs.last_ms ? `first ${dayOf(fs.first_ms)} · last ${when(fs.last_ms)}` : "never entered",
        statRow([["Visits", fmt(fs.entries)], ["First of a session", fmt(fs.first_entries)],
          ["Minutes flown", fs.stretches ? fmt(fs.minutes) : "–"], ["Stretches", fmt(fs.stretches)], ["Switches", fmt(fs.switch_count)]]),
        h("div", { class: "bargroups" },
          barGroup("Vessels flown there", barList(fs.vessels_flown, null, { empty: "No vessel named yet (builds after 6 Nov name it)." })),
          barGroup("Switched to", barList(fs.switched_to, null, { empty: "No switches." }))),
        h("p", { class: "small" }, note)),
      card("Vessel switches in the Toybox", list.length ? `${fmt(list.length)} in order, oldest first${fs.recovered_switches ? ` · ${fmt(fs.recovered_switches)} recovered (approx.)` : ""}` : null,
        list.length ? h("div", { class: "scroll-y" }, table(["When", "From", "", "To", "Into the stretch"], list.map((x) => h("tr", {},
          h("td", { class: "nowrap" }, when(x.ts_ms), x.approx ? h("span", { class: "tag approx", title: "Recovered from the identify history: when the profile was sent, not the click" }, "approx.") : null),
          h("td", {}, x.from || "–"), h("td", { class: "soft", "aria-hidden": "true" }, "→"), h("td", {}, x.to || "–"),
          h("td", { class: x.seconds_in_freestyle === null || x.seconds_in_freestyle === undefined ? "soft" : "" }, seconds(x.seconds_in_freestyle))))))
          : h("p", { class: "small" }, "No switches. Exact switches exist only from builds after 2026-11-06, recovered ones from builds since 2026-10-12; the PostHog history before that has entries, not vessels.")));
  }

  async function profile(id) {
    const p = await api(`/v1/person?id=${encodeURIComponent(id)}`);
    const m = p.matches, e = p.economy, w = p.where, act = p.activity;
    const all = m.recent || [];
    const list = state.play === "all" ? all : all.filter((x) => groupOf(x.play_type) === state.play);
    const filtered = state.play !== "all";
    const finished = filtered ? list.filter((x) => x.finished).length : m.finished;
    const total = filtered ? list.length : m.total;
    const won = filtered ? list.filter((x) => isWin(x.won)).length : m.won;
    const flight = filtered ? sum(list, (x) => x.flight_time_seconds) : m.flight_seconds;
    const done = list.filter((x) => x.finished);
    const rematch = done.length ? 100 * done.filter((x) => x.rematched).length / done.length : null;
    const mix = GROUPS.map(([g]) => [g, all.filter((x) => groupOf(x.play_type) === g).length]);
    const mostly = mix.slice().sort((a, b) => b[1] - a[1])[0];
    const person = Object.entries(p.person || {}).filter(([k]) => k !== "display_name");
    const back = h("button", { type: "button", class: "btn sls ghost", onclick: () => { state.person = null; render(); } }, "All players");
    const exportBtn = h("button", { type: "button", class: "btn sls default", onclick: () => csvDownload(`froglet-player-${id.slice(0, 12)}-matches.csv`,
      ["when", "mode", "intensity", "vessel", "play_type", "humans", "ai", "finished", "won", "flight_s", "duration_s", "rematched"],
      all.map((x) => [when(x.ts_ms), x.game_mode, x.intensity, x.vessel_class, x.play_type, x.humans, x.ai, x.finished, x.won, x.flight_time_seconds, x.duration_seconds, x.rematched])) }, "Export matches");
    const chips = [country(w.latest_country), Object.keys(w.platforms || {})[0], `first seen ${dayOf(p.first_ms)}`, `last seen ${when(p.last_ms)}`,
      p.lifespan_days !== undefined ? `lifespan ${fmt(p.lifespan_days)} d` : null,
      mostly && mostly[1] ? `mostly ${GROUP_LABEL[mostly[0]].toLowerCase()}` : null].filter(Boolean);
    const heat = act.by_hour_weekday;
    const balancePts = (e.recent || []).filter((x) => x.balance !== null && x.balance !== undefined).slice().reverse();
    const filterNote = filtered ? `${GROUP_LABEL[state.play]} only, from the latest ${fmt(all.length)} matches` : null;

    return [
      h("div", { class: "profile-head" },
        h("div", { class: "profile-name" }, h("span", { class: "lab" }, "Players / profile"),
          h("div", { class: "row" }, h("h2", { class: "h1" }, p.display_name || "(no display name)"), h("span", { class: "small id" }, p.player_id)),
          h("div", { class: "chips-row" }, chips.map((c) => h("span", { class: "tag" }, c)))),
        h("div", { class: "row" }, back, exportBtn)),
      filterNote ? h("p", { class: "small" }, `Play filter: ${filterNote}. Everything else on the page is every match.`) : null,
      tileRow([
        ["Matches", fmt(total), `${fmt(act.days_active)} active days`],
        ["Finished", total ? pct(Math.round(100 * finished / total)) : "–", `${fmt(finished)} of ${fmt(total)}`],
        ["Won", total ? pct(Math.round(100 * won / total)) : "–", `${fmt(won)} ${won === 1 ? "match" : "matches"}`],
        ["At the controls", seconds(flight), total ? `${seconds(flight / total)} a match` : null],
        ["Rematch", pct(rematch === null ? null : Math.round(rematch)), "within 10 min of finishing"],
        ["Crystals", fmt(e.balance), `${fmt(e.earned)} earned · ${fmt(e.spent)} spent`],
      ]),
      h("div", { class: "grid4" },
        chartPanel({ id: "profile.mix", title: "How they play", note: "share of matches", forms: ["pie", "bars", "table"], defaults: { form: "pie" },
          donut: { centre: "matches" },
          load: staticLoad(() => catFrame(mix.map(([g, n]) => ({ g, n })), "g", "n", { by: "play_type", measure: "matches", kname: "Play type", vname: "Matches" })) }),
        chartPanel({ id: "profile.modes", title: "Modes", note: "matches, most first", forms: ["bars", "pie", "table"], limit: 9,
          load: staticLoad(() => catFrame(Object.entries(m.by_mode || {}).sort((a, b) => b[1] - a[1]).map(([k, n]) => ({ k, n })), "k", "n", { kname: "Mode", vname: "Matches" })) }),
        chartPanel({ id: "profile.vessels", title: "Vessels", note: "matches, most first", forms: ["bars", "pie", "table"], limit: 9,
          load: staticLoad(() => catFrame(Object.entries(m.by_vessel || {}).sort((a, b) => b[1] - a[1]).map(([k, n]) => ({ k, n })), "k", "n", { kname: "Vessel", vname: "Matches" })) }),
        heat ? chartPanel({ id: "profile.heat", title: "When they play", note: "hour of day × weekday, UTC", forms: [],
          custom: { heatmap: { label: "Heatmap", draw: (f, W, box) => drawHeatmap(heat, W, box) },
                    table: { label: "Table", draw: () => table(["Day"].concat(Array.from({ length: 24 }, (_, i) => String(i).padStart(2, "0"))),
                      heat.map((r, d) => h("tr", {}, h("td", {}, WEEKDAYS[d]), r.map((n) => h("td", { class: n ? "" : "soft" }, n))))) } },
          isEmpty: () => false, load: staticLoad(() => makeFrame({ custom: true })), after: h("p", { class: "small" }, busiest(heat)) })
          : card("When they play", null, h("p", { class: "small" }, "The server has not sent the hour × weekday counts yet."))),
      h("div", { class: "grid-wide" },
        chartPanel({ id: "profile.timeline", title: "Match timeline", note: `${list.length ? `${dayOf(Math.min(...list.map((x) => x.ts_ms)))} to ${dayOf(Math.max(...list.map((x) => x.ts_ms)))}` : ""} · one lane per mode · hover a bar`,
          forms: [], isEmpty: () => !list.length, emptyText: "No matches.",
          custom: { gantt: { label: "Gantt", draw: (f, W, box) => drawGantt(list, W, box) },
                    perday: { label: "Per day", draw: (f, W, box) => {
                      const by = {}; list.forEach((x) => { const d = dayOf(x.ts_ms); by[d] = (by[d] || 0) + 1; });
                      const days = Object.keys(by).sort();
                      return drawTimeBars(timeFrame(days.map((d) => ({ d, n: by[d] })), "d", "n", { measure: "matches" }), W, box, { height: 180 });
                    } } },
          load: staticLoad(() => makeFrame({ custom: true })) }),
        h("div", { class: "page" },
          chartPanel({ id: "profile.balance", title: "Crystal balance", note: "after each change", forms: ["line", "bars", "table"], defaults: { form: "line" },
            emptyText: "No crystal activity.", height: 170,
            load: staticLoad(() => Object.assign(timeFrame(balancePts.map((x, i) => ({ x: i, v: x.balance })), "x", "v", { measure: null, xname: "When" }),
              { xlabel: (i) => (balancePts[i] ? dayOf(balancePts[i].ts_ms) : ""), label: () => "Balance",
                tips: Object.fromEntries(balancePts.map((x, i) => [i, `${x.event.replace(/_/g, " ")} ${fmt(x.amount)} ${x.source || ""}`])) })) }),
          chartPanel({ id: "profile.sources", title: "Earned by source", forms: ["bars", "pie", "table"], emptyText: "Nothing earned yet.",
            load: staticLoad(() => catFrame((e.by_source || []).slice().sort((a, b) => b.earned - a.earned), "source", "earned", { kname: "Source", vname: "Crystals" })) }))),
      freestyleProfile(p.freestyle),
      h("div", { class: "grid2" },
        card("Where and on what", "events from each, most first",
          statRow([["Devices (installs)", fmt(Object.keys(w.installs || {}).length)], ["Launch to menu, typical", seconds(act.launch_to_menu_p50)]]),
          h("div", { class: "bargroups" },
            barGroup("Countries", barList(w.countries, country)), barGroup("Platforms", barList(w.platforms)),
            barGroup("App versions", barList(w.app_versions)), barGroup("Builds", barList(w.builds)),
            barGroup("How sessions ended", barList(act.session_ends, null, { empty: "No session ends." })))),
        card("Profile the game sent", "the latest person properties", person.length
          ? kv(person.map(([k, v]) => [k.replace(/_/g, " "), typeof v === "object" ? JSON.stringify(v) : String(v)]))
          : h("p", { class: "small" }, "None yet."))),
      chartPanel({ id: "profile.activity", title: "Activity per day", note: "hover a bar", forms: ["bars", "line", "table"],
        measures: [["events", "Events"], ["matches", "Matches"]], emptyText: "No activity.", height: 170,
        load: staticLoad(({ measure }) => timeFrame(act.per_day || [], "day", measure, { measure })) }),
      h("div", { class: "grid2" },
        card("Unlocks", `${(e.unlocks || []).length}`, (e.unlocks || []).length
          ? table(["When", "Kind", "What"], e.unlocks.map((u) => h("tr", {}, h("td", {}, when(u.ts_ms)), h("td", {}, u.kind), h("td", {}, u.name))))
          : h("p", { class: "small" }, "Nothing unlocked.")),
        card("Friends and parties", "who with: open a player's profile", Object.keys(p.social || {}).length
          ? table(["What", "Times", "With"], Object.entries(p.social).sort((a, b) => b[1].count - a[1].count).map(([k, v]) => h("tr", {},
              h("td", {}, k.replace(/_/g, " ")), h("td", { class: "num" }, fmt(v.count)),
              h("td", {}, v.with.length ? h("span", { class: "chips" }, ...v.with.slice(0, 6).map((other) =>
                // friends and parties name players (open their profile); shares and favourites name a mode (plain)
                /^(friend|party)_/.test(k)
                  ? h("button", { type: "button", class: "chip-link", title: other, onclick: () => { state.person = other; render(); window.scrollTo(0, 0); } },
                      other.length > 14 ? other.slice(0, 12) + "…" : other)
                  : h("span", { class: "tag" }, other)), v.with.length > 6 ? h("span", { class: "small" }, `+${v.with.length - 6}`) : null) : "–"))))
          : h("p", { class: "small" }, "No friends or parties yet."))),
      card("Every match", `latest ${Math.min(list.length, 100)} of ${fmt(total)}${filterNote ? " · " + filterNote : ""}`, list.length
        ? table(["When", "Mode", "Int.", "Vessel", "Play type", "Humans / AI", "Finished", "Won", "Flight", "Length", "Rematched"],
            list.map((x) => h("tr", {}, h("td", {}, when(x.ts_ms)), h("td", {}, x.game_mode || "–"), h("td", {}, x.intensity ?? "–"),
              h("td", {}, x.vessel_class || "–"), h("td", {}, playType(x.play_type)), h("td", {}, `${x.humans ?? "–"} / ${x.ai ?? "–"}`),
              h("td", { class: x.finished ? "" : "bad" }, x.finished ? "yes" : "no"),
              h("td", {}, x.won === null || x.won === undefined ? "–" : isWin(x.won) ? "yes" : "no"),
              h("td", {}, seconds(x.flight_time_seconds)), h("td", {}, seconds(x.duration_seconds)), h("td", {}, x.rematched ? "yes" : "–"))))
        : h("p", { class: "small" }, "No matches.")),
      card("Crystals and transactions", `${fmt(e.earned)} earned, ${fmt(e.spent)} spent · latest ${(e.recent || []).length}`, (e.recent || []).length
        ? table(["When", "What", "Amount", "Source", "Balance"], e.recent.map((x) => h("tr", {}, h("td", {}, when(x.ts_ms)),
            h("td", {}, x.event.replace(/_/g, " ")), h("td", {}, fmt(x.amount)), h("td", {}, x.source || "–"), h("td", {}, fmt(x.balance)))))
        : h("p", { class: "small" }, "No crystal activity.")),
      h("div", { class: "grid2" },
        card("FTUE", `${p.ftue.length} steps`, p.ftue.length ? table(["When", "Phase", "Step", "After launch"], p.ftue.map((f) => h("tr", {},
          h("td", {}, when(f.ts_ms)), h("td", {}, f.phase_index ?? "–"), h("td", {}, f.step), h("td", {}, seconds(f.seconds_since_launch)))))
          : h("p", { class: "small" }, "No FTUE steps recorded.")),
        card("Settings", "the latest value of each", Object.keys(p.settings || {}).length
          ? table(["Setting", "Value", "Changed"], Object.entries(p.settings).sort((a, b) => b[1].ts_ms - a[1].ts_ms).map(([k, v]) => h("tr", {},
              h("td", {}, k.replace(/_/g, " ")), h("td", {}, String(v.value)), h("td", {}, dayOf(v.ts_ms)))))
          : h("p", { class: "small" }, "No settings changed."))),
      card("Event timeline", `latest ${p.recent_events.length} of ${fmt(act.events)} events · ${fmt(act.flagged_duplicates)} flagged as duplicates`,
        table(["When", "Event", "Build", "Platform", "Country", "Detail"], p.recent_events.map((x) => h("tr", {}, h("td", {}, when(x.ts_ms)),
          h("td", {}, x.event), h("td", {}, x.build), h("td", {}, x.platform || "–"), h("td", {}, x.country || "–"),
          h("td", { class: "soft" }, x.detail || ""))))),
      card("This player's data", null, h("p", { class: "small" },
        "Everything above is kept only on our server and is never sent to Claude. To delete it, use Erasure with this player id.")),
    ].filter(Boolean);
  }

  // ------------------------------------------------------------------ Cohorts and lifespans

  const openPlayer = (id) => { state.person = id; if (state.page === "players") render(); else location.hash = "#players"; window.scrollTo(0, 0); };

  // Every player's lifespan as a Gantt on one calendar: a bar first to last seen in the colour of how they mostly play,
  // ticks under it on the days they played, faded when lapsed (no play in 14 days). Names open the profile.
  function drawLifespans(list, W, box) {
    const L = Math.min(170, Math.max(110, W * 0.18)), R = 10, T = 6, row = 26, B = 22, chars = Math.floor((L - 10) / 8.2);
    const H = T + list.length * row + B;
    const t0 = Math.min(...list.map((p) => isoMs(p.first_day))), t1 = Math.max(Date.now(), ...list.map((p) => isoMs(p.last_day) + DAY_MS));
    const plotW = W - L - R, xAt = (ms) => L + plotW * (ms - t0) / Math.max(DAY_MS, t1 - t0);
    const svg = s("svg", { width: W, height: H, viewBox: `0 0 ${W} ${H}`, role: "img", "aria-label": "Player lifespans" });
    // month lines
    const d = new Date(t0); d.setUTCDate(1); d.setUTCMonth(d.getUTCMonth() + 1);
    for (; d.getTime() < t1; d.setUTCMonth(d.getUTCMonth() + 1)) {
      const x = xAt(d.getTime());
      svg.append(s("line", { class: "grid faint", x1: x, x2: x, y1: T, y2: H - B }),
        s("text", { class: "axis", x, y: H - 6, "text-anchor": "middle" }, `${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]}`));
    }
    const tip = tipFor(box);
    const lapsedBefore = Date.now() - 14 * DAY_MS;
    list.forEach((p, i) => {
      const y = T + i * row, lapsed = isoMs(p.last_day) + DAY_MS < lapsedBefore;
      const name = p.display_name || p.distinct_id.slice(0, 10);
      const label = s("text", { class: "axis label link-text" + (lapsed ? " soft" : ""), x: 0, y: y + 14, tabindex: "0", role: "link" },
        name.length > chars ? name.slice(0, chars - 1) + "…" : name);
      label.addEventListener("click", () => openPlayer(p.distinct_id));
      label.addEventListener("keydown", (ev) => { if (ev.key === "Enter") openPlayer(p.distinct_id); });
      const x0 = xAt(isoMs(p.first_day)), x1 = Math.max(x0 + 3, xAt(isoMs(p.last_day) + DAY_MS));
      const colour = GROUP_COLOUR[p.mostly] || "var(--data)";
      const g = s("g", {}, s("rect", { x: x0, y: y + 4, width: x1 - x0, height: 11, style: `fill:${colour};opacity:${lapsed ? 0.35 : 0.9}` }));
      for (const day of p.active_days || []) { const x = xAt(isoMs(day) + DAY_MS / 2); g.append(s("rect", { x: x - 0.75, y: y + 17, width: 1.5, height: 4, style: "fill:var(--fs-text-soft)" })); }
      g.append(s("rect", { class: "hit", x: x0, y, width: x1 - x0, height: row }));
      g.addEventListener("mouseenter", () => tip(`${name}: ${p.first_day} to ${p.last_day}, ${fmt((p.active_days || []).length)} days played, mostly ${(GROUP_LABEL[p.mostly] || "–").toLowerCase()}${lapsed ? ", lapsed" : ""}`, (x0 + x1) / 2, y));
      g.addEventListener("mouseleave", () => tip(null));
      svg.append(label, g);
    });
    return [svg, h("div", { class: "legend" }, GROUPS.map(([k, l, c]) => h("span", { class: "legend-item" }, h("i", { style: `background:${c}` }), h("span", {}, "Mostly " + (k === "solo_vs_ai" ? l : l.toLowerCase())))),
      h("span", { class: "legend-item" }, h("i", { style: "background:var(--data);opacity:.35" }), h("span", {}, "faded: no play in 14 days")))];
  }

  function returnHeatmap(weekly) {
    const weeks = Math.max(0, ...weekly.map((c) => (c.weeks || []).length));
    const cellStyle = (v) => {
      if (v === null || v === undefined) return "";
      const share = 10 + 0.9 * Math.max(0, Math.min(100, v));
      return `background:color-mix(in srgb, var(--data) ${share.toFixed(0)}%, var(--fs-ground));color:${share > 58 ? "var(--fs-ground)" : "var(--fs-text)"}`;
    };
    return table(["Cohort", "N"].concat(Array.from({ length: weeks }, (_, i) => `W${i}`)), weekly.map((c) => h("tr", {},
      h("td", {}, shortDay(c.cohort)), h("td", { class: "soft" }, fmt(c.players)),
      Array.from({ length: weeks }, (_, i) => {
        const v = (c.weeks || [])[i];
        return h("td", { class: "heat", style: cellStyle(v), title: v === null || v === undefined ? "" : `${shortDay(c.cohort)} cohort, week ${i}: ${pct(v)} came back` },
          v === null || v === undefined ? "" : pct(Math.round(v)));
      }))));
  }

  async function cohorts() {
    const c = await api(`/v1/cohorts?${filterQuery()}`);
    setUpdated(c.built_ms);
    state.unfiltered = c.unfiltered || [];
    const t = c.tiles || {};
    const weekly = c.weekly || [];
    const lifespans = c.lifespans;
    const [showAll, setShowAll] = [state.allLifespans, (v) => { state.allLifespans = v; render(); }];
    const shownSpans = lifespans ? (showAll ? lifespans : lifespans.slice(-60)) : null;
    return [
      tileRow([
        ["Players", fmt(t.players), rangeLabel()],
        ["Median lifespan", t.median_lifespan_days === null || t.median_lifespan_days === undefined ? "–" : `${fmt(t.median_lifespan_days)} d`, "first to last seen"],
        ["D1 return", pct(t.d1_pct), "of new players"],
        ["D7 return", pct(t.d7_pct), "cohorts 7+ days old"],
        ["Lapsed", fmt(t.lapsed_14d), "no play in 14 days", t.lapsed_14d > 0],
      ], "tiles-5"),
      h("div", { class: lifespans ? "grid-cohorts" : "page" },
        lifespans
          ? chartPanel({ id: "cohorts.lifespans", title: "Every player's lifespan", note: `sorted by first seen · click a name for the profile${lifespans.length > 60 && !showAll ? ` · the newest 60 of ${fmt(lifespans.length)}` : ""}`,
              forms: [], isEmpty: () => !shownSpans.length, emptyText: "Nobody in this range.",
              custom: { gantt: { label: "Gantt", draw: (f, W, box) => drawLifespans(shownSpans, W, box) },
                        table: { label: "Table", draw: () => table(["Player", "First", "Last", "Days played", "Mostly"], shownSpans.slice().reverse().map((p) => h("tr", {},
                          h("td", {}, h("button", { type: "button", class: "link name", onclick: () => openPlayer(p.distinct_id) }, p.display_name || p.distinct_id)),
                          h("td", {}, p.first_day), h("td", {}, p.last_day), h("td", {}, fmt((p.active_days || []).length)), h("td", {}, GROUP_LABEL[p.mostly] || "–")))) } },
              load: staticLoad(() => makeFrame({ custom: true })),
              after: lifespans.length > 60 ? h("button", { type: "button", class: "link", onclick: () => setShowAll(!showAll) }, showAll ? "Show the newest 60" : `Show all ${fmt(lifespans.length)}`) : null })
          : ucard("lifespans", "Every player's lifespan", null, h("p", { class: "small" },
              "Lifespans name players, so they open with an admin key or a key named in the server's profile_readers. The return heatmap and the tiles are totals and need neither.")),
        chartPanel({ id: "cohorts.weekly", title: "Return by weekly cohort", note: "the darker, the more came back", forms: [],
          isEmpty: () => !weekly.length, emptyText: "No cohort yet.",
          custom: { heatmap: { label: "Heatmap", draw: () => returnHeatmap(weekly) },
                    line: { label: "Line", draw: (f, W, box) => {
                      const weeks = Math.max(0, ...weekly.map((x) => (x.weeks || []).length));
                      const v = new Map(), keys = weekly.slice(-4).map((x) => x.cohort);
                      for (const x of weekly.slice(-4)) (x.weeks || []).forEach((n, i) => v.set(`W${i}` + SEP + x.cohort, n));
                      return drawLines(makeFrame({ time: true, xs: Array.from({ length: weeks }, (_, i) => `W${i}`), keys, v, kind: "pct", label: (k) => `${shortDay(k)} cohort`, xlabel: (x) => x }), W, box, { height: 240 });
                    } } },
          load: staticLoad(() => makeFrame({ custom: true })),
          after: h("p", { class: "small" }, "Share of each week's new players who played again in each later week. The Line shows the four newest cohorts.") })),
      seriesPanel("cohorts.new", "New players", { measure: "new_players", by: "none", over: "week", form: "bars" },
        { measures: [["new_players", "New players"], ["players", "Players"]] }),
    ];
  }

  // ------------------------------------------------------------------ Eras: the plan chart, the weeks, compare, edit

  function drawPlan(data, W, box) {
    const eras = data.eras || [], weekly = data.weekly || [], milestones = data.milestones || [];
    const narrow = W < 640;
    const L = narrow ? 112 : 190, R = 12, T = 34, lane = narrow ? 40 : 44, msRow = 40, barsH = 110, B = 22;
    const from = Math.min(Date.UTC(2026, 7, 1), ...eras.map((e) => isoMs(e.from)));
    const to = Math.max(Date.UTC(2027, 3, 30), ...eras.map((e) => isoMs(e.to) + DAY_MS));
    const plotW = W - L - R, xAt = (ms) => L + plotW * (ms - from) / (to - from);
    const yMs = T + eras.length * lane + 6, yBars = yMs + msRow + 16, H = yBars + barsH + B;
    const svg = s("svg", { width: W, height: H, viewBox: `0 0 ${W} ${H}`, role: "img", "aria-label": "The plan: eras on a calendar" });
    const tip = tipFor(box);
    for (const d = new Date(from); d.getTime() <= to; d.setUTCMonth(d.getUTCMonth() + 1)) {
      const x = xAt(d.getTime());
      svg.append(s("line", { class: "grid faint", x1: x, x2: x, y1: T - 4, y2: yBars + barsH }));
      if (!narrow || d.getUTCMonth() % 2 === 0) svg.append(s("text", { class: "axis", x: x + 4, y: 12 }, MONTHS[d.getUTCMonth()]));
      if (d.getUTCMonth() === 0 || d.getTime() === from) svg.append(s("text", { class: "axis tiny", x: x + 4, y: 25 }, String(d.getUTCFullYear())));
    }
    const today = Date.now();
    eras.forEach((e, i) => {
      const y = T + i * lane, c = eraColour(e), a = isoMs(e.from), b = isoMs(e.to) + DAY_MS;
      svg.append(s("text", { class: "axis label strong", x: 0, y: y + 14 }, e.name.length > (narrow ? 15 : 28) ? e.name.slice(0, narrow ? 14 : 27) + "…" : e.name),
        s("text", { class: "axis tiny", x: 0, y: y + 28 }, `${(e.kind === "planned" ? "planned" : e.source || "").toUpperCase()}${narrow ? "" : ` · ${e.from.slice(5)} to ${e.to.slice(5)}`}`));
      const g = s("g", {});
      const bx = xAt(a), bw = Math.max(3, xAt(b) - bx), by = y + 4, bh = 18;
      if (e.kind === "planned") g.append(s("rect", { x: bx + 1, y: by + 1, width: bw - 2, height: bh - 2, style: `fill:none;stroke:${c};stroke-width:1.5;stroke-dasharray:5 4` }));
      else {
        const split = Math.max(a, Math.min(b, today));
        if (split > a) g.append(s("rect", { x: bx, y: by, width: Math.max(2, xAt(split) - bx), height: bh, style: `fill:${c}` }));
        if (b > split) g.append(s("rect", { x: xAt(split) + 0.75, y: by + 0.75, width: Math.max(1, xAt(b) - xAt(split) - 1.5), height: bh - 1.5, style: `fill:none;stroke:${c};stroke-width:1.5` }));
      }
      g.append(s("rect", { class: "hit", x: bx, y: by - 4, width: bw, height: bh + 8 }));
      const counts = [e.events !== undefined && `${fmt(e.events)} events`, e.players !== undefined && `${fmt(e.players)} players`, e.matches !== undefined && `${fmt(e.matches)} matches`].filter(Boolean).join(", ");
      g.addEventListener("mouseenter", () => tip(`${e.name}: ${e.from} to ${e.to} · ${e.kind === "planned" ? "planned" : e.source}${counts ? " · " + counts : ""}`, bx + bw / 2, by));
      g.addEventListener("mouseleave", () => tip(null));
      svg.append(g);
    });
    const ends = [-1e9, -1e9];                           // milestone labels take the first of two rows they fit on
    milestones.slice().sort((a, b) => (a.day < b.day ? -1 : 1)).forEach((mst) => {
      const x = xAt(isoMs(mst.day));
      const width = 10 + 7.4 * mst.name.length;
      const rowAt = x >= ends[0] + 8 ? 0 : x >= ends[1] + 8 ? 1 : (ends[0] <= ends[1] ? 0 : 1);
      ends[rowAt] = x + width;
      const y = yMs + 8 + rowAt * 18;
      const dmd = s("path", { d: `M${x} ${y - 6}L${x + 6} ${y}L${x} ${y + 6}L${x - 6} ${y}Z`, style: "fill:var(--fs-text)" });
      svg.append(dmd, s("text", { class: "axis label", x: x + 10, y: y + 4, "text-anchor": x + width > W - R ? "end" : "start", dx: x + width > W - R ? -20 : 0 },
        narrow && mst.name.length > 14 ? mst.name.slice(0, 13) + "…" : mst.name));
      dmd.addEventListener("mouseenter", () => tip(`${mst.name}: ${mst.day}`, x, y - 6)); dmd.addEventListener("mouseleave", () => tip(null));
    });
    // the weeks' events, coloured by era
    svg.append(s("text", { class: "axis label strong", x: 0, y: yBars + 14 }, "Events a week"), s("text", { class: "axis tiny", x: 0, y: yBars + 28 }, "COLOURED BY ERA"));
    const max = niceMax(Math.max(1, ...weekly.map((w) => w.events || 0)));
    const wk = plotW * 7 * DAY_MS / (to - from);
    svg.append(s("line", { class: "grid", x1: L, x2: W - R, y1: yBars + barsH, y2: yBars + barsH }));
    weekly.forEach((w) => {
      const x = xAt(isoMs(w.week)), bh = barsH * (w.events || 0) / max, e = eraById(w.era) || (eras.find((x) => x.id === w.era));
      const r = s("rect", { class: "mark", x: x + 1, y: yBars + barsH - bh, width: Math.max(1, wk - 2), height: Math.max(bh, w.events ? 1 : 0), style: `fill:${e ? eraColour(e) : OTHER}` });
      r.addEventListener("mouseenter", () => tip(`Week of ${w.week}: ${fmt(w.events)} events · ${e ? e.name : "in no era"}`, x + wk / 2, yBars + barsH - bh));
      r.addEventListener("mouseleave", () => tip(null));
      svg.append(r);
    });
    svg.append(s("text", { class: "axis", x: L + 4, y: yBars + 10 }, fmt(max)));
    if (today >= from && today <= to) {
      const x = xAt(today);
      svg.append(s("line", { class: "today", x1: x, x2: x, y1: T - 6, y2: yBars + barsH }),
        s("text", { class: "axis label", x: x + 4, y: yBars + barsH + 16 }, "today"));
    }
    return [svg, h("div", { class: "legend" }, eras.map((e) => h("span", { class: "legend-item" },
      h("i", { class: e.kind === "planned" ? "dashed" : "", style: e.kind === "planned" ? `border-color:${eraColour(e)}` : `background:${eraColour(e)}` }),
      h("span", {}, e.name + (e.kind === "planned" ? " (planned)" : ""))))),
      h("p", { class: "small" }, "A filled bar holds data; an outline is still ahead; dashed has no data yet and moves with the plan.")];
  }

  const COMPARE_ROWS = [["players", "Players", "count"], ["new_players", "New players", "count"], ["matches", "Matches", "count"],
    ["editor_share_pct", "Editor share", "pct"], ["finished_pct", "Finished", "pct"], ["rematch_pct", "Rematch within 10 min", "pct"],
    ["d1_pct", "Back next day", "pct"], ["rows_per_play", "Rows per play", "ratio"]];

  function eraCompare(eras) {
    const dataEras = eras.filter((e) => e.kind !== "planned");
    const pick = Object.assign({ a: (dataEras[0] || eras[0] || {}).id, b: (dataEras[1] || eras[1] || {}).id }, store.get("fa-era-compare", {}) || {});
    const out = h("div", { class: "compare-out" });
    const selA = h("select", { class: "mini-select", "aria-label": "First era" }, eras.map((e) => h("option", { value: e.id, selected: e.id === pick.a || null }, e.name)));
    const selB = h("select", { class: "mini-select", "aria-label": "Second era" }, eras.map((e) => h("option", { value: e.id, selected: e.id === pick.b || null }, e.name)));
    async function load() {
      pick.a = selA.value; pick.b = selB.value; store.set("fa-era-compare", pick);
      out.replaceChildren(h("p", { class: "small" }, "Comparing…"));
      try {
        const r = await api(`/v1/eras/compare?a=${encodeURIComponent(pick.a)}&b=${encodeURIComponent(pick.b)}&build=${state.build}`);
        const A = r.a || {}, B = r.b || {};
        const show = (v, kind) => (kind === "pct" ? pct(v) : kind === "ratio" ? ratio(v) : fmt(v));
        const change = (a, b, kind) => {
          if (a === null || a === undefined || b === null || b === undefined) return "–";
          if (kind === "pct") { const d = Math.round((b - a) * 10) / 10; return d === 0 ? "same" : `${d > 0 ? "▲ +" : "▼ "}${d} pts`; }
          if (kind === "ratio") { const d = Math.round((b - a) * 100) / 100; return d === 0 ? "same" : `${d > 0 ? "▲ +" : "▼ "}${d.toFixed(2)}`; }
          return a ? `${b >= a ? "▲ +" : "▼ "}${Math.round(100 * (b - a) / a)}%` : "–";
        };
        out.replaceChildren(table(["Measure", (eraById(pick.a) || { name: pick.a }).name, (eraById(pick.b) || { name: pick.b }).name, "Change"],
          COMPARE_ROWS.map(([k, label, kind]) => h("tr", {}, h("td", {}, label), h("td", {}, show(A[k], kind)), h("td", {}, show(B[k], kind)),
            h("td", { class: "soft" }, change(A[k], B[k], kind))))),
          ...(r.notes || []).map((n) => h("p", { class: "small" }, typeof n === "string" ? n : JSON.stringify(n))));
      } catch (e) { out.replaceChildren(h("p", { class: "small" }, e.message)); }
    }
    selA.addEventListener("change", load); selB.addEventListener("change", load);
    load();
    return card("The eras compared", `any two eras · ${state.build} builds`,
      h("div", { class: "row" }, h("span", { class: "mini-lab" }, "Compare"), selA, h("span", { class: "small" }, "with"), selB), out);
  }

  function eraEditor(eras) {
    const draft = eras.map((e) => Object.assign({}, e));
    const listBox = h("div", { class: "era-list" });
    const status = h("p", { class: "small", "aria-live": "polite" });
    function field(e, k, type, label, extra) {
      const input = h("input", Object.assign({ class: "field sls compact", type, value: e[k] || "", "aria-label": label }, extra || {}));
      input.addEventListener("input", () => { e[k] = input.value; });
      return input;
    }
    function select(e, k, options, label) {
      const sel = h("select", { class: "mini-select", "aria-label": label }, options.map(([v, l]) => h("option", { value: v, selected: e[k] === v || null }, l)));
      sel.addEventListener("change", () => { e[k] = sel.value; draw(); });
      return sel;
    }
    function draw() {
      listBox.replaceChildren(...draft.map((e, i) => h("div", { class: "era-edit sls", style: `box-shadow:inset 3px 0 0 ${eraColour(e)}` },
        h("div", { class: "row" }, field(e, "name", "text", "Era name", { maxlength: "60", style: "flex:1;min-width:140px" }),
          h("button", { type: "button", class: "link", onclick: () => { draft.splice(i, 1); draw(); } }, "Remove")),
        h("div", { class: "row" }, field(e, "from", "date", "From"), h("span", { class: "small" }, "to"), field(e, "to", "date", "To")),
        field(e, "note", "text", "Note", { maxlength: "200", placeholder: "a note (optional)" }),
        h("div", { class: "row" },
          select(e, "source", [["live", "Live"], ["imported", "Imported"], ["any", "Any"]], "Source"),
          select(e, "kind", [["data", "Data"], ["planned", "Planned"]], "Kind"),
          select(e, "colour", [["jade", "Jade"], ["ruby", "Ruby"], ["gold", "Gold"], ["blue", "Blue"]], "Colour"),
          h("span", { class: "small id" }, e.id)))));
    }
    draw();
    const add = h("button", { type: "button", class: "btn sls ghost small-btn", onclick: () => {
      const n = draft.length + 1;
      draft.push({ id: `era_${Date.now().toString(36)}`, name: `New era ${n}`, from: todayIso(), to: todayIso(), source: "live", kind: "planned", builds: [], colour: "blue" });
      draw();
    } }, "New era");
    const save = h("button", { type: "button", class: "btn sls default small-btn", onclick: async () => {
      const bad = draft.find((e) => !/^[a-z0-9_]{1,40}$/.test(e.id) || !/^\d{4}-\d{2}-\d{2}$/.test(e.from) || !/^\d{4}-\d{2}-\d{2}$/.test(e.to) || e.from > e.to || !e.name.trim());
      if (bad) { status.className = "small danger"; status.textContent = `${bad.name || bad.id}: a name, and a from date on or before its to date.`; return; }
      save.disabled = true;
      try {
        await api("/v1/eras", { eras: draft.map((e) => Object.assign({}, e, { name: e.name.trim() })), milestones: state.milestones || [] }, "PUT");
        status.className = "small"; status.textContent = "Saved."; getCache.clear(); state.eras = null;
        setTimeout(() => loadEras().then(render), 400);
      } catch (err) { status.className = "small danger"; status.textContent = err.message; }
      save.disabled = false;
    } }, "Save eras");
    return card("Eras", "admins edit", listBox, h("div", { class: "row" }, add, save), status,
      h("p", { class: "small" }, "An era is a named date range, limited to imported or live data if you like. Pick one in the Era filter on any page; compare any two on the left."));
  }

  function eraList(eras) {
    return card("Eras", "admins edit them", h("div", { class: "era-list" }, eras.map((e) => h("div", { class: "era-edit sls", style: `box-shadow:inset 3px 0 0 ${eraColour(e)}` },
      h("div", { class: "row", style: "justify-content:space-between" }, h("span", { class: "body" }, e.name),
        h("span", { class: "tag" }, e.kind === "planned" ? "planned" : e.source)),
      h("span", { class: "small num" }, `${e.from} to ${e.to}`), e.note ? h("span", { class: "small" }, e.note) : null))));
  }

  async function erasPage() {
    const data = await api("/v1/eras");
    state.eras = data.eras || [];
    state.milestones = data.milestones || [];
    const eras = state.eras;
    return [
      h("div", { class: "grid-eras" },
        h("div", { class: "page" },
          chartPanel({ id: "eras.plan", title: "The plan", note: `${shortDay("2026-08-01")} 2026 to Apr 2027 · today ${shortDay(todayIso())}`, forms: [],
            custom: { plan: { label: "Plan", draw: (f, W, box) => drawPlan(data, W, box) },
                      table: { label: "Table", draw: () => table(["Era", "From", "To", "Source", "Kind", "Events", "Players", "Matches"], eras.map((e) => h("tr", {},
                        h("td", {}, e.name), h("td", {}, e.from), h("td", {}, e.to), h("td", {}, e.source), h("td", {}, e.kind),
                        h("td", {}, fmt(e.events)), h("td", {}, fmt(e.players)), h("td", {}, fmt(e.matches))))) } },
            isEmpty: () => false, load: staticLoad(() => makeFrame({ custom: true })),
            after: (data.notes || []).length ? h("div", {}, data.notes.map((n) => h("p", { class: "small" }, typeof n === "string" ? n : JSON.stringify(n)))) : null }),
          eraCompare(eras),
          h("div", {},
            chartPanel({ id: "eras.source", title: "Where the data comes from", note: "all builds",
              measures: [["events", "Events"], ["players", "Players"], ["matches", "Matches"]], forms: ["bars", "pie", "table"],
              load: staticLoad(({ measure }) => catFrame(eras.filter((e) => e.kind !== "planned"), "id", measure, { by: "era", measure, kname: "Era" })),
              after: h("p", { class: "small" }, "Imported events keep their own time and say so: nothing from PostHog is counted twice or mixed into the new era.") }))),
        state.admin ? eraEditor(eras) : eraList(eras)),
      seriesPanel("eras.series", "Any measure, by era", { measure: "events", by: "era", over: "week", form: "bars" }),
    ];
  }

  // ------------------------------------------------------------------ Play types

  async function playtypesPage() {
    const p = await api(`/v1/playtypes?${filterQuery()}`);
    setUpdated(p.built_ms);
    state.unfiltered = p.unfiltered || [];
    const groups = p.groups || {};
    const share = p.share || {};
    const shareRows = GROUPS.map(([g]) => ({ g, n: share[g] ?? (groups[g] || {}).matches ?? 0 }));
    const column = ([g, label, colour]) => {
      const d = groups[g] || {};
      const meters = [["Finished", d.finished_pct], ["Rematch < 10 min", d.rematch_pct], ["Back next day", d.d1_pct], ["Back in 7 days", d.d7_pct]];
      const tops = d.top_modes || [];
      const topMax = Math.max(1, ...tops.map((t) => t.matches || 0));
      return h("section", { class: "card panel sl group-col", style: `box-shadow:inset 0 2px 0 ${colour}, inset 0 0 0 1px color-mix(in srgb, var(--rim) 28%, var(--fs-line))` },
        h("span", { class: "lab", style: "color:var(--fs-text)" }, label),
        h("div", { class: "row", style: "align-items:baseline" }, h("span", { class: "value-xl num" }, fmt(d.matches)), h("span", { class: "small" }, `matches · ${rangeLabel()}`)),
        chartView({ id: `playtypes.${g}.per_day`, forms: ["line", "bars", "table"], defaults: { form: "line" }, height: 120,
          load: staticLoad(() => timeFrame(d.per_day || [], "day", "matches", { measure: "matches", colours: { null: colour } })) }),
        h("div", { class: "meters" }, meters.map(([l, v]) => h("div", { class: "meter-row" }, h("span", { class: "small" }, l),
          meter(v, colour), h("span", { class: "small num", style: "color:var(--fs-text)" }, pct(v))))),
        h("div", { class: "meter-row" }, h("span", { class: "small" }, "At the controls per match"), h("span"), h("span", { class: "small num", style: "color:var(--fs-text)" },
          d.avg_flight_min === null || d.avg_flight_min === undefined ? "–" : `${d.avg_flight_min} min`)),
        h("span", { class: "lab" }, "Top modes"),
        tops.length ? h("div", { class: "hbars" }, tops.slice(0, 5).map((t) => h("div", { class: "hbar", title: `${t.game_mode}: ${fmt(t.matches)} matches` },
          h("span", { class: "hbar-label" }, t.game_mode || "Unknown"),
          h("div", { class: "hbar-track" }, h("div", { class: "hbar-fill", style: `width:${100 * (t.matches || 0) / topMax}%;background:${colour}` })),
          h("span", { class: "hbar-value num" }, fmt(t.matches))))) : h("p", { class: "small" }, "No matches."));
    };
    return [
      h("div", { class: "grid3" }, GROUPS.map(column)),
      h("div", { class: "grid-funnel" },
        chartPanel({ id: "playtypes.funnel", title: "Multiplayer, from invite to playing again", note: `${rangeLabel()} · ${state.build} builds`,
          forms: ["bars", "table"], emptyText: "No invites in this range.",
          load: staticLoad(() => catFrame(p.funnel || [], "step", "count", { kname: "Step", vname: "Count", colours: Object.fromEntries((p.funnel || []).map((f) => [f.step, GROUP_COLOUR.multi])),
            tips: Object.fromEntries((p.funnel || []).map((f, i, all) => [f.step, i && all[0].count ? `${pct(Math.round(1000 * f.count / all[0].count) / 10)} of invites` : "every invite"])) })),
          after: h("p", { class: "small" }, p.funnel_note || "Each step as a count; the drop between steps is where multiplayer loses people.") }),
        chartPanel({ id: "playtypes.share", title: "Share of matches", forms: ["pie", "bars", "table"], defaults: { form: "pie" }, donut: { centre: "matches" },
          load: staticLoad(() => catFrame(shareRows, "g", "n", { by: "play_type", measure: "matches", kname: "Play type", vname: "Matches" })) })),
      seriesPanel("playtypes.series", "Each play type over time", { measure: "matches", by: "play_type", over: "day", form: "line" },
        { splits: ["play_type"] }),
      h("p", { class: "small" }, "The Play filter in the header sits on Overview, Players, Cohorts, Retention and Matches: pick Multiplayer and every number reads multiplayer only. Numbers the server cannot split by play type say so in their corner."),
    ];
  }

  // ------------------------------------------------------------------ FTUE, Economy, Social, Data health

  async function ftuePage() {
    const f = await api(`/v1/ftue?${filterQuery()}`);
    if (f.empty) return [empty("Nothing has been received yet.")];
    return [
      tileRow([["New players", fmt(f.new_players)], ["Finished the FTUE", fmt(f.finished_ftue)], ["Finished", pct(f.finished_pct)]], "tiles-3"),
      chartPanel({ id: "ftue.steps", title: "Each step of the quest graph", note: "players who started in this range and completed the step, in the order players reach them",
        measures: [["pct_of_new", "% of new"], ["players", "Players"], ["avg_seconds", "Seconds after launch"]], forms: ["bars", "table"], defaults: { form: "table" }, limit: 40,
        emptyText: "No FTUE steps in this range.",
        load: staticLoad(({ measure }) => Object.assign(catFrame(f.steps, "step_name", measure, { kind: measure === "players" ? "count" : measure === "pct_of_new" ? "pct" : "avg", measure, vname: measure }),
          { table: { columns: ["Phase", "Step", "Players", "Of new", "After launch"], rows: f.steps.map((s) => [s.phase_index ?? "–", s.step_name, fmt(s.players), pct(s.pct_of_new), seconds(s.avg_seconds)]) } })) }),
    ];
  }

  async function economyPage() {
    const e = await api(`/v1/economy?${filterQuery()}`);
    if (e.empty) return [empty("Nothing has been received yet.")];
    const t = (k) => (e.totals[k] || {}).amount || 0;
    return [
      tileRow([["Earned", fmt(t("earned"))], ["Spent", fmt(t("spent") + t("unlock"))],
        ["Spends blocked", fmt((e.totals.blocked || {}).events || 0)], ["Paid twice", fmt(e.double_payouts.count), null, e.double_payouts.count > 0],
        ["Unlocks", fmt(e.unlocks.reduce((a, u) => a + u.count, 0))], ["Earned per spent", t("spent") + t("unlock") ? (t("earned") / (t("spent") + t("unlock"))).toFixed(2) : "–"]]),
      chartPanel({ id: "economy.per_day", title: "Crystals per day", note: "hover a bar", forms: ["bars", "line", "table"], height: 200,
        measures: [["earned", "Earned"], ["spent", "Spent"]],
        load: staticLoad(({ measure }) => Object.assign(timeFrame(e.per_day, "day", measure, { label: () => measure === "earned" ? "Earned" : "Spent" }),
          { tips: Object.fromEntries(e.per_day.map((d) => [d.day, `${fmt(d.earned)} earned, ${fmt(d.spent)} spent`])) })) }),
      h("div", { class: "grid2" },
        chartPanel({ id: "economy.sources", title: "Where crystals come from and go", measures: [["amount", "Crystals"], ["events", "Times"]], forms: ["bars", "pie", "table"], defaults: { form: "table" },
          load: staticLoad(({ measure }) => Object.assign(catFrame(e.sources.map((r) => Object.assign({ k: `${r.kind}: ${r.source}` }, r)), "k", measure, { vname: measure }),
            { table: { columns: ["", "Source", "Times", "Crystals"], rows: e.sources.map((r) => [r.kind, r.source, fmt(r.events), fmt(r.amount)]) } })) }),
        chartPanel({ id: "economy.balances", title: "Balances now", note: "players active in the last 30 days, by their latest balance", forms: ["bars", "pie", "table"],
          load: staticLoad(() => catFrame(e.balances, "bucket", "players", { kname: "Balance", vname: "Players" })) })),
      h("div", { class: "grid2" },
        card("Spends blocked", "a player tried to buy without enough crystals", e.blocked.length
          ? table(["Item", "Times", "Short by"], e.blocked.map((r) => h("tr", {}, h("td", {}, r.item), h("td", {}, fmt(r.events)), h("td", {}, fmt(r.short_by)))))
          : h("p", { class: "small" }, "None.")),
        chartPanel({ id: "economy.unlocks", title: "Unlocks", forms: ["bars", "pie", "table"], defaults: { form: "table" },
          load: staticLoad(() => Object.assign(catFrame(e.unlocks.map((r) => Object.assign({ k: `${r.kind}: ${r.name}` }, r)), "k", "count", { vname: "Times" }),
            { table: { columns: ["Kind", "What", "Times"], rows: e.unlocks.map((r) => [r.kind, r.name, fmt(r.count)]) } })) })),
    ];
  }

  async function socialPage() {
    const so = await api(`/v1/social?${filterQuery()}`);
    if (so.empty) return [empty("Nothing has been received yet.")];
    const t = so.totals;
    return [
      tileRow([["Party invites", fmt(t.party_invite_sent)], ["Invites joined", pct(so.invite_join_pct)],
        ["Friend requests", fmt(t.friend_request_sent)], ["Requests accepted", pct(so.friend_accept_pct)], ["Shares", fmt(t.share_triggered)],
        ["Multi-human matches", fmt(so.multi_human_matches)]]),
      chartPanel({ id: "social.events", title: "Every social event", forms: ["bars", "pie", "table"], defaults: { form: "bars" },
        load: staticLoad(() => catFrame(Object.entries(t).map(([k, v]) => ({ k: k.replace(/_/g, " "), v })), "k", "v", { kname: "Event", vname: "Count" })) }),
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
        chartPanel({ id: "health.rpp", title: "Rows per play, release builds", note: "game_started rows over distinct (player, match): 1.0 is healthy",
          forms: ["line", "bars", "table"], defaults: { form: "line" }, target: 1, emptyText: "No release matches in this range.",
          load: staticLoad(() => Object.assign(timeFrame(rpp, "day", "rows_per_play", { kind: "ratio", label: () => "Rows per play" }),
            { tips: Object.fromEntries(rpp.map((r) => [r.day, `${fmt(r.rows)} rows, ${fmt(r.plays)} plays`])) })) }),
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
        chartPanel({ id: "health.dups", title: "Flagged duplicates per day", note: "same player, event and properties within 50 ms",
          forms: ["bars", "line", "table"], height: 180, emptyText: "None.",
          load: staticLoad(() => Object.assign(timeFrame(d.near_duplicates || [], "day", "near_duplicates", { label: () => "Duplicates" }),
            { tips: Object.fromEntries((d.near_duplicates || []).map((r) => [r.day, `of ${fmt(r.rows)} rows`])) })) }),
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
        card("Matches with no element", "every match ever, by mode: a mode with no genre or an old name",
          (d.modes_without_element || []).length
            ? table(["Mode", "Plays", "From PostHog"], d.modes_without_element.slice(0, 20).map((r) => h("tr", {}, h("td", {}, r.game_mode),
                h("td", { class: "num" }, fmt(r.plays)), h("td", { class: "num soft" }, fmt(r.imported_plays)))))
            : h("p", { class: "small" }, "None. Every match has an element.")),
        card("Rejected events", "counted, not stored",
          (d.rejects || []).length
            ? table(["Reason", "Event", "Count"], d.rejects.map((r) => h("tr", {}, h("td", {}, r.reason.replace(/_/g, " ")),
                h("td", {}, r.event || "–"), h("td", { class: "bad" }, fmt(r.count)))))
            : h("p", { class: "small" }, "None. Every event matched the schema."))),
    ];
  }

  // ------------------------------------------------------------------ Ask Claude, Erasure

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

  // ------------------------------------------------------------------ Query Studio (admins)
  // Tables on the left (GET /v1/schema), a builder that writes SQL (or SQL by hand, or Ask Claude), Run -> POST
  // /v1/query (the Ask box's read-only guard: aggregates only, 2 s, 200 rows), the result as any chart, Save.

  const QB_FNS = [["count", "Count rows"], ["count_distinct", "Count distinct"], ["sum", "Sum"], ["avg", "Average"], ["min", "Min"], ["max", "Max"], ["pct", "Share % (of 0/1)"]];
  const QB_OPS = ["=", "!=", ">", ">=", "<", "<=", "LIKE", "IS NULL", "IS NOT NULL"];
  const qs = { tab: "builder", sql: "", result: null, running: false, schema: null, saved: null, qb: null, x: null, y: null, ask: null,
               keep: "query", vis: "readers" };
  const ident = (n) => (/^[A-Za-z_][A-Za-z0-9_]*$/.test(n) ? n : `"${String(n).replace(/"/g, '""')}"`);
  const lit = (v) => (/^-?\d+(\.\d+)?$/.test(String(v).trim()) ? String(v).trim() : `'${String(v).replace(/'/g, "''")}'`);

  function measureSql(m) {
    const c = m.col ? ident(m.col) : "*";
    const expr = { count: "COUNT(*)", count_distinct: `COUNT(DISTINCT ${c})`, sum: `SUM(${c})`, avg: `ROUND(AVG(${c}), 2)`,
                   min: `MIN(${c})`, max: `MAX(${c})`, pct: `ROUND(100.0 * AVG(${c}), 1)` }[m.fn] || "COUNT(*)";
    return `${expr} AS ${ident(m.as || (m.fn === "count" ? "rows" : `${m.fn}_${m.col || "rows"}`))}`;
  }
  const PLAY_SQL = { solo_vs_ai: "play_type = 'solo_vs_ai'", solo: "play_type = 'solo_alone'",
                     multi: "play_type IN ('coop', 'adversarial', 'mixed', 'multi_human_unknown')" };
  function builderSql(qb, columns) {
    const where = [];
    if (qb.useFilters) {
      if (columns.includes("build_kind")) where.push(`build_kind = ${lit(state.build)}`);
      const era = state.era && eraById(state.era);
      const dayCol = columns.includes("day") ? "day" : columns.includes("first_day") ? "first_day" : null;
      if (dayCol && era) where.push(`${dayCol} BETWEEN ${lit(era.from)} AND ${lit(era.to)}`);
      else if (dayCol) where.push(`${dayCol} >= date('now', '-${state.days} days')`);
      if (columns.includes("play_type") && state.play !== "all") where.push(PLAY_SQL[state.play]);
    }
    for (const w of qb.where) if (w.col) where.push(/NULL/.test(w.op) ? `${ident(w.col)} ${w.op}` : `${ident(w.col)} ${w.op} ${lit(w.val)}`);
    const sel = qb.group.map(ident).concat(qb.measures.map(measureSql));
    let sql = `SELECT ${sel.length ? sel.join(",\n       ") : "*"}\nFROM ${ident(qb.from)}`;
    if (where.length) sql += `\nWHERE ${where.join("\n  AND ")}`;
    if (qb.group.length) sql += `\nGROUP BY ${qb.group.map(ident).join(", ")}`;
    if (qb.order.by) sql += `\nORDER BY ${ident(qb.order.by)} ${qb.order.dir === "asc" ? "ASC" : "DESC"}`;
    sql += `\nLIMIT ${Math.max(1, Math.min(200, Number(qb.limit) || 50))};`;
    return sql;
  }

  // A result {columns, rows} as a frame: x column -> categories (or days), y column -> values.
  function resultFrame(res, x, y) {
    const cols = res.columns || [], rows = res.rows || [];
    let xi = Math.max(0, cols.indexOf(x)), yi = cols.indexOf(y);
    if (yi < 0) yi = cols.findIndex((c, i) => i !== xi && rows.some((r) => typeof r[i] === "number"));
    if (yi < 0) yi = cols.length > 1 ? 1 : 0;
    const isDay = rows.length > 1 && rows.every((r) => /^\d{4}-\d{2}-\d{2}/.test(String(r[xi])));
    const name = cols[yi] || "value";
    const kind = /pct|share|rate/.test(name) ? "pct" : /avg|mean|ratio|per_/.test(name) ? "avg" : "count";
    const frame = isDay
      ? timeFrame(rows.map((r) => ({ x: r[xi], v: Number(r[yi]) })).sort((a, b) => (a.x < b.x ? -1 : 1)), "x", "v", { kind, label: () => name, xname: cols[xi] })
      : catFrame(rows.map((r) => ({ k: String(r[xi] ?? "–"), v: Number(r[yi]) })), "k", "v", { kind, kname: cols[xi], vname: name });
    frame.table = { columns: cols, rows: rows.map((r) => r.map((c) => (c === null ? "–" : c))) };
    if (cols.length === 1 && rows.length === 1) { frame.keys = [cols[0]]; frame.v = new Map([["null" + SEP + cols[0], Number(rows[0][0])]]); frame.label = () => cols[0]; }
    return frame;
  }
  const CHART_FORM = { bars: "bars", line: "line", donut: "pie", pie: "pie", table: "table", number: "number", gantt: "table" };

  async function queryPage() {
    if (!state.admin) return [empty("The Query Studio needs an admin key.")];
    if (!qs.schema) qs.schema = await api("/v1/schema");
    try { qs.saved = await api("/v1/queries"); } catch (e) { qs.saved = { queries: [], error: e.message }; }
    const tables = qs.schema.tables || [];
    if (!qs.qb) qs.qb = { from: (tables.find((t) => t.name === "plays") || tables[0] || { name: "plays" }).name, where: [], group: ["game_mode"],
                          measures: [{ fn: "count_distinct", col: "match_id", as: "matches" }, { fn: "pct", col: "completed", as: "finished_pct" }],
                          order: { by: "matches", dir: "desc" }, limit: 12, useFilters: true };
    const qb = qs.qb;
    const colsOf = (name) => ((tables.find((t) => t.name === name) || {}).columns || []);
    if (!qs.sql) qs.sql = builderSql(qb, colsOf(qb.from));

    // the tables
    const open = new Set(store.get("fa-qs-open", [qb.from]) || []);
    const browser = card("Tables", `${tables.length}`, h("div", { class: "tables" }, tables.map((t) => {
      const isOpen = open.has(t.name);
      return h("div", { class: "tbl" + (isOpen ? " open" : "") },
        h("button", { type: "button", class: "tbl-name", "aria-expanded": String(isOpen), onclick: () => {
          isOpen ? open.delete(t.name) : open.add(t.name); store.set("fa-qs-open", [...open]); render();
        } }, h("span", {}, t.name), h("span", { class: "small num" }, `${t.saved ? "saved · " : ""}${(t.columns || []).length} cols${t.rows !== undefined ? ` · ${fmt(t.rows)} rows` : ""}`)),
        isOpen ? h("div", { class: "tbl-cols" }, (t.columns || []).map((c) => h("button", { type: "button", class: "tbl-col", title: "Insert into the SQL",
          onclick: () => { qs.tab = "sql"; qs.sql = qs.sql.trimEnd() + (qs.sql.trim() ? " " : "") + c; render(); } }, c))) : null);
    })), h("p", { class: "small" }, "Totals only: no player ids. Saved tables start t_."));

    // the builder
    const cols = colsOf(qb.from);
    const sync = () => { qs.sql = builderSql(qb, cols); sqlPreview.textContent = qs.sql; };
    const colSelect = (value, onChange, label, allowEmpty) => {
      const sel = h("select", { class: "mini-select", "aria-label": label },
        allowEmpty ? h("option", { value: "" }, "—") : null,
        cols.map((c) => h("option", { value: c, selected: c === value || null }, c)));
      sel.addEventListener("change", () => onChange(sel.value));
      return sel;
    };
    const rowOf = (label, ...items) => h("div", { class: "qb-row" }, h("span", { class: "qb-lab" }, label), h("div", { class: "qb-items" }, ...items));
    const fromSel = h("select", { class: "mini-select", "aria-label": "From" }, tables.map((t) => h("option", { value: t.name, selected: t.name === qb.from || null }, t.name)));
    fromSel.addEventListener("change", () => {
      qb.from = fromSel.value; const c = colsOf(qb.from);
      qb.where = []; qb.group = qb.group.filter((g) => c.includes(g));
      qb.measures = [{ fn: "count", col: "", as: "rows" }]; qb.order = { by: "rows", dir: "desc" };
      qs.sql = builderSql(qb, c); render();
    });
    const sqlPreview = h("pre", { class: "code" }, qs.sql);
    const builder = h("div", { class: "qb" },
      rowOf("From", fromSel, h("label", { class: "small check-label" },
        h("input", { type: "checkbox", checked: qb.useFilters || null, onchange: (e) => { qb.useFilters = e.target.checked; sync(); } }), "Use the header's build, range, era and play")),
      rowOf("Where", ...qb.where.map((w, i) => {
        const op = h("select", { class: "mini-select", "aria-label": "Operator" }, QB_OPS.map((o) => h("option", { value: o, selected: o === w.op || null }, o)));
        const val = h("input", { class: "field sls compact", type: "text", value: w.val || "", placeholder: "value", "aria-label": "Value", hidden: /NULL/.test(w.op) || null });
        op.addEventListener("change", () => { w.op = op.value; val.hidden = /NULL/.test(w.op); sync(); });
        val.addEventListener("input", () => { w.val = val.value; sync(); });
        return h("span", { class: "qb-chip" }, colSelect(w.col, (v) => { w.col = v; sync(); }, "Column"), op, val,
          h("button", { type: "button", class: "x", "aria-label": "Remove condition", onclick: () => { qb.where.splice(i, 1); sync(); render(); } }, "×"));
      }), h("button", { type: "button", class: "qb-add", onclick: () => { qb.where.push({ col: cols[0], op: "=", val: "" }); sync(); render(); } }, "+ Condition")),
      rowOf("Group by", ...qb.group.map((g, i) => h("span", { class: "qb-chip" }, colSelect(g, (v) => { qb.group[i] = v; sync(); }, "Group by"),
        h("button", { type: "button", class: "x", "aria-label": "Remove group", onclick: () => { qb.group.splice(i, 1); sync(); render(); } }, "×"))),
        h("button", { type: "button", class: "qb-add", onclick: () => { qb.group.push(cols[0]); sync(); render(); } }, "+ Group")),
      rowOf("Measure", ...qb.measures.map((m, i) => {
        const fn = h("select", { class: "mini-select", "aria-label": "Function" }, QB_FNS.map(([k, l]) => h("option", { value: k, selected: k === m.fn || null }, l)));
        const as = h("input", { class: "field sls compact", type: "text", value: m.as || "", placeholder: "name", "aria-label": "Name", style: "width:120px" });
        fn.addEventListener("change", () => { m.fn = fn.value; sync(); render(); });
        as.addEventListener("input", () => { m.as = as.value.replace(/[^A-Za-z0-9_]/g, ""); sync(); });
        return h("span", { class: "qb-chip" }, fn, m.fn === "count" ? null : colSelect(m.col, (v) => { m.col = v; sync(); }, "Column", true), h("span", { class: "small" }, "as"), as,
          h("button", { type: "button", class: "x", "aria-label": "Remove measure", onclick: () => { qb.measures.splice(i, 1); sync(); render(); } }, "×"));
      }), h("button", { type: "button", class: "qb-add", onclick: () => { qb.measures.push({ fn: "count", col: "", as: `m${qb.measures.length + 1}` }); sync(); render(); } }, "+ Measure")),
      rowOf("Order", (() => {
        const names = qb.group.concat(qb.measures.map((m) => m.as || "")).filter(Boolean);
        const by = h("select", { class: "mini-select", "aria-label": "Order by" }, h("option", { value: "" }, "—"), names.map((n) => h("option", { value: n, selected: n === qb.order.by || null }, n)));
        by.addEventListener("change", () => { qb.order.by = by.value; sync(); });
        const dir = h("select", { class: "mini-select", "aria-label": "Direction" }, [["desc", "High to low"], ["asc", "Low to high"]].map(([k, l]) => h("option", { value: k, selected: k === qb.order.dir || null }, l)));
        dir.addEventListener("change", () => { qb.order.dir = dir.value; sync(); });
        const lim = h("input", { class: "field sls compact", type: "number", min: "1", max: "200", value: qb.limit, "aria-label": "Limit", style: "width:84px" });
        lim.addEventListener("input", () => { qb.limit = lim.value; sync(); });
        return h("span", { class: "qb-items" }, by, dir, h("span", { class: "small" }, "limit"), lim);
      })()),
      sqlPreview);

    const sqlBox = h("textarea", { class: "field code-field sls", spellcheck: "false", "aria-label": "SQL", rows: "10" }, qs.sql);
    sqlBox.addEventListener("input", () => { qs.sql = sqlBox.value; });
    const askBox = h("input", { class: "field sls", type: "text", maxlength: "500", placeholder: "Ask in plain words; Claude writes the query", "aria-label": "Question", value: qs.askDraft || "" });
    askBox.addEventListener("input", () => { qs.askDraft = askBox.value; });
    const askOut = qs.ask ? (qs.ask.error ? h("p", { class: "small danger" }, qs.ask.error)
      : h("div", { class: "read info sls" }, h("p", { class: "body" }, qs.ask.answer || ""), qs.ask.caveat ? h("p", { class: "small" }, qs.ask.caveat) : null)) : null;
    const tabs = h("div", { class: "mini-seg", role: "tablist", "aria-label": "Query" }, [["builder", "Builder"], ["sql", "SQL"], ["ask", "Ask Claude"]].map(([k, l]) =>
      h("button", { type: "button", role: "tab", "aria-selected": String(qs.tab === k), "aria-pressed": String(qs.tab === k), onclick: () => { qs.tab = k; render(); } }, l)));
    const run = h("button", { type: "button", class: "btn sls cta", disabled: qs.running || null, onclick: () => runQuery() }, qs.running ? "Running…" : "Run");
    const status = qs.result ? (qs.result.error ? h("span", { class: "small danger" }, qs.result.error)
      : h("span", { class: "small" }, `Ctrl + Enter · ran in ${fmt(qs.result.ms)} ms · ${fmt((qs.result.rows || []).length)} rows`)) : h("span", { class: "small" }, "Ctrl + Enter runs it");
    const askRun = h("button", { type: "button", class: "btn sls default", disabled: qs.running || null, onclick: async () => {
      const q = askBox.value.trim(); if (!q) return;
      qs.running = true; render();
      try {
        const a = await api("/v1/ask", { question: q });
        qs.ask = a; if (a.sql) qs.sql = a.sql;
        qs.result = { columns: a.columns || [], rows: a.rows || [], ms: a.ms };
      } catch (e) { qs.ask = { error: e.message }; }
      qs.running = false; render();
    } }, "Ask");
    const editor = card("Query", "read-only: aggregates, 2 s and 200 rows at most, like the Ask box",
      tabs,
      qs.tab === "builder" ? builder : qs.tab === "sql" ? sqlBox : h("div", { class: "page" }, h("div", { class: "row" }, h("div", { style: "flex:1;min-width:220px" }, askBox), askRun), askOut,
        qs.ask && qs.ask.sql ? h("pre", { class: "code" }, qs.ask.sql) : null),
      qs.tab !== "ask" ? h("div", { class: "row" }, run, status) : status);
    editor.addEventListener("keydown", (e) => { if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) { e.preventDefault(); runQuery(); } });

    // the result
    let resultCard = null;
    const res = qs.result;
    if (res && !res.error && (res.columns || []).length) {
      const cols2 = res.columns;
      if (!cols2.includes(qs.x)) qs.x = cols2[0];
      if (!cols2.includes(qs.y)) qs.y = cols2.find((c, i) => i > 0 && (res.rows || []).some((r) => typeof r[i] === "number")) || cols2[cols2.length - 1];
      const pickCol = (label, value, set) => {
        const sel = h("select", { class: "mini-select", "aria-label": label }, cols2.map((c) => h("option", { value: c, selected: c === value || null }, c)));
        sel.addEventListener("change", () => { set(sel.value); render(); });
        return h("label", { class: "mini-seg" }, h("span", { class: "mini-lab" }, label), sel);
      };
      resultCard = chartPanel({ id: "query.result", title: "Result", forms: ["bars", "line", "pie", "table", "number"], defaults: { form: "bars" },
        load: staticLoad(() => resultFrame(res, qs.x, qs.y)), after: h("div", { class: "row" }, pickCol("X", qs.x, (v) => { qs.x = v; }), pickCol("Y", qs.y, (v) => { qs.y = v; })) });
    } else if (res && !res.error) resultCard = card("Result", null, h("p", { class: "small" }, "No rows."));

    // save and the saved list
    const name = h("input", { class: "field sls", type: "text", maxlength: "40", placeholder: "name_in_lower_case", "aria-label": "Name", value: qs.saveName || "" });
    name.addEventListener("input", () => { qs.saveName = name.value; });
    const pickSeg = (label, options, k) => h("div", { class: "mini-seg stack", role: "group", "aria-label": label }, h("span", { class: "mini-lab" }, label),
      options.map(([v, l]) => h("button", { type: "button", "aria-pressed": String((qs[k] || options[0][0]) === v), onclick: () => { qs[k] = v; render(); } }, l)));
    const saveStatus = h("p", { class: "small", "aria-live": "polite" }, qs.saveStatus || "");
    const saveBtn = h("button", { type: "button", class: "btn sls default", onclick: async () => {
      const n = name.value.trim();
      if (!/^[a-z0-9_]{1,40}$/.test(n)) { saveStatus.className = "small danger"; saveStatus.textContent = "A name of a-z, 0-9 and _, up to 40."; return; }
      const form = (store.get("fa-chart-query.result", {}) || {}).form || "bars";
      try {
        await api("/v1/queries", { id: qs.loaded && qs.loaded.name === n ? qs.loaded.id : undefined, name: n, sql: qs.sql, chart: form === "pie" ? "donut" : form, x: qs.x, y: qs.y,
          visibility: qs.vis || "readers", as_table: (qs.keep || "query") === "table" });
        qs.saveStatus = (qs.keep === "table") ? `Saved; rebuilt nightly as t_${n}.` : "Saved."; qs.schema = null; render();
      } catch (e) { saveStatus.className = "small danger"; saveStatus.textContent = e.message; }
    } }, "Save");
    const saved = (qs.saved && (qs.saved.queries || (Array.isArray(qs.saved) ? qs.saved : []))) || [];
    const saveCard = card("Save", null, name,
      pickSeg("Keep it as", [["query", "A saved query"], ["table", "A table, rebuilt nightly"]], "keep"),
      pickSeg("Who sees it", [["me", "Only me"], ["readers", "Readers"], ["admins", "Admins"]], "vis"),
      saveBtn, saveStatus,
      h("span", { class: "lab" }, "Saved queries"),
      saved.length ? h("div", { class: "saved-list" }, saved.map((q) => h("div", { class: "saved-row" },
        h("div", { class: "saved-name" }, h("button", { type: "button", class: "link name", title: q.sql, onclick: () => { qs.sql = q.sql; qs.tab = "sql"; qs.x = q.x; qs.y = q.y; qs.saveName = q.name; qs.loaded = { id: q.id, name: q.name };
          qs.keep = q.as_table ? "table" : "query"; qs.vis = q.visibility || "readers";
          if (q.chart) store.set("fa-chart-query.result", { form: CHART_FORM[q.chart] || "bars" }); runQuery(); } }, q.name),
          h("span", { class: "small" }, [q.owner, q.visibility, q.as_table ? q.table || `t_${q.name}` : null].filter(Boolean).join(" · "))),
        h("button", { type: "button", class: "x", "aria-label": `Delete ${q.name}`, onclick: async () => {
          try { await api(`/v1/queries?id=${encodeURIComponent(q.id)}`, null, "DELETE"); render(); } catch (e) { alertLine(saveCard, e.message); }
        } }, "×")))) : h("p", { class: "small" }, qs.saved && qs.saved.error ? qs.saved.error : "None yet."));

    return [h("div", { class: "grid-query" }, browser, h("div", { class: "page" }, editor, resultCard), saveCard)];
  }

  async function runQuery() {
    if (qs.running) return;
    const sql = (qs.sql || "").trim();
    if (!sql) return;
    qs.running = true; render();
    try { qs.result = await api("/v1/query", { sql }); }
    catch (e) { qs.result = { error: e.message }; }
    qs.running = false; render();
  }

  // ------------------------------------------------------------------ Dashboards
  // GET /v1/dashboards; tiles from saved queries or the console's builtin views (GET /v1/dashboards/tile); admins edit:
  // add, remove, resize (w 1-4, h 1-3), move, share, an email schedule kept for later; PUT saves, DELETE removes.

  const BUILTINS = [["builtin:new_players", "New players", "number"], ["builtin:d1_return", "D1 return", "number"],
    ["builtin:ftue_finished", "FTUE finished", "number"], ["builtin:rows_per_play", "Rows per play", "number"],
    ["builtin:matches_per_day", "Matches per day", "bars"], ["builtin:play_mix", "Play mix", "donut"],
    ["builtin:freestyle_entries", "Toybox entries per day", "bars"]];
  const FORM_CHART = { bars: "bars", line: "line", pie: "donut", table: "table", number: "number" };
  const ds = { current: null, editing: false, draft: null, list: null };
  const emailOf = (d) => (d && d.email ? (typeof d.email === "string" ? d.email : d.email.email || "") : "");

  function tileView(tile) {
    const form = CHART_FORM[tile.chart] || "bars";
    const room = 224 * (tile.h || 1) - 14 - 112;           // the tile's height less its head and switches
    return chartView({ id: `dash.${ds.current}.${tile.id}`, forms: ["bars", "line", "pie", "table", "number"], defaults: { form }, height: Math.max(90, room), compact: true,
      donut: { max: Math.max(96, room), centre: "" }, limit: Math.max(3, Math.floor(room / 22)),
      load: async () => {
        const r = await cached(`/v1/dashboards/tile?${filterQuery(PAGES.dashboards, { source: tile.source })}`);
        if (r.series) return Object.assign(frameFromSeries(r.series), { unfiltered: [] });   // a saved view: its own filters
        if (tile.source === "builtin:play_mix") {         // the raw play types folded into the three groups
          const by = {};
          for (const [k, n] of r.rows || []) by[groupOf(k) || k] = (by[groupOf(k) || k] || 0) + (Number(n) || 0);
          return Object.assign(catFrame(GROUPS.map(([g]) => ({ g, n: by[g] || 0 })), "g", "n", { by: "play_type", measure: "matches", kname: "Play type", vname: "Matches" }),
            { unfiltered: r.unfiltered || [] });
        }
        const cols = r.columns || [];                    // a builtin's measure is its last column; a query's, its second
        return Object.assign(resultFrame(r, cols[0], tile.source.startsWith("builtin:") ? cols[cols.length - 1] : cols[1]), { unfiltered: r.unfiltered || [] });
      } });
  }

  async function dashboardsPage() {
    const r = await api("/v1/dashboards");
    ds.list = r.dashboards || (Array.isArray(r) ? r : []);
    let queries = [];
    if (state.admin) try { const q = await api("/v1/queries"); queries = q.queries || (Array.isArray(q) ? q : []); } catch (e) {}
    if (!ds.editing && (!ds.current || !ds.list.some((d) => d.id === ds.current))) ds.current = ds.list.length ? ds.list[0].id : null;
    const board = ds.editing ? ds.draft : ds.list.find((d) => d.id === ds.current);
    const side = card("Dashboards", null, h("div", { class: "dash-list" }, ds.list.map((d) => h("button", { type: "button", class: "dash-item" + (d.id === ds.current ? " on" : ""),
      "aria-pressed": String(d.id === ds.current), onclick: () => { ds.current = d.id; ds.editing = false; render(); } },
      h("span", { class: "body" }, d.name), h("span", { class: "small" }, [d.owner, d.shared ? `shared with ${d.shared}` : null].filter(Boolean).join(" · "))))),
      state.admin ? h("button", { type: "button", class: "btn sls ghost small-btn", onclick: () => {
        ds.editing = true; ds.current = null; ds.draft = { name: "New dashboard", shared: "me", email: "", tiles: [] }; render();
      } }, "New dashboard") : null,
      !ds.list.length && !ds.editing ? h("p", { class: "small" }, state.admin ? "No dashboards yet. Make one from any saved query or the console's own numbers." : "No dashboards are shared with you yet.") : null);
    if (!board) return [h("div", { class: "grid-dash" }, side, h("div", { class: "page" }, empty("Pick a dashboard, or make one."), presetsCard(null)))];

    const head = h("div", { class: "dash-head" },
      ds.editing ? h("input", { class: "field sls", type: "text", value: board.name, maxlength: "60", "aria-label": "Dashboard name", style: "max-width:360px",
        oninput: (e) => { board.name = e.target.value; } }) : h("h2", { class: "h2" }, board.name),
      h("div", { class: "row" },
        ds.editing ? h("span", { class: "tag" }, "Editing") : null,
        ds.editing ? h("label", { class: "mini-seg" }, h("span", { class: "mini-lab" }, "Share"), (() => {
          const sel = h("select", { class: "mini-select", "aria-label": "Share" }, [["me", "Only me"], ["readers", "Readers"], ["admins", "Admins"]].map(([k, l]) =>
            h("option", { value: k, selected: (board.shared || "me") === k || null }, l)));
          sel.addEventListener("change", () => { board.shared = sel.value; });
          return sel;
        })()) : h("span", { class: "small" }, `Shared with ${board.shared || "me"}`),
        ds.editing ? h("label", { class: "mini-seg" }, h("span", { class: "mini-lab" }, "Email"), h("input", { class: "field sls compact", type: "text", value: board.email || "",
          placeholder: "mondays 09:00", "aria-label": "Email schedule", style: "width:150px", oninput: (e) => { board.email = e.target.value; } })) : emailOf(board) ? h("span", { class: "small" }, `Email ${emailOf(board)} (kept; sending is a later decision)`) : null,
        state.admin && !ds.editing ? h("button", { type: "button", class: "btn sls ghost small-btn", onclick: () => { ds.editing = true; ds.draft = JSON.parse(JSON.stringify(board)); ds.draft.email = emailOf(board); render(); } }, "Edit") : null,
        ds.editing ? h("button", { type: "button", class: "btn sls ghost small-btn", onclick: () => { ds.editing = false; ds.draft = null; if (!ds.current && ds.list.length) ds.current = ds.list[0].id; render(); } }, "Cancel") : null,
        ds.editing && board.id ? h("button", { type: "button", class: "btn sls danger-btn small-btn", onclick: async () => {
          try { await api(`/v1/dashboards?id=${encodeURIComponent(board.id)}`, null, "DELETE"); ds.editing = false; ds.current = null; render(); } catch (e) { alertLine(head, e.message); }
        } }, "Delete") : null,
        ds.editing ? h("button", { type: "button", class: "btn sls default small-btn", onclick: async () => {
          try {
            const saved = await api("/v1/dashboards", { id: board.id, name: board.name.trim() || "Dashboard", shared: board.shared || "me", email: (typeof board.email === "string" ? board.email.trim() : emailOf(board)) || null,
              tiles: board.tiles.map((t) => ({ id: t.id, source: t.source, title: t.title, chart: t.chart, w: t.w, h: t.h })) }, "PUT");
            ds.editing = false; ds.current = (saved.dashboard || saved).id || board.id; getCache.clear(); render();
          } catch (e) { alertLine(head, e.message); }
        } }, "Done") : null));

    const grid = h("div", { class: "dash-grid" }, board.tiles.map((t, i) => {
      const tools = ds.editing ? h("div", { class: "tile-tools" },
        h("button", { type: "button", "aria-label": "Move earlier", disabled: i === 0 || null, onclick: () => { board.tiles.splice(i - 1, 0, board.tiles.splice(i, 1)[0]); render(); } }, "↑"),
        h("button", { type: "button", "aria-label": "Move later", disabled: i === board.tiles.length - 1 || null, onclick: () => { board.tiles.splice(i + 1, 0, board.tiles.splice(i, 1)[0]); render(); } }, "↓"),
        h("button", { type: "button", "aria-label": "Narrower", disabled: t.w <= 1 || null, onclick: () => { t.w--; render(); } }, "W−"),
        h("button", { type: "button", "aria-label": "Wider", disabled: t.w >= 4 || null, onclick: () => { t.w++; render(); } }, "W+"),
        h("button", { type: "button", "aria-label": "Shorter", disabled: t.h <= 1 || null, onclick: () => { t.h--; render(); } }, "H−"),
        h("button", { type: "button", "aria-label": "Taller", disabled: t.h >= 3 || null, onclick: () => { t.h++; render(); } }, "H+"),
        h("button", { type: "button", "aria-label": "Remove tile", onclick: () => { board.tiles.splice(i, 1); render(); } }, "×")) : null;
      return h("section", { class: "card panel sl dash-tile", style: `--w:${Math.max(1, Math.min(4, t.w || 1))};--h:${Math.max(1, Math.min(3, t.h || 1))}` },
        h("div", { class: "card-head" }, h("span", { class: "lab" }, t.title || t.source), h("span", { class: "small" }, sourceLabel(t.source))),
        tools, tileView(t));
    }));
    if (ds.editing) {
      const sources = BUILTINS.map(([v, l, c]) => [v, `${l} (console)`, c]).concat(queries.map((q) => [`query:${q.id}`, `${q.name} (saved query)`, q.chart || "table"]),
        (state.presets || []).filter((x) => x.series).map((x) => [`preset:${x.id}`, `${x.name} (saved view)`, FORM_CHART[x.settings.form] || "bars"]));
      const src = h("select", { class: "mini-select", "aria-label": "Source" }, sources.map(([v, l]) => h("option", { value: v }, l)));
      const title = h("input", { class: "field sls compact", type: "text", placeholder: "Title", "aria-label": "Title", style: "width:180px" });
      const chart = h("select", { class: "mini-select", "aria-label": "Chart" }, ["bars", "line", "donut", "table", "number"].map((c) => h("option", { value: c }, c)));
      src.addEventListener("change", () => { const s0 = sources.find((x) => x[0] === src.value); if (s0) chart.value = s0[2] === "gantt" ? "table" : s0[2]; });
      src.dispatchEvent(new Event("change"));
      grid.append(h("section", { class: "card panel sl dash-tile add-tile", style: "--w:2;--h:1" },
        h("span", { class: "lab" }, "Add a tile"),
        h("div", { class: "row" }, src, title, chart,
          h("button", { type: "button", class: "btn sls default small-btn", onclick: () => {
            const s0 = sources.find((x) => x[0] === src.value) || sources[0];
            board.tiles.push({ id: `t${Date.now().toString(36)}`, source: s0[0], title: title.value.trim() || s0[1].replace(/ \(.*\)$/, ""), chart: chart.value, w: chart.value === "number" ? 1 : 2, h: 1 });
            render();
          } }, "Add")),
        h("p", { class: "small" }, "From a saved query, or any of the console's own numbers.")));
    }
    return [h("div", { class: "grid-dash" }, side, h("div", { class: "page" }, head,
      board.tiles.length || ds.editing ? grid : empty(state.admin ? "No tiles yet. Edit to add some." : "This dashboard has no tiles yet."),
      presetsCard(board)))];
  }

  const sourceLabel = (src) => {
    if (src.startsWith("preset:")) { const p = (state.presets || []).find((x) => x.id === src.slice(7)); return p ? `saved view · ${p.name}` : "saved view"; }
    return src.replace(/^builtin:|^query:/, "");
  };

  // Saved presets: every saved view this key can see (its own and the public ones). Open goes to its page with its
  // switches and filters; a series view can be added to the dashboard on screen as a tile (preset:<id>).
  function presetsCard(board) {
    const list = state.presets || [];
    const out = h("span", { class: "small", "aria-live": "polite" });
    const addTo = async (p) => {
      const tile = { id: `t${Date.now().toString(36)}`, source: `preset:${p.id}`, title: p.name.slice(0, 80), chart: FORM_CHART[p.settings.form] || "bars", w: 2, h: 2 };
      if (ds.editing && ds.draft) { ds.draft.tiles.push(tile); render(); return; }
      const target = board || { name: "Saved views", shared: "me", email: null, tiles: [] };
      try {
        const saved = await api("/v1/dashboards", { id: target.id, name: target.name, shared: target.shared || "me", email: emailOf(target) || null,
          tiles: target.tiles.map((t) => ({ id: t.id, source: t.source, title: t.title, chart: t.chart, w: t.w, h: t.h })).concat(tile) }, "PUT");
        ds.current = (saved.dashboard || saved).id || target.id; getCache.clear(); render();
      } catch (e) { out.className = "small danger"; out.textContent = e.message; }
    };
    const del = async (p) => {
      try { await api(`/v1/presets?id=${encodeURIComponent(p.id)}`, null, "DELETE"); state.presets = list.filter((x) => x.id !== p.id); render(); }
      catch (e) { out.className = "small danger"; out.textContent = e.message; }
    };
    return card("Saved presets", list.length ? `${fmt(list.length)} ${list.length === 1 ? "view" : "views"} · yours and the public ones` : null,
      list.length ? h("div", { class: "table-wrap" }, h("table", { class: "presets" },
        h("thead", {}, h("tr", {}, ["View", "Chart", "Owner", "Who sees it", ""].map((c) => h("th", { scope: "col" }, c)))),
        h("tbody", {}, list.map((p) => h("tr", {},
          h("td", {}, h("button", { type: "button", class: "link name", onclick: () => applyPreset(p) }, p.name),
            h("div", { class: "small" }, [settingsText(p.settings || {}), filtersText(p.filters || {})].filter(Boolean).join(" · "))),
          h("td", {}, chartTitle(p.chart_id), h("div", { class: "small" }, PAGES[p.page] ? PAGES[p.page].title : p.page)),
          h("td", {}, p.owner === state.me ? "you" : p.owner),
          h("td", {}, h("span", { class: "tag" }, p.public ? "public" : "private")),
          h("td", { class: "actions" },
            h("button", { type: "button", class: "btn sls ghost small-btn", onclick: () => applyPreset(p) }, "Open"),
            p.series && state.admin ? h("button", { type: "button", class: "btn sls ghost small-btn", title: board ? `Add to ${board.name}` : "Make a dashboard of saved views",
              onclick: () => addTo(p) }, "Add to dashboard") : null,
            p.owner === state.me ? h("button", { type: "button", class: "x", "aria-label": `Delete ${p.name}`, onclick: () => del(p) }, "×") : null)))))) 
        : h("p", { class: "small" }, "No saved views yet. Every chart has Save view: its switches and this page's filters, kept under a name, for you or for everyone."),
      out);
  }

  // ------------------------------------------------------------------ the frame

  function setUpdated(ms) { $("updated").textContent = ms ? `Numbers from ${new Date(ms).toISOString().slice(11, 16)} UTC` : ""; }

  function segment(container, options, current, pick) {
    container.replaceChildren(...options.map(([value, label]) =>
      h("button", { type: "button", "aria-pressed": String(value === current), onclick: () => pick(value) }, label)));
  }

  async function loadEras() {
    if (SNAPSHOT && !SNAPSHOT.get["/v1/eras"]) { state.eras = []; return; }
    try {
      const d = await api("/v1/eras");
      state.eras = d.eras || [];
      state.milestones = d.milestones || [];
    } catch (e) { state.eras = []; }
    if (state.era && !eraById(state.era)) state.era = "";
  }

  function eraFilter(meta) {
    const box = $("seg-era");
    const show = !!(meta.era && state.eras && state.eras.length);
    box.hidden = !show;
    if (!show) return;
    const sel = h("select", { id: "era-select", class: "era-select", "aria-label": "Era" },
      h("option", { value: "" }, "All time"),
      state.eras.map((e) => h("option", { value: e.id, selected: e.id === state.era || null }, e.name + (e.kind === "planned" ? " (planned)" : ""))));
    sel.addEventListener("change", () => { state.era = sel.value; getCache.clear(); render(); });
    box.replaceChildren(h("label", { class: "era-lab", for: "era-select" }, "Era"), sel);
  }

  const PAGE_FNS = () => ({ overview, players, cohorts, eras: erasPage, playtypes: playtypesPage, retention, matches, health,
    ftue: ftuePage, economy: economyPage, social: socialPage, query: queryPage, dashboards: dashboardsPage });

  let renderTicket = 0;
  async function render() {
    const ticket = ++renderTicket;
    const meta = PAGES[state.page];
    document.title = `${meta.title} · Froglet Analytics`;
    $("page-title").textContent = state.page === "players" && state.person ? "Player profile" : meta.title;
    const era = state.era && meta.era && eraById(state.era);
    const eyebrow = [meta.admin ? "admin" : null, meta.build ? `${state.build} builds` : null,
      meta.days ? (era ? era.name : `last ${state.page === "health" ? state.healthDays : state.days} days`) : null,
      meta.play && state.play !== "all" ? GROUP_LABEL[state.play] : null].filter(Boolean);
    $("eyebrow").textContent = eyebrow.length ? eyebrow.join(" · ") : "Froglet Analytics";
    for (const a of document.querySelectorAll(".nav")) {
      if (a.dataset.page === state.page) a.setAttribute("aria-current", "page");
      else a.removeAttribute("aria-current");
    }
    $("admin-nav").hidden = !state.admin;
    eraFilter(meta);
    $("seg-build").hidden = !meta.build;
    $("seg-days").hidden = !meta.days;
    $("seg-play").hidden = !meta.play;
    segment($("seg-build"), BUILDS, state.build, (b) => { state.build = b; getCache.clear(); render(); });
    if (meta.days && era) {
      // an era REPLACES the range: show its dates where the range was, and a way back to the range
      $("seg-days").replaceChildren(h("span", { class: "era-dates num", title: "The era sets the dates; pick All time in Era to use the range again" },
        `${shortDay(era.from)} to ${shortDay(era.to)}`),
        h("button", { type: "button", onclick: () => { state.era = ""; getCache.clear(); render(); } }, "Use range"));
    } else if (meta.days) {
      const current = state.page === "health" ? state.healthDays : state.days;
      segment($("seg-days"), meta.days.map((d) => [d, `${d} d`]), current, (d) => {
        if (state.page === "health") state.healthDays = d; else state.days = d; getCache.clear(); render();
      });
    }
    if (meta.play) segment($("seg-play"), PLAYS, state.play, (p) => { state.play = p; getCache.clear(); render(); });
    if (["ask", "erasure", "eras", "query", "dashboards"].includes(state.page)) setUpdated(null);
    const page = $("page");
    $("preview-note").hidden = !SNAPSHOT;
    if (SNAPSHOT) $("preview-note").textContent = SNAPSHOT.note;
    $("sign-out").hidden = !!SNAPSHOT;
    if (!key()) { page.replaceChildren(); showGate(); return; }
    state.unfiltered = [];
    try {
      const parts = state.page === "ask" ? askPage()
        : state.page === "erasure" ? (state.admin ? erasurePage() : [empty("Erasure needs an admin key.")])
        : await PAGE_FNS()[state.page]();
      if (ticket !== renderTicket) return;
      const note = $("unfiltered");
      const uf = (state.unfiltered || []).filter((k) => k !== "lifespans");
      const filtering = state.play !== "all" || state.era;
      const ignored = [!meta.play && state.play !== "all" && "the Play filter", !meta.era && state.era && "the Era filter"].filter(Boolean);
      if (uf.includes("page") || (ignored.length && meta.days)) {
        note.hidden = false;
        note.textContent = `This page reads every play type and its own range: ${ignored.join(" and ") || "the Play and Era filters"} ${ignored.length > 1 ? "do" : "does"} not reach it.`;
      } else {
        note.hidden = !(uf.length && filtering);
        note.textContent = note.hidden ? "" : `Not split by ${filterWord()} on this page: ${uf.join(", ").replace(/[_.]/g, " ")}. Those numbers carry the "all" tag.`;
      }
      page.replaceChildren(...parts.filter(Boolean));
    } catch (e) {
      if (ticket !== renderTicket) return;
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
    // Take the key out of whatever was pasted: `key read ian` prints "read key for ian (shown once): fa_read_...".
    const typed = $("gate-key").value.trim(), found = typed.match(/fa_(?:read|admin)_[A-Za-z0-9_-]+/);
    setKey(found ? found[0] : typed);
    $("gate").hidden = true;
    $("gate-key").value = "";
    await whoAmI();
    await loadEras();
    await loadPresets();
    render();
  });
  $("sign-out").addEventListener("click", () => { setKey(""); state.admin = false; state.profiles = false; $("admin-nav").hidden = true; $("who-name").textContent = "Not signed in"; showGate(); });

  async function whoAmI() {
    if (!key()) return;
    try {
      const me = await api("/v1/me");
      state.profiles = !!me.profiles;
      state.admin = !!me.admin;
      state.me = me.name || "";
      // Say the key's kind in words: a read key that happens to be named "admin" once read as an admin (2026-11-06).
      $("who-name").textContent = `${me.name} · ${me.admin ? "admin key" : me.profiles ? "read key, profiles" : "read key"}`;
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
  whoAmI().then(loadEras).then(loadPresets).then(route);
})();
