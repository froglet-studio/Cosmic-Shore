// economy.js - UGS Cloud Code script: the server owns the crystal wallet (fair play, Threat Board T3).
//
// The client used to write its own balance into the PLAYER_PROFILE Cloud Save record, so anyone could
// give themselves any amount (Docs/AntiCheat/README.md section 2, T3). This script keeps the wallet in
// a PROTECTED Cloud Save key, PLAYER_ECONOMY, which a player can never write: only Cloud Code can.
// The game asks; this script checks the request against RULES and answers with the wallet as it now
// stands. The client mirrors that answer into PLAYER_PROFILE as a display cache only.
//
// One endpoint, one parameter `action`:
//   state                                  the wallet (and a one-time migration from PLAYER_PROFILE)
//   earn   source, amount                  credit, within the source's per-call, spacing and daily limits
//   spend  source, amount, item            debit, if the balance covers it; item is recorded for support
//   claim  rewardId, amount                a once-only reward (today's toy activity), credited once
//
// Deploy and switch-on steps: Tools/UGS/README.md. Tests: node Tools/UGS/CloudCode/economy.test.cjs

const { DataApi } = require("@unity-services/cloud-save-1.4");

const ECONOMY_KEY = "PLAYER_ECONOMY";
const PROFILE_KEY = "PLAYER_PROFILE";
const SCHEMA = 1;

// The rules. Change them here and redeploy; the game reads nothing from this table.
const RULES = {
  // The largest honest balance a pre-existing account can bring across in the one-time migration.
  migrationCap: 20000,
  earn: {
    // Scoreboard placement crystals: 200 / 50 / 0 by default (Scoreboard.placementCrystalRewards).
    game_placement:       { maxPerCall: 250, minIntervalSeconds: 30, dailyCap: 6000 },
    tournament_placement: { maxPerCall: 250, minIntervalSeconds: 30, dailyCap: 6000 },
    // Editor and test builds only (the Froglet Toolbox crystal field). Refused in production.
    debug:                { maxPerCall: 100000, minIntervalSeconds: 0, dailyCap: 1e9, nonProductionOnly: true },
  },
  spend: {
    vessel_unlock:  { maxPerCall: 100000 },
    hangar_upgrade: { maxPerCall: 100000 },
    debug:          { maxPerCall: 1e9, nonProductionOnly: true },
  },
  claim: {
    // DailyToyActivity.RewardIdFor(dayKey) = "toy-activity:yyyy-MM-dd". Today or yesterday (UTC) only.
    "toy-activity:": { maxAmount: 100, dayWindow: 1 },
  },
  spendLogLength: 50,
};

function emptyEconomy() {
  return {
    schema: SCHEMA,
    crystalBalance: 0,
    lifetimeCrystalsEarned: 0,
    lifetimeCrystalsSpent: 0,
    unlockedRewardIds: [],
    day: "",
    earnedToday: {},
    lastEarnAt: {},
    spendLog: [],
    migratedBalance: null,
  };
}

function dayKey(ms) {
  return new Date(ms).toISOString().slice(0, 10);
}

function isWholePositive(n) {
  return typeof n === "number" && Number.isInteger(n) && n > 0;
}

function view(econ, ok, reason) {
  return {
    ok,
    reason: reason || "",
    crystalBalance: econ.crystalBalance,
    lifetimeCrystalsEarned: econ.lifetimeCrystalsEarned,
    lifetimeCrystalsSpent: econ.lifetimeCrystalsSpent,
    unlockedRewardIds: econ.unlockedRewardIds.slice(),
  };
}

// Seeds a new wallet from the client-written PLAYER_PROFILE.Economy, once, capped.
function migrate(profile) {
  const econ = emptyEconomy();
  const old = profile && profile.Economy ? profile.Economy : null;
  if (!old) return econ;
  const bal = Number.isFinite(old.CrystalBalance) ? Math.max(0, Math.floor(old.CrystalBalance)) : 0;
  econ.crystalBalance = Math.min(bal, RULES.migrationCap);
  econ.migratedBalance = bal;
  econ.lifetimeCrystalsEarned = Math.max(0, Math.floor(old.LifetimeCrystalsEarned || 0));
  econ.lifetimeCrystalsSpent = Math.max(0, Math.floor(old.LifetimeCrystalsSpent || 0));
  if (Array.isArray(old.UnlockedRewardIds))
    econ.unlockedRewardIds = old.UnlockedRewardIds.filter((x) => typeof x === "string" && x.length <= 64).slice(0, 500);
  return econ;
}

