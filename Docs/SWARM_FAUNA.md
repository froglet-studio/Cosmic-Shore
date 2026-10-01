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

**There are two species of swarm, on two simulation cores** (§8): the **field** swarm
(`SwarmFieldCore`, designed attractor fields — every tadpole owns a slot) and the **grid** swarm
(`SwarmGridCore`, the research's `hgrid2` grid morphogen — nothing assigns a tadpole a place; each
reads only fields at its own position). One config field picks the core
(`SwarmFaunaConfigSO.Model`), and the Swarm cell hosts both side by side so they can be compared in
one session: **grid whales inside, field dragonflies in the middle, grid pufferfish outside**.

---

## 1. Architecture

```
SwarmFauna (heartless population anchor, a Fauna — the worm-colony shape)
 ├─ ISwarmCore                what the glue drives; config.Model picks the implementation:
 ├─ SwarmFieldCore  | SwarmGridCore   pure C#, System.Numerics, SoA arrays — ALL behaviour
 │    ├─ SwarmPlanData x4     baked body plans (8 frames of slot positions + facings)
 │    └─ SwarmFieldParams     every behaviour constant (research values + game additions)
 ├─ fixed-step clock          TickHz (10) steps/s, MaxStepsPerFrame (3), render interpolates
 └─ SwarmTadpoleFauna[]       one per live tadpole — a real Fauna with NO behaviour of its own
       heart (LifeFormCrystal) · spindle · one HealthPrism body
```

