# Branch archive: `claude/steam-early-access-checklist-qyun5c`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-08-04 by Claude
- **Unmerged commits:** 4
- **Forked from:** `536a3723e` (2026-08-03, Add meta files)
- **Tip:** `019cc66e1`
- **Files touched (3):**
  - `Assets/_Scripts/ScriptableObjects/SO_EpisodeTokenConfig.cs`
  - `Docs/ECONOMY_TABLES.md`
  - `Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md`

### `3b993abbf` — docs(economy): DLC vs token analysis; episode pricing to $5 / 12-for-$30

_Claude, 2026-08-04 01:29:46 +0000_

```text
Analyses whether the original idea - player picks which episodes, half
off on a pack - survives as Steam DLC.

Key reframe: DLC vs tokens is not the real axis. Both are Steam
purchases; what differs is where the player pays (store page = DLC,
in-game = Microtransaction API) and what the SKU grants (fixed content =
an episode, flexible credit = a token). Those are independent, so a token
pack can be sold as DLC - which preserves player choice and the pack
discount without the backend the Microtransaction API requires.

Four routes compared on choice, partial-selection discount, refunds,
merchant of record, and build cost. Recommendation depends on one
unanswered question: whether the catalogue grows past 12.

Pricing finding worth flagging: at $5 each and $30 for twelve, six
episodes bought individually costs exactly what all twelve cost. Nobody
rational buys 6-11 individually, so the player-choice discount never gets
exercised - anyone wanting a discount buys all twelve, leaving nothing to
choose. Choice only has value if a pack is smaller than the catalogue,
hence the proposed 6-for-$20 mid tier.

Also records the refund asymmetry that a token-granting DLC introduces: a
refunded pack whose tokens are already spent needs a revoke path, which
does not exist yet. Individual DLC has no such problem because ownership
is the entitlement.

Config and tables updated to $5 per episode and $30 for twelve.
```

```text
 Assets/_Scripts/ScriptableObjects/SO_EpisodeTokenConfig.cs |   8 +-
 Docs/ECONOMY_TABLES.md                                     |   6 +-
 Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md                       | 178 +++++++++++++++++++++++++++++++++++++++++++
 3 files changed, 187 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 238 lines)</summary>

```diff
diff --git a/Assets/_Scripts/ScriptableObjects/SO_EpisodeTokenConfig.cs b/Assets/_Scripts/ScriptableObjects/SO_EpisodeTokenConfig.cs
index 436d4113c..e3612d171 100644
--- a/Assets/_Scripts/ScriptableObjects/SO_EpisodeTokenConfig.cs
+++ b/Assets/_Scripts/ScriptableObjects/SO_EpisodeTokenConfig.cs
@@ -36,7 +36,7 @@ namespace CosmicShore.ScriptableObjects
             [Min(1)] public int tokenCount = 1;
 
             [Tooltip("Display price in USD. The storefront is authoritative; this is for UI only.")]
-            [Min(0f)] public float displayPriceUsd = 2f;
+            [Min(0f)] public float displayPriceUsd = 5f;
 
             [Tooltip("Optional 'best value' flag for the UI to badge.")]
             public bool highlight;
@@ -46,10 +46,12 @@ namespace CosmicShore.ScriptableObjects
         [Tooltip("Tokens required to unlock one episode. 1 = the shipped design (1 episode = 1 token).")]
         [Min(1)] public int tokensPerEpisode = 1;
 
-        [Tooltip("Purchasable bundles, in display order.")]
+        [Tooltip("Purchasable bundles, in display order. Product ids must match the storefront SKUs " +
+                 "(Steam DLC app ids under the recommended DLC route).")]
         public List<TokenBundle> bundles = new()
         {
-            new TokenBundle { productId = "episode_token_1", displayName = "Episode Token",   tokenCount = 1, displayPriceUsd = 2f },
+            new TokenBundle { productId = "episode_token_1",  displayName = "Episode Token",     tokenCount = 1,  displayPriceUsd = 5f },
+            new TokenBundle { productId = "episode_token_12", displayName = "All 12 Episodes",   tokenCount = 12, displayPriceUsd = 30f, highlight = true },
         };
 
         [Header("Display")]
diff --git a/Docs/ECONOMY_TABLES.md b/Docs/ECONOMY_TABLES.md
index 9e5e60dfa..f442e69e2 100644
--- a/Docs/ECONOMY_TABLES.md
+++ b/Docs/ECONOMY_TABLES.md
@@ -70,17 +70,19 @@ earned before that point bank toward the first purchase rather than being wasted
 | Item | Type | Consumable? | Price (USD) | Notes |
 |---|---|---|---|---|
 | Cosmic Shore (Early Access) | Base game | Non-consumable | **$15.00** | Price does **not** rise at 1.0 |
-| Episode Token ×1 | Currency | Consumable | **$2.00** | 1 token = 1 episode |
+| Episode ×1 | Entitlement | Non-consumable | **$5.00** | Single episode |
+| All 12 Episodes | Entitlement / token pack | Non-consumable | **$30.00** | Half off ($2.50 each) |
 | Episode (via token) | Entitlement | Non-consumable | 1 token | Permanent, all devices |
 
 | Rule | Value |
 |---|---|
