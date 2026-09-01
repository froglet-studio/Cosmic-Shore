# Economy, Currencies & Pricing

Everything the game charges or pays out, the audit of whether those numbers work, and the
questionnaire to take to whoever sets prices.

Owner: Shombith · Related: `Docs/MENU_PROGRESSION_AND_IAP.md`, `Docs/STEAM_EA_INVESTOR_CHECKPOINT.pdf`

---

## 1. Currency inventory

Four things behave like currency. Only two of them are real economies today.

| # | Currency | Type | Earned by | Spent on | State |
|---|---|---|---|---|---|
| 1 | **Crystals** | Soft, cloud-persisted (`ProfileEconomy.CrystalBalance`) | Placement payout at game end | Vessel unlocks only | Live. Retuned — see §2 |
| 2 | **Elemental crystals** (Charge / Mass / Space / Time) | In-run gameplay resource | Collected while flying | Buffs and abilities during a run | Not a wallet. Never persists between runs. |
| 3 | **Episode tokens** | Hard, real-money entitlement | Purchase | 1 token = 1 episode | **Built this pass — see §4.** No storefront wired. |
| 4 | **Real money (USD)** | — | — | Episode tokens; the app itself on Steam | Web-checkout path exists but is inert and **not Steam-compliant** (§5) |

### Priceable items — the list for whoever sets prices

**Vessels** (`SO_Vessel.UnlockCost`, priced in crystals):

| Vessel | Locked | Cost | Note |
|---|---|---|---|
| Manta | Yes | 4000 | |
| Dolphin | Yes | 4000 | |
| Rhino | Yes | 4000 | |
| Serpent | Yes | 4000 | |
| Sparrow | Yes | 4000 | |
| Squirrel | **No** | 100 | Starter vessel — unlocked, so the cost never applies |
| Grizzly | **No** | *(unset → 100)* | ⚠️ Free by accident, see §2 |
| Urchin | **No** | *(unset → 100)* | ⚠️ Free by accident, see §2 |
| Termite | **No** | *(unset → 100)* | ⚠️ Not playable, but unlocked |
| Falcon / Shrike | — | — | `_TEMP` assets, not in the shipping list |

**Episodes** (`SO_EpisodeData`): one asset exists — `cosmic_shore_1`, `amount: "Support Us"`,
`priceUsd: 0`. Nothing is purchasable today.

**Crystal payouts** — now `RewardTableSO` (`Resources/RewardTable.asset`), one asset for every
mode. The numbers are `Docs/ECONOMY_TABLES.md` Table 2; the plumbing is `Docs/REWARD_SYSTEM.md`.

| Place | Payout |
|---|---|
| 1st | **200** |
| 2nd | **50** |
| Last | **0**, always — with two domains that makes the runner-up a loser, not a medallist |

> The table above previously described `Scoreboard.winnerCrystalReward` at **5, winner only**.
> That field was retired when the payout became placement-based, and this line was left behind:
> the code default had been `{200, 50, 0}` for some time. Five surfaces still carried the dead
> key and were silently paying out of the C# initializer; all 14 now read the asset.

---

## 2. Economy audit — findings, all now resolved

> The three findings below were the state **before** the pricing pass. All three are fixed; the
> current numbers live in `Docs/ECONOMY_TABLES.md`, which is the source of truth for pricing.

Three findings, in severity order.

### 🔴 ~~A vessel costs 800 wins~~ — FIXED (payout raised 5 → 200; a vessel is now 20 wins)

This is the headline problem and it makes vessel unlocks effectively unreachable.

| Path | Payout | Games for one 4000-crystal vessel |
|---|---|---|
| Win a normal game | 5 | **800 wins** |
| Win a Tournament game (1st of 3 domains) | 2 | **2000 wins** |
| Lose anything | 0 | never |

At a realistic 50% win rate that is **~1,600 games played** for a single vessel, and there are five
to buy.

Either the payout is roughly 100× too small or the price is roughly 100× too large. Someone has to
choose which; §3 is the questionnaire for that conversation.

### 🟠 ~~Three vessels are free by accident~~ — FIXED (Grizzly, Urchin, Termite locked at 4000)

`SO_Class_Grizzly`, `SO_Class_Urchin`, and `SO_Class_Termite` serialize **neither** `isLocked` nor
`UnlockCost`, so they fall back to the C# defaults: `isLocked = false`, `UnlockCost = 100`. They are
unlocked and free. Termite is not even a playable vessel per the class table.

This is almost certainly unintentional — the five real vessels all carry explicit `isLocked: 1` and
`UnlockCost: 4000`. Decide per vessel and set the fields explicitly rather than relying on defaults.

### 🟡 One earn path, and no floor — partly addressed (losing still pays nothing, by decision)

`Scoreboard.AwardCrystalsToLocalPlayer` is the **only** place crystals are ever created outside
debug tools. Consequences:

- **Losing pays nothing.** A new player who loses their first several games earns zero, sees a
  4000-crystal price tag, and has no visible path to it.
