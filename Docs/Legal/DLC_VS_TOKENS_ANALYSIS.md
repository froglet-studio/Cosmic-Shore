# DLC vs. Episode Tokens — analysis

Answers one question: **can the original idea survive as DLC?**

The model being priced:

| Item | Price | Notes |
|---|---|---|
| 1 episode | **$5** | Player picks which |
| 12 episodes | **$30** | Half off |
| **Lifetime pass** | **$90** | All episodes — past, present, and future |
| **Lifetime spend cap** | **$120** | At $120 total spend the pass is granted, and we never take more |

Companion to `DLC_VS_TOKENS_QUESTIONNAIRE.md` (the decision + counsel questions).

---

## 0. The reframe that changes the answer

"DLC or tokens" is the wrong axis. On Steam, **both are Steam purchases**. The real axis is:

| Question | Answer determines |
|---|---|
| **Where does the player pay?** | Steam **store page** → DLC. **In-game** store UI → Microtransaction API. |
| **What does the SKU grant?** | Fixed content → an episode. Flexible credit → a token. |

Those are independent. You can sell a **token pack as DLC** — bought on the store page, granting
credit the player spends on whichever episodes they like. That combination preserves the original
idea *without* the backend the in-game Microtransaction API demands.

**So yes, the original idea survives as DLC** — with two caveats: a refund edge case (§6) and the
spend cap, which Steam cannot enforce for you (§4).

---

## 1. What each mechanism can express

| Capability | Individual DLC | Steam Bundle | Token pack as DLC | Tokens via MicroTxn |
|---|---|---|---|---|
| Player picks **which** episode | ✅ | ❌ pre-selected | ✅ | ✅ |
| Discount on a **player-chosen partial** selection | ❌ | ❌ | ✅ | ✅ |
| Discount on the **full set** | ❌ | ✅ | ✅ | ✅ |
| Already-owned items auto-discounted | — | ✅ Complete the Set | ❌ manual | ❌ manual |
| Grants **future** content automatically | ❌ | ❌ | ✅ via pass SKU | ✅ via pass SKU |
| Enforce a **lifetime spend cap** | ❌ | ❌ | ⚠️ you implement it | ⚠️ you implement it |
| Purchase happens in-game | ❌ overlay → store | ❌ | ❌ overlay → store | ✅ |
| Wishlist / gifting / regional pricing | ✅ | ✅ | ✅ | ❌ |
| Valve is merchant of record | ✅ | ✅ | ✅ | ✅ |
| **Backend required** | ❌ | ❌ | ❌ | ✅ InitTxn + verification |

**The one thing plain DLC cannot do:** discount a selection the *player* chooses. A DLC SKU maps to
fixed content; a Steam Bundle discounts a fixed set. "Any 6 of 12, half off" is not a storefront
primitive. Only a credit/token abstraction expresses it.

---

## 2. The routes, with advantages and disadvantages

### Route A — Individual DLC + Complete-the-Set bundle + pass SKU

| ✅ Advantages | ❌ Disadvantages |
|---|---|
| Smallest build: an ownership check per episode | **No partial-selection discount** — 6 chosen episodes cost full price |
| Complete the Set auto-credits what the player owns | Spend cap must still be hand-built |
| Refunds are automatic — ownership *is* the entitlement | Pass SKU cannot be auto-discounted by prior episode purchases |
| No token wallet, no revoke path, no grant bugs | Every future episode needs a new SKU authored |
| Wishlists, gifting, regional pricing all free | |

### Route B — Token packs sold as DLC ⭐ recommended

| ✅ Advantages | ❌ Disadvantages |
|---|---|
| **Preserves the original idea exactly** — choice *and* pack discount | Refund asymmetry: spent tokens need a revoke path (§6) |
| Valve stays merchant of record: tax, refunds, regional pricing | Store page must plainly say what a pack grants |
| **No backend** — read DLC ownership, grant locally, idempotent | Two concepts for the player to learn (tokens *and* episodes) |
| The wallet already built is reused as-is | Tokens are a prepaid balance — see the legal note in §5 |
| Spend cap is implementable, because you control the ledger | Needs a Valve sanity-check before building on it |
| Future episodes need no new SKU — tokens already cover them | |

### Route C — Tokens via the Microtransaction API

| ✅ Advantages | ❌ Disadvantages |
|---|---|
| Purchase never leaves the game — best conversion | **Requires a backend** (InitTxn/FinalizeTxn + verification) |
| Full control of the in-game store surface | No wishlists, no gifting, no store-page visibility |
| Same flexibility as Route B | Most engineering time by a wide margin |

