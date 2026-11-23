# Crew of Four Lab

A `/labmaker` browser studio for a **four-seat multiplayer vessel** that captures the magic of **Wildgate**
(Moonshot Games / Dreamhaven, 2025). The question it puts to the designer, from the brief:

> Look up the game Wildgate and make an artifact lab that can explore concepts for a 4 player multiplayer vessel
> that captures the magic of that game. (Garrett, 2026-10-20)

- **Source (the page is published from it):** `Docs/Studios/CrewOfFourLab.html` (round 7: the 4-seat vessel; every shot flies down its aim; every seat an element; a gun and missiles on every gunner seat, a sniper with the Serpent's pip scope always open, a fast gun that writes prisms like a pen, a one-at-a-time bomb; a shield carried super-shielded, then dropped as danger prisms).
  Rounds 1 (2D) and 2 (3D) are kept as `CrewOfFourLab.round1.html` and `.round2.html`, and their numbers are on the
  page's Archive tab. Open it in Chrome to use a gamepad (the
  claude.ai viewer can block the Gamepad API).
- **Live page:** https://claude.ai/artifact/UaJcpaXzgkza2eHSQCZwmb (private to the owner until shared from its Share
  menu; decisions recorded there land in the shared log, collection `decisions`, that Claude reads at the start of each
  round).
