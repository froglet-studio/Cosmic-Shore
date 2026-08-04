# Economy Tables — Notion copy-paste

Locked values from the pricing pass. Paste any table straight into Notion.

Status: **applied in code and assets** except where marked *pending*.
Source of truth: `SO_Vessel` assets, `Scoreboard.placementCrystalRewards`, `SO_EpisodeTokenConfig`.

---

## Table 1 — Currencies

| Currency | Type | Consumable? | Persists | Source | Sink |
|---|---|---|---|---|---|
| Crystals | Soft currency | Consumable | Cloud Save, across devices | Match placement payouts | Vessel unlocks |
| Episode tokens | Hard currency (real money) | Consumable | Cloud Save, across devices | Purchase; grant on game purchase | Episode unlocks |
| Episode ownership | Entitlement | **Non-consumable** | Cloud Save, permanent | Spending 1 token | — |
| Vessel ownership | Entitlement | **Non-consumable** | Cloud Save, permanent | Spending crystals | — |
| Elemental crystals | In-run resource | Consumable | Not persisted | Pickups during a run | Buffs during that run |

---

## Table 2 — Crystal payouts

Same table in every mode, tournament included. Last place always earns 0.

| Place | Crystals |
|---|---|
| 1st | **200** |
| 2nd | **50** |
| Last | **0** |
| Did not place | 0 |

| Rule | Value |
|---|---|
| Losing payout | None |
| Intensity scaling | None |
| Daily / streak bonuses | None |
| Quest completion payout | Handled on a separate branch |

---

## Table 3 — Vessel prices

Flat pricing. All purchasable vessels cost the same.

| Vessel | Type | Consumable? | Price | State |
|---|---|---|---|---|
| Squirrel | Vessel | Non-consumable | **Free** | Granted at first login. The only free vessel. |
| Manta | Vessel | Non-consumable | **4,000 crystals** | Locked |
| Dolphin | Vessel | Non-consumable | **4,000 crystals** | Locked |
| Rhino | Vessel | Non-consumable | **4,000 crystals** | Locked |
| Serpent | Vessel | Non-consumable | **4,000 crystals** | Locked |
| Sparrow | Vessel | Non-consumable | **4,000 crystals** | Locked |
| Grizzly | Vessel | Non-consumable | **4,000 crystals** | Locked |
| Urchin | Vessel | Non-consumable | **4,000 crystals** | Locked |
| Termite | Vessel | Non-consumable | **4,000 crystals** | Locked |

**Time to earn one vessel:** 4,000 ÷ 200 = **20 wins.**
Vessels cannot be bought at all until the Hangar unlocks via the quest chain (3 games), so crystals
earned before that point bank toward the first purchase rather than being wasted.

> **Open item:** Termite is locked and priced, but is not a playable vessel. Locking keeps it out of
> reach; whether it *appears* in the Hangar at all is controlled by the vessel list assets
> (`SO_Classlist_*`), not by the lock flag. Say the word and I'll remove it from the shipping list.

---

## Table 4 — Real-money items

| Item | Type | Consumable? | Price (USD) | Notes |
|---|---|---|---|---|
| Cosmic Shore (Early Access) | Base game | Non-consumable | **$15.00** | Price does **not** rise at 1.0 |
| Episode ×1 | Entitlement | Non-consumable | **$5.00** | Player picks which |
| All 12 Episodes | Entitlement / token pack | Non-consumable | **$30.00** | Half off ($2.50 each) |
| Lifetime pass | Entitlement | Non-consumable | **$90.00** | All episodes: past, present, future |
| **Lifetime spend cap** | — | — | **$120.00** | Ceiling. At $120 the pass is granted; we take no more |
| Episode (via token) | Entitlement | Non-consumable | 1 token | Permanent, all devices |

| Rule | Value |
|---|---|
| Bundles / volume discounts | 12-for-$30 (half off). Mid-tier 6-for-$20 proposed — see analysis |
| Crystals → tokens | **Never.** No soft-to-hard conversion. |
| Regional pricing | Accept Valve's suggested conversions |
| Launch discount | *Pending — not yet decided* |
| Episodes at launch | **6** |
| Free tokens granted on game purchase | *Pending — how many?* |
| Delivery mechanism | *Pending — see `Docs/Legal/DLC_VS_TOKENS_ANALYSIS.md`* |
| Mid tier (6 for $20) | *Proposed — decides DLC vs tokens* |

### Threshold credit — how the $120 cap is delivered

`pass price = $90 − max(0, spend − $30)`

| Spend so far | Credited | Pass price | **Total** |
|---|---|---|---|
| $0 | $0 | $90 | $90 |
| $25 (5 episodes) | $0 | $90 | $115 |
| $30 (12-pack) | $0 | $90 | **$120** |
| $40 | $10 | $80 | **$120** |
| $60 (12 singles) | $30 | $60 | **$120** |
| $120 | $90 | **free** | **$120** |

Above $30 of spend the total is **exactly $120 on every path**, and at $120 the pass is granted.

> ⚠️ **Steam cannot price a SKU per player.** The credit must be computed from **which SKUs the
> player owns** at our list prices, never from dollars paid — sales, regional pricing and gifts break
> any dollar inference. Delivering the sliding price with no backend means **stepped upgrade SKUs**;
> a continuous price means the Microtransaction API and a backend. See the analysis §4 and §4b.

---

## Table 5 — Progression gates

| Gate | Unlocked by | Notes |
|---|---|---|
| First game mode | Free at first boot | Tournament is always unlocked |
| Game modes 2+ | Quest chain | Claim the previous quest |
| Vessel Hangar | Quest chain (~3 games) | No vessel can be bought before this |
| Intensity 3 and 4 | Playing the mode | Tournament ships at full intensity |
| Vessels | 4,000 crystals each | After the Hangar unlocks |
| Episodes | 1 episode token each | Requires a storefront — not wired |

---

## Two values still needed

1. **Free tokens granted on game purchase** — how many? This is the "some free episode tokens when
   they buy the game" line. It needs a number, and a grant path (a Steam-ownership check that credits
   once, idempotently).
2. **Launch discount %** — checklist A6.
