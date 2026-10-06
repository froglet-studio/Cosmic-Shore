# Tandava

`GameModes.Tandava = 62` · ARENA card · Squirrel, Sparrow, Rhino · co-op (every pilot on one domain) · scene
`MinigameTandava` · assets owned by `Tools/Build/author_tandava_assets.py` (`--check`).

**Status: authored headless, NOT yet run in the Unity editor.** Everything below that describes behaviour is from the
headless harness (`Tools/Build/swarm_core_harness`, mode `tandava`), a Roslyn type-check, and the generators. QA:
`QA-TANDAVA-1..4` in `Docs/UNITY_VERIFICATION_CHECKLIST.md` (§ Tandava; `/verify-unity` could not run: no Unity editor or
`unity` CLI in the authoring session).

## 1. The pitch

A tadpole swarm hatches at one end of a long cell and races for the exit membrane at the other. It stops at oases to
eat, and every time its body is full and its stomach holds the surplus, it takes its next **form**. **The cell
changes with it**: at the moment of each form the membrane, the nucleus and the cytoplasm bloom and ease into that
form's colours. Pilots chase it, cull it, and burn the oasis ahead so it cannot feed. They win by wiping it out,
starving it, or cutting its final form below the break threshold. They lose when it crosses the exit, and the loss is
scored by the form it escaped as, so a lost stage is still worth fighting.

Only the mode carries the name. The forms, the narrator and every UI string use plain English names.

## 2. The forms (phase A)

| # | Form | Body (plan / in game at density 3) | Element mix | What changes |
|---|---|---|---|---|
| 1 | Young Serpent | 69 / 207 | Mass 72 · Charge 7 · Space 9 · Time 12 % | the first form; it hatches as this |
| 2 | Serpent | 108 / 324 | Mass 72 · Charge 7 · Space 9 · Time 11 % | the same head and hood, a longer tail |
| 3 | Great Serpent | 157 / 471 | Mass 71 · Charge 7 · Space 10 · Time 11 % | longer again |
| 4 | Bull | 154 / 462 | Mass 53 · Charge 26 · Space 5 · Time 16 % | coils fold into a horned, four-legged body |

The names are what the HUD ("Serpent 62%") and the narrator say. The plans are keyed `serpent_s` / `serpent_m` /
`serpent_l` / `bull` (`Assets/_SO_Assets/Swarm Fauna/Tandava/SwarmPlan_tandava_<kind>.json`).

- **Serpent**: Mass coils (bulk, food store, what grows), a Charge **hood** of shield-tier plates flared round the
  head, Space rods along the back (the shimmer band), and a Time tail rattle. A lateral travelling wave carries it.
  It grows by adding stations at the TAIL, so a small → medium → large commit reads as the snake getting longer.
- **Bull**: a hollow Mass body with a hump, a Charge brow and two long horns, four Time legs on a diagonal gait,
  and a Space neck bell and tail.