-| Bundles / volume discounts | None at launch |
+| Bundles / volume discounts | 12-for-$30 (half off). Mid-tier 6-for-$20 proposed — see analysis |
 | Crystals → tokens | **Never.** No soft-to-hard conversion. |
 | Regional pricing | Accept Valve's suggested conversions |
 | Launch discount | *Pending — not yet decided* |
 | Episodes at launch | **6** |
 | Free tokens granted on game purchase | *Pending — how many?* |
+| Delivery mechanism | *Pending — DLC vs token packs, see `Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md`* |
 
 ---
 
diff --git a/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md b/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
new file mode 100644
index 000000000..0f3979987
--- /dev/null
+++ b/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
@@ -0,0 +1,178 @@
+# DLC vs. Episode Tokens — analysis
+
+Answers one question: **can the original idea survive as DLC?**
+
+Original idea: **1 episode = $5**, **12 episodes = $30** (half off), and the player **chooses which
+episodes** they get — unlike a DLC bundle, whose contents are pre-selected.
+
+Companion to `DLC_VS_TOKENS_QUESTIONNAIRE.md` (the decision + counsel questions).
+
+---
+
+## 0. The reframe that changes the answer
+
+"DLC or tokens" is the wrong axis. On Steam, **both are Steam purchases**. The real axis is:
+
+| Question | Answer determines |
+|---|---|
+| **Where does the player pay?** | Steam **store page** → DLC. **In-game** store UI → Microtransaction API. |
+| **What does the SKU grant?** | Fixed content → an episode. Flexible credit → a token. |
+
+Those two are independent. You can sell a **token pack as DLC** — bought on the store page, granting
+credit the player spends on whichever episodes they like. That combination is what preserves the
+original idea *without* the backend that the in-game Microtransaction API demands.
+
+**So: yes, the original idea survives as DLC.** The catch is a refund edge case, covered in §4.
+
+---
+
+## 1. What each mechanism can and cannot express
+
+| Capability | Individual DLC | Steam Bundle | Token pack sold as DLC | Tokens via MicroTxn |
+|---|---|---|---|---|
+| Player picks **which** episode | ✅ | ❌ pre-selected set | ✅ | ✅ |
+| Discount on a **partial, player-chosen** selection | ❌ | ❌ | ✅ | ✅ |
+| Discount on the **full set** | ❌ | ✅ | ✅ | ✅ |
+| Already-owned items discounted automatically | — | ✅ Complete the Set | ❌ manual | ❌ manual |
+| Purchase happens **in-game** | ❌ overlay → store | ❌ | ❌ overlay → store | ✅ |
+| Wishlist / gifting / regional pricing | ✅ | ✅ | ✅ | ❌ |
+| Valve is merchant of record (tax, refunds) | ✅ | ✅ | ✅ | ✅ |
+| **Backend required** | ❌ none | ❌ none | ❌ none | ✅ InitTxn/FinalizeTxn + verification |
+| Steamworks SDK needed | ownership check | ownership check | ownership check | full MicroTxn integration |
+
+**The one thing plain DLC cannot do:** discount a selection the *player* chooses. A DLC SKU maps to
+fixed content and a Steam Bundle discounts a fixed set. "Any 6 of 12, half off" is not a storefront
+primitive on Steam. Only a credit/token abstraction expresses it.
+
+---
+
+## 2. The four viable routes
+
+### Route A — Individual DLC + a Complete-the-Set bundle
+12 episode DLCs at $5, plus an "All Episodes" bundle at $30.
+
+- ✅ Smallest possible build: an ownership check per episode, nothing else.
+- ✅ **Complete the Set** automatically discounts by what the player already owns, so someone with 3
+  episodes pays roughly the remainder rather than full bundle price.
+- ❌ Choosing 6 specific episodes costs $30 — full price. No partial discount.
+
+### Route B — Token packs sold as DLC ⭐ recommended
+"Episode Pack (12)" DLC at $30 grants 12 tokens; a "Single Episode Token" DLC at $5 grants 1. The
+player redeems tokens against any episode, in-game.
+
+- ✅ **Preserves the original idea exactly**: player choice *and* the half-off pack price.
+- ✅ Valve handles payment, tax, refunds, regional pricing; wishlists and gifting still work.
+- ✅ **No backend** — ownership is read from Steam, the grant is local and idempotent.
+- ✅ The token wallet already built (`EpisodeTokenService`) is the entitlement store; the purchase
+  provider becomes a DLC-ownership reader instead of a payment client.
+- ⚠️ Refund asymmetry — see §4.
+- ⚠️ Store page must state plainly: *"grants 12 episode unlocks, redeemable against any episodes."*
+
+### Route C — Tokens via the Microtransaction API
+An in-game store that charges the Steam Wallet directly.
+
+- ✅ Purchase never leaves the game; best conversion.
+- ❌ Requires a backend for InitTxn/FinalizeTxn and order verification. Real engineering time.
+- ❌ No wishlists, no gifting, no store-page visibility for the packs.
+- Only worth it if the in-game store becomes a significant, recurring surface.
+
+### Route D — Season Pass DLC (all episodes)
+One $30 SKU granting everything, forever, including future episodes.
+
+- ✅ Simplest to communicate and to build.
+- ❌ No choice at all, and it commits you to delivering future episodes to existing buyers.
+
+---
+
+## 3. The pricing math — and a problem in it
+
+| Episodes wanted | Buy individually | Buy the 12-pack | Player's rational choice |
+|---|---|---|---|
```

</details>

### `8fab98dc5` — docs(economy): fold the lifetime pass and $120 spend cap into the analysis

_Claude, 2026-08-04 01:32:08 +0000_

```text
Adds the $90 lifetime pass (all episodes past, present, future) and the
$120 lifetime spend cap, with advantages/disadvantages per route.

