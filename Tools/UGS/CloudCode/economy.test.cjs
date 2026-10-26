// Tests for economy.js: node Tools/UGS/CloudCode/economy.test.cjs
// Runs the real script against an in-memory Cloud Save stand-in (no network, no UGS account).
const assert = require("node:assert/strict");
const Module = require("node:module");
const path = require("node:path");

// A stand-in for @unity-services/cloud-save-1.4: protected and default items per player, write locks.
const store = { protected: new Map(), default: new Map(), lockSeq: 0, failNextWriteWith409: false };
class FakeDataApi {
  async getProtectedItems(_p, player, keys) {
    const rows = keys.map((k) => store.protected.get(player + "/" + k)).filter(Boolean);
    return { data: { results: rows.map((r) => ({ key: r.key, value: JSON.parse(JSON.stringify(r.value)), writeLock: r.writeLock })) } };
  }
  async getItems(_p, player, keys) {
    const rows = keys.map((k) => store.default.get(player + "/" + k)).filter(Boolean);
    return { data: { results: rows } };
  }
  async setProtectedItem(_p, player, body) {
    const id = player + "/" + body.key;
    const cur = store.protected.get(id);
    if (store.failNextWriteWith409) {
      store.failNextWriteWith409 = false;
      const e = new Error("conflict"); e.response = { status: 409 }; throw e;
    }
    if (cur && body.writeLock !== cur.writeLock) { const e = new Error("conflict"); e.response = { status: 409 }; throw e; }
    store.protected.set(id, { key: body.key, value: JSON.parse(JSON.stringify(body.value)), writeLock: "w" + ++store.lockSeq });
    return { data: { writeLock: "w" + store.lockSeq } };
  }
}
const realLoad = Module._load;
Module._load = function (req, parent, isMain) {
  if (req === "@unity-services/cloud-save-1.4") return { DataApi: FakeDataApi };
  return realLoad.apply(this, arguments);
};
const economy = require(path.join(__dirname, "economy.js"));
const { apply, migrate, emptyEconomy } = economy._test;

const logger = { info() {}, warning() {}, error() {} };
const ctx = (player, env = "production") => ({ projectId: "p", playerId: player, environmentName: env });
const call = (player, params, env) => economy({ params, context: ctx(player, env), logger });

let passed = 0;
async function test(name, fn) { await fn(); passed++; console.log("ok -", name); }

