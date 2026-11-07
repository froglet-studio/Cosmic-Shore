# Cosmic Shore revenue projection, 2027–2031

**The same model as an interactive page:** [Cosmic Shore Revenue Projection](https://claude.ai/artifact/YCjzMf8LiyvskeJgj8hQgZ)
(private; the owner shares it from the page's Share menu). Source: `Docs/Reports/revenue_projection.html`, whose
`project()` function is the model. It builds on [`MARKET_SIZING.md`](MARKET_SIZING.md) (the Steam funnel) and
[`AUDIENCE_ROADMAP.md`](AUDIENCE_ROADMAP.md) (the order and timing of each stream).

**From:** a Claude session for Garrett, 2026-11-07. Gross is what players spend; net is what reaches Froglet after the
store's cut (Steam and console 30%, app stores 15%), before tax, refunds and costs.

## 1. The answer

| Year | Low gross | Low net | Base gross | Base net | High gross | High net |
|---|---:|---:|---:|---:|---:|---:|
| 2027 | $0 | $0 | $65k | $46k | $740k | $518k |
| 2028 | $22k | $15k | $87k | $63k | $2.0M | $1.5M |
| 2029 | $11k | $8k | $84k | $64k | $3.0M | $2.3M |
| 2030 | $8k | $6k | $109k | $82k | $3.0M | $2.3M |
| 2031 | $6k | $5k | $92k | $70k | $3.1M | $2.4M |
| **Five years** | **$46k** | **$33k** | **$437k** | **$325k** | **$11.8M** | **$8.9M** |

Base, by stream:

| Stream | 2027 | 2028 | 2029 | 2030 | 2031 |
|---|---:|---:|---:|---:|---:|
| Steam: the game | $65k | $68k | $45k | $34k | $27k |
| Mobile, free to play | $0 | $17k | $36k | $37k | $38k |
| Console port | $0 | $0 | $0 | $33k | $21k |
| Add-ons on PC | $0 | $1k | $3k | $4k | $5k |
| Steam copies sold | 5,949 | 6,217 | 4,097 | 3,125 | 2,478 |
| Mobile installs | 0 | 43,090 | 89,544 | 93,039 | 95,807 |

## 2. What it means

- **The Steam wishlist count decides almost everything.** Doubling it takes the base five years from $437k to $769k;
  halving it gives $271k. No other lever moves the result half as much.
- **Friend invites only pay once the PC base is large.** Mobile installs from invites scale with Steam owners, so with
  about 22,000 owners in the base case they add little. In the high case they are the largest source of phone players.
  The roadmap's order matters for money, not only for reach.
- **Paid mobile acquisition does not pay.** A casual puzzle install costs $2.50–6.00 to buy, and the base case earns
  $0.40 an install.
- **The base case does not fund a team:** about $45–80k a year net. The high case (a breakout comparable to Pummel
  Party or Ultimate Chicken Horse, $8–20M lifetime) depends on the launch, not on the later streams.
- **Thresholds:** FMOD's free tier ends at $200k gross a year; the decision on record is to buy the licence if crossed
  (`Docs/THIRD_PARTY_DECISIONS.md`). Only the high case crosses it, in 2027.
- **Below the low case is zero.** The median Steam game earned about $249 in 2025.

## 3. Assumptions

| Input | Low | Base | High | Why |
|---|---:|---:|---:|---|
| Early Access launch | Jan 2028 | Jul 2027 | Apr 2027 | Fee unpaid; floor ~6 weeks after it, realistically 8–12, then Playtest waves |
| 1.0 launch | Apr 2029 | Jul 2028 | Apr 2028 | About 12 months in Early Access |
| Launch wishlists | 7,000 | 30,000 | 150,000 | The Market Map's cases |
| Week-one conversion | 0.10× | 0.10× | 0.15× | GameDiscoverCo median above $10 is 0.10× |
| Year one ÷ week one | 2.8× | 2.8× | 3.5× | Median for $15–19 games |
| Realized gross per copy | $11 | $11 | $11 | $15 list after discounts and regional prices |
| 1.0 wave, share of EA year one | 20% | 40% | 100% | Median 1.0 month ≈ 40% of EA launch month |
| Steam years 2–5, share of year one | 25·15·10·7% | 33·28·22·17% | 50·45·40·35% | Year two ≈ a third of year one |
| Add-ons on PC, from 1.0 | none | 3% × $8/yr | 8% × $12/yr | Needs commerce back on (R4) |
| Public mobile release | Jul 2029 | Jul 2028 | Jan 2028 | Pin 3 |
| Mobile installs a year, without invites | 20,000 | 75,000 | 300,000 | No paid acquisition |
| Phone installs per Steam owner a year | 0.2 | 1.0 | 2.5 | The household bridge |
| Mobile gross per install | $0.15 | $0.40 | $1.20 | ~2.5 active days at median retention × ARPDAU $0.15–0.50 |
| Console port | none | Jul 2030, 50% | Jul 2029, 150% | Year one vs Steam EA year one |

Year one of any launch is front-loaded: month one is 46.5% of year one (1.47× of 3.16× week one). Mobile revenue is
counted in the install month. Not included: tax, VAT, refunds, costs, investment, merchandise, browser Toybox revenue.

## 4. Sources

- Steam long tail: [GameDiscoverCo](https://newsletter.gamediscover.co/p/revealed-the-state-of-steam-wishlist), [summary](https://gamedevreports.substack.com/p/gamediscoverco-games-long-tail-revenue), [month one](https://newsletter.gamediscover.co/p/data-deep-dive-whats-the-long-tail)
- Wishlist conversion: [GameDiscoverCo](https://newsletter.gamediscover.co/p/the-state-of-steam-wishlist-conversions), [benchmarks](https://gamedevreports.substack.com/p/gamediscoverco-conversion-benchmarks)
- 1.0 launches: [GameDiscoverCo](https://newsletter.gamediscover.co/p/the-state-of-steam-early-access-graduates), [Game World Observer](https://gameworldobserver.com/2025/12/10/according-to-gamediscoverco-most-games-on-steam-earn-less-after-leaving-early-access-than-they-do-at-launch)
- Mobile retention and ARPDAU: [Playio (citing GameAnalytics)](https://blog.playio.co/arpdau-benchmarks-mobile-games); CPI: [Admiral](https://admiral.media/mobile-app-marketing-benchmarks-2026/)
- Console ports: [PC Gamer](https://pcgamer.com/indie-veterans-on-the-state-of-indie-games-on-nintendo-switch-versus-steam), [Nintendo Life](https://www.nintendolife.com/news/2018/10/wandersong_on_switch_eshop_has_outperformed_steam_sales_by_nearly_three_to_one)
- Steam distribution: [game-developers.org](https://game-developers.org/2025-steam-game-revenue-distribution)
