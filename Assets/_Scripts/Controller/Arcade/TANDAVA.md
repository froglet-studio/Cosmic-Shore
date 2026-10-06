# Tandava

`GameModes.Tandava = 62` · ARENA card · Squirrel, Sparrow, Rhino · co-op (every pilot on one domain) · scene
`MinigameTandava` · assets owned by `Tools/Build/author_tandava_assets.py` (`--check`).

**Status: authored headless, NOT yet run in the Unity editor.** Everything below that describes behaviour is from the
headless harness (`Tools/Build/swarm_core_harness`, mode `tandava`), the reference compile (`unity_refcompile`, player
and editor), a Roslyn type-check of the swarm glue, and the generators. QA: `QA-TANDAVA-1..8` in
`Docs/UNITY_VERIFICATION_CHECKLIST.md` (§ Tandava; `/verify-unity` could not run: no Unity editor or `unity` CLI in the
authoring sessions). Phase A (the route, the four eating forms, the cell's colour) and phase B (the ascension, the
Winged Lion, the sealed exit, foraging and starvation) are both in.

## 1. The pitch

A tadpole swarm hatches at one end of a long cell and eats its way down a route of oases toward the exit membrane at
the other. Every time its body is full and its stomach holds the surplus, it takes its next **form**. Every form is a
**flyer** (the HyperSea has no ground): no form walks. The cell keeps its own colours through every form; it changes
for one thing only, the dance (below).

**The exit is sealed to every form but the last.** A serpent or a bull that reaches the membrane is held back by it.
To leave, the banked Bull must **ascend**: it rises into the **Lord of the Dance** (the Nataraja, as a sculpture of
tadpoles) and swims to the dance ground as the figure assembles, and **the cell blooms and turns to fire with it**.
There a **ring of fire** of twelve flames lights round the figure, its attendant packs patrolling round the ring, and a
drum runs. When the drum stops, the swarm takes its **final form**, the **Winged Lion**, the only form the membrane
lets through, and the cell eases back to its own colours.

Pilots chase it, cull it, and burn its food. They win four ways:
- **wipe it out**;
- **starve it**: with nothing left to eat and the membrane sealed, it presses on the membrane and withers;
- **break the dance**: put out nine of the twelve flames before the drum stops, by threading them, past the patrol;
- **break the Winged Lion**: cut the final form below 35% of its body before it crosses.

They lose only when the Winged Lion crosses the exit.

Only the mode carries the name. The forms, the narrator and every UI string use plain English names.

## 2. The forms

| # | Form | Body (plan / in game at density 3) | Element mix | How it is reached |
|---|---|---|---|---|
| 1 | Young Serpent | 69 / 207 | Mass 72 · Charge 7 · Space 9 · Time 12 % | it hatches as this |
| 2 | Serpent | 108 / 324 | Mass 72 · Charge 7 · Space 9 · Time 11 % | eating: the same head and hood, a longer tail |
| 3 | Great Serpent | 157 / 471 | Mass 71 · Charge 7 · Space 10 · Time 11 % | eating: longer again |
| 4 | Bull | 154 / 462 | Mass 53 · Charge 26 · Space 5 · Time 16 % | eating: coils fold into a horned bull that FLIES, legs swept back and out as fins |
| 5 | Lord of the Dance | 128 / 384 | Mass 49 · Charge 10 · Space 10 · Time 30 % | the ASCENSION: the banked Bull rises into it on the way to the dance ground |
| 6 | Winged Lion | 142 / 426 | Mass 42 · Charge 15 · Space 27 · Time 17 % | the drum stops; NO legs; the only form the exit lets through |

The names are what the HUD ("Serpent 62%") and the narrator say. The plans are keyed `serpent_s` / `serpent_m` /
`serpent_l` / `bull` / `dancer` / `lion` (`Assets/_SO_Assets/Swarm Fauna/Tandava/SwarmPlan_tandava_<kind>.json`).

