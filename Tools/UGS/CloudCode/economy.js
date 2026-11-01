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
//   unlockVessel   vessel                  buys a vessel at its CATALOG price and records it (T3: unlock records)
//   upgradeHangar  vessel, element         buys the next Hangar level at its CATALOG price and records it
//   migrateUnlocks vessels, hangar         once per account: brings the client-written unlocks across, capped
//
// Unlock records (2026-10-26): the vessels an account owns and its Hangar levels live in the same protected wallet as
// the crystals that pay for them, written only here, in the same write as the spend. The client mirrors them into its
// own saves (HANGAR_DATA, PLAYER_PROFILE) as a display cache, and an edited cache is replaced by this record. Prices
// come from the shipped Unity assets through Tools/Build/author_economy_catalog.py (the CATALOG block below).
//
// Deploy and switch-on steps: Tools/UGS/README.md. Tests: node Tools/UGS/CloudCode/economy.test.cjs

const { DataApi } = require("@unity-services/cloud-save-1.4");

const ECONOMY_KEY = "PLAYER_ECONOMY";
const PROFILE_KEY = "PLAYER_PROFILE";
const SCHEMA = 2;   // 2: vessels and hangar levels (unlock records)

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
  // The largest crystal value of unlocks an account bought before the server owned them that it may bring across once
  // (vessel prices plus Hangar level prices, at CATALOG prices). The rest stays locked.
  unlockMigrationCap: 20000,
};

// <catalog> written by Tools/Build/author_economy_catalog.py from the shipped assets; do not edit by hand
const CATALOG = {
  vessels: {
    "Angler": { cost: 100, starter: false },
    "Brittle Star": { cost: 100, starter: false },
    "Dolphin": { cost: 4000, starter: false },
    "Falcon": { cost: 4000, starter: false },
    "Goby": { cost: 0, starter: true },
    "Grizzly": { cost: 4000, starter: true },
    "Honeyguide": { cost: 4000, starter: true },
    "Jacks": { cost: 4000, starter: true },
    "Manta": { cost: 4000, starter: false },
    "Meerkat": { cost: 0, starter: true },
    "Orca": { cost: 4000, starter: true },
    "Rhino": { cost: 4000, starter: false },
    "Scarab": { cost: 100, starter: false },
    "Serpent": { cost: 4000, starter: false },
    "Shrike": { cost: 100, starter: false },
    "Sparrow": { cost: 4000, starter: false },
    "Squirrel": { cost: 0, starter: true },
    "Starlings": { cost: 100, starter: false },
    "Stoat": { cost: 0, starter: true },
    "Termite": { cost: 4000, starter: false },
    "Thresher": { cost: 100, starter: false },
    "Tiger Moth": { cost: 100, starter: true },
    "Urchin": { cost: 4000, starter: false },
    "Wrasse": { cost: 4000, starter: true },
  },
  hangar: { perLevel: 100, maxLevel: 5 },
};
// </catalog>

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
    vessels: [],                 // names of vessels bought (starters are owned by everyone and are not listed)
    hangar: {},                  // "<vessel>:<element>" -> level (VesselClassType and Element enum values)
    unlocksMigrated: false,
  };
}

function hangarList(econ) {
  return Object.entries(econ.hangar || {}).map(([k, level]) => {
    const [vessel, element] = k.split(":").map(Number);
    return { vessel, element, level };
  });
}

function isSmallIndex(n) {
  return Number.isInteger(n) && n >= 0 && n < 256;
}