- **In the Vessel Studio** (round 9): the Orca bay of https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa opens the hub
  copy `Docs/Studios/VesselStudio/orca.html`, made from this page by `python3 .claude/skills/vessel-studio/copy_orca.py`
  (`--check` fails when the copy is stale). Its decisions carry `vessel: 'orca'`, so they stay apart from the other
  studios' in that artifact's shared log.
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/CrewOfFourLab.html` (PASS, round 9; the hub copy
  passes too).
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

## 2. The concepts (rounds 1–2; round 3 replaced them with one vessel, §4e)

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

## Round 9 (2026-10-27): play styles to choose between

**Input this round** (Yash, 2026-10-27): "In the orca artifact, I will choose between the play styles, while testing
them, so make them in the artifact for me to test, and show to garrett, We going with Orca only, just we can have
multiple choice for abilities and features."

**What changed.** A **Play styles** tab (second, after Presets) with one dropdown per ability and the numbers of every
option. **Every default is round 7's**, so the page, the scorecard and the C# port (`OrcaCrewConfig`) are unchanged
until someone picks another option. The options come from the calm vessels (`Docs/Studios/CONCEPT_FLEET.md` §12):

| Ability (seat, element) | Options (first = shipped) | What the new option does |
|---|---|---|
| Boost (Helm, Time, RT) | **Tank** / Breath / Tide | **Breath** (the Moon Jelly): hold to breathe in (the hull slows to ×0.6 and swells), let go to surge (0.6–2.4 s by how full the breath was); breaths 2 s or longer, each within 25% of the last, chain a calm (+15% surge each, up to 4). **Tide** (the Sea Turtle): three currents loop through the arena (in the Ring Race the first follows the rings); in one the hull is carried along its heading at up to 70 u/s, and holding boost tucks in (×2 carry, ×0.5 turn). |
| Shield (Helm, Mass, LT / B / E) | **Shield** / Dock | **Dock** (the Seahorse): press to anchor to the nearest block within 45 u: the hull holds still, each gunner seat reloads a missile every 4 s, the sniper cools ×3 faster, and a garden of trail prisms grows round you; press again (or after 8 s) to dart off with 1 s of boost. 3 s between docks. No shield while it is picked. |
| Missiles (each gunner, A / F / right mouse) | **Fly-by-wire** / Seeker | **Seeker**: the missile flies itself to what the turret aimed at (a hull inside 25° of the aim, else the aim point), so the gunner keeps the turret and the gun; a second press bursts it early. One missile in flight per seat. |

Presets: **Calm crew** (all three new options), **Breathing helm**, **Tide helm**, **Docking helm**, **Seeker missiles**,
all on the Presets tab and in **Score every preset**. The scorecard gains breaths/min, calm %, docks/min, docked %,
missiles from docking, garden prisms, seeker locks, time in a current and time tucked. The Decisions tab gains a
**Play styles** topic pre-filled with the current pick.

**Measured** (12 seeds, I2, Hard rivals, AI in every seat; points, ± SE; the lab's own `runBatch`):

| Preset | Ring Race | Breach | Anchorage | Broadside |
|---|---|---|---|---|
| Round 7 (shipped) | 218.88 ± 2.69 | 182.75 ± 11.39 | 71.92 ± 14.55 | 50.13 ± 1.51 |
| Calm crew | 220.54 ± 2.54 | 171.08 ± 8.59 | 84.00 ± 11.77 | 50.89 ± 0.91 |
| Breathing helm | 219.20 ± 1.89 | = round 7 | = round 7 | 50.09 ± 1.34 |
| Tide helm | 203.65 ± 1.84 | 183.25 ± 10.39 | 80.00 ± 15.00 | 47.45 ± 0.99 |
| Docking helm | 222.73 ± 2.36 | 187.50 ± 10.34 | 60.50 ± 13.68 | 50.54 ± 1.14 |
| Seeker missiles | 220.22 ± 2.19 | 179.83 ± 11.32 | 83.08 ± 11.67 | 51.52 ± 0.81 |

**Checked.**
- The shipped preset scores exactly as before on all four games (the round 7 row equals the page's baked table,
  Ring Race 218.88 ± 2.69), so the options are opt-in.
- `verify_lab.cjs` PASS on the page and on the hub copy; `parity_gate.py` ok (`--self-test` ok).
- Screenshots read: the Tide helm in a current in the Ring Race, a docked hull with its tether in Anchorage, the Play
  styles tab, the phone layout.
- **Not run here:** the C# parity gate (`Tools/Build/orca_crew_lab/parity.cjs`) needs .NET 8, which this container
  lacks. Round 9 does not touch the pure functions it compares or any asset key it reads.

**Found.**
- Most choices sit inside the noise band against round 7: they change how the Orca **feels**, not how strongly it
  plays. That is what was asked for (choices to test), and the human decides between them by flying.
- **Tide helm is slower in the Ring Race** (finish 46 s against 31): it rides a current 66% of the time but tucks
  only 27%, and the loop does not pass through every ring. Its Broadside dips too (47.45).
- **Breathing helm is not used by the AI in Breach and Anchorage**, because the shipped helm AI does not boost there;
  those rows equal round 7 and say nothing about the breath.
- **Docking helm** costs Anchorage (60.50 against 71.92): a docked hull cannot hold the anchor point. In the Ring Race
  the AI never docks (no shield either), which is why it finishes faster.
- **Seeker missiles** hit less (about 25% against 64% in Broadside) and fuse out about half the time, but the gunner
  keeps firing the gun meanwhile; the points come out level.
- **None of the options is in the game yet**: `OrcaCrewConfig` has none of them, so a pick needs a port first.

**Decisions needed (Yash with Garrett):** which option each ability keeps (one, or a choice for the player), and
whether a picked option replaces the round 7 one in the game.

## 4. Round 8 (2026-10-21): rockets with a lit plume, and a bomb that lights what it will break

**Input this round** (Garrett, after flying round 7 in Unity):
- "increase the fidelity on the fly by wire missiles. they feel cheap. the bomb should not have a ring that says burst.
  it should use a lit state to show what it will destroy. always go for the 3d in world solution over the 2d flat text
  solution."

The last sentence is now a LOCKED rule for every surface (`Docs/claude/SHADERS_AND_PERFORMANCE.md`, "In the world, not
on the screen"). No number that decides play changed: the scorecard is round 7's (`ROUND7` on the page).

### What changed

- **A flown missile is a rocket** (`MLOOK`, `missileView`). Round 7 drew a six-sided cone. Now each missile has a body,
  an ogive nose, a band in the crew's colour, four fins, a nozzle, and a lit, faceted motor flame with a white core.
  The flame ignites over 0.15 s with a bright pulse, then flickers; our crew's missiles also carry a point light. The
  body **banks into the turn** its gunner's stick asks for (45° at full yaw, eased over 0.12 s), so the thumb's input
  shows on the missile. In Unity the body is the Sparrow's skyburst rocket (`OrcaMissileVisual`).
- **The trail is a plume, not a ribbon.** A lit, faceted six-sided tube runs through the path: nothing at the nozzle,
  widest (1.4 u) half way through its 3 s life, then nothing again. It keeps tapering after a burst. A first try drew an
  additive ribbon and a TrailRenderer, which the LOCKED "nothing in the world looks flat" rule names as wrong; the plume
  shape also keeps the missile's chase camera out of its own trail.
- **The bomb's crosshair is a chevron and two ticks**, dimmed while a bomb is out. The dashed blast ring and the
  "BURST" ring are gone.
- **What the live bomb will break is lit** (`bombLight`). While our person holds the bomb seat and a bomb is out, every
  prism inside its blast that is not ours is drawn glowing hot in its own colour, fading in over a quarter second. Our
  own prisms stay unlit: the blast spares them. An AI's bomb lights nothing. In Unity this is PrismLit's light bank: a
  sphere gated to each domain the blast breaks (`OrcaCrewView.LightBomb`).

### Checked

- `verify_lab.cjs` PASS; `parity.cjs` PASS (the asset's new look fields are game-side and not compared).
- Looked at: a missile from three sides (body, fins, flame, plume) and from its chase camera, ignition and bank; the
  bomb seat's lit prisms against the same frame unlit, found by a scripted throw-and-search through `__lab`.
  `__lab.still(eye, look)` renders any viewpoint for such checks.

### Not modelled

- PrismLit's own look: the page approximates a lit prism as its colour lifted and glowing. The game adds the gunner's
  light colour pulled toward white.
- Rival hulls inside the blast are not lit, in the page or the game: PrismLit lights prisms only.

## 4a. Round 7 (2026-10-21): every shot flies down the aim, and the sniper's pip scope

**Input this round** (Garrett, after flying round 6 in Unity):
- "the sniper should always have the pip scope open like the serpent does when holding the left trigger, not have the
  whole camera zoomed in. the fast gun and the rocket both had models and crosshairs that were not shooting where they
  were aiming or where the trajectory head. the vessel inheritance is challenging to deal with but we need to make all of
  these things properly coordinated to make it easier on players."

### What changed

- **Shots fly down the aim** (`shotInherit`, `shotVel`). Round 6 launched every round and bomb at `aim × speed + the
  hull's whole velocity`. With the Orca at 50–175 u/s and rounds at 135, a shot slewed far off the barrel and the
  crosshair, in the direction the hull was flying. A shot now keeps only the part of the hull's velocity that runs
  **along its aim**: it flies exactly down the aimed line, a forward shot still outruns the hull, and a shot aimed
  astern never goes slower than its own speed. The **Round 6 shots** control keeps the old rule.
