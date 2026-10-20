# Crew of Four Lab

A `/labmaker` browser studio for a **four-seat multiplayer vessel** that captures the magic of **Wildgate**
(Moonshot Games / Dreamhaven, 2025). The question it puts to the designer, from the brief:

> Look up the game Wildgate and make an artifact lab that can explore concepts for a 4 player multiplayer vessel
> that captures the magic of that game. (Garrett, 2026-10-20)

- **Source (the page is published from it):** `Docs/Studios/CrewOfFourLab.html` (round 4: the 4-seat vessel, one helm and three stabilized gunner seats).
  Rounds 1 (2D) and 2 (3D) are kept as `CrewOfFourLab.round1.html` and `.round2.html`, and their numbers are on the
  page's Archive tab. Open it in Chrome to use a gamepad (the
  claude.ai viewer can block the Gamepad API).
- **Live page:** https://claude.ai/artifact/UaJcpaXzgkza2eHSQCZwmb (private to the owner until shared from its Share
  menu; decisions recorded there land in the shared log, collection `decisions`, that Claude reads at the start of each
  round).
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/CrewOfFourLab.html` (PASS, round 4).
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

## 2. The concepts (rounds 1–2; round 3 replaced them with one vessel, §4a)

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
| Time | **Gate Run** (rounds 1–2; round 3: **Ring Race**, §4) | race | seconds to escape (points: 100 + seconds to spare, else 0.5 per second you held the Artifact) | 1, 1, 2, 2 rival crews; gate opens at 30 / 40 / 50 / 60 s; 14 → 8 crystals; 0 → 3 hazard clusters |
| Space | **Breach** | destruction | fortress prisms destroyed in 90 s | 2 → 5 levels (round 2: each level three orthogonal rings, an armillary sphere; the first at 26 u, inside a Charge-3 skiff charge); 1, 1, 2, 2 rivals |
| Mass | **Anchorage** | creation | your prisms standing at 90 s (trail + forged blocks) | 1, 1, 2, 2 raider crews; 16 → 12 crystals; 0 → 2 hazards |
| Charge | **Broadside** | dogfight | net petal hits (points: your share of the hits) | 1, 1, 2, 2 rivals; 12 → 8 crystals |

Rivals: Easy / Medium / Hard AI crews (aim error 9° / 5° / 2.5°, lead 0 / ½ / full, re-plan 1.8× / 1.25× / 1×,
ward tolerance 3 / 1.5 / 0.5 threats). Your own crew's AI has the same three levels. Every AI writes the same input
objects a person does (`/labmaker` §2.4).

## 4. Round 4 (2026-10-20): stabilized turrets on outriggers, and "4 seat"

**Input this round** (Garrett): "stabilize the gunners guns as best you can. you can place them outside the vessel to do
this. then bring this into unity", and "we can start referring to multiplayer vessels by their seats. this is a 4 seat
vessel. that lets players know it supports four players or brings in 4 AI." The page and this doc now call it **the
4-seat vessel**; the Unity card says "4 seat" (§5).

### How the guns are stabilized (the order matters; each step removes what the one before leaves)

1. **The aim lives in the world, not on the hull.** Each turret keeps its own world-space aim direction (`aimW`). The
   gunner's input turns that direction; the hull's yaw, pitch and roll never reach it. In round 3 the aim was a local
   direction on the hull, so a 80 °/s helm turn swung every gunner's view 80 °/s.
2. **The horizon is held, then eased.** Each turret keeps its own up (`upW`), kept square to the aim. It drifts toward
   the hull's up over `horizonSmooth` (3 s), so a barrel roll does not spin the gunner's picture, but after a long
   inverted run the gunner's "up" ends up matching the ship's.
3. **The mounts sit outside the hull, on outriggers** (`mountOut`): gunner 1 on a dorsal mast (0, 1.8 R, −0.2 R),
   gunners 2 and 3 on side booms (±2.4 R, 0.35 R, −0.5 R), drawn as struts. Outside the hull the turret's view is never
   blocked by its own ship and the launch point is clear of it.
4. **The outriggers ride a lagged frame** (`mountSmooth` 0.35 s): the mount's position follows a copy of the hull's frame
   that eases toward it, so a snap turn swings the turret through a soft arc instead of whipping its eye point round
   the hull at 2.4 R. The aim is unaffected (step 1); only where the eye sits lags.
5. **The AI aims the same way** (it turns `aimW` toward its lead point at the turret rate), so AI and human gunners
   share one turret model.

`turretStab 0, mountOut 0` is round 3's hull-locked turret, kept as the **Hull-locked turrets** control. The new
**View sway °/s** column measures how fast a gunner's view turns *without* the gunner asking: the rotation of the
turret's view (aim plus horizon) minus the rotation the gunner's own input made, averaged over the match for crew 0's
gunner 1. A first version counted the AI gunner's own aiming; it now counts only motion nobody steered.

### Scorecard (I2, Hard rivals, mirror matches, every dial at its round-4 value, 12 seeds)

`runBatch({ game, intensity: 1, diff: 2, seeds: [1..12], seats: true, rookie: true })`; 9–18 s per game. Baked into the
page's Scorecard tab (`ROUND4`).

**Ring Race**

| Preset | Points | ± | Win % | Seat value H/G1/G2/G3 | Sway °/s | Missiles/min | Hit % | Dry % | Omni by missile % | Stops/min |
|---|---|---|---|---|---|---|---|---|---|---|
| Round 4 | 215.09 | 3.08 | 42 | 100/-1/-1/1 | 6.04 | 46 | 66.26 | 44.8 | 89.08 | 4.07 |
| Fast darts | 209.77 | 3.47 | 8 | 100/1/-1/-3 | 7.78 | 40.05 | 65.57 | 56.99 | 82.82 | 4.66 |
| Heavy shells | 202.48 | 17.96 | 58 | 100/-8/-7/-8 | 4.82 | 45.45 | 60.66 | 32.6 | 85.17 | 1.6 |
| Dropped shield | 217.5 | 1.24 | 33 | 100/-1/0/0 | 5.57 | 53.74 | 67.89 | 38.44 | 88.5 | 1.25 |
| Scarce omnis | 215.91 | 1.43 | 42 | 100/0/2/7 | 8.38 | 15.27 | 76.36 | 91.5 | 66.8 | 0.72 |
| Hull-locked turrets (control) | 212.85 | 2.27 | 25 | (control) | 47.3 | 35.96 | 70.54 | 60.16 | 79.92 | 3.12 |
| No shield (control) | 201.74 | 15.19 | 17 | (control) | 5.81 | 43.68 | 63.27 | 43.83 | 92.9 | 0 |
| One gunner (control) | 215.41 | 1.85 | 17 | (control) | 4.38 | 20.67 | 70.22 | 23.47 | 77.75 | 3.06 |

**Breach**

| Preset | Points | ± | Win % | Seat value H/G1/G2/G3 | Sway °/s | Missiles/min | Hit % | Dry % | Omni by missile % | Stops/min |
|---|---|---|---|---|---|---|---|---|---|---|
| Round 4 | 171.25 | 11.64 | 42 | 29/1/3/9 | 4.67 | 54.46 | 73.9 | 43.04 | 84.5 | 0.62 |
| Fast darts | 159 | 15.07 | 42 | 44/-17/1/-6 | 7.29 | 44.15 | 73.49 | 60.48 | 77 | 1.63 |
| Heavy shells | 146.08 | 14.92 | 33 | -7/-9/7/-3 | 2.86 | 49.85 | 63.92 | 33.7 | 86.83 | 0.97 |
| Dropped shield | 167.08 | 13.99 | 33 | 28/0/1/6 | 4.41 | 53.77 | 74.97 | 41.4 | 88.17 | 0.6 |
| Scarce omnis | 165 | 13.08 | 75 | 44/11/14/15 | 8.58 | 10.72 | 82.47 | 91.84 | 47.17 | 0.39 |
| Hull-locked turrets (control) | 194 | 12.4 | 67 | (control) | 54.15 | 52.87 | 76.72 | 39.52 | 90.17 | 0.51 |
| No shield (control) | 183.17 | 11.77 | 58 | (control) | 4.22 | 55 | 73.92 | 37.65 | 88.58 | 0 |
| One gunner (control) | 130.67 | 6.23 | 0 | (control) | 3.92 | 26.96 | 94.98 | 15.92 | 88.33 | 0.4 |

**Anchorage**

| Preset | Points | ± | Win % | Seat value H/G1/G2/G3 | Sway °/s | Missiles/min | Hit % | Dry % | Omni by missile % | Stops/min |
|---|---|---|---|---|---|---|---|---|---|---|
| Round 4 | 90.42 | 15.61 | 67 | 79/61/70/53 | 3.32 | 55.59 | 75.72 | 15.55 | 96.33 | 3.44 |
| Fast darts | 93.42 | 15.04 | 67 | 79/33/72/55 | 4.11 | 68.75 | 81.15 | 12.72 | 98.42 | 7.44 |
| Heavy shells | 83.75 | 17.7 | 58 | 60/19/15/26 | 3.23 | 44.76 | 67.63 | 19.76 | 92.92 | 2.17 |
| Dropped shield | 72.5 | 16.55 | 42 | 63/45/39/26 | 4.26 | 49.93 | 71.78 | 24.75 | 94 | 3.67 |
| Scarce omnis | 98.5 | 7.06 | 58 | 28/0/2/-4 | 8.42 | 11.61 | 85.05 | 87.94 | 58 | 1.28 |
| Hull-locked turrets (control) | 82.83 | 16.13 | 58 | (control) | 52.27 | 56.03 | 73.74 | 15.69 | 97.58 | 3 |
| No shield (control) | 99.17 | 15.26 | 75 | (control) | 3.38 | 58.48 | 75.88 | 13.18 | 97 | 0 |
| One gunner (control) | 9.42 | 1.34 | 0 | (control) | 3.38 | 22.6 | 58.63 | 11.24 | 94.08 | 5.33 |

**Broadside**

| Preset | Points | ± | Win % | Seat value H/G1/G2/G3 | Sway °/s | Missiles/min | Hit % | Dry % | Omni by missile % | Stops/min |
|---|---|---|---|---|---|---|---|---|---|---|
| Round 4 | 46.06 | 7.92 | 50 | 25/13/-1/-8 | 4.88 | 49.98 | 66.77 | 40.31 | 88.17 | 4.72 |
| Fast darts | 48.26 | 6.33 | 42 | 37/2/29/29 | 6.31 | 52.48 | 76.85 | 48.97 | 91.58 | 6.27 |
| Heavy shells | 46.74 | 7.45 | 42 | 29/5/4/-5 | 5.64 | 37.15 | 61.54 | 49.67 | 84.5 | 2.72 |
| Dropped shield | 53.48 | 6.71 | 58 | 13/34/30/22 | 4.84 | 55.09 | 73.99 | 33.32 | 95.83 | 2.78 |
| Scarce omnis | 39.96 | 5.92 | 25 | 60/-30/-25/-44 | 9.63 | 9.22 | 71.54 | 91.79 | 60.58 | 1.83 |
| Hull-locked turrets (control) | 48.21 | 8.27 | 50 | (control) | 53.17 | 48.87 | 64.92 | 40.46 | 85.17 | 4.77 |
| No shield (control) | 49.87 | 6.97 | 50 | (control) | 5.55 | 49.81 | 70.09 | 42.26 | 88.83 | 0 |
| One gunner (control) | 27.53 | 1.9 | 0 | (control) | 5.63 | 22.32 | 58.9 | 30.11 | 88.58 | 5.83 |

### Found

1. **Stabilization removes about 90% of the view's sway**: 3–6 °/s against 47–54 °/s hull-locked, in every game. The
   residue is the eased horizon (step 2) and the lagged mount (step 4), both slow by design.
2. **It costs the AI nothing measurable.** Points with and without it sit within about one standard error in every game
   (Breach 171 ± 12 against 194 ± 12 is the widest gap, under 1.5 SE). The AI never felt sway; the person in the seat
   does. Earlier 3-seed runs showed a big Anchorage gain (126 against 54); 12 seeds erased it, so it was noise.
3. **Every seat still matters in the fights.** One gunner keeps 9 Anchorage prisms against 90 and a 28% Broadside share
   against 46%; in Breach 131 against 171. The race is still the helm's alone (seat value 100 / 0 / 0 / 0 within noise).
4. **The shield's value is not resolved.** Round 3 measured +17 points of Broadside share; round 4's No shield control
   scores within one standard error of Round 4 in every game. Both rounds are within noise of each other, so the
   designer's decision on the shield (§4a) stays open and needs a person at the helm, not more seeds.

### Checked

- `verify_lab.cjs` PASS. Chase and turret screenshots read: the outrigger struts and pods show on the hull; the
  turret's view holds level while the helm turns under it.

## 4a. Round 3 (2026-10-20): one helm, three gunner seats, flown missiles

**Input this round** (Garrett, in substance; the decision log was empty):
- The guns get a first-person camera. The skiff was glitchy, as if it controlled both at once.
- The helm is a two-thumb flyer, with a defensive Mass ability (surround itself in danger prisms except forward) and a
  boost button.
- The vessel's feel is the goal, so the rest of Wildgate (the Artifact) goes.
- "1 helm, 3 guns/skiffs", unified: the vessel always has four players (AI if not people).
- The gun-skiff seats are first-person turrets with a release button. Release flies a fast fly-by-wire missile with one
  thumb; it explodes on contact or after 5 s into a sizable sphere. Flown missiles collect elemental crystals and omni
  crystals; omni crystals add missiles; missiles are spent when the gunner seats fire them.

### The design under test

| Seat | What it does | Controls | From the game |
|---|---|---|---|
| **Helm** | Flies the hull with two thumbs; **Boost** (held, a tank); **Shield**: danger prisms on a shell round the hull, the forward cone open, for 4 s | Left stick W A S D, right stick the arrows (or P L ; ' as in the game); Space boost, E shield; a pad's two sticks, A / RT boost, B / RB shield; on a phone a thumbstick on each half of the view | `DualStickMix` (yaw = eased sum of the sticks' x, pitch = of their y, roll = their y difference, throttle = their x difference, XDiff 0.5 at rest) through `VesselTransformer`'s formulas at the Sparrow's numbers (`YawScaler`/`PitchScaler` 80, `RollScaler` 30, `RotationThrottleScaler` 0.1, speed 10 + 25 × XDiff, ×5 boost); the shell from `DangerHemisphereConfig.asset` (12 rays × 12 blocks, radius 50–70, blocks 10 × 1 × 1); touching a danger prism costs −0.5 on every element for 4 s (`VesselElementalDebuffByDangerPrismEffectSO`) |
| **Gunner 1–3** | A first-person turret. **Release** launches a missile from the hull's shared magazine and hands the gunner its controls: one thumb bends it; it bursts on contact, at 5 s, or on a second Release; it collects crystals it flies through (elemental: a petal; omni: +3 missiles). When it bursts the gunner is back in the turret | Mouse look (click to lock), W A S D, a pad stick or a drag aims; Space / click / A / RT / the on-screen button releases; in flight W A S D, the left stick or a drag | The blast's radius is `AOEExplosion`'s (MaxScale 60 → 30 u); everything else is a lab proposal |

Rivals fly the same vessel. A blast that catches a rival hull takes 2 petals (scored hits); a missile that touches a
rival's danger prism bursts on it and breaks it; blasts break prisms and other missiles.

### What changed

- **Four seats, fixed.** The Core, Scope, ward, walking and Artifact are gone. Presets replace concepts: Round 3,
  Fast darts, Heavy shells, Dropped shield, Scarce omnis, and two controls, No shield and One gunner.
- **The glitch is gone by construction.** Each seat reads its own controller and only its own; the human's sticks never
  reach a body the seat does not own. Checked: holding D while flying a missile turned the missile about 80° in 0.45 s while
  the hull's sticks stayed the AI helm's.
- **Cameras per seat:** the helm keeps the chase camera (`SparrowCameraSettingsSO`); a gunner looks out of its turret
  (first person, FOV 75, lab) and, in flight, from just behind its missile (14 u back, 3 u up, lab).
- **The Time game is now a Ring Race** (rings on a tilted, wavy loop; at I2 and up some rings are walled with neutral
  prisms a blast clears for everyone), replacing the Artifact's Gate Run.
- **Three things the first traces broke, fixed before the numbers below:** crystal slots filled with uncollected
  elemental crystals and the omni crystals vanished (each slot now keeps its kind); AI gunners emptied the shared
  magazine before a human could fire (they now leave one missile per human gunner, `aiReserve`); a Release pressed
  during the 0.6 s reload was lost (it is now held for half a second).
- **The economy is tuned to leave some tension** (a lab proposal): 3 missiles per omni and 40% of crystal slots omni.
  At 2 per omni and 25%, magazines sat empty 69–80% of a match and gunners waited over a minute.

### Scorecard (I2, Hard rivals, mirror matches, every dial at its round-3 value, 6 seeds)

`runBatch({ game, intensity: 1, diff: 2, seeds: [1..6], seats: true, rookie: true })`; 5.1–8.6 s per game. Seat value is
Helm / Gunner 1 / Gunner 2 / Gunner 3. Points: Ring Race 100 + seconds to spare when finished (else 60 × rings passed /
rings); Breach and Anchorage prisms; Broadside your share of petal hits (%).

#### Ring Race (Time race)

| Preset | Points | ± SE | Finished % | Finish s | Seat value % (H/G1/G2/G3) | Rookie % | Missiles/min | Hit % | Fuse-out % | Flight s | Omnis/min | by missile % | Dry % | Shields/min | Stops/min | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 3 | 211.42 | 3.63 | 100 | 38.57 | 100 / -1 / -2 / 6 | 105 | 40.78 | 77.4 | 9.4 | 1.36 | 12.83 | 88.17 | 54.15 | 4.43 | 2.03 | 20.2 | 23.83 / 24.83 |
| Fast darts | 214.19 | 3.7 | 100 | 35.8 | 100 / -1 / -1 / -1 | 101 | 43.5 | 72.63 | 9.67 | 1.07 | 13.51 | 80.6 | 54.18 | 4.83 | 6.58 | 14.03 | 21.17 / 30.33 |
| Heavy shells | 215.65 | 4.09 | 100 | 34.35 | 100 / 0 / 0 / 0 | 102 | 35.04 | 73.98 | 15.52 | 1.56 | 9.64 | 88.83 | 59.33 | 3.88 | 2.68 | 20.95 | 20.67 / 22.67 |
| Dropped shield | 207.13 | 8.17 | 100 | 42.87 | 100 / -6 / -3 / -4 | 103 | 33.46 | 58.78 | 14.53 | 1.53 | 9.78 | 78.17 | 64.35 | 5.05 | 1.58 | 33.85 | 15.33 / 31.17 |
| Scarce omnis | 187.5 | 30.31 | 83 | 32.2 | 100 / 0 / -15 / -13 | 100 | 11.49 | 88.47 | 9.45 | 1.25 | 3.44 | 71.6 | 92.02 | 2.24 | 0.95 | 51.38 | 8 / 10.67 |
| No shield (control) | 216.1 | 4.34 | 100 | 33.92 | — | — | 40.25 | 59.6 | 17.5 | 1.68 | 13.51 | 92.67 | 45.6 | 0 | 0 | 20.13 | 20.83 / 36.33 |
| One gunner (control) | 214.91 | 3.4 | 100 | 35.08 | — | — | 14.55 | 67.5 | 10.8 | 1.26 | 4.94 | 80.25 | 52 | 5.61 | 4.44 | 25.65 | 7.33 / 32.67 |

#### Breach (Space destruction)

| Preset | Points | ± SE | Score | Win % | Seat value % (H/G1/G2/G3) | Rookie % | Missiles/min | Hit % | Fuse-out % | Flight s | Omnis/min | by missile % | Dry % | Shields/min | Stops/min | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 3 | 194.83 | 18.07 | 194.83 | 67 | 46 / 25 / 14 / 19 | 79 | 51.15 | 76.13 | 5 | 1.69 | 13.84 | 90.33 | 40.15 | 5.2 | 1.01 | 8.47 | 0 / 0.33 |
| Fast darts | 166.67 | 8.5 | 166.67 | 33 | 55 / -9 / -3 / -13 | 89 | 40.06 | 76.48 | 5.25 | 1.13 | 11.96 | 75 | 64.1 | 4.08 | 0.62 | 18.77 | 3 / 1.67 |
| Heavy shells | 175.33 | 21.12 | 175.33 | 33 | 23 / 13 / 9 / 27 | 78 | 51.14 | 64.93 | 10.65 | 2.39 | 13.07 | 91.17 | 24.33 | 5.56 | 0.46 | 2.35 | 0 / 0.33 |
| Dropped shield | 199.17 | 21.04 | 199.17 | 67 | 47 / 28 / 19 / 17 | 78 | 54.13 | 76.08 | 5.9 | 1.72 | 14.57 | 92.33 | 36.68 | 5.63 | 0 | 6.67 | 0.67 / 0.33 |
| Scarce omnis | 157.33 | 18.74 | 157.33 | 83 | 32 / 24 / 31 / 7 | 78 | 10.66 | 85.55 | 6.83 | 1.64 | 6.66 | 56.17 | 92.68 | 2.78 | 1.22 | 31.08 | 0.67 / 1.67 |
| No shield (control) | 168 | 28.47 | 168 | 33 | — | — | 45.78 | 70.45 | 4.8 | 1.49 | 12.22 | 81 | 50.43 | 0 | 0 | 10.7 | 2.33 / 0.67 |
| One gunner (control) | 147.17 | 21.63 | 147.17 | 33 | — | — | 28.89 | 95.65 | 1.9 | 1.16 | 8.28 | 80.5 | 11.62 | 6.02 | 2.98 | 3.17 | 0 / 0.33 |

#### Anchorage (Mass creation)

| Preset | Points | ± SE | Score | Win % | Seat value % (H/G1/G2/G3) | Rookie % | Missiles/min | Hit % | Fuse-out % | Flight s | Omnis/min | by missile % | Dry % | Shields/min | Stops/min | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 3 | 86.67 | 24.02 | 86.67 | 67 | 87 / 62 / 55 / 47 | 67 | 58.31 | 73.75 | 6.32 | 1.99 | 18.66 | 97.33 | 15.07 | 4.55 | 3.33 | 8.22 | 39 / 28.67 |
| Fast darts | 109.33 | 20.08 | 109.33 | 83 | 92 / 86 / 69 / 43 | 20 | 72.42 | 84.77 | 4.42 | 1.53 | 23.55 | 99.5 | 9.57 | 5.22 | 4.89 | 6.78 | 50.67 / 27.17 |
| Heavy shells | 96.17 | 26.49 | 96.17 | 67 | 71 / 62 / 80 / 83 | 57 | 47.76 | 62.27 | 19.97 | 2.54 | 15.44 | 90.67 | 15.65 | 4.44 | 2.33 | 5.58 | 32.17 / 21.33 |
| Dropped shield | 115.5 | 17.92 | 115.5 | 83 | 47 / 63 / 45 / 45 | 47 | 57.98 | 76.38 | 7.63 | 2.27 | 18.88 | 99.5 | 5.18 | 5.11 | 2.78 | 1.9 | 53 / 32.17 |
| Scarce omnis | 103.5 | 7.19 | 103.5 | 50 | 31 / -2 / 5 / -4 | 89 | 12.88 | 87.22 | 7.03 | 2.19 | 8.88 | 58 | 86.18 | 2.78 | 2.11 | 22.85 | 14.33 / 9 |
| No shield (control) | 72.33 | 27.14 | 72.33 | 50 | — | — | 53.09 | 65.32 | 12.62 | 2.23 | 16.99 | 94.33 | 15.35 | 0 | 0 | 5.03 | 36.67 / 37.33 |
| One gunner (control) | 19.33 | 4.33 | 19.33 | 0 | — | — | 24.32 | 61.47 | 7.22 | 1.61 | 7.77 | 89.67 | 9.37 | 6.33 | 4.66 | 6.82 | 13 / 46.83 |

#### Broadside (Charge dogfight)

| Preset | Points | ± SE | Score | Win % | Seat value % (H/G1/G2/G3) | Rookie % | Missiles/min | Hit % | Fuse-out % | Flight s | Omnis/min | by missile % | Dry % | Shields/min | Stops/min | Longest wait s | Hits dealt/taken |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 3 | 60.7 | 9.75 | 10.67 | 67 | 27 / 21 / 20 / -1 | 29 | 58.87 | 78.88 | 4.98 | 1.63 | 18.66 | 94.17 | 27.57 | 4.44 | 4.44 | 12.37 | 51.17 / 40.5 |
| Fast darts | 59.4 | 11.43 | 13.17 | 67 | 35 / 46 / 35 / 10 | 51 | 67.64 | 80.98 | 6.18 | 1.19 | 21.66 | 95.33 | 30.07 | 3.78 | 6.66 | 17.22 | 41.17 / 28 |
| Heavy shells | 70.39 | 8.47 | 22.5 | 83 | 64 / 6 / 54 / 20 | 53 | 49.09 | 78.98 | 10.88 | 2.11 | 15.33 | 91.33 | 29.07 | 3.33 | 1.45 | 14.3 | 45.17 / 22.67 |
| Dropped shield | 57.76 | 7.7 | 10.83 | 67 | 27 / 6 / 33 / 18 | 42 | 59.42 | 78.98 | 5.83 | 1.62 | 18.88 | 92.83 | 28.05 | 4.33 | 2.44 | 10.97 | 52.17 / 41.33 |
| Scarce omnis | 60.47 | 6.01 | 7.67 | 67 | 66 / 26 / 23 / 29 | 55 | 11.66 | 82.9 | 3.47 | 1.87 | 7.66 | 52.5 | 88.13 | 3.33 | 2.56 | 25.47 | 23 / 15.33 |
| No shield (control) | 43.73 | 7.32 | -8.67 | 33 | — | — | 40.65 | 61.5 | 9.5 | 1.66 | 12.44 | 87.83 | 47.88 | 0 | 0 | 22.9 | 49.5 / 58.17 |
| One gunner (control) | 24.98 | 2.37 | -33.17 | 0 | — | — | 22.66 | 54.9 | 7.78 | 1.27 | 6.55 | 91.17 | 28.75 | 6.33 | 6.22 | 17.87 | 18.33 / 51.5 |

### Found

1. **Every seat matters in the fights, none of the gunners matter in the race.** In Anchorage the four seats are worth
   87 / 62 / 55 / 47% and a lone gunner keeps 19 prisms against 87; in Broadside 27 / 21 / 20 / −1% and a lone gunner
   takes a 25% share against 61%. In the Ring Race the helm is worth 100% and each gunner −15 to 6%: a hull flies
   through a wall's prisms at the cost of a 0.4 s slow, so clearing walls buys nothing. **Design hole:** walls (or the
   race) need a reason for gunners.
2. **The shield earns its button in the fights.** Without it the Broadside share falls from 61% to 44% and Anchorage
   from 87 to 72 prisms. Helms raise it 4–5 times a minute and it stops 1–4 rival missiles a minute. Dropping it in
   place (instead of riding with the hull) scores the same within the noise.
3. **Missiles do the collecting.** 88–97% of omni crystals are taken by a flown missile, not the hull, so the gunners
   own the ammo economy, as the design intends. Magazines sit empty 15–54% of a match (Anchorage least, the race most).
   Scarce omnis (20%, one missile each) empties them 86–93% of the time and gunners wait 22–51 s.
4. **Skill matters most in the dogfight.** An Easy crew keeps 29% of a Hard crew's Broadside points against 67–105%
   elsewhere.
5. **Missile feel:** a missile flies 1.4–2.0 s on average; 5–10% fuse out (Heavy shells 11–20%); 74–79% of missiles hit
   something. Fast darts edge Round 3 in Anchorage (109 against 87) and lose in Breach (167 against 195); Heavy shells
   lead Broadside (70%); every difference but the controls sits within about two standard errors.

### Checked

- `verify_lab.cjs` PASS; desktop and iPhone 13 screenshots read (helm chase view with the shield up, turret crosshair,
  missile view with its fuse bar, the crew panel and radar).
- A scripted person: at the helm, W + Up wrote YSum −1.0 (full pitch), A + Right wrote XDiff 1 (full speed), E raised
  the shield; at a gunner seat the camera was the turret's, Release launched (or burst an inherited missile), D turned
  the missile and not the hull; no console or page errors.

### Decision needed (designer), round 3

- **The ammo economy:** how many missiles an omni gives and how many omni crystals exist (3 and 40% proposed), and
  whether the magazine is shared by the three gunners (as built) or per seat.
- **Should the AI keep missiles for human gunners** (`aiReserve` 1)?
- **The shield:** rides with the hull (as built) or is dropped in place; its 50–70 u radius (the game asset's) is far
  outside the hull, so a rival flying near you touches it.
- **What gives gunners a job in the race.**
- **What a blast takes from a hull** (2 petals proposed).

## 4b. Round 2 (2026-10-20): 3D with the game's chase camera (superseded)

> **Superseded by the designer (2026-10-20)** by round 3 ("1 helm, 3 guns/skiffs ... we don't need other parts of
> wildgate gameplay like the artifact"). The page is kept as `Docs/Studios/CrewOfFourLab.round2.html`.

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

## 4c. Round 1 (2026-10-20): 2D top-down (superseded)

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

## 5. Graduation: the Orca (2026-10-20)

Garrett: "stabilize the gunners guns as best you can... then bring this into unity". Round 4 graduated as the **Orca**,
`VesselClassType.Orca = 20`, card **Orca · 4 seat**. It is in the Toybox Vessel Changer, which is what "bring it to
Unity" means (`/labmaker` §7). Design and files: `Assets/_Scripts/Controller/Vessel/Crew/ORCA_CREW.md`.

**The crew rules are ported line for line** into `OrcaCrewCore.cs` (no engine): the stabilized turret, the held
horizon, the lagged mounts, the flown missile, the magazine, the shield's shape and the AI's stick.
- The page exposes the functions it flies as `window.__lab.parity`. Round 4 lifted `holdHorizon`, `shieldDirs` and
  `flyMissile` out of their inline blocks; a 12-seed batch is byte-identical before and after.
- `node Tools/Build/orca_crew_lab/parity.cjs` compiles the shipped C# and diffs it against those functions step by
  step (600 turret steps, 300 missile steps, 60 AI sticks, 144 shield directions).
- It also checks `OrcaCrewConfig.asset` against SHIPPED, key by key, and planted controls must be caught.

**The numbers belong to this lab.** The asset was seeded once from SHIPPED; tune here and re-seed.
- The lab's element factors (an effect at level 15 is 1 + k) are `ElementalFloat` channels in the game. Each channel
  runs from rest to rest × (1 + 2k/3) at level 10, which is the same line.
- The hull takes the lab's 0.85 × the Sparrow's speed and 0.55 × its turn rates.

**What the game does differently** (ORCA_CREW.md):
- The AI helm is the hull's own `AIPilot`.
- The burst is the real `AOEExplosion`, and its vessel effect decides what a caught hull loses.
- Missiles collect crystals through the game's own skimmer and sweep paths.
- The crew runs on one machine until it is networked.
