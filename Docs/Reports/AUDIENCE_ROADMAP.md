# Cosmic Shore audience roadmap: one niche at a time, joined into one household

**The same plan as an interactive page:** [Cosmic Shore Audience Roadmap](https://claude.ai/artifact/AZTC2TZYJJDY7ajqgaogtZ)
(private; the owner shares it from the page's Share menu). Source: `Docs/Reports/audience_roadmap.html`. It continues
[`MARKET_SIZING.md`](MARKET_SIZING.md), whose SOM is pin 1 below.

**From:** a Claude session for Garrett, 2026-11-05. A proposal: nothing here is assigned to anyone, and every "done
when" is a suggestion until pin 1 gives a baseline.

## 1. The idea

Cosmic Shore already holds a racer, a party game, a hex puzzle, a sniper duel, a kite toy, a painter and a sport, and
its crew vessels seat two different kinds of player in one hull. Go after one niche at a time, starting with the one
reachable today. Use each niche to bring in the next through the people its players already play with. The goal is
voyages: one squad with a racer, a puzzler, a kid and a competitor in it, each on their own device, each playing their
own genre, each feeding the others. The Ratel's patterns become the Honeyguide's slip charges, and the Honeyguide's
hives become the Ratel's raids (`Docs/Studios/ONE_THUMB_SEAT_ARCS.md` §4).

## 2. The pins, in order

| # | When | Niche | Way in | Device | State |
|---|---|---|---|---|---|
| 1 | Now (Steam Playtest) | Arcade flight racers | Skim Race and each hull's Time card; Regatta | PC, Steam Deck, gamepad | Playable |
| 2 | Next (paid Early Access) | Friends' party nights | Maelstrom, Bloomrush, Scarab Scramble, Joust | PC, Steam Deck | Playable |
| 3 | Then (public mobile) | Mobile puzzle players → the Ratel | The Dynamo hex puzzle, with the AI flying the Honeyguide | Phone, portrait, one thumb | Partly built |
| 4 | Later | Families and kids | Wrasse kite, Goby and Pistol, the Toybox in a browser | Tablet, phone, browser | Partly built |
| 5 | Later | Cozy creators | Tiger Moth painting, the Toybox gallery | Steam Deck, PC, browser | Partly built |
| 6 | Later | Competitive PvP and sports | Astro League, Broadside, Brood Rush, Dog Fight, Serpent Gulag | PC, gamepad | Partly built |
| 7 | The goal | Mixed-genre squads | Voyages on the HyperSea | Every device | Partly built |
| 8 | Expansion | The living room on console | Orca and the crew vessels, local | Switch family | Not started |

**Why racers first:** the Steam Playtest is the launch in motion, the racers' hulls ship, and Time is the one element
whose AI is live (`Docs/AI_SYSTEM/AI_PLAN.md`), so a niche with few players still gets full grids. Skim Race seats 12.
The niche is small (only 85 Flight-tagged games have ever passed $1M on Steam) and has few strong rivals.

**Why puzzlers third and not first:** the Ratel is the strongest bridge in the game, but it needs a public mobile
release, a way for phone players to pay (commerce is de-scoped, R4), online crew seats and phone-to-PC cross-play. None
of those exists yet. Pins 1 and 2 build the PC players whose partners, parents and friends the puzzlers are.

## 3. Working backwards

| The end state needs | Delivered by |
|---|---|
| Mixed-genre squads sail voyages together | pin 7 |
| A seat for every kind of player: online crew seats, one-thumb seats | pins 3, 4 |
| One party across phone, tablet, Steam Deck and PC | pin 3 |
| A public mobile release and a way for phone players to pay | pin 3 |
| A paying PC audience with friends to invite | pin 2 |
| A first niche that plays even when few people are online | pin 1 |

## 4. The bridges

| Bridge | Joins | State |
|---|---|---|
| Honeyguide and Ratel (flyer + portrait hex puzzle) | 1, 2 → 3 | Built, local; remote Ratel untested |
| Wrasse and Grouper (flyer + portrait kite; Wrasse Run) | 2 → 4 | Built; online seats planned |
| Goby and Pistol (flyer + side-view hill rider) | 4 | Built; no card |
| Orca (helm + three gunners) | 4 → 8 | Stub; not networked |
| Maelstrom (a random mode each round from sixteen) | 1 → 2 → 6 | Playable |
| Elements as genres (Time race, Mass make, Space destroy, Charge PvP; `ModeGenre.cs`) | all | In the game |
| Squads and voyages | all → 7 | Core and tests built; slice not played |
| One account and one party on any device | 2 → 3, 4 | Cross-play not verified |

**The number that says it is working (proposal):** the share of weekly players who played with someone whose main
vessel is a different genre. It rises only when the niches join.

## 5. Decisions this needs

1. **Steam first or mobile first.** `Docs/claude/ABOUT_PROJECT.md` says "mobile-first with PC/console expansion";
   everything in motion is Steam-first. This plan takes Steam for pins 1–2 and brings mobile in at pin 3.
2. **How a phone player pays.** Free with purchases, free with the PC owner's invite, or paid.
3. **A Dynamo card**, so a puzzle player starts in the Ratel seat from the Arcade with the AI flying the Honeyguide. It
   seats a player and changes nothing about the vessel, so the locked rule that a mode never changes the vessel holds.
4. **Cross-play** between phone and PC: not yet checked.
5. **Steam Remote Play Together** could hand a local crew seat to a friend who does not own the game, once two humans
   in one hull (built, untested) is tested.
6. **Children's privacy** before pin 4 is marketed to families (`Docs/Legal/` lists eleven policy/code disagreements).

Owners to ask before changing their areas: the Ratel and Wrasse seats are Garrett's. Anything on UGS or the backend,
and activation, goes through Shombith, with Caleb heading the FTUE.

## 6. Market anchors (2025)

- Mobile puzzle: over $10B IAP, +14%, second genre after strategy ([Sensor Tower](https://gamedevreports.substack.com/p/sensor-tower-state-of-gaming-2026)); 81% of puzzle players women, 84% play to relax ([Naavik, Mistplay survey](https://naavik.co/digest/what-leading-match-3-and-merge-games-do-differently/)).
- Families: 70% of US parents play; 82% of them play with their children, 52% weekly ([ESA 2025](https://www.theesa.com/annual-esa-study-reveals-video-games-universal-appeal-across-generations/)).
- Co-op on Steam ≈ $8.2B ([Notebookcheck, Alinea](https://www.notebookcheck.net/Games-with-co-op-modes-generated-8-2-billion-in-gross-revenue-on-Steam-in-2025.1218225.0.html)); Flight tag: 85 games ever over $1M ([WN Hub](https://wnhub.io/news/other/item-52312)).
- Cozy: 11% of cozy games reached about 1,000 reviews against 4.2% across Steam ([Steam Page Analyzer](https://www.steampageanalyzer.com/blog/cozy-games-steam-2026-niche-report)).
- Browser: Poki about 100M monthly players ([Mobidictum](https://mobidictum.com/pokis-web-gaming-interview-michiel-van-amerongen)).
- Steam Deck ≈ 3.7–4M units ([WN Hub, IDC](https://wnhub.io/news/analytics/item-47202)); Switch over 155M, Super Mario Party Jamboree 8.64M ([Gematsu](https://www.gematsu.com/2026/02/switch-2-worldwide-sales-top-17-37-million-switch-tops-155-37-million)).
- Multi-genre social: Roblox $6.8B bookings, Q4 DAU 144M ([GamesBeat](https://gamesbeat.com/roblox-stock-rises-20-after-earnings-daily-active-users-up-69-in-q4/)); LEGO Fortnite launched bigger than every battle royale mode combined ([GamesRadar](https://www.gamesradar.com/lego-fortnite-launches-with-more-players-than-every-fortnite-battle-royale-mode-combined)).