- **The sniper looks over its turret** (`sniperCamBack` 12, `sniperCamUp` 6) like the other gunners, at the normal
  field of view. The **pip scope** is always open in the upper left, at `sniperFov` 22. It is the Serpent's eyepiece
  (`SniperScopeOverlay`): it looks from the barrel tip down the aim, with the cone at its true size, posts, a dot and
  the recharge ring. The same cone reticle sits over the main view at the shot's point.
- **In Unity, the model, the crosshair and the shot share one line** (the lab has no model): each turret is posed at the
  moment it fires, rounds, bombs and missiles leave the barrel tips and missile tubes, the crosshair is projected from
  the barrel tip, and the gunners fire after the hull has moved that frame (`ORCA_CREW.md`, round 7).

### Scorecard (I2, Hard rivals, mirror matches, every dial at its round-7 value, 12 seeds)

Baked into the page as `ROUND7`; the full tables are on the page's Scorecard tab.

| Game | Round 7 points | Round 6 shots (control) | Gun hull hits, R7 / R6 shots | Gun kills/min, R7 / R6 shots | Seat value H/S/F/B |
|---|---|---|---|---|---|
| Ring Race | 218.88 ± 2.69 | 218.99 ± 2.57 | 25.67 / 11.25 | 31.73 / 21.67 | 100/0/4/6 |
| Breach | 182.75 ± 11.39 | 193.42 ± 11.94 | 0 / 0.5 | 115.23 / 71.22 | -17/15/7/40 |
| Anchorage | 71.92 ± 14.55 | 73 ± 15.56 | 39.33 / 24.92 | 21.05 / 14.72 | 70/76/75/58 |
| Broadside | 50.13 ± 1.51 | 48.7 ± 1.49 | 63.5 / 38.42 | 21.16 / 18.6 | 4/3/10/9 |

### Found

1. **Aiming true makes the guns land.** Gun hits on rival hulls rise in every game (Broadside 64 against 38, the Ring
   Race 26 against 11), and the guns kill 30–60% more prisms (Breach 115 a minute against 71). The AI gunners aim as
   before; their shots used to slew off the line.
2. **Points move within noise** in mirror matches, as with every gun change so far: both crews gain the same.
3. **The bomb seat is still worth the most in Breach** (40%); Anchorage stays where round 6 left it (72 ± 15).

### Checked

- `verify_lab.cjs` PASS; the sniper's pip scope and over-the-turret view, and the fast gun's rounds streaming to the
  crosshair, looked at in seat screenshots.
