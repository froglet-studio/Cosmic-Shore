# DLC vs. Episode Tokens — analysis

Answers one question: **can the original idea survive as DLC?**

Original idea: **1 episode = $5**, **12 episodes = $30** (half off), and the player **chooses which
episodes** they get — unlike a DLC bundle, whose contents are pre-selected.

Companion to `DLC_VS_TOKENS_QUESTIONNAIRE.md` (the decision + counsel questions).

---

## 0. The reframe that changes the answer

"DLC or tokens" is the wrong axis. On Steam, **both are Steam purchases**. The real axis is:

| Question | Answer determines |
|---|---|
| **Where does the player pay?** | Steam **store page** → DLC. **In-game** store UI → Microtransaction API. |
| **What does the SKU grant?** | Fixed content → an episode. Flexible credit → a token. |

Those two are independent. You can sell a **token pack as DLC** — bought on the store page, granting
credit the player spends on whichever episodes they like. That combination is what preserves the
original idea *without* the backend that the in-game Microtransaction API demands.

**So: yes, the original idea survives as DLC.** The catch is a refund edge case, covered in §4.

---

## 1. What each mechanism can and cannot express

| Capability | Individual DLC | Steam Bundle | Token pack sold as DLC | Tokens via MicroTxn |
|---|---|---|---|---|
| Player picks **which** episode | ✅ | ❌ pre-selected set | ✅ | ✅ |
| Discount on a **partial, player-chosen** selection | ❌ | ❌ | ✅ | ✅ |
| Discount on the **full set** | ❌ | ✅ | ✅ | ✅ |
| Already-owned items discounted automatically | — | ✅ Complete the Set | ❌ manual | ❌ manual |
| Purchase happens **in-game** | ❌ overlay → store | ❌ | ❌ overlay → store | ✅ |
| Wishlist / gifting / regional pricing | ✅ | ✅ | ✅ | ❌ |
| Valve is merchant of record (tax, refunds) | ✅ | ✅ | ✅ | ✅ |
| **Backend required** | ❌ none | ❌ none | ❌ none | ✅ InitTxn/FinalizeTxn + verification |
| Steamworks SDK needed | ownership check | ownership check | ownership check | full MicroTxn integration |

**The one thing plain DLC cannot do:** discount a selection the *player* chooses. A DLC SKU maps to
fixed content and a Steam Bundle discounts a fixed set. "Any 6 of 12, half off" is not a storefront
primitive on Steam. Only a credit/token abstraction expresses it.

---

## 2. The four viable routes

### Route A — Individual DLC + a Complete-the-Set bundle
12 episode DLCs at $5, plus an "All Episodes" bundle at $30.

- ✅ Smallest possible build: an ownership check per episode, nothing else.
- ✅ **Complete the Set** automatically discounts by what the player already owns, so someone with 3
  episodes pays roughly the remainder rather than full bundle price.
- ❌ Choosing 6 specific episodes costs $30 — full price. No partial discount.

### Route B — Token packs sold as DLC ⭐ recommended
"Episode Pack (12)" DLC at $30 grants 12 tokens; a "Single Episode Token" DLC at $5 grants 1. The
player redeems tokens against any episode, in-game.

- ✅ **Preserves the original idea exactly**: player choice *and* the half-off pack price.
- ✅ Valve handles payment, tax, refunds, regional pricing; wishlists and gifting still work.
- ✅ **No backend** — ownership is read from Steam, the grant is local and idempotent.
- ✅ The token wallet already built (`EpisodeTokenService`) is the entitlement store; the purchase
  provider becomes a DLC-ownership reader instead of a payment client.
- ⚠️ Refund asymmetry — see §4.
- ⚠️ Store page must state plainly: *"grants 12 episode unlocks, redeemable against any episodes."*

### Route C — Tokens via the Microtransaction API
An in-game store that charges the Steam Wallet directly.

- ✅ Purchase never leaves the game; best conversion.
- ❌ Requires a backend for InitTxn/FinalizeTxn and order verification. Real engineering time.
- ❌ No wishlists, no gifting, no store-page visibility for the packs.
- Only worth it if the in-game store becomes a significant, recurring surface.

### Route D — Season Pass DLC (all episodes)
One $30 SKU granting everything, forever, including future episodes.

