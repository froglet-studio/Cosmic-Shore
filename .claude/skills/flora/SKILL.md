---
name: flora
description: Use for ANY new Cosmic Shore FLORA species, or a change to how one is authored - a new Flora subclass or growth family, a flora prefab, the four per-element FloraConfigurationSO assets, a per-plant prism budget or leaf/plate size, a species' planting band, flora reproduction (GrowthPerOffspring, the lattice colony cycle), a flora heart size, or wiring a species into a SpawnProfile or the Lifeform Matrix toy. Loads the four growth families and how to choose between them, the field-ownership map (which of five tools owns which asset field), the measure/author/verify tool trio every new species ships with, and the traps that have actually cost passes. Trigger when editing Assets/_Scripts/Controller/Environment/FloraAndFauna/*Flora*.cs, Assets/_Scripts/Controller/Assemblers/**, Assets/_Prefabs/FloraAndFauna/*Flora*.prefab, Assets/_SO_Assets/Lifeforms/* Flora *.asset, or Tools/Build/*flora*.py.
---

# Flora Species Protocol

You are adding or changing a **plant**. Flora are one half of the food web, so this sits inside the
ecology's locked invariants — **read `/ecology` first and restate which invariants the change
touches**. This skill is the layer on top: it says how a *species* is built, who owns each authored
number, and what has to be measured before anything is authored.

The reference implementation to copy is **`MandelbulbFlora`** — the newest family, and the only one
that shipped with the full tool trio from day one. Its record is `Docs/ECOSYSTEM.md §44`.

---

## 1. Pick the growth family FIRST — it decides everything downstream

A flora species is a **growth rule**, not a model: every flora prefab carries exactly ONE prism (the
seed) and the plant is whatever its rule lays. There are four families and they are not
interchangeable. Choosing wrong is the single most expensive mistake available here, because the
family decides how the species reproduces, whether its prism size may ever change, and whether it
needs a bond table.

| family | the rule | reproduction | prism size | needs |
|---|---|---|---|---|
| `BranchingFlora` | stochastic branch walk toward a goal | per-plant growth quota | authored `leafSize`, uniform | nothing |
| `PhyllotacticFlora` | growing TIPS + golden-angle whorls | per-plant growth quota | per ROLE (stem spans its segment, leaf its reach) | nothing |
| `AssembledFlora` | crystallise a LATTICE from a measured bond table | **colony cycle** — one birth per `Cell.CurrentFaunaSpawnPeriod` for the whole population | fixed by the lattice, absolute units | an exact tile, a measured bond table, a claim book, a frontier |
| `MandelbulbFlora` | discover a SURFACE defined by a function, on the ambient integer lattice | per-plant growth quota | fixed by the lattice pitch | a membership predicate, nothing baked |

**The question that picks the family: where does the shape come from?**

- From the plant's own *walk* → Branching (scribble in bulk) or Phyllotactic (a silhouette: trunk,
  crown, creeper, rosette). Neither needs any offline work.
- From a *periodic or quasiperiodic tiling* → `AssembledFlora`. Only if the surface genuinely HAS an
  exact intrinsic tile. `Docs/ECOSYSTEM.md §34` is emphatic: a square-ish marching walk across a
  curved surface accumulates drift, fronts arriving from different directions disagree, and the
  occupancy key degrades to a quantized float. Gyroid, Schwarz P and the icosahedral quasicrystal
  each have their own tile and use it.
- From a *function* with no intrinsic tile at all (a fractal boundary, an implicit surface, a level
  set) → the `MandelbulbFlora` shape: address in the **ambient** `Vector3Int` lattice, decide
  membership by a pure function of three integers. This keeps the property §34 actually cares about
  — **sameness is an integer address** — without inventing the fitted grid §34 forbids. Say so
  explicitly in the class doc, or the next reader will file it as the mistake.

**Prefer extension to addition.** A fifth family must earn its place: state the three things the
existing four cannot express, and what it composes with (see CLAUDE.md's fundamentals-curation
process). Four families for ~17 species is already a lot.

---

## 2. Ownership: five tools author flora fields, and two must never own one field

This is the trap that costs a whole pass, because the loser silently wins on whoever ran last. Know
which tool owns what **before** you write an asset.