// The rules, as a pure function: (wallet, request, now, environment) -> { ok, reason, changed }.
// It mutates `econ` only when it returns changed = true.
function apply(econ, params, nowMs, environmentName) {
  const production = environmentName === "production";
  const action = params.action;

  const today = dayKey(nowMs);
  if (econ.day !== today) {
    econ.day = today;
    econ.earnedToday = {};
  }

  if (action === "state") return { ok: true, changed: false };

  if (action === "earn") {
    const rule = RULES.earn[params.source];
    if (!rule) return { ok: false, reason: "unknown_source" };
    if (rule.nonProductionOnly && production) return { ok: false, reason: "not_in_production" };
    const amount = params.amount;
    if (!isWholePositive(amount) || amount > rule.maxPerCall) return { ok: false, reason: "bad_amount" };
    const last = econ.lastEarnAt[params.source] || 0;
    if (nowMs - last < rule.minIntervalSeconds * 1000) return { ok: false, reason: "too_soon" };
    const sofar = econ.earnedToday[params.source] || 0;
    if (sofar + amount > rule.dailyCap) return { ok: false, reason: "daily_cap" };
    econ.crystalBalance += amount;
    econ.lifetimeCrystalsEarned += amount;
    econ.earnedToday[params.source] = sofar + amount;
    econ.lastEarnAt[params.source] = nowMs;
    return { ok: true, changed: true };
  }

  if (action === "spend") {
    const rule = RULES.spend[params.source];
    if (!rule) return { ok: false, reason: "unknown_source" };
    if (rule.nonProductionOnly && production) return { ok: false, reason: "not_in_production" };
    const amount = params.amount;
    if (!isWholePositive(amount) || amount > rule.maxPerCall) return { ok: false, reason: "bad_amount" };
    if (econ.crystalBalance < amount) return { ok: false, reason: "insufficient" };
    econ.crystalBalance -= amount;
    econ.lifetimeCrystalsSpent += amount;
    const item = typeof params.item === "string" ? params.item.slice(0, 64) : "";
    econ.spendLog.push({ source: params.source, item, amount, at: nowMs });
    if (econ.spendLog.length > RULES.spendLogLength) econ.spendLog.splice(0, econ.spendLog.length - RULES.spendLogLength);
    return { ok: true, changed: true };
  }

  if (action === "claim") {
    const id = params.rewardId;
    if (typeof id !== "string" || id.length > 64) return { ok: false, reason: "bad_reward" };
    const prefix = Object.keys(RULES.claim).find((p) => id.startsWith(p));
    if (!prefix) return { ok: false, reason: "bad_reward" };
    const rule = RULES.claim[prefix];
    const day = id.slice(prefix.length);
    const allowed = [];
    for (let d = 0; d <= rule.dayWindow; d++) allowed.push(dayKey(nowMs - d * 86400000));
    if (!allowed.includes(day)) return { ok: false, reason: "wrong_day" };
    if (econ.unlockedRewardIds.includes(id)) return { ok: false, reason: "already_claimed" };
    const amount = params.amount === undefined || params.amount === null ? 0 : params.amount;
    if (!(Number.isInteger(amount) && amount >= 0 && amount <= rule.maxAmount)) return { ok: false, reason: "bad_amount" };
    econ.unlockedRewardIds.push(id);
    econ.crystalBalance += amount;
    econ.lifetimeCrystalsEarned += amount;
    return { ok: true, changed: true };
  }

  return { ok: false, reason: "unknown_action" };
}

async function loadEconomy(api, projectId, playerId) {
  const res = await api.getProtectedItems(projectId, playerId, [ECONOMY_KEY]);
  const item = res && res.data && res.data.results ? res.data.results.find((r) => r.key === ECONOMY_KEY) : null;
  if (item) return { econ: Object.assign(emptyEconomy(), item.value), writeLock: item.writeLock, created: false };

  const prof = await api.getItems(projectId, playerId, [PROFILE_KEY]);
  const p = prof && prof.data && prof.data.results ? prof.data.results.find((r) => r.key === PROFILE_KEY) : null;
  return { econ: migrate(p ? p.value : null), writeLock: undefined, created: true };
}

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi(context);
  const { projectId, playerId, environmentName } = context;

  for (let attempt = 0; attempt < 3; attempt++) {
    const { econ, writeLock, created } = await loadEconomy(api, projectId, playerId);
    const result = apply(econ, params, Date.now(), environmentName);
    // Nothing to store unless the request changed the wallet or the wallet was just migrated: a new
    // wallet is stored even when the request is refused, so the migration happens exactly once.
    if (!result.changed && !created) return view(econ, result.ok, result.reason);
    try {
      const body = { key: ECONOMY_KEY, value: econ };
      if (writeLock) body.writeLock = writeLock;
      await api.setProtectedItem(projectId, playerId, body);
      if (!result.ok) logger.info(`economy refused ${params.action}: ${result.reason}`);
      return view(econ, result.ok, result.reason);
    } catch (e) {
      const status = e && e.response ? e.response.status : 0;
      if (status === 409 && attempt < 2) continue; // someone else wrote first: reload and re-apply
      throw e;
    }
  }
};

module.exports.params = {
  action: { type: "String", required: true },
  source: { type: "String", required: false },
  amount: { type: "Numeric", required: false },
  item: { type: "String", required: false },
  rewardId: { type: "String", required: false },
};

// For the tests only; Cloud Code reads the default export and `params`.
module.exports._test = { apply, migrate, emptyEconomy, RULES };