Two findings change the recommendation:

The $120 ceiling is structural only on one path. 12-pack ($30) plus pass
($90) is exactly $120, which is almost certainly where the number came
from - but twelve singles ($60) plus the pass ($90) is $150. The cap is
therefore a rule to implement, not a price that holds by itself.

Steam cannot enforce a spend cap. There is no lifetime-spend API; only
DLC ownership is readable, and inferring spend from list prices is broken
by sales, regional pricing, bundles and gifts. Store pages also cannot be
gated, so a purchase past the cap cannot be blocked - the promise has to
be delivered by crediting after the fact. The native mechanism that does
hold is a Complete-the-Set bundle priced at $120 containing every episode
plus the pass: Steam charges only for what the player does not own, on
every purchase path, with no ledger and no revoke.

Recommendation moves to Route A (individual DLC plus that bundle) unless
the proposed 6-of-12 mid tier is real, since a player-chosen partial
discount is the one requirement only tokens satisfy - and at the current
numbers that discount is never exercised anyway, because at six episodes
buying individually already costs the same as all twelve.

Legality section added: the cap is consumer-favourable and legally easy;
the exposure is failing to deliver it, since "never more than $120" is an
advertising claim that must hold in every currency and through every
sale. The harder claim is "future episodes" - open-ended content promises
need the term defined against the game's commercial life rather than
"lifetime", which regulators have challenged. Prepaid-balance obligations
apply to the token routes only, which is a further argument for DLC.
```

```text
 Docs/ECONOMY_TABLES.md               |  11 ++-
 Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md | 287 +++++++++++++++++++++++++++++++++++++++++++----------------------
 2 files changed, 201 insertions(+), 97 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 397 lines)</summary>

