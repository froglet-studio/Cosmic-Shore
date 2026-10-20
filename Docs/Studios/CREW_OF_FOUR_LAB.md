# Crew of Four Lab

A `/labmaker` browser studio for a **four-seat multiplayer vessel** that captures the magic of **Wildgate**
(Moonshot Games / Dreamhaven, 2025). The question it puts to the designer, from the brief:

> Look up the game Wildgate and make an artifact lab that can explore concepts for a 4 player multiplayer vessel
> that captures the magic of that game. (Garrett, 2026-10-20)

- **Source (the page is published from it):** `Docs/Studios/CrewOfFourLab.html`. Open it in Chrome to use a gamepad
  (the claude.ai viewer can block the Gamepad API).
- **Live page:** https://claude.ai/artifact/UaJcpaXzgkza2eHSQCZwmb (private to the owner until shared from its Share
  menu; decisions recorded there land in the shared log, collection `decisions`, that Claude reads at the start of each
  round).
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/CrewOfFourLab.html` (PASS, round 1).
- **Nearest lab:** the Crew Vessel Lab (`Docs/Studios/CrewVesselLab.html`, `CREW_VESSEL_LAB.md`), the two-seat
  "Maverick and Goose" lab that graduated as the Wrasse (`VesselClassType` 19). This lab does not replace it or touch
  it: the Wrasse is a pilot plus a 2D navigator; this lab asks what a crew of **four** on one hull should be. Round 1 of
  that lab (four navigator consoles, superseded: "it does not look fun") is the trap this one tries to avoid by giving
  every seat a body in the world (a turret, a skiff, a deck to cross) rather than a menu.

## 1. What makes Wildgate work (research, 2026-10-20)

Wildgate is a crew-based PvPvE extraction shooter: five ships of up to four players in a procedurally generated
cloud field (the Reach). A crew wins by flying the Artifact out through the Wildgate, or by being the last ship flying.
What the sources describe as its magic, and what this lab borrows from each:

| Wildgate | What it does for the game | In this lab |
|---|---|---|
| **The ship is the crew's lifeline**; while it holds, the crew lives | Shared fate: four people care about one thing | One hull, one shared pool of petals (element levels) powering every station |
| **Loose roles**: one pilots, others shoot, repair, probe or leave the ship; nothing is assigned | Crews split tasks in real time and re-split when something breaks | Stations on a deck; any crew member can stand at any of them; moving costs walk time |
| **Shields lowered from the pilot seat**: +35% base speed, and cannons do heavy damage | The pilot's gamble: to hurt or to run you open yourself up | The ward: up, it keeps petals (a bolt on it scores nothing); with "ward blocks your own guns" on, nothing fires until the helm drops it; down, the hull is faster |
| **Repairs, fires, the reactor fed with ice** to heal | Constant chores that pull people off their posts | The Core refines hold crystals into petals and recharges the ward; nobody at the Core, no regen beyond a trickle |
| **Boarding**: jetpack across, steal cannons, overload the reactor, drop their shields, take the helm | The best stories, and the reason someone always has to stay home | The skiff boards a rival and siphons petals into a bag it must bring home; a defender standing on deck casts it off and the bag comes back |
| **The Artifact** reveals the carrier to everyone; no teleport home while carrying it | The whole server converges; reversals and ambushes | Gate Run: lifting the Artifact reveals you; a skiff carrying it cannot recall; a boarder aboard a carrier for 4 s lifts it |
| **Probes** reveal rivals, points of interest and the Wildgate | Information as a job | The Scope seat (Lantern): a manned Scope sees every rival and the loose Artifact |
| **Ship classes** (Hunter, Bastion, Privateer, Scout) | Different crews, different plans | The concepts below |

Reviewers name the same strengths and the same risk: the ship-to-ship fights with a good crew "give Sea of Thieves a run
for its money", and **boarding can dominate** (Gideon's Gaming: "stealing gun emplacements only takes a couple of
seconds"; it "is going to have to be kneecapped"). The lab measured both (§4).

Sources: [Xbox Wire deep dive](https://news.xbox.com/en-us/2025/03/26/wildgate-deep-dive-crew-based-shooter/),
[Xbox Wire ship guide](https://news.xbox.com/en-us/2025/07/21/wildgate-spaceship-ship-guide/),
[Dignitas quick guide](https://dignitas.gg/articles/welcome-to-wildgate-a-quick-guide),
[Steam page](https://store.steampowered.com/app/3504780/Wildgate/),
[Gamerant preview](https://gamerant.com/wildgate-gameplay-preview-hands-on/),
[Gideon's Gaming review](https://gideonsgaming.com/wild-gate-review-an-incredibly-unbalanced-game/),
[PCGamesN guide](https://www.pcgamesn.com/wildgate/guide).

### What Cosmic Shore's rules change

**PvP is petals only (LOCKED, Garrett 2026-10-10, `Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`).** Wildgate's hull damage,
fires, reactor overloads, stolen cannons and boarders seizing the helm are all effects on another crew's vessel beyond
petals, so the lab leaves them out. Every vessel-on-vessel effect here moves petals and counts as a scored hit:

- a bolt on an unwarded hull takes one petal from its fullest element and gives it to the shooter;
- a boarder siphons petals into its bag (each one a scored hit);
- a defender casting a boarder off takes the bag back (each petal a scored hit for the defender), and the boarder's own
  recall fires. Nothing pushes it; that recall is the boarder's rule, flagged in §6 for a check against the LOCKED rule;
- a bolt on a skiff takes one petal from its bag, or from its hull when the bag is empty.

The ward is a petal ward, the way `CombatHitDrain`'s wards are: a bolt it absorbs scores nothing.

## 2. The concepts

Each concept is a seat set plus rules; the card writes the rules into the dials so they can be mixed.

| Concept | Seats | Rules | Chases |
|---|---|---|---|
| **Galleon** | Helm, Guns, Core, Skiff | walk 2.5 s; ward blocks your guns; crystals refined at the Core; boarders work under the ward | Wildgate taken literally: every crisis pulls someone off a station |
| **Corsair** | Helm, Guns, Skiff, Skiff | walk 1.5 s; no Core (crystals are petals on pickup, the ward only trickles back) | The boarding stories |
| **Lantern** | Helm, Guns, Core, Scope | consoles (a swap is `PilotSwap.CooldownSeconds` 0.75 s); guns fire through the ward; nobody in a Lantern match can board | Information and timing; the probe station |
| **Ironclad** | Helm, Guns, Guns, Core | walk 2.5 s; ward blocks your guns | The ship-to-ship duel and the ward gamble |
| *Solo crew* (control) | Galleon seats, **one** crew member | as Galleon, against full Galleon crews | What a crew of four buys |

Rival crews fly the same concept (a mirror match) unless **Match ▸ Rivals always fly the Galleon** is on.

## 3. The games (one per element, `/labmaker` §1.5), each with I1–I4

| Element | Game | Kind | Scores | I1 → I4 |
|---|---|---|---|---|
| Time | **Gate Run** | race | seconds to escape (points: 100 + seconds to spare, else 0.5 per second you held the Artifact) | 1, 1, 2, 2 rival crews; gate opens at 30 / 40 / 50 / 60 s; 14 → 8 crystals; 0 → 3 hazard clusters |
| Space | **Breach** | destruction | fortress prisms destroyed in 90 s | 2 → 5 rings (first ring at 26 u, inside a Charge-3 skiff charge); 1, 1, 2, 2 rivals |
| Mass | **Anchorage** | creation | your prisms standing at 90 s (trail + forged blocks) | 1, 1, 2, 2 raider crews; 16 → 12 crystals; 0 → 2 hazards |
| Charge | **Broadside** | dogfight | net petal hits (points: your share of the hits) | 1, 1, 2, 2 rivals; 12 → 8 crystals |

Rivals: Easy / Medium / Hard AI crews (aim error 9° / 5° / 2.5°, lead 0 / ½ / full, re-plan 1.8× / 1.25× / 1×,
ward tolerance 3 / 1.5 / 0.5 threats). Your own crew's AI has the same three levels. Every AI writes the same input
objects a person does (`/labmaker` §2.4).

## 4. Round 1 (2026-10-20)

### The model, and where its numbers come from

| Lab key | Value | Source |
|---|---|---|
| `minSpeed`, `throttleScaler`, `boostMul`, `yawRate` | 10 u/s, 25 u/s, ×5, 80 °/s | `Sparrow.prefab` (`VesselTransformer` defaults, `VesselStatus.boostMultiplier`), as the Crew Vessel Lab copies them |
| `prismW`, `prismL`, `wavelength` | 2, 5, 7 u | Sparrow `VesselPrismController` |
| Element levels | −5 … 15 | `ElementalBarsConfigSO.MinLevel` / `MaxLevel` (`PetalCount` 5) |
| `swapCooldown` | 0.75 s | `PilotSwap.CooldownSeconds` |
| `spaceScale`, `blastDuration`, `blastDelay` | 60, 3 s, 0.2 s | `AOEExplosion.prefab` (radius 0.5 × MaxScale, sin ease) |
| Domain colours | Jade, Ruby, Gold, Blue (neutral) | `Docs/Studios/VesselStudio/studio-domains.js` |
| Everything else (hull scale 0.85 / turn 0.55, ward, guns, Core, skiff, walk time, arena 420 u) | see the page's `SHIPPED` block | **lab** proposals: the game has no four-seat hull |

### How to use it

Pick a concept on the **Concept** tab, a game and intensity in the header, then **Watch** or take a seat (1–4, 5 for
the deck). The deck plan in the stage's lower left shows your crew walking between stations, the shared petals, the
hold, the ward and the boost tank. **Score every concept** re-runs the scorecard on your settings (about 5–8 s).
Record what you decide on the **Decisions** tab.

### Scorecard (I2, Hard rivals, mirror matches, every dial at its round-1 value)

`runBatch({ game, intensity: 1, diff: 2, seeds, seats: true, rookie: true })`, re-run for this write-up on the final
page. **Seat value** = points lost when that station is empty (one crew member fewer), as % of the larger of the points
and a game scale (Gate Run 60, Breach 40, Anchorage 60, Broadside 30). **Rookie** = an Easy crew against a Hard one,
100 minus the points lost on the same scale. In a mirror match Win % should sit near 50 (1 rival) and is noise over
6–12 seeds (20 seeds read 25–85% for one concept; §5 Found 8); read Points and the per-minute columns instead.

#### Gate Run (Time race, 12 seeds)

| Concept | Points | ± SE | Escaped % | Escape s | Seat value % (1/2/3/4) | Rookie % | Swaps/min | Ward drops/min | Punished % | Boards/min | Cast off % | Reversals | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Galleon | 98.79 | 31.64 | 42 | 57.6 | Helm 69 / Guns -116 / Core 35 / Skiff 98 | 101 | 6.73 | 23.18 | 41.69 | 3.66 | 48 | 2.42 | 9.1 | 26.33 / 22.17 |
| Corsair | 159.28 | 31.82 | 67 | 46.3 | Helm 83 / Guns 37 / Skiff 26 / Skiff 85 | 40 | 9.61 | 15.57 | 48.32 | 6.78 | 69 | 2 | 8.93 | 38.08 / 29.25 |
| Lantern | 77.49 | 33.06 | 33 | 47.5 | Helm 100 / Guns -2 / Core 0 / Scope -1 | 102 | 0 | 2.31 | 4.17 | 0 | — | 0 | 37.48 | 10.33 / 8.5 |
| Ironclad | 97.38 | 34.76 | 42 | 46.3 | Helm 100 / Guns 20 / Guns 20 / Core -1 | 101 | 0 | 21.27 | 48.83 | 0 | — | 0 | 12.69 | 32.83 / 33 |
| Solo crew (control) | 1.96 | 1.06 | 0 |  | — | — | 2.08 | 1.88 | 2.77 | 0.17 | 56 | 0.25 | 1.38 | 3.42 / 18 |

#### Breach (Space destruction, 6 seeds)

| Concept | Points | ± SE | Score | Win % | Seat value % (1/2/3/4) | Rookie % | Swaps/min | Ward drops/min | Punished % | Boards/min | Cast off % | Reversals | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Galleon | 63.33 | 4.61 | 63.3 | 67 | Helm 45 / Guns 76 / Core -2 / Skiff 26 | 93 | 0.28 | 10.72 | 27.05 | 0.42 | 100 | 0.67 | 19.47 | 7.17 / 6.5 |
| Corsair | 53.33 | 2.56 | 53.3 | 17 | Helm 32 / Guns 93 / Skiff 1 / Skiff -3 | 96 | 0.9 | 9.73 | 42.65 | 0.17 | 100 | 0.67 | 3.07 | 8.17 / 7.83 |
| Lantern | 60.17 | 2.44 | 60.2 | 50 | Helm 13 / Guns 95 / Core 4 / Scope -1 | 99 | 0 | 0 | 0 | 0 | — | 2.33 | 16.38 | 0 / 0 |
| Ironclad | 65.17 | 3.13 | 65.2 | 67 | Helm 73 / Guns 39 / Guns 39 / Core 1 | 79 | 0 | 11.83 | 35.48 | 0 | — | 1.5 | 9.48 | 6.33 / 7.17 |
| Solo crew (control) | 15.33 | 0.71 | 15.3 | 0 | — | — | 0.89 | 0 | 0 | 0 | — | 0 | 2.82 | 0 / 5.83 |

#### Anchorage (Mass creation, 6 seeds)

| Concept | Points | ± SE | Score | Win % | Seat value % (1/2/3/4) | Rookie % | Swaps/min | Ward drops/min | Punished % | Boards/min | Cast off % | Reversals | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Galleon | 167.33 | 6.63 | 167.3 | 50 | Helm 20 / Guns -9 / Core 41 / Skiff 34 | 104 | 1.67 | 5.77 | 13.75 | 0.67 | 100 | 3.83 | 19.03 | 7.83 / 8.83 |
| Corsair | 141.83 | 2.57 | 141.8 | 83 | Helm 16 / Guns 7 / Skiff 9 / Skiff 10 | 96 | 4 | 6.11 | 18.87 | 0.78 | 100 | 2.83 | 17.03 | 15.5 / 13.33 |
| Lantern | 104.83 | 2.93 | 104.8 | 50 | Helm 26 / Guns -3 / Core 4 / Scope -3 | 105 | 0 | 0 | 0 | 0 | — | 1.67 | 35.33 | 0 / 0 |
| Ironclad | 109.67 | 3.81 | 109.7 | 50 | Helm 35 / Guns 2 / Guns 2 / Core 8 | 93 | 0 | 5.33 | 17.03 | 0 | — | 3.33 | 27.68 | 2.17 / 2.5 |
| Solo crew (control) | 119.83 | 4.17 | 119.8 | 0 | — | — | 6.77 | 0 | 0 | 0 | 88 | 0.33 | 3.17 | 4.33 / 6.5 |

#### Broadside (Charge dogfight, 6 seeds)

| Concept | Points | ± SE | Score | Win % | Seat value % (1/2/3/4) | Rookie % | Swaps/min | Ward drops/min | Punished % | Boards/min | Cast off % | Reversals | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Galleon | 59.28 | 8.27 | 4 | 67 | Helm 21 / Guns 35 / Core 32 / Skiff 7 | 67 | 11.66 | 19.32 | 54.35 | 5.77 | 94 | 3.67 | 16.18 | 76.17 / 72.17 |
| Corsair | 48.19 | 1.25 | -8 | 50 | Helm 15 / Guns 21 / Skiff 14 / Skiff 13 | 96 | 17.77 | 7.77 | 51.42 | 8.44 | 84 | 4.33 | 23.37 | 96.5 / 104.5 |
| Lantern | 30.15 | 11.5 | -15.5 | 17 | Helm -129 / Guns 100 / Core 100 / Scope -37 | 0 | 0 | 0 | 0 | 0 | — | 0.83 | 71.58 | 15.67 / 31.17 |
| Ironclad | 43.6 | 4.14 | -15.5 | 33 | Helm 13 / Guns 29 / Guns 29 / Core 8 | 70 | 0 | 10.55 | 60.47 | 0 | — | 1.33 | 21.68 | 50 / 65.5 |
| Solo crew (control) | 14.27 | 2.07 | -30.7 | 0 | — | — | 5.22 | 0 | 0 | 0 | 30 | 0 | 3.58 | 6.67 / 37.33 |

**Noise.** Gate Run is won or lost, so its points carry a standard error of about 32 over 12 seeds, and its seat
values for Guns and Core sit inside that noise (Galleon Guns −116% is about −70 points, two standard errors). The
other games' standard errors are 1–12 points. Read Gate Run's seat values for the Helm and the Skiff only.

### Checked

- `verify_lab.cjs` PASS: no console errors, desktop (1600×900) and phone (iPhone 13) layouts fit with no sideways
  scroll, the `__lab` hooks answer, every `SPEC` key is in `SHIPPED` and in range, the manual clock advances,
  `runBatch()` is identical within a page and across two page loads, the stage draws.
- A scripted person (Playwright keyboard and mouse) took each seat: at the Helm, Space dropped the ward; at the Guns,
  holding the mouse fired once the AI helm dropped the ward for them; at the Skiff, Space launched and recalled it;
  0 returned to Watch. No page errors.
- Mirror fairness over 20 seeds (`runBatch`, one rival): Gate Run 35–55% wins before the turn-taking fix; Breach
  25–50% and Broadside 45–70% after crews took turns (before blasts did, Found 8). Inside about two binomial standard
  errors (±11%) except Corsair in Breach (25%).
- The batch takes 4.6–8.3 s per game for 5 concepts × (full crew, four empty seats, Easy crew) × seeds.

### Found

1. **A crew of four is the point.** The solo control (the Galleon with one person running between stations, against
   full crews) scores 1.96 points in Gate Run (no escapes in 12), 15 prisms in Breach (Galleon 63), and a 14% hit
   share in Broadside. The exception is Anchorage: a solo Galleon keeps 120 prisms, more than full Lantern (105) and
   Ironclad (110) crews, because it runs between the Core and the Helm 6.8 times a minute and forges.
2. **Boarding is what makes the crew move.** Concepts with no skiff (Lantern, Ironclad) show **0 station changes a
   minute in every game and 0 reversals in Gate Run**: nobody ever has a reason to leave a post, and the race is
   decided at the pickup. Galleon and Corsair change stations 6.7–17.8 times a minute in Gate Run and Broadside, and
   the Artifact changes hands about twice a run. This matches Wildgate's sources, where boarders are both the best
   stories and the reason someone stays home.
3. **The Corsair (two skiffs) is the strongest racer and the busiest crew,** and the Galleon is the best all-rounder.
   Corsair: Gate Run 159 points and 67% escapes (Galleon 99 and 42%, inside the noise), 17.8 swaps and 8.4 boards a
   minute in Broadside, and every Broadside seat worth 13–21%. Galleon: the most Anchorage points (167), Breach
   level with the others (63; Ironclad 65, Lantern 60, Corsair 53), every Broadside seat worth 7–35%, and the most ward
   drops in Gate Run and Broadside (23 and 19 a minute). The Corsair's Gate Run rookie
   share is 40%: the two-skiff plan only works with a skilled crew.
4. **The ward gamble only exists when the ward blocks your guns.** Galleon, Corsair and Ironclad helms drop the ward
   5–23 times a minute and 14–60% of those drops are punished by a hit. The Lantern (guns fire through the ward) drops
   it 0–2.3 times a minute, and its Broadside turns into a ward-grinding contest: 30% hit share ± 11, a negative Helm
   value, and a rookie share of 0 (an Easy crew never broke a Hard crew's ward).
5. **Some seats sit idle.** The Lantern's Scope is worth −37 to −1% in every game (leaving it empty never cost
   points), and its crew's longest wait is 16–72 s. Lantern and Ironclad crews wait 28–35 s in Anchorage. The Corsair's longest wait in Breach is 3.1 s. A seat whose
   value is near 0 and whose occupant waits half a minute fails the "every seat matters" test.
6. **Breach is a gun game.** The Guns seat is worth 76–95% for every concept with one gun deck; the Core is worth
   −2 to 4%. The skiff's charge is worth 26% on the Galleon once the fortress's first ring sits inside its blast
   (Found 7).
7. **Design hole fixed during the round:** the skiff's breach charge, copied from `AOEExplosion` (MaxScale 60, radius
   30 u × 1.16 at Charge 3 = 35 u), never reached a fortress whose first ring was at 40 u, so the skiff was worth
   about nothing in Breach. The rings now start at 26 u (`FORT_R0`).
8. **The order crews were stepped in decided ties.** With crew 0 always stepped first, Corsair crews won 85% of mirror
   Breach matches over 20 seeds (two charges in one core: the first processed took the prisms). Crews now take turns
   going first each step, and blasts too; the same run read 25%. Wins in a mirror match stay noisy.
9. **Bugs the lab's own traces found and fixed before the numbers above:** a skiff carrying the Artifact chased its old
   boarding target forever instead of flying home; boarders re-boarded a drained hull every second (62 boardings in one
   run); a Broadside run with no hits scored a 0% share instead of 50%; a lone crew member never left the Helm (its
   need was always the highest); taking the Skiff seat while an AI was out in it left two people on one seat; an AI
   helm ignored a human gunner holding fire.

### Decision needed (designer)

- **Which concept to take forward**, or which mix: the Galleon's every-seat-matters spread, the Corsair's boarding and
  racing, or a Galleon with a second skiff option. Record it on the Decisions tab.
- **Should the ward block your own guns?** It is the only thing that makes the helm gamble (Found 4); off, wards grind.
- **Should boarders work under the ward?** Wildgate's shields never stop a boarder; Cosmic Shore's wards keep petals.
  The lab defaults to yes (`boardBypass`).
- **The cast-off.** A defender takes the bag back and the boarder's own recall fires. Confirm this reads as "petals
  only" (the boarder is not pushed) against the LOCKED rule, or drop the recall and let the emptied boarder stay.
- **Should a skiff carrying the Artifact drop it when shot?** Not modelled: dropping an objective is not a petal move,
  so the lab leaves it out until the owner rules on it.
- **How much walking.** Lantern consoles (0.75 s) show no swaps at all; the Galleon's 2.5 s walk shows 6.7 a minute.
  Whether walking is fun for a person, not an AI, needs a playtest.

### What this lab does NOT model

2D top-down only (the game is a 3D chase view: four cameras on one hull and how a deck reads in 3D are open);
only one seat is a person, and real crews talk, which is most of Wildgate's magic; no networking (who owns the hull,
seats and skiff across machines); no fires, breaches, overloads or stolen guns (forbidden by the LOCKED rule); no PvE
dungeons, upgrades or probes as items; at most three crews (the game's three playable domains), not five; the hull,
ward, guns, Core, skiff and their numbers are proposals; crystals are lime with their element told by shape, and the
element bars use lab colours.

## 5. Graduation (not started)

Per `/labmaker` §7, "bring it to Unity" means the Toybox's Vessel Changer offers it: a crew hull ships on a hull class
the way the Wrasse does (`Tools/Build/author_wrasse_assets.py`), seats would ride the duo-vessel system
(`cece/sharp-tesla-j0kd7z`), and the online question is the Wrasse's open `T-20261017-b06e`. Nothing is ported in
round 1, and no enum value or `GameModes` id is claimed.