### Route D — Lifetime pass only ($90, no à-la-carte)

| ✅ Advantages | ❌ Disadvantages |
|---|---|
| Simplest possible: one SKU, one entitlement | No entry point under $90 — kills impulse purchases |
| No cap logic needed — $90 *is* the ceiling | No player choice at all |
| Trivially honest store page | Forward commitment to future episodes remains (§5) |

---

## 3. The price ladder — and where $120 breaks

Paths to owning everything:

| Path | Cost | Within the $120 cap? |
|---|---|---|
| Pass directly | **$90** | ✅ |
| 12-pack ($30) → pass ($90) | **$120** | ✅ exactly at the cap |
| 12 singles (12 × $5 = $60) → pass ($90) | **$150** | ❌ **overshoots by $30** |

> **The $120 ceiling is structural only on the pack path.** $30 + $90 = $120 exactly — elegant, and
> almost certainly where the number came from. But a player who buys episodes **one at a time**
> reaches $60 before the pass and blows through the cap by $30.

**Therefore the cap is not a price — it is a rule you must implement:** the pass has to be *credited
by prior spend*, or the player must be blocked from over-paying. Steam will not do this for you (§4).

There is also a mid-range problem:

| Episodes wanted | Individually | 12-pack | Rational choice |
|---|---|---|---|
| 1–5 | $5–$25 | $30 | Individual |
| **6** | **$30** | **$30** | **Pack — same price, 6 more episodes** |
| 7–12 | $35–$60 | $30 | Pack |

At six episodes, buying individually costs exactly what all twelve cost. **Nobody rational buys 6–11
individually**, so the player-choice discount you are designing for never actually gets exercised —
anyone wanting a discount buys all twelve, and there is nothing left to choose. Choice only has
value when a pack is **smaller than the catalogue**, which argues for a mid tier:

| Suggested ladder | Price | Per episode |
|---|---|---|
| 1 episode | $5.00 | $5.00 |
| 6 tokens (pick any) | $20.00 | $3.33 |
| 12 tokens (all current) | $30.00 | $2.50 |
| Lifetime pass (all future too) | $90.00 | — |

---

## 4. The $120 spend cap — Steam cannot enforce it

Three hard constraints, none of which have a platform-side fix.

**1. Steam does not tell you what a player spent.** There is no "lifetime spend on my app" API. You
can read *which DLC they own*, and infer spend from **list prices** — but that is not what they paid.
Sales, regional pricing, bundles, and gifts all break the inference. A player in a low-price region
who bought during a 40% sale might own $120 of list price having paid $45.

> **Consequence: define the cap in entitlements, not dollars.** "Own the 12-pack and the pass" is
> checkable. "Has spent $120" is not.

**2. You cannot block a purchase.** Steam store pages are always live. You can hide a buy button
in-game, but nothing stops a player buying a DLC directly from the store page — including one that
pushes them past the cap.

> **Consequence: deliver the promise by crediting, not by refusing.** If a player somehow pays past
> the cap, grant the pass and treat the excess as a discount you owe — or refund it manually.

**3. Regional pricing makes "$120" ambiguous.** Is it $120 USD, or the regional equivalent? Whatever
you decide has to be what the store page says, in every currency.

### What is actually implementable

| Mechanism | Feasible? | How |
|---|---|---|
| "Own everything → get the pass free" | ✅ | Ownership check; grant pass entitlement locally |
| "Complete the Set" discount on a bundle | ✅ | Native Steam bundle feature |
| Pass price reduced by prior episode purchases | ⚠️ | Only via a Complete-the-Set bundle containing the pass |
| Hard block on spending past $120 | ❌ | Store page cannot be gated |
| Exact dollar-based cap across regions and sales | ❌ | Spend is not knowable |

**Recommended shape:** put every episode SKU *and* the pass into one **Complete the Set bundle**
priced at $120. Steam then charges a player only for what they do not already own, and the ceiling
holds natively for every purchase path — no ledger, no revoke, no inference.

---

## 5. Legality — the cap and the "lifetime" claim

Not legal advice. These are the specific issues to put in front of counsel.

**The cap itself is legally easy.** Charging a customer *less* is consumer-favourable and no
regulator objects. **The risk is not the cap — it is failing to deliver it.** "You will never pay
more than $120" is an advertising claim, and it has to hold in every currency, every region, and
through every sale. If someone pays $150 because the store page let them, that is a false-advertising
and unfair-practice exposure. Given §4, the promise must be honoured by crediting after the fact.

**"Lifetime" and "future episodes" is the harder claim.** Open-ended promises of unreleased content
are the classic lifetime-pass problem:

| Issue | Why it matters | Mitigation to discuss with counsel |
|---|---|---|
| "Future episodes" with no end date | Promises content that may never be made | Define the term: episodes released **while the game is commercially available**, not forever |
| "Lifetime" wording | Regulators have challenged "lifetime" where it meant the *product's* life, not the customer's | Say "all episodes we release", not "lifetime" |
| Studio stops making episodes | Consumers paid for an expectation | Reserve the right to end the series; state it plainly at point of sale |
| Game shuts down | Entitlement becomes worthless | Standard service-termination clause |
| Cap stated in USD | Regional buyers pay a different number | Say which currency the cap is denominated in |
| Refund after auto-grant | Player refunds a component but keeps the pass | Define whether the pass is revoked (§6) |

**Prepaid balance.** Tokens are an unspent balance with no expiry. Some jurisdictions treat prepaid
balances as stored value with reporting or escheatment duties. Already logged as question 2.2 in the
questionnaire — it applies to Routes B and C, not to pure DLC.

> **The cleanest legal position is Route A/§4's Complete-the-Set bundle**: no prepaid balance, no
> spend ledger, no cap promise to enforce — the ceiling is just the bundle price, and Steam enforces
> it natively.

---

## 6. Refunds

| Route | Refund behaviour | Work |
|---|---|---|
| Individual DLC | Automatic — ownership *is* the entitlement | None |
| Complete-the-Set bundle | Automatic | None |
| Token pack as DLC | ⚠️ Pack refunded, tokens possibly already spent | **Revoke path needed** |
| MicroTxn tokens | ⚠️ Same, plus backend reconciliation | Revoke + backend |

The revoke path for Routes B/C: on launch, read DLC ownership; if a pack the player was granted for
is no longer owned, remove unspent tokens and revoke episodes unlocked by them; clamp at zero and
log. `EpisodeTokenService` is already idempotent per order id, so re-granting after re-purchase is
safe. **The revoke path does not exist yet** and is the main work Routes B and C add.

---

## 7. Recommendation

**Route A, with a Complete-the-Set bundle at $120 containing every episode plus the pass.**

The new requirements pushed the answer here. Specifically:

- The **$120 ceiling** is delivered natively by Complete the Set, with no ledger, no spend
  inference, and no cap promise you have to police across regions and sales.
- The **lifetime pass** is just another SKU, and it is what makes future episodes free.
- **No prepaid balance** means the stored-value question in §5 disappears entirely.
- **No revoke path**, because refunds are handled by ownership.

The cost is real: **no discount on a player-chosen partial selection**. But §3 shows that discount
never gets exercised at the current numbers anyway — at six episodes the pack is already the same
price. If you add the 6-for-$20 mid tier and genuinely want "pick any six at a discount," that is the
one requirement only tokens satisfy, and Route B becomes correct instead.

**Decision rule:** if the 6-of-12 mid tier is real → **Route B**. If not → **Route A**.

---

## 8. What this means for what is already built

The token layer is **not wasted under any route**:

| Component | Route A | Route B | Route C |
|---|---|---|---|
| `EpisodeTokenService` (ownership, idempotency) | Entitlement store; tokens unused | ✅ as built | ✅ as built |
| `IEpisodeTokenPurchaseProvider` | DLC ownership reader | DLC ownership reader | MicroTxn client |
| `SO_EpisodeTokenConfig` | Prices only | ✅ as built | ✅ as built |
| `EpisodeTokenController` | ✅ as built | ✅ as built | ✅ as built |
| Revoke-on-refund | not needed | **to build** | **to build** |
| Pass entitlement + "owns everything" check | **to build** | **to build** | **to build** |

Under every route the purchase provider is the only piece that changes — which is what the interface
was for.

---

## 9. Open questions

1. **Is the 6-of-12 mid tier real?** This single answer picks Route A or Route B (§7).
2. **Will the catalogue grow past 12?** If yes, the pass is doing heavy lifting and its $90 price
   needs justifying against the $30 twelve-pack.
3. **Is the cap $120 USD, or the regional equivalent?** Must match the store page (§4).
4. **How many free tokens ship with the base game?** Still unanswered; only meaningful under a token
   route.
5. **Does a refund revoke the pass** once auto-granted? (§6)
6. **Confirm with Valve**: that a DLC granting redeemable credit is acceptable, and that a
   Complete-the-Set bundle can contain both the episodes and the pass.

> Platform rules change and none of this is legal advice. Verify DLC, bundle, and Complete-the-Set
> behaviour in Steamworks before committing, and take §5 to counsel.