| file | role |
|---|---|
| `Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/SwarmFieldCore.cs` | The FIELD simulation. No `UnityEngine` reference; compiled and RUN headless by `Tools/Build/swarm_core_harness`. Also holds the shared plan data (`SwarmPlanData`, `SwarmPlanJson`) and events. |
| `.../Swarm/SwarmGridCore.cs` | The GRID simulation (research `hgrid2`, §8). Same constraints, same harness. |
| `.../Swarm/ISwarmCore.cs` | The interface the glue drives, and `SwarmModel` (Field / Grid). |
| `.../Swarm/SwarmFauna.cs` | The anchor. Owns the core, the clock, member spawn/pose, vessel sensing, feeding, starvation, extinction. |
| `.../Swarm/SwarmTadpoleFauna.cs` | One member. Heart, body shape, tier (danger/shield), death paths. No `Update`. |
| `.../Swarm/SwarmFaunaConfigSO.cs` | Every number, including `Model` and the grid model's parameters. One asset per MODEL serves every swarm of that model. |
| `.../Swarm/SwarmPlanLibrary.cs` | Loads the four plan `TextAsset`s via `JsonUtility`, caches them. |
| `Assets/_SO_Assets/Swarm Fauna/Plans/SwarmPlan_*.json` | The baked plans (`Tools/Build/swarm_plans.py`). Research units. |
| `Assets/_SO_Assets/Swarm Fauna/SwarmFaunaConfig.asset`, `SwarmGridFaunaConfig.asset` | The two configs (`Model: 0` field, `Model: 1` grid); identical except the model. |
| `Assets/_Prefabs/FloraAndFauna/SwarmFauna.prefab`, `SwarmGridFauna.prefab`, `SwarmTadpole.prefab` | The two anchors (one per config) + the shared member. The tadpole is derived from `TadPoleFauna.prefab` with its NetworkObject, NetworkTransform, FaunaNetworkSync, FMOD emitters and embedded crystal stripped. |
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
| **No imposed death** | No lifespan, no timer cull. (The GRID model's research rule withers misplaced surplus on a hunger clock; in the game hunger only picks the starvation victim — §8.3.) The only self-inflicted death is **starvation**: after `StarvationSeconds` (90) with no meal the swarm sheds one member every `ShedIntervalSeconds` (4), each withering to its crystal. Feeding resets the clock. An extinct anchor (no members, no heart, no body) removes itself after `ExtinctLingerSeconds` so the seeder can hatch a fresh swarm — extinction recovery, the same as every species. |
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

| band (world radius) | model | swarm starts as | flora to eat (floor / cap) | food relation |
|---|---|---|---|---|
| inner 430–600 | **grid** | **whale** (Mass) | Arbor, Mass (48 / 100) | own element — grows fast, hardest to convert |
| middle 660–840 | **field** | **dragonfly** (Time) | Spire, Space (64 / 140) | cross — grows at half rate, and Space food is the jellyfish's element |
| outer 900–1120 | **grid** | **pufferfish** (Charge) | Frond, Time (80 / 160) | cross — grows at half rate |

The models alternate by band so that a pilot flying outward meets grid, field, grid; the author
script FAILS if the cell stops hosting both.

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

**The grid model changes none of these numbers.** A grid swarm is the same population of the same
tadpoles under the same 192 cap: one heart collider and one body prism per member, so a grid whale
at full size is **192 hearts + 192 body prisms**, exactly like a field whale. What it adds is CPU
and memory (§8.5).

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

### 5.1 Comparing the field swarm and the grid swarm (exact)

The cell hosts both models (§4): the **inner** band (430–600 u from the centre) holds GRID whales,
the **middle** band (660–840 u) FIELD dragonflies, the **outer** band (900–1120 u) GRID pufferfish.
The Ecology log line names the model: `[Swarm] SwarmGridFauna(Clone) (Grid) hatched as mass …`.

11. **Find one of each.** From the Cell Selector (near the membrane, ~980 u out) fly inward. The
    first creatures you reach are grid pufferfish; keep going past ~840 u for the field dragonflies;
    past ~600 u for the grid whales. Park alongside one of each for 20–30 s.
12. **What to look for — the grid's look.** A grid body is a *school that happens to be a whale*:
    it forms loosely from its knot and then sharpens; members jostle for places (two that want one
    site push it between them and one drifts on); the outline is right, the interior shuffles.
    The field body is crisper: every tadpole sits on its slot, members move in near-lockstep, and
    when it is wrong it is wrong *mechanically* (a member stuck, a ripple of identical motion).
    Note which reads as alive at gameplay distance — that is the question the lead is answering.
13. **Morph both.** Kill the majority of a field dragonfly (~36 Time) and of a grid pufferfish
    (~100 Charge; or wait for a grid swarm to become a dragonfly first). The field swarm hesitates
    ~1.2 s (its `Dwell`), then swirls into the new body with members visibly MOLTING. The grid swarm
    commits on the kill that tips the majority (no dwell; a 3 s `GridPlanLock` stops it flickering
    back), never molts, and re-forms by members CLIMBING to where the new plan wants their element;
    the surplus element drifts to the edges and stays (it is not killed — §8.3).
14. **Fly through both** without firing. Both scatter the same way (the grid core reuses the field
    core's vessel reaction), so any difference you see is the body, not the reaction.
15. **Profiler:** `SwarmFauna.Update` for a grown grid whale vs a grown field dragonfly (headless
    CoreCLR: ~1.2 ms vs ~0.1 ms per step; §8.5). With 16 grid swarms and 8 field swarms grown, record
    the total frame time — this cell is the overtune pass and is unprofiled.

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

- **`SwarmGridCore.cs` is compiled and RUN** by the same harness (`GridHarness.cs`, 33 more
  assertions): research-mode growth of every plan; game-mode growth, funded laying, selective-kill
  morphs on all four plans with exactly one commit, the wounded-swarm lay hold (G4b), hunger that
  never kills in game mode and picks the starvation victim, vessel reaction, swimming, station-
  keeping (G7b), band, cost.
- **SwarmGridCore is scored with the research's UNCHANGED scorer** (`score_grid.py`: exported
  states → `swarm_nca.decode` → `swarm_nca.swarm_loss`, default `LossCfg`) next to the Python
  hgrid2 itself over 8 seeds — §8.5. Raw output: `Tools/Build/swarm_core_harness/score_grid_results.txt`.
- The glue type-check now covers `ISwarmCore.cs` and `SwarmGridCore.cs` too (seven files).

**NOT verified — needs the editor:** that it compiles in Unity; that the prefabs import and the
tadpole's spindle/heart/prism hierarchy is right after the strip; everything on screen in §5; the
modelled volume ladder; Mono frame cost; the Cell Selector station. Tracked as `QA-SWARM-FAUNA`
in `Docs/QA/QA_BACKLOG.md`. For the grid model additionally: that a grid swarm reads as a creature
on screen at all, its Mono frame cost with 16 grid swarms grown, and the side-by-side comparison
(§5.1) — `QA-SWARM-GRID`.

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
13. **A body hovering on its goal must not chase its own jitter.** Both cores re-aimed the body at
    its swim target every step; a swarm grazing a plant has its centre jittering around that plant,
    so the FIELD creature spun ~1,000° a minute while it fed (measured over 600 hovering steps) and
    the GRID body, rebuilt every step around a frame turning a few degrees, smeared (whale own-plan
    loss 8.1 → 5.6 once fixed). Fixed in both (`AimHold`): the field core holds its heading inside
    half a body radius; the grid core, whose centre is not pinned by any anchor, holds its heading
    inside 1.5 radii and STATION-KEEPS there (sidles back without turning). Harness 6b / G7b assert
    < 5° a minute. **If the research ever adds swimming, this is the first thing it will hit.**

### 7.1 Findings from the grid species (hgrid2 in the game)

14. **The port reproduces hgrid2.** Research mode (the model exactly as the research runs it) lands
    in hgrid2's own range under the unchanged scorer (§8.5: own-plan means 7.9 / 4.7 / 4.3 / 7.7 vs
    Python's 6.7 / 4.6 / 5.0 / 7.4 over 8 seeds) and its feel metrics match to the second decimal
    (jerk_rel 0.598 vs 0.597, coherence 0.565 vs 0.571, jitter 1.094 vs 1.098). The whale sits ~1.2
    above Python's mean on every seed while the pufferfish sits ~0.6 below; no port difference was
    found that explains either (the plan data were checked to 5e-4, the dynamics line by line). Worth
    a look from the research side: the whale is at the bar's edge in BOTH implementations.
15. **One domain makes the grid model easier, not harder.** Collapsing the three domain slots to
    one (the game's one-colour law) means a tadpole competes with every same-ELEMENT tadpole for the
    plan's sites instead of only its own domain's; scored against the one-domain plan, game mode
    lands at 5.1 / 1.6 / 3.4 / 3.9 with 32/32 passes. The domain sort was the hard part of hgrid2.
16. **The grid model needs no dwell.** The field core waits `Dwell` (12 steps) before committing a
    new majority; hgrid2 commits the step a new element leads. In the game that is fine with a
    lock (`GridPlanLock`, 30 steps = 3 s) to stop a near-tie flickering — harness G4: one commit,
    no flip-back, on all four plans.
17. **Without molting, a morph leaves a surplus that does not go away.** The research corrects a
    morph's composition by withering misplaced surplus tadpoles (hunger). The game cannot (§8.3), and
    the grid model never molts, so after a morph the old majority's leftovers drift to the edge of
    the new body and stay until something eats or shoots them — e.g. a jellyfish of 100 where the
    plan holds 88. It reads (to us, headless) as debris clinging to a new creature; whether that is
    organic or a bug is a judgement for play.
18. **Feel, in numbers** (`swarm_feel.metrics`, mean over plans and seeds, grown bodies): the field
    core in the game is the "too clean" model the lead described — coherence 0.776 (lock-step),
    jitter 0.656 (rigid), jerk_rel 0.130, and **9.4% of members stuck** (a bug-like freeze) — while
    the grid core is 0.574 / 1.023 / 0.572 / 0.000 stuck, matching Python hgrid2. The metric that
    separates them most is `stuck`: the field model's mistakes are frozen members, the grid's are
    members still searching.
19. **Cost.** hgrid2's grid work is per STEP, not per member: ~0.6 ms/step for a 76-tadpole
    dragonfly and ~1.2 ms for a 192-tadpole whale on CoreCLR, against the field core's 0.1 and 0.9.
    The fixed part is the 16³ grid (splat, blur, deficits); the plan fields are cached per plan and
    frame (770 KB a frame). A research model that wants to be cheaper in the game should shrink G or
    run the coarse grid every other step.

---

## 8. The second species: the grid swarm (`SwarmGridCore`, research `hgrid2`)

The lead's verdict on the field model: *"the transitions are fun, but it has lost too much organic
imperfection; its mistakes feel like bugs, not emergence."* On the grid morphogen family: *"an
excellent direction: both accurate and organic."* Its second round, **hgrid2**, was the first LOCAL
model to pass the research's tier 1 under the loss-8 bar. This is that model, in the game.

### 8.1 How it differs from the field swarm

| | field (`SwarmFieldCore`) | grid (`SwarmGridCore`) |
|---|---|---|
| where a tadpole goes | a SLOT the swarm assigns it (greedy assignment over sorted costs) | nowhere in particular: it climbs its class's DEFICIT field |
| what it reads | its slot's position and velocity | only fields at its own position |
| shape control | arrive-to-slot + boids | a coarse 16³ grid of class deficits riding the body + a fine per-class morphogen (a Gaussian bump per wanted site minus one per live tadpole of the class) + feed-forward of the plan's own motion |
| composition | molting (a member changes element) in a settle window + laying toward the plan's element deficit | laying INTO its class's deficit (chance ∝ the deficit; a quarter take the most-wanted element); never molts |
| plan switch | majority must lead `Dwell` steps; then a swirl morph | commits the step a new majority leads; held by a 3 s lock |
| imperfection | rare and mechanical (a stuck member; lock-step motion) | constant and local (members jostle for sites, the interior shuffles) |

Everything else is shared, unchanged: funded laying, the stomach, feeding, starvation, swimming in
a band, the vessel reaction (the grid core reuses the field core's startle / flee / mob / inflate
layer verbatim), the kill path, hearts, crystals, continuity of existence, the glue, the prefab.

### 8.2 One step

1. **Plan** — the live majority element names the plan (`hgrid_boid.decide_plan`); a switch emits
   `Switched` unless the current plan is still inside its `Lock`.
2. **Coarse grid** — the plan frame's field (wanted density per element × slot, plus the looks:
   prism, Charge tier, facing, spindle), rasterised once per (plan, frame) and cached, is mapped to
   (element × DOMAIN) through the slot→domain map; the live tadpoles are splatted per class and
   blurred; deficit = wanted − actual.
3. **Desire** — each tadpole: `KClass` × its own class's deficit gradient (only if the plan has room
   for its class anywhere) + `KTotal` × the all-class deficit gradient + a home pull if it sits
   where nothing is wanted + noise.
