# Cosmic Shore — Terms of Sale for Virtual Items (DRAFT TEMPLATE)

> **DRAFT. NOT LEGAL ADVICE. DO NOT PUBLISH AS-IS.**
> Written for counsel to review and correct. Fill every `[BRACKETED]` placeholder first.
>
> **This document is only needed if we sell episode tokens directly.** If episodes ship as Steam
> DLC, Valve is merchant of record and the Steam Subscriber Agreement governs the transaction —
> confirm with counsel (question 2.3 in `DLC_VS_TOKENS_QUESTIONNAIRE.md`) before spending time here.

**Last updated:** `[DATE]`
**Applies to:** Cosmic Shore, published by Froglet Inc.

---

## 1. Who we are

Cosmic Shore is published by **Froglet Inc.**, a Delaware corporation with its principal place of
business at `[COMPANY ADDRESS, GRAND RAPIDS, MI]` ("Froglet", "we", "us").

Questions about a purchase: `[SUPPORT EMAIL]`.

## 2. What these terms cover

These terms apply when you buy **Episode Tokens** or unlock **Episodes** inside Cosmic Shore. They
are in addition to the platform's own terms — where you bought the game through Steam, the
[Steam Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/) also applies, and
where the two conflict on payment, refunds, or billing, **the platform's terms control**.

## 3. Episode Tokens

**What a token is.** An Episode Token is a limited, personal, non-transferable, revocable licence to
unlock one Episode within Cosmic Shore. It is **not** money, not a stored-value instrument, not a
gift card, and not property.

**What a token costs.** `[PRICE]` per token, plus any tax the platform collects. The price shown at
checkout is the price that applies.

**What a token buys.** One token unlocks one Episode. Once spent, that Episode is unlocked on your
account permanently and is available on any device where you sign in to the same account.

**Tokens do not expire.** An unspent token remains on your account for as long as the account exists
and Cosmic Shore continues to operate.

**Tokens have no cash value.** They cannot be sold, traded, gifted, transferred between accounts, or
exchanged for money or anything else of value, by you or by us.

**Tokens cannot be earned with in-game currency.** Crystals and other in-game currencies can never
be converted into Episode Tokens.

## 4. Episodes

An unlocked Episode is a personal, non-transferable licence to access that content within Cosmic
Shore. You do not own the content and no intellectual property transfers to you.

We may update, change, or retire Episodes over time. If we retire an Episode you have unlocked, we
will `[COMMITMENT — e.g. "give at least 30 days' notice and offer a replacement Episode or a
token"]`.

## 5. Refunds

**The base game and token purchases** are handled by the platform you bought them on. On Steam,
Valve's refund policy applies — generally within 14 days of purchase and under 2 hours of playtime.
Request refunds through the platform, not through us.

**Unspent tokens** are covered by the platform refund policy above.

**Spent tokens are not refundable.** Once a token unlocks an Episode, the transaction is complete
and the token is consumed. `[CONFIRM ENFORCEABLE — some jurisdictions require a cooling-off period
for digital goods even after access begins, and the EU requires an explicit waiver of that right
before delivery.]`

**If something goes wrong** — you paid and no tokens appeared, or a token was consumed but no
Episode unlocked — contact `[SUPPORT EMAIL]` with your platform order id and we will correct it.

## 6. Account requirement

Purchases attach to your Cosmic Shore account. If your account is deleted, closed, or terminated for
breach, any unspent tokens and unlocked Episodes are lost and are not refunded or restored.

## 7. Age

You must be at least `[MINIMUM AGE]` to make a purchase, or have permission from a parent or
guardian. `[CONFIRM: the game is not age-gated at purchase; counsel to advise whether that is
sufficient.]`

## 8. Availability

We may change prices, add or remove items, or stop selling tokens at any time. Changes do not affect
tokens or Episodes you have already bought.

## 9. Changes to these terms

We may update these terms. Material changes will be posted here with a new "Last updated" date and
`[NOTICE COMMITMENT — e.g. "announced in-game and on our Steam page at least 14 days beforehand"]`.
Changes never apply retroactively to a completed purchase.

## 10. Governing law

These terms are governed by the laws of `[STATE — likely Delaware or Michigan]`, without regard to
conflict-of-laws rules. `[COUNSEL: dispute resolution, arbitration, class-action waiver, and the EU
consumer carve-out all go here.]`

## 11. Contact

Froglet Inc.
`[COMPANY ADDRESS]`
`[SUPPORT EMAIL]`

---

## Implementation cross-reference (remove before publishing)

Behaviour these terms describe, and where it lives in the code:

| Clause | Enforced by |
|---|---|
| Tokens only created by a verified order | `EpisodeTokenService.GrantTokens` requires an `OrderReceipt` from a purchase provider |
| No double-grant on a replayed receipt | `ProfileEconomy.RedeemedOrderIds` idempotency check |
| Episode ownership is permanent and roams | `ProfileEconomy.OwnedEpisodeIds`, persisted to UGS Cloud Save |
| Tokens never expire | Nothing decrements the balance except an unlock |
| Crystals cannot become tokens | No conversion path exists, by design |
| Spent tokens are consumed | `EpisodeTokenService.TryUnlockEpisode` decrements before writing ownership |

**Open questions for counsel** are consolidated in `DLC_VS_TOKENS_QUESTIONNAIRE.md` §2 — chiefly
whether a no-expiry prepaid balance triggers stored-value obligations, and whether this document is
needed at all when Valve is merchant of record.
