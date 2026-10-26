// trust.js - UGS Cloud Code script: each account's fair-play trust record, and which pool it plays in
// (fair play, phase B; Docs/AntiCheat/README.md sections 4.5 and 4.6).
//
// The record lives in Cloud Save PRIVATE custom data, one custom id per account ("fp-" + player id), so no player
// can read or write it (the JS SDK has no private class for player data; private custom data is the server-only
// store). It is rebuilt on every `status` call from what the other scripts already record:
//   - FAIRPLAY_SIGNALS (protected): host_result_mismatch (match-report.js), around_the_clock and
//     repetitive_results (match-report.js, bot farming);
//   - FAIRPLAY_CONDUCT (protected): the leave rate (match-report.js);
//   - FAIRPLAY_AGE (protected): the birth year, for the minors rule (R13).
//
// The ladder (README 4.6): shore -> watch (silent) -> undertow (automatic, adults only) -> restricted (a PERSON only).
// A minor never goes to the Undertow automatically: the account goes to `review` instead and plays on as before.
// Repeated quitting puts an adult in the quitters' pool. A person decides by editing the record's `review` field in
// the dashboard (Tools/UGS/README.md, "Reviewing fair-play accounts"); the next `status` applies it once.
//
// What the client learns: a presence-lobby suffix (empty on the shore, so it joins the lobby it always did), the twin leaderboard suffix, and a
// notice once one is owed (after RULES.noticeAfterMatches Undertow matches for cheating or automation; at once for a
// restriction a person made). Never the stage, the score or the reasons.
//
// The pool pass (a host checking a guest without anyone learning who is flagged): every `status` issues a fresh
// random pass, which the client shows only to a party it joins (a party-session player property). A host asks
// `verifyGuest` with the guest's id and pass; the server answers only for a pass that guest really holds, so nobody can
// ask about an account that has not joined their party. A flagged guest caught in a shore host's party gets a
// `pool_bypass` signal (the server checked it itself, nothing the host claimed), which keeps it in the Undertow.
//
// Params: action ("status" | "appeal" | "verifyGuest"), text (appeal), guestId and pass (verifyGuest)
// Returns: status -> { ok, lobby, board, notice, pass }; appeal -> { ok, reason, appeal };
//          verifyGuest -> { ok, verified, same }
//
// Tests: node Tools/UGS/CloudCode/trust.test.cjs

const { DataApi } = require("@unity-services/cloud-save-1.4");
// A pass is a random token. Node's crypto when the Cloud Code runtime offers it (it is a Node runtime), otherwise a
// Math.random token: either is unguessable enough for "show this to your party", and neither is a secret key.
function newPass() {
  try { return require("crypto").randomBytes(16).toString("hex"); }
  catch (e) { return Array.from({ length: 32 }, () => Math.floor(Math.random() * 16).toString(16)).join(""); }
}

const SIGNALS_KEY = "FAIRPLAY_SIGNALS";
const CONDUCT_KEY = "FAIRPLAY_CONDUCT";
const AGE_KEY = "FAIRPLAY_AGE";
const TRUST_KEY = "trust";
const QUEUE_ID = "fairplay-review";
const QUEUE_KEY = "queue";

const RULES = {
  decayDays: 30,              // a signal older than this no longer counts (clean play earns trust back)
  weights: { host_result_mismatch: 120, around_the_clock: 80, repetitive_results: 100, pool_bypass: 300 },
  watchAt: 100,
  undertowAt: 300,
  quit: { minMatches: 10, rate: 0.5 },   // over the last 20 matches (match-report.js RULES.recentMatches)
  noticeAfterMatches: 3,      // the owner's rule: three rounds in the Undertow, then tell them and offer an appeal
  pools: { shore: "", undertow: "u1", quitters: "q1" },   // shore stays the one PRESENCE_LOBBY every old client joins
  shadowBoardSuffix: "_undertow",
  // The Code of Conduct rule a notice names (Docs/Legal/CODE_OF_CONDUCT_TEMPLATE.md "The rules").
  rule: { cheating: 1, automated: 2, exploit: 3, quitting: 6 },
  kindOf: { host_result_mismatch: "cheating", around_the_clock: "automated", repetitive_results: "automated", pool_bypass: "cheating" },
  appealMaxChars: 500,
  adultAge: 18,
  queueMax: 500,
  verifyPerHour: 30,          // verifyGuest calls one host may make an hour
  maxSignals: 50,             // match-report.js RULES.maxSignals
};

