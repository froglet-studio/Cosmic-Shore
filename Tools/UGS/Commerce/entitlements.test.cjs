// Tests for entitlements.js: node Tools/UGS/Commerce/entitlements.test.cjs
// Runs the real script against an in-memory Cloud Save stand-in (no network, no UGS account).
const assert = require("node:assert/strict");
const Module = require("node:module");
const path = require("node:path");

const store = { protected: new Map(), lockSeq: 0, failNextWriteWith409: false, writes: 0 };
class FakeDataApi {
  async getProtectedItems(_p, player, keys) {
    const rows = keys.map((k) => store.protected.get(player + "/" + k)).filter(Boolean);
    return { data: { results: rows.map((r) => ({ key: r.key, value: JSON.parse(JSON.stringify(r.value)), writeLock: r.writeLock })) } };
  }
  async setProtectedItem(_p, player, body) {
    const id = player + "/" + body.key;
    const cur = store.protected.get(id);
    if (store.failNextWriteWith409) {
      store.failNextWriteWith409 = false;
      const e = new Error("conflict"); e.response = { status: 409 }; throw e;
    }
    if (cur && body.writeLock !== cur.writeLock) { const e = new Error("conflict"); e.response = { status: 409 }; throw e; }
    store.writes++;
    store.protected.set(id, { key: body.key, value: JSON.parse(JSON.stringify(body.value)), writeLock: "w" + ++store.lockSeq });
  }
}
const realLoad = Module._load;
Module._load = function (req) {
  if (req === "@unity-services/cloud-save-1.4") return { DataApi: FakeDataApi };
  return realLoad.apply(this, arguments);
};
const ent = require(path.join(__dirname, "entitlements.js"));
const { STORE } = ent._test;

const logger = { info() {}, warning() {}, error() {} };
const call = (player, params, env = "development") =>
  ent({ params, context: { projectId: "p", playerId: player, environmentName: env }, logger });
const fake = (orderId, productId = "episode_token_1") => ({ action: "redeemOrder", provider: "editor_fake", orderId, productId });

let passed = 0;
async function test(name, fn) { await fn(); passed++; console.log("ok -", name); }

(async () => {
  await test("a new player owns nothing and nothing is stored for a look", async () => {
    const r = await call("a", { action: "state" });
    assert.equal(r.ok, true); assert.equal(r.tokens, 0); assert.deepEqual(r.episodes, []);
    assert.equal(store.protected.has("a/PLAYER_ENTITLEMENTS"), false);
  });

  await test("a verified order credits the server's bundle size, once", async () => {
    const r = await call("b", { ...fake("o1"), tokens: 999 });
    assert.equal(r.ok, true); assert.equal(r.tokens, STORE.bundles.episode_token_1.tokens, "the client's number is ignored");
    const again = await call("b", fake("o1"));
    assert.equal(again.ok, true); assert.equal(again.reason, "already_redeemed"); assert.equal(again.tokens, 1);
    assert.equal((await call("b", fake("o2"))).tokens, 2);
  });

  await test("no storefront grants until it is configured; the fake one never grants in production", async () => {
    for (const provider of ["steam_microtxn", "steam_dlc", "app_store", "google_play"]) {
      const r = await call("c", { action: "redeemOrder", provider, orderId: "x1", productId: "episode_token_1" }, "production");
      assert.equal(r.ok, false); assert.equal(r.reason, "provider_not_configured"); assert.equal(r.tokens, 0);
    }
    const prod = await call("c", fake("x2"), "production");
    assert.equal(prod.reason, "not_in_production"); assert.equal(prod.tokens, 0);
    assert.equal((await call("c", { action: "redeemOrder", provider: "my_shop", orderId: "x3", productId: "episode_token_1" })).reason, "unknown_provider");
    assert.equal(store.protected.has("c/PLAYER_ENTITLEMENTS"), false);
  });

  await test("unknown products, malformed order ids and episode ids are refused", async () => {
    assert.equal((await call("d", fake("o1", "episode_token_100"))).reason, "unknown_product");
    assert.equal((await call("d", fake(""))).reason, "bad_order");
    assert.equal((await call("d", fake("a b"))).reason, "bad_order");
    assert.equal((await call("d", { action: "unlockEpisode", episode: "" })).reason, "bad_episode");
    assert.equal((await call("d", { action: "nope" })).reason, "unknown_action");
  });

  await test("an episode costs tokens, is owned once, and needs the balance", async () => {
    assert.equal((await call("e", { action: "unlockEpisode", episode: "ep1" })).reason, "insufficient");
    await call("e", fake("o1"));
    const r = await call("e", { action: "unlockEpisode", episode: "ep1" });
    assert.equal(r.ok, true); assert.deepEqual(r.episodes, ["ep1"]); assert.equal(r.tokens, 0); assert.equal(r.lifetimeTokensSpent, 1);
    const again = await call("e", { action: "unlockEpisode", episode: "ep1" });
    assert.equal(again.reason, "already_owned"); assert.equal(again.tokens, 0);
  });

  await test("a concurrent write is retried, not lost", async () => {
    await call("f", fake("o1"));
    store.failNextWriteWith409 = true;
    const r = await call("f", fake("o2"));
    assert.equal(r.ok, true); assert.equal(r.tokens, 2);
  });

  await test("the record lives in protected data only", async () => {
    assert.ok(store.protected.has("b/PLAYER_ENTITLEMENTS"));
    assert.equal(store.protected.get("b/PLAYER_ENTITLEMENTS").value.schema, 1);
  });

  console.log(`${passed} passed`);
})().catch((e) => { console.error("FAILED:", e); process.exit(1); });
