// match-report.js - UGS Cloud Code script: every player reports each match it plays
// (fair play, Threat Board T4 and T9; Docs/AntiCheat/README.md sections 4.2 and 4.4).
//
// T9, quitting: a player reports `start` when a match begins and `end` when it finishes. A player who
// quits never sends `end`, so the match stays open and is counted as left at their next `start` (or after
// RULES.openTimeoutMinutes). The leaver is never trusted to report their own leaving.
//
// T4, host results: the match host is a player, and its game decides every score. Each `end` report carries
// the final scores as that player's game received them. The script compares it with the other human
// players' reports of the same match (the host's gameData.MatchId, which every player receives, and a start
// within RULES.sameMatchSeconds, since a replay in the same scene keeps the id) and, when
// they disagree, records a signal against the HOST. Agreement proves only that everyone saw the same thing:
// a host that lies identically to everyone is not caught here (that needs a server it does not run).
//
// Nothing is punished here. The record feeds the trust record and the Undertow later (phase B), and a person
// reviews before anything restricts play.
//
// Minors (Threat Board R13; README section 4.6): each `start` carries the birth year the age screen recorded
// (0 = unknown). It is kept in the protected key FAIRPLAY_AGE and can only move LATER (younger), so re-entering an
// older year never turns a child into an adult. A player who is not surely 18 (unknown year, or under 18 by the
// year alone) is treated as a minor: no quit record is kept about them (no behaviour profile, the UK Children's
// Code default), and every signal recorded about them says review: "human", which phase B must honour (no
// automatic Undertow for a child). Security checks still run: a minor host's mismatching results are recorded.
//
// Params: action ("start" | "end"), matchId, mode, hostId, players (JSON array of UGS ids),
//         scores (JSON array of { id, score }; end only), winner (end only), birthYear (start only; 0 = unknown)
// Returns: { ok, reason, matchesStarted, matchesLeft, leaveRate }
//
// Tests: node Tools/UGS/CloudCode/match-report.test.cjs

const { DataApi } = require("@unity-services/cloud-save-1.4");

const CONDUCT_KEY = "FAIRPLAY_CONDUCT";
const SIGNALS_KEY = "FAIRPLAY_SIGNALS";
const AGE_KEY = "FAIRPLAY_AGE";

const RULES = {
  recentMatches: 20,          // matches kept per player, for the leave rate and for comparing reports
  sameMatchSeconds: 180,      // two reports with the same host and mode, started this close, are one match
  openTimeoutMinutes: 90,     // an unfinished match this old counts as left
  maxPlayers: 8,
  maxSignals: 50,
  scoreTolerance: 0.001,
  adultAge: 18,               // surely this old by the birth year alone: nowYear - birthYear - 1 >= adultAge
  oldestAge: 100,
  keepConductForMinors: false, // quit counting is a behaviour profile; off for minors unless counsel says otherwise
};

// Pure: the stored birth year after a report. Only a later (younger) year replaces a stored one.
function mergeBirthYear(stored, reported, nowYear) {
  const valid = Number.isInteger(reported) && reported <= nowYear && reported >= nowYear - RULES.oldestAge;
  if (!valid) return stored || 0;
  if (!stored) return reported;
  return Math.max(stored, reported);
}

// Pure: true unless the player is surely an adult. A year alone leaves the birthday unknown, so a player born
// in Y is surely 18 only from Y + 19.
function treatAsMinor(birthYear, nowYear) {
  if (!birthYear) return true;
  return nowYear - birthYear - 1 < RULES.adultAge;
}

function reviewFor(birthYear, nowYear) {
  return treatAsMinor(birthYear, nowYear) ? "human" : "standard";
}

function emptyConduct() {
  return { matchesStarted: 0, matchesFinished: 0, matchesLeft: 0, open: null, recent: [] };
}

function isId(s) {
  return typeof s === "string" && s.length > 0 && s.length <= 64;
}

function leaveRate(c) {
  const window = c.recent.length;
  if (window === 0) return 0;
  return c.recent.filter((m) => m.left).length / window;
}