const DAY = 86400000;

function emptyTrust() {
  return { stage: "shore", since: 0, matches: 0, reasons: [], clearedBefore: 0, appeal: null, review: null,
           restrictedUntil: 0, queued: false };
}

function treatAsMinor(birthYear, nowYear) {
  if (!birthYear) return true;
  return nowYear - birthYear - 1 < RULES.adultAge;
}

// Pure: apply a person's decision, once. Fields a reviewer writes in the dashboard:
//   review: { decision: "restrict" | "clear" | "uphold", until: "<ISO date>" (restrict), by, note }
function applyReview(t, nowMs) {
  const r = t.review;
  if (!r || r.appliedAt || typeof r.decision !== "string") return false;
  if (r.decision === "restrict") {
    const until = Date.parse(r.until);
    t.stage = "restricted";
    t.since = nowMs;
    t.restrictedUntil = Number.isFinite(until) ? until : nowMs + 30 * DAY;
  } else if (r.decision === "clear") {
    t.stage = "shore";
    t.clearedBefore = nowMs;          // every signal so far is set aside; a false positive leaves no mark
    t.reasons = [];
    t.matches = 0;
    t.restrictedUntil = 0;
    if (t.appeal) t.appeal = { ...t.appeal, state: "upheld", decidedAt: nowMs, by: r.by || "" };
  } else if (r.decision === "uphold") {
    // The measure stands: the appeal is refused, the stage is unchanged.
    if (t.appeal) t.appeal = { ...t.appeal, state: "refused", decidedAt: nowMs, by: r.by || "" };
  } else {
    return false;
  }
  t.review = { ...r, appliedAt: nowMs };   // the queue entry is step()'s to remove, so it is left alone here
  return true;
}

// Pure: the live reasons, the score and the stage from the other scripts' records.
function evaluate(t, signals, conduct, birthYear, nowMs) {
  const nowYear = new Date(nowMs).getUTCFullYear();
  const minor = treatAsMinor(birthYear, nowYear);
  const live = (Array.isArray(signals) ? signals : []).filter((s) => s && RULES.weights[s.kind] &&
    typeof s.at === "number" && s.at > t.clearedBefore && nowMs - s.at <= RULES.decayDays * DAY);
  const score = live.reduce((sum, s) => sum + RULES.weights[s.kind], 0);

  const recent = conduct && Array.isArray(conduct.recent) ? conduct.recent : [];
  const leaveRate = recent.length ? recent.filter((m) => m.left).length / recent.length : 0;
  const quitter = !minor && recent.length >= RULES.quit.minMatches && leaveRate >= RULES.quit.rate;

  let stage;
  if (t.stage === "restricted" && t.restrictedUntil > nowMs) stage = "restricted";   // only a person lifts it early
  else if (score >= RULES.undertowAt) stage = minor ? "review" : "undertow";
  else if (quitter) stage = "quitters";
  else if (score >= RULES.watchAt) stage = "watch";
  else stage = "shore";

  // The reasons kept: kind, when, the evidence the signal carried, so a reviewer and an appeal see what was found.
  const reasons = live.map((s) => ({ kind: s.kind, at: s.at, evidence: s.diff || s.durations || s.activeHours || null,
                                     reporters: s.reporters || undefined, review: s.review || undefined }));
  if (quitter) reasons.push({ kind: "leave_rate", at: nowMs, evidence: { leaveRate, matches: recent.length } });
  return { stage, score, minor, reasons, leaveRate };
}

// Pure: what one status call changes. Returns { trust, reply, queue: "add" | "remove" | null }.
function step(t, signals, conduct, birthYear, nowMs) {
  const reviewed = applyReview(t, nowMs);
  const e = evaluate(t, signals, conduct, birthYear, nowMs);
  const was = t.stage;
  if (e.stage !== was) {
    t.stage = e.stage;
    t.since = nowMs;
    t.matches = 0;                     // the three-match count starts when the account enters a pool
  }
  t.reasons = e.reasons;
  t.minor = e.minor;

  // A person sees every account the system moved on its own (undertow, review) and every open appeal.
  // A stage a person has already looked at (a decision applied since the account entered it) waits for nobody.
  const seen = t.review && t.review.appliedAt && t.review.appliedAt >= t.since;
  const wantsPerson = ((e.stage === "undertow" || e.stage === "review") && !seen) || (t.appeal && t.appeal.state === "open");
  let queue = null;
  if (wantsPerson && !t.queued) { t.queued = true; queue = "add"; }
  else if (!wantsPerson && t.queued) { t.queued = false; queue = "remove"; }

  return { trust: t, reply: reply(t, nowMs), queue, reviewed };
}