```diff
diff --git a/Docs/ECONOMY_TABLES.md b/Docs/ECONOMY_TABLES.md
index f442e69e2..81c045cdb 100644
--- a/Docs/ECONOMY_TABLES.md
+++ b/Docs/ECONOMY_TABLES.md
@@ -70,8 +70,10 @@ earned before that point bank toward the first purchase rather than being wasted
 | Item | Type | Consumable? | Price (USD) | Notes |
 |---|---|---|---|---|
 | Cosmic Shore (Early Access) | Base game | Non-consumable | **$15.00** | Price does **not** rise at 1.0 |
-| Episode ×1 | Entitlement | Non-consumable | **$5.00** | Single episode |
+| Episode ×1 | Entitlement | Non-consumable | **$5.00** | Player picks which |
 | All 12 Episodes | Entitlement / token pack | Non-consumable | **$30.00** | Half off ($2.50 each) |
+| Lifetime pass | Entitlement | Non-consumable | **$90.00** | All episodes: past, present, future |
+| **Lifetime spend cap** | — | — | **$120.00** | Ceiling. At $120 the pass is granted; we take no more |
 | Episode (via token) | Entitlement | Non-consumable | 1 token | Permanent, all devices |
 
 | Rule | Value |
@@ -82,7 +84,12 @@ earned before that point bank toward the first purchase rather than being wasted
 | Launch discount | *Pending — not yet decided* |
 | Episodes at launch | **6** |
 | Free tokens granted on game purchase | *Pending — how many?* |
-| Delivery mechanism | *Pending — DLC vs token packs, see `Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md`* |
+| Delivery mechanism | *Pending — see `Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md`* |
+| Mid tier (6 for $20) | *Proposed — decides DLC vs tokens* |
+
+> ⚠️ **The $120 cap only holds on the pack path.** 12-pack ($30) + pass ($90) = $120 exactly, but
+> twelve singles ($60) + pass ($90) = $150. The cap has to be delivered by crediting prior
+> purchases — Steam cannot enforce a spend ceiling. See the analysis §4.
 
 ---
 
diff --git a/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md b/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
index 0f3979987..cddb1f051 100644
--- a/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
+++ b/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
@@ -2,8 +2,14 @@
 
 Answers one question: **can the original idea survive as DLC?**
 
-Original idea: **1 episode = $5**, **12 episodes = $30** (half off), and the player **chooses which
-episodes** they get — unlike a DLC bundle, whose contents are pre-selected.
+The model being priced:
+
+| Item | Price | Notes |
+|---|---|---|
+| 1 episode | **$5** | Player picks which |
+| 12 episodes | **$30** | Half off |
+| **Lifetime pass** | **$90** | All episodes — past, present, and future |
+| **Lifetime spend cap** | **$120** | At $120 total spend the pass is granted, and we never take more |
 
 Companion to `DLC_VS_TOKENS_QUESTIONNAIRE.md` (the decision + counsel questions).
 
@@ -18,161 +24,252 @@ Companion to `DLC_VS_TOKENS_QUESTIONNAIRE.md` (the decision + counsel questions)
 | **Where does the player pay?** | Steam **store page** → DLC. **In-game** store UI → Microtransaction API. |
 | **What does the SKU grant?** | Fixed content → an episode. Flexible credit → a token. |
 
-Those two are independent. You can sell a **token pack as DLC** — bought on the store page, granting
-credit the player spends on whichever episodes they like. That combination is what preserves the
-original idea *without* the backend that the in-game Microtransaction API demands.
+Those are independent. You can sell a **token pack as DLC** — bought on the store page, granting
+credit the player spends on whichever episodes they like. That combination preserves the original
+idea *without* the backend the in-game Microtransaction API demands.
 
-**So: yes, the original idea survives as DLC.** The catch is a refund edge case, covered in §4.
+**So yes, the original idea survives as DLC** — with two caveats: a refund edge case (§6) and the
+spend cap, which Steam cannot enforce for you (§4).
 
 ---
 
-## 1. What each mechanism can and cannot express
+## 1. What each mechanism can express
 
-| Capability | Individual DLC | Steam Bundle | Token pack sold as DLC | Tokens via MicroTxn |
+| Capability | Individual DLC | Steam Bundle | Token pack as DLC | Tokens via MicroTxn |
 |---|---|---|---|---|
-| Player picks **which** episode | ✅ | ❌ pre-selected set | ✅ | ✅ |
-| Discount on a **partial, player-chosen** selection | ❌ | ❌ | ✅ | ✅ |
+| Player picks **which** episode | ✅ | ❌ pre-selected | ✅ | ✅ |
+| Discount on a **player-chosen partial** selection | ❌ | ❌ | ✅ | ✅ |
 | Discount on the **full set** | ❌ | ✅ | ✅ | ✅ |
-| Already-owned items discounted automatically | — | ✅ Complete the Set | ❌ manual | ❌ manual |
-| Purchase happens **in-game** | ❌ overlay → store | ❌ | ❌ overlay → store | ✅ |
+| Already-owned items auto-discounted | — | ✅ Complete the Set | ❌ manual | ❌ manual |
+| Grants **future** content automatically | ❌ | ❌ | ✅ via pass SKU | ✅ via pass SKU |
+| Enforce a **lifetime spend cap** | ❌ | ❌ | ⚠️ you implement it | ⚠️ you implement it |
+| Purchase happens in-game | ❌ overlay → store | ❌ | ❌ overlay → store | ✅ |
 | Wishlist / gifting / regional pricing | ✅ | ✅ | ✅ | ❌ |
-| Valve is merchant of record (tax, refunds) | ✅ | ✅ | ✅ | ✅ |
-| **Backend required** | ❌ none | ❌ none | ❌ none | ✅ InitTxn/FinalizeTxn + verification |
-| Steamworks SDK needed | ownership check | ownership check | ownership check | full MicroTxn integration |
+| Valve is merchant of record | ✅ | ✅ | ✅ | ✅ |
+| **Backend required** | ❌ | ❌ | ❌ | ✅ InitTxn + verification |
 
 **The one thing plain DLC cannot do:** discount a selection the *player* chooses. A DLC SKU maps to
-fixed content and a Steam Bundle discounts a fixed set. "Any 6 of 12, half off" is not a storefront
-primitive on Steam. Only a credit/token abstraction expresses it.
+fixed content; a Steam Bundle discounts a fixed set. "Any 6 of 12, half off" is not a storefront
+primitive. Only a credit/token abstraction expresses it.
 
 ---
 
-## 2. The four viable routes
+## 2. The routes, with advantages and disadvantages
 
-### Route A — Individual DLC + a Complete-the-Set bundle
-12 episode DLCs at $5, plus an "All Episodes" bundle at $30.
+### Route A — Individual DLC + Complete-the-Set bundle + pass SKU
 
-- ✅ Smallest possible build: an ownership check per episode, nothing else.
-- ✅ **Complete the Set** automatically discounts by what the player already owns, so someone with 3
-  episodes pays roughly the remainder rather than full bundle price.
-- ❌ Choosing 6 specific episodes costs $30 — full price. No partial discount.
+| ✅ Advantages | ❌ Disadvantages |
+|---|---|
+| Smallest build: an ownership check per episode | **No partial-selection discount** — 6 chosen episodes cost full price |
+| Complete the Set auto-credits what the player owns | Spend cap must still be hand-built |
+| Refunds are automatic — ownership *is* the entitlement | Pass SKU cannot be auto-discounted by prior episode purchases |
+| No token wallet, no revoke path, no grant bugs | Every future episode needs a new SKU authored |
+| Wishlists, gifting, regional pricing all free | |
 
 ### Route B — Token packs sold as DLC ⭐ recommended
-"Episode Pack (12)" DLC at $30 grants 12 tokens; a "Single Episode Token" DLC at $5 grants 1. The
-player redeems tokens against any episode, in-game.
 
-- ✅ **Preserves the original idea exactly**: player choice *and* the half-off pack price.
-- ✅ Valve handles payment, tax, refunds, regional pricing; wishlists and gifting still work.
-- ✅ **No backend** — ownership is read from Steam, the grant is local and idempotent.
-- ✅ The token wallet already built (`EpisodeTokenService`) is the entitlement store; the purchase
-  provider becomes a DLC-ownership reader instead of a payment client.
-- ⚠️ Refund asymmetry — see §4.
-- ⚠️ Store page must state plainly: *"grants 12 episode unlocks, redeemable against any episodes."*
+| ✅ Advantages | ❌ Disadvantages |
+|---|---|
+| **Preserves the original idea exactly** — choice *and* pack discount | Refund asymmetry: spent tokens need a revoke path (§6) |
+| Valve stays merchant of record: tax, refunds, regional pricing | Store page must plainly say what a pack grants |
+| **No backend** — read DLC ownership, grant locally, idempotent | Two concepts for the player to learn (tokens *and* episodes) |
+| The wallet already built is reused as-is | Tokens are a prepaid balance — see the legal note in §5 |
+| Spend cap is implementable, because you control the ledger | Needs a Valve sanity-check before building on it |
+| Future episodes need no new SKU — tokens already cover them | |
 
 ### Route C — Tokens via the Microtransaction API
-An in-game store that charges the Steam Wallet directly.
 
-- ✅ Purchase never leaves the game; best conversion.
-- ❌ Requires a backend for InitTxn/FinalizeTxn and order verification. Real engineering time.
-- ❌ No wishlists, no gifting, no store-page visibility for the packs.
-- Only worth it if the in-game store becomes a significant, recurring surface.
+| ✅ Advantages | ❌ Disadvantages |
+|---|---|
+| Purchase never leaves the game — best conversion | **Requires a backend** (InitTxn/FinalizeTxn + verification) |
+| Full control of the in-game store surface | No wishlists, no gifting, no store-page visibility |
```

