// entitlements.js - UGS Cloud Code script: the server owns episode tokens and episode ownership (Threat Board T3).
//
// Paid content, kept apart from fair play on purpose: this folder is paid-content code, Tools/UGS/CloudCode is
// fair-play code, and check_fairplay_paid_content.py (R14) fails either one that names the other. No restriction,
// pool or trust record can reach what a player bought.
//
// The client used to hold its token balance and owned episodes in the client-written PLAYER_PROFILE, so a player could
// give themselves any of them. They now live in a PROTECTED Cloud Save key, PLAYER_ENTITLEMENTS, written only here:
//   - a token is created only from an order a storefront confirmed (redeemOrder), counted from the server's own STORE
//     table, never from a number the client sends, and each order counts once;
//   - an episode is owned only by spending tokens here (unlockEpisode).
// Which storefront: Docs/Legal/DLC_VS_TOKENS_QUESTIONNAIRE.md. This record serves either answer (the questionnaire:
// "the token wallet stays as the entitlement store"); a storefront joins as one entry in PROVIDERS with its verify().
// Until one does, only the editor's fake provider grants, and only outside production.
//
// One endpoint, one parameter `action`:
//   state                                  the record
//   redeemOrder   provider, orderId, productId   verify the order with its storefront, then credit the bundle once
//   unlockEpisode episode                  spend STORE.tokensPerEpisode tokens and own the episode
//
// No migration from PLAYER_PROFILE: no storefront has ever sold a token, so every client-held token was made by the
// editor's fake provider. Deploy steps: Tools/UGS/README.md. Tests: node Tools/UGS/Commerce/entitlements.test.cjs

const { DataApi } = require("@unity-services/cloud-save-1.4");

const KEY = "PLAYER_ENTITLEMENTS";
const SCHEMA = 1;

// What the store sells. Matches SO_EpisodeTokenConfig's defaults (tokensPerEpisode 1, bundle episode_token_1 = 1);
// keep the two in step until that asset exists and a generator copies it here.
const STORE = {
  tokensPerEpisode: 1,
  bundles: { episode_token_1: { tokens: 1 } },
};

// Storefronts. verify(order, context) answers { ok: true } only when the storefront confirms the order was paid.
// Steam (ISteamMicroTxn or DLC ownership), the App Store and Google Play join here once the decision is made and their
// server keys are set; until then they refuse.
const PROVIDERS = {
  editor_fake: { nonProductionOnly: true, verify: async () => ({ ok: true }) },
  steam_microtxn: { verify: async () => ({ ok: false, reason: "provider_not_configured" }) },
  steam_dlc: { verify: async () => ({ ok: false, reason: "provider_not_configured" }) },
  app_store: { verify: async () => ({ ok: false, reason: "provider_not_configured" }) },
  google_play: { verify: async () => ({ ok: false, reason: "provider_not_configured" }) },
};

const ID = /^[A-Za-z0-9_.:-]{1,128}$/;

function emptyRecord() {
  return { schema: SCHEMA, tokens: 0, lifetimeTokensPurchased: 0, lifetimeTokensSpent: 0, episodes: [], orders: {} };
}

function view(rec, ok, reason) {
  const out = { ok, tokens: rec.tokens, lifetimeTokensPurchased: rec.lifetimeTokensPurchased,
    lifetimeTokensSpent: rec.lifetimeTokensSpent, episodes: rec.episodes.slice(), orders: Object.keys(rec.orders) };
  if (reason) out.reason = reason;
  return out;
}

// Decides a request. verified is the provider's answer for redeemOrder (asked before, so this stays synchronous).
function apply(rec, params, nowMs, environmentName, verified) {
  const production = environmentName === "production";
  const p = params || {};
  switch (p.action) {
    case "state":
      return { ok: true, changed: false };

    case "redeemOrder": {
      const provider = PROVIDERS[p.provider];
      if (!provider) return { ok: false, reason: "unknown_provider" };
      if (provider.nonProductionOnly && production) return { ok: false, reason: "not_in_production" };
      if (typeof p.orderId !== "string" || !ID.test(p.orderId)) return { ok: false, reason: "bad_order" };
      const bundle = STORE.bundles[p.productId];
      if (!bundle) return { ok: false, reason: "unknown_product" };
      const orderKey = p.provider + ":" + p.orderId;
      if (rec.orders[orderKey]) return { ok: true, changed: false, reason: "already_redeemed" };
      if (!verified || !verified.ok) return { ok: false, reason: (verified && verified.reason) || "not_verified" };
      rec.orders[orderKey] = { product: p.productId, tokens: bundle.tokens, at: new Date(nowMs).toISOString() };
      rec.tokens += bundle.tokens;
      rec.lifetimeTokensPurchased += bundle.tokens;
      return { ok: true, changed: true };
    }

    case "unlockEpisode": {
      if (typeof p.episode !== "string" || !ID.test(p.episode)) return { ok: false, reason: "bad_episode" };
      if (rec.episodes.includes(p.episode)) return { ok: true, changed: false, reason: "already_owned" };
      const cost = STORE.tokensPerEpisode;
      if (rec.tokens < cost) return { ok: false, reason: "insufficient" };
      rec.tokens -= cost;
      rec.lifetimeTokensSpent += cost;
      rec.episodes.push(p.episode);
      return { ok: true, changed: true };
    }

    default:
      return { ok: false, reason: "unknown_action" };
  }
}

async function load(api, projectId, playerId) {
  const res = await api.getProtectedItems(projectId, playerId, [KEY]);
  const item = res && res.data && res.data.results ? res.data.results.find((r) => r.key === KEY) : null;
  if (!item) return { rec: emptyRecord(), writeLock: undefined };
  const rec = Object.assign(emptyRecord(), item.value);
  rec.episodes = Array.isArray(rec.episodes) ? rec.episodes : [];
  rec.orders = rec.orders && typeof rec.orders === "object" ? rec.orders : {};
  return { rec, writeLock: item.writeLock };
}

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi(context);
  const { projectId, playerId, environmentName } = context;

  // Ask the storefront once, before the write loop; the order id makes a retried write safe.
  let verified = null;
  const provider = params && params.action === "redeemOrder" ? PROVIDERS[params.provider] : null;
  if (provider && !(provider.nonProductionOnly && environmentName === "production"))
    verified = await provider.verify({ orderId: params.orderId, productId: params.productId, playerId }, context);

  for (let attempt = 0; attempt < 3; attempt++) {
    const { rec, writeLock } = await load(api, projectId, playerId);
    const result = apply(rec, params, Date.now(), environmentName, verified);
    if (!result.changed) {
      if (!result.ok) logger.info(`entitlements refused ${params && params.action}: ${result.reason}`);
      return view(rec, result.ok, result.reason);
    }
    try {
      const body = { key: KEY, value: rec };
      if (writeLock) body.writeLock = writeLock;
      await api.setProtectedItem(projectId, playerId, body);
      return view(rec, true);
    } catch (e) {
      const status = e && e.response ? e.response.status : 0;
      if (status === 409 && attempt < 2) continue;
      throw e;
    }
  }
};

module.exports.params = {
  action: { type: "String", required: true },
  provider: { type: "String", required: false },
  orderId: { type: "String", required: false },
  productId: { type: "String", required: false },
  episode: { type: "String", required: false },
};

module.exports._test = { apply, emptyRecord, view, STORE, PROVIDERS };
