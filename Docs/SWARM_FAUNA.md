# Swarm Fauna — a creature made of creatures

**Status: authored headless, NEVER run in the Unity editor.** Read "Verification status" before
trusting anything below that describes behaviour on screen. Branch: `cece/swarm-fauna-game`.

A **swarm** is a population of tadpoles that swims as one large animal. Every tadpole is a real
lifeform — one elemental heart, one spindle, one body prism — and the swarm's **body plan** is
chosen by its **majority element**:

| majority | creature | tadpoles at full size | element mix of the full body (Charge/Mass/Space/Time) |
|---|---|---|---|
| Mass | whale | 192 | 19 / 126 / 25 / 22 |
| Charge | pufferfish | 179 | 131 / 1 / 16 / 31 |
| Space | jellyfish | 88 | 25 / 2 / 54 / 7 (the 25 Charge bell members wear shields) |
| Time | dragonfly | 76 | 2 / 3 / 18 / 53 |

Kill enough of a swarm's majority and another element leads; after a short hysteresis the
swarm **morphs** into that element's creature, in motion, with every surviving tadpole swimming
to a new home in the new body.

It ports the research "field" model (`Tools/NCA/` on `cece/gifted-curie-x2cpd0`,
`field_swarm.py`) into the game, plus the three things the research did not have: laying that is
**funded by eating**, **starvation**, and **swimming through a real cell**.

---

## 1. Architecture

```
SwarmFauna (heartless population anchor, a Fauna — the worm-colony shape)
 ├─ SwarmFieldCore            pure C#, System.Numerics, SoA arrays — ALL behaviour
 │    ├─ SwarmPlanData x4     baked body plans (8 frames of slot positions + facings)
 │    └─ SwarmFieldParams     every behaviour constant (research values + game additions)
 ├─ fixed-step clock          TickHz (10) steps/s, MaxStepsPerFrame (3), render interpolates
 └─ SwarmTadpoleFauna[]       one per live tadpole — a real Fauna with NO behaviour of its own
       heart (LifeFormCrystal) · spindle · one HealthPrism body
```

| file | role |
|---|---|
| `Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/SwarmFieldCore.cs` | The simulation. No `UnityEngine` reference; compiled and RUN headless by `Tools/Build/swarm_core_harness`. |
| `.../Swarm/SwarmFauna.cs` | The anchor. Owns the core, the clock, member spawn/pose, vessel sensing, feeding, starvation, extinction. |
| `.../Swarm/SwarmTadpoleFauna.cs` | One member. Heart, body shape, tier (danger/shield), death paths. No `Update`. |
| `.../Swarm/SwarmFaunaConfigSO.cs` | Every number. One asset serves every swarm. |
| `.../Swarm/SwarmPlanLibrary.cs` | Loads the four plan `TextAsset`s via `JsonUtility`, caches them. |
| `Assets/_SO_Assets/Swarm Fauna/Plans/SwarmPlan_*.json` | The baked plans (`Tools/Build/swarm_plans.py`). Research units. |
| `Assets/_SO_Assets/Swarm Fauna/SwarmFaunaConfig.asset` | The config. |
| `Assets/_Prefabs/FloraAndFauna/SwarmFauna.prefab`, `SwarmTadpole.prefab` | Anchor + member. The tadpole is derived from `TadPoleFauna.prefab` with its NetworkObject, NetworkTransform, FaunaNetworkSync, FMOD emitters and embedded crystal stripped. |
| `Assets/_SO_Assets/Cell Configs/Swarm Cell/` | The cell, its spawn profile, three swarm fauna configs, three flora configs. |
| `Tools/Build/author_swarm_fauna.py` | **Owns every asset above** (`--check`). Hand edits are drift. |

**Why a pure-C# core and no Burst/Jobs.** The step is dominated by greedy slot assignment
(sorting candidate costs) and boids neighbour loops over ≤192 units; measured on .NET (CoreCLR)
it costs **0.95 ms/step** for the 179-unit pufferfish, **0.85** whale, **0.14** jellyfish,
**0.11** dragonfly. At 10 Hz that is ≤10 ms per second of game time per swarm, i.e. ~1 ms of
frame budget amortised. That did not justify a Burst port (whose `NativeArray` plumbing would
also have made the core un-runnable headless). **Mono in a player will be slower than CoreCLR —
re-measure with the Profiler** (`QA-SWARM-FAUNA` step 9). The SoA layout is Burst-ready if it is
ever needed.

