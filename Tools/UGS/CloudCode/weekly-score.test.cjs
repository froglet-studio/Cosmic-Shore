// Tests for weekly-score.js: node Tools/UGS/CloudCode/weekly-score.test.cjs
const assert = require("node:assert/strict");
const Module = require("node:module");
const path = require("node:path");

const posted = [];
const saved = new Map();
class FakeLeaderboardsApi {
  async addLeaderboardPlayerScore(_p, board, player, body) { posted.push({ board, player, ...body }); return { data: {} }; }
}
class FakeDataApi {
  async getProtectedItems(_p, player, keys) {
    return { data: { results: keys.map((k) => saved.get(player + "/" + k)).filter(Boolean) } };
  }
  async setProtectedItem(_p, player, body) { saved.set(player + "/" + body.key, { key: body.key, value: body.value, writeLock: "x" }); }
  async getPrivateCustomItems(_p, id, keys) {
    return { data: { results: keys.map((k) => saved.get(id + "/" + k)).filter(Boolean) } };
  }
}
const realLoad = Module._load;
Module._load = function (req) {
  if (req === "@unity-services/leaderboards-1.1") return { LeaderboardsApi: FakeLeaderboardsApi };
  if (req === "@unity-services/cloud-save-1.4") return { DataApi: FakeDataApi };
  return realLoad.apply(this, arguments);
};
const script = require(path.join(__dirname, "weekly-score.js"));
const { check, weekKey } = script._test;
const logger = { info() {}, warning() {} };
const call = (player, params) => script({ params, context: { projectId: "p", playerId: player }, logger });

let passed = 0;
async function test(name, fn) { await fn(); passed++; console.log("ok -", name); }

(async () => {
  await test("an honest time is posted with the metadata the game reads back", async () => {
    const r = await call("a", { leaderboardId: "weekly_challenge", seconds: 74.2, periodKey: "2026-W43", avatarId: 3 });
    assert.equal(r.ok, true);
    assert.deepEqual(posted.at(-1), { board: "weekly_challenge", player: "a", score: 74.2, metadata: { w: "2026-W43", a: 3 } });
  });
  await test("an impossible time is refused and never posted", async () => {
    const n = posted.length;
    const r = await call("b", { leaderboardId: "weekly_challenge", seconds: 0.1 });
    assert.equal(r.reason, "impossible_time"); assert.equal(posted.length, n);
  });
  await test("other boards, NaN-like values and huge times are refused", () => {
    const h = {}, t = Date.now();
    assert.equal(check({ leaderboardId: "season_rank", seconds: 60 }, h, t).reason, "unknown_board");
    assert.equal(check({ leaderboardId: "weekly_challenge", seconds: "60" }, h, t).reason, "bad_time");
    assert.equal(check({ leaderboardId: "weekly_challenge", seconds: 1e9 }, h, t).reason, "bad_time");
    assert.equal(check({ leaderboardId: "weekly_challenge_eu", seconds: 60 }, h, t).ok, true);
  });
  await test("posting again within 20 s is refused, but the same run's regional copy is allowed", async () => {
    assert.equal((await call("a", { leaderboardId: "weekly_challenge", seconds: 70 })).reason, "too_soon");
    assert.equal((await call("a", { leaderboardId: "weekly_challenge_eu", seconds: 74.2 })).ok, true);
  });
  await test("the weekly limit holds and resets on Monday (UTC)", () => {
    let h = {}, t = Date.UTC(2026, 9, 19, 1), ok = 0;
    for (let i = 0; i < 300; i++) { const v = check({ leaderboardId: "weekly_challenge", seconds: 60 + i }, h, t); if (v.ok) { ok++; h = { week: v.week, count: v.count, lastAt: t, lastSeconds: 60 + i, lastBoard: "weekly_challenge" }; } t += 21000; }
    assert.equal(ok, 200);
    assert.equal(weekKey(Date.UTC(2026, 9, 25, 23)), "2026-10-19");
    assert.equal(weekKey(Date.UTC(2026, 9, 26, 0)), "2026-10-26");
  });
  await test("an account in the Undertow posts to the twin board, never the public one (phase B)", async () => {
    saved.set("fp-shadow/trust", { key: "trust", value: { stage: "undertow" } });
    const r = await call("shadow", { leaderboardId: "weekly_challenge", seconds: 61, periodKey: "2026-W44" });
    assert.equal(r.ok, true);
    assert.equal(posted.at(-1).board, "weekly_challenge_undertow");
    assert.ok(!posted.some((p) => p.player === "shadow" && p.board === "weekly_challenge"));
  });
  await test("no trust record, or the shore, posts to the public board as before", async () => {
    const r = await call("fresh", { leaderboardId: "weekly_challenge", seconds: 62 });
    assert.equal(r.ok, true); assert.equal(posted.at(-1).board, "weekly_challenge");
  });
  console.log(`\n${passed} passed`);
})().catch((e) => { console.error("FAILED:", e); process.exit(1); });
