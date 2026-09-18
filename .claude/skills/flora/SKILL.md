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
| `MandelbulbFlora` | trace CURVES over a surface defined by a function | per-plant growth quota | authored cross-section x a per-curve GIRTH, length from the curve | a baked height field, nothing else |

**The question that picks the family: where does the shape come from?**

- From the plant's own *walk* → Branching (scribble in bulk) or Phyllotactic (a silhouette: trunk,
  crown, creeper, rosette). Neither needs any offline work.
- From a *periodic or quasiperiodic tiling* → `AssembledFlora`. Only if the surface genuinely HAS an
  exact intrinsic tile. `Docs/ECOSYSTEM.md §34` is emphatic: a square-ish marching walk across a
  curved surface accumulates drift, fronts arriving from different directions disagree, and the
  occupancy key degrades to a quantized float. Gyroid, Schwarz P and the icosahedral quasicrystal
  each have their own tile and use it.
- From a *function* with no intrinsic tile at all (a fractal boundary, an implicit surface, a level
  set) → the `MandelbulbFlora` shape: bake the surface once, offline, and address a prism by
  where it sits ON it — `(theta, phi)` plus a heading in that point's own tangent basis. This keeps the property §34 actually cares about
  — **sameness is an integer address** — without inventing the fitted grid §34 forbids. Say so
  explicitly in the class doc, or the next reader will file it as the mistake.

**A SURFACE SPECIES MUST DECIDE WHETHER IT IS A SKIN, AND THE ANSWER IS USUALLY NO.** Plating every
surface cell gives you a closed crust, and a closed crust of a solid form reads as *that solid*,
however fine the lattice and however clever the prisms. Raising the resolution makes it a finer
lumpy sphere; it does not make it interesting. Before you tune anything, decide what the form's
structure actually IS and plate only that — the Mandelbulb's is its TERRACING, so it grows on the
terrace risers and leaves the treads open, which is what lets you see into the object at all
(`Docs/ECOSYSTEM.md §44.2`, which lists the four closed-surface candidates that were built and
rejected first). Its sibling finding: **a solid form has no interior structure to reveal**, so
cutting nested shells inside one just gives you spheres — all the information is on the boundary.

