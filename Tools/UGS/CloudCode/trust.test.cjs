// Tests for trust.js: node Tools/UGS/CloudCode/trust.test.cjs
const assert = require("node:assert/strict");
const Module = require("node:module");
const path = require("node:path");

const prot = new Map();   // "player/key" -> value
const priv = new Map();   // "customId/key" -> { value, writeLock }
let lock = 0;
const conflict = () => { const e = new Error("conflict"); e.response = { status: 409 }; return e; };
class FakeDataApi {
  async getProtectedItems(_p, player, keys) {
    return { data: { results: keys.filter((k) => prot.has(player + "/" + k)).map((k) => ({ key: k, value: prot.get(player + "/" + k) })) } };
  }
  async getPrivateCustomItems(_p, id, keys) {
    return { data: { results: keys.filter((k) => priv.has(id + "/" + k)).map((k) => ({ key: k, ...JSON.parse(JSON.stringify(priv.get(id + "/" + k))) })) } };
  }
  async setPrivateCustomItem(_p, id, body) {
    const cur = priv.get(id + "/" + body.key);
    if (cur && body.writeLock !== cur.writeLock) throw conflict();
    priv.set(id + "/" + body.key, { value: JSON.parse(JSON.stringify(body.value)), writeLock: "w" + ++lock });
  }
}
const realLoad = Module._load;
Module._load = function (req) {
  if (req === "@unity-services/cloud-save-1.4") return { DataApi: FakeDataApi };
  return realLoad.apply(this, arguments);
};
const script = require(path.join(__dirname, "trust.js"));
const { emptyTrust, step, appeal, evaluate, RULES } = script._test;
const logger = { info() {}, warning() {} };
const call = (player, params) => script({ params, context: { projectId: "p", playerId: player }, logger });
const trustOf = (player) => (priv.get("fp-" + player + "/trust") || {}).value;
const queue = () => ((priv.get("fairplay-review/queue") || {}).value || []).map((q) => q.id);

const NOW = Date.UTC(2026, 9, 26, 12);
const Y = new Date(NOW).getUTCFullYear();
const DAY = 86400000;
const sig = (kind, daysAgo) => ({ kind, at: NOW - daysAgo * DAY, diff: [{ id: "x", mine: 1, theirs: 9 }] });
const left = (n, of) => ({ recent: Array.from({ length: of }, (_, i) => ({ left: i < n })) });

let passed = 0;
async function test(name, fn) { await fn(); passed++; console.log("ok -", name); }