(async () => {
  await test("a new player starts at 0 and the wallet is stored once", async () => {
    const r = await call("a", { action: "state" });
    assert.equal(r.ok, true); assert.equal(r.crystalBalance, 0);
    assert.ok(store.protected.has("a/PLAYER_ECONOMY"));
  });

  await test("migration brings the old profile balance across, capped", async () => {
    store.default.set("b/PLAYER_PROFILE", { key: "PLAYER_PROFILE", value: { Economy: { CrystalBalance: 999999, UnlockedRewardIds: ["toy-activity:2026-10-01"] } } });
    const r = await call("b", { action: "state" });
    assert.equal(r.crystalBalance, economy._test.RULES.migrationCap);
    assert.deepEqual(r.unlockedRewardIds, ["toy-activity:2026-10-01"]);
    // A later edit of the client-written profile changes nothing: the server wallet wins.
    store.default.set("b/PLAYER_PROFILE", { key: "PLAYER_PROFILE", value: { Economy: { CrystalBalance: 5 } } });
    assert.equal((await call("b", { action: "state" })).crystalBalance, economy._test.RULES.migrationCap);
  });

  await test("an honest placement reward is credited; a forged one is refused", async () => {
    assert.equal((await call("c", { action: "earn", source: "game_placement", amount: 200 })).crystalBalance, 200);
    const forged = await call("c2", { action: "earn", source: "game_placement", amount: 1000000 });
    assert.equal(forged.ok, false); assert.equal(forged.reason, "bad_amount"); assert.equal(forged.crystalBalance, 0);
  });

  await test("rewards cannot be farmed faster than a match", async () => {
    const r = await call("c", { action: "earn", source: "game_placement", amount: 50 });
    assert.equal(r.ok, false); assert.equal(r.reason, "too_soon"); assert.equal(r.crystalBalance, 200);
  });

  await test("the daily cap holds", () => {
    const e = emptyEconomy(); let t = Date.UTC(2026, 9, 24, 1); let ok = 0;
    for (let i = 0; i < 100; i++) { if (apply(e, { action: "earn", source: "game_placement", amount: 200 }, t, "production").ok) ok++; t += 31000; }
    assert.equal(ok, 30); assert.equal(e.crystalBalance, 6000);
    // The next UTC day starts a fresh allowance.
    assert.equal(apply(e, { action: "earn", source: "game_placement", amount: 200 }, Date.UTC(2026, 9, 25, 0, 1), "production").ok, true);
  });

  await test("unknown sources, fractions and negatives are refused", () => {
    const e = emptyEconomy();
    for (const p of [{ source: "free_money", amount: 5 }, { source: "game_placement", amount: 1.5 }, { source: "game_placement", amount: -5 }, { source: "game_placement", amount: "200" }])
      assert.equal(apply(e, { action: "earn", ...p }, Date.now(), "production").ok, false);
    assert.equal(e.crystalBalance, 0);
  });

  await test("debug crystals work in test environments and never in production", () => {
    const e = emptyEconomy();
    assert.equal(apply(e, { action: "earn", source: "debug", amount: 5000 }, Date.now(), "production").reason, "not_in_production");
    assert.equal(apply(e, { action: "earn", source: "debug", amount: 5000 }, Date.now(), "development").ok, true);
  });

  await test("spending needs the balance and records the item", async () => {
    const r = await call("c", { action: "spend", source: "vessel_unlock", amount: 4000, item: "Squirrel" });
    assert.equal(r.ok, false); assert.equal(r.reason, "insufficient"); assert.equal(r.crystalBalance, 200);
    const s = await call("c", { action: "spend", source: "hangar_upgrade", amount: 150, item: "Manta:Boost" });
    assert.equal(s.ok, true); assert.equal(s.crystalBalance, 50);
    assert.equal(store.protected.get("c/PLAYER_ECONOMY").value.spendLog.at(-1).item, "Manta:Boost");
  });

  await test("today's toy reward pays once; other days and replays are refused", () => {
    const e = emptyEconomy(); const now = Date.UTC(2026, 9, 24, 12);
    assert.equal(apply(e, { action: "claim", rewardId: "toy-activity:2026-10-24", amount: 10 }, now, "production").ok, true);
    assert.equal(apply(e, { action: "claim", rewardId: "toy-activity:2026-10-24", amount: 10 }, now, "production").reason, "already_claimed");
    assert.equal(apply(e, { action: "claim", rewardId: "toy-activity:2026-10-23", amount: 10 }, now, "production").ok, true);
    assert.equal(apply(e, { action: "claim", rewardId: "toy-activity:2026-09-01", amount: 10 }, now, "production").reason, "wrong_day");
    assert.equal(apply(e, { action: "claim", rewardId: "toy-activity:2026-10-25", amount: 10 }, now, "production").reason, "wrong_day");
    assert.equal(apply(e, { action: "claim", rewardId: "anything:2026-10-24", amount: 10 }, now, "production").reason, "bad_reward");
    assert.equal(e.crystalBalance, 20);
  });

  await test("a concurrent write is retried, not lost", async () => {
    await call("d", { action: "earn", source: "debug", amount: 10 }, "development");
    store.failNextWriteWith409 = true;
    const r = await call("d", { action: "spend", source: "debug", amount: 3 }, "development");
    assert.equal(r.ok, true); assert.equal(r.crystalBalance, 7);
  });

  await test("a client cannot write the wallet key: only the protected API stores it", () => {
    assert.ok(!store.default.has("c/PLAYER_ECONOMY"));
  });

  console.log(`\n${passed} passed`);
})().catch((e) => { console.error("FAILED:", e); process.exit(1); });