**Clock.** `SwarmFauna.Update` accumulates `Time.deltaTime`, runs whole steps at `TickHz`, and
renders every member at `lerp(prev, current, alpha)` for position and facing. Behaviour is
therefore frame-rate independent; a hitch drops time after `MaxStepsPerFrame` rather than
cascading.

**Units.** The core runs in the research's units ("voxels", per step). `UnitScale` (2) converts
to world units at the boundary, so the whale is ~130 world units long and adjacent tadpoles sit
~5 units apart — a body made of individually shootable pieces.

### 1.1 What the core does each step (in order)

1. **Plan choice** — the majority element of the live, non-molting members is the candidate
   plan; it must lead for `Dwell` (12) consecutive steps before the swarm commits. While a
   candidate is pending the swarm is **contested**: it neither lays nor molts.
2. **Morph** — on commit, a `MorphSteps` (60) vortex blends from the old body to the new, homes are
   re-solved, and a `SettleSteps` (300) window opens (see Molting).
3. **Slot assignment** — greedy over sorted costs, with stickiness (4) and an element-mismatch
   penalty (60), every `ReassignEvery` steps.
4. **Predators** — sensed vessels startle members inside `Sense` with a lookahead; startle relays
   through the body and decays. Per-element **flee** (Charge 0.6, Mass 0.5, Space 1.4, Time 2.0).
   Time members **mob** a ship that is loitering (speed < `MobSpeed`). Charge members **inflate**
   with threat (pufferfish).
5. **Boids + arrive** — separation (2.0), alignment (5.0), arrive-to-home with speed cap.
6. **Swimming** — the anchor turns toward `SwimTarget` at ≤`Turn` rad/step, cruises at `Cruise`,
   and is clamped to its radial band and inside the membrane. The body basis follows the heading,
   so the creature swims nose-first.
7. **Molting** (only inside the settle window) and **laying** (funded — below).

Events (`Laid`, `MoltBegan`, `MoltDone`, `Switched`) are the only things the glue reacts to.

---

## 2. Invariants touched (ecology protocol)