function summary(c, ok, reason) {
  return { ok, reason: reason || "", matchesStarted: c.matchesStarted, matchesLeft: c.matchesLeft, leaveRate: leaveRate(c) };
}

function pushRecent(c, entry) {
  c.recent.push(entry);
  if (c.recent.length > RULES.recentMatches) c.recent.splice(0, c.recent.length - RULES.recentMatches);
}

function closeOpenAsLeft(c, trackConduct) {
  if (!c.open) return;
  if (trackConduct) {
    c.matchesLeft++;
    pushRecent(c, { matchId: c.open.matchId, mode: c.open.mode, hostId: c.open.hostId, startedAt: c.open.at, left: true });
  }
  c.open = null;
}

// Pure: applies one report to one player's conduct record. Returns { ok, reason, report? }.
// trackConduct false (a minor): unfinished matches are dropped, never counted as left.
function apply(c, params, nowMs, trackConduct = true) {
  if (c.open && nowMs - c.open.at > RULES.openTimeoutMinutes * 60000) closeOpenAsLeft(c, trackConduct);

  if (typeof params.mode !== "string" || params.mode.length === 0 || params.mode.length > 48) return { ok: false, reason: "bad_mode" };
  if (!isId(params.hostId)) return { ok: false, reason: "bad_host" };
  if (!isId(params.matchId)) return { ok: false, reason: "bad_match" };
  const players = Array.isArray(params.players) ? params.players.filter(isId).slice(0, RULES.maxPlayers) : [];

  if (params.action === "start") {
    closeOpenAsLeft(c, trackConduct); // the previous match was never finished
    if (trackConduct) c.matchesStarted++;
    c.open = { matchId: params.matchId, mode: params.mode, hostId: params.hostId, at: nowMs, players };
    return { ok: true };
  }

  if (params.action === "end") {
    if (!c.open || c.open.matchId !== params.matchId || c.open.mode !== params.mode || c.open.hostId !== params.hostId)
      return { ok: false, reason: "no_open_match" };
    const scores = Array.isArray(params.scores)
      ? params.scores.filter((s) => s && isId(s.id) && typeof s.score === "number" && Number.isFinite(s.score)).slice(0, RULES.maxPlayers)
      : [];
    const report = {
      matchId: c.open.matchId, mode: c.open.mode, hostId: c.open.hostId, startedAt: c.open.at, endedAt: nowMs, left: false,
      players: c.open.players, scores, winner: typeof params.winner === "string" ? params.winner.slice(0, 64) : "",
    };
    if (trackConduct) c.matchesFinished++;
    c.open = null;
    pushRecent(c, report);
    return { ok: true, report };
  }

  return { ok: false, reason: "unknown_action" };
}

// Pure: the other player's report of the same match, if they made one.
function findSameMatch(otherConduct, report) {
  if (!otherConduct || !Array.isArray(otherConduct.recent)) return null;
  return otherConduct.recent.find((m) => !m.left && m.matchId === report.matchId && m.hostId === report.hostId && m.mode === report.mode &&
    Math.abs(m.startedAt - report.startedAt) <= RULES.sameMatchSeconds * 1000) || null;
}

// Pure: where two reports of one match disagree. [] = they agree.
function disagreements(a, b) {
  const out = [];
  const byId = new Map(b.scores.map((s) => [s.id, s.score]));
  for (const s of a.scores) {
    if (!byId.has(s.id)) continue;
    if (Math.abs(byId.get(s.id) - s.score) > RULES.scoreTolerance) out.push({ id: s.id, mine: s.score, theirs: byId.get(s.id) });
  }
  if (a.winner && b.winner && a.winner !== b.winner) out.push({ winner: true, mine: a.winner, theirs: b.winner });
  return out;
}

async function loadProtected(api, projectId, playerId, key) {
  const res = await api.getProtectedItems(projectId, playerId, [key]);
  const row = res && res.data && res.data.results ? res.data.results.find((r) => r.key === key) : null;
  return row ? { value: row.value, writeLock: row.writeLock } : { value: null, writeLock: undefined };
}