- ✅ Simplest to communicate and to build.
- ❌ No choice at all, and it commits you to delivering future episodes to existing buyers.

---

## 3. The pricing math — and a problem in it

| Episodes wanted | Buy individually | Buy the 12-pack | Player's rational choice |
|---|---|---|---|
| 1 | $5 | $30 | Individual |
| 3 | $15 | $30 | Individual |
| 5 | $25 | $30 | Individual (marginal) |
| **6** | **$30** | **$30** | **Pack — same price, 6 more episodes** |
| 9 | $45 | $30 | Pack |
| 12 | $60 | $30 | Pack |

> ⚠️ **The mid-range is dead.** At 6 episodes, buying individually costs exactly what all 12 cost.
> Nobody rational buys 6–11 individually, so the catalogue is effectively "1–5 episodes, or
> everything." That is not necessarily wrong — it is a strong upsell — but it means the
> player-choice discount you are designing for **never actually gets used**: anyone who wants a
> discount just buys all 12, and there is nothing left to choose.

**This is the crux.** Player choice only has value when the pack is **smaller than the catalogue**.
With 12 of 12, Route A and Route B deliver identical player outcomes, and Route A is far less work.

Choice becomes genuinely valuable if either:
- the catalogue grows past 12 (packs stay a subset), or
- you add a **mid-tier pack** — e.g. **6 tokens for $20** ($3.33 each), which fills the dead zone and
  gives the player a real "which six?" decision.

| Suggested ladder | Price | Per episode | Discount |
|---|---|---|---|
| 1 episode | $5.00 | $5.00 | — |
| 6 tokens (pick any) | $20.00 | $3.33 | 33% |
| 12 tokens (all) | $30.00 | $2.50 | 50% |

---

## 4. The refund edge case (Route B's only real cost)

Steam refunds the **pack**, but the player may already have **spent** the tokens. The game must
handle ownership going away:

1. On launch, read DLC ownership from Steam.
2. If a pack the player was granted for is no longer owned, revoke that grant: remove the tokens if
   unspent, and revoke episodes unlocked by them if not.
3. Never let the balance go negative; clamp and log.

`EpisodeTokenService` is idempotent per order id, so a re-grant after a re-purchase is safe. The
**revoke** path does not exist yet and is the main piece of work Route B adds over Route A.

Individual DLC (Route A) has no such problem: ownership *is* the entitlement, so a refund removes
access automatically with no code.

---

## 5. Recommendation

**If the catalogue stays at 12 and the pack is all 12 → Route A.** The player-choice discount has
nothing to bite on, and Route A costs almost nothing to build.

**If you add a mid-tier pack, or the catalogue will grow → Route B.** It preserves the original idea
in full, keeps Valve as merchant of record, and needs no backend. Budget the revoke path in §4.

**Route C only if an in-game store becomes a real surface.** It is the only option that needs a
backend, and it gives up wishlists and gifting to get in-game checkout.

---

## 6. What this means for what is already built

The token layer is **not wasted under any route**:

| Component | Route A | Route B | Route C |
|---|---|---|---|
| `EpisodeTokenService` (wallet, ownership, idempotency) | Ownership store only; tokens unused | ✅ as built | ✅ as built |
| `IEpisodeTokenPurchaseProvider` | DLC ownership reader | DLC ownership reader | MicroTxn client |
| `SO_EpisodeTokenConfig` | Prices only | ✅ as built | ✅ as built |
| `EpisodeTokenController` | ✅ as built | ✅ as built | ✅ as built |
| Revoke-on-refund | not needed | **to build** | **to build** |

Under every route the purchase provider is the only piece that changes, which is exactly what the
interface was for.

---

## 7. Open questions

1. **Will the catalogue grow past 12?** This single answer picks Route A or Route B.
2. **Do you want the mid-tier pack** (6 for $20)? Without it, player choice is decorative.
3. **How many free tokens ship with the base game?** Still unanswered, and it interacts with all of
   this — free tokens only make sense under a token route.
4. **Confirm with Valve** that a DLC granting redeemable episode credit is acceptable. It is an
   ordinary pattern, but worth a partner-support message before building on it.

> Platform rules change and none of this is legal advice. Verify DLC, bundle, and Complete-the-Set
> behaviour in Steamworks before committing, and take §4 of the questionnaire to counsel.