// The presence-lobby suffix the client appends to PRESENCE_LOBBY: empty for the shore.
function lobbyOf(stage) {
  const key = poolOf(stage);
  return key ? "_" + key : "";
}

function poolOf(stage) {
  if (stage === "undertow" || stage === "restricted") return RULES.pools.undertow;
  if (stage === "quitters") return RULES.pools.quitters;
  return RULES.pools.shore;                       // shore, watch and review play as before
}

function noticeKind(t) {
  if (t.stage === "quitters") return "quitting";
  const kinds = t.reasons.map((r) => RULES.kindOf[r.kind]).filter(Boolean);
  return kinds.includes("cheating") ? "cheating" : kinds[0] || "cheating";
}

// Pure: what the client is told. The notice is owed after the Undertow matches, or at once for a person's restriction.
function reply(t, nowMs) {
  const lobby = lobbyOf(t.stage);
  const board = t.stage === "undertow" || t.stage === "restricted" ? RULES.shadowBoardSuffix : "";
  let notice = null;
  const owed = (t.stage === "undertow" || t.stage === "quitters") && t.matches >= RULES.noticeAfterMatches;
  if (owed || t.stage === "restricted") {
    const kind = noticeKind(t);
    notice = {
      kind,
      rule: RULES.rule[kind] || 0,
      restricted: t.stage === "restricted",
      until: t.stage === "restricted" ? new Date(t.restrictedUntil).toISOString() : "",
      automated: t.stage !== "restricted",
      appeal: t.appeal ? t.appeal.state : "none",
    };
  }
  return { ok: true, lobby, board, notice, pass: t.pass || "" };
}

// Pure: a host's question about a guest. Answers only for the pass the guest holds now.
function verify(host, guest, pass) {
  if (!guest || !guest.pass || typeof pass !== "string" || pass !== guest.pass) return { ok: true, verified: false, same: false };
  return { ok: true, verified: true, same: poolOf(host.stage) === poolOf(guest.stage) };
}

// Pure: the hourly limit on verifyGuest, kept in the host's own record.
function allowVerify(t, nowMs) {
  const hour = Math.floor(nowMs / 3600000);
  if (!t.verifyWindow || t.verifyWindow.hour !== hour) t.verifyWindow = { hour, count: 0 };
  if (t.verifyWindow.count >= RULES.verifyPerHour) return false;
  t.verifyWindow.count++;
  return true;
}

// Pure: an appeal. One open at a time; the text is short and kept for the reviewer only.
function appeal(t, text, nowMs) {
  if (t.stage !== "undertow" && t.stage !== "quitters" && t.stage !== "restricted") return { ok: false, reason: "nothing_to_appeal" };
  if (t.appeal && t.appeal.state === "open") return { ok: false, reason: "already_open", appeal: "open" };
  const clean = typeof text === "string" ? text.slice(0, RULES.appealMaxChars) : "";
  t.appeal = { state: "open", openedAt: nowMs, text: clean };
  return { ok: true, reason: "", appeal: "open" };
}

function trustId(playerId) {
  return "fp-" + playerId;                       // custom ids are 1-50 characters; UGS player ids are 28
}

async function loadProtected(api, projectId, playerId, key) {
  const res = await api.getProtectedItems(projectId, playerId, [key]);
  const row = res && res.data && res.data.results ? res.data.results.find((r) => r.key === key) : null;
  return row ? row.value : null;
}

async function loadTrust(api, projectId, playerId) {
  const res = await api.getPrivateCustomItems(projectId, trustId(playerId), [TRUST_KEY]);
  const row = res && res.data && res.data.results ? res.data.results.find((r) => r.key === TRUST_KEY) : null;
  return { value: Object.assign(emptyTrust(), row ? row.value : {}), writeLock: row ? row.writeLock : undefined };
}

async function saveTrust(api, projectId, playerId, value, writeLock) {
  const body = { key: TRUST_KEY, value };
  if (writeLock) body.writeLock = writeLock;
  await api.setPrivateCustomItem(projectId, trustId(playerId), body);
}