async function saveProtected(api, projectId, playerId, key, value, writeLock) {
  const body = { key, value };
  if (writeLock) body.writeLock = writeLock;
  await api.setProtectedItem(projectId, playerId, body);
}

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi(context);
  const { projectId, playerId } = context;
  const nowMs = Date.now(), nowYear = new Date(nowMs).getUTCFullYear();

  const birthYear = await updateBirthYear(api, projectId, playerId, params, nowYear);
  const trackConduct = !treatAsMinor(birthYear, nowYear) || RULES.keepConductForMinors;

  for (let attempt = 0; attempt < 3; attempt++) {
    const loaded = await loadProtected(api, projectId, playerId, CONDUCT_KEY);
    const conduct = Object.assign(emptyConduct(), loaded.value || {});
    const result = apply(conduct, params, nowMs, trackConduct);
    if (!result.ok) return summary(conduct, false, result.reason);

    try {
      await saveProtected(api, projectId, playerId, CONDUCT_KEY, conduct, loaded.writeLock);
    } catch (e) {
      if (e && e.response && e.response.status === 409 && attempt < 2) continue;
      throw e;
    }

    if (result.report) {
      try { await compareWithOthers(api, projectId, playerId, result.report, logger); }
      catch (e) { logger.warning(`match report saved; comparison skipped: ${e.message}`); }
    }
    return summary(conduct, true, "");
  }
};

// Keeps FAIRPLAY_AGE: a start report's birth year is merged in (only ever younger). Returns the stored year.
async function updateBirthYear(api, projectId, playerId, params, nowYear) {
  const loaded = await loadProtected(api, projectId, playerId, AGE_KEY);
  const stored = loaded.value && Number.isInteger(loaded.value.birthYear) ? loaded.value.birthYear : 0;
  if (params.action !== "start") return stored;
  const merged = mergeBirthYear(stored, typeof params.birthYear === "number" ? params.birthYear : Number(params.birthYear), nowYear);
  if (merged !== stored) {
    try { await saveProtected(api, projectId, playerId, AGE_KEY, { birthYear: merged }, loaded.writeLock); }
    catch (e) { if (!(e && e.response && e.response.status === 409)) throw e; } // a parallel start wrote it
  }
  return merged;
}

async function compareWithOthers(api, projectId, playerId, report, logger) {
  const others = report.players.filter((id) => id !== playerId);
  for (const other of others) {
    const theirs = (await loadProtected(api, projectId, other, CONDUCT_KEY)).value;
    const same = findSameMatch(theirs, report);
    if (!same) continue; // they have not reported yet; their report will compare against this one
    const diff = disagreements(report, same);
    if (diff.length === 0) continue;

    const host = await loadProtected(api, projectId, report.hostId, SIGNALS_KEY);
    const hostAge = (await loadProtected(api, projectId, report.hostId, AGE_KEY)).value;
    const review = reviewFor(hostAge && hostAge.birthYear, new Date().getUTCFullYear());
    const signals = Array.isArray(host.value) ? host.value : [];
    signals.push({ kind: "host_result_mismatch", mode: report.mode, startedAt: report.startedAt, reporters: [playerId, other], diff, review, at: Date.now() });
    if (signals.length > RULES.maxSignals) signals.splice(0, signals.length - RULES.maxSignals);
    await saveProtected(api, projectId, report.hostId, SIGNALS_KEY, signals, host.writeLock);
    logger.info(`host_result_mismatch: host ${report.hostId}, ${report.mode}, reporters ${playerId} and ${other}`);
  }
}

module.exports.params = {
  action: { type: "String", required: true },
  matchId: { type: "String", required: true },
  mode: { type: "String", required: true },
  hostId: { type: "String", required: true },
  players: { type: "JSON", required: false },
  scores: { type: "JSON", required: false },
  winner: { type: "String", required: false },
  birthYear: { type: "Numeric", required: false },
};

module.exports._test = { apply, findSameMatch, disagreements, leaveRate, emptyConduct, mergeBirthYear, treatAsMinor, reviewFor, RULES };