(async () => {
  await test("a clean account is on the shore, joins the usual lobby and is told nothing", () => {
    const r = step(emptyTrust(), [], left(0, 20), 1990, NOW);
    assert.equal(r.trust.stage, "shore"); assert.equal(r.reply.lobby, ""); assert.equal(r.reply.board, ""); assert.equal(r.reply.notice, null);
  });

  await test("one signal is watch: silent, no effect on play", () => {
    const r = step(emptyTrust(), [sig("host_result_mismatch", 1)], null, 1990, NOW);
    assert.equal(r.trust.stage, "watch"); assert.equal(r.reply.lobby, ""); assert.equal(r.reply.notice, null);
  });

  await test("enough signals move an adult to the Undertow, the shadow board and the review queue", () => {
    const r = step(emptyTrust(), [sig("host_result_mismatch", 1), sig("host_result_mismatch", 2), sig("repetitive_results", 3)], null, 1990, NOW);
    assert.equal(r.trust.stage, "undertow"); assert.equal(r.reply.lobby, "_u1"); assert.equal(r.reply.board, "_undertow");
    assert.equal(r.queue, "add"); assert.equal(r.reply.notice, null, "no notice before three Undertow matches");
    assert.equal(r.trust.reasons.length, 3); assert.ok(r.trust.reasons[0].evidence, "every reason carries its evidence");
  });

  await test("a minor with the same signals is never sent to the Undertow: review, same lobby (R13)", () => {
    const r = step(emptyTrust(), [sig("host_result_mismatch", 1), sig("host_result_mismatch", 2), sig("repetitive_results", 3)], null, Y - 15, NOW);
    assert.equal(r.trust.stage, "review"); assert.equal(r.reply.lobby, ""); assert.equal(r.queue, "add");
  });

  await test("an unknown age counts as a minor", () => {
    const r = step(emptyTrust(), [sig("host_result_mismatch", 1), sig("host_result_mismatch", 2), sig("repetitive_results", 3)], null, 0, NOW);
    assert.equal(r.trust.stage, "review");
  });

  await test("signals older than the decay window no longer count (trust comes back)", () => {
    const r = step(emptyTrust(), [sig("host_result_mismatch", 31), sig("host_result_mismatch", 40), sig("repetitive_results", 35)], null, 1990, NOW);
    assert.equal(r.trust.stage, "shore");
  });

  await test("a repeat quitter goes to the quitters' pool, not the Undertow", () => {
    const r = step(emptyTrust(), [], left(12, 20), 1990, NOW);
    assert.equal(r.trust.stage, "quitters"); assert.equal(r.reply.lobby, "_q1"); assert.equal(r.reply.board, "");
  });

  await test("the notice comes after three Undertow matches, naming the rule, and offers an appeal", () => {
    const t = step(emptyTrust(), [sig("host_result_mismatch", 1), sig("host_result_mismatch", 2), sig("host_result_mismatch", 3)], null, 1990, NOW).trust;
    t.matches = 2;
    assert.equal(step(t, [sig("host_result_mismatch", 1), sig("host_result_mismatch", 2), sig("host_result_mismatch", 3)], null, 1990, NOW).reply.notice, null);
    t.matches = 3;
    const n = step(t, [sig("host_result_mismatch", 1), sig("host_result_mismatch", 2), sig("host_result_mismatch", 3)], null, 1990, NOW).reply.notice;
    assert.equal(n.kind, "cheating"); assert.equal(n.rule, RULES.rule.cheating); assert.equal(n.automated, true); assert.equal(n.appeal, "none");
  });

  await test("only a person restricts, and a restriction is notified at once", () => {
    const t = emptyTrust(); t.review = { decision: "restrict", until: new Date(NOW + 7 * DAY).toISOString(), by: "reviewer" };
    const r = step(t, [], null, 1990, NOW);
    assert.equal(r.trust.stage, "restricted"); assert.equal(r.reply.notice.restricted, true); assert.equal(r.reply.notice.automated, false);
    assert.equal(r.trust.review.appliedAt, NOW, "a decision is applied once");
    const later = step(r.trust, [], null, 1990, NOW + 8 * DAY);
    assert.equal(later.trust.stage, "shore", "the restriction ends on its date");
  });

  await test("no score alone ever reaches restricted", () => {
    const many = Array.from({ length: 30 }, (_, i) => sig("host_result_mismatch", i % 20));
    assert.notEqual(step(emptyTrust(), many, null, 1990, NOW).trust.stage, "restricted");
  });

  await test("a person clearing an account sets every signal so far aside and upholds the appeal", () => {
    const sigs = [sig("host_result_mismatch", 1), sig("host_result_mismatch", 2), sig("repetitive_results", 3)];
    const t = step(emptyTrust(), sigs, null, 1990, NOW).trust;
    appeal(t, "I was not cheating", NOW);
    t.review = { decision: "clear", by: "reviewer" };
    const r = step(t, sigs, null, 1990, NOW + 1000);
    assert.equal(r.trust.stage, "shore"); assert.equal(r.trust.appeal.state, "upheld"); assert.equal(r.queue, "remove");
  });

  await test("an appeal a person refused leaves the measure and the queue entry goes", () => {
    const sigs = [sig("host_result_mismatch", 1), sig("host_result_mismatch", 2), sig("repetitive_results", 3)];
    const t = step(emptyTrust(), sigs, null, 1990, NOW).trust;
    appeal(t, "not me", NOW);
    t.review = { decision: "uphold", by: "reviewer" };
    const r = step(t, sigs, null, 1990, NOW + 1000);
    assert.equal(r.trust.stage, "undertow"); assert.equal(r.trust.appeal.state, "refused"); assert.equal(r.queue, "remove");
    assert.equal(step(r.trust, sigs, null, 1990, NOW + 2000).queue, null, "and it does not come back while the stage holds");
  });

  await test("an appeal needs a measure, opens once, and keeps a short text", () => {
    assert.equal(appeal(emptyTrust(), "hi", NOW).reason, "nothing_to_appeal");
    const t = emptyTrust(); t.stage = "undertow";
    assert.equal(appeal(t, "x".repeat(2000), NOW).ok, true); assert.equal(t.appeal.text.length, RULES.appealMaxChars);
    assert.equal(appeal(t, "again", NOW).reason, "already_open");
  });

  await test("the whole script: status stores the record privately and queues the account; appeal; samePool", async () => {
    prot.set("bad/FAIRPLAY_SIGNALS", [sig("host_result_mismatch", 1), sig("host_result_mismatch", 2), sig("repetitive_results", 3)]);
    prot.set("bad/FAIRPLAY_AGE", { birthYear: 1990 });
    const realNow = Date.now; Date.now = () => NOW;
    try {
      const r = await call("bad", { action: "status" });
      assert.deepEqual(Object.keys(r).sort(), ["board", "lobby", "notice", "ok"], "the client never sees the stage, score or reasons");
      assert.equal(r.lobby, "_u1"); assert.equal(trustOf("bad").stage, "undertow"); assert.deepEqual(queue(), ["bad"]);
      const a = await call("bad", { action: "appeal", text: "please look again" });
      assert.equal(a.ok, true); assert.equal(trustOf("bad").appeal.state, "open");
      await call("good", { action: "status" });
      assert.equal((await call("bad", { action: "samePool", otherId: "good" })).same, false);
      assert.equal((await call("good", { action: "samePool", otherId: "nobody-yet" })).same, true);
    } finally { Date.now = realNow; }
  });

  console.log(`\n${passed} passed`);
})().catch((e) => { console.error("FAILED:", e); process.exit(1); });