| invariant | how this holds it |
|---|---|
| **Continuity of existence** | A newborn tadpole blooms from scale 0.001 over `BirthBloomSeconds`; a molting heart shrinks away and re-forms over `MoltHeartSeconds`; a body prism re-shape is a `ChangeSize()` grow on the prism clock; a dead member's husk shrinks out over 0.45 s; the body prism is left as a skeleton or suctioned into an eater. Nothing is `Destroy`ed while visible. |
| **No imposed death** | No lifespan, no timer cull. The only self-inflicted death is **starvation**: after `StarvationSeconds` (90) with no meal the swarm sheds one member every `ShedIntervalSeconds` (4), each withering to its crystal. Feeding resets the clock. An extinct anchor (no members, no heart, no body) removes itself after `ExtinctLingerSeconds` so the seeder can hatch a fresh swarm — extinction recovery, the same as every species. |
| **One-colour spawning** | Every member wears the swarm's ONE domain (the cell's controlling colour, stamped on the anchor by the standard spawner). **Conservative choice, deliberately:** the research gives each slot a domain as well as an element; that was dropped. Elements vary within a swarm — elements are not domains — so "a creature made of four elements" is legal, but "a creature made of three teams" would be the cross-domain spawning the invariant forbids. The three swarms differ by starting ELEMENT MIX, not by colour. Revisit only with sign-off. |
| **Shielded mass is never food** | Every bite routes through `Fauna.IsShieldedMass` and `IsPreyForMe`. Consequence worth knowing: **Charge flora are inedible** (Charge plant leaves are armoured, `Docs/ECOSYSTEM.md §35`), so the cell plants no Charge flora — it would be a feeding ground nothing can graze. |
| **A creature dies when its last body prism is destroyed** | Each tadpole has exactly one body prism, so destroying it kills that tadpole through `Fauna.OnBodyPrismExploded` → sealed `Die`. The swarm itself is a population, not a creature. |
| **Every lifeform drops one crystal** | Every tadpole carries its own heart (the worm-colony ruling, `ECOSYSTEM.md §23.3`); the anchor is heartless (`ProvisionHeart` is overridden to record the start element only). Heart sizes are the canonical Tadpole species' band values. A heart caught mid-molt is restored to full size before any death path releases it, so a molt can never make a kill pay less. |
| **Mass is conserved** | Laying is funded: an egg costs eaten volume (below). A starved member leaves its prism as a skeleton; a killed one leaves its prism to whatever killed it. |
| **Clock-material law** | No per-frame material/scale writes on prisms. Member poses are TRANSFORM writes (a mover), each followed by `NotifyBodyPrismsMoved()` — the mover contract that keeps `PrismSpatialIndex` honest. Shape changes are one `ChangeSize()` stamp. Tier changes are state changes (`MakeDangerous`, `ActivateShield`, `DeactivateShields`). |
| **Cell owns the environment** | No parallel spawner. Three ordinary `FaunaConfigurationSO`s (one per swarm) with per-species `BandInner/OuterRadius` in an ordinary `SpawnProfileSO`, seeded by the cell's own spawner. The swarm's anchor is clamped to that band. |
| **Replication seam** | `SwarmTadpole.prefab` carries NO NetworkObject; members are client-local like every freestyle creature. `SpawnMember` still routes through `FaunaNetworkSync.ServerSpawn`, so `check_fauna_replication_seam.py` holds for this producer. |
| **FMOD** | `SwarmLoopEvent` (one loop per swarm, at the anchor) and `MorphEvent` are exposed `EventReference`s, **shipped empty**. Silent until the audio owner authors them. |