- **`Quest.CrystalRewards` exists and is never granted** — the field is `List<(Element, int)>` on
  quests, but nothing reads it. A designed reward path was built and left unwired.
- **No daily, first-win, or milestone payouts.**

A participation payout — even 1 crystal for finishing — changes the shape of this materially.

### ✅ What is sound

- Crystal spend is atomic and guarded: `TrySpendCrystals` checks the balance before deducting and
  reports blocked spends to analytics.
- Lifetime earned/spent totals are tracked, so the funnel is measurable without an event roll-up.
- Vessel unlocks persist to Cloud Save and roam across devices.
- The Hangar itself is correct: gated by quest chain for access, by crystals per vessel, and
  re-sorts unlocked-first on purchase.

---

## 3. Pricing questionnaire — ANSWERED

> Answered in full; the resulting values are in `Docs/ECONOMY_TABLES.md`. Kept here as the record
> of what was asked and why.

### A. Vessels

1. **How long should earning one vessel take?** Give it in games or in sessions, not crystals —
   e.g. "about 10 sessions for a regular player." We convert to numbers.
2. **Should losing pay out?** If yes, what fraction of a win? (Common choice: a loss pays 25–50% of
   a win, so a bad night still progresses.)
3. **Should all five vessels cost the same?** Flat pricing is simplest; tiered pricing implies some
   vessels are better, which changes balance conversations.
4. **Grizzly, Urchin, Termite — locked or free?** They are currently free by accident. Termite is
   not playable at all; should it even appear?
5. **What does a brand-new player have?** Squirrel only, or a small starting crystal balance so the
   first purchase is reachable early?

### B. Payout structure

6. **Flat or placement-based?** Non-tournament modes pay winner-only; Tournament pays {2,1,0}.
   Should these unify?
7. **Any non-match payouts?** Daily first win, quest completion (the field exists, unwired),
   milestone rewards. Which, if any, do we want?
8. **Should payout scale with intensity?** Intensity 4 is materially harder than 1 and pays the same.

### C. Episode tokens

9. **Confirm: $2.00 per token, 1 token = 1 episode.** Is that the launch price?
10. **Bundle discounts?** e.g. 3 tokens for $5. Bundles raise average order value but complicate
    refunds.
11. **Can crystals ever buy a token?** A soft→hard conversion path is a major design decision — it
    makes crystals meaningful but caps revenue.
12. **What does a token buy exactly?** Permanent ownership of one episode, forever, across devices?
    (That is what the code does today.)
13. **How many episodes will exist at launch?** One asset exists. Selling tokens for content that
    does not exist yet is a refund problem.

### D. Real-money pricing

14. **App price on Steam and any launch discount?** (Checklist A6.)
15. **Does the Early Access price rise at 1.0?** The questionnaire answer we drafted says it may —
    it should match the actual plan.
16. **Regional pricing** — accept Valve's suggested conversions, or set manually?

### E. Policy

17. **Is any of this shipping at Early Access launch?** Decision 2 of the launch plan says no IAP
    at launch. See §5 — this needs an explicit call.
18. **Refund policy for tokens**, beyond Steam's standard two-hour/two-week window?

---

## 4. Episode tokens — what was built

Money buys tokens. Tokens buy episodes. Only a **verified order** can create a token.

| File | Role |
|---|---|
| `ScriptableObjects/SO_EpisodeTokenConfig.cs` | Bundles, SKUs, display prices, tokens-per-episode. Prices here are display-only. |
| `System/Monetization/EpisodeTokenService.cs` | The wallet. Single writer for balance and ownership. |
| `System/Monetization/IEpisodeTokenPurchaseProvider.cs` | Storefront abstraction + an editor-only fake. |
| `System/Monetization/EpisodeTokenController.cs` | The MonoBehaviour the UI binds to. |
| `UI/Views/PlayerProfileData.cs` | `ProfileEconomy` gained token balance, owned episodes, redeemed order ids. |
| `UI/Views/PlayerDataService.cs` | `PersistProfileNow()` — entitlement writes flush immediately. |

### The two safety properties

1. **Grants require a verified receipt.** `GrantTokens` takes an `OrderReceipt` that only a purchase
   provider can produce, after the storefront confirms payment. There is no "add tokens" method a
   button can reach — deliberately.
2. **Grants are idempotent.** Every order id is recorded in `RedeemedOrderIds`. Replaying a receipt
   (retry, restart, player reopening a confirmation) grants nothing the second time.

Entitlement writes call `PersistProfileNow()` rather than riding the ~1.5s debounce, because a
dropped save here is a player who paid and got nothing.

### UI surface

```csharp
controller.TokenBalance          // int
controller.Bundles               // what to show in the store
controller.CanPurchase           // false when no storefront -> disable buy buttons
controller.CanUnlock             // enough tokens for one episode
controller.OwnsEpisode(episode)
controller.BuyBundle(productId)  // Button onClick
controller.UnlockEpisode(episode)

controller.OnBalanceChanged   += balance => ...
controller.OnEpisodeUnlocked  += episodeId => ...
controller.OnPurchaseFinished += (ok, message) => ...   // message is player-facing
```