- **Lord of the Dance**: the Nataraja in the body's (side, up) plane, facing back down its heading: the right leg on
  the prone dwarf, the left leg raised and swung across, four arms (the drum, the fire, the open palm, the arm across
  the chest), the crown and two locks of flying hair, on a pedestal. Limbs are double strokes so the pose reads at a
  distance. Outside the ring of fire, four **attendant packs** of seven Time units (the bestiary's pack hunters)
  orbit the ring across the eight frames, two each way, one turn per loop. The dance plan runs at 40 steps a frame
  (one patrol turn per 32 s); the figure itself barely moves between frames. The plan bakes where the ring is
  (`"ring"`: its centre, radius and the packs' patrol radius), so the mode and the harness read one source.
- **Winged Lion**: an ellipsoid body and head under a Charge mane of two shield-plate rings, two great wings of five
  Space feathers each beating slowly (a 9.6 s wingbeat at 12 steps a frame: feathers are the slowest tadpoles,
  0.8 voxels a step, and a faster stroke left the live wings smeared above the body), a tail with a tuft, and **no
  legs**: two Time flame ribbons stream from its haunches into a swallow-tail V beside the tail, rippling, each with a
  Charge flame tip. They start at mid-height and never hang below the belly, so from a chase camera they read as
  contrails, not legs (the prompter's call, 2026-10-06: a legged animal walking through the HyperSea looks wrong).

- **Serpent**: Mass coils (bulk, food store, what grows), a Charge **hood** of shield-tier plates flared round the
  head, Space rods along the back (the shimmer band), and a Time tail rattle. A lateral travelling wave carries it.
  It grows by adding stations at the TAIL, so a small → medium → large commit reads as the snake getting longer.
- **Bull**: a hollow Mass body with a hump, a Charge brow and two long horns, a Space neck bell and tail, and four
  Time legs that are **fins**: swept back AND out from the lower flanks like a manta's, the fore pair spreading wide, the
  hind pair trailing past the rump, beating together in one stroke from root to tip (never a gait). Swept back alone
  they read as two hanging columns from the front, which is why they spread.

The plans are generated procedurally by `Tools/Build/tandava_plans.py`, in the same JSON schema as the Swarm cell's
four elemental plans and through the same element identity clamp (`swarm_plans.identity`). It asserts:
- the design's shares, to within 3.5 points;
- both regions present, so lineages can colour back and belly;
- only Charge carries a tier;
- every pair of units at least 2.1 voxels apart in every frame (the sort core's collision radius is 2.25).

The dance and the lion are STROKE figures, and the sort core blurs a stroke into a blob at its default twelve
Gaussian wells per element and its sortfeel dead zone. The Tandava config runs `SortWellsPerType 24` and
`SortWellDead 0` (no flat-bottomed wells; the design's own note for the statue) (§4).

## 3. How it works

```
TandavaController  (MultiplayerDomainGamesController + ISwarmDirector, on every peer)
 ├─ SERVER: TandavaDirectorCore  (pure C#: route, forms, ascension, outcome)   <- the swarm's state, once per tick
 │     ├─ Route: oasis 1 .. 8; a denied oasis is skipped
 │     ├─ Forage: the route is eaten - nearest oasis with food left; a stop that yields nothing is written off
 │     ├─ Starving: nothing left anywhere - it presses on the sealed membrane until it has starved
 │     ├─ form: body >= 90% of its plan AND the bank holds the surplus -> commit the next form; the banked Bull -> ToDance
 │     ├─ Dance: the dance form, the drum, BreakFlame(k); the drum's end -> the final form (Final)
 │     └─ outcome: escaped (the final form only) | wiped | starved | broken | dance broken
 │   replicated: form, phase, goal, anchor, progress, the story clock, the outcome, the ring's centre and axes,
 │               the flames-out and flames-guarded masks (NetworkVariables) + narrator lines (ClientRpc)
 ├─ every peer: SwarmFauna (the swarm, client-local like all fauna) asks the director where to hatch and swim;
 │   a CLIENT's swarm takes the replicated form and is nudged toward the server's anchor;
 │   EVERY peer holds its own swarm behind the sealed membrane (TandavaDirectorCore.SealCorrection)
 ├─ every peer: the ring of fire - twelve TandavaFlame switches from the replicated placement; each peer tests the
 │   pilots it simulates and reports a threaded flame; the server judges it against the guard
 └─ every peer: CellVisualTint on the cell - eases membrane / nucleus / cytoplasm to the form's (or starving) palette
```

### 3.1 The swarm: the sort core with a scripted form list

The swarm is the Swarm cell's own sort core (`Docs/SWARM_FAUNA.md` §9), with one addition:
- `SwarmSortParams.Scripted` lets a director name the plan instead of the census.
- The plan list is then the ordered forms. A form's major element is data (`SwarmPlanData.MajorElement`), so three
  Serpent sizes can all be Mass.
- A form changes only on `RequestPlan`, through the same commit a majority morph takes: the `Switched` event, stale
  fates, the region map re-picked, the lay ramp restarted.
- Killing the majority never re-forms a scripted swarm.
- Every shipped config leaves `Scripted` off, and the majority mode is bit-identical. The four elemental plans index
  by element, so `Major == PlanIx`, and all 209 existing harness checks plus the tick-job and LOD suites still pass.

The swarm still evolves **by eating**: it lays only from eaten flora (1:1), and a form commits only when the body it
grew is full and the stomach holds the surplus. The director names which shape comes next, never when. It cannot
kill, spawn or feed a member.

### 3.2 The route

The cell is the Cleave membrane (radius 3,600). The route runs along +X through the centre:
- the swarm hatches at x = -2,000;
- 8 oases sit at x = ±550, ±900, ±1,250, ±1,650, each 100 u in radius;
- the exit membrane plane is at x = +2,000.

The middle crossing passes the nucleus (r ~392). No oasis sits inside it, because fauna eat nothing in the nucleus.

The flora is the cell's own (the Cell owns the environment): three Borromean flora configs, planted by the cell's
spawner into **planting pens** (sector cones of the membrane) centred on the oases.

| Config | Oases | Plants |
|---|---|---|
| early Mass | 1-4 | 2 each |
| late Mass | 5-8 | 3 each |
| late Space | 5-8 | 1 each ("mixed later") |

Each plant is budget-capped at 60 prisms (`MaxTotalSpawnedObjectsOverride`), so a lobby can burn an oasis out.

The director's route loop:
1. **Travel** to the next oasis.
2. **Feed** until it has eaten its meal there. A meal is the form's stage over 2.5, the design's "two to three oases
   a stage". It also leaves when the oasis is grazed bare, when its stomach is full, or after 45 s.
3. **Move on** to the next oasis.

An oasis with no edible flora left is **denied** and skipped. The server re-checks this each second.

After the last oasis the route is eaten, and the swarm **forages**: it goes to the nearest oasis that still has food
(the pilots' leftovers), and writes off a stop that yields nothing so it never circles. With nothing left anywhere it
is **Starving**: it swims to 140 u inside the exit plane and presses on the sealed membrane. Two things end it there,
and neither is the director killing anyone:
- **the swarm's own metabolism.** Unfed for `StarvationSeconds` (30 in this config) while hungry, `SwarmFauna` sheds a
  member every `ShedIntervalSeconds` (0.25), the core choosing whom. Below 15% of its form's plan it has starved;
- **the membrane's clock.** 40 s pressing on the membrane, it has starved. This is the outcome's clock for a swarm
  whose banked stomach keeps it from shedding at all (a Bull banking its offering can sit above the hunger line). It
  says who won; it removes no one.

**The sealed exit.** A form that is not the final one never crosses the exit plane: every peer nudges its own swarm
back to `SealHold` (60 u) inside it (`TandavaDirectorCore.SealCorrection`, the pure rule; the client nudge's rigid move).
Within `SealMargin` the narrator says the membrane holds, once per form.

### 3.3 The cell changes for the dance alone

`CellVisualTint` is a new, mode-agnostic Cell capability. It eases the cell's own spawned membrane, nucleus and
cytoplasm to a `CellPalette` and back. It changes how they **look**, never gameplay: the boundary, the nucleus
control radius and the phase are untouched. It recolours draws, never assets:

| Visual | How it is recoloured | Why |
|---|---|---|
| membrane | a property block on its instanced draw (`CapsuleMembrane.SetColourOverride`) | its `SpindleMaterial` is shared with every spindle |
| nucleus | property blocks on its renderers | per-renderer, no material copy |
| cytoplasm | ONE per-cell material clone (`SnowChanger.SetColourOverride`) | one write a frame however many motes |

**The rule (the prompter's call, 2026-10-06): the cell keeps its own colours for every form and every moment but
one.** When the Bull rises into the Lord of the Dance, every peer's cell blooms (up to 2.5× bright, gone by mid-ease)
and eases over 3 s into the dance's bronze and fire (`TandavaSettings.AscensionPalette`). It holds that through the
dance, and eases back to its own colours over 5 s when the dance ends: the Winged Lion is taken, or the dance is
broken. Nothing else tints it: not the serpents, not the Bull, not starvation, not the escape, not the win. The
trigger is the replicated form index (the dance form), so every peer changes at the same moment. Idle, the tint costs
nothing, and a cell the mode never tinted is never restored (no override is ever applied to it).

A big cell needs sparse motes: `CellConfigDataSO.CytoplasmShardDistance` (new, 0 = the prefab's 120). Tandava
authors 360, giving about 4,200 motes in the 3,600 u cell instead of the ~113,000 the prefab's spacing would build.
Cleave, the other 3,600 u cell, is unchanged.

### 3.4 Pilots, stakes and the AI

The cell takes one rule from the Swarm cell and keeps the shipped default for the rest:
- It starts **hostile** (`initialControllingDomain: OpposingLocalPilot`). Without it, an empty nucleus hands control
  to the local pilot and the swarm hatches friendly.
- It plays the **Shipped** petal burn and the **shipped** fauna stomach. The Tuned burn and the conserved stomach are
  the Swarm cell's contained experiments: `check_elemental_economy.py` fails on a second Tuned cell, and
  `Fauna.UsesConservedStomach` documents the stomach as the Swarm cell's alone. The swarm's own stomach is
  `SwarmFauna`'s and does not read that flag.
- Every strike rule of the round-10 bestiary applies per element (`Bestiary` on): pufferfish plates (Charge), lurker
  (Mass), locust shimmer (Space), pack (Time).
- **One colour.** `MultiDomain` is OFF, against the design's "MultiDomain on". A lineage swarm gives its slots 1 and 2
  the other two playable domains (`SwarmFauna.BuildSlotDomains`), and with every pilot on one domain one of those IS
  the pilots'. A drifted lineage would grow members in the pilots' own colour: an enemy that reads as a friend. It
  stays off until a lineage can exclude the pilots' domain.

The fleet (every hull on the card, flown by a human):

| Hull | Keeps up with the swarm's 50 u/s? | How it culls | How it denies an oasis |
|---|---|---|---|
| Rhino | yes: cruise 50, ramps to 1,200 on a straight | the sword, at speed | rams and sword through the plants |
| Squirrel | yes: cruise 60, 300 on skim energy (a teammate's trail is the steady source) | the joust | rams |
| Sparrow | yes with its free boost (35 → 135) | guns and rockets, from range | guns into the plants |

There are no starting elements: nobody races anybody, and every hull can hold the swarm's pace. The residual is
the AI, below.

Scoring:
- A pilot's score is members culled (`ScoringMetric.LifeformsKilled`, attributed kills only) plus 25 for each flame
  they put out (`TandavaScoringRuleSO.flamePoints`, counted in `SwitchesThreaded`: a flame is a switch, threaded).
- The match result is the swarm's outcome, not a metric race.
- `MinDomainsAllowed = MaxDomainsAllowed = 1`: every pilot and AI teammate flies one domain. It is the first co-op
  arena card.

The AI: `ArmChasers` points every AI pilot at the swarm, led 40 u toward its goal and spread ±45 u across the body.
During the dance it flies at the nearest lit, unguarded flame instead, aimed 30 u past it along the flame's axis so the
line it flies threads the mouth.
**An all-AI lobby cannot win** (the Tollway rule, restated for speed):

| Hull | Autopilot speed | vs the swarm's 50 u/s cruise |
|---|---|---|
| Squirrel | 60 u/s (cruise) | can keep up |
| Sparrow | 35 u/s (cruise) | cannot keep up |
| Rhino | ramps on straights | can keep up |

### 3.5 Networking (the design's open question, settled)

The sim runs on the server and every peer follows it:
- **The server is authoritative.** It runs the director on its own swarm and replicates the form, goal, anchor,
  evolve progress and outcome. The match end and the scoring are the server's.
- **A client runs its own swarm** (fauna are client-local, the Brood Rush precedent). It takes the replicated form
  and goal, and is nudged toward the server's anchor: 20% of the gap per tick, at most 6 u, once past 25 u
  (`SwarmFauna.TryNudge`, the LOD's rigid move).

What this does not make identical:
- A client's individual tadpoles are its own: same animal, same place, different members.
- Kills land on each peer's own swarm through the replicated vessels and weapons.

Proving how far apart two peers' swarms drift is QA-TANDAVA-3.

### 3.6 The ascension: the dance ground, the drum and the ring of fire

The banked Bull (its offering is a whole body's worth of Mass, 15,510 at density 3, the stomach's 90% cap) **rises
into the dance form at once** through the ordinary commit (molts, never deaths; T2), and the cell turns to fire. It
swims to the **dance ground** at (1,075, 0, 400) assembling the figure on the way, so the statue and its patrols are
standing when the flames light; the drum is the pilots' window, not the shape's. The ground is beside the course,
between the sixth and seventh oases, clear of both by the ring's whole reach so the dance never stands in flora (the
generator asserts it). There:
1. **The ring of fire is placed once**, on the server: its centre is the dance ground plus the dance plan's baked ring
   offset in the swarm's body axes of that moment, and its plane is the body's (up, side) - the plane the statue
   stands in. Twelve flames sit on it, 133 u from the centre and 69 u apart, each a switch ring of mouth 24 u whose
   axis is the ring's TANGENT: a pilot circling the dancer threads flame after flame, against the patrol.
2. **The drum runs 30 s.** Every flame a pilot threads while it is **unguarded** goes out. A flame is guarded while at
   least 6 of the swarm's Time members (the attendants) are within 40 u of its **guard post**, 167 u from the centre
   straight out from the flame (the packs' patrol radius). The guard is read off the SERVER's swarm and replicated
   as a mask; a guarded flame's drawn ring gutters to 35% of its mouth, so the ring never advertises a mouth that is
   not open. Culling a pack unguards what it was guarding.
3. **Nine out (EndConditionOverrides `tandavaFlamesToBreak`) and the dance is broken**: the pilots win, the figure
   falls back into the Bull (a molt; nobody dies), and the cell eases back to its own colours. **The drum stops
   first**, and the swarm takes the Winged Lion: the cell eases back, the exit is open to it alone, and the pilots
   have its flight to the membrane (~27 s unopposed) to cut it below 35%.

The flame is the switch vocabulary's fourth verb, `ToySwitchSignal.Flame`: *this is a fire, thread it to put it out*,
painted the platform's danger red (`Docs/ToySystem/ARCHITECTURE.md`, "What a switch's SHADER says").

## 4. What the harness proves (`bash Tools/Build/swarm_core_harness/run.sh <plans> tandava <tandava plans>`)

The real sort core and the real director, with a food model that keeps the geometry `SwarmFauna.Feed` depends on:
each tick one biting member (`BitersPerStep 1`) bites only if it touches a plant with food.

The swarm's own metabolism is `SwarmFauna.Starving`'s rule (unfed 30 s while hungry, one member shed per 0.25 s, the
core choosing whom), and the ring of fire is the glue's (placed off the baked ring and the body axes, guarded by Time
members at the posts). Numbers from 2026-10-06:

| Test | Result |
|---|---|
| T1 scripted seed | hatches as the Young Serpent and holds it while it grows |
| T2 the walk | Young Serpent → Serpent → Great Serpent → Bull → Lord of the Dance → Winged Lion, each a `Switched` the next step. 0 self-inflicted deaths, the body never shrinks through a commit. Shape coverage after 30 s to re-sort: 98 / 97 / 100 / 70 / 80 % (stroke figures held to 65%, the blob forms to 70%) |
| T3 | killing 90% of the Mass never re-forms it |
| T4 | bad requests are ignored, and the majority mode ignores `RequestPlan` |
| T5 | a shipped (majority) swarm still morphs when its Time is killed |
| T6 unopposed | escapes **as the Winged Lion** at 239 s: forms at 40 / 88 / 126 s, the Bull rises into the dance at 162 s, the ring lights at 182 s, the lion at 212 s (exactly the drum after), 9 stops |
| T7 starvation | every oasis denied: never evolves and **starves** (64 s, 113 shed). All food burnt once it is the Bull: it cannot ascend and cannot leave - **starves** at the membrane (177 s) |
| T8 two oases denied | the race costs it 19 s |
| T9 | culling every member wins (Wiped); cutting the Winged Lion below 35% wins (Broken) |
| T10 the sealed exit | a serpent past the exit plane has not escaped and the membrane raises `Sealed`; its correction puts it back 60 u inside; the final form is never held. A swarm swimming straight at the exit for 150 s never crosses (14 sealed beats, at most -60 u from the plane) |
| T11 the ascension | the statue is assembled by the drum's end (73% over its last 5 s; 72% already when the ring lights, because it assembled on the way); the body axes drift 0° over the drum; 100% of the non-Time body stands inside the ring and 72% of the Time members patrol outside it; the attendants guard 26% of flame-seconds, and all 12 flames at some point (the guard patrols); the lion is assembled before it crosses (80%) |
| T12 breaking the dance | a lobby threading an unguarded flame every 1.5 s breaks the dance at 194 s, 9 of 9 tries landing; molted back to the Bull with 443/443 alive and 0 deaths; pilots who charge the guarded flames are repelled on 19 of 20 tries and only put out 1 |
| T13 the drum wins a slow lobby | one flame per 5 s puts out 6 before the drum stops; the swarm takes the lion and escapes |

**Negative control:** with the scripted branch disabled, T1-T3, T6, T8 and T9 fail.

`TANDAVA_DUMP=<dir>` writes the live body against the pose the core is steering to, at the drum's end and at the
lion's best, as JSON for a picture. Three things were found only that way: the dance plan briefly shipped at the
shared 6 steps a frame (the packs' wells spun a turn every 4.8 s, faster than a tadpole swims, and no Time member ever
reached a guard post); the lion's feathers smeared above its body (the wingbeat outran Space's top speed); and the
statue plateaued at 57-68% however long it was given, which was the sortfeel dead zone widening every stroke
(`SortWellDead 0` lifts it to 70-77%; more time alone did nothing).

**Sweep** (2026-10-06):
- Cruise 2.0 / 2.5 / 3.0 all hold T6-T9.
- Grazing every second tick fails T6: stops hit the cap and the swarm stalls a form short.
- Ships: cruise 2.5 (50 u/s), one biter every tick, a 45 s stop cap.

**What is not proved:** the real bite rate against a real budget-capped Borromean plant, frame time, the look, and
the network drift. The race times are a model, not a measurement.

## 5. Numbers and where they live

| Value | Where |
|---|---|
| route, forms, the dance's palette, narration, director dials, the seal and starvation, the dance ground, drum and ring of fire, client nudge | `Assets/_SO_Assets/Games/TandavaSettings.asset` |
| swarm (PlanDensity 3, cruise 2.5, biters 1, MacroLod off, MultiDomain off, StarvationSeconds 30 / ShedInterval 0.25, SortWellsPerType 24, SortWellDead 0, the six scripted plans and their periods 6/6/6/6/40/12) | `Assets/_SO_Assets/Swarm Fauna/Tandava/TandavaSwarmFaunaConfig.asset` |
| break threshold 35%; flames to break the dance 9 | `EndConditionOverrides.asset` `tandavaBreakPercent`, `tandavaFlamesToBreak` (FrogletTools > Game Modes > End Game Conditions) |
| 25 points a flame | `TandavaScoringRule.asset` `flamePoints` |
| cell, flora pens, ladder | `Assets/_SO_Assets/Cell Configs/Tandava Cell/` |

**Each form's bank** is the design's starting value: 40% of its body in banked Mass volume (Mass egg 40.31),
clamped to 90% of the stomach. The Bull's is the ascension's offering: a whole body's worth, which the cap holds to
15,509.9. The dance and final forms are taken at the dance ground, not eaten to.

| Form | Bank (Mass) | Stage | Meal |
|---|---|---|---|
| Young Serpent | 3,337.7 | 4,780.3 | 1,912.1 |
| Serpent | 5,224.2 | 5,477.6 | 2,191.0 |
| Great Serpent | 7,594.4 | 6,857.4 | 2,743.0 |
| Bull | 15,509.9 | 7,915.5 | 3,166.2 |
| Lord of the Dance | 0 | 0 | 1 (the field's floor) |
| Winged Lion | 0 | 0 | 1 |

The route holds 87,439 Mass at its floor (20 plants × 60 prisms) against the stages' 25,031, so a lobby that burns
nothing loses to an unopposed swarm, and one that burns well can starve it (T7) or slow it (T8).

**The ring of fire** (world, from the dance plan's baked ring × cbrt(3) × unit scale 2): radius 132.7, guard posts at
167.3, centre (0, 2.3, 0) in the body axes; 12 flames 69 u apart, mouth 24, guard radius 40, 6 Time members to guard.

**Each form's meal** is that form's stage over 2.5 (never below 1). A stage is three parts:
1. grow from the body it arrived with to 90% of its own, at its own mix's egg prices;
2. plus its bank;
3. less the previous form's bank, which the commit spends on exactly that growth.

The generator and `TandavaHarness.BuildForms` compute the same arithmetic.

## 6. Ecology invariants (the protocol)

| Law | Holds? |
|---|---|
| continuity of existence | yes: births bloom, molts re-form, the cell's colour eases, a client nudge is small |
| no imposed death | yes: the director cannot kill. Starvation is the swarm's own metabolism (`SwarmFauna.Starving`, as everywhere, at this config's pace); the membrane's starving clock and a broken dance are OUTCOMES, and a broken dance molts the figure back into the Bull |
| mass is conserved | yes: eggs are paid 1:1 from eaten flora |
| one colour at birth | yes: Tandava's swarm is ONE colour, the cell's hostile controller (`MultiDomain` off, §3.4). The Swarm cell's lineages are unchanged |
| shielded mass is never food | yes, and this is why the Bull's design diet (Charge flora) is NOT built (§9) |
| the Cell owns the environment | yes: oases are the cell's flora in planting pens, the tint recolours the cell's own visuals, and the mode owns only the exit plane |
| endogenous selection | a stated, user-requested exception: the FORM ORDER is scripted. The swarm still grows only by eating, and a form commits only when eating has filled it. |

**Colliders:**
- Always on: flora hearts, 24 at cap (20 Mass + 4 Space plants).
- With a vessel near: proxies, up to 2 × 160 for the one swarm.
- Worst case 344, under the 1,200 ceiling (asserted by the generator).

**Phase ladder** (the Swarm cell's ratios 0.35 / 0.26 / 2.5 / 2.2 against this cell's mature model: 1,440 forest
prisms + 160 proxies, 89,625 forest volume + the biggest body's 15,543): Restless 600 / 500 prisms and 37,000 /
28,000 volume, Frenzy 4,000 / 3,600 prisms and 263,000 / 232,000 volume.

## 7. Files

| File | Role |
|---|---|
| `Arcade/Tandava/TandavaDirectorCore.cs` | the stage director (pure C#; the harness runs it) |
| `Arcade/Tandava/TandavaController.cs` | the mode: director glue, replication, cell tint, the sealed exit on every peer, the ring of fire, AI chasers, game end |
| `Arcade/Tandava/TandavaFlame.cs` | one flame of the ring of fire: a `Flame` switch ring, swept-segment crossing test, gutters while guarded |
| `Arcade/Tandava/TandavaSettingsSO.cs`, `TandavaObjectiveProvider.cs` | settings; the HUD arrow (points at the swarm) |
| `Arcade/Scoring/TandavaScoringRuleSO.cs`, `Arcade/TurnMonitors/TandavaTurnMonitor.cs` | outcome-based rule (culls + flames); the turn ends on the outcome, and the display follows the story: "Serpent 62%", "Bull - to the dance ground", "Ring of fire 4/9 - drum 12 s", "Bull - starving 23 s", "Winged Lion" |
| `Environment/CellVisualTint.cs` | the cell's colour transition (platform capability) |
| `Environment/FloraAndFauna/Swarm/ISwarmDirector.cs` | the director seam on `SwarmFauna` |
| `SwarmSortCore.cs` (`Scripted`, `RequestPlan`, `Major`), `SwarmTickJob.RequestPlan`, `SwarmPlanLibrary.LoadScripted` | the scripted form list |
| `Tools/Build/tandava_plans.py`, `Tools/Build/author_tandava_assets.py` | the plans; every asset (`--check`; `--self-test` runs the scene checks on the donor scene, where all 16 must fire) |
| `Tools/Build/swarm_core_harness/TandavaHarness.cs` | T1-T13 |
| `Assets/_Scenes/Multiplayer Scenes/MinigameTandava.unity` | a one-shot clone of `MinigameBroodRush`: the controller, monitor, cell and four RANDOM-hull AI templates swapped, pilots on a start line at x = -2,300 facing the course. When the donor moves on, the generator keeps the committed scene and still validates it |
| `Assets/_SO_Assets/Cell Configs/Tandava Cell/` | the cell config, spawn profile, the swarm species and three Borromean flora forks, one planting pen per oasis |
| `Assets/_SO_Assets/Games/ArcadeGameTandava.asset` | the arena card: Rhino, Squirrel, Sparrow; 1-6 players seated as 3 (one per hull); one domain |
| `Assets/_Graphics/ARCADE/CardBackgrounds/Tandava.png` | the card backdrop, rendered MODEL tier by `render_card_backgrounds.py`: the oases as Borromean glyphs in their pens, the Great Serpent's own plan at glyph scale, three pilots, the cell in its OWN colours (it changes only for the dance). The icons are the shared placeholder pair (`arcade_mode_lib.CARD_ART`) |

## 8. Platform changes (and the rule each records)

- **`SwarmSortParams.Scripted` + `IScriptedSwarmCore`.** A plan index is not an element. Every element-semantic read
  of `PlanIx` (inflation, the frame period, the Time dead zone, the lay and molt majority guard) now reads `Major`.
  General rule: *when an index has been doubling as a meaning, name the meaning before you let the index grow.*
- **`ISwarmDirector` / `SwarmFauna.SetDirector`.** A mode steers a population by registering per config; nothing in
  the swarm names the mode.
- **`CellVisualTint` + colour overrides on `CapsuleMembrane` / `SnowChanger` + `Cell.MembraneVisual` / `NucleusVisual`
  / `CytoplasmVisual`.** A mode may change how the cell's own visuals LOOK. It never edits a shared material, and
  never touches what they DO.
- **`CellConfigDataSO.CytoplasmShardDistance`.** The mote count is a cube of the radius, so it is a per-cell number.
- **`ToySwitchSignal.Flame` (the vocabulary's fourth verb) + `ToyFactory.FireRed`.** A ring that is a FIRE to put out.
  `ToySwitchVocabularyTests` now also asserts every unreserved verb reads as itself (0.5 summed channel distance
  between each pair), because a fire that read as "thread me next" would send a pilot into the guarded ring.
- **`SwarmFauna.CountMembersNear(world, radius, element)` and `BodyForward / BodyUp / BodySide`.** A mode may ask
  where a population's members are and how its body is turned; it still cannot move or kill one.
- **`TandavaDirectorCore.SealCorrection`.** A boundary a population may not cross is one pure rule every peer applies
  to its own copy, never a server correction the clients chase.

## 9. Next phases (from the design, in its order)

1. **Run phases A and B in the editor** (the design's own prerequisite: the Swarm cell has not run in Unity either):
   QA-SWARM-* first, then QA-TANDAVA-*. Nothing is drawn at the exit: the exit is a plane the director tests and the
   peers hold the swarm behind. A visible sealed membrane (a gameplay-bearing structure, the mode's own) comes with
   the first playtest's readability notes.
2. **The Bull's diet.** The design has its horns crack shielded Charge flora. That is the locked "shielded mass is
   never food" law, so it needs the owner's sign-off and a named exception before it is built.
3. **Region overrides:** a queued command API on the core (a limb's wells onto a pilot for N ticks, a region's tier,
   the body goal) applied on the worker beside the kills. This gives the tactic chooser its verbs: limb strike,
   shield wall, whole-body charge.
4. **Severing:** a region split into its own `SwarmFauna` (decoy or hunter), within the proxy and collider ceilings.
5. ~~**The ascension**~~ (phase B, §3.6). Built differently from the design's sketch: the attendants are four packs
   INSIDE the dance plan (the core carries them round by its frames, no split), the flames are switch rings rather
   than tadpole clusters, and the statue is the shipped sort core at 24 wells rather than a special one-frame mode.
6. **The tactic chooser:** threat meter, Thompson sampling per tactic, cooldowns, a grudge target with a visible tell.
7. **The Winged Lion hunts** (phase B built its body, its open exit and its 35% break): a hunting goal blend and
   feeding from combat.
8. **Variety:** a seeded temperament genome, a route seed, one announced mutation per match.