async function addSignal(api, projectId, playerId, signal) {
  const res = await api.getProtectedItems(projectId, playerId, [SIGNALS_KEY]);
  const row = res && res.data && res.data.results ? res.data.results.find((r) => r.key === SIGNALS_KEY) : null;
  const signals = row && Array.isArray(row.value) ? row.value : [];
  signals.push(signal);
  if (signals.length > RULES.maxSignals) signals.splice(0, signals.length - RULES.maxSignals);
  const body = { key: SIGNALS_KEY, value: signals };
  if (row && row.writeLock) body.writeLock = row.writeLock;
  await api.setProtectedItem(projectId, playerId, body);
}

async function updateQueue(api, projectId, playerId, op, t, nowMs) {
  const res = await api.getPrivateCustomItems(projectId, QUEUE_ID, [QUEUE_KEY]);
  const row = res && res.data && res.data.results ? res.data.results.find((r) => r.key === QUEUE_KEY) : null;
  let queue = row && Array.isArray(row.value) ? row.value.filter((q) => q.id !== playerId) : [];
  if (op === "add") {
    queue.push({ id: playerId, stage: t.stage, minor: !!t.minor, appeal: t.appeal ? t.appeal.state : "none", since: nowMs,
                 record: "Cloud Save > Custom data (private) > " + trustId(playerId) });
    if (queue.length > RULES.queueMax) queue = queue.slice(queue.length - RULES.queueMax);
  }
  const body = { key: QUEUE_KEY, value: queue };
  if (row && row.writeLock) body.writeLock = row.writeLock;
  await api.setPrivateCustomItem(projectId, QUEUE_ID, body);
}

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi(context);
  const { projectId, playerId } = context;
  const nowMs = Date.now();
  const action = params.action || "status";

  if (action === "verifyGuest") {
    if (typeof params.guestId !== "string" || params.guestId.length === 0 || params.guestId.length > 64 || params.guestId === playerId)
      return { ok: false, verified: false, same: false };
    const hostRec = await loadTrust(api, projectId, playerId);
    if (!allowVerify(hostRec.value, nowMs)) return { ok: false, reason: "rate_limited", verified: false, same: false };
    try { await saveTrust(api, projectId, playerId, hostRec.value, hostRec.writeLock); } catch (e) { /* the count is best effort */ }
    const guest = (await loadTrust(api, projectId, params.guestId)).value;
    const out = verify(hostRec.value, guest, params.pass);
    // A flagged guest in a shore party: the server's own fact, so it is evidence. A flagged host taking a shore guest is
    // already isolated from the shore by its lobby; the guest is not at fault.
    if (out.verified && !out.same && poolOf(guest.stage) !== RULES.pools.shore && poolOf(hostRec.value.stage) === RULES.pools.shore) {
      try { await addSignal(api, projectId, params.guestId, { kind: "pool_bypass", at: nowMs, host: playerId }); }
      catch (e) { logger.warning(`pool_bypass not recorded: ${e.message}`); }
    }
    return out;
  }

  for (let attempt = 0; attempt < 3; attempt++) {
    const loaded = await loadTrust(api, projectId, playerId);
    const t = loaded.value;
    let out;
    let queueOp = null;
    if (action === "appeal") {
      out = appeal(t, params.text, nowMs);
      if (out.ok && !t.queued) { t.queued = true; queueOp = "add"; }
    } else if (action === "status") {
      t.pass = newPass();   // a fresh pass every sign-in; the old one stops verifying
      const signals = await loadProtected(api, projectId, playerId, SIGNALS_KEY);
      const conduct = await loadProtected(api, projectId, playerId, CONDUCT_KEY);
      const age = await loadProtected(api, projectId, playerId, AGE_KEY);
      const r = step(t, signals, conduct, age && age.birthYear, nowMs);
      out = r.reply;
      queueOp = r.queue;
    } else {
      return { ok: false, reason: "unknown_action" };
    }

    try {
      await saveTrust(api, projectId, playerId, t, loaded.writeLock);
    } catch (e) {
      if (e && e.response && e.response.status === 409 && attempt < 2) continue;
      throw e;
    }
    if (queueOp) {
      try { await updateQueue(api, projectId, playerId, queueOp, t, nowMs); }
      catch (e) { logger.warning(`trust saved; review queue not updated: ${e.message}`); }
    }
    return out;
  }
};

module.exports.params = {
  action: { type: "String", required: false },
  text: { type: "String", required: false },
  guestId: { type: "String", required: false },
  pass: { type: "String", required: false },
};

module.exports._test = { emptyTrust, applyReview, evaluate, step, reply, appeal, verify, allowVerify, poolOf, lobbyOf, trustId, RULES };
