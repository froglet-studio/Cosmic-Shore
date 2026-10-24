// Tests for match-report.js: node Tools/UGS/CloudCode/match-report.test.cjs
const assert = require("node:assert/strict");
const Module = require("node:module");
const path = require("node:path");

const store = new Map(); // "player/key" -> { value, writeLock }
let lock = 0;
class FakeDataApi {
  async getProtectedItems(_p, player, keys) {
    return { data: { results: keys.filter((k) => store.has(player + "/" + k)).map((k) => ({ key: k, ...JSON.parse(JSON.stringify(store.get(player + "/" + k))) })) } };
  }
  async setProtectedItem(_p, player, body) {
    const id = player + "/" + body.key, cur = store.get(id);
    if (cur && body.writeLock !== cur.writeLock) { const e = new Error("conflict"); e.response = { status: 409 }; throw e; }
    store.set(id, { value: JSON.parse(JSON.stringify(body.value)), writeLock: "w" + ++lock });
  }
}
const realLoad = Module._load;
Module._load = function (req) {
  if (req === "@unity-services/cloud-save-1.4") return { DataApi: FakeDataApi };
  return realLoad.apply(this, arguments);
};
const script = require(path.join(__dirname, "match-report.js"));
const { apply, emptyConduct, leaveRate } = script._test;
const logger = { info() {}, warning() {} };
const call = (player, params) => script({ params, context: { projectId: "p", playerId: player }, logger });
const signalsOf = (host) => (store.get(host + "/FAIRPLAY_SIGNALS") || { value: [] }).value;

let passed = 0;
async function test(name, fn) { await fn(); passed++; console.log("ok -", name); }

(async () => {
  const players = ["host", "g1", "g2"];
  const scores = [{ id: "host", score: 10 }, { id: "g1", score: 7 }, { id: "g2", score: 3 }];

  await test("a finished match counts as finished, not left", async () => {
    await call("g1", { action: "start", matchId: "m1", mode: "SkimRace", hostId: "host", players });
    const r = await call("g1", { action: "end", matchId: "m1", mode: "SkimRace", hostId: "host", players, scores, winner: "host" });
    assert.equal(r.ok, true); assert.equal(r.matchesStarted, 1); assert.equal(r.matchesLeft, 0); assert.equal(r.leaveRate, 0);
  });

  await test("a quit is counted at the next start, without the quitter's help", async () => {
    await call("g2", { action: "start", matchId: "m1", mode: "SkimRace", hostId: "host", players });
    // g2 quits: no end report. Next match:
    const r = await call("g2", { action: "start", matchId: "m2", mode: "Joust", hostId: "host", players });
    assert.equal(r.matchesLeft, 1); assert.equal(r.matchesStarted, 2);
  });

  await test("an unfinished match times out as left", () => {
    const c = emptyConduct(); const t0 = Date.UTC(2026, 9, 24, 10);
    apply(c, { action: "start", matchId: "x", mode: "SkimRace", hostId: "h" }, t0);
    apply(c, { action: "end", matchId: "x", mode: "SkimRace", hostId: "h", scores: [] }, t0 + 91 * 60000);
    assert.equal(c.matchesLeft, 1);
  });

  await test("an end with no matching start is refused (no free 'finished' credit)", async () => {
    const r = await call("g3", { action: "end", matchId: "m1", mode: "SkimRace", hostId: "host", players, scores });
    assert.equal(r.ok, false); assert.equal(r.reason, "no_open_match");
  });

  await test("matching reports from two players raise nothing", async () => {
    await call("host", { action: "start", matchId: "m1", mode: "SkimRace", hostId: "host", players });
    await call("host", { action: "end", matchId: "m1", mode: "SkimRace", hostId: "host", players, scores, winner: "host" });
    assert.equal(signalsOf("host").length, 0);
  });

  await test("a report that disagrees raises a signal against the HOST with the evidence", async () => {
    await call("host2", { action: "start", matchId: "j1", mode: "Joust", hostId: "host2", players: ["host2", "a", "b"] });
    await call("a", { action: "start", matchId: "j1", mode: "Joust", hostId: "host2", players: ["host2", "a", "b"] });
    await call("host2", { action: "end", matchId: "j1", mode: "Joust", hostId: "host2", players: ["host2", "a", "b"], scores: [{ id: "host2", score: 99 }, { id: "a", score: 5 }], winner: "host2" });
    await call("a", { action: "end", matchId: "j1", mode: "Joust", hostId: "host2", players: ["host2", "a", "b"], scores: [{ id: "host2", score: 4 }, { id: "a", score: 5 }], winner: "a" });
    const s = signalsOf("host2");
    assert.equal(s.length, 1); assert.equal(s[0].kind, "host_result_mismatch");
    assert.deepEqual(s[0].diff[0], { id: "host2", mine: 4, theirs: 99 });
    assert.equal(s[0].diff.at(-1).winner, true);
  });

  await test("reports from a different match of the same host are not compared", async () => {
    store.get("a/FAIRPLAY_CONDUCT").value.recent.at(-1).startedAt -= 10 * 60000;
    await call("b", { action: "start", matchId: "j1", mode: "Joust", hostId: "host2", players: ["host2", "a", "b"] });
    await call("b", { action: "end", matchId: "j1", mode: "Joust", hostId: "host2", players: ["host2", "a", "b"], scores: [{ id: "a", score: 1 }], winner: "b" });
    assert.equal(signalsOf("host2").length, 2); // vs host2's report only, not a's older one
  });

  await test("a report with another match id is never compared, even at the same time", async () => {
    const n = signalsOf("host2").length;
    await call("c", { action: "start", matchId: "j9", mode: "Joust", hostId: "host2", players: ["host2", "c", "a"] });
    await call("c", { action: "end", matchId: "j9", mode: "Joust", hostId: "host2", players: ["host2", "c", "a"], scores: [{ id: "a", score: 777 }], winner: "c" });
    assert.equal(signalsOf("host2").length, n);
  });

  await test("the leave rate is over the last 20 matches", () => {
    const c = emptyConduct(); let t = Date.UTC(2026, 9, 24);
    for (let i = 0; i < 30; i++) { apply(c, { action: "start", matchId: "mm", mode: "M", hostId: "h" }, t); t += 60000; if (i % 3) apply(c, { action: "end", matchId: "mm", mode: "M", hostId: "h", scores: [] }, t); t += 60000; }
    assert.equal(c.recent.length, 20);
    assert.ok(Math.abs(leaveRate(c) - 0.35) < 0.051);
  });

  console.log(`\n${passed} passed`);
})().catch((e) => { console.error("FAILED:", e); process.exit(1); });