**Danger.** A Charge member startled past `DangerEnter` spikes its plate to DANGER (the
pufferfish's spines), relaxing below `DangerExit`. Danger prisms hurt any vessel that rams them,
including the swarm's own domain (locked design). There is no "make safe" API; the revert clears
`IsDangerous` and calls `DeactivateShields()` (the reverse of `MakeDangerous`'s two writes).

**Molting — flagged.** A member whose element mismatches its home slot may **molt** into the
slot's element: its body prism re-forms, its heart shrinks away and re-grows as the new element.
The research molts continuously. In the game, molting runs **only inside the settle window after
a committed morph**, because continuous molting turned out to defeat the mechanic it exists for:
measured, a swarm whose majority was being killed simply molted minority members into the
majority and shrank, never morphing. Molting remains a transform of a lifeform's element without a
death, which no other species does — it is kept because it is what makes a morph read as the body
re-arranging itself rather than a population replacing itself, and it is visibly animated. If it is
judged to violate "a lifeform is its species and its element" (`ECOSYSTEM.md §40`), set
`SettleSteps` to 0 and the swarm will morph by laying alone (slower, and the new body is mostly
newborns).

---

## 3. Feeding, laying and starvation

Every tadpole may bite: `BitersPerStep` (96) members per step, round-robin, query
`PrismSpatialIndex.QuerySphere` within `BiteRadius` (10 world units). A prism is food when it is a
flora `HealthPrism` (never another fauna's body, never a dying plant), not shielded, and
`IsPreyForMe` (outside the nucleus every domain is prey). The bite is a `Consume` toward the biter
(the suction death visual), and its **volume** lands in the swarm's stomach **under the plant's
element**.

An egg of element *e* costs `EggVolume[e]` of element-*e* food — authored at the tadpole body
prism's own volume (Charge 20.4, Mass 40.3, Space 22.2, Time 12.8), so eaten mass converts to
swarm mass 1:1. If the stomach lacks *e*, the egg can be paid out of OTHER elements' food at
`CrossElementCost` (2) times the price. Which element is laid is decided by the body plan (the
element with the largest deficit that still has a parent); food decides only whether it can be
afforded. A full stomach (`StomachEggs`, 240 eggs) stops grazing. Laying runs at `LayRate` 0.2 of the
headcount per step, at most `LayMax` 16 per step (10 Hz), whenever the stomach can pay.

**Hatching is budgeted, laying is not.** A laid member is alive in the sim at once, but its
GameObject is queued and given a body under `MaxSpawnsPerFrame` (48), a budget shared by EVERY
swarm in the cell — so a whole cell of swarms seeding together, or a burst of laying, never puts
more than 48 `Instantiate`s in one frame. A queued member is simply not drawn and cannot be hit
or bite until it hatches (normally the same or next frame).

**A wounded swarm holds its eggs** (`KillLayHoldSeconds`, 2 s). Every kill postpones laying, so a
burst of kills is a window the body cannot refill. At the overtuned lay rate this is load-bearing:
without it a fed dragonfly survives 80 Time kills in 4 s unchanged; with it, 53 kills in the same
burst morph it into a jellyfish (harness test 3c, both halves asserted).

Goal-seeking: the swarm swims to the nearest living flora heart inside its band (via
`FloraHeartRegistry`), and a plant it has sat beside for 10 s without landing a bite is
remembered as barren for 45 s. With nothing to eat it wanders to a point in its band at least
`WanderReach` away.

---

## 4. The Swarm cell

`Assets/_SO_Assets/Cell Configs/Swarm Cell/Swarm Cell Config.asset` — **no `EnvironmentPrefab`**,
its own spawn profile, the Arboretum's membrane / nucleus / cytoplasm (nucleus world radius ~392),
appended to `Menu_Main` → Cell → `CellConfigs[12]`, so it appears in the freestyle **Cell
Selector** toy (a bare station: like Lattice and the Arboretum, a grown world has no scale model).

| band (world radius) | swarm starts as | flora to eat (floor / cap) | food relation |
|---|---|---|---|
| inner 430–600 | **whale** (Mass) | Arbor, Mass (48 / 100) | own element — grows fast, hardest to convert |
| middle 660–840 | **dragonfly** (Time) | Spire, Space (64 / 140) | cross — grows at half rate, and Space food is the jellyfish's element |
| outer 900–1120 | **pufferfish** (Charge) | Frond, Time (80 / 160) | cross — grows at half rate |

Bands are disjoint, so the three populations stay separated in the cytoplasm. Each band holds a
SCHOOL of swarms: `InitialSpawnCount / PopulationSize / MaxLivePopulation = 8`; a new swarm
hatches with `SeedMembers` (96, clamped to its plan's size) tadpoles at its plan's own mix and grows by eating. Flora use the shipped
canonical phyllotactic species through `ElementPalette` + per-cell band overrides; they reproduce
(`GrowthPerOffspring` = 0.8 × budget) so a grazed feeding ground regrows.

**Volume ladder — MODELLED, not measured.** RestlessEnter/Exit 4,453,000 / 3,308,000, FrenzyEnter/Exit
31,805,000 / 27,988,000 (counts 26,800 / 19,900 / 191,200 / 168,300), derived by
`author_swarm_fauna.py` from a model of the mature cell (~76,468 prisms, ~12,721,711 volume):
Restless early, Frenzy above the mature cell. The phyllotactic per-prism volume in that model is
an approximation (leaf cross-section × segment). **Re-measure in the editor** (FrogletTools >
Ecology > Measure Cell Environment Baselines, then a few minutes of growth) and re-author.

### 4.1 Collider budget (the hard gate)

| | always-on | notes |
|---|---|---|
| tadpole hearts at cap | **4,608** | 24 swarms × 192 (each swarm's cap is the largest plan; a swarm only reaches it as a whale) |
| flora hearts at cap | **400** | 100 + 140 + 160 |
| **total always-on** | **5,008** | **4.6× the Lattice cell's 1,080** — see the overtune note below |
| tadpole body prisms | ≤4,608 | ordinary prism colliders, one per member |

**OVERTUNE PASS (user-authorized: "the next order of magnitude across the board").** This cell
deliberately breaks the collider budget every other cell is held to: 5,008 always-on hearts
against the Lattice cell's 1,080, and a mature cell (~76k prisms) above Atlantis' ~69k. The author
script's gate is restated as an explicit `OVERTUNE_HEART_CEILING` (6,000), so it still fails if the
numbers drift further. **It is unprofiled.** The dials to bring it back, cheapest first:
`SWARMS_PER_BAND` (each swarm is up to 192 hearts), the flora caps, then `SeedMembers`/`LayRate`.

Realistic load is lower: a dragonfly is 76 and a jellyfish 88. The cell authors no environment
prisms at all.

---

## 5. In-editor test steps (exact)

1. Open `Assets/_Scenes/Bootstrap.unity` and press Play (or Play from Bootstrap as usual); let it
   reach **Menu_Main**. Optional but recommended first: **FrogletTools > Toolbox > Logging**, enable
   the **Ecology** channel — the swarm logs `[Swarm] … hatched as …` and `[Swarm] … morphs X -> Y`.
2. Take control: **click the centre of the screen** (gamepad **Y**). You are in freestyle.
3. Fly to the **Cell Selector** toy (a ring near the membrane, World group). Fly through it; a row
   of stations blooms outward. Fly into the station for **Swarm** (a bare station — no miniature).
   The world suctions out and the Swarm cell grows in behind the loading veil.
4. Wait ~6 s (`InitialFaunaSpawnWaitTime`). Twenty-four swarms of up to 96 tadpoles hatch, eight in each band,
   and flora seed around them. **Look for:** each swarm already reads as a sparse ghost of its
   creature; it swims nose-first; newborn tadpoles grow in, never pop.
5. **Feeding:** watch a swarm reach a plant. Prisms suction off the plant into tadpoles; the
   swarm fills toward its full creature (whale fastest — it eats its own element).
6. **Vessel reaction:** fly through a body without firing. Members near your path scatter ahead of
   you and the startle ripples through the body; Space/Time members flee hardest. Near the
   pufferfish, Charge members puff up and their plates turn **danger** (do not ram them).
   **Hover still** next to the dragonfly: its Time members converge on you (mobbing).
7. **Killing:** switch to a gun vessel with the **Vessel Changer** toy (Sparrow is easiest) and
   shoot tadpoles. Each kill: the body prism dies, the heart drops a collectable crystal, the
   husk shrinks away, and the body re-arranges around the hole. Every tadpole is killable by
   any active force (ram, gun, missile, blast).
8. **Morph:** the cheapest morph is the **dragonfly** (middle band): kill **~36 of its 53 Time
   members** (it turns into a jellyfish — Space leads). The whale needs ~102 Mass kills (→
   jellyfish); the pufferfish ~101 Charge kills (→ dragonfly). Kill quickly: a swarm sitting on
   food re-lays its majority (measured: the same kills spread over time against a fed swarm never
   convert it). **Look for:** after the last kill, ~1.2 s of hesitation (the swarm stops laying),
   then the body swirls into the new creature while swimming; mismatched members visibly molt
   (heart shrinks, re-grows in the new element's colour/shape, body prism re-forms). The Ecology
   log prints the morph.
9. **Profiler:** with the swarms grown, record `SwarmFauna.Update` cost per frame.
10. **Starvation (long):** a swarm that cannot reach food (e.g. after its plants are grazed out)
    begins shedding members after 90 s; each withers to a crystal and leaves its body prism.

---

## 6. Verification status

What was proved, and how:

- **`SwarmFieldCore.cs` is compiled and RUN** by `Tools/Build/swarm_core_harness/run.sh` (Roslyn
  `csc`, .NET 8): 33 assertions — growth to each plan, funded laying (an empty stomach lays
  nothing; a fixed meal lays exactly what it pays for; cross-element eggs at double price),
  selective-kill morphs for all four plans, homeostasis on food, vessel reaction, mobbing,
  swimming with shape held, band clamp.
- **The five Swarm files type-check** (`Tools/Build/swarm_glue_typecheck/run.sh`) against a
  hand-written stub of every Unity and first-party member they touch. Every first-party stub
  signature was then checked by grep against the real source (Fauna, Prism, HealthPrism, Crystal,
  LifeForm, Cell, PrismSpatialIndex, FloraHeartRegistry, FaunaNetworkSync, LifeFormCrystal,
  AudioSystem, CSLogChannel). This proves internal consistency against that stub, **not** a
  compile against the real assemblies.
- `author_swarm_fauna.py --check`: every asset matches its model; GUIDs owned by exactly one
  `.meta`; bands disjoint, outside the nucleus, inside the membrane; hearts under the Lattice
  ceiling; the tadpole prefab stripped of networking and FMOD emitters with its local references
  intact; Menu_Main carries the config.
- Standing gates green (conditional compilation, enum members, switch labels, using directives,
  self-referential locals, duplicate attributes, abstract members, fauna replication seam,
  heart sizes). `check_console_logging` reports one pre-existing finding in
  `TrainingSessionRunner.cs`, not on this branch.

**NOT verified — needs the editor:** that it compiles in Unity; that the prefabs import and the
tadpole's spindle/heart/prism hierarchy is right after the strip; everything on screen in §5; the
modelled volume ladder; Mono frame cost; the Cell Selector station. Tracked as `QA-SWARM-FAUNA`
in `Docs/QA/QA_BACKLOG.md`.

---

## 7. Findings for the research

Things the game port taught, for whoever iterates the field model:

1. **Tick rate.** 10 Hz with interpolation is enough: the research's step constants (boids,
   arrive, startle decay) are all per-step, so they carry over unchanged and only `TickHz` maps
   them to seconds. Running the sim at frame rate would have made every constant frame-rate
   dependent. Facing must be interpolated too, or members visibly tick-rotate.
2. **Scale.** At 2 world units per voxel a 192-unit whale is ~130 units long with ~5-unit slot
   spacing: shootable pieces, still one silhouette at arena range. Smaller scales make tadpoles
   overlap their own hearts.
3. **Continuous molting defeats morphing.** With molting always on, killing the majority made the
   body molt minority members into the majority and shrink; it never morphed (measured). Molting
   only inside a post-commit settle window (300 steps) fixed it and is the single most important
   game-side change to the model.
4. **Homeostasis on food.** A swarm on its own food re-lays its majority faster than a steady
   player kills it: 100 Time kills spread over 200 steps against a fed dragonfly produced 0
   switches. A morph is therefore a burst-damage objective — an emergent difficulty dial set by
   the feeding ground, not a tuned number.
5. **Funded laying makes the feeding ground the strategy layer.** Cross-element eggs at 2× mean
   a swarm grows at half rate on foreign food, and food of a rival element is effectively a slow
   push toward that element (the dragonfly grazing Space flora). The research's free laying
   (0.04 / 4) was first replaced by LayRate 0.02 / LayMax 2 gated on the stomach; the overtune pass
   runs 0.2 / 16, which only stays morphable because kills hold laying (test 3c).
6. **Stomach cap.** Without one, a grown swarm strips its feeding ground for nothing; capping at
   24 eggs (240 after the overtune pass) leaves grazing pressure proportional to growth.
7. **Kills needed** (from the harness): dragonfly 36 Time → jellyfish; jellyfish 30 Space →
   pufferfish; whale 102 Mass → jellyfish; pufferfish 101 Charge → dragonfly. Small creatures are
   the accessible morphs; the big ones are bosses.
8. **Startle helps avoidance measurably:** a straight ship pass touched 2% / 1% / 0% of members
   (whale / puffer / dragonfly) with predator reaction on, vs 5% / 3% / 3% inert.
9. **Mobbing works:** a loitering ship had 25.7 tadpoles within two ship radii with mobbing on
   vs 2.3 off.
10. **Dragonfly shape error** (median 4.0 voxels) comes from the wing runners lapping faster than
    `VMax`; excluding runners the body holds its shape. A higher `VMax` for runners, or slower wing
    frames, would tighten it.
11. **Domain slots were dropped** (one-colour invariant, §2). If the research wants multi-domain
    bodies, that is a fundamentals conversation, not a parameter.
12. **What has NOT been learned** (needs play): whether the morph reads as one creature
    becoming another at arena range; whether the swim speed (`Cruise` 0.35 voxels/step = 7
    world u/s) reads as alive or sluggish next to 60+ u/s vessels; whether a 10 Hz pose is smooth
    enough under fast startle flight.
