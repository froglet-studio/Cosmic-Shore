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
const priv = new Map(); // "customId/key" -> { value, writeLock }  (trust.js's record)
FakeDataApi.prototype.getPrivateCustomItems = async function (_p, id, keys) {
  return { data: { results: keys.filter((k) => priv.has(id + "/" + k)).map((k) => ({ key: k, ...JSON.parse(JSON.stringify(priv.get(id + "/" + k))) })) } };
};
FakeDataApi.prototype.setPrivateCustomItem = async function (_p, id, body) {
  priv.set(id + "/" + body.key, { value: JSON.parse(JSON.stringify(body.value)), writeLock: "w" + ++lock });
};
const realLoad = Module._load;
Module._load = function (req) {
  if (req === "@unity-services/cloud-save-1.4") return { DataApi: FakeDataApi };
  return realLoad.apply(this, arguments);
};
const script = require(path.join(__dirname, "match-report.js"));
const { apply, emptyConduct, leaveRate, mergeBirthYear, treatAsMinor } = script._test;
const logger = { info() {}, warning() {} };
// Adults unless a test says otherwise: an unknown age is treated as a minor (R13).
const call = (player, params) => script({
  params: params.action === "start" && !("birthYear" in params) ? { ...params, birthYear: 1990 } : params,
  context: { projectId: "p", playerId: player }, logger });
