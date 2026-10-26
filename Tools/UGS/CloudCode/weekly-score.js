// weekly-score.js - UGS Cloud Code script: the only writer of the weekly-challenge leaderboards
// (fair play, Threat Board T2).
//
// The client used to post any time it liked straight to UGS Leaderboards; the only check was "not
// negative" (Docs/AntiCheat/README.md section 2, T2). With the Access Control policy in
// Tools/UGS/AccessControl/project-policy.json applied, players can no longer write a leaderboard,
// so every weekly time goes through this script, which checks it before posting it.
//
// Params: leaderboardId, seconds, periodKey, avatarId (optional)
// Returns: { ok, reason }
//
// Deploy and switch-on steps: Tools/UGS/README.md. Tests: node Tools/UGS/CloudCode/weekly-score.test.cjs

const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const SUBMITS_KEY = "WEEKLY_SCORE_SUBMITS";

const RULES = {
  // The boards this script may write: the world board and any regional board named after it
  // (WeeklyChallengeCatalog.asset leaderboardId / regionalLeaderboards).
  boardIds: ["weekly_challenge"],
  boardPrefixes: ["weekly_challenge_"],
  // No weekly challenge can be completed faster than this. Raise it when a week's course is known to
  // need longer (per-board overrides below); the point is to make impossible times impossible to post.
  minSeconds: 20,
  minSecondsByBoard: {},
  maxSeconds: 3 * 60 * 60,
  // One completion every this many seconds per player, across boards (a regional post rides along).
  minIntervalSeconds: 20,
  // How many times a player may post in one UTC week before the board stops taking their times.
  maxSubmitsPerWeek: 200,
};

function weekKey(ms) {
  const d = new Date(ms);
  const day = (d.getUTCDay() + 6) % 7; // Monday = 0
  const monday = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate() - day));
  return monday.toISOString().slice(0, 10);
}

function boardAllowed(id) {
  if (typeof id !== "string" || id.length === 0 || id.length > 50) return false;
  return RULES.boardIds.includes(id) || RULES.boardPrefixes.some((p) => id.startsWith(p));
}

// The rules, as a pure function: (params, submit history, now) -> { ok, reason }.
function check(params, history, nowMs) {
  if (!boardAllowed(params.leaderboardId)) return { ok: false, reason: "unknown_board" };
  const s = params.seconds;
  if (typeof s !== "number" || !Number.isFinite(s)) return { ok: false, reason: "bad_time" };
  const floor = RULES.minSecondsByBoard[params.leaderboardId] ?? RULES.minSeconds;
  if (s < floor) return { ok: false, reason: "impossible_time" };
  if (s > RULES.maxSeconds) return { ok: false, reason: "bad_time" };
  if (params.periodKey !== undefined && (typeof params.periodKey !== "string" || params.periodKey.length > 64))
    return { ok: false, reason: "bad_period" };

  const week = weekKey(nowMs);
  const sameRun = history.lastSeconds === s && history.lastBoard !== params.leaderboardId &&
                  nowMs - (history.lastAt || 0) < RULES.minIntervalSeconds * 1000;
  if (!sameRun && nowMs - (history.lastAt || 0) < RULES.minIntervalSeconds * 1000) return { ok: false, reason: "too_soon" };
  const count = history.week === week ? history.count || 0 : 0;
  if (!sameRun && count >= RULES.maxSubmitsPerWeek) return { ok: false, reason: "weekly_limit" };
  return { ok: true, week, count: sameRun ? count : count + 1 };
}

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const saves = new DataApi(context);

  const res = await saves.getProtectedItems(projectId, playerId, [SUBMITS_KEY]);
  const row = res && res.data && res.data.results ? res.data.results.find((r) => r.key === SUBMITS_KEY) : null;
  const history = row ? row.value : {};
  const now = Date.now();

  const verdict = check(params, history, now);
  if (!verdict.ok) {
    logger.info(`weekly score refused (${verdict.reason}): ${params.leaderboardId} ${params.seconds}`);
    return { ok: false, reason: verdict.reason };
  }

  // The same keys the game reads back (WeeklyChallengeRanking.PeriodMetadataKey / AvatarMetadataKey).
  const metadata = {};
  if (params.periodKey) metadata.w = params.periodKey;
  if (Number.isInteger(params.avatarId) && params.avatarId >= 0) metadata.a = params.avatarId;

  const boards = new LeaderboardsApi(context);
  await boards.addLeaderboardPlayerScore(projectId, params.leaderboardId, playerId, { score: params.seconds, metadata });

  const body = {
    key: SUBMITS_KEY,
    value: { week: verdict.week, count: verdict.count, lastAt: now, lastSeconds: params.seconds, lastBoard: params.leaderboardId },
  };
  if (row && row.writeLock) body.writeLock = row.writeLock;
  try { await saves.setProtectedItem(projectId, playerId, body); }
  catch (e) { logger.warning(`weekly score posted but history not saved: ${e.message}`); }
  return { ok: true, reason: "" };
};

module.exports.params = {
  leaderboardId: { type: "String", required: true },
  seconds: { type: "Numeric", required: true },
  periodKey: { type: "String", required: false },
  avatarId: { type: "Numeric", required: false },
};

module.exports._test = { check, weekKey, RULES };