**Judge a growth rule by RENDERING it, at the size it will be judged.** An offline model can report
a prism count, a size spread and an aspect distribution that all look excellent for a form that
reads as gravel. Render oriented boxes with a depth buffer and a light, not screen-aligned squares
— squares cannot show the one thing a multi-scale rule is for, which is that no two prisms share a
frame. `measure_mandelbulb_flora.py --render` is the worked example.

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
Tools/Build/mandelbulb_surface_harness/{Stubs.cs,Bulb.cs,Driver.cs,run.sh}   # copy this
Tools/Build/regatta_course_harness/                                    # the original precedent
```

`run.sh` needs a per-user dotnet 8 SDK (`bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh)
--channel 8.0 --install-dir $HOME/.dotnet --no-path`, ~40 s). **Split the pure math into its own
file so this is possible** — `MandelbulbSurface.cs` holds the whole growth rule and knows nothing
about plants; `MandelbulbFlora.cs` holds the Unity wiring. That split is what turns
"I think this is right" into a proof, and it costs nothing.

**Put the WHOLE rule in the pure file, not just the membership test.** Anything the verifier
cannot run is a thing nobody proved: the Mandelbulb's reconstruction, frame, steering fields, seed
set, hop and emission all live in `MandelbulbSurface` for exactly that reason, and the flora
subclass only consumes the addresses. Its harness also compiles the SHIPPED table and the real
`Element` enum, so what runs is the game rather than a copy of it. It is also what lets the verifier compare two implementations by
an INTEGER identity (which patch each prism stands for) rather than by position.

**A harness's stdout is a DATA channel — nothing else may write to it.** `csc` prints its
diagnostics to stdout, so one warning from a rebuild lands in front of the JSON and the caller's
parse fails naming nothing (`Expecting value: line 1 column 1`), which reads exactly like drift in
the file under test. Redirect the build to stderr, where `set -e` still fails on it. Measured: a
self-test control that made a method return early produced CS0162 and cost two full self-test runs
to attribute. And treat an EMPTY result as a harness failure with its own message rather than
feeding it to the parser.

Then run the standing gates, which DO cover the wiring half:
`check_conditional_compilation.py`, `check_using_directives.py`, `check_enum_member_references.py`,
`check_switch_label_collisions.py`, `check_self_referential_locals.py`, `check_console_logging.py`.

---

## 5. THE FOUR ELEMENTAL IDENTITIES — a species does not get to invent these

A plant's element is **ROLLED**, so this is a LAW in code, not a field you author. Every species
inherits all four; what a species authors is the ONE form the four spend four ways.

| element | its identity | the mechanism |
|---|---|---|
| **CHARGE** | **armours its leaves** — shielded mass, so grazing it costs two passes | `Flora.ResolveShieldPeriod` (§35) |
| **MASS** | **the most cumulative prism volume, in the most CUBIC leaf** — x, y and z closest together | `FloraElementalForm.ShapeLeaf` |
| **SPACE** | **the highest ASPECT RATIO** — the long axis trades cumulative prism volume for the **bounding volume of the assembly** | `FloraElementalForm.ShapeLeaf` + `ReachScale` |
| **TIME** | **the fastest clock** — grows fastest *and* reproduces fastest | `Flora.ResolveGrowPeriod` + `ResolveGrowthPerOffspring` (§38) |

Two are about shape and two are not, and that is the design: **Charge and Time take the species'
own authored form** (their identities are a state and a tempo), and only Mass and Space restate it.

**The four are a REDISTRIBUTION, never an inflation.** The four volume multipliers average to
exactly 1 and the aspect term is volume-exact by construction (a unit-volume shape vector raised to
any power still has volume 1), so a mixed-element forest holds the mass it held before and **no
cell's volume phase ladder moves**. This is what makes a fleet-wide leaf law shippable at all — a
law that gave Mass more material would land on Rampage's play-tested ladder, on Hesperides, and on
every future cell that grows flora. `ReachScale` falls out of the same statement with **no new
constant**: it is `volume^(-1/3)`, i.e. a plant spending a fixed amount of material, so Space
reaches ×1.35 and Mass draws in to ×0.82.

**What a new species has to do about it:**

1. **Nothing, if your leaf is free.** Author ONE leaf that reads well and the law spends it. Do
   not author four per-element leaves "to express the elements" — you will be expressing them
   twice, and inconsistently with the fleet.
2. **State it in your own data if your prism size is fixed by your growth rule**
   (`PrismSizeFixedByGrowthRule` — a lattice, a surface species). You are EXEMPT from the runtime
   transform, because a transformed leaf lays prisms your bond table no longer describes (§34.8) —
   so you must satisfy all four clauses yourself, and
   `Tools/Build/measure_flora_elemental_form.py` checks every one of them: Mass heaviest, Mass most
   cubic, Space lighter than Time, Space most elongated.
3. **Say where your REACH lives.** The law scales extent as well as leaf, and each family spends it
   on its own field (`PhyllotacticFlora.segmentLength`/`whorlRadius`,
   `BranchingFlora.branchingScaleFactor`). A family whose leaf IS its strut needs nothing — the
   anisotropy already lengthened it. A family whose length lives elsewhere and does not wire this
   **cannot express Space at all**, and nothing will tell you.

**Two traps specific to this law:**

- **A leaf whose axes are already equal has no aspect to exaggerate.** The transform is a no-op on
  a cube, by design — the law must not invent an aspect a species never authored. If your Space
  variant has to read as a needle, author a leaf with a long axis and let the law stretch it; do
  not expect the law to choose one for you.
- **Check which axis your family actually RENDERS before trusting the transform to be visible.**
  `PhyllotacticFlora` reads only `LeafSize.x/y`, so a law that lengthens `z` does nothing there —
  which is precisely why its reach fields are wired. Ask the same question of any new family.

The constants are MEASURED off the eleven species that already shipped the law (three lattice
species authoring four fitted leaves each, and the eight Hesperides phyllotactics sharing one
authored ladder), **one vote per FAMILY**, geometric mean of the family medians. If you retune a
species' per-element leaves you may move the measurement, so re-run the tool — it fails the build
when the code stops tracking the assets, and its `--self-test` proves it can.

---

### 5.1 Two species on one growth rule

A new species does not always need a new FAMILY. If an existing growth rule can express your
concept with a different parameter set plus at most one new dial, ship a **second prefab** on the
same component — the way the eight Hesperides phyllotactics are eight species on one class, and
the way Coral Bloom is the Mandelbulb rule with the twist off and the curves made to continue
(`Docs/ECOSYSTEM.md §46`). You get the family's whole tool trio, its bake and its verifier for
free, and the two plants read as the same WORLD grown two ways rather than as two unrelated
objects.

Four things to get right:

1. **A new dial must be a pure function of the ADDRESS.** A prism's address is the whole of its
   identity, so anything the pose needs has to be stored in it — never recomputed from state the
   curve no longer has. And the dial's ZERO must be bit-identical to before it existed, which is
   what lets you prove you broke nothing.
2. **Give the second species its own component fileID and its own prefab guid.** A wrong fileID in
   a `FloraPrefab` reference resolves to no component at all and the config grows nothing,
   silently.
3. **DERIVE the elemental law from the species' own neutral form** rather than typing four
   per-element prisms (§5). That is what makes the CONCEPT persist through the four elements while
   each element still expresses itself, and it means retuning the concept cannot silently break
   the law.
4. **Register it everywhere the first species is registered** —
   `author_lifeform_heart_sizes.py`'s `FLORA_PREFABS`, the toy roster, the population hand-off —
   and make every tool loop over the species table rather than defaulting to the first one.

**And re-run the verifier on the NEW species specifically.** A gate written against one species is
a gate calibrated on one species: adding a second to the Mandelbulb family exposed three
constants in its verifier that were coincidences rather than margins (a tolerance stated in the
wrong unit, a "how many prisms agreed first" heuristic that is really a statement about one
element's surface roughness, and a `phi` comparison with no seam unwrap that had been making the
first species look 50x worse than it was). Expect your second species to find the same class of
thing, and fix the gate rather than widening it.

---

## 6. Fit the prism — do not eyeball it, and remember CHARGE is a different question

A prism's size is a geometric claim about a specific point set (this species' own measured sites,
with this species' own orientations), so it is **fitted**, exactly. Both bodies are centrally
symmetric about the prism centre, so the touching scale is closed form rather than a bisection:

```
s* = max over candidate axes of  |d·u| / (rA(u) + rB(u))
```

with candidates = the separating-axis set (15 axes for two boxes; 8+8 face normals plus 36
edge-edge crosses for two octahedra). `s* >= 1` means the pair is clear as authored.

**A regular lattice can claim ZERO overlapping pairs and must be fitted to it.** A species whose
prisms are fitted to IRREGULAR patches cannot — two patches meeting along a ridge have bounding
boxes that must overlap, so it states BOUNDS and gates both of them: how MANY pairs may
interpenetrate at all, and how DEEP (refuse to lay a prism that would sit essentially inside one
already laid). A zero you cannot have is worse than a bound you can measure.

**Then do it again for Charge.** `Flora.ResolveShieldPeriod` makes every Charge plant's leaves
shielded *by law*, and `PrismStateManager.ActivateShield` replaces the box with the octahedron that
CIRCUMSCRIBES it — `OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE` (3) on the HALF-extents, reaching
`1.5 × leafSize`. A species fitted for the box it draws is **not** fitted for its armour: measured
on the Mandelbulb, the ribbon the other three elements clear at fused EVERY armoured pair it had. Both shipped lattice species had the same defect (`Docs/ECOSYSTEM.md §35`).

Two legitimate answers, and you must pick one out loud:
- **Fit Charge's own prism** (uniformly, so the leaf ASPECT — the species' identity — is exact).
  Its plates then read as a sparse skeleton and its octahedra fill the lattice in. This is what the
  gyroid, Schwarz P and Mandelbulb do — and **check WHICH dimension is doing the fusing before you
  shrink the cross-section**: a prism's `leafSize` includes its LENGTH, so a species that lays
  prisms end to end along a curve or a rail fuses along its OWN chain, and no cross-section shrink
  reaches that (measured on the Mandelbulb at a quarter width: still 84% fused). The lever there is
  the length — its Charge ribbon is DASHED, its prisms shorter than the step that spaces them, and
  the octahedra fill the dashes in. **The bar to clear is its SIBLINGS, not an invented number:**
  *a Charge plant wearing its shields must be no more fused than an ordinary plant is bare.* That
  moves if the species is ever retuned, which an invented constant does not. And check the read
  INVERTS rather than merely shrinking — armouring multiplies a plant's own silhouette by exactly
  `0.5 × CIRCUMSCRIBING_SCALE²` = 4.5, so a Charge plant should end up the DENSEST of the four
  shielded and the sparsest stripped. Gate that ordering; it is the two-pass grazing cost made
  visible.
- **Accept the fusion and state it**, when one authored leaf serves all four rolled elements and
  there is no per-element field to reach (the Hesperides topiaries). *An accepted graze must be a
  stated number, never something a later reader discovers.*

Never fit the LATTICE instead of the prism: scaling the lattice drags a whole family of
absolute-distance coherence tolerances with it (§34.8), scaling the prism drags nothing.

---

## 7. Budget: prisms, volume, and the one that is never free

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

## 8. Where the species lives — decide it, do not leave it implied

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

## 9. Traps that have actually cost passes

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
- **A PCA frame is arbitrary when the point set has rank under 2.** If a species orients a prism by
  fitting a patch of cells, a two-cell patch (there are always some) has one non-zero eigenvalue, so
  the two smallest eigenvectors are interchangeable and the frame can flip on a rounding difference
  between two machines. Test the MINOR eigenvalue against a tolerance and fall back to a
  well-defined normal. Measured: 12 of 2,596 prisms disagreed between the offline model and the
  shipped C# until that test landed; then 0 of 2,615.
- **An orientation decided by the SIGN of a quantity that can be zero is not decided at all.**
  "Flip the fitted normal outward if `n · census < 0`" is a coin toss whenever the two are nearly
  perpendicular, which a real patch does reach — it flips between two machines and between the
  shipped code and the offline model, and on a centrally-symmetric prism it is **invisible on
  screen**, so nothing but a cross-check finds it. Answer it in order by things that are each
  either a real answer or explicitly not one (census normal → radial → a sign convention on the
  first significant component), with a stated tolerance. Measured: 8 of 1,848 plates.
- **A prism thickness stated as a FRACTION of the prism is a cubic volume term.** A rule that makes
  long plates then makes long *slabs*, and a slab's volume lands on the cell's Frenzy ladder as the
  cube of its length. State thickness in absolute lattice cells; the plant's volume is then linear
  in its plated area, which is a number you can reason about.
- **A species whose prisms are fitted to irregular patches cannot claim zero interpenetration.** Two
  patches meeting along a ridge have bounding boxes that MUST overlap — it is geometry, not a
  defect. State BOUNDS instead and gate both: how MANY pairs may interpenetrate at all, and how DEEP
  (refuse to lay a prism that would sit essentially inside one already laid — hidden mass buys
  nothing, and the same rule bounds the interleaving). A zero you cannot have is worse than a bound
  you can measure. The regular lattice species CAN claim zero and must still be fitted to it.
- **A tool that MODELS a body size can be wrong by an order of magnitude for a new family.**
  `author_lifeform_heart_sizes.py` sizes a flora body as the disc `N` prisms of footprint `A` settle
  into, from the prefab's `leafSize` and a budget capped at 400. Both inputs are meaningless for a
  species that measures a size per prism and grows thousands of them, and the error is silent
  because the tool's `--check` compares its own model against itself. Check a new family's body
  measurement against the species' own model before trusting the heart it authors.
- **A static registry/frontier survives every cell teardown.** If your species keeps population
  state, key it by `(Cell, species)` and drop it at all three Cell reset sites. A cache of a *pure
  function* is exempt — say so, or the next reader files it as the bug.

---

## 10. Hand back honestly — you cannot run Unity

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
