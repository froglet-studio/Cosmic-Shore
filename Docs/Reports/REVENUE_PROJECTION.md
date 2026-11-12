# Cosmic Shore revenue projection, 2027–2031

**The same model as an interactive page:** [Cosmic Shore Revenue Projection](https://claude.ai/artifact/YCjzMf8LiyvskeJgj8hQgZ)
(private; the owner shares it from the page's Share menu). Source: `Docs/Reports/revenue_projection.html`, whose
`project()` function is the model. It builds on [`MARKET_SIZING.md`](MARKET_SIZING.md) and
[`AUDIENCE_ROADMAP.md`](AUDIENCE_ROADMAP.md).

**From:** a Claude session for Garrett. First written 2026-11-07 for a paid game; rebuilt 2026-11-12 for the business
model Garrett set:

- **One account** on all three platforms (read as Steam, iOS and Android, with the web as a store beside them).
- **Paid Early Access** on Steam ($15), then **free to play** from 1.0.
- **$5** buys a token: one episode's paid track. **$30** buys 12 tokens at half off. **$90** buys the infinite pass.
- **All purchases stop at $120**, where the infinite pass unlocks automatically. After that only **merchandise, sold on
  the web**, takes more money.

Gross is what players spend. Net is what reaches Froglet after store fees (Steam and console 30%, app stores 15%, web 5%)
and, for merchandise, after the cost of making it; before tax, refunds and running costs.

## 1. The answer

| Year | Low gross | Low net | Base gross | Base net | High gross | High net |
|---|---:|---:|---:|---:|---:|---:|
| 2027 | $0 | $0 | $65k | $46k | $740k | $518k |
| 2028 | $22k | $15k | $65k | $47k | $1.5M | $1.1M |
| 2029 | $5k | $4k | $107k | $83k | $2.7M | $2.1M |
| 2030 | $7k | $5k | $121k | $96k | $3.9M | $3.0M |
| 2031 | $7k | $5k | $141k | $112k | $4.2M | $3.3M |
| **Five years** | **$41k** | **$30k** | **$499k** | **$385k** | **$13.0M** | **$10.2M** |
| The paid plan (2026-11-07) | $46k | $33k | $437k | $325k | $11.8M | $8.9M |

Base, by stream:

| Stream | 2027 | 2028 | 2029 | 2030 | 2031 |
|---|---:|---:|---:|---:|---:|
| Early Access copies | $65k | $27k | $0 | $0 | $0 |
| Tokens and passes, in the stores | $0 | $29k | $84k | $95k | $111k |
| Tokens and passes, on the web | $0 | $7k | $21k | $24k | $28k |
| Merchandise, on the web | $0 | $1k | $2k | $2k | $3k |
| Early Access copies sold | 5,949 | 2,451 | 0 | 0 | 0 |
| New accounts: Steam and console | 0 | 42,491 | 27,409 | 39,545 | 28,705 |
| New accounts: phones | 0 | 42,379 | 85,314 | 86,457 | 87,118 |
| Monthly players (average) | 0 | 11,389 | 20,704 | 23,257 | 24,111 |

## 2. What it means

- **Free to play pays back more slowly and keeps growing.** Over five years the base case passes the paid plan ($499k
  against $437k gross), and 2031 is its biggest year, still rising. The paid plan peaked in 2030 and fell after.
- **Spend per paying player is the biggest lever, then the free launch.** Doubling what a payer buys takes five years
  from $499k to $789k; doubling the Steam free-to-play launch takes it to $704k. Early Access wishlists matter less than in
  the paid plan, because paid copies stop at 1.0.
- **The $120 ceiling costs almost nothing in five years.** Lifetime spend per paying player is $33 in the base case.
  Only pass buyers, and players who bundle and then take the pass, reach it. What the ceiling gives up is the long tail
  of heavy spenders most free-to-play games depend on.
- **The prices fit together.** A $30 bundle plus the $90 pass is exactly $120. Tokens one at a time reach the ceiling
  after 24 episodes; bundles after four bundles (48 episodes). Against bundles, the pass pays for itself after 36 episodes.
- **The web store is worth about $38k net** over five years in the base case (none on the web against half), within
  what Apple and Valve allow.
- **Merchandise is small until the audience is large:** under $3k a year in the base case, about $125k in the high case.
- **The base case still does not fund a team:** about $83–112k a year net from 2029.

## 3. Decisions the model had to guess

1. **The pass price after spending.** The ceiling holds only if the pass costs $120 minus what was spent, whenever that
   is less than $90. Assumed.
2. **Whether the $15 Early Access price counts toward the $120.** Assumed yes.
3. **"The three platforms."** Read as Steam, iOS and Android; a console port stays a later stream on the same account.
4. **Episodes a year.** Two exist today (`Assets/_SO_Assets/XP/EpisodeList.asset`). The page's calculator compares the
   offers at any cadence; the scenarios set spend per payer directly.
5. **Steam and the web store.** If Valve requires Steam's own payment for purchases in the Steam version, Steam players'
   web share is zero. Apple's US rules on links to web purchases are still being settled after the Ninth Circuit's
   December 2025 decision. The repo already has an episode web checkout (`Docs/MENU_PROGRESSION_AND_IAP.md` §5),
   switched off with the rest of commerce (R4).

## 4. Assumptions

| Input | Low | Base | High |
|---|---:|---:|---:|
| Early Access launch (paid, $15) | Jan 2028 | Jul 2027 | Apr 2027 |
| Free to play from (1.0) | Apr 2029 | Jul 2028 | Apr 2028 |
| Early Access wishlists | 7,000 | 30,000 | 150,000 |
| Steam accounts, first free year | 10,000 | 60,000 | 600,000 |
| Phones go public | Jul 2029 | Jul 2028 | Apr 2028 |
| Phone installs a year, without invites | 20,000 | 75,000 | 300,000 |
| Phone invites per active player a year | 0.1 | 0.5 | 1.0 |
| Console port | none | Jul 2030, 50% | Jul 2029, 100% |
| Monthly active after month one: PC, phone | 12%, 4% | 20%, 6% | 30%, 10% |
| Players who ever pay: Steam, phone, EA owners | 3%, 1%, 10% | 6%, 2.5%, 20% | 10%, 4%, 35% |
| Payers: tokens · bundle · pass | 60 · 30 · 10% | 55 · 35 · 10% | 45 · 40 · 15% |
| Lifetime buys per payer of that kind | 2 tokens, 1 bundle | 3 tokens, 1.5 bundles | 4 tokens, 2 bundles |
| Lifetime spend per payer | $24 | $33 | $47 |
| Bought on the web | 5% | 20% | 30% |
| Merchandise (per 1,000 players a year × order, margin) | 1 × $30, 25% | 3 × $35, 30% | 6 × $40, 35% |

Token spend is spread over a player's first four years (50%, 25%, 15%, 10%); spend after 2031 is not counted. App stores
are charged at the 15% small-business rate; the high case passes $1M a year on mobile from 2029, which would lower its
net.

## 5. Sources

- Early Access sales: [GameDiscoverCo long tail](https://newsletter.gamediscover.co/p/revealed-the-state-of-steam-wishlist), [wishlist conversions](https://newsletter.gamediscover.co/p/the-state-of-steam-wishlist-conversions)
- Free to play on Steam: [King of Crabs deep dive (GameDiscoverCo)](https://newsletter.gamediscover.co/p/deep-dive-inside-king-of-crabs-steam)
- Paying players: [Playio (GameAnalytics)](https://blog.playio.co/arppu-mobile-games), [80.lv (Xsolla)](https://80.lv/articles/where-do-players-pay-and-play-more), [GamesBeat (Newzoo)](https://gamesbeat.com/free-to-play-gaming-revenue-is-better-on-pc-than-on-console-newzoo/), [MWM](https://mwm.ai/glossary/battle-pass), [ShaneTheGamer](https://www.shanethegamer.com/research/battle-pass-statistics/)
- Mobile retention: [Playio (GameAnalytics)](https://blog.playio.co/arpdau-benchmarks-mobile-games)
- Apple link-outs: [Neon](https://www.neonpay.com/blog/the-latest-epic-v.-apple-appeal-ruling-december-2025-what-changed-for-game-developers), [9to5Mac](https://9to5mac.com/2025/12/11/apple-defeats-ban-on-charging-commission-on-linked-out-purchases-from-ios-apps/); Steam: [Steamworks microtransactions](https://partner.steamgames.com/doc/features/microtransactions)