| field | owner | notes |
|---|---|---|
| `PopulationSize`, `MaxLivePopulation`, `GrowthPerOffspring`, `OffspringPerBirth` | `Tools/Build/author_flora_populations.py` | …except configs it hands off by name in `OWNED_ELSEWHERE`. It only reaches `_SO_Assets/Lifeforms` configs whose prefab is in `LATTICE_PREFABS`; everything else there is owned by that species' own generator. |
| `Variant.HeartWorldScale` | `Tools/Build/author_lifeform_heart_sizes.py` | Solves the whole fleet's band at once (`K · bodyDiameter^0.5`, largest lands on `HEART_MAX`). **Register a new species in its `FLORA_PREFABS`** or its heart silently falls back to the set default and stops tracking body size — the exact non-monotone defect it exists to fail the build on. |
| a shielded (CHARGE) species' leaf size | `Tools/Build/fit_shield_clearance.py` | for the two lattice species. A new species may fit its own, but then say so in both docstrings. |
| Schwarz P plate sizes | `Tools/Build/fit_schwarz_p_leaf_sizes.py` | |
| everything else about one species | that species' own `Tools/Build/author_<species>_flora_assets.py` | |

**If your generator rewrites a whole asset, it must CARRY THROUGH every field another tool owns** —
read the existing file, extract the field, re-emit it. Dropping it is worse than fighting over it,
because the asset silently falls back to a default and nothing reports the change.
`author_mandelbulb_flora_assets.py` does this for `HeartWorldScale`; copy that shape.

Hand off **by name, never by an exclusion set** — an `EXCLUDE` is invisible in the output, so the
next reader cannot tell "deliberately owned elsewhere" from "forgotten".

---

## 3. What a new species ships with

Three scripts, and they are three because they answer three different questions. Do not collapse
them.

1. **`Tools/Build/measure_<species>_flora.py` — the MODEL.** A fresh transcription of the growth
   rule, independent of the C#. It is the authority for prism counts, per-element volume, the
   fitted prism size, and any claim the C# comments make. Give it `--check` (fail the build on
   drift), `--render DIR` (PNGs — you cannot judge a plant you have not looked at), and a flag
   per measured claim.
2. **`Tools/Build/verify_<species>_flora_tables.py` — the TRANSCRIPTION CHECK.** The step neither
   the measurement nor code review can see. The gyroid paid five playtests for this gap. It must
   **compile and RUN the shipped C#**, not parse it — see §4 — and it must have `--self-test` that
   mutates the shipped file and asserts the gate trips. *A gate nobody has watched fail is a gate
   nobody should trust.*
3. **`Tools/Build/author_<species>_flora_assets.py` — the ASSETS.** Prefab, the four element
   configs, the `.cs.meta` files (stable guids: a script committed without its meta gets a fresh
   guid on every clone and every prefab reference to it dangles), and the roster row. `--check`,
   and idempotent.

---

## 4. Compile and RUN the shipped C#, offline

You cannot open Unity, and a syntax-only check proves almost nothing about a `MonoBehaviour` (see
CLAUDE.md on why Roslyn abandons class-body binding when the base type is unresolved). But the
*growth math* of a good species is a **pure file** — no `MonoBehaviour`, no Unity types beyond
`Vector3`/`Vector3Int` — and a pure file can be compiled against a stub and executed:

```
Tools/Build/mandelbulb_lattice_harness/{Stubs.cs,Driver.cs,run.sh}     # copy this
Tools/Build/regatta_course_harness/                                    # the original precedent
```

`run.sh` needs a per-user dotnet 8 SDK (`bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh)
--channel 8.0 --install-dir $HOME/.dotnet --no-path`, ~40 s). **Split the pure math into its own
file so this is possible** — `MandelbulbLattice.cs` holds membership, adjacency and orientation and
knows nothing about plants; `MandelbulbFlora.cs` holds the Unity wiring. That split is what turns
"I think this is right" into a proof, and it costs nothing.

Then run the standing gates, which DO cover the wiring half:
`check_conditional_compilation.py`, `check_using_directives.py`, `check_enum_member_references.py`,
`check_switch_label_collisions.py`, `check_self_referential_locals.py`, `check_console_logging.py`.

---

## 5. Fit the prism — do not eyeball it, and remember CHARGE is a different question

A prism's size is a geometric claim about a specific point set (this species' own measured sites,
with this species' own orientations), so it is **fitted**, exactly. Both bodies are centrally
symmetric about the prism centre, so the touching scale is closed form rather than a bisection:

