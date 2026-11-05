# Cosmic Shore market sizing: TAM, SAM, SOM

**The same model as an interactive page:** [Cosmic Shore Market Map](https://claude.ai/artifact/QgkTZY2WKM1khg4twGCind)
(private; the owner shares it from the page's Share menu). Its source is `Docs/Reports/market_sizing.html`.

**From:** a Claude session for Garrett, 2026-11-05. **Basis:** calendar 2025 gross consumer spend in USD, from public
third-party estimates (Valve publishes no Steam figures). Every share is either **sourced** or marked **assumption**.

## 1. In one minute

- **TAM, $13.8B:** premium (paid) games on Steam. That is the store and business model the game is built to sell
  through ($15, no price rise at 1.0, `Docs/STEAM_BUSINESS_SETUP.md` A6).
- **SAM, ≈ $108M a year:** the part of that the launch can serve: indie-priced, online multiplayer, party / arcade racing
  / arcade flight, English-only, Windows.
- **SOM, ≈ $92k gross in year one (base case):** about 8,400 units from 30,000 launch wishlists, about 0.09% of SAM.
  Low case $22k, high case $866k. Net to Froglet after Steam's 30% is about $65k in the base case.
- **The lever is wishlists.** At base assumptions each extra 10,000 launch wishlists is worth about $31k of year-one
  gross. The Steam Playtest earns nothing (Valve forbids charging for access), so its job is to grow that number.
- **The biggest branch is not on Steam.** Mobile is $108B; a free-to-play party and arcade multiplayer slice is roughly
  $1.2B (assumption). It needs public store releases and an in-app economy; commerce is de-scoped today (R4).

## 2. The hierarchy

| Level | Slice kept | Share of level above | Value | Basis |
|---|---|---:|---:|---|
| L0 Industry | Global games market | 100% | $197B | Newzoo 2025 year-in-review (Sept forecast $188.8B) |
| L1 Platform | PC (vs mobile $108B, console $46B) | 21.8% | $43B | Newzoo revised PC figure; handhelds count as PC |
| L2 Store | Steam (vs Epic, first-party clients, China PC) | 41.2% | $17.7B | Alinea Analytics (GameDiscoverCo: $16.9B) |
| L3 Business model | Premium games — **TAM** | 78% | $13.8B | Alinea: premium 78% of Steam |
| L4 Studio tier | Indie | 29% | $4.0B | Assumption from indie ≈ 25% of all Steam ($4.4–4.5B), mostly premium |
| L5 Mode | Online multiplayer and co-op | 45% | $1.8B | Assumption from co-op ≈ $8.2B, 46% of Steam |
| L6 Genre | Party 6%, arcade racing 2%, arcade space/flight combat 2% | 10% | $180M | Assumption |
| L7 Language | English-only launch — **SAM** | 60% | $108M | Assumption; more languages are planned for 1.0 |
| L8 Capture | Cosmic Shore, year one — **SOM** | 0.09% | $92k | Funnel model, §3 |

## 3. The SOM funnel

Year-one gross = launch wishlists × week-one conversion × (year one ÷ week one) × realized gross per unit.

| Scenario | Wishlists | Conversion | Year ÷ week | Realized $/unit | Units | Gross |
|---|---:|---:|---:|---:|---:|---:|
| Low | 7,000 | 0.10× | 2.8 | $11 | 1,960 | $22k |
| Base | 30,000 | 0.10× | 2.8 | $11 | 8,400 | $92k |
| High | 150,000 | 0.15× | 3.5 | $11 | 78,750 | $866k |

Conversion and the year multiplier are GameDiscoverCo medians (0.10× for games above $10; 2.8× for $15–19 games).
Realized price is an assumption: 73% of the $15 list after the launch discount, sales and Valve's regional prices.

## 4. Branches the launch does not reach yet

| Branch | Market | Slice that fits | Unlocked by | Today |
|---|---:|---:|---|---|
| Steam, more languages | — | +$72M | Localization (Simplified Chinese first) | Planned for 1.0 |
| Mobile, free to play | $108B | ≈ $1.2B | Public store releases + an in-app economy | iOS TestFlight, Play internal testing |
| Console | $46B | not sized | A port (Switch is where party games sell) | Not targeted |
| PC outside Steam | $25.3B | small | Epic, itch.io, the browser Toybox | Not targeted |

## 5. Reality check

- About 20,000 games launched on Steam in 2025; about 300 crossed $1M gross; the median earned about $249.
- The Flight tag: 85 games have ever crossed $1M on Steam, fewer than 20 reached $10M.
- Comparables (lifetime Steam gross, modelled by trackers that disagree up to 2.4×): Pummel Party $8.3M–$19.1M,
  Ultimate Chicken Horse $8.5M–$20.1M, Spaceflight Simulator $1.9M, Reentry ≈ $1M net. Mobile: Stumble Guys $168M
  lifetime.

## 6. Sources

- Newzoo 2025: [Wccftech](https://wccftech.com/newzoo-expects-global-video-games-market-reach-197-billion-2025-exceeds-expectations/), [PocketGamer.biz](https://www.pocketgamer.biz/newzoo-global-games-market-set-to-hit-1888bn-in-2025/)
- Steam 2025, premium and indie shares: [Notebookcheck (Alinea)](https://www.notebookcheck.net/Indie-games-accounted-for-25-of-Steam-s-revenue-in-2025.1189429.0.html), [Alinea year in review](https://gamedevreports.substack.com/p/alinea-analytics-2025-year-in-review), [GameDiscoverCo](https://newsletter.gamediscover.co/p/revealed-the-numbers-behind-steams)
- Co-op on Steam: [Notebookcheck (Alinea)](https://www.notebookcheck.net/Games-with-co-op-modes-generated-8-2-billion-in-gross-revenue-on-Steam-in-2025.1218225.0.html)
- Release distribution: [game-developers.org](https://game-developers.org/2025-steam-game-revenue-distribution); Flight tag: [WN Hub](https://wnhub.io/news/other/item-52312)
- Wishlist conversion: [GameDiscoverCo](https://newsletter.gamediscover.co/p/the-state-of-steam-wishlist-conversions), [benchmarks](https://gamedevreports.substack.com/p/gamediscoverco-conversion-benchmarks)
- Mobile: [Sensor Tower State of Gaming 2026](https://gamedevreports.substack.com/p/sensor-tower-state-of-gaming-2026), [Udonis on Stumble Guys](https://www.blog.udonis.co/mobile-marketing/mobile-games/stumble-guys)
- Comparables: [Raijin](https://raijin.gg/app/880940/Pummel_Party), [SteamData.AI](https://steamdata.ai/game/880940), [Raijin UCH](https://raijin.gg/app/386940/Ultimate_Chicken_Horse), [SteamData.AI UCH](https://steamdata.ai/game/386940), [Raijin SFS](https://raijin.gg/app/1718870/Spaceflight_Simulator), [Datahumble Reentry](https://datahumble.com/reentry---a-space-flight-simulator-882140)
- Cosmic Shore: `Docs/STEAM_BUSINESS_SETUP.md`, `Docs/Reports/STEAM_LAUNCH.md`, `Docs/STEAM_RELEASE_TASKS.md`
