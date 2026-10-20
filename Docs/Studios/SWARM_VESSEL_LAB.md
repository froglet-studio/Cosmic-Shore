# Swarm Vessel Lab — round 3 (2026-10-20): Marlin and Jacks (the Shepherd, rebuilt)

**Live page:** https://claude.ai/artifact/Q7BgQYrT9xq5kmNtqCudmw (private to Garrett until shared)
**Straight in:** https://claude.ai/artifact/Q7BgQYrT9xq5kmNtqCudmw#shepherd (or the **Marlin and Jacks** button in the header)
**Source:** `Docs/Studios/SwarmVesselLab/src/` → `python3 Docs/Studios/SwarmVesselLab/build.py` → `Docs/Studios/SwarmVesselLab.html`
**Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/SwarmVesselLab.html` (PASS, desktop + phone)

## Round 3: top-down, a two-thumb pilot whose roll sets the plane (Garrett, 2026-10-20)

Garrett played round 2 and sent five notes. Each one, and what the lab now does:

| Garrett's note | What the lab does now | Reading that is the lab's |
|---|---|---|
| 1. The thumb ring felt great. Zoom out more; drop the isometric view for the Wrasse's top-down | The camera is the Wrasse's kite view (`KiteNavigatorView`): orthographic, straight down the plane's up axis, the plane's forward up the screen (heading up). The view span is 1,800 u (was 1,400). Settings has a **North up** box for comparison | The thumb floor is fixed on screen, so it grows in the world as the view zooms out: 400 u floor, 568 u ring at Space 2. |
| 2. The Time L5 decoy is a pilot ability built on the dog's decoy | At Time L5 the pilot drops **the** decoy where it is (F, X button or the DECOY pad), 4 s cooldown. It is the same one-at-a-time decoy, so it replaces a dog's. Teleporting back to it is the combo. The round-2 swap is gone | The 4 s cooldown is a lab knob. |
| 3. The pilot flies on two thumbs like the Grouper, helps the dogs, and its **roll** controls the plane the dogs move in and the sheep are drawn to | The pilot flies the Grouper's axes (`VesselTransformer`, `DualStickMix`): stick sum = yaw and pitch, difference = roll, spread = throttle. It uses the Grouper's rates (pitch/yaw 100, roll 120, +0.1 °/s per u/s; `Wrasse.prefab`) and keeps the Rhino's speed and straight ramp. The plane is the hull's own x-z plane, eased at 3/s, the Wrasse's signed-off board (`KiteBoardFrame.Eased`). Dogs, the decoy and the flock's spring all use it. Settings → **Plane frame** also offers Locked and Level (round 2) | "Straight" for the ramp means yaw, pitch and roll near zero; the spread does not count. Pitch tilts the plane too, because the board is the hull's whole x-z plane. |
| 4. Danger sheep collide with the pilot under the existing mechanics, without a permanent loss | A danger sheep the pilot touches dies. The pilot takes the own-domain danger hit (`IMPACT_EFFECTS_AND_AUDIO.md`): a full stop that recovers over 3 s, a ramp reset, and every element down 2 levels decaying over 4 s (1 s cooldown). There is no burn | The 2-level size is the lab's reading of the shipped `debuffMagnitude -0.5`. The debuff is real here, so it briefly shrinks the ring, slows the flock and thins the danger share. |
| 5. Name it "X and Ys" with real clades | The page now uses the proposed name **Marlin and Jacks** (one constant, `SHEP_NAME`, in `src/shepherd.js`) | Waiting on Garrett's pick. |

**The world is 3D now, so the plane matters.** Pasture patches sit at many heights. The Space fort floats
380 u above the start. The Time pens are spheres at 0, +420, −360, +520 and −480. Each pen draws its outline
(faint dashes) and the circle where the plane cuts it (solid), with a ▲/▼ height on the current pen. The
lower right shows the plane's tilt from level.

**The AI pilot** rolls toward the plane that passes through itself and the flock's target, as level as that
allows. It holds the flank opposite the driving dog on that plane, steers by elevation only (so a goal behind
it is a turn, not a loop), and avoids its own danger sheep. At Time L5 it leaves a decoy on station and
teleports back to it.

### Measured: round 3 scorecard

Same method as round 2 (AI crews, 30 s, seeds 1-2, round-3 values; deterministic across two calls). The
world changed, so compare rows with each other, not with round 2's table.

| Build | Time | Space | Mass | Charge |
|---|---|---|---|---|
| Pilot only (no dogs) | 0 | 0 | 6 | 1.5 |
| Dogs only (no pilot) | 10 | **0** | 0 | 12.5 |
| **Pilot + 2 dogs** | 20 | 138 | 2 | 2 |
| Level plane (round 2 frame) | 20 | 48 | 1 | 2 |
| Small thumb (14 mm floor) | 10 | 128 | 7 | 0 |
| Rookie crew (noise 0.6) | 10 | 92.5 | 8.5 | 2 |
| Time 5 (pilot decoy) | 20 | 124 | 2 | 4.5 |
| Space 5 (eat shields) | 20 | **263** | 3 | 5 |
| Mass 5 (poop mass) | 20 | 121.5 | **45** | 0.5 |
| Charge 5 (attack) | 20 | 138 | 2 | 14 |
| Charge 15 (all danger) | 20 | 138 | 2 | **19.5** |

### Checked / Found (round 3)

- **Checked:**
  - `verify_lab.cjs` PASS, and the mode loads on desktop and on a landscape phone with no console errors.
  - Holding roll for 0.6 s from level tilts the board 39°, easing on to 73°, and the dogs stay on it (height 0).
  - At Time 5 the pilot's decoy lands where the pilot is, and a teleport two seconds later brings it back.
  - Flying straight through a 100%-danger flock during a call: three hits, each a full stop and a −2 element debuff that is gone 4 s later, with the ring shrinking and recovering with it.
- **Found:**
  1. **The pilot now matters.** Without the pilot's plane the dogs cannot reach the floating fort (Space 0). With the round-2 level plane the crew grazes 48. With the roll plane it grazes 138. This answers round 2's "what is the pilot for": it carries the flock to a height.
  2. **The pilot's repulsion keeps its own danger sheep away.** The AI pilot took no danger hits even at Charge 15. Hits come from the call (the pilot stops repelling while it calls) and from teleporting into the flock. That makes the call a real risk at high Charge.
  3. **Mass is weak without Mass L5 (2 against 45).** Pasture spread over heights means fewer meals per sheep, and a lamb takes 2.4 meals. If Mass should matter below L5, lower meals per lamb or put more pasture near the plane.
  4. **The AI pens slowly** (two of five in 30 s). The plane eases at 3/s and the AI rolls only when the next pen is out of plane. A human pilot who tilts ahead of time should beat it.

### Decision needed (Garrett), round 3

1. **The name** (decision card in the thread). The proposal is **Marlin and Jacks**: the marlin is the fast straight-line lunger, and jacks are the pack fish that herd bait balls.
2. **Fly the pilot** ("You: the pilot", two thumbs or a gamepad). Does rolling the plane feel like steering the flock? Is the eased board (3/s) too slow or too fast?
3. **Mass below L5**: is a weak Mass game acceptable (finding 3), or should breeding be easier?

## Round 2: the Shepherd (Garrett's direction, 2026-10-20)

Garrett played the toys and asked for new directions, starting with this one. The only vote in the log
before it was **"Herd the plague" → merge**, the round-1 sheepdog toy, so this is that toy grown into a crew.
It lives in `src/shepherd.js` as a full-screen mode of the same page, with its own model, loop, scorecard
and `window.__lab.shep` test surface. It writes to the same decision log (topic `shepherd:<game>`).

### The spec (as asked, and how the lab reads each line)

| Garrett's line | What the lab does | Reading that is the lab's, not Garrett's |
|---|---|---|
| A **shepherd (pilot)**, with the Rhino's full speed and its fly-straight zoom | The pilot flies the Rhino's shipped numbers: cruise 50 u/s, the straight-line ramp to ×16.8 = 840 u/s at 220 u/s², bleed 300, coast 120, full boost below 0.3 stick deviation and none at 1.0, turn rate (speed × 0.2 + 90) °/s (`Rhino.prefab`, `RhinoRampBoostAction.asset`, `StraightLineGesture.EngageThreshold`) | No sword, shield swipe or trail. Time does not speed the ramp here, because Time is the flock's. |
| **Two sheepdogs**, one mobile player, **one thumb per dog** | Each new touch claims the nearest free dog; the dog runs to the point under the thumb on the pilot's plane at 520 u/s | Mouse drag works too, for testing at a desk. |
| **Minimum repulsion radius larger than a big thumb's occlusion** | Floor = (big thumb 22 mm ÷ 2) × 6.3 px/mm × 1.25 margin, fixed in world units from a 390 px landscape phone: **311 u** at the 1,400 u view. The ring is never smaller than that. A translucent disc shows what the thumb hides | The thumb size, the margin, the nominal phone and the view span are lab knobs. |
| **Sheep = the swarm**, 3D, **very attracted to the pilot's XZ plane** (where the dogs are) | Boids in 3D (separation, alignment, cohesion, wander) with a strong spring to the plane at the pilot's altitude. Fly up or down and the plane, the dogs and the flock follow | Repulsion is a vertical cylinder round each herder, so it reads from above. |
| **The pilot is repulsive**, and can **briefly call the sheep** on a long cooldown | Same ring as the dogs. Call: 2.5 s of pull toward the pilot, 20 s cooldown | Both lengths are lab guesses. |
| **Lift a thumb → a repulsive decoy**; one at a time, the new replaces the old | A lift leaves a decoy where the dog stood, with the same ring | The dog stays where it was until a thumb comes back. |
| The pilot **teleports to the decoy** with a trigger or by lifting a thumb | Right trigger, Space, or lifting the touch-stick thumb. The decoy is used up | 0.6 s guard between teleports. |
| **Mass** controls the reproduction rate | A sheep has a lamb after 4 ÷ (1 + 0.1 × (Mass + 5)) meals: 4 at −5, 2.4 at 2, 1.3 at 15 | Sheep graze any non-team prism they touch. |
| **Time** controls the speed of the swarm | Sheep cruise 70 u/s × (1 + 0.06 × Time); fleeing adds up to +80% | |
| **Space** controls the repulsion radius of all three sub-vessels | Ring = floor × (1 + 0.06 × (Space + 5)): the floor at −5, 442 u at 2, 591 u at 10 | Space scales *above* the floor, so no level is dead. The decoy gets the same ring. |
| **Charge** sets the share of **danger prisms** in the swarm: 0% at −5, 100% at 15 | Each sheep rolls once at birth; it is a danger sheep while its roll is under (Charge + 5) ÷ 20 (35% at 2). Danger sheep wear the danger rim | "Danger prism" is read as the game's danger prism (`PrismKinds`): a rival that touches one is slowed and loses a petal, and the sheep is spent. The game's band stops at 10 (75%). |
| **Mass L5**: the swarm periodically **poops a cluster of mass** | Every 6 s a random sheep leaves 7 of your prisms | |
| **Space L5**: the swarm can **eat shielded prisms** | Shielded prisms (white outline) become edible | "Shielded prism" is read as the game's shielded tier. |
| **Charge L5**: the swarm **attacks players** once in proximity | Sheep within 200 u of a rival go for it; a plain sheep's nip is worth a third of a petal | |
| **Time L5** leaves a decoy **in the pilot's location** | When the pilot teleports, a new decoy is left where it was, so a second teleport swaps back | **Decision needed:** is that the meaning, or should the pilot drop decoys on its own? |

**The four games** (one per element, the labmaker rule): Time = a race of five pens (60% of the flock at
once fills one), Space = graze a ruby fort that is 35% shielded at I2, Mass = grow the flock on regrowing
pasture, Charge = rival "wolves" that hunt the pilot and eat plain sheep. Each has an I1-I4 intensity.
**You play:** the dogs (AI pilot), the pilot (AI dogs), both (pilot on keys or pad, dogs on touch or
mouse), or watch the AI.

### Measured: the Shepherd scorecard

AI pilot and AI dogs (Strömbom-style drive and collect), 30 s, seeds 1-2 averaged, shipped / round-2
values, produced by **Settings → Run scorecard** (`window.__lab.shep.runBatch({seeds: [1, 2]})`,
deterministic across two calls). Time = pens × 10 + seconds spare; Space = enemy prisms eaten; Mass =
lambs + your mass standing; Charge = petals taken − lost + 3 per knockout.

| Build | Time | Space | Mass | Charge |
|---|---|---|---|---|
| Pilot only (no dogs) | 0 | 0 | 15.5 | 2 |
| Dogs only (no pilot) | 20 | 116.5 | 22 | 8.5 |
| **Shepherd + 2 dogs** | 20 | 107.5 | 13.5 | 3 |
| Small thumb (14 mm floor) | **0** | 98 | 27 | 3.5 |
| Rookie crew (noise 0.6) | 15 | 112 | 18 | 3 |
| Time 5 (swap decoy) | 20 | 128 | 29.5 | 4.5 |
| Space 5 (eat shields) | 20 | **201** | 18 | 2.5 |
| Mass 5 (poop mass) | 20 | 147.5 | **70** | 3.5 |
| Charge 5 (attack) | 20 | 107.5 | 13.5 | 9.5 |
| Charge 15 (all danger) | 20 | 107.5 | 13.5 | **18** |

### Checked / Found (round 2)

- **Checked:**
  - `verify_lab.cjs` PASS on desktop and phone (the round-1 lab is unchanged: its batch reads the same before and after).
  - The Shepherd batch gives identical tables on two calls.
  - A scripted landscape-phone thumb (844 × 390, touch) grabbed dog B, dragged it, and on lift left one decoy where the dog stood. No horizontal scroll on the phone.
- **Found:**
  1. **Three of the four L5 upgrades win their own element's column**: Space 5 doubles the fort grazed (107.5 → 201), Mass 5 multiplies Mass by five (13.5 → 70), and Charge 15 is the Charge top (18). **Time 5 does not move Time** (20 → 20): the swap decoy helps Space and Mass, but nothing in the pen race rewards it. Time 5 needs a racing reason, or the race needs a teleport-shaped shortcut.
  2. **The AI pilot adds nothing over two dogs** (Space 116.5 without it, 107.5 with it), and alone it cannot herd at all (0). A Rhino cannot hover: it circles on station, and its ring sweeps through the flock. In Charge it does something the score cannot see. It pulls the wolves onto itself, so in a 45 s run (seed 1) 38 of 60 sheep live, against 5 without it, but fewer danger sheep meet a wolf. Whether the pilot is a herder, a decoy for wolves, or mainly the call-and-teleport is the design question this crew raises.
  3. **The thumb floor is a game rule, not a UI detail.** At a 14 mm floor the ring is too small to move a 60-sheep flock into a pen (Time 0). At 22 mm the rings are so big that the flock panics most of the time, and panicking sheep do not graze, so Mass is low (13.5) until Mass L5 poops mass. Herding and grazing compete. (L-STU-118)
  4. **Wolves eat the flock fast.** Before rivals were given a 1.2 s pause per sheep, they ate 58 of 60 sheep in 45 s. Danger sheep are the flock's only defence below Charge 5.
  5. **A rookie crew out-grazes an expert in Space** (112 vs 107.5), as in round 1: a noisy herd spreads over more prisms.

### Decision needed (Garrett), round 2

1. **Play it on your phone in landscape**, as "You: the dogs". Does a 311 u floor (a ring about 1.8 thumbs wide) feel right, or is it so big that one dog moves the whole flock?
2. **Time L5**: is "teleport leaves a decoy behind (swap)" what you meant?
3. **What is the pilot for?** Herding, drawing the wolves, or gathering (call) and repositioning (teleport)?
4. **Danger sheep vs your own team**: here they hurt only rivals. Should they also slow your own pilot?

## Why it exists (round 1)

The Termite came back as a commander queen with a card deck, and playing it was the first time the
studio held a vessel that *is* a swarm. The question this lab answers is wider than the Termite:
**what is a swarm vessel made of, and which combinations are fun?** The aim is to find enough fun moments
to cut several vessels out of one kit. Each vessel would control a different kind of swarm, play
differently and fill a different team role.

The lab's answer to "made of" is four axes, each swappable while the others hold:

| Axis | What it is | Count |
|---|---|---|
| **Body** | a species: how members move (genes: separation, alignment, cohesion, wander, orbit, agility, leash) and the ONE verb they do to the world | 19 |
| **Control** | how the pilot talks to the swarm. It may only write a focus point, an order, a mood, a bait, a throw point or a selection — never a member's position | 14 |
| **Economy** | what pays for members and cards | 7 |
| **Meta** | the layer above a match | 7 |

So the whole design space is 19 × 14 × 7 × 7 = 13,034 builds with **no code per pair**.
L-STU-115 records why that worked: the control vocabulary is narrow and every body reads only it.

## Sources (and what is proposal)

Each item carries a `source` tag in the page (`shipped` / `notion` / `ecology` / `lab`).

- **Shipped:**
  - the Termite's six cards and pheromone tank (numbers read from `TermitePheromoneAction.asset`, `TermiteDroneTuning`, `TermiteDeckExecutor`);
  - the tadpole boid;
  - the locust's phase rule (`Tools/Ecology/bestiary/species/locust.py`: crowding → gregarious; march 135 u/s; crowd radius 40).
- **Notion (read-only):**
  - the School (six fish bound by plasma) and the D-pad fauna orders;
  - Shepherd / sheepdog herding and Boldt's tiered fuel;
  - the Shuffle octopus and the Angler's fake crystal;
  - Beast Tamer bait and the 2024 termite commander ("distributing termites between cell and queen");
  - the NCA backlog's aggression knob (B5), plus slime-mold and siphonophore swarm concepts.
- **Lab proposals:** ants, pigeons, rats, bees, wasps, starlings, bats, fireflies, mosquitoes, crows,
  piranhas and spiders. The docs name few of these swarms, so their verbs are this round's guesses at
  each animal's one sentence.

## The toy box (37 fun moments)

Each toy is one hypothesis about a fun moment, a build to test it in, what to try, and the number that
shows whether it happened. **Keep / Cut / Merge into a vessel** on each toy writes to the shared
decision log.

| Body | Toys (the moment) |
|---|---|
| Termite | Mound rising (their mass walks home and becomes yours) · Paint a highway · Cell or queen? (2024 split) · Clash commander (cards only) · Workers, soldiers, scouts (castes) |
| Ant | The trail recruits (stigmergy) · Starve or thrive (food web) · Roguelike colony (draft) |
| Locust | The phase snap · Herd the plague (sheepdog) · Survival of the swarm (evolution) |
| Pigeon | The homing wheel · Message relay · Possess the lead bird |
| Rat | Through the wall (gnaws armour) · Pied Piper (rhythm) · Breeding explosion |
| Bee / Wasp | Claim the meadow · Each sting is a life · The nest boils over · Tiered fuel deck · Three into one (merge) |
| Starling / Bat / Firefly | Murmuration decoy · Shape the cloud · Echo reveal · Sync flash (Kuramoto) |
| Mosquito / Crow / Piranha | Throw the cloud · Mob and pickpocket · Crystal heist · Blood in the water |
| Slime / Spider / Siphonophore / School / Tadpole / Octopus | Grow a road · Web the lane · Living wall · Plasma ring · Plasma lasso · Metamorphosis · Shuffle arms |

## Measured: the role fingerprint

Every row is the AI commander playing that build in each element minigame (15 s, seed 1, 10 Hz).
Time is gates × 10 plus seconds to spare. Space is hostile prisms destroyed. Mass is your prisms
gained. Charge is petals taken − lost + 3 per knockout. **Hull only** is the same arena with no swarm.

| Build (default) | Time | Space | Mass | Charge | Reads as |
|---|---|---|---|---|---|
| Hull only | 20 | 0 | 13 | −3 | baseline |
| Termite | 30 | 2 | 32 | −3 | Builder |
| Ant | 20 | 8 | 25 | −2 | Builder |
| Locust | 20 | **152** | 15 | −4 | Breaker |
| Pigeon | 40 | 16 | 12 | −4 | Runner |
| Rat | 20 | 140 | 17 | −5 | Breaker |
| Bee | 20 | 2 | **43** | **26** | Builder / Hunter |
| Wasp | 20 | 0 | 20 | 10 | Hunter |
| Starling | 30 | 9 | 12 | −5 | (support: see below) |
| Bat | **50** | 0 | 9 | −4 | Runner / Scout |
| Firefly | 20 | 98 | 20 | −2 | Breaker |
| Mosquito | 20 | 1 | 19 | 13 | Hunter |
| Crow | **50** | 0 | 17 | 7 | Runner / Hunter |
| Piranha | 20 | 36 | 17 | −3 | Breaker |
| Slime mold | 20 | 3 | 21 | −4 | Builder (slow) |
| Spider | 20 | 4 | 19 | −2 | (support) |
| Siphonophore | 30 | 15 | 15 | −5 | (support) |
| Plasma School | 30 | 119 | 15 | −4 | Breaker |
| Tadpole | 20 | 81 | 22 | −5 | Breaker |
| Octopus | 30 | 6 | 27 | −2 | Flex |

The axes measured one at a time, the others held:

| Termite under each control | Time | Space | Mass | Charge |
|---|---|---|---|---|
| Commander point (shipped) | 30 | 2 | 32 | −3 |
| Rhythm | 30 | 4 | **45** | −2 |
| Two squads | 30 | 5 | 42 | −2 |
| Formations | 30 | 11 | 40 | −6 |
| D-pad orders | 30 | 2 | 32 | **26** |
| Sheepdog | **0** | 0 | 27 | −5 |

| Locust under each economy (Space) | Pheromone 125 · Biomass **152** · Crystal 126 · Hive 118 · Fixed 125 · Tiered 125 · Food web 126 |
|---|---|

| Wasp under each meta | Time | Mass | Charge |
|---|---|---|---|
| None | 20 | 20 | 10 |
| Castes | **40** | 22 | **13** |
| Roulette | 20 | **37** | 10 |
| Merge | 20 | 19 | 12 |

## Checked / Found

- **Checked:**
  - `verify_lab.cjs` PASS on desktop and phone: no console errors; `runBatch` is deterministic across two calls and across a page reload.
  - A smoke run loaded all 37 toys and ran each for 20 s with no exception.
  - The decision log is wired: an `ArtifactData list` returned the empty `decisions` collection.
- **Found in the lab:** two scores that could not discriminate (L-STU-114), plus mosquitoes that
  could never latch because a throw overrode their drain. All three are fixed.
- **Found in the tooling:** the verifier failed on the sandbox rather than on the lab, in two ways.
  Playwright was not on `NODE_PATH`, and Google Fonts failed through the TLS proxy. Both are fixed in
  `verify_lab.cjs`, and its self-test still names all six planted defects (L-GEN-18).
- **Findings worth a design conversation:**
  1. **The D-pad Attack order turns ANY body into a hunter** (Termite Charge −3 → 26). One order
     button erases the species' role. Either Attack needs a species-specific cost, or "who can attack"
     is part of what a body is.
  2. **Starlings and siphonophores do not show their value in any one score.** They absorb shots
     meant for the hull, and the Charge score barely moves (−5 vs −3), because a 15 s round with two
     hunters rarely empties the hull. Their role is protection. A fifth column (petals *saved*,
     measured against a hull-only run) would show it.
  3. **The economy axis barely matters at 15 s** (locust 118–152). Economies are slow levers, so judge
     them in the live 45 s rounds, not in this scorecard.
  4. **The rookie Termite out-built the expert** (Mass 43 vs 35). A noisy focus spreads the workers
     over more patches, so for a builder, dispersal beats precision. The AI commander is a heuristic
     and treats one point as best; take that as a hint about the Termite's scheme, not a ranking.
  5. **Sheepdog control cannot race** (Time 0): herding has no way to send a member to a point. It
     is a Space/Mass scheme only.

## Decision needed (Garrett)

1. **Play the toys and vote.** Keep / Cut / Merge on each toy goes to the shared log, and round 2 reads it.
2. **First consolidation guess, for you to overturn.** Four vessels, one per element role:
   - **Builder** (Mass): Termite or Bee body, Commander point or Rhythm, hive economy, card deck.
   - **Breaker** (Space): Locust or Plasma School, Mood dial, biomass economy.
   - **Runner/Scout** (Time): Pigeon or Bat or Crow, Escort control, crystal tithe.
   - **Hunter** (Charge): Wasp or Mosquito, Throw control, castes meta.
   - The Starling / Siphonophore / Spider family is a **Support** vessel the scorecard cannot see yet (finding 2).
3. Should **Attack** be universal (finding 1)?

## What this lab does NOT model

- It is 2D top-down. A 3D swarm surrounding a ship in a sphere reads and defends differently.
- Member counts (6–60) say nothing about the collider budget. A shipped swarm must use the Termite's
  drone path (vessel agents, never lifeforms), and its collider cost is unmeasured here.
- Rivals are scripted ships, not players. Petal transfer moves whole petals, but there is no crystal ejection.
- There is no networking and no gamepad.
- A prism here is a dot with one hit point. Mass scores count prisms, not volume.
- The AI commander is a heuristic; treat a gap under ~10% as noise.

## Round log

- **Round 3 (2026-10-20, `claude/project-thread-iu9brp`):** the Wrasse's top-down camera zoomed out; the pilot on the Grouper's two sticks with the plane as its eased x-z board (roll tilts it); a 3D world (floating fort, spherical pens, pasture at heights); Time L5 as the pilot's own decoy; own danger sheep hit the pilot (slow plus a decaying element debuff, no burn); the proposed name Marlin and Jacks.
- **Round 2 (2026-10-20, `claude/project-thread-4slpcz`):** the Shepherd mode (`src/shepherd.js`): Rhino pilot, two thumb-driven sheepdogs with a thumb-occlusion floor, decoys, call, teleport, the four element rules and their L5s, four games with I1-I4, a ten-row scorecard; learning L-STU-118.

- **Round 1 (2026-10-19, `cece/inspiring-einstein-k6syi5`):** built the lab, the 37 toys, the scorecard and team builder, and the decision log; made `verify_lab.cjs` hermetic (L-GEN-18); learnings L-STU-114 and L-STU-115.
