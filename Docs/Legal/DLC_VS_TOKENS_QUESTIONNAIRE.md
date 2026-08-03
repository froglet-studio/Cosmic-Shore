# DLC vs. Tokens — decision questionnaire

External web checkout is **ruled out permanently** (confirmed). That leaves two compliant ways to
sell episodes on Steam. Answer these and the implementation follows directly.

Answer in the **Answer** column. Nothing here needs a lawyer yet — §2 is the part that does.

---

## Why this decision matters

| | **Episodes as Steam DLC** | **Episode tokens via Steam MicroTxn** |
|---|---|---|
| What the player buys | An episode, directly | A token, then spends it on an episode |
| Payment | Steam, as merchant of record | Steam Wallet via ISteamMicroTxn |
| Backend needed | **None** | Yes — InitTxn/FinalizeTxn + order verification |
| Steamworks SDK | Ownership check only | Full microtransaction integration |
| Refunds | Valve handles entirely | Valve handles payment; **you** handle the spent-token case |
| Regional pricing | Automatic | You set it per SKU |
| Sales tax / VAT | **Valve's problem** | Valve's problem |
| Gifting / wishlists | Free | Not supported |
| Work remaining | Small | Substantial |
| Legal surface | Minimal | Terms of sale for a virtual currency |

**The honest summary:** DLC is less code, less risk, and less legal surface. Tokens only earn their
place if you need one currency spanning multiple storefronts, or want to grant tokens as rewards or
bundle them with the base game. The token layer already built works either way — it is the
entitlement store, and a DLC ownership check can grant straight into it.

---

## 1. The decision

| # | Question | Answer |
|---|---|---|
| 1.1 | **DLC or tokens?** If tokens, what do they buy you that per-episode DLC does not? | |
| 1.2 | Will Cosmic Shore ever sell episodes **outside Steam** (itch, Epic, direct, mobile)? If never, tokens lose their main advantage. | |
| 1.3 | You want to grant free tokens with the base game. If we go DLC, is the equivalent "the first N episodes are included"? | |
| 1.4 | Should episodes ever be **earnable** in game (quests, milestones)? Earnable content is far easier with tokens than with DLC. | |
| 1.5 | If DLC: sold **individually**, as a **season pass**, or both? | |
| 1.6 | If DLC: does a player who owns no episodes still see them in the menu (as store entries), or are they hidden? | |

## 2. For counsel

| # | Question | Answer |
|---|---|---|
| 2.1 | Does selling a **virtual currency** (tokens) rather than the goods themselves change our obligations under US or EU consumer law — disclosure, expiry, refunds, unused balances? | |
| 2.2 | Some jurisdictions treat unspent prepaid balances as **stored value** with escheatment/reporting duties. Does a $2 token with no expiry trip any of that? | |
| 2.3 | With Valve as merchant of record, do we still need our **own** terms of sale, or does the Steam Subscriber Agreement cover the transaction? | |
| 2.4 | Do we need a **custom EULA**, or is Steam's default Subscriber Agreement sufficient? (Baseline plan is the default.) | |
| 2.5 | Our refund position: **spent tokens are non-refundable, unspent tokens follow Steam's window.** Is that enforceable and adequately disclosed? | |
| 2.6 | Any obligation triggered specifically by selling to **minors**, given the game is not age-gated at purchase? | |
| 2.7 | Anything Delaware-C-corp or Michigan specific we should know about selling digital goods? | |

## 3. Operational

| # | Question | Answer |
|---|---|---|
| 3.1 | Who handles a support ticket saying "I paid and got nothing"? What is the manual remedy? | |
| 3.2 | How long do we retain purchase records? (Privacy policy needs a number.) | |
| 3.3 | If an episode is later removed or reworked, what happens to players who bought it? | |
| 3.4 | Free tokens with the base game: **how many**, and granted once per account or once per purchase? | |

---

## What happens after you answer

- **DLC** → drop the MicroTxn provider entirely. Implement a Steam ownership check that grants
  episode entitlements directly. The token wallet stays as the entitlement store; the purchase
  provider becomes a DLC-ownership reader. Smallest remaining work by a wide margin.
- **Tokens** → implement `IEpisodeTokenPurchaseProvider` against ISteamMicroTxn, plus a backend
  service for InitTxn/FinalizeTxn and order verification. Budget real backend time.

Either way, nothing ships taking money until §2 is answered and the documents in
`Docs/Legal/` are reviewed and hosted.