const ageOf = (player) => (store.get(player + "/FAIRPLAY_AGE") || { value: null }).value;
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

  // ---- Minors (Threat Board R13) ----
  const Y = new Date().getUTCFullYear();

  await test("a stored birth year only ever moves younger", async () => {
    assert.equal(mergeBirthYear(0, 2012, Y), 2012);
    assert.equal(mergeBirthYear(2012, 1980, Y), 2012, "re-entering an older year never makes a child an adult");
    assert.equal(mergeBirthYear(1990, 2010, Y), 2010);
    assert.equal(mergeBirthYear(1990, Y + 1, Y), 1990, "a future year is ignored");
    assert.equal(mergeBirthYear(0, "x", Y), 0);
  });

  await test("only a surely-adult year is treated as adult", () => {
    assert.equal(treatAsMinor(0, Y), true, "unknown age is a minor");
    assert.equal(treatAsMinor(Y - 17, Y), true);
    assert.equal(treatAsMinor(Y - 18, Y), true, "may not have had the 18th birthday yet");
    assert.equal(treatAsMinor(Y - 19, Y), false);
  });

  await test("a minor's quits are never counted (no behaviour profile)", async () => {
    await call("kid", { action: "start", matchId: "k1", mode: "SkimRace", hostId: "host", players, birthYear: Y - 12 });
    const r = await call("kid", { action: "start", matchId: "k2", mode: "SkimRace", hostId: "host", players, birthYear: 1970 });
    assert.equal(r.matchesLeft, 0); assert.equal(r.leaveRate, 0); assert.equal(r.matchesStarted, 0);
    assert.equal(ageOf("kid").birthYear, Y - 12, "the older year did not replace the younger one");
  });

  await test("an unknown age is treated as a minor too", async () => {
    await call("old-client", { action: "start", matchId: "u1", mode: "Joust", hostId: "host", players, birthYear: 0 });
    const r = await call("old-client", { action: "start", matchId: "u2", mode: "Joust", hostId: "host", players, birthYear: 0 });
    assert.equal(r.matchesLeft, 0);
  });

  await test("a minor host's mismatch is still recorded, marked for a person", async () => {
    const ps = ["kidhost", "d"];
    await call("kidhost", { action: "start", matchId: "q1", mode: "Joust", hostId: "kidhost", players: ps, birthYear: Y - 14 });
    await call("d", { action: "start", matchId: "q1", mode: "Joust", hostId: "kidhost", players: ps });
    await call("kidhost", { action: "end", matchId: "q1", mode: "Joust", hostId: "kidhost", players: ps, scores: [{ id: "kidhost", score: 50 }], winner: "kidhost" });
    await call("d", { action: "end", matchId: "q1", mode: "Joust", hostId: "kidhost", players: ps, scores: [{ id: "kidhost", score: 2 }], winner: "d" });
    const s = signalsOf("kidhost");
    assert.equal(s.length, 1); assert.equal(s[0].review, "human");
    assert.equal(signalsOf("host2")[0].review, "standard", "an adult host's signal is standard");
  });

  // ---- Bot farming (Threat Board R10) ----
  const T0 = Date.UTC(2026, 9, 25, 0, 5);
  const H = 3600000;
  const startAt = (c, t, id = "f") => apply(c, { action: "start", matchId: id, mode: "SkimRace", hostId: "h" }, t, true, "me");
  const endAt = (c, t, id = "f", score = 10) =>
    apply(c, { action: "end", matchId: id, mode: "SkimRace", hostId: "h", scores: [{ id: "me", score }] }, t, true, "me");

  await test("starting matches in 20 hours of one day raises around_the_clock once", () => {
    const c = emptyConduct(); const raised = [];
    for (let h = 0; h < 22; h++) raised.push(...startAt(c, T0 + h * H).selfSignals);
    assert.equal(raised.length, 1); assert.equal(raised[0].kind, "around_the_clock"); assert.equal(raised[0].activeHours, 20);
  });

  await test("a long human session (8 hours, a match every 5 minutes) raises nothing", () => {
    const c = emptyConduct(); let n = 0;
    for (let t = T0; t < T0 + 8 * H; t += 5 * 60000) n += startAt(c, t).selfSignals.length;
    assert.equal(n, 0);
  });

  await test("six matches of one mode within half a second of each other raise repetitive_results", () => {
    const c = emptyConduct(); const raised = []; let t = T0;
    for (let i = 0; i < 6; i++) {
      startAt(c, t, "r" + i); t += 120000 + (i % 2 ? 400 : -300);
      raised.push(...endAt(c, t, "r" + i).selfSignals); t += 60000;
    }
    assert.equal(raised.length, 1); assert.equal(raised[0].kind, "repetitive_results");
    assert.ok(raised[0].durationCv < 0.01); assert.deepEqual(raised[0].scores, [10, 10, 10, 10, 10, 10]);
  });

  await test("human variation in match time raises nothing", () => {
    const c = emptyConduct(); let n = 0, t = T0;
    const lengths = [118, 131, 109, 140, 125, 115, 122, 137];
    lengths.forEach((L, i) => { startAt(c, t, "v" + i); t += L * 1000; n += endAt(c, t, "v" + i).selfSignals.length; t += 60000; });
    assert.equal(n, 0);
  });

  await test("matches under 30 s are never compared", () => {
    const c = emptyConduct(); let n = 0, t = T0;
    for (let i = 0; i < 8; i++) { startAt(c, t, "s" + i); t += 10000; n += endAt(c, t, "s" + i).selfSignals.length; t += 5000; }
    assert.equal(n, 0);
  });

  await test("no farming check for a minor (R13)", () => {
    const c = emptyConduct(); let n = 0, t = T0;
    for (let h = 0; h < 24; h++) {
      n += apply(c, { action: "start", matchId: "m" + h, mode: "SkimRace", hostId: "h" }, t, false, "me").selfSignals.length;
      t += 120000;
      n += apply(c, { action: "end", matchId: "m" + h, mode: "SkimRace", hostId: "h", scores: [] }, t, false, "me").selfSignals.length;
      t = T0 + (h + 1) * H;
    }
    assert.equal(n, 0);
  });

  await test("a farming signal is stored on the player's own record with the review rule", async () => {
    const realNow = Date.now; let fake = Date.UTC(Y, 2, 1, 0, 1);
    Date.now = () => fake;
    try {
      for (let h = 0; h < 20; h++) {
        await call("farm", { action: "start", matchId: "x" + h, mode: "SkimRace", hostId: "farm", players: ["farm"] });
        fake += H;
      }
    } finally { Date.now = realNow; }
    const s = signalsOf("farm");
    assert.equal(s.length, 1); assert.equal(s[0].kind, "around_the_clock"); assert.equal(s[0].review, "standard");
  });

  // ---- Phase B: matches in a pool count toward the notice (trust.js) ----
  await test("a finished match in the Undertow counts toward the notice; on the shore it does not", async () => {
    priv.set("fp-under/trust", { value: { stage: "undertow", matches: 2 }, writeLock: "w0" });
    priv.set("fp-shore/trust", { value: { stage: "shore", matches: 0 }, writeLock: "w0" });
    for (const p of ["under", "shore"]) {
      await call(p, { action: "start", matchId: "pb", mode: "Joust", hostId: p, players: [p] });
      await call(p, { action: "end", matchId: "pb", mode: "Joust", hostId: p, players: [p], scores: [], winner: p });
    }
    assert.equal(priv.get("fp-under/trust").value.matches, 3);
    assert.equal(priv.get("fp-shore/trust").value.matches, 0);
  });

  console.log(`\n${passed} passed`);
})().catch((e) => { console.error("FAILED:", e); process.exit(1); });