</details>

### `65f33a985` — docs(economy): threshold-credit model for the $120 cap

_Claude, 2026-08-04 01:43:24 +0000_

```text
Adds section 4b analysing "every dollar over $30 discounts the $90 pass".

The formula is pass = 90 - max(0, spend - 30). Verified: above $30 of
spend the total is exactly $120 on every path, and at $120 the pass costs
nothing and is simply granted, which is precisely the stated rule. The
uncredited first $30 is what sets the ceiling at $120 rather than $90.

Enforcement: Steam has no API that prices a SKU per player, so the
sliding price cannot be a store-page price. Five mechanisms compared. The
best no-backend option is tiered upgrade SKUs - the pass authored at
about six price points, hidden from the store, opened through the overlay
so the player only ever sees the correct one. A continuous sliding price
requires the Microtransaction API and a backend.

Carries forward the constraint from section 4: spend is not knowable from
Steam, so the credit must be computed from which SKUs the player owns at
our list prices, never from dollars paid.

Also flags a fairness wrinkle: below $30 of spend, sampling first costs
more than going straight to the pass - one episode then the pass is $95
against $90 for the pass alone. It does not break the "never more than
$120" promise but players notice. Fix is either to credit from dollar one
(ceiling drops to $90) or to keep the threshold and never show a
single-episode price next to the pass.
```