- `parity.cjs` PASS with 40 shot velocities (C# `OrcaCrewCore.ShotVelocity` against the page's `shotVel`) and a planted
  `shotInherit` 1 -> 0 control caught.

## 4b. Round 6 (2026-10-21): every seat an element, a scoped sniper, a pen, and bespoke crosshairs

**Input this round** (Garrett):
- "the space ability icon should display the number of missiles in the current seat. the sniper should be the space seat
  and the space ability should show the snipers cooldown. the scope should always be out for the sniper. the bomb seat is
  the space seat it never goes on cooldown it can just only have one bomb at a time so it must blow up a bomb to fire
  another. the ability icon for the seat lights up for the player in that seat. the fast gun is the mass seat it should
  shoot prisms like a sparrow does in when stopped but less than a sparrow does, and with a clean precision of
  calligraphy. also the camera is in a terrible position for that gunner and all gunners need crosshairs. that are
  bespoke to their weapon. also it is time use the vessel modeling tools and really step up the vessel"

**How the input was read.** The sniper is the Space seat and the fast gun the Mass seat, as written. "The bomb seat is the
space seat" cannot also hold (two seats on one element), so the bomb is the **Charge** seat, the one element left, and
the helm keeps **Time** (its boost). Seat s owns element s, in the lab's order Time, Space, Mass, Charge. "The space
ability icon should display the number of missiles in the current seat": each gunner seat's icon shows **its own
seat's** missiles, so the Space icon shows the sniper's; the occupant's icon is the lit one.

### What changed

- **Seats are elements, and the HUD shows it.** A four-icon ability row (Time, Space, Mass, Charge), one icon per seat:
  - the person's seat's icon is **lit**;
  - each icon's veil is its seat's recharge: the boost tank, the sniper's 12 s, the fast gun's heat; the bomb has none;
  - each gunner icon carries its seat's missiles as a number.
- **The sniper is always scoped**: `sniperFov` 22, the Serpent's full zoom (`SniperScopeAction`).
- **The bomb has no cooldown, one at a time.** A press throws it and the next press bursts it. It also bursts on a rival
  hull, and `bombLife` (now 10 s) is only a safety. The AI bursts its own bomb (`aiBurst`): when a rival hull is inside
  the blast, or the bomb has reached (half the blast radius) or passed its target.
- **The fast gun writes prisms like a pen** (`penLay`, `penFlush`, `penLanding`):
  - at most `rapidPenRate` 10 rounds a second are pen rounds, a third of the rounds; the Sparrow's turret makes every
    round a prism;
  - a pen round leaves a prism where it reaches the paper, `rapidPenReach` 120 u out;
  - the prisms are laid as **strokes**: one every `rapidPenSpacing` (5 u, the Sparrow turret prism's length), each
    along the stroke; a stroke's first landing waits for the second; a landing nearer than the spacing lays nothing; a
    quick sweep fills up to `rapidPenFill` 2 prisms per landing; a longer jump or a `rapidPenGap` 0.35 s pause lifts the
    pen, and a lone landing becomes a dot at the nib's 45°;
  - `rapidPenCap` 150 prisms per hull, the oldest first. Pen prisms do not count toward Anchorage (only the trail does).
- **The paper was 387 u first, and it drew sticks.** At the round's full reach a turret sweep moved a landing ~100 u
  between pen rounds, so every landing started a new stroke. At 120 u a scripted human sweep (34°/s for 3 s) lays one
  stroke of 20 prisms, every gap exactly 5.0 u.
- **Over-the-turret views** for the fast gun (14 u back, 8 up) and the bomb (22 back, 10 up), looking at the gun's line
  `gunCamAim` 180 u out. Before, the fast gun's eye sat at its muzzle and its own rounds filled the view. Our own turret
  now shows in those views; only the scoped sniper hides its own.
- **Bespoke crosshairs**, drawn where the gun's line meets the view: a scope (dark surround, wires with mil ticks, the
  cone at its true size, the recharge as a ring); a pen nib (dot, 45° nib stroke, a lime tip while the next round is a
  pen round, the heat as an arc); a bomb sight (a dashed ring the size of the blast at the aim; with a bomb out, the
  ring round the live bomb, pulsing, and BURST).
- **Seat names** are Helm, Sniper, Fast gun, Bomb. A **No pen** control is new.

### Scorecard (I2, Hard rivals, mirror matches, every dial at its round-6 value, 12 seeds)

`runBatch({ game, intensity: 1, diff: 2, seeds: [1..12], seats: true, rookie: true })`; 13–43 s per game. Baked into
the page as `ROUND6`. Seat values are Helm / Sniper / Fast gun / Bomb.

**Ring Race**

| Preset | Points | ± | Win % | Seat value H/S/F/B | Gun shots/min | Gun kills/min | Gun hull hits | Pen prisms/min | Missiles/min | Dry % | Stops/min |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 6 | 218.99 | 2.57 | 50 | 100/-1/1/6 | 680.61 | 21.67 | 11.25 | 187.16 | 40.47 | 46.3 | 76.84 |
| No pen (control) | 220.35 | 2.35 | 58 | (control) | 711.9 | 19.12 | 12.92 | 0 | 43.43 | 43.46 | 61.88 |
| Fast darts | 220.7 | 2.57 | 58 | 100/1/3/6 | 653.75 | 25.45 | 15.83 | 183.92 | 47.98 | 42.04 | 47.63 |
| Heavy shells | 221.43 | 2.56 | 58 | 100/3/4/5 | 596.68 | 27.03 | 11.25 | 178.1 | 35.17 | 46.02 | 57.06 |
| Long linger | 218.95 | 2.59 | 50 | 100/-1/1/5 | 683.16 | 21.74 | 11.25 | 187.84 | 40.7 | 45.41 | 76.39 |
| Scarce omnis | 223.69 | 2.24 | 50 | 100/2/3/7 | 734.36 | 23.24 | 19.08 | 198.36 | 32.77 | 54.43 | 49.37 |
| Hull-locked turrets (control) | 220.96 | 2.5 | 67 | (control) | 673.19 | 28.72 | 14.92 | 179.64 | 35.08 | 49.43 | 81.99 |
| Round 4 shield (control) | 223.25 | 2.44 | 75 | (control) | 707.52 | 28.24 | 18.75 | 191.51 | 39.22 | 44.2 | 13.07 |
| No guns (control) | 209.66 | 1.96 | 0 | (control) | 0 | 0 | 0 | 0 | 22.2 | 79.9 | 4.19 |
| No shield (control) | 220.96 | 2.83 | 50 | (control) | 664.6 | 24.15 | 21.42 | 183.32 | 42.54 | 40.44 | 0 |
| One gunner (control) | 203.54 | 1.71 | 0 | (control) | 2.12 | 3.15 | 1.25 | 0 | 8.11 | 25.36 | 58.82 |

**Breach**

| Preset | Points | ± | Win % | Seat value H/S/F/B | Gun shots/min | Gun kills/min | Gun hull hits | Pen prisms/min | Missiles/min | Dry % | Stops/min |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 6 | 193.42 | 11.94 | 75 | -14/18/7/42 | 967.03 | 71.22 | 0.5 | 164.88 | 43.21 | 53.43 | 79.56 |
| No pen (control) | 193.42 | 11.94 | 75 | (control) | 967.03 | 64.57 | 0.5 | 0 | 43.21 | 53.43 | 80.17 |
| Fast darts | 205.83 | 11.68 | 75 | -24/9/9/52 | 1003.49 | 68.36 | 0 | 165.39 | 39.08 | 63.42 | 62.72 |
| Heavy shells | 189.58 | 9.52 | 67 | -18/11/20/26 | 1054.59 | 82.1 | 0.33 | 195.98 | 44.18 | 45.07 | 83.92 |
| Long linger | 193.42 | 11.94 | 75 | -14/18/7/42 | 967.03 | 71.22 | 0.5 | 164.88 | 43.21 | 53.43 | 79.56 |
| Scarce omnis | 178.33 | 12.52 | 42 | -24/19/5/42 | 1007.96 | 71.78 | 0.08 | 168.1 | 33.21 | 66.58 | 65.38 |
| Hull-locked turrets (control) | 186.17 | 13.94 | 58 | (control) | 943.01 | 73.54 | 0.17 | 172.17 | 41.79 | 52.44 | 68.26 |
| Round 4 shield (control) | 201.5 | 9.82 | 83 | (control) | 960.68 | 91.89 | 0.17 | 161.53 | 45.28 | 52.13 | 25.34 |
| No guns (control) | 182.08 | 17.08 | 58 | (control) | 0 | 0 | 0 | 0 | 33.04 | 78.97 | 1.56 |
| No shield (control) | 190.58 | 12.38 | 67 | (control) | 954.78 | 78.66 | 0 | 165.89 | 45.71 | 50.74 | 0 |
| One gunner (control) | 72.58 | 7.49 | 0 | (control) | 4.62 | 21.52 | 0 | 0 | 13.02 | 23.67 | 55.34 |

**Anchorage**

| Preset | Points | ± | Win % | Seat value H/S/F/B | Gun shots/min | Gun kills/min | Gun hull hits | Pen prisms/min | Missiles/min | Dry % | Stops/min |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 6 | 73 | 15.56 | 50 | 43/78/63/70 | 472.88 | 14.72 | 24.92 | 104.96 | 49.7 | 39.83 | 59.42 |
| No pen (control) | 65.08 | 15.15 | 50 | (control) | 433.23 | 10.61 | 16.75 | 0 | 50.7 | 37.8 | 43.48 |
| Fast darts | 75.42 | 14.38 | 50 | 57/63/67/69 | 531.86 | 13.61 | 23.92 | 106.63 | 56.53 | 43.76 | 52.54 |
| Heavy shells | 89.33 | 16.35 | 75 | 54/51/58/80 | 443.28 | 10.33 | 21.67 | 102.13 | 44.04 | 38.9 | 46.26 |
| Long linger | 78 | 17.22 | 58 | 53/66/79/61 | 419.74 | 21.55 | 10.83 | 92.47 | 53.59 | 36.07 | 53.42 |
| Scarce omnis | 63.58 | 14.31 | 42 | 12/10/34/63 | 784.54 | 21.66 | 25.08 | 153.66 | 23.77 | 78.11 | 33.38 |
| Hull-locked turrets (control) | 98.42 | 13.71 | 75 | (control) | 408.35 | 11.72 | 15.33 | 86.25 | 52.76 | 32.06 | 40.93 |
| Round 4 shield (control) | 56.67 | 13.57 | 58 | (control) | 442.78 | 13.27 | 14.83 | 98.85 | 50.43 | 41.58 | 9.11 |
| No guns (control) | 91.58 | 15.52 | 58 | (control) | 0 | 0 | 0 | 0 | 54.26 | 38.43 | 4.88 |
| No shield (control) | 79 | 15.35 | 67 | (control) | 458.05 | 7.16 | 7.33 | 91.8 | 53.2 | 38.18 | 0 |
| One gunner (control) | 14.42 | 1.59 | 0 | (control) | 3.39 | 4.17 | 2.58 | 0 | 9.5 | 22.44 | 56.98 |

**Broadside**

| Preset | Points | ± | Win % | Seat value H/S/F/B | Gun shots/min | Gun kills/min | Gun hull hits | Pen prisms/min | Missiles/min | Dry % | Stops/min |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 6 | 48.7 | 1.49 | 33 | -4/7/9/19 | 781.49 | 18.6 | 38.42 | 173.55 | 37.76 | 61.52 | 85.41 |
| No pen (control) | 47.02 | 1.88 | 33 | (control) | 683.08 | 14.61 | 15.08 | 0 | 40.76 | 58.95 | 90.02 |
| Fast darts | 50.93 | 1.62 | 58 | 6/12/8/19 | 770.55 | 21.38 | 19.58 | 174.33 | 49.43 | 57.87 | 127.45 |
| Heavy shells | 48.91 | 1.23 | 33 | -6/10/5/11 | 777.16 | 18.33 | 51.58 | 184.26 | 29.6 | 67.64 | 86.36 |
| Long linger | 50.08 | 1.97 | 58 | -1/8/8/20 | 739.45 | 25.82 | 26.33 | 164.33 | 42.87 | 57.27 | 93.13 |
| Scarce omnis | 48.52 | 0.97 | 33 | -4/6/6/18 | 927.1 | 18.94 | 45.42 | 210.81 | 21.44 | 82.61 | 99.24 |
| Hull-locked turrets (control) | 47.97 | 1.03 | 33 | (control) | 755.67 | 20.72 | 39.92 | 174.66 | 33.65 | 65.82 | 66.14 |
| Round 4 shield (control) | 46.65 | 1.25 | 25 | (control) | 802.32 | 28.82 | 35.58 | 188.04 | 37.1 | 64.8 | 15.94 |
| No guns (control) | 49.47 | 7.12 | 50 | (control) | 0 | 0 | 0 | 0 | 36.76 | 67.75 | 6.94 |
| No shield (control) | 50.77 | 1.22 | 58 | (control) | 717.62 | 11.44 | 35.08 | 167.16 | 42.71 | 58.76 | 0 |
| One gunner (control) | 33.5 | 0.22 | 0 | (control) | 4.22 | 3.61 | 6.08 | 0 | 7 | 26.81 | 71.31 |

### Found

1. **The no-cooldown bomb turns every game more violent.** Hits dealt and taken roughly double (Anchorage 138 / 132
   against round 5's 72 / 45), because every crew now throws a bomb as soon as the last one bursts. In Anchorage this
   costs everyone trail: 73 ± 16 against round 5's 119.5 ± 7.6, and below "No guns" (91.6, the same in both rounds).
   It is the design asked for; Mass-game balance (a bomb that spares prisms, or a longer reload after a burst) is the
   designer's call.
2. **The pen is worth a little where mass counts.** Anchorage 73 with it against 65 without; elsewhere within noise.
   It lays 105–187 prisms a minute.
3. **Breach is unchanged in points** (193 ± 12): the bomb seat is still worth the most there (42%).
4. **Every gunner seat still matters in the fights**: Anchorage 43 / 78 / 63 / 70%, Broadside -4 / 7 / 9 / 19%. The Ring
   Race is still the helm's alone.

### Checked

- `verify_lab.cjs` PASS. Seat screenshots (scope, nib, bomb sight, ability row) looked at. A scripted human sweep
  measured the pen's stroke (20 prisms, every gap 5.0 u). The radial fan of magenta sticks in the first screenshot was a
  rival's dropped shield, not the pen.
- `parity.cjs` PASS with the pen: 400 scripted landings and 21 reaches, C# `OrcaPen` against the page, plus a planted
  `rapidPenSpacing` 5 -> 4.8 control caught.

## 4c. Round 5 (2026-10-21): a gun on every seat, a missile per seat, and a shield that drops

**Input this round** (Garrett):
- "each gun seat should still have missile launcher (guided missile) and gun. getting the omnicrystal should give each
  gun a missile (not a shared pool). each of the three guns should be different. long cooldown sniper that kills
  supershielded prisms, fast gun like a brittlestar, and something like a grizzly bomb, but it doesn't blast the vessel
  around."
- "lets have the helms prisms start as supershielded prisms while they are being carried with the vessel, but after a
  few seconds they transform to danger prisms and remain in place."

### What changed

- **Each gunner seat has its own magazine:** 2 at the start, 4 at most. An omni crystal gives **every** seat one
  missile; before, there was one shared magazine of 6, up to 12, with 3 per omni. `aiReserve` is gone, because a seat
  no longer competes with the others for missiles.
- **Each gunner seat has a gun**, on its own button: Fire is click / Space / RT; the missile moved to right-click / F /
  A. The game weapon each one copies supplies its numbers:
  - **Gunner 1 (dorsal mast): the sniper.** This is the Serpent's sniper shot (`SniperShotAction.asset`): a 12 s
    cooldown and a 1.5° cone at least 6 u wide. **Everything in the cone dies, super-shielded prisms included**, and
    it pierces without limit. Each rival hull in the cone loses a petal of each element (4).
  - **Gunner 2 (port boom): the fast gun.** These are the Brittle Star's rounds (`BrittleStarConfig`): 135 u/s, 4.5 s,
    slowing as they fly, for 387 u of range.
    - In the game a round on a rival hull takes 0.01 of each element (`VesselCombatHitByBullet`). The lab adds that up
      as 0.4 of a petal per hit.
    - The lab fires 30 rounds/s, a third of the Brittle Star's 90, so the scorecard stays fast.
    - It overheats after 6 s of fire and is locked out for 5 s, close to the Brittle Star's five guns at Space 0.
  - **Gunner 3 (starboard boom): the bomb.** This is the Grizzly's trigger bomb (`GrizzlyTriggerBombConfig`): 90 u/s
    plus the hull's speed.
    - It bursts on a rival hull, on a second press, or at a 4 s fuse (the lab's; the Grizzly's has none).
    - The blast radius is 50 u, the Grizzly's mid-charge.
    - **No self-launch:** the lab never had one, and in the game it is only `GrizzlyTriggerBombExecutor.LaunchSelf`,
      which the Orca does not call.
- **The helm's shield has two phases** (`shieldSuper`):
  1. **Carried:** for 4 s × Mass the prisms ride with the hull as **super-shielded** prisms. A carried block is a wall:
     a rival hull touching it is slowed, not debuffed, and missiles, rounds and bombs burst on it without breaking it.
     **Only the sniper breaks one.**
  2. **Dropped:** then each prism turns into a **danger prism where it is** and stays for `shieldLinger` (20 s). A
     dropped prism is an ordinary danger prism: it debuffs a rival that touches it, and any blast breaks it.

  The **Round 4 shield** control is the old shell: danger prisms that ride with the hull, then vanish.
- **The AI helm has a throttle.** A Ring Race seed showed the helm circling ring 2 for the whole race: at full speed the
  ring sat inside its turn circle (L-STU-106). The helm now pinches the sticks when its target is close (under 150 u)
  and off the nose (more than ~45°). Every race number below has this fix.

### Scorecard (I2, Hard rivals, mirror matches, every dial at its round-5 value, 12 seeds)

`runBatch({ game, intensity: 1, diff: 2, seeds: [1..12], seats: true, rookie: true })`; 13–32 s per game. Baked into
the page as `ROUND5`. These are mirror matches, so a control changes every crew; read a control as "the game with this
feature removed".

**Ring Race**

| Preset | Points | ± | Win % | Seat value H/G1/G2/G3 | Gun shots/min | Gun kills/min | Gun hull hits | Missiles/min | Dry % | Stops/min | Sway °/s |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 5 | 217.47 | 2.08 | 25 | 100/1/4/1 | 583.31 | 28.87 | 16.83 | 35.44 | 57.47 | 60.02 | 6.65 |
| Fast darts | 216.56 | 2.07 | 33 | 100/1/3/1 | 595.52 | 24.3 | 17.33 | 39.43 | 58.16 | 77.22 | 6.32 |
| Heavy shells | 216.42 | 1.78 | 33 | 100/1/4/2 | 527.82 | 31.35 | 13.25 | 31.17 | 58.82 | 58.05 | 6.12 |
| Long linger | 217.03 | 2.37 | 25 | 100/1/3/1 | 584.45 | 29.02 | 16.92 | 36.28 | 55.98 | 59.02 | 6.34 |
| Scarce omnis | 215.93 | 1.41 | 17 | 100/1/2/2 | 602.43 | 32.8 | 12.83 | 21.23 | 77.72 | 60.61 | 7.77 |
| Hull-locked turrets (control) | 216.07 | 1.61 | 25 | (control) | 586.7 | 41.6 | 18.42 | 32.55 | 57.23 | 60.95 | 40.75 |
| Round 4 shield (control) | 223.2 | 1.07 | 58 | (control) | 726.24 | 43.77 | 28.42 | 40.76 | 50.25 | 16.98 | 6.62 |
| No guns (control) | 209.66 | 1.96 | 0 | (control) | 0 | 0 | 0 | 22.2 | 79.9 | 4.19 | 7.9 |
| No shield (control) | 219.76 | 1.39 | 33 | (control) | 771.75 | 27.67 | 32.42 | 32.77 | 59.94 | 0 | 7.5 |
| One gunner (control) | 207.34 | 1.06 | 0 | (control) | 2.28 | 2.64 | 1.17 | 7.61 | 25.28 | 50.04 | 8.29 |

**Breach**

| Preset | Points | ± | Win % | Seat value H/G1/G2/G3 | Gun shots/min | Gun kills/min | Gun hull hits | Missiles/min | Dry % | Stops/min | Sway °/s |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 5 | 176.83 | 18.38 | 50 | 1/14/8/33 | 875.78 | 61.28 | 0 | 41.35 | 58.52 | 67.98 | 5.97 |
| Fast darts | 189.92 | 21.36 | 58 | 0/17/16/42 | 848.61 | 57.41 | 0.08 | 39.39 | 67.69 | 75.55 | 6.82 |
| Heavy shells | 175 | 13.64 | 58 | -15/5/14/22 | 941.27 | 58.07 | 0.17 | 45.85 | 43.51 | 90.92 | 4.39 |
| Long linger | 176.83 | 18.38 | 50 | 1/14/8/33 | 875.78 | 61.28 | 0 | 41.35 | 58.52 | 67.98 | 5.97 |
| Scarce omnis | 189.58 | 19.4 | 50 | -11/13/5/31 | 941.2 | 57.38 | 0.17 | 28.82 | 73.64 | 39.83 | 6.63 |
| Hull-locked turrets (control) | 167.17 | 18.36 | 42 | (control) | 720.6 | 58.5 | 0.08 | 43.02 | 53.99 | 48.55 | 38.23 |
| Round 4 shield (control) | 178.42 | 17.61 | 50 | (control) | 891.48 | 88.47 | 0.08 | 40.43 | 59.13 | 21.56 | 6.05 |
| No guns (control) | 182.08 | 17.08 | 58 | (control) | 0 | 0 | 0 | 33.04 | 78.97 | 1.56 | 6.65 |
| No shield (control) | 191.17 | 16.48 | 67 | (control) | 845.25 | 57.32 | 0 | 40.55 | 57.68 | 0 | 5.59 |
| One gunner (control) | 78.67 | 7.6 | 0 | (control) | 3.98 | 17.62 | 0 | 12.95 | 23.56 | 48.37 | 6.38 |

**Anchorage**

| Preset | Points | ± | Win % | Seat value H/G1/G2/G3 | Gun shots/min | Gun kills/min | Gun hull hits | Missiles/min | Dry % | Stops/min | Sway °/s |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 5 | 119.5 | 7.59 | 92 | 87/79/74/77 | 406.85 | 11.33 | 6.17 | 58.92 | 29.02 | 51.87 | 4.09 |
| Fast darts | 68 | 14.1 | 42 | 66/51/30/67 | 461.77 | 18.11 | 19.58 | 54.81 | 44.59 | 48.54 | 5.37 |
| Heavy shells | 81.92 | 15.6 | 58 | 68/64/54/70 | 335.71 | 7.33 | 13.17 | 41.48 | 40.83 | 34.66 | 4.38 |
| Long linger | 107.5 | 12.37 | 83 | 73/70/83/71 | 353.37 | 15.88 | 6.33 | 59.26 | 29.54 | 53.09 | 3.78 |
| Scarce omnis | 97.25 | 12.62 | 58 | 72/40/56/57 | 498.65 | 16.16 | 16.33 | 36.93 | 62.22 | 17.66 | 5.9 |
| Hull-locked turrets (control) | 60.25 | 14.94 | 42 | (control) | 385.47 | 9.77 | 6.25 | 46.93 | 41.14 | 35.76 | 53.21 |
| Round 4 shield (control) | 76.67 | 16.15 | 58 | (control) | 408.07 | 12.11 | 10.17 | 55.26 | 37.16 | 13.66 | 4.47 |
| No guns (control) | 91.58 | 15.52 | 58 | (control) | 0 | 0 | 0 | 54.26 | 38.43 | 4.88 | 4.63 |
| No shield (control) | 76.58 | 16.33 | 58 | (control) | 335.93 | 4.28 | 4.33 | 54.54 | 33.74 | 0 | 4.23 |
| One gunner (control) | 21.33 | 2.71 | 0 | (control) | 3.39 | 4.89 | 2.75 | 8.88 | 23.07 | 45.37 | 6.59 |

**Broadside**

| Preset | Points | ± | Win % | Seat value H/G1/G2/G3 | Gun shots/min | Gun kills/min | Gun hull hits | Missiles/min | Dry % | Stops/min | Sway °/s |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 5 | 51.53 | 2.43 | 58 | 15/23/23/14 | 618.66 | 14.88 | 29.83 | 44.15 | 54.64 | 90.91 | 5.9 |
| Fast darts | 52.8 | 1.77 | 67 | 16/22/26/18 | 728.06 | 19.27 | 40.08 | 46.48 | 59.26 | 116.84 | 6.23 |
| Heavy shells | 48.6 | 2.32 | 33 | -4/11/15/10 | 636.99 | 11.77 | 36.92 | 31.77 | 64.78 | 88.36 | 6.83 |
| Long linger | 51.88 | 2.01 | 58 | 13/23/26/17 | 609.66 | 17.88 | 34.08 | 43.15 | 53.91 | 98.85 | 5.7 |
| Scarce omnis | 50.07 | 2.1 | 58 | 11/22/23/5 | 785.88 | 18.94 | 36.25 | 23.55 | 78.77 | 68.09 | 7.86 |
| Hull-locked turrets (control) | 46.9 | 2.13 | 25 | (control) | 746.95 | 16.38 | 44.33 | 29.99 | 69.08 | 90.19 | 51.92 |
| Round 4 shield (control) | 50.69 | 2.36 | 50 | (control) | 641.87 | 23.94 | 31.5 | 41.71 | 56.94 | 20.1 | 6.38 |
| No guns (control) | 49.47 | 7.12 | 50 | (control) | 0 | 0 | 0 | 36.76 | 67.75 | 6.94 | 7.13 |
| No shield (control) | 47.22 | 1.34 | 33 | (control) | 704.4 | 7 | 51.17 | 32.93 | 67.45 | 0 | 7.67 |
| One gunner (control) | 33.4 | 0.47 | 0 | (control) | 3.89 | 2.56 | 5.5 | 7.44 | 25.55 | 81.25 | 7.28 |

### Found

1. **The two-phase shield is the round's biggest change, in Anchorage.**
   - It scores 119.5 ± 7.6 there, against 76.7 ± 16.2 for the round-4 shield and 76.6 ± 16.3 with no shield, at a 92%
     win rate.
   - The carried super-shielded shell stops 52–91 rival shots a minute, against the old shell's 14–24.
   - The dropped danger prisms then wall off a home cluster.
   - Elsewhere the shield's worth stays within noise.
2. **Stabilization matters once there are guns.** In Anchorage, hull-locked turrets score 60 against 119.5. The guns
   aim; round 4's missiles were steered after launch, which hid the turret's sway.
3. **Every gunner seat matters in the fights.**
   - Anchorage's seat values are 87 / 79 / 74 / 77%, and Broadside's 15 / 23 / 23 / 14%.
   - In Breach the bomb seat is worth most (33%), then the sniper (14%) and the fast gun (8%).
   - The Ring Race is still the helm's alone (100 / 1 / 4 / 1).
4. **The guns add little to points in mirror matches**: 217 against 210 in the race, 120 against 92 in Anchorage, and
   within noise in Breach and Broadside. They take most of the hull hits in Broadside (30 a match). With per-seat
   magazines a seat has no missile 29–58% of the time, so the gun is what a gunner does meanwhile.
5. **Per-seat magazines empty seats unevenly.** Scarce omnis (20% omni crystals) leaves a seat dry 62–79% of the time.

### Checked

- `verify_lab.cjs` PASS. Before baking, single race seeds were traced to tell an AI fault (the orbit) from a design
  finding.

## 4d. Round 4 (2026-10-20): stabilized turrets on outriggers, and "4 seat"

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
   designer's decision on the shield (§4e) stays open and needs a person at the helm, not more seeds.

### Checked

- `verify_lab.cjs` PASS. Chase and turret screenshots read: the outrigger struts and pods show on the hull; the
  turret's view holds level while the helm turns under it.

## 4e. Round 3 (2026-10-20): one helm, three gunner seats, flown missiles

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

## 4f. Round 2 (2026-10-20): 3D with the game's chase camera (superseded)

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

## 4g. Round 1 (2026-10-20): 2D top-down (superseded)

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