```
s* = max over candidate axes of  |d·u| / (rA(u) + rB(u))
```

with candidates = the separating-axis set (15 axes for two boxes; 8+8 face normals plus 36
edge-edge crosses for two octahedra). `s* >= 1` means the pair is clear as authored.

**Then do it again for Charge.** `Flora.ResolveShieldPeriod` makes every Charge plant's leaves
shielded *by law*, and `PrismStateManager.ActivateShield` replaces the box with the octahedron that
CIRCUMSCRIBES it — `OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE` (3) on the HALF-extents, reaching
`1.5 × leafSize`. A species fitted for the box it draws is **not** fitted for its armour: measured
on the Mandelbulb, the plate the other three elements clear at fuses 1639 of Charge's 2810 neighbour
pairs into one solid. Both shipped lattice species had the same defect (`Docs/ECOSYSTEM.md §35`).

Two legitimate answers, and you must pick one out loud:
- **Fit Charge's own prism** (uniformly, so the leaf ASPECT — the species' identity — is exact).
  Its plates then read as a sparse skeleton and its octahedra fill the lattice in. This is what the
  gyroid, Schwarz P and Mandelbulb do.
- **Accept the fusion and state it**, when one authored leaf serves all four rolled elements and
  there is no per-element field to reach (the Hesperides topiaries). *An accepted graze must be a
  stated number, never something a later reader discovers.*

Never fit the LATTICE instead of the prism: scaling the lattice drags a whole family of
absolute-distance coherence tolerances with it (§34.8), scaling the prism drags nothing.

---

## 6. Budget: prisms, volume, and the one that is never free

State all three before authoring, and remember `/ecology` §4.6 — **prove WHICH ceiling binds**.

- **Per-prism volume** is `leafSize.x·y·z`, flat for the plant's whole life (there is no lifeform
  level — §40). Nominal is 16; flora ship 0.85 → 135, a 159× span, so never assume nominal.
- **Per-plant volume** = per-prism × the settled prism count. A cell whose prisms are not nominal
  **must author its own `PhaseThresholds`**, never inherit the `count × 16` derivation.
- **Always-on heart colliders = the live PLANT CAP**, one per plant, culled by no phase. This is the
  only flora number that is never free. Prisms are LOD-cullable by phase; hearts are not. Size
  `MaxLivePopulation` against the collider budget (shipped references: Rampage 440, the Lattice cell
  1,080), not against the forest you want — grazing is the real control.

A per-plant budget is **geometry** for a lattice or surface species (a gyroid octagon is 24 prisms
around one crystal), so a cell-level budget override truncates a shape mid-figure rather than
thinning the plant. **Plant COUNT is the only lever there.**

---

## 7. Where the species lives — decide it, do not leave it implied

- **A `SpawnProfileSO`** makes it part of a cell's standing population. Every cell referencing that
  profile inherits the cost, so grep the profile's guid and hold the species against the *tightest*
  consumer — a "N% of Frenzy" figure written against one cell rots the moment the cell is retired.
- **The Lifeform Matrix toy** (`Assets/_SO_Assets/Toys/Toy_LifeformMatrix.asset`, `floraSpecies`)
  makes it opt-in: a player flies the bench and releases a population. Costs no cell anything.