```text
 Docs/ECONOMY_TABLES.md               | 22 ++++++++++++++++++---
 Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md | 70 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++--
 2 files changed, 87 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/ECONOMY_TABLES.md b/Docs/ECONOMY_TABLES.md
index 81c045cdb..879cded94 100644
--- a/Docs/ECONOMY_TABLES.md
+++ b/Docs/ECONOMY_TABLES.md
@@ -87,9 +87,25 @@ earned before that point bank toward the first purchase rather than being wasted
 | Delivery mechanism | *Pending — see `Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md`* |
 | Mid tier (6 for $20) | *Proposed — decides DLC vs tokens* |
 
-> ⚠️ **The $120 cap only holds on the pack path.** 12-pack ($30) + pass ($90) = $120 exactly, but
-> twelve singles ($60) + pass ($90) = $150. The cap has to be delivered by crediting prior
-> purchases — Steam cannot enforce a spend ceiling. See the analysis §4.
+### Threshold credit — how the $120 cap is delivered
+
+`pass price = $90 − max(0, spend − $30)`
+
+| Spend so far | Credited | Pass price | **Total** |
+|---|---|---|---|
+| $0 | $0 | $90 | $90 |
+| $25 (5 episodes) | $0 | $90 | $115 |
+| $30 (12-pack) | $0 | $90 | **$120** |
+| $40 | $10 | $80 | **$120** |
+| $60 (12 singles) | $30 | $60 | **$120** |
+| $120 | $90 | **free** | **$120** |
+
+Above $30 of spend the total is **exactly $120 on every path**, and at $120 the pass is granted.
+
+> ⚠️ **Steam cannot price a SKU per player.** The credit must be computed from **which SKUs the
+> player owns** at our list prices, never from dollars paid — sales, regional pricing and gifts break
+> any dollar inference. Delivering the sliding price with no backend means **stepped upgrade SKUs**;
+> a continuous price means the Microtransaction API and a backend. See the analysis §4 and §4b.
 
 ---
 
diff --git a/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md b/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
index cddb1f051..8cbedd761 100644
--- a/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
+++ b/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
@@ -172,6 +172,66 @@ holds natively for every purchase path — no ledger, no revoke, no inference.
 
 ---
 
+## 4b. The threshold-credit model — "every dollar over $30 discounts the pass"
+
+**The formula:** `pass price = $90 − max(0, spend − $30)`
+
+It is self-consistent, and the arithmetic is genuinely elegant:
+
+| Spend so far | Credited | Pass price | **Total** |
+|---|---|---|---|
+| $0 | $0 | $90 | **$90** |
+| $5 (1 episode) | $0 | $90 | $95 |
+| $25 (5 episodes) | $0 | $90 | $115 |
+| $30 (12-pack) | $0 | $90 | **$120** |
+| $40 | $10 | $80 | **$120** |
+| $60 (12 singles) | $30 | $60 | **$120** |
+| $90 | $60 | $30 | **$120** |
+| $120 | $90 | **$0 — granted** | **$120** |
+
+**Above $30 of spend the total is exactly $120, on every path.** At $120 the pass costs nothing and
+is simply granted, which is precisely the "unlock the pass at $120" rule. The uncredited first $30 is
+what makes the ceiling $120 rather than $90 — credit from dollar one and the ceiling drops to $90.
+
+### Can Steam enforce it?
+
+**Not as a store-page price.** There is no API that sets a DLC's price per player. Steam prices are
+per-SKU and per-region, never per-user. Five ways to land it anyway:
+
+| Mechanism | Exact formula? | Backend? | Verdict |
+|---|---|---|---|
+| **MicroTxn** — you specify the amount on each transaction | ✅ exact, continuous | ✅ required | The **only** route that computes an arbitrary per-player price |
+| **Tiered upgrade SKUs** — e.g. Pass at $90/$75/$60/$45/$30/$15/free, hidden from the store, opened via the overlay | ≈ stepped | ❌ none | Best no-backend approximation |
+| **Complete-the-Set bundle** | ❌ Steam's own math | ❌ none | Automatic ownership credit, but the arithmetic is Valve's, not yours |
+| **Grant-on-threshold** — never discount, just *give* the pass once owned value crosses $120 | ✅ for the cap | ❌ none | Delivers the promise, not the sliding price |
+| **Manual refund of the overage** | ✅ exact | ❌ none | Backstop only; operationally costly |
+
+> **Recommended if you want this model:** tiered upgrade SKUs. Author the pass at ~6 price points,
+> mark them **not visible on the store**, and open the correct one through the Steam overlay from
+> in-game. No backend, and the player sees one price — the right one. Confirm with Valve that DLC can
+> be hidden from the store page before committing.
+>
+> If the sliding price must be continuous rather than stepped, that is **MicroTxn, and a backend**.
+
+### The constraint from §4 still applies
+
+"Spend" is **not knowable** from Steam. Sales, regional pricing, gifts, and refunds all break any
+inference from dollars. So the credit must be computed from **which SKUs the player owns**, valued at
+your own list prices — not from what they actually paid. Write the rule as *"own the 12-pack → the
+pass is $60"*, never *"spent $30 → the pass is $60"*.
+
+### One wrinkle worth fixing
+
+Below $30 of spend, buying à la carte first **costs more than going straight to the pass**: one
+episode ($5) then the pass ($90) is $95, against $90 for the pass alone. It does not break the "never
+more than $120" promise, but players notice paying extra for having sampled first. Two fixes:
+
+- **Credit from dollar one** (`pass = $90 − spend`) — fair at every point, ceiling becomes $90.
+- **Keep the $30 threshold** and make sure the store never shows a single-episode price to someone
+  who is one click from the pass.
+
+---
+
 ## 5. Legality — the cap and the "lifetime" claim
 
 Not legal advice. These are the specific issues to put in front of counsel.
@@ -239,6 +299,9 @@ one requirement only tokens satisfy, and Route B becomes correct instead.
 
 **Decision rule:** if the 6-of-12 mid tier is real → **Route B**. If not → **Route A**.
 
+**If the §4b sliding-credit model is a hard requirement**, that changes the answer again: stepped
+upgrade SKUs keep you on Route A/B with no backend, and a continuous sliding price forces Route C.
+
 ---
 
 ## 8. What this means for what is already built
@@ -268,8 +331,11 @@ was for.
 4. **How many free tokens ship with the base game?** Still unanswered; only meaningful under a token
    route.
 5. **Does a refund revoke the pass** once auto-granted? (§6)
-6. **Confirm with Valve**: that a DLC granting redeemable credit is acceptable, and that a
-   Complete-the-Set bundle can contain both the episodes and the pass.
+6. **Is the sliding credit (§4b) a requirement, or is a stepped approximation acceptable?** Stepped
+   keeps the no-backend routes open; continuous forces MicroTxn.
+7. **Confirm with Valve**: that a DLC granting redeemable credit is acceptable, and that a
+   Complete-the-Set bundle can contain both the episodes and the pass, and that DLC can be hidden
+   from the store page.
 
 > Platform rules change and none of this is legal advice. Verify DLC, bundle, and Complete-the-Set
 > behaviour in Steamworks before committing, and take §5 to counsel.
```

</details>

### `019cc66e1` — docs(economy): entitlement-based credit model and its evolution path