### What is deliberately NOT done

- **No storefront.** `ResolveProvider()` returns null outside the editor, so `CanPurchase` is false
  and buy buttons stay disabled. Nothing can take money yet. That is the correct state until §5 is
  resolved.
- **No UI** — yours to build.
- **The editor fake is fenced three ways**: `UNITY_EDITOR || DEVELOPMENT_BUILD`, an explicit
  `allowUnverifiedGrantsInEditor` flag, and a runtime refusal in release players.

---

## 5. ⚠️ Legal and platform position — read before wiring a storefront

Three things, in order of how much they can hurt.

### 5a. On Steam, in-game purchases must use the Steam Wallet

Steamworks documentation is explicit: for in-game purchases you use the **microtransaction API**, so
that Steam customers purchase from their Steam Wallet. Valve's revenue share applies to in-game
purchases as well as the base app.

**The consequence for the planned design:** selling episode tokens through an external web checkout
(`Application.OpenURL` → Stripe/Froglet page) inside a Steam build is **not a compliant path**. It
routes around the Steam Wallet and around Valve's revenue share. The existing `IAPManager`
web-checkout flow is fine for a non-Steam distribution, and not fine for this one.

Compliant options on Steam:

| Option | Fit | Cost |
|---|---|---|
| **Episodes as DLC** | Simplest by far. Each episode is a DLC item; Steam handles payment, ownership, refunds, regional pricing. **Tokens become unnecessary.** | No backend, no SDK work beyond ownership checks |
| **Steam MicroTxn API** | Keeps the token abstraction | Steamworks SDK + a backend calling InitTxn/FinalizeTxn |
| External web checkout | **Not compliant on Steam** | — |

> **Worth asking directly:** if episodes are sold on Steam, do tokens earn their place at all? DLC
> gives you ownership, refunds, regional pricing, and gifting for free. Tokens make sense when you
> need one currency across several storefronts, or plan to grant them as rewards. If neither is
> true, DLC is less code and less risk. The token layer built this pass survives either way — it is
> the entitlement store, and a Steam DLC check can grant into it.

### 5b. This contradicts a locked launch decision

Decision 2 of the launch plan cuts **all IAP** from Early Access:

> *"Episode tokens and the web-checkout flow are cut from launch — the current web-checkout has a
> known unresolved entitlement-verification gap and must not ship taking money."*

I have built the feature as asked, and built it so it **cannot** take money until a provider is
wired. But shipping it live at Early Access reverses that decision, and the investor checkpoint
currently tells investors monetisation is deferred. That needs to be a deliberate call, not a
side effect. If the intent is post-launch, everything here can sit dormant — `CanPurchase` is
already false.

### 5c. Legal documentation required before taking money

Not legal advice — this is the list to take to counsel.

| Item | Why | Who |
|---|---|---|
| **Terms of Sale / EULA covering virtual items** | What a token is, that it has no cash value, is non-transferable, and expires never. Steam's Subscriber Agreement covers the transaction; it does not describe *your* virtual item. | Counsel + Garrett |
| **Refund policy** | Steam's standard policy applies to the app. State explicitly what happens to a spent token. | Garrett |
| **Privacy policy update** | Purchase records are personal data. The hosted policy (checklist F1) must cover payment/entitlement data. | Shombith |
| **Tax** | Sales tax/VAT on digital goods. Selling via Steam makes Valve merchant of record, which removes most of this. Selling direct does not. **This alone is a strong argument for DLC.** | Counsel |
| **Consumer protection / auto-renewal** | Not applicable — tokens are one-time. Keep it that way unless someone proposes a subscription. | Counsel |
| **COPPA / under-13** | Analytics is already age-gated. If under-13 players can reach a purchase button, that is a separate and much stricter regime. | Counsel + Shombith |

---

## 6. What was not asked about but should be

1. **The 4000-crystal price predates the payout being 5.** Whoever set one did not set the other.
   Fix as a pair, not separately.
2. **No crystal sink besides vessels.** Once all five are bought, crystals are inert. Cosmetics,
   intensity unlocks, or token conversion would give the currency a second life.
3. **Debug crystals ship in the build.** `LogControlWindow` can mint crystals. Confirm that tool is
   editor-only or stripped from release players before launch.
4. **No purchase analytics events.** Crystal earn/spend are instrumented; token purchase, grant
   failure, and spend are not. Add them before the first real transaction or the funnel is blind.
5. **Episode content does not exist yet.** One episode asset, `isAvailable: 1`, no real content
   behind it. Selling tokens against unbuilt content is the fastest route to refunds and bad reviews.

---

## Sources

[Steamworks Microtransactions](https://partner.steamgames.com/doc/features/microtransactions) ·
[ISteamMicroTxn](https://partner.steamgames.com/doc/webapi/isteammicrotxn) ·
[Steam Direct Fee](https://partner.steamgames.com/doc/gettingstarted/appfee)

Platform rules change and none of this is legal advice. Confirm 5a with Valve partner support and
5c with counsel before any storefront goes live.