function recordSpend(econ, source, item, amount, nowMs) {
  econ.crystalBalance -= amount;
  econ.lifetimeCrystalsSpent += amount;
  econ.spendLog.push({ source, item, amount, at: nowMs });
  if (econ.spendLog.length > RULES.spendLogLength) econ.spendLog.splice(0, econ.spendLog.length - RULES.spendLogLength);
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
    vessels: (econ.vessels || []).slice(),
    hangar: hangarList(econ),
    unlocksMigrated: !!econ.unlocksMigrated,
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

  if (action === "unlockVessel") {
    const name = params.vessel;
    const item = typeof name === "string" ? CATALOG.vessels[name] : undefined;
    if (!item) return { ok: false, reason: "unknown_vessel" };
    if (item.starter) return { ok: true, changed: false };               // everyone owns a starter
    if (econ.vessels.includes(name)) return { ok: false, reason: "already_owned" };
    if (econ.crystalBalance < item.cost) return { ok: false, reason: "insufficient" };
    if (item.cost > 0) recordSpend(econ, "vessel_unlock", "vessel:" + name, item.cost, nowMs);
    econ.vessels.push(name);
    return { ok: true, changed: true };
  }

  if (action === "upgradeHangar") {
    if (!isSmallIndex(params.vessel) || !isSmallIndex(params.element)) return { ok: false, reason: "bad_slot" };
    const key = params.vessel + ":" + params.element;
    const level = econ.hangar[key] || 0;
    if (level >= CATALOG.hangar.maxLevel) return { ok: false, reason: "max_level" };
    const cost = CATALOG.hangar.perLevel * (level + 1);                 // HangarUpgradeStore.CostOfNext
    if (econ.crystalBalance < cost) return { ok: false, reason: "insufficient" };
    recordSpend(econ, "hangar_upgrade", "hangar:" + key + ":" + (level + 1), cost, nowMs);
    econ.hangar[key] = level + 1;
    return { ok: true, changed: true };
  }

  if (action === "migrateUnlocks") {
    if (econ.unlocksMigrated) return { ok: false, reason: "already_migrated" };
    let budget = RULES.unlockMigrationCap;
    const names = Array.isArray(params.vessels) ? params.vessels.slice(0, 64) : [];
    for (const name of names) {
      const item = typeof name === "string" ? CATALOG.vessels[name] : undefined;
      if (!item || item.starter || econ.vessels.includes(name) || item.cost > budget) continue;
      budget -= item.cost;
      econ.vessels.push(name);
    }
    const levels = Array.isArray(params.hangar) ? params.hangar.slice(0, 256) : [];
    for (const h of levels) {
      if (!h || !isSmallIndex(h.vessel) || !isSmallIndex(h.element) || !Number.isInteger(h.level)) continue;
      const key = h.vessel + ":" + h.element;
      let level = econ.hangar[key] || 0;
      const want = Math.min(h.level, CATALOG.hangar.maxLevel);
      while (level < want) {
        const cost = CATALOG.hangar.perLevel * (level + 1);
        if (cost > budget) break;
        budget -= cost;
        level++;
      }
      if (level > 0) econ.hangar[key] = level;
    }
    econ.unlocksMigrated = true;
    return { ok: true, changed: true };
  }

  return { ok: false, reason: "unknown_action" };
}

async function loadEconomy(api, projectId, playerId) {
  const res = await api.getProtectedItems(projectId, playerId, [ECONOMY_KEY]);
  const item = res && res.data && res.data.results ? res.data.results.find((r) => r.key === ECONOMY_KEY) : null;
  if (item) {
    const econ = Object.assign(emptyEconomy(), item.value);
    econ.vessels = Array.isArray(econ.vessels) ? econ.vessels : [];
    econ.hangar = econ.hangar && typeof econ.hangar === "object" ? econ.hangar : {};
    econ.schema = SCHEMA;
    return { econ, writeLock: item.writeLock, created: false };
  }

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
  vessel: { type: "Any", required: false },
  element: { type: "Numeric", required: false },
  vessels: { type: "JSON", required: false },
  hangar: { type: "JSON", required: false },
};

// For the tests only; Cloud Code reads the default export and `params`.
module.exports._test = { apply, migrate, emptyEconomy, view, RULES, CATALOG };