_Claude, 2026-08-04 02:04:54 +0000_

```text
Replaces the dollar-based credit in section 4b with an entitlement-based
one that Steam can actually support, and records the launch
simplification plus the path to the full model.

credit = (list value of everything owned) - $30, clamped to $0-$90, where
list value is $5 per single owned plus $30 if the 12-pack is owned. This
caps at exactly $120 on every path, keeps working as the catalogue grows,
and cannot be corrupted by sales, regions, gifts or refunds because it
never asks what the player paid - only what they own. In episode terms it
is $5 per single owned beyond the sixth.

Records why "$2.50 per episode beyond 12" was rejected: the per-episode
shape is right but the coefficient is the pack rate while singles cost
$5, so it credits half of what an a-la-carte buyer paid and overshoots
the cap by $15-$30 in three common cases. It also never fires at all
until the catalogue passes twelve episodes.

Launch simplification: stop selling singles once list value reaches $30.
The credit term is then always zero, one $90 pass SKU suffices instead of
roughly nineteen stepped ones, and the ceiling holds by construction
rather than by a rule policed across regions and sales. The full formula
is the same model with the credit switched on, so growing into it means
authoring more SKUs rather than redesigning.

Also flags an inconsistency: the tables say six episodes at launch while
the pack is priced for twelve.
```

```text
 Docs/ECONOMY_TABLES.md               |  35 ++++++++++---------
 Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md | 124 +++++++++++++++++++++++++++++++++++++++++++++++++----------------
 2 files changed, 112 insertions(+), 47 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 218 lines)</summary>

```diff
diff --git a/Docs/ECONOMY_TABLES.md b/Docs/ECONOMY_TABLES.md
index 879cded94..8ec0b652a 100644
--- a/Docs/ECONOMY_TABLES.md
+++ b/Docs/ECONOMY_TABLES.md
@@ -87,25 +87,26 @@ earned before that point bank toward the first purchase rather than being wasted
 | Delivery mechanism | *Pending — see `Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md`* |
 | Mid tier (6 for $20) | *Proposed — decides DLC vs tokens* |
 
-### Threshold credit — how the $120 cap is delivered
+### Credit model — how the $120 cap is delivered
 
-`pass price = $90 − max(0, spend − $30)`
+> **credit = (list value of everything owned) − $30**, clamped to $0–$90
+> where list value = `$5 × singles owned` + `$30 if the 12-pack is owned`
 
-| Spend so far | Credited | Pass price | **Total** |
-|---|---|---|---|
-| $0 | $0 | $90 | $90 |
-| $25 (5 episodes) | $0 | $90 | $115 |
-| $30 (12-pack) | $0 | $90 | **$120** |
-| $40 | $10 | $80 | **$120** |
-| $60 (12 singles) | $30 | $60 | **$120** |
-| $120 | $90 | **free** | **$120** |
-
-Above $30 of spend the total is **exactly $120 on every path**, and at $120 the pass is granted.
-
-> ⚠️ **Steam cannot price a SKU per player.** The credit must be computed from **which SKUs the
-> player owns** at our list prices, never from dollars paid — sales, regional pricing and gifts break
-> any dollar inference. Delivering the sliding price with no backend means **stepped upgrade SKUs**;
-> a continuous price means the Microtransaction API and a backend. See the analysis §4 and §4b.
+| Owned | List value | Credit | Pass | **Total** |
+|---|---|---|---|---|
+| 3 singles | $15 | $0 | $90 | $105 |
+| 12-pack | $30 | $0 | $90 | **$120** |
+| 12 singles | $60 | $30 | $60 | **$120** |
+| Pack + 6 later singles | $60 | $30 | $60 | **$120** |
+| Pack + 12 later singles | $90 | $60 | $30 | **$120** |
+
+Caps at exactly **$120 on every path**. Computed from **owned SKUs at our list prices**, never from
+dollars paid — so sales, regions, gifts and refunds cannot corrupt it.
+
+**At launch, ship the simplification:** stop selling singles once list value reaches $30, so the
+credit is always zero, **one $90 pass SKU** is enough, and the ceiling holds by construction. The
+full formula is the same model with the credit switched on — see the analysis §4b for the evolution
+path.
 
 ---
 
diff --git a/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md b/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
index 8cbedd761..47adb26fc 100644
--- a/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
+++ b/Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md
@@ -172,11 +172,13 @@ holds natively for every purchase path — no ledger, no revoke, no inference.
 
 ---
 
-## 4b. The threshold-credit model — "every dollar over $30 discounts the pass"
+## 4b. The credit model — how the $120 cap is actually delivered
 
-**The formula:** `pass price = $90 − max(0, spend − $30)`
+Two formulations were considered. The second is the one to build.
 
-It is self-consistent, and the arithmetic is genuinely elegant:
+### The dollar formulation (the original idea)
+
+`pass price = $90 − max(0, spend − $30)`
 
 | Spend so far | Credited | Pass price | **Total** |
 |---|---|---|---|
@@ -186,50 +188,110 @@ It is self-consistent, and the arithmetic is genuinely elegant:
 | $30 (12-pack) | $0 | $90 | **$120** |
 | $40 | $10 | $80 | **$120** |
 | $60 (12 singles) | $30 | $60 | **$120** |
