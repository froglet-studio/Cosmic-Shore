# Crew of Four Lab

A `/labmaker` browser studio for a **four-seat multiplayer vessel** that captures the magic of **Wildgate**
(Moonshot Games / Dreamhaven, 2025). The question it puts to the designer, from the brief:

> Look up the game Wildgate and make an artifact lab that can explore concepts for a 4 player multiplayer vessel
> that captures the magic of that game. (Garrett, 2026-10-20)

- **Source (the page is published from it):** `Docs/Studios/CrewOfFourLab.html` (round 2, 3D). Round 1's 2D page is kept
  as `CrewOfFourLab.round1.html`, and its numbers are on the page's Archive tab. Open it in Chrome to use a gamepad (the
  claude.ai viewer can block the Gamepad API).
- **Live page:** https://claude.ai/artifact/UaJcpaXzgkza2eHSQCZwmb (private to the owner until shared from its Share
  menu; decisions recorded there land in the shared log, collection `decisions`, that Claude reads at the start of each
  round).
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/CrewOfFourLab.html` (PASS, round 2).
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
| Space | **Breach** | destruction | fortress prisms destroyed in 90 s | 2 → 5 levels (round 2: each level three orthogonal rings, an armillary sphere; the first at 26 u, inside a Charge-3 skiff charge); 1, 1, 2, 2 rivals |
| Mass | **Anchorage** | creation | your prisms standing at 90 s (trail + forged blocks) | 1, 1, 2, 2 raider crews; 16 → 12 crystals; 0 → 2 hazards |
| Charge | **Broadside** | dogfight | net petal hits (points: your share of the hits) | 1, 1, 2, 2 rivals; 12 → 8 crystals |

Rivals: Easy / Medium / Hard AI crews (aim error 9° / 5° / 2.5°, lead 0 / ½ / full, re-plan 1.8× / 1.25× / 1×,
ward tolerance 3 / 1.5 / 0.5 threats). Your own crew's AI has the same three levels. Every AI writes the same input
objects a person does (`/labmaker` §2.4).

## 4. Round 2 (2026-10-20): 3D with the game's chase camera

**Input this round** (Garrett): "make it 3D with a chase camera like the game". The decision log was empty; this
message is the round's input. Round 1's 2D page is kept as `CrewOfFourLab.round1.html` and its scorecard on the
Archive tab.

### What changed

- **The model is 3D.** The arena is a sphere of the game cell's radius (320 u). Every hull and skiff has a forward and
  an up vector and turns by yaw, pitch and roll at the Sparrow's rates (`YawScaler` / `PitchScaler` 80 °/s,
  `RollScaler` 30 °/s, times the hull's 0.55). Turrets aim in 3D. Crystals, hazards and the Artifact sit anywhere in the
  ball; the gate is a ring; the Breach fortress is an armillary sphere (three orthogonal rings per level). AI crews orbit
  in their own tilted planes and still write only inputs.
- **The view is a three.js chase camera** (r128 from cdnjs, as the other studios load it) at
  `SparrowCameraSettingsSO`'s numbers: `followOffset` (0, 10, −50) in the hull's own frame, `followSmoothTime` 0.2 s,
  near 0.3, far 12 000; it follows your skiff when you fly it. **C** (pad **Y**) switches to an overview of the whole arena.
  Everything in the world is lit and faceted (the LOCKED "nothing in the world looks flat" rule): prisms are instanced
  boxes at the Sparrow's `BaseScale`, the ward is a faceted translucent shell, bolts are lit volumes.
- **The game's occlusion corridor.** Prisms between the camera and the hull are not drawn, as the game's
  `PrismOcclusionCorridor` hides them (`Docs/PRISM_ANIMATION.md` §4.7). Without it the hull's own trail streamed into
  the lens and filled the screen.
- **An overlay** keeps the deck plan and adds a radar (top right: your hull seen from above, a tick up or down for
  above or below), labels and edge arrows for rivals, the gate, the Artifact and the fortress, and at the Guns a
  crosshair (where you point) and a diamond (where the turret points; red while your ward blocks it).
- **Controls:** **A/D** yaw, **W/S** pitch, **Q/E** roll, **Shift** boost, **X** brake, **Space** the station's action
  (Controls tab: invert pitch). The guns aim with the mouse along the camera ray; a pad aims with the right stick; touch
  drags to steer and holds to fire.
- **The Camera tab** exposes `camBack`, `camUp`, `camSmooth` (from the asset) and `camFov` (lab).

### Scorecard (I2, Hard rivals, mirror matches, every dial at its round-2 value)

Same columns and rules as round 1 (below). Re-run for this write-up on the final page with `runBatch`; 4.7–8.3 s per
game.

#### Gate Run (Time race, 12 seeds)

| Concept | Points | ± SE | Escaped % | Escape s | Seat value % (1/2/3/4) | Rookie % | Swaps/min | Ward drops/min | Punished % | Boards/min | Cast off % | Reversals | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Galleon | 118.32 | 33.13 | 50 | 52 | Helm 91 / Guns -19 / Core 14 / Skiff 98 | 118 | 5.29 | 25.32 | 41.09 | 4.32 | 33 | 2.5 | 10.18 | 22.25 / 21.25 |
| Corsair | 120.69 | 33.3 | 50 | 49 | Helm 92 / Guns 17 / Skiff 0 / Skiff 15 | 70 | 9.89 | 17.26 | 41.77 | 6.54 | 53 | 2.67 | 6.28 | 32.92 / 33.75 |
| Lantern | 98.41 | 35.11 | 42 | 43.8 | Helm 100 / Guns 2 / Core 0 / Scope 40 | 120 | 0 | 1.6 | 0 | 0 | — | 0 | 39.84 | 11.67 / 13 |
| Ironclad | 79.01 | 33.69 | 33 | 43 | Helm 100 / Guns 0 / Guns 0 / Core 0 | 100 | 0 | 27.13 | 52.57 | 0 | — | 0 | 8.36 | 24 / 23.75 |
| Solo crew (control) | 2.81 | 0.91 | 0 |  | — | — | 1.96 | 1.7 | 0 | 0.31 | 60 | 0.67 | 1.3 | 5.08 / 18 |

#### Breach (Space destruction, 6 seeds)

| Concept | Points | ± SE | Score | Win % | Seat value % (1/2/3/4) | Rookie % | Swaps/min | Ward drops/min | Punished % | Boards/min | Cast off % | Reversals | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Galleon | 55.67 | 3.92 | 55.7 | 0 | Helm 47 / Guns 100 / Core 8 / Skiff 10 | 92 | 0 | 4 | 37.25 | 0 | — | 1 | 48.17 | 1.33 / 1.83 |
| Corsair | 66.67 | 9.33 | 66.7 | 17 | Helm 52 / Guns 83 / Skiff 16 / Skiff 15 | 106 | 0 | 4 | 51.02 | 0 | — | 0.83 | 2.02 | 1.5 / 1.67 |
| Lantern | 76.33 | 2.4 | 76.3 | 50 | Helm -34 / Guns 100 / Core 4 / Scope 0 | 93 | 0 | 0 | 0 | 0 | — | 2.83 | 43.2 | 0 / 0 |
| Ironclad | 93.83 | 4.92 | 93.8 | 0 | Helm 66 / Guns 34 / Guns 34 / Core -5 | 114 | 0 | 5.89 | 12.5 | 0 | — | 3.67 | 46.25 | 3.5 / 4 |
| Solo crew (control) | 0 | 0 | 0 | 0 | — | — | 0.89 | 0 | 0 | 0 | — | 0 | 2.62 | 0 / 1 |

#### Anchorage (Mass creation, 6 seeds)

| Concept | Points | ± SE | Score | Win % | Seat value % (1/2/3/4) | Rookie % | Swaps/min | Ward drops/min | Punished % | Boards/min | Cast off % | Reversals | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Galleon | 133.17 | 7.94 | 133.2 | 50 | Helm -5 / Guns 9 / Core 33 / Skiff 30 | 93 | 5.44 | 23.88 | 31.45 | 3.78 | 84 | 5.33 | 13.53 | 37.83 / 37.17 |
| Corsair | 129.67 | 6.69 | 129.7 | 50 | Helm 15 / Guns 10 / Skiff 4 / Skiff 15 | 88 | 13.44 | 13 | 41.23 | 4.89 | 89 | 4.83 | 10.6 | 59.17 / 61.83 |
| Lantern | 92.17 | 6.71 | 92.2 | 33 | Helm -8 / Guns -1 / Core 12 / Scope 1 | 97 | 0 | 0 | 0 | 0 | — | 5.5 | 73.33 | 0 / 0 |
| Ironclad | 89.33 | 2.63 | 89.3 | 100 | Helm -3 / Guns 7 / Guns 7 / Core 7 | 92 | 0 | 23.1 | 24.92 | 0 | — | 7.33 | 15.98 | 44.67 / 26.5 |
| Solo crew (control) | 93.83 | 5.09 | 93.8 | 0 | — | — | 4.44 | 0 | 0 | 0 | 75 | 0.17 | 3.23 | 8.17 / 20.33 |

#### Broadside (Charge dogfight, 6 seeds)

| Concept | Points | ± SE | Score | Win % | Seat value % (1/2/3/4) | Rookie % | Swaps/min | Ward drops/min | Punished % | Boards/min | Cast off % | Reversals | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Galleon | 49.95 | 0.58 | 0 | 50 | Helm 4 / Guns 18 / Core 14 / Skiff 6 | 77 | 14.11 | 24.21 | 67.33 | 7.22 | 95 | 5 | 6.85 | 91.67 / 91.67 |
| Corsair | 50.29 | 0.87 | 0.2 | 33 | Helm 17 / Guns 21 / Skiff 10 / Skiff 10 | 81 | 19.88 | 10.33 | 51.92 | 8.89 | 87 | 5.33 | 10.08 | 99.67 / 99.5 |
| Lantern | 51.63 | 12.05 | 0.8 | 50 | Helm -21 / Guns 100 / Core 100 / Scope -5 | 0 | 0 | 0 | 0 | 0 | — | 0.33 | 88.87 | 33.5 / 32.67 |
| Ironclad | 51.95 | 3.37 | 7.2 | 67 | Helm 10 / Guns 38 / Guns 38 / Core 21 | 18 | 0 | 14.22 | 61.13 | 0 | — | 2.83 | 5.45 | 90.17 / 83 |
| Solo crew (control) | 17.85 | 1.41 | -30.2 | 0 | — | — | 4.89 | 0 | 0 | 0 | 43 | 0 | 2.95 | 8.5 / 38.67 |

### Checked

- `verify_lab.cjs` PASS (the stage is WebGL, so its blank check is skipped and the screenshots were read: desktop and
  iPhone 13 both show the hull under the chase camera, the deck plan and the radar).
- A scripted person in headless Chromium with SwiftShader: at the Helm, holding W+D wrote yaw 1 and pitch 1 and Space
  dropped the ward; at the Guns the mouse aimed along the camera ray and bolts flew; at the Skiff Space launched it and
  the camera moved to it; C switched to the overview. No console or page errors.
- Mirror fairness: Gate Run over 20 seeds 35–60% wins; Breach over 60 seeds 45% (Ironclad) and 53% (Galleon).

### Found

1. **The round-1 findings survive the move to 3D.** Concepts with no skiff (Lantern, Ironclad) change stations
   0 times a minute in every game and have 0 reversals in Gate Run. The solo control loses everywhere: 2.8 points in
   Gate Run, 0 prisms in Breach, a 17.9% hit share in Broadside. In Anchorage it keeps 94 prisms, level with full
   Lantern (92) and Ironclad (89) crews.
2. **Galleon and Corsair tie as racers in 3D** (118 and 121 points, 50% escapes each; Lantern 98 and 42%, Ironclad 79
   and 33%, all inside a standard error of about 33). The Corsair still needs a skilled crew (rookie share 70 against the
   Galleon's 118). The Galleon's Helm and Skiff are its seats that matter in the race (91% and 98%).
3. **The Corsair keeps everyone busy.** In Broadside it changes stations 19.9 times a minute, boards 8.9 times, and every
   seat is worth 10–21% (Galleon 4–18%). In Anchorage every Corsair seat is worth 4–15%.
4. **Breach became a shooting gallery in 3D.** Crews orbit the fortress in their own planes and barely meet (0–4 hits a
   match); the single gun deck is worth 83–100% and the skiff 10–16%; the Galleon's longest wait is 48 s. Ironclad (two
   gun decks) scores most (94).
5. **The ward gamble holds:** helms that must drop the ward to fire drop it 4–27 times a minute, and 12–67% of drops
   are punished. The Lantern drops it 0–1.6 times a minute, and its Broadside is still degenerate (Helm −21%, Scope −5%,
   rookie share 0, a crew member waiting 89 s).
6. **A 3D arena empties fast.** At round 1's 420 u radius the ball holds about 560 times the 2D disc's area in volume:
   Anchorage crews never met (0 hits) and a Core waited the whole 90 s. The arena is now the game cell's 320 u and
   Anchorage crews spend every third 20 s spell raiding the nearest rival's mass.
7. **In Breach the first charge takes the inner ring.** Two skiffs that launch together reach the core a step apart
   (3.60 s and 3.67 s in one trace) and the first blast clears all 69 inner prisms before the second grows. A prism
   inside two blasts in the same step now goes to one of them at random (seeded). Winner-takes-the-ring stays, and is a
   design question (below).

### Decision needed (designer), added this round

- **Is the chase camera's offset right for a four-seat hull?** The Sparrow's (0, 10, −50) frames a 23 u hull tightly;
  the Camera tab moves it.
- **Breach's first charge.** Should a skiff charge in the fortress core clear the whole inner ring for whoever gets there
  first, or should charges be smaller and repeatable?
- **What the other three crew see.** Only the helm's chase camera exists; a gunner's or a skiff pilot's own view is open.

## 4b. Round 1 (2026-10-20): 2D top-down (superseded)

> **Superseded by the designer (2026-10-20):** "make it 3D with a chase camera like the game". The page is kept as
> `Docs/Studios/CrewOfFourLab.round1.html` and its numbers are on the live page's Archive tab.

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

round 2 is 3D with one chase camera and stand-in shapes (the hull, skiffs and fortress are not game models; four cameras on one hull and a turret's own view are open);
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