The plans are generated procedurally by `Tools/Build/tandava_plans.py`, in the same JSON schema as the Swarm cell's
four elemental plans and through the same element identity clamp (`swarm_plans.identity`). It asserts:
- the design's shares, to within 3.5 points;
- both regions present, so lineages can colour back and belly;
- only Charge carries a tier;
- every pair of units at least 2.1 voxels apart in every frame (the sort core's collision radius is 2.25).

**Not in phase A** (§9): the ascension set piece (the Dancer statue, the attendant packs and the ring of fire) and
the final Beast (a lion-bird of eight legs, wings and talons).

## 3. How it works

```
TandavaController  (MultiplayerDomainGamesController + ISwarmDirector, on every peer)
 ├─ SERVER: TandavaDirectorCore  (pure C#: route, forms, outcome)        <- the swarm's state, once per tick
 │     ├─ route: oasis 1 .. 8, then the exit; a denied oasis is skipped
 │     ├─ form: body >= 90% of its plan AND the bank holds the surplus -> commit the next form
 │     └─ outcome: escaped | wiped | starved | broken
 │   replicated: form, goal, anchor, progress, outcome (NetworkVariables) + narrator lines (ClientRpc)
 ├─ every peer: SwarmFauna (the swarm, client-local like all fauna) asks the director where to hatch and swim,
 │   and a CLIENT's swarm takes the replicated form and is nudged toward the server's anchor
 └─ every peer: CellVisualTint on the cell - eases membrane / nucleus / cytoplasm to the form's palette
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
3. **Move on.** After the last oasis it sprints for the exit.

An oasis with no edible flora left is **denied** and skipped. The server re-checks this each second.

### 3.3 The cell changes with the swarm

`CellVisualTint` is a new, mode-agnostic Cell capability. It eases the cell's own spawned membrane, nucleus and
cytoplasm to a `CellPalette` and back. It changes how they **look**, never gameplay: the boundary, the nucleus
control radius and the phase are untouched. It recolours draws, never assets:

| Visual | How it is recoloured | Why |
|---|---|---|
| membrane | a property block on its instanced draw (`CapsuleMembrane.SetColourOverride`) | its `SpindleMaterial` is shared with every spindle |
| nucleus | property blocks on its renderers | per-renderer, no material copy |
| cytoplasm | ONE per-cell material clone (`SnowChanger.SetColourOverride`) | one write a frame however many motes |

At the GO and at each form commit, every peer's cell blooms (up to 2.5× bright, gone by mid-ease) and eases over 3 s
into that form's palette:
- **Serpent**: teal / emerald / jade, deepening with size.
- **Bull**: ember.
- **Escape**: crimson ("the cycle ends too soon").
- **Pilots win**: the cell eases back to its own colours over 5 s.

The trigger is the replicated form index, so every peer changes at the same moment. Idle, the tint costs nothing.

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
- A pilot's score is members culled (`ScoringMetric.LifeformsKilled`, attributed kills only).
- The match result is the swarm's outcome, not a metric race.
- `MinDomainsAllowed = MaxDomainsAllowed = 1`: every pilot and AI teammate flies one domain. It is the first co-op
  arena card.

The AI: `ArmChasers` points every AI pilot at the swarm, led 40 u toward its goal and spread ±45 u across the body.
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

## 4. What the harness proves (`bash Tools/Build/swarm_core_harness/run.sh <plans> tandava <tandava plans>`)

The real sort core and the real director, with a food model that keeps the geometry `SwarmFauna.Feed` depends on:
each tick one biting member (`BitersPerStep 1`) bites only if it touches a plant with food.

| Test | Result |
|---|---|
| T1 scripted seed | hatches as the Young Serpent and holds it while it grows |
| T2 the walk | Young Serpent → Serpent → Great Serpent → Bull, each a `Switched` the next step. 0 self-inflicted deaths, the body never shrinks through a commit (the Bull molts ~90 members). Shape coverage after 30 s to re-sort: 98 / 98 / 96 % |
| T3 | killing 90% of the Mass never re-forms it |
| T4 | bad requests are ignored, and the majority mode ignores `RequestPlan` |
| T5 | a shipped (majority) swarm still morphs when its Time is killed |
| T6 unopposed | escapes as the **Bull** at 142 s, forms committed at 41 / 85 / 126 s, 8 stops |
| T7 every oasis denied | never evolves, escapes as the Young Serpent at 80 s (the weakest loss) |
| T8 two oases denied | escapes a form short (Great Serpent) at 133 s |
| T9 | culling every member wins (Wiped); cutting the Bull below 35% wins (Broken) |

**Negative control:** with the scripted branch disabled, T1-T3, T6, T8 and T9 fail.

**Sweep** (2026-10-06):
- Cruise 2.0 / 2.5 / 3.0 all hold T6-T9.
- Grazing every second tick fails T6: stops hit the cap and the swarm stalls a form short.
- Ships: cruise 2.5 (50 u/s), one biter every tick, a 45 s stop cap.

**What is not proved:** the real bite rate against a real budget-capped Borromean plant, frame time, the look, and
the network drift. The race times are a model, not a measurement.

## 5. Numbers and where they live

| Value | Where |
|---|---|
| route, forms, palettes, narration, director dials, client nudge | `Assets/_SO_Assets/Games/TandavaSettings.asset` |
| swarm (PlanDensity 3, cruise 2.5, biters 1, MacroLod off, MultiDomain off, the scripted plans) | `Assets/_SO_Assets/Swarm Fauna/Tandava/TandavaSwarmFaunaConfig.asset` |
| break threshold 35% | `EndConditionOverrides.asset` `tandavaBreakPercent` (FrogletTools > Game Modes > End Game Conditions) |
| cell, flora pens, ladder | `Assets/_SO_Assets/Cell Configs/Tandava Cell/` |

**Each form's bank** is the design's starting value: 40% of its body in banked Mass volume (Mass egg 40.31),
clamped to 90% of the stomach.

| Form | Bank (Mass) | Stage | Meal |
|---|---|---|---|
| Young Serpent | 3,337.7 | 4,780.3 | 1,912.1 |
| Serpent | 5,224.2 | 5,477.6 | 2,191.0 |
| Great Serpent | 7,594.4 | 6,857.4 | 2,743.0 |
| Bull | 0 | 0 | 1 (the field's floor) |

The route holds 87,439 Mass at its floor (20 plants × 60 prisms) against the stages' 17,115, so a lobby that burns
nothing loses to an unopposed swarm, and one that burns well can hold it a form or more short (T7, T8).

**Each form's meal** is that form's stage over 2.5 (never below 1). A stage is three parts:
1. grow from the body it arrived with to 90% of its own, at its own mix's egg prices;
2. plus its bank;
3. less the previous form's bank, which the commit spends on exactly that growth.

The generator and `TandavaHarness.BuildForms` compute the same arithmetic.

## 6. Ecology invariants (the protocol)

| Law | Holds? |
|---|---|
| continuity of existence | yes: births bloom, molts re-form, the cell's colour eases, a client nudge is small |
| no imposed death | yes: the director cannot kill; starvation is the host's clock as everywhere |
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
| `Arcade/Tandava/TandavaController.cs` | the mode: director glue, replication, cell tint, AI chasers, game end |
| `Arcade/Tandava/TandavaSettingsSO.cs`, `TandavaObjectiveProvider.cs` | settings; the HUD arrow (points at the swarm) |
| `Arcade/Scoring/TandavaScoringRuleSO.cs`, `Arcade/TurnMonitors/TandavaTurnMonitor.cs` | outcome-based rule; the turn ends on the outcome, and the display shows "Serpent 62%" |
| `Environment/CellVisualTint.cs` | the cell's colour transition (platform capability) |
| `Environment/FloraAndFauna/Swarm/ISwarmDirector.cs` | the director seam on `SwarmFauna` |
| `SwarmSortCore.cs` (`Scripted`, `RequestPlan`, `Major`), `SwarmTickJob.RequestPlan`, `SwarmPlanLibrary.LoadScripted` | the scripted form list |
| `Tools/Build/tandava_plans.py`, `Tools/Build/author_tandava_assets.py` | the plans; every asset (`--check`; `--self-test` runs the scene checks on the donor scene, where all 16 must fire) |
| `Tools/Build/swarm_core_harness/TandavaHarness.cs` | T1-T9 |
| `Assets/_Scenes/Multiplayer Scenes/MinigameTandava.unity` | a one-shot clone of `MinigameBroodRush`: the controller, monitor, cell and four RANDOM-hull AI templates swapped, pilots on a start line at x = -2,300 facing the course. When the donor moves on, the generator keeps the committed scene and still validates it |
| `Assets/_SO_Assets/Cell Configs/Tandava Cell/` | the cell config, spawn profile, the swarm species and three Borromean flora forks, one planting pen per oasis |
| `Assets/_SO_Assets/Games/ArcadeGameTandava.asset` | the arena card: Rhino, Squirrel, Sparrow; 1-6 players seated as 3 (one per hull); one domain |
| `Assets/_Graphics/ARCADE/CardBackgrounds/Tandava.png` | the card backdrop, rendered MODEL tier by `render_card_backgrounds.py`: the oases as Borromean glyphs in their pens, the Great Serpent's own plan at glyph scale, three pilots, the cell in the Great Serpent's colours. The icons are the shared placeholder pair (`arcade_mode_lib.CARD_ART`) |

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

## 9. Next phases (from the design, in its order)

1. **Run phase A in the editor** (the design's own prerequisite: the Swarm cell has not run in Unity either):
   QA-SWARM-* first, then QA-TANDAVA-*. Phase A draws nothing at the exit: the exit is a plane the director tests. A
   visible exit (a gameplay-bearing structure, the mode's own) comes with the first playtest's readability notes.
2. **The Bull's diet.** The design has its horns crack shielded Charge flora. That is the locked "shielded mass is
   never food" law, so it needs the owner's sign-off and a named exception before it is built.
3. **Region overrides:** a queued command API on the core (a limb's wells onto a pilot for N ticks, a region's tier,
   the body goal) applied on the worker beside the kills. This gives the tactic chooser its verbs: limb strike,
   shield wall, whole-body charge.
4. **Severing:** a region split into its own `SwarmFauna` (decoy or hunter), within the proxy and collider ceilings.
5. **The ascension:** attendant packs (~40% split into 3-5 hunters), the Dancer statue (one frame; `SortNoise 0`,
   `SortWellDead 0`, higher adhesion), 12 flame clusters, the drum countdown, and both outcomes.
6. **The tactic chooser:** threat meter, Thompson sampling per tactic, cooldowns, a grudge target with a visible tell.
7. **The final Beast:** a hunting goal blend, feeding from combat, the 35% break.
8. **Variety:** a seeded temperament genome, a route seed, one announced mutation per match.