-| $90 | $60 | $30 | **$120** |
 | $120 | $90 | **$0 — granted** | **$120** |
 
-**Above $30 of spend the total is exactly $120, on every path.** At $120 the pass costs nothing and
-is simply granted, which is precisely the "unlock the pass at $120" rule. The uncredited first $30 is
-what makes the ceiling $120 rather than $90 — credit from dollar one and the ceiling drops to $90.
+Above $30 of spend the total is exactly **$120 on every path**, and at $120 the pass is granted. The
+uncredited first $30 is what sets the ceiling at $120 rather than $90 — credit from dollar one and
+the ceiling drops to $90.
+
+**But "spend" is not knowable from Steam** (§4). Sales, regional pricing, gifts, and refunds all
+break any dollar inference, and there is no lifetime-spend API at all. So this formulation cannot be
+implemented as written.
+
+### The entitlement formulation ⭐ build this
 
-### Can Steam enforce it?
+Same outcome, computed from something Steam actually reports — **which SKUs the player owns**, valued
+at *our* list prices:
+
+> **credit = (list value of everything owned) − $30**, clamped to $0–$90
+> where list value = `$5 × singles owned` + `$30 if the 12-pack is owned`
+
+| Case | List value | Credit | Pass | **Total** |
+|---|---|---|---|---|
+| 12-pack only | $30 | $0 | $90 | **$120** |
+| 12 singles | $60 | $30 | $60 | **$120** |
+| Pack + 6 later singles | $60 | $30 | $60 | **$120** |
+| Pack + 12 later singles | $90 | $60 | $30 | **$120** |
+| 3 singles | $15 | $0 | $90 | $105 |
+
+Caps at exactly $120 on every path, keeps working as the catalogue grows, and **cannot be corrupted
+by sales, regions, gifts, or refunds** — because it never asks what the player paid, only what they
+own. In episode terms it reads as **$5 credit per single-episode SKU owned beyond the sixth**, since
+six singles at $5 is precisely the $30 uncredited base.
+
+### ❌ Rejected: "$2.50 per episode owned beyond 12"
+
+The per-episode shape is right, but this coefficient breaks the cap:
+
+| Case | Paid | Credit | Pass | Total |
+|---|---|---|---|---|
+| 12-pack only | $30 | $0 | $90 | $120 ✅ |
+| 12 singles | $60 | $0 | $90 | **$150** ❌ |
+| Pack + 6 later singles | $60 | $15 | $75 | **$135** ❌ |
+| Pack + 12 later singles | $90 | $30 | $60 | **$150** ❌ |
 
-**Not as a store-page price.** There is no API that sets a DLC's price per player. Steam prices are
-per-SKU and per-region, never per-user. Five ways to land it anyway:
+Two reasons it fails. **$2.50 is the pack rate, but singles cost $5** — it credits half of what an
+à-la-carte buyer actually paid, so every one of them overshoots. And **"beyond 12" never fires**:
+with 6 episodes at launch nobody can own a 13th for a long time, so the rule does nothing until the
+catalogue outgrows the pack.
 
-| Mechanism | Exact formula? | Backend? | Verdict |
+### Can Steam enforce a sliding price?
+
+**Not as a store-page price.** There is no API that prices a SKU per player — prices are per-SKU and
+per-region, never per-user. Five ways to land it anyway:
+
+| Mechanism | Exact? | Backend? | Verdict |
 |---|---|---|---|
-| **MicroTxn** — you specify the amount on each transaction | ✅ exact, continuous | ✅ required | The **only** route that computes an arbitrary per-player price |
-| **Tiered upgrade SKUs** — e.g. Pass at $90/$75/$60/$45/$30/$15/free, hidden from the store, opened via the overlay | ≈ stepped | ❌ none | Best no-backend approximation |
-| **Complete-the-Set bundle** | ❌ Steam's own math | ❌ none | Automatic ownership credit, but the arithmetic is Valve's, not yours |
-| **Grant-on-threshold** — never discount, just *give* the pass once owned value crosses $120 | ✅ for the cap | ❌ none | Delivers the promise, not the sliding price |
+| **MicroTxn** — you specify the amount per transaction | ✅ exact, continuous | ✅ required | The **only** route that computes an arbitrary per-player price |
+| **Tiered upgrade SKUs** — pass authored at several price points, hidden from the store, opened via the overlay | ≈ stepped | ❌ none | Best no-backend approximation of the sliding price |
+| **Complete-the-Set bundle** | ❌ Valve's own math | ❌ none | Automatic ownership credit, arithmetic is Valve's not yours |
+| **Grant-on-threshold** — never discount, just *give* the pass once owned value reaches $120 | ✅ for the cap | ❌ none | Delivers the promise, not the sliding price |
 | **Manual refund of the overage** | ✅ exact | ❌ none | Backstop only; operationally costly |
 
-> **Recommended if you want this model:** tiered upgrade SKUs. Author the pass at ~6 price points,
-> mark them **not visible on the store**, and open the correct one through the Steam overlay from
-> in-game. No backend, and the player sees one price — the right one. Confirm with Valve that DLC can
-> be hidden from the store page before committing.
->
```

</details>