- A species may be in **both**, or in only the toy (the worm colony's "a boss is opt-in", and the
  Mandelbulb's "in NO SpawnProfile").

**Whichever you choose, say it in the docstring and in `Docs/ECOSYSTEM.md`** — and know that *"it is
wired nowhere" is true only on the date it was written*. Re-prove absence by grepping the config
asset's GUID across `_SO_Assets` rather than inheriting the claim.

---

## 8. Traps that have actually cost passes

- **A SENTINEL IS NOT A MEASUREMENT.** `LeafSize {0,0,0}`, `MaxTotalSpawnedObjects -1`,
  `HeartWorldScale 0`, `LatticeScale -1`, `GrowPeriod -1`, `ShieldPeriod -1` all mean *keep what you
  have*. An offline pass must resolve each the way the RUNTIME does (fall through to the prefab).
  Reading the zero as a real leaf priced one species 25% light. And write `(-?\d+)`, not `(\d+)`,
  or you skip `-1` by accident rather than by rule.
- **A variant block with `Enabled: 0` is never read.** Any tool that authors into a `Variant` must
  flip `Enabled` with it. A zero-initialised block is safe to enable — every other field's
  initializer is a keep-the-prefab sentinel.
- **The component fileID in a `FloraPrefab` reference differs by family.** `PhyllotacticFlora` and
  `BranchingFlora` share `7514956980722975813`; `AssembledFlora` uses `8186157953239024492`. A wrong
  one resolves to no component at all and the config grows nothing, **silently**. Copy it from that
  species' own shipped element asset, or give a new family its own and use it consistently.
- **`PrismScaleAnimator.SetTargetScale` clamps per axis into `[0.5, 10]` inside the setter**, with
  no log and no return value — 363 of 404 prefabs inherit those defaults. Anything that STATES a
  size calls `Prism.AdmitTargetScale(size)` first; anything that GROWS into the bound leaves it
  alone. When a fitted size does not read on screen, check what the engine actually STORED before
  re-fitting.
- **NOTHING may be parented under a prism.** A prism carries its leaf as `localScale`, and a
  non-uniform scale above a rotated child is a **shear** that compounds every generation. Parent to
  the prism's *spindle*. (`ReseedBranches` did this and skewed all three lattice species.)
- **A scale applied to a node that PARENTS its successors compounds** as `scale^depth`. Scale the
  node's children, not the node.
- **Flora reproduce TWO ways and the config gives no hint which.** The per-plant growth quota
  (`GrowthPerOffspring`, spent in `Flora.TryReproduce`) or the lattice **colony cycle** (one birth
  per `Cell.CurrentFaunaSpawnPeriod` for the whole population). The quota field is written on every
  config and is **inert** on the lattice ones. Before touching reproduction, split the species list
  by family and say which path each change reaches.
- **A colony's cadence reads `Cell.CurrentFaunaSpawnPeriod`, never `OnFaunaWaveSpawned`** — only
  `RandomLifeSpawner` raises that event, so a subscription is dead code in every `IntensityWise`
  cell.
- **`CellTypeChoiceOptions.IntensityWise` swaps the SPAWNER class**, so a spawn-loop feature written
  in only one of `RandomLifeSpawner`/`IntensityWiseLifeSpawner` is dead in exactly the modes that
  asked for it. Flora has **five** producers (both spawners, reproduction, the `Microscene`
  conveyor, the Lifeform Matrix toy), which is why per-cell scalars resolve on the **`Cell`**.
- **An elemental LAW cannot live in per-element config when the element is ROLLED.** A config with
  `SpreadElements` and an empty palette applies its OWN block to a rolled element, so nothing
  writable on any per-element asset reaches it. Put the law at `LifeForm.Initialize`, the one point
  where prefab, variant, cell overrides and the crystal have all landed — the shape
  `Flora.ResolveShieldPeriod` and `ResolveGrowthPerOffspring` use. Scope it to `Flora`, not
  `LifeForm`, or every creature inherits a rule written about plants.
- **A shared species asset is why per-cell tuning belongs on the PROFILE.** Grep who else references
  a config before tuning it; Rampage's species are referenced straight out of `Blob Cell/`.
- **A static registry/frontier survives every cell teardown.** If your species keeps population
  state, key it by `(Cell, species)` and drop it at all three Cell reset sites. A cache of a *pure
  function* is exempt — say so, or the next reader files it as the bug.

---

## 9. Hand back honestly — you cannot run Unity

State the exact in-editor steps, and never claim something works that you have not seen work.
The minimum handoff for a new species:

1. Open **Menu_Main**, fly the **Lifeform Matrix** toy → Flora → *species* → an element, and watch
   one plant grow in. (Continuity of existence: prisms must bloom in, never pop.)
2. Confirm the plant reaches its authored budget and stops, and that shooting prisms makes it
   **regrow** (budget frees on consumption).
3. Confirm the CHARGE variant's leaves shield and that the octahedra have room.
4. Confirm the heart drops on death and is collectable.
5. **FrogletTools ▸ Validation ▸ Validate Lifeform Crystals** after any lifeform-prefab change.
6. **FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines** if the species entered a
   SpawnProfile — the offline volume is an ESTIMATE until the measurer confirms it.

Then say which of those you did **not** verify. `Docs/QA/QA_BACKLOG.md` is where unverified work
goes; the `/ship` protocol reads the PR body's "Verification status" section for exactly this.