4. **Vessel reaction** (game) — startle / relay / flee / mob from the field core; startle damps the
   desire and raises the top speed. Then persistence and the element's top speed.
5. **Swim** (game) — far from its goal the body swims nose-first; near it, it station-keeps (§8.4).
6. **Collision + membrane** (`swarm_nca`'s designed boid), **looks** eased toward the field's
   attributes, **hatching**, **hunger**.
7. **Laying** into the deficits (`hgrid_boid._lay`), funded and held while wounded (game).
8. **Fine morphogen** (`hgrid2_model.fine_disp`) — the per-class bump field, its gain ramped by how
   settled the body is, plus feed-forward; clamped at the element's top speed.

### 8.3 Ecology decision: hunger never kills (no imposed death)

hgrid2 corrects composition by STARVING misplaced surplus: a tadpole whose class the whole swarm
holds 15% more of than the plan, sitting where its class is not wanted, gains hunger 0.04 a step
and withers at 1.0 — about 2.5 seconds at 10 Hz. Run in the game, that is a **timer cull of a fed
animal**: nothing about it is starvation (the swarm may have a full stomach), and CLAUDE.md's
no-imposed-death invariant forbids it (`/ecology`: populations are bounded by consumption and
starvation, never an imposed clock).

**Decision (conservative):** the grid core still COMPUTES hunger exactly as hgrid2 does, but in the
game (`SwarmGridParams.HungerKills = false`, what `SwarmFauna` always builds) **hunger never kills
anything**. The ONLY self-inflicted death remains the host's starvation: when the swarm has gone
`StarvationSeconds` without a meal, it sheds one member every `ShedIntervalSeconds`, and hunger
only decides WHO — the hungriest (most misplaced surplus) member first
(`ISwarmCore.StarvationVictim`). That is starvation in the plain sense (the swarm is unfed) with the
research's rule picking the victim, and it holds the law with no exception. The research behaviour
is kept behind `HungerKills = true` for the harness only (research mode); the event it raises
(`Starved`) is still routed to the sealed death path so it could never be silent.

**Cost, stated:** after a morph, the old majority's surplus is not culled — it drifts to the edges
and persists (finding 17). A fed grid swarm can therefore carry more members of an element than
its plan wants; laying never adds to a class with no deficit, so the overshoot is bounded by what
the morph left behind, and the cap (192) still holds.

### 8.4 What the game changes in hgrid2, and why

| | research | game | why |
|---|---|---|---|
| domains | three slots, mapped to domains | ONE domain — every slot → domain 0 | one-colour fauna law (§2) |
| hunger | kills at 1.0 | picks the starvation victim only | §8.3 |
| laying | free | funded (`TryFund`, the field core's rule) + `GridLayMaxPerStep` 3 + the wounded-swarm hold (`KillLayHoldSeconds`) | mass is conserved; a morph must stay reachable against a fed swarm (G4b) |
| plan lock | 0 | 30 steps (3 s) | the grid commits on the tipping kill; a near-tie must not flicker |
| frame | fixed world axes | the plan turns to the body's heading | the creature swims nose-first |
| grid centre | snapped to whole cells | the exact centroid | a swimming body would see its target jump a cell at a time |
| goal | — | far: swim along the heading toward it; within 1.5 radii: hold heading, station-keep | finding 13 |
| seed | 16 in a knot | `SeedMembers` (96) in a knot, clamped to the plan's size | the overtune pass |
| capacity | 280 | 192 (the largest plan) | the collider budget is stated per member |
| vessel reaction | none | the field core's predator layer | the brief; and so a feel comparison compares bodies, not reactions |

### 8.5 Proof

**Accuracy** — `python3 Tools/Build/swarm_core_harness/score_grid.py` (the shipped C# is compiled,
run, and its exported states scored by the research's unchanged `swarm_nca.swarm_loss`, default
`LossCfg`; bar 8). Own-plan loss per seed 7, 23, 41, 108, 209, 310, 411, 512:

| | whale | jellyfish | pufferfish | dragonfly |
|---|---|---|---|---|
| **Python hgrid2** (reference, same seeds) | 6.66 (6/8 pass) | 4.61 (8/8) | 4.95 (8/8) | 7.44 (5/8) |
| **C# grid, research mode** (the port check) | 7.93 (3/8) | 4.74 (8/8) | 4.33 (8/8) | 7.72 (5/8) |
| **C# grid, game mode** (what ships; one-domain plan) | 5.14 (8/8) | 1.59 (8/8) | 3.35 (8/8) | 3.87 (8/8) |

Research mode is the apples-to-apples check: same seed protocol (16 tadpoles, randn × 2), same
240 steps, same scorer. Game mode differs in the ways §8.4 lists (notably a 96-tadpole seed and
one domain) and is scored against the one-domain version of each plan (every unit's slot set to 0 —
a different Target handed to the same function), because the domain term would only measure that
the game dropped domains. The brief's own-plan reference (6.1 / 4.8 / 5.5 / 7.2, seed 7, 3 samples)
is a different seed protocol from the per-seed table above; the Python column is the like-for-like.

**Feel** (`swarm_feel.metrics` on a 64-step window of each grown body, mean over plans and seeds):

| | speed | jerk_rel | planar | coherence | jitter | phase | stuck | osc |
|---|---|---|---|---|---|---|---|---|
| Python hgrid2 | 0.198 | 0.597 | 0.419 | 0.571 | 1.098 | 0.821 | 0.000 | 0.033 |
| C# grid, research | 0.197 | 0.598 | 0.428 | 0.565 | 1.094 | 0.822 | 0.000 | 0.034 |
| C# grid, game | 0.209 | 0.572 | 0.460 | 0.574 | 1.023 | 0.546 | 0.000 | 0.031 |
| C# **field**, game | 0.404 | 0.130 | 0.500 | 0.776 | 0.656 | 0.777 | **0.094** | 0.028 |

**Cost** (CoreCLR, Release, grown bodies, game mode): whale ~1.2 ms/step, pufferfish ~1.1, jellyfish
~0.7, dragonfly ~0.6 — against the field core's 0.9 / 0.9 / 0.15 / 0.1 and Python hgrid2's ~13. At
10 Hz a grid whale is ~12 ms of CPU per second. **The overtuned cell runs 16 grid swarms**: grown,
that is ~0.2 s of CPU per second on CoreCLR before Mono's slowdown — a real frame-time risk, and
the reason `QA-SWARM-GRID` asks for a Profiler capture. Dials, cheapest first: `SWARMS_PER_BAND`,
`GridSize` (12 roughly halves the grid work), `TickHz`. Memory: the plan fields are cached per plan
and per frame, shared by every swarm — 770 KB a frame, 6.2 MB a plan, built on first use.

**Colliders at full size:** unchanged from the field swarm — **one heart collider and one body
prism per member**, cap 192, so a full grid whale is 192 + 192 and the cell's ceiling is the same
5,008 always-on hearts (§4.1).

**Asserted** (harness `G1`–`G9`, all green): research growth of every plan; game growth to ≥ 85% of
every plan with its own majority; an unfed swarm lays nothing and a meal lays only what it pays for;
selective kills morph all four plans with ONE commit; a fed swarm under a kill burst morphs only
with the lay hold; hunger never kills in game mode and picks the starvation victim; the vessel
reaction (the pufferfish's threat rises); swimming to a target; station-keeping (< 5° a minute);
the band.

### 8.6 Files

| file | role |
|---|---|
| `Assets/.../Swarm/SwarmGridCore.cs` | the core and `SwarmGridParams` |
| `Assets/.../Swarm/ISwarmCore.cs` | the interface; `SwarmModel` |
| `Assets/_SO_Assets/Swarm Fauna/SwarmGridFaunaConfig.asset` | `Model: 1` + the grid parameters |
| `Assets/_Prefabs/FloraAndFauna/SwarmGridFauna.prefab` | the grid anchor (references the grid config) |
| `Tools/Build/swarm_core_harness/GridHarness.cs` | the asserted tests + the state export |
| `Tools/Build/swarm_core_harness/score_grid.py` | the unchanged-scorer comparison (needs torch; reads the research code off its branch) |
| `Tools/Build/swarm_plans.py` | now also bakes each unit's slot and every frame's prism / tier / spindle (the grid's coarse field rasterises them) |
