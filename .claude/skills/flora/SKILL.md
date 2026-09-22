---
name: flora
description: Use for ANY work on a PLANT — adding a flora species, changing how one grows, poses its spindles, shapes or orients its prisms, expresses its four elements, reproduces, or plants itself; wiring a FloraConfigurationSO or a FloraVariantTuning; or answering "why does this plant look messy / grow in disconnected lumps / have branches pointing nowhere / look the same on all four elements". Loads the growth law (a flora grows the way it withers, run backwards), the limb contract, the elegance rules for a tiling, the element-expression contract, and the traps that cost real time. Trigger when editing Assets/_Scripts/Controller/Environment/FloraAndFauna/{Flora,BranchingFlora,PhyllotacticFlora,AssembledFlora,BorromeanFlora,FloraHeartRegistry}.cs, any Assets/_Prefabs/FloraAndFauna/*Flora.prefab or Spindles/*.prefab, any `* Flora *` config asset, Tools/Build/*flora*/*borromean*/*gyroid*/*schwarz*/*quasi*, or Docs/ECOSYSTEM.md §§32-38, 40, 42-43, 47-49.
---

# Flora: the per-plant contract

`/ecology` owns the **system** — populations, caps, diets, phase ladders, the locked
invariants, the collider budget. **This skill owns the PLANT**: how one grows, what its
limbs are for, what makes its prisms read as a form rather than as confetti, and what its
element is supposed to say.

**Run `/ecology` first for anything that changes the SYSTEM** (seed floors, caps,
reproduction rates, phase thresholds, what a cell plants). The invariants it loads are
LOCKED and this skill assumes them rather than restating them.

`/fauna` is the sibling for creatures. A plant and a creature share `LifeForm`, the heart
rule and the wither, and share almost nothing else.

**The worked example this skill was written from is `Docs/ECOSYSTEM.md §49`** — a species
whose SHAPE was right and whose PLANT was wrong through four passes: the growth order, the
tiling, the zero-overlap fit, the element contract, and finally its adoption into four
cells. Every rule below that cites a measured number cites one of those passes. §32 (the
gyroid octagon colony), §34 (the lattice family) and §48 (Garland, a cell composed for a
CAMERA) are the other three records worth reading before adding a species.

---

## 1. The roster, measured

Measured off the shipped prefabs (`m_Script` guid → class, `spindle` guid → prefab), not
remembered:

| prefab | class | spindle prefab |
|---|---|---|
| Branching, Cacti, Pine, Nerve, SecondaryNerve | `BranchingFlora` | `Branch` |
| Arbor, Coral, Frond, Lantern, Reed, Rosette, Spire, Tendril | `PhyllotacticFlora` | `Branch` |
| Gyroid | `AssembledFlora` (+`GyroidAssembler`) | `GyroidBranch` |
| SchwarzP | `AssembledFlora` (+`SchwarzPAssembler`) | `AssemblyBranch` |
| Quasicrystal | `AssembledFlora` (+`QuasicrystalAssembler`) | `QuasicrystalBranch` |
| Borromean | `BorromeanFlora` | `Branch` |
| MandelbulbFlora, CoralBloomFlora, WatershedFlora, ApolloniaFlora | `MandelbulbFlora` — **all four**, measured; there is no `CoralBloomFlora` class, the species differ only in their authored `GrowthRules` (`Docs/ECOSYSTEM.md` §50, §52, §53, §54, §55, §56) | `Branch` |
| Wall | `AssembledFlora` | `AssemblyBranch` |
| **Seaweed** | **`SegmentSpawner` — NOT a `Flora` at all** | (n/a) |
| **oldWallFlora** | **`GyroidAssembler` — not a `Flora` either** | (n/a) |

The last two rows are the reason a roster is MEASURED: both sit in the flora folder with
`*Flora` names and carry components that are not a `Flora` at all, so "every prefab named
`*Flora`" is not the species list. Resolve `m_Script`'s guid to a `.cs.meta` instead.

---

## 2. THE GROWTH LAW — a flora grows the way it withers, RUN BACKWARDS

> **First comes the CRYSTAL. Then SPINDLES grow from the crystal. Then spindles and/or
> PRISMS grow from spindles. Spindles can grow from prisms. The process loops. New crystals
> are planted as flora REPRODUCE, which makes new flora.**

Two properties follow, and both are testable rather than aspirational:

**(a) The plant is ONE CONNECTED OBJECT at every tick.** It expands outward from the
crystal. It does not start several patches that meet up and seal later — that reads as
parts appearing in mid-air, which is the continuity-of-existence law failing at the level
of the whole organism rather than of a prism.

**(b) Every prism hangs off something that already exists.** Whatever growth order a
species uses, a prism's PARENT must already be laid. In a table-driven species make the
parent index part of the table and assert `parent[i] < i`; in a frontier-driven species
(branching, phyllotactic, lattice) the frontier already guarantees it.

**How to prove (a) rather than claim it.** Walk the growth order in the increments the
species actually lays (one orbit, one frontier batch, one prism), union-find the laid set
plus the heart, and assert one component every time.
`borromean_surface.connected_prefixes` is the worked example; its negative control is the
ordering it replaced (orbits sorted by RADIUS), which measured **up to 3 components**.

**(c) AND A BOND HAS TO BE A BOND — connectivity alone cannot see a WIRE.** Give every
curve or patch the heart as its parent with no connector between them and both properties
above still hold: the plant is a formally connected STAR whose limbs each span most of it.
So measure the bond LENGTH too, priced in the species' own stride (its walk step, its ring
chord, its lane gap), and gate the worst. On the Mandelbulb family that row is what caught
a gasket ring stemming from its parent disc's CENTRE while the prism it hung off sat on
that disc's RIM — 15.2 strides against a shipped worst of 4.7 (`Docs/ECOSYSTEM.md` §55.2).

**Two more traps the same family paid for, both of which outlive it:**

- **A GREEDY PICK OVER A SYMMETRIC POINT SET IS DECIDED BY FLOAT WIDTH.** If the growth
  order comes from a spanning tree, a nearest-neighbour search or any other greedy choice,
  a symmetric surface puts candidates at distances equal to the LAST BIT — measured, four
  of one bulb's saddles sat at exactly 0.847114625 — and the shipped float32 and the
  offline float64 model then grow visibly DIFFERENT plants from one seed while every
  statistical gate stays green. Use a tolerant compare plus an INDEX tie-break, and make
  every ordering a TOTAL key (`Docs/ECOSYSTEM.md` §54, §55.2).
- **WHEN A CURVE GAINS A PROLOGUE, EVERY GATE THAT SAYS "THE CURVE'S FIRST" ANSWERS ABOUT
  THE PROLOGUE.** Adding connectors broke six gates at once that had nothing to do with
  connectors: an arm census keyed on a curve's first prism, lane shares that counted every
  prism's lane, and every Fall statistic that separated free space from surface with a
  field test. Hand the consumer a CONNECTOR flag out of the growth rule (beside the parent
  — neither belongs in the address, or the pose stops being a pure function of one row),
  and put it on the wire so the verifier PROVES the split instead of trusting a label.

**What NOT to do.** Do not order a table by distance from the centre and call it "outward
from the heart". On any surface that wraps — and a compact or periodic one always does —
a radius shell is several disconnected rings. Order by HOP DISTANCE over the species' own
neighbour graph instead.

---

## 3. THE LIMB CONTRACT — a spindle is a BOND, not a marker

A spindle is a **limb**: it exists to make the plant read as connected, so it has to lie
ALONG the gap between a prism and whatever that prism grew from. Two poses work and one
does not:

| species | spindle posed | branch geometry | result |
|---|---|---|---|
| `BranchingFlora` | at the **PARENT**, rotation = the growth heading; the child is placed `branchingScaleFactor` forward | `Branch`: one arm along local **+z** from the origin | the limb IS the bond ✅ |
| `PhyllotacticFlora` | at the **NEW prism**, rotation = its heading | `Branch` | the limb points at the next node ✅ |
| `AssembledFlora` / gyroid | at the **prism**, wearing the prism's rotation | `GyroidBranch`: a **MIRRORED PAIR** of half-branches meeting at the prism (`Docs/ECOSYSTEM.md` §34.12) | covers the bond in BOTH directions ✅ |
| `AssembledFlora` / quasicrystal | at the prism, prism's rotation | `QuasicrystalBranch`: mirrored pair along ±x | the strut ✅ |
| `AssembledFlora` / **Schwarz P** | at the prism, prism's rotation | `AssemblyBranch`: a **SINGLE off-centre arm** along local −y (0.5 → 6.7 units) | points wherever that prism's −y happens to face ⚠ **known weak case — do not copy it** |
| `BorromeanFlora` | at the **PARENT**, aimed at the child, stretched to the bond | `Branch` | the limb IS the bond ✅ |
| the **Mandelbulb family** (4 species) | at the nearest **STANDING ancestor**, aimed at the prism, stretched to the bond | `Branch` | the limb IS the bond ✅ — and the ancestor WALK is the part to copy: a species whose claim can refuse a prism cannot hang the next one off a parent that was never laid (`Docs/ECOSYSTEM.md` §55) |

**Rules that come out of that table.**

1. **A limb must be aimed at something.** Either root it at the parent and `LookRotation`
   it at the child, or pose it at the prism and give it geometry that reaches out
   symmetrically. Posing it at the prism with a ONE-SIDED branch is the Schwarz P case: it
   is aimed by a rotation that describes the SURFACE, not the bond.
2. **On a surface species, the prism's rotation is a frame OF the membrane.** Its +z is the
   surface NORMAL. A spindle wearing it stands perpendicular to the plant and skewers its
   own plate.
3. **Stretch the limb to its bond** when bond lengths vary, and scale the spindle's
   **CHILDREN**, never the root — a prism parents to the root, so a scaled root multiplies
   the authored `leafSize` and the config stops describing the prism
   (`AssembledFlora.ScaleSpindleToLattice` records this; `BorromeanFlora.StretchToBond`
   follows it). Scale the child's LOCAL **z**: on every spindle prefab in the project that
   is the branch's length axis.
4. **Measure the branch's reach, don't author it.** Compose the prefab's mesh bounds through
   its transform chain into spindle-root space and take the furthest +z. A constant copied
   out of an asset is true only on the day it is copied, and a prefab swap silently invalidates it.
5. **Keep the limb when its prism is eaten, and re-use it when the prism regrows.** A branch
   whose leaf was grazed is still a branch — and re-use is also what stops regrowth minting
   a second spindle on one bond. Track limb-per-site and clear the slot in `RemoveSpindle`.

---

## 4. THE TILING — why prisms read as messy, and the fix

A plant made of many identical plates is judged as a TILING, not as a set of plates. Three
things decide whether it reads as a form:

**(a) The orientation field must be COMBED.** This is the one that has actually shipped
broken. Where each site picks from several equally-valid axes independently — e.g. a
minimal surface's **two asymptotic directions**, which are orthogonal and interchangeable —
the per-site choice comes out of the sign of an eigenvector in an arbitrary local basis,
i.e. effectively at random. Every plate is individually correct and the tiling is noise.
Measured on the Borromean surface before combing: neighbouring plates' long axes **56.6°
apart**, with **49% of edges more than 60° apart**. After combing (iterated conditional
modes over the neighbour graph, seeded restarts, the choice made per SYMMETRY ORBIT so it
cannot break the symmetry): **25.9°**, 11.5%.

> **General rule: a per-site choice among equally-valid options is NOISE unless something
> makes neighbouring sites choose together.** If your species picks an axis, a handedness, a
> phase or a variant per prism, comb it — and measure the before/after, because the "after"
> number is the whole claim.

**(b) The plate has to LIE on the surface.** On a minimal surface the principal curvatures
are equal and opposite, so normal curvature vanishes along the two directions bisecting
them AND those two are orthogonal — a property minimal surfaces alone have, and the reason
a flat rectangle can sit flush on a saddle at all. Put the plate's in-plane axes there.
Measure it as **corner lift as a fraction of the plate's own thickness** (Borromean Time:
0.56; a long Space plate: 0.86 — reported, because that is what a high aspect ratio costs).

**(c) NO PRISM MAY INTERPENETRATE ANOTHER — so a plate's SIZE is FITTED, not authored.**
Two plates of conserved mass passing through each other is a defect, not a density dial.
What an element authors is the SHAPE of its plate; the size is fitted offline to the largest
that clears its own neighbours (exact OBB separating-axis test, inflated by a small margin
so the shipped gap is real rather than a float epsilon). Spindles are exempt — a LIMB may
pass through a plate, which is what lets a limb span a bond instead of stopping short of it.

Three facts follow, all measured on the Borromean surface and none of them specific to it:

* **The FOOTPRINT costs clearance and the THICKNESS does not.** A plate's neighbours lie in
  the surface beside it, so growing it along the surface runs into them and growing it along
  the NORMAL runs into nothing: 0.1 → 0.8 of its own width in thickness cost **1.3%** of the
  footprint and bought **7.7×** the volume. **This is the axis to spend**, and it is what
  lets the element contract in §5 survive the zero-overlap rule.
* **Coverage is a property of the TILING, not of the COUNT.** Every plate is fitted against
  its own neighbours, so a looser tessellation covers the same surface with FEWER, BIGGER
  pieces (the fitted long axis stayed 0.73–0.93 of the mean site spacing from 24 to 60
  orbits). So the site count is a free choice — and the one rule on it is **the more ROOM PER
  SITE, the bigger the body it carries**, which is why each Borromean element tiles at its own
  spacing.
* **An element buys room TWO ways: by cutting the surface into fewer pieces, or by growing
  the SURFACE.** State the ladder in ROOM PER SITE, never in site count — the two are the
  same thing only while every element shares one surface, and the moment one does not, a
  count-based rule reads backwards (Borromean SPACE has a FINER cut than Mass or Charge and
  the most room of the four). Growing the surface is a **SIMILARITY**, the one transform that
  maps a clearing arrangement onto a clearing arrangement exactly, so it costs the no-overlap
  proof nothing to re-derive — and if the element's contract holds its plate's VOLUME, a `k×`
  surface pays for itself: the fit hands it a `k×` footprint and the volume target drives the
  thickness down by `k²`. Same plant volume, same prism count, `k×` the span, `k²` thinner
  struts, from ONE dial.
* **A bisection converges onto its own boundary**, so a verifier must prove the PROPERTY and
  not the solver's stopping condition: fit to a 3% margin, assert a 2% gap and that 10%
  bigger collides. Asserting the margin itself asserts the fit's tolerance, and rounding the
  shipped table to five decimals was enough to tip 2 of 24 pairs over it.

The lap is therefore **retired as a dial**: it was a look call made by rendering candidates
(at 1.40× the site spacing the Borromean plates lapped 61% and read as one smooth blob; at
0.85 they lapped not at all and read as perforated), and removing it costs real coverage —
that plant's anchor volume fell 7,499 → 3,279 and its Space element now reads as a frame of
struts rather than a skin (9.2% of its membrane covered). What survives is the ASPECT, which is still a look call, and the
rule for making one: **judge a candidate at the size it will be judged**
(`Docs/PALETTE.md §4.3`).

---

## 5. THE ELEMENT CONTRACT — a plant is its species and its ELEMENT

`Docs/ECOSYSTEM.md §40`: a lifeform is its species and its element and nothing else. There
is no level, no acquired growth, no per-individual history. So everything an element says
about itself it says exactly ONCE, in that element's own config.

**Author one ANCHOR and three PERTURBATIONS of it**, so "what does this element do to the
plant" is one comparison rather than four independent fits:

| element | says | how |
|---|---|---|
| **TIME** | the optimum | the plate tuned by rendering it. Everything else is measured against this. |
| **MASS** | more VOLUME, and the CHUNKIEST plate | nearly square in plan and THICK. Borromean: **8.00×** the anchor's volume, axes **1 : 0.83 : 0.50** against the anchor's 1 : 0.59 : 0.15. |
| **SPACE** | more ASPECT, and more ROOM | longer and narrower **at the same volume**, so the element reads as SHAPE rather than as size — and, where the species can scale its own surface, a bigger surface too. Borromean: **2× the membrane**, 8.50:1 against the anchor's 1.69:1, volume 1.00×, span 222 against 111. |
| **CHARGE** | armour | FITTED, not authored — see below. |

**Chunky is a claim about SHAPE and only THICKNESS can pay for it.** With the footprint
FITTED (§4c), the one axis left to move a plate toward a cube is the free one — and the free
axis IS the volume, so "make Mass chunkier" and "make Mass heavier" are the same edit. Stop
short of a cube: a cube is not a plate, and the check that says so is worth writing down
(Borromean asserts `min/max < 0.9`).

**A chunkiness rule is asserted over the plates worn AS PLATES, and the exclusion is the
finding.** Borromean CHARGE's plate is `1 : 1.00 : 0.50` — squarer than Mass's — and that is
not a counter-example: its plate is a square slab *because* the body it was fitted against is
the octahedron three times it, so how cube-like the plate is says nothing about what a Charge
plant looks like. *A check that has to be scoped is usually telling you something true about
the thing you scoped out.*

**CHARGE is a different geometry problem and must be fitted, never scaled by eye.** Charge
armours its mass by law (`Flora.ResolveShieldPeriod`), and a shield swaps the plate for its
**CIRCUMSCRIBING octahedron**, which reaches **1.5 × leafSize** from the prism centre —
3× the plate's own reach, 4.5× its volume (`Docs/ECOSYSTEM.md §35`). So what has to look
good on a Charge plant is the SHIELDED form; the plate is what shows between refreshes.
Two measured decisions from the Borromean fit, both of which beat a uniform shrink:

* **Fit the in-plane axes and spend the THICKNESS.** Thickness is spent along the surface
  NORMAL, where the neighbours are not, so it costs almost nothing in clearance — and it is
  the difference between a solid little jewel and a foil. This is the same free axis §4(c)
  records for every element; Charge is simply where it was noticed first.
* **Square the footprint.** The clearance is set by the tightest BOND, which runs along the
  grain, so length bought along the grain is paid for twice. Sweeping the in-plane aspect at
  the shield limit, a square footprint covered **22.5%** of the membrane with octahedra
  against **15.6%** at the anchor's aspect.

**THE REACH — §51's other clause, and the one most likely to be declined by mistake.** A
Space plant reaches `volume^(-1/3)` = ×1.35 and a Mass plant draws in to ×0.82, on whatever
field carries the species' EXTENT. `Flora.ElementalReachScale` returns 1 for a species exempt
from the leaf law on the stated grounds that *a family whose leaf IS its strut needs nothing,
because the anisotropy already lengthened it* — **that is true of the STRUT and false of the
PLANT.** The Mandelbulb family inherited the exemption and grew four plants of the same size
for its whole life (8% extent spread, against the Borromean's 2×). Before you accept the
exemption, ask which of the two the clause was written about.

Taking it is not free where extent and leaf are the SAME dial, and three ways to pay fail
instructively (`Docs/ECOSYSTEM.md §56`): scaling the shell alone is a SIMILARITY that
equalises every element's plant volume; scaling POSITIONS alone DASHES the long-reaching
element and FUSES the compact one, because a prism's length IS the walk's step; and paying
UNIFORMLY on both cross axes — which is volume-exact and looks obviously right — made **MASS
LESS CUBIC**, because a compact element's shrink lands on the axis that was already its
smallest while the pay grows the one that was already largest. What works is paying on the
**THINNEST** cross axis, where the heavy element's growth pulls the axes together and the
light one's shrink drives them apart: one rule, each element more itself, volume exact.
**When a pay can land on any of several free axes, the axis is not a detail.**

**Where the per-element plate LIVES depends on whether it is a preference or a
measurement.** Ordinarily it goes in each config's `FloraVariantTuning.LeafSize`, which
`Flora.ApplyVariantTuning` reads BEFORE `Initialize`, so the prefab's own seed prism gets it
too — and `PrismSizeFixedByGrowthRule` does NOT block it (that guard is about a per-CELL
scale, `SpawnProfileSO.FloraPrismScale`, resizing the leaf out from under a measured bond
table). **But where the plate is FITTED rather than chosen — §4(c) — the species must take
it from its own table instead, through the protected `Flora.LeafSize` setter**, because a
guarantee any asset edit can break is not a guarantee and no config field can know the size
that clears. That is `Flora.ResolveShieldPeriod`'s argument one field over. The element then
has to be resolved from the plant's own crystal at the TOP of `Initialize`, before
`base.Initialize` binds and stamps the prefab's authored prisms — otherwise the SEED prism
wears another element's plate (`BorromeanFlora.ResolveElement`; the ordering
`Flora.ApplyCellPrismScale` already records). Author the config's `LeafSize` to the same
number anyway, so the asset is not silent about the plant it describes.

---

## 6. REPRODUCTION — there are TWO paths and tuning one is dead tuning on the other

**Which path a species is on is decided by whether its FORM is bounded.** A periodic
surface (gyroid, Schwarz P, quasilattice) tiles indefinitely, so its growth rule has an
opinion about where the next PLANT belongs and it reproduces as a COLONY. A **compact**
form — one that closes on itself and is FINISHED, like the Borromean membrane — does not:
it completes and funds an ordinary per-plant offspring out of its growth quota.

That is also the answer to *how much machinery does a new species need*. A compact species
deliberately has **none** of the lattice apparatus — no frontier, no claim book, no
mate-snap tolerance, no `LatticeScale` family of absolute distances (`Docs/ECOSYSTEM.md
§34.8`), no misalignment gate — and inheriting them "for symmetry with the other computed
species" is inheriting a family of coherence tolerances written in world units against a
lattice that does not exist here. *A species whose form is bounded does not need them.*
What it does keep is `PrismSizeFixedByGrowthRule`, because its offsets are still a measured
table in absolute units; it resizes through its own surface scale, which moves the sites and
the leaf together.

A species is on exactly one path and the config gives no hint which:

* **Per-plant growth QUOTA** — `FloraConfigurationSO.GrowthPerOffspring`, spent in
  `Flora.TryReproduce`, earned by `NotifyGrew`. Branching, phyllotactic, Borromean.
* **Colony CYCLE** — a lattice species births ONE plant for the whole population per
  `Cell.CurrentFaunaSpawnPeriod` (`AssembledFlora`, `Docs/ECOSYSTEM.md §32.7`).

`author_flora_populations.py` writes `GrowthPerOffspring` on every config, including the
lattice ones where it is **inert** — the asset says 22, the tests pass, and nothing reports
that the number is never read. Measured: of 102 flora configs, 50 reproduce at all, and
**34 of those 50 are lattice** (including every asset literally named "…Flora Time"). Split
the list by `FloraPrefab` before touching either. Colony cadence keys on the **CONFIG's**
authored element, never the ticking plant's — a colony is mixed-element by construction.

Note the **TIME law** (`Docs/ECOSYSTEM.md §38`): a Time plant reproduces at 1.25× the fleet
rate and Charge/Mass/Space at 0.8×, applied at spawn by `Flora.ResolveGrowthPerOffspring`
and by `AssembledFlora.ColonyCyclePeriod`. It cannot be authored per config; do not try.

---

## 7. BUDGETS — what a flora species costs

State all three when you add or resize one:

1. **Always-on heart colliders = `MaxLivePopulation` × elements deployed.** One per LIVE
   plant, culled by no phase. This is the ceiling that matters; the Lattice cell's 1,080 is
   the shipped high-water mark.
2. **LOD-cullable prisms** = plants × per-plant budget. Bounded by the cell's own
   `FrenzyEnter` COUNT backstop (Frenzy freezes planting AND growth).
3. **Volume**, because volume is the spine. A species whose prisms are not nominal (16)
   makes its cell's `PhaseThresholds` wrong. Per-family exponent:
   `BranchingFlora` lays `leafSize` on all three axes (**s³**); `PhyllotacticFlora` reads
   only `leafSize.x/y` as a CROSS-SECTION (**s²**); a species whose leaf is a measured
   TABLE is exempt via `PrismSizeFixedByGrowthRule`, and its exponent is **0** — the
   per-cell scalar (`SpawnProfileSO.FloraPrismScale`) does not reach it AT ALL, because
   `Flora.ApplyCellPrismScale` returns early. That is a statement about the code, not a
   rounding, and a cell's prism axis will then move some of its species and not others.

A species in **no SpawnProfile** (the worm colony) costs a cell nothing until somebody
adopts it. Say so — and re-prove it by grepping the config GUIDs, because *an "it is wired
nowhere" claim is true only on the date it was written*.

---

## 7.1 ADOPTING a species into a cell

**A cell may be a COLLECTION rather than a forest**, and then the population IS the design:
the Arboretum holds `MaxLivePopulation 1` on each of twenty configs — five species in four
elements — which is a cap and
never a cull: the plant keeps its authored growth quota, cannot spend it while it is the
only one of its kind alive, and the seeder's whole remaining job is extinction recovery. Two
rules for any such cell (`Docs/ECOSYSTEM.md §57`): **quote the per-plant budget, never
re-author it** (on a lattice or a surface species a budget is GEOMETRY, so cutting it ships a
truncated specimen — the Lattice cell may cut a lattice plant to 30 prisms only because a
lattice plant is a TILE), and **read every other per-element field verbatim off the canonical
`_SO_Assets/Lifeforms` asset**, because those are the element palette and a fork is two
sources of truth for one plant's shape.

**And if the species already has a per-cell DEPLOYMENT table, use it rather than authoring a
second copy.** The Borromean four in that cell are not authored by the cell's generator at
all: `author_borromean_flora_assets.py` owns that species' configs in every cell that grows
it, so its `DEPLOYMENTS` row says only *seed, cap, band* and the cell's generator READS the
four it wrote — GUIDs off their own `.meta`, budget and plate through that tool's own table
reader. Two owners for one asset is the trap §2 is about; a cell that quotes instead is a
cell a leaf refit cannot leave stale. **Check the handoff rather than assuming it**: a
`SupportedFloras` entry pointing at a GUID nothing owns resolves to no config at all and grows
nothing, SILENTLY — so fail by name, and say which tool closes it.

Two traps from authoring one, both about IDENTITY:

* **A species key with a SPACE in it can never match a de-spaced name.**
  `author_lifeform_heart_sizes.py` resolves a cell-config variant by prefab GUID and a
  canonical one by asset NAME through `species_of`, which strips spaces — so `"Coral Bloom"`,
  its only two-word key, missed, and those four canonical assets had never been sized by the
  tool that owns their heart. *When one script resolves the same identity two ways, the two
  will disagree, and the name-based half fails silently.*
* **`EnvironmentPrefab == null` is how a world is BUILT, never what it CONTAINS.** Most
  environment-free configs grow a whole world (Lattice, Arboretum, every Rampage, Tollway and
  Wrecking Ball cell). Ask `Cell.IsBareCanvas` instead — §36.10's rule, now on its third
  reader.

The cheap-looking half is the `SupportedFloras` entry. Four things are not cheap, and each
has cost a real pass:

* **A species with per-element geometry is adopted as N configs, not one.** A
  `FloraConfigurationSO` carries ONE `Variant` block, so a rolled config can author one
  leaf, one budget and one **heart size** for elements that may be nothing alike (the
  Borromean four span 180–360 prisms, 804–15,739 plant volume and 108–222 units across).
  `author_lifeform_heart_sizes.py` would then be sizing an average rather than a lifeform.
  Roll only where the elements really are variations of one plant.
* **The cell's volume ladder moves, and the AUTHORED half must not follow it.** A
  play-tested `*EnterVolume` pair is a number a human reached by playing the arena: hold it
  and let the MARGIN absorb the new mass, then state the new margin. A generator that pins
  a `REFERENCE_FOREST_VOLUME` is pinning it for exactly this moment — re-anchor it
  deliberately rather than letting four ladders slide and calling that "unchanged". The
  COUNT half is derived and legitimately moves.
* **One model row for N configs needs an assert.** Density scalars round half UP **per
  config**, and that does not commute with a sum: two seeds × four configs at 3.67 is 28
  plants, eight seeds once is 29. Scale per config, and assert the row divides evenly.
  *A row that prices a forest the game does not grow is worse than no row* — worst on the
  CAP, which is the always-on crystal collider line.
* **Who OWNS the new config.** A species whose budget is a measured table is owned by its
  own generator, not by `author_flora_populations.py` — and that script's `OWNED_ELSEWHERE`
  matching is a **prefix**, which is correct for a CELL-named family and wrong for a
  SPECIES-named one, because a per-cell config is named for the cell first (`Rampage
  Borromean Flora Mass Config Data`). Check which kind you are adding. And if the adopting
  cell's own generator already FORKS its donor's configs, let it fork yours too — two
  owners for one forest is the same defect as two fitters for one asset.

---

## 8. THE TOOL CHAIN

A species whose form is computed gets an offline chain, and the split is deliberate:

```
measure_<species>.py  --write   # derives the table from the definition; slow; emits the C#
verify_<species>_tables.py      # re-proves the SHIPPED table from the points alone; CI gate
author_<species>_assets.py      # prefab + 4 configs + registry wiring; --check
author_lifeform_heart_sizes.py  # owns HeartWorldScale for the whole fleet
author_flora_populations.py     # owns the population numbers (unless OWNED_ELSEWHERE)
author_<cell>_cell.py           # a cell built FROM a species; grows it to size its ladder
```

* **The ORDER matters and is not obvious**: `author_lifeform_heart_sizes.py` writes both the
  canonical `Lifeforms` assets AND every copy under `Cell Configs`, while a cell generator
  READS the canonical heart back. Run the heart sizer first, then the species generator, then
  the cell's — and re-run all three `--check`s, because a cell that copies a stale heart makes
  the disagreement visible for the first time rather than causing it.

* **`--check` must read the DISK.** A `--check` that re-runs the in-memory validation and
  prints "no files written" passes whatever the assets actually say.
* **A slow measurement needs a CACHE keyed on the SOLVER only**, not on the whole library,
  or every edit to an unrelated helper costs the full solve. Key it on the source of the
  functions that can change the result (`inspect.getsource`), and say so.
* **Two fitters must not own one asset.** `author_lifeform_heart_sizes.py` owns
  `HeartWorldScale`; a species generator READS it back rather than authoring it, or the two
  undo each other forever with both `--check`s green.
  **Resizing a plant therefore RE-PRICES its heart**, because that tool sizes every heart as
  `K · bodyDiameter^0.5` — growing the Borromean Space membrane 2× took its heart 2.661 →
  3.379 — and a heart's world scale is read twice AS GAMEPLAY (the collect reward and the
  live domain fauna buff), so a body-size change is a balance change. Its **monotonicity**
  check (a bigger lifeform may never carry a smaller heart) is the one place a body-size bug
  surfaces; re-run it after any geometry change, not just after a heart edit.
* **Register the species in `author_flora_populations.py`'s `OWNED_ELSEWHERE`** if its own
  generator authors the populations, so the fleet tool stands down by name instead of
  silently disagreeing.
* **Every verifier check needs a NEGATIVE CONTROL** under `--self-test`. A check nobody has
  watched fail is a check nobody should trust — and a control has to break the thing the
  check is about, not something correlated with it (sorting an already-sorted table by
  radius is a no-op, so it proves nothing).
* **A check on the WRONG invariant is worse than no check.** The Borromean chain shipped a
  green *"the blocks' radii are non-decreasing"* — true, cheap, negative-controllable, and
  asserting the very property that made the plant grow in disconnected patches. It was
  RETIRED, and its retirement is worth as much as the checks that replaced it. When a check
  passes on a plant that is visibly wrong, the check is a suspect.
* **A solver can fail by being slightly WRONG rather than by failing.** 1,200 Jacobi sweeps
  on a cotangent Laplacian were still 3% above what five sparse solves reach in under a
  second — an inflated membrane that passes every structural check. So assert a property of
  the SURFACE (its AREA) rather than the solver's stopping condition, and prefer a direct
  solve to an iteration you then have to bound.
* **A `ROOT` one `dirname` too shallow writes the whole asset tree into the wrong place —
  and a verifier that shares the bug reads it back and PASSES.** *Consistent wrongness reads
  exactly like correctness.* The path constant carries an `assert` that `Assets/` is under
  it; put the same assert in anything that resolves a repo root.
* **A generated C# constructor's ARITY is a contract several readers parse.** Adding one
  field to the emitted table broke the verifier's regex and
  `author_flora_populations.py`'s, both of which count the ctor's floats — neither failed
  loudly, and one of them is a fleet-wide tool. Grep for every reader of the generated file
  before you widen its signature.

---

## 9. Traps, each of which cost real time

* **A radius sort is not a growth order.** §2.
* **A per-site choice among equal options is noise.** §4(a).
* **`AssemblyBranch` is not a model to copy.** §3.
* **A prism wears its leaf as `localScale`, so NOTHING may be parented under one** — a
  non-uniform scale above a rotated child is a SHEAR, and that is how every lattice species
  grew skewed slivers from its first reseed (`Docs/ECOSYSTEM.md §37.9`). Parent a prism to
  its SPINDLE.
* **`PrismScaleAnimator` clamps a target scale per axis inside the setter, silently**
  (`[0.5, 10]` on 363 of 404 prefabs). Anything that STATES a size calls
  `Prism.AdmitTargetScale(size)` first — `Flora.AddHealthBlock` does. A fitted size that
  does not read on screen means the engine stored something else.
* **A planting band is measured from the CELL CENTRE, not the crystal**
  (`Flora.ResolvePlantCenter`), and it is a volume-uniform BAND, not a shell — a
  uniform-in-radius draw crowds the inner edge because a shell's area grows as r².
* **`FloraVariantTuning` sentinels are `0` (LeafSize) and `-1` (counts), and they mean KEEP
  WHAT YOU HAVE.** A measurement layer that reads a sentinel as a value is measuring the
  wrong plant; a `\d+` regex that skips `-1` by accident rather than by rule is the same bug
  waiting.
* **A `FloraPrefab` reference's component fileID differs by FAMILY** (`PhyllotacticFlora`
  and `BranchingFlora` share `7514956980722975813`; `AssembledFlora` uses
  `8186157953239024492`). Copy it per species from a shipped asset of the same family — a
  wrong one resolves to no component and grows nothing, silently.
* **A flora prefab holds exactly ONE prism** (the seed), because a plant is a growth RULE.
  Anything that photographs or measures a flora must run the rule
  (`Flora.TryPreviewGrowth`), never harvest the prefab's meshes.
* **`Flora.TryPreviewGrowth` must not touch `UnityEngine.Random`** — it is a pure preview and
  must not advance a sequence the simulation is drawing from. Seed a `System.Random`.

---

## 10. Checklist for a new or reworked species

1. `/ecology`: restate which LOCKED invariants the change touches and confirm none is violated.
2. Growth order is connected from the crystal at every tick — **asserted**, with the
   rejected ordering as the negative control.
3. Every limb is aimed at a bond, stretched to it, and scales the spindle's CHILDREN.
4. The orientation field is combed — with the before/after angle measured.
5. Four elements: one anchor, two perturbations, Charge FITTED against `1.5 × leafSize`
   octahedra with a measured clearance and zero shielded overlaps.
6. Budgets stated: heart colliders, prism count, per-prism volume, and whether any cell's
   `PhaseThresholds` have to move.
7. Tool chain green: `measure --check`, `verify --self-test`, `author --check`,
   `author_lifeform_heart_sizes.py --check`, `author_flora_populations.py --check`.
8. Compile the new C# (a stub harness is enough — see the `asset-surgery` skill) and say
   plainly that nothing has been run in the editor, if it has not.
9. If a cell ADOPTS it, run §7.1: N configs not one, re-anchor the reference forest rather
   than letting the authored volume pair float, assert the density row divides evenly across
   the configs, and say who owns them.
10. Record the findings in `Docs/ECOSYSTEM.md` and the invariant in `CLAUDE.md` if the change
    is one — and check no parallel branch has claimed your section number.
